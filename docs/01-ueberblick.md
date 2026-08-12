# 01 — Überblick

## 1.1 Das System in einem Absatz

Ein Event-getriebenes Backend, das Commands über **typgeroutete virtuelle Actors**
(Proto.Actor) entgegennimmt, Entscheidungen in reinem Fachcode (Decider) trifft und die
resultierenden Events in **Marten/PostgreSQL** als einzige Wahrheit anhängt. Aus dem Log
werden vier durable Konsumenten (Projektion, Reaktion, Prozess/Saga, Pipeline) **genau
einmal wirksam** bedient — über eine gemeinsame Pull-/Signal-Schleife. Ein schnelles,
verlierbares Signal `(StreamId, Version)` weckt Konsumenten; ein 30-s-Poll ist das
Sicherheitsnetz. Sämtliches Dispatching (Handler, Actor, Routing, Serialisierung, Signale,
Schema-Evolution) wird zur **Compile-Zeit generiert**; Runtime-Reflection ist verbannt.
Darauf sitzen ein generierter **Blazor-Client**, ein paritätisches **Python-SDK** und ein
**Wissensgraph-/Simulations-Werkzeug**.

## 1.2 Die sechs Invarianten

Jede Designentscheidung leitet sich aus diesen sechs Sätzen ab. Sie sind im Code
durchgehend belegbar (siehe [02-design-prinzipien.md](02-design-prinzipien.md)):

1. **Die Wahrheit ist der Log.** Ordnung, Vollständigkeit, Wiederholbarkeit kommen nur aus
   dem Event-Store-Read. Alles andere (Redis-Index, Snapshots, Marking-Cursor) ist abgeleitet
   und verwerfbar.
2. **Das Signal ist nur ein Weckruf.** Es trägt nur `(StreamId, Version)`, darf verloren,
   doppelt oder ungeordnet sein.
3. **Routing über Typen** — nie ein handgebauter Identitäts-String.
4. **Keine Runtime-Reflection.** Alles Dispatchende ist generiert.
5. **Der Fachcode bleibt rein.** Cursor, Signal, Ordnung, Exactly-once, Sharding,
   Prozess-Maschinerie tauchen im Entwickler-Code nie auf.
6. **Persistent genau dann, wenn ein durabler Konsument abhängt.** Verlierbares (Tick,
   UI-Feedback, Datei-Trigger) bleibt auf dem schnellen Kanal.

## 1.3 Solution-Landkarte (27 Projekte)

Gemessen (2026-08-12): **452 C#-Dateien / ~46.000 Zeilen** (ohne generierte `.g.cs`),
**18 Python-Module**, **39 Razor-Komponenten**, **3 Proto-Dateien**.

### Verträge & Kern
| Projekt | Rolle |
|---|---|
| `Abstractions` | alle Marker-Interfaces & Verträge (`IState`, `ICommand`, `IEvent`, `IDecider<T>`, `IProzessDefinition`, `IWireMessage`, `IUpcast`, …), `OneOf`, Prozess-DSL |
| `Core` | store-freie Bausteine (`ProjectionWriter` u.a.), in-memory prüfbar |
| `Infrastructure` | **das Herz**: Actor-Wiring, Marten-Store, Batching, Redis, PubSub, Pull-Pfad, Prozess-Manager, Pipeline, Wire-Serializer, gRPC-Service, Monitoring (100 Dateien) |
| `Domain.Infrastructure` | domänenspezifische Infra-Verdrahtung |

### Domäne (Fachcode)
| Projekt | Rolle |
|---|---|
| `Domain` | Aggregate (`Konto`, `Ueberweisung`, `Lager`, `ImagePair`, …), Decider/Applier, Commands/Events, Saga-Definitionen |
| `Domain.Projections` | Projektionen & Reaktionen (`ISubscriber`/`IPullSubscriber`) |
| `Domain.Pipeline` | Pipelines (`IPipelineHandler`) |

