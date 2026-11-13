using System.Text;
using Abstractions;

namespace DomainEditor;

/// <summary>Welche Rolle eine generierte Datei hat — bestimmt, wie sie in eine bestehende Datei gemischt wird.</summary>
public enum DateiArt
{
    Typen, State, Decider, Applier,
    /// <summary>Fähigkeits- und Bündel-Interfaces eines Stores (Mischen: fehlende Interfaces + fehlende Basen im Bündel).</summary>
    Schnittstellen,
    /// <summary>Methoden einer Store-Implementierung (Mischen: fehlende Methoden nach Name + Parametertypen).</summary>
    StoreImpl,
    /// <summary>Projektion bzw. Reader (Mischen: fehlende Handles nach Eingangstyp; Parameter-Abgleich gesondert).</summary>
    Konsument, Leser,
}

/// <summary>
/// Eine generierte Quelldatei: Pfad (relativ zur Solution bzw. zum Zielordner) + Inhalt + Rolle. <paramref name="Platzierbar"/>
/// = false: der Code kennt für den Namespace kein Verzeichnis — der Pfad ist nur ein Platzhalter, geschrieben wird nicht.
/// </summary>
public sealed record GenerierteDatei(string Pfad, string Inhalt, DateiArt Art = DateiArt.Typen, bool Platzierbar = true);

/// <summary>
/// Der DETERMINISTISCHE Scaffold-Generator (record-zentrisch). Aus den getaggten Records + Enums + der Aggregat-
/// Komposition erzeugt er C#. WO etwas hinkommt, entscheidet der Code, nicht der Scaffolder: jeder Typ geht in seine
/// echte Datei (<c>Datei</c>); ein neuer Typ in die Datei gleichartiger Typen seines Namespace; ein neuer Namespace in
/// das Verzeichnis/Dateimuster, das der übrige Code benutzt (<see cref="Rahmen"/>). Marker-Namen kommen über
/// <c>nameof</c>/<c>typeof</c> aus dem Vertrag.
///
/// Erzeugt bewusst NUR die handgeschriebenen Dateien — Id/Version, State-Property, Handler und Factory liefern die
/// bestehenden Roslyn-Generatoren. Decide/Apply-Körper ohne Rumpf werden ein kompilierbarer <c>throw</c>-Platzhalter.
/// </summary>
public static class Scaffolder
{
    // ── Der Vertrag, über nameof/typeof (nie als String) ──
    private static readonly string ICommand = nameof(Abstractions.ICommand);
    private static readonly string Id = typeof(Abstractions.Id<>).Name.Split('`')[0];
    private static readonly string ICreationCommand = nameof(Abstractions.ICreationCommand);
    private static readonly string IEvent = nameof(Abstractions.IEvent);
    private static readonly string ITransientEvent = nameof(Abstractions.ITransientEvent);
    private static readonly string IState = nameof(Abstractions.IState);
    private static readonly string IDecider = typeof(IDecider<>).Name.Split('`')[0];
    private static readonly string IApplier = typeof(IApplier<>).Name.Split('`')[0];
    private static readonly string OneOf = typeof(OneOf<>).Name.Split('`')[0];
    // ── Leseseite ──
    private static readonly string IQuery = nameof(Abstractions.IQuery);
    private static readonly string IQueryResponse = nameof(Abstractions.IQueryResponse);
    private static readonly string IReadModel = nameof(Abstractions.IReadModel);
    private static readonly string IGeteiltesReadModel = nameof(Abstractions.IGeteiltesReadModel);
    private static readonly string IWriteStore = nameof(Abstractions.IWriteStore);
    private static readonly string IReadStore = nameof(Abstractions.IReadStore);
    private static readonly string IStore = nameof(Abstractions.IStore);
    private static readonly string IProjektion = nameof(Abstractions.IProjektion);
    private static readonly string IReader = nameof(Abstractions.IReader);
    private static readonly string Herkunft = nameof(Abstractions.Herkunft);
    private static readonly string MindestensEinmal = nameof(MindestensEinmalAttribute)[..^"Attribute".Length];
    private static readonly string Abbruch = nameof(CancellationToken);
    // ── Akteure ──
    private static readonly string IAkteur = nameof(Abstractions.IAkteur);
    private static readonly string IDarf = typeof(IDarf<>).Name.Split('`')[0];
    /// <summary>
    /// Die Methodennamen, die die Generatoren rufen — Framework-Vertrag (<see cref="Projektionsvertrag"/>), erzwungen durch CQRS057
    /// (HandlerFormAnalyzer): ein anders benannter Eingang ist ein Build-Fehler, kein stilles Auseinanderlaufen.
    /// </summary>
    private const string AufMethode = Projektionsvertrag.Auf, FrageMethode = Projektionsvertrag.Frage;

    public static IReadOnlyList<GenerierteDatei> Generiere(EditorModell modell)
    {
        // Neue Karten in das Projekt ihrer Rolle (docs/07): Namespace = Projekt + Bereich, Datei nach der Tabelle.
        modell = Strukturort.Normalisiere(modell);
        var ergebnisse = Strukturort.Funktionsergebnisse(modell);
        var anAggregat = Strukturort.Aggregatfragen(modell);
        var dateien = new List<GenerierteDatei>();
        var rahmen = modell.Rahmen;

        // ── Records + Enums → Zieldatei (echte Datei; sonst Datei gleichartiger Typen im Namespace; sonst Muster) ──
        // Gruppe = (Datei, Namespace): eine Datei darf mehrere Namespaces tragen — jeder Namespace bleibt seiner.
        var gruppen = new Dictionary<(string Pfad, string Ns), (string Ns, List<Record> Records, List<Enumeration> Enums)>();
        (string Ns, List<Record> Records, List<Enumeration> Enums) Gruppe(string pfad, string ns)
        {
            if (!gruppen.TryGetValue((pfad, ns), out var g)) gruppen[(pfad, ns)] = g = (ns, new(), new());
            return g;
        }
        foreach (var rec in modell.Records.Where(x => RecordArt.Alle.Contains(x.Kind)))
            Gruppe(rec.Datei ?? StrukturZiel(modell, rec.Namespace, Strukturort.Primitive(rec, ergebnisse, anAggregat), rec.Name)
                   ?? TypZiel(modell, rec.Namespace, Sorte(rec.Kind), rec.Name), rec.Namespace).Records.Add(rec);
        foreach (var e in modell.Enums)
            Gruppe(e.Datei ?? TypZiel(modell, e.Namespace, "enum", e.Name), e.Namespace).Enums.Add(e);
        foreach (var ((pfad, _), g) in gruppen)
            dateien.Add(new(pfad, TypDatei(g.Ns, g.Records, g.Enums, modell), DateiArt.Typen, !pfad.StartsWith(Unplatziert, StringComparison.Ordinal)));

        // ── Akteure → ihre echte Datei; sonst die (eine) Akteur-Datei ihres Namespace; sonst Akteure.cs im Verzeichnis ──
        foreach (var g in modell.Akteure
                     .GroupBy(a => (Pfad: a.Datei ?? AkteurZiel(modell, a.Namespace), a.Namespace)))
            dateien.Add(new(g.Key.Pfad, AkteurDatei(g.Key.Namespace, g.ToList(), modell), DateiArt.Typen,
                !g.Key.Pfad.StartsWith(Unplatziert, StringComparison.Ordinal)));

        // ── Clients (docs/konzept-akteure.md §4) → ihre echte Datei; sonst die (eine) Client-Datei ihres Namespace; sonst Clients.cs ──
        foreach (var g in modell.Clients.GroupBy(c => (Pfad: c.Datei ?? ClientZiel(modell, c.Namespace), c.Namespace)))
            dateien.Add(new(g.Key.Pfad, ClientDatei(g.Key.Namespace, g.ToList(), modell), DateiArt.Typen,
                !g.Key.Pfad.StartsWith(Unplatziert, StringComparison.Ordinal)));

        // ── Apps (docs/10 E18) → ihre echte Datei; sonst die (eine) App-Datei ihres Namespace; sonst Apps.cs ──
        foreach (var g in modell.Apps.GroupBy(a => (Pfad: a.Datei ?? AppZiel(modell, a.Namespace), a.Namespace)))
            dateien.Add(new(g.Key.Pfad, AppDatei(g.Key.Namespace, g.ToList(), modell), DateiArt.Typen,
                !g.Key.Pfad.StartsWith(Unplatziert, StringComparison.Ordinal)));

        // ── Graphen (docs/04 §4.4) → ihre echte Datei; sonst die (eine) Graph-Datei ihres Namespace; sonst Graphen.cs im Verzeichnis ──
        foreach (var g in modell.Graphen.GroupBy(x => (Pfad: x.Datei ?? GraphZiel(modell, x.Namespace), x.Namespace)))
            dateien.Add(new(g.Key.Pfad, GraphDatei(g.Key.Namespace, g.ToList(), modell), DateiArt.Typen,
                !g.Key.Pfad.StartsWith(Unplatziert, StringComparison.Ordinal)));

        // ── Übersetzungen (docs/04 §4.4) → ihre echte Datei; sonst die (eine) Datei der Übersetzungen ihres Namespace; sonst
        //    Uebersetzungen.cs im Verzeichnis ──
        foreach (var g in modell.Uebersetzungen.GroupBy(x => (Pfad: x.Datei ?? UebersetzungsZiel(modell, x.Namespace), x.Namespace)))
            dateien.Add(new(g.Key.Pfad, UebersetzungsDatei(g.Key.Namespace, g.ToList(), modell), DateiArt.Typen,
                !g.Key.Pfad.StartsWith(Unplatziert, StringComparison.Ordinal)));

        // ── Aggregate → State; Decider/Applier je Aggregat aus den eigenständigen Regeln ──
        foreach (var agg in modell.Aggregate)
        {
            var decider = modell.Decider.Where(d => d.Aggregat == agg.Name).ToList();
            var applier = modell.Applier.Where(a => a.Aggregat == agg.Name).ToList();
            // Neue Aggregat-Dateien: feste Regel {Aggregat}.cs / {Aggregat}.{Decider}.cs / {Aggregat}.{Applier}.cs — keine Statistik.
            var v = Verzeichnis(modell, agg.Namespace);
            dateien.Add(Platziert(agg.Datei, v, $"{agg.Name}.cs", StateDatei(agg, modell), DateiArt.State));
            dateien.Add(Platziert(agg.DeciderDatei, v, $"{agg.Name}.{rahmen.DeciderKlasse}.cs", DeciderDatei(agg, decider, modell), DateiArt.Decider));
            dateien.Add(Platziert(agg.ApplierDatei, v, $"{agg.Name}.{rahmen.ApplierKlasse}.cs", ApplierDatei(agg, applier, modell), DateiArt.Applier));
        }

        // ── Funktionen (IFunktion, docs/04 §4.4) → je NEUE Funktion ihre Klasse (im Host, throw-Stubs; eigene Datei) bzw. ihre Schnittstelle
        //    (draußen oder mit Umsetzung; auch sie in eigener Datei, wie die Lage es will: Technik je Typ eine Datei) — mit Eingängen (Auf) oder gerufen (Methoden); je NEUE Umsetzung einer
        //    Schnittstelle ihre Klasse mit throw-Stubs. Eine bestehende gehört dem Code, ihre Signaturen gleicht der Abgleich ab ──
        foreach (var f in modell.BausteinFunktionen)
        {
            if (f.Datei == null)
                dateien.Add(Platziert(null, Verzeichnis(modell, f.Namespace), $"{f.Name}.cs", FunktionsDatei(f, modell), DateiArt.Typen));
            foreach (var u in f.Umsetzungen.Where(u => u.Datei == null && !f.Klasse))
                dateien.Add(Platziert(null, Verzeichnis(modell, f.Namespace), $"{u.Name}.cs", UmsetzungsDatei(f, u, modell), DateiArt.Typen));
        }

        // ── Quellarten (IQuellart, docs/04 §4.7) → je NEUE ihre Klasse (eigene Datei im Katalog, throw-Stub von Lausche); eine bestehende gehört dem
        //    Code, eine des Frameworks schreibt der Editor nie ──
        foreach (var q in modell.Quellarten.Where(q => q.Datei == null && !q.Framework))
            dateien.Add(Platziert(null, Verzeichnis(modell, q.Namespace), $"{q.Name}.cs", QuellartDatei(q, modell), DateiArt.Typen));

        if (modell.Lesen is { } lesen) dateien.AddRange(LeseseitenDateien(modell, lesen));

        return dateien.OrderBy(d => d.Pfad, StringComparer.Ordinal).ToList();
    }

