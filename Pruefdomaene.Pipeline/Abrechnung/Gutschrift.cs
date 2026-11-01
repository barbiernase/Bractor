using Abstractions;
using Pruefdomaene.Abrechnung;

namespace Pruefdomaene.Pipeline.Abrechnung;

/// <summary>
/// Eine Funktion vor dem Aggregat (docs/04 §4.4): eine bezahlte Rechnung wird dem Kundenkonto gutgeschrieben — sie hört die Folge <see cref="Gutschreibung"/>
/// des Decide, die Command-Id folgt aus der Kennung des Eingangs.
/// </summary>
public sealed class Gutschrift : IFunktion
{
    public BucheGutschrift Auf(Gutschreibung f) => new(f.Konto, f.Rechnung, f.Betrag);
}
