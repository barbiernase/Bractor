using Domain.ImagePair;

namespace Domain.Datensatz;

// ═══════════════════════════════════════════════════
// RANGE-KRITERIEN — Provenienz einer Such-Range
//
// Spiegelt bewusst die Felder von Domain.Projections.ImagePairFilter, ohne den
// Projektpfad Domain → Domain.Projections zu drehen (das wäre ein Zyklus). Der
// server-seitige Resolver (Meilenstein 5) übersetzt RangeKriterien 1:1 in einen
// ImagePairFilter und ruft SearchAsync auf. So bleibt das Aggregat rein (kein I/O)
// und die Kriterien landen als Provenienz im Log.
// ═══════════════════════════════════════════════════

/// <summary>
/// Suchkriterien einer Range — alle Felder nullable, nur gesetzte Felder filtern.
/// Von/Bis beziehen sich auf den Produktionszeitpunkt (ProduziertAm).
/// </summary>
public record RangeKriterien(
    DateTimeOffset? Von = null,
    DateTimeOffset? Bis = null,
    Klassifikation? KiKlassifikation = null,
    Klassifikation? MenschLabel = null,
    Klassifikation? ProduktLabel = null,
    bool? NurKomplette = null,
    bool? HatKiKlassifikation = null,
    bool? HatMenschLabel = null,
    bool? NurNichtInspizierte = null
);

/// <summary>
/// Herkunft einer aufgenommenen Range — welche Suche, wie viele Treffer.
/// „Diese Range kam aus <em>dieser</em> Suche." Ermöglicht die elegante Vereinigung
/// mehrerer Ranges in einem Datensatz und den Audit-Trail (Konzept §10).
/// </summary>
public record RangeHerkunft(
    RangeKriterien Kriterien,
    int AnzahlPaare
);

/// <summary>
/// Aufteilung train/val/test in Prozent + fester Seed.
/// Default 70/15/15 (Konzept §3.3): im Normalfall null Knöpfe.
/// </summary>
public record SplitKonfig(
    int TrainProzent,
    int ValProzent,
    int TestProzent,
    int Seed
)
{
    /// <summary>Der Default — 70/15/15, Seed 42. Zwei Trainings auf v1 werden so vergleichbar.</summary>
    public static SplitKonfig Default { get; } = new(70, 15, 15, 42);

    /// <summary>Gültig, wenn die Anteile nicht-negativ sind und sich zu 100 summieren.</summary>
    public bool IstGueltig =>
        TrainProzent >= 0 && ValProzent >= 0 && TestProzent >= 0 &&
        TrainProzent + ValProzent + TestProzent == 100;
}

/// <summary>
/// Ein eingefrorenes Mitglied — der Label-Stand + Split zum Einfrier-Zeitpunkt,
/// festgeschrieben und immutabel. Trägt die Bildpfade mit, damit Python die Pixel
/// über den bestehenden Datei-Endpunkt holen kann (Events tragen Pfade, keine Blobs).
/// </summary>
public record DatensatzMitglied(
    Guid ImagePairId,
    Klassifikation Label,
    Split Split,
    string Dc0Pfad,
    string Dc2Pfad
);
