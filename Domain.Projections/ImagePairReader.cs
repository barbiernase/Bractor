using Abstractions;
using Domain.ImagePair;

namespace Domain.Projections;

[ProjectionReader(TrackDeps = true)]
public partial class ImagePairReader : IReader<ImagePairProjection>
{
    private readonly IImagePairReadStore _store;

    public ImagePairReader(IImagePairReadStore store)
    {
        _store = store;
    }

    public async Task<OneOf<ImagePairAntwort, ImagePairNichtGefundenAntwort>>
        Handle(GetImagePair query, IMessageEnvelope envelope, ReadContext ctx)
    {
        var model = await _store.FindByIdAsync(query.PairId);

        if (model != null)
        {
            ctx.Track(query.PairId.ToString());
            return ToAntwort(model);
        }

        return new ImagePairNichtGefundenAntwort(query.PairId);
    }

    public async Task<ImagePairSuchergebnis>
        Handle(SucheImagePairs query, IMessageEnvelope envelope, ReadContext ctx)
    {
        var (items, gesamtAnzahl) = await _store.SearchAsync(query.Filter);

        var antworten = items.Select(m =>
        {
            ctx.Track(m.Id.ToString());
            return ToAntwort(m);
        }).ToList();

        return new ImagePairSuchergebnis(
            antworten, gesamtAnzahl,
            query.Filter.Seite, query.Filter.SeitenGroesse);
    }

    public async Task<ImagePairStatistikAntwort>
        Handle(GetImagePairStatistik query, IMessageEnvelope envelope, ReadContext ctx)
    {
        var s = await _store.GetStatistikAsync();

        return new ImagePairStatistikAntwort(
            s.Gesamt, s.Komplett,
            s.MitKiKlassifikation, s.MitMenschLabel,
            s.MitProduktLabel, s.OhneKlassifikation,
            s.AnzahlAnomalienKi, s.AnzahlAnomalienMensch);
    }

    public async Task<ImagePairArbeitsliste>
        Handle(GetUnklassifizierteImagePairs query, IMessageEnvelope envelope, ReadContext ctx)
    {
        var models = await _store.GetUnklassifizierteAsync(query.MaxAnzahl);

        var antworten = models.Select(m =>
        {
            ctx.Track(m.Id.ToString());
            return ToAntwort(m);
        }).ToList();

        return new ImagePairArbeitsliste(antworten);
    }

    public async Task<ProduktionsVerlaufAntwort>
        Handle(GetProduktionsVerlauf query, IMessageEnvelope envelope, ReadContext ctx)
    {
        return await _store.GetVerlaufAsync(
            query.Von, query.Bis, query.BucketMinuten);
    }

    public async Task<ProduktionsTageAntwort>
        Handle(GetProduktionsTage query, IMessageEnvelope envelope, ReadContext ctx)
    {
        return await _store.GetProduktionsTageAsync();
    }

    public async Task<ProduktionsStripAntwort>
        Handle(GetProduktionsStrip query, IMessageEnvelope envelope, ReadContext ctx)
    {
        return await _store.GetProduktionsStripAsync(query.Von, query.Bis);
    }

    private static ImagePairAntwort ToAntwort(ImagePairReadModel m) => new(
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