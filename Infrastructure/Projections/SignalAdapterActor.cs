using Abstractions;
using Core;
using Infrastructure.PubSub;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Projections;

/// <summary>
/// Der Adapter eines EMITTENTEN als VIRTUELLER CLUSTER-ACTOR je Stream (docs/04 §4.1) — Reaktionen und der Fluss-Event-Pfad; Projektionen
/// laufen im Batch-Konsum (<see cref="BatchLeserActor"/>).
///
/// Identität: (AdapterKind, StreamId). Der Cluster garantiert genau EINE Instanz pro Stream → Single-Writer je Stream; verschiedene Streams
/// laufen parallel. Die Mailbox serialisiert Signal- und Poll-Weckungen eines Streams. Der durable Zustand (der Emittenten-Cursor) liegt im
/// Store — Passivierung nach Leerlauf ist folgenlos (PF-5).
///
/// Konsumenten-agnostisch: Cursor und Dispatch kommen per Factory (eine frische, actor-eigene Instanz je Stream).
/// </summary>
public sealed class SignalAdapterActor : IActor
{
    private readonly IEventStoreRepository _eventStore;
    private readonly Func<(string KonsumentId, IEmittentenCursor? EmittentenCursor, Func<EventEnvelope, ProjectionWriter, EmitAusgaenge, Task> Dispatch)> _perActorFactory;
    private readonly IReadModelDepsSink? _depsSink;
    private readonly TimeSpan _leerlauf;

    private EmittentenAdapter? _adapter;
    private Guid _streamId;

    /// <summary>
    /// Die Factory liefert die Konsumenten-Id (aus <c>ISubscriber.SubscriberId</c> bzw. der PipelineId, zur Laufzeit gelesen), den
    /// Emittenten-Cursor und den Dispatch. Frisch je Stream-Actor.
    /// </summary>
    public SignalAdapterActor(
        IEventStoreRepository eventStore,
        Func<(string, IEmittentenCursor?, Func<EventEnvelope, ProjectionWriter, EmitAusgaenge, Task>)> perActorFactory,
        IReadModelDepsSink? depsSink = null,
        Infrastructure.Lebenszyklus.Passivierung? passivierung = null)
    {
        _leerlauf = (passivierung ?? Infrastructure.Lebenszyklus.Passivierung.Standard).Adapter;
        _eventStore = eventStore;
        _perActorFactory = perActorFactory;
        _depsSink = depsSink;
    }

    public async Task ReceiveAsync(IContext context)
    {
        // PF-5: nach dem Leerlauf passivieren — der Cursor liegt durabel im Store.
        if (Infrastructure.Lebenszyklus.Passivierung.Behandle(context, _leerlauf)) return;
        switch (context.Message)
        {
            case Started:
                // Die Cluster-Identität IST der Stream.
                var identity = context.ClusterIdentity()?.Identity;
                if (identity != null && Guid.TryParse(identity, out var sid))
                {
                    _streamId = sid;
                    var (konsumentId, emittentenCursor, dispatch) = _perActorFactory();
                    _adapter = new EmittentenAdapter(_eventStore, emittentenCursor, konsumentId, dispatch, _depsSink);
                }
                break;

            case Wake:
                // Signal und Poll gleich: ab dem Cursor. „Nicht aufgeholt“ (ein Command blieb unbekannt) hält den Poll an.
                var aufgeholt = _adapter is null || await _adapter.WakeAsync(_streamId, context.CancellationToken);
                context.Respond(new WakeAck(aufgeholt));
                break;
        }
    }
}
