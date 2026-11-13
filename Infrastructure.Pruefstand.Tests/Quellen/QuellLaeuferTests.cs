using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Bildvorschau.Katalog;
using FluentAssertions;
using Infrastructure.Eingaenge;
using Infrastructure.Funktionen;
using Infrastructure.Quellen;
using Microsoft.Extensions.DependencyInjection;
using Proto;
using Xunit;

namespace Infrastructure.Pruefstand.Quellen;

/// <summary>
/// Der LÄUFER eines Eingangs aus einer Quelle (docs/04 §4.7, <c>QuellLaeufer</c>), store-frei in einem echten lokalen Actor-System: er aktiviert die
/// Quellart mit ihrem gesicherten Zustand, lauscht, übersetzt jede Meldung mit den Übersetzungen der Akteur-Seite und gibt die Nachricht im Namen des
/// Akteurs hinein — ein Command an ein Aggregat über den Emitter (eine Naht), ein Command ohne Ziel als offener Eingang am Kopf (Tabelle und Wege
/// als Fakes). Bewiesen: je Meldung eine Nachricht mit Ids aus der Kennung der Meldung und der Spur des Eingangs; nach einem Neustart in anderer
/// Reihenfolge trägt jede Id dasselbe Element; bei voller Grenze wartet der Läufer, bis Platz ist; gesichert wird erst, wenn hineingegeben ist.
/// </summary>
public class QuellLaeuferTests
{
    private sealed class Ziel : IState { public int Version { get; set; } public Guid Id { get; set; } }
    private sealed record Buche(Id<Ziel> AggregateId, string Datei) : ICommand<Ziel>;

    /// <summary>Die Meldung der Probe-Quellart: eine gefundene Datei, ihre Kennung ist ihr Name (aus dem Inhalt, nie aus der Reihenfolge).</summary>
    private sealed record Gefunden(string Datei) : IMeldung
    {
        public string Kennung => Datei;
    }

    /// <summary>Der Zustand der Quellart: was sie gesehen hat.</summary>
    private sealed class Gesehen { public List<string> Dateien { get; set; } = new(); }

    private static readonly FunktionsZustandsform Form = new(typeof(Gesehen), () => new Gesehen(),
        o => Encoding.UTF8.GetBytes(string.Join("\n", ((Gesehen)o).Dateien)),
        (o, b) => ((Gesehen)o).Dateien = Encoding.UTF8.GetString(b).Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList(), _ => { });

    private static readonly Id<Ziel> Konto = Id<Ziel>.Neu();
    private static readonly Id<Ziel> Gegenkonto = Id<Ziel>.Neu();
    private const string Client = "Linie", Name = "kamera", Ort = Client + "/" + Name, Akteur = "KameraSystem", Instanz = "bildaufbereitung";

    private static Guid Element(string datei) => Kettenvertrag.Element(Ort, new Gefunden(datei));

    private static UebersetzungPlan U<TAus>(string name, Func<Gefunden, TAus> rechne) where TAus : notnull =>
        new(name, typeof(Gefunden), typeof(TAus), x => rechne((Gefunden)x));

    private static readonly UebersetzungPlan Buchen = U("Gefunden_Buche_1", g => new Buche(Konto, g.Datei));
    private static readonly UebersetzungPlan Lesen = U("Gefunden_LiesBild_1", g => new LiesBild(g.Datei, "/bilder/" + g.Datei, 1, DateTimeOffset.UnixEpoch));

