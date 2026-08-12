namespace GraphExtractor;

/// <summary>
/// Setzt aus der autoritativen <see cref="RoutingTruth"/> und dem <see cref="DomainModel"/> den
/// Property-Graph zusammen: Knoten + typisierte, gerichtete Kanten mit Provenienz, plus die abgeleiteten
/// Sichten (Event-Fanout, Kausalketten, Diagnosen).
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
        BuildProcessNodes();
        BuildProjectionNodes();
        BuildQueryNodes();
        BuildPipelineNodes();

        BuildRoutingEdges();     // command → aggregat, aggregat → event  (autoritativ)
        BuildRejectEdges();      // aggregat → ablehnung  (decider)
        BuildProcessEdges();     // event → process, process → command
        BuildConsumerEdges();    // event → projection, query → projection

        ComputeCommandOrigins();
        ComputeEmittedBy();
        ComputeProcessPatterns();
        ComputeContexts();

        BuildFanout();
        BuildCausalChains();
        BuildDiagnostics();

        _g.Meta.Counts = new()
        {
            ["aggregate"] = _g.Nodes.Count(n => n.Kind == NodeKind.aggregate),
            ["command"] = _g.Nodes.Count(n => n.Kind == NodeKind.command),
            ["event"] = _g.Nodes.Count(n => n.Kind == NodeKind.@event),
            ["process"] = _g.Nodes.Count(n => n.Kind == NodeKind.process),
            ["projection"] = _g.Nodes.Count(n => n.Kind == NodeKind.projection),
            ["query"] = _g.Nodes.Count(n => n.Kind == NodeKind.query),
            ["pipeline"] = _g.Nodes.Count(n => n.Kind == NodeKind.pipeline),
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
                Aggregate = new AggregateInfo { State = a.State, Handles = a.HandlesCommandsFull.Select(SimpleCmd).ToList() }
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
                Event = new EventInfo { Persisted = et.Persisted }
            });
        }
    }

    private void BuildProcessNodes()
    {
        foreach (var p in _dom.Processes)
        {
            var rules = new List<SagaRule>();
            for (var i = 0; i < p.Rules.Count; i++)
            {
                var r = p.Rules[i];
                rules.Add(new SagaRule
                {
                    Index = i,
                    When = r.WhenFull.Select(SimpleEvt).ToList(),
                    Join = r.Join,
                    Sammel = r.SammelFull is null ? null : SimpleEvt(r.SammelFull),
                    FanOut = r.FanOut,
                    Sends = SimpleCmd(r.SendsFull),
                    Compensates = r.CompensatesFull is null ? null : SimpleCmd(r.CompensatesFull),
                });
            }
            Add(new Node
            {
                Id = $"proc:{p.Name}", Kind = NodeKind.process, Name = p.Name, Namespace = p.Namespace,
                Process = new ProcessInfo
                {
                    Trigger = SimpleEvt(p.TriggerFull),
                    Registered = _dom.RegisteredProcesses.Contains(p.Name),
                    Rules = rules,
                }
            });
        }
    }

    private void BuildProjectionNodes()
    {
        foreach (var pr in _dom.Projections)
            Add(new Node
            {
                Id = $"proj:{pr.Name}", Kind = NodeKind.projection, Name = pr.Name, FullName = pr.Full,
                Projection = new ProjectionInfo { SubscriberId = pr.SubscriberId, Consumes = pr.ConsumesFull.Select(SimpleEvt).ToList() }
            });
    }

    private void BuildQueryNodes()
    {
        // Reader liefern Query→Projektion.
        var readerByQuery = _dom.Readers
            .SelectMany(r => r.QueryNames.Select(q => (q, r)))
            .GroupBy(x => x.q).ToDictionary(g => g.Key, g => g.First().r, StringComparer.Ordinal);

        foreach (var q in _dom.Queries)
        {
            readerByQuery.TryGetValue(q.Name, out var reader);
            Add(new Node
            {
                Id = $"query:{q.Name}", Kind = NodeKind.query, Name = q.Name, FullName = q.Full,
                Query = new QueryInfo { Fields = q.Fields, ServedByReader = reader?.Name, ReadsProjection = reader?.ProjectionName }
            });
        }
    }

    private void BuildPipelineNodes()
    {
        foreach (var p in _dom.Pipelines)
        {
            var handles = p.Handles.Select(h => new PipelineHandle
            {
                Input = _dom.Events.TryGetValue(h.InputFull, out var et) ? et.Simple : Short(h.InputFull),
                InputKind = h.IsTrigger ? "trigger" : "event",
                Emits = h.EmitsFull.Select(SimpleCmd).ToList()
            }).ToList();
            Add(new Node
            {
                Id = $"pipe:{p.Name}", Kind = NodeKind.pipeline, Name = p.Name, FullName = p.Full,
                Pipeline = new PipelineInfo { PipelineId = p.PipelineId, Handles = handles }
            });
        }
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
            Edge(cmdNode, AggNodeByName(aggName), EdgeKind.routedTo, "GeneratedCommandRouting");
        }

        // Handler-sensitiv: der Command produziert GENAU seine persistierten OneOf-Ausgänge (autoritativ).
        foreach (var (cmdFull, events) in _routing.CommandToEvents)
        {
            if (!_cmdId.TryGetValue(cmdFull, out var cmdNode)) continue;
            var cmd = _byId[cmdNode];
            foreach (var evtFull in events)
                if (_evtId.TryGetValue(evtFull, out var evtNode))
                {
                    Edge(cmdNode, evtNode, EdgeKind.produces, "GeneratedCommandRouting");
                    cmd.Command!.Produces.Add(new CommandOutcome { Event = SimpleEvt(evtFull), Persisted = true, Guard = GuardFor(cmdFull, SimpleEvt(evtFull)) });
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
                    _byId[cmdNode].Command!.Produces.Add(new CommandOutcome { Event = SimpleEvt(evtFull), Persisted = false, Guard = GuardFor(cmdFull, SimpleEvt(evtFull)) });
                }
    }

    private Dictionary<string, string>? _guardMap;

    /// <summary>Der Guard-Ausdruck dieses Zweigs (aus der Decider-Syntax) — das „Warum". Null = Sonst-Zweig.</summary>
    private string? GuardFor(string cmdFull, string evtSimple)
    {
        _guardMap ??= _dom.Aggregates
            .SelectMany(a => a.Guards)
            .GroupBy(kv => kv.Key)
            .ToDictionary(g => g.Key, g => g.First().Value);
        return _guardMap.TryGetValue(cmdFull + "|" + evtSimple, out var g) ? g : null;
    }

    private void BuildProcessEdges()
    {
        foreach (var p in _dom.Processes)
        {
            var procNode = $"proc:{p.Name}";
            if (_evtId.TryGetValue(p.TriggerFull, out var trigNode))
                Edge(trigNode, procNode, EdgeKind.triggers, "process-dsl");

            var conditions = new HashSet<string>(StringComparer.Ordinal);
            for (var i = 0; i < p.Rules.Count; i++)
            {
                var r = p.Rules[i];
                foreach (var w in r.WhenFull) conditions.Add(w);
                if (r.SammelFull != null) conditions.Add(r.SammelFull);

                if (_cmdId.TryGetValue(r.SendsFull, out var sendNode))
                    Edge(procNode, sendNode, EdgeKind.sends, "process-dsl", via: i.ToString());
                if (r.CompensatesFull != null && _cmdId.TryGetValue(r.CompensatesFull, out var compNode))
                    Edge(procNode, compNode, EdgeKind.compensates, "process-dsl", via: i.ToString());
            }

            // Bedingungs-Events (außer dem Auslöser) treiben den Prozess voran.
            foreach (var c in conditions)
                if (c != p.TriggerFull && _evtId.TryGetValue(c, out var cNode))
                    Edge(cNode, procNode, EdgeKind.advances, "process-dsl");
        }
    }

    private void BuildConsumerEdges()
    {
        foreach (var pr in _dom.Projections)
        {
            var projNode = $"proj:{pr.Name}";
            foreach (var evtFull in pr.ConsumesFull)
                if (_evtId.TryGetValue(evtFull, out var evtNode))
                    Edge(evtNode, projNode, EdgeKind.consumedBy, "subscriber");
        }

        var projByName = _dom.Projections.ToDictionary(p => p.Name, p => $"proj:{p.Name}", StringComparer.Ordinal);
        foreach (var q in _dom.Queries)
        {
            var node = _byId[$"query:{q.Name}"];
            if (node.Query?.ReadsProjection is { } projName && projByName.TryGetValue(projName, out var projNode))
                Edge(node.Id, projNode, EdgeKind.readsFrom, "reader");
        }

        foreach (var p in _dom.Pipelines)
        {
            var pipeNode = $"pipe:{p.Name}";
            foreach (var h in p.Handles)
                foreach (var cmdFull in h.EmitsFull)
                    if (_cmdId.TryGetValue(cmdFull, out var cmdNode))
                        Edge(pipeNode, cmdNode, EdgeKind.pipelineEmits, "pipeline");
        }
    }

    // ── Ableitungen auf den Knoten ──────────────────────────────────────────────

    private void ComputeCommandOrigins()
    {
        foreach (var n in _g.Nodes.Where(n => n.Kind == NodeKind.command))
        {
            var origins = new SortedSet<string>(StringComparer.Ordinal);
            foreach (var e in _g.Edges.Where(e => e.To == n.Id))
            {
                if (e.Kind == EdgeKind.sends) origins.Add("process");
                else if (e.Kind == EdgeKind.compensates) origins.Add("process-compensation");
                else if (e.Kind == EdgeKind.pipelineEmits) origins.Add("pipeline");
            }
            // Kein interner Erzeuger und geroutet ⇒ Einstieg von außen (Client) — inkl. Creation-Commands.
            if (n.Command!.IsCreation || origins.Count == 0)
                origins.Add("client");
            n.Command!.Origin = origins.ToList();
        }
    }

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

    private void ComputeProcessPatterns()
    {
        // Verkettung: der Auslöser eines Prozesses wird von einem Command erzeugt, den ein ANDERER Prozess sendet.
        var sentCommands = _g.Edges.Where(e => e.Kind == EdgeKind.sends).Select(e => e.To).ToHashSet();
        foreach (var procNode in _g.Nodes.Where(n => n.Kind == NodeKind.process))
        {
            var pi = procNode.Process!;
            var fanOut = pi.Rules.Any(r => r.FanOut || r.Join == "count");
            var branches = pi.Rules.Count(r => r.Join == "single");
            var joins = pi.Rules.Count(r => r.Join is "and" or "count");

            // Verkettung erkennen: Auslöser-Event wird über einen von einem Prozess gesendeten Command erzeugt.
            var triggerNodeId = _g.Nodes.FirstOrDefault(n => n.Kind == NodeKind.@event && n.Name == pi.Trigger)?.Id;
            var chained = triggerNodeId != null && IsProducedByProcessCommand(triggerNodeId, sentCommands);

            pi.Pattern = fanOut ? "Fan-out"
                : chained ? "Verkettung"
                : (branches >= 2 && joins >= 1) ? "Diamant"
                : "Linear";
        }
    }

    private bool IsProducedByProcessCommand(string eventNodeId, HashSet<string> sentCommandNodeIds)
    {
        // Welche Commands erzeugen dieses Event? produces-Kante ist command→event.
        var producingCmdIds = _g.Edges
            .Where(e => e.Kind == EdgeKind.produces && e.To == eventNodeId)
            .Select(e => e.From)
            .ToHashSet(StringComparer.Ordinal);

        return sentCommandNodeIds.Any(producingCmdIds.Contains);
    }

    /// <summary>
    /// Ordnet jeden Knoten einem Bounded Context (Domäne) zu. Commands/Events erben den Context ihres
    /// behandelnden/emittierenden Aggregats (fachlich richtiger als ihr Deklarations-Namespace); Prozesse
    /// bekommen ihren eigenen; Projektion/Query/Pipeline den Context ihres ersten konsumierten Events/Commands.
    /// So fallen die cross-context-Kanten (Aggregat→Aggregat über eine Saga) sichtbar aus.
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

        foreach (var p in _g.Nodes.Where(n => n.Kind == NodeKind.process))
            p.Context = Ctx(p.Namespace);

        // Projektion/Query/Pipeline: Context des ersten konsumierten Events bzw. emittierten Commands.
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
            q.Context = CtxOfNeighbour(q.Id, EdgeKind.readsFrom, outgoing: true);
        foreach (var pi in _g.Nodes.Where(n => n.Kind == NodeKind.pipeline))
            pi.Context = CtxOfNeighbour(pi.Id, EdgeKind.pipelineEmits, outgoing: true);

        _g.Meta.Contexts = _g.Nodes.Select(n => n.Context).Where(c => c != null)!
            .Distinct().OrderBy(c => c, StringComparer.Ordinal).Cast<string>().ToList();
    }

    private static string Ctx(string? ns)
    {
        if (string.IsNullOrEmpty(ns)) return "(unbekannt)";
        var parts = ns.Split('.');
        return parts.Length >= 2 && parts[0] == "Domain" ? parts[1] : parts[0];
    }

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
            fo.TriggersProcesses = _g.Edges.Where(e => e.From == evt.Id && e.Kind == EdgeKind.triggers)
                .Select(e => _byId[e.To].Name).Distinct().ToList();
            fo.AdvancesProcesses = _g.Edges.Where(e => e.From == evt.Id && e.Kind == EdgeKind.advances)
                .Select(e => _byId[e.To].Name).Distinct().ToList();
            fo.Pipelines = _dom.Pipelines
                .Where(p => p.Handles.Any(h => _dom.Events.TryGetValue(h.InputFull, out var et) && et.Simple == evt.Name))
                .Select(p => p.Name).Distinct().ToList();

            if (fo.Projections.Count + fo.TriggersProcesses.Count + fo.AdvancesProcesses.Count + fo.Pipelines.Count > 0)
                _g.Views.EventFanout.Add(fo);
        }
    }

    private void BuildCausalChains()
    {
        foreach (var proc in _g.Nodes.Where(n => n.Kind == NodeKind.process))
        {
            var pi = proc.Process!;
            var steps = new List<string>();
            foreach (var r in pi.Rules)
            {
                var cond = string.Join(" ∧ ", r.When);
                var arrow = r.FanOut ? "⇉" : (r.Join == "count" ? $"⨝(alle {r.Sammel})" : "→");
                var comp = r.Compensates != null ? $"   ⟲ {r.Compensates}" : "";
                steps.Add($"[{cond}] {arrow} {r.Sends}{comp}");
            }
            _g.Views.CausalChains.Add(new CausalChain { Start = pi.Trigger, Process = proc.Name, Steps = steps });
        }
    }

    private void BuildDiagnostics()
    {
        var d = _g.Views.Diagnostics;

        d.Add(new Finding
        {
            Severity = _routing.Source == "GeneratedCommandRouting" ? "info" : "warning",
            Code = "ROUTING-SOURCE",
            Message = $"Routing-Wahrheit: {_routing.Source}" +
                      (_routing.Source == "GeneratedCommandRouting" ? " (autoritativ, aus dem gebauten Generat)." : " (Decider-Ableitung — Infrastructure nicht gebaut?).")
        });

        // Saga sendet einen Command, den kein Aggregat behandelt → würde zur Laufzeit hängen.
        foreach (var proc in _g.Nodes.Where(n => n.Kind == NodeKind.process))
            foreach (var r in proc.Process!.Rules)
            {
                foreach (var cmd in new[] { r.Sends, r.Compensates })
                {
                    if (cmd == null) continue;
                    var node = _g.Nodes.FirstOrDefault(n => n.Kind == NodeKind.command && n.Name == cmd);
                    if (node?.Command?.RoutedTo == null)
                        d.Add(new Finding { Severity = "error", Code = "UNROUTED-SAGA-CMD",
                            Message = $"Saga '{proc.Name}' sendet '{cmd}', aber kein Aggregat behandelt ihn (Prozess würde hängen)." });
                }
            }

        // Prozess definiert, aber nicht in GeneratedProzessRegeln verdrahtet.
        foreach (var proc in _g.Nodes.Where(n => n.Kind == NodeKind.process && !n.Process!.Registered))
            d.Add(new Finding { Severity = "warning", Code = "PROCESS-NOT-REGISTERED",
                Message = $"Prozess '{proc.Name}' ist definiert, aber NICHT in GeneratedProzessRegeln.Alle registriert (läuft nicht)." });

        // Persistiertes Event ohne jeden Konsumenten (kein Konsument, kein Prozess, keine Pipeline).
        foreach (var evt in _g.Nodes.Where(n => n.Kind == NodeKind.@event && n.Event!.Persisted))
        {
            var hasConsumer = _g.Edges.Any(e => e.From == evt.Id && e.Kind is EdgeKind.consumedBy or EdgeKind.triggers or EdgeKind.advances)
                || _dom.Pipelines.Any(p => p.Handles.Any(h => _dom.Events.TryGetValue(h.InputFull, out var et) && et.Simple == evt.Name));
            var hasProducer = _g.Edges.Any(e => e.To == evt.Id && e.Kind == EdgeKind.produces);
            if (hasProducer && !hasConsumer)
                d.Add(new Finding { Severity = "info", Code = "DANGLING-EVENT",
                    Message = $"Event '{evt.Name}' wird produziert, aber von niemandem konsumiert (keine Projektion/Saga/Pipeline)." });
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
