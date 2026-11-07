using Abstractions;
using Pruefdomaene.Abrechnung;

namespace Pruefdomaene.Pipeline.Abrechnung;

/// <summary>
/// Die Freigabe einer gestellten Rechnung: bis 10 000 frei, darüber prüft ein Mensch — die Rechnung liegt ihm dann vor
/// (<see cref="LegeRechnungZurPruefungVor"/>); auf das Fakt reagiert der Rechnungsprüfer, der frei gibt oder sperrt.
/// </summary>
public sealed class Freigabeautomat : IFunktion
{
    public const decimal Grenze = 10_000;

    public OneOf<GibRechnungFrei, LegeRechnungZurPruefungVor> Auf(Freizugeben f) =>
        f.Betrag > Grenze ? new LegeRechnungZurPruefungVor(f.Rechnung) : new GibRechnungFrei(f.Rechnung);
}
