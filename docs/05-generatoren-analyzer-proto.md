# 05 — Generatoren, Analyzer & Codegen

Stand 2026-10-13, gegen den Code geprüft. Maßstab: docs/konzept-werkbank.md.

Alles Dispatchende wird zur Compile-Zeit erzeugt, ohne Runtime-Reflection (Invariante 4). Drei Bausteine tragen das, alle laufen bei jedem
`dotnet build` mit:
1. **In-Compilation-Generatoren** (Roslyn `[Generator]`) erzeugen Dispatch, Registrierungen und Tabellen.
2. **Analyzer** (`[DiagnosticAnalyzer]`) und Generator-Diagnosen machen Regeln zu Build-Fehlern — **Prüfen = Bauen**. Was sich erst beim
   Start zeigt, prüfen Boot-Guards.
3. **Der Codegen-Schritt im Build** (`Cqrs.Codegen`) erzeugt die Artefakte, die ein nachgelagertes Werkzeug braucht (protoc, der
   STJ-Source-Generator, Python) und die ein In-Compilation-Generator deshalb nicht liefern kann.

## 5.1 Projekte und Verdrahtung

| Projekt | TFM | Rolle |
|---|---|---|
| `Abstractions.SourceGeneration` | net9.0 | Modell-Bibliothek (`TypeNode`), kein `[Generator]` |
| `Core.SourceGeneration` | net9.0 | Analyse-Kern ohne `[Generator]`: `DomainGraphAnalyzer`, `TypeAggregator`, `CompilationTypeResolver`, `TypeMappingHelper`, `ProtoScalarSpec`, `NameSanitizer` |
| `Domain.SourceGeneration` | netstandard2.0 | Generatoren der Domänen-Seite (syntax-lokal) und acht Analyzer |
| `Infrastructure.SourceGeneration` | netstandard2.0 | Generatoren der Laufzeit-Seite (symbol-basiert) und zwei Analyzer |
| `Projections.SourceGeneration` | netstandard2.0 | DI-Verdrahtung der Leseseite und ein Analyzer |
| `Client.SourceGeneration` | netstandard2.0 | Generatoren des Blazor-Clients und ein Analyzer |
| `Cqrs.Codegen` | net9.0 (Exe) | Werkzeug: der Codegen-Schritt im Build (§5.7) |
| `Proto.SourceGeneration` | net9.0 (Bibliothek) | liefert den `FileGenerator` (domain.proto), den `Cqrs.Codegen` nutzt |

