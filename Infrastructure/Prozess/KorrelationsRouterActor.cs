using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Infrastructure.Projections;   // SignalEnvelope kommt aus Abstractions; BrokerSubscription aus PubSub
using Infrastructure.Lebenszyklus;
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
public sealed class KorrelationsRouterActor : RahmenActor
{
    private readonly string _subscriberId;
    private readonly IReadOnlyList<Type> _signalTypes;
    private readonly KorrelationsRouter _router;
    private BrokerSubscription? _subscription;
    // PF-12: Signale parallel (begrenzt) — der Router ist zustandslos, eine Weckung idempotent, die Reihenfolge der Signale zählt nicht.
    //   Nacheinander hielt der Read je Signal die Mailbox an: unter Last staute sie sich bis in den Leerlauf.
    private const int Gleichzeitig = 32;
    private readonly SemaphoreSlim _plaetze = new(Gleichzeitig);

    public KorrelationsRouterActor(string subscriberId, IReadOnlyList<Type> signalTypes, KorrelationsRouter router)
    {
        _subscriberId = subscriberId;
        _signalTypes = signalTypes;
        _router = router;
    }

    protected override async Task BeginneAsync(IContext context)
    {
        _subscription = new BrokerSubscription(context.System.Cluster(), _subscriberId, context.Self);
        await _subscription.SubscribeAsync(_signalTypes, Stopp);
    }

    protected override Task BehandleAsync(IContext context)
    {
        if (context.Message is SignalEnvelope env)
            _ = RouteAsync(env.Signal);
        return Task.CompletedTask;
    }

    protected override Task BeimStoppenAsync(IContext context)
    {
        _subscription?.AlleAbmelden();
        return Task.CompletedTask;
    }

    private async Task RouteAsync(IStateChangeSignal signal)
    {
        try
        {
            await _plaetze.WaitAsync(Stopp);
            try { await _router.OnSignalAsync(signal, Stopp); }
            finally { _plaetze.Release(); }
        }
        catch (OperationCanceledException) { /* Stopp */ }
        catch (Exception ex)
        {
            // Ein Signal darf verloren gehen (Invariante 2): der Prozess-Poll heilt.
            Console.WriteLine($"[Korrelations-Router] Signal {signal.StreamId}@{signal.Version} nicht geroutet: {ex.Message}");
        }
    }
}
