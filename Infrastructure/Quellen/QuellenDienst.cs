using Abstractions;
using Infrastructure.Lebenszyklus;
using Infrastructure.Projections;   // IClusterKindContributor
using Infrastructure.Quellen.Treiber.Datei;
using Infrastructure.Quellen.Treiber.Takt;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Infrastructure.Komposition;

namespace Infrastructure.Quellen;

/// <summary>Eine konfigurierte Quelle: Typ aus der generierten Tabelle + Werte aus der Konfig (die Konfig ihres Treibers).</summary>
public sealed record KonfigurierteQuelle(QuellTyp Typ, QuellKonfig Konfig);

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
            Console.WriteLine($"  ⛲ Quelle {q.Typ.Abschnitt} ({q.Typ.Treiber}, {q.Konfig.Laufmodus}) "
                + (q.Konfig is DateiQuellKonfig d ? d.Pfad : q.Konfig is TaktQuellKonfig t ? $"alle {t.Intervall}" : "") + " → "
                + string.Join(", ", Infrastructure.Komposition.Anwendung.Funktionen.Bausteine.Where(b => b.Eingang(q.Typ.Typ) is not null)
                    .Select(b => b.Name).DefaultIfEmpty("(keine Funktion hört sie)")));
        return Task.WhenAll(liste.Select(q => q.Konfig.Laufmodus == QuellLaufmodus.JederKnoten
            ? LaufeAsync(q, eingang, stoppingToken)
            : MitLeaseAsync(q, eingang, stoppingToken)));
    }

    /// <summary>Hält dieser Knoten die Lease, läuft der Treiber; verliert er sie, wird er gestoppt.</summary>
    private async Task MitLeaseAsync(KonfigurierteQuelle q, QuellEingang eingang, CancellationToken ct)
    {
        var system = sp.GetRequiredService<ActorSystem>();
        var knoten = system.Id;
        var bote = new Bote(system.Cluster());
        CancellationTokenSource? lauf = null;
        Task? treiber = null;
        try
        {
            while (!ct.IsCancellationRequested)
            {
                // Ohne Antwort (Cluster gerade nicht erreichbar): lieber anhalten als doppelt laufen.
                var antwort = await bote.FrageAsync<QuelleGehalten>(QuellenHalterActor.Identitaet(q.Typ), new HalteQuelle(knoten), TimeSpan.FromSeconds(5), ct);
                var haelt = antwort?.Haelt == true;

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
                switch (q.Konfig)
                {
                    case DateiQuellKonfig datei:
                        await new DateiTreiber(q.Typ, datei, (n, c) => eingang.NimmAufAsync(n, c, QuellVorgang.Schluessel(n, datei)), bestand.BekannteAsync, zustand, logger: logger)
                            .LaufeAsync(ct);
                        return;
                    case TaktQuellKonfig takt:
                        var uhr = sp.GetRequiredService<IDbClock>();
                        await new TaktTreiber(q.Typ, takt, (n, c) => eingang.NimmAufAsync(n, c, QuellVorgang.Schluessel(n, takt)), c => uhr.JetztAsync(c), zustand, logger)
                            .LaufeAsync(ct);
                        return;
                    default:
                        throw new NotSupportedException($"Quelle {q.Typ.Abschnitt}: kein Treiber für {q.Konfig.GetType().Name}.");
                }
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
        => services.AddQuellen(konfig, Anwendung.Quellen);

    public static IServiceCollection AddQuellen(this IServiceCollection services, IConfiguration konfig, IReadOnlyList<QuellTyp> typen)
    {
        var abschnitt = konfig.GetSection(Treibervertrag.KonfigAbschnitt);
        var bekannt = typen.Select(t => t.Abschnitt).ToHashSet(StringComparer.Ordinal);
        var fremd = abschnitt.GetChildren().Select(c => c.Key).Where(k => !bekannt.Contains(k)).ToList();
        if (fremd.Count > 0)
            throw new Infrastructure.Umgebung.BootFehler($"Konfig {Treibervertrag.KonfigAbschnitt}: Abschnitt ohne Quellen-Typ: {string.Join(", ", fremd)} "
                + $"(bekannt: {string.Join(", ", bekannt.DefaultIfEmpty("—"))}).", "host:Quellen");
        var fehlend = typen.Where(t => !abschnitt.GetSection(t.Abschnitt).Exists()).Select(t => t.Abschnitt).ToList();
        if (fehlend.Count > 0)
            throw new Infrastructure.Umgebung.BootFehler($"Konfig {Treibervertrag.KonfigAbschnitt}: Quelle ohne Abschnitt: {string.Join(", ", fehlend)}.", "quelle:" + fehlend[0]);

        foreach (var t in typen)
        {
            // Die Konfig des Treibers (der Basistyp der Quelle wählt ihn, nie ein Name aus der Konfig).
            QuellKonfig? k;
            try
            {
                k = t.Treiber == Treibervertrag.Takt
                    ? abschnitt.GetSection(t.Abschnitt).Get<TaktQuellKonfig>(o => o.ErrorOnUnknownConfiguration = true)
                    : abschnitt.GetSection(t.Abschnitt).Get<DateiQuellKonfig>(o => o.ErrorOnUnknownConfiguration = true);
            }
            catch (InvalidOperationException e) { throw new Infrastructure.Umgebung.BootFehler($"Konfig {Treibervertrag.KonfigAbschnitt}:{t.Abschnitt}: {e.Message}", "quelle:" + t.Abschnitt); }
            if (k == null) throw new Infrastructure.Umgebung.BootFehler($"Konfig {Treibervertrag.KonfigAbschnitt}:{t.Abschnitt} ist leer.", "quelle:" + t.Abschnitt);
            if (k.Webhook is { Length: > 0 } w && !w.Trim().StartsWith('/'))
                throw new Infrastructure.Umgebung.BootFehler($"Konfig {Treibervertrag.KonfigAbschnitt}:{t.Abschnitt}:Webhook = '{w}' ist keine Route (beginnt mit /).", "quelle:" + t.Abschnitt);
            switch (k)
            {
                case DateiQuellKonfig d:
                    if (string.IsNullOrWhiteSpace(d.Pfad))
                        throw new Infrastructure.Umgebung.BootFehler($"Konfig {Treibervertrag.KonfigAbschnitt}:{t.Abschnitt}:Pfad fehlt.", "quelle:" + t.Abschnitt);
                    if (!Directory.Exists(d.Pfad))
                        Console.WriteLine($"  ⚠ Quelle {t.Abschnitt}: Verzeichnis {d.Pfad} beim Start nicht erreichbar — der Treiber versucht es weiter.");
                    break;
                case TaktQuellKonfig takt when takt.Intervall < TimeSpan.FromSeconds(1):
                    throw new Infrastructure.Umgebung.BootFehler($"Konfig {Treibervertrag.KonfigAbschnitt}:{t.Abschnitt}:Intervall = {takt.Intervall} — mindestens eine Sekunde.",
                        "quelle:" + t.Abschnitt);
            }
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
