using Abstractions;
using FluentAssertions;
using Infrastructure.Pipeline;

namespace Infrastructure.Pruefstand.Pipeline;

/// <summary>
/// Ebene 1 (reiner Kontrollfluss, Fakes) — der entkoppelte Webhook-Kern
/// <see cref="WebhookTrigger.VerarbeiteAsync"/>: aus dem Request einen Trigger bauen und senden. Die HTTP-
/// Verdrahtung (Routing, JSON-Bindung, 202) deckt der real-hostende Integrationstest ab.
/// </summary>
public class WebhookTriggerTests
{
    private record WebhookBody(string Pfad, int Groesse);
    private record DateiTrigger(string Pfad, int Groesse) : IPipelineTrigger;

    [Fact]
    public async Task Der_Trigger_wird_aus_dem_Request_gebaut_und_gesendet()
    {
        var body = new WebhookBody("/in/bild.png", 2048);
        IPipelineTrigger? gesendet = null;

        await WebhookTrigger.VerarbeiteAsync(
            request: body,
            baueTrigger: r => new DateiTrigger(r.Pfad, r.Groesse),
            sende: t => { gesendet = t; return Task.CompletedTask; });

        gesendet.Should().BeOfType<DateiTrigger>()
            .Which.Should().BeEquivalentTo(new DateiTrigger("/in/bild.png", 2048),
                "der Send-Seam bekommt den aus dem Request gebauten Trigger");
    }
}
