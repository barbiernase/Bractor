// ── Domain.Client/ImagePair/Stores/ListStore.cs ──

using System.Collections.Immutable;
using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Collections;
using CommunityToolkit.Mvvm.ComponentModel;
using Domain.ImagePair;
using Domain.Projections;

namespace Domain.Client.ImagePair;

/// <summary>
/// Hält die aktuelle Seite von ImagePair-Records als TrackedMap.
///
/// Empfängt:
///   - ImagePairSuchergebnis    → ReplaceAll + Pagination
///   - ImagePairArbeitsliste    → ReplaceAll (ohne Pagination)
///   - ImagePairAntwort         → einzelnen Record upserten
///   - Alle Server-Events       → in-place Update per TrackedMap.Update
///   - FileLoaded               → BildSrc setzen
///
/// Publisht nichts. Liest keine anderen Stores.
/// Subscribed als 1. Kind im Workspace.
/// </summary>
public partial class ListStore : StoreBase
{
    private readonly TrackedMap<Guid, ImagePairRecord> _items;

    [ObservableProperty] private int _gesamtAnzahl;
    [ObservableProperty] private int _seite = 1;
    [ObservableProperty] private int _seitenGroesse = 50;
    [ObservableProperty] private bool _isLoaded;

    public int GesamtSeiten => SeitenGroesse > 0
        ? (int)Math.Ceiling((double)GesamtAnzahl / SeitenGroesse)
        : 0;

    public ListStore()
    {
        _items = CreateMap<Guid, ImagePairRecord>(r => r.Id);
    }

    // ═══════════════════════════════════════════════════
    // LESEZUGRIFF
    // ═══════════════════════════════════════════════════

    public int Count => _items.Count;
    public ImagePairRecord? FindById(Guid id) => _items.Get(id);
    public ImagePairRecord this[int index] => _items[index];
    public IEnumerable<ImagePairRecord> Values => _items.Values;

    // ═══════════════════════════════════════════════════
    // HANDLE — Query-Responses
    // ═══════════════════════════════════════════════════

    void Handle(ImagePairSuchergebnis result, MessageContext ctx)
    {
        _items.ReplaceAll(result.Items.Select(MapFromAntwort));
        GesamtAnzahl = result.GesamtAnzahl;
        Seite = result.Seite;
        SeitenGroesse = result.SeitenGroesse;
        OnPropertyChanged(nameof(GesamtSeiten));
        IsLoaded = true;
    }

    void Handle(ImagePairArbeitsliste liste, MessageContext ctx)
    {
        _items.ReplaceAll(liste.Items.Select(MapFromAntwort));
    }

    void Handle(ImagePairAntwort antwort, MessageContext ctx)
    {
        _items.Put(MapFromAntwort(antwort));
    }

    // ═══════════════════════════════════════════════════
    // HANDLE — Server-Events (live)
    // ═══════════════════════════════════════════════════

    void Handle(ImagePairErstellt evt, MessageContext ctx)
    {
        if (_items.Get(ctx.AggregateId) == null) return;
        _items.Update(ctx.AggregateId, e => e with
        {
            PairKey = evt.PairKey,
            ProduziertAm = evt.ProduziertAm,
            AufgenommenAm = evt.AufgenommenAm,
            UrsprungsPfad = evt.UrsprungsPfad
        });
    }

    void Handle(BildVerfuegbar evt, MessageContext ctx)
    {
        if (_items.Get(ctx.AggregateId) == null) return;
        _items.Update(ctx.AggregateId, e => evt.Version switch
        {
            BildVersion.Dc0 => e with { Dc0Pfad = evt.Pfad },
            BildVersion.Dc2 => e with { Dc2Pfad = evt.Pfad },
            _ => e
        });
    }

    void Handle(ImagePairKomplett evt, MessageContext ctx)
    {
        if (_items.Get(ctx.AggregateId) == null) return;
        _items.Update(ctx.AggregateId, e => e with { IstKomplett = true });
    }

    void Handle(EinzelBildDurchKiKlassifiziert evt, MessageContext ctx)
    {
        if (_items.Get(ctx.AggregateId) == null) return;
        _items.Update(ctx.AggregateId, e => evt.Version switch
        {
            BildVersion.Dc0 => e with
            {
                Dc0KiBildKlassifikation = evt.BildLabel,
                Dc0KiRegionen = MapRegionen(evt.RegionLabels)
            },
            BildVersion.Dc2 => e with
            {
                Dc2KiBildKlassifikation = evt.BildLabel,
                Dc2KiRegionen = MapRegionen(evt.RegionLabels)
            },
            _ => e
        });
    }

    void Handle(BildPaarDurchKiKlassifiziert evt, MessageContext ctx)
    {
        if (_items.Get(ctx.AggregateId) == null) return;
        _items.Update(ctx.AggregateId, e => e with { KiBildpaarKlassifikation = evt.Label });
    }

