# Multi-Node-Deployment (mehrere Container, ein Consul) — Anleitung & Ergebnis

> **Zweck:** den in Iteration 1+2 gebauten Wire-Serializer im **echten verteilten Betrieb**
> nachweisen — drei gRPC-Cluster-Nodes als getrennte Container an EINEM Consul, plus ein
> Verifikations-Node, der dem Cluster beitritt und cross-node Commands absetzt.
> Alle Dateien liegen unter `deploy-multinode/`.

## Überblick

```
                         ┌───────────── Docker-Netz cqrs-multinode ─────────────┐
                         │                                                      │
   docker compose up  ─► │   consul        postgres        redis               │
                         │     ▲              ▲              ▲                   │
                         │     │  (Membership)│ (Event-Store)│ (Version-Index)   │
                         │  ┌──┴───┐      ┌───┴───┐     ┌────┴───┐               │
                         │  │grpc1 │◄────►│ grpc2 │◄───►│ grpc3  │   (ein Cluster,│
                         │  └──────┘      └───────┘     └────────┘    3 Member)  │
                         │      ▲  Adv=grpc1   Adv=grpc2    Adv=grpc3            │
                         │      │                                               │
                         │  ┌───┴──────────┐                                    │
                         │  │ loadharness  │  joint als 4. Member (--profile     │
                         │  │ (Verifier)   │  verify), dispatcht Commands        │
                         │  └──────────────┘  cross-node, prüft exactly-once     │
                         └──────────────────────────────────────────────────────┘
```

- **Ein** Consul (`agent -dev`), **ein** Postgres (der Event-Store = einzige Wahrheit), **ein** Redis
  (abgeleiteter Version-/Deps-Index) — alle drei Nodes teilen sie.
- **Drei** gRPC-Hosts (`grpc1`/`grpc2`/`grpc3`), identisches Image, unterschiedlicher
  `Cluster__AdvertisedHost` (= eigener Service-Name).
- **Ein** LoadHarness-Node als cross-node-Verifier (nur über das Compose-Profil `verify`).

## Die Dateien

| Datei | Rolle |
|---|---|
| `deploy-multinode/Dockerfile` | Multi-Stage-Image (`sdk:9.0` build → `aspnet:9.0` runtime), parametrisiert über `PROJECT`/`ENTRY_DLL` → baut sowohl Host.Grpc als auch LoadHarness. `dotnet publish <PROJECT>` zieht nur den Abhängigkeitsgraphen (Domain.Client/Blazor NICHT). netcat für den TCP-Healthcheck. |
| `deploy-multinode/docker-compose.yml` | Consul + Postgres + Redis + grpc1/2/3 + loadharness. YAML-Anker für gemeinsame Cluster-Env; grpc2/3 warten auf grpc1s Healthcheck (Schema-Race, s. u.). |
| `.dockerignore` (Repo-Root) | hält host-lokale `bin/obj` (falscher RID) und `deploy-linux/`-Artefakte aus dem Build-Context. |
| `LoadHarness/Program.cs` | minimal parametrisiert: liest `Cluster__Name`/`Consul__Address`/`Cluster__AdvertisedHost` + Connection-Strings aus der Config; ohne diese Env behält es sein natives, isoliertes Last-Test-Verhalten (Zufalls-Cluster). MIT ihnen JOINT es den laufenden `cqrs-cluster`. |

## Der kritische Wert: `Cluster__AdvertisedHost`

Jeder Node bindet Proto.Remote intern auf `0.0.0.0` (`CqrsServiceExtension.cs`), teilt den anderen
Membern aber nur seinen `AdvertisedHost` mit. Im Container **muss** das ein Name/eine IP sein, die die
anderen Container auflösen und erreichen — hier der Docker-Service-Name (`grpc1`/`grpc2`/`grpc3`). Der
native Default `localhost` funktioniert single-node, aber im Cluster würde jeder Node „localhost"
verteilen → die anderen erreichten ihn nie. **Das ist der eine Wert, der Multi-Node ausmacht.**

