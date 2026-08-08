# Backend-Neubau — Fahrplan

> Kompakter, umsetzbarer Begleiter zu `docs/backend-neubau-einheitliche-maschine.md` (dort: Herleitung,
> Philosophie, Begründung jeder Phase). Dieses Dokument ist die **Task-/Tor-Liste** zum Abhaken.
>
> **Leitsatz:** Wir bauen die *Backend-Maschine* neu, nicht den Stack. **Festgehalten:** API,
> Source-Generatoren, Marten/PostgreSQL, Redis, Proto.Actor, gRPC, Client-Struktur. **Ziel:** aus einer
> Feature-Sammlung *eine* einheitliche, wartbare Maschine.

## Legende

- **Tor** = messbare Abnahmebedingung; das System ist nach jeder Phase grün.
- **Ebene 1** = Prüfstand (in-memory, Fake-Cluster) · **Ebene 2** = Integration (echtes Marten/Consul/Redis,
  *sequentiell*) · **Ebene 3** = Last-Harness.
- Invarianten-Kürzel: **TG** = technischer Graph, **EM** = Emit, **GA** = Garantie (siehe Leitdokument §5).

## Abhängigkeitsgraph der Phasen

```
  P0 Verträge ─┬─► P1 Kanten-Graph ───────────────────────────► P7 Transport/K1 ─► P8 Multi-Node
               └─► P2 Schreiber+Inbox ─► P3 Emit ─┬─► P4 Konsum-Maschine ─► P6 Pipeline
                                                   ├─► P5 Prozess-festklopfen   (braucht P4 NICHT)
                                                   └─► Feature-Strom             (orthogonal)
```

**Kein Zug, ein DAG.** Fundament **P0–P3** zuerst und isoliert. Nach P3 verzweigen *vier unabhängige
Ströme*: **P4→P6** (Vereinheitlichung, de-scoped), **P5** (Prozess-Terminal-Fix — hängt an **P3**, nicht
an P4), **Feature-Strom** (orthogonal), **P7→P8** (Multi-Node — hängt nur an P1). Die Nummerierung ist
keine strikte Ausführungsreihenfolge. **Reihenfolge-Prinzip (Zielbild §10/§13):** erst Emit+Bugfixes, dann
die hochwertigen risikoarmen Fixes (P5/Features), die große Vereinheitlichung (P4) *zuletzt* — sie ist
**keine** Voraussetzung für P5 oder Features.

---

## Phase 0 — Verträge & Graph-Fundament
**Zweck:** Grundlage ohne Verhaltensänderung.

- [ ] `ICommandEmitter` + `EmitKausalität(Guid Korrelation, Guid Ursache, string Diskriminator)` in `Abstractions`.
- [ ] Schreiber-Eingänge entwerfen: `HandleClientCommand(expectedVersion)` / `HandleEmittedCommand(commandId)`.
- [ ] `AnyVersion`-Sentinel als „zu entfernen" markieren (noch nicht löschen).
- [ ] Marken-Interfaces `IReplaybarerTracker` / `IEmittentenCursor` in `Abstractions`.

**Tor:** kompiliert; alle bestehenden Tests unverändert grün.

---

## Phase 1 — Kanonischer Kanten-Graph (Generatoren) · TG-1, TG-3
**Zweck:** eine signaturgetriebene Ableitung pro Kante; kollisionsfreie Identitäten.

- [ ] `GeneratedEventCommandMapping` (namespace-grob) löschen.
- [ ] Konsumenten (`MessageTypeMapping`, Proto/`EventCommandMapping`) auf präzises `GeneratedCommandRouting` umstellen.
- [ ] `AggregateHandlerGenerator`: volle OneOf-Typargumente lesen (statt nur Bool `ReturnsOneOf`).
- [ ] `Event→Signal` aus Typ/Marker ableiten statt Namens-Präfix `StateChangeVia{X}`.
- [ ] Knoten-Identität auf FQN oder `[AggregatName]`/`[ProzessName]`-Attribut; „pro Namespace ein Aggregat"-Konvention entfernen.

**Tor (Ebene 1):** kein Generator nutzt Namespace-Gruppierung/Namens-Präfix; keine zwei Generatoren erzeugen
dieselbe Relation; CQRS001/002/003/010 grün; ein bewusster Namens-Kollisionstest (gleicher Typname, zwei
Namespaces) bricht *nicht* die Laufzeit.

---

## Phase 2 — Der Schreiber (P1) + Inbox · GA-1 (Aggregat-Commit-Punkt)
**Zweck:** zwei explizite Eingänge; Co-Commit in *einer* Marten-Transaktion.
**Der Sentinel bleibt hier noch** — seine Löschung ist an die Sender-Migration in P3 gekoppelt.