    // ── Wohin? — aus dem Code abgeleitet ──────────────────────────────────────────────────────
    private static string Sorte(string kind) => kind == RecordArt.Rejection ? RecordArt.Event : kind;

    /// <summary>Pfad-Präfix für Typen, deren Namespace der Code keinem Verzeichnis zuordnet (nie geschrieben).</summary>
    private const string Unplatziert = "?/";

    private static GenerierteDatei Platziert(string? echt, string? verzeichnis, string name, string inhalt, DateiArt art) =>
        echt != null ? new(echt, inhalt, art)
        : verzeichnis != null ? new($"{verzeichnis}/{name}", inhalt, art)
        : new($"{Unplatziert}{name}", inhalt, art, Platzierbar: false);

    /// <summary>
    /// Zieldatei eines NEUEN Typs in einem Rollen-Projekt: Verzeichnis seines Namespace + Dateiname der Tabelle (docs/07 §7.4,
    /// <see cref="Abstractions.Projektstruktur.Datei"/>). <c>null</c> = Namespace außerhalb der Struktur oder Art ohne Tabellen-Eintrag.
    /// </summary>
    private static string? StrukturZiel(EditorModell m, string ns, Abstractions.Projektstruktur.Primitive? p, string typName) =>
        p is { } prim && Strukturort.InStruktur(ns) && Verzeichnis(m, ns) is { } v ? $"{v}/{Abstractions.Projektstruktur.Datei(prim, typName)}" : null;

    /// <summary>
    /// Zieldatei eines NEUEN Typs (ohne eigene Datei) außerhalb der Rollen-Projekte — feste Regel, keine Mehrheits-Statistik: stehen ALLE gleichartigen
    /// Typen seines Namespace in genau EINER Datei, dorthin (das ist ein Fakt des Codes); sonst die kanonische Datei der
    /// Sorte (<c>Commands.cs</c> …) bzw. bei schon verteilten Typen eine eigene. Kein Verzeichnis bekannt: unplatziert.
    /// </summary>
    private static string TypZiel(EditorModell m, string ns, string sorte, string typName)
    {
        IEnumerable<(string Ns, string? Datei)> gleichartig = sorte == "enum"
            ? m.Enums.Select(e => (e.Namespace, e.Datei))
            : m.Records.Where(r => Sorte(r.Kind) == sorte).Select(r => (r.Namespace, r.Datei));
        var dateienImNs = gleichartig.Where(x => x.Ns == ns && x.Datei != null).Select(x => x.Datei!).Distinct(StringComparer.Ordinal).ToList();
        if (dateienImNs.Count == 1) return dateienImNs[0];
        // Sonst die kanonische Form des Scaffolders (feste Ausgabe-Regel für NEUEN Code, keine Schätzung):
        //   je Sorte eine Datei; verteilt der Code die Sorte schon auf mehrere Dateien, bekommt der neue Typ eine eigene.
        var name = dateienImNs.Count > 1 ? $"{typName}.cs" : sorte switch
        {
            RecordArt.Command => "Commands.cs",
            RecordArt.Event => "Events.cs",
            RecordArt.ValueObject => "ValueObjects.cs",
            RecordArt.Query => "Queries.cs",
            RecordArt.Antwort => "Responses.cs",
            RecordArt.ReadModel => "ReadModels.cs",
            RecordArt.Konfig => "Konfigs.cs",
            RecordArt.Meldung => "Meldungen.cs",
            _ => "Enums.cs",
        };
        var v = Verzeichnis(m, ns);
        return v != null ? $"{v}/{name}" : $"{Unplatziert}{name}";
    }

    /// <summary>
    /// Verzeichnis eines Namespace (relativ zur Solution): bekannt aus dem Code (alle Typen des Namespace in genau einem
    /// Verzeichnis); sonst über den längsten Namespace, dessen Verzeichnis bekannt ist (auch die Projekt-Wurzel-Namespaces
    /// aus den csproj) plus die Rest-Segmente als Unterordner — die Namespace↔Ordner-Abbildung von MSBuild.
    /// Kein bekannter Präfix: null (nicht platzierbar).
    /// </summary>
    private static string? Verzeichnis(EditorModell m, string ns)
    {
        var v = m.Rahmen.Verzeichnisse;
        if (v.TryGetValue(ns, out var d)) return d;
        var teile = ns.Split('.');
        for (var n = teile.Length - 1; n > 0; n--)
        {
            var präfix = string.Join('.', teile[..n]);
            if (v.TryGetValue(präfix, out var basis)) return basis + "/" + string.Join('/', teile[n..]);
        }
        return null;
    }

    // ── Akteure ───────────────────────────────────────────────────────────────────────────────
    private static string AkteurZiel(EditorModell m, string ns)
    {
        var dateien = m.Akteure.Where(a => a.Namespace == ns && a.Datei != null).Select(a => a.Datei!).Distinct(StringComparer.Ordinal).ToList();
        if (dateien.Count == 1) return dateien[0];
        var v = Verzeichnis(m, ns);
        return v != null ? $"{v}/Akteure.cs" : $"{Unplatziert}Akteure.cs";
    }

    /// <summary>Die Basisliste eines Akteurs: <c>IAkteur, IDarf&lt;A&gt;…, IHoert&lt;B&gt;…</c> — der ganze Akteur steht in ihr.</summary>
    public static IReadOnlyList<string> AkteurBasen(Akteur a) => [IAkteur, .. a.Darf.Select(d => $"{IDarf}<{d}>"), .. a.Hoert.Select(h => $"{IHoert}<{h}>")];

    /// <summary>Gehört dieser Basistyp zum Akteur (<c>IAkteur</c>, <c>IDarf&lt;…&gt;</c>, <c>IHoert&lt;…&gt;</c>)? Alles andere bleibt beim Abgleich stehen.</summary>
    public static bool IstAkteurBasis(string basis)
    {
        var name = basis.Split('<')[0].Split('.').Last().Trim();
        return name == IAkteur || name == IDarf || name == IHoert;
    }

    // ── Clients ─────────────────────────────────────────────────────────────────────────────────
    private static readonly string IClient = nameof(Abstractions.IClient);
    private static readonly string IVerkoerpert = typeof(IVerkoerpert<>).Name.Split('`')[0];

    private static string ClientZiel(EditorModell m, string ns)
    {
        var dateien = m.Clients.Where(c => c.Namespace == ns && c.Datei != null).Select(c => c.Datei!).Distinct(StringComparer.Ordinal).ToList();
        if (dateien.Count == 1) return dateien[0];
        var v = Verzeichnis(m, ns);
        return v != null ? $"{v}/Clients.cs" : $"{Unplatziert}Clients.cs";
    }

