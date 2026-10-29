using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Lebenszyklus;
using Infrastructure.Persistence;
using Infrastructure.Projections;
using Infrastructure.Wecken;
using Proto.Cluster;
using Xunit;

namespace Infrastructure.Pruefstand.Wecken;

/// <summary>
/// Der Wecker am Commit (docs/03 §3.5): je geschriebenem Event die Ziele aus dem Weckplan, je Commit und Ziel einmal —
/// die Partition des Streams bzw. die eine Adresse; Events, die niemand hört, wecken niemanden. Die Start-Prüfung findet einen Hörer, der nicht im
/// Plan steht.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class WeckerTests
{
    private sealed record Gebucht(int Betrag) : IEvent;
    private sealed record Storniert : IEvent;
    private sealed record Unbeachtet : IEvent;

    private static readonly IReadOnlyDictionary<Type, Weckziel[]> Plan = new Dictionary<Type, Weckziel[]>
    {
        [typeof(Gebucht)] = new[] { new Weckziel("batch-Konto"), new Weckziel("funktion-Statistik", EineAdresse: true) },
        [typeof(Storniert)] = new[] { new Weckziel("batch-Konto") },
    };

    private static (Wecker Wecker, ConcurrentBag<ClusterIdentity> Gesendet) Mit(Func<ClusterIdentity, Task<object?>>? antwort = null)
    {
        var gesendet = new ConcurrentBag<ClusterIdentity>();
        var bote = new Bote((ziel, _, _) => { gesendet.Add(ziel); return antwort?.Invoke(ziel) ?? Task.FromResult<object?>(new WakeAck()); });
        return (new Wecker(Plan, new Konsum(8, 500), () => bote), gesendet);
    }

    [Fact]
    public void Ein_Commit_weckt_je_Ziel_einmal_die_Partition_des_Streams_bzw_die_eine_Adresse()
    {
        var (wecker, _) = Mit();
        var stream = Guid.NewGuid();
        var partition = Partitionierung.Von(stream, 8).ToString();

        wecker.Ziele(stream, new IEvent[] { new Gebucht(1), new Gebucht(2), new Storniert(), new Unbeachtet() })
            .Should().BeEquivalentTo(new[] { ("batch-Konto", partition), ("funktion-Statistik", "0") }, "drei Events, zwei Ziele — je Ziel einmal");
        wecker.Ziele(stream, new IEvent[] { new Unbeachtet() }).Should().BeEmpty("ein Event, das niemand hört, weckt niemanden");
    }

    [Fact]
    public async Task Ein_Group_Commit_weckt_die_Ziele_aller_Appends_und_jedes_nur_einmal()
    {
        var (wecker, gesendet) = Mit();
        var a = Guid.NewGuid();
        var b = Enumerable.Range(0, 100).Select(_ => Guid.NewGuid()).First(g => Partitionierung.Von(g, 8) != Partitionierung.Von(a, 8));

        wecker.Geschrieben(new[]
        {
            new BatchAppend(a, 1, new[] { Teil(new Gebucht(1)) }, "Konto"),
            new BatchAppend(b, 3, new[] { Teil(new Storniert()), Teil(new Gebucht(2)) }, "Konto"),
        });

        await Warte(() => gesendet.Count >= 3);
        gesendet.Select(z => (z.Kind, z.Identity)).Should().BeEquivalentTo(new[]
        {
            ("batch-Konto", Partitionierung.Von(a, 8).ToString()), ("batch-Konto", Partitionierung.Von(b, 8).ToString()), ("funktion-Statistik", "0"),
        });
    }

    [Fact]
    public async Task Ein_verlorener_Weckruf_haelt_den_naechsten_nicht_auf()
    {
        // Der erste Weckruf an jedes Ziel geht verloren (die Anfrage wirft); der nächste Commit weckt trotzdem.
        var verloren = new ConcurrentDictionary<string, bool>();
        var (wecker, gesendet) = Mit(ziel => verloren.TryAdd(ziel.ToString(), true)
            ? Task.FromException<object?>(new TimeoutException("verloren")) : Task.FromResult<object?>(new WakeAck()));
        var stream = Guid.NewGuid();

        wecker.Geschrieben(stream, new[] { Teil(new Storniert()) });
        await Warte(() => gesendet.Count >= 1);
        await Task.Delay(50);
        wecker.Geschrieben(stream, new[] { Teil(new Storniert()) });

        await Warte(() => gesendet.Count >= 2);
        gesendet.Should().HaveCount(2, "der zweite Commit weckt erneut, der verlorene Weckruf blockiert nichts");
    }

    [Fact]
    public void Die_Startpruefung_findet_einen_Hoerer_der_nicht_im_Plan_steht()
    {
        var gut = new[] { new BatchRegistrierung("batch-Konto", "konto", new[] { typeof(Gebucht), typeof(Storniert) }),
            new BatchRegistrierung("funktion-Statistik", "statistik", new[] { typeof(Gebucht) }, EineAdresse: true) };
        Wecker.Pruefe(Plan, gut);

        var fehlt = () => Wecker.Pruefe(Plan, new[] { new BatchRegistrierung("batch-Konto", "konto", new[] { typeof(Unbeachtet) }) });
        fehlt.Should().Throw<Infrastructure.Umgebung.BootFehler>().WithMessage("*Unbeachtet*");

        var falscheAdresse = () => Wecker.Pruefe(Plan, new[] { new BatchRegistrierung("funktion-Statistik", "statistik", new[] { typeof(Gebucht) }) });
        falscheAdresse.Should().Throw<Infrastructure.Umgebung.BootFehler>("der Plan weckt die eine Adresse, der Konsument liest aber je Partition");
    }

    private static AppendTeil Teil(IEvent e) => new(new[] { e }, null, null, null, null, null);

    private static async Task Warte(Func<bool> bedingung)
    {
        for (var i = 0; i < 200 && !bedingung(); i++) await Task.Delay(10);
        bedingung().Should().BeTrue();
    }
}
