using System;
using System.Collections.Generic;
using System.Linq;
using Abstractions;
using Domain.Konto;
using FluentAssertions;
using Google.Protobuf;
using Infrastructure.Aggregate;             // StateChangeViaKommandoVerarbeitet, KommandoVerarbeitet
using Infrastructure.Mapping;               // GeneratedTypeRegistry
using Infrastructure.Projections;           // Wake, WakeAck
using Infrastructure.Prozess;               // ProzessWake
using Infrastructure.PubSub.Messages;       // Subscribe, Publish, Ack, …
using Infrastructure.Serialization;
using Proto;                                // PID
using Proto.Remote;                         // ISerializer

namespace Infrastructure.Pruefstand.MultiNode;

/// <summary>
/// Phase 1+2 des Multi-Node-Umbaus: der <see cref="InternalPlaneSerializer"/> schließt das Cross-Node-Tor.
///
/// Single-Node übt Serialisierung NIE aus (in-process bleiben rohe CLR-Objekte) — Lücken wären also still.
/// Diese Tests sind das store-freie Korrektheits-Herz: (1) die Registry deckt exakt die Node-übergreifende
/// Typmenge ab, (2) jeder interne Nachrichtentyp geht über den ECHTEN <see cref="ISerializer"/>-Pfad
/// (Serialize→Bytes→typeName→Deserialize) verlustfrei durch.
/// </summary>
public class InternalPlaneSerializerTests
{
    private readonly InternalPlaneSerializer _ser = new();

    /// <summary>Der echte Proto.Remote-Pfad: über GetTypeName + Serialize raus, über Deserialize rein.</summary>
    private T RoundTrip<T>(T obj) where T : notnull
    {
        _ser.CanSerialize(obj).Should().BeTrue($"{typeof(T)} muss auf der internen Ebene serialisierbar sein");
        var name = _ser.GetTypeName(obj);
        var bytes = _ser.Serialize(obj);
        var zurueck = _ser.Deserialize(bytes, name);
        return zurueck.Should().BeOfType<T>().Subject;
    }

    // ── (1) Vollständigkeit ───────────────────────────────────────────────────────

    [Fact]
    public void Registry_deckt_alle_generierten_Domaenentypen_ab()
    {
        var domaene = GeneratedTypeRegistry.Commands.Values
            .Concat(GeneratedTypeRegistry.Events.Values)
            .Concat(GeneratedTypeRegistry.Signals.Values)
            .Concat(GeneratedTypeRegistry.Triggers.Values)
            .Distinct();

        foreach (var t in domaene)
            InternalPlaneTypen.Kennt(t).Should().BeTrue(
                $"generierter Domänentyp {t.FullName} muss cross-node serialisierbar sein");
    }

    [Fact]
    public void Registry_enthaelt_die_feste_Framework_Menge()
    {
        foreach (var t in InternalPlaneTypen.FrameworkTypen)
            InternalPlaneTypen.Kennt(t).Should().BeTrue($"Framework-Wire-Typ {t.Name} muss registriert sein");
    }

    [Fact]
    public void Diskriminator_ist_bijektiv()
    {
        foreach (var t in InternalPlaneTypen.AlleTypen)
            InternalPlaneTypen.Typ(InternalPlaneTypen.Name(t)).Should().Be(t);
    }

    // ── (2) Abgrenzung zum Protobuf-Serializer ─────────────────────────────────────

    [Fact]
    public void CanSerialize_lehnt_Protobuf_Nachrichten_ab()
    {
        // PID IST ein Protobuf-IMessage → muss beim eingebauten Serializer (Id 0) bleiben.
        IMessage pid = new PID("127.0.0.1:8000", "actor/1");
        _ser.CanSerialize(pid).Should().BeFalse();
        // Ein reiner interner POCO dagegen gehört uns.
        _ser.CanSerialize(new Ack()).Should().BeTrue();
    }

    // ── (2) Round-Trips über den echten Serializer-Pfad ────────────────────────────

