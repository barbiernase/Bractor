using Abstractions;
using Pruefdomaene.Messung;

namespace Pruefdomaene.Pipeline.Skalierung;

/// <summary>
/// Ein Client fordert die Skalierung eines Werts für eine Messstelle an — ein Command ohne Ziel an den Kopf des Graphen <see cref="Hochrechnung"/>;
/// der Wert kommt über den Katalog Rechenwerk als Ausreißer an die Messstelle (docs/04 §4.4, Übersetzung in beide Richtungen).
/// </summary>
public sealed record HochrechnungAngefordert(Id<Messstelle> Stelle, double Wert) : ICommand;
