# 06 — Transport, Multi-Node & Betrieb

Stand 2026-10-28 (Plan Pub/Sub-Neustrukturierung Phase 6: Wecker statt Signale, ohne Redis), gegen den Code geprüft. Ziel für den Umbau: docs/plan-pubsub-neustrukturierung.md.

Abschnittsnummern sind stabil, weil Code-Kommentare und `CLAUDE.md` auf dieses Kapitel verweisen.

## 6.1 Die Prozesse

| Prozess | Rolle |
|---|---|
| **Host.Grpc** | der einzige Cluster-tragende Prozess: Marten + ActorSystem (Einzelknoten oder Consul) + gRPC + Einblick-Port |
| **Host.Blazor** | Beispiel-GUI der Beispieldomäne, ein gRPC-**Client** mit Vertrag (kein ActorSystem, kein Cluster); Verbindung aus `Clients:<Name>` der Umgebung (Kapitel [08](08-frontend-blazor-client.md)) |
| **Werkbank** | Entwurfs-Oberfläche (`dotnet run --project Werkbank`); startet den Host als eigenen Prozess (`HostProzess`), **nur in einer Sim-Umgebung** — bei Prod endet ⟳ nach dem Lesen (`konzept-werkbank.md` §5) |
| **Python-Worker externer Funktionen** | generischer `funktions_worker.py` für Funktionen mit Laufort `Extern`; meldet sich wie jeder Client am gRPC-Port an (Token, dessen Akteur `IDarf<F>` hat) und holt Aufträge beim Vermittler. Gestartet über „▶ Worker“ der Werkbank (`Werkbank/Server/Worker.cs`, aus `Funktionen:<F>:Worker`) oder von Hand (Kapitel [09](09-python-sdk.md)) |

### Host.Grpc-Bootstrap (`Host.Grpc/Program.cs`)

`Program.cs` ist nur `var umgebung = builder.AddCqrs(args); … await app.LaufeAsync(umgebung);` — ohne Domänen-Zeile (`konzept-werkbank.md` §4.2);
`AddCqrs(args)` generiert die Komposition (`Domain.Laufzeit`, `GeneratedAnwendung`), die der Host referenziert.

- **Umgebung:** `--umgebung <name>` bzw. `CQRS_UMGEBUNG` → `umgebungen/umgebung.<name>.json` (mit dem Host veröffentlicht). Umgebungsvariablen
  und Argumente überschreiben (`Quellen__DateiErkannt__Pfad=…`), `${env:NAME}` und `${branch}` (aus `CQRS_BRANCH`) werden ersetzt; ein
  unbekannter Abschnitt oder ein unbekanntes Feld ist ein Startfehler (Lader: `Infrastructure/Umgebung/UmgebungLader.cs`). Sim-DB je Branch:
  `cqrs_sim_${branch}`.
- **`AddCqrs`** (`Infrastructure/Umgebung/CqrsUmgebungExtensions.cs`) bindet der Reihe nach: Kestrel (gRPC-Port HTTP/2; Einblick-Port
  HTTP/1.1+2, 0 = aus), Umgebungs-Siegel, `AddCqrsFramework` (Werte aus der Umgebung), `IDbClock` mit Versatz, Projektions-Stand,
  Pull-Pfade, Prozesse und Flüsse, Monitoring, Fristen, Akteure (`Identitaeten`), Quellen, Funktionen (Laufort), Dienste, Einblick.
- **`LaufeAsync`** mappt gRPC (`CqrsClientService`, bidirektionaler Stream `Connect`), Monitoring, Einblick, JSON-Eingang und die Quell-Webhooks
  (§6.6). Mit Rolle `Migrator` legt es nur das Schema an und endet (§6.3).
- **Umgebungs-Siegel** (`<schema>.umgebung`): ein Host startet nur gegen eine DB mit passendem Namen, passender Art und passendem Cluster-Namen;
  eine Sim-DB legt der Host bei Bedarf an. **Uhr** (`<schema>.uhr`): Versatz nur in Sim wirksam.
  Details `konzept-werkbank.md` §4.3–§4.4.
