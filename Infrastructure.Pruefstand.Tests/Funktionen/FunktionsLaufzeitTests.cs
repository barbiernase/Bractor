using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Eingaenge;
using Infrastructure.Funktionen;
using Microsoft.Extensions.DependencyInjection;
using Proto;
using Xunit;

namespace Infrastructure.Pruefstand.Funktionen;

/// <summary>
/// Die Laufzeit der Graphen vor dem Aggregat (docs/04 §4.4), store-frei in einem echten lokalen Actor-System: je Instanz ein Graph-Actor (Halter und
/// Kopf), je Knoten ein Parent (ein Schritt der Kette: Warteschlange, Kinder bis Parallel, Wiederholungen, Wegtabelle, „fertig“ erst nach den gesicherten
/// Ausgaben), je Umsetzung ein Kind (Commands mit Ids aus Aufruf, Ort und Stelle). Die Pläne baut der Test von Hand wie der Generator (Wege aus den
/// Typen, verlierbar je Quelle); der Emitter ist eine Naht. Bewiesen: die Folge mit ihren Ids, zwei Werte an einem Schritt (zweimal gerechnet), Wahl,
/// Strom, eine verlierbare Kante (hält den Kopf nicht auf), Rückstau, der gescheiterte Kopf, der Neustart vom Kopf mit denselben Ids, die Quelle und ein
/// Zustand, der nach einem Absturz nichts doppelt zählt.
/// </summary>
public class FunktionsLaufzeitTests
{
    private sealed class Ziel : IState { public int Version { get; set; } public Guid Id { get; set; } }
    private sealed record Buche(Id<Ziel> AggregateId, int Wert) : ICommand<Ziel>;
    private sealed record Start(int Wert) : ICommand;
    private sealed record X(int Wert) : ICommand;
    private sealed record Z(int Wert) : ICommand;
    private sealed record Links(int Wert) : ICommand;
    private sealed record Rechts(int Wert) : ICommand;
    private sealed record Teil(int Wert) : ICommand;
    private sealed record Gemerkt(int Wert) : ITransientEvent;
    private sealed record Falsch(int Wert) : IEvent;
    private sealed record Gebucht(int Wert) : IEvent;
    private sealed record Bestaetige(Id<Ziel> AggregateId) : ICommand<Ziel>;
    private sealed class Zaehler { public int N { get; set; } }

    private static readonly Guid Stream = Guid.NewGuid();
    private static readonly Id<Ziel> Konto = Id<Ziel>.Neu();

    // ── Pläne wie der Generator: je Ausgabe die Wege zu den Knoten, die ihren Typ nehmen; Köpfe = Eingänge ohne Erzeuger ──

    private sealed class K(string name, FunktionsRechnung rechne)
    {
        public string Name { get; } = name;
        public FunktionsRechnung Rechne { get; } = rechne;
        public List<(int Auf, Type? Eingang, Type[] Aus)> Ops { get; } = new();
        public FunktionsOrtKonfig Konfig { get; set; } = new();
        public KnotenKonfig Knoten { get; } = new();
        public Dictionary<Type, int> Antworten { get; } = new();
        public Action? BeimBau { get; set; }
        /// <summary>Von welchen Knoten dieser verlierbar bekommt.</summary>
        public HashSet<string> Verlierbar { get; } = new();
        /// <summary>Mit Zustand: ein <see cref="Zaehler"/>, die Umsetzung ist er selbst.</summary>
        public bool MitZustand { get; set; }
        /// <summary>Eine Quelle: die Nummer ihres Lausche (eine Operation ohne Eingang).</summary>
        public int? Lauscht { get; private set; }

        public K Lausche(params Type[] aus)
        {
            Lauscht = Ops.Count + Antworten.Count;
            Ops.Add((Lauscht.Value, null, aus));
            return this;
        }

        public K Op<TEingang>(params Type[] aus)
        {
            Ops.Add((Ops.Count + Antworten.Count, typeof(TEingang), aus));
            return this;
        }

        public K Antwort<TEvent>(params Type[] aus)
        {
            var nr = Ops.Count + Antworten.Count;
            Antworten[typeof(TEvent)] = nr;
            // Die Ausgaben einer Antwort gehören zu jeder Operation, die Commands an Aggregate gibt (wie im Generator).
            for (var i = 0; i < Ops.Count; i++)
                if (Ops[i].Aus.Any(t => typeof(IAggregatCommand).IsAssignableFrom(t))) Ops[i] = Ops[i] with { Aus = Ops[i].Aus.Concat(aus).Distinct().ToArray() };
            return this;
        }
    }

    private static GraphPlan Plan(string name, IReadOnlyList<K> knoten, IReadOnlyList<UebersetzungPlan>? uebersetzungen = null)
    {
        var u = uebersetzungen ?? [];
        WegPlan[] An(Type typ, string von) =>
            (from n in knoten where n.Name != von
             from o2 in n.Ops where o2.Eingang == typ
             select new WegPlan(typ, n.Name, o2.Auf, n.Verlierbar.Contains(von))).ToArray();
        var plaene = knoten.Select(k => new KnotenPlan(k.Name, k.Name, k.Ops.Select(o => new OperationPlan(o.Auf, o.Eingang, o.Aus,
            o.Aus.SelectMany(t => An(t, k.Name).Concat(u.Where(x => x.Ein == t).Select(x => new WegPlan(t, x.Name, 0)))).ToArray())).ToList())).ToList();
        var erzeugt = knoten.SelectMany(k => k.Ops.SelectMany(o => o.Aus)).Concat(u.Select(x => x.Aus)).ToHashSet();
        var koepfe = knoten.SelectMany(k => k.Ops.Where(o => o.Eingang is not null && !erzeugt.Contains(o.Eingang)).Select(o => (o.Eingang!, k.Name)))
            .Concat(u.Where(x => !erzeugt.Contains(x.Ein)).Select(x => (x.Ein, x.Name)))
            .GroupBy(x => x.Item1).ToDictionary(g => g.Key, g => g.Select(x => x.Name).Distinct().ToArray());
        return new GraphPlan(name, typeof(object), plaene, koepfe, Array.Empty<Type>(), knoten.FirstOrDefault(k => k.Lauscht is not null)?.Name,
            u.Select(x => x with { Wege = An(x.Aus, x.Name) }).ToList());
    }

