using Infrastructure.Lebenszyklus;
using Infrastructure.Projections;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Startup;

/// <summary>
/// Die Bereitschaft des Clusters beim Start eines Knotens: erst wenn Aktivierungen auf jedem Mitglied antworten, beginnt der Knoten zu arbeiten
/// (Takt, Wecker, Eingänge). Nach dem harten Ende eines Knotens steht er noch eine Weile in der Mitgliederliste (Consul: bis sein Zustand abläuft);
/// der Cluster legt Aktivierungen auf ihn, und jede Anfrage dorthin läuft in ihre Frist — ein neu gestarteter Knoten, der sofort Last abarbeitet,
/// verliert so Minuten. Die Sperre fragt Proben-Identitäten, die über die Mitglieder streuen, mit Wiederholung, bis jede antwortet.
/// </summary>
public static class Bereitschaft
{
    /// <summary>Das Cluster-Kind der Proben (ein Actor, der nur antwortet).</summary>
    public const string Kind = "bereitschaft";

    /// <summary>So viele Proben-Identitäten — genug, dass jedes Mitglied eines kleinen Clusters mindestens eine besitzt.</summary>
    public const int Proben = 32;

    /// <summary>Wie lange der Start höchstens wartet; danach arbeitet der Knoten trotzdem (mit Warnung).</summary>
    public static readonly TimeSpan Hoechstens = TimeSpan.FromSeconds(90);

    /// <summary>Die Frist je Probe.</summary>
    public static readonly TimeSpan ProbenFrist = TimeSpan.FromSeconds(3);

    public static ClusterKind ClusterKind() => new(Kind, Props.FromProducer(() => new ProbeActor()));

    /// <summary>Wartet, bis jede Probe antwortet (oder <see cref="Hoechstens"/> um ist). Liefert, ob der Cluster bereit war.</summary>
    public static async Task<bool> WarteAsync(Cluster cluster, ILogger? logger, CancellationToken ct)
    {
        var bote = new Bote(cluster, logger);
        var offen = Enumerable.Range(0, Proben).Select(i => ClusterIdentity.Create($"probe-{i}", Kind)).ToList();
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        var pause = TimeSpan.FromMilliseconds(250);
        while (true)
        {
            var antworten = await Task.WhenAll(offen.Select(async p => (p, await bote.FrageAsync<WakeAck>(p, new Wake(), ProbenFrist, ct))));
            offen = antworten.Where(a => a.Item2 is null).Select(a => a.p).ToList();
            if (offen.Count == 0) return true;
            if (System.Diagnostics.Stopwatch.GetElapsedTime(start) >= Hoechstens)
            {
                logger?.LogWarning("[Start] Cluster nach {Sekunden} s nicht bereit: {Offen} von {Proben} Proben ohne Antwort — der Knoten arbeitet trotzdem",
                    Hoechstens.TotalSeconds, offen.Count, Proben);
                return false;
            }
            Console.WriteLine($"  … Cluster noch nicht bereit ({offen.Count} von {Proben} Proben ohne Antwort), nächster Versuch in {pause.TotalMilliseconds:0} ms");
            await Task.Delay(pause, ct);
            if (pause < TimeSpan.FromSeconds(2)) pause *= 2;
        }
    }

    /// <summary>Antwortet auf jede Probe; passiviert nach kurzem Leerlauf.</summary>
    private sealed class ProbeActor() : RahmenActor(leerlauf: TimeSpan.FromSeconds(30))
    {
        protected override Task BehandleAsync(IContext context)
        {
            if (context.Message is Wake) context.Respond(new WakeAck());
            return Task.CompletedTask;
        }
    }
}
