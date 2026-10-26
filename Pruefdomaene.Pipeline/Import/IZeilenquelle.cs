namespace Pruefdomaene.Pipeline.Import;

/// <summary>Woher ein Import seine Zeilen liest (Außenwelt): der Bestand der Zeile <c>zeile</c> des Imports.</summary>
public interface IZeilenquelle : Abstractions.IDienst
{
    Task<int> LiesAsync(Guid import, int zeile, CancellationToken ct);
}
