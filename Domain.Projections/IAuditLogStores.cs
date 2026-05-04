using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Projections;

/// <summary>
/// Write-Zugriffsmuster für AuditLog-Einträge.
/// Optimiert für append-only Schreibvorgänge.
/// Wird vom Writer (AuditLog) verwendet.
/// </summary>
public interface IAuditLogWriteStore
{
    Task AppendAsync(AuditEntry entry);
}

/// <summary>
/// Read-Zugriffsmuster für AuditLog-Einträge.
/// Optimiert für Abfragen nach AggregateId und Gesamt-Übersicht.
/// Wird vom Reader (AuditLogReader) verwendet.
/// </summary>
public interface IAuditLogReadStore
{
    Task<IReadOnlyList<AuditEntry>> FindByAggregateIdAsync(Guid aggregateId);
    Task<IReadOnlyList<AuditEntry>> GetAllAsync();
}