    [Fact]
    public void RoundTrip_CommandEnvelope_Emittiert()
    {
        var id = Guid.NewGuid();
        var env = new CommandEnvelope
        {
            CommandId = id,
            AggregateId = id,
            AggregateType = "Konto",
            Modus = new CommandModus.Emittiert(),
            Payload = new EroeffneKonto(id, 1000m),
            CorrelationId = "corr-1",
            UserId = "tester",
        };
        RoundTrip(env).Should().BeEquivalentTo(env);
    }

    [Fact]
    public void RoundTrip_CommandEnvelope_Client_mit_Version()
    {
        var id = Guid.NewGuid();
        var env = new CommandEnvelope
        {
            AggregateId = id,
            AggregateType = "Konto",
            Modus = new CommandModus.Client(42),
            Payload = new SchreibeGut(id, 10m),
        };
        var zurueck = RoundTrip(env);
        zurueck.Should().BeEquivalentTo(env);
        zurueck.Modus.Should().BeOfType<CommandModus.Client>().Which.ExpectedVersion.Should().Be(42);
    }

    [Fact]
    public void RoundTrip_EventEnvelope()
    {
        var id = Guid.NewGuid();
        var env = new EventEnvelope
        {
            AggregateId = id,
            AggregateVersion = 3,
            AggregateType = "Konto",
            Payload = new KontoEroeffnet(1000m, false),
            CausationId = "cause-1",
            CorrelationId = "corr-1",
        };
        RoundTrip(env).Should().BeEquivalentTo(env);
    }

    [Fact]
    public void RoundTrip_SignalEnvelope()
    {
        var env = new SignalEnvelope
        {
            CorrelationId = "corr-9",
            Signal = new StateChangeViaKommandoVerarbeitet(Guid.NewGuid(), 7),
        };
        RoundTrip(env).Should().BeEquivalentTo(env);
    }

    [Fact]
    public void RoundTrip_CommandResult_mit_Events_und_Rejection()
    {
        var res = new CommandResult
        {
            Success = true,
            AggregateId = Guid.NewGuid(),
            NewVersion = 5,
            Events = new List<IEvent> { new KontoEroeffnet(1000m, false), new BetragReserviert(49.99m) },
            RejectionEvent = new BetragReserviert(1m),
        };
        RoundTrip(res).Should().BeEquivalentTo(res);
    }

    [Fact]
    public void RoundTrip_Weckrufe()
    {
        RoundTrip(new Wake(VomPoll: true)).VomPoll.Should().BeTrue();
        RoundTrip(new WakeAck());   // leerer Record: RoundTrip prüft bereits den Typ
        var pw = new ProzessWake(Guid.NewGuid(), 4, "BestellProzess");
        RoundTrip(pw).Should().BeEquivalentTo(pw);
    }

    [Fact]
    public void RoundTrip_PubSub_DataPlane()
    {
        var sub = new Subscribe("sub-7", new PID("127.0.0.1:8123", "receiver/42"));
        var zurueck = RoundTrip(sub);
        zurueck.SubscriberId.Should().Be("sub-7");
        zurueck.Subscriber.Address.Should().Be("127.0.0.1:8123");
        zurueck.Subscriber.Id.Should().Be("receiver/42");

        RoundTrip(new Unsubscribe("sub-7")).SubscriberId.Should().Be("sub-7");
        RoundTrip(new Ack());
        RoundTrip(new Activate());
        RoundTrip(new GetSubscriberCount());
        RoundTrip(new SubscriberCountResponse(13)).Count.Should().Be(13);
        RoundTrip(new PipelineAck(true)).Accepted.Should().BeTrue();
    }

    [Fact]
    public void RoundTrip_Publish_umschliesst_Event_und_Signal()
    {
        var id = Guid.NewGuid();
        var evPub = new Publish(new EventEnvelope
        {
            AggregateId = id, AggregateType = "Konto", Payload = new BetragReserviert(5m),
        });
        RoundTrip(evPub).Envelope.Should().BeOfType<EventEnvelope>()
            .Which.Payload.Should().BeOfType<BetragReserviert>();

        var sigPub = new Publish(new SignalEnvelope
        {
            Signal = new StateChangeViaKommandoVerarbeitet(id, 1),
        });
        RoundTrip(sigPub).Envelope.Should().BeOfType<SignalEnvelope>()
            .Which.Signal.Should().BeOfType<StateChangeViaKommandoVerarbeitet>();
    }
}