- **Cluster:** `Cluster:Art = Einzelknoten` (Mitgliedschaft in-memory über den `TestProvider` von Proto.Cluster, kein Consul — Standard in Sim)
  oder `Consul` (`ConsulProvider` als Discovery, kein statischer Seed). In beiden Fällen `PartitionIdentityLookup`. Die Kinds werden beim
  Start aggregiert: Aggregate und alle `IClusterKindContributor` (Batch-Leser, Funktions-Actors, Funktions-Vermittler, Uhr, Quellen-Halter); dazu
  spawnt jeder Knoten seinen Verteiler (lokaler Actor mit festem Namen `verteiler`, `VerteilerStart`). Cluster-Start mit hartem 30-s-Timeout (`ClusterStartupService`), danach die **Start-Sperre** (`Bereitschaft`): 32 Proben-Identitäten, die über
  die Mitglieder streuen, werden gefragt, bis jede antwortet (höchstens 90 s, dann mit Warnung weiter) — erst dann starten Takt und Eingänge. Nach
  dem harten Ende eines Knotens steht er noch in der Mitgliederliste, und der Cluster legt Aktivierungen auf ihn; ohne die Sperre arbeitete ein
  neu gestarteter Host Minuten lang gegen Fristen (vorher leistete das unausgesprochen die Aktivierung der Broker beim Start).
- **Boot-Fehler** gehen als Zeile `CQRS-BOOT {text, karte, art}` auf stderr; die Werkbank hängt sie an die Karte.

## 6.2 Der Wire-Transport (Multi-Node-Herzstück)

Ohne Serializer wären die internen Actor-Nachrichten rohe CLR-Records → de facto single-node.

**Marker.** `IWireMessage` (`Abstractions/IWireMessage.cs`) markiert genau die **Top-Level-Transporthüllen** — nicht die Domänen-`ICommand`/
`IEvent`/`IAuftrag`, die reisen *geschachtelt* als polymorphe Payloads. Implementierer je Plane (prüfen mit
`rg -n ": IWireMessage" -g '*.cs' Infrastructure Abstractions`):

| Plane | Hüllen |
|---|---|
| Command/Pull | `CommandEnvelope`, `CommandResult`, `EventEnvelope`, `Wake`/`WakeAck` |
| PubSub | `Subscribe`, `Unsubscribe`, `Publish`, `Ack`, `Activate`, `GetSubscriberCount`, `SubscriberCountResponse` |
| Prozess | `ProzessWake` |
| Funktionen | `AuftragAnbieten`, `HoleArbeit`, `ArbeitZugeteilt`, `ArbeitErledigt`, `ArbeitLebt`, `ListeOffene`, `OffeneAuftraege` (`Infrastructure/Funktionen/FunktionsVermittlung.cs`) |
| Quellen | `HalteQuelle`, `QuelleGehalten` (Lease für `Laufmodus: EinmalImCluster`, `Infrastructure/Quellen/QuellenHalter.cs`) |

**Generator (reflexionsfrei)** — `WireSerializerGenerator` (Infrastructure.SourceGeneration) emittiert `GeneratedWire` (Top-Level-Whitelist:
`Serialize/Deserialize/TypeName/CanSerialize`) und `GeneratedWirePoly` (polymorpher Payload-Dispatch je Wurzel Event, Command).
Diskriminator = Typname, identisch zu `GeneratedTypeRegistry`.

**STJ-Manifest** — `CqrsWireJsonContext` (`Domain.Laufzeit/Serialization/CqrsWireJsonContext.g.cs`, `Metadata`-Mode) ist **generiert**: der
Codegen-Schritt im Build (Target `CqrsCodegenPrepass` in `Domain.Leitung/Domain.Leitung.csproj` → `Cqrs.Codegen`, `JsonContextEmitter`) schreibt
Framework-Kopf (alle Wire-Hüllen) plus alle Commands/Events/Trigger der Domäne. Eingebacken sind die Converter aus `WirePolyConverters.cs`
(`IEvent`, `ICommand`, `IMessageEnvelope`, `PID`, `CommandModus`). `Cqrs.Codegen` liest die gebauten
Domänen-DLLs heute per `MetadataLoadContext` — das ist Reflection in einem Werkzeug (docs/13 W10-1).

