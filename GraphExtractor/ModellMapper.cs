using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using DomainEditor;

namespace GraphExtractor;

/// <summary>
/// Der ROUND-TRIP: bestehenden Code (als Wissensgraph + Roh-Domänenmodell extrahiert) zurück ins
/// record-zentrische <see cref="EditorModell"/> lesen — so vollständig, dass der <see cref="Scaffolder"/>
/// daraus wieder denselben Code erzeugt (Fixpunkt, geprüft von <see cref="ParitaetsPruefung"/>).
///
/// Quelle der Schreibseite ist das Roh-Modell (<see cref="DomainModel"/>): echte OneOf-Reihenfolge, echte
/// Apply-Methoden (nicht aus den Commands abgeleitet), Parameternamen, Defaults, Handcode-Anteile (Zusatz),
/// Doku, Saga-Lambdas verbatim, Value Objects und Enums. Nur Domänen-Typen (Domain*-Assemblies) — Framework-
/// Records (KommandoAbgelehnt, ProzessGestartet …) gehören nicht ins editierbare Modell.
/// </summary>
public static class ModellMapper
{
    public static EditorModell ZuEditorModell(KnowledgeGraph graph, DomainModel dom)
    {
        var simpleEvt = dom.Events.ToDictionary(kv => kv.Key, kv => kv.Value.Simple, StringComparer.Ordinal);
        var simpleCmd = dom.Commands.ToDictionary(kv => kv.Key, kv => kv.Value.Simple, StringComparer.Ordinal);
        string Evt(string full) => simpleEvt.TryGetValue(full, out var s) ? s : Kurz(full);
        string? Rel(string? abs) => Relativ(dom.Wurzel, abs);
        string Cmd(string full) => simpleCmd.TryGetValue(full, out var s) ? s : Kurz(full);

        // Zugehörigkeit aus dem Code: Command → das Aggregat, dessen Decider ihn entscheidet; Event → das Aggregat, das ihn
        // erzeugt (Decide-Ausgang) oder faltet (Apply). Nur wenn EINDEUTIG — sonst gehört er keinem (nicht geraten).
        string? Eindeutig(IEnumerable<string> xs) { var l = xs.Distinct(StringComparer.Ordinal).ToList(); return l.Count == 1 ? l[0] : null; }
        string? AggVonCmd(string full) => Eindeutig(dom.Aggregates.Where(a => a.HandlesCommandsFull.Contains(full)).Select(a => a.Name));
        string? AggVonEvt(string full) => Eindeutig(dom.Aggregates
            .Where(a => a.ApplyEventsFull.Contains(full) || a.DecideOutcomes.Values.Any(o => o.Contains(full))).Select(a => a.Name));

        var records = new List<Record>();
        foreach (var (full, c) in dom.Commands.Where(kv => kv.Value.Meta.IstDomäne).OrderBy(kv => kv.Key, StringComparer.Ordinal))
            records.Add(AlsRecord(c.Simple, RecordArt.Command, c.Fields, c.Meta, dom.Wurzel) with { IstErzeugung = c.IsCreation, Aggregat = AggVonCmd(full) });
        foreach (var (full, e) in dom.Events.Where(kv => kv.Value.Meta.IstDomäne).OrderBy(kv => kv.Key, StringComparer.Ordinal))
            records.Add(AlsRecord(e.Simple, e.Persisted ? RecordArt.Event : RecordArt.Rejection, e.Fields, e.Meta, dom.Wurzel) with { Aggregat = AggVonEvt(full) });
        foreach (var vo in dom.ValueObjects)
            records.Add(AlsRecord(vo.Name, RecordArt.ValueObject, vo.Fields, vo.Meta, dom.Wurzel));

        var enums = dom.Enums.Select(e => new Enumeration
        {
            Name = e.Name, Namespace = e.Meta.Namespace, Werte = e.Werte, Doku = e.Meta.Doku, Datei = Rel(e.Meta.Datei),
        }).ToList();

        var aggregate = new List<Aggregat>();
        var decider = new List<DecideRegel>();
        var applier = new List<ApplyRegel>();
        foreach (var a in dom.Aggregates)
        {
            aggregate.Add(new Aggregat
            {
                Name = a.Name, Namespace = a.Namespace, Doku = a.Doku,
                State = a.State.Select(MappeFeld).ToList(),
                StateZusatz = a.StateZusatz, DeciderZusatz = a.DeciderZusatz, ApplierZusatz = a.ApplierZusatz,
                Usings = a.Usings,
                Datei = Rel(a.Datei), DeciderDatei = Rel(a.DeciderDatei), ApplierDatei = Rel(a.ApplierDatei),
            });

            foreach (var cmdFull in a.HandlesCommandsFull)
                decider.Add(new DecideRegel
                {
                    Aggregat = a.Name,
                    Command = Cmd(cmdFull),
                    Ergibt = a.DecideOutcomes[cmdFull].Select(e => new Ausgang
                    {
                        Event = Evt(e),
                        Guard = a.Guards.TryGetValue(cmdFull + "|" + Evt(e), out var g) ? g : null,
                    }).ToList(),
                    Rumpf = a.DecideBodies.TryGetValue(cmdFull, out var db) ? db : null,
                    Parameter = a.DecideParams.TryGetValue(cmdFull, out var dp) ? dp : "cmd",
                    Datei = Rel(a.DecideDateien.GetValueOrDefault(cmdFull)),
                });

            foreach (var evtFull in a.ApplyEventsFull)
            {
                var ev = Evt(evtFull);
                applier.Add(new ApplyRegel
                {
                    Aggregat = a.Name, Event = ev,
                    Rumpf = a.ApplyBodies.TryGetValue(ev, out var ab) ? ab : null,
                    Parameter = a.ApplyParams.TryGetValue(ev, out var ap) ? ap : "evt",
                    Datei = Rel(a.ApplyDateien.GetValueOrDefault(ev)),
                });
            }
        }

        var sagas = dom.Processes.Select(p => new Saga
        {
            Name = p.Name, Namespace = p.Namespace, TriggerEvent = Evt(p.TriggerFull), Doku = p.Doku,
            ExtraUsings = p.Usings, Datei = Rel(p.Datei),
            Schritte = p.Rules.Select(r => new SagaSchritt
            {
                Wenn = r.WhenFull.Select(Evt).ToList(),
                SammelEvent = r.SammelFull is null ? null : Evt(r.SammelFull),
                SammelAusdruck = r.SammelLambda,
                Sende = Cmd(r.SendsFull),
                SendeJe = r.FanOut,
                SendeAusdruck = r.SendLambda,
                Kompensation = r.CompensatesFull is null ? null : Cmd(r.CompensatesFull),
                KompensationJe = r.CompFanOut,
                KompensationAusdruck = r.CompLambda,
            }).ToList(),
        }).ToList();

        // Der Rahmen AUS DEM CODE: Namenskonvention, globale usings, Verzeichnisse (Namespaces + Projekt-Wurzeln).
        var standard = new Rahmen();
        var verzeichnisse = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (ns, dir) in dom.ProjektWurzeln) if (Relativ(dom.Wurzel, dir) is { } r) verzeichnisse[ns] = r;
        foreach (var (ns, dir) in dom.NamespaceVerzeichnisse) if (Relativ(dom.Wurzel, dir) is { } r) verzeichnisse[ns] = r;
        var rahmen = new Rahmen
        {
            VertragsNamespace = Vertrag.VertragsNamespace,
            GlobaleUsings = dom.GlobaleUsings.ToList(),
            DeciderKlasse = dom.DeciderKlasse ?? standard.DeciderKlasse,
            ApplierKlasse = dom.ApplierKlasse ?? standard.ApplierKlasse,
            DecideMethode = dom.DecideMethode ?? standard.DecideMethode,
            ApplyMethode = dom.ApplyMethode ?? standard.ApplyMethode,
            Verzeichnisse = verzeichnisse,
            Kennung = Kennung(dom.Wurzel),
            Skalare = WireSkalare(),
            OneOfMax = dom.OneOfMax, UndMax = dom.UndMax,
        };

