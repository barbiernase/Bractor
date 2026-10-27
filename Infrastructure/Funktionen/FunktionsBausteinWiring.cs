using Abstractions;
using Infrastructure.Komposition;
using Infrastructure.Lebenszyklus;
using Infrastructure.Projections;   // IClusterKindContributor, BatchRegistrierung, BatchKonsumStartupService, Konsum
using Infrastructure.PubSub;        // CommandEmitter, BrokerPublisher
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Funktionen;

/// <summary>
/// Wo und wie eine Funktion in Baustein-Form läuft (Umgebung <c>Funktionen:&lt;Name&gt;</c>, sonst der Standard): Laufort, die Umsetzung im Host
/// (bei mehreren nach Name), Slots je Partition und Wiederholungen, bevor ein werfender Eingang seinen Stream parkt; für eine Funktion mit Zustand
/// dazu, wo und wie oft er gesichert wird (ohne Angabe <see cref="ZustandsBindung.Standard"/>).
/// </summary>
public sealed record BausteinBindung(string Funktion, FunktionsLaufort Laufort = FunktionsLaufort.Host, string? Umsetzung = null, int Slots = 1,
    int Wiederholungen = 0, ZustandsBindung? Zustand = null);

/// <summary>
/// Die Verdrahtung der Funktionen in Baustein-Form (Plan Pub/Sub-Neustrukturierung Phase 3), für jede Anwendung gleich — aus der generierten Tabelle
/// <c>Anwendung.Funktionen.Bausteine</c>, ohne Domänen-Typ im Framework:
/// <list type="bullet">
/// <item>je Funktion ein Cluster-Kind <c>funktion-&lt;F&gt;</c> (Identität = Partition) mit dem <see cref="FunktionsActor"/>;</item>
/// <item>je Funktion mit Eingängen aus dem Log eine Weck-Registrierung wie bei einer Projektion: das Signal eines Aggregat-Commits weckt die Partition,
///   der Takt (30 s) jede Partition — bis der Wecker am Flush kommt (Phase 6). Ihr Stand liegt im selben Dokument wie der einer Projektion, so zählt
///   <c>/health</c> ihren Rückstand mit;</item>
/// <item>das Festhalten im Log (<see cref="LogAusgabeAblage"/>) und der Stand im Dokument-Store (<see cref="MartenFunktionsStand"/>);</item>
/// <item>das Hinweis-Netz (<see cref="HinweisNetz"/>), wenn irgendeine Funktion einen Hinweis hört: Hinweise aus Funktionen und Aggregaten gehen
///   direkt an die hörenden Funktions-Actors, ohne Log und Broker;</item>
/// <item>für Funktionen draußen der Vermittler (Lease) — die Hand-Seite über den <see cref="HandAusfuehrer"/>, die Leitung (Laufort Extern) über die
///   Sitzung des Clients, der sie trägt (<c>CqrsClientService</c>).</item>
/// </list>
/// </summary>
public static class FunktionsBausteinWiring
{
    public static IServiceCollection AddFunktionsBausteine(this IServiceCollection services)
    {
        var bausteine = Anwendung.Funktionen.Bausteine;
        if (bausteine.Count == 0) return services;
        services.TryAddSingleton<IAusgabeAblage>(sp => new LogAusgabeAblage(sp.GetRequiredService<IEventStoreRepository>(), sp.GetService<BrokerPublisher>()));
        services.TryAddSingleton<IFunktionsStand>(sp => new MartenFunktionsStand(sp.GetRequiredService<Marten.IDocumentStore>()));
        foreach (var b in bausteine)
        {
            services.AddSingleton<IClusterKindContributor>(new FunktionsKind(b));
            if (b.HoertLog)
                services.AddSingleton(new BatchRegistrierung(b.KindName, b.StandId, b.LogEingaenge.Select(e => e.Typ).ToList(), EineAdresse: b.EineAdresse));
        }
        if (bausteine.Any(b => b.HoertLog))
        {
            services.AddHostedService<BatchKonsumStartupService>();   // idempotent (TryAddEnumerable), auch wenn die Projektionen ihn schon anmeldeten
            services.TryAddSingleton<IKonsumRueckstand, KonsumRueckstand>();
        }
        // Das Hinweis-Netz (Plan §2.9): die generierte Routing-Tabelle Hinweis-Typ → Funktions-Adressen, Versand direkt über den Bote.
        if (Anwendung.Funktionen.Hinweisplan.Count > 0)
            services.TryAddSingleton(sp => new HinweisNetz(bausteine, Anwendung.Funktionen.Hinweisplan, sp.GetService<Konsum>() ?? Konsum.Standard,
                () => new Bote(sp.GetRequiredService<ActorSystem>().Cluster(), sp.GetService<ILoggerFactory>()?.CreateLogger("Hinweisnetz")),
                sp.GetService<ILoggerFactory>()?.CreateLogger("Hinweisnetz")));
        if (bausteine.Any(b => b.Draussen) && !services.Any(d => d.ImplementationType == typeof(FunktionsVermittlerKind)))
            services.AddSingleton<IClusterKindContributor, FunktionsVermittlerKind>();
        Console.WriteLine($"[CQRS] {bausteine.Count} Funktion(en) als Baustein: " + string.Join(", ", bausteine.Select(b => b.Name)));
        return services;
    }

