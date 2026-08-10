using Abstractions;

namespace Infrastructure.Persistence;

/// <summary>
/// No-op-<see cref="IVersionTracker"/> — der Redis-Version-Index ist komplett AUS. Dient (a) der Messung
/// (isoliert den synchronen Redis-Round-Trip im Command-Turn aus der Durchsatzzahl heraus) und (b) als
/// gefahrlose Registrierung, wenn kein Redis läuft: der Index ist ohnehin nicht-autoritativ, sein einziger
/// Leser (<c>ReadModelDepsWriter</c>) verträgt das leere Ergebnis (behandelt fehlende Versionen wie „noch nicht
/// getrackt"). DI-Signatur bleibt erfüllt (ReadModelDepsWriter fordert IVersionTracker nicht-nullable).
/// </summary>
public sealed class NullVersionTracker : IVersionTracker
{
    private static readonly IReadOnlyDictionary<Guid, AggregateVersionInfo> Empty
        = new Dictionary<Guid, AggregateVersionInfo>();

    public Task TrackAsync(Guid aggregateId, string aggregateType, int newVersion)
        => Task.CompletedTask;

    public Task<AggregateVersionInfo?> GetAsync(Guid aggregateId)
        => Task.FromResult<AggregateVersionInfo?>(null);

    public Task<IReadOnlyDictionary<Guid, AggregateVersionInfo>> GetManyAsync(
        IReadOnlyList<Guid> aggregateIds)
        => Task.FromResult(Empty);
}
