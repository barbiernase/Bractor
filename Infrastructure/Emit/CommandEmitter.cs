using Abstractions;
using Infrastructure.Aggregate;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Proto.Cluster;
using Infrastructure.Komposition;

namespace Infrastructure.Emit;

/// <summary>
/// Das EINE Emit-Primitiv (EM-1). Genau hier — und nur hier — wird ein Command an ein Fremd-Aggregat
/// geschickt: mit der CommandId, die der Aufrufer deterministisch ableitet (Funktion: aus Eingang und Ausgabe-Index, Uhr: aus der Frist → W1:
/// der Empfänger dedupliziert eine wiederholte Zustellung), <see cref="CommandModus.Emittiert"/> (KEINE Version, §4.2) und eine
/// <c>Frage</c> des <see cref="Bote"/> mit Pflicht-Frist (W2: kein Infinit-Hang). At-least-once auf
/// dem Draht, exactly-once-wirksam über die Empfänger-Inbox.
///
/// Der Aufrufer bekommt den <see cref="EmitAusgang"/> (docs/11 §11.3): „zugestellt“ und „abgelehnt“ sind entschieden,
/// „unbekannt“ (Frist abgelaufen, technischer Fehler, kein Ziel) verlangt eine Wiederholung mit derselben CommandId. Der Emitter
/// selbst wiederholt nie — das tut der Aufrufer an seinem durablen Punkt: die Frist bleibt liegen (G-1), die Funktion lässt ihren
/// Eingang offen und rückt die Position nicht darüber (G-2). Eine Ausnahme verlässt den Emitter nicht.
///
/// <b>Drossel je Ziel</b> (PF-9): höchstens <see cref="EmitDrossel.StandardJeZiel"/> Sends je Ziel-Aggregat gleichzeitig unterwegs (je
/// Knoten). Ein heißes Ziel arbeitet seine Mailbox in der Frist ab, statt dass sich Timeouts und Wiederholungen aufschaukeln; der
/// Rückstau liegt vor dem Send, nicht in der Mailbox des Ziels.
///
/// Context-frei: der <see cref="Bote"/> hängt am Cluster des ActorSystems, nicht an einem per-Message-Context
/// → aus den Funktionen und der Uhr nutzbar. Im Prüfstand ist der Ziel-Send ein
/// <see cref="SendeSeam"/> (ein Fake ohne echten Cluster), eingesetzt als Anfrage des Boten.
/// </summary>
public sealed class CommandEmitter
{
    /// <summary>Naht/Test: der Ziel-Send ohne Cluster.</summary>
    public delegate Task<CommandResult?> SendeSeam(ClusterIdentity ziel, CommandEnvelope envelope, CancellationToken ct);

    private readonly Bote _bote;
    private readonly ILogger _logger;
    private readonly TimeSpan _sendeFrist;
    private readonly EmitDrossel _drossel;

    private static readonly TimeSpan StandardFrist = TimeSpan.FromSeconds(5);

    /// <summary>Live: fragt über den Cluster; alle Live-Emitter eines Knotens teilen eine Drossel.</summary>
    public CommandEmitter(Cluster cluster, ILogger? logger = null, TimeSpan? sendeFrist = null)
        : this(new Bote(cluster ?? throw new ArgumentNullException(nameof(cluster)), logger), logger, sendeFrist) { }

    /// <summary>Live: über einen vorhandenen Boten; alle Live-Emitter eines Knotens teilen eine Drossel.</summary>
    public CommandEmitter(Bote bote, ILogger? logger = null, TimeSpan? sendeFrist = null)
    {
        _bote = bote ?? throw new ArgumentNullException(nameof(bote));
        _logger = logger ?? NullLogger.Instance;
        _sendeFrist = sendeFrist ?? StandardFrist;
        _drossel = EmitDrossel.Gemeinsam;
    }

    /// <summary>Naht/Test: injizierter Send (Fake-Cluster in-memory), eigene Drossel, wenn keine übergeben.</summary>
    public CommandEmitter(SendeSeam send, ILogger? logger = null, TimeSpan? sendeFrist = null, EmitDrossel? drossel = null)
    {
        ArgumentNullException.ThrowIfNull(send);
        _bote = new Bote(async (ziel, nachricht, ct) => await send(ziel, (CommandEnvelope)nachricht, ct), logger);
        _logger = logger ?? NullLogger.Instance;
        _sendeFrist = sendeFrist ?? StandardFrist;
        _drossel = drossel ?? new EmitDrossel();
    }

