namespace Domain.Projections.ImagePair;

/// <summary>
/// Kategorien für farbliche Gruppierung in der UI.
///
///   Lifecycle → grau   (Erstellt, BildVerfuegbar, Komplett)
///   Ki        → blau   (KI-Klassifikationen)
///   Mensch    → grün   (Mensch-Labels)
///   Inspektion → gelb  (Inspiziert)
/// </summary>
public enum HistorieKategorie
{
    Lifecycle,
    Ki,
    Mensch,
    Inspektion
}
