using Abstractions;
using Marten;

namespace Infrastructure.Persistence;

/// <summary>
/// Marten-basierter <see cref="IPollCursorStore"/>: ein durables <see cref="PollCursor"/>-
/// Dokument pro Pull-Pfad. Eigene, kurze Session je Zugriff (der Cursor ist unkritisch —
/// ein Verlust bedeutet höchstens ein einmaliges Re-Scannen, keine Datenkorruption).
///
/// Marten-Registrierung nötig (in der StoreOptions-Konfiguration):
///   opts.Schema.For&lt;PollCursor&gt;().Identity(x =&gt; x.Id);
/// </summary>
public sealed class MartenPollCursorStore : IPollCursorStore
{
    private readonly IDocumentStore _store;

    public MartenPollCursorStore(IDocumentStore store)
        => _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<long> GetAsync(string pollerId, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var doc = await session.LoadAsync<PollCursor>(pollerId, ct);
        return doc?.HighWater ?? 0;
    }

    public async Task SetAsync(string pollerId, long highWater, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        session.Store(new PollCursor
        {
            Id = pollerId,
            HighWater = highWater,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await session.SaveChangesAsync(ct);
    }
}
