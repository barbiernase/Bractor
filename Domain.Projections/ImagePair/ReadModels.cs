using Abstractions;
using Domain.ImagePair;

namespace Domain.Projections.ImagePair;

public record ImagePairReadModel : IReadModel
{
    public Guid Id { get; init; }
    public DateTimeOffset ProduziertAm { get; init; }
    public DateTimeOffset AufgenommenAm { get; init; }
    public DateTimeOffset LetzteAktualisierung { get; init; }

    // ── Lifecycle ──
    public bool IstKomplett { get; init; }
    public string? PairKey { get; init; }
    public string? UrsprungsPfad { get; init; }

    // ── DC0 ──
    public string? Dc0Pfad { get; init; }
    public string? Dc0OriginalDateiname { get; init; }
    public int? Dc0BreitePixel { get; init; }
    public int? Dc0HoehePixel { get; init; }
    public Klassifikation? Dc0KiBildKlassifikation { get; init; }
    public Klassifikation? Dc0MenschBildLabel { get; init; }
    public IReadOnlyList<Klassifikation?> Dc0KiRegionen { get; init; } = new Klassifikation?[8];
    public IReadOnlyList<Klassifikation?> Dc0MenschRegionen { get; init; } = new Klassifikation?[8];

    // ── DC2 ──
    public string? Dc2Pfad { get; init; }
    public string? Dc2OriginalDateiname { get; init; }
    public int? Dc2BreitePixel { get; init; }
    public int? Dc2HoehePixel { get; init; }
    public Klassifikation? Dc2KiBildKlassifikation { get; init; }
    public Klassifikation? Dc2MenschBildLabel { get; init; }
    public IReadOnlyList<Klassifikation?> Dc2KiRegionen { get; init; } = new Klassifikation?[8];
    public IReadOnlyList<Klassifikation?> Dc2MenschRegionen { get; init; } = new Klassifikation?[8];

    // ── Paar-Ebene ──
    public Klassifikation? KiBildpaarKlassifikation { get; init; }
    public Klassifikation? MenschBildpaarLabel { get; init; }
    public Klassifikation? PhysischesProduktLabel { get; init; }

    // ── Berechnete Flags ──
    public bool HatKiKlassifikation { get; init; }
    public bool HatMenschLabel { get; init; }
    public bool HatProduktLabel { get; init; }

    // ── Inspektion ──
    public bool IstInspiziert { get; init; }
}

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
