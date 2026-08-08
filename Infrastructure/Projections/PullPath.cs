using Abstractions;
using Microsoft.Extensions.Hosting;
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

    private readonly List<PID> _pids = new();
    private CancellationTokenSource? _pollCts;
    private readonly List<Task> _pollLoops = new();

    public GenericPullStartupService(
        ActorSystem system,
        IEventStoreRepository eventStore,
        IEnumerable<PullPathRegistration> registrations,
        IPollCursorStore pollCursors)
    {
        _system = system;
        _eventStore = eventStore;
        _registrations = registrations;
        _pollCursors = pollCursors;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        _pollCts = new CancellationTokenSource();

        Console.WriteLine();
        Console.WriteLine("=== Pull-Pfad (generiert) ===");

        foreach (var reg in _registrations)
        {
            var signalTypes = SignalReceiver.SignalTypesFor(reg.EventTypes);
            var kindName = reg.KindName;

            // Signal → Wake an (KindName, StreamId): der per-Stream-Cluster-Actor serialisiert
            // Signal- und Poll-Quelle (kein Race). Das Signal ist NUR ein Weckruf und darf
            // verloren gehen (Invariante 2) → fire-and-forget; es läuft im Receiver-Actor-Turn
            // und darf ihn nicht blockieren.
            Func<Guid, CancellationToken, Task> signalWake = (streamId, c) =>
            {
                var identity = ClusterIdentity.Create(streamId.ToString(), kindName);
                _ = _system.Cluster().RequestAsync<WakeAck>(identity, new Wake(), c);
                return Task.CompletedTask;
            };

            // Poll → Wake: der Backstop MUSS die Verarbeitung BESTÄTIGEN, bevor sein Cursor
            // vorrücken darf (Befund 1). Auf das WakeAck warten (bounded) und zurückmelden, ob
            // aufgeholt wurde; kein Ack (Timeout/Fehler) → nicht caught-up → der Poller hält die
            // HWM und re-scannt beim nächsten Durchlauf, statt einen unverarbeiteten (terminalen)
            // Stream still zu überspringen. Läuft im Poll-Loop (kein Actor-Turn) → awaiten ist ok.
            Func<Guid, CancellationToken, Task<bool>> pollWake = async (streamId, c) =>
            {
                var identity = ClusterIdentity.Create(streamId.ToString(), kindName);
                using var cts = CancellationTokenSource.CreateLinkedTokenSource(c);
                cts.CancelAfter(TimeSpan.FromSeconds(10));
                try
                {
                    var ack = await _system.Cluster().RequestAsync<WakeAck>(identity, new Wake(), cts.Token);
                    return ack is not null;
                }
                catch { return false; }
            };

            var receiver = new SignalReceiver(signalWake);
            var pid = _system.Root.Spawn(Props.FromProducer(() =>
                new SignalReceiverActor(reg.SubscriberId, signalTypes, receiver)));
            _pids.Add(pid);

            // Durabler Start: bei der zuletzt gescannten HWM aufsetzen, NICHT bei 0 — sonst würde
            // jeder Boot die ganze Historie re-scannen (und tracker-lose Handler alles re-emittieren).
            var startHighWater = await _pollCursors.GetAsync(reg.SubscriberId, ct);
            var poller = new Poller(_eventStore, pollWake, startHighWater);
            _pollLoops.Add(PollLoopAsync(reg.SubscriberId, poller, _pollCts.Token));

            Console.WriteLine($"  ✓ {reg.SubscriberId}: Receiver auf {signalTypes.Count} Signal-Typen → Kind '{kindName}' (+ Poll {PollInterval.TotalSeconds:0}s, ab HWM {startHighWater})");
        }

        Console.WriteLine();
    }

    private async Task PollLoopAsync(string pollerId, Poller poller, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(PollInterval, ct); }
            catch (OperationCanceledException) { break; }

            try
            {
                await poller.PollOnceAsync(ct);
                // HWM durabel vorrücken → der nächste Boot setzt hier auf (holt „während unten"-
                // Events nach, re-scannt aber keine alte Historie).
                await _pollCursors.SetAsync(pollerId, poller.HighWater, ct);
            }
            catch (Exception ex) { Console.WriteLine($"[Pull] Poll fehlgeschlagen: {ex.Message}"); }
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
