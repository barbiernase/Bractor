using Abstractions;
using Pruefdomaene.Messung;

namespace Pruefdomaene.Pipeline.Messung;

/// <summary>
/// Ein Merkmal eines Messwerts — ein HINWEIS (Plan §2.9, E7): er gehört nicht zur Fachdomäne, geht von Funktion zu Funktion im Speicher, nie in den
/// Log, höchstens einmal. Wer ihn hört, bekommt ihn direkt an seine Adresse.
/// </summary>
public record Merkmal(Id<Messstelle> Stelle, double Wert) : ITransientEvent;
