using System;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using FluentAssertions;
using Infrastructure.Lebenszyklus;
using Proto;
using Xunit;

namespace Infrastructure.Pruefstand.Lebenszyklus;

/// <summary>
/// Der Lebenszyklus jedes Framework-Actors (<see cref="RahmenActor"/>): der Fähigkeits-Bereich wird nach
/// Passivierung, Stopp und Neustart frei — aber erst, wenn keine Arbeit ihn mehr nutzt; ein Stopp bricht ab und wartet begrenzt; ein Fehler wird
/// ehrlich beantwortet; Weckungen während eines Durchlaufs ergeben genau einen weiteren. Lokales <see cref="ActorSystem"/> ohne Cluster und eine
/// Proben-Unterklasse; die echten Actors im Cluster prüft <see cref="LebenszyklusImClusterTests"/>. Zeitkritisch (echte Uhr).
/// </summary>
[Collection(Zeitkritisch.Name)]
[Trait("Kategorie", "Garantie")]
public class RahmenActorTests
{
    private static readonly TimeSpan Geduld = TimeSpan.FromSeconds(5);

    /// <summary>Was die Probe tut und was mit ihrem Bereich geschieht.</summary>
    private sealed class Werkzeug
    {
        public ZaehlFabrik Fabrik { get; } = new();
        public int Starts;
        public int Durchlaeufe;
        public TaskCompletionSource ErsterLaeuft { get; } = Neu();
        public TaskCompletionSource ZweiterFertig { get; } = Neu();
        public TaskCompletionSource Tor { get; } = Neu();
        public TaskCompletionSource Abgebrochen { get; } = Neu();
        public TaskCompletionSource<string> NebenGescheitert { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        /// <summary>Hält sich der Durchlauf an das Stopp-Token (bricht er ab, bevor er auf das Tor wartet)?</summary>
        public bool HoertAufStopp { get; init; }
        /// <summary>Konnte der Durchlauf nach dem Tor seinen Bereich noch benutzen?</summary>
        public bool? BereichBenutzbar;
        /// <summary>War der Actor schon im Stopp (Token abgebrochen), als das Tor aufging?</summary>
        public bool? GestopptAmTor;

        public async Task DurchlaufAsync(IFaehigkeiten bereich, CancellationToken stopp)
        {
            var nr = Interlocked.Increment(ref Durchlaeufe);
            if (nr == 1)
            {
                ErsterLaeuft.TrySetResult();
                if (HoertAufStopp)
                {
                    try { await Task.Delay(Timeout.Infinite, stopp); }
                    catch (OperationCanceledException) { Abgebrochen.TrySetResult(); }
                }
                await Tor.Task;
                GestopptAmTor = stopp.IsCancellationRequested;
                try { bereich.Hole<ZaehlFabrik.Bereich>(); BereichBenutzbar = true; }
                catch (ObjectDisposedException) { BereichBenutzbar = false; }
            }
            if (nr == 2) ZweiterFertig.TrySetResult();
        }
    }

    private sealed class Probe(Werkzeug w, TimeSpan leerlauf = default, TimeSpan? abwarten = null)
        : RahmenActor(null, leerlauf, w.Fabrik, abwarten)
    {
        protected override Task BeginneAsync(IContext context)
        {
            Interlocked.Increment(ref w.Starts);
            return Task.CompletedTask;
        }

        protected override Task BehandleAsync(IContext context)
        {
            switch (context.Message)
            {
                case "wecke":
                    var bereich = Bereich;
                    Wecke(context, stopp => w.DurchlaufAsync(bereich, stopp));
                    break;
                case "kaputt":
                    throw new InvalidOperationException("kaputt");
                case "ping":
                    context.Respond("pong");
                    break;
                case "neben-wirft":
                    // Eine Arbeit ohne async, die schon beim Aufruf wirft (etwa ein Seam, der „=> throw …“ schreibt).
                    Neben(context, _ => throw new InvalidOperationException("sofort"), t =>
                    {
                        w.NebenGescheitert.TrySetResult(t.Exception?.GetBaseException().Message ?? "");
                        return Task.CompletedTask;
                    });
                    break;
            }
            return Task.CompletedTask;
        }

        protected override object? Fehlerantwort(object nachricht, Exception fehler) => nachricht is "kaputt" ? "ehrlich: " + fehler.Message : null;
    }

