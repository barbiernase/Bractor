using System.Text.Json;
using System.Text.Json.Serialization;

namespace DomainEditor;

/// <summary>
/// Das Domänen-Modell als AUSFÜHRBARE DATEN — RECORD-ZENTRISCH (v2).
///
/// Die Kern-Einsicht: Commands, Events, Ablehnungen und Value Objects sind ALLE Records
/// (Name + typisierte Felder). Deshalb gibt es EINEN uniformen Record-Editor statt bespoke
/// Formulare je Bausteinart. Ein Feldtyp darf ein anderer Record/Enum sein — so KOMPONIEREN
/// sich Records (z. B. <c>BildInfo</c> enthält <c>BildMeta</c> und <c>List&lt;RegionBewertung&gt;</c>).
///
/// Aus den getaggten Records KOMPONIERT man dann die Aggregate: State (Felder) + Decider
/// (Command → OneOf-Events) + Applier (Event → State). Der <see cref="Scaffolder"/> erzeugt daraus
/// deterministisch C# in kanonischer Gestalt (Commands.cs / Events.cs / ValueObjects.cs / Enums.cs
/// / {Aggregat}.cs / Decider.cs / Applier.cs).
/// </summary>
public sealed record EditorModell
{
    public string SchemaVersion { get; init; } = "2";

    /// <summary>Alle Records der Domäne — Commands, Events, Ablehnungen, Value Objects (kind-getaggt).</summary>
    public IReadOnlyList<Record> Records { get; init; } = [];
    /// <summary>Enums der Domäne (auch Feldtypen), z. B. <c>BildVersion { Dc0, Dc2 }</c>.</summary>
    public IReadOnlyList<Enumeration> Enums { get; init; } = [];
    /// <summary>Aggregate = Konsistenzgrenzen; halten den State und nehmen Decider/Applier entgegen.</summary>
    public IReadOnlyList<Aggregat> Aggregate { get; init; } = [];
    /// <summary>Decider (Command → OneOf-Events) — je Regel EIGENSTÄNDIG, referenziert sein Aggregat.</summary>
    public IReadOnlyList<DecideRegel> Decider { get; init; } = [];
    /// <summary>Applier (Event → State-Faltung) — je Regel EIGENSTÄNDIG, referenziert sein Aggregat.</summary>
    public IReadOnlyList<ApplyRegel> Applier { get; init; } = [];
    public IReadOnlyList<Saga> Sagas { get; init; } = [];
    /// <summary>Der aus dem Code abgeleitete Rahmen (Vertrags-Namespace, globale usings, Namenskonvention, Verzeichnisse).</summary>
    public Rahmen Rahmen { get; init; } = new();

    // Serialisierung: camelCase, Enums als String, Nulls weglassen — wie knowledge-graph.json.
    public static readonly JsonSerializerOptions JsonOptionen = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    public string AlsJson() => JsonSerializer.Serialize(this, JsonOptionen);

    public static EditorModell AusJson(string json) =>
        JsonSerializer.Deserialize<EditorModell>(json, JsonOptionen)
        ?? throw new FormatException("Konnte EditorModell nicht aus JSON lesen (null).");
}

/// <summary>Die Rolle eines Records — bestimmt Interface und Zieldatei.</summary>
public static class RecordArt
{
    public const string Command = "command";        // : ICommand → Commands.cs
    public const string Event = "event";            // : IEvent → Events.cs (persistent)
    public const string Rejection = "rejection";    // : ITransientEvent → Events.cs (Ablehnung)
    public const string ValueObject = "valueobject";// reiner Record → ValueObjects.cs

    public static readonly IReadOnlyList<string> Alle = [Command, Event, Rejection, ValueObject];
}

