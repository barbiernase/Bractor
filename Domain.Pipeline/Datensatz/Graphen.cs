using Abstractions;

namespace Domain.Pipeline.Datensatz;

/// <summary>Die Pflege eines Datensatzes: eine Range auflösen und das Einfrieren abschließen — zwei Köpfe, je ein Command aus dem Decide des Datensatzes.</summary>
public sealed record Datensatzpflege(RangeAufloesung Aufloesung, MitgliederEinfrieren Einfrieren) : IGraph;
