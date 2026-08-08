using Abstractions;

namespace Core;

/// <summary>
/// Optionale Naht: nimmt nach jedem Write-Scope die gesammelten
/// <see cref="WriteResult"/> entgegen und veröffentlicht den daraus abgeleiteten
/// (nicht-autoritativen, verlierbaren) Versions-Index — heute Redis.
///
/// Bewusst store-agnostisch und optional: Fehlt eine Implementierung, entfällt der
/// Index folgenlos (er ist abgeleitet, keine Wahrheit). Der Adapter ruft sie mit der
/// kausalen Position aus dem <see cref="WriteContext"/> — dieselbe Quelle wie die
/// Fortschrittsmarke, nur an einen anderen (verlierbaren) Ort.
/// </summary>
public interface IReadModelDepsSink
{
    Task PublishAsync(
        string subscriberId,
        EventEnvelope envelope,
        IReadOnlyList<WriteResult> results,
        CancellationToken ct = default);
}
