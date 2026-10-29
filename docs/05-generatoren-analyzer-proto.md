# 05 — Generatoren, Analyzer & Codegen

Stand 2026-10-29, gegen den Code geprüft.

Alles Dispatchende wird zur Compile-Zeit erzeugt, ohne Runtime-Reflection (Invariante 4). Drei Bausteine tragen das, alle laufen bei jedem
`dotnet build` mit:
1. **In-Compilation-Generatoren** (Roslyn `[Generator]`) erzeugen Dispatch, Registrierungen und Tabellen.
2. **Analyzer** (`[DiagnosticAnalyzer]`) und Generator-Diagnosen machen Regeln zu Build-Fehlern — **Prüfen = Bauen**. Was sich erst beim
   Start zeigt, prüfen Boot-Guards.
3. **Der Codegen-Schritt im Build** (`Cqrs.Codegen`) erzeugt die Artefakte, die ein nachgelagertes Werkzeug braucht (protoc, der
   STJ-Source-Generator, Python) und die ein In-Compilation-Generator deshalb nicht liefern kann.

Welche Regeln es gibt und warum, steht in [10](10-entwickler-api.md) §10.6 (Grammatik), was sie garantieren, in [11](11-garantien.md) §11.10.
Dieses Kapitel sagt, **wo** sie im Code stehen und was die Generatoren lesen und erzeugen.

## 5.1 Projekte und Verdrahtung

| Projekt | TFM | Rolle |
|---|---|---|
| `Abstractions.SourceGeneration` | net9.0 | Modell-Bibliothek (`TypeNode`), kein `[Generator]` |
| `Core.SourceGeneration` | net9.0 | Analyse-Kern ohne `[Generator]`: `DomainGraphAnalyzer`, `TypeAggregator`, `CompilationTypeResolver`, `TypeMappingHelper`, `ProtoScalarSpec`, `NameSanitizer` |
| `Domain.SourceGeneration` | netstandard2.0 | sechs Generatoren der Domänen-Seite (syntax-lokal) und zwölf Analyzer |
| `Infrastructure.SourceGeneration` | netstandard2.0 | sechzehn Generatoren der Laufzeit-Seite (symbol-basiert) und zwei Analyzer |
| `Projections.SourceGeneration` | netstandard2.0 | DI-Verdrahtung der Leseseite (ein Generator) und zwei Analyzer |
| `Client.SourceGeneration` | netstandard2.0 | vier Generatoren des Blazor-Clients und ein Analyzer |
| `Hausregel.SourceGeneration` | netstandard2.0 | ein Analyzer für das Framework selbst (R18) |
| `Cqrs.Codegen` | net9.0 (Exe) | Werkzeug: der Codegen-Schritt im Build (§5.7) |
| `Proto.SourceGeneration` | net9.0 (Bibliothek) | liefert den `FileGenerator` (domain.proto), den `Cqrs.Codegen` nutzt |

`Core.SourceGeneration` und `Abstractions.SourceGeneration` werden in `Infrastructure.SourceGeneration` per Link mitkompiliert. Ebenso hängen
die **Verträge** als verlinkte Quelle in Generatoren und Laufzeit — eine Regel, eine Datei: `Abstractions/Aggregatvertrag.cs`, `Idvertrag.cs`
(typisierte Ids und Command-Ziel), `Funktionsvertrag.cs`, `Projektionsvertrag.cs`, `Dienstvertrag.cs`, `Clientvertragsregeln.cs`,
`Quellen/Treibervertrag.cs`, `Projektstruktur.cs` (docs/07), `Kodehash.cs`, `Spur/WirkerId.cs`, `StoreZugriff.cs`. Dazu die **Lesarten**, die
Generatoren, Analyzer und Extractor teilen (verlinkt in die übrigen Generator-Projekte und `GraphExtractor.Kern`):
`Infrastructure.SourceGeneration/Funktionsform.cs` (Funktionen), `Projektionsform.cs` (Projektion, Reader, Frage im Decider), `Ctorbindung.cs`,
`Vertragsassemblies.cs`, `Clientvertragslesart.cs` (dieselbe Lesart für Server und C#-Client), `Domain.SourceGeneration/Lageart.cs` (WAS ein Typ
für die Lage-Prüfung ist; dieselbe Lesart für CQRS098 und `--check`), `Kodeform.cs` (Code-Hash) und `Core.SourceGeneration/ProtoScalarSpec.cs`.

