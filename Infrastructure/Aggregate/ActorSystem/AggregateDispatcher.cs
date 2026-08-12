using Abstractions;
using Infrastructure.PubSub;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Proto.Cluster;

namespace Infrastructure.Aggregate.ActorSystem;

/// <summary>
/// Dispatcht Commands an Aggregate-Actors.
///
/// FIRE-AND-FORGET aus Sicht des Aufrufers: <see cref="Dispatch"/> kehrt sofort zurück.
/// Feedback kommt ausschließlich über Events (PubSub):
/// - Erfolg: Domain-Events (Broadcast)
/// - Fehler: CommandFailed Event (Targeted) — technische Actor-Fehler publiziert der Actor
///   selbst; ERREICHT der Command nach erschöpfter Platzierung nie einen Actor, publiziert
///   HIER der Dispatcher das CommandFailed (T2a), damit der auslösende Client nicht still hängt.
///
/// ★ Härtung: der eigentliche Send läuft mit BESCHRÄNKTEM Token + bounded Retry. Ein
/// <c>CancellationToken.None</c> ließ <c>RequestAsync</c> bei noch nicht konvergierter Cluster-
/// Topologie (Cold-Boot, PartitionIdentityLookup noch nicht gesetzt) UNENDLICH still retryen —
/// der Command hing dann für immer, ohne Log. Jetzt: pro Versuch ein bounded Token; bleibt die
/// Platzierung aus, wird der Versuch beendet und erneut probiert (die Topologie bekommt weitere
/// Chancen); nach allen Versuchen wird LAUT geloggt statt lautlos zu hängen.
/// </summary>
public class ProtoActorAggregateDispatcher : IAggregateDispatcher
{
    private readonly Proto.ActorSystem _actorSystem;
    private readonly ILogger _logger;
    private readonly IDeadLetterSink? _deadLetters;   // ★ Audit-Fix #1: nicht-zustellbaren Command durabel machen
    private readonly BrokerPublisher? _publisher;     // ★ T2a: CommandFailed an den Client bei erschöpfter Platzierung

    /// <summary>Timeout pro Sende-Versuch (begrenzt die interne Placement-Retry-Schleife von Proto.Cluster).</summary>
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Anzahl Versuche, bevor der Command als nicht-zustellbar geloggt und verworfen wird.</summary>
    private const int MaxAttempts = 3;

    public ProtoActorAggregateDispatcher(
        Proto.ActorSystem actorSystem,
        ILogger<ProtoActorAggregateDispatcher>? logger = null,
        IDeadLetterSink? deadLetters = null,
        BrokerPublisher? publisher = null)
    {
        _actorSystem = actorSystem;
        _logger = logger ?? NullLogger<ProtoActorAggregateDispatcher>.Instance;
        _deadLetters = deadLetters;
        _publisher = publisher;
    }

    /// <summary>
    /// Dispatcht einen Command an den zuständigen Aggregate-Actor.
    /// Fire-and-Forget: Kehrt sofort zurück, wartet nicht auf Verarbeitung.
    /// </summary>
    public void Dispatch(CommandEnvelope envelope)
    {
        _logger.LogDebug("[Dispatcher] Fire-and-forget: {Command} to {Type}/{Id}",
            envelope.Payload.GetType().Name, envelope.AggregateType, envelope.AggregateId);

        // Fire-and-Forget aus Aufrufersicht: der Send läuft im Hintergrund mit bounded Retry.
        _ = DispatchWithRetryAsync(envelope);
    }

    private async Task DispatchWithRetryAsync(CommandEnvelope envelope)
    {
        var identity = ClusterIdentity.Create(
            envelope.AggregateId.ToString(),
            envelope.AggregateType);

        var grund = "keine Antwort (Cluster nicht routbar)";   // ★ #1: letzter Fehlergrund für den Dead-Letter

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            try
            {
                using var cts = new CancellationTokenSource(AttemptTimeout);
                var result = await _actorSystem.Cluster()
                    .RequestAsync<CommandResult>(identity, envelope, cts.Token);

                // Nicht-null ⇒ der Actor hat den Command verarbeitet (Erfolg ODER fachliche Ablehnung —
                // das Feedback fließt über Events). In beiden Fällen ist er ZUGESTELLT → fertig.
                if (result != null)
                    return;

                grund = "keine Antwort (Cluster evtl. noch nicht routbar)";
                _logger.LogWarning(
                    "[Dispatcher] Keine Antwort für {Command} an {Type}/{Id} (Versuch {Attempt}/{Max}) — " +
                    "Cluster evtl. noch nicht routbar, erneuter Versuch.",
                    envelope.Payload.GetType().Name, envelope.AggregateType, envelope.AggregateId, attempt, MaxAttempts);
            }
            catch (OperationCanceledException)
            {
                grund = $"Timeout beim Platzieren nach {AttemptTimeout.TotalSeconds:0}s (Cluster-Topologie evtl. nicht gesetzt)";
                _logger.LogWarning(
                    "[Dispatcher] Timeout beim Platzieren von {Type}/{Id} (Versuch {Attempt}/{Max}) — " +
                    "Cluster-Topologie evtl. noch nicht gesetzt.",
                    envelope.AggregateType, envelope.AggregateId, attempt, MaxAttempts);
            }
            catch (Exception ex)
            {
                grund = $"{ex.GetType().Name}: {ex.Message}";
                _logger.LogWarning(ex,
                    "[Dispatcher] Fehler beim Senden von {Command} an {Type}/{Id} (Versuch {Attempt}/{Max}).",
                    envelope.Payload.GetType().Name, envelope.AggregateType, envelope.AggregateId, attempt, MaxAttempts);
            }
        }

