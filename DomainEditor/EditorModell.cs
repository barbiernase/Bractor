using System.Text.Json;
using System.Text.Json.Serialization;

namespace DomainEditor;

/// <summary>
/// Das Domänen-Modell als AUSFÜHRBARE DATEN (Kern-Idee 5): eine Wahrheit, zwei Backends.
/// Aus diesem Modell erzeugt der <see cref="Scaffolder"/> deterministisch C# (der echte,
/// commitbare Artefakt); dasselbe Modell kann live über den SagaLaufwerk-Interpreter laufen
/// (Stufe 2). Bewusst nur die FORM — Aggregate, Felder, Commands, Events, Sagas. Die winzigen
/// reinen Decide/Apply-Körper füllt in Stufe 3 ein kleines lokales LLM; hier stehen sie als
/// optionaler Freitext (<see cref="Befehl.Rumpf"/> / <see cref="Ereignis"/>-Applier), sonst
/// generiert der Scaffolder einen kompilierbaren <c>throw</c>-Platzhalter.
/// </summary>
public sealed record EditorModell
{
    /// <summary>Schema-Version des Modell-JSON (nicht der Domäne). Erlaubt spätere Migration.</summary>
    public string SchemaVersion { get; init; } = "1";

    public IReadOnlyList<Aggregat> Aggregate { get; init; } = [];
    public IReadOnlyList<Saga> Sagas { get; init; } = [];

    // ── Serialisierung: camelCase, Enums als String, Nulls weglassen — deckungsgleich mit dem
    //    knowledge-graph.json-Stil, damit das Board beide ohne Sonderfall liest. ──
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

/// <summary>
/// Ein Aggregat = eine Konsistenzgrenze. Trägt seine reinen Zustands-Felder, die Commands, die es
/// entscheidet, und die Events, die es produziert (persistent) bzw. als Ablehnung zurückgibt
/// (transient). Id/Version werden generiert und tauchen hier NICHT auf (Invariante 5).
/// </summary>
public sealed record Aggregat
{
    public required string Name { get; init; }
    /// <summary>Voller Namespace, z. B. <c>Domain.Konto</c>.</summary>
    public required string Namespace { get; init; }
    /// <summary>Optionaler XML-Doc-Text (eine Zeile oder mehrzeilig) über der State-Klasse.</summary>
    public string? Doku { get; init; }

