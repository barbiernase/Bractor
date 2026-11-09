using Abstractions;
using Core.Katalog;

namespace Bildvorschau.Katalog.Aufbereitung;

/// <summary>
/// Die Bildaufbereitung (docs/04 §4.4) als KETTE im Katalog: eine erkannte Datei wird gelesen, verkleinert, im Kontrast ausgeglichen (die Vorschau)
/// und nach der Aufnahme-Konvention gedeutet; jede Nachricht trägt die Datei der Quelle mit. Der Kopf nimmt <see cref="DateiGefunden"/> (die Meldung
/// des Dateiwächters); jede Kante steht am nehmenden Knoten (Einlesen → Verkleinern → Kontrast → Deuten). Hinaus gehen <see cref="BildGedeutet"/> und
/// die Hinweise <see cref="BildNichtLesbar"/>, <see cref="DateinameUnbekannt"/> — was daraus in einer Domäne wird, entscheiden ihre Übersetzungen
/// (nach dem Typ, auch für jeden Zwischenstand). Läuft er als eigene Instanz, nimmt sein Kopf die Datei von außen (Tests, Clients).
/// </summary>
public sealed record Bildaufbereitung(
    [Kopf] Bildeinlesen Einlesen,
    [Von(nameof(Einlesen))] Bildverkleinerung Verkleinern,
    [Von(nameof(Verkleinern))] Kontrastausgleich Kontrast,
    [Von(nameof(Kontrast))] Dateinamendeutung Deuten) : IGraph;

/// <summary>
/// Der Linienimport (docs/04 §4.7): ein Dateiwächter am Kopf (eine Quellfunktion aus dem Katalog des Frameworks), danach die Bildaufbereitung — je
/// gefundene Datei ein Lauf. Eine Instanz je Linie (je Verzeichnis), dieselbe Blaupause beliebig oft. Domänenneutral: das gedeutete Bild verlässt den
/// Graphen, eine Übersetzung der Domäne macht es zu einem Command (die Bildaufnahme: <c>Domain.ImagePair.Aufnehmen</c>); der Akteur der Instanz
/// darf, was daraus wird.
/// </summary>
public sealed record Linienimport(
    Dateiwaechter Eingang,
    [Von(nameof(Eingang))] Bildaufbereitung Aufbereitung) : IGraph;
