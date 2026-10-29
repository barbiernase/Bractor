using System;
using Abstractions;
using FluentAssertions;
using Infrastructure.Aggregate.ActorSystem;
using Xunit;

namespace Infrastructure.Pruefstand.Dispatcher;

/// <summary>
/// Die Quittung eines Client-Commands (docs/08 §8.4, FB-22): hat der Actor geantwortet, bekommt die Sitzung, die den Command schickte, gezielt
/// eine <see cref="CommandQuittung"/> mit Ausgang und Version — auch für einen Noop und für Events, die der Client nicht hört. Getestet wird die
/// reine Envelope-Konstruktion (<see cref="ProtoActorAggregateDispatcher.BaueQuittung"/>); den Weg über die Leitung prüft das Verhaltens-Netz.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class DispatcherQuittungTests
{
    private record TestCommand(Id<Infrastructure.Pruefstand.Tests.TestZiel> AggregateId) : ICommand<Infrastructure.Pruefstand.Tests.TestZiel>;

    private static CommandEnvelope Envelope(string? sitzung) => new()
    {
        Payload = new TestCommand(new(Guid.NewGuid())),
        Modus = new CommandModus.Client(3),
        AggregateType = "Konto",
        CorrelationId = "k-1",
        OriginSessionId = sitzung,
    };

    [Fact]
    public void Ein_geschriebener_Command_wird_mit_Version_quittiert()
    {
        var env = Envelope("knoten#0001");
        var e = ProtoActorAggregateDispatcher.BaueQuittung(env, new CommandResult { Success = true, NewVersion = 5, ErsteVersion = 4 })!;

        e.ZielSitzung.Should().Be("knoten#0001", "gezielt an die Sitzung, die ihn schickte — gleich, was sie hört");
        e.CorrelationId.Should().Be("k-1", "über die Korrelation beendet der Client sein Warten");
        e.CausationId.Should().Be(env.CommandId.ToString());
        e.AggregateVersion.Should().Be(5);
        e.Payload.Should().Be(new CommandQuittung(nameof(TestCommand), env.AggregateId, 5, Geschrieben: true));
    }

    [Fact]
    public void Ein_Noop_und_eine_erkannte_Wiederholung_werden_ohne_Geschriebenes_quittiert()
    {
        var env = Envelope("knoten#0001");
        var q = (CommandQuittung)ProtoActorAggregateDispatcher.BaueQuittung(env, new CommandResult { Success = true, NewVersion = 7 })!.Payload;
        q.Geschrieben.Should().BeFalse();
        q.Version.Should().Be(7, "der Stand für den nächsten Command und für „mindestens S auf v“");
    }

    [Fact]
    public void Eine_Ablehnung_wird_als_abgelehnt_quittiert()
    {
        var q = (CommandQuittung)ProtoActorAggregateDispatcher.BaueQuittung(Envelope("knoten#0001"),
            new CommandResult { Success = false, Abgelehnt = true, NewVersion = 2 })!.Payload;
        q.Abgelehnt.Should().BeTrue();
        q.Geschrieben.Should().BeFalse();
    }

    [Fact]
    public void Ein_technischer_Fehler_und_ein_Command_ohne_Sitzung_bekommen_keine_Quittung()
    {
        ProtoActorAggregateDispatcher.BaueQuittung(Envelope("knoten#0001"), new CommandResult { Success = false, ErrorMessage = "x" })
            .Should().BeNull("dafür meldet der Actor CommandFailed");
        ProtoActorAggregateDispatcher.BaueQuittung(Envelope(null), new CommandResult { Success = true, NewVersion = 1 }).Should().BeNull();
        ProtoActorAggregateDispatcher.BaueQuittung(Envelope(""), new CommandResult { Success = true, NewVersion = 1 }).Should().BeNull();
    }
}
