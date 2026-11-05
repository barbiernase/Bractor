using Abstractions;

namespace Domain.Pipeline.Datensatz;

/// <summary>Die Pflege eines Datensatzes: eine Range auflösen und das Einfrieren abschließen — zwei Köpfe, je ein Command aus dem Decide des Datensatzes.</summary>
public sealed record Datensatzpflege([Kopf] RangeAufloesung Aufloesung, [Kopf] MitgliederEinfrieren Einfrieren) : IGraph;

public sealed record NeuerGraph() : IGraph;

public sealed record NeuerGraph2() : IGraph;

public sealed record NeuerGraph3() : IGraph;
