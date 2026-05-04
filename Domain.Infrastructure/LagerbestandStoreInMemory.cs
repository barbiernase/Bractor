using System.Collections.Concurrent;
using Domain.Projections;

namespace Domain.Infrastructure;

/// <summary>
/// In-Memory Implementierung für Lagerbestand Read- und Write-Store.
/// 
/// Implementiert beide Interfaces — intern dasselbe ConcurrentDictionary.
/// In Produktion wären das getrennte Klassen: der Writer macht Upserts gegen Postgres,
/// der Reader macht Queries gegen einen Cache oder materialisierte Views.
/// 
/// Als Singleton registrieren, damit Read- und Write-Seite dieselben Daten sehen.
/// </summary>
public class LagerbestandStoreInMemory : ILagerbestandWriteStore, ILagerbestandReadStore
{
    private readonly ConcurrentDictionary<Guid, LagerbestandReadModel> _data = new();

    // ── Write-Seite ──────────────────────────────────────

    public Task UpsertAsync(LagerbestandReadModel model)
    {
        _data[model.Id] = model;
        return Task.CompletedTask;
    }

    public Task AddToBestandAsync(Guid id, int anzahl, DateTimeOffset aktualisierung)
    {
        _data.AddOrUpdate(id,
            _ => throw new InvalidOperationException($"Lagerartikel {id} nicht gefunden"),
            (_, existing) => existing with
            {
                Anzahl = existing.Anzahl + anzahl,
                LetzteAktualisierung = aktualisierung
            });
        return Task.CompletedTask;
    }

    public Task SubtractFromBestandAsync(Guid id, int anzahl, DateTimeOffset aktualisierung)
    {
        _data.AddOrUpdate(id,
            _ => throw new InvalidOperationException($"Lagerartikel {id} nicht gefunden"),
            (_, existing) => existing with
            {
                Anzahl = existing.Anzahl - anzahl,
                LetzteAktualisierung = aktualisierung
            });
        return Task.CompletedTask;
    }

    public Task<int?> GetBestandAsync(Guid id)
    {
        if (_data.TryGetValue(id, out var model))
            return Task.FromResult<int?>(model.Anzahl);
        return Task.FromResult<int?>(null);
    }

    // ── Read-Seite ───────────────────────────────────────

    public Task<LagerbestandReadModel?> FindByIdAsync(Guid id)
    {
        _data.TryGetValue(id, out var model);
        return Task.FromResult(model);
    }

    public Task<IReadOnlyList<LagerbestandReadModel>> GetAllAsync()
    {
        IReadOnlyList<LagerbestandReadModel> result = _data.Values.ToList();
        return Task.FromResult(result);
    }
}