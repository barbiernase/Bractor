using Abstractions;
using Npgsql;

namespace SimHost.Werkbank;

/// <summary>
/// Die Sim-Datenbank (docs/konzept-werkbank.md §4.6), nur für Umgebungen der Art Sim: zurücksetzen (löschen — der Host legt sie beim
/// nächsten Start leer neu an und siegelt sie), Stand speichern (<c>CREATE DATABASE &lt;db&gt;_stand_&lt;x&gt; TEMPLATE &lt;db&gt;</c>), Stand laden,
/// Prod-Stand übernehmen (<c>pg_dump | pg_restore</c> per Container, Siegel neu). Der Host muss dabei stehen (eine Vorlage verträgt keine
/// Verbindungen) — das ordnet der Aufrufer an.
/// <para>
/// Der Stand der Daten liegt IN der Datenbank (Tabelle <c>&lt;schema&gt;.stand</c>, Schlüssel → Wert): <c>proto</c> = das <c>domain.proto</c>,
/// mit dem die Events geschrieben wurden, und <c>code</c> = Build-Stand (beide vom Host beim Start), <c>projektion:&lt;Id&gt;</c> = Code-Hash je
/// Projektion (Host). Damit reist er mit jedem
/// gespeicherten Stand mit und verschwindet mit einem Zurücksetzen.
/// </para>
/// </summary>
public sealed class SimDatenbank
{
    public const string ProtoSchluessel = "proto";

    private static void NurSim(UmgebungKonfig u)
    {
        if (u.Umgebung.Art != UmgebungsArt.Sim) throw new InvalidOperationException($"Umgebung {u.Umgebung.Name} ist {u.Umgebung.Art} — nur Sim-Datenbanken werden verwaltet.");
    }

    // Ohne Pool zu den Sim-Datenbanken: eine Vorlage (TEMPLATE) verträgt keine offene Verbindung — auch keine geparkte der Werkbank.
    private static string Verbindung(UmgebungKonfig u, string datenbank) => new NpgsqlConnectionStringBuilder(u.EventStore.Verbindung)
        { Database = datenbank, Pooling = datenbank == "postgres" }.ConnectionString;

