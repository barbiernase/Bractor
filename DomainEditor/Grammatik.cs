namespace DomainEditor;

/// <summary>
/// Die GRAMMATIK der Kompositions-Sprache (docs/10) als EINE Quelle: welche Nachrichtensorte
/// in welchen Baustein-Eingang darf (mit Kardinalität), welcher Baustein welche Sorten erzeugen darf, und die Zusatzregeln — je
/// Regel mit ihrem Gegenstück im Build (Analyzer-/Generator-ID, Compiler, Boot-Guard oder „offen").
///
/// Sie ist DATEN (Werkbank P7.4, „Prüfen = Bauen“: geprüft wird im Build, nicht hier) und speist zwei Stellen:
///   • den Editor (GraphExtractor legt sie als <c>rahmen.grammatik</c> ins Board; ⊕ fragt den Server — GraphExtractor/Sicht.Passt —, der nur
///     gültige Ziele anbietet; der Editor zeigt die Tabelle nur an),
///   • die Liste „Regel → Build-Gegenstück" (<c>--grammatik</c>; <c>--check</c> prüft, dass jede genannte ID im Code existiert).
///
/// Leitsatz: <b>Wenn es sich verbinden lässt, kompiliert es.</b> Die Grammatik darf enger sein als der Compiler, nie weiter.
/// Rein, Roslyn-frei, ohne Domänen-Namen.
/// </summary>
public static class Grammatik
{
    // ── Alphabet: Nachrichtensorten (die Kanten) ──────────────────────────────────────────────────────────────────────
    public const string Command = "command", Event = "event", Transient = "transient", Frist = "frist", Query = "query", Response = "response",
        Faehigkeit = "faehigkeit", Quelle = "quelle", Gerufen = "gerufen";

    // ── Alphabet: Bausteine (die Knoten) ─────────────────────────────────────────────────────────────────────────────
    public const string Aggregat = "aggregat", Projektion = "projektion", Reader = "reader", Store = "store", Aussenwelt = "aussenwelt",
        Akteur = "akteur", Client = "client", Funktion = "funktion", Uhr = "uhr";

    /// <summary>Kardinalität eines Konsum-Eingangs.</summary>
    public const string GenauEins = "eins", Beliebig = "beliebig";

    public sealed record SorteInfo(string Id, string Name, string Symbol, string Garantie, string Bedeutung);
    public sealed record BausteinInfo(string Id, string Name, bool Zustand);
    /// <summary>Nachricht der Sorte <see cref="Sorte"/> darf in einen Eingang des Bausteins <see cref="Baustein"/> (Kardinalität je Nachricht).</summary>
    public sealed record Konsum(string Sorte, string Baustein, string Kardinalitaet, string Regel);
    /// <summary>Der Baustein darf Nachrichten dieser Sorte erzeugen (Ausgang).</summary>
    public sealed record Erzeugung(string Baustein, string Sorte, string Regel);
    /// <summary>
    /// Das Gegenstück einer Regel im Build: Art (analyzer/generator/compiler/boot/laufzeit/test/offen; test = ein allgemeiner Test, den jede
    /// Implementierung besteht), Kennung (z. B. CQRS010), Ort.
    /// </summary>
    public sealed record Gegenstueck(string Art, string? Kennung, string Ort);
    public sealed record Regel(string Id, string Name, string Text, string Schwere, IReadOnlyList<Gegenstueck> Build);

    public static readonly IReadOnlyList<SorteInfo> Sorten =
    [
        new(Command, "Command", "▶", "wirkt genau einmal (Command-Id, Inbox)", "Absicht an genau einen Empfänger: ein Aggregat (Ziel im Typ), einen Knoten desselben Graphen, den Kopf eines Graphen oder einen Akteur (Arbeit)"),
        new(Event, "Event", "◆", "durabel; wirkt bei jedem Hörer genau einmal", "Fakt im Log"),
        new(Transient, "Hinweis", "◇", "höchstens einmal (verlierbar)", "flüchtig: Ablehnung, Fortschritt, Live-Anzeige (ITransientEvent)"),
        new(Frist, "Frist", "⏳", "durabel; feuert mindestens einmal, wirkt genau einmal", "Zeit aus dem Decide eines Aggregats — die Uhr macht daraus den Command an dasselbe Aggregat"),
        new(Query, "Query", "?", "synchron, wirkt nicht", "Frage"),
        new(Response, "Antwort", "↩", "synchron; trägt den Stand der gezeigten Ids", "Rückgabe eines Readers oder eines Aggregats (dann mit seiner Version als OCC-Stand)"),
        new(Faehigkeit, "Fähigkeit", "⚙", "als Parameter von Auf bzw. Frage", "Store-Funktion (Lesen/Schreiben)"),
        new(Gerufen, "Technik", "🧩", "im Konstruktor (gebunden nach der Umgebung)",
            "Technik hinter einer Funktion (ohne Marker: was ihr Konstruktor nimmt) — keine Nachricht, eine Abhängigkeit, läuft im Prozess ihres Halters"),
    ];

    public static readonly IReadOnlyList<BausteinInfo> Bausteine =
    [
        new(Aggregat, "Aggregat", true), new(Projektion, "Projektion", false), new(Reader, "Reader", false),
        new(Store, "Store", true), new(Aussenwelt, "Außenwelt", false),
        new(Akteur, "Akteur", false), new(Client, "Client", false), new(Funktion, "Funktion", false),
        // Eingebaut: die Uhr sendet fällige Fristen als Command an das Aggregat, das sie setzte.
        new(Uhr, "Uhr", false),
    ];

    // ── Regeln (Id = der Name, unter dem Build-Diagnosen und Editor sie nennen) ──────────────────────────────────────────────
    private static Gegenstueck An(string id, string ort) => new("analyzer", id, ort);
    private static Gegenstueck Gen(string id, string ort) => new("generator", id, ort);
    private static Gegenstueck Comp(string ort) => new("compiler", null, ort);
    private static Gegenstueck Offen(string ort) => new("offen", null, ort);

