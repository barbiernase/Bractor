using Abstractions;
using Bildvorschau.Katalog;
using Domain.ImagePair;

namespace Domain.Uebersetzungen.ImagePair;

#pragma warning disable CS9113

public sealed class BildGedeutet_NimmBildAuf_1(BildGedeutet source) : ITranslate<BildGedeutet, NimmBildAuf>
{
    public NimmBildAuf Translate() => new(
        AggregateId: new(source.Paar),
        PairKey: source.PairKey,
        ProduziertAm: source.ProduziertAm,
        AufgenommenAm: source.AufgenommenAm,
        UrsprungsPfad: source.UrsprungsPfad,
        Version: source.Version,
        OriginalDateiname: source.OriginalDateiname,
        DateigroesseBytes: source.DateigroesseBytes,
        BreitePixel: source.BreitePixel,
        HoehePixel: source.HoehePixel,
        ErstelltAm: source.ErstelltAm,
        Pfad: source.Pfad);
}
