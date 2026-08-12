# 01 — Schreibseite (Command → Append → Event-Stream)

> Wie ein Command in den Log kommt. Verwandt: [00 Überblick](00-ueberblick.md),
> [02 Konsum-Maschine](02-konsum-maschine.md).

## Der volle Weg

Der virtuelle Cluster-Actor `AggregateActorBase<TState>`
(`Infrastructure/Aggregate/ActorSystem/AggregateActorBase.cs`) empfängt eine
`CommandEnvelope`.

1. **Erste Nachricht → Rehydration.** `AggregateRehydrator.LoadAsync` faltet in EINEM
   Tail-Read State **und** beide Inbox-Mengen (siehe *Framework-Inbox*). Optional aus einem
   Snapshot geseedet, dann nur der Tail ab `snap.Version+1`.
2. **Dispatch nach `CommandModus`** (exhaustiver `switch`, kein Sentinel):
   - `CommandModus.Client(int ExpectedVersion)` → OCC gegen die behauptete Version,
     nicht-idempotent (der normale Client-Command-Pfad).
   - `CommandModus.Emittiert` → `expectedVersion = _state.Version` (Single-Writer,
     aufgelöst), idempotent über die Inbox (Reaktion/Prozess/Pipeline/Frist).
3. **Kern (`HandleCommandCoreAsync`):** AggregateId-Check → Actor-seitige OCC-Assertion
   (`_state.Version != expectedVersion` → `Success=false` mit `NewVersion`) → Inbox-Dedup
   (nur emittierter Pfad) → Decider → Trennung `persistentEvents` vs.
   `ITransientEvent`-Ablehnungen → reine Ablehnung (kein Append) **oder** Erfolg (Append).
4. **Append + Metadaten:** `AppendEventsAsync(id, expectedVersion, events, correlationId,
   causationId = CommandId, aggregateType)`. Auf dem emittierten Pfad wird die Inbox-Marke
   an die Event-Liste angehängt und **co-committet** (eine Transaktion).
5. **Post-Append (Härtung):** Ab dem committeten Append ist der Command durabel. Alles
   Weitere (Applier auf `_state`, Version-Track, Snapshot, Publish) läuft in einem inneren
   try — wirft es, wird der State frisch rehydriert und trotzdem `Success=true` geantwortet.
   Verhindert falsche Negative (Doppel-Kompensation) und vergiftete Actors.

## `CommandModus` statt Sentinel

`Abstractions/CommandModus.cs` ist ein geschlossener Summentyp:

```
abstract record CommandModus            // privater Ctor
  ├─ sealed record Client(int ExpectedVersion)
  └─ sealed record Emittiert
```

`CommandEnvelope` hat `required CommandModus Modus` → es gibt keinen Default-Pfad und keinen
Footgun. Die Version lebt **strukturell** nur im Client-Fall; interne Emitter behaupten nie
eine Version. (Der frühere `AnyVersion = -1`-Sentinel ist gelöscht.)

## Version pro Event & Metadaten

- `EventEnvelopeFactory.BuildPerEvent` gibt jedem Event seine eigene Position
  `baseVersion + i + 1` — nie den Batch-Endwert.
- Marten-`MetadataConfig` ist aktiv: der Actor setzt `session.CorrelationId`/`CausationId`
  + Header `aggregate_type`; `ReadStreamAsync` rekonstruiert sie in den `EventEnvelope`. Die
  Prozess-Maschine faltet über `CausationId == Vorgang` und routet über `CorrelationId`.

## Framework-Inbox (Exactly-once, Zwei-Mengen)

Der Actor hält zwei gedeckelte Mengen (`BoundedInbox`, Cap 10 000, FIFO-Verdrängung):
`_verarbeiteteCommandIds` und `_abgelehnteCommandIds`. Beide werden aus dem Stream gefaltet.

Die Marken (`Infrastructure/Aggregate/KommandoVerarbeitet.cs`) sind `IEvent, IProzessIntern`
→ beim Domänen-Falten übersprungen (der Applier sieht sie nie), aber **in der Version
mitgezählt** (Stream-Position bündig):

- `KommandoVerarbeitet(CommandId)` — Erfolg oder idempotenter Noop.
- `KommandoAbgelehnt(CommandId, Grund)` — fachliche Ablehnung (`ITransientEvent`).

**Dedup-Semantik (nur emittierter Pfad):**
- CommandId in `_verarbeiteteCommandIds` → `Success=true` (Noop).
- CommandId in `_abgelehnteCommandIds` → **konsistent `Success=false`**, nie Erfolg — das
  schließt den stillen Falsch-Erfolg bei Re-Delivery nach einer Zustandsänderung.

Diese **Zwei-Mengen-Inbox + `KommandoAbgelehnt`-Marke + die Fold-Achse `AbgelehntDa` im
Prozess-Manager gehören zusammen** — einzeln entstünde ein stiller Falsch-Erfolg
(`ProzessBeendet(true)` trotz abgelehnten Schritts).

## OCC-Assertion (zwei Ebenen)

- **Actor-seitig, schnell:** `_state.Version != expectedVersion` → `Success=false` mit
  `NewVersion` (Client-Pipeline kann retrien).