        return new EditorModell
        {
            Records = records, Enums = enums, Aggregate = aggregate, Decider = decider, Applier = applier, Sagas = sagas, Rahmen = rahmen,
        };
    }

    /// <summary>Stabile Kennung der Solution (FNV-1a über den Wurzelpfad) — kein Inhalt, nur Unterscheidung.</summary>
    private static string Kennung(string wurzel)
    {
        var h = 2166136261u;
        foreach (var ch in wurzel) { h ^= ch; h *= 16777619u; }
        return h.ToString("x8");
    }

    /// <summary>
    /// Die Skalare, die der Wire trägt — aus <c>ProtoScalarSpecs</c> (dieselbe Quelle wie Proto-Prepass und DtoMapper):
    /// je nicht-nullbarer Typ-Familie der kurze C#-Name (Schlüsselwort, sonst Typname ohne <c>System.</c>).
    /// </summary>
    private static List<string> WireSkalare() =>
        Core.SourceGeneration.ProtoScalarSpecs.ByCSharpType.Where(kv => !kv.Value.IsNullable)
            .GroupBy(kv => kv.Value)
            .Select(g => g.Select(kv => kv.Key).FirstOrDefault(k => !k.Contains('.')) ?? g.First().Key.Replace("System.", ""))
            .OrderBy(x => x, StringComparer.Ordinal).ToList();

    private static Record AlsRecord(string name, string kind, List<FieldInfo> felder, TypMeta meta, string wurzel) => new()
    {
        Name = name, Kind = kind, Namespace = meta.Namespace, Felder = felder.Select(MappeFeld).ToList(),
        Doku = meta.Doku, Zusatz = meta.Zusatz, Usings = meta.Usings, Datei = Relativ(wurzel, meta.Datei),
        Typart = meta.Typart == "public record" ? null : meta.Typart,
        OhneParameterliste = meta.OhneParameterliste,
        Basen = meta.Basen, Attribute = meta.Attribute,
    };

    /// <summary>Pfad relativ zur Solution, „/"-getrennt (für den Editor/SimHost); null, wenn außerhalb oder unbekannt.</summary>
    private static string? Relativ(string wurzel, string? abs)
    {
        if (string.IsNullOrEmpty(abs) || string.IsNullOrEmpty(wurzel)) return null;
        var rel = Path.GetRelativePath(wurzel, abs);
        return rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel) ? null : rel.Replace('\\', '/');
    }

    // ── Reimport als VOLLES Board-Modell (Schreibseite + Leseseite + Betrieb) ──────────────────
    // camelCase + Nulls weglassen — deckungsgleich mit dem Board-MODEL und EditorModell.JsonOptionen.
    private static readonly JsonSerializerOptions BoardOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Reimport als VOLLES Board-Modell: die Schreibseite (<see cref="ZuEditorModell"/>) PLUS die Leseseite
    /// (Query-/Response-Records, ReadModels, Stores, Projektionen, Reaktionen, Reader) und der Betrieb
    /// (Pipelines mit Trigger/Event/Self-Kanal, Trigger-Nachrichten mit Feldern, Fristen, Dienste,
    /// HostSettings). Die Positionen liefert das Board (autoLayout), nicht dieser Seed.
    /// </summary>
    public static string ZuBoardJson(KnowledgeGraph graph, DomainModel dom, CompositionRoot cr)
    {
        var root = JsonNode.Parse(ZuEditorModell(graph, dom).AlsJson())!.AsObject();
        var records = root["records"]!.AsArray();

        // (Store, Methode, isRead) → Board-Fn-Id — damit Projektion/Reader-Handles die aufgerufene Store-Fn
        // referenzieren (die Kante Projektion/Reader → Store, die sonst nur als Rumpf-Text existiert).
        var fnId = new Dictionary<(string Store, string Method, bool Read), string>();
        for (var i = 0; i < dom.Stores.Count; i++)
        {
            var s = dom.Stores[i];
            var wf = s.Fns.Where(f => !f.IsRead).ToList();
            for (var fi = 0; fi < wf.Count; fi++) fnId[(s.Name, wf[fi].Name, false)] = "wf_" + i + "_" + fi;
            var rf = s.Fns.Where(f => f.IsRead).ToList();
            for (var fi = 0; fi < rf.Count; fi++) fnId[(s.Name, rf[fi].Name, true)] = "rf_" + i + "_" + fi;
        }
        // Die Seite (Lesen/Schreiben) kommt aus dem aufgerufenen Interface, nicht aus der Rolle des Aufrufers.
        string[] StoreFns(Dictionary<string, List<(string Store, string Method, bool IsRead)>> calls, string key) =>
            calls.TryGetValue(key, out var cs)
                ? cs.Select(c => fnId.TryGetValue((c.Store, c.Method, c.IsRead), out var id) ? id : null)
                    .Where(x => x != null).Distinct().ToArray()!
                : System.Array.Empty<string>();
        string[] Liste(Dictionary<string, List<string>> d, string key) =>
            d.TryGetValue(key, out var xs) ? xs.ToArray() : System.Array.Empty<string>();
        string? Rumpf(Dictionary<string, string> d, string key) => d.TryGetValue(key, out var b) ? b : null;

        // Query- und Response-Records — als Board-Records, damit Reader daran andocken.
        foreach (var q in dom.Queries.Where(q => q.Meta.IstDomäne).OrderBy(q => q.Full, StringComparer.Ordinal))
            records.Add(Knoten(RecordJson(q.Name, "query", q.Fields, q.Meta, dom.Wurzel)));
        foreach (var r in dom.Responses)
            records.Add(Knoten(RecordJson(r.Name, "queryresponse", r.Fields, r.Meta, dom.Wurzel)));
        // Konfigurations-Records (per DI in Konsumenten injiziert) — eigene Art, KEIN Value Object.
        foreach (var k in dom.Konfigs)
            records.Add(Knoten(RecordJson(k.Name, "konfig", k.Fields, k.Meta, dom.Wurzel)));

        // ReadModels — Dokument-Felder + der Store, der sie liest/schreibt.
        var readModels = dom.ReadModels.Select((rm, i) => new
        {
            _id = "rm" + (i + 1), name = rm.Name, @namespace = rm.Meta.Namespace, store = rm.Store, storeKandidaten = rm.StoreKandidaten,
            felder = rm.Fields.Select(FeldJson).ToArray(),
            typart = rm.Meta.Typart, doku = rm.Meta.Doku, datei = Relativ(dom.Wurzel, rm.Meta.Datei),
        }).ToList();

        // Projektionen (replaybar/idempotent) und Reaktionen (emittierend: yield Command) getrennt.
        var projektionen = dom.Projections.Where(p => !p.IstReaktion).OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select((p, i) => new
            {
                _id = "pj" + (i + 1), name = p.Name, @namespace = p.Namespace, subscriberId = p.SubscriberId, pull = p.Pull, append = p.Append ? true : (bool?)null,
                handles = p.ConsumesFull.Select(full => KurzEvt(dom, full)).Select(ev => new
                {
                    @event = ev,
                    fns = StoreFns(p.HandleStoreCalls, ev),
                    publishes = Liste(p.HandlePublishes, ev),
                    rumpf = Rumpf(p.HandleBodies, ev),
                }).ToArray(),
            }).ToList();

        var reaktionen = dom.Projections.Where(p => p.IstReaktion).OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select((p, i) => new
            {
                _id = "rk" + (i + 1), name = p.Name, @namespace = p.Namespace, subscriberId = p.SubscriberId, pull = p.Pull,
                handles = p.ConsumesFull.Select(full => KurzEvt(dom, full)).Select(ev => new
                {
                    @event = ev,
                    sends = Liste(p.HandleSends, ev),
                    publishes = Liste(p.HandlePublishes, ev),
                    rumpf = Rumpf(p.HandleBodies, ev),
                }).ToArray(),
            }).ToList();

        // Reader — direkt aus dem Roh-Modell (IReader<TProjektion>, Queries, OneOf-Responses).
        var reader = dom.Readers.OrderBy(r => r.Name, StringComparer.Ordinal)
            .Select((r, i) => new
            {
                _id = "rd" + (i + 1), name = r.Name, @namespace = r.Namespace, trackDeps = r.TrackDeps,
                projektion = r.ProjectionName,
                handles = r.QueryNames.Select(q => new
                {
                    query = q,
                    fns = StoreFns(r.HandleStoreCalls, q),
                    responses = Liste(r.HandleResponses, q),
                    rumpf = Rumpf(r.HandleBodies, q),
                }).ToArray(),
            }).ToList();

        // Pipelines + Trigger — je Handle Ingress (Trigger/Event/Self) → Command/Trigger/ScheduleSelf.
        var triggerFelder = dom.Triggers.ToDictionary(t => t.Name, t => t.Fields, StringComparer.Ordinal);
        var triggerIds = new Dictionary<string, string>(StringComparer.Ordinal); // Trigger-Msg → _id
        var pipelines = dom.Pipelines.OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select((p, pi) => new
            {
                _id = "pl" + (pi + 1), name = p.Name, @namespace = p.Namespace, pipelineId = p.PipelineId,
                konfigs = p.Konfigs.ToArray(),
                dienste = cr.PipelineDienste.TryGetValue(p.Name, out var ds) ? ds.ToArray() : Array.Empty<string>(),
                handles = p.Handles.Select(hd =>
                {
                    var input = KurzEvt(dom, hd.InputFull);
                    string? trigId = null;
                    if (hd.InputKind == "trigger" && !triggerIds.TryGetValue(input, out trigId))
                        triggerIds[input] = trigId = "tg" + (triggerIds.Count + 1);
                    return new
                    {
                        inputKind = hd.InputKind,
                        @event = hd.InputKind == "event" ? input : null,
                        selfName = hd.InputKind == "self" ? input : null,
                        input,
                        trigId,
                        sends = hd.EmitsFull.Select(c => dom.Commands.TryGetValue(c, out var ct) ? ct.Simple : Kurz(c)).ToArray(),
                        emits = Liste(p.HandleEmitsTriggers, hd.InputFull),
                        schedules = p.HandleSchedules.TryGetValue(hd.InputFull, out var sc)
                            ? sc.Select(x => new { name = x.Name, delay = x.Delay }).ToArray()
                            : Array.Empty<object>(),
                        rumpf = Rumpf(p.HandleBodies, hd.InputFull),
                    };
                }).ToArray(),
            }).ToList();

        var triggers = triggerIds.OrderBy(kv => kv.Value, StringComparer.Ordinal)
            .Select(kv => new
            {
                _id = kv.Value, name = kv.Key, msgName = kv.Key,
                felder = triggerFelder.TryGetValue(kv.Key, out var tf) ? tf.Select(FeldJson).ToArray() : Array.Empty<object>(),
            }).ToList();

        // Stores — aus dem Roh-Domänenmodell (I{X}Write/ReadStore + Impl-Rümpfe).
        var stores = dom.Stores.Select((s, i) => new
        {
            _id = "st" + (i + 1), name = s.Name, @namespace = s.Namespace,
            writeFns = s.Fns.Where(f => !f.IsRead).Select((f, fi) => new
            {
                _id = "wf_" + i + "_" + fi, name = f.Name,
                @params = f.Params.Select(p => new { name = p.Name, typ = p.Type }).ToArray(),
                rumpf = f.Body,
            }).ToArray(),
            readFns = s.Fns.Where(f => f.IsRead).Select((f, fi) => new
            {
                _id = "rf_" + i + "_" + fi, name = f.Name,
                @params = f.Params.Select(p => new { name = p.Name, typ = p.Type }).ToArray(),
                rueckgabe = f.Return,
                rumpf = f.Body,
            }).ToArray(),
        }).ToList();

        // ── Composition-Root (Betrieb/Host): Webhook-Trigger über die Pipeline-Stubs legen (modus/route),
        //    plus Frist-Relationen, Dienst-Bindungen und HostSettings als eigene Board-Sektionen. ──
        var triggersNode = Knoten(triggers).AsArray();
        foreach (var tb in cr.Triggers)
        {
            var match = triggersNode.FirstOrDefault(n => (string?)n?["msgName"] == tb.MsgName)?.AsObject();
            if (match != null)
            {
                match["modus"] = tb.Modus;
                if (tb.Route != null) match["route"] = tb.Route;
                if (tb.Interval != null) match["intervall"] = tb.Interval;
                if (tb.Path != null) match["pfad"] = tb.Path;
            }
            else
            {
                triggersNode.Add(Knoten(new
                {
                    _id = "tgw" + (triggersNode.Count + 1), name = tb.Name, msgName = tb.MsgName,
                    modus = tb.Modus, route = tb.Route, intervall = tb.Interval, pfad = tb.Path,
                    felder = triggerFelder.TryGetValue(tb.MsgName, out var tf) ? tf.Select(FeldJson).ToArray() : Array.Empty<object>(),
                }));
            }
        }

        // Graph-Diagnosen (MISSING-APPLY, ENUM-ZERO, UNROUTED-SAGA-CMD …) — der Editor zeigt sie bei „✓ Prüfen".
        root["diagnosen"] = Knoten(graph.Views.Diagnostics.Where(d => d.Severity != "info")
            .Select(d => new { schweregrad = d.Severity, code = d.Code, meldung = d.Message }));
        root["readModels"] = Knoten(readModels);
        root["projektionen"] = Knoten(projektionen);
        root["reaktionen"] = Knoten(reaktionen);
        root["reader"] = Knoten(reader);
        root["pipelines"] = Knoten(pipelines);
        root["triggers"] = triggersNode;
        root["stores"] = Knoten(stores);
        root["frists"] = Knoten(cr.Frists.Select((f, i) => new
        {
            _id = "fr" + (i + 1), name = f.Name, kontext = f.Kontext, sendet = f.Sendet,
            aggregat = f.Aggregat, plant = f.Plant, storniert = f.Storniert, dauerSetting = f.DauerSetting,
        }));
        root["dienste"] = Knoten(cr.Dienste.Select((d, i) => new
        {
            _id = "di" + (i + 1), name = d.Name, vertrag = d.Vertrag, @extern = d.Extern, codeSrc = (string?)null,
        }));
        root["hostSettings"] = Knoten(cr.HostSettings.Select((s, i) => new
        {
            _id = "hs" + (i + 1), name = s.Name, typ = s.Typ, @default = s.Default, envKey = s.EnvKey,
            konfig = s.Konfig, feld = s.Feld,
        }));
        return root.ToJsonString(new JsonSerializerOptions
        {
            WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
    }

    private static object RecordJson(string name, string kind, List<FieldInfo> felder, TypMeta meta, string wurzel) => new
    {
        name, kind, @namespace = meta.Namespace, felder = felder.Select(FeldJson).ToArray(),
        doku = meta.Doku, zusatz = meta.Zusatz, usings = meta.Usings.Count > 0 ? meta.Usings : null,
        datei = Relativ(wurzel, meta.Datei),
        typart = meta.Typart == "public record" ? null : meta.Typart,
        ohneParameterliste = meta.OhneParameterliste ? true : (bool?)null, basen = meta.Basen, attribute = meta.Attribute,
    };

    private static object FeldJson(FieldInfo f) => new { name = f.Name, typ = f.Type, standard = f.Default, zugriff = f.Zugriff, pflicht = f.Pflicht ? true : (bool?)null, elementTyp = f.ElementTyp };

    private static JsonNode Knoten(object o) => JsonSerializer.SerializeToNode(o, BoardOpts)!;

    private static Feld MappeFeld(FieldInfo f) =>
        new() { Name = f.Name, Typ = f.Type, Ausdruck = f.Expr, Standard = f.Default, NurGet = f.NurGet, Zugriff = f.Zugriff, Pflicht = f.Pflicht, ElementTyp = f.ElementTyp };

    private static string KurzEvt(DomainModel dom, string full) =>
        dom.Events.TryGetValue(full, out var e) ? e.Simple : Kurz(full);

    private static string Kurz(string full)
    {
        var i = full.LastIndexOf('.');
        return i >= 0 ? full[(i + 1)..] : full;
    }
}
