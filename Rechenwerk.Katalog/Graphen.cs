using Abstractions;

namespace Rechenwerk.Katalog;

/// <summary>
/// Die Hochrechnung (docs/04 §4.4): der Skalierer am Kopf — domänenneutral. Was in den Kopf kommt und was aus dem Ergebnis wird, entscheiden die
/// Übersetzungen der Domäne, die ihn nutzt (die Prüfdomäne: eine Anforderung für eine Messstelle hinein, ein Ausreißer an die Messstelle heraus).
/// </summary>
public sealed record Hochrechnung([Kopf] Skalierer Rechnen) : IGraph;
