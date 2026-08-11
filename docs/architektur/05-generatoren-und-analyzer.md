# 05 — Generatoren & Analyzer

> Invariante 4: keine Runtime-Reflection — alles Dispatchende wird zur Compile-Zeit
> generiert. Verwandt: [00 Überblick](00-ueberblick.md).

## Zwei Roslyn-Ebenen + ein Standalone-Tool

- **`Domain.SourceGeneration`** (läuft auf Domain / Domain.Projections / Domain.Pipeline):
  **Syntax-Ebene** — sieht nur die im eigenen Projekt deklarierte Syntax, **nicht** fremdes
  Generat.
- **`Infrastructure.SourceGeneration`** (läuft auf Infrastructure): **Symbol-Ebene** — sieht
  die kompilierte Domain.dll über die Compilation, **inkl. deren Generat**.
- **`Abstractions.SourceGeneration` / `Core.SourceGeneration`**: keine `[Generator]` — reine
  Analyse-Bibliotheken (`DomainGraphAnalyzer`, `TypeAggregator`, `TypeNode` …), vom
  `DtoMapperGenerator` konsumiert.
- **`Proto.SourceGeneration`**: **kein** Roslyn-Generator, sondern ein manuell auszuführendes
  Konsolen-Tool (`dotnet run`), das `ProtoRepo/domain.proto` schreibt.

Diese Ebenen-Trennung ist entscheidend: ein Aggregat-Generat, das ein Domain-Generator
liest, muss selbst-enthalten sein (der Domain-Generator sieht kein Nachbar-Generat); die
Verkettung „Domain-Generat → Infrastructure-Generator" funktioniert dagegen über Symbole.

## Infrastructure-Generatoren (Symbol-Ebene)

| Datei | Input | Output |
|---|---|---|
| `AggregateActorGenerator` | `IState`-Aggregate (`[AggregatName]`/Typname) | `{Name}Actor`, `GeneratedAggregates` (DI + ClusterKind) |
| `CommandAggregateMapGenerator` | `IDecider.Decide`-Signaturen + OneOf-Rückgaben | **`GeneratedCommandRouting`** (`CommandToAggregate`, `CommandToEvents`, `Produziert`) + CQRS010/011 |
| `PipelineActorGenerator` | `IPipelineHandler` | `{Name}PipelineActor`, `GeneratedPipelines`, `AddGeneratedPipelineEventPulls`, `{Name}EventPullKind` |
| `PullPathGenerator` | `ISubscriber` **+** `IPullSubscriber` | `{Name}PullAdapterKind`, `GeneratedPullPaths.AddGeneratedPullPaths()`, `PushSubscriberExclusions` |
| `SignalFactoryGenerator` | `IStateChangeSignal<TEvent>`-Marker | `GeneratedSignalFactory.Create`, `GeneratedSignalRoutes.EventToSignal` |
| `EventJsonGenerator` | persistierte `IEvent` vs. `EventJsonSerializerContext` | `GeneratedEventJson` (Serialize/Deserialize/Diskriminator) |
| `TypeRegistryGenerator` | alle Message-Typen | `GeneratedTypeRegistry` (+ `PersistableEventTypes`) |
| `SnapshotRegistrationGenerator` | `IState`-Aggregate | `RegisteredSnapshotTypes.Register`, `GeneratedSnapshotSchema.VersionOf<T>()` |
| `DtoMapperGenerator` | Domain-Payloads via `DomainGraphAnalyzer` | `ProtoMessageMapper.Generated.cs` (Proto-DTO ↔ Domäne) |
| `CommandEmitAnalyzer` *(Analyzer)* | `RequestAsync<CommandResult>`-Invocations | CQRS020/021 (kein Generat) |

## Domain-Generatoren (Syntax-Ebene)

`ProzessRegelnGenerator` (→ `GeneratedProzessRegeln.Alle` + CQRS012),
`ProzessRegelDiagnosticGenerator` (CQRS001/002/003), `SignalTypeGenerator` (→
`StateChangeVia{Event} : IStateChangeSignal<Event>`), `SubscriberDispatchGenerator` (→
`DispatchAsync`), `PipelineDispatchGenerator` (→ `DispatchTrigger/Event/SelfAsync`),
`ProjectionReaderDispatchGenerator`, `ProjectionQueryServiceGenerator`,
`AggregateHandlerGenerator`, `FactoryGenerator`, `StateGenerator`, `Generator` (State-Glue).

## EM-1-Erzwingung: `CommandEmitAnalyzer`

Ein `[DiagnosticAnalyzer]` (`OutputItemType="Analyzer"`), läuft automatisch auf dem gesamten
Infrastructure-Compile:

1. **Vorfilter:** `x.RequestAsync<T>(...)` mit genau 1 Typ-Argument.
2. **Semantik:** `T` ist exakt `Abstractions.CommandResult` — der Pipeline-**Trigger**-Pfad
   hat einen anderen Rückgabetyp und fällt bewusst raus.
