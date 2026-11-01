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
/// Die Funktions-Laufzeit vor dem Aggregat (docs/04 §4.4), store-frei in einem echten lokalen Actor-System: je Funktion ein Parent (begrenzte
/// Warteschlange, Kinder bis Parallel, Kennung, Wiederholungen), je Umsetzung ein Kind (Commands mit Ids aus Eingang, Funktion und Stelle;
/// Zwischenergebnisse an die Hörer, gewartet bis sie fertig sind). Die Wege zwischen den Parents laufen hier lokal, der Emitter ist eine Naht.
/// </summary>
public class FunktionsLaufzeitTests
{
    private sealed class Ziel : IState { public int Version { get; set; } public Guid Id { get; set; } }
    private sealed record Buche(Id<Ziel> AggregateId, int Wert) : ICommand<Ziel>;
    private sealed record Rohwert(int Wert);
    private sealed record Verdoppelt(int Wert);
    private sealed record Gemerkt(int Wert) : ITransientEvent;
    private sealed record Falsch(int Wert) : IEvent;

    private static readonly Guid Stream = Guid.NewGuid();

    /// <summary>Eine Welt: Parents je Funktion im lokalen System, Wege dazwischen, ein Emitter, der mitschreibt.</summary>
    private sealed class Welt : IAsyncDisposable, IFunktionsWege
    {
        public readonly ActorSystem System = new();
        public readonly ConcurrentQueue<(ICommand Befehl, Guid Id)> Gesendet = new();
        public readonly ConcurrentQueue<EingangZustellen> Hinweise = new();
        public readonly ConcurrentQueue<Guid> Ausgetragen = new();
        public Func<ICommand, EmitAusgang> Ausgang = _ => EmitAusgang.Zugestellt;
        private readonly Dictionary<string, PID> _parents = new();
        private readonly ServiceProvider _dienste = new ServiceCollection().BuildServiceProvider();
        public Verkettung Verkettung = Verkettung.Leer;

        public void Funktion(string name, FunktionsRechnung rechne, FunktionsOrtKonfig? konfig = null, Action? beimBau = null)
        {
            var traeger = new FunktionsTraeger(name, sp => new FunktionsAktivierung(sp, (_, _) => { beimBau?.Invoke(); return new object(); }, name: name), rechne);
            var k = konfig ?? new FunktionsOrtKonfig();
            _parents[name] = System.Root.SpawnNamed(Props.FromProducer(() => new FunktionsParent(traeger, k,
                () => new FunktionsKindActor(traeger, _dienste, this, Verkettung, SendeAsync), new Offene(this))), FunktionenWurzel.Name(name));
        }

        private Task<EmitAusgang> SendeAsync(ICommand c, Guid id, Guid korrelation, CancellationToken ct)
        {
            Gesendet.Enqueue((c, id));
            return Task.FromResult(Ausgang(c));
        }

        public async Task<EingangQuittung?> StelleZuAsync(EingangZustellen eingang, TimeSpan frist, CancellationToken ct)
        {
            if (!_parents.TryGetValue(eingang.Funktion, out var p)) return new EingangQuittung(eingang.Kennung, Quittungsart.Spaeter);
            try { return await System.Root.RequestAsync<EingangQuittung>(p, eingang, frist); }
            catch (TimeoutException) { return null; }
        }

        public void Melde(EingangZustellen hinweis)
        {
            Hinweise.Enqueue(hinweis);
            if (_parents.TryGetValue(hinweis.Funktion, out var p)) System.Root.Send(p, hinweis);
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
        }

        public async ValueTask DisposeAsync()
        {
            await System.ShutdownAsync();
            await _dienste.DisposeAsync();
        }
    }

    private static EingangZustellen Eingang(string funktion, object nachricht, Eingangsart art = Eingangsart.Kette, Guid? kennung = null) =>
        new(kennung ?? Guid.NewGuid(), funktion, nachricht, art, Stream, 7, DateTimeOffset.UnixEpoch, null, null, null);

    private static FunktionsRechnung Einzeln(Func<object, Herkunft, object?> f) => (_, e, h, _, ct) => Strom(f(e, h));

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

