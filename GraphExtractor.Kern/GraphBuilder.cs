namespace GraphExtractor;

/// <summary>
/// Setzt aus der autoritativen <see cref="RoutingTruth"/> und dem <see cref="DomainModel"/> den
/// Property-Graph zusammen: Knoten + typisierte, gerichtete Kanten mit Provenienz, plus die abgeleiteten
/// Sichten (Event-Fanout, Diagnosen).
/// </summary>
public sealed class GraphBuilder
{
    private readonly RoutingTruth _routing;
    private readonly DomainModel _dom;

    private readonly KnowledgeGraph _g = new();
    private readonly Dictionary<string, Node> _byId = new(StringComparer.Ordinal);

    // FullName → Node-Id
    private readonly Dictionary<string, string> _cmdId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _evtId = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _aggIdByName = new(StringComparer.Ordinal);

    public GraphBuilder(RoutingTruth routing, DomainModel dom)
    {
        _routing = routing;
        _dom = dom;
    }

    public KnowledgeGraph Build()
    {
        _g.Meta.RoutingSource = _routing.Source;

        BuildAggregateNodes();
        BuildCommandNodes();
        BuildEventNodes();
        BuildProjectionNodes();
        BuildQueryNodes();
        BuildAkteurNodes();      // Akteure, Clients (IClientVertrag) samt Kanten

        BuildRoutingEdges();     // command → aggregat, aggregat → event  (autoritativ)
        BuildRejectEdges();      // aggregat → ablehnung  (decider)
        BuildConsumerEdges();    // event → projection, query → projection

        ComputeEmittedBy();
        ComputeContexts();

        BuildFanout();
        BuildDiagnostics();

        _g.Meta.Counts = new()
        {
            ["aggregate"] = _g.Nodes.Count(n => n.Kind == NodeKind.aggregate),
            ["command"] = _g.Nodes.Count(n => n.Kind == NodeKind.command),
            ["event"] = _g.Nodes.Count(n => n.Kind == NodeKind.@event),
            ["projection"] = _g.Nodes.Count(n => n.Kind == NodeKind.projection),
            ["query"] = _g.Nodes.Count(n => n.Kind == NodeKind.query),
            ["akteur"] = _g.Nodes.Count(n => n.Kind == NodeKind.akteur),
            ["client"] = _g.Nodes.Count(n => n.Kind == NodeKind.client),
            ["edges"] = _g.Edges.Count,
        };
        return _g;
    }

    // ── Knoten ────────────────────────────────────────────────────────────────

    private Node Add(Node n) { _g.Nodes.Add(n); _byId[n.Id] = n; return n; }

    private string UniqueId(string prefix, string simple, Dictionary<string, string> map, string full)
    {
        if (map.TryGetValue(full, out var existing)) return existing;
        var id = $"{prefix}:{simple}";
        var i = 2;
        while (_byId.ContainsKey(id) && _byId[id].FullName != full) id = $"{prefix}:{simple}#{i++}";
        map[full] = id;
        return id;
    }

    private void BuildAggregateNodes()
    {
        foreach (var a in _dom.Aggregates)
        {
            var id = $"agg:{a.Name}";
            if (_byId.ContainsKey(id)) continue;
            _aggIdByName[a.Name] = id;
            Add(new Node
            {
                Id = id, Kind = NodeKind.aggregate, Name = a.Name, FullName = a.Full, Namespace = a.Namespace,
                Aggregate = new AggregateInfo
                {
                    State = a.State,
                    Handles = a.HandlesCommandsFull.Select(SimpleCmd).ToList(),
                    DecideBodies = a.DecideBodies.GroupBy(kv => SimpleCmd(kv.Key)).ToDictionary(g => g.Key, g => g.First().Value),
                    ApplyBodies = a.ApplyBodies,
                }
            });
        }
    }

    /// <summary>Aggregat-Knoten per Name (aus dem Routing als String) — legt bei Bedarf einen Stub an.</summary>
    private string AggNodeByName(string name)
    {
        if (_aggIdByName.TryGetValue(name, out var id)) return id;
        id = $"agg:{name}";
        _aggIdByName[name] = id;
        Add(new Node { Id = id, Kind = NodeKind.aggregate, Name = name, Aggregate = new AggregateInfo() });
        return id;
    }

