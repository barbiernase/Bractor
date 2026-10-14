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

**Prinzip:** Ebene 1 testet nur, was ohne Store deterministisch entscheidbar ist. Store-Semantik (Co-Commit-Atomarität, `seq_id`-Lücken, geteilte
Dokumente, Metadaten) wird **ausschließlich** gegen echtes Marten geprüft. Es gibt bewusst **keinen `InMemoryEventStore`**; In-Memory-Doubles gibt
es nur für nicht-autoritative Randstores (Inv. 6).

## 12.2 Zahlen

**Aktuell (2026-10-15, gemessen auf Commit-Basis `e26bc15` + WB-2):** Prüfstand **540/540**, Integration **39/39** sequentiell (100 s), Prüfdomäne **16/16**
sequentiell, Werkbank-Abnahme **1/1** sequentiell (81–88 s), `Client.Infrastructure.CollectionTests` **30/30**, Python SDK **34/34**, Worker **21/21**;
`--check` grün (alle Bereiche inkl. „Lage“), `--sonde` grün (111 Soll-Fakten, Lage-Soll 56 Typen); Gate `./pruefe-checkout.sh` rot nur durch
`Domain.Client.Modules.Blazor/Debug/` (docs/13 P3-2).

Statisch gezählt (2026-10-15):

| Projekt | `[Fact]` | `[Theory]` | Dateien |
|---|---:|---:|---:|
| Infrastructure.Pruefstand.Tests | 518 | 6 | 75 |
| Infrastructure.Integration.Tests | 39 | 0 | 19 |
| Pruefdomaene.Integration.Tests | 16 | 0 | 4 |
| Client.Infrastructure.CollectionTests | 30 | 0 | 2 |
| Werkbank.Abnahme.Tests | 1 | 0 | 2 |

Die Differenz zwischen 524 Methoden und 540 Läufen im Prüfstand kommt von den expandierten `[Theory]`-Fällen.

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
| Quellen, Umgebung, Spur, Dienste | 64 | `DateiTreiberTests` (17), `UmgebungLaderTests`, `ProjektionsStandTests`, `SpurTests`, `WirkerIdTests`, `SpurWegTests`, `DienstBindungTests` (15) |
| Konsum-Maschine, Prozess | 37 | `EmittentenCursorTests`, `ProjectionRebuilderTests`, `EmittentenPfadAdapterTests`, `GaEinsPruefungTests`, `ProzessPollFilterTests`, `AblehnungsMarkeTests`, `ReaktionsIdTests`, `CommandVorpruefungTests`, `DispatcherCommandFailedTests`, `TransientPersistiertSplitTests`, `DeadLetterReadStoreTests` |
| Übrige | 23 | `FristSchedulerTests`, `FristPlanerTests`, `BackendMonitoringTests`, `EingangJsonTests`, `BrokerIdentityTests`, Handle-Welt: `PipelineEventPullBridgeTests`, `TimerTriggerTests`, `WebhookTriggerTests` |

**Integration (Ebene 2):**

| Klasse | Tests | Beweisgegenstand |
|---|---:|---|
| `CoCommitPostgresTests` | 3 | Effekt + Checkpoint atomar in einer Marten-Session; Absturz dazwischen → genau ein Eintrag |
| `GeteilteDokumentePostgresTests`, `SchreibRegelPostgresTests` | 4 + 5 | Single-Writer je Dokument, OCC bei geteilten Dokumenten, Wiederholung |
| `SequenceGapPostgresTests` | 2 | `seq_id`-Lücke + Straggler-Grace |
| `MetadataPostgresTests` | 3 | Header-Readback |
| `DeadlineStorePostgresTests` | 2 | durabler Fristplan |
| `PollerBackstopPostgresTests` | 1 | Poll-Cursor heilt verlorene Signale |
| `LiveCommandE2ETests` | 1 | Command → Event → Projektion im Cluster |
| `TwoNodeCommandDispatchTests`, `RemotePidDeliverySmokeTests`, `SignalDeliveryClusterTests` | 3 | Cross-Node-Serialisierung, Remote-PID, Signal-Zustellung |
| `PipelineFlussE2ETests` | 5 | Fluss gegen den echten Cluster: Quelle → Dirigent → Vermittler → Abholer → Fakt, auch mit Python-Worker über gRPC; Warten + Backstop; Lese-Fähigkeit |
| `VertragsZusageE2ETests` | 1 | doppelt zugestellte Zusage wirkt genau einmal |
| `UmgebungE2ETests`, `LeseseitenStandE2ETests`, `EingangE2ETests` | 3 + 1 + 1 | Siegel, Projektions-Stand nach Code-Änderung, JSON-Eingang/Webhook |
| `MonitoringHttpTests` | 2 | `/health`, Kennzahlen |
| Handle-Welt: `PipelineWebhookHttpTests`, `TimerTriggerSchedulingTests` | 2 | Trigger-Pfade ohne Domänen-Nutzer (`docs/13` W11-1) |