### Generatoren (Compile-Zeit)
| Projekt | Rolle |
|---|---|
| `Abstractions.SourceGeneration` | geteiltes Typ-Modell (`TypeNode`), kein `[Generator]` |
| `Core.SourceGeneration` | geteilter Analyse-Kern (Domain-Graph, Multi-Compilation) |
| `Domain.SourceGeneration` | Domain-seitige Generatoren (State, Handler, Signale, Dispatch) |
| `Infrastructure.SourceGeneration` | Infra-seitige Generatoren (Routing, Actors, Pull-Pfad, Wire, Upcasting) + der **Analyzer** |
| `Client.SourceGeneration` | Blazor-Client-Generatoren (Wiring, ViewModels, Module) |
| `Proto.SourceGeneration` | **manuell** laufendes Exe: schreibt `domain.proto` |
| `ProtoRepo` | aus `domain.proto` per Grpc.Tools generierte DTOs |

### Hosts & Betrieb
| Projekt | Rolle |
|---|---|
| `Host.Grpc` | **der Cluster-tragende Prozess** (Marten+Consul+Redis+Actors+gRPC) |
| `Host.Blazor` | Blazor-Server-**Client** (kein Cluster-Member; redet gRPC mit Host.Grpc) |
| `LoadHarness` | Durchsatz-/Exactly-once-/Saga-/Serializer-Messwerkzeug (nicht in `.sln`) |

### Frontend
| Projekt | Rolle |
|---|---|
| `Client.Infrastructure` | Client-Transport-/Bus-/Store-Stack (gRPC, Bus, StoreBase, Virtualisierung) |
| `Domain.Client.Modules.Blazor` | **neue** modulare UI (13 Slot-Module) |
| `Domain.Client` / `Domain.Client.Ui.Blazor` | **alte** monolithische UI (tot, aber noch referenziert → Build-Blocker) |

### Python
| Projekt | Rolle |
|---|---|
| `Client.Infrastructure.Python` | `cqrs_client` — paritätisches Python-SDK (gRPC-Client) |
| `Domain.Client.Worker.Python.ML` | ML-Worker (Torch-Bildklassifikation als out-of-process-Reaktion) |

### Werkzeuge & Tests
| Projekt | Rolle |
|---|---|
| `GraphExtractor` | Roslyn-Extractor → `knowledge-graph.json` + interaktives HTML-Board |
| `SimHost` | actor-freie Live-Runtime (Port 5178) für wertabhängige Simulation (nicht in `.sln`) |
| `ProjectScanner` | älteres Struktur-Dump-Tool (`PROJECT_STRUCTURE.md`) |
| `Infrastructure.Pruefstand.Tests` | Ebene 1 — store-frei, in-memory (126 Tests) |
| `Infrastructure.Integration.Tests` | Ebene 2 — echte Infra (41 Tests) |
| `Client.Infrastructure.CollectionTests` | Client-Virtual-Collection (26 Tests) |

## 1.4 Der Gesamt-Datenfluss

```
                         ┌─────────────── Clients ───────────────┐
                         │  Blazor (Host.Blazor)  ·  Python-SDK   │
                         └───────────────┬───────────────────────┘
                                         │ bidirektionaler gRPC-Stream
                                         │ (Command · Query · Event · Trigger)
                                         ▼
   ┌─────────────────────────────  Host.Grpc (Cluster-Node)  ─────────────────────────────┐
   │  CqrsClientService ──► AggregateDispatcher ──► [ClusterIdentity(Guid, AggregatTyp)]   │
   │                                                        │                              │
   │                                            AggregateActor  (Single-Activation)        │
   │                                                        │ Decider (reiner Fachcode)    │
   │                                                        ▼ OneOf<Events>                │
   │                                          BatchingEventAppender (Group-Commit)          │
   │                                                        ▼                              │
   │                                   ══════ Marten / PostgreSQL (der Log) ══════          │
   │                                          │                    │                       │
   │                    Signal (StreamId,Version)          Redis Version-Index (abgeleitet) │
   │                     fire-and-forget │  + Poll(30s)                                     │
   │                                     ▼                                                  │
   │        ┌────────────── ProjectionAdapter (eine Pull-/Signal-Schleife) ──────────────┐ │
   │        │  Projektion   ·   Reaktion   ·   Prozess/Saga   ·   Pipeline (persist.)    │ │
   │        │  (replaybar,       (emittiert    (Petri-Netz-      (Event-Pfad über Pull)  │ │
   │        │   Co-Commit)        Commands)     Interpreter)                             │ │
   │        └────────────────────────────────┬───────────────────────────────────────────┘ │
   │                                          │ neue Commands (CommandEmitter, das 1 Emit)  │
   │                                          └──────────► zurück in den Aggregat-Dispatch  │
   └──────────────────────────────────────────────────────────────────────────────────────┘
```

