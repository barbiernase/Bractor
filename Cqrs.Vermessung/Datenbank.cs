using System.Collections.Concurrent;
using System.Diagnostics;
using Npgsql;

namespace Cqrs.Vermessung;

/// <summary>
/// Die Datenbank-Seite der Messung — nie ein Schreiben in den Log der Anwendung: Zurücksetzen der Vermessungs-DB (E3), Lese-Rolle, Messwerte
/// aus <c>pg_stat_statements</c>/<c>pg_stat_database</c>, Uhr-Versatz, Lesen des Logs für Beobachtung und Exactly-once-Prüfung.
/// </summary>
internal sealed class Datenbank
{
    /// <summary>Die Rolle, mit der der Treiber den Log liest — ihre Statements zählen nicht als Last des Hosts.</summary>
    public const string LeseRolle = "vermessung_leser";
    private const string LesePasswort = "vermessung";

    private readonly string _basis;   // Host/Port/Benutzer/Passwort ohne Datenbank (aus der Umgebung)
    public string Name { get; }
    public string Schema { get; }

    public Datenbank(string basisVerbindung, string name, string schema)
    {
        _basis = basisVerbindung;
        Name = name;
        Schema = schema;
    }

    public string Admin(string db) => $"{_basis};Database={db};Pooling=true;Maximum Pool Size=4";
    public string Leser => new NpgsqlConnectionStringBuilder(_basis) { Database = Name, Username = LeseRolle, Password = LesePasswort, MaxPoolSize = 4 }.ConnectionString;

    public async Task<T> SkalarAsync<T>(string db, string sql, params (string, object)[] p)
    {
        await using var c = new NpgsqlConnection(Admin(db));
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        foreach (var (n, w) in p) cmd.Parameters.AddWithValue(n, w);
        return (T)(await cmd.ExecuteScalarAsync())!;
    }

    public async Task AusfuehrenAsync(string db, string sql)
    {
        await using var c = new NpgsqlConnection(Admin(db));
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>Einmal je Postgres-Instanz: pg_stat_statements und die Lese-Rolle.</summary>
    public async Task VorbereitenAsync()
    {
        var geladen = await SkalarAsync<string>("postgres", "show shared_preload_libraries");
        if (!geladen.Contains("pg_stat_statements"))
            throw new InvalidOperationException("pg_stat_statements ist nicht geladen — scripts/vermessung-e2e.sh richtet es ein (docker-compose.infrastructure.yml).");
        await AusfuehrenAsync("postgres", "create extension if not exists pg_stat_statements");
        await AusfuehrenAsync("postgres", $"""
            do $$ begin
              if not exists (select from pg_roles where rolname = '{LeseRolle}') then create role {LeseRolle} login password '{LesePasswort}'; end if;
            end $$;
            """);
    }

    /// <summary>
    /// Setzt die Vermessungs-DB zurück (E3: Sim- und Vermessungs-DBs dürfen zurückgesetzt werden): löschen, leer anlegen, Schema anlegen und
    /// der Lese-Rolle Lesen erlauben — das Siegel des Hosts versiegelt die leere DB beim ersten Start.
    /// </summary>
    public async Task ZuruecksetzenAsync()
    {
        await AusfuehrenAsync("postgres", $"drop database if exists {Name} with (force)");
        await AusfuehrenAsync("postgres", $"create database {Name}");
        await AusfuehrenAsync(Name, $"""
            create schema if not exists {Schema};
            grant connect on database {Name} to {LeseRolle};
            grant usage on schema {Schema} to {LeseRolle};
            alter default privileges in schema {Schema} grant select on tables to {LeseRolle};
            """);
    }

    /// <summary>Versatz lokale Uhr − DB-Uhr (Median aus fünf Messungen, Rundlauf halbiert) — macht Log-Zeitstempel mit Sendezeiten vergleichbar.</summary>
    public async Task<TimeSpan> UhrversatzAsync()
    {
        await using var c = new NpgsqlConnection(Admin(Name));
        await c.OpenAsync();
        var proben = new List<double>();
        for (var i = 0; i < 5; i++)
        {
            var vor = DateTimeOffset.UtcNow;
            await using var cmd = new NpgsqlCommand("select clock_timestamp()", c);
            var db = (DateTime)(await cmd.ExecuteScalarAsync())!;
            var nach = DateTimeOffset.UtcNow;
            var mitte = vor + (nach - vor) / 2;
            proben.Add((mitte - new DateTimeOffset(DateTime.SpecifyKind(db, DateTimeKind.Utc))).TotalMilliseconds);
        }
        return TimeSpan.FromMilliseconds(Statistik.Median(proben));
    }

    // ── Messwerte des Hosts ────────────────────────────────────────────────────────────────────────────────────────────────

    public sealed record Statement(long QueryId, string Text, long Aufrufe, double ZeitMs, long Zeilen);
    public sealed record PgStand(DateTimeOffset Zeit, long Commits, long Rollbacks, IReadOnlyDictionary<long, Statement> Statements);

    /// <summary>Ein Stand von pg_stat_database und pg_stat_statements dieser DB — ohne die Statements der Lese-Rolle.</summary>
    public async Task<PgStand> PgStandAsync()
    {
        await using var c = new NpgsqlConnection(Admin("postgres"));
        await c.OpenAsync();
        long commits, rollbacks;
        await using (var cmd = new NpgsqlCommand("select xact_commit, xact_rollback from pg_stat_database where datname = @db", c))
        {
            cmd.Parameters.AddWithValue("db", Name);
            await using var r = await cmd.ExecuteReaderAsync();
            await r.ReadAsync();
            (commits, rollbacks) = (r.GetInt64(0), r.GetInt64(1));
        }
        var st = new Dictionary<long, Statement>();
        await using (var cmd = new NpgsqlCommand("""
            select s.queryid, left(s.query, 300), s.calls, s.total_exec_time, s.rows
            from pg_stat_statements s join pg_database d on d.oid = s.dbid
            where d.datname = @db and s.userid <> (select oid from pg_roles where rolname = @rolle) and s.queryid is not null
            """, c))
        {
            cmd.Parameters.AddWithValue("db", Name);
            cmd.Parameters.AddWithValue("rolle", LeseRolle);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var id = r.GetInt64(0);
                var s = new Statement(id, r.GetString(1), r.GetInt64(2), r.GetDouble(3), r.GetInt64(4));
                st[id] = st.TryGetValue(id, out var alt) ? alt with { Aufrufe = alt.Aufrufe + s.Aufrufe, ZeitMs = alt.ZeitMs + s.ZeitMs, Zeilen = alt.Zeilen + s.Zeilen } : s;
            }
        }
        return new PgStand(DateTimeOffset.UtcNow, commits, rollbacks, st);
    }

