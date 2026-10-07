namespace Abstractions;

/// <summary>
/// Bestätigung für Trigger-Sender.
/// Proto.Actor erfordert eine Antwort auf RequestAsync — sonst Retry.
/// </summary>
public record PipelineAck(bool Accepted = true) : IWireMessage;

/// <summary>
/// Weckt die EINE Cluster-Aktivierung einer Pipeline (<c>Pipeline-{PipelineId}</c>), damit sie auch ohne Trigger läuft
/// (Self-Ticks ab <see cref="PipelineGestartet"/>). Der <c>PipelineStartupService</c> jedes Nodes sendet sie periodisch —
/// idempotent: eine laufende Pipeline quittiert nur, eine verlorene (Node weg) wird woanders neu aktiviert.
/// </summary>
public sealed record PipelineAktivieren : IWireMessage;
