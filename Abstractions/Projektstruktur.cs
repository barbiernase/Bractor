#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;

namespace Abstractions;

/// <summary>
/// Die feste PROJEKT- UND ORDNERSTRUKTUR einer Anwendung (<c>docs/07-projektstruktur.md</c>) an EINER Stelle: welche Rollen-Projekte es gibt
/// (als Suffix zur Anwendungs-Wurzel), wie sie einander referenzieren, welche Primitive in welches Projekt gehört und wie ihre Datei heißt.
/// Generatoren, Analyzer (per Link, netstandard2.0), Codegen, Extractor, Scaffolder und Werkbank lesen sie von hier; <c>--check</c> und
/// CQRS098 prüfen sie (falscher Ort = Befund, nie still).
/// <para>Die Struktur sagt nur, WO etwas liegt. WAS ein Typ ist (die <see cref="Primitive"/>), bestimmen weiterhin Marker und Signaturen
/// (Leitsatz W2) — die Zuordnung Symbol → Primitive steht in <c>Domain.SourceGeneration/Lageart.cs</c>.</para>
/// <para>In den Generatoren (netstandard2.0, per Link) ist die Klasse <c>internal</c> — sonst sähe ein Projekt, das Abstractions UND
/// einen Generator referenziert (Prüfstand), sie doppelt.</para>
/// </summary>
#if NETSTANDARD2_0
internal
#else
public
#endif
static class Projektstruktur
{
    /// <summary>Die Rolle eines Projekts der Anwendung (docs/07 §7.2).</summary>
    public enum Rolle
    {
        /// <summary><c>&lt;Wurzel&gt;</c>: Aggregate, Commands, Events, Hinweise, Wertobjekte, Enums; Queries an Aggregate und ihre Antworten.</summary>
        Schreibseite,
        /// <summary><c>&lt;Wurzel&gt;.Projections</c>: ReadModels, Queries, Antworten, Store-Fähigkeiten, Projektionen, Reader.</summary>
        Leseseite,
        /// <summary><c>&lt;Wurzel&gt;.Pipeline</c>: Funktionen (Klassen im Host), Graphen, die Commands, die nur Knoten einander geben, Konfig-Records und
        /// Technik.</summary>
        Ablaeufe,
        /// <summary><c>&lt;Wurzel&gt;.Akteure</c>: Akteure und Clients — der äußerste Ring.</summary>
        Akteure,
        /// <summary><c>&lt;Wurzel&gt;.Infrastructure</c>: Store-Implementierungen.</summary>
        Speicher,
        /// <summary><c>&lt;Wurzel&gt;.Leitung</c>: die Domänen-DTOs der Leitung (<c>domain.proto</c>, vom Codegen) und ihr generierter Nutzlast-Mapper.</summary>
        Leitung,
        /// <summary><c>&lt;Wurzel&gt;.Laufzeit</c>: die Komposition — Framework + Rollen-Projekte, die generierten Laufzeit-Tabellen.</summary>
        Komposition,
        /// <summary>
        /// <c>&lt;Name&gt;.Katalog</c>: ein FUNKTIONSKATALOG (docs/plan-katalog-und-graphen.md) — Funktionen, ihre Nachrichten (Commands ohne Ziel,
        /// Hinweise), Konfig-Records, Technik und Graphen, domänenneutral: er referenziert nur Abstractions (und andere Kataloge), keine Anwendung.
        /// Keine Rolle EINER Anwendung: seine Wurzel ist sein eigener Name, die Abläufe einer Anwendung nutzen ihn per Referenz.
        /// </summary>
        Katalog,
    }

