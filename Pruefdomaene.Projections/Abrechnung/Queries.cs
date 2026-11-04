using Abstractions;
using Pruefdomaene.Abrechnung;

namespace Pruefdomaene.Projections.Abrechnung;

public record HoleRechnungsbild(Id<Rechnung> Rechnung) : IQuery;

public record HoleLage : IQuery;
