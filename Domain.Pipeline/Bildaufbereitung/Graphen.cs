using Abstractions;
using Core.Katalog;
using Domain.Pipeline.ImagePair;

namespace Domain.Pipeline.Bildaufbereitung;

/// <summary>
/// Die Bildaufbereitung (docs/04 §4.4) als KETTE: eine erkannte Datei wird gelesen, verkleinert und im Kontrast ausgeglichen (die Vorschau); jede
/// Nachricht trägt die Datei der Quelle mit, so meldet der Eingang aus der Vorschau allein das Bild seines Paars. Der Kopf nimmt
/// <see cref="DateiGefunden"/> (aus dem Katalog); jede Kante steht am nehmenden Knoten (Einlesen → Verkleinern → Kontrast → Eingang); hinaus geht
/// <c>NimmBildAuf</c> an das Paar und die Hinweise.
/// </summary>
public sealed record Bildaufbereitung(
    [Kopf] Bildeinlesen Einlesen,
    [Von(nameof(Einlesen))] Bildverkleinerung Verkleinern,
    [Von(nameof(Verkleinern))] Kontrastausgleich Kontrast,
    [Von(nameof(Kontrast))] Bildeingang Eingang) : IGraph;

/// <summary>
/// Der Linienimport (docs/04 §4.7): ein Dateiwächter am Kopf (eine Quellfunktion aus dem Katalog) und die Bildaufbereitung als Graph im Graphen — je
/// gefundene Datei ein Lauf. Eine Instanz je Linie (je Verzeichnis), dieselbe Blaupause beliebig oft; der Akteur der Instanz gibt hinein, was den
/// Graphen verlässt (das Kamerasystem: <c>NimmBildAuf</c>).
/// </summary>
public sealed record Linienimport(Dateiwaechter Eingang, [Von(nameof(Eingang))] Bildaufbereitung Aufbereitung) : IGraph;
