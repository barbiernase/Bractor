using Domain.Modell;
using Domain.Projections;
using Marten;

namespace Domain.Infrastructure;

/// <summary>
/// Co-Commit-Store der Modell-Projektion. Die Exactly-once-Naht (Puffern → ein SaveChanges +
/// ProjectionCheckpoint) liegt in <see cref="MartenCoCommitStoreBase"/>; hier nur die fachlichen
/// Write-Effekte. TRANSIENT registriert.
/// </summary>
public sealed class ModellStore : MartenCoCommitStoreBase, IModellWriteStore
{
    public ModellStore(IDocumentStore store) : base(store) { }

    public Task UpsertAsync(ModellReadModel model) => EnqueueStore(model);

    public Task SetzeAktivAsync(Guid modellId, string? name, string? pfad, DateTimeOffset aktiviertAm)
        => EnqueueStore(new AktivesModellReadModel
        {
            Id = AktivesModellReadModel.Singleton,
            ModellId = modellId,
            Name = name,
            Pfad = pfad,
            AktiviertAm = aktiviertAm
        });

    public Task ArchiviereAsync(Guid modellId, DateTimeOffset aktualisierung)
        => EnqueueTransform<ModellReadModel>(modellId, existing => existing with { Status = ModellStatus.Archiviert });
}
