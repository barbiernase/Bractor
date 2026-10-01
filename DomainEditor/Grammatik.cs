namespace DomainEditor;

/// <summary>
/// Die GRAMMATIK der Kompositions-Sprache (<c>docs/konzept-editor-komposition.md</c> §3) als EINE Quelle: welche Nachrichtensorte
/// in welchen Baustein-Eingang darf (mit Kardinalität), welcher Baustein welche Sorten erzeugen darf, und die Zusatzregeln — je
/// Regel mit ihrem Gegenstück im Build (Analyzer-/Generator-ID, Compiler, Boot-Guard oder „offen").
///
/// Sie speist drei Stellen, die sonst auseinanderlaufen würden:
///   • den <see cref="Validator"/> (nennt die verletzte Regel beim Namen),
///   • den Editor (GraphExtractor legt sie als <c>rahmen.grammatik</c> ins Board; der Verbinden-Modus bietet nur gültige Ziele an),
///   • die Liste „Regel → Build-Gegenstück" (<c>--check</c> prüft, dass jede genannte ID im Code existiert).
///
/// Leitsatz: <b>Wenn es sich verbinden lässt, kompiliert es.</b> Die Grammatik darf enger sein als der Compiler, nie weiter.
/// Rein, Roslyn-frei, ohne Domänen-Namen.
/// </summary>
public static class Grammatik
{
    // ── Alphabet: Nachrichtensorten (die Kanten) ──────────────────────────────────────────────────────────────────────
    public const string Command = "command", Event = "event", Transient = "transient", Trigger = "trigger", Selbst = "selbst",
        Frist = "frist", Query = "query", Response = "response", Faehigkeit = "faehigkeit";

    // ── Alphabet: Bausteine (die Knoten) ─────────────────────────────────────────────────────────────────────────────
    public const string Aggregat = "aggregat", Prozess = "prozess", Projektion = "projektion", Reaktion = "reaktion", Reader = "reader",
        Pipeline = "pipeline", Ingress = "ingress", Store = "store", Aussenwelt = "aussenwelt";

    /// <summary>Kardinalität eines Konsum-Eingangs.</summary>
    public const string GenauEins = "eins", Beliebig = "beliebig", Dieselbe = "dieselbe";

    public sealed record SorteInfo(string Id, string Name, string Symbol, string Garantie, string Bedeutung);
    public sealed record BausteinInfo(string Id, string Name, bool Zustand);
    /// <summary>Nachricht der Sorte <see cref="Sorte"/> darf in einen Eingang des Bausteins <see cref="Baustein"/> (Kardinalität je Nachricht).</summary>
    public sealed record Konsum(string Sorte, string Baustein, string Kardinalitaet, string Regel);
    /// <summary>Der Baustein darf Nachrichten dieser Sorte erzeugen (Ausgang).</summary>
    public sealed record Erzeugung(string Baustein, string Sorte, string Regel);
    /// <summary>Das Gegenstück einer Regel im Build: Art (analyzer/generator/compiler/boot/laufzeit/offen), Kennung (z. B. CQRS010), Ort.</summary>
    public sealed record Gegenstueck(string Art, string? Kennung, string Ort);
    public sealed record Regel(string Id, string Name, string Text, string Schwere, IReadOnlyList<Gegenstueck> Build);

    public static readonly IReadOnlyList<SorteInfo> Sorten =
    [
        new(Command, "Command", "▶", "wie der Absender (ab Event idempotent)", "Absicht an genau ein Aggregat"),
        new(Event, "Event", "◆", "durabel", "Fakt im Log"),
        new(Transient, "Transientes Event", "◇", "verlierbar", "Hinweis, Ablehnung"),
        new(Trigger, "Trigger", "⚡", "verlierbar", "Anstoß von außen oder aus einer Pipeline"),
        new(Selbst, "Selbst", "↺", "verlierbar", "Zeit, lokal (Tick)"),
        new(Frist, "Frist", "⏳", "durabel", "Zeit, dauerhaft (Deadline) → Command"),
        new(Query, "Query", "?", "synchron", "Lesen"),
        new(Response, "Response", "↩", "synchron", "Antwort eines Readers"),
        new(Faehigkeit, "Fähigkeit", "⚙", "im Handle-Aufruf", "Store-Funktion (Lesen/Schreiben)"),
    ];

