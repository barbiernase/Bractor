using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;

namespace Infrastructure.Aggregate;

/// <summary>
/// Das Ergebnis der Aggregat-Rehydration: der gefaltete State, die beiden Framework-Inboxen
/// (verarbeitet/abgelehnt) und die Stream-Position des zugrunde gelegten Snapshots (0, wenn ohne
/// Snapshot voll gefaltet wurde).
/// </summary>
public sealed record RehydrationResult<TState>(
    TState State,
    BoundedInbox ProcessedCommandIds,
    BoundedInbox RejectedCommandIds,
    int SnapshotVersion)
    where TState : class, IState, new();

/// <summary>
/// Rehydriert ein Aggregat in EINEM Tail-Fold — State UND Framework-Inbox aus einem einzigen
/// Stream-Read (Snapshot-Konzept §6, docs/snapshot-konzept.md). Ersetzt das frühere Doppel-Lesen
/// (LoadStateAsync + ReadStreamAsync(0)) im Actor; mit Snapshot seedet der Fold und liest nur den Rest.
///
/// In einen puren Helfer extrahiert, damit die Falt-Logik in-memory OHNE Proto-Actor beweisbar ist
/// (Prüfstand). Reflection-frei — der Applier kommt aus der generierten <see cref="IAggregateHandlerFactory"/>.
/// </summary>
public static class AggregateRehydrator
{
    public static async Task<RehydrationResult<TState>> LoadAsync<TState>(
        Guid id,
        ISnapshotStore? snapshots,
        IEventStoreRepository store,
        IAggregateHandlerFactory factory,
        string? expectedSchemaVersion,
        CancellationToken ct,
        int inboxCap = 10_000)   // ★ Audit-Fix #4/H3: harte Obergrenze der Framework-Inbox (letzte K)
        where TState : class, IState, new()
    {
        // 1. Snapshot-Cache konsultieren. Stale (Schema-Mismatch) → ignorieren, voll replayen (Invariante 1).
        var snap = snapshots is null ? null : await snapshots.TryLoadAsync<TState>(id, ct);
        if (snap != null && expectedSchemaVersion != null && snap.SchemaVersion != expectedSchemaVersion)
            snap = null;

        // 2. Seed aus dem Snapshot (oder leer). Die Inbox reist im Snapshot mit — sonst ginge die
        //    Dedup der vor dem Snapshot verarbeiteten Reaktions-/Prozess-Commands verloren. Gedeckelt (#4):
        //    Snapshot-Ids (bereits ≤cap, ältestes zuerst) seeden, der Tail-Fold hängt an — die letzten cap bleiben.
        var state = snap?.State ?? new TState { Id = id, Version = 0 };
        var inbox = BoundedInbox.FromOrdered(snap?.ProcessedCommandIds ?? Array.Empty<Guid>(), inboxCap);
        var abgelehnt = BoundedInbox.FromOrdered(snap?.RejectedCommandIds ?? Array.Empty<Guid>(), inboxCap);
        var baseVersion = snap?.Version ?? 0;

        // 3. Handler JETZT erstellen — mit dem geseedeten State, damit Applier auf dasselbe Objekt zeigt.
        var handler = factory.CreateHandler(state);

        // 4. Tail ab baseVersion+1 falten: State (Applier) und BEIDE Inboxen (Marken) in EINEM Durchlauf.
        //    Jede Hülle — auch die internen Marken — zählt die Version hoch (Stream-Position bündig).
        var tail = await store.ReadStreamAsync(id, baseVersion + 1, ct);
        foreach (var env in tail)
        {
            if (env.Payload is KommandoVerarbeitet marke)
                inbox.Add(marke.CommandId);
            else if (env.Payload is KommandoAbgelehnt ablehnung)
                abgelehnt.Add(ablehnung.CommandId);
            else if (env.Payload is not IProzessIntern)
                handler.ApplyEvent(env.Payload);
            state.Version++;
        }

        state.Id = id;
        return new RehydrationResult<TState>(state, inbox, abgelehnt, baseVersion);
    }
}
