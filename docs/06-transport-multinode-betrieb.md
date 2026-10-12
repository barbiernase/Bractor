# 06 — Transport, Multi-Node & Betrieb

## 6.1 Die zwei Prozesse

| Prozess | Rolle |
|---|---|
| **Host.Grpc** | der einzige Cluster-tragende Prozess: Marten + Consul + Redis + ActorSystem + gRPC |
| **Host.Blazor** | reiner gRPC-**Client** (kein ActorSystem, kein Consul); redet über den bidirektionalen Stream mit Host.Grpc; entkoppelbar auf eine andere Maschine (`GrpcServer__Address`) |

### Host.Grpc-Bootstrap (`Host.Grpc/Program.cs`, seit Werkbank-P2)
- Der Host lädt eine **Umgebung**: `Host.Grpc --umgebung <name>` (bzw. `CQRS_UMGEBUNG`) → `umgebungen/umgebung.<name>.json`
  (mit dem Host veröffentlicht). Umgebungsvariablen und Argumente überschreiben (`Quellen__DateiErkannt__Pfad=…`), `${env:NAME}` und
  `${branch}` werden ersetzt; unbekannte Abschnitte/Felder sind Start-Fehler (Bindung per Source-Generator).
- `builder.AddCqrs(args)` (`Infrastructure/Umgebung/CqrsUmgebungExtensions.cs`) verdrahtet alles: Kestrel (gRPC-Port HTTP/2,
  Einblick-Port HTTP/1.1+2 für `/health`, Webhook, Werkbank), Siegel, `AddCqrsFramework`, Pull-Pfade, Prozesse/Flüsse, Monitoring,
  Deadlines, Akteure (`Identitaeten`), Quellen, Funktions-Laufort (`Funktionen`). `app.LaufeAsync(umgebung)` startet (oder migriert).
- **Umgebungs-Siegel** (`<schema>.umgebung`): ein Host startet nur gegen eine DB mit passendem Namen/Art/Cluster; eine Sim-DB legt
  der Host bei Bedarf an. **Uhr** (`<schema>.uhr`): Versatz nur in Sim wirksam. Redis-Schlüssel tragen das Präfix der Umgebung.
- `Cluster:Art = Einzelknoten` (in-memory Mitgliedschaft) oder `Consul`.
- Konsumenten-Registrierung nach dem Kern: `AddGeneratedPullPaths()`, `AddGeneratedProzesse()`,
  Pipeline-Services, `AddBackendMonitoring()`, `AddDeadlines(...)`.
- Exponierte Dienste: gRPC `CqrsClientService` (bidirektionaler Stream `Connect`), Webhook
  `POST /webhook/datei`, Monitoring-Endpoints.
- Cluster: `ConsulProvider` + `PartitionIdentityLookup`; Kinds dynamisch aggregiert
  (Aggregate + Pipeline + Adapter via `IClusterKindContributor` + Broker). Kein statischer
  Seed — Consul ist die Discovery. Cluster-Start mit hartem 30-s-Timeout.

## 6.2 Der Wire-Transport (Multi-Node-Herzstück)

Ohne Serializer wären die internen Actor-Nachrichten rohe CLR-Records → de facto single-node.

**Marker & Discovery.** `IWireMessage` markiert genau die **Top-Level-Transporthüllen** — nicht
die Domänen-`ICommand`/`IEvent`, die reisen *geschachtelt* als polymorphe Payloads.
Implementierer über alle Planes:
- **Command-/Pull-Plane**: `CommandEnvelope`, `CommandResult`, `EventEnvelope`, `Wake`/`WakeAck`.
- **PubSub-Plane**: `Subscribe`, `Unsubscribe`, `Publish`, `Ack`, `Activate`,
  `GetSubscriberCount`, `SubscriberCountResponse`.
- **Pipeline-Plane**: `SignalEnvelope`, `PipelineAck`, konkrete `IPipelineTrigger`.
- **Prozess-Plane**: `ProzessWake`.