    public static readonly IReadOnlyList<BausteinInfo> Bausteine =
    [
        new(Aggregat, "Aggregat", true), new(Prozess, "Prozess", true), new(Projektion, "Projektion", false),
        new(Reaktion, "Reaktion", false), new(Reader, "Reader", false), new(Pipeline, "Pipeline", false),
        new(Ingress, "Ingress", false), new(Store, "Store", true), new(Aussenwelt, "Außenwelt", false),
    ];

    // ── Regeln (Id = der Name, unter dem Validator und Editor sie melden) ──────────────────────────────────────────────
    private static Gegenstueck An(string id, string ort) => new("analyzer", id, ort);
    private static Gegenstueck Gen(string id, string ort) => new("generator", id, ort);
    private static Gegenstueck Comp(string ort) => new("compiler", null, ort);
    private static Gegenstueck Offen(string ort) => new("offen", null, ort);

    public static readonly IReadOnlyList<Regel> Regeln =
    [
        // Konsum: Sorte → Eingang
        new("GR-COMMAND", "Command → genau ein Aggregat", "Ein Command wird von genau einem Aggregat entschieden.", "error",
            [Gen("CQRS010", "Infrastructure.SourceGeneration/CommandAggregateMapGenerator.cs"), Gen("CQRS002", "Domain.SourceGeneration/ProzessRegelDiagnosticGenerator.cs (Prozess sendet Command ohne Decider)")]),
        new("GR-EVENT", "Event → beliebig viele Konsumenten", "Ein persistentes Event hören Prozess, Projektion, Reaktion und Pipeline (beliebig viele); gefaltet wird es im eigenen Aggregat.", "error",
            [Comp("Handle-Parameter : IEvent, Prozess-DSL Auf<T> where T : IEvent"), An("CQRS057", "Domain.SourceGeneration (HandlerFormAnalyzer)")]),
        new("GR-FALTUNG", "Event → genau ein Aggregat faltet", "Ein Event faltet genau ein Aggregat (Applier) — das, das es entscheidet.", "warning",
            [Offen("keine Diagnose; der Aggregat-Generator faltet je State")]),
        new("GR-TRANSIENT", "Transientes Event → Projektion/Reaktion/Pipeline", "Ein transientes Event (Hinweis, Ablehnung) hören Projektion, Reaktion und Pipeline — kein Prozess, kein Aggregat.", "error",
            [Comp("Handle-Parameter : ITransientEvent"), An("CQRS057", "Domain.SourceGeneration (HandlerFormAnalyzer)")]),
        new("GR-TRIGGER", "Trigger → genau eine Pipeline", "Einen Trigger behandelt genau eine Pipeline.", "error",
            [Offen("PipelineActorGenerator: bei zwei Pipelines gewinnt heute die letzte still")]),
        new("GR-SELBST", "Selbst → dieselbe Pipeline", "Eine Selbst-Nachricht kommt nur in der Pipeline an, die sie plant.", "error",
            [Offen("PipelineActorBase.PlaneAsync schickt an sich selbst; keine Diagnose für fremde Selbst-Handles")]),
        new("GR-FRIST", "Frist → Command an genau ein Aggregat", "Eine Frist wird fällig als Command an genau ein Aggregat.", "error",
            [An("CQRS056", "Domain.SourceGeneration (Fristen-Router)"), Gen("CQRS010", "Infrastructure.SourceGeneration/CommandAggregateMapGenerator.cs")]),
        new("GR-QUERY", "Query → genau ein Reader", "Eine Query beantwortet genau ein Reader.", "error",
            [Offen("keine Diagnose im Query-Dispatch")]),
        new("GR-FAEHIGKEIT", "Fähigkeit → genau ein Store", "Eine Fähigkeit (Interface mit genau einer Funktion) implementiert genau eine Store-Klasse.", "error",
            [Gen("CQRS053", "Projections.SourceGeneration/ProjectionServicesGenerator.cs"), An("CQRS051", "Domain.SourceGeneration (FaehigkeitAnalyzer)")]),
        // Erzeugung: Baustein-Ausgang → Sorte
        new("GR-AUS-AGGREGAT", "Aggregat erzeugt Events", "Ein Aggregat (Decide) erzeugt persistente Events und Ablehnungen (transient) — sonst nichts.", "error",
            [An("CQRS050", "Domain.SourceGeneration (Ausgabe-Vertrag)")]),
        new("GR-AUS-PROZESS", "Prozess erzeugt Commands", "Ein Prozess erzeugt Commands (und Kompensations-Commands).", "error",
            [Gen("CQRS003", "Domain.SourceGeneration/ProzessRegelDiagnosticGenerator.cs"), Comp("Sende<TCmd> where TCmd : ICommand")]),
        new("GR-AUS-PROJEKTION", "Projektion erzeugt nur Transientes", "Eine Projektion schreibt über Fähigkeiten und veröffentlicht höchstens transiente Events — kein Command (dann ist sie eine Reaktion), kein Fakt.", "error",
            [An("CQRS050", "Domain.SourceGeneration (Ausgabe-Vertrag)"), Gen("CQRS052", "Infrastructure.SourceGeneration/PullPathGenerator.cs (ein Store je Konsument)")]),
        new("GR-AUS-REAKTION", "Reaktion erzeugt Commands", "Eine Reaktion erzeugt Commands und transiente Events — keine Fakten.", "error",
            [An("CQRS050", "Domain.SourceGeneration (Ausgabe-Vertrag)"), An("CQRS020", "Emit nur über CommandEmitter")]),
        new("GR-AUS-READER", "Reader erzeugt Responses", "Ein Reader antwortet mit Responses (OneOf) und liest über Lese-Fähigkeiten.", "error",
            [An("CQRS050", "Domain.SourceGeneration (Ausgabe-Vertrag)")]),
        new("GR-AUS-PIPELINE", "Pipeline erzeugt Commands/Trigger/Zeit", "Eine Pipeline erzeugt Command, Trigger, transientes Event, Selbst und Frist (und liest über Lese-Fähigkeiten).", "error",
            [An("CQRS050", "Domain.SourceGeneration (Ausgabe-Vertrag)"), An("CQRS057", "Domain.SourceGeneration (HandlerFormAnalyzer)")]),
        new("GR-AUS-INGRESS", "Ingress erzeugt Trigger", "Ein Ingress (Webhook, Timer, Datei) erzeugt genau Trigger-Nachrichten.", "error",
            [Comp("[Ingress]-Methoden: Trigger-Typ : IPipelineTrigger")]),
        // Zusatzregeln
        new("GR-AUSGANG-GESCHLOSSEN", "Geschlossener Ausgang", "Ausgänge sind konkrete Typen im OneOf — zeichnen heißt Typ wählen.", "error",
            [An("CQRS050", "Domain.SourceGeneration (Ausgabe-Vertrag)")]),
        new("GR-HANDLE-FORM", "Handle-Form folgt dem Inhalt", "Keine Ausgabe ⇒ Task, sonst Strom mit OneOf.", "error",
            [An("CQRS057", "Domain.SourceGeneration (HandlerFormAnalyzer)")]),
        new("GR-SELBST-OHNE-EVENT", "Selbst nur ohne Event-Eingang", "Ein Handle mit Event-Eingang hat keine Mailbox — er darf kein Selbst<T> planen.", "error",
            [Offen("Laufzeit: NotSupportedException in PipelineEventPullBridge; Analyzer offen")]),
        new("GR-KEIN-EVENT-AUS-PIPELINE", "Kein persistentes Event aus Pipelines", "Fakten entstehen nur im Aggregat — eine Pipeline erzeugt kein persistentes Event.", "error",
            [Offen("PipelineDispatchGenerator verwirft es heute still; Analyzer offen")]),
        new("GR-FRIST-CTOR", "Frist-Command braucht (Guid)-Konstruktor", "Der Command einer Frist wird mit der Aggregat-Id gebaut — er braucht einen Konstruktor (Guid).", "warning",
            [An("CQRS056", "Domain.SourceGeneration (Fristen-Router)")]),
        new("GR-START-EINMAL", "Start höchstens einmal", "Eine Pipeline hat höchstens einen Start-Handle (PipelineGestartet).", "error",
            [Comp("CS0111 (zwei Handle-Methoden gleicher Signatur)")]),
        new("GR-GARANTIE", "Garantie-Regel", "Führt ein verlierbarer Pfad (Trigger, Selbst, transientes Event) in einen Schritt, der Durabilität braucht (Command), ist die Kante verlierbar/nicht idempotent.", "warning",
            [Offen("Laufzeit: Kausalität ab Trigger/Selbst = neue Guid (PipelineActorBase); Editor zeigt es gestrichelt")]),
        new("GR-ZYKLUS", "Zyklus nur durch einen Zustandsschritt", "Ein Kreis ist erlaubt, wenn er durch ein Aggregat läuft (Selbst-Schleifen sind der Schleifen-Operator); sonst nicht.", "warning",
            [new("boot", null, "Azyklizitäts-Guard (ProzessManagerWiring) — nur Prozesse"), Offen("Reaktionen/Pipelines")]),
        new("GR-ZUSTAND", "Regel Z: Zustand nur in Aggregaten und Lesemodellen", "Pipelines, Reaktionen und Projektionen sind zustandslose Übersetzer; Gedächtnis gehört in ein Aggregat.", "info",
            [Offen("Hinweis im Validator; Analyzer bewusst noch nicht gebaut")]),
        new("GR-MODUL-EINGANG-OFFEN", "Eingang ohne Konsument", "Ein Command, eine Query, ein Trigger oder eine Selbst-Nachricht, die niemand konsumiert — offener Eingang des Moduls.", "warning",
            [Gen("CQRS002", "nur: Prozess sendet Command ohne Decider"), Offen("sonst")]),
        new("GR-MODUL-AUSGANG-OFFEN", "Ausgang ohne Erzeuger", "Ein Event, eine Ablehnung oder eine Response, die niemand erzeugt — offener Ausgang des Moduls.", "warning",
            [Offen("nur Graph-Diagnose UNUSED-EVENT des Extractors")]),
    ];

