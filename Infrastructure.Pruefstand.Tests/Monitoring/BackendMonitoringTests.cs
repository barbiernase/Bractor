using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Monitoring;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Infrastructure.Pruefstand.Monitoring;

/// <summary>
/// Ebene 1 — die Monitoring-Aggregation + die HealthCheck-Zustandsableitung (deterministisch, Fakes). Die
/// HTTP-Endpoints (/health, /monitoring/metrics) deckt der real-hostende Integrationstest ab.
/// </summary>
public class BackendMonitoringTests
{
    private sealed class FakeDlq : IDeadLetterReadStore
    {
        private readonly long _count;
        private readonly bool _wirft;
        public FakeDlq(long count, bool wirft = false) { _count = count; _wirft = wirft; }
        public Task<long> CountAsync(CancellationToken ct = default)
            => _wirft ? throw new InvalidOperationException("Store weg") : Task.FromResult(_count);
        public Task<IReadOnlyList<DeadLetter>> ListAsync(int limit, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DeadLetter>>(Array.Empty<DeadLetter>());
        public Task<IReadOnlyList<DeadLetter>> ListByCorrelationAsync(string c, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DeadLetter>>(Array.Empty<DeadLetter>());
        public Task<DeadLetter?> GetAsync(Guid id, CancellationToken ct = default)
            => Task.FromResult<DeadLetter?>(null);
        public Task<bool> ResolveAsync(Guid id, CancellationToken ct = default) => Task.FromResult(false);
    }

    private static async Task<HealthStatus> Status(IDeadLetterReadStore dlq)
    {
        var check = new BackendHealthCheck(new BackendMetrics(dlq));
        var result = await check.CheckHealthAsync(new HealthCheckContext(), default);
        return result.Status;
    }

    [Fact]
    public async Task Erfasst_die_DLQ_Zahl()
    {
        var m = await new BackendMetrics(new FakeDlq(2)).ErfasseAsync();
        m.DlqEinträge.Should().Be(2);
    }

    [Fact]
    public async Task Leere_DLQ_ist_Healthy()
        => (await Status(new FakeDlq(0))).Should().Be(HealthStatus.Healthy);

    [Fact]
    public async Task Nicht_leere_DLQ_ist_Degraded_aber_nicht_Unhealthy()
        => (await Status(new FakeDlq(4))).Should().Be(HealthStatus.Degraded);

    [Fact]
    public async Task Ein_werfender_Store_ist_Unhealthy()
        => (await Status(new FakeDlq(0, wirft: true))).Should().Be(HealthStatus.Unhealthy);
}
