using Domain.ImagePair;
using Domain.Projections;
using Marten;

namespace Domain.Infrastructure;

/// <summary>
/// Co-Commit-Store der ImagePair-Projektion (Pull-Variante der Write-Seite; die Read-Seite bleibt das
/// Singleton <see cref="ImagePairStorePostgres"/>). Exactly-once-Naht in
/// <see cref="MartenCoCommitStoreBase"/>; hier nur die fachlichen Write-Effekte. TRANSIENT registriert.
/// </summary>
public sealed class ImagePairStore : MartenCoCommitStoreBase, IImagePairWriteStore
{
    public ImagePairStore(IDocumentStore store) : base(store) { }

    public Task UpsertAsync(ImagePairReadModel model) => EnqueueStore(model);

    public Task SetBildVerfuegbarAsync(
        Guid id, BildVersion version, BildMeta meta, string pfad, DateTimeOffset aktualisierung)
        => EnqueueTransform<ImagePairReadModel>(id, existing => version switch
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

    public Task SetKomplettAsync(Guid id, DateTimeOffset aktualisierung)
        => EnqueueTransform<ImagePairReadModel>(id, existing => existing with { IstKomplett = true, LetzteAktualisierung = aktualisierung });

    public Task SetKiEinzelbildKlassifikationAsync(
        Guid id, BildVersion version, Klassifikation bildLabel,
        IReadOnlyList<Klassifikation> regionLabels, DateTimeOffset aktualisierung)
        => EnqueueTransform<ImagePairReadModel>(id, existing =>
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

    public Task SetKiBildpaarKlassifikationAsync(Guid id, Klassifikation label, DateTimeOffset aktualisierung)
        => EnqueueTransform<ImagePairReadModel>(id, existing => existing with { KiBildpaarKlassifikation = label, LetzteAktualisierung = aktualisierung });

    public Task SetMenschRegionLabelAsync(
        Guid id, BildVersion version, int regionIndex, Klassifikation label, DateTimeOffset aktualisierung)
        => EnqueueTransform<ImagePairReadModel>(id, existing => version switch
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
        Guid id, BildVersion version, Klassifikation label, DateTimeOffset aktualisierung)
        => EnqueueTransform<ImagePairReadModel>(id, existing => version switch
        {
            BildVersion.Dc0 => existing with { Dc0MenschBildLabel = label, HatMenschLabel = true, LetzteAktualisierung = aktualisierung },
            BildVersion.Dc2 => existing with { Dc2MenschBildLabel = label, HatMenschLabel = true, LetzteAktualisierung = aktualisierung },
            _ => existing
        });

    public Task SetMenschBildpaarLabelAsync(Guid id, Klassifikation label, DateTimeOffset aktualisierung)
        => EnqueueTransform<ImagePairReadModel>(id, existing => existing with { MenschBildpaarLabel = label, HatMenschLabel = true, LetzteAktualisierung = aktualisierung });

    public Task SetPhysischesProduktLabelAsync(Guid id, Klassifikation label, DateTimeOffset aktualisierung)
        => EnqueueTransform<ImagePairReadModel>(id, existing => existing with { PhysischesProduktLabel = label, HatProduktLabel = true, LetzteAktualisierung = aktualisierung });

    public Task SetInspiziertAsync(Guid id, DateTimeOffset aktualisierung)
        => EnqueueTransform<ImagePairReadModel>(id, existing => existing with { IstInspiziert = true, LetzteAktualisierung = aktualisierung });

    private static IReadOnlyList<Klassifikation?> UpdateRegionArray(
        IReadOnlyList<Klassifikation?> regionen, int index, Klassifikation label)
    {
        var arr = regionen.ToArray();
        arr[index] = label;
        return arr;
    }
}
