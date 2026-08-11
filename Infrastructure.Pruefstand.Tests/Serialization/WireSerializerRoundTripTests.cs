using System;
using System.Collections.Generic;
using System.Linq;
using Abstractions;
using Domain.Konto;
using Domain.Pipeline.Benchmark;
using Domain.Zahlung;
using FluentAssertions;
using Infrastructure.Mapping;
using Infrastructure.Projections;
using Infrastructure.PubSub.Messages;
using Infrastructure.Serialization;
using Proto;
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
    public void Top_Level_Whitelist_greift_die_Huellen_aber_nicht_die_Payloads()
    {
        // Iter.1-Hüllen
        GeneratedWire.CanSerialize(typeof(CommandEnvelope)).Should().BeTrue();
        GeneratedWire.CanSerialize(typeof(CommandResult)).Should().BeTrue();
        GeneratedWire.CanSerialize(typeof(Wake)).Should().BeTrue();
        GeneratedWire.CanSerialize(typeof(WakeAck)).Should().BeTrue();
        // Iter.2-Hüllen
        GeneratedWire.CanSerialize(typeof(Publish)).Should().BeTrue();
        GeneratedWire.CanSerialize(typeof(SignalEnvelope)).Should().BeTrue();
        GeneratedWire.CanSerialize(typeof(EventEnvelope)).Should().BeTrue();
        GeneratedWire.CanSerialize(typeof(Subscribe)).Should().BeTrue();
        GeneratedWire.CanSerialize(typeof(BenchPing)).Should().BeTrue();   // Trigger = Top-Level
        // Ein geschachtelter Domänen-Payload ist KEINE Top-Level-Hülle → Default-Serializer, nicht wir.
        GeneratedWire.CanSerialize(typeof(EroeffneKonto)).Should().BeFalse();
        GeneratedWire.CanSerialize(typeof(KontoEroeffnet)).Should().BeFalse();
    }

    [Fact]
    public void Boot_Check_deckt_jeden_Command_und_jedes_Event_ab()
    {
        // Backstop gegen Registry↔Context-Drift: darf nicht werfen.
        var act = () => WireSerializerBootCheck.Verify();
        act.Should().NotThrow();

        // Und explizit: jeder polymorphe Payload-Typ hat eine Wire-JsonTypeInfo.
        var payloads = GeneratedTypeRegistry.Commands.Values
            .Concat(GeneratedTypeRegistry.Events.Values)
            .Concat(GeneratedTypeRegistry.Signals.Values)
            .Concat(GeneratedTypeRegistry.Triggers.Values);
        foreach (var t in payloads)
            CqrsWireJsonContext.Default.GetTypeInfo(t).Should().NotBeNull($"Wire-Context muss {t.Name} abdecken");
    }

    // ══════════════════ Iteration 2: PubSub-/Pipeline-/Prozess-Plane ══════════════════

    [Fact]
    public void SignalEnvelope_mit_polymorphem_Signal_bleibt_wertgleich()
    {
        var original = new SignalEnvelope
        {
            Signal = new Domain.Konto.StateChangeViaKontoEroeffnet(Guid.NewGuid(), 7),
            CorrelationId = "corr-sig",
        };

        var back = RoundTrip(original);

        back.Should().Be(original);
        back.Signal.Should().BeOfType<Domain.Konto.StateChangeViaKontoEroeffnet>()
            .Which.Version.Should().Be(7);
    }

    [Fact]
    public void EventEnvelope_mit_polymorphem_Event_bleibt_wertgleich()
    {
        var original = new EventEnvelope
        {
            AggregateId = Guid.NewGuid(),
            AggregateVersion = 3,
            Payload = new KontoEroeffnet(250m, false),
            AggregateType = "Konto",
            TargetSubscriberId = "sess-7",
        };

        var back = RoundTrip(original);

        back.Should().Be(original);
        back.Payload.Should().BeOfType<KontoEroeffnet>();
    }

    [Fact]
    public void Publish_traegt_EventEnvelope_und_SignalEnvelope_polymorph()
    {
        var mitEvent = RoundTrip(new Publish(new EventEnvelope
        {
            AggregateId = Guid.NewGuid(), Payload = new KontoEroeffnet(1m, false), AggregateType = "Konto",
        }));
        mitEvent.Envelope.Should().BeOfType<EventEnvelope>()
            .Which.Payload.Should().BeOfType<KontoEroeffnet>();

        var mitSignal = RoundTrip(new Publish(new SignalEnvelope
        {
            Signal = new Domain.Konto.StateChangeViaKontoEroeffnet(Guid.NewGuid(), 1),
        }));
        mitSignal.Envelope.Should().BeOfType<SignalEnvelope>()
            .Which.Signal.Should().BeOfType<Domain.Konto.StateChangeViaKontoEroeffnet>();
    }

    [Fact]
    public void Subscribe_mit_PID_erhaelt_Adresse_und_Id()
    {
        var original = new Subscribe("sub-42", PID.FromAddress("nodeB:5001", "$abc"));

        var back = RoundTrip(original);

        back.SubscriberId.Should().Be("sub-42");
        back.Subscriber.Address.Should().Be("nodeB:5001");
        back.Subscriber.Id.Should().Be("$abc");
    }

    [Fact]
    public void Pipeline_Trigger_ist_ein_Top_Level_Wire_Typ()
    {
        GeneratedWire.CanSerialize(typeof(BenchPing)).Should().BeTrue();
        RoundTrip(new BenchPing(99)).Should().Be(new BenchPing(99));
    }

    [Fact]
    public void Kleine_Huellen_Ack_PipelineAck_ProzessWake_SubscriberCount()
    {
        RoundTrip(new Ack()).Should().Be(new Ack());
        RoundTrip(new PipelineAck(true)).Should().Be(new PipelineAck(true));
        RoundTrip(new SubscriberCountResponse(5)).Should().Be(new SubscriberCountResponse(5));

        var wake = new Infrastructure.Prozess.ProzessWake(Guid.NewGuid(), 4, "BestellProzess");
        RoundTrip(wake).Should().Be(wake);
    }
}
