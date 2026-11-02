namespace Pruefdomaene.Pipeline.Import;

/// <summary>Technik (im Konstruktor des <see cref="Importlauf"/>): woher ein Import seine Zeilen liest (Außenwelt) — der Bestand der Zeile <c>zeile</c> des Imports.</summary>
public interface IZeilenquelle
{
    Task<int> LiesAsync(Guid import, int zeile, CancellationToken ct);
}

/// <summary>Die Zeilen der Prüfdomäne: ein Bestand aus der Zeilennummer (Tests setzen eine Quelle ein, die mitzählt oder abbricht).</summary>
public sealed class Zeilenquelle : IZeilenquelle
{
    public Task<int> LiesAsync(Guid import, int zeile, CancellationToken ct) => Task.FromResult(zeile % 7 + 1);
}
