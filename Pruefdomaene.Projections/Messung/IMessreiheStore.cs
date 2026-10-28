using Abstractions;
using Pruefdomaene.Messung;

namespace Pruefdomaene.Projections.Messung;

// FÄHIGKEITEN des Messreihen-Stores (Klasse 2, wiederholungsfest) — je Funktion ein Interface (CQRS051). Eine Schreib-Fähigkeit eines Stores der
// Klasse 2 nimmt die Herkunft (R14, CQRS109): ihre Wirkung trägt die Event-Identität im Schlüssel bzw. schreibt bedingt nach ihr.

/// <summary>Einen Messpunkt ablegen — einer je Event, unter seiner Identität; dazu den letzten Wert der Stelle, nur wenn er neuer ist.</summary>
public interface ISchreibeMesspunkt : IWriteStore { Task SchreibeAsync(Id<Messstelle> stelle, double wert, Herkunft h); }

/// <summary>Die Messreihe einer Stelle.</summary>
public interface IHoleMessreihe : IReadStore { Task<Messreihe> HoleAsync(Id<Messstelle> stelle); }

/// <summary>Der Messreihen-Store — Bündel seiner Fähigkeiten.</summary>
public interface IMessreiheStore : IStore, ISchreibeMesspunkt, IHoleMessreihe { }
