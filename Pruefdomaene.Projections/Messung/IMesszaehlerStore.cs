using Abstractions;
using Pruefdomaene.Messung;

namespace Pruefdomaene.Projections.Messung;

// FÄHIGKEITEN des Messzähler-Stores (Klasse 3, nur mindestens einmal) — eine Beobachtung, keine Wahrheit.

/// <summary>Eine Messung zählen — ohne Bedingung; nach einem Abbruch kann sie doppelt zählen.</summary>
public interface IZaehleMessung : IWriteStore { Task ZaehleAsync(Id<Messstelle> stelle); }

/// <summary>Wie oft gezählt wurde (mindestens so oft, wie gemessen).</summary>
public interface ILiesMesszaehler : IReadStore { Task<Messzaehler> LiesAsync(Id<Messstelle> stelle); }

/// <summary>Der Messzähler-Store — Bündel seiner Fähigkeiten.</summary>
public interface IMesszaehlerStore : IStore, IZaehleMessung, ILiesMesszaehler { }
