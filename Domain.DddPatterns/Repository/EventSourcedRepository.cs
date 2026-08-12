using Abstractions;

namespace Domain.DddPatterns.Repository;

/// <summary>
/// Wird geworfen, wenn zwei Schreiber denselben Aggregat-Stream gleichzeitig ändern
/// wollen (optimistische Nebenläufigkeit) — die fachliche Entsprechung zur
/// Event-Store-Version-Kollision.
/// </summary>
public sealed class NebenlaeufigkeitsKonflikt : Exception
{
    public NebenlaeufigkeitsKonflikt(Guid id, int erwartet, int tatsaechlich)
        : base($"Nebenläufigkeitskonflikt auf {id}: erwartet Version {erwartet}, gefunden {tatsaechlich}.") { }
}

/// <summary>
/// Generische, event-sourced In-Memory-Umsetzung von <see cref="IRepository{TAggregat}"/>.
/// Speichert je Aggregat einen Ereignis-Stream; <see cref="LadeAsync"/> faltet ihn per
/// <c>ausHistorie</c> zum Zustand, <see cref="SpeichereAsync"/> hängt die neuen Ereignisse
/// an und prüft dabei die erwartete Version (Optimistic Concurrency). Store-frei — genau
/// die Ebene, auf der der Prüfstand Logik prüft.
/// </summary>
public sealed class EventSourcedRepository<TAggregat, TEreignis> : IRepository<TAggregat>
    where TAggregat : class, IState, IEreignisGespeist<TEreignis>
{
    private readonly Dictionary<Guid, List<TEreignis>> _streams = new();
    private readonly Func<IEnumerable<TEreignis>, TAggregat> _ausHistorie;

    public EventSourcedRepository(Func<IEnumerable<TEreignis>, TAggregat> ausHistorie)
        => _ausHistorie = ausHistorie;

    public Task<TAggregat?> LadeAsync(Guid id, CancellationToken ct = default)
    {
        if (!_streams.TryGetValue(id, out var stream) || stream.Count == 0)
            return Task.FromResult<TAggregat?>(null);
        return Task.FromResult<TAggregat?>(_ausHistorie(stream));
    }

    public Task SpeichereAsync(TAggregat aggregat, CancellationToken ct = default)
    {
        var neue = aggregat.NeueEreignisse;
        if (neue.Count == 0) return Task.CompletedTask;

        // Basis-Version = aktuelle Version minus der noch nicht persistierten Ereignisse.
        var basisVersion = aggregat.Version - neue.Count;
        var stream = _streams.TryGetValue(aggregat.Id, out var vorhanden) ? vorhanden : new List<TEreignis>();

        if (stream.Count != basisVersion)
            throw new NebenlaeufigkeitsKonflikt(aggregat.Id, basisVersion, stream.Count);

        stream.AddRange(neue);
        _streams[aggregat.Id] = stream;
        aggregat.EreignisseUebernommen();
        return Task.CompletedTask;
    }
}
