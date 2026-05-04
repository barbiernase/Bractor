using Domain.ImagePair;
using Domain.Projections;
using Marten;
using Microsoft.Extensions.Logging;

namespace Domain.Infrastructure;

/// <summary>
/// PostgreSQL-Implementierung für ImagePair Read- und Write-Store.
/// Nutzt Marten als Document Store mit eigenem Schema "rm".
/// </summary>
public class ImagePairStorePostgres : IImagePairWriteStore, IImagePairReadStore
{
    private readonly IDocumentStore _store;
    private readonly ILogger<ImagePairStorePostgres> _logger;

    public ImagePairStorePostgres(
        IDocumentStore store,
        ILogger<ImagePairStorePostgres> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    // ═══════════════════════════════════════════════════════════
    // Write-Seite
    // ═══════════════════════════════════════════════════════════

    public async Task UpsertAsync(ImagePairReadModel model)
    {
        await using var session = _store.LightweightSession();
        session.Store(model);
        await session.SaveChangesAsync();
    }

    public async Task SetBildVerfuegbarAsync(
        Guid id, BildVersion version, BildMeta meta, string pfad,
        DateTimeOffset aktualisierung)
    {
        await using var session = _store.LightweightSession();
        var existing = await session.LoadAsync<ImagePairReadModel>(id);
        if (existing == null) { _logger.LogWarning("SetBildVerfuegbar: {Id} nicht gefunden", id); return; }

        var updated = version switch
        {
            BildVersion.Dc0 => existing with
            {
                Dc0Pfad = pfad, Dc0OriginalDateiname = meta.OriginalDateiname,
                Dc0BreitePixel = meta.BreitePixel, Dc0HoehePixel = meta.HoehePixel,
                LetzteAktualisierung = aktualisierung
            },
            BildVersion.Dc2 => existing with
            {
                Dc2Pfad = pfad, Dc2OriginalDateiname = meta.OriginalDateiname,
                Dc2BreitePixel = meta.BreitePixel, Dc2HoehePixel = meta.HoehePixel,
                LetzteAktualisierung = aktualisierung
            },
            _ => existing
        };
        session.Store(updated);
        await session.SaveChangesAsync();
    }

    public async Task SetKomplettAsync(Guid id, DateTimeOffset aktualisierung)
    {
        await using var session = _store.LightweightSession();
        var existing = await session.LoadAsync<ImagePairReadModel>(id);
        if (existing == null) return;
        session.Store(existing with { IstKomplett = true, LetzteAktualisierung = aktualisierung });
        await session.SaveChangesAsync();
    }

    public async Task SetKiEinzelbildKlassifikationAsync(
        Guid id, BildVersion version,
        Klassifikation bildLabel, IReadOnlyList<Klassifikation> regionLabels,
        DateTimeOffset aktualisierung)
    {
        await using var session = _store.LightweightSession();
        var existing = await session.LoadAsync<ImagePairReadModel>(id);
        if (existing == null) return;

        var kiRegionen = regionLabels.Select(r => (Klassifikation?)r).ToArray();
        var updated = version switch
        {
            BildVersion.Dc0 => existing with
            {
                Dc0KiBildKlassifikation = bildLabel, Dc0KiRegionen = kiRegionen,
                HatKiKlassifikation = true, LetzteAktualisierung = aktualisierung
            },
            BildVersion.Dc2 => existing with
            {
                Dc2KiBildKlassifikation = bildLabel, Dc2KiRegionen = kiRegionen,
                HatKiKlassifikation = true, LetzteAktualisierung = aktualisierung
            },
            _ => existing
        };
        session.Store(updated);
        await session.SaveChangesAsync();
    }

    public async Task SetKiBildpaarKlassifikationAsync(Guid id, Klassifikation label, DateTimeOffset aktualisierung)
    {
        await using var session = _store.LightweightSession();
        var existing = await session.LoadAsync<ImagePairReadModel>(id);
        if (existing == null) return;
        session.Store(existing with { KiBildpaarKlassifikation = label, LetzteAktualisierung = aktualisierung });
        await session.SaveChangesAsync();
    }

    public async Task SetMenschRegionLabelAsync(Guid id, BildVersion version, int regionIndex, Klassifikation label, DateTimeOffset aktualisierung)
    {
        await using var session = _store.LightweightSession();
        var existing = await session.LoadAsync<ImagePairReadModel>(id);
        if (existing == null) return;

        var updated = version switch
        {
            BildVersion.Dc0 => existing with
            {
                Dc0MenschRegionen = UpdateRegionArray(existing.Dc0MenschRegionen, regionIndex, label),
                HatMenschLabel = true, LetzteAktualisierung = aktualisierung
            },
            BildVersion.Dc2 => existing with
            {
                Dc2MenschRegionen = UpdateRegionArray(existing.Dc2MenschRegionen, regionIndex, label),
                HatMenschLabel = true, LetzteAktualisierung = aktualisierung
            },
            _ => existing
        };
        session.Store(updated);
        await session.SaveChangesAsync();
    }

    public async Task SetMenschEinzelbildLabelAsync(Guid id, BildVersion version, Klassifikation label, DateTimeOffset aktualisierung)
    {
        await using var session = _store.LightweightSession();
        var existing = await session.LoadAsync<ImagePairReadModel>(id);
        if (existing == null) return;

        var updated = version switch
        {
            BildVersion.Dc0 => existing with { Dc0MenschBildLabel = label, HatMenschLabel = true, LetzteAktualisierung = aktualisierung },
            BildVersion.Dc2 => existing with { Dc2MenschBildLabel = label, HatMenschLabel = true, LetzteAktualisierung = aktualisierung },
            _ => existing
        };
        session.Store(updated);
        await session.SaveChangesAsync();
    }

    public async Task SetMenschBildpaarLabelAsync(Guid id, Klassifikation label, DateTimeOffset aktualisierung)
    {
        await using var session = _store.LightweightSession();
        var existing = await session.LoadAsync<ImagePairReadModel>(id);
        if (existing == null) return;
        session.Store(existing with { MenschBildpaarLabel = label, HatMenschLabel = true, LetzteAktualisierung = aktualisierung });
        await session.SaveChangesAsync();
    }

    public async Task SetPhysischesProduktLabelAsync(Guid id, Klassifikation label, DateTimeOffset aktualisierung)
    {
        await using var session = _store.LightweightSession();
        var existing = await session.LoadAsync<ImagePairReadModel>(id);
        if (existing == null) return;
        session.Store(existing with { PhysischesProduktLabel = label, HatProduktLabel = true, LetzteAktualisierung = aktualisierung });
        await session.SaveChangesAsync();
    }

    public async Task SetInspiziertAsync(Guid id, DateTimeOffset aktualisierung)
    {
        await using var session = _store.LightweightSession();
        var existing = await session.LoadAsync<ImagePairReadModel>(id);
        if (existing == null) return;
        session.Store(existing with { IstInspiziert = true, LetzteAktualisierung = aktualisierung });
        await session.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════
    // Read-Seite
    // ═══════════════════════════════════════════════════════════

    public async Task<ImagePairReadModel?> FindByIdAsync(Guid id)
    {
        await using var session = _store.QuerySession();
        return await session.LoadAsync<ImagePairReadModel>(id);
    }

    public async Task<(IReadOnlyList<ImagePairReadModel> Items, int GesamtAnzahl)> SearchAsync(
        ImagePairFilter filter)
    {
        await using var session = _store.QuerySession();
        var query = session.Query<ImagePairReadModel>().AsQueryable();

        if (filter.Von.HasValue)
            query = query.Where(m => m.ProduziertAm >= filter.Von.Value);

        if (filter.Bis.HasValue)
            query = query.Where(m => m.ProduziertAm < filter.Bis.Value);

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
                m.KiBildpaarKlassifikation == ki);
        }

        if (filter.MenschLabel.HasValue)
        {
            var ml = filter.MenschLabel.Value;
            query = query.Where(m =>
                m.Dc0MenschBildLabel == ml ||
                m.Dc2MenschBildLabel == ml ||
                m.MenschBildpaarLabel == ml);
        }

        if (filter.ProduktLabel.HasValue)
        {
            var pl = filter.ProduktLabel.Value;
            query = query.Where(m => m.PhysischesProduktLabel == pl);
        }

        if (filter.NurNichtInspizierte == true)
            query = query.Where(m => !m.IstInspiziert);

        var gesamtAnzahl = await query.CountAsync();

        var page = await query
            .OrderByDescending(m => m.ProduziertAm)
            .Skip((filter.Seite - 1) * filter.SeitenGroesse)
            .Take(filter.SeitenGroesse)
            .ToListAsync();

        return (page, gesamtAnzahl);
    }

