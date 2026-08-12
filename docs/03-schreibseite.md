# 03 — Die Schreibseite (Command → Event → Store)

Der Schreibpfad ist das dichte Zentrum des Frameworks. `AggregateActorBase` trägt fast alle
Invarianten und Audit-Fixes und ist ausführlich kommentiert.

## 3.1 Command-Flow end-to-end

**(a) Ingress — gRPC-Streaming-Gateway.**
`CqrsClientService.HandleCommandAsync` (`Infrastructure/GrpcClient/CqrsClientService.cs:376`)
empfängt eine `CommandRequest`, mappt sie via `ProtoMessageMapper` auf ein `CommandEnvelope`,
stempelt `OriginSessionId = sessionId` (für Targeted Delivery von Ablehnungen an den Aufrufer)
und ruft `_dispatcher.Dispatch(envelope)`. Dispatch ist fire-and-forget.

**(b) Routing — Dispatcher zu Cluster-Identität.**
`ProtoActorAggregateDispatcher.Dispatch` (`Infrastructure/Aggregate/ActorSystem/AggregateDispatcher.cs:49`)
baut die Ziel-Identität typbasiert: `ClusterIdentity.Create(guid, envelope.AggregateType)`.
Der Send ist `cluster.RequestAsync<CommandResult>(identity, envelope, ct)` mit bounded Token
(10 s/Versuch, 3 Versuche). Erschöpfte Versuche → LogError + durabler Dead-Letter, bewusst
**kein** Auto-Retry. Die serverseitige Overload `AggregateDispatcherExtensions.Dispatch(cmd)`
löst den Aggregat-Typ aus `GeneratedPipelines.CommandAggregateTypes` auf — statt aus einem
Magic String.

**(c) Aggregat-Actor — der Kern.**
`AggregateActorBase.ReceiveAsync` (`.../AggregateActorBase.cs:90`). Erstes `CommandEnvelope` →
`InitializeAsync` (Rehydration aus dem Log), dann `HandleCommandAsync`. Es wird **immer**
`context.Respond(...)` aufgerufen — sonst retryt Proto.Actor.

`HandleCommandAsync` dispatcht exhaustiv über den Summentyp `CommandModus`:
- `Client c` → OCC gegen `c.ExpectedVersion`, `istIdempotent: false`
- `Emittiert` → `expectedVersion = _state.Version`, `istIdempotent: true`

Der gemeinsame Kern `HandleCommandCoreAsync` (`:200`):
1. Guard: falsche AggregateId → Fehler.
2. **OCC-Vorprüfung**: `_state.Version != expectedVersion` → `CommandResult(Success=false)`.
3. **Framework-Inbox-Dedup** (nur idempotenter Pfad): bereits verarbeitete `CommandId` →
   Noop-Success; bereits abgelehnte → konsistente Ablehnung.
4. **Decider**: `_handler.HandleCommand(payload).ToList()`.
5. **Noop** (keine Events) → auf idempotentem Pfad trotzdem `KommandoVerarbeitet`-Marke
   co-committen (K2-Livelock-Fix).
6. Trennung `persistentEvents` vs. `rejections` (`ITransientEvent`); ein *gemischtes* Ergebnis
   wirft laut (`:307`).
7. **Reine Ablehnung** → kein Append/Apply; bei idempotent `KommandoAbgelehnt`-Marke; Targeted
   Delivery an den Aufrufer.
8. **Erfolg**: bei idempotent `KommandoVerarbeitet`-Marke anhängen, dann
   `_eventStore.AppendEventsAsync(...)`.
9. **Post-Append-Grenze** (`:372`): ab Commit ist der Command durabel. Applier mutiert `_state`
   in-memory, Redis-Track, Snapshot, Publish. Wirft etwas davon → `catch` rehydriert frisch
   aus dem Log und quittiert trotzdem **Erfolg** (H1: kein falsches Negativ).

## 3.2 Das Aggregat-Modell (reiner Fachcode)

Musterbeispiel `Domain/Konto/`. Ein Aggregat ist eine `partial class : IState` mit zwei
verschachtelten `partial`-Klassen `Decider : IDecider<T>` und `Applier : IApplier<T>`:

