using Abstractions;
using Pruefdomaene.Abrechnung;

namespace Pruefdomaene.Pipeline.Abrechnung;

/// <summary>Der Versand einer Mahnung (Außenwelt): liefert die Versandnummer des Briefs.</summary>
public interface IMahnversand : IDienst
{
    Task<string> VersendeAsync(Id<Kundenkonto> konto, Id<Rechnung> rechnung, int stufe);
}
