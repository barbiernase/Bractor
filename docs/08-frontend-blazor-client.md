# 08 — Frontend: C#-Client-Framework und Blazor-Beispiel

Stand 2026-10-29, gegen den Code geprüft.

## 8.1 Charakterisierung

Zwei Ebenen, streng getrennt (W11):

- **Framework** — ein generierter Redux/Flux-Bus-/Store-Stack mit gRPC-Anbindung und Client-Vertrag: `Client.Infrastructure`
  (Transport, Bus, Store, Virtualisierung), `Client.SourceGeneration` (Generatoren + Vertrags-Analyzer) und der Client-Vertrag in
  `Abstractions/Akteur.cs` (`IClientVertrag`, `ISendet<T>`, `IFragt<T>`, `IHoert<T>`, `ITraegt<F>`, `[assembly: IstClient<T>]`). Er hängt
  über **eine bidirektionale gRPC-Verbindung** (`CqrsClientService.Connect`, `Cqrs.Leitung/cqrs.proto`) an `Host.Grpc`; dort ist sie eine
  Sitzung (§8.3).
- **Beispiel-App** — die Blazor-Server-GUI (MudBlazor) der Beispieldomäne Bildinspektion: `Host.Blazor` (Shell, Startup) +
  `Domain.Client.Modules.Blazor` (Module). Sie ist ein Prüfling des Frameworks, nicht seine Architektur.

GUIs baut die Werkbank nicht (W12); sie bereitet nur den Client-Vertrag vor.

## 8.2 Der Client-Vertrag

Ein C#-Client nennt seinen Vertrag per Assembly-Attribut; der Code ist dann gegen den Vertrag gehalten. Grammatik:
[10](10-entwickler-api.md) §10.3.6 und §10.5; Begriffe und Rechte: [konzept-akteure.md](konzept-akteure.md).

```csharp
// Beispieldomäne: Domain.Client.Modules.Blazor/Arbeitsplatz.cs
[assembly: IstClient<IArbeitsplatz>]

// Beispieldomäne: Domain.Akteure/Clients.cs
public interface IArbeitsplatz : IClientVertrag,
    ISendet<LabelBildPaar>, IFragt<SucheImagePairs>, IHoert<ImagePairErstellt>, …;
```

- **Generiert** (`WiringGenerator`, Klasse `GeneratedWiring_<Assembly>`): `Vertrag` (Handshake-Name = Interface ohne führendes `I`),
  `VertragHash` (dieselbe Lesart wie der Server: `Infrastructure.SourceGeneration/Clientvertragslesart.cs` per Link), `ServerEventTypes`
  (= `IHoert`), `CommandTypes` (= `ISendet`), `RegisterQueries` (= `IFragt`), `CommandAggregateTypes` (das Ziel-Aggregat jedes gesendeten
  Commands aus `ICommand<TAggregat>`), `HydrationStores`, `SubscribeAll`/`UnsubscribeAll` und `AddClientDomain_<Assembly>`, das alles als
  `ClientWiringConfig` registriert.
- **Handshake ohne Selbstauskunft:** der Client schickt im `CapabilitiesRequest` nur Vertrag + Hash (`GrpcProxy.ConnectAsync`). Der Server
  (`SitzungsActor.HandshakeAsync`, `Infrastructure/Akteure/ClientVertragsPruefung.cs`) sucht den Vertrag in **seiner** generierten Tabelle
  (`GeneratedClientVertraege`); wirksam ist Vertrag ∩ Akteure des Tokens, gehört wird genau, was der Vertrag hört. Ein unbekannter Vertrag oder ein
  Token, das keinen seiner Akteure verkörpert, beendet die Sitzung (`VERTRAG_ABGELEHNT`); ein abweichender Hash ist eine Warnung, im strengen
  Modus (`AkteurOptionen.VertragStreng`) eine Ablehnung. Der Client prüft die Antwort: nennt sie nicht seinen Vertrag, bricht `ConnectionModule.ConnectAsync`
  ab; weicht nur der Hash ab, meldet er es auf `stderr` (Client gegen anderen Stand gebaut).
