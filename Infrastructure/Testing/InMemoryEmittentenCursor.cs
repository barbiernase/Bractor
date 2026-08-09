using System.Collections.Concurrent;
using Abstractions;

namespace Infrastructure.Testing;

/// <summary>
/// Store-freie <see cref="IEmittentenCursor"/>-Implementierung für den Prüfstand (in-memory).
/// Best-effort wie die Marten-Variante, nur ohne Persistenz — monoton fortschreibend
/// (ein späteres <see cref="SchreibeAsync"/> mit kleinerer Position rückt NICHT zurück, damit
/// out-of-order-Weckungen den Cursor nicht senken).
/// </summary>
public sealed class InMemoryEmittentenCursor : IEmittentenCursor
{
    private readonly ConcurrentDictionary<string, long> _positionen = new();

    public Task<long> LadeAsync(string partition, CancellationToken ct)
        => Task.FromResult(_positionen.TryGetValue(partition, out var p) ? p : 0);

    public Task SchreibeAsync(string partition, long bis, CancellationToken ct)
    {
        _positionen.AddOrUpdate(partition, bis, (_, alt) => bis > alt ? bis : alt);
        return Task.CompletedTask;
    }
}
