using Domain.ImagePair;

namespace Domain.Projections;

/// <summary>
/// Write-Zugriffsmuster — optimiert für atomare Updates pro Event.
/// Wird von der ImagePairProjection verwendet.
/// </summary>
public interface IImagePairWriteStore
{
    Task UpsertAsync(ImagePairReadModel model);

    Task SetBildVerfuegbarAsync(
        Guid id, BildVersion version, BildMeta meta, string pfad,
        DateTimeOffset aktualisierung);

    Task SetKomplettAsync(Guid id, DateTimeOffset aktualisierung);

    Task SetKiEinzelbildKlassifikationAsync(
        Guid id, BildVersion version,
        Klassifikation bildLabel, IReadOnlyList<Klassifikation> regionLabels,
        DateTimeOffset aktualisierung);

    Task SetKiBildpaarKlassifikationAsync(
        Guid id, Klassifikation label, DateTimeOffset aktualisierung);

    Task SetMenschRegionLabelAsync(
        Guid id, BildVersion version, int regionIndex,
        Klassifikation label, DateTimeOffset aktualisierung);

    Task SetMenschEinzelbildLabelAsync(
        Guid id, BildVersion version,
        Klassifikation label, DateTimeOffset aktualisierung);

    Task SetMenschBildpaarLabelAsync(
        Guid id, Klassifikation label, DateTimeOffset aktualisierung);

    Task SetPhysischesProduktLabelAsync(
        Guid id, Klassifikation label, DateTimeOffset aktualisierung);

    Task SetInspiziertAsync(Guid id, DateTimeOffset aktualisierung);
}

/// <summary>
/// Read-Zugriffsmuster — optimiert für Queries mit Filterung.
/// Wird vom ImagePairReader verwendet.
/// </summary>
public interface IImagePairReadStore
{
    Task<ImagePairReadModel?> FindByIdAsync(Guid id);
    Task<(IReadOnlyList<ImagePairReadModel> Items, int GesamtAnzahl)> SearchAsync(ImagePairFilter filter);
    Task<ImagePairStatistik> GetStatistikAsync();
    Task<IReadOnlyList<ImagePairReadModel>> GetUnklassifizierteAsync(int maxAnzahl = 20);

    Task<ProduktionsVerlaufAntwort> GetVerlaufAsync(
        DateTimeOffset von, DateTimeOffset bis, int bucketMinuten);

    Task<ProduktionsTageAntwort> GetProduktionsTageAsync();

    Task<ProduktionsStripAntwort> GetProduktionsStripAsync(
        DateTimeOffset von, DateTimeOffset bis);
}

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