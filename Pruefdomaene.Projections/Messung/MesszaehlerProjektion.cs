using Abstractions;
using Pruefdomaene.Messung;

namespace Pruefdomaene.Projections.Messung;

/// <summary>
/// Ein Zähler der Messungen je Stelle — in einen Store der Klasse 3 (eine Senke ohne Bedingung): nur mindestens einmal, ausdrücklich erlaubt
/// (<see cref="MindestensEinmalAttribute"/>, R14). Eine Beobachtung, keine Wahrheit: nach einem Abbruch kann er zu hoch stehen.
/// </summary>
[MindestensEinmal]
public sealed class MesszaehlerProjektion : IProjektion
{
    public Task Auf(MesswertErfasst e, Herkunft h, IZaehleMessung zaehle) => zaehle.ZaehleAsync(h.Id<Messstelle>());
}