    public static readonly IReadOnlyList<Regel> Regeln =
    [
        // Konsum: Sorte → Eingang
        new("GR-COMMAND", "Command → genau ein Empfänger", "Ein Command hat genau einen Empfänger, vom Absender aus: nennt er sein Ziel im Typ "
            + "(ICommand<TAggregat> mit Id<TAggregat> AggregateId), entscheidet ihn genau dieses Aggregat; ohne Typargument (ICommand) bekommt ihn der "
            + "Knoten desselben Graphen, der ihn nimmt, sonst der Kopf genau eines Graphen oder ein Akteur, der ihn hört (Arbeit). Kein Empfänger, mehrere, oder "
            + "ein Typ, den ein Graph nimmt und ein Akteur hört, ist ein Fehler.", "error",
            [Comp("ICommand<TAggregat> liefert das Ziel aus Id<TAggregat> AggregateId (IAggregatCommand)"),
             An("CQRS013", "Domain.SourceGeneration/ModellAnalyzer.cs (Ziel nicht im Typ: IAggregatCommand von Hand, AggregateId ohne Typargument)"),
             Gen("CQRS010", "Infrastructure.SourceGeneration/CommandAggregateMapGenerator.cs (Decide außerhalb des Ziel-Aggregats)"),
             An("CQRS086", "Infrastructure.SourceGeneration/GesamtbildAnalyzer.cs (Command ohne Decider bzw. ohne Knoten, der ihn nimmt)"),
             An("CQRS116", "Infrastructure.SourceGeneration/GesamtbildAnalyzer.cs (Command ohne Ziel ohne bzw. mit mehreren Empfängern)")]),
        new("GR-EVENT", "Event → Projektionen, Akteure, das eigene Aggregat", "Ein persistentes Event hören Projektionen (beliebig viele) und Akteure zur "
            + "Kenntnis (IHoert, gepusht an ihre Sitzungen); gefaltet wird es im eigenen Aggregat. Eine Funktion liest nie das Log: sie hört ein Event nur "
            + "als Antwort auf einen eigenen Command (docs/04 §4.4).", "error",
            [Comp("Auf-Parameter : IEvent"), An("CQRS057", "Domain.SourceGeneration/HandlerFormAnalyzer.cs (Auf einer Projektion)"),
             Gen("CQRS100", "Infrastructure.SourceGeneration/FunktionsBausteinGenerator.cs (eine Funktion, die nur Events hört, hat keinen Einstieg)")]),
        new("GR-DECIDE-COMMAND", "Command ohne Ziel aus dem Decide → Graph-Kopf oder Akteur", "Ein Decide kann neben Events einen Command ohne Ziel "
            + "ausgeben, nie einen an ein Aggregat. Er geht nicht ins Log; für seinen Empfänger (die Instanz, deren Graph ihn am Kopf nimmt, oder den Akteur, "
            + "der ihn als Arbeit hört) steht in derselben Transaktion ein offener Eingang (Kennung aus Command und Stelle), den die Laufzeit zustellt, bis "
            + "der Lauf bzw. die Arbeit fertig ist — genau einmal wirksam (docs/03 §3.2, docs/04 §4.4).", "error",
            [An("CQRS050", "Domain.SourceGeneration (Ausgabe-Vertrag: Commands ohne Ziel stehen in der Signatur)"),
             An("CQRS116", "Infrastructure.SourceGeneration/GesamtbildAnalyzer.cs (genau ein Empfänger: Graph-Kopf oder Akteur)"),
             new("laufzeit", null, "Infrastructure/Eingaenge/Folgen.cs (offene Eingänge im Commit, schneller Weg danach)")]),
        new("GR-ANTWORT", "Antwort → die Funktion, die den Command schickte", "Ein Event hört eine Funktion nur als Antwort auf einen eigenen Command: "
            + "die Antwort trägt die Events, die das Ziel entschied, und das Kind rechnet sie gleich weiter. Ein Event-Eingang, den keiner ihrer Commands "
            + "entscheiden kann, käme nie (docs/04 §4.4).", "error",
            [An("CQRS108", "Infrastructure.SourceGeneration/GesamtbildAnalyzer.cs (Antwort ohne eigenen Command)"),
             new("laufzeit", null, "Infrastructure/Funktionen/FunktionsKindActor.cs (Antwort aus CommandResult.Antwort weitergerechnet)")]),
        new("GR-KANTE", "Command ohne Ziel → Knoten desselben Graphen", "Eine Funktion gibt Commands und Hinweise aus, nie ein Event. Einen Command ohne "
            + "Ziel bekommt jeder Knoten desselben Graphen, der ihn nimmt — die Kante folgt aus den Typen, geschrieben wird sie nie (docs/04 §4.4).", "error",
            [Gen("CQRS100", "Infrastructure.SourceGeneration/FunktionsBausteinGenerator.cs (Ausgabe einer Funktion)"),
             new("generator", null, "Infrastructure.SourceGeneration/GraphGenerator.cs (GeneratedGraphen: Ports mit ihren Erzeugern aus den Typen)"),
             new("laufzeit", null, "Infrastructure/Funktionen/Lauf.cs (die Ausgaben eines Aufrufs gehen an die Ports seiner Abnehmer)")]),
        new("GR-GRAPH", "Graph: Knoten als Parameter, Kanten aus den Typen", "Ein Graph ist ein Record mit IGraph; jeder Parameter ist ein Knoten (eine "
            + "Funktion — eine generische mit Typargument — oder ein anderer Graph; Name = Konfig-Schlüssel, in einem inneren Graphen mit Punkt). Ein Graph "
            + "im Graphen ist eine Schachtel: hinein geht nur, was er nimmt und innen keiner erzeugt, heraus nur, was er erzeugt und innen keiner nimmt (seine "
            + "Signatur). Eingänge, die kein Knoten erzeugt, sind die Köpfe — je Eingangstyp genau einer; der Graph ist kreisfrei. Kein "
            + "Builder, keine ausdrücklichen Kanten, keine Knotenarten (docs/04 §4.4).", "error",
            [An("CQRS115", "Domain.SourceGeneration/GraphAnalyzer.cs (Form des Graphen, Lesart Graphform)"),
             An("CQRS117", "Infrastructure.SourceGeneration/GesamtbildAnalyzer.cs (eine Funktion in keinem Graphen läuft nie)")]),
        new("GR-JOIN", "Ports: T, T?, OneOf, IReadOnlyList", "Die Command-Parameter eines Auf sind seine Ports, und ihr Typ sagt, was er erwartet: T genau "
            + "einen je Lauf (warten), T? höchstens einen (null, wenn keiner kommt oder das Zeitlimit des Knotens um ist), OneOf<A, B> den ersten (die "
            + "übrigen Zweige brechen ab), IReadOnlyList<T> alle aus den Unterläufen eine Ebene tiefer (ein Strom öffnet je Element einen). Alle Ports eines "
            + "Auf liegen auf einer Ebene; vom nächsten gemeinsamen Vorfahren aus ist ein T-Port hinter einer Wahl T? oder Teil eines OneOf — der Befund nennt "
            + "den passenden Typ (docs/04 §4.4).", "error",
            [Gen("CQRS100", "Infrastructure.SourceGeneration/FunktionsBausteinGenerator.cs (Port-Formen, mehrere Ports nur mit Commands ohne Ziel)"),
             An("CQRS115", "Domain.SourceGeneration/GraphAnalyzer.cs (Ebenen und Vielfachheit, Lesart Graphform)")]),
        new("GR-LAUF", "Ein Lauf je Kopf", "Jede Nachricht im Graphen gehört zu einem Lauf, den eine Kopf-Nachricht startet; Läufe verschiedener Köpfe "
            + "kommen nie zusammen. Fertig ist der Kopf, wenn jeder Aufruf fertig ist oder nie kommt; wirft ein Knoten auch nach seinen Wiederholungen, "
            + "scheitert der Lauf: offene Zweige brechen ab, der Kopf wird geparkt, LaufGescheitert geht an die Hörer (docs/04 §4.4).", "error",
            [new("laufzeit", null, "Infrastructure/Funktionen/GraphActor.cs (Koordinator der Läufe einer Instanz)"),
             new("laufzeit", null, "Infrastructure/Funktionen/Lauf.cs (Buchführung: Pfade, Ports, Sammeln, Rennen, Zeitlimit)")]),
        new("GR-INSTANZ", "Graph läuft als Instanz", "Ein Graph läuft erst als Instanz: eine Datei je Instanz (umgebungen/<name>/graphen/<instanz>.json) "
            + "mit Graph, bei einem Quellkopf dem Akteur (er darf, was den Graphen verlässt) und je Knoten Parallel, Warteschlange, Wiederholungen, Zeitlimit, "
            + "Technik und die Felder seiner Konfig-Records. Ein Graph mit Command-Kopf läuft je Umgebung genau einmal (docs/04 §4.4).", "error",
            [new("boot", null, "Infrastructure/Funktionen/Graph.cs (GraphInstanzen.Aus: Graph, Knoten, Akteur ∩ IDarf, Command-Kopf einmal)"),
             new("boot", null, "Infrastructure/Funktionen/Technik.cs (BindeTechnik: Werte der Konfig-Records, Technik-Wahl je Knoten)")]),
        new("GR-FALTUNG", "Event → genau ein Aggregat faltet", "Ein Event faltet genau ein Aggregat (Applier) — das, das es entscheidet.", "warning",
            [An("CQRS080", "Domain.SourceGeneration/ModellAnalyzer.cs (Apply ohne Decide)"), An("CQRS079", "Domain.SourceGeneration/ModellAnalyzer.cs (Decide ohne Apply)")]),
        new("GR-TRANSIENT", "Hinweis → Akteur draußen", "Einen Hinweis (ITransientEvent: Ablehnung, Fortschritt) geben Aggregate und Funktionen aus; "
            + "hören tun ihn nur Akteure draußen (IHoert, über die Verteiler an ihre Sitzungen, höchstens einmal) — nie eine Funktion, nie eine Projektion, "
            + "nie ein Aggregat. Was ein Knoten braucht, ist ein Command; ist eine Ablehnung für einen Ablauf wichtig, ist sie ein Event.", "error",
            [Gen("CQRS100", "Infrastructure.SourceGeneration/FunktionsBausteinGenerator.cs (ein Hinweis ist kein Port einer Funktion)"),
             new("laufzeit", null, "Infrastructure/Funktionen/Hinweise.cs (aus einem Graphen nur an die Verteiler, ohne Log und Broker)")]),
        new("GR-WECKPLAN", "Weckplan: jeder Hörer mit jedem gehörten Typ", "Nach jedem Commit weckt der Wecker gezielt, wen der generierte Weckplan "
            + "zum Typ eines geschriebenen Events nennt: die Partition jeder Projektion, die ihn hört, und die Uhr bei einer gesetzten Frist — "
            + "verlierbar, der Takt je Partition holt nach. Ein Hörer aus dem Log hört nur konkrete, "
            + "persistente Events (der Plan sucht nach dem Typ des geschriebenen Events) — sonst ist schon seine Form falsch, denn Weckplan und Form "
            + "lesen dieselbe Lesart; jeder registrierte Hörer steht mit jedem gehörten Typ im Plan (docs/03 §3.5).", "error",
            [An("CQRS057", "Domain.SourceGeneration/HandlerFormAnalyzer.cs (Auf einer Projektion, Lesart Projektionsform)"),
             new("boot", null, "Infrastructure/Wecken/Wecker.cs (Pruefe: jeder registrierte Hörer steht mit jedem gehörten Typ im Plan)")]),
        new("GR-FRIST", "Frist → Uhr → Command an dasselbe Aggregat", "Eine Frist (Frist<TCmd> aus dem Decide eines Aggregats) nimmt die Uhr; fällig "
            + "sendet sie den Command an das Aggregat, das sie setzte (R5) — mindestens einmal, mit der Id aus dem Frist-Event.", "error",
            [Gen("CQRS010", "Infrastructure.SourceGeneration/CommandAggregateMapGenerator.cs"),
             new("laufzeit", null, "Infrastructure/Aggregate/ActorSystem/AggregateActorBase.cs (Fristen: eine fremde Frist schreibt nichts)"),
             new("laufzeit", null, "Infrastructure/Uhr/UhrActor.cs (fällig senden, nach entschiedenem Ausgang austragen)")]),
        new("GR-QUERY", "Query → genau ein Ziel: Reader oder Aggregat", "Eine Query hat genau ein Ziel (R4): einen Reader (Frage über Lese-Fähigkeiten, "
            + "der Stand der Projektionen dahinter) oder ein Aggregat (Frage im Decider, nur lesend auf seinen Zustand; die Query nennt es mit genau einem "
            + "Glied Id<TAggregat>, der Actor antwortet im Turn auf dem committeten Stand — exakt dem, auf dem der nächste Command entschieden würde; die "
            + "Antwort trägt seine Version als OCC-Stand, docs/03 §3.2).", "error",
            [An("CQRS084", "Infrastructure.SourceGeneration/GesamtbildAnalyzer.cs (mehrere Ziele: Reader und/oder Aggregate)"),
             An("CQRS086", "Infrastructure.SourceGeneration/GesamtbildAnalyzer.cs (kein Reader, kein Aggregat)"),
             An("CQRS057", "Domain.SourceGeneration/HandlerFormAnalyzer.cs (Frage an ein Aggregat: eine Query, synchron, genau ein Glied Id<TAggregat>)")]),
        new("GR-FAEHIGKEIT", "Fähigkeit → genau ein Store", "Eine Fähigkeit (Interface mit genau einer Funktion) gehört zu genau einem Store "
            + "(eine Implementierung); Auf und Frage nehmen sie als Parameter.", "error",
            [Gen("CQRS053", "Projections.SourceGeneration/ProjectionServicesGenerator.cs"), An("CQRS051", "Domain.SourceGeneration (FaehigkeitAnalyzer)")]),
        // Erzeugung: Baustein-Ausgang → Sorte
        new("GR-AUS-AGGREGAT", "Aggregat erzeugt Events", "Ein Aggregat (Decide) erzeugt Events, Hinweise, Fristen und Commands ohne Ziel — nie einen "
            + "Command an ein Aggregat (Aggregate reden über Graphen miteinander, CQRS114); auf "
            + "eine direkte Query (Frage) Antworten.", "error",
            [An("CQRS050", "Domain.SourceGeneration (Ausgabe-Vertrag)"), An("CQRS114", "Domain.SourceGeneration/AusgabeVertragAnalyzer.cs (Decide gibt einen Command an ein Aggregat aus)")]),
        new("GR-AUS-PROJEKTION", "Projektion erzeugt keine Nachricht", "Eine Projektion wirkt nur in ihrem Store (Schreib-Fähigkeiten genau eines Stores, R9) "
            + "und gibt nichts aus (Auf liefert Task) — was auf eine Entscheidung hin etwas sendet, nimmt ihren Command ohne Ziel (ein Graph).", "error",
            [An("CQRS057", "Domain.SourceGeneration/HandlerFormAnalyzer.cs (Rückgabe Task)"), Gen("CQRS052", "Infrastructure.SourceGeneration/PullPathGenerator.cs (ein Store je Projektion)")]),
        new("GR-AUS-READER", "Reader erzeugt Antworten", "Ein Reader antwortet je Frage mit einer Antwort oder OneOf<…> (auch als Task) und liest nur über "
            + "Lese-Fähigkeiten. Welche Projektionen hinter einer Frage stehen, folgt aus ihnen: die, die in denselben Store schreiben; was eine Antwort "
            + "zeigt, sagen ihre Id<T>-Felder (und die der Query).", "error",
            [An("CQRS050", "Domain.SourceGeneration (Ausgabe-Vertrag)"), An("CQRS057", "Domain.SourceGeneration/HandlerFormAnalyzer.cs (Frage eines Readers)")]),
        // Funktion als Baustein und die Regeln der Wiederholbarkeit (docs/10 §10.6).
        new("GR-FUNKTION-FORM", "Funktion: je Eingang ein Auf, freie Ausgabe", "Eine Funktion hat je Eingang (Command ohne Ziel oder ein Event als "
            + "Antwort auf einen eigenen Command) ein Auf mit optional Herkunft, Lese-Fähigkeiten und Abbruch; ausgegeben werden "
            + "Commands und Hinweise (konkret, OneOf, je auch als Task; IEnumerable bzw. IAsyncEnumerable — kein void, kein T?, kein ValueTask) — vollständig in der Signatur. Eine Funktion ist eine Klasse und läuft im Host; eine "
            + "Schnittstelle mit IFunktion ist ein Formfehler — was draußen gerechnet wird, ist Arbeit eines Akteurs (GR-ARBEIT). Eine Funktion ohne Auf ist "
            + "ein Formfehler; was sie ruft, ist Technik (GR-TECHNIK).", "error",
            [Gen("CQRS100", "Infrastructure.SourceGeneration/FunktionsBausteinGenerator.cs (Form der Funktion)"),
             new("boot", null, "Infrastructure/Funktionen/FunktionsBausteinWiring.cs (BindungVon: Parallel, Warteschlange, Wiederholungen je Knoten)")]),
        new("GR-EIN-EINGANG", "Je Baustein und Eingangstyp eine Methode", "Ein Baustein nimmt jeden Eingangstyp mit höchstens einer Methode — ein Auf "
            + "einer Funktion oder Projektion, eine Frage eines Readers (R2).", "error",
            [An("CQRS101", "Domain.SourceGeneration/EinEingangAnalyzer.cs"), Gen("CQRS101", "Infrastructure.SourceGeneration/FunktionsBausteinGenerator.cs")]),
        new("GR-GLEICH-RECHNEN", "Aggregat und Projektion rechnen gleich", "Aggregate und Projektionen rechnen bei gleicher Eingabe gleich: keine Uhr, "
            + "kein Zufall, kein Lesen fremder Daten — Zeit kommt aus dem Event bzw. der Herkunft (R11).", "error",
            [An("CQRS102", "Domain.SourceGeneration/GleichbleibendAnalyzer.cs")]),
        new("GR-FUNKTION-ZUSTAND", "Zustand einer Funktion nur über IZustand<T>", "Eine Funktion hält Zustand (ein Modell, das weiterlernt, ein Zähler) "
            + "nur über IZustand<T> im Konstruktor einer Funktions-Klasse oder einer Technik-Umsetzung (deren Zustand gehört dem Knoten ihres Halters): "
            + "er ist je Instanz und Knoten gesichert, wenn der Lauf, der ihn änderte, fertig ist, in der Reihenfolge der Läufe; eine Instanz mit Zustand "
            + "rechnet im Cluster auf genau einem Knoten (Advisory-Lock). Was fachlich zählt, steht im Log. Felder mit Setter, "
            + "veränderliche Felder und Sammlungen hinter readonly sind verboten; eine Funktion mit Zustand darf aus einem Eingang allein lernen (Auf "
            + "ohne Ausgabe) (R19, docs/04 §4.4).", "error",
            [An("CQRS105", "Domain.SourceGeneration/ZustandAnalyzer.cs"),
             Gen("CQRS100", "Infrastructure.SourceGeneration/FunktionsBausteinGenerator.cs (höchstens ein Zustand, eigene Klasse; Auf ohne Ausgabe nur mit Zustand)"),
             new("laufzeit", null, "Infrastructure/Funktionen/FunktionsAktivierung.cs (je Aktivierung ihr Zustand, Technik über AktivierungsZustaende)"),
             new("laufzeit", null, "Infrastructure/Funktionen/ZustandsSperre.cs (eine Aktivierung im Cluster, wer die Sperre hält)")]),
        new("GR-NEUE-ID", "Neue Ids nur abgeleitet", "Auf der Server-Seite (Aggregat, Projektion, Reader, Funktion im Host) entstehen neue Ids nur "
            + "abgeleitet (Herkunft.NeueId), nie mit Guid.NewGuid() oder Id<T>.Neu() — eine Wiederholung bekäme sonst ein anderes Ziel (R13). Clients "
            + "würfeln.", "error",
            [An("CQRS104", "Domain.SourceGeneration/GleichbleibendAnalyzer.cs")]),
        // Technik (docs/06 §6.5): Abhängigkeiten der Funktionen und ihrer Umsetzungen, ohne Marker, gebunden nach der Umgebung.
        new("GR-TECHNIK", "Technik ohne Marker, nur im Konstruktor", "Technik ist, was der Konstruktor einer Funktion (oder geschachtelt einer "
            + "Technik-Umsetzung) nimmt und weder Konfig-Record noch ILogger<T> noch IZustand<T> ist: eine Schnittstelle oder Klasse der Anwendung, ohne "
            + "Marker. Sie steht nur dort — nie als Parameter, Feld oder Eigenschaft im Fachcode (Aggregat, Projektion, Reader): dort sind es Fähigkeiten "
            + "als Parameter. Sie läuft im Prozess ihres Halters, lädt beim ersten Aufruf und wird über IAsyncDisposable freigegeben; eine Funktion steht "
            + "nie in einem Konstruktor (sie ruft das Framework).", "error",
            [An("CQRS089", "Domain.SourceGeneration/TechnikAnalyzer.cs"),
             new("boot", null, "Infrastructure/Funktionen/FunktionsExtensions.cs (BindeFunktionen: unter Funktionen nur die Implementierung)")]),
        new("GR-TECHNIK-BINDBAR", "Umsetzung baubar, Konstruktor bindbar", "Eine Funktion im Host und jede Umsetzung ist öffentlich und nicht "
            + "generisch; ihr Konstruktor nimmt nur Technik der Anwendung, Konfig-Records (Felder: Text, Zahl, Wahrheitswert, Dauer, Guid, Enum, "
            + "Text-Liste), ILogger<T> und ihren Zustand (IZustand<T>); gebaut wird sie per generierter Fabrik, ohne Reflection.", "error",
            [An("CQRS090", "Domain.SourceGeneration/TechnikAnalyzer.cs (Konstruktor-Parameter)"), An("CQRS091", "Domain.SourceGeneration/TechnikAnalyzer.cs (Konfig-Feld)"),
             An("CQRS092", "Domain.SourceGeneration/TechnikAnalyzer.cs (öffentlich, nicht generisch)"),
             new("generator", null, "Infrastructure.SourceGeneration/TechnikGenerator.cs (GeneratedTechnik: Umsetzungen, Bedarf, Konfig-Bindung)")]),
        new("GR-TECHNIK-GEBUNDEN", "Gebrauchte Technik ist gebunden", "Was eine gebundene Funktion (transitiv) braucht, ist in der Umgebung "
            + "eindeutig: je Technik genau eine Umsetzung (sonst die Wahl am Knoten der Instanz, Technik:<Schnittstelle>), je Konfig-Record seine Felder "
            + "am Knoten, kein Kreis über Technik — sonst bricht der Start.", "error",
            [new("boot", null, "Infrastructure/Funktionen/Technik.cs (BindeTechnik: fehlend, mehrdeutig, Kreis, Konfig)")]),
        new("GR-QUELLE", "Quelle = Funktion, die lauscht", "Eine Quelle ist eine Funktion mit Lausche(CancellationToken) statt Auf — aus dem Katalog des "
            + "Frameworks (Dateiwaechter, Taktgeber, Webhook) oder eigen. Sie steht am Kopf ihres Graphen (eine je Graph, dann kein anderer Kopf); jedes "
            + "Element startet einen Lauf, nichts davon steht im Log. Was sie gesehen hat und ihre laufende Nummer stehen in ihrem gesicherten Zustand; ihre "
            + "Instanz läuft genau einmal im Cluster (docs/04 §4.7).", "error",
            [Gen("CQRS100", "Infrastructure.SourceGeneration/FunktionsBausteinGenerator.cs (Form von Lausche)"),
             An("CQRS115", "Domain.SourceGeneration/GraphAnalyzer.cs (eine Quelle je Graph, sonst kein Kopf)"),
             new("laufzeit", null, "Infrastructure/Funktionen/GraphActor.cs (je Element ein Lauf, Gegendruck, Zustand gesichert nach dem Lauf)")]),
        // Zusatzregeln
        new("GR-AUSGANG-GESCHLOSSEN", "Geschlossener Ausgang", "Ausgänge sind konkrete Typen im OneOf — zeichnen heißt Typ wählen.", "error",
            [An("CQRS050", "Domain.SourceGeneration (Ausgabe-Vertrag)")]),
        new("GR-LESESEITE-FORM", "Form von Auf und Frage ist die, die die Laufzeit ruft", "Eine Projektion (IProjektion) hat je Event ein "
            + "Task Auf(Event e[, Herkunft h], Schreib-Fähigkeiten…[, CancellationToken ct]), ein Reader (IReader) je Query eine Frage(Query q, "
            + "Lese-Fähigkeiten…[, CancellationToken ct]) — sonst wäre die Methode im Editor sichtbar, zur Laufzeit tot. Die Kennung einer Projektion ist "
            + "ihr Typname; Position, Ordnung, Wiederholung und Store-Transaktion trägt das Framework.", "error",
            [An("CQRS057", "Domain.SourceGeneration/HandlerFormAnalyzer.cs"), An("CQRS101", "Domain.SourceGeneration/EinEingangAnalyzer.cs (doppeltes Auf/Frage)")]),
        new("GR-STORE-KLASSE", "Store mit genau einer Klasse", "Ein Store, in den eine Projektion schreibt, implementiert genau eine Store-Klasse: "
            + "IAtomaresZiel (1: Wirkung und Fortschritt in einer Transaktion), IWiederholungsfestesZiel (2: jede Wirkung ergibt wiederholt dasselbe; "
            + "darum nimmt jede Schreib-Fähigkeit die Herkunft) oder INurMindestensEinmal (3: keine Zusage; nur für eine Projektion mit "
            + "[MindestensEinmal]). Ob ein Treiber seine Klasse einhält, zeigt der allgemeine Store-Test (R14, docs/04 §4.3).", "error",
            [An("CQRS109", "Projections.SourceGeneration/StoreKlasseAnalyzer.cs (genau eine Klasse; Klasse 2: Herkunft in jeder Schreib-Fähigkeit)"),
             Gen("CQRS110", "Infrastructure.SourceGeneration/PullPathGenerator.cs (Klasse 3 nur mit [MindestensEinmal])"),
             new("boot", null, "Infrastructure/Projections/Projektionsnaht.cs (Projektionslauf.Ziel: genau eine Klasse, Klasse 3 nur mit Erlaubnis)"),
             new("test", null, "Infrastructure/Projections/Storetest.cs (allgemeiner Store-Test: Abbruch, Wiederholung, zweite Aktivierung, Neuaufbau)")]),
        new("GR-READMODEL-SCHLUESSEL", "Read-Model mit typisierter Id ist partial", "Ein Read-Model zu einem Aggregat trägt Id<T> Id und ist partial — "
            + "der Generator ergänzt den Speicher-Schlüssel (Schluessel) für Stores, die einen einfachen Schlüssel brauchen; Fähigkeiten nehmen Id<T>.", "error",
            [Gen("CQRS111", "Domain.SourceGeneration/ReadModelSchluesselGenerator.cs")]),
        new("GR-KREIS", "Kreise Funktion ↔ Aggregat", "Ein Ablauf ist ein Kreis (Vorgang → Funktion → anderes Aggregat → Funktion → Vorgang); er endet, "
            + "weil das Decide des Vorgangs fortschreitenden Zustand trägt. Einen Kreis ohne Fortschritt erkennt der Build nicht (R6, docs/10 §10.6).", "info",
            [Offen("docs/13 FB-5: aus Signaturen allein nicht von einem Ablauf zu unterscheiden")]),
        new("GR-ZUSTAND", "Regel Z: Zustand nur in Aggregaten und Lesemodellen", "Projektionen sind zustandslose Übersetzer; Gedächtnis gehört in ein Aggregat.", "warning",
            [An("CQRS083", "Domain.SourceGeneration/ModellAnalyzer.cs (Instanzfelder, die Zustand halten)")]),
        new("GR-AKTEUR", "Akteur: IDarf hinein, IHoert heraus", "Ein Akteur (record X : IAkteur) gibt nur hinein, was er per IDarf<T> darf: einen "
            + "Command, eine Query, einen Hinweis. Zu ihm kommt, was er per IHoert<T> hört: ein Command ohne Ziel ist Arbeit, ein Event oder Hinweis "
            + "Kenntnis; einen Command an ein Aggregat hört kein Akteur.", "error",
            [An("CQRS058", "Domain.SourceGeneration/AkteurAnalyzer.cs"), Gen("CQRS059", "Infrastructure.SourceGeneration/AkteurRechteGenerator.cs (Name eindeutig)"),
             new("laufzeit", null, "Infrastructure/Akteure/AkteurTor.cs (Handshake + jede hineingehende Nachricht)")]),
        new("GR-HERKUNFT", "Alles kommt von einem Akteur", "Sobald es Akteure gibt: jeder Command und jede Query kommt von einem Akteur — direkt "
            + "(IDarf<T>) oder über die Kette (eine Funktion oder eine Frist erzeugt ihn aus einem Event, das von einem Akteur stammt). Sonst steht er im "
            + "Rahmen „ohne Akteur“.", "warning",
            [new("laufzeit", null, "Infrastructure/Akteure/AkteurTor.cs (ohne IDarf abgewiesen)"),
             Offen("Editor-Sicht: Rahmen „ohne Akteur“ (board.sicht); Fixpunkt im Generator (docs/konzept-akteure.md §2.3)")]),
        new("GR-ARBEIT", "Arbeit für einen Akteur", "Hört ein Akteur einen Command ohne Ziel, ist er dessen einziger Empfänger: die Werkstatt gibt ihn "
            + "einer Sitzung, die den Akteur verkörpert (verkörpert ∩ Token), und stellt ihn erneut zu, bis eine Antwort kommt — die erste gewinnt; die "
            + "Antwort sind Commands seines IDarf, ihre Ids folgen aus dem Auftrag. Ein Command, den ein Graph nimmt und ein Akteur hört, ist ein Fehler.", "error",
            [An("CQRS116", "Infrastructure.SourceGeneration/GesamtbildAnalyzer.cs (genau ein Empfänger: Graph-Kopf oder Akteur)"),
             new("laufzeit", null, "Infrastructure/Funktionen/Arbeit.cs (je Akteur mit Arbeit eine Instanz, ein Knoten draußen)"),
             new("laufzeit", null, "Infrastructure/Funktionen/Werkstatt.cs (Aufträge an Sitzungen, die erste Antwort gewinnt)")]),
        new("GR-CLIENT", "Client: nennt nur, wen er verkörpert", "Ein Client (record X : IClient) nennt in seiner Basisliste nur, wen er verkörpert "
            + "(IVerkoerpert<A>) — keine eigenen Member, keine client-internen Typen. Was über seine Leitung geht, sind die IDarf und IHoert seiner "
            + "Akteure; Handshake-Name ist der Record-Name.", "error",
            [An("CQRS063", "Domain.SourceGeneration/AkteurAnalyzer.cs (Form)"),
             new("generator", null, "Infrastructure.SourceGeneration/AkteurRechteGenerator.cs (GeneratedClientVertraege, Client-Hash)"),
             new("laufzeit", null, "Infrastructure/GrpcClient/CqrsClientService.cs (Handshake je Client, verkörpert ∩ Token)")]),
        new("GR-CLIENT-BEFUGT", "Client verkörpert Akteure", "Ein Client verkörpert nur konkrete Akteure (IVerkoerpert<A>); befugt ist der Akteur, nicht "
            + "die Software. Wirksam ist an jeder Sitzung verkörpert ∩ Token, für beide Richtungen (hinein wie gehört).", "error",
            [An("CQRS064", "Domain.SourceGeneration/AkteurAnalyzer.cs"), new("laufzeit", null, "Infrastructure/Akteure/ClientVertrag.cs (Rechte = verkörperte Akteure ∩ Token)")]),
        new("GR-CLIENT-CODE", "Client-Code = seine Akteure", "Eine C#-Client-Assembly nennt ihren Client ([assembly: IstClient<X>]); was ihr Code an "
            + "Commands erzeugt und an Queries stellt, muss ein verkörperter Akteur dürfen (IDarf), worauf ein Handler hört, muss einer hören (IHoert) — "
            + "abonniert wird nur das. Arbeit nimmt heute nur das Python-SDK an (je gehörtem Command ein Handler in der generierten Basis).", "error",
            [Gen("CQRS093", "Client.SourceGeneration/ClientVertrag.cs (Client-Assembly nennt ihren Client nicht)"),
             Gen("CQRS094", "Client.SourceGeneration/ClientVertrag.cs (C#-Client verkörpert Akteure mit Arbeit)"),
             An("CQRS095", "Client.SourceGeneration/ClientVertrag.cs (Command darf kein verkörperter Akteur)"),
             An("CQRS096", "Client.SourceGeneration/ClientVertrag.cs (Query darf kein verkörperter Akteur)"),
             An("CQRS097", "Client.SourceGeneration/ClientVertrag.cs (Server-Event nicht Kenntnis)")]),
        new("GR-MODUL-EINGANG-OFFEN", "Eingang ohne Konsument", "Ein Command oder eine Query, die niemand konsumiert — offener Eingang des Moduls.", "warning",
            [An("CQRS086", "Infrastructure.SourceGeneration/GesamtbildAnalyzer.cs (Command ohne Decider, Query ohne Reader und ohne Aggregat)")]),
        new("GR-MODUL-AUSGANG-OFFEN", "Ausgang ohne Erzeuger", "Ein Event, eine Ablehnung oder eine Response, die niemand erzeugt — offener Ausgang des Moduls.", "warning",
            [An("CQRS085", "Infrastructure.SourceGeneration/GesamtbildAnalyzer.cs (Event/Ablehnung/Response ohne Erzeuger)")]),
    ];

