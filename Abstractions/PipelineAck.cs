namespace Abstractions;

/// <summary>
/// Bestätigung für Trigger-Sender.
/// Proto.Actor erfordert eine Antwort auf RequestAsync — sonst Retry.
/// </summary>
public record PipelineAck(bool Accepted = true);