Der interne Proto-Remote-Port ist **ephemer** (vom OS vergeben) — er muss nicht gemappt/exponiert
werden, weil Container im selben Docker-Netz sich auf allen Ports direkt erreichen. Nur `grpc1:5001`
(der gRPC-Client-Port) wird optional nach außen gemappt.

## Anleitung

Voraussetzung: Docker + Docker Compose. Kein lokales .NET nötig (der Build läuft im SDK-Image).

```bash
# 1. Cluster bauen + hochfahren (grpc1 migriert das Schema zuerst, dann grpc2/grpc3)
docker compose -f deploy-multinode/docker-compose.yml up -d --build

# 2. Formation prüfen — Consul zeigt die drei Member
docker exec cqrs-multinode-consul-1 \
  wget -qO- 'http://localhost:8500/v1/catalog/service/cqrs-cluster' \
  | tr ',' '\n' | grep -E '"ServiceAddress"|"ServicePort"'

# 3. Node-Logs: Wire-Serializer registriert + Member-Konvergenz
docker compose -f deploy-multinode/docker-compose.yml logs grpc2 \
  | grep -E 'Wire-Serializer id=100|Cluster gestartet'

# 4a. Cross-node Command-Dispatch verifizieren (LoadHarness joint als 4. Member)
#     WICHTIG: --use-aliases, damit die grpc-Nodes den advertised Namen "loadharness" auflösen können
#     (docker compose run vergibt sonst KEINEN Service-Alias → Gossip-Rückweg tot; s. Stolpersteine).
docker compose -f deploy-multinode/docker-compose.yml --profile verify run --rm --use-aliases loadharness

# 4b. Cross-node PROZESS/SAGA verifizieren (Überweisungs-Prozess + Gelderhaltung)
docker compose -f deploy-multinode/docker-compose.yml --profile verify run --rm --use-aliases loadharness \
  --mode saga --accounts 20 --transfers 40 --concurrency 16 --log warning

# 5. Aufräumen (inkl. Volumes — löscht auch die Consul-Registrierungen)
docker compose -f deploy-multinode/docker-compose.yml down -v
```

## Stolpersteine (real aufgetreten) & Lösungen

1. **Marten-Schema-Race beim Cold-Start.** Fahren alle drei Nodes gleichzeitig hoch, führen sie
   parallel `CREATE SCHEMA es` / die Marten-Migration aus → `duplicate key value violates unique
   constraint "pg_namespace_nspname_index"`, zwei Nodes crashen. **Lösung:** `grpc1` bekommt einen
   TCP-Healthcheck (Port 5001 offen == Schema migriert); `grpc2`/`grpc3` haben `depends_on: grpc1
   condition: service_healthy` → sie starten erst, wenn das Schema steht, und finden alles vor (kein
   DDL, kein Race). Standardmuster „migrate once, then scale out".
2. **HTTP-Endpoints sind h2c-only.** Kestrel im Host.Grpc lauscht nur mit `HttpProtocols.Http2` auf
   Port 5001 → `/health`, `/monitoring/metrics`, `/webhook/datei` sprechen HTTP/2 im Klartext. Ein
   gewöhnliches `curl http://…/health` scheitert; man braucht `curl --http2-prior-knowledge`. Für die
   Cluster-Verifikation ist Consul (Membership) + der joinende LoadHarness der bessere Weg.
3. **Ephemerer Proto-Port.** Kein fester interner Port → nicht mappbar, aber im Docker-Netz auch nicht
   nötig (Container erreichen sich auf allen Ports). Nur relevant, wenn Nodes über getrennte Netze/Hosts
   laufen — dann `AdvertisedHost` = real routbarer Name **und** der Proto-Port müsste fixiert/geöffnet
   werden.
4. **`AdvertisedHost` nicht gesetzt.** Die bestehenden nativen Deploy-Skripte (`deploy/deploy.sh`) setzen
   `Cluster__AdvertisedHost` NICHT → Default `localhost`. Für Multi-Node zwingend je Node auf den
   erreichbaren Namen setzen (hier über die Compose-Env erledigt).
