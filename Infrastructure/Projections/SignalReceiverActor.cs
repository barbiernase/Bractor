using Abstractions;
using Infrastructure.Lebenszyklus;
using Infrastructure.PubSub;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Projections;

/// <summary>
/// Proto-Mantel um den <see cref="SignalReceiver"/> (docs/04 §4.1) — ein lokaler,
/// zustandsloser Actor pro Handler-Klasse und Node.
///
/// Auf <see cref="Started"/> abonniert er über die bestehende <see cref="BrokerSubscription"/>
/// die `StateChangeVia{Event}`-Signal-Typen seiner Projektion. Jedes eintreffende
/// <see cref="SignalEnvelope"/> reicht er an den Receiver-Kern weiter, der den Adapter
/// weckt. Weil der Actor seine Mailbox serialisiert, ist die Verarbeitung pro Node
/// serialisiert — genug für Phase 2 (single-node); die per-Stream-Cluster-Identität
/// (echtes Single-Writer-Sharding) kommt in Phase 4.
///
/// Verlust/Duplikat/Wettrennen sind folgenlos: die Wahrheit liegt im Log-Read des
/// Adapters, nicht im Signal (Invariante 2). Beim Stoppen meldet er sich ab, ohne zu warten (Rahmen, <see cref="Bote.Melde"/>).
/// </summary>
public sealed class SignalReceiverActor : RahmenActor
{
    private readonly string _subscriberId;
    private readonly IReadOnlyList<Type> _signalTypes;
    private readonly SignalReceiver _receiver;
    private BrokerSubscription? _subscription;

    public SignalReceiverActor(
        string subscriberId,
        IReadOnlyList<Type> signalTypes,
        SignalReceiver receiver)
    {
        _subscriberId = subscriberId;
        _signalTypes = signalTypes;
        _receiver = receiver;
    }

    protected override async Task BeginneAsync(IContext context)
    {
        _subscription = new BrokerSubscription(context.System.Cluster(), _subscriberId, context.Self);
        await _subscription.SubscribeAsync(_signalTypes, Stopp);
    }

    protected override async Task BehandleAsync(IContext context)
    {
        if (context.Message is SignalEnvelope env)
            await _receiver.OnSignalAsync(env.Signal, Stopp);
    }

    protected override Task BeimStoppenAsync(IContext context)
    {
        _subscription?.AlleAbmelden();
        return Task.CompletedTask;
    }
}
