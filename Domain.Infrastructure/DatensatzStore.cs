using Abstractions;
using Domain.Datensatz;
using Domain.Projections;
using Marten;

namespace Domain.Infrastructure;

/// <summary>
/// Co-Commit-Store der Datensatz-Projektion (Domänen-Infra, nicht im Framework) — nach dem
/// Naht-Muster von <see cref="ImagePairStore"/> / <see cref="ImagePairHistorieStore"/>
/// (Spec 7.4, Fall 1).
///
/// Die Write-Operationen PUFFERN nur. <see cref="MarkProcessedAsync"/> ist der Commit-Punkt:
/// EINE Marten-<c>IdentitySession</c> spielt alle gepufferten Effekte in Reihenfolge ab
/// (read-your-writes über die Identity-Map) UND staged den <see cref="ProjectionCheckpoint"/>,
/// dann EIN <c>SaveChanges</c> → Effekt und Marke gemeinsam gültig (exactly-once). Nötig, weil
/// die Projektion <see cref="IAppendProjektion"/> ist (das Einfrieren schreibt neue Sample-Zeilen).
///
/// TRANSIENT registriert (frisch pro Stream-Actor → isolierter Puffer; der Single-Writer-Actor
/// serialisiert).
/// </summary>
public sealed class DatensatzStore
    : ICoCommitTracker, IDatensatzWriteStore
{
    private readonly IDocumentStore _store;
    private readonly List<Func<IDocumentSession, Task>> _pending = new();

    public DatensatzStore(IDocumentStore store) => _store = store;

    // ═══════════════════════════════════════════════════════════
    // Write-Seite: nur puffern, KEIN Commit (aufgeschoben auf MarkProcessedAsync)
    // ═══════════════════════════════════════════════════════════

    public Task UpsertAsync(DatensatzReadModel model)
    {
        _pending.Add(s => { s.Store(model); return Task.CompletedTask; });
        return Task.CompletedTask;
    }

    public Task NimmRangeAufAsync(
        Guid id, IReadOnlyList<Guid> imagePairIds, RangeHerkunft herkunft, DateTimeOffset aktualisierung)
        => Buffer(id, existing =>
        {
            var mitglieder = new List<Guid>(existing.Mitglieder);
            foreach (var pid in imagePairIds)
                if (!mitglieder.Contains(pid)) mitglieder.Add(pid);   // Union, dedup, Reihenfolge stabil
            var ranges = new List<RangeHerkunft>(existing.Ranges) { herkunft };
            return existing with
            {
                Mitglieder = mitglieder,
                AnzahlMitglieder = mitglieder.Count,
                Ranges = ranges,
                LetzteAktualisierung = aktualisierung
            };
        });

    public Task NimmPaarAufAsync(Guid id, Guid imagePairId, DateTimeOffset aktualisierung)
        => Buffer(id, existing =>
        {
            if (existing.Mitglieder.Contains(imagePairId)) return existing;
            var mitglieder = new List<Guid>(existing.Mitglieder) { imagePairId };
            return existing with
            {
                Mitglieder = mitglieder,
                AnzahlMitglieder = mitglieder.Count,
                LetzteAktualisierung = aktualisierung
            };
        });

    public Task EntfernePaarAsync(Guid id, Guid imagePairId, DateTimeOffset aktualisierung)
        => Buffer(id, existing =>
        {
            if (!existing.Mitglieder.Contains(imagePairId)) return existing;
            var mitglieder = new List<Guid>(existing.Mitglieder);
            mitglieder.Remove(imagePairId);
            return existing with
            {
                Mitglieder = mitglieder,
                AnzahlMitglieder = mitglieder.Count,
                LetzteAktualisierung = aktualisierung
            };
        });

    public Task SetzeSplitAsync(Guid id, SplitKonfig split, DateTimeOffset aktualisierung)
        => Buffer(id, existing => existing with { Split = split, LetzteAktualisierung = aktualisierung });

    public Task FriereEinAsync(
        Guid id, int version, IReadOnlyList<DatensatzMitglied> mitglieder, DateTimeOffset aktualisierung)
    {
        // 1) Datensatz-Kopf: Status + eingefrorene Version.
        _pending.Add(async s =>
        {
            var existing = await s.LoadAsync<DatensatzReadModel>(id);
            if (existing is null) return;
            s.Store(existing with
            {
                Status = DatensatzStatus.Eingefroren,
                EingefroreneVersion = version,
                LetzteAktualisierung = aktualisierung
            });
        });

        // 2) Je Mitglied eine Sample-Zeile (immutable Snapshot; idempotent über die zusammengesetzte Id).
        _pending.Add(s =>
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

    /// <summary>Puffert ein read-modify-store: lädt im Batch (read-your-writes), transformiert, staged.</summary>
    private Task Buffer(Guid id, Func<DatensatzReadModel, DatensatzReadModel> transform)
    {
        _pending.Add(async s =>
        {
            var existing = await s.LoadAsync<DatensatzReadModel>(id);
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
}
