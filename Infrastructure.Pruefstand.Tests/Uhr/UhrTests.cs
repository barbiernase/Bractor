using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Quellen;
using Infrastructure.Quellen.Treiber.Takt;
using Infrastructure.Uhr;
using Xunit;

namespace Infrastructure.Pruefstand.Uhr;

/// <summary>
/// Die Uhr (Plan Pub/Sub-Neustrukturierung §2.2, §4.2, Phase 3), store-frei: eine Frist ist Ausgabe eines Decide und geht als <see cref="FristGesetzt"/>
/// ins Log; die Uhr feuert fällige Fristen mit einer Command-Id aus dem Frist-Event — doppelt gefeuert, dieselbe Id —, nimmt sie erst bei
/// entschiedenem Ausgang aus dem Verzeichnis und lässt sie bei unbekanntem liegen. Dazu der Takt-Treiber: je Intervall genau eine Meldung.
/// </summary>
[Trait("Kategorie", "Garantie")]
public class UhrTests
{
    private sealed class Ziel : IState { public int Version { get; set; } public Guid Id { get; set; } }
    private sealed record Pruefe(Id<Ziel> AggregateId) : ICommand<Ziel>;

    private sealed class Verzeichnis : IFaelligkeiten
    {
        public readonly List<FaelligeFrist> Zeilen = new();
        public Func<Guid, int, Task>? BeimErledigen;
        public Task<IReadOnlyList<FaelligeFrist>> FaelligeAsync(int hoechstens, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<FaelligeFrist>>(Zeilen.Take(hoechstens).ToList());
        public async Task ErledigtAsync(Guid stream, int version, CancellationToken ct)
        {
            if (BeimErledigen is { } b) await b(stream, version);
            Zeilen.RemoveAll(z => z.Stream == stream && z.Version == version);
        }
    }

    private static FaelligeFrist Zeile(Guid stream, int version) => new(stream, version, "Pruefe", "{}", "anna");

    [Fact]
    public void Eine_Frist_aus_dem_Decide_wird_FristGesetzt_ein_Event_bleibt_es_selbst()
    {
        var befehl = new Pruefe(Id<Ziel>.Neu());
        var als = Entscheidungsausgabe.Als(new Frist<Pruefe>(befehl, TimeSpan.FromDays(14)));
        als.Should().BeOfType<FristGesetzt>().Which.Should().Be(new FristGesetzt(befehl, TimeSpan.FromDays(14)));
        als.Should().BeAssignableTo<IProzessIntern>("der Applier sieht sie nie, die Version zählt sie");
        var e = new FristGesetzt(befehl, TimeSpan.Zero);
        Entscheidungsausgabe.Als(e).Should().BeSameAs(e);
    }

    [Fact]
    public async Task Entschieden_verlaesst_das_Verzeichnis_unbekannt_bleibt_liegen_und_feuert_mit_derselben_Id_erneut()
    {
        var v = new Verzeichnis();
        var (a, b) = (Guid.NewGuid(), Guid.NewGuid());
        v.Zeilen.AddRange(new[] { Zeile(a, 2), Zeile(b, 5) });
        var ids = new List<Guid>();
        var unbekannt = true;
        var werk = new Uhrwerk(v, (cmd, id, korrelation, ct) =>
        {
            lock (ids) ids.Add(id);
            return Task.FromResult(korrelation == b && unbekannt ? EmitAusgang.Unbekannt : EmitAusgang.Zugestellt);
        }, befehl: (_, _) => new Pruefe(Id<Ziel>.Neu()));

        (await werk.LaufAsync(default)).Should().Be(1);
        v.Zeilen.Should().ContainSingle(z => z.Stream == b, "unbekannt — der nächste Takt feuert erneut");
        unbekannt = false;
        (await werk.LaufAsync(default)).Should().Be(1);
        v.Zeilen.Should().BeEmpty();
        ids.Should().HaveCount(3);
        ids.Where(i => i == Uhrwerk.BefehlsId(b, 5)).Should().HaveCount(2, "jedes Feuern derselben Frist trägt dieselbe Command-Id");
        Uhrwerk.BefehlsId(a, 2).Should().NotBe(Uhrwerk.BefehlsId(a, 3));
    }

