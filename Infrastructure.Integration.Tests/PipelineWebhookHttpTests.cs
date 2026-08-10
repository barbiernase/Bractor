using System.Net;
using System.Net.Http.Json;
using Abstractions;
using Domain.Pipeline.Benchmark;
using FluentAssertions;
using Infrastructure.Pipeline;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;

namespace Infrastructure.Integration.Tests;

/// <summary>
/// Ebene 2 — die REALE HTTP-Kante des Webhook-Triggers (das Stück, das der deterministische Prüfstand nicht
/// deckt). Eine minimale <see cref="WebApplication"/> auf echtem Kestrel (Zufalls-Port) mappt den Webhook mit
/// einem FAKE Send-Seam (kein Cluster nötig) → ein echter <see cref="HttpClient"/>-POST beweist: Routing +
/// JSON-Body-Bindung + Endpoint-Ausführung + Trigger-Bau + 202 Accepted.
///
/// Braucht KEIN Postgres/Consul/Redis — nur den HTTP-Stack; darum schnell und nicht flaky.
/// </summary>
public class PipelineWebhookHttpTests
{
    [Fact]
    public async Task POST_baut_den_Trigger_aus_dem_Body_und_stellt_ihn_zu()
    {
        IPipelineTrigger? gesendet = null;

        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");   // Zufalls-Port
        builder.Logging.ClearProviders();
        var app = builder.Build();

        app.MapPipelineWebhook<BenchPing>(
            "/webhook/bench",
            baueTrigger: r => r,                          // BenchPing IST der Trigger
            sendeFactory: _ => (t => { gesendet = t; return Task.CompletedTask; }));

        await app.StartAsync();
        try
        {
            var adresse = app.Urls.First();
            using var client = new HttpClient { BaseAddress = new Uri(adresse) };

            var antwort = await client.PostAsJsonAsync("/webhook/bench", new BenchPing(42));

            antwort.StatusCode.Should().Be(HttpStatusCode.Accepted, "at-least-once: der Empfang ist quittiert");
            gesendet.Should().BeOfType<BenchPing>()
                .Which.Seq.Should().Be(42, "der Trigger wurde aus dem JSON-Body gebaut und zugestellt");
        }
        finally
        {
            await app.StopAsync();
        }
    }
}
