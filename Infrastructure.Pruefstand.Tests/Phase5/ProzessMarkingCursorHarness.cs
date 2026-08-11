using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Infrastructure;                 // generierte AggregateHandlerFactory
using Infrastructure.Aggregate;       // KommandoVerarbeitet / KommandoAbgelehnt
using Domain.Konto;

namespace Infrastructure.Pruefstand.Phase5;

/// <summary>
/// Der in-memory Saga-Harness für P5b (Backend-Handoff §6): es gab bislang KEINEN store-freien
/// <c>ProzessManager</c>-Saga-Harness (die Sagas leben nur als Integrationstests). Dieser treibt den ECHTEN
/// <see cref="Infrastructure.Prozess.ProzessManager"/> gegen einen mutierbaren In-Memory-Store und einen
/// Dispatch, der emittierte Commands an die ECHTEN Aggregate (über die generierte
/// <see cref="AggregateHandlerFactory"/>) routet — inklusive der Framework-Inbox-Marken
/// (<see cref="KommandoVerarbeitet"/>/<see cref="KommandoAbgelehnt"/>), die der Manager-Fold liest.
///
/// So laufen ganze Sagas deterministisch, sequenziell (Spec §8), ohne Cluster — die Grundlage für den
/// Äquivalenz-Beweis „Cursor-Fold == Voll-Fold".
/// </summary>
internal sealed class MutableEventStore : IEventStoreRepository
{
    private readonly Dictionary<Guid, List<EventEnvelope>> _streams = new();

    /// <summary>Zähler der GELESENEN Ziel-Events (der O(N²)→O(N)-Beleg). Auslöser-/Log-Reads zählen mit, dominieren aber nicht.</summary>
    public long GeleseneEvents { get; private set; }

    public Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(Guid streamId, int fromVersion, CancellationToken ct)
    {
        IReadOnlyList<EventEnvelope> res = _streams.TryGetValue(streamId, out var evs)
            ? evs.Where(e => e.AggregateVersion >= fromVersion).OrderBy(e => e.AggregateVersion).ToList()
            : Array.Empty<EventEnvelope>();
        GeleseneEvents += res.Count;
        return Task.FromResult(res);
    }

    public Task AppendEventsAsync(
        Guid aggregateId, int expectedVersion, IReadOnlyList<IEvent> events,
        string? correlationId = null, string? causationId = null, string? aggregateType = null)
    {
        if (events.Count == 0) return Task.CompletedTask;
        if (!_streams.TryGetValue(aggregateId, out var stream)) { stream = new List<EventEnvelope>(); _streams[aggregateId] = stream; }

        // OCC wie Marten: expectedVersion == aktuelle Stream-Länge (1-basierte, lückenlose Versionen).
        if (expectedVersion != stream.Count)
            throw new ConcurrencyException($"OCC: erwartet {expectedVersion}, ist {stream.Count} (Stream {aggregateId}).");

        var basis = stream.Count;
        for (int i = 0; i < events.Count; i++)
        {
            stream.Add(new EventEnvelope
            {
                AggregateId = aggregateId,
                AggregateVersion = basis + i + 1,   // Marten ist 1-basiert (StartStream → v1..)
                Payload = events[i],
                CausationId = causationId ?? Guid.NewGuid().ToString(),
                CorrelationId = correlationId ?? Guid.NewGuid().ToString(),
                AggregateType = aggregateType ?? "",
            });
        }
        return Task.CompletedTask;
    }

    public Task<StreamChanges> ReadChangedStreamsAsync(long after, CancellationToken ct)
        => Task.FromResult(new StreamChanges(_streams.Keys.ToList(), _streams.Values.Sum(v => v.Count)));

    public Task<TState?> LoadStateAsync<TState>(Guid id) where TState : class, IState, new()
        => throw new NotSupportedException();
}

/// <summary>
/// Die Ziel-Aggregat-Welt des Harness (nur Konto-Ziele — die Piloten-Sagas buchen ausschließlich auf Konten).
/// Der Dispatch bildet den EMITTIERTEN Pfad des <c>AggregateActorBase</c> getreu nach: Dedup über den Vorgang
/// (Framework-Inbox), Noop-/Ablehnungs-/Erfolgs-Marken durabel auf dem Ziel-Stream, CausationId == Vorgang.
/// </summary>
internal sealed class KontoWelt
{
    private readonly MutableEventStore _store;
    private readonly AggregateHandlerFactory _factory = new();
    private readonly Dictionary<Guid, Lauf> _läufe = new();