- **Build-Regeln** (`Client.SourceGeneration/ClientVertrag.cs`): CQRS093 (Client-Assembly ohne Vertrag) und CQRS094 (C#-Client trägt Funktionen,
  `ITraegt`; das kann heute nur das Python-SDK) meldet der `WiringGenerator`; der `ClientVertragAnalyzer` meldet CQRS095 (erzeugter Command nicht
  in `ISendet`), CQRS096 (gestellte Query nicht in `IFragt`), CQRS097 (`Handle(E, MessageContext)` auf ein Server-Event ohne `IHoert<E>`).
  Gelesen werden Code-Fakten (Objekterzeugung, Signatur), keine Namen.
- **Verbindung** aus der Umgebung: `Clients:<Vertrag>` (Token, Adresse, Urls, Dateien) — §8.6.

## 8.3 Leitung und Client.Infrastructure — Transport, Bus, Store

**Was über die Leitung geht** (`Cqrs.Leitung/cqrs.proto`; Domänen-Nachrichten als `Nutzlast { typ, daten }`, Ids als UUID-Text):

| Richtung | Nachricht | im C#-Client |
|---|---|---|
| Client → Server | `CapabilitiesRequest` (Vertrag, Hash) | `GrpcProxy.ConnectAsync` |
| | `CommandRequest` (`CommandEnvelopeDto`: Command-Id, Ziel, `expected_version`, Korrelation) | `GrpcProxy.SendCommandAsync` |
| | `QueryRequest` (Korrelation, Query, `mindestens` = „S auf v“) | `GrpcProxy.QueryAsync` |
| | `LiesLog` (ab Position, höchstens n) | `GrpcProxy.LiesLogAsync` |
| Server → Client | `CapabilitiesResponse`, `EventNotification` (Push, gezielte Antwort), `QueryResponse` (`deps` = Stand je gezeigter Id, `noch_nicht`), `Aktualisiert` (Queries), `LogSeite`, `ErrorResponse` | `GrpcProxy.HandleServerMessageAsync` |

Hinweise (`TransientEventRequest`), Antworten auf weitergeleitete Queries und getragene Funktionen (`FunktionenAnbieten`, `ArbeitsAuftrag`,
`ArbeitsErgebnis`) nutzt nur das Python-SDK ([09](09-python-sdk.md)); der C#-Client sendet von `ISendet` nur Commands (`ConnectionModule`
abonniert alle `CommandTypes`, gibt aber nur `ICommand` weiter).

**Am Server** ([06](06-transport-multinode-betrieb.md) für den Transport):
- **`CqrsClientServiceImpl`** (`Infrastructure/GrpcClient/CqrsClientService.cs`) prüft je Verbindung das Akteur-Token (Header `akteur-token`,
  ungültig = `Unauthenticated`), startet die Sitzung und reicht in seiner Lese-Schleife jede Client-Nachricht nur weiter (`VonClient`).
- **`SitzungsActor`** (`Infrastructure/GrpcClient/SitzungsActor.cs`) ist der einzige Schreiber am Antwort-Stream; Sitzungs-Id `<Knoten>#<Nr>`
  (`Verteilung.SitzungsId`). Er stempelt Commands mit seiner Id als `OriginSessionId` und dem Akteur des Tokens, prüft das Akteur-Tor
  (verweigert = gezieltes `CommandFailed`), setzt bei `expected_version < 0` den Modus `Emittiert` und dispatcht; Queries laufen neben der
  Mailbox (`IQueryDienst.ExecuteAsync` mit „mindestens S auf v“), die Antwort wird danach im Actor geschrieben.
- **`VerteilerActor`** (`Infrastructure/Verteilen/Verteiler.cs`, fester Name `verteiler` je Knoten): Tabelle Typ → Sitzungen dieses Knotens,
  dazu Sitzungs-Id → Sitzung. `Verteilung.Melde` schickt je Commit **eine** Nachricht an den Verteiler jedes Knotens — nur Typen, die irgendein
  Client hören darf; gezielte Envelopes (`ZielSitzung`) nur an den Knoten aus der Sitzungs-Id. Der Verteiler fasst je Commit und Sitzung zu einer
  `Zustellung` zusammen und verwirft über 1000 offenen Zustellungen einer Sitzung (`Rueckstau`). Kein Broker, keine Abos im Cluster; verlierbar
  (Garantien: [11](11-garantien.md) §11.9).

