using Abstractions;
using Core;
using Domain.Projections.ImagePair;
using Infrastructure.Persistence;
using Infrastructure.Projections;
using Marten;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Gemeinsame Hilfe der Integrations-Tests für den Batch-Konsum gegen echtes Marten. Die Test-Schemata leben über viele Läufe; ein frischer
/// Projektions-Schlüssel beginnt deshalb am Kopf des Logs (wie eine Projektion, die jetzt in Betrieb geht und die Historie nicht braucht) —
/// sonst läse jeder Test die ganze Historie des Schemas.
/// </summary>
public static class BatchHilfe
{
    /// <summary>
    /// Marten setzt den Event-Zeitstempel beim Append mit der Uhr des Clients, die Karenz vergleicht ihn mit der DB-Uhr. Mit Karenz 0 liegt ein
    /// eben angehängtes Event deshalb manchmal um Bruchteile einer Millisekunde „in der Zukunft“ (gemessen bis 0,2 ms) und ist noch nicht gesetzt.
    /// Tests, die mit Karenz 0 eine Position prüfen, warten vorher kurz.
    /// </summary>
    public static Task UhrenversatzAbwartenAsync() => Task.Delay(20);

    /// <summary>
    /// Die höchste Sequenz des Logs im Schema des Stores. Rohes SQL löst Martens AutoCreate nicht aus — auf einer frischen Datenbank (vor dem
    /// ersten Append) legt der Helfer die Event-Tabellen deshalb selbst an.
    /// </summary>
    public static async Task<long> KopfAsync(IDocumentStore store)
    {
        await store.Storage.Database.EnsureStorageExistsAsync(typeof(JasperFx.Events.IEvent));
        await using var s = store.QuerySession();
        var r = await s.QueryAsync<long>($"select coalesce(max(seq_id), 0) from {store.Options.Events.DatabaseSchemaName}.mt_events");
        return r.Count > 0 ? r[0] : 0;
    }

    /// <summary>Legt alle Partitionen der Projektion am Kopf des Logs an (Version 1).</summary>
    public static async Task StarteAmKopfAsync(IDocumentStore store, string projektionsId, Konsum konsum)
    {
        var kopf = await KopfAsync(store);
        await using var s = store.LightweightSession();
        for (var p = 0; p < konsum.Partitionen; p++)
        {
            var cp = PartitionsCheckpoint.Neu(projektionsId, p);
            cp.Position = kopf;
            s.Insert(cp);
        }
        await s.SaveChangesAsync();
    }

    public static async Task<PartitionsCheckpoint?> PartitionAsync(IDocumentStore store, string projektionsId, int partition)
    {
        await using var s = store.QuerySession();
        return await s.LoadAsync<PartitionsCheckpoint>(PartitionsCheckpoint.MakeId(projektionsId, partition));
    }

    public static async Task<int> GuardAsync(IDocumentStore store, string projektionsId, Guid stream)
    {
        await using var s = store.QuerySession();
        return (await s.LoadAsync<ProjectionCheckpoint>(ProjectionCheckpoint.MakeId(projektionsId, stream)))?.Version ?? -1;
    }

    /// <summary>Eine Stream-Id in Partition <paramref name="partition"/>.</summary>
    public static Guid StreamIn(int partition, int partitionen)
    {
        while (true)
        {
            var id = Guid.NewGuid();
            if (Partitionierung.Von(id, partitionen) == partition) return id;
        }
    }

    /// <summary>Der Dispatch der Historien-Projektion über eine Store-Instanz (wie der generierte Batch-Kind).</summary>
    public static Func<EventEnvelope, ProjectionWriter, Task> HistorieDispatch(IAppendHistorieEintrag store, Func<EventEnvelope, Task>? vorher = null)
    {
        var projektion = new ImagePairHistorieProjektion();
        var faehigkeiten = new FaehigkeitenAus(store);
        return async (e, writer) =>
        {
            if (vorher is not null) await vorher(e);
            await projektion.DispatchAsync(e, writer, BatchLeser.KeineAusgabe, faehigkeiten);
        };
    }

    /// <summary>Ein Batch-Leser der Historien-Projektion mit echtem Typfilter (inkl. Upcasting-Herkunft).</summary>
    public static BatchLeser HistorieLeser(MartenEventStore es, Domain.Infrastructure.ImagePair.ImagePairHistorieStore store, string projektionsId,
        int partition, Konsum konsum, Func<EventEnvelope, Task>? vorher = null, Func<Task>? absturz = null, Func<DateTimeOffset>? jetzt = null)
        => new(es, store, projektionsId, partition, konsum, BatchLeser.TypenAus(ImagePairHistorieProjektion.SubscribedTypes),
            HistorieDispatch(store, vorher), absturzVorCommit: absturz, jetzt: jetzt);
}
