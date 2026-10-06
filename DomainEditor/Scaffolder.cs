using System.Text;
using Abstractions;

namespace DomainEditor;

/// <summary>Welche Rolle eine generierte Datei hat — bestimmt, wie sie in eine bestehende Datei gemischt wird.</summary>
public enum DateiArt
{
    Typen, State, Decider, Applier, Saga,
    /// <summary>Fähigkeits- und Bündel-Interfaces eines Stores (Mischen: fehlende Interfaces + fehlende Basen im Bündel).</summary>
    Schnittstellen,
    /// <summary>Methoden einer Store-Implementierung (Mischen: fehlende Methoden nach Name + Parametertypen).</summary>
    StoreImpl,
    /// <summary>Projektion/Reaktion bzw. Reader (Mischen: fehlende Handles nach Eingangstyp; Parameter-Abgleich gesondert).</summary>
    Konsument, Leser,
    /// <summary>Pipeline (Mischen: fehlende Handles nach Eingangstyp; Parameter/Rückgabe-Abgleich gesondert).</summary>
    Pipeline,
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
/// das Verzeichnis/Dateimuster, das der übrige Code benutzt (<see cref="Rahmen"/>). Marker- und DSL-Namen kommen über
/// <c>nameof</c>/<c>typeof</c> aus dem Vertrag.
///
/// Erzeugt bewusst NUR die handgeschriebenen Dateien — Id/Version, State-Property, Handler und Factory liefern die
/// bestehenden Roslyn-Generatoren. Decide/Apply-Körper ohne Rumpf werden ein kompilierbarer <c>throw</c>-Platzhalter.
/// </summary>
public static class Scaffolder
{
    // ── Der Vertrag, über nameof/typeof (nie als String) ──
    private static readonly string ICommand = nameof(Abstractions.ICommand);
    private static readonly string ICreationCommand = nameof(Abstractions.ICreationCommand);
    private static readonly string IEvent = nameof(Abstractions.IEvent);
    private static readonly string ITransientEvent = nameof(Abstractions.ITransientEvent);
    private static readonly string IState = nameof(Abstractions.IState);
    private static readonly string IDecider = typeof(IDecider<>).Name.Split('`')[0];
    private static readonly string IApplier = typeof(IApplier<>).Name.Split('`')[0];
    private static readonly string IProzessDefinition = nameof(Abstractions.IProzessDefinition);
    private static readonly string ProzessRegeln = nameof(Abstractions.ProzessRegeln);
    private static readonly string Regeln = nameof(Abstractions.IProzessDefinition.Regeln);
    private static readonly string Prozess = typeof(Prozess<>).Name.Split('`')[0];
    private static readonly string Definiere = nameof(Prozess<IEvent>.Definiere);
    private static readonly string Auf = nameof(RegelBauer.Auf);
    private static readonly string Und = nameof(RegelBauer<IEvent>.Und);
    private static readonly string UndAlle = nameof(RegelBauer<IEvent>.UndAlle);
    private static readonly string Sende = nameof(RegelBauer<IEvent>.Sende);
    private static readonly string SendeJe = nameof(RegelBauer<IEvent>.SendeJe);
    private static readonly string Rufe = nameof(RegelBauer<IEvent>.Rufe);
    private static readonly string Zeitlimit = nameof(RegelAbschluss<IEvent>.Zeitlimit);
    // ── Katalog-Funktionen ──
    private static readonly string IFunktion = nameof(Abstractions.IFunktion);
    private static readonly string IAuftrag = typeof(IAuftrag<>).Name.Split('`')[0];
    private static readonly string IAusfuehrung = nameof(Abstractions.IAusfuehrung);
    private static readonly string RückgängigDurch = nameof(RegelAbschluss<IEvent>.RückgängigDurch);
    private static readonly string RückgängigDurchJe = nameof(RegelAbschluss<IEvent>.RückgängigDurchJe);
    private static readonly string OneOf = typeof(OneOf<>).Name.Split('`')[0];
    // ── Leseseite ──
    private static readonly string IQuery = nameof(Abstractions.IQuery);
    private static readonly string IQueryResponse = nameof(Abstractions.IQueryResponse);
    private static readonly string IReadModel = nameof(Abstractions.IReadModel);
    private static readonly string IGeteiltesReadModel = nameof(Abstractions.IGeteiltesReadModel);
    private static readonly string IWriteStore = nameof(Abstractions.IWriteStore);
    private static readonly string IReadStore = nameof(Abstractions.IReadStore);
    private static readonly string IStore = nameof(Abstractions.IStore);
    private static readonly string ISubscriber = nameof(Abstractions.ISubscriber);
    private static readonly string IPullSubscriber = nameof(Abstractions.IPullSubscriber);
    private static readonly string IAppendProjektion = nameof(Abstractions.IAppendProjektion);
    private static readonly string SubscriberId = nameof(Abstractions.ISubscriber.SubscriberId);
    private static readonly string IReader = typeof(IReader<>).Name.Split('`')[0];
    private static readonly string IAggregateEnvelope = nameof(Abstractions.IAggregateEnvelope);
    private static readonly string IMessageEnvelope = nameof(Abstractions.IMessageEnvelope);
    private static readonly string ReadContext = nameof(Abstractions.ReadContext);
    private static readonly string ProjectionReader = nameof(ProjectionReaderAttribute)[..^"Attribute".Length];
    private static readonly string TrackDeps = nameof(ProjectionReaderAttribute.TrackDeps);
    private static readonly string IPipelineHandler = nameof(Abstractions.IPipelineHandler);
    private static readonly string IPipelineTrigger = nameof(Abstractions.IPipelineTrigger);
    private static readonly string IPipelineSelfMessage = nameof(Abstractions.IPipelineSelfMessage);
    private static readonly string PipelineContext = nameof(Abstractions.PipelineContext);
    private static readonly string PipelineId = nameof(Abstractions.IPipelineHandler.PipelineId);
    // ── Akteure ──
    private static readonly string IAkteur = nameof(Abstractions.IAkteur);
    private static readonly string IDarf = typeof(IDarf<>).Name.Split('`')[0];
    /// <summary>
    /// Der Methodenname, den die Dispatch-Generatoren rufen — Framework-Vertrag, erzwungen durch CQRS057
    /// (HandlerFormAnalyzer): ein anders benannter Handler ist ein Build-Fehler, kein stilles Auseinanderlaufen.
    /// </summary>
    private const string HandleMethode = "Handle";

