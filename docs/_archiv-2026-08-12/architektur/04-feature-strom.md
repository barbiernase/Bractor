# 04 — Feature-Strom (Pipeline, Trigger, Deadlines, Monitoring, Dead-Letter)

> Die additiven Feature-Scheiben rund um den Kern (geliefert am 10.08). Verwandt:
> [00 Überblick](00-ueberblick.md), [03 Prozess-Maschine](03-prozess-maschine.md).

## Pipeline

`Infrastructure/Pipeline/PipelineActorBase.cs` ist das serverseitige Gegenstück zum
gRPC-Client: empfängt Events/Trigger, sendet Commands — der vierte durable Konsument. Vier
Kanäle:

- **Self-Messages** (`IPipelineSelfMessage`): via `ScheduleSelf`/`ReenterAfter` geplante
  Ticks; bleibt lokal im Actor (kein Cursor, kein Log).
- **Trigger** (`IPipelineTrigger`): direkte Messages, beantwortet mit `PipelineAck`.
- **Envelope** (`IAggregateEnvelope`): seit **P6.2 nur noch TRANSIENTE Events**
  (`ITransientEvent`) über den Push-Broker. Persistierte Events laufen über die
  Pull-Maschine.

**Command-Send** über das EINE Emit-Primitiv (`CommandEmitter`): deterministische `EmitId`
aus `EmitKausalität(korrelation, sourceAggregateId, "{version}:{cmd}")` → Empfänger-Inbox
dedupliziert. Die alte „Version 0 → Conflict → Retry"-Strategie ist entfernt.

**P6-Zerlegung (erledigt):**
- **P6.1** — Actor entrümpelt: toter OCC-Ballast (`MaxRetries`, `ResolveVersion`,
  `DeadLetterAsync`, write-only Version-Cache) gelöscht; die Trigger-Kante von
  `CancellationToken.None` auf bounded 5 s umgestellt.
- **P6.2** — der persistierte Event-Pfad wurde in die Pull-/Signal-Maschine gefaltet.
  `PipelineEventPullBridge` adaptiert die generierte `DispatchEventAsync` auf den
  Pull-Dispatch; der `PipelineActorGenerator` erzeugt je Pipeline mit persistierten
  `SubscribedEventTypes` einen `{Name}EventPullKind` + `PullPathRegistration`
  (`AddGeneratedPipelineEventPulls()`).

**persistiert → Pull / transient → Push** ist die konkrete Anwendung von Invariante 6.

**`PipelineTriggerSender`** ist die eine pipeline→pipeline-Trigger-Route (Routing via
`GeneratedPipelines.TriggerToPipelineId`, bounded 5 s, at-least-once).

## Trigger

Beide Trigger sind bewusst **PUSH/verlierbar** — ein Tick/Webhook ist KEIN Log-Event (kein
Cursor, kein Fold, Invariante 2/6). Ein verlorener Tick/POST heilt der nächste.

- **Timer** (`TimerTrigger` + `TimerTriggerActor`): der reine Kern `TickAsync` (kein
  Actor/Cluster/Zeit → deterministisch prüfbar) erzeugt je Tick einen frischen Trigger; die
  Zeitschleife lebt im Actor (`ReenterAfter`-Loop, mailbox-safe, kein Timer-Thread).
  `TimerTrigger.Registrierung(name, intervall, erzeugeTrigger)` liefert eine
  `ITriggerRegistration`.
- **Webhook** (`WebhookTrigger`): `MapPipelineWebhook<TRequest>(route, baueTrigger)` mappt
  `POST {route}` → JSON-Body → Trigger → Pipeline, antwortet **202 Accepted** (Empfang
  quittiert, Wirkung heilt Re-Trigger).

Startup: `TriggerStartupService` iteriert die `ITriggerRegistration`-DI-Instanzen und spawnt
sie nach dem `PipelineStartupService`.

## Deadlines / Fristen

- **`Frist`** (`Abstractions/Frist.cs`): durables POCO `(Id, Fällig, ZielAggregatId,
  Kontext)` — bewusst KEIN serialisierter Command, nur primitive Felder + fester
  Zustellungs-Command. **`IFristplan`** = `PlaneAsync`/`FälligeAsync(jetzt)`/`EntferneAsync`.
  **`FristId.FürZustellung`** = deterministische CommandId.
- **`IDbClock`/`MartenDbClock`** (`SELECT now()`): die EINE Zeitquelle für Fälligkeits-
  **Entscheidungen** — nie Node-`DateTime.UtcNow` (Multi-Node-Uhren driften).
