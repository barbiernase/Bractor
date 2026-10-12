using Abstractions;
using Infrastructure.Projections;   // IClusterKindContributor
using Infrastructure.Quellen.Treiber.Datei;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Quellen;

/// <summary>Eine konfigurierte Quelle: Typ aus der generierten Tabelle + Werte aus der Konfig.</summary>
public sealed record KonfigurierteQuelle(QuellTyp Typ, DateiQuellKonfig Konfig);

/// <summary>
/// Läuft im Host: je konfigurierter Quelle ihren Treiber. <c>EinmalImCluster</c> (Default) läuft nur auf dem Knoten, der die Lease
/// der Cluster-Identität <c>quelle/&lt;Typ&gt;</c> hält; <c>JederKnoten</c> überall. Wirft ein Treiber (z. B. Log nicht erreichbar),
/// startet er nach einer Pause neu — seine Nachrichten sind idempotent.
/// </summary>
public sealed class QuellenDienst(IServiceProvider sp, IEnumerable<KonfigurierteQuelle> quellen, QuellenZustand zustand,
    ILogger<QuellenDienst>? logger = null) : BackgroundService
{
    private static readonly TimeSpan Verlaengern = TimeSpan.FromSeconds(5);

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var liste = quellen.ToList();
        if (liste.Count == 0) return Task.CompletedTask;
        var eingang = QuellEingang.Live(sp);
        foreach (var q in liste)
            Console.WriteLine($"  ⛲ Quelle {q.Typ.Abschnitt} ({q.Typ.Treiber}, {q.Konfig.Laufmodus}) {q.Konfig.Pfad} → "
                + string.Join(", ", eingang.PipelinesFür(q.Typ.Typ).DefaultIfEmpty("(keine Pipeline)")));
        return Task.WhenAll(liste.Select(q => q.Konfig.Laufmodus == QuellLaufmodus.JederKnoten
            ? LaufeAsync(q, eingang, stoppingToken)
            : MitLeaseAsync(q, eingang, stoppingToken)));
    }

    /// <summary>Hält dieser Knoten die Lease, läuft der Treiber; verliert er sie, wird er gestoppt.</summary>
    private async Task MitLeaseAsync(KonfigurierteQuelle q, QuellEingang eingang, CancellationToken ct)
    {
        var system = sp.GetRequiredService<ActorSystem>();
        var knoten = system.Id;
        CancellationTokenSource? lauf = null;
        Task? treiber = null;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                var haelt = false;
                try
                {
                    using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    cts.CancelAfter(TimeSpan.FromSeconds(5));
                    var antwort = await system.Cluster().RequestAsync<QuelleGehalten>(QuellenHalterActor.Identitaet(q.Typ), new HalteQuelle(knoten), cts.Token);
                    haelt = antwort?.Haelt == true;
                }
                catch (Exception ex) when (ex is OperationCanceledException or TimeoutException && !ct.IsCancellationRequested)
                { /* Cluster gerade nicht erreichbar: lieber anhalten als doppelt laufen */ }

                if (haelt && treiber == null)
                {
                    lauf = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    treiber = LaufeAsync(q, eingang, lauf.Token);
                }
                else if (!haelt && treiber != null)
                {
                    lauf!.Cancel();
                    try { await treiber; } catch (OperationCanceledException) { }
                    lauf.Dispose();
                    (lauf, treiber) = (null, null);
                }
                try { await Task.Delay(Verlaengern, ct); } catch (OperationCanceledException) { break; }
            }
        }
        finally
        {
            lauf?.Cancel();
            if (treiber != null) try { await treiber; } catch (OperationCanceledException) { }
            lauf?.Dispose();
        }
    }

    private async Task LaufeAsync(KonfigurierteQuelle q, QuellEingang eingang, CancellationToken ct)
    {
        var bestand = sp.GetRequiredService<IStreamBestand>();
        while (!ct.IsCancellationRequested)
        {
            try
            {
                var treiber = new DateiTreiber(q.Typ, q.Konfig, (n, c) => eingang.NimmAufAsync(n, c),
                    bestand.BekannteAsync, zustand, logger: logger);
                await treiber.LaufeAsync(ct);
                return;
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                logger?.LogWarning(ex, "Quelle {Quelle} fehlgeschlagen — Neustart in 5 s", q.Typ.Abschnitt);
                try { await Task.Delay(TimeSpan.FromSeconds(5), ct); } catch (OperationCanceledException) { return; }
            }
        }
    }
}

