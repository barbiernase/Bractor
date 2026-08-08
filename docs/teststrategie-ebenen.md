# Teststrategie — drei Ebenen (Übergabe)

> **Zweck:** Handoff für einen Agenten, der die Testsuite entlang dieser Struktur (neu) ordnet.
> Self-contained. Motivation: das Backend-Audit (`docs/backend-audit-befunde.md`) fand drei
> HOCH-Bugs, die sich **nur** verstecken konnten, weil **Store-Semantik in-memory getestet**
> wurde. Der In-Memory-Store bildet Marten/Postgres von Hand nach und divergiert → falsche
> Sicherheit. Die Korrektur ist **Scoping**, nicht Löschen.
>
> **Kern-Grundsatz:** Das Testwerkzeug muss zu dem passen, was tatsächlich unter Test steht.
> Reine Logik → schneller Fake (deterministisch, ms). Store-/DB-Garantien → echtes Marten
> (der Fake kann nicht testen, was er faked). „Mocke nicht, was du nicht besitzt."

---

## ⚠ AKTUALISIERT (umgesetzt) — KEIN In-Memory-Event-Store mehr

Die Entscheidung wurde radikalisiert und ist umgesetzt: **ein handgepflegter In-Memory-Store IST
„Postgres nachprogrammieren"** — und jede Divergenz war eine stille Falle (Befund 1, 3, 6, 11a/b).
Deshalb sind `InMemoryEventStore` + `Infrastructure/Testing/{InMemoryProjectionTracker,
InMemoryPollCursorStore, InMemorySnapshotStore}` **gelöscht** (waren test-only, lagen aber in der
Prod-Assembly). Die maßgebliche Regel lautet jetzt schärfer:

- **Ebene 1** testet NUR noch, was **gar keinen Store braucht** (reine Funktionen: Decider/Applier,
  Saga-Regel-Faltung als Datenstruktur, Graph-Algorithmen, Fake-Send-Kontrollfluss). Kein Event-Store,
  kein Tracker — wo ein Test einen Store „zum Arrangieren" bräuchte, gehört er auf Ebene 2.
- **Store-Semantik + Saga-/Adapter-Verhalten** liegen ausschließlich auf **echter Infra** (Ebene 2 = echtes
  Marten ohne Cluster; Ebene 3 = Cluster/E2E). Die früheren In-Memory-Duplikate (Crash-Proben, Co-Commit,
  Poller, Metadaten, Reaktions-/Saga-Faltung, Snapshot-Rehydration) sind gelöscht — ihre Garantien decken
  `CoCommitPostgresTests`, `MetadataPostgresTests`, `PollerBackstopPostgresTests`, `SequenceGapPostgresTests`,
  `BestellSagaE2ETests`, `ReiseSaga*E2ETests`, `ProzessManagerE2ETests`, `SnapshotStorePostgresTests` /
  `SnapshotLiveE2ETests` gegen echtes Postgres/Cluster ab.

Der Rest dieses Dokuments ist der HISTORISCHE Plan (Ebene 1 sollte den In-Memory-Store behalten). Er ist
durch das Obige überholt: die „Fake-Harness bleibt / hier gehört der In-Memory-Store hin"-Passagen gelten
NICHT mehr.

---

## Rahmenbedingung (Entscheidung)

- **Alle Tests setzen voraus, dass Postgres/Consul/Redis im Hintergrund laufen.** Kein
  Container-Management im Test-Code (kein Testcontainers), auch nicht für die Ebenen, die
  gegen echte Infrastruktur laufen. Die Tests verbinden sich einfach auf die localhost-Defaults
  aus `AddCqrsFramework` (`Infrastructure/Extensions/CqrsServiceExtension.cs`, `CqrsFrameworkBuilder`:
  Postgres `localhost:5432`, Consul `localhost:8500`, Redis `localhost:6379`).
- Infra hochfahren (vor dem Testlauf): `docker compose -f deploy-linux/docker-compose.infrastructure.yml up -d`.
- **Ausnahme:** Ebene 1 braucht KEINE Infra (reine In-Memory-Logik).

---

## Die drei Ebenen

