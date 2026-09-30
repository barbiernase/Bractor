using Abstractions;
using Domain.ImagePair;

namespace Domain.Projections;

// ═══════════════════════════════════════════════════════════
// FÄHIGKEITEN — je Store-Funktion ein Interface (genau eine Funktion, CQRS051).
// Ein Handle nimmt als Parameter genau die Fähigkeiten, die er benutzen darf.
// ═══════════════════════════════════════════════════════════

// ── Schreiben (ImagePairProjection) ──
public interface IUpsertImagePair : IWriteStore { Task UpsertAsync(ImagePairReadModel model); }

public interface ISetBildVerfuegbar : IWriteStore
{
    Task SetBildVerfuegbarAsync(
        Guid id, BildVersion version, BildMeta meta, string pfad,
        DateTimeOffset aktualisierung);
}

public interface ISetKomplett : IWriteStore { Task SetKomplettAsync(Guid id, DateTimeOffset aktualisierung); }

public interface ISetKiEinzelbildKlassifikation : IWriteStore
{
    Task SetKiEinzelbildKlassifikationAsync(
        Guid id, BildVersion version,
        Klassifikation bildLabel, IReadOnlyList<Klassifikation> regionLabels,
        DateTimeOffset aktualisierung);
}

public interface ISetKiBildpaarKlassifikation : IWriteStore
{
    Task SetKiBildpaarKlassifikationAsync(
        Guid id, Klassifikation label, DateTimeOffset aktualisierung);
}

public interface ISetMenschRegionLabel : IWriteStore
{
    Task SetMenschRegionLabelAsync(
        Guid id, BildVersion version, int regionIndex,
        Klassifikation label, DateTimeOffset aktualisierung);
}

public interface ISetMenschEinzelbildLabel : IWriteStore
{
    Task SetMenschEinzelbildLabelAsync(
        Guid id, BildVersion version,
        Klassifikation label, DateTimeOffset aktualisierung);
}

public interface ISetMenschBildpaarLabel : IWriteStore
{
    Task SetMenschBildpaarLabelAsync(
        Guid id, Klassifikation label, DateTimeOffset aktualisierung);
}

public interface ISetPhysischesProduktLabel : IWriteStore
{
    Task SetPhysischesProduktLabelAsync(
        Guid id, Klassifikation label, DateTimeOffset aktualisierung);
}

public interface ISetInspiziert : IWriteStore { Task SetInspiziertAsync(Guid id, DateTimeOffset aktualisierung); }

// ── Lesen (ImagePairReader, DatensatzReader, DatensatzResolverPipeline) ──
public interface IFindImagePair : IReadStore { Task<ImagePairReadModel?> FindByIdAsync(Guid id); }

/// <summary>Mehrere Read-Models per Id laden (Batch, für die Datensatz-gescopte Galerie).</summary>
public interface ILadeVieleImagePairs : IReadStore { Task<IReadOnlyList<ImagePairReadModel>> LadeVieleAsync(IReadOnlyList<Guid> ids); }

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

/// <summary>
/// Statistik-Daten — internes Transfer-Objekt zwischen Store und Reader.
/// </summary>
public record ImagePairStatistik(
    int Gesamt,
    int Komplett,
    int MitKiKlassifikation,
    int MitMenschLabel,
    int MitProduktLabel,
    int OhneKlassifikation,
    int AnzahlAnomalienKi,
    int AnzahlAnomalienMensch
);