**Analyzer-Verdrahtung** (`OutputItemType="Analyzer"`, bestimmt die Sichtbarkeit, §5.6):

| Generator-Projekt | hängt als Analyzer an |
|---|---|
| `Domain.SourceGeneration` | `Domain`, `Domain.Projections`, `Domain.Pipeline`, `Domain.Akteure`; `Pruefdomaene`, `Pruefdomaene.Projections`, `Pruefdomaene.Pipeline` |
| `Projections.SourceGeneration` | `Domain.Infrastructure`, `Pruefdomaene.Infrastructure` |
| `Infrastructure.SourceGeneration` | `Domain.Laufzeit`, `Pruefdomaene.Laufzeit` (Komposition: alle Laufzeit-Generatoren und der `GesamtbildAnalyzer`, Gate `Laufzeitprojekt.Ist`) und `Domain.Leitung` (dort nur der `DtoMapperSourceGenerator`, Gate Rolle Leitung); der `CommandEmitAnalyzer` hat kein Gate und läuft in allen dreien |
| `Client.SourceGeneration` | `Domain.Client.Modules.Blazor` |
| `Hausregel.SourceGeneration` | `Infrastructure` |

`Infrastructure.Pruefstand.Tests` referenziert die Generator-Projekte als gewöhnliche Assemblies, um Generatoren und Analyzer gegen Proben
laufen zu lassen.

**Framework und Komposition sind getrennt** (docs/07, Rolle Komposition). `Infrastructure` ist reines Framework und referenziert kein
Domänen-Projekt. Die Komposition der Anwendung (`<Wurzel>.Laufzeit`, hier `Domain.Laufzeit`) referenziert Framework und alle Rollen-Projekte;
nur dort laufen die Laufzeit-Generatoren. Der `AnwendungGenerator` fasst ihre Tabellen zu
`GeneratedAnwendung : Infrastructure.Komposition.IAnwendung` zusammen und erzeugt das `AddCqrs(args)` der Hosts, das die Anwendung anmeldet
(`Anwendung.Setze`); das Framework liest die Tabellen nur über `Infrastructure/Komposition/Anwendung.cs` (`Anwendung.Typen`, `Routing`, `Wire`,
`WirePoly`, `Upcasting`, `Akteure`, `Funktionen`, `Dienste`, `Aggregate`, `Weckplan`, `Quellen`, `QueryTypen`, `WireJson`, `EventJson`). Tests
melden die Anwendung per `[ModuleInitializer]` an (`AnwendungAnmelden.cs`). Ein Prozess fährt eine Anwendung.

## 5.2 Die Generatoren

**Input-Legende:** *Syntax* = liest die Syntaxbäume der eigenen Compilation (sieht nur im aktuellen Projekt deklarierte Typen).
*Symbol* = wandert über die Symbole der Compilation und sieht auch referenzierte Assemblies. Dateiname und Klassenname weichen teils ab
(Klasse genannt).

