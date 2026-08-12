# 11 — Feature-Inventar

Vollständige Aufstellung der Fähigkeiten mit Reifegrad. Legende: 🟢 fertig & belegt · 🟡 fertig
mit Schulden/Einschränkung · 🟠 teilweise / im Umbau · 🔴 blockiert/unfertig.

## 11.1 Schreibseite (Write-Path)

| Feature | Status | Beleg / Anmerkung |
|---|:--:|---|
| Aggregat-Modell (State/Decider/Applier, reiner Fachcode) | 🟢 | generiert; [03](03-schreibseite.md) |
| `OneOf<…>`-Decider-Kontrakt (Compile-Zeit-erzwungen) | 🟢 | `Abstractions/OneOf.cs` |
| Optimistic Concurrency (OCC) | 🟢 | Marten-Version + `ConcurrencyException` |
| Framework-Inbox / Idempotenz (`KommandoVerarbeitet`/`KommandoAbgelehnt`) | 🟢 | `IProzessIntern`-Marken |
| Ablehnungen als transiente Events + Targeted Delivery | 🟢 | `ITransientEvent`, `OriginSessionId` |
| Group-Commit-Batching + paralleler Drain | 🟢 | +48 % gemessen; skaliert sublinear |
| Snapshots (nicht-autoritativ, FNV-Struktur-Hash-Version) | 🟢 | `MartenSnapshotStore` |
| Redis-Version-Index (abschaltbar, graceful degradation) | 🟢 | `RedisVersionTracker` / `NullVersionTracker` |
| Signal-Mechanik `(StreamId, Version)` fire-and-forget | 🟢 | pro Event ein `StateChangeVia…` |
| `BoundedInbox`-Dedup | 🟡 | FIFO ab `InboxCap`, nicht airtight |
| Client-Command-Zustellgarantie | 🟢 | Nack-Rückkanal vollständig (`CommandSendFailed` + `CommandFailed`, T2a); idempotenter Client-Pfad (Inbox vor OCC) + Retry-Loop mit deterministischer CommandId (T2b, 2026-08-12); Stille → `CommandUnbestaetigt` |

## 11.2 Konsum-/Prozess-Maschine

| Feature | Status | Beleg / Anmerkung |
|---|:--:|---|
| Gemeinsame Pull-/Signal-Schleife (`ProjectionAdapter`) | 🟢 | [04](04-konsum-und-prozess-maschine.md) |
| Poll-Backstop (30 s) mit `WakeAck`-Härtung | 🟢 | `Poller.cs` |
| Zwei Achsen: replaybar (Tracker) vs. emittierend (Cursor) | 🟢 | compile-zeit-strukturell |
| Projektionen (Read-Models) | 🟢 | `ISubscriber, IPullSubscriber` |
| **Exactly-once-Wirksamkeit (echter Co-Commit)** | 🟢 | implementiert & Postgres-bewiesen (`ImagePairStore`/`Historie` + `CoCommitPostgresTests`); Guard `ICoCommitTracker` schließt den false-green (2026-08-12) |
| Guard: Mis-Wiring strukturell unbaubar (Unit-of-Work) | 🟠 | optional, zurückgestellt (Hebel 2, s. konzept-exactly-once-naht.md) |
| Reaktionen (Command-Emission über `CommandEmitter`) | 🟢 | ein Emit-Weg, CQRS020/021 |
| Prozesse/Sagas als typisierte DSL (`IProzessDefinition`) | 🟢 | Diamant, Fan-out, Count-Join |
| Kompensation / `RückgängigDurch` | 🟢 | reverse Regel-Reihenfolge |
| `KlärungNötig`-Terminal + DLQ-Eintrag | 🟡 | korrekt-per-Konstruktion, **ohne Testdeckung** |
| Marking-Cursor O(N²)→O(N) (feuer-gerichtete Reads) | 🟢 | bis 9× gemessen (echtes Postgres) |
| `MarkingKompakt`-Verdichtung (Zähler+Bitset) | 🟠 | Konzept; Payloads noch O(N) bei Fan-out |
| Kompensations-Warmpfad cursor-optimiert | 🟠 | `NächsteKompensationAsync` liest noch ab 0 |
| Azyklizitäts-Boot-Guard | 🟢 | aus `GeneratedCommandRouting.Produziert` |
| §3-Backstop (offene Prozesse, 15 s) | 🟢 | durabler Offen-Index |
| Pipeline P6.1/P6.2 (Trigger/persist./transient getrennt) | 🟢 | `PipelineActorBase` |
| Dead-Letter (Sink + Read) | 🟢 | `MartenDeadLetterSink/ReadStore` |

