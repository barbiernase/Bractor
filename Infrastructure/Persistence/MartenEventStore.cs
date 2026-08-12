using Abstractions;
using Infrastructure.Serialization;
using JasperFx.Events;
using Marten;
using Marten.Exceptions;
using Microsoft.Extensions.Logging;
using IEvent = Abstractions.IEvent;

namespace Infrastructure.Persistence;

/// <summary>
/// EventStore-Implementierung auf Basis von MartenDB (PostgreSQL).
/// 
/// Ersetzt den InMemoryEventStore. Keine Marten-Projektionen –
/// Projektionen laufen über das eigene PubSub-System.
/// 
/// Concurrency-Sicherheit:
/// - StartStream wirft wenn Stream bereits existiert (Schutz gegen doppelte Erstellung)
/// - Append mit Zielversion wirft bei Konflikt (Optimistic Concurrency)
/// - Beide werden als ConcurrencyException nach oben gegeben
/// </summary>
public class MartenEventStore : IEventStoreRepository
{
    private readonly IDocumentStore _store;
    private readonly IAggregateHandlerFactory _handlerFactory;
    private readonly ILogger<MartenEventStore> _logger;

    /// <summary>
    /// Straggler-Karenz für den Poll-Scan (Befund 3): die neuesten Events werden beim Vorrücken der
    /// High-Water-Mark eine Karenzzeit zurückgehalten. Postgres zieht die globale <c>seq_id</c> beim
    /// INSERT innerhalb der Transaktion; zwei nebenläufige Commits können in umgekehrter Seq-Reihenfolge
    /// sichtbar werden (höhere Seq committet zuerst). Rückte die HWM naiv auf <c>max(seq)</c> vor, würde
    /// eine noch uncommittete niedrigere Seq übersprungen und nie wieder gescannt. Die Karenz hält die
    /// HWM hinter Events zurück, die jünger als <see cref="_stragglerGrace"/> sind → ihre in-flight
    /// niedriger-Seq-Geschwister haben Zeit, sichtbar zu werden. Robust gegen permanente Lücken
    /// (rollback-verbrauchte Seqs): die Karenz ist zeitbasiert, bleibt also nie an einem Loch hängen.
    /// </summary>
    private readonly TimeSpan _stragglerGrace;

