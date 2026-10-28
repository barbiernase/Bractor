using Abstractions;
using Domain.ImagePair;

namespace Domain.Projections.ImagePair;

public sealed class ImagePairProjektion : IProjektion
{
    // =========================================================================
    // LIFECYCLE
    // =========================================================================

    public Task Auf(ImagePairErstellt e, Herkunft h, IUpsertImagePair upsertImagePair)
        => upsertImagePair.UpsertAsync(new ImagePairReadModel
        {
            Id = h.Id<Domain.ImagePair.ImagePair>(),
            PairKey = e.PairKey,
            ProduziertAm = e.ProduziertAm,
            AufgenommenAm = e.AufgenommenAm,
            UrsprungsPfad = e.UrsprungsPfad,
            LetzteAktualisierung = h.Zeit
        });

    public Task Auf(BildVerfuegbar e, Herkunft h, ISetBildVerfuegbar setBildVerfuegbar)
        => setBildVerfuegbar.SetBildVerfuegbarAsync(
            h.Id<Domain.ImagePair.ImagePair>(), e.Version, e.Meta, e.Pfad,
            h.Zeit);

    public Task Auf(ImagePairKomplett e, Herkunft h, ISetKomplett setKomplett)
        => setKomplett.SetKomplettAsync(h.Id<Domain.ImagePair.ImagePair>(), h.Zeit);

    // =========================================================================
    // STRANG 1 — KI-Klassifikation
    // =========================================================================

    public Task Auf(EinzelBildDurchKiKlassifiziert e, Herkunft h, ISetKiEinzelbildKlassifikation setKiEinzelbildKlassifikation)
        => setKiEinzelbildKlassifikation.SetKiEinzelbildKlassifikationAsync(
            h.Id<Domain.ImagePair.ImagePair>(), e.Version, e.BildLabel, e.RegionLabels,
            h.Zeit);

    public Task Auf(BildPaarDurchKiKlassifiziert e, Herkunft h, ISetKiBildpaarKlassifikation setKiBildpaarKlassifikation)
        => setKiBildpaarKlassifikation.SetKiBildpaarKlassifikationAsync(
            h.Id<Domain.ImagePair.ImagePair>(), e.Label, h.Zeit);

    // =========================================================================
    // STRANG 2 — Mensch labelt Kamerabilder
    // =========================================================================

    public Task Auf(BildRegionGelabelt e, Herkunft h, ISetMenschRegionLabel setMenschRegionLabel)
        => setMenschRegionLabel.SetMenschRegionLabelAsync(
            h.Id<Domain.ImagePair.ImagePair>(), e.Version, e.RegionIndex, e.Label,
            h.Zeit);

    public Task Auf(EinzelBildGelabelt e, Herkunft h, ISetMenschEinzelbildLabel setMenschEinzelbildLabel)
        => setMenschEinzelbildLabel.SetMenschEinzelbildLabelAsync(
            h.Id<Domain.ImagePair.ImagePair>(), e.Version, e.Label, h.Zeit);

    public Task Auf(BildPaarGelabelt e, Herkunft h, ISetMenschBildpaarLabel setMenschBildpaarLabel)
        => setMenschBildpaarLabel.SetMenschBildpaarLabelAsync(
            h.Id<Domain.ImagePair.ImagePair>(), e.Label, h.Zeit);

    // =========================================================================
    // STRANG 3 — Mensch labelt physisches Produkt
    // =========================================================================

    public Task Auf(PhysischesProduktGelabelt e, Herkunft h, ISetPhysischesProduktLabel setPhysischesProduktLabel)
        => setPhysischesProduktLabel.SetPhysischesProduktLabelAsync(
            h.Id<Domain.ImagePair.ImagePair>(), e.Label, h.Zeit);

    // =========================================================================
    // INSPEKTION
    // =========================================================================

    public Task Auf(ImagePairInspiziert e, Herkunft h, ISetInspiziert setInspiziert)
        => setInspiziert.SetInspiziertAsync(
            h.Id<Domain.ImagePair.ImagePair>(), h.Zeit);
}
