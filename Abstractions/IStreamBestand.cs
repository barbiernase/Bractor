namespace Abstractions;

/// <summary>
/// Fähigkeit: gebündelt nachsehen, welche Streams es im Log schon gibt (z. B. der Start-Abgleich eines Quellen-Treibers — die
/// Wahrheit ist der Log, ein Treiber hat keinen eigenen Stand). Eine eigene schmale Schnittstelle statt einer weiteren Methode am
/// Repository.
/// </summary>
public interface IStreamBestand
{
    /// <summary>Die Teilmenge von <paramref name="streams"/>, die im Log existiert.</summary>
    Task<IReadOnlySet<Guid>> BekannteAsync(IReadOnlyCollection<Guid> streams, CancellationToken ct = default);
}
