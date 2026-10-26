using Abstractions;

namespace Pruefdomaene.Auftrag;

public record StarteAuftrag(Id<Auftrag> AggregateId, Id<Posten.Posten> PostenId, Id<Kontingent.Kontingent> KontingentId, int Menge) : ICommand<Auftrag>, ICreationCommand;

public record BestaetigeAuftrag(Id<Auftrag> AggregateId) : ICommand<Auftrag>;
