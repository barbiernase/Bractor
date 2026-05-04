using System.Collections.Concurrent;
using Domain.ImagePair;
using Domain.Projections;
/*
namespace Domain.Infrastructure;

/// <summary>
/// In-Memory Implementierung für ImagePair Read- und Write-Store.
///
/// Implementiert beide Interfaces — intern dasselbe ConcurrentDictionary.
/// In Produktion wären das getrennte Klassen: der Writer macht gezielte UPDATEs
/// gegen Postgres, der Reader macht SQL-Queries mit dynamischem WHERE.
///
/// Als Singleton registrieren, damit Read- und Write-Seite dieselben Daten sehen.
/// </summary>
public class ImagePairStoreInMemory : IImagePairWriteStore, IImagePairReadStore
{
    private readonly ConcurrentDictionary<Guid, ImagePairReadModel> _data = new();

    // ═══════════════════════════════════════════════════════════
    // Write-Seite
    // ═══════════════════════════════════════════════════════════

    public Task UpsertAsync(ImagePairReadModel model)
    {
        _data[model.Id] = model;
        return Task.CompletedTask;
    }

    public Task SetBildVerfuegbarAsync(
        Guid id, BildVersion version, BildMeta meta, string pfad,
        DateTimeOffset aktualisierung)
    {
        _data.AddOrUpdate(id,
            _ => throw new InvalidOperationException($"ImagePair {id} nicht gefunden"),
            (_, existing) =>
            {
                var updated = version switch
                {
                    BildVersion.Dc0 => existing with
                    {
                        Dc0Pfad = pfad,
                        Dc0OriginalDateiname = meta.OriginalDateiname,
                        Dc0BreitePixel = meta.BreitePixel,
                        Dc0HoehePixel = meta.HoehePixel,
                        LetzteAktualisierung = aktualisierung
                    },
                    BildVersion.Dc2 => existing with
                    {
                        Dc2Pfad = pfad,
                        Dc2OriginalDateiname = meta.OriginalDateiname,
                        Dc2BreitePixel = meta.BreitePixel,
                        Dc2HoehePixel = meta.HoehePixel,
                        LetzteAktualisierung = aktualisierung
                    },
                    _ => existing
                };
                return updated;
            });
        return Task.CompletedTask;
    }

    public Task SetKomplettAsync(Guid id, DateTimeOffset aktualisierung)
    {
        _data.AddOrUpdate(id,
            _ => throw new InvalidOperationException($"ImagePair {id} nicht gefunden"),
            (_, existing) => existing with
            {
                IstKomplett = true,
                LetzteAktualisierung = aktualisierung
            });
        return Task.CompletedTask;
    }

    public Task SetKiEinzelbildKlassifikationAsync(
        Guid id, BildVersion version,
        Klassifikation bildLabel, IReadOnlyList<Klassifikation> regionLabels,
        DateTimeOffset aktualisierung)
    {
        _data.AddOrUpdate(id,
            _ => throw new InvalidOperationException($"ImagePair {id} nicht gefunden"),
            (_, existing) =>
            {
                var kiRegionen = regionLabels
                    .Select(r => (Klassifikation?)r)
                    .ToArray();

                var updated = version switch
                {
                    BildVersion.Dc0 => existing with
                    {
                        Dc0KiBildKlassifikation = bildLabel,
                        Dc0KiRegionen = kiRegionen,
                        HatKiKlassifikation = true,
                        LetzteAktualisierung = aktualisierung
                    },
                    BildVersion.Dc2 => existing with
                    {
                        Dc2KiBildKlassifikation = bildLabel,
                        Dc2KiRegionen = kiRegionen,
                        HatKiKlassifikation = true,
                        LetzteAktualisierung = aktualisierung
                    },
                    _ => existing
                };
                return updated;
            });
        return Task.CompletedTask;
    }

    public Task SetKiBildpaarKlassifikationAsync(
        Guid id, Klassifikation label, DateTimeOffset aktualisierung)
    {
        _data.AddOrUpdate(id,
            _ => throw new InvalidOperationException($"ImagePair {id} nicht gefunden"),
            (_, existing) => existing with
            {
                KiBildpaarKlassifikation = label,
                LetzteAktualisierung = aktualisierung
            });
        return Task.CompletedTask;
    }

    public Task SetMenschRegionLabelAsync(
        Guid id, BildVersion version, int regionIndex,
        Klassifikation label, DateTimeOffset aktualisierung)
    {
        _data.AddOrUpdate(id,
            _ => throw new InvalidOperationException($"ImagePair {id} nicht gefunden"),
            (_, existing) =>
            {
                var updated = version switch
                {
                    BildVersion.Dc0 => existing with
                    {
                        Dc0MenschRegionen = UpdateRegionArray(existing.Dc0MenschRegionen, regionIndex, label),
                        HatMenschLabel = true,
                        LetzteAktualisierung = aktualisierung
                    },
                    BildVersion.Dc2 => existing with
                    {
                        Dc2MenschRegionen = UpdateRegionArray(existing.Dc2MenschRegionen, regionIndex, label),
                        HatMenschLabel = true,
                        LetzteAktualisierung = aktualisierung
                    },
                    _ => existing
                };
                return updated;
            });
        return Task.CompletedTask;
    }

    public Task SetMenschEinzelbildLabelAsync(
        Guid id, BildVersion version,
        Klassifikation label, DateTimeOffset aktualisierung)
    {
        _data.AddOrUpdate(id,
            _ => throw new InvalidOperationException($"ImagePair {id} nicht gefunden"),
            (_, existing) =>
            {
                var updated = version switch
                {
                    BildVersion.Dc0 => existing with
                    {
                        Dc0MenschBildLabel = label,
                        HatMenschLabel = true,
                        LetzteAktualisierung = aktualisierung
                    },
                    BildVersion.Dc2 => existing with
                    {
                        Dc2MenschBildLabel = label,
                        HatMenschLabel = true,
                        LetzteAktualisierung = aktualisierung
                    },
                    _ => existing
                };
                return updated;
            });
        return Task.CompletedTask;
    }

    public Task SetMenschBildpaarLabelAsync(
        Guid id, Klassifikation label, DateTimeOffset aktualisierung)
    {
        _data.AddOrUpdate(id,
            _ => throw new InvalidOperationException($"ImagePair {id} nicht gefunden"),
            (_, existing) => existing with
            {
                MenschBildpaarLabel = label,
                HatMenschLabel = true,
                LetzteAktualisierung = aktualisierung
            });
        return Task.CompletedTask;
    }

    public Task SetPhysischesProduktLabelAsync(
        Guid id, Klassifikation label, DateTimeOffset aktualisierung)
    {
        _data.AddOrUpdate(id,
            _ => throw new InvalidOperationException($"ImagePair {id} nicht gefunden"),
            (_, existing) => existing with
            {
                PhysischesProduktLabel = label,
                HatProduktLabel = true,
                LetzteAktualisierung = aktualisierung
            });
        return Task.CompletedTask;
    }

    // ═══════════════════════════════════════════════════════════
    // Read-Seite
    // ═══════════════════════════════════════════════════════════

    public Task<ImagePairReadModel?> FindByIdAsync(Guid id)
    {
        _data.TryGetValue(id, out var model);
        return Task.FromResult(model);
    }

    public Task<(IReadOnlyList<ImagePairReadModel> Items, int GesamtAnzahl)> SearchAsync(
        ImagePairFilter filter)
    {
        var query = _data.Values.AsEnumerable();

        // Filter anwenden — nur gesetzte Felder filtern
        if (filter.Von.HasValue)
            query = query.Where(m => m.ErstelltAm >= filter.Von.Value);

        if (filter.Bis.HasValue)
            query = query.Where(m => m.ErstelltAm < filter.Bis.Value);

        if (filter.NurKomplette == true)
            query = query.Where(m => m.IstKomplett);

        if (filter.HatKiKlassifikation.HasValue)
            query = query.Where(m => m.HatKiKlassifikation == filter.HatKiKlassifikation.Value);

        if (filter.HatMenschLabel.HasValue)
            query = query.Where(m => m.HatMenschLabel == filter.HatMenschLabel.Value);

        if (filter.KiKlassifikation.HasValue)
        {
            var ki = filter.KiKlassifikation.Value;
            query = query.Where(m =>
                m.Dc0KiBildKlassifikation == ki ||
                m.Dc2KiBildKlassifikation == ki ||
                m.KiBildpaarKlassifikation == ki ||
                m.Dc0KiRegionen.Any(r => r == ki) ||
                m.Dc2KiRegionen.Any(r => r == ki));
        }

        if (filter.MenschLabel.HasValue)
        {
            var ml = filter.MenschLabel.Value;
            query = query.Where(m =>
                m.Dc0MenschBildLabel == ml ||
                m.Dc2MenschBildLabel == ml ||
                m.MenschBildpaarLabel == ml ||
                m.Dc0MenschRegionen.Any(r => r == ml) ||
                m.Dc2MenschRegionen.Any(r => r == ml));
        }

        // Sortierung: neueste zuerst
        var sorted = query.OrderByDescending(m => m.ErstelltAm).ToList();
        var gesamtAnzahl = sorted.Count;

        // Pagination
        var page = sorted
            .Skip((filter.Seite - 1) * filter.SeitenGroesse)
            .Take(filter.SeitenGroesse)
            .ToList();

        return Task.FromResult<(IReadOnlyList<ImagePairReadModel>, int)>((page, gesamtAnzahl));
    }

    public Task<ImagePairStatistik> GetStatistikAsync()
    {
        var alle = _data.Values.ToList();

        var statistik = new ImagePairStatistik(
            Gesamt: alle.Count,
            Komplett: alle.Count(m => m.IstKomplett),
            MitKiKlassifikation: alle.Count(m => m.HatKiKlassifikation),
            MitMenschLabel: alle.Count(m => m.HatMenschLabel),
            MitProduktLabel: alle.Count(m => m.HatProduktLabel),
            OhneKlassifikation: alle.Count(m => !m.HatKiKlassifikation && !m.HatMenschLabel),
            AnzahlAnomalienKi: alle.Count(m =>
                m.Dc0KiBildKlassifikation == Klassifikation.Anomalie ||
                m.Dc2KiBildKlassifikation == Klassifikation.Anomalie ||
                m.KiBildpaarKlassifikation == Klassifikation.Anomalie),
            AnzahlAnomalienMensch: alle.Count(m =>
                m.Dc0MenschBildLabel == Klassifikation.Anomalie ||
                m.Dc2MenschBildLabel == Klassifikation.Anomalie ||
                m.MenschBildpaarLabel == Klassifikation.Anomalie));

        return Task.FromResult(statistik);
    }

    public Task<IReadOnlyList<ImagePairReadModel>> GetUnklassifizierteAsync(int maxAnzahl = 20)
    {
        IReadOnlyList<ImagePairReadModel> result = _data.Values
            .Where(m => m.IstKomplett && !m.HatMenschLabel)
            .OrderBy(m => m.ErstelltAm)
            .Take(maxAnzahl)
            .ToList();

        return Task.FromResult(result);
    }

    // ═══════════════════════════════════════════════════════════
    // Hilfsmethoden
    // ═══════════════════════════════════════════════════════════

    private static IReadOnlyList<Klassifikation?> UpdateRegionArray(
        IReadOnlyList<Klassifikation?> regionen, int index, Klassifikation label)
    {
        var arr = regionen.ToArray();
        arr[index] = label;
        return arr;
    }
}*/