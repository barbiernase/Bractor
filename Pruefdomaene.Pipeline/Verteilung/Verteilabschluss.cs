using Abstractions;
using Pruefdomaene.Verteilung;

namespace Pruefdomaene.Pipeline.Verteilung;

/// <summary>
/// SAMMELN: sind alle Unterläufe fertig, kommen die gebuchten Ziele als Liste — die Verteilung schließt mit ihrer Summe ab (je Lauf genau einmal, die
/// Command-Id folgt aus dem Lauf; ein Ziel, das nicht gebucht wurde, fehlt in der Liste).
/// </summary>
public sealed class Verteilabschluss : IFunktion
{
    public IEnumerable<SchliesseVerteilungAb> Auf(IReadOnlyList<ZielGebucht> alle)
    {
        if (alle.Count == 0) yield break;
        yield return new SchliesseVerteilungAb(alle[0].VerteilungId, alle.Sum(z => z.Menge), alle.Count);
    }
}