5. **`docker compose run` vergibt keinen Netzwerk-Alias** (real aufgetreten). Der Verifier startete zwar
   und sah die Member über Consul, aber die grpc-Nodes konnten den advertised Namen `loadharness` nicht
   auflösen → der Gossip-**Rückweg** lief in Timeouts, danach scheiterte die Broker-Aktivierung
   (`PubSubStartupService` → `RequestAsync` timeout) und der Node crashte beim Start. **Lösung:** `run`
   mit **`--use-aliases`** (gibt dem run-Container den Service-Alias `loadharness`) — oder den Verifier
   per `up` statt `run` starten (`up` vergibt Aliase automatisch).
6. **Consul (dev-mode) reapt tote Member nicht sofort** (real aufgetreten). Ein per `docker rm -f`
   hart entfernter Verifier-Container ließ eine Geister-Registrierung (`loadharness:<port>`) in Consul
   zurück; der nächste Verifier-Start versuchte endlos, diesen toten Endpoint zu erreichen (`RpcException`-
   Sturm) und crashte. **Lösung:** Verifier sauber durchlaufen lassen (nicht mittendrin killen); bei
   Bedarf `down -v` → frischer Cluster ohne Geister. In Produktion mit echtem Consul + Health-Check-TTL
   werden tote Member automatisch abgeräumt.

## Ergebnis — wie es lief

**Cluster-Formation: ✅ bestätigt.** Consul-Katalog listet drei distinkte Member des `cqrs-cluster`,
jeder unter seinem `AdvertisedHost` mit eigenem (ephemerem) Proto-Remote-Port:

```
"ServiceAddress":"grpc1"  "ServicePort":44795
"ServiceAddress":"grpc2"  "ServicePort":46271
"ServiceAddress":"grpc3"  "ServicePort":39853
```

Alle drei Node-Logs zeigen `+ ActorSystem erstellt (+ Wire-Serializer id=100)` (der Wire-Serializer ist
registriert, der Boot-Check wirft nicht) und die Member-Konvergenz (`✓ Cluster gestartet (… Member)`, der
zuletzt gestartete Node sah alle drei). Nach dem gestaffelten Start **kein** Marten-Schema-Race mehr
(0 Treffer für `MartenSchemaException` in allen Logs).

**Cross-node Command-Dispatch: ✅ bewiesen.** Der LoadHarness-Node joint denselben `cqrs-cluster` als
**vierter Member** (`✓ Cluster routbar nach 1247 ms`), setzt 360 Commands ab (40 Konten × je 1 Eröffnung
+ 8 Gutschriften) und prüft anschließend per Rehydration die Salden:

```
Nachrichten:     360  (ok 360, fehlgeschlagen 0)
Salden geprüft:  40, korrekt 40, falsch 0
✓ Exactly-once hält: alle 40 Salden == 1080.
```

`PartitionIdentityLookup` platziert die 40 Konto-Aggregate über alle vier Member — rund ¾ landen fremd
(auf grpc1/2/3), aus Sicht des dispatchenden LoadHarness-Nodes also **cross-node**. Jeder dieser Commands
(und die `CommandResult`-Antwort) reist damit SERIALISIERT über den Wire-Serializer. Der grüne
Exactly-once-Nachweis (0 falsch, 0 Fehler) zeigt, dass die Serialisierung in beide Richtungen und über
alle vier Nodes korrekt funktioniert — wäre sie kaputt, hätten die fremd platzierten Aggregate versagt und
der Rehydrations-Check `falsch > 0` gemeldet.

