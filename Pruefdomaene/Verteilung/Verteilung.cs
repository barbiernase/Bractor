using Abstractions;

namespace Pruefdomaene.Verteilung;

/// <summary>Aggregat <see cref="Verteilung"/> — ihr Start löst den <see cref="VerteilProzess"/> aus (Fan-out auf N Posten, Count-Join).</summary>
public partial class Verteilung : IState
{
    public bool Abgeschlossen { get; set; }
    public int Zugebucht { get; set; }

    public bool Existiert => Version > 0;
}