## 11.3 Feature-Strom (Trigger, Fristen, Monitoring)

| Feature | Status | Beleg |
|---|:--:|---|
| Timer-Trigger | 🟢 | `TriggerStartupService`, `TimerTriggerSchedulingTests` |
| Webhook-Trigger | 🟢 | `POST /webhook/datei`, `PipelineWebhookHttpTests` |
| Deadlines/Fristen (`IDbClock`, `FristScheduler`) | 🟢 | `Deadlines/`; **nicht in einen Prozess integriert** (Primitiv steht allein) |
| Monitoring `/health` + `/monitoring/metrics` | 🟡 | 2 Zahlen; keine Prometheus/OTel |

## 11.4 Generatoren, Analyzer, Schema-Evolution

| Feature | Status | Beleg |
|---|:--:|---|
| ~20 Source-Generatoren (reflexionsfrei) | 🟢 | [05](05-generatoren-analyzer-proto.md) |
| 15 Diagnose-Codes (CQRS001–046) | 🟢 | Build-Guards |
| `GeneratedCommandRouting` (Dispatch-Kern) | 🟢 | aus Decider-Signaturen |
| Event-Upcasting 1:1 (typisiert, generiert) | 🟢 | `LagerEvolutionRoundtripPostgresTests` |
| Event-Upcasting 1:N (Split) | 🔴 | per CQRS046 bewusst blockiert (Consumer-Fabric fehlt) |
| STJ-Source-Gen-Serializer (Marten-Storage) | 🟡 | opt-in (`UseGeneratedJsonSerializer=false`) |
| Proto-DTO-Generierung (manueller Schritt) | 🟡 | string-basiert, zwei divergierende Maps |

## 11.5 Transport & Multi-Node

| Feature | Status | Beleg |
|---|:--:|---|
| Generierter reflexionsfreier Wire-Serializer | 🟢 | über alle Planes |
| Boot-Guard (Serializer-Vollständigkeit) | 🟢 | `WireSerializerBootCheck` |
| Cross-Node Command-Plane | 🟢 | `TwoNodeCommandDispatchTests` |
| Cross-Node PubSub-/Signal-Plane | 🟢 | `TwoNodePubSubSignalTests` |
| Cross-Node PID-Delivery | 🟢 | `RemotePidDeliverySmokeTests` |
| Cross-Node Saga | 🟢 | `LoadHarness --mode saga` |
| Weg-B Client-Subscription-Reassert | 🟢 | `ClientSubscriptionReassertTests` |
| Cold-Start-Schema-Migrator (dedizierter Init-Node) | 🟢 | `deploy-multinode/` |
| Produktiver Multi-Node-Betrieb (systemd-Cluster) | 🟠 | nur Container-Compose + Verify-Harness |
| Rolling Schema-Migration im laufenden Cluster | ⚪ | **bewusst weggelassen** (never-needed, Nutzer-Entscheid); Cold-Start-Migrator genügt |

## 11.6 Frontend (Blazor)

