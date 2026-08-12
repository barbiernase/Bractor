# 08 — Frontend (Blazor-Client)

## 8.1 Charakterisierung

Ein **Blazor-Server**-Client (MudBlazor) auf einem selbstgebauten, generierten,
reflexionsfreien **Redux/Flux-Bus-/Store-Stack**, der über **bidirektionales gRPC-Streaming** an
`Host.Grpc` hängt. Er existiert in **zwei Generationen**: alt/monolithisch (`Domain.Client` +
`Domain.Client.Ui.Blazor`, tot) und neu/modular (`Domain.Client.Modules.Blazor`, produktiv).
Der Build war blockiert und ist **2026-08-12 repariert** (§8.6); die GUI-Struktur läuft über
`Domain.Client.Modules.Blazor`.

## 8.2 Host.Blazor — Startup & Anbindung

`Host.Blazor/Program.cs` (Blazor Server, net9.0):
- Config: `Blazor:Urls` (Default `:5010`), `GrpcServer:Address` (Default `http://localhost:5001`),
  `Pipeline:WatchPath`.
- DI-Scope = **pro Circuit** (`AddServerSideBlazor`, `AddMudServices`). Der ganze
  Infrastruktur-Stack ist `Scoped`: `ClientBus`, `GrpcProxy`, `VersioningModule`,
  `ConnectionModule`, `QueryBridge`, `FileBridge`, `ClientStartupService`.
- Backend-Anbindung: `new GrpcServerConfig(grpcAddress)`; die Verbindung macht `GrpcProxy` gegen
  den generierten gRPC-Client. Kein REST.
- Domain+Module werden generiert eingehängt: `AddClientDomain_Domain_Client_Modules_Blazor()` +
  `AddModules_Domain_Client_Modules_Blazor()`.
- Endpunkte: `MapBlazorHub()`, `/api/files/...` (Bild-Auslieferung), Fallback `_Host`. Ein
  `ProtoEndpointExtensions.cs` (`/api/proto/domain.proto` + Hash, für Python-Clients) existiert,
  wird in `Program.cs` aber **nicht gemappt** (toter, harmloser Pfad).

**Startup-Choreografie** (`Client.Infrastructure/ClientStartupService.cs`): `BootstrapState`
(Connecting→Hydrating→Ready→Failed). Reihenfolge: `SubscribeAll` → `RegisterQueries` → Module
aktivieren → `ConnectAsync` (publiziert `ConnectionEstablished` → RefreshHandler feuern
Start-Queries) → **auf Hydration warten** (alle `IHydrationStore.IsHydrated`, 15 s Timeout) →
`Ready`. Das koppelt Daten-Timeline an View-Timeline und schließt das „leere Liste beim
Start"-Rennen konstruktiv aus. Die Shell gated hart auf `Ready`.

## 8.3 Client.Infrastructure — Transport/Bus/Store

**Kein Reflection, kein `dynamic`** (durchgehend).

**Transport** (`Connection/`):
- **`GrpcProxy`** — hält die bidirektionale Duplex-Verbindung (`CqrsClientService.Connect`). Ein
  `SemaphoreSlim` serialisiert alle Stream-Writes; Reads in `ReadLoopAsync`. Drei
  Nachrichtenklassen: **Commands** fire-and-forget; **Queries** request/response über
  `ConcurrentDictionary<correlationId, TCS>` (echte parallele Queries); **Events** Server-Push
  über `Channel<EventEnvelope>`. Serialisierung: `ProtoMessageMapper`.
- **`ConnectionModule`** — Brücke Bus↔gRPC. Upstream: abonniert alle Command-Typen, baut
  `CommandEnvelope` (ExpectedVersion via `ICreationCommand`→0 bzw. `VersioningModule`,
  AggregateType via generiertem Dict, `OriginSessionId`), sendet fire-and-forget. Downstream:
  liest Events aus dem Channel, `PostToSyncContext` → `bus.Publish` auf dem UI-Thread.
- **`QueryBridge`** — abonniert je Query-Typ, ruft `proxy.QueryAsync<TResponse>`, trackt `Deps`
  im `VersioningModule`, publiziert die Response wieder auf den Bus.
- **`VersioningModule`** — trackt Aggregat-Versionen (OCC) aus Event-`AggregateVersion` und
  Query-`Deps`.

**Bus** (`Bus/ClientBus.cs`): `IBus` mit `Dictionary<Type, List<Handler>>`, Dispatch via
`message.GetType()`. Transaktionsmodell: `IsDispatching`-Flag + `DispatchCompleted`-Event
(feuert genau einmal pro Root-Publish), Tiefen-Guard (MaxDepth 16 → Zyklus-Erkennung).

