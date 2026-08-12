# 05 — Generatoren, Analyzer & Proto-Flow

Alles Dispatchende wird zur Compile-Zeit erzeugt (Invariante 4). Es gibt **drei
Ausführungsmodelle**: In-Compilation-Generatoren (laufen bei jedem Build mit), ein reiner
Analyzer (nur Diagnostics), und ein **manuell** laufendes Konsolentool (Proto).

## 5.1 Die sechs Generator-Projekte

| Projekt | TFM | Modell |
|---|---|---|
| `Abstractions.SourceGeneration` | net9.0 | reine Modell-Bibliothek (`TypeNode`), kein `[Generator]` |
| `Core.SourceGeneration` | net9.0 | geteilter Analyse-Kern (Domain-Graph, Multi-Compilation) |
| `Domain.SourceGeneration` | netstandard2.0 | In-Compilation, **Syntax**-basiert (Domain-Seite) |
| `Infrastructure.SourceGeneration` | netstandard2.0 | In-Compilation, **Symbol**-basiert + der Analyzer |
| `Client.SourceGeneration` | netstandard2.0 | In-Compilation (Blazor-Client) |
| `Proto.SourceGeneration` | net9.0 (Exe) | **manuell** laufendes Tool |

**Analyzer-Verdrahtung** (bestimmt die Sichtbarkeit, §5.6): `Domain.SourceGeneration` hängt an
`Domain`, `Domain.Projections`, `Domain.Pipeline`; `Infrastructure.SourceGeneration` nur an
`Infrastructure`; `Client.SourceGeneration` an `Domain.Client.Modules.Blazor`.

## 5.2 Vollständige Generator-Tabelle

**Input-Legende:** *Syntax* = liest die Syntax der eigenen Compilation (sieht nur im aktuellen
Projekt deklarierte Typen). *Symbol* = wandert `GlobalNamespace` und sieht auch referenzierte
Assemblies.

### Domain.SourceGeneration (Syntax)
| Generator | Input (Marker) | Output → Typ | Zweck |
|---|---|---|---|
| `Generator` | `IDecider<T>`/`IApplier<T>` | `{State}.State.g.cs` | injiziert `State`-Property + Ctor |
| `AggregateHandlerGenerator` | `IState` + nested Decider+Applier | `{State}AggregateHandler` | `HandleCommand`/`ApplyEvent`-Switch, entpackt `OneOf` |
| `HandlerFactoryGenerator` | `IState` | `AggregateHandlerFactory` (ns `Infrastructure`) | State-Typ-Switch → Handler |
| `StatePropertyGenerator` | `IState` | `{State}.State.g.cs` | `Guid Id` + `int Version` |
| `SignalTypeGenerator` | `IEvent` & !`ITransient` | `StateChangeVia{Event}` | Weckruf-Signal je Event (bewusst syntax-lokal) |
| `SubscriberDispatchGenerator` | `ISubscriber` + `Handle(...)` | `{Sub}.Dispatch.g.cs` | `SubscribedTypes` + `DispatchAsync` |
| `ProjectionReaderDispatchGenerator` | `IReader<T>` + `Handle(TQuery,…)` | `{Reader}.Dispatch.g.cs` | Query-Routing |
| `ProjectionQueryServiceGenerator` | `IReader<T>` | `ProjectionQueryService` (ns `Domain.Projections`) | zentraler Query-Service |
| `PipelineDispatchGenerator` | `IPipelineHandler` + `Handle(...)` | `{Pipe}.Dispatch.g.cs` | Trigger/Event/Self-Dispatch |
| `ProzessRegelnGenerator` | `IProzessDefinition` | `GeneratedProzessRegeln` | DAG-Registry Name→Regeln (**CQRS012**) |
| `ProzessRegelDiagnosticGenerator` | `IProzessDefinition` + `IDecider` | — (nur Diagnostics) | **CQRS001/002/003** |

