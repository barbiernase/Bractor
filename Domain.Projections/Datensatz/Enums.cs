namespace Domain.Projections.Datensatz;

/// <summary>Welche Bildpaar-Sicht eines Datensatzes die Galerie zeigt.</summary>
public enum DatensatzPaarModus
{
    // Bewusst 1-basiert (nicht 0): der DtoMapper verwirft den Enum-Default-Wert 0 auf der Wire.
    Mitglieder = 1,
    Ausgeschlossen = 2,
}
