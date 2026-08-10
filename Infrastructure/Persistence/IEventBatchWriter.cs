using Abstractions;

namespace Infrastructure.Persistence;

/// <summary>
/// Ein einzelner Append-Auftrag, wie ihn der <see cref="BatchingEventAppender"/> sammelt.
/// Trägt exakt die Argumente von <see cref="IEventStoreRepository.AppendEventsAsync"/> — inkl. der
/// PRO-COMMAND-Metadaten (Correlation/Causation/Type), die pro Event gestempelt werden müssen, weil
/// die Prozess-Maschine über <c>CausationId == Vorgang</c> faltet und über <c>CorrelationId</c> routet.
/// </summary>
public readonly record struct BatchAppend(
    Guid StreamId,
    int ExpectedVersion,
    IReadOnlyList<IEvent> Events,
    string? CorrelationId,
    string? CausationId,
    string? AggregateType);

/// <summary>
/// Schreibt eine ganze Menge von Append-Aufträgen ALL-ODER-NICHTS in EINER nativen Transaktion
/// (Group-Commit). Wirft bei irgendeinem Fehler — der Aufrufer (<see cref="BatchingEventAppender"/>)
/// isoliert dann pro Auftrag über den Einzel-Append.
///
/// Der Vertrag ist bewusst getrennt vom Store, damit die Batch-Zustandsmaschine
/// (Queue/Drain/Isolation) store-agnostisch in-memory prüfbar bleibt und nur die konkrete
/// Marten-Bündelung austauschbar ist.
/// </summary>
public interface IEventBatchWriter
{
    Task WriteBatchAsync(IReadOnlyList<BatchAppend> batch, CancellationToken ct);
}
