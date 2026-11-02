using Abstractions;
using Pruefdomaene.Messung;

namespace Pruefdomaene.Pipeline.Messung;

// COMMANDS, die nur Knoten einander geben (docs/04 §4.4): Commands ohne Ziel, nie im Log.

/// <summary>Ein Merkmal eines Messwerts — die <see cref="Merkmalsbildung"/> gibt es aus, die Ausreißerprüfung und der Schätzer nehmen es (ein Fork).</summary>
public record Merkmal(Id<Messstelle> Stelle, double Wert) : ICommand;
