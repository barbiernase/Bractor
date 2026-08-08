using FluentAssertions;
using Infrastructure.Mapping;

namespace Infrastructure.Pruefstand.Mapping;

/// <summary>
/// P1a (TG-1): die „erlaubten Commands" für ein Event werden jetzt PRÄZISE aus den Decider-Signaturen
/// abgeleitet (GeneratedCommandRouting: CommandToAggregate + CommandToEvents), nicht mehr aus Namespace-
/// Gruppierung (die gelöschte GeneratedEventCommandMapping). Beweis am REALEN Generat: ein Konto-Event
/// liefert die Konto-Geschwister-Commands und KEINE fremden (ImagePair-)Commands.
/// </summary>
public class EventCommandDerivationTests
{
    [Fact]
    public void Konto_Event_liefert_die_Geschwister_Commands_des_Konto_Aggregats()
    {
        var erlaubt = MessageTypeMapping.GetAllowedCommandNames(new[] { nameof(Domain.Konto.BetragReserviert) });

        erlaubt.Should().Contain("ReserviereBetrag");      // Konto-Command (Geschwister)
        erlaubt.Should().Contain("BucheReservierung");     // Konto-Command (Geschwister)
        erlaubt.Should().NotContain("ErstelleImagePair");  // Fremd-Aggregat → NICHT erlaubt
    }

    [Fact]
    public void Unbekanntes_Event_liefert_keine_Commands()
    {
        MessageTypeMapping.GetAllowedCommandNames(new[] { "GibtsNichtEvent" }).Should().BeEmpty();
    }
}
