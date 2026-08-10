using Abstractions;
using Marten;

namespace Infrastructure.Persistence;

/// <summary>
/// Marten-basierter <see cref="IDeadLetterReadStore"/>: liest/löscht <see cref="DeadLetter"/>-Dokumente
/// (Schema „dlq", von <see cref="MartenDeadLetterSink"/> geschrieben). Eigene, kurze Sessions je Zugriff.
/// </summary>
public sealed class MartenDeadLetterReadStore : IDeadLetterReadStore
{
    private readonly IDocumentStore _store;

    public MartenDeadLetterReadStore(IDocumentStore store)
        => _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<IReadOnlyList<DeadLetter>> ListAsync(int limit, CancellationToken ct = default)
    {
        await using var session = _store.QuerySession();
        return await session.Query<DeadLetter>()
            .OrderByDescending(d => d.ErfasstUtc)
            .Take(limit)
            .ToListAsync(ct);
    }

    public async Task<IReadOnlyList<DeadLetter>> ListByCorrelationAsync(string correlationId, CancellationToken ct = default)
    {
        await using var session = _store.QuerySession();
        return await session.Query<DeadLetter>()
            .Where(d => d.CorrelationId == correlationId)
            .OrderByDescending(d => d.ErfasstUtc)
            .ToListAsync(ct);
    }

    public async Task<DeadLetter?> GetAsync(Guid id, CancellationToken ct = default)
    {
        await using var session = _store.QuerySession();
        return await session.LoadAsync<DeadLetter>(id, ct);
    }

    public async Task<long> CountAsync(CancellationToken ct = default)
    {
        await using var session = _store.QuerySession();
        return await session.Query<DeadLetter>().LongCountAsync(ct);
    }

    public async Task<bool> ResolveAsync(Guid id, CancellationToken ct = default)
    {
        await using var session = _store.LightweightSession();
        var doc = await session.LoadAsync<DeadLetter>(id, ct);
        if (doc is null) return false;
        session.Delete<DeadLetter>(id);
        await session.SaveChangesAsync(ct);
        return true;
    }
}
