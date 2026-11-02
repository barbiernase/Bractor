using Abstractions;
using Core.Katalog;
using Domain.Pipeline.ImagePair;

namespace Domain.Pipeline.Bildaufbereitung;

/// <summary>
/// Die Bildaufbereitung (docs/04 §4.4) als FORK-JOIN: eine erkannte Datei wird gelesen; das eingelesene Bild gabelt — ein Zweig verkleinert es und gleicht
/// den Kontrast aus (die Vorschau), der andere geht mit der Datei direkt zum Eingang, der beide im selben Lauf zusammenführt und das Bild seines Paars
/// meldet. Der Kopf nimmt <see cref="DateiGefunden"/> (aus dem Katalog); die Kanten folgen aus den Typen (BildEingelesen → Verkleinern und Eingang,
/// BildVerkleinert → Kontrast, VorschauErstellt → Eingang); hinaus geht <c>NimmBildAuf</c> an das Paar und die Hinweise.
/// </summary>
public sealed record Bildaufbereitung(IBildEinlesen Einlesen, IBildVerkleinerung Verkleinern, IKontrastausgleich Kontrast, Bildeingang Eingang) : IGraph;

/// <summary>
/// Der Linienimport (docs/04 §4.7): ein Dateiwächter am Kopf (eine Quellfunktion aus dem Katalog) und die Bildaufbereitung als Graph im Graphen — je
/// gefundene Datei ein Lauf. Eine Instanz je Linie (je Verzeichnis), dieselbe Blaupause beliebig oft; der Akteur der Instanz gibt hinein, was den
/// Graphen verlässt (das Kamerasystem: <c>NimmBildAuf</c>).
/// </summary>
public sealed record Linienimport(Dateiwaechter Eingang, Bildaufbereitung Aufbereitung) : IGraph;
