using Abstractions;
using Core;
using Domain.ImagePair;

namespace Domain.Projections;

/// <summary>
/// Materialisiert die Historie eines ImagePairs als eigenständige Projektion.
///
/// Empfängt alle Events via PubSub (KEIN EventStore-Zugriff!)
/// und appendet pro Event einen HistorieEintrag an das ReadModel.
///
/// Jeder Handle-Aufruf erzeugt genau einen Eintrag mit:
///   - Zeitpunkt aus dem Envelope
///   - Menschenlesbare Beschreibung
///   - Kategorie für UI-Farbkodierung
///   - Optionale Details (Klassifikation, Version, etc.)
/// </summary>
public partial class ImagePairHistorieProjection : ISubscriber
{
    private readonly IImagePairHistorieWriteStore _store;

    public ImagePairHistorieProjection(IImagePairHistorieWriteStore store)
    {
        _store = store;
    }

    public string SubscriberId => "imagepair-historie";

    // ═══════════════════════════════════════════════════════════
    // LIFECYCLE
    // ═══════════════════════════════════════════════════════════

    public async Task Handle(
        ImagePairErstellt evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await _store.AppendEintragAsync(envelope.AggregateId, new HistorieEintrag(
                Zeitpunkt: envelope.CreatedAtUtc,
                EreignisTyp: nameof(ImagePairErstellt),
                Beschreibung: "ImagePair erstellt",
                Kategorie: HistorieKategorie.Lifecycle,
                Details: $"PairKey: {evt.PairKey}"));
        });
    }

    public async Task Handle(
        BildVerfuegbar evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await _store.AppendEintragAsync(envelope.AggregateId, new HistorieEintrag(
                Zeitpunkt: envelope.CreatedAtUtc,
                EreignisTyp: nameof(BildVerfuegbar),
                Beschreibung: $"{evt.Version} verfügbar",
                Kategorie: HistorieKategorie.Lifecycle,
                Details: $"{evt.Meta.BreitePixel}×{evt.Meta.HoehePixel}, {evt.Meta.DateigroesseBytes / 1024} KB"));
        });
    }

    public async Task Handle(
        ImagePairKomplett evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await _store.AppendEintragAsync(envelope.AggregateId, new HistorieEintrag(
                Zeitpunkt: envelope.CreatedAtUtc,
                EreignisTyp: nameof(ImagePairKomplett),
                Beschreibung: "Beide Bilder verfügbar",
                Kategorie: HistorieKategorie.Lifecycle));
        });
    }

    // ═══════════════════════════════════════════════════════════
    // STRANG 1 — KI-Klassifikation
    // ═══════════════════════════════════════════════════════════

    public async Task Handle(
        EinzelBildDurchKiKlassifiziert evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await _store.AppendEintragAsync(envelope.AggregateId, new HistorieEintrag(
                Zeitpunkt: envelope.CreatedAtUtc,
                EreignisTyp: nameof(EinzelBildDurchKiKlassifiziert),
                Beschreibung: $"KI: {evt.Version} klassifiziert",
                Kategorie: HistorieKategorie.Ki,
                Details: $"Bild: {FormatKlassifikation(evt.BildLabel)}"));
        });
    }

    public async Task Handle(
        BildPaarDurchKiKlassifiziert evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await _store.AppendEintragAsync(envelope.AggregateId, new HistorieEintrag(
                Zeitpunkt: envelope.CreatedAtUtc,
                EreignisTyp: nameof(BildPaarDurchKiKlassifiziert),
                Beschreibung: "KI: Bildpaar klassifiziert",
                Kategorie: HistorieKategorie.Ki,
                Details: FormatKlassifikation(evt.Label)));
        });
    }

    // ═══════════════════════════════════════════════════════════
    // STRANG 2 — Mensch labelt Kamerabilder
    // ═══════════════════════════════════════════════════════════

    public async Task Handle(
        BildRegionGelabelt evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await _store.AppendEintragAsync(envelope.AggregateId, new HistorieEintrag(
                Zeitpunkt: envelope.CreatedAtUtc,
                EreignisTyp: nameof(BildRegionGelabelt),
                Beschreibung: $"Region {evt.RegionIndex} gelabelt ({evt.Version})",
                Kategorie: HistorieKategorie.Mensch,
                Details: FormatKlassifikation(evt.Label)));
        });
    }

    public async Task Handle(
        EinzelBildGelabelt evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await _store.AppendEintragAsync(envelope.AggregateId, new HistorieEintrag(
                Zeitpunkt: envelope.CreatedAtUtc,
                EreignisTyp: nameof(EinzelBildGelabelt),
                Beschreibung: $"Einzelbild {evt.Version} gelabelt",
                Kategorie: HistorieKategorie.Mensch,
                Details: FormatKlassifikation(evt.Label)));
        });
    }

    public async Task Handle(
        BildPaarGelabelt evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await _store.AppendEintragAsync(envelope.AggregateId, new HistorieEintrag(
                Zeitpunkt: envelope.CreatedAtUtc,
                EreignisTyp: nameof(BildPaarGelabelt),
                Beschreibung: "Bildpaar gelabelt",
                Kategorie: HistorieKategorie.Mensch,
                Details: FormatKlassifikation(evt.Label)));
        });
    }

    // ═══════════════════════════════════════════════════════════
    // STRANG 3 — Mensch labelt physisches Produkt
    // ═══════════════════════════════════════════════════════════

    public async Task Handle(
        PhysischesProduktGelabelt evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await _store.AppendEintragAsync(envelope.AggregateId, new HistorieEintrag(
                Zeitpunkt: envelope.CreatedAtUtc,
                EreignisTyp: nameof(PhysischesProduktGelabelt),
                Beschreibung: "Produkt gelabelt",
                Kategorie: HistorieKategorie.Mensch,
                Details: FormatKlassifikation(evt.Label)));
        });
    }

    // ═══════════════════════════════════════════════════════════
    // INSPEKTION
    // ═══════════════════════════════════════════════════════════

    public async Task Handle(
        ImagePairInspiziert evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await _store.AppendEintragAsync(envelope.AggregateId, new HistorieEintrag(
                Zeitpunkt: envelope.CreatedAtUtc,
                EreignisTyp: nameof(ImagePairInspiziert),
                Beschreibung: "Inspiziert",
                Kategorie: HistorieKategorie.Inspektion));
        });
    }

    // ─── Hilfsmethode ───

    private static string FormatKlassifikation(Klassifikation label) => label switch
    {
        Klassifikation.KeineAnomalie => "OK",
        Klassifikation.Questionable  => "Fraglich",
        Klassifikation.Anomalie      => "Anomalie",
        _                            => label.ToString()
    };
}