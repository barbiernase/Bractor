using System.Security.Cryptography;
using System.Text;
using Abstractions;
using Infrastructure.Akteure;       // AkteurHerkunft
using Infrastructure.Projections;   // WakeAck
using Infrastructure.Prozess;       // ProzessManagerActor, ProzessWake
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Quellen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// QUELLEN (docs/konzept-editor-pipelines.md §14.5): eine Katalog-Quelle (IQuelle<T>) liefert Nachrichten; das Framework hängt
// jede als erstes Event eines VORGANGS-Streams an — Id deterministisch aus Typ + Kennung, geschrieben per StartStream. Dieselbe
// Nachricht zweimal (Neustart, zweiter Knoten, Re-Scan) startet den Vorgang also genau einmal. Danach wird jede Pipeline geweckt,
// die mit p.Quelle<T>() beginnt; geht die Weckung verloren, heilt der Poll des Korrelations-Routers (der Typ ist ein Auslöser).
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Eine gebundene Quelle: welcher Nachrichtentyp, wie sie läuft (aus DI aufgelöst, generisch gebunden — keine Reflection).</summary>
public sealed record QuellenBindung(Type Nachricht, string Name, Func<IServiceProvider, CancellationToken, IAsyncEnumerable<IQuellNachricht>> Laufe);

/// <summary>Deterministische Vorgangs-Ids der Quellen.</summary>
public static class QuellVorgang
{
    /// <summary>Stream-Id des Vorgangs einer Quell-Nachricht: aus Typ und Kennung (gleiche Nachricht → gleicher Vorgang).</summary>
    public static Guid StreamFür(Type nachricht, string kennung)
    {
        Span<byte> hash = stackalloc byte[16];
        MD5.HashData(Encoding.UTF8.GetBytes($"quelle:{nachricht.FullName}:{kennung}"), hash);
        return new Guid(hash);
    }
}

/// <summary>
/// Der transport-freie Kern: Nachricht genau einmal ins Log, dann die Pipelines wecken, die damit beginnen. Im Prüfstand mit
/// einem Log im Speicher beweisbar; live über <see cref="QuellenDienst"/> an Marten und den Cluster gebunden.
/// </summary>
public sealed class QuellEingang
{
    private readonly IEventStoreRepository _store;
    private readonly IReadOnlyDictionary<Type, IReadOnlyList<string>> _pipelinesJeTyp;
    private readonly Func<Guid, Guid, int, string, CancellationToken, Task> _wecke;
    private readonly Func<Type, string?> _akteur;

    /// <param name="registry">Die Regel-Registry (Prozesse und Pipeline-Flüsse) — daraus: welche beginnen mit welchem Typ.</param>
    /// <param name="wecke">(Korrelation, Stream, Version, Pipeline-Name) → Start-Weckung des Dirigenten.</param>
    /// <param name="akteur">Der Akteur, der diesen Nachrichtentyp liefert (IDarf), oder null.</param>
    public QuellEingang(IEventStoreRepository store, IReadOnlyDictionary<string, ProzessRegeln> registry,
        Func<Guid, Guid, int, string, CancellationToken, Task> wecke, Func<Type, string?>? akteur = null)
    {
        _store = store;
        _wecke = wecke;
        _akteur = akteur ?? (_ => null);
        _pipelinesJeTyp = registry
            .GroupBy(kv => kv.Value.AuslöserTyp)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<string>)g.Select(kv => kv.Key).OrderBy(n => n).ToList());
    }

    /// <summary>Der Eingang live: Marten-Log, Regel-Registry, Weckung über den Cluster, Akteur aus den IDarf-Befugnissen.</summary>
    public static QuellEingang Live(IServiceProvider sp)
    {
        var system = sp.GetRequiredService<ActorSystem>();
        return new QuellEingang(sp.GetRequiredService<IEventStoreRepository>(), sp.GetRequiredService<IReadOnlyDictionary<string, ProzessRegeln>>(),
            (korr, stream, version, name, ct) => QuellenDienst.WeckeAsync(system, korr, stream, version, name, ct),
            AkteurHerkunft.EindeutigerHalter);
    }

    /// <summary>Welche Pipelines eine Nachricht dieses Typs startet (leer = niemand hört zu, die Nachricht wird trotzdem protokolliert).</summary>
    public IReadOnlyList<string> PipelinesFür(Type nachricht)
        => _pipelinesJeTyp.TryGetValue(nachricht, out var p) ? p : Array.Empty<string>();

    /// <summary>Nimmt eine Quell-Nachricht auf: genau einmal ins Log, dann jede startende Pipeline wecken. Liefert den Vorgangs-Stream.</summary>
    public async Task<Guid> NimmAufAsync(IQuellNachricht nachricht, CancellationToken ct)
    {
        var typ = nachricht.GetType();
        var stream = QuellVorgang.StreamFür(typ, nachricht.Kennung);
        try
        {
            await _store.AppendEventsAsync(stream, 0, new IEvent[] { nachricht },
                correlationId: stream.ToString(), causationId: stream.ToString(),
                aggregateType: $"Quelle:{typ.Name}", akteur: _akteur(typ));
        }
        catch (Exception)
        {
            // Schon aufgenommen (Neustart, anderer Knoten)? Dann läuft der Vorgang bereits — die Weckung unten ist idempotent.
            if ((await _store.ReadStreamAsync(stream, 0, ct)).Count == 0) throw;
        }

        foreach (var name in PipelinesFür(typ))
            await _wecke(ProzessId.Für(name, stream, 1), stream, 1, name, ct);
        return stream;
    }
}

