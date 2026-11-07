using Abstractions;

namespace Bildvorschau.Katalog.Aufbereitung;

/// <summary>
/// Die Bildaufbereitung (docs/04 §4.4) als KETTE im Katalog: eine erkannte Datei wird gelesen, verkleinert und im Kontrast ausgeglichen (die
/// Vorschau); jede Nachricht trägt die Datei der Quelle mit. Der Kopf nimmt <see cref="DateiGefunden"/> (die Meldung des Dateiwächters); jede Kante
/// steht am nehmenden Knoten (Einlesen → Verkleinern → Kontrast). Hinaus gehen <see cref="VorschauErstellt"/> und der Hinweis
/// <see cref="BildNichtLesbar"/> — was daraus in einer Domäne wird, entscheidet der Ablauf, der den Graphen bindet.
/// </summary>
public sealed record Bildaufbereitung(
    [Kopf] Bildeinlesen Einlesen,
    [Von(nameof(Einlesen))] Bildverkleinerung Verkleinern,
    [Von(nameof(Verkleinern))] Kontrastausgleich Kontrast) : IGraph;
