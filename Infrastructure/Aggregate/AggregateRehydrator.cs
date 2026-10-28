using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;

namespace Infrastructure.Aggregate;

/// <summary>
/// Das Ergebnis der Aggregat-Rehydration: der gefaltete State und die Stream-Position des zugrunde gelegten Snapshots (0, wenn ohne
/// Snapshot voll gefaltet wurde).
/// </summary>
public sealed record RehydrationResult<TState>(TState State, int SnapshotVersion)
    where TState : class, IState, new();

/// <summary>
/// Rehydriert ein Aggregat in EINEM Tail-Fold aus einem einzigen Stream-Read (docs/04-konsum-und-prozess-maschine.md §4.8); mit Snapshot
/// seedet der Fold und liest nur den Rest. Die Inbox gehört nicht dazu — sie ist eine eigene Tabelle des Stores (docs/04 §4.6).
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
        CancellationToken ct)
        where TState : class, IState, new()
    {
        // 1. Snapshot-Cache konsultieren. Stale (Schema-Mismatch) → ignorieren, voll replayen (Invariante 1).
        var snap = snapshots is null ? null : await snapshots.TryLoadAsync<TState>(id, ct);
        if (snap != null && expectedSchemaVersion != null && snap.SchemaVersion != expectedSchemaVersion)
            snap = null;

        // 2. Seed aus dem Snapshot (oder leer).
        var state = snap?.State ?? new TState { Id = id, Version = 0 };
        var baseVersion = snap?.Version ?? 0;

        // 3. Handler JETZT erstellen — mit dem geseedeten State, damit Applier auf dasselbe Objekt zeigt.
        var handler = factory.CreateHandler(state);

        // 4. Tail ab baseVersion+1 falten. Jede Hülle — auch die des Rahmens (Frist gesetzt) — zählt die Version hoch
        //    (Stream-Position bündig), angewandt werden nur Domänen-Events.
        var tail = await store.ReadStreamAsync(id, baseVersion + 1, ct);
        foreach (var env in tail)
        {
            if (env.Payload is not IRahmenEvent)
                handler.ApplyEvent(env.Payload);
            state.Version++;
        }

        state.Id = id;
        return new RehydrationResult<TState>(state, baseVersion);
    }
}
