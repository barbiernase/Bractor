# Backend-Analyse — 2026-08-11

> Datierter Gesamtbefund über das komplette Backend, code-verifiziert am Commit-Stand
> `4e5b06c` (main). Erhoben aus acht parallelen Subsystem-Analysen (Code + Commits +
> Docs) plus Bodenprobe (Build + Prüfstand-Tests). Dieses Dokument ist eine
> **Momentaufnahme** — die lebende Referenz ist `docs/architektur/`, der lebende
> Fahrplan `docs/backend-neubau-fahrplan.md`.

## Executive Summary

Der Kern des Frameworks — **Schreibseite, Konsum-Maschine, Prozess-Maschine** — ist
funktional fertig und in sich konsistent. Der Code ist an mehreren Stellen **deutlich
weiter als die Doku** (Feature-Strom, Schreibpfad-Perf, Snapshots, Deadlines sind
geliefert, aber in CLAUDE.md noch als „offen"/„NOCH NICHT" geführt). Der eine große
strukturell offene Block ist **Cross-Node/Multi-Node** (de facto single-node, weil kein
Serializer für den internen Plane registriert ist).

**Bodenprobe (gemessen):**
- Build der Backend-Kette: grün. (Nur `Domain.Client`, die Blazor-Frontend-Schicht,
  bricht mit 13 `_publish`-Fehlern — bekannter, laufender Client-Generator-Umbau,
  nicht Teil der Backend-Kette.)
- **Prüfstand (in-memory): 99/99 grün.** Integration (gegen echtes Marten/Consul/Redis):
  laut Feature-Strom-Handoff 33/33. Die in den Docs kursierenden Zahlen (54/58/73/91)
  sind veraltete Zwischenstände.

---

## Die tragende Architektur

**„Vier Konsumenten, eine Maschine."** Projektion, Reaktion, Prozess und Pipeline sind
vier durable Konsumenten, die **dieselbe** store-agnostische Pull-/Signal-Schleife
(`ProjectionAdapter`) nutzen. Kein zweiter Marker, keine Taxonomie — der Unterschied
fällt aus den **Ctor-Stores + Rückgabetypen**:

| Achse | Ausprägung | Erzwingung |
|---|---|---|
| **B — Replaybar vs. Emittierend** | Projektion hat `IProjectionTracker` (Co-Commit + Reset); Reaktion/Prozess/Pipeline hat `IEmittentenCursor` (best-effort, **kein** Reset, weil Replay echtes Geld bewegt) | Beide gesetzt → `InvalidOperationException` im Adapter-Ctor |
| **Transport** | Signal (schnell, verlierbar) + Poll (30 s, Sicherheit) wecken **dieselbe** Cluster-Identität | — |
| **Emit** | **Genau ein** Command-Emit-Weg über `CommandEmitter` (deterministische Id, bounded Token) | Roslyn-Analyzer **CQRS020/021** = Build-Fehler bei jedem anderen Weg |

Die sechs Invarianten (Log ist Wahrheit; Signal ist nur Weckruf; Routing über Typen;
keine Runtime-Reflection; Fachcode bleibt rein; persistent nur bei durablem Konsumenten)
sind durchgehend eingehalten und in den Verträgen strukturell verankert.

---

## Reifegrad je Subsystem

### 1. Schreibseite (Command → Append) — ✅ reif + Perf-Ausbau
- **`CommandModus { Client(int) | Emittiert }`** (geschlossener Summentyp) ersetzt den
  alten `AnyVersion = -1`-Sentinel; `required Modus` killt den Footgun-Default. Der
  Actor dispatcht exhaustiv per `switch`.
- **Version PRO Event** (`EventEnvelopeFactory.BuildPerEvent`), Metadaten
  (CorrelationId/CausationId/`aggregate_type`) im Log.
- **Framework-Inbox (exactly-once, Zwei-Mengen):** `_verarbeiteteCommandIds` +
  `_abgelehnteCommandIds` (beide `BoundedInbox`, Cap 10 000), aus co-committeten Marken
  `KommandoVerarbeitet`/`KommandoAbgelehnt` (`IProzessIntern`) gefaltet. Re-Delivery
  eines abgelehnten Vorgangs bleibt konsistent `Success:false`.
