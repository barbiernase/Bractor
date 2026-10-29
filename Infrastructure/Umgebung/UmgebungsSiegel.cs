using Abstractions;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace Infrastructure.Umgebung;

/// <summary>
/// Das Umgebungs-Siegel (docs/06 §6.5): jede Datenbank trägt in <c>&lt;schema&gt;.umgebung</c> Name, Art und
/// Cluster-Namen ihrer Umgebung. Ein Host startet nur, wenn seine Umgebung zum Siegel passt — ein Sim-Host schreibt so nie in die
/// Prod-DB und umgekehrt. Eine leere DB bekommt das Siegel beim ersten Start; eine Sim-DB legt der Host bei Bedarf an (nie Prod).
/// Dazu die Tabelle <c>&lt;schema&gt;.uhr</c> (Versatz der DB-Uhr, nur in Sim wirksam, §4.4).
/// </summary>
public sealed class UmgebungsSiegel(UmgebungKonfig umgebung) : IHostedService
{
    public sealed record Siegel(string Name, string Art, string Cluster);

    /// <summary>Der reine Vergleich: null = passt, sonst der Grund.</summary>
    public static string? Pruefe(Siegel? gespeichert, Siegel erwartet) => gespeichert switch
    {
        null => null,
        _ when gespeichert.Art != erwartet.Art => $"die Datenbank gehört zu einer {gespeichert.Art}-Umgebung ('{gespeichert.Name}'), dieser Host ist {erwartet.Art}",
        _ when gespeichert.Name != erwartet.Name => $"die Datenbank gehört zur Umgebung '{gespeichert.Name}', dieser Host ist '{erwartet.Name}'",
        _ when gespeichert.Cluster != erwartet.Cluster => $"die Datenbank gehört zum Cluster '{gespeichert.Cluster}', dieser Host tritt '{erwartet.Cluster}' bei",
        _ => null,
    };

    public static Siegel Von(UmgebungKonfig u) => new(u.Umgebung.Name, u.Umgebung.Art.ToString(), u.Cluster.Name);

