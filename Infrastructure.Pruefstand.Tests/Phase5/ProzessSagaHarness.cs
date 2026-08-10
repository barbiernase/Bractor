using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Infrastructure;                 // AggregateHandlerFactory
using Infrastructure.Aggregate;       // AggregateRehydrator, KommandoVerarbeitet/-Abgelehnt, EventEnvelopeFactory
using Infrastructure.Prozess;         // ProzessManager, ProzessBeendet

namespace Infrastructure.Pruefstand.Phase5;

/// <summary>
/// In-memory Saga-Harness (P5(b), Handoff §6): treibt den ECHTEN <see cref="ProzessManager"/> ohne Cluster.
/// Der <c>_dispatch</c>-Seam routet die emittierten Commands an die ECHTEN Ziel-Aggregate (Konto) über die
/// generierte <see cref="AggregateHandlerFactory"/> + einen zählenden In-Memory-Event-Store — mit
/// FAITHFULLER Framework-Inbox (KommandoVerarbeitet/-Abgelehnt, Zwei-Mengen-Dedup), damit die Sagas
/// deterministisch, ohne Proto, aber semantisch wie live laufen.
///
/// Der Dispatch ist hier SYNCHRON (der Effekt ist durabel, wenn die Weckung zurückkehrt) — live läuft er
/// fire-and-forget + Selbst-Weckung; das ändert nur das Timing, nicht die gefaltete Feuer-Entscheidung.
/// Genau das ist die Wahrheit, an der P5(b) den Cursor misst: der Manager wird zweimal getrieben —
/// Cursor AUS vs. AN — und die Feuer-Sequenz + der Terminal-Ausgang müssen identisch sein.
/// </summary>
public sealed class ProzessSagaHarness
{
    public readonly ZaehlenderEventStore Store;
    private readonly IReadOnlyDictionary<string, ProzessRegeln> _registry;
    private readonly IAggregateHandlerFactory _factory = new AggregateHandlerFactory();
    private readonly IProzessMarkingStore? _marking;
    private readonly ProzessManager _manager;

    /// <summary>Die gefeuerten Transitionen in Reihenfolge: (Command-Typ, Vorgang, Ziel-Aggregat).</summary>
    public readonly List<(string Cmd, Guid Vorgang, Guid Ziel)> Gefeuert = new();

    public ProzessSagaHarness(IReadOnlyDictionary<string, ProzessRegeln> registry, IProzessMarkingStore? marking = null)
    {
        _registry = registry;
        _marking = marking;
        Store = new ZaehlenderEventStore(_factory);
        _manager = new ProzessManager(Store, _registry, DispatchAsync, offenIndex: null, deadLetters: null, markingStore: _marking);
    }

    // ── Aufbau: Ziel-/Auslöser-Streams seeden (wie Client-Commands, aber direkt in den Store) ──

    /// <summary>Eröffnet ein Konto unter der GEGEBENEN Id (Client-Append, Version 1). Explizite Id, damit der
    /// Äquivalenz-Test dieselbe Topologie durch zwei Manager (Cursor AUS/AN) treiben kann → gleiche Vorgänge.</summary>
    public async Task EröffneKontoAsync(Guid id, decimal startSaldo, bool gesperrt = false)
        => await Store.AppendEventsAsync(id, 0,
            new IEvent[] { new Domain.Konto.KontoEroeffnet(startSaldo, gesperrt) },
            correlationId: Guid.NewGuid().ToString(), causationId: Guid.NewGuid().ToString(), aggregateType: "Konto");

    /// <summary>Legt ein Auslöse-Event in den Auftrags-Stream <paramref name="stream"/> (Version 1).</summary>
    public async Task AuslöserAsync(Guid stream, IEvent auslöser, string aggregateType)
        => await Store.AppendEventsAsync(stream, 0, new[] { auslöser },
            correlationId: Guid.NewGuid().ToString(), causationId: Guid.NewGuid().ToString(), aggregateType: aggregateType);

    // ── Treiben: Manager wecken, bis terminal ──

