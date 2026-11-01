using Abstractions;
using Infrastructure.Eingaenge;
using Infrastructure.Emit;
using Infrastructure.Komposition;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Funktionen;

/// <summary>
/// Die Verdrahtung der Funktionen vor dem Aggregat (docs/04 §4.4), für jede Anwendung gleich: Orte im Gossip-Zustand, Wege über den Boten, die
/// Zustellung der offenen Eingänge an die Parents und beim Start die Wurzel mit je einem Parent je Funktion, die dieser Knoten hält.
/// </summary>
public static class FunktionsLaufzeitWiring
{
    public static IServiceCollection AddFunktionsLaufzeit(this IServiceCollection services)
    {
        services.TryAddSingleton(_ => new Verkettung(Anwendung.Funktionen.Verkettung));
        services.TryAddSingleton(sp => new FunktionsOrte(sp.GetRequiredService<ActorSystem>().Cluster()));
        services.TryAddSingleton<IFunktionsWege>(sp =>
        {
            var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("Funktionen");
            return new FunktionsWege(new Bote(sp.GetRequiredService<ActorSystem>().Cluster(), logger), sp.GetRequiredService<FunktionsOrte>(), logger);
        });
        if (!Anwendung.Funktionen.Bausteine.Any(b => b.VorDemAggregat)) return services;
        services.TryAddSingleton<IEingangsZustellung>(sp => new WegeZustellung(sp.GetRequiredService<IFunktionsWege>()));
        services.AddHostedService<FunktionsOrteTakt>();
        services.AddHostedService<FunktionenStart>();
        return services;
    }
}

/// <summary>Bringt einen offenen Eingang über die Wege zum Parent seiner Funktion (durabel: „angenommen“ sofort, ausgetragen trägt der Parent).</summary>
public sealed class WegeZustellung(IFunktionsWege wege) : IEingangsZustellung
{
    /// <summary>So lange wartet der Zusteller auf „angenommen“.</summary>
    public static readonly TimeSpan Frist = TimeSpan.FromSeconds(10);

    public async Task<Zustellausgang> StelleZuAsync(OffenerEingang e, CancellationToken ct)
    {
        var eingang = new EingangZustellen(e.Kennung, e.Funktion, Nachrichtenform.Lies(e.Typ, e.Daten), Eingangsart.Durabel, e.Stream, e.Version, e.Zeit,
            e.Akteur, e.Korrelation, Spur.Ursache(e.Stream, e.Version));
        return (await wege.StelleZuAsync(eingang, Frist, ct))?.Ausgang switch
        {
            Quittungsart.Fertig => Zustellausgang.Fertig,
            // Angenommen — und gescheitert: dann ist er nach der Frist des Zustellers wieder fällig und wird erneut gerechnet.
            Quittungsart.Angenommen or Quittungsart.Gescheitert => Zustellausgang.Angenommen,
            _ => Zustellausgang.Spaeter,
        };
    }
}

/// <summary>
/// Spawnt beim Start (nach dem Cluster-Beitritt) die Wurzel der Funktionen dieses Knotens: je Funktion vor dem Aggregat ein Parent mit der Umsetzung
/// aus ihrer Bindung. Im Umbau läuft eine solche Funktion nur im Host.
/// </summary>
public sealed class FunktionenStart(IServiceProvider dienste, ActorSystem system, FunktionsOrte orte, IFunktionsWege wege, Verkettung verkettung,
    ILoggerFactory? logs = null) : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        var logger = logs?.CreateLogger("Funktionen");
        var emitter = new CommandEmitter(new Bote(system.Cluster(), logger), logger);
        var bindungen = dienste.GetServices<BausteinBindung>().ToList();
        var plaene = new List<ParentPlan>();
        foreach (var b in Anwendung.Funktionen.Bausteine.Where(b => b.VorDemAggregat))
        {
            var bindung = FunktionsBausteinWiring.BindungVon(b, bindungen);
            if (bindung.Laufort != FunktionsLaufort.Host)
                throw new Infrastructure.Umgebung.BootFehler($"Funktionen:{b.Name}: eine Funktion vor dem Aggregat läuft im Umbau nur im Host.", "funktion:" + b.Name);
            var umsetzung = FunktionsBausteinWiring.Umsetzung(b, bindung);
            var gerufenerZustand = dienste.GetService<GerufeneWahl>()?.MitZustand(umsetzung.Typ) == true;
            var traeger = new FunktionsTraeger(b.Name, sp => new FunktionsAktivierung(sp, umsetzung.Baue, b.Zustand, b.Name, gerufenerZustand), b.Rechne!, b.Liest);
            var konfig = new FunktionsOrtKonfig(Parallel: bindung.Slots,
                Wiederholungen: bindung.Wiederholungen > 0 ? bindung.Wiederholungen : FunktionsOrtKonfig.StandardWiederholungen);
            var kindLogger = logs?.CreateLogger("Funktion." + b.Name);
            plaene.Add(new ParentPlan(traeger, konfig, () => new FunktionsParent(traeger, konfig,
                () => new FunktionsKindActor(traeger, dienste, wege, verkettung, async (cmd, id, korrelation, t) => await emitter.EmitAsync(cmd, id, korrelation, t),
                    dienste.GetService<IFaehigkeitsFabrik>(), kindLogger),
                dienste.GetService<IOffeneEingaenge>(), kindLogger)));
        }
        system.Root.SpawnNamed(Props.FromProducer(() => new FunktionenWurzel(plaene, orte, logger)), FunktionenWurzel.Wurzelname);
        Console.WriteLine($"  ✓ Funktionen vor dem Aggregat {system.Address}/{FunktionenWurzel.Wurzelname}: " + string.Join(", ", plaene.Select(p => p.Traeger.Name)));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
