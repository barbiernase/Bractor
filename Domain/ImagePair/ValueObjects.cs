namespace Domain.ImagePair;

// ═══════════════════════════════════════════════════
// POSITIONS- UND METADATEN
// ═══════════════════════════════════════════════════

/// <summary>
/// Position einer Region im Bild — prozentual (0.0–100.0).
/// 8 Spalten nebeneinander: jede Region = 1/8 der Bildbreite, volle Höhe.
/// </summary>
public record RegionPosition(
    double XProzent,
    double YProzent,
    double BreiteProzent,
    double HoeheProzent
);

/// <summary>Metadaten eines einzelnen Bildes.</summary>
public record BildMeta(
    string OriginalDateiname,
    long DateigroesseBytes,
    int BreitePixel,
    int HoehePixel,
    DateTimeOffset ErstelltAm
);

// ═══════════════════════════════════════════════════
// BEWERTUNGEN — zwei parallele Slots pro Ebene
//
// KI klassifiziert, Mensch labelt.
// Jedes Nullable-Feld wird eigenständig gefüllt.
// ═══════════════════════════════════════════════════

/// <summary>Bewertung auf Einzelbild-Ebene.</summary>
public record BildBewertung(
    Klassifikation? KiKlassifikation,
    Klassifikation? MenschLabel
);

/// <summary>Bewertung einer einzelnen Region (1 von 8).</summary>
public record RegionBewertung(
    int RegionIndex,          // 0–7
    RegionPosition Position,
    Klassifikation? KiKlassifikation,
    Klassifikation? MenschLabel
);

// ═══════════════════════════════════════════════════
// BILD-INFO (zusammengesetzt)
// ═══════════════════════════════════════════════════

/// <summary>
/// Ein vollständiges Bild mit Meta, Bewertung und 8 Regionen.
/// Existiert einmal für DC0 und einmal für DC2.
/// Wird erst beim Aggregat angelegt wenn das Bild verfügbar ist —
/// es gibt keinen Zwischenzustand.
/// </summary>
public record BildInfo(
    BildVersion Version,
    BildMeta Meta,
    string Pfad,
    BildBewertung Bewertung,
    IReadOnlyList<RegionBewertung> Regionen   // genau 8
);