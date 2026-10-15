using Abstractions;
using Infrastructure.Aggregate;
using Infrastructure.Pipeline;   // Anwendung.Pipelines.CommandAggregateTypes
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Proto.Cluster;
using Infrastructure.Komposition;

namespace Infrastructure.PubSub;

/// <summary>
/// Das EINE Emit-Primitiv (EM-1). Genau hier — und nur hier — wird ein Command an ein Fremd-Aggregat
/// geschickt: deterministische CommandId (<see cref="EmitId"/> → W1: der Empfänger dedupliziert eine
/// wiederholte Zustellung), <see cref="CommandModus.Emittiert"/> (KEINE Version, §4.2) und ein
/// <b>bounded</b> Token (W2: kein <c>CancellationToken.None</c>, kein Infinit-Hang). At-least-once auf
/// dem Draht, exactly-once-wirksam über die Empfänger-Inbox.
///
/// Der Aufrufer bekommt den <see cref="EmitAusgang"/> (docs/11 §11.3): „zugestellt“ und „abgelehnt“ sind entschieden,
/// „unbekannt“ (Frist abgelaufen, technischer Fehler, kein Ziel) verlangt eine Wiederholung mit derselben CommandId. Der Emitter
/// selbst wiederholt nie — das tut der Aufrufer an seinem durablen Punkt: die Frist bleibt liegen (G-1), der Emittenten-Cursor
/// rückt nicht vor (G-2), der Dirigent wiederholt mit Backoff (PF-9). Eine Ausnahme verlässt den Emitter nicht.
///
/// <b>Drossel je Ziel</b> (PF-9): höchstens <see cref="EmitDrossel.StandardJeZiel"/> Sends je Ziel-Aggregat gleichzeitig unterwegs (je
/// Knoten). Ein heißes Ziel arbeitet seine Mailbox in der Frist ab, statt dass sich Timeouts und Wiederholungen aufschaukeln; der
/// Rückstau liegt vor dem Send, nicht in der Mailbox des Ziels.
///
/// Context-frei: der <see cref="Cluster"/> kommt aus dem ActorSystem, nicht aus einem per-Message-Context
/// → aus dem Pull-Adapter (Reaktion), dem Prozess-Manager und dem Fristplaner nutzbar. Der Ziel-Send ist ein
/// <see cref="SendeSeam"/> — live der <c>cluster.RequestAsync</c>, im Prüfstand ein Fake (ohne echten Cluster).
/// </summary>
public sealed class CommandEmitter : ICommandEmitter
{
    /// <summary>Der einzige Cluster-Berührungspunkt — live gebunden, im Test injiziert.</summary>
    public delegate Task<CommandResult?> SendeSeam(ClusterIdentity ziel, CommandEnvelope envelope, CancellationToken ct);

    private readonly SendeSeam _send;
    private readonly ILogger _logger;
    private readonly TimeSpan _sendeFrist;
    private readonly EmitDrossel _drossel;

    private static readonly TimeSpan StandardFrist = TimeSpan.FromSeconds(5);

    /// <summary>Live: bindet den Send an den Cluster; alle Live-Emitter eines Knotens teilen eine Drossel.</summary>
    public CommandEmitter(Cluster cluster, ILogger? logger = null, TimeSpan? sendeFrist = null)
    {
        if (cluster is null) throw new ArgumentNullException(nameof(cluster));
        _send = (id, env, ct) => cluster.RequestAsync<CommandResult>(id, env, ct);
        _logger = logger ?? NullLogger.Instance;
        _sendeFrist = sendeFrist ?? StandardFrist;
        _drossel = EmitDrossel.Gemeinsam;
    }

    /// <summary>Naht/Test: injizierter Send (Fake-Cluster in-memory), eigene Drossel, wenn keine übergeben.</summary>
    public CommandEmitter(SendeSeam send, ILogger? logger = null, TimeSpan? sendeFrist = null, EmitDrossel? drossel = null)
    {
        _send = send ?? throw new ArgumentNullException(nameof(send));
        _logger = logger ?? NullLogger.Instance;
        _sendeFrist = sendeFrist ?? StandardFrist;
        _drossel = drossel ?? new EmitDrossel();
    }

