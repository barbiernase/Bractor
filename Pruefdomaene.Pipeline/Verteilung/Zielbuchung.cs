using Abstractions;
using Pruefdomaene.Posten;

namespace Pruefdomaene.Pipeline.Verteilung;

/// <summary>Bucht ein Ziel zu (im Unterlauf seines Ziels); die Zubuchung hört sie als Antwort auf ihren Command und meldet das Ziel gebucht.</summary>
public sealed class Zielbuchung : IFunktion
{
    public BuchePostenZu Auf(ZielZubuchen z) => new(z.Ziel, z.Menge, new Beleg(Belegart.Verteilung, z.VerteilungId.Wert));

    public ZielGebucht Auf(PostenZugebucht e) => new(new(e.Beleg.Nummer), e.Menge);
}
