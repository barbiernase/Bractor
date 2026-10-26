using Abstractions;

namespace Pruefdomaene.Import;

public record StarteImport(Id<Importauftrag> AggregateId, int Zeilen) : ICommand<Importauftrag>, ICreationCommand;
public record MeldeImportStand(Id<Importauftrag> AggregateId, int Zeilen) : ICommand<Importauftrag>;