    /// <summary>Eine Übersetzung wie der Generator sie erzeugt: <typeparamref name="TEin"/> → genau eine Nachricht (ihre Wege rechnet der Plan).</summary>
    private static UebersetzungPlan U<TEin, TAus>(string name, Func<TEin, TAus> rechne) where TEin : notnull where TAus : notnull =>
        new(name, typeof(TEin), typeof(TAus), x => rechne((TEin)x), []);

    private static readonly FunktionsZustandsform ZaehlerForm = new(typeof(Zaehler), () => new Zaehler(), o => BitConverter.GetBytes(((Zaehler)o).N),
        (o, b) => ((Zaehler)o).N = BitConverter.ToInt32(b), _ => { });

    /// <summary>Eine Welt: Graph-Actors im lokalen System, Wege dazwischen, ein Emitter, der mitschreibt.</summary>
    private sealed class Welt : IAsyncDisposable, IFunktionsWege
    {
        public readonly ActorSystem System = new();
        public readonly ConcurrentQueue<(IAggregatCommand Befehl, Guid Id)> Gesendet = new();
        /// <summary>Was verlierbar über die Wege ging (der schnelle Weg eines offenen Eingangs).</summary>
        public readonly ConcurrentQueue<EingangZustellen> Gemeldet = new();
        /// <summary>Die Hinweise an den Verteiler (an die Akteure, die sie hören): aus Knoten und gescheiterten Ketten.</summary>
        public readonly ConcurrentQueue<HinweisZustellen> Hinweise = new();
        public readonly ConcurrentQueue<Guid> Ausgetragen = new();
        public readonly ConcurrentQueue<(Guid, string)> Geparkt = new();
        public readonly Ablage Zustaende = new();
        public Func<IAggregatCommand, EmitAusgang> Ausgang = _ => EmitAusgang.Zugestellt;
        public Func<IAggregatCommand, IReadOnlyList<Antwortereignis>> Antwort = _ => Array.Empty<Antwortereignis>();
        public Verkettung Koepfe = Verkettung.Leer;
        private readonly Dictionary<string, PID> _instanzen = new();
        private readonly ServiceProvider _dienste = new ServiceCollection().BuildServiceProvider();

        public GraphInstanz Instanz(string name, params K[] knoten) => Instanz(name, [], knoten);

        public GraphInstanz Instanz(string name, UebersetzungPlan[] uebersetzungen, params K[] knoten)
        {
            var plan = Plan(name, knoten, uebersetzungen);
            var instanz = new GraphInstanz(name, plan, null, knoten.ToDictionary(k => k.Name, k => k.Knoten));
            var parents = knoten.ToDictionary(k => k.Name, k =>
            {
                var traeger = new FunktionsTraeger(k.Name, sp => new FunktionsAktivierung(sp, (_, z) => { k.BeimBau?.Invoke(); return z ?? new object(); },
                        k.MitZustand ? ZaehlerForm : null, name: k.Name, knoten: new KnotenKontext(name, k.Name, k.Knoten), ablage: Zustaende,
                        quelle: k.Lauscht is not null),
                    k.Rechne, Antworten: k.Antworten)
                { Instanz = name, Knoten = k.Name, ImGraphen = plan.ImGraphen(k.Name), Uebersetzungen = plan.UebersetzungenVon(k.Name),
                    Zeitlimit = k.Knoten.Zeitlimit, Lausche = k.Lauscht };
                return (Func<FunktionsParent>)(() => new FunktionsParent(traeger, k.Konfig, () => new FunktionsKindActor(traeger, _dienste, this, Koepfe, SendeAsync,
                    hinweise: Hinweise.Enqueue), new KnotenWege(instanz, k.Name), k.MitZustand ? Zustaende : null));
            });
            Starte(instanz, parents);
            _plaene[name] = (instanz, parents);
            return instanz;
        }

        private readonly Dictionary<string, (GraphInstanz, Dictionary<string, Func<FunktionsParent>>)> _plaene = new();

        private void Starte(GraphInstanz instanz, Dictionary<string, Func<FunktionsParent>> parents) =>
            _instanzen[instanz.Name] = System.Root.SpawnNamed(Props.FromProducer(() => new GraphActor(instanz, parents, new Offene(this), Hinweise.Enqueue,
                ablage: Zustaende, sende: SendeAsync)), FunktionenWurzel.Name(instanz.Name) + "-" + Guid.NewGuid().ToString("N")[..6]);

        /// <summary>Dieser Cluster-Knoten hält die Instanz (bzw. nicht mehr): ihre Quelle lauscht.</summary>
        public void Halte(string name, bool halten) => System.Root.Send(_instanzen[name], new QuelleHalten(halten));

        /// <summary>Der Cluster-Knoten fällt aus: der Graph-Actor samt Parents und Kindern ist fort, ein neuer beginnt leer.</summary>
        public async Task AbsturzAsync(string name)
        {
            await System.Root.StopAsync(_instanzen[name]);
            var (instanz, parents) = _plaene[name];
            Starte(instanz, parents);
        }

