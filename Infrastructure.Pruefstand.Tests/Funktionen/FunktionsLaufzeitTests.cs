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
/// Die Laufzeit der Graphen vor dem Aggregat (docs/04 §4.4), store-frei in einem echten lokalen Actor-System: je Instanz ein Graph-Actor (der Koordinator
/// ihrer Läufe), je Knoten ein Parent (Warteschlange, Kinder bis Parallel, Wiederholungen, Abbruch), je Umsetzung ein Kind (Commands mit Ids aus Aufruf,
/// Ort und Stelle). Die Pläne baut der Test von Hand wie der Generator (Erzeuger und Ebenen aus den Typen); der Emitter ist eine Naht. Je Muster aus
/// Entscheidung 5 ein Test: Folge, Fork-Join, Wahl mit Zusammenführen, Je mit Sammeln, Fork-Join je Element, Rennen mit Abbruch, Warten mit Frist —
/// dazu der gescheiterte Lauf und der Neustart vom Kopf.
/// </summary>
public class FunktionsLaufzeitTests
{
    private sealed class Ziel : IState { public int Version { get; set; } public Guid Id { get; set; } }
    private sealed record Buche(Id<Ziel> AggregateId, int Wert) : ICommand<Ziel>;
    private sealed record Start(int Wert) : ICommand;
    private sealed record X(int Wert) : ICommand;
    private sealed record Y(int Wert) : ICommand;
    private sealed record Z(int Wert) : ICommand;
    private sealed record Links(int Wert) : ICommand;
    private sealed record Rechts(int Wert) : ICommand;
    private sealed record Teil(int Wert) : ICommand;
    private sealed record Befund(int Wert) : ICommand;
    private sealed record Gemerkt(int Wert) : ITransientEvent;
    private sealed record Falsch(int Wert) : IEvent;
    private sealed record Gebucht(int Wert) : IEvent;
    private sealed record Bestaetige(Id<Ziel> AggregateId) : ICommand<Ziel>;

    private static readonly Guid Stream = Guid.NewGuid();
    private static readonly Id<Ziel> Konto = Id<Ziel>.Neu();

    // ── Pläne wie der Generator: Erzeuger je Port aus den Ausgaben, Ebenen aus den Strömen, Köpfe = Ports ohne Erzeuger ──

    private static (Portform, Type[]) Eins<T>() => (Portform.Eins, [typeof(T)]);
    private static (Portform, Type[]) Hoechstens<T>() => (Portform.Hoechstens, [typeof(T)]);
    private static (Portform, Type[]) Erster<TA, TB>() => (Portform.Erster, [typeof(TA), typeof(TB)]);
    private static (Portform, Type[]) Alle<T>() => (Portform.Alle, [typeof(T)]);

    private sealed class K(string name, FunktionsRechnung rechne)
    {
        public string Name { get; } = name;
        public FunktionsRechnung Rechne { get; } = rechne;
        public List<(int Auf, (Portform Form, Type[] Typen)[] Ports, Type[] Aus, bool Strom)> Ops { get; } = new();
        public FunktionsOrtKonfig Konfig { get; set; } = new();
        public KnotenKonfig Knoten { get; } = new();
        public Dictionary<Type, int> Antworten { get; } = new();
        public Action? BeimBau { get; set; }
        /// <summary>Eine Quelle: die Nummer ihres Lausche (eine Operation ohne Ports).</summary>
        public int? Lauscht { get; private set; }

        public K Lausche(params Type[] aus)
        {
            Lauscht = Ops.Count + Antworten.Count;
            Ops.Add((Lauscht.Value, [], aus, false));
            return this;
        }

