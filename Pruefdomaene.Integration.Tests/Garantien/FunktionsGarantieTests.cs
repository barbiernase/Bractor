using Abstractions;
using FluentAssertions;
using Infrastructure.Funktionen;
using Infrastructure.Lebenszyklus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Proto;
using Proto.Cluster;
using Pruefdomaene.Abrechnung;
using Pruefdomaene.Pipeline.Abrechnung;
using static Pruefdomaene.Integration.Tests.Pruefstand;

namespace Pruefdomaene.Integration.Tests.Garantien;

/// <summary>
/// Die Übergabe „Funktion → Ausgaben“ unter Störung, gegen echtes Marten (docs/11, docs/11 §11.14), am Mahnwesen (eine Funktion vor dem Aggregat mit
/// gerufenem Versand): ein scheiternder Versand wird wiederholt, ein Abbruch mitten im Eingang holt der Zusteller beim nächsten Host nach, derselbe
/// Eingang zweimal zugestellt wirkt einmal; dazu zwei Worker mit verschiedenen Antworten (Funktion mit Log-Eingang, im Umbau). Geprüft wird die
/// Wirkung im Log — je Eingang genau eine, auch wenn die Außenwelt (der Versand) bei jedem Aufruf anders antwortet.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class FunktionsGarantieTests
{
    private static readonly TimeSpan Frist = TimeSpan.FromSeconds(90);

    /// <summary>Der Versand draußen: jede Antwort anders; optional wartet jeder Aufruf, bis <see cref="Zusammen"/> Aufrufe da sind.</summary>
    private sealed class Versand(int zusammen = 1) : IMahnversand
    {
        private readonly TaskCompletionSource _alle = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _n;
        public int Aufrufe => Volatile.Read(ref _n);
        public async Task<string> VersendeAsync(Id<Kundenkonto> konto, Id<Rechnung> rechnung, int stufe)
        {
            var n = Interlocked.Increment(ref _n);
            if (n >= zusammen) _alle.TrySetResult();
            await _alle.Task.WaitAsync(TimeSpan.FromSeconds(60));
            return $"brief-{n}-{Guid.NewGuid():N}";
        }
    }

    private static async Task<(Id<Rechnung> Rechnung, Id<Kundenkonto> Konto)> UeberfaelligAsync(IHost host)
    {
        var rechnung = Id<Rechnung>.Neu();
        var konto = Id<Kundenkonto>.Neu();
        (await FrageAsync(host, new StelleRechnung(rechnung, konto, 99m), Guid.NewGuid()))!.Success.Should().BeTrue();
        (await FrageAsync(host, new PruefeZahlung(rechnung), Guid.NewGuid()))!.Success.Should().BeTrue();
        return (rechnung, konto);
    }

    private static async Task<IReadOnlyList<EventEnvelope>> Log(IHost host, Guid stream) =>
        await host.Services.GetRequiredService<IEventStoreRepository>().ReadStreamAsync(stream, 0, default);

    private static async Task<int> Vermerkt(IHost host, Id<Kundenkonto> konto) =>
        (await Log(host, konto.Wert)).Count(e => e.Payload is MahnungVermerkt);

    /// <summary>Ein Versand, der beim ersten Aufruf scheitert.</summary>
    private sealed class ErstScheiternd : IMahnversand
    {
        public int Aufrufe;
        public Task<string> VersendeAsync(Id<Kundenkonto> konto, Id<Rechnung> rechnung, int stufe) =>
            Interlocked.Increment(ref Aufrufe) == 1 ? throw new InvalidOperationException("der Versand ist kurz weg")
                : Task.FromResult($"brief-{Guid.NewGuid():N}");
    }

    [Fact]
    public async Task Scheitert_der_Versand_rechnet_ein_Kind_den_Eingang_erneut_und_er_wirkt_genau_einmal()
    {
        var versand = new ErstScheiternd();
        using var host = Cluster("garantie-funktion-wiederholt", dienste: s => s.AddSingleton<IMahnversand>(versand));
        await host.StartAsync();
        try
        {
            var (_, konto) = await UeberfaelligAsync(host);
            await Warte(async () => await Vermerkt(host, konto) == 1, Frist, "die Mahnung kommt nach der Wiederholung nicht an");
            await Task.Delay(TimeSpan.FromSeconds(2));
            (await Vermerkt(host, konto)).Should().Be(1, "je Eingang eine Wirkung");
            versand.Aufrufe.Should().Be(2, "einmal gescheitert, einmal gelungen");
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Bricht_der_Host_mitten_im_Eingang_ab_bringt_der_Zusteller_ihn_dem_naechsten_und_er_wirkt_genau_einmal()
    {
        // Der erste Host nimmt den Eingang an (schneller Weg nach dem Commit), sein Versand kommt nie zurück — er wird gestoppt. Die Zeile in
        //   cqrs_eingaenge bleibt offen; der Zusteller des zweiten Hosts bringt sie nach ihrer Frist, der Versand antwortet, die Mahnung wirkt einmal.
        var haengt = new Versand(zusammen: int.MaxValue);
        Id<Kundenkonto> konto;
        using (var a = Cluster("garantie-funktion-abbruch-a", dienste: s => s.AddSingleton<IMahnversand>(haengt)))
        {
            await a.StartAsync();
            try
            {
                (_, konto) = await UeberfaelligAsync(a);
                await Warte(() => Task.FromResult(haengt.Aufrufe == 1), Frist, "der erste Host rechnet den Eingang nicht");
            }
            finally { await a.StopAsync(); }
        }
        using var b = Cluster("garantie-funktion-abbruch-b");
        await b.StartAsync();
        try
        {
            await Warte(async () => await Vermerkt(b, konto) == 1, Frist, "der Zusteller bringt den offenen Eingang nicht zum nächsten Host");
            await Task.Delay(TimeSpan.FromSeconds(2));
            (await Vermerkt(b, konto)).Should().Be(1);
        }
        finally { await b.StopAsync(); }
    }

    [Fact]
    public async Task Derselbe_Eingang_zweimal_zugestellt_wirkt_einmal()
    {
        // Zwei Hosts auf demselben Log: ein Eingang mit derselben Kennung an beide Parents (etwa schneller Weg und Zusteller auf verschiedenen
        //   Knoten). Beide rechnen; die Command-Id folgt aus Kennung, Funktion und Stelle — die Inbox des Kundenkontos verwirft die zweite.
        using var a = Cluster("garantie-funktion-doppelt-a");
        using var b = Cluster("garantie-funktion-doppelt-b");
        await a.StartAsync();
        await b.StartAsync();
        try
        {
            var konto = Id<Kundenkonto>.Neu();
            var rechnung = Id<Rechnung>.Neu();
            var kennung = Guid.NewGuid();
            var eingang = new EingangZustellen(kennung, nameof(Mahnwesen), new Mahnfall(rechnung, konto, 99m, 1), Eingangsart.Kette, rechnung.Wert, 2,
                DateTimeOffset.UtcNow, null, null, null);
            foreach (var h in new[] { a, b })
            {
                var system = h.Services.GetRequiredService<ActorSystem>();
                var q = await system.Root.RequestAsync<EingangQuittung>(PID.FromAddress(system.Address, FunktionenWurzel.Name(nameof(Mahnwesen))), eingang,
                    TimeSpan.FromSeconds(30));
                q.Ausgang.Should().Be(Quittungsart.Fertig);
            }
            (await Vermerkt(a, konto)).Should().Be(1, "zweimal gerechnet, einmal gewirkt");
        }
        finally
        {
            await b.StopAsync();
            await a.StopAsync();
        }
    }

    [Fact]
    public async Task Zwei_Antworten_auf_denselben_Eingang_es_wirkt_die_erste()
    {
        using var host = Cluster("garantie-funktion-worker", dienste: s =>
        {
            s.AddSingleton(new BausteinBindung(nameof(IFreigabe), FunktionsLaufort.Hand));
            s.AddHandAusfuehrer();
        });
        await host.StartAsync();
        try
        {
            var rechnung = Id<Rechnung>.Neu();
            (await FrageAsync(host, new StelleRechnung(rechnung, Id<Kundenkonto>.Neu(), 500m), Guid.NewGuid()))!.Success.Should().BeTrue();
            var hand = host.Services.GetRequiredService<HandAusfuehrer>();
            HandEingang? offen = null;
            await Warte(() => Task.FromResult((offen = hand.OffeneEingaenge.FirstOrDefault(e => e.Stream == rechnung.Wert)) is not null),
                Frist, "der Eingang wartet nicht von Hand");

            // Ein zweiter Arbeiter antwortet auf denselben Eingang (seine Kennung) anders und schneller — die Werkstatt nimmt die erste Antwort.
            var werkstatt = host.Services.GetRequiredService<Werkstatt>();
            werkstatt.Ergebnis(offen!.Vorgang, new object[] { new GibRechnungFrei(rechnung) }, 0, true, null).Should().Be(Werkstattausgang.Angenommen);
            (await hand.AntworteAsync(offen.Vorgang, new IAusgabe[] { new SperreRechnung(rechnung, "zu spät") }, null, default))
                .Should().Be(ErgebnisAusgang.Unbekannt, "die erste Antwort beendete den Lauf — die zweite verfällt");

            await Warte(async () => (await Zustand<Rechnung>(host.Services, rechnung.Wert)).Freigegeben, Frist, "die erste Antwort wirkt nicht");
            await Task.Delay(TimeSpan.FromSeconds(2));
            (await Zustand<Rechnung>(host.Services, rechnung.Wert)).Gesperrt.Should().BeFalse();
        }
        finally { await host.StopAsync(); }
    }
}