### Domain.SourceGeneration (Syntax)
| Generator | Input (Marker) | Output | Zweck |
|---|---|---|---|
| `Generator` | `IDecider<T>`/`IApplier<T>` | `{State}.{Klasse}.State.g.cs` | injiziert `State`-Property + Ctor in Decider/Applier |
| `StatePropertyGenerator` | `IState` | `{State}.State.g.cs` | `Guid Id` + `int Version` |
| `AggregateHandlerGenerator` | `IState` + nested Decider/Applier | `{State}AggregateHandler` | `HandleCommand`/`ApplyEvent`-Switch, entpackt `OneOf`; eine `Frist<TCmd>` aus dem Decide wird `FristGesetzt` (`IRahmenEvent`, zählt in der Version, der Applier sieht es nie); `Frage`-Switch für die direkten Queries des Deciders (docs/03 §3.2, über `Projektionsform.LiesAggregatfragen`); `CodeHash` (State + Applier) für Snapshots |
| `HandlerFactoryGenerator` (`FactoryGenerator.cs`) | `IState` | `AggregateHandlerFactory` (ns = Projekt der Aggregate) | State-Typ → Handler |
| `ProjektionsstandGenerator` | `IProjektion` | `[assembly: Projektionsstand(typeof(P), "…")]` | Code-Stand der Projektion (nur in ihrer Compilation ist der Quelltext sichtbar); die Komposition liest ihn für den Neuaufbau in Sim |
| `ReadModelSchluesselGenerator` | `IReadModel` mit `Id<T> Id` (partial) | `{Typ}.Schluessel.g.cs` | Speicher-Identität `Schluessel` (UUID) für Stores mit einfachem Schlüssel (Marten kann `Id<T>` nicht als Identität) — CQRS111 |

