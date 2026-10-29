using Abstractions;
using Marten;
using Marten.Exceptions;
using Marten.Storage;
using Npgsql;
using Polly;
using Weasel.Postgresql;
using Weasel.Postgresql.Functions;
using Weasel.Postgresql.Tables;

namespace Infrastructure.Persistence;

/// <summary>
/// Die Inbox der Schreibseite (docs/04 §4.6): eine Tabelle <c>cqrs_inbox</c> im Event-Schema, je Command eine Quittung
/// (<c>command_id</c> eindeutig, <c>stream_id</c>, <c>abgelehnt</c>, <c>zeit</c> = <c>now()</c> der schreibenden Transaktion). Geschrieben
/// in derselben Transaktion wie die Events des Commands; ein zweites Mal scheitert am Primärschlüssel und rollt die Events mit zurück.
///
/// Ein von Marten verwaltetes Schema-Objekt (<c>Storage.ExtendedSchemaObjects</c>): Marten legt Tabelle und Index an (Migrator eager, sonst beim
/// ersten Schreiben über <c>QueueSqlCommand</c> bzw. über <see cref="SicherAsync"/>) und Weasel pflegt sie — eigene Indizes auf Martens Tabellen
/// würde Weasel beim Abgleich löschen. Der Zeit-Index ist ein B-Baum: die Zeit wächst nur, neue Einträge kommen an seinen rechten Rand (billig
/// wie der Index auf <c>seq_id</c>), und der <see cref="InboxAufraeumer"/> findet die ältesten, ohne die Tabelle zu durchsuchen (PF-27).
///
/// Eine Quittung ohne Events (ein Noop, eine Ablehnung — keine Marken im Log) prüft in derselben
/// Transaktion den Kopf ihres Streams (<see cref="Kopf"/>): die Entscheidung fiel auf einem Stand, und nur wenn der Stream noch dort steht, gilt
/// sie. Sonst hätte eine zweite Aktivierung auf einem veralteten Stand abgelehnt oder nichts getan, und das wäre dauerhaft (docs/11 §11.2, „nur
/// einer gewinnt“). Die Prüfung sperrt die Kopfzeile in <c>mt_streams</c> bis zum Commit, wie ein Append mit erwarteter Version.
/// </summary>
public static class InboxTabelle
{
    public const string Tabelle = "cqrs_inbox";

    /// <summary>Die Funktion der Kopf-Prüfung (Schema-Objekt neben der Tabelle).</summary>
    public const string Kopf = "cqrs_kopf";

    /// <summary>Der SQLSTATE, mit dem die Kopf-Prüfung scheitert: der Stream steht nicht mehr auf der erwarteten Version.</summary>
    public const string KopfKonflikt = "CQ409";

    /// <summary>
    /// Registriert die Tabelle im Event-Schema (<c>options.Events.DatabaseSchemaName</c> muss gesetzt sein) und nimmt Eindeutigkeits-Verletzungen
    /// aus Martens Wiederholungen: Martens Standard wiederholt jede <c>MartenCommandException</c> dreimal — ein Duplikat der Inbox ist
    /// deterministisch und liefe sonst viermal mit Rollback des ganzen Group-Commits. Sonst gelten Martens Standardwerte.
    /// </summary>
    public static void Registriere(StoreOptions options)
    {
        var t = new Table(new PostgresqlObjectName(options.Events.DatabaseSchemaName, Tabelle));
        t.AddColumn<Guid>("command_id").AsPrimaryKey();
        t.AddColumn<Guid>("stream_id").NotNull();
        t.AddColumn<bool>("abgelehnt").NotNull();
        t.AddColumn("zeit", "timestamptz").NotNull().DefaultValueByExpression("now()");
        t.Indexes.Add(new IndexDefinition($"ix_{Tabelle}_zeit") { Columns = ["zeit"] });
        options.Storage.ExtendedSchemaObjects.Add(t);
        var schema = options.Events.DatabaseSchemaName;
        options.Storage.ExtendedSchemaObjects.Add(new Function(new PostgresqlObjectName(schema, Kopf), $$"""
            create or replace function {{schema}}.{{Kopf}}(stream uuid, erwartet bigint) returns void language plpgsql as $f$
            declare
                v bigint;
            begin
                select version into v from {{schema}}.mt_streams where id = stream for update;
                if coalesce(v, 0) <> erwartet then
                    raise exception using errcode = '{{KopfKonflikt}}',
                        message = format('Stream %s steht auf Version %s, die Entscheidung fiel auf %s', stream, coalesce(v, 0), erwartet);
                end if;
            end
            $f$;
            """));

        // Duplikat und Kopf-Konflikt sind deterministisch: eine Wiederholung scheitert gleich (und rollte den ganzen Group-Commit erneut zurück).
        options.ConfigurePolly(b => b.AddRetry(new()
        {
            ShouldHandle = new PredicateBuilder()
                .Handle<NpgsqlException>(e => !IstEindeutigkeitsverletzung(e) && !IstKopfKonflikt(e))
                .Handle<MartenCommandException>(e => !IstEindeutigkeitsverletzung(e) && !IstKopfKonflikt(e))
                .Handle<Marten.Events.Daemon.Internals.EventLoaderException>(),
            MaxRetryAttempts = 3,
            Delay = TimeSpan.FromMilliseconds(50),
            BackoffType = DelayBackoffType.Exponential,
        }));
    }