**Besonderheiten** (`WirePolyConverters.cs`):
- Polymorphe Payloads als 2-Element-Array `["Name", {…}]`, single-pass.
- **PID-Converter**: serialisiert nur `Address`+`Id`, rekonstruiert via `PID.FromAddress` — Location-Transparency routet über die Adresse.
  *Designentscheidung:* PID bleibt (nicht ClusterIdentity).
- `CommandModus`-Summentyp hand-serialisiert (privater Ctor).

**Proto.Remote-Anbindung** — `CqrsWireSerializer : ISerializer`, `SerializerId = 100` (cross-node identisch), `Priority = -100` (zwischen
Default-Protobuf 0 und Default-JSON -1000). `CanSerialize` = strikte Whitelist (`GeneratedWire.CanSerialize`); `PID` und andere
Protobuf-`IMessage` bleiben beim Default-Serializer. Registriert an `.WithSerializer(...)` **vor** `WithRemote` (`CqrsServiceExtension.cs`).

**Boot-Guard** — `WireSerializerBootCheck.Verify()` läuft direkt nach `WithRemote/WithCluster`: (a) jede `IWireMessage`-Hülle ist
serialisierbar; (b) jeder Command/Event/Trigger aus `GeneratedTypeRegistry` hat eine `JsonTypeInfo` im Kontext — sonst
**Start-Abbruch**. Backstop gegen Registry↔Kontext-Drift neben dem Compile-Zeit-Guard des Generators.

**Clients: Sitzung und Verteiler** (Plan Pub/Sub-Neustrukturierung §2.7). Je gRPC-Verbindung ist die Sitzung ein Actor (`SitzungsActor`), der
einzige Schreiber auf den Antwort-Stream; die Lese-Schleife der Verbindung reicht jede Nachricht des Clients nur weiter, was wartet (Query,
Antwort einer Funktion draußen, Weiterleitung an einen zuständigen Client) läuft neben der Mailbox. Ihre Id nennt den Knoten
(`<Adresse>#<Nummer>`). Nach dem Handshake meldet sie ihre wirksame Liste (Vertrag ∩ Token) beim **Verteiler** ihres Knotens an — einem lokalen
Actor mit festem Namen `verteiler`, der Typ → Sitzungen hält und jede Sitzung beobachtet (`Watch`). Ein Commit geht als EINE Nachricht
(`Verteile`) an den Verteiler jedes Knotens (Mitgliederliste, `Bote.MeldeAnKnoten`), nur mit Typen, die irgendein Client hören darf; gezielte
Antworten nur an den Knoten der Sitzungs-Id. Liest ein Client nicht nach, verwirft der Verteiler über 1000 offenen Zustellungen je Sitzung
(`Rueckstau`). Proto.Clusters eigenes Pub/Sub wird bewusst nicht genutzt. Dieselben Verteiler tragen „P hat S weiterverarbeitet“
(`Verarbeitet`) an die Sitzungen, die eine Antwort mit diesen Streams halten ([04](04-konsum-und-prozess-maschine.md) §4.1).

**Nachgewiesen** durch Integrationstests (`TwoNodeCommandDispatchTests`, `RemotePidDeliverySmokeTests`), das Verhaltens-Netz
(`ZweiKnotenTests`: gezielte Antworten an der richtigen Sitzung bei zwei Knoten, Weckrufe nach dem harten Ende eines Knotens) und den
3-Node-Container-Betrieb (§6.3). Welche Tests es gibt und was sie messen: [12](12-tests-und-vermessung.md).

## 6.3 Multi-Node-Betrieb (`deploy-multinode/`)

`docker-compose.yml` + `Dockerfile`. Alle Werte stehen in `umgebungen/umgebung.multinode.json` (`CQRS_UMGEBUNG=multinode`, `Cluster:Art =
Consul`); je Node setzt Compose nur `Cluster__Rolle` und `Cluster__AdvertisedHost`. Topologie: ein Docker-Netz, Service-Namen als DNS.

