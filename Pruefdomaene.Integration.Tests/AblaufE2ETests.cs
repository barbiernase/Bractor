using Abstractions;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pruefdomaene.Auftrag;
using Pruefdomaene.Kontingent;
using Pruefdomaene.Posten;
using Pruefdomaene.Verteilung;
using static Pruefdomaene.Integration.Tests.Pruefstand;

namespace Pruefdomaene.Integration.Tests;

/// <summary>
/// Ebene 2 — ABLÄUFE Ende zu Ende im echten Cluster (Postgres/Consul), domänenneutral: ein Vorgang ist ein Aggregat (Auftrag, Verteilung),
/// seine Kanten Event → Command tragen gleichbleibende Funktionen (Plan Pub/Sub-Neustrukturierung §2.2, Phase 4). Beweiskraft: der Endzustand der
/// Aggregate (Voll-Fold) und die Zahl der Fakten je Stream im Log — genau einmal je Schritt, die Kompensation genau einmal; keine Prozess-Events.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class AblaufE2ETests
{
    private static readonly TimeSpan Frist = TimeSpan.FromSeconds(60);

    private static async Task Eroeffne(IHost host, Guid posten, int bestand, Guid? kontingent = null, int plaetze = 0)
    {
        (await FrageAsync(host, new EroeffnePosten(new(posten), bestand), Guid.NewGuid()))!.Success.Should().BeTrue();
        if (kontingent is { } k) (await FrageAsync(host, new RichteKontingentEin(new(k), plaetze), Guid.NewGuid()))!.Success.Should().BeTrue();
    }

    private static async Task<int> Zahl<T>(IHost host, Guid stream) =>
        (await host.Services.GetRequiredService<IEventStoreRepository>().ReadStreamAsync(stream, 0, default)).Count(e => e.Payload is T);

    [Fact]
    public async Task Ein_Auftrag_reserviert_und_belegt_je_einmal_und_wird_einmal_bestaetigt()
    {
        using var host = Cluster("pruef-auftrag");
        await host.StartAsync();
        try
        {
            Guid posten = Guid.NewGuid(), kontingent = Guid.NewGuid(), auftrag = Guid.NewGuid();
            await Eroeffne(host, posten, 10, kontingent, 5);

            (await FrageAsync(host, new StarteAuftrag(new(auftrag), new(posten), new(kontingent), 3), Guid.NewGuid()))!.Success.Should().BeTrue();

            await Warte(async () => (await Zustand<Auftrag.Auftrag>(host.Services, auftrag)).Bestaetigt, Frist, "Der Auftrag wurde nicht bestätigt.");
            await Task.Delay(TimeSpan.FromSeconds(2));
            (await Zustand<Posten.Posten>(host.Services, posten)).Reserviert.Should().Be(3);
            (await Zustand<Kontingent.Kontingent>(host.Services, kontingent)).Frei.Should().Be(2);
            (await Zahl<PostenReserviert>(host, posten)).Should().Be(1);
            (await Zahl<KontingentBelegt>(host, kontingent)).Should().Be(1);
            (await Zahl<AuftragBestaetigt>(host, auftrag)).Should().Be(1);
            (await Zahl<AuftragGescheitert>(host, auftrag)).Should().Be(0);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Scheitert_ein_Zweig_wird_der_gelungene_genau_einmal_zurueckgenommen_und_nie_bestaetigt()
    {
        using var host = Cluster("pruef-kompensation");
        await host.StartAsync();
        try
        {
            Guid posten = Guid.NewGuid(), kontingent = Guid.NewGuid(), auftrag = Guid.NewGuid();
            await Eroeffne(host, posten, 10, kontingent, plaetze: 1);   // das Kontingent reicht nicht für 3

            (await FrageAsync(host, new StarteAuftrag(new(auftrag), new(posten), new(kontingent), 3), Guid.NewGuid()))!.Success.Should().BeTrue();

            await Warte(async () => await Zahl<PostenFreigegeben>(host, posten) == 1, Frist, "Die Reservierung des gelungenen Zweigs wurde nicht zurückgenommen.");
            await Task.Delay(TimeSpan.FromSeconds(2));
            (await Zahl<PostenReserviert>(host, posten)).Should().Be(1);
            (await Zahl<PostenFreigegeben>(host, posten)).Should().Be(1, "der Gegenzug genau einmal");
            (await Zustand<Posten.Posten>(host.Services, posten)).Reserviert.Should().Be(0);
            (await Zustand<Kontingent.Kontingent>(host.Services, kontingent)).Frei.Should().Be(1, "die abgelehnte Belegung hat nichts verändert");
            (await Zahl<BelegungAbgelehnt>(host, kontingent)).Should().Be(1, "die Absage ist ein Fakt im Kontingent");
            (await Zahl<AuftragGescheitert>(host, auftrag)).Should().Be(1);
            (await Zahl<PostenFreigabeAngeordnet>(host, auftrag)).Should().Be(1);
            (await Zahl<KontingentEntlastungAngeordnet>(host, auftrag)).Should().Be(0, "die Belegung gelang nie — nichts zu entlasten");
            (await Zustand<Auftrag.Auftrag>(host.Services, auftrag)).Bestaetigt.Should().BeFalse();
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Scheitern_beide_Zweige_wird_nichts_zurueckgenommen()
    {
        using var host = Cluster("pruef-beide-abgelehnt");
        await host.StartAsync();
        try
        {
            Guid posten = Guid.NewGuid(), kontingent = Guid.NewGuid(), auftrag = Guid.NewGuid();
            await Eroeffne(host, posten, 1, kontingent, plaetze: 1);

            (await FrageAsync(host, new StarteAuftrag(new(auftrag), new(posten), new(kontingent), 3), Guid.NewGuid()))!.Success.Should().BeTrue();

            await Warte(async () => await Zahl<AuftragGescheitert>(host, auftrag) == 1, Frist, "Der Auftrag scheitert nicht.");
            await Warte(async () => await Zahl<ReservierungAbgelehnt>(host, posten) == 1 && await Zahl<BelegungAbgelehnt>(host, kontingent) == 1, Frist,
                "Beide Absagen stehen nicht im Log.");
            await Task.Delay(TimeSpan.FromSeconds(2));
            (await Zahl<AuftragGescheitert>(host, auftrag)).Should().Be(1, "die zweite Absage ändert nichts mehr");
            (await Zahl<PostenFreigegeben>(host, posten)).Should().Be(0);
            (await Zahl<KontingentEntlastet>(host, kontingent)).Should().Be(0);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Nebenlaeufige_Auftraege_auf_denselben_Aggregaten_wirken_je_genau_einmal()
    {
        using var host = Cluster("pruef-parallel");
        await host.StartAsync();
        try
        {
            Guid posten = Guid.NewGuid(), kontingent = Guid.NewGuid();
            await Eroeffne(host, posten, 10, kontingent, 10);

            var auftraege = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
            await Task.WhenAll(auftraege.Select(a => FrageAsync(host, new StarteAuftrag(new(a), new(posten), new(kontingent), 2), Guid.NewGuid())));

            foreach (var a in auftraege)
                await Warte(async () => (await Zustand<Auftrag.Auftrag>(host.Services, a)).Bestaetigt, Frist, $"Auftrag {a} wurde nicht bestätigt.");
            (await Zustand<Posten.Posten>(host.Services, posten)).Reserviert.Should().Be(10, "fünf Aufträge × 2, keiner doppelt");
            (await Zustand<Kontingent.Kontingent>(host.Services, kontingent)).Frei.Should().Be(0);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Eine_Verteilung_bucht_jedes_Ziel_einmal_und_schliesst_nach_allen_ab()
    {
        using var host = Cluster("pruef-verteilung");
        await host.StartAsync();
        try
        {
            var quelle = Guid.NewGuid();
            var ziele = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToList();
            await Eroeffne(host, quelle, 100);
            foreach (var z in ziele) await Eroeffne(host, z, 0);

            var verteilung = Guid.NewGuid();
            (await FrageAsync(host, new StarteVerteilung(new(verteilung), new(quelle), ziele.Select(z => new Id<Posten.Posten>(z)).ToList(), 5),
                Guid.NewGuid()))!.Success.Should().BeTrue();

            await Warte(async () => (await Zustand<Verteilung.Verteilung>(host.Services, verteilung)).Abgeschlossen, Frist,
                "Die Verteilung wurde nicht abgeschlossen — Fan-out oder Zählung lief nicht durch.");
            await Task.Delay(TimeSpan.FromSeconds(2));
            (await Zustand<Verteilung.Verteilung>(host.Services, verteilung)).Zugebucht.Should().Be(20, "das Zähl-Aggregat sah alle vier Zubuchungen");
            (await Zahl<ZubuchungGezaehlt>(host, verteilung)).Should().Be(4);
            (await Zahl<VerteilungAbgeschlossen>(host, verteilung)).Should().Be(1);
            foreach (var z in ziele)
            {
                (await Zustand<Posten.Posten>(host.Services, z)).Bestand.Should().Be(5);
                (await Zahl<PostenZugebucht>(host, z)).Should().Be(1, "je Ziel genau eine Zubuchung");
            }
            (await Zustand<Posten.Posten>(host.Services, quelle)).Reserviert.Should().Be(20);
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Reicht_die_Quelle_nicht_scheitert_die_Verteilung_ohne_Zubuchung()
    {
        using var host = Cluster("pruef-verteilung-absage");
        await host.StartAsync();
        try
        {
            var quelle = Guid.NewGuid();
            var ziel = Guid.NewGuid();
            await Eroeffne(host, quelle, 3);
            await Eroeffne(host, ziel, 0);

            var verteilung = Guid.NewGuid();
            (await FrageAsync(host, new StarteVerteilung(new(verteilung), new(quelle), [new Id<Posten.Posten>(ziel)], 5), Guid.NewGuid()))!
                .Success.Should().BeTrue();

            await Warte(async () => await Zahl<VerteilungGescheitert>(host, verteilung) == 1, Frist, "Die Verteilung scheitert nicht.");
            await Task.Delay(TimeSpan.FromSeconds(2));
            (await Zahl<PostenZugebucht>(host, ziel)).Should().Be(0);
            (await Zahl<ZubuchungenAngeordnet>(host, verteilung)).Should().Be(0);
        }
        finally { await host.StopAsync(); }
    }
}