**Store** (`Abstractions/StoreBase.cs`): `ObservableObject` (CommunityToolkit.Mvvm),
Redux-Analogie explizit — `Handle()` = Reducer, `Changed` = subscribe. **Ein** gebatchtes
Notification-Signal `Changed` (sammelt `[ObservableProperty]`- und `TrackedMap`-Änderungen
während des Dispatch-Zyklus, feuert im `Flush` einmal). Implementiert `IHydrationStore`.

**View-Anbindung** (`Abstractions/EffectScope.cs`): pro Komponente ein transienter `EffectScope`;
`Fx.OnChanged(store, () => InvokeAsync(StateHasChanged))`, `Fx.On<TEvent>`, `Fx.Dispatch(msg)`;
in `Dispose()` aufgeräumt.

**Virtualisierung** (`Collections/`): eigenes `VirtualCollection`/`PagedCollection`/`Cursor`/
`TrackedMap` — virtuelles Fenster mit lazy Seiten-Nachforderung über eine `baueQuery`-Closure.
Getestet in `Client.Infrastructure.CollectionTests` (26 Tests).

## 8.4 Client.SourceGeneration

Vier Incremental-Generatoren (Details in [05](05-generatoren-analyzer-proto.md)):
- **`HandleMethodGenerator`** — `Handle(TEvent, MessageContext)`; Rückgabetyp entscheidet die
  Rolle (`void` → Store-`Dispatch`; `IEnumerable<T>` → sync Handler mit emit; `IAsyncEnumerable`
  → async).
- **`ViewModelGenerator`** — für `IViewModel`: generiert `_publish` + `__InitBus()` + öffentliche
  `PascalCase`-Methoden + `IRelayCommand`. Das ist der Mechanismus hinter dem „`_publish`"-Feld.
- **`WiringGenerator`** (36 KB, Herzstück) — sammelt Stores/Handler/VMs + Domain-Typen und
  emittiert **eine** Klasse `GeneratedWiring_{Assembly}` mit `AddClientDomain_…`, `SubscribeAll`
  (Stores **depth-first**, Eltern vor Kindern), `CommandAggregateTypes`, `HydrationStores`,
  `RegisterQueries`.
- **`ModuleRegistryGenerator`** — `IUiModule` → `AddScoped`.

## 8.5 Blazor-UI — Modul-System

**Slot-basiertes Plugin-Modell.** Die `Shell.razor` löst `IEnumerable<IUiModule>` per DI auf und
rendert jedes Modul via `<DynamicComponent Type="m.ComponentType"/>` in seinen Slot:

| Interface | Slot |
|---|---|
| `IHeaderModule` | Kopfzeile |
| `ISidebarModule` | linke/rechte Sidebar (`Side`, `ExpandedWidth`, `BarTitle`) |
| `IStageModule` | zentrale `MudTabs`-Bühne |
| `IFooterModule` | Fußzeile |
| `IHeadlessModule` | unsichtbar (nur Effekte/Datenlogik) |

`ShellState` hält Sichtbarkeit + aktiven Tab + Sidebar-Expand; `ShellKeyBindingBuilder` die
Tastaturbindungen.

**13 Module** in `Domain.Client.Modules.Blazor/`: Stages (`Bilder`, `Chart`/ApexCharts,
`Heatmap`, `Debug`); Sidebars (`Paarliste`, `Historie`, `Suche`); Footer (`Navigation`,
`Labeling`); Header (`StatusBar`); Headless (`Data`, `Feedback`, `Statistik`); Shared
(`VirtualList`).

**Reads anzeigen** (Bsp. `PaarlistenPanel.razor`): `@inject Store` + `@inject EffectScope`; eine
`<VirtualList>` bindet selektiv an `Store.VirtualImagePairs` + `Store.Cursor` und fordert Seiten
über die im Store hinterlegte `baueQuery` nach. Der `PaarlisteRefreshHandler` (sync Handler)
feuert nach jedem Kontextwechsel die Query(s).

**Commands/Intents auslösen**: die View ruft `Fx.Dispatch(new ImagePairAusgewaehlt(id))`
(Client-Event) bzw. ein `IntentHandler` publiziert einen `ICommand` → `ConnectionModule` → gRPC.
Bemerkenswert: das **neue** Modell nutzt statt fetter ViewModels überwiegend RefreshHandler +
IntentHandler + Client-Events — im neuen Projekt gibt es **keine** `IViewModel`-Klasse mehr (der
`ViewModelGenerator` bedient faktisch nur noch das alte `Domain.Client`).

