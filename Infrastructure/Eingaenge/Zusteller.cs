using Infrastructure.Lebenszyklus;
using Infrastructure.Projections;   // IClusterKindContributor, Wake
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Eingaenge;

/// <summary>Wie eine Zustellung ausging: fertig (der Eingang verlässt die Tabelle), angenommen (in Arbeit) oder später (voll, nicht erreichbar).</summary>
public enum Zustellausgang { Fertig, Angenommen, Spaeter }

/// <summary>Wohin der Zusteller einen offenen Eingang bringt (Naht; live die Funktions-Laufzeit, docs/04 §4.4).</summary>
public interface IEingangsZustellung
{
    Task<Zustellausgang> StelleZuAsync(OffenerEingang eingang, CancellationToken ct);
}

/// <summary>
/// Die Arbeit des Zustellers (docs/04 §4.4): die fälligen offenen Eingänge lesen und je Eingang zustellen — fertig → aus der Tabelle; angenommen →
/// wieder fällig nach der <see cref="Frist"/> (kommt bis dahin kein „fertig“, geht er erneut hinaus: mindestens einmal); später oder gescheitert → wieder
/// fällig nach <see cref="Spaeter"/>. Der Zusteller ist gleichbleibend und hält nichts fest: die Nachricht steht in der Zeile, die Kennung ist abgeleitet,
/// ein Empfänger erkennt einen doppelten Eingang an ihr.
/// </summary>
public sealed class Zustellwerk(IOffeneEingaenge eingaenge, IEingangsZustellung zustellung, ILogger? logger = null, int seite = Zustellwerk.Seite)
{
    /// <summary>So viele fällige Eingänge holt ein Durchlauf je Abfrage.</summary>
    public const int Seite = 500;

    /// <summary>Ein angenommener Eingang ist nach dieser Frist wieder fällig, wenn bis dahin kein „fertig“ kam.</summary>
    public static readonly TimeSpan Frist = TimeSpan.FromSeconds(60);

    /// <summary>Ein Eingang, den niemand annahm, ist nach dieser Zeit wieder fällig.</summary>
    public static readonly TimeSpan Spaeter = TimeSpan.FromSeconds(2);

    /// <summary>Ein Durchlauf: Seiten fälliger Eingänge, bis eine nicht voll war. Liefert die zugestellten (fertig oder angenommen).</summary>
    public async Task<int> LaufAsync(CancellationToken ct)
    {
        var zugestellt = 0;
        while (!ct.IsCancellationRequested)
        {
            var faellig = await eingaenge.FaelligeAsync(seite, ct);
            if (faellig.Count == 0) return zugestellt;
            var ausgaenge = await Task.WhenAll(faellig.Select(e => StelleZuAsync(e, ct)));
            var fertig = faellig.Where((_, i) => ausgaenge[i] == Zustellausgang.Fertig).Select(e => e.Kennung).ToList();
            var angenommen = faellig.Where((_, i) => ausgaenge[i] == Zustellausgang.Angenommen).Select(e => e.Kennung).ToList();
            var spaeter = faellig.Where((_, i) => ausgaenge[i] == Zustellausgang.Spaeter).Select(e => e.Kennung).ToList();
            await eingaenge.ErledigtAsync(fertig, ct);
            await eingaenge.VerschiebeAsync(angenommen, Frist, ct);
            await eingaenge.VerschiebeAsync(spaeter, Spaeter, ct);
            zugestellt += fertig.Count + angenommen.Count;
            if (faellig.Count < seite) return zugestellt;
        }
        return zugestellt;
    }

    private async Task<Zustellausgang> StelleZuAsync(OffenerEingang e, CancellationToken ct)
    {
        try { return await zustellung.StelleZuAsync(e, ct); }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            logger?.LogWarning(ex, "[Zusteller] Eingang {Kennung} an {Instanz} nicht zugestellt — später erneut", e.Kennung, e.Instanz);
            return Zustellausgang.Spaeter;
        }
    }
}

/// <summary>
/// Der ZUSTELLER als virtueller Cluster-Actor (eine Aktivierung im Cluster, Kind <c>zusteller</c>, Identität <c>0</c>), nach dem Vorbild der Uhr: jeder
/// Knoten meldet ihm jede Sekunde einen Takt (<see cref="ZustellTakt"/>), ein Aggregat weckt ihn nach einem Commit mit Folgen; der Rahmen fasst die
/// Weckungen zusammen — höchstens ein Durchlauf zugleich, einer hinterher.
/// </summary>
public sealed class ZustellerActor(Zustellwerk werk, ILogger? logger = null) : RahmenActor(logger)
{
    public const string KindName = "zusteller";
    public static readonly ClusterIdentity Identitaet = ClusterIdentity.Create("0", KindName);

    protected override Task BehandleAsync(IContext context)
    {
        if (context.Message is Wake)
        {
            if (context.Sender is not null) context.Respond(new WakeAck());
            Wecke(context, async stopp =>
            {
                try { await werk.LaufAsync(stopp); }
                catch (OperationCanceledException) when (stopp.IsCancellationRequested) { }
                catch (Exception ex) { logger?.LogWarning(ex, "[Zusteller] Durchlauf gescheitert — der nächste Takt versucht es erneut"); }
            });
        }
        return Task.CompletedTask;
    }
}

/// <summary>Der Takt je Knoten: jede Sekunde eine Meldung an den Zusteller (verlierbar); ohne Zustellung (<paramref name="aktiv"/> falsch) keiner.</summary>
public sealed class ZustellTakt(ActorSystem system, ILogger<ZustellTakt>? logger = null, bool aktiv = true) : BackgroundService
{
    public static readonly TimeSpan Abstand = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!aktiv) return;
        var bote = new Bote(system.Cluster(), logger);
        try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); } catch (OperationCanceledException) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            bote.Melde(ZustellerActor.Identitaet, new Wake());
            try { await Task.Delay(Abstand, stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }
}

/// <summary>Das Cluster-Kind des Zustellers.</summary>
internal sealed class ZustellerKind : IClusterKindContributor
{
    public ClusterKind CreateKind(ActorSystem system, IServiceProvider provider)
    {
        var logger = provider.GetService<ILoggerFactory>()?.CreateLogger("Zusteller");
        return new ClusterKind(ZustellerActor.KindName, Props.FromProducer(() =>
            new ZustellerActor(new Zustellwerk(provider.GetRequiredService<IOffeneEingaenge>(), provider.GetRequiredService<IEingangsZustellung>(), logger),
                logger)));
    }
}

public static class EingangsExtensions
{
    /// <summary>
    /// Die offenen Eingänge: Tabelle, Verkettung (die generierte) und — wenn eine Zustellung registriert ist — der Zusteller mit Cluster-Kind
    /// und Takt je Knoten.
    /// </summary>
    public static IServiceCollection AddEingaenge(this IServiceCollection services)
    {
        services.AddSingleton<IOffeneEingaenge>(sp => new OffeneEingaenge(sp.GetRequiredService<Marten.IDocumentStore>()));
        services.TryAddSingleton(sp => sp.GetService<Infrastructure.Funktionen.GraphInstanzen>()?.Koepfe() ?? Infrastructure.Funktionen.Verkettung.Leer);
        services.AddSingleton<IClusterKindContributor, ZustellerKind>();
        services.AddHostedService(sp => new ZustellTakt(sp.GetRequiredService<ActorSystem>(), sp.GetService<ILogger<ZustellTakt>>(),
            sp.GetService<IEingangsZustellung>() is not null));
        return services;
    }
}
