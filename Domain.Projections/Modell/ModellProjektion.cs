using Abstractions;
using Domain.Modell;

namespace Domain.Projections.Modell;

/// <summary>
/// Materialisiert den Modell-Zustand: ein
/// <see cref="ModellReadModel"/> pro Modell (Upsert) + den Singleton-Zeiger
/// <see cref="AktivesModellReadModel"/> auf das aktive Modell.
///
/// Upsert-artig; der <c>ModellStore</c> ist ein Store der Klasse 1 (Wirkung und Fortschritt in einer Transaktion).
/// </summary>
public sealed class ModellProjektion : IProjektion
{
    public Task Auf(ModellRegistriert e, Herkunft h, IUpsertModell upsertModell)
        => upsertModell.UpsertAsync(new ModellReadModel
        {
            Id = h.Id<Domain.Modell.Modell>(),
            Name = e.Name,
            Pfad = e.Pfad,
            TrainingslaufId = e.TrainingslaufId,
            DatensatzId = e.DatensatzId,
            DatensatzVersion = e.DatensatzVersion,
            Loss = e.Metriken.Loss,
            Genauigkeit = e.Metriken.Genauigkeit,
            Status = ModellStatus.Registriert,
            RegistriertAm = h.Zeit
        });

    public Task Auf(ModellAktiviert e, Herkunft h, ISetzeModellAktiv setzeModellAktiv)
        => setzeModellAktiv.SetzeAktivAsync(h.Id<Domain.Modell.Modell>(), e.Name, e.Pfad, h.Zeit);

    public Task Auf(ModellArchiviert e, Herkunft h, IArchiviereModell archiviereModell)
        => archiviereModell.ArchiviereAsync(h.Id<Domain.Modell.Modell>(), h.Zeit);
}
