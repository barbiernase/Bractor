# Architektur — Überblick

> Die **lebende Referenz** auf den Ist-Zustand des Backends. Beschreibt, wie das
> Framework heute funktioniert, nicht wie es dorthin kam (Herleitung:
> `docs/design-philosophie.md`; Momentaufnahme mit Reifegrad/Baustellen:
> `docs/backend-analyse-2026-08-11.md`).

## Was das Projekt ist

Ein selbstgebautes, signalbasiertes CQRS-/Event-Sourcing-Framework auf **Proto.Actor**
(virtuelle Cluster-Actors), **Marten/PostgreSQL** (Event-Store, einzige Wahrheit) und
**Redis** (abgeleiteter, nicht-autoritativer Versions-Index). Events werden geordnet und
**genau einmal wirksam** an Projektionen, Reaktionen, Prozesse und Pipelines zugestellt —
ohne Runtime-Reflection, alles über Typen geroutet, alles Dispatchende zur Compile-Zeit
generiert.

## Die sechs Invarianten

Jede Design-Entscheidung leitet sich hieraus ab:

1. **Die Wahrheit ist der Log.** Ordnung, Vollständigkeit und Wiederholbarkeit kommen NUR
   aus dem Event-Store-Read.
2. **Das Signal ist nur ein Weckruf.** Es trägt nur `(StreamId, Version)` und darf verloren,
   doppelt oder ungeordnet sein.
3. **Routing über Typen** — nie ein handgebauter Identitäts-String.
4. **Keine Runtime-Reflection.** Kein `Activator.CreateInstance`, kein `MethodInfo.Invoke`,
   kein Assembly-Scan im Laufzeitpfad. Alles generiert.
5. **Der Fachcode bleibt rein.** Cursor, Signal, Ordnung, Exactly-once, Sharding,
   Prozess-Maschinerie tauchen im Entwickler-Code nie auf.
6. **Persistent genau dann, wenn ein durabler Konsument abhängt.** Verlierbares (Tick,
   UI-Feedback, Datei-Trigger) bleibt auf dem schnellen Kanal.

## Das tragende Bild: vier Konsumenten, eine Maschine

Es gibt vier durable Konsumenten von Events:

- **Projektion** — baut ein Read-Model (Repo-Write-Effekt).
- **Reaktion** — feuert ein Command auf ein Fremd-Aggregat.
- **Prozess** — orchestriert mehrere Aggregate über einen Event-Regel-DAG.
- **Pipeline** — serverseitiges Gegenstück zum Client: Trigger/Events → Commands.

Alle vier laufen über **dieselbe** store-agnostische Pull-/Signal-Schleife
(`ProjectionAdapter`). Es gibt **keinen zweiten Marker und keine Typ-Taxonomie** — der
Unterschied fällt aus den Konstruktor-Stores und den Rückgabetypen des Handlers.

### Achse B — replaybar vs. emittierend (Compile-Zeit-Schnitt)

| | Projektion | Reaktion / Prozess / Pipeline |
|---|---|---|
| Ctor-Store | `IProjectionTracker` (Co-Commit **+** Reset) | `IEmittentenCursor` (best-effort, **kein** Reset) |
| Durabler Effekt | co-committetes Read-Model | emittiertes Command (idempotent am Empfänger) |
| Replay | ja (`ProjectionRebuilder`) | strukturell unmöglich (kein Reset — Replay bewegt echtes Geld) |

