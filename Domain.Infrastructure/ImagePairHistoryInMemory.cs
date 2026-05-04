using System.Collections.Concurrent;
using Domain.Projections;

namespace Domain.Infrastructure;

/// <summary>
/// In-Memory Implementierung für die Historie-Projektion.
///
/// Für Tests und lokale Entwicklung ohne Postgres.
/// Thread-safe via ConcurrentDictionary + lock auf der Liste.
/// </summary>
public class ImagePairHistorieStoreInMemory
    : IImagePairHistorieWriteStore, IImagePairHistorieReadStore
{
    private readonly ConcurrentDictionary<Guid, ImagePairHistorieReadModel> _data = new();

    public Task AppendEintragAsync(Guid pairId, HistorieEintrag eintrag)
    {
        _data.AddOrUpdate(
            pairId,
            // Neues Dokument
            _ => new ImagePairHistorieReadModel
            {
                Id = pairId,
                Eintraege = new List<HistorieEintrag> { eintrag }
            },
            // Bestehend → Eintrag anhängen
            (_, existing) =>
            {
                var neueEintraege = new List<HistorieEintrag>(existing.Eintraege) { eintrag };
                return existing with { Eintraege = neueEintraege };
            });

        return Task.CompletedTask;
    }

    public Task<ImagePairHistorieReadModel?> GetByPairIdAsync(Guid pairId)
    {
        _data.TryGetValue(pairId, out var model);
        return Task.FromResult(model);
    }
}