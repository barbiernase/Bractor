using System.Collections.Concurrent;
using Domain.Projections;

namespace Domain.Infrastructure;

/// <summary>
/// In-Memory Implementierung für AuditLog Read- und Write-Store.
/// 
/// Implementiert beide Interfaces — intern dieselbe ConcurrentQueue.
/// In Produktion: Writer macht Appends in eine Tabelle,
/// Reader macht indizierte Queries (z.B. nach AggregateId).
/// 
/// Als Singleton registrieren.
/// </summary>
public class InMemoryAuditLogStore : IAuditLogWriteStore, IAuditLogReadStore
{
    private readonly ConcurrentQueue<AuditEntry> _entries = new();

    // ── Write-Seite ──────────────────────────────────────

    public Task AppendAsync(AuditEntry entry)
    {
        _entries.Enqueue(entry);
        return Task.CompletedTask;
    }

    // ── Read-Seite ───────────────────────────────────────

    public Task<IReadOnlyList<AuditEntry>> FindByAggregateIdAsync(Guid aggregateId)
    {
        IReadOnlyList<AuditEntry> result = _entries
            .Where(e => e.AggregateId == aggregateId)
            .ToList();
        return Task.FromResult(result);
    }

    public Task<IReadOnlyList<AuditEntry>> GetAllAsync()
    {
        IReadOnlyList<AuditEntry> result = _entries.ToList();
        return Task.FromResult(result);
    }
}