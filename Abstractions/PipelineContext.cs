namespace Abstractions;

/// <summary>
/// Kontext für einen Pipeline-Handler-Aufruf.
///
/// Bei Trigger-Input: Nur CorrelationId gesetzt, kein Aggregate-Kontext.
/// Bei Event-Input: Voller Aggregate-Kontext aus dem IAggregateEnvelope.
/// Bei Self-Message: Kontext aus dem ursprünglichen ScheduleSelf-Aufruf.
///
/// Analog zu MessageContext (Client) und WriteContext (Subscriber).
///
/// ScheduleSelf und CancelScheduled sind virtuelle Stubs.
/// PipelineActorBase überschreibt sie mit echter Implementierung
/// (ReenterAfter + Actor-Mailbox).
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
    // Self-Messaging — Virtuelle Stubs, von PipelineActorBase überschrieben
    // ═══════════════════════════════════════════════════════

    /// <summary>
    /// Plant eine Self-Message für die eigene Pipeline-Mailbox.
    /// Mailbox-safe: nutzt Proto.Actor ReenterAfter + Task.Delay.
    ///
    /// Optional mit Token: gleiches Token → vorheriges Schedule wird ersetzt.
    /// ScheduleSelf(payload, TimeSpan.Zero) ist äquivalent zu SendSelf (sofort).
    ///
    /// Default-Implementierung ist no-op (für Tests und Init-Context ohne Actor).
    /// </summary>
    public virtual void ScheduleSelf<T>(T payload, TimeSpan delay, string? token = null)
        where T : IPipelineSelfMessage
    { }

    /// <summary>
    /// Bricht ein geplantes Schedule mit dem angegebenen Token ab.
    /// Gibt true zurück wenn ein Schedule gefunden und abgebrochen wurde.
    ///
    /// Default-Implementierung gibt immer false zurück.
    /// </summary>
    public virtual bool CancelScheduled(string token) => false;
}