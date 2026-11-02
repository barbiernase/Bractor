using Abstractions;

namespace Domain.Pipeline.ImagePair;

/// <summary>Ein komplettes Paar wird beurteilt — draußen, beim Klassifikations-Worker.</summary>
public sealed record Paarbeurteilung(IBildpaarBeurteilung Beurteilung) : IGraph;