    private static readonly string IHoert = typeof(IHoert<>).Name.Split('`')[0];

    /// <summary>Die Basisliste eines Clients: <c>IClient, IVerkoerpert&lt;A&gt;…</c> — der ganze Client steht in ihr (docs/konzept-akteure.md §4).</summary>
    public static IReadOnlyList<string> ClientBasen(Client c) => [IClient, .. c.Verkoerpert.Select(a => $"{IVerkoerpert}<{a}>")];

    /// <summary>Gehört dieser Basistyp zum Client (wird vom Abgleich verwaltet)?</summary>
    public static bool IstClientBasis(string basis)
    {
        var name = basis.Split('<')[0].Split('.').Last().Trim();
        return name == IClient || name == IVerkoerpert;
    }

    /// <summary>Ein Client: <c>public sealed record X : IClient, IVerkoerpert&lt;A&gt;, …;</c> — ohne eigene Member.</summary>
    public static string ClientInterface(Client c)
    {
        var b = new StringBuilder();
        b.AppendLine($"public sealed record {c.Name} : {string.Join(",\n    ", ClientBasen(c))};");
        return b.ToString();
    }

    private static string ClientDatei(string ns, List<Client> clients, EditorModell modell)
    {
        // usings: die verkörperten Akteure.
        var b = Kopf(ns, Usings(ns, modell, [modell.Rahmen.VertragsNamespace], clients.SelectMany(c => c.Verkoerpert), []));
        for (var i = 0; i < clients.Count; i++)
        {
            Doku(b, clients[i].Doku, "");
            b.Append(ClientInterface(clients[i]));
            if (i < clients.Count - 1) b.AppendLine();
        }
        return b.ToString();
    }

    // ── Apps ────────────────────────────────────────────────────────────────────────────────────
    private static string AppZiel(EditorModell m, string ns)
    {
        var dateien = m.Apps.Where(a => a.Namespace == ns && a.Datei != null).Select(a => a.Datei!).Distinct(StringComparer.Ordinal).ToList();
        if (dateien.Count == 1) return dateien[0];
        var v = Verzeichnis(m, ns);
        var name = Abstractions.Projektstruktur.Sammeldatei(Abstractions.Projektstruktur.Primitive.App)!;
        return v != null ? $"{v}/{name}" : $"{Unplatziert}{name}";
    }

    /// <summary>Die Parameter einer App: je Quelle <c>[Uebersetzt&lt;U&gt;] Quellart Name</c> (Reihenfolge der Quellen).</summary>
    public static string AppParameter(App a) => string.Join(",\n    ", a.Quellen.Select(q =>
        string.Concat(q.Uebersetzungen.Select(u => $"[Uebersetzt<{u}>] ")) + q.Quellart + " " + q.Name));

    /// <summary>Eine App: <c>public sealed record X(…Quellen…) : IApp&lt;Client&gt;;</c> — ohne eigene Member.</summary>
    public static string AppRecord(App a) => a.Quellen.Count == 0
        ? $"public sealed record {a.Name} : IApp<{a.Client}>;\n"
        : $"public sealed record {a.Name}(\n    {AppParameter(a)}) : IApp<{a.Client}>;\n";

    private static string AppDatei(string ns, List<App> apps, EditorModell modell)
    {
        // usings: der Client (und über die Übersetzungen ihre Namespaces), die Quellarten (Usings der App, z. B. Core.Katalog).
        var b = Kopf(ns, Usings(ns, modell, [modell.Rahmen.VertragsNamespace], apps.Select(a => a.Client).Concat(apps.SelectMany(a => a.Quellen.SelectMany(q => q.Uebersetzungen))),
            apps.SelectMany(a => a.Usings)));
        for (var i = 0; i < apps.Count; i++)
        {
            Doku(b, apps[i].Doku, "");
            b.Append(AppRecord(apps[i]));
            if (i < apps.Count - 1) b.AppendLine();
        }
        return b.ToString();
    }

    // ── Graphen ─────────────────────────────────────────────────────────────────────────────────
    private static readonly string IGraph = nameof(Abstractions.IGraph);

    private static string UebersetzungsZiel(EditorModell m, string ns)
    {
        var dateien = m.Uebersetzungen.Where(u => u.Namespace == ns && u.Datei != null).Select(u => u.Datei!).Distinct(StringComparer.Ordinal).ToList();
        if (dateien.Count == 1) return dateien[0];
        var v = Verzeichnis(m, ns);
        var name = Abstractions.Projektstruktur.Sammeldatei(Abstractions.Projektstruktur.Primitive.Uebersetzung)!;
        return v != null ? $"{v}/{name}" : $"{Unplatziert}{name}";
    }

    private static string GraphZiel(EditorModell m, string ns)
    {
        var dateien = m.Graphen.Where(g => g.Namespace == ns && g.Datei != null).Select(g => g.Datei!).Distinct(StringComparer.Ordinal).ToList();
        if (dateien.Count == 1) return dateien[0];
        var v = Verzeichnis(m, ns);
        return v != null ? $"{v}/Graphen.cs" : $"{Unplatziert}Graphen.cs";
    }

    /// <summary>
    /// Ein Graph: <c>public sealed record X([Kopf] T1 A, [Von(nameof(A))] T2 B) : IGraph;</c> — die Knoten als Parameter, jede Kante als <c>Von</c> am
    /// nehmenden Knoten. Passt er nicht in eine Zeile, steht je Knoten eine.
    /// </summary>
    public static string GraphRecord(Graph g, Func<string, Graph?>? graphNamens = null)
    {
        var knoten = OhneLoseKanten(g).Knoten.Select(k => GraphKnotenText(k, g, graphNamens)).ToList();
        var zeile = $"public sealed record {g.Name}({string.Join(", ", knoten)}) : {IGraph};";
        return zeile.Length <= 150 ? zeile : $"public sealed record {g.Name}(\n    {string.Join(",\n    ", knoten)}) : {IGraph};";
    }

    /// <summary>Der Graph ohne die <c>Von</c> auf Knoten, die er nicht (mehr) hat — ein entfernter Knoten nimmt seine Kanten mit.</summary>
    public static Graph OhneLoseKanten(Graph g) => g with
    {
        Knoten = g.Knoten.Select(k => k with
        {
            Von = k.Von.Where(v => g.Knoten.Any(x => x.Name == v)).ToList(),
            Verlierbar = k.Verlierbar.Where(v => k.Von.Contains(v) && g.Knoten.Any(x => x.Name == v)).ToList(),
        }).ToList(),
    };

    /// <summary>
    /// Ein Knoten als Parameter: <c>[Kopf] [Von(nameof(A), nameof(B))] [Von(nameof(C), Verlierbar = true)] Typ Name</c> — gesicherte Quellen in einem
    /// <c>[Von]</c>, verlierbare in einem eigenen.
    /// </summary>
    public static string GraphKnotenText(GraphKnoten k, Graph? g = null, Func<string, Graph?>? graphNamens = null)
    {
        string Von(IEnumerable<string> namen, bool verlierbar) =>
            $"[{VonAttribut}({string.Join(", ", namen.Select(v => $"nameof({v})"))}{(verlierbar ? ", Verlierbar = true" : "")})] ";
        var gesichert = k.Von.Where(v => !k.Verlierbar.Contains(v)).ToList();
        var verlierbar = k.Von.Where(v => k.Verlierbar.Contains(v)).ToList();
        return (k.Kopf ? $"[{KopfAttribut}] " : "") + (gesichert.Count > 0 ? Von(gesichert, false) : "") + (verlierbar.Count > 0 ? Von(verlierbar, true) : "")
            + $"{k.Typ} {k.Name}";
    }

    /// <summary>
    /// Eine ÜBERSETZUNG (docs/04 §4.4) als Klasse: der Konstruktor nimmt die Quelle, <c>Translate()</c> baut das Ziel aus der Verdrahtung im Editor —
    /// je zugeordnetem Parameter <c>Ziel: source.Feld</c>, <c>Guid</c> → <c>Id&lt;T&gt;</c> als <c>new(source.Feld)</c>, <c>Id&lt;T&gt;</c> → <c>Guid</c> als
    /// <c>source.Feld.Wert</c>. Was nicht zugeordnet ist, fehlt im Aufruf: der Compiler meldet es (Unfertiges ist Code, der nicht baut, W1).
    /// <code>public sealed class Ein_Aus_1(Ein source) : ITranslate&lt;Ein, Aus&gt;
    /// {
    ///     public Aus Translate() => new(AggregateId: new(source.Paar), …);
    /// }</code>
    /// </summary>
    public static string UebersetzungKlasse(Uebersetzung u, EditorModell m)
    {
        var quellen = UebersetzungQuellen(u, m);
        var ziele = UebersetzungZiele(u, m);
        var typVon = quellen.ToDictionary(q => q.Name, q => q.Typ, StringComparer.Ordinal);
        string Ausdruck(FeldZuordnung f)
        {
            var nach = ziele.FirstOrDefault(z => z.Name == f.Ziel).Typ ?? "";
            var von = typVon.GetValueOrDefault(f.Quelle) ?? "";
            var feld = $"{QuellParameter}.{f.Quelle}";
            return IstIdTyp(nach) && IstGuidTyp(von) ? $"new({feld})" : IstGuidTyp(nach) && IstIdTyp(von) ? $"{feld}.Wert" : feld;
        }
        var reihe = ziele.Select(z => z.Name).ToList();
        var argumente = u.Felder.Where(f => typVon.ContainsKey(f.Quelle))
            .OrderBy(f => reihe.IndexOf(f.Ziel) is var i && i >= 0 ? i : int.MaxValue).Select(f => $"{f.Ziel}: {Ausdruck(f)}");
        var marker = nameof(Abstractions.ITranslate<object, object>);
        var liste = argumente.ToList();
        var zeile = $"    public {u.Aus} Translate() => new({string.Join(", ", liste)});";
        // Passt der Aufruf nicht in eine Zeile, steht je Feld eine (wie die Knoten eines Graphen).
        if (zeile.Length > 150) zeile = $"    public {u.Aus} Translate() => new(\n" + string.Join(",\n", liste.Select(x => "        " + x)) + ");";
        return $"public sealed class {u.Name}({u.Ein} {QuellParameter}) : {marker}<{u.Ein}, {u.Aus}>\n{{\n{zeile}\n}}";
    }

