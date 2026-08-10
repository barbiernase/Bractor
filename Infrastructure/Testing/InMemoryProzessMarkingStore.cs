using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;

namespace Infrastructure.Testing;

/// <summary>
/// Store-freie <see cref="IProzessMarkingStore"/>-Implementierung für den Prüfstand (in-memory).
/// Best-effort wie die Marten-Variante — hält die lebenden <see cref="ProzessMarking"/>-Objekte
/// direkt (kein Serialisierungs-Umweg). Der <see cref="Zaehler"/> zählt Load/Save für die Proben.
/// </summary>
public sealed class InMemoryProzessMarkingStore : IProzessMarkingStore
{
    private readonly ConcurrentDictionary<Guid, ProzessMarking> _markings = new();

    public long Loads;
    public long Saves;

    public Task<ProzessMarking?> LadeAsync(Guid korrelation, CancellationToken ct)
    {
        Interlocked.Increment(ref Loads);
        return Task.FromResult(_markings.TryGetValue(korrelation, out var m) ? m : null);
    }

    public Task SchreibeAsync(ProzessMarking marking, CancellationToken ct)
    {
        Interlocked.Increment(ref Saves);
        _markings[marking.Id] = marking;
        return Task.CompletedTask;
    }
}
