using Abstractions;
using Pruefdomaene.Abrechnung;

namespace Pruefdomaene.Projections.Abrechnung;

// FÄHIGKEITEN des Rechnungsbild-Stores (Klasse 2, wiederholungsfest). Jede schreibt den Stand eines Bündels je Schlüssel BEDINGT nach der Position
// des auslösenden Events im Log (Herkunft.Stand): Events verschiedener Streams kommen in verschiedenen Partitionen an — so bleibt der jüngste Stand.

/// <summary>Die Rechnung ist gestellt und freigegeben (Bündel über den Stream der Rechnung).</summary>
public interface ISchreibeFreigabe : IWriteStore { Task FreigegebenAsync(Id<Rechnung> rechnung, decimal betrag, Herkunft h); }

/// <summary>Die Rechnung ist gestellt und eine Gutschrift auf sie gebucht (Bündel über das Feld Rechnung der Gutschrift).</summary>
public interface ISchreibeGutschrift : IWriteStore { Task GutgeschriebenAsync(Id<Rechnung> rechnung, decimal gestellt, decimal gutgeschrieben, Herkunft h); }

/// <summary>Die Lage: die neueste Sperre einer Rechnung und der neueste Ausreißer einer Messstelle (globales Bündel).</summary>
public interface ISchreibeLage : IWriteStore { Task LageAsync(string sperrgrund, double ausreisser, Herkunft h); }

/// <summary>Das Bild einer Rechnung.</summary>
public interface IHoleRechnungsbild : IReadStore { Task<Rechnungsbild> HoleAsync(Id<Rechnung> rechnung); }

/// <summary>Die Lage.</summary>
public interface IHoleLage : IReadStore { Task<Lagebild> HoleAsync(); }

/// <summary>Der Rechnungsbild-Store — Bündel seiner Fähigkeiten.</summary>
public interface IRechnungsbildStore : IStore, ISchreibeFreigabe, ISchreibeGutschrift, ISchreibeLage, IHoleRechnungsbild, IHoleLage { }