    /// <summary>Der Name des Konstruktor-Parameters einer Übersetzung: die Quelle.</summary>
    public const string QuellParameter = "source";

    /// <summary>Die Felder der Quelle (Name, Typ): wie der Code sie liest, sonst aus dem Record im Modell.</summary>
    internal static IReadOnlyList<(string Name, string Typ)> UebersetzungQuellen(Uebersetzung u, EditorModell m) =>
        u.Quellen.Count > 0 ? u.Quellen.Select(q => (q.Name, q.Typ)).ToList()
            : m.Records.FirstOrDefault(r => r.Name == Basis(u.Ein))?.Felder.Select(f => (f.Name, f.Typ)).ToList() ?? [];

    /// <summary>Die Parameter des Ziels (Name, Typ) — das Ziel-Feld eines Commands an ein Aggregat ist dessen <c>Id&lt;T&gt;</c>.</summary>
    internal static IReadOnlyList<(string Name, string Typ)> UebersetzungZiele(Uebersetzung u, EditorModell m)
    {
        if (u.Ziele.Count > 0) return u.Ziele.Select(z => (z.Name, z.Typ)).ToList();
        var r = m.Records.FirstOrDefault(x => x.Name == Basis(u.Aus));
        return r?.Felder.Select(f => (f.Name, f.Name == m.Rahmen.AggregatIdFeld && !string.IsNullOrEmpty(r.Aggregat) && IstGuidTyp(f.Typ) ? $"{Id}<{r.Aggregat}>" : f.Typ))
            .ToList() ?? [];
    }

    private static bool IstGuidTyp(string t) => t is "Guid" or "System.Guid";
    private static bool IstIdTyp(string t) => t.StartsWith(Id + "<", StringComparison.Ordinal) || t.Contains("." + Id + "<");

    private static string Basis(string typ) => typ.IndexOf('<') is > 0 and var i ? typ[..i] : typ;

    /// <summary>Die Attribute eines Graph-Knotens, wie man sie schreibt (<c>[Kopf]</c>, <c>[Von(…)]</c>).</summary>
    public static readonly string KopfAttribut = nameof(Abstractions.KopfAttribute)[..^"Attribute".Length];
    public static readonly string VonAttribut = nameof(Abstractions.VonAttribute)[..^"Attribute".Length];

    private static string GraphDatei(string ns, List<Graph> graphen, EditorModell modell)
    {
        // usings: die Typen der Knoten (Funktionen, Graphen; bei generischen auch die Typargumente).
        var b = Kopf(ns, Usings(ns, modell, [modell.Rahmen.VertragsNamespace],
            graphen.SelectMany(g => g.Knoten.SelectMany(k => k.Typ.Split('<', '>', ',').Select(x => x.Trim()).Where(x => x.Length > 0))),
            graphen.SelectMany(g => g.Usings)));
        for (var i = 0; i < graphen.Count; i++)
        {
            Doku(b, graphen[i].Doku, "");
            b.AppendLine(GraphRecord(graphen[i], n => modell.Graphen.FirstOrDefault(x => x.Name == n)));
            if (i < graphen.Count - 1) b.AppendLine();
        }
        return b.ToString();
    }

    private static string UebersetzungsDatei(string ns, List<Uebersetzung> uebersetzungen, EditorModell modell)
    {
        // usings: Quelle und Ziel.
        var b = Kopf(ns, Usings(ns, modell, [modell.Rahmen.VertragsNamespace],
            uebersetzungen.SelectMany(u => new[] { u.Ein, u.Aus }),
            uebersetzungen.SelectMany(u => u.Usings)));
        // Der Konstruktor nimmt die Quelle — eine Übersetzung, deren Ziel nur Standardwerte hat, liest sie nicht (CS9113 ist dann kein Befund).
        b.AppendLine("#pragma warning disable CS9113");
        b.AppendLine();
        for (var i = 0; i < uebersetzungen.Count; i++)
        {
            Doku(b, uebersetzungen[i].Doku, "");
            b.AppendLine(UebersetzungKlasse(uebersetzungen[i], modell));
            if (i < uebersetzungen.Count - 1) b.AppendLine();
        }
        return b.ToString();
    }

    private static string AkteurDatei(string ns, List<Akteur> akteure, EditorModell modell)
    {
        var b = Kopf(ns, Usings(ns, modell, [modell.Rahmen.VertragsNamespace], akteure.SelectMany(a => a.Darf.Concat(a.Hoert)), []));
        for (var i = 0; i < akteure.Count; i++)
        {
            Doku(b, akteure[i].Doku, "");
            b.AppendLine($"public sealed record {akteure[i].Name} : {string.Join(", ", AkteurBasen(akteure[i]))};");
            if (i < akteure.Count - 1) b.AppendLine();
        }
        return b.ToString();
    }

    // ── Typ-Dateien aus Records/Enums ─────────────────────────────────────────────────────────
    private static string TypDatei(string ns, List<Record> records, List<Enumeration> enums, EditorModell modell)
    {
        var basis = records.Any(r => r.Kind != RecordArt.ValueObject) ? new[] { modell.Rahmen.VertragsNamespace } : Array.Empty<string>();
        var b = Kopf(ns, Usings(ns, modell, basis, records.SelectMany(RecordTypen), records.SelectMany(r => r.Usings)));
        var abschnitte = new List<Action>();

        if (enums.Count > 0) abschnitte.Add(() =>
        {
            for (var i = 0; i < enums.Count; i++)
            {
                Doku(b, enums[i].Doku, "");
                b.AppendLine($"public enum {enums[i].Name} {{ {string.Join(", ", enums[i].Werte)} }}");
                if (i < enums.Count - 1) b.AppendLine();
            }
        });
        // Commands: das Ziel steht im Typ (ICommand<TAggregat>, docs/10 §10.4) — das Aggregat aus dem Code bzw. dem Decide, das ihn entscheidet. Ohne
        //   Aggregat bleibt er unfertig (ICommand ohne Ziel baut nicht, W1); das Ziel-Feld des Editors (Guid) wird die Id des Aggregats.
        var commands = records.Where(r => r.Kind == RecordArt.Command).Select(r => MitZiel(r, modell)).ToList();
        if (commands.Count > 0) abschnitte.Add(() =>
        {
            for (var i = 0; i < commands.Count; i++)
            {
                var ziel = commands[i].Aggregat is { Length: > 0 } a ? $"{ICommand}<{a}>" : ICommand;
                RecordZeilen(b, commands[i], commands[i].IstErzeugung ? $"{ziel}, {ICreationCommand}" : ziel, i < commands.Count - 1);
            }
        });
        var persistent = records.Where(r => r.Kind == RecordArt.Event).ToList();
        var ablehnungen = records.Where(r => r.Kind == RecordArt.Rejection).ToList();
        if (persistent.Count + ablehnungen.Count > 0) abschnitte.Add(() =>
        {
            for (var i = 0; i < persistent.Count; i++)
                RecordZeilen(b, persistent[i], IEvent, i < persistent.Count - 1);
            if (ablehnungen.Count > 0)
            {
                if (persistent.Count > 0) b.AppendLine();
                for (var i = 0; i < ablehnungen.Count; i++)
                    RecordZeilen(b, ablehnungen[i], ITransientEvent, i < ablehnungen.Count - 1);
            }
        });
        var vos = records.Where(r => r.Kind == RecordArt.ValueObject).ToList();
        if (vos.Count > 0) abschnitte.Add(() =>
        {
            for (var i = 0; i < vos.Count; i++)
                RecordZeilen(b, vos[i], null, i < vos.Count - 1);
        });
        // Leseseite: Queries, Responses, ReadModels — je Art ein Abschnitt mit ihrem Marker.
        foreach (var (art, marker) in new (string, string?)[] { (RecordArt.Query, IQuery), (RecordArt.Antwort, IQueryResponse), (RecordArt.ReadModel, IReadModel),
                     (RecordArt.Konfig, null), (RecordArt.Meldung, IMeldung) })
        {
            var liste = records.Where(r => r.Kind == art).ToList();
            if (liste.Count > 0) abschnitte.Add(() =>
            {
                for (var i = 0; i < liste.Count; i++)
                    RecordZeilen(b, art == RecordArt.Meldung ? MitKennung(liste[i]) : liste[i],
                        liste[i].Geteilt && art == RecordArt.ReadModel ? $"{marker}, {IGeteiltesReadModel}" : marker, i < liste.Count - 1);
            });
        }

        for (var i = 0; i < abschnitte.Count; i++)
        {
            if (i > 0) b.AppendLine();
            abschnitte[i]();
        }
        return b.ToString();
    }