### Infrastructure.SourceGeneration (Symbol, nur in der Komposition; Ausnahme `DtoMapperSourceGenerator`)
| Generator | Input | Output | Zweck |
|---|---|---|---|
| `CommandAggregateMapGenerator` | jeder `ICommand<TAggregat>` (Ziel) + `IDecider<T>.Decide(TCommand)` mit OneOf-Rückgaben | `GeneratedCommandRouting` (ns `Infrastructure.Mapping`) | **Dispatch-Kern** (§5.4): `CommandToAggregate` (aus dem Typ), `CommandToEvents`, `Produziert()` (CQRS010, CQRS011) |
| `AggregateActorGenerator` | `IState` | `AggregateActors`, `GeneratedAggregates` | Proto.Actor-Actor + Cluster-Kinds |
| `PullPathGenerator` | `IProjektion` + `Auf` (über `Projektionsform`) | `GeneratedProjektionen`, `GeneratedPullPaths` | je Projektion Dispatch auf ihr `Auf`, gehörte Typen, Store aus dem Fähigkeits-Bereich, Batch-Kind `batch-<Projektion>`, `Projektionseintrag` mit Code-Stand; ein Store je Projektion (CQRS052), Klasse 3 nur mit `[MindestensEinmal]` (CQRS110) ([04](04-konsum-und-prozess-maschine.md) §4.1–§4.3) |
| `WeckplanGenerator` | `Auf` der Projektionen und der Funktionen mit Log-Eingang (dieselben Lesarten), `FristGesetzt` | `GeneratedWeckplan` | je Event-Typ, wen ein Commit weckt: `batch-<P>` je Partition, `funktion-<F>` je Partition bzw. die eine Adresse, die Uhr ([03](03-schreibseite.md) §3.5) |
| `FunktionsBausteinGenerator` | `IFunktion` (Klasse oder Schnittstelle) + `Auf` (über `Funktionsform`), Umsetzungen im Host | `GeneratedFunktionsBausteine` | je Funktion Eingänge, Ausgaben, Lese-Fähigkeiten, ob gleichbleibend (R12), Bau, Dispatch in Einzel- und Seiten-Form; der **Hinweisplan** (Hinweis-Typ → Funktions-Actors) für das Hinweis-Netz (CQRS100, CQRS101) ([04](04-konsum-und-prozess-maschine.md) §4.4) |
| `QueryDienstGenerator` | `IReader` + `Frage`, `Frage` im Decider (über `Projektionsform`) | `GeneratedQueryDienst : IQueryDienst` | je Query die Frage mit Lese-Fähigkeiten; die Projektionen dahinter aus den Fähigkeiten (dieselben Stores), die gezeigten Ids aus den `Id<T>`-Feldern von Antwort und Query; eine Query an ein Aggregat geht über `AggregatAbfrage` an die Identität aus ihrem Glied `Id<TAggregat>`, Stand = das Aggregat mit seiner Version |
| `SnapshotRegistrationGenerator` | `IState` | `RegisteredSnapshotTypes`, `GeneratedSnapshotSchema` (Datei `GeneratedSnapshotRegistration.g.cs`) | Marten-Registrierung; Schema-Version = FNV-1a-Struktur-Hash + `CodeHash` |
| `EventJsonGenerator` | persistierte Events (`IEvent` ohne `ITransientEvent`) | `GeneratedEventJson` | Event-JSON über `EventJsonSerializerContext.Default.{Typ}` |
| `WireSerializerGenerator` | `IWireMessage` + Payload-Wurzeln (Command, Event, Query, Antwort) | `GeneratedWire`, `GeneratedWirePoly` | Cross-Node-Wire über `CqrsWireJsonContext.Default.{Typ}` ([06](06-transport-multinode-betrieb.md) §6.2) |
| `DienstGenerator` | `IDienst` + Konstruktoren der Implementierungen (`Ctorbindung`) | `GeneratedDienste` | Fabriken ohne Reflection, Bedarf je Konstruktor, Konfig-Records Feld für Feld |
| `QuellenGenerator` | Record, der von einer Treiber-Basis erbt (`DateiGemeldet`, `TaktGemeldet`; `Treibervertrag`) | `GeneratedQuellen` | je Quelle Treiber, Konfig-Abschnitt, Wirker-Id, Fabrik |
| `AkteurRechteGenerator` | `IAkteur` + `IDarf<T>`, `IClientVertrag` | `GeneratedAkteurRechte`, `GeneratedClientVertraege` | Befugnisse, abgeleitete Hör-Menge, Funktionen draußen je Akteur, Verträge + Hash (CQRS059) |
| `EventUpcastingGenerator` | `IUpcast<…>` | `GeneratedEventUpcasting` | Backend-Primitive für Schema-Evolution in Prod, kein Editor-Konzept (CQRS040–042, CQRS044–046; 1:N bricht bewusst den Build) |
| `DtoMapperSourceGenerator` (`DtoMapperGenerator.cs`) | Domänen-Graph (`DomainGraphAnalyzer`), ohne `IRahmenEvent`; läuft nur im Leitungs-Projekt (`<Wurzel>.Leitung`) | `GeneratedNutzlast : Cqrs.Leitung.INutzlastTabelle` (Datei `ProtoMessageMapper.Generated.cs`) | Domain ↔ `{Typ}Dto` ↔ `Nutzlast { typ, daten }` an der gRPC-Grenze (CQRS030) |
| `TypeRegistryGenerator` | `IEvent`, `ICommand`, `IQuery`, `IQueryResponse`, `ITransientEvent` | `GeneratedTypeRegistry` | Name ↔ Typ |
| `AnwendungGenerator` | die Tabellen der übrigen Laufzeit-Generatoren derselben Compilation; Leseseite, Speicher und Leitung über die Projektstruktur | `GeneratedAnwendung : IAnwendung`, `AddCqrs(args)` | die Anwendung für das Framework (`Anwendung.Setze`, §5.1) |

### Projections.SourceGeneration (Symbol, in `<Wurzel>.Infrastructure`)
| Generator | Input | Output | Zweck |
|---|---|---|---|
| `ProjectionServicesGenerator` | `IReadModel`, Store-Klassen mit Fähigkeiten | `GeneratedProjectionServices` (ns `{Projekt}.Generated`) | Marten-Schema je Read-Model (Identität `Schluessel` bei typisierter Id, `Id<X>` als Werttyp für LINQ, OCC nur für `IGeteiltesReadModel`), Stores Scoped unter jeder Fähigkeit + Bündel (`AddGeneratedProjectionServices`, `RegistriereSchema`; CQRS053) |

