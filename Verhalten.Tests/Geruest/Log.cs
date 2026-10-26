using Npgsql;

namespace Verhalten.Tests.Geruest;

/// <summary>
/// Das fachliche Soll steht im Log (Inv. 1): gezählt werden FACHLICHE Events je Stream — nie Prozess-Events, Marken oder Funktions-Streams,
/// die der Umbau abschafft. Gelesen wird die Event-Tabelle direkt (Marten: <c>mt_events</c>, Typ als <c>snake_case</c> des Klassennamens);
/// verglichen wird der Typ ohne Unterstriche gegen den Klassennamen, damit kein Name von Hand übersetzt werden muss.
/// </summary>
public sealed class Log(Testumgebung u)
{
    private static string Typ<T>() => typeof(T).Name.ToLowerInvariant();

    /// <summary>Wie oft ein fachliches Event im Stream steht.</summary>
    public Task<long> ZahlAsync<T>(Guid stream) => ZahlAsync(Typ<T>(), stream);

    /// <summary>Wie oft ein fachliches Event im ganzen Log steht.</summary>
    public async Task<long> ZahlAsync<T>() =>
        await SkalarAsync($"select count(*) from {Testumgebung.Schema}.mt_events where replace(type, '_', '') = @t and not is_archived", ("t", Typ<T>()));

    /// <summary>Wie oft ein fachliches Event mit diesem Feldwert im ganzen Log steht (z. B. eine Quell-Nachricht je Dateiname).</summary>
    public async Task<long> ZahlMitFeldAsync<T>(string feld, string wert) =>
        await SkalarAsync($"select count(*) from {Testumgebung.Schema}.mt_events where replace(type, '_', '') = @t and data->>@f = @w and not is_archived",
            ("t", Typ<T>()), ("f", feld), ("w", wert));

    public async Task<long> ZahlAsync(string typ, Guid stream) =>
        await SkalarAsync($"select count(*) from {Testumgebung.Schema}.mt_events where stream_id = @s and replace(type, '_', '') = @t and not is_archived",
            ("s", stream), ("t", typ.ToLowerInvariant()));

    /// <summary>Die Daten (jsonb als Text) aller Events eines Typs im Stream, in Versionsreihenfolge.</summary>
    public async Task<IReadOnlyList<string>> DatenAsync<T>(Guid stream)
    {
        await using var conn = new NpgsqlConnection(u.DbVerbindung);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            $"select data::text from {Testumgebung.Schema}.mt_events where stream_id = @s and replace(type, '_', '') = @t and not is_archived order by version", conn);
        cmd.Parameters.AddWithValue("s", stream);
        cmd.Parameters.AddWithValue("t", Typ<T>());
        var liste = new List<string>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) liste.Add(r.GetString(0));
        return liste;
    }

    /// <summary>Version je Event eines Streams (Typ ohne Unterstriche) — das, was ein Client aus den Pushes kennen muss.</summary>
    public async Task<IReadOnlyList<(int Version, string Typ)>> StreamAsync(Guid stream)
    {
        await using var conn = new NpgsqlConnection(u.DbVerbindung);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(
            $"select version, replace(type, '_', '') from {Testumgebung.Schema}.mt_events where stream_id = @s and not is_archived order by version", conn);
        cmd.Parameters.AddWithValue("s", stream);
        var liste = new List<(int, string)>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) liste.Add(((int)r.GetInt64(0), r.GetString(1)));
        return liste;
    }

    private async Task<long> SkalarAsync(string sql, params (string Name, object Wert)[] p)
    {
        await using var conn = new NpgsqlConnection(u.DbVerbindung);
        await conn.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, conn);
        foreach (var (n, w) in p) cmd.Parameters.AddWithValue(n, w);
        return Convert.ToInt64(await cmd.ExecuteScalarAsync());
    }
}
