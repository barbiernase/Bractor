using Abstractions;
using FluentAssertions;
using Infrastructure.Prozess;

namespace Infrastructure.Pruefstand.Prozess;

/// <summary>
/// Der Filter des Prozess-Polls (PF-4): ein Auslöser startet seinen Vorgang (Korrelation deterministisch), jedes andere Event weckt nur
/// über die CorrelationId eines OFFENEN Vorgangs — so auch das terminale Ergebnis-Event, das Auslöser keiner Regel ist (Befund 2) —,
/// fremde Events wecken niemanden. Ein Start weckt nur, wenn der Vorgang offen ist oder es ihn noch nicht gibt.
/// </summary>
public class ProzessPollFilterTests
{
    private sealed record AuslöserEvent : IEvent;
    private sealed record TerminalesEvent : IEvent;   // Auslöser keiner Regel

    private static readonly IReadOnlyDictionary<Type, string> Auslöser = new Dictionary<Type, string> { [typeof(AuslöserEvent)] = "P" };

    private static EventEnvelope Env(IEvent e, string korrelation = "", Guid? stream = null, int version = 1)
        => new() { Payload = e, CorrelationId = korrelation, AggregateId = stream ?? Guid.NewGuid(), AggregateVersion = version };

    [Fact]
    public void Terminales_Event_eines_offenen_Prozesses_weckt_ihn()
    {
        var korr = Guid.NewGuid();
        var w = ProzessPollFilter.Weckung(Env(new TerminalesEvent(), korr.ToString()), Auslöser, new HashSet<Guid> { korr });

        w!.Korrelation.Should().Be(korr, "das terminale Ergebnis-Event trägt die Korrelation des offenen Prozesses");
        w.ProzessNameFürStart.Should().BeNull();
    }

    [Fact]
    public void Fremdes_oder_nicht_parsebares_Event_weckt_niemanden()
    {
        var offene = new HashSet<Guid> { Guid.NewGuid() };
        ProzessPollFilter.Weckung(Env(new TerminalesEvent(), Guid.NewGuid().ToString()), Auslöser, offene)
            .Should().BeNull("kein Über-Wecken: die Korrelation gehört zu keinem offenen Prozess");
        ProzessPollFilter.Weckung(Env(new TerminalesEvent(), ""), Auslöser, offene).Should().BeNull();
    }
}
