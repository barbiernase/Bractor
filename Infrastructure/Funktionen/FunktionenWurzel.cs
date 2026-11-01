using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.Logging;
using Proto;

namespace Infrastructure.Funktionen;

/// <summary>Ein Parent, den die Wurzel anlegt: die Funktion und wie sie an diesem Knoten läuft.</summary>
public sealed record ParentPlan(FunktionsTraeger Traeger, FunktionsOrtKonfig Konfig, Func<FunktionsParent> Neu);

/// <summary>Eine Funktion draußen: wie ihr Parent mit so vielen Kindern entsteht, wie ihre Arbeiter Slots haben.</summary>
public sealed record DraussenPlan(string Funktion, Func<int, FunktionsParent> Neu);

/// <summary>An die Wurzel: die Slots der Arbeiter einer Funktion draußen auf diesem Knoten (0 = kein Arbeiter mehr).</summary>
public sealed record DraussenGeaendert(string Funktion, int Slots);

/// <summary>
/// Die WURZEL „Funktionen“ je Knoten (docs/04 §4.4, Actor mit festem Namen <see cref="Wurzelname"/>, kein virtueller Actor): beim Start legt sie je
/// Funktion im Host, die dieser Knoten laut Konfig halten darf, einen Parent an (<c>funktionen/&lt;Name&gt;</c>); für eine Funktion draußen entsteht er,
/// sobald sich auf diesem Knoten ein Arbeiter für sie meldet (<see cref="Werkstatt"/>); geht der letzte, hält der Knoten sie nicht mehr. Was der Knoten hält, meldet sie in
/// der Mitgliedschaft (<see cref="FunktionsOrte"/>). Ein Parent, der scheitert, beginnt über die Supervision neu.
/// </summary>
public sealed class FunktionenWurzel(IReadOnlyList<ParentPlan> plaene, FunktionsOrte orte, ILogger? logger = null,
    IReadOnlyList<DraussenPlan>? draussen = null) : RahmenActor(logger)
{
    public const string Wurzelname = "funktionen";

    /// <summary>Der Name des Parents einer Funktion auf jedem Knoten.</summary>
    public static string Name(string funktion) => Wurzelname + "/" + funktion;

    private readonly Dictionary<string, PID> _draussen = new(StringComparer.Ordinal);
    private readonly HashSet<string> _mitArbeiter = new(StringComparer.Ordinal);

    protected override Task BeginneAsync(IContext context)
    {
        foreach (var p in plaene)
            context.SpawnNamed(Props.FromProducer(() => p.Neu()), p.Traeger.Name);
        Melde();
        return Task.CompletedTask;
    }

    protected override Task BehandleAsync(IContext context)
    {
        if (context.Message is not DraussenGeaendert g || draussen?.FirstOrDefault(d => d.Funktion == g.Funktion) is not { } plan) return Task.CompletedTask;
        if (g.Slots > 0)
        {
            if (_draussen.TryGetValue(g.Funktion, out var parent)) context.Send(parent, new ParallelSetzen(g.Slots));
            else _draussen[g.Funktion] = context.SpawnNamed(Props.FromProducer(() => plan.Neu(g.Slots)), g.Funktion);
            if (_mitArbeiter.Add(g.Funktion)) Melde();
        }
        else if (_mitArbeiter.Remove(g.Funktion))
        {
            // Der letzte Arbeiter ist fort: der Knoten hält die Funktion nicht mehr (neue Eingänge gehen an Knoten mit Arbeiter); was der Parent noch
            //   hat, scheitert ohne Arbeiter, der durable Rand bringt es wieder. Der Parent bleibt (sein Name ist fest), bis ein Arbeiter zurückkommt.
            Melde();
        }
        return Task.CompletedTask;
    }

    private void Melde() => orte.Halte(plaene.Select(p => p.Traeger.Name).Concat(_mitArbeiter));
}