        public K Op(Type[] aus, bool strom = false, params (Portform, Type[])[] ports)
        {
            Ops.Add((Ops.Count + Antworten.Count, ports, aus, strom));
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

    private static GraphPlan Plan(string name, IReadOnlyList<K> knoten)
    {
        var erzeuger = knoten.SelectMany(k => k.Ops.Select(o => (k, o))).ToList();
        var ebenen = new Dictionary<(string, int), string[]>();
        string[] Ebene(K k, (int Auf, (Portform Form, Type[] Typen)[] Ports, Type[] Aus, bool Strom) o)
        {
            if (ebenen.TryGetValue((k.Name, o.Auf), out var e)) return e;
            e = [];
            if (o.Ports.Length > 0)
            {
                var p = o.Ports[0];
                var von = erzeuger.FirstOrDefault(x => x.o.Aus.Intersect(p.Typen).Any());
                if (von.k is not null)
                {
                    var oben = Ebene(von.k, von.o).ToList();
                    if (von.o.Strom) oben.Add(von.k.Name);
                    if (p.Form == Portform.Alle) oben.RemoveAt(oben.Count - 1);
                    e = oben.ToArray();
                }
            }
            return ebenen[(k.Name, o.Auf)] = e;
        }
        var plaene = knoten.Select(k => new KnotenPlan(k.Name, k.Name, k.Ops.Select(o => new OperationPlan(o.Auf,
            o.Ports.Select((p, i) => new PortPlan("p" + i, p.Form, p.Typen,
                erzeuger.Where(x => x.k != k).SelectMany(x => x.o.Aus.Intersect(p.Typen).Select(t => new ErzeugerPlan(x.k.Name, x.o.Auf, t, x.o.Strom))).ToArray()))
                .ToList(),
            o.Aus, o.Strom ? o.Aus.Where(t => !typeof(IAggregatCommand).IsAssignableFrom(t)).ToArray() : [], Ebene(k, o))).ToList())).ToList();
        var koepfe = plaene.SelectMany(k => k.Operationen.SelectMany(o => o.Ports.Where(p => p.Erzeuger.Length == 0).SelectMany(p => p.Typen.Select(t => (t, k.Name)))))
            .GroupBy(x => x.t).ToDictionary(g => g.Key, g => g.Select(x => x.Name).Distinct().ToArray());
        return new GraphPlan(name, typeof(object), plaene, koepfe, Array.Empty<Type>(), knoten.FirstOrDefault(k => k.Lauscht is not null)?.Name);
    }

    /// <summary>Eine Welt: Graph-Actors im lokalen System, Wege dazwischen, ein Emitter, der mitschreibt.</summary>
    private sealed class Welt : IAsyncDisposable, IFunktionsWege
    {
        public readonly ActorSystem System = new();
        public readonly ConcurrentQueue<(IAggregatCommand Befehl, Guid Id)> Gesendet = new();
        public readonly ConcurrentQueue<EingangZustellen> Hinweise = new();
        public readonly ConcurrentQueue<HinweisZustellen> Gescheitert = new();
        public readonly ConcurrentQueue<Guid> Ausgetragen = new();
        public readonly ConcurrentQueue<(Guid, string)> Geparkt = new();
        public readonly Ablage Zustaende = new();
        public Func<IAggregatCommand, EmitAusgang> Ausgang = _ => EmitAusgang.Zugestellt;
        public Func<IAggregatCommand, IReadOnlyList<Antwortereignis>> Antwort = _ => Array.Empty<Antwortereignis>();
        public Verkettung Koepfe = Verkettung.Leer;
        private readonly Dictionary<string, PID> _instanzen = new();
        private readonly ServiceProvider _dienste = new ServiceCollection().BuildServiceProvider();

        public GraphInstanz Instanz(string name, params K[] knoten)
        {
            var plan = Plan(name, knoten);
            var instanz = new GraphInstanz(name, plan, null, knoten.ToDictionary(k => k.Name, k => k.Knoten));
            var parents = knoten.ToDictionary(k => k.Name, k =>
            {
                var traeger = new FunktionsTraeger(k.Name, sp => new FunktionsAktivierung(sp, (_, _) => { k.BeimBau?.Invoke(); return new object(); }, name: k.Name,
                        knoten: new KnotenKontext(name, k.Name, k.Knoten), ablage: Zustaende, quelle: k.Lauscht is not null),
                    k.Rechne, Antworten: k.Antworten)
                { Instanz = name, Knoten = k.Name, ImGraphen = plan.ImGraphen(k.Name), Zeitlimit = k.Knoten.Zeitlimit, Lausche = k.Lauscht };
                return (Func<FunktionsParent>)(() => new FunktionsParent(traeger, k.Konfig, () => new FunktionsKindActor(traeger, _dienste, this, Koepfe, SendeAsync)));
            });
            Starte(instanz, parents);
            _plaene[name] = (instanz, parents);
            return instanz;
        }

        private readonly Dictionary<string, (GraphInstanz, Dictionary<string, Func<FunktionsParent>>)> _plaene = new();

        private void Starte(GraphInstanz instanz, Dictionary<string, Func<FunktionsParent>> parents) =>
            _instanzen[instanz.Name] = System.Root.SpawnNamed(Props.FromProducer(() => new GraphActor(instanz, parents, new Offene(this), Gescheitert.Enqueue,
                ablage: Zustaende)), FunktionenWurzel.Name(instanz.Name) + "-" + Guid.NewGuid().ToString("N")[..6]);

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

        public void Melde(EingangZustellen hinweis)
        {
            Hinweise.Enqueue(hinweis);
            if (_instanzen.TryGetValue(hinweis.Instanz, out var p)) System.Root.Send(p, hinweis);
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

    private static Guid AufrufId(Guid lauf, string ort, string pfad = "", int auf = 0) => Kettenvertrag.Aufruf(lauf, ort, pfad, auf);

    // ── Folge, Kennung, Rand ──────────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Eine_Folge_bis_zum_Command_mit_Ids_aus_Lauf_Pfad_Knoten_und_Stelle()
    {
        await using var w = new Welt();
        w.Instanz("G",
            new K("A", Einzeln(a => new X(((Start)a[0]!).Wert * 2))).Op([typeof(X)], ports: Eins<Start>()),
            new K("B", (_, _, a, _, _, _) => Strom(new Buche(Konto, ((X)a[0]!).Wert), new Buche(Konto, ((X)a[0]!).Wert + 1)))
                .Op([typeof(Buche)], ports: Eins<X>()));

        var eingang = Eingang("G", new Start(21));
        (await Fertig(w, eingang)).Should().Be(Quittungsart.Fertig, "fertig ist der Lauf erst, wenn jeder Aufruf fertig ist — B erst, wenn seine Commands beantwortet sind");

        var b = AufrufId(eingang.Kennung, "G/B");
        w.Gesendet.Select(x => x.Befehl).Should().Equal(new Buche(Konto, 42), new Buche(Konto, 43));
        w.Gesendet.Select(x => x.Id).Should().Equal(Kettenvertrag.Befehl(b, "G/B", 0, new Buche(Konto, 42)), Kettenvertrag.Befehl(b, "G/B", 1, new Buche(Konto, 43)));
    }

    [Fact]
    public async Task Ein_doppelter_Kopf_wird_an_der_Kennung_erkannt_fertig_rechnet_nicht_neu()
    {
        await using var w = new Welt();
        var gerechnet = 0;
        w.Instanz("G", new K("A", Einzeln(a => { Interlocked.Increment(ref gerechnet); return new Buche(Konto, ((Start)a[0]!).Wert); }))
            .Op([typeof(Buche)], ports: Eins<Start>()));
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
        w.Instanz("G", new K("A", Einzeln(a => new Buche(Konto, ((Start)a[0]!).Wert))).Op([typeof(Buche)], ports: Eins<Start>()));
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
        var a = new K("A", (_, _, _, _, _, ct) => Blockiert(ct)) { Konfig = new FunktionsOrtKonfig(Parallel: 2) }.Op([typeof(Buche)], ports: Eins<Start>());
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
        (await Fertig(w, Eingang("G", new Start(9)), 5)).Should().Be(Quittungsart.Spaeter, "drei Läufe sind offen — die Warteschlange des Kopfs (3) ist voll");
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
            .Op([typeof(Buche)], strom: true, ports: Eins<Start>()));
        await Warte(() => Volatile.Read(ref gebaut) == 3, "vorgewärmt baut der Parent alle Kinder beim Start");
        var kalt = 0;
        w.Instanz("Kalt", new K("A", Einzeln(_ => null)) { Konfig = new FunktionsOrtKonfig(Parallel: 3), BeimBau = () => Interlocked.Increment(ref kalt) }
            .Op([typeof(Buche)], strom: true, ports: Eins<Start>()));
        await Task.Delay(300);
        kalt.Should().Be(0);
        (await Fertig(w, Eingang("Kalt", new Start(1)))).Should().Be(Quittungsart.Fertig);
        kalt.Should().Be(1, "das erste Kind entsteht mit dem ersten Aufruf");
    }

    [Fact]
    public async Task Ein_Hinweis_geht_verlierbar_an_die_Koepfe_die_ihn_nehmen_ein_Event_als_Ausgabe_laesst_den_Lauf_scheitern()
    {
        await using var w = new Welt();
        var gehoert = new ConcurrentQueue<int>();
        w.Koepfe = new Verkettung(new Dictionary<Type, string[]> { [typeof(Gemerkt)] = ["Hoerer"] });
        w.Instanz("G", new K("A", Einzeln(a => new Gemerkt(((Start)a[0]!).Wert))).Op([typeof(Gemerkt)], ports: Eins<Start>()));
        w.Instanz("Hoerer", new K("H", Einzeln(a => { gehoert.Enqueue(((Gemerkt)a[0]!).Wert); return null; })).Op([typeof(Buche)], strom: true, ports: Eins<Gemerkt>()));
        var schreiber = new K("S", Einzeln(_ => new Falsch(1))) { Konfig = new FunktionsOrtKonfig(Wiederholungen: 0) }.Op([typeof(Buche)], ports: Eins<Start>());
        w.Instanz("Schreiber", schreiber);

        (await Fertig(w, Eingang("G", new Start(5)))).Should().Be(Quittungsart.Fertig, "auf einen Hinweis wird nicht gewartet");
        await Warte(() => gehoert.Contains(5), "der Kopf, der ihn nimmt, bekommt den Hinweis");
        w.Hinweise.Single().Art.Should().Be(Eingangsart.Hinweis);
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
        }).Op([typeof(Buche)], ports: Eins<Start>()).Antwort<Gebucht>(typeof(Bestaetige)));