`Core.SourceGeneration` und `Abstractions.SourceGeneration` werden in `Infrastructure.SourceGeneration` per Link mitkompiliert. Ebenso
hängen die **Verträge** als verlinkte Quelle in Generator und Laufzeit — eine Regel, eine Datei: `Abstractions/Aggregatvertrag.cs`,
`Akteurvertrag.cs`, `Dienstvertrag.cs`, `Quellen/Treibervertrag.cs`, `Kodehash.cs`, `Spur/WirkerId.cs`, `Projektstruktur.cs` (docs/07), dazu
`Infrastructure.SourceGeneration/Clientvertragslesart.cs` (dieselbe Lesart für Server und C#-Client), `Ctorbindung.cs` und
`Domain.SourceGeneration/Lageart.cs` (WAS ein Typ für die Lage-Prüfung ist; dieselbe Lesart für CQRS098 und `--check`).

**Analyzer-Verdrahtung** (bestimmt die Sichtbarkeit, §5.6):

| Generator-Projekt | hängt als Analyzer an |
|---|---|
| `Domain.SourceGeneration` | `Domain`, `Domain.Projections`, `Domain.Pipeline`, `Domain.Akteure` |
| `Projections.SourceGeneration` | `Domain.Infrastructure` |
| `Infrastructure.SourceGeneration` | `Infrastructure` |
| `Client.SourceGeneration` | `Domain.Client.Modules.Blazor` |

Die Symbol-Generatoren und der `GesamtbildAnalyzer` sehen die Domäne nur, weil `Infrastructure.csproj` die Domänen-Projekte (`Domain`,
`Domain.Projections`, `Domain.Pipeline`, `Domain.Infrastructure`) referenziert. `Infrastructure` ist damit zugleich Framework und
Komposition — eine offene Kopplung (docs/13 W2-1).

## 5.2 Die Generatoren

**Input-Legende:** *Syntax* = liest die Syntaxbäume der eigenen Compilation (sieht nur im aktuellen Projekt deklarierte Typen).
*Symbol* = wandert `GlobalNamespace` und sieht auch referenzierte Assemblies. Dateiname und Klassenname weichen teils ab (Klasse genannt).

### Domain.SourceGeneration (Syntax)
| Generator | Input (Marker) | Output | Zweck |
|---|---|---|---|
| `Generator` | `IDecider<T>`/`IApplier<T>` | `{State}.{Klasse}.State.g.cs` | injiziert `State`-Property + Ctor in Decider/Applier |
| `StatePropertyGenerator` | `IState` | `{State}.State.g.cs` | `Guid Id` + `int Version` |
| `AggregateHandlerGenerator` | `IState` + nested Decider/Applier | `{State}AggregateHandler` | `HandleCommand`/`ApplyEvent`-Switch, entpackt `OneOf`; `CodeHash` (State + Applier) für Snapshots |
| `HandlerFactoryGenerator` | `IState` | `AggregateHandlerFactory` (ns `Infrastructure`) | State-Typ → Handler |
| `SignalTypeGenerator` | `IEvent` & nicht transient | `StateChangeVia{Event}` | Weckruf-Signal je persistiertem Event (bewusst syntax-lokal) |
| `SubscriberDispatchGenerator` | `ISubscriber` + `Handle(…)` | `{Sub}.Dispatch.g.cs` | `SubscribedTypes` + `DispatchAsync` mit Fähigkeits-Parametern |
| `ProjectionReaderDispatchGenerator` | `IReader<T>` + `Handle(TQuery,…)` | `{Reader}.Dispatch.g.cs` | Query-Routing |
| `ProjectionQueryServiceGenerator` | `IReader<T>` | `ProjectionQueryService` (ns = Leseseiten-Projekt) | zentraler Query-Service |
| `ProzessRegelnGenerator` | `IProzessDefinition`, `IPipeline` | `{Assembly}.Prozess.GeneratedProzessRegeln` | Registry Name → Regeln, Regel- und Knoten-Namen (CQRS012, CQRS072) |
| `LageAnalyzer` | Rollen-Marker (`Lageart`) | — (nur Diagnosen) | CQRS098: Primitive im Projekt ihrer Rolle (docs/07 §7.2) |
| `ProzessRegelDiagnosticGenerator` | `IProzessDefinition` + `IDecider` | — (nur Diagnosen) | CQRS001–003 |
| `PipelineDispatchGenerator` | `IPipelineHandler` | `{Pipe}.Dispatch.g.cs` | **Altlast** (Handle-Pipeline, ohne Domänen-Nutzer, docs/13 W11-1) |

### Infrastructure.SourceGeneration (Symbol)
| Generator | Input | Output | Zweck |
|---|---|---|---|
| `CommandAggregateMapGenerator` | jede `IDecider<T>.Decide(TCommand)` + OneOf-Rückgaben | `GeneratedCommandRouting` (ns `Infrastructure.Mapping`) | **Dispatch-Kern**: `CommandToAggregate`, `CommandToEvents`, `Produziert()` (CQRS010/011) |
| `AggregateActorGenerator` | `IState` | `AggregateActors`, `GeneratedAggregates` | Proto.Actor-Actor + Cluster-Kinds |
| `PullPathGenerator` | `ISubscriber` & `IPullSubscriber` | `GeneratedPullPaths` | Pull-Pfad je Konsument; Achse B aus den Schreib-Fähigkeiten der Handles (CQRS052, [04 §4.2](04-konsum-und-prozess-maschine.md)) |
| `SignalFactoryGenerator` | `IStateChangeSignal` | `GeneratedSignalFactory`, `GeneratedSignalRoutes` | Event → Signal ohne Reflection |
| `SnapshotRegistrationGenerator` | `IState` | `GeneratedSnapshotRegistration` | Marten-Registrierung; Schema-Version = FNV-1a-Struktur-Hash + `CodeHash` |
| `EventJsonGenerator` | persistierte Events | `GeneratedEventJson` | Event-JSON über `EventJsonSerializerContext.Default.{Typ}` |
| `WireSerializerGenerator` | `IWireMessage` + Payload-Wurzeln | `GeneratedWire`, `GeneratedWirePoly` | Cross-Node-Wire über `CqrsWireJsonContext.Default.{Typ}` ([06](06-transport-multinode-betrieb.md)) |
| `FunktionsGenerator` | `IFunktion` + `IAuftrag<F>` | `GeneratedFunktionen` | Dispatch Auftrag → Funktion → Ergebnis, Implementierungen je Funktion (CQRS068/069) |
| `DienstGenerator` | `IDienst` + Konstruktoren der Implementierungen | `GeneratedDienste` | Fabriken ohne Reflection, Bedarf je Konstruktor, Konfig-Records Feld für Feld |
| `QuellenGenerator` | Record, der von einer Treiber-Basis erbt (`DateiGemeldet`) | `GeneratedQuellen` | je Quelle Treiber, Konfig-Abschnitt, Wirker-Id, Fabrik |
| `ProzessVerbundGenerator` | `{Assembly}.Prozess.GeneratedProzessRegeln` der referenzierten Domänen-Assemblies | `GeneratedProzessVerbund` | alle Prozesse und Flüsse für den Dirigenten (CQRS012 über Assemblies, CQRS099 Prozesse ohne Registry) |
| `AkteurRechteGenerator` | `IAkteur` + `IDarf<T>`, `IAkteurVertrag<A>`, `IClientVertrag` | `GeneratedAkteurRechte`, `GeneratedClientVertraege` | Befugnisse, abgeleitete Hör-Menge, Verträge + Hash (CQRS059) |
| `EventUpcastingGenerator` | `IUpcast<…>` | `GeneratedEventUpcasting` | Backend-Primitive für Schema-Evolution in Prod, kein Editor-Konzept (CQRS040–046; 1:N bricht bewusst den Build) |
| `DtoMapperSourceGenerator` | Domänen-Graph (`DomainGraphAnalyzer`) | `ProtoMessageMapper.Generated` | Domain ↔ Proto-DTO an der gRPC-Grenze (CQRS030) |
| `TypeRegistryGenerator` | `IEvent`, `ICommand`, `IQuery`, `IQueryResponse`, `IPipelineTrigger` … | `GeneratedTypeRegistry` | Name ↔ Typ |
| `PipelineActorGenerator` | `IPipelineHandler` | `PipelineActors`, `GeneratedPipelines`, `GeneratedFristen` | **Altlast** (Handle-Pipeline, Trigger, `Frist<TCmd>`, CQRS056; docs/13 W11-1). `GeneratedPipelines.CommandAggregateTypes` ist nur noch ein Durchreicher auf `GeneratedCommandRouting`, über den u. a. der `CommandEmitter` routet |

### Projections.SourceGeneration
| Generator | Input | Output | Zweck |
|---|---|---|---|
| `ProjectionServicesGenerator` | `IReadModel`, Store-Klassen mit Fähigkeiten, `IReader<T>` | `GeneratedProjectionServices` (ns `Domain.Infrastructure.Generated`) | Marten-Schema je Read-Model, Stores Scoped unter jeder Fähigkeit + Bündel, Reader, Read-Model-Typen für den Neuaufbau (CQRS053) |

### Client.SourceGeneration (Syntax, außer `WiringGenerator`)
| Generator | Input | Zweck |
|---|---|---|
| `HandleMethodGenerator` | `Handle(TEvent, MessageContext)` | Store-/Handler-Dispatch nach Rückgabetyp |
| `ModuleRegistryGenerator` | `IUiModule` | DI-Registrierung (`GeneratedModuleRegistry`) |
| `WiringGenerator` | Syntax + referenzierte Assemblies, `[assembly: IstClient<IX>]` | `GeneratedWiring_{Assembly}`: DI, Abos, Vertrag + Hash, Commands/Queries aus dem Vertrag (CQRS093/094) |
| `ViewModelGenerator` | `IViewModel` | ungenutzt (docs/13 P3-2) |

## 5.3 Die Regeln: Analyzer, Generator-Diagnosen, Boot-Guards

Es gibt **zwölf Analyzer-Klassen**; dazu melden einzelne Generatoren eigene Diagnosen, und was erst mit DI und Umgebung prüfbar ist, prüft
der Start. Die eine Quelle der Regeln ist der Code selbst; die Liste „Regel → Build-Gegenstück“ druckt
`dotnet run --project GraphExtractor -- --grammatik` (aus `DomainEditor/Grammatik.cs`). Die Werkbank holt die CQRS-Befunde bei ⟳ und je Geste
aus den Analyzern der warmen Compilations und hängt sie an die Karte (`Werkbank/Server/Arbeitsbereich.cs`).

| Bereich | IDs | Ort |
|---|---|---|
| Schreibseite & Routing | CQRS010/011 · CQRS020/021 · CQRS050 · CQRS057 · CQRS079–083 | `CommandAggregateMapGenerator` · `CommandEmitAnalyzer` · `AusgabeVertragAnalyzer` · `HandlerFormAnalyzer` · `ModellAnalyzer` (Apply fehlt, Faltung, Enum-Wert 0, Command ab verlierbarem Eingang, Konsument mit Zustand) |
| Prozess & Fluss | CQRS001–003 · CQRS012, 072 · CQRS073–078 · CQRS099 | `ProzessRegelDiagnosticGenerator` · `ProzessRegelnGenerator` · `FlussAnalyzer` · `ProzessVerbundGenerator` |
| Projektstruktur | CQRS098 | `LageAnalyzer` (Ordner, Namespace, Datei prüft `--check`, docs/07) |
| Leseseite & Store | CQRS051, 054, 055 · CQRS052 · CQRS053 · CQRS066/067 | `FaehigkeitAnalyzer` · `PullPathGenerator` · `ProjectionServicesGenerator` · `StoreZugriffAnalyzer` |
| Funktionen, Dienste, Quellen | CQRS068/069 · CQRS089–092 · CQRS070 | `FunktionsGenerator` · `DienstAnalyzer` · `QuellenTypAnalyzer` |
| Akteure, Verträge, Clients | CQRS058, 060–065 · CQRS059 · CQRS093/094 · CQRS095–097 | `AkteurAnalyzer` · `AkteurRechteGenerator` · `WiringGenerator` · `ClientVertragAnalyzer` |
| Gesamtbild | CQRS084–088 | `GesamtbildAnalyzer` (Query mit mehreren Readern, Ausgang ohne Erzeuger, Eingang ohne Konsument, Zusage ohne Client, Kreis ohne Zustandsschritt) |
| Proto | CQRS030 | `DtoMapperSourceGenerator` (Generator-Ausnahme wird lauter Fehler) |
| Upcasting | CQRS040–046 | `EventUpcastingGenerator` |
| Altlast | CQRS056 | `PipelineActorGenerator` (`Frist<TCmd>`) |

Die neun Analyzer von `Domain.SourceGeneration` laufen in jedem Domänen-Projekt; der `GesamtbildAnalyzer` läuft in der
`Infrastructure`-Compilation, sieht alle Domänen-Projekte und meldet ohne Quell-Ort (die Meldung beginnt mit `'Typ'`). Die Domänen-
Dispatch-Generatoren melden selbst nichts; ein nicht erkannter Fall wird zum generierten Laufzeit-`throw` — dass Handler und Modelle die
erkannte Form haben, sichern die Analyzer davor.

**Boot-Guards** (beim Start, als maschinenlesbare Zeile `CQRS-BOOT {json}` für die Werkbank, `Infrastructure/Umgebung/BootFehler.cs`):
Wire-Abdeckung (`WireSerializerBootCheck`), GA-1 (`GaEinsPruefung`), Azyklizität der Regeln, Auslöser-Kollision, Bindung jeder gerufenen
Funktion, Dienst-Bindung (fehlend, mehrdeutig, Kreis, Konfig), Umgebungs-Datei.

## 5.4 Der Dispatch-Kern

**`GeneratedCommandRouting`** ist die Routing-Wahrheit. Input: **jede `IDecider<TState>.Decide(TCommand)`-Signatur** (`TCommand` gehört
strukturell zu `TState`, nicht per Namespace). Output: `CommandToAggregate`, `CommandToEvents` (aus den OneOf-Rückgaben, transiente
gefiltert) und `Produziert()` — die präzise Command→Event-Kantenmenge, die den Azyklizitäts-Boot-Guard speist.

`AggregateActorGenerator` und `CommandAggregateMapGenerator` berechnen die Aggregat-Identität unabhängig; dass sie übereinstimmen, ist nur
per Kommentar vereinbart (docs/13 P2-9).

## 5.5 Warum reflexionsfrei — die Verriegelung

Jeder dispatchende Serialisierungspfad bindet an `CqrsWireJsonContext.Default.{Typ}` bzw. `EventJsonSerializerContext.Default.{Typ}`
(STJ-Source-Generation) statt an `JsonSerializer.Deserialize(type, …)`. Beide Kontexte erzeugt der Codegen-Schritt (§5.7) aus der Domäne;
der STJ-Generator macht daraus die Typ-Infos. Weil `WireSerializerGenerator` und `EventJsonGenerator` die Property `Ctx.Default.{Typ}`
**namentlich** referenzieren, bricht der Build, sobald ein Typ im erzeugten Kontext fehlt — Dispatch und Serialisierungs-Manifest sind
strukturell gekoppelt. Durchgängig wird vor jedem Emit geordnet (`OrderBy(…, Ordinal)`) → stabile Diffs, reproduzierbare Builds.

Grenzen: Marten serialisiert per Default noch reflektiv (der generierte Event-Kontext ist opt-in über `Schreibpfad:GeneriertesJson`), und ein
Build-Verbot gegen `System.Reflection` fehlt (docs/13 W10-2).

## 5.6 Sichtbarkeitsgrenzen (architektonisch tragend)

**Ein Domain-Generator sieht fremdes Generat NICHT (Syntax); ein Infrastructure-Generator schon (Symbol).**
- **Domain-Seite:** sieht nur im aktuellen Projekt deklarierte Typen. Deshalb bleibt `SignalTypeGenerator` syntax-lokal (sonst doppelte
  Signale in mehreren Assemblies), und Aggregat-Generat muss **selbst-enthalten** sein. Die Prozess-Registry entsteht je Assembly
  (`{Assembly}.Prozess.GeneratedProzessRegeln`, nur wo es Prozesse/Flüsse gibt); die Laufzeit vereinigt alle, die sie sieht
  (`ProzessVerbundGenerator` → `GeneratedProzessVerbund`, CQRS099 bei Prozessen ohne Registry, CQRS012 bei doppeltem Namen).
- **Infrastructure-Seite:** sieht die kompilierten Domänen-Symbole. Deshalb liegt das *aggregatübergreifende* Routing hier, ebenso die
  Regeln über alle Domänen-Projekte (`GesamtbildAnalyzer`).
- **Client-Seite:** syntax-lokal, außer `WiringGenerator`, der per `CompilationProvider` referenzierte Assemblies (den Vertrag in
  `Domain.Akteure`) liest.

## 5.7 Der Codegen-Schritt im Build

```
Domain · Domain.Projections · Domain.Akteure · Domain.Pipeline     (bauen)
        ▼
Cqrs.Codegen                     (gebaut über eine reine Build-Reihenfolge-Referenz aus ProtoRepo)
        ▼
ProtoRepo: Target CqrsCodegenPrepass  (AfterTargets=ResolveProjectReferences, `dotnet exec Cqrs.Codegen.dll`)
  liest die gebauten Domänen-DLLs per MetadataLoadContext und schreibt nur bei geändertem Inhalt:
    ProtoRepo/domain.proto
    Infrastructure/Serialization/EventJsonSerializerContext.g.cs
    Infrastructure/Serialization/CqrsWireJsonContext.g.cs
    Domain.Client.Worker.Python.ML/domain_client/generated/vertraege.py · funktionen.py · funktions_tabelle.py
        ▼
ProtoRepo: Grpc.Tools kompiliert domain.proto → {Typ}Dto
        ▼
Infrastructure: STJ-Generator über beide Kontexte; DtoMapperSourceGenerator mappt Domain ↔ {Typ}Dto
```

Kein manueller Schritt: jedes `dotnet build` führt die Kette aus. Welche Projekte die Domäne bilden und wohin die Artefakte gehen, liest das
Werkzeug aus `Abstractions/Projektstruktur.cs` (docs/07); ob es erzeugen muss, entscheidet es selbst über einen Hash der gebauten
Domänen-DLLs und des Werkzeugs (`--wenn-geaendert`) — keine Quellen-Liste im Target. Die Artefakte sind committet; `./codegen.sh --check` generiert
frisch und prüft per `git diff` auf Drift (CI-Gate).

Innen: `ReflectionTypeAnalyzer` liest die Typen, `FileGenerator` (aus `Proto.SourceGeneration`) schreibt das Proto über die eine
Skalar-Tabelle `Core.SourceGeneration/ProtoScalarSpec.cs` (dieselbe, die der `DtoMapperSourceGenerator` liest), `JsonContextEmitter` die
beiden STJ-Kontexte, `PythonVertragsEmitter`/`PythonFunktionsEmitter` die Python-Basen. Ausgenommen sind Signale (nie über die Proto-Grenze,
vom Wire-Generator selbst serialisiert) und `IProzessIntern`-Typen (Maschinerie des Dirigenten).

Schuld: das Werkzeug liest per Reflection (`MetadataLoadContext`), und der Python-Vertrags-Emitter liest Verträge anders als
`Clientvertragslesart` (der Handshake-Hash kann abweichen, docs/13 W10-1).

**Proto an der gRPC-Grenze:** `Guid` und `decimal` reisen als `string`, `DateTimeOffset` als `int64` (Unix-Millisekunden); nullable
Werttypen als proto3 `optional`. Die interne Actor-Ebene nutzt dagegen den Wire-/Event-JSON-Pfad.

## 5.8 Offen

Offene Punkte stehen nur in [13](13-reifegrad-schulden-bewertung.md): Reflection im Codegen W10-1, reflektive
Laufzeit-Serialisierung und fehlende Reflection-Sperre W10-2, Komposition (Infrastructure referenziert Domänen-Projekte) W2-1, Handle-Pipeline-Generatoren W11-1, Identitäts-Vertrag nur per
Kommentar P2-9, Prüf-Lücken des Grammatik-Gates WZ-1, Mojibake P3-1, toter `ViewModelGenerator` P3-2.
