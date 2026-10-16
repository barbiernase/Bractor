# 12 — Tests & Vermessung

**Die eine Quelle für Zahlen und Messwerte.** Andere Dokumente verweisen hierher. Offene Testlücken stehen als Befund in
`docs/13-reifegrad-schulden-bewertung.md` (T-1, P2-5). Stand: 2026-10-15.

## 12.1 Die Test-Ebenen

Die Teststrategie folgt den Invarianten „die Wahrheit ist der Log“ und „nie faken, was man nicht besitzt“.

| Ebene | Projekt | Läuft gegen | Testet | Parallel? |
|---|---|---|---|---|
| 1 — Prüfstand | `Infrastructure.Pruefstand.Tests` | in-memory, **store-frei** | Decider/Applier, Fold, Routing, Signale, Wire, Analyzer und Generatoren (über eine eigene `CSharpCompilation`), Extractor/Gesten/Sicht, Funktionen, Akteure, Quellen-Treiber, Umgebung, Spur | ja |
| 2 — Integration | `Infrastructure.Integration.Tests` | **echtes** Marten/Postgres, Consul, Redis, Kestrel | Store-Semantik und Ende-zu-Ende-Verhalten im Cluster (Beispielanwendung) | **nein** (sequentiell erzwungen) |
| 2 — Prüfdomäne | `Pruefdomaene.Integration.Tests` | **echtes** Marten/Postgres, Consul, Redis; eigene Schemata `pruef_*` | Framework-Verhalten domänenneutral an der zweiten Anwendung `Pruefdomaene`: Prozess-Diamant, Kompensation, Fan-out + Count-Join, Snapshot, Upcasting, Marking-Cursor-Äquivalenz | **nein** (sequentiell erzwungen; nie gleichzeitig mit der anderen Integrations-Suite) |
| Client (C#) | `Client.Infrastructure.CollectionTests` | in-memory | `VirtualCollection`, `PendingCommandTracker` | ja |
| Client (Python) | `Client.Infrastructure.Python/tests`, `Domain.Client.Worker.Python.ML/tests` | pytest | SDK (Vertrag, Funktion, Query, Reconnect, Mapper), Worker (Verträge, Funktionen, generischer Worker) | ja |
| Werkbank-Abnahme | `Werkbank.Abnahme.Tests` | **echte** Werkbank (Prozess) auf einer Kopie des Arbeitsbaums, echter Host in `sim`, eigene Sim-DB, Python-Worker | ⟳ (alle Schritte, Stände), JSON-Eingang als Akteur, Projektion, Spur, „▶ Worker“ bis zum Fakt, Geste/↶ byte-gleich, Daten-Prüfung meldet ein entferntes Feld | **nein** (sequentiell; nie gleichzeitig mit den anderen Integrations-Suiten) |
| Werkbank (Oberfläche) | `Werkbank` + Browser | echter Host gegen die Sim-DB | Sichtprüfung im Browser (Board, Panels, Spur-Überlagerung) — die Abläufe prüft die Abnahme oben | n/a |
| Werkzeug-Gates | `GraphExtractor -- --check`, `-- --sonde` | Solution bzw. fremde Domäne im Speicher | Code ⇄ Karten, Fixpunkt, Wirker, Routing; Agnostik | n/a |
| Gate frischer Checkout | `./pruefe-checkout.sh` | der Commit `HEAD` in einem temporären `git worktree` | ignorierte Quellen, Build, Codegen-Drift, Prüfstand, Build verändert den Checkout nicht | n/a |
| Garantie-Suite | `--filter Kategorie=Garantie` auf Prüfstand, Integration, Prüfdomäne | wie die jeweilige Ebene | jede Garantie aus [11](11-garantien.md) unter Störung; offene Lücken als Skip-Tests (`G-n offen (docs/13)`) | je Ebene wie oben |
| E2E-Vermessung | `scripts/vermessung-e2e.sh` (Treiber `Cqrs.Vermessung`) | echte Hosts (`Host.Grpc`, `Host.Pruefdomaene`, Release) in den Umgebungen `vermessung` bzw. `vermessung-pruefdomaene`, eigene DBs, Python-Worker | Höchstlast mit 1/8/64 Sendern: Durchsatz, Rückstand, Latenz, Last je Vorgang; Exactly-once nach jedem Lauf; Störungslauf (§12.5) | **nein** (nie gleichzeitig mit einer Integrations-Suite oder der Werkbank-Abnahme) |

**Prinzip:** Ebene 1 testet nur, was ohne Store deterministisch entscheidbar ist. Store-Semantik (Co-Commit-Atomarität, `seq_id`-Lücken, geteilte
Dokumente, Metadaten) wird **ausschließlich** gegen echtes Marten geprüft. Es gibt bewusst **keinen `InMemoryEventStore`**; In-Memory-Doubles gibt
es nur für nicht-autoritative Randstores (Inv. 6).

## 12.2 Zahlen

**Aktuell (2026-10-15, Phase 3 des Plans Garantien und Durchsatz):** Prüfstand **582 grün + 1 übersprungen** (Lücke G-5),
Integration **49 + 0** sequentiell (≈ 105 s), Prüfdomäne **23 + 1** (G-2) sequentiell (≈ 100 s); Garantie-Suite: Prüfstand 159 + 1,
Integration 31 + 0, Prüfdomäne 18 + 1;
Werkbank-Abnahme **1/1** sequentiell (≈ 110 s), `Client.Infrastructure.CollectionTests` **30/30**, Python SDK **34/34**, Worker **21/21**;
`--check` grün (alle Bereiche inkl. „Lage“), `--sonde` grün (111 Soll-Fakten, Lage-Soll 56 Typen); Gate `./pruefe-checkout.sh` grün.

Statisch gezählt (2026-10-15):

| Projekt | `[Fact]` | `[Theory]` | Dateien |
|---|---:|---:|---:|
| Infrastructure.Pruefstand.Tests | 551 | 8 | 86 |
| Infrastructure.Integration.Tests | 49 | 0 | 22 |
| Pruefdomaene.Integration.Tests | 24 | 0 | 9 |
| Client.Infrastructure.CollectionTests | 31 | 0 | 2 |
| Werkbank.Abnahme.Tests | 1 | 0 | 2 |

Gezählt: Zeilen `[Fact]`/`[Fact(` bzw. `[Theory]`, Dateien = alle `.cs` des Projekts. Die Differenz zwischen 559 Methoden und 583 Läufen im
Prüfstand kommt von den expandierten `[Theory]`-Fällen.

## 12.3 Was getestet ist

**Prüfstand (Ebene 1), nach Bereich (Tests):**

| Bereich | Tests | Klassen |
|---|---:|---|
| Analyzer | 85 | `AkteurAnalyzerTests` (36), `FlussAnalyzerTests`, `FaehigkeitAnalyzerTests`, `GesamtbildAnalyzerTests`, `ModellAnalyzerTests`, `StoreZugriffAnalyzerTests`, `AusgabeVertragAnalyzerTests`, `QuellenTypAnalyzerTests`, `CommandEmitAnalyzerTests` |
| Editor-Modell, Scaffolder | 73 | `DomainEditorLeseseiteTests`, `DomainEditorAkteurTests`, `DomainEditorScaffolderTests`, `DomainEditorFunktionTests`, `PythonQuelleTests`, `DomainEditorFlussTests`, `DomainEditorKompositionTests` |
| Funktionen und Flüsse | 69 | `FunktionsAufrufTests`, `PipelineFlussTests`, `FunktionsGeneratorTests`, `FunktionsAnbieterSitzungTests`, `FunktionsVermittlungTests`, `FlussLaufwerkTests`, `BildeingangTests`, `UmgezogeneFluesseTests` |
| Akteure, Verträge, Clients | 49 | `AkteurRechteTests`, `ClientVertragTests`, `AkteurVertragTests` (Hash Python = Server), `ClientCodeVertragTests`, `AkteurDienstTests` |
| Extractor, Gesten, Sicht | 47 | `GestenTests` (30), `SichtTests`, `LesenToleranzTests`, `UsingsTests` |
| Schreibpfad, Signale, Serialisierung | 24 | `SignalTypeGeneratorTests`, `SignalEmitTests`, `SignalWireRoundTripTests`, `EventJsonSerializationTests`, `EventVersioningTests`, `AppendBatchingTests`, `DtoMapperRoundTripTests` |
| Beispiel-Aggregate | 39 | `DatensatzAggregatTests`, `TrainingslaufAggregatTests`, `SammelvorgangBarriereTests`, `SammelvorgangKaskadeTests`, `BoundedInboxTests` |
| Quellen, Umgebung, Spur, Dienste | 65 | `DateiTreiberTests` (17), `UmgebungLaderTests`, `ProjektionsStandTests`, `SpurTests`, `WirkerIdTests`, `SpurWegTests`, `DienstBindungTests` (15) |
| Konsum-Maschine, Prozess | 37 | `EmittentenCursorTests`, `ProjectionRebuilderTests` (Neuaufbau über Partitionen als Strom), `BatchLeserTests` (Position über fremde Events, Typfilter), `EmittentenPfadAdapterTests`, `GaEinsPruefungTests`, `ProzessPollFilterTests`, `AblehnungsMarkeTests`, `ReaktionsIdTests`, `CommandVorpruefungTests`, `DispatcherCommandFailedTests`, `TransientPersistiertSplitTests`, `DeadLetterReadStoreTests` |
| Übrige | 26 | `PassivierungTests` (Leerlauf stoppt, Nachricht schiebt hinaus, 0 = nie; Sammlung „Zeitkritisch“, nie parallel), `FristSchedulerTests`, `FristPlanerTests`, `BackendMonitoringTests`, `EingangJsonTests`, `BrokerIdentityTests`, Handle-Welt: `PipelineEventPullBridgeTests`, `TimerTriggerTests`, `WebhookTriggerTests` |
| Garantien (Störung über Seams, `Garantien/`) | 33 + 1 übersprungen | `BatchKonsumGarantieTests` (doppelte/ungeordnete Seiten und Nachzügler, volle Seiten, Absturz mitten im Batch, zwei Aktivierungen, Poison parken und nachholen, Konflikt trennen, dauerhafter Konflikt hält die Position; Seams `PartitionsLog`, `SpeicherTracker` in `BatchKonsum/`), `EmitGarantieTests` (Emit-Ausgang je Antwort, Drossel je Ziel, volle Drossel meldet unbekannt, Dirigent: Backoff nach unbekanntem Ausgang, kein Doppel-Send, sofortige Weckung nach entschiedenem), `FristGarantieTests` (zugestellt, abgelehnt, Emit-Timeout und -Fehlschlag lassen die Frist liegen, doppeltes Feuern), `KonsumGarantieTests` (doppelte/ungeordnete Weckungen, Absturz zwischen Emit und Cursor, nicht zugestellter Emit hält den Cursor, Ablehnung ist entschieden, Commands eines Tails parallel), `RehydrationGarantieTests` (Neustart stellt die Inbox her, Split-Upcast Skip G-5), `BuildGarantieTests` (CQRS010/011/052/053, Wire-Boot-Check) |

**Integration (Ebene 2):**

| Klasse | Tests | Beweisgegenstand |
|---|---:|---|
| `CoCommitPostgresTests` | 3 | Batch-Co-Commit: Effekte vieler Streams, Guards und Partitions-Checkpoint in einer Marten-Session (ein Commit je Batch); Absturz mitten im Batch → nichts gültig, Wiederholung genau einmal |
| `GeteilteDokumentePostgresTests`, `SchreibRegelPostgresTests` | 4 + 5 | Single-Writer je Dokument, OCC bei geteilten Dokumenten zwischen zwei bzw. vier Partitions-Lesern, Trennen und Wiederholung |
| `SequenceGapPostgresTests` | 3 | `seq_id`-Lücke + Straggler-Grace; der Batch-Leser überholt einen Nachzügler nicht und wendet jedes Event genau einmal an |
| `MetadataPostgresTests` | 3 | Header-Readback |
| `DeadlineStorePostgresTests` | 2 | durabler Fristplan |
| `PollerBackstopPostgresTests` | 2 | Poll-Cursor der Emittenten heilt verlorene Signale; der Batch-Leser holt ein Event mit verlorenem Signal beim nächsten Poll nach (auch eines, das während des Durchlaufs kam) |
| `LiveCommandE2ETests` | 1 | Command → Event → Projektion im Cluster |
| `TwoNodeCommandDispatchTests`, `RemotePidDeliverySmokeTests`, `SignalDeliveryClusterTests` | 3 | Cross-Node-Serialisierung, Remote-PID, Signal-Zustellung |
| `PipelineFlussE2ETests` | 5 | Fluss gegen den echten Cluster: Quelle → Dirigent → Vermittler → Abholer → Fakt, auch mit Python-Worker über gRPC; Warten + Backstop; Lese-Fähigkeit |
| `VertragsZusageE2ETests` | 1 | doppelt zugestellte Zusage wirkt genau einmal |
| `UmgebungE2ETests`, `LeseseitenStandE2ETests`, `EingangE2ETests` | 3 + 1 + 1 | Siegel, Projektions-Stand nach Code-Änderung, JSON-Eingang/Webhook |
| `MonitoringHttpTests` | 2 | `/health`, Kennzahlen |
| `Garantien/GarantiePostgresTests` | 7 | ein Append ganz oder gar nicht, zwei Schreiber ohne verlorenes Update, Poison-Stream hält die HWM des Emittenten-Polls ohne andere zu blockieren, ein gescheiterter Append (Tombstones) legt den Poll nicht still; Batch-Konsum: zwei Aktivierungen einer Partition wenden nichts doppelt an (G-4), ein Poison-Stream wird geparkt und holt nach, ein Neuaufbau nimmt einem laufenden Leser den Commit |
| `EingangE2ETests.Client_wiederholt_mit_derselben_CommandId…` | 1 | Client-Wiederholung über den JSON-Eingang wirkt genau einmal |
| Handle-Welt: `PipelineWebhookHttpTests`, `TimerTriggerSchedulingTests` | 2 | Trigger-Pfade ohne Domänen-Nutzer (`docs/13` W11-1) |

**Prüfdomäne (Ebene 2, `Pruefdomaene.Integration.Tests`):**

| Klasse | Tests | Beweisgegenstand |
|---|---:|---|
| `ProzessE2ETests` | 4 | im Cluster: Diamant (Fork + `Und`-Join), Kompensation (ein Zweig abgelehnt → `RückgängigDurch`, `ProzessBeendet(false)`), fünf nebenläufige Vorgänge je genau einmal, `SendeJe`-Fan-out + `UndAlle`-Count-Join |
| `SnapshotTests` | 3 | `MartenSnapshotStore` über die generierte Registrierung (Roundtrip samt Inboxen, Überschreiben, Löschen); Actor über der Schwelle → Snapshot, Rehydration seedet daraus (Snapshot + Tail == Voll-Fold), fremde Schema-Version → Voll-Replay |
| `UpcastingTests` | 5 | generierte Tabelle (Diskriminator `_v2`, Kette, unbekannt → laut); echter Seam: alte Bytes werden beim Lesen aufgewertet, neue Writes tragen `_v2` |
| `MarkingCursorAequivalenzTests` | 4 | `ProzessManager` direkt gegen Marten, Cursor aus vs. an (`MartenProzessMarkingStore`): identische Feuerungen für Diamant, Kompensation, Fan-out + Count-Join (Cursor liest weniger); falscher RegelHash → Voll-Fold |
| `Garantien/SchreibGarantieTests` | 3 + 1 übersprungen | doppelt und gleichzeitig zugestellter Command wirkt genau einmal; Heilung nach Versionskonflikt durch einen zweiten Schreiber, mehrdeutiger Commit wird als Erfolg quittiert und wirkt genau einmal; alte CommandId jenseits der Inbox-Grenze (Skip G-2) |
| `Garantien/LebenszyklusGarantieTests` | 1 | Passivierung (1 s Leerlauf, Snapshot beim Stoppen) und Reaktivierung: derselbe Stand, dieselbe Inbox, derselbe Cursor der Reaktion |
| `Garantien/AblaufGarantieTests` | 3 | die Reaktion `NachschubBeiMindestbestand` wirkt genau einmal über Signal und Poll (beide ab Cursor) und nach einem Absturz zwischen Emit und Cursor (Cursor-Dekorator, erneutes Senden wird verworfen); Neustart des Hosts mitten im Vorgang führt ihn zu Ende (eigenes Schema) |

**Client:** `VirtualCollectionTests` (26), `PendingCommandTrackerTests` (5). **Python:** SDK `test_vertrag`, `test_funktion`, `test_client_query`,
`test_client_reconnect`, `test_mapper_*` (inkl. Nutzlast-Tabelle); Worker `test_vertraege`, `test_funktionen`, `test_funktions_worker`, `test_training_worker`.

## 12.4 Was nicht getestet ist

- **Prozess-Verkettung** (ein Prozess-Ergebnis löst den nächsten aus) und Zeitlimits im Prozess — die Prüfdomäne deckt Diamant, Kompensation,
  Nebenläufigkeit, Fan-out und Count-Join ab, Verkettung noch nicht.
- **`KlärungNötig`-Pfad** und der Kompensations-Warmpfad (`NächsteKompensationAsync` ab 0).
- **`SendeJe`, Count-Join in der Sonde** — getestet (Prüfdomäne), aber nicht von `--sonde` abgedeckt (`docs/13` WZ-1).
- **Paralleler Drain / Schreibpfad-Skalierung** — kein Assertion-Test (früher nur im LoadHarness beobachtet).
- **Shard-Rebalance / Node-Ausfall** — nur simuliert.
- **Blazor-Client** jenseits `VirtualCollection`/`PendingCommandTracker` (Bus, Store, Transport).

## 12.5 Messwerte

(NEU)

**Als korrekt verifiziert (Backend-Audit 2026-08, nicht erneut jagen):** Versionsarithmetik inkl. Noop- und Ablehnungs-Marken; OCC-Skip auf dem
`AnyVersion`-Pfad; Append vor State-Mutation; Shard-Hash-Konsistenz Subscribe ↔ gezieltes Publish; Pre-Dispatch-Guard des Adapters je Stream (heute `EmittentenAdapter`);
Signal→Wake-Mapping; Kompensations-Dedup und Count-Join ohne verfrühtes Feuern; Snapshot-Schema-Gating; Metadaten-Readback.

## 12.6 Wie man einen Test schreibt

- **Prüfstand:** ein Aggregat über die generierte Fabrik treiben, FluentAssertions, Methodennamen `Deutsch_mit_Unterstrich`. Muster:
  `Ddd/DatensatzAggregatTests.cs`, `Aggregate/SammelvorgangBarriereTests.cs`. Analyzer und Generatoren über eine eigene `CSharpCompilation`
  (Muster: `Analyzers/*Tests.cs`). Gesten gegen eine Solution im Speicher (Muster: `Extractor/GestenTests.cs`).
- **Test-DSL `Cqrs.Testing`** (nur Prüfstand): `Szenario`/`SagaSzenario` (Gegeben → Wenn → Dann über die generierte Fabrik) und `FlussLaufwerk`
  (Flüsse mit gewähltem Ausgangsfall je Funktion; derselbe Belegungs-Kern wie der Dirigent). Die Saga-DSL kennt `Rufe` nicht (`docs/13` P3-3).
- **Framework-Verhalten domänenneutral:** in die Prüfdomäne (`Pruefdomaene/`, eine eigene Anwendung mit Komposition `Pruefdomaene.Laufzeit`)
  einen Typ ergänzen und in `Pruefdomaene.Integration.Tests` testen (Vorlage `ProzessE2ETests`, Helfer `Pruefstand.cs`: Cluster, eigenes Schema,
  Voll-Fold-Zustand) — nicht an die Beispieldomäne binden.
- **Integration:** Store-Semantik gegen echtes Marten, Vorlage `CoCommitPostgresTests`. Infrastruktur extern starten
  (`docker compose -f docker-compose.infrastructure.yml up -d` bzw. `docker start cqrs-postgres cqrs-redis cqrs-consul`), kein
  Testcontainers. Fixtures je Klasse; Sequentialität per `xunit.runner.json`; Verbindungsdaten je Klasse hartkodiert (`docs/13` P2-8).
- **Python:** nur mit dem Interpreter der Umgebung (`Umgebung:Python` in `umgebung.<name>.json`, sonst `CQRS_PYTHON`, sonst `python3`; lokal
  `gpu_env`), weil betterproto/pytest dort installiert sind. Der Fluss-E2E-Test mit Python-Worker liest ihn aus `umgebung.sim-main.json`.

**Garantie-Test:** in den Ordner `Garantien/` der passenden Ebene (Prüfstand über Seams, sonst gegen echtes Marten), Klasse oder Methode mit
`[Trait("Kategorie", "Garantie")]`, und die Zeile in der Nachweis-Spalte von [11](11-garantien.md) nachtragen. Zeigt der Test eine offene Lücke:
`[Fact(Skip = "G-n offen (docs/13)")]` — und einmal ohne Skip laufen lassen, er muss rot sein. Die Phase, die die Lücke schließt, entfernt das Skip.
Störungs-Seams der Prüfdomäne: `Pruefstand.Cluster(…, inboxCache, dienste, schema)`, `Pruefstand.Dekoriere<T>`, `Pruefstand.FrageAsync` (Command mit
vorgegebener CommandId, Modus `Emittiert`).

**E2E-Messung:** `scripts/vermessung-e2e.sh --anlass <phase> --spalte "<Spalte>"` (Höchstlast, ≈ 5 min), `--art stoerung --anlass <phase>-stoerung`
(≈ 3 min); `--probe` ist ein verkürzter Funktionstest des Treibers und schreibt nicht ins Repo. `--art leerlauf` ist eine Diagnose (PF-14),
kein Messlauf: dieselben Blöcke, der Leerlauf aber als Zeitreihe in 10-s-Scheiben (vor der Last 60 s, danach 300 s, nach einem Neustart des Hosts
auf demselben Bestand 150 s) mit Statements und Konsumenten-Ständen je Scheibe (`zeitreihe.csv`, `zeitreihe_statements.csv`, Ordner im
Temp-Verzeichnis wie bei `--probe`); `--szenarien S1,S3,S4,S5` lässt S2 weg.
Danach `scripts/vermessung-tabelle.sh --schreibe` und den Ordner mitcommitten. Das Skript schaltet `pg_stat_statements` und `max_connections = 300`
am Postgres-Container ein (einmaliger Neustart) und startet den Redis der Vermessung.

**Messserver (seit 2026-10-15):** Messungen, Integrations-Suiten und das Gate laufen auf einem eigenen Rechner (Hetzner, bare metal, Intel
i5-13500 mit 6 P- und 8 E-Kernen/20 Threads, 62 GiB, NVMe-RAID1, Ubuntu 24.04, keine GPU) statt auf dem MacBook. Der Code kommt nur über GitHub
dorthin: `scripts/server.sh lauf <befehl …>` verlangt einen gepushten `HEAD`, checkt ihn auf dem Server aus (`/opt/bractor`, detached), führt den
Befehl in tmux aus (übersteht SSH-Abbrüche), liest die Ausgabe mit und holt neue Ordner aus `vermessung/ergebnisse/` per rsync zurück; `--los`
startet ohne zu warten, `warte`/`status`/`log` holen später ab. Ein Lauf zur Zeit. `scripts/server.sh einrichten` (idempotent,
`scripts/server-einrichten.sh`) installiert das .NET-SDK in der Version des Macs, die Python-Umgebung der Worker (torch nur CPU, verlinkt unter dem
Pfad aus `Umgebung:Python`), die Bibliotheken der nativen OpenCvSharp-Runtime und startet die Infrastruktur-Container (Projekt `cqrs`; Postgres und
Redis im Host-Netzwerk ohne docker-proxy, `docker-compose.messserver.yml`, `--netz proxy` für Vergleiche; alles nur auf 127.0.0.1, die Einrichtung prüft das). Nach außen ist nichts offen; die übrigen Dienste des Servers (Caddy, andere Apps) bleiben unberührt. Messwerte des Servers sind nicht mit
denen des MacBooks vergleichbar — Läufe dort tragen ein eigenes Anlass-Präfix (`server-…`) und erst eine eigene Baseline-Spalte, wenn die
Tabelle nach Rechner getrennt ist.

**Gate „frischer Checkout baut“ (vor jedem Push):** `./pruefe-checkout.sh` (Optionen `--verz <dir>` für den Ort des Worktrees, `--ohne-tests`).
Es meldet zuerst lokal vorhandene Dateien, die eine Ignore-Regel schluckt (mit der greifenden Regel aus `git check-ignore -v`; Build- und
Cache-Ausgaben, `deploy-*/` aus `build.sh` und die bewusst ignorierten Altdateien sind ausgenommen), baut dann den Commit `HEAD` in einem
temporären `git worktree` (nur committete Dateien — nicht committete Änderungen prüft es nicht), fährt dort `./codegen.sh --check` und den
Prüfstand und verlangt, dass der Build den Checkout nicht verändert. Der Worktree wird immer entfernt; Exit-Code 0 = grün. Dauer ≈ 2½ min (144 s auf `e26bc15`).

**Werkbank-Abnahme (Ebene 2, `Werkbank.Abnahme.Tests`):** braucht vorher `dotnet build` (startet die gebaute Werkbank-DLL als Prozess, Black-Box über
HTTP). `Werkbanklauf` kopiert alle Quell-Dateien (`git ls-files -co --exclude-standard`, also auch nicht committete Änderungen) in ein Temp-Verzeichnis,
legt dort `git init -b abnahme-<id>` an (Branch → Sim-DB `cqrs_sim_abnahme_<id>`, Redis-Präfix, Cluster) und schreibt `umgebung.abnahme.json` aus
`sim-main` mit freien Ports, Daten-Pfaden im Temp-Verzeichnis und `IBildVerkleinerung` auf Laufort `Extern`. Arbeitsbaum, `.werkbank/` und Sim-DB des
Entwicklers bleiben unberührt; am Ende SIGTERM an die Werkbank (sie hält Host und Worker an), eigene DB und Redis-Schlüssel löschen, Kopie löschen.

## 12.7 Fallstricke

- **xUnit schluckt App-Logs** (Console, ILogger) bis zum Testende — Hänger zuerst im Prüfstand isolieren (Glue-Test mit der echten
  Zustell-Selektion), Cluster-Diagnose mit einem eigenständigen Programm mit sichtbaren Logs.
- **Cluster-Cold-Boot:** Konvergiert die Identitäts-Platzierung beim ersten Start nicht, hilft kein längerer Timeout; viele Cluster-Boot-Läufe
  hintereinander degradieren die lokale Infra (zwischendurch neu starten).
- **Event-Zeitstempel kommen vom Client:** Marten setzt `timestamp` beim Append mit der Uhr des Hosts, die Karenz (Poll, Batch-Leser) vergleicht
  ihn mit der DB-Uhr. Direkt nach dem Append liegt er manchmal bis 0,2 ms „in der Zukunft“ — Tests mit Karenz 0, die eine Position bzw.
  Hochwassermarke prüfen, warten vorher kurz (`BatchHilfe.UhrenversatzAbwartenAsync`). Das war die Ursache der früher gelegentlich flackernden
  `SequenceGapPostgresTests` und `GarantiePostgresTests.Ein_gescheiterter_Append…`.
- **Zeit-Tests nie parallel:** Prüfstand-Tests mit echter Uhr (Leerlauf, Takte im Millisekunden-Bereich) gehören in die Sammlung
  `Zeitkritisch` (`DisableParallelization`); im parallelen Lauf verschieben CPU-hungrige Nachbarn (Analyzer-Kompilate) ihre Takte um Hunderte ms.
- `[Trait("Kategorie", "Garantie")]` markiert die Garantie-Suite; `Skip` gibt es nur für offene G-Lücken (`G-n offen (docs/13)`). Flake-Handling
  bleibt Konvention.
- **Messung:** `pg_stat_database` schreibt seine Zähler verzögert fort (Sekunden) — für Messfenster taugt nur `pg_stat_statements`; mit
  `track = all` zählte es auch Statements innerhalb von Funktionen (Martens `mt_immutable_timestamptz` je Zeile) mit. Fremdlast auf dem Rechner
  (ein Spiel, ein Build) verschiebt den Durchsatz um den Faktor 2 und mehr — sie steht je Szenario im Ergebnis; Läufe darunter vergleichen nur
  Verhältnisse. E2E-Läufe nie gleichzeitig mit einer Integrations-Suite.
- **Kindprozesse mit umgeleiteter Ausgabe:** `dotnet build/restore/run` startet MSBuild-Knoten und den Compiler-Server, die die Pipe erben —
  wer auf das Ende der Ausgabe wartet (`WaitForExitAsync`, `ReadToEnd`), hängt bis zum Zeitlimit. Auf das Prozessende warten (`Werkbank/Server/Prozesse.cs`).

## 12.8 Prinzipien

1. **Nie faken, was man nicht besitzt** — Store-Semantik nur gegen echtes Marten.
2. **Die Testebene folgt der Frage, nicht dem Feature.**
3. **Generative Vollständigkeits-Guards** statt punktueller Fälle (Wire-Boot-Check, Azyklizitäts-Guard, „Code ohne Karte: 0“).
4. **Beweiskraft explizit** — jede Integrationsklasse sagt, warum grün = bewiesen.
5. **Determinismus durch Sequentialität und direktes Treiben.**
6. **Domänenneutral testen:** Framework-Verhalten gehört in eine Test- oder Sonde-Domäne, nicht an die Beispieldomäne gebunden (W11).
