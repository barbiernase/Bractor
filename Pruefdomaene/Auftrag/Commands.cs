using Abstractions;

namespace Pruefdomaene.Auftrag;

public record StarteAuftrag(Guid AggregateId, Guid PostenId, Guid KontingentId, int Menge) : ICreationCommand;

public record BestaetigeAuftrag(Guid AggregateId) : ICommand;
