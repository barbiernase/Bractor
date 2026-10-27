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
/// Ablage-Naht des Festhaltens, store-frei: je Schlüssel „F, X“ eine Liste; wer an der Stelle schon etwas findet, verliert (wie die Versionsprüfung
/// im Log). Die Store-Semantik selbst (zwei Schreiber, Absturz) prüfen Integration und Prüfdomäne gegen echtes Marten.
/// </summary>
public sealed class SpeicherAblage : IAusgabeAblage
{
    private readonly Dictionary<Guid, (List<IAusgabe> Ausgaben, bool Fertig)> _f = new();
    public int Festhalten { get; private set; }
    public Func<FunktionsEingabe, int, Task>? VorDemFesthalten { get; set; }

    public Task<Festgehalten> LiesAsync(FunktionsEingabe eingabe, CancellationToken ct)
    {
        lock (_f) return Task.FromResult(_f.TryGetValue(eingabe.Schluessel, out var x) ? new Festgehalten(x.Ausgaben.ToList(), x.Fertig) : Festgehalten.Nichts);
    }

    public async Task<bool> HalteFestAsync(FunktionsEingabe eingabe, int ab, IReadOnlyList<IAusgabe> ausgaben, bool abgeschlossen, CancellationToken ct)
    {
        if (VorDemFesthalten is { } v) await v(eingabe, ab);
        lock (_f)
        {
            var (liste, fertig) = _f.TryGetValue(eingabe.Schluessel, out var x) ? x : (new List<IAusgabe>(), false);
            if (fertig || liste.Count != ab) return false;
            liste.AddRange(ausgaben);
            _f[eingabe.Schluessel] = (liste, abgeschlossen);
            Festhalten++;
            return true;
        }
    }
}