/// <summary>
/// Ein Record: Name + Kind (<see cref="RecordArt"/>) + typisierte Felder. Der uniforme Baustein für
/// Commands, Events, Ablehnungen und Value Objects. Felder dürfen andere Records/Enums referenzieren.
/// </summary>
public sealed record Record
{
    public required string Name { get; init; }
    /// <summary>Siehe <see cref="RecordArt"/>: command | event | rejection | valueobject.</summary>
    public required string Kind { get; init; }
    /// <summary>Voller Namespace, z. B. <c>Domain.ImagePair</c> — bestimmt die Zieldatei-Gruppierung.</summary>
    public required string Namespace { get; init; }
    public IReadOnlyList<Feld> Felder { get; init; } = [];
    public string? Doku { get; init; }
    /// <summary>Nur command: erzeugt das Aggregat (<c>ICreationCommand</c> statt nur <c>ICommand</c>).</summary>
    public bool IstErzeugung { get; init; }
    /// <summary>
    /// Zusätzliche Member im Record-Rumpf als roher C#-Text (z. B. <c>static Default</c>, abgeleitete
    /// Props) — Handcode, den der Scaffolder verbatim in <c>{ … }</c> einhängt. Null ⇒ <c>record X(…);</c>.
    /// </summary>
    public string? Zusatz { get; init; }
    /// <summary>Zusätzliche <c>using</c>-Namespaces, die der Handcode (Zusatz) braucht (ohne <c>using</c>/<c>;</c>).</summary>
    public IReadOnlyList<string> Usings { get; init; } = [];
    /// <summary>Die Quelldatei (relativ zur Solution), in der der Record steht — null = noch nicht geschrieben.</summary>
    public string? Datei { get; init; }
    /// <summary>
    /// Das Aggregat, zu dem der Record GEHÖRT — aus dem Code: der Command, den genau ein Decider entscheidet; das Event,
    /// das genau ein Aggregat erzeugt/faltet. Null = keinem (Value Object, geteilt, oder im Editor noch nicht verdrahtet).
    /// </summary>
    public string? Aggregat { get; init; }
    /// <summary>Die Deklarationsform verbatim ohne Namen, z. B. <c>public sealed record</c>, <c>public record struct</c>. Null = <c>public record</c>.</summary>
    public string? Typart { get; init; }
    /// <summary>Record ohne Parameterliste (<c>record X : I { … }</c> statt <c>record X(…) : I</c>).</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool OhneParameterliste { get; init; }
    /// <summary>Basistypen AUSSER dem Rollen-Marker, verbatim (z. B. weitere Interfaces).</summary>
    public IReadOnlyList<string>? Basen { get; init; }
    /// <summary>Attribut-Listen des Typs verbatim (mehrere zeilengetrennt).</summary>
    public string? Attribute { get; init; }
}

/// <summary>
/// Ein Feld = Name + Typ. Der Typ ist C#-Text: ein Skalar (<see cref="Skalar"/>), ein anderer
/// Record/Enum der Domäne (Komposition), ein Framework-Typ (<c>DateTimeOffset</c> …), nullable
/// (<c>Klassifikation?</c>) oder eine Collection (<c>List&lt;RegionBewertung&gt;</c>). Verbatim übernommen.
/// </summary>
public sealed record Feld
{
    public required string Name { get; init; }
    public required string Typ { get; init; }
    /// <summary>Optionaler Default (nur Command-Felder), z. B. <c>false</c> — roher C#-Literal.</summary>
    public string? Standard { get; init; }
    /// <summary>Nur State-Felder: gesetzt ⇒ abgeleitete Read-only-Property (<c>=> Ausdruck</c>), kein gespeichertes Feld.</summary>
    public string? Ausdruck { get; init; }
    /// <summary>Nur State-Felder: <c>{ get; }</c> statt <c>{ get; set; }</c> (z. B. mit <see cref="Standard"/> <c>new()</c>).</summary>
    public bool NurGet { get; init; }
    /// <summary>Nur Record-Felder: als Property deklariert, Accessor-Satz z. B. <c>{ get; init; }</c>. Null = Positions-Parameter.</summary>
    public string? Zugriff { get; init; }
    /// <summary>Property mit <c>required</c>.</summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    public bool Pflicht { get; init; }
    /// <summary>Sammlungs-Feld: der Element-Typ (vom Extractor per Symbol bestimmt). Null = keine Sammlung bzw. noch nicht übersetzt.</summary>
    public string? ElementTyp { get; init; }
}

/// <summary>Ein Enum-Typ der Domäne (→ <c>Enums.cs</c>).</summary>
public sealed record Enumeration
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public IReadOnlyList<string> Werte { get; init; } = [];
    public string? Doku { get; init; }
    public string? Datei { get; init; }
}