**Aktualisieren** („P hat S weiterverarbeitet“): die Sitzung merkt sich je gehaltener Antwort (höchstens 200) die Streams ihrer `deps`, bei einer
Antwort ohne Ids grob die Projektion dahinter (`SitzungsActor.Beobachte`), und meldet das dem Verteiler (`SitzungBeobachtet`). Nach jedem
Projektions-Commit, der Events anwandte, geht `Verarbeitet(Projektion, Streams)` an alle Verteiler (`Verteilung.MeldeVerarbeitet`); betroffen
ist jede Sitzung, die einen dieser Streams beobachtet (gleich, welche Projektion ihn verarbeitet hat) bzw. grob diese Projektion. Die Sitzung sammelt
200 ms und schickt dem Client `Aktualisiert` mit den betroffenen Queries. So kommt auch eine transitive Änderung an (ein fremdes Label in einer
Datensatz-Ansicht, deren Antwort das Paar zeigt). Antworten eines Aggregats (direkte Abfrage) werden nicht beobachtet (docs/13 FB-19).

**Im Client** gilt: kein `dynamic`; Routing über Typen (`Dictionary<Type, …>`, Lookup per `message.GetType()`).

**Transport** (`Connection/`):
- **`GrpcProxy`** — hält die Duplex-Verbindung, Akteur-Token als gRPC-Header `akteur-token`. Ein `SemaphoreSlim` serialisiert alle
  Stream-Writes; Reads in `ReadLoopAsync`. Commands gehen ohne Antwort-Warten hinaus; Queries und `LiesLog` sind request/response über
  `ConcurrentDictionary<Korrelation, TCS>` (parallel); Pushes über `Channel<EventEnvelope>`, `Aktualisiert` über `Channel<IQuery>`. Ein
  `ErrorResponse` mit Korrelation lässt die passende Query bzw. Log-Lesung mit `QueryException` scheitern. Serialisierung: `ProtoMessageMapper`
  mit der Nutzlast-Tabelle der Anwendung (`GeneratedNutzlast`). Neu verbinden tut der Proxy nicht selbst.
- **`ConnectionModule`** — Brücke Bus↔gRPC. Upstream: abonniert auf dem Bus die `CommandTypes` des Vertrags, baut den `CommandEnvelope` (§8.4)
  und sendet. Downstream: liest Pushes aus dem Channel, quittiert offene Commands über die Korrelation (`PendingCommandTracker.Ack`) und
  publiziert per `PostToSyncContext` auf dem UI-Thread; nennt der Server Queries (`Aktualisiert`), legt es sie wieder auf den Bus.
- **`QueryBridge`** — abonniert je Query-Typ des Vertrags (`Register<TQuery, TResponse>`, aus `RegisterQueries`), ruft
  `QueryAsync<TResponse>` mit `VersioningModule.Mindestens` (Read-your-writes: die Leseseite wartet begrenzt, bis ihre Projektion jeden dieser
  Streams so weit hat, sonst `noch_nicht` mit dem Stand davor — der Client nimmt die Antwort trotzdem), übernimmt die `deps` ins
  `VersioningModule` und publiziert die Antwort. Keine Nachfrage-Schleife im Client. Fehler werden zu `QueryFailed`.
- **`VersioningModule`** (`Versioning/`) — **eine** Version je Aggregat (die Zahl seiner Events), monoton aus Pushes der gehörten Typen
  (`ServerEventTypes`; der Kopf nach dem Commit `StreamHeadVersion`, sonst die Version des Events) und aus den `deps` der Antworten. Der Stand
  einer Antwort ist der Stand der Projektion je gezeigter Id (`Infrastructure/Projections/ProjektionsStaende.cs`: die Version des zuletzt
  angewandten Events, bis vor das nächste gehörte Event bzw. bis zum Kopf), bei einer direkten Aggregat-Abfrage die Version des Aggregats
  (`AggregatAbfrage`). Die Version ist die `ExpectedVersion` des nächsten Commands. `MarkWritten` merkt beim Senden das Ziel (die letzten 64);
  `Mindestens` nennt davon jedes mit bekannter Version — bekannt wird die neue Version nur aus einem Push, den der Client hört.
