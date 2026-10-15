using Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Pruefdomaene.Auftrag;
using Pruefdomaene.Kontingent;
using Pruefdomaene.Posten;
using static Pruefdomaene.Integration.Tests.Pruefstand;

namespace Pruefdomaene.Integration.Tests.Garantien;

/// <summary>
/// Garantien der durablen Konsumenten im echten Cluster (docs/11 §11.6, §11.7): die Reaktion <c>NachschubBeiMindestbestand</c> wirkt genau
/// einmal, obwohl sie at-least-once ausgibt (der Poll eines Emittenten gibt heute alles erneut aus); ein Vorgang, dessen Host mitten im
/// Lauf neu startet, wird zu Ende geführt und wirkt jeden Schritt genau einmal.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class AblaufGarantieTests
{
    private static readonly TimeSpan Frist = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task Reaktion_wirkt_genau_einmal_auch_wenn_ihr_Poll_alles_erneut_ausgibt()
    {
        using var host = Cluster("garantie-reaktion");
        await host.StartAsync();
        try
        {
            var store = host.Services.GetRequiredService<IEventStoreRepository>();
            var p = Guid.NewGuid();
            (await FrageAsync(host, new EroeffnePosten(p, 100, Mindestbestand: 98), Guid.NewGuid()))!.Success.Should().BeTrue();
            (await FrageAsync(host, new ReservierePosten(p, 5), Guid.NewGuid()))!.Success.Should().BeTrue();   // Fehlmenge 3

            async Task<int> Nachschuebe() => (await store.ReadStreamAsync(p, 0, default)).Count(e => e.Payload is PostenZugebucht);
            await Warte(async () => await Nachschuebe() > 0, Frist, "Die Reaktion hat nicht nachgebucht.");
            // Mindestens ein Poll-Durchlauf (30 s) weckt den Posten-Stream erneut und gibt den Nachschub noch einmal aus.
            await Task.Delay(TimeSpan.FromSeconds(40));

            (await Nachschuebe()).Should().Be(1, "dieselbe Ursache ergibt dieselbe CommandId — die Inbox des Postens verwirft die Wiederholung");
            (await Zustand<Posten.Posten>(host.Services, p)).Bestand.Should().Be(103);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Ein_Neustart_des_Hosts_mitten_im_Vorgang_fuehrt_ihn_zu_Ende()
    {
        // Ein eigenes, frisches Schema: der Neustart soll nur den verlorenen Start-Weckruf heilen müssen — nicht die Altlasten anderer Tests
        // (ein Tombstone im geteilten Schema legt den Poll still, G-7, eigener Test in GarantiePostgresTests).
        var schema = "pruef_neustart_" + Guid.NewGuid().ToString("N")[..8];
        Guid posten = Guid.NewGuid(), kontingent = Guid.NewGuid(), auftrag = Guid.NewGuid();
        using (var erster = Cluster("garantie-neustart-1", schema: schema))
        {
            await erster.StartAsync();
            (await FrageAsync(erster, new EroeffnePosten(posten, 10), Guid.NewGuid()))!.Success.Should().BeTrue();
            (await FrageAsync(erster, new RichteKontingentEin(kontingent, 10), Guid.NewGuid()))!.Success.Should().BeTrue();
            (await FrageAsync(erster, new StarteAuftrag(auftrag, posten, kontingent, 3), Guid.NewGuid()))!.Success.Should().BeTrue();
            await erster.StopAsync();   // sofort danach: Signale, Weckungen und abgesetzte Sends im Flug gehen mit dem Host unter
        }

        using var zweiter = Cluster("garantie-neustart-2", schema: schema);
        await zweiter.StartAsync();
        try
        {
            var store = zweiter.Services.GetRequiredService<IEventStoreRepository>();
            var korr = ProzessId.Für(nameof(AuftragsProzess), auftrag, 1);
            await Warte(() => Beendet(store, korr, erfolg: true), Frist, "Der Vorgang wurde nach dem Neustart nicht zu Ende geführt.");
            (await store.ReadStreamAsync(posten, 0, default)).Count(e => e.Payload is PostenReserviert).Should().Be(1);
            (await store.ReadStreamAsync(kontingent, 0, default)).Count(e => e.Payload is KontingentBelegt).Should().Be(1);
            (await store.ReadStreamAsync(auftrag, 0, default)).Count(e => e.Payload is AuftragBestaetigt).Should().Be(1);
            (await store.ReadStreamAsync(korr, 0, default)).Count(e => e.Payload is Infrastructure.Prozess.ProzessBeendet).Should().Be(1);
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
