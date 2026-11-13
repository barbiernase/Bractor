using Abstractions;

namespace Bildvorschau.Katalog;

/// <summary>
/// Die Bildaufbereitung (docs/04 §4.4) als KETTE im Katalog: eine erkannte Datei wird gelesen, verkleinert, im Kontrast ausgeglichen (die Vorschau)
/// und nach der Aufnahme-Konvention gedeutet; jede Nachricht trägt die Datei mit. Der Kopf nimmt <see cref="LiesBild"/> — von einem Akteur, etwa
/// einem Kamerasystem, dessen Dateiwächter die Datei fand (eine Übersetzung der Akteur-Seite macht aus der Meldung diesen Command); jede Kante steht am nehmenden Knoten (Einlesen → Verkleinern → Kontrast → Deuten). Hinaus gehen <see cref="BildGedeutet"/> und
/// die Hinweise <see cref="BildNichtLesbar"/>, <see cref="DateinameUnbekannt"/> — was daraus in einer Domäne wird, entscheiden ihre Übersetzungen
/// (nach dem Typ, auch für jeden Zwischenstand).
/// </summary>
public sealed record Bildaufbereitung(
    [Kopf] Bildeinlesen Einlesen,
    [Von(nameof(Einlesen))] Bildverkleinerung Verkleinern,
    [Von(nameof(Verkleinern))] Kontrastausgleich Kontrast,
    [Von(nameof(Kontrast))] Dateinamendeutung Deuten) : IGraph;
