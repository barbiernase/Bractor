using Abstractions;

namespace Domain.ImagePair;

// ═══════════════════════════════════════════════════
// LIFECYCLE
// ═══════════════════════════════════════════════════

public record ErstelleImagePair(
    Id<ImagePair> AggregateId,
    string PairKey,
    DateTimeOffset ProduziertAm,
    DateTimeOffset AufgenommenAm,
    string UrsprungsPfad
) : ICommand<ImagePair>, ICreationCommand;

public record MeldeBildVerfuegbar(
    Id<ImagePair> AggregateId,
    BildVersion Version,
    BildMeta Meta,
    string Pfad
) : ICommand<ImagePair>;

// ═══════════════════════════════════════════════════
// STRANG 1 — KI klassifiziert Kamerabilder
// ═══════════════════════════════════════════════════

public record KlassifiziereEinzelBildDurchKi(
    Id<ImagePair> AggregateId,
    BildVersion Version,
    Klassifikation BildLabel,
    IReadOnlyList<Klassifikation> RegionLabels
) : ICommand<ImagePair>;

public record KlassifiziereBildPaarDurchKi(
    Id<ImagePair> AggregateId,
    Klassifikation Label
) : ICommand<ImagePair>;

// ═══════════════════════════════════════════════════
// STRANG 2 — Mensch labelt Kamerabilder
// ═══════════════════════════════════════════════════

public record LabelBildRegion(
    Id<ImagePair> AggregateId,
    BildVersion Version,
    int RegionIndex,
    Klassifikation Label
) : ICommand<ImagePair>;

public record LabelEinzelBild(
    Id<ImagePair> AggregateId,
    BildVersion Version,
    Klassifikation Label
) : ICommand<ImagePair>;

public record LabelBildPaar(
    Id<ImagePair> AggregateId,
    Klassifikation Label
) : ICommand<ImagePair>;

// ═══════════════════════════════════════════════════
// STRANG 3 — Mensch labelt physisches Produkt
// ═══════════════════════════════════════════════════

public record LabelPhysischesProdukt(
    Id<ImagePair> AggregateId,
    Klassifikation Label
) : ICommand<ImagePair>;

// ═══════════════════════════════════════════════════
// INSPEKTION — Mensch hat Bildpaar betrachtet
// ═══════════════════════════════════════════════════

public record MarkiereAlsInspiziert(
    Id<ImagePair> AggregateId
) : ICommand<ImagePair>;