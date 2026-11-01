using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Funktionen;
using Xunit;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Die Werkstatt eines Knotens (docs/04 §4.4): ein Kind gibt einen Eingang einem Arbeiter draußen und nimmt seine Ausgaben in Reihenfolge entgegen —
/// ohne Vermittler, Lease und Festhalten. Store-frei, ohne Actors.
/// </summary>
public class WerkstattTests
{
    private sealed record Eingang(int N);
    private sealed record Weiter(int N);
    private sealed record Fremd;

    private sealed class Arbeiter(string name, TimeSpan? geduld = null) : IArbeiter
    {
        public ConcurrentQueue<AuftragDraussen> Auftraege { get; } = new();
        public string Name => name;
        public TimeSpan? Geduld => geduld;
        public void Sende(AuftragDraussen auftrag) => Auftraege.Enqueue(auftrag);
    }

    private static FunktionsBaustein Funktion(bool strom = false) => new(typeof(object), "IProbe", Draussen: true,
        new[] { new FunktionsEingang(typeof(Eingang), false, strom, new[] { typeof(Weiter) }, Type.EmptyTypes) },
        Array.Empty<FunktionsUmsetzung>(), Array.Empty<Type>(),
        (_, _, _, _, _) => throw new InvalidOperationException("draußen"));

    private static readonly Herkunft H = new(Guid.NewGuid(), 3, DateTimeOffset.UtcNow, null, "funktion:IProbe");

    /// <summary>Startet den Lauf und sammelt seine Ausgaben, bis er endet (oder wirft).</summary>
    private static async Task<List<object>> Sammle(IAsyncEnumerable<object> lauf)
    {
        var aus = new List<object>();
        await foreach (var a in lauf) aus.Add(a);
        return aus;
    }

    private static async Task<AuftragDraussen> Warte(Arbeiter a)
    {
        for (var i = 0; i < 200; i++)
        {
            if (a.Auftraege.TryPeek(out var x)) return x;
            await Task.Delay(10);
        }
        throw new TimeoutException("kein Auftrag beim Arbeiter");
    }

    [Fact]
    public async Task Der_Arbeiter_bekommt_den_Eingang_und_seine_Antwort_kommt_als_Ausgabe_zurueck()
    {
        var w = new Werkstatt();
        var a = new Arbeiter("w1");
        w.Anmelden(a, "IProbe", 2);
        var kennung = Guid.NewGuid();
        var lauf = Sammle(w.RechneAsync(Funktion(), kennung, new Eingang(1), H, "k", false, default));

        var auftrag = await Warte(a);
        auftrag.Vorgang.Should().Be(kennung, "die Kennung des Eingangs ist draußen der Schlüssel (gleich bei jeder Wiederholung)");
        auftrag.Eingang.Should().Be(new Eingang(1));
        w.Ergebnis(a, kennung, new object[] { new Weiter(2) }, 0, true, null).Should().Be(Werkstattausgang.Angenommen);

        (await lauf).Should().Equal(new Weiter(2));
        w.Laeuft(kennung).Should().BeFalse();
    }

    [Fact]
    public async Task Die_erste_Antwort_des_Arbeiters_der_den_Auftrag_haelt_gewinnt_jede_andere_ist_unbekannt()
    {
        var w = new Werkstatt();
        var a = new Arbeiter("w1");
        w.Anmelden(a, "IProbe", 1);
        var kennung = Guid.NewGuid();
        var lauf = Sammle(w.RechneAsync(Funktion(), kennung, new Eingang(1), H, null, false, default));
        await Warte(a);

        w.Ergebnis(new Arbeiter("fremd"), kennung, new object[] { new Weiter(7) }, 0, true, null).Should().Be(Werkstattausgang.Unbekannt,
            "nur der Arbeiter, der den Auftrag hält, antwortet");
        w.Ergebnis(a, kennung, new object[] { new Weiter(1) }, 0, true, null).Should().Be(Werkstattausgang.Angenommen);
        w.Ergebnis(a, kennung, new object[] { new Weiter(9) }, 0, true, null).Should().Be(Werkstattausgang.Unbekannt);
        (await lauf).Should().Equal(new Weiter(1));
    }

    [Fact]
    public async Task Eine_Ausgabe_ausserhalb_der_Signatur_laesst_den_Lauf_scheitern()
    {
        var w = new Werkstatt();
        var a = new Arbeiter("w1");
        w.Anmelden(a, "IProbe", 1);
        var kennung = Guid.NewGuid();
        var lauf = Sammle(w.RechneAsync(Funktion(), kennung, new Eingang(1), H, null, false, default));
        await Warte(a);

        w.Ergebnis(a, kennung, new object[] { new Fremd() }, 0, true, null).Should().Be(Werkstattausgang.Abgelehnt);
        await FluentActions.Awaiting(() => lauf).Should().ThrowAsync<InvalidOperationException>().WithMessage("*außerhalb der Signatur*");
    }