    public IReadOnlyList<Feld> Felder { get; init; } = [];
    public IReadOnlyList<Befehl> Commands { get; init; } = [];
    public IReadOnlyList<Ereignis> Events { get; init; } = [];
}

/// <summary>
/// Ein Feld — als Record-Parameter (Command/Event) oder als State-Property (Aggregat). Ist
/// <see cref="Ausdruck"/> gesetzt, ist es eine ABGELEITETE Read-only-Property
/// (<c>public T Name => Ausdruck;</c>) statt eines gespeicherten <c>{ get; set; }</c>-Felds.
/// </summary>
public sealed record Feld
{
    public required string Name { get; init; }
    /// <summary>Skalarer Typ; siehe <see cref="Skalar"/>. Erlaubt: Guid, decimal, int, bool, string.</summary>
    public required string Typ { get; init; }
    /// <summary>Optionaler Default (nur Command-/Ctor-Parameter), z. B. <c>false</c> — roher C#-Literal.</summary>
    public string? Standard { get; init; }
    /// <summary>Gesetzt ⇒ abgeleitete State-Property mit diesem Ausdruck (kein gespeichertes Feld).</summary>
    public string? Ausdruck { get; init; }
}

/// <summary>
/// Ein Command. Erstes Feld ist per Konvention <c>Guid AggregateId</c> (explizit im Modell). Die
/// <see cref="Ergibt"/>-Liste ist die OneOf-Ausgangsmenge des zugehörigen <c>Decide</c> — der
/// Erfolgs-Event zuerst, dann die Ablehnungen (transient). Das ist die Routing-FORM.
/// </summary>
public sealed record Befehl
{
    public required string Name { get; init; }
    public IReadOnlyList<Feld> Felder { get; init; } = [];
    /// <summary>Die OneOf-Ausgänge (Event-Namen + optionaler Guard) in Reihenfolge.</summary>
    public IReadOnlyList<Ausgang> Ergibt { get; init; } = [];
    public string? Doku { get; init; }
    /// <summary>
    /// Optionaler, von Hand/LLM gefüllter Decide-Körper (Stufe 3). Null ⇒ kompilierbarer
    /// <c>throw new NotImplementedException(...)</c>-Platzhalter.
    /// </summary>
    public string? Rumpf { get; init; }
}

/// <summary>Ein OneOf-Ausgang eines Decide: der Event-Name plus optional der Guard (das „Warum").</summary>
public sealed record Ausgang
{
    public required string Event { get; init; }
    /// <summary>Roher Bedingungs-Ausdruck (z. B. <c>State.Verfuegbar &lt; cmd.Betrag</c>), rein informativ.</summary>
    public string? Guard { get; init; }
}

/// <summary>
/// Ein Event — rein/ID-los. <see cref="Transient"/> ⇒ Ablehnung (<c>ITransientEvent</c>, nicht im
/// Log, kein Signal, kein Applier); sonst persistent (<c>IEvent</c>, bekommt ein Apply).
/// </summary>
public sealed record Ereignis
{
    public required string Name { get; init; }
    public IReadOnlyList<Feld> Felder { get; init; } = [];
    public bool Transient { get; init; }
    /// <summary>Optionaler, von Hand/LLM gefüllter Apply-Körper (Stufe 3). Nur für persistente Events.</summary>
    public string? ApplyRumpf { get; init; }
}

/// <summary>
/// Eine Saga/ein Prozess: mehrere Aggregate / asynchron / mit Kompensation (Kern-Idee 7). Der
/// <see cref="TriggerEvent"/> ist das <c>Prozess&lt;T&gt;</c>-Argument. Jeder <see cref="Schritt"/>
/// ist eine Transition (Auf/Und → Sende → RückgängigDurch).
/// </summary>
public sealed record Saga
{
    public required string Name { get; init; }
    public required string Namespace { get; init; }
    public required string TriggerEvent { get; init; }
    public IReadOnlyList<SagaSchritt> Schritte { get; init; } = [];
    public string? Doku { get; init; }
    /// <summary>Zusätzliche <c>using</c>-Direktiven für aggregat-fremde Commands/Events (z. B. <c>Domain.Konto</c>).</summary>
    public IReadOnlyList<string> ExtraUsings { get; init; } = [];
}

/// <summary>
/// Eine Saga-Transition: <c>p.Auf&lt;Wenn[0]&gt;().Und&lt;Wenn[1]&gt;()… .Sende&lt;Sende&gt;(…)
/// .RückgängigDurch&lt;Kompensation&gt;(…)</c>. Die Lambda-Argumente (<c>t.Quelle</c> …) sind Fachlogik
/// und werden — wo bekannt — als roher Ausdruck mitgeführt; sonst generiert der Scaffolder
/// kompilierbare <c>default</c>-Platzhalter je Command-Feld.
/// </summary>
public sealed record SagaSchritt
{
    /// <summary>Bedingungs-Events in Reihenfolge; das erste ist der Trigger.</summary>
    public IReadOnlyList<string> Wenn { get; init; } = [];
    public required string Sende { get; init; }
    /// <summary>Argument-Ausdrücke für den Sende-Command-Ctor; null ⇒ <c>default</c>-Platzhalter.</summary>
    public IReadOnlyList<string>? SendeArgumente { get; init; }
    public string? Kompensation { get; init; }
    public IReadOnlyList<string>? KompensationArgumente { get; init; }
}

/// <summary>Die erlaubten skalaren Feldtypen (bewusst eng — Kern-Idee/EHRLICHE GRENZEN).</summary>
public static class Skalar
{
    public static readonly IReadOnlyList<string> Erlaubt = ["Guid", "decimal", "int", "bool", "string"];

    public static bool IstErlaubt(string typ) => Erlaubt.Contains(typ);
}
