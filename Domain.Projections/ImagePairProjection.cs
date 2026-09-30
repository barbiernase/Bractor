using Abstractions;
using Core;
using Domain.ImagePair;

namespace Domain.Projections;

public partial class ImagePairProjection : ISubscriber, IPullSubscriber
{
    public string SubscriberId => "imagepair-projection";

    // =========================================================================
    // LIFECYCLE
    // =========================================================================

    public async Task Handle(
        ImagePairErstellt evt, IAggregateEnvelope envelope, ProjectionWriter writer, IUpsertImagePair upsertImagePair)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await upsertImagePair.UpsertAsync(new ImagePairReadModel
            {
                Id = envelope.AggregateId,
                PairKey = evt.PairKey,
                ProduziertAm = evt.ProduziertAm,
                AufgenommenAm = evt.AufgenommenAm,
                UrsprungsPfad = evt.UrsprungsPfad,
                LetzteAktualisierung = envelope.CreatedAtUtc
            });
        });
    }

    public async Task Handle(
        BildVerfuegbar evt, IAggregateEnvelope envelope, ProjectionWriter writer, ISetBildVerfuegbar setBildVerfuegbar)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await setBildVerfuegbar.SetBildVerfuegbarAsync(
                envelope.AggregateId, evt.Version, evt.Meta, evt.Pfad,
                envelope.CreatedAtUtc);
        });
    }

    public async Task Handle(
        ImagePairKomplett evt, IAggregateEnvelope envelope, ProjectionWriter writer, ISetKomplett setKomplett)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await setKomplett.SetKomplettAsync(envelope.AggregateId, envelope.CreatedAtUtc);
        });
    }

    // =========================================================================
    // STRANG 1 — KI-Klassifikation
    // =========================================================================

    public async Task Handle(
        EinzelBildDurchKiKlassifiziert evt, IAggregateEnvelope envelope, ProjectionWriter writer, ISetKiEinzelbildKlassifikation setKiEinzelbildKlassifikation)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await setKiEinzelbildKlassifikation.SetKiEinzelbildKlassifikationAsync(
                envelope.AggregateId, evt.Version, evt.BildLabel, evt.RegionLabels,
                envelope.CreatedAtUtc);
        });
    }

    public async Task Handle(
        BildPaarDurchKiKlassifiziert evt, IAggregateEnvelope envelope, ProjectionWriter writer, ISetKiBildpaarKlassifikation setKiBildpaarKlassifikation)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await setKiBildpaarKlassifikation.SetKiBildpaarKlassifikationAsync(
                envelope.AggregateId, evt.Label, envelope.CreatedAtUtc);
        });
    }

    // =========================================================================
    // STRANG 2 — Mensch labelt Kamerabilder
    // =========================================================================

    public async Task Handle(
        BildRegionGelabelt evt, IAggregateEnvelope envelope, ProjectionWriter writer, ISetMenschRegionLabel setMenschRegionLabel)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await setMenschRegionLabel.SetMenschRegionLabelAsync(
                envelope.AggregateId, evt.Version, evt.RegionIndex, evt.Label,
                envelope.CreatedAtUtc);
        });
    }

    public async Task Handle(
        EinzelBildGelabelt evt, IAggregateEnvelope envelope, ProjectionWriter writer, ISetMenschEinzelbildLabel setMenschEinzelbildLabel)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await setMenschEinzelbildLabel.SetMenschEinzelbildLabelAsync(
                envelope.AggregateId, evt.Version, evt.Label, envelope.CreatedAtUtc);
        });
    }

    public async Task Handle(
        BildPaarGelabelt evt, IAggregateEnvelope envelope, ProjectionWriter writer, ISetMenschBildpaarLabel setMenschBildpaarLabel)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await setMenschBildpaarLabel.SetMenschBildpaarLabelAsync(
                envelope.AggregateId, evt.Label, envelope.CreatedAtUtc);
        });
    }

    // =========================================================================
    // STRANG 3 — Mensch labelt physisches Produkt
    // =========================================================================

    public async Task Handle(
        PhysischesProduktGelabelt evt, IAggregateEnvelope envelope, ProjectionWriter writer, ISetPhysischesProduktLabel setPhysischesProduktLabel)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await setPhysischesProduktLabel.SetPhysischesProduktLabelAsync(
                envelope.AggregateId, evt.Label, envelope.CreatedAtUtc);
        });
    }

    // =========================================================================
    // INSPEKTION
    // =========================================================================

    public async Task Handle(
        ImagePairInspiziert evt, IAggregateEnvelope envelope, ProjectionWriter writer, ISetInspiziert setInspiziert)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await setInspiziert.SetInspiziertAsync(
                envelope.AggregateId, envelope.CreatedAtUtc);
        });
    }
}