    /// <summary>
    /// Die Bindung einer Funktion ohne Prüfung: die letzte registrierte, sonst der Standard — eine Klasse läuft im Host, eine Schnittstelle mit genau
    /// einer Umsetzung ebenso (wie ein Dienst mit einer Implementierung); eine Schnittstelle ohne Umsetzung, die ein Client trägt
    /// (<c>ITraegt&lt;F&gt;</c>), läuft draußen (Laufort Extern); sonst keine.
    /// </summary>
    public static BausteinBindung? Gebunden(FunktionsBaustein b, IEnumerable<BausteinBindung> bindungen) =>
        bindungen.LastOrDefault(x => x.Funktion == b.Name)
        ?? (!b.Draussen || b.Umsetzungen.Count == 1 ? new BausteinBindung(b.Name)
            : b.Umsetzungen.Count == 0 && Getragen(b) ? new BausteinBindung(b.Name, FunktionsLaufort.Extern)
            : null);

    /// <summary>Trägt irgendein Client die Funktion (<c>ITraegt&lt;F&gt;</c> in einem Client-Vertrag)?</summary>
    public static bool Getragen(FunktionsBaustein b) => Anwendung.Akteure.ClientVertraege.Values.Any(c => c.Traegt.Contains(b.Typ));

    /// <summary>
    /// Die geprüfte Bindung einer Funktion (<see cref="Gebunden"/>). Eine Funktion draußen ohne Bindung, ohne eindeutige Umsetzung und ohne Träger
    /// ist ein Boot-Fehler (R17: sie braucht einen Träger oder eine Bindung), ebenso eine Funktion mit Lese-Fähigkeit, die nicht im Host läuft.
    /// </summary>
    public static BausteinBindung BindungVon(FunktionsBaustein b, IEnumerable<BausteinBindung> bindungen)
    {
        var bindung = Gebunden(b, bindungen)
            ?? throw new Infrastructure.Umgebung.BootFehler($"Die Funktion '{b.Name}' ist eine Schnittstelle (läuft draußen), hat keine Bindung und keinen "
                + $"Träger — in der Umgebung fehlt Funktionen:{b.Name} (Laufort Hand oder Extern, oder Host mit Implementierung), oder ein Client-Vertrag "
                + $"trägt sie nicht (ITraegt<{b.Name}>).", "funktion:" + b.Name);
        if (bindung.Laufort != FunktionsLaufort.Host && b.Liest)
            throw new Infrastructure.Umgebung.BootFehler($"Funktionen:{b.Name}: eine Funktion mit Lese-Fähigkeit läuft nur im Host (Laufort Host).", "funktion:" + b.Name);
        if (bindung.Laufort == FunktionsLaufort.Extern && !b.Draussen)
            throw new Infrastructure.Umgebung.BootFehler($"Funktionen:{b.Name}: Laufort Extern braucht eine Schnittstelle — eine Klasse rechnet im Host.", "funktion:" + b.Name);
        if (bindung.Laufort == FunktionsLaufort.Host)
        {
            var passend = b.Umsetzungen.Where(u => bindung.Umsetzung is null || u.Name == bindung.Umsetzung).ToList();
            if (passend.Count != 1)
                throw new Infrastructure.Umgebung.BootFehler($"Funktionen:{b.Name}: Laufort Host braucht genau eine Umsetzung"
                    + (bindung.Umsetzung is null ? "" : $" namens '{bindung.Umsetzung}'") + " — gefunden: "
                    + string.Join(", ", b.Umsetzungen.Select(u => u.Name).DefaultIfEmpty("keine")) + ".", "funktion:" + b.Name);
        }
        return bindung;
    }

