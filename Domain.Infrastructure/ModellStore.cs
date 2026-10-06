using Domain.Modell;
using Domain.Projections;
using Marten;

namespace Domain.Infrastructure;

/// <summary>
/// Co-Commit-Store der Modell-Projektion. Die Exactly-once-Naht (Puffern → ein SaveChanges +
/// ProjectionCheckpoint) liegt in <see cref="MartenCoCommitStoreBase"/>; hier nur die fachlichen
/// Write-Effekte. SCOPED registriert (eine Instanz je Fähigkeits-Bereich, unter jeder Fähigkeit).
/// </summary>
public sealed partial class ModellStore : MartenCoCommitStoreBase, IModellStore
{
    public ModellStore(IDocumentStore store) : base(store) { }

    public Task UpsertAsync(ModellReadModel model) => EnqueueStore(model.Id, model);

    // Geteiltes Dokument (jedes Modell ein eigener Stream): die jüngere Aktivierung nach Event-Zeit gewinnt, bei
    // Gleichstand die größere ModellId — so hängt das Ergebnis nicht davon ab, welcher Stream zuerst verarbeitet wird.
    public Task SetzeAktivAsync(Guid modellId, string? name, string? pfad, DateTimeOffset aktiviertAm)
        => EnqueueGeteilt<AktivesModellReadModel>(AktivesModellReadModel.Singleton, aktuell =>
            aktuell is not null && (aktuell.AktiviertAm, aktuell.ModellId).CompareTo((aktiviertAm, modellId)) >= 0
                ? null
                : new AktivesModellReadModel
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