        private Task<Infrastructure.Emit.EmitAntwort> SendeAsync(IAggregatCommand c, Guid id, Guid korrelation, CancellationToken ct)
        {
            Gesendet.Enqueue((c, id));
            var ausgang = Ausgang(c);
            return Task.FromResult(new Infrastructure.Emit.EmitAntwort(ausgang, ausgang == EmitAusgang.Zugestellt ? Antwort(c) : Array.Empty<Antwortereignis>()));
        }

        public async Task<EingangQuittung?> StelleZuAsync(EingangZustellen eingang, TimeSpan frist, CancellationToken ct)
        {
            if (!_instanzen.TryGetValue(eingang.Instanz, out var p)) return new EingangQuittung(eingang.Kennung, Quittungsart.Spaeter);
            try { return await System.Root.RequestAsync<EingangQuittung>(p, eingang, frist); }
            catch (TimeoutException) { return null; }
        }

        public void Melde(EingangZustellen eingang)
        {
            Gemeldet.Enqueue(eingang);
            if (_instanzen.TryGetValue(eingang.Instanz, out var p)) System.Root.Send(p, eingang);
        }

        /// <summary>Die gesicherten Zustände im Speicher (je Instanz und Knoten der neueste), mit jeder Sicherung in Reihenfolge.</summary>
        public sealed class Ablage : IZustandsAblage
        {
            public readonly ConcurrentDictionary<(string, string), GesicherterZustand> Je = new();
            public readonly ConcurrentQueue<long> Gesichert = new();
            public Task<GesicherterZustand?> LadeAsync(string instanz, string knoten, CancellationToken ct) =>
                Task.FromResult(Je.TryGetValue((instanz, knoten), out var z) ? z : null);
            public Task SichereAsync(IReadOnlyCollection<(string Instanz, string Knoten, GesicherterZustand Zustand)> zustaende, CancellationToken ct)
            {
                foreach (var (i, k, z) in zustaende) { Je[(i, k)] = z; Gesichert.Enqueue(z.Nummer); }
                return Task.CompletedTask;
            }
        }

        private sealed class Offene(Welt w) : IOffeneEingaenge
        {
            public Task<IReadOnlyList<OffenerEingang>> FaelligeAsync(int hoechstens, CancellationToken ct) => throw new NotSupportedException();
            public Task ErledigtAsync(IReadOnlyCollection<Guid> kennungen, CancellationToken ct)
            {
                foreach (var k in kennungen) w.Ausgetragen.Enqueue(k);
                return Task.CompletedTask;
            }
            public Task VerschiebeAsync(IReadOnlyCollection<Guid> kennungen, TimeSpan nach, CancellationToken ct) => throw new NotSupportedException();
            public Task LegeAsync(IReadOnlyList<EingangOffen> eingaenge, Guid stream, CancellationToken ct) => throw new NotSupportedException();
            public Task ParkeAsync(IReadOnlyCollection<(Guid Kennung, string Grund)> geparkt, CancellationToken ct)
            {
                foreach (var g in geparkt) w.Geparkt.Enqueue(g);
                return Task.CompletedTask;
            }
        }

        public async ValueTask DisposeAsync()
        {
            await System.ShutdownAsync();
            await _dienste.DisposeAsync();
        }
    }

    private static EingangZustellen Eingang(string instanz, object nachricht, Eingangsart art = Eingangsart.Kette, Guid? kennung = null) =>
        new(kennung ?? Guid.NewGuid(), instanz, nachricht, art, Stream, 7, DateTimeOffset.UnixEpoch, null, null, null);

    private static FunktionsRechnung Einzeln(Func<object?[], object?> f) => (_, _, a, _, _, _) => Strom(f(a));

    private static async IAsyncEnumerable<object> Strom(params object?[] xs)
    {
        foreach (var x in xs) if (x is not null) yield return x;
        await Task.CompletedTask;
    }

    private static async Task Warte(Func<bool> bedingung, string meldung)
    {
        for (var i = 0; i < 100 && !bedingung(); i++) await Task.Delay(50);
        bedingung().Should().BeTrue(meldung);
    }

    private static async Task<Quittungsart?> Fertig(Welt w, EingangZustellen e, int sekunden = 10) =>
        (await w.StelleZuAsync(e, TimeSpan.FromSeconds(sekunden), default))?.Ausgang;

    /// <summary>Die Kennung des Aufrufs am Kopf (aus der Kennung des Eingangs).</summary>
    private static Guid AmKopf(Guid eingang, string ort, int auf = 0) => Kettenvertrag.Aufruf(eingang, ort, auf);

    /// <summary>Die Kennung des Aufrufs, den <paramref name="von"/> mit seiner Ausgabe an <paramref name="stelle"/> bei <paramref name="ort"/> auslöst.</summary>
    private static Guid Danach(Guid von, string vonOrt, string ort, int stelle = 0, int auf = 0) => Kettenvertrag.Aufruf(Kettenvertrag.Weiter(von, vonOrt, stelle), ort, auf);

    // ── Übersetzungen (docs/04 §4.4): inline, wo die Nachricht gerade ist — hinter einer Funktion in ihrem Kind, am Kopf im Graph-Actor ──────────

    [Fact]
    public async Task Eine_Uebersetzung_hinter_einer_Funktion_rechnet_im_Kind_und_schickt_mit_ihrem_Ort()
    {
        await using var w = new Welt();
        w.Instanz("G", [U<X, Buche>("Ergebnis", x => new Buche(Konto, x.Wert + 1))],
            new K("A", Einzeln(a => new X(((Start)a[0]!).Wert * 2))).Op<Start>(typeof(X)));

        var eingang = Eingang("G", new Start(21));
        (await Fertig(w, eingang)).Should().Be(Quittungsart.Fertig, "fertig erst, wenn der übersetzte Command beantwortet ist");
        var a = AmKopf(eingang.Kennung, "G/A");
        w.Gesendet.Should().Equal((new Buche(Konto, 43), Kettenvertrag.Befehl(a, "G/Ergebnis", 0, new Buche(Konto, 43))));
    }