    private void BuildCommandNodes()
    {
        foreach (var (full, ct) in _dom.Commands)
        {
            var id = UniqueId("cmd", ct.Simple, _cmdId, full);
            if (_byId.ContainsKey(id)) continue;
            _routing.CommandToAggregate.TryGetValue(full, out var routedTo);
            Add(new Node
            {
                Id = id, Kind = NodeKind.command, Name = ct.Simple, FullName = full, Namespace = NsOf(full),
                Command = new CommandInfo { IsCreation = ct.IsCreation, RoutedTo = routedTo, Fields = ct.Fields }
            });
        }
    }

    private void BuildEventNodes()
    {
        foreach (var (full, et) in _dom.Events)
        {
            var id = UniqueId("evt", et.Simple, _evtId, full);
            if (_byId.ContainsKey(id)) continue;
            Add(new Node
            {
                Id = id, Kind = NodeKind.@event, Name = et.Simple, FullName = full, Namespace = NsOf(full),
                Event = new EventInfo { Persisted = et.Persisted, Fields = et.Fields }
            });
        }
    }

    private void BuildProjectionNodes()
    {
        foreach (var pr in _dom.Projections)
            Add(new Node
            {
                Id = $"proj:{pr.Name}", Kind = NodeKind.projection, Name = pr.Name, FullName = pr.Full, Namespace = pr.Namespace,
                Projection = new ProjectionInfo { MindestensEinmal = pr.MindestensEinmal, Consumes = pr.ConsumesFull.Select(SimpleEvt).ToList() }
            });
    }

    private void BuildQueryNodes()
    {
        // Reader liefern Query→Projektion (über ihre Lese-Fähigkeiten).
        var readerByQuery = _dom.Readers
            .SelectMany(r => r.QueryNames.Select(q => (q, r)))
            .GroupBy(x => x.q).ToDictionary(g => g.Key, g => g.First().r, StringComparer.Ordinal);
        // Aggregate liefern Query→Aggregat (Frage im Decider, docs/03 §3.2).
        var aggregatByQuery = _dom.Aggregates
            .SelectMany(a => a.Fragen.Select(f => (q: f.QueryFull, a.Name)))
            .GroupBy(x => x.q).ToDictionary(g => g.Key, g => g.First().Name, StringComparer.Ordinal);

        foreach (var q in _dom.Queries)
        {
            readerByQuery.TryGetValue(q.Name, out var reader);
            Add(new Node
            {
                Id = $"query:{q.Name}", Kind = NodeKind.query, Name = q.Name, FullName = q.Full, Namespace = q.Meta.Namespace,
                Query = new QueryInfo
                {
                    Fields = q.Fields, ServedByReader = reader?.Name, ReadsProjections = reader?.Projektionen.ToList() ?? new(),
                    AnsweredByAggregate = aggregatByQuery.GetValueOrDefault(q.Full),
                }
            });
        }
    }

