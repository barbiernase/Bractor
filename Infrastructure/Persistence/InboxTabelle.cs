using Abstractions;
using Marten;
using Marten.Exceptions;
using Marten.Storage;
using Npgsql;
using Polly;
using Weasel.Postgresql;
using Weasel.Postgresql.Tables;

namespace Infrastructure.Persistence;

/// <summary>
/// Die Inbox der Schreibseite (docs/04 §4.6, Plan Phase 4): eine Tabelle <c>cqrs_inbox</c> im Event-Schema, je Command eine Quittung
/// (<c>command_id</c> eindeutig, <c>stream_id</c>, <c>abgelehnt</c>, <c>zeit</c> = <c>now()</c> der schreibenden Transaktion). Geschrieben
/// in derselben Transaktion wie die Events des Commands; ein zweites Mal scheitert am Primärschlüssel und rollt die Events mit zurück.
///
/// Ein von Marten verwaltetes Schema-Objekt (<c>Storage.ExtendedSchemaObjects</c>): Marten legt Tabelle und Index an (Migrator eager, sonst beim
/// ersten Schreiben über <c>QueueSqlCommand</c> bzw. über <see cref="SicherAsync"/>) und Weasel pflegt sie — eigene Indizes auf Martens Tabellen
/// würde Weasel beim Abgleich löschen. Der Zeit-Index ist ein B-Baum: die Zeit wächst nur, neue Einträge kommen an seinen rechten Rand (billig
/// wie der Index auf <c>seq_id</c>), und der <see cref="InboxAufraeumer"/> findet die ältesten, ohne die Tabelle zu durchsuchen (PF-27).
/// </summary>
public static class InboxTabelle
{
    public const string Tabelle = "cqrs_inbox";

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

        options.ConfigurePolly(b => b.AddRetry(new()
        {
            ShouldHandle = new PredicateBuilder()
                .Handle<NpgsqlException>(e => !IstEindeutigkeitsverletzung(e))
                .Handle<MartenCommandException>(e => !IstEindeutigkeitsverletzung(e))
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
