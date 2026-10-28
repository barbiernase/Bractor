using Abstractions;
using Core;
using Domain.Datensatz;
using Domain.Projections;
using Marten;
using Domain.Projections.Datensatz;

using DatensatzAgg = Domain.Datensatz.Datensatz;

using ImagePairAgg = Domain.ImagePair.ImagePair;

namespace Domain.Infrastructure.Datensatz;

/// <summary>
/// Co-Commit-Store der Datensatz-Projektion. Exactly-once-Naht in <see cref="MartenStore"/>;
/// hier nur die fachlichen Write-Effekte (inkl. Rückwärts-Index-Pflege + Einfrieren-Snapshot). Die
/// Projektion ist <see cref="Abstractions.IAppendProjektion"/> (Einfrieren schreibt neue Sample-Zeilen).
/// SCOPED registriert (eine Instanz je Fähigkeits-Bereich, unter jeder Fähigkeit).
/// </summary>
public sealed partial class DatensatzStore : MartenStore, IDatensatzStore
{
    public DatensatzStore(IDocumentStore store) : base(store, typeof(DatensatzReadModel), typeof(DatensatzSampleReadModel), typeof(DatensatzMitgliedschaftZeile)) { }

    public Task UpsertAsync(DatensatzReadModel model) => EnqueueStore(model.Id.Wert, model);

    public Task NimmRangeAufAsync(
        Id<DatensatzAgg> id, IReadOnlyList<Id<ImagePairAgg>> imagePairIds, RangeHerkunft herkunft, DateTimeOffset aktualisierung)
    {
        EnqueueTransform<DatensatzReadModel>(id.Wert, existing =>
        {
            var mitglieder = new List<Id<ImagePairAgg>>(existing.Mitglieder);
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

    public Task NimmPaarAufAsync(Id<DatensatzAgg> id, Id<ImagePairAgg> imagePairId, DateTimeOffset aktualisierung)
    {
        EnqueueTransform<DatensatzReadModel>(id.Wert, existing =>
        {
            var ausgeschlossen = existing.Ausgeschlossen.Where(x => x != imagePairId).ToList();
            if (existing.Mitglieder.Contains(imagePairId))
                return ausgeschlossen.Count == existing.Ausgeschlossen.Count
                    ? existing
                    : existing with { Ausgeschlossen = ausgeschlossen, LetzteAktualisierung = aktualisierung };
            var mitglieder = new List<Id<ImagePairAgg>>(existing.Mitglieder) { imagePairId };
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

    public Task EntfernePaarAsync(Id<DatensatzAgg> id, Id<ImagePairAgg> imagePairId, DateTimeOffset aktualisierung)
    {
        EnqueueTransform<DatensatzReadModel>(id.Wert, existing =>
        {
            if (!existing.Mitglieder.Contains(imagePairId)) return existing;
            var mitglieder = new List<Id<ImagePairAgg>>(existing.Mitglieder);
            mitglieder.Remove(imagePairId);
            // Aussortiert → in die Ausgeschlossen-Sicht (dedup).
            var ausgeschlossen = new List<Id<ImagePairAgg>>(existing.Ausgeschlossen);
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

    public Task SetzeSplitAsync(Id<DatensatzAgg> id, SplitKonfig split, DateTimeOffset aktualisierung)
        => EnqueueTransform<DatensatzReadModel>(id.Wert, existing => existing with { Split = split, LetzteAktualisierung = aktualisierung });

    public Task FriereEinAsync(
        Id<DatensatzAgg> id, int version, IReadOnlyList<DatensatzMitglied> mitglieder, DateTimeOffset aktualisierung)
    {
        // 1) Datensatz-Kopf: Status + eingefrorene Version.
        EnqueueTransform<DatensatzReadModel>(id.Wert, existing => existing with
        {
            Status = DatensatzStatus.Eingefroren,
            EingefroreneVersion = version,
            LetzteAktualisierung = aktualisierung
        });

        // 2) Je Mitglied eine Sample-Zeile dieses Datensatzes (immutable Snapshot; idempotent über die zusammengesetzte Id).
        foreach (var m in mitglieder)
            EnqueueZeile(id.Wert, new DatensatzSampleReadModel
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

        return Task.CompletedTask;
    }

    /// <summary>
    /// Puffert die Rückwärts-Index-Pflege als ZEILE dieses Datensatzes: drin → Zeile <c>{datensatzId}:{imagePairId}</c>
    /// anlegen (idempotent), raus → entfernen. Keine Liste je Bildpaar: die würden mehrere Datensatz-Streams parallel
    /// überschreiben. Co-committet im selben Batch wie das Vorwärts-Delta.
    /// </summary>
    private void BufferRueckwaerts(Id<ImagePairAgg> imagePairId, Id<DatensatzAgg> datensatzId, bool drin)
    {
        var zeilenId = DatensatzMitgliedschaftZeile.MakeId(datensatzId, imagePairId);
        if (drin)
            EnqueueZeile(datensatzId.Wert, new DatensatzMitgliedschaftZeile
            {
                Id = zeilenId,
                DatensatzId = datensatzId,
                ImagePairId = imagePairId
            });
        else
            EnqueueZeileEntfernen<DatensatzMitgliedschaftZeile>(datensatzId.Wert, zeilenId);
    }
}