    /// <summary>
    /// Akteure und Clients als Knoten (docs/konzept-akteure.md): <c>akteur:X</c> —darf→ Command/Query; <c>client:X</c> —traegt/sendet/fragt→,
    /// Event —hoert→ Client, Client —verkoerpert→ Akteur.
    /// </summary>
    private void BuildAkteurNodes()
    {
        string? NachrichtId(string simple) =>
            _g.Nodes.FirstOrDefault(n => n.Name == simple && n.Kind is NodeKind.command or NodeKind.@event or NodeKind.query)?.Id;
        var kanten = new List<(string, string, EdgeKind, string?)>();
        foreach (var a in _dom.Akteure)
        {
            var aid = $"akteur:{a.Name}";
            Add(new Node
            {
                Id = aid, Kind = NodeKind.akteur, Name = a.Name, FullName = a.Full, Namespace = a.Namespace,
                Akteur = new AkteurInfo { Art = a.Art, Darf = a.Darf.ToList() },
            });
            foreach (var d in a.Darf)
                if (NachrichtId(d) is { } ziel) kanten.Add((aid, ziel, EdgeKind.darf, null));
        }
        // Clients (docs/konzept-akteure.md §4, docs/konzept-akteure.md §5): client:X —traegt→ Funktion, —sendet→ Command, —fragt→ Query, Event —hoert→ client,
        //   client —verkoerpert→ akteur (abgeleitet wie AkteurRechteGenerator).
        foreach (var c in _dom.Clients)
        {
            var cid = $"client:{c.Name}";
            var verk = Abstractions.Clientvertragsregeln.Verkoerpert(
                c.Traegt.Concat(c.Sendet).Concat(c.Fragt).Select(t => _dom.Akteure.Where(a => a.Darf.Contains(t)).Select(a => a.Name)));
            Add(new Node
            {
                Id = cid, Kind = NodeKind.client, Name = c.Name, FullName = c.Full, Namespace = c.Namespace,
                Client = new ClientInfo
                {
                    Handshake = Abstractions.Clientvertragsregeln.ClientName(c.Name), Traegt = c.Traegt.ToList(), Sendet = c.Sendet.ToList(),
                    Fragt = c.Fragt.ToList(), Kenntnis = c.Kenntnis.ToList(), Verkoerpert = verk.ToList(),
                },
            });
            foreach (var t in c.Traegt)
                if (_dom.BausteinFunktionen.FirstOrDefault(f => f.Name == t) is { } f) kanten.Add((cid, $"funktion:{f.Name}", EdgeKind.traegt, t));
            foreach (var s in c.Sendet) if (NachrichtId(s) is { } ziel) kanten.Add((cid, ziel, EdgeKind.sendet, null));
            foreach (var q in c.Fragt) if (NachrichtId(q) is { } ziel) kanten.Add((cid, ziel, EdgeKind.fragt, null));
            foreach (var e in c.Kenntnis) if (NachrichtId(e) is { } ein) kanten.Add((ein, cid, EdgeKind.hoert, null));
            foreach (var a in verk) kanten.Add((cid, $"akteur:{a}", EdgeKind.verkoerpert, null));
        }
        foreach (var (von, nach, art, via) in kanten) Edge(von, nach, art, "akteur", via);
    }

    // ── Kanten ────────────────────────────────────────────────────────────────

    private void Edge(string from, string to, EdgeKind kind, string provenance, string? via = null)
    {
        if (from is null || to is null || !_byId.ContainsKey(from) || !_byId.ContainsKey(to)) return;
        _g.Edges.Add(new Edge { From = from, To = to, Kind = kind, Provenance = provenance, Via = via });
    }

    private void BuildRoutingEdges()
    {
        foreach (var (cmdFull, aggName) in _routing.CommandToAggregate)
        {
            if (!_cmdId.TryGetValue(cmdFull, out var cmdNode)) continue;
            Edge(cmdNode, AggNodeByName(aggName), EdgeKind.routedTo, _routing.Source);
        }

        // Handler-sensitiv: der Command produziert GENAU seine persistierten OneOf-Ausgänge (autoritativ).
        foreach (var (cmdFull, events) in _routing.CommandToEvents)
        {
            if (!_cmdId.TryGetValue(cmdFull, out var cmdNode)) continue;
            var cmd = _byId[cmdNode];
            foreach (var evtFull in events)
                if (_evtId.TryGetValue(evtFull, out var evtNode))
                {
                    Edge(cmdNode, evtNode, EdgeKind.produces, _routing.Source);
                    cmd.Command!.Produces.Add(new CommandOutcome { Event = SimpleEvt(evtFull), Persisted = true });
                }
        }
    }

    private void BuildRejectEdges()
    {
        // Die andere Hälfte des OneOf: die Ablehnungen (ITransientEvent) je Command, aus dem Decider.
        foreach (var a in _dom.Aggregates)
            foreach (var (cmdFull, evtFull) in a.Rejects)
                if (_cmdId.TryGetValue(cmdFull, out var cmdNode) && _evtId.TryGetValue(evtFull, out var evtNode))
                {
                    Edge(cmdNode, evtNode, EdgeKind.produces, "decider");
                    _byId[cmdNode].Command!.Produces.Add(new CommandOutcome { Event = SimpleEvt(evtFull), Persisted = false });
                }
    }

