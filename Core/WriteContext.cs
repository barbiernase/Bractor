using Abstractions;

namespace Core;

/// <summary>
/// Kontext für einen einzelnen ReadModel-Write-Scope.
/// 
/// Wird von ProjectionWriter.Execute() erstellt und an die Entwickler-Lambda übergeben.
/// Der Entwickler registriert Aggregate-Abhängigkeiten via Track&lt;T&gt;().
/// Das Framework liest die gesammelten Tracks nach der Lambda-Ausführung aus.
/// </summary>
public class WriteContext
{
    private readonly List<TrackedAggregate> _trackedAggregates = new();

    /// <summary>
    /// Die ReadModel-ID dieses Scopes.
    /// Vom Entwickler in writer.Execute(readModelId, ...) gesetzt.
    /// </summary>
    public string ReadModelId { get; }

    public WriteContext(string readModelId)
    {
        ReadModelId = readModelId ?? throw new ArgumentNullException(nameof(readModelId));
    }

    /// <summary>
    /// Registriert eine Aggregate-Abhängigkeit.
    /// "Dieses ReadModel hängt von diesem Aggregat ab."
    /// 
    /// Void statt Ref-Return: der Intent ist Tracking, nicht Value-Erzeugung.
    /// </summary>
    public void Track<TAggregate>(Guid id) where TAggregate : IState
    {
        _trackedAggregates.Add(new TrackedAggregate(typeof(TAggregate), id));
    }

    /// <summary>
    /// Gesammelte Aggregate-Abhängigkeiten (Framework-intern).
    /// </summary>
    internal IReadOnlyList<TrackedAggregate> TrackedAggregates => _trackedAggregates;
}

/// <summary>
/// Ein registriertes Aggregate-Tracking aus einem WriteContext.
/// </summary>
/// <param name="AggregateType">Der CLR-Typ des Aggregats (z.B. typeof(Lagerartikel))</param>
/// <param name="Id">Die Aggregate-ID</param>
public record TrackedAggregate(Type AggregateType, Guid Id);