using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.Logging;
using Proto;

namespace Infrastructure.Funktionen;

/// <summary>Eine Instanz, die die Wurzel anlegt: ihr Graph-Actor, ob sie Zustand hat und ob sie auf diesem Rechner festgelegt ist.</summary>
/// <param name="Festgelegt">Die Instanz nennt diesen Rechner: trägt ein zweiter Host denselben Namen, hält nur einer sie (<see cref="RechnerDoppelt"/>).</param>
public sealed record InstanzPlan(string Instanz, Func<GraphActor> Neu, bool MitZustand, bool Festgelegt = false);

/// <summary>
/// Ein Eingang aus einer Quelle, den die Wurzel auf diesem Host laufen lässt (docs/04 §4.7): sein Name (<c>Client/Eingang</c>) und sein Läufer.
/// <paramref name="Festgelegt"/>: er nennt diesen Rechner — trägt ein zweiter Host denselben Namen, lauscht nur einer.
/// </summary>
public sealed record LaeuferPlan(string Eingang, Func<IActor> Neu, bool Festgelegt = false);

/// <summary>
/// An die Wurzel: ein anderer Host im Cluster trägt denselben Rechnernamen und hat Vorrang (<see cref="FunktionsOrte"/>) — dieser hält seine
/// festgelegten Instanzen nicht mehr (bzw. wieder, wenn der andere fort ist).
/// </summary>
public sealed record RechnerDoppelt(bool Verdraengt);

/// <summary>
/// Die WURZEL „Funktionen“ je Knoten (docs/04 §4.4, Actor mit festem Namen <see cref="Wurzelname"/>, kein virtueller Actor): beim Start legt sie je
/// Instanz eines Graphen ihren Graph-Actor an (<c>funktionen/&lt;instanz&gt;</c>, darunter je Knoten ein Parent). Ob dieser Knoten die Instanz HÄLT
/// (Eingänge an sie bekommt), meldet sie in der Mitgliedschaft (<see cref="FunktionsOrte"/>): sofort — außer sie ist auf diesem Rechner festgelegt und
/// ein anderer Host mit demselben Namen hat Vorrang (dann gar nicht). Ein Graph-Actor, der scheitert, beginnt über die Supervision neu.
/// </summary>
/// <para>Dazu je Eingang aus einer Quelle, den dieser Host bedient, sein Läufer (<c>funktionen/eingang-&lt;Client&gt;-&lt;Eingang&gt;</c>) — er lauscht,
/// solange der Host ihn hält (bei doppeltem Rechnernamen nur der mit Vorrang).</para>
public sealed class FunktionenWurzel(IReadOnlyList<InstanzPlan> plaene, FunktionsOrte orte, ILogger? logger = null,
    IReadOnlyList<LaeuferPlan>? laeufer = null) : RahmenActor(logger)
{
    public const string Wurzelname = "funktionen";

    /// <summary>Der Name des Graph-Actors einer Instanz auf jedem Knoten.</summary>
    public static string Name(string instanz) => Wurzelname + "/" + instanz;

    private bool _verdraengt;
    private readonly Dictionary<string, PID> _graphen = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PID> _laeufer = new(StringComparer.Ordinal);

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
            case RechnerDoppelt d:
                _verdraengt = d.Verdraengt;
                Melde(context);
                break;
        }
        return Task.CompletedTask;
    }

    /// <summary>Hält dieser Knoten die Instanz? Hat ein Host mit demselben Rechnernamen Vorrang, gehen neue Eingänge an ihn bzw. warten; was der
    /// Graph-Actor noch hat, rechnet er zu Ende oder es scheitert, der durable Rand bringt es wieder.</summary>
    private bool Haelt(InstanzPlan p) => !p.Festgelegt || !_verdraengt;

    /// <summary>Meldet die gehaltenen Instanzen in der Mitgliedschaft und startet bzw. stoppt die Läufer der Eingänge, die dieser Host hält.</summary>
    private void Melde(IContext context)
    {
        orte.Halte(plaene.Where(Haelt).Select(p => p.Instanz));
        foreach (var l in laeufer ?? [])
        {
            var haelt = !l.Festgelegt || !_verdraengt;
            var laeuft = _laeufer.TryGetValue(l.Eingang, out var pid);
            if (haelt && !laeuft)
                _laeufer[l.Eingang] = context.SpawnNamed(Props.FromProducer(() => l.Neu()), "eingang-" + l.Eingang.Replace('/', '-'));
            else if (!haelt && laeuft)
            {
                context.Stop(pid!);
                _laeufer.Remove(l.Eingang);
            }
        }
    }
}

