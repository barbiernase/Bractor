using Domain.Datensatz;

namespace Domain.Projections;

/// <summary>
/// Write-Zugriffsmuster der Datensatz-Projektion — je Event ein atomarer Effekt.
/// Wird von <c>DatensatzProjektion</c> verwendet.
/// </summary>
public interface IDatensatzWriteStore
{
    /// <summary>Legt den Datensatz an (Entwurf, leerer Korb).</summary>
    Task UpsertAsync(DatensatzReadModel model);

    /// <summary>Nimmt eine aufgelöste Range auf (Union, dedupliziert) + Provenienz.</summary>
    Task NimmRangeAufAsync(
        Guid id, IReadOnlyList<Guid> imagePairIds, RangeHerkunft herkunft, DateTimeOffset aktualisierung);

    /// <summary>Manuelles Delta: ein Paar aufnehmen.</summary>
    Task NimmPaarAufAsync(Guid id, Guid imagePairId, DateTimeOffset aktualisierung);

    /// <summary>Manuelles Delta: ein Paar entfernen.</summary>
    Task EntfernePaarAsync(Guid id, Guid imagePairId, DateTimeOffset aktualisierung);

    /// <summary>Split-Override setzen.</summary>
    Task SetzeSplitAsync(Guid id, SplitKonfig split, DateTimeOffset aktualisierung);

    /// <summary>
    /// Einfrieren festschreiben: Status/Version am Datensatz setzen UND je Mitglied
    /// eine <see cref="DatensatzSampleReadModel"/>-Zeile schreiben (der immutable Snapshot).
    /// </summary>
    Task FriereEinAsync(
        Guid id, int version, IReadOnlyList<DatensatzMitglied> mitglieder, DateTimeOffset aktualisierung);
}

/// <summary>
/// Read-Zugriffsmuster der Datensatz-Projektion — Queries mit Paginierung.
/// Wird vom <c>DatensatzReader</c> (M4) und von der GUI verwendet.
/// </summary>
public interface IDatensatzReadStore
{
    Task<DatensatzReadModel?> FindByIdAsync(Guid id);

    /// <summary>Alle Datensätze (Sidebar-Liste), neueste zuerst.</summary>
    Task<IReadOnlyList<DatensatzReadModel>> GetAlleAsync();

    /// <summary>
    /// Die eingefrorenen Samples einer bestimmten Datensatz-Version, paginiert.
    /// Liest den immutablen Snapshot (Reproduzierbarkeit) — stabil sortiert nach ImagePairId.
    /// </summary>
    Task<(IReadOnlyList<DatensatzSampleReadModel> Items, int GesamtAnzahl)> HoleSamplesAsync(
        Guid datensatzId, int version, int seite, int seitenGroesse);
}
