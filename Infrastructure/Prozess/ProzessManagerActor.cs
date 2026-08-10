using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Infrastructure.Projections;   // WakeAck
using Infrastructure.PubSub;        // CommandEmitter
using Proto;
using Proto.Cluster;

namespace Infrastructure.Prozess;

/// <summary>
/// Der GENERISCHE Prozess-Manager als virtueller Cluster-Actor (Spec §4): EIN Kind für ALLE Prozess-Typen,
/// eine Instanz pro Korrelation (Identität = Korrelations-Guid). Verschmilzt das alte Prozess-Aggregat +
/// den Treiber. Er hält im Speicher NICHTS außer der Korrelation — das Marking faltet der
/// <see cref="ProzessManager"/> bei jeder Weckung aus dem Log.
///
/// ★ Treiber-Fold (EM-1): Er feuert die Ziel-Commands FIRE-AND-FORGET über das EINE Emit-Primitiv
/// (<see cref="CommandEmitter"/>) — es gibt genau EINEN Emit-Weg, KEINE Quittung mehr. Der Send läuft
/// detached (<see cref="DetachedProzessSend"/>, der strukturelle (A)-Hang-Fix, in-memory bewiesen); der Turn
/// kehrt sofort zurück. Nach JEDEM Send weckt sich der Manager selbst (<see cref="WeckeSelbst"/>) und faltet
/// den durablen Ausgang der Transition — eine Wirkung, die <c>KommandoVerarbeitet</c>-Noop-Marke oder die
/// <c>KommandoAbgelehnt</c>-Ablehnungs-Marke. Der Fold (nicht mehr eine out-of-turn-Quittung) trägt die
/// Fehlschlag-Erkennung. Die Korrelation reist als <c>CommandEnvelope.CorrelationId</c> mit → sie landet in
/// den Ziel-Event-Metadaten, woraus der <see cref="KorrelationsRouter"/> den Manager neu weckt. Ziel-Aggregate
/// (Konto) bleiben dadurch rein.
///
/// (A)-Fix: <c>system.Cluster()</c> wird in der SPAWN-Factory aufgelöst (siehe Wiring), NICHT bei der
/// Kind-Registrierung; der Ziel-Send ist bounded (kein <c>None</c>-Infinit-Retry).
/// </summary>
public sealed class ProzessManagerActor : IActor
{
    public const string KindName = "prozess-manager";

    private readonly IEventStoreRepository _eventStore;
    private readonly IReadOnlyDictionary<string, ProzessRegeln> _registry;
    private readonly Cluster _cluster;
    private readonly IProzessOffenIndex? _offenIndex;
    private readonly IDeadLetterSink? _deadLetters;
    private readonly IProzessMarkingStore? _markingStore;   // ★ P5(b): nicht-autoritativer Marking-Cursor
    private Guid _korrelation;
    private ProzessManager? _manager;
    private CommandEmitter? _emitter;

    public ProzessManagerActor(
        IEventStoreRepository eventStore,
        IReadOnlyDictionary<string, ProzessRegeln> registry,
        Cluster cluster,
        IProzessOffenIndex? offenIndex = null,
        IDeadLetterSink? deadLetters = null,
        IProzessMarkingStore? markingStore = null)
    {
        _eventStore = eventStore;
        _registry = registry;
        _cluster = cluster;
        _offenIndex = offenIndex;
        _deadLetters = deadLetters;
        _markingStore = markingStore;
    }

    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Started:
                var identity = context.ClusterIdentity()?.Identity;
                if (identity != null && Guid.TryParse(identity, out var korr))
                    _korrelation = korr;
                // Das EINE Emit-Primitiv, an den Cluster gebunden (Cluster ist zur Spawn-Zeit fertig — (A)-Fix).
                _emitter = new CommandEmitter(_cluster);
                _manager = new ProzessManager(_eventStore, _registry, ErzeugeDispatch(), _offenIndex, _deadLetters, _markingStore);
                break;

            case ProzessWake w:
                if (_manager is not null)
                {
                    if (w.ProzessNameFürStart is string name)
                        await _manager.StarteAsync(_korrelation, name, w.QuellStream, w.QuellVersion, context.CancellationToken);
                    else
                        await _manager.WakeAsync(_korrelation, context.CancellationToken);
                }
                context.Respond(new WakeAck());
                break;
        }
    }

    /// <summary>
    /// Der Engine-Dispatch (Treiber-Fold/EM-1): fire-and-forget Emit über das <see cref="CommandEmitter"/>-Primitiv
    /// + Selbst-Weckung nach JEDEM Send. Keine Quittung, kein Fehlschlag-Rückkanal — der Fold liest den durablen
    /// Ausgang (Wirkung/KommandoVerarbeitet/KommandoAbgelehnt).
    /// </summary>
    private Func<Guid, ICommand, Guid, CancellationToken, Task> ErzeugeDispatch()
        => DetachedProzessSend.Wrap(EmittiereAnZiel, WeckeSelbst);

    /// <summary>Nach JEDEM Send: DIESE Manager-Instanz neu wecken (Fortschritt/Terminal/Fehlschlag aus dem Fold).</summary>
    private async Task WeckeSelbst(Guid korrelation, CancellationToken ct)
    {
        var identity = ClusterIdentity.Create(korrelation.ToString(), KindName);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await _cluster.RequestAsync<WakeAck>(identity, new ProzessWake(Guid.Empty, 0, null), cts.Token); }
        catch (OperationCanceledException) { /* Poll-Backstop heilt */ }
    }

    /// <summary>
    /// Der Ziel-Send über das EINE Emit-Primitiv (§5 Weg a): der deterministische <c>vorgang</c> IST die
    /// vorgegebene CommandId → der Fold matcht den durablen Ausgang über <c>CausationId == vorgang</c>, ohne
    /// Quittung. Korrelation → Ziel-Event-Metadatum (Router weckt daraus). Bounded, at-least-once, Empfänger-Inbox.
    /// </summary>
    private Task EmittiereAnZiel(Guid korrelation, ICommand cmd, Guid vorgang, CancellationToken ct)
        => _emitter!.EmitAsync(cmd, commandId: vorgang, korrelation: korrelation, ct);
}