/// <summary>
/// Ein Aggregat = eine Konsistenzgrenze. Hält NUR den State (Felder). Decider und Applier sind
/// eigenständige Bausteine (<see cref="DecideRegel"/>/<see cref="ApplyRegel"/>), die IHR Aggregat per
/// Namen referenzieren — das Aggregat „nimmt sie entgegen".
/// </summary>
public sealed record Aggregat
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public string? Doku { get; init; }
    /// <summary>State-Felder (mit optionalem <see cref="Feld.Ausdruck"/> für abgeleitete Props).</summary>
    public IReadOnlyList<Feld> State { get; init; } = [];
    /// <summary>Zusätzliche State-Member als roher C#-Text (Hilfsmethoden), in die Klasse eingehängt.</summary>
    public string? StateZusatz { get; init; }
    /// <summary>Private Hilfs-Member der Decider-Klasse (roher C#-Text), hinter den Decide-Methoden eingehängt.</summary>
    public string? DeciderZusatz { get; init; }
    /// <summary>Private Hilfs-Member der Applier-Klasse (roher C#-Text), hinter den Apply-Methoden eingehängt.</summary>
    public string? ApplierZusatz { get; init; }
    /// <summary>Zusätzliche <c>using</c>-Namespaces der Aggregat-Dateien (State/Decider/Applier), z. B. für Hilfstypen in Rümpfen.</summary>
    public IReadOnlyList<string> Usings { get; init; } = [];
    /// <summary>Quelldateien (relativ zur Solution): State, Decider, Applier — null = noch nicht geschrieben.</summary>
    public string? Datei { get; init; }
    public string? DeciderDatei { get; init; }
    public string? ApplierDatei { get; init; }
}

/// <summary>
/// Ein Decider (eigenständig): entscheidet für sein <see cref="Aggregat"/> einen Command zu OneOf-
/// Events. Command → Decider → Events, und Decider → Aggregat.
/// </summary>
public sealed record DecideRegel
{
    /// <summary>Name des Aggregats, zu dem dieser Decider gehört.</summary>
    public required string Aggregat { get; init; }
    /// <summary>Name des Command-Records.</summary>
    public string Command { get; init; } = "";
    /// <summary>Die OneOf-Ausgänge (Event-/Ablehnungs-Record-Namen + optionaler Guard) in Reihenfolge.</summary>
    public IReadOnlyList<Ausgang> Ergibt { get; init; } = [];
    /// <summary>Optionaler Decide-Körper; null ⇒ kompilierbarer <c>throw</c>-Platzhalter, <c>""</c> ⇒ bewusst leer.</summary>
    public string? Rumpf { get; init; }
    /// <summary>Parametername des Commands in der Signatur (der Rumpf bezieht sich darauf).</summary>
    public string Parameter { get; init; } = "cmd";
    /// <summary>Die Quelldatei der Methode (relativ zur Solution) — Anker für Code-Sync/IDE; null = noch nicht geschrieben.</summary>
    public string? Datei { get; init; }
}

/// <summary>Ein OneOf-Ausgang: der Event-Record-Name plus optional der Guard (das „Warum").</summary>
public sealed record Ausgang
{
    public required string Event { get; init; }
    public string? Guard { get; init; }
}

/// <summary>
/// Ein Applier (eigenständig): faltet für sein <see cref="Aggregat"/> einen (persistenten) Event in
/// den State. Event → Applier → Aggregat.
/// </summary>
public sealed record ApplyRegel
{
    /// <summary>Name des Aggregats, zu dem dieser Applier gehört.</summary>
    public required string Aggregat { get; init; }
    /// <summary>Name des Event-Records.</summary>
    public string Event { get; init; } = "";
    /// <summary>Optionaler Apply-Körper; null ⇒ <c>throw</c>-Platzhalter, <c>""</c> ⇒ bewusst leerer No-op.</summary>
    public string? Rumpf { get; init; }
    /// <summary>Parametername des Events in der Signatur (der Rumpf bezieht sich darauf).</summary>
    public string Parameter { get; init; } = "evt";
    public string? Datei { get; init; }
}

/// <summary>
/// Eine Saga/ein Prozess: mehrere Aggregate / asynchron / mit Kompensation. Der
/// <see cref="TriggerEvent"/> ist das <c>Prozess&lt;T&gt;</c>-Argument.
/// </summary>
public sealed record Saga
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public required string TriggerEvent { get; init; }
    public IReadOnlyList<SagaSchritt> Schritte { get; init; } = [];
    public string? Doku { get; init; }
    public IReadOnlyList<string> ExtraUsings { get; init; } = [];
    public string? Datei { get; init; }
}

