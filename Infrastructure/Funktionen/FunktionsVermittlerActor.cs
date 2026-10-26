using Abstractions;
using Infrastructure.Lebenszyklus;
using Infrastructure.Projections;   // IClusterKindContributor, WakeAck
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Infrastructure.Komposition;

namespace Infrastructure.Funktionen;

/// <summary>
/// Der Vermittler einer Funktion als virtueller Cluster-Actor (Identität = voller Name der Funktion). Er hält die
/// <see cref="FunktionsVermittlung"/>; eine <see cref="HoleArbeit"/>-Anfrage ohne Arbeit PARKT er bis zur Wartezeit
/// (Long-Poll) und bedient sie, sobald ein Angebot kommt — so wartet kein Ausführer im Takt, und neue Arbeit geht sofort raus.
/// </summary>
public sealed class FunktionsVermittlerActor : RahmenActor
{
    public const string KindName = "funktions-vermittler";

    private sealed record Tick;
    private sealed record Geparkt(PID Antwort, HoleArbeit Anfrage, DateTimeOffset Bis);
    private sealed record GeparktEingang(PID Antwort, HoleEingaenge Anfrage, DateTimeOffset Bis);

    private readonly FunktionsVermittlung _kern;
    // Eingänge der Baustein-Form (Plan Pub/Sub-Neustrukturierung §2.2): dieselbe Mechanik, eigene Schlange — ein Vermittler je Funktion.
    private readonly Vermittlung<EingangAnbieten> _eingaenge;
    private readonly Func<DateTimeOffset> _jetzt;
    private readonly LinkedList<Geparkt> _geparkt = new();
    private readonly LinkedList<GeparktEingang> _geparktEingang = new();

    public FunktionsVermittlerActor(TimeSpan lease, Func<DateTimeOffset>? jetzt = null)
    {
        _kern = new FunktionsVermittlung(lease);
        _eingaenge = new Vermittlung<EingangAnbieten>(lease);
        _jetzt = jetzt ?? (() => DateTimeOffset.UtcNow);
    }

    protected override Task BeginneAsync(IContext context)
    {
        PlaneTick(context);
        return Task.CompletedTask;
    }

    protected override Task BehandleAsync(IContext context)
    {
        switch (context.Message)
        {
            case AuftragAnbieten a:
                _kern.Biete(a);
                Bediene(context);
                context.Respond(new WakeAck());
                break;

            case HoleArbeit h:
                var los = _kern.Hole(h.Arbeiter, Math.Max(0, h.Max), _jetzt());
                if (los.Count > 0 || h.WarteMs <= 0 || context.Sender is null)
                    context.Respond(new ArbeitZugeteilt(los));
                else
                {
                    // Ein Ausführer fragt nacheinander, nie parallel — eine neuere Anfrage desselben Arbeiters ersetzt die ältere. Sonst
                    //   bekäme die Arbeit eine Anfrage, deren Absender längst aufgegeben hat (der Cluster wiederholt eine Anfrage nach seinem
                    //   Request-Timeout), und der Auftrag hinge bis zum Ablauf der Lease.
                    for (var n = _geparkt.First; n is not null;)
                    {
                        var weiter = n.Next;
                        if (n.Value.Anfrage.Arbeiter == h.Arbeiter) _geparkt.Remove(n);
                        n = weiter;
                    }
                    _geparkt.AddLast(new Geparkt(context.Sender, h, _jetzt().AddMilliseconds(h.WarteMs)));
                }
                break;

            case EingangAnbieten a:
                _eingaenge.Biete(a);
                BedieneEingaenge(context);
                context.Respond(new WakeAck());
                break;

            case HoleEingaenge h:
                var eingaenge = _eingaenge.Hole(h.Arbeiter, Math.Max(0, h.Max), _jetzt());
                if (eingaenge.Count > 0 || h.WarteMs <= 0 || context.Sender is null)
                    context.Respond(new EingaengeZugeteilt(eingaenge));
                else
                {
                    for (var n = _geparktEingang.First; n is not null;)
                    {
                        var weiter = n.Next;
                        if (n.Value.Anfrage.Arbeiter == h.Arbeiter) _geparktEingang.Remove(n);
                        n = weiter;
                    }
                    _geparktEingang.AddLast(new GeparktEingang(context.Sender, h, _jetzt().AddMilliseconds(h.WarteMs)));
                }
                break;

            case ArbeitLebt l:
                _kern.Lebenszeichen(l.Vorgang, l.Arbeiter, _jetzt());
                _eingaenge.Lebenszeichen(l.Vorgang, l.Arbeiter, _jetzt());
                context.Respond(new WakeAck());
                break;

            case ArbeitErledigt e:
                _kern.Erledigt(e.Vorgang);
                _eingaenge.Erledigt(e.Vorgang);
                context.Respond(new WakeAck());
                break;

            case ListeOffene:
                context.Respond(new OffeneAuftraege(_kern.Offen().Concat(_eingaenge.Offen()).ToList()));
                break;

            case Tick:
                // abgelaufene Leases zurückgeben (und neu verteilen), abgelaufene Wartende leer beantworten
                _kern.GibAbgelaufeneZurück(_jetzt());
                _eingaenge.GibAbgelaufeneZurück(_jetzt());
                Bediene(context);
                BedieneEingaenge(context);
                var jetzt = _jetzt();
                for (var n = _geparktEingang.First; n is not null;)
                {
                    var weiter = n.Next;
                    if (n.Value.Bis <= jetzt)
                    {
                        context.Send(n.Value.Antwort, new EingaengeZugeteilt(Array.Empty<EingangAnbieten>()));
                        _geparktEingang.Remove(n);
                    }
                    n = weiter;
                }
                for (var n = _geparkt.First; n is not null;)
                {
                    var weiter = n.Next;
                    if (n.Value.Bis <= jetzt)
                    {
                        context.Send(n.Value.Antwort, new ArbeitZugeteilt(Array.Empty<AuftragAnbieten>()));
                        _geparkt.Remove(n);
                    }
                    n = weiter;
                }
                PlaneTick(context);
                break;
        }
        return Task.CompletedTask;
    }

