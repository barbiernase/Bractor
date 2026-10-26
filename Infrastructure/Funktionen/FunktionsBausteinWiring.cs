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
/// (bei mehreren nach Name), Slots je Partition und Wiederholungen, bevor ein werfender Eingang seinen Stream parkt.
/// </summary>
public sealed record BausteinBindung(string Funktion, FunktionsLaufort Laufort = FunktionsLaufort.Host, string? Umsetzung = null, int Slots = 1,
    int Wiederholungen = 0);

/// <summary>
/// Die Verdrahtung der Funktionen in Baustein-Form (Plan Pub/Sub-Neustrukturierung Phase 3), für jede Anwendung gleich — aus der generierten Tabelle
/// <c>Anwendung.Funktionen.Bausteine</c>, ohne Domänen-Typ im Framework:
/// <list type="bullet">
/// <item>je Funktion ein Cluster-Kind <c>funktion-&lt;F&gt;</c> (Identität = Partition) mit dem <see cref="FunktionsActor"/>;</item>
/// <item>je Funktion mit Eingängen aus dem Log eine Weck-Registrierung wie bei einer Projektion: das Signal eines Aggregat-Commits weckt die Partition,
///   der Takt (30 s) jede Partition — bis der Wecker am Flush kommt (Phase 6). Ihr Stand liegt im selben Dokument wie der einer Projektion, so zählt
///   <c>/health</c> ihren Rückstand mit;</item>
/// <item>das Festhalten im Log (<see cref="LogAusgabeAblage"/>) und der Stand im Dokument-Store (<see cref="MartenFunktionsStand"/>);</item>
/// <item>für Funktionen draußen der Vermittler (Lease) — die Hand-Seite über den <see cref="HandAusfuehrer"/>.</item>
/// </list>
/// </summary>
public static class FunktionsBausteinWiring
{
    public static IServiceCollection AddFunktionsBausteine(this IServiceCollection services)
    {
        var bausteine = Anwendung.Funktionen.Bausteine;
        if (bausteine.Count == 0) return services;
        services.TryAddSingleton<IAusgabeAblage>(sp => new LogAusgabeAblage(sp.GetRequiredService<IEventStoreRepository>()));
        services.TryAddSingleton<IFunktionsStand>(sp => new MartenFunktionsStand(sp.GetRequiredService<Marten.IDocumentStore>()));
        foreach (var b in bausteine)
        {
            services.AddSingleton<IClusterKindContributor>(new FunktionsKind(b));
            if (b.LogEingaenge.Any())
                services.AddSingleton(new BatchRegistrierung(b.KindName, b.StandId, b.LogEingaenge.Select(e => e.Typ).ToList()));
        }
        if (bausteine.Any(b => b.LogEingaenge.Any()))
        {
            services.AddHostedService<BatchKonsumStartupService>();   // idempotent (TryAddEnumerable), auch wenn die Projektionen ihn schon anmeldeten
            services.TryAddSingleton<IKonsumRueckstand, KonsumRueckstand>();
        }
        if (bausteine.Any(b => b.Draussen) && !services.Any(d => d.ImplementationType == typeof(FunktionsVermittlerKind)))
            services.AddSingleton<IClusterKindContributor, FunktionsVermittlerKind>();
        Console.WriteLine($"[CQRS] {bausteine.Count} Funktion(en) als Baustein: " + string.Join(", ", bausteine.Select(b => b.Name)));
        return services;
    }

    /// <summary>
    /// Die Bindung einer Funktion ohne Prüfung: die letzte registrierte, sonst der Standard — eine Klasse läuft im Host, eine Schnittstelle mit genau
    /// einer Umsetzung ebenso (wie ein Dienst mit einer Implementierung); sonst keine.
    /// </summary>
    public static BausteinBindung? Gebunden(FunktionsBaustein b, IEnumerable<BausteinBindung> bindungen) =>
        bindungen.LastOrDefault(x => x.Funktion == b.Name) ?? (!b.Draussen || b.Umsetzungen.Count == 1 ? new BausteinBindung(b.Name) : null);