- **`FristScheduler`** (Hosted Service, 5 s-Loop): reiner Kern `TickAsync(clock, plan,
  feuere)` — DB-Zeit holen → fällige Fristen laden → je Frist feuern, DANN entfernen
  (at-least-once; Feuer-Fehler lässt die Frist stehen, nächster Tick wiederholt dedup-sicher
  über `FristId.FürZustellung`). `AddDeadlines(baueCommand)` ist opt-in.

**Bezug zu Prozessen — bewusst entkoppelt:** `FristScheduler` ist STANDALONE, keine
ProzessManager-/Marking-Kopplung. Timeout→Kompensation-im-Marking hängt am zurückgestellten
P5b (Marking-Cursor) als künftigem Unterbau. Heute feuert eine Frist nur einen Command auf
ein Ziel-Aggregat (Demo-Ziel: `Domain/Erinnerung/Erinnerung.cs`).

## Monitoring

- **`BackendMetrik(int OffeneProzesse, long DlqEinträge)`** + `IBackendMetrics` — reiner
  Read-Pfad, aggregiert aus `IProzessOffenIndex` + `IDeadLetterReadStore` (kein separater
  Zähler-Zustand).
- **`BackendHealthCheck`** (`IHealthCheck`, Name „backend"): nicht-leere DLQ = **Degraded**,
  Store-Fehler = **Unhealthy**, sonst **Healthy**.
- **`MonitoringExtensions`**: `AddBackendMonitoring()` + `MapBackendMonitoring()` mappt
  **`GET /health`** (JSON: Status + Kennzahlen) und **`GET /monitoring/metrics`**.
- **Offen:** Tracing (Emit-/Wake-Kanten) nicht gebaut.

## Dead-Letter

Zwei getrennte Achsen: **`IDeadLetterSink.WriteAsync`** (schreiben, best-effort, wirft NIE in
den Aufrufer) und **`IDeadLetterReadStore`** (Ops/Read:
`ListAsync`/`ListByCorrelationAsync`/`GetAsync`/`CountAsync`/`ResolveAsync`). `DeadLetter`
ist ein POCO ohne Command-Payload → **kein Auto-Replay** (der OCC-Pfad hat keine
Inbox-Dedup; blinder Retry könnte bei verlorener Quittung doppelt wirken). „Replay" =
Betreiber sieht den Verlust, klärt manuell, löst per `ResolveAsync` auf.

**Drei Schreibstellen:**
1. `AggregateDispatcher` (`Quelle="aggregate-dispatcher"`): Client-/OCC-Command nach
   erschöpften Retries nicht zustellbar.
2. `AggregateActorBase.TryMeldeVergiftetAsync` (`Quelle="aggregate-rehydrate"`): ein
   vergiftetes Aggregat (Applier wirft deterministisch) — EINMAL je Aktivierung durabel
   gemeldet.
3. `ProzessManager` (KlärungNötig): eine Kompensation selbst abgelehnt/unvollziehbar.

Der Pipeline-Emit-Pfad selbst hat KEINE DLQ-Schreibstelle (Timeout wird verworfen,
at-least-once, Re-Wake heilt).

## Offene Punkte

- Transiente Pipeline-Events schlucken Fehler still (per Design verlierbar, aber keine
  Beobachtbarkeit über den Log hinaus).
- `MartenFristplan.FälligeAsync` lädt alle Fälligen ohne Limit/Paging.
- Frist-Feuer eines nicht-routbaren Commands bleibt ewig im Plan (kein Abbruch, kein DLQ).

## Schlüsseldateien

`Infrastructure/Pipeline/PipelineActorBase.cs`, `PipelineEventPullBridge.cs`,
`PipelineTriggerSender.cs`, `TimerTrigger.cs`, `TimerTriggerActor.cs`, `WebhookTrigger.cs`,
`TriggerStartupService.cs`; `Infrastructure/Deadlines/FristScheduler.cs`,
`DeadlineExtensions.cs`; `Infrastructure/Monitoring/BackendMetrics.cs`,
`BackendHealthCheck.cs`, `MonitoringExtensions.cs`;
`Infrastructure/Persistence/MartenFristplan.cs`, `MartenDbClock.cs`,
`MartenDeadLetterSink.cs`, `MartenDeadLetterReadStore.cs`; `Abstractions/Frist.cs`,
`IDbClock.cs`, `IDeadLetterSink.cs`, `IDeadLetterReadStore.cs`; `Core/TriggerRegistration.cs`;
`Infrastructure.SourceGeneration/PipelineActorGenerator.cs`.
