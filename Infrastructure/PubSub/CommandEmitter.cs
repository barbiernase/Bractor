using Abstractions;
using Infrastructure.Aggregate;
using Infrastructure.Pipeline;   // GeneratedPipelines.CommandAggregateTypes
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Proto.Cluster;

namespace Infrastructure.PubSub;

/// <summary>
/// Das EINE Emit-Primitiv (EM-1). Genau hier — und nur hier — wird ein Command an ein Fremd-Aggregat
/// geschickt: deterministische CommandId (<see cref="EmitId"/> → W1: der Empfänger dedupliziert eine
/// wiederholte Zustellung), <see cref="CommandModus.Emittiert"/> (KEINE Version, §4.2) und ein
/// <b>bounded</b> Token (W2: kein <c>CancellationToken.None</c>, kein Infinit-Hang). At-least-once auf
/// dem Draht — eine verlorene/verspätete Zustellung heilt der nächste Re-Wake/Poll, exactly-once-wirksam
/// macht die Empfänger-Inbox.
///
/// Context-frei: der <see cref="Cluster"/> kommt aus dem ActorSystem, nicht aus einem per-Message-Context
/// → aus dem Pull-Adapter (Reaktion) UND aus dem Pipeline-Actor nutzbar. Bewusst best-effort: das
/// Ergebnis wird nicht beobachtet — Korrektheit hängt nie an der Quittung (Empfänger-Inbox + Re-Wake).
/// (Der Prozess-Treiber, der die Quittung für seine Zustandsmaschine BRAUCHT, folgt in P5.)
///
/// Der Ziel-Send ist ein <see cref="SendeSeam"/> — live der <c>cluster.RequestAsync</c>, im Prüfstand ein
/// Fake (W1/W2 in-memory beweisbar, ohne echten Cluster; <c>memory/hang-diagnose-in-memory.md</c>).
/// </summary>
public sealed class CommandEmitter : ICommandEmitter
{
    /// <summary>Der einzige Cluster-Berührungspunkt — live gebunden, im Test injiziert.</summary>
    public delegate Task<CommandResult?> SendeSeam(ClusterIdentity ziel, CommandEnvelope envelope, CancellationToken ct);

    private readonly SendeSeam _send;
    private readonly ILogger _logger;
    private readonly TimeSpan _sendeFrist;

    private static readonly TimeSpan StandardFrist = TimeSpan.FromSeconds(5);

    /// <summary>Live: bindet den Send an den Cluster.</summary>
    public CommandEmitter(Cluster cluster, ILogger? logger = null, TimeSpan? sendeFrist = null)
    {
        if (cluster is null) throw new ArgumentNullException(nameof(cluster));
        _send = (id, env, ct) => cluster.RequestAsync<CommandResult>(id, env, ct);
        _logger = logger ?? NullLogger.Instance;
        _sendeFrist = sendeFrist ?? StandardFrist;
    }

    /// <summary>Naht/Test: injizierter Send (Fake-Cluster in-memory).</summary>
    public CommandEmitter(SendeSeam send, ILogger? logger = null, TimeSpan? sendeFrist = null)
    {
        _send = send ?? throw new ArgumentNullException(nameof(send));
        _logger = logger ?? NullLogger.Instance;
        _sendeFrist = sendeFrist ?? StandardFrist;
    }

    public Task EmitAsync(ICommand cmd, EmitKausalität k, CancellationToken ct)
        => SendeAsync(cmd, EmitId.Ableiten(k, cmd.AggregateId), k.Korrelation, ct);

    /// <summary>
    /// EM-1-Überladung für den Prozess-Manager (Treiber-Fold, §5 Weg a): eine VORGEGEBENE CommandId — der
    /// deterministische <c>vorgang</c> (aus <c>ProzessId.FürTransition</c>/<c>FürKompensation</c>). So bleibt
    /// der Fold-Match (Ziel-Event/-Marke mit <c>CausationId == vorgang</c>) unverändert; der Manager braucht
    /// KEINE Quittung mehr, sondern faltet den durablen Ausgang (Wirkung/KommandoVerarbeitet/KommandoAbgelehnt).
    /// Die Korrelation reist explizit (→ Ziel-Event-Metadatum, P5-Poll-Routing). Sonst identisch: bounded (W2),
    /// <see cref="CommandModus.Emittiert"/> (KEINE Version, §4.2), fire-and-forget (Empfänger-Inbox dedupliziert).
    /// </summary>
    public Task EmitAsync(ICommand cmd, Guid commandId, Guid korrelation, CancellationToken ct)
        => SendeAsync(cmd, commandId, korrelation, ct);

    private async Task SendeAsync(ICommand cmd, Guid commandId, Guid korrelation, CancellationToken ct)
    {
        if (!GeneratedPipelines.CommandAggregateTypes.TryGetValue(cmd.GetType(), out var aggregateType))
        {
            _logger.LogError("[Emit] keine AggregateType-Zuordnung für {Command} — nicht routbar", cmd.GetType().Name);
            return;
        }

        var envelope = new CommandEnvelope
        {
            CommandId = commandId,                              // deterministisch → Empfänger-Inbox dedupliziert (W1)
            AggregateId = cmd.AggregateId,
            AggregateType = aggregateType,
            Modus = new CommandModus.Emittiert(),               // KEINE Version (§4.2)
            CorrelationId = korrelation.ToString(),             // → Ziel-Event-Metadatum (P5-Poll-Routing)
            Payload = cmd,
        };
        var identity = ClusterIdentity.Create(cmd.AggregateId.ToString(), aggregateType);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_sendeFrist);                           // bounded (W2)
        try
        {
            await _send(identity, envelope, cts.Token);
        }
        catch (OperationCanceledException)
        {
            // at-least-once: kein Retry hier — der nächste Re-Wake/Poll heilt (Invariante 2).
            _logger.LogDebug("[Emit] Timeout an {AggregateType}/{AggregateId} — Re-Wake/Poll heilt",
                aggregateType, cmd.AggregateId);
        }
    }
}