    /// <summary>Eine Welt: ein lokales Actor-System, der Emitter als Naht, die offenen Eingänge, die Wege und die Zustandsablage im Speicher.</summary>
    private sealed class Welt : IAsyncDisposable, IFunktionsWege
    {
        public readonly ActorSystem System = new();
        public readonly ConcurrentQueue<(IAggregatCommand Befehl, Guid Id, Guid Korrelation, string? Akteur, Spur.Kopf? Spur)> Gesendet = new();
        public readonly ConcurrentQueue<(EingangOffen Eingang, Guid Stream, string? Akteur)> Gelegt = new();
        public readonly ConcurrentQueue<EingangZustellen> Gemeldet = new();
        public readonly ConcurrentDictionary<Guid, bool> Fertig = new();
        public int NachgesehenMal;
        public readonly Ablage Zustaende = new();
        /// <summary>Was die Quellart in ihrem n-ten Lauf meldet (ab 1) — was ihr Zustand schon kennt, überspringt sie, außer sie <see cref="Vergisst"/>.</summary>
        public Func<int, string[]> Meldet = _ => [];
        /// <summary>Die Quellart meldet nach einem Neustart alles noch einmal (wie ein Absender, der wiederholt) — ihr Zustand zählt nicht.</summary>
        public bool Vergisst;
        /// <summary>Hält den Emitter an, bevor er antwortet (der Command ist dann schon zugestellt, die Antwort steht aus).</summary>
        public Func<IAggregatCommand, CancellationToken, Task> Antworte = (_, _) => Task.CompletedTask;
        public int Laeufe;
        private readonly ServiceProvider _dienste = new ServiceCollection().BuildServiceProvider();
        private readonly Offene _offene;

        public Welt() => _offene = new Offene(this);

        private sealed class Quellart(Gesehen gesehen, Welt w)
        {
            public async IAsyncEnumerable<object> Lausche([EnumeratorCancellation] CancellationToken ct)
            {
                var lauf = Interlocked.Increment(ref w.Laeufe);
                foreach (var d in w.Meldet(lauf))
                {
                    if (!w.Vergisst && gesehen.Dateien.Contains(d)) continue;
                    if (!gesehen.Dateien.Contains(d)) gesehen.Dateien.Add(d);
                    yield return new Gefunden(d);
                }
                await Task.Delay(Timeout.Infinite, ct);
            }
        }

        private FunktionsBaustein Art => new(typeof(Quellart), "Probequelle", [], (_, z) => new Quellart((Gesehen)z!, this), [],
            (u, _, _, _, _, ct) => ((Quellart)u).Lausche(ct), Form, Lausche: 0, Meldet: [typeof(Gefunden)]);

        public EingangPlan Plan(int offen, params UebersetzungPlan[] uebersetzungen) => new(Client, Name, Art, Akteur,
            uebersetzungen.GroupBy(u => u.Ein).ToDictionary(g => g.Key, g => g.ToArray()), new KnotenKonfig(), null, offen);

        /// <summary>Startet den Läufer des Eingangs (wie die Wurzel auf seinem Rechner).</summary>
        public PID Starte(EingangPlan plan) => System.Root.Spawn(Props.FromProducer(() => new QuellLaeufer(plan, _dienste, SendeAsync,
            new Verkettung(new Dictionary<Type, string[]> { [typeof(LiesBild)] = [Instanz] }), _offene, this, Zustaende)));

        /// <summary>Der Host fällt aus: der Läufer ist fort (sein Lauschen bricht ab).</summary>
        public Task AbsturzAsync(PID laeufer) => System.Root.StopAsync(laeufer);

        private async Task<Infrastructure.Emit.EmitAntwort> SendeAsync(IAggregatCommand c, Guid id, Guid korrelation, CancellationToken ct)
        {
            Gesendet.Enqueue((c, id, korrelation, ImAuftrag.Akteur, Spur.Aktuell));
            await Antworte(c, ct);
            return new Infrastructure.Emit.EmitAntwort(EmitAusgang.Zugestellt, Array.Empty<Antwortereignis>());
        }

        public Task<EingangQuittung?> StelleZuAsync(EingangZustellen eingang, TimeSpan frist, CancellationToken ct) => throw new NotSupportedException();

        public void Melde(EingangZustellen eingang) => Gemeldet.Enqueue(eingang);

