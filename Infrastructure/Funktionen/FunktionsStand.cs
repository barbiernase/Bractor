using Abstractions;
using Marten;
using Marten.Exceptions;

namespace Infrastructure.Funktionen;

/// <summary>
/// Der Stand einer Funktion je Partition (Plan Pub/Sub-Neustrukturierung §2.2: „Position und Fortschritt beim Framework“): Position im Log und
/// geparkte Streams, geschrieben mit erwarteter Version — eine zweite Aktivierung derselben Partition verliert ihren Schreibversuch
/// (<see cref="PartitionsKonfliktException"/>) und lädt neu. Dasselbe Dokument wie der Partitions-Checkpoint einer Projektion, Id
/// <c>funktion:&lt;F&gt;:&lt;Partition&gt;</c>; so zählt auch der Rückstand (<c>/health</c>) die Funktionen mit.
/// </summary>
public interface IFunktionsStand
{
    Task<PartitionsCheckpoint> LadeAsync(string standId, int partition, CancellationToken ct);

    /// <summary>Schreibt den Stand mit erwarteter Version (<see cref="PartitionsCheckpoint.Version"/>); liefert die neue Version.</summary>
    Task<int> SchreibeAsync(PartitionsCheckpoint stand, CancellationToken ct);

    /// <summary>Der gesicherte Zustand einer Funktion an einer Adresse (Plan §2.9), oder null.</summary>
    Task<FunktionsZustandsDokument?> LadeZustandAsync(string id, CancellationToken ct);

    /// <summary>
    /// Sichert Zustand und (bei Log-Eingängen) Stand in EINER Transaktion, beide mit erwarteter Version (A5); liefert die neuen Versionen. Eine zweite
    /// Aktivierung derselben Adresse verliert den ganzen Versuch (<see cref="PartitionsKonfliktException"/>).
    /// </summary>
    Task<(int? Stand, int Zustand)> SichereAsync(PartitionsCheckpoint? stand, FunktionsZustandsDokument zustand, CancellationToken ct);
}

/// <summary>Der Stand im Dokument-Store (Marten, numerische Revision wie beim Partitions-Checkpoint der Projektionen).</summary>
public sealed class MartenFunktionsStand(IDocumentStore store) : IFunktionsStand
{
    public async Task<PartitionsCheckpoint> LadeAsync(string standId, int partition, CancellationToken ct)
    {
        await using var s = store.QuerySession();
        return await s.LoadAsync<PartitionsCheckpoint>(PartitionsCheckpoint.MakeId(standId, partition), ct)
               ?? PartitionsCheckpoint.Neu(standId, partition);
    }

    public async Task<int> SchreibeAsync(PartitionsCheckpoint stand, CancellationToken ct)
    {
        var neu = new PartitionsCheckpoint
        {
            Id = stand.Id, ProjektionsId = stand.ProjektionsId, Partition = stand.Partition, Position = stand.Position,
            Geparkt = stand.Geparkt.ToList(), UpdatedAt = DateTimeOffset.UtcNow, Version = stand.Version + 1,
        };
        try
        {
            await using var s = store.LightweightSession();
            // Noch nie geschrieben → Insert (ein zweiter Erster scheitert am Schlüssel), sonst UpdateRevision (eine zweite Aktivierung → Konflikt).
            if (stand.Version == 0) s.Insert(neu);
            else s.UpdateRevision(neu, stand.Version + 1);
            await s.SaveChangesAsync(ct);
            return neu.Version;
        }
        catch (Exception ex) when (ex is JasperFx.ConcurrencyException or DocumentAlreadyExistsException
                                       || (ex is AggregateException a && a.InnerExceptions.All(i => i is JasperFx.ConcurrencyException or DocumentAlreadyExistsException)))
        {
            throw new PartitionsKonfliktException($"Funktions-Stand {stand.Id} zwischenzeitlich geschrieben (erwartet v{stand.Version}).", ex);
        }
    }

    public Task<FunktionsZustandsDokument?> LadeZustandAsync(string id, CancellationToken ct) => MartenZustand.LadeAsync(store, id, ct);

    public Task<(int? Stand, int Zustand)> SichereAsync(PartitionsCheckpoint? stand, FunktionsZustandsDokument zustand, CancellationToken ct) =>
        MartenZustand.SichereAsync(store, stand, zustand, ct);
}