    [Fact]
    public async Task Eine_Uebersetzung_am_Kopf_gibt_an_den_Knoten_der_sie_nennt()
    {
        await using var w = new Welt();
        w.Instanz("G", [U<Start, X>("Hinein", s => new X(s.Wert * 10))],
            new K("B", Einzeln(a => new Buche(Konto, ((X)a[0]!).Wert))).Op<X>(typeof(Buche)));

        var eingang = Eingang("G", new Start(4));
        (await Fertig(w, eingang)).Should().Be(Quittungsart.Fertig);
        var b = Kettenvertrag.Aufruf(Kettenvertrag.Weiter(eingang.Kennung, "G/Hinein", 0), "G/B", 0);
        w.Gesendet.Should().Equal((new Buche(Konto, 40), Kettenvertrag.Befehl(b, "G/B", 0, new Buche(Konto, 40))));
    }

    [Fact]
    public async Task Eine_Uebersetzung_am_Kopf_direkt_an_ein_Aggregat_rechnet_nach_einem_Absturz_mit_derselben_Id()
    {
        await using var w = new Welt();
        w.Instanz("G", [U<Start, Buche>("Hinein", s => new Buche(Konto, s.Wert))]);

        var eingang = Eingang("G", new Start(7));
        (await Fertig(w, eingang)).Should().Be(Quittungsart.Fertig, "der Graph-Actor wartet auf die Antwort des Ziels");
        await w.AbsturzAsync("G");
        (await Fertig(w, eingang)).Should().Be(Quittungsart.Fertig);
        var id = Kettenvertrag.Befehl(eingang.Kennung, "G/Hinein", 0, new Buche(Konto, 7));
        w.Gesendet.Should().Equal((new Buche(Konto, 7), id), (new Buche(Konto, 7), id));
    }

    [Fact]
    public async Task Eine_Uebersetzung_zwischen_zwei_Knoten_und_eine_Gabel_daneben()
    {
        await using var w = new Welt();
        w.Instanz("G", [U<X, Z>("Mitte", x => new Z(x.Wert + 100))],
            new K("A", Einzeln(a => new X(((Start)a[0]!).Wert))).Op<Start>(typeof(X)),
            new K("B", Einzeln(a => new Buche(Konto, ((Z)a[0]!).Wert))).Op<Z>(typeof(Buche)),
            new K("C", Einzeln(a => new Buche(Konto, -((X)a[0]!).Wert))).Op<X>(typeof(Buche)));

        var eingang = Eingang("G", new Start(5));
        (await Fertig(w, eingang)).Should().Be(Quittungsart.Fertig, "fertig erst, wenn beide Zweige fertig sind");
        w.Gesendet.Select(x => x.Befehl).Should().BeEquivalentTo([new Buche(Konto, 105), new Buche(Konto, -5)],
            "X geht übersetzt an B und unübersetzt an C");
        var a = AmKopf(eingang.Kennung, "G/A");
        w.Gesendet.Should().Contain((new Buche(Konto, 105),
            Kettenvertrag.Befehl(Kettenvertrag.Aufruf(Kettenvertrag.Weiter(a, "G/A", 0), "G/B", 0), "G/B", 0, new Buche(Konto, 105))),
            "die Übersetzung im Kind gibt Z mit der Stelle des Aufrufs an den Parent, der den Weg der Übersetzung nimmt");
    }

    // ── Folge, Kennung, Rand ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Eine_Folge_bis_zum_Command_mit_Ids_aus_der_Kette()
    {
        await using var w = new Welt();
        w.Instanz("G",
            new K("A", Einzeln(a => new X(((Start)a[0]!).Wert * 2))).Op<Start>(typeof(X)),
            new K("B", (_, _, a, _, _, _) => Strom(new Buche(Konto, ((X)a[0]!).Wert), new Buche(Konto, ((X)a[0]!).Wert + 1)))
                .Op<X>(typeof(Buche)));

        var eingang = Eingang("G", new Start(21));
        (await Fertig(w, eingang)).Should().Be(Quittungsart.Fertig, "fertig ist die Kette erst, wenn B fertig ist — und B erst, wenn seine Commands beantwortet sind");

        var b = Danach(AmKopf(eingang.Kennung, "G/A"), "G/A", "G/B");
        w.Gesendet.Select(x => x.Befehl).Should().Equal(new Buche(Konto, 42), new Buche(Konto, 43));
        w.Gesendet.Select(x => x.Id).Should().Equal(Kettenvertrag.Befehl(b, "G/B", 0, new Buche(Konto, 42)), Kettenvertrag.Befehl(b, "G/B", 1, new Buche(Konto, 43)));
    }

    [Fact]
    public async Task Ein_doppelter_Kopf_wird_an_der_Kennung_erkannt_fertig_rechnet_nicht_neu()
    {
        await using var w = new Welt();
        var gerechnet = 0;
        w.Instanz("G", new K("A", Einzeln(a => { Interlocked.Increment(ref gerechnet); return new Buche(Konto, ((Start)a[0]!).Wert); }))
            .Op<Start>(typeof(Buche)));
        var eingang = Eingang("G", new Start(1));
        (await Fertig(w, eingang)).Should().Be(Quittungsart.Fertig);
        (await Fertig(w, eingang)).Should().Be(Quittungsart.Fertig);
        gerechnet.Should().Be(1, "ein fertiger Kopf bekommt sofort „fertig“");
        w.Gesendet.Should().HaveCount(1);
    }