        /// <summary>Der gesicherte Zustand des Eingangs (unter <c>eingang:&lt;Client&gt;</c> und seinem Namen), oder null.</summary>
        public (long Nummer, List<string> Dateien)? Gesichert =>
            Zustaende.Je.TryGetValue(("eingang:" + Client, Name), out var z)
                ? (z.Nummer, Encoding.UTF8.GetString(Zustandsbild.Entpacke(z.Bild)[""]).Split('\n', StringSplitOptions.RemoveEmptyEntries).ToList())
                : null;

        public sealed class Ablage : IZustandsAblage
        {
            public readonly ConcurrentDictionary<(string, string), GesicherterZustand> Je = new();
            public Task<GesicherterZustand?> LadeAsync(string instanz, string knoten, CancellationToken ct) =>
                Task.FromResult(Je.TryGetValue((instanz, knoten), out var z) ? z : null);
            public Task SichereAsync(IReadOnlyCollection<(string Instanz, string Knoten, GesicherterZustand Zustand)> zustaende, CancellationToken ct)
            {
                foreach (var (i, k, z) in zustaende) Je[(i, k)] = z;
                return Task.CompletedTask;
            }
        }

        private sealed class Offene(Welt w) : IOffeneEingaenge
        {
            public Task<IReadOnlyList<OffenerEingang>> FaelligeAsync(int hoechstens, CancellationToken ct) => throw new NotSupportedException();
            public Task ErledigtAsync(IReadOnlyCollection<Guid> kennungen, CancellationToken ct) => throw new NotSupportedException();
            public Task VerschiebeAsync(IReadOnlyCollection<Guid> kennungen, TimeSpan nach, CancellationToken ct) => throw new NotSupportedException();
            public Task ParkeAsync(IReadOnlyCollection<(Guid Kennung, string Grund)> geparkt, CancellationToken ct) => throw new NotSupportedException();
            public Task LegeAsync(IReadOnlyList<EingangOffen> eingaenge, Guid stream, CancellationToken ct)
            {
                foreach (var e in eingaenge) w.Gelegt.Enqueue((e, stream, ImAuftrag.Akteur));
                return Task.CompletedTask;
            }
            public Task<IReadOnlyCollection<Guid>> NochOffenAsync(IReadOnlyCollection<Guid> kennungen, CancellationToken ct)
            {
                Interlocked.Increment(ref w.NachgesehenMal);
                return Task.FromResult<IReadOnlyCollection<Guid>>(kennungen.Where(k => !w.Fertig.ContainsKey(k)).ToList());
            }
        }

        public async ValueTask DisposeAsync()
        {
            await System.ShutdownAsync();
            await _dienste.DisposeAsync();
        }
    }

    private static async Task Warte(Func<bool> bedingung, string meldung)
    {
        for (var i = 0; i < 200 && !bedingung(); i++) await Task.Delay(50);
        bedingung().Should().BeTrue(meldung);
    }

    // ── (a) je Meldung eine Nachricht im Namen des Akteurs, Ids aus der Kennung der Meldung ─────────────────────────────

