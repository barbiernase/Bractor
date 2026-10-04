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
        // Leseseiten-Records: Queries, Responses, ReadModels (Marker IQuery/IQueryResponse/IReadModel).
        foreach (var q in dom.Queries.Where(q => q.Meta.IstDomäne).OrderBy(q => q.Full, StringComparer.Ordinal))
            records.Add(AlsRecord(q.Name, RecordArt.Query, q.Fields, q.Meta, dom.Wurzel));
        foreach (var r in dom.Responses.OrderBy(r => r.Full, StringComparer.Ordinal))
            records.Add(AlsRecord(r.Name, RecordArt.Antwort, r.Fields, r.Meta, dom.Wurzel));
        foreach (var r in dom.ReadModels.OrderBy(r => r.Full, StringComparer.Ordinal))
            records.Add(AlsRecord(r.Name, RecordArt.ReadModel, r.Fields, r.Meta, dom.Wurzel));
        // Betrieb: Konfigurations-Records (Ctor-Injektion), Trigger-Nachrichten, Selbst-Nachrichten der Pipelines.
        foreach (var r in dom.Konfigs)
            records.Add(AlsRecord(r.Name, RecordArt.Konfig, r.Fields, r.Meta, dom.Wurzel));
        foreach (var r in dom.Triggers)
            records.Add(AlsRecord(r.Name, RecordArt.Trigger, r.Fields, r.Meta, dom.Wurzel));
        foreach (var r in dom.SelbstNachrichten)
            records.Add(AlsRecord(r.Name, RecordArt.Selbst, r.Fields, r.Meta, dom.Wurzel));

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
                    Ergibt = a.DecideOutcomes[cmdFull].Select(e => new Ausgang { Event = Evt(e) }).ToList(),
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
            ProjektionsSchreiber = dom.ProjektionsSchreiber, ProjektionsSchreiberNamespace = dom.ProjektionsSchreiberNamespace,
            StoreBasis = dom.StoreBasis is { } sb ? new StoreBasis
            {
                Name = sb.Name, Namespace = sb.Ns, Usings = sb.Usings,
                KtorParameter = sb.Ktor.Select(k => new Parameter { Typ = k.Typ, Name = k.Name }).ToList(),
            } : null,
            StoreImplNamespace = dom.StoreImplNamespace,
        };

        return Herkunft.Stempeln(new EditorModell
        {
            Records = records, Enums = enums, Aggregate = aggregate, Decider = decider, Applier = applier, Sagas = sagas, Rahmen = rahmen,
            Akteure = dom.Akteure.Select(x => new Akteur
            {
                Name = x.Name, Namespace = x.Namespace, Darf = x.Darf, Dienst = x.Dienst, Doku = x.Doku, Datei = Rel(x.Datei),
            }).ToList(),
            Lesen = Leseseite(dom),
        });
    }

    // ── Leseseite: Stores (Fähigkeit + Bündel + Impl), Projektionen/Reaktionen, Reader, Pipeline-Fähigkeiten ──────────
    private static Leseseite Leseseite(DomainModel dom)
    {
        string? Rel(string? abs) => Relativ(dom.Wurzel, abs);
        var stores = dom.Stores.Select(s => new Store
        {
            Name = s.Name, Namespace = s.Namespace, Doku = s.Doku, IstBuendel = s.IstBuendel, Datei = Rel(s.Datei),
            // Kanonische Reihenfolge wie die Store-Karte: Schreib-Fns, dann Lese-Fns (je in Code-Reihenfolge).
            Fns = s.Fns.Where(f => !f.IsRead).Concat(s.Fns.Where(f => f.IsRead)).Select(f => new Faehigkeit
            {
                Name = f.Faehigkeit, Namespace = f.FaehigkeitNamespace == s.Namespace ? null : f.FaehigkeitNamespace,
                Methode = f.Name, Lesen = f.IsRead, Rueckgabe = f.RueckgabeVoll, Doku = f.FaehigkeitDoku, Datei = Rel(f.FaehigkeitDatei),
                Parameter = f.ParamsVoll.Select(p => new Parameter { Typ = p.Typ, Name = p.Name, Standard = p.Standard }).ToList(),
            }).ToList(),
            Impl = s.Impl is { } i ? new StoreImpl
            {
                Name = i.Name, Namespace = i.Ns, Datei = Rel(i.Datei), SchreibDatei = Rel(i.SchreibDatei), LeseDatei = Rel(i.LeseDatei),
            } : null,
        }).ToList();

        Handle AlsHandle(string eingang, HandleSigRaw? sig, HandleVertragRaw? v, bool mitRumpf = true) => new()
        {
            Eingang = eingang,
            Parameter = sig?.Parameter ?? "evt",
            Kontext = sig?.Kontext,
            Faehigkeiten = sig?.Faehigkeiten.Select(f => new Parameter { Typ = f.Typ, Name = f.Name }).ToList() ?? [],
            Ausgaenge = AusgangsTypen(v),
            Rueckgabe = sig?.Rueckgabe, Modifikatoren = sig?.Modifikatoren,
            Rumpf = mitRumpf ? sig?.Rumpf : null, Ausdruck = mitRumpf ? sig?.Ausdruck : null,
            Datei = Rel(sig?.Datei),
        };

        var konsumenten = dom.Projections.OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => new Konsument
        {
            Name = p.Name, Namespace = p.Namespace, SubscriberId = p.SubscriberId, Pull = p.Pull, Append = p.Append,
            Handles = p.ConsumesFull.Select(full => Kurz(full))
                .Select(ev => AlsHandle(ev, p.HandleSigs.GetValueOrDefault(ev), p.HandleVertraege.GetValueOrDefault(ev))).ToList(),
            Doku = p.Quelle.Doku, Typart = p.Quelle.Typart, Basen = p.Quelle.Basen, Attribute = p.Quelle.Attribute,
            Zusatz = p.Quelle.Zusatz, Usings = p.Quelle.Usings, Datei = Rel(p.Quelle.Datei),
            Zustand = p.Quelle.Zustand.Count > 0 ? p.Quelle.Zustand : null,
        }).ToList();

        var reader = dom.Readers.OrderBy(r => r.Name, StringComparer.Ordinal).Select(r => new Leser
        {
            Name = r.Name, Namespace = r.Namespace, Projektion = r.ProjectionName, TrackDeps = r.TrackDeps,
            Handles = r.QueryNames.Select(q => AlsHandle(q, r.HandleSigs.GetValueOrDefault(q), r.HandleVertraege.GetValueOrDefault(q))).ToList(),
            Doku = r.Quelle.Doku, Typart = r.Quelle.Typart, Basen = r.Quelle.Basen, Attribute = r.Quelle.Attribute,
            Zusatz = r.Quelle.Zusatz, Usings = r.Quelle.Usings, Datei = Rel(r.Quelle.Datei),
        }).ToList();

        var pipelines = dom.Pipelines.OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => new PipelineKarte
        {
            Name = p.Name, Namespace = p.Namespace, Datei = Rel(p.Datei), PipelineId = p.PipelineId, Konfigs = p.Konfigs,
            Handles = p.Handles.Select(h => AlsHandle(Kurz(h.InputFull), p.HandleSigs.GetValueOrDefault(h.InputFull),
                p.HandleVertraege.GetValueOrDefault(h.InputFull))).ToList(),
            Doku = p.Quelle.Doku, Typart = p.Quelle.Typart, Basen = p.Quelle.Basen, Attribute = p.Quelle.Attribute,
            Zusatz = p.Quelle.Zusatz, Usings = p.Quelle.Usings,
            Zustand = p.Quelle.Zustand.Count > 0 ? p.Quelle.Zustand : null,
        }).ToList();

        return new Leseseite { Stores = stores, Konsumenten = konsumenten, Reader = reader, Pipelines = pipelines };
    }

    /// <summary>
    /// Die Ausgänge eines Handles als Typ-Texte, wie sie in der OneOf-Signatur stehen — Planung als <c>Selbst&lt;T&gt;</c>/
    /// <c>Frist&lt;T&gt;</c>/<c>FristStorno&lt;T&gt;</c> (die Planungs-Typnamen aus dem Vertrag); Fähigkeiten sind Parameter, keine Ausgänge.
    /// </summary>
    internal static List<string> AusgangsTypen(HandleVertragRaw? v) => v == null ? [] : v.Ausgaenge.Where(a => a.Art != "storefn")
        .Select(a => a.Art switch
        {
            "self" => $"{Generisch(Vertrag.SelbstTyp)}<{a.Typ}>",
            "frist" => $"{Generisch(Vertrag.FristTyp)}<{a.Typ}>",
            "fristStorno" => $"{Generisch(Vertrag.FristStornoTyp)}<{a.Typ}>",
            _ => a.Typ,
        }).ToList();

    private static string Generisch(string metadatenName) => metadatenName.Split('`')[0];

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
    /// <summary>Die Ingress-Bindungen der Composition Root als Modell-Bindungen (Trigger, Modus, Ort).</summary>
    public static List<IngressBindung> Ingress(CompositionRoot cr) =>
        cr.Triggers.Select(tb => new IngressBindung { Trigger = tb.MsgName, Modus = tb.Modus, Ort = tb.Route ?? tb.Interval ?? tb.Path }).ToList();

    public static string ZuBoardJson(KnowledgeGraph graph, DomainModel dom, CompositionRoot cr)
    {
        var modell = ZuEditorModell(graph, dom);
        var root = JsonNode.Parse(modell.AlsJson())!.AsObject();
        // Die Leseseite zeigt das Board in seinen eigenen Sammlungen (stores/projektionen/reader/…) — mit den Code-Fakten
        //   darin (Datei, Zusatz, Signatur verbatim); Board → Modell (DomainEditor.BoardLeseseite) liest sie zurück.
        root.Remove("lesen");
        // Die Grammatik der Kompositions-Sprache (EINE Quelle: DomainEditor.Grammatik) — der Editor bietet danach nur gültige
        //   Verbindungen an. Reine Editor-Sicht: EditorModell.Rahmen kennt das Feld nicht (beim Zurücklesen ignoriert).
        root["rahmen"]!.AsObject()["grammatik"] = JsonSerializer.SerializeToNode(Grammatik.AlsJson(), EditorModell.JsonOptionen);
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
        // Handle → Fn = die Fähigkeits-Parameter der Signatur (die Seite kommt aus dem Marker der Fähigkeit).
        string[] StoreFns(Dictionary<string, List<(string Store, string Method, bool IsRead)>> calls, string key) =>
            calls.TryGetValue(key, out var cs)
                ? cs.Select(c => fnId.TryGetValue((c.Store, c.Method, c.IsRead), out var id) ? id : null)
                    .Where(x => x != null).Distinct().ToArray()!
                : System.Array.Empty<string>();
        string[] Liste(Dictionary<string, List<string>> d, string key) =>
            d.TryGetValue(key, out var xs) ? xs.ToArray() : System.Array.Empty<string>();
        string? Rumpf(Dictionary<string, string> d, string key) => d.TryGetValue(key, out var b) ? b : null;
        // Handle-Vertrag (was der Handler erzeugen KANN + welche Fns er rufen DARF) — nur Signatur; Fähigkeiten auf die Board-Fn-Id abgebildet.
        HandleVertragRaw? Vertrag(Dictionary<string, HandleVertragRaw> d, string key) => d.TryGetValue(key, out var v) ? v : null;
        object[] Ausgaenge(HandleVertragRaw? v) => v == null ? System.Array.Empty<object>() : v.Ausgaenge.Select(a => (object)new
        {
            typ = a.Typ, art = a.Art,
            fn = a.Art == "storefn" && a.Store != null && fnId.TryGetValue((a.Store, a.Typ, a.IstLesen), out var id) ? id : null,
            store = a.Store,
        }).ToArray();

        // Query- und Response-Records stehen schon als Records im Modell (Reader docken daran an). ReadModels zeigt das Board
        //   als eigene Sammlung (readModels) — die Record-Form reist dort mit (Zusatz/Basen/…), Board → Modell baut sie zurück.
        // Trigger- und Selbst-Nachrichten zeigt das Board ebenfalls in eigenen Sammlungen (triggers / selbstNachrichten).
        var recordJson = records.Where(r => r != null).ToDictionary(r => $"{r!["kind"]}|{r["name"]}", r => r!, StringComparer.Ordinal);
        foreach (var rm in records.Where(r => (string?)r?["kind"] is RecordArt.ReadModel or RecordArt.Trigger or RecordArt.Selbst).ToList()) records.Remove(rm);
        root["selbstNachrichten"] = new JsonArray(recordJson.Where(kv => kv.Key.StartsWith(RecordArt.Selbst + "|", StringComparison.Ordinal))
            .Select(kv => kv.Value.DeepClone()).ToArray());
        JsonNode? TriggerCode(string name) => recordJson.GetValueOrDefault(RecordArt.Trigger + "|" + name)?.DeepClone();
        var lesen = modell.Lesen ?? new Leseseite();
        var storeVon = lesen.Stores.ToDictionary(s => s.Name, StringComparer.Ordinal);
        var konsumentVon = lesen.Konsumenten.ToDictionary(k => k.Name, StringComparer.Ordinal);
        var leserVon = lesen.Reader.ToDictionary(r => r.Name, StringComparer.Ordinal);
        var pipelineVon = lesen.Pipelines.ToDictionary(p => p.Name, StringComparer.Ordinal);
        // Die Signatur verbatim eines Handles (für Board → Modell: verbinden/lösen ändert nur die Fähigkeits-Parameter).
        static object? Sig(Handle? h) => h == null ? null : new
        {
            parameter = h.Parameter, kontext = h.Kontext, rueckgabe = h.Rueckgabe, modifikatoren = h.Modifikatoren,
            faehigkeitParameter = h.Faehigkeiten.Select(f => new { typ = f.Typ, name = f.Name }).ToArray(),
            ausgangsTypen = h.Ausgaenge, ausdruck = h.Ausdruck, rumpfCode = h.Rumpf, datei = h.Datei, herkunft = h.Herkunft,
        };
        // Konfigurations-Records stehen als Records der Art „konfig" schon im Modell.

        // ReadModels — Dokument-Felder + der Store, der sie liest/schreibt.
        var readModels = dom.ReadModels.Select((rm, i) => new
        {
            _id = "rm" + (i + 1), name = rm.Name, @namespace = rm.Meta.Namespace, store = rm.Store, storeKandidaten = rm.StoreKandidaten,
            felder = rm.Fields.Select(FeldJson).ToArray(),
            typart = rm.Meta.Typart, doku = rm.Meta.Doku, datei = Relativ(dom.Wurzel, rm.Meta.Datei),
            zusatz = rm.Meta.Zusatz, usings = rm.Meta.Usings.Count > 0 ? rm.Meta.Usings : null, basen = rm.Meta.Basen, attribute = rm.Meta.Attribute,
            ohneParameterliste = rm.Meta.OhneParameterliste ? true : (bool?)null,
            herkunft = modell.Records.FirstOrDefault(r => r.Kind == RecordArt.ReadModel && r.Name == rm.Name && r.Namespace == rm.Meta.Namespace)?.Herkunft,
        }).ToList();

        // Projektionen (replaybar/idempotent) und Reaktionen (emittierend: yield Command) getrennt.
        var projektionen = dom.Projections.Where(p => !p.IstReaktion).OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select((p, i) => new
            {
                _id = "pj" + (i + 1), name = p.Name, @namespace = p.Namespace, subscriberId = p.SubscriberId, pull = p.Pull, append = p.Append ? true : (bool?)null,
                code = KlassenCode(konsumentVon.GetValueOrDefault(p.Name)),
                handles = p.ConsumesFull.Select(full => KurzEvt(dom, full)).Select(ev => new
                {
                    @event = ev,
                    sig = Sig(konsumentVon.GetValueOrDefault(p.Name)?.Handles.FirstOrDefault(h => h.Eingang == Kurz(ev))),
                    fns = StoreFns(p.HandleFaehigkeiten, ev),
                    publishes = Liste(p.HandlePublishes, ev),
                    rumpf = Rumpf(p.HandleBodies, ev),
                    form = Vertrag(p.HandleVertraege, ev)?.Form, signatur = Vertrag(p.HandleVertraege, ev)?.Signatur,
                    signaturOffen = Vertrag(p.HandleVertraege, ev)?.SignaturOffen == true ? true : (bool?)null,
                    ausgaenge = Ausgaenge(Vertrag(p.HandleVertraege, ev)),
                }).ToArray(),
            }).ToList();

        var reaktionen = dom.Projections.Where(p => p.IstReaktion).OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select((p, i) => new
            {
                _id = "rk" + (i + 1), name = p.Name, @namespace = p.Namespace, subscriberId = p.SubscriberId, pull = p.Pull,
                append = p.Append ? true : (bool?)null,
                code = KlassenCode(konsumentVon.GetValueOrDefault(p.Name)),
                handles = p.ConsumesFull.Select(full => KurzEvt(dom, full)).Select(ev => new
                {
                    @event = ev,
                    sig = Sig(konsumentVon.GetValueOrDefault(p.Name)?.Handles.FirstOrDefault(h => h.Eingang == Kurz(ev))),
                    sends = Liste(p.HandleSends, ev),
                    publishes = Liste(p.HandlePublishes, ev),
                    rumpf = Rumpf(p.HandleBodies, ev),
                    form = Vertrag(p.HandleVertraege, ev)?.Form, signatur = Vertrag(p.HandleVertraege, ev)?.Signatur,
                    signaturOffen = Vertrag(p.HandleVertraege, ev)?.SignaturOffen == true ? true : (bool?)null,
                    ausgaenge = Ausgaenge(Vertrag(p.HandleVertraege, ev)),
                }).ToArray(),
            }).ToList();

        // Reader — direkt aus dem Roh-Modell (IReader<TProjektion>, Queries, OneOf-Responses).
        var reader = dom.Readers.OrderBy(r => r.Name, StringComparer.Ordinal)
            .Select((r, i) => new
            {
                _id = "rd" + (i + 1), name = r.Name, @namespace = r.Namespace, trackDeps = r.TrackDeps,
                projektion = r.ProjectionName,
                code = KlassenCode(leserVon.GetValueOrDefault(r.Name)),
                handles = r.QueryNames.Select(q => new
                {
                    query = q,
                    sig = Sig(leserVon.GetValueOrDefault(r.Name)?.Handles.FirstOrDefault(h => h.Eingang == q)),
                    fns = StoreFns(r.HandleFaehigkeiten, q),
                    responses = Liste(r.HandleResponses, q),
                    rumpf = Rumpf(r.HandleBodies, q),
                    form = Vertrag(r.HandleVertraege, q)?.Form, signatur = Vertrag(r.HandleVertraege, q)?.Signatur,
                    signaturOffen = Vertrag(r.HandleVertraege, q)?.SignaturOffen == true ? true : (bool?)null,
                    ausgaenge = Ausgaenge(Vertrag(r.HandleVertraege, q)),
                }).ToArray(),
            }).ToList();

        // Pipelines + Trigger — je Handle Ingress (Trigger/Event/Self) → Command/Trigger/Selbst/Frist + Fähigkeiten.
        var triggerFelder = dom.Triggers.ToDictionary(t => t.Name, t => t.Fields, StringComparer.Ordinal);
        var triggerIds = new Dictionary<string, string>(StringComparer.Ordinal); // Trigger-Msg → _id
        var pipelines = dom.Pipelines.OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select((p, pi) => new
            {
                _id = "pl" + (pi + 1), name = p.Name, @namespace = p.Namespace, pipelineId = p.PipelineId,
                datei = pipelineVon.GetValueOrDefault(p.Name)?.Datei,
                code = KlassenCode(pipelineVon.GetValueOrDefault(p.Name)), herkunft = pipelineVon.GetValueOrDefault(p.Name)?.Herkunft,
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
                        sig = Sig(pipelineVon.GetValueOrDefault(p.Name)?.Handles.FirstOrDefault(h => h.Eingang == Kurz(hd.InputFull))),
                        @event = hd.InputKind == "event" ? input : null,
                        selfName = hd.InputKind == "self" ? input : null,
                        input,
                        trigId,
                        sends = hd.EmitsFull.Select(c => dom.Commands.TryGetValue(c, out var ct) ? ct.Simple : Kurz(c)).ToArray(),
                        emits = Liste(p.HandleEmitsTriggers, hd.InputFull),
                        // Selbst-Nachrichten = die Selbst<T>-Varianten der Signatur (Verzögerung ist Rumpf → nicht gezeigt).
                        schedules = (Vertrag(p.HandleVertraege, hd.InputFull)?.Ausgaenge ?? new()).Where(a => a.Art == "self")
                            .Select(a => (object)new { name = a.Typ, delay = "" }).ToArray(),
                        // Veröffentlichte Events (transient → Broker; ein persistentes hier ist ein Grammatik-Verstoß, bleibt aber sichtbar).
                        publishes = (Vertrag(p.HandleVertraege, hd.InputFull)?.Ausgaenge ?? new()).Where(a => a.Art is "transient" or "event")
                            .Select(a => a.Typ).ToArray(),
                        // Frist<TCmd>/FristStorno<TCmd> = ein Command „per Frist" bzw. dessen Storno — ein Ausgang am Handle, kein eigener Knoten.
                        fristen = (Vertrag(p.HandleVertraege, hd.InputFull)?.Ausgaenge ?? new()).Where(a => a.Art is "frist" or "fristStorno")
                            .Select(a => (object)new { command = a.Typ, art = a.Art == "frist" ? "frist" : "storno" }).ToArray(),
                        fns = StoreFns(p.HandleFaehigkeiten, hd.InputFull),
                        // Im Auftrag eines Akteur-Dienstes (z. B. der KI) — ein Handle-Parameter wie eine Fähigkeit.
                        akteur = p.HandleAkteur.GetValueOrDefault(hd.InputFull),
                        rumpf = Rumpf(p.HandleBodies, hd.InputFull),
                        form = Vertrag(p.HandleVertraege, hd.InputFull)?.Form, signatur = Vertrag(p.HandleVertraege, hd.InputFull)?.Signatur,
                        signaturOffen = Vertrag(p.HandleVertraege, hd.InputFull)?.SignaturOffen == true ? true : (bool?)null,
                        ausgaenge = Ausgaenge(Vertrag(p.HandleVertraege, hd.InputFull)),
                    };
                }).ToArray(),
            }).ToList();

        var triggers = triggerIds.OrderBy(kv => kv.Value, StringComparer.Ordinal)
            .Select(kv => new
            {
                _id = kv.Value, name = kv.Key, msgName = kv.Key,
                @namespace = dom.Triggers.FirstOrDefault(t => t.Name == kv.Key)?.Meta.Namespace,
                felder = triggerFelder.TryGetValue(kv.Key, out var tf) ? tf.Select(FeldJson).ToArray() : Array.Empty<object>(),
                code = TriggerCode(kv.Key),
            }).ToList();

        // Stores — aus dem Roh-Domänenmodell (Bündel IStore + je Fn ihre Fähigkeit + Impl-Rümpfe).
        // Je Fn ihre Fähigkeit verbatim (Rückgabe/Parameter/Ort) — die Store-Karte bleibt die Anzeige, „sig" der Code-Fakt.
        object? FnSig(string store, string methode)
        {
            var f = storeVon.GetValueOrDefault(store)?.Fns.FirstOrDefault(x => x.Methode == methode);
            return f == null ? null : new
            {
                rueckgabe = f.Rueckgabe, @namespace = f.Namespace, doku = f.Doku, datei = f.Datei, herkunft = f.Herkunft,
                parameter = f.Parameter.Select(p => new { typ = p.Typ, name = p.Name, standard = p.Standard }).ToArray(),
            };
        }
        var stores = dom.Stores.Select((s, i) => new
        {
            _id = "st" + (i + 1), name = s.Name, @namespace = s.Namespace,
            datei = storeVon.GetValueOrDefault(s.Name)?.Datei, doku = s.Doku, istBuendel = s.IstBuendel ? (bool?)null : false,
            impl = storeVon.GetValueOrDefault(s.Name)?.Impl,
            writeFns = s.Fns.Where(f => !f.IsRead).Select((f, fi) => new
            {
                _id = "wf_" + i + "_" + fi, name = f.Name, faehigkeit = f.Faehigkeit,
                @params = f.Params.Select(p => new { name = p.Name, typ = p.Type }).ToArray(),
                rumpf = f.Body, sig = FnSig(s.Name, f.Name),
            }).ToArray(),
            readFns = s.Fns.Where(f => f.IsRead).Select((f, fi) => new
            {
                _id = "rf_" + i + "_" + fi, name = f.Name, faehigkeit = f.Faehigkeit,
                @params = f.Params.Select(p => new { name = p.Name, typ = p.Type }).ToArray(),
                rueckgabe = f.Return,
                rumpf = f.Body, sig = FnSig(s.Name, f.Name),
            }).ToArray(),
        }).ToList();

        // ── Composition-Root (Betrieb/Host): Webhook-Trigger über die Pipeline-Stubs legen (modus/route),
        //    plus Frist-Relationen, Dienst-Bindungen und HostSettings als eigene Board-Sektionen. ──
        var triggersNode = Knoten(triggers).AsArray();
        foreach (var tb in cr.Triggers)
        {
            var match = triggersNode.FirstOrDefault(n => (string?)n?["msgName"] == tb.MsgName)?.AsObject();
            // Die Bindung aus dem Code (Vorlage für neue Bindungen desselben Modus).
            var bindung = Knoten(new { datei = Relativ(dom.Wurzel, tb.Datei), anweisung = tb.Anweisung, typArgument = tb.TypArgument, ortArgument = tb.OrtArgument, modus = tb.Modus, ort = tb.Route ?? tb.Interval ?? tb.Path });
            if (match != null)
            {
                match["bindung"] = bindung;
                match["modus"] = tb.Modus;
                if (tb.Route != null) match["route"] = tb.Route;
                if (tb.Interval != null) match["intervall"] = tb.Interval;
                if (tb.Path != null) match["pfad"] = tb.Path;
            }
            else
            {
                var neu = Knoten(new
                {
                    _id = "tgw" + (triggersNode.Count + 1), name = tb.Name, msgName = tb.MsgName,
                    @namespace = dom.Triggers.FirstOrDefault(t => t.Name == tb.MsgName)?.Meta.Namespace,
                    modus = tb.Modus, route = tb.Route, intervall = tb.Interval, pfad = tb.Path,
                    felder = triggerFelder.TryGetValue(tb.MsgName, out var tf) ? tf.Select(FeldJson).ToArray() : Array.Empty<object>(),
                }).AsObject();
                neu["code"] = TriggerCode(tb.MsgName);
                neu["bindung"] = bindung;
                triggersNode.Add(neu);
            }
        }
        // Auch Trigger-Nachrichten ohne Pipeline/Bindung sind Code — eigene Karte.
        foreach (var t in dom.Triggers.Where(t => !triggersNode.Any(n => (string?)n?["msgName"] == t.Name)))
        {
            var neu = Knoten(new { _id = "tgc" + (triggersNode.Count + 1), name = t.Name, msgName = t.Name, @namespace = t.Meta.Namespace,
                felder = t.Fields.Select(FeldJson).ToArray() }).AsObject();
            neu["code"] = TriggerCode(t.Name);
            triggersNode.Add(neu);
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
        // Fristen sind Ausgänge der Pipeline-Handles (fristen[]) — kein eigener Board-Knoten mehr (eine Wahrheit: die Signatur).
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

    /// <summary>Die Klasse eines Konsumenten/Readers verbatim (Datei, Form, Basen, Attribute, Zusatz, usings, Doku) — Board-Feld „code".</summary>
    private static object? KlassenCode(object? k) => k switch
    {
        Konsument x => new { datei = x.Datei, doku = x.Doku, typart = x.Typart, basen = x.Basen, attribute = x.Attribute, zusatz = x.Zusatz, usings = x.Usings, herkunft = x.Herkunft, zustand = x.Zustand },
        Leser x => new { datei = x.Datei, doku = x.Doku, typart = x.Typart, basen = x.Basen, attribute = x.Attribute, zusatz = x.Zusatz, usings = x.Usings, herkunft = x.Herkunft },
        PipelineKarte x => new { datei = x.Datei, doku = x.Doku, typart = x.Typart, basen = x.Basen, attribute = x.Attribute, zusatz = x.Zusatz, usings = x.Usings, herkunft = x.Herkunft, zustand = x.Zustand },
        _ => null,
    };

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
