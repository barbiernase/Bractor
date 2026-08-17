# Umsetzung & Handoff — MEILENSTEIN 9: Blazor-Module (Datensatz & Training GUI)

> **Zweck:** Vollständiger Handoff für einen frischen Agenten, der **M9** aus
> [`konzept-training-und-datensatz.md`](konzept-training-und-datensatz.md) §8 **implementiert** —
> die Blazor-GUI für Datensatz-Komposition und Trainings-Dashboard.
>
> Lies zuerst: dieses Dokument, dann **[`08-frontend-blazor-client.md`](08-frontend-blazor-client.md)**
> (Frontend-Architektur im Ist-Zustand), das Konzept §8, und **CLAUDE.md** (Konventionen).
>
> **Basis-Branch:** `claude/imageoair-frontend-setup-0g0s4b`. **M1–M8 sind fertig, gebaut und grün**
> (dieser Branch). Du baust auf einem vollständigen Backend + Read-Seite + Query-Kanal + Python-Worker auf.

---

## 0. Wo wir stehen (M1–M8 fertig — was schon existiert und aufrufbar ist)

Der **gesamte C#-Backend-Teil + Python** ist gebaut. Für M9 zählt: **die Schreib-, Lese- und
Query-Seite steht** — du rufst sie nur noch aus der GUI. Konkret schon da (mit exakten Typnamen,
alle in `Domain/` bzw. `Domain.Projections`):

### Aggregate + Commands (die die GUI SENDET)
- **`Domain/Datensatz`** (`Domain.Datensatz`): `ErstelleDatensatz(AggregateId, Name)`,
  `FuegeRangeHinzu(AggregateId, RangeKriterien)`, `NimmPaarAuf(AggregateId, ImagePairId)`,
  `EntfernePaar(AggregateId, ImagePairId)`, `SetzeSplit(AggregateId, TrainProzent, ValProzent, TestProzent, Seed)`,
  `FriereEin(AggregateId)`.
  → **`NimmRangeAuf` und `SchliesseEinfrierenAb` NICHT von der GUI senden** — die löst der
  server-seitige `DatensatzResolverPipeline` (M5) selbst aus (`RangeAngefordert`→SearchAsync→`NimmRangeAuf`;
  `EinfrierenAngefordert`→Snapshot→`SchliesseEinfrierenAb`). Die GUI sendet nur `FuegeRangeHinzu`/`FriereEin`.
- **`Domain/Trainingslauf`** (`Domain.Trainingslauf`): `StarteTraining(AggregateId, DatensatzId, DatensatzVersion, Hyperparameter)`,
  `BricheTrainingAb(AggregateId)`.
  → `MeldeTrainingBegonnen`/`MeldeFortschritt`/`MeldeTrainingAbgeschlossen`/`MeldeTrainingGescheitert`
  kommen vom **Python-Worker** (M8), nicht von der GUI.

### Value Objects (Command-/Response-Payloads)
- `Domain.Datensatz.RangeKriterien(Von?, Bis?, KiKlassifikation?, MenschLabel?, ProduktLabel?, NurKomplette?, HatKiKlassifikation?, HatMenschLabel?, NurNichtInspizierte?)`
  — spiegelt `ImagePairFilter`; **die GUI baut hieraus die Range** (dieselben Felder wie das Suchpanel).
- `Domain.Datensatz.SplitKonfig(TrainProzent, ValProzent, TestProzent, Seed)` — `SplitKonfig.Default` = 70/15/15/42.
- `Domain.Datensatz.Split { Train, Val, Test }`, `Domain.Datensatz.DatensatzStatus { Entwurf, Eingefroren }`.
- `Domain.Trainingslauf.Hyperparameter(Epochen, LernRate, BatchGroesse, Architektur, Seed)`,
  `EpochenMetrik(Epoche, Loss, Genauigkeit)`, `Endmetriken(Loss, Genauigkeit)`,
  `TrainingsStatus { Angefordert, Laeuft, Abgeschlossen, Gescheitert, Abgebrochen, Haengengeblieben }`.