    [Fact]
    public async Task Je_Meldung_ein_Command_an_ein_Aggregat_im_Namen_des_Akteurs_mit_Id_aus_der_Kennung_der_Meldung()
    {
        await using var w = new Welt { Meldet = _ => ["a.tiff", "b.tiff"] };
        var gegen = U("Gefunden_Buche_2", g => new Buche(Gegenkonto, g.Datei));
        w.Starte(w.Plan(0, Buchen, gegen));
        await Warte(() => w.Gesendet.Count == 4, "je Meldung zwei Commands — zwei Übersetzungen der Akteur-Seite sind eine Gabel");

        foreach (var (befehl, id, korrelation, akteur, spur) in w.Gesendet)
        {
            var b = (Buche)befehl;
            var element = Element(b.Datei);
            var stelle = b.AggregateId.Equals(Konto) ? 0 : 1;
            id.Should().Be(Kettenvertrag.Befehl(element, Ort, stelle, b), "die Id folgt aus Eingang, Kennung der Meldung, Übersetzung (Stelle) und Command");
            korrelation.Should().Be(element);
            akteur.Should().Be(Akteur, "der Läufer gibt hinein wie eine App, im Namen des Akteurs des Eingangs");
            spur.Should().NotBeNull();
            spur!.Ursache.Should().Be(Spur.Ursache(element, 0), "die Ursache ist das Element der Quelle (es steht nicht im Log)");
            spur.Wirker.Should().Be(WirkerId.FuerEingang(Client, Name)).And.Be("eingang:Linie/kamera", "der Wirker ist der Eingang (W9)");
        }
        w.Gesendet.Select(x => ((Buche)x.Befehl).Datei).Should().Equal(["a.tiff", "a.tiff", "b.tiff", "b.tiff"], "der Reihe nach, wie die Quelle meldet");
        w.Gelegt.Should().BeEmpty("ein Command an ein Aggregat geht über den Emitter, nicht über cqrs_eingaenge");
    }

    [Fact]
    public async Task Ein_Command_ohne_Ziel_wird_ein_offener_Eingang_am_Kopf_im_Namen_des_Akteurs_mit_dem_Element_als_Ursache()
    {
        await using var w = new Welt { Meldet = _ => ["a.tiff", "b.tiff"] };
        w.Starte(w.Plan(0, Lesen));
        await Warte(() => w.Gelegt.Count == 2 && w.Gemeldet.Count == 2, "je Meldung ein offener Eingang und der schnelle Weg");

        var gelegt = w.Gelegt.ToList();
        gelegt.Select(g => g.Stream).Should().Equal([Element("a.tiff"), Element("b.tiff")], "die Zeile gehört zum Element (Stream = seine Kennung)");
        gelegt.Should().OnlyContain(g => g.Eingang.Instanz == Instanz && g.Eingang.Akteur == Akteur && g.Eingang.Version == 0 && g.Akteur == Akteur,
            "am Kopf der Instanz, deren Graph LiesBild nimmt — im Namen des Kamerasystems");
        gelegt.Select(g => g.Eingang.Korrelation).Should().Equal(Element("a.tiff").ToString(), Element("b.tiff").ToString());
        gelegt.Select(g => g.Eingang.Kennung).Distinct().Should().HaveCount(2);

        var gemeldet = w.Gemeldet.ToList();
        gemeldet.Select(e => e.Kennung).Should().Equal(gelegt.Select(g => g.Eingang.Kennung), "der schnelle Weg trägt die Kennung der Zeile");
        gemeldet.Select(e => e.Nachricht).Should().Equal(new LiesBild("a.tiff", "/bilder/a.tiff", 1, DateTimeOffset.UnixEpoch),
            new LiesBild("b.tiff", "/bilder/b.tiff", 1, DateTimeOffset.UnixEpoch));
        gemeldet.Should().OnlyContain(e => e.Art == Eingangsart.Durabel && e.Instanz == Instanz && e.Akteur == Akteur && e.Version == 0);
        gemeldet.Select(e => e.Ursache).Should().Equal([Spur.Ursache(Element("a.tiff"), 0), Spur.Ursache(Element("b.tiff"), 0)],
            "die Kette trägt das Element der Quelle als Ursache");
        w.Gesendet.Should().BeEmpty();
    }

    // ── (b) Neustart: dieselben Elemente in anderer Reihenfolge ─────────────────────────────────────────────────────────

