using Abstractions;

namespace Pruefdomaene.Projections.Messung;

/// <summary>Reader der Messungs-Projektionen: die Messreihe (Klasse 2) und der Zähler (Klasse 3).</summary>
public sealed class MessungReader : IReader
{
    public Task<Messreihe> Frage(HoleMessreihe query, IHoleMessreihe hole) => hole.HoleAsync(query.Stelle);

    public Task<Messzaehler> Frage(HoleMesszaehler query, ILiesMesszaehler lies) => lies.LiesAsync(query.Stelle);
}
