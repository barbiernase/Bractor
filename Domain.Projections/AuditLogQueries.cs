using System;
using Abstractions;

namespace Domain.Projections;

public record GetAuditEintraege(Guid AggregateId) : IQuery;
public record GetAlleAuditEintraege() : IQuery;