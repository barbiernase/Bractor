using Abstractions;
using Domain.ImagePair;

namespace Domain.Projections;

public record ImagePairAntwort(
    Guid Id,
    string? PairKey,
    DateTimeOffset ProduziertAm,
    DateTimeOffset AufgenommenAm,
    bool IstKomplett,
    string? UrsprungsPfad,

    // DC0
    string? Dc0Pfad,
    Klassifikation? Dc0KiBildKlassifikation,
    Klassifikation? Dc0MenschBildLabel,
    IReadOnlyList<Klassifikation?> Dc0KiRegionen,
    IReadOnlyList<Klassifikation?> Dc0MenschRegionen,

    // DC2
    string? Dc2Pfad,
    Klassifikation? Dc2KiBildKlassifikation,
    Klassifikation? Dc2MenschBildLabel,
    IReadOnlyList<Klassifikation?> Dc2KiRegionen,
    IReadOnlyList<Klassifikation?> Dc2MenschRegionen,

    // Paar-Ebene
    Klassifikation? KiBildpaarKlassifikation,
    Klassifikation? MenschBildpaarLabel,
    Klassifikation? PhysischesProduktLabel,

    // Inspektion
    bool IstInspiziert
) : IQueryResponse;

public record ImagePairNichtGefundenAntwort(Guid PairId) : IQueryResponse;

public record ImagePairSuchergebnis(
    IReadOnlyList<ImagePairAntwort> Items,
    int GesamtAnzahl,
    int Seite,
    int SeitenGroesse
) : IQueryResponse;

public record ImagePairStatistikAntwort(
    int Gesamt,
    int Komplett,
    int MitKiKlassifikation,
    int MitMenschLabel,
    int MitProduktLabel,
    int OhneKlassifikation,
    int AnzahlAnomalienKi,
    int AnzahlAnomalienMensch
) : IQueryResponse;

public record ImagePairArbeitsliste(
    IReadOnlyList<ImagePairAntwort> Items
) : IQueryResponse;

// ═══════════════════════════════════════════════════════
// Chart: Produktionsverlauf (Zeitbucket-Balken)
// ═══════════════════════════════════════════════════════

public record ProduktionsZeitBucket(
    DateTimeOffset Zeitpunkt,
    int Gesamt,
    int Ok,
    int Questionable,
    int Anomalie,
    int Ungelabelt,
    int NichtInspiziert
);

public record ProduktionsVerlaufAntwort(
    IReadOnlyList<ProduktionsZeitBucket> Buckets,
    int GesamtImZeitraum,
    int AnomalienImZeitraum,
    int UngelabeltImZeitraum
) : IQueryResponse;

// ═══════════════════════════════════════════════════════
// Chart: Produktionstage (Tagesliste)
// ═══════════════════════════════════════════════════════

public record ProduktionsTag(
    DateTimeOffset Datum,
    int AnzahlGesamt,
    int AnzahlAnomalie,
    int AnzahlUngelabelt
);

public record ProduktionsTageAntwort(
    IReadOnlyList<ProduktionsTag> Tage
) : IQueryResponse;

// ═══════════════════════════════════════════════════════
// Chart: Produktions-Strip (ein Punkt pro Teil)
// ═══════════════════════════════════════════════════════

/// <summary>
/// Ein einzelnes Teil im Strip — nur Zeitpunkt + Label.
/// Leichtgewichtig für die Barcode-Visualisierung.
/// </summary>
public record ProduktionsStripPunkt(
    DateTimeOffset Zeitpunkt,
    Klassifikation? ProduktLabel
);

/// <summary>
/// Alle Teile eines Tages als Strip-Punkte.
/// Chronologisch sortiert. Lücken = Produktionsausfälle.
/// </summary>
public record ProduktionsStripAntwort(
    IReadOnlyList<ProduktionsStripPunkt> Punkte
) : IQueryResponse;