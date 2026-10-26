using Abstractions;

namespace Domain.Teilauftrag;

public record TeilauftragGestartet(
    Id<Sammelvorgang.Sammelvorgang> SammelvorgangId
) : IEvent;

/// <summary>Teil fertig — trägt die <see cref="SammelvorgangId"/> als Korrelation, damit der Prozess weiß, an welcher Barriere er meldet.</summary>
public record TeilauftragAbgeschlossen(
    Id<Sammelvorgang.Sammelvorgang> SammelvorgangId
) : IEvent;

public record TeilauftragExistiertBereits(Id<Teilauftrag> TeilauftragId) : ITransientEvent;
public record TeilauftragNichtGefunden(Id<Teilauftrag> TeilauftragId) : ITransientEvent;
public record TeilauftragBereitsFertig(Id<Teilauftrag> TeilauftragId) : ITransientEvent;
