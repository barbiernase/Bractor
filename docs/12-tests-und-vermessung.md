# 12 — Tests & Vermessung

## 12.1 Die vier Test-Ebenen (das tragende Prinzip)

Die Teststrategie spiegelt die Invarianten „die Wahrheit ist der Log" und „nie faken, was man
nicht besitzt":

| Ebene | Projekt | Läuft gegen | Testet | Parallel? |
|---|---|---|---|---|
| 1 — Prüfstand | `Infrastructure.Pruefstand.Tests` | in-memory, **store-frei** | reine store-freie Logik: Decider/Applier, Fold, Routing, Wire-Serializer, Analyzer, Signale | ja |
| 2 — Integration | `Infrastructure.Integration.Tests` | **echtes** Marten/Postgres, Consul, Redis, Kestrel | Store-**Semantik** + E2E-Cluster-Verhalten | **nein** (erzwungen sequentiell) |
| Client | `Client.Infrastructure.CollectionTests` | in-memory | Frontend-`VirtualCollection` | ja |
| Last | `LoadHarness` | echter Cluster (bootet 1×) | Durchsatz, Latenz, Exactly-once, cross-node Saga, Serializer-Bench | n/a |

**Prinzip:** Ebene 1 testet nur, was ohne Store deterministisch entscheidbar ist. Store-Semantik
(Co-Commit-Atomarität, `seq_id`-Lücken, Snapshot-Roundtrip, Upcasting-Bytes) wird
**ausschließlich** gegen echtes Marten geprüft — ein In-Memory-Store würde sie nur *behaupten*.
Es gibt bewusst **keinen `InMemoryEventStore`**. In-Memory-Doubles existieren nur für
nicht-autoritative Randstores (`InMemoryProzessMarkingStore`, `InMemoryEmittentenCursor`, …,
konsistent mit Invariante 6).

## 12.2 Test-Zählung (statisch ermittelt)

| Projekt | `[Fact]` | `[Theory]` | Dateien | In `.sln`? |
|---|---:|---:|---:|:--:|
| Infrastructure.Pruefstand.Tests | 119 | 1 | 33 | ja |
| Infrastructure.Integration.Tests | 41 | 0 | 26 | ja |
| Client.Infrastructure.CollectionTests | 26 | 0 | 1 | ja |
| LoadHarness | — | — | 1 (480 LOC) | **nein** |

## 12.3 Echte Messwerte (gemessen 2026-08-12)

| Messung | Ergebnis |
|---|---|
| **Prüfstand-Testlauf** | ✅ **126/126 grün**, Dauer 1 m 20 s |
| **Build Host.Grpc (Backend)** | ✅ 0 Fehler |
| **Build Host.Blazor (Frontend)** | ✅ 0 Fehler (nach Fix 2026-08-12; vorher 13× CS0103 `_publish` wegen stale `Domain.Client.Ui.Blazor`-Referenz) |
| **Voller Solution-Build** | ✅ 0 Fehler, 128 Warnungen |
| **Integration-Tests** | nicht ausgeführt (benötigen laufendes Postgres/Consul/Redis) |

> Die statisch gezählten 119+1 decken sich mit den 126 zur Laufzeit expandierten Tests (die eine
> `[Theory]` und geteilte Fixtures ergeben die Differenz). Ältere Notizen nannten „106" — die
> Baseline ist gewachsen.

## 12.4 Kern-Test-Klassen und ihr Beweisgegenstand

**Prüfstand (Ebene 1):**
- `WireSerializerRoundTripTests` (13, größte) — reflexionsfreier Wire-Serializer; ein
  Boot-Check deckt **jeden** Command/jedes Event ab (generativer Vollständigkeits-Guard).
- `ProzessMarkingCursorTests` (7) + `Benchmark` (1) — Äquivalenz Tail-Cursor-Fold == Voll-Fold
  + Skalierungs-Benchmark (N ∈ {50…800}, misst gelesene Events + Wall-Clock).
