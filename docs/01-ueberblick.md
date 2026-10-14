# 01 — Überblick

Stand 2026-10-13, gegen den Code geprüft. Maßstab: docs/konzept-werkbank.md.

## 1.1 Das System in einem Absatz

Ein Event-getriebenes Backend, das Commands über **typgeroutete virtuelle Actors** (Proto.Actor) entgegennimmt, Entscheidungen in reinem
Fachcode (Decide) trifft und die resultierenden Events in **Marten/PostgreSQL** als einzige Wahrheit anhängt — jeder Eintrag mit seiner Spur
(`ursache`, `wirker`, `ueber`, `stand`). Aus dem Log werden durable Konsumenten (Projektion, Reaktion, Prozess, Fluss) **genau einmal wirksam**
bedient. Ein schnelles, verlierbares Signal `(StreamId, Version)` weckt sie; ein 30-s-Poll ist das Sicherheitsnetz. Eingänge sind Clients
über gRPC (Akteur-Tor), der JSON-Eingang der Werkbank und Quellen (Treiber + Konfig, Webhook als Konfig). Dispatch, Routing,
Wire-Serialisierung und Signale sind **generiert**. Darauf sitzen Clients mit Vertrag (Blazor, Python) und die **Werkbank** (§1.6).

## 1.2 Die sechs Invarianten

**Die eine Quelle.** Jede Designentscheidung leitet sich aus diesen sechs Sätzen ab; andere Dokumente verweisen hierher.
Belege im Code: [02-design-prinzipien.md](02-design-prinzipien.md).

1. **Die Wahrheit ist der Log.** Ordnung, Vollständigkeit und Wiederholbarkeit kommen nur aus dem Event-Store-Read. Alles andere
   (Redis-Index, Snapshots, Marking-Cursor) ist abgeleitet und verwerfbar.
2. **Das Signal ist nur ein Weckruf:** trägt nur `(StreamId, Version)`, darf verloren, doppelt, ungeordnet sein.
3. **Routing über Typen** — nie ein handgebauter Identitäts-String.
4. **Keine Reflection.** Die Laufzeit nutzt generierte Tabellen/Kontexte; Werkzeuge (Codegen, Schema, Extractor) lesen über Roslyn.
5. **Der Fachcode bleibt rein:** Cursor, Signal, Ordnung, Exactly-once, Sharding, Prozess-Maschinerie tauchen im Entwickler-Code nie auf.
6. **Persistent genau dann, wenn ein durabler Konsument abhängt.** Verlierbares (Tick, Ablehnung, UI-Feedback, Kenntnis-Push an Clients)
   bleibt auf dem schnellen Kanal. Quell-Nachrichten (z. B. vom Datei-Treiber) sind durabel: genau einmal je Kennung im Log.

Wo der Code Invariante 4 noch nicht einlöst, steht in docs/13 (W10-1 Werkzeuge, W10-2 Laufzeit).

## 1.3 Projekt-Landkarte

Die Solution (`CqrsSolution.sln`) umfasst 30 Projekte; dazu kommen zwei Python-Projekte außerhalb der `.sln`.

### Framework
| Projekt | Rolle |
|---|---|
| `Abstractions` | Verträge und Marker (`ICommand`, `IEvent`, `IState`, `IPipeline`, `IQuellNachricht`, `IFunktion`, `IDienst`, `IProzessDefinition`, Akteur- und Client-Verträge, `IWireMessage`, `IUpcast` …), `OneOf`, Prozess- und Fluss-DSL, Spur (`Spur/`), Umgebungs-Schema (`Umgebung/UmgebungKonfig.cs`) |
| `Core` | store-freie Bausteine (`ProjectionWriter`, `AggregateHandlerBase`), im Prüfstand prüfbar |
| `Infrastructure` | die Laufzeit: Aggregat-Actors, Marten-Log, Batching, Redis, PubSub/Signal, Pull-Pfad, Prozess-Manager (Dirigent), Quellen und Treiber, Funktionen (Vermittler, Ausführer), Dienste, Akteure, Umgebung, JSON-Eingang, Einblick-API, Wire-Serializer, gRPC-Service, Monitoring |

