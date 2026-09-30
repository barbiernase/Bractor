using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abstractions;
using Core;
using FluentAssertions;
using Infrastructure.Pipeline;
using Xunit;

namespace Infrastructure.Pruefstand.Phase6;

/// <summary>
/// P6.2 — der Pipeline-Event-Pfad-Fold, in-memory bewiesen (der fehlende Pipeline-Test-Harness). Die
/// <see cref="PipelineEventPullBridge"/> hängt den Event-Pfad an die Pull-Maschine; hier mit einer
/// Fake-Dispatch (statt der generierten <c>DispatchEventAsync</c>) + Fake-Nähten, ohne Cluster/OpenCV:
///   (1) ein Event → der Event-Handler yieldet ein Command → es läuft über die EMIT-Naht (nicht Push),
///       und der PipelineContext trägt Quell-Aggregat/Version/Korrelation aus dem Envelope,
///   (2) ein Event → der Handler yieldet einen Trigger → er läuft über die TRIGGER-Naht,
///   (3) der ProjectionWriter wird nicht angefasst (ein Emittent hat keinen co-committeten Effekt).
/// </summary>
public class PipelineEventPullBridgeTests
{
    private sealed record FakeEvent(Guid AggregateId) : IEvent;
    private sealed record FakeCommand(Guid AggregateId) : ICommand;
    private sealed record FakeTrigger : IPipelineTrigger;
    private sealed record FakeTransient(Guid AggregateId) : ITransientEvent;

    private static EventEnvelope Env(Guid stream, int version, string corr) => new()
    {
        AggregateId = stream,
        AggregateVersion = version,
        AggregateType = "FakeAggregat",
        CorrelationId = corr,
        Payload = new FakeEvent(stream),
    };

    [Fact]
    public async Task Event_yieldet_Command_laeuft_ueber_die_Emit_Naht_mit_Envelope_Kontext()
    {
        var stream = Guid.NewGuid();
        var corr = Guid.NewGuid().ToString();

        var emittiert = new List<IPipelineOutput>();
        PipelineContext? gesehenerCtx = null;

        // Fake-Dispatch: spielt einen Event-Handler nach, der genau ein Command yieldet.
        PipelineEventPullBridge.EventDispatch dispatch = async (env, ctx, sendCommand, _, _, _) =>
        {
            gesehenerCtx = ctx;
            await sendCommand(new FakeCommand(env.AggregateId));
        };

        Func<EventEnvelope, Func<IPipelineOutput, Task>> emitFactory =
            _ => payload => { emittiert.Add(payload); return Task.CompletedTask; };
        Func<IPipelineTrigger, string, Task> sendTrigger = (_, _) => Task.CompletedTask;

        var pullDispatch = PipelineEventPullBridge.Wrap(dispatch, emitFactory, sendTrigger);

        await pullDispatch(Env(stream, 7, corr), new ProjectionWriter(stream, 7));

        emittiert.Should().ContainSingle().Which.Should().BeOfType<FakeCommand>();
        gesehenerCtx.Should().NotBeNull();
        gesehenerCtx!.SourceAggregateId.Should().Be(stream);
        gesehenerCtx.SourceAggregateVersion.Should().Be(7);
        gesehenerCtx.CorrelationId.Should().Be(corr);
    }

    [Fact]
    public async Task Event_yieldet_Trigger_laeuft_ueber_die_Trigger_Naht()
    {
        var triggerGesendet = new List<IPipelineTrigger>();

        PipelineEventPullBridge.EventDispatch dispatch = async (_, _, _, sendTrigger, _, _) =>
            await sendTrigger(new FakeTrigger());

        Func<EventEnvelope, Func<IPipelineOutput, Task>> emitFactory =
            _ => _ => Task.CompletedTask;
        Func<IPipelineTrigger, string, Task> sendTrigger =
            (t, _) => { triggerGesendet.Add(t); return Task.CompletedTask; };

        var pullDispatch = PipelineEventPullBridge.Wrap(dispatch, emitFactory, sendTrigger);

        await pullDispatch(Env(Guid.NewGuid(), 1, ""), new ProjectionWriter(Guid.NewGuid(), 1));

        triggerGesendet.Should().ContainSingle().Which.Should().BeOfType<FakeTrigger>();
    }

    [Fact]
    public async Task Transient_yield_laeuft_ueber_die_Emit_Naht()
    {
        var stream = Guid.NewGuid();
        var emittiert = new List<IPipelineOutput>();

        PipelineEventPullBridge.EventDispatch dispatch = async (env, _, _, _, broadcastTransient, _) =>
            await broadcastTransient(new FakeTransient(env.AggregateId));

        var pullDispatch = PipelineEventPullBridge.Wrap(
            dispatch,
            _ => p => { emittiert.Add(p); return Task.CompletedTask; },
            (_, _) => Task.CompletedTask);

        await pullDispatch(Env(stream, 3, ""), new ProjectionWriter(stream, 3));

        emittiert.Should().ContainSingle().Which.Should().BeOfType<FakeTransient>();
    }

    [Fact]
    public async Task Frist_Planung_laeuft_ueber_die_Frist_Naht()
    {
        var stream = Guid.NewGuid();
        var geplant = new List<FristAuftrag>();

        PipelineEventPullBridge.EventDispatch dispatch = async (env, _, _, _, _, plane) =>
            await plane(new FristAuftrag("Probe.Cmd", env.AggregateId, TimeSpan.FromMinutes(5)));

        var pullDispatch = PipelineEventPullBridge.Wrap(dispatch, _ => _ => Task.CompletedTask, (_, _) => Task.CompletedTask,
            f => { geplant.Add(f); return Task.CompletedTask; });

        await pullDispatch(Env(stream, 2, ""), new ProjectionWriter(stream, 2));

        geplant.Should().ContainSingle().Which.ZielAggregatId.Should().Be(stream);
        geplant[0].FristId.Should().Be(FristId.Für("Probe.Cmd", stream), "Planen und Storno treffen dieselbe deterministische Frist");
    }

    [Fact]
    public async Task Selbst_Planung_aus_Event_Handle_wird_abgelehnt()
    {
        PipelineEventPullBridge.EventDispatch dispatch = async (_, _, _, _, _, plane) =>
            await plane(Selbst.In(new FakeTick(), TimeSpan.FromSeconds(1)));

        var pullDispatch = PipelineEventPullBridge.Wrap(dispatch, _ => _ => Task.CompletedTask, (_, _) => Task.CompletedTask);

        var akt = () => pullDispatch(Env(Guid.NewGuid(), 1, ""), new ProjectionWriter(Guid.NewGuid(), 1));
        await akt.Should().ThrowAsync<NotSupportedException>("der Pull-Pfad hat keine Actor-Mailbox für Selbst-Nachrichten");
    }

    private sealed record FakeTick : IPipelineSelfMessage;
}
