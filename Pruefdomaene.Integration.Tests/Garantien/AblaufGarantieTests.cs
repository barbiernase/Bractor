using Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Pruefdomaene.Auftrag;
using Pruefdomaene.Kontingent;
using Pruefdomaene.Posten;
using static Pruefdomaene.Integration.Tests.Pruefstand;

namespace Pruefdomaene.Integration.Tests.Garantien;

/// <summary>
/// Garantien der Abläufe im echten Cluster (docs/11 §11.6, §11.7): die Funktion <c>Nachschub</c> wirkt genau einmal über Signal und Takt (beide
/// lesen ab ihrer Position; den Absturz zwischen Commands und Position deckt <see cref="FunktionsGarantieTests"/>); ein Auftrag, dessen Host mitten
/// im Lauf neu startet, wird zu Ende geführt und wirkt jeden Schritt genau einmal — ohne eigene Prozess-Maschine: das Vorgangs-Aggregat wartet passiv,
/// liegen bleiben kann nur ein Funktions-Eingang, und den holen Position und Takt.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class AblaufGarantieTests
{
    private static readonly TimeSpan Frist = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task Nachschub_wirkt_genau_einmal_ueber_Signal_und_Takt()
    {
        using var host = Cluster("garantie-nachschub");
        await host.StartAsync();
        try
        {
            var store = host.Services.GetRequiredService<IEventStoreRepository>();
            var p = Guid.NewGuid();
            (await FrageAsync(host, new EroeffnePosten(new(p), 100, Mindestbestand: 98), Guid.NewGuid()))!.Success.Should().BeTrue();
            (await FrageAsync(host, new ReservierePosten(new(p), 5, Beleg.Ohne), Guid.NewGuid()))!.Success.Should().BeTrue();   // Fehlmenge 3

            async Task<int> Nachschuebe() => (await store.ReadStreamAsync(p, 0, default)).Count(e => e.Payload is PostenZugebucht);
            await Warte(async () => await Nachschuebe() > 0, Frist, "Die Funktion Nachschub hat nicht nachgebucht.");
            // Mindestens ein Takt (30 s) weckt die Partition erneut; sie liest ab ihrer Position und gibt nichts erneut aus.
            await Task.Delay(TimeSpan.FromSeconds(40));

            (await Nachschuebe()).Should().Be(1, "dieselbe Ursache ergibt dieselbe CommandId — die Inbox des Postens verwirft die Wiederholung");
            (await Zustand<Posten.Posten>(host.Services, p)).Bestand.Should().Be(103);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Ein_Neustart_des_Hosts_mitten_im_Auftrag_fuehrt_ihn_zu_Ende()
    {
        // Ein eigenes, frisches Schema: der zweite Host liest die Positionen der Funktionen ab dem Beginn dieses Schemas.
        var schema = "pruef_neustart_" + Guid.NewGuid().ToString("N")[..8];
        Guid posten = Guid.NewGuid(), kontingent = Guid.NewGuid(), auftrag = Guid.NewGuid();
        using (var erster = Cluster("garantie-neustart-1", schema: schema))
        {
            await erster.StartAsync();
            (await FrageAsync(erster, new EroeffnePosten(new(posten), 10), Guid.NewGuid()))!.Success.Should().BeTrue();
            (await FrageAsync(erster, new RichteKontingentEin(new(kontingent), 10), Guid.NewGuid()))!.Success.Should().BeTrue();
            (await FrageAsync(erster, new StarteAuftrag(new(auftrag), new(posten), new(kontingent), 3), Guid.NewGuid()))!.Success.Should().BeTrue();
            await erster.StopAsync();   // sofort danach: Signale, Weckungen und Sends im Flug gehen mit dem Host unter
        }

        using var zweiter = Cluster("garantie-neustart-2", schema: schema);
        await zweiter.StartAsync();
        try
        {
            var store = zweiter.Services.GetRequiredService<IEventStoreRepository>();
            await Warte(async () => (await Zustand<Auftrag.Auftrag>(zweiter.Services, auftrag)).Bestaetigt, Frist,
                "Der Auftrag wurde nach dem Neustart nicht zu Ende geführt.");
            await Task.Delay(TimeSpan.FromSeconds(2));
            (await store.ReadStreamAsync(posten, 0, default)).Count(e => e.Payload is PostenReserviert).Should().Be(1);
            (await store.ReadStreamAsync(kontingent, 0, default)).Count(e => e.Payload is KontingentBelegt).Should().Be(1);
            (await store.ReadStreamAsync(auftrag, 0, default)).Count(e => e.Payload is AuftragBestaetigt).Should().Be(1);
        }
        finally
        {
            await zweiter.StopAsync();
            await using var c = new Npgsql.NpgsqlConnection(Verbindung);
            await c.OpenAsync();
            await using var cmd = new Npgsql.NpgsqlCommand($"drop schema if exists {schema} cascade", c);
            await cmd.ExecuteNonQueryAsync();
        }
    }
}