    public Task<EmitAusgang> EmitAsync(ICommand cmd, EmitKausalität k, CancellationToken ct)
        => SendeAsync(cmd, EmitId.Ableiten(k, cmd.AggregateId), k.Korrelation, ct);

    /// <summary>
    /// EM-1-Überladung für den Prozess-Manager (Treiber-Fold) und den Fristplaner: eine VORGEGEBENE CommandId — beim Prozess der
    /// deterministische <c>vorgang</c> (aus <c>ProzessId.FürTransition</c>/<c>FürKompensation</c>), sodass der Fold-Match (Ziel-Event/-Marke
    /// mit <c>CausationId == vorgang</c>) unverändert bleibt. Die Korrelation reist explizit (→ Ziel-Event-Metadatum, Poll-Routing). Sonst
    /// identisch: bounded (W2), <see cref="CommandModus.Emittiert"/> (KEINE Version, §4.2), Ausgang an den Aufrufer.
    /// </summary>
    public Task<EmitAusgang> EmitAsync(ICommand cmd, Guid commandId, Guid korrelation, CancellationToken ct)
        => SendeAsync(cmd, commandId, korrelation, ct);

    private async Task<EmitAusgang> SendeAsync(ICommand cmd, Guid commandId, Guid korrelation, CancellationToken ct)
    {
        if (!Anwendung.Pipelines.CommandAggregateTypes.TryGetValue(cmd.GetType(), out var aggregateType))
        {
            // Ein Command ohne Aggregat verhindert der Build (CQRS010); kommt es trotzdem vor, ist nichts gewirkt.
            _logger.LogError("[Emit] keine AggregateType-Zuordnung für {Command} — nicht routbar", cmd.GetType().Name);
            return EmitAusgang.Unbekannt;
        }

        // Der Envelope entsteht SYNCHRON vor dem ersten await: Spur und Akteur sind AsyncLocal-Bereiche des Aufrufers.
        var envelope = new CommandEnvelope
        {
            CommandId = commandId,                              // deterministisch → Empfänger-Inbox dedupliziert (W1)
            AggregateId = cmd.AggregateId,
            AggregateType = aggregateType,
            Modus = new CommandModus.Emittiert(),               // KEINE Version (§4.2)
            CorrelationId = korrelation.ToString(),             // → Ziel-Event-Metadatum (P5-Poll-Routing)
            Payload = cmd,
            // Akteur (docs/konzept-akteure.md §5.1): der der Kette bzw. des Akteur-Dienstes (ImAuftrag, vom Konsumenten/Dispatch
            //   gesetzt); ohne Kette der eine Akteur, der den Command per IDarf darf; sonst keiner („system").
            UserId = ImAuftrag.Akteur ?? Infrastructure.Akteure.AkteurHerkunft.EindeutigerHalter(cmd.GetType()) ?? ImAuftrag.Ohne,
            // Spur: der auslösende Log-Eintrag und der absendende Baustein — das Aggregat stempelt die Ursache an seine Events.
            Ursache = Spur.Aktuell?.Ursache,
            Wirker = Spur.Aktuell?.Wirker,
        };
        var identity = ClusterIdentity.Create(cmd.AggregateId.ToString(), aggregateType);

        // Drossel je Ziel (PF-9): das Warten auf einen Platz ist ebenso gebunden wie der Send.
        using var platz = await _drossel.BelegeAsync(identity.ToString(), _sendeFrist, ct);
        if (platz is null)
        {
            _logger.LogDebug("[Emit] Drossel an {AggregateType}/{AggregateId} voll — nicht gesendet", aggregateType, cmd.AggregateId);
            return EmitAusgang.Unbekannt;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(_sendeFrist);                           // bounded (W2)
        CommandResult? ergebnis;
        try
        {
            ergebnis = await _send(identity, envelope, cts.Token);
        }
        catch (OperationCanceledException)
        {
            _logger.LogDebug("[Emit] Frist an {AggregateType}/{AggregateId} abgelaufen — Ausgang unbekannt", aggregateType, cmd.AggregateId);
            return EmitAusgang.Unbekannt;
        }
        catch (Exception ex)
        {
            // Transportfehler (Identität nicht auflösbar, Dead-Letter, Knoten weg): ob das Ziel gewirkt hat, ist offen.
            _logger.LogDebug(ex, "[Emit] Send an {AggregateType}/{AggregateId} gescheitert — Ausgang unbekannt", aggregateType, cmd.AggregateId);
            return EmitAusgang.Unbekannt;
        }

        return ergebnis switch
        {
            null => EmitAusgang.Unbekannt,                     // Proto.Cluster liefert null, wenn die Frist abläuft
            { Success: true } => EmitAusgang.Zugestellt,
            { Abgelehnt: true } => EmitAusgang.Abgelehnt,
            _ => Technisch(ergebnis, aggregateType, cmd),
        };
    }

    private EmitAusgang Technisch(CommandResult ergebnis, string aggregateType, ICommand cmd)
    {
        // Ein technischer Fehler im Ziel (DB weg, Append gescheitert): die Wirkung ist offen — wiederholen, die Inbox klärt es.
        _logger.LogWarning("[Emit] {Command} an {AggregateType}/{AggregateId} technisch gescheitert ({Fehler}) — Ausgang unbekannt",
            cmd.GetType().Name, aggregateType, cmd.AggregateId, ergebnis.ErrorMessage);
        return EmitAusgang.Unbekannt;
    }
}

/// <summary>
/// Begrenzt die gleichzeitig unterwegs befindlichen Sends je Ziel (Cluster-Identität) — PF-9. Einträge leben nur, solange ein Send
/// für das Ziel läuft oder wartet; ein Knoten mit vielen Zielen hält deshalb keine Semaphoren auf Vorrat.
/// </summary>
public sealed class EmitDrossel
{
    /// <summary>Gleichzeitige Sends je Ziel-Aggregat und Knoten. Ein Aggregat arbeitet seriell; mehr als ein paar Sends in seiner
    /// Mailbox erhöhen nur die Wartezeit jedes einzelnen bis an die Sendefrist.</summary>
    public const int StandardJeZiel = 8;

