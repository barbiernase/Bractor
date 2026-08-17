# Umsetzung & Handoff — Datensätze & Training

> **Zweck:** Handoff für einen frischen Agenten, der das Konzept aus
> [`konzept-training-und-datensatz.md`](konzept-training-und-datensatz.md) **implementiert**.
> Das *Warum* und die Design-Entscheidungen stehen dort; hier steht das *Wie*: Build-Reihenfolge,
> Datei-Checklisten, Umgebungs-Setup (mühsam erarbeitet) und Akzeptanzkriterien.
>
> **Basis-Branch:** `claude/imageoair-frontend-setup-0g0s4b` (hier liegen Konzept + dieses Doc).
> Von diesem Branch abzweigen, sonst fehlen die Konzeptdokumente.

---

## 0. Start-Prompt (zum Kopieren)

```
Implementiere das Konzept aus docs/konzept-training-und-datensatz.md (Trainings-/Datensatz-
Kontext für das ImagePair-System). Lies zuerst BEIDE Dokumente:
  - docs/konzept-training-und-datensatz.md            (Design, das Warum)
  - docs/konzept-training-und-datensatz-umsetzung.md  (Build-Reihenfolge, Umgebung, Akzeptanz)
sowie CLAUDE.md (Konventionen) und docs/09-python-sdk.md.

Umgebung zuerst herrichten (Anhang A im Umsetzungs-Doc) und `dotnet build CqrsSolution.sln`
grün sehen, BEVOR du Code schreibst.

Arbeite Phase 1 in der vorgegebenen Reihenfolge ab und beginne mit MEILENSTEIN 1:
das Aggregat Domain/Datensatz (Commands/Events/ValueObjects/State/Decider/Applier), streng
im Stil von Domain/ImagePair. Nach jedem Aggregat: bauen + Prüfstand-Test grün. Neue
Commands/Events/Queries brauchen Proto-DTOs (Proto-Regen, Anhang A.4) — sonst bricht der
DtoMapperGenerator. Breche bestehende Tests nicht.

Konventionen: Deutsch (Domäne/Kommentare), Verträge → Abstractions, Marten/Infra →
Infrastructure/Domain.Infrastructure, keine Runtime-Reflection, kein InMemoryEventStore.

Commit + push nur auf den dir zugewiesenen Branch. Halte nach MEILENSTEIN 1 an und
berichte, bevor du mit dem Trainingslauf weitermachst.
```

---

## 1. Build-Reihenfolge (Phase 1)

Jeder Meilenstein ist für sich baubar + testbar. **Nach jedem Schritt: `dotnet build` + Prüfstand grün.**

| # | Meilenstein | Ergebnis |
|---|---|---|
| **1** | **`Domain/Datensatz`** (reines Aggregat) | Commands/Events/Decider/Applier, store-frei testbar |
| **2** | **Proto-DTOs** für M1 + Regen (Anhang A.4) | Infrastructure baut mit den neuen Typen |
| **3** | **Read-Seite Datensatz** (`Domain.Projections` + `Domain.Infrastructure`, Schema `rm`) | `DatensatzReadModel` + `DatensatzSampleReadModel` + Projektion + Store |
| **4** | **Query `HoleDatensatzSamples`** + Reader (paginiert) | Beide Clients können die Samples abfragen |
| **5** | **Server-Resolver** (Range-Auflösung + Freeze-Snapshot) | `FügeRangeHinzu`/`FriereEin` werden serverseitig aufgelöst |
| **6** | **`Domain/Trainingslauf`** + Proto + Read-Seite | Trainings-Lebenszyklus event-sourced |
| **7** | **Python: `query()` + `extract_query_response`** (`cqrs_client`) | Query-Parität hergestellt (Konzept §7) |
| **8** | **Python `TrainingWorker`** | Event→Fortschritts-Commands, Samples per `query()` |
| **9** | **Blazor-Module** (Komposition-Stage, Dashboard, Sidebars, Headless) | GUI über generierte Verdrahtung — **eigener Handoff: [`konzept-training-und-datensatz-m9-blazor-handoff.md`](konzept-training-und-datensatz-m9-blazor-handoff.md)** |
| **10** | **`Domain/Modell`** + Inferenz-Kopplung (Phase 2) | Kreis geschlossen |

**Empfohlener erster Agenten-Lauf: Meilenstein 1** (sauber abgegrenzt, rein, schnell grün).

> **Stand:** M1–M8 sind auf Branch `claude/imageoair-frontend-setup-0g0s4b` gebaut & grün
> (Prüfstand 219/219, Python-Suites grün). **M9 als Nächstes** — der Blazor-Handoff oben hebt einen
> frischen Agenten auf den vollen Wissensstand (Architektur, Server-Contract, Datei-Checklisten,
> Fallstricke, die eine nötige Backend-Ergänzung `HoleDatensaetze()`).

---

## 2. Datei-Checkliste je Aggregat (Vorbild: `Domain/ImagePair/`)

Für `Domain/Datensatz/` (und analog `Domain/Trainingslauf/`):