    [Fact]
    public async Task Durabel_heisst_sofort_angenommen_den_fertigen_Kopf_traegt_der_Graph_Actor_aus()
    {
        await using var w = new Welt();
        w.Instanz("G", new K("A", Einzeln(a => new Buche(Konto, ((Start)a[0]!).Wert))).Op<Start>(typeof(Buche)));
        var eingang = Eingang("G", new Start(1), Eingangsart.Durabel);
        (await Fertig(w, eingang)).Should().Be(Quittungsart.Angenommen);
        await Warte(() => w.Ausgetragen.Contains(eingang.Kennung), "der fertige durable Kopf wird nicht ausgetragen");
    }

    [Fact]
    public async Task Volle_Warteschlange_des_Kopfs_heisst_spaeter_Parallel_begrenzt_die_Kinder()
    {
        await using var w = new Welt();
        var frei = new TaskCompletionSource();
        var laufen = 0;
        var hoechstens = 0;
        var a = new K("A", (_, _, _, _, _, ct) => Blockiert(ct)) { Konfig = new FunktionsOrtKonfig(Parallel: 2) }.Op<Start>(typeof(Buche));
        a.Knoten.Warteschlange = 3;
        w.Instanz("G", a);

        async IAsyncEnumerable<object> Blockiert([EnumeratorCancellation] CancellationToken ct)
        {
            var n = Interlocked.Increment(ref laufen);
            InterlockedMax(ref hoechstens, n);
            await frei.Task.WaitAsync(ct);
            Interlocked.Decrement(ref laufen);
            yield break;
        }

        var quittungen = new List<Task<EingangQuittung?>>();
        for (var i = 0; i < 3; i++) quittungen.Add(w.StelleZuAsync(Eingang("G", new Start(i)), TimeSpan.FromSeconds(10), default));
        await Warte(() => Volatile.Read(ref laufen) == 2, "zwei Kinder rechnen");
        (await Fertig(w, Eingang("G", new Start(9)), 5)).Should().Be(Quittungsart.Spaeter, "drei Ketten sind offen — die Warteschlange des Kopfs (3) ist voll");
        frei.SetResult();
        foreach (var q in quittungen) (await q)!.Ausgang.Should().Be(Quittungsart.Fertig);
        hoechstens.Should().Be(2, "nie mehr als Parallel Kinder zugleich");
    }

    private static void InterlockedMax(ref int ziel, int wert)
    {
        int alt;
        while ((alt = Volatile.Read(ref ziel)) < wert && Interlocked.CompareExchange(ref ziel, wert, alt) != alt) { }
    }

    [Fact]
    public async Task Vorwaermen_baut_die_Kinder_beim_Start_ohne_Vorwaermen_beim_ersten_Aufruf()
    {
        await using var w = new Welt();
        var gebaut = 0;
        w.Instanz("Warm", new K("A", Einzeln(_ => null)) { Konfig = new FunktionsOrtKonfig(Parallel: 3, Vorwaermen: true), BeimBau = () => Interlocked.Increment(ref gebaut) }
            .Op<Start>(typeof(Buche)));
        await Warte(() => Volatile.Read(ref gebaut) == 3, "vorgewärmt baut der Parent alle Kinder beim Start");
        var kalt = 0;
        w.Instanz("Kalt", new K("A", Einzeln(_ => null)) { Konfig = new FunktionsOrtKonfig(Parallel: 3), BeimBau = () => Interlocked.Increment(ref kalt) }
            .Op<Start>(typeof(Buche)));
        await Task.Delay(300);
        kalt.Should().Be(0);
        (await Fertig(w, Eingang("Kalt", new Start(1)))).Should().Be(Quittungsart.Fertig);
        kalt.Should().Be(1, "das erste Kind entsteht mit dem ersten Aufruf");
    }

    [Fact]
    public async Task Ein_Hinweis_geht_verlierbar_nur_an_die_Akteure_ein_Event_als_Ausgabe_laesst_die_Kette_scheitern()
    {
        await using var w = new Welt();
        w.Instanz("G", new K("A", Einzeln(a => new Gemerkt(((Start)a[0]!).Wert))).Op<Start>(typeof(Gemerkt)));
        var schreiber = new K("S", Einzeln(_ => new Falsch(1))) { Konfig = new FunktionsOrtKonfig(Wiederholungen: 0) }.Op<Start>(typeof(Buche));
        w.Instanz("Schreiber", schreiber);

        (await Fertig(w, Eingang("G", new Start(5)))).Should().Be(Quittungsart.Fertig, "auf einen Hinweis wird nicht gewartet");
        w.Hinweise.Should().ContainSingle("über die Verteiler an die Akteure, die ihn hören").Which.Hinweis.Should().Be(new Gemerkt(5));
        w.Gemeldet.Should().BeEmpty("keine Funktion hört einen Hinweis");
        (await Fertig(w, Eingang("Schreiber", new Start(1)))).Should().Be(Quittungsart.Gescheitert, "eine Funktion schreibt nie ins Log");
    }