    private static TaskCompletionSource Neu() => new(TaskCreationOptions.RunContinuationsAsynchronously);

    private static (ActorSystem System, PID Pid) Starte(Werkzeug w, TimeSpan leerlauf = default, TimeSpan? abwarten = null)
    {
        var system = new ActorSystem();
        return (system, system.Root.Spawn(Props.FromProducer(() => new Probe(w, leerlauf, abwarten))));
    }

    [Fact]
    public async Task Der_Bereich_wird_nach_der_Passivierung_freigegeben()
    {
        var w = new Werkzeug();
        var (system, _) = Starte(w, leerlauf: TimeSpan.FromMilliseconds(200));

        await w.Fabrik.Frei.Task.WaitAsync(Geduld);
        (w.Fabrik.Geoeffnet, w.Fabrik.Freigegeben).Should().Be((1, 1), "ein Bereich je Aktivierung, freigegeben mit ihr");
        await system.ShutdownAsync();
    }

    [Fact]
    public async Task Ein_Durchlauf_haelt_die_Passivierung_auf()
    {
        var w = new Werkzeug();
        var (system, pid) = Starte(w, leerlauf: TimeSpan.FromMilliseconds(200));
        system.Root.Send(pid, "wecke");
        await w.ErsterLaeuft.Task.WaitAsync(Geduld);

        await Task.Delay(800);
        w.Fabrik.Frei.Task.IsCompleted.Should().BeFalse("viermal so lang wie der Leerlauf, aber der Durchlauf läuft noch");

        w.Tor.SetResult();
        await w.Fabrik.Frei.Task.WaitAsync(Geduld);
        w.GestopptAmTor.Should().BeFalse("während des Durchlaufs passiviert der Actor nicht — er stoppt erst danach");
        w.BereichBenutzbar.Should().BeTrue();
        await system.ShutdownAsync();
    }

    [Fact]
    public async Task Ein_Stopp_bricht_den_Durchlauf_ab_wartet_auf_ihn_und_gibt_den_Bereich_danach_frei()
    {
        var w = new Werkzeug { HoertAufStopp = true };
        var (system, pid) = Starte(w, abwarten: Geduld);
        system.Root.Send(pid, "wecke");
        await w.ErsterLaeuft.Task.WaitAsync(Geduld);

        var stopp = system.Root.StopAsync(pid);
        await w.Abgebrochen.Task.WaitAsync(Geduld);                // das Stopp-Token ist abgebrochen
        await Task.Delay(300);
        stopp.IsCompleted.Should().BeFalse("der Stopp wartet auf den Durchlauf");
        w.Fabrik.Frei.Task.IsCompleted.Should().BeFalse("der Durchlauf nutzt den Bereich noch");

        w.Tor.SetResult();
        await stopp.WaitAsync(Geduld);
        await w.Fabrik.Frei.Task.WaitAsync(Geduld);
        w.BereichBenutzbar.Should().BeTrue("freigegeben wird erst nach dem Durchlauf");
        await system.ShutdownAsync();
    }

    [Fact]
    public async Task Ein_Stopp_wartet_nur_begrenzt_und_der_Bereich_gehoert_dem_Durchlauf_bis_zu_seinem_Ende()
    {
        // Gegenprobe zum Befund (docs/06 §6.7): der alte Batch-Leser gab den Bereich beim Stopp frei, während der Durchlauf ihn noch nutzte.
        var w = new Werkzeug();                                    // hört nicht auf das Token (etwa ein laufender Datenbank-Befehl)
        var (system, pid) = Starte(w, abwarten: TimeSpan.FromMilliseconds(300));
        system.Root.Send(pid, "wecke");
        await w.ErsterLaeuft.Task.WaitAsync(Geduld);

        await system.Root.StopAsync(pid).WaitAsync(Geduld);        // der Stopp hängt nicht am Durchlauf
        w.Fabrik.Frei.Task.IsCompleted.Should().BeFalse("der Durchlauf nutzt den Bereich noch");

        w.Tor.SetResult();
        await w.Fabrik.Frei.Task.WaitAsync(Geduld);
        w.BereichBenutzbar.Should().BeTrue("der Bereich wurde erst nach dem Durchlauf frei");
        await system.ShutdownAsync();
    }

