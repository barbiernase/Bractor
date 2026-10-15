using Abstractions;
using FluentAssertions;
using Infrastructure.Extensions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Pruefdomaene.Auftrag;
using Pruefdomaene.Kontingent;
using Pruefdomaene.Posten;
using Pruefdomaene.Verteilung;
using static Pruefdomaene.Integration.Tests.Pruefstand;

namespace Pruefdomaene.Integration.Tests;

/// <summary>
/// Ebene 2 — PROZESSE Ende zu Ende im echten Cluster (Postgres/Consul/Redis), domänenneutral (docs/13 T-1). Ein Fach-Command startet
/// den Vorgang; der Dirigent treibt die Regeln über die echten Aggregat-Actors. Beweiskraft: der Endzustand der Aggregate steht im Log
/// (Voll-Fold, keine Projektion), der Manager-Log endet mit <c>ProzessBeendet</c> und dem erwarteten Ausgang.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class ProzessE2ETests
{
    private static readonly TimeSpan Frist = TimeSpan.FromSeconds(60);

    private static async Task Eroeffne(IHost host, Guid posten, int bestand, Guid? kontingent = null, int plaetze = 0)
    {
        var d = host.Services.GetRequiredService<IAggregateDispatcher>();
        d.Dispatch(new EroeffnePosten(posten, bestand));
        if (kontingent is { } k) d.Dispatch(new RichteKontingentEin(k, plaetze));
        await Warte(async () => (await Zustand<Posten.Posten>(host.Services, posten)).Existiert
                                && (kontingent == null || (await Zustand<Kontingent.Kontingent>(host.Services, kontingent.Value)).Existiert),
            Frist, "Posten/Kontingent wurden nicht angelegt (Cluster-Boot?).");
    }

    [Fact]
    public async Task Diamant_reserviert_beide_Zweige_und_bestaetigt_am_Join()
    {
        using var host = Cluster("pruef-diamant");
        await host.StartAsync();
        try
        {
            var store = host.Services.GetRequiredService<IEventStoreRepository>();
            Guid posten = Guid.NewGuid(), kontingent = Guid.NewGuid(), auftrag = Guid.NewGuid();
            await Eroeffne(host, posten, 10, kontingent, 5);

            host.Services.GetRequiredService<IAggregateDispatcher>().Dispatch(new StarteAuftrag(auftrag, posten, kontingent, 3));

            var korr = ProzessId.Für(nameof(AuftragsProzess), auftrag, 1);
            await Warte(() => Beendet(store, korr, erfolg: true), Frist, "Der Diamant ist nicht bis zur Bestätigung gelaufen.");
            (await Zustand<Posten.Posten>(host.Services, posten)).Reserviert.Should().Be(3);
            (await Zustand<Kontingent.Kontingent>(host.Services, kontingent)).Frei.Should().Be(2);
            (await Zustand<Auftrag.Auftrag>(host.Services, auftrag)).Bestaetigt.Should().BeTrue();
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Scheitert_ein_Zweig_wird_der_gelungene_kompensiert_und_nie_bestaetigt()
    {
        using var host = Cluster("pruef-kompensation");
        await host.StartAsync();
        try
        {
            var store = host.Services.GetRequiredService<IEventStoreRepository>();
            Guid posten = Guid.NewGuid(), kontingent = Guid.NewGuid(), auftrag = Guid.NewGuid();
            await Eroeffne(host, posten, 10, kontingent, plaetze: 1);   // das Kontingent reicht nicht für 3

            host.Services.GetRequiredService<IAggregateDispatcher>().Dispatch(new StarteAuftrag(auftrag, posten, kontingent, 3));

            var korr = ProzessId.Für(nameof(AuftragsProzess), auftrag, 1);
            await Warte(() => Beendet(store, korr, erfolg: false), Frist, "Der gescheiterte Vorgang wurde nicht als fehlgeschlagen beendet.");
            await Warte(async () => (await Zustand<Posten.Posten>(host.Services, posten)).Reserviert == 0, Frist,
                "Die Reservierung des gelungenen Zweigs wurde nicht freigegeben (Kompensation).");

            var postenLog = (await store.ReadStreamAsync(posten, 0, default)).Select(e => e.Payload).ToList();
            postenLog.OfType<PostenReserviert>().Should().ContainSingle();
            postenLog.OfType<PostenFreigegeben>().Should().ContainSingle().Which.Menge.Should().Be(3);
            (await Zustand<Kontingent.Kontingent>(host.Services, kontingent)).Frei.Should().Be(1, "die abgelehnte Belegung hat nichts verändert");
            (await Zustand<Auftrag.Auftrag>(host.Services, auftrag)).Bestaetigt.Should().BeFalse();
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Nebenlaeufige_Vorgaenge_auf_denselben_Aggregaten_wirken_je_genau_einmal()
    {
        using var host = Cluster("pruef-parallel");
        await host.StartAsync();
        try
        {
            var store = host.Services.GetRequiredService<IEventStoreRepository>();
            Guid posten = Guid.NewGuid(), kontingent = Guid.NewGuid();
            await Eroeffne(host, posten, 10, kontingent, 10);

            var auftraege = Enumerable.Range(0, 5).Select(_ => Guid.NewGuid()).ToList();
            var d = host.Services.GetRequiredService<IAggregateDispatcher>();
            foreach (var a in auftraege) d.Dispatch(new StarteAuftrag(a, posten, kontingent, 2));

            foreach (var a in auftraege)
                await Warte(() => Beendet(store, ProzessId.Für(nameof(AuftragsProzess), a, 1), erfolg: true), Frist,
                    $"Vorgang {a} lief nicht bis zur Bestätigung.");
            (await Zustand<Posten.Posten>(host.Services, posten)).Reserviert.Should().Be(10, "fünf Vorgänge × 2, keiner doppelt");
            (await Zustand<Kontingent.Kontingent>(host.Services, kontingent)).Frei.Should().Be(0);
            foreach (var a in auftraege) (await Zustand<Auftrag.Auftrag>(host.Services, a)).Bestaetigt.Should().BeTrue();
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Fan_out_bucht_jedes_Ziel_einmal_und_der_Count_Join_schliesst_nach_allen()
    {
        using var host = Cluster("pruef-fanout");
        await host.StartAsync();
        try
        {
            var store = host.Services.GetRequiredService<IEventStoreRepository>();
            var quelle = Guid.NewGuid();
            var ziele = Enumerable.Range(0, 4).Select(_ => Guid.NewGuid()).ToList();
            await Eroeffne(host, quelle, 100);
            foreach (var z in ziele) await Eroeffne(host, z, 0);

            var verteilung = Guid.NewGuid();
            host.Services.GetRequiredService<IAggregateDispatcher>().Dispatch(new StarteVerteilung(verteilung, quelle, ziele, 5));

            await Warte(async () => (await Zustand<Verteilung.Verteilung>(host.Services, verteilung)).Abgeschlossen, Frist,
                "Die Verteilung wurde nicht abgeschlossen — Fan-out oder Count-Join lief nicht durch.");
            (await Zustand<Verteilung.Verteilung>(host.Services, verteilung)).Zugebucht.Should().Be(20, "der Count-Join sah alle vier Zubuchungen");
            foreach (var z in ziele)
            {
                (await Zustand<Posten.Posten>(host.Services, z)).Bestand.Should().Be(5);
                (await store.ReadStreamAsync(z, 0, default)).Select(e => e.Payload).OfType<PostenZugebucht>().Should().ContainSingle("je Ziel genau ein Vorgang");
            }
            (await Zustand<Posten.Posten>(host.Services, quelle)).Reserviert.Should().Be(20);
            await Warte(() => Beendet(store, ProzessId.Für(nameof(VerteilProzess), verteilung, 1), erfolg: true), Frist,
                "Der Verteil-Vorgang wurde nicht erfolgreich beendet.");
        }
        finally { await host.StopAsync(); }
    }
}
