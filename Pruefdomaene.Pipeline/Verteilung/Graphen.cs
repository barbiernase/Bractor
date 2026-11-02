using Abstractions;

namespace Pruefdomaene.Pipeline.Verteilung;

/// <summary>Der Ablauf einer Verteilung: die Quelle reservieren, die Ziele zubuchen, jede Zubuchung zählen.</summary>
public sealed record Verteilungsablauf(Verteilabwicklung Abwicklung) : IGraph;
