using Abstractions;

namespace Pruefwerk.Katalog.Messung;

/// <summary>
/// Eine LESE-FÄHIGKEIT, die der Katalog deklariert (docs/04 §4.4): mit seinen eigenen Typen; umgesetzt wird sie vom Store der
/// Anwendung, die ihn nutzt (dort liest er aus seinem eigenen Read-Model).
/// </summary>
public interface IHoleReihe : IReadStore { Task<Reihe> HoleAsync(Guid stelle); }
