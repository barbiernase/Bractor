using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Bildvorschau.Katalog;
using Client.Infrastructure.Quellen;
using FluentAssertions;
using Infrastructure.Funktionen;
using Infrastructure.Quellen;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Infrastructure.Pruefstand.Quellen;

/// <summary>
/// Der RECHNERDIENST (docs/10 E18, Client.Infrastructure/Quellen) store- und clusterfrei gegen eine Leitung im Speicher: der Läufer aktiviert die
/// Quellart, lauscht und gibt jede Meldung über die Leitung hinein — ein Command an ein Aggregat bzw. ohne Ziel an den Kopf, je mit einer Id aus der Kennung
/// der Meldung und dem Element als Ursache; er wartet auf die Quittung und nimmt dann die nächste. Bewiesen: Ids aus dem Inhalt, auch nach einem Neustart in
/// anderer Reihenfolge dieselben; ohne Quittung geht derselbe Command mit derselben Id noch einmal; das Framework sichert für die Quelle nichts.
/// </summary>
public class RechnerdienstTests
{
    private sealed class Ziel : IState { public int Version { get; set; } public Guid Id { get; set; } }
    private sealed record Buche(Id<Ziel> AggregateId, string Datei) : ICommand<Ziel>;

    /// <summary>Die Meldung der Probe-Quellart: eine gefundene Datei, ihre Kennung ist ihr Name (aus dem Inhalt, nie aus der Reihenfolge).</summary>
    private sealed record Gefunden(string Datei) : IMeldung
    {
        public string Kennung => Datei;
    }

    private static readonly Id<Ziel> Konto = Id<Ziel>.Neu();
    private static readonly Id<Ziel> Gegenkonto = Id<Ziel>.Neu();
    private const string Instanz = "linie1", Quelle = "Kamera", Ort = Instanz + "/" + Quelle, Akteur = "KameraSystem";

    private static Guid Element(string datei) => Kettenvertrag.Element(Ort, new Gefunden(datei));

    private static UebersetzungPlan U<TAus>(string name, Func<Gefunden, TAus> rechne) where TAus : notnull =>
        new(name, typeof(Gefunden), typeof(TAus), x => rechne((Gefunden)x));

    private static readonly UebersetzungPlan Buchen = U("Gefunden_Buche_1", g => new Buche(Konto, g.Datei));
    private static readonly UebersetzungPlan Lesen = U("Gefunden_LiesBild_1", g => new LiesBild(g.Datei, "/bilder/" + g.Datei, 1, DateTimeOffset.UnixEpoch));

    /// <summary>Die Probe-Quellart: je Lauf meldet sie, was <see cref="Meldet"/> für diesen Lauf nennt, und lauscht dann weiter.</summary>
    private sealed class Probe
    {
        public Func<int, string[]> Meldet = _ => [];
        public int Laeufe;

        private sealed class Quellart(Probe p)
        {
            public async IAsyncEnumerable<object> Lausche([EnumeratorCancellation] CancellationToken ct)
            {
                foreach (var d in p.Meldet(Interlocked.Increment(ref p.Laeufe))) yield return new Gefunden(d);
                await Task.Delay(Timeout.Infinite, ct);
            }
        }

        public EingangPlan Plan(params UebersetzungPlan[] uebersetzungen) => new(Instanz, Quelle,
            new FunktionsBaustein(typeof(Quellart), "Probequelle", [], (_, _) => new Quellart(this), [], (u, _, _, _, _, ct) => ((Quellart)u).Lausche(ct),
                null, Lausche: 0, Meldet: [typeof(Gefunden)]),
            Akteur, uebersetzungen.GroupBy(u => u.Ein).ToDictionary(g => g.Key, g => g.ToArray()), new KnotenKonfig(), "linie-1");
    }

    private static async Task Warte(Func<bool> bedingung, string meldung)
    {
        for (var i = 0; i < 200 && !bedingung(); i++) await Task.Delay(50);
        bedingung().Should().BeTrue(meldung);
    }

    /// <summary>Lässt den Läufer laufen, bis <paramref name="bis"/> gilt, und hält ihn dann an (ein Neustart beginnt mit einem neuen Läufer).</summary>
    private static async Task LaufeAsync(EingangPlan plan, Leitungsprobe l, Func<bool> bis, string meldung)
    {
        await using var abgabe = new Abgabe(l, TimeSpan.FromMilliseconds(200));
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await using var dienste = new ServiceCollection().BuildServiceProvider();
        var lauf = new QuellLaeufer(plan, dienste, new Hineingabe(plan, abgabe)).LaufeAsync(cts.Token);
        await Warte(bis, meldung);
        await cts.CancelAsync();
        await lauf;
    }

