using Abstractions;
using Core;
using Infrastructure.PubSub;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Projections;

/// <summary>
/// Phase 4a — der Adapter als VIRTUELLER CLUSTER-ACTOR pro Stream (docs/04 §4.1).
///
/// Identität: (AdapterKind, StreamId). Der Cluster garantiert genau EINE Instanz pro
/// Stream im ganzen Cluster → Single-Writer je Stream; verschiedene Streams laufen
/// parallel. Die Mailbox serialisiert die Wakes eines Streams, sodass Signal- und
/// Poll-Weckungen ohne Race auf denselben Store laufen.
///
/// Der gesamte durable Zustand (die Fortschrittsmarke) liegt im Store — Passivierung bei
/// Idle ist folgenlos. Auf <see cref="Wake"/> läuft die store-agnostische
/// <see cref="ProjectionAdapter"/>-Schleife für den eigenen Stream.
///
/// Projektions-agnostisch: Tracker + Dispatch kommen per Factory (eine frische,
/// actor-eigene Instanz je Stream), damit parallele Stream-Actors sich keinen Puffer
/// bzw. keine Session teilen. Der Tracker ist optional (null → ab 0 lesen).
/// </summary>
public sealed class SignalAdapterActor : IActor
{
    private readonly IEventStoreRepository _eventStore;
    private readonly Func<(string ProjectionId, IProjectionTracker? Tracker, IEmittentenCursor? EmittentenCursor, Func<EventEnvelope, ProjectionWriter, EmitAusgaenge, Task> Dispatch)> _perActorFactory;
    private readonly IReadModelDepsSink? _depsSink;
    private readonly TimeSpan _leerlauf;

    private ProjectionAdapter? _adapter;
    private Guid _streamId;

    /// <summary>
    /// Die Factory liefert die ProjektionsId (aus <c>ISubscriber.SubscriberId</c>, zur Laufzeit
    /// gelesen — der Generator kann sie aus referenzierten Assemblies nicht statisch kennen), die
    /// zueinander exklusiven Achse-B-Marken (replaybarer Tracker ODER Emittenten-Cursor) und den
    /// Dispatch. Frisch je Stream-Actor.
    /// </summary>
    public SignalAdapterActor(
        IEventStoreRepository eventStore,
        Func<(string, IProjectionTracker?, IEmittentenCursor?, Func<EventEnvelope, ProjectionWriter, EmitAusgaenge, Task>)> perActorFactory,
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
        // PF-5: nach dem Leerlauf passivieren — Marke bzw. Cursor liegen durabel im Store.
        if (Infrastructure.Lebenszyklus.Passivierung.Behandle(context, _leerlauf)) return;
        switch (context.Message)
        {
            case Started:
                // Die Cluster-Identität IST der Stream.
                var identity = context.ClusterIdentity()?.Identity;
                if (identity != null && Guid.TryParse(identity, out var sid))
                {
                    _streamId = sid;
                    var (projectionId, tracker, emittentenCursor, dispatch) = _perActorFactory();
                    _adapter = new ProjectionAdapter(
                        _eventStore, tracker, emittentenCursor, projectionId, dispatch, _depsSink);
                }
                break;

            case Wake:
                // Signal und Poll gleich: ab Marke bzw. Cursor. „Nicht aufgeholt“ (ein Command blieb unbekannt) hält den Poll an.
                var aufgeholt = _adapter is null || await _adapter.WakeAsync(_streamId, context.CancellationToken);
                context.Respond(new WakeAck(aufgeholt));
                break;
        }
    }
}
