using Abstractions;

namespace Domain.Teilauftrag;

/// <summary>Legt ein Teil an und bindet es (per <paramref name="SammelvorgangId"/>) an seine Barriere.</summary>
public record StarteTeilauftrag(
    Guid AggregateId,
    Guid SammelvorgangId
) : ICreationCommand;

/// <summary>Das Teil ist fertig (in echt: der Worker meldet das).</summary>
public record SchließeTeilauftragAb(
    Guid AggregateId
) : ICommand;
