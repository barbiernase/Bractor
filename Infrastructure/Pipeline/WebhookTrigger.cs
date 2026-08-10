using Abstractions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Pipeline;

/// <summary>
/// Webhook-Trigger-Ingress (Zielbild §6): ein HTTP-POST feuert einen konfigurierten
/// <see cref="IPipelineTrigger"/> an die Ziel-Pipeline. Wie der Timer bewusst PUSH — ein Webhook ist KEIN
/// Log-Event (kein Cursor/Fold, verlierbar per Invariante 2/6); ein verpasster POST heilt der nächste.
///
/// Host-GLUE, kein Framework-Kern: die generische Endpoint-Verdrahtung liegt hier (damit sie testbar ist),
/// die DOMÄNEN-Wahl (welche Route baut welchen Trigger) trifft der Host (<c>Program.cs</c>) beim Mappen.
/// Der Kern (<see cref="VerarbeiteAsync"/>) ist von ASP.NET UND Cluster entkoppelt → deterministisch prüfbar.
/// </summary>
public static class WebhookTrigger
{
    /// <summary>
    /// Ein Webhook-Aufruf: aus dem Request einen Trigger bauen und über den Send-Seam zustellen. Rein —
    /// kein HTTP, kein Actor, kein Cluster.
    /// </summary>
    public static async Task VerarbeiteAsync<TRequest>(
        TRequest request,
        Func<TRequest, IPipelineTrigger> baueTrigger,
        Func<IPipelineTrigger, Task> sende)
    {
        var trigger = baueTrigger(request);
        await sende(trigger);
    }

    /// <summary>
    /// Mappt <c>POST {route}</c> → baut aus dem JSON-Body einen Trigger → sendet ihn an die Pipeline. Live geht
    /// der Send über <see cref="PipelineTriggerSender"/> (bounded/W2, Cluster aus DI); im Test injizierbar über
    /// <paramref name="sendeFactory"/>. Antwortet <c>202 Accepted</c> (at-least-once: der Empfang ist quittiert,
    /// die Wirkung heilt der Re-Trigger).
    /// </summary>
    public static IEndpointRouteBuilder MapPipelineWebhook<TRequest>(
        this IEndpointRouteBuilder endpoints,
        string route,
        Func<TRequest, IPipelineTrigger> baueTrigger,
        Func<IServiceProvider, Func<IPipelineTrigger, Task>>? sendeFactory = null)
    {
        endpoints.MapPost(route, async (TRequest request, IServiceProvider sp) =>
        {
            var sende = sendeFactory?.Invoke(sp) ?? StandardSende(sp);
            await VerarbeiteAsync(request, baueTrigger, sende);
            return Results.Accepted();
        });
        return endpoints;
    }

    /// <summary>Der Live-Send-Seam: bounded Pipeline-Trigger-Zustellung über den Cluster aus dem DI-ActorSystem.</summary>
    private static Func<IPipelineTrigger, Task> StandardSende(IServiceProvider sp)
    {
        var cluster = sp.GetRequiredService<ActorSystem>().Cluster();
        var logger = sp.GetService<ILoggerFactory>()?.CreateLogger("PipelineWebhook");
        return trigger => PipelineTriggerSender.SendAsync(cluster, trigger, logger);
    }
}
