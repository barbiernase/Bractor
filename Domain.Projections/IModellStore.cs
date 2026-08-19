namespace Domain.Projections;

/// <summary>Write-Zugriffsmuster der Modell-Projektion — je Event ein atomarer Effekt.</summary>
public interface IModellWriteStore
{
    /// <summary>Registriert/aktualisiert ein Modell-Dokument.</summary>
    Task UpsertAsync(ModellReadModel model);

    /// <summary>Setzt den Singleton-Zeiger auf das aktive Modell (last-writer-wins).</summary>
    Task SetzeAktivAsync(Guid modellId, string? name, string? pfad, DateTimeOffset aktiviertAm);

    /// <summary>Markiert ein Modell als archiviert.</summary>
    Task ArchiviereAsync(Guid modellId, DateTimeOffset aktualisierung);
}

/// <summary>Read-Zugriffsmuster der Modell-Projektion — Listen + aktiver Zeiger.</summary>
public interface IModellReadStore
{
    Task<ModellReadModel?> FindByIdAsync(Guid id);

    /// <summary>Alle Modelle, neueste zuerst.</summary>
    Task<IReadOnlyList<ModellReadModel>> GetAlleAsync();

    /// <summary>Der aktive-Modell-Singleton (oder null, wenn noch keiner gesetzt).</summary>
    Task<AktivesModellReadModel?> GetAktivesAsync();
}
