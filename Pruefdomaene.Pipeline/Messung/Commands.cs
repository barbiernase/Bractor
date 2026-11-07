using Abstractions;
using Pruefdomaene.Messung;

namespace Pruefdomaene.Pipeline.Messung;

// COMMANDS ohne Ziel (docs/04 §4.4): nie im Log. Am Kopf gibt sie ein Client hinein, der auf das Fakt reagiert; zwischen den Knoten geben sie
// Knoten einander.

/// <summary>Ein Messwert ist erfasst (<see cref="MesswertErfasst"/>): die Merkmalsbildung macht daraus ein Merkmal.</summary>
public record NeuerWert(Id<Messstelle> Stelle, double Wert) : ICommand;

/// <summary>Ein Merkmal eines Messwerts — die <see cref="Merkmalsbildung"/> gibt es aus, die Ausreißerprüfung und der Schätzer nehmen es (ein Fork).</summary>
public record Merkmal(Id<Messstelle> Stelle, double Wert) : ICommand;
