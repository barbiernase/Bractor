using Abstractions;

namespace Domain.Projections;

// FÄHIGKEITEN des Modell-Stores — je Funktion ein Interface (CQRS051).

// ── Schreiben (ModellProjektion) ──
/// <summary>Registriert/aktualisiert ein Modell-Dokument.</summary>
public interface IUpsertModell : IWriteStore { Task UpsertAsync(ModellReadModel model); }

/// <summary>Setzt den Singleton-Zeiger auf das aktive Modell (last-writer-wins).</summary>
public interface ISetzeModellAktiv : IWriteStore { Task SetzeAktivAsync(Guid modellId, string? name, string? pfad, DateTimeOffset aktiviertAm); }

/// <summary>Markiert ein Modell als archiviert.</summary>
public interface IArchiviereModell : IWriteStore { Task ArchiviereAsync(Guid modellId, DateTimeOffset aktualisierung); }

// ── Lesen (ModellReader) ──
public interface IFindModell : IReadStore { Task<ModellReadModel?> FindByIdAsync(Guid id); }

/// <summary>Alle Modelle, neueste zuerst.</summary>
public interface IGetAlleModelle : IReadStore { Task<IReadOnlyList<ModellReadModel>> GetAlleAsync(); }

/// <summary>Der aktive-Modell-Singleton (oder null, wenn noch keiner gesetzt).</summary>
public interface IGetAktivesModell : IReadStore { Task<AktivesModellReadModel?> GetAktivesAsync(); }

/// <summary>Der Modell-Store — Bündel seiner Fähigkeiten, Transaktionsgrenze.</summary>
public interface IModellStore : IStore,
    IUpsertModell, ISetzeModellAktiv, IArchiviereModell,
    IFindModell, IGetAlleModelle, IGetAktivesModell
{ }