    /// <summary>Was ein Typ ist — bestimmt aus Code-Fakten (Lageart), nie aus Name oder Ordner.</summary>
    public enum Primitive
    {
        /// <summary>Ein Command an ein Aggregat (<c>ICommand&lt;T&gt;</c>) oder ein Command ohne Ziel, den ein <c>Decide</c> abgibt: bei der Schreibseite.</summary>
        Command,
        Aggregat, Decider, Applier, Event, Wertobjekt, Enum,
        /// <summary>Eine Query, die ein Aggregat beantwortet (<c>Frage</c> im Decider, docs/03 §3.2), und ihre Antwort: sie liegen
        /// bei der Schreibseite, denn der Decider sieht die Leseseite nicht. Eine Query an einen Reader ist <see cref="Query"/>.</summary>
        AggregatQuery, AggregatAntwort,
        ReadModel, Query, Antwort, StoreBuendel, StoreFaehigkeit, Projektion, Reader,
        /// <summary>Eine Funktion (Klasse mit <c>IFunktion</c>, Baustein-Form, <c>Auf</c>): sie läuft im Host — je Funktion eine eigene Datei.</summary>
        Funktion,
        Konfig,
        /// <summary>Ein Command ohne Ziel, den nur Knoten einander geben (kein <c>Decide</c> gibt ihn ab): bei den Abläufen bzw. im Katalog, in
        /// <c>Commands.cs</c>.</summary>
        KnotenCommand,
        /// <summary>Ein Hinweis (<c>ITransientEvent</c>, flüchtig, an Akteure): bei der Schreibseite, den Abläufen oder im Katalog — wer ihn
        /// ausgibt, entscheidet; in <c>Events.cs</c>.</summary>
        Hinweis,
        /// <summary>Ein Graph (<c>record … : IGraph</c>, Knoten als Parameter): bei den Abläufen, in <c>Graphen.cs</c>.</summary>
        Graph,
        /// <summary>Technik hinter einer Funktion (ohne Marker: was der Konstruktor einer Funktion nimmt, und ihre Umsetzungen) — bei den Abläufen,
        /// je Typ eine eigene Datei.</summary>
        Technik,
        /// <summary>Ein Akteur (<c>record … : IAkteur</c>) bzw. ein Client (<c>record … : IClient</c>): beim Rand, in <c>Akteure.cs</c> bzw. <c>Clients.cs</c>.</summary>
        Akteur, Client,
        StoreImplementierung,
        /// <summary>Kein Rollen-Marker: liegt im Bereichs-Ordner seines Projekts, in eigener Datei oder einer Sammeldatei des Projekts.</summary>
        Hilfscode,
    }

    /// <summary>Die Wurzel der Anwendung, die Codegen und Werkbank bearbeiten (die Beispielanwendung; eine Test-Anwendung hat eine eigene).</summary>
    public const string StandardWurzel = "Domain";

    /// <summary>Der Ordner der Umgebungen (relativ zur Solution, docs/07 §7.2, Leitsatz W7): je Umgebung ein Ordner <c>&lt;name&gt;</c> mit
    /// <see cref="UmgebungsDatei"/> und dem Ordner <see cref="GraphenOrdner"/> (je Instanz eines Graphen eine Datei).</summary>
    public const string UmgebungsOrdner = "umgebungen";

    /// <summary>Die Datei einer Umgebung in ihrem Ordner.</summary>
    public const string UmgebungsDatei = "umgebung.json";

    /// <summary>Der Ordner der Instanzen in einer Umgebung: je Instanz <c>&lt;instanz&gt;.json</c>.</summary>
    public const string GraphenOrdner = "graphen";

    /// <summary>Das Projekt, dessen Host die Werkbank baut und startet (⟳).</summary>
    public const string ServerHost = "Host.Grpc";

    /// <summary>Wohin der Codegen die STJ-Kontexte schreibt: der Ordner Serialization der Komposition (relativ zur Solution).</summary>
    public static string SerialisierungsOrdner(string wurzel) => Projekt(wurzel, Rolle.Komposition) + "/Serialization";

    /// <summary>Das Werkzeug-Projekt des Codegen-Prepass: es muss alle <see cref="CodegenProjekte"/> referenzieren (Build-Reihenfolge).</summary>
    public const string CodegenWerkzeug = "Cqrs.Codegen";

    /// <summary>Die Datei der Domänen-DTOs im Leitungs-Projekt der Anwendung (<see cref="Rolle.Leitung"/>), vom Codegen geschrieben.</summary>
    public const string ProtoDatei = "domain.proto";

    /// <summary>Das Python-Worker-Projekt der Anwendung (getragene Funktionen) und darin der Ordner der generierten Basen.</summary>
    public static string PythonWorker(string wurzel) => wurzel + ".Client.Worker.Python.ML";
    public const string PythonGeneriert = "domain_client/generated";