- **Post-Append-Härtung:** Ab dem committeten Append läuft alles Weitere (Applier,
  Version-Track, Snapshot, Publish) in einem inneren try → wirft es, wird `Success=true`
  geantwortet und der State frisch rehydriert (kein vergifteter Actor, keine
  Doppel-Kompensation).
- **Group-Commit-Batching:** `BatchingEventAppender` (node-lokal) bündelt Appends in
  einen Channel; **paralleler Commit-Drain** (Default K=4) → **+48 % Durchsatz**
  (3249 → 4805 msg/s). Sicher durch Proto.Cluster Single-Activation.
- **Snapshots:** voll verdrahtet (`MartenSnapshotStore`, Threshold 200, out-of-band,
  Zwei-Mengen-Inbox im Snapshot), abgeleitet/nicht-autoritativ (Miss → Voll-Replay).
- **Version-Index (Redis)** ist optional (`EnableVersionTracking` / `NullVersionTracker`),
  abgeleitet, best-effort.
- **STJ-Serializer** (`EventJsonGenerator` + `EventJsonSerializerContext`): reflection-frei,
  opt-in (Default AUS), Baustein für einen künftigen COPY-Event-Store — perf-neutral bei
  aktueller Last, bewusst zurückgestellt.

### 2. Konsum-/Pull-Maschine — ✅ reif (P4)
- **Eine Schleife** (`ProjectionAdapter`): Marke lesen → geordnet ab Marke+1 → Guard →
  dispatch → Marke vorrücken. Keine Transaktion im Adapter (die gehört dem Store).
- **Per-Stream-Cluster-Actor** (`SignalAdapterActor`, Identität = StreamId) serialisiert
  Signal- und Poll-Weckung → kein Race.
- **Signalverlust heilt:** Coalescing fängt Duplikate/Verlust im Betrieb; das letzte
  verlorene Signal vor Stille fängt der Poll (durabler Poll-Cursor, kein Re-Scan der
  Historie je Boot).
- **Co-Commit** entscheidet der Store: Effekt + Marke in EINER Transaktion →
  exactly-once; getrennt → at-least-once (Handler idempotent).
- **GA-1-Boot-Guard** (`GaEinsPruefung`): append-artige Projektion (`IAppendProjektion`)
  ohne Co-Commit-Tracker bricht beim Boot, statt still doppelt zu appenden.
- **`ProjectionRebuilder`:** Replay IST der Adapter-Pfad mit Cursor = −1; nur replaybare
  Konsumenten (mit Reset) sind rebuildbar, Emittenten strukturell nicht.

### 3. Prozess-Maschine (Event-Regel-DAG) — ✅ funktional komplett
- **Petri-Netz:** Events = Tokens, Commands = Transitionen. Ein Prozess ist eine
  `IProzessDefinition` mit typisierten Regeln
  (`Prozess<TAuslöser>.Definiere(p => p.Auf<E>().Und<E2>().Sende<Cmd>(…).RückgängigDurch(…))`).
- **Ein generischer `ProzessManager`** verschmilzt Aggregat + Treiber. Log speichert nur
  Entscheidungen (`ProzessGestartet`/`SchrittGescheitert`/`ProzessBeendet`); das Marking
  wird bei **jeder Weckung** aus den Ziel-Streams gefaltet (Ergebnis ↔ Transition per
  `CausationId == Vorgang`).
- **EM-1 durchgezogen:** feuert fire-and-forget über `CommandEmitter`, **keine Quittung
  mehr**; Fehlschlag-Erkennung trägt allein die Fold-Achse `AbgelehntDa`.
- **Korrelation** reist als `CommandEnvelope.CorrelationId` → Ziel-Event-Metadatum →
  `KorrelationsRouter`; präzises Poll-Routing (`ProzessPollFilter.SollRouten`) +
  `ProzessOffenIndex`-Backstop + Selbst-Weckung (bewusstes Doppelnetz, Auflage A2).
- **Azyklizität:** aktiver Boot-Guard (`ProzessAzyklizität.PrüfeAlle`), gespeist aus der
  präzisen `GeneratedCommandRouting.Produziert` (Decider-OneOf-Rückgaben).
- **Belegte Muster:** Diamant (Bestell-/Reise-Saga, parallele Zweige + Join +
  Kompensation), Fan-out (`SendeJe` + Count-Join `UndAlle<E>(n)`), Verkettung (Ende A
  startet B über ein persistiertes Domänen-Event).