    public async Task RunAsync(
        Guid korrelation, string prozessName, Guid auslöserStream, int auslöserVersion,
        int maxSchritte = 1_000_000, CancellationToken ct = default)
    {
        await _manager.StarteAsync(korrelation, prozessName, auslöserStream, auslöserVersion, ct);
        for (int i = 0; i < maxSchritte; i++)
        {
            var status = await _manager.LadeStatusAsync(korrelation, ct);
            if (status.Beendet) return;
            await _manager.WakeAsync(korrelation, ct);
        }
        throw new InvalidOperationException($"Prozess {prozessName} nicht terminal nach {maxSchritte} Schritten (Livelock?).");
    }

    /// <summary>Treibt den Prozess NUR <paramref name="wakes"/> Weckungen weit (StarteAsync + N Weckungen) —
    /// für Proben, die einen AKTIVEN (nicht-terminalen) Prozess brauchen. Liefert, ob er schon terminal ist.</summary>
    public async Task<bool> TeilRunAsync(
        Guid korrelation, string prozessName, Guid auslöserStream, int auslöserVersion, int wakes, CancellationToken ct = default)
    {
        await _manager.StarteAsync(korrelation, prozessName, auslöserStream, auslöserVersion, ct);
        for (int i = 0; i < wakes; i++)
        {
            if ((await _manager.LadeStatusAsync(korrelation, ct)).Beendet) return true;
            await _manager.WakeAsync(korrelation, ct);
        }
        return (await _manager.LadeStatusAsync(korrelation, ct)).Beendet;
    }

    /// <summary>
    /// Eine EINZELNE Weckung mit einem FRISCHEN Manager (leerer In-Memory-Cache) über DEMSELBEN Event-Store,
    /// aber mit einem übergebenen (evtl. bewusst stale) Marking-Store — die §3-Probe „Duplikat verpufft":
    /// ein veraltetes Marking feuert eine bereits erledigte Transition erneut, die beim Empfänger (Framework-
    /// Inbox) verpufft. Liefert die Zahl der in dieser Weckung gefeuerten Transitionen.
    /// </summary>
    public async Task<int> ExtraWeckungAsync(Guid korrelation, IProzessMarkingStore markingStore, CancellationToken ct = default)
    {
        var vorher = Gefeuert.Count;
        var frisch = new ProzessManager(Store, _registry, DispatchAsync, offenIndex: null, deadLetters: null, markingStore: markingStore);
        await frisch.WakeAsync(korrelation, ct);
        return Gefeuert.Count - vorher;
    }

    // ── Der Dispatch-Seam: emittierten Command an das echte Konto-Aggregat routen (Framework-Inbox faithful) ──

    private async Task DispatchAsync(Guid korrelation, ICommand cmd, Guid vorgang, CancellationToken ct)
    {
        Gefeuert.Add((cmd.GetType().Name, vorgang, cmd.AggregateId));

        var ziel = cmd.AggregateId;
        var reh = await AggregateRehydrator.LoadAsync<Domain.Konto.Konto>(
            ziel, snapshots: null, Store, _factory, expectedSchemaVersion: null, ct);
        var state = reh.State;

        // Framework-Inbox (Zwei-Mengen): verarbeitet → Noop-Erfolg; abgelehnt → bleibt abgelehnt. Kein Append.
        if (reh.ProcessedCommandIds.Contains(vorgang) || reh.RejectedCommandIds.Contains(vorgang))
            return;

        var handler = _factory.CreateHandler(state);
        var events = handler.HandleCommand(cmd).ToList();
        var persistent = events.Where(e => e is not ITransientEvent).ToList();
        var ablehnungen = events.Where(e => e is ITransientEvent).ToList();

        IReadOnlyList<IEvent> zuSchreiben;
        if (ablehnungen.Any() && !persistent.Any())
            // Reine Ablehnung → durable KommandoAbgelehnt-Marke (Treiber-Fold/EM-1).
            zuSchreiben = new IEvent[] { new KommandoAbgelehnt(vorgang, ablehnungen.First().GetType().Name) };
        else
            // Erfolg (auch Noop) → persistente Events + co-committete KommandoVerarbeitet-Marke.
            zuSchreiben = persistent.Append((IEvent)new KommandoVerarbeitet(vorgang)).ToList();

        await Store.AppendEventsAsync(
            ziel, state.Version, zuSchreiben,
            correlationId: korrelation.ToString(),
            causationId: vorgang.ToString(),
            aggregateType: "Konto");
    }
}
