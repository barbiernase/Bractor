using Abstractions;
using Core;
using Domain.ImagePair;
using Domain.Projections;
using Marten;
using Domain.Projections.ImagePair;


namespace Domain.Infrastructure.ImagePair;

/// <summary>
/// Store (Klasse 1) der ImagePair-Projektion — implementiert das Bündel <see cref="IImagePairStore"/> (Schreiben hier,
/// Lesen im Teil <c>ImagePairStore.Lesen.cs</c>). Exactly-once-Naht in <see cref="MartenStore"/>; hier nur
/// die fachlichen Write-Effekte. SCOPED registriert (eine Instanz je Fähigkeits-Bereich, unter jeder Fähigkeit).
/// </summary>
public sealed partial class ImagePairStore : MartenStore, IImagePairStore
{
    public ImagePairStore(IDocumentStore store) : base(store, typeof(ImagePairReadModel)) { }

    public Task UpsertAsync(ImagePairReadModel model) => EnqueueStore(model.Id.Wert, model);

    public Task SetBildVerfuegbarAsync(
        Id<Domain.ImagePair.ImagePair> id, BildVersion version, BildMeta meta, string pfad, DateTimeOffset aktualisierung)
        => EnqueueTransform<ImagePairReadModel>(id.Wert, existing => version switch
        {
            BildVersion.Dc0 => existing with
            {
                Dc0Pfad = pfad, Dc0OriginalDateiname = meta.OriginalDateiname,
                Dc0BreitePixel = meta.BreitePixel, Dc0HoehePixel = meta.HoehePixel,
                LetzteAktualisierung = aktualisierung
            },
            BildVersion.Dc2 => existing with
            {
                Dc2Pfad = pfad, Dc2OriginalDateiname = meta.OriginalDateiname,
                Dc2BreitePixel = meta.BreitePixel, Dc2HoehePixel = meta.HoehePixel,
                LetzteAktualisierung = aktualisierung
            },
            _ => existing
        });

    public Task SetKomplettAsync(Id<Domain.ImagePair.ImagePair> id, DateTimeOffset aktualisierung)
        => EnqueueTransform<ImagePairReadModel>(id.Wert, existing => existing with { IstKomplett = true, LetzteAktualisierung = aktualisierung });

    public Task SetKiEinzelbildKlassifikationAsync(
        Id<Domain.ImagePair.ImagePair> id, BildVersion version, Klassifikation bildLabel,
        IReadOnlyList<Klassifikation> regionLabels, DateTimeOffset aktualisierung)
        => EnqueueTransform<ImagePairReadModel>(id.Wert, existing =>
        {
            var kiRegionen = regionLabels.Select(r => (Klassifikation?)r).ToArray();
            return version switch
            {
                BildVersion.Dc0 => existing with
                {
                    Dc0KiBildKlassifikation = bildLabel, Dc0KiRegionen = kiRegionen,
                    HatKiKlassifikation = true, LetzteAktualisierung = aktualisierung
                },
                BildVersion.Dc2 => existing with
                {
                    Dc2KiBildKlassifikation = bildLabel, Dc2KiRegionen = kiRegionen,
                    HatKiKlassifikation = true, LetzteAktualisierung = aktualisierung
                },
                _ => existing
            };
        });

    public Task SetKiBildpaarKlassifikationAsync(Id<Domain.ImagePair.ImagePair> id, Klassifikation label, DateTimeOffset aktualisierung)
        => EnqueueTransform<ImagePairReadModel>(id.Wert, existing => existing with { KiBildpaarKlassifikation = label, LetzteAktualisierung = aktualisierung });

    public Task SetMenschRegionLabelAsync(
        Id<Domain.ImagePair.ImagePair> id, BildVersion version, int regionIndex, Klassifikation label, DateTimeOffset aktualisierung)
        => EnqueueTransform<ImagePairReadModel>(id.Wert, existing => version switch
        {
            BildVersion.Dc0 => existing with
            {
                Dc0MenschRegionen = UpdateRegionArray(existing.Dc0MenschRegionen, regionIndex, label),
                HatMenschLabel = true, LetzteAktualisierung = aktualisierung
            },
            BildVersion.Dc2 => existing with
            {
                Dc2MenschRegionen = UpdateRegionArray(existing.Dc2MenschRegionen, regionIndex, label),
                HatMenschLabel = true, LetzteAktualisierung = aktualisierung
            },
            _ => existing
        });

    public Task SetMenschEinzelbildLabelAsync(
        Id<Domain.ImagePair.ImagePair> id, BildVersion version, Klassifikation label, DateTimeOffset aktualisierung)
        => EnqueueTransform<ImagePairReadModel>(id.Wert, existing => version switch
        {
            BildVersion.Dc0 => existing with { Dc0MenschBildLabel = label, HatMenschLabel = true, LetzteAktualisierung = aktualisierung },
            BildVersion.Dc2 => existing with { Dc2MenschBildLabel = label, HatMenschLabel = true, LetzteAktualisierung = aktualisierung },
            _ => existing
        });

    public Task SetMenschBildpaarLabelAsync(Id<Domain.ImagePair.ImagePair> id, Klassifikation label, DateTimeOffset aktualisierung)
        => EnqueueTransform<ImagePairReadModel>(id.Wert, existing => existing with { MenschBildpaarLabel = label, HatMenschLabel = true, LetzteAktualisierung = aktualisierung });

    public Task SetPhysischesProduktLabelAsync(Id<Domain.ImagePair.ImagePair> id, Klassifikation label, DateTimeOffset aktualisierung)
        => EnqueueTransform<ImagePairReadModel>(id.Wert, existing => existing with { PhysischesProduktLabel = label, HatProduktLabel = true, LetzteAktualisierung = aktualisierung });

    public Task SetInspiziertAsync(Id<Domain.ImagePair.ImagePair> id, DateTimeOffset aktualisierung)
        => EnqueueTransform<ImagePairReadModel>(id.Wert, existing => existing with { IstInspiziert = true, LetzteAktualisierung = aktualisierung });

    private static IReadOnlyList<Klassifikation?> UpdateRegionArray(
        IReadOnlyList<Klassifikation?> regionen, int index, Klassifikation label)
    {
        var arr = regionen.ToArray();
        arr[index] = label;
        return arr;
    }
}
