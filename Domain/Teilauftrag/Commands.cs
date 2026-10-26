using Abstractions;

namespace Domain.Teilauftrag;

/// <summary>Legt ein Teil an und bindet es (per <paramref name="SammelvorgangId"/>) an seine Barriere.</summary>
public record StarteTeilauftrag(
    Id<Teilauftrag> AggregateId,
    Id<Sammelvorgang.Sammelvorgang> SammelvorgangId
) : ICommand<Teilauftrag>, ICreationCommand;

/// <summary>Das Teil ist fertig (in echt: der Worker meldet das).</summary>
public record SchließeTeilauftragAb(
    Id<Teilauftrag> AggregateId
) : ICommand<Teilauftrag>;