### Client.SourceGeneration (Syntax, außer `WiringGenerator`)
| Generator | Input | Zweck |
|---|---|---|
| `HandleMethodGenerator` | `Handle(TEvent, MessageContext)` | Store-/Handler-Dispatch nach Rückgabetyp (`{Klasse}.Dispatch.g.cs`) |
| `ModuleRegistryGenerator` | `IUiModule` | DI-Registrierung (`GeneratedModuleRegistry_{Assembly}`) |
| `WiringGenerator` | Syntax + referenzierte Assemblies, `[assembly: IstClient<IX>]` | `GeneratedWiring_{Assembly}`: DI, Abos, Vertrag + Hash, Commands/Queries aus dem Vertrag, Ziel-Aggregat je Command aus `ICommand<TAggregat>` (CQRS093/094) |
| `ViewModelGenerator` | `IViewModel` | ungenutzt (docs/13 P3-2) |

## 5.3 Die Regeln: Analyzer, Generator-Diagnosen, Boot-Guards

Es gibt **achtzehn Analyzer-Klassen** (zwölf in `Domain.SourceGeneration`, je zwei in `Infrastructure.SourceGeneration` und
`Projections.SourceGeneration`, je einer in `Client.SourceGeneration` und `Hausregel.SourceGeneration`); dazu melden einzelne Generatoren
eigene Diagnosen, und was erst mit DI und Umgebung prüfbar ist, prüft der Start. Die eine Quelle der Regeln ist der Code selbst; die Liste
„Regel → Build-Gegenstück“ druckt `dotnet run --project GraphExtractor -- --grammatik` (aus `DomainEditor/Grammatik.cs`). Die Werkbank holt die
CQRS-Befunde bei ⟳ und je Geste aus den Analyzern der warmen Compilations und hängt sie an die Karte (`Werkbank/Server/Arbeitsbereich.cs`).

Alle Diagnosen sind Fehler, außer den Warnungen CQRS080, 081, 083, 085, 086, 087. Nummern, die hier fehlen, sind nicht vergeben.

| Bereich | IDs | Ort |
|---|---|---|
| Schreibseite & Routing | CQRS010/011 · CQRS013, 079–081, 083 · CQRS020/021 · CQRS050 · CQRS057 | `CommandAggregateMapGenerator` (Decide außerhalb des Ziels, zwei Aggregate mit gleicher Identität) · `ModellAnalyzer` (Command ohne typisiertes Ziel, Apply fehlt, Faltung ohne Decide, Enum-Wert 0, Projektion mit Zustand in Feldern) · `CommandEmitAnalyzer` (ein Emit-Weg, Send mit Frist) · `AusgabeVertragAnalyzer` · `HandlerFormAnalyzer` (Form von Projektion, Reader und Frage an ein Aggregat) |
| Eingang und Wiederholung | CQRS101 · CQRS102–104 | `EinEingangAnalyzer` (je Eingang ein `Auf` bzw. eine `Frage`, R2) · `GleichbleibendAnalyzer` (R11 gleiche Rechnung, R12 gleichbleibende Funktion, R13 abgeleitete Ids) |
| Funktionen | CQRS100/101 · CQRS105 · CQRS107 · CQRS106 | `FunktionsBausteinGenerator` (Form, doppelter Eingang) · `ZustandAnalyzer` (R19) · `LebenszyklusAnalyzer` · `GesamtbildAnalyzer` (Hinweis ohne Erzeuger, R20) |
| Dienste, Quellen | CQRS089–092 · CQRS070 | `DienstAnalyzer` · `QuellenTypAnalyzer` |
| Projektstruktur | CQRS098 | `LageAnalyzer` (Ordner, Namespace, Datei prüft `--check`, docs/07) |
| Leseseite & Store | CQRS051, 054, 055 · CQRS052, 110 · CQRS053 · CQRS066/067 · CQRS109 · CQRS111 | `FaehigkeitAnalyzer` · `PullPathGenerator` · `ProjectionServicesGenerator` · `StoreZugriffAnalyzer` · `StoreKlasseAnalyzer` · `ReadModelSchluesselGenerator` |
| Akteure, Verträge, Clients | CQRS058, 063, 064 · CQRS059 · CQRS093/094 · CQRS095–097 | `AkteurAnalyzer` · `AkteurRechteGenerator` · `WiringGenerator` · `ClientVertragAnalyzer` (`ClientVertrag.cs`) |
| Gesamtbild | CQRS084–087, 106 | `GesamtbildAnalyzer` (Query mit mehreren Zielen — R4: ein Reader oder ein Aggregat —, Ausgang ohne Erzeuger, Eingang ohne Konsument, Funktion draußen ohne Umsetzung und ohne Träger, Hinweis ohne Erzeuger) |
| Proto | CQRS030 | `DtoMapperSourceGenerator` (Generator-Ausnahme wird lauter Fehler) |
| Upcasting | CQRS040–042, 044–046 | `EventUpcastingGenerator` |
| Framework: Hausregel für Actors (R18) | CQRS112/113 | `HausregelAnalyzer` (nur in `Infrastructure`: in Actors und dem, was sie besitzen, keine eigenen Fäden, kein `lock`, kein Feuern und Vergessen, kein `ContinueWith`, kein Blockieren, keine rohe Cluster-API; Ausnahme `[HausregelAusnahme("Begründung")]`, leere Begründung = CQRS113) |