public static partial class QuellenExtensions
{
    /// <summary>
    /// Bindet die Quellen aus der generierten Tabelle (<c>GeneratedQuellen.Alle</c>) an ihre Konfig (Abschnitt <c>Quellen:&lt;Typ&gt;</c>).
    /// Start-Prüfungen (docs/konzept-werkbank.md §3.4): jede Quelle braucht einen Abschnitt; ein Abschnitt ohne Typ und unbekannte
    /// Felder sind Fehler; ein beim Start nicht erreichbares Verzeichnis ist nur eine Warnung (der Treiber versucht es weiter).
    /// </summary>
    public static IServiceCollection AddQuellen(this IServiceCollection services, IConfiguration konfig)
        => services.AddQuellen(konfig, GeneratedQuellen.Alle);

    public static IServiceCollection AddQuellen(this IServiceCollection services, IConfiguration konfig, IReadOnlyList<QuellTyp> typen)
    {
        var abschnitt = konfig.GetSection(Treibervertrag.KonfigAbschnitt);
        var bekannt = typen.Select(t => t.Abschnitt).ToHashSet(StringComparer.Ordinal);
        var fremd = abschnitt.GetChildren().Select(c => c.Key).Where(k => !bekannt.Contains(k)).ToList();
        if (fremd.Count > 0)
            throw new InvalidOperationException($"Konfig {Treibervertrag.KonfigAbschnitt}: Abschnitt ohne Quellen-Typ: {string.Join(", ", fremd)} "
                + $"(bekannt: {string.Join(", ", bekannt.DefaultIfEmpty("—"))}).");
        var fehlend = typen.Where(t => !abschnitt.GetSection(t.Abschnitt).Exists()).Select(t => t.Abschnitt).ToList();
        if (fehlend.Count > 0)
            throw new InvalidOperationException($"Konfig {Treibervertrag.KonfigAbschnitt}: Quelle ohne Abschnitt: {string.Join(", ", fehlend)}.");

        foreach (var t in typen)
        {
            var k = abschnitt.GetSection(t.Abschnitt).Get<DateiQuellKonfig>(o => o.ErrorOnUnknownConfiguration = true)
                ?? throw new InvalidOperationException($"Konfig {Treibervertrag.KonfigAbschnitt}:{t.Abschnitt} ist leer.");
            if (string.IsNullOrWhiteSpace(k.Pfad))
                throw new InvalidOperationException($"Konfig {Treibervertrag.KonfigAbschnitt}:{t.Abschnitt}:Pfad fehlt.");
            if (!Directory.Exists(k.Pfad))
                Console.WriteLine($"  ⚠ Quelle {t.Abschnitt}: Verzeichnis {k.Pfad} beim Start nicht erreichbar — der Treiber versucht es weiter.");
            services.AddSingleton(new KonfigurierteQuelle(t, k));
        }

        services.AddSingleton<QuellenZustand>();
        services.AddSingleton<IStreamBestand>(sp => new Infrastructure.Persistence.MartenStreamBestand(sp.GetRequiredService<Marten.IDocumentStore>()));
        services.AddSingleton<IClusterKindContributor, QuellenHalterKind>();
        services.AddHealthChecks().AddCheck<QuellenHealthCheck>("quellen");
        services.AddHostedService<QuellenDienst>();
        return services;
    }
}