- **Infra**: `consul` (`agent -dev`), `postgres:16-alpine` — mit Healthchecks (kein Redis mehr, Plan Phase 6).
- **Nodes**: `grpc1/2/3` (identisches Image) und ein `migrate`-Init-Node.
- **Cold-Start über einen dedizierten Migrator**: `migrate` (`Cluster__Rolle=Migrator`) legt **alle** Marten-Objekte (inkl. der sonst lazy
  erzeugten Snapshot-Tabellen) eager und advisory-lock-gesichert an (`CqrsSchemaMigrator`), dann `exit 0`, `restart: no`. Die Nodes
  (`Cluster__Rolle=Mitglied`, AutoCreate.None) warten per `depends_on: migrate: service_completed_successfully` → kein gleichzeitiges
  `CREATE TABLE`-Race (23505).
- **Der Multi-Node-Hebel**: `Cluster__AdvertisedHost` = eigener Service-Name je Node — nur so erreichen sich die Member über die Leitung.
- **Ports**: nur `grpc1` exponiert `127.0.0.1:5001` (gRPC, h2c) und `127.0.0.1:5002` (Einblick: `/health`, Quell-Webhook); grpc2/3 nur
  netzintern.
- **Dockerfile**: Multi-Stage (sdk:9.0 → aspnet:9.0), parametrisiert über `PROJECT`/`ENTRY_DLL`; `dotnet publish` zieht nur den
  Abhängigkeitsgraphen von Host.Grpc. Die Kommentare im Dockerfile und in `docker-compose.yml` nennen noch den entfernten
  LoadHarness-Verifikationsnode (Commit `fb07f5e`); Compose und Image brauchen ihn nicht.

## 6.4 Regulärer Deploy-Weg (`deploy` / `deploy-linux` / `deploy-windows`)

Der Regelbetrieb ist nicht der Multi-Node-Container, sondern **native Prozesse + Docker nur für Infra**. Die Begründung (GPU-/OpenCV-Zugriff)
kommt aus der Beispieldomäne (Bildverarbeitung), nicht aus dem Framework.

- **`deploy/`** = systemd-Weg (single-node): `cqrs-grpc.service` / `cqrs-blazor.service` (`Type=exec`, `EnvironmentFile`,
  `Restart=on-failure`, `LimitNOFILE=65536`); `cqrs-grpc.env` wählt nur die Umgebung (`CQRS_UMGEBUNG=prod`); `setup-server.sh`
  (Einmal-Setup inkl. GitHub-Actions-Self-Hosted-Runner); `deploy.sh` (publish + `setcap` + `nohup`, Host.Grpc mit `--umgebung prod`).
- **`deploy-linux/`** = vorgebautes Artefakt-Paket + Infra-Compose + `start-server.sh`.
- **`deploy-windows/`** = analog (`Host.Grpc.exe`, web.config, `start.ps1`).
- **`build.sh linux|windows`** = macOS/ARM-Cross-Compile; schreibt die Umgebung `umgebung.linux.json` in beide Pakete (Host.Grpc und
  Host.Blazor starten mit `--umgebung linux`).

**Veraltet:** Die Start-Skripte für Host.Blazor (`deploy/deploy.sh`, `deploy/deploy_clients.sh`, `deploy/cqrs-blazor.env`,
`deploy-linux/start-server.sh`, `deploy-windows/start.ps1`) setzen noch `Pipeline__WatchPath`, `Blazor__Urls` und `GrpcServer__Address`;
`deploy-linux/start-server.sh` startet beide Hosts ohne Umgebung und setzt `ConnectionStrings__EventStore`. Heute gilt: beide Hosts brauchen
`--umgebung` bzw. `CQRS_UMGEBUNG`; Host.Blazor liest `Clients:<Name>` der Umgebung, die Quelle `Quellen:<Typ>:Pfad`.