    /// <summary>Die Tabelle aus §3: Sorte × Baustein-Eingang (mit Kardinalität je Nachricht).</summary>
    public static readonly IReadOnlyList<Konsum> Konsume =
    [
        new(Command, Aggregat, GenauEins, "GR-COMMAND"),
        new(Event, Projektion, Beliebig, "GR-EVENT"), new(Event, Funktion, Beliebig, "GR-EVENT"),
        new(Event, Aggregat, GenauEins, "GR-FALTUNG"),
        new(Query, Reader, GenauEins, "GR-QUERY"), new(Query, Aggregat, GenauEins, "GR-QUERY"),
        new(Faehigkeit, Projektion, Beliebig, "GR-FAEHIGKEIT"), new(Faehigkeit, Reader, Beliebig, "GR-FAEHIGKEIT"),
        new(Faehigkeit, Funktion, Beliebig, "GR-FUNKTION-FORM"),
        new(Gerufen, Funktion, Beliebig, "GR-TECHNIK"),
        // Was ein Akteur hört (IHoert): ein Event oder Hinweis zur Kenntnis, ein Command ohne Ziel als Arbeit (er ist dann sein einziger Empfänger).
        new(Event, Akteur, Beliebig, "GR-EVENT"), new(Transient, Akteur, Beliebig, "GR-TRANSIENT"), new(Command, Akteur, GenauEins, "GR-ARBEIT"),
        // Ein Command ohne Ziel zwischen Knoten: jeder Knoten desselben Graphen, der ihn nimmt, bzw. der Kopf genau eines Graphen.
        new(Command, Funktion, GenauEins, "GR-KANTE"),
        new(Frist, Uhr, GenauEins, "GR-FRIST"),
    ];