**Generator (reflexionsfrei)** — `WireSerializerGenerator` emittiert `GeneratedWire`
(Top-Level-Whitelist: `Serialize/Deserialize/TypeName/CanSerialize`) und `GeneratedWirePoly`
(polymorphe Payload-Dispatch je Wurzel Event/Command/Signal). Diskriminator = PascalCase-Typname,
identisch zu `GeneratedTypeRegistry`.

**STJ-Manifest** — `CqrsWireJsonContext` (hand-gepflegt, `Metadata`-Mode) enthält alle
Wire-Hüllen + alle Commands/Events/Trigger/Signale, plus eingebackene Converter
(`IEventJsonConverter`, `PidJsonConverter`, `CommandModusJsonConverter`, …).

**Besonderheiten** (`WirePolyConverters.cs`):
- Polymorphe Payloads als 2-Element-Array `["Name", {…}]`, single-pass.
- **PID-Converter**: serialisiert nur `Address`+`Id`, rekonstruiert via `PID.FromAddress` —
  Location-Transparency routet über die Adresse. PID selbst bleibt bewusst beim
  Default-Protobuf-Serializer (Whitelist schließt `IMessage` aus). *Designentscheidung:* PID
  wird beibehalten (nicht ClusterIdentity).
- `CommandModus`-Summentyp hand-serialisiert (privater Ctor).

**Proto.Remote-Anbindung** — `CqrsWireSerializer : ISerializer`, `SerializerId = 100`
(cross-node identisch), `Priority = -100` (zwischen Default-Protobuf 0 und Default-JSON -1000).
`CanSerialize` = strikte Whitelist. Registriert an `.WithSerializer(...)` **vor** `WithRemote`.

**Boot-Guard** — `WireSerializerBootCheck` läuft direkt nach `WithRemote/WithCluster`. Zwei
Achsen: (a) jede `IWireMessage`-Hülle ist serialisierbar; (b) jeder polymorphe Payload
(Commands+Events+Signals+Triggers) hat eine `JsonTypeInfo` im Context — sonst **Start-Abbruch**.
Backstop gegen Registry↔Context-Drift, den der Compile-Guard nicht fängt.

**Nachgewiesen** (Integration + LoadHarness, siehe [12](12-tests-und-vermessung.md)):
`TwoNodeCommandDispatchTests`, `TwoNodePubSubSignalTests`, `RemotePidDeliverySmokeTests`,
`ClientSubscriptionReassertTests`; cross-node Saga via `LoadHarness --mode saga`; echter
3-Node-Container-Betrieb.

## 6.3 Multi-Node-Betrieb (`deploy-multinode/`)

`docker-compose.yml` + `Dockerfile`. Topologie: ein Docker-Netz, Service-Namen als DNS.
- **Infra**: `consul` (`agent -dev`), `postgres:16`, `redis:7` — mit Healthchecks.
- **Nodes**: `grpc1/2/3` (identisches Image), plus optionaler `loadharness` (Profil `verify`)
  und ein `migrate`-Init-Node.
- **Cold-Start via dediziertem Migrator**: der `migrate`-Service (`Cluster__Role=migrator`)
  legt **alle** Marten-Objekte (inkl. der sonst lazy erzeugten Snapshot-Tabellen) eager +
  advisory-lock-gesichert an, dann `exit 0`, `restart: no`. Member (`Cluster__Role=member`)
  warten per `depends_on: migrate: service_completed_successfully` → kein gleichzeitiges
  `CREATE TABLE`-Race (23505).
- **Der Multi-Node-Hebel**: `Cluster__AdvertisedHost` = eigener Service-Name je Node — nur so
  erreichen sich Member über die Leitung.