### Generatoren
| Projekt | Rolle |
|---|---|
| `Abstractions.SourceGeneration` | geteiltes Typ-Modell (`TypeNode`), kein Generator |
| `Core.SourceGeneration` | geteilter Analyse-Kern (Domänen-Graph, `ProtoScalarSpec`) |
| `Domain.SourceGeneration` | Generatoren und Analyzer, die in den Domänen-Projekten laufen (State, Handler, Signale, Prozess-Regeln, Fluss-/Modell-/Dienst-Analyzer) |
| `Infrastructure.SourceGeneration` | Generatoren und Analyzer der Laufzeit-Compilation (Routing, Actors, Pull-Pfad, Wire, Funktionen, Dienste, Quellen, Akteur-Rechte, Gesamtbild-Analyzer) |
| `Projections.SourceGeneration` | DI-Verdrahtung der Leseseite (`AddGeneratedProjectionServices`), Store-Zugriffs-Analyzer |
| `Client.SourceGeneration` | Blazor-Client-Generatoren (Wiring aus dem Client-Vertrag, ViewModels, Module) |
| `Cqrs.Codegen` | Codegen-Schritt im Build (Target `CqrsCodegenPrepass` in `Domain.Leitung/Domain.Leitung.csproj`): `domain.proto`, STJ-Kontexte, Python-Basen; liest heute per `MetadataLoadContext` (docs/13 W10-1) |
| `Proto.SourceGeneration` | Roslyn-Leser für `domain.proto` (`FileGenerator`), von `Cqrs.Codegen` als Bibliothek genutzt |
| `Cqrs.Leitung` | die Leitung des Frameworks (`cqrs.proto`, handgeschrieben): gRPC-Dienst, Hüllen, Protokoll-Nachrichten; Domänen-Nachrichten reisen als `Nutzlast` (Typname + Bytes) |
| `Domain.Leitung` | Leitung der Anwendung (Rolle `.Leitung`): aus `domain.proto` (vom Codegen, nur die Domänen-DTOs) per Grpc.Tools erzeugte DTOs |

Was welche Generatoren und Analyzer tun: [05](05-generatoren-analyzer-proto.md).

### Werkzeuge (Werkbank)
| Projekt | Rolle |
|---|---|
| `Werkbank` | Werkbank-Server und Editor-Oberfläche (`Werkbank/Server/`, `Werkbank/wwwroot/editor/`) |
| `GraphExtractor.Kern` | Lesen (Code → Board), Sicht (`board.sicht`, ⊕-Passung), Schreiben (Gesten → Patch), Sonde, Paritätsprüfung |
| `GraphExtractor` | CLI: `--check`, `--sonde`, `--grammatik`, `--warm`, `--kontexte` |
| `DomainEditor` | Editor-Modell, Grammatik (`Grammatik.cs`), Scaffolder |
| `ProjectScanner` | älteres Struktur-Dump-Tool |

### Hosts
| Projekt | Rolle |
|---|---|
| `Domain.Laufzeit` | die Komposition der Anwendung (Rolle `.Laufzeit`): Framework + Rollen-Projekte, alle Laufzeit-Generate, `GeneratedAnwendung` und das `AddCqrs` der Hosts; die STJ-Kontexte |
| `Host.Grpc` | der Cluster-Knoten: `builder.AddCqrs(args)` (aus `Domain.Laufzeit`) + `app.LaufeAsync(umgebung)`, Konfiguration aus `umgebungen/umgebung.<name>.json` |
| `Host.Blazor` | Blazor-Server-Host des Arbeitsplatz-Clients (kein Cluster-Member, gRPC zu `Host.Grpc`), ebenfalls mit `--umgebung` |

### Clients
| Projekt | Rolle |
|---|---|
| `Client.Infrastructure` | C#-Client-Stack: gRPC-Transport, Bus, Stores, Virtualisierung, Zustellverfolgung |
| `Client.Infrastructure.Python` (außerhalb der `.sln`) | `cqrs_client`, das Python-SDK |

### Beispieldomäne (Prüfling)
Die Beispieldomäne prüft das Framework; sie ist nicht Architektur (konzept-werkbank W11).

| Projekt | Rolle |
|---|---|
| `Domain` | Aggregate, Commands, Events, Decide/Apply, Prozesse und Flüsse der Beispieldomäne |
| `Domain.Projections` | Projektionen, Reader, Queries, Store-Fähigkeiten |
| `Domain.Pipeline` | Flüsse und Funktions-Implementierungen samt Diensten |
| `Domain.Infrastructure` | Store-Implementierungen (Marten); ihre Basisklasse `MartenCoCommitStoreBase` liegt im Framework (`Core`) |
| `Domain.Akteure` | Akteure und Client-Verträge |
| `Domain.Client.Modules.Blazor` | die Blazor-Module des Arbeitsplatz-Clients |
| `Domain.Client.Worker.Python.ML` (außerhalb der `.sln`) | Python-Worker: Funktions-Implementierungen und Zusagen der Akteure |

### Tests
| Projekt | Rolle |
|---|---|
| `Infrastructure.Pruefstand.Tests` | Ebene 1: store-frei, im Speicher |
| `Infrastructure.Integration.Tests` | Ebene 2: gegen echtes Marten/Consul/Redis, sequentiell |
| `Client.Infrastructure.CollectionTests` | C#-Client: Virtualisierung, Zustellverfolgung |
| `Cqrs.Testing` | Test-DSL für den Prüfstand (`Szenario`, `SagaSzenario`, `FlussLaufwerk`) |

Zahlen: [12](12-tests-und-vermessung.md).

## 1.4 Der Gesamt-Datenfluss

