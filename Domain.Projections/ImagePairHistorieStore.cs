using Abstractions;
namespace Domain.Projections;

// FÄHIGKEITEN des Historie-Stores — je Funktion ein Interface (CQRS051).
// Append-only: Einträge werden nur hinzugefügt, nie geändert oder gelöscht.

/// <summary>
/// Fügt einen einzelnen HistorieEintrag an die Timeline eines ImagePairs an.
/// Erstellt das Dokument falls es noch nicht existiert.
/// </summary>
public interface IAppendHistorieEintrag : IWriteStore { Task AppendEintragAsync(Guid pairId, HistorieEintrag eintrag); }

/// <summary>
/// Lädt die komplette Historie eines ImagePairs.
/// Gibt null zurück wenn das Paar nicht existiert.
/// </summary>
public interface IGetHistorie : IReadStore { Task<ImagePairHistorieReadModel?> GetByPairIdAsync(Guid pairId); }

/// <summary>Der Historie-Store — Bündel seiner Fähigkeiten, Transaktionsgrenze.</summary>
public interface IImagePairHistorieStore : IStore, IAppendHistorieEintrag, IGetHistorie { }
