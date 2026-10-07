using Abstractions;

namespace Domain.ImagePair;

// ═══════════════════════════════════════════════════
// LIFECYCLE
// ═══════════════════════════════════════════════════

public record ErstelleImagePair(
    Guid AggregateId,
    string PairKey,
    DateTimeOffset ProduziertAm,
    DateTimeOffset AufgenommenAm,
    string UrsprungsPfad
) : ICreationCommand;

public record MeldeBildVerfuegbar(
    Guid AggregateId,
    BildVersion Version,
    BildMeta Meta,
    string Pfad
) : ICommand;

// ═══════════════════════════════════════════════════
// STRANG 1 — KI klassifiziert Kamerabilder
// ═══════════════════════════════════════════════════

public record KlassifiziereEinzelBildDurchKi(
    Guid AggregateId,
    BildVersion Version,
    Klassifikation BildLabel,
    IReadOnlyList<Klassifikation> RegionLabels
) : ICommand;

public record KlassifiziereBildPaarDurchKi(
    Guid AggregateId,
    Klassifikation Label
) : ICommand;

// ═══════════════════════════════════════════════════
// STRANG 2 — Mensch labelt Kamerabilder
// ═══════════════════════════════════════════════════

public record LabelBildRegion(
    Guid AggregateId,
    BildVersion Version,
    int RegionIndex,
    Klassifikation Label
) : ICommand;

public record LabelEinzelBild(
    Guid AggregateId,
    BildVersion Version,
    Klassifikation Label
) : ICommand;

public record LabelBildPaar(
    Guid AggregateId,
    Klassifikation Label
) : ICommand;

// ═══════════════════════════════════════════════════
// STRANG 3 — Mensch labelt physisches Produkt
// ═══════════════════════════════════════════════════

public record LabelPhysischesProdukt(
    Guid AggregateId,
    Klassifikation Label
) : ICommand;

// ═══════════════════════════════════════════════════
// INSPEKTION — Mensch hat Bildpaar betrachtet
// ═══════════════════════════════════════════════════

public record MarkiereAlsInspiziert(
    Guid AggregateId
) : ICommand;