        object Merke(Herkunft h, object x) { gesehen = h; return x; }

        var eingang = Eingang("G", new Start(3));
        (await Fertig(w, eingang)).Should().Be(Quittungsart.Fertig);
        w.Gesendet.Select(x => x.Befehl).Should().Equal(new Buche(Konto, 3), new Bestaetige(Konto));
        var antwort = Kettenvertrag.Antwort(AufrufId(eingang.Kennung, "G/A"), "G/A", 0, 0);
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
        w.Instanz("G", new K("A", Einzeln(_ => new Buche(Konto, 1))).Op([typeof(Buche)], ports: Eins<Start>()));
        (await Fertig(w, Eingang("G", new Start(1)))).Should().Be(Quittungsart.Fertig);
        w.Gesendet.Select(x => x.Id).Distinct().Should().HaveCount(1, "dieselbe Id bei jedem Versuch — die Inbox verwirft Wiederholungen");
        w.Gesendet.Should().HaveCount(3);
    }

    // ── Die Muster aus Entscheidung 5 ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Fork_Join_zwei_Zweige_nebeneinander_der_Join_feuert_einmal_mit_beiden()
    {
        await using var w = new Welt();
        var beideLaufen = new CountdownEvent(2);
        var joins = new ConcurrentQueue<(int, int)>();
        FunktionsRechnung Zweig(Func<int, object> aus) => (_, _, a, _, _, ct) => Gleichzeitig(ct, aus(((X)a[0]!).Wert));
        async IAsyncEnumerable<object> Gleichzeitig([EnumeratorCancellation] CancellationToken ct, object aus)
        {
            beideLaufen.Signal();
            beideLaufen.Wait(TimeSpan.FromSeconds(5), ct).Should().BeTrue("die Zweige eines Forks laufen nebeneinander");
            yield return aus;
            await Task.CompletedTask;
        }
        w.Instanz("G",
            new K("A", Einzeln(a => new X(((Start)a[0]!).Wert))).Op([typeof(X)], ports: Eins<Start>()),
            new K("B", Zweig(v => new Y(v + 1))).Op([typeof(Y)], ports: Eins<X>()),
            new K("C", Zweig(v => new Z(v + 2))).Op([typeof(Z)], ports: Eins<X>()),
            new K("D", Einzeln(a => { joins.Enqueue((((Y)a[0]!).Wert, ((Z)a[1]!).Wert)); return new Buche(Konto, ((Y)a[0]!).Wert + ((Z)a[1]!).Wert); }))
                .Op([typeof(Buche)], ports: [Eins<Y>(), Eins<Z>()]));

        (await Fertig(w, Eingang("G", new Start(10)))).Should().Be(Quittungsart.Fertig);
        joins.Should().Equal((11, 12));
        w.Gesendet.Select(x => x.Befehl).Should().Equal(new Buche(Konto, 23));
    }