    // ── Rollen-Projekte ──────────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Ein Rollen-Projekt: Suffix zur Wurzel, ob es Bereichs-Ordner hat, welche Rollen es referenziert, seine Sammeldateien und ob es nur
    /// Generiertes trägt (Leitung, Komposition: nichts Handgeschriebenes, docs/07 §7.2).
    /// </summary>
    public sealed class Eintrag
    {
        public Eintrag(Rolle rolle, string suffix, bool mitBereich, Rolle[] referenziert, string[] sammeldateien, bool nurGeneriert = false)
        { Rolle = rolle; Suffix = suffix; MitBereich = mitBereich; Referenziert = referenziert; Sammeldateien = sammeldateien; NurGeneriert = nurGeneriert; }
        public Rolle Rolle { get; }
        public string Suffix { get; }
        public bool MitBereich { get; }
        public IReadOnlyList<Rolle> Referenziert { get; }
        public IReadOnlyList<string> Sammeldateien { get; }
        public bool NurGeneriert { get; }
    }

    /// <summary>Die Rollen in Referenz-Reihenfolge (unten → oben, docs/07 §7.2).</summary>
    public static readonly IReadOnlyList<Eintrag> Rollen = new[]
    {
        new Eintrag(Rolle.Schreibseite, "", true, new Rolle[0],
            new[] { "Commands.cs", "Events.cs", "ValueObjects.cs", "Enums.cs", "Queries.cs", "Responses.cs" }),
        new Eintrag(Rolle.Leseseite, ".Projections", true, new[] { Rolle.Schreibseite },
            new[] { "ReadModels.cs", "Queries.cs", "Responses.cs", "Enums.cs" }),
        new Eintrag(Rolle.Ablaeufe, ".Pipeline", true, new[] { Rolle.Schreibseite, Rolle.Leseseite },
            new[] { "Funktionen.cs", "Graphen.cs", "Commands.cs", "Konfigs.cs", "Enums.cs" }),
        new Eintrag(Rolle.Akteure, ".Akteure", false, new[] { Rolle.Schreibseite, Rolle.Leseseite, Rolle.Ablaeufe },
            new[] { "Akteure.cs", "Clients.cs" }),
        new Eintrag(Rolle.Speicher, ".Infrastructure", true, new[] { Rolle.Schreibseite, Rolle.Leseseite },
            new string[0]),
        new Eintrag(Rolle.Leitung, ".Leitung", false, new[] { Rolle.Schreibseite, Rolle.Leseseite, Rolle.Ablaeufe },
            new string[0], nurGeneriert: true),
        new Eintrag(Rolle.Komposition, ".Laufzeit", false,
            new[] { Rolle.Schreibseite, Rolle.Leseseite, Rolle.Ablaeufe, Rolle.Akteure, Rolle.Speicher, Rolle.Leitung },
            new string[0], nurGeneriert: true),
    };

    /// <summary>
    /// Ein Funktionskatalog (<see cref="Rolle.Katalog"/>): <c>&lt;Name&gt;.Katalog</c> mit Bereichs-Ordnern; er referenziert keine Rolle einer
    /// Anwendung. Nicht in <see cref="Rollen"/> — die Rollen-Projekte EINER Anwendung (<see cref="Projekte"/>) kennen ihn nicht, eine Anwendung
    /// nutzt beliebig viele Kataloge.
    /// </summary>
    public static readonly Eintrag Katalog = new Eintrag(Rolle.Katalog, ".Katalog", true, new Rolle[0],
        new[] { "Commands.cs", "Events.cs", "ValueObjects.cs", "Enums.cs", "Graphen.cs", "Konfigs.cs" });

    /// <summary>Was ein Projekt außer Abstractions (und anderen Katalogen) referenzieren darf, wenn es ein Katalog ist: das Framework (<c>Core</c>
    /// mit den Quellen des Framework-Katalogs).</summary>
    public static readonly IReadOnlyList<string> KatalogDarfReferenzieren = new[] { "Abstractions", "Core" };

    /// <summary>
    /// Der allgemeine Katalog (docs/konzept-funktionen-im-editor.md §2): ein Katalog wie jeder andere, der die Commands hält, die mehrere Kataloge
    /// teilen; jeder Katalog darf ihn nutzen. Die Werkbank legt ihn beim ersten Gebrauch an.
    /// </summary>
    public const string AllgemeinerKatalog = "Allgemein.Katalog";

