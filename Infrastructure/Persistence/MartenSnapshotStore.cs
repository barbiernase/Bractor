using System;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Marten;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Persistence;

/// <summary>
/// Marten-<see cref="ISnapshotStore"/>: legt je Aggregat einen <c>Snapshot&lt;TState&gt;</c> als jsonb-Dokument
/// ab (Tabelle pro State-Typ, z. B. <c>es.mt_doc_snapshot_konto</c>). Identität = die Aggregat-GUID. Der State
/// steht als echtes, einfach-kodiertes, typisiertes nested jsonb drin (kein doppelt-kodierter String).
///
/// Generisch und winzig — die Dokumenttypen werden reflection-frei generiert registriert
/// (<c>RegisterGeneratedSnapshotTypes</c>); Marten löst den geschlossenen Generic <c>Snapshot&lt;TState&gt;</c>
/// selbst auf. Nicht-autoritativ (Invariante 1): Load ohne Treffer → null → Voll-Replay.
/// </summary>
public sealed class MartenSnapshotStore : ISnapshotStore
{
    private readonly IDocumentStore _store;
    private readonly ILogger<MartenSnapshotStore>? _logger;

    public MartenSnapshotStore(IDocumentStore store, ILogger<MartenSnapshotStore>? logger = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _logger = logger;
    }

    public async Task<Snapshot<TState>?> TryLoadAsync<TState>(Guid id, CancellationToken ct)
        where TState : class, IState, new()
    {
        await using var session = _store.LightweightSession();
        return await session.LoadAsync<Snapshot<TState>>(id, ct);
    }

    public async Task SaveAsync<TState>(Snapshot<TState> snapshot, CancellationToken ct)
        where TState : class, IState, new()
    {
        await using var session = _store.LightweightSession();
        session.Store(snapshot);
        await session.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync<TState>(Guid id, CancellationToken ct)
        where TState : class, IState, new()
    {
        await using var session = _store.LightweightSession();
        session.Delete<Snapshot<TState>>(id);
        await session.SaveChangesAsync(ct);
    }
}
