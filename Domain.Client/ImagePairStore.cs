using System.Collections.ObjectModel;
using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Connection;
using CommunityToolkit.Mvvm.ComponentModel;
using Domain.ImagePair;
using Domain.Projections;

namespace Domain.Client.ImagePair;

/// <summary>
/// Drei explizite Arbeitsmodi statt kombinierter Booleans.
///
///   Live    — springt zu neuen Teilen, zeigt aktuelle Seite
///   Inspekt — freie Navigation, kein Auto-Sprung
///   Tag     — wie Inspekt, aber gefiltert auf einen Produktionstag
/// </summary>
public enum ArbeitsModus { Live, Inspekt, Tag }

public partial class ImagePairStore : ObservableObject
{
    // ─── Bus (wird bei Aktivierung gesetzt) ───

    private IBus? _bus;

    /// <summary>
    /// Wird vom CircuitHandler nach SubscribeAll aufgerufen.
    /// Gibt dem Store die Möglichkeit, Commands und Queries
    /// auf dem Bus zu publizieren.
    /// </summary>
    public void Initialisiere(IBus bus)
    {
        _bus = bus;
    }

    // ═══════════════════════════════════════════════════════════════
    // Observable State — Domain
    // ═══════════════════════════════════════════════════════════════

    public ObservableCollection<ImagePairEntry> Items { get; } = new();

    [ObservableProperty] private ImagePairStatistikAntwort? _statistik;
    [ObservableProperty] private object? _letzteAblehnung;
    [ObservableProperty] private bool _isLoaded;
    [ObservableProperty] private LetzteDateiInfo? _letzteVerfuegbareDatei;

    // ─── Pagination ───

    [ObservableProperty] private int _gesamtAnzahl;
    [ObservableProperty] private int _seite = 1;
    [ObservableProperty] private int _seitenGroesse = 25; // TODO: zurück auf 50 nach Test

    public int GesamtSeiten => SeitenGroesse > 0
        ? (int)Math.Ceiling((double)GesamtAnzahl / SeitenGroesse)
        : 0;

    // ─── Render-Signal ───

    [ObservableProperty] private int _renderGeneration;