    [Fact]
    public async Task Wahl_und_Zusammenfuehren_der_Join_feuert_mit_null_fuer_den_Zweig_der_nicht_kam()
    {
        await using var w = new Welt();
        var gesehen = new ConcurrentQueue<string>();
        w.Instanz("G",
            new K("A", Einzeln(a => ((Start)a[0]!).Wert > 0 ? new Links(1) : new Rechts(2))).Op([typeof(Links), typeof(Rechts)], ports: Eins<Start>()),
            new K("B", Einzeln(a => new X(((Links)a[0]!).Wert))).Op([typeof(X)], ports: Eins<Links>()),
            new K("C", Einzeln(a => new Y(((Rechts)a[0]!).Wert))).Op([typeof(Y)], ports: Eins<Rechts>()),
            new K("D", Einzeln(a => { gesehen.Enqueue($"{(a[0] as X)?.Wert}|{(a[1] as Y)?.Wert}"); return new Buche(Konto, 0); }))
                .Op([typeof(Buche)], ports: [Hoechstens<X>(), Hoechstens<Y>()]));

        (await Fertig(w, Eingang("G", new Start(1)))).Should().Be(Quittungsart.Fertig);
        (await Fertig(w, Eingang("G", new Start(-1)))).Should().Be(Quittungsart.Fertig);
        gesehen.Should().Equal("1|", "|2");
    }

