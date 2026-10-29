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
        Faehigkeit = "faehigkeit", Quelle = "quelle", Dienst = "dienst";

    // ── Alphabet: Bausteine (die Knoten) ─────────────────────────────────────────────────────────────────────────────
    public const string Aggregat = "aggregat", Projektion = "projektion", Reader = "reader", Store = "store", Aussenwelt = "aussenwelt",
        Akteur = "akteur", Client = "client", Funktion = "funktion", DienstImpl = "dienstimpl", Uhr = "uhr";

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
        new(Command, "Command", "▶", "wirkt genau einmal (Command-Id, Inbox)", "Absicht an genau ein Aggregat"),
        new(Event, "Event", "◆", "durabel; wirkt bei jedem Hörer genau einmal", "Fakt im Log"),
        new(Transient, "Hinweis", "◇", "höchstens einmal (verlierbar)", "flüchtig: Ablehnung, Fortschritt, Live-Anzeige (ITransientEvent)"),
        new(Frist, "Frist", "⏳", "durabel; feuert mindestens einmal, wirkt genau einmal", "Zeit aus dem Decide eines Aggregats — die Uhr macht daraus den Command an dasselbe Aggregat"),
        new(Query, "Query", "?", "synchron, wirkt nicht", "Frage"),
        new(Response, "Antwort", "↩", "synchron; trägt den Stand der gezeigten Ids", "Rückgabe eines Readers oder eines Aggregats (dann mit seiner Version als OCC-Stand)"),
        new(Faehigkeit, "Fähigkeit", "⚙", "als Parameter von Auf bzw. Frage", "Store-Funktion (Lesen/Schreiben)"),
        new(Quelle, "Quell-Nachricht", "⛲", "durabel (genau einmal je Kennung)", "Anstoß von außen (Datei, Webhook …) — ein Event, das Funktionen und Projektionen hören"),
        new(Dienst, "Dienst", "🧩", "im Konstruktor (gebunden nach der Umgebung)", "Technik hinter einer Funktion (IDienst) — keine Nachricht, eine Abhängigkeit"),
    ];

    public static readonly IReadOnlyList<BausteinInfo> Bausteine =
    [
        new(Aggregat, "Aggregat", true), new(Projektion, "Projektion", false), new(Reader, "Reader", false),
        new(Store, "Store", true), new(Aussenwelt, "Außenwelt", false),
        new(Akteur, "Akteur", false), new(Client, "Client", false), new(Funktion, "Funktion", false),
        // Domänen-Dienst (docs/06 §6.5): seine Implementierung nimmt selbst Dienste im Konstruktor.
        new(DienstImpl, "Dienst-Implementierung", false),
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
        new("GR-COMMAND", "Command → genau ein Aggregat", "Ein Command nennt sein Ziel-Aggregat im Typ (ICommand<TAggregat> mit Id<TAggregat>) und wird genau dort entschieden.", "error",
            [Comp("ICommand verlangt Ziel; ICommand<TAggregat> liefert es aus Id<TAggregat> AggregateId"), An("CQRS013", "Domain.SourceGeneration/ModellAnalyzer.cs (Command ohne typisiertes Ziel)"),
             Gen("CQRS010", "Infrastructure.SourceGeneration/CommandAggregateMapGenerator.cs (Decide außerhalb des Ziel-Aggregats)"),
             An("CQRS086", "Infrastructure.SourceGeneration/GesamtbildAnalyzer.cs (Command ohne Decider)")]),
        new("GR-EVENT", "Event → beliebig viele Konsumenten", "Ein persistentes Event (auch eine Quell-Nachricht) hören Projektionen und Funktionen "
            + "(beliebig viele) und Clients zur Kenntnis (IHoert); gefaltet wird es im eigenen Aggregat.", "error",
            [Comp("Auf-Parameter : IEvent"), An("CQRS057", "Domain.SourceGeneration/HandlerFormAnalyzer.cs (Auf einer Projektion)"),
             Gen("CQRS100", "Infrastructure.SourceGeneration/FunktionsBausteinGenerator.cs (Eingang einer Funktion)")]),
        new("GR-FALTUNG", "Event → genau ein Aggregat faltet", "Ein Event faltet genau ein Aggregat (Applier) — das, das es entscheidet.", "warning",
            [An("CQRS080", "Domain.SourceGeneration/ModellAnalyzer.cs (Apply ohne Decide)"), An("CQRS079", "Domain.SourceGeneration/ModellAnalyzer.cs (Decide ohne Apply)")]),
        new("GR-TRANSIENT", "Hinweis → Funktion/Client", "Einen Hinweis (ITransientEvent: Ablehnung, Fortschritt) hören Funktionen (höchstens einmal) "
            + "und Clients (IHoert) — nie eine Projektion, nie ein Aggregat. Ist eine Ablehnung für einen Ablauf wichtig, ist sie ein Event.", "error",
            [Gen("CQRS100", "Infrastructure.SourceGeneration/FunktionsBausteinGenerator.cs (Hinweisplan; aus einem Hinweis nur Commands und Hinweise)"),
             new("laufzeit", null, "Infrastructure/Funktionen/HinweisNetz.cs (direkt an die Adresse, ohne Log und Broker)")]),
        new("GR-HINWEIS-NETZ", "Hinweis-Netz: hart verdrahtet, höchstens einmal", "Ein Hinweis aus einem Aggregat oder einer Funktion geht direkt an "
            + "die Adresse jeder Funktion, die ihn hört (Partition seines Herkunfts-Streams bzw. die eine Adresse) — im Speicher, ohne Log, ohne Broker, "
            + "höchstens einmal. Jeder Hinweis, den eine Funktion hört, hat einen Erzeuger im Netz; jede Adresse der Routing-Tabelle existiert; "
            + "Commands aus einem Hinweis tragen eine Id aus Funktion, Hinweis-Kennung, Stelle, Typ und Ziel (R20, docs/04 §4.4).", "error",
            [An("CQRS106", "Infrastructure.SourceGeneration/GesamtbildAnalyzer.cs (gehörter Hinweis ohne Erzeuger)"),
             new("boot", null, "Infrastructure/Funktionen/HinweisNetz.cs (jede Adresse der Routing-Tabelle gehört zu einer Funktion)")]),
        new("GR-WECKPLAN", "Weckplan: jeder Hörer mit jedem gehörten Typ", "Nach jedem Commit weckt der Wecker gezielt, wen der generierte Weckplan "
            + "zum Typ eines geschriebenen Events nennt: die Partition jeder Projektion und jeder Funktion, die ihn aus dem Log hört (bzw. die eine "
            + "Adresse), und die Uhr bei einer gesetzten Frist — verlierbar, der Takt je Partition holt nach. Ein Hörer aus dem Log hört nur konkrete, "
            + "persistente Events (der Plan sucht nach dem Typ des geschriebenen Events) — sonst ist schon seine Form falsch, denn Weckplan und Form "
            + "lesen dieselbe Lesart; jeder registrierte Hörer steht mit jedem gehörten Typ im Plan (docs/03 §3.5).", "error",
            [An("CQRS057", "Domain.SourceGeneration/HandlerFormAnalyzer.cs (Auf einer Projektion, Lesart Projektionsform)"),
             Gen("CQRS100", "Infrastructure.SourceGeneration/FunktionsBausteinGenerator.cs (Eingang einer Funktion, Lesart Funktionsform)"),
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
        new("GR-AUS-AGGREGAT", "Aggregat erzeugt Events", "Ein Aggregat (Decide) erzeugt Events, Hinweise und Fristen — keine Commands; auf eine "
            + "direkte Query (Frage) Antworten.", "error",
            [An("CQRS050", "Domain.SourceGeneration (Ausgabe-Vertrag)")]),
        new("GR-AUS-PROJEKTION", "Projektion erzeugt keine Nachricht", "Eine Projektion wirkt nur in ihrem Store (Schreib-Fähigkeiten genau eines Stores, R9) "
            + "und gibt nichts aus (Auf liefert Task) — was auf ein Event hin etwas sendet, ist eine Funktion.", "error",
            [An("CQRS057", "Domain.SourceGeneration/HandlerFormAnalyzer.cs (Rückgabe Task)"), Gen("CQRS052", "Infrastructure.SourceGeneration/PullPathGenerator.cs (ein Store je Projektion)")]),
        new("GR-AUS-READER", "Reader erzeugt Antworten", "Ein Reader antwortet je Frage mit einer Antwort oder OneOf<…> (auch als Task) und liest nur über "
            + "Lese-Fähigkeiten. Welche Projektionen hinter einer Frage stehen, folgt aus ihnen: die, die in denselben Store schreiben; was eine Antwort "
            + "zeigt, sagen ihre Id<T>-Felder (und die der Query).", "error",
            [An("CQRS050", "Domain.SourceGeneration (Ausgabe-Vertrag)"), An("CQRS057", "Domain.SourceGeneration/HandlerFormAnalyzer.cs (Frage eines Readers)")]),
        // Funktion als Baustein und die Regeln der Wiederholbarkeit (docs/10 §10.6).
        new("GR-FUNKTION-FORM", "Funktion: je Eingang ein Auf, freie Ausgabe", "Eine Funktion hat je Eingang (Event, Quell-Nachricht oder Hinweis) ein Auf "
            + "mit optional Herkunft, Lese-Fähigkeiten und Abbruch; ausgegeben werden Commands, Events und Hinweise (konkret, OneOf, T?, Strom, je auch als "
            + "Task) — vollständig in der Signatur. Als Klasse läuft sie im Host, als Schnittstelle draußen (Laufort aus der Umgebung); eine Schnittstelle "
            + "ohne eindeutige Umsetzung braucht eine Bindung, eine Funktion mit Lese-Fähigkeit läuft nur im Host.", "error",
            [Gen("CQRS100", "Infrastructure.SourceGeneration/FunktionsBausteinGenerator.cs (Form der Funktion)"),
             new("boot", null, "Infrastructure/Funktionen/FunktionsBausteinWiring.cs (BindungVon: Laufort, Umsetzung, Lese-Fähigkeit nur im Host)")]),
        new("GR-EIN-EINGANG", "Je Baustein und Eingangstyp eine Methode", "Ein Baustein nimmt jeden Eingangstyp mit höchstens einer Methode — ein Auf "
            + "einer Funktion oder Projektion, eine Frage eines Readers (R2).", "error",
            [An("CQRS101", "Domain.SourceGeneration/EinEingangAnalyzer.cs"), Gen("CQRS101", "Infrastructure.SourceGeneration/FunktionsBausteinGenerator.cs")]),
        new("GR-GLEICH-RECHNEN", "Aggregat und Projektion rechnen gleich", "Aggregate und Projektionen rechnen bei gleicher Eingabe gleich: keine Uhr, "
            + "kein Zufall, kein Lesen fremder Daten — Zeit kommt aus dem Event bzw. der Herkunft (R11).", "error",
            [An("CQRS102", "Domain.SourceGeneration/GleichbleibendAnalyzer.cs")]),
        new("GR-FESTHALTEN", "Funktion hält ihre Ausgabe fest", "Eine Funktion hält ihre Ausgabe im Log fest, bevor sie wirkt; ohne Festhalten sendet nur "
            + "eine nachweislich gleichbleibende — eine Klasse ohne Dienst und ohne Lese-Fähigkeit, deren Rumpf weder Uhr noch Zufall noch fremde Daten "
            + "nutzt (R12).", "error",
            [An("CQRS103", "Domain.SourceGeneration/GleichbleibendAnalyzer.cs (gleichbleibend heißt ohne Uhr und Zufall)"),
             new("generator", null, "Infrastructure.SourceGeneration/FunktionsBausteinGenerator.cs (Gleichbleibend aus der Signatur)")]),
        new("GR-FUNKTION-ZUSTAND", "Zustand einer Funktion nur über IZustand<T>", "Eine Funktion hält Zustand (ein Modell, das weiterlernt, ein Zähler) "
            + "nur über IZustand<T> im Konstruktor einer Funktions-Klasse: er wird mit der Position gesichert, kommt beim Umzug mit, und Auf läuft immer "
            + "nur einmal zugleich. Felder mit Setter, veränderliche Felder und Sammlungen hinter readonly sind verboten (sie gingen beim Umzug still "
            + "verloren); eine Funktion mit Zustand ist nie gleichbleibend und darf aus einem Eingang allein lernen (Auf ohne Ausgabe); ein Zustand für "
            + "alles steht mit [EineAdresse] an der Funktion (R19, docs/04 §4.4).", "error",
            [An("CQRS105", "Domain.SourceGeneration/ZustandAnalyzer.cs"),
             Gen("CQRS100", "Infrastructure.SourceGeneration/FunktionsBausteinGenerator.cs (höchstens ein Zustand, eigene Klasse; Auf ohne Ausgabe nur mit Zustand)"),
             new("generator", null, "Infrastructure.SourceGeneration/FunktionsBausteinGenerator.cs (mit Zustand nie gleichbleibend; Sichern über den generierten Kontext)")]),
        new("GR-FUNKTION-LEBENSZYKLUS", "Lebenszyklus einer Funktion optional", "Eine Funktions-Klasse im Host kann einen Lebenszyklus haben "
            + "(ILebenszyklus): BeimAktivierenAsync nach dem Bau und dem Laden des Zustands, vor dem ersten Auf (ein Netz laden); VorDemSichernAsync vor "
            + "jeder Sicherung des Zustands (Gewichte zurückholen); das Ende über IAsyncDisposable. Eine geladene Ressource darf in einem readonly-Feld "
            + "stehen, Zustand nur in IZustand<T>. Nicht an Schnittstellen (draußen gehört der Lebenszyklus dem Worker), nicht an Diensten; eine Funktion "
            + "mit Lebenszyklus ist nie gleichbleibend (docs/04 §4.4, docs/10 E10).", "error",
            [An("CQRS107", "Domain.SourceGeneration/LebenszyklusAnalyzer.cs"),
             new("generator", null, "Infrastructure.SourceGeneration/Funktionsform.cs (mit Lebenszyklus nie gleichbleibend)"),
             new("laufzeit", null, "Infrastructure/Funktionen/FunktionsActor.cs (Aktivieren nach dem Laden, vor jeder Sicherung, Freigabe am Ende)")]),
        new("GR-NEUE-ID", "Neue Ids nur abgeleitet", "Auf der Server-Seite (Aggregat, Projektion, Reader, Funktion im Host) entstehen neue Ids nur "
            + "abgeleitet (Herkunft.NeueId), nie mit Guid.NewGuid() oder Id<T>.Neu() — eine Wiederholung bekäme sonst ein anderes Ziel (R13). Clients "
            + "würfeln.", "error",
            [An("CQRS104", "Domain.SourceGeneration/GleichbleibendAnalyzer.cs")]),
        // Domänen-Dienste (docs/06 §6.5): Abhängigkeiten der Implementierungen, gebunden nach der Umgebung.
        new("GR-DIENST", "Dienst nur im Konstruktor einer Implementierung", "Ein Dienst (interface IX : IDienst) steht nur im Konstruktor einer "
            + "Funktion im Host oder einer Dienst-Implementierung — nie als Parameter, Feld oder Eigenschaft im Fachcode (Aggregat, Projektion, Reader): "
            + "dort sind es Fähigkeiten als Parameter.", "error",
            [An("CQRS089", "Domain.SourceGeneration/DienstAnalyzer.cs")]),
        new("GR-DIENST-BINDBAR", "Implementierung baubar, Konstruktor bindbar", "Eine Funktion im Host oder eine Dienst-Implementierung ist öffentlich "
            + "und nicht generisch; ihr Konstruktor nimmt nur Dienste, Konfig-Records (Felder: Text, Zahl, Wahrheitswert, Dauer, Guid, Enum, Text-Liste) "
            + "und ILogger<T> — gebaut wird sie per generierter Fabrik, ohne Reflection.", "error",
            [An("CQRS090", "Domain.SourceGeneration/DienstAnalyzer.cs (Konstruktor-Parameter)"), An("CQRS091", "Domain.SourceGeneration/DienstAnalyzer.cs (Konfig-Feld)"),
             An("CQRS092", "Domain.SourceGeneration/DienstAnalyzer.cs (öffentlich, nicht generisch)"),
             new("generator", null, "Infrastructure.SourceGeneration/DienstGenerator.cs (GeneratedDienste: Implementierungen, Bedarf, Konfig-Bindung)")]),
        new("GR-DIENST-GEBUNDEN", "Gebrauchter Dienst ist gebunden", "Was eine gebundene Funktion (transitiv) braucht, ist in der Umgebung eindeutig: "
            + "je Dienst genau eine Implementierung (sonst Dienste:<IX>:Implementierung), je Konfig-Record ein Abschnitt Konfig:<Record>, kein Kreis über "
            + "Dienste — sonst bricht der Start.", "error",
            [new("boot", null, "Infrastructure/Dienste/Dienste.cs (BindeDienste: fehlend, mehrdeutig, Kreis, Konfig)")]),
        new("GR-QUELLE", "Quell-Nachricht von außen", "Eine Quell-Nachricht (record … : IQuellNachricht) wird genau einmal je Kennung ins Log "
            + "geschrieben; Funktionen und Projektionen hören sie wie jedes Event. Sie kommt von außen (die Maschine/der Akteur hinter der Quelle, IDarf).", "error",
            [new("laufzeit", null, "Infrastructure/Quellen/Quellen.cs (QuellEingang: StartStream je Kennung)")]),
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
        new("GR-AKTEUR", "Akteur → nur, was er darf", "Ein Akteur gibt nur hinein, was er per IDarf<T> darf: Command, Query, Quell-Nachricht, "
            + "Transient-Event oder die Ausgaben einer Funktion, die er draußen rechnet. Was er hören darf, wird abgeleitet (Aggregate seiner Commands, "
            + "Projektionen hinter seinen Queries).", "error",
            [An("CQRS058", "Domain.SourceGeneration/AkteurAnalyzer.cs"), Gen("CQRS059", "Infrastructure.SourceGeneration/AkteurRechteGenerator.cs (Name eindeutig)"),
             new("laufzeit", null, "Infrastructure/Akteure/AkteurTor.cs (Handshake + jede hineingehende Nachricht)")]),
        new("GR-HERKUNFT", "Alles kommt von einem Akteur", "Sobald es Akteure gibt: jeder Command und jede Query kommt von einem Akteur — direkt "
            + "(IDarf<T>) oder über die Kette (eine Funktion oder eine Frist erzeugt ihn aus einem Event, das von einem Akteur stammt). Sonst steht er im "
            + "Rahmen „ohne Akteur“.", "warning",
            [new("laufzeit", null, "Infrastructure/Akteure/AkteurTor.cs (ohne IDarf abgewiesen)"),
             Offen("Editor-Sicht: Rahmen „ohne Akteur“ (board.sicht); Fixpunkt im Generator (docs/konzept-akteure.md §2.3)")]),
        new("GR-CLIENT", "Client-Vertrag: nur der Rand", "Ein Client (interface IX : IClientVertrag) stellt in seiner Basisliste zusammen, was über SEINE "
            + "Leitung geht: getragene Funktionen (ITraegt<F>), ISendet<T>/IFragt<T> und Kenntnis (IHoert<E>) — keine eigenen Member, keine "
            + "client-internen Typen.", "error",
            [An("CQRS063", "Domain.SourceGeneration/AkteurAnalyzer.cs (Form)"),
             new("generator", null, "Infrastructure.SourceGeneration/AkteurRechteGenerator.cs (GeneratedClientVertraege, Client-Hash)"),
             new("laufzeit", null, "Infrastructure/GrpcClient/CqrsClientService.cs (Handshake je Client, Vertrag ∩ Token)")]),
        new("GR-CLIENT-BEFUGT", "Client sendet nur, was ein Akteur darf", "Was ein Client trägt (ITraegt), sendet (ISendet) oder fragt (IFragt), muss ein "
            + "Akteur per IDarf<T> dürfen — der Client-Vertrag schneidet aus der Befugnis, er erweitert sie nie.", "error",
            [An("CQRS064", "Domain.SourceGeneration/AkteurAnalyzer.cs"), new("laufzeit", null, "Infrastructure/Akteure/ClientVertrag.cs (Rechte = Vertrag ∩ IDarf ∩ Token)")]),
        new("GR-CLIENT-CODE", "Client-Code = Vertrag", "Eine Client-Assembly nennt ihren Vertrag ([assembly: IstClient<IX>]); was ihr Code an "
            + "Commands erzeugt, sendet der Vertrag (ISendet), was er an Queries stellt, fragt er (IFragt), worauf ein Handler hört, kennt er "
            + "(IHoert) — abonniert, gesendet und gefragt wird nur, was im Vertrag steht. Funktionen trägt draußen nur das Python-SDK.", "error",
            [Gen("CQRS093", "Client.SourceGeneration/ClientVertrag.cs (Client ohne Vertrag)"),
             Gen("CQRS094", "Client.SourceGeneration/ClientVertrag.cs (C#-Client trägt Funktionen)"),
             An("CQRS095", "Client.SourceGeneration/ClientVertrag.cs (Command nicht gesendet)"),
             An("CQRS096", "Client.SourceGeneration/ClientVertrag.cs (Query nicht gefragt)"),
             An("CQRS097", "Client.SourceGeneration/ClientVertrag.cs (Server-Event nicht Kenntnis)")]),
        new("GR-GETRAGEN", "Jede Funktion draußen hat einen Träger", "Eine Funktion, die als Schnittstelle deklariert ist und keine Umsetzung im Host "
            + "hat, wird von mindestens einem Client getragen (ITraegt<F>) — sonst läuft sie nur mit einer Bindung von Hand.", "warning",
            [An("CQRS087", "Infrastructure.SourceGeneration/GesamtbildAnalyzer.cs (Warnung — Funktion ohne Träger)")]),
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
        new(Transient, Funktion, Beliebig, "GR-TRANSIENT"),
        new(Query, Reader, GenauEins, "GR-QUERY"), new(Query, Aggregat, GenauEins, "GR-QUERY"),
        new(Faehigkeit, Projektion, Beliebig, "GR-FAEHIGKEIT"), new(Faehigkeit, Reader, Beliebig, "GR-FAEHIGKEIT"),
        new(Faehigkeit, Funktion, Beliebig, "GR-FUNKTION-FORM"),
        new(Dienst, Funktion, Beliebig, "GR-DIENST"), new(Dienst, DienstImpl, Beliebig, "GR-DIENST"),
        new(Event, Client, Beliebig, "GR-EVENT"), new(Transient, Client, Beliebig, "GR-TRANSIENT"),
        new(Frist, Uhr, GenauEins, "GR-FRIST"),
    ];

    /// <summary>Wer welche Sorten erzeugen darf (§2.2 Spalte „Ausgänge").</summary>
    public static readonly IReadOnlyList<Erzeugung> Erzeugungen =
    [
        new(Aggregat, Event, "GR-AUS-AGGREGAT"), new(Aggregat, Transient, "GR-AUS-AGGREGAT"), new(Aggregat, Frist, "GR-AUS-AGGREGAT"),
        new(Funktion, Command, "GR-FUNKTION-FORM"), new(Funktion, Event, "GR-FUNKTION-FORM"), new(Funktion, Transient, "GR-FUNKTION-FORM"),
        new(Reader, Response, "GR-AUS-READER"), new(Aggregat, Response, "GR-AUS-AGGREGAT"),
        new(Store, Faehigkeit, "GR-FAEHIGKEIT"),
        new(DienstImpl, Dienst, "GR-DIENST"),
        new(Aussenwelt, Command, "GR-COMMAND"), new(Aussenwelt, Query, "GR-QUERY"),
        new(Akteur, Command, "GR-AKTEUR"), new(Akteur, Query, "GR-AKTEUR"), new(Akteur, Transient, "GR-AKTEUR"),
        new(Akteur, Quelle, "GR-AKTEUR"), new(Aussenwelt, Quelle, "GR-QUELLE"),
        new(Client, Command, "GR-CLIENT-BEFUGT"), new(Client, Query, "GR-CLIENT-BEFUGT"), new(Client, Transient, "GR-CLIENT-BEFUGT"),
        new(Client, Quelle, "GR-CLIENT-BEFUGT"), new(Client, Event, "GR-CLIENT-BEFUGT"),   // Quell-Nachricht (JSON-Eingang); Events als Ausgabe getragener Funktionen
        new(Uhr, Command, "GR-FRIST"),
    ];

    /// <summary>
    /// Nachrichtensorte eines Records: aus seiner Art — ein Event mit der Basis <c>IQuellNachricht</c> (Code-Fakt der Basisliste) oder einem
    /// Treiber ist eine Quell-Nachricht.
    /// </summary>
    public static string? SorteVon(Record r) =>
        r.Kind == RecordArt.Event && (r.Treiber != null || (r.Basen?.Contains(QuellBasis) ?? false)) ? Quelle : SorteVonRecordArt(r.Kind);

    /// <summary>Die Vertrags-Basis einer Quell-Nachricht (aus dem Vertrag, kein Text-Raten).</summary>
    public static readonly string QuellBasis = nameof(Abstractions.IQuellNachricht);

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
        ["wcall"] = Faehigkeit, ["rcall"] = Faehigkeit, ["dienst"] = Dienst,
    };
    public static readonly IReadOnlyList<(string Schluessel, string Baustein)> EditorPortBaustein =
    [
        ("dec", Aggregat), ("app", Aggregat), ("proj", Projektion), ("reader", Reader), ("akt", Akteur), ("client", Client),
        ("fk", Funktion), ("dn", DienstImpl),
    ];
    /// <summary>
    /// Editor-Ports, an denen der Baustein des AUSGANGS die Sorte GEBRAUCHT statt sie zu erzeugen: Auf/Frage → Store-Fähigkeit (der Port
    /// sitzt am Eingang, die Fähigkeit selbst erzeugt der Store). Geprüft wird dort der Konsum (Sorte × Baustein des Ausgangs).
    /// </summary>
    public static readonly IReadOnlyList<string> EditorPortGebrauch = ["wcall", "rcall", "dienst"];

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
        quellBasis = QuellBasis,
        portSorte = EditorPortSorte,
        portBaustein = EditorPortBaustein.Select(x => new[] { x.Schluessel, x.Baustein }),
        portGebrauch = EditorPortGebrauch,
        katalogSorte = EditorKatalogSorte,
        // Für die Modul-Ableitung im Editor (dieselbe wie DomainEditor.Module): Sorten mit Konsumenten-/Erzeuger-Pflicht.
        offenEin = Module.BrauchtKonsument.Order(StringComparer.Ordinal),
        offenAus = Module.BrauchtErzeuger.Order(StringComparer.Ordinal),
    };
}
