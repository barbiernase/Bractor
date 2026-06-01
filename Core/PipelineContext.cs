/*namespace Abstractions;

/// <summary>
/// Kontext für einen Pipeline-Handler-Aufruf.
///
/// Bei Trigger-Input: Nur CorrelationId gesetzt, kein Aggregate-Kontext.
/// Bei Event-Input: Voller Aggregate-Kontext aus dem IAggregateEnvelope.
/// Bei Self-Message: Kontext aus dem ursprünglichen ScheduleSelf-Aufruf.
///
/// Analog zu MessageContext (Client) und WriteContext (Subscriber).
/// </summary>
public class PipelineContext
{
    /// <summary>
    /// Korrelations-ID für Tracing. Wird an alle erzeugten Commands propagiert.
    /// Bei Trigger-Input: neue GUID. Bei Event-Input: aus Envelope übernommen.
    /// </summary>
    public string CorrelationId { get; init; } = "";

    /// <summary>Nur gesetzt bei Event-Input (PubSub).</summary>
    public Guid? SourceAggregateId { get; init; }

    /// <summary>Nur gesetzt bei Event-Input (PubSub).</summary>
    public string? SourceAggregateType { get; init; }

    /// <summary>Nur gesetzt bei Event-Input (PubSub).</summary>
    public int? SourceAggregateVersion { get; init; }

    // ═══════════════════════════════════════════════════════
    // Self-Messaging — Wird im Actor mit echter Implementierung verdrahtet
    // ═══════════════════════════════════════════════════════

    internal Action<IPipelineSelfMessage, TimeSpan, string?>? _scheduleSelf;
    internal Func<string, bool>? _cancelScheduled;

    /// <summary>
    /// Plant eine Self-Message für die eigene Pipeline-Mailbox.
    /// Mailbox-safe: nutzt Proto.Actor ReenterAfter + Task.Delay.
    ///
    /// Optional mit Token: gleiches Token → vorheriges Schedule wird ersetzt.
    /// ScheduleSelf(payload, TimeSpan.Zero) ist äquivalent zu SendSelf (sofort).
    /// </summary>
    public void ScheduleSelf<T>(T payload, TimeSpan delay, string? token = null)
        where T : IPipelineSelfMessage
        => _scheduleSelf?.Invoke(payload, delay, token);

    /// <summary>
    /// Bricht ein geplantes Schedule mit dem angegebenen Token ab.
    /// Gibt true zurück wenn ein Schedule gefunden und abgebrochen wurde.
    /// </summary>
    public bool CancelScheduled(string token)
        => _cancelScheduled?.Invoke(token) ?? false;
}*/