- `Commands.cs` — `record … : ICommand` / `ICreationCommand`
- `Events.cs` — Erfolgs-Events (`IEvent`) **und** Ablehnungen (`ITransientEvent`)
- `ValueObjects.cs` — `DatensatzMitglied`, `RangeHerkunft`, `SplitKonfig`, …
- `Enums.cs` — falls nötig (z. B. `Split { Train, Val, Test }`, `DatensatzStatus`)
- `Datensatz.cs` — `partial class Datensatz : IState` (State-Properties + Helfer)
- `Decider.cs` — `partial class Decider : IDecider<Datensatz>`, je Command eine `Decide`-Methode
  mit `IEnumerable<OneOf<…>>` (Invarianten prüfen, siehe Konzept §3.4)
- `Applier.cs` — `partial class Applier : IApplier<Datensatz>`, je Event ein `Apply`

Read-Seite (Vorbild: `Domain.Projections/ImagePair*.cs` + `Domain.Infrastructure/ImagePairStore*.cs`):

- `Domain.Projections/DatensatzReadModel.cs`, `DatensatzSampleReadModel.cs` (`: IReadModel`)
- `Domain.Projections/DatensatzProjektion.cs` (`: ISubscriber, IPullSubscriber, IAppendProjektion`)
- `Domain.Projections/DatensatzQueries.cs` (`: IQuery`), `DatensatzResponses.cs` (`: IQueryResponse`)
- `Domain.Projections/DatensatzReader.cs` + `IDatensatzStore.cs`
- `Domain.Infrastructure/DatensatzStorePostgres.cs` (Read+Write, Schema `rm`) + Co-Commit-Store
  (Vorbild `ImagePairStore.cs` mit `ICoCommitTracker`) + Registrierung in `DomainServiceExtension.cs`

Server-Resolver (Vorbild `Domain.Pipeline/ImageProcessing/ImageProcessingPipeline.cs`): ein
Handler mit injiziertem `IImagePairReadStore`, der auf `RangeAngefordert`/`EinfrierenAngefordert`
reagiert und Commands yieldet.

---

## 3. Konventionen (aus CLAUDE.md — verbindlich)

- **Deutsch** für Domäne/Kommentare; Bestand konsistent halten.
- Neue **Verträge → `Abstractions`**; Marten/Infra → `Infrastructure` bzw. `Domain.Infrastructure`.
- **Keine Runtime-Reflection** (Inv. 4). Neue Dispatch-Logik = Generator erweitern, nicht Handschalter.
- **Kein `InMemoryEventStore`** — Store-Semantik nur gegen echtes Marten (Integration). Der
  Prüfstand testet nur store-freie Logik.
- **Proto-Regen** bei jedem neuen Command/Event/Query/Trigger (Anhang A.4). Signale sind ausgenommen.

---

## 4. Bauen / Testen / Laufen

```bash
dotnet build CqrsSolution.sln -c Release
dotnet test Infrastructure.Pruefstand.Tests/Infrastructure.Pruefstand.Tests.csproj   # store-frei, immer grün
dotnet test Infrastructure.Integration.Tests/Infrastructure.Integration.Tests.csproj # braucht Infra, sequentiell
```

Vor Integration/Lasttest **`docs/testen-und-lasttest.md` lesen** (Integration sequentiell lassen;
bekannter `SnapshotLive`-Cold-Boot-Flake ist Consul-Boot).

---

## 5. Akzeptanzkriterien je Meilenstein

- **M1 (Datensatz-Aggregat):** neue Prüfstand-Tests decken die Invarianten ab (Range-Union/Dedup,
  „keine Änderung nach Einfrieren" → `DatensatzBereitsEingefroren`, „Einfrieren ohne Mitglieder" →
  `DatensatzLeer`, Split deterministisch). `dotnet build` grün, **bestehende 126 Prüfstand-Tests
  bleiben grün**.
- **M3/M4 (Read + Query):** Integrationstest zeigt: nach `DatensatzEingefroren` liefert
  `HoleDatensatzSamples` die eingefrorenen Samples paginiert (analog `LiveCommandE2ETests`).
- **M6 (Trainingslauf):** Fortschritts-Fold korrekt (mehrere `MeldeFortschritt` → `MetrikHistorie`),
  Timeout über `Frist` verdrahtet.
- **M7/M8 (Python):** `await client.query(SucheImagePairsDto(...))` liefert eine **typisierte**
  Antwort; der `TrainingWorker` zieht Samples per `query()` und meldet Fortschritt zurück.

---

## Anhang A — Umgebungs-Setup (in dieser Sandbox erprobt)

> **Wichtig:** Kein .NET-SDK vorinstalliert, und die offiziellen .NET-Download-Hosts sind per
> Egress-Policy gesperrt (403). Docker-Hub-Images ebenso (cloudfront 403). `api.nuget.org` und
> `releases.hashicorp.com` sind erreichbar. Deshalb der folgende Weg.

