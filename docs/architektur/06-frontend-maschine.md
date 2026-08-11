# Architektur — Frontend-Maschine (Client)

> Die **lebende Referenz** auf die Client-Seite des Frameworks: den signalbasierten
> Blazor-/Worker-Client, der dasselbe CQRS-Backend konsumiert. Beschreibt, wie das Frontend
> heute funktioniert. Gegenstück zu den Server-Dokumenten [00](00-ueberblick.md)–[05](05-generatoren-und-analyzer.md).
>
> Stand: erhoben 2026-08-11 durch vollständige Code-Analyse; die Architektur wurde per
> In-Memory-Prüfstand als produktiv nutzbar verifiziert (siehe [Befund](#befund--reifegrad)).

## Die Leitidee: das Frontend spiegelt die sechs Invarianten

Das Frontend ist keine eigene Welt, sondern die **spiegelbildliche Fortsetzung der sechs
Invarianten** ([00](00-ueberblick.md)) auf die Client-Seite. Was im Backend „Signal ist nur
Weckruf, die Wahrheit ist der Log" heißt, heißt im Client: **„Server-Event ist nur ein
Versions-Signal, die Wahrheit kommt aus der Query."**

| Invariante | Ausprägung im Client |
|---|---|
| 1 — Wahrheit ist der Read | Inhalte kommen aus Queries (`QueryBridge`); Events tragen keine Wahrheit. |
| 2 — Signal ist nur Weckruf | Server-Events tragen im `MessageContext` nur `(AggregateId, AggregateVersion)`. |
| 3 — Routing über Typen | `ClientBus` dispatcht rein über `Dictionary<Type, List>` — nie ein Identitäts-String. |
| 4 — Keine Runtime-Reflection | Alle Verdrahtung (Subscribe, Dispatch, Query-Mapping) von Roslyn generiert. |
| 5 — Fachcode bleibt rein | Stores sind Reducer; Bus, Cursor, Flush, Hydration liegen in der Basisklasse. |
| 6 — Persistent nur wenn nötig | Client-lokale Intents/UI-Events (`IClientEvent`) verlassen den Client nie. |

## Drei Clients auf einem Vertrag

Es gibt nicht *ein* Frontend, sondern **drei First-Class-Clients desselben Backends**, die
sich dieselbe Client-Architektur teilen. Der gemeinsame Vertrag ist die kanonische
`ProtoRepo/domain.proto` plus ein bidirektionaler gRPC-Duplex-Stream (`Connect`) mit
Capabilities-Handshake.

| Client | Projekte | Rolle |
|---|---|---|
| **Blazor (modular)** | `Client.Infrastructure` + `Domain.Client.Modules.Blazor` + `Host.Blazor` | Interaktives UI (aktiv) |
| **Blazor (Legacy)** | `Domain.Client` + `Domain.Client.Ui.Blazor` | Toter Übergangscode — siehe [Befund](#befund--reifegrad) |
| **Python-Worker** | `Client.Infrastructure.Python` + `Domain.Client.Worker.Python.ML` | Headless ML-Klassifikator |

## Projektlandkarte (Client)

| Projekt | Rolle |
|---|---|
| `Client.Infrastructure` | der Framework-Kern: Bus, StoreBase, Collections, Connection, Versioning, Startup |
| `Client.SourceGeneration` | vier Roslyn-Generatoren (Dispatch, Wiring, ViewModel, ModuleRegistry) |
| `Domain.Client.Modules.Blazor` | die konkreten Feature-Module (Stores, Handler, Views) |
| `Host.Blazor` | Blazor-Server-Host + Shell; ist der gRPC-Client zum Backend |
| `Client.Infrastructure.CollectionTests` | Prüfstand: Collections + Architektur-Smoke-Test |
| `Client.Infrastructure.Python` | strukturgleiche Python-Portierung des Kerns |
| `Domain.Client.Worker.Python.ML` | Python-Worker (PyTorch-Inferenz → Command) |

## Der Kern: Bus → Store → View

Das Herz ist ein **hybrider Transaktions-Bus** (`Client.Infrastructure/Bus/ClientBus.cs`) mit
einem Batching-Modell, das genau ein Render-Signal pro Wurzel-Publish garantiert.

```
bus.Publish(evt, ctx)                     _depth++  →  IsDispatching = true
  ├─ 1. Sync-Subscriber (sofort, depth-first, gleicher Thread)
  │       Store.Handle(evt)   ← Reducer, mutiert State, sammelt nur _hasChanges
  │       VersioningModule    ← liest NUR (AggregateId, Version) aus dem Context
  ├─ 2. Async-Subscriber (fire-and-forget: ConnectionModule, QueryBridge)
  └─ finally: _depth--  →  wenn isRoot: DispatchCompleted feuert GENAU EINMAL
          Store.Flush()  →  ConsumeHasChanges() je Collection  →  Changed?.Invoke()
              EffectScope → InvokeAsync(StateHasChanged)  →  Blazor rendert
```

Drei Eigenschaften machen das robust:

- **Ein Signal pro Zyklus.** Ein Event, das N Stores und M Collections berührt, endet in
  *einem* Batch; jeder Store rendert maximal einmal. **Reentrante** Publishes (ein Handler
  publiziert ein Folge-Event) werden in denselben Wurzel-Zyklus eingefaltet —
  `DispatchCompleted` feuert erst, wenn der ganze Baum fertig ist (`ClientBus.cs:100-106`).
  `MaxDepth = 16` bricht Zyklen.
- **Grobkörnig mit Absicht.** `StoreBase` (`Abstractions/StoreBase.cs`) kennt kein
  Delta-Tracking, nur ein Boolean-Flag pro Collection — Blazor diffed die Komponente ohnehin
  vollständig. Redux-Analogie: `Handle()` = Reducer, `Changed` = `store.subscribe()`.
- **Kein Reflection.** Routing rein über `message.GetType()`.

### Getrackte Collections

Das State-Substrat der Stores (`Client.Infrastructure/Collections/`). Alle implementieren
`ITrackedCollection` mit `bool ConsumeHasChanges()`, das `StoreBase.Flush()` einsammelt.

| Typ | Zweck |
|---|---|
| `TrackedMap<TKey,TValue>` | Dictionary + stabile Reihenfolge; `Put`/`Update`/`Remove`/`ReplaceAll` |
| `PagedCollection<TKey,TRec>` | seitenbasierte Liste, zusätzlich `IVersioned` |
| `Cursor<TKey>` | Selektions-Zustand (`Id`/`Index`), `IVersioned`, mit Pending-Index |
| `VirtualCollection<TItem,TKey>` | gleitendes Fenster; **zwei Koordinatensysteme** (siehe unten) |

Die `VirtualCollection` ist der anspruchsvollste Typ: ein **stabiler Index** (bei Absorb/Insert
vergeben, nie geändert) und ein **Anzeige-Index** (= stabil + `InsertOffset`). Dadurch bleibt
der Cursor bei Live-Zuwachs oben *ohne Umnummerieren* fix — `FuegeVorneEin` ist O(1). Skeletons
(`null`) für ungeladene Chunks blockieren nie. (Deckung: `VirtualCollectionTests.cs`.)

## Das Signal-/Versionsmodell

`VersioningModule` (`Client.Infrastructure/Versioning/VersioningModule.cs`) hält nur ein
`Dictionary<Guid,int>` (AggregateId → höchste bekannte Version) und speist es aus **zwei
Quellen**:

1. **Server-Events** — der Handler ignoriert die Payload und liest nur
   `MessageContext.AggregateVersion`. Das Event ist *nur* ein Versions-Signal.
2. **Query-Deps** — die `QueryBridge` ruft `TrackFromDeps()` mit den `AggregateDep(Id, Version)`
   aus jeder Query-Response.

Verwendung: beim Command-Senden setzt das `ConnectionModule` `ExpectedVersion =
GetVersion(id) ?? 0` (bzw. immer `0` bei `ICreationCommand`). Der Client behauptet nie eine
Version aus eigenem Inhaltswissen — er reicht nur zurück, was er als Signal oder Query-Dep
empfangen hat. Das ist Invariante 2, treu auf die Client-Seite übertragen.

## Die vier Source-Generatoren

Der Kern des „kein Boilerplate, kein Reflection"-Versprechens (`Client.SourceGeneration/`).

| Generator | Trigger (Input) | Output | Zweck |
|---|---|---|---|
| **HandleMethodGenerator** | `partial class` mit `Handle(TEvent, MessageContext)`; **Rückgabetyp = Rolle**: `void`→Store, `IEnumerable<T>`→Sync-Handler, `IAsyncEnumerable<T>`→Async-Handler | `{Class}.Dispatch.g.cs` | typsicherer `Dispatch`-Switch + `SubscribedTypes`/`ProducedTypes` |
| **ModuleRegistryGenerator** | Klasse implementiert `IUiModule` | `GeneratedModuleRegistry.g.cs` | `AddScoped<IUiModule,…>` für alle UI-Module |
| **ViewModelGenerator** | `partial class : IViewModel`, private `_camelCase`-Methoden mit Rückgabe `ICommand`/`IQuery`/`IClientEvent` | `{Class}.ViewModel.g.cs` | öffentliche Methoden + `IRelayCommand` + `__InitBus`/`_publish` |
| **WiringGenerator** | Stores/Handler/ViewModels + alle Domain-Typen (auch aus Referenzen) | `GeneratedWiring.g.cs` | das zentrale Bindeglied → `ClientWiringConfig` |

Der **WiringGenerator** ist der Dirigent. Er erzeugt u.a. `SubscribeAll` in **kritischer
Reihenfolge** (Store-Baum depth-first → Standalone-Stores → Sync-Handler → Async-Handler,
damit der Store beim Handler-Lauf schon aktualisiert ist), baut den Store-Baum daraus, dass
ein Store einen anderen als public Property hält, und registriert die `ClientWiringConfig`
(Singleton-Record mit 7 Feldern) — die **einzige** Brücke zwischen Framework und Domäne. Der
Framework-Kern importiert keine einzige Domänen-Klasse.

## Anatomie eines Feature-Moduls

Ein Modul ist über Namenskonventionen aus fünf Bausteinen zusammengesetzt; der Generator
verdrahtet sie:

1. **`XxxModule.cs`** — Metadaten-Klasse; implementiert ein Regions-Interface
   (`IStageModule`/`ISidebarModule`/`IFooterModule`/`IHeaderModule`/`IHeadlessModule`),
   liefert `Id`/`Title`/`ComponentType` und deklariert **KeyBindings als reine Daten**.
2. **Store** — `StoreBase`-Reducer. Häufig **ein** geteilter `partial class Store` über die
   Feature-Ordner plus mehrere eigenständige Stores.
3. **Events** — reine Records. `IClientEvent` = client-lokal, geht nie an den Server.
4. **Handler** — `RefreshHandler` (löst Queries aus) und `IntentHandler` (Intent→Command).
5. **View** (`.razor`) — injiziert Store + `EffectScope`, abonniert `Changed`, dispatcht.

### Der tragende Loop: Intent → Command → Event → Store → View

Die zentrale Design-Entscheidung ist die **Dreiteilung der Nachrichten** (durch Konvention,
nicht Typhierarchie):

```
View dispatcht INTENT           kontextfrei, testbar (IClientEvent)
  → IntentHandler liest Store    ergänzt fehlenden Kontext (z.B. Cursor.Id)
    → produziert COMMAND         mit Aggregat-ID
      → Server verarbeitet
        → SERVER-EVENT zurück    trägt (AggregateId, Version)
          → Store-Reducer patcht die Collection in-place per Key
            → ein Changed-Signal → selektives Re-Render
```

Es gibt **keine Optimistic Updates** — der Store wartet auf das Server-Event. Ein `Patch`
verpufft folgenlos, wenn der Key (noch) nicht geladen ist → robuste eventual consistency.
Module halten *keine* direkten Referenzen aufeinander; die einzige Kopplung ist, dass ein
Handler seinen Store per Ctor-Injection **lesen** darf.

**Views** binden über drei `EffectScope.OnChanged`-Overloads: „bei jeder Änderung", „nur bei
Versionsanstieg einer Facette" (rendert initial einmal — löst das „leere Liste beim
Start"-Problem), „nur bei geändertem Selektor". Render-Optimierung per `ShouldRender()` mit
`ReferenceEquals` auf den Record: nur die gepatchte Zeile bekommt eine neue Referenz, der Rest
rendert nicht neu.

## Shell, Host & Circuit-Lifecycle

**Der Blazor-Server ist der gRPC-Client zum Backend.** Der Browser spricht nur via
SignalR-Circuit mit dem Host; der Host öffnet den gRPC-Stream zu `Host.Grpc`.

- **Ein DI-Scope = ein Circuit = eine Nutzerverbindung.** Bus, Stores, Module, Connection sind
  `Scoped` → jeder Nutzer hat einen isolierten Objektgraphen, kein Cross-Talk.
- **Shell-Regionen:** die `Shell` injiziert `IEnumerable<IUiModule>`, sortiert per `OfType<>`
  + Sichtbarkeit + `OrderBy(Order)` in Header / Left-Sidebar / Stage (Tabs) / Right-Sidebar /
  Footer / Headless und rendert jedes über `<DynamicComponent Type="m.ComponentType"/>`.
- **Circuit-Lifecycle** (`CircuitHandlerBase`): `OnCircuitOpened → StartAsync`;
  `OnCircuitClosed → StopAsync` (Hydration-Reset + Detach aller Stores).
- **Keybindings:** Prioritäts-Layer Footer < Header < Sidebar < aktive Stage < Shell; nur die
  aktive Stage steuert bei; Tastendruck → aufgelöste Message auf den Bus.

### Das Hydration-Gate — Daten-Timeline vor View-Timeline

`ClientStartupService` durchläuft `Connecting → Hydrating → Ready`. **`Ready` heißt: alle als
`IHydrationStore` markierten Stores haben ihre Start-Daten** (`MarkHydrated()`, 15 s Timeout).
Die Shell mountet die Module erst bei `Ready` — das „leere Liste beim Start"-Rennen ist
**konstruktiv** ausgeschlossen. Wichtiges Detail: eine leere Ergebnismenge ist „hydriert, aber
leer", nicht „noch am Laden".

## Der Python-Worker — strukturgleiche Portierung

`Client.Infrastructure.Python` ist eine bewusste 1:1-Portierung; die Modul-Dokstrings benennen
die Gegenstücke wörtlich:

| Python | .NET |
|---|---|
| `dispatch.py` (`type(payload)`-Lookup, Metaklasse `HandlerMeta`) | `ClientBus` + `HandleMethodGenerator` |
| `versioning.py` | `VersioningModule` |
| `proxy.py` + `connection.py` (Reconnect mit Exp-Backoff) | `GrpcProxy` + `ConnectionModule` |
| `registry.py` + `generate_registry.py` | die Roslyn-Generatoren |

Statt Roslyn nutzt Python **betterproto-Deskriptoren** (oneof-Field-Metadaten) zur
Typklassifikation — dieselbe Reflection-Vermeidung (Invariante 4). Der ML-Worker konsumiert
`BildVerfuegbar`/`ImagePairKomplett`-Events, lädt Bilder per HTTP vom `/api/files/`-Endpunkt,
führt TorchScript-Inferenz aus und **emittiert genau einen Command** zurück ins Aggregat —
dasselbe Muster wie eine Backend-Reaktion, nur außerhalb des Clusters.

## Befund & Reifegrad

**Die Architektur ist produktiv nutzbar — verifiziert.** Ein In-Memory-Architektur-Smoke-Test
(`Client.Infrastructure.CollectionTests/ArchitectureSmokeTests.cs`, auf dem Frontend-Branch)
verdrahtet eine **fremde Wegwerf-Domäne** exakt so, wie es der WiringGenerator täte, und belegt
die tragenden Fähigkeiten unabhängig von der ImagePair-Domäne: reaktive Schleife + Batching,
reentranter Publish, Intent→Command→Server-Event→Patch, Hydration-Gate, Versions-Signal,
selektives Rendern, Wiring-Vertrag. Ergebnis: **33/33 grün** (8 neu + 25 VirtualCollection),
net9.0.

**Bewusst noch nicht abgedeckt / offen:**

- **Generator-Kette nicht end-to-end getestet.** Der Smoke-Test schreibt den Dispatch-/
  Wiring-Code von Hand (wie der Generator ihn erzeugt); der Roslyn-Generator-Lauf selbst ist
  nicht abgesichert. Nächster Beweisschritt.
- **Connection-Schicht (gRPC).** `GrpcProxy`/`ConnectionModule`/`QueryBridge` brauchen einen
  echten Server (Integration, nicht in-memory) und sind im Smoke-Test nur per Bus-Publish
  simuliert.
- **`MapProtoEndpoint`** (Proto-Distribution an Python-Clients) ist implementiert, aber in
  `Host.Blazor/Program.cs` **nicht registriert** — nur `/api/files` ist aktiv verdrahtet.
- **`ClientBus` mit `null`-SynchronizationContext** (`Program.cs`) → `PostToSyncContext` läuft
  synchron inline, nicht über den Blazor-Renderer-Context.

**Legacy-Rückbau (empfohlen):** `Domain.Client` und `Domain.Client.Ui.Blazor` sind toter
Übergangscode aus dem Client-Generator-Umbau (ViewModel-Muster → IntentHandler-Muster;
Store-Baum → partieller Feature-Store + `VirtualCollection`). Belege:

- Beide sind **nicht in der Solution** (`CqrsSolution.sln`).
- `Domain.Client` baut nicht (`_publish` fehlt — der `ViewModelGenerator` ist dort nicht mehr
  als Analyzer eingebunden).
- Der aktive Host rendert `<Shell />` aus `Host.Blazor.Shell` und verdrahtet die **neuen**
  Module (`AddClientDomain_…`/`AddModules_…`); die alte `Ui.Blazor`-Shell wird nicht angesteuert.
- Es verbleiben genau **zwei tote Verweise**, beide in `Host.Blazor`: die ProjectReference auf
  `Domain.Client.Ui.Blazor` (`Host.Blazor.csproj`) und ein ungenutzter `using
  Domain.Client.ImagePair;` (`Program.cs`). Die ProjectReference zieht das nicht-baubare
  `Domain.Client` transitiv in den Host-Build.

Sauberer Rückbau: beide Verweise entfernen, dann die Ordner `Domain.Client/` und
`Domain.Client.Ui.Blazor/` löschen (kein `.sln`-Eingriff nötig). Das entfernt nicht nur toten
Code, sondern nimmt auch das nicht-baubare Legacy aus der Host.Blazor-Kette.