    public static string Name(IDocumentStore store) => $"{store.Options.Events.DatabaseSchemaName}.{Tabelle}";

    /// <summary>Legt die Tabelle an, falls nötig (rohes SQL löst Martens AutoCreate nicht aus); einmal je Store gemerkt.</summary>
    public static async ValueTask SicherAsync(IDocumentStore store, CancellationToken ct)
    {
        if (Da.TryGetValue(store, out _)) return;
        await store.Storage.Database.EnsureStorageExistsAsync(typeof(StorageFeatures), ct);
        Da.TryAdd(store, new object());
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IDocumentStore, object> Da = new();

    /// <summary>Alle Quittungen eines Appends bzw. Group-Commits in EINE Anweisung (sie läuft nach den Events in derselben Transaktion).</summary>
    public static void Quittiere(IDocumentSession session, string name, List<Guid> commands, List<Guid> streams, List<bool> abgelehnt)
        => session.QueueSqlCommand(
            $"insert into {name} (command_id, stream_id, abgelehnt) select * from unnest(?::uuid[], ?::uuid[], ?::boolean[])",
            commands.ToArray(), streams.ToArray(), abgelehnt.ToArray());

    /// <summary>
    /// Die Kopf-Prüfung für Quittungen ohne Events in EINER Anweisung derselben Transaktion: je Stream die Version, auf der entschieden wurde.
    /// Steht ein Stream woanders, scheitert der Commit mit <see cref="KopfKonflikt"/> und nichts ist geschrieben.
    /// </summary>
    public static void PruefeKoepfe(IDocumentSession session, IDocumentStore store, List<Guid> streams, List<long> versionen)
        => session.QueueSqlCommand(
            $"select {store.Options.Events.DatabaseSchemaName}.{Kopf}(s, v) from unnest(?::uuid[], ?::bigint[]) as t(s, v)",
            streams.ToArray(), versionen.ToArray());

    /// <summary>Scheiterte der Commit an der Kopf-Prüfung (der Stream stand nicht mehr auf der Version der Entscheidung)?</summary>
    public static bool IstKopfKonflikt(Exception ex)
    {
        for (Exception? e = ex; e != null; e = e.InnerException)
            if (e is PostgresException { SqlState: KopfKonflikt }) return true;
        return false;
    }

    /// <summary>Scheiterte der Commit am Schlüssel der Inbox (ein Command, den sie schon kennt)?</summary>
    public static bool IstDuplikat(Exception ex)
    {
        for (Exception? e = ex; e != null; e = e.InnerException)
            if (e is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } pg && pg.TableName == Tabelle) return true;
        return false;
    }

    private static bool IstEindeutigkeitsverletzung(Exception ex)
    {
        for (Exception? e = ex; e != null; e = e.InnerException)
            if (e is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation }) return true;
        return false;
    }

    /// <summary>Der Ausgang eines Commands laut Inbox (Primärschlüssel-Lesen), oder null.</summary>
    public static async Task<KommandoAusgang?> AusgangAsync(IDocumentStore store, Guid commandId, CancellationToken ct)
    {
        await SicherAsync(store, ct);
        await using var session = store.QuerySession();
        var r = await session.QueryAsync<bool>($"select abgelehnt from {Name(store)} where command_id = ?", ct, commandId);
        return r.Count == 0 ? null : r[0] ? KommandoAusgang.Abgelehnt : KommandoAusgang.Gewirkt;
    }
}