    [Fact]
    public async Task Eine_Kette_ueber_ein_Zwischenergebnis_bis_zum_Command_mit_Ids_aus_Eingang_Funktion_und_Stelle()
    {
        await using var w = new Welt();
        var ziel = Id<Ziel>.Neu();
        w.Verkettung = new Verkettung(new Dictionary<Type, string[]> { [typeof(Verdoppelt)] = new[] { "B" } });
        w.Funktion("A", Einzeln((e, _) => new Verdoppelt(((Rohwert)e).Wert * 2)));
        w.Funktion("B", (_, e, _, _, _) => Strom(new Buche(ziel, ((Verdoppelt)e).Wert), new Buche(ziel, ((Verdoppelt)e).Wert + 1)));

        var eingang = Eingang("A", new Rohwert(21));
        (await w.StelleZuAsync(eingang, TimeSpan.FromSeconds(10), default))!.Ausgang.Should().Be(Quittungsart.Fertig,
            "fertig ist der Eingang erst, wenn sein Nachfolger fertig ist — und der erst, wenn seine Commands beantwortet sind");

        var beiB = Eingangsvertrag.Eingang(Kettenvertrag.Ausgabe(eingang.Kennung, "A", 0), "B");
        w.Gesendet.Select(x => x.Befehl).Should().Equal(new Buche(ziel, 42), new Buche(ziel, 43));
        w.Gesendet.Select(x => x.Id).Should().Equal(Kettenvertrag.Befehl(beiB, "B", 0, new Buche(ziel, 42)), Kettenvertrag.Befehl(beiB, "B", 1, new Buche(ziel, 43)));
    }

    [Fact]
    public async Task Ein_doppelter_Eingang_wird_an_der_Kennung_erkannt_fertig_rechnet_nicht_neu()
    {
        await using var w = new Welt();
        var ziel = Id<Ziel>.Neu();
        var gerechnet = 0;
        w.Funktion("A", Einzeln((e, _) => { Interlocked.Increment(ref gerechnet); return new Buche(ziel, ((Rohwert)e).Wert); }));
        var eingang = Eingang("A", new Rohwert(1));
        (await w.StelleZuAsync(eingang, TimeSpan.FromSeconds(10), default))!.Ausgang.Should().Be(Quittungsart.Fertig);
        (await w.StelleZuAsync(eingang, TimeSpan.FromSeconds(10), default))!.Ausgang.Should().Be(Quittungsart.Fertig);
        gerechnet.Should().Be(1, "ein fertiger Eingang bekommt sofort „fertig“");
        w.Gesendet.Should().HaveCount(1);
    }

    [Fact]
    public async Task Durabel_heisst_sofort_angenommen_fertig_traegt_der_Parent_selbst_aus()
    {
        await using var w = new Welt();
        var ziel = Id<Ziel>.Neu();
        w.Funktion("A", Einzeln((e, _) => new Buche(ziel, ((Rohwert)e).Wert)));
        var eingang = Eingang("A", new Rohwert(1), Eingangsart.Durabel);
        (await w.StelleZuAsync(eingang, TimeSpan.FromSeconds(10), default))!.Ausgang.Should().Be(Quittungsart.Angenommen);
        await Warte(() => w.Ausgetragen.Contains(eingang.Kennung), "der fertige durable Eingang wird nicht ausgetragen");
    }