    [Fact]
    public async Task Je_und_Sammeln_ein_Unterlauf_je_Element_die_Liste_in_ihrer_Reihenfolge_auch_leer()
    {
        await using var w = new Welt();
        var listen = new ConcurrentQueue<string>();
        w.Instanz("G",
            new K("Teile", (_, _, a, _, _, _) => Strom(Enumerable.Range(1, ((Start)a[0]!).Wert).Select(i => (object)new Teil(i)).ToArray()))
                .Op([typeof(Teil)], strom: true, ports: Eins<Start>()),
            new K("Pruefe", (_, _, a, _, _, _) => ((Teil)a[0]!).Wert == 2 ? Strom(new Gemerkt(2)) : Strom(new Befund(((Teil)a[0]!).Wert * 10)))
                .Op([typeof(Befund), typeof(Gemerkt)], ports: Eins<Teil>()),
            new K("Sammle", Einzeln(a =>
            {
                var alle = (object?[])a[0]!;
                listen.Enqueue(string.Join(",", alle.Cast<Befund>().Select(b => b.Wert)));
                return new Buche(Konto, alle.Length);
            })).Op([typeof(Buche)], ports: Alle<Befund>()));

        var drei = Eingang("G", new Start(3));
        (await Fertig(w, drei)).Should().Be(Quittungsart.Fertig);
        (await Fertig(w, Eingang("G", new Start(0)))).Should().Be(Quittungsart.Fertig);
        listen.Should().Equal("10,30", "");
        var pruefe2 = AufrufId(drei.Kennung, "G/Pruefe", "Teile#0.1");
        pruefe2.Should().NotBe(AufrufId(drei.Kennung, "G/Pruefe", "Teile#0.0"), "je Unterlauf (Pfad + Stelle) eine eigene Kennung");
    }

