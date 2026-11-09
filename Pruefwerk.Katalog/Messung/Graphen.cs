using Abstractions;

namespace Pruefwerk.Katalog.Messung;

/// <summary>Die Merkmalbildung: das Merkmal am Kopf, dahinter eine Gabel — geprüft und geschätzt (der Schätzer mit Zustand im Zählwerk).</summary>
public sealed record Merkmalbildung([Kopf] Merkmalsbildung Bildung, [Von(nameof(Bildung))] Ausreisserpruefung Pruefung,
    [Von(nameof(Bildung))] Schaetzer Schaetzer) : IGraph;
