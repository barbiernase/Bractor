using Abstractions;

namespace Bildvorschau.Katalog.Aufbereitung;

/// <summary>
/// Die Bildaufbereitung (docs/04 §4.4) als KETTE im Katalog: eine erkannte Datei wird gelesen, verkleinert, im Kontrast ausgeglichen (die Vorschau)
/// und nach der Aufnahme-Konvention gedeutet; jede Nachricht trägt die Datei der Quelle mit. Der Kopf nimmt <see cref="DateiGefunden"/> (die Meldung
/// des Dateiwächters); jede Kante steht am nehmenden Knoten (Einlesen → Verkleinern → Kontrast → Deuten). Hinaus gehen <see cref="BildGedeutet"/> und
/// die Hinweise <see cref="BildNichtLesbar"/>, <see cref="DateinameUnbekannt"/> — was daraus in einer Domäne wird, entscheidet der Ablauf, der den
/// Graphen bindet; jeder Zwischenstand lässt sich dort ebenso abgreifen.
/// </summary>
public sealed record Bildaufbereitung(
    [Kopf] Bildeinlesen Einlesen,
    [Von(nameof(Einlesen))] Bildverkleinerung Verkleinern,
    [Von(nameof(Verkleinern))] Kontrastausgleich Kontrast,
    [Von(nameof(Kontrast))] Dateinamendeutung Deuten) : IGraph;
