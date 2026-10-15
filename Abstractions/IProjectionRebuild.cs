namespace Abstractions;

/// <summary>
/// Implementiert vom Store/Ziel einer Projektion. Erlaubt dem Neuaufbau (<c>ProjectionRebuilder</c>), das Ziel vor dem Neuaufbau zu leeren.
///
/// Das Leeren ist eine STORE-Operation, kein Framework-Konzept (docs/04 §4.3, Neuaufbau). Grundsatz: es wird NIE in einen befüllten Zustand
/// replayt — erst leeren (hier), dann die Partitionen zurücksetzen (<see cref="IProjectionTracker.ResetAllAsync"/>), dann ab 0 lesen.
/// </summary>
public interface IRebuildableProjection
{
    /// <summary>Leert das gesamte Ziel dieser Projektion.</summary>
    Task ClearAsync(CancellationToken ct);
}
