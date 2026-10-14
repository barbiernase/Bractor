using Abstractions;

namespace Pruefdomaene.Verteilung;

public record StarteVerteilung(Guid AggregateId, Guid QuelleId, IReadOnlyList<Guid> Ziele, int MengeJeZiel) : ICreationCommand;

public record SchliesseVerteilungAb(Guid AggregateId, int Zugebucht) : ICommand;
