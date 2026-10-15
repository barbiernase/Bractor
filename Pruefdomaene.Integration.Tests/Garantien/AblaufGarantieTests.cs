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
/// einmal über Signal und Poll (beide lesen ab ihrem Cursor) und auch, wenn sie zwischen Emit und Cursor abstürzt (dann sendet sie erneut,
/// die Inbox verwirft es); ein Vorgang, dessen Host mitten im Lauf neu startet, wird zu Ende geführt und wirkt jeden Schritt genau einmal.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class AblaufGarantieTests
{
    private static readonly TimeSpan Frist = TimeSpan.FromSeconds(90);

    [Fact]
    public async Task Reaktion_wirkt_genau_einmal_ueber_Signal_und_Poll()
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
            // Mindestens ein Poll-Durchlauf (30 s) weckt den Posten-Stream erneut; er liest ab dem Cursor und gibt nichts erneut aus.
            await Task.Delay(TimeSpan.FromSeconds(40));

            (await Nachschuebe()).Should().Be(1, "dieselbe Ursache ergibt dieselbe CommandId — die Inbox des Postens verwirft die Wiederholung");
            (await Zustand<Posten.Posten>(host.Services, p)).Bestand.Should().Be(103);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Absturz_zwischen_Emit_und_Cursor_die_Reaktion_sendet_erneut_und_wirkt_genau_einmal()
    {
        // Der Cursor scheitert beim ersten Vorrücken hinter die Reservierung (v2): der Nachschub ist da schon zugestellt. Die nächste Weckung
        // (Poll) liest ab dem alten Cursor, gibt denselben Command mit derselben CommandId erneut aus — die Inbox des Postens verwirft ihn.
        var abgestuerzt = 0;
        var p = Guid.NewGuid();
        var partition = $"nachschub-bei-mindestbestand:{p}";
        using var host = Cluster("garantie-emit-cursor", dienste: s => Dekoriere<IEmittentenCursor>(s,
            (_, innen) => new AbsturzCursor(innen, (teil, bis) => teil == partition && bis >= 3 && Interlocked.Exchange(ref abgestuerzt, 1) == 0)));
        await host.StartAsync();
        try
        {
            var store = host.Services.GetRequiredService<IEventStoreRepository>();
            var cursor = host.Services.GetRequiredService<IEmittentenCursor>();
            (await FrageAsync(host, new EroeffnePosten(p, 100, Mindestbestand: 98), Guid.NewGuid()))!.Success.Should().BeTrue();
            (await FrageAsync(host, new ReservierePosten(p, 5), Guid.NewGuid()))!.Success.Should().BeTrue();   // Fehlmenge 3

            async Task<int> Nachschuebe() => (await store.ReadStreamAsync(p, 0, default)).Count(e => e.Payload is PostenZugebucht);
            await Warte(async () => await Nachschuebe() > 0, Frist, "Die Reaktion hat nicht nachgebucht.");
            await Warte(async () => await cursor.LadeAsync(partition, default) >= 3, Frist,
                "Der Cursor ist nach dem Absturz nicht vorgerückt.");

            abgestuerzt.Should().Be(1, "der Absturz zwischen Emit und Cursor ist eingetreten");
            (await Nachschuebe()).Should().Be(1, "dieselbe Ursache ergibt dieselbe CommandId — die Inbox des Postens verwirft die Wiederholung");
            (await Zustand<Posten.Posten>(host.Services, p)).Bestand.Should().Be(103);
        }
        finally { await host.StopAsync(); }
    }

    /// <summary>Ein Cursor, der einmal beim Vorrücken „abstürzt“ — der Emit ist schon draußen, der Fortschritt nicht geschrieben.</summary>
    private sealed class AbsturzCursor(IEmittentenCursor innen, Func<string, long, bool> stuerzt) : IEmittentenCursor
    {
        public Task<long> LadeAsync(string partition, CancellationToken ct) => innen.LadeAsync(partition, ct);
        public Task SchreibeAsync(string partition, long bis, CancellationToken ct)
            => stuerzt(partition, bis) ? throw new InvalidOperationException("Absturz zwischen Emit und Cursor") : innen.SchreibeAsync(partition, bis, ct);
    }

    [Fact]
    public async Task Ein_Neustart_des_Hosts_mitten_im_Vorgang_fuehrt_ihn_zu_Ende()
    {
        // Ein eigenes, frisches Schema: der Neustart soll nur den verlorenen Start-Weckruf heilen müssen, ohne die Hochwassermarke des Polls
        // über die Altlasten anderer Tests im geteilten Schema schieben zu müssen.
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
