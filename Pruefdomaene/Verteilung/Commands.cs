using Abstractions;

namespace Pruefdomaene.Verteilung;

public record StarteVerteilung(Id<Verteilung> AggregateId, Id<Posten.Posten> QuelleId, IReadOnlyList<Id<Posten.Posten>> Ziele, int MengeJeZiel) : ICommand<Verteilung>, ICreationCommand;

public record SchliesseVerteilungAb(Id<Verteilung> AggregateId, int Zugebucht) : ICommand<Verteilung>;