```
 Eingänge
 ─────────────────────────────────────────────────────────────────────────────────────────────────────────
  Clients (Blazor, Python)         Werkbank                         Quellen (Konfig Quellen:<Typ>)
  gRPC CqrsClientService           JSON-Eingang /eingang/*           Datei-Treiber · Webhook (Einblick-Port)
        │ Handshake: Vertrag+Token        │ Token                            │
        └──────────► Akteur-Tor ◄─────────┘                                  ▼
                       │ Befugnis (IDarf), Akteur ins Envelope           QuellEingang
                       ▼                                                  │ Quell-Nachricht genau einmal
             AggregateDispatcher                                          │ je Kennung ins Log,
                       ▼                                                  │ weckt die Flüsse, die damit beginnen
          Aggregat-Actor (Single-Activation)                              │
            Decide (reiner Fachcode) → OneOf<Events>                      │
                       ▼                                                  │
          BatchingEventAppender (Group-Commit)                            │
                       ▼                                                  ▼
 ═══════════════ Marten/PostgreSQL: der Log (jeder Eintrag mit Spur: ursache · wirker · ueber · stand) ═════════
                       │                                               │
           Signal (StreamId, Version), verlierbar                Redis-Versions-Index (abgeleitet)
           + Poll (30 s)                                               
             ┌─────────┴───────────────────────────┐
             ▼                                     ▼
   ProjectionAdapter (Pull-/Signal-Schleife)    Prozess-Manager = Dirigent
     Projektion (replaybar, Co-Commit)            Prozesse (Event-Regeln) und Flüsse (Quelle → Funktionen → Commands)
     Reaktion (emittierend)                         │                      │
             │                                      │ Sende<Cmd>           │ Rufe<F>
             └──────────────► CommandEmitter ◄──────┘                      ▼
                         (der eine Emit-Weg)                     Funktions-Vermittler (Actor je Funktion)
                                │                                          │ Pull: Abholer holen Aufträge
                                ▼                                          ▼
                     zurück zum AggregateDispatcher         Laufort Host: FunktionsAbholer → FunktionsAusfuehrer
                                                            Laufort Extern: Python-Worker über gRPC
                                                            Laufort Hand: HandAusfuehrer (die Werkbank antwortet)
                                                                           │ genau ein Ergebnis im Ausführungs-Stream,
                                                                           ▼ Weckung des Dirigenten
                                                                         Log
 Einblick-API (/einblick/*: Spur, Vorgänge, Aufträge, SSE) ──────────────────────────────► Werkbank (Spur-Spiegel)
```

**Zwei Achsen prägen das Bild:**
- **Schnell vs. durabel:** Das Signal ist schnell und verlierbar; Poll und Log heilen alles.
- **Replaybar vs. emittierend:** Projektionen dürfen zurückgesetzt und neu aufgebaut werden; Emittenten (Reaktion, Prozess, Fluss) bewegen
  echte Wirkung und werden nicht blind wiederholt. Welche Seite gilt, folgt aus den Schreib-Fähigkeiten der Handles (Details: [04](04-konsum-und-prozess-maschine.md)).

## 1.5 Was das System besonders macht

- **Vier Konsumenten, eine Maschine.** Projektion, Reaktion, Prozess und Fluss teilen dieselbe Pull-/Signal-Mechanik; es gibt keine
  Taxonomie. Ob ein Konsument replaybar oder emittierend ist (Achse B), liest der Generator aus den Schreib-Fähigkeiten seiner Handles.
  Siehe [04](04-konsum-und-prozess-maschine.md).
- **Compile-Zeit statt Laufzeit.** Analyzer, Generator-Diagnosen und Boot-Guards machen ganze Fehlerklassen zu Build- bzw. Startfehlern
  („Prüfen = Bauen“). Siehe [05](05-generatoren-analyzer-proto.md).
- **Abläufe als typisierte DSL.** Prozesse (`IProzessDefinition`, Event-Regeln mit `Sende<Cmd>`/`Rufe<F>`) und Flüsse (`IPipeline`, vom Draht
  aus) beschreiben nur die Regel; Dirigent, Korrelation, Marking und Kompensation sind einmal geschrieben.
- **Ein Weg je Sache.** Commands entstehen nur über den `CommandEmitter` (CQRS020/021); Ausgaben stehen nur in der Signatur (CQRS050).
- **Dieselben Invarianten in den Clients.** Blazor und Python routen typbasiert, sprechen mit Vertrag und Hash, wirksam ist Vertrag ∩ Token
  ([konzept-akteure.md](konzept-akteure.md)).

## 1.6 Die Werkbank

Die Werkbank ist die eine Oberfläche, auf der eine Anwendung entworfen, als Code erzeugt, gebaut, gestartet, durchgespielt und nachvollzogen wird.
Der Code ist das Modell: das Board wird über Roslyn aus dem Code gelesen, jede Geste ist sofort ein Patch am Code. Durchgespielt wird der echte
Host in der Umgebung `sim` gegen eine eigene Datenbank; was geschah, zeigt die Spur im Log. Kanonisch: [konzept-werkbank.md](konzept-werkbank.md).

## 1.7 Offenes

Offene Punkte und Schulden stehen gesammelt in [13](13-reifegrad-schulden-bewertung.md) §13.0 (Index mit IDs).
