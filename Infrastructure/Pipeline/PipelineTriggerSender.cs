using Abstractions;
using Microsoft.Extensions.Logging;
using Proto.Cluster;

namespace Infrastructure.Pipeline;

/// <summary>
/// Die eine pipeline→pipeline-Trigger-Route (Routing via <c>GeneratedPipelines.TriggerToPipelineId</c>,
/// bounded/W2). Herausgezogen aus <see cref="PipelineActorBase"/>, damit BEIDE Sender sie nutzen: der
/// Pipeline-Actor (Trigger-/Self-Kanal) UND der Pull-Adapter des Event-Pfads (P6.2 —
/// <see cref="PipelineEventPullBridge"/>), falls ein Event-Handler einen Trigger yieldet.
///
/// Context-frei: der <see cref="Cluster"/> kommt aus dem ActorSystem, nicht aus einem per-Message-Context.
/// At-least-once: ein Timeout heilt der nächste Auslöser/Re-Wake (Invariante 2).
/// </summary>
public static class PipelineTriggerSender
{
    /// <summary>Bounded-Frist für Pipeline→Pipeline-Trigger-Sends (W2: kein Infinit-Hang).</summary>
    public static readonly TimeSpan Frist = TimeSpan.FromSeconds(5);

    public static async Task SendAsync(Cluster cluster, IPipelineTrigger trigger, ILogger? logger = null)
    {
        var triggerType = trigger.GetType();

        if (!GeneratedPipelines.TriggerToPipelineId.TryGetValue(triggerType, out var targetPipelineId))
        {
            logger?.LogError("[PipelineTrigger] Keine TriggerToPipelineId-Zuordnung für {TriggerType}", triggerType.Name);
            return;
        }

        var identity = ClusterIdentity.Create(targetPipelineId, $"Pipeline-{targetPipelineId}");

        using var cts = new CancellationTokenSource(Frist);
        PipelineAck? ack;
        try
        {
            ack = await cluster.RequestAsync<PipelineAck>(identity, trigger, cts.Token);
        }
        catch (OperationCanceledException)
        {
            logger?.LogWarning("[PipelineTrigger] {TriggerType} → {Target} Timeout ({Frist}s) — Re-Wake heilt",
                triggerType.Name, targetPipelineId, Frist.TotalSeconds);
            return;
        }

        if (ack?.Accepted == true)
            logger?.LogDebug("[PipelineTrigger] ✔ {TriggerType} → {Target}", triggerType.Name, targetPipelineId);
        else
            logger?.LogWarning("[PipelineTrigger] {TriggerType} → {Target} nicht akzeptiert", triggerType.Name, targetPipelineId);
    }
}
