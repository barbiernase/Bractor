using Client.Infrastructure.Abstractions;
using Domain.ImagePair;

namespace Domain.Client.ImagePair;

/// <summary>
/// Berechnet Statistiken wenn sich Events ändern.
/// IEnumerable&lt;T&gt; Handle → sync Handler im WiringGenerator.
///
/// Wird NACH dem Store aufgerufen (Subscription-Reihenfolge),
/// sieht also immer den bereits aktualisierten State.
/// </summary>
public partial class ImagePairStatistikHandler
{
    private readonly ImagePairStore _store;

    public ImagePairStatistikHandler(ImagePairStore store)
    {
        _store = store;
    }

    // ── Lifecycle-Events ──

    IEnumerable<ImagePairStatistikBerechnet> Handle(
        ImagePairErstellt evt, MessageContext ctx) => Berechne();

    IEnumerable<ImagePairStatistikBerechnet> Handle(
        ImagePairKomplett evt, MessageContext ctx) => Berechne();

    // ── KI-Klassifikation ──

    IEnumerable<ImagePairStatistikBerechnet> Handle(
        EinzelBildDurchKiKlassifiziert evt, MessageContext ctx) => Berechne();

    IEnumerable<ImagePairStatistikBerechnet> Handle(
        BildPaarDurchKiKlassifiziert evt, MessageContext ctx) => Berechne();

    // ── Mensch-Labels ──

    IEnumerable<ImagePairStatistikBerechnet> Handle(
        BildRegionGelabelt evt, MessageContext ctx) => Berechne();

    IEnumerable<ImagePairStatistikBerechnet> Handle(
        EinzelBildGelabelt evt, MessageContext ctx) => Berechne();

    IEnumerable<ImagePairStatistikBerechnet> Handle(
        BildPaarGelabelt evt, MessageContext ctx) => Berechne();

    IEnumerable<ImagePairStatistikBerechnet> Handle(
        PhysischesProduktGelabelt evt, MessageContext ctx) => Berechne();

    // ── Berechnung ──

    private IEnumerable<ImagePairStatistikBerechnet> Berechne()
    {
        var alle = _store.Items;

        yield return new ImagePairStatistikBerechnet(
            AnzahlGesamt: alle.Count,
            AnzahlKomplett: alle.Count(e => e.IstKomplett),
            AnzahlMitKiKlassifikation: alle.Count(e => e.HatKiKlassifikation),
            AnzahlOhneKlassifikation: alle.Count(e => !e.HatKiKlassifikation),
            AnzahlAnomalie: alle.Count(e =>
                e.Dc0KiBildKlassifikation == Klassifikation.Anomalie ||
                e.Dc2KiBildKlassifikation == Klassifikation.Anomalie ||
                e.KiBildpaarKlassifikation == Klassifikation.Anomalie));
    }
}