### 4. Feature-Strom (frisch, 10.08) — ✅ geliefert, teils entkoppelt
- **Trigger:** Timer (`TimerTriggerActor`, `ReenterAfter`-Loop) + Webhook
  (`MapPipelineWebhook`, HTTP→Trigger, 202 Accepted). Bewusst PUSH/verlierbar
  (Invariante 6).
- **Deadlines/Fristen:** `Frist`-Primitiv + `FristScheduler` (DB-Uhr `IDbClock`
  entscheidet Fälligkeit), feuert deterministisch über `CommandEmitter`. **Bewusst
  standalone** — noch nicht in die Prozess-Marking-Schicht integriert (hängt an P5b).
- **Monitoring:** `GET /health` (Healthy/Degraded/Unhealthy) + `GET /monitoring/metrics`
  (offene Prozesse, DLQ-Einträge); reiner Read-Pfad aus bestehenden Quellen.
- **Dead-Letter:** `IDeadLetterSink` (schreiben, best-effort) + `IDeadLetterReadStore`
  (Ops/Read). Drei Schreibstellen: Dispatcher-Retry erschöpft, vergiftetes Aggregat,
  Prozess-KlärungNötig. Bewusst KEINE Payload → kein Auto-Replay.
- **Pipeline P6.1/P6.2:** Actor entrümpelt (toter OCC-Ballast weg, Trigger-Kante
  gebändigt); persistierter Event-Pfad auf die Pull-Maschine gefaltet
  (`PipelineEventPullBridge`), transiente Events bleiben auf Push.

### 5. Generatoren & Analyzer — ✅ reif
- **9 Generatoren + 1 Analyzer** auf Infrastructure (Symbol-Ebene, sieht Domain-Generat),
  **11 Generatoren** auf Domain (Syntax-Ebene, sieht fremdes Generat nicht).
- **`GeneratedCommandRouting`** (aus Decider-OneOf-Signaturen) ist die einzige
  Routing-Wahrheit; `GeneratedPipelines.CommandAggregateTypes` ist nur Passthrough
  darauf.
- **Diagnostik-Codes:** CQRS001/002/003 (Prozess-Regeln), CQRS010/011/012 (Routing- /
  Identitäts-Kollision), CQRS020/021 (EM-1-Erzwingung).
- **Proto.SourceGeneration** ist kein Roslyn-Generator, sondern ein manuell auszuführendes
  Konsolen-Tool (`dotnet run`), das `ProtoRepo/domain.proto` schreibt.

### 6. Domäne (Testvehikel) — ✅ rein
- **16 `IState`-Aggregate + 6 Prozess-Definitionen.** Ziel-/Auslöser-Aggregate sind
  strikt rein (kein `Vorgang`-Feld, keine Dedup-Menge, mengenbasierte Reservierungen);
  Idempotenz sichert die Framework-Inbox.
- **Ein Alt-Leak:** `Domain/Reaktion/Reaktionsempfaenger.cs` trägt noch eine
  Domänen-Dedup-Menge `VerarbeiteteReaktionen` (altes Phase-3-Muster, bewusste Schuld).

### Cross-Node / Multi-Node — ❌ nicht erreicht
Kein registrierter (poly-)Serializer für den internen Plane (Wake/WakeAck/SignalEnvelope/
CommandEnvelope/EventEnvelope) → **de facto single-node** (rohe CLR-Objekte laufen nur
in-process). Das Multi-Node-Tor (P4c/P4d) ist der größte strukturell offene Block.

---

## Entwicklungs-Zeitachse

Die eigentliche Framework-Arbeit ist in wenige Tage komprimiert (Phasen 0–4 leben nur in
den Docs, nicht in granularen Commits):

| Datum | Block | Kern |
|---|---|---|
| 05–06 | Bootstrap/Deployment | ImagePair-Domäne, Pipeline-Grundgerüst, Deploy |
| **08.08** | EM-1 / Treiber-Fold (`8f1fd1e`) | Sentinel → `CommandModus`; das eine Emit-Primitiv; Analyzer erzwingt EM-1 |
| **09.08** | P4 + P6 | Konsumenten-Maschine vereinheitlicht (Achse B, GA-1, EmittentenCursor); Pipeline entrümpelt + Event-Pfad auf Pull |
| **10.08** | Feature-Strom | Rebuild-Runner, DLQ, Timer, Verkettung, Webhook, Monitoring, Deadlines (6 Scheiben) |
| **10.08** | Schreibpfad-Perf | Group-Commit + paralleler Drain (+48 %), STJ-Serializer opt-in, Version-Index optional |

