using Abstractions;

namespace Pruefdomaene.Messung;

public record MesswertErfasst(double Wert) : IEvent;
public record SchaetzungVermerkt(double Erwartet, int Anzahl) : IEvent;
public record AusreisserGemeldet(double Wert) : IEvent;

/// <summary>
/// Ein Merkmal eines Messwerts — ein HINWEIS (docs/04 §4.4, docs/10 E7): er gehört nicht zur Fachdomäne, geht von Funktion zu Funktion im Speicher, nie in den
/// Log, höchstens einmal. Wer ihn hört, bekommt ihn direkt an seine Adresse.
/// </summary>
public record Merkmal(Id<Messstelle> Stelle, double Wert) : ITransientEvent;
