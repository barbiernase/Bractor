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
/// Doku, Value Objects und Enums. Nur Domänen-Typen (Domain*-Assemblies) — Framework-Records (FristGesetzt …)
/// gehören nicht ins editierbare Modell.
/// </summary>
public static class ModellMapper
{
    public static EditorModell ZuEditorModell(KnowledgeGraph graph, DomainModel dom)
    {
        var simpleEvt = dom.Events.ToDictionary(kv => kv.Key, kv => kv.Value.Simple, StringComparer.Ordinal);
        var simpleCmd = dom.Commands.ToDictionary(kv => kv.Key, kv => kv.Value.Simple, StringComparer.Ordinal);
        string Evt(string full) => full.StartsWith("Frist<", StringComparison.Ordinal) ? $"Frist<{Cmd(full[6..^1])}>"
            : simpleEvt.TryGetValue(full, out var s) ? s : Kurz(full);
        string? Rel(string? abs) => Relativ(dom.Wurzel, abs);
        string Cmd(string full) => simpleCmd.TryGetValue(full, out var s) ? s : Kurz(full);

        // Zugehörigkeit aus dem Code: Command → das Aggregat, dessen Decider ihn entscheidet; Event → das Aggregat, das ihn
        // erzeugt (Decide-Ausgang) oder faltet (Apply). Nur wenn EINDEUTIG — sonst gehört er keinem (nicht geraten).
        string? Eindeutig(IEnumerable<string> xs) { var l = xs.Distinct(StringComparer.Ordinal).ToList(); return l.Count == 1 ? l[0] : null; }
        string? AggVonEvt(string full) => Eindeutig(dom.Aggregates
            .Where(a => a.ApplyEventsFull.Contains(full) || a.DecideOutcomes.Values.Any(o => o.Contains(full))).Select(a => a.Name));

        var records = new List<Record>();
        foreach (var (full, c) in dom.Commands.Where(kv => kv.Value.Meta.IstDomäne).OrderBy(kv => kv.Key, StringComparer.Ordinal))
            records.Add(AlsRecord(c.Simple, RecordArt.Command, c.Fields, c.Meta, dom.Wurzel) with { IstErzeugung = c.IsCreation, Aggregat = c.Ziel });
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
            records.Add(AlsRecord(r.Name, RecordArt.ReadModel, r.Fields, r.Meta, dom.Wurzel) with { Geteilt = r.Geteilt });
        // Konfigurations-Records (Ctor-Injektion).
        foreach (var r in dom.Konfigs)
            records.Add(AlsRecord(r.Name, RecordArt.Konfig, r.Fields, r.Meta, dom.Wurzel));

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
                Fragen = a.Fragen.Select(f => new AggregatFrage { Query = Kurz(f.QueryFull), Antworten = f.AntwortenFull.Select(Kurz).ToList() }).ToList(),
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
            PythonPaket = PythonClients.Paket(dom.Wurzel),
            Kennung = Kennung(dom.Wurzel),
            Skalare = WireSkalare(),
            OneOfMax = dom.OneOfMax,
            StoreBasis = dom.StoreBasis is { } sb ? new StoreBasis
            {
                Name = sb.Name, Namespace = sb.Ns, Usings = sb.Usings,
                KtorParameter = sb.Ktor.Select(k => new Parameter { Typ = k.Typ, Name = k.Name }).ToList(),
            } : null,
            StoreImplNamespace = dom.StoreImplNamespace,
        };

