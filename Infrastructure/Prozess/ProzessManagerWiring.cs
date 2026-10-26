using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Infrastructure.Lebenszyklus;
using Infrastructure.Projections;   // IClusterKindContributor, WakeAck
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;
using Infrastructure.Komposition;

namespace Infrastructure.Prozess;

/// <summary>
/// Der Kind-Contributor für den EINEN generischen Prozess-Manager (eine Instanz pro Korrelation).
/// <c>system.Cluster()</c> wird in der SPAWN-Factory aufgelöst (Actor-Started, Cluster fertig) — der
/// (A)-Hang-Fix; NICHT bei der Kind-Registrierung.
/// </summary>
internal sealed class ProzessManagerKind : IClusterKindContributor
{
    public ClusterKind CreateKind(ActorSystem system, IServiceProvider provider)
    {
        var eventStore = provider.GetRequiredService<IEventStoreRepository>();
        var registry = provider.GetRequiredService<IReadOnlyDictionary<string, ProzessRegeln>>();
        var offenIndex = provider.GetRequiredService<IProzessOffenIndex>();
        var deadLetters = provider.GetService<IDeadLetterSink>();   // ★ #12: optional, best-effort Ops-Sicht
        var markingStore = provider.GetService<IProzessMarkingStore>();   // ★ P5b: optional, best-effort Cursor
        var uhr = provider.GetService<IDbClock>();   // Zeitlimit gegen die DB-Uhr (Event-Zeitstempel sind DB-generiert)
        var passivierung = provider.GetService<Infrastructure.Lebenszyklus.Passivierung>();   // PF-5
        // Rufe<F>/Fluss-Funktionen: der Auftrag geht an den VERMITTLER seiner Funktion (Pull durch die Ausführer, §14.5).
        Func<Guid, IAuftrag, Guid, string?, CancellationToken, Task> rufe =
            (k, a, v, akteur, ct) => Infrastructure.Funktionen.FunktionsAnbieter.BieteAnAsync(system, k, a, v, akteur, ct);
        return new ClusterKind(ProzessManagerActor.KindName, Props.FromProducer(() =>
            new ProzessManagerActor(eventStore, registry, system.Cluster(), offenIndex, deadLetters, markingStore, rufe, uhr, passivierung)));
    }
}