    /// <summary>
    /// Die Differenz zweier Stände. Transaktionen = die COMMITs aus pg_stat_statements (dort ohne Verzug gezählt; <c>pg_stat_database</c>
    /// schreibt seine Zähler verzögert fort und taugt für kurze Fenster nicht — er steht nur zur Information als <c>xact</c> daneben).
    /// Queries = alle übrigen Statements ohne Transaktions-Steuerung und Verbindungs-Reset.
    /// </summary>
    public static (double TxJeS, double QueriesJeS, long Tx, long Queries, double XactJeS, List<Statement> Statements) Differenz(PgStand a, PgStand b, long leserStatements)
    {
        var dauer = Math.Max(0.001, (b.Zeit - a.Zeit).TotalSeconds);
        var xact = Math.Max(0, b.Commits - a.Commits - leserStatements);
        var liste = new List<Statement>();
        foreach (var (id, s) in b.Statements)
        {
            var vorher = a.Statements.TryGetValue(id, out var v) ? v : null;
            var d = s with { Aufrufe = s.Aufrufe - (vorher?.Aufrufe ?? 0), ZeitMs = s.ZeitMs - (vorher?.ZeitMs ?? 0), Zeilen = s.Zeilen - (vorher?.Zeilen ?? 0) };
            if (d.Aufrufe > 0) liste.Add(d);
        }
        var tx = liste.Where(s => s.Text.TrimStart().StartsWith("COMMIT", StringComparison.OrdinalIgnoreCase)).Sum(s => s.Aufrufe);
        var queries = liste.Where(s => !IstTransaktionsSteuerung(s.Text)).Sum(s => s.Aufrufe);
        return (tx / dauer, queries / dauer, tx, queries, xact / dauer, liste.OrderByDescending(s => s.Aufrufe).ToList());
    }

    private static bool IstTransaktionsSteuerung(string text)
    {
        var t = text.TrimStart().ToUpperInvariant();
        return t.StartsWith("BEGIN") || t.StartsWith("COMMIT") || t.StartsWith("ROLLBACK") || t.StartsWith("DISCARD") || t.StartsWith("SET ") || t.StartsWith("SHOW ");
    }

    // ── Lesen des Logs (nur Lese-Rolle) ────────────────────────────────────────────────────────────────────────────────────

    public async Task<List<T>> LiesAsync<T>(string sql, Func<NpgsqlDataReader, T> zeile, params (string, object)[] p)
    {
        Interlocked.Increment(ref _leserStatements);
        await using var c = new NpgsqlConnection(Leser);
        await c.OpenAsync();
        await using var cmd = new NpgsqlCommand(sql, c) { CommandTimeout = 600 };
        foreach (var (n, w) in p) cmd.Parameters.AddWithValue(n, w);
        await using var r = await cmd.ExecuteReaderAsync();
        var liste = new List<T>();
        while (await r.ReadAsync()) liste.Add(zeile(r));
        return liste;
    }

