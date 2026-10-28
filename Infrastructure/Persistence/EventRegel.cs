using Marten;
using Npgsql;

namespace Infrastructure.Persistence;

/// <summary>
/// Die Regel jeder Event-Zeile (<c>before insert</c> auf <c>mt_events</c>), zwei Dinge, die nur die Datenbank richtig setzen kann:
/// <list type="number">
/// <item><b>Sequenz nach der Transaktionsnummer</b> (G-17, docs/03 §3.3): der Horizont der Vollständigkeit (<see cref="Horizont"/>) setzt voraus,
///   dass jede Transaktion ihre Nummer hat, bevor sie eine Sequenz zieht. Martens Insert zieht <c>nextval</c> selbst — gemessen unter Last
///   mitunter Dutzende Millisekunden vor der Nummer der Transaktion; eine Probe dazwischen sah den Zählerstand schon, die Transaktion aber noch
///   nicht als laufend, gab die Sequenzen frei, und Leser rückten über Events, die erst danach sichtbar wurden. Die Regel holt deshalb zuerst die
///   Nummer (<c>pg_current_xact_id()</c>, vergibt sie, falls nötig) und zieht dann die Sequenz der Zeile neu; die von Marten gezogene bleibt eine
///   Lücke. So gilt die Voraussetzung für jede Event-Zeile, gleich welcher Weg sie schreibt.</item>
/// <item><b>Eine Uhr für alles Fachliche</b> (G-10): den Zeitstempel setzt die Datenbank mit DERSELBEN Uhr, die <see cref="Abstractions.IDbClock"/>
///   liest (in Sim samt Versatz), statt der Uhr des Hosts aus Martens Insert. Zeitlimits, Warte-Knoten und Fristen vergleichen so Zeit aus dem
///   Log mit Zeit aus derselben Quelle — ohne Uhrenversatz zwischen Knoten, und ein Zeitsprung der Sim verschiebt beide gleich.</item>
/// </list>
/// Weasel kennt keine Trigger, die Regel ist deshalb kein Schema-Objekt: angelegt wird sie vor dem ersten Schreiben je Store
/// (<see cref="SicherAsync"/>) bzw. vom Migrator; ein Knoten ohne Schema-Recht (<c>AutoCreate.None</c>) prüft nur, dass sie da ist. Weasels
/// Tabellen-Abgleich lässt Trigger stehen.
/// </summary>
public static class EventRegel
{
    /// <summary>Die Uhr ohne Umgebung (Tests, Werkzeuge): die Zeit der Datenbank.</summary>
    public const string Datenbankzeit = "clock_timestamp()";

    public const string Regel = "cqrs_event_regel";

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
    private static string Rumpf(string schema, string uhr) =>
        $"\nbegin\n    perform pg_current_xact_id();\n    new.seq_id := nextval('\"{schema}\".mt_events_sequence');\n    new.timestamp := {uhr};\n    return new;\nend\n";

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
                create or replace function "{schema}".{Regel}() returns trigger language plpgsql as $f${Rumpf(schema, uhr)}$f$;
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
        return !r.IsDBNull(0) && r.GetString(0) == Rumpf(schema, uhr) && r.GetBoolean(1);
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