/// <summary>
/// Läuft im Host: jede gebundene Quelle in einer eigenen Schleife; jede Nachricht über den <see cref="QuellEingang"/>. Wirft eine
/// Quelle, wird sie nach einer Pause neu gestartet (ihre Nachrichten sind idempotent, ein erneutes Liefern schadet nicht).
/// Läuft auf jedem Knoten — die deterministische Vorgangs-Id macht das korrekt (genau ein StartStream gewinnt).
/// </summary>
public sealed class QuellenDienst : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly IEnumerable<QuellenBindung> _bindungen;
    private readonly ILogger<QuellenDienst>? _logger;

    public QuellenDienst(IServiceProvider sp, IEnumerable<QuellenBindung> bindungen, ILogger<QuellenDienst>? logger = null)
    {
        _sp = sp;
        _bindungen = bindungen;
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var bindungen = _bindungen.ToList();
        if (bindungen.Count == 0) return Task.CompletedTask;

        var eingang = QuellEingang.Live(_sp);

        foreach (var b in bindungen)
            Console.WriteLine($"  ⛲ Quelle {b.Name} → {string.Join(", ", eingang.PipelinesFür(b.Nachricht).DefaultIfEmpty("(keine Pipeline)"))}");
        return Task.WhenAll(bindungen.Select(b => LaufeAsync(b, eingang, stoppingToken)));
    }

    private async Task LaufeAsync(QuellenBindung b, QuellEingang eingang, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await foreach (var n in b.Laufe(_sp, ct).WithCancellation(ct))
                    await eingang.NimmAufAsync(n, ct);
                return;   // die Quelle ist zu Ende
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Quelle {Quelle} fehlgeschlagen — Neustart in 5 s", b.Name);
                try { await Task.Delay(TimeSpan.FromSeconds(5), ct); } catch (OperationCanceledException) { return; }
            }
        }
    }

    internal static async Task WeckeAsync(ActorSystem system, Guid korrelation, Guid stream, int version, string name, CancellationToken ct)
    {
        var identity = ClusterIdentity.Create(korrelation.ToString(), ProzessManagerActor.KindName);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        try { await system.Cluster().RequestAsync<WakeAck>(identity, new ProzessWake(stream, version, name), cts.Token); }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        { /* der Poll des Korrelations-Routers startet den Vorgang nach */ }
    }
}

public static class QuellenExtensions
{
    /// <summary>
    /// Bindet die Quelle <typeparamref name="TQuelle"/> für Nachrichten <typeparamref name="TNachricht"/> (Singleton; ihre
    /// Einstellungen bekommt sie über den Konstruktor aus DI). Pipelines, die mit <c>p.Quelle&lt;TNachricht&gt;()</c> beginnen,
    /// starten je Nachricht einen Vorgang.
    /// </summary>
    public static IServiceCollection AddQuelle<TNachricht, TQuelle>(this IServiceCollection services)
        where TNachricht : class, IQuellNachricht
        where TQuelle : class, IQuelle<TNachricht>
    {
        services.AddSingleton<TQuelle>();
        services.AddSingleton(new QuellenBindung(typeof(TNachricht), typeof(TQuelle).Name,
            (sp, ct) => sp.GetRequiredService<TQuelle>().LaufeAsync(ct)));
        if (!services.Any(d => d.ImplementationType == typeof(QuellenDienst)))
            services.AddHostedService<QuellenDienst>();
        return services;
    }

    /// <summary>
    /// Ein zweiter Weg in dieselbe Quelle: <c>POST {route}</c> mit der Nachricht als JSON → genau einmal ins Log, Vorgang gestartet.
    /// Antwortet <c>202 Accepted</c> mit der Vorgangs-Id (dieselbe Nachricht zweimal → derselbe Vorgang).
    /// <c>[Ingress]</c>: der Editor liest die Bindung (Nachricht = Typ-Argument, Route = <paramref name="route"/>) aus dem Aufruf.
    /// </summary>
    [Ingress(IngressArt.Webhook, Ort = nameof(route))]
    public static IEndpointRouteBuilder MapQuellWebhook<TNachricht>(this IEndpointRouteBuilder app, string route)
        where TNachricht : class, IQuellNachricht
    {
        app.MapPost(route, async (TNachricht nachricht, HttpContext http) =>
        {
            var vorgang = await QuellEingang.Live(http.RequestServices).NimmAufAsync(nachricht, http.RequestAborted);
            return Results.Accepted(value: new { vorgang });
        });
        return app;
    }
}