    [Fact]
    public async Task Fork_Join_je_Element_der_Join_paart_nur_im_selben_Unterlauf()
    {
        await using var w = new Welt();
        var summe = new ConcurrentQueue<int>();
        w.Instanz("G",
            new K("Teile", (_, _, a, _, _, _) => Strom(new Teil(1), new Teil(2), new Teil(3))).Op([typeof(Teil)], strom: true, ports: Eins<Start>()),
            new K("Mal", Einzeln(a => new X(((Teil)a[0]!).Wert * 10))).Op([typeof(X)], ports: Eins<Teil>()),
            new K("Plus", Einzeln(a => new Y(((Teil)a[0]!).Wert + 100))).Op([typeof(Y)], ports: Eins<Teil>()),
            new K("Paar", Einzeln(a => new Z(((X)a[0]!).Wert + ((Y)a[1]!).Wert))).Op([typeof(Z)], ports: [Eins<X>(), Eins<Y>()]),
            new K("Sammle", Einzeln(a => { var z = ((object?[])a[0]!).Cast<Z>().Select(x => x.Wert).ToList(); summe.Enqueue(z.Sum()); z.Should().Equal(111, 122, 133); return null; }))
                .Op([typeof(Buche)], strom: true, ports: Alle<Z>()));

        (await Fertig(w, Eingang("G", new Start(0)))).Should().Be(Quittungsart.Fertig);
        summe.Should().Equal(366);
    }