    /// <summary>
    /// Die Rollen einer Anwendung, die Kataloge nutzen dürfen (alle über den Abläufen, dazu der Speicher: er setzt die Fähigkeiten um, die ein Katalog
    /// zum Lesen deklariert); Schreib- und Leseseite kennen keine Funktionsnachricht.
    /// </summary>
    public static readonly IReadOnlyList<Rolle> NutzenKataloge = new[] { Rolle.Ablaeufe, Rolle.Akteure, Rolle.Speicher, Rolle.Leitung, Rolle.Komposition };

    public static Eintrag Von(Rolle r) => r == Rolle.Katalog ? Katalog : Rollen.First(e => e.Rolle == r);

    /// <summary>
    /// Die Kataloge, die eine Projektdatei referenziert (<c>&lt;ProjectReference Include="..\X.Katalog\X.Katalog.csproj" /&gt;</c> → <c>X.Katalog</c>) —
    /// gelesen aus dem Text, wie MSBuild ihn hat: so wissen Codegen und Werkbank, welche Kataloge eine Anwendung nutzt, ohne sie zu bauen.
    /// </summary>
    public static IEnumerable<string> KatalogeIn(string projektdatei) =>
        System.Text.RegularExpressions.Regex.Matches(projektdatei, @"<ProjectReference\s+Include=""([^""]+)""")
            .Cast<System.Text.RegularExpressions.Match>()
            .Select(m => System.IO.Path.GetFileNameWithoutExtension(m.Groups[1].Value.Replace('\\', '/').Split('/').Last()))
            .Where(IstKatalog).Distinct(StringComparer.Ordinal);

    /// <summary>Ist <paramref name="projekt"/> ein Funktionskatalog (<c>&lt;Name&gt;.Katalog</c>)?</summary>
    public static bool IstKatalog(string projekt) =>
        projekt.EndsWith(Katalog.Suffix, StringComparison.Ordinal) && projekt.Length > Katalog.Suffix.Length;

    /// <summary>Der Katalog, in dessen Projekt der Namespace <paramref name="ns"/> liegt (<c>Bild.Katalog.Skalierung</c> → <c>Bild.Katalog</c>), sonst <c>null</c>.</summary>
    public static string? KatalogVon(string ns)
    {
        for (var i = ns.IndexOf(Katalog.Suffix, StringComparison.Ordinal); i > 0; i = ns.IndexOf(Katalog.Suffix, i + 1, StringComparison.Ordinal))
        {
            var ende = i + Katalog.Suffix.Length;
            if (ende == ns.Length || ns[ende] == '.') return ns.Substring(0, ende);
        }
        return null;
    }

    /// <summary>Der Projektname einer Rolle: Wurzel + Suffix.</summary>
    public static string Projekt(string wurzel, Rolle r) => wurzel + Von(r).Suffix;

    /// <summary>Alle Rollen-Projekte einer Anwendung (Referenz-Reihenfolge).</summary>
    public static IEnumerable<string> Projekte(string wurzel) => Rollen.Select(e => wurzel + e.Suffix);

    /// <summary>
    /// Die Rollen-Projekte mit handgeschriebenem Code (ohne Leitung und Komposition): über sie läuft der Stand der Werkbank. Was der Build
    /// selbst erzeugt (Proto, STJ-Kontexte), darf den Stand nicht verschieben — sonst wechselte er durch das Bauen (Spur-Stand ≠ Board-Stand).
    /// </summary>
    public static IEnumerable<string> HandgeschriebeneProjekte(string wurzel) =>
        Rollen.Where(e => !e.NurGeneriert).Select(e => wurzel + e.Suffix);

    /// <summary>Die Projekte, aus denen der Codegen Proto, STJ-Kontexte und Python-Basen erzeugt (alles außer den Store-Implementierungen).</summary>
    public static IEnumerable<string> CodegenProjekte(string wurzel) =>
        new[] { Rolle.Schreibseite, Rolle.Leseseite, Rolle.Ablaeufe, Rolle.Akteure }.Select(r => Projekt(wurzel, r));