- `UpcastingTests` (6), `EventVersioningTests` (4), `EventJsonSerializationTests` (5),
  `SignalTypeGeneratorTests` (4), `SignalEmitTests` (4), `AppendBatchingTests` (3).
- `EmitPrimitivTests` (6) + `ReaktionsIdTests` (4) — das eine Emit-Primitiv (Fake-Send).
- `CommandEmitAnalyzerTests` (3) — fährt den echten Analyzer über eine `CSharpCompilation`;
  beweist CQRS020/021.
- `KontoAggregatTests` (4), `ProzessAzyklizitaetTests` (3), `ProzessPollFilterTests` (4),
  `AblehnungsMarkeTests` (3), `GaEinsPruefungTests`, `DeadLetterReadStoreTests`,
  `PipelineEventPullBridgeTests`, `FristSchedulerTests`, `BackendMonitoringTests`.

**Integration (Ebene 2):**
- `TwoNodeCommandDispatchTests` — das Multi-Node-Tor: zwei Hosts, `PartitionIdentityLookup`
  verteilt N=12 Ids → grün beweist cross-node Serialisierung ((½)¹²-Argument im Kommentar).
- `TwoNodePubSubSignalTests`, `RemotePidDeliverySmokeTests`, `ClientSubscriptionReassertTests`.
- `BestellSagaE2ETests` (2), `ReiseSagaE2ETests` (1), `ReiseSagaParallelE2ETests` (2),
  `ProzessVerkettungE2ETests` (1) — Diamant-Sagas: happy/kompensierend/nebenläufig (kein
  Oversell)/verkettet.
- `CoCommitPostgresTests` (3) — „das größte Risiko": Effekt + Checkpoint atomar in einer
  Marten-Session; Absturz dazwischen → genau ein Eintrag.
- `SequenceGapPostgresTests` (2) — `seq_id`-Lücke + Straggler-Grace.
- `SnapshotStorePostgresTests` (2) + `SnapshotLiveE2ETests` (1).
- `LagerEvolutionRoundtripPostgresTests` (2) — echte Event-Evolution (Bytes).
- `ProzessBackstopE2ETests` (3) — Noop-Marke (K2) + §3-Backstop.
- `ProzessMarkingCursorPerfTests` (2) — ehrlicher Perf-Beweis gegen echtes Postgres.

**Client:** `VirtualCollectionTests` (26) — Paging, Skeleton, `FuegeVorneEin`, `Patch`, `Reset`.

## 12.5 Coverage-Bild

**Gut abgedeckt:** Schreibpfad + Exactly-once (Unit + Integration + LoadHarness-Rehydration);
Prozess-/Saga-Maschine (DAG-Guards, Fold-Äquivalenz, Sagas in vier Varianten, Backstop);
Multi-Node (je ein dedizierter Test pro Plane); Store-Fallstricke (seq-gap, Snapshot, Upcasting,
Metadaten); Analyzer (durabler Regressions-Guard); Wire-Serializer (generativer Vollständigkeits-
Guard über alle Typen).

**Nicht / schwach abgedeckt:**
- **`KlärungNötig`-Pfad** — kein Test-Treffer (bestätigt).
- **Paralleler Drain / Schreibpfad-Skalierung** — nur im LoadHarness beobachtbar, kein
  Assertion-Test.
- **`MarkingKompakt`-Verdichtung** — unimplementiert, ungetestet.
- **Kompensations-Warmpfad** (`NächsteKompensationAsync` ab-0) — ungetestet.
- **Echtes Shard-Rebalance / Node-Ausfall** — simuliert (direktes `Unsubscribe`), nicht erzwungen.
- **Client/Blazor jenseits `VirtualCollection`** — untestet; Bus/Store/Transport.
- **Python** — gar keine Tests.

## 12.6 LoadHarness — Modi & Vermessung

`dotnet run --project LoadHarness -- --mode <m> …`. Cluster wird **einmal** gebootet
(Readiness-Barriere `WaitRoutableCore`, kein Cold-Boot-Flake):
- **`aggregate`** (Default): `--accounts × (1 + --credits)` Commands, `--concurrency`. Misst
  Durchsatz + Latenz p50/p95/p99, dann **Exactly-once via Rehydration** (jeder Saldo == Soll).
  Gibt ein Group-Commit-Profil aus (Batches, Ø Events/Commit, serielle Commit-Zeit als %).
