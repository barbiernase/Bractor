using System.Collections.Concurrent;
using Abstractions;

namespace Infrastructure.Testing;

/// <summary>Store-freier <see cref="IFristplan"/> für den Prüfstand (in-memory).</summary>
public sealed class InMemoryFristplan : IFristplan
{
    private readonly ConcurrentDictionary<Guid, Frist> _fristen = new();

    public Task PlaneAsync(Frist frist, CancellationToken ct = default)
    {
        _fristen[frist.Id] = frist;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<Frist>> FälligeAsync(DateTimeOffset jetzt, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<Frist>>(
            _fristen.Values.Where(f => f.Fällig <= jetzt).ToList());

    public Task EntferneAsync(Guid id, CancellationToken ct = default)
    {
        _fristen.TryRemove(id, out _);
        return Task.CompletedTask;
    }
}