**Prüfdomäne (Ebene 2, `Pruefdomaene.Integration.Tests`):**

| Klasse | Tests | Beweisgegenstand |
|---|---:|---|
| `ProzessE2ETests` | 4 | im Cluster: Diamant (Fork + `Und`-Join), Kompensation (ein Zweig abgelehnt → `RückgängigDurch`, `ProzessBeendet(false)`), fünf nebenläufige Vorgänge je genau einmal, `SendeJe`-Fan-out + `UndAlle`-Count-Join |
| `SnapshotTests` | 3 | `MartenSnapshotStore` über die generierte Registrierung (Roundtrip samt Inboxen, Überschreiben, Löschen); Actor über der Schwelle → Snapshot, Rehydration seedet daraus (Snapshot + Tail == Voll-Fold), fremde Schema-Version → Voll-Replay |
| `UpcastingTests` | 5 | generierte Tabelle (Diskriminator `_v2`, Kette, unbekannt → laut); echter Seam: alte Bytes werden beim Lesen aufgewertet, neue Writes tragen `_v2` |
| `MarkingCursorAequivalenzTests` | 4 | `ProzessManager` direkt gegen Marten, Cursor aus vs. an (`MartenProzessMarkingStore`): identische Feuerungen für Diamant, Kompensation, Fan-out + Count-Join (Cursor liest weniger); falscher RegelHash → Voll-Fold |

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

**Werkbank (2026-10-12/13):** warmes Lesen nach Text-Änderung p50 224 ms (`GraphExtractor -- --warm`); Geste p50 unter Fremdlast 346–470 ms
(Ziel 300 ms).

**Prozess-Maschine (P5b, gegen echtes Postgres):** Marking-Cursor und feuer-gerichtete Reads machen den warmen Fold bis 9× schneller bei N = 60
(5,5× mit Aggregat-Historie); die Stream-Reads sinken von O(N²) auf O(N).

**Schreibpfad (2026-08, mit dem inzwischen entfernten LoadHarness, 4-Kern-localhost):** Group-Commit-Batching mit parallelem Drain +48 % Durchsatz.
Negativ-Befunde, die nicht erneut untersucht werden müssen:
- Serialisierung ist für den Durchsatz irrelevant (~0 %); der STJ-Source-Gen-Serializer brachte nichts (bleibt opt-in, `Schreibpfad.GeneriertesJson`).
- Synchroner Redis-Zugriff im Command-Turn ist bei localhost-Redis irrelevant (~0 %); relevant erst bei Remote-Redis.
- Die CPU ist unter Volllast zu ~80 % idle; der Hebel ist Parallelität, nicht schnellere Einzel-Commits. Ein eigener COPY-basierter Event-Store
  ist zurückgestellt.

**Als korrekt verifiziert (Backend-Audit 2026-08, nicht erneut jagen):** Versionsarithmetik inkl. Inbox-Marken; OCC-Skip auf dem
`AnyVersion`-Pfad; Append vor State-Mutation; Shard-Hash-Konsistenz Subscribe ↔ gezieltes Publish; Pre-Dispatch-Guard des `ProjectionAdapter`;
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
- **`SequenceGapPostgresTests`** flaken im Gesamtlauf gelegentlich, einzeln grün.
- Es gibt keine `[Skip]`/`[Trait]`-Attribute; Flake-Handling ist Konvention.
- **Kindprozesse mit umgeleiteter Ausgabe:** `dotnet build/restore/run` startet MSBuild-Knoten und den Compiler-Server, die die Pipe erben —
  wer auf das Ende der Ausgabe wartet (`WaitForExitAsync`, `ReadToEnd`), hängt bis zum Zeitlimit. Auf das Prozessende warten (`Werkbank/Server/Prozesse.cs`).

## 12.8 Prinzipien

1. **Nie faken, was man nicht besitzt** — Store-Semantik nur gegen echtes Marten.
2. **Die Testebene folgt der Frage, nicht dem Feature.**
3. **Generative Vollständigkeits-Guards** statt punktueller Fälle (Wire-Boot-Check, Azyklizitäts-Guard, „Code ohne Karte: 0“).
4. **Beweiskraft explizit** — jede Integrationsklasse sagt, warum grün = bewiesen.
5. **Determinismus durch Sequentialität und direktes Treiben.**
6. **Domänenneutral testen:** Framework-Verhalten gehört in eine Test- oder Sonde-Domäne, nicht an die Beispieldomäne gebunden (W11).
