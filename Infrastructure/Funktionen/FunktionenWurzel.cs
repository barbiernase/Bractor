using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.Logging;
using Proto;

namespace Infrastructure.Funktionen;

/// <summary>Ein Parent, den die Wurzel anlegt: die Funktion und wie sie an diesem Knoten läuft.</summary>
public sealed record ParentPlan(FunktionsTraeger Traeger, FunktionsOrtKonfig Konfig, Func<FunktionsParent> Neu);

/// <summary>
/// Eine Funktion, deren Parent erst bei Bedarf entsteht: draußen, sobald sich ein Arbeiter meldet (die Kinder warten in der Werkstatt auf einen
/// freien Platz); mit Zustand, sobald dieser Knoten ihre Sperre hält (ein Kind, <see cref="ZustandsSperre"/>).
/// </summary>
public sealed record BedarfsPlan(string Funktion, Func<int, FunktionsParent> Neu);

/// <summary>An die Wurzel: die Funktion hat auf diesem Knoten Bedarf (Slots bzw. 1) oder keinen mehr (0 = der Knoten hält sie nicht mehr).</summary>
public sealed record PlaetzeGeaendert(string Funktion, int Slots);

/// <summary>
/// Die WURZEL „Funktionen“ je Knoten (docs/04 §4.4, Actor mit festem Namen <see cref="Wurzelname"/>, kein virtueller Actor): beim Start legt sie je
/// Funktion im Host, die dieser Knoten laut Konfig halten darf, einen Parent an (<c>funktionen/&lt;Name&gt;</c>); für eine Funktion draußen entsteht er,
/// sobald sich auf diesem Knoten ein Arbeiter für sie meldet (<see cref="Werkstatt"/>), für eine mit Zustand, sobald der Knoten ihre Sperre hält
/// (<see cref="ZustandsSperre"/>); fällt der Bedarf weg, hält der Knoten sie nicht mehr. Was der Knoten hält, meldet sie in
/// der Mitgliedschaft (<see cref="FunktionsOrte"/>). Ein Parent, der scheitert, beginnt über die Supervision neu.
/// </summary>
public sealed class FunktionenWurzel(IReadOnlyList<ParentPlan> plaene, FunktionsOrte orte, ILogger? logger = null,
    IReadOnlyList<BedarfsPlan>? bedarf = null) : RahmenActor(logger)
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
        if (context.Message is not PlaetzeGeaendert g || bedarf?.FirstOrDefault(d => d.Funktion == g.Funktion) is not { } plan) return Task.CompletedTask;
        if (g.Slots > 0)
        {
            if (!_draussen.ContainsKey(g.Funktion)) _draussen[g.Funktion] = context.SpawnNamed(Props.FromProducer(() => plan.Neu(g.Slots)), g.Funktion);
            if (_mitArbeiter.Add(g.Funktion)) Melde();
        }
        else if (_mitArbeiter.Remove(g.Funktion))
        {
            // Kein Bedarf mehr (der letzte Arbeiter ist fort, die Sperre verloren): der Knoten hält die Funktion nicht mehr — neue Eingänge gehen an
            //   andere Knoten; was der Parent noch hat, rechnet er zu Ende oder es scheitert, der durable Rand bringt es wieder. Der Parent bleibt (sein
            //   Name ist fest), bis der Bedarf zurückkommt.
            Melde();
        }
        return Task.CompletedTask;
    }

    private void Melde() => orte.Halte(plaene.Select(p => p.Traeger.Name).Concat(_mitArbeiter));
}
