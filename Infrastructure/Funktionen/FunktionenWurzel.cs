using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.Logging;
using Proto;

namespace Infrastructure.Funktionen;

/// <summary>Ein Parent, den die Wurzel beim Start anlegt: die Funktion und wie sie an diesem Knoten läuft.</summary>
public sealed record ParentPlan(FunktionsTraeger Traeger, FunktionsOrtKonfig Konfig, Func<FunktionsParent> Neu);

/// <summary>
/// Die WURZEL „Funktionen“ je Knoten (docs/04 §4.4, Actor mit festem Namen <see cref="Wurzelname"/>, kein virtueller Actor): beim Start legt sie je
/// Funktion, die dieser Knoten laut Konfig halten darf, einen Parent an (<c>funktionen/&lt;Name&gt;</c>) und meldet die Funktionen in der
/// Mitgliedschaft (<see cref="FunktionsOrte"/>). Ein Parent, der scheitert, beginnt über die Supervision neu.
/// </summary>
public sealed class FunktionenWurzel(IReadOnlyList<ParentPlan> plaene, FunktionsOrte orte, ILogger? logger = null) : RahmenActor(logger)
{
    public const string Wurzelname = "funktionen";

    /// <summary>Der Name des Parents einer Funktion auf jedem Knoten.</summary>
    public static string Name(string funktion) => Wurzelname + "/" + funktion;

    protected override Task BeginneAsync(IContext context)
    {
        foreach (var p in plaene)
            context.SpawnNamed(Props.FromProducer(() => p.Neu()), p.Traeger.Name);
        orte.Halte(plaene.Select(p => p.Traeger.Name));
        return Task.CompletedTask;
    }

    protected override Task BehandleAsync(IContext context) => Task.CompletedTask;
}
