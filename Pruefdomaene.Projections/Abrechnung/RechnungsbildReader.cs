using Abstractions;

namespace Pruefdomaene.Projections.Abrechnung;

/// <summary>Reader des Rechnungsbilds: je Rechnung ihr Bild und die Lage.</summary>
public sealed class RechnungsbildReader : IReader
{
    public Task<Rechnungsbild> Frage(HoleRechnungsbild query, IHoleRechnungsbild hole) => hole.HoleAsync(query.Rechnung);

    public Task<Lagebild> Frage(HoleLage query, IHoleLage hole) => hole.HoleAsync();
}
