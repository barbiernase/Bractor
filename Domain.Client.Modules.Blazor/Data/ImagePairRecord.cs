using System.Collections.Immutable;
using Domain.ImagePair;
using Domain.Projections;

namespace Domain.Client.Modules;

public record ImagePairRecord(
    Guid Id, string? PairKey,
    DateTimeOffset ProduziertAm, DateTimeOffset AufgenommenAm,
    bool IstKomplett, string? UrsprungsPfad,
    string? Dc0Pfad, Klassifikation? Dc0KiBildKlassifikation,
    Klassifikation? Dc0MenschBildLabel, string? Dc0BildSrc,
    ImmutableArray<Klassifikation?> Dc0KiRegionen, ImmutableArray<Klassifikation?> Dc0MenschRegionen,
    string? Dc2Pfad, Klassifikation? Dc2KiBildKlassifikation,
    Klassifikation? Dc2MenschBildLabel, string? Dc2BildSrc,
    ImmutableArray<Klassifikation?> Dc2KiRegionen, ImmutableArray<Klassifikation?> Dc2MenschRegionen,
    Klassifikation? KiBildpaarKlassifikation, Klassifikation? MenschBildpaarLabel,
    Klassifikation? PhysischesProduktLabel, bool IstInspiziert)
{
    public bool HatKiKlassifikation => Dc0KiBildKlassifikation != null || Dc2KiBildKlassifikation != null;
    public bool HatMenschLabel => Dc0MenschBildLabel != null || Dc2MenschBildLabel != null;

    public ImagePairRecord WithDc0MenschRegion(int i, Klassifikation? l) =>
        this with { Dc0MenschRegionen = Dc0MenschRegionen.SetItem(i, l) };
    public ImagePairRecord WithDc2MenschRegion(int i, Klassifikation? l) =>
        this with { Dc2MenschRegionen = Dc2MenschRegionen.SetItem(i, l) };

    public static ImagePairRecord FromAntwort(ImagePairAntwort a) => new(
        a.Id, a.PairKey, a.ProduziertAm, a.AufgenommenAm, a.IstKomplett, a.UrsprungsPfad,
        a.Dc0Pfad, a.Dc0KiBildKlassifikation, a.Dc0MenschBildLabel, null,
        ToRegions(a.Dc0KiRegionen), ToRegions(a.Dc0MenschRegionen),
        a.Dc2Pfad, a.Dc2KiBildKlassifikation, a.Dc2MenschBildLabel, null,
        ToRegions(a.Dc2KiRegionen), ToRegions(a.Dc2MenschRegionen),
        a.KiBildpaarKlassifikation, a.MenschBildpaarLabel,
        a.PhysischesProduktLabel, a.IstInspiziert);

    /// <summary>
    /// Frisches, gerade produziertes Paar (Live-Insert aus ImagePairErstellt).
    /// Alles ungelabelt/uninspiziert; Regionen-Arrays 8-lang mit null, damit
    /// spätere WithDc*MenschRegion(i, …)-Aufrufe (SetItem) nicht ins Leere greifen.
    /// Bilder/KI/Labels tröpfeln danach als Patch nach.
    /// </summary>
    public static ImagePairRecord Neu(
        Guid id, string? pairKey,
        DateTimeOffset produziertAm, DateTimeOffset aufgenommenAm, string? ursprungsPfad) => new(
        id, pairKey, produziertAm, aufgenommenAm, IstKomplett: false, ursprungsPfad,
        Dc0Pfad: null, Dc0KiBildKlassifikation: null, Dc0MenschBildLabel: null, Dc0BildSrc: null,
        Dc0KiRegionen: LeereRegionen(), Dc0MenschRegionen: LeereRegionen(),
        Dc2Pfad: null, Dc2KiBildKlassifikation: null, Dc2MenschBildLabel: null, Dc2BildSrc: null,
        Dc2KiRegionen: LeereRegionen(), Dc2MenschRegionen: LeereRegionen(),
        KiBildpaarKlassifikation: null, MenschBildpaarLabel: null,
        PhysischesProduktLabel: null, IstInspiziert: false);

    private static ImmutableArray<Klassifikation?> ToRegions(IReadOnlyList<Klassifikation?> src)
    {
        var b = ImmutableArray.CreateBuilder<Klassifikation?>(8);
        for (int i = 0; i < 8; i++) b.Add(i < src.Count ? src[i] : null);
        return b.MoveToImmutable();
    }

    private static ImmutableArray<Klassifikation?> LeereRegionen()
    {
        var b = ImmutableArray.CreateBuilder<Klassifikation?>(8);
        for (int i = 0; i < 8; i++) b.Add(null);
        return b.MoveToImmutable();
    }
}