    [Fact]
    public async Task Rennen_der_erste_gewinnt_der_Verlierer_wird_abgebrochen()
    {
        await using var w = new Welt();
        var abgebrochen = new TaskCompletionSource();
        var sieger = new ConcurrentQueue<string>();
        async IAsyncEnumerable<object> Langsam([EnumeratorCancellation] CancellationToken ct)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(30), ct); }
            catch (OperationCanceledException) { abgebrochen.TrySetResult(); throw; }
            yield return new Y(0);
        }
        w.Instanz("G",
            new K("A", Einzeln(a => new X(((Start)a[0]!).Wert))).Op([typeof(X)], ports: Eins<Start>()),
            new K("Schnell", Einzeln(a => new Links(((X)a[0]!).Wert))).Op([typeof(Links)], ports: Eins<X>()),
            new K("Bedaechtig", (_, _, _, _, _, ct) => Langsam(ct)) { Konfig = new FunktionsOrtKonfig(Wiederholungen: 0) }.Op([typeof(Rechts)], ports: Eins<X>()),
            new K("Ziel", Einzeln(a => { sieger.Enqueue(a[0]!.GetType().Name); return new Buche(Konto, 1); }))
                .Op([typeof(Buche)], ports: Erster<Links, Rechts>()));

        (await Fertig(w, Eingang("G", new Start(1)))).Should().Be(Quittungsart.Fertig, "der Lauf wartet nicht auf den Verlierer");
        sieger.Should().Equal(nameof(Links));
        await abgebrochen.Task.WaitAsync(TimeSpan.FromSeconds(5));
        w.Gescheitert.Should().BeEmpty("ein abgebrochener Verlierer ist kein Fehler");
    }

    [Fact]
    public async Task Warten_mit_Frist_nach_dem_Zeitlimit_feuert_der_Join_mit_null_der_Saeumige_wird_abgebrochen()
    {
        await using var w = new Welt();
        var gesehen = new ConcurrentQueue<string>();
        async IAsyncEnumerable<object> Saeumig([EnumeratorCancellation] CancellationToken ct)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), ct);
            yield return new Y(0);
        }
        var join = new K("Join", Einzeln(a => { gesehen.Enqueue($"{((X)a[0]!).Wert}|{(a[1] as Y)?.Wert}"); return new Buche(Konto, 1); }))
            .Op([typeof(Buche)], ports: [Eins<X>(), Hoechstens<Y>()]);
        join.Knoten.Zeitlimit = TimeSpan.FromMilliseconds(300);
        w.Instanz("G",
            new K("A", Einzeln(a => new X(((Start)a[0]!).Wert))).Op([typeof(X)], ports: Eins<Start>()),
            new K("Antwort", (_, _, _, _, _, ct) => Saeumig(ct)) { Konfig = new FunktionsOrtKonfig(Wiederholungen: 0) }.Op([typeof(Y)], ports: Eins<X>()),
            join);

        var dauer = System.Diagnostics.Stopwatch.StartNew();
        (await Fertig(w, Eingang("G", new Start(4)))).Should().Be(Quittungsart.Fertig);
        dauer.Elapsed.Should().BeLessThan(TimeSpan.FromSeconds(10), "nach dem Zeitlimit wartet der Lauf nicht auf den Säumigen");
        gesehen.Should().Equal("4|");
    }

    // ── Fehler, Abbruch, Neustart ─────────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Ein_scheiternder_Knoten_wird_wiederholt_dann_scheitert_der_Lauf_offene_Zweige_brechen_ab_der_Kopf_wird_geparkt()
    {
        await using var w = new Welt();
        var versuche = 0;
        var abgebrochen = new TaskCompletionSource();
        async IAsyncEnumerable<object> Wartend([EnumeratorCancellation] CancellationToken ct)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(30), ct); }
            catch (OperationCanceledException) { abgebrochen.TrySetResult(); throw; }
            yield return new Buche(Konto, 0);
        }
        var kaputt = new K("Kaputt", (_, _, _, _, _, _) => { Interlocked.Increment(ref versuche); throw new InvalidOperationException("kaputt"); })
            { Konfig = new FunktionsOrtKonfig(Wiederholungen: 2) }.Op([typeof(Buche)], ports: Eins<X>());
        w.Instanz("G",
            new K("A", Einzeln(a => new X(((Start)a[0]!).Wert))).Op([typeof(X)], ports: Eins<Start>()),
            kaputt,
            new K("Wartet", (_, _, _, _, _, ct) => Wartend(ct)).Op([typeof(Buche)], ports: Eins<X>()));

        (await Fertig(w, Eingang("G", new Start(1)))).Should().Be(Quittungsart.Gescheitert);
        versuche.Should().Be(3, "einmal und zwei Wiederholungen");
        await abgebrochen.Task.WaitAsync(TimeSpan.FromSeconds(5));
        var hinweis = w.Gescheitert.Should().ContainSingle().Which.Hinweis.Should().BeOfType<LaufGescheitert>().Which;
        hinweis.Knoten.Should().Be("Kaputt");
        hinweis.Grund.Should().Contain("kaputt");

        var durabel = Eingang("G", new Start(2), Eingangsart.Durabel);
        (await Fertig(w, durabel)).Should().Be(Quittungsart.Angenommen);
        await Warte(() => w.Geparkt.Any(g => g.Item1 == durabel.Kennung), "der durable Kopf eines gescheiterten Laufs wird geparkt");
        w.Ausgetragen.Should().NotContain(durabel.Kennung);
    }

    [Fact]
    public async Task Bricht_ein_Lauf_mitten_ab_rechnet_er_vom_Kopf_neu_mit_denselben_Ids()
    {
        await using var w = new Welt();
        var freigabe = new TaskCompletionSource();
        async IAsyncEnumerable<object> Haengt(object?[] a, [EnumeratorCancellation] CancellationToken ct)
        {
            await freigabe.Task.WaitAsync(ct);
            yield return new Buche(Konto, ((X)a[0]!).Wert + 1);
        }
        w.Instanz("G",
            new K("A", (_, _, a, _, _, _) => Strom(new Buche(Konto, ((Start)a[0]!).Wert), new X(((Start)a[0]!).Wert))).Op([typeof(Buche), typeof(X)], ports: Eins<Start>()),
            new K("B", (_, _, a, _, _, ct) => Haengt(a, ct)).Op([typeof(Buche)], ports: Eins<X>()));

        var kopf = Eingang("G", new Start(5), Eingangsart.Durabel);
        (await Fertig(w, kopf)).Should().Be(Quittungsart.Angenommen);
        await Warte(() => w.Gesendet.Count == 1, "A hat seinen Command gegeben");
        await w.AbsturzAsync("G");
        freigabe.SetResult();

        // Der Zusteller bringt den offenen Kopf erneut: der Lauf beginnt von vorn, A gibt denselben Command mit derselben Id (die Inbox verwirft ihn).
        (await Fertig(w, kopf)).Should().Be(Quittungsart.Angenommen);
        await Warte(() => w.Ausgetragen.Contains(kopf.Kennung), "der neue Lauf wird fertig");
        var ids = w.Gesendet.Select(x => x.Id).ToList();
        ids.Should().HaveCount(3);
        ids.Distinct().Should().HaveCount(2, "A rechnet nach dem Absturz neu — mit derselben Id, genau einmal wirksam");
    }

    // ── Quelle (Entscheidung 8) und gesicherter Zustand (Entscheidung 9) ─────────────────────────────────────────────────

    [Fact]
    public async Task Eine_Quelle_startet_je_Element_einen_Lauf_sichert_ihre_Nummer_nach_dem_Lauf_und_zaehlt_nach_einem_Neustart_weiter()
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
            if (((X)a[0]!).Wert == 2) await freigabe.Task.WaitAsync(ct);   // der zweite Lauf hängt
            yield return new Buche(Konto, ((X)a[0]!).Wert);
        }
        w.Instanz("G",
            new K("Quelle", (_, _, _, _, _, ct) => Lauschen(ct)).Lausche(typeof(X)),
            new K("B", (_, _, a, _, _, ct) => Buchen(a, ct)) { Konfig = new FunktionsOrtKonfig(Parallel: 4) }.Op([typeof(Buche)], ports: Eins<X>()));
        w.Halte("G", true);

        await Warte(() => w.Gesendet.Count == 2, "die Läufe 1 und 3 sind fertig, 2 hängt");
        await Task.Delay(300);
        w.Zustaende.Je.GetValueOrDefault(("G", "Quelle"))?.Nummer.Should().Be(1, "gesichert wird in der Reihenfolge der Läufe — 3 wartet auf 2");
        freigabe.SetResult();
        await Warte(() => w.Zustaende.Je.GetValueOrDefault(("G", "Quelle"))?.Nummer == 3, "nach dem zweiten Lauf ist der Stand 3");
        w.Gesendet.Select(x => x.Id).Should().Contain(Kettenvertrag.Befehl(AufrufId(Herkunft.Ableite("quelle:G:2"), "G/B"), "G/B", 0, new Buche(Konto, 2)),
            "die Kennung eines Quell-Laufs folgt aus Instanz und Nummer");

        // Neustart: die Quelle beginnt mit ihrer gesicherten Nummer — das erste neue Element ist Nummer 4.
        w.Halte("G", false);
        await w.AbsturzAsync("G");
        w.Halte("G", true);
        await Warte(() => w.Gesendet.Count == 6, "nach dem Neustart meldet die Quelle wieder (hier dieselben drei Elemente)");
        w.Gesendet.Select(x => x.Id).Should().Contain(Kettenvertrag.Befehl(AufrufId(Herkunft.Ableite("quelle:G:4"), "G/B"), "G/B", 0, new Buche(Konto, 1)));
    }

    [Fact]
    public async Task Ein_Knoten_mit_Zustand_schickt_nach_jedem_Aufruf_seinen_Schnappschuss_der_nach_dem_Lauf_gesichert_wird()
    {
        await using var w = new Welt();
        w.Instanz("G", new K("A", Einzeln(a => new Buche(Konto, ((Start)a[0]!).Wert))).Op([typeof(Buche)], ports: Eins<Start>()));
        (await Fertig(w, Eingang("G", new Start(1)))).Should().Be(Quittungsart.Fertig);
        await Task.Delay(300);
        w.Zustaende.Je.Should().BeEmpty("ohne Zustand gibt es nichts zu sichern");
    }
}
