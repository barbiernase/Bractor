using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Domain.Projections;

/// <summary>
/// Write-Zugriffsmuster für Lagerbestand-ReadModels.
/// Optimiert für Upserts und atomare Updates.
/// Wird vom Writer (LagerbestandProjection) verwendet.
/// </summary>
public interface ILagerbestandWriteStore
{
    Task UpsertAsync(LagerbestandReadModel model);
    Task AddToBestandAsync(Guid id, int anzahl, DateTimeOffset aktualisierung);
    Task SubtractFromBestandAsync(Guid id, int anzahl, DateTimeOffset aktualisierung);
    
    /// <summary>
    /// Liest den aktuellen Bestand — nur für reaktive Logik im Writer
    /// (z.B. Nachbestellungs-Schwelle prüfen nach Warenabgang).
    /// </summary>
    Task<int?> GetBestandAsync(Guid id);
}

/// <summary>
/// Read-Zugriffsmuster für Lagerbestand-ReadModels.
/// Optimiert für Queries mit Caching und materialisierten Views.
/// Wird vom Reader (LagerbestandReader) verwendet.
/// </summary>
public interface ILagerbestandReadStore
{
    Task<LagerbestandReadModel?> FindByIdAsync(Guid id);
    Task<IReadOnlyList<LagerbestandReadModel>> GetAllAsync();
}