    /// <summary>Wer welche Sorten erzeugen darf (§2.2 Spalte „Ausgänge").</summary>
    public static readonly IReadOnlyList<Erzeugung> Erzeugungen =
    [
        new(Aggregat, Event, "GR-AUS-AGGREGAT"), new(Aggregat, Transient, "GR-AUS-AGGREGAT"), new(Aggregat, Frist, "GR-AUS-AGGREGAT"),
        new(Funktion, Command, "GR-FUNKTION-FORM"), new(Funktion, Transient, "GR-FUNKTION-FORM"),
        new(Aggregat, Command, "GR-DECIDE-COMMAND"),
        new(Reader, Response, "GR-AUS-READER"), new(Aggregat, Response, "GR-AUS-AGGREGAT"),
        new(Store, Faehigkeit, "GR-FAEHIGKEIT"),
        new(Funktion, Gerufen, "GR-TECHNIK"),
        new(Aussenwelt, Command, "GR-COMMAND"), new(Aussenwelt, Query, "GR-QUERY"),
        new(Akteur, Command, "GR-AKTEUR"), new(Akteur, Query, "GR-AKTEUR"), new(Akteur, Transient, "GR-AKTEUR"),
        new(Client, Command, "GR-CLIENT-BEFUGT"), new(Client, Query, "GR-CLIENT-BEFUGT"), new(Client, Transient, "GR-CLIENT-BEFUGT"),
        new(Uhr, Command, "GR-FRIST"),
    ];

