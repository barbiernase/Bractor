using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Infrastructure.Monitoring;

/// <summary>
/// HealthCheck (Monitoring, Scheibe 1): erfasst die Backend-Kennzahlen und leitet daraus den Zustand ab.
/// Doppelt als Erreichbarkeits-Probe — die Erfassung berührt den durablen Offen-Index (Postgres) UND die DLQ
/// (Postgres); wirft eine Quelle, ist das Backend <c>Unhealthy</c>.
///
/// Semantik: eine nicht-leere DLQ ist <c>Degraded</c> (Ops-Aufmerksamkeit nötig — tote Commands liegen an),
/// aber NICHT <c>Unhealthy</c> — das Backend läuft und verarbeitet. Die Kennzahlen reisen als
/// <c>data</c> mit (offeneProzesse, dlq), damit ein Health-Endpoint sie direkt ausgibt.
/// </summary>
public sealed class BackendHealthCheck : IHealthCheck
{
    private readonly IBackendMetrics _metrics;

    public BackendHealthCheck(IBackendMetrics metrics) => _metrics = metrics;

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var m = await _metrics.ErfasseAsync(cancellationToken);
            var daten = new Dictionary<string, object>
            {
                ["offeneProzesse"] = m.OffeneProzesse,
                ["dlq"] = m.DlqEinträge,
            };

            return m.DlqEinträge > 0
                ? HealthCheckResult.Degraded($"{m.DlqEinträge} DLQ-Einträge liegen an", data: daten)
                : HealthCheckResult.Healthy("Backend erreichbar", daten);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Backend-Stores nicht erreichbar", ex);
        }
    }
}