### Infrastructure.SourceGeneration (Symbol)
| Generator | Input | Output → Typ | Zweck |
|---|---|---|---|
| `CommandAggregateMapGenerator` | `IDecider<T>.Decide(TCommand)` + OneOf-Rückgaben | `GeneratedCommandRouting` (ns `Infrastructure.Mapping`) | **Dispatch-Kern**: `CommandToAggregate` + `CommandToEvents` + `Produziert()` (**CQRS010/011**) |
| `AggregateActorGenerator` | `IState` (+ `[AggregatName]`) | `AggregateActors`, `GeneratedAggregates` | Proto.Actor-Actor + DI/ClusterKinds |
| `PipelineActorGenerator` | `IPipelineHandler` | `PipelineActors`, `GeneratedPipelines` | Pipeline-Actors + Pull-Kind-Wiring |
| `PullPathGenerator` | `ISubscriber` **&** `IPullSubscriber` | `GeneratedPullPaths` | kompletter Pull-Pfad je Konsument (Store per DI) |
| `SignalFactoryGenerator` | `IStateChangeSignal<T>` | `GeneratedSignalFactory`, `…Routes` | reflexionsfreie Event→Signal-Factory |
| `SnapshotRegistrationGenerator` | `IState` | `GeneratedSnapshotRegistration` | Marten-Registrierung + **FNV-1a-Struktur-Hash** als Schema-Version |
| `EventJsonGenerator` | `IEvent` & !transient & !self | `GeneratedEventJson` | reflexionsfreier Event-JSON-Dispatch |
| `WireSerializerGenerator` | `IWireMessage` + Payload-Wurzeln | `GeneratedWire` + `GeneratedWirePoly` | Cross-Node-Wire-Dispatch |
| `EventUpcastingGenerator` | `IUpcast<..>` (Arity 2/3/4) | `GeneratedEventUpcasting` | typisierte Schema-Evolution (**CQRS040–046**) |
| `DtoMapperSourceGenerator` | `IMessagePayload`/`IQuery`/`IPipelineTrigger` via Domain-Graph | `ProtoMessageMapper.Generated` | Domain↔Proto-DTO-Mapper (**CQRS030**) |
| `TypeRegistryGenerator` | 6 Interface-Kategorien | `GeneratedTypeRegistry` | Runtime-Typ-Registry (String↔Type) |

### Client.SourceGeneration (Syntax, außer `WiringGenerator`)
| Generator | Input | Zweck |
|---|---|---|
| `HandleMethodGenerator` | `Handle(TEvent, MessageContext)` | Store-/Handler-Dispatch nach Rückgabetyp |
| `ViewModelGenerator` | `IViewModel` + `_camelCase`→Cmd/Query/Event | `_publish`/`__InitBus` + öffentliche Methoden + `IRelayCommand` |
| `ModuleRegistryGenerator` | `IUiModule` | DI-Registrierung |
| `WiringGenerator` | gemischt (Syntax + `CompilationProvider` über referenzierte Assemblies) | **eine** aggregierte Wiring-Klasse (DI + Subscription depth-first) |

### Core.SourceGeneration (kein `[Generator]`)
`DomainGraphAnalyzer`, `MultiCompilationAnalyzer`, `TypeAggregator`, `CompilationTypeResolver`,
`TypeMappingHelper`, `NameSanitizer` — das string-basierte `TypeNode`-Substrat, geteilt von
DtoMapper (in-compilation) und Proto-Tool (multi-compilation).

## 5.3 Die 15 Diagnose-Codes (CQRS0xx)

Alle `Error` (der Build bricht), sofern nicht anders vermerkt.