### Queries + Responses (die die GUI STELLT — Read-Seite, Schema `rm`)
Alle in `Domain.Projections`, alle als **`IQuery`** deklariert und automatisch client-verdrahtet (§3.4 unten):
- `HoleDatensatz(DatensatzId)` → `DatensatzAntwort(Id, Name, Status, AnzahlMitglieder, EingefroreneVersion, Split, Ranges)` | `DatensatzNichtGefunden(DatensatzId)`.
- `HoleDatensatzSamples(DatensatzId, Version, Seite=1, SeitenGroesse=500)` → `DatensatzSamples(Samples, GesamtAnzahl, Seite, SeitenGroesse)` über `DatensatzSample(ImagePairId, Dc0Pfad, Dc2Pfad, Label, Split)`.
- `HoleTrainingslauf(TrainingslaufId)` → `TrainingslaufAntwort(Id, DatensatzId, DatensatzVersion, Status, AktuelleEpoche, GesamtEpochen, Hyperparameter, MetrikHistorie, ModellPfad, Endmetriken, Fehlergrund, Startzeit)` | `TrainingslaufNichtGefundenAntwort`.
- `HoleTrainingslaeufe()` → `TrainingslaufListe(Items)` (Liste von `TrainingslaufAntwort`).
- **Bestehend + wiederverwendbar:** `SucheImagePairs(ImagePairFilter)` → `ImagePairSuchergebnis`,
  `GetProduktionsTage()` → `ProduktionsTageAntwort` (der Produktionstage-Baum), `GetImagePairStatistik()` → `ImagePairStatistikAntwort`.

### Events (die die GUI als Fx.On-Auslöser NUTZT — Live-Refresh)
Persistente Events aus `Domain.Datensatz` / `Domain.Trainingslauf`, die per gRPC-Event-Push am Client
ankommen: `DatensatzErstellt`, `PaareAufgenommen`, `PaarAufgenommen`, `PaarEntfernt`, `SplitGesetzt`,
`DatensatzEingefroren`; `TrainingAngefordert`, `TrainingBegonnen`, **`TrainingFortschritt`** (die Live-Kurve!),
`TrainingAbgeschlossen`, `TrainingGescheitert`, `TrainingAbgebrochen`, `TrainingHaengengeblieben`.

### ⚠️ Eine bekannte Lücke, die M9 selbst schließen muss (kleiner Backend-Schritt)
**Es gibt noch KEINE Query, die ALLE Datensätze listet** (für die Sidebar). `HoleDatensatz(id)`
existiert (Einzel), `HoleTrainingslaeufe()` existiert (Liste). Für die **Datensatz-Sidebar** brauchst du:
`HoleDatensaetze()` → `DatensatzListe(Items)`. Der Read-Store hat die Methode schon
(`IDatensatzReadStore.GetAlleAsync()` in `Domain.Projections/IDatensatzStore.cs`) — du wrappst sie nur:
eine Query + Response + eine Reader-Methode in `DatensatzReader.cs` + **Proto-Regen** (Anhang A). Siehe §5.5.

---

## 1. Das Ziel von M9 (Konzept §8)

Vier neue GUI-Bausteine, **alle über die generierte Verdrahtung** — kein Handwiring:

| Slot | Modul | Inhalt (Konzept §8.1/§8.2) |
|---|---|---|
| `IStageModule` | **Datensatz-Komposition** | Transfer-/Korb-Layout: Suchergebnis links, Datensatz-Korb rechts. „ganze Range → Datensatz", live Größe/Klassenbalance/Split/Provenienz, „Einfrieren". |
| `IStageModule` | **Training-Dashboard** | „Neues Training" (Datensatz + Hyperparameter) + **Live-Kurven** (loss/accuracy) je Lauf über **ApexCharts**, gespeist vom Event-Stream. |
| `ISidebarModule` | **Datensatz-Liste** | Entwurf/Eingefroren, Größe, Version. |
| `ISidebarModule` | **Trainingslauf-Liste** | Status-Badges live. |
| `IHeadlessModule` | **Data/Refresh/Intent** | Stores + RefreshHandler für die neuen Read-Models, IntentHandler für „Starte Training"/„Friere ein". |

Nutzer-Flow (Konzept §9): Suchen → „ganze Range → Datensatz" (beliebig oft) → Balance/Größe ansehen →
„Einfrieren" → „Training starten" → Live-Kurve zusehen. **Drei Klicks bis zum trainierbaren Datensatz.**

---

## 2. Die Blazor-Client-Architektur — das mentale Modell, das du brauchst

**Projekt:** `Domain.Client.Modules.Blazor` (die produktive Frontend-Generation — die alten
`Domain.Client`/`Domain.Client.Ui.Blazor` sind TOT, nicht in der `.sln`). Host: `Host.Blazor`.

