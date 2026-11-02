using Abstractions;

namespace Pruefdomaene.Pipeline.Messung;

/// <summary>Aus einem neuen Wert wird ein Merkmal (ein Hinweis).</summary>
public sealed record Merkmalbildung(Merkmalsbildung Bildung) : IGraph;

/// <summary>Ein Merkmal über der Schwelle wird als Ausreißer gemeldet (im Host oder draußen).</summary>
public sealed record Ausreisserkontrolle(IAusreisserpruefung Pruefung) : IGraph;

/// <summary>Aus einem Merkmal wird eine Schätzung (mit Zustand: genau eine Aktivierung im Cluster).</summary>
public sealed record Schaetzung(Schaetzer Schaetzer) : IGraph;