```csharp
// State — nur fachliche Properties. Id/Version werden generiert.
public partial class Konto : IState {
    public decimal Saldo { get; set; }
    public decimal Reserviert { get; set; }
    public bool Gesperrt { get; set; }
    public decimal Verfuegbar => Saldo - Reserviert;
}

// Command — record mit Guid AggregateId : ICommand, nur fachliche Felder.
public record ReserviereBetrag(Guid AggregateId, decimal Betrag) : ICommand;

// Events — persistent : IEvent ; Ablehnung : ITransientEvent (nie im Log).
public record BetragReserviert(decimal Betrag) : IEvent;
public record DeckungReichtNicht(decimal Verfuegbar, decimal Angefordert) : ITransientEvent;

// Decider — ein Decide je Command, Rückgabe IEnumerable<OneOf<...>>.
public partial class Konto {
  public partial class Decider : IDecider<Konto> {
    public IEnumerable<OneOf<BetragReserviert, DeckungReichtNicht>> Decide(ReserviereBetrag cmd) {
        if (State.Gesperrt || State.Verfuegbar < cmd.Betrag) {
            yield return new DeckungReichtNicht(State.Verfuegbar, cmd.Betrag); yield break; }
        yield return new BetragReserviert(cmd.Betrag);
    }
  }
  // Applier — ein Apply je Event, mutiert State.
  public partial class Applier : IApplier<Konto> {
    public void Apply(BetragReserviert e) => State.Reserviert += e.Betrag;
  }
}
```

**Wie der Fachcode rein bleibt:** `this.State`, der `Decider(Konto state)`-Ctor sowie
`Id`/`Version` sind generiert. OCC, Signal, Cursor, Idempotenz erscheinen ausschließlich im
Framework. Die Dedup-Marken (`KommandoVerarbeitet`/`KommandoAbgelehnt`) sind `IProzessIntern`,
werden beim Falten übersprungen (aber versionsgezählt) — der Decider sieht sie nie.

## 3.3 Event-Store (Marten)

`Infrastructure/Persistence/MartenEventStore.cs`:
- **Append** (`:59`): `expectedVersion == 0` → `session.Events.StartStream` (wirft bei
  Doppel-Erstellung); `> 0` → `session.Events.Append(id, expectedVersion + count, events)`.
- **OCC**: `EventStreamUnexpectedMaxEventIdException` → `ConcurrencyException`; Collision ebenso.
  Metadaten (Correlation/Causation/Header `aggregate_type`) werden in **dieselbe** Transaktion
  gestempelt.
- **Read** (`ReadStreamAsync`, `:196`): `Where(StreamId== && Version>=from).OrderBy(Version)`,
  materialisiert `EventEnvelope` mit **Upcasting-Naht** (`GeneratedEventUpcasting.Aufwerten`,
  1:1 oder 1:N mit `SubIndex`).
- **Poll-Backstop** (`ReadChangedStreamsAsync`, `:252`): globale Marten-Sequenz `seq_id` mit
  **Straggler-Karenz** (default 3 s): die High-Water-Mark rückt nur bis zur höchsten Sequenz
  vor, deren Event älter als die Karenz ist — schützt gegen nebenläufig in umgekehrter
  Seq-Reihenfolge sichtbare Commits. Referenzzeit ist die DB-Uhr (`select now()`), kein
  App/DB-Skew.

## 3.4 Group-Commit-Batching + paralleler Drain

`Infrastructure/Persistence/BatchingEventAppender.cs` — Dekorator um `IEventStoreRepository`,
nur der Schreib-Hotpath wird gebündelt, Reads durchgereicht.

- **Hotpath**: baut ein `Pending` mit `TaskCompletionSource`, schreibt in einen unbounded
  Channel, gibt `Tcs.Task` zurück. Der Task löst **erst beim Batch-Commit** — die scharfe
  Durabilitätsgrenze. Single-Writer bleibt (der Actor awaited vor dem nächsten Command
  desselben Streams).
- **Drain**: K parallele Loops (`AppendDrainParallelism`, default 4). Jeder sammelt bis
  `AppendBatchMaxSize` (256) oder ein optionales `AppendBatchLingerMs`-Fenster (0 =
  selbstregulierend), dann `FlushAsync`.
- **Commit** (`MartenEventBatchWriter.WriteBatchAsync`): N Appends in **einer**
  `LightweightSession` → ein `SaveChangesAsync` = eine Postgres-Transaktion. Metadaten **pro
  Event** gesetzt (Voraussetzung fürs Bündeln verschiedener Korrelationen).
- **Fehlermodell**: Batch ist all-or-nothing; wirft er (z. B. OCC-Konflikt eines Streams),
  wiederholt `IsolateAsync` jeden Auftrag **einzeln** über den inneren Store → nur der
  Schuldige fällt.
- **Verteilt-korrekt weil node-lokal**: Proto.Cluster garantiert Single-Activation — ein
  Stream lebt auf genau einem Node, zwei Nodes bündeln nie denselben Stream, OCC pro Stream
  bleibt ohne Cross-Node-Koordination korrekt.