    /// <summary>Nachrichtensorte eines Records: aus seiner Art (Quellen sind Funktionen, die lauschen — ihre Commands sind gewöhnliche Commands).</summary>
    public static string? SorteVon(Record r) => SorteVonRecordArt(r.Kind);

    /// <summary>Record-Art → Nachrichtensorte (null = keine Nachricht: Value Object, Konfig, ReadModel).</summary>
    public static string? SorteVonRecordArt(string kind) => kind switch
    {
        RecordArt.Command => Command, RecordArt.Event => Event, RecordArt.Rejection => Transient, RecordArt.Query => Query,
        RecordArt.Antwort => Response, _ => null,
    };

    public static Regel RegelVon(string id) => Regeln.First(r => r.Id == id);
    public static Konsum? KonsumVon(string sorte, string baustein) => Konsume.FirstOrDefault(k => k.Sorte == sorte && k.Baustein == baustein);
    public static Erzeugung? ErzeugungVon(string baustein, string sorte) => Erzeugungen.FirstOrDefault(e => e.Baustein == baustein && e.Sorte == sorte);
    public static string SorteName(string sorte) => Sorten.FirstOrDefault(s => s.Id == sorte)?.Name ?? sorte;
    public static string BausteinName(string baustein) => Bausteine.FirstOrDefault(b => b.Id == baustein)?.Name ?? baustein;

