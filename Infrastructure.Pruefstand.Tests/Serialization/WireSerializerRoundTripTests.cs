using System;
using System.Collections.Generic;
using System.Linq;
using Abstractions;
using Domain.Konto;
using Domain.Zahlung;
using FluentAssertions;
using Infrastructure.Mapping;
using Infrastructure.Projections;
using Infrastructure.Serialization;
using Xunit;

namespace Infrastructure.Pruefstand.Serialization;

/// <summary>
/// P7/K1 — Round-Trip über den Wire-Serializer (store-frei, in-memory). Beweist, dass die internen
/// Actor-Plane-Nachrichten reflexionsfrei serialisiert und wertgleich zurückgelesen werden — die
/// Voraussetzung für Multi-Node. Single-Node lässt genau diese Lücke unsichtbar; hier fällt sie auf.
///
/// Deckt die drei harten Fälle ab: polymorpher Payload (<c>ICommand</c>/<c>IEvent</c> per Diskriminator),
/// geschlossener Summentyp (<c>CommandModus</c>) und die mutable <c>CommandResult</c>-Klasse mit
/// polymorpher Event-Liste + Transient-<c>RejectionEvent</c>.
/// </summary>
public class WireSerializerRoundTripTests
{
    private static readonly CqrsWireSerializer Ser = new();

    private static T RoundTrip<T>(T message) where T : class
    {
        var bytes = Ser.Serialize(message!);
        var typeName = Ser.GetTypeName(message!);
        return (T)Ser.Deserialize(bytes, typeName);
    }

    [Fact]
    public void CommandEnvelope_mit_Client_Modus_und_polymorphem_Payload_bleibt_wertgleich()
    {
        var original = new CommandEnvelope
        {
            AggregateId = Guid.NewGuid(),
            Modus = new CommandModus.Client(5),
            Payload = new EroeffneKonto(Guid.NewGuid(), 100.50m, Gesperrt: true),
            AggregateType = "Konto",
            CorrelationId = "corr-1",
            UserId = "u1",
            OriginSessionId = "sess-9",
        };

        var back = RoundTrip(original);

        back.Should().Be(original);                     // volle Record-Gleichheit inkl. Payload + Modus
        back.Payload.Should().BeOfType<EroeffneKonto>();
        back.Modus.Should().Be(new CommandModus.Client(5));
    }

    [Fact]
    public void CommandEnvelope_mit_Emittiert_Modus_bleibt_wertgleich()
    {
        var original = new CommandEnvelope
        {
            AggregateId = Guid.NewGuid(),
            Modus = new CommandModus.Emittiert(),
            Payload = new BelasteKonto(Guid.NewGuid(), 42m),
            AggregateType = "Konto",
        };

        var back = RoundTrip(original);

        back.Should().Be(original);
        back.Modus.Should().BeOfType<CommandModus.Emittiert>();
    }

    [Fact]
    public void CommandResult_mit_polymorpher_Event_Liste_und_Transient_RejectionEvent()
    {
        var aggId = Guid.NewGuid();
        var original = new CommandResult
        {
            Success = false,
            ErrorMessage = null,
            AggregateId = aggId,
            NewVersion = 3,
            Events = new List<IEvent> { new KontoEroeffnet(100m, false), new KontoBelastet(10m) },
            RejectionEvent = new KontoUngedeckt(aggId, 5m, 10m),   // ITransientEvent — NICHT im Marten-Context
        };

        var back = RoundTrip(original);

        back.Success.Should().BeFalse();
        back.AggregateId.Should().Be(aggId);
        back.NewVersion.Should().Be(3);
        back.Events.Should().HaveCount(2);
        back.Events[0].Should().BeOfType<KontoEroeffnet>().Which.StartSaldo.Should().Be(100m);
        back.Events[1].Should().BeOfType<KontoBelastet>();
        back.RejectionEvent.Should().BeOfType<KontoUngedeckt>().Which.Angefordert.Should().Be(10m);
    }

    [Fact]
    public void Wake_und_WakeAck_bleiben_wertgleich()
    {
        RoundTrip(new Wake(VomPoll: true)).Should().Be(new Wake(true));
        RoundTrip(new Wake(VomPoll: false)).Should().Be(new Wake(false));
        RoundTrip(new WakeAck()).Should().Be(new WakeAck());
    }

    [Fact]
    public void CommandModus_beide_Faelle_ueber_die_Huelle()
    {
        var client = RoundTrip(new CommandEnvelope
        {
            AggregateId = Guid.NewGuid(), Modus = new CommandModus.Client(99),
            Payload = new BelasteKonto(Guid.NewGuid(), 1m), AggregateType = "Konto",
        });
        client.Modus.Should().Be(new CommandModus.Client(99));
    }

    [Fact]
    public void Nur_die_vier_Top_Level_Huellen_sind_serialisierbar()
    {
        GeneratedWire.WireMessageTypes.Should().HaveCount(4);
        GeneratedWire.CanSerialize(typeof(CommandEnvelope)).Should().BeTrue();
        GeneratedWire.CanSerialize(typeof(CommandResult)).Should().BeTrue();
        GeneratedWire.CanSerialize(typeof(Wake)).Should().BeTrue();
        GeneratedWire.CanSerialize(typeof(WakeAck)).Should().BeTrue();
        // Ein Domänen-Payload ist KEINE Top-Level-Hülle → Default-Serializer, nicht wir.
        GeneratedWire.CanSerialize(typeof(EroeffneKonto)).Should().BeFalse();
    }

    [Fact]
    public void Boot_Check_deckt_jeden_Command_und_jedes_Event_ab()
    {
        // Backstop gegen Registry↔Context-Drift: darf nicht werfen.
        var act = () => WireSerializerBootCheck.Verify();
        act.Should().NotThrow();

        // Und explizit: jeder Payload-Typ hat eine Wire-JsonTypeInfo.
        foreach (var t in GeneratedTypeRegistry.Commands.Values.Concat(GeneratedTypeRegistry.Events.Values))
            CqrsWireJsonContext.Default.GetTypeInfo(t).Should().NotBeNull($"Wire-Context muss {t.Name} abdecken");
    }
}