        // Alle Versuche erschöpft: LAUT melden statt lautlos zu verschlucken. Der Aufrufer bekam
        // ohnehin kein synchrones Ergebnis (Fire-and-forget) — aber im Log ist es jetzt sichtbar.
        _logger.LogError(
            "[Dispatcher] {Command} an {Type}/{Id} nach {Max} Versuchen NICHT zugestellt — verworfen.",
            envelope.Payload.GetType().Name, envelope.AggregateType, envelope.AggregateId, MaxAttempts);

        // ★ Audit-Fix #1: Zusätzlich DURABEL machen. Ein LogError verschwindet in der Rotation; der
        //   Aufrufer (fire-and-forget) hat längst zurückgegeben, kein Konsument heilt eine NIE geloggte
        //   Erst-Zustellung (der Poll-Backstop heilt nur bereits geloggte Events, nicht diesen Command).
        //   Der Dead-Letter macht den Verlust beobachtbar + replay-bar — bewusst KEIN Auto-Retry: der
        //   Client-/OCC-Pfad hat keine Idempotenz-Dedup, ein blinder Retry könnte bei verlorener Quittung
        //   doppelt wirken (§5). Best-effort; WriteAsync wirft laut Vertrag nie in den Aufrufer zurück.
        await SchreibeDeadLetterAsync(envelope, grund);

        // ★ T2a: Zusätzlich den auslösenden Client benachrichtigen. Der Command hat nie einen Actor
        //   erreicht → NIEMAND publiziert sonst ein CommandFailed (das tut normal der Actor). Ohne dies
        //   hinge der Client still (kein Ack, kein Nack). Targeted über OriginSessionId, wie der Actor.
        await TryPublishCommandFailedAsync(envelope, grund);
    }

    /// <summary>
    /// Publiziert ein <see cref="CommandFailed"/> targeted an die <c>OriginSessionId</c> — spiegelt
    /// das Actor-Muster (<c>AggregateActorBase.TryPublishCommandFailedAsync</c>), für den Fall, dass
    /// der Command mangels routbarem Cluster nie einen Actor erreicht hat. Best-effort: ohne Publisher
    /// oder ohne OriginSessionId folgenlos; Fehler beim Publish werden nur geloggt, nie geworfen.
    /// </summary>
    private async Task TryPublishCommandFailedAsync(CommandEnvelope envelope, string grund)
    {
        var failed = BaueCommandFailed(envelope, grund, MaxAttempts);
        if (_publisher is null || failed is null)
            return;

        try
        {
            await _publisher.PublishAsync(failed);
            _logger.LogDebug("[Dispatcher] CommandFailed an '{Session}' publiziert (nicht zugestellt).",
                envelope.OriginSessionId);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Dispatcher] CommandFailed-Publish fehlgeschlagen für {Type}/{Id}.",
                envelope.AggregateType, envelope.AggregateId);
        }
    }

    /// <summary>
    /// Reine Konstruktion des targeted <see cref="CommandFailed"/>-EventEnvelopes für den
    /// Nicht-zugestellt-Fall (store-/cluster-frei, Ebene-1-testbar). Gibt <c>null</c> zurück, wenn keine
    /// <c>OriginSessionId</c> vorliegt — dann gibt es kein Ziel für die Targeted Delivery. Spiegelt
    /// die Feldbelegung von <c>AggregateActorBase.TryPublishCommandFailedAsync</c>.
    /// </summary>
    public static EventEnvelope? BaueCommandFailed(CommandEnvelope envelope, string grund, int maxAttempts)
    {
        if (string.IsNullOrEmpty(envelope.OriginSessionId))
            return null;

        return new EventEnvelope
        {
            AggregateId = envelope.AggregateId,
            AggregateVersion = 0,
            AggregateType = envelope.AggregateType,
            CorrelationId = envelope.CorrelationId,
            CausationId = envelope.CommandId.ToString(),
            UserId = envelope.UserId,
            Payload = new CommandFailed(
                envelope.Payload.GetType().Name,
                $"Nicht zugestellt nach {maxAttempts} Versuchen: {grund}",
                envelope.AggregateId.ToString()),
            TargetSubscriberId = envelope.OriginSessionId,
        };
    }

    private async Task SchreibeDeadLetterAsync(CommandEnvelope envelope, string grund)
    {
        if (_deadLetters is null) return;
        try
        {
            await _deadLetters.WriteAsync(new DeadLetter
            {
                Id = Guid.NewGuid(),
                Quelle = "aggregate-dispatcher",
                CommandType = envelope.Payload.GetType().Name,
                AggregateId = envelope.AggregateId,
                AggregateType = envelope.AggregateType,
                CorrelationId = envelope.CorrelationId,
                Grund = grund,
                Versuche = MaxAttempts,
                ErfasstUtc = DateTimeOffset.UtcNow,
            });
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "[Dispatcher] Dead-Letter-Write fehlgeschlagen für {Type}/{Id}.",
                envelope.AggregateType, envelope.AggregateId);
        }
    }
}