    [Fact]
    public async Task Eine_Antwort_auf_den_eigenen_Command_rechnet_die_Funktion_gleich_weiter()
    {
        await using var w = new Welt();
        w.Antwort = c => c is Buche b ? [new Antwortereignis(new Gebucht(b.Wert), 5)] : Array.Empty<Antwortereignis>();
        Herkunft? gesehen = null;
        w.Instanz("G", new K("A", (_, auf, a, h, _, _) => auf switch
        {
            0 => Strom(new Buche(Konto, ((Start)a[0]!).Wert)),
            1 => Strom(Merke(h, new Bestaetige(Konto))),
            _ => Strom(),
        }).Op<Start>(typeof(Buche)).Antwort<Gebucht>(typeof(Bestaetige)));

        object Merke(Herkunft h, object x) { gesehen = h; return x; }

        var eingang = Eingang("G", new Start(3));
        (await Fertig(w, eingang)).Should().Be(Quittungsart.Fertig);
        w.Gesendet.Select(x => x.Befehl).Should().Equal(new Buche(Konto, 3), new Bestaetige(Konto));
        var antwort = Kettenvertrag.Antwort(AmKopf(eingang.Kennung, "G/A"), "G/A", 0, 0);
        w.Gesendet.Last().Id.Should().Be(Kettenvertrag.Befehl(antwort, "G/A", 0, new Bestaetige(Konto)), "die Kennung der Antwort folgt aus Aufruf, Stelle und Nummer");
        gesehen!.Stream.Should().Be(Konto.Wert, "die Herkunft einer Antwort ist das Ziel");
        gesehen.Version.Should().Be(5);
    }

    [Fact]
    public async Task Bleibt_ein_Command_unbekannt_wird_er_mit_derselben_Id_wiederholt()
    {
        await using var w = new Welt();
        var mal = 0;
        w.Ausgang = _ => Interlocked.Increment(ref mal) < 3 ? EmitAusgang.Unbekannt : EmitAusgang.Zugestellt;
        w.Instanz("G", new K("A", Einzeln(_ => new Buche(Konto, 1))).Op<Start>(typeof(Buche)));
        (await Fertig(w, Eingang("G", new Start(1)))).Should().Be(Quittungsart.Fertig);
        w.Gesendet.Select(x => x.Id).Distinct().Should().HaveCount(1, "dieselbe Id bei jedem Versuch — die Inbox verwirft Wiederholungen");
        w.Gesendet.Should().HaveCount(3);
    }

    // ── Mehrere Nachrichten, Wahl, Strom, verlierbar, Rückstau ───────────────────────────────────────────────────────────

    [Fact]
    public async Task Zwei_Werte_an_einem_Schritt_werden_zweimal_gerechnet_jeder_mit_eigener_Kennung()
    {
        await using var w = new Welt();
        w.Instanz("G",
            new K("A", Einzeln(a => new X(((Start)a[0]!).Wert))).Op<Start>(typeof(X)),
            new K("Vorschau", Einzeln(a => new Z(((X)a[0]!).Wert + 1))).Op<X>(typeof(Z)),
            new K("Daumen", Einzeln(a => new Z(((X)a[0]!).Wert + 2))).Op<X>(typeof(Z)),
            new K("Melde", Einzeln(a => new Buche(Konto, ((Z)a[0]!).Wert))).Op<Z>(typeof(Buche)));

        var eingang = Eingang("G", new Start(10));
        (await Fertig(w, eingang)).Should().Be(Quittungsart.Fertig, "fertig erst, wenn beide Rechnungen von Melde fertig sind");
        w.Gesendet.Select(x => x.Befehl).Should().BeEquivalentTo([new Buche(Konto, 11), new Buche(Konto, 12)], "kein Wert wird verworfen");
        var a = AmKopf(eingang.Kennung, "G/A");
        w.Gesendet.Select(x => x.Id).Should().BeEquivalentTo([
            Kettenvertrag.Befehl(Danach(Danach(a, "G/A", "G/Vorschau"), "G/Vorschau", "G/Melde"), "G/Melde", 0, new Buche(Konto, 11)),
            Kettenvertrag.Befehl(Danach(Danach(a, "G/A", "G/Daumen"), "G/Daumen", "G/Melde"), "G/Melde", 0, new Buche(Konto, 12))],
            "jede Rechnung trägt die Kennung der Nachricht, aus der sie entstand — unabhängig davon, welche zuerst ankam");
    }

    [Fact]
    public async Task Eine_Wahl_gibt_nur_den_Fall_weiter_der_kam()
    {
        await using var w = new Welt();
        w.Instanz("G",
            new K("W", Einzeln(a => ((Start)a[0]!).Wert > 0 ? new Links(1) : new Rechts(2))).Op<Start>(typeof(Links), typeof(Rechts)),
            new K("L", Einzeln(a => new Buche(Konto, ((Links)a[0]!).Wert))).Op<Links>(typeof(Buche)),
            new K("R", Einzeln(a => new Buche(Konto, ((Rechts)a[0]!).Wert))).Op<Rechts>(typeof(Buche)));

        (await Fertig(w, Eingang("G", new Start(5)))).Should().Be(Quittungsart.Fertig, "auf den Zweig, der nicht kam, wartet niemand");
        w.Gesendet.Select(x => x.Befehl).Should().Equal(new Buche(Konto, 1));
    }

    [Fact]
    public async Task Ein_Strom_gibt_je_Element_eine_Nachricht_ein_leerer_ist_sofort_fertig()
    {
        await using var w = new Welt();
        w.Instanz("G",
            new K("Teile", (_, _, a, _, _, _) => Strom(Enumerable.Range(1, ((Start)a[0]!).Wert).Select(i => (object?)new Teil(i)).ToArray()))
                .Op<Start>(typeof(Teil)),
            new K("P", Einzeln(a => new Buche(Konto, ((Teil)a[0]!).Wert * 10))).Op<Teil>(typeof(Buche)));

        (await Fertig(w, Eingang("G", new Start(3)))).Should().Be(Quittungsart.Fertig);
        w.Gesendet.Select(x => x.Befehl).Should().BeEquivalentTo([new Buche(Konto, 10), new Buche(Konto, 20), new Buche(Konto, 30)]);
        w.Gesendet.Select(x => x.Id).Distinct().Should().HaveCount(3, "je Element eine eigene Kennung (Stelle im Strom)");
        (await Fertig(w, Eingang("G", new Start(0)))).Should().Be(Quittungsart.Fertig, "ein leerer Strom heißt nichts");
    }

