using System.Collections.Concurrent;
using Abstractions;
using FluentAssertions;
using Infrastructure.Eingaenge;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pruefdomaene.Posten;
using static Pruefdomaene.Integration.Tests.Pruefstand;

namespace Pruefdomaene.Integration.Tests.Garantien;

/// <summary>
/// Die Folge eines Decide (docs/03 §3.1, docs/04 §4.4) gegen echtes Marten, im echten Cluster: eine Reservierung, die den Posten unter seinen
/// Mindestbestand drückt, gibt neben dem Event die Folge <see cref="Unterschritten"/> aus. Je hörende Funktion legt die Entscheidung einen offenen
/// Eingang in derselben Transaktion; der Zusteller bringt ihn hin, „fertig“ trägt ihn aus, „angenommen“ bringt ihn nach der Frist erneut; ein
/// wiederholter Command legt keinen zweiten.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class FolgeGarantieTests
{
    private static readonly TimeSpan Frist = TimeSpan.FromSeconds(30);

    /// <summary>Eine Zustellung, die mitschreibt und nach Wunsch antwortet.</summary>
    private sealed class Probe(Func<OffenerEingang, Zustellausgang> ausgang) : IEingangsZustellung
    {
        public readonly ConcurrentQueue<OffenerEingang> Gesehen = new();
        public Task<Zustellausgang> StelleZuAsync(OffenerEingang e, CancellationToken ct)
        {
            Gesehen.Enqueue(e);
            return Task.FromResult(ausgang(e));
        }
    }

    private static Action<IServiceCollection> Mit(Probe probe, params string[] hoerer) => s =>
    {
        s.AddSingleton(new Folgenplan(new Dictionary<Type, string[]> { [typeof(Unterschritten)] = hoerer }));
        s.AddSingleton<IEingangsZustellung>(probe);
    };

    private static async Task<int> OffenAsync(IHost host, Guid stream)
    {
        var store = host.Services.GetRequiredService<Marten.IDocumentStore>();
        await using var conn = store.Storage.Database.CreateConnection();
        await conn.OpenAsync();
        await using var cmd = new Npgsql.NpgsqlCommand($"select count(*) from {OffeneEingaenge.Name(store)} where stream_id = $1", conn);
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = stream });
        return Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Eine_Folge_oeffnet_je_Hoerer_einen_Eingang_fertig_traegt_ihn_aus_ein_wiederholter_Command_keinen_zweiten()
    {
        var probe = new Probe(_ => Zustellausgang.Fertig);
        using var host = Cluster("folge-eingang", dienste: Mit(probe, "Probe", "Zweite"));
        await host.StartAsync();
        try
        {
            var p = Guid.NewGuid();
            (await FrageAsync(host, new EroeffnePosten(new(p), 100, Mindestbestand: 98), Guid.NewGuid()))!.Success.Should().BeTrue();
            var reservierung = Guid.NewGuid();
            (await FrageAsync(host, new ReservierePosten(new(p), 5, Beleg.Ohne), reservierung))!.Success.Should().BeTrue();   // Fehlmenge 3

            await Warte(() => Task.FromResult(probe.Gesehen.Count(e => e.Stream == p) == 2), Frist, "der Zusteller bringt die Folge nicht zu beiden Hörern");
            var folge = Eingangsvertrag.Folge(reservierung, 0);
            foreach (var funktion in new[] { "Probe", "Zweite" })
            {
                var e = probe.Gesehen.Single(x => x.Funktion == funktion);
                e.Kennung.Should().Be(Eingangsvertrag.Eingang(folge, funktion), "die Kennung ist abgeleitet: Command, Stelle, Funktion");
                e.Version.Should().Be(2, "Herkunft ist der Stand nach der Entscheidung");
                Nachrichtenform.Lies(e.Typ, e.Daten).Should().Be(new Unterschritten(new(p), 3));
            }
            await Warte(async () => await OffenAsync(host, p) == 0, Frist, "fertig trägt den Eingang nicht aus");

            (await FrageAsync(host, new ReservierePosten(new(p), 5, Beleg.Ohne), reservierung))!.Success.Should().BeTrue("eine Wiederholung");
            await Task.Delay(TimeSpan.FromSeconds(3));
            probe.Gesehen.Count(e => e.Stream == p).Should().Be(2, "ein wiederholter Command legt keinen zweiten Eingang");
            (await OffenAsync(host, p)).Should().Be(0);

            (await FrageAsync(host, new ReservierePosten(new(p), 1, Beleg.Ohne), Guid.NewGuid()))!.Success.Should().BeTrue();   // Fehlmenge 4
            await Warte(() => Task.FromResult(probe.Gesehen.Count(e => e.Stream == p) == 4), Frist, "eine neue Entscheidung öffnet neue Eingänge");
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Ohne_Hoerer_kein_Eingang_eine_Reservierung_ohne_Fehlmenge_keine_Folge()
    {
        var probe = new Probe(_ => Zustellausgang.Fertig);
        using var host = Cluster("folge-ohne-hoerer", dienste: s => s.AddSingleton<IEingangsZustellung>(probe));
        await host.StartAsync();
        try
        {
            var p = Guid.NewGuid();
            (await FrageAsync(host, new EroeffnePosten(new(p), 100, Mindestbestand: 98), Guid.NewGuid()))!.Success.Should().BeTrue();
            (await FrageAsync(host, new ReservierePosten(new(p), 5, Beleg.Ohne), Guid.NewGuid()))!.Success.Should().BeTrue();
            await Task.Delay(TimeSpan.FromSeconds(3));
            probe.Gesehen.Where(e => e.Stream == p).Should().BeEmpty("ohne Hörer legt die Folge keinen Eingang");   // offene Eingänge anderer Tests kommen hier auch an
            (await OffenAsync(host, p)).Should().Be(0);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Ein_angenommener_Eingang_ohne_fertig_bleibt_offen_und_kommt_nach_der_Frist_erneut()
    {
        // Die Frist des Zustellers ist 60 s — der Test prüft, dass ein angenommener Eingang offen bleibt und nicht sofort erneut kommt; das Wieder-
        //   kommen nach der Frist prüft der Prüfstand (ZustellwerkTests).
        var probe = new Probe(_ => Zustellausgang.Angenommen);
        using var host = Cluster("folge-angenommen", dienste: Mit(probe, "Probe"));
        await host.StartAsync();
        try
        {
            var p = Guid.NewGuid();
            (await FrageAsync(host, new EroeffnePosten(new(p), 100, Mindestbestand: 98), Guid.NewGuid()))!.Success.Should().BeTrue();
            (await FrageAsync(host, new ReservierePosten(new(p), 5, Beleg.Ohne), Guid.NewGuid()))!.Success.Should().BeTrue();
            await Warte(() => Task.FromResult(probe.Gesehen.Any(e => e.Stream == p)), Frist, "der Zusteller bringt die Folge nicht hin");
            await Task.Delay(TimeSpan.FromSeconds(4));
            probe.Gesehen.Count(e => e.Stream == p).Should().Be(1, "angenommen ist er erst nach der Frist wieder fällig");
            (await OffenAsync(host, p)).Should().Be(1, "ohne „fertig“ bleibt der Eingang offen");
        }
        finally { await host.StopAsync(); }
    }
}