    private void BuildConsumerEdges()
    {
        foreach (var pr in _dom.Projections)
        {
            var projNode = $"proj:{pr.Name}";
            foreach (var evtFull in pr.ConsumesFull)
                if (_evtId.TryGetValue(evtFull, out var evtNode))
                    Edge(evtNode, projNode, EdgeKind.consumedBy, "projektion");
        }

        // Tolerant: ein doppelter Projektions-Name (halb editiert) ist ein Lese-Befund (ModellMapper), kein Absturz.
        var projByName = _dom.Projections.GroupBy(p => p.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => $"proj:{g.Key}", StringComparer.Ordinal);
        foreach (var q in _dom.Queries)
        {
            var node = _byId[$"query:{q.Name}"];
            foreach (var projName in node.Query?.ReadsProjections ?? [])
                if (projByName.TryGetValue(projName, out var projNode))
                    Edge(node.Id, projNode, EdgeKind.readsFrom, "reader");
            if (node.Query?.AnsweredByAggregate is { } agg && _byId.ContainsKey($"agg:{agg}"))
                Edge(node.Id, $"agg:{agg}", EdgeKind.answeredBy, "frage");
        }
    }

    // ── Ableitungen auf den Knoten ──────────────────────────────────────────────

    private void ComputeEmittedBy()
    {
        // produces-Kante ist command→event; das emittierende Aggregat ist das, an das der Command geroutet ist.
        foreach (var e in _g.Edges.Where(e => e.Kind == EdgeKind.produces))
        {
            var cmd = _byId[e.From];
            var evt = _byId[e.To];
            var aggName = cmd.Command?.RoutedTo;
            if (aggName == null) continue;
            evt.Event!.EmittedBy ??= aggName;
            if (_aggIdByName.TryGetValue(aggName, out var aggId) && _byId[aggId].Aggregate is { } ai && !ai.Emits.Contains(evt.Name))
                ai.Emits.Add(evt.Name);
        }
    }

    /// <summary>
    /// Ordnet jeden Knoten einem Bounded Context (Domäne) zu. Commands/Events erben den Context ihres
    /// behandelnden/emittierenden Aggregats (fachlich richtiger als ihr Deklarations-Namespace);
    /// Projektion/Query den Context ihres ersten konsumierten Events bzw. ihrer Projektion.
    /// </summary>
    private void ComputeContexts()
    {
        var aggCtxByName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var a in _g.Nodes.Where(n => n.Kind == NodeKind.aggregate))
        {
            a.Context = Ctx(a.Namespace);
            aggCtxByName[a.Name] = a.Context;
        }

        foreach (var c in _g.Nodes.Where(n => n.Kind == NodeKind.command))
            c.Context = c.Command?.RoutedTo is { } agg && aggCtxByName.TryGetValue(agg, out var ctx) ? ctx : Ctx(c.Namespace);

        foreach (var e in _g.Nodes.Where(n => n.Kind == NodeKind.@event))
            e.Context = e.Event?.EmittedBy is { } agg && aggCtxByName.TryGetValue(agg, out var ctx) ? ctx : Ctx(e.Namespace);

        // Projektion/Query: Context des ersten konsumierten Events bzw. der gelesenen Projektion.
        string CtxOfNeighbour(string nodeId, EdgeKind kind, bool outgoing)
        {
            var e = _g.Edges.FirstOrDefault(x => x.Kind == kind && (outgoing ? x.From == nodeId : x.To == nodeId));
            if (e == null) return "(quer)";
            var other = _byId.GetValueOrDefault(outgoing ? e.To : e.From);
            return other?.Context ?? "(quer)";
        }
        foreach (var pr in _g.Nodes.Where(n => n.Kind == NodeKind.projection))
            pr.Context = _g.Edges.Where(x => x.Kind == EdgeKind.consumedBy && x.To == pr.Id)
                .Select(x => _byId[x.From].Context).FirstOrDefault(c => c != null) ?? "(quer)";
        foreach (var q in _g.Nodes.Where(n => n.Kind == NodeKind.query))
            q.Context = q.Query?.AnsweredByAggregate != null
                ? CtxOfNeighbour(q.Id, EdgeKind.answeredBy, outgoing: true)
                : CtxOfNeighbour(q.Id, EdgeKind.readsFrom, outgoing: true);

