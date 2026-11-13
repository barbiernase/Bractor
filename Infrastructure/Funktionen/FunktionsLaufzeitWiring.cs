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
/// Die Verdrahtung der Graphen vor dem Aggregat (docs/04 §4.4), für jede Anwendung gleich: Orte im Gossip-Zustand, Wege über den Boten, die
/// Zustellung der offenen Eingänge an die Graph-Actors und beim Start die Wurzel mit je einem Graph-Actor je Instanz.
/// </summary>
public static class FunktionsLaufzeitWiring
{
    public static IServiceCollection AddFunktionsLaufzeit(this IServiceCollection services)
    {
        services.TryAddSingleton(sp => new FunktionsOrte(sp.GetRequiredService<ActorSystem>().Cluster(), sp.GetService<EigenerRechner>()?.Name,
            sp.GetService<ILoggerFactory>()?.CreateLogger("Funktionen")));
        services.TryAddSingleton<IFunktionsWege>(sp =>
        {
            var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("Funktionen");
            return new FunktionsWege(new Bote(sp.GetRequiredService<ActorSystem>().Cluster(), logger), sp.GetRequiredService<FunktionsOrte>(), logger);
        });
        if (Anwendung.Funktionen.Bausteine.Count == 0) return services;
        services.TryAddSingleton<IEingangsZustellung>(sp => new WegeZustellung(sp.GetRequiredService<IFunktionsWege>()));
        services.TryAddSingleton<IZustandsAblage>(sp => new ZustandsAblage(sp.GetRequiredService<Marten.IDocumentStore>()));
        services.AddHostedService<FunktionsOrteTakt>();
        services.AddHostedService<FunktionenStart>();
        return services;
    }

    /// <summary>Rechnet ein Knoten der Instanz im Host mit Zustand — dem seiner Funktion oder dem einer Technik in der Konstruktor-Kette ihrer Umsetzung?</summary>
    public static bool MitZustand(GraphInstanz instanz, IServiceProvider dienste) =>
        FunktionsBausteinWiring.Knoten(new GraphInstanzen([instanz])).Any(x => MitZustand(x.Baustein, x.Konfig, instanz.Ort(x.Knoten.Name), dienste));

    /// <summary>Rechnet die Funktion mit dieser Konfig am Ort <paramref name="ort"/> (<c>instanz/knoten</c>) im Host mit Zustand?</summary>
    public static bool MitZustand(FunktionsBaustein b, KnotenKonfig k, string ort, IServiceProvider dienste) => b.Zustand is not null || dienste.GetService<TechnikWahl>()?.MitZustand(ort) == true;
}

/// <summary>Bringt einen offenen Eingang über die Wege zum Kopf seiner Instanz (durabel: „angenommen“ sofort, ausgetragen trägt der Parent).</summary>
public sealed class WegeZustellung(IFunktionsWege wege) : IEingangsZustellung
{
    /// <summary>So lange wartet der Zusteller auf „angenommen“.</summary>
    public static readonly TimeSpan Frist = TimeSpan.FromSeconds(10);