    public async Task<long> ZahlAsync(string sql, params (string, object)[] p)
        => (await LiesAsync(sql, r => r.IsDBNull(0) ? 0L : Convert.ToInt64(r.GetValue(0)), p)).FirstOrDefault();

    private long _leserStatements;
    /// <summary>Wie viele Statements (= Transaktionen) der Treiber als Lese-Rolle bisher ausgeführt hat.</summary>
    public long LeserStatements => Interlocked.Read(ref _leserStatements);
}

/// <summary>Eine Zeile des Logs, so wie der Treiber sie sieht.</summary>
internal sealed record LogZeile(long Seq, Guid Stream, int Version, string Typ, string? Daten, string? Korrelation, string? Kausal,
    DateTimeOffset Zeit, string? Ueber, string? Ursache);

/// <summary>
/// Liest den Log fortlaufend (alle 200 ms) und reicht jede neue Zeile an die Beobachter. Gegen nebenläufig spät sichtbare Commits liest er
/// ein Fenster von 5 s (DB-Zeit) erneut und merkt sich, was er schon gesehen hat — kein Eintrag geht verloren.
/// </summary>
internal sealed class LogStrom : IAsyncDisposable
{
    private readonly Datenbank _db;
    private readonly string[] _mitDaten;
    private readonly ConcurrentBag<Action<LogZeile>> _beobachter = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly HashSet<long> _gesehen = new();
    private long _sicher;
    private Task? _schleife;

    /// <param name="mitDaten">Event-Typen, deren Daten (JSON) der Beobachter braucht; alle anderen ohne Daten (spart Last).</param>
    public LogStrom(Datenbank db, params string[] mitDaten)
    {
        _db = db;
        _mitDaten = mitDaten;
    }

    public void Beobachte(Action<LogZeile> b) => _beobachter.Add(b);

    public void Starte() => _schleife = Task.Run(SchleifeAsync);

    /// <summary>Ein Durchlauf sofort (z. B. vor einer Prüfung).</summary>
    public Task NachziehenAsync() => DurchlaufAsync();

    private async Task SchleifeAsync()
    {
        while (!_cts.IsCancellationRequested)
        {
            try { await DurchlaufAsync(); }
            catch (Exception e) when (!_cts.IsCancellationRequested) { Console.WriteLine($"[Log] {e.Message}"); }
            try { await Task.Delay(200, _cts.Token); } catch (OperationCanceledException) { }
        }
    }

    private readonly SemaphoreSlim _einer = new(1, 1);

    private async Task DurchlaufAsync()
    {
        await _einer.WaitAsync();
        try
        {
            if (!await TabelleDaAsync()) return;
            var zeilen = await _db.LiesAsync($"""
                select seq_id, stream_id, version, type, case when type = any(@mit) then data::text end, correlation_id, causation_id,
                       timestamp, headers->>'ueber', headers->>'ursache', clock_timestamp()
                from {_db.Schema}.mt_events where seq_id > @ab order by seq_id limit 50000
                """, r => (Zeile: new LogZeile(r.GetInt64(0), r.GetGuid(1), r.GetInt32(2), r.GetString(3), r.IsDBNull(4) ? null : r.GetString(4),
                    r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6), r.GetFieldValue<DateTimeOffset>(7),
                    r.IsDBNull(8) ? null : r.GetString(8), r.IsDBNull(9) ? null : r.GetString(9)), Jetzt: r.GetFieldValue<DateTimeOffset>(10)),
                ("mit", _mitDaten), ("ab", _sicher));
            if (zeilen.Count == 0) return;
            var grenze = zeilen[0].Jetzt - TimeSpan.FromSeconds(5);
            foreach (var (z, _) in zeilen)
            {
                if (!_gesehen.Add(z.Seq)) continue;
                foreach (var b in _beobachter) b(z);
            }
            // Sicher ist, was älter als das Fenster ist und lückenlos davor liegt.
            var neuSicher = _sicher;
            foreach (var (z, _) in zeilen) { if (z.Zeit < grenze) neuSicher = z.Seq; else break; }
            if (neuSicher > _sicher)
            {
                _sicher = neuSicher;
                _gesehen.RemoveWhere(s => s <= _sicher);
            }
        }
        finally { _einer.Release(); }
    }

    private bool _tabelle;
    private async Task<bool> TabelleDaAsync()
    {
        if (_tabelle) return true;
        _tabelle = await _db.ZahlAsync("select count(*) from information_schema.tables where table_schema = @s and table_name = 'mt_events'", ("s", _db.Schema)) > 0;
        return _tabelle;
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_schleife != null) { try { await _schleife; } catch { } }
    }
}
