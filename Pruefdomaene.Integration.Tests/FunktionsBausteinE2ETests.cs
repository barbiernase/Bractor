using Abstractions;
using FluentAssertions;
using Infrastructure.Funktionen;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pruefdomaene.Abrechnung;
using Pruefdomaene.Import;
using Pruefdomaene.Pipeline.Abrechnung;
using Xunit;
using static Pruefdomaene.Integration.Tests.Pruefstand;

namespace Pruefdomaene.Integration.Tests;

/// <summary>
/// Funktionen in Baustein-Form im echten Cluster gegen echtes Marten (Plan Pub/Sub-Neustrukturierung Phase 3): der Funktions-Actor je (Funktion,
/// Partition) liest ab seiner Position, rechnet, hält fest (außer gleichbleibend), sendet aus dem Festgehaltenen mit abgeleiteten Command-Ids;
/// eine Funktion draußen wartet auf die Antwort von Hand. Geprüft wird die Wirkung im Log — genau einmal je Eingang.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class FunktionsBausteinE2ETests
{
    private static readonly TimeSpan Frist = TimeSpan.FromSeconds(60);

    /// <summary>Ein Versand, der bei jedem Aufruf eine andere Nummer liefert — wie die Außenwelt: zwei Läufe ergäben zwei Briefe.</summary>
    private sealed class ZaehlenderVersand : IMahnversand
    {
        private int _n;
        public int Aufrufe => _n;
        public Task<string> VersendeAsync(Id<Kundenkonto> konto, Id<Rechnung> rechnung, int stufe) =>
            Task.FromResult($"brief-{Interlocked.Increment(ref _n)}-{Guid.NewGuid():N}");
    }

    private static async Task<IReadOnlyList<EventEnvelope>> Log(IHost host, Guid stream) =>
        await host.Services.GetRequiredService<IEventStoreRepository>().ReadStreamAsync(stream, 0, default);

    [Fact]
    public async Task Eine_gleichbleibende_Funktion_wirkt_genau_einmal_ohne_etwas_festzuhalten()
    {
        using var host = Cluster("pruef-funktion-gutschrift");
        await host.StartAsync();
        try
        {
            var konto = Id<Kundenkonto>.Neu();
            var rechnung = Id<Rechnung>.Neu();
            (await FrageAsync(host, new StelleRechnung(rechnung, konto, 120m), Guid.NewGuid()))!.Success.Should().BeTrue();
            (await FrageAsync(host, new BezahleRechnung(rechnung), Guid.NewGuid()))!.Success.Should().BeTrue();

            await Warte(async () => (await Zustand<Kundenkonto>(host.Services, konto.Wert)).Gutschriften == 1, Frist, "die Gutschrift kommt nicht an");
            await Task.Delay(TimeSpan.FromSeconds(2));
            (await Log(host, konto.Wert)).Count(e => e.Payload is GutschriftGebucht).Should().Be(1, "genau einmal je Eingang");

            var bezahlt = (await Log(host, rechnung.Wert)).Single(e => e.Payload is RechnungBezahlt);
            (await Log(host, FunktionsIds.Festhalten(nameof(Gutschrift), rechnung.Wert, bezahlt.AggregateVersion))).Should()
                .BeEmpty("eine gleichbleibende Funktion mit nur Commands in der Ausgabe schreibt nichts ins Log");
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Eine_Funktion_mit_Dienst_haelt_ihre_Ausgabe_fest_bevor_sie_wirkt()
    {
        var versand = new ZaehlenderVersand();
        using var host = Cluster("pruef-funktion-mahnwesen", dienste: s => s.AddSingleton<IMahnversand>(versand));
        await host.StartAsync();
        try
        {
            var konto = Id<Kundenkonto>.Neu();
            var rechnung = Id<Rechnung>.Neu();
            (await FrageAsync(host, new StelleRechnung(rechnung, konto, 80m), Guid.NewGuid()))!.Success.Should().BeTrue();
            (await FrageAsync(host, new PruefeZahlung(rechnung), Guid.NewGuid()))!.Success.Should().BeTrue();

            await Warte(async () => (await Zustand<Kundenkonto>(host.Services, konto.Wert)).Mahnungen == 1, Frist, "die Mahnung kommt nicht an");
            var ueberfaellig = (await Log(host, rechnung.Wert)).Single(e => e.Payload is ZahlungUeberfaellig);
            var fest = await Log(host, FunktionsIds.Festhalten(nameof(Mahnwesen), rechnung.Wert, ueberfaellig.AggregateVersion));
            fest.Select(e => e.Payload.GetType().Name).Should().Equal(nameof(BefehlFestgehalten), nameof(AusgabeFestgehalten));

            await Task.Delay(TimeSpan.FromSeconds(2));
            var vermerkt = (await Log(host, konto.Wert)).Where(e => e.Payload is MahnungVermerkt).ToList();
            vermerkt.Should().ContainSingle("genau eine Mahnung, auch wenn der Versand jedes Mal anders antwortet");
            ((BefehlFestgehalten)fest[0].Payload).Daten.Should().Contain(((MahnungVermerkt)vermerkt[0].Payload).Versand, "gewirkt hat das Festgehaltene");
            versand.Aufrufe.Should().Be(1);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Ein_Strom_wird_stueckweise_festgehalten_und_jede_Ausgabe_wirkt_genau_einmal()
    {
        using var host = Cluster("pruef-funktion-import");
        await host.StartAsync();
        try
        {
            const int zeilen = 230;
            var import = Id<Importauftrag>.Neu();
            (await FrageAsync(host, new StarteImport(import, zeilen), Guid.NewGuid()))!.Success.Should().BeTrue();

            await Warte(async () => (await Zustand<Importauftrag>(host.Services, import.Wert)).Stand == zeilen, Frist, "der Import wird nicht fertig");
            var herkunft = new Herkunft(import.Wert, 1, default, null, WirkerId.FuerFunktion("Importlauf"));
            for (var i = 0; i < zeilen; i += 23)
                (await Zustand<Posten.Posten>(host.Services, herkunft.NeueId<Posten.Posten>(i).Wert)).Existiert.Should().BeTrue($"Posten der Zeile {i}");

            var fest = await Log(host, FunktionsIds.Festhalten("Importlauf", import.Wert, 1));
            fest.Count(e => e.Payload is BefehlFestgehalten).Should().Be(zeilen + zeilen / Pipeline.Import.Importlauf.StandAlle, "jede Ausgabe genau einmal festgehalten");
            fest.Last().Payload.Should().BeOfType<AusgabeFestgehalten>();
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Eine_Funktion_draussen_rechnet_ohne_Bindung_mit_ihrer_einen_Umsetzung_im_Host()
    {
        using var host = Cluster("pruef-funktion-freigabe");
        await host.StartAsync();
        try
        {
            var rechnung = Id<Rechnung>.Neu();
            (await FrageAsync(host, new StelleRechnung(rechnung, Id<Kundenkonto>.Neu(), 20_000m), Guid.NewGuid()))!.Success.Should().BeTrue();
            await Warte(async () => (await Zustand<Rechnung>(host.Services, rechnung.Wert)).Gesperrt, Frist, "die Freigabe entscheidet nicht");
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Mit_Laufort_Hand_wartet_der_Eingang_auf_die_Antwort_und_die_erste_gewinnt()
    {
        using var host = Cluster("pruef-funktion-hand", dienste: s =>
        {
            s.AddSingleton(new BausteinBindung(nameof(IFreigabe), FunktionsLaufort.Hand));
            s.AddHandAusfuehrer();
        });
        await host.StartAsync();
        try
        {
            var rechnung = Id<Rechnung>.Neu();
            (await FrageAsync(host, new StelleRechnung(rechnung, Id<Kundenkonto>.Neu(), 50m), Guid.NewGuid()))!.Success.Should().BeTrue();
            var hand = host.Services.GetRequiredService<HandAusfuehrer>();
            HandEingang? offen = null;
            await Warte(() => Task.FromResult((offen = hand.OffeneEingaenge.FirstOrDefault(e => e.Eingang.Stream == rechnung.Wert)) is not null),
                Frist, "der Eingang wartet nicht von Hand");

            (await hand.AntworteAsync(offen!.Eingang.Vorgang, new IPipelineOutput[] { new SperreRechnung(rechnung, "von Hand") }, null, default))
                .Should().Be(ErgebnisAusgang.Geschrieben);
            (await hand.AntworteAsync(offen.Eingang.Vorgang, new IPipelineOutput[] { new GibRechnungFrei(rechnung) }, null, default))
                .Should().Be(ErgebnisAusgang.Unbekannt, "beantwortet ist er nicht mehr offen");
            await Warte(async () => (await Zustand<Rechnung>(host.Services, rechnung.Wert)).Gesperrt, Frist, "die Antwort von Hand wirkt nicht");
            (await Zustand<Rechnung>(host.Services, rechnung.Wert)).Freigegeben.Should().BeFalse();
        }
        finally { await host.StopAsync(); }
    }
}
