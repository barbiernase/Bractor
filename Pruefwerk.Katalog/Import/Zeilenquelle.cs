namespace Pruefwerk.Katalog.Import;

/// <summary>Die eine Umsetzung der Zeilenquelle: ein Wert aus der Zeile.</summary>
public sealed class Zeilenquelle : IZeilenquelle
{
    public Task<int> LiesAsync(Guid vorgang, int zeile, CancellationToken ct) => Task.FromResult(zeile % 7 + 1);
}
