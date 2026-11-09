namespace Pruefwerk.Katalog.Import;

/// <summary>Technik hinter dem Zeilenlauf (ohne Marker): liest eine Zeile eines Vorgangs.</summary>
public interface IZeilenquelle
{
    Task<int> LiesAsync(Guid vorgang, int zeile, CancellationToken ct);
}
