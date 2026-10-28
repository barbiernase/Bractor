using Abstractions;
using FluentAssertions;
using Infrastructure.Lebenszyklus;
using Infrastructure.Projections;
using Infrastructure.Wecken;
using Microsoft.Extensions.DependencyInjection;
using Proto;
using Proto.Cluster;
using Pruefdomaene.Abrechnung;
using Pruefdomaene.Posten;
using static Pruefdomaene.Integration.Tests.Pruefstand;

namespace Pruefdomaene.Integration.Tests.Garantien;

/// <summary>
/// Der Wecker am Commit im echten Cluster (Plan Pub/Sub-Neustrukturierung §2.3, §7 Risiko 1, Phase 6), am Beispiel der Funktion <c>Nachschub</c>
/// (Aggregat-Event → Funktion → Command an das Aggregat):
/// <list type="bullet">
/// <item><b>Latenz:</b> ein Commit weckt die Funktion, die das Event hört — sie wartet nicht auf den Takt (30 s); eine gesetzte Frist weckt die Uhr
///   (ihr eigener Takt ist eine Sekunde, die Latenz-Prüfung der Uhr unterscheidet Weckruf und Takt darum nicht);</item>
/// <item><b>verlorene Weckrufe:</b> geht jeder Weckruf eines Commits verloren, wirkt der Eingang erst mit dem Takt — und genau einmal;</item>
/// <item><b>doppelte Weckrufe:</b> kommt jeder Weckruf zweimal, wirkt jeder Eingang trotzdem genau einmal (das Ziel liest ab seiner Position).</item>
/// </list>
/// Die Weckrufe stört ein Bote mit eingesetzter Anfrage (Naht <see cref="Bote.Anfrage"/>), der Rest des Clusters bleibt echt.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class WeckerGarantieTests
{
    private static readonly TimeSpan Schnell = TimeSpan.FromSeconds(5);

    /// <summary>Ersetzt den Wecker durch einen mit demselben Plan, dessen Weckrufe <paramref name="anfrage"/> zustellt (über den echten Cluster).</summary>
    private static Action<IServiceCollection> Wecker(Func<Bote.Anfrage, Bote.Anfrage> anfrage) => s => Dekoriere<Infrastructure.Wecken.Wecker>(s,
        (sp, alt) => new Infrastructure.Wecken.Wecker(alt.Plan, sp.GetRequiredService<Konsum>(), () =>
        {
            var cluster = sp.GetRequiredService<ActorSystem>().Cluster();
            return new Bote(anfrage((ziel, nachricht, ct) => cluster.RequestAsync<object?>(ziel, nachricht, ct)));
        }));

    private static async Task<Guid> PostenMitFehlmengeAsync(Microsoft.Extensions.Hosting.IHost host)
    {
        var p = Guid.NewGuid();
        (await FrageAsync(host, new EroeffnePosten(new(p), 100, Mindestbestand: 98), Guid.NewGuid()))!.Success.Should().BeTrue();
        (await FrageAsync(host, new ReservierePosten(new(p), 5, Beleg.Ohne), Guid.NewGuid()))!.Success.Should().BeTrue();   // Fehlmenge 3
        return p;
    }

    private static async Task<int> Nachschuebe(Microsoft.Extensions.Hosting.IHost host, Guid p) =>
        (await host.Services.GetRequiredService<IEventStoreRepository>().ReadStreamAsync(p, 0, default)).Count(e => e.Payload is PostenZugebucht);

    [Fact]
    public async Task Ein_Commit_weckt_die_Funktion_die_sein_Event_hoert_ohne_auf_den_Takt_zu_warten()
    {
        using var host = Cluster("wecker-latenz", takt: Konsum.StandardTakt);   // der lange Takt: sonst bestünde die Prüfung auch ohne Wecker
        await host.StartAsync();
        try
        {
            // Aufwärmen: Aktivierung des Funktions-Actors, erster Takt (2 s nach dem Start).
            var warm = await PostenMitFehlmengeAsync(host);
            await Warte(async () => await Nachschuebe(host, warm) == 1, TimeSpan.FromSeconds(60), "Aufwärmen: der Nachschub kommt nicht");

            // Dreimal hintereinander je unter 5 s: dass der Takt (30 s) dreimal zufällig passt, ist ausgeschlossen.
            for (var i = 0; i < 3; i++)
            {
                var p = await PostenMitFehlmengeAsync(host);
                await Warte(async () => await Nachschuebe(host, p) == 1, Schnell,
                    $"Durchlauf {i + 1}: der Commit weckt die Funktion nicht (nur der Takt holte sie) — fehlt sie im Weckplan oder weckt der Wecker nicht?");
            }
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Eine_kurze_Frist_feuert_in_Sekunden()
    {
        using var host = Cluster("wecker-uhr");
        await host.StartAsync();
        try
        {
            var log = host.Services.GetRequiredService<IEventStoreRepository>();
            for (var i = 0; i < 3; i++)
            {
                var rechnung = Id<Rechnung>.Neu();
                (await FrageAsync(host, new StelleRechnung(rechnung, Id<Kundenkonto>.Neu(), 10m, ZahlungszielSekunden: 1), Guid.NewGuid()))!.Success.Should().BeTrue();
                await Warte(async () => (await log.ReadStreamAsync(rechnung.Wert, 0, default)).Any(e => e.Payload is ZahlungUeberfaellig),
                    TimeSpan.FromSeconds(i == 0 ? 30 : 4), $"Durchlauf {i + 1}: die Frist (1 s) feuert nicht in Sekunden");
            }
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Gehen_die_Weckrufe_eines_Commits_verloren_holt_der_Takt_den_Eingang_genau_einmal_nach()
    {
        var verloren = false;
        using var host = Cluster("wecker-verloren", dienste: Wecker(echt => (ziel, nachricht, ct) =>
            Volatile.Read(ref verloren) ? Task.FromResult<object?>(null) : echt(ziel, nachricht, ct)));
        await host.StartAsync();
        try
        {
            var warm = await PostenMitFehlmengeAsync(host);
            await Warte(async () => await Nachschuebe(host, warm) == 1, TimeSpan.FromSeconds(60), "Aufwärmen: der Nachschub kommt nicht");
            await Task.Delay(TimeSpan.FromSeconds(2));   // der Lauf des Aufwärmens ist zu Ende

            Volatile.Write(ref verloren, true);
            var p = await PostenMitFehlmengeAsync(host);
            await Task.Delay(TimeSpan.FromSeconds(4));
            (await Nachschuebe(host, p)).Should().Be(0, "ohne Weckruf weckt nur der Takt — der Eingang wartet noch");
            Volatile.Write(ref verloren, false);

            // Der Takt (hier der kurze der Prüfdomäne) weckt die Partition; sie liest ab ihrer Position.
            await Warte(async () => await Nachschuebe(host, p) == 1, TimeSpan.FromSeconds(45), "der Takt holt den Eingang nicht nach");
            await Task.Delay(TimeSpan.FromSeconds(3));
            (await Nachschuebe(host, p)).Should().Be(1, "der nachgeholte Eingang wirkt genau einmal");
        }
        finally { await host.StopAsync(); }
    }

    [Fact]
    public async Task Doppelte_Weckrufe_wirken_genau_einmal()
    {
        using var host = Cluster("wecker-doppelt", dienste: Wecker(echt => async (ziel, nachricht, ct) =>
        {
            var erste = echt(ziel, nachricht, ct);
            var zweite = echt(ziel, nachricht, ct);
            await Task.WhenAll(erste, zweite);
            return await erste;
        }));
        await host.StartAsync();
        try
        {
            var posten = new List<Guid>();
            for (var i = 0; i < 6; i++) posten.Add(await PostenMitFehlmengeAsync(host));
            foreach (var p in posten)
                await Warte(async () => await Nachschuebe(host, p) >= 1, TimeSpan.FromSeconds(60), $"Posten {p}: der Nachschub kommt nicht");
            await Task.Delay(TimeSpan.FromSeconds(3));
            foreach (var p in posten)
            {
                (await Nachschuebe(host, p)).Should().Be(1, "jeder Eingang wirkt genau einmal, gleich wie oft geweckt wurde");
                (await Zustand<Posten.Posten>(host.Services, p)).Bestand.Should().Be(103);
            }
        }
        finally { await host.StopAsync(); }
    }
}