    public async Task<Zustellausgang> StelleZuAsync(OffenerEingang e, CancellationToken ct)
    {
        var eingang = new EingangZustellen(e.Kennung, e.Instanz, Nachrichtenform.Lies(e.Typ, e.Daten), Eingangsart.Durabel, e.Stream, e.Version, e.Zeit,
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
/// Spawnt beim Start (nach dem Cluster-Beitritt) die Wurzel der Graphen dieses Knotens: je Instanz ein Graph-Actor mit je Knoten einem Parent — im
/// Host mit seiner Funktion (eine Aktivierung je Kind, mit dem Knoten-Bereich für Konfig-Records und Technik); ein Knoten mit Zustand rechnet mit genau
/// einem Kind. Eine Instanz, die einen anderen Rechner nennt, legt er gar nicht an (<see cref="EigenerRechner"/>).
/// </summary>
public sealed class FunktionenStart(IServiceProvider dienste, ActorSystem system, FunktionsOrte orte, IFunktionsWege wege, Verkettung koepfe,
    GraphInstanzen instanzen, ILoggerFactory? logs = null) : IHostedService
{
    public Task StartAsync(CancellationToken ct)
    {
        var rechner = dienste.GetService<EigenerRechner>() ?? EigenerRechner.Keiner;
        rechner.Pruefe(instanzen, i => FunktionsLaufzeitWiring.MitZustand(i, dienste));
        var logger = logs?.CreateLogger("Funktionen");
        var emitter = new CommandEmitter(new Bote(system.Cluster(), logger), logger);
        var verteilung = dienste.GetService<Infrastructure.Verteilen.Verteilung>();
        Action<HinweisZustellen>? hinweise = verteilung is null ? null : h =>
        {
            // An die Akteure, die ihn hören: über die Verteiler an ihre Sitzungen (Funktionen hören keine Hinweise).
            verteilung.Melde(new EventEnvelope
            {
                AggregateId = h.Stream, AggregateVersion = h.Version, CorrelationId = h.Korrelation, UserId = h.Akteur, Payload = h.Hinweis,
                AggregateType = "Funktion",
            });
        };
        var plaene = new List<InstanzPlan>();
        // Nur die Instanzen, die dieser Host bedient: ohne Rechner jede, sonst die, die seinen nennen (eine Quelle an ihrer Steuerung, ein Modell auf
        //   seiner GPU) — so hat jede festgelegte Instanz genau einen Schreiber.
        foreach (var instanz in instanzen.Alle.Where(rechner.Bedient))
        {
            var knoten = new Dictionary<string, Func<FunktionsParent>>(StringComparer.Ordinal);
            var mitZustand = FunktionsLaufzeitWiring.MitZustand(instanz, dienste);
            foreach (var (_, k, b, kk) in FunktionsBausteinWiring.Knoten(new GraphInstanzen([instanz])))
            {
                var bindung = FunktionsBausteinWiring.BindungVon(b, kk, instanz.Ort(k.Name));
                var antworten = b.Antworten();
                var wiederholungen = bindung.Wiederholungen > 0 ? bindung.Wiederholungen : FunktionsOrtKonfig.StandardWiederholungen;
                var kindLogger = logs?.CreateLogger("Funktion." + b.Name);
                var knotenWege = new KnotenWege(instanz, k.Name);
                FunktionsParent Parent(FunktionsTraeger traeger, FunktionsOrtKonfig konfig, IZustandsAblage? zustand) => new(traeger, konfig,
                    () => new FunktionsKindActor(traeger, dienste, wege, koepfe, async (cmd, id, korrelation, t) => await emitter.EmitMitAntwortAsync(cmd, id, korrelation, t),
                        dienste.GetService<IFaehigkeitsFabrik>(), kindLogger, hinweise: hinweise),
                    knotenWege, zustand, kindLogger);
                var konfig = new FunktionsOrtKonfig(
                    Parallel: bindung.Parallel > 0 ? bindung.Parallel : FunktionsOrtKonfig.StandardParallel,
                    Warteschlange: bindung.Warteschlange > 0 ? bindung.Warteschlange : FunktionsOrtKonfig.StandardWarteschlange,
                    Vorwaermen: bindung.Vorwaermen, Wiederholungen: wiederholungen);
                FunktionsTraeger traeger;
                IZustandsAblage? zustandsAblage = null;
                var technikZustand = dienste.GetService<TechnikWahl>()?.MitZustand(instanz.Ort(k.Name)) == true;
                var bereich = new KnotenKontext(instanz.Name, k.Name, kk);
                var ablage = dienste.GetService<IZustandsAblage>();
                traeger = new FunktionsTraeger(b.Name, sp => new FunktionsAktivierung(sp, b.Baue!, b.Zustand, b.Name, technikZustand, bereich, ablage),
                    b.Rechne, b.Liest, antworten)
                { Instanz = instanz.Name, Knoten = k.Name, ImGraphen = instanz.Graph.ImGraphen(k.Name), Uebersetzungen = koepfe.Uebersetzungen,
                    Zeitlimit = kk.Zeitlimit };
                // Mit Zustand: ein Kind — eine Aktivierung im Cluster, auf dem Rechner der Instanz (der Zustand ist ein Cache und soll alles sehen).
                if (mitZustand && (b.Zustand is not null || technikZustand))
                {
                    konfig = konfig with { Parallel = 1 };
                    zustandsAblage = ablage;   // der Parent sichert seinen Schnappschuss mit den Kennungen, die er enthält
                }
                var t0 = traeger;
                var k0 = konfig;
                var z0 = zustandsAblage;
                knoten[k.Name] = () => Parent(t0, k0, z0);
            }
            var i0 = instanz;
            var knoten0 = knoten;
            plaene.Add(new InstanzPlan(instanz.Name, () => new GraphActor(i0, knoten0, dienste.GetService<IOffeneEingaenge>(), hinweise, logger,
                async (cmd, id, korrelation, t) => await emitter.EmitMitAntwortAsync(cmd, id, korrelation, t)), mitZustand,
                Festgelegt: instanz.Rechner is not null && !rechner.Einzelknoten));
        }
        // Die Eingänge aus Quellen, die dieser Host bedient (sein Rechner bzw. jeder im Einzelknoten-Betrieb): je einer ein Läufer (docs/04 §4.7).
        var eingaenge = dienste.GetService<Infrastructure.Quellen.Eingaenge>() ?? Infrastructure.Quellen.Eingaenge.Keine;
        var quellLogger = logs?.CreateLogger("Quellen");
        var laeufer = eingaenge.Alle.Where(e => rechner.Bedient(e.Rechner)).Select(e => new LaeuferPlan(e.Ort,
            () => new Infrastructure.Quellen.QuellLaeufer(e, dienste, async (cmd, id, korrelation, t) => await emitter.EmitMitAntwortAsync(cmd, id, korrelation, t),
                koepfe, dienste.GetService<IOffeneEingaenge>(), wege, dienste.GetService<IZustandsAblage>(), quellLogger),
            Festgelegt: e.Rechner is not null && !rechner.Einzelknoten)).ToList();
        _wurzel = system.Root.SpawnNamed(Props.FromProducer(() => new FunktionenWurzel(plaene, orte, logger, laeufer)), FunktionenWurzel.Wurzelname);
        var wurzel = _wurzel;
        orte.Beobachte(doppelt => system.Root.Send(wurzel, new RechnerDoppelt(doppelt)));
        Console.WriteLine($"  ✓ Graphen {system.Address}/{FunktionenWurzel.Wurzelname}: " + string.Join(", ", plaene.Select(p => p.Instanz
            + (p.MitZustand ? " (mit Zustand)" : ""))) + (laeufer.Count > 0 ? " · Eingänge: " + string.Join(", ", laeufer.Select(l => l.Eingang)) : ""));
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
