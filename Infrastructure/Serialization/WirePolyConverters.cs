using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Abstractions;

namespace Infrastructure.Serialization;

/// <summary>
/// Polymorphe STJ-Converter für die Interface-Payloads, die GESCHACHTELT in einer Wire-Hülle reisen
/// (<c>CommandEnvelope.Payload : ICommand</c>, <c>CommandResult.Events/RejectionEvent : IEvent</c>).
///
/// Reflexionsfrei (Invariante 4): der Diskriminator ist der Typ-Name (PascalCase, identisch zu den
/// <c>GeneratedTypeRegistry</c>-Schlüsseln); der konkrete Typ wird über den source-gen
/// <see cref="CqrsWireJsonContext"/> serialisiert — via die generierten Dispatch-Tabellen
/// <c>GeneratedWirePoly</c>. Form ist ein 2-elementiges JSON-Array <c>["Name", { …Felder… }]</c>:
/// single-pass, kein Buffering, keine Property-Reihenfolge-Annahme.
///
/// Diese Converter sind über <c>[JsonSourceGenerationOptions(Converters=…)]</c> in den Context
/// eingebacken — so gelten sie auch für die geschachtelten Member der Hüllen-<c>JsonTypeInfo</c>.
/// </summary>
internal sealed class IEventJsonConverter : JsonConverter<IEvent>
{
    public override void Write(Utf8JsonWriter writer, IEvent value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(GeneratedWirePoly.EventDiskriminator(value));
        GeneratedWirePoly.WriteEvent(writer, value);
        writer.WriteEndArray();
    }

    public override IEvent Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Erwartet: StartArray für IEvent-Payload.");
        reader.Read();
        var disc = reader.GetString() ?? throw new JsonException("Erwartet: Event-Diskriminator (string).");
        reader.Read();
        var evt = GeneratedWirePoly.ReadEvent(disc, ref reader);
        reader.Read(); // EndArray
        return evt;
    }
}

internal sealed class ICommandJsonConverter : JsonConverter<ICommand>
{
    public override void Write(Utf8JsonWriter writer, ICommand value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(GeneratedWirePoly.CommandDiskriminator(value));
        GeneratedWirePoly.WriteCommand(writer, value);
        writer.WriteEndArray();
    }

    public override ICommand Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Erwartet: StartArray für ICommand-Payload.");
        reader.Read();
        var disc = reader.GetString() ?? throw new JsonException("Erwartet: Command-Diskriminator (string).");
        reader.Read();
        var cmd = GeneratedWirePoly.ReadCommand(disc, ref reader);
        reader.Read(); // EndArray
        return cmd;
    }
}

/// <summary>
/// Hand-Converter für den geschlossenen Summentyp <see cref="CommandModus"/> (privater ctor,
/// kein parameterloser ctor → STJ kann ihn nicht default-konstruieren). Fixe 2-Fall-Form, driftfrei.
/// </summary>
internal sealed class CommandModusJsonConverter : JsonConverter<CommandModus>
{
    public override void Write(Utf8JsonWriter writer, CommandModus value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        switch (value)
        {
            case CommandModus.Client c:
                writer.WriteStringValue("client");
                writer.WriteNumberValue(c.ExpectedVersion);
                break;
            case CommandModus.Emittiert:
                writer.WriteStringValue("emittiert");
                break;
            default:
                throw new JsonException($"Unbekannter CommandModus: {value.GetType().Name}");
        }
        writer.WriteEndArray();
    }

    public override CommandModus Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Erwartet: StartArray für CommandModus.");
        reader.Read();
        var tag = reader.GetString();
        CommandModus modus;
        switch (tag)
        {
            case "client":
                reader.Read();
                modus = new CommandModus.Client(reader.GetInt32());
                break;
            case "emittiert":
                modus = new CommandModus.Emittiert();
                break;
            default:
                throw new JsonException($"Unbekannter CommandModus-Tag: {tag}");
        }
        reader.Read(); // → EndArray: Converter lässt den Reader auf dem LETZTEN Token des Werts stehen.
        return modus;
    }
}
