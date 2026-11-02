using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.Logging;
using Proto;

namespace Infrastructure.Funktionen;

/// <summary>
/// Eine Instanz, die die Wurzel anlegt: ihr Graph-Actor, die Funktionen ihrer Knoten, die draußen rechnen (die Instanz hält dieser Knoten erst, wenn
/// für jede ein Arbeiter da ist), und ob sie Zustand hat (dann hält er sie nur mit ihrer <see cref="ZustandsSperre"/>).
/// </summary>
/// <param name="Quelle">Die Instanz hat eine Quelle: sie lauscht auf dem Knoten, der die Instanz hält (genau einer im Cluster, mit der Sperre).</param>
public sealed record InstanzPlan(string Instanz, Func<GraphActor> Neu, IReadOnlyList<string> Draussen, bool MitZustand, bool Quelle = false);

/// <summary>An die Wurzel: für die Funktion draußen hat dieser Knoten Arbeiter mit so vielen Plätzen (0 = keinen mehr).</summary>
public sealed record PlaetzeGeaendert(string Funktion, int Slots);

/// <summary>An die Wurzel: die Sperre der Instanz (mit Zustand) ist gehalten bzw. verloren.</summary>
public sealed record SperreGeaendert(string Instanz, bool Gehalten);

/// <summary>
/// Die WURZEL „Funktionen“ je Knoten (docs/04 §4.4, Actor mit festem Namen <see cref="Wurzelname"/>, kein virtueller Actor): beim Start legt sie je
/// Instanz eines Graphen ihren Graph-Actor an (<c>funktionen/&lt;instanz&gt;</c>, darunter je Knoten ein Parent). Ob dieser Knoten die Instanz HÄLT
/// (Eingänge an sie bekommt), meldet sie in der Mitgliedschaft (<see cref="FunktionsOrte"/>): sofort — außer eine Funktion ihrer Knoten rechnet
/// draußen (dann erst, wenn sich hier für jede ein Arbeiter gemeldet hat, <see cref="Werkstatt"/>) oder sie hat Zustand (dann nur, solange dieser
/// Knoten ihre Sperre hält, <see cref="ZustandsSperre"/>). Ein Graph-Actor, der scheitert, beginnt über die Supervision neu.
/// </summary>
public sealed class FunktionenWurzel(IReadOnlyList<InstanzPlan> plaene, FunktionsOrte orte, ILogger? logger = null) : RahmenActor(logger)
{
    public const string Wurzelname = "funktionen";

    /// <summary>Der Name des Graph-Actors einer Instanz auf jedem Knoten.</summary>
    public static string Name(string instanz) => Wurzelname + "/" + instanz;

    private readonly Dictionary<string, int> _plaetze = new(StringComparer.Ordinal);
    private readonly HashSet<string> _gesperrt = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PID> _graphen = new(StringComparer.Ordinal);
    private readonly Dictionary<string, bool> _lauscht = new(StringComparer.Ordinal);

    protected override Task BeginneAsync(IContext context)
    {
        foreach (var p in plaene)
            _graphen[p.Instanz] = context.SpawnNamed(Props.FromProducer(() => p.Neu()), p.Instanz);
        Melde(context);
        return Task.CompletedTask;
    }

    protected override Task BehandleAsync(IContext context)
    {
        switch (context.Message)
        {
            case PlaetzeGeaendert p:
                _plaetze[p.Funktion] = p.Slots;
                Melde(context);
                break;
            case SperreGeaendert s:
                if (s.Gehalten) _gesperrt.Add(s.Instanz); else _gesperrt.Remove(s.Instanz);
                Melde(context);
                break;
        }
        return Task.CompletedTask;
    }

    /// <summary>Hält dieser Knoten die Instanz? Fehlt ein Arbeiter oder die Sperre, gehen neue Eingänge an andere Knoten; was der Graph-Actor noch hat,
    /// rechnet er zu Ende oder es scheitert, der durable Rand bringt es wieder.</summary>
    private bool Haelt(InstanzPlan p) =>
        p.Draussen.All(f => _plaetze.GetValueOrDefault(f) > 0) && (!p.MitZustand || _gesperrt.Contains(p.Instanz));

    /// <summary>Meldet die gehaltenen Instanzen in der Mitgliedschaft und sagt jeder Quellinstanz, ob sie hier lauscht.</summary>
    private void Melde(IContext context)
    {
        orte.Halte(plaene.Where(Haelt).Select(p => p.Instanz));
        foreach (var p in plaene.Where(p => p.Quelle))
        {
            var haelt = Haelt(p);
            if (_lauscht.TryGetValue(p.Instanz, out var vorher) && vorher == haelt) continue;
            _lauscht[p.Instanz] = haelt;
            context.Send(_graphen[p.Instanz], new QuelleHalten(haelt));
        }
    }
}