    [Fact]
    public async Task Eine_verlierbare_Kante_haelt_den_Kopf_nicht_auf()
    {
        await using var w = new Welt();
        var freigabe = new TaskCompletionSource();
        async IAsyncEnumerable<object> Haengt(object?[] a, [EnumeratorCancellation] CancellationToken ct)
        {
            await freigabe.Task.WaitAsync(ct);
            yield return new Buche(Konto, ((X)a[0]!).Wert);
        }
        var b = new K("B", (_, _, a, _, _, ct) => Haengt(a, ct)).Op<X>(typeof(Buche));
        b.Verlierbar.Add("A");
        w.Instanz("G", new K("A", Einzeln(a => new X(((Start)a[0]!).Wert))).Op<Start>(typeof(X)), b);

        (await Fertig(w, Eingang("G", new Start(4)))).Should().Be(Quittungsart.Fertig, "auf eine verlierbare Kante wartet niemand");
        w.Gesendet.Should().BeEmpty("B rechnet noch");
        freigabe.SetResult();
        await Warte(() => w.Gesendet.Count == 1, "B rechnet trotzdem zu Ende");
    }

    [Fact]
    public async Task Ein_voller_Schritt_antwortet_spaeter_der_Absender_schickt_erneut_bis_alles_gerechnet_ist()
    {
        await using var w = new Welt();
        var freigabe = new TaskCompletionSource();
        async IAsyncEnumerable<object> Langsam(object?[] a, [EnumeratorCancellation] CancellationToken ct)
        {
            await freigabe.Task.WaitAsync(ct);
            yield return new Buche(Konto, ((Teil)a[0]!).Wert);
        }
        w.Instanz("G",
            new K("Teile", (_, _, a, _, _, _) => Strom(new Teil(1), new Teil(2), new Teil(3))).Op<Start>(typeof(Teil)),
            new K("P", (_, _, a, _, _, ct) => Langsam(a, ct)) { Konfig = new FunktionsOrtKonfig(Parallel: 1, Warteschlange: 1) }.Op<Teil>(typeof(Buche)));

        var quittung = w.StelleZuAsync(Eingang("G", new Start(0)), TimeSpan.FromSeconds(15), default);
        await Task.Delay(500);
        quittung.IsCompleted.Should().BeFalse("P nimmt nur einen zugleich, die anderen warten beim Absender");
        freigabe.SetResult();
        (await quittung)!.Ausgang.Should().Be(Quittungsart.Fertig);
        w.Gesendet.Select(x => x.Befehl).Should().BeEquivalentTo([new Buche(Konto, 1), new Buche(Konto, 2), new Buche(Konto, 3)], "nichts geht verloren");
    }

    // ── Fehler und Neustart ───────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ein_scheiternder_Knoten_wird_wiederholt_dann_meldet_die_Kette_gescheitert_der_Kopf_wird_geparkt()
    {
        await using var w = new Welt();
        var versuche = 0;
        var kaputt = new K("Kaputt", (_, _, _, _, _, _) => { Interlocked.Increment(ref versuche); throw new InvalidOperationException("kaputt"); })
            { Konfig = new FunktionsOrtKonfig(Wiederholungen: 2) }.Op<X>(typeof(Buche));
        w.Instanz("G", new K("A", Einzeln(a => new X(((Start)a[0]!).Wert))).Op<Start>(typeof(X)), kaputt);

        (await Fertig(w, Eingang("G", new Start(1)))).Should().Be(Quittungsart.Gescheitert, "das „gescheitert“ reicht über die gesicherte Kante bis zum Kopf");
        versuche.Should().Be(3, "einmal und zwei Wiederholungen");
        var hinweis = w.Hinweise.Should().ContainSingle().Which.Hinweis.Should().BeOfType<LaufGescheitert>().Which;
        hinweis.Knoten.Should().Be("Kaputt");
        hinweis.Grund.Should().Contain("kaputt");

        var durabel = Eingang("G", new Start(2), Eingangsart.Durabel);
        (await Fertig(w, durabel)).Should().Be(Quittungsart.Angenommen);
        await Warte(() => w.Geparkt.Any(g => g.Item1 == durabel.Kennung), "der durable Kopf einer gescheiterten Kette wird geparkt");
        w.Ausgetragen.Should().NotContain(durabel.Kennung);
    }

    [Fact]
    public async Task Bricht_eine_Kette_mitten_ab_rechnet_sie_vom_Kopf_neu_mit_denselben_Ids()
    {
        await using var w = new Welt();
        var freigabe = new TaskCompletionSource();
        async IAsyncEnumerable<object> Haengt(object?[] a, [EnumeratorCancellation] CancellationToken ct)
        {
            await freigabe.Task.WaitAsync(ct);
            yield return new Buche(Konto, ((X)a[0]!).Wert + 1);
        }
        w.Instanz("G",
            new K("A", (_, _, a, _, _, _) => Strom(new Buche(Konto, ((Start)a[0]!).Wert), new X(((Start)a[0]!).Wert))).Op<Start>(typeof(Buche), typeof(X)),
            new K("B", (_, _, a, _, _, ct) => Haengt(a, ct)).Op<X>(typeof(Buche)));

        var kopf = Eingang("G", new Start(5), Eingangsart.Durabel);
        (await Fertig(w, kopf)).Should().Be(Quittungsart.Angenommen);
        await Warte(() => w.Gesendet.Count == 1, "A hat seinen Command gegeben");
        await w.AbsturzAsync("G");
        freigabe.SetResult();

        // Der Zusteller bringt den offenen Kopf erneut: die Kette beginnt von vorn, A gibt denselben Command mit derselben Id (die Inbox verwirft ihn).
        (await Fertig(w, kopf)).Should().Be(Quittungsart.Angenommen);
        await Warte(() => w.Ausgetragen.Contains(kopf.Kennung), "die neue Kette wird fertig");
        var ids = w.Gesendet.Select(x => x.Id).ToList();
        ids.Should().HaveCount(3);
        ids.Distinct().Should().HaveCount(2, "A rechnet nach dem Absturz neu — mit derselben Id, genau einmal wirksam");
    }

