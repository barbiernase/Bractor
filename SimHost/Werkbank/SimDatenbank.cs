using Abstractions;
using Npgsql;

namespace SimHost.Werkbank;

/// <summary>
/// Die Sim-Datenbank (docs/konzept-werkbank.md §4.6), nur für Umgebungen der Art Sim: zurücksetzen (löschen — der Host legt sie beim
/// nächsten Start leer neu an und siegelt sie), Stand speichern (<c>CREATE DATABASE &lt;db&gt;_stand_&lt;x&gt; TEMPLATE &lt;db&gt;</c>), Stand laden.
/// Der Host muss dabei stehen (eine Vorlage verträgt keine Verbindungen) — das ordnet der Aufrufer an.
/// </summary>
public sealed class SimDatenbank
{
    private static void NurSim(UmgebungKonfig u)
    {
        if (u.Umgebung.Art != UmgebungsArt.Sim) throw new InvalidOperationException($"Umgebung {u.Umgebung.Name} ist {u.Umgebung.Art} — nur Sim-Datenbanken werden verwaltet.");
    }

    private static string Verwaltung(UmgebungKonfig u) => new NpgsqlConnectionStringBuilder(u.EventStore.Verbindung) { Database = "postgres" }.ConnectionString;

    private static async Task SqlAsync(UmgebungKonfig u, string sql)
    {
        await using var c = new NpgsqlConnection(Verwaltung(u));
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    public static string StandName(UmgebungKonfig u, string stand) => $"{u.EventStore.Datenbank}_stand_{Umgebungen.BranchSicher(stand)}";

    public async Task ZuruecksetzenAsync(UmgebungKonfig u)
    {
        NurSim(u);
        await SqlAsync(u, $"drop database if exists \"{u.EventStore.Datenbank}\" with (force)");
    }

    public async Task SpeichereAsync(UmgebungKonfig u, string stand)
    {
        NurSim(u);
        var ziel = StandName(u, stand);
        await SqlAsync(u, $"drop database if exists \"{ziel}\" with (force)");
        await SqlAsync(u, $"create database \"{ziel}\" template \"{u.EventStore.Datenbank}\"");
    }

    public async Task LadeAsync(UmgebungKonfig u, string stand)
    {
        NurSim(u);
        await SqlAsync(u, $"drop database if exists \"{u.EventStore.Datenbank}\" with (force)");
        await SqlAsync(u, $"create database \"{u.EventStore.Datenbank}\" template \"{StandName(u, stand)}\"");
    }

    public async Task<IReadOnlyList<string>> StaendeAsync(UmgebungKonfig u)
    {
        var praefix = u.EventStore.Datenbank + "_stand_";
        await using var c = new NpgsqlConnection(Verwaltung(u));
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "select datname from pg_database where datname like @p order by datname";
        cmd.Parameters.AddWithValue("p", praefix + "%");
        var aus = new List<string>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync()) aus.Add(r.GetString(0)[praefix.Length..]);
        return aus;
    }
}