Die zwölf Analyzer von `Domain.SourceGeneration` laufen in jedem Rollen-Projekt der Domäne (§5.1). Der `GesamtbildAnalyzer` läuft in der
Komposition, sieht alle Domänen-Projekte und meldet ohne Quell-Ort (die Meldung beginnt mit `'Typ'`, daran hängt die Werkbank sie an die
Karte). Die Domänen-Dispatch-Generatoren melden selbst nichts; ein nicht erkannter Fall wird zum generierten Laufzeit-`throw` — dass Decider,
Projektionen, Reader und Funktionen die erkannte Form haben, sichern die Analyzer davor.

**Boot-Guards** (beim Start; jeder Startfehler erscheint als maschinenlesbare Zeile `CQRS-BOOT {json}` für die Werkbank, ein `BootFehler`
mit der Karte, an der er hängt, `Infrastructure/Umgebung/BootFehler.cs`):

| Prüfung | Ort |
|---|---|
| Umgebung angegeben, Abschnitte bekannt, Datenbank gehört der Umgebung | `CqrsUmgebungExtensions`, `UmgebungLader.PruefeAbschnitte`, `UmgebungsSiegel` |
| jeder Wire-Typ serialisierbar | `WireSerializerBootCheck` |
| jeder Hörer aus dem Log steht mit jedem gehörten Typ im Weckplan (GR-WECKPLAN) | `Wecker.Pruefe` |
| jede Adresse des Hinweisplans gehört zu einer Funktion (R20) | `HinweisNetz` |
| jede Funktion hat einen Laufort (Umsetzung im Host, Träger, Bindung); Lese-Fähigkeit nur im Host | `FunktionsBausteinWiring`, `FunktionsExtensions` |
| Dienst-Bindung (fehlend, mehrdeutig, Kreis, Konfig) | `Infrastructure/Dienste/Dienste.cs` |
| Quellen-Konfig | `Infrastructure/Quellen/Quellen.cs`, `QuellenDienst.cs` |
| Akteure der Konfiguration bekannt | `AkteurOptionen.Pruefe` |
| Store einer Projektion: ein Store, genau eine Klasse, Klasse 3 nur mit Erlaubnis (R14, beim ersten Spawn) | `Projektionslauf.Ziel` (`Infrastructure/Projections/Projektionsnaht.cs`) |

Zur Laufzeit prüft der Aggregat-Actor beim Schreiben, dass eine Frist auf einen Command des eigenen Aggregats zielt (R5,
`AggregateActorBase`).

## 5.4 Der Dispatch-Kern

