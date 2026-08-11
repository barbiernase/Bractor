# CLAUDE.md

Projektgedächtnis für Claude Code. Bewusst schlank — wird bei jeder Session geladen.
Volltexte liegen in `docs/` und werden bei Bedarf gelesen, nicht hier eingebettet.

> **Einstieg in die Doku:** `docs/README.md` (Wegweiser). Ist-Zustand: `docs/architektur/`
> (00–05). Aktueller Stand + offene Baustellen: `docs/backend-analyse-2026-08-11.md`. Das
> „Warum": `docs/design-philosophie.md`. Prozess schreiben: `docs/anleitung-prozess-schreiben.md`.

## Was das Projekt ist

Selbstgebautes, signalbasiertes CQRS-/Event-Sourcing-Framework auf **Proto.Actor** (virtuelle
Cluster-Actors), **Marten/PostgreSQL** (Event-Store, einzige Wahrheit) und **Redis**
(abgeleiteter, nicht-autoritativer Versions-Index). Events werden geordnet und **genau einmal
wirksam** an Projektionen, Reaktionen, Prozesse und Pipelines zugestellt — ohne
Runtime-Reflection, alles über Typen geroutet, alles Dispatchende zur Compile-Zeit generiert.

## Die sechs Invarianten (jede Entscheidung leitet sich hieraus ab)

1. Die Wahrheit ist der Log. Ordnung/Vollständigkeit/Wiederholbarkeit kommen NUR aus dem
   Event-Store-Read.
2. Das Signal ist nur ein Weckruf: trägt nur `(StreamId, Version)`, darf verloren, doppelt,
   ungeordnet sein.
3. Routing über Typen — nie ein handgebauter Identitäts-String.
4. Keine Runtime-Reflection. Alles generiert.
5. Der Fachcode bleibt rein. Cursor, Signal, Ordnung, Exactly-once, Sharding,
   Prozess-Maschinerie tauchen im Entwickler-Code nie auf.
6. Persistent genau dann, wenn ein durabler Konsument abhängt. Verlierbares (Tick,
   UI-Feedback, Datei-Trigger) bleibt auf dem schnellen Kanal.

## Das tragende Bild: vier Konsumenten, eine Maschine

Projektion, Reaktion, Prozess und Pipeline sind vier durable Konsumenten, die **dieselbe**
store-agnostische Pull-/Signal-Schleife (`ProjectionAdapter`) nutzen. Kein zweiter Marker,
keine Taxonomie — der Unterschied fällt aus Ctor-Stores + Rückgabetypen:
- **Achse B (replaybar vs. emittierend):** `IProjectionTracker` (Co-Commit + Reset) vs.
  `IEmittentenCursor` (best-effort, kein Reset). Beide gesetzt → Ctor wirft.
- **Transport:** Signal (schnell) + Poll (30 s, Sicherheit) wecken dieselbe Cluster-Identität.
- **Emit:** genau ein Weg (`CommandEmitter`), erzwungen durch den Analyzer **CQRS020/021**.

## Aktueller Stand (2026-08-11)

**Kern fertig und in sich konsistent:** Schreibseite, Konsum-Maschine (Projektion + Reaktion),
Prozess-Maschine (Event-Regel-DAG). **Feature-Strom geliefert:** Timer-/Webhook-Trigger,
Deadlines/Fristen (`IDbClock`), Monitoring (`/health`, `/monitoring/metrics`), Dead-Letter
(Read+Sink), Pipeline P6.1/P6.2 zerlegt. **Schreibpfad-Perf:** Group-Commit-Batching mit
parallelem Drain (+48 %), STJ-Serializer (opt-in), optionaler Version-Index. **Snapshots** voll
verdrahtet. **P5b Marking-Cursor geliefert:** der Prozess-Fold ist von O(N²) auf O(N) Stream-Reads
(nicht-autoritativer Tail-Cursor, `IProzessMarkingStore` + Marten-`ProzessMarkingDoc`, HOT-Cache je
Actor; Voll-Fold bleibt Fallback bei fehlendem/stale `RegelHash`). **Feuer-gerichtete Reads:** warm
liest der Manager nur die seit dem letzten Fold befeuerten Streams nach → auch die DB-Roundtrips O(N²)→O(N)
(gemessen echtes Postgres: bis 9× schnellere Wall-Clock bei N=60; mit Aggregat-Historie 5,5×). Kaltstart
faltet voll (Invariante 1). Äquivalenz + Sagas grün.

**Tests (echt gemessen): Prüfstand 106/106 grün (in-memory, store-frei), Integration 33/33
(gegen echtes Marten/Consul/Redis, sequentiell; der `SnapshotLive`-Cold-Boot-Flake ausgenommen).**

