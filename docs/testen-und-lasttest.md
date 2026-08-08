# Testen & Lasttest

> Für Menschen **und Agenten**: wie diese Codebasis getestet wird, was wo abgedeckt ist,
> welche Fallstricke real sind und wie der Last-Harness läuft. Kurz halten, Volltext-Kontext
> steht im Code und in den anderen `docs/`.

## TL;DR

| Ebene | Womit | Braucht | Zweck |
|---|---|---|---|
| **Prüfstand** | `Infrastructure.Pruefstand.Tests` | nichts (in-memory) | gesamte Logik: Command→Event, Exactly-once, Crash-Proben, Sagas, Prozesse |
| **Integration** | `Infrastructure.Integration.Tests` | Postgres+Consul+Redis | echte Store-Transaktionen, Signal-Zustellung, Live-E2E |
| **Last** | `LoadHarness/` (Console-App) | Postgres+Consul+Redis | Durchsatz/Latenz + Exactly-once unter Dauerlast |

```bash
# 1. Infrastruktur hochfahren (einmalig)
docker compose -f deploy-linux/docker-compose.infrastructure.yml up -d

# 2. Schnell & immer grün — die Logik
dotnet test Infrastructure.Pruefstand.Tests/Infrastructure.Pruefstand.Tests.csproj

# 3. Gegen echte Infrastruktur (sequentiell, siehe Fallstrick unten)
dotnet test Infrastructure.Integration.Tests/Infrastructure.Integration.Tests.csproj

# 4. Lasttest / Durchsatz (bootet EINEN Cluster, kein Flake)
dotnet run --project LoadHarness -- --accounts 500 --credits 40 --concurrency 128 --log warning
```

## Voraussetzung: Infrastruktur

Postgres, Consul (dev), Redis laufen als Docker-Container:

```bash
docker compose -f deploy-linux/docker-compose.infrastructure.yml up -d
```

Ports (localhost): Postgres `5432`, Redis `6379`, Consul `8500`. Die Defaults in
`AddCqrsFramework` (siehe `Infrastructure/Extensions/CqrsServiceExtension.cs`,
`CqrsFrameworkBuilder`) zeigen genau dorthin — Tests und Harness brauchen also keine
Connection-Strings.

Nur der **Prüfstand** braucht das alles NICHT (reine In-Memory-Fakes).

## Ebene 1 — Prüfstand (in-memory, immer grün)

`Infrastructure.Pruefstand.Tests` fährt einen **In-Memory-Fake-Cluster** und deckt die
gesamte Logik ab: die 7.3-Adapterschleife, Exactly-once (Co-Commit vs. getrennte Marke),
Crash-Proben (definierte Absturzpunkte), Reaktionen, und die komplette Prozess-/Saga-Schicht
(Happy Path, Join, Zweig-Kompensation) — ohne Broker, ohne DB.

```bash
dotnet test Infrastructure.Pruefstand.Tests/Infrastructure.Pruefstand.Tests.csproj
```

**Das ist die erste Anlaufstelle.** Wer Logik ändert, muss hier grün sein (aktuell 68 Tests).
Verteilte/Actor-Hangs IMMER hier (bzw. mit Fake-Cluster) diagnostizieren, nie im langsamen
Integrationstest — siehe Fallstrick „xUnit schluckt Logs".

## Ebene 2 — Integration (echte Infrastruktur)

`Infrastructure.Integration.Tests` prüft, was der Prüfstand bewusst offenlässt: die echten
Store-Transaktionen (Co-Commit gegen Postgres), Signal-Zustellung über einen echten
Consul-Cluster, und Live-E2E (Command → Event → Reaktion/Saga).

```bash
dotnet test Infrastructure.Integration.Tests/Infrastructure.Integration.Tests.csproj
```

### ⚠ Fallstrick A — sequentiell laufen lassen
`xunit.runner.json` schaltet die Parallelisierung AB. Grund: jede Testklasse fährt einen
**echten Consul-Cluster** hoch; parallel konkurrieren mehrere um Postgres/Consul/Redis und
erzeugen Timing-Flakiness. **Nicht** auf parallel umstellen.

### ⚠ Fallstrick B — der Cold-Boot-Flake (bekannt, dokumentiert)
`SnapshotLiveE2ETests` flaked gelegentlich **bimodal: ~1s grün ODER Hang bis Timeout** — auch
isoliert. Ursache ist NICHT der Test-Code und NICHT die Command-Verarbeitung, sondern die
**Consul-Cluster-Formation / erste Actor-Aktivierung beim Cold-Boot**, die manchmal nicht
rechtzeitig konvergiert. Beleg: der Fehler trifft immer den Schritt, der *persistierte Events*
prüft (Append) — das liegt VOR allem Publish/Logging.