    /// <summary>Die Tabelle aus §3: Sorte × Baustein-Eingang (mit Kardinalität je Nachricht).</summary>
    public static readonly IReadOnlyList<Konsum> Konsume =
    [
        new(Command, Aggregat, GenauEins, "GR-COMMAND"),
        new(Event, Prozess, Beliebig, "GR-EVENT"), new(Event, Projektion, Beliebig, "GR-EVENT"),
        new(Event, Reaktion, Beliebig, "GR-EVENT"), new(Event, Pipeline, Beliebig, "GR-EVENT"),
        new(Event, Aggregat, GenauEins, "GR-FALTUNG"),
        new(Transient, Projektion, Beliebig, "GR-TRANSIENT"), new(Transient, Reaktion, Beliebig, "GR-TRANSIENT"), new(Transient, Pipeline, Beliebig, "GR-TRANSIENT"),
        new(Trigger, Pipeline, GenauEins, "GR-TRIGGER"),
        new(Selbst, Pipeline, Dieselbe, "GR-SELBST"),
        new(Frist, Aggregat, GenauEins, "GR-FRIST"),
        new(Query, Reader, GenauEins, "GR-QUERY"),
        new(Faehigkeit, Projektion, Beliebig, "GR-FAEHIGKEIT"), new(Faehigkeit, Reader, Beliebig, "GR-FAEHIGKEIT"), new(Faehigkeit, Pipeline, Beliebig, "GR-FAEHIGKEIT"),
    ];