/// <summary>
/// Verdrahtet die Korrelations-Zustellung (das Gegenstück zum <c>GenericPullStartupService</c>, aber
/// nach Korrelation statt StreamId): EIN <see cref="KorrelationsRouterActor"/> abonniert die Signale ALLER
/// teilnehmenden Prozess-Events und weckt daraus den zuständigen Manager; ein Poll-Backstop heilt verlorene
/// letzte Weckungen. Beide reichen ihr Event durch DENSELBEN <see cref="KorrelationsRouter"/>.
/// </summary>
public sealed class ProzessManagerStartupService : IHostedService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(30);
    // Der §3-Backstop (PF-4): jede Sekunde die fälligen Fristen (Zeitlimit, Warte-Knoten) aus dem Offen-Index, alle 15 s der Abgleich
    //   des Heil-Plans (Heil-Weckung nach Stille, mit Backoff je Vorgang).
    private static readonly TimeSpan FristTakt = TimeSpan.FromSeconds(1);
    private static readonly TimeSpan AbgleichTakt = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan WeckFrist = TimeSpan.FromSeconds(10);
    private const int BackstopParallel = 32;
    private const string RouterId = "prozess-manager-router";

    private readonly ActorSystem _system;
    private Bote? _bote;
    private readonly IEventStoreRepository _store;
    private readonly IReadOnlyDictionary<string, ProzessRegeln> _registry;
    private readonly IPollCursorStore _pollCursors;
    private readonly IProzessOffenIndex _offenIndex;
    private readonly IEnumerable<Infrastructure.Funktionen.FunktionsBindung> _bindungen;
    private readonly IDbClock? _uhr;
    private readonly ILogger<ProzessManagerStartupService>? _logger;
    private readonly Heilplan _heilplan = new();
    // Vorgänge, deren Backstop-Weckung gerade läuft (keine zweite daneben, wenn eine hängt).
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, byte> _imFlug = new();

    private PID? _routerPid;
    private CancellationTokenSource? _pollCts;
    private Task? _pollLoop;
    private Task? _backstopLoop;

    public ProzessManagerStartupService(
        ActorSystem system,
        IEventStoreRepository store,
        IReadOnlyDictionary<string, ProzessRegeln> registry,
        IPollCursorStore pollCursors,
        IProzessOffenIndex offenIndex,
        IEnumerable<Infrastructure.Funktionen.FunktionsBindung> bindungen,
        IDbClock? uhr = null,
        ILogger<ProzessManagerStartupService>? logger = null)
    {
        _bindungen = bindungen;
        _uhr = uhr;
        _system = system;
        _store = store;
        _registry = registry;
        _pollCursors = pollCursors;
        _offenIndex = offenIndex;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        if (_registry.Count == 0)
        {
            Console.WriteLine("=== Prozess-Manager: keine IProzessDefinition — nichts zu verdrahten ===");
            return;
        }
        var bote = _bote = new Bote(_system.Cluster(), _logger);

        // ★ Audit-Fix (fail-fast): Azyklizitäts-Boot-Guard (docs/04 §4.5). Jetzt AKTIV — er speist sich aus der
        //   PRÄZISEN Command→Event-Map (GeneratedCommandRouting.CommandToEvents, aus den Decide-OneOf-Rückgaben),
        //   nicht mehr aus der aggregat-groben Map, die Falsch-Zyklen erzeugte. Ein tatsächlich zyklischer
        //   Regelsatz (Command → Event → Command → …) bricht damit am Start statt zur Laufzeit endlos zu feuern.
        //   Eine gerufene Funktion ist darin ein Knoten wie ein Command (Bedingung → Funktion → ihre Ergebnis-Events).
        Abstractions.ProzessAzyklizität.PrüfeAlle(_registry, Infrastructure.Funktionen.FunktionsExtensions.Produziert);
        // Jede gerufene Funktion muss gebunden sein (fail-fast statt eines still unbeantworteten Auftrags).
        Infrastructure.Funktionen.FunktionsExtensions.PrüfeBindungen(_registry, _bindungen);

        // Auslöser-Typ → Prozess-Name (der Start bindet den Typ); Union aller teilnehmenden Event-Typen.
        var auslöserZuProzess = new Dictionary<Type, string>();
        var alleTeilnehmende = new HashSet<Type>();
        foreach (var kv in _registry)
        {
            // ★ Audit-Fix H6 (fail-fast): Zwei Prozesse mit demselben Auslöser-Typ würden sich hier still
            //   überschreiben — der Router startet daraus nur EINEN, der andere liefe NIE an (stiller Verlust).
            //   Lieber laut am Boot scheitern als einen Prozess stumm zu verschlucken.
            if (auslöserZuProzess.TryGetValue(kv.Value.AuslöserTyp, out var schon))
                throw new InvalidOperationException(
                    $"Auslöser-Kollision: die Prozesse '{schon}' und '{kv.Key}' hören beide auf das Auslöse-Event " +
                    $"'{kv.Value.AuslöserTyp.Name}'. Der Korrelations-Router kann daraus nur EINEN Prozess starten. " +
                    $"Jeder Prozess braucht einen eigenen Auslöser-Typ.");

            auslöserZuProzess[kv.Value.AuslöserTyp] = kv.Key;
            foreach (var t in kv.Value.TeilnehmendeEvents) alleTeilnehmende.Add(t);
        }
        var signalTypes = KorrelationsRouter.SignalTypesFor(alleTeilnehmende);

        // Signal → ProzessWake an (prozess-manager, Korrelation), gemeldet (ein Signal darf verloren gehen, der Poll heilt).
        Func<Guid, Guid, int, string?, CancellationToken, Task> wecke =
            (korr, quellStream, quellVersion, prozessNameFürStart, c) =>
            {
                _heilplan.Aktiv(korr, DateTimeOffset.UtcNow);
                bote.Melde(ClusterIdentity.Create(korr.ToString(), ProzessManagerActor.KindName),
                    new ProzessWake(quellStream, quellVersion, prozessNameFürStart));
                return Task.CompletedTask;
            };

        var router = new KorrelationsRouter(_store, auslöserZuProzess, wecke, Anwendung.Signale.EventToSignal);
        _routerPid = _system.Root.Spawn(Props.FromProducer(() =>
            new KorrelationsRouterActor(RouterId, signalTypes, router)));

        // Poll nach Korrelation (PF-4, G-8): das Log seitenweise ab der Marke, die geladenen Events direkt routen, bestätigt wecken.
        _pollCts = new CancellationTokenSource();
        var startHwm = await _pollCursors.GetAsync(RouterId, ct);
        var poll = new ProzessPoll(_store, _offenIndex, auslöserZuProzess, WeckeBestaetigtAsync,
            (hwm, c) => _pollCursors.SetAsync(RouterId, hwm, c), startHwm);
        _pollLoop = PollLoopAsync(poll, _pollCts.Token);

        // §3-Backstop: das Liveness-Netz für das, was weder Signal noch Poll sehen — fällige Fristen (Zeitlimit, Warte-Knoten) und die
        //   Heil-Weckung eines still gewordenen Vorgangs (verlorene Selbst-Weckung ohne Ziel-Event, Absturz). Der geweckte Manager faltet
        //   neu und treibt weiter bzw. schreibt ProzessBeendet.
        _backstopLoop = BackstopLoopAsync(_pollCts.Token);

        Console.WriteLine();
        Console.WriteLine($"=== Prozess-Manager (generisch): {_registry.Count} Prozess(e), Router auf {signalTypes.Count} Signal-Typen (+ Poll {PollInterval.TotalSeconds:0}s, ab HWM {startHwm}, + Backstop: Fristen {FristTakt.TotalSeconds:0}s, Heilung ab {Heilplan.Anfang.TotalSeconds:0}s Stille) ===");
        foreach (var kv in _registry)
            Console.WriteLine($"  ✓ {kv.Key} (Auslöser {kv.Value.AuslöserTyp.Name}, {kv.Value.Regeln.Count} Regeln)");
        Console.WriteLine();
    }

    private async Task PollLoopAsync(ProzessPoll poll, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(PollInterval, ct); }
            catch (OperationCanceledException) { break; }

            try { await poll.DurchlaufAsync(ct); }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                // Ein Durchlauf, der scheitert, rückt die Hochwassermarke nicht vor — scheitert jeder, startet ein Vorgang mit verlorenem
                //   Start-Signal nie (so sah G-7 aus). Deshalb laut, nicht nur auf der Konsole.
                if (_logger is null) Console.WriteLine($"[Prozess-Poll] fehlgeschlagen: {ex.Message}");
                else _logger.LogWarning(ex, "[Prozess-Poll] Durchlauf ab HWM {Hwm} fehlgeschlagen — die Marke steht", poll.Hochwasser);
            }
        }
    }

    /// <summary>
    /// Eine bestätigte Weckung des Polls (G-8): eine Frage mit Frist; kein Ack oder „nicht aufgeholt“ (die Weckung scheiterte im Manager) =
    /// unbestätigt.
    /// </summary>
    private async Task<bool> WeckeBestaetigtAsync(ProzessPollWeckung w, CancellationToken ct)
    {
        var ack = await _bote!.FrageAsync<WakeAck>(ClusterIdentity.Create(w.Korrelation.ToString(), ProzessManagerActor.KindName),
            new ProzessWake(w.Stream, w.Version, w.ProzessNameFürStart), WeckFrist, ct);
        if (ack is not { Aufgeholt: true }) return false;
        _heilplan.Aktiv(w.Korrelation, DateTimeOffset.UtcNow);
        return true;
    }

    /// <summary>
    /// §3-Backstop (PF-4): jede Sekunde die Vorgänge mit fälliger Frist wecken (Zeitlimit genau auf die Sekunde, durabel über Neustarts),
    /// alle 15 s den Heil-Plan mit dem Offen-Index abgleichen und die still gewordenen wecken. Je Vorgang höchstens eine Weckung zugleich;
    /// sie ist idempotent (der Manager faltet frisch).
    /// </summary>
    private async Task BackstopLoopAsync(CancellationToken ct)
    {
        var naechsterAbgleich = DateTimeOffset.UtcNow + AbgleichTakt;
        using var drossel = new SemaphoreSlim(BackstopParallel);
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(FristTakt, ct); }
            catch (OperationCanceledException) { break; }

            try
            {
                var bis = _uhr is null ? DateTimeOffset.UtcNow : await _uhr.JetztAsync(ct);
                foreach (var korr in await _offenIndex.ListeFaelligeAsync(bis, ct))
                    _ = WeckeImBackstopAsync(korr, drossel, ct);

                var jetzt = DateTimeOffset.UtcNow;
                if (jetzt < naechsterAbgleich) continue;
                naechsterAbgleich = jetzt + AbgleichTakt;
                _heilplan.Abgleich(await _offenIndex.ListeOffeneAsync(ct), jetzt);
                foreach (var korr in _heilplan.Faellige(jetzt))
                    _ = WeckeImBackstopAsync(korr, drossel, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { Console.WriteLine($"[Prozess-Backstop] Scan fehlgeschlagen: {ex.Message}"); }
        }
    }

    /// <summary>Eine bounded, fire-and-forget-Weckung eines offenen Vorgangs durch den Backstop.</summary>
    private async Task WeckeImBackstopAsync(Guid korrelation, SemaphoreSlim drossel, CancellationToken ct)
    {
        if (!_imFlug.TryAdd(korrelation, 0)) return;
        try
        {
            await drossel.WaitAsync(ct);
            try
            {
                await _bote!.FrageAsync<WakeAck>(ClusterIdentity.Create(korrelation.ToString(), ProzessManagerActor.KindName),
                    new ProzessWake(Guid.Empty, 0, null, VomBackstop: true), WeckFrist, ct);
            }
            finally { drossel.Release(); }
        }
        catch (OperationCanceledException) { /* der nächste Takt wiederholt */ }
        catch (Exception ex) { Console.WriteLine($"[Prozess-Backstop] Weckung {korrelation} fehlgeschlagen: {ex.Message}"); }
        finally { _imFlug.TryRemove(korrelation, out _); }
    }

    public async Task StopAsync(CancellationToken ct)
    {
        _pollCts?.Cancel();
        if (_pollLoop != null) { try { await _pollLoop; } catch { /* egal */ } }
        if (_backstopLoop != null) { try { await _backstopLoop; } catch { /* egal */ } }
        if (_routerPid != null) await _system.Root.StopAsync(_routerPid);
    }
}

