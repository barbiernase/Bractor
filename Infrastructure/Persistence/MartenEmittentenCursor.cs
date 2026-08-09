using Abstractions;
using Marten;

namespace Infrastructure.Persistence;

/// <summary>
/// Marten-basierter <see cref="IEmittentenCursor"/>: ein durables <see cref="EmittentenCursorDoc"/>-
/// Dokument pro Partition. Eigene, kurze Session je Zugriff — bewusst <b>kein</b> Co-Commit mit dem
/// Effekt (der Effekt eines Emittenten IST das idempotente Command am Empfänger, nicht ein
/// co-committetes Read-Model). Der Cursor ist reiner Fortschritts-Cache: geht er verloren oder ist er
/// inkonsistent, liest der Konsument wieder ab 0 und der Empfänger dedupliziert über die Inbox — kein
/// Datenverlust, höchstens ein einmaliges Re-Emit.
///
/// Marten-Registrierung nötig (in der StoreOptions-Konfiguration):
///   opts.Schema.For&lt;EmittentenCursorDoc&gt;().Identity(x =&gt; x.Id);
/// </summary>
public sealed class MartenEmittentenCursor : IEmittentenCursor
{
    private readonly IDocumentStore _store;

    public MartenEmittentenCursor(IDocumentStore store)
        => _store = store ?? throw new ArgumentNullException(nameof(store));

    public async Task<long> LadeAsync(string partition, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        var doc = await session.LoadAsync<EmittentenCursorDoc>(partition, ct);
        return doc?.Position ?? 0;
    }

    public async Task SchreibeAsync(string partition, long bis, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();
        session.Store(new EmittentenCursorDoc
        {
            Id = partition,
            Position = bis,
            UpdatedAt = DateTimeOffset.UtcNow
        });
        await session.SaveChangesAsync(ct);
    }
}
