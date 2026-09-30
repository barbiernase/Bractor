using Domain.ImagePair;
using Domain.Projections;
using Marten;

namespace Domain.Infrastructure;

/// <summary>
/// Lese-Seite des <see cref="ImagePairStore"/> (Teil derselben Klasse, eine Instanz je DI-Bereich): eigene
/// Query-Sessions, sieht committete Daten. Jede Methode bedient genau eine Lese-Fähigkeit.
/// </summary>
public sealed partial class ImagePairStore
{
    // ═══════════════════════════════════════════════════════════
    // Read-Seite
    // ═══════════════════════════════════════════════════════════

    public async Task<ImagePairReadModel?> FindByIdAsync(Guid id)
    {
        await using var session = Store.QuerySession();
        return await session.LoadAsync<ImagePairReadModel>(id);
    }

    public async Task<IReadOnlyList<ImagePairReadModel>> LadeVieleAsync(IReadOnlyList<Guid> ids)
    {
        if (ids.Count == 0) return System.Array.Empty<ImagePairReadModel>();
        await using var session = Store.QuerySession();
        var geladen = await session.LoadManyAsync<ImagePairReadModel>(ids.ToArray());
        return geladen.ToList();
    }

    public async Task<(IReadOnlyList<ImagePairReadModel> Items, int GesamtAnzahl)> SearchAsync(
        ImagePairFilter filter)
    {
        await using var session = Store.QuerySession();
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
        await using var session = Store.QuerySession();
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
        await using var session = Store.QuerySession();
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
        await using var session = Store.QuerySession();

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
        await using var session = Store.QuerySession();

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
        await using var session = Store.QuerySession();

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
}