    public static string BuildText(Regel r) => string.Join(", ", r.Build.Select(g => g.Kennung ?? (g.Art == "offen" ? "offen" : g.Art)).Distinct());

    /// <summary>
    /// Die Abbildung der EDITOR-Ports auf die Sprache — damit das Editor-JS nichts hart kodiert: Port-Typ → Sorte (für Ports,
    /// die nicht an einer Nachrichten-Karte hängen), Port-Schlüssel → Baustein (die Info-Felder der Ports: <c>dec</c>, <c>proj</c> …).
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> EditorPortSorte = new Dictionary<string, string>
    {
        ["wcall"] = Faehigkeit, ["rcall"] = Faehigkeit, ["gerufen"] = Gerufen,
    };
    public static readonly IReadOnlyList<(string Schluessel, string Baustein)> EditorPortBaustein =
    [
        ("dec", Aggregat), ("app", Aggregat), ("proj", Projektion), ("reader", Reader), ("akt", Akteur), ("client", Client),
        ("fk", Funktion),
    ];
    /// <summary>
    /// Editor-Ports, an denen der Baustein des AUSGANGS die Sorte GEBRAUCHT statt sie zu erzeugen: Auf/Frage → Store-Fähigkeit (der Port
    /// sitzt am Eingang, die Fähigkeit selbst erzeugt der Store). Geprüft wird dort der Konsum (Sorte × Baustein des Ausgangs).
    /// </summary>
    public static readonly IReadOnlyList<string> EditorPortGebrauch = ["wcall", "rcall", "gerufen"];