Der Schnitt ist **erzwungen**: sind im `ProjectionAdapter`-Ctor beide Stores gesetzt,
wirft er `InvalidOperationException` („replaybar ODER emittierend").

### Transport-Achse — Signal + Poll

- **Signal (schnell, verlierbar):** Ein `StateChangeVia{Event}`-Signal weckt den
  per-Stream-Actor. Signal = nur Weckruf (Invariante 2).
- **Poll (Sicherheit, 30 s):** Ein globaler Scan über die Store-Sequenz weckt jeden
  geänderten Stream und heilt das letzte verlorene Signal vor Stille.

Beide wecken **dieselbe** Cluster-Identität (StreamId bzw. Korrelation) → der per-Stream-
Actor serialisiert sie, kein Race.

### Emit-Achse — genau ein Weg (EM-1)

Es gibt **genau einen** Command-Emit-Weg: `CommandEmitter` (deterministische CommandId +
bounded Token). Das ist **zur Compile-Zeit erzwungen**: der Roslyn-Analyzer
`CommandEmitAnalyzer` bricht den Build (CQRS020) bei jedem rohen
`RequestAsync<CommandResult>` außerhalb der zwei legitimen Sender und (CQRS021) bei
`CancellationToken.None` auf einer Command-Kante.

## Exactly-once — die ehrliche Aussage

Das Framework stellt NUR einen Nahtpunkt bereit (`IProjectionTracker`), es garantiert die
Wirksamkeit NICHT selbst. Ob aus „wirksam" ein „genau einmal wirksam" wird, entscheidet
die Store-Implementierung: Effekt + Marke in EINER nativen Transaktion → exactly-once
wirksam; getrennt → at-least-once, Handler müssen idempotent sein. Append-artige
Projektionen brauchen Co-Commit ODER einen Dedup-Schlüssel `(AggregateId, AggregateVersion)`;
der Boot-Guard **GA-1** erzwingt das.

Auf dem Emit-Pfad (Reaktion/Prozess/Pipeline) trägt die Idempotenz nicht der Store, sondern
die **Framework-Inbox** am Empfänger: eine deterministische CommandId + eine co-committete
`KommandoVerarbeitet`-Marke lassen ein wiederholtes Command verpuffen.

## Projektlandkarte (Solution-Projekte)

| Projekt | Rolle |
|---|---|
| `Abstractions` | reine Verträge (kein Marten/Proto); Marker, Envelopes, Prozess-DSL, Ids |
| `Core` | framework-nahe Hilfstypen (Write-Scope, Deps-Sink, Trigger-Registrierung) |
| `Infrastructure` | die Maschine: Persistenz, Actors, Konsum-Maschine, Prozess, Pipeline, Monitoring |
| `Domain` / `Domain.Projections` / `Domain.Pipeline` | Testvehikel (Aggregate, Prozesse, Projektionen, Pipelines) |
| `Domain.Infrastructure` | Co-Commit-Stores der Domäne |
| `*.SourceGeneration` | Roslyn-Generatoren (Domain: Syntax-Ebene; Infrastructure: Symbol-Ebene) |
| `Proto.SourceGeneration` | Standalone-Tool (`dotnet run`) → `ProtoRepo/domain.proto` |
| `Host.Grpc` / `Host.Blazor` | Hosts |
| `Infrastructure.Pruefstand.Tests` | Ebene 1: store-freie Logik (in-memory) |
| `Infrastructure.Integration.Tests` | Ebene 2: gegen echtes Marten/Consul/Redis (sequentiell) |
| `LoadHarness` | Ebene 3: Durchsatz/Latenz + Exactly-once unter Dauerlast |

## Die Subsystem-Dokumente

- [01 — Schreibseite](01-schreibseite.md): Command → Append → Event-Stream, `CommandModus`,
  Batching, Inbox, Snapshots, Version-Index, Serialisierung.
- [02 — Konsum-Maschine](02-konsum-maschine.md): der eine Pull-Adapter, Signal/Poll,
  Co-Commit, Emittenten-Cursor, Rebuilder, GA-1.
- [03 — Prozess-Maschine](03-prozess-maschine.md): Event-Regel-DAG, `ProzessManager`, EM-1,
  Korrelation, Azyklizität, Fan-out, Verkettung, KlärungNötig.
- [04 — Feature-Strom](04-feature-strom.md): Pipeline, Trigger (Timer/Webhook), Deadlines,
  Monitoring, Dead-Letter.
- [05 — Generatoren & Analyzer](05-generatoren-und-analyzer.md): alle Generatoren, die
  Diagnostik-Codes CQRS001–021, die Generator-Ketten.

## Aktueller Scope & Grenzen

**Enthalten und geliefert:** die gesamte Schreibseite, die Konsum-Maschine (Projektion +
Reaktion), die Prozess-Maschine (Event-Regel-DAG mit Diamant/Fan-out/Verkettung/
Kompensation), der Feature-Strom (Trigger, Deadlines, Monitoring, Dead-Letter,
Pipeline-Zerlegung), Snapshots, Group-Commit-Batching.

**Bewusst noch offen:**
- **Cross-Node/Multi-Node** — kein registrierter Serializer für den internen Plane → de
  facto single-node.
- **P5b Marking-Cursor** — der Prozess-Fold ist O(N²); bewusst zurückgestellte Optimierung.
- **Schreibpfad-Perf** — der parallele Commit-Drain skaliert sublinear (`wait_event` offen).
- **Deadline↔Prozess-Integration** — das Frist-Primitiv ist noch nicht in die
  Prozess-Marking-Schicht eingebunden.

Details und Priorisierung: `docs/backend-analyse-2026-08-11.md`.
