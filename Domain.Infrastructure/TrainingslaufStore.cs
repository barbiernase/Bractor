using Abstractions;
using Domain.Projections;
using Domain.Trainingslauf;
using Marten;

namespace Domain.Infrastructure;

/// <summary>
/// Co-Commit-Store der Trainingslauf-Projektion (Domänen-Infra) — Naht-Muster wie
/// <see cref="DatensatzStore"/>. Write-Ops PUFFERN nur; <see cref="MarkProcessedAsync"/>
/// spielt sie + den <see cref="ProjectionCheckpoint"/> in EINER Session ab (ein SaveChanges →
/// exactly-once). Nötig, weil die Projektion <see cref="IAppendProjektion"/> ist (die
/// MetrikHistorie wächst je Epoche). TRANSIENT registriert.
/// </summary>
public sealed class TrainingslaufStore
    : ICoCommitTracker, ITrainingslaufWriteStore
{
    private readonly IDocumentStore _store;
    private readonly List<Func<IDocumentSession, Task>> _pending = new();

    public TrainingslaufStore(IDocumentStore store) => _store = store;

    // ─── Write-Seite: nur puffern ───

    public Task UpsertAsync(TrainingslaufReadModel model)
    {
        _pending.Add(s => { s.Store(model); return Task.CompletedTask; });
        return Task.CompletedTask;
    }

    public Task SetBegonnenAsync(Guid id, DateTimeOffset startzeit)
        => Buffer(id, existing => existing with
        {
            Status = TrainingsStatus.Laeuft,
            Startzeit = startzeit,
            LetzteAktualisierung = startzeit
        });

    public Task AppendMetrikAsync(Guid id, EpochenMetrik metrik, DateTimeOffset aktualisierung)
        => Buffer(id, existing => existing with
        {
            MetrikHistorie = new List<EpochenMetrik>(existing.MetrikHistorie) { metrik },
            AktuelleEpoche = metrik.Epoche,
            LetzteAktualisierung = aktualisierung
        });

    public Task SetAbgeschlossenAsync(Guid id, string modellPfad, Endmetriken endmetriken, DateTimeOffset aktualisierung)
        => Buffer(id, existing => existing with
        {
            Status = TrainingsStatus.Abgeschlossen,
            ModellPfad = modellPfad,
            Endmetriken = endmetriken,
            LetzteAktualisierung = aktualisierung
        });

    public Task SetGescheitertAsync(Guid id, string grund, DateTimeOffset aktualisierung)
        => Buffer(id, existing => existing with
        {
            Status = TrainingsStatus.Gescheitert,
            Fehlergrund = grund,
            LetzteAktualisierung = aktualisierung
        });

    public Task SetStatusAsync(Guid id, TrainingsStatus status, DateTimeOffset aktualisierung)
        => Buffer(id, existing => existing with
        {
            Status = status,
            LetzteAktualisierung = aktualisierung
        });

    private Task Buffer(Guid id, Func<TrainingslaufReadModel, TrainingslaufReadModel> transform)
    {
        _pending.Add(async s =>
        {
            var existing = await s.LoadAsync<TrainingslaufReadModel>(id);
            if (existing is null) return;   // Event vor dem erzeugenden Upsert → im nächsten Batch geheilt
            s.Store(transform(existing));
        });
        return Task.CompletedTask;
    }

    // ─── Marke lesen (klammert Batch-Beginn) ───

    public async Task<int> LastProcessedVersionAsync(string projectionId, Guid streamId, CancellationToken ct)
    {
        _pending.Clear();
        await using var s = _store.QuerySession();
        var doc = await s.LoadAsync<ProjectionCheckpoint>(
            ProjectionCheckpoint.MakeId(projectionId, streamId), ct);
        return doc?.Version ?? -1;
    }

    // ─── Commit-Punkt: Effekt(e) + Marke in EINER Session ───

    public async Task MarkProcessedAsync(string projectionId, Guid streamId, int version, CancellationToken ct)
    {
        await using var s = _store.IdentitySession();
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

        await s.SaveChangesAsync(ct);
        _pending.Clear();
    }

    // ─── Replay / Rebuild ───

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