    /// <summary>
    /// Die geprüfte Bindung einer Funktion (<see cref="Gebunden"/>). Eine Funktion draußen ohne Bindung und ohne eindeutige Umsetzung ist ein
    /// Boot-Fehler (R17: sie braucht einen Träger oder eine Bindung), ebenso eine Funktion mit Lese-Fähigkeit, die nicht im Host läuft.
    /// </summary>
    public static BausteinBindung BindungVon(FunktionsBaustein b, IEnumerable<BausteinBindung> bindungen)
    {
        var bindung = Gebunden(b, bindungen)
            ?? throw new Infrastructure.Umgebung.BootFehler($"Die Funktion '{b.Name}' ist eine Schnittstelle (läuft draußen) und hat keine Bindung — in der "
                + $"Umgebung fehlt Funktionen:{b.Name} (Laufort Hand, oder Host mit Implementierung).", "funktion:" + b.Name);
        if (bindung.Laufort != FunktionsLaufort.Host && b.Liest)
            throw new Infrastructure.Umgebung.BootFehler($"Funktionen:{b.Name}: eine Funktion mit Lese-Fähigkeit läuft nur im Host (Laufort Host).", "funktion:" + b.Name);
        if (bindung.Laufort == FunktionsLaufort.Extern)
            throw new Infrastructure.Umgebung.BootFehler($"Funktionen:{b.Name}: Laufort Extern (Worker, Client über die Leitung) kommt für Funktionen in Baustein-Form mit "
                + "ITraegt (Plan Pub/Sub-Neustrukturierung Phase 7) — heute Host oder Hand.", "funktion:" + b.Name);
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

    /// <summary>Die Umsetzung im Host nach der Bindung (gebaut einmal je Knoten über die generierte Fabrik).</summary>
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
        // Die Umsetzung einmal je Knoten (wie die Katalog-Funktionen: darf warme Ressourcen halten, muss bei Slots > 1 thread-sicher sein).
        var instanz = bindung.Laufort == FunktionsLaufort.Host
            ? new Lazy<object>(() => FunktionsBausteinWiring.Umsetzung(baustein, bindung).Baue(provider))
            : null;
        return new ClusterKind(baustein.KindName, Props.FromProducer(() =>
        {
            // Der Cluster ist hier fertig (Spawn-Zeit): Emitter und Bote erst jetzt bauen.
            var bote = new Bote(system.Cluster(), logger);
            var emitter = new CommandEmitter(bote, logger);
            var publisher = provider.GetService<BrokerPublisher>();
            var arbeit = new FunktionsArbeit(baustein,
                instanz is null ? null : () => instanz.Value,
                provider.GetRequiredService<IAusgabeAblage>(),
                async (cmd, id, korrelation, ct) => (await emitter.EmitAsync(cmd, id, korrelation, ct)).Ausgang,
                publisher is null ? null : (hinweis, ursache) => publisher.Melde(new EventEnvelope
                {
                    AggregateId = ursache.AggregateId, AggregateVersion = ursache.AggregateVersion, CorrelationId = ursache.CorrelationId,
                    UserId = ursache.UserId, Payload = hinweis, AggregateType = "Funktion:" + baustein.Name,
                }),
                provider.GetService<IFaehigkeitsFabrik>(),
                (angebot, ct) => bote.FrageAsync<WakeAck>(FunktionsVermittlerActor.Identitaet(baustein.Typ), angebot, TimeSpan.FromSeconds(5), ct),
                logger);
            return new FunktionsActor(baustein, arbeit, provider.GetRequiredService<IEventStoreRepository>(), provider.GetRequiredService<IFunktionsStand>(),
                konsum, bindung.Slots, bindung.Wiederholungen, provider.GetService<IDeadLetterSink>(), logger, typen: typen);
        }));
    }
}
