using Abstractions;

namespace Infrastructure.Persistence;

/// <summary>
/// Ein Append-Auftrag an einen Stream, wie ihn der <see cref="BatchingEventAppender"/> sammelt: die Teile eines oder mehrerer Commands
/// (<see cref="AppendTeil"/>) mit ihren PRO-COMMAND-Metadaten (Correlation/Causation/Akteur/Spur), die pro Event gestempelt werden müssen,
/// weil die Prozess-Maschine über <c>CausationId == Vorgang</c> faltet und über <c>CorrelationId</c> routet — und ihren Beilagen (Quittung
/// des Commands für die Inbox, Eintrag und Ende eines Vorgangs, docs/03 §3.3), die in derselben Transaktion landen.
/// </summary>
public readonly record struct BatchAppend(Guid StreamId, int ExpectedVersion, IReadOnlyList<AppendTeil> Teile, string? AggregateType)
{
    public int EventAnzahl => Teile.Sum(t => t.Events.Count);
}

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