**`GeneratedCommandRouting`** ist die Routing-Wahrheit. Input: **jeder Command-Typ** mit seinem Ziel aus `ICommand<TAggregat>` (docs/10 §10.4:
das Ziel steht im Typ, nicht im Namespace und nicht im Decider) und **jede `IDecider<TState>.Decide(TCommand)`-Signatur** (muss im Ziel-Aggregat
stehen, sonst CQRS010). Output: `CommandToAggregate`, `CommandToEvents` (aus den OneOf-Rückgaben, transiente gefiltert) und
`Produziert(command)` — die Command→Event-Kantenmenge. Darüber routen Dispatcher, Leitung und Emitter (`Anwendung.Routing`). Ein Command ohne
typisiertes Ziel baut nicht: `ICommand` verlangt `Ziel`, `ICommand<TAggregat>` liefert es aus `Id<TAggregat> AggregateId`; den Umweg über eine
handgeschriebene `Guid` meldet CQRS013, einen Command ohne Decide CQRS086. Dieselbe Ziel-Lesart (`Idvertrag`) nutzen das Client-Wiring und der
Editor-Extractor.

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
- **Domain-Seite:** die Generatoren sehen nur im aktuellen Projekt deklarierte Typen. Deshalb muss Aggregat-Generat **selbst-enthalten** sein,
  und Code-Fakten, die nur im Quelltext stehen, gibt die Domänen-Compilation als Assembly-Angabe weiter (`Projektionsstand`, vom
  `PullPathGenerator` in der Komposition gelesen). Die Domänen-Analyzer lesen dagegen auch referenzierte Assemblies, die den Vertrag nutzen
  (`Domain.SourceGeneration/Typen.cs`) — etwa die Lage-Prüfung oder die Regeln über Funktionen in `Domain.Pipeline`.
- **Infrastructure-Seite:** sieht die kompilierten Domänen-Symbole aller Rollen-Projekte. Deshalb liegen das *aggregatübergreifende* Routing,
  Weckplan, Hinweisplan und die Regeln über alle Domänen-Projekte (`GesamtbildAnalyzer`) hier, in der Komposition.
- **Client-Seite:** syntax-lokal, außer `WiringGenerator`, der per `CompilationProvider` referenzierte Assemblies (den Vertrag in
  `Domain.Akteure`) liest.

## 5.7 Der Codegen-Schritt im Build

```
Domain · Domain.Projections · Domain.Akteure · Domain.Pipeline     (bauen)
        ▼
Cqrs.Codegen                     (gebaut über eine reine Build-Reihenfolge-Referenz aus Domain.Leitung)
        ▼
Domain.Leitung: Target CqrsCodegenPrepass  (Cqrs.Codegen/CqrsCodegen.targets, AfterTargets=ResolveProjectReferences,
                `dotnet exec Cqrs.Codegen.dll --projekt=Domain.Leitung --wenn-geaendert` → Wurzel Domain)
  liest die gebauten Domänen-DLLs per MetadataLoadContext und schreibt nur bei geändertem Inhalt:
    Domain.Leitung/domain.proto
    Domain.Laufzeit/Serialization/EventJsonSerializerContext.g.cs
    Domain.Laufzeit/Serialization/CqrsWireJsonContext.g.cs
    Domain.Client.Worker.Python.ML/domain_client/generated/vertraege.py · funktionen.py · funktions_tabelle.py · nutzlast_tabelle.py
        ▼
Domain.Leitung: Grpc.Tools kompiliert domain.proto → {Typ}Dto        (nur Domänen-DTOs)
        ▼
Domain.Leitung: DtoMapperSourceGenerator → GeneratedNutzlast (Domain ↔ {Typ}Dto ↔ Nutzlast)
        ▼
Domain.Laufzeit: STJ-Generator über beide Kontexte; Laufzeit-Generatoren; GeneratedAnwendung
```
Eine Anwendung ohne Leitung (die Prüfdomäne) importiert das Target in ihre Komposition (`Pruefdomaene.Laufzeit`); dann entstehen nur die
STJ-Kontexte. Jede Rolle erzeugt nur, wenn ihr Projekt existiert (Python nur mit Python-Worker-Projekt). Je Anwendung ein Hash
(`cqrs-codegen.<Wurzel>.hash`); `./codegen.sh` arbeitet mit `--alle` (je Komposition `<Wurzel>.Laufzeit` eine Anwendung).