/// <summary>
/// Die Arbeit an einem Eingang (Plan Pub/Sub-Neustrukturierung §2.2, §4.2), store-frei: gleichbleibend ohne Festhalten, sonst erst festhalten, dann aus dem
/// Festgehaltenen senden; ein Strom hält stückweise fest und setzt nach einem Abbruch fort; die erste Antwort gewinnt; Commands tragen abgeleitete Ids.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class FunktionsArbeitTests
{
    private sealed class Ziel : IState { public int Version { get; set; } public Guid Id { get; set; } }
    private sealed record Eingang(int N) : IEvent;
    private sealed record Tu(Id<Ziel> AggregateId, string Was) : ICommand<Ziel>;
    private sealed record Getan(string Was) : IEvent;
    private sealed record Wink(string Was) : ITransientEvent;

    private sealed class Gesendet
    {
        public readonly ConcurrentQueue<(Guid Id, Tu Befehl)> Alle = new();
        public Func<Tu, EmitAusgang> Ausgang = _ => EmitAusgang.Zugestellt;
        public Task<EmitAusgang> SendeAsync(ICommand c, Guid id, Guid korrelation, CancellationToken ct)
        {
            Alle.Enqueue((id, (Tu)c));
            return Task.FromResult(Ausgang((Tu)c));
        }
        public IEnumerable<Guid> Ids => Alle.Select(x => x.Id);
    }

    private static readonly Guid Stream = Guid.NewGuid();
    private static EventEnvelope Ein(int version = 1) => new() { AggregateId = Stream, AggregateVersion = version, Payload = new Eingang(version), CorrelationId = Guid.NewGuid().ToString() };

    private static FunktionsBaustein Baustein(Func<Eingang, Herkunft, CancellationToken, IAsyncEnumerable<IAusgabe>> auf, bool gleichbleibend,
        bool strom = false, bool mitEvents = false, bool draussen = false) =>
        new(typeof(FunktionsArbeitTests), "Probe", draussen, gleichbleibend,
            new[] { new FunktionsEingang(typeof(Eingang), false, strom, mitEvents, new[] { typeof(Tu), typeof(Getan), typeof(Wink) }, Type.EmptyTypes) },
            Array.Empty<FunktionsUmsetzung>(), Type.EmptyTypes,
            (f, e, h, b, ct) => auf((Eingang)e, h, ct),
            (f, s, b, ct) => throw new NotSupportedException());

    private static async IAsyncEnumerable<IAusgabe> Aus(params IAusgabe[] a)
    {
        await Task.Yield();
        foreach (var x in a) yield return x;
    }

    [Fact]
    public async Task Gleichbleibend_sendet_ohne_Festhalten_und_ein_zweiter_Lauf_traegt_dieselben_Ids()
    {
        var ablage = new SpeicherAblage();
        var sende = new Gesendet();
        var ziel = Id<Ziel>.Neu();
        var b = Baustein((e, h, ct) => Aus(new Tu(ziel, "a"), new Wink("w"), new Tu(ziel, "b")), gleichbleibend: true);
        var hinweise = new List<IEvent>();
        var arbeit = new FunktionsArbeit(b, () => new object(), ablage, sende.SendeAsync, h => hinweise.Add(h.Hinweis));

        (await arbeit.VerarbeiteAsync(Ein(), default)).Should().Be(EingangsAusgang.Fertig);
        (await arbeit.VerarbeiteAsync(Ein(), default)).Should().Be(EingangsAusgang.Fertig);

        ablage.Festhalten.Should().Be(0, "eine gleichbleibende Funktion ohne Events schreibt nichts ins Log");
        sende.Alle.Should().HaveCount(4);
        sende.Ids.Distinct().Should().HaveCount(2, "derselbe Eingang ergibt dieselben Command-Ids — die Inbox des Ziels verwirft das Zweite");
        hinweise.Should().HaveCount(2);
    }

    [Fact]
    public async Task Mit_Zustand_laeuft_Auf_je_Eingang_und_Aktivierung_genau_einmal_auch_beim_Wiederholen()
    {
        // Plan §2.9, A5: ein Wiederholen in derselben Aktivierung (Command „unbekannt“) rechnet nicht noch einmal — sonst wirkte der Eingang
        //   zweimal auf den Zustand; gesendet wird dasselbe mit denselben Ids.
        var ablage = new SpeicherAblage();
        var zaehler = 0;
        var ziel = Id<Ziel>.Neu();
        var b = Baustein((e, h, ct) => Aus(new Tu(ziel, $"stand-{Interlocked.Increment(ref zaehler)}"), new Wink("w")), gleichbleibend: false);
        var hinweise = new List<IEvent>();
        var unbekannt = new Gesendet { Ausgang = _ => EmitAusgang.Unbekannt };
        var arbeit = new FunktionsArbeit(b, () => new object(), ablage, unbekannt.SendeAsync, h => hinweise.Add(h.Hinweis));
        var rechnung = new EingangsRechnung();

        (await arbeit.VerarbeiteAsync(Ein(), default, rechnung)).Should().Be(EingangsAusgang.Spaeter);
        (await arbeit.VerarbeiteAsync(Ein(), default, rechnung)).Should().Be(EingangsAusgang.Spaeter);
        zaehler.Should().Be(1, "Auf wirkt einmal auf den Zustand, das Wiederholen sendet nur");
        unbekannt.Ids.Distinct().Should().ContainSingle("dieselbe Stelle, dieselbe Command-Id");
        hinweise.Should().ContainSingle("der Hinweis geht nur mit dem Lauf, der festhält");
    }

    [Fact]
    public async Task Mit_Zustand_rechnet_eine_neue_Aktivierung_einen_festgehaltenen_Eingang_nach_und_sendet_das_Festgehaltene()
    {
        // Nach einem Absturz lädt die Aktivierung den gesicherten Zustand und liest die Log-Eingänge ab der gesicherten Position erneut: Auf läuft auf
        //   dem Zustand (damit jeder Eingang genau einmal auf den gesicherten wirkt), gesendet wird das Festgehaltene, Hinweise nicht noch einmal.
        var ablage = new SpeicherAblage();
        var zaehler = 0;
        var ziel = Id<Ziel>.Neu();
        var b = Baustein((e, h, ct) => Aus(new Tu(ziel, $"stand-{Interlocked.Increment(ref zaehler)}"), new Wink("w")), gleichbleibend: false);
        var hinweise = new List<IEvent>();
        var sende = new Gesendet();
        (await new FunktionsArbeit(b, () => new object(), ablage, sende.SendeAsync, h => hinweise.Add(h.Hinweis))
            .VerarbeiteAsync(Ein(), default, new EingangsRechnung())).Should().Be(EingangsAusgang.Fertig);

        (await new FunktionsArbeit(b, () => new object(), ablage, sende.SendeAsync, h => hinweise.Add(h.Hinweis))
            .VerarbeiteAsync(Ein(), default, new EingangsRechnung())).Should().Be(EingangsAusgang.Fertig);
        zaehler.Should().Be(2, "die neue Aktivierung rechnet auf ihrem (geladenen) Zustand nach");
        sende.Alle.Select(x => x.Befehl.Was).Should().AllBe("stand-1", "es gilt das Festgehaltene");
        sende.Ids.Distinct().Should().ContainSingle();
        hinweise.Should().ContainSingle();
        ablage.Festhalten.Should().Be(1);
    }

    [Fact]
    public async Task Festhaltend_rechnet_einmal_und_ein_zweiter_Lauf_sendet_aus_dem_Festgehaltenen()
    {
        var ablage = new SpeicherAblage();
        var sende = new Gesendet();
        var gerufen = 0;
        var ziel = Id<Ziel>.Neu();
        // Jeder Lauf rechnet anders (wie ein Dienst draußen) — gelten darf nur einer.
        var b = Baustein((e, h, ct) => Aus(new Tu(ziel, $"lauf-{Interlocked.Increment(ref gerufen)}"), new Getan("x")), gleichbleibend: false, mitEvents: true);
        var arbeit = new FunktionsArbeit(b, () => new object(), ablage, sende.SendeAsync);

        (await arbeit.VerarbeiteAsync(Ein(), default)).Should().Be(EingangsAusgang.Fertig);
        (await arbeit.VerarbeiteAsync(Ein(), default)).Should().Be(EingangsAusgang.Fertig);

        gerufen.Should().Be(1, "der zweite Lauf findet den vollständigen Eintrag");
        sende.Alle.Select(x => x.Befehl.Was).Should().AllBe("lauf-1");
        sende.Ids.Distinct().Should().ContainSingle();
    }

    [Fact]
    public async Task Abbruch_nach_dem_Festhalten_vor_den_Commands_sendet_beim_naechsten_Lauf_dasselbe()
    {
        var ablage = new SpeicherAblage();
        var gerufen = 0;
        var ziel = Id<Ziel>.Neu();
        var b = Baustein((e, h, ct) => Aus(new Tu(ziel, $"lauf-{Interlocked.Increment(ref gerufen)}")), gleichbleibend: false);
        var abbruch = new Gesendet { Ausgang = _ => throw new InvalidOperationException("Absturz nach dem Festhalten") };
        await FluentActions.Awaiting(() => new FunktionsArbeit(b, () => new object(), ablage, abbruch.SendeAsync).VerarbeiteAsync(Ein(), default))
            .Should().ThrowAsync<InvalidOperationException>();

        var sende = new Gesendet();
        (await new FunktionsArbeit(b, () => new object(), ablage, sende.SendeAsync).VerarbeiteAsync(Ein(), default)).Should().Be(EingangsAusgang.Fertig);
        gerufen.Should().Be(1);
        sende.Alle.Should().ContainSingle().Which.Befehl.Was.Should().Be("lauf-1");
        sende.Alle.Single().Id.Should().Be(abbruch.Alle.Single().Id, "dieselbe Stelle, dieselbe Command-Id");
    }

    [Fact]
    public async Task Zwei_Laeufe_gleichzeitig_es_gilt_der_zuerst_festgehaltene()
    {
        var ablage = new SpeicherAblage();
        var sende = new Gesendet();
        var ziel = Id<Ziel>.Neu();
        var beideGerechnet = new TaskCompletionSource();
        var zaehler = 0;
        // Beide Läufe warten mit dem Festhalten, bis beide gerechnet haben — dann hält einer fest, der andere verliert.
        ablage.VorDemFesthalten = async (_, _) => { if (Interlocked.Increment(ref zaehler) == 2) beideGerechnet.TrySetResult(); await beideGerechnet.Task; };
        var nummer = 0;
        var b = Baustein((e, h, ct) => Aus(new Tu(ziel, $"lauf-{Interlocked.Increment(ref nummer)}")), gleichbleibend: false);
        var a1 = new FunktionsArbeit(b, () => new object(), ablage, sende.SendeAsync);
        var a2 = new FunktionsArbeit(b, () => new object(), ablage, sende.SendeAsync);

        var ausgaenge = await Task.WhenAll(a1.VerarbeiteAsync(Ein(), default), a2.VerarbeiteAsync(Ein(), default));

        ausgaenge.Should().OnlyContain(a => a == EingangsAusgang.Fertig);
        ablage.Festhalten.Should().Be(1);
        sende.Alle.Select(x => x.Befehl.Was).Distinct().Should().ContainSingle("beide senden aus demselben Eintrag");
        sende.Ids.Distinct().Should().ContainSingle();
    }

    [Fact]
    public async Task Ein_Strom_haelt_stueckweise_fest_und_setzt_nach_einem_Abbruch_bei_der_naechsten_Ausgabe_fort()
    {
        var ablage = new SpeicherAblage();
        var ziel = Id<Ziel>.Neu();
        var gerechnet = new List<int>();
        var brichAb = true;
        async IAsyncEnumerable<IAusgabe> Strom(Eingang e, Herkunft h, [EnumeratorCancellation] CancellationToken ct)
        {
            for (var i = 0; i < 7; i++)
            {
                await Task.Yield();
                if (brichAb && i == 5) throw new InvalidOperationException("Absturz mitten im Strom");
                lock (gerechnet) gerechnet.Add(i);
                yield return new Tu(ziel, "zeile-" + i);
            }
        }
        var b = Baustein(Strom, gleichbleibend: false, strom: true);
        var erste = new Gesendet();
        await FluentActions.Awaiting(() => new FunktionsArbeit(b, () => new object(), ablage, erste.SendeAsync, stueck: 2).VerarbeiteAsync(Ein(), default))
            .Should().ThrowAsync<InvalidOperationException>();
        erste.Alle.Select(x => x.Befehl.Was).Should().Equal("zeile-0", "zeile-1", "zeile-2", "zeile-3");

        brichAb = false;
        gerechnet.Clear();
        var zweite = new Gesendet();
        (await new FunktionsArbeit(b, () => new object(), ablage, zweite.SendeAsync, stueck: 2).VerarbeiteAsync(Ein(), default))
            .Should().Be(EingangsAusgang.Fertig);

        var fest = await ablage.LiesAsync(FunktionsArbeit.EingabeVon(b, Ein()), default);
        fest.Abgeschlossen.Should().BeTrue();
        fest.Ausgaben.Cast<Tu>().Select(t => t.Was).Should().Equal(Enumerable.Range(0, 7).Select(i => "zeile-" + i), "jede Zeile genau einmal festgehalten");
        zweite.Alle.Select(x => x.Befehl.Was).Distinct().Should().BeEquivalentTo(Enumerable.Range(0, 7).Select(i => "zeile-" + i),
            "das schon Festgehaltene wird einmal nachgesendet, der Rest neu");
        erste.Ids.Concat(zweite.Ids).Distinct().Should().HaveCount(7, "jede Stelle hat genau eine Command-Id");
    }

    [Fact]
    public async Task Ein_unbekannter_Emit_Ausgang_heisst_spaeter_ein_Wurf_der_Funktion_Fehler()
    {
        var ziel = Id<Ziel>.Neu();
        var unbekannt = new Gesendet { Ausgang = _ => EmitAusgang.Unbekannt };
        var b = Baustein((e, h, ct) => Aus(new Tu(ziel, "x")), gleichbleibend: true);
        (await new FunktionsArbeit(b, () => new object(), new SpeicherAblage(), unbekannt.SendeAsync).VerarbeiteAsync(Ein(), default))
            .Should().Be(EingangsAusgang.Spaeter);
        var abgelehnt = new Gesendet { Ausgang = _ => EmitAusgang.Abgelehnt };
        (await new FunktionsArbeit(b, () => new object(), new SpeicherAblage(), abgelehnt.SendeAsync).VerarbeiteAsync(Ein(), default))
            .Should().Be(EingangsAusgang.Fertig, "eine Ablehnung ist entschieden");
    }

    [Fact]
    public async Task Hinweise_schickt_nur_der_gewinnende_Lauf()
    {
        var ablage = new SpeicherAblage();
        var ziel = Id<Ziel>.Neu();
        var b = Baustein((e, h, ct) => Aus(new Wink("w"), new Tu(ziel, "x")), gleichbleibend: false);
        var hinweise = 0;
        var arbeit = new FunktionsArbeit(b, () => new object(), ablage, new Gesendet().SendeAsync, _ => Interlocked.Increment(ref hinweise));
        await arbeit.VerarbeiteAsync(Ein(), default);
        await arbeit.VerarbeiteAsync(Ein(), default);
        hinweise.Should().Be(1, "ein Hinweis ist höchstens einmal — der zweite Lauf sendet nur aus dem Festgehaltenen");
    }

    [Fact]
    public async Task Draussen_wird_angeboten_und_nach_der_Antwort_aus_dem_Festgehaltenen_gesendet()
    {
        var ablage = new SpeicherAblage();
        var sende = new Gesendet();
        var ziel = Id<Ziel>.Neu();
        var b = Baustein((e, h, ct) => throw new InvalidOperationException("draußen rechnet niemand im Host"), gleichbleibend: false, draussen: true);
        FunktionsArbeit? arbeit = null;
        var angeboten = new List<EingangAnbieten>();
        arbeit = new FunktionsArbeit(b, null, ablage, sende.SendeAsync, bieteAn: async (angebot, ct) =>
        {
            angeboten.Add(angebot);
            // Der Worker antwortet: hält fest und meldet es dem Actor.
            var eingabe = new FunktionsEingabe("Probe", angebot.Stream, angebot.Version, angebot.KorrelationsText, angebot.Akteur);
            await ablage.HalteFestAsync(eingabe, 0, new IAusgabe[] { new Tu(ziel, "von draußen") }, true, ct);
            arbeit!.Beantwortet(angebot.Vorgang, null);
        }, antwortGeduld: TimeSpan.FromSeconds(5));

        (await arbeit.VerarbeiteAsync(Ein(), default)).Should().Be(EingangsAusgang.Fertig);
        angeboten.Should().ContainSingle().Which.Vorgang.Should().Be(FunktionsIds.Festhalten("Probe", Stream, 1));
        sende.Alle.Should().ContainSingle().Which.Befehl.Was.Should().Be("von draußen");
    }

    [Fact]
    public async Task Draussen_ohne_Antwort_heisst_spaeter_und_ein_gemeldeter_Fehler_Fehler()
    {
        var b = Baustein((e, h, ct) => Aus(), gleichbleibend: false, draussen: true);
        var still = new FunktionsArbeit(b, null, new SpeicherAblage(), new Gesendet().SendeAsync, bieteAn: (_, _) => Task.CompletedTask,
            antwortGeduld: TimeSpan.FromMilliseconds(50));
        (await still.VerarbeiteAsync(Ein(), default)).Should().Be(EingangsAusgang.Spaeter);

        FunktionsArbeit? fehler = null;
        fehler = new FunktionsArbeit(b, null, new SpeicherAblage(), new Gesendet().SendeAsync,
            bieteAn: (a, _) => { fehler!.Beantwortet(a.Vorgang, "kaputt"); return Task.CompletedTask; }, antwortGeduld: TimeSpan.FromSeconds(5));
        (await fehler.VerarbeiteAsync(Ein(), default)).Should().Be(EingangsAusgang.Fehler);
    }

    [Fact]
    public async Task Zwei_Worker_mit_verschiedenen_Antworten_die_erste_gewinnt()
    {
        var ablage = new SpeicherAblage();
        var ziel = Id<Ziel>.Neu();
        var b = Baustein((e, h, ct) => Aus(), gleichbleibend: false, draussen: true);
        var angebot = new EingangAnbieten(FunktionsIds.Festhalten("Probe", Stream, 1), "Probe", new Eingang(1), Stream, 1, DateTimeOffset.UnixEpoch,
            null, null);
        BausteinSitzung Worker(string name) => new(name, n => n == "Probe" ? b : null,
            (f, h, ct) => Task.FromResult<EingaengeZugeteilt?>(new EingaengeZugeteilt(new[] { angebot })),
            (f, m) => Task.CompletedTask, (e, f, _) => Task.CompletedTask, ablage, (f, e, fehler, _) => Task.CompletedTask);
        await using var w1 = Worker("w1");
        await using var w2 = Worker("w2");
        w1.Biete(new[] { ("Probe", 1) });
        w2.Biete(new[] { ("Probe", 1) });
        await Warte(() => w1.Laufende.Count == 1 && w2.Laufende.Count == 1);

        (await w1.AntwortAsync(angebot.Vorgang, new IAusgabe[] { new Tu(ziel, "erste") }, null, default)).Should().Be(ErgebnisAusgang.Geschrieben);
        (await w2.AntwortAsync(angebot.Vorgang, new IAusgabe[] { new Tu(ziel, "zweite") }, null, default)).Should().Be(ErgebnisAusgang.Ueberholt);

        var fest = await ablage.LiesAsync(new FunktionsEingabe("Probe", Stream, 1, null, null), default);
        fest.Ausgaben.Cast<Tu>().Single().Was.Should().Be("erste");
    }

    [Fact]
    public async Task Eine_Antwort_ausserhalb_der_Signatur_wird_abgelehnt()
    {
        var b = Baustein((e, h, ct) => Aus(), gleichbleibend: false, draussen: true);
        var angebot = new EingangAnbieten(Guid.NewGuid(), "Probe", new Eingang(1), Stream, 1, DateTimeOffset.UnixEpoch, null, null);
        string? gemeldet = "—";
        await using var w = new BausteinSitzung("w", n => b, (f, h, ct) => Task.FromResult<EingaengeZugeteilt?>(new EingaengeZugeteilt(new[] { angebot })),
            (f, m) => Task.CompletedTask, (e, f, _) => Task.CompletedTask, new SpeicherAblage(), (f, e, fehler, _) => { gemeldet = fehler; return Task.CompletedTask; });
        w.Biete(new[] { ("Probe", 1) });
        await Warte(() => w.Laufende.Count == 1);
        (await w.AntwortAsync(angebot.Vorgang, new IAusgabe[] { new Eingang(9) }, null, default)).Should().Be(ErgebnisAusgang.Abgelehnt);
        gemeldet.Should().Contain("außerhalb der Signatur");
    }

    private static async Task Warte(Func<bool> bedingung)
    {
        var bis = DateTime.UtcNow.AddSeconds(5);
        while (!bedingung())
        {
            if (DateTime.UtcNow > bis) throw new TimeoutException();
            await Task.Delay(10);
        }
    }
}
