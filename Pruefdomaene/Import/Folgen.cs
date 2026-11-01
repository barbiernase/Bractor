using Abstractions;

namespace Pruefdomaene.Import;

// FOLGEN des Imports (docs/03 §3.2): was eine Entscheidung den Funktionen sagt, die sie hören — schlichte Records, nicht im Log.

/// <summary>Der Import ist gestartet: seine Zeilen sind zu lesen und als Posten anzulegen.</summary>
public record ImportZuLaufen(Id<Importauftrag> Import, int Zeilen);
