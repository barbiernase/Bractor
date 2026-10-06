using Abstractions;
using Domain.Datensatz;

namespace Domain.Projections;

// FÄHIGKEITEN des Datensatz-Stores — je Funktion ein Interface (CQRS051).

// ── Schreiben (DatensatzProjektion) — je Event ein atomarer Effekt ──

/// <summary>Legt den Datensatz an (Entwurf, leerer Korb).</summary>
public interface IUpsertDatensatz : IWriteStore { Task UpsertAsync(DatensatzReadModel model); }

/// <summary>Nimmt eine aufgelöste Range auf (Union, dedupliziert) + Provenienz.</summary>
public interface INimmRangeAuf : IWriteStore
{
    Task NimmRangeAufAsync(
        Guid id, IReadOnlyList<Guid> imagePairIds, RangeHerkunft herkunft, DateTimeOffset aktualisierung);
}

/// <summary>Manuelles Delta: ein Paar aufnehmen.</summary>
public interface INimmPaarAuf : IWriteStore { Task NimmPaarAufAsync(Guid id, Guid imagePairId, DateTimeOffset aktualisierung); }

/// <summary>Manuelles Delta: ein Paar entfernen.</summary>
public interface IEntfernePaar : IWriteStore { Task EntfernePaarAsync(Guid id, Guid imagePairId, DateTimeOffset aktualisierung); }

/// <summary>Split-Override setzen.</summary>
public interface ISetzeSplit : IWriteStore { Task SetzeSplitAsync(Guid id, SplitKonfig split, DateTimeOffset aktualisierung); }

/// <summary>
/// Einfrieren festschreiben: Status/Version am Datensatz setzen UND je Mitglied
/// eine <see cref="DatensatzSampleReadModel"/>-Zeile schreiben (der immutable Snapshot).
/// </summary>
public interface IFriereEin : IWriteStore
{
    Task FriereEinAsync(
        Guid id, int version, IReadOnlyList<DatensatzMitglied> mitglieder, DateTimeOffset aktualisierung);
}

// ── Lesen (DatensatzReader, GUI) — Queries mit Paginierung ──

public interface IFindDatensatz : IReadStore { Task<DatensatzReadModel?> FindByIdAsync(Guid id); }

/// <summary>Alle Datensätze (Sidebar-Liste), neueste zuerst.</summary>
public interface IGetAlleDatensaetze : IReadStore { Task<IReadOnlyList<DatensatzReadModel>> GetAlleAsync(); }

/// <summary>
/// Die eingefrorenen Samples einer bestimmten Datensatz-Version, paginiert.
/// Liest den immutablen Snapshot (Reproduzierbarkeit) — stabil sortiert nach ImagePairId.
/// </summary>
public interface IHoleSamples : IReadStore
{
    Task<(IReadOnlyList<DatensatzSampleReadModel> Items, int GesamtAnzahl)> HoleSamplesAsync(
        Guid datensatzId, int version, int seite, int seitenGroesse);
}

/// <summary>
/// Rückwärts-Index: alle Datensätze, in denen das Bildpaar (als Entwurfs-Mitglied) liegt.
/// Liest die <see cref="DatensatzMitgliedschaftZeile"/>n des Paares und joint die Kopf-Daten.
/// </summary>
public interface IHoleDatensaetzeFuerPaar : IReadStore { Task<IReadOnlyList<DatensatzReadModel>> HoleDatensaetzeFuerPaarAsync(Guid imagePairId); }

/// <summary>Der Datensatz-Store — Bündel seiner Fähigkeiten, Transaktionsgrenze.</summary>
public interface IDatensatzStore : IStore,
    IUpsertDatensatz, INimmRangeAuf, INimmPaarAuf, IEntfernePaar, ISetzeSplit, IFriereEin,
    IFindDatensatz, IGetAlleDatensaetze, IHoleSamples, IHoleDatensaetzeFuerPaar
{ }
