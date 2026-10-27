using Abstractions;

namespace Pruefdomaene.Messung;

/// <summary>
/// Aggregat <see cref="Messstelle"/> — der allgemeine Prüfling für Funktionen als Actors (Plan Pub/Sub-Neustrukturierung §2.9, Phase 4b): Messwerte
/// kommen ins Log, eine gleichbleibende Funktion macht daraus Merkmale (Hinweise, nie im Log), ein Modell mit gekapseltem Zustand lernt aus dem Log
/// und schätzt auf jedes Merkmal hin, eine Prüfung meldet Ausreißer — was fachlich zählt, kommt als Command hierher zurück.
/// </summary>
public partial class Messstelle : IState
{
    public int Messwerte { get; set; }
    public int Schaetzungen { get; set; }
    public int Ausreisser { get; set; }
}