    /// <summary>Wer welche Sorten erzeugen darf (§2.2 Spalte „Ausgänge").</summary>
    public static readonly IReadOnlyList<Erzeugung> Erzeugungen =
    [
        new(Aggregat, Event, "GR-AUS-AGGREGAT"), new(Aggregat, Transient, "GR-AUS-AGGREGAT"),
        new(Prozess, Command, "GR-AUS-PROZESS"),
        new(Projektion, Transient, "GR-AUS-PROJEKTION"),
        new(Reaktion, Command, "GR-AUS-REAKTION"), new(Reaktion, Transient, "GR-AUS-REAKTION"),
        new(Reader, Response, "GR-AUS-READER"),
        new(Pipeline, Command, "GR-AUS-PIPELINE"), new(Pipeline, Trigger, "GR-AUS-PIPELINE"), new(Pipeline, Transient, "GR-AUS-PIPELINE"),
        new(Pipeline, Selbst, "GR-AUS-PIPELINE"), new(Pipeline, Frist, "GR-AUS-PIPELINE"),
        new(Ingress, Trigger, "GR-AUS-INGRESS"),
        new(Store, Faehigkeit, "GR-FAEHIGKEIT"),
        new(Aussenwelt, Command, "GR-COMMAND"), new(Aussenwelt, Query, "GR-QUERY"), new(Aussenwelt, Trigger, "GR-AUS-INGRESS"),
    ];

