using FluentAssertions;
using Infrastructure.Prozess;

namespace Infrastructure.Pruefstand.Phase5;

/// <summary>
/// P5(a) — der Poll-Filter des Prozess-Backstops. Beweist: das terminale Ergebnis-Event (Auslöser KEINER
/// Regel → kein teilnehmender Typ) wird jetzt über die CorrelationId eines OFFENEN Prozesses geroutet
/// (Terminal-Fix, Befund 2), OHNE fremde Events über-zu-wecken. Der Router selbst konnte das schon
/// (RouteAsync-else-Zweig); der Fix sitzt allein in diesem Filter.
/// </summary>
public class ProzessPollFilterTests
{
    private sealed record TeilnehmendesEvent;
    private sealed record TerminalesEvent;   // Auslöser keiner Regel → nicht teilnehmend

    private static readonly IReadOnlySet<Type> Teilnehmend = new HashSet<Type> { typeof(TeilnehmendesEvent) };

    [Fact]
    public void Terminales_Event_eines_offenen_Prozesses_wird_geroutet()
    {
        var korr = Guid.NewGuid();
        var offene = new HashSet<Guid> { korr };

        ProzessPollFilter.SollRouten(typeof(TerminalesEvent), korr.ToString(), Teilnehmend, offene)
            .Should().BeTrue("das terminale Ergebnis-Event trägt die Korrelation des offenen Prozesses");
    }

    [Fact]
    public void Fremdes_Event_mit_unbekannter_Korrelation_wird_NICHT_geroutet()
    {
        var offene = new HashSet<Guid> { Guid.NewGuid() };

        ProzessPollFilter.SollRouten(typeof(TerminalesEvent), Guid.NewGuid().ToString(), Teilnehmend, offene)
            .Should().BeFalse("kein Über-Wecken: die Korrelation gehört zu keinem offenen Prozess");
    }

    [Fact]
    public void Teilnehmendes_Event_wird_weiter_geroutet()
    {
        ProzessPollFilter.SollRouten(typeof(TeilnehmendesEvent), "kein-guid", Teilnehmend, new HashSet<Guid>())
            .Should().BeTrue("teilnehmende Typen routen wie bisher — unabhängig von der Korrelation");
    }

    [Fact]
    public void Nicht_parsebare_CorrelationId_ohne_Teilnahme_verpufft()
    {
        ProzessPollFilter.SollRouten(typeof(TerminalesEvent), "", Teilnehmend, new HashSet<Guid> { Guid.NewGuid() })
            .Should().BeFalse();
    }
}
