using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Infrastructure.Monitoring;

/// <summary>
/// Verdrahtung der Monitoring-Scheibe 1 (HealthChecks + Prozess-/DLQ-Zähler). Host-Glue über
/// ASP.NET-HealthChecks + zwei schlanke Read-Endpoints; die Aggregation liegt in <see cref="BackendMetrics"/>.
/// </summary>
public static class MonitoringExtensions
{
    /// <summary>Registriert die Kennzahlen-Aggregation + den Backend-HealthCheck (Name „backend").</summary>
    public static IServiceCollection AddBackendMonitoring(this IServiceCollection services)
    {
        services.AddSingleton<IBackendMetrics, BackendMetrics>();
        services.AddHealthChecks().AddCheck<BackendHealthCheck>("backend");
        return services;
    }

    /// <summary>
    /// Mappt <c>GET /health</c> (JSON: Status + Kennzahlen aus dem HealthCheck-<c>data</c>) und
    /// <c>GET /monitoring/metrics</c> (die reine <see cref="BackendMetrik"/>). Beide reiner Read-Pfad.
    /// </summary>
    public static IEndpointRouteBuilder MapBackendMonitoring(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapHealthChecks("/health", new HealthCheckOptions { ResponseWriter = SchreibeHealthAsync });

        endpoints.MapGet("/monitoring/metrics", async (IBackendMetrics metrics, CancellationToken ct) =>
            Results.Ok(await metrics.ErfasseAsync(ct)));

        return endpoints;
    }

    /// <summary>Schreibt den HealthReport als JSON (Gesamtstatus + je Check Status/Beschreibung/data).</summary>
    private static Task SchreibeHealthAsync(HttpContext context, HealthReport report)
    {
        context.Response.ContentType = "application/json";
        var payload = new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description,
                data = e.Value.Data,
            }),
        };
        return context.Response.WriteAsync(JsonSerializer.Serialize(payload));
    }
}
