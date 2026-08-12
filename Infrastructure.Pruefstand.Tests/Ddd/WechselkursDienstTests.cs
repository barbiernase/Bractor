using Domain.Verkauf;
using FluentAssertions;

namespace Infrastructure.Pruefstand.Ddd;

/// <summary>
/// DDD-Baustein DOMAIN SERVICE — zustandslose Operation über dem Value Object
/// <see cref="Geldwert"/>, die zu keinem Aggregat gehört (Währungsumrechnung).
/// </summary>
public class WechselkursDienstTests
{
    private static WechselkursDienst Dienst() => new(new Dictionary<(Waehrung, Waehrung), decimal>
    {
        [(Waehrung.EUR, Waehrung.USD)] = 1.10m,
        [(Waehrung.USD, Waehrung.EUR)] = 0.90m,
    });

    [Fact]
    public void Rechnet_um_und_laesst_gleiche_Waehrung_unveraendert()
    {
        var dienst = Dienst();
        dienst.Rechne(Geldwert.Euro(100), Waehrung.USD).Should().Be(Geldwert.Von(110, Waehrung.USD));
        dienst.Rechne(Geldwert.Euro(100), Waehrung.EUR).Should().Be(Geldwert.Euro(100));
    }

    [Fact]
    public void Verlangt_einen_hinterlegten_Kurs()
    {
        var akt = () => Dienst().Rechne(Geldwert.Euro(100), Waehrung.CHF);
        akt.Should().Throw<InvalidOperationException>().WithMessage("*Wechselkurs*");
    }
}
