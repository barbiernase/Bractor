using Abstractions;
using Infrastructure.Serialization;
using JasperFx.Events;
using Marten;
using Marten.Exceptions;
using Microsoft.Extensions.Logging;
using Infrastructure.Komposition;
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
    /// Der Horizont der Vollständigkeit (Befund 3, G-9): Postgres zieht die globale <c>seq_id</c> beim INSERT innerhalb der Transaktion; zwei
    /// nebenläufige Commits werden in umgekehrter Reihenfolge sichtbar, wenn die höhere Sequenz zuerst committet. Rückte eine Marke naiv auf
    /// <c>max(seq)</c>, übersprang sie die noch unsichtbare niedrigere für immer. Bis wohin alles endgültig ist, sagt nicht die Uhr, sondern
    /// Postgres über die Transaktionsnummern (<see cref="Horizont"/>); eine lange offene Transaktion hält die Marken an, statt überholt zu werden.
    /// </summary>
    private readonly Horizont _horizont;
    private long _letzteHorizontWarnung = long.MinValue;

    public MartenEventStore(
        IDocumentStore store,
        IAggregateHandlerFactory handlerFactory,
        ILogger<MartenEventStore> logger)
    {
        _store = store ?? throw new ArgumentNullException(nameof(store));
        _handlerFactory = handlerFactory ?? throw new ArgumentNullException(nameof(handlerFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _horizont = Horizont.Fuer(store);
    }

    /// <summary>
    /// Persistiert Events in PostgreSQL mit Optimistic Concurrency.
    /// 
    /// expectedVersion == 0: Neuer Stream (StartStream – wirft wenn bereits vorhanden)
    /// expectedVersion > 0:  Append mit Zielversion (expectedVersion + events.Count)
    /// expectedVersion < 0:  Ungültig (Guard gegen CommandEnvelope-Default von -1)
    /// </summary>
    public Task AppendEventsAsync(
        Guid aggregateId,
        int expectedVersion,
        IReadOnlyList<IEvent> events,
        string? correlationId = null,
        string? causationId = null,
        string? aggregateType = null,
        string? akteur = null,
        IReadOnlyList<Beilage>? beilagen = null)
        => events.Count == 0 && expectedVersion >= 0
            ? Task.CompletedTask
            : AppendTeileAsync(aggregateId, expectedVersion,
                [new AppendTeil(events, correlationId, causationId, akteur, Spur.Aktuell, beilagen)], aggregateType);

    /// <summary>
    /// Ein Append an einen Stream mit einem oder mehreren Teilen (dem Bündel eines Aggregat-Actors) — eine Transaktion, Metadaten je Event aus
    /// seinem Teil (<see cref="MartenEventBatchWriter.Stage"/>, derselbe Weg wie der Group-Commit), Beilagen aller Teile in derselben Transaktion.
    /// Version 0 → <c>StartStream</c> (wirft, wenn es den Stream gibt), sonst Append mit Ziel-Version (erwartet + Anzahl Events).
    /// </summary>
    public async Task AppendTeileAsync(Guid aggregateId, int expectedVersion, IReadOnlyList<AppendTeil> teile, string? aggregateType)
    {
        // Guard: der Actor prüft bereits gegen seinen State; ohne Actor-Kontext (CommandEnvelope-Default −1) defensiv abfangen.
        if (expectedVersion < 0)
            throw new ArgumentException($"ExpectedVersion must be >= 0, was {expectedVersion}.", nameof(expectedVersion));
        if (teile.All(t => t.Events.Count == 0))
            return;

        await EventZeit.SicherAsync(_store, default);   // den Zeitstempel setzt die Datenbank-Uhr (G-10)
        await using var session = _store.LightweightSession();
        var item = new BatchAppend(aggregateId, expectedVersion, teile, aggregateType);
        MartenEventBatchWriter.Stage(session, item);

        // Die Beilagen in DERSELBEN Transaktion (docs/03 §3.3): kennt die Inbox eine Quittung schon, scheitert der Commit am Schlüssel und
        //   nichts ist geschrieben.
        var schreiber = new BeilagenSchreiber();
        foreach (var teil in teile) schreiber.Lege(session, aggregateId, teil.Beilagen);
        schreiber.Abschliessen(session, _store);

        try
        {
            await session.SaveChangesAsync();
            Infrastructure.Einblick.SpurSignal.Melde();

            _logger.LogDebug("Appended {EventCount} events to stream {AggregateId}, expected version {ExpectedVersion}",
                item.EventAnzahl, aggregateId, expectedVersion);
        }
        catch (EventStreamUnexpectedMaxEventIdException ex)
        {
            _logger.LogWarning("Concurrency conflict for aggregate {AggregateId}. Expected version {ExpectedVersion}, but stream has diverged.",
                aggregateId, expectedVersion);
            throw new ConcurrencyException(
                $"Concurrency conflict for aggregate {aggregateId}. Expected version {expectedVersion}, but stream has diverged.", ex);
        }
        catch (ExistingStreamIdCollisionException ex)
        {
            // StartStream wirft dies, wenn der Stream bereits existiert
            _logger.LogWarning("Stream {AggregateId} already exists. Duplicate creation attempt.", aggregateId);
            throw new ConcurrencyException(
                $"Stream {aggregateId} already exists. Cannot create aggregate that already has events.", ex);
        }
        catch (Exception ex) when (teile.Select(t => BeilagenSchreiber.QuittungIn(t.Beilagen)).FirstOrDefault(q => q is not null) is { } commandId
                                   && InboxTabelle.IstDuplikat(ex))
        {
            // Bei mehreren Quittungen nennt die Ausnahme eine von ihnen — der Aufrufer eines Bündels entscheidet ohnehin einzeln neu.
            throw new KommandoSchonEntschiedenException(commandId, ex);
        }
    }

    public Task<KommandoAusgang?> AusgangAsync(Guid commandId, CancellationToken ct) => InboxTabelle.AusgangAsync(_store, commandId, ct);

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
            foreach (var domainEvent in Anwendung.Upcasting.Aufwerten(@event.Data))
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
    /// materialisiert sie als EventEnvelope (docs/04 §4.1). Das eine tragende
    /// Leseprimitiv des Pull-Ansatzes.
    ///
    /// ★ Phase 1: CorrelationId/CausationId/AggregateType werden jetzt aus den
    ///   Log-Metadaten rekonstruiert (beim Append gestempelt) — nicht mehr leer.
    /// </summary>
    public Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(Guid streamId, int fromVersion, CancellationToken ct)
        => LeseStreamAsync("stream_id = $1 and version >= $2", ct, streamId, (long)fromVersion);

    public Task<IReadOnlyList<EventEnvelope>> ReadKorreliertAsync(Guid streamId, int fromVersion, string korrelation, CancellationToken ct)
        => LeseStreamAsync("stream_id = $1 and version >= $2 and correlation_id = $3", ct, streamId, (long)fromVersion, korrelation);

    public async Task<EventEnvelope?> ReadEventAsync(Guid streamId, int version, CancellationToken ct)
        => (await LeseStreamAsync("stream_id = $1 and version = $2", ct, streamId, (long)version)).FirstOrDefault();

    /// <summary>
    /// Die Event-Reads eines Streams als festes SQL statt über Martens LINQ (PF-21): Marten übersetzte <c>QueryAllRawEvents().Where(…)</c> bei
    /// jedem Aufruf neu und kompilierte dabei die eingefangenen Werte — unter Last über die Hälfte der arbeitenden Host-Threads, und das Erzeugen
    /// dynamischer Methoden läuft in der Laufzeit über eine Sperre. Das SQL ist dasselbe, das Marten erzeugte (samt <c>is_archived = false</c>);
    /// Tabellen, Format und Serializer bleiben Martens. Den Typ eines Events nennt die Spalte <c>type</c> (Speicher-Diskriminator), den .NET-Typ
    /// dazu die generierte Registrierung, mit der Marten die Typen auch schreibt (<see cref="EventTypen"/>) — keine Reflection.
    /// </summary>
    private async Task<IReadOnlyList<EventEnvelope>> LeseStreamAsync(string bedingung, CancellationToken ct, params object[] werte)
        => (await LeseEventsAsync(bedingung, "order by version", ct, werte)).Events.Select(e => e.Event).ToList();

    /// <summary>
    /// Liest Events aus <c>mt_events</c> mit festem SQL (<paramref name="bedingung"/>, <paramref name="ordnung"/> samt Grenze, Werte als $1, $2 …);
    /// immer ohne archivierte Streams, wie Martens LINQ. Kopfzeilen kommen als Text-Spalten (<c>headers->>'…'</c>) statt als Wörterbuch (PF-22).
    /// Zeilen = gelesene Zeilen samt Tombstones (für „Seite voll“).
    /// </summary>
    private async Task<(List<PartitionsEvent> Events, int Zeilen)> LeseEventsAsync(string bedingung, string ordnung, CancellationToken ct,
        params object[] werte)
    {
        await EventTabellenAsync(_store, ct);
        var schema = _store.Options.Events.DatabaseSchemaName;
        await using var conn = _store.Storage.Database.CreateConnection();
        await conn.OpenAsync(ct);
        await using var cmd = new Npgsql.NpgsqlCommand(EventStoreAbfragen.Events(schema, MetadatenSpalten(_store), bedingung, ordnung), conn);
        foreach (var w in werte) cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = w });

        var serializer = _store.Options.Serializer();
        var ergebnis = new List<PartitionsEvent>();
        var zeilen = 0;
        string? T(System.Data.Common.DbDataReader r, int i) => r.IsDBNull(i) ? null : r.GetString(i);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        while (await r.ReadAsync(ct))
        {
            zeilen++;
            var diskriminator = r.GetString(1);
            if (diskriminator == TombstoneDiskriminator) continue;   // G-7: Platzhalter eines gescheiterten Appends, kein Fakt
            if (!EventTypen.TryGetValue(diskriminator, out var typ))
                throw new InvalidOperationException($"Event-Typ „{diskriminator}“ ist nicht registriert (GeneratedEventUpcasting.Registrierungen).");
            var daten = serializer.FromJson(typ, r, 0);
            var sequenz = r.GetInt64(14);
            foreach (var env in Materialisiere(daten, r.GetGuid(2), r.GetGuid(3), r.GetInt64(4), r.GetFieldValue<DateTimeOffset>(5), T(r, 6), T(r, 7),
                         T(r, 8), T(r, 9), T(r, 10), T(r, 11), T(r, 12), T(r, 13)))
                ergebnis.Add(new PartitionsEvent(sequenz, env));
        }
        return (ergebnis, zeilen);
    }

    /// <summary>
    /// Korrelation, Kausalität und Kopfzeilen sind Spalten, die Marten nur anlegt, wenn sie in der Konfiguration eingeschaltet sind (der Host
    /// schaltet alle ein, manche Test-Stores nicht) — eine fehlende liest das SQL als <c>null</c>, wie Martens LINQ sie dann auslässt. Von den
    /// Kopfzeilen nur die sechs, die ein <see cref="EventEnvelope"/> trägt, als Text (Aggregat-Typ, Akteur, Spur).
    /// </summary>
    public static string MetadatenSpalten(IDocumentStore store)
    {
        var m = ((StoreOptions)store.Options).Events.MetadataConfig;
        var kopf = new[] { "aggregate_type", ImAuftrag.Header, Spur.UrsacheHeader, Spur.WirkerHeader, Spur.UeberHeader, Spur.StandHeader }
            .Select(k => m.HeadersEnabled ? $"headers->>'{k}'" : "null::text");
        return (m.CorrelationIdEnabled ? "correlation_id" : "null::varchar") + ", "
             + (m.CausationIdEnabled ? "causation_id" : "null::varchar") + ", " + string.Join(", ", kopf);
    }

    /// <summary>Marten speichert die Platzhalter gescheiterter Appends unter diesem Diskriminator (G-7).</summary>
    private const string TombstoneDiskriminator = "tombstone";

    /// <summary>Speicher-Diskriminator → .NET-Typ, aus derselben generierten Tabelle, mit der <see cref="MartenEventTypeRegistration"/> die Typen
    /// bei Marten anmeldet (aktuelle und frühere Gestalten).</summary>
    private static readonly Lazy<IReadOnlyDictionary<string, Type>> EventTypenLazy = new(() =>
        Anwendung.Upcasting.Registrierungen.ToDictionary(r => r.Diskriminator, r => r.Typ));
    private static IReadOnlyDictionary<string, Type> EventTypen => EventTypenLazy.Value;

    /// <summary>
    /// Ein gespeichertes Event als EventEnvelope(s). Upcasting-Seam: eine Stored-Position wird zu einem (1:1) oder — bei einem künftigen
    /// Split — mehreren materialisierten Events. Alle teilen dieselbe AggregateVersion, tragen aber aufsteigende SubIndex 0,1,… → der
    /// Dedup-/Exactly-once-Schlüssel append-artiger Projektionen (AggregateId, AggregateVersion, SubIndex) bleibt eindeutig.
    /// </summary>
    private static IEnumerable<EventEnvelope> Materialisiere(object daten, Guid id, Guid streamId, long version, DateTimeOffset zeit,
        string? correlationId, string? causationId, string? aggregateType, string? akteur, string? ursache, string? wirker, string? ueber,
        string? stand)
    {
        var materialisiert = Anwendung.Upcasting.Aufwerten(daten);
        for (int si = 0; si < materialisiert.Count; si++)
        {
            yield return new EventEnvelope
            {
                EventId          = id,
                AggregateId      = streamId,
                AggregateVersion = (int)version,
                SubIndex         = si,
                CreatedAtUtc     = zeit,
                CorrelationId    = correlationId ?? string.Empty,
                CausationId      = causationId  ?? string.Empty,
                AggregateType    = aggregateType ?? string.Empty,
                UserId           = akteur ?? ImAuftrag.Ohne,
                Ursache          = ursache,
                Wirker           = wirker,
                Ueber            = ueber,
                Stand            = stand,
                Payload          = materialisiert[si]
            };
        }
    }

    /// <summary>
    /// Rohes SQL löst Martens AutoCreate nicht aus: auf einer frischen Datenbank legt der erste rohe Leser die Event-Tabellen an; mit
    /// <c>AutoCreate.None</c> (Multi-Node-Member, das Schema legt der Migrator an) folgenlos. Früher legte sie zufällig der Prozess-Poll an
    /// (er las über LINQ) — rohe Leser vor ihm scheiterten bis dahin. Einmal je Store gemerkt, damit die Seiten des Batch-Konsums nicht jedes
    /// Mal Martens Prüfung durchlaufen.
    /// </summary>
    internal static async ValueTask EventTabellenAsync(IDocumentStore store, CancellationToken ct)
    {
        if (EventTabellenDa.TryGetValue(store, out _)) return;
        await store.Storage.Database.EnsureStorageExistsAsync(typeof(JasperFx.Events.IEvent), ct);
        EventTabellenDa.TryAdd(store, new object());
    }

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<IDocumentStore, object> EventTabellenDa = new();

    /// <summary>
    /// Leseprimitiv des Batch-Konsums (docs/04 §4.1): zuerst die gesetzte Sequenz (bis dorthin ist jede endgültig, <see cref="GesetztAsync"/>),
    /// danach die Seite der Partition: Partition per <see cref="PartitionsAusdruck"/>, Typfilter über die Speicher-Diskriminatoren, ohne schon
    /// erledigte Sequenzen, geordnet nach Sequenz. In dieser Reihenfolge, damit jede Sequenz bis zur gesetzten beim Lesen der Seite sichtbar ist.
    /// </summary>
    public async Task<PartitionsSeite> ReadPartitionAsync(
        long nach, int partition, int partitionen, IReadOnlyCollection<string>? typen, IReadOnlyCollection<long> ausser, int limit,
        CancellationToken ct)
    {
        await EventTabellenAsync(_store, ct);
        var (gesetzt, dahinter) = await GesetztAsync(nach, ct);
        if (!dahinter) return new PartitionsSeite(Array.Empty<PartitionsEvent>(), gesetzt, false);

        // Festes SQL statt Martens LINQ (PF-22) — dieselbe Bedingung, die Marten erzeugte: Partition, nicht schon erledigt, jenseits der Position,
        //   optional nur die abonnierten Typen, geordnet nach Sequenz, höchstens limit Zeilen.
        var (events, zeilen) = typen is null
            ? await LeseEventsAsync(EventStoreAbfragen.PartitionsBedingung(false), EventStoreAbfragen.PartitionsOrdnung, ct,
                nach, partitionen, partition, ausser.ToArray(), limit)
            : await LeseEventsAsync(EventStoreAbfragen.PartitionsBedingung(true), EventStoreAbfragen.PartitionsOrdnung, ct,
                nach, partitionen, partition, ausser.ToArray(), limit, typen.ToArray());
        return new PartitionsSeite(events, gesetzt, zeilen >= limit);
    }

    /// <summary>
    /// Die gesetzte Sequenz jenseits von <paramref name="nach"/> — bis dorthin ist jede Sequenz endgültig (<see cref="Horizont"/>) — und ob
    /// dahinter überhaupt ein Event steht, in EINER Runde (PF-14): steht nichts dahinter, sparen die Polls ihre Seite. Ist keine Probe offen
    /// oder eine fällig (solange eine offen ist, höchstens eine je <see cref="Horizont.ProbenAbstand"/>), ist die Runde eine kurze eigene Transaktion: Zählerstand, dann ihre
    /// Transaktionsnummer und die älteste noch laufende — sie schreibt keine Daten, nur ihren Commit, ohne auf die Platte zu warten. Sonst prüft
    /// die Runde nur die älteste laufende. Bleibt die höchste offene Probe stehen und war die letzte Freigabe lange her (ein Leser ohne Mitleser,
    /// etwa ein Poll alle 30 s), wartet die Abfrage kurz auf sie — sonst stünde seine Marke ein Poll-Intervall zurück. Nur im Anhänge-Modus
    /// <c>Quick</c> gültig (dort hat jede Transaktion ihre Nummer, bevor sie eine Sequenz zieht; <c>Rich</c> zieht die Sequenzen vor der
    /// Transaktion).
    /// </summary>
    private async Task<(long Gesetzt, bool Dahinter)> GesetztAsync(long nach, CancellationToken ct)
    {
        if (_store.Options.Events.AppendMode != EventAppendMode.Quick)
            throw new InvalidOperationException(
                "Der Horizont der Vollständigkeit setzt Martens Anhänge-Modus Quick voraus (docs/03 §3.3): im Modus Rich zieht Marten die "
                + "Sequenzen vor der Transaktion, eine Marke könnte ein noch unsichtbares Event überholen.");
        var schema = _store.Options.Events.DatabaseSchemaName;
        await using var conn = _store.Storage.Database.CreateConnection();
        await conn.OpenAsync(ct);

        long frei;
        bool dahinter;
        if (_horizont.HoechsteOffene is null || _horizont.ProbeFaellig())
        {
            await using var probe = new Npgsql.NpgsqlBatch(conn);
            probe.BatchCommands.Add(new Npgsql.NpgsqlBatchCommand("begin"));
            probe.BatchCommands.Add(new Npgsql.NpgsqlBatchCommand("set local synchronous_commit to off"));
            // Erst der Zählerstand, dann — in einer späteren Anweisung, also einer späteren Momentaufnahme — die eigene Nummer.
            probe.BatchCommands.Add(new Npgsql.NpgsqlBatchCommand(
                $"select case when is_called then last_value else 0 end from {schema}.mt_events_sequence"));
            var zweite = new Npgsql.NpgsqlBatchCommand(
                "select pg_current_xact_id()::text, pg_snapshot_xmin(pg_current_snapshot())::text, "
                + EventStoreAbfragen.Dahinter(schema));
            zweite.Parameters.Add(new Npgsql.NpgsqlParameter { Value = nach });
            probe.BatchCommands.Add(zweite);
            probe.BatchCommands.Add(new Npgsql.NpgsqlBatchCommand("commit"));

            await using var r = await probe.ExecuteReaderAsync(ct);
            await r.ReadAsync(ct);
            var stand = r.GetInt64(0);
            await r.NextResultAsync(ct);
            await r.ReadAsync(ct);
            var nummer = ulong.Parse(r.GetString(0));
            var aelteste = ulong.Parse(r.GetString(1));
            dahinter = r.GetBoolean(2);
            frei = _horizont.Probe(stand, nummer, aelteste);
        }
        else
        {
            await using var pruefung = new Npgsql.NpgsqlCommand(
                "select pg_snapshot_xmin(pg_current_snapshot())::text, "
                + EventStoreAbfragen.Dahinter(schema), conn);
            pruefung.Parameters.Add(new Npgsql.NpgsqlParameter { Value = nach });
            await using var r = await pruefung.ExecuteReaderAsync(ct);
            await r.ReadAsync(ct);
            var aelteste = ulong.Parse(r.GetString(0));
            dahinter = r.GetBoolean(1);
            frei = _horizont.Pruefe(aelteste);
        }

        if (_horizont.HoechsteOffene is { } offen && frei < offen && _horizont.SeitFreigabe > HorizontFrisch)
            frei = await AufProbeWartenAsync(conn, offen, ct);
        await WarneBeiHaengendemHorizontAsync(conn, ct);

        if (!dahinter) return (nach, false);
        return (Math.Max(nach, frei), true);
    }

    /// <summary>Gilt die letzte Freigabe als frisch, wartet niemand auf seine Probe (Takt, nie Richtigkeit).</summary>
    private static readonly TimeSpan HorizontFrisch = TimeSpan.FromMilliseconds(100);

    /// <summary>Höchstens so lange wartet eine Abfrage auf ihre eigene Probe; danach gilt der bisherige Horizont.</summary>
    private static readonly TimeSpan HorizontWarten = TimeSpan.FromMilliseconds(250);

    private async Task<long> AufProbeWartenAsync(Npgsql.NpgsqlConnection conn, long stand, CancellationToken ct)
    {
        var start = System.Diagnostics.Stopwatch.GetTimestamp();
        var pause = TimeSpan.FromMilliseconds(2);
        var frei = _horizont.Frei;
        while (_horizont.Wartet(stand) && System.Diagnostics.Stopwatch.GetElapsedTime(start) < HorizontWarten)
        {
            await Task.Delay(pause, ct);
            if (pause < TimeSpan.FromMilliseconds(32)) pause *= 2;
            await using var cmd = new Npgsql.NpgsqlCommand("select pg_snapshot_xmin(pg_current_snapshot())::text", conn);
            frei = _horizont.Pruefe(ulong.Parse((string)(await cmd.ExecuteScalarAsync(ct))!));
        }
        return frei;
    }

    /// <summary>
    /// Wartet eine Probe länger als 30 s, hält eine lange offene Transaktion (irgendwo in diesem Postgres) alle Marken an — Zustellung und
    /// Latenz laufen weiter, nur die Positionen nicht. Einmal je Minute eine Warnung mit der ältesten schreibenden Transaktion.
    /// </summary>
    private async Task WarneBeiHaengendemHorizontAsync(Npgsql.NpgsqlConnection conn, CancellationToken ct)
    {
        if (_horizont.AeltesteOffen is not { } alter || alter < TimeSpan.FromSeconds(30)) return;
        if (_letzteHorizontWarnung != long.MinValue
            && System.Diagnostics.Stopwatch.GetElapsedTime(_letzteHorizontWarnung) < TimeSpan.FromMinutes(1)) return;
        _letzteHorizontWarnung = System.Diagnostics.Stopwatch.GetTimestamp();
        string wer = "unbekannt";
        try
        {
            await using var cmd = new Npgsql.NpgsqlCommand(
                "select format('pid %s, Datenbank %s, seit %s, %s', pid, datname, xact_start, left(query, 120)) from pg_stat_activity "
                + "where backend_xid is not null order by xact_start limit 1", conn);
            wer = await cmd.ExecuteScalarAsync(ct) as string ?? wer;
        }
        catch (Exception) { /* nur Diagnose */ }
        _logger.LogWarning("Horizont der Vollständigkeit hängt seit {Sekunden:0} s: eine lange offene Transaktion hält die Marken an ({Wer})",
            alter.TotalSeconds, wer);
    }

    public async Task<IReadOnlyCollection<Guid>> StreamsVorhandenAsync(IReadOnlyCollection<Guid> streams, CancellationToken ct)
    {
        if (streams.Count == 0) return Array.Empty<Guid>();
        await EventTabellenAsync(_store, ct);
        await using var session = _store.QuerySession();
        return await session.QueryAsync<Guid>(
            $"select id from {_store.Options.Events.DatabaseSchemaName}.mt_streams where id = any(?)", ct, streams.ToArray());
    }

    /// <summary>
    /// Globales Leseprimitiv für den Poll der Emittenten (docs/04 §4.1, PF-11/PF-12): eine Seite der Streams mit einem Event der abonnierten
    /// Typen jenseits der Marke — reines SQL über Sequenz, Stream und Typ, ohne Daten. Früher lud es jedes Event jenseits der Marke samt
    /// Daten in einer Abfrage, und der Poller weckte jeden geänderten Stream, auch ohne abonniertes Event (der Adapter las ihn ab 0 und legte
    /// einen Cursor an — je Stream des Logs einer).
    ///
    /// ★ Befund 3: die Marke rückt nur bis zur gesetzten Sequenz (<see cref="GesetztAsync"/>). Jüngere Events werden geweckt (Latenz),
    /// schieben die Marke aber nicht vor: ein Nachzügler mit kleinerer Sequenz, dessen Transaktion noch läuft, wird nicht überholt.
    /// Tombstones (G-7) belegen nur Sequenzen eines gescheiterten Appends und wecken niemanden.
    /// </summary>
    public async Task<StreamChanges> ReadChangedStreamsAsync(
        long afterGlobalSequence, IReadOnlyCollection<string>? typen, int limit, CancellationToken ct)
    {
        await EventTabellenAsync(_store, ct);
        await using var session = _store.QuerySession();
        var schema = _store.Options.Events.DatabaseSchemaName;

        var (gesetzt, dahinter) = await GesetztAsync(afterGlobalSequence, ct);
        if (!dahinter) return new StreamChanges(Array.Empty<Guid>(), gesetzt, false);

        var conn = (Npgsql.NpgsqlConnection)session.Connection!;
        if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = EventStoreAbfragen.GeaenderteStreams(schema, typen is not null);
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = afterGlobalSequence });
        cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = limit });
        if (typen is not null) cmd.Parameters.Add(new Npgsql.NpgsqlParameter { Value = typen.ToArray() });

        var streams = new List<Guid>();
        var gesehen = new HashSet<Guid>();
        long letzte = afterGlobalSequence;
        var zeilen = 0;
        await using (var r = await cmd.ExecuteReaderAsync(ct))
            while (await r.ReadAsync(ct))
            {
                zeilen++;
                letzte = r.GetInt64(0);
                var s = r.GetGuid(1);
                if (gesehen.Add(s)) streams.Add(s);
            }

        var voll = zeilen >= limit;
        var hochwasser = voll ? Math.Min(gesetzt, letzte) : gesetzt;
        return new StreamChanges(streams, Math.Max(afterGlobalSequence, hochwasser), voll);
    }
}
