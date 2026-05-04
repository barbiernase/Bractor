using Domain.Projections;
using Marten;
using Microsoft.Extensions.Logging;

namespace Domain.Infrastructure;

/// <summary>
/// PostgreSQL-Implementierung für die Historie-Projektion.
///
/// Nutzt Marten als Document Store — das ReadModel wird als
/// JSONB-Dokument mit einer wachsenden Einträge-Liste gespeichert.
///
/// Append-Semantik: Load → Add → Store. Kein Patch-API nötig,
/// weil Marten das gesamte Dokument sowieso als JSONB schreibt.
/// Bei hohem Durchsatz könnte man auf Marten's Patch-API wechseln,
/// aber für die erwartete Event-Rate (1-12 Events pro Paar) ist
/// Load+Store absolut ausreichend.
/// </summary>
public class ImagePairHistorieStorePostgres
    : IImagePairHistorieWriteStore, IImagePairHistorieReadStore
{
    private readonly IDocumentStore _store;
    private readonly ILogger<ImagePairHistorieStorePostgres> _logger;

    public ImagePairHistorieStorePostgres(
        IDocumentStore store,
        ILogger<ImagePairHistorieStorePostgres> logger)
    {
        _store = store;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════
    // Write-Seite
    // ═══════════════════════════════════════════════════════════

    public async Task AppendEintragAsync(Guid pairId, HistorieEintrag eintrag)
    {
        await using var session = _store.LightweightSession();

        var existing = await session.LoadAsync<ImagePairHistorieReadModel>(pairId);

        if (existing != null)
        {
            // Bestehende Liste erweitern
            var neueEintraege = new List<HistorieEintrag>(existing.Eintraege) { eintrag };
            session.Store(existing with { Eintraege = neueEintraege });
        }
        else
        {
            // Neues Dokument anlegen
            session.Store(new ImagePairHistorieReadModel
            {
                Id = pairId,
                Eintraege = new List<HistorieEintrag> { eintrag }
            });
        }

        await session.SaveChangesAsync();
    }

    // ═══════════════════════════════════════════════════════════
    // Read-Seite
    // ═══════════════════════════════════════════════════════════

    public async Task<ImagePairHistorieReadModel?> GetByPairIdAsync(Guid pairId)
    {
        await using var session = _store.QuerySession();
        return await session.LoadAsync<ImagePairHistorieReadModel>(pairId);
    }
}