**Nicht an den `WaitAsync`-Timeouts drehen** — ein Boot-Hang lässt sich nicht „härten"
(30→60s half nicht). Der Flake ist ein **Test-Muster-Artefakt** („frischer Cluster pro Test +
sofort dispatchen"); in **Produktion** existiert es nicht (der Host bootet EINMAL). Details:
`memory/snapshot-e2e-flake-clusterboot.md`.

Angewandte Härtung (Robustheit, eliminiert den Flake aber nicht):
- `ProtoActorAggregateDispatcher`: bounded Token + Retry + **lauter** Error-Log statt stillem
  Endlos-Hang (früher `CancellationToken.None` → unendliche stille Retry-Schleife).
- `ClusterStartupService`: der 30s-Start-Timeout ist jetzt via `Task.WhenAny` wirklich erzwungen.

### ⚠ Fallstrick C — xUnit schluckt Logs
`dotnet test`-stdout zeigt **kein App-Logging** (weder `Console.WriteLine` noch `ILogger`).
Zum Diagnostizieren von Cluster-/Consul-Verhalten NICHT den Integrationstest nehmen —
nimm den **Last-Harness** mit `--log debug` (der druckt Proto/Consul-Logs sichtbar).

## Ebene 3 — Last-Harness (`LoadHarness/`)

Eigenständige Console-App (bewusst NICHT in `CqrsSolution.sln`). Sie bootet den Cluster
**einmal** (wie Produktion), wartet über eine **Readiness-Barriere** bis er routbar ist
(damit kein Cold-Boot-Flake), treibt dann Last und misst Durchsatz + Latenz. Zwei Modi:

### Modus `aggregate` (Default) — Schreibpfad MIT Persistenz
Commands über die Konto-Domäne (Command → Event → Marten-Append). Prüft zum Schluss per
Rehydration jeden Saldo → **Exactly-once-Beweis unter Last**.

```bash
dotnet run --project LoadHarness -- --mode aggregate --accounts 500 --credits 40 --concurrency 128 --log warning
```
Gesamtzahl Commands = `accounts × (1 + credits)`. Erwarteter Saldo = `1000 + credits × 10`.
Exit `0` nur bei 0 Fehlern UND allen Salden korrekt.

### Modus `pipeline` — eingehende Trigger OHNE Persistenz
Schickt viele `BenchPing`-Trigger an die **No-Op-Benchmark-Pipeline**
(`Domain.Pipeline/Benchmark/BenchmarkPipeline.cs` — Handler = `Task.CompletedTask`, yieldet
kein Command ⇒ kein Aggregate-Send ⇒ kein Event-Store-Append). Misst den reinen
Trigger→Ack-Pfad.

```bash
dotnet run --project LoadHarness -- --mode pipeline --messages 200000 --concurrency 128 --log warning
```
> ⚠ **Eine Pipeline = EIN Actor = serielle Mailbox.** Gemessen wird der Durchsatz EINER
> Pipeline; Sender-Concurrency erhöht nur die In-Flight-Requests, nicht die Actor-Parallelität.

### Parameter
| Flag | Default | Modus | Bedeutung |
|---|---|---|---|
| `--mode` | aggregate | — | `aggregate` (Persistenz) oder `pipeline` (No-Op-Trigger) |
| `--accounts` | 200 | aggregate | Anzahl Konto-Streams |
| `--credits` | 20 | aggregate | Gutschriften pro Konto |
| `--messages` | 50000 | pipeline | Anzahl Trigger |
| `--concurrency` | 64 | beide | gleichzeitig in-flight Requests |
| `--log` | warning | beide | `warning` = saubere Messung; `debug` = Cluster/Consul-Logs sichtbar (Diagnose) |

### Referenzwerte (lokaler Dev-Rechner, Single-Node, in-process)
| Modus | Last | Durchsatz | p50 / p99 | Persistenz |
|---|---|---|---|---|
| aggregate | 4.200 Cmd | ~4.000/s | 13 / 80 ms | ja (Append je Command) |
| aggregate | 20.500 Cmd | ~4.600/s | 26 / 51 ms | ja |
| **pipeline** | 200.000 Trigger | **~400–500k/s** | **0,1 / 1 ms** | **nein** |

**Einordnung:**
- Der **aggregate**-Durchsatz ist im Wesentlichen **DB-gebunden** (ein Marten/Postgres-Append
  pro Command, Single-Writer pro Stream).
- Der **pipeline**-Durchsatz (~100× höher) zeigt: der **Framework-/Actor-Transport selbst ist
  NICHT der Flaschenhals** — ohne Persistenz verarbeitet ein einzelner Actor hunderttausende
  Nachrichten/s. Aber: **in-process** (RequestAsync ist lokal, keine Netzwerk-Serialisierung)
  und **No-Op-Handler** (echte Handler-Arbeit senkt die Zahl). Cross-node läge deutlich tiefer.
- Zahlen sind lokal/Single-Node — **keine Produktions-Benchmarks**, nur Größenordnungen.

### Warum der Harness auch die Logger-Arbeit validiert
Bei `--log warning` ist der per-Command-Logpfad komplett still. Der gemessene Durchsatz ist
nur deshalb echt, weil das synchrone `Console.WriteLine` aus dem Hot-Path entfernt und durch
level-gegatetes `ILogger` ersetzt wurde (Actor/Broker/gRPC-Service). Mit dem alten Console-I/O
pro Command/Event/Publish bräche der Durchsatz ein.

## Wann was nehmen (Agenten-Kurzregel)

- **Logik geändert** → Prüfstand. Muss grün sein.
- **Store/Transaktion/Signal/E2E geändert** → Integration (sequentiell). `SnapshotLiveE2ETests`-Flake
  ist bekannt und Cold-Boot-bedingt — nicht jagen, isoliert nachlaufen lassen.
- **Performance / Durchsatz / „skaliert das?"** → Last-Harness. Bootet einen Cluster, kein Flake.
- **Cluster/Consul-Hang verstehen** → Last-Harness mit `--log debug` (NICHT Integrationstest — xUnit
  schluckt die Logs).

## Verwandte Dokumente / Gedächtnis
- `memory/snapshot-e2e-flake-clusterboot.md` — der Cold-Boot-Flake im Detail + angewandte Fixes.
- `memory/hang-diagnose-in-memory.md` — verteilte Hangs in-memory diagnostizieren, nicht im Integrationstest.
- `CLAUDE.md` — Projektgedächtnis, Invarianten, Phasenstand.
