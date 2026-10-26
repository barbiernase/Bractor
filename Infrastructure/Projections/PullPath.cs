using Abstractions;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Projections;

/// <summary>
/// Generische, DOMÄNENFREIE Beschreibung eines Pull-Pfads (vom Generator pro
/// <c>IPullSubscriber</c> registriert). Trägt nur Strings/Typen — kein Domänen-Code.
/// </summary>
/// <param name="SubscriberId">Broker-Subscription-Id des Receivers und Projektions-Id
/// (Mark- und Deps-Schlüssel). = <c>ISubscriber.SubscriberId</c>.</param>
/// <param name="KindName">Cluster-Kind des per-Stream-Adapters (Ziel der Wake-Zustellung).</param>
/// <param name="EventTypes">Die Input-Event-Typen der Projektion → deren StateChangeVia-Signale.</param>
public sealed record PullPathRegistration(
    string SubscriberId,
    string KindName,
    IReadOnlyList<Type> EventTypes);

/// <summary>
/// Der EINE generische Pull-Startup-Dienst (ersetzt jeden handgeschriebenen, projektions-
/// spezifischen Startup). Iteriert die registrierten <see cref="PullPathRegistration"/> und
/// verdrahtet pro Projektion:
///   - einen <see cref="SignalReceiverActor"/> (Signal → Wake an die per-Stream-Cluster-Identität),
///   - einen <see cref="Poller"/> als Backstop (weckt DIESELBE Identität → der Stream-Actor serialisiert).
///
/// Kennt keinen Domänentyp — die per-Stream-Adapter-Kinds bringt der Generator als
/// <see cref="IClusterKindContributor"/> bei (vor StartMemberAsync eingesammelt).
/// </summary>
public sealed class GenericPullStartupService : IHostedService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);

    private readonly ActorSystem _system;
    private readonly IEventStoreRepository _eventStore;
    private readonly IEnumerable<PullPathRegistration> _registrations;
    private readonly IPollCursorStore _pollCursors;
    private readonly ILogger<GenericPullStartupService>? _logger;

    private readonly List<PID> _pids = new();
    private CancellationTokenSource? _pollCts;
    private readonly List<Task> _pollLoops = new();

    public GenericPullStartupService(
        ActorSystem system,
        IEventStoreRepository eventStore,
        IEnumerable<PullPathRegistration> registrations,
        IPollCursorStore pollCursors,
        ILogger<GenericPullStartupService>? logger = null)
    {
        _system = system;
        _eventStore = eventStore;
        _registrations = registrations;
        _pollCursors = pollCursors;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _pollCts = new CancellationTokenSource();
        var bote = new Bote(_system.Cluster(), _logger);

        Console.WriteLine();
        Console.WriteLine("=== Pull-Pfad (generiert) ===");

        foreach (var reg in _registrations)
        {
            var signalTypes = SignalReceiver.SignalTypesFor(reg.EventTypes);
            var kindName = reg.KindName;

            // Signal → Wake an (KindName, StreamId): der per-Stream-Cluster-Actor serialisiert
            // Signal- und Poll-Quelle (kein Race). Das Signal ist NUR ein Weckruf und darf
            // verloren gehen (Invariante 2) → fire-and-forget; es läuft im Receiver-Actor-Turn
            // und darf ihn nicht blockieren. Zusammengefasst je Stream (PF-12): ein Adapter liest
            // bei einer Weckung alles ab seinem Cursor — viele Signale brauchen eine Weckung, nicht viele.
            var weckungen = new Weckungen<Guid>(streamId =>
                // Verlierbar (der Poll-Backstop heilt); die Quittung abwarten, damit je Stream höchstens eine Weckung unterwegs ist.
                bote.FrageAsync<WakeAck>(ClusterIdentity.Create(streamId.ToString(), kindName), new Wake(), TimeSpan.FromSeconds(10), default));
            Func<Guid, CancellationToken, Task> signalWake = (streamId, _) =>
            {
                weckungen.Wecke(streamId);
                return Task.CompletedTask;
            };

            // Poll → Wake: der Backstop MUSS die Verarbeitung BESTÄTIGEN, bevor sein Cursor
            // vorrücken darf (Befund 1). Auf das WakeAck warten (bounded) und zurückmelden, ob
            // aufgeholt wurde; kein Ack (Timeout/Fehler) → nicht caught-up → der Poller hält die
            // HWM und re-scannt beim nächsten Durchlauf, statt einen unverarbeiteten (terminalen)
            // Stream still zu überspringen. Läuft im Poll-Loop (kein Actor-Turn) → awaiten ist ok.
            Func<Guid, CancellationToken, Task<bool>> pollWake = async (streamId, c) =>
                await bote.FrageAsync<WakeAck>(ClusterIdentity.Create(streamId.ToString(), kindName), new Wake(), TimeSpan.FromSeconds(10), c)
                    is { Aufgeholt: true };

            var receiver = new SignalReceiver(signalWake);
            var pid = _system.Root.Spawn(Props.FromProducer(() =>
                new SignalReceiverActor(reg.SubscriberId, signalTypes, receiver)));
            _pids.Add(pid);

            // Durabler Start: bei der zuletzt gescannten HWM aufsetzen, NICHT bei 0 — sonst würde
            // jeder Boot die ganze Historie re-scannen (und tracker-lose Handler alles re-emittieren).
            var startHighWater = await _pollCursors.GetAsync(reg.SubscriberId, ct);
            var poller = new Poller(_eventStore, pollWake, startHighWater, BatchLeser.TypenAus(reg.EventTypes));
            _pollLoops.Add(PollLoopAsync(reg.SubscriberId, poller, _pollCts.Token));

            Console.WriteLine($"  ✓ {reg.SubscriberId}: Receiver auf {signalTypes.Count} Signal-Typen → Kind '{kindName}' (+ Poll {PollInterval.TotalSeconds:0}s, ab HWM {startHighWater})");
        }

        Console.WriteLine();
    }

    private async Task PollLoopAsync(string pollerId, Poller poller, CancellationToken ct)
    {
        // Der erste Durchlauf sofort (kurz nach dem Start, wenn der Cluster steht): holt nach, was während „unten" geschah, und
        // startet einen Neuaufbau (Cursor 0, ProjektionsStandDienst) ohne ein Poll-Intervall Wartezeit.
        var erster = true;
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(erster ? TimeSpan.FromSeconds(2) : PollInterval, ct); }
            catch (OperationCanceledException) { break; }
            erster = false;

            try
            {
                await poller.PollOnceAsync(ct);
                // HWM durabel vorrücken → der nächste Boot setzt hier auf (holt „während unten"-
                // Events nach, re-scannt aber keine alte Historie).
                await _pollCursors.SetAsync(pollerId, poller.HighWater, ct);
            }
            catch (Exception ex)
            {
                // Ein gescheiterter Durchlauf hält die Hochwassermarke; scheitert jeder, heilt kein verlorenes Signal mehr (G-7) — laut melden.
                if (_logger is null) Console.WriteLine($"[Pull] Poll {pollerId} fehlgeschlagen: {ex.Message}");
                else _logger.LogWarning(ex, "[Pull] Poll {PollerId} ab HWM {Hwm} fehlgeschlagen — die Marke steht", pollerId, poller.HighWater);
            }
        }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _pollCts?.Cancel();
        foreach (var loop in _pollLoops)
        {
            try { await loop; } catch { /* egal */ }
        }
        foreach (var pid in _pids)
            await _system.Root.StopAsync(pid);
        _pids.Clear();
    }
}