/// <summary>Eine Saga-Transition: <c>Auf/Und (→ UndAlle) → Sende/SendeJe → RückgängigDurch</c>.</summary>
public sealed record SagaSchritt
{
    /// <summary>Der Join (Konjunktion): <c>Auf&lt;Wenn[0]&gt;().Und&lt;Wenn[1]&gt;()…</c>.</summary>
    public IReadOnlyList<string> Wenn { get; init; } = [];
    /// <summary>Optional Count-Join: <c>.UndAlle&lt;SammelEvent&gt;(t => SammelAnzahl)</c> — feuert erst nach ALLEN N.</summary>
    public string? SammelEvent { get; init; }
    /// <summary>Der Anzahl-Ausdruck des Count-Joins, z. B. <c>t.Ziele.Count</c>.</summary>
    public string? SammelAnzahl { get; init; }
    /// <summary>Der echte Count-Join-Lambda-Ausdruck verbatim (z. B. <c>t => t.Anzahl</c>); Vorrang vor <see cref="SammelAnzahl"/>.</summary>
    public string? SammelAusdruck { get; init; }
    public required string Sende { get; init; }
    /// <summary>Fan-out: <c>SendeJe</c> statt <c>Sende</c> — iteriert <see cref="SendeJeCollection"/> mit <see cref="SendeJeElement"/>.</summary>
    public bool SendeJe { get; init; }
    public string? SendeJeCollection { get; init; }
    public string? SendeJeElement { get; init; }
    public IReadOnlyList<string>? SendeArgumente { get; init; }
    /// <summary>
    /// Der ECHTE Sende-Lambda-Ausdruck verbatim (z. B. <c>e => new X(e.Id)</c>) — aus dem Code gelesen.
    /// Gesetzt ⇒ hat Vorrang vor <see cref="SendeArgumente"/>/<see cref="SendeJeCollection"/> (verlustfreier Round-trip).
    /// </summary>
    public string? SendeAusdruck { get; init; }
    public string? Kompensation { get; init; }
    public IReadOnlyList<string>? KompensationArgumente { get; init; }
    /// <summary>Der echte Kompensations-Lambda-Ausdruck verbatim; Vorrang vor <see cref="KompensationArgumente"/>.</summary>
    public string? KompensationAusdruck { get; init; }
    /// <summary>Kompensation als Fan-out (<c>RückgängigDurchJe</c>).</summary>
    public bool KompensationJe { get; init; }
}

/// <summary>
/// Der Rahmen, in den geschrieben wird — vom Extractor AUS DEM CODE abgeleitet, nicht im Scaffolder festgelegt:
/// Vertrags-Namespace, globale usings der Domänen-Projekte, die Namen der inneren Decider/Applier-Klassen und ihrer
/// Methoden (wie der Code sie benennt) und die Verzeichnisse je Namespace (wo die Typen tatsächlich liegen).
/// Die Klassen-/Methodennamen sind der Vertrag des Framework-Generators (<c>Abstractions.Aggregatvertrag</c>); die
/// Verzeichnisse stehen nur für Namespaces, deren Typen in genau EINEM Verzeichnis liegen.
/// </summary>
public sealed record Rahmen
{
    public string VertragsNamespace { get; init; } = typeof(Abstractions.IState).Namespace!;
    /// <summary>global usings der Domänen-Compilations (ImplicitUsings) — für In-Memory-Übersetzungen.</summary>
    public IReadOnlyList<string> GlobaleUsings { get; init; } = [];
    // Die Aggregat-Namensregel des Framework-Generators — Vertrag (Abstractions.Aggregatvertrag), nicht geschätzt.
    public string DeciderKlasse { get; init; } = Abstractions.Aggregatvertrag.Decider;
    public string ApplierKlasse { get; init; } = Abstractions.Aggregatvertrag.Applier;
    public string DecideMethode { get; init; } = Abstractions.Aggregatvertrag.Decide;
    public string ApplyMethode { get; init; } = Abstractions.Aggregatvertrag.Apply;
    /// <summary>Namespace → Verzeichnis (relativ zur Solution, „/"-getrennt). Enthält auch die Wurzel-Namespaces der Projekte.</summary>
    public IReadOnlyDictionary<string, string> Verzeichnisse { get; init; } = new Dictionary<string, string>();

    /// <summary>Kennung der Solution, aus der das Modell stammt — trennt Browser-Zwischenstände verschiedener Repos.</summary>
    public string? Kennung { get; init; }
    /// <summary>Die Skalar-Typen, die der Wire (Proto-Codegen, <c>ProtoScalarSpecs</c>) trägt — Vorschläge + Validierung.</summary>
    public IReadOnlyList<string> Skalare { get; init; } = [];
    /// <summary>Das Identitäts-Feld, das <c>ICommand</c> verlangt (<c>nameof(ICommand.AggregateId)</c>).</summary>
    public string AggregatIdFeld { get; init; } = nameof(Abstractions.ICommand.AggregateId);
    /// <summary>Größte Stelligkeit von <c>OneOf&lt;…&gt;</c> im Vertrag (aus der Compilation gezählt; 0 = unbekannt).</summary>
    public int OneOfMax { get; init; }
    /// <summary>Größte Join-Stelligkeit der Prozess-DSL (<c>RegelBauer&lt;…&gt;</c>, aus der Compilation gezählt; 0 = unbekannt).</summary>
    public int UndMax { get; init; }
}
