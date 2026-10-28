using Abstractions;
using Pruefdomaene.Messung;

namespace Pruefdomaene.Projections.Messung;

public record HoleMessreihe(Id<Messstelle> Stelle) : IQuery;

public record HoleMesszaehler(Id<Messstelle> Stelle) : IQuery;