- **Marten-seitig:** `EventStreamUnexpectedMaxEventIdException` /
  `ExistingStreamIdCollisionException` → `ConcurrencyException`.

Client-Pfad = OCC gegen die behauptete Version. Emittierter Pfad = `_state.Version` (kein
Konflikt im Normalfall); Idempotenz trägt die Inbox, nicht OCC.

## Group-Commit-Batching (Perf, +48 %)

`Infrastructure/Persistence/BatchingEventAppender.cs` ist ein **node-lokaler** Dekorator um
`IEventStoreRepository`:

- Appends werden in einen unbounded Channel eingereiht; der zurückgegebene Task löst **erst
  nach Batch-Commit** (Enqueue ≠ durabel — „append vor Mutation" bleibt).
- **Paralleler Commit-Drain:** K Drain-Loops teilen sich den Channel, jeder committet auf
  eigener Marten-Session/Connection. Default `AppendDrainParallelism = 4`. Sicher durch
  Proto.Cluster Single-Activation (ein Stream liegt nie in zwei Batches gleichzeitig).
- `MartenEventBatchWriter.WriteBatchAsync` staged N Appends in EINE `LightweightSession` →
  EIN `SaveChangesAsync` = eine Postgres-Transaktion. Metadaten werden pro Event gesetzt.
- **Fehlermodell:** Group-Commit ist alles-oder-nichts; wirft er (z. B. OCC-Konflikt eines
  Streams), fällt `IsolateAsync` auf Einzel-Appends zurück — nur der Schuldige scheitert.
- **Perf-Befund:** Der Schreibpfad ist commit-WAIT-gebunden (nicht fsync); der parallele
  Drain hebt den Durchsatz um ~48 %, skaliert aber **sublinear** (geteilter Postgres-
  Serialisierungspunkt; `wait_event`-Ursache noch offen).

## Version-Index (Redis, optional)

`RedisVersionTracker` ist ein abgeleiteter, nicht-autoritativer Index (Wahrheit bleibt der
Log). Der Actor trackt **nach** dem durablen Append, best-effort (Redis-Fehler unterbrechen
den Flow nie). `EnableVersionTracking` (Default AN) schaltet auf `NullVersionTracker`
(No-op) um — für Messung und Redis-losen Betrieb. Perf-Befund: synchroner localhost-Redis =
0 % Effekt.

## Serialisierung (reflection-frei, opt-in)

- `Infrastructure/Serialization/EventJsonSerializerContext.cs` — ein hand-gepflegtes
  STJ-Source-Gen-Manifest (`[JsonSerializable]`) über alle persistierbaren Events.
- `EventJsonGenerator` erzeugt `GeneratedEventJson.Serialize/Deserialize/Diskriminator`
  (Invariante 4). Compile-Zeit-Drift-Schutz: fehlt ein Event im Manifest, bricht der Build.
- **Status:** opt-in via `UseGeneratedJsonSerializer` (Default AUS); Baustein für einen
  künftigen COPY-Event-Store. Perf-neutral bei aktueller Last — bewusst zurückgestellt.

## Snapshots

Voll verdrahtete, abgeleitete Cache-Naht (Miss/stale → Voll-Replay, nie Fehler):

- `Abstractions/Snapshot.cs`: `Snapshot<TState>` (Version inkl. interner Marken,
  `SchemaVersion`, **`ProcessedCommandIds` + `RejectedCommandIds`** = beide Inbox-Mengen);
  `ISnapshotStore`; `SnapshotOptions(Threshold=200, InboxCap=10 000)`.
- `MartenSnapshotStore`: ein jsonb-Doc je Aggregat-Typ; Registrierung reflection-frei
  generiert (`SnapshotRegistrationGenerator`).
- Schreiben out-of-band: nach je `Threshold` Events + bei `Stopping`, State + Inboxen
  in-turn eingefroren, dann fire-and-forget `SaveAsync`. Fehler folgenlos.
- Bekannter Flake: `SnapshotLiveE2ETests` cold-boot (Ursache Consul-Cluster-Boot, **nicht**
  Schreibpfad; nicht an Timeouts drehen).

## Offene Punkte

- **Cross-Node-Serialisierung** fehlt → de facto single-node.
- Paralleler Drain **sublinear** (`wait_event` in Postgres unaufgelöst).
- Zwei DB-Uhr-Implementierungen (`MartenDbClock` + `MartenEventStore.DbNowAsync`) leicht
  redundant.

## Schlüsseldateien

`Infrastructure/Aggregate/ActorSystem/AggregateActorBase.cs`,
`Aggregate/AggregateRehydrator.cs`, `Aggregate/BoundedInbox.cs`,
`Aggregate/KommandoVerarbeitet.cs`, `Aggregate/EventEnvelopeFactory.cs`,
`Persistence/MartenEventStore.cs`, `Persistence/BatchingEventAppender.cs`,
`Persistence/MartenEventBatchWriter.cs`, `Persistence/RedisVersionTracker.cs`,
`Persistence/NullVersionTracker.cs`, `Persistence/MartenSnapshotStore.cs`,
`Serialization/EventJsonSerializerContext.cs`; `Abstractions/CommandModus.cs`,
`CommandEnvelope.cs`, `Snapshot.cs`; DI + Flags in
`Infrastructure/Extensions/CqrsServiceExtension.cs`.
