using Abstractions;
using Bildvorschau.Katalog.Aufbereitung;
using Core.Katalog;

namespace Domain.Pipeline.ImagePair;

/// <summary>
/// Die Bildaufnahme des Paars: die Bildaufbereitung aus dem Katalog (Datei → Vorschau), danach deutet der <see cref="Bildeingang"/> den Dateinamen
/// und gibt dem Paar das Bild. Der Kopf nimmt <see cref="DateiGefunden"/> von außen — so läuft die Aufnahme auch ohne Dateiwächter (Tests, Clients).
/// </summary>
public sealed record Bildaufnahme(
    [Kopf] Bildaufbereitung Aufbereitung,
    [Von(nameof(Aufbereitung))] Bildeingang Deuten) : IGraph;

/// <summary>
/// Der Linienimport (docs/04 §4.7): ein Dateiwächter am Kopf (eine Quellfunktion aus dem Katalog des Frameworks), die Bildaufbereitung aus dem
/// Katalog und der Bildeingang des Paars — je gefundene Datei ein Lauf. Eine Instanz je Linie (je Verzeichnis), dieselbe Blaupause beliebig oft; der
/// Akteur der Instanz gibt hinein, was den Graphen verlässt (das Kamerasystem: <c>NimmBildAuf</c>).
/// </summary>
public sealed record Linienimport(
    Dateiwaechter Eingang,
    [Von(nameof(Eingang))] Bildaufbereitung Aufbereitung,
    [Von(nameof(Aufbereitung))] Bildeingang Deuten) : IGraph;