    [Fact]
    public async Task Absturz_zwischen_Feuern_und_Austragen_feuert_beim_naechsten_Takt_mit_derselben_Id()
    {
        var v = new Verzeichnis();
        var s = Guid.NewGuid();
        v.Zeilen.Add(Zeile(s, 3));
        var ids = new List<Guid>();
        var werk = new Uhrwerk(v, (cmd, id, k, ct) => { ids.Add(id); return Task.FromResult(EmitAusgang.Zugestellt); },
            befehl: (_, _) => new Pruefe(Id<Ziel>.Neu()));
        v.BeimErledigen = (_, _) => throw new InvalidOperationException("Absturz nach dem Senden, vor dem Austragen");
        await FluentActions.Awaiting(() => werk.LaufAsync(default)).Should().ThrowAsync<InvalidOperationException>();
        v.BeimErledigen = null;
        (await werk.LaufAsync(default)).Should().Be(1);
        ids.Should().HaveCount(2).And.OnlyContain(i => i == Uhrwerk.BefehlsId(s, 3), "doppelt gefeuert, einmal gewirkt (Inbox des Ziels)");
    }

    // ── Takt-Treiber ─────────────────────────────────────────────────────────────────────────────────────────────────

    private sealed record Minutentakt : TaktGemeldet;

    private static readonly QuellTyp Takt = new(typeof(Minutentakt), Treibervertrag.Takt, "Minutentakt", "quelle:Minutentakt", null,
        t => new Minutentakt { Zeitpunkt = t.Zeitpunkt, Kennung = t.Kennung });

    [Fact]
    public void Der_Intervall_Beginn_und_die_Kennung_sind_auf_jedem_Knoten_gleich()
    {
        var zeit = new DateTimeOffset(2026, 10, 26, 14, 37, 12, TimeSpan.FromHours(2));
        var beginn = TaktTreiber.IntervallBeginn(zeit, TimeSpan.FromMinutes(15));
        beginn.Should().Be(new DateTimeOffset(2026, 10, 26, 12, 30, 0, TimeSpan.Zero));
        TaktTreiber.Kennung(beginn).Should().Be("2026-10-26T12:30:00Z");
        TaktTreiber.IntervallBeginn(zeit.AddMinutes(7), TimeSpan.FromMinutes(15)).Should().Be(beginn.AddMinutes(15));
    }

    [Fact]
    public async Task Je_Intervall_genau_eine_Meldung_auch_nach_einem_Neustart_dieselbe_Kennung()
    {
        var jetzt = new DateTimeOffset(2026, 10, 26, 12, 0, 1, TimeSpan.Zero);
        var gemeldet = new List<Minutentakt>();
        TaktTreiber Neu() => new(Takt, new TaktQuellKonfig { Intervall = TimeSpan.FromMinutes(1) },
            (n, _) => { gemeldet.Add((Minutentakt)n); return Task.CompletedTask; }, _ => Task.FromResult(jetzt), new QuellenZustand());

        var t = Neu();
        await t.ProbeAsync(default);
        jetzt = jetzt.AddSeconds(30);
        await t.ProbeAsync(default);
        gemeldet.Should().ContainSingle("dasselbe Intervall meldet ein Treiber nur einmal");
        jetzt = jetzt.AddSeconds(31);
        (await t.ProbeAsync(default)).Should().BeLessThanOrEqualTo(TimeSpan.FromSeconds(1));
        gemeldet.Select(m => m.Kennung).Should().Equal("2026-10-26T12:00:00Z", "2026-10-26T12:01:00Z");

        await Neu().ProbeAsync(default);   // Neustart oder zweiter Knoten im selben Intervall
        gemeldet.Last().Kennung.Should().Be("2026-10-26T12:01:00Z", "dieselbe Kennung — das Log nimmt sie genau einmal (Version 0 bzw. Verzeichnis)");
    }

    [Fact]
    public void Ein_Intervall_unter_einer_Sekunde_ist_ein_Fehler()
        => FluentActions.Invoking(() => new TaktTreiber(Takt, new TaktQuellKonfig { Intervall = TimeSpan.FromMilliseconds(200) },
                (_, _) => Task.CompletedTask, _ => Task.FromResult(DateTimeOffset.UnixEpoch), new QuellenZustand()))
            .Should().Throw<ArgumentOutOfRangeException>();
}
