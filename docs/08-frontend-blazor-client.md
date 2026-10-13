# 08 — Frontend: C#-Client-Framework und Blazor-Beispiel

Stand 2026-10-13, gegen den Code geprüft. Maßstab: docs/konzept-werkbank.md.

## 8.1 Charakterisierung

Zwei Ebenen, streng getrennt (W11):

- **Framework** — ein generierter Redux/Flux-Bus-/Store-Stack mit gRPC-Anbindung und Client-Vertrag: `Client.Infrastructure`
  (Transport, Bus, Store, Virtualisierung), `Client.SourceGeneration` (Generatoren + Vertrags-Analyzer) und der Client-Vertrag
  (`IClientVertrag`, `[assembly: IstClient<T>]`, Abstractions). Er hängt über **bidirektionales gRPC-Streaming** an `Host.Grpc`.
- **Beispiel-App** — die Blazor-Server-GUI (MudBlazor) der Beispieldomäne Bildinspektion: `Host.Blazor` (Shell, Startup) +
  `Domain.Client.Modules.Blazor` (Module). Sie ist ein Prüfling des Frameworks, nicht seine Architektur.

GUIs baut die Werkbank nicht (W12); sie bereitet nur den Client-Vertrag vor (`konzept-werkbank.md` §1).

## 8.2 Der Client-Vertrag

