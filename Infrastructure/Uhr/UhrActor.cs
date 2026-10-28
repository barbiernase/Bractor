using Abstractions;
using Infrastructure.Komposition;
using Infrastructure.Lebenszyklus;
using Infrastructure.Projections;   // IClusterKindContributor, Wake
using Infrastructure.Emit;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Uhr;

/// <summary>
/// Die Arbeit der Uhr (Plan Pub/Sub-Neustrukturierung §2.2, §4.2): die fälligen Fristen lesen, je Frist ihren Command senden — mit der Command-Id
/// aus dem Frist-Event (Stream, Version), also beim zweiten Feuern derselben: doppelt gefeuert, einmal gewirkt. Entschieden (zugestellt oder
/// abgelehnt) → aus dem Verzeichnis; unbekannt → liegen lassen, der nächste Takt feuert erneut. Die Uhr ist gleichbleibend und hält nichts fest:
/// der Command steht im Frist-Event.
/// </summary>
/// <param name="befehl">Liest den Command einer Frist (Diskriminator, Wire-JSON); live die generierten Tabellen (<c>Befehlsform</c>).</param>
public sealed class Uhrwerk(IFaelligkeiten verzeichnis, Func<ICommand, Guid, Guid, CancellationToken, Task<EmitAusgang>> sende, ILogger? logger = null,
    int seite = Uhrwerk.Seite, Func<string, string, ICommand>? befehl = null)
{
    private readonly Func<string, string, ICommand> _befehl = befehl ?? ((typ, daten) => Funktionen.Befehlsform.Lies(new Funktionen.BefehlFestgehalten(typ, daten)));

    /// <summary>So viele fällige Fristen holt ein Durchlauf je Abfrage.</summary>
    public const int Seite = 500;

    /// <summary>Die Command-Id einer Frist: aus ihrem Event — jedes Feuern derselben Frist trägt dieselbe.</summary>
    public static Guid BefehlsId(Guid stream, int version) => Herkunft.Ableite($"frist:{stream:N}:{version}");

    /// <summary>Ein Durchlauf: Seiten fälliger Fristen, bis eine nicht voll war oder nichts mehr entschieden wurde. Liefert die entschiedenen.</summary>
    public async Task<int> LaufAsync(CancellationToken ct)
    {
        var entschieden = 0;
        while (!ct.IsCancellationRequested)
        {
            var faellig = await verzeichnis.FaelligeAsync(seite, ct);
            if (faellig.Count == 0) return entschieden;
            var ausgaenge = await Task.WhenAll(faellig.Select(f => FeuereAsync(f, ct)));
            var jetzt = ausgaenge.Count(x => x);
            entschieden += jetzt;
            if (faellig.Count < seite || jetzt == 0) return entschieden;   // was unbekannt blieb, versucht der nächste Takt
        }
        return entschieden;
    }

    private async Task<bool> FeuereAsync(FaelligeFrist f, CancellationToken ct)
    {
        ICommand befehl;
        try { befehl = _befehl(f.Befehl, f.Daten); }
        catch (Exception ex)
        {
            logger?.LogError(ex, "[Uhr] Frist {Stream}:{Version}: Command {Typ} nicht lesbar — bleibt liegen", f.Stream, f.Version, f.Befehl);
            return false;
        }
        // Spur: Ursache ist das Frist-Event, Wirker die Uhr; der Akteur der Kette, der die Frist setzte, reist mit (docs/konzept-akteure.md §5.1).
        using var spur = Spur.Von(Spur.Ursache(f.Stream, f.Version), WirkerId.Uhr);
        using var akteur = Akteure.AkteurHerkunft.Aus(f.Akteur);
        var ausgang = await sende(befehl, BefehlsId(f.Stream, f.Version), f.Stream, ct);
        if (!ausgang.IstEntschieden()) return false;
        await verzeichnis.ErledigtAsync(f.Stream, f.Version, ct);
        return true;
    }
}

/// <summary>
/// Die UHR als virtueller Cluster-Actor (eine Aktivierung im Cluster, Kind <c>uhr</c>, Identität <c>0</c>): jeder Knoten meldet ihr jede Sekunde
/// einen Takt (<see cref="UhrTakt"/>), der Wecker weckt sie nach einer gesetzten Frist (Weckplan); der Rahmen fasst die Weckungen zusammen — höchstens ein Durchlauf zugleich, einer hinterher. Auflösung
/// ≈ 1 s. Fällt eine Aktivierung aus, startet der nächste Takt sie anderswo; was sie halb gefeuert hatte, feuert sie erneut (dieselbe Command-Id).
/// </summary>
public sealed class UhrActor(Uhrwerk werk, ILogger? logger = null) : RahmenActor(logger)
{
    public const string KindName = "uhr";
    public static readonly ClusterIdentity Identitaet = ClusterIdentity.Create("0", KindName);

    protected override Task BehandleAsync(IContext context)
    {
        // Geweckt vom Takt je Knoten (jede Sekunde) und vom Wecker, wenn eine Frist gesetzt wurde (Weckplan: FristGesetzt → Uhr).
        if (context.Message is Wake)
        {
            if (context.Sender is not null) context.Respond(new WakeAck());
            Wecke(context, async stopp =>
            {
                try { await werk.LaufAsync(stopp); }
                catch (OperationCanceledException) when (stopp.IsCancellationRequested) { }
                catch (Exception ex) { logger?.LogWarning(ex, "[Uhr] Durchlauf gescheitert — der nächste Takt versucht es erneut"); }
            });
        }
        return Task.CompletedTask;
    }
}

/// <summary>Der Takt je Knoten: jede Sekunde eine Meldung an die Uhr (verlierbar, gebunden).</summary>
public sealed class UhrTakt(ActorSystem system, ILogger<UhrTakt>? logger = null) : BackgroundService
{
    public static readonly TimeSpan Abstand = TimeSpan.FromSeconds(1);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bote = new Bote(system.Cluster(), logger);
        try { await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken); } catch (OperationCanceledException) { return; }
        while (!stoppingToken.IsCancellationRequested)
        {
            bote.Melde(UhrActor.Identitaet, new Wake());
            try { await Task.Delay(Abstand, stoppingToken); } catch (OperationCanceledException) { return; }
        }
    }
}

/// <summary>Das Cluster-Kind der Uhr.</summary>
internal sealed class UhrKind : IClusterKindContributor
{
    public ClusterKind CreateKind(ActorSystem system, IServiceProvider provider)
    {
        var logger = provider.GetService<ILoggerFactory>()?.CreateLogger("Uhr");
        return new ClusterKind(UhrActor.KindName, Props.FromProducer(() =>
        {
            var emitter = new CommandEmitter(new Bote(system.Cluster(), logger), logger);
            var werk = new Uhrwerk(provider.GetRequiredService<IFaelligkeiten>(),
                async (cmd, id, korrelation, ct) => await emitter.EmitAsync(cmd, id, korrelation, ct), logger);
            return new UhrActor(werk, logger);
        }));
    }
}

public static class UhrExtensions
{
    /// <summary>Die Uhr: Verzeichnis, Cluster-Kind, Takt je Knoten (jede Anwendung hat sie — ohne Fristen ist ihr Durchlauf eine leere Abfrage).</summary>
    public static IServiceCollection AddUhr(this IServiceCollection services)
    {
        services.AddSingleton<IFaelligkeiten>(sp => new FristVerzeichnis(sp.GetRequiredService<Marten.IDocumentStore>()));
        services.AddSingleton<IClusterKindContributor, UhrKind>();
        services.AddHostedService<UhrTakt>();
        return services;
    }
}
