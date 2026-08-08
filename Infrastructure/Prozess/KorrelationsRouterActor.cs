using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Abstractions;
using Infrastructure.Projections;   // SignalEnvelope kommt aus Abstractions; BrokerSubscription aus PubSub
using Infrastructure.PubSub;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Prozess;

/// <summary>
/// Proto-Mantel um den <see cref="KorrelationsRouter"/> — analog <c>SignalReceiverActor</c>, aber er weckt
/// nach KORRELATION statt nach StreamId. Ein lokaler, zustandsloser Actor pro Node: auf <c>Started</c>
/// abonniert er die <c>StateChangeVia{Event}</c>-Signale aller teilnehmenden Prozess-Events; jedes
/// eintreffende Signal reicht er an den Router, der daraus den zuständigen Prozess-Manager weckt.
/// </summary>
public sealed class KorrelationsRouterActor : IActor
{
    private readonly string _subscriberId;
    private readonly IReadOnlyList<Type> _signalTypes;
    private readonly KorrelationsRouter _router;
    private BrokerSubscription? _subscription;

    public KorrelationsRouterActor(string subscriberId, IReadOnlyList<Type> signalTypes, KorrelationsRouter router)
    {
        _subscriberId = subscriberId;
        _signalTypes = signalTypes;
        _router = router;
    }

    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Started:
                _subscription = new BrokerSubscription(context.System.Cluster(), _subscriberId, context.Self);
                await _subscription.SubscribeAsync(_signalTypes);
                break;

            case SignalEnvelope env:
                await _router.OnSignalAsync(env.Signal, context.CancellationToken);
                break;

            case Stopping:
                if (_subscription != null)
                    await _subscription.UnsubscribeAllAsync();
                break;
        }
    }
}
