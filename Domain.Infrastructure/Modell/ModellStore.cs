using Abstractions;
using Core;
using Domain.Modell;
using Domain.Projections;
using Marten;
using Domain.Projections.Modell;

using ModellAgg = Domain.Modell.Modell;

namespace Domain.Infrastructure.Modell;

/// <summary>
/// Store (Klasse 1) der Modell-Projektion. Die Exactly-once-Naht (Puffern → ein SaveChanges +
/// ProjectionCheckpoint) liegt in <see cref="MartenStore"/>; hier nur die fachlichen
/// Write-Effekte. SCOPED registriert (eine Instanz je Fähigkeits-Bereich, unter jeder Fähigkeit).
/// </summary>
public sealed partial class ModellStore : MartenStore, IModellStore
{
    public ModellStore(IDocumentStore store) : base(store, typeof(ModellReadModel), typeof(AktivesModellReadModel)) { }

    public Task UpsertAsync(ModellReadModel model) => EnqueueStore(model.Id.Wert, model);

    // Geteiltes Dokument (jedes Modell ein eigener Stream): die jüngere Aktivierung nach Event-Zeit gewinnt, bei
    // Gleichstand die größere ModellId — so hängt das Ergebnis nicht davon ab, welcher Stream zuerst verarbeitet wird.
    public Task SetzeAktivAsync(Id<ModellAgg> modellId, string? name, string? pfad, DateTimeOffset aktiviertAm)
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

    public Task ArchiviereAsync(Id<ModellAgg> modellId, DateTimeOffset aktualisierung)
        => EnqueueTransform<ModellReadModel>(modellId.Wert, existing => existing with { Status = ModellStatus.Archiviert });
}