    /// <summary>
    /// Der Katalog im Verbinden-Modus: welche Record-Arten an einen Editor-Port passen KÖNNEN (Port-Typ + Richtung der Gegenseite), auch wenn
    /// sie nicht als Karte auf dem Board stehen. Ob eine davon passt, urteilt <c>Sicht.Passt</c> wie bei jedem Kandidaten.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string[]> EditorKatalogSorte = new Dictionary<string, string[]>
    {
        ["evtOut|in"] = ["event", "rejection"], ["evtUse|out"] = ["event", "rejection"], ["cmd|out"] = ["command"],
        ["query|out"] = ["query"], ["qrsp|in"] = ["queryresponse"], ["ftype|out"] = ["valueobject"],
    };

    /// <summary>Die Grammatik als JSON-Objekt für das Board (<c>rahmen.grammatik</c>).</summary>
    public static object AlsJson() => new
    {
        sorten = Sorten.Select(s => new { id = s.Id, name = s.Name, symbol = s.Symbol, garantie = s.Garantie, bedeutung = s.Bedeutung }),
        bausteine = Bausteine.Select(b => new { id = b.Id, name = b.Name, zustand = b.Zustand }),
        konsume = Konsume.Select(k => new { sorte = k.Sorte, baustein = k.Baustein, kardinalitaet = k.Kardinalitaet, regel = k.Regel }),
        erzeugungen = Erzeugungen.Select(e => new { baustein = e.Baustein, sorte = e.Sorte, regel = e.Regel }),
        regeln = Regeln.Select(r => new
        {
            id = r.Id, name = r.Name, text = r.Text, schwere = r.Schwere, build = BuildText(r),
            gegenstuecke = r.Build.Select(g => new { art = g.Art, kennung = g.Kennung, ort = g.Ort }),
        }),
        recordSorte = RecordArt.Alle.Where(k => SorteVonRecordArt(k) != null).ToDictionary(k => k, k => SorteVonRecordArt(k)!),
        portSorte = EditorPortSorte,
        portBaustein = EditorPortBaustein.Select(x => new[] { x.Schluessel, x.Baustein }),
        portGebrauch = EditorPortGebrauch,
        katalogSorte = EditorKatalogSorte,
        // Für die Modul-Ableitung im Editor (dieselbe wie DomainEditor.Module): Sorten mit Konsumenten-/Erzeuger-Pflicht.
        offenEin = Module.BrauchtKonsument.Order(StringComparer.Ordinal),
        offenAus = Module.BrauchtErzeuger.Order(StringComparer.Ordinal),
    };
}