    [Fact]
    public async Task Ohne_Strom_sind_mehrere_Ausgaben_abgelehnt_mit_Strom_kommen_sie_in_Stuecken_in_Reihenfolge()
    {
        var w = new Werkstatt();
        var a = new Arbeiter("w1");
        w.Anmelden(a, "IProbe", 2);

        var einzeln = Guid.NewGuid();
        var lauf1 = Sammle(w.RechneAsync(Funktion(), einzeln, new Eingang(1), H, null, false, default));
        await Warte(a);
        w.Ergebnis(a, einzeln, new object[] { new Weiter(1), new Weiter(2) }, 0, true, null).Should().Be(Werkstattausgang.Abgelehnt);
        await FluentActions.Awaiting(() => lauf1).Should().ThrowAsync<InvalidOperationException>();

        var strom = Guid.NewGuid();
        var lauf2 = Sammle(w.RechneAsync(Funktion(strom: true), strom, new Eingang(2), H, null, false, default));
        await Task.Delay(50);
        w.Ergebnis(a, strom, new object[] { new Weiter(1) }, 0, false, null).Should().Be(Werkstattausgang.Angenommen);
        w.Ergebnis(a, strom, new object[] { new Weiter(1) }, 0, false, null).Should().Be(Werkstattausgang.Angenommen, "ein doppeltes Stück wird übersprungen");
        w.Regt(a, strom).Should().BeTrue();
        w.Ergebnis(a, strom, new object[] { new Weiter(2), new Weiter(3) }, 1, true, null).Should().Be(Werkstattausgang.Angenommen);
        (await lauf2).Should().Equal(new Weiter(1), new Weiter(2), new Weiter(3));
    }

    [Fact]
    public async Task Geht_der_Arbeiter_scheitert_sein_Lauf_sofort_und_ohne_Arbeiter_scheitert_der_naechste()
    {
        var w = new Werkstatt();
        var a = new Arbeiter("w1");
        var gemeldet = new List<(string, int)>();
        w.Beobachte((f, n) => gemeldet.Add((f, n)));
        w.Anmelden(a, "IProbe", 3);
        var lauf = Sammle(w.RechneAsync(Funktion(), Guid.NewGuid(), new Eingang(1), H, null, false, default));
        await Warte(a);

        w.Abmelden(a);
        await FluentActions.Awaiting(() => lauf).Should().ThrowAsync<InvalidOperationException>().WithMessage("*fort*");
        gemeldet.Should().Equal(("IProbe", 3), ("IProbe", 0));
        await FluentActions.Awaiting(() => Sammle(w.RechneAsync(Funktion(), Guid.NewGuid(), new Eingang(2), H, null, false, default)))
            .Should().ThrowAsync<InvalidOperationException>().WithMessage("*kein Arbeiter*");
    }

    [Fact]
    public async Task Ohne_Regung_ueber_die_Geduld_hinaus_scheitert_der_Lauf()
    {
        var w = new Werkstatt();
        w.Anmelden(new Arbeiter("w1", TimeSpan.FromMilliseconds(100)), "IProbe", 1);
        await FluentActions.Awaiting(() => Sammle(w.RechneAsync(Funktion(), Guid.NewGuid(), new Eingang(1), H, null, false, default)))
            .Should().ThrowAsync<TimeoutException>();
    }

    [Fact]
    public async Task Der_Arbeiter_mit_dem_meisten_freien_Platz_bekommt_den_Eingang()
    {
        var w = new Werkstatt();
        var klein = new Arbeiter("klein");
        var gross = new Arbeiter("gross");
        w.Anmelden(klein, "IProbe", 1);
        w.Anmelden(gross, "IProbe", 4);
        w.Slots("IProbe").Should().Be(5);
        using var ende = new CancellationTokenSource();
        var laeufe = Enumerable.Range(0, 5).Select(i => Sammle(w.RechneAsync(Funktion(), Guid.NewGuid(), new Eingang(i), H, null, false, ende.Token))).ToList();
        await Task.Delay(100);

        klein.Auftraege.Should().HaveCount(1);
        gross.Auftraege.Should().HaveCount(4);
        ende.Cancel();
        foreach (var l in laeufe) await FluentActions.Awaiting(() => l).Should().ThrowAsync<OperationCanceledException>();
    }

    [Fact]
    public async Task Ist_kein_Platz_frei_wartet_der_Lauf_und_ein_Platz_wird_mit_der_vollstaendigen_Antwort_frei()
    {
        var w = new Werkstatt();
        var a = new Arbeiter("w1");
        w.Anmelden(a, "IProbe", 1);
        var (k1, k2) = (Guid.NewGuid(), Guid.NewGuid());
        var e1 = w.RechneAsync(Funktion(), k1, new Eingang(1), H, null, false, default).GetAsyncEnumerator();
        var erster = e1.MoveNextAsync().AsTask();
        var lauf2 = Sammle(w.RechneAsync(Funktion(), k2, new Eingang(2), H, null, false, default));
        await Task.Delay(100);
        a.Auftraege.Should().HaveCount(1, "ein Slot: der zweite Lauf wartet auf einen freien Platz");

        w.Ergebnis(a, k1, new object[] { new Weiter(1) }, 0, true, null).Should().Be(Werkstattausgang.Angenommen);
        (await erster).Should().BeTrue("die Ausgabe ist da — das Kind hat sie noch nicht hinausgebracht");
        await Task.Delay(100);
        a.Auftraege.Should().HaveCount(2, "der Platz ist mit der vollständigen Antwort frei, nicht erst, wenn das Kind sie hinausgebracht hat");

        w.Ergebnis(a, k2, new object[] { new Weiter(2) }, 0, true, null).Should().Be(Werkstattausgang.Angenommen);
        (await lauf2).Should().Equal(new Weiter(2));
        (await e1.MoveNextAsync()).Should().BeFalse();
    }
}
