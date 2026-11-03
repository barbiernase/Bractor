using Abstractions;
using Infrastructure.Akteure;

namespace Infrastructure.Funktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// ARBEIT FÜR AKTEURE (docs/konzept-akteure.md §5): hört ein Akteur einen Command ohne Ziel (IHoert<TCmd>), ist das Arbeit — der Command hat ihn als
// einzigen Empfänger. Die Laufzeit macht daraus eine Instanz unter dem Namen des Akteurs mit genau einem Knoten, der DRAUSSEN rechnet: sein Kind gibt den
// Command der Werkstatt, die ihn einer Sitzung gibt, die den Akteur verkörpert (verkörpert ∩ Token), und erneut zustellt, bis eine Antwort kommt — die
// erste gewinnt. Die Antwort sind Commands über das IDarf des Akteurs; ihre Ids folgen aus dem Auftrag (Kennung, Ort, Stelle) wie die jedes Knotens.
// So bekommt die Arbeit alles, was ein Kopf hat: offene Eingänge aus einem Decide, den Weg aus einem Graphen, Orte, Wiederholung.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Die Arbeit eines Akteurs als Baustein, Graph und Instanz (ein Knoten draußen).</summary>
public static class Arbeit
{
    /// <summary>Der Baustein der Arbeit: je gehörtem Command ein Eingang, als Antwort die Commands seines IDarf (in Stücken erlaubt).</summary>
    public static FunktionsBaustein Baustein(AkteurRechte a) => new(a.Typ, a.Name,
        a.Arbeit.OrderBy(t => t.Name, StringComparer.Ordinal)
            .Select(t => new FunktionsEingang(t, Strom: true, a.Commands.OrderBy(x => x.Name, StringComparer.Ordinal).ToArray(), Type.EmptyTypes)).ToList(),
        Baue: null, Dienste: Type.EmptyTypes, Rechne: null);

    /// <summary>Der Graph der Arbeit: ein Knoten (der Akteur), je gehörtem Command ein Kopf; was er antwortet, verlässt ihn.</summary>
    public static GraphPlan Plan(AkteurRechte a)
    {
        var b = Baustein(a);
        var operationen = b.Eingaenge.Select((e, i) => new OperationPlan(i, e.Typ, e.Ausgaben.ToArray(), [])).ToList();
        return new GraphPlan(a.Name, a.Typ, [new KnotenPlan(a.Name, a.Name, operationen)], b.Eingaenge.ToDictionary(e => e.Typ, _ => new[] { a.Name }),
            b.Eingaenge.SelectMany(e => e.Ausgaben).Distinct().ToList());
    }

    /// <summary>Je Akteur mit Arbeit eine Instanz unter seinem Namen; was sie antwortet, gibt der Akteur hinein.</summary>
    public static IEnumerable<GraphInstanz> Instanzen(IReadOnlyDictionary<string, AkteurRechte> akteure) =>
        akteure.Values.Where(a => a.Arbeit.Count > 0).OrderBy(a => a.Name, StringComparer.Ordinal)
            .Select(a => new GraphInstanz(a.Name, Plan(a), a.Name, new Dictionary<string, KnotenKonfig>()));
}