- **Lesen ab Position** — `GrpcProxy.LiesLogAsync(ab)` liefert das Log ab einer Position, nur Event-Typen, die die Sitzung hört und die im Log
  stehen, nur bis zum Horizont (`SitzungsActor.LogSeite`, höchstens 1000 je Seite); wer ab `Bis` weiterliest, verpasst nichts, auch nach dem
  Wiederverbinden. Der Push bleibt die Abkürzung. `ConnectionModule` und der Blazor-Client rufen es nicht auf.

**Bus** (`Bus/ClientBus.cs`): `IBus` mit `Dictionary<Type, List<Handler>>`. Transaktionsmodell: `IsDispatching` + `DispatchCompleted`
(genau einmal pro Root-Publish), Tiefen-Guard (`MaxDepth` 16 → Zyklus-Erkennung).

**Store** (`Abstractions/StoreBase.cs`): `ObservableObject` (CommunityToolkit.Mvvm), Redux-Analogie — `Handle()` = Reducer, `Changed` =
subscribe. **Ein** gebatchtes Signal `Changed` (sammelt `[ObservableProperty]`- und `TrackedMap`-Änderungen eines Dispatch-Zyklus, feuert im
`Flush` einmal). `IHydrationStore` (`Abstractions/Markers.cs`) markiert Stores mit Start-Daten.

**View-Anbindung** (`Abstractions/EffectScope.cs`): pro Komponente ein transienter `EffectScope` — `OnChanged(store, …)` (auch selektiv über
eine Facette oder einen Selektor), `On<TEvent>(…)`, `Dispatch(msg)`; in `Dispose()` aufgeräumt.

**Virtualisierung** (`Collections/`): `VirtualCollection`/`PagedCollection`/`Cursor`/`TrackedMap` — virtuelles Fenster mit lazy
Seiten-Nachforderung über eine `baueQuery`-Closure.

**Startup-Choreografie** (`ClientStartupService.cs`, Eingabe `ClientWiringConfig` aus dem Generat): `BootstrapState`
Connecting → Hydrating → Ready (bzw. Failed). Reihenfolge: `SubscribeAll` → `RegisterQueries` (nur `IFragt`) → Module aktivieren
(`ConnectionModule` mit `CommandTypes`, `VersioningModule` mit `ServerEventTypes`, `FileBridge`) → `ConnectAsync` als dieser Vertrag
(publiziert `ConnectionEstablished` → RefreshHandler feuern Start-Queries) → warten, bis alle `IHydrationStore.IsHydrated` (Timeout 15 s) →
`Ready`. Das schließt das „leere Liste beim Start“-Rennen konstruktiv aus. `StopAsync` setzt die Hydration zurück, trennt die Stores vom Bus und
schließt die Verbindung.

## 8.4 Zustellung von Commands

Der Client sendet at-least-once mit fester Command-Id; die Inbox des Ziels macht daraus genau eine Wirkung (Garantien: [11](11-garantien.md)
§11.3).

- **Hülle** (`ConnectionModule.HandleCommandAsync`): `CommandId` einmal je Command (`Guid.NewGuid()`), über alle Wiederholungen dieselbe;
  Korrelation aus dem `MessageContext` oder neu; `AggregateType` aus `CommandAggregateTypes`; `ExpectedVersion`: `ICreationCommand` → 0, sonst
  die bekannte Version aus dem `VersioningModule` (`CommandModus.Client`, OCC), ist keine bekannt, `-1` — der Server behandelt das wie einen
  emittierten Command (keine OCC, nur Inbox-Dedup).
- **Ausgangskorb** (`Connection/Ausgangskorb.cs`: `IAusgangskorb`; im Speicher `Ausgangskorb`, als Verzeichnis `DateiAusgangskorb`, je Command
  eine Datei mit der Proto-Hülle): der Command liegt darin, **bevor** er gesendet wird, bis seine korrelierte Antwort kam. Beim nächsten
  `ConnectAsync` desselben `ConnectionModule` gehen die offenen mit derselben Command-Id und Korrelation an die neue Sitzung
  (`SendeOffeneAsync`); mit dem Datei-Korb auch nach einem Neustart des Clients. Ohne Registrierung nimmt das `ConnectionModule` den Korb im
  Speicher — so in `Host.Blazor`, wo jeder Circuit einen eigenen Stack hat.
