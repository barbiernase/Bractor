using Abstractions;
using Domain.ImagePair;

namespace Domain.Projections.ImagePair;

/// <summary>
/// Materialisiert die Historie eines ImagePairs als eigenständige Projektion.
///
/// Jedes Auf hängt genau einen Eintrag an (Zeitpunkt aus der Herkunft, menschenlesbare Beschreibung, Kategorie für die Farbe, optionale
/// Details). Append-artig — eine doppelte Wirkung wäre sichtbar; genau einmal, weil der Historien-Store ein Store der Klasse 1 ist.
/// </summary>
public sealed class ImagePairHistorieProjektion : IProjektion
{
    // ═══════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════

    public Task Auf(ImagePairErstellt e, Herkunft h, IAppendHistorieEintrag appendHistorieEintrag)
        => appendHistorieEintrag.AppendEintragAsync(h.Id<Domain.ImagePair.ImagePair>(), new HistorieEintrag(
            Zeitpunkt: h.Zeit,
            EreignisTyp: nameof(ImagePairErstellt),
            Beschreibung: "ImagePair erstellt",
            Kategorie: HistorieKategorie.Lifecycle,
            Details: $"PairKey: {e.PairKey}"));

    public Task Auf(BildVerfuegbar e, Herkunft h, IAppendHistorieEintrag appendHistorieEintrag)
        => appendHistorieEintrag.AppendEintragAsync(h.Id<Domain.ImagePair.ImagePair>(), new HistorieEintrag(
            Zeitpunkt: h.Zeit,
            EreignisTyp: nameof(BildVerfuegbar),
            Beschreibung: $"{e.Version} verfügbar",
            Kategorie: HistorieKategorie.Lifecycle,
            Details: $"{e.Meta.BreitePixel}×{e.Meta.HoehePixel}, {e.Meta.DateigroesseBytes / 1024} KB"));

    public Task Auf(ImagePairKomplett e, Herkunft h, IAppendHistorieEintrag appendHistorieEintrag)
        => appendHistorieEintrag.AppendEintragAsync(h.Id<Domain.ImagePair.ImagePair>(), new HistorieEintrag(
            Zeitpunkt: h.Zeit,
            EreignisTyp: nameof(ImagePairKomplett),
            Beschreibung: "Beide Bilder verfügbar",
            Kategorie: HistorieKategorie.Lifecycle));

    // ═══════════════════════════════════════════════════════════
    // STRANG 1 — KI-Klassifikation
    // ═══════════════════════════════════════════════════════════

    public Task Auf(EinzelBildDurchKiKlassifiziert e, Herkunft h, IAppendHistorieEintrag appendHistorieEintrag)
        => appendHistorieEintrag.AppendEintragAsync(h.Id<Domain.ImagePair.ImagePair>(), new HistorieEintrag(
            Zeitpunkt: h.Zeit,
            EreignisTyp: nameof(EinzelBildDurchKiKlassifiziert),
            Beschreibung: $"KI: {e.Version} klassifiziert",
            Kategorie: HistorieKategorie.Ki,
            Details: $"Bild: {FormatKlassifikation(e.BildLabel)}"));

    public Task Auf(BildPaarDurchKiKlassifiziert e, Herkunft h, IAppendHistorieEintrag appendHistorieEintrag)
        => appendHistorieEintrag.AppendEintragAsync(h.Id<Domain.ImagePair.ImagePair>(), new HistorieEintrag(
            Zeitpunkt: h.Zeit,
            EreignisTyp: nameof(BildPaarDurchKiKlassifiziert),
            Beschreibung: "KI: Bildpaar klassifiziert",
            Kategorie: HistorieKategorie.Ki,
            Details: FormatKlassifikation(e.Label)));

    // ═══════════════════════════════════════════════════════════
    // STRANG 2 — Mensch labelt Kamerabilder
    // ═══════════════════════════════════════════════════════════

    public Task Auf(BildRegionGelabelt e, Herkunft h, IAppendHistorieEintrag appendHistorieEintrag)
        => appendHistorieEintrag.AppendEintragAsync(h.Id<Domain.ImagePair.ImagePair>(), new HistorieEintrag(
            Zeitpunkt: h.Zeit,
            EreignisTyp: nameof(BildRegionGelabelt),
            Beschreibung: $"Region {e.RegionIndex} gelabelt ({e.Version})",
            Kategorie: HistorieKategorie.Mensch,
            Details: FormatKlassifikation(e.Label)));

    public Task Auf(EinzelBildGelabelt e, Herkunft h, IAppendHistorieEintrag appendHistorieEintrag)
        => appendHistorieEintrag.AppendEintragAsync(h.Id<Domain.ImagePair.ImagePair>(), new HistorieEintrag(
            Zeitpunkt: h.Zeit,
            EreignisTyp: nameof(EinzelBildGelabelt),
            Beschreibung: $"Einzelbild {e.Version} gelabelt",
            Kategorie: HistorieKategorie.Mensch,
            Details: FormatKlassifikation(e.Label)));

    public Task Auf(BildPaarGelabelt e, Herkunft h, IAppendHistorieEintrag appendHistorieEintrag)
        => appendHistorieEintrag.AppendEintragAsync(h.Id<Domain.ImagePair.ImagePair>(), new HistorieEintrag(
            Zeitpunkt: h.Zeit,
            EreignisTyp: nameof(BildPaarGelabelt),
            Beschreibung: "Bildpaar gelabelt",
            Kategorie: HistorieKategorie.Mensch,
            Details: FormatKlassifikation(e.Label)));

    // ═══════════════════════════════════════════════════════════
    // STRANG 3 — Mensch labelt physisches Produkt
    // ═══════════════════════════════════════════════════════════

    public Task Auf(PhysischesProduktGelabelt e, Herkunft h, IAppendHistorieEintrag appendHistorieEintrag)
        => appendHistorieEintrag.AppendEintragAsync(h.Id<Domain.ImagePair.ImagePair>(), new HistorieEintrag(
            Zeitpunkt: h.Zeit,
            EreignisTyp: nameof(PhysischesProduktGelabelt),
            Beschreibung: "Produkt gelabelt",
            Kategorie: HistorieKategorie.Mensch,
            Details: FormatKlassifikation(e.Label)));

    // ═══════════════════════════════════════════════════════════
    // INSPEKTION
    // ═══════════════════════════════════════════════════════════

    public Task Auf(ImagePairInspiziert e, Herkunft h, IAppendHistorieEintrag appendHistorieEintrag)
        => appendHistorieEintrag.AppendEintragAsync(h.Id<Domain.ImagePair.ImagePair>(), new HistorieEintrag(
            Zeitpunkt: h.Zeit,
            EreignisTyp: nameof(ImagePairInspiziert),
            Beschreibung: "Inspiziert",
            Kategorie: HistorieKategorie.Inspektion));

    // ─── Hilfsmethode ───

    private static string FormatKlassifikation(Klassifikation label) => label switch
    {
        Klassifikation.KeineAnomalie => "OK",
        Klassifikation.Questionable  => "Fraglich",
        Klassifikation.Anomalie      => "Anomalie",
        _                            => label.ToString()
    };
}