    void Handle(BildRegionGelabelt evt, MessageContext ctx)
    {
        if (_items.Get(ctx.AggregateId) == null) return;
        _items.Update(ctx.AggregateId, e => evt.Version switch
        {
            BildVersion.Dc0 => e.WithDc0MenschRegion(evt.RegionIndex, evt.Label),
            BildVersion.Dc2 => e.WithDc2MenschRegion(evt.RegionIndex, evt.Label),
            _ => e
        });
    }

    void Handle(EinzelBildGelabelt evt, MessageContext ctx)
    {
        if (_items.Get(ctx.AggregateId) == null) return;
        _items.Update(ctx.AggregateId, e => evt.Version switch
        {
            BildVersion.Dc0 => e with { Dc0MenschBildLabel = evt.Label },
            BildVersion.Dc2 => e with { Dc2MenschBildLabel = evt.Label },
            _ => e
        });
    }

    void Handle(BildPaarGelabelt evt, MessageContext ctx)
    {
        if (_items.Get(ctx.AggregateId) == null) return;
        _items.Update(ctx.AggregateId, e => e with { MenschBildpaarLabel = evt.Label });
    }

    void Handle(PhysischesProduktGelabelt evt, MessageContext ctx)
    {
        if (_items.Get(ctx.AggregateId) == null) return;
        _items.Update(ctx.AggregateId, e => e with { PhysischesProduktLabel = evt.Label });
    }

    void Handle(ImagePairInspiziert evt, MessageContext ctx)
    {
        if (_items.Get(ctx.AggregateId) == null) return;
        _items.Update(ctx.AggregateId, e => e with { IstInspiziert = true });
    }

    // ═══════════════════════════════════════════════════
    // HANDLE — Client-Events
    // ═══════════════════════════════════════════════════

    void Handle(FileLoaded evt, MessageContext ctx)
    {
        foreach (var entry in _items.Values)
        {
            var updated = entry;
            if (entry.Dc0Pfad == evt.SourcePath && entry.Dc0BildSrc == null)
                updated = updated with { Dc0BildSrc = evt.AccessUrl };
            if (entry.Dc2Pfad == evt.SourcePath && entry.Dc2BildSrc == null)
                updated = updated with { Dc2BildSrc = evt.AccessUrl };

            if (!ReferenceEquals(updated, entry))
                _items.Put(updated);
        }
    }

    // ═══════════════════════════════════════════════════
    // MAPPING
    // ═══════════════════════════════════════════════════

    private static ImagePairRecord MapFromAntwort(ImagePairAntwort a) => new(
        Id: a.Id, PairKey: a.PairKey,
        ProduziertAm: a.ProduziertAm, AufgenommenAm: a.AufgenommenAm,
        IstKomplett: a.IstKomplett, UrsprungsPfad: a.UrsprungsPfad,
        Dc0Pfad: a.Dc0Pfad, Dc0KiBildKlassifikation: a.Dc0KiBildKlassifikation,
        Dc0MenschBildLabel: a.Dc0MenschBildLabel, Dc0BildSrc: null,
        Dc0KiRegionen: MapNullableRegionen(a.Dc0KiRegionen),
        Dc0MenschRegionen: MapNullableRegionen(a.Dc0MenschRegionen),
        Dc2Pfad: a.Dc2Pfad, Dc2KiBildKlassifikation: a.Dc2KiBildKlassifikation,
        Dc2MenschBildLabel: a.Dc2MenschBildLabel, Dc2BildSrc: null,
        Dc2KiRegionen: MapNullableRegionen(a.Dc2KiRegionen),
        Dc2MenschRegionen: MapNullableRegionen(a.Dc2MenschRegionen),
        KiBildpaarKlassifikation: a.KiBildpaarKlassifikation,
        MenschBildpaarLabel: a.MenschBildpaarLabel,
        PhysischesProduktLabel: a.PhysischesProduktLabel,
        IstInspiziert: a.IstInspiziert);

    private static ImmutableArray<Klassifikation?> MapRegionen(IReadOnlyList<Klassifikation> labels)
    {
        var builder = ImmutableArray.CreateBuilder<Klassifikation?>(8);
        for (int i = 0; i < 8; i++)
            builder.Add(i < labels.Count ? labels[i] : null);
        return builder.MoveToImmutable();
    }

    private static ImmutableArray<Klassifikation?> MapNullableRegionen(IReadOnlyList<Klassifikation?> labels)
    {
        var builder = ImmutableArray.CreateBuilder<Klassifikation?>(8);
        for (int i = 0; i < 8; i++)
            builder.Add(i < labels.Count ? labels[i] : null);
        return builder.MoveToImmutable();
    }
}
