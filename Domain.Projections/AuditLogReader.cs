using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abstractions;

namespace Domain.Projections;

/// <summary>
/// Reader für AuditLog-Queries — eigenständige Top-Level-Klasse.
///
/// DI: IAuditLogReadStore — entkoppelt von der Write-Seite.
/// Kein [ProjectionReader]-Attribut → TrackDeps = false (AuditLog trackt keine Deps).
/// </summary>
public partial class AuditLogReader : IReader<AuditLog>
{
    private readonly IAuditLogReadStore _store;

    public AuditLogReader(IAuditLogReadStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Audit-Einträge für ein bestimmtes Aggregat abfragen.
    /// Rückgabe: Liste ODER KeineGefunden — in der Signatur sichtbar.
    /// </summary>
    public async Task<OneOf<AuditEintraegeListe, KeineAuditEintraegeGefunden>>
        Handle(GetAuditEintraege query, IMessageEnvelope envelope, ReadContext ctx)
    {
        var entries = await _store.FindByAggregateIdAsync(query.AggregateId);

        if (entries.Count == 0)
            return new KeineAuditEintraegeGefunden(query.AggregateId);

        var items = entries.Select(e => new AuditEintragAntwort(
            e.Timestamp, e.AggregateId, e.AggregateType,
            e.UserId, e.Action, e.Details)).ToList();

        return new AuditEintraegeListe(items);
    }

    /// <summary>
    /// Alle Audit-Einträge abfragen.
    /// Nur ein mögliches Outcome → kein OneOf nötig.
    /// </summary>
    public async Task<AuditEintraegeListe>
        Handle(GetAlleAuditEintraege query, IMessageEnvelope envelope, ReadContext ctx)
    {
        var entries = await _store.GetAllAsync();

        var items = entries.Select(e => new AuditEintragAntwort(
            e.Timestamp, e.AggregateId, e.AggregateType,
            e.UserId, e.Action, e.Details)).ToList();

        return new AuditEintraegeListe(items);
    }
}