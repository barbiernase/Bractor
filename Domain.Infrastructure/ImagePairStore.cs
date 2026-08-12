using Abstractions;
using Domain.ImagePair;
using Domain.Projections;
using Marten;

namespace Domain.Infrastructure;

/// <summary>
/// Co-Commit-Store der ImagePair-Projektion (Domänen-Infra, nicht im Framework) — die Pull-Variante der
/// Write-Seite von <see cref="ImagePairStorePostgres"/> nach dem Naht-Muster von
/// <see cref="ImagePairHistorieStore"/> (Spec 7.4, Fall 1).
///
/// Die Write-Operationen PUFFERN nur (als aufgeschobene Session-Operationen). <see cref="MarkProcessedAsync"/>
/// ist der Commit-Punkt: EINE Marten-<c>IdentitySession</c> spielt alle gepufferten Effekte in Reihenfolge
/// ab (read-your-writes über die Identity-Map — ein späteres Set sieht ein früheres Upsert im selben Batch)
/// UND staged den <see cref="ProjectionCheckpoint"/>, dann EIN <c>SaveChanges</c> → Effekt und Marke gemeinsam
/// gültig (exactly-once). Ein Absturz davor hat den DB nie berührt → nichts durabel → nächster Read heilt.
///
/// NUR die Write-Seite (+ Tracker): die Read-Seite (<see cref="IImagePairReadStore"/>) bleibt das bestehende
/// Singleton <see cref="ImagePairStorePostgres"/> (eigene Query-Sessions, sieht committete Daten).
///
/// TRANSIENT registriert (frisch pro Stream-Actor → isolierter Puffer; der Single-Writer-Actor serialisiert).
/// </summary>
public sealed class ImagePairStore
    : ICoCommitTracker, IImagePairWriteStore
{
    private readonly IDocumentStore _store;
    private readonly List<Func<IDocumentSession, Task>> _pending = new();

    public ImagePairStore(IDocumentStore store) => _store = store;

    // ═══════════════════════════════════════════════════════════
    // Write-Seite: nur puffern, KEIN Commit (aufgeschoben auf MarkProcessedAsync)
    // ═══════════════════════════════════════════════════════════

    public Task UpsertAsync(ImagePairReadModel model)
    {
        _pending.Add(s => { s.Store(model); return Task.CompletedTask; });
        return Task.CompletedTask;
    }

    public Task SetBildVerfuegbarAsync(
        Guid id, BildVersion version, BildMeta meta, string pfad, DateTimeOffset aktualisierung)
        => Buffer(id, existing => version switch
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
        });

    public Task SetKomplettAsync(Guid id, DateTimeOffset aktualisierung)
        => Buffer(id, existing => existing with { IstKomplett = true, LetzteAktualisierung = aktualisierung });

    public Task SetKiEinzelbildKlassifikationAsync(
        Guid id, BildVersion version, Klassifikation bildLabel,
        IReadOnlyList<Klassifikation> regionLabels, DateTimeOffset aktualisierung)
        => Buffer(id, existing =>
        {
            var kiRegionen = regionLabels.Select(r => (Klassifikation?)r).ToArray();
            return version switch
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
        });

    public Task SetKiBildpaarKlassifikationAsync(Guid id, Klassifikation label, DateTimeOffset aktualisierung)
        => Buffer(id, existing => existing with { KiBildpaarKlassifikation = label, LetzteAktualisierung = aktualisierung });

    public Task SetMenschRegionLabelAsync(
        Guid id, BildVersion version, int regionIndex, Klassifikation label, DateTimeOffset aktualisierung)
        => Buffer(id, existing => version switch
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
        });

    public Task SetMenschEinzelbildLabelAsync(
        Guid id, BildVersion version, Klassifikation label, DateTimeOffset aktualisierung)
        => Buffer(id, existing => version switch
        {
            BildVersion.Dc0 => existing with { Dc0MenschBildLabel = label, HatMenschLabel = true, LetzteAktualisierung = aktualisierung },
            BildVersion.Dc2 => existing with { Dc2MenschBildLabel = label, HatMenschLabel = true, LetzteAktualisierung = aktualisierung },
            _ => existing
        });

    public Task SetMenschBildpaarLabelAsync(Guid id, Klassifikation label, DateTimeOffset aktualisierung)
        => Buffer(id, existing => existing with { MenschBildpaarLabel = label, HatMenschLabel = true, LetzteAktualisierung = aktualisierung });

    public Task SetPhysischesProduktLabelAsync(Guid id, Klassifikation label, DateTimeOffset aktualisierung)
        => Buffer(id, existing => existing with { PhysischesProduktLabel = label, HatProduktLabel = true, LetzteAktualisierung = aktualisierung });

    public Task SetInspiziertAsync(Guid id, DateTimeOffset aktualisierung)
        => Buffer(id, existing => existing with { IstInspiziert = true, LetzteAktualisierung = aktualisierung });

    /// <summary>Puffert ein read-modify-store: lädt im Batch (read-your-writes), transformiert, staged.</summary>
    private Task Buffer(Guid id, Func<ImagePairReadModel, ImagePairReadModel> transform)
    {
        _pending.Add(async s =>
        {
            var existing = await s.LoadAsync<ImagePairReadModel>(id);
            if (existing is null) return;   // Event vor dem erzeugenden Upsert → im nächsten Batch geheilt
            s.Store(transform(existing));
        });
        return Task.CompletedTask;
    }

    // ═══════════════════════════════════════════════════════════
    // Marke lesen: reiner Read, klammert den Batch-Beginn
    // ═══════════════════════════════════════════════════════════

    public async Task<int> LastProcessedVersionAsync(string projectionId, Guid streamId, CancellationToken ct)
    {
        _pending.Clear();   // neuer Batch → alten Vorlauf verwerfen
        await using var s = _store.QuerySession();
        var doc = await s.LoadAsync<ProjectionCheckpoint>(
            ProjectionCheckpoint.MakeId(projectionId, streamId), ct);
        return doc?.Version ?? -1;
    }

    // ═══════════════════════════════════════════════════════════
    // Commit-Punkt: Effekt(e) + Marke in EINER Session, ein SaveChanges
    // ═══════════════════════════════════════════════════════════

    public async Task MarkProcessedAsync(string projectionId, Guid streamId, int version, CancellationToken ct)
    {
        await using var s = _store.IdentitySession();   // read-your-writes im Batch
        foreach (var op in _pending)
            await op(s);

        s.Store(new ProjectionCheckpoint
        {
            Id = ProjectionCheckpoint.MakeId(projectionId, streamId),
            ProjectionId = projectionId,
            StreamId = streamId,
            Version = version,
            UpdatedAt = DateTimeOffset.UtcNow
        });

        await s.SaveChangesAsync(ct);   // EIN Commit → Effekt(e) + Marke gemeinsam gültig
        _pending.Clear();
    }

    // ─── Replay / Rebuild (eigene Session) ───

    public async Task ResetAsync(string projectionId, Guid streamId, CancellationToken ct)
    {
        await using var s = _store.LightweightSession();
        s.Delete<ProjectionCheckpoint>(ProjectionCheckpoint.MakeId(projectionId, streamId));
        await s.SaveChangesAsync(ct);
    }

    public async Task ResetAllAsync(string projectionId, CancellationToken ct)
    {
        await using var s = _store.LightweightSession();
        s.DeleteWhere<ProjectionCheckpoint>(c => c.ProjectionId == projectionId);
        await s.SaveChangesAsync(ct);
    }

    // ─── Hilfsmethode ───

    private static IReadOnlyList<Klassifikation?> UpdateRegionArray(
        IReadOnlyList<Klassifikation?> regionen, int index, Klassifikation label)
    {
        var arr = regionen.ToArray();
        arr[index] = label;
        return arr;
    }
}
