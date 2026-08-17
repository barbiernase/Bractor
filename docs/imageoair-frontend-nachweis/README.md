# imageoair-Frontend — Verstehen, Bauen, Laufen-Lassen (Nachweis)

> **Stand: 2026-08-17.** Dieser Bericht dokumentiert das **Verstehen**, **Bauen** und
> **nachweisliche Laufen-Lassen** des Frontends der **ImagePair-Domäne** (umgangssprachlich
> „imageoair") — Domäne *und* Architektur. Alle Zahlen und Screenshots stammen aus einem
> tatsächlich hochgefahrenen Voll-Stack-Lauf (Postgres + Redis + Consul + `Host.Grpc` +
> `Host.Blazor`), kein Mock.

„imageoair" ist die lautliche Schreibweise der **`ImagePair`**-Domäne (`Domain/ImagePair`,
`Domain.Client.Modules.Blazor`, `Host.Blazor`).

---

## 1. Was die Domäne fachlich ist

Die ImagePair-Domäne modelliert die **industrielle Bildpaar-Inspektion**: pro produziertem Teil
entstehen **zwei Kamerabilder** (`DC0` und `DC2` — zwei Aufnahme-Modi). Beide zusammen sind *ein*
**Bildpaar** (`ImagePair`). Auf drei parallelen Strängen wird bewertet, ob eine **Anomalie**
vorliegt:

| Strang | Akteur | Was passiert |
|---|---|---|
| **1 — KI** | Maschine | klassifiziert Einzelbilder + das Paar (`KeineAnomalie` / `Questionable` / `Anomalie`), inkl. 8 Regionen je Bild |
| **2 — Mensch (Bild)** | Labeler | labelt Regionen, Einzelbilder und das Paar (Mensch-Slot **neben** dem KI-Slot) |
| **3 — Mensch (Produkt)** | Labeler | labelt das *physische* Produkt |

Dazu die **Inspektion** (hat ein Mensch das Paar betrachtet?) und der Lebenszyklus
(`erstellt` → je Bild `verfügbar` → `komplett`).

**Die fachliche Identität** ist der `PairKey` = die ersten 7 Segmente des Dateinamens
`YYYY_MM_DD_HH_mm_ss_mmm_VERSION.ext` (z. B. `2025_06_16_17_46_20_293_DC2.tiff`). Aus dem
PairKey wird per MD5 eine **deterministische Aggregate-Guid** erzeugt — zwei Dateien mit gleichem
PairKey (aber `DC0`/`DC2`) gehören garantiert zum selben Aggregat (`Domain/ImagePair/ImagePairName.cs`).

**Bausteine** (`Domain/ImagePair/`):
- **Commands** (`Commands.cs`): `ErstelleImagePair`, `MeldeBildVerfuegbar`,
  `KlassifiziereEinzelBildDurchKi`, `KlassifiziereBildPaarDurchKi`, `LabelBildRegion`,
  `LabelEinzelBild`, `LabelBildPaar`, `LabelPhysischesProdukt`, `MarkiereAlsInspiziert`.
- **Events** (`Events.cs`): die Erfolgs-Events (`ImagePairErstellt`, `BildVerfuegbar`,
  `ImagePairKomplett`, …) plus **Ablehnungs-Events** (`ITransientEvent`, z. B.
  `PaarNichtKomplett`, `BildNichtVerfuegbar`).
- **Decider** (`Decider.cs`): reine Entscheidungslogik `Command × State → Events`. Prüft
  Invarianten (z. B. `KlassifiziereBildPaarDurchKi` verlangt `IstKomplett`, sonst
  `PaarNichtKomplett`; `MarkiereAlsInspiziert` ist **idempotent**).
- **Applier** (`Applier.cs`): `Event × State → State` (Fold). Rein, ohne I/O.
- **State/Value Objects** (`ImagePair.cs`, `ValueObjects.cs`, `Enums.cs`): `BildInfo`,
  `BildBewertung` (zwei Slots: `KiKlassifikation` + `MenschLabel`), 8 × `RegionBewertung`.

Fachcode bleibt rein (Invariante 5): Cursor, Signal, Exactly-once, Sharding tauchen hier nirgends auf.

---

## 2. Die Architektur des Frontends

Ein **Blazor-Server**-Client (MudBlazor) auf einem selbstgebauten, **generierten,
reflexionsfreien Redux/Flux-Bus-/Store-Stack**, angebunden per **bidirektionalem gRPC-Streaming**
an `Host.Grpc`. Details: [`docs/08-frontend-blazor-client.md`](../08-frontend-blazor-client.md).

```
Browser ──WebSocket(Circuit)──> Host.Blazor ──gRPC(HTTP/2, bidi)──> Host.Grpc ──> Marten/Postgres
   ▲            (MudBlazor)         │  ClientBus / Store / Generated Wiring         (Event-Store)
   └── Shell (Slot-Module) ◀────────┘                                              Redis / Consul
```

**Modulares Slot-System** (`Domain.Client.Modules.Blazor/`, 13 Module): Die `Shell.razor` löst
`IEnumerable<IUiModule>` per DI auf und rendert jedes Modul über `<DynamicComponent>` in seinen
Slot:

| Slot-Interface | Module (Beispiele) |
|---|---|
| `IHeaderModule` | `StatusBar` |
| `ISidebarModule` | `Paarliste`, `Historie`, `Suche` |
| `IStageModule` | `Bilder`, `Chart`, `Heatmap` |
| `IFooterModule` | `Navigation`, `Labeling` |
| `IHeadlessModule` | `Data`, `Feedback`, `Statistik` |

**Der Datenfluss (unidirektional, Redux/Flux):**
- **Store** (`StoreBase`, `ObservableObject`): `Handle(TEvent, …)` = Reducer; ein gebatchtes
  `Changed`-Signal.
- **Bus** (`ClientBus`): typ-geroutetes Dispatch, transaktionsgebatcht, Zyklus-Guard.
- **Transport** (`GrpcProxy` + `ConnectionModule` + `QueryBridge` + `VersioningModule`):
  Command = fire-and-forget, Query = request/response (Korrelation), Event = Server-Push.
- **Generatoren** (`Client.SourceGeneration`): `AddClientDomain_…` / `AddModules_…`,
  `SubscribeAll`, `CommandTypes`, `HydrationStores` — **kein Reflection, alles zur Compile-Zeit**.
- **Bootstrap-Gate** (`ClientStartupService`): `Connecting → Hydrating → Ready`. Die Shell
  mountet Module **erst** bei `Ready` (alle `IHydrationStore` befüllt) → schließt das
  „leere-Liste-beim-Start"-Rennen konstruktiv aus.

**Read-Seite** (`Domain.Projections` + `Domain.Infrastructure`): Die `ImagePairProjection` (ein
durabler Pull-Subscriber mit Co-Commit) materialisiert `ImagePairReadModel` als Marten-JSONB im
**eigenen Schema `rm`** (`ImagePairStorePostgres`). Die `SucheImagePairs`-/Chart-/Statistik-Queries
lesen daraus.

---

## 3. Build

Ziel-Framework der Kette ist **`net9.0`**. In dieser Umgebung war **kein** .NET-SDK vorhanden und
die offiziellen .NET-Download-Hosts sind per Egress-Policy gesperrt (403). Gelöst über das
**.NET-10-SDK aus dem Ubuntu-Paketfeed** (`apt`), das `net9.0`-Projekte baut (die 9.0-Ref-Packs
kommen von `nuget.org`, erreichbar).

```bash
dotnet build Host.Blazor/Host.Grpc  -c Release
# Ergebnis: 0 Fehler (Warnungen vorbestehend: NU1904 Marten, CS86xx Nullable, generierter Code)
```

**Ergebnis: `Host.Blazor` und `Host.Grpc` bauen mit 0 Fehlern.**

---

## 4. Laufen-Lassen (Voll-Stack)

Die Infrastruktur-Images (Docker Hub) sind per Policy gesperrt — daher **nativ** hochgezogen:
**PostgreSQL 16** + **Redis 7** (`apt`), **Consul 1.19.2** (Binary von `releases.hashicorp.com`).
`Host.Grpc` (Cluster-Member) und `Host.Blazor` laufen nativ.

**Wichtig — Laufzeit:** Eine `net9.0`-App braucht die **net9-Runtime**. Diese ist per `apt` nicht
verfügbar (nur 8.0/10.0). Für `Host.Grpc` reicht **Roll-Forward** auf die net10-Runtime. Für
`Host.Blazor` **nicht**: `_framework/blazor.server.js` (Static Web Asset des *Shared Framework*)
lieferte unter net10 **404** → der Circuit startet nie, die Seite hängt auf „Verbinde…". Fix: die
echte **net9-Shared-Framework-Runtime** (`Microsoft.NETCore.App.Runtime` +
`Microsoft.AspNetCore.App.Runtime` **9.0.19**) aus den **NuGet-Runtime-Packs** installiert →
`blazor.server.js` = **200**, Circuit startet, UI wird `Ready`.

### Datenerzeugung — über den echten Client-Weg

Um Daten zu erzeugen, wurde ein kleiner **Seeder** gebaut, der **exakt den Frontend-Transportweg
nutzt** (dieselbe DI wie `Host.Blazor/Program.cs`: `AddClientDomain_…` + `GrpcProxy` +
`ConnectionModule`). Er publiziert echte `ICommand`s auf den `ClientBus` → `CommandEnvelope` →
gRPC → Decider → Event → Store → Projektion. **Kein DB-Backdoor.** (Quelle: Anhang B.)

### Belege aus dem laufenden Event-Store (echtes Postgres)

```
es.mt_streams                      : 13
es.mt_events                       : 106
rm.mt_doc_imagepairreadmodel       : 12   ← Paarliste-Read-Model
rm.mt_doc_imagepairhistoriereadmodel: 12  ← Historie-Read-Model

Event-Typen:
  image_pair_erstellt          12      reaktion_gewirkt            12
  bild_verfuegbar              12      physisches_produkt_gelabelt  4
  image_pair_inspiziert         4      kommando_verarbeitet        44
```

