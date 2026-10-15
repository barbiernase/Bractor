using Abstractions;
using FluentAssertions;
using Infrastructure.Prozess;

namespace Infrastructure.Pruefstand.Phase5;

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
    public void Ein_Ausloeser_startet_seinen_Vorgang_mit_der_deterministischen_Korrelation()
    {
        var stream = Guid.NewGuid();
        var w = ProzessPollFilter.Weckung(Env(new AuslöserEvent(), stream: stream, version: 3), Auslöser, new HashSet<Guid>());

        w.Should().Be(new ProzessPollWeckung(ProzessId.Für("P", stream, 3), stream, 3, "P"));
    }

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

    [Fact]
    public void Ein_Start_weckt_nur_einen_offenen_oder_noch_nicht_vorhandenen_Vorgang()
    {
        var (offen, beendet, neu) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var offene = new HashSet<Guid> { offen };
        var vorhanden = new HashSet<Guid> { offen, beendet };

        ProzessPollFilter.StartNoetig(offen, offene, vorhanden).Should().BeTrue();
        ProzessPollFilter.StartNoetig(neu, offene, vorhanden).Should().BeTrue("ein verlorenes Start-Signal startet den Vorgang über den Poll");
        ProzessPollFilter.StartNoetig(beendet, offene, vorhanden).Should().BeFalse("ein beendeter Vorgang braucht keine Weckung");
    }
}