    /// <summary>
    /// Sendet den Command mit der VORGEGEBENEN CommandId (Funktion: aus Eingang und Ausgabe-Index; Uhr: aus der Frist). Die Korrelation reist
    /// explizit (→ Ziel-Event-Metadatum). Bounded (W2), <see cref="CommandModus.Emittiert"/> (KEINE Version, §4.2), Ausgang an den Aufrufer.
    /// </summary>
    public async Task<EmitAusgang> EmitAsync(ICommand cmd, Guid commandId, Guid korrelation, CancellationToken ct)
    {
        if (!Anwendung.Routing.CommandToAggregate.TryGetValue(cmd.GetType(), out var aggregateType))
        {
            // Ein Command ohne Aggregat verhindert der Build (CQRS010); kommt es trotzdem vor, ist nichts gewirkt.
            _logger.LogError("[Emit] keine AggregateType-Zuordnung für {Command} — nicht routbar", cmd.GetType().Name);
            return EmitAusgang.Unbekannt;
        }

        // Der Envelope entsteht SYNCHRON vor dem ersten await: Spur und Akteur sind AsyncLocal-Bereiche des Aufrufers.
        var envelope = new CommandEnvelope
        {
            CommandId = commandId,                              // deterministisch → Empfänger-Inbox dedupliziert (W1)
            AggregateType = aggregateType,
            Modus = new CommandModus.Emittiert(),               // KEINE Version (§4.2)
            CorrelationId = korrelation.ToString(),             // → Ziel-Event-Metadatum
            Payload = cmd,
            // Akteur (docs/konzept-akteure.md §5.1): der der Kette (ImAuftrag, vom Konsumenten gesetzt); ohne Kette der eine Akteur, der den Command per IDarf darf; sonst keiner („system").
            UserId = ImAuftrag.Akteur ?? Infrastructure.Akteure.AkteurHerkunft.EindeutigerHalter(cmd.GetType()) ?? ImAuftrag.Ohne,
            // Spur: der auslösende Log-Eintrag und der absendende Baustein — das Aggregat stempelt die Ursache an seine Events.
            Ursache = Spur.Aktuell?.Ursache,
            Wirker = Spur.Aktuell?.Wirker,
        };
        var identity = ClusterIdentity.Create(cmd.Ziel.ToString(), aggregateType);

        // Drossel je Ziel (PF-9): das Warten auf einen Platz ist ebenso gebunden wie der Send.
        using var platz = await _drossel.BelegeAsync(identity.ToString(), _sendeFrist, ct);
        if (platz is null)
        {
            _logger.LogDebug("[Emit] Drossel an {AggregateType}/{AggregateId} voll — nicht gesendet", aggregateType, cmd.Ziel);
            return EmitAusgang.Unbekannt;
        }

        // Frage mit Pflicht-Frist (W2): keine Antwort in der Frist (abgelaufen, Identität nicht auflösbar, Knoten weg, Abbruch) heißt: ob
        //   das Ziel gewirkt hat, ist offen.
        var ergebnis = await _bote.FrageAsync<CommandResult>(identity, envelope, _sendeFrist, ct);
        return ergebnis switch
        {
            null => EmitAusgang.Unbekannt,
            { Success: true } => EmitAusgang.Zugestellt,
            { Abgelehnt: true } => EmitAusgang.Abgelehnt,
            _ => Technisch(ergebnis, aggregateType, cmd),
        };
    }

    private EmitAusgang Technisch(CommandResult ergebnis, string aggregateType, ICommand cmd)
    {
        // Ein technischer Fehler im Ziel (DB weg, Append gescheitert): die Wirkung ist offen — wiederholen, die Inbox klärt es.
        _logger.LogWarning("[Emit] {Command} an {AggregateType}/{AggregateId} technisch gescheitert ({Fehler}) — Ausgang unbekannt",
            cmd.GetType().Name, aggregateType, cmd.Ziel, ergebnis.ErrorMessage);
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
