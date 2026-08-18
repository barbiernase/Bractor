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
    /// Das Read-Your-Writes-Ziel: die höchste DOMAIN-Event-Version, die der Client für dieses
    /// Aggregat aus dem Event-Push gesehen hat. Anders als <see cref="GetVersion"/> (Stream-Head
    /// inkl. Marke, für OCC) zählt hier NUR die materialisierbare Domain-Version — denn genau bis
    /// dorthin muss die (asynchrone Pull-)Projektion aufgeschlossen haben, damit ein Read frisch ist.
    /// null, wenn der Client kein Event für das Aggregat gesehen hat (nichts zu erwarten).
    /// </summary>
    int? GetReadTarget(Guid aggregateId);

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