---

## Offene Baustellen (priorisiert)

1. **Cross-Node-Serialisierung** (P4c/d) — der eine große strukturelle Block; alles läuft
   sonst single-node.
2. **P5b Marking-Cursor** — `ProzessManager.FaltMarkingAsync` liest bei jeder Weckung jeden
   Ziel-Stream ab 0 → **O(N²)**; bewusst zurückgestellte Optimierung
   (`docs/prozess-marking-cursor-konzept.md`, `docs/p5b-marking-cursor-handoff.md`).
3. **Schreibpfad-Perf** — der parallele Drain skaliert sublinear; die Postgres-`wait_event`-
   Ursache (WAL-Insert/Sequence) ist noch nicht aufgelöst
   (`docs/naechster-agent-prompt-schreibpfad-perf.md`).
4. **KlärungNötig-Pfad** — korrekt-per-Konstruktion (Kompensation selbst abgelehnt →
   `ProzessBeendet(KlärungNötig)` + DLQ), aber **null Testdeckung**.
5. **Kleinere Schulden:**
   - `DtoMapperGenerator` aufgebläht/fragil (~1325 Z., hartkodierte Domänen-Enums
     `Klassifikation`/`BildVersion`, Encoding-Schäden in Kommentaren, tote if-Zweige).
   - `Reaktionsempfaenger`-Dedup-Menge (Domänen-Leak, unbegrenztes Wachstum) — auf die
     Framework-Inbox migrierbar.
   - Deadline-Primitiv existiert, wird aber von **keinem** Prozess ausgelöst (kein
     DSL-Verb `NachFrist`/`MitFrist`, kein Beispiel-Prozess).
   - `CqrsFrameworkOptions` toter `[Obsolete]`-Typ; `IAggregateRepository`/
     `IAggregateMessenger` ungenutzte Legacy-Verträge; `Interfaces.cs` überladene
     Sammel-Datei.
   - `CancellationToken.None` auf den generierten Emit-Factory-Pfaden
     (`PullPathGenerator`, `PipelineActorGenerator` `{Name}EventPullKind`) — vom
     CQRS021-Analyzer nicht erfasst (anderer Rückgabetyp), Emit selbst ist gebounded.

---

## Doku-Befund (Ausgangslage vor der Neuaufstellung)

- **CLAUDE.md intern widersprüchlich/veraltet:** Fortschrittsblock endet bei
  „EM-1/Treiber-Fold C", nennt P4/P6/Feature-Strom noch als *nächste* Schritte (alle
  geliefert); „Aktueller Scope" führt Deadlines/Verkettung/Snapshots unter „NOCH NICHT"
  (existieren); Trigger-`None` an einer Stelle „offen", an anderer „✅".
- **Test-Zahlen driften** durch jedes Dokument (54/58/73/91/93 …). Echt: Prüfstand 99,
  Integration 33.
- **Fragmentierung:** 7 Handoff-/„nächster-Agent"-Prompts (6 erledigt), 4 `wurzel-1-*`-Docs
  zu einem nie implementierten Outbox-Thema, 3 große Herleitungs-Docs mit überlappender
  Philosophie, 2 konkurrierende Gesamtpläne, `spezifikation.md` innerlich gespalten
  (Kap. 1–9 gültig, Kap. 10–15 durch den Prozess-Neubau überholt).
- **DSL-Drift aufgelöst:** `anleitung-prozess-schreiben.md` zeigt untypisiertes
  `.Sende(...)`; gültig (und via CQRS003 erzwungen) ist **typisiert `.Sende<Cmd>(...)`**.
- **Toter Testpfad-Verweis:** mehrere Docs verweisen auf
  `Infrastructure.Pruefstand.Tests/Phase5/BestellSagaTests.cs` — existiert nicht (nur
  `BestellSagaE2ETests.cs` auf Integrationsebene).

---

## Anhang — Schlüsseldateien nach Subsystem