    /// <summary>Beendet übrige Verbindungen zu <paramref name="datenbank"/> (der Host steht schon; z. B. ein psql oder ein Abfrage-Werkzeug).</summary>
    private static Task TrenneAsync(UmgebungKonfig u, string datenbank)
        => SqlAsync(u, $"select pg_terminate_backend(pid) from pg_stat_activity where datname = '{datenbank.Replace("'", "''")}' and pid <> pg_backend_pid()");

    private static async Task SqlAsync(UmgebungKonfig u, string sql)
    {
        await using var c = new NpgsqlConnection(Verbindung(u, "postgres"));
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = sql;
        await cmd.ExecuteNonQueryAsync();
    }

    public static string StandName(UmgebungKonfig u, string stand) => $"{u.EventStore.Datenbank}_stand_{Umgebungen.BranchSicher(stand)}";

    public async Task<bool> GibtEsAsync(UmgebungKonfig u, string datenbank)
    {
        await using var c = new NpgsqlConnection(Verbindung(u, "postgres"));
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = "select 1 from pg_database where datname = @d";
        cmd.Parameters.AddWithValue("d", datenbank);
        return await cmd.ExecuteScalarAsync() != null;
    }

    public async Task ZuruecksetzenAsync(UmgebungKonfig u)
    {
        NurSim(u);
        await SqlAsync(u, $"drop database if exists \"{u.EventStore.Datenbank}\" with (force)");
    }

    public async Task SpeichereAsync(UmgebungKonfig u, string stand)
    {
        NurSim(u);
        if (!await GibtEsAsync(u, u.EventStore.Datenbank)) throw new InvalidOperationException($"Die Sim-DB {u.EventStore.Datenbank} gibt es noch nicht (⟳ legt sie an).");
        var ziel = StandName(u, stand);
        await SqlAsync(u, $"drop database if exists \"{ziel}\" with (force)");
        await TrenneAsync(u, u.EventStore.Datenbank);
        await SqlAsync(u, $"create database \"{ziel}\" template \"{u.EventStore.Datenbank}\"");
    }

    public async Task LadeAsync(UmgebungKonfig u, string stand)
    {
        NurSim(u);
        var quelle = StandName(u, stand);
        // Erst prüfen, dann löschen — ein Tippfehler im Namen darf die laufende Sim-DB nicht kosten.
        if (!await GibtEsAsync(u, quelle)) throw new InvalidOperationException($"Stand '{stand}' gibt es nicht.");
        await SqlAsync(u, $"drop database if exists \"{u.EventStore.Datenbank}\" with (force)");
        await TrenneAsync(u, quelle);
        await SqlAsync(u, $"create database \"{u.EventStore.Datenbank}\" template \"{quelle}\"");
    }

    public async Task LoescheStandAsync(UmgebungKonfig u, string stand)
    {
        NurSim(u);
        await SqlAsync(u, $"drop database if exists \"{StandName(u, stand)}\" with (force)");
    }

    /// <summary>
    /// Prod-Stand übernehmen: die Sim-DB wird aus einem <c>pg_dump</c> der Prod-DB neu angelegt (Werkzeug im Container <c>postgres:16-alpine</c>, dieselbe Version wie die Deployments — lokal muss
    /// kein Postgres-Client liegen), Siegel, Uhr und Stand gelöscht (der Host siegelt beim Start auf Sim, Uhr 0, Leseseite neu). Die Prod-DB
    /// wird nur gelesen.
    /// </summary>
    public async Task<IReadOnlyList<string>> UebernehmeProdAsync(UmgebungKonfig sim, UmgebungKonfig prod, string slnRoot, CancellationToken ct)
    {
        NurSim(sim);
        if (prod.Umgebung.Art != UmgebungsArt.Prod) throw new InvalidOperationException($"Umgebung {prod.Umgebung.Name} ist keine Prod-Umgebung.");
        await SqlAsync(sim, $"drop database if exists \"{sim.EventStore.Datenbank}\" with (force)");
        await SqlAsync(sim, $"create database \"{sim.EventStore.Datenbank}\"");
        static string Url(UmgebungKonfig u)
        {
            var b = new NpgsqlConnectionStringBuilder(u.EventStore.Verbindung) { Database = u.EventStore.Datenbank };
            // Aus dem Container heraus ist „localhost" der Container selbst.
            var host = b.Host is null or "localhost" or "127.0.0.1" ? "host.docker.internal" : b.Host;
            return $"postgresql://{Uri.EscapeDataString(b.Username ?? "postgres")}:{Uri.EscapeDataString(b.Password ?? "")}@{host}:{b.Port}/{b.Database}";
        }
        var r = await Prozesse.LaufeAsync("docker",
            ["run", "--rm", "--add-host=host.docker.internal:host-gateway", "postgres:16-alpine", "sh", "-c",
             $"pg_dump -Fc --no-owner '{Url(prod)}' | pg_restore --no-owner --no-acl -d '{Url(sim)}'"],
            slnRoot, zeitlimit: TimeSpan.FromMinutes(30), ct: ct);
        if (!r.Ok) throw new InvalidOperationException("pg_dump/pg_restore: " + string.Join(" · ", r.Zeilen.TakeLast(5)));
        var schema = sim.EventStore.Schema;
        await using var c = new NpgsqlConnection(Verbindung(sim, sim.EventStore.Datenbank));
        await c.OpenAsync(ct);
        await using var cmd = c.CreateCommand();
        // Siegel und Uhr der Herkunft weg (auch wenn es sie dort nie gab): der Host siegelt beim Start auf Sim, die Uhr steht auf 0.
        // Vom Stand bleibt nur das Proto der Herkunft (der Prod-Host schreibt es beim Start) — „Daten prüfen“ vergleicht es vor dem
        // nächsten Start mit dem Code. Ohne Projektions-Hashes baut der Host die Leseseite aus dem Log neu auf (die Read-Models der Prod-DB
        // stammen womöglich von anderem Code).
        cmd.CommandText = $"""
            drop table if exists "{schema}".umgebung;
            drop table if exists "{schema}".uhr;
            do $$ begin
              if to_regclass('"{schema}".stand') is not null then
                delete from "{schema}".stand where schluessel <> '{ProtoSchluessel}';
              end if;
            end $$;
            """;
        await cmd.ExecuteNonQueryAsync(ct);
        return [$"Prod-Stand aus {prod.EventStore.Datenbank} übernommen ({r.Dauer.TotalSeconds:0.0} s) — der Host siegelt beim Start auf Sim"];
    }

    /// <summary>Ein gespeicherter Stand und ob er zum aktuellen Code passt (Proto-Vergleich).</summary>
    public sealed record Stand(string Name, bool Gueltig, IReadOnlyList<string> Gruende);

    public async Task<IReadOnlyList<Stand>> StaendeAsync(UmgebungKonfig u, string? aktuellesProto)
    {
        var praefix = u.EventStore.Datenbank + "_stand_";
        var namen = new List<string>();
        await using (var c = new NpgsqlConnection(Verbindung(u, "postgres")))
        {
            await c.OpenAsync();
            await using var cmd = c.CreateCommand();
            cmd.CommandText = "select datname from pg_database where datname like @p order by datname";
            cmd.Parameters.AddWithValue("p", praefix.Replace("_", "\\_") + "%");
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) namen.Add(r.GetString(0));
        }
        var aus = new List<Stand>();
        foreach (var db in namen)
        {
            var proto = await LiesAsync(u, db, ProtoSchluessel);
            var gruende = proto == null || aktuellesProto == null ? [] : ProtoVergleich.Unvertraeglich(proto, aktuellesProto);
            aus.Add(new Stand(db[praefix.Length..], gruende.Count == 0, gruende));
        }
        return aus;
    }

