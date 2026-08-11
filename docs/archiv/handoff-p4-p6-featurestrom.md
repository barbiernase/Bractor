# Handoff: P4 + P6 + Feature-Strom (Single-Node)

> **Für einen frischen Agenten.** Self-contained. Der Backend-Neubau ist bis **EM-1** fertig (genau ein
> Emit-Weg, per Analyzer erzwungen). Was bleibt: **P4** (Konsumenten-Maschine vereinheitlichen), **P6**
> (Pipeline zerlegen) und der **Feature-Strom**. **Wir bleiben bewusst SINGLE-NODE** — P7 (Transport-
> Serializer) und P8 (Multi-Node-Tor) sind für diese Ausbaustufe **gestrichen**, nicht anfassen.
>
> Lies dieses Dokument ganz, dann `CLAUDE.md` (oben) und `docs/backend-neubau-fahrplan.md`
> (Abschnitte „Fortschritt", „Phase 4", „Phase 6", „Feature-Strom"). Fasse **nichts** an, bevor du
> Stufe 1 (Verstehen) quittiert und meine Freigabe hast.

---

## 0. Wo wir stehen (fertig & grün, committet + gepusht)

Der ganze Kern des Neubaus steht: **P0** (Verträge), **P1a–c** (kanonischer Kanten-Graph, alles
signaturgetrieben), **P2** (`CommandModus` statt Sentinel), **P3** (Emit-Primitiv `CommandEmitter`),
**P5(a)** (präzises Korrelations-Poll-Routing), **P5 · Treiber-Fold A/B/C** (der Prozess-Manager sendet
fire-and-forget über `CommandEmitter`, keine Quittung mehr; Analyzer A6 erzwingt EM-1).

**Zählerstände: Prüfstand 58/58, Integration 25/25** (sequentiell gegen echtes Postgres/Consul/Redis).
Der einzige bekannte Flake ist `SnapshotLiveE2ETests` (Consul-Cold-Boot, bimodal, prozess-unabhängig —
**NICHT** mit Timeouts härten; `memory/snapshot-e2e-flake-clusterboot.md`).

Kontext/Historie: `docs/handoff-treiber-fold.md` (die gerade abgeschlossene Aufgabe),
`docs/anleitung-prozess-schreiben.md` (die Entwickler-API — reine Domäne),
`docs/zielbild-vereinheitlichte-konsumenten-maschine.md` (Philosophie hinter P4).

---

## 1. Die sechs Invarianten (jede Entscheidung leitet sich hieraus ab)

1. **Die Wahrheit ist der Log.** Ordnung/Vollständigkeit/Wiederholbarkeit kommen NUR aus dem Store-Read.
2. **Das Signal ist nur ein Weckruf** — trägt nur `(StreamId, Version)`, darf verloren/doppelt/ungeordnet sein.
3. **Routing über Typen** — nie ein handgebauter Identitäts-String.
4. **Keine Runtime-Reflection.** Kein `Activator.CreateInstance`, kein `MethodInfo.Invoke`, kein Assembly-Scan
   im Laufzeitpfad. **Neue Dispatch-Logik = Generator erweitern, nie ein Handschalter.**
5. **Der Fachcode bleibt rein.** Cursor, Signal, Ordnung, Exactly-once, Sharding, Prozess-Maschinerie tauchen
   im Entwickler-Code NIE auf.
6. **Persistent genau dann, wenn ein durabler Konsument abhängt.** Verlierbares (Tick, UI-Feedback,
   Datei-Trigger) bleibt auf dem schnellen Kanal.

**Der Glue ist generiert (nachgewiesen):** 15 Generatoren erzeugen Actors, Command-Routing, Prozess-Registry,
Signale, Pull-Pfade, DTOs (`AggregateActors.g.cs`, `GeneratedCommandRouting.g.cs`, `GeneratedProzessRegeln.g.cs`,
`GeneratedPullPaths.g.cs`, …). **Jede neue Verdrahtung, die du brauchst, MUSS auch generiert werden — nicht
von Hand getippt.** Das ist die tragende Anforderung dieser Aufgabe.

