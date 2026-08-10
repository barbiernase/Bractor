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
    private sealed class FakeOffenIndex : IProzessOffenIndex
    {
        private readonly List<Guid> _offen;
        public FakeOffenIndex(int anzahl)
        {
            _offen = new List<Guid>();
            for (var i = 0; i < anzahl; i++) _offen.Add(Guid.NewGuid());
        }
        public Task MarkiereOffenAsync(Guid k, string p, CancellationToken ct) => Task.CompletedTask;
        public Task MarkiereBeendetAsync(Guid k, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<Guid>> ListeOffeneAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Guid>>(_offen);
    }

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

    private static async Task<HealthStatus> Status(IProzessOffenIndex offen, IDeadLetterReadStore dlq)
    {
        var check = new BackendHealthCheck(new BackendMetrics(offen, dlq));
        var result = await check.CheckHealthAsync(new HealthCheckContext(), default);
        return result.Status;
    }

    [Fact]
    public async Task Erfasst_offene_Prozesse_und_DLQ_Zahl()
    {
        var m = await new BackendMetrics(new FakeOffenIndex(3), new FakeDlq(2)).ErfasseAsync();
        m.OffeneProzesse.Should().Be(3);
        m.DlqEinträge.Should().Be(2);
    }

    [Fact]
    public async Task Leere_DLQ_ist_Healthy()
        => (await Status(new FakeOffenIndex(5), new FakeDlq(0))).Should().Be(HealthStatus.Healthy);

    [Fact]
    public async Task Nicht_leere_DLQ_ist_Degraded_aber_nicht_Unhealthy()
        => (await Status(new FakeOffenIndex(0), new FakeDlq(4))).Should().Be(HealthStatus.Degraded);

    [Fact]
    public async Task Ein_werfender_Store_ist_Unhealthy()
        => (await Status(new FakeOffenIndex(0), new FakeDlq(0, wirft: true))).Should().Be(HealthStatus.Unhealthy);
}