## 6.5 Konfiguration: die Umgebungs-Datei

Für Nutzer gibt es **eine** Konfiguration je Umgebung: `umgebungen/umgebung.<name>.json`. Die Abschnitte und Felder sind Typen in
`Abstractions/Umgebung/UmgebungKonfig.cs` — **sie sind das Schema** (eine Quelle; die Werkbank rendert daraus das Formular). Die vollständige
Abschnitts-Tabelle steht in `konzept-werkbank.md` §4.1; hier nur der Überblick:

- **Backend:** `Umgebung` (Name, Art `Sim`/`Prod`), `EventStore`, `Cluster`
  (Art, Name, Consul, AdvertisedHost, Rolle), `Schreibpfad` (generiertes JSON, Group-Commit, Batch, Linger, Drain, Snapshot-Schwelle,
  Inbox-Grenze), `Lebenszyklus` (Leerlauf bis zur Passivierung je Actor-Art), `Konsum` (Partitionen, Seitengröße und Takt je Partition des Batch-Konsums), `Transport` (gRPC-Port, Einblick-Port, JSON-Eingang).
- **Was aktiv ist:** `Identitaeten` (Token → Akteure; ohne Abschnitt ist das Akteur-Tor offen), `Quellen` (Treiber-Konfig inkl. Webhook),
  `Funktionen` (Laufort `Host`/`Extern`/`Hand`, Implementierung, Slots, Wiederholungen, `Worker`), `Dienste` (Implementierung je
  Dienst-Interface), `Konfig` (Konfig-Records der Domäne, Feld für Feld), `Clients` (Token, Adresse, Urls, Dateien je Client-Vertrag),
  `Logging`.

Jeder Wert lässt sich per `__`-Variable überschreiben, z. B. `Cluster__AdvertisedHost=grpc1`. Die **Rolle** eines Knotens heißt
`Cluster__Rolle` mit den Werten `Allein` (Standard) / `Migrator` / `Mitglied`.

Intern übersetzt `AddCqrs` die Umgebung in den `CqrsFrameworkBuilder` (`AddCqrsFramework`); der Builder ist kein Nutzer-Schalter. Der Typ
`CqrsFrameworkOptions` existiert noch als toter Code (docs/13 P3-2).

**Ports** (Werte der mitgelieferten Umgebungen): `prod` 5001 (gRPC/h2c) / 5002 (Einblick); `sim-main` 5101 / 5102 mit JSON-Eingang;
Host.Blazor 5010 (`Clients:<Name>:Urls`); Postgres 5432, Consul 8500. Ohne Angabe ist der Einblick-Port 0 (aus).

### Betriebs-Kommandos
- Infra: `docker compose -f docker-compose.infrastructure.yml up -d` (bzw. in Sim: ⟳ der Werkbank startet fehlende Container,
  `konzept-werkbank.md` §4.5)
- Host lokal: `dotnet run --project Host.Grpc -- --umgebung sim-main`
- Single-Node nativ: `./start-server.sh {start|stop|status}` bzw. systemd (siehe „Veraltet“ in §6.4)
- Build-Pakete: `./build.sh linux|windows`
- Multi-Node: `docker compose -f deploy-multinode/docker-compose.yml up -d --build`; Teardown `down -v`

## 6.6 Monitoring, Einblick und Eingänge

**Monitoring** — `AddBackendMonitoring()` + `MapBackendMonitoring()` (`Infrastructure/Monitoring/`), bewusst schmal:
- **`GET /health`**: HealthReport als JSON. DLQ nicht leer ⇒ `Degraded`; Store-Quelle wirft ⇒ `Unhealthy`. Zugleich Erreichbarkeits-Probe
  (Offen-Index + DLQ, beide Postgres). Die Werkbank wertet den Host als bereit, sobald `/health` am Einblick-Port antwortet.
- **`GET /monitoring/metrics`**: `BackendMetrik(OffeneProzesse, DlqEinträge)` — aus den durablen Quellen, kein separater Zählerzustand.