    /// <summary>Geparkte Anfragen der Reihe nach bedienen, solange Arbeit da ist.</summary>
    private void Bediene(IContext context)
    {
        while (_geparkt.First is { } n && _kern.Wartend > 0)
        {
            var los = _kern.Hole(n.Value.Anfrage.Arbeiter, n.Value.Anfrage.Max, _jetzt());
            if (los.Count == 0) break;
            _geparkt.RemoveFirst();
            context.Send(n.Value.Antwort, new ArbeitZugeteilt(los));
        }
    }

    /// <summary>Geparkte Hol-Anfragen nach Eingängen der Reihe nach bedienen, solange Arbeit da ist.</summary>
    private void BedieneEingaenge(IContext context)
    {
        while (_geparktEingang.First is { } n && _eingaenge.Wartend > 0)
        {
            var los = _eingaenge.Hole(n.Value.Anfrage.Arbeiter, n.Value.Anfrage.Max, _jetzt());
            if (los.Count == 0) break;
            _geparktEingang.RemoveFirst();
            context.Send(n.Value.Antwort, new EingaengeZugeteilt(los));
        }
    }

    private void PlaneTick(IContext context)
        => Neben(context, stopp => Task.Delay(TimeSpan.FromSeconds(1), stopp), _ =>
        {
            if (!Stopp.IsCancellationRequested) context.Send(context.Self, new Tick());
            return Task.CompletedTask;
        });

    /// <summary>Die Cluster-Identität des Vermittlers einer Funktion.</summary>
    public static ClusterIdentity Identitaet(Type funktion) => ClusterIdentity.Create(funktion.FullName ?? funktion.Name, KindName);
}

/// <summary>Das Cluster-Kind des Vermittlers (eine Instanz je Funktion).</summary>
internal sealed class FunktionsVermittlerKind : IClusterKindContributor
{
    public ClusterKind CreateKind(ActorSystem system, IServiceProvider provider)
        => new(FunktionsVermittlerActor.KindName, Props.FromProducer(() => new FunktionsVermittlerActor(FunktionsAnbieter.Lease)));
}