/// <summary>
/// Der EINE Aufruf für den Host: registriert die generierte Regel-Registry, das generische Manager-Kind
/// und die Korrelations-Zustellung. Kein per-Prozess-Glue — die Prozesse stecken nur in den
/// <c>IProzessDefinition</c>/<c>IPipeline</c> der Domäne → <c>GeneratedProzessVerbund.Alle</c>.
/// </summary>
public static class GeneratedProzesse
{
    /// <summary>
    /// Die Registry aller Prozesse und Flüsse: der generierte Verbund der Registries jeder Domänen-Assembly (ProzessVerbundGenerator;
    /// ein Name zweimal = CQRS012, Prozesse ohne Registry = CQRS099 — beides im Build).
    /// </summary>
    public static IReadOnlyDictionary<string, ProzessRegeln> AlleRegeln() => Anwendung.Prozesse;

    public static IServiceCollection AddGeneratedProzesse(this IServiceCollection services)
    {
        services.AddSingleton<IReadOnlyDictionary<string, ProzessRegeln>>(AlleRegeln());
        services.AddSingleton<IClusterKindContributor, ProzessManagerKind>();
        // Der Ausführer der Katalog-Funktionen (Rufe<F>); die Funktionen selbst bindet der Host mit AddFunktion<F, Impl>().
        services.AddSingleton(Infrastructure.Funktionen.FunktionsExtensions.BaueAusfuehrer);
        // Vermittler je Funktion (Cluster-Kind) + der Abholer dieses Knotens für die hier in C# gebundenen Funktionen.
        services.AddSingleton<IClusterKindContributor, Infrastructure.Funktionen.FunktionsVermittlerKind>();
        services.AddHostedService<Infrastructure.Funktionen.FunktionsAbholer>();
        services.AddHostedService<ProzessManagerStartupService>();
        return services;
    }
}
