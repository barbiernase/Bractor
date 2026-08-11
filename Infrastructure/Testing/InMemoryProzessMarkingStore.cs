using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;

namespace Infrastructure.Testing;

/// <summary>
/// Store-freie <see cref="IProzessMarkingStore"/>-Implementierung für den Prüfstand (in-memory). Best-effort
/// wie die Marten-Variante, nur ohne Persistenz. Legt bei Laden UND Schreiben eine tiefe Kopie an — so teilen
/// gespeicherte und benutzte <see cref="MarkingKompakt"/>-Instanz nie dieselbe (mutierbare) Referenz, exakt
/// wie ein echter Round-Trip durch jsonb.
/// </summary>
public sealed class InMemoryProzessMarkingStore : IProzessMarkingStore
{
    private readonly ConcurrentDictionary<Guid, ProzessMarking> _markings = new();

    public Task<ProzessMarking?> LadeAsync(Guid korrelation, CancellationToken ct)
        => Task.FromResult(_markings.TryGetValue(korrelation, out var m) ? Kopie(m) : null);

    public Task SchreibeAsync(ProzessMarking marking, CancellationToken ct)
    {
        _markings[marking.Id] = Kopie(marking);
        return Task.CompletedTask;
    }

    public Task LöscheAsync(Guid korrelation, CancellationToken ct)
    {
        _markings.TryRemove(korrelation, out _);
        return Task.CompletedTask;
    }

    private static ProzessMarking Kopie(ProzessMarking m) => new()
    {
        Id = m.Id,
        RegelHash = m.RegelHash,
        LogVersion = m.LogVersion,
        UpdatedAt = m.UpdatedAt,
        Marking = m.Marking.Kopie(),
    };
}