/// <summary>
/// Die Seite des Dirigenten: einen Auftrag dem Vermittler seiner Funktion anbieten (bounded, verlierbar — der Dirigent bietet
/// bei jeder Weckung erneut an).
/// </summary>
public static class FunktionsAnbieter
{
    /// <summary>Wie lange ein geholter Auftrag ohne Lebenszeichen einem Ausführer gehört.</summary>
    public static readonly TimeSpan Lease = TimeSpan.FromSeconds(30);

    public static async Task BieteAnAsync(ActorSystem system, Guid korrelation, IAuftrag auftrag, Guid vorgang, string? akteur, CancellationToken ct)
    {
        if (!Anwendung.Funktionen.AuftragZuFunktion.TryGetValue(auftrag.GetType(), out var funktion))
            throw new InvalidOperationException($"Der Auftrag {auftrag.GetType().Name} gehört zu keiner Funktion (IAuftrag<F>).");
        // Ohne Antwort bietet der Dirigent bei der nächsten Weckung erneut an (Backstop).
        await new Bote(system.Cluster()).FrageAsync<WakeAck>(FunktionsVermittlerActor.Identitaet(funktion),
            new AuftragAnbieten(vorgang, korrelation, akteur, auftrag, Spur.Aktuell?.Ursache, Spur.Aktuell?.Wirker), TimeSpan.FromSeconds(5), ct);
    }

    /// <summary>
    /// Der Hol-Weg einer Anbieter-Sitzung zum Vermittler: eine Frage, deren Frist die Wartezeit des Long-Polls und Luft für die Antwort ist;
    /// ohne Antwort holt die Sitzung erneut.
    /// </summary>
    public static Func<Type, HoleArbeit, CancellationToken, Task<ArbeitZugeteilt?>> Hole(Bote bote)
        => (f, anfrage, ct) => bote.FrageAsync<ArbeitZugeteilt>(FunktionsVermittlerActor.Identitaet(f), anfrage,
            TimeSpan.FromMilliseconds(Math.Max(0, anfrage.WarteMs)) + TimeSpan.FromSeconds(5), ct);

    /// <summary>Der Melde-Weg einer Anbieter-Sitzung (Lebenszeichen, Erledigt): verlierbar, die Lease heilt.</summary>
    public static Func<Type, object, Task> Melde(Bote bote)
        => (f, nachricht) =>
        {
            bote.Melde(FunktionsVermittlerActor.Identitaet(f), nachricht);
            return Task.CompletedTask;
        };

    /// <summary>
    /// Einblick (Werkbank P4.7): die offenen Aufträge je Funktion — vom Vermittler jeder Funktion, bounded. Ein Vermittler, der nicht antwortet,
    /// fehlt in der Liste (er ist nicht maßgeblich; der Dirigent bietet ohnehin neu an).
    /// </summary>
    public static async Task<IReadOnlyDictionary<string, IReadOnlyList<OffenerAuftrag>>> ListeOffeneAsync(
        ActorSystem system, IEnumerable<Type> funktionen, CancellationToken ct)
    {
        var bote = new Bote(system.Cluster());
        var anfragen = funktionen.Distinct().Select(async f =>
            (f.Name, (await bote.FrageAsync<OffeneAuftraege>(FunktionsVermittlerActor.Identitaet(f), new ListeOffene(), TimeSpan.FromSeconds(3), ct))?.Auftraege));
        return (await Task.WhenAll(anfragen)).Where(x => x.Item2 != null).ToDictionary(x => x.Item1, x => x.Item2!);
    }
}

/// <summary>
/// Der Abholer eines Knotens: für jede hier in C# gebundene Funktion so viele Aufträge holen, wie Slots frei sind, sie mit dem
/// <see cref="FunktionsAusfuehrer"/> rechnen (genau ein Ergebnis im Log, Weckung des Dirigenten), währenddessen Lebenszeichen
/// senden und danach „erledigt“ melden. Python-Worker tun dasselbe über gRPC.
/// </summary>
public sealed class FunktionsAbholer : BackgroundService
{
    private static readonly TimeSpan LebenszeichenAlle = TimeSpan.FromSeconds(10);
    // Long-Poll unter dem Request-Timeout des Clusters (Standard 5 s) — sonst wiederholt der Cluster die Anfrage, während sie geparkt ist.
    private const int WarteMs = 4_000;
    /// <summary>Frist einer Abhol-Frage: die Wartezeit des Long-Polls und Luft für die Antwort.</summary>
    private static readonly TimeSpan HoleFrist = TimeSpan.FromMilliseconds(WarteMs) + TimeSpan.FromSeconds(5);
    private readonly ActorSystem _system;
    private Bote? _bote;
    private readonly FunktionsAusfuehrer _ausfuehrer;
    private readonly IReadOnlyList<FunktionsBindung> _bindungen;
    private readonly ILogger<FunktionsAbholer>? _logger;