## 8.6 Build-Status — 2026-08-12 repariert

**Vorher** (Ausgangsbefund): `dotnet build Host.Blazor` → EXIT=1, 13 Fehler `CS0103: _publish`
in `Domain.Client/NavigationViewModel.cs` und `ChartViewModel.cs`. Ursachenkette:
1. `Host.Blazor.csproj:16` referenzierte **noch** das alte `Domain.Client.Ui.Blazor`.
2. `Domain.Client.Ui.Blazor` → `Domain.Client`.
3. `Domain.Client.csproj` referenzierte `Client.SourceGeneration` **nicht** als Analyzer → der
   `ViewModelGenerator` lief dort nie → `_publish`/`__InitBus` wurden für die alten ViewModels
   nie generiert → 13 Fehler.

**Fix (angewandt 2026-08-12):** in `Host.Blazor.csproj` die tote
`Domain.Client.Ui.Blazor`-Referenz entfernt (Host referenziert nur noch `Client.Infrastructure`
+ `Domain.Client.Modules.Blazor`) und das verwaiste `using Domain.Client.ImagePair;` aus
`Program.cs` gestrichen. Verifiziert: `Host.Blazor` baut mit **0 Fehlern**, der volle
Solution-Build ist **grün** (128 Warnungen, davon NU1904/CS8524 vorbestehend).

**Noch offen (Cleanup, P2):** die beiden Legacy-Projekte `Domain.Client` und
`Domain.Client.Ui.Blazor` liegen weiterhin auf Disk (nicht in der `.sln`, nicht mehr
referenziert) — können bei Gelegenheit gelöscht werden. Die produktive GUI-Struktur läuft
vollständig über `Domain.Client.Modules.Blazor`.

## 8.7 Öffentliche API aus Entwicklersicht

Siehe auch [10-entwickler-api.md](10-entwickler-api.md). Konvention pro Feature-Modul (Ordner
unter `Domain.Client.Modules.Blazor/`):
1. **Modul-Klasse** `XModule : IStageModule|ISidebarModule|…` mit `Id`/`Title`/`ComponentType`
   → automatisch als `IUiModule` in DI, von der Shell in den Slot gerendert (kein manuelles
   DI/Routing).
2. **`.razor`-View** mit `@inject Store` + `@inject EffectScope Fx`; `Fx.OnChanged(store,
   StateHasChanged)`, `Fx.Dispatch(...)`, `Dispose() → Fx.Dispose()`.
3. **Store** (`partial class : StoreBase`, optional `IHydrationStore`): `void Handle(TEvent,
   MessageContext)`-Methoden = Reducer. Public Store-Properties, die andere Stores sind,
   definieren den Store-Baum (Subscription-Reihenfolge).
4. **RefreshHandler/IntentHandler** für Query-Feuerung / Intent→Command.
5. Commands/Queries/Events werden **nicht** client-seitig deklariert — sie kommen aus den
   referenzierten Backend-Assemblies; der `WiringGenerator` findet sie automatisch.

## 8.8 Design-Prinzipien & Schulden

**Prinzipien:** kein Reflection/`dynamic`; Redux/Flux (unidirektional, ein Bus, ein Signal,
transaktionsgebatcht); Symmetrie zum Backend (Command fire-and-forget, Query request/response,
Event push, dieselben Envelope-Typen); Daten-Timeline vor View-Timeline (Hydration-Gate); Modul
= Plugin; Generierung statt Handverdrahtung; Blazor Server (Circuit-Scoping).

**Schulden:**
- **Zwei Frontend-Generationen parallel auf Disk** (Aufräum-Schuld — die toten
  Legacy-Projekte `Domain.Client`/`Domain.Client.Ui.Blazor` können gelöscht werden; §8.6).
- `ProtoEndpointExtensions.cs` implementiert, aber nicht gemappt — toter Pfad.
- `GrpcProxy` schreibt viele `Console.WriteLine` (kein strukturiertes Logging); Reconnect-Logik
  rudimentär (kein sichtbarer Auto-Reconnect-Loop).
- Kein Client-Test außer `VirtualCollectionTests`; Bus/Store/Transport untestet.

**Reif & solide:** generierter Wiring-/Bus-/Store-Kern, Hydration-Bootstrap, Modul-Slot-System,
gRPC-Query-Korrelation (parallele Queries, serialisierte Writes) und die eigene
Virtualisierungs-Collection.
