using Abstractions;
using Bildvorschau.Katalog.Aufbereitung;
using Core.Katalog;
using Domain.ImagePair;

namespace Domain.Pipeline.ImagePair;

/// <summary>
/// Die Bildaufnahme des Paars: die Bildaufbereitung aus dem Katalog (Datei → Vorschau → gedeuteter Name), danach übersetzt der Ablauf das gedeutete Bild
/// in den Command an das Paar (die Id aus der Guid des Schlüssels, alles andere Feld für Feld). Der Kopf nimmt <see cref="DateiGefunden"/> von außen
/// — so läuft die Aufnahme auch ohne Dateiwächter (Tests, Clients).
/// </summary>
public sealed record Bildaufnahme(
    [Kopf] Bildaufbereitung Aufbereitung,
    [Von(nameof(Aufbereitung)), Feld(nameof(NimmBildAuf.AggregateId), nameof(BildGedeutet.Paar))] Uebersetze<BildGedeutet, NimmBildAuf> Aufnehmen) : IGraph;

/// <summary>
/// Der Linienimport (docs/04 §4.7): ein Dateiwächter am Kopf (eine Quellfunktion aus dem Katalog des Frameworks), die Bildaufbereitung aus dem
/// Katalog, das gedeutete Bild übersetzt in den Command an das Paar — je gefundene Datei ein Lauf. Eine Instanz je Linie (je Verzeichnis), dieselbe
/// Blaupause beliebig oft; der Akteur der Instanz gibt hinein, was den Graphen verlässt (das Kamerasystem: <c>NimmBildAuf</c>).
/// </summary>
public sealed record Linienimport(
    Dateiwaechter Eingang,
    [Von(nameof(Eingang))] Bildaufbereitung Aufbereitung,
    [Von(nameof(Aufbereitung)), Feld(nameof(NimmBildAuf.AggregateId), nameof(BildGedeutet.Paar))] Uebersetze<BildGedeutet, NimmBildAuf> Aufnehmen) : IGraph;