    /// <summary>
    /// Zerlegt einen Assembly-/Projektnamen in Wurzel und Rolle über das längste passende Suffix; ohne Suffix ist es die Schreibseite der
    /// Wurzel <paramref name="name"/>. Ob das Projekt zu einer Anwendung gehört, entscheidet der Aufrufer (bekannte Wurzel, Domänen-Generator).
    /// </summary>
    public static (string Wurzel, Rolle Rolle) Zerlege(string name)
    {
        foreach (var e in Rollen.Append(Katalog).Where(e => e.Suffix.Length > 0).OrderByDescending(e => e.Suffix.Length))
            if (name.EndsWith(e.Suffix, StringComparison.Ordinal) && name.Length > e.Suffix.Length)
                return (name.Substring(0, name.Length - e.Suffix.Length), e.Rolle);
        return (name, Rolle.Schreibseite);
    }

    /// <summary>
    /// Die Domäne eines Namespace bei den Abläufen (docs/konzept-funktionen-im-editor.md §4): <c>&lt;Wurzel&gt;.Pipeline.&lt;Bereich&gt;</c> →
    /// <c>&lt;Wurzel&gt;.&lt;Bereich&gt;</c> (der Bereich der Schreibseite derselben Wurzel) — ein Ablauf gehört der Domäne, in deren Bereich er liegt.
    /// Ohne Bereich oder außerhalb der Abläufe <c>null</c>.
    /// </summary>
    public static string? DomaeneDerAblaeufe(string ns)
    {
        var suffix = Von(Rolle.Ablaeufe).Suffix + ".";
        var i = (ns ?? "").IndexOf(suffix, StringComparison.Ordinal);
        if (i <= 0 || KatalogVon(ns!) != null) return null;
        return ns!.Substring(0, i) + "." + ns.Substring(i + suffix.Length);
    }

    /// <summary>Ist <paramref name="projekt"/> ein Rollen-Projekt der Anwendung <paramref name="wurzel"/>?</summary>
    public static bool IstProjekt(string wurzel, string projekt) => Projekte(wurzel).Contains(projekt, StringComparer.Ordinal);

    // ── Primitive → Rolle und Datei ──────────────────────────────────────────────────────────────────────────────────────

    /// <summary>In welchen Rollen-Projekten eine Primitive liegen darf (genau eines, außer Enum und Hilfscode).</summary>
    public static IReadOnlyList<Rolle> RollenVon(Primitive p)
    {
        switch (p)
        {
            case Primitive.Aggregat: case Primitive.Decider: case Primitive.Applier: case Primitive.Command: case Primitive.Event:
            case Primitive.AggregatQuery: case Primitive.AggregatAntwort:
                return new[] { Rolle.Schreibseite };
            case Primitive.Wertobjekt:
                return new[] { Rolle.Schreibseite, Rolle.Katalog };
            case Primitive.Hinweis:
                return new[] { Rolle.Schreibseite, Rolle.Ablaeufe, Rolle.Katalog };
            case Primitive.StoreFaehigkeit:
                // Eine Fähigkeit deklariert die Leseseite für ihre Projektionen und Reader — oder ein Katalog für seine Funktionen (mit seinen eigenen
                //   Typen); umgesetzt wird sie im Speicher der Anwendung (docs/plan-ohne-pipeline.md §2.3).
                return new[] { Rolle.Leseseite, Rolle.Katalog };
            case Primitive.ReadModel: case Primitive.Query: case Primitive.Antwort: case Primitive.StoreBuendel:
            case Primitive.Projektion: case Primitive.Reader:
                return new[] { Rolle.Leseseite };
            case Primitive.Funktion: case Primitive.KnotenCommand: case Primitive.Graph:
            case Primitive.Konfig: case Primitive.Technik:
                return new[] { Rolle.Ablaeufe, Rolle.Katalog };
            case Primitive.Akteur: case Primitive.Client:
                return new[] { Rolle.Akteure };
            case Primitive.StoreImplementierung:
                return new[] { Rolle.Speicher };
            case Primitive.Enum:
                return new[] { Rolle.Schreibseite, Rolle.Leseseite, Rolle.Ablaeufe, Rolle.Katalog };
            default:
                return Rollen.Select(e => e.Rolle).Append(Rolle.Katalog).ToArray();
        }
    }