**Die goldene Regel: du deklarierst Typen, die Generatoren verdrahten ALLES.** Es gibt **kein
manuelles DI**, keine `services.AddScoped<MeinStore>()`, keine Bus-Subscriptions von Hand. Der
`WiringGenerator` + `ModuleRegistryGenerator` (in `Client.SourceGeneration`) scannen dein Projekt und
generieren `AddClientDomain_Domain_Client_Modules_Blazor()` + `AddModules_…()` (aufgerufen in
`Host.Blazor/Program.cs:47-48`). Belegt: die generierte `GeneratedWiring.g.cs` enthält
`services.AddScoped<…Store>()`, `AttachToBus(...)`, `RegisterQueries(...)` für JEDEN Store/Handler.

### 2.1 Die fünf Bausteine (mit Vorbild-Dateien zum 1:1-Abschauen)

1. **Modul** (`IUiModule`: `Id`, `Title`, `ComponentType`) → `IStageModule` / `ISidebarModule` / `IHeadlessModule`.
   Vorbild: `Statistik/StatistikModule.cs` (headless), `Paarliste/PaarlisteModule.cs` (sidebar),
   `Suche/SuchModule.cs` (sidebar). **Ein neues `IStageModule` wird automatisch ein Tab in der Shell**
   (`Host.Blazor/Shell/Shell.razor.cs:53` `AllModules.OfType<IStageModule>()`), ein `ISidebarModule`
   automatisch eine Seitenleiste. Nur die Klasse anlegen — fertig.

2. **Store** (`partial class X : StoreBase`) — der State + „Reducer". Vorbild: `Statistik/StatistikStore.cs`.
   - `[ObservableProperty] private int _feld;` (CommunityToolkit.Mvvm) → generierte Property `Feld`.
   - `void Handle(TResponse antwort, MessageContext ctx) { ... }` — **eine Handle-Methode je Server-Response/Event,
     das der Store konsumiert.** Der Generator subscribed sie automatisch auf dem Bus.
   - Für Listen/Collections: `Track(new PagedCollection<…>(r => r.Id))` im Konstruktor (siehe
     `Paarliste/Store.Paarliste.cs`), oder schlicht ein `List<…>` in einer `[ObservableProperty]`.
   - `Changed` feuert genau einmal pro Dispatch-Zyklus; die View rendert darauf.
   - Optional `: IHydrationStore` + `MarkHydrated()` wenn der Store zur Start-Hydration zählt.

3. **Effects** (razor, `IHeadlessModule.ComponentType`) — reagiert auf Events, stößt Queries an.
   Vorbild: `Statistik/StatistikEffects.razor`.
   ```razor
   @inject EffectScope Fx
   @implements IDisposable
   @code {
       protected override void OnInitialized() {
           Fx.On<DatensatzEingefroren>(_ => Fx.Dispatch(new HoleDatensatz(...)));
           Fx.On<TrainingFortschritt>(_ => Fx.Dispatch(new HoleTrainingslauf(...)));
       }
       public void Dispose() => Fx.Dispose();
   }
   ```
   `Fx.On<TEvent>(effect)` = Bus-Subscription; `Fx.Dispatch(msg)` = `Bus.Publish(msg)`.

4. **RefreshHandler / IntentHandler** (`partial class`, `Handle`-Methoden, die **yielden**) — die
   generierte Alternative zu Effects, wenn du mehrere Queries/Commands je Event brauchst. Vorbild:
   `Paarliste/PaarlisteRefreshHandler.cs`, `Labeling/LabelingIntentHandler.cs`.
   ```csharp
   public partial class DatensatzIntentHandler {
       private readonly DatensatzKompositionStore _store;
       public DatensatzIntentHandler(DatensatzKompositionStore store) => _store = store;
       IEnumerable<object> Handle(RangeHinzufuegenIntent evt, MessageContext ctx) {
           yield return new FuegeRangeHinzu(_store.AktuelleDatensatzId, _store.BaueRangeKriterien());
       }
   }
   ```
   `yield return <Command>` → landet auf dem Bus → `ConnectionModule` sendet ihn an den Server.
   `yield return <Query>` → `QueryBridge` stellt ihn. **Intents** sind client-lokale Records
   (`: IClientEvent`), die die View dispatcht (z. B. Button-Klick).