- **Ports**: nur `grpc1` exponiert `127.0.0.1:5001` extern; grpc2/3 nur netzintern.
- **Dockerfile**: Multi-Stage (sdk:9.0 → aspnet:9.0), parametrisiert über `PROJECT`/`ENTRY_DLL`
  (dasselbe Dockerfile baut Host.Grpc UND LoadHarness). `dotnet publish` zieht nur den
  Zielgraphen (Domain.Client/Blazor ist nicht im Graph → wird nicht gebaut, umgeht den
  Frontend-Build-Blocker).

## 6.4 Regulärer Deploy-Weg (`deploy` / `deploy-linux` / `deploy-windows`)

Der **Produktions-Standardweg** ist NICHT der Multi-Node-Container, sondern **native Prozesse +
Docker nur für Infra** (bewusst, wegen GPU-/OpenCV-Zugriff).

- **`deploy/`** = systemd-Weg (single-node): `cqrs-grpc.service` / `cqrs-blazor.service`
  (`Type=exec`, `EnvironmentFile`, `Restart=on-failure`, `LimitNOFILE=65536`); `.env`-Dateien;
  `setup-server.sh` (Einmal-Setup inkl. GitHub-Actions-Self-Hosted-Runner); `deploy.sh`
  (publish + `setcap` + `nohup`).
- **`deploy-linux/`** = vorgebautes Artefakt-Paket + Infra-Compose + `start-server.sh`.
- **`deploy-windows/`** = analog (`Host.Grpc.exe`, web.config, `start.ps1`).
- **`build.sh linux|windows`** = macOS/ARM-Cross-Compile erzeugt die Deploy-Pakete.

## 6.5 Konfiguration (`CqrsFrameworkBuilder`)

| Schalter | Default | Wirkung |
|---|---|---|
| `SchemaRole` | Standalone | Standalone/Migrator/Member — Cold-Start-Strategie (AutoCreate) |
| `CommandTimeoutSeconds` | 30 | expliziter Npgsql-CommandTimeout |
| `SnapshotThreshold` | 200 | Events/Snapshot; 0 = aus |
| `InboxCap` | 10 000 | Obergrenze der Framework-Inbox (Dedup-Ids) |
| `AppendBatching` | true | node-lokaler Group-Commit |
| `AppendBatchMaxSize` | 256 | Appends pro Batch-Transaktion |
| `AppendBatchLingerMs` | 0 | Linger-Fenster (0 = opportunistisch) |
| `AppendDrainParallelism` | 4 | parallele Commit-Drain-Loops (gemessenes 4-Kern-Optimum) |
| `UseGeneratedJsonSerializer` | **false** | STJ-Source-Gen für Marten-Event-Storage (opt-in, Mess-Schalter) |
| `EnableVersionTracking` | true | Redis-Version-Index; false = Betrieb ganz ohne Redis möglich |
| `EnableGrpc` | true | gRPC-Client-Service |

> Der frühere `CqrsFrameworkOptions`-`[Obsolete]`-Typ ist tot; der aktive Pfad ist
> ausschließlich `CqrsFrameworkBuilder`.

### Umgebungs-Abschnitte (`umgebungen/umgebung.<name>.json`)
| Abschnitt | Beispiel | Zweck |
|---|---|---|
| `Umgebung` | `{ "Name": "prod", "Art": "Prod" }` | Name + Art (Sim/Prod) — beides im Siegel |
| `EventStore` | `Verbindung`, `Datenbank` (`cqrs_sim_${branch}`), `Schema` | Marten/Postgres |
| `VersionIndex` | `Betrieb` (Aus/Verwaltet/Vorhanden), `Endpunkt`, `Praefix` | Redis |
| `Cluster` | `Art` (Einzelknoten/Consul), `Name`, `Consul`, `AdvertisedHost`, `Rolle` (Allein/Migrator/Mitglied) | Proto.Actor |
| `Schreibpfad` | `GroupCommit`, `BatchGroesse`, `Drain`, `SnapshotSchwelle`, … | Tuning |
| `Transport` | `GrpcPort`, `EinblickPort` | Ports |
| `Identitaeten` | `Tokens`, `Standard`, `VertragStreng` | Akteur-Tor |
| `Quellen` | `DateiErkannt: { Pfad, Muster, Fertig, Kennung, Laufmodus }` | Treiber-Konfig |
| `Funktionen` | `IBildVerkleinerung: { Laufort, Implementierung, Slots, Wiederholungen }` | Laufort je Funktion |
| `Konfig` | `PreprocessingConfig: { OutputPath }` | Konfig-Records der Domäne |