    /// <summary>Die gefeuerte Sequenz (Command-Typ, Ziel, Vorgang) — der Vergleichsgegenstand des Äquivalenz-Tests.</summary>
    public List<(string Cmd, Guid Ziel, Guid Vorgang)> Feuerungen { get; } = new();

    public KontoWelt(MutableEventStore store) => _store = store;

    private sealed class Lauf
    {
        public required IState State;
        public required IAggregateHandler Handler;
        public readonly HashSet<Guid> Verarbeitet = new();
        public readonly HashSet<Guid> Abgelehnt = new();
    }

    private Lauf Hole(Guid id)
    {
        if (!_läufe.TryGetValue(id, out var lauf))
        {
            var state = new Konto { Id = id };
            lauf = new Lauf { State = state, Handler = _factory.CreateHandler(state) };
            _läufe[id] = lauf;
        }
        return lauf;
    }

    /// <summary>Seedet ein Konto (Client-Pfad, KontoEroeffnet als v1 des Streams).</summary>
    public async Task EröffneKontoAsync(Guid id, decimal saldo, bool gesperrt = false)
    {
        var lauf = Hole(id);
        var evs = lauf.Handler.HandleCommand(new EroeffneKonto(id, saldo, gesperrt)).ToList();
        await _store.AppendEventsAsync(id, 0, evs, correlationId: id.ToString(), causationId: Guid.NewGuid().ToString(), aggregateType: "Konto");
        foreach (var e in evs) { lauf.Handler.ApplyEvent(e); lauf.State.Version++; }
    }

    /// <summary>Der Prozess-Manager-Dispatch: emittierten Command am Ziel-Aggregat verarbeiten (idempotent, durabel).</summary>
    public async Task DispatchAsync(Guid korrelation, ICommand cmd, Guid vorgang, CancellationToken ct)
    {
        Feuerungen.Add((cmd.GetType().Name, cmd.AggregateId, vorgang));
        var lauf = Hole(cmd.AggregateId);

        // Framework-Inbox: ein bereits verarbeiteter/abgelehnter Vorgang verpufft (at-least-once → exactly-once).
        if (lauf.Verarbeitet.Contains(vorgang) || lauf.Abgelehnt.Contains(vorgang)) return;

        var alle = lauf.Handler.HandleCommand(cmd).ToList();
        var erwartet = lauf.State.Version;

        if (alle.Count == 0)   // Noop → nur die KommandoVerarbeitet-Marke (K2-Livelock-Fix)
        {
            await _store.AppendEventsAsync(cmd.AggregateId, erwartet,
                new IEvent[] { new KommandoVerarbeitet(vorgang) }, korrelation.ToString(), vorgang.ToString(), "Konto");
            lauf.State.Version++; lauf.Verarbeitet.Add(vorgang);
            return;
        }

        var persistent = alle.Where(e => e is not ITransientEvent).ToList();
        var ablehnungen = alle.Where(e => e is ITransientEvent).ToList();

        if (ablehnungen.Count > 0 && persistent.Count == 0)   // reine Ablehnung → durable KommandoAbgelehnt-Marke
        {
            await _store.AppendEventsAsync(cmd.AggregateId, erwartet,
                new IEvent[] { new KommandoAbgelehnt(vorgang, ablehnungen[0].GetType().Name) }, korrelation.ToString(), vorgang.ToString(), "Konto");
            lauf.State.Version++; lauf.Abgelehnt.Add(vorgang);
            return;
        }

        // Erfolg → Domänen-Events + co-committete KommandoVerarbeitet-Marke.
        var zuSchreiben = persistent.Append((IEvent)new KommandoVerarbeitet(vorgang)).ToList();
        await _store.AppendEventsAsync(cmd.AggregateId, erwartet, zuSchreiben, korrelation.ToString(), vorgang.ToString(), "Konto");
        foreach (var e in persistent) { if (e is not IProzessIntern) lauf.Handler.ApplyEvent(e); lauf.State.Version++; }
        lauf.State.Version++;   // die Marke
        lauf.Verarbeitet.Add(vorgang);
    }

    /// <summary>Aktueller Saldo eines Konten-Ziels (für Effekt-Assertions, z. B. „Duplikat verpufft").</summary>
    public decimal Saldo(Guid id) => ((Konto)Hole(id).State).Saldo;
}