    [Fact]
    public async Task Volle_Warteschlange_heisst_spaeter_Parallel_begrenzt_die_Kinder()
    {
        await using var w = new Welt();
        var frei = new TaskCompletionSource();
        var laufen = 0;
        var hoechstens = 0;
        w.Funktion("A", (_, e, _, _, ct) => Blockiert(ct), new FunktionsOrtKonfig(Parallel: 2, Warteschlange: 1));

        async IAsyncEnumerable<object> Blockiert([EnumeratorCancellation] CancellationToken ct)
        {
            var n = Interlocked.Increment(ref laufen);
            InterlockedMax(ref hoechstens, n);
            await frei.Task.WaitAsync(ct);
            Interlocked.Decrement(ref laufen);
            yield break;
        }

        var quittungen = new List<Task<EingangQuittung?>>();
        for (var i = 0; i < 3; i++) quittungen.Add(w.StelleZuAsync(Eingang("A", new Rohwert(i)), TimeSpan.FromSeconds(10), default));
        await Warte(() => Volatile.Read(ref laufen) == 2, "zwei Kinder rechnen");
        (await w.StelleZuAsync(Eingang("A", new Rohwert(9)), TimeSpan.FromSeconds(5), default))!.Ausgang.Should().Be(Quittungsart.Spaeter,
            "zwei rechnen, einer wartet — die Schlange (1) ist voll");
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
    public async Task Ein_scheiternder_Eingang_wird_wiederholt_dann_gescheitert()
    {
        await using var w = new Welt();
        var versuche = 0;
        w.Funktion("A", Einzeln((_, _) => { Interlocked.Increment(ref versuche); throw new InvalidOperationException("kaputt"); }),
            new FunktionsOrtKonfig(Wiederholungen: 2));
        (await w.StelleZuAsync(Eingang("A", new Rohwert(1)), TimeSpan.FromSeconds(10), default))!.Ausgang.Should().Be(Quittungsart.Gescheitert);
        versuche.Should().Be(3, "einmal und zwei Wiederholungen");
    }

    [Fact]
    public async Task Vorwaermen_baut_die_Kinder_beim_Start_ohne_Vorwaermen_beim_ersten_Eingang()
    {
        await using var w = new Welt();
        var gebaut = 0;
        w.Funktion("Warm", Einzeln((_, _) => null), new FunktionsOrtKonfig(Parallel: 3, Vorwaermen: true), () => Interlocked.Increment(ref gebaut));
        await Warte(() => Volatile.Read(ref gebaut) == 3, "vorgewärmt baut der Parent alle Kinder beim Start");
        var kalt = 0;
        w.Funktion("Kalt", Einzeln((_, _) => null), new FunktionsOrtKonfig(Parallel: 3), () => Interlocked.Increment(ref kalt));
        await Task.Delay(300);
        kalt.Should().Be(0);
        (await w.StelleZuAsync(Eingang("Kalt", new Rohwert(1)), TimeSpan.FromSeconds(10), default))!.Ausgang.Should().Be(Quittungsart.Fertig);
        kalt.Should().Be(1, "das erste Kind entsteht mit dem ersten Eingang");
    }

    [Fact]
    public async Task Ein_Hinweis_geht_verlierbar_an_seine_Hoerer_ein_Event_als_Ausgabe_ist_ein_Fehler()
    {
        await using var w = new Welt();
        var gehoert = new ConcurrentQueue<int>();
        w.Verkettung = new Verkettung(new Dictionary<Type, string[]> { [typeof(Gemerkt)] = new[] { "Hoerer" } });
        w.Funktion("A", Einzeln((e, _) => new Gemerkt(((Rohwert)e).Wert)));
        w.Funktion("Hoerer", Einzeln((e, _) => { gehoert.Enqueue(((Gemerkt)e).Wert); return null; }));
        w.Funktion("Schreiber", Einzeln((e, _) => new Falsch(1)), new FunktionsOrtKonfig(Wiederholungen: 0));

        (await w.StelleZuAsync(Eingang("A", new Rohwert(5)), TimeSpan.FromSeconds(10), default))!.Ausgang.Should().Be(Quittungsart.Fertig,
            "auf einen Hinweis wird nicht gewartet");
        await Warte(() => gehoert.Contains(5), "der Hörer bekommt den Hinweis");
        w.Hinweise.Single().Art.Should().Be(Eingangsart.Hinweis);
        (await w.StelleZuAsync(Eingang("Schreiber", new Rohwert(1)), TimeSpan.FromSeconds(10), default))!.Ausgang.Should().Be(Quittungsart.Gescheitert,
            "eine Funktion schreibt nie ins Log");
    }

    [Fact]
    public async Task Bleibt_ein_Command_unbekannt_wird_er_mit_derselben_Id_wiederholt()
    {
        await using var w = new Welt();
        var ziel = Id<Ziel>.Neu();
        var mal = 0;
        w.Ausgang = _ => Interlocked.Increment(ref mal) < 3 ? EmitAusgang.Unbekannt : EmitAusgang.Zugestellt;
        w.Funktion("A", Einzeln((e, _) => new Buche(ziel, 1)));
        (await w.StelleZuAsync(Eingang("A", new Rohwert(1)), TimeSpan.FromSeconds(10), default))!.Ausgang.Should().Be(Quittungsart.Fertig);
        w.Gesendet.Select(x => x.Id).Distinct().Should().HaveCount(1, "dieselbe Id bei jedem Versuch — die Inbox verwirft Wiederholungen");
        w.Gesendet.Should().HaveCount(3);
    }
}
