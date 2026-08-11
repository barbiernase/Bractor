using System;
using Google.Protobuf;
using Proto.Remote;

namespace Infrastructure.Serialization;

/// <summary>
/// Proto.Remote-<c>ISerializer</c> für den internen Actor-Plane — macht das System cross-node.
///
/// Vorher lief nichts über die Leitung: die internen Nachrichten (<c>CommandEnvelope</c>,
/// <c>CommandResult</c>, <c>Wake</c>, …) sind rohe CLR-<c>record</c>s ohne registrierten Serializer,
/// also de facto single-node. Dieser Serializer greift genau diese Typen (strikte Whitelist via
/// <c>GeneratedWire.CanSerialize</c>) und serialisiert sie reflexionsfrei über den source-gen
/// <see cref="CqrsWireJsonContext"/> (Invariante 4). Alles andere (<c>PID</c> u. a.
/// <c>Google.Protobuf.IMessage</c>) fällt durch <c>CanSerialize==false</c> weiter an den
/// Default-Protobuf-Serializer.
///
/// ── Registrierung / Priorität ──
/// Registriert an <c>GrpcNetRemoteConfig.WithSerializer(SerializerId, Priority, …)</c> VOR
/// <c>WithRemote</c>. Proto wählt bei der Serialisierung den höchsten <c>Priority</c> mit
/// <c>CanSerialize==true</c>; die Deserialisierung wählt per <c>SerializerId</c> aus dem Wire-Header.
/// Defaults: Protobuf = id 0/prio 0, JSON = id 1/prio -1000. Unser <c>Priority = -100</c> liegt
/// dazwischen (schlägt JSON, verliert gegen Protobuf für dessen IMessage-Typen — was korrekt ist,
/// da unsere Whitelist diese ohnehin ausschließt).
///
/// <c>SerializerId</c> ist <b>const</b>: der Empfänger-Node MUSS denselben Serializer unter
/// derselben Id kennen (beide Nodes laufen denselben Framework-Code).
/// </summary>
public sealed class CqrsWireSerializer : ISerializer
{
    /// <summary>Cross-node identische Serializer-Id (Empfänger wählt Deserializer hierüber).</summary>
    public const int SerializerId = 100;

    /// <summary>Zwischen Default-Protobuf (0) und Default-JSON (-1000).</summary>
    public const int Priority = -100;

    public bool CanSerialize(object obj) => GeneratedWire.CanSerialize(obj.GetType());

    public string GetTypeName(object message) => GeneratedWire.TypeName(message);

    public ByteString Serialize(object obj) => ByteString.CopyFrom(GeneratedWire.Serialize(obj));

    public object Deserialize(ByteString bytes, string typeName)
        => GeneratedWire.Deserialize(bytes.ToByteArray(), typeName);
}
