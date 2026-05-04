using System.Collections.Concurrent;
using Domain.Projections;
using Domain.Todo;

namespace Domain.Infrastructure;

/// <summary>
/// In-Memory Implementierung für Todo Read- und Write-Store.
///
/// Implementiert beide Interfaces — intern dasselbe ConcurrentDictionary.
/// In Produktion wären das getrennte Klassen.
///
/// Als Singleton registrieren, damit Read- und Write-Seite dieselben Daten sehen.
/// </summary>
public class TodoStoreInMemory : ITodoWriteStore, ITodoReadStore
{
    private readonly ConcurrentDictionary<Guid, TodoReadModel> _data = new();

    // ── Write-Seite ──────────────────────────────────────

    public Task UpsertAsync(TodoReadModel model)
    {
        _data[model.Id] = model;
        return Task.CompletedTask;
    }

    public Task UpdateStatusAsync(Guid id, TodoStatus status, DateTimeOffset? erledigtAm)
    {
        _data.AddOrUpdate(id,
            _ => throw new InvalidOperationException($"Todo {id} nicht gefunden"),
            (_, existing) => existing with
            {
                Status = status,
                ErledigtAm = erledigtAm
            });
        return Task.CompletedTask;
    }

    public Task UpdateTitelAsync(Guid id, string neuerTitel)
    {
        _data.AddOrUpdate(id,
            _ => throw new InvalidOperationException($"Todo {id} nicht gefunden"),
            (_, existing) => existing with { Titel = neuerTitel });
        return Task.CompletedTask;
    }

    public Task UpdatePrioritaetAsync(Guid id, Prioritaet neuePrioritaet)
    {
        _data.AddOrUpdate(id,
            _ => throw new InvalidOperationException($"Todo {id} nicht gefunden"),
            (_, existing) => existing with { Prioritaet = neuePrioritaet });
        return Task.CompletedTask;
    }

    public Task AddTagAsync(Guid id, string tag)
    {
        _data.AddOrUpdate(id,
            _ => throw new InvalidOperationException($"Todo {id} nicht gefunden"),
            (_, existing) => existing with
            {
                Tags = existing.Tags.Append(tag).Distinct().ToList()
            });
        return Task.CompletedTask;
    }

    public Task RemoveTagAsync(Guid id, string tag)
    {
        _data.AddOrUpdate(id,
            _ => throw new InvalidOperationException($"Todo {id} nicht gefunden"),
            (_, existing) => existing with
            {
                Tags = existing.Tags.Where(t => t != tag).ToList()
            });
        return Task.CompletedTask;
    }

    public Task<TodoReadModel?> FindByIdAsync(Guid id)
    {
        _data.TryGetValue(id, out var model);
        return Task.FromResult(model);
    }

    // ── Read-Seite ───────────────────────────────────────

    Task<TodoReadModel?> ITodoReadStore.FindByIdAsync(Guid id)
        => FindByIdAsync(id);

    public Task<IReadOnlyList<TodoReadModel>> GetAllAsync()
    {
        IReadOnlyList<TodoReadModel> result = _data.Values.ToList();
        return Task.FromResult(result);
    }
}