- **`saga`**: löst Überweisungen aus, Prozess läuft **cross-node**; verifiziert Gelderhaltung +
  je Konto `Reserviert == 0`.
- **`pipeline`**: Trigger→Ack ohne Persistenz. Vorbehalt: eine Pipeline = ein Actor = serielle
  Mailbox → gemessen wird der Durchsatz *einer* Pipeline.
- **`serbench`**: Serializer-Mikrobench ohne Cluster/DB — reflection vs. source-gen ns/op +
  Amdahl-Rechnung (Serialisierungsanteil an der Append-Wall-Clock; Baseline „20500 Events in
  ~6.46 s").

A/B-Schalter über Env (`BRACTOR_BATCHING`, `_BATCH_LINGER`, `_DRAIN_PAR`, `_SOURCEGEN_JSON`,
`_VERSION_TRACKING`). Konkrete Ergebnisse (+48 % Batching, 9×/5,5× Prozess-Reads) sind
Projektnotizen, nicht im Code hinterlegt.

## 12.7 Test-Infrastruktur & wie man einen Test schreibt

- **Kein Testcontainers.** Echte Infra extern via `docker compose -f
  deploy-linux/docker-compose.infrastructure.yml up -d`; Tests verbinden gegen feste Endpunkte
  (Connection-Strings je Testklasse **hartkodiert** — Wartungslast).
- **Fixtures pro Klasse** (`IClassFixture<PostgresFixture>` u.a.), kein geteiltes
  `CollectionDefinition`. Cluster-bootende Tests bauen je Test ihren eigenen Consul-Cluster.
- **Sequentialität erzwungen** via `xunit.runner.json` (`parallelizeTestCollections: false`).
- **HTTP-Tests** brauchen kein Postgres/Consul/Redis (minimale `WebApplication`).
- **Analyzer-Harness**: eigene `CSharpCompilation`.
- **Keine dedizierte Test-Basisklasse** — der Prüfstand ist bewusst leichtgewichtig. Einstieg:
  Aggregat über die generierte `AggregateHandlerFactory` treiben, FluentAssertions, deutsche
  `Methoden_Namen_mit_Unterstrich`. Muster: `Phase5/KontoAggregatTests.cs`. Store-Semantik →
  Ebene 2 (`CoCommitPostgresTests` als Vorlage). Minimalbeispiel siehe
  [10 §10.10](10-entwickler-api.md).

## 12.8 Bekannte Flakes

- **`SnapshotLiveE2ETests` Cold-Boot-Flake**: bimodal (~1 s grün *oder* Boot-Hang > Timeout),
  Ursache **Consul-Cold-Boot** (upstream von Append), **nicht** Timeout-tunebar; wird in der
  Integrations-Bilanz bewusst ausgenommen.
- **Keine `[Skip]`/`[Trait]`-Attribute** im gesamten Bestand — Flake-Handling per Konvention/Doku
  statt Annotation (in CI schwer selektiv auszuschließen).

## 12.9 Design-Prinzipien der Test-Strategie

1. **Nie faken, was man nicht besitzt** — Store-Semantik nur gegen echtes Marten.
2. **Testebene folgt der Frage, nicht dem Feature** — dieselbe Fachlogik auf mehreren Ebenen.
3. **Generative Vollständigkeits-Guards** statt punktueller Fälle (Wire-Boot-Check,
   Azyklizitäts-Guard).
4. **Beweiskraft explizit** — jede Integrationsklasse dokumentiert *warum* grün = bewiesen.
5. **Determinismus durch Sequentialität + direktes Treiben** (Consul-Cold-Boot umgehen).
6. **Perf ist Teil des Beweises** — LoadHarness koppelt Durchsatz mit einer Korrektheitsprüfung
   (ein schneller, aber falscher Lauf ist rot).
