// ── Domain.Client/ImagePair/Stores/HistorieStore.cs ──

using Client.Infrastructure.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using Domain.ImagePair;
using Domain.Projections;

namespace Domain.Client.ImagePair;

/// <summary>
/// Hält die Historie pro ImagePair als Dictionary.
///
/// Kein TrackedMap — Historie ist immer nur für ein Paar sichtbar.
/// PropertyChanged auf HistorieVersion signalisiert der View Änderungen.
///
/// Publisht nichts. Liest keine anderen Stores.
/// Subscribed als 4. Kind im Workspace.
/// </summary>
public partial class HistorieStore : StoreBase
{
    private readonly Dictionary<Guid, List<HistorieEintrag>> _historie = new();
    private readonly HashSet<Guid> _geladen = new();

    /// <summary>
    /// Wird bei jeder Änderung inkrementiert.
    /// Die View subscribt auf PropertyChanged("HistorieVersion") → StateHasChanged.
    /// </summary>
    [ObservableProperty] private int _historieVersion;

    // ═══════════════════════════════════════════════════
    // LESEZUGRIFF
    // ═══════════════════════════════════════════════════

    public IReadOnlyList<HistorieEintrag> GetHistorie(Guid pairId)
        => _historie.TryGetValue(pairId, out var list) ? list : [];

    public bool IstGeladen(Guid pairId) => _geladen.Contains(pairId);

    // ═══════════════════════════════════════════════════
    // HANDLE — Query-Responses
    // ═══════════════════════════════════════════════════

    void Handle(ImagePairHistorieAntwort antwort, MessageContext ctx)
    {
        _historie[antwort.PairId] = antwort.Eintraege.ToList();
        _geladen.Add(antwort.PairId);
        HistorieVersion++;
    }

    void Handle(ImagePairHistorieNichtGefunden antwort, MessageContext ctx)
    {
        _historie[antwort.PairId] = new List<HistorieEintrag>();
        _geladen.Add(antwort.PairId);
        HistorieVersion++;
    }

    // ═══════════════════════════════════════════════════
    // HANDLE — Server-Events → Live-Historie
    // ═══════════════════════════════════════════════════

    void Handle(ImagePairErstellt evt, MessageContext ctx)
        => AppendLive(ctx, nameof(ImagePairErstellt),
            "ImagePair erstellt", HistorieKategorie.Lifecycle,
            $"PairKey: {evt.PairKey}");

    void Handle(BildVerfuegbar evt, MessageContext ctx)
        => AppendLive(ctx, nameof(BildVerfuegbar),
            $"{evt.Version} verfügbar", HistorieKategorie.Lifecycle,
            $"{evt.Meta.BreitePixel}×{evt.Meta.HoehePixel}");

    void Handle(ImagePairKomplett evt, MessageContext ctx)
        => AppendLive(ctx, nameof(ImagePairKomplett),
            "Beide Bilder verfügbar", HistorieKategorie.Lifecycle);

    void Handle(EinzelBildDurchKiKlassifiziert evt, MessageContext ctx)
        => AppendLive(ctx, nameof(EinzelBildDurchKiKlassifiziert),
            $"KI: {evt.Version} klassifiziert", HistorieKategorie.Ki,
            FormatKlassifikation(evt.BildLabel));

    void Handle(BildPaarDurchKiKlassifiziert evt, MessageContext ctx)
        => AppendLive(ctx, nameof(BildPaarDurchKiKlassifiziert),
            "KI: Bildpaar klassifiziert", HistorieKategorie.Ki,
            FormatKlassifikation(evt.Label));

    void Handle(BildRegionGelabelt evt, MessageContext ctx)
        => AppendLive(ctx, nameof(BildRegionGelabelt),
            $"Region {evt.RegionIndex} gelabelt ({evt.Version})", HistorieKategorie.Mensch,
            FormatKlassifikation(evt.Label));

    void Handle(EinzelBildGelabelt evt, MessageContext ctx)
        => AppendLive(ctx, nameof(EinzelBildGelabelt),
            $"Einzelbild {evt.Version} gelabelt", HistorieKategorie.Mensch,
            FormatKlassifikation(evt.Label));

    void Handle(BildPaarGelabelt evt, MessageContext ctx)
        => AppendLive(ctx, nameof(BildPaarGelabelt),
            "Bildpaar gelabelt", HistorieKategorie.Mensch,
            FormatKlassifikation(evt.Label));

    void Handle(PhysischesProduktGelabelt evt, MessageContext ctx)
        => AppendLive(ctx, nameof(PhysischesProduktGelabelt),
            "Produkt gelabelt", HistorieKategorie.Mensch,
            FormatKlassifikation(evt.Label));

    void Handle(ImagePairInspiziert evt, MessageContext ctx)
        => AppendLive(ctx, nameof(ImagePairInspiziert),
            "Inspiziert", HistorieKategorie.Inspektion);

    // ═══════════════════════════════════════════════════
    // INTERN
    // ═══════════════════════════════════════════════════

    private void AppendLive(
        MessageContext ctx, string ereignisTyp, string beschreibung,
        HistorieKategorie kategorie, string? details = null)
    {
        if (!_historie.TryGetValue(ctx.AggregateId, out var list)) return;

        list.Add(new HistorieEintrag(
            Zeitpunkt: ctx.CreatedAtUtc,
            EreignisTyp: ereignisTyp,
            Beschreibung: beschreibung,
            Kategorie: kategorie,
            Details: details));

        HistorieVersion++;
    }

    internal static string FormatKlassifikation(Klassifikation label) => label switch
    {
        Klassifikation.KeineAnomalie => "OK",
        Klassifikation.Questionable => "Fraglich",
        Klassifikation.Anomalie => "Anomalie",
        _ => label.ToString()
    };
}