    /// <summary>Record-Art → Nachrichtensorte (null = keine Nachricht: Value Object, Konfig, ReadModel).</summary>
    public static string? SorteVonRecordArt(string kind) => kind switch
    {
        RecordArt.Command => Command, RecordArt.Event => Event, RecordArt.Rejection => Transient, RecordArt.Query => Query,
        RecordArt.Antwort => Response, RecordArt.Trigger => Trigger, RecordArt.Selbst => Selbst, _ => null,
    };

    public static Regel RegelVon(string id) => Regeln.First(r => r.Id == id);
    public static Konsum? KonsumVon(string sorte, string baustein) => Konsume.FirstOrDefault(k => k.Sorte == sorte && k.Baustein == baustein);
    public static Erzeugung? ErzeugungVon(string baustein, string sorte) => Erzeugungen.FirstOrDefault(e => e.Baustein == baustein && e.Sorte == sorte);
    public static string SorteName(string sorte) => Sorten.FirstOrDefault(s => s.Id == sorte)?.Name ?? sorte;
    public static string BausteinName(string baustein) => Bausteine.FirstOrDefault(b => b.Id == baustein)?.Name ?? baustein;

    /// <summary>Die Regel verletzt: der lesbare Satz „Regel »Name« (Id; Build: …)" für Validator und Editor.</summary>
    public static string Beschreibe(string regelId)
    {
        var r = RegelVon(regelId);
        return $"Regel »{r.Name}« ({r.Id}; Build: {BuildText(r)})";
    }

    public static string BuildText(Regel r) => string.Join(", ", r.Build.Select(g => g.Kennung ?? (g.Art == "offen" ? "offen" : g.Art)).Distinct());

    /// <summary>
    /// Die Abbildung der EDITOR-Ports auf die Sprache — damit das Editor-JS nichts hart kodiert: Port-Typ → Sorte (für Ports,
    /// die nicht an einer Nachrichten-Karte hängen), Port-Schlüssel → Baustein (die Info-Felder der Ports: <c>dec</c>, <c>proj</c> …).
    /// Der alte Frist-Knoten (Composition-Root-Sicht) ist der Frist-Ausgang einer Pipeline.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> EditorPortSorte = new Dictionary<string, string>
    {
        ["trigmsg"] = Trigger, ["self"] = Selbst, ["wcall"] = Faehigkeit, ["rcall"] = Faehigkeit,
    };
    public static readonly IReadOnlyList<(string Schluessel, string Baustein)> EditorPortBaustein =
    [
        ("dec", Aggregat), ("app", Aggregat), ("trans", Prozess), ("saga", Prozess), ("proj", Projektion), ("reaktion", Reaktion),
        ("pipeline", Pipeline), ("reader", Reader), ("trigId", Ingress), ("frist", Pipeline),
    ];
    /// <summary>Editor-Port-Schlüssel, deren Ausgang eine andere Sorte trägt als die Ziel-Karte (Frist-Knoten → Command = Sorte Frist).</summary>
    public static readonly IReadOnlyDictionary<string, string> EditorAusgangSorte = new Dictionary<string, string> { ["frist"] = Frist };

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
        ausgangSorte = EditorAusgangSorte,
        portBaustein = EditorPortBaustein.Select(x => new[] { x.Schluessel, x.Baustein }),
        // Für die Modul-Ableitung im Editor (dieselbe wie DomainEditor.Module): Sorten mit Konsumenten-/Erzeuger-Pflicht, Start-Nachricht.
        offenEin = Module.BrauchtKonsument.Order(StringComparer.Ordinal),
        offenAus = Module.BrauchtErzeuger.Order(StringComparer.Ordinal),
        start = nameof(Abstractions.PipelineGestartet),
    };
}
