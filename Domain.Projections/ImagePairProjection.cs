using Abstractions;
using Core;
using Domain.ImagePair;

namespace Domain.Projections;

public partial class ImagePairProjection : ISubscriber
{
    private readonly IImagePairWriteStore _store;

    public ImagePairProjection(IImagePairWriteStore store)
    {
        _store = store;
    }

    public string SubscriberId => "imagepair-projection";

    // =========================================================================
    // LIFECYCLE
    // =========================================================================

    public async Task Handle(
        ImagePairErstellt evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await _store.UpsertAsync(new ImagePairReadModel
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
        BildVerfuegbar evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await _store.SetBildVerfuegbarAsync(
                envelope.AggregateId, evt.Version, evt.Meta, evt.Pfad,
                envelope.CreatedAtUtc);
        });
    }

    public async Task Handle(
        ImagePairKomplett evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await _store.SetKomplettAsync(envelope.AggregateId, envelope.CreatedAtUtc);
        });
    }

    // =========================================================================
    // STRANG 1 — KI-Klassifikation
    // =========================================================================

    public async Task Handle(
        EinzelBildDurchKiKlassifiziert evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await _store.SetKiEinzelbildKlassifikationAsync(
                envelope.AggregateId, evt.Version, evt.BildLabel, evt.RegionLabels,
                envelope.CreatedAtUtc);
        });
    }

    public async Task Handle(
        BildPaarDurchKiKlassifiziert evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await _store.SetKiBildpaarKlassifikationAsync(
                envelope.AggregateId, evt.Label, envelope.CreatedAtUtc);
        });
    }

    // =========================================================================
    // STRANG 2 — Mensch labelt Kamerabilder
    // =========================================================================

    public async Task Handle(
        BildRegionGelabelt evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await _store.SetMenschRegionLabelAsync(
                envelope.AggregateId, evt.Version, evt.RegionIndex, evt.Label,
                envelope.CreatedAtUtc);
        });
    }

    public async Task Handle(
        EinzelBildGelabelt evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await _store.SetMenschEinzelbildLabelAsync(
                envelope.AggregateId, evt.Version, evt.Label, envelope.CreatedAtUtc);
        });
    }

    public async Task Handle(
        BildPaarGelabelt evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await _store.SetMenschBildpaarLabelAsync(
                envelope.AggregateId, evt.Label, envelope.CreatedAtUtc);
        });
    }

    // =========================================================================
    // STRANG 3 — Mensch labelt physisches Produkt
    // =========================================================================

    public async Task Handle(
        PhysischesProduktGelabelt evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await _store.SetPhysischesProduktLabelAsync(
                envelope.AggregateId, evt.Label, envelope.CreatedAtUtc);
        });
    }

    // =========================================================================
    // INSPEKTION
    // =========================================================================

    public async Task Handle(
        ImagePairInspiziert evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<ImagePair.ImagePair>(envelope.AggregateId);
            await _store.SetInspiziertAsync(
                envelope.AggregateId, envelope.CreatedAtUtc);
        });
    }
}