using Abstractions;

namespace Pruefdomaene.Pipeline.Import;

/// <summary>Gerufen (kein Auf): woher ein Import seine Zeilen liest (Außenwelt) — der Bestand der Zeile <c>zeile</c> des Imports.</summary>
public interface IZeilenquelle : IFunktion
{
    Task<int> LiesAsync(Guid import, int zeile, CancellationToken ct);
}