    /// <summary>Ein Wert aus <c>&lt;schema&gt;.stand</c> (null: DB, Tabelle oder Schlüssel fehlt).</summary>
    public async Task<string?> LiesAsync(UmgebungKonfig u, string datenbank, string schluessel)
    {
        if (!await GibtEsAsync(u, datenbank)) return null;
        await using var c = new NpgsqlConnection(Verbindung(u, datenbank));
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = $"select wert from \"{u.EventStore.Schema}\".stand where schluessel = @k";
        cmd.Parameters.AddWithValue("k", schluessel);
        try { return await cmd.ExecuteScalarAsync() as string; }
        catch (PostgresException e) when (e.SqlState is "42P01" or "3F000") { return null; }   // Tabelle/Schema fehlt
    }

    /// <summary>Der Versatz der DB-Uhr (§4.4; nur Sim). null: die DB ist noch nicht angelegt.</summary>
    public async Task<TimeSpan?> VersatzAsync(UmgebungKonfig u)
    {
        if (!await GibtEsAsync(u, u.EventStore.Datenbank)) return null;
        await using var c = new NpgsqlConnection(Verbindung(u, u.EventStore.Datenbank));
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = $"select versatz from \"{u.EventStore.Schema}\".uhr where id = 1";
        try { return await cmd.ExecuteScalarAsync() is TimeSpan t ? t : null; }
        catch (PostgresException e) when (e.SqlState is "42P01" or "3F000") { return null; }
    }

    /// <summary>Verstellt die Uhr der Sim-DB — der Host liest sie bei jedem <c>IDbClock.JetztAsync</c>, ein Neustart ist nicht nötig.</summary>
    public async Task SetzeVersatzAsync(UmgebungKonfig u, TimeSpan versatz)
    {
        NurSim(u);
        await using var c = new NpgsqlConnection(Verbindung(u, u.EventStore.Datenbank));
        await c.OpenAsync();
        await using var cmd = c.CreateCommand();
        cmd.CommandText = $"update \"{u.EventStore.Schema}\".uhr set versatz = @v where id = 1";
        cmd.Parameters.AddWithValue("v", versatz);
        if (await cmd.ExecuteNonQueryAsync() == 0) throw new InvalidOperationException("Die Sim-DB ist noch nicht gesiegelt (⟳ startet den Host).");
    }
}