| Feature | Status | Beleg |
|---|:--:|---|
| Bus/Store-Stack (Redux/Flux, reflexionsfrei) | 🟢 | [08](08-frontend-blazor-client.md) |
| Generiertes Wiring/Subscription (depth-first) | 🟢 | `WiringGenerator` |
| Slot-basiertes Modul-System (13 Module) | 🟢 | `Shell.razor` + `IUiModule` |
| Hydration-Bootstrap (Daten- vor View-Timeline) | 🟢 | `ClientStartupService` |
| gRPC-Query-Korrelation (parallele Queries) | 🟢 | `GrpcProxy` |
| Eigene Virtualisierungs-Collection | 🟢 | 26 Tests |
| **Build der Frontend-Kette** | 🟢 | **2026-08-12 repariert** (stale Referenz entfernt); Solution-Build grün |
| Legacy-Projekte gelöscht (`Domain.Client`, `…Ui.Blazor`) | 🟠 | noch auf Disk (Cleanup offen, P2) |
| Client-Reconnect (Auto-Loop) | 🟠 | rudimentär, kein sichtbarer Loop |

## 11.7 Python

| Feature | Status | Beleg |
|---|:--:|---|
| `cqrs_client`-SDK (Transport/Dispatch/Mapper/Registry) | 🟢 | [09](09-python-sdk.md) |
| Event→Command-Pfad | 🟢 | vollständig |
| Proto-Sync (Build + Laufzeit-Hash) | 🟢 | `proto_sync.py` |
| Reconnect + Backoff | 🟢 | `connection.py` |
| ML-Worker (Torch-Bildklassifikation) | 🟢 | `classifier.py` |
| Query-Beantwortung durch Python-Client | 🟢 | oneof gesetzt via `wrap_query_response` (T3 2026-08-12) |
| Client→Client-Trigger | 🟢 | `wrap_trigger` + `_route_output`→`send_trigger` (2026-08-12); Server forwarded an registrierten Client-Handler |
| `generate_registry.py` (Auto-Registry) | 🔴 | Stub — schreibt nichts |
| Python-Tests | 🟡 | pytest-Grundgerüst (`requirements-dev.txt`, `pytest.ini`, `tests/`, T3 2026-08-12); Abdeckung noch dünn |
| Transport-Sicherheit (TLS/Auth) | 🔴 | keine — **system-weit** (Server h2c-plain, Blazor `http://`, Python plain); nicht Python-lokal, s. [13 P1-2](13-reifegrad-schulden-bewertung.md) |

## 11.8 Werkzeuge & Tests

| Feature | Status | Beleg |
|---|:--:|---|
| GraphExtractor (Property-Graph + HTML-Board) | 🟡 | 138/169; ungetrackt, `GraphModule` gelöscht |
| SimHost (Live-Simulation echter Domänenlogik) | 🟡 | Port 5178; nicht in `.sln`, ungetrackt |
| ProjectScanner (Struktur-Dump) | 🟡 | älteres Nebengleis |
| Prüfstand-Tests (Ebene 1, store-frei) | 🟢 | **126/126 grün** (gemessen 2026-08-12) |
| Integration-Tests (Ebene 2, echte Infra) | 🟢 | 41 Tests (infra-abhängig) |
| Client-Collection-Tests | 🟢 | 26 Tests |
| LoadHarness (Durchsatz/Saga/Serializer) | 🟢 | 4 Modi; nicht in `.sln` |

## 11.9 Domänen-Fachbausteine (Beispiel-Domänen)

Die Domäne dient als lebendiges Beispiel für alle Muster:

| Domäne | zeigt |
|---|---|
| `Konto` | einfaches Aggregat, OCC, Ablehnung |
| `Ueberweisung` | Saga mit 3-Wege-Join |
| `Reiseauftrag` | echter Diamant-Prozess + Kompensation je Zweig |
| `Sammelueberweisung` | Fan-out dynamischer Breite (`SendeJe` + `UndAlle`) |
| `Lager` | Event-Upcasting (echte Evolution `Bestand`→`AnfangsBestand`) |
| `ImagePair` | Projektion + Reaktion + Pipeline + ML-Worker (industrielle Bildinspektion) |
| `Reaktion` | Reaktions-Empfänger (mit Domänen-Leak-Schuld) |