    // ═══════════════════════════════════════════════════════════════
    // Observable State — Modus & Navigation
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty] private ArbeitsModus _modus = ArbeitsModus.Live;
    [ObservableProperty] private DateTimeOffset? _tagLabelingDatum;

    [ObservableProperty] private int _selectedIndex;
    [ObservableProperty] private string _filterWert = "alle";
    [ObservableProperty] private bool _nurNichtInspizierte;

    /// <summary>Abwärtskompatibel — wird von keiner View mehr direkt gesetzt.</summary>
    public bool IstLive => Modus == ArbeitsModus.Live;

    /// <summary>
    /// Das aktuell ausgewählte ImagePairEntry.
    /// Wird aus SelectedIndex abgeleitet.
    /// </summary>
    public ImagePairEntry? Aktuell =>
        _selectedIndex >= 0 && _selectedIndex < Items.Count
            ? Items[_selectedIndex] : null;

    // ─── Interner Workflow-State (nicht observable) ───

    /// <summary>
    /// Ziel-Index nach der nächsten Re-Query.
    ///   null  → ID-Recovery (Inspekt/Tag-Hintergrund-Refresh)
    ///   0     → erstes Item (Live-Sprung, Filter-Wechsel, Seitenwechsel vorwärts)
    ///   -1    → letztes Item (Seitenwechsel rückwärts)
    /// </summary>
    private int? _pendingIndex;
    private bool _ersteLadung = true;
    private readonly HashSet<string> _requestedPaths = new();

    // ═══════════════════════════════════════════════════════════════
    // Observable State — Chart
    // ═══════════════════════════════════════════════════════════════

    [ObservableProperty] private ProduktionsVerlaufAntwort? _verlauf;
    [ObservableProperty] private int _verlaufGeneration;

    [ObservableProperty] private ProduktionsTageAntwort? _produktionsTage;
    [ObservableProperty] private int _produktionsTageGeneration;

    [ObservableProperty] private ProduktionsStripAntwort? _strip;
    [ObservableProperty] private int _stripGeneration;

    [ObservableProperty] private DateTimeOffset? _gewaehlterTag;
    [ObservableProperty] private bool _warteAufStrip;

    // ═══════════════════════════════════════════════════════════════
    // Interner Index
    // ═══════════════════════════════════════════════════════════════

    private readonly Dictionary<Guid, ImagePairEntry> _index = new();

    public ImagePairEntry? FindById(Guid id)
        => _index.TryGetValue(id, out var e) ? e : null;

    private ImagePairEntry GetOrCreate(Guid id)
    {
        if (_index.TryGetValue(id, out var existing))
            return existing;
        var entry = new ImagePairEntry { Id = id };
        _index[id] = entry;
        Items.Add(entry);
        return entry;
    }

    private void ReplaceAll(IReadOnlyList<ImagePairAntwort> antworten)
    {
        Items.Clear();
        _index.Clear();

        foreach (var antwort in antworten)
            ApplyAntwort(antwort);
    }

    // ═══════════════════════════════════════════════════════════════
    // Öffentliche Methoden — User-Intents
    // ═══════════════════════════════════════════════════════════════

    // ─── Modus ───

    /// <summary>
    /// Zyklischer Modus-Wechsel über den Header-Chip.
    ///
    ///   Live    → Inspekt
    ///   Inspekt → Live
    ///   Tag     → Live (beendet Tag-Labeling)
    /// </summary>
    public void ToggleModus()
    {
        switch (Modus)
        {
            case ArbeitsModus.Live:
                Modus = ArbeitsModus.Inspekt;
                break;

            case ArbeitsModus.Inspekt:
                Modus = ArbeitsModus.Live;
                break;

            case ArbeitsModus.Tag:
                BeendeTagLabeling();
                break;
        }
    }

    /// <summary>
    /// Startet den Tag-Labeling-Modus für den aktuell im Chart
    /// gewählten Tag. Wechselt auf Inspekt, filtert die Liste
    /// auf diesen Tag und lädt die erste Seite.
    /// </summary>
    public void StarteTagLabeling()
    {
        if (GewaehlterTag == null) return;

        TagLabelingDatum = GewaehlterTag;
        Modus = ArbeitsModus.Tag;
        _pendingIndex = 0;
        _bus?.Publish(new SucheImagePairs(BaueFilter()));
    }

    /// <summary>
    /// Beendet den Tag-Labeling-Modus und kehrt zu Live zurück.
    /// Hebt den Tag-Filter auf und lädt die ungefilterte Liste.
    /// </summary>
    public void BeendeTagLabeling()
    {
        TagLabelingDatum = null;
        Modus = ArbeitsModus.Live;
        _pendingIndex = 0;
        _bus?.Publish(new SucheImagePairs(BaueFilter()));
    }

    // ─── Navigation ───

    public void WechsleZu(int index)
    {
        if (index < 0 || index >= Items.Count) return;
        SelectedIndex = index;
        OnPropertyChanged(nameof(Aktuell));

        var entry = Items[index];

        if (!entry.IstInspiziert)
            _bus?.Publish(new MarkiereAlsInspiziert(entry.Id));

        entry.Historie.Clear();
        entry.HistorieGeladen = false;
        _bus?.Publish(new GetImagePairHistorie(entry.Id));

        PreloadBilder();
    }

    public void NavigateNext()
    {
        if (SelectedIndex < Items.Count - 1)
            WechsleZu(SelectedIndex + 1);
        else if (Seite < GesamtSeiten)
        {
            _pendingIndex = 0;
            _bus?.Publish(new SucheImagePairs(BaueFilter(seite: Seite + 1)));
        }
    }

    public void NavigatePrev()
    {
        if (SelectedIndex > 0)
            WechsleZu(SelectedIndex - 1);
        else if (Seite > 1)
        {
            _pendingIndex = -1; // letztes Item der vorigen Seite
            _bus?.Publish(new SucheImagePairs(BaueFilter(seite: Seite - 1)));
        }
    }

    public void NavigateNextOffen()
    {
        for (var i = SelectedIndex + 1; i < Items.Count; i++)
            if (Items[i].PhysischesProduktLabel == null
                || Items[i].MenschBildpaarLabel == null)
            { WechsleZu(i); return; }
    }

    // ─── Labeling ───

    public void LabelProdukt(Klassifikation label)
    {
        if (Aktuell is { } e)
            _bus?.Publish(new LabelPhysischesProdukt(e.Id, label));
    }

    public void LabelKamera(Klassifikation label)
    {
        if (Aktuell is { } e)
            _bus?.Publish(new LabelBildPaar(e.Id, label));
    }

    // ─── Filter & Pagination ───

    public void SetFilter(string wert)
    {
        FilterWert = wert;
        _pendingIndex = 0;
        _bus?.Publish(new SucheImagePairs(BaueFilter()));
    }

    public void ToggleInspektionsFilter()
    {
        NurNichtInspizierte = !NurNichtInspizierte;
        _pendingIndex = 0;
        _bus?.Publish(new SucheImagePairs(BaueFilter()));
    }

    public void VorigeSeite()
    {
        if (Seite > 1)
        {
            _pendingIndex = 0;
            _bus?.Publish(new SucheImagePairs(BaueFilter(seite: Seite - 1)));
        }
    }

    public void NaechsteSeite()
    {
        if (Seite < GesamtSeiten)
        {
            _pendingIndex = 0;
            _bus?.Publish(new SucheImagePairs(BaueFilter(seite: Seite + 1)));
        }
    }

    // ─── Chart ───

    public void LadeProduktionsTage()
    {
        _bus?.Publish(new GetProduktionsTage());
    }

    public void LadeStripFuerTag(DateTimeOffset datum)
    {
        GewaehlterTag = datum.Date;
        WarteAufStrip = true;

        var von = new DateTimeOffset(datum.Date, TimeSpan.Zero);
        _bus?.Publish(new GetProduktionsStrip(von, von.AddDays(1)));
    }

    // ─── Initiale Daten ───

    /// <summary>
    /// Lädt die erste Seite über BaueFilter — nutzt die konfigurierte
    /// SeitenGroesse statt den Server-Default. Wird vom CircuitHandler
    /// beim Start aufgerufen.
    /// </summary>
    public void LadeInitialeDaten()
    {
        _pendingIndex = 0;
        _bus?.Publish(new SucheImagePairs(BaueFilter()));
    }

    // ═══════════════════════════════════════════════════════════════
    // Query-Responses → Store befüllen
    // ═══════════════════════════════════════════════════════════════

    void Handle(ImagePairSuchergebnis result, MessageContext ctx)
    {
        // Aktuelle Auswahl merken (ID, nicht Index — Index verschiebt sich)
        var selectedId = Aktuell?.Id;

        ReplaceAll(result.Items);

        GesamtAnzahl = result.GesamtAnzahl;
        Seite = result.Seite;
        SeitenGroesse = result.SeitenGroesse;
        OnPropertyChanged(nameof(GesamtSeiten));

        IsLoaded = true;
        _requestedPaths.Clear();

        if (_pendingIndex != null && Items.Count > 0)
        {
            // Explizites Sprungziel: Live-Update, Seitenwechsel, Filter-Wechsel
            //   0  → erstes Item (neuestes bei absteigender Sortierung)
            //  -1  → letztes Item (NavigatePrev über Seitengrenze)
            var ziel = _pendingIndex.Value < 0
                ? Items.Count - 1
                : Math.Min(_pendingIndex.Value, Items.Count - 1);
            _pendingIndex = null;
            WechsleZu(ziel);
        }
        else if (_ersteLadung && Items.Count > 0)
        {
            WechsleZu(0);
            _ersteLadung = false;
        }
        else if (selectedId != null && Items.Count > 0)
        {
            // Inspekt/Tag Hintergrund-Refresh: Auswahl anhand ID wiederfinden.
            // Kein WechsleZu — kein erneutes MarkiereAlsInspiziert/Historie.
            var gefunden = false;
            for (int i = 0; i < Items.Count; i++)
            {
                if (Items[i].Id == selectedId)
                {
                    SelectedIndex = i;
                    gefunden = true;
                    break;
                }
            }
            if (!gefunden)
                SelectedIndex = 0;

            OnPropertyChanged(nameof(Aktuell));
            PreloadBilder();
        }
        else
        {
            PreloadBilder();
        }

        RenderGeneration++;
    }

    void Handle(ImagePairAntwort antwort, MessageContext ctx)
    {
        ApplyAntwort(antwort);
        RenderGeneration++;
    }

    void Handle(ImagePairStatistikAntwort statistik, MessageContext ctx)
    {
        Statistik = statistik;
    }

    void Handle(ImagePairArbeitsliste liste, MessageContext ctx)
    {
        ReplaceAll(liste.Items);

        _requestedPaths.Clear();

        if (_ersteLadung && Items.Count > 0)
        {
            WechsleZu(0);
            _ersteLadung = false;
        }
        else
        {
            PreloadBilder();
        }

        RenderGeneration++;
    }

    void Handle(ImagePairNichtGefundenAntwort antwort, MessageContext ctx)
    {
        LetzteAblehnung = antwort;
    }

    void Handle(ProduktionsVerlaufAntwort antwort, MessageContext ctx)
    {
        Verlauf = antwort;
        VerlaufGeneration++;
    }

    void Handle(ProduktionsTageAntwort antwort, MessageContext ctx)
    {
        ProduktionsTage = antwort;
        ProduktionsTageGeneration++;
    }

    void Handle(ProduktionsStripAntwort antwort, MessageContext ctx)
    {
        Strip = antwort;
        StripGeneration++;
        WarteAufStrip = false;
    }

    private void ApplyAntwort(ImagePairAntwort a)
    {
        var entry = GetOrCreate(a.Id);

        entry.PairKey = a.PairKey;
        entry.ProduziertAm = a.ProduziertAm;
        entry.AufgenommenAm = a.AufgenommenAm;
        entry.IstKomplett = a.IstKomplett;
        entry.UrsprungsPfad = a.UrsprungsPfad;

        entry.Dc0Pfad = a.Dc0Pfad;
        entry.Dc0KiBildKlassifikation = a.Dc0KiBildKlassifikation;
        entry.Dc0MenschBildLabel = a.Dc0MenschBildLabel;
        CopyRegionen(a.Dc0KiRegionen, entry.Dc0KiRegionen);
        CopyRegionen(a.Dc0MenschRegionen, entry.Dc0MenschRegionen);

        entry.Dc2Pfad = a.Dc2Pfad;
        entry.Dc2KiBildKlassifikation = a.Dc2KiBildKlassifikation;
        entry.Dc2MenschBildLabel = a.Dc2MenschBildLabel;
        CopyRegionen(a.Dc2KiRegionen, entry.Dc2KiRegionen);
        CopyRegionen(a.Dc2MenschRegionen, entry.Dc2MenschRegionen);

        entry.KiBildpaarKlassifikation = a.KiBildpaarKlassifikation;
        entry.MenschBildpaarLabel = a.MenschBildpaarLabel;
        entry.PhysischesProduktLabel = a.PhysischesProduktLabel;
        entry.IstInspiziert = a.IstInspiziert;
    }

    // ═══════════════════════════════════════════════════════════════
    // Historie — Query-Response
    // ═══════════════════════════════════════════════════════════════

    void Handle(ImagePairHistorieAntwort antwort, MessageContext ctx)
    {
        var entry = FindById(antwort.PairId);
        if (entry == null) return;

        entry.Historie.Clear();
        foreach (var eintrag in antwort.Eintraege)
            entry.Historie.Add(eintrag);
        entry.HistorieGeladen = true;
        RenderGeneration++;
    }

    void Handle(ImagePairHistorieNichtGefunden antwort, MessageContext ctx)
    {
        var entry = FindById(antwort.PairId);
        if (entry != null)
        {
            entry.Historie.Clear();
            entry.HistorieGeladen = true;
        }
        RenderGeneration++;
    }

    void Handle(QueryFailed evt, MessageContext ctx)
    {
        Console.WriteLine($"[Store] ⚠ QueryFailed: {evt.QueryType} — {evt.ErrorMessage}");
    }

    // ═══════════════════════════════════════════════════════════════
    // Server-Events (live)
    // ═══════════════════════════════════════════════════════════════

    void Handle(ImagePairErstellt evt, MessageContext ctx)
    {
        // Strip: neues Teil anhängen (unabhängig ob Entry im Set)
        if (Strip != null)
        {
            var neuePunkte = Strip.Punkte
                .Append(new ProduktionsStripPunkt(evt.ProduziertAm, null))
                .OrderBy(p => p.Zeitpunkt)
                .ToList();
            Strip = new ProduktionsStripAntwort(neuePunkte);
        }

        var entry = FindById(ctx.AggregateId);
        if (entry == null)
        {
            // Immer re-querien — der Filter regelt was zurückkommt.
            // Live:    Seite 1 (neuestes Bild ist dort, absteigend sortiert)
            // Inspekt: aktuelle Seite auffrischen
            // Tag:     aktuelle Seite (Von/Bis in BaueFilter)
            var zielSeite = (Modus == ArbeitsModus.Live) ? 1 : Seite;
            _bus?.Publish(new SucheImagePairs(BaueFilter(seite: zielSeite)));

            // Auto-Navigation nur im Live-Modus
            if (Modus == ArbeitsModus.Live)
                _pendingIndex = 0;

            RenderGeneration++;
            return;
        }

        entry.PairKey = evt.PairKey;
        entry.ProduziertAm = evt.ProduziertAm;
        entry.AufgenommenAm = evt.AufgenommenAm;
        entry.UrsprungsPfad = evt.UrsprungsPfad;

        AppendLiveHistorie(entry, ctx, nameof(ImagePairErstellt),
            "ImagePair erstellt", HistorieKategorie.Lifecycle,
            $"PairKey: {evt.PairKey}");
        RenderGeneration++;
    }

    void Handle(BildVerfuegbar evt, MessageContext ctx)
    {
        var entry = FindById(ctx.AggregateId);
        if (entry == null) return;

        switch (evt.Version)
        {
            case BildVersion.Dc0: entry.Dc0Pfad = evt.Pfad; break;
            case BildVersion.Dc2: entry.Dc2Pfad = evt.Pfad; break;
        }

        // Wenn dieses Entry gerade angezeigt wird (oder in Preload-Reichweite),
        // sofort Datei laden — PreloadBilder() lief bereits als Pfad noch null war.
        if (entry == Aktuell)
            RequestFile(evt.Pfad);

        LetzteVerfuegbareDatei = new LetzteDateiInfo(
            evt.Meta.OriginalDateiname, evt.Pfad, evt.Version,
            ctx.AggregateId, evt.Meta.DateigroesseBytes, ctx.CreatedAtUtc);

        AppendLiveHistorie(entry, ctx, nameof(BildVerfuegbar),
            $"{evt.Version} verfügbar", HistorieKategorie.Lifecycle,
            $"{evt.Meta.BreitePixel}×{evt.Meta.HoehePixel}");
        RenderGeneration++;
    }

    void Handle(ImagePairKomplett evt, MessageContext ctx)
    {
        var entry = FindById(ctx.AggregateId);
        if (entry != null)
        {
            entry.IstKomplett = true;
            AppendLiveHistorie(entry, ctx, nameof(ImagePairKomplett),
                "Beide Bilder verfügbar", HistorieKategorie.Lifecycle);
        }
        RenderGeneration++;
    }

    void Handle(EinzelBildDurchKiKlassifiziert evt, MessageContext ctx)
    {
        var entry = FindById(ctx.AggregateId);
        if (entry == null) return;

        switch (evt.Version)
        {
            case BildVersion.Dc0:
                entry.Dc0KiBildKlassifikation = evt.BildLabel;
                for (int i = 0; i < 8 && i < evt.RegionLabels.Count; i++)
                    entry.Dc0KiRegionen[i] = evt.RegionLabels[i];
                break;
            case BildVersion.Dc2:
                entry.Dc2KiBildKlassifikation = evt.BildLabel;
                for (int i = 0; i < 8 && i < evt.RegionLabels.Count; i++)
                    entry.Dc2KiRegionen[i] = evt.RegionLabels[i];
                break;
        }

        AppendLiveHistorie(entry, ctx, nameof(EinzelBildDurchKiKlassifiziert),
            $"KI: {evt.Version} klassifiziert", HistorieKategorie.Ki,
            FormatKlassifikation(evt.BildLabel));
        RenderGeneration++;
    }

    void Handle(BildPaarDurchKiKlassifiziert evt, MessageContext ctx)
    {
        var entry = FindById(ctx.AggregateId);
        if (entry != null)
        {
            entry.KiBildpaarKlassifikation = evt.Label;
            AppendLiveHistorie(entry, ctx, nameof(BildPaarDurchKiKlassifiziert),
                "KI: Bildpaar klassifiziert", HistorieKategorie.Ki,
                FormatKlassifikation(evt.Label));
        }
        RenderGeneration++;
    }

    void Handle(BildRegionGelabelt evt, MessageContext ctx)
    {
        var entry = FindById(ctx.AggregateId);
        if (entry != null)
        {
            entry.GetMenschRegionen(evt.Version)[evt.RegionIndex] = evt.Label;
            AppendLiveHistorie(entry, ctx, nameof(BildRegionGelabelt),
                $"Region {evt.RegionIndex} gelabelt ({evt.Version})", HistorieKategorie.Mensch,
                FormatKlassifikation(evt.Label));
        }
        RenderGeneration++;
    }

    void Handle(EinzelBildGelabelt evt, MessageContext ctx)
    {
        var entry = FindById(ctx.AggregateId);
        if (entry == null) return;
        switch (evt.Version)
        {
            case BildVersion.Dc0: entry.Dc0MenschBildLabel = evt.Label; break;
            case BildVersion.Dc2: entry.Dc2MenschBildLabel = evt.Label; break;
        }
        AppendLiveHistorie(entry, ctx, nameof(EinzelBildGelabelt),
            $"Einzelbild {evt.Version} gelabelt", HistorieKategorie.Mensch,
            FormatKlassifikation(evt.Label));
        RenderGeneration++;
    }

    void Handle(BildPaarGelabelt evt, MessageContext ctx)
    {
        var entry = FindById(ctx.AggregateId);
        if (entry != null)
        {
            entry.MenschBildpaarLabel = evt.Label;
            AppendLiveHistorie(entry, ctx, nameof(BildPaarGelabelt),
                "Bildpaar gelabelt", HistorieKategorie.Mensch,
                FormatKlassifikation(evt.Label));
        }
        RenderGeneration++;
    }

    void Handle(PhysischesProduktGelabelt evt, MessageContext ctx)
    {
        var entry = FindById(ctx.AggregateId);
        if (entry != null)
        {
            entry.PhysischesProduktLabel = evt.Label;

            if (Strip != null)
            {
                Strip = new ProduktionsStripAntwort(
                    Strip.Punkte.Select(p =>
                        p.Zeitpunkt == entry.ProduziertAm
                            ? p with { ProduktLabel = evt.Label }
                            : p).ToList());
            }

            AppendLiveHistorie(entry, ctx, nameof(PhysischesProduktGelabelt),
                "Produkt gelabelt", HistorieKategorie.Mensch,
                FormatKlassifikation(evt.Label));
        }
        RenderGeneration++;
    }

    void Handle(ImagePairInspiziert evt, MessageContext ctx)
    {
        var entry = FindById(ctx.AggregateId);
        if (entry != null)
        {
            entry.IstInspiziert = true;
            AppendLiveHistorie(entry, ctx, nameof(ImagePairInspiziert),
                "Inspiziert", HistorieKategorie.Inspektion);
        }
        RenderGeneration++;
    }

    // ═══════════════════════════════════════════════════════════════
    // FileBridge + Ablehnungen + Client-Events
    // ═══════════════════════════════════════════════════════════════

    void Handle(FileLoaded evt, MessageContext ctx)
    {
        foreach (var entry in Items)
        {
            if (entry.Dc0Pfad == evt.SourcePath && entry.Dc0BildSrc == null)
                entry.Dc0BildSrc = evt.AccessUrl;
            if (entry.Dc2Pfad == evt.SourcePath && entry.Dc2BildSrc == null)
                entry.Dc2BildSrc = evt.AccessUrl;
        }
        RenderGeneration++;
    }

    void Handle(BildNichtVerfuegbar evt, MessageContext ctx) { LetzteAblehnung = evt; }
    void Handle(RegionIndexUngueltig evt, MessageContext ctx) { LetzteAblehnung = evt; }
    void Handle(PaarNichtKomplett evt, MessageContext ctx) { LetzteAblehnung = evt; }
    void Handle(ImagePairNichtGefunden evt, MessageContext ctx) { LetzteAblehnung = evt; }
    void Handle(ImagePairEingabeUngueltig evt, MessageContext ctx) { LetzteAblehnung = evt; }

    void Handle(ImagePairStatistikBerechnet evt, MessageContext ctx) { }

    // ═══════════════════════════════════════════════════════════════
    // Interne Logik — Filter, Preload
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Baut den Filter. Im Tag-Modus wird Von/Bis auf den
    /// gewählten Tag eingeschränkt (00:00 – 24:00 UTC).
    /// </summary>
    private ImagePairFilter BaueFilter(int? seite = null)
    {
        DateTimeOffset? von = null, bis = null;

        if (Modus == ArbeitsModus.Tag && TagLabelingDatum is { } tag)
        {
            von = new DateTimeOffset(tag.Date, TimeSpan.Zero);
            bis = von.Value.AddDays(1);
        }

        return FilterWert switch
        {
            "offen"    => new ImagePairFilter(Von: von, Bis: bis,
                              HatMenschLabel: false,
                              NurNichtInspizierte: NurNichtInspizierteWert(),
                              Seite: seite ?? 1,
                              SeitenGroesse: SeitenGroesse),
            "anomalie" => new ImagePairFilter(Von: von, Bis: bis,
                              ProduktLabel: Klassifikation.Anomalie,
                              NurNichtInspizierte: NurNichtInspizierteWert(),
                              Seite: seite ?? 1,
                              SeitenGroesse: SeitenGroesse),
            _          => new ImagePairFilter(Von: von, Bis: bis,
                              NurNichtInspizierte: NurNichtInspizierteWert(),
                              Seite: seite ?? 1,
                              SeitenGroesse: SeitenGroesse)
        };
    }

    private bool? NurNichtInspizierteWert() =>
        NurNichtInspizierte ? true : null;

    private void PreloadBilder()
    {
        for (var offset = 0; offset <= 2; offset++)
        {
            var i = SelectedIndex + offset;
            if (i >= Items.Count) break;
            RequestFile(Items[i].Dc0Pfad);
            RequestFile(Items[i].Dc2Pfad);
        }
    }

    private void RequestFile(string? pfad)
    {
        if (!string.IsNullOrEmpty(pfad) && _requestedPaths.Add(pfad))
            _bus?.Publish(new LoadFile(pfad));
    }

    // ═══════════════════════════════════════════════════════════════
    // Live-Historie
    // ═══════════════════════════════════════════════════════════════

    private static void AppendLiveHistorie(
        ImagePairEntry entry, MessageContext ctx,
        string ereignisTyp, string beschreibung,
        HistorieKategorie kategorie, string? details = null)
    {
        entry.Historie.Add(new HistorieEintrag(
            Zeitpunkt: ctx.CreatedAtUtc,
            EreignisTyp: ereignisTyp,
            Beschreibung: beschreibung,
            Kategorie: kategorie,
            Details: details));
    }

    // ─── Hilfsmethoden ───

    private static void CopyRegionen(IReadOnlyList<Klassifikation?> source, Klassifikation?[] target)
    {
        for (int i = 0; i < 8 && i < source.Count; i++)
            target[i] = source[i];
    }

    private static string FormatKlassifikation(Klassifikation label) => label switch
    {
        Klassifikation.KeineAnomalie => "OK",
        Klassifikation.Questionable  => "Fraglich",
        Klassifikation.Anomalie      => "Anomalie",
        _                            => label.ToString()
    };
}

public record LetzteDateiInfo(
    string FileName, string Pfad, BildVersion Version,
    Guid PairId, long SizeBytes, DateTimeOffset EmpfangenAm);