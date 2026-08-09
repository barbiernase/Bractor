# Handoff — P6.2: Pipeline-Event-Pfad in die Pull-Maschine falten

> Status: **offen** (P6.1 ✅, ganz P4 ✅ committet). Diese Scheibe ist bewusst NICHT in derselben
> Session mit-implementiert worden — Begründung unten (§4). Hier steht der präzise Ausführungsplan,
> geerdet auf dem verifizierten Ist-Code.

## 1. Ziel (Zielbild §4.5 / §6)

Der Pipeline-**Event-Pfad** (Kanal 2: Event rein → Command raus) *ist* bereits eine Reaktion. Er hängt
heute am **verlustbehafteten Push-Broker** (`BrokerSubscription`), nicht an der geordneten Pull-/Signal-
Maschine. P6.2 faltet ihn in dieselbe Konsumenten-Maschine wie Projektion/Reaktion (Ein-Strom/Emittierend,
P4) und **löscht das `BrokerSubscription`-Event-Abo** aus `PipelineActorBase`. Der **Trigger-Ingress**
(Kanal 1) und die **Self-Messages** (Kanal 0) bleiben als dünner Push-Adapter unverändert.

**Tor:** kein `BrokerSubscription`-Event-Abo mehr außer im Signal-Receiver; die drei Domänen-Pipelines
(`BenchmarkPipeline`, `FileWatchPipeline`, `ImageProcessingPipeline`) laufen unverändert über ihre
Handler-API — nur der *Transport* unter `Handle(evt, ctx)` ändert sich.

## 2. Ist-Zustand (verifiziert)

- `PipelineActorBase.OnStartedAsync` (`Infrastructure/Pipeline/PipelineActorBase.cs`) baut ein
  `BrokerSubscription` über `GetSubscribedEventTypes()` und abonniert jeden Typ (Push). Eingehende Events
  landen als `IAggregateEnvelope` → `OnEnvelopeAsync` → `DispatchEventAsync` → Commands via `CommandEmitter`.
- Genau EINE Pipeline hat einen Event-Pfad: `ImageProcessingPipeline.Handle(ImagePairKomplett)` →
  `KlassifiziereBildPaarDurchKi` (+ `Handle(PaarNichtKomplett)` = no-op). `Benchmark`/`FileWatch` sind reine
  Trigger-/Self-Pipelines (kein Kanal 2).
- Die Pull-Maschine (P4) dispatcht via `Func<EventEnvelope, ProjectionWriter, Task>`; Reaktionen routen ihre
  Commands über `HandlerOutputRouter` + `DetachedEmit` + `CommandEmitter` (bounded, deterministische Id).

## 3. Ausführungsplan

1. **Pull-Registrierung für Pipelines mit Event-Typen.** Den `PipelineActorGenerator` (oder einen neuen,
   parallelen Generator) so erweitern, dass er für jede `IPipelineHandler`-Klasse mit nicht-leeren
   `SubscribedEventTypes` zusätzlich erzeugt:
   - einen `{Name}PipelineEventPullKind : IClusterKindContributor` (analog `{Name}PullAdapterKind` im
     `PullPathGenerator`, KindName z. B. `"pull-pipeline-{Name}"`),
   - eine `PullPathRegistration(SubscriberId=PipelineId, KindName, SubscribedEventTypes)`.
2. **Brücke Pull-Dispatch → Pipeline-Event-Dispatch.** Im Kind-Factory-`dispatch`:
   `(EventEnvelope e, ProjectionWriter _) => { ctx = PipelineContext aus e; await handler.DispatchEventAsync(
   e, ctx, sendCommand, sendTrigger, broadcastTransient); }` — mit `sendCommand` = `DetachedEmit.Wrap` über
   `CommandEmitter` (identisch zur Reaktions-Route), `sendTrigger` = Pipeline→Pipeline (bounded, wie in P6.1),
   `broadcastTransient` = `BrokerPublisher`. Achse B = **emittierend** → `IEmittentenCursor` (P4.2), kein Tracker.
   *Wichtig:* der `PipelineContext` muss `SourceAggregateId/-Version/CorrelationId` aus dem Envelope tragen (wie
   heute `OnEnvelopeAsync`), sonst bricht die deterministische Emit-Id.
3. **Push-Abo entfernen.** In `PipelineActorBase`: `GetSubscribedEventTypes()`-Abo aus `OnStartedAsync`
   löschen, den `IAggregateEnvelope`-Kanal aus `ReceiveAsync` entfernen (Kanal 2 wandert komplett in die Pull-
   Maschine). Trigger (Kanal 1) + Self (Kanal 0) + `OnInitializeAsync`/`ScheduleSelf` **bleiben**.
   `PushSubscriberExclusions` ggf. um die Pipeline-Event-Subscriber ergänzen.
4. **Trigger-Ingress bleibt Push** (§6): `IPipelineTrigger`/`ScheduleSelf` unverändert; nur der Event-Pfad
   wandert.

## 4. Warum separat (nicht in der P6.1/P4-Session)

- **Kein Pipeline-Test-Harness.** Weder Prüfstand noch Integration testen heute IRGENDEINE Pipeline. Die
  einzige Event-Pipeline (`ImageProcessingPipeline`) hängt an OpenCV (`Cv2.ImRead`/`ImWrite`) + einem
  `IClassifierService`. Der Tor-Nachweis „die drei Pipelines laufen unverändert" ist ohne neuen Harness nicht
  führbar — und eine Transport-Änderung ungetestet zu shippen widerspricht der Faithfulness-Regel.
- **Empfehlung:** ZUERST einen Prüfstand-Harness bauen (Fake-`IPipelineHandler` mit
  `Handle(TestEvent)→yield TestCommand`, Fake-Emit-Seam wie in `EmitPrimitivTests`), der den Fold-Dispatch
  in-memory beweist (Event → Pipeline-Event-Handler → Command über das Primitiv, ohne echten Cluster/OpenCV).
  Dann die Generator-Änderung, dann das Push-Abo löschen. Erst danach `ImageProcessingPipeline` live verifizieren.

## 5. Regressions-Orakel

- Ebene 1: `dotnet test Infrastructure.Pruefstand.Tests` (aktuell 67/67).
- Ebene 2: `dotnet test Infrastructure.Integration.Tests` (aktuell 24/25 — nur der dokumentierte
  SnapshotLive-Cold-Boot-Flake). Infra: `scripts/dev-infra-setup.sh` (native Postgres/Redis/Consul + .NET 10,
  `DOTNET_ROLL_FORWARD=LatestMajor`).
