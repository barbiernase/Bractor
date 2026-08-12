using System;
using Abstractions;
using FluentAssertions;
using Infrastructure.Aggregate.ActorSystem;
using Xunit;

namespace Infrastructure.Pruefstand.Dispatcher;

/// <summary>
/// T2a — der Dispatcher benachrichtigt den auslösenden Client, wenn ein Command nach erschöpfter
/// Platzierung NIE einen Actor erreicht (sonst still, weil das CommandFailed sonst der Actor publiziert).
/// Getestet wird die reine Envelope-Konstruktion (<see cref="ProtoActorAggregateDispatcher.BaueCommandFailed"/>),
/// store-/cluster-frei. Der eigentliche Cluster-Send ist wie der Dead-Letter-Pfad nur integrationsweise prüfbar.
/// </summary>
public class DispatcherCommandFailedTests
{
    private record TestCommand(Guid AggregateId) : ICommand;

    private static CommandEnvelope Envelope(string? originSessionId) => new()
    {
        AggregateId = Guid.NewGuid(),
        Payload = new TestCommand(Guid.NewGuid()),
        Modus = new CommandModus.Client(0),
        AggregateType = "Konto",
        OriginSessionId = originSessionId,
    };

    [Fact]
    public void Ohne_OriginSessionId_kein_Envelope()
    {
        // Kein Ziel für Targeted Delivery → nichts zu publizieren.
        ProtoActorAggregateDispatcher.BaueCommandFailed(Envelope(null), "Timeout", 3).Should().BeNull();
        ProtoActorAggregateDispatcher.BaueCommandFailed(Envelope(""), "Timeout", 3).Should().BeNull();
    }

    [Fact]
    public void Mit_OriginSessionId_targeted_CommandFailed()
    {
        var env = Envelope("session-abc");
        var failed = ProtoActorAggregateDispatcher.BaueCommandFailed(env, "Cluster nicht routbar", 3);

        failed.Should().NotBeNull();
        failed!.TargetSubscriberId.Should().Be("session-abc");          // targeted an den auslösenden Client
        failed.CorrelationId.Should().Be(env.CorrelationId);            // Client matched darüber
        failed.CausationId.Should().Be(env.CommandId.ToString());       // Ursache = der Command
        failed.AggregateId.Should().Be(env.AggregateId);

        var payload = failed.Payload.Should().BeOfType<CommandFailed>().Subject;
        payload.CommandType.Should().Be(nameof(TestCommand));
        payload.Reason.Should().Contain("Nicht zugestellt").And.Contain("Cluster nicht routbar");
    }
}
