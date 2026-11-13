using Abstractions;

namespace Rechenwerk.Katalog;

/// <summary>
/// Skaliert einen Wert — eine Katalogfunktion der Prüfdomäne: sie kennt keine Anwendung, Übersetzungen verbinden sie mit Client und Aggregat
/// (docs/04 §4.4).
/// </summary>
public sealed class Skalierer : IFunktion
{
    public Skaliert Auf(Skalieren e) => new(e.Bezug, e.Wert * e.Faktor);
}