| Ebene | Projekt | Braucht Infra | Cluster? | Prüft |
|---|---|---|---|---|
| **1 — Logik** | `Infrastructure.Pruefstand.Tests` | nein | nein (Fake) | store-agnostische Logik: Decider, Saga-Regel-Faltung, Adapter-Kontrollfluss, Dedup |
| **2 — Store-Semantik** | *(neu / erweitert)* `Infrastructure.Integration.Tests` | Postgres (+Redis) | **nein** | echte Marten-Garantien: Append/Read, HWM, Gap, Co-Commit, exactly-once, Metadaten |
| **3 — Cluster/E2E** | `Infrastructure.Integration.Tests` | Postgres+Consul+Redis | **ja** | Signal-Zustellung, Live-Command, Reaktion/Saga end-to-end, Multi-Node |

### Ebene 1 — Logik (schnell, keine Infra)
Der Fake-Harness bleibt. Hier gehört der In-Memory-Store hin — **aber nur für store-agnostische
Logik**. Millisekunden, deterministisch, kein Docker. Prüft: Decider/Applier (reine Funktionen),
Saga-/Prozess-Regel-Faltung (`ProzessManager`-Marking, Join/Fan-out-Logik), Adapter-Kontrollfluss
(Marke lesen → ab Marke+1 → Guard → dispatch → Marke), Reaktions-Routing-Logik.
Bausteine: `Infrastructure.Pruefstand.Tests/Pruefstand/*` (PruefstandAdapter, PruefstandFaults,
InMemoryCoCommitStore, NoopHandlerFactory).

### Ebene 2 — Store-Semantik (echtes Marten, KEIN Cluster)
Verbindet sich direkt auf das laufende Postgres über `IEventStoreRepository` + die echten Stores,
**ohne** einen Proto-Cluster hochzufahren. Prüft genau das, was der In-Memory-Store NICHT kann.
**Wichtiger Nebeneffekt: diese Ebene ist echt UND nicht-flaky** — die Cold-Boot-Flakiness kommt
ausschließlich vom Cluster-Start; ein reiner Marten-Test hat sie nicht. Vorbild existiert bereits:
`Infrastructure.Integration.Tests/CoCommitPostgresTests.cs` (Postgres, kein Cluster).

### Ebene 3 — Cluster/E2E (echte Infra + Cluster)
Voller Boot: `SignalDeliveryClusterTests`, `LiveCommandE2ETests`, `ReaktionE2ETests`,
`BestellSagaE2ETests`, `ReiseSagaParallelE2ETests`, `SnapshotLiveE2ETests`. Echt, aber mit der
**bekannten Cold-Boot-Flakiness** (bimodal ~1s grün / Hang; Ursache Consul-Konvergenz beim
Kaltstart, siehe `memory/snapshot-e2e-flake-clusterboot.md`). Bewusst schlank halten — nur was
wirklich den Cluster braucht. **Sequentiell laufen lassen** (siehe Konventionen).

---

## Konventionen (für alle Ebenen mit Infra)

1. **Kein Container-Code im Test.** Infra wird als laufend vorausgesetzt.
2. **Isolation gegen das geteilte Postgres:** jeder Test nutzt **zufällige** AggregateIds/StreamIds
   (`Guid.NewGuid()`) und **eindeutige** Cluster-/Schema-Namen (`"...-" + Guid.NewGuid().ToString("N")[..8]`),
   damit Tests im gemeinsamen Store nicht kollidieren. (Bestehende E2E-Tests machen das bereits.)
3. **Ebene 3 sequentiell:** `Infrastructure.Integration.Tests/xunit.runner.json` schaltet die
   Parallelisierung ab (jede Klasse fährt einen echten Consul-Cluster hoch; parallel → Contention).
   **Nicht** auf parallel umstellen.
4. **Ebene-3-Flakiness nicht mit Timeouts „härten".** Der Snapshot-Cold-Boot-Hang ist bimodal und
   nicht timeout-tunebar (dokumentiert). Isoliert nachlaufen lassen, als bekannt akzeptieren.
5. **Diagnose von Cluster-Verhalten NICHT über `dotnet test`** — xUnit schluckt App-Logs (Console
   und ILogger). Dafür den Last-Harness mit `--log debug` nehmen (`docs/testen-und-lasttest.md`).

---

## Der Umzug: was von Ebene 1 nach Ebene 2 wandert

Heute nutzen ~16 Prüfstand-Dateien den `InMemoryEventStore`. Ein Teil prüft **Store-Semantik**
in-memory — genau die falsche Sicherheit. Kandidaten für den Umzug nach Ebene 2 (echtes Marten).
**Der Fix-Agent muss jede Datei einzeln bestätigen** — die Einteilung unten ist begründet, aber
pro Datei zu prüfen (manche mischen Logik und Semantik → ggf. aufteilen).