    [Fact]
    [Trait("Kategorie", "Garantie")]
    public async Task Meldet_eine_Quelle_nach_einem_Neustart_dieselben_Elemente_in_anderer_Reihenfolge_traegt_jede_Id_dasselbe_Element()
    {
        // Vor dem Absturz: a ist hineingegeben, b zugestellt, aber ohne Antwort (nicht gesichert). Danach meldet die Quelle b vor a (ein Scan sortiert
        //   anders, als die Dateien fertig wurden; ein Absender wiederholt). Mit Ids aus einer laufenden Nummer bekäme b die Id eines anderen Elements —
        //   die Inbox verwürfe das eine als Wiederholung, das andere wirkte doppelt.
        await using var w = new Welt { Vergisst = true, Meldet = lauf => lauf == 1 ? ["a.tiff", "b.tiff"] : ["b.tiff", "a.tiff"] };
        var haengt = true;
        w.Antworte = async (c, ct) => { if (haengt && ((Buche)c).Datei == "b.tiff") await Task.Delay(Timeout.Infinite, ct); };
        var laeufer = w.Starte(w.Plan(0, Buchen));
        await Warte(() => w.Gesendet.Count == 2, "a ist hineingegeben, b zugestellt — die Antwort steht aus");
        w.Gesichert!.Value.Nummer.Should().Be(1, "gesichert ist nur a");

        await w.AbsturzAsync(laeufer);
        haengt = false;
        laeufer = w.Starte(w.Plan(0, Buchen));
        await Warte(() => w.Gesendet.Count == 4, "nach dem Neustart: b und a noch einmal");

        foreach (var je in w.Gesendet.GroupBy(x => x.Id))
            je.Select(x => x.Befehl).Distinct().Should().ContainSingle("eine Id trägt immer dasselbe Element — sonst verwürfe die Inbox ein anderes");
        w.Gesendet.Select(x => x.Id).Distinct().Should().HaveCount(2, "zwei Elemente, zwei Ids — jedes wirkt über die Inbox einmal");
        w.Gesendet.Select(x => x.Id).Should().Equal(Kettenvertrag.Befehl(Element("a.tiff"), Ort, 0, new Buche(Konto, "a.tiff")),
            Kettenvertrag.Befehl(Element("b.tiff"), Ort, 0, new Buche(Konto, "b.tiff")),
            Kettenvertrag.Befehl(Element("b.tiff"), Ort, 0, new Buche(Konto, "b.tiff")),
            Kettenvertrag.Befehl(Element("a.tiff"), Ort, 0, new Buche(Konto, "a.tiff")));
    }

    [Fact]
    [Trait("Kategorie", "Garantie")]
    public async Task Ein_offener_Eingang_traegt_nach_einem_Neustart_dieselbe_Kennung_und_wird_nicht_doppelt_gelegt()
    {
        await using var w = new Welt { Vergisst = true, Meldet = lauf => lauf == 1 ? ["a.tiff", "b.tiff"] : ["b.tiff", "a.tiff"] };
        var laeufer = w.Starte(w.Plan(0, Lesen));
        await Warte(() => w.Gelegt.Count == 2, "beide gelegt");
        await w.AbsturzAsync(laeufer);
        w.Starte(w.Plan(0, Lesen));
        await Warte(() => w.Gelegt.Count == 4, "nach dem Neustart noch einmal gelegt");

        var je = w.Gelegt.GroupBy(g => g.Eingang.Kennung).ToList();
        je.Should().HaveCount(2, "dasselbe Element ergibt dieselbe Kennung — die Tabelle legt keine zweite Zeile");
        foreach (var g in je) g.Select(x => x.Eingang.Daten).Distinct().Should().ContainSingle("eine Kennung trägt immer dasselbe Element");
    }

