namespace Client.Infrastructure.Versioning;

/// <summary>
/// Tracked Aggregate-Versionen für Optimistic Concurrency.
///
/// ConnectionModule nutzt GetVersion() um ExpectedVersion für Commands zu setzen.
/// VersioningModule (Phase 8) implementiert dies und subscribt auf Server-Events.
/// </summary>
public interface IVersioningModule
{
    /// <summary>
    /// Gibt die letzte bekannte Version eines Aggregats zurück.
    /// null wenn das Aggregat dem Client nicht bekannt ist.
    /// </summary>
    int? GetVersion(Guid aggregateId);

    /// <summary>
    /// Trackt Aggregate-Versionen aus Query-Response-Dependencies.
    /// Wird von der QueryBridge aufgerufen.
    /// </summary>
    void TrackFromDeps(IEnumerable<AggregateDep> deps);
}

/// <summary>
/// Aggregate-Abhängigkeit aus einer Query-Response.
/// Korrespondiert zu Abstractions.AggregateMeta auf dem Server.
/// </summary>
public record AggregateDep(Guid Id, string AggregateType, int Version);