    public MartenEventStore(
        IDocumentStore store,
        IAggregateHandlerFactory handlerFactory,
        ILogger<MartenEventStore> logger,
        TimeSpan? stragglerGrace = null)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _handlerFactory = handlerFactory ?? throw new ArgumentNullException(nameof(handlerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _stragglerGrace = stragglerGrace ?? TimeSpan.FromSeconds(3);
    }

    /// <summary>
    /// Persistiert Events in PostgreSQL mit Optimistic Concurrency.
    /// 
    /// expectedVersion == 0: Neuer Stream (StartStream – wirft wenn bereits vorhanden)
    /// expectedVersion > 0:  Append mit Zielversion (expectedVersion + events.Count)
    /// expectedVersion < 0:  Ungültig (Guard gegen CommandEnvelope-Default von -1)
    /// </summary>
    public async Task AppendEventsAsync(
        Guid aggregateId,
        int expectedVersion,
        IReadOnlyList<IEvent> events,
        string? correlationId = null,
        string? causationId = null,
        string? aggregateType = null)
    {
        // Guard: expectedVersion muss >= 0 sein.
        // Der Actor prüft bereits gegen seinen State, aber falls
        // AppendEventsAsync ohne Actor-Kontext aufgerufen wird,
        // fangen wir ungültige Werte defensiv ab.
        if (expectedVersion < 0)
            throw new ArgumentException(
                $"ExpectedVersion must be >= 0, was {expectedVersion}.",
                nameof(expectedVersion));

        if (events.Count == 0)
            return;

        await using var session = _store.LightweightSession();

        // ★ Phase 1: Metadaten in DIESELBE Marten-Transaktion, damit sie nach dem
        //   Log-Read (ReadStreamAsync) verfügbar sind. Setzt die MetadataConfig-Flags
        //   voraus (CorrelationId/CausationId/Headers enabled — in der StoreOptions-Config).
        if (correlationId != null) session.CorrelationId = correlationId;
        if (causationId != null) session.CausationId = causationId;
        if (!string.IsNullOrEmpty(aggregateType)) session.SetHeader("aggregate_type", aggregateType);

        if (expectedVersion == 0)
        {
            // Neuer Stream: StartStream garantiert, dass der Stream
            // noch nicht existiert. Schärfer als Append mit Version 0.
            session.Events.StartStream(aggregateId, events.ToArray());
        }
        else
        {
            // Bestehender Stream: Append mit Versionsprüfung.
            // Marten erwartet die Ziel-Version NACH dem Append.
            // Bei Version 5 mit 2 Events → Zielversion 7.
            session.Events.Append(
                aggregateId,
                expectedVersion + events.Count,
                events.ToArray());
        }

        try
        {
            await session.SaveChangesAsync();

            _logger.LogDebug(
                "Appended {EventCount} events to stream {AggregateId}, " +
                "expected version {ExpectedVersion}",
                events.Count, aggregateId, expectedVersion);
        }
        catch (EventStreamUnexpectedMaxEventIdException ex)
        {
            _logger.LogWarning(
                "Concurrency conflict for aggregate {AggregateId}. " +
                "Expected version {ExpectedVersion}, but stream has diverged.",
                aggregateId, expectedVersion);

            throw new ConcurrencyException(
                $"Concurrency conflict for aggregate {aggregateId}. " +
                $"Expected version {expectedVersion}, but stream has diverged.",
                ex);
        }
        catch (ExistingStreamIdCollisionException ex)
        {
            // StartStream wirft dies wenn der Stream bereits existiert
            _logger.LogWarning(
                "Stream {AggregateId} already exists. Duplicate creation attempt.",
                aggregateId);

            throw new ConcurrencyException(
                $"Stream {aggregateId} already exists. " +
                $"Cannot create aggregate that already has events.",
                ex);
        }
    }

    /// <summary>
    /// Rekonstruiert den State eines Aggregats durch Event-Replay aus PostgreSQL.
    /// 
    /// Identisch zum bisherigen Replay-Muster: Events laden, Handler erstellen,
    /// jedes Event applizieren, Version hochzählen.
    /// 
    /// Unterschied zu InMemoryEventStore: Id wird VOR dem Replay gesetzt,
    /// nicht danach. Dokumentierte, bewusste Entscheidung.
    /// </summary>
    public async Task<TState?> LoadStateAsync<TState>(Guid aggregateId)
        where TState : class, IState, new()
    {
        await using var session = _store.LightweightSession();

        var eventStream = await session.Events.FetchStreamAsync(aggregateId);

        if (eventStream == null || eventStream.Count == 0)
            return null;

        // Id VOR Replay setzen – falls ein Applier die Id während des Replays braucht
        var state = new TState { Id = aggregateId };
        var handler = _handlerFactory.CreateHandler(state);

        foreach (var @event in eventStream)
        {
            // ★ Upcasting-Seam: Marten deserialisiert @event.Data via MapEventType getreu in den
            //   FRÜHEREN oder AKTUELLEN Versionstyp. GeneratedEventUpcasting.Aufwerten fährt frühere
            //   Versionen typisiert (reflection-frei) bis zur heutigen Gestalt; aktuelle Events fallen
            //   unverändert durch. Ein unbekannter Typ WIRFT (statt still übersprungen zu werden).
            //   Die Version zählt pro GESPEICHERTEM Event (nicht pro materialisiertem) — Split-invariant:
            //   auch wenn ein Stored-Event später zu mehreren wird, rückt die Stream-Position um 1.
            foreach (var domainEvent in GeneratedEventUpcasting.Aufwerten(@event.Data))
            {
                // Interne Inbox-/Prozess-Marken: in der Version mitzählen, aber nicht auf den State anwenden
                // (der Applier kennt sie nicht) — Framework-Naht (Exactly-once-Inbox).
                if (domainEvent is not IProzessIntern)
                    handler.ApplyEvent(domainEvent);
            }
            state.Version++;
        }

        _logger.LogDebug(
            "Loaded state for {AggregateId}: Version={Version}",
            aggregateId, state.Version);

        return state;
    }

    /// <summary>
    /// Liest die echten Events eines Streams ab <paramref name="fromVersion"/> und
    /// materialisiert sie als EventEnvelope (Spec 4.5 / 5.2). Das eine tragende
    /// Leseprimitiv des Pull-Ansatzes.
    ///
    /// ★ Phase 1: CorrelationId/CausationId/AggregateType werden jetzt aus den
    ///   Log-Metadaten rekonstruiert (beim Append gestempelt) — nicht mehr leer.
    /// </summary>
    public async Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(
        Guid streamId, int fromVersion, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();

        var raw = await session.Events
            .QueryAllRawEvents()
            .Where(e => e.StreamId == streamId && e.Version >= fromVersion)
            .OrderBy(e => e.Version)
            .ToListAsync(ct);

        var result = new List<EventEnvelope>(raw.Count);
        foreach (var e in raw)
        {
            var aggregateType = e.Headers != null
                && e.Headers.TryGetValue("aggregate_type", out var at)
                    ? at?.ToString() ?? string.Empty
                    : string.Empty;

            // ★ Upcasting-Seam: eine Stored-Position wird zu ein (1:1) oder — bei einem künftigen
            //   Split — mehreren materialisierten Events. Alle teilen dieselbe AggregateVersion, tragen
            //   aber aufsteigende SubIndex 0,1,… → der Dedup-/Exactly-once-Schlüssel append-artiger
            //   Projektionen (AggregateId, AggregateVersion, SubIndex) bleibt eindeutig. Für 1:1 ist
            //   SubIndex immer 0 und das Verhalten identisch zu vorher.
            var materialisiert = GeneratedEventUpcasting.Aufwerten(e.Data);
            for (int si = 0; si < materialisiert.Count; si++)
            {
                result.Add(new EventEnvelope
                {
                    EventId          = e.Id,
                    AggregateId      = streamId,
                    AggregateVersion = (int)e.Version,
                    SubIndex         = si,
                    CreatedAtUtc     = e.Timestamp,
                    CorrelationId    = e.CorrelationId ?? string.Empty,
                    CausationId      = e.CausationId  ?? string.Empty,
                    AggregateType    = aggregateType,
                    Payload          = materialisiert[si]
                });
            }
        }
        return result;
    }

    /// <summary>
    /// Globales Leseprimitiv für den Poll-Backstop (Spec 19.1): scannt die Events
    /// jenseits der globalen Sequenz und liefert die betroffenen Streams + neue
    /// High-Water-Mark. Marten führt die globale Sequenz (mt_events.seq_id) nativ.
    ///
    /// ★ Befund 3: Die HWM rückt NICHT naiv auf <c>max(seq)</c> vor, sondern nur bis zur höchsten
    ///   Sequenz, deren Event bereits älter als die Straggler-Karenz (<see cref="_stragglerGrace"/>)
    ///   ist. Zu junge Events werden zwar GEWECKT (Heilung, niedrige Latenz), schieben die HWM aber
    ///   noch nicht vor — so bekommen in-flight, niedriger-nummerierte Nachzügler Zeit, sichtbar zu
    ///   werden, bevor der Cursor sie überholt. Ohne Karenz (Grace = 0) ist das Verhalten identisch
    ///   zur alten <c>max(seq)</c>-Logik.
    /// </summary>
    public async Task<StreamChanges> ReadChangedStreamsAsync(
        long afterGlobalSequence, CancellationToken ct)
    {
        await using var session = _store.LightweightSession();

        var raw = await session.Events
            .QueryAllRawEvents()
            .Where(e => e.Sequence > afterGlobalSequence)
            .OrderBy(e => e.Sequence)
            .ToListAsync(ct);

        // Alle geänderten Streams wecken (Heilung) — unabhängig von der Karenz.
        var streams = raw.Select(e => e.StreamId).Distinct().ToList();

        if (raw.Count == 0)
            return new StreamChanges(streams, afterGlobalSequence);

        // Die HWM nur bis zur höchsten Sequenz vorrücken, deren Event die Karenzzeit überstanden hat.
        // Die DB-Uhr als Referenz (die Event-Timestamps sind DB-generiert) → kein App/DB-Clock-Skew.
        var cutoff = await DbNowAsync(session, ct) - _stragglerGrace;

        long highWater = afterGlobalSequence;
        foreach (var e in raw) // aufsteigend nach Sequenz; Timestamp läuft monoton mit
        {
            if (e.Timestamp <= cutoff && e.Sequence > highWater)
                highWater = e.Sequence;
        }

        return new StreamChanges(streams, highWater);
    }

    /// <summary>Die aktuelle DB-Uhr (timestamptz) über die Session-Verbindung — als Referenz für die
    /// Straggler-Karenz, konsistent mit den DB-generierten Event-Timestamps.</summary>
    private static async Task<DateTimeOffset> DbNowAsync(IQuerySession session, CancellationToken ct)
    {
        var conn = session.Connection;
        if (conn is null)
            return DateTimeOffset.UtcNow; // defensiv — sollte nach dem Query offen sein
        if (conn.State != System.Data.ConnectionState.Open)
            await conn.OpenAsync(ct);

        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "select now()";
        var value = await cmd.ExecuteScalarAsync(ct);
        return value switch
        {
            DateTimeOffset dto => dto,
            DateTime dt => new DateTimeOffset(DateTime.SpecifyKind(dt, DateTimeKind.Utc)),
            _ => DateTimeOffset.UtcNow
        };
    }
}