    public FunktionsAbholer(ActorSystem system, FunktionsAusfuehrer ausfuehrer, IEnumerable<FunktionsBindung> bindungen,
        ILogger<FunktionsAbholer>? logger = null)
    {
        _system = system;
        _ausfuehrer = ausfuehrer;
        _bindungen = bindungen.Where(b => !b.Extern).GroupBy(b => b.Funktion).Select(g => g.Last()).ToList();
        _logger = logger;
    }

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _bote = new Bote(_system.Cluster(), _logger);
        return Task.WhenAll(_bindungen.Select(b => HoleSchleifeAsync(b, stoppingToken)));
    }

    private async Task HoleSchleifeAsync(FunktionsBindung b, CancellationToken ct)
    {
        var arbeiter = $"{_system.Address}/{b.Funktion.Name}";
        var slots = new SemaphoreSlim(Math.Max(1, b.Slots));
        var identitaet = FunktionsVermittlerActor.Identitaet(b.Funktion);
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await slots.WaitAsync(ct);
                var frei = 1;
                while (slots.CurrentCount > 0 && slots.Wait(0)) frei++;   // alle gerade freien Slots auf einmal anfragen

                ArbeitZugeteilt? zugeteilt = null;
                try
                {
                    zugeteilt = await _bote!.FrageAsync<ArbeitZugeteilt>(identitaet, new HoleArbeit(arbeiter, frei, WarteMs), HoleFrist, ct);
                }
                finally
                {
                    var genutzt = zugeteilt?.Auftraege.Count ?? 0;
                    if (frei - genutzt > 0) slots.Release(frei - genutzt);
                }
                foreach (var a in zugeteilt?.Auftraege ?? Array.Empty<AuftragAnbieten>())
                    _ = FuehreAusAsync(a, arbeiter, identitaet, slots, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { return; }
            catch (Exception ex)
            {
                _logger?.LogDebug(ex, "[Funktion] Abholen von {Funktion} fehlgeschlagen — neuer Versuch", b.Funktion.Name);
                try { await Task.Delay(TimeSpan.FromSeconds(1), ct); } catch (OperationCanceledException) { return; }
            }
        }
    }

    /// <summary>Lebenszeichen/Erledigt an den Vermittler — verlierbar (die Lease heilt).</summary>
    private void Melde(ClusterIdentity identitaet, object nachricht) => _bote!.Melde(identitaet, nachricht);

    private async Task FuehreAusAsync(AuftragAnbieten a, string arbeiter, ClusterIdentity identitaet, SemaphoreSlim slots, CancellationToken ct)
    {
        using var lebt = new CancellationTokenSource();
        var herz = Task.Run(async () =>
        {
            while (!lebt.IsCancellationRequested)
            {
                try { await Task.Delay(LebenszeichenAlle, lebt.Token); } catch (OperationCanceledException) { return; }
                Melde(identitaet, new ArbeitLebt(a.Vorgang, arbeiter));
            }
        });
        try
        {
            using (Spur.Von(a.Ursache, a.Wirker))   // die Spur des Absenders (Regel/Fluss-Knoten), wie beim Anbieten
                await _ausfuehrer.FuehreAusAsync(a.Korrelation, a.Auftrag, a.Vorgang, a.Akteur, ct);
            Melde(identitaet, new ArbeitErledigt(a.Vorgang));
        }
        catch (Exception ex)
        {
            // Nicht erledigt melden: die Lease läuft ab, der Auftrag geht an einen anderen Ausführer.
            _logger?.LogWarning(ex, "[Funktion] Ausführung {Vorgang} abgebrochen", a.Vorgang);
        }
        finally
        {
            lebt.Cancel();
            await herz;
            slots.Release();
        }
    }
}
