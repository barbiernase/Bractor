using System.Text.Json;
using System.Text.Json.Serialization;

namespace Abstractions;

/// <summary>
/// Die typisierte Id eines Aggregats (Plan Pub/Sub-Neustrukturierung §2.5, E3): das Ziel jedes Commands (<see cref="ICommand{TAggregat}"/>)
/// und jeder Verweis auf ein Aggregat in Events, Queries und Antworten. Der Typ sagt, worauf die Id zeigt — Routing über Typen (Inv. 3)
/// ohne Konvention, und Editor, LLM und Generatoren sehen es in der Signatur.
///
/// Auf der Leitung, im Log und im Wire reist sie als UUID (Proto <c>string</c>, JSON-String): die Generatoren bilden sie ab
/// (<see cref="Idvertrag"/>), JSON über einen geschlossenen <see cref="IdJsonKonverter{T}"/> je verwendetem <c>Id&lt;X&gt;</c> (generiert, ohne
/// Reflection). Neue Ids würfeln nur Clients und Tests (<see cref="Neu"/>); die Server-Seite leitet sie ab (§4.2, R13).
/// </summary>
public readonly record struct Id<T>(Guid Wert) where T : IState
{
    /// <summary>Eine neue, zufällige Id — für Clients (sie halten die Command-Id ohnehin fest) und Tests, nie im Fachcode der Server-Seite.</summary>
    public static Id<T> Neu() => new(Guid.NewGuid());

    public static Id<T> Parse(string text) => new(Guid.Parse(text));

    public static bool TryParse(string? text, out Id<T> id)
    {
        var ok = Guid.TryParse(text, out var g);
        id = new Id<T>(g);
        return ok;
    }

    /// <summary>Die UUID — wie sie auf der Leitung und im Log steht.</summary>
    public override string ToString() => Wert.ToString();
}

/// <summary>
/// JSON-Form einer <see cref="Id{T}"/>: der UUID-String, auch als Wörterbuch-Schlüssel. Geschlossen je Ziel-Typ, damit der STJ-Quellgenerator
/// ihn ohne Reflection anlegt: der Codegen trägt je verwendetem <c>Id&lt;X&gt;</c> einen in die Optionen der Kontexte ein, Marten bekommt dieselben.
/// </summary>
public sealed class IdJsonKonverter<T> : JsonConverter<Id<T>> where T : IState
{
    public override Id<T> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) => new(reader.GetGuid());

    public override void Write(Utf8JsonWriter writer, Id<T> value, JsonSerializerOptions options) => writer.WriteStringValue(value.Wert);

    public override Id<T> ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => new(Guid.Parse(reader.GetString()!));

    public override void WriteAsPropertyName(Utf8JsonWriter writer, Id<T> value, JsonSerializerOptions options)
        => writer.WritePropertyName(value.Wert.ToString());
}