Gemessener Gewinn (Projektnotiz): **+48 %** Schreibdurchsatz; paralleler Drain skaliert
sublinear (`wait_event` offen, siehe [13](13-reifegrad-schulden-bewertung.md)).

## 3.5 Redis Version-Index (nicht-autoritativ)

`RedisVersionTracker.cs`: schneller verteilter Index für Stale-Detection auf der Leseseite
(Wahrheit bleibt der Log). Datenmodell: Hash `agg:{id}` (Felder `v`/`t`/`ts`) + Sekundär-Set
`agg_idx:{type}`. Geschrieben **nach** erfolgreichem Append, per Redis-Pipeline. Bei
**jedem** Track indexiert (Audit-Fix #6 — sonst würden Multi-Event-Aggregate fehlen).
Graceful Degradation: `RedisConnection/TimeoutException` → nur Warning, Command-Flow bricht
nicht. Ganz abschaltbar via `EnableVersionTracking=false` → `NullVersionTracker`.

## 3.6 Signal-Mechanik `(StreamId, Version)`

- **Vertrag**: `IStateChangeSignal` (`Abstractions/IStateChangeSignal.cs`) trägt nur
  `Guid StreamId` + `int Version`. Typisierte Variante `IStateChangeSignal<TEvent>` trägt die
  Event→Signal-Kante am Typ.
- **Erzeugung**: pro persistiertem Event ein `StateChangeVia{Event}` (SignalTypeGenerator);
  die Instanz baut `GeneratedSignalFactory.Create(payload, aggregateId, version)`.
- **Verteilung**: im Erfolgspfad **fire-and-forget** über den `BrokerPublisher`
  (`AggregateActorBase.cs:634`). Kein Warten auf Shard-Acks — der Command ist bereits
  committet, Zustellung ist best-effort, durable Konsumenten heilen über Log-Read + Poll.
  Jedes Event trägt seine eigene Position (`baseVersion`, nicht den Batch-Endwert).
- **Routing**: der `BrokerPublisher` shardet über `Payload.GetType()` — die konkrete
  Signal-Klasse — sodass nur Receiver dieses Event-Typs geweckt werden.

## 3.7 Proto.Actor: virtuelle Cluster-Actors

Wiring in `Infrastructure/Extensions/CqrsServiceExtension.cs:408`:
- `ActorSystem` mit `ConsulProvider` + `PartitionIdentityLookup`.
- **Aggregate-Kinds generiert**: `GeneratedAggregates.GetAllKinds(system)`. Pro `IState`-Klasse
  erzeugt der `AggregateActorGenerator` (a) `XActor : AggregateActorBase<X>` mit DI-Ctor und
  (b) `AddTransient<XActor>()` + `new ClusterKind(identität, PropsFor<XActor>())`. Die
  Kind-Identität = `[AggregatName("…")]` oder Typname — garantiert konsistent zum Routing.
- **Identität pro Command**: `ClusterIdentity.Create(aggregateId, aggregateType)` — der
  Stream-Guid *ist* die virtuelle Actor-Identität. Single-Activation → Single-Writer pro
  Stream (tragend für Batching + OCC).
- **Multi-Node**: generierter Wire-Serializer `CqrsWireSerializer` an `WithRemote`;
  `WireSerializerBootCheck` bricht bei Lücke ab. Siehe [06](06-transport-multinode-betrieb.md).

Die alte handgeschriebene `ClusterKindsFactory.cs` ist vollständig auskommentiert — Zeugnis
der Generator-Migration.

## 3.8 Schulden im Schreibpfad (Auszug)

Vollständig und priorisiert in [13](13-reifegrad-schulden-bewertung.md). Kurz:
- **`BoundedInbox` nicht airtight**: FIFO-Verdrängung ab `InboxCap` (10.000) — eine sehr alte
  Command-Id wird nicht mehr als Duplikat erkannt (bewusster Speicher-Tradeoff).
- **JSON-Round-Trip-`Clone`** für Snapshots — brüchig bei State-Typen, die nicht sauber
  round-trippen.
- **Client-Command-Pfad ist verlustbehaftet**: nach 3 Fehlversuchen Dead-Letter, kein
  Auto-Retry, und der gRPC-Service hat kein sichtbares Ack-Protokoll — der externe Client
  müsste selbst erneut senden.
- **Doppelte Falt-Logik**: `MartenEventStore.LoadStateAsync` (im Live-Pfad tot) und
  `AggregateRehydrator` müssen synchron bleiben.