| Code | Titel | Erzwingt |
|---|---|---|
| **CQRS001** | Doppelter Prozess-Auslöser | ein Event startet höchstens EINEN Prozess |
| **CQRS002** | Command ohne Decider-Handler | jeder Saga-Command wird von einem `Decide` behandelt |
| **CQRS003** | Command-Typ muss explizit sein | `Sende<T>`/`SendeJe`/`RückgängigDurch` tragen `T` explizit |
| **CQRS010** | Command von mehreren Aggregaten | jeder Command genau EIN Decider (eindeutiges Routing) |
| **CQRS011** | Zwei Aggregate gleicher Identität | State-Namenskollision bricht Build |
| **CQRS012** | Zwei Prozesse gleichen Namens | Prozess-Name eindeutig |
| **CQRS020** | Roher Command-Send | `RequestAsync<CommandResult>` nur in Emitter/Dispatcher |
| **CQRS021** | Unbounded Command-Kante | kein `CancellationToken.None`/`default` auf Command-Kante |
| **CQRS030** | DTO-Mapper-Gen fehlgeschlagen | Generator-Exception wird lauter Fehler, nicht still |
| **CQRS040** | Mehrdeutige Upcast-Kante | eine Version → genau eine ausgehende `IUpcast` |
| **CQRS041** | Upcast-Kette endet nicht aktuell | Kette erreicht eine aktuelle Gestalt |
| **CQRS042** | Zyklus in Upcast-Kette | Upcasting azyklisch |
| **CQRS044** | Merge (N:1) verboten | keine mehreren Kanten auf dasselbe Ziel |
| **CQRS045** | Split-Sekundärziel mit eigener Kette | in Stufe 2 nur das erste Ziel setzt die Kette fort |
| **CQRS046** | Split (1:N) nicht produktionsreif | **jeder** 1:N-Upcaster bricht bewusst den Build, bis die Consumer-Fabric steht |

Einziger echter `DiagnosticAnalyzer` ist `CommandEmitAnalyzer` (syntaktischer Vorfilter
`RequestAsync<T>` + semantische Bestätigung `T == CommandResult`). Alle anderen Diagnostics
reiten auf `ISourceGenerator`-Läufen mit. Die **Domain-Dispatch-Generatoren melden gar keine
Diagnostics** — sie signalisieren Fehler durch generierten Laufzeit-`throw` (`_ =>
NotSupportedException`).

## 5.4 Der Dispatch-Kern

**`GeneratedCommandRouting`** ist die Routing-Wahrheit. Input: **jede
`IDecider<TState>.Decide(TCommand)`-Signatur** (`TCommand` gehört strukturell zu `TState`, nicht
per Namespace). Output: `CommandToAggregate` + `CommandToEvents` (aus den
`IEnumerable<OneOf<E1,E2,…>>`-Rückgaben, transiente gefiltert) + `Produziert()`. Letzteres ist
die präzise Command→Event-Kantenmenge und speist den Azyklizitäts-Boot-Guard.

> **Historie:** `GeneratedEventCommandMapping` existiert **nicht mehr** (gelöscht, nur noch
> Kommentar-Referenzen). Nachfolger ist `GeneratedCommandRouting`.

**Wire-Serializer, Upcasting** — siehe [06](06-transport-multinode-betrieb.md) bzw. das
Feature-Inventar [11](11-feature-inventar.md).

## 5.5 Warum reflexionsfrei — der Verriegelungs-Trick

Jeder dispatchende Pfad bindet an `CqrsWireJsonContext.Default.{Typ}` (STJ-Source-Gen) statt an
`JsonSerializer.Deserialize(type, …)`. Da die Generatoren die Property `Ctx.Default.{Typ}`
**namentlich** referenzieren, bricht der Build, wenn die Typmenge driftet und der Typ im
hand-gepflegten Context fehlt. So ist die Kopplung „Dispatch ↔ Serialisierungs-Manifest"
strukturell erzwungen. Durchgängig: `Sort`/`OrderBy(Ordinal)` vor jedem Emit → stabile Diffs,
reproduzierbare Builds.

## 5.6 Sichtbarkeitsgrenzen (architektonisch tragend)

**Ein Domain-Generator sieht fremdes Generat NICHT (Syntax); ein Infrastructure-Generator
schon (Symbol).**
- **Domain-Seite** (Syntax): sieht nur im aktuellen Projekt deklarierte Typen. Deshalb muss
  `SignalTypeGenerator` syntax-lokal bleiben (sonst doppelte Signale in mehreren Assemblies),
  und Aggregat-Generat **muss selbst-enthalten** sein.
