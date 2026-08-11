using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using Abstractions;
using Proto;

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

/// <summary>Signal-Payload einer <see cref="SignalEnvelope"/> (StateChangeVia{Event}). Iteration 2.</summary>
internal sealed class IStateChangeSignalJsonConverter : JsonConverter<IStateChangeSignal>
{
    public override void Write(Utf8JsonWriter writer, IStateChangeSignal value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(GeneratedWirePoly.SignalDiskriminator(value));
        GeneratedWirePoly.WriteSignal(writer, value);
        writer.WriteEndArray();
    }

    public override IStateChangeSignal Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Erwartet: StartArray für IStateChangeSignal.");
        reader.Read();
        var disc = reader.GetString() ?? throw new JsonException("Erwartet: Signal-Diskriminator (string).");
        reader.Read();
        var sig = GeneratedWirePoly.ReadSignal(disc, ref reader);
        reader.Read(); // EndArray
        return sig;
    }
}

/// <summary>
/// Envelope-Payload einer <see cref="Abstractions.SignalEnvelope"/>? Nein — die HÜLLE einer
/// <c>Publish</c>-Nachricht (<c>Publish.Envelope : IMessageEnvelope</c>). Über den PubSub reisen nur
/// <see cref="EventEnvelope"/> (Events) und <see cref="SignalEnvelope"/> (Signale); die anderen
/// <c>IMessageEnvelope</c>-Implementierer (<c>CommandEnvelope</c>/<c>QueryEnvelope</c>) gehen NICHT durch
/// <c>Publish</c> und werfen hier bewusst. Hand-Converter (fixe, kleine Menge, kein Generat nötig).
/// </summary>
internal sealed class IMessageEnvelopeJsonConverter : JsonConverter<IMessageEnvelope>
{
    public override void Write(Utf8JsonWriter writer, IMessageEnvelope value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        switch (value)
        {
            case EventEnvelope ee:
                writer.WriteStringValue("EventEnvelope");
                JsonSerializer.Serialize(writer, ee, CqrsWireJsonContext.Default.EventEnvelope);
                break;
            case SignalEnvelope se:
                writer.WriteStringValue("SignalEnvelope");
                JsonSerializer.Serialize(writer, se, CqrsWireJsonContext.Default.SignalEnvelope);
                break;
            default:
                throw new JsonException(
                    $"IMessageEnvelope {value.GetType().Name} reist nicht über Publish (nur EventEnvelope/SignalEnvelope).");
        }
        writer.WriteEndArray();
    }

    public override IMessageEnvelope Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Erwartet: StartArray für IMessageEnvelope.");
        reader.Read();
        var disc = reader.GetString();
        reader.Read();
        IMessageEnvelope env = disc switch
        {
            "EventEnvelope"  => JsonSerializer.Deserialize(ref reader, CqrsWireJsonContext.Default.EventEnvelope)!,
            "SignalEnvelope" => JsonSerializer.Deserialize(ref reader, CqrsWireJsonContext.Default.SignalEnvelope)!,
            _ => throw new JsonException($"Unbekannter IMessageEnvelope-Diskriminator: {disc}"),
        };
        reader.Read(); // EndArray
        return env;
    }
}

/// <summary>
/// Proto-<see cref="PID"/> in einem CLR-Record (<c>Subscribe.Subscriber</c>). STJ kann die
/// protobuf-generierte PID nicht direkt; wir serialisieren nur die Adressier-Bestandteile
/// (<c>Address</c> + <c>Id</c>) und rekonstruieren via <see cref="PID.FromAddress"/>. Das reicht für
/// <c>context.Send(pid, …)</c> — die Location-Transparency routet über die Adresse (bewiesen im
/// RemotePidDeliverySmokeTest). RequestId u. a. sind für ein gespeichertes Subscriber-Ziel irrelevant.
/// </summary>
internal sealed class PidJsonConverter : JsonConverter<PID>
{
    public override void Write(Utf8JsonWriter writer, PID value, JsonSerializerOptions options)
    {
        writer.WriteStartArray();
        writer.WriteStringValue(value.Address);
        writer.WriteStringValue(value.Id);
        writer.WriteEndArray();
    }

    public override PID Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray) throw new JsonException("Erwartet: StartArray für PID.");
        reader.Read();
        var address = reader.GetString() ?? "";
        reader.Read();
        var id = reader.GetString() ?? "";
        reader.Read(); // EndArray
        return PID.FromAddress(address, id);
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
