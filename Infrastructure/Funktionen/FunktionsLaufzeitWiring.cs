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
        if (Anwendung.Funktionen.Bausteine.Count == 0) return services;
        services.TryAddSingleton<Werkstatt>();
        // Funktionen mit Zustand (im Host): genau eine Aktivierung im Cluster, wer die Sperre hält.
        services.TryAddSingleton(sp => new ZustandsSperre(sp.GetRequiredService<Marten.IDocumentStore>(),
            Anwendung.Funktionen.Bausteine.Where(b => MitZustand(b, sp)).Select(b => b.Name).ToList(), sp.GetService<ILogger<ZustandsSperre>>()));
        services.AddHostedService(sp => sp.GetRequiredService<ZustandsSperre>());
        services.TryAddSingleton<IEingangsZustellung>(sp => new WegeZustellung(sp.GetRequiredService<IFunktionsWege>()));
        services.AddHostedService<FunktionsOrteTakt>();
        services.AddHostedService<FunktionenStart>();
        return services;
    }

    /// <summary>Rechnet die Funktion im Host mit Zustand — ihrem eigenen oder dem einer gerufenen Funktion in der Konstruktor-Kette ihrer Umsetzung?</summary>
    public static bool MitZustand(FunktionsBaustein b, IServiceProvider dienste)
    {
        var bindung = FunktionsBausteinWiring.BindungVon(b, dienste.GetServices<BausteinBindung>());
        if (bindung.Laufort != FunktionsLaufort.Host) return false;
        return b.Zustand is not null || dienste.GetService<GerufeneWahl>()?.MitZustand(FunktionsBausteinWiring.Umsetzung(b, bindung).Typ) == true;
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
/// Spawnt beim Start (nach dem Cluster-Beitritt) die Wurzel der Funktionen dieses Knotens: je Funktion vor dem Aggregat im Host ein Parent mit der
/// Umsetzung aus ihrer Bindung; für eine Funktion draußen (Laufort Extern oder Hand) den Plan, nach dem die Wurzel ihren Parent anlegt, sobald sich ein
/// Arbeiter in der <see cref="Werkstatt"/> meldet — seine Kinder geben ihre Eingänge an die Arbeiter; für eine Funktion mit Zustand den Plan mit genau
/// einem Kind, angelegt, sobald dieser Knoten ihre <see cref="ZustandsSperre"/> hält.
/// </summary>
public sealed class FunktionenStart(IServiceProvider dienste, ActorSystem system, FunktionsOrte orte, IFunktionsWege wege, Verkettung verkettung,
    Werkstatt werkstatt, ZustandsSperre sperre, ILoggerFactory? logs = null) : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        var logger = logs?.CreateLogger("Funktionen");
        var emitter = new CommandEmitter(new Bote(system.Cluster(), logger), logger);
        var netz = dienste.GetService<HinweisNetz>();
        var verteilung = dienste.GetService<Infrastructure.Verteilen.Verteilung>();
        Action<HinweisZustellen>? hinweise = netz is null && verteilung is null ? null : h =>
        {
            // An die hörenden Funktionen (Hinweis-Netz: beide Laufzeiten); an die Clients, die ihn hören, über die Verteiler.
            netz?.Verteile(h);
            verteilung?.Melde(new EventEnvelope
            {
                AggregateId = h.Stream, AggregateVersion = h.Version, CorrelationId = h.Korrelation, UserId = h.Akteur, Payload = h.Hinweis,
                AggregateType = "Funktion",
            });
        };
        var bindungen = dienste.GetServices<BausteinBindung>().ToList();
        var plaene = new List<ParentPlan>();
        var bedarf = new List<BedarfsPlan>();
        foreach (var b in Anwendung.Funktionen.Bausteine)
        {
            var bindung = FunktionsBausteinWiring.BindungVon(b, bindungen);
            var antworten = b.Eingaenge.Where(e => !e.Hinweis && typeof(IEvent).IsAssignableFrom(e.Typ) && !typeof(IQuellNachricht).IsAssignableFrom(e.Typ))
                .Select(e => e.Typ).ToHashSet();
            var wiederholungen = bindung.Wiederholungen > 0 ? bindung.Wiederholungen : FunktionsOrtKonfig.StandardWiederholungen;
            var kindLogger = logs?.CreateLogger("Funktion." + b.Name);
            FunktionsParent Parent(FunktionsTraeger traeger, FunktionsOrtKonfig konfig) => new(traeger, konfig,
                () => new FunktionsKindActor(traeger, dienste, wege, verkettung, async (cmd, id, korrelation, t) => await emitter.EmitMitAntwortAsync(cmd, id, korrelation, t),
                    dienste.GetService<IFaehigkeitsFabrik>(), kindLogger, hinweise: hinweise),
                dienste.GetService<IOffeneEingaenge>(), kindLogger);
            if (bindung.Laufort != FunktionsLaufort.Host)
            {
                // Draußen: kein Bereich, keine Umsetzung — ein Arbeiter in der Werkstatt rechnet. Die Kinder warten dort auf einen freien Platz; ein
                //   Platz wird frei, sobald die Antwort vollständig ist, das Kind bringt sie danach hinaus (so wartet der Arbeiter nicht auf die Kette).
                var baustein = b;
                var traeger = new FunktionsTraeger(b.Name, null, null, false, antworten,
                    (kennung, nachricht, herkunft, korrelation, hinweis, t) => werkstatt.RechneAsync(baustein, kennung, nachricht, herkunft, korrelation, hinweis, t));
                var draussen = new FunktionsOrtKonfig(
                    Parallel: bindung.Parallel > 0 ? bindung.Parallel : FunktionsOrtKonfig.StandardParallel,
                    Warteschlange: bindung.Warteschlange > 0 ? bindung.Warteschlange : FunktionsOrtKonfig.StandardWarteschlange,
                    Wiederholungen: wiederholungen);
                bedarf.Add(new BedarfsPlan(b.Name, _ => Parent(traeger, draussen)));
                continue;
            }
            var umsetzung = FunktionsBausteinWiring.Umsetzung(b, bindung);
            var gerufenerZustand = dienste.GetService<GerufeneWahl>()?.MitZustand(umsetzung.Typ) == true;
            var imHost = new FunktionsTraeger(b.Name, sp => new FunktionsAktivierung(sp, umsetzung.Baue, b.Zustand, b.Name, gerufenerZustand), b.Rechne, b.Liest,
                antworten);
            var konfig = new FunktionsOrtKonfig(
                Parallel: bindung.Parallel > 0 ? bindung.Parallel : FunktionsOrtKonfig.StandardParallel,
                Warteschlange: bindung.Warteschlange > 0 ? bindung.Warteschlange : FunktionsOrtKonfig.StandardWarteschlange,
                Vorwaermen: bindung.Vorwaermen, Wiederholungen: wiederholungen);
            if (sperre.Funktionen.Contains(b.Name))
            {
                // Mit Zustand: ein Kind — eine Aktivierung im Cluster, auf dem Knoten, der die Sperre hält (der Zustand ist ein Cache und soll alles sehen).
                var einKind = konfig with { Parallel = 1 };
                bedarf.Add(new BedarfsPlan(b.Name, _ => Parent(imHost, einKind)));
                continue;
            }
            plaene.Add(new ParentPlan(imHost, konfig, () => Parent(imHost, konfig)));
        }
        _wurzel = system.Root.SpawnNamed(Props.FromProducer(() => new FunktionenWurzel(plaene, orte, logger, bedarf)), FunktionenWurzel.Wurzelname);
        var wurzel = _wurzel;
        werkstatt.Beobachte((f, slots) => system.Root.Send(wurzel, new PlaetzeGeaendert(f, slots)));
        sperre.Beobachte((f, gehalten) => system.Root.Send(wurzel, new PlaetzeGeaendert(f, gehalten)));
        Console.WriteLine($"  ✓ Funktionen {system.Address}/{FunktionenWurzel.Wurzelname}: " + string.Join(", ", plaene.Select(p => p.Traeger.Name))
            + (bedarf.Count > 0 ? "; bei Bedarf (draußen, mit Zustand): " + string.Join(", ", bedarf.Select(d => d.Funktion)) : ""));
        return Task.CompletedTask;
    }

    private PID? _wurzel;

    /// <summary>Stoppt die Wurzel samt Parents und Kindern geordnet (vor dem Cluster): jede Aktivierung gibt ihre Ressourcen frei.</summary>
    public async Task StopAsync(CancellationToken ct)
    {
        if (_wurzel is null) return;
        try { await system.Root.StopAsync(_wurzel).WaitAsync(TimeSpan.FromSeconds(15), ct); }
        catch (Exception) when (!ct.IsCancellationRequested) { /* ein hängender Stopp hält den Host nicht auf */ }
    }
}