**Bewusst offen (Priorität):**
1. **Cross-Node/Multi-Node** — **Iteration 1 + 2 geliefert; Multi-Node-Block geschlossen.** Generierter,
   reflexionsfreier Wire-Serializer (`CqrsWireSerializer` + `WireSerializerGenerator` →
   `GeneratedWire`/`GeneratedWirePoly` über `CqrsWireJsonContext`, Marker `IWireMessage`) am
   `WithRemote`-Punkt, Boot-Check, Round-trip. Iter. 1 = Command-/Pull-Plane; Iter. 2 = PubSub-/
   Pipeline-/Prozess-Plane (`PID`-Converter via `PID.FromAddress`). **Bewiesen:**
   `TwoNodeCommandDispatchTests`, `TwoNodePubSubSignalTests`, `RemotePidDeliverySmokeTests`; cross-node
   Saga (`LoadHarness --mode saga`) + echter 3-Node-Container-Betrieb (`deploy-multinode/`,
   `docs/multi-node-deployment.md`). Weg B robust via Re-Assert (`ClientSubscriptionReassertTests`,
   `docs/multi-node-weg-b-client-gateway-konzept.md`). **Offener Folge-Schritt:** Cold-Start-Schema-
   Migrator (`docs/multi-node-schema-migrator-handoff.md`).
2. **P5b-Restfeinschliff (klein):** die `MarkingKompakt`-Größe ist für einen extremen Fan-out noch
   O(N) (Payloads je Vorgang); die volle Zähler+Bitset-Verdichtung (Konzept §4) bleibt optionaler
   Feinschliff. Der O(N²)→O(N)-Read-Gewinn (das eigentliche Problem) ist voll geliefert. Die
   Kompensations-`NächsteKompensationAsync` liest noch ab 0 (Fehlerpfad, nicht die Warm-Schleife).
3. **Schreibpfad-Perf** — paralleler Drain skaliert sublinear (`wait_event` offen).
4. **KlärungNötig-Pfad** korrekt-per-Konstruktion, aber ohne Testdeckung.

**Kleinere Schulden:** `DtoMapperGenerator` fragil (hartkodierte Enums, Encoding-Schäden);
`Reaktionsempfaenger`-Dedup-Menge (Domänen-Leak); Deadline-Primitiv nicht in einen Prozess
integriert; `CqrsFrameworkOptions` toter `[Obsolete]`-Typ.

> **Nebenbefund (nicht Backend):** `Domain.Client` (Blazor-Frontend) baut derzeit nicht
> (`_publish` fehlt, laufender Client-Generator-Umbau). Unabhängig von der Backend-Kette.

## Konventionen

- Kommentare/Domäne auf Deutsch (Bestand konsistent halten).
- Neue Verträge → `Abstractions`; Marten/Infra → `Infrastructure`.
- Nichts mit Runtime-Reflection (Inv. 4). Neue Dispatch-Logik = Generator erweitern, nicht
  Handschalter.
- **Kein `InMemoryEventStore`:** Store-Semantik nur gegen echtes Marten (Integration). Der
  Prüfstand testet nur store-freie Logik. Nie faken, was man nicht besitzt.
- **Proto-Regenerierung bei neuen Domain-Typen:** jeder neue Command/Event/Query/Trigger
  braucht einen Proto-DTO, sonst bricht der `DtoMapperGenerator`. Ablauf:
  `dotnet run --project Proto.SourceGeneration` → `ProtoRepo` neu bauen → Infrastructure baut.
  (Signale sind bewusst ausgenommen.)

## Build / Test

**⚠ Vor Test/Lasttest: `docs/testen-und-lasttest.md` lesen.** Drei Ebenen (Prüfstand in-memory
/ Integration gegen echte Infra / Last-Harness), plus reale Fallstricke: Integration
**sequentiell** lassen; der bekannte `SnapshotLiveE2ETests`-Cold-Boot-Flake ist Consul-Boot,
NICHT Timeout-tunebar; xUnit schluckt App-Logs (Cluster-Diagnose → Last-Harness `--log debug`).

- Build: `dotnet build`
- Test (Logik, immer grün): `dotnet test Infrastructure.Pruefstand.Tests/Infrastructure.Pruefstand.Tests.csproj`
- Integration (braucht Postgres/Consul/Redis, sequentiell): `dotnet test Infrastructure.Integration.Tests/Infrastructure.Integration.Tests.csproj`
- Last/Durchsatz: `dotnet run --project LoadHarness -- --accounts 500 --credits 40 --concurrency 128 --log warning`
- Infra hochfahren: `docker compose -f deploy-linux/docker-compose.infrastructure.yml up -d`
- Multi-Node (3 Nodes + 1 Consul, echt containerisiert): `docker compose -f deploy-multinode/docker-compose.yml up -d --build` (Anleitung + Ergebnis: `docs/multi-node-deployment.md`)
- Hosts: `Host_Blazor`, `Host_Grpc` (siehe deploy.sh).