5. **Panel/View** (razor, `IStageModule`/`ISidebarModule.ComponentType`) — die sichtbare Fläche.
   Vorbild: `Suche/SuchPanel.razor`, `Paarliste/PaarlistenPanel.razor`.
   ```razor
   @inject DatensatzKompositionStore Store
   @inject EffectScope Fx
   @implements IDisposable
   @code {
       protected override void OnInitialized() => Fx.OnChanged(Store, () => InvokeAsync(StateHasChanged));
       public void Dispose() => Fx.Dispose();
       void EinKlick() => Fx.Dispatch(new RangeHinzufuegenIntent());   // Intent ODER direkt Command/Query
   }
   ```
   Der Store wird per `@inject` bezogen (der Generator hat ihn als `AddScoped` registriert).

### 2.2 Die Datenflüsse (auswendig)

**Query (Read):**
```
View/Effect: Fx.Dispatch(new HoleDatensatz(id))
   → ClientBus → QueryBridge (Register<HoleDatensatz, IQueryResponse> generiert) → gRPC → Server-Reader
   → DatensatzAntwort → ClientBus.Publish(antwort) → Store.Handle(DatensatzAntwort) → [ObservableProperty] → Changed → View rendert
```
Wichtig: die `QueryBridge` registriert **jede** `IQuery` automatisch (generiert:
`queryBridge.Register<{Query}, Abstractions.IQueryResponse>(bus)`). Der Response-Typ wird zur Laufzeit
aus dem Proto-oneof aufgelöst und per `type` gepublisht → jeder Store mit passender `Handle(...)` bekommt ihn.
**Du musst NICHTS registrieren** — Query in `Domain.Projections` deklariert (schon geschehen für M4/M6) +
Store mit `Handle(Response)` = fertig.

**Command (Write):**
```
View/Intent: Fx.Dispatch(new FriereEin(id))   (oder yield aus IntentHandler)
   → ClientBus → ConnectionModule (subscribed auf alle Command-Typen) → GrpcProxy.SendCommandAsync → Server
```
Fire-and-forget. Die Wirkung kommt als Event zurück (`DatensatzEingefroren`) → Fx.On → Query-Refresh.