    public static IReadOnlyList<GenerierteDatei> Generiere(EditorModell modell)
    {
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
            Gruppe(rec.Datei ?? TypZiel(modell, rec.Namespace, Sorte(rec.Kind), rec.Name), rec.Namespace).Records.Add(rec);
        foreach (var e in modell.Enums)
            Gruppe(e.Datei ?? TypZiel(modell, e.Namespace, "enum", e.Name), e.Namespace).Enums.Add(e);
        foreach (var ((pfad, _), g) in gruppen)
            dateien.Add(new(pfad, TypDatei(g.Ns, g.Records, g.Enums, modell), DateiArt.Typen, !pfad.StartsWith(Unplatziert, StringComparison.Ordinal)));

        // ── Akteure → ihre echte Datei; sonst die (eine) Akteur-Datei ihres Namespace; sonst Akteure.cs im Verzeichnis ──
        //    Ein bestehender Dienst-Vertrag gehört dem Code (Methoden!) — nur seine Basisliste gleicht der Abgleich ab.
        foreach (var g in modell.Akteure
                     .GroupBy(a => (Pfad: a.Datei ?? AkteurZiel(modell, a.Namespace), a.Namespace)))
            dateien.Add(new(g.Key.Pfad, AkteurDatei(g.Key.Namespace, g.ToList(), modell), DateiArt.Typen,
                !g.Key.Pfad.StartsWith(Unplatziert, StringComparison.Ordinal)));

        // ── Clients (docs/konzept-akteure.md §4) → ihre echte Datei; sonst die (eine) Client-Datei ihres Namespace; sonst Clients.cs ──
        foreach (var g in modell.Clients.GroupBy(c => (Pfad: c.Datei ?? ClientZiel(modell, c.Namespace), c.Namespace)))
            dateien.Add(new(g.Key.Pfad, ClientDatei(g.Key.Namespace, g.ToList(), modell), DateiArt.Typen,
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

        foreach (var saga in modell.Sagas)
            dateien.Add(Platziert(saga.Datei, Verzeichnis(modell, saga.Namespace), $"{saga.Name}.cs", SagaDatei(saga, modell), DateiArt.Saga));

        // ── Katalog-Funktionen → je Funktion ihre Schnittstelle (nur die Signatur; die Implementierung ist Bindung) ──
        foreach (var f in modell.Funktionen)
            dateien.Add(Platziert(f.Datei, Verzeichnis(modell, f.Namespace), $"{f.Name}.cs", FunktionsDatei(f, modell), DateiArt.Typen));

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
    /// Zieldatei eines NEUEN Typs (ohne eigene Datei) — feste Regel, keine Mehrheits-Statistik: stehen ALLE gleichartigen
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
            RecordArt.Trigger => "Triggers.cs",
            RecordArt.Selbst => "SelbstNachrichten.cs",
            RecordArt.Auftrag => "Auftraege.cs",
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

    /// <summary>Die Basisliste eines Akteurs: <c>IMensch, IDarf&lt;A&gt;, IDarf&lt;B&gt;</c> (Art bzw. <c>IAkteur</c>) — der ganze Akteur steht in ihr.</summary>
    public static IReadOnlyList<string> AkteurBasen(Akteur a) => [ArtBasis(a.Art), .. a.Darf.Select(d => $"{IDarf}<{d}>")];

    /// <summary>Die Art als Basistyp: „Mensch" → <c>IMensch</c>, „Maschine" → <c>IMaschine</c>, „Ki" → <c>IKi</c>, sonst <c>IAkteur</c>.</summary>
    public static string ArtBasis(string? art) => art switch
    {
        "Mensch" => nameof(Abstractions.IMensch),
        "Maschine" => nameof(Abstractions.IMaschine),
        "Ki" => nameof(Abstractions.IKi),
        _ => IAkteur,
    };

    /// <summary>Gehört dieser Basistyp zum Akteur-Vertrag (<c>IAkteur</c> bzw. <c>IDarf&lt;…&gt;</c>)? Alles andere bleibt beim Abgleich stehen.</summary>
    public static bool IstAkteurBasis(string basis)
    {
        var name = basis.Split('<')[0].Split('.').Last().Trim();
        return name == IAkteur || name == IDarf
            || name == nameof(Abstractions.IMensch) || name == nameof(Abstractions.IMaschine) || name == nameof(Abstractions.IKi);
    }

    // ── Clients ─────────────────────────────────────────────────────────────────────────────────
    private static readonly string IClientVertrag = nameof(Abstractions.IClientVertrag);
    private static readonly string ISendet = typeof(ISendet<>).Name.Split('`')[0];
    private static readonly string IFragt = typeof(IFragt<>).Name.Split('`')[0];

    private static string ClientZiel(EditorModell m, string ns)
    {
        var dateien = m.Clients.Where(c => c.Namespace == ns && c.Datei != null).Select(c => c.Datei!).Distinct(StringComparer.Ordinal).ToList();
        if (dateien.Count == 1) return dateien[0];
        var v = Verzeichnis(m, ns);
        return v != null ? $"{v}/Clients.cs" : $"{Unplatziert}Clients.cs";
    }

    /// <summary>
    /// Die Basisliste eines Client-Vertrags: <c>IClientVertrag, ITeil…, ISendet&lt;C&gt;…, IFragt&lt;Q&gt;…</c> — der ganze Rand bis auf die
    /// Kenntnis-Methoden steht in ihr.
    /// </summary>
    public static IReadOnlyList<string> ClientBasen(Client c) =>
        [IClientVertrag, .. c.Traegt, .. c.Sendet.Select(s => $"{ISendet}<{s}>"), .. c.Fragt.Select(q => $"{IFragt}<{q}>")];

    /// <summary>Gehört dieser Basistyp zum Client-Vertrag (wird vom Abgleich verwaltet)? <paramref name="teile"/> = bekannte Vertrags-Teile.</summary>
    public static bool IstClientBasis(string basis, ISet<string> teile)
    {
        var name = basis.Split('<')[0].Split('.').Last().Trim();
        return name == IClientVertrag || name == ISendet || name == IFragt || teile.Contains(name);
    }

    /// <summary>Kenntnis als Signatur: <c>void Auf(E e)</c>.</summary>
    public static string KenntnisMethode(string e) => $"void {Abstractions.Akteurvertrag.Auf}({e} e)";

    /// <summary>Ein Client-Vertrag: <c>public interface IX : IClientVertrag, … { void Auf(E e); }</c>.</summary>
    public static string ClientInterface(Client c)
    {
        var b = new StringBuilder();
        var basen = ClientBasen(c);
        b.AppendLine($"public interface {c.Name} : {string.Join(",\n    ", basen)}");
        b.AppendLine("{");
        foreach (var e in c.Kenntnis) b.AppendLine($"    {KenntnisMethode(e)};");
        b.AppendLine("}");
        return b.ToString();
    }

    private static string ClientDatei(string ns, List<Client> clients, EditorModell modell)
    {
        // usings: Vertrags-Teile liegen beim Akteur (dessen Namespace), Nachrichten bei ihren Records.
        var teilNs = modell.Akteure.Where(a => a.Vertrag.Count > 0)
            .SelectMany(a => a.Vertrag.Select(r => r.Teil ?? a.VertragTyp).Distinct().Select(t => (t, a.Namespace)))
            .Where(x => clients.Any(c => c.Traegt.Contains(x.t))).Select(x => x.Namespace);
        var b = Kopf(ns, Usings(ns, modell, [modell.Rahmen.VertragsNamespace, .. teilNs],
            clients.SelectMany(c => c.Sendet.Concat(c.Fragt).Concat(c.Kenntnis)), []));
        for (var i = 0; i < clients.Count; i++)
        {
            Doku(b, clients[i].Doku, "");
            b.Append(ClientInterface(clients[i]));
            if (i < clients.Count - 1) b.AppendLine();
        }
        return b.ToString();
    }

    private static string AkteurDatei(string ns, List<Akteur> akteure, EditorModell modell)
    {
        var b = Kopf(ns, Usings(ns, modell, [modell.Rahmen.VertragsNamespace],
            akteure.SelectMany(a => a.Darf.Concat(a.Vertrag.SelectMany(r => r.Ausgaenge.Append(r.Eingang)))), []));
        for (var i = 0; i < akteure.Count; i++)
        {
            Doku(b, akteure[i].Doku, "");
            b.AppendLine($"public sealed record {akteure[i].Name} : {string.Join(", ", AkteurBasen(akteure[i]))};");
            if (akteure[i].Vertrag.Count > 0)
            {
                b.AppendLine();
                b.Append(VertragsInterface(akteure[i]));
            }
            if (i < akteure.Count - 1) b.AppendLine();
        }
        return b.ToString();
    }

    private static readonly string IAkteurVertrag = typeof(Abstractions.IAkteurVertrag<>).Name.Split('`')[0];

    /// <summary>
    /// Der Vertrag eines Akteurs (<c>docs/konzept-akteure.md</c> §3): <c>public interface IX : IAkteurVertrag&lt;X&gt; { … Auf(E e); }</c> —
    /// je Zusage eine Methode in Modell-Reihenfolge.
    /// </summary>
    public static string VertragsInterface(Akteur a)
    {
        var b = new StringBuilder();
        b.AppendLine($"public interface {a.VertragTyp} : {IAkteurVertrag}<{a.Name}>");
        b.AppendLine("{");
        var haupt = a.Vertrag.Where(r => r.Teil == null).ToList();   // weitere Vertrags-Teile liest der Editor nur
        for (var i = 0; i < haupt.Count; i++)
        {
            Doku(b, haupt[i].Doku, "    ");
            b.AppendLine($"    {VertragsMethode(haupt[i])};");
            if (i < haupt.Count - 1) b.AppendLine();
        }
        b.AppendLine("}");
        return b.ToString();
    }

    /// <summary>Eine Zusage als Signatur: <c>void Auf(E e)</c>, <c>OneOf&lt;A, B&gt; Auf(E e)</c> bzw. <c>IAsyncEnumerable&lt;OneOf&lt;…&gt;&gt; Auf(E e)</c>.</summary>
    public static string VertragsMethode(AkteurZusage r) => $"{VertragsRueckgabe(r)} {Abstractions.Akteurvertrag.Auf}({r.Eingang} e)";

    public static string VertragsRueckgabe(AkteurZusage r)
    {
        if (r.Ausgaenge.Count == 0) return "void";
        var oneOf = $"OneOf<{string.Join(", ", r.Ausgaenge)}>";
        return r.Strom ? $"IAsyncEnumerable<{oneOf}>" : oneOf;
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
        var commands = records.Where(r => r.Kind == RecordArt.Command).ToList();
        if (commands.Count > 0) abschnitte.Add(() =>
        {
            for (var i = 0; i < commands.Count; i++)
                RecordZeilen(b, commands[i], commands[i].IstErzeugung ? $"{ICommand}, {ICreationCommand}" : ICommand, i < commands.Count - 1);
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
                     (RecordArt.Konfig, null), (RecordArt.Trigger, IPipelineTrigger), (RecordArt.Selbst, IPipelineSelfMessage) })
        {
            var liste = records.Where(r => r.Kind == art).ToList();
            if (liste.Count > 0) abschnitte.Add(() =>
            {
                for (var i = 0; i < liste.Count; i++)
                    RecordZeilen(b, liste[i], liste[i].Geteilt && art == RecordArt.ReadModel ? $"{marker}, {IGeteiltesReadModel}" : marker,
                        i < liste.Count - 1);
            });
        }

        // Aufträge: je Record der Marker seiner Funktion (IAuftrag<F>) — der eine Eingang einer Katalog-Funktion.
        var auftraege = records.Where(r => r.Kind == RecordArt.Auftrag).ToList();
        if (auftraege.Count > 0) abschnitte.Add(() =>
        {
            for (var i = 0; i < auftraege.Count; i++)
                RecordZeilen(b, auftraege[i], string.IsNullOrWhiteSpace(auftraege[i].Funktion) ? null : $"{IAuftrag}<{auftraege[i].Funktion}>",
                    i < auftraege.Count - 1);
        });

        for (var i = 0; i < abschnitte.Count; i++)
        {
            if (i > 0) b.AppendLine();
            abschnitte[i]();
        }
        return b.ToString();
    }

    // ── Katalog-Funktion: nur die Signatur (CQRS068) ─────────────────────────────────────────
    private static string FunktionsDatei(Funktion f, EditorModell modell)
    {
        var b = Kopf(f.Namespace, Usings(f.Namespace, modell, [modell.Rahmen.VertragsNamespace], [f.Auftrag, .. f.Ergebnisse], []));
        Doku(b, f.Doku, "");
        b.Append(FunktionsInterface(f));
        return b.ToString();
    }

    /// <summary><c>public interface IX : IFunktion { Task&lt;OneOf&lt;E…&gt;&gt; RufeAsync(XAuftrag auftrag, IAusfuehrung x); }</c></summary>
    public static string FunktionsInterface(Funktion f)
    {
        var b = new StringBuilder();
        b.AppendLine($"public interface {f.Name} : {IFunktion}");
        b.AppendLine("{");
        b.AppendLine($"    Task<{OneOf}<{string.Join(", ", f.Ergebnisse)}>> {Funktionsvertrag.Methode}({f.Auftrag} auftrag, {IAusfuehrung} x);");
        b.AppendLine("}");
        return b.ToString();
    }

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
        var kopf = $"{r.Typart ?? "public record"} {r.Name}"
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

    private static IEnumerable<string> RecordTypen(Record r) =>
        string.IsNullOrWhiteSpace(r.Funktion) ? r.Felder.Select(f => f.Typ) : r.Felder.Select(f => f.Typ).Append(r.Funktion);

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
        Zusatz(b, agg.DeciderZusatz);

        b.AppendLine("    }");
        b.AppendLine("}");
        return b.ToString();
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

    // ── Saga / Prozess ────────────────────────────────────────────────────────────────────────
    private static string SagaDatei(Saga saga, EditorModell modell)
    {
        var referenzen = new List<string> { saga.TriggerEvent };
        foreach (var st in saga.Schritte)
        {
            referenzen.AddRange(st.Wenn);
            if (!string.IsNullOrWhiteSpace(st.Rufe)) { referenzen.Add(st.Rufe); referenzen.Add(AuftragVon(st.Rufe, modell) ?? st.Rufe); }
            else referenzen.Add(st.Sende);
            if (st.SammelEvent is not null) referenzen.Add(st.SammelEvent);
            if (st.Kompensation is not null) referenzen.Add(st.Kompensation);
        }
        var b = Kopf(saga.Namespace, Usings(saga.Namespace, modell, [modell.Rahmen.VertragsNamespace], referenzen, saga.ExtraUsings));
        Doku(b, saga.Doku, "");
        b.AppendLine($"public sealed class {saga.Name} : {IProzessDefinition}");
        b.AppendLine("{");
        b.AppendLine($"    public {ProzessRegeln} {Regeln} => {Prozess}<{saga.TriggerEvent}>.{Definiere}(p =>");
        b.AppendLine("    {");

        for (var i = 0; i < saga.Schritte.Count; i++)
        {
            SchrittZeilen(b, saga.Schritte[i], modell);
            if (i < saga.Schritte.Count - 1) b.AppendLine();
        }

        b.AppendLine("    });");
        b.AppendLine("}");
        return b.ToString();
    }

    /// <summary>Der Auftrag der Funktion (ihr einer Eingang) — aus dem Modell; null, wenn die Funktion (noch) unbekannt ist.</summary>
    private static string? AuftragVon(string funktion, EditorModell modell) =>
        modell.Funktionen.FirstOrDefault(f => f.Name == funktion)?.Auftrag;

    private static void SchrittZeilen(StringBuilder b, SagaSchritt s, EditorModell modell)
    {
        var auf = new StringBuilder($"        p.{Auf}<{s.Wenn[0]}>()");
        for (var i = 1; i < s.Wenn.Count; i++) auf.Append($".{Und}<{s.Wenn[i]}>()");
        var hatSammel = !string.IsNullOrWhiteSpace(s.SammelEvent);
        if (hatSammel)
        {
            var anzahl = !string.IsNullOrWhiteSpace(s.SammelAusdruck) ? s.SammelAusdruck
                : $"t => {(string.IsNullOrWhiteSpace(s.SammelAnzahl) ? "0" : s.SammelAnzahl)}";
            auf.Append($".{UndAlle}<{s.SammelEvent}>({anzahl})");
        }
        b.AppendLine(auf.ToString());

        var lambda = LambdaKopf(s.Wenn.Count + (hatSammel ? 1 : 0));
        var hatKomp = s.Kompensation is not null;
        var hatLimit = !string.IsNullOrWhiteSpace(s.Zeitlimit);
        var verb = s.SendeJe ? SendeJe : Sende;
        string sende;
        if (!string.IsNullOrWhiteSpace(s.Rufe))
        {
            // Aufruf-Knoten: die Funktion steht im Typ-Argument (CQRS003), der Lambda baut ihren Auftrag (verbatim oder Stub).
            var auftrag = AuftragVon(s.Rufe, modell);
            var ausdruck = !string.IsNullOrWhiteSpace(s.SendeAusdruck) ? s.SendeAusdruck
                : auftrag is null ? $"{lambda} => default!" : $"{lambda} => new {auftrag}({ArgListe(s.SendeArgumente, auftrag, modell)})";
            sende = $"            .{Rufe}<{s.Rufe}>({ausdruck})";
        }
        else if (!string.IsNullOrWhiteSpace(s.SendeAusdruck))
            sende = $"            .{verb}<{s.Sende}>({s.SendeAusdruck})";
        else if (s.SendeJe)
        {
            var sendeArgs = ArgListe(s.SendeArgumente, s.Sende, modell);
            var el = string.IsNullOrWhiteSpace(s.SendeJeElement) ? "z" : s.SendeJeElement;
            var coll = string.IsNullOrWhiteSpace(s.SendeJeCollection) ? "/* Collection */" : s.SendeJeCollection;
            sende = $"            .{SendeJe}<{s.Sende}>({lambda} => {coll}.Select({el} => new {s.Sende}({sendeArgs})))";
        }
        else
            sende = $"            .{Sende}<{s.Sende}>({lambda} => new {s.Sende}({ArgListe(s.SendeArgumente, s.Sende, modell)}))";
        b.AppendLine(hatKomp || hatLimit ? sende : sende + ";");
        if (hatLimit) b.AppendLine($"            .{Zeitlimit}({s.Zeitlimit})" + (hatKomp ? "" : ";"));

        if (hatKomp)
        {
            var kompVerb = s.KompensationJe ? RückgängigDurchJe : RückgängigDurch;
            var komp = !string.IsNullOrWhiteSpace(s.KompensationAusdruck)
                ? s.KompensationAusdruck
                : $"{lambda} => new {s.Kompensation}({ArgListe(s.KompensationArgumente, s.Kompensation!, modell)})";
            b.AppendLine($"            .{kompVerb}<{s.Kompensation}>({komp});");
        }
    }

    // ── Leseseite: Fähigkeiten + Bündel, Store-Impl, Projektion/Reaktion, Reader ──────────────────────
    //    Nur SIGNATUREN aus dem Modell; Rümpfe/Handcode verbatim, neue Rümpfe = throw-Platzhalter. Kein Store im Ctor
    //    (CQRS054), kein new Store (CQRS055), eine Fn je Fähigkeit (CQRS051), OneOf/konkret (CQRS050), Handle-Form (CQRS057).

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
        {
            var d = KonsumentDatei(m, k);
            yield return d == null
                ? new GenerierteDatei($"{Unplatziert}{k.Name}.cs", "", DateiArt.Konsument, Platzierbar: false)
                : Datei(k.Datei ?? Ziel(m, k.Namespace, $"{k.Name}.cs"), d, DateiArt.Konsument);
        }
        foreach (var r in lesen.Reader)
            yield return Datei(r.Datei ?? Ziel(m, r.Namespace, $"{r.Name}.cs"), LeserDatei(m, r), DateiArt.Leser);
        foreach (var p in lesen.Pipelines)
            yield return Datei(p.Datei ?? Ziel(m, p.Namespace, $"{p.Name}.cs"), PipelineDatei(m, p), DateiArt.Pipeline);
    }

    /// <summary>
    /// Pipeline: <c>partial class P : IPipelineHandler</c>, <c>PipelineId</c>, Konfig-Records per Konstruktor, je Handle
    /// <c>Handle(TEingang, PipelineContext, Fähigkeit…)</c> → <c>IAsyncEnumerable&lt;OneOf&lt;…&gt;&gt;</c> (Commands, Trigger, Selbst, Frist);
    /// ohne Ausgang <c>Task</c> (die geschlossene leere Menge — nur Effekte, wie bei Projektionen).
    /// </summary>
    private static string PipelineDatei(EditorModell m, PipelineKarte p)
    {
        var b = Kopf(p.Namespace, Usings(p.Namespace, m, [m.Rahmen.VertragsNamespace],
            KlassenTypen(p.Datei, p.Handles).Concat(p.Datei == null ? p.Konfigs : []), p.Usings));
        Doku(b, p.Doku, "");
        Attribute(b, p.Attribute);
        b.AppendLine($"{MitPartial(p.Typart)} {p.Name} : {string.Join(", ", p.Basen ?? [IPipelineHandler])}");
        b.AppendLine("{");
        var teile = new List<Action>();
        teile.Add(() =>
        {
            if (p.Zusatz is not null) { Eingerückt(b, p.Zusatz, "    "); return; }
            b.AppendLine($"    public string {PipelineId} => \"{p.PipelineId ?? p.Name}\";");
            if (p.Konfigs.Count == 0) return;
            b.AppendLine();
            foreach (var k in p.Konfigs) b.AppendLine($"    private readonly {k} _{BoardLeseseite.ParameterName(k)};");
            b.AppendLine();
            b.AppendLine($"    public {p.Name}({string.Join(", ", p.Konfigs.Select(k => $"{k} {BoardLeseseite.ParameterName(k)}"))})");
            b.AppendLine("    {");
            foreach (var k in p.Konfigs) b.AppendLine($"        _{BoardLeseseite.ParameterName(k)} = {BoardLeseseite.ParameterName(k)};");
            b.AppendLine("    }");
        });
        // Zwei Formen, nach der Ausgabemenge: keine ⇒ Task (geschlossen leer, nur Effekte); sonst IAsyncEnumerable<OneOf<…>> —
        //   der Pipeline-Dispatch erkennt nur OneOf, auch ein einzelner Ausgang steht darin.
        foreach (var h in p.Handles)
            teile.Add(() => HandleZeilen(b, h, [PipelineContext], ["ctx"],
                h.Rueckgabe ?? (h.Ausgaenge.Count == 0 ? "Task" : $"IAsyncEnumerable<{OneOf}<{string.Join(", ", h.Ausgaenge)}>>")));
        Teile(b, teile);
        b.AppendLine("}");
        return b.ToString();
    }

    private static string Ziel(EditorModell m, string ns, string name) =>
        Verzeichnis(m, ns) is { } v ? $"{v}/{name}" : $"{Unplatziert}{name}";

    private static GenerierteDatei Datei(string pfad, string inhalt, DateiArt art) =>
        new(pfad, inhalt, art, !pfad.StartsWith(Unplatziert, StringComparison.Ordinal));

    private static string StoreImplDatei(EditorModell m, Store st, StoreImpl impl, IReadOnlyList<Faehigkeit> fns, bool neu)
    {
        var basis = neu ? m.Rahmen.StoreBasis : null;
        var usings = new List<string>();
        if (basis != null) usings.Add(basis.Namespace);
        var b = Kopf(impl.Namespace, Usings(impl.Namespace, m, usings.Concat(basis?.Usings ?? []),
            fns.SelectMany(f => f.Parameter.Select(p => p.Typ).Append(f.Rueckgabe)).Append(st.Name), []));
        var basen = basis == null ? st.Name : $"{basis.Name}, {st.Name}";
        b.AppendLine($"public sealed partial class {impl.Name}" + (neu ? $" : {basen}" : ""));
        b.AppendLine("{");
        var zeilen = new List<string>();
        if (basis != null)
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

    /// <summary>
    /// Projektion/Reaktion. Null = der Schreiber-Typ der Handles ist aus dem Code nicht bekannt (keine Projektion im Code) —
    /// dann wird nicht geraten, sondern nicht geschrieben.
    /// </summary>
    private static string? KonsumentDatei(EditorModell m, Konsument k)
    {
        var r = m.Rahmen;
        if (r.ProjektionsSchreiber is null) return null;
        var basis = new List<string> { r.VertragsNamespace };
        if (r.ProjektionsSchreiberNamespace is { } sns) basis.Add(sns);
        var b = Kopf(k.Namespace, Usings(k.Namespace, m, basis, KlassenTypen(k.Datei, k.Handles), k.Usings));
        Doku(b, k.Doku, "");
        Attribute(b, k.Attribute);
        var basen = k.Basen ?? new[] { ISubscriber }.Concat(k.Pull ? [IPullSubscriber] : []).Concat(k.Append ? [IAppendProjektion] : []).ToList();
        b.AppendLine($"{MitPartial(k.Typart)} {k.Name} : {string.Join(", ", basen)}");
        b.AppendLine("{");
        var teile = new List<Action>();
        teile.Add(() => { if (k.Zusatz is null) b.AppendLine($"    public string {SubscriberId} => \"{k.SubscriberId ?? k.Name}\";"); else Eingerückt(b, k.Zusatz, "    "); });
        foreach (var h in k.Handles)
            teile.Add(() => HandleZeilen(b, h, [IAggregateEnvelope, r.ProjektionsSchreiber], ["envelope", "writer"],
                h.Rueckgabe ?? (h.Ausgaenge.Count == 0 ? "Task" : $"IAsyncEnumerable<{OneOfVon(h.Ausgaenge)}>")));
        Teile(b, teile);
        b.AppendLine("}");
        return b.ToString();
    }

    private static string LeserDatei(EditorModell m, Leser r)
    {
        var b = Kopf(r.Namespace, Usings(r.Namespace, m, [m.Rahmen.VertragsNamespace],
            KlassenTypen(r.Datei, r.Handles).Append(r.Projektion), r.Usings));
        Doku(b, r.Doku, "");
        b.AppendLine($"[{ProjectionReader}({TrackDeps} = {(r.TrackDeps ? "true" : "false")})]");
        Attribute(b, r.Attribute);
        b.AppendLine($"{MitPartial(r.Typart)} {r.Name} : {string.Join(", ", r.Basen ?? [$"{IReader}<{r.Projektion}>"])}");
        b.AppendLine("{");
        var teile = new List<Action>();
        if (r.Zusatz is not null) teile.Add(() => Eingerückt(b, r.Zusatz, "    "));
        // Ein Reader-Handle ohne Response hat keinen Vertrag (CQRS050) — nicht geschrieben (Validator meldet es).
        foreach (var h in r.Handles.Where(h => h.Rueckgabe != null || h.Ausgaenge.Count > 0))
            teile.Add(() => HandleZeilen(b, h, [IMessageEnvelope, ReadContext], ["envelope", "ctx"],
                h.Rueckgabe ?? $"Task<{OneOfVon(h.Ausgaenge)}>"));
        Teile(b, teile);
        b.AppendLine("}");
        return b.ToString();
    }

    /// <summary>
    /// Typen, aus denen die usings einer Konsumenten-/Reader-Datei ABGELEITET werden: bei einer NEUEN Klasse alle, bei einer
    /// bestehenden nur die der neuen Handles (die bestehenden usings stehen verbatim im Modell — keine neuen Mehrdeutigkeiten).
    /// </summary>
    private static IEnumerable<string> KlassenTypen(string? datei, IReadOnlyList<Handle> handles) =>
        handles.Where(h => datei == null || h.Datei == null)
            .SelectMany(h => h.Faehigkeiten.Select(f => f.Typ).Concat(h.Ausgaenge).Append(h.Eingang));

    private static void HandleZeilen(StringBuilder b, Handle h, string[] kontextTypen, string[] kontextNamen, string rueckgabe)
    {
        var ps = new List<string> { $"{h.Eingang} {h.Parameter}" };
        for (var i = 0; i < kontextTypen.Length; i++)
            ps.Add($"{kontextTypen[i]} {(h.Kontext is { } k && i < k.Count ? k[i] : kontextNamen[i])}");
        ps.AddRange(h.Faehigkeiten.Select(f => $"{f.Typ} {f.Name}"));
        // Neuer Handle mit Entwurf, der await benutzt ⇒ async (bestehende Modifizierer bleiben wörtlich).
        var asy = h.Rumpf is { } r && (r.Contains("await ") || rueckgabe.StartsWith("IAsyncEnumerable<", StringComparison.Ordinal) && r.Contains("yield "));
        var mods = h.Modifikatoren ?? (asy ? "public async" : "public");
        var kopf = $"    {mods} {rueckgabe} {HandleMethode}({string.Join(", ", ps)})";
        if (h.Ausdruck is not null)
        {
            b.AppendLine(kopf + " =>");
            b.AppendLine("        " + h.Ausdruck + ";");
            return;
        }
        b.AppendLine(kopf);
        b.AppendLine("    {");
        if (h.Rumpf is null) b.AppendLine($"        throw new NotImplementedException(\"TODO: {HandleMethode}({h.Eingang})\");");
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

    /// <summary>Die Deklarationsform mit <c>partial</c> vor dem Schlüsselwort (die Dispatch-Generatoren ergänzen die Klasse).</summary>
    private static string MitPartial(string? typart)
    {
        var teile = (typart ?? "public class").Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
        teile.Insert(teile.Count - 1, "partial");
        return string.Join(" ", teile);
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

        var menge = new SortedSet<string>(basis.Concat(explizit), StringComparer.Ordinal);
        foreach (var typ in typen)
            foreach (System.Text.RegularExpressions.Match m in Bezeichner.Matches(typ ?? ""))
                if (nsVon.TryGetValue(m.Value, out var andere) && andere != ns) menge.Add(andere);
        menge.Remove(ns);
        // Vertrags-Namespace zuerst (Hausstil), dann alphabetisch.
        return menge.OrderBy(u => u == modell.Rahmen.VertragsNamespace ? 0 : 1).ThenBy(u => u, StringComparer.Ordinal).ToList();
    }

    private static readonly System.Text.RegularExpressions.Regex Bezeichner = new(@"[A-Za-z_][A-Za-z0-9_]*");

    private static string Parameter(IReadOnlyList<Feld> felder) =>
        string.Join(", ", felder.Select(f =>
            $"{f.Typ} {f.Name}" + (f.Standard is null ? "" : $" = {f.Standard}")));

    private static string LambdaKopf(int anzahl)
    {
        string[] namen = ["t", "r", "g"];
        var teile = Enumerable.Range(0, Math.Max(1, anzahl))
            .Select(i => i < namen.Length ? namen[i] : $"e{i + 1}")
            .ToArray();
        return anzahl <= 1 ? teile[0] : "(" + string.Join(", ", teile) + ")";
    }

    private static string ArgListe(IReadOnlyList<string>? ausdruecke, string command, EditorModell modell)
    {
        if (ausdruecke is { Count: > 0 }) return string.Join(", ", ausdruecke);
        var record = modell.Records.FirstOrDefault(r => r.Name == command && r.Kind == RecordArt.Command);
        if (record is null) return "/* TODO: Argumente */";
        return string.Join(", ", record.Felder.Select(_ => "default"));
    }
}