    [Fact]
    public async Task Ein_Fehler_wird_ehrlich_beantwortet_und_der_Actor_startet_frisch()
    {
        var w = new Werkzeug();
        var (system, pid) = Starte(w);

        var antwort = await system.Root.RequestAsync<string>(pid, "kaputt", Geduld);
        antwort.Should().Be("ehrlich: kaputt", "der Absender läuft nicht in seine Frist");

        await Warte(() => w.Starts == 2 && w.Fabrik.Freigegeben == 1);
        w.Fabrik.Geoeffnet.Should().Be(2, "die neue Instanz hat einen eigenen Bereich, der alte ist frei");
        (await system.Root.RequestAsync<string>(pid, "ping", Geduld)).Should().Be("pong");
        await system.ShutdownAsync();
    }

    [Fact]
    public async Task Eine_Arbeit_die_schon_beim_Aufruf_wirft_ist_ein_gescheiterter_Lauf_der_Actor_laeuft_weiter()
    {
        var w = new Werkzeug();
        var (system, pid) = Starte(w);
        system.Root.Send(pid, "neben-wirft");

        (await w.NebenGescheitert.Task.WaitAsync(Geduld)).Should().Be("sofort", "danach sieht den Wurf als gescheiterten Task");
        (await system.Root.RequestAsync<string>(pid, "ping", Geduld)).Should().Be("pong");
        w.Starts.Should().Be(1, "kein Neustart — der Wurf gehört der Arbeit, nicht der Mailbox");
        await system.ShutdownAsync();
    }

    [Fact]
    public async Task Weckungen_waehrend_eines_Durchlaufs_ergeben_genau_einen_weiteren()
    {
        var w = new Werkzeug();
        var (system, pid) = Starte(w);
        system.Root.Send(pid, "wecke");
        await w.ErsterLaeuft.Task.WaitAsync(Geduld);
        for (var i = 0; i < 10; i++) system.Root.Send(pid, "wecke");
        (await system.Root.RequestAsync<string>(pid, "ping", Geduld)).Should().Be("pong", "die Mailbox staut sich nicht hinter dem Durchlauf");

        w.Tor.SetResult();
        await w.ZweiterFertig.Task.WaitAsync(Geduld);
        await Task.Delay(300);
        w.Durchlaeufe.Should().Be(2, "zehn Weckungen während des ersten Durchlaufs ergeben genau einen weiteren");

        system.Root.Send(pid, "wecke");
        await Warte(() => w.Durchlaeufe == 3);
        await system.ShutdownAsync();
    }

    private static async Task Warte(Func<bool> bedingung)
    {
        var bis = DateTime.UtcNow + Geduld;
        while (!bedingung())
        {
            if (DateTime.UtcNow > bis) throw new TimeoutException("Bedingung nicht erreicht");
            await Task.Delay(20);
        }
    }
}

/// <summary>Öffnet zählbare Bereiche; ein freigegebener Bereich verweigert jede Fähigkeit.</summary>
internal sealed class ZaehlFabrik : IFaehigkeitsFabrik
{
    public int Geoeffnet;
    public int Freigegeben;
    /// <summary>Erfüllt, sobald ein Bereich freigegeben ist.</summary>
    public TaskCompletionSource Frei { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    public IFaehigkeitsBereich Oeffne()
    {
        Interlocked.Increment(ref Geoeffnet);
        return new Bereich(this);
    }

    public sealed class Bereich(ZaehlFabrik fabrik) : IFaehigkeitsBereich
    {
        private int _frei;

        public T Hole<T>() where T : class
        {
            if (Volatile.Read(ref _frei) == 1) throw new ObjectDisposedException(nameof(Bereich));
            return this as T ?? throw new InvalidOperationException($"Keine Fähigkeit {typeof(T).Name}");
        }

        public void Dispose()
        {
            if (Interlocked.Exchange(ref _frei, 1) == 1) return;
            Interlocked.Increment(ref fabrik.Freigegeben);
            fabrik.Frei.TrySetResult();
        }
    }
}
