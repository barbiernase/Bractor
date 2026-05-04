using System;
using System.Collections.Generic;
using Abstractions;

namespace Domain.Projections;

public record AuditEintragAntwort(
    DateTimeOffset Timestamp,
    Guid AggregateId,
    string AggregateType,
    string UserId,
    string Action,
    string Details) : IQueryResponse;

public record AuditEintraegeListe(
    IReadOnlyList<AuditEintragAntwort> Items) : IQueryResponse;

public record KeineAuditEintraegeGefunden(Guid AggregateId) : IQueryResponse;