    // ── (c) Gegendruck ─────────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Bei_voller_Grenze_wartet_der_Laeufer_bis_ein_hineingegebenes_Element_fertig_ist()
    {
        await using var w = new Welt { Meldet = _ => ["a.tiff", "b.tiff", "c.tiff"] };
        w.Starte(w.Plan(2, Lesen));
        await Warte(() => w.Gelegt.Count == 2, "zwei Elemente gehen hinein — so viele erlaubt der Eingang offen");
        await Task.Delay(QuellLaeufer.Nachsehen * 3);
        w.Gelegt.Should().HaveCount(2, "a und b sind offen — der Läufer nimmt c nicht");
        w.NachgesehenMal.Should().BeGreaterThan(1, "er sieht wiederholt nach, was noch offen ist");
        w.Gesichert!.Value.Dateien.Should().Equal(["a.tiff", "b.tiff"], "c ist noch nicht hineingegeben");

        w.Fertig[w.Gelegt.First().Eingang.Kennung] = true;   // a ist fertig gerechnet: seine Zeile verlässt die Tabelle
        await Warte(() => w.Gelegt.Count == 3, "ist a fertig, ist Platz: c geht hinein");
        w.Gelegt.Last().Stream.Should().Be(Element("c.tiff"));
        await Warte(() => w.Gesichert?.Nummer == 3, "nach c gesichert");
    }

    // ── (d) Zustand: gesichert nach dem Hineingeben, beim Neustart geladen ──────────────────────────────────────────────

    [Fact]
    [Trait("Kategorie", "Garantie")]
    public async Task Der_Zustand_der_Quellart_wird_erst_gesichert_wenn_ihre_Nachricht_hineingegeben_ist_und_traegt_ueber_einen_Neustart()
    {
        await using var w = new Welt { Meldet = lauf => lauf == 1 ? ["a.tiff", "b.tiff"] : ["a.tiff", "b.tiff", "c.tiff"] };
        var freigabe = new TaskCompletionSource();
        w.Antworte = (c, ct) => ((Buche)c).Datei == "b.tiff" ? freigabe.Task.WaitAsync(ct) : Task.CompletedTask;
        var laeufer = w.Starte(w.Plan(0, Buchen));

        await Warte(() => w.Gesendet.Count == 2, "a hineingegeben, b zugestellt — die Antwort steht aus");
        await Task.Delay(300);
        w.Gesichert.Should().NotBeNull("nach a gesichert");
        w.Gesichert!.Value.Nummer.Should().Be(1);
        w.Gesichert!.Value.Dateien.Should().Equal(["a.tiff"], "b ist noch nicht durabel hineingegeben — der gesicherte Stand kennt es nicht");

        freigabe.SetResult();
        await Warte(() => w.Gesichert?.Nummer == 2, "nach b gesichert");
        w.Gesichert!.Value.Dateien.Should().Equal(["a.tiff", "b.tiff"]);

        // Neustart: die Quellart beginnt mit dem gesicherten Stand — was er enthält, meldet sie nicht wieder.
        await w.AbsturzAsync(laeufer);
        w.Starte(w.Plan(0, Buchen));
        await Warte(() => w.Gesendet.Count == 3, "nach dem Neustart nur das Neue");
        await Task.Delay(300);
        w.Gesendet.Select(x => ((Buche)x.Befehl).Datei).Should().Equal(["a.tiff", "b.tiff", "c.tiff"], "a und b stehen im Zustand, c ist neu");
        w.Gesichert!.Value.Nummer.Should().Be(3, "die Nummer läuft mit dem gesicherten Stand weiter");
        w.Gesichert!.Value.Dateien.Should().Equal(["a.tiff", "b.tiff", "c.tiff"]);
    }

    [Fact]
    public async Task Eine_Meldung_ohne_Uebersetzung_geht_nicht_hinein_der_Laeufer_lauscht_weiter()
    {
        await using var w = new Welt { Meldet = _ => ["a.tiff"] };
        w.Starte(w.Plan(0));   // keine Übersetzung für Gefunden (der Start ließe das nicht zu — hier die Rückfallebene)
        await Warte(() => w.Gesichert is not null, "die Meldung zählt als gesehen");
        w.Gesendet.Should().BeEmpty();
        w.Gelegt.Should().BeEmpty();
    }
}
