using Abstractions;
using Pruefdomaene.Abrechnung;

namespace Pruefdomaene.Pipeline.Abrechnung;

/// <summary>
/// Die Freigabe einer gestellten Rechnung: bis 10 000 frei, darüber prüft ein Mensch — der Command <see cref="RechnungZuPruefen"/> verlässt den Graphen
/// als Arbeit des Rechnungsprüfers (er hört ihn), der frei gibt oder sperrt.
/// </summary>
public sealed class Freigabeautomat : IFunktion
{
    public const decimal Grenze = 10_000;

    public OneOf<GibRechnungFrei, RechnungZuPruefen> Auf(Freizugeben f) =>
        f.Betrag > Grenze ? new RechnungZuPruefen(f.Rechnung, f.Betrag) : new GibRechnungFrei(f.Rechnung);
}
