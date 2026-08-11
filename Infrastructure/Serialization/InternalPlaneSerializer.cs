using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Abstractions;
using Google.Protobuf;
using Proto;
using Proto.Remote;
using JsonSerializer = System.Text.Json.JsonSerializer;   // Disambig ggü. Proto.Remote.JsonSerializer

namespace Infrastructure.Serialization;

/// <summary>
/// Der Serializer der <b>internen Ebene</b> (Node↔Node) — schließt das Cross-Node-Tor: bislang war für
/// die rohen CLR-Records (Envelopes, Weckrufe, Broker-Data-Plane) <b>kein</b> Serializer bei Proto.Remote
/// registriert, weshalb sie nur in-process überlebten (de-facto single-node).
///
/// Weg A (JSON-Poly-Serializer): baut auf dem bestehenden, reflection-freien
/// <see cref="EventJsonSerializerContext"/> auf. Die <b>Typ-Routing</b>-Achse ist generiert
/// (<see cref="InternalPlaneTypen"/> ← <c>GeneratedTypeRegistry</c>, Invariante 3/4); das reine
/// Feld-Marshalling besorgt STJ. Events laufen dabei über den Source-Gen-Kontext (reflection-frei),
/// die niederfrequenten Framework-Records über den Reflection-Resolver (wie Martens Default) — die
/// interne Serialisierung passiert ohnehin nur beim Node-Übergang, nie im Single-Node-Hot-Path.
///
/// Disjunkt zum Protobuf-Serializer (Id 0): <see cref="CanSerialize"/> lehnt jedes
/// <see cref="IMessage"/> ab → Protos eigene Cluster-Kontrollnachrichten (ActivationRequest &amp; Co.)
/// bleiben beim eingebauten Protobuf-Serializer. Nur die hier registrierten POCOs übernimmt dieser.
/// </summary>
public sealed class InternalPlaneSerializer : ISerializer
{
    /// <summary>Serializer-Id (0 = Protobuf, 1 = JSON sind vergeben) — eindeutig.</summary>
    public const int SerializerId = 2;

    /// <summary>Höhere Priorität als die Eingebauten: dieser wird zuerst befragt. (Disjunkte Mengen,
    /// daher an sich unkritisch — aber deterministisch.)</summary>
    public const int Priority = 100;

    private readonly JsonSerializerOptions _opt;

    public InternalPlaneSerializer()
    {
        var o = new JsonSerializerOptions
        {
            // Events → reflection-freier Source-Gen-Kontext; alles andere → Reflection-Fallback.
            TypeInfoResolver = JsonTypeInfoResolver.Combine(
                EventJsonSerializerContext.Default,
                new DefaultJsonTypeInfoResolver()),
        };
        // Polymorphie an den Envelope-Grenzen (Diskriminator + konkreter Wert).
        o.Converters.Add(new PolyConverter<ICommand>());
        o.Converters.Add(new PolyConverter<IEvent>());
        o.Converters.Add(new PolyConverter<IStateChangeSignal>());
        o.Converters.Add(new PolyConverter<IMessagePayload>());
        o.Converters.Add(new PolyConverter<IMessageEnvelope>());
        // Geschlossene Union + Proto-PID: eigene, bespoke Converter.
        o.Converters.Add(new CommandModusConverter());
        o.Converters.Add(new PidConverter());
        _opt = o;
    }

    public bool CanSerialize(object obj) =>
        obj is not IMessage && InternalPlaneTypen.Kennt(obj.GetType());

    public string GetTypeName(object message) => InternalPlaneTypen.Name(message.GetType());

    public ByteString Serialize(object obj) =>
        // Mit der LAUFZEIT-Art serialisieren → der konkrete Record wird vollständig geschrieben.
        ByteString.CopyFrom(JsonSerializer.SerializeToUtf8Bytes(obj, obj.GetType(), _opt));

