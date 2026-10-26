using Abstractions;
using Core;
using Infrastructure.Lebenszyklus;
using Infrastructure.PubSub;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Projections;

/// <summary>
/// Der Adapter eines EMITTENTEN als VIRTUELLER CLUSTER-ACTOR je Stream (docs/04 §4.1) — Reaktionen und der Fluss-Event-Pfad; Projektionen
/// laufen im Batch-Konsum (<see cref="BatchLeserActor"/>).
///
/// Identität: (AdapterKind, StreamId). Der Cluster garantiert genau EINE Instanz pro Stream → Single-Writer je Stream; verschiedene Streams
/// laufen parallel. Die Mailbox serialisiert Signal- und Poll-Weckungen eines Streams. Der durable Zustand (der Emittenten-Cursor) liegt im
/// Store — Passivierung nach Leerlauf ist folgenlos (PF-5). Der Fähigkeits-Bereich gehört der Aktivierung und wird mit ihr frei (Rahmen);
/// scheitert eine Weckung, antwortet der Adapter ehrlich „nicht aufgeholt“ — der Poll hält seine Marke.
///
/// Konsumenten-agnostisch: Cursor und Dispatch kommen per Factory (eine frische, actor-eigene Instanz je Stream, aus ihrem Bereich).
/// </summary>
public sealed class SignalAdapterActor : RahmenActor
{
    private readonly IEventStoreRepository _eventStore;
    private readonly Func<IFaehigkeiten, (string KonsumentId, IEmittentenCursor? EmittentenCursor, Func<EventEnvelope, ProjectionWriter, EmitAusgaenge, Task> Dispatch)> _perActorFactory;
    private readonly IReadModelDepsSink? _depsSink;

    private EmittentenAdapter? _adapter;
    private Guid _streamId;

    /// <summary>
    /// Die Factory liefert aus dem Bereich der Aktivierung die Konsumenten-Id (aus <c>ISubscriber.SubscriberId</c> bzw. der PipelineId, zur
    /// Laufzeit gelesen), den Emittenten-Cursor und den Dispatch. Frisch je Stream-Actor.
    /// </summary>
    public SignalAdapterActor(
        IEventStoreRepository eventStore,
        Func<IFaehigkeiten, (string, IEmittentenCursor?, Func<EventEnvelope, ProjectionWriter, EmitAusgaenge, Task>)> perActorFactory,
        IFaehigkeitsFabrik faehigkeiten,
        IReadModelDepsSink? depsSink = null,
        Passivierung? passivierung = null,
        ILogger? logger = null)
        : base(logger, (passivierung ?? Passivierung.Standard).Adapter, faehigkeiten)
    {
        _eventStore = eventStore;
        _perActorFactory = perActorFactory;
        _depsSink = depsSink;
    }

    protected override Task BeginneAsync(IContext context)
    {
        // Die Cluster-Identität IST der Stream.
        var identity = context.ClusterIdentity()?.Identity;
        if (identity != null && Guid.TryParse(identity, out var sid))
        {
            _streamId = sid;
            var (konsumentId, emittentenCursor, dispatch) = _perActorFactory(Bereich);
            _adapter = new EmittentenAdapter(_eventStore, emittentenCursor, konsumentId, dispatch, _depsSink);
        }
        return Task.CompletedTask;
    }

    protected override async Task BehandleAsync(IContext context)
    {
        if (context.Message is not Wake) return;
        // Signal und Poll gleich: ab dem Cursor. „Nicht aufgeholt“ (ein Command blieb unbekannt) hält den Poll an.
        var aufgeholt = _adapter is null || await _adapter.WakeAsync(_streamId, Stopp);
        context.Respond(new WakeAck(aufgeholt));
    }

    /// <summary>Eine gescheiterte Weckung ist nicht aufgeholt — der Poll hält seine Marke und weckt erneut.</summary>
    protected override object? Fehlerantwort(object nachricht, Exception fehler) => nachricht is Wake ? new WakeAck(Aufgeholt: false) : null;
}
