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
/// Der GENERISCHE Prozess-Manager als virtueller Cluster-Actor (docs/04 §4.5): EIN Kind für ALLE Prozess-Typen,
/// eine Instanz pro Korrelation (Identität = Korrelations-Guid). Verschmilzt das alte Prozess-Aggregat +
/// den Treiber. Er hält im Speicher NICHTS außer der Korrelation — das Marking faltet der
/// <see cref="ProzessManager"/> bei jeder Weckung aus dem Log.
///
/// ★ Treiber-Fold (EM-1): Er feuert die Ziel-Commands über das EINE Emit-Primitiv (<see cref="CommandEmitter"/>) — genau EIN
/// Emit-Weg. Der Send läuft losgelöst (<see cref="DetachedProzessSend"/>); der Turn kehrt sofort zurück. Nach einem entschiedenen Send
/// weckt sich der Manager sofort selbst (<see cref="WeckeSelbst"/>), nach einem unbekannten erst nach einem Backoff je Vorgang (PF-9); er
/// faltet dann den durablen Ausgang der Transition — eine Wirkung, die <c>KommandoVerarbeitet</c>-Noop-Marke oder die
/// <c>KommandoAbgelehnt</c>-Ablehnungs-Marke. Der Fold (nicht der Emit-Ausgang) trägt die Fehlschlag-Erkennung. Die Korrelation reist als <c>CommandEnvelope.CorrelationId</c> mit → sie landet in
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
    private readonly IProzessMarkingStore? _markingStore;   // ★ P5b: nicht-autoritativer Marking-Cursor (optional)
    private readonly Func<Guid, IAuftrag, Guid, string?, CancellationToken, Task>? _rufe;   // Rufe<F>: Auftrag an den Vermittler
    private readonly IDbClock? _uhr;                                                // Zeitlimit gegen die DB-Uhr
    private readonly TimeSpan _leerlauf;                                            // PF-5: Passivierung (Marking-Cursor bleibt durabel)
    private Guid _korrelation;
    private ProzessManager? _manager;
    private CommandEmitter? _emitter;

    public ProzessManagerActor(
        IEventStoreRepository eventStore,
        IReadOnlyDictionary<string, ProzessRegeln> registry,
        Cluster cluster,
        IProzessOffenIndex? offenIndex = null,
        IDeadLetterSink? deadLetters = null,
        IProzessMarkingStore? markingStore = null,
        Func<Guid, IAuftrag, Guid, string?, CancellationToken, Task>? rufe = null,
        IDbClock? uhr = null,
        Infrastructure.Lebenszyklus.Passivierung? passivierung = null)
    {
        _leerlauf = (passivierung ?? Infrastructure.Lebenszyklus.Passivierung.Standard).ProzessManager;
        _rufe = rufe;
        _uhr = uhr;
        _eventStore = eventStore;
        _registry = registry;
        _cluster = cluster;
        _offenIndex = offenIndex;
        _deadLetters = deadLetters;
        _markingStore = markingStore;
    }

    public async Task ReceiveAsync(IContext context)
    {
        // PF-5: ein Vorgang ohne Nachricht passiviert. Ein offener, stiller Vorgang wird nur noch selten geheilt (Abstand bis 1 h) — beim
        //   Stoppen geht sein Stand deshalb durabel, damit die nächste Aktivierung dort weiterliest statt beim Beginn des Vorgangs.
        if (Infrastructure.Lebenszyklus.Passivierung.Behandle(context, _leerlauf)) return;
        switch (context.Message)
        {
            case Stopping when _manager is not null:
                await _manager.SichereMarkingAsync(_korrelation, CancellationToken.None);
                break;

            case Started:
                var identity = context.ClusterIdentity()?.Identity;
                if (identity != null && Guid.TryParse(identity, out var korr))
                    _korrelation = korr;
                // Das EINE Emit-Primitiv, an den Cluster gebunden (Cluster ist zur Spawn-Zeit fertig — (A)-Fix).
                _emitter = new CommandEmitter(_cluster);
                _manager = new ProzessManager(_eventStore, _registry, ErzeugeDispatch(), _offenIndex, _deadLetters, _markingStore,
                    rufe: _rufe,
                    jetzt: _uhr is null ? null : _uhr.JetztAsync);
                break;

            case ProzessWake w:
                if (_manager is not null)
                {
                    if (w.ProzessNameFürStart is string name)
                        await _manager.StarteAsync(_korrelation, name, w.QuellStream, w.QuellVersion, context.CancellationToken);
                    else
                        await _manager.WakeAsync(_korrelation, context.CancellationToken, w.VomBackstop, w.QuellStream, w.QuellVersion, w.Vorgang);
                }
                context.Respond(new WakeAck());
                break;
        }
    }

    /// <summary>
    /// Der Engine-Dispatch (Treiber-Fold/EM-1): losgelöster Emit über das <see cref="CommandEmitter"/>-Primitiv, Selbst-Weckung je nach
    /// Ausgang sofort oder nach Backoff, kein zweiter Send eines Vorgangs, solange einer läuft (PF-9).
    /// </summary>
    private Func<Guid, ICommand, Guid, CancellationToken, Task> ErzeugeDispatch()
        => new DetachedProzessSend(EmittiereAnZiel, WeckeSelbst).SendeAsync;

    /// <summary>
    /// Nach dem Send (bzw. seinem Backoff): DIESE Manager-Instanz neu wecken (Fortschritt/Terminal/Fehlschlag aus dem Fold), mit Ziel-Stream
    /// und Vorgang als Quelle und — wenn bekannt — der Version, ab der der Ausgang im Ziel-Stream steht.
    /// </summary>
    private async Task WeckeSelbst(Guid korrelation, Guid ziel, Guid vorgang, int abVersion, CancellationToken ct)
    {
        var identity = ClusterIdentity.Create(korrelation.ToString(), KindName);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await _cluster.RequestAsync<WakeAck>(identity, new ProzessWake(ziel, abVersion, null, Vorgang: vorgang), cts.Token); }
        catch (OperationCanceledException) { /* Poll-Backstop heilt */ }
    }

    /// <summary>
    /// Der Ziel-Send über das EINE Emit-Primitiv (§5 Weg a): der deterministische <c>vorgang</c> IST die
    /// vorgegebene CommandId → der Fold matcht den durablen Ausgang über <c>CausationId == vorgang</c>, ohne
    /// Quittung. Korrelation → Ziel-Event-Metadatum (Router weckt daraus). Bounded, at-least-once, Empfänger-Inbox.
    /// </summary>
    private Task<EmitErgebnis> EmittiereAnZiel(Guid korrelation, ICommand cmd, Guid vorgang, CancellationToken ct)
        => _emitter!.EmitAsync(cmd, commandId: vorgang, korrelation: korrelation, ct);
}
