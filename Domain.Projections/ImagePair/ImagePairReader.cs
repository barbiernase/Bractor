using Abstractions;
using Domain.ImagePair;

namespace Domain.Projections.ImagePair;

public sealed class ImagePairReader : IReader
{
    public async Task<OneOf<ImagePairAntwort, ImagePairNichtGefundenAntwort>>
        Frage(GetImagePair query, IFindImagePair findImagePair)
    {
        var model = await findImagePair.FindByIdAsync(query.PairId);

        if (model != null)
        {
            return ToAntwort(model);
        }

        return new ImagePairNichtGefundenAntwort(query.PairId);
    }

    public async Task<ImagePairSuchergebnis>
        Frage(SucheImagePairs query, ISearchImagePairs searchImagePairs)
    {
        var (items, gesamtAnzahl) = await searchImagePairs.SearchAsync(query.Filter);

        var antworten = items.Select(m =>
        {
            return ToAntwort(m);
        }).ToList();

        return new ImagePairSuchergebnis(
            antworten, gesamtAnzahl,
            query.Filter.Seite, query.Filter.SeitenGroesse);
    }

    public async Task<ImagePairStatistikAntwort>
        Frage(GetImagePairStatistik query, IGetImagePairStatistik getImagePairStatistik)
    {
        var s = await getImagePairStatistik.GetStatistikAsync();

        return new ImagePairStatistikAntwort(
            s.Gesamt, s.Komplett,
            s.MitKiKlassifikation, s.MitMenschLabel,
            s.MitProduktLabel, s.OhneKlassifikation,
            s.AnzahlAnomalienKi, s.AnzahlAnomalienMensch);
    }

    public async Task<ImagePairArbeitsliste>
        Frage(GetUnklassifizierteImagePairs query, IGetUnklassifizierte getUnklassifizierte)
    {
        var models = await getUnklassifizierte.GetUnklassifizierteAsync(query.MaxAnzahl);

        var antworten = models.Select(m =>
        {
            return ToAntwort(m);
        }).ToList();

        return new ImagePairArbeitsliste(antworten);
    }

    public async Task<ProduktionsVerlaufAntwort>
        Frage(GetProduktionsVerlauf query, IGetVerlauf getVerlauf)
    {
        return await getVerlauf.GetVerlaufAsync(
            query.Von, query.Bis, query.BucketMinuten);
    }

    public async Task<ProduktionsTageAntwort>
        Frage(GetProduktionsTage query, IGetProduktionsTage getProduktionsTage)
    {
        return await getProduktionsTage.GetProduktionsTageAsync();
    }

    public async Task<ProduktionsStripAntwort>
        Frage(GetProduktionsStrip query, IGetProduktionsStrip getProduktionsStrip)
    {
        return await getProduktionsStrip.GetProduktionsStripAsync(query.Von, query.Bis);
    }

    /// <summary>Read-Model → DTO. Internal, damit der DatensatzReader (Galerie-Join) es wiederverwendet.</summary>
    internal static ImagePairAntwort ToAntwort(ImagePairReadModel m) => new(
        Id: m.Id,
        PairKey: m.PairKey,
        ProduziertAm: m.ProduziertAm,
        AufgenommenAm: m.AufgenommenAm,
        IstKomplett: m.IstKomplett,
        UrsprungsPfad: m.UrsprungsPfad,
        Dc0Pfad: m.Dc0Pfad,
        Dc0KiBildKlassifikation: m.Dc0KiBildKlassifikation,
        Dc0MenschBildLabel: m.Dc0MenschBildLabel,
        Dc0KiRegionen: m.Dc0KiRegionen,
        Dc0MenschRegionen: m.Dc0MenschRegionen,
        Dc2Pfad: m.Dc2Pfad,
        Dc2KiBildKlassifikation: m.Dc2KiBildKlassifikation,
        Dc2MenschBildLabel: m.Dc2MenschBildLabel,
        Dc2KiRegionen: m.Dc2KiRegionen,
        Dc2MenschRegionen: m.Dc2MenschRegionen,
        KiBildpaarKlassifikation: m.KiBildpaarKlassifikation,
        MenschBildpaarLabel: m.MenschBildpaarLabel,
        PhysischesProduktLabel: m.PhysischesProduktLabel,
        IstInspiziert: m.IstInspiziert);
}
