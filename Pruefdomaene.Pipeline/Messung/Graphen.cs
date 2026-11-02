using Abstractions;

namespace Pruefdomaene.Pipeline.Messung;

/// <summary>
/// Aus einem neuen Wert wird ein Merkmal, das gabelt (ein Fork): die Ausreißerprüfung meldet einen Wert über der Schwelle, der
/// Schätzer schätzt (mit Zustand: genau eine Aktivierung der Instanz im Cluster).
/// </summary>
public sealed record Merkmalbildung(Merkmalsbildung Bildung, Ausreisserpruefung Pruefung, Schaetzer Schaetzer) : IGraph;