- [ ] `HandleClientCommand`/`HandleEmittedCommand` umsetzen — *neben* dem alten `AnyVersion`-Pfad (koexistieren bis P3).
- [ ] Inbox-Co-Commit: `KommandoVerarbeitet` + Domänen-Events in *einem* `SaveChangesAsync`.
- [ ] `is not IProzessIntern`-Filter im Live-Apply angleichen an die Rehydration (Symmetrie).

**Tor (Ebene 1+2):** OCC-Pfad byte-genau unverändert (Regressionsvergleich); Emit-Pfad exactly-once am
Empfänger (Fake-Cluster, verlorene Quittung); Co-Commit = *eine* Transaktion (Ebene 2 gegen Marten).

---

## Phase 3 — Das Emit-Primitiv · EM-1, EM-2 (W1/W2 strukturell weg)
**Zweck:** vier Emit-Pfade → einer **und** der Sentinel stirbt.

- [ ] `ICommandEmitter`-Impl: deterministische CommandId aus `EmitKausalität`, bounded Token, at-least-once.
- [ ] **Alle** Sender migrieren: Dispatcher-intern, `HandlerOutputRouter`, `ProzessManager`/`DetachedProzessSend`, `PipelineActorBase` → über das Primitiv auf `HandleEmittedCommand`.
- [ ] Erst **danach**: `AnyVersion` löschen; `PipelineActorBase`-Retry-Schleife (zufällige CommandId + `CancellationToken.None`) entfernen.

**Tor (Ebene 1):** Grep findet keinen zweiten `RequestAsync<CommandResult>` außerhalb des Primitivs, kein
`CancellationToken.None` auf Command-Kanten, kein `AnyVersion` mehr; **Pipeline dedupliziert (W1)** und
**hängt nicht (W2)** — Fake-Cluster-Test mit verlorener Quittung. *(Pipeline-Tor transitorisch — der
Event-Pfad wird in P6 in Reaktionen gefaltet.)*

---

## Phase 4 — Konsumenten-Maschinenklasse (zwei Achsen) · GA-1 (Projektions-Commit-Punkt)
**Zweck:** Projektion/Reaktion/Pipeline-Event auf *einer* Klasse. *(Hängt an P3; de-scoped, keine
Voraussetzung für P5/Features.)* **Riskanter Schnitt** — bei Bedarf Ausprägung für Ausprägung migrieren.

- [ ] Lese-Falt-Emit-Schleife parametrisieren: Achse A (Ein-Strom | Korrelations-Multistrom) × Achse B (Replaybar | Emittierend).
- [ ] Kind + Marken-Interface je Achsen-Kombination generieren (`IReplaybarerTracker` vs. `IEmittentenCursor`).
- [ ] **GA-1-Build-/DI-Check umsetzen:** append-artige Projektion ohne Co-Commit-`IReplaybarerTracker` → Build-Fehler.
- [ ] Marker-API unverändert (`ISubscriber`+`Handle`, `IPipelineHandler`+`Handle`, `IProzessDefinition`).

**Tor (Ebene 1+2):** alle Ein-Strom-Konsumenten auf derselben Maschinen-Klasse; `Reset` nur bei
`IReplaybarerTracker` (Compile-Zeit-Schnitt); der GA-1-Check bricht eine bewusst tracker-lose
Append-Projektion; Projektions-/Reaktions-E2E grün.

---

## Phase 5 — Prozess festklopfen (Terminal-Fix + Marking-Cache) · hängt an P3, NICHT an P4
**Zweck:** Marking aus dem Log; Cursor als Cache; Terminal ohne `WeckeSelbst`. **Der hochwertigste,
risikoärmste Korrektheitsgewinn — früh und unabhängig von der Vereinheitlichung.** (Die Migration des
Managers *auf* die P4-Maschinenklasse ist eine optionale Folge-Konsolidierung, kein Teil dieses Tors.)

- [ ] (a) `CorrelationId`-Poll-Routing: Typ-Filter (`relevanteTypen`, `ProzessManagerWiring.cs:154`) fallen lassen, per `CorrelationId`-Metadatum routen.
  - **Tor:** Terminal erkannt ohne `WeckeSelbst`; `ProzessBackstopE2ETests` grün → `WeckeSelbst` + `ProzessOffenIndex` retirable.
- [ ] (b) Marking-Cursor als **Cache** (best-effort, außerhalb der Entscheidungs-Transaktion; kein Co-Commit).
  - [ ] **Verdichtete Darstellung zwingend:** Count-Join = Zähler+Done-Set, Fan-out = Done-Set der Zweige — **nicht** alle Tokens roh (sonst kehrt O(N²) zurück).
  - **Tor:** Sagas grün; O(N²)→O(N) (Read-Zähler); Voll-Fold heilt Cache-Verlust/`RegelHash`-Mismatch.
- [ ] `ErgebnisDa`/`WirkungDa`-Zwei-Achsen-Marke bleibt (S15-Schutz).

---