3. **CQRS020** (Build-Fehler): `ContainingType` nicht in der Allow-Liste. Allow = die zwei
   legitimen Sender: `Infrastructure.PubSub.CommandEmitter` (das Emit-Primitiv) und
   `Infrastructure.Aggregate.ActorSystem.ProtoActorAggregateDispatcher` (Client-/OCC-Pfad).
4. **CQRS021** (Build-Fehler): letztes Argument ist `CancellationToken.None`/`default` —
   gilt auch innerhalb der erlaubten Typen (Regressions-Riegel gegen die Hang-Klasse).

So wird „genau EIN Emit-Weg" zur Compile-Zeit-Invariante statt Konvention.

## Diagnostik-Codes

| Code | Emittiert von | Bedeutung |
|---|---|---|
| CQRS001 | `ProzessRegelDiagnosticGenerator` | Doppelter Prozess-Auslöser |
| CQRS002 | `ProzessRegelDiagnosticGenerator` | `Sende<TCmd>` ohne behandelnden Decider |
| CQRS003 | `ProzessRegelDiagnosticGenerator` | `Sende(...)` ohne explizites Command-Typ-Argument |
| CQRS010 | `CommandAggregateMapGenerator` | Command von >1 Aggregat behandelt (nicht eindeutig routbar) |
| CQRS011 | `CommandAggregateMapGenerator` | Zwei Aggregate mit gleicher Identität (ClusterKind-Kollision) |
| CQRS012 | `ProzessRegelnGenerator` | Zwei Prozesse mit gleichem aufgelöstem Namen |
| CQRS020/021 | `CommandEmitAnalyzer` | EM-1-Erzwingung (siehe oben) |

Zu den Attributen: `[AggregatName]` wird an ZWEI Stellen identisch gelesen —
`CommandAggregateMapGenerator` (Routing) **und** `AggregateActorGenerator` (ClusterKind);
beide müssen denselben Wert liefern. `[ProzessName]` liest nur der `ProzessRegelnGenerator`.
Default = einfacher Typname → keine Migration nötig.

## Generator-Ketten

- **`GeneratedCommandRouting`** ist die **einzige** Routing-Wahrheit:
  `GeneratedPipelines.CommandAggregateTypes` ist nur Passthrough darauf; `Produziert` speist
  den Azyklizitäts-Boot-Guard; zur Laufzeit konsumieren es `HandlerOutputRouter`,
  `ProzessManagerActor`, `PipelineActorBase`.
- **`SignalTypeGenerator` (Domain) → `SignalFactoryGenerator` (Infra)** — typ-getrieben über
  das Typ-Argument von `IStateChangeSignal<TEvent>` (kein Namens-Präfix).
- **`EventJsonGenerator`** koppelt gegen `EventJsonSerializerContext` (fehlt ein Event, bricht
  der Build); Diskriminator (snake_case) identisch zu `TypeRegistryGenerator` und Martens
  `MapEventType`.

## Abgelöst / gelöscht (nur noch als Kommentare)

`SubscriberActorGenerator` (Push-Treiber), `ProzessAggregatGenerator`/`ProzessWiringGenerator`
(alte Schrittlisten-Welt), `EventCommandMappingGenerator`/`GeneratedEventCommandMapping`
(namespace-grob, ersetzt durch das Decider-präzise `GeneratedCommandRouting`).

## Schulden

- **`DtoMapperGenerator`** (~1325 Z.) ist der fragilste Generator: hartkodierte
  Domänen-Enums (`Klassifikation`/`BildVersion`), Encoding-Schäden in Kommentaren, tote
  if-Zweige, `try/catch` schluckt Fehler in Debug-Output, kein Incremental.
- `CancellationToken.None` auf den Emit-Factory-Pfaden (`PullPathGenerator`,
  `PipelineActorGenerator` `{Name}EventPullKind`) — vom CQRS021-Analyzer nicht erfasst
  (anderer Rückgabetyp), Emit selbst gebounded.
- Kommentar-Drift: `PipelineActorGenerator`-Klassenkommentar nennt noch die alte
  Namespace-Konvention, obwohl längst auf `GeneratedCommandRouting` umgestellt.

## Generat inspizieren

Generatoren laufen in-memory; `obj/generated`-`.g.cs` sind stale gecacht. Zum Inspizieren
`EmitCompilerGeneratedFiles` erzwingen + `dotnet build-server shutdown`.

## Schlüsseldateien

`Infrastructure.SourceGeneration/CommandEmitAnalyzer.cs`, `CommandAggregateMapGenerator.cs`,
`EventJsonGenerator.cs`, `PipelineActorGenerator.cs`, `PullPathGenerator.cs`,
`SignalFactoryGenerator.cs`, `AggregateActorGenerator.cs`, `TypeRegistryGenerator.cs`,
`SnapshotRegistrationGenerator.cs`, `DtoMapperGenerator.cs`;
`Domain.SourceGeneration/ProzessRegelnGenerator.cs`, `ProzessRegelDiagnosticGenerator.cs`,
`SignalTypeGenerator.cs`, `SubscriberDispatchGenerator.cs`, `PipelineDispatchGenerator.cs`;
`Proto.SourceGeneration/Program.cs` (Standalone-Tool).
