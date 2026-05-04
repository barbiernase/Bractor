using Abstractions;

namespace Domain.ImagePair;

// ═══════════════════════════════════════════════════
// ERFOLGS-EVENTS (IEvent) — werden persistiert + publiziert
// ═══════════════════════════════════════════════════

public record ImagePairErstellt(
    Guid AggregateId,
    string PairKey,
    DateTimeOffset ProduziertAm,
    DateTimeOffset AufgenommenAm,
    string UrsprungsPfad
) : IEvent;

public record BildVerfuegbar(
    BildVersion Version,
    BildMeta Meta,
    string Pfad,
    IReadOnlyList<RegionBewertung> Regionen
) : IEvent;

public record ImagePairKomplett() : IEvent;

// --- Strang 1: KI-Klassifikation ---

public record EinzelBildDurchKiKlassifiziert(
    BildVersion Version,
    Klassifikation BildLabel,
    IReadOnlyList<Klassifikation> RegionLabels
) : IEvent;

public record BildPaarDurchKiKlassifiziert(
    Klassifikation Label
) : IEvent;

// --- Strang 2: Mensch Kamerabild-Labels ---

public record BildRegionGelabelt(
    BildVersion Version,
    int RegionIndex,
    Klassifikation Label
) : IEvent;

public record EinzelBildGelabelt(
    BildVersion Version,
    Klassifikation Label
) : IEvent;

public record BildPaarGelabelt(
    Klassifikation Label
) : IEvent;

// --- Strang 3: Mensch physisches Produkt ---

public record PhysischesProduktGelabelt(
    Klassifikation Label
) : IEvent;

// --- Inspektion ---

public record ImagePairInspiziert() : IEvent;

// ═══════════════════════════════════════════════════
// ABLEHNUNGS-EVENTS (ITransientEvent)
// ═══════════════════════════════════════════════════

public record ImagePairExistiertBereits(Guid PairId) : ITransientEvent;
public record ImagePairNichtGefunden(Guid PairId) : ITransientEvent;
public record BildVersionBereitsVerfuegbar(BildVersion Version) : ITransientEvent;
public record BildNichtVerfuegbar(BildVersion Version) : ITransientEvent;
public record RegionIndexUngueltig(int RegionIndex) : ITransientEvent;
public record RegionLabelsUngueltig(int Anzahl) : ITransientEvent;
public record PaarNichtKomplett() : ITransientEvent;
public record ImagePairEingabeUngueltig(string Feld, string Grund) : ITransientEvent;