    // ── Meldung (IMeldung) und Quellart (IQuellart, docs/04 §4.7) ─────────────────────────────────────────────────────────────
    private static readonly string IMeldung = nameof(Abstractions.IMeldung);
    private static readonly string IQuellart = nameof(Abstractions.IQuellart);
    private static readonly string IAnnahme = typeof(Abstractions.IAnnahme<>).Name.Split('`')[0];
    private static readonly string Kennung = nameof(Abstractions.IMeldung.Kennung);

    /// <summary>
    /// Eine NEUE Meldung ohne Kennung (kein Feld <c>Kennung</c>, kein Handcode): die Kennung ihres Elements kommt aus seinem Inhalt — als throw-Stub, bis
    /// der Modellierer sie schreibt. Eine bestehende behält ihren Handcode (Zusatz).
    /// </summary>
    private static Record MitKennung(Record r) =>
        r.Zusatz != null || r.Felder.Any(f => f.Name == Kennung) ? r
            : r with { Zusatz = $"string {IMeldung}.{Kennung} => throw new NotImplementedException(\"TODO: {r.Name}.{Kennung} — die Kennung des Elements aus seinem Inhalt\");" };

    /// <summary>
    /// Eine NEUE Quellart: <c>public sealed class X(Konstruktor) : IQuellart</c> mit einem throw-Stub von <c>Lausche</c> — nie <c>IFunktion</c>, nie ein Knoten.
    /// Was sie meldet, steht in der Signatur (ein asynchroner Strom ihrer Meldungen).
    /// </summary>
    private static string QuellartDatei(Quellart q, EditorModell modell)
    {
        var b = Kopf(q.Namespace, Usings(q.Namespace, modell, [modell.Rahmen.VertragsNamespace], q.Meldet.Concat(q.Konstruktor.Select(p => p.Typ)), []));
        Doku(b, q.Doku, "");
        var ctor = q.Konstruktor.Count > 0 ? $"({ParameterListe(q.Konstruktor)})" : "";
        // Eine Annahme lauscht nicht: was sie meldet, steht im Typargument, ihre Meldungen nimmt der Host an.
        if (q.Annahme)
        {
            b.AppendLine($"public sealed class {q.Name}{ctor} : {IAnnahme}<{q.Meldet.FirstOrDefault() ?? nameof(Abstractions.Aufruf)}>;");
            return b.ToString();
        }
        b.AppendLine($"public sealed class {q.Name}{ctor} : {IQuellart}");
        b.AppendLine("{");
        b.Append(LauscheStub(q, "    "));
        b.AppendLine("}");
        return b.ToString();
    }

    // ── Funktion (IFunktion): je Eingang ein Auf — die Ausgaben stehen in der Signatur (CQRS050), nie im Rumpf ───────────────
    private static readonly string IFunktion = nameof(Abstractions.IFunktion);
    private static readonly string OneOfName = typeof(Abstractions.OneOf<>).Name.Split('`')[0];

    /// <summary>
    /// Die Rückgabe eines Auf aus seinen Ausgaben und seiner Form (Entscheidung 4: kein <c>T?</c>, ein leerer Strom ist „nichts“): eine → <c>X</c>;
    /// mehrere → eine Wahl <c>OneOf&lt;…&gt;</c>; als Strom <c>IEnumerable&lt;…&gt;</c>; asynchron in <c>Task&lt;…&gt;</c> bzw. als
    /// <c>IAsyncEnumerable&lt;…&gt;</c>; keine → <c>void</c> bzw. <c>Task</c> (baut nicht — W1: Unfertiges ist Code, der nicht baut).
    /// </summary>
    public static string FunktionsRueckgabe(BausteinEingang e) => Rueckgabe(e.Ausgaben, e.Strom, e.Asynchron);

    private static string Rueckgabe(IReadOnlyList<string> ausgaben, bool strom, bool asynchron)
    {
        var innen = ausgaben.Count switch { 0 => null, 1 => ausgaben[0], _ => $"{OneOfName}<{string.Join(", ", ausgaben)}>" };
        if (innen == null) return asynchron ? "Task" : "void";
        if (strom) return asynchron ? $"IAsyncEnumerable<{innen}>" : $"IEnumerable<{innen}>";
        return asynchron ? $"Task<{innen}>" : innen;
    }

    /// <summary>
    /// Die Parameter eines Auf: die eine Nachricht (GR-EINGANG), die Herkunft (Ids über <c>h.NeueId</c>), dann die Lese-Fähigkeiten.
    /// </summary>
    public static IReadOnlyList<Parameter> FunktionsParameter(BausteinEingang e) =>
        [new Parameter { Typ = e.Eingang, Name = "e" },
         .. (e.Herkunft ? [new Parameter { Typ = nameof(Abstractions.Herkunft), Name = "h" }] : Array.Empty<Parameter>()),
         .. e.Faehigkeiten];

    /// <summary>Eine Quellart lauscht so (docs/04 §4.7): ein asynchroner Strom ihrer Meldungen, mit dem Abbruch als einzigem Parameter.</summary>
    public static string LauscheKopf(Quellart q) =>
        $"async {Rueckgabe(q.Meldet, strom: true, asynchron: true)} {Abstractions.Funktionsvertrag.Lausche}([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)";

    private static string LauscheStub(Quellart q, string einzug) =>
        $"{einzug}public {LauscheKopf(q)}\n{einzug}{{\n{einzug}    await Task.CompletedTask;\n{einzug}    throw new NotImplementedException(\"TODO: {q.Name}.Lausche\");\n"
        + $"{einzug}    yield break;\n{einzug}}}\n";

    public static string FunktionsKopf(BausteinEingang e) =>
        $"{FunktionsRueckgabe(e)} {Abstractions.Funktionsvertrag.Auf}({ParameterListe(FunktionsParameter(e))})";

    /// <summary>Ein Auf als throw-Stub in einer Klasse (Funktion im Host bzw. Umsetzung).</summary>
    public static string FunktionsStub(string klasse, BausteinEingang e, string einzug) =>
        $"{einzug}public {FunktionsKopf(e)}\n{einzug}{{\n{einzug}    throw new NotImplementedException(\"TODO: {klasse}.Auf({e.Eingang})\");\n{einzug}}}\n";

    /// <summary>Die Typen, die eine Funktion nennt (Eingänge, Ausgaben, Lese-Fähigkeiten, gerufene Methoden) — für ihre usings.</summary>
    private static IEnumerable<string> FunktionsTypen(BausteinFunktion f) =>
        f.Eingaenge.SelectMany(e => FunktionsParameter(e).Select(p => p.Typ).Concat(e.Ausgaben))
            .Concat(f.Methoden.SelectMany(m => m.Parameter.Select(p => p.Typ).Append(m.Rueckgabe)));

    private static string FunktionsDatei(BausteinFunktion f, EditorModell modell)
    {
        var b = Kopf(f.Namespace, Usings(f.Namespace, modell, [modell.Rahmen.VertragsNamespace], FunktionsTypen(f).Concat(f.Konstruktor.Select(p => p.Typ)), []));
        Doku(b, f.Doku, "");
        // Technik (Methoden, kein Eingang) trägt keinen Marker — sie ist, was der Konstruktor einer Funktion nimmt (docs/10 §10.3).
        var basis = f.Methoden.Count > 0 && f.Eingaenge.Count == 0 ? "" : $" : {IFunktion}";
        if (f.Klasse)
        {
            var ctor = f.Konstruktor.Count > 0 ? $"({ParameterListe(f.Konstruktor)})" : "";
            b.AppendLine($"public sealed class {f.Name}{ctor}{basis}");
            b.AppendLine("{");
            b.Append(Stubs(f, f.Name));
            b.AppendLine("}");
        }
        else
        {
            b.AppendLine($"public interface {f.Name}{basis}");
            b.AppendLine("{");
            foreach (var e in f.Eingaenge) b.AppendLine($"    {FunktionsKopf(e)};");
            foreach (var m in f.Methoden)
            {
                Doku(b, m.Doku, "    ");
                b.AppendLine($"    {MethodenKopf(m)};");
            }
            b.AppendLine("}");
        }
        return b.ToString();
    }

    /// <summary>Die throw-Stubs einer Klasse: je Eingang ein Auf, je gerufene Methode die Methode.</summary>
    private static string Stubs(BausteinFunktion f, string klasse)
    {
        var teile = f.Eingaenge.Select(e => FunktionsStub(klasse, e, "    ")).Concat(f.Methoden.Select(m => MethodenStub(klasse, m, "    ")));
        return string.Join("\n", teile);
    }

    /// <summary><c>public sealed class X(Konstruktor) : IF { … Stubs … }</c> — eine neue Umsetzung einer Funktions-Schnittstelle.</summary>
    private static string UmsetzungsDatei(BausteinFunktion f, FunktionsUmsetzung u, EditorModell modell)
    {
        var b = Kopf(f.Namespace, Usings(f.Namespace, modell, [modell.Rahmen.VertragsNamespace], FunktionsTypen(f).Concat(u.Konstruktor.Select(p => p.Typ)), []));
        b.AppendLine($"public sealed class {u.Name}{(u.Konstruktor.Count == 0 ? "" : $"({ParameterListe(u.Konstruktor)})")} : {f.Name}");
        b.AppendLine("{");
        b.Append(Stubs(f, u.Name));
        b.AppendLine("}");
        return b.ToString();
    }

    /// <summary><c>R M(P p)</c> — Rückgabe und Parameter einer gerufenen Methode wie im Modell.</summary>
    public static string MethodenKopf(FunktionsMethode m) => $"{m.Rueckgabe} {m.Name}({ParameterListe(m.Parameter)})";