    /// <summary>Die Sammeldatei einer Primitive (<c>Commands.cs</c> …), oder <c>null</c>, wenn der Typ eine eigene Datei hat.</summary>
    public static string? Sammeldatei(Primitive p)
    {
        switch (p)
        {
            case Primitive.Command: case Primitive.KnotenCommand: return "Commands.cs";
            case Primitive.Event: case Primitive.Hinweis: return "Events.cs";
            case Primitive.Graph: return "Graphen.cs";
            case Primitive.Wertobjekt: return "ValueObjects.cs";
            case Primitive.Enum: return "Enums.cs";
            case Primitive.ReadModel: return "ReadModels.cs";
            case Primitive.Query: case Primitive.AggregatQuery: return "Queries.cs";
            case Primitive.Antwort: case Primitive.AggregatAntwort: return "Responses.cs";
            case Primitive.Konfig: return "Konfigs.cs";
            case Primitive.Akteur: return "Akteure.cs";
            case Primitive.Client: return "Clients.cs";
            default: return null;
        }
    }

    /// <summary>
    /// Der Dateiname einer NEUEN Primitive (docs/07 §7.4): die Sammeldatei ihrer Art, sonst <c>{Typ}.cs</c>; Decide- und Apply-Methoden eines
    /// Aggregats stehen in <c>{Aggregat}.Decider.cs</c>/<c>{Aggregat}.Applier.cs</c> (<paramref name="typ"/> = Aggregat-Name).
    /// </summary>
    public static string Datei(Primitive p, string typ) =>
        p == Primitive.Decider ? typ + "." + Aggregatvertrag.Decider + ".cs"
        : p == Primitive.Applier ? typ + "." + Aggregatvertrag.Applier + ".cs"
        : Sammeldatei(p) ?? typ + ".cs";

    /// <summary>Der Namespace eines Typs im Bereich <paramref name="bereich"/> eines Projekts: Projekt + Ordner (ohne Bereich: das Projekt).</summary>
    public static string Namespace(string projekt, string? bereich) => string.IsNullOrEmpty(bereich) ? projekt : projekt + "." + bereich;

    /// <summary>Der Bereich eines Namespace im Projekt (<c>Domain.Pipeline.ImagePair</c> in <c>Domain.Pipeline</c> → <c>ImagePair</c>); sonst <c>null</c>.</summary>
    public static string? BereichAus(string projekt, string ns) =>
        ns.StartsWith(projekt + ".", StringComparison.Ordinal) && ns.IndexOf('.', projekt.Length + 1) < 0 ? ns.Substring(projekt.Length + 1) : null;

    // ── Lage-Prüfung (eine Regel für --check und CQRS098) ────────────────────────────────────────────────────────────────

    /// <summary>
    /// Liegt eine Primitive im Projekt ihrer Rolle? Liefert den Befund oder <c>null</c>. <paramref name="projekt"/> muss ein Rollen-Projekt
    /// der Wurzel sein (sonst prüft der Aufrufer das Projekt selbst).
    /// </summary>
    public static string? PruefeProjekt(string wurzel, string projekt, Primitive p, string typ)
    {
        var rolle = Zerlege(projekt).Rolle;
        var erlaubt = RollenVon(p);
        if (erlaubt.Contains(rolle)) return null;
        if (rolle == Rolle.Katalog)
            return $"'{typ}' ist {Text(p)} und gehört nicht in einen Katalog ({projekt}) — ein Katalog kennt keine Domäne; {Text(p)} liegt in einer "
                + $"Anwendung ({string.Join(" oder ", erlaubt.Select(r => "<Wurzel>" + Von(r).Suffix))}, docs/07 §7.2)";
        // In einem Projekt der Anwendung: das Rollen-Projekt der Anwendung (ein Katalog ist eine eigene Entscheidung, keine Korrektur der Lage).
        return $"'{typ}' ist {Text(p)} und gehört nach {string.Join(" oder ", erlaubt.Where(r => r != Rolle.Katalog).Select(r => Projekt(wurzel, r)))}, "
            + $"nicht nach {projekt} (docs/07 §7.2)";
    }

