using Abstractions;
using Pruefdomaene.Messung;

namespace Pruefdomaene.Projections.Messung;

/// <summary>Die Messpunkte einer Stelle in Log-Reihenfolge und der letzte Wert.</summary>
public record Messreihe(Id<Messstelle> Stelle, IReadOnlyList<double> Werte, double? Letzter) : IQueryResponse;

/// <summary>Wie oft die Messungen einer Stelle gezählt wurden (mindestens einmal je Messung).</summary>
public record Messzaehler(Id<Messstelle> Stelle, int Anzahl) : IQueryResponse;