- **Wiederholung bei Stille** (`ConnectionModule.SendWithRetryAsync`, `PendingCommandTracker`): die Korrelation wird vor dem Senden registriert.
  Kommt binnen 5 s keine korrelierte Antwort, sendet der Client dieselbe Hülle erneut, höchstens dreimal; danach ist der Command aus dem Korb
  und es kommt `CommandUnbestaetigt`. Wirft das Senden (Verbindung weg), bleibt er im Korb (`CommandSendFailed`); ist der Client schon beim
  Absenden nicht verbunden, kommt nur `CommandSendFailed`, und der Command liegt nicht im Korb.
- **Server: Inbox vor OCC** (`Infrastructure/Aggregate/ActorSystem/CommandVorpruefung.cs`): eine bereits gewirkte Command-Id ergibt idempotent
  Erfolg, eine bereits abgelehnte dieselbe Ablehnung; erst ein frischer Command läuft in die Versionsprüfung. So liest eine Wiederholung keinen
  falschen Versionskonflikt.
- **Was als Antwort zählt:** jeder Push mit derselben Korrelation — ein Event, das der Client hört, eine fachliche Ablehnung oder ein
  `CommandFailed`. Ablehnung und `CommandFailed` gehen gezielt an die Sitzung aus `OriginSessionId`, gleich, was sie hört: die Ablehnung (das
  transiente Event aus dem `OneOf` von Decide, nie im Log; die Inbox hält „abgelehnt“) aus dem Aggregat; `CommandFailed`
  (`Abstractions/CommandFailed.cs`, `ITransientEvent`) bei Versionskonflikt, technischem Fehler oder falschem Ziel im Aggregat
  (`AggregateActorBase`), bei Verweigerung am Akteur-Tor (`AkteurVerweigerung`) und wenn der Dispatcher nach allen Platzierungs-Versuchen aufgibt
  (`AggregateDispatcher`, dazu Dead-Letter). Ein Erfolg, dessen Events der Client nicht hört, ein Noop und eine idempotent beantwortete Wiederholung
  bringen keinen Push; sie enden als `CommandUnbestaetigt`.
- **Client-Events** auf dem Bus (`Connection/ConnectionEvents.cs`): `CommandSendFailed` (nicht verbunden bzw. Senden warf),
  `CommandUnbestaetigt` (Ausgang **unbekannt**, nicht „fehlgeschlagen“; erneutes Senden mit derselben Id ist sicher), `ConnectionEstablished`,
  `ConnectionLost`; `ReconnectAttempt` ist definiert, wird aber nie ausgelöst (niemand setzt `ConnectionState.Reconnecting`).

## 8.5 Client.SourceGeneration

Vier Generatoren und ein Analyzer (Überblick über alle Generatoren: [05](05-generatoren-analyzer-proto.md)):
- **`HandleMethodGenerator`** — für partial-Klassen mit `Handle(TEvent, MessageContext)`; der Rückgabetyp entscheidet die Rolle (`void` →
  Store-Reducer; `IEnumerable<T>` → synchroner Handler, der Nachrichten erzeugt; `IAsyncEnumerable<T>` → asynchron).
- **`WiringGenerator`** — sammelt Stores/Handler und den Vertrag und emittiert **eine** Klasse `GeneratedWiring_<Assembly>` (§8.2);
  `SubscribeAll` abonniert Stores **depth-first** (Eltern vor Kindern), dann Standalone-Stores, sync und async Handler.
- **`ModuleRegistryGenerator`** — jedes `IUiModule` → `AddScoped<IUiModule, …>`, als `AddModules_<Assembly>`.
- **`ViewModelGenerator`** — für `IViewModel`; ohne Nutzer, toter Code (docs/13 P3-2).
- **`ClientVertragAnalyzer`** — CQRS095–097 (§8.2).

## 8.6 Beispiel-App: Host.Blazor + Domain.Client.Modules.Blazor

Alles in diesem Abschnitt ist **Beispieldomäne** (Bildinspektion, Datensätze, Training).