Ein C#-Client nennt seinen Vertrag per Assembly-Attribut; der Code ist dann gegen den Vertrag gehalten. Quelle der Begriffe und Regeln:
`docs/konzept-akteure.md` §4 (insbesondere §4.6 „C#-Client: Code = Vertrag“) und §5.2 (Handshake).

```csharp
// Beispieldomäne: Domain.Client.Modules.Blazor/Arbeitsplatz.cs
[assembly: IstClient<IArbeitsplatz>]

// Beispieldomäne: Domain.Akteure/Clients.cs
public interface IArbeitsplatz : IClientVertrag,
    ISendet<LabelBildPaar>, IFragt<SucheImagePairs>, …
{
    void Auf(ImagePairErstellt e);   // Kenntnis = Abo
    …
}
```

- **Generiert** (`WiringGenerator`, Klasse `GeneratedWiring_<Assembly>`): `Vertrag` (Handshake-Name = Interface ohne führendes `I`),
  `VertragHash` (gleiche Lesart wie der Server, `Infrastructure.SourceGeneration/Clientvertragslesart.cs` per Link), `ServerEventTypes`
  (= Kenntnis `void Auf(E)`), `CommandTypes` (= `ISendet<C>`), `RegisterQueries` (= `IFragt<Q>`), dazu `CommandAggregateTypes`,
  `HydrationStores`, `SubscribeAll`/`UnsubscribeAll`, `AddClientDomain_<Assembly>`.
- **Handshake ohne Selbstauskunft:** der Client schickt im `CapabilitiesRequest` nur Vertrag + Hash; Fähigkeiten und Abos nimmt der Server
  aus **seiner** Tabelle (`GeneratedClientVertraege`), wirksam ist Vertrag ∩ Token. Nimmt der Server den Vertrag nicht an, bricht der
  Verbindungsaufbau ab; ein abweichender Hash wird gemeldet (Client gegen anderen Stand gebaut).
- **Build-Regeln** (`Client.SourceGeneration/ClientVertrag.cs`): CQRS093 Client-Assembly ohne Vertrag, CQRS094 C#-Client trägt
  Akteur-Vertrags-Teile (Zusagen beantworten heute nur Python-Basen) — beide meldet der `WiringGenerator`; der `ClientVertragAnalyzer` meldet
  CQRS095 (erzeugter Command nicht in `ISendet`), CQRS096 (gestellte Query nicht in `IFragt`), CQRS097 (`Handle` auf ein Server-Event ohne
  Kenntnis im Vertrag). Gelesen werden Code-Fakten (Objekterzeugung, Signatur), keine Namen.
- **Verbindung** aus der Umgebung: `Clients:<Name>` (Token, Adresse, Urls, Dateien) — §8.5.

## 8.3 Client.Infrastructure — Transport, Bus, Store

Kein `dynamic`; Routing über Typen (`Dictionary<Type, …>`, Lookup per `message.GetType()`).

**Transport** (`Connection/`):
- **`GrpcProxy`** — hält die Duplex-Verbindung (`CqrsClientService.Connect`), Akteur-Token als gRPC-Header `akteur-token`. Ein
  `SemaphoreSlim` serialisiert alle Stream-Writes; Reads in `ReadLoopAsync`. Drei Nachrichtenklassen: **Commands** (gesendet ohne
  Antwort-Warten); **Queries** request/response über `ConcurrentDictionary<correlationId, TCS>` (parallele Queries); **Events** als
  Server-Push über `Channel<EventEnvelope>`. Serialisierung: `ProtoMessageMapper`.
- **`ConnectionModule`** — Brücke Bus↔gRPC. Upstream: abonniert auf dem Bus **nur die Command-Typen des Vertrags** (`CommandTypes` =
  `ISendet`), baut den `CommandEnvelope` (§8.4) und sendet. Downstream: liest Events aus dem Channel, quittiert offene Commands über die
  CorrelationId, `PostToSyncContext` → `bus.Publish` auf dem UI-Thread.
- **`QueryBridge`** — abonniert je Query-Typ des Vertrags, ruft `proxy.QueryAsync<TResponse>`, trackt `Deps` im `VersioningModule`,
  publiziert die Antwort wieder auf den Bus.
- **`VersioningModule`** — trackt Aggregat-Versionen (OCC) aus Event-`AggregateVersion` und Query-`Deps`; Read-Your-Writes: gerade
  beschriebene Aggregate gehen als `expected_fresh_ids` mit den nächsten Queries mit (`MarkWritten`).

**Bus** (`Bus/ClientBus.cs`): `IBus` mit `Dictionary<Type, List<Handler>>`. Transaktionsmodell: `IsDispatching` + `DispatchCompleted`
(genau einmal pro Root-Publish), Tiefen-Guard (`MaxDepth` 16 → Zyklus-Erkennung).

**Store** (`Abstractions/StoreBase.cs`): `ObservableObject` (CommunityToolkit.Mvvm), Redux-Analogie — `Handle()` = Reducer, `Changed` =
subscribe. **Ein** gebatchtes Signal `Changed` (sammelt `[ObservableProperty]`- und `TrackedMap`-Änderungen eines Dispatch-Zyklus, feuert im
`Flush` einmal). `IHydrationStore` markiert Stores mit Start-Daten.

**View-Anbindung** (`Abstractions/EffectScope.cs`): pro Komponente ein transienter `EffectScope` — `OnChanged(store, …)` (auch selektiv über
eine Facette oder einen Selektor), `On<TEvent>(…)`, `Dispatch(msg)`; in `Dispose()` aufgeräumt.

**Virtualisierung** (`Collections/`): `VirtualCollection`/`PagedCollection`/`Cursor`/`TrackedMap` — virtuelles Fenster mit lazy
Seiten-Nachforderung über eine `baueQuery`-Closure.

**Startup-Choreografie** (`ClientStartupService.cs`, Eingabe `ClientWiringConfig` aus dem Generat): `BootstrapState`
Connecting → Hydrating → Ready (bzw. Failed). Reihenfolge: `SubscribeAll` → `RegisterQueries` (nur `IFragt`) → Module aktivieren
(`ConnectionModule` mit `CommandTypes`, `VersioningModule` mit `ServerEventTypes`) → `ConnectAsync` als dieser Vertrag (publiziert
`ConnectionEstablished` → RefreshHandler feuern Start-Queries) → warten, bis alle `IHydrationStore.IsHydrated` (Timeout 15 s) → `Ready`. Das
schließt das „leere Liste beim Start“-Rennen konstruktiv aus.

## 8.4 Zustellgarantie für Commands

Der Client sendet at-least-once; der Server macht daraus genau einen Effekt.

- **Wiederholung bei Stille** (`ConnectionModule.SendWithRetryAsync`, `PendingCommandTracker`): vor dem Senden wird die CorrelationId
  registriert (sonst ginge eine sehr frühe Antwort verloren). Kommt binnen 5 s keine korrelierte Antwort — ein Event **oder** ein
  `CommandFailed` mit derselben CorrelationId —, sendet der Client **dieselbe Hülle** erneut, höchstens dreimal. Die `CommandId` wird je
  Command einmal erzeugt und über alle Wiederholungen beibehalten.
- **Server: Inbox vor OCC** (`Infrastructure/Aggregate/ActorSystem/CommandVorpruefung.cs`): eine bereits verarbeitete `CommandId` ergibt
  idempotent denselben Erfolg, eine bereits abgelehnte dieselbe Ablehnung; erst ein frischer Command läuft in die OCC-Prüfung. So liest ein
  wiederholter, längst erfolgreicher Command keinen falschen Versionskonflikt.
- **`ExpectedVersion`:** `ICreationCommand` → 0; sonst die aus Event-Pushes bekannte Stream-Head-Version (`CommandModus.Client`); ist keine
  bekannt (Aggregat nur per Query geladen), geht `-1` mit — der Server behandelt das wie einen emittierten Command (keine OCC, nur Inbox-Dedup).
- **`CommandFailed`** (`Abstractions/CommandFailed.cs`, `ITransientEvent`, nie im Log) geht gezielt an die auslösende Sitzung: bei Versionskonflikt,
  technischem Fehler oder falschem Ziel im Aggregat (`AggregateActorBase`), bei Ablehnung am Akteur-Tor und wenn der Dispatcher nach allen
  begrenzten Platzierungs-Versuchen aufgibt (`AggregateDispatcher`, dazu Dead-Letter). Eine fachliche Ablehnung ist dagegen ein eigenes
  transientes Event aus dem OneOf von Decide (`ITransientEvent`, im Log steht nur die Inbox-Marke) und geht ebenfalls gezielt an die
  Sitzung; auch sie quittiert den Command. Jede Sitzung ist automatisch auf `CommandFailed` abonniert.
- **Client-Events** auf dem Bus (`Connection/ConnectionEvents.cs`): `CommandSendFailed` (nicht verbunden bzw. Senden warf),
  `CommandUnbestaetigt` (nach allen Versuchen keine Antwort — Ausgang **unbekannt**, nicht „fehlgeschlagen“; erneutes Senden ist sicher),
  dazu `ConnectionEstablished`, `ConnectionLost`, `ReconnectAttempt`.

## 8.5 Client.SourceGeneration

Vier Generatoren und ein Analyzer (Überblick über alle Generatoren: [05](05-generatoren-analyzer-proto.md)):
- **`HandleMethodGenerator`** — für partial-Klassen mit `Handle(TEvent, MessageContext)`; der Rückgabetyp entscheidet die Rolle (`void` →
  Store-Reducer; `IEnumerable<T>` → synchroner Handler, der Nachrichten erzeugt; `IAsyncEnumerable<T>` → asynchron).
- **`WiringGenerator`** — sammelt Stores/Handler und den Vertrag und emittiert **eine** Klasse `GeneratedWiring_<Assembly>` (§8.2);
  `SubscribeAll` abonniert Stores **depth-first** (Eltern vor Kindern), dann Standalone-Stores, sync und async Handler.
- **`ModuleRegistryGenerator`** — jedes `IUiModule` → `AddScoped`, als `AddModules_<Assembly>`.
- **`ViewModelGenerator`** — für `IViewModel`; ohne Nutzer, toter Code (docs/13 P3-2).
- **`ClientVertragAnalyzer`** — CQRS095–097 (§8.2).

## 8.6 Beispiel-App: Host.Blazor + Domain.Client.Modules.Blazor

Alles in diesem Abschnitt ist **Beispieldomäne** (Bildinspektion, Datensätze, Training).

**Host.Blazor** (`Host.Blazor/Program.cs`, Blazor Server, net9.0):
- Start mit Umgebung: `dotnet run --project Host.Blazor -- --umgebung sim-main` (bzw. `CQRS_UMGEBUNG`). Gelesen wird
  `Clients:<Vertrag>` der Umgebung (`Abstractions/Umgebung/UmgebungKonfig.cs`, `ClientKonfig`): **Token** (Akteur-Token aus `Identitaeten`;
  leer = kein Header), **Adresse** (leer = `http://localhost:<Transport:GrpcPort>`), **Urls** (eigene URLs, Default `http://localhost:5010`),
  **Dateien** (Verzeichnisse, die `/api/files/...` ausliefern darf). Fehlt der Abschnitt, startet der Host nicht. Mit `CQRS_BRANCH` werden die
  Datei-Pfade der Sim-Sandbox aufgelöst.
- DI-Scope **pro Circuit**; der ganze Stack ist `Scoped`: `ClientBus`, `GrpcProxy`, `VersioningModule`, `ConnectionModule`, `QueryBridge`,
  `FileBridge`, `ClientStartupService`.
- Domäne und Module werden generiert eingehängt: `AddClientDomain_Domain_Client_Modules_Blazor()` + `AddModules_Domain_Client_Modules_Blazor()`.
- Endpunkte: `MapBlazorHub()`, `/api/files/...`, Fallback `_Host`. `ProtoEndpointExtensions.cs` (`/api/proto/...`) existiert, wird aber
  nicht gemappt (docs/13 P3-2).

**Modul-System.** `Host.Blazor/Shell/Shell.razor` löst `IEnumerable<IUiModule>` per DI auf und rendert jedes Modul per
`<DynamicComponent Type="m.ComponentType"/>` in seinen Slot; die Shell zeigt die Module erst bei `Ready`. Die Slot-Interfaces liegen im
Framework (`Client.Infrastructure/Abstractions`):

| Interface | Slot |
|---|---|
| `IHeaderModule` | Kopfzeile |
| `ISidebarModule` | linke/rechte Sidebar (`Side`, `ExpandedWidth`, `BarTitle`) |
| `IStageModule` | zentrale `MudTabs`-Bühne |
| `IFooterModule` | Fußzeile |
| `IHeadlessModule` | unsichtbar (nur Effekte/Datenlogik) |

`ShellState` hält Sichtbarkeit, aktiven Tab und Sidebar-Zustand; `ShellKeyBindingBuilder` die Tastaturbindungen. Die Module liegen je Ordner
in `Domain.Client.Modules.Blazor/` (Galerie, Kuratieren, Datensätze, Training-Dashboard, Statistik u. a.).

**Konvention je Feature-Modul** (Ordner unter `Domain.Client.Modules.Blazor/`):
1. **Modul-Klasse** `XModule : IStageModule|ISidebarModule|…` mit `Id`/`Title`/`ComponentType` → automatisch als `IUiModule` in DI.
2. **`.razor`-View** mit `@inject Store` + `@inject EffectScope Fx`; `Fx.OnChanged(store, StateHasChanged)`, `Fx.Dispatch(...)`,
   `Dispose() → Fx.Dispose()`.
3. **Store** (`partial class : StoreBase`, optional `IHydrationStore`): `void Handle(TEvent, MessageContext)` = Reducer. Store-Properties,
   die andere Stores sind, definieren den Store-Baum (Subscription-Reihenfolge).
4. **RefreshHandler** (feuert Queries nach Kontextwechsel) und **IntentHandler** (Client-Event → Command, `IEnumerable<object> Handle(…)`).
5. Commands/Queries/Events werden **nicht** im Client deklariert — sie kommen aus den referenzierten Domänen-Assemblies; was der Client
   davon senden, fragen und hören darf, sagt der Vertrag (CQRS095–097).

Beispiel Lesen: `PaarlistenPanel.razor` bindet eine `<VirtualList>` an `Store.VirtualImagePairs` + `Store.Cursor` und fordert Seiten über die
im Store hinterlegte `baueQuery` nach; der `PaarlisteRefreshHandler` feuert nach jedem Kontextwechsel die Query. Beispiel Schreiben: die View
dispatcht ein Client-Event, ein IntentHandler macht daraus einen Command, `ConnectionModule` sendet ihn.

## 8.7 Prinzipien

Redux/Flux (unidirektional, ein Bus, ein Signal, transaktionsgebatcht); Symmetrie zum Backend (Command ohne Antwort-Warten mit Wiederholung,
Query request/response, Event push, dieselben Envelope-Typen); Daten-Timeline vor View-Timeline (Hydration-Gate); Modul = Plugin; Generierung
statt Handverdrahtung; Vertrag statt Selbstauskunft.

## 8.8 Offen

Offene Punkte stehen nur in `docs/13`: durable Zustellung an Clients, `als` im Envelope, Zusagen über C#-Clients, Anmeldung je Person
(AK-1); kein TLS (P1-2); toter Code `IViewModel`/`ViewModelGenerator`, nicht gemappte Proto-Endpunkte (P3-2); `ActivatorUtilities` im
generierten Wiring für ViewModels (W10-2). Welche Client-Tests es gibt: [12](12-tests-und-vermessung.md).