---

## 2. Das aktuelle Ziel (worauf P4/P6 hinauslaufen)

Es gibt heute vier durable Konsumenten, die im Kern DASSELBE tun (Spec „eine Maschine"):
**lese ab Marke, falte, dispatch, rücke Marke vor**. Sie laufen aber auf **verschiedenen** Trägern:

| Konsument | Träger heute | Achse A | Achse B | Marke/Cursor |
|---|---|---|---|---|
| **Projektion** | `ProjectionAdapter` (+ `SignalAdapterActor` pro Stream) | Ein-Strom | Replaybar | `IProjectionTracker` (Co-Commit, `Reset`) |
| **Reaktion** | derselbe Pull-Adapter, emittiert via `HandlerOutputRouter`→`CommandEmitter` | Ein-Strom | Emittierend | (heute tracker-los, liest ab 0) |
| **Prozess** | `ProzessManager` (eigene Fold-Maschine) | Korrelations-Multistrom | Emittierend | (Marking gefaltet, `ProzessOffenIndex`) |
| **Pipeline** | `PipelineActorBase` — **noch PUSH** (BrokerSubscription) | Ein-Strom | Emittierend | Version-Cache (kein durabler Cursor) |

**P4** macht daraus **eine** parametrisierte Maschinen-Klasse über **zwei Achsen**
(A: Ein-Strom | Korrelations-Multistrom, B: Replaybar | Emittierend) und zieht daraus den **GA-1-Schnitt**:
`IReplaybarerTracker` (mit `Reset`) vs. `IEmittentenCursor` (**ohne** `Reset`) — beide Verträge liegen schon
in `Abstractions/` (P0), aber der Compile-Zeit-Schnitt ist NOCH nicht verdrahtet.

**P6** holt die Pipeline auf diese Maschine (Event-Pfad wird ein emittierender Ein-Strom-Konsument) und macht
den Trigger-Ingress zu einem dünnen Push-Adapter über das Emit-Primitiv.

Danach ist die „eine Maschine" real: **keine Taxonomie, kein zweiter Marker, kein Push-Event-Zweig** außer im
Signal-Receiver.

---

## 3. STUFE 1 — VERSTEHEN (kein Produktivcode)

Lies in dieser Reihenfolge und öffne die echten Dateien (nicht raten):

**Die Konsumenten-Maschine & Marken (P4):**
- `Infrastructure/Projections/ProjectionAdapter.cs` — die 7.3-Schleife (lese ab Marke → dispatch → Marke vor).
  Der `IProjectionTracker?` ist optional; `null` → liest ab 0 (der Reaktions-Fall heute).
- `Infrastructure/Projections/PullPath.cs` — `PullPathRegistration` + `GenericPullStartupService` (spawnt
  Receiver + Poller je registriertem Pfad; kennt keinen Domänentyp).
- `Infrastructure/Projections/SignalAdapterActor.cs` — der per-Stream-Cluster-Actor (Ein-Strom, Achse A links).
- `Infrastructure.SourceGeneration/PullPathGenerator.cs` — erzeugt `GeneratedPullPaths` +
  `PushSubscriberExclusions` aus jedem `IPullSubscriber`.
- `Abstractions/IProjectionTracker.cs`, `Abstractions/IReplaybarerTracker.cs`, `Abstractions/IEmittentenCursor.cs`
  — die drei Marken-/Cursor-Verträge (Achse B). **`IEmittentenCursor` trägt bewusst KEIN `Reset`.**
- `Abstractions/Interfaces.cs` (`ISubscriber`), `Abstractions/IPullSubscriber.cs` — die Entwickler-Marker
  (die API, die UNVERÄNDERT bleiben MUSS).
- `Infrastructure/Prozess/ProzessManager.cs` — die Multistrom-/emittierende Fold-Maschine (Achse A rechts,
  Achse B emittierend). **Optionale Folge-Konsolidierung, KEIN Teil des P4-Tors** — der Manager kann später
  auf die P4-Klasse wandern; erst mal bleibt er.

**Die Pipeline (P6):**
- `Infrastructure/Pipeline/PipelineActorBase.cs` — Kanal 0 (Self/`ScheduleSelf`, bleibt lokal), Kanal 1
  (Trigger, `IPipelineTrigger`; `SendTriggerAsync` nutzt noch `CancellationToken.None` → Zeile ~394, das
  bewusst zurückgestellte P6-Item), Kanal 2 (Event via `BrokerSubscription` → **der PUSH-Event-Zweig, den P6
  auflöst**). Command-Send läuft schon über `CommandEmitter` (P3).
- `Infrastructure/Pipeline/PipelineStartupService.cs`, `TriggerStartupService.cs`, `PipelineActorGenerator.cs`.
- `Abstractions/Interfaces.cs` (`IPipelineHandler`, `IPipelineTrigger`).

**GA-1 (der Kern-Gewinn von P4):**
- Heute ist `IProjectionTracker` optional — eine append-artige Projektion OHNE Co-Commit-Tracker läuft (liest
  ab 0, re-emittiert) und ist NUR gültig, wenn ihr Effekt idempotent ist. P4 macht das zu einem **Build-Fehler**:
  eine append-artige (replaybare) Projektion, die keinen `IReplaybarerTracker` co-committet, DARF nicht booten.
  Referenz-Ziel `Domain.Infrastructure/ImagePairHistorieStore.cs` (das scharfe append-artige Read-Model).

**Quittung (max. 12 Zeilen):** (a) welche vier Konsumenten heute auf welchem Träger laufen; (b) wie die zwei
Achsen (A × B) die vier Fälle aufspannen; (c) wo genau der GA-1-Schnitt greift (welcher Bau-/DI-Check bricht
eine tracker-lose Append-Projektion); (d) was P6 aus der Pipeline herauslöst (Kanal 2) und was bleibt (Kanal 0);
(e) wie du sicherstellst, dass die neue Kind-/Marker-Verdrahtung **generiert** wird (welcher Generator).
**Dann STOPP und warte auf Freigabe.**

---

## 4. STUFE 2 — UMSETZEN (erst nach Freigabe), in bewiesenen Scheiben

**Reihenfolge & Abhängigkeiten:** P6 hängt an P4 (die Pipeline-Event-Kante wird ein Konsument der P4-Maschine).
Feature-Strom-Elemente hängen nur an P3 (fertig) — sie sind **jederzeit** baubar und ein guter erster Gewinn,
falls du vor dem riskanten P4-Schnitt Vertrauen aufbauen willst.

### P4 — Konsumenten-Maschinenklasse (zwei Achsen) · GA-1
Riskanter Schnitt → **Ausprägung für Ausprägung** migrieren, nie big-bang. Vorgeschlagene Scheiben:
- **P4.a — Achsen benennen & Ein-Strom vereinheitlichen:** die Lese-Falt-Emit-Schleife über Achse A
  (Ein-Strom | Multistrom) × Achse B (Replaybar | Emittierend) parametrisieren; Projektion **und** Reaktion
  (beide Ein-Strom) auf DIESELBE Maschinen-Klasse ziehen. Marker-API unverändert.
- **P4.b — GA-1-Build-/DI-Check:** eine append-artige (replaybare) Projektion ohne co-committeten
  `IReplaybarerTracker` bricht den Build/Boot (Analyzer ODER DI-Registrierungs-Check — die Analyzer-Harness
  aus A6 existiert jetzt, siehe `Infrastructure.SourceGeneration/CommandEmitAnalyzer.cs` +
  `Infrastructure.Pruefstand.Tests/Analyzers/CommandEmitAnalyzerTests.cs` als Vorlage). `Reset` NUR bei
  `IReplaybarerTracker` (Compile-Zeit-Schnitt gegen `IEmittentenCursor`).
- **P4.c (optional) — Prozess-Manager auf die Klasse:** Folge-Konsolidierung, KEIN Teil des Tors.

**Tor P4 (Ebene 1+2):** alle Ein-Strom-Konsumenten auf derselben Maschinen-Klasse; `Reset` compile-zeit nur bei
Replaybar; der GA-1-Check bricht eine bewusst tracker-lose Append-Projektion (beweisen wie A6: Probe → Build-
Fehler → entfernen); Projektions-/Reaktions-E2E grün (`LiveCommandE2ETests`, `ReaktionE2ETests`).

### P6 — Pipeline ehrlich zerlegen (hängt an P4)
- **P6.a — Event-Pfad (Kanal 2) falten:** die `BrokerSubscription`-Event-Kante der Pipeline als
  emittierenden Ein-Strom-Konsumenten auf die P4-Maschine ziehen (wie eine Reaktion). Danach kein
  `BrokerSubscription`-Event-Abo mehr außer im Signal-Receiver.
- **P6.b — Trigger-Ingress (Kanal 1) als dünner Push-Adapter über das Emit-Primitiv;** `SendTriggerAsync`-
  `CancellationToken.None` (Zeile ~394) beseitigen; `ScheduleSelf`/Self-Messages bleiben lokal (Kanal 0).
- **P6.c — `IPipelineTrigger`/Timer/Webhook-Registrierung verdrahten** (überlappt mit dem Feature-Strom).

**Tor P6 (Ebene 1+2):** kein `BrokerSubscription`-Event-Abo außer im Signal-Receiver; die drei Domänen-
Pipelines (`Benchmark`, `FileWatch`, `ImageProcessing`) laufen **unverändert** über ihre Handler-API.

### Feature-Strom (orthogonal, hängt nur an P3 — single-node-relevant)
- **DLQ-Replay** (Ops-/Read-Pfad auf `dlq`; die DLQ-Senke `IDeadLetterSink` schreibt schon).
- **Timer/Webhook-Trigger** + `ITriggerRegistration` verdrahten (Trigger-Ingress bleibt Push, Invariante 6).
- **Projektions-Rebuild-Runner** (Vertrag + `Reset` existieren; nach P4 leicht — EINE Rebuild-Schleife).
- **Deadlines/Timeouts** (nach stabilem Prozessmodell — Timer-Token/Zeit-Event).
- **Prozess-Verkettung** (Modell ok; braucht Test/Beispiel).
- **Monitoring** (Metrics/Tracing/HealthChecks/Prozess-Sicht; profitiert von der uniformen Maschine).

**Pro Scheibe: Tor benennen → umsetzen → beweisen → Ergebnis melden, dann nächste vorschlagen.**

---

## 5. Guardrails (halten — nicht verletzen)

- **Single-Node.** P7 (Poly-Serializer) + P8 (Multi-Node-Tor) sind gestrichen. Das System ist de-facto
  single-node (kein Cross-Node-Serializer für interne Nachrichten) — das ist OK und Absicht. Baue nichts für
  Multi-Node.
- **Die sechs Invarianten** (oben). Kein Runtime-Reflection (Inv. 4) → **neue Verdrahtung = Generator, nie
  von Hand getippt.** Das ist der ausdrückliche Wunsch: der Glue MUSS generiert werden.
- **Die Entwickler-API bleibt unverändert:** `ISubscriber`+`Handle`, `IPipelineHandler`+`Handle`,
  `IProzessDefinition`. Unterschiede fallen aus Ctor-Stores + Rückgabetypen, nicht aus einer Taxonomie
  (kein zweiter Marker, kein „Projektion-vs-Reaktion"-Zweig).
- **Idempotenz ist NIE Default.** Der Normalfall ist der nicht-idempotente Effekt, Co-Commit der allgemeine
  Mechanismus. GA-1 macht ein fehlendes Co-Commit zum Build-Fehler.
- **Verteilte/Actor-Hangs ZUERST in-memory beweisen** (Fake-Cluster/Fake-Emit im Prüfstand), nie im langsamen
  Integrationstest raten — xUnit versteckt die Host-Logs (`memory/hang-diagnose-in-memory.md`).
- **Kein In-Memory-Event-Store.** Der `InMemoryEventStore` + Testing-Doubles sind gelöscht — Store-Semantik
  (OCC, Co-Commit-Atomizität) NUR gegen echtes Marten (Ebene 2/3); Ebene 1 testet store-freie Logik
  (`memory/kein-inmemory-store.md`).
- **Integrationstests IMMER sequentiell** (`Infrastructure.Integration.Tests/xunit.runner.json`, Parallelität
  aus). Den `SnapshotLiveE2ETests`-Cold-Boot-Flake als bekannt akzeptieren — NICHT mit Timeouts härten.
- **Bei neuen nicht-internen Domain-Typen Proto regenerieren:** `dotnet run --project Proto.SourceGeneration`
  → `dotnet build ProtoRepo` → `dotnet build Infrastructure`. Interne Marken (`IProzessIntern`) sind
  ausgeschlossen (kein DTO nötig).
- **Docs synchron halten:** `CLAUDE.md` (oben) + `docs/backend-neubau-fahrplan.md` („Fortschritt") nach jeder
  Scheibe.
- **Git:** Commits in diesem Repo **OHNE** `Co-Authored-By`-Zeile; Commit/Push nur auf ausdrückliche
  Aufforderung (`memory/commit-ohne-co-authored-by.md`).

---

## 6. Build / Test / Beweise

- **Build:** `dotnet build`. **Framework-Fehler NUR in `Domain.Client` ignorieren** — bekannter,
  umbau-unabhängiger `_publish`-Bruch (`NavigationViewModel`/`ChartViewModel`).
- **Logik (immer grün, in-memory):** `dotnet test Infrastructure.Pruefstand.Tests` — Ziel bleibt ≥ 58/58.
- **Integration (echtes Postgres/Consul/Redis, SEQUENTIELL):** `dotnet test Infrastructure.Integration.Tests`
  — Ziel bleibt 25/25 (SnapshotLive-Flake ausgenommen). Infra hoch:
  `docker compose -f deploy-linux/docker-compose.infrastructure.yml up -d` (bzw. Container starten;
  Consul-Leader prüfen: `curl -s http://localhost:8500/v1/status/leader`).
- **Analyzer-Beweis-Muster (für GA-1 wiederverwendbar):** eine temporäre Probe-Datei erzeugt den erwarteten
  Build-Fehler, dann löschen → 0 Fehler; zusätzlich ein durabler Harness-Test
  (`CommandEmitAnalyzerTests` als Vorlage, eigene `CSharpCompilation`).
- **Host-Boot als Rauch-Test:** `Host_Grpc` bootet mit gRPC + Cluster + Pull-Pfaden + Prozessen, 0 Exceptions.

**Tor-Kennzahl gesamt:** Prüfstand ≥ 58, Integration 25 (+ neue Tests je Scheibe). Kein Rückschritt in der
Saga-/Projektions-/Reaktions-Suite (der schärfste No-Regression-Check).

---

## 7. Danach

Nach P4 + P6 + den gewählten Feature-Strom-Elementen ist die „eine Maschine" real und die single-node-
Ausbaustufe funktional komplett. Multi-Node (P7/P8) bleibt bewusst offen — erst wenn ein echter Zweit-Node-
Bedarf besteht.