    /// <summary>
    /// Ordner, Namespace und Datei eines Typs im Projekt (docs/07 §7.3/§7.4). <paramref name="relativerPfad"/> ist der Pfad der Datei relativ
    /// zum Projektverzeichnis mit '/' (<c>ImagePair/Events.cs</c>); <paramref name="aggregat"/> ist bei Decider/Applier der Aggregat-Name;
    /// <paramref name="buendel"/> sind bei einer Store-Fähigkeit die Bündel, die sie enthalten (sie steht in der Datei eines davon).
    /// </summary>
    public static IEnumerable<string> PruefeOrt(string projekt, string relativerPfad, string ns, Primitive p, string typ, string? aggregat,
        IEnumerable<string>? buendel = null)
    {
        var e = Von(Zerlege(projekt).Rolle);
        var teile = relativerPfad.Split('/');
        var datei = teile[teile.Length - 1];
        var bereich = teile.Length == 2 ? teile[0] : null;
        if (e.MitBereich ? teile.Length != 2 : teile.Length != 1)
            yield return $"'{typ}' liegt in {projekt}/{relativerPfad} — erwartet " + (e.MitBereich ? $"{projekt}/<Bereich>/<Datei>" : $"{projekt}/<Datei>") + " (docs/07 §7.3)";
        else if (ns != Namespace(projekt, bereich))
            yield return $"'{typ}' hat den Namespace {ns}, liegt aber in {projekt}/{relativerPfad} — Namespace = Projekt + Ordner: {Namespace(projekt, bereich)} (docs/07 §7.3)";

        string[] erwartet;
        if (p == Primitive.Decider || p == Primitive.Applier) erwartet = new[] { Datei(p, aggregat ?? typ) };
        else if (p == Primitive.StoreFaehigkeit) erwartet = (buendel ?? Enumerable.Empty<string>()).Select(b => b + ".cs").Concat(new[] { typ + ".cs" }).ToArray();
        else if (Sammeldatei(p) is { } s) erwartet = new[] { s };
        else if (p == Primitive.Hilfscode) erwartet = e.Sammeldateien.Concat(new[] { typ + ".cs" }).ToArray();
        else erwartet = new[] { typ + ".cs" };
        var eigene = p != Primitive.Decider && p != Primitive.Applier && p != Primitive.StoreFaehigkeit && Sammeldatei(p) == null;
        if (!erwartet.Contains(datei, StringComparer.Ordinal) && !(eigene && datei.StartsWith(typ + ".", StringComparison.Ordinal)))
            yield return $"'{typ}' ({Text(p)}) steht in {datei} — erwartet {string.Join(" oder ", erwartet)} (docs/07 §7.4)";
    }

    /// <summary>Lesbarer Name einer Primitive für Befunde.</summary>
    public static string Text(Primitive p)
    {
        switch (p)
        {
            case Primitive.Aggregat: return "ein Aggregat";
            case Primitive.Decider: return "ein Decider";
            case Primitive.Applier: return "ein Applier";
            case Primitive.Command: return "ein Command";
            case Primitive.Event: return "ein Event";
            case Primitive.Wertobjekt: return "ein Wertobjekt";
            case Primitive.Enum: return "ein Enum";
            case Primitive.ReadModel: return "ein ReadModel";
            case Primitive.Query: return "eine Query";
            case Primitive.Antwort: return "eine Antwort";
            case Primitive.AggregatQuery: return "eine Query an ein Aggregat";
            case Primitive.AggregatAntwort: return "eine Antwort eines Aggregats";
            case Primitive.StoreBuendel: return "ein Store-Bündel";
            case Primitive.StoreFaehigkeit: return "eine Store-Fähigkeit";
            case Primitive.Projektion: return "eine Projektion";
            case Primitive.Reader: return "ein Reader";
            case Primitive.Funktion: return "eine Funktion";
            case Primitive.KnotenCommand: return "ein Command zwischen Knoten";
            case Primitive.Hinweis: return "ein Hinweis";
            case Primitive.Graph: return "ein Graph";
            case Primitive.Technik: return "Technik";
            case Primitive.Konfig: return "ein Konfig-Record";
            case Primitive.Akteur: return "ein Akteur";
            case Primitive.Client: return "ein Client";
            case Primitive.StoreImplementierung: return "eine Store-Implementierung";
            default: return "Hilfscode";
        }
    }
}