    /// <summary>Die Umsetzung im Host nach der Bindung (gebaut je Aktivierung über die generierte Fabrik, <see cref="FunktionsAktivierung"/>).</summary>
    public static FunktionsUmsetzung Umsetzung(FunktionsBaustein b, BausteinBindung bindung) =>
        b.Umsetzungen.Single(u => bindung.Umsetzung is null || u.Name == bindung.Umsetzung);
}

/// <summary>Das Cluster-Kind des Funktions-Actors einer Funktion (Identität = Partition).</summary>
internal sealed class FunktionsKind(FunktionsBaustein baustein) : IClusterKindContributor
{
    public ClusterKind CreateKind(ActorSystem system, IServiceProvider provider)
    {
        var bindung = FunktionsBausteinWiring.BindungVon(baustein, provider.GetServices<BausteinBindung>());
        var konsum = provider.GetService<Konsum>() ?? Konsum.Standard;
        var logger = provider.GetService<ILoggerFactory>()?.CreateLogger("Funktion." + baustein.Name);
        var typen = FunktionsActor.TypenVon(baustein);
        var umsetzung = bindung.Laufort == FunktionsLaufort.Host ? FunktionsBausteinWiring.Umsetzung(baustein, bindung) : null;
        // Mit Zustand (Plan §2.9): Laden und Sichern über den Stand-Store, große Zustände als Datei in der Ablage der Umgebung.
        var zustand = bindung.Zustand ?? ZustandsBindung.Standard;
        var sicherung = umsetzung is not null && baustein.Zustand is not null
            ? new ZustandsSicherung(provider.GetRequiredService<IFunktionsStand>(), zustand,
                zustand.Verzeichnis is { } verz ? new VerzeichnisZustandsDateien(verz) : null)
            : null;
        var netz = provider.GetService<HinweisNetz>();   // Start-Prüfung der Routing-Tabelle (R20: jede Adresse existiert)
        return new ClusterKind(baustein.KindName, Props.FromProducer(() =>
        {
            // Der Cluster ist hier fertig (Spawn-Zeit): Emitter und Bote erst jetzt bauen.
            var bote = new Bote(system.Cluster(), logger);
            var emitter = new CommandEmitter(bote, logger);
            var publisher = provider.GetService<BrokerPublisher>();
            // Die Umsetzung je Aktivierung, samt ihren Diensten in einem eigenen Bereich (Plan §2.9); bei Slots > 1 teilen sich die Eingänge
            //   dieser Aktivierung die Instanz — sie muss dann thread-sicher sein.
            var aktivierung = umsetzung is null ? null : new FunktionsAktivierung(provider, umsetzung.Baue, baustein.Zustand);
            var arbeit = new FunktionsArbeit(baustein,
                aktivierung is null ? null : () => aktivierung.Umsetzung,
                provider.GetRequiredService<IAusgabeAblage>(),
                async (cmd, id, korrelation, ct) => await emitter.EmitAsync(cmd, id, korrelation, ct),
                publisher is null && netz is null ? null : h =>
                {
                    // An die hörenden Funktionen direkt (Hinweis-Netz); an Clients bis Plan Phase 7 auf dem heutigen Weg.
                    netz?.Verteile(h);
                    publisher?.Melde(new EventEnvelope
                    {
                        AggregateId = h.Stream, AggregateVersion = h.Version, CorrelationId = h.Korrelation, UserId = h.Akteur, Payload = h.Hinweis,
                        AggregateType = "Funktion:" + baustein.Name,
                    });
                },
                provider.GetService<IFaehigkeitsFabrik>(),
                (angebot, ct) => bote.FrageAsync<WakeAck>(FunktionsVermittlerActor.Identitaet(baustein.Typ), angebot, TimeSpan.FromSeconds(5), ct),
                logger, partitionen: konsum.Partitionen);
            return new FunktionsActor(baustein, arbeit, provider.GetRequiredService<IEventStoreRepository>(), provider.GetRequiredService<IFunktionsStand>(),
                konsum, bindung.Slots, bindung.Wiederholungen, provider.GetService<IDeadLetterSink>(), logger, typen: typen, aktivierung: aktivierung,
                sicherung: sicherung);
        }));
    }
}