**Zwei Achsen prägen das Bild:**
- **Schnell vs. durabel:** Signal ist schnell und verlierbar; der Poll + der Log heilen alles.
- **Replaybar vs. emittierend:** Projektionen dürfen zurückgesetzt/neu aufgebaut werden;
  Emittenten (Reaktion, Prozess, Pipeline) bewegen echte Wirkung und dürfen **nicht** blind
  replayt werden — das ist im Typsystem verankert (kein Reset auf `IEmittentenCursor`).

## 1.5 Was das System besonders macht (Kurzcharakteristik)

- **Vier Konsumenten, eine Maschine.** Projektion/Reaktion/Prozess/Pipeline teilen *dieselbe*
  Pull-Schleife; der Unterschied fällt aus Konstruktor-Stores + Rückgabetypen, nicht aus
  einer Taxonomie. Siehe [04](04-konsum-und-prozess-maschine.md).
- **Compile-Zeit statt Laufzeit.** ~20 Generatoren + 15 Diagnose-Codes verwandeln ganze
  Fehlerklassen in Build-Fehler. Siehe [05](05-generatoren-analyzer-proto.md).
- **Sagas als typisierte DSL.** Ein Prozess ist ein `IProzessDefinition` mit Event→Command-
  Fluent-Regeln; der Rest (Manager, Korrelation, Marking, Kompensation) ist einmal geschrieben.
- **Reinheit durchgezogen bis in Client & Python.** Auch Blazor und der Python-Worker routen
  typbasiert, reflexionsarm, mit reinem Fachcode.
- **Selbstbeobachtung.** Ein Extractor macht das ganze System als traversierbaren Graph
  sichtbar und kann es über eine Mini-Runtime *echt* simulieren.

## 1.6 Bekannte, systemprägende Einschränkungen (Vorschau)

Diese drei Punkte sind für eine Bewertung zentral und werden in [13](13-reifegrad-schulden-bewertung.md)
priorisiert behandelt:

1. **Kein echter Co-Commit im Marten-Tracker.** Die versprochene Exactly-once-Wirksamkeit der
   Projektionen ist strukturell vorbereitet, aber der produktive `MartenProjectionTracker`
   committet Effekt und Marke in *getrennten* Sessions (at-least-once, abgesichert nur über
   den Dedup-Schlüssel + idempotente Upserts). Siehe [04 §3](04-konsum-und-prozess-maschine.md).
2. **Frontend-Build 2026-08-12 repariert.** Eine stale Projektreferenz hatte das tote alte
   UI-Projekt in den Build gezogen (13 Compile-Fehler); die Referenz ist entfernt, der volle
   Solution-Build ist grün. Die modulare GUI läuft über `Domain.Client.Modules.Blazor`; die
   beiden Legacy-Projekte liegen noch auf Disk (Cleanup offen). Siehe [08](08-frontend-blazor-client.md).
3. **Multi-Node ist bewiesen, aber betrieblich noch container-only.** Der produktive
   Regelweg bleibt Single-Node (systemd/nativ). Siehe [06](06-transport-multinode-betrieb.md).