    /// <summary>Eine gerufene Methode als Stub: <c>public R M(P p) { throw … }</c> (ohne async — passt für jeden Rückgabetyp).</summary>
    public static string MethodenStub(string klasse, FunktionsMethode m, string einzug)
        => $"{einzug}public {MethodenKopf(m)}\n{einzug}{{\n{einzug}    throw new NotImplementedException(\"TODO: {klasse}.{m.Name}\");\n{einzug}}}\n";

    /// <summary>
    /// Ein Record in SEINER deklarierten Form: Doku, Attribute, <c>{Typart} X(Positions-Felder) : Marker, Basen</c>, im Rumpf
    /// die Property-Felder (mit ihrem Accessor-Satz) und der Handcode (Zusatz). Ohne Rumpf-Inhalt: <c>…;</c>.
    /// </summary>
    private static void RecordZeilen(StringBuilder b, Record r, string? marker, bool leerzeileDanach)
    {
        Doku(b, r.Doku, "");
        if (!string.IsNullOrWhiteSpace(r.Attribute))
            foreach (var zeile in r.Attribute.Replace("\r\n", "\n").Split('\n')) b.AppendLine(zeile);
        var positionell = r.Felder.Where(f => f.Zugriff is null).ToList();
        var eigenschaften = r.Felder.Where(f => f.Zugriff is not null).ToList();
        var basen = new[] { marker }.Concat(r.Basen ?? []).Where(x => !string.IsNullOrEmpty(x)).ToList();
        var typart = r.Typart ?? "public record";
        // Ein Read-Model mit typisierter Id ist partial (CQRS111): der Generator ergänzt seine Speicher-Identität.
        if (marker is not null && marker.StartsWith(IReadModel) && r.Felder.Any(f => f.Name == "Id" && f.Typ.StartsWith("Id<"))
            && !typart.Split(' ').Contains("partial"))
            typart = typart.Contains("record") ? typart.Replace("record", "partial record") : typart.Replace("class", "partial class");
        var kopf = $"{typart} {r.Name}"
                   + (r.OhneParameterliste && positionell.Count == 0 ? "" : $"({Parameter(positionell)})")
                   + (basen.Count == 0 ? "" : $" : {string.Join(", ", basen)}");
        if (eigenschaften.Count == 0 && string.IsNullOrWhiteSpace(r.Zusatz))
            b.AppendLine(kopf + (r.OhneParameterliste && positionell.Count == 0 ? " { }" : ";"));
        else
        {
            b.AppendLine(kopf);
            b.AppendLine("{");
            foreach (var f in eigenschaften)
                b.AppendLine($"    public {(f.Pflicht ? "required " : "")}{f.Typ} {f.Name} {f.Zugriff}" + (f.Standard is null ? "" : $" = {f.Standard};"));
            if (!string.IsNullOrWhiteSpace(r.Zusatz))
            {
                if (eigenschaften.Count > 0) b.AppendLine();
                Eingerückt(b, r.Zusatz, "    ");
            }
            b.AppendLine("}");
        }
        if (leerzeileDanach && (r.Zusatz is not null || r.Doku is not null || eigenschaften.Count > 0)) b.AppendLine();
    }

    /// <summary>Ein Command mit seinem Ziel-Aggregat (aus dem Code oder dem Decide, das ihn entscheidet); das Ziel-Feld als <c>Id&lt;Aggregat&gt;</c>.</summary>
    private static Record MitZiel(Record r, EditorModell modell)
    {
        var aggregat = r.Aggregat ?? modell.Decider.FirstOrDefault(d => d.Command == r.Name && d.Aggregat.Length > 0)?.Aggregat;
        if (aggregat is null) return r;
        return r with
        {
            Aggregat = aggregat,
            Felder = r.Felder.Select(f => f.Name == modell.Rahmen.AggregatIdFeld && f.Typ == nameof(Guid) ? f with { Typ = $"{Id}<{aggregat}>" } : f).ToList(),
        };
    }

    private static IEnumerable<string> RecordTypen(Record r) => r.Felder.Select(f => f.Typ);

    // ── Aggregat-Komposition ──────────────────────────────────────────────────────────────────
    private static IEnumerable<string> AggregatTypen(Aggregat agg, IReadOnlyList<DecideRegel> decider, IReadOnlyList<ApplyRegel> applier) =>
        agg.State.Select(f => f.Typ)
            .Concat(decider.Select(d => d.Command)).Concat(decider.SelectMany(d => d.Ergibt.Select(a => a.Event)))
            .Concat(applier.Select(a => a.Event));

    private static string StateDatei(Aggregat agg, EditorModell modell)
    {
        var b = Kopf(agg.Namespace, Usings(agg.Namespace, modell, [modell.Rahmen.VertragsNamespace], agg.State.Select(f => f.Typ), agg.Usings));
        Doku(b, agg.Doku, "");
        b.AppendLine($"public partial class {agg.Name} : {IState}");
        b.AppendLine("{");

        // In Deklarations-Reihenfolge (gespeichert und abgeleitet gemischt) — so bleibt der Round-trip ein Fixpunkt.
        for (var i = 0; i < agg.State.Count; i++)
        {
            var f = agg.State[i];
            // Kanonische Form: eine Leerzeile, wo gespeicherte Felder in abgeleitete übergehen.
            if (i > 0 && f.Ausdruck is not null && agg.State[i - 1].Ausdruck is null) b.AppendLine();
            b.AppendLine(f.Ausdruck is not null
                ? $"    public {f.Typ} {f.Name} => {f.Ausdruck};"
                : $"    public {f.Typ} {f.Name} {{ get;{(f.NurGet ? "" : " set;")} }}" + (f.Standard is null ? "" : $" = {f.Standard};"));
        }

        if (!string.IsNullOrWhiteSpace(agg.StateZusatz))
        {
            if (agg.State.Count > 0) b.AppendLine();
            Eingerückt(b, agg.StateZusatz, "    ");
        }

        b.AppendLine("}");
        return b.ToString();
    }

    private static string DeciderDatei(Aggregat agg, IReadOnlyList<DecideRegel> decider, EditorModell modell)
    {
        var r = modell.Rahmen;
        var b = Kopf(agg.Namespace, Usings(agg.Namespace, modell, [r.VertragsNamespace], AggregatTypen(agg, decider, []), agg.Usings));
        b.AppendLine($"public partial class {agg.Name}");
        b.AppendLine("{");
        b.AppendLine($"    public partial class {r.DeciderKlasse} : {IDecider}<{agg.Name}>");
        b.AppendLine("    {");

        for (var i = 0; i < decider.Count; i++)
        {
            var d = decider[i];
            var oneOf = string.Join(", ", d.Ergibt.Select(a => a.Event));
            b.AppendLine($"        public IEnumerable<{OneOf}<{oneOf}>> {r.DecideMethode}({d.Command} {d.Parameter})");
            b.AppendLine("        {");
            Rumpf(b, d.Rumpf, "TODO: Entscheidungslogik.");
            b.AppendLine("        }");
            if (i < decider.Count - 1) b.AppendLine();
        }
        Zusatz(b, FragenImZusatz(agg));

        b.AppendLine("    }");
        b.AppendLine("}");
        return b.ToString();
    }

    /// <summary>
    /// Die Fragen eines Aggregats (docs/03 §3.2) stehen als Quelltext im Zusatz des Deciders. Eine Frage, die dort fehlt, bekommt eine Methode mit
    /// Platzhalter; nennt eine vorhandene andere Antworten, als das Modell sagt, folgt ihr Rückgabetyp. Sonst bleibt der Zusatz byte-gleich (Fixpunkt).
    /// </summary>
    private static string? FragenImZusatz(Aggregat agg)
    {
        var z = agg.DeciderZusatz ?? "";
        foreach (var f in agg.Fragen)
        {
            if (string.IsNullOrEmpty(f.Query) || f.Antworten.Count == 0) continue;
            var rueckgabe = f.Antworten.Count == 1 ? f.Antworten[0] : $"{OneOf}<{string.Join(", ", f.Antworten)}>";
            var m = System.Text.RegularExpressions.Regex.Match(z, $@"public\s+(?<r>[^\n;{{}}=]+?)\s+Frage\(\s*{System.Text.RegularExpressions.Regex.Escape(f.Query)}\s");
            if (m.Success)
            {
                var r = m.Groups["r"];
                var bisher = Bezeichner.Matches(r.Value).Select(x => x.Value).Where(x => x != OneOf && x != "Task").ToHashSet(StringComparer.Ordinal);
                if (!bisher.SetEquals(f.Antworten)) z = z[..r.Index] + rueckgabe + z[(r.Index + r.Length)..];
                continue;
            }
            z = (z.Length > 0 ? z.TrimEnd() + "\n\n" : "") + $"public {rueckgabe} Frage({f.Query} q) => throw new NotImplementedException(\"TODO: Frage-Logik.\");";
        }
        return z.Length > 0 ? z : agg.DeciderZusatz;
    }