    /// <summary>Die Drossel aller Live-Emitter eines Prozesses.</summary>
    public static EmitDrossel Gemeinsam { get; } = new();

    private readonly int _jeZiel;
    private readonly Dictionary<string, Eintrag> _ziele = new();

    public EmitDrossel(int jeZiel = StandardJeZiel) => _jeZiel = Math.Max(1, jeZiel);

    private sealed class Eintrag(int plaetze)
    {
        public readonly SemaphoreSlim Plaetze = new(plaetze, plaetze);
        public int Nutzer;
    }

    /// <summary>Belegt einen Platz für <paramref name="ziel"/>; null, wenn in <paramref name="frist"/> keiner frei wurde.</summary>
    public async Task<IDisposable?> BelegeAsync(string ziel, TimeSpan frist, CancellationToken ct)
    {
        Eintrag e;
        lock (_ziele)
        {
            if (!_ziele.TryGetValue(ziel, out e!)) _ziele[ziel] = e = new Eintrag(_jeZiel);
            e.Nutzer++;
        }
        var belegt = false;
        try
        {
            belegt = await e.Plaetze.WaitAsync(frist, ct);
        }
        catch (OperationCanceledException) { }
        if (belegt) return new Platz(this, ziel, e);
        Verlasse(ziel, e);
        return null;
    }

    /// <summary>Wie viele Sends für <paramref name="ziel"/> gerade laufen (Test, Diagnose).</summary>
    public int Unterwegs(string ziel)
    {
        lock (_ziele) return _ziele.TryGetValue(ziel, out var e) ? _jeZiel - e.Plaetze.CurrentCount : 0;
    }

    private void Verlasse(string ziel, Eintrag e)
    {
        lock (_ziele)
            if (--e.Nutzer == 0) _ziele.Remove(ziel);
    }

    private sealed class Platz(EmitDrossel drossel, string ziel, Eintrag e) : IDisposable
    {
        private int _frei;
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _frei, 1) == 1) return;
            e.Plaetze.Release();
            drossel.Verlasse(ziel, e);
        }
    }
}
