using Abstractions;

namespace Domain.Projections;

// ═══════════════════════════════════════════════════════════════
// HISTORIE-EINTRAG — ein einzelner Schritt in der Timeline
// ═══════════════════════════════════════════════════════════════

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

/// <summary>
/// Ein einzelner Eintrag in der ImagePair-Timeline.
///
/// Wird von der Projektion aus jedem Event erzeugt.
/// Unveränderlich, append-only.
/// </summary>
public record HistorieEintrag(
    /// <summary>Zeitpunkt des Events (aus Envelope.CreatedAtUtc).</summary>
    DateTimeOffset Zeitpunkt,

    /// <summary>CLR-Typname des Events (für programmatische Nutzung).</summary>
    string EreignisTyp,

    /// <summary>Menschenlesbare Beschreibung.</summary>
    string Beschreibung,

    /// <summary>Kategorie für UI-Gruppierung/Farbe.</summary>
    HistorieKategorie Kategorie,

    /// <summary>Optionale Details (Klassifikation, Bildversion, etc.).</summary>
    string? Details = null
);

// ═══════════════════════════════════════════════════════════════
// READMODEL — ein Dokument pro ImagePair
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// Materialisierte Timeline eines ImagePairs.
///
/// Wird als JSONB-Dokument in Marten gespeichert.
/// Die Einträge-Liste wächst nur — kein Update, kein Delete.
///
/// Eigenständige Projektion, KEIN Zugriff auf den EventStore.
/// Die Wahrheit kommt ausschließlich aus PubSub-Events.
/// </summary>
public record ImagePairHistorieReadModel : IReadModel
{
    public Guid Id { get; init; }
    public List<HistorieEintrag> Eintraege { get; init; } = new();
}