    // ── Quelle und gesicherter Zustand ───────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Eine_Quelle_startet_je_Element_eine_Kette_sichert_ihre_Nummer_danach_und_zaehlt_nach_einem_Neustart_weiter()
    {
        await using var w = new Welt();
        var freigabe = new TaskCompletionSource();
        async IAsyncEnumerable<object> Lauschen([EnumeratorCancellation] CancellationToken ct)
        {
            for (var i = 1; i <= 3; i++) yield return new X(i);
            await Task.Delay(Timeout.Infinite, ct);
        }
        async IAsyncEnumerable<object> Buchen(object?[] a, [EnumeratorCancellation] CancellationToken ct)
        {
            if (((X)a[0]!).Wert == 2) await freigabe.Task.WaitAsync(ct);   // die zweite Kette hängt
            yield return new Buche(Konto, ((X)a[0]!).Wert);
        }
        w.Instanz("G",
            new K("Quelle", (_, _, _, _, _, ct) => Lauschen(ct)).Lausche(typeof(X)),
            new K("B", (_, _, a, _, _, ct) => Buchen(a, ct)) { Konfig = new FunktionsOrtKonfig(Parallel: 4) }.Op<X>(typeof(Buche)));
        w.Halte("G", true);

        await Warte(() => w.Gesendet.Count == 2, "die Ketten 1 und 3 sind fertig, 2 hängt");
        await Task.Delay(300);
        w.Zustaende.Je.GetValueOrDefault(("G", "Quelle"))?.Nummer.Should().Be(1, "gesichert wird in der Reihenfolge der Elemente — 3 wartet auf 2");
        freigabe.SetResult();
        await Warte(() => w.Zustaende.Je.GetValueOrDefault(("G", "Quelle"))?.Nummer == 3, "nach der zweiten Kette ist der Stand 3");
        var zwei = Herkunft.Ableite("quelle:G:2");
        w.Gesendet.Select(x => x.Id).Should().Contain(Kettenvertrag.Befehl(Danach(zwei, "G/Quelle", "G/B"), "G/B", 0, new Buche(Konto, 2)),
            "die Kennung einer Quell-Kette folgt aus Instanz und Nummer");

        // Neustart: die Quelle beginnt mit ihrer gesicherten Nummer — das erste neue Element ist Nummer 4.
        w.Halte("G", false);
        await w.AbsturzAsync("G");
        w.Halte("G", true);
        await Warte(() => w.Gesendet.Count == 6, "nach dem Neustart meldet die Quelle wieder (hier dieselben drei Elemente)");
        w.Gesendet.Select(x => x.Id).Should().Contain(Kettenvertrag.Befehl(Danach(Herkunft.Ableite("quelle:G:4"), "G/Quelle", "G/B"), "G/B", 0, new Buche(Konto, 1)));
    }

    [Fact]
    public async Task Ein_Knoten_mit_Zustand_sichert_ihn_mit_seinen_Kennungen_und_zaehlt_nach_einem_Absturz_nichts_doppelt()
    {
        await using var w = new Welt();
        var zaehle = new K("Z", (u, _, a, _, _, _) => { var z = (Zaehler)u; z.N++; return Strom(new Buche(Konto, z.N)); })
            { MitZustand = true, Konfig = new FunktionsOrtKonfig(Parallel: 1) }.Op<Start>(typeof(Buche));
        w.Instanz("G", zaehle);
        var erster = Eingang("G", new Start(1));
        (await Fertig(w, erster)).Should().Be(Quittungsart.Fertig);
        await Warte(() => w.Zustaende.Je.ContainsKey(("G", "Z")), "der Zustand wird nach dem fertigen Aufruf gesichert");

        // Absturz: der Graph-Actor vergisst alles; der Kopf kommt erneut — der Zustand kennt die Kennung schon und rechnet nicht neu.
        await w.AbsturzAsync("G");
        (await Fertig(w, erster)).Should().Be(Quittungsart.Fertig);
        w.Gesendet.Select(x => x.Befehl).Should().Equal([new Buche(Konto, 1)], "was der gesicherte Zustand enthält, zählt nicht doppelt");
        (await Fertig(w, Eingang("G", new Start(2)))).Should().Be(Quittungsart.Fertig);
        w.Gesendet.Select(x => x.Befehl).Should().Equal([new Buche(Konto, 1), new Buche(Konto, 2)], "der Zähler lief mit dem gesicherten Stand weiter");
    }

    [Fact]
    public async Task Ein_Knoten_ohne_Zustand_sichert_nichts()
    {
        await using var w = new Welt();
        w.Instanz("G", new K("A", Einzeln(a => new Buche(Konto, ((Start)a[0]!).Wert))).Op<Start>(typeof(Buche)));
        (await Fertig(w, Eingang("G", new Start(1)))).Should().Be(Quittungsart.Fertig);
        await Task.Delay(300);
        w.Zustaende.Je.Should().BeEmpty("ohne Zustand gibt es nichts zu sichern");
    }
}