    public object Deserialize(ByteString bytes, string typeName)
    {
        var t = InternalPlaneTypen.Typ(typeName);
        return JsonSerializer.Deserialize(bytes.Span, t, _opt)
            ?? throw new InvalidOperationException(
                $"Interne Deserialisierung ergab null für Diskriminator '{typeName}'.");
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Converter
    // ─────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Polymorpher Converter für eine Interface-Grenze <typeparamref name="T"/> (z. B. <c>ICommand</c>):
    /// schreibt <c>{ "$t": "&lt;FullName&gt;", "v": &lt;konkreter Wert&gt; }</c> und liest über
    /// <see cref="InternalPlaneTypen"/> zurück. Greift NUR, wenn die statische Property-Art exakt
    /// <typeparamref name="T"/> ist — konkrete Werte laufen über den (Source-Gen-)Resolver, kein Re-Entry.
    /// </summary>
    private sealed class PolyConverter<T> : JsonConverter<T> where T : class
    {
        public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(T);

        public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
        {
            var rt = value.GetType();
            writer.WriteStartObject();
            writer.WriteString("$t", InternalPlaneTypen.Name(rt));
            writer.WritePropertyName("v");
            JsonSerializer.Serialize(writer, value, rt, options);   // konkrete Art → Resolver
            writer.WriteEndObject();
        }

        public override T Read(ref Utf8JsonReader reader, Type _, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var disc = doc.RootElement.GetProperty("$t").GetString()
                ?? throw new JsonException("Fehlender Diskriminator '$t'.");
            var rt = InternalPlaneTypen.Typ(disc);
            var v = doc.RootElement.GetProperty("v");
            return (T)JsonSerializer.Deserialize(v.GetRawText(), rt, options)!;
        }
    }

    /// <summary>Geschlossene Union <see cref="CommandModus"/> (Client(int) | Emittiert).</summary>
    private sealed class CommandModusConverter : JsonConverter<CommandModus>
    {
        public override void Write(Utf8JsonWriter writer, CommandModus value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            switch (value)
            {
                case CommandModus.Client c:
                    writer.WriteString("$t", "Client");
                    writer.WriteNumber("v", c.ExpectedVersion);
                    break;
                case CommandModus.Emittiert:
                    writer.WriteString("$t", "Emittiert");
                    break;
                default:
                    throw new NotSupportedException($"Unbekannter CommandModus: {value.GetType()}");
            }
            writer.WriteEndObject();
        }

        public override CommandModus Read(ref Utf8JsonReader reader, Type _, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var t = doc.RootElement.GetProperty("$t").GetString();
            return t switch
            {
                "Client" => new CommandModus.Client(doc.RootElement.GetProperty("v").GetInt32()),
                "Emittiert" => new CommandModus.Emittiert(),
                _ => throw new JsonException($"Unbekannter CommandModus-Diskriminator: '{t}'."),
            };
        }
    }

    /// <summary>
    /// Proto-<see cref="PID"/> (steckt in <c>Subscribe</c>): Adresse + Id genügen, um den Subscriber
    /// zurückzurouten. Wird nicht über die FullName-Registry geführt (es ist ein Protobuf-Typ), sondern
    /// bespoke als Adresse/Id serialisiert.
    /// </summary>
    private sealed class PidConverter : JsonConverter<PID>
    {
        public override void Write(Utf8JsonWriter writer, PID value, JsonSerializerOptions options)
        {
            writer.WriteStartObject();
            writer.WriteString("address", value.Address);
            writer.WriteString("id", value.Id);
            writer.WriteEndObject();
        }

        public override PID Read(ref Utf8JsonReader reader, Type _, JsonSerializerOptions options)
        {
            using var doc = JsonDocument.ParseValue(ref reader);
            var address = doc.RootElement.GetProperty("address").GetString() ?? "";
            var id = doc.RootElement.GetProperty("id").GetString() ?? "";
            return new PID(address, id);
        }
    }
}