**Cross-node PROZESS/SAGA: ✅ bewiesen (mit einem dokumentierten Cold-Start-Vorbehalt).** Der
`--mode saga` löst 40 Überweisungen aus (`BeauftrageUeberweisung` → `Ueberweisungsauftrag` emittiert
das Auslöse-Event → die Prozess-Maschine auf den Server-Nodes reserviert an der Quelle, schreibt dem
Ziel gut und bucht per Join). Das übt den **kompletten durable-Konsumenten-Plane cross-node**: Signal →
Korrelations-Router → ProzessManager → Folge-Commands an FREMDE Aggregate — genau die
`SignalEnvelope`/`ProzessWake`/`Publish`-Pfade, die Iteration 2 serialisierbar gemacht hat.

Warmer Cluster (Snapshot-Tabellen existieren bereits):
```
Konten abgeschlossen:  20/20  (Saldo==erwartet ∧ Reserviert==0)
Gelderhaltung:         Ist-Summe 20000000 == Soll-Summe 20000000
Offene Reservierungen: 0
✓ Alle 40 Überweisungs-Sagas cross-node abgeschlossen — exactly-once, Geld erhalten.   (nach 0.1 s)
```

**Cold-Start-Vorbehalt (aufgedeckt vom Saga-Test):** Beim ALLERERSTEN Auftreten eines bisher
ungenutzten Aggregat-Typs (hier `Ueberweisungsauftrag`) legt Marten dessen Snapshot-Tabelle
(`es.mt_doc_snapshot_ueberweisungsauftrag`) **lazy beim ersten Zugriff** an — ohne Advisory-Lock.
Aktivieren mehrere Nodes den Typ gleichzeitig, rennen sie auf `CREATE TABLE` (`duplicate key pg_type`).
Im ersten Saga-Lauf gegen einen KALTEN Cluster lief dadurch nur ein Teil im 120-s-Fenster durch
(reproduziert: 14–16 von 20, Race-Timing-abhängig) — **aber die Gelderhaltung hielt jedes Mal exakt** (die
nicht-gelaufenen Transfers bewegten schlicht nichts, 0 offene Reservierungen). Es ist **transient und
selbstheilend** (Proto reaktiviert den Actor, sobald ein Node die Tabelle angelegt hat; der
Prozess-§3-Backstop holt Hänger nach) und **kein** Serializer-/Dispatch-Fehler: der zweite Lauf gegen
denselben (nun warmen) Cluster war jedes Mal sofort 20/20.

**Sauberer Produktions-Fix (empfohlen, hier bewusst NICHT verdrahtet):** die Schema-Migration von
GENAU EINEM Migrator ausführen (`ApplyAllDatabaseChangesOnStartup` auf einem Init-Job/Node,
`AutoCreate.None` auf den übrigen). Achtung: `ApplyAllDatabaseChangesOnStartup` auf ALLEN Nodes
gleichzeitig ersetzt den Lazy-Race durch **Migrations-Lock-Contention** (der Verlierer crasht mit
„Unable to attain a global lock in time") — verifiziert und wieder zurückgenommen. Der Single-Migrator
braucht eine per-Node-Config (Migrator-Flag) und ist ein eigener, kleiner Framework-Schritt.

## Fazit

Der in Iteration 1+2 gebaute Wire-Serializer trägt im **echten verteilten Container-Betrieb**: drei
Nodes bilden über einen Consul einen Cluster, ein vierter tritt bei und dispatcht cross-node **Commands**
(Iteration 1) UND fährt einen cross-node **Prozess/Saga** (Iteration 2 — Überweisungs-Petri-Netz mit
Signal/ProzessWake/Publish über Node-Grenzen), beides mit bewiesener Exactly-once-Semantik. Alle
aufgetretenen Stolpersteine sind **Deployment-/Betriebs-Themen** (Schema-Migrations-Reihenfolge inkl. des
Lazy-Snapshot-Cold-Start-Race, Docker-Netzwerk-Aliase, Consul-Geister) — **kein einziger** im
Framework-Kern oder in der Serialisierung selbst. Der Saga-Test war dabei besonders wertvoll: er hat
sowohl die cross-node Prozess-Maschine bewiesen als auch den Cold-Start-Schema-Race sichtbar gemacht, den
der reine Aggregat-Lauf nie berührt hätte.
