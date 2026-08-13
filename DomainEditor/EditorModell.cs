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
    /// <summary>Die Komposition: Aggregate (State + Decider + Applier), die die Records verdrahten.</summary>
    public IReadOnlyList<Aggregat> Aggregate { get; init; } = [];
    public IReadOnlyList<Saga> Sagas { get; init; } = [];

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
}

/// <summary>Ein Enum-Typ der Domäne (→ <c>Enums.cs</c>).</summary>
public sealed record Enumeration
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public IReadOnlyList<string> Werte { get; init; } = [];
    public string? Doku { get; init; }
}

/// <summary>
/// Die KOMPOSITION: ein Aggregat verdrahtet die Records. Es hält den State (Felder) und die
/// Regeln — Decider (Command → OneOf-Events) und Applier (Event → State-Faltung).
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
    public IReadOnlyList<DecideRegel> Decider { get; init; } = [];
    public IReadOnlyList<ApplyRegel> Applier { get; init; } = [];
}

/// <summary>Eine Decide-Regel: welcher Command → welche OneOf-Events (+ optionaler Körper).</summary>
public sealed record DecideRegel
{
    /// <summary>Name des Command-Records.</summary>
    public required string Command { get; init; }
    /// <summary>Die OneOf-Ausgänge (Event-/Ablehnungs-Record-Namen + optionaler Guard) in Reihenfolge.</summary>
    public IReadOnlyList<Ausgang> Ergibt { get; init; } = [];
    /// <summary>Optionaler Decide-Körper; null ⇒ kompilierbarer <c>throw</c>-Platzhalter.</summary>
    public string? Rumpf { get; init; }
}

/// <summary>Ein OneOf-Ausgang: der Event-Record-Name plus optional der Guard (das „Warum").</summary>
public sealed record Ausgang
{
    public required string Event { get; init; }
    public string? Guard { get; init; }
}

/// <summary>Eine Apply-Regel: welcher (persistente) Event faltet den State wie (+ optionaler Körper).</summary>
public sealed record ApplyRegel
{
    /// <summary>Name des Event-Records.</summary>
    public required string Event { get; init; }
    public string? Rumpf { get; init; }
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
}

/// <summary>Eine Saga-Transition: <c>Auf/Und → Sende → RückgängigDurch</c>.</summary>
public sealed record SagaSchritt
{
    public IReadOnlyList<string> Wenn { get; init; } = [];
    public required string Sende { get; init; }
    public IReadOnlyList<string>? SendeArgumente { get; init; }
    public string? Kompensation { get; init; }
    public IReadOnlyList<string>? KompensationArgumente { get; init; }
}

/// <summary>Vorgeschlagene skalare Feldtypen (nur Vorschläge fürs Dropdown; der Typ ist frei).</summary>
public static class Skalar
{
    public static readonly IReadOnlyList<string> Vorschlaege =
        ["Guid", "decimal", "int", "long", "double", "bool", "string", "DateTimeOffset"];
}
