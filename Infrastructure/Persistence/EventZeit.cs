using Marten;
using Npgsql;

namespace Infrastructure.Persistence;

/// <summary>
/// Eine Uhr für alles Fachliche (G-10, docs/03 §3.3): den Zeitstempel eines Events setzt die Datenbank beim Einfügen, mit DERSELBEN Uhr, die
/// <see cref="Abstractions.IDbClock"/> liest (in Sim samt Versatz). Marten schickt im Insert die Uhr des Hosts mit; eine Regel
/// <c>before insert</c> auf <c>mt_events</c> ersetzt sie. Zeitlimits, Warte-Knoten und Fristen vergleichen so Zeit aus dem Log mit Zeit aus
/// derselben Quelle — ohne Uhrenversatz zwischen Knoten, und ein Zeitsprung der Sim verschiebt beide gleich.
///
/// Weasel kennt keine Trigger, die Regel ist deshalb kein Schema-Objekt: angelegt wird sie vor dem ersten Schreiben je Store
/// (<see cref="SicherAsync"/>) bzw. vom Migrator; ein Knoten ohne Schema-Recht (<c>AutoCreate.None</c>) prüft nur, dass sie da ist. Weasels
/// Tabellen-Abgleich lässt Trigger stehen.
/// </summary>
public static class EventZeit
{
    /// <summary>Die Uhr ohne Umgebung (Tests, Werkzeuge): die Zeit der Datenbank.</summary>
    public const string Datenbankzeit = "clock_timestamp()";

    public const string Regel = "cqrs_event_zeit";

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<object, string> Uhren = new();
    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IDocumentStore, object> Da = new();
    private static readonly SemaphoreSlim Anlegen = new(1, 1);

    /// <summary>Legt die Uhr des Stores fest: ein SQL-Ausdruck vom Typ <c>timestamptz</c>, derselbe, den die <c>IDbClock</c> liest.</summary>
    public static void Registriere(StoreOptions options, string uhr) => Uhren.AddOrUpdate(options, uhr);

    /// <summary>Der Uhr-Ausdruck des Stores (ohne Registrierung: <see cref="Datenbankzeit"/>).</summary>
    public static string Uhr(IDocumentStore store) => Uhren.TryGetValue(store.Options, out var u) ? u : Datenbankzeit;   // Options ist die registrierte StoreOptions-Instanz

    /// <summary>Stellt die Regel sicher (einmal je Store und Prozess): Event-Tabellen anlegen, dann Funktion und Trigger, lock-gesichert.</summary>
    public static async ValueTask SicherAsync(IDocumentStore store, CancellationToken ct)
    {
        if (Da.TryGetValue(store, out _)) return;
        await Anlegen.WaitAsync(ct);
        try
        {
            if (Da.TryGetValue(store, out _)) return;
            var schema = store.Options.Events.DatabaseSchemaName;
            if (store.Options is StoreOptions { AutoCreateSchemaObjects: JasperFx.AutoCreate.None })
            {
                if (!await VorhandenAsync(store, schema, ct))
                    throw new InvalidOperationException(
                        $"Die Regel {schema}.{Regel} (Event-Zeitstempel von der Datenbank-Uhr, G-10) fehlt; der Schema-Migrator legt sie an.");
            }
            else
            {
                await store.Storage.Database.EnsureStorageExistsAsync(typeof(JasperFx.Events.IEvent), ct);
                await AnlegenAsync(store, schema, Uhr(store), ct);
            }
            Da.AddOrUpdate(store, new object());
        }
        finally { Anlegen.Release(); }
    }

    /// <summary>Für den Migrator: die Regel jetzt anlegen bzw. erneuern (die Event-Tabellen stehen schon).</summary>
    public static Task AnlegenAsync(IDocumentStore store, CancellationToken ct)
        => AnlegenAsync(store, store.Options.Events.DatabaseSchemaName, Uhr(store), ct);

    /// <summary>Der Rumpf der Regel — so, wie Postgres ihn speichert (<c>pg_proc.prosrc</c>), damit ein unveränderter Stand nichts anfasst.</summary>
    private static string Rumpf(string uhr) => $"\nbegin\n    new.timestamp := {uhr};\n    return new;\nend\n";

    /// <summary>
    /// Legt Funktion und Trigger an — nur, wenn sie fehlen oder anders lauten: ein <c>create or replace trigger</c> braucht eine Sperre auf
    /// <c>mt_events</c>, die mit laufenden Inserts kollidiert und alle folgenden hinter sich warten lässt; beim Neustart eines Hosts schreibt der
    /// alte oft noch. Wenn doch, dann mit Sperrfrist — ein Start hängt nie unbegrenzt, er scheitert laut.
    /// </summary>
    private static async Task AnlegenAsync(IDocumentStore store, string schema, string uhr, CancellationToken ct)
    {
        await using var conn = store.Storage.Database.CreateConnection();
        await conn.OpenAsync(ct);
        if (await AktuellAsync(conn, schema, uhr, ct)) return;
        await using var tx = await conn.BeginTransactionAsync(ct);
        await using (var cmd = new NpgsqlCommand
        {
            Connection = conn,
            Transaction = tx,
            // Mehrere Knoten starten gleichzeitig: die Sperre reiht ihr Anlegen hintereinander.
            CommandText = $"""
                set local lock_timeout = '10s';
                select pg_advisory_xact_lock(hashtext('{schema}.{Regel}'));
                create or replace function "{schema}".{Regel}() returns trigger language plpgsql as $f${Rumpf(uhr)}$f$;
                create or replace trigger {Regel} before insert on "{schema}".mt_events
                    for each row execute function "{schema}".{Regel}();
                """,
        })
            await cmd.ExecuteNonQueryAsync(ct);
        await tx.CommitAsync(ct);
    }

    /// <summary>Ob Trigger und Funktion schon so stehen, wie sie stehen sollen (eine Leseabfrage, keine Sperre auf <c>mt_events</c>).</summary>
    private static async Task<bool> AktuellAsync(NpgsqlConnection conn, string schema, string uhr, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(
            "select (select p.prosrc from pg_proc p join pg_namespace n on n.oid = p.pronamespace where n.nspname = $2 and p.proname = $1), "
            + "exists(select 1 from pg_trigger t join pg_class c on c.oid = t.tgrelid join pg_namespace n on n.oid = c.relnamespace "
            + "where t.tgname = $1 and c.relname = 'mt_events' and n.nspname = $2)", conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = Regel });
        cmd.Parameters.Add(new NpgsqlParameter { Value = schema });
        await using var r = await cmd.ExecuteReaderAsync(ct);
        await r.ReadAsync(ct);
        return !r.IsDBNull(0) && r.GetString(0) == Rumpf(uhr) && r.GetBoolean(1);
    }

    private static async Task<bool> VorhandenAsync(IDocumentStore store, string schema, CancellationToken ct)
    {
        await using var conn = store.Storage.Database.CreateConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new NpgsqlCommand(
            "select exists(select 1 from pg_trigger t join pg_class c on c.oid = t.tgrelid join pg_namespace n on n.oid = c.relnamespace "
            + "where t.tgname = $1 and c.relname = 'mt_events' and n.nspname = $2)", conn);
        cmd.Parameters.Add(new NpgsqlParameter { Value = Regel });
        cmd.Parameters.Add(new NpgsqlParameter { Value = schema });
        return (bool)(await cmd.ExecuteScalarAsync(ct))!;
    }
}
