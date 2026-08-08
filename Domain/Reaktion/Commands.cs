using Abstractions;

namespace Domain.Reaktion;

/// <summary>
/// Reaktions-Command an den Empfänger. <see cref="ReaktionId"/> ist deterministisch
/// aus dem auslösenden Event abgeleitet (ReaktionsId.For) — dadurch ist ein wiederholt
/// gesendeter Command als Duplikat erkennbar und verpufft beim Empfänger.
///
/// Regulärer ICommand: der sendende Adapter/Treiber nutzt OCC-Retry über
/// CommandResult.NewVersion; die Wirksamkeit sichert der Noop-Decider.
/// </summary>
public record WirkeReaktion(
    Guid AggregateId,
    Guid ReaktionId,
    string Notiz
) : ICommand;
