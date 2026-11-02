using Abstractions;

namespace Domain.Bildaufbereitung;

/// <summary>Hinweis: die Datei ließ sich nicht als Bild lesen — die Bildaufbereitung endet ohne Bild (verlierbar, nicht im Log).</summary>
public record BildNichtLesbar(string Pfad, string Grund) : ITransientEvent;