Beide Endpunkte sind ohne Port-Bindung gemappt; per HTTP/1.1 erreichbar sind sie über den Einblick-Port (der gRPC-Port spricht nur HTTP/2).

**Am Einblick-Port** (nur dort, `RequireHost`):
- **Einblick-API** `/einblick/*` (`Infrastructure/Einblick/EinblickEndpunkte.cs`): `stand`, `spur?ab=`, `vorgaenge`, `vorgang/{k}`,
  `auftraege`, `hinweise`, SSE `neu` — die Spur aus den Log-Headern (`konzept-werkbank.md` §7).
- **JSON-Eingang** `/eingang/*` (`Infrastructure/Eingang/JsonEingang.cs`): `command`, `query`, `rechte`, `auftraege`, `ergebnis` — Commands und
  Queries als Akteur (Header `akteur-token` → Akteur-Tor), Antworten für Laufort `Hand`. Nur mit `Transport:JsonEingang` (Sim an, Prod aus;
  `konzept-werkbank.md` §6).
- **Quell-Webhooks**: je Quelle mit `Quellen:<Typ>:Webhook` mappt `MapQuellWebhooks` generisch `POST <route>` (Nachricht als JSON über den
  Wire-Kontext, Antwort `202 {vorgang}`, gleiche Nachricht = derselbe Vorgang); ohne Einblick-Port ist das ein Boot-Fehler
  (`konzept-werkbank.md` §3.6). Eine Route steht nicht im Programm.

Keine Prometheus-Exposition, kein OpenTelemetry (docs/13 P2-10).

## 6.7 Erkannte Betriebs-Prinzipien

1. **Ein Node-Image, Rolle per Env** (`Cluster__Rolle` = `Allein`/`Migrator`/`Mitglied`).
2. **„Migrate once, then scale out“** — Schema-Erzeugung von der Laufzeit getrennt.
3. **Fail-fast am Boot** — Umgebungs-Siegel, unbekannte Konfig-Felder, Wire-Serializer-Lücke und Cluster-Join-Hänger brechen hart ab.
4. **Generierter Transport** — Wire-Serializer und STJ-Kontext generiert, doppelter Guard (Build + Boot).
5. **Domänen-Reinheit im Transport** — nur Transporthüllen tragen `IWireMessage`.
6. **Umgebungen unterscheiden sich nur in Konfig** (W7) — derselbe Host, dieselben einkompilierten Implementierungen; Einzelknoten in Sim,
   Consul im Cluster.
7. **Keine Cluster-Nachricht ohne Frist** — der `Bote` (`Infrastructure/Lebenszyklus/Bote.cs`) kennt zwei Verben: `Melde` (verlierbar,
   ohne Antwort, intern gebunden) und `FrageAsync<T>` (Antwort mit Pflicht-Frist, ohne Antwort `null`). Ohne Frist wiederholt Proto.Cluster eine
   Anfrage unbegrenzt — bei einem toten oder noch nicht eingeschwungenen Knoten hinge der Aufrufer still. Die Antwort auf einen Command
   (`CommandResult`) trägt nur Ausgang, Versionen und Ablehnung, nicht die Events: über die Leitung reist so wenig wie möglich.
8. **Ein Weg je Absicht im Actor** (R18, CQRS112) — der Build verbietet in Actors und dem, was sie besitzen, eigene Fäden, `lock`, Feuern und
   Vergessen, `ContinueWith`, Blockieren und die rohe Cluster-API; Parallelität kommt von mehr Actors, lange I/O läuft neben der Mailbox
   (`RahmenActor.Neben` über `ReenterAfter`).

## 6.8 Offen

Offene Punkte stehen nur in `docs/13`: Multi-Node nur container-erprobt, Consul im `-dev`-Modus (P1-1); kein TLS, keine Identitäten für Prod,
Webhook ohne Akteur-Tor (P1-2); Monitoring schmal und Default-Credentials (P2-10); Reflection im Codegen (W10-1). Der Migrator ist ein
Init-Job, kein laufender Migrationsdienst; Schema-Evolution ist kein Ziel der Werkbank (`konzept-werkbank.md` §1).
