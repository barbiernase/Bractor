using Abstractions;

namespace Domain.Sammelvorgang;

/// <summary>
/// Startet die Barriere mit der zur LAUFZEIT bekannten Anzahl N (= Anzahl der aufgefächerten Teile).
/// Der Fan-out selbst (die N Einzel-Commands an die Worker) macht der Client/die Pipeline; hier wird
/// nur das skalare „erwarte N" als Faktum festgehalten.
/// </summary>
public record StarteSammelvorgang(
    Guid AggregateId,
    int Anzahl
) : ICreationCommand;

/// <summary>
/// Ein Teil meldet sich fertig. Wird typischerweise von einer Reaktion gefeuert, die das
/// Fertig-Event eines Worker-Aggregats auf diesen Sammelvorgang (per Korrelation) abbildet.
/// Doppelte Zustellung desselben Teils wird von der Framework-Inbox dedupliziert (nicht hier).
/// </summary>
public record MeldeTeilFertig(
    Guid AggregateId
) : ICommand;
