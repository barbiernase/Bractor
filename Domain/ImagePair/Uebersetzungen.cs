using Abstractions;
using Bildvorschau.Katalog.Aufbereitung;

namespace Domain.ImagePair;

// ÜBERSETZUNGEN (docs/plan-ohne-pipeline.md): sie machen die domänenneutralen Graphen aus den Katalogen zu Bildpaaren. Wohin sie gehören, sagt nur der
// Typ, den sie nehmen — hier jedes gedeutete Bild, aus welcher Instanz der Bildaufbereitung bzw. des Linienimports auch immer.

/// <summary>
/// Die Bildaufnahme: das gedeutete Bild wird ein Paar — die Id aus der Guid des Schlüssels, alles andere Feld für Feld nach Namen.
/// </summary>
[Feld(nameof(NimmBildAuf.AggregateId), nameof(BildGedeutet.Paar))]
public sealed record Aufnehmen : IUebersetzung<BildGedeutet, NimmBildAuf>;