    [Fact]
    [Trait("Kategorie", "Garantie")]
    public async Task Je_Meldung_ein_Command_an_ein_Aggregat_mit_Id_aus_der_Kennung_und_dem_Element_als_Ursache()
    {
        var p = new Probe { Meldet = _ => ["a.tiff", "b.tiff"] };
        var l = new Leitungsprobe();
        var gegen = U("Gefunden_Buche_2", g => new Buche(Gegenkonto, g.Datei));
        await LaufeAsync(p.Plan(Buchen, gegen), l, () => l.Abgegeben.Count == 4, "je Meldung zwei Commands — zwei Übersetzungen sind eine Gabel");

        foreach (var (command, id, ursache, anAggregat) in l.Abgegeben)
        {
            var b = (Buche)command;
            var element = Element(b.Datei);
            id.Should().Be(Kettenvertrag.Befehl(element, Ort, b.AggregateId.Equals(Konto) ? 0 : 1, b), "die Id folgt aus Instanz, Quelle, Kennung und Stelle");
            ursache.Should().Be(Spur.Ursache(element, 0), "die Ursache ist das Element der Quelle (es steht nicht im Log)");
            anAggregat.Should().BeTrue();
        }
        l.Abgegeben.Select(x => ((Buche)x.Command).Datei).Should().Equal(["a.tiff", "a.tiff", "b.tiff", "b.tiff"], "der Reihe nach, wie die Quelle meldet");
    }

    [Fact]
    public async Task Ein_Command_ohne_Ziel_geht_an_den_Kopf_mit_einer_Id_aus_Element_und_Stelle()
    {
        var p = new Probe { Meldet = _ => ["a.tiff"] };
        var l = new Leitungsprobe();
        await LaufeAsync(p.Plan(Lesen), l, () => l.Abgegeben.Count == 1, "eine Nachricht");
        var (command, id, ursache, anAggregat) = l.Abgegeben.Single();
        command.Should().BeOfType<LiesBild>();
        anAggregat.Should().BeFalse("ein Command ohne Ziel geht als Nachricht an den Kopf — die Sitzung legt den offenen Eingang");
        id.Should().Be(Herkunft.Ableite($"eingang:{Ort}:{Element("a.tiff"):N}:0"));
        ursache.Should().Be(Spur.Ursache(Element("a.tiff"), 0));
    }

    [Fact]
    [Trait("Kategorie", "Garantie")]
    public async Task Nach_einem_Neustart_in_anderer_Reihenfolge_traegt_jedes_Element_dieselben_Ids()
    {
        var p = new Probe { Meldet = lauf => lauf == 1 ? ["a.tiff", "b.tiff"] : ["b.tiff", "a.tiff", "c.tiff"] };
        var erst = new Leitungsprobe();
        await LaufeAsync(p.Plan(Buchen), erst, () => erst.Abgegeben.Count == 2, "der erste Lauf");
        var dann = new Leitungsprobe();
        await LaufeAsync(p.Plan(Buchen), dann, () => dann.Abgegeben.Count == 3, "nach dem Neustart meldet die Quelle alles erneut");
        var ids = erst.Abgegeben.ToDictionary(x => ((Buche)x.Command).Datei, x => x.Id);
        foreach (var (command, id, _, _) in dann.Abgegeben.Where(x => ids.ContainsKey(((Buche)x.Command).Datei)))
            id.Should().Be(ids[((Buche)command).Datei], "dieselbe Kennung ergibt dieselbe Id — die Inbox verwirft die Wiederholung");
    }

    [Fact]
    public async Task Ohne_Quittung_geht_derselbe_Command_mit_derselben_Id_noch_einmal_erst_dann_der_naechste()
    {
        var p = new Probe { Meldet = _ => ["a.tiff", "b.tiff"] };
        var l = new Leitungsprobe { Schweigt = 2 };
        await LaufeAsync(p.Plan(Buchen), l, () => l.Abgegeben.Count == 4, "a dreimal (zweimal geschwiegen), dann b");
        l.Abgegeben.Select(x => ((Buche)x.Command).Datei).Should().Equal("a.tiff", "a.tiff", "a.tiff", "b.tiff");
        l.Abgegeben.Take(3).Select(x => x.Id).Distinct().Should().ContainSingle("eine Wiederholung trägt dieselbe Id");
    }

    [Fact]
    public async Task Bleibt_die_Antwort_ganz_aus_ist_die_Abgabe_unterbrochen_und_der_Laeufer_beginnt_neu()
    {
        var l = new Leitungsprobe { Schweigt = int.MaxValue };
        await using var abgabe = new Abgabe(l, TimeSpan.FromMilliseconds(20));
        await FluentActions.Invoking(() => abgabe.GibAbAsync(new Buche(Konto, "x"), Guid.NewGuid(), "u:0", default)).Should().ThrowAsync<AbgabeUnterbrochen>();
        l.Abgegeben.Should().HaveCount(Abgabe.Versuche);
        l.Verbunden = false;
        await FluentActions.Invoking(() => abgabe.GibAbAsync(new Buche(Konto, "x"), Guid.NewGuid(), "u:0", default)).Should().ThrowAsync<AbgabeUnterbrochen>()
            .WithMessage("*keine Verbindung*");

        var p = new Probe { Meldet = _ => ["a.tiff"] };
        var neu = new Leitungsprobe { Schweigt = Abgabe.Versuche };
        await LaufeAsync(p.Plan(Buchen), neu, () => p.Laeufe >= 2 && neu.Abgegeben.Count > Abgabe.Versuche,
            "nach der Unterbrechung beginnt der Läufer neu, die Quelle meldet erneut, dann kommt die Quittung");
    }
}