**Verträge (`Abstractions/`):** `CommandModus.cs`, `CommandEnvelope.cs`, `IEventEnvelope.cs`,
`ICommandEmitter.cs`, `IProjectionTracker.cs`, `IReplaybarerTracker.cs`,
`IEmittentenCursor.cs`, `Frist.cs`, `IDbClock.cs`, `IDeadLetterSink.cs`/
`IDeadLetterReadStore.cs`, `Snapshot.cs`, `Prozess/ProzessBuilder.cs`,
`Prozess/ProzessRegeln.cs`, `Prozess/ProzessAzyklizitaet.cs`, `ProzessId.cs`,
`IStateChangeSignal.cs`, `IAppendProjektion.cs`, `IdentitaetsAttribute.cs`,
`IPullSubscriber.cs`.

**Schreibseite (`Infrastructure/`):** `Aggregate/ActorSystem/AggregateActorBase.cs`,
`Aggregate/AggregateRehydrator.cs`, `Aggregate/BoundedInbox.cs`,
`Aggregate/KommandoVerarbeitet.cs`, `Aggregate/EventEnvelopeFactory.cs`,
`Persistence/MartenEventStore.cs`, `Persistence/BatchingEventAppender.cs`,
`Persistence/MartenEventBatchWriter.cs`, `Persistence/RedisVersionTracker.cs`/
`NullVersionTracker.cs`, `Persistence/MartenSnapshotStore.cs`,
`Serialization/EventJsonSerializerContext.cs`.

**Konsum-Maschine (`Infrastructure/Projections/`, `PubSub/`):** `ProjectionAdapter.cs`,
`SignalAdapterActor.cs`, `PullPath.cs`, `SignalReceiver.cs`, `Poller.cs`,
`ProjectionRebuilder.cs`, `GaEinsPruefung.cs`, `PubSub/HandlerOutputRouter.cs`,
`PubSub/DetachedEmit.cs`, `PubSub/CommandEmitter.cs`,
`Persistence/MartenEmittentenCursor.cs`, `Persistence/MartenPollCursorStore.cs`.

**Prozess-Maschine (`Infrastructure/Prozess/`):** `ProzessManager.cs`,
`ProzessManagerActor.cs`, `ProzessManagerWiring.cs`, `ProzessManagerEvents.cs`,
`DetachedProzessSend.cs`, `KorrelationsRouter.cs`, `ProzessPollFilter.cs`,
`Persistence/MartenProzessOffenIndex.cs`.

**Feature-Strom (`Infrastructure/`):** `Pipeline/PipelineActorBase.cs`,
`Pipeline/PipelineEventPullBridge.cs`, `Pipeline/PipelineTriggerSender.cs`,
`Pipeline/TimerTrigger.cs`/`TimerTriggerActor.cs`, `Pipeline/WebhookTrigger.cs`,
`Deadlines/FristScheduler.cs`, `Persistence/MartenFristplan.cs`/`MartenDbClock.cs`,
`Monitoring/BackendHealthCheck.cs`/`BackendMetrics.cs`/`MonitoringExtensions.cs`,
`Persistence/MartenDeadLetterSink.cs`/`MartenDeadLetterReadStore.cs`.

**Generatoren:** `Infrastructure.SourceGeneration/CommandEmitAnalyzer.cs`,
`CommandAggregateMapGenerator.cs`, `EventJsonGenerator.cs`, `PipelineActorGenerator.cs`,
`PullPathGenerator.cs`, `SignalFactoryGenerator.cs`, `AggregateActorGenerator.cs`,
`TypeRegistryGenerator.cs`, `SnapshotRegistrationGenerator.cs`, `DtoMapperGenerator.cs`;
`Domain.SourceGeneration/ProzessRegelnGenerator.cs`, `SignalTypeGenerator.cs`,
`SubscriberDispatchGenerator.cs`, `PipelineDispatchGenerator.cs`,
`AggregateHandlerGenerator.cs`.

**Domäne (Beispiele):** `Domain/Bestellung/BestellProzess.cs` (Diamant),
`Domain/Reiseauftrag/ReiseProzess.cs` (Diamant), `Domain/Ueberweisung/` (linear + Join +
Datenfluss), `Domain/Sammelueberweisung/SammelueberweisungsProzess.cs` (Fan-out),
`Domain/Vorgang/{Genehmigungs,Aktivierungs}Prozess.cs` (Verkettung); Ziel-Aggregate
`Domain/{Konto,Lager,Zahlung,Versand,Flug,Hotel,Reise,Vorgang,Erinnerung}/`.
