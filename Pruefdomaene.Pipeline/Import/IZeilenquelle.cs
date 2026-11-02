namespace Pruefdomaene.Pipeline.Import;

/// <summary>Technik (im Konstruktor des <see cref="Importlauf"/>): woher ein Import seine Zeilen liest (Außenwelt) — der Bestand der Zeile <c>zeile</c> des Imports.</summary>
public interface IZeilenquelle
{
    Task<int> LiesAsync(Guid import, int zeile, CancellationToken ct);
}
