using Abstractions;

namespace Domain.Pipeline.Bildaufbereitung;

/// <summary>
/// Eine erkannte Datei ist aufbereitet: die Datei selbst (Name, Pfad, Größe, wann erkannt) und ihre Vorschau (verkleinert, im Kontrast
/// ausgeglichen). Ein Fakt im Log — festgehalten von der Funktion, gehört von <c>Bildeingang</c>.
/// </summary>
public record BildAufbereitet(string Dateiname, string Pfad, long Groesse, DateTimeOffset ErkanntAm,
    string Vorschau, int BreitePixel, int HoehePixel, DateTimeOffset ErstelltAm) : IEvent;

/// <summary>Die Datei ließ sich nicht lesen/schreiben — die Bildaufbereitung endet ohne Bild.</summary>
public record BildNichtLesbar(string Pfad, string Grund) : IEvent;
