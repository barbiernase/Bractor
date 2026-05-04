namespace Domain.Projections;

/// <summary>
/// Write-Zugriffsmuster für die Historie-Projektion.
///
/// Append-only: Einträge werden nur hinzugefügt, nie geändert oder gelöscht.
/// Wird von ImagePairHistorieProjection (ISubscriber) verwendet.
/// </summary>
public interface IImagePairHistorieWriteStore
{
    /// <summary>
    /// Fügt einen einzelnen HistorieEintrag an die Timeline eines ImagePairs an.
    /// Erstellt das Dokument falls es noch nicht existiert.
    /// </summary>
    Task AppendEintragAsync(Guid pairId, HistorieEintrag eintrag);
}

/// <summary>
/// Read-Zugriffsmuster für die Historie-Projektion.
///
/// Wird vom ImagePairHistorieReader verwendet.
/// </summary>
public interface IImagePairHistorieReadStore
{
    /// <summary>
    /// Lädt die komplette Historie eines ImagePairs.
    /// Gibt null zurück wenn das Paar nicht existiert.
    /// </summary>
    Task<ImagePairHistorieReadModel?> GetByPairIdAsync(Guid pairId);
}