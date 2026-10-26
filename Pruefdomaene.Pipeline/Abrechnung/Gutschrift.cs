using Abstractions;
using Pruefdomaene.Abrechnung;

namespace Pruefdomaene.Pipeline.Abrechnung;

/// <summary>
/// Eine gleichbleibende Funktion (R12): keine Dienste, keine Lese-Fähigkeit, weder Uhr noch Zufall — derselbe Eingang ergibt denselben Command mit
/// derselben Id. Sie sendet ohne Festhalten: eine bezahlte Rechnung wird dem Kundenkonto gutgeschrieben (die eine Kante Event → Command).
/// </summary>
public sealed class Gutschrift : IFunktion
{
    public BucheGutschrift Auf(RechnungBezahlt e) => new(e.Konto, e.Rechnung, e.Betrag);
}
