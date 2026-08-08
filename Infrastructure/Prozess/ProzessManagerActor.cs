using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Infrastructure.Pipeline;      // GeneratedPipelines.CommandAggregateTypes
using Infrastructure.Projections;   // WakeAck
using Proto;
using Proto.Cluster;

namespace Infrastructure.Prozess;

/// <summary>
/// Der GENERISCHE Prozess-Manager als virtueller Cluster-Actor (Spec §4): EIN Kind für ALLE Prozess-Typen,
/// eine Instanz pro Korrelation (Identität = Korrelations-Guid). Verschmilzt das alte Prozess-Aggregat +
/// den Treiber. Er hält im Speicher NICHTS außer der Korrelation — das Marking faltet der
/// <see cref="ProzessManager"/> bei jeder Weckung aus dem Log.
///
/// Er feuert die Ziel-Commands FIRE-AND-FORGET über <see cref="DetachedProzessSend"/> (der strukturelle
/// (A)-Hang-Fix, in-memory bewiesen): der Ziel-<c>RequestAsync</c> läuft detached, der Turn kehrt sofort
/// zurück. Eine negative Quittung kommt out-of-turn als <see cref="MeldeFehlschlag"/> über die eigene
/// Mailbox zurück (Single-Writer bleibt gewahrt). Die Korrelation reist als
/// <c>CommandEnvelope.CorrelationId</c> mit → sie landet in den Ziel-Event-Metadaten, woraus der
/// <see cref="KorrelationsRouter"/> den Manager neu weckt. Ziel-Aggregate (Konto) bleiben dadurch rein.
///
/// (A)-Fix: <c>system.Cluster()</c> wird in der SPAWN-Factory aufgelöst (siehe Wiring), NICHT bei der
/// Kind-Registrierung; der Ziel-Send ist bounded (kein <c>None</c>-Infinit-Retry).
/// </summary>
public sealed class ProzessManagerActor : IActor
{
    public const string KindName = "prozess-manager";

    private readonly IEventStoreRepository _eventStore;
    private readonly IReadOnlyDictionary<string, ProzessRegeln> _registry;
    private readonly Cluster _cluster;
    private readonly IProzessOffenIndex? _offenIndex;
    private readonly IDeadLetterSink? _deadLetters;
    private Guid _korrelation;
    private ProzessManager? _manager;

    public ProzessManagerActor(
        IEventStoreRepository eventStore,
        IReadOnlyDictionary<string, ProzessRegeln> registry,
        Cluster cluster,
        IProzessOffenIndex? offenIndex = null,
        IDeadLetterSink? deadLetters = null)
    {
        _eventStore = eventStore;
        _registry = registry;
        _cluster = cluster;
        _offenIndex = offenIndex;
        _deadLetters = deadLetters;
    }

    public async Task ReceiveAsync(IContext context)
    {
        switch (context.Message)
        {
            case Started:
                var identity = context.ClusterIdentity()?.Identity;
                if (identity != null && Guid.TryParse(identity, out var korr))
                    _korrelation = korr;
                _manager = new ProzessManager(_eventStore, _registry, ErzeugeDispatch(context), _offenIndex, _deadLetters);
                break;

            case ProzessWake w:
                if (_manager is not null)
                {
                    if (w.ProzessNameFürStart is string name)
                        await _manager.StarteAsync(_korrelation, name, w.QuellStream, w.QuellVersion, context.CancellationToken);
                    else
                        await _manager.WakeAsync(_korrelation, context.CancellationToken);
                }
                context.Respond(new WakeAck());
                break;

            case MeldeFehlschlag f:
                if (_manager is not null)
                {
                    await _manager.NotiereFehlschlagAsync(_korrelation, f.Vorgang, f.Grund, context.CancellationToken);
                    await _manager.WakeAsync(_korrelation, context.CancellationToken);
                }
                context.Respond(new WakeAck());
                break;
        }
    }

    /// <summary>Der Engine-Dispatch: detached Ziel-Send + Fehlschlag-Rückmeldung + Selbst-Weckung nach Erfolg.</summary>
    private Func<Guid, ICommand, Guid, CancellationToken, Task> ErzeugeDispatch(IContext context)
        => DetachedProzessSend.Wrap(SendeAnZiel, MeldeFehlschlagAnManager, WeckeSelbst);

    /// <summary>Nach einem erfolgreichen Send: DIESE Manager-Instanz neu wecken (Terminal/Fortschritt prüfen).</summary>
    private async Task WeckeSelbst(Guid korrelation, CancellationToken ct)
    {
        var identity = ClusterIdentity.Create(korrelation.ToString(), KindName);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await _cluster.RequestAsync<WakeAck>(identity, new ProzessWake(Guid.Empty, 0, null), cts.Token); }
        catch (OperationCanceledException) { /* Poll-Backstop heilt */ }
    }

    /// <summary>Der bounded Ziel-Send; Vorgang → deterministische CommandId (Framework-Inbox), Korrelation → Metadatum.</summary>
    private async Task<CommandResult?> SendeAnZiel(ICommand cmd, Guid vorgang, CancellationToken ct)
    {
        if (!GeneratedPipelines.CommandAggregateTypes.TryGetValue(cmd.GetType(), out var aggregateType))
            return null;

        var envelope = new CommandEnvelope
        {
            CommandId = vorgang,                            // deterministisch → Framework-Inbox dedupliziert (Exactly-once)
            AggregateId = cmd.AggregateId,
            AggregateType = aggregateType,
            ExpectedVersion = CommandEnvelope.AnyVersion,   // kein OCC — Idempotenz sichert die CommandId
            CorrelationId = _korrelation.ToString(),        // → Ziel-Event-Metadaten (Router weckt daraus)
            Payload = cmd
        };
        var identity = ClusterIdentity.Create(cmd.AggregateId.ToString(), aggregateType);

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));   // bounded — kein None-Infinit-Retry (die Lektion aus (A))
        try { return await _cluster.RequestAsync<CommandResult>(identity, envelope, cts.Token); }
        catch (OperationCanceledException) { return null; }   // Timeout → nächste Weckung/Poll heilt
    }

    /// <summary>Rückmeldung einer negativen Quittung an DIESE Manager-Instanz (über die Mailbox → Single-Writer).</summary>
    private async Task MeldeFehlschlagAnManager(Guid korrelation, Guid vorgang, string grund, CancellationToken ct)
    {
        var identity = ClusterIdentity.Create(korrelation.ToString(), KindName);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await _cluster.RequestAsync<WakeAck>(identity, new MeldeFehlschlag(vorgang, grund), cts.Token); }
        catch (OperationCanceledException) { /* Poll-Backstop heilt */ }
    }
}
