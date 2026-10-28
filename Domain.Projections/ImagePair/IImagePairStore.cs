using Abstractions;
using Domain.ImagePair;
using ImagePairAgg = Domain.ImagePair.ImagePair;

namespace Domain.Projections.ImagePair;

// ═══════════════════════════════════════════════════════════
// FÄHIGKEITEN — je Store-Funktion ein Interface (genau eine Funktion, CQRS051).
// Ein Handle nimmt als Parameter genau die Fähigkeiten, die er benutzen darf.
// ═══════════════════════════════════════════════════════════

// ── Schreiben (ImagePairProjektion) ──
public interface IUpsertImagePair : IWriteStore { Task UpsertAsync(ImagePairReadModel model); }

public interface ISetBildVerfuegbar : IWriteStore
{
    Task SetBildVerfuegbarAsync(
        Id<ImagePairAgg> id, BildVersion version, BildMeta meta, string pfad,
        DateTimeOffset aktualisierung);
}

public interface ISetKomplett : IWriteStore { Task SetKomplettAsync(Id<ImagePairAgg> id, DateTimeOffset aktualisierung); }

public interface ISetKiEinzelbildKlassifikation : IWriteStore
{
    Task SetKiEinzelbildKlassifikationAsync(
        Id<ImagePairAgg> id, BildVersion version,
        Klassifikation bildLabel, IReadOnlyList<Klassifikation> regionLabels,
        DateTimeOffset aktualisierung);
}

public interface ISetKiBildpaarKlassifikation : IWriteStore
{
    Task SetKiBildpaarKlassifikationAsync(
        Id<ImagePairAgg> id, Klassifikation label, DateTimeOffset aktualisierung);
}

public interface ISetMenschRegionLabel : IWriteStore
{
    Task SetMenschRegionLabelAsync(
        Id<ImagePairAgg> id, BildVersion version, int regionIndex,
        Klassifikation label, DateTimeOffset aktualisierung);
}

public interface ISetMenschEinzelbildLabel : IWriteStore
{
    Task SetMenschEinzelbildLabelAsync(
        Id<ImagePairAgg> id, BildVersion version,
        Klassifikation label, DateTimeOffset aktualisierung);
}

public interface ISetMenschBildpaarLabel : IWriteStore
{
    Task SetMenschBildpaarLabelAsync(
        Id<ImagePairAgg> id, Klassifikation label, DateTimeOffset aktualisierung);
}

public interface ISetPhysischesProduktLabel : IWriteStore
{
    Task SetPhysischesProduktLabelAsync(
        Id<ImagePairAgg> id, Klassifikation label, DateTimeOffset aktualisierung);
}

public interface ISetInspiziert : IWriteStore { Task SetInspiziertAsync(Id<ImagePairAgg> id, DateTimeOffset aktualisierung); }

// ── Lesen (ImagePairReader, DatensatzReader, DatensatzResolverPipeline) ──
public interface IFindImagePair : IReadStore { Task<ImagePairReadModel?> FindByIdAsync(Id<ImagePairAgg> id); }

/// <summary>Mehrere Read-Models per Id laden (Batch, für die Datensatz-gescopte Galerie).</summary>
public interface ILadeVieleImagePairs : IReadStore { Task<IReadOnlyList<ImagePairReadModel>> LadeVieleAsync(IReadOnlyList<Id<ImagePairAgg>> ids); }

public interface ISearchImagePairs : IReadStore
{
    Task<(IReadOnlyList<ImagePairReadModel> Items, int GesamtAnzahl)> SearchAsync(ImagePairFilter filter);
}

public interface IGetImagePairStatistik : IReadStore { Task<ImagePairStatistik> GetStatistikAsync(); }

public interface IGetUnklassifizierte : IReadStore { Task<IReadOnlyList<ImagePairReadModel>> GetUnklassifizierteAsync(int maxAnzahl = 20); }

public interface IGetVerlauf : IReadStore
{
    Task<ProduktionsVerlaufAntwort> GetVerlaufAsync(
        DateTimeOffset von, DateTimeOffset bis, int bucketMinuten);
}

public interface IGetProduktionsTage : IReadStore { Task<ProduktionsTageAntwort> GetProduktionsTageAsync(); }

public interface IGetProduktionsStrip : IReadStore
{
    Task<ProduktionsStripAntwort> GetProduktionsStripAsync(
        DateTimeOffset von, DateTimeOffset bis);
}

/// <summary>
/// Der ImagePair-Store — das Bündel seiner Fähigkeiten und die Transaktionsgrenze (eine Klasse
/// implementiert es; Schreiben co-committet mit der Marke, Lesen über eigene Query-Sessions).
/// </summary>
public interface IImagePairStore : IStore,
    IUpsertImagePair, ISetBildVerfuegbar, ISetKomplett, ISetKiEinzelbildKlassifikation,
    ISetKiBildpaarKlassifikation, ISetMenschRegionLabel, ISetMenschEinzelbildLabel,
    ISetMenschBildpaarLabel, ISetPhysischesProduktLabel, ISetInspiziert,
    IFindImagePair, ILadeVieleImagePairs, ISearchImagePairs, IGetImagePairStatistik,
    IGetUnklassifizierte, IGetVerlauf, IGetProduktionsTage, IGetProduktionsStrip
{ }