    public async Task<ImagePairStatistik> GetStatistikAsync()
    {
        await using var session = _store.QuerySession();
        var alle = await session.Query<ImagePairReadModel>().ToListAsync();

        return new ImagePairStatistik(
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
    }

    public async Task<IReadOnlyList<ImagePairReadModel>> GetUnklassifizierteAsync(int maxAnzahl = 20)
    {
        await using var session = _store.QuerySession();
        return await session.Query<ImagePairReadModel>()
            .Where(m => m.IstKomplett && !m.HatMenschLabel)
            .OrderBy(m => m.ProduziertAm)
            .Take(maxAnzahl)
            .ToListAsync();
    }

    // ═══════════════════════════════════════════════════════════
    // Chart: Produktionsverlauf — Zeitbucket-Aggregation
    // ═══════════════════════════════════════════════════════════

    public async Task<ProduktionsVerlaufAntwort> GetVerlaufAsync(
        DateTimeOffset von, DateTimeOffset bis, int bucketMinuten)
    {
        await using var session = _store.QuerySession();

        var items = await session.Query<ImagePairReadModel>()
            .Where(m => m.ProduziertAm >= von && m.ProduziertAm < bis)
            .ToListAsync();

        var bucketSize = TimeSpan.FromMinutes(bucketMinuten);

        var buckets = items
            .GroupBy(m => TruncateZeit(m.ProduziertAm, bucketSize))
            .OrderBy(g => g.Key)
            .Select(g => new ProduktionsZeitBucket(
                Zeitpunkt: g.Key,
                Gesamt: g.Count(),
                Ok: g.Count(m => m.PhysischesProduktLabel == Klassifikation.KeineAnomalie),
                Questionable: g.Count(m => m.PhysischesProduktLabel == Klassifikation.Questionable),
                Anomalie: g.Count(m => m.PhysischesProduktLabel == Klassifikation.Anomalie),
                Ungelabelt: g.Count(m => !m.HatProduktLabel),
                NichtInspiziert: g.Count(m => !m.IstInspiziert)))
            .ToList();

        var vollstaendig = FuelleLuecken(buckets, von, bis, bucketSize);

        return new ProduktionsVerlaufAntwort(
            Buckets: vollstaendig,
            GesamtImZeitraum: items.Count,
            AnomalienImZeitraum: items.Count(m =>
                m.PhysischesProduktLabel == Klassifikation.Anomalie),
            UngelabeltImZeitraum: items.Count(m => !m.HatProduktLabel));
    }

    // ═══════════════════════════════════════════════════════════
    // Chart: Produktionstage — Tagesliste
    // ═══════════════════════════════════════════════════════════

    public async Task<ProduktionsTageAntwort> GetProduktionsTageAsync()
    {
        await using var session = _store.QuerySession();

        var alle = await session.Query<ImagePairReadModel>().ToListAsync();

        var tage = alle
            .GroupBy(m => m.ProduziertAm.Date)
            .OrderByDescending(g => g.Key)
            .Select(g => new ProduktionsTag(
                Datum: new DateTimeOffset(g.Key, TimeSpan.Zero),
                AnzahlGesamt: g.Count(),
                AnzahlAnomalie: g.Count(m =>
                    m.PhysischesProduktLabel == Klassifikation.Anomalie),
                AnzahlUngelabelt: g.Count(m => !m.HatProduktLabel)))
            .ToList();

        return new ProduktionsTageAntwort(tage);
    }

    // ═══════════════════════════════════════════════════════════
    // Chart: Produktions-Strip — ein Punkt pro Teil
    // ═══════════════════════════════════════════════════════════

    public async Task<ProduktionsStripAntwort> GetProduktionsStripAsync(
        DateTimeOffset von, DateTimeOffset bis)
    {
        await using var session = _store.QuerySession();

        var punkte = await session.Query<ImagePairReadModel>()
            .Where(m => m.ProduziertAm >= von && m.ProduziertAm < bis)
            .OrderBy(m => m.ProduziertAm)
            .Select(m => new ProduktionsStripPunkt(
                m.ProduziertAm,
                m.PhysischesProduktLabel))
            .ToListAsync();

        return new ProduktionsStripAntwort(punkte);
    }

    // ═══════════════════════════════════════════════════════════
    // Hilfsmethoden
    // ═══════════════════════════════════════════════════════════

    private static DateTimeOffset TruncateZeit(DateTimeOffset dt, TimeSpan bucket)
    {
        var ticks = dt.UtcTicks - (dt.UtcTicks % bucket.Ticks);
        return new DateTimeOffset(ticks, TimeSpan.Zero);
    }

    private static IReadOnlyList<ProduktionsZeitBucket> FuelleLuecken(
        List<ProduktionsZeitBucket> buckets,
        DateTimeOffset von, DateTimeOffset bis,
        TimeSpan bucketSize)
    {
        var index = buckets.ToDictionary(b => b.Zeitpunkt);
        var result = new List<ProduktionsZeitBucket>();
        var leer = new ProduktionsZeitBucket(default, 0, 0, 0, 0, 0, 0);

        var start = TruncateZeit(von, bucketSize);
        var ende = TruncateZeit(bis, bucketSize);

        for (var t = start; t <= ende; t = t.Add(bucketSize))
        {
            result.Add(index.TryGetValue(t, out var existing)
                ? existing
                : leer with { Zeitpunkt = t });
        }

        return result;
    }

    private static IReadOnlyList<Klassifikation?> UpdateRegionArray(
        IReadOnlyList<Klassifikation?> regionen, int index, Klassifikation label)
    {
        var arr = regionen.ToArray();
        arr[index] = label;
        return arr;
    }
}