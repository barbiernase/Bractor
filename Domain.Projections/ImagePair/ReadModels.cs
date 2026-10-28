using Abstractions;
using Domain.ImagePair;
using ImagePairAgg = Domain.ImagePair.ImagePair;

namespace Domain.Projections.ImagePair;

public partial record ImagePairReadModel : IReadModel
{
    public Id<ImagePairAgg> Id { get; init; }
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
/// </summary>
public partial record ImagePairHistorieReadModel : IReadModel
{
    public Id<ImagePairAgg> Id { get; init; }
    public List<HistorieEintrag> Eintraege { get; init; } = new();
}

/// <summary>
/// Statistik-Daten — internes Transfer-Objekt zwischen Store und Reader.
/// </summary>
public record ImagePairStatistik(
    int Gesamt,
    int Komplett,
    int MitKiKlassifikation,
    int MitMenschLabel,
    int MitProduktLabel,
    int OhneKlassifikation,
    int AnzahlAnomalienKi,
    int AnzahlAnomalienMensch
);