**Die Leitung.** Dienst, Hüllen (`CommandEnvelopeDto`, `EventEnvelopeDto`) und Protokoll-Nachrichten stehen handgeschrieben im Framework
(`Cqrs.Leitung/cqrs.proto`, Paket `cqrs`, Route `/cqrs.CqrsClientService/Connect`). Eine Domänen-Nachricht reist dort als
`Nutzlast { typ, daten }`: `typ` ist der einfache Typname (= Name des DTO ohne „Dto“), `daten` die `{typ}Dto`-Message aus `domain.proto`,
protobuf-kodiert. Der `DtoMapperSourceGenerator` erzeugt je Kategorie die Tabellen Typname → Domänen-Typ und Domänen-Typ → Nutzlast; das
Python-SDK packt und entpackt über dieselbe Regel (`cqrs_client/mapper.py`, `nutzlast_tabelle.py`). Weil kein `oneof` über Domänen-Typen
existiert, ändert ein Namespace-Umzug die Leitung nicht.

Kein manueller Schritt: jedes `dotnet build` führt die Kette aus. Welche Projekte die Domäne bilden und wohin die Artefakte gehen, liest das
Werkzeug aus `Abstractions/Projektstruktur.cs` (docs/07); ob es erzeugen muss, entscheidet es selbst über einen Hash der gebauten
Domänen-DLLs und des Werkzeugs (`--wenn-geaendert`) — keine Quellen-Liste im Target. Die Artefakte sind committet; `./codegen.sh --check`
generiert frisch und prüft per `git diff` auf Drift (CI-Gate).

Innen: `ReflectionTypeAnalyzer` liest die Typen, `FileGenerator` (aus `Proto.SourceGeneration`) schreibt das Proto über die eine
Skalar-Tabelle `Core.SourceGeneration/ProtoScalarSpec.cs` (dieselbe, die der `DtoMapperSourceGenerator` liest), `JsonContextEmitter` die
beiden STJ-Kontexte, `PythonVertragsEmitter`, `PythonFunktionsEmitter` und `PythonNutzlastEmitter` die Python-Generate. Ausgenommen von Proto
und Nutzlast sind die Events des Rahmens (`IRahmenEvent`, etwa `FristGesetzt`): sie gehen nie über die Leitung.

Schuld: das Werkzeug liest per Reflection (`MetadataLoadContext`), und der Python-Vertrags-Emitter liest Verträge anders als
`Clientvertragslesart` (der Handshake-Hash kann abweichen, docs/13 W10-1).

**Proto an der gRPC-Grenze:** `Guid`, typisierte Ids (`Id<T>`) und `decimal` reisen als `string`, `DateTimeOffset` als `int64`
(Unix-Millisekunden); nullable Werttypen als proto3 `optional`. Die interne Actor-Ebene nutzt dagegen den Wire-/Event-JSON-Pfad; dort ist eine
`Id<T>` ebenfalls ein UUID-String: der Codegen trägt je verwendetem `Id<X>` einen geschlossenen `IdJsonKonverter<X>` in beide STJ-Kontexte ein
(der STJ-Quellgenerator kennt keine offenen generischen Konverter), und Martens Serializer bekommt dieselben Konverter
(`Infrastructure/Persistence/IdSerialisierung.cs`) — ohne Reflection.

## 5.8 Offen

Offene Punkte stehen nur in [13](13-reifegrad-schulden-bewertung.md) §13.0: Reflection im Codegen W10-1, reflektive Laufzeit-Serialisierung und
fehlende Reflection-Sperre W10-2, Rest der Projektstruktur (u. a. Framework-Namespaces in Generaten der Komposition) W2-1, Identitäts-Vertrag
nur per Kommentar P2-9, Prüf-Lücken des Grammatik-Gates WZ-1, Mojibake P3-1, toter `ViewModelGenerator` P3-2.
