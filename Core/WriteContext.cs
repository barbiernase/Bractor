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

    /// <summary>
    /// Kausale Stream-Identität des auslösenden Events (= AggregateId).
    /// Der Store kann sie als Schlüssel/Bedingung nutzen, ohne das Envelope zu kennen.
    /// </summary>
    public Guid StreamId { get; }

    /// <summary>
    /// Kausale Position im Log (= AggregateVersion des auslösenden Events).
    /// Dieselbe Zahl, aus der die Fortschrittsmarke fällt (Spec 5.3, monoton pro Stream).
    /// -1, wenn ohne Positions-Kontext erzeugt (Push-Pfad / Alt-Aufrufer).
    /// </summary>
    public int Version { get; }

    public WriteContext(string readModelId, Guid streamId = default, int version = -1)
    {
        ReadModelId = readModelId ?? throw new ArgumentNullException(nameof(readModelId));
        StreamId = streamId;
        Version = version;
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