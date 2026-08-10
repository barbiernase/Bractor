using System.Collections.Concurrent;
using Abstractions;

namespace Infrastructure.Testing;

/// <summary>
/// Store-freie DLQ für den Prüfstand: implementiert BEIDE Achsen — den schreibenden
/// <see cref="IDeadLetterSink"/> und den lesenden <see cref="IDeadLetterReadStore"/> — auf einer
/// In-Memory-Ablage. So lässt sich der Ops-Pfad (schreiben → listen/filtern/auflösen) ohne Marten prüfen.
/// </summary>
public sealed class InMemoryDeadLetterStore : IDeadLetterSink, IDeadLetterReadStore
{
    private readonly ConcurrentDictionary<Guid, DeadLetter> _eintraege = new();

    public Task WriteAsync(DeadLetter eintrag, CancellationToken ct = default)
    {
        _eintraege[eintrag.Id] = eintrag;
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<DeadLetter>> ListAsync(int limit, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DeadLetter>>(
            _eintraege.Values.OrderByDescending(d => d.ErfasstUtc).Take(limit).ToList());

    public Task<IReadOnlyList<DeadLetter>> ListByCorrelationAsync(string correlationId, CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<DeadLetter>>(
            _eintraege.Values.Where(d => d.CorrelationId == correlationId)
                             .OrderByDescending(d => d.ErfasstUtc).ToList());

    public Task<DeadLetter?> GetAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_eintraege.TryGetValue(id, out var d) ? d : null);

    public Task<long> CountAsync(CancellationToken ct = default)
        => Task.FromResult((long)_eintraege.Count);

    public Task<bool> ResolveAsync(Guid id, CancellationToken ct = default)
        => Task.FromResult(_eintraege.TryRemove(id, out _));
}
