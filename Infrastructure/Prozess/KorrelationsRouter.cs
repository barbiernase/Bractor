using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Abstractions;
using Infrastructure.Projections;   // SignalReceiver.SignalTypesFor

namespace Infrastructure.Prozess;

/// <summary>
/// Die EINE echte neue Infrastruktur (docs/04 §4.5): der Korrelations-Router. Wo der alte
/// <c>SignalReceiver</c> <c>(kind, streamId)</c> weckt, weckt dieser <c>(prozess-manager, Korrelation)</c>.
/// Aus einem Signal <c>(StreamId, Version)</c> liest er das Event und leitet die Korrelation ab:
///   - ist das Event ein registrierter AUSLÖSER-Typ → Start: Korrelation = <see cref="ProzessId.Für"/>
///     (deterministisch aus Prozess-Name + Stream + Version).
///   - sonst (teilnehmendes Ziel-Event) → die Korrelation reist in den Event-Metadaten
///     (<c>EventEnvelope.CorrelationId</c>, vom Manager beim Feuern gestempelt) → daraus wecken.
///
/// So bleibt das Ziel-Aggregat rein (kein Korrelations-Feld nötig) und der Manager instance-korrekt
/// getroffen. Eine Weckung eines gar-nicht-gestarteten Managers (fremdes Konto-Event) verpufft (der
/// Manager faltet „nicht gestartet" und kehrt zurück).
/// </summary>
public sealed class KorrelationsRouter
{
    private readonly IEventStoreRepository _store;
    private readonly IReadOnlyDictionary<Type, string> _auslöserZuProzess;
    private readonly Func<Guid, Guid, int, string?, CancellationToken, Task> _wecke;
    private readonly Dictionary<Type, string> _startJeSignal = new();

    /// <param name="auslöserZuProzess">Auslöser-Event-Typ → Prozess-Name (der Start bindet den Typ).</param>
    /// <param name="wecke">Weckt den Manager: (Korrelation, QuellStream, QuellVersion, ProzessNameFürStart|null).</param>
    /// <param name="eventZuSignal">Event-Typ → sein <c>StateChangeVia</c>-Signal-Typ (generierte Tabelle). Damit startet ein Auslöser-Signal
    /// seinen Vorgang ohne Read; ohne Tabelle liest der Router jedes Event.</param>
    public KorrelationsRouter(
        IEventStoreRepository store,
        IReadOnlyDictionary<Type, string> auslöserZuProzess,
        Func<Guid, Guid, int, string?, CancellationToken, Task> wecke,
        IReadOnlyDictionary<Type, Type>? eventZuSignal = null)
    {
        _store = store;
        _auslöserZuProzess = auslöserZuProzess;
        _wecke = wecke;
        if (eventZuSignal is not null)
            foreach (var (typ, prozess) in auslöserZuProzess)
                if (eventZuSignal.TryGetValue(typ, out var signalTyp)) _startJeSignal[signalTyp] = prozess;
    }

    /// <summary>
    /// Signal-Pfad (PF-12): ein Auslöser-Signal startet seinen Vorgang ohne Read — der Signal-Typ nennt das Event, die Korrelation folgt aus
    /// Stream und Version. Jedes andere Signal liest genau sein Event (die Korrelation steht in den Metadaten), nicht den Rest des Streams.
    /// </summary>
    public async Task OnSignalAsync(IStateChangeSignal signal, CancellationToken ct = default)
    {
        if (_startJeSignal.TryGetValue(signal.GetType(), out var prozess))
        {
            await _wecke(ProzessId.Für(prozess, signal.StreamId, signal.Version), signal.StreamId, signal.Version, prozess, ct);
            return;
        }
        var env = await _store.ReadEventAsync(signal.StreamId, signal.Version, ct);
        if (env is null) return;
        await RouteAsync(signal.StreamId, env.AggregateVersion, env.Payload, env.CorrelationId, ct);
    }

    /// <summary>
    /// Der Routing-Kern (Signal- UND Poll-Pfad): aus einem Event die Korrelation ableiten und den Manager
    /// wecken. Auslöser-Typ → Start (deterministische Korrelation); teilnehmendes Event → Korrelation aus
    /// den Metadaten (<paramref name="correlationId"/>, vom Manager beim Feuern gestempelt).
    /// </summary>
    public async Task RouteAsync(Guid streamId, int version, IEvent payload, string correlationId, CancellationToken ct = default)
    {
        var typ = payload.GetType();
        if (_auslöserZuProzess.TryGetValue(typ, out var prozessName))
        {
            var korr = ProzessId.Für(prozessName, streamId, version);
            await _wecke(korr, streamId, version, prozessName, ct);
        }
        else if (Guid.TryParse(correlationId, out var korr))
        {
            await _wecke(korr, streamId, version, null, ct);
        }
    }

    /// <summary>Die Signal-Typen, die der Router abonnieren muss (aus den teilnehmenden Event-Typen).</summary>
    public static IReadOnlyList<Type> SignalTypesFor(IEnumerable<Type> eventTypes)
        => SignalReceiver.SignalTypesFor(eventTypes);
}
