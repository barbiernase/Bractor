using Abstractions;
using Pruefdomaene.Abrechnung;

namespace Pruefdomaene.Pipeline.Abrechnung;

/// <summary>
/// Eine Funktion vor dem Aggregat (docs/04 §4.4): eine bezahlte Rechnung wird dem Kundenkonto gutgeschrieben — sie nimmt <see cref="Gutschreibung"/> am Kopf
/// (ein Client, der auf <c>RechnungBezahlt</c> reagiert, gibt sie hinein), die Command-Id folgt aus der Kennung des Eingangs.
/// </summary>
public sealed class Gutschrift : IFunktion
{
    public BucheGutschrift Auf(Gutschreibung f) => new(f.Konto, f.Rechnung, f.Betrag);
}
