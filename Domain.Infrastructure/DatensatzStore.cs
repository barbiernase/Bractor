using Domain.Datensatz;
using Domain.Projections;
using Marten;

namespace Domain.Infrastructure;

/// <summary>
/// Co-Commit-Store der Datensatz-Projektion. Exactly-once-Naht in <see cref="MartenCoCommitStoreBase"/>;
/// hier nur die fachlichen Write-Effekte (inkl. Rückwärts-Index-Pflege + Einfrieren-Snapshot). Die
/// Projektion ist <see cref="Abstractions.IAppendProjektion"/> (Einfrieren schreibt neue Sample-Zeilen).
/// TRANSIENT registriert.
/// </summary>
public sealed class DatensatzStore : MartenCoCommitStoreBase, IDatensatzWriteStore
{
    public DatensatzStore(IDocumentStore store) : base(store) { }

    public Task UpsertAsync(DatensatzReadModel model) => EnqueueStore(model);

    public Task NimmRangeAufAsync(
        Guid id, IReadOnlyList<Guid> imagePairIds, RangeHerkunft herkunft, DateTimeOffset aktualisierung)
    {
        EnqueueTransform<DatensatzReadModel>(id, existing =>
        {
            var mitglieder = new List<Guid>(existing.Mitglieder);
            foreach (var pid in imagePairIds)
                if (!mitglieder.Contains(pid)) mitglieder.Add(pid);   // Union, dedup, Reihenfolge stabil
            var ausgeschlossen = existing.Ausgeschlossen.Where(x => !imagePairIds.Contains(x)).ToList();
            var ranges = new List<RangeHerkunft>(existing.Ranges) { herkunft };
            return existing with
            {
                Mitglieder = mitglieder,
                Ausgeschlossen = ausgeschlossen,
                AnzahlMitglieder = mitglieder.Count,
                Ranges = ranges,
                LetzteAktualisierung = aktualisierung
            };
        });
        foreach (var pid in imagePairIds) BufferRueckwaerts(pid, id, drin: true);
        return Task.CompletedTask;
    }

    public Task NimmPaarAufAsync(Guid id, Guid imagePairId, DateTimeOffset aktualisierung)
    {
        EnqueueTransform<DatensatzReadModel>(id, existing =>
        {
            var ausgeschlossen = existing.Ausgeschlossen.Where(x => x != imagePairId).ToList();
            if (existing.Mitglieder.Contains(imagePairId))
                return ausgeschlossen.Count == existing.Ausgeschlossen.Count
                    ? existing
                    : existing with { Ausgeschlossen = ausgeschlossen, LetzteAktualisierung = aktualisierung };
            var mitglieder = new List<Guid>(existing.Mitglieder) { imagePairId };
            return existing with
            {
                Mitglieder = mitglieder,
                Ausgeschlossen = ausgeschlossen,
                AnzahlMitglieder = mitglieder.Count,
                LetzteAktualisierung = aktualisierung
            };
        });
        BufferRueckwaerts(imagePairId, id, drin: true);
        return Task.CompletedTask;
    }

    public Task EntfernePaarAsync(Guid id, Guid imagePairId, DateTimeOffset aktualisierung)
    {
        EnqueueTransform<DatensatzReadModel>(id, existing =>
        {
            if (!existing.Mitglieder.Contains(imagePairId)) return existing;
            var mitglieder = new List<Guid>(existing.Mitglieder);
            mitglieder.Remove(imagePairId);
            // Aussortiert → in die Ausgeschlossen-Sicht (dedup).
            var ausgeschlossen = new List<Guid>(existing.Ausgeschlossen);
            if (!ausgeschlossen.Contains(imagePairId)) ausgeschlossen.Add(imagePairId);
            return existing with
            {
                Mitglieder = mitglieder,
                Ausgeschlossen = ausgeschlossen,
                AnzahlMitglieder = mitglieder.Count,
                LetzteAktualisierung = aktualisierung
            };
        });
        BufferRueckwaerts(imagePairId, id, drin: false);
        return Task.CompletedTask;
    }

    public Task SetzeSplitAsync(Guid id, SplitKonfig split, DateTimeOffset aktualisierung)
        => EnqueueTransform<DatensatzReadModel>(id, existing => existing with { Split = split, LetzteAktualisierung = aktualisierung });

    public Task FriereEinAsync(
        Guid id, int version, IReadOnlyList<DatensatzMitglied> mitglieder, DateTimeOffset aktualisierung)
    {
        // 1) Datensatz-Kopf: Status + eingefrorene Version.
        EnqueueTransform<DatensatzReadModel>(id, existing => existing with
        {
            Status = DatensatzStatus.Eingefroren,
            EingefroreneVersion = version,
            LetzteAktualisierung = aktualisierung
        });

        // 2) Je Mitglied eine Sample-Zeile (immutable Snapshot; idempotent über die zusammengesetzte Id).
        Enqueue(s =>
        {
            foreach (var m in mitglieder)
            {
                s.Store(new DatensatzSampleReadModel
                {
                    Id = DatensatzSampleReadModel.MakeId(id, version, m.ImagePairId),
                    DatensatzId = id,
                    Version = version,
                    ImagePairId = m.ImagePairId,
                    Dc0Pfad = m.Dc0Pfad,
                    Dc2Pfad = m.Dc2Pfad,
                    Label = m.Label,
                    Split = m.Split
                });
            }
            return Task.CompletedTask;
        });

        return Task.CompletedTask;
    }

    /// <summary>
    /// Puffert die Rückwärts-Index-Pflege: <c>imagePairId → +/− datensatzId</c>. Idempotent
    /// (Union bzw. Remove), co-committet im selben Batch wie das Vorwärts-Delta.
    /// </summary>
    private void BufferRueckwaerts(Guid imagePairId, Guid datensatzId, bool drin)
    {
        Enqueue(async s =>
        {
            var doc = await s.LoadAsync<DatensatzMitgliedschaftReadModel>(imagePairId)
                      ?? new DatensatzMitgliedschaftReadModel { Id = imagePairId };

            if (drin)
            {
                if (doc.DatensatzIds.Contains(datensatzId)) return;   // schon drin → idempotent
                s.Store(doc with { DatensatzIds = new List<Guid>(doc.DatensatzIds) { datensatzId } });
            }
            else
            {
                if (!doc.DatensatzIds.Contains(datensatzId)) return;
                var ids = new List<Guid>(doc.DatensatzIds);
                ids.Remove(datensatzId);
                s.Store(doc with { DatensatzIds = ids });
            }
        });
    }
}