        var pyImpls = PythonClients.Lies(dom.Wurzel);
        return Herkunft.Stempeln(new EditorModell
        {
            Records = records, Enums = enums, Aggregate = aggregate, Decider = decider, Applier = applier, Rahmen = rahmen,
            BausteinFunktionen = dom.BausteinFunktionen.Select(f => new BausteinFunktion
            {
                Name = f.Name, Namespace = f.Namespace, Klasse = f.Klasse, Doku = f.Doku, Datei = Rel(f.Datei),
                Zustand = f.ZustandFull is { } z ? Kurz(z) : null, EineAdresse = f.EineAdresse, Lebenszyklus = f.Lebenszyklus,
                Eingaenge = f.Eingaenge.Select(e => new BausteinEingang
                {
                    Eingang = Kurz(e.EingangFull), Hinweis = e.Hinweis, Herkunft = e.Herkunft, Strom = e.Strom,
                    Faehigkeiten = e.Faehigkeiten.Select(p => new Parameter { Typ = p.Typ, Name = p.Name }).ToList(),
                    Ausgaben = e.AusgabenFull.Select(Kurz).ToList(),
                }).ToList(),
                Konstruktor = f.Konstruktor.Select(p => new Parameter { Typ = p.Typ, Name = p.Name }).ToList(),
                Umsetzungen = f.UmsetzungenFull.Select(u => new FunktionsUmsetzung
                {
                    Name = Kurz(u),
                    Konstruktor = f.UmsetzungsKonstruktoren.GetValueOrDefault(u, []).Select(p => new Parameter { Typ = p.Typ, Name = p.Name }).ToList(),
                    Datei = Rel(f.UmsetzungsDateien.GetValueOrDefault(u)),
                }).ToList(),
            }).ToList(),
            Dienste = dom.Dienste.Select(d => new Dienst
            {
                Name = d.Name, Namespace = d.Namespace, Doku = d.Doku, Datei = Rel(d.Datei),
                Methoden = d.Methoden.Select(me => new DienstMethode
                {
                    Name = me.Name, Rueckgabe = me.Rueckgabe, Doku = me.Doku,
                    Parameter = me.Parameter.Select(p => new Parameter { Typ = p.Typ, Name = p.Name }).ToList(),
                }).ToList(),
                Implementierungen = d.Impls.Select(i => new DienstImpl
                {
                    Name = i.Name, Datei = Rel(i.Datei),
                    Konstruktor = i.Konstruktor.Select(p => new Parameter { Typ = p.Typ, Name = p.Name }).ToList(),
                    Rumpfe = i.Rumpfe.Select(r => new DienstRumpf { Methode = r.Methode, Rumpf = r.Rumpf }).ToList(),
                }).ToList(),
            }).ToList(),
            Akteure = dom.Akteure.Select(x => new Akteur
            {
                Name = x.Name, Namespace = x.Namespace, Darf = x.Darf, Art = x.Art, Doku = x.Doku, Datei = Rel(x.Datei),
            }).ToList(),
            Clients = dom.Clients.Select(x => new Client
            {
                Name = x.Name, Namespace = x.Namespace, Verkoerpert = x.Verkoerpert, Kenntnis = x.Kenntnis,
                Doku = x.Doku, Datei = Rel(x.Datei),
                // Handshake-Name = Interface ohne führendes I — derselbe Name, unter dem die generierte Python-Basis den Client führt.
                Implementierungen = pyImpls.GetValueOrDefault(x.Name.Length > 1 && x.Name[0] == 'I' && char.IsUpper(x.Name[1]) ? x.Name[1..] : x.Name) ?? [],
            }).ToList(),
            Lesen = Leseseite(dom),
        });
    }

    // ── Leseseite: Stores (Fähigkeit + Bündel + Impl), Projektionen, Reader ──────────
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
            Klassen = s.Klassen.ToList(),
        }).ToList();

        Handle AlsHandle(string eingang, HandleSigRaw? sig, HandleVertragRaw? v, bool mitRumpf = true) => new()
        {
            Eingang = eingang,
            Parameter = sig?.Parameter ?? "e",
            HerkunftParameter = sig?.HerkunftParameter, AbbruchParameter = sig?.AbbruchParameter,
            Faehigkeiten = sig?.Faehigkeiten.Select(f => new Parameter { Typ = f.Typ, Name = f.Name }).ToList() ?? [],
            Ausgaenge = AusgangsTypen(v),
            Rueckgabe = sig?.Rueckgabe, Modifikatoren = sig?.Modifikatoren,
            Rumpf = mitRumpf ? sig?.Rumpf : null, Ausdruck = mitRumpf ? sig?.Ausdruck : null,
            Datei = Rel(sig?.Datei),
        };

        var konsumenten = dom.Projections.OrderBy(p => p.Name, StringComparer.Ordinal).Select(p => new Konsument
        {
            Name = p.Name, Namespace = p.Namespace, MindestensEinmal = p.MindestensEinmal,
            Handles = p.ConsumesFull.Select(full => Kurz(full))
                .Select(ev => AlsHandle(ev, p.HandleSigs.GetValueOrDefault(ev), p.HandleVertraege.GetValueOrDefault(ev))).ToList(),
            Doku = p.Quelle.Doku, Typart = p.Quelle.Typart, Basen = p.Quelle.Basen, Attribute = p.Quelle.Attribute,
            Zusatz = p.Quelle.Zusatz, Usings = p.Quelle.Usings, Datei = Rel(p.Quelle.Datei),
            Zustand = p.Quelle.Zustand.Count > 0 ? p.Quelle.Zustand : null,
        }).ToList();

        var reader = dom.Readers.OrderBy(r => r.Name, StringComparer.Ordinal).Select(r => new Leser
        {
            Name = r.Name, Namespace = r.Namespace,
            Handles = r.QueryNames.Select(q => AlsHandle(q, r.HandleSigs.GetValueOrDefault(q), r.HandleVertraege.GetValueOrDefault(q))).ToList(),
            Doku = r.Quelle.Doku, Typart = r.Quelle.Typart, Basen = r.Quelle.Basen, Attribute = r.Quelle.Attribute,
            Zusatz = r.Quelle.Zusatz, Usings = r.Quelle.Usings, Datei = Rel(r.Quelle.Datei),
        }).ToList();

        return new Leseseite { Stores = stores, Konsumenten = konsumenten, Reader = reader };
    }

    /// <summary>Die Ausgänge eines Handles als Typ-Texte, wie sie in der OneOf-Signatur stehen; Fähigkeiten sind Parameter, keine Ausgänge.</summary>
    internal static List<string> AusgangsTypen(HandleVertragRaw? v) => v == null ? [] : v.Ausgaenge.Where(a => a.Art != "storefn").Select(a => a.Typ).ToList();

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
        Basen = meta.Basen, Attribute = meta.Attribute, Treiber = meta.Treiber,
    };

    /// <summary>Pfad relativ zur Solution, „/"-getrennt (für den Editor/die Werkbank); null, wenn außerhalb oder unbekannt.</summary>
    private static string? Relativ(string wurzel, string? abs)
    {
        if (string.IsNullOrEmpty(abs) || string.IsNullOrEmpty(wurzel)) return null;
        var rel = Path.GetRelativePath(wurzel, abs);
        return rel.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(rel) ? null : rel.Replace('\\', '/');
    }

    // ── Reimport als VOLLES Board-Modell (Schreibseite + Leseseite) ──────────────────
    // camelCase + Nulls weglassen — deckungsgleich mit dem Board-MODEL und EditorModell.JsonOptionen.
    private static readonly JsonSerializerOptions BoardOpts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    /// <summary>
    /// Reimport als VOLLES Board-Modell: die Schreibseite (<see cref="ZuEditorModell"/>) PLUS die Leseseite
    /// (Query-/Response-Records, ReadModels, Stores, Projektionen, Reader). Die Positionen liefert das Board (autoLayout), nicht dieser Seed.
    /// </summary>
    public static string ZuBoardJson(KnowledgeGraph graph, DomainModel dom, CompositionRoot cr, JsonObject? umgebungen = null) =>
        Serialisiere(ZuBoard(graph, dom, cr, umgebungen));

    /// <summary>Das Board als JSON-Text (eingerückt, Umlaute unmaskiert) — dieselbe Form wie <c>domain-model.json</c>.</summary>
    public static string Serialisiere(JsonObject board) => board.ToJsonString(new JsonSerializerOptions
    {
        WriteIndented = true, Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    });

    /// <summary>Wie <see cref="ZuBoardJson"/>, aber als Knoten (die Werkbank hält ihn im Speicher).</summary>
    /// <param name="mitSicht">false: ohne <c>sicht</c> (das Lesen der Werkbank rechnet sie als eigene, gemessene Phase dazu).</param>
    public static JsonObject ZuBoard(KnowledgeGraph graph, DomainModel dom, CompositionRoot cr, JsonObject? umgebungen = null, bool mitSicht = true)
    {
        // Code mit Fehlern (Werkbank P7.1): was das Lesen nicht fassen konnte, wird ein Befund an der Karte — nie still verworfen.
        var befunde = new List<Lesebefund>();
        var modell = ZuEditorModell(graph, dom);
        var root = JsonNode.Parse(modell.AlsJson())!.AsObject();
        // Die Leseseite zeigt das Board in seinen eigenen Sammlungen (stores/projektionen/reader/…) — mit den Code-Fakten
        //   darin (Datei, Zusatz, Signatur verbatim); Board → Modell (DomainEditor.BoardLeseseite) liest sie zurück.
        root.Remove("lesen");
        // Die Grammatik der Kompositions-Sprache (EINE Quelle: DomainEditor.Grammatik) — der Editor bietet danach nur gültige
        //   Verbindungen an. Reine Editor-Sicht: EditorModell.Rahmen kennt das Feld nicht (beim Zurücklesen ignoriert).
        root["rahmen"]!.AsObject()["grammatik"] = JsonSerializer.SerializeToNode(Grammatik.AlsJson(), EditorModell.JsonOptionen);
        // Von welchem Akteur kommt was (direkt / über die Kette) — Grundlage der Sicht (Akteure je Karte, Akteur-Rahmen, Pfade).
        root["rahmen"]!.AsObject()["akteurMengen"] = JsonSerializer.SerializeToNode(AkteurAnteile.Aus(modell).AlsJson(), EditorModell.JsonOptionen);
        // Quellen (docs/04 §4.7): die fest codierten Treiber (Basis + Felder, zum Anlegen neuer Quellen-Typen); dazu die
        //   Umgebungen (§4) roh — die Quellkarte zeigt die Werte je Umgebung, der Editor rechnet nichts davon nach.
        root["rahmen"]!.AsObject()["treiber"] = Knoten(dom.Treiber.ToDictionary(kv => kv.Key,
            kv => new { basis = kv.Value.Basis, felder = kv.Value.Felder.Select(FeldJson).ToArray() }));
        root["rahmen"]!.AsObject()["umgebungen"] = umgebungen?.DeepClone() ?? new JsonObject();
        var records = root["records"]!.AsArray();
        StabileIds(root);

        // (Store, Methode, isRead) → Board-Fn-Id — damit Projektion/Reader-Handles die aufgerufene Store-Fn
        // referenzieren (die Kante Projektion/Reader → Store, die sonst nur als Rumpf-Text existiert).
        var fnId = new Dictionary<(string Store, string Method, bool Read), string>();
        for (var i = 0; i < dom.Stores.Count; i++)
        {
            var s = dom.Stores[i];
            var wf = s.Fns.Where(f => !f.IsRead).ToList();
            for (var fi = 0; fi < wf.Count; fi++) fnId[(s.Name, wf[fi].Name, false)] = FnId(s.Name, wf[fi].Name, false);
            var rf = s.Fns.Where(f => f.IsRead).ToList();
            for (var fi = 0; fi < rf.Count; fi++) fnId[(s.Name, rf[fi].Name, true)] = FnId(s.Name, rf[fi].Name, true);
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
        foreach (var rm in records.Where(r => (string?)r?["kind"] == RecordArt.ReadModel).ToList()) records.Remove(rm);
        var lesen = modell.Lesen ?? new Leseseite();
        var storeVon = Eindeutig(lesen.Stores, s => s.Name, s => s.Namespace, "store:", befunde);
        var konsumentVon = Eindeutig(lesen.Konsumenten, k => k.Name, k => k.Namespace, "projektion:", befunde);
        var leserVon = Eindeutig(lesen.Reader, r => r.Name, r => r.Namespace, "reader:", befunde);
        // Die Signatur verbatim eines Handles (für Board → Modell: verbinden/lösen ändert nur die Fähigkeits-Parameter).
        static object? Sig(Handle? h) => h == null ? null : new
        {
            parameter = h.Parameter, herkunftParameter = h.HerkunftParameter, abbruchParameter = h.AbbruchParameter, rueckgabe = h.Rueckgabe, modifikatoren = h.Modifikatoren,
            faehigkeitParameter = h.Faehigkeiten.Select(f => new { typ = f.Typ, name = f.Name }).ToArray(),
            ausgangsTypen = h.Ausgaenge, ausdruck = h.Ausdruck, rumpfCode = h.Rumpf, datei = h.Datei, herkunft = h.Herkunft,
        };
        // Konfigurations-Records stehen als Records der Art „konfig" schon im Modell.

        // ReadModels — Dokument-Felder + der Store, der sie liest/schreibt.
        var readModels = dom.ReadModels.Select((rm, i) => new
        {
            _id = "readmodel:" + rm.Name, name = rm.Name, @namespace = rm.Meta.Namespace, store = rm.Store, storeKandidaten = rm.StoreKandidaten,
            felder = rm.Fields.Select(FeldJson).ToArray(),
            typart = rm.Meta.Typart, doku = rm.Meta.Doku, datei = Relativ(dom.Wurzel, rm.Meta.Datei),
            zusatz = rm.Meta.Zusatz, usings = rm.Meta.Usings.Count > 0 ? rm.Meta.Usings : null, basen = rm.Meta.Basen, attribute = rm.Meta.Attribute,
            ohneParameterliste = rm.Meta.OhneParameterliste ? true : (bool?)null,
            geteilt = rm.Geteilt ? true : (bool?)null,
            herkunft = modell.Records.FirstOrDefault(r => r.Kind == RecordArt.ReadModel && r.Name == rm.Name && r.Namespace == rm.Meta.Namespace)?.Herkunft,
        }).ToList();

        // Projektionen.
        var projektionen = dom.Projections.OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select((p, i) => new
            {
                _id = Abstractions.WirkerId.FuerProjektion(p.Name), name = p.Name, @namespace = p.Namespace,
                mindestensEinmal = p.MindestensEinmal ? true : (bool?)null,
                code = KlassenCode(konsumentVon.GetValueOrDefault(p.Name)),
                handles = p.ConsumesFull.Select(full => KurzEvt(dom, full)).Select(ev => new
                {
                    @event = ev,
                    sig = Sig(konsumentVon.GetValueOrDefault(p.Name)?.Handles.FirstOrDefault(h => h.Eingang == Kurz(ev))),
                    _ort = OrtJson(dom, p.HandleSigs.GetValueOrDefault(ev)?.Ort),
                    fns = StoreFns(p.HandleFaehigkeiten, ev),
                    rumpf = Rumpf(p.HandleBodies, ev),
                    form = Vertrag(p.HandleVertraege, ev)?.Form, signatur = Vertrag(p.HandleVertraege, ev)?.Signatur,
                    signaturOffen = Vertrag(p.HandleVertraege, ev)?.SignaturOffen == true ? true : (bool?)null,
                    ausgaenge = Ausgaenge(Vertrag(p.HandleVertraege, ev)),
                }).ToArray(),
            }).ToList();

        // Reader — direkt aus dem Roh-Modell (IReader, Queries, OneOf-Antworten; die Projektionen dahinter aus den Lese-Fähigkeiten).
        var reader = dom.Readers.OrderBy(r => r.Name, StringComparer.Ordinal)
            .Select((r, i) => new
            {
                _id = "reader:" + r.Name, name = r.Name, @namespace = r.Namespace,
                projektionen = r.Projektionen,
                code = KlassenCode(leserVon.GetValueOrDefault(r.Name)),
                handles = r.QueryNames.Select(q => new
                {
                    query = q,
                    sig = Sig(leserVon.GetValueOrDefault(r.Name)?.Handles.FirstOrDefault(h => h.Eingang == q)),
                    _ort = OrtJson(dom, r.HandleSigs.GetValueOrDefault(q)?.Ort),
                    fns = StoreFns(r.HandleFaehigkeiten, q),
                    responses = Liste(r.HandleResponses, q),
                    rumpf = Rumpf(r.HandleBodies, q),
                    form = Vertrag(r.HandleVertraege, q)?.Form, signatur = Vertrag(r.HandleVertraege, q)?.Signatur,
                    signaturOffen = Vertrag(r.HandleVertraege, q)?.SignaturOffen == true ? true : (bool?)null,
                    ausgaenge = Ausgaenge(Vertrag(r.HandleVertraege, q)),
                }).ToArray(),
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
            _id = "store:" + s.Name, name = s.Name, @namespace = s.Namespace,
            datei = storeVon.GetValueOrDefault(s.Name)?.Datei, doku = s.Doku, istBuendel = s.IstBuendel ? (bool?)null : false,
            impl = storeVon.GetValueOrDefault(s.Name)?.Impl,
            klassen = s.Klassen,
            writeFns = s.Fns.Where(f => !f.IsRead).Select((f, fi) => new
            {
                _id = FnId(s.Name, f.Name, false), name = f.Name, faehigkeit = f.Faehigkeit,
                @params = f.Params.Select(p => new { name = p.Name, typ = p.Type }).ToArray(),
                rumpf = f.Body, sig = FnSig(s.Name, f.Name),
            }).ToArray(),
            readFns = s.Fns.Where(f => f.IsRead).Select((f, fi) => new
            {
                _id = FnId(s.Name, f.Name, true), name = f.Name, faehigkeit = f.Faehigkeit,
                @params = f.Params.Select(p => new { name = p.Name, typ = p.Type }).ToArray(),
                rueckgabe = f.Return,
                rumpf = f.Body, sig = FnSig(s.Name, f.Name),
            }).ToArray(),
        }).ToList();

        root["readModels"] = Knoten(readModels);
        root["projektionen"] = Knoten(projektionen);
        root["reader"] = Knoten(reader);
        root["stores"] = Knoten(stores);
        Orte(root, dom);

        // Unaufgelöste Basistypen: ein Typ, den deshalb kein Marker erkennt, wird eine Code-Karte mit ⚠ (roher Code, Diagnose); hat er
        //   trotzdem eine Karte (z. B. nur ein Zusatz-Interface fehlt), hängt der Befund dort.
        var codeKarten = new JsonArray();
        foreach (var u in dom.Unaufgeloest)
        {
            var karte = KartenIds.Zu(u.Name, null, root);
            if (karte == null)
            {
                karte = "code:" + u.Name;
                codeKarten.Add(Knoten(new
                {
                    _id = karte, name = u.Name, @namespace = u.Namespace, art = u.Art, datei = Relativ(dom.Wurzel, u.Datei), code = u.Code,
                    unaufgeloest = u.Basen, _ort = OrtJson(dom, new Ort(u.Datei ?? "", u.Start, u.Ende, u.Zeile)),
                }));
            }
            befunde.Add(new Lesebefund("LESEN-BASIS", $"Basistyp nicht aufgelöst: {string.Join(", ", u.Basen)} — {u.Name} wird nicht als Baustein erkannt",
                karte, u.Datei, u.Zeile));
        }
        // Vollständigkeit (Zielbild §9): jede Typ-Deklaration der Editor-Projekte, die keine Karte über _ort/_teile abdeckt, wird eine Code-Karte —
        //   roher Code in ihrer Domäne. Der Extractor rät nichts; was er nicht als Baustein erkennt, zeigt er so.
        root["codeKarten"] = codeKarten;
        CodeKarten(root, dom, codeKarten);
        root["lesebefunde"] = Knoten(befunde.Select(b => new { code = b.Code, text = b.Text, karte = b.Karte, datei = Relativ(dom.Wurzel, b.Datei), zeile = b.Zeile }));
        // Die Ableitungen (Werkbank P7.3): Domäne, Block, Akteure, Kanten, Inseln, Einklappen, Kette, Clients — der Editor rechnet nichts davon nach.
        if (mitSicht) root["sicht"] = Sicht.Aus(root).AlsJson();
        return root;
    }

    /// <summary>
    /// Code-Karten für jede nicht abgedeckte Typ-Deklaration oberster Ebene (partial: die Teile einer Karte). Abgedeckt ist, was in einer
    /// <c>_ort</c>- oder <c>_teile</c>-Spanne irgendeiner Karte liegt. Id <c>code:Name</c>, bei gleichem Namen in zwei Namespaces <c>code:Ns.Name</c>.
    /// </summary>
    private static void CodeKarten(JsonObject root, DomainModel dom, JsonArray codeKarten)
    {
        var spannen = Abdeckung(root);
        bool Abgedeckt(TypDeklaration d) => Relativ(dom.Wurzel, d.Ort.Datei) is { } rel && spannen.TryGetValue(rel, out var l)
            && l.Any(x => x.Start <= d.Ort.Start && d.Ort.Ende <= x.Ende);
        var offen = dom.Deklarationen.Where(d => !Abgedeckt(d)).GroupBy(d => d.Namespace + "." + d.Name, StringComparer.Ordinal).ToList();
        var mehrfach = offen.GroupBy(g => g.First().Name, StringComparer.Ordinal).Where(g => g.Count() > 1).Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        foreach (var g in offen)
        {
            var (erste, rest) = (g.First(), g.Skip(1).ToList());
            var karte = Knoten(new
            {
                _id = "code:" + (mehrfach.Contains(erste.Name) ? g.Key : erste.Name), name = erste.Name, @namespace = erste.Namespace, art = erste.Art,
                datei = Relativ(dom.Wurzel, erste.Ort.Datei), code = string.Join("\n\n", g.Select(d => d.Code)), _ort = OrtJson(dom, erste.Ort),
            });
            if (rest.Count > 0) karte["_teile"] = new JsonArray(rest.Select(d => (JsonNode?)OrtJson(dom, d.Ort)).ToArray());
            codeKarten.Add(karte);
        }
    }

    /// <summary>Alle Spannen, die Karten belegen (<c>_ort</c> und <c>_teile</c>, auch an verschachtelten Karten wie Dienst-Methoden), je Datei.</summary>
    public static Dictionary<string, List<(int Start, int Ende)>> Abdeckung(params JsonNode?[] knoten)
    {
        var aus = new Dictionary<string, List<(int, int)>>(StringComparer.Ordinal);
        void Merke(JsonNode? o)
        {
            if (o is JsonObject { } x && (string?)x["datei"] is { } d && x["start"] is { } st && x["ende"] is { } en)
                (aus.TryGetValue(d, out var l) ? l : aus[d] = new()).Add(((int)st, (int)en));
        }
        void Lauf(JsonNode? n)
        {
            switch (n)
            {
                case JsonObject o:
                    foreach (var (k, v) in o)
                        if (k == "_ort") Merke(v);
                        else if (k == "_teile") foreach (var t in v as JsonArray ?? []) Merke(t);
                        else if (k != "sicht") Lauf(v);
                    break;
                case JsonArray a: foreach (var x in a) Lauf(x); break;
            }
        }
        foreach (var k in knoten) Lauf(k);
        return aus;
    }

    /// <summary>Nach Namen nachschlagen — tolerant: ein doppelter Name (halb editiertes Duplikat) wird ein Befund, der erste gilt.</summary>
    private static Dictionary<string, T> Eindeutig<T>(IEnumerable<T> xs, Func<T, string> name, Func<T, string> ns, string kartenPraefix, List<Lesebefund> befunde)
    {
        var aus = new Dictionary<string, T>(StringComparer.Ordinal);
        foreach (var g in xs.GroupBy(name, StringComparer.Ordinal))
        {
            aus[g.Key] = g.First();
            if (g.Count() > 1)
                befunde.Add(new Lesebefund("LESEN-DOPPELT", $"Name {g.Key} gibt es mehrfach ({string.Join(", ", g.Select(x => ns(x) + "." + g.Key))}) — gezeigt wird der erste",
                    kartenPraefix + g.Key, null, 0));
        }
        return aus;
    }

    /// <summary>Ein Ort als Board-Feld: Datei relativ zur Solution, Spanne der Deklaration, Zeile.</summary>
    private static JsonObject? OrtJson(DomainModel dom, Ort? o) =>
        o == null || Relativ(dom.Wurzel, o.Datei) is not { } rel ? null
            : o.Bis > 0 ? new JsonObject { ["datei"] = rel, ["start"] = o.Start, ["ende"] = o.Ende, ["zeile"] = o.Zeile, ["bis"] = o.Bis }
            : new JsonObject { ["datei"] = rel, ["start"] = o.Start, ["ende"] = o.Ende, ["zeile"] = o.Zeile };

    /// <summary>
    /// Karten tragen Datei + Spanne (<c>_ort</c>): Typ-Karten über ihre Deklaration (bei partial die in ihrer Datei), Decide/Apply über ihre
    /// Methode, Handles über ihre Methode (beim Erzeugen gesetzt).
    /// </summary>
    private static void Orte(JsonObject root, DomainModel dom)
    {
        IEnumerable<JsonObject> Alle(JsonNode? n) => (n as JsonArray ?? new JsonArray()).OfType<JsonObject>();
        string S(JsonObject o, string k) => (string?)o[k] ?? "";
        Ort? TypOrt(string full, string? datei)
        {
            if (!dom.TypOrte.TryGetValue(full, out var l) || l.Count == 0) return null;
            return l.FirstOrDefault(o => datei != null && Relativ(dom.Wurzel, o.Datei) == datei) ?? l[0];
        }
        // Die übrigen Deklarationen, die zur Karte gehören (weitere partial-Teile; Decider/Applier außerhalb des State; die Umsetzungen
        //   einer Funktion; die Implementierungen eines Dienstes oder Stores) — „_teile": beim Löschen der Karte gehen sie mit. Verschachteltes
        //   zählt nicht extra.
        void Teile(JsonObject k, Ort? haupt, IEnumerable<string> weitereTypen)
        {
            var orte = new List<Ort>();
            bool Enthalten(Ort o) => orte.Append(haupt).Any(x => x != null && x.Datei == o.Datei && x.Start <= o.Start && o.Ende <= x.Ende);
            foreach (var o in weitereTypen.SelectMany(t => dom.TypOrte.GetValueOrDefault(t) ?? []))
                if (!Enthalten(o)) orte.Add(o);
            if (orte.Count > 0) k["_teile"] = new JsonArray(orte.Select(o => (JsonNode?)OrtJson(dom, o)).Where(x => x != null).ToArray());
        }
        var storeVon = dom.Stores.GroupBy(x => x.Namespace + "." + x.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var dienstVon = dom.Dienste.GroupBy(d => d.Full, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var bausteinVon = dom.BausteinFunktionen.GroupBy(f => f.Full, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var sammlung in new[] { "records", "enums", "bausteinFunktionen", "dienste", "akteure", "clients", "readModels", "projektionen", "reader", "stores" })
            foreach (var k in Alle(root[sammlung]))
            {
                var full = S(k, "namespace") + "." + S(k, "name");
                var haupt = TypOrt(full, (string?)k["datei"]);
                k["_ort"] = OrtJson(dom, haupt);
                var weitere = new List<string> { full };
                // Eine Funktion draußen: ihre Umsetzungen im Host gehören zur Karte.
                if (sammlung == "bausteinFunktionen" && bausteinVon.TryGetValue(full, out var bf)) weitere.AddRange(bf.UmsetzungenFull);
                // Ein Dienst: seine Implementierungen gehören zur Karte; je Methode steht der Rumpf in der Implementierung.
                if (sammlung == "dienste" && dienstVon.GetValueOrDefault(full) is { } draw)
                {
                    foreach (var (m, mraw) in Alle(k["methoden"]).Zip(draw.Methoden)) if (OrtJson(dom, mraw.Ort) is { } mo) m["_ort"] = mo;
                    foreach (var (dimpl, iraw) in Alle(k["implementierungen"]).Zip(draw.Impls))
                    {
                        weitere.Add(iraw.Full);
                        if (OrtJson(dom, iraw.Ort) is { } io) dimpl["_ort"] = io;
                        foreach (var (r, rraw) in Alle(dimpl["rumpfe"]).Zip(iraw.Rumpfe))
                            if (OrtJson(dom, rraw.Ort) is { } ro) r["_rumpf"] = ro;
                    }
                }
                // Die Implementierung eines Stores gehört zu seiner Karte (wie bei Diensten).
                if (sammlung == "stores" && storeVon.GetValueOrDefault(full) is { } st)
                {
                    if (st.Impl is { } si) weitere.Add(si.Ns + "." + si.Name);
                    // Eine Store-Funktion steht an ihrer Fähigkeit (Interface mit genau dieser Funktion, CQRS051).
                    foreach (var fn in Alle(k["writeFns"]).Concat(Alle(k["readFns"])))
                        if (st.Fns.FirstOrDefault(x => x.Faehigkeit == S(fn, "faehigkeit")) is { } fr)
                        {
                            fn["_ort"] = OrtJson(dom, TypOrt(fr.FaehigkeitFull, null));
                            if (OrtJson(dom, fr.BodyOrt) is { } ro) fn["_rumpf"] = ro;   // der Rumpf steht in der Implementierung
                        }
                }
                Teile(k, haupt, weitere);
            }
        var aggVon = dom.Aggregates.GroupBy(a => a.Name, StringComparer.Ordinal).ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        foreach (var a in Alle(root["aggregate"]))
            if (aggVon.TryGetValue(S(a, "name"), out var raw))
            {
                var haupt = TypOrt(raw.Full, (string?)a["datei"]);
                a["_ort"] = OrtJson(dom, haupt);
                Teile(a, haupt, raw.TeilTypen.Prepend(raw.Full));
            }
        foreach (var d in Alle(root["decider"]))
            if (aggVon.TryGetValue(S(d, "aggregat"), out var raw)) d["_ort"] = OrtJson(dom, raw.DecideOrte.GetValueOrDefault(S(d, "command")));
        foreach (var ap in Alle(root["applier"]))
            if (aggVon.TryGetValue(S(ap, "aggregat"), out var raw)) ap["_ort"] = OrtJson(dom, raw.ApplyOrte.GetValueOrDefault(S(ap, "event")));
    }

    /// <summary>Die Klasse einer Projektion/eines Readers verbatim (Datei, Form, Basen, Attribute, Zusatz, usings, Doku) — Board-Feld „code".</summary>
    private static object? KlassenCode(object? k) => k switch
    {
        Konsument x => new { datei = x.Datei, doku = x.Doku, typart = x.Typart, basen = x.Basen, attribute = x.Attribute, zusatz = x.Zusatz, usings = x.Usings, herkunft = x.Herkunft, zustand = x.Zustand },
        Leser x => new { datei = x.Datei, doku = x.Doku, typart = x.Typart, basen = x.Basen, attribute = x.Attribute, zusatz = x.Zusatz, usings = x.Usings, herkunft = x.Herkunft },
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

    private static object FeldJson(FieldInfo f) => new { name = f.Name, typ = f.Type, standard = f.Default, zugriff = f.Zugriff, pflicht = f.Pflicht ? true : (bool?)null, elementTyp = f.ElementTyp, geerbt = f.Geerbt ? true : (bool?)null };

    private static JsonNode Knoten(object o) => JsonSerializer.SerializeToNode(o, BoardOpts)!;

    /// <summary>
    /// Stabile Karten-Ids (Werkbank P4.8): natürliche Schlüssel statt Positionen oder Zähler — und für jeden Baustein, der in den Log schreibt,
    /// genau seine Wirker-Id (<see cref="Abstractions.WirkerId"/>). So findet ein Befund, ein Boot-Fehler oder ein Spur-Eintrag seine Karte über
    /// die Id. Reine Board-Sicht: <c>_</c>-Felder zählen nicht zum Herkunfts-Hash des Editors und werden beim Zurücklesen ignoriert.
    /// </summary>
    private static void StabileIds(JsonObject root)
    {
        IEnumerable<JsonObject> Alle(JsonNode? n) => (n as JsonArray ?? new JsonArray()).OfType<JsonObject>();
        string S(JsonObject o, string k) => (string?)o[k] ?? "";
        foreach (var d in Alle(root["decider"])) d["_id"] = Abstractions.WirkerId.FuerDecide(S(d, "aggregat"), S(d, "command"));
        foreach (var a in Alle(root["applier"])) a["_id"] = "apply:" + S(a, "aggregat") + "/" + S(a, "event");
        foreach (var f in Alle(root["bausteinFunktionen"])) f["_id"] = Abstractions.WirkerId.FuerFunktion(S(f, "name"));
        foreach (var d in Alle(root["dienste"])) d["_id"] = KartenIds.Dienst + S(d, "name");
        foreach (var a in Alle(root["akteure"])) a["_id"] = Abstractions.WirkerId.FuerAkteur(S(a, "name"));
        foreach (var c in Alle(root["clients"])) c["_id"] = "client:" + S(c, "name");
    }

    /// <summary>Stabile Id einer Store-Funktion (natürlicher Schlüssel statt Position): <c>wf:IStore.Methode</c> bzw. <c>rf:…</c>.</summary>
    private static string FnId(string store, string methode, bool lesen) => (lesen ? "rf:" : "wf:") + store + "." + methode;

    private static Feld MappeFeld(FieldInfo f) =>
        new() { Name = f.Name, Typ = f.Type, Ausdruck = f.Expr, Standard = f.Default, NurGet = f.NurGet, Zugriff = f.Zugriff, Pflicht = f.Pflicht, ElementTyp = f.ElementTyp, Geerbt = f.Geerbt };

    private static string KurzEvt(DomainModel dom, string full) =>
        dom.Events.TryGetValue(full, out var e) ? e.Simple : Kurz(full);

    private static string Kurz(string full)
    {
        var i = full.LastIndexOf('.');
        return i >= 0 ? full[(i + 1)..] : full;
    }
}
