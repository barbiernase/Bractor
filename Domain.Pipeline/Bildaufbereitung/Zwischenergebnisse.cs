using Abstractions;

namespace Domain.Pipeline.Bildaufbereitung;

/// <summary>Schritt 1: die erkannte Datei ist als Bild lesbar — die Datei (Name, Pfad, Größe, wann erkannt) und ihre Maße.</summary>
public record BildEingelesen(string Dateiname, string Pfad, long Groesse, DateTimeOffset ErkanntAm, int BreitePixel, int HoehePixel) : IEvent;

/// <summary>Die Datei ließ sich nicht als Bild lesen — die Bildaufbereitung endet ohne Bild.</summary>
public record BildNichtLesbar(string Pfad, string Grund) : IEvent;

/// <summary>Schritt 2: das Bild ist auf die Vorschau-Höhe verkleinert — die Datei der Quelle und das Zwischenbild mit seinen Maßen.</summary>
public record BildVerkleinert(string Dateiname, string Pfad, long Groesse, DateTimeOffset ErkanntAm,
    string Verkleinert, int BreitePixel, int HoehePixel) : IEvent;

/// <summary>
/// Schritt 3: die Vorschau ist erstellt (verkleinert, im Kontrast ausgeglichen) — die Datei der Quelle und die Vorschau. Ein Fakt im Log,
/// gehört vom <c>Bildeingang</c>.
/// </summary>
public record VorschauErstellt(string Dateiname, string Pfad, long Groesse, DateTimeOffset ErkanntAm,
    string Vorschau, int BreitePixel, int HoehePixel, DateTimeOffset ErstelltAm) : IEvent;
