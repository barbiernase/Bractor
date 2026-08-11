using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Infrastructure.Projections;   // IClusterKindContributor, WakeAck
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Proto;
using Proto.Cluster;

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
        return new ClusterKind(ProzessManagerActor.KindName, Props.FromProducer(() =>
            new ProzessManagerActor(eventStore, registry, system.Cluster(), offenIndex, deadLetters, markingStore)));
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
    // Der §3-Backstop ist das KORREKTHEITS-Netz (er heilt hängende Prozesse); der Poll oben ist danach nur
    // noch Latenz-Optimierung. Etwas kürzeres Intervall → geringere Hang-Latenz im verlorene-Selbst-Weckung-Fall.
    private static readonly TimeSpan BackstopInterval = TimeSpan.FromSeconds(15);
    private const string RouterId = "prozess-manager-router";

    private readonly ActorSystem _system;
    private readonly IEventStoreRepository _store;
    private readonly IReadOnlyDictionary<string, ProzessRegeln> _registry;
    private readonly IPollCursorStore _pollCursors;
    private readonly IProzessOffenIndex _offenIndex;
    private readonly ILogger<ProzessManagerStartupService>? _logger;

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
        ILogger<ProzessManagerStartupService>? logger = null)
    {
        _system = system;
        _store = store;
        _registry = registry;
        _pollCursors = pollCursors;
        _offenIndex = offenIndex;
        _logger = logger;
    }

    /// <summary>Prozess-Wake fire-and-forget, aber Fehler gefangen + geloggt (nicht als unbeobachtete Task-Exception).</summary>
    private async Task ProzessWakeBeobachtetAsync(ClusterIdentity identity, ProzessWake wake, CancellationToken c)
    {
        try
        {
            await _system.Cluster().RequestAsync<WakeAck>(identity, wake, c);
        }
        catch (Exception ex)
        {
            _logger?.LogDebug(ex,
                "[Prozess] Wake an {Identity} fehlgeschlagen — verlierbar, §3-Backstop heilt.", identity.Identity);
        }
    }

    public async Task StartAsync(CancellationToken ct)
    {
        if (_registry.Count == 0)
        {
            Console.WriteLine("=== Prozess-Manager: keine IProzessDefinition — nichts zu verdrahten ===");
            return;
        }

        // ★ Audit-Fix (fail-fast): Azyklizitäts-Boot-Guard (Spec §3). Jetzt AKTIV — er speist sich aus der
        //   PRÄZISEN Command→Event-Map (GeneratedCommandRouting.CommandToEvents, aus den Decide-OneOf-Rückgaben),
        //   nicht mehr aus der aggregat-groben Map, die Falsch-Zyklen erzeugte. Ein tatsächlich zyklischer
        //   Regelsatz (Command → Event → Command → …) bricht damit am Start statt zur Laufzeit endlos zu feuern.
        Abstractions.ProzessAzyklizität.PrüfeAlle(
            _registry, Infrastructure.Mapping.GeneratedCommandRouting.Produziert);

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

        // Weckung → ProzessWake an (prozess-manager, Korrelation), fire-and-forget.
        Func<Guid, Guid, int, string?, CancellationToken, Task> wecke =
            (korr, quellStream, quellVersion, prozessNameFürStart, c) =>
            {
                var identity = ClusterIdentity.Create(korr.ToString(), ProzessManagerActor.KindName);
                // ★ Härtung (Multi-Node): fire-and-forget, Send-Fehler beobachtet (nicht still verschluckt).
                _ = ProzessWakeBeobachtetAsync(
                    identity, new ProzessWake(quellStream, quellVersion, prozessNameFürStart), c);
                return Task.CompletedTask;
            };

        var router = new KorrelationsRouter(_store, auslöserZuProzess, wecke);
        _routerPid = _system.Root.Spawn(Props.FromProducer(() =>
            new KorrelationsRouterActor(RouterId, signalTypes, router)));

        // Poll-Backstop nach Korrelation: geänderte Streams scannen, relevante Events erneut routen.
        _pollCts = new CancellationTokenSource();
        var startHwm = await _pollCursors.GetAsync(RouterId, ct);
        _pollLoop = PollLoopAsync(router, alleTeilnehmende, startHwm, _pollCts.Token);

        // §3-Backstop: den durablen Offen-Index periodisch scannen und jeden noch offenen Prozess direkt
        // wecken. Das ist das Liveness-Netz für den einen Fall, den weder Signal-Router noch Poll fangen:
        // ein Prozess, dessen terminale/kompensierende Selbst-Weckung verloren ging (das auslösende
        // Ergebnis-Event ist Auslöser keiner Regel → wird nirgends geroutet). Der geweckte Manager faltet
        // neu und schreibt ProzessBeendet bzw. treibt die Kompensation zu Ende.
        _backstopLoop = BackstopLoopAsync(_pollCts.Token);

        Console.WriteLine();
        Console.WriteLine($"=== Prozess-Manager (generisch): {_registry.Count} Prozess(e), Router auf {signalTypes.Count} Signal-Typen (+ Poll {PollInterval.TotalSeconds:0}s, ab HWM {startHwm}, + Backstop {BackstopInterval.TotalSeconds:0}s) ===");
        foreach (var kv in _registry)
            Console.WriteLine($"  ✓ {kv.Key} (Auslöser {kv.Value.AuslöserTyp.Name}, {kv.Value.Regeln.Count} Regeln)");
        Console.WriteLine();
    }

    private async Task PollLoopAsync(
        KorrelationsRouter router, HashSet<Type> relevanteTypen, long hwm, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(PollInterval, ct); }
            catch (OperationCanceledException) { break; }

            try
            {
                // ★ P5(a): die offenen Korrelationen EINMAL je Poll-Zyklus laden (billig) → der Filter routet
                //   zusätzlich das terminale/teilnehmende Ergebnis-Event eines OFFENEN Prozesses per
                //   CorrelationId (event-getrieben, präzise), statt es allein dem Brute-Force-Backstop zu
                //   überlassen. Der ProzessOffenIndex-Backstop BLEIBT (Auflage A2): er heilt den fully-stalled
                //   Prozess OHNE Stream-Änderung, den dieses Poll-Routing strukturell nicht sieht.
                var offene = new HashSet<Guid>(await _offenIndex.ListeOffeneAsync(ct));
                var changes = await _store.ReadChangedStreamsAsync(hwm, ct);
                foreach (var streamId in changes.StreamIds)
                {
                    var events = await _store.ReadStreamAsync(streamId, 0, ct);
                    foreach (var env in events)
                        if (ProzessPollFilter.SollRouten(env.Payload.GetType(), env.CorrelationId, relevanteTypen, offene))
                            await router.RouteAsync(streamId, env.AggregateVersion, env.Payload, env.CorrelationId, ct);
                }
                hwm = changes.HighWaterMark;
                await _pollCursors.SetAsync(RouterId, hwm, ct);
            }
            catch (Exception ex) { Console.WriteLine($"[Prozess-Poll] fehlgeschlagen: {ex.Message}"); }
        }
    }

    /// <summary>
    /// §3-Backstop: alle offenen Prozesse aus dem durablen Index holen und je Korrelation den Manager wecken.
    /// Fire-and-forget je Weckung, aber mit BOUNDED Token (kein <c>None</c>-Infinit-Retry, die (A)-Lektion);
    /// die Weckung ist idempotent (WakeAsync faltet frisch), also ist ein Doppel-/Extra-Wecken folgenlos.
    /// </summary>
    private async Task BackstopLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(BackstopInterval, ct); }
            catch (OperationCanceledException) { break; }

            try
            {
                var offene = await _offenIndex.ListeOffeneAsync(ct);
                foreach (var korr in offene)
                    _ = WeckeOffenenProzessAsync(korr, ct);
            }
            catch (Exception ex) { Console.WriteLine($"[Prozess-Backstop] Scan fehlgeschlagen: {ex.Message}"); }
        }
    }

    /// <summary>Eine bounded, fire-and-forget-Weckung eines offenen Prozess-Managers (Terminal/Fortschritt prüfen).</summary>
    private async Task WeckeOffenenProzessAsync(Guid korrelation, CancellationToken ct)
    {
        var identity = ClusterIdentity.Create(korrelation.ToString(), ProzessManagerActor.KindName);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(10));
        try { await _system.Cluster().RequestAsync<WakeAck>(identity, new ProzessWake(Guid.Empty, 0, null), cts.Token); }
        catch (OperationCanceledException) { /* nächster Backstop-Scan wiederholt */ }
        catch (Exception ex) { Console.WriteLine($"[Prozess-Backstop] Weckung {korrelation} fehlgeschlagen: {ex.Message}"); }
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
/// <c>IProzessDefinition</c> (Domain) → <c>GeneratedProzessRegeln.Alle</c>.
/// </summary>
public static class GeneratedProzesse
{
    public static IServiceCollection AddGeneratedProzesse(this IServiceCollection services)
    {
        services.AddSingleton<IReadOnlyDictionary<string, ProzessRegeln>>(Domain.Prozess.GeneratedProzessRegeln.Alle);
        services.AddSingleton<IClusterKindContributor, ProzessManagerKind>();
        services.AddHostedService<ProzessManagerStartupService>();
        return services;
    }
}
