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
// QUELLEN = fest codierte Treiber + Konfig (docs/konzept-werkbank.md §3). Eine Domäne benennt ihre Quelle nur als Typ
// (`sealed record DateiErkannt : DateiGemeldet;`); der Treiber (hier: Datei) liest, die Konfig der Umgebung (`Quellen:<Typ>`) sagt wie.
// Jede Nachricht wird das erste Event eines VORGANGS-Streams — Id deterministisch aus Typ + Kennung, geschrieben per StartStream.
// Dieselbe Nachricht zweimal (Neustart, zweiter Knoten, Re-Scan) startet den Vorgang also genau einmal. Danach wird jede Pipeline
// geweckt, die mit p.Quelle<T>() beginnt; geht die Weckung verloren, heilt der Poll des Korrelations-Routers.
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>Was ein Datei-Treiber über eine fertige Datei weiß — die Fabrik eines Quellen-Typs baut daraus die Nachricht.</summary>
public sealed record DateiDaten(string Name, string Pfad, string RelativerPfad, long Groesse, DateTimeOffset GeaendertAm,
    DateTimeOffset ErkanntAm, string Kennung);

/// <summary>
/// Ein Quellen-Typ aus der generierten Tabelle <c>GeneratedQuellen.Alle</c>: Typ, Treiber, Konfig-Abschnitt (= Typname), Wirker-Id
/// und die reflexionsfreie Fabrik des Datei-Treibers.
/// </summary>
public sealed record QuellTyp(Type Typ, string Treiber, string Abschnitt, string WirkerId, Func<DateiDaten, IQuellNachricht> DateiFabrik);

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
            (korr, stream, version, name, ct) => WeckeAsync(system, korr, stream, version, name, ct),
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
            // Spur: eine Quell-Nachricht ist eine Wurzel (keine Ursache im Log), geschrieben von der Quelle.
            using var spur = Spur.Von(null, WirkerId.FuerQuelle(typ.Name));
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

    private static async Task WeckeAsync(ActorSystem system, Guid korrelation, Guid stream, int version, string name, CancellationToken ct)
    {
        var identity = ClusterIdentity.Create(korrelation.ToString(), ProzessManagerActor.KindName);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        try { await system.Cluster().RequestAsync<WakeAck>(identity, new ProzessWake(stream, version, name), cts.Token); }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        { /* der Poll des Korrelations-Routers startet den Vorgang nach */ }
    }
}

public static partial class QuellenExtensions
{
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