    public async Task StartAsync(CancellationToken ct)
    {
        if (umgebung.Umgebung.Art == UmgebungsArt.Sim) await LegeDatenbankAnAsync(ct);
        var schema = umgebung.EventStore.Schema;
        await using var conn = new NpgsqlConnection(UmgebungLader.Verbindung(umgebung.EventStore));
        await conn.OpenAsync(ct);
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"""
                create schema if not exists "{schema}";
                create table if not exists "{schema}".umgebung (id int primary key default 1 check (id = 1), name text not null, art text not null, cluster text not null);
                create table if not exists "{schema}".uhr (id int primary key default 1 check (id = 1), versatz interval not null default interval '0');
                insert into "{schema}".uhr (id) values (1) on conflict (id) do nothing;
                """;
            await cmd.ExecuteNonQueryAsync(ct);
        }
        Siegel? gespeichert = null;
        await using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = $"select name, art, cluster from \"{schema}\".umgebung where id = 1";
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct)) gespeichert = new Siegel(r.GetString(0), r.GetString(1), r.GetString(2));
        }
        var erwartet = Von(umgebung);
        if (Pruefe(gespeichert, erwartet) is { } grund)
            throw new BootFehler($"Umgebungs-Siegel: {grund}. Start abgebrochen (Datenbank {umgebung.EventStore.Datenbank}).", "host:EventStore");
        if (gespeichert == null)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"insert into \"{schema}\".umgebung (id, name, art, cluster) values (1, @n, @a, @c) on conflict (id) do nothing";
            cmd.Parameters.AddWithValue("n", erwartet.Name);
            cmd.Parameters.AddWithValue("a", erwartet.Art);
            cmd.Parameters.AddWithValue("c", erwartet.Cluster);
            await cmd.ExecuteNonQueryAsync(ct);
        }
        if (umgebung.Umgebung.Art == UmgebungsArt.Prod)
        {
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"update \"{schema}\".uhr set versatz = interval '0' where id = 1";
            await cmd.ExecuteNonQueryAsync(ct);
        }
        await SchreibeLaufstandAsync(conn, schema, ct);
        Console.WriteLine($"[CQRS] Umgebung {erwartet.Name} ({erwartet.Art}) · DB {umgebung.EventStore.Datenbank} · Cluster {erwartet.Cluster} — Siegel ok"
            + (Spur.Stand is { } st ? $" · Stand {st}" : ""));
    }

    /// <summary>Schlüssel in <c>&lt;schema&gt;.stand</c>: das <c>domain.proto</c> und der Build-Stand des Hosts, der zuletzt startete.</summary>
    public const string ProtoSchluessel = "proto", CodeSchluessel = "code";

    /// <summary>
    /// Der Host schreibt beim Start — in JEDER Umgebung — seinen Proto-Stand (aus dem Build eingebettet) und seinen Build-Stand in
    /// <c>&lt;schema&gt;.stand</c>. So kennt ein übernommener Prod-Stand seine Herkunft, und die Werkbank prüft vor dem nächsten Start, ob die
    /// gespeicherten Events zum neuen Code passen (Werkbank „Daten prüfen“).
    /// </summary>
    private static async Task SchreibeLaufstandAsync(NpgsqlConnection conn, string schema, CancellationToken ct)
    {
        string? proto = null;
        await using (var res = System.Reflection.Assembly.GetEntryAssembly()?.GetManifestResourceStream("cqrs/domain.proto"))
            if (res != null)
                using (var leser = new StreamReader(res))
                    proto = await leser.ReadToEndAsync(ct);
        if (proto == null && Spur.Stand == null) return;
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"""
            create table if not exists "{schema}".stand (schluessel text primary key, wert text not null);
            insert into "{schema}".stand (schluessel, wert) select k, w from (values (@pk, @p), (@ck, @c)) as v(k, w) where w is not null
              on conflict (schluessel) do update set wert = excluded.wert;
            """;
        void Text(string name, string? wert) =>
            cmd.Parameters.Add(new NpgsqlParameter(name, NpgsqlTypes.NpgsqlDbType.Text) { Value = (object?)wert ?? DBNull.Value });
        Text("pk", ProtoSchluessel);
        Text("p", proto);
        Text("ck", CodeSchluessel);
        Text("c", Spur.Stand);
        await cmd.ExecuteNonQueryAsync(ct);
    }

    /// <summary>Nur Sim: fehlt die Datenbank, wird sie angelegt (Prod-Datenbanken legt nie ein Host an).</summary>
    private async Task LegeDatenbankAnAsync(CancellationToken ct)
    {
        var verwaltung = new NpgsqlConnectionStringBuilder(umgebung.EventStore.Verbindung) { Database = "postgres" }.ConnectionString;
        await using var conn = new NpgsqlConnection(verwaltung);
        await conn.OpenAsync(ct);
        await using var pruefe = conn.CreateCommand();
        pruefe.CommandText = "select 1 from pg_database where datname = @d";
        pruefe.Parameters.AddWithValue("d", umgebung.EventStore.Datenbank);
        if (await pruefe.ExecuteScalarAsync(ct) != null) return;
        await using var lege = conn.CreateCommand();
        lege.CommandText = $"create database \"{umgebung.EventStore.Datenbank}\"";
        try { await lege.ExecuteNonQueryAsync(ct); }
        catch (PostgresException e) when (e.SqlState == "42P04") { /* gleichzeitig angelegt */ }
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}

/// <summary>
/// Die DB-Uhr einer Umgebung (docs/04 §4.7): die Zeit der Datenbank plus <c>versatz</c> aus <c>&lt;schema&gt;.uhr</c> — nur in
/// Sim; in Prod ohne Versatz. Derselbe Ausdruck setzt den Zeitstempel jedes Events (G-10, <c>Persistence.EventZeit</c>): Zeitlimits, Warte-Knoten
/// und Fristen vergleichen Zeit aus dem Log mit Zeit aus derselben Uhr, auch nach einem Zeitsprung der Sim.
/// </summary>
public sealed class UmgebungsDbClock(UmgebungKonfig umgebung) : IDbClock
{
    /// <summary>Die Uhr der Umgebung als SQL-Ausdruck (<c>timestamptz</c>) — die eine Quelle für <see cref="IDbClock"/> und den Event-Zeitstempel.</summary>
    public static string Ausdruck(UmgebungKonfig u) => u.Umgebung.Art == UmgebungsArt.Sim
        ? $"(clock_timestamp() + coalesce((select versatz from \"{u.EventStore.Schema}\".uhr where id = 1), interval '0'))"
        : Infrastructure.Persistence.EventZeit.Datenbankzeit;

    public async Task<DateTimeOffset> JetztAsync(CancellationToken ct = default)
    {
        await using var conn = new NpgsqlConnection(UmgebungLader.Verbindung(umgebung.EventStore));
        await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = $"select {Ausdruck(umgebung)}";
        return await cmd.ExecuteScalarAsync(ct) switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            _ => throw new InvalidOperationException("now() lieferte keinen Zeitwert"),
        };
    }
}
