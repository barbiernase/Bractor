using Abstractions;

namespace Pruefdomaene.Pipeline.Messung;

/// <summary>
/// Aus einem neuen Wert wird ein Merkmal, das gabelt (ein Fork): die Ausreißerprüfung meldet einen Wert über der Schwelle, der
/// Schätzer schätzt (mit Zustand: genau eine Aktivierung der Instanz im Cluster).
/// </summary>
public sealed record Merkmalbildung([Kopf] Merkmalsbildung Bildung, [Von(nameof(Bildung))] Ausreisserpruefung Pruefung,
    [Von(nameof(Bildung))] Schaetzer Schaetzer) : IGraph;