    private static string ApplierDatei(Aggregat agg, IReadOnlyList<ApplyRegel> applier, EditorModell modell)
    {
        var r = modell.Rahmen;
        var b = Kopf(agg.Namespace, Usings(agg.Namespace, modell, [r.VertragsNamespace], AggregatTypen(agg, [], applier), agg.Usings));
        b.AppendLine($"public partial class {agg.Name}");
        b.AppendLine("{");
        b.AppendLine($"    public partial class {r.ApplierKlasse} : {IApplier}<{agg.Name}>");
        b.AppendLine("    {");

        for (var i = 0; i < applier.Count; i++)
        {
            var a = applier[i];
            b.AppendLine($"        public void {r.ApplyMethode}({a.Event} {a.Parameter})");
            b.AppendLine("        {");
            Rumpf(b, a.Rumpf, "TODO: Apply-Logik.");
            b.AppendLine("        }");
            if (i < applier.Count - 1) b.AppendLine();
        }
        Zusatz(b, agg.ApplierZusatz);

        b.AppendLine("    }");
        b.AppendLine("}");
        return b.ToString();
    }

    // ── Leseseite: Fähigkeiten + Bündel, Store-Impl, Projektion, Reader ──────────────────────
    //    Nur SIGNATUREN aus dem Modell; Rümpfe/Handcode verbatim, neue Rümpfe = throw-Platzhalter. Kein Store im Ctor
    //    (CQRS054), kein new Store (CQRS055), eine Fn je Fähigkeit (CQRS051), OneOf/konkret (CQRS050), Form von Auf/Frage (CQRS057).

    private static IEnumerable<GenerierteDatei> LeseseitenDateien(EditorModell m, Leseseite lesen)
    {
        // ── Fähigkeiten + Bündel: je (Datei, Namespace) eine Schnittstellen-Datei ──
        var gruppen = new Dictionary<(string Pfad, string Ns), List<(string? Doku, string Zeile, IEnumerable<string> Typen)>>();
        void Nimm(string pfad, string ns, string? doku, string zeile, IEnumerable<string> typen)
        {
            if (!gruppen.TryGetValue((pfad, ns), out var l)) gruppen[(pfad, ns)] = l = new();
            l.Add((doku, zeile, typen));
        }
        foreach (var st in lesen.Stores)
        {
            var storeDatei = st.Datei ?? Ziel(m, st.Namespace, $"{st.Name}.cs");
            foreach (var f in st.Fns)
            {
                var fns = f.Namespace ?? st.Namespace;
                var pfad = f.Datei ?? (fns == st.Namespace ? storeDatei : Ziel(m, fns, $"{f.Name}.cs"));
                Nimm(pfad, fns, f.Doku,
                    $"public interface {f.Name} : {(f.Lesen ? IReadStore : IWriteStore)} {{ {f.Rueckgabe} {f.Methode}({ParameterListe(f.Parameter)}); }}",
                    f.Parameter.Select(p => p.Typ).Append(f.Rueckgabe));
            }
            if (st.IstBuendel)
                Nimm(storeDatei, st.Namespace, st.Doku,
                    $"public interface {st.Name} : {string.Join(", ", st.Fns.Select(f => f.Name).Prepend(IStore))} {{ }}",
                    st.Fns.Select(f => f.Name));
        }
        foreach (var ((pfad, ns), zeilen) in gruppen)
        {
            var b = Kopf(ns, Usings(ns, m, [m.Rahmen.VertragsNamespace], zeilen.SelectMany(z => z.Typen), []));
            for (var i = 0; i < zeilen.Count; i++)
            {
                if (i > 0) b.AppendLine();
                Doku(b, zeilen[i].Doku, "");
                b.AppendLine(zeilen[i].Zeile);
            }
            yield return Datei(pfad, b.ToString(), DateiArt.Schnittstellen);
        }

        // ── Store-Implementierungen: neue Klasse voll; bestehende → Schreib-/Lese-Teil (nur die Methoden zählen) ──
        foreach (var st in lesen.Stores.Where(s => s.Impl != null))
        {
            var impl = st.Impl!;
            if (impl.Datei == null)
            {
                yield return Datei(Ziel(m, impl.Namespace, $"{impl.Name}.cs"), StoreImplDatei(m, st, impl, st.Fns, neu: true), DateiArt.StoreImpl);
                continue;
            }
            // Bestehende Klasse: nur die NEUEN Fähigkeiten (ohne Datei) — die übrigen implementiert sie schon (sonst kompilierte sie nicht).
            foreach (var teil in st.Fns.Where(f => f.Datei == null).GroupBy(f => (f.Lesen ? impl.LeseDatei : impl.SchreibDatei) ?? impl.Datei))
                yield return Datei(teil.Key, StoreImplDatei(m, st, impl, teil.ToList(), neu: false), DateiArt.StoreImpl);
        }

        foreach (var k in lesen.Konsumenten)
            yield return Datei(k.Datei ?? Ziel(m, k.Namespace, $"{k.Name}.cs"), KonsumentDatei(m, k), DateiArt.Konsument);
        foreach (var r in lesen.Reader)
            yield return Datei(r.Datei ?? Ziel(m, r.Namespace, $"{r.Name}.cs"), LeserDatei(m, r), DateiArt.Leser);
    }

    private static string Ziel(EditorModell m, string ns, string name) =>
        Verzeichnis(m, ns) is { } v ? $"{v}/{name}" : $"{Unplatziert}{name}";

    private static GenerierteDatei Datei(string pfad, string inhalt, DateiArt art) =>
        new(pfad, inhalt, art, !pfad.StartsWith(Unplatziert, StringComparison.Ordinal));

    private static string StoreImplDatei(EditorModell m, Store st, StoreImpl impl, IReadOnlyList<Faehigkeit> fns, bool neu)
    {
        // Neu mit gewählter Store-Klasse: Basis aus dem Framework (Storeklassen); sonst die gemeinsame Basis des Bestands (StoreBasis).
        var klasse = neu ? Storeklassen.Von(st.Klassen.FirstOrDefault()) : null;
        var basis = neu && klasse == null ? m.Rahmen.StoreBasis : null;
        var dokumente = klasse is { Dokumente: true } ? Storeklassen.Dokumente(m, st).ToList() : [];
        var usings = new List<string>();
        if (basis != null) usings.Add(basis.Namespace);
        if (klasse != null) usings.AddRange(klasse.Usings);
        var b = Kopf(impl.Namespace, Usings(impl.Namespace, m, usings.Concat(basis?.Usings ?? []),
            fns.SelectMany(f => f.Parameter.Select(p => p.Typ).Append(f.Rueckgabe)).Append(st.Name).Concat(dokumente), []));
        var basen = klasse != null ? $"{klasse.Name}, {st.Name}" : basis == null ? st.Name : $"{basis.Name}, {st.Name}";
        b.AppendLine($"public sealed partial class {impl.Name}" + (neu ? $" : {basen}" : ""));
        b.AppendLine("{");
        var zeilen = new List<string>();
        if (klasse != null)
            zeilen.Add($"    public {impl.Name}({ParameterListe(klasse.Konstruktor)}) : base({Storeklassen.Argumente(klasse, impl.Name, dokumente)}) {{ }}");
        else if (basis != null)
            zeilen.Add($"    public {impl.Name}({ParameterListe(basis.KtorParameter)}) : base({string.Join(", ", basis.KtorParameter.Select(p => p.Name))}) {{ }}");
        foreach (var f in fns)
        {
            var kopf = $"    public {f.Rueckgabe} {f.Methode}({ParameterListe(f.Parameter)})";
            if (string.IsNullOrWhiteSpace(f.ImplRumpf))
                zeilen.Add($"{kopf}\n        => throw new NotImplementedException(\"TODO: {f.Name}.{f.Methode}\");");
            else
            {
                // Rumpf-Entwurf aus dem Editor (Code-/LLM-Knoten); async, wenn er await benutzt.
                var asy = f.ImplRumpf.Contains("await ") && !kopf.Contains(" async ") ? kopf.Replace("    public ", "    public async ") : kopf;
                var rb = new StringBuilder();
                Eingerückt(rb, f.ImplRumpf, "        ");
                zeilen.Add($"{asy}\n    {{\n{rb.ToString().TrimEnd('\n')}\n    }}");
            }
        }
        b.AppendLine(string.Join("\n\n", zeilen));
        b.AppendLine("}");
        return b.ToString();
    }

    /// <summary>Projektion: <c>IProjektion</c>, je Event ein <c>Task Auf(TEvent e[, Herkunft h], Schreib-Fähigkeit…[, CancellationToken ct])</c>.</summary>
    private static string KonsumentDatei(EditorModell m, Konsument k)
    {
        var b = Kopf(k.Namespace, Usings(k.Namespace, m, [m.Rahmen.VertragsNamespace], KlassenTypen(k.Datei, k.Handles), k.Usings));
        Doku(b, k.Doku, "");
        if (k.MindestensEinmal) b.AppendLine($"[{MindestensEinmal}]");
        Attribute(b, k.Attribute);
        b.AppendLine($"{k.Typart ?? "public sealed class"} {k.Name} : {string.Join(", ", k.Basen ?? [IProjektion])}");
        b.AppendLine("{");
        var teile = new List<Action>();
        if (k.Zusatz is not null) teile.Add(() => Eingerückt(b, k.Zusatz, "    "));
        foreach (var h in k.Handles)
            teile.Add(() => HandleZeilen(b, h, AufMethode, h.Rueckgabe ?? "Task"));
        Teile(b, teile);
        b.AppendLine("}");
        return b.ToString();
    }

