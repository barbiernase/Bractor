using Abstractions;
using FluentAssertions;
using Infrastructure.Funktionen;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pruefdomaene.Abrechnung;
using Pruefdomaene.Import;
using Pruefwerk.Katalog.Abrechnung;
using Pruefwerk.Katalog.Import;
using Xunit;
using static Pruefdomaene.Integration.Tests.Pruefstand;

namespace Pruefdomaene.Integration.Tests;

/// <summary>
/// Funktionen in Baustein-Form im echten Cluster gegen echtes Marten: der Funktions-Actor je (Funktion,
/// Partition) liest ab seiner Position, rechnet, hält fest (außer gleichbleibend), sendet aus dem Festgehaltenen mit abgeleiteten Command-Ids;
/// eine große Rechnung ist Arbeit des Rechnungsprüfers, die die Hand beantwortet. Geprüft wird die Wirkung im Log — genau einmal je Eingang.
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
        public Task<string> VersendeAsync(Guid empfaenger, Guid vorgang, int stufe) =>
            Task.FromResult($"brief-{Interlocked.Increment(ref _n)}-{Guid.NewGuid():N}");
    }

    private static async Task<IReadOnlyList<EventEnvelope>> Log(IHost host, Guid stream) =>
        await host.Services.GetRequiredService<IEventStoreRepository>().ReadStreamAsync(stream, 0, default);

    [Fact]
    public async Task Eine_Funktion_ohne_Dienst_wirkt_genau_einmal()
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

        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Eine_Funktion_vor_dem_Aggregat_mit_Dienst_wirkt_genau_einmal_ohne_etwas_festzuhalten()
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

            await Task.Delay(TimeSpan.FromSeconds(2));
            (await Log(host, konto.Wert)).Where(e => e.Payload is MahnungVermerkt).Should().ContainSingle("genau eine Mahnung");
            versand.Aufrufe.Should().Be(1);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Ein_Strom_aus_einer_Folge_wirkt_je_Ausgabe_genau_einmal_ohne_etwas_festzuhalten()
    {
        using var host = Cluster("pruef-funktion-import");
        await host.StartAsync();
        try
        {
            const int zeilen = 230;
            var import = Id<Importauftrag>.Neu();
            (await FrageAsync(host, new StarteImport(import, zeilen), Guid.NewGuid()))!.Success.Should().BeTrue();

            await Warte(async () => (await Zustand<Importauftrag>(host.Services, import.Wert)).Stand == zeilen, Frist, "der Import wird nicht fertig");
            // Die Ids der Posten leitet der Zeilenlauf (Katalog Pruefwerk) aus seinem Eingang und der Zeile ab, die Übersetzung macht sie zu Posten-Ids.
            var herkunft = new Herkunft(import.Wert, 1, default, null, WirkerId.FuerFunktion(nameof(Zeilenlauf)));
            for (var i = 0; i < zeilen; i += 23)
                (await Zustand<Posten.Posten>(host.Services, herkunft.NeueId("zeile:" + i))).Existiert.Should().BeTrue($"Posten der Zeile {i}");

            await Task.Delay(TimeSpan.FromSeconds(2));
            (await Log(host, import.Wert)).Count(e => e.Payload is ImportStandGemeldet)
                .Should().Be((zeilen + Zeilenlauf.StandAlle - 1) / Zeilenlauf.StandAlle, "jeder Stand genau einmal");
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Der_Freigabeautomat_gibt_kleine_Rechnungen_im_Host_frei()
    {
        using var host = Cluster("pruef-funktion-freigabe");
        await host.StartAsync();
        try
        {
            var rechnung = Id<Rechnung>.Neu();
            (await FrageAsync(host, new StelleRechnung(rechnung, Id<Kundenkonto>.Neu(), 50m), Guid.NewGuid()))!.Success.Should().BeTrue();
            await Warte(async () => (await Zustand<Rechnung>(host.Services, rechnung.Wert)).Freigegeben, Frist, "die Freigabe entscheidet nicht");
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Eine_grosse_Rechnung_legt_die_Freigabe_dem_Rechnungspruefer_vor_er_entscheidet()
    {
        using var host = Cluster("pruef-vorlage");
        await host.StartAsync();
        try
        {
            var rechnung = Id<Rechnung>.Neu();
            (await FrageAsync(host, new StelleRechnung(rechnung, Id<Kundenkonto>.Neu(), 20_000m), Guid.NewGuid()))!.Success.Should().BeTrue();
            await Warte(async () => (await Zustand<Rechnung>(host.Services, rechnung.Wert)).Vorgelegt, Frist, "die Rechnung liegt nicht vor");
            (await Zustand<Rechnung>(host.Services, rechnung.Wert)).Freigegeben.Should().BeFalse("über der Grenze gibt der Automat nicht frei");

            // Der Rechnungsprüfer reagiert auf das Fakt (hier der Test an seiner Stelle): er sperrt; eine spätere Freigabe ändert nichts mehr.
            (await FrageAsync(host, new SperreRechnung(rechnung, "von Hand"), Guid.NewGuid()))!.Success.Should().BeTrue();
            (await FrageAsync(host, new GibRechnungFrei(rechnung), Guid.NewGuid()))!.Success.Should().BeTrue();
            var r = await Zustand<Rechnung>(host.Services, rechnung.Wert);
            r.Gesperrt.Should().BeTrue();
            r.Freigegeben.Should().BeFalse();
        }
        finally { await host.StopAsync(); }
    }
}
