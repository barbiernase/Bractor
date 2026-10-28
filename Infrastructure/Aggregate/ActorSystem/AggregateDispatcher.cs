using Abstractions;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Proto.Cluster;

namespace Infrastructure.Aggregate.ActorSystem;

/// <summary>
/// Dispatcht Commands an Aggregate-Actors.
///
/// FIRE-AND-FORGET aus Sicht des Aufrufers: <see cref="Dispatch"/> kehrt sofort zurück.
/// Feedback kommt ausschließlich über Events an die Verteiler:
/// - Erfolg: Domain-Events (an jede Sitzung, die sie hört)
/// - Fehler: CommandFailed Event (gezielt an die Sitzung) — technische Actor-Fehler publiziert der Actor
///   selbst; ERREICHT der Command nach erschöpfter Platzierung nie einen Actor, publiziert
///   HIER der Dispatcher das CommandFailed (docs/08 §8.4), damit der auslösende Client nicht still hängt.
///
/// ★ Härtung: der eigentliche Send ist eine <c>Frage</c> des <see cref="Bote"/> (Pflicht-Frist) mit begrenzter Wiederholung. Ohne Frist
/// wiederholt Proto.Cluster bei noch nicht konvergierter Topologie (Cold-Boot, PartitionIdentityLookup noch nicht gesetzt) UNENDLICH still —
/// der Command hinge für immer, ohne Log. Bleibt die Antwort aus, wird der Versuch beendet und erneut probiert (die Topologie bekommt weitere
/// Chancen); nach allen Versuchen wird LAUT geloggt statt lautlos zu hängen.
/// </summary>
public class ProtoActorAggregateDispatcher : IAggregateDispatcher
{
    private readonly Proto.ActorSystem _actorSystem;
    private Bote? _bote;                              // erst beim ersten Senden: der Cluster ist bei der Registrierung noch nicht fertig
    private readonly ILogger _logger;
    private readonly IDeadLetterSink? _deadLetters;   // ★ Audit-Fix #1: nicht-zustellbaren Command durabel machen
    private readonly Infrastructure.Verteilen.Verteilung? _verteilung;   // ★ CommandFailed an den Client bei erschöpfter Platzierung

    /// <summary>Frist pro Sende-Versuch (begrenzt die interne Placement-Retry-Schleife von Proto.Cluster).</summary>
    private static readonly TimeSpan AttemptTimeout = TimeSpan.FromSeconds(10);

    /// <summary>Anzahl Versuche, bevor der Command als nicht-zustellbar geloggt und verworfen wird.</summary>
    private const int MaxAttempts = 3;

    public ProtoActorAggregateDispatcher(
        Proto.ActorSystem actorSystem,
        ILogger<ProtoActorAggregateDispatcher>? logger = null,
        IDeadLetterSink? deadLetters = null,
        Infrastructure.Verteilen.Verteilung? verteilung = null)
    {
        _actorSystem = actorSystem;
        _logger = logger ?? NullLogger<ProtoActorAggregateDispatcher>.Instance;
        _deadLetters = deadLetters;
        _verteilung = verteilung;
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

    /// <summary>
    /// Der Client-Pfad MIT Antwort (Werkbank-JSON-Eingang, P5): ein Versuch, eine <c>Frage</c> mit dem Zeitlimit als Frist, das
    /// <see cref="CommandResult"/> zurück — Ausgang, Versionen, Ablehnung. <c>null</c> = keine Antwort im Zeitlimit (der Command kann trotzdem
    /// gewirkt haben).
    /// </summary>
    public static Task<CommandResult?> FrageAsync(Proto.ActorSystem system, CommandEnvelope envelope, TimeSpan zeitlimit, CancellationToken ct)
        => new Bote(system.Cluster()).FrageAsync<CommandResult>(
            ClusterIdentity.Create(envelope.AggregateId.ToString(), envelope.AggregateType), envelope, zeitlimit, ct);

    private async Task DispatchWithRetryAsync(CommandEnvelope envelope)
    {
        var identity = ClusterIdentity.Create(
            envelope.AggregateId.ToString(),
            envelope.AggregateType);

        var grund = $"keine Antwort in {AttemptTimeout.TotalSeconds:0} s (Cluster evtl. nicht routbar, Topologie nicht gesetzt)";   // ★ #1: für den Dead-Letter

        for (var attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            // Eine Antwort ⇒ der Actor hat den Command verarbeitet (Erfolg ODER fachliche Ablehnung — das Feedback fließt über Events). In
            //   beiden Fällen ist er ZUGESTELLT → fertig.
            _bote ??= new Bote(_actorSystem.Cluster(), _logger);
            if (await _bote.FrageAsync<CommandResult>(identity, envelope, AttemptTimeout, CancellationToken.None) is not null)
                return;

            _logger.LogWarning(
                "[Dispatcher] Keine Antwort für {Command} an {Type}/{Id} in {Frist} s (Versuch {Attempt}/{Max}) — " +
                "Cluster evtl. noch nicht routbar, erneuter Versuch.",
                envelope.Payload.GetType().Name, envelope.AggregateType, envelope.AggregateId, AttemptTimeout.TotalSeconds, attempt, MaxAttempts);
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

        // ★ Zusätzlich den auslösenden Client benachrichtigen. Der Command hat nie einen Actor
        //   erreicht → NIEMAND publiziert sonst ein CommandFailed (das tut normal der Actor). Ohne dies
        //   hinge der Client still (kein Ack, kein Nack). Gezielt an die OriginSessionId, wie der Actor.
        MeldeCommandFailed(envelope, grund);
    }

    /// <summary>
    /// Meldet ein <see cref="CommandFailed"/> targeted an die <c>OriginSessionId</c> (verlierbar) — spiegelt
    /// das Actor-Muster (<c>AggregateActorBase.MeldeCommandFailed</c>), für den Fall, dass
    /// der Command mangels routbarem Cluster nie einen Actor erreicht hat. Ohne Verteilung
    /// oder ohne OriginSessionId folgenlos.
    /// </summary>
    private void MeldeCommandFailed(CommandEnvelope envelope, string grund)
    {
        var failed = BaueCommandFailed(envelope, grund, MaxAttempts);
        if (_verteilung is null || failed is null)
            return;
        _verteilung.Melde(failed);
        _logger.LogDebug("[Dispatcher] CommandFailed an '{Session}' gemeldet (nicht zugestellt).", envelope.OriginSessionId);
    }

    /// <summary>
    /// Reine Konstruktion des targeted <see cref="CommandFailed"/>-EventEnvelopes für den
    /// Nicht-zugestellt-Fall (store-/cluster-frei, Ebene-1-testbar). Gibt <c>null</c> zurück, wenn keine
    /// <c>OriginSessionId</c> vorliegt — dann gibt es keine Sitzung, der es gälte. Spiegelt
    /// die Feldbelegung von <c>AggregateActorBase.MeldeCommandFailed</c>.
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
            ZielSitzung = envelope.OriginSessionId,
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