Das ist der **vollständige CQRS/ES-Pfad** end-to-end: 12 Aggregate über gRPC-Commands angelegt,
Events persistiert, die durable Projektion hat 12 Read-Model-Dokumente materialisiert, die das
Frontend anzeigt. Die `reaktion_gewirkt`-Events belegen zusätzlich die **Reaktions-Maschine**
(`ImagePairReaktion` reagiert auf jede Erstellung).

### Nachweis-Screenshots (`bilder/`)

| Bild | Zeigt |
|---|---|
| [`01-uebersicht.png`](bilder/01-uebersicht.png) | Voll-UI im `Ready`-Zustand: StatusBar „Live \| 1/12", **Paarliste mit 12 Bildpaaren** (farbige Status-Punkte = Produkt-Label), Bilder/Chart-Bühne, **Historie mit echtem Event-Verlauf** („ImagePair erstellt" → „Dc2 verfügbar"), Suche mit **Produktionstage-Baum** (2025 → Juni = 12) + Filter, Labeling-Hotkeys in der Fußzeile. |
| [`02-paar-ausgewaehlt.png`](bilder/02-paar-ausgewaehlt.png) | Paar-Auswahl (2/12), Bühne „DC0 — kein Bild" (siehe Befund unten). |
| [`03-chart.png`](bilder/03-chart.png) | `CHART`-Tab: Produktionsstrip-Visualisierung, „Tag 12.06.2025 — 1 Punkte". |

---

## 5. Befunde am Rande (ehrlich dokumentiert)

1. **net9-Runtime nötig für Host.Blazor.** Roll-Forward auf net10 bricht die Static-Web-Assets
   (`blazor.server.js` 404). Für einen echten Betrieb die net9-Runtime installieren (siehe §4).
2. **`DC0` (Enum-Wert 0) wurde beim Seeden konsistent verworfen, `DC2` (Wert 1) stets akzeptiert**
   — unabhängig von Reihenfolge/Timing. Das passt zur bekannten Schuld „`DtoMapperGenerator`
   fragil (hartkodierte Enums)" (CLAUDE.md) und verdient einen genaueren Blick auf die
   Proto-/DTO-Abbildung des **Default-Enumwerts 0** über die Wire. Sichtbare Folge: „DC0 — kein
   Bild", Paare bleiben `nicht komplett` (deshalb keine KI-/Mensch-Paarlabels im Datensatz).
3. **Client-seitige OCC-Verfolgung ist rennanfällig ohne SynchronizationContext.** Im Seeder (kein
   Circuit) musste die `ExpectedVersion` aktiv nachgehalten/wiederholt werden; im laufenden
   Frontend erzeugt das View-getriebene `MarkiereAlsInspiziert` sichtbare
   „Concurrency conflict"-Toasts (`expected version N, actual M`). Deckt sich mit der Doku-Schuld
   „Bus/Store/Transport untestet, Reconnect rudimentär" (Doku 8.8).

Keiner dieser Punkte ist ein Backend-Defekt — das Backend erzwingt OCC/Invarianten korrekt; die
Punkte betreffen die Wire-Enum-Abbildung bzw. die Client-Härtung.

---

## Anhang A — Reproduktion (Kurzfassung)

```bash
# 1) SDK (net9 baubar mit SDK 10)
sudo apt-get install -y dotnet-sdk-10.0
# 2) net9-Runtime für Host.Blazor (aus NuGet-Runtime-Packs 9.0.19 ins Shared-Framework)
#    Microsoft.NETCore.App.Runtime.linux-x64 + Microsoft.AspNetCore.App.Runtime.linux-x64
# 3) Infra nativ: postgres-16 + redis (apt), consul 1.19.2 (Binary)
#    DB cqrs_events, User postgres/postgres; consul agent -dev
# 4) Backend (Roll-Forward ok):
DOTNET_ROLL_FORWARD=LatestMajor dotnet Host.Grpc/bin/Release/net9.0/Host.Grpc.dll
# 5) Frontend (native net9, publiziert):
dotnet publish Host.Blazor -c Release -o pub && cd pub && dotnet Host.Blazor.dll
#    → http://localhost:5010  (Blazor)   http://localhost:5001 (gRPC)
```

## Anhang B — Seeder (echter Client-Command-Weg)

Der Seeder spiegelt `Host.Blazor/Program.cs` (ohne Blazor), ruft `ClientStartupService.StartAsync`
(verbindet per gRPC) und publiziert `ErstelleImagePair` / `MeldeBildVerfuegbar` /
`KlassifiziereBildPaarDurchKi` / `LabelPhysischesProdukt` / `MarkiereAlsInspiziert` auf den
`ClientBus`. Damit läuft die Datenerzeugung über **genau denselben** Weg wie ein Klick im
Frontend (Command → Envelope → gRPC → Decider → Event → Projektion). Er ist bewusst **nicht** Teil
der Lösung eingecheckt (Diagnose-Werkzeug mit festen Pfaden), sondern hier als Beleg für die
Vorgehensweise beschrieben.