## Phase 6 — Pipeline ehrlich zerlegen · hängt an P4
**Zweck:** kein gepushter Event-Konsument mehr.

- [ ] Event-Pfad (Kanal 2) in die Konsumenten-Maschine falten (Reaktion, Ein-Strom/Emittierend).
- [ ] Trigger-Ingress (Kanal 1) als dünner Push-Adapter über das Emit-Primitiv; `ScheduleSelf`/Self-Messages lokal.
- [ ] `IPipelineTrigger`/Timer/Webhook-Registrierung verdrahten.

**Tor (Ebene 1+2):** kein `BrokerSubscription`-Event-Abo außer im Signal-Receiver; die drei Domänen-Pipelines
(`Benchmark`, `FileWatch`, `ImageProcessing`) laufen unverändert über ihre Handler-API.

---

## Feature-Strom (orthogonal, hängt nur an P3) — aus Zielbild §11
**Zweck:** die benannten Feature-Lücken auf der sauberen Emit-Grundlage; **kein** Teil der Vereinheitlichung.

- [ ] **DLQ-Replay** (Ops-/Read-Pfad auf `dlq`).
- [ ] **Timer/Webhook-Trigger** + `ITriggerRegistration` verdrahten (Trigger-Ingress bleibt Push).
- [ ] **Projektions-Rebuild-Runner** (Vertrag + `Reset` da; leicht nach P4 — *eine* Rebuild-Schleife).
- [ ] **Deadlines/Timeouts** (nach stabilem Prozessmodell — Timer-Token/Zeit-Event).
- [ ] **Prozess-Verkettung** (Modell ok; braucht Test/Beispiel).
- [ ] **Monitoring** (Metrics/Tracing/HealthChecks/Prozess-Sicht; profitiert von der uniformen Maschine).

---

## Phase 7 — Transport in den Graphen · TG-2 / K1 (orthogonal)
**Zweck:** interne Nachrichten als Graph-Knoten; generierter Poly-Serializer.

- [ ] Interne Typen (`CommandEnvelope`, `EventEnvelope`, `SignalEnvelope`, `Wake`/`WakeAck`, `Publish`/`Ack`, `Subscribe`, `ProzessWake`/`MeldeFehlschlag`) in die Typ-Registry.
- [ ] Poly-Serializer generieren (reflexionsfrei, über die Registry) und am `WithRemote`-Punkt (`CqrsServiceExtension.cs:335-340`) registrieren.
- [ ] Boot-Check: jeder interne Typ hat einen Serializer → sonst Start-Abbruch.
- [ ] Laute Fehler: `_ = RequestAsync`-Stellen fangen + dead-lettern statt stillem Drop.
- [ ] Broker: Subscriber per `ClusterIdentity` statt lokaler PID; Abo-Entscheidung (durable vs. Poll-heilt) treffen.

**Tor (Ebene 1):** Round-trip-Test über *jeden* internen Typ grün (serialisiert+deserialisiert = gleich);
Boot bricht bei fehlendem Serializer.

---

## Phase 8 — Multi-Node-Tor (Verifikation)
**Zweck:** der eigentliche Rest-Aufwand — beweisen, nicht coden.

- [ ] Zwei-Member-Test: zwei ActorSystems, ein Consul-Cluster.

**Tor (Ebene 2, zwei Nodes):** ein Adapter je Stream; Ordnung erhalten; Poll heilt Totalverlust cross-node.

---

## Unabhängige Bugfixes (jederzeit, entkoppelt)

- [ ] `BrokerIdentity.GetShardIndex`: `(hash & 0x7FFFFFFF) % ShardCount` statt `Math.Abs(hash)` (`BrokerIdentity.cs:63`, Overflow bei `int.MinValue`).
- [ ] Fan-out-Diskriminator: RegelIndex + Instanz-Index in die `Vorgang`-Id (latente Kollision, `ProzessId`/`ProzessManager`).
- [ ] Gemischtes Decider-Ergebnis (Effekt + Ablehnung): bereits als Fail-fast behandelt — beim Neubau als Contract festschreiben (`AggregateActorBase.cs:257`).

## Querschnitts-Regeln (während des ganzen Umbaus halten)

- Adressierung nur per `ClusterIdentity` — nie lokale PID.
- Alle Wahrheit + alle Co-Commits in *einem* Marten; Redis nur abgeleitet.
- Interne Messages sind reine Daten — kein `Func`/Delegate/`Task`/Closure im Payload (Seam als Dependency ist ok).
- Zeit-Entscheidungen per DB-Uhr, nie per Node-`DateTime.UtcNow`.
- Korrektheit aus dem Log (OCC/Idempotenz/Poll), nie aus „ist eh derselbe Prozess".
- Serializer-Round-trip-Test früh mitlaufen lassen.
- Integrationstests immer *sequentiell* (`Infrastructure.Integration.Tests/xunit.runner.json`).
