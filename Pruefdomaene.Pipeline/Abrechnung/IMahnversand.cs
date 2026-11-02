using Abstractions;
using Pruefdomaene.Abrechnung;

namespace Pruefdomaene.Pipeline.Abrechnung;

/// <summary>Technik (im Konstruktor des <see cref="Mahnwesen"/>): der Versand einer Mahnung (Außenwelt) — liefert die Versandnummer des Briefs.</summary>
public interface IMahnversand
{
    Task<string> VersendeAsync(Id<Kundenkonto> konto, Id<Rechnung> rechnung, int stufe);
}
