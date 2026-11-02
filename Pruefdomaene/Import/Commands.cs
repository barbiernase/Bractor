using Abstractions;

namespace Pruefdomaene.Import;

public record StarteImport(Id<Importauftrag> AggregateId, int Zeilen) : ICommand<Importauftrag>, ICreationCommand;
public record MeldeImportStand(Id<Importauftrag> AggregateId, int Zeilen) : ICommand<Importauftrag>;

/// <summary>Der Import ist gestartet: seine Zeilen sind zu lesen und als Posten anzulegen.</summary>
public record ImportZuLaufen(Id<Importauftrag> Import, int Zeilen) : ICommand;