**Host.Blazor** (`Host.Blazor/Program.cs`, Blazor Server, net9.0):
- Start mit Umgebung: `dotnet run --project Host.Blazor -- --umgebung sim-main` (bzw. `CQRS_UMGEBUNG`). Gelesen wird
  `Clients:<Vertrag>` der Umgebung (`Abstractions/Umgebung/UmgebungKonfig.cs`, `ClientKonfig`): **Token** (Akteur-Token aus `Identitaeten`;
  leer = kein Header), **Adresse** (leer = `http://localhost:<Transport:GrpcPort>`), **Urls** (eigene URLs, Default `http://localhost:5010`),
  **Dateien** (Verzeichnisse, die `/api/files/...` ausliefern darf). Fehlt der Abschnitt, startet der Host nicht. `${branch}` in der Umgebung
  ersetzt `CQRS_BRANCH` (Default `main`, `UmgebungLader`).
- DI-Scope **pro Circuit**; der ganze Stack ist `Scoped`: `ClientBus`, `GrpcProxy`, `VersioningModule`, `ConnectionModule`, `QueryBridge`,
  `FileBridge`, `ClientStartupService`. `CircuitHandlerBase` startet ihn beim Öffnen des Circuits und stoppt ihn beim Schließen.
- Domäne und Module werden generiert eingehängt: `AddClientDomain_Domain_Client_Modules_Blazor()` + `AddModules_Domain_Client_Modules_Blazor()`.
- Endpunkte: `MapBlazorHub()`, `/api/files/...` (`FileEndpointExtensions.cs`), Fallback `_Host`. `ProtoEndpointExtensions.cs`
  (`/api/proto/...`) existiert, wird aber nicht gemappt (docs/13 P3-2).

**Modul-System.** `Host.Blazor/Shell/Shell.razor` löst `IEnumerable<IUiModule>` per DI auf und rendert jedes Modul per
`<DynamicComponent Type="m.ComponentType"/>` in seinen Slot; die Shell zeigt die Module erst bei `Ready`. Die Slot-Interfaces liegen im
Framework (`Client.Infrastructure/Abstractions`):

| Interface | Slot |
|---|---|
| `IHeaderModule` | Kopfzeile |
| `ISidebarModule` | linke/rechte Sidebar (`Side`, `ExpandedWidth`, `BarWidth`, `BarTitle`) |
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
   davon senden, fragen und hören darf, sagt der Vertrag (CQRS095–097). Verweise auf Aggregate sind auch im Client-Zustand typisierte Ids
   (`Id<T>`; kurze Namen in `Domain.Client.Modules.Blazor/Kennungen.cs`); neue Ids würfelt der Client mit `Id<T>.Neu()` (z. B.
   `Modelle/ModellIntentHandler.cs`).

Beispiel Lesen: `Paarliste/PaarlistenPanel.razor` bindet eine `<VirtualList>` an `Store.VirtualImagePairs` + `Store.Cursor` und fordert Seiten
über die im Store hinterlegte `baueQuery` nach; der `PaarlisteRefreshHandler` feuert nach jedem Kontextwechsel die Query. Beispiel Schreiben: die
View dispatcht ein Client-Event, ein IntentHandler macht daraus einen Command, `ConnectionModule` sendet ihn.

## 8.7 Prinzipien

Redux/Flux (unidirektional, ein Bus, ein Signal, transaktionsgebatcht); Symmetrie zum Backend (Command ohne Antwort-Warten mit Wiederholung,
Query request/response, Event push, dieselben Envelope-Typen); Daten-Timeline vor View-Timeline (Hydration-Gate); Modul = Plugin; Generierung
statt Handverdrahtung; Vertrag statt Selbstauskunft.

## 8.8 Offen

Offene Punkte stehen nur in `docs/13` §13.0: `als` im Envelope, Funktionen über C#-Clients tragen, Anmeldung je Person (AK-1); kein TLS
(P1-2); Client-Framework hängt am Server-Framework (`ProtoMessageMapper`, W2-1); Lesen nach Schreiben einmal verfehlt (G-16); Rückstau an der
Sitzung nur als Log-Warnung (FB-15); Aggregat-Antworten ohne `Aktualisiert` (FB-19); `GrpcProxy` loggt per `Console.WriteLine` (P2-10); toter
Code `IViewModel`/`ViewModelGenerator`, nicht gemappte Proto-Endpunkte (P3-2); `ActivatorUtilities` im generierten Wiring für ViewModels
(W10-2). Welche Client-Tests es gibt: [12](12-tests-und-vermessung.md).
