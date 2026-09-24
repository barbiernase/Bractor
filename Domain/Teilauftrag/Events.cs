using Abstractions;

namespace Domain.Teilauftrag;

public record TeilauftragGestartet(
    Guid SammelvorgangId
) : IEvent;

/// <summary>Teil fertig — trägt die <see cref="SammelvorgangId"/> als Korrelation, damit der Prozess weiß, an welcher Barriere er meldet.</summary>
public record TeilauftragAbgeschlossen(
    Guid SammelvorgangId
) : IEvent;

public record TeilauftragExistiertBereits(Guid TeilauftragId) : ITransientEvent;
public record TeilauftragNichtGefunden(Guid TeilauftragId) : ITransientEvent;
public record TeilauftragBereitsFertig(Guid TeilauftragId) : ITransientEvent;
