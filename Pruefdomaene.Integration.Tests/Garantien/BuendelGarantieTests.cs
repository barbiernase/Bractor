using Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pruefdomaene.Abrechnung;
using Pruefdomaene.Messung;
using Pruefdomaene.Projections.Abrechnung;
using Xunit;
using static Pruefdomaene.Integration.Tests.Pruefstand;

namespace Pruefdomaene.Integration.Tests.Garantien;

/// <summary>
/// BÜNDEL im echten Cluster (docs/04 §4.3.1, docs/11): <c>Auf(Buendel&lt;A, B&gt; b, …)</c> wird gerufen, sobald zum Schlüssel von jedem Typ ein Event
/// im Log steht — gleich, in welcher Reihenfolge und in welchen Partitionen sie ankommen —, und bei jedem weiteren Event eines Typs erneut mit dem
/// neuesten. Ein unvollständiges Bündel ruft nichts. Je Schlüsselart ein Bündel der <see cref="RechnungsbildProjektion"/>: der Stream der Rechnung,
/// das Feld Rechnung der Gutschrift (mit <c>[Ueber]</c>), global.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class BuendelGarantieTests
{
    private static readonly TimeSpan Frist = TimeSpan.FromSeconds(60);

    private static async Task<Rechnungsbild> Bild(IHost host, Id<Rechnung> rechnung)
        => (Rechnungsbild)(await host.Services.GetRequiredService<IQueryDienst>().ExecuteAsync(new HoleRechnungsbild(rechnung))).Data;

    private static async Task<Lagebild> Lage(IHost host)
        => (Lagebild)(await host.Services.GetRequiredService<IQueryDienst>().ExecuteAsync(new HoleLage())).Data;

    [Fact]
    public async Task Ein_Buendel_ueber_den_Stream_ruft_erst_wenn_beide_Events_im_Log_stehen()
    {
        using var host = Cluster("pruef-buendel-stream", schema: "pruef_buendel");
        await host.StartAsync();
        try
        {
            // Klein: der Freigabeautomat gibt sie frei — RechnungGestellt und RechnungFreigegeben im selben Stream.
            var klein = Id<Rechnung>.Neu();
            (await FrageAsync(host, new StelleRechnung(klein, Id<Kundenkonto>.Neu(), 50m), Guid.NewGuid()))!.Success.Should().BeTrue();
            await Warte(async () => (await Bild(host, klein)).Freigegeben == 50m, Frist, "das Bündel Gestellt ⋈ Freigegeben ruft nicht");

            // Groß: Arbeit des Rechnungsprüfers, nicht freigegeben — das Bündel bleibt unvollständig.
            var gross = Id<Rechnung>.Neu();
            (await FrageAsync(host, new StelleRechnung(gross, Id<Kundenkonto>.Neu(), 20_000m), Guid.NewGuid()))!.Success.Should().BeTrue();
            var kontrolle = Id<Rechnung>.Neu();
            (await FrageAsync(host, new StelleRechnung(kontrolle, Id<Kundenkonto>.Neu(), 60m), Guid.NewGuid()))!.Success.Should().BeTrue();
            await Warte(async () => (await Bild(host, kontrolle)).Freigegeben == 60m, Frist, "die Kontrolle nach der großen Rechnung ruft nicht");
            (await Bild(host, gross)).Freigegeben.Should().BeNull("ohne RechnungFreigegeben ist das Bündel unvollständig");
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Ein_Buendel_ueber_ein_Feld_ruft_in_beiden_Reihenfolgen_und_nimmt_das_neueste()
    {
        using var host = Cluster("pruef-buendel-feld", schema: "pruef_buendel");
        await host.StartAsync();
        try
        {
            // Erst die Rechnung, dann die Gutschrift (sie entscheidet das Kundenkonto — ein anderer Stream, meist eine andere Partition).
            var konto = Id<Kundenkonto>.Neu();
            var erst = Id<Rechnung>.Neu();
            (await FrageAsync(host, new StelleRechnung(erst, konto, 100m), Guid.NewGuid()))!.Success.Should().BeTrue();
            (await FrageAsync(host, new BucheGutschrift(konto, erst, 30m), Guid.NewGuid()))!.Success.Should().BeTrue();
            await Warte(async () => (await Bild(host, erst)) is { Gestellt: 100m, Gutgeschrieben: 30m }, Frist, "Gestellt ⋈ Gutschrift ruft nicht");

            // Umgekehrt: die Gutschrift steht vor der Rechnung im Log — die Rechnung findet sie beim Blick zurück.
            var konto2 = Id<Kundenkonto>.Neu();
            var spaet = Id<Rechnung>.Neu();
            (await FrageAsync(host, new BucheGutschrift(konto2, spaet, 7m), Guid.NewGuid()))!.Success.Should().BeTrue();
            (await FrageAsync(host, new StelleRechnung(spaet, konto2, 70m), Guid.NewGuid()))!.Success.Should().BeTrue();
            await Warte(async () => (await Bild(host, spaet)) is { Gestellt: 70m, Gutgeschrieben: 7m }, Frist, "die spätere Rechnung findet die frühere Gutschrift nicht");

            // Eine weitere Gutschrift: das Bündel ruft erneut, mit dem neuesten Event.
            (await FrageAsync(host, new BucheGutschrift(konto, erst, 45m), Guid.NewGuid()))!.Success.Should().BeTrue();
            await Warte(async () => (await Bild(host, erst)).Gutgeschrieben == 45m, Frist, "die neue Gutschrift ersetzt die alte nicht");
            (await Bild(host, erst)).Gestellt.Should().Be(100m);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Ein_globales_Buendel_nimmt_je_Typ_das_neueste_Event()
    {
        using var host = Cluster("pruef-buendel-global", schema: "pruef_buendel");
        await host.StartAsync();
        try
        {
            var grund = "sperre-" + Guid.NewGuid().ToString("N")[..8];
            var rechnung = Id<Rechnung>.Neu();
            (await FrageAsync(host, new StelleRechnung(rechnung, Id<Kundenkonto>.Neu(), 20_000m), Guid.NewGuid()))!.Success.Should().BeTrue();
            (await FrageAsync(host, new SperreRechnung(rechnung, grund), Guid.NewGuid()))!.Success.Should().BeTrue();
            var wert = 1000 + Random.Shared.NextDouble();
            var stelle = Id<Messstelle>.Neu();
            (await FrageAsync(host, new MeldeAusreisser(stelle, wert), Guid.NewGuid()))!.Success.Should().BeTrue();
            await Warte(async () => await Lage(host) is { } l && l.Sperrgrund == grund && l.Ausreisser == wert, Frist, "Sperre ⋈ Ausreißer ruft nicht");

            // Eine neuere Sperre einer anderen Rechnung: global zählt das neueste Event je Typ.
            var zweiter = "sperre-" + Guid.NewGuid().ToString("N")[..8];
            var andere = Id<Rechnung>.Neu();
            (await FrageAsync(host, new StelleRechnung(andere, Id<Kundenkonto>.Neu(), 20_000m), Guid.NewGuid()))!.Success.Should().BeTrue();
            (await FrageAsync(host, new SperreRechnung(andere, zweiter), Guid.NewGuid()))!.Success.Should().BeTrue();
            await Warte(async () => await Lage(host) is { } l && l.Sperrgrund == zweiter && l.Ausreisser == wert, Frist, "die neuere Sperre zählt nicht");
        }
        finally { await host.StopAsync(); }
    }
}
