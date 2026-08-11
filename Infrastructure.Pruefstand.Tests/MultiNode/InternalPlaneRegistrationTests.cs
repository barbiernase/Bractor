using System;
using Abstractions;
using Domain.Konto;
using FluentAssertions;
using Google.Protobuf;
using Infrastructure.Serialization;
using Proto;
using Proto.Remote;
using Proto.Remote.GrpcNet;
using ProtoSerialization = Proto.Remote.Serialization;

namespace Infrastructure.Pruefstand.MultiNode;

/// <summary>
/// Phase 3: der Serializer ist bei Proto.Remotes <see cref="Serialization"/>-Dispatcher registriert — und
/// der Dispatcher routet die interne Ebene korrekt. Das ist die Naht zwischen unserem Serializer und dem
/// echten Proto-Transport, store-frei geprüft (ohne vollen Cluster).
///
/// Wichtig: Proto wählt den Serializer über <c>CanSerialize</c> nach Priorität — dieser Test beweist, dass
/// (a) unsere POCOs auf Serializer-Id 2 landen und verlustfrei zurückkommen, (b) Protobuf-Nachrichten (PID)
/// beim eingebauten Serializer 0 bleiben (disjunkt, keine Kollision).
/// </summary>
public class InternalPlaneRegistrationTests
{
    private static ProtoSerialization BaueRegistrierteSerialization()
    {
        // Wie im Host: Serializer auf der RemoteConfig-Serialization registrieren.
        var cfg = GrpcNetRemoteConfig.BindToLocalhost();
        cfg.Serialization.RegisterSerializer(
            InternalPlaneSerializer.SerializerId,
            InternalPlaneSerializer.Priority,
            new InternalPlaneSerializer());
        return cfg.Serialization;
    }

    [Fact]
    public void Boot_Check_findet_keine_Luecke()
    {
        InternalPlaneCoverage.FindeLuecken().Should().BeEmpty();
        // und wirft folglich nicht:
        var wurf = Record.Exception(() => InternalPlaneCoverage.PruefeOderWirf());
        wurf.Should().BeNull();
    }

    [Fact]
    public void Dispatcher_routet_interne_POCO_auf_Serializer_2_und_zurueck()
    {
        var ser = BaueRegistrierteSerialization();
        var id = Guid.NewGuid();
        var env = new CommandEnvelope
        {
            AggregateId = id,
            AggregateType = "Konto",
            Modus = new CommandModus.Emittiert(),
            Payload = new EroeffneKonto(id, 500m),
        };

        var (bytes, typeName, serializerId) = ser.Serialize(env);
        serializerId.Should().Be(InternalPlaneSerializer.SerializerId,
            "interne POCOs müssen über den registrierten Plane-Serializer laufen, nicht über Protobuf/JSON");

        var zurueck = ser.Deserialize(typeName, bytes, serializerId);
        zurueck.Should().BeOfType<CommandEnvelope>().Which.Should().BeEquivalentTo(env);
    }

    [Fact]
    public void Dispatcher_laesst_Protobuf_beim_eingebauten_Serializer()
    {
        var ser = BaueRegistrierteSerialization();
        IMessage pid = new PID("127.0.0.1:8000", "actor/7");

        var (_, _, serializerId) = ser.Serialize(pid);
        serializerId.Should().Be(ProtoSerialization.SERIALIZER_ID_PROTOBUF,
            "Protos eigene Cluster-Kontrollnachrichten dürfen NICHT von uns übernommen werden");
    }
}