**→ Nach Ebene 2 (Store-Semantik, echtes Marten):**
- `Infrastructure.Pruefstand.Tests/CrashProbeTests.cs` — Crash zwischen Effekt und Marke; Co-Commit-Atomarität. **Das ist Postgres-Transaktionsverhalten** — in-memory nicht aussagekräftig.
- `Infrastructure.Pruefstand.Tests/NichtIdempotentCoCommitTests.cs` — Co-Commit. Store-Semantik.
- `Infrastructure.Pruefstand.Tests/Phase4/PollerTests.cs` — HWM-Vorrücken/Gap. **Genau Befund 1 + 3** — muss gegen echtes Marten laufen, sonst verdeckt es die Bugs weiter.
- `Infrastructure.Pruefstand.Tests/Phase1/MetadataPersistenceTests.cs` — CorrelationId/CausationId/AggregateType-Readback. Store-Semantik (Marten-Header).
- `Infrastructure.Pruefstand.Tests/Phase6/SnapshotRehydrationTests.cs` — Snapshot-Store-Semantik + SchemaVersion-Gating. Borderline → prüfen; wenn es echte Persistenz prüft, Ebene 2.

**→ Bleibt Ebene 1 (reine Logik):**
- `Phase5/BestellSagaTests.cs`, `ReiseSagaTests.cs`, `ProzessManagerTests.cs`, `ProzessManagerHangTests.cs`, `ProzessManagerGlueTests.cs`, `ProzessFanOutTests.cs`, `SprechenderDispatchTests.cs` — Saga-/Prozess-**Regel-Logik**. Store ist Ablenkung.
- `Phase2/SignalReceiverTests.cs`, `Phase3/ReaktionAufPullTests.cs` — Receiver→Wake- bzw. Reaktions-Routing-**Logik** (Fake-Emit).
- `ProjectionAdapterTests.cs` — **Kontrollfluss** des Adapters (Guard, Schleife). Der Kontrollfluss bleibt Ebene 1; falls einzelne Fälle Co-Commit-Atomarität behaupten, diese nach Ebene 2 ausgliedern.

**Sonstige Aufräumung (aus dem Audit):**
- `Infrastructure/InMemoryEventStore.cs` liegt im **Produktions-Projekt** `Infrastructure/`. Ein
  Test-Double gehört nicht in die Produktions-Assembly — nach `Infrastructure/Testing/` (oder ein
  Test-Support-Projekt) verschieben.

---

## Eine schlanke Ebene-2-Basis (Skizze)

Kein Cluster, keine Container — nur echtes Marten. Grobes Muster (Details beim Umsetzen):

```csharp
// Verbindet sich auf das laufende Postgres (localhost). Eigenes Schema pro Testlauf für Isolation.
var store = /* MartenEventStore via DocumentStore.For(...) mit eindeutigem EventStoreSchema */;
var tracker = new MartenProjectionTracker(docStore);
var poller  = new Poller(store, wake, startHighWater: 0);
// ... Arrange echte Events, Act (append/read/poll/mark), Assert gegen echtes Marten-Verhalten.
```

Für Reproduktionen der Backstop-Bugs (Befund 1/3): einen Stub-Adapter, dessen Verarbeitung beim
ersten Aufruf wirft, plus zwei nebenläufige Marten-Sessions mit interleaved Commit-Timing.

---

## Laufbefehle

```bash
# Infra (einmalig, vor Ebene 2/3)
docker compose -f deploy-linux/docker-compose.infrastructure.yml up -d

# Ebene 1 — Logik (schnell, immer)
dotnet test Infrastructure.Pruefstand.Tests/Infrastructure.Pruefstand.Tests.csproj

# Ebene 2 + 3 — gegen echte Infra (sequentiell)
dotnet test Infrastructure.Integration.Tests/Infrastructure.Integration.Tests.csproj
```

## Verwandte Dokumente
- `docs/backend-audit-befunde.md` — die Bugs, die diese Umstrukturierung motivieren (v.a. 1–3, 6, 11).
- `docs/testen-und-lasttest.md` — How-to der Ebenen + Last-Harness (Durchsatz/Latenz).
- `memory/snapshot-e2e-flake-clusterboot.md` — die Cluster-Cold-Boot-Flakiness (Ebene 3).
- `memory/hang-diagnose-in-memory.md` — verteilte Hangs nicht im Integrationstest jagen.