    /// <summary>Reader: <c>IReader</c>, je Query eine <c>Frage(TQuery q, Lese-Fähigkeit…[, CancellationToken ct])</c> → Antwort bzw. <c>OneOf&lt;…&gt;</c>.</summary>
    private static string LeserDatei(EditorModell m, Leser r)
    {
        var b = Kopf(r.Namespace, Usings(r.Namespace, m, [m.Rahmen.VertragsNamespace], KlassenTypen(r.Datei, r.Handles), r.Usings));
        Doku(b, r.Doku, "");
        Attribute(b, r.Attribute);
        b.AppendLine($"{r.Typart ?? "public sealed class"} {r.Name} : {string.Join(", ", r.Basen ?? [IReader])}");
        b.AppendLine("{");
        var teile = new List<Action>();
        if (r.Zusatz is not null) teile.Add(() => Eingerückt(b, r.Zusatz, "    "));
        // Eine Frage ohne Antwort hat keinen Vertrag (CQRS050/CQRS057) — nicht geschrieben (der Build meldet es).
        foreach (var h in r.Handles.Where(h => h.Rueckgabe != null || h.Ausgaenge.Count > 0))
            teile.Add(() => HandleZeilen(b, h, FrageMethode, h.Rueckgabe ?? $"Task<{OneOfVon(h.Ausgaenge)}>"));
        Teile(b, teile);
        b.AppendLine("}");
        return b.ToString();
    }

    /// <summary>
    /// Typen, aus denen die usings einer Projektions-/Reader-Datei ABGELEITET werden: bei einer NEUEN Klasse alle, bei einer
    /// bestehenden nur die der neuen Eingänge (die bestehenden usings stehen verbatim im Modell — keine neuen Mehrdeutigkeiten).
    /// </summary>
    private static IEnumerable<string> KlassenTypen(string? datei, IReadOnlyList<Handle> handles) =>
        handles.Where(h => datei == null || h.Datei == null)
            .SelectMany(h => h.Faehigkeiten.Select(f => f.Typ).Concat(h.Ausgaenge).Append(h.Eingang).Concat(h.Ueber is { } u ? [u] : []));

    /// <summary>Ein Eingang: <c>Methode(Eingang e[, Herkunft h], Fähigkeit…[, CancellationToken ct])</c> mit Rumpf (verbatim) bzw. <c>throw</c>-Platzhalter.</summary>
    private static void HandleZeilen(StringBuilder b, Handle h, string methode, string rueckgabe)
    {
        var ps = new List<string> { (h.Ueber is { } u ? $"[Ueber<{u}>] " : "") + $"{h.Eingang} {h.Parameter}" };
        if (h.HerkunftParameter is { } hp) ps.Add($"{Herkunft} {hp}");
        ps.AddRange(h.Faehigkeiten.Select(f => $"{f.Typ} {f.Name}"));
        if (h.AbbruchParameter is { } ap) ps.Add($"{Abbruch} {ap}");
        // Neuer Eingang mit Entwurf, der await benutzt ⇒ async (bestehende Modifizierer bleiben wörtlich).
        var asy = h.Rumpf is { } r && r.Contains("await ");
        var mods = h.Modifikatoren ?? (asy ? "public async" : "public");
        var kopf = $"    {mods} {rueckgabe} {methode}({string.Join(", ", ps)})";
        if (h.Ausdruck is not null)
        {
            b.AppendLine(kopf + " =>");
            b.AppendLine("        " + h.Ausdruck + ";");
            return;
        }
        b.AppendLine(kopf);
        b.AppendLine("    {");
        if (h.Rumpf is null) b.AppendLine($"        throw new NotImplementedException(\"TODO: {methode}({h.Eingang})\");");
        else if (h.Rumpf.Trim().Length > 0) Eingerückt(b, h.Rumpf, "        ");
        b.AppendLine("    }");
    }

    private static void Teile(StringBuilder b, List<Action> teile)
    {
        for (var i = 0; i < teile.Count; i++)
        {
            if (i > 0) b.AppendLine();
            teile[i]();
        }
    }

    private static void Attribute(StringBuilder b, string? attribute)
    {
        if (string.IsNullOrWhiteSpace(attribute)) return;
        foreach (var zeile in attribute.Replace("\r\n", "\n").Split('\n')) b.AppendLine(zeile);
    }

    private static string OneOfVon(IReadOnlyList<string> typen) =>
        typen.Count == 1 ? typen[0] : $"{OneOf}<{string.Join(", ", typen)}>";

    private static string ParameterListe(IReadOnlyList<Parameter> ps) =>
        string.Join(", ", ps.Select(p => $"{p.Typ} {p.Name}" + (p.Standard is null ? "" : $" = {p.Standard}")));

    // ── Bausteine ─────────────────────────────────────────────────────────────────────────────
    private static StringBuilder Kopf(string ns, IReadOnlyList<string> usings)
    {
        var b = new StringBuilder();
        foreach (var u in usings) b.AppendLine($"using {u};");
        if (usings.Count > 0) b.AppendLine();
        b.AppendLine($"namespace {ns};");
        b.AppendLine();
        return b;
    }

    private static void Doku(StringBuilder b, string? doku, string einzug)
    {
        if (string.IsNullOrWhiteSpace(doku)) return;
        b.AppendLine($"{einzug}/// <summary>");
        foreach (var zeile in doku.Replace("\r\n", "\n").Split('\n'))
            b.AppendLine($"{einzug}/// {zeile}");
        b.AppendLine($"{einzug}/// </summary>");
    }

    private static void Rumpf(StringBuilder b, string? rumpf, string todo)
    {
        if (rumpf is null)
        {
            b.AppendLine($"            throw new NotImplementedException(\"{todo}\");");
            return;
        }
        // "" = bewusst leerer Rumpf (No-op-Apply) — kein Platzhalter.
        if (rumpf.Trim().Length == 0) return;
        Eingerückt(b, rumpf, "            ");
    }

    /// <summary>Handcode-Member (Hilfsmethoden) in eine innere Klasse hängen.</summary>
    private static void Zusatz(StringBuilder b, string? zusatz)
    {
        if (string.IsNullOrWhiteSpace(zusatz)) return;
        b.AppendLine();
        Eingerückt(b, zusatz, "        ");
    }

    private static void Eingerückt(StringBuilder b, string text, string einzug)
    {
        foreach (var zeile in text.Replace("\r\n", "\n").TrimEnd('\n').Split('\n'))
            b.AppendLine(zeile.Trim().Length == 0 ? "" : einzug + zeile);
    }

    /// <summary>
    /// Die <c>using</c>-Liste einer Datei: feste Basis + explizite (aus dem Code gelesene) + ABGELEITETE —
    /// jeder Typname in <paramref name="typen"/>, der ein Record/Enum eines ANDEREN Namespace im Modell ist,
    /// zieht dessen Namespace nach. So kompiliert Komposition über Aggregat-Grenzen ohne Handarbeit.
    /// </summary>
    private static IReadOnlyList<string> Usings(string ns, EditorModell modell, IEnumerable<string> basis,
        IEnumerable<string> typen, IEnumerable<string> explizit)
    {
        var nsVon = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var r in modell.Records) nsVon.TryAdd(r.Name, r.Namespace);
        foreach (var e in modell.Enums) nsVon.TryAdd(e.Name, e.Namespace);
        foreach (var st in modell.Lesen?.Stores ?? [])
        {
            nsVon.TryAdd(st.Name, st.Namespace);
            foreach (var f in st.Fns) nsVon.TryAdd(f.Name, f.Namespace ?? st.Namespace);
        }
        foreach (var k in modell.Lesen?.Konsumenten ?? []) nsVon.TryAdd(k.Name, k.Namespace);
        foreach (var f in modell.BausteinFunktionen) nsVon.TryAdd(f.Name, f.Namespace);   // z. B. IDarf<IFunktion> am Akteur, Client über einen verkörperten Akteur
        foreach (var a in modell.Akteure) nsVon.TryAdd(a.Name, a.Namespace);      // IVerkoerpert<Akteur> am Client
        foreach (var c in modell.Clients) nsVon.TryAdd(c.Name, c.Namespace);      // IApp<Client> an der App
        foreach (var u in modell.Uebersetzungen) nsVon.TryAdd(u.Name, u.Namespace);   // [Uebersetzt<U>] an einer Quelle
        foreach (var g in modell.Graphen) nsVon.TryAdd(g.Name, g.Namespace);      // ein Graph als Knoten eines anderen
        foreach (var a in modell.Aggregate) nsVon.TryAdd(a.Name, a.Namespace);    // der Schlüssel eines Bündels ([Ueber<Aggregat>])

        var menge = new SortedSet<string>(basis.Concat(explizit), StringComparer.Ordinal);
        foreach (var typ in typen)
        {
            // Eine typisierte Id (Id<TAggregat>) ist ein Typ des Vertrags — auch in Wertobjekten und Leseseiten-Records.
            if (IdTyp.IsMatch(typ ?? "")) menge.Add(modell.Rahmen.VertragsNamespace);
            foreach (System.Text.RegularExpressions.Match m in Bezeichner.Matches(typ ?? ""))
                if (nsVon.TryGetValue(m.Value, out var andere) && andere != ns) menge.Add(andere);
        }
        menge.Remove(ns);
        // Vertrags-Namespace zuerst (Hausstil), dann alphabetisch.
        return menge.OrderBy(u => u == modell.Rahmen.VertragsNamespace ? 0 : 1).ThenBy(u => u, StringComparer.Ordinal).ToList();
    }

    private static readonly System.Text.RegularExpressions.Regex Bezeichner = new(@"[A-Za-z_][A-Za-z0-9_]*");
    private static readonly System.Text.RegularExpressions.Regex IdTyp = new($@"(?<![A-Za-z0-9_.]){Id}<");

    private static string Parameter(IReadOnlyList<Feld> felder) =>
        string.Join(", ", felder.Select(f =>
            $"{f.Typ} {f.Name}" + (f.Standard is null ? "" : $" = {f.Standard}")));
}
