# Handoff: Multi-Node Cold-Start — Single-Migrator für Marten-Schema

> **Status:** offen / zu implementieren. Aufgedeckt vom cross-node Saga-Test
> (`LoadHarness --mode saga`, siehe `docs/multi-node-deployment.md`). **Kein** Serializer-/
> Dispatch-/Framework-Kern-Fehler — ein reines Schema-Migrations-Deployment-Thema.

## Das Problem (präzise)

Beim ersten Aktivieren eines **bisher ungenutzten Aggregat-Typs** in einem Multi-Node-Cluster
legt Marten dessen Snapshot-Tabelle (`es.mt_doc_snapshot_<typ>`) **lazy beim ersten Zugriff** an
(`ensureStorageExistsAsync`, `AutoCreate.CreateOrUpdate`) — **ohne Advisory-Lock**. Aktivieren
mehrere Nodes denselben Typ gleichzeitig (z. B. weil `PartitionIdentityLookup` mehrere neue
`Ueberweisungsauftrag`-Identitäten parallel auf verschiedene Nodes platziert), rennen sie auf

```
CREATE TABLE es.mt_doc_snapshot_ueberweisungsauftrag …   → 23505 duplicate key "pg_type_typname_nsp_index"
```

**Beobachtetes Symptom (Saga-Cold-Start):** erster Lauf gegen einen kalten Cluster nur 14–16 von
20 Sagas im 120-s-Fenster fertig. **Wichtig:** die Gelderhaltung hält jedes Mal exakt (die
nicht-gelaufenen Transfers bewegen nichts, 0 offene Reservierungen), es ist **transient und
selbstheilend** (Proto reaktiviert den Actor, sobald ein Node die Tabelle angelegt hat; der
Prozess-§3-Backstop holt Hänger nach). Warmer Cluster = sofort 20/20. Also kein Korrektheits-,
sondern ein **Cold-Start-Latenz/Robustheits-Thema**.

Betroffen sind alle Aggregat-Typen mit registrierter Snapshot-Doc, die erst zur Laufzeit das
erste Mal aktiviert werden — die Event-Tabellen selbst deckt der bestehende gestaffelte Start
(grpc1-Healthcheck) bereits ab, die **lazy** angelegten Snapshot-Tabellen nicht.

## Warum die naheliegenden Fixes NICHT funktionieren (beide verifiziert)

1. **Nichts tun / lazy lassen:** der Cold-Start-Race bleibt (14–16/20 beim ersten Lauf).
2. **`ApplyAllDatabaseChangesOnStartup()` auf ALLEN Nodes:** ersetzt den Lazy-Race durch
   **Migrations-Lock-Contention** — Marten nimmt dafür einen globalen Advisory-Lock; starten
   mehrere Nodes gleichzeitig, crasht der Verlierer hart mit
   `InvalidOperationException: Unable to attain a global lock in time order to apply database changes`.
   Zusätzlich feuert der Port-Healthcheck, BEVOR die Migration fertig ist (Kestrel öffnet den Port,
   dann läuft der Marten-Activator) → die Staffelung greift nicht. **Wurde probiert und wieder
   zurückgenommen** (Commit-Historie „Prozess/Saga verifiziert").

## Die Lösung: genau EIN Migrator

Standardmuster für Schema-Migration in Cluster-Deployments: **migrate once, then scale out.**
Ein einzelner Migrator legt ALLE Schema-Objekte (inkl. aller Snapshot-Tabellen) EAGER und
lock-gesichert an; die übrigen Nodes migrieren NICHT, sondern nutzen das fertige Schema.

**Zu bauen:**

1. **Per-Node-Config-Flag** (z. B. `Cluster__Role=migrator|member` oder `Marten__ApplyChanges=true|false`),
   gelesen in `CqrsFrameworkBuilder`/`AddCqrsFramework`.
2. **Marten-Konfig konditional** (`Infrastructure/Extensions/CqrsServiceExtension.cs`, die
   `services.AddMarten(...)`-Kette ~Z. 103–169):
   - Migrator: `AutoCreate.CreateOrUpdate` + `.ApplyAllDatabaseChangesOnStartup()`.
   - Member: `AutoCreate.None` (kein Runtime-Lazy-Create mehr → kein Race; setzt voraus, dass der
     Migrator vorher durch ist).
   - Achtung `RegisteredSnapshotTypes.Register(options)` muss auf BEIDEN Pfaden laufen, damit die
     Member die Typen kennen (nur eben nicht anlegen).
3. **Compose-Verdrahtung** (`deploy-multinode/docker-compose.yml`): entweder
   - (a) ein dediziertes **`migrate`-Init-Service** (dasselbe Image, `Cluster__Role=migrator`, läuft,
     migriert, exitet), auf dessen erfolgreichen Abschluss grpc1/2/3 per
     `depends_on: { migrate: { condition: service_completed_successfully } }` warten und alle als
     `member` (`AutoCreate.None`) starten; ODER
   - (b) grpc1 = `migrator`, grpc2/grpc3 = `member` mit `depends_on: grpc1 healthy` — ABER dann muss
     grpc1s Healthcheck den Migrations-Abschluss widerspiegeln (nicht nur „Port offen"), sonst
     starten die Member zu früh. Variante (a) ist sauberer.
4. **Doku** `docs/multi-node-deployment.md`: den Cold-Start-Vorbehalt durch die neue Anleitung
   ersetzen; den „ApplyAll-auf-allen crasht"-Hinweis als erledigt markieren.

## Akzeptanzkriterium

Der Saga-**Cold-Start** (frischer Cluster, `down -v`, erster Lauf) muss **20/20** liefern:

```bash
docker compose -f deploy-multinode/docker-compose.yml down -v
docker compose -f deploy-multinode/docker-compose.yml up -d --build      # + Migrator-Schritt
# nach Konvergenz, ERSTER Lauf:
docker compose -f deploy-multinode/docker-compose.yml --profile verify run --rm --use-aliases \
  loadharness --mode saga --accounts 20 --transfers 40 --concurrency 16 --log warning
#   → "✓ Alle 40 Überweisungs-Sagas cross-node abgeschlossen — exactly-once, Geld erhalten."
```

Zusätzlich **keine Regression**: Prüfstand 112/112, Integration grün (der `SnapshotLiveE2ETests`-
Consul-Cold-Boot-Flake ist bekannt/umgebungsbedingt, siehe CLAUDE.md), Host.Grpc baut.

## Schlüsseldateien

- `Infrastructure/Extensions/CqrsServiceExtension.cs` — `AddMarten`-Kette (Z. 103–169), Kommentar zum
  Race steht dort bereits.
- `Infrastructure/Persistence/RegisteredSnapshotTypes.cs` (generierte Snapshot-Typ-Registrierung).
- `Infrastructure/Persistence/MartenSnapshotStore.cs` — lazy-Load-Pfad (der die Tabelle triggert).
- `deploy-multinode/docker-compose.yml`, `deploy-multinode/Dockerfile`.
- `docs/multi-node-deployment.md` (Stolpersteine + Ergebnis), dieses Handoff.

## Kontext / Nicht-Ziele

- **Nicht** die Serialisierung/Iteration 1+2 anfassen — die ist fertig und cross-node bewiesen
  (`multi-node-iter1-wire-serializer` Memory, `docs/backend-neubau-fahrplan.md` Phase 7/8).
- Der Fix ist bewusst klein und deployment-nah; er ändert keine Domänen-/Prozess-Logik.
