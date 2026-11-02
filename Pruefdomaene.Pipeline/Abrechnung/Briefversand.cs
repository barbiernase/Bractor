using Abstractions;
using Pruefdomaene.Abrechnung;

namespace Pruefdomaene.Pipeline.Abrechnung;

/// <summary>Der Versand der Prüfdomäne: die Versandnummer aus Rechnung und Stufe (Tests setzen einen Versand ein, der jedes Mal anders antwortet).</summary>
public sealed class Briefversand : IMahnversand
{
    public Task<string> VersendeAsync(Id<Kundenkonto> konto, Id<Rechnung> rechnung, int stufe) => Task.FromResult($"brief-{rechnung.Wert:N}-{stufe}");
}
