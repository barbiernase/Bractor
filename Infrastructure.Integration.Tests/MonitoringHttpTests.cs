using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Abstractions;
using FluentAssertions;
using Infrastructure.Monitoring;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Ebene 2 — die REALE HTTP-Kante der Monitoring-Scheibe 1 (HealthCheck + Zähler). Eine minimale
/// <see cref="WebApplication"/> auf echtem Kestrel mit FAKE-Stores (kein Postgres nötig) beweist die volle
/// beobachtbare Fläche: die HealthCheck-Middleware, der Status (Healthy/Degraded), der JSON-ResponseWriter
/// und der <c>/monitoring/metrics</c>-Endpoint mit den echten Kennzahlen.
/// </summary>
public class MonitoringHttpTests
{
    private sealed class FakeOffenIndex(int anzahl) : IProzessOffenIndex
    {
        private readonly List<Guid> _offen = Enumerable.Range(0, anzahl).Select(_ => Guid.NewGuid()).ToList();
        public Task MarkiereOffenAsync(Guid k, string p, CancellationToken ct) => Task.CompletedTask;
        public Task MarkiereBeendetAsync(Guid k, CancellationToken ct) => Task.CompletedTask;
        public Task<IReadOnlyList<Guid>> ListeOffeneAsync(CancellationToken ct)
            => Task.FromResult<IReadOnlyList<Guid>>(_offen);
    }

    private sealed class FakeDlq(long count) : IDeadLetterReadStore
    {
        public Task<long> CountAsync(CancellationToken ct = default) => Task.FromResult(count);
        public Task<IReadOnlyList<DeadLetter>> ListAsync(int limit, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DeadLetter>>(Array.Empty<DeadLetter>());
        public Task<IReadOnlyList<DeadLetter>> ListByCorrelationAsync(string c, CancellationToken ct = default)
            => Task.FromResult<IReadOnlyList<DeadLetter>>(Array.Empty<DeadLetter>());
        public Task<DeadLetter?> GetAsync(Guid id, CancellationToken ct = default) => Task.FromResult<DeadLetter?>(null);
        public Task<bool> ResolveAsync(Guid id, CancellationToken ct = default) => Task.FromResult(false);
    }

    private static async Task<WebApplication> BooteAsync(int offeneProzesse, long dlq)
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        builder.Services.AddSingleton<IProzessOffenIndex>(new FakeOffenIndex(offeneProzesse));
        builder.Services.AddSingleton<IDeadLetterReadStore>(new FakeDlq(dlq));
        builder.Services.AddBackendMonitoring();
        var app = builder.Build();
        app.MapBackendMonitoring();
        await app.StartAsync();
        return app;
    }

    [Fact]
    public async Task Health_ist_Healthy_und_metrics_liefert_die_Zaehler()
    {
        var app = await BooteAsync(offeneProzesse: 2, dlq: 0);
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

            var health = await client.GetAsync("/health");
            health.StatusCode.Should().Be(HttpStatusCode.OK);
            var doc = JsonDocument.Parse(await health.Content.ReadAsStringAsync());
            doc.RootElement.GetProperty("status").GetString().Should().Be("Healthy");

            var metrik = await client.GetFromJsonAsync<BackendMetrik>("/monitoring/metrics");
            metrik!.OffeneProzesse.Should().Be(2);
            metrik.DlqEinträge.Should().Be(0);
        }
        finally { await app.StopAsync(); }
    }

    [Fact]
    public async Task Nicht_leere_DLQ_meldet_Degraded_und_metrics_zeigt_die_Zahl()
    {
        var app = await BooteAsync(offeneProzesse: 0, dlq: 3);
        try
        {
            using var client = new HttpClient { BaseAddress = new Uri(app.Urls.First()) };

            var health = await client.GetAsync("/health");
            health.StatusCode.Should().Be(HttpStatusCode.OK, "Degraded ist noch 200 — das Backend läuft");
            var doc = JsonDocument.Parse(await health.Content.ReadAsStringAsync());
            doc.RootElement.GetProperty("status").GetString().Should().Be("Degraded");

            var metrik = await client.GetFromJsonAsync<BackendMetrik>("/monitoring/metrics");
            metrik!.DlqEinträge.Should().Be(3);
        }
        finally { await app.StopAsync(); }
    }
}