Jeder Wert lässt sich per `__`-Variable überschreiben (z. B. `Cluster__AdvertisedHost=grpc1`, `Cluster__Rolle=Migrator`).
Host.Blazor liest weiter `Blazor__Urls` / `GrpcServer__Address`.

**Ports**: 5001 (gRPC/h2c), 5010 (Blazor UI), 5432 (Postgres), 6379 (Redis), 8500 (Consul).

### Betriebs-Kommandos
- Infra: `docker compose -f deploy-linux/docker-compose.infrastructure.yml up -d`
- Single-Node nativ: `./start-server.sh {start|stop|status}` bzw. systemd
- Build-Pakete: `./build.sh linux|windows`
- Multi-Node: `docker compose -f deploy-multinode/docker-compose.yml up -d --build` (Umgebung `multinode`); Teardown `down -v`

## 6.6 Monitoring

`AddBackendMonitoring()` + `MapBackendMonitoring()` (`Infrastructure/Monitoring/`), bewusst
schmal (2 Zahlen):
- **`GET /health`**: HealthReport als JSON. DLQ nicht leer ⇒ `Degraded`; Store-Quelle wirft ⇒
  `Unhealthy`. Doppelt als Erreichbarkeits-Probe (berührt Offen-Index + DLQ, beide Postgres).
- **`GET /monitoring/metrics`**: `BackendMetrik(OffeneProzesse, DlqEinträge)` — aus den
  durablen Quellen der uniformen Maschine, kein separater Zählerzustand.

Keine Prometheus-Exposition, kein OpenTelemetry.

## 6.7 Erkannte Betriebs-Prinzipien

1. **Ein Node-Image, Rolle per Env** (standalone/migrator/member über `Cluster__Role`).
2. **„Migrate once, then scale out"** — Schema-Erzeugung von der Laufzeit getrennt.
3. **Fail-fast am Boot** — Wire-Serializer-Lücke + Cluster-Join-Hänger brechen hart ab.
4. **Reflexionsfreiheit bis in den Transport** — generierter Wire-Serializer, doppelter Guard.
5. **Domänen-Reinheit im Transport** — nur Transporthüllen tragen `IWireMessage`.
6. **Infra containerisiert, Compute nativ** (Regelfall, wegen GPU).

## 6.8 Schulden & Cold-Start-Themen

- **Consul im `-dev`-Modus** in beiden Compose-Dateien — nicht produktionshart (kein Quorum,
  kein persistenter Cluster).
- **`CqrsWireJsonContext` hand-gepflegt** — jeder neue Typ braucht eine `[JsonSerializable]`-Zeile
  (Boot-/Compile-Guard fangen Vergessen ab, aber manuell).
- **`deploy/deploy.sh` inkonsistent** (`DOTNET_ENVIRONMENT=Development` vs. systemd `Production`);
  harte projektspezifische Pfade.
- **Klartext-Default-Credentials** (`postgres/postgres`) durchgängig.
- **Monitoring schmal** — keine Prometheus/OTel, keine Cluster-Member-Metriken über den Endpoint.
- **Single-Node bleibt der Produktions-Regelweg**; echter Multi-Node existiert als
  Container-Compose + Verify-Harness, nicht als produktives systemd-Cluster.
- **Migrator ist Init-Job, kein laufender Migrationsdienst** — rolling Schema-Evolution im
  laufenden Cluster ist über diesen Weg nicht abgedeckt.