**Live-Push (Server→Client):** durabler gRPC-Event-Stream liefert alle abonnierten Domain-Events;
`Fx.On<TrainingFortschritt>` feuert bei jedem → Dashboard-Refresh (die Live-Kurve „streamt von selbst").

---

## 3. Datei-Checkliste je Modul (Vorbilder in Klammern)

Lege je Modul einen Ordner unter `Domain.Client.Modules.Blazor/` an (z. B. `DatensatzKomposition/`,
`TrainingDashboard/`, `DatensatzListe/`, `TrainingslaufListe/`). **Namespace-Konvention beachten:**
Module liegen unter `Domain.Client.Modules.<Name>` (siehe die bestehenden — der Ordner heißt oft anders
als der Namespace; richte dich nach dem Namespace in den Vorbild-`.cs`).

### 3.1 Stage „Datensatz-Komposition" (`IStageModule`)
- `DatensatzKompositionModule.cs` (`IStageModule`, `ComponentType = typeof(DatensatzKompositionPanel)`) — (Statistik/Paarliste-Modul)
- `DatensatzKompositionStore.cs` (`StoreBase`): hält `AktuelleDatensatzId`, `Name`, `Status`, `AnzahlMitglieder`,
  `Ranges`, `SplitKonfig`, `EingefroreneVersion`; `Handle(DatensatzAntwort)`, `Handle(DatensatzSamples)` (für Balance nach Freeze).
  Baut `RangeKriterien` aus dem aktuellen Suchpanel-Filter (siehe §4).
- `DatensatzKompositionPanel.razor`: Transfer-Layout. **Links** = bestehendes Suchergebnis wiederverwenden
  (der `Suche`-Store + `SucheImagePairs`-Ergebnis; du kannst den `Suche/Store` injecten oder ein eigenes
  Suchergebnis führen). **Rechts** = Korb (Größe/Balance/Split/Provenienz + „Einfrieren"-Button).
  Buttons dispatchen Intents/Commands.
- `DatensatzKompositionEffects.razor` (`IHeadlessModule` ODER in den Stage-Panel-`OnInitialized`): 
  `Fx.On<DatensatzErstellt/PaareAufgenommen/PaarEntfernt/DatensatzEingefroren>(_ => Fx.Dispatch(new HoleDatensatz(id)))`.
- `DatensatzIntentHandler.cs` (`partial`, RefreshHandler-Stil): Intents → Commands
  (`ErstelleDatensatz`, `FuegeRangeHinzu`, `NimmPaarAuf`, `EntfernePaar`, `SetzeSplit`, `FriereEin`).

### 3.2 Stage „Training-Dashboard" (`IStageModule`)
- `TrainingDashboardModule.cs` (`IStageModule`)
- `TrainingDashboardStore.cs` (`StoreBase`): eine Map `TrainingslaufId → TrainingslaufAntwort` (via
  `Track`/`CreateMap`), `Handle(TrainingslaufAntwort)`, `Handle(TrainingslaufListe)`.
- `TrainingDashboardPanel.razor`: „Neues Training"-Formular (Datensatz-Auswahl + Hyperparameter-Felder →
  `StarteTraining`), darunter je laufendem/abgeschlossenem Lauf eine **ApexCharts**-Live-Kurve
  (loss + accuracy aus `MetrikHistorie`). Siehe §6 für ApexCharts.
- `TrainingDashboardEffects.razor`: `Fx.On<TrainingAngefordert/TrainingBegonnen/TrainingFortschritt/TrainingAbgeschlossen/TrainingGescheitert>(e => Fx.Dispatch(new HoleTrainingslauf(<id aus ctx/event>)))`.
  → jeder `TrainingFortschritt` triggert einen Re-Query → die Kurve wächst.
- `TrainingIntentHandler.cs`: `StarteTrainingIntent` → `StarteTraining(...)`; `BrichAbIntent` → `BricheTrainingAb(id)`.

### 3.3 Sidebar „Datensatz-Liste" (`ISidebarModule`)
- `DatensatzListeModule.cs` (`ISidebarModule`, `Side = SidebarSide.Left`)
- `DatensatzListeStore.cs` (`StoreBase`): `Handle(DatensatzListe)` → hält die Liste. **Braucht die neue
  Query `HoleDatensaetze()`** (§5.5).
- `DatensatzListePanel.razor`: Liste mit Name/Status-Badge/Größe/Version; Klick wählt den aktiven Datensatz
  (setzt ihn im Komposition-Store, z. B. via Intent).
- Refresh: `Fx.On<DatensatzErstellt/DatensatzEingefroren>(_ => Fx.Dispatch(new HoleDatensaetze()))`.

### 3.4 Sidebar „Trainingslauf-Liste" (`ISidebarModule`)
- `TrainingslaufListeModule.cs` (`ISidebarModule`)
- `TrainingslaufListeStore.cs`: `Handle(TrainingslaufListe)`.
- `TrainingslaufListePanel.razor`: Status-Badges live.
- Refresh: `Fx.On<TrainingAngefordert/TrainingBegonnen/TrainingAbgeschlossen/TrainingGescheitert>(_ => Fx.Dispatch(new HoleTrainingslaeufe()))`.

### 3.5 Headless-Bündel
Effects + Data-Stores können auch in einem `IHeadlessModule` (wie `Statistik`) gebündelt werden, statt in
jedem Stage/Panel. Wähle, was übersichtlicher ist — beide Wege sind idiomatisch.

---

## 4. Wiederverwendung: das Suchpanel + Produktionstage-Baum

Die Komposition-Stage soll **nicht** ein zweites Suchpanel erfinden. Das bestehende
`Suche/SuchPanel.razor` + `Suche/ProduktionstageBaum.razor` + `Data/SuchKriterien.cs` sind genau der
Filter, den die Range braucht (`SuchKriterien` ≈ `RangeKriterien`, feldgleich bis auf Paginierung).

Zwei gangbare Wege:
1. **`SucheGeaendert`-Event mitlesen:** der `Suche`-Store publisht `SucheGeaendert(SuchKriterien)`
   (`Data/SuchKriterien.cs`). Dein Komposition-Store subscribed es (`Handle(SucheGeaendert)`), hält die
   aktuellen Kriterien und mappt sie beim „Range → Datensatz"-Klick 1:1 auf `RangeKriterien` →
   `FuegeRangeHinzu`. Das Suchergebnis (`ImagePairSuchergebnis`) zeigt die linke Spalte.
2. **Eigenes schlankes Suchfeld** in der Stage, das direkt `RangeKriterien` baut. Mehr Kontrolle, mehr Code.

Empfehlung: **Weg 1** — maximale Wiederverwendung, „der Filter links ist der vorhandene Baustein" (Konzept §8.1).
`GetProduktionsTage()` → `ProduktionsTageAntwort` liefert den Baum (die natürliche Zeit-Range).

---

## 5. Backend-Ergänzungen, die M9 selbst braucht (klein, aber Pflicht)

### 5.5 `HoleDatensaetze()` — die Datensatz-Listen-Query (für die Sidebar)
Der Read-Store hat `GetAlleAsync()` schon. Zu bauen (Vorbild: die Trainingslauf-Liste aus M6b —
`HoleTrainingslaeufe()`/`TrainingslaufListe`/`TrainingslaufReader.Handle`):
1. `Domain.Projections/DatensatzQueries.cs`: `public record HoleDatensaetze() : IQuery;`
2. `Domain.Projections/DatensatzResponses.cs`: `public record DatensatzListe(IReadOnlyList<DatensatzAntwort> Items) : IQueryResponse;`
3. `Domain.Projections/DatensatzReader.cs`: `Handle(HoleDatensaetze q, …)` → `_store.GetAlleAsync()` → auf `DatensatzAntwort` mappen (der `ToAntwort`-Helper existiert dort schon nicht — nutze das Muster aus `Handle(HoleDatensatz)`).
4. **Proto-Regen** (Anhang A): `dotnet run --project Proto.SourceGeneration` → `ProtoRepo` bauen → `Infrastructure` bauen. Queries/Responses laufen über den gRPC-QueryResponse-DTO-Pfad → **keine** Hand-Kontext-Änderung nötig (bewiesen in M4/M6b).

### 5.6 (Optional, empfohlen) Live-Klassenbalance in `DatensatzAntwort`
Konzept §8.1 zeigt links/rechts eine **live** Klassenbalance des Entwurfs. `DatensatzAntwort` trägt sie
noch nicht (bewusst in M4 ausgelassen — sie ist dynamisch, Join gegen die ImagePair-Labels). Wenn die
Balance im Entwurf gezeigt werden soll:
- `DatensatzReader` zusätzlich `IImagePairReadStore` injizieren, in `Handle(HoleDatensatz)` über die
  Draft-Mitglieder (`DatensatzReadModel.Mitglieder`) die Labels ziehen, Balance/Menschlabel-Quote rechnen,
  in ein erweitertes `DatensatzAntwort` legen. Proto-Regen.
- **Alternative (weniger Backend):** Balance erst nach dem Einfrieren aus `HoleDatensatzSamples` rechnen
  (die tragen `Label`+`Split`). Für den Entwurf dann nur `AnzahlMitglieder` zeigen. Für einen ersten M9-Wurf reicht das.

Entscheide pragmatisch; die Alternative hält M9 frontend-lastig.

---

## 6. ApexCharts (Live-Kurven) — schon verfügbar

`Host.Blazor.csproj` referenziert **`Blazor-ApexCharts` 6.1.0**. Für die loss/accuracy-Kurve je Lauf:
- Datenquelle: `TrainingslaufAntwort.MetrikHistorie` (Liste `EpochenMetrik(Epoche, Loss, Genauigkeit)`),
  gefüllt durch die `TrainingFortschritt`-getriebenen Re-Queries.
- Zwei Serien (loss, accuracy) über `Epoche` als X-Achse. Bei jedem `TrainingFortschritt`-Event wächst die
  `MetrikHistorie` → Store `Changed` → `StateHasChanged` → ApexCharts rendert den neuen Punkt.
- Prüfe die genaue `Blazor-ApexCharts`-API (v6) im NuGet-Cache/Doku; grundsätzlich `<ApexChart>` +
  `<ApexPointSeries>` mit `Items` gebunden an die Metrik-Liste. `_Imports.razor` ggf. um
  `@using ApexCharts` ergänzen, und im Host das JS/CSS registrieren (die bestehende Chart-Nutzung im
  Projekt prüfen: `Domain.Client.Modules.Blazor/Chart/` zeigt, wie Charts hier schon gemacht werden —
  **erst dort schauen**, ob ApexCharts oder eigenes SVG genutzt wird, und konsistent bleiben).

---

## 7. Konventionen & Fallstricke (verbindlich)

1. **Kein Handwiring.** Nur Typen deklarieren (Module/Stores/Handler/Effects/Panels). Der Build verdrahtet.
   Wenn ein Store nicht reagiert: hat er eine `Handle(TResponse, MessageContext)`-Methode mit **exakt** dem
   Response-Typ, den der Reader zurückgibt? Ist die Klasse `partial`?
2. **Deutsch, Umlaut-Transliteration in Identifiern** (ae/oe/ue): `FriereEin`, `Haengengeblieben`,
   `DatensatzKomposition`. Umlaute nur in Kommentaren/UI-Strings.
3. **Enum-Wire-Fallstrick (Anhang B des Umsetzungs-Docs):** proto3 lässt Default-Enumwert 0 weg; er
   round-trippt symmetrisch zurück (unkritisch). Aber: verlasse dich in der GUI nicht auf „0 == nicht gesetzt".
4. **Nullable Komplex-VO (`T?`) in Wire-Messages ist kaputt** (Proto-Generator bildet es als `string` ab).
   Falls du eine neue Response/Query mit einem optionalen VO-Feld brauchst: nicht-nullable machen +
   Fallback (siehe `TrainingslaufAntwort.Hyperparameter/Endmetriken`). Betrifft nur NEUE Server-Typen.
5. **Commits ohne `Co-Authored-By`-Trailer** (Projekt-Regel). **Nur auf den zugewiesenen Branch pushen.**
6. **Nach jedem Modul: `dotnet build CqrsSolution.sln -c Release` grün + Prüfstand grün halten**
   (`dotnet test Infrastructure.Pruefstand.Tests/…` — aktuell **219/219**). Frontend-Module brechen den
   Prüfstand nicht, aber ein Proto-Regen (§5.5) berührt die generierte Kette — immer neu bauen.
7. **Host.Blazor braucht die net9-Runtime** (Anhang A.3 des Umsetzungs-Docs) — unter net10-Roll-Forward
   liefert `blazor.server.js` 404. Für die Live-Ansicht die echte net9-Runtime nutzen.

---

## 8. Bauen / Testen / Laufen

```bash
# Nach Proto-Regen (§5.5) IMMER:
dotnet run --project Proto.SourceGeneration
dotnet build ProtoRepo/ProtoRepo.csproj
dotnet build Infrastructure/Infrastructure.csproj

# Volle Solution + Prüfstand (muss grün bleiben):
dotnet build CqrsSolution.sln -c Release
dotnet test Infrastructure.Pruefstand.Tests/Infrastructure.Pruefstand.Tests.csproj -c Release

# Frontend prüfen: baut das Modul-Projekt + Host?
dotnet build Domain.Client.Modules.Blazor/Domain.Client.Modules.Blazor.csproj -c Release
dotnet build Host.Blazor/Host.Blazor.csproj -c Release
```

**Generierte Verdrahtung inspizieren** (wenn ein Store/Query nicht zieht — der Generator schreibt normal
nicht auf Disk, memory „Generator-Output nicht auf Disk"):
```bash
dotnet build Domain.Client.Modules.Blazor/Domain.Client.Modules.Blazor.csproj -c Debug /p:EmitCompilerGeneratedFiles=true
# dann obj/Debug/**/Client.SourceGeneration.WiringGenerator/GeneratedWiring.g.cs lesen:
#   - services.AddScoped<DeinStore>()  vorhanden?
#   - RegisterQueries: deine Query drin?
#   - AttachToBus/SubscribeAll: dein Store gelistet?
# obj/Debug/**/ModuleRegistryGenerator/… : dein IUiModule registriert?
```

---

## 9. Akzeptanz & Verifikation OHNE Infra

**Achtung:** ein echtes End-to-End (Datensatz komponieren → einfrieren → trainieren → Live-Kurve) braucht
laufende Infra (Postgres/Consul/Redis) + den Host + idealerweise den Python-Worker. In der Sandbox war
**keine Infra/kein Docker** verfügbar (Anhang A des Umsetzungs-Docs beschreibt den Linux-Setup-Weg). Was du
ohne Infra sicher zeigen kannst:

- **Baut alles grün** (Solution + Host.Blazor + Modul-Projekt), Prüfstand bleibt 219/219.
- **Generatoren greifen** (GeneratedWiring listet die neuen Stores/Queries/Module — via EmitCompilerGeneratedFiles).
- **Optional, stark:** ein `SimHost`-artiger oder Bunit-Rendertest je Panel (prüfe, ob das Projekt schon
  Komponententests hat — `Client.Infrastructure.CollectionTests` existiert; ein Frontend-Render-Test wäre neu).
- Mit Infra (falls verfügbar): der Flow aus Konzept §9, plus Anhang A.6 „Testdaten säen" (gRPC-Client-Seeder),
  damit die Suche echte Bildpaare zeigt.

**Definition of Done M9:** die vier Module existieren, sind generiert-verdrahtet (Tab/Sidebar erscheinen),
`HoleDatensaetze()` ergänzt + Proto grün, Solution + Prüfstand grün. Live-E2E ist Infra-abhängig und darf als
„verdrahtet, aber hier nicht end-to-end gefahren" berichtet werden — ehrlich kennzeichnen.

---

## 10. Karte: wo alles liegt (Schnellreferenz)

| Was | Wo |
|---|---|
| Modul-Contracts | `Client.Infrastructure/Abstractions/{IUiModule,IStageModule,ISidebarModule,IHeadlessModule}.cs` |
| Store-Basis, Bus, Effects | `Client.Infrastructure/Abstractions/{StoreBase,IBus,EffectScope,Markers}.cs` |
| Query-Brücke | `Client.Infrastructure/Connection/QueryBridge.cs` (Register pro IQuery, generiert) |
| Command-Versand | `Client.Infrastructure/Connection/{ConnectionModule,GrpcProxy}.cs` |
| Generatoren | `Client.SourceGeneration/{WiringGenerator,ModuleRegistryGenerator,HandleMethodGenerator,ViewModelGenerator}.cs` |
| Host-Wiring-Aufruf | `Host.Blazor/Program.cs:47-48`, Shell-Stages `Host.Blazor/Shell/Shell.razor.cs:53` |
| Vorbild headless+store+effects | `Domain.Client.Modules.Blazor/Statistik/` |
| Vorbild sidebar+panel+refresh+paginierte Liste | `Domain.Client.Modules.Blazor/Paarliste/` |
| Vorbild Suchpanel+Produktionstage-Baum+Kriterien | `Domain.Client.Modules.Blazor/{Suche/, Data/SuchKriterien.cs}` |
| Vorbild Intent→Command | `Domain.Client.Modules.Blazor/Labeling/LabelingIntentHandler.cs` |
| Vorbild Chart (erst hier schauen!) | `Domain.Client.Modules.Blazor/Chart/` |
| Datensatz-Aggregat + Commands/Events/VOs | `Domain/Datensatz/` |
| Trainingslauf-Aggregat + Commands/Events/VOs | `Domain/Trainingslauf/` |
| Datensatz Read-Seite (Query/Response/Reader/Store) | `Domain.Projections/Datensatz*.cs`, `Domain.Infrastructure/DatensatzStore*.cs` |
| Trainingslauf Read-Seite | `Domain.Projections/Trainingslauf*.cs`, `Domain.Infrastructure/TrainingslaufStore*.cs` |
| Resolver (Range/Freeze — server-seitig, nicht anfassen) | `Domain.Pipeline/Datensatz/DatensatzResolverPipeline.cs` |
| Frist-Timeout (server-seitig) | `Domain.Pipeline/Trainingslauf/TrainingFristPipeline.cs` |
| Python-Query-Parität + Worker (Referenz, nicht M9) | `Client.Infrastructure.Python/`, `Domain.Client.Worker.Python.ML/` |
| Proto-Regen | Anhang A.4 im Umsetzungs-Doc; `Proto.SourceGeneration` → `ProtoRepo` → `Infrastructure` |

---

**Kurzfassung für den Einstieg:** Bau zuerst `HoleDatensaetze()` (§5.5, inkl. Proto-Regen, Solution grün).
Dann Modul für Modul: **Datensatz-Liste-Sidebar** (kleinster, beweist den ganzen Query→Store→View-Kreis),
dann **Trainingslauf-Liste-Sidebar**, dann **Datensatz-Komposition-Stage** (mit Suchpanel-Wiederverwendung),
zuletzt **Training-Dashboard-Stage** (ApexCharts-Live-Kurve). Nach jedem Modul: bauen + Prüfstand grün +
commit (ohne Trailer) + push. Kein Handwiring — nur Typen deklarieren.
