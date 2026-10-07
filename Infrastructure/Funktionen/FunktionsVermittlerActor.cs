using Abstractions;
using Infrastructure.Projections;   // IClusterKindContributor, WakeAck
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

namespace Infrastructure.Funktionen;

/// <summary>
/// Der Vermittler einer Funktion als virtueller Cluster-Actor (Identität = voller Name der Funktion). Er hält die
/// <see cref="FunktionsVermittlung"/>; eine <see cref="HoleArbeit"/>-Anfrage ohne Arbeit PARKT er bis zur Wartezeit
/// (Long-Poll) und bedient sie, sobald ein Angebot kommt — so wartet kein Ausführer im Takt, und neue Arbeit geht sofort raus.
/// </summary>
public sealed class FunktionsVermittlerActor : IActor
{
    public const string KindName = "funktions-vermittler";

    private sealed record Tick;
    private sealed record Geparkt(PID Antwort, HoleArbeit Anfrage, DateTimeOffset Bis);

    private readonly FunktionsVermittlung _kern;
    private readonly Func<DateTimeOffset> _jetzt;
    private readonly LinkedList<Geparkt> _geparkt = new();

    public FunktionsVermittlerActor(TimeSpan lease, Func<DateTimeOffset>? jetzt = null)
    {
        _kern = new FunktionsVermittlung(lease);
        _jetzt = jetzt ?? (() => DateTimeOffset.UtcNow);
    }

    public Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Started:
                PlaneTick(context);
                break;

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
                    _geparkt.AddLast(new Geparkt(context.Sender, h, _jetzt().AddMilliseconds(h.WarteMs)));
                break;

            case ArbeitLebt l:
                _kern.Lebenszeichen(l.Vorgang, l.Arbeiter, _jetzt());
                context.Respond(new WakeAck());
                break;

            case ArbeitErledigt e:
                _kern.Erledigt(e.Vorgang);
                context.Respond(new WakeAck());
                break;

            case Tick:
                // abgelaufene Leases zurückgeben (und neu verteilen), abgelaufene Wartende leer beantworten
                _kern.GibAbgelaufeneZurück(_jetzt());
                Bediene(context);
                var jetzt = _jetzt();
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

    private static void PlaneTick(IContext context)
        => context.ReenterAfter(Task.Delay(TimeSpan.FromSeconds(1)), () => context.Send(context.Self, new Tick()));

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
        if (!GeneratedFunktionen.AuftragZuFunktion.TryGetValue(auftrag.GetType(), out var funktion))
            throw new InvalidOperationException($"Der Auftrag {auftrag.GetType().Name} gehört zu keiner Funktion (IAuftrag<F>).");
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try
        {
            await system.Cluster().RequestAsync<WakeAck>(FunktionsVermittlerActor.Identitaet(funktion),
                new AuftragAnbieten(vorgang, korrelation, akteur, auftrag), cts.Token);
        }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException)
        { /* der Dirigent bietet bei der nächsten Weckung erneut an (Backstop) */ }
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
    private readonly ActorSystem _system;
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
        => Task.WhenAll(_bindungen.Select(b => HoleSchleifeAsync(b, stoppingToken)));

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
                    zugeteilt = await _system.Cluster().RequestAsync<ArbeitZugeteilt>(
                        identitaet, new HoleArbeit(arbeiter, frei, 10_000), ct);
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

    /// <summary>Lebenszeichen/Erledigt an den Vermittler — bounded, verlierbar (die Lease heilt).</summary>
    private async Task MeldeAsync(ClusterIdentity identitaet, object nachricht)
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        try { await _system.Cluster().RequestAsync<WakeAck>(identitaet, nachricht, cts.Token); }
        catch (Exception ex) when (ex is OperationCanceledException or TimeoutException) { }
    }

    private async Task FuehreAusAsync(AuftragAnbieten a, string arbeiter, ClusterIdentity identitaet, SemaphoreSlim slots, CancellationToken ct)
    {
        using var lebt = new CancellationTokenSource();
        var herz = Task.Run(async () =>
        {
            while (!lebt.IsCancellationRequested)
            {
                try { await Task.Delay(LebenszeichenAlle, lebt.Token); } catch (OperationCanceledException) { return; }
                await MeldeAsync(identitaet, new ArbeitLebt(a.Vorgang, arbeiter));
            }
        });
        try
        {
            await _ausfuehrer.FuehreAusAsync(a.Korrelation, a.Auftrag, a.Vorgang, a.Akteur, ct);
            await MeldeAsync(identitaet, new ArbeitErledigt(a.Vorgang));
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