- **Infrastructure-Seite** (Symbol): sieht die kompilierten Domain-Symbole. Deshalb liegt das
  *aggregatübergreifende* Routing hier, nicht in Domain.
- **Client-Seite**: syntax-lokal, außer `WiringGenerator`, das per `CompilationProvider`
  bewusst referenzierte Assemblies scannt.

Konsequenz: `AggregateActorGenerator` und `CommandAggregateMapGenerator` berechnen die
Aggregat-Identität unabhängig und müssen byte-genau übereinstimmen — als Kommentar-Vertrag
festgehalten, **nicht** maschinell erzwungen (Schuld).

## 5.7 Der Proto-Flow (manuell, dreistufig)

```
Proto.SourceGeneration (Exe)   ── MANUELL: dotnet run --project Proto.SourceGeneration
  MSBuildWorkspace lädt Domain/Domain.Projections/Domain.Pipeline
  MultiCompilationAnalyzer + TypeAggregator → File.WriteAllText → ProtoRepo/domain.proto
        ▼
ProtoRepo                       ── dotnet build
  Grpc.Tools kompiliert domain.proto → {Type}Dto C#-Klassen
        ▼
Infrastructure (build)
  DtoMapperSourceGenerator liest Domain-Symbole + erzeugt Domain↔{Type}Dto-Mapper
```

Der **manuelle Schritt** ist zwingend, weil `Proto.SourceGeneration` ein Exe ist. Ablauf bei
jedem neuen Command/Event/Query/Trigger: `dotnet run --project Proto.SourceGeneration` →
ProtoRepo bauen → Infrastructure baut. Vergisst man ihn, kompiliert der DtoMapper gegen
fehlende `{Type}Dto` → Build-Fehler (dank CQRS030 laut statt still). **Signale sind bewusst
ausgenommen** — sie queren nie die Proto-Grenze.

## 5.8 Fragilität / Schulden

- **`DtoMapperSourceGenerator` (1317 Z.) ist string-basiert** über `TypeNode.FullName` statt
  Symbole. Positiv: die früher hartkodierte Enum-Liste ist inzwischen dynamisch aus dem
  Domain-Graph (die CLAUDE.md-Notiz „hartkodierte Enums" ist **veraltet**); die
  String-Fragilität bleibt.
- **Zwei divergierende Proto-Typ-Maps**: `Core.SourceGeneration/TypeMappingHelper.cs` und
  `Proto.SourceGeneration/FileGenerator.cs` duplizieren die C#→Proto-Map und **stimmen bereits
  nicht überein** (`bool?`→`bool` vs. `bool?`→`int32`) — latenter Serialisierungs-Drift an der
  gRPC-Grenze.
- **Verlustbehaftete Proto-Skalare**: `DateTime`→`int64`, `decimal`/`Guid`→`string`. Für die
  interne Actor-Ebene irrelevant (dort läuft der Wire-/EventJson-Pfad), an der externen
  gRPC-Grenze semantisch verlustbehaftet.
- **Manueller Proto-Schritt** ist ein prozessualer Single-Point-of-Failure ohne
  Build-Verriegelung (erst der Folgefehler zeigt Stale).
- **Encoding-Schäden (Mojibake)** in Generator-Kommentaren (`MultiCompilationAnalyzer.cs`,
  `AggregateHandlerGenerator.cs`) — kosmetisch, aber Zeichen von Encoding-Drift.
- **CQRS046 als absichtliche Sackgasse**: 1:N-Upcasting ist generator-/leseseitig codiert, wird
  aber von jedem realen Split-Deklarat blockiert, bis die Consumer-Fabric steht.

**Kernbefund:** Die interne Actor-/Event-Ebene ist konsequent symbol-typisiert und
reflexionsfrei. Die **Proto/DTO-Schiene an der externen gRPC-Grenze** ist die schwächste
Stelle — string-basiert, dupliziert, manuell, verlustbehaftet — und architektonisch bewusst
vom Kern isoliert.