        _g.Meta.Contexts = _g.Nodes.Select(n => n.Context).Where(c => c != null)!
            .Distinct().OrderBy(c => c, StringComparer.Ordinal).Cast<string>().ToList();
    }

    /// <summary>
    /// Kontext eines Knotens = sein voller Namespace, so wie der Code ihn deklariert — kein Segment-Raten über einen
    /// gemeinsamen Präfix. (Zugehörigkeit zu einem Aggregat ergibt sich aus den Kanten, nicht aus dem Namespace.)
    /// </summary>
    private static string Ctx(string? ns) => string.IsNullOrEmpty(ns) ? "(unbekannt)" : ns;

    private static string NsOf(string full)
    {
        var i = full.LastIndexOf('.');
        return i >= 0 ? full[..i] : full;
    }

    // ── Abgeleitete Sichten ──────────────────────────────────────────────────

    private void BuildFanout()
    {
        foreach (var evt in _g.Nodes.Where(n => n.Kind == NodeKind.@event))
        {
            var fo = new EventFanout { Event = evt.Name, Persisted = evt.Event!.Persisted };
            fo.Projections = _g.Edges.Where(e => e.From == evt.Id && e.Kind == EdgeKind.consumedBy)
                .Select(e => _byId[e.To].Name).Distinct().ToList();
            fo.Funktionen = FunktionenAuf(evt.FullName).ToList();

            if (fo.Projections.Count + fo.Funktionen.Count > 0)
                _g.Views.EventFanout.Add(fo);
        }
    }

    /// <summary>Die Funktionen, die das Event (voller Name) als Eingang eines <c>Auf</c> nehmen.</summary>
    private IEnumerable<string> FunktionenAuf(string? evtFull) =>
        _dom.BausteinFunktionen.Where(f => f.Eingaenge.Any(e => e.EingangFull == evtFull)).Select(f => f.Name).Distinct();

    private void BuildDiagnostics()
    {
        var d = _g.Views.Diagnostics;

        d.Add(new Finding
        {
            // Warm (Werkbank, E7) sind die Decide-Signaturen der Primärweg — nur ein FEHLENDES Generat beim kalten Lesen ist eine Warnung.
            Severity = _routing.Source == RoutingTruth.Fallback ? "warning" : "info",
            Code = "ROUTING-SOURCE",
            Message = $"Routing-Wahrheit: {_routing.Source}" + (_routing.Source switch
            {
                RoutingTruth.Generat => " (autoritativ, aus dem gebauten Generat).",
                RoutingTruth.DecideSignaturen => " (aus den Decide-Signaturen — warmes Lesen der Werkbank; das Generat ist die Gegenprobe in --check).",
                _ => " (Decider-Ableitung — Infrastructure nicht gebaut?).",
            })
        });

        // Die Regeln, die hier früher standen, prüft der Build (Werkbank P7.4, „Prüfen = Bauen“): MISSING-APPLY = CQRS079,
        //   ENUM-ZERO = CQRS081, UNUSED-EVENT/RESPONSE-OHNE-QUERY = CQRS085, STORE-AMBIGUOUS = CQRS053, HANDLE-OFFEN = CQRS050.

        // Persistiertes Event ohne jeden Konsumenten (keine Projektion, keine Funktion).
        foreach (var evt in _g.Nodes.Where(n => n.Kind == NodeKind.@event && n.Event!.Persisted))
        {
            var hasConsumer = _g.Edges.Any(e => e.From == evt.Id && e.Kind == EdgeKind.consumedBy) || FunktionenAuf(evt.FullName).Any();
            var hasProducer = _g.Edges.Any(e => e.To == evt.Id && e.Kind == EdgeKind.produces);
            if (hasProducer && !hasConsumer)
                d.Add(new Finding { Severity = "info", Code = "DANGLING-EVENT",
                    Message = $"Event '{evt.Name}' wird produziert, aber von niemandem konsumiert (keine Projektion, keine Funktion)." });
        }
    }

    // ── Namens-Helfer ──────────────────────────────────────────────────────────

    private string SimpleCmd(string full) =>
        _dom.Commands.TryGetValue(full, out var c) ? c.Simple :
        _routing.CommandSimpleName.TryGetValue(full, out var s) ? s : Short(full);

    private string SimpleEvt(string full) =>
        _dom.Events.TryGetValue(full, out var e) ? e.Simple : Short(full);

    private static string Short(string full)
    {
        var i = full.LastIndexOf('.');
        return i >= 0 ? full[(i + 1)..] : full;
    }
}