**A.1 SDK (baut `net9.0` mit dem .NET-10-SDK):**
```bash
sudo apt-get update && sudo apt-get install -y --no-install-recommends dotnet-sdk-10.0
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1 PATH="$PATH:/usr/lib/dotnet"
# net9-Ref-Packs kommen beim restore von nuget.org (erreichbar)
```

**A.2 Backend laufen lassen** (`Host.Grpc`): läuft per Roll-Forward auf der net10-Runtime:
```bash
DOTNET_ROLL_FORWARD=LatestMajor dotnet Host.Grpc/bin/Release/net9.0/Host.Grpc.dll
```

**A.3 Frontend laufen lassen** (`Host.Blazor`): **braucht die echte net9-Runtime** — unter net10
liefert `_framework/blazor.server.js` 404 (Static Web Assets), die Seite hängt auf „Verbinde…".
net9-Shared-Framework aus den NuGet-Runtime-Packs installieren:
```bash
V=9.0.19
for P in microsoft.netcore.app.runtime.linux-x64 microsoft.aspnetcore.app.runtime.linux-x64; do
  curl -sSL "https://api.nuget.org/v3-flatcontainer/$P/$V/$P.$V.nupkg" -o "$P.zip"
  unzip -oq "$P.zip" -d "$P"
done
# runtimes/linux-x64/{lib/net9.0,native} nach /usr/lib/dotnet/shared/{Microsoft.NETCore.App,Microsoft.AspNetCore.App}/$V kopieren
dotnet --list-runtimes   # muss 9.0.x zeigen; dann Host.Blazor OHNE Roll-Forward starten
```

**A.4 Proto-Regeneration** (Pflicht bei neuen Domain-Typen):
```bash
dotnet run --project Proto.SourceGeneration      # erzeugt domain.proto-DTOs
dotnet build ProtoRepo/ProtoRepo.csproj          # ProtoRepo neu
dotnet build Infrastructure/Infrastructure.csproj
```

**A.5 Infrastruktur nativ** (Docker-Hub gesperrt → apt + Binary):
```bash
sudo apt-get install -y --no-install-recommends postgresql redis-server
sudo pg_ctlcluster 16 main start
sudo -u postgres psql -c "ALTER USER postgres WITH PASSWORD 'postgres';"
sudo -u postgres createdb cqrs_events
sudo redis-server --daemonize yes
# Consul-Binary (Docker-Image gesperrt):
curl -sSL "https://releases.hashicorp.com/consul/1.19.2/consul_1.19.2_linux_amd64.zip" -o consul.zip
unzip -o consul.zip && sudo mv consul /usr/local/bin/
setsid consul agent -dev -client=0.0.0.0 </dev/null >/tmp/consul.log 2>&1 &
```
Default-Connectionstrings passen (`localhost`, DB `cqrs_events`, `postgres/postgres`, Redis
`localhost:6379`, Consul `localhost:8500`). Server sauber neu starten:
`pkill -9 -f Host.Grpc.dll; sudo fuser -k 5001/tcp` (alte Instanz hält sonst Port 5001).
Kompletter Reset: `DROP SCHEMA es CASCADE; DROP SCHEMA rm CASCADE;` + `redis-cli FLUSHALL` +
Consul neu (leert stale Cluster-Member).

**A.6 Testdaten säen** (falls die GUI mit echten Daten gezeigt werden soll): ein kleiner
gRPC-Client-Seeder, der die **echte** Client-DI (`AddClientDomain_…` + `GrpcProxy` +
`ConnectionModule`) spiegelt und Commands auf den `ClientBus` publiziert — kein DB-Backdoor. Nach
jedem Command warten, bis `IVersioningModule.GetVersion(id)` steigt (OCC ohne SyncContext ist
sonst rennanfällig).

---

## Anhang B — Bekannte Fallstricke (in dieser Codebasis beobachtet)

1. **Wire-Enum Default-Wert 0**: beim Säen wurde `BildVersion.Dc0` (Enum 0) über die Wire
   **konsistent verworfen**, `Dc2` (1) akzeptiert. Passt zur Schuld „`DtoMapperGenerator` fragil,
   hartkodierte Enums" (CLAUDE.md). **Beim Entwurf neuer Enums/Commands prüfen**, dass der
   Default-Enumwert 0 sauber serialisiert (sonst Felder mit Wert 0 gehen verloren).
2. **OpenCvSharp native** ist im framework-dependent Build nicht für Linux dabei → der
   File-Drop-Pfad der Pipeline (OpenCV-Preprocessing) ist in dieser Sandbox fragil. Für Tests
   Daten lieber per gRPC-Command säen (Anhang A.6) statt Bilder in den WatchPath zu legen.
3. **Host.Blazor auf net10-Roll-Forward** = `blazor.server.js` 404 (siehe A.3). Immer die
   net9-Runtime nutzen.
4. **Zwei Frontend-Generationen auf Disk**: `Domain.Client`/`Domain.Client.Ui.Blazor` sind tot
   (nicht in der `.sln`). Produktiv ist **nur** `Domain.Client.Modules.Blazor`.
