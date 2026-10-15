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

**Postgres-Grundgrößen der Konsum- und Schreibseite (2026-10-15, `scripts/vermessung-postgres.sh`).** Bedingungen: Postgres 16 im Container
(Docker Desktop, macOS, 10 Kerne), `pgbench` im Container (ohne Netzwerk), rohes SQL ohne Marten, `synchronous_commit = on`, 200 000 Streams,
rund 2,6 Mio. Events, 15 s je Lauf; zwei Läufe, der zweite unter Fremdlast auf dem Rechner (Rohwerte:
`vermessung/ergebnisse/2026-10-15_000*_nachgetragen_postgres-grundgroessen-lauf*/`). Die absoluten Werte sind eine Obergrenze (Marten,
Netzwerk und Proto.Actor kommen im echten Host dazu); **belastbar sind die Verhältnisse**, sie waren in beiden Läufen gleich.

| Operation (bildet nach) | Lauf 1 | Lauf 2 |
|---|---|---|
| Weckung je (Projektion, Stream): Tracker lesen, Tail lesen, Effekt + Checkpoint in einer Transaktion (bis Phase 2 der Pfad der Projektionen) | 8 200/s (16 Verb.), 3 300/s (4) | 4 100/s, 1 700/s |
| leere Weckung (Poll-Heilung, nichts zu tun): zwei Lese-Queries | 37 000/s (16), 13 400/s (4) | 16 700/s, 8 200/s |
| Batch: 500 Effekte + 500 Checkpoints in einer Transaktion (4 Verb.) | 350 Tx/s = 175 000 Effekte/s | 182 Tx/s = 91 000 Effekte/s |
| Group-Commit, 256 Appends je Tx, ohne Inbox-Marke (4 Verb.) | 510–780 Tx/s | 554 Tx/s |
| dasselbe mit `KommandoVerarbeitet` je Command | 363 Tx/s | 332 Tx/s |
| Poll-Fenster: 30 000 Events mit Payload lesen (nur DB) | 9 ms | 9 ms |
| nachhängende Streams als eine Abfrage über 200 000 Streams | 92 ms | 105 ms |
| langer Stream (100 000 Events) ab 0 | 48 ms | 194 ms |
| derselbe ab Cursor (letzte 10) | 0,02 ms | 0,03 ms |

Folgerungen (Verhältnisse):
- **Batch-Konsum gegen Weckung je Stream: Faktor ~20–22** Effekte/s (docs/13 PF-1). Mit Netzwerk zwischen Host und DB wird der Abstand größer.
- **Inbox-Marke im Stream kostet 35–40 %** Durchsatz im Group-Commit (PF-3).
- **Stream ab 0 gegen ab Cursor: Faktor > 2 000** bei 100 000 Events; wächst linear mit der Stream-Länge (G-2, PF-4).
- Der Poll-Scan selbst ist billig (9 ms je 30 000 Events); teuer sind die Weckungen je Stream danach (PF-2).
- Hochgerechnet auf Weckung je Event trägt die Konsumseite heute etwa 8 200 / K Events/s (K = replaybare Projektionen): bei K = 5 rund 1 600,
  bei K = 20 rund 400 — vor dem Overhead von Marten und Netzwerk. Der Schreibpfad ist nicht die Decke. Gegen den echten Host gemessen ist das
  noch nicht — das tut die E2E-Messung unten.

**Ende zu Ende an den realen Domänen, unter Höchstlast (seit 2026-10-15, `scripts/vermessung-e2e.sh`, ≈ 5 min, Plan Garantien und Durchsatz
§0.1).** Gemessen wird der echte Host im Release-Build (`Host.Grpc` in `umgebungen/umgebung.vermessung.json`, `Host.Pruefdomaene` in
`umgebung.vermessung-pruefdomaene.json`, je eigene DB, eigener Redis `cqrs-vermessung-redis`), Last nur über die echten Eingänge, gezählt bis zum
fachlich sichtbaren Ergebnis. Jeder Block sendet höchstens 15 s so schnell, wie der Eingang annimmt, dann baut der Rückstand ab:

| Block | Eingang | sichtbar, wenn |
|---|---|---|
| S1 Bildeingang Host | Stoß von 600 TIFF-Paaren (96 × 64) ins Quell-Verzeichnis, verschoben, dann Fertig-Marke | `GetImagePair` liefert das Paar komplett (je offenes Paar alle 100 ms) |
| S2 wie S1, Bild-Funktionen im Python-Worker (`Laufort Extern`, zwei Worker) | Stoß von 200 Paaren | wie S1 |
| S3-8, S3-64 Sammelvorgang heiß | 8 bzw. 64 Sender, je Teil `StarteTeilauftrag` + `SchließeTeilauftragAb` an EINEN Sammelvorgang (höchstens 2 000 Teile) | `TeilVerbucht` des Teils im Log (keine Query vorhanden) |
| S4 Datensatz | als KI-Operator `ErstelleDatensatz`, `FuegeRangeHinzu` (alle Paare aus S1), `FriereEin` | `HoleDatensatz`: alle Paare Mitglied, dann `Eingefroren` |
| S5-1, S5-8, S5-64 Prüfdomäne verteilt | 1, 8 bzw. 64 Sender, je zehn Vorgänge 8 Aufträge (Diamant), 1 Auftrag gegen ein volles Kontingent (Kompensation), 1 Verteilung (Fan-out 4, Count-Join), höchstens 3 000 je Block; jede Reservierung unter dem Mindestbestand weckt die Reaktion | `ProzessBeendet` im Log; die Reaktion gesondert (Reservierung → Nachschub) |
| L1, L5 Leerlauf | 15 s ohne Eingang nach den Blöcken | Grundlast (PF-2, PF-4) |

Kennzahlen je Block: angenommen je Sekunde (Schreibseite), sichtbar je Sekunde im Fenster und Durchsatz bis „alles sichtbar“ (Ende zu Ende),
Rückstand am Fensterende und Abbauzeit (ein nicht abgebauter Rückstand ist ein Ergebnis), Latenz p50/p95/p99, Transaktionen und Queries je
Sekunde und je Vorgang (`pg_stat_statements`, `track = top`, Transaktionen = COMMITs, ohne die Lese-Rolle des Treibers), CPU und RSS des Hosts.
Eine Aufwärmrunde vor S1 und S5 zählt nicht. Jeder Lauf auf frischen DBs; danach die Exactly-once-Prüfung ([11](11-garantien.md) §11.14) — mit
offenem Rückstand prüft sie „höchstens einmal, nichts doppelt“. Ablage: `vermessung/ergebnisse/<datum>_<commit>_<anlass>/` (nur anhängen); die
Tabelle unten erzeugt `scripts/vermessung-tabelle.sh --schreibe` aus diesen Ordnern — nie von Hand.

<!-- vermessung-tabelle:anfang -->
| Szenario | Kennzahl | Baseline ([2026-10-15_0429_3cfec32_baseline](../vermessung/ergebnisse/2026-10-15_0429_3cfec32_baseline/)) | Phase 1 ([2026-10-15_0539_b955d40_phase-1](../vermessung/ergebnisse/2026-10-15_0539_b955d40_phase-1/)) | Phase 2 ([2026-10-15_0745_4caa176_phase-2](../vermessung/ergebnisse/2026-10-15_0745_4caa176_phase-2/)) | Phase 2 / Baseline |
|---|---|---:|---:|---:|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 258 | 1600 | 1536 | 5.96× |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 777 | 5472 | 5182 | 6.67× |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 87.2 | 324 | 337 | 3.86× |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1743 | 2120 | 1875 | 1.08× |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 162 | 305 | 226 | 1.39× |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 3739 | 3588 | 4215 | 1.13× |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 203 | 158 | 244 | 1.2× |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 976 | 775 | 991 | 1.02× |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 1474 | 1583 | 1819 | 1.23× |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 4.9 | 13.2 | 3 | 0.62× |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 19.9 | 31.2 | 33.3 | 1.68× |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 598 | 595 | 599 | 1× |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 29.8 | 18.8 | 17.7 | 0.59× |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 17286 | 11296 | 10354 | 0.6× |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 29476 | 18725 | 17565 | 0.6× |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 29.8 | 30.2 | 30.6 | 1.03× |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 258 | 228 | 227 | 0.88× |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 591 | 937 | 1016 | 1.72× |
| S1 Bildeingang Host, Stoß | Queries (/s) | 5104 | 7074 | 7532 | 1.48× |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 360 | 370 | 393 | 1.09× |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1061 | 1287 | 1184 | 1.12× |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 2424 | 1564 | 1517 | 0.63× |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0 | 0 | 0 | – |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 16 | 23.3 | 22.8 | 1.42× |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 | 200 | 200 | 1× |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 12.4 | 8.5 | 8.7 | 0.7× |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 6154 | 4301 | 4777 | 0.78× |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 12279 | 8464 | 8549 | 0.7× |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 55.1 | 43.5 | 48 | 0.87× |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 314 | 306 | 305 | 0.97× |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 879 | 1002 | 1081 | 1.23× |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 5007 | 7046 | 6860 | 1.37× |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 281 | 289 | 284 | 1.01× |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 69.8 | 79.2 | 77.7 | 1.11× |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1341 | 1776 | 1220 | 0.91× |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 505 | 1137 | 1010 | 2× |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 21.2 | 40.4 | 39.4 | 1.86× |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 50.3 | 98 | 90.3 | 1.8× |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1916 | 1929 | 1922 | 1× |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 35.8 | 18.6 | 20.2 | 0.56× |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0.6 | 0 | 0 | 0.06× |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 22828 | 11269 | 11941 | 0.52× |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 35756 | 18819 | 20330 | 0.57× |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 10.9 | 11.8 | 11.8 | 1.09× |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 67.3 | 59 | 60.7 | 0.9× |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 544 | 1144 | 1057 | 1.94× |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 3365 | 5740 | 5428 | 1.61× |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 369 | 416 | 428 | 1.16× |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2725 | 2549 | 2388 | 0.88× |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 119 | 195 | 265 | 2.23× |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 39.3 | 65.6 | 88 | 2.24× |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 54.4 | 105 | 100 | 1.84× |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1207 | 1326 | 1335 | 1.11× |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 17.9 | 8.9 | 12.4 | 0.69× |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 2.7 | 0 | 0 | 0.01× |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 11111 | 5148 | 5222 | 0.47× |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 21101 | 14858 | 15096 | 0.72× |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 10.5 | 8.3 | 8.5 | 0.81× |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 57.6 | 50.6 | 50.4 | 0.88× |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 563 | 854 | 844 | 1.5× |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 3097 | 5227 | 4998 | 1.61× |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 307 | 441 | 443 | 1.44× |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1843 | 2636 | 2407 | 1.31× |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1091 | 1826 | 1828 | 1.68× |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0.1 | 0.1 | 0.1 | 0.94× |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0.4 | 0.2 | 0.2 | 0.49× |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0 | 0 | 0 | 1× |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 6.4 | 3.2 | 3.2 | 0.5× |
| S4 Datensatz einfrieren | Transaktionen (/s) | 28.5 | 41.1 | 42.4 | 1.49× |
| S4 Datensatz einfrieren | Queries (/s) | 5564 | 4002 | 4134 | 0.74× |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 258 | 216 | – | – |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 774 | 1307 | – | – |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 108 | 149 | 131 | 1.21× |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 107 | 147 | 130 | 1.21× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 108 | 149 | 131 | 1.21× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 21 | 27 | 17 | 0.81× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0 | 0 | 0 | 1.08× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 57.4 | 42.6 | 47.2 | 0.82× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 105 | 74.7 | 92 | 0.88× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10.6 | 10.5 | 10.5 | 0.99× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 66.4 | 63.9 | 63.7 | 0.96× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 1136 | 1556 | 1355 | 1.19× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 7099 | 9449 | 8217 | 1.16× |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 365 | 368 | 377 | 1.03× |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 987 | 1329 | 997 | 1.01× |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2181 | 2956 | 2356 | 1.08× |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0 | 0 | 0 | – |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 126 | 144 | 128 | 1.02× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 | 3000 | 3000 | 1× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 22.4 | 19.9 | 22.1 | 0.99× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0 | 0 | 0 | 0.89× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 14771 | 12782 | 13996 | 0.95× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 22324 | 19822 | 21980 | 0.98× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 27.4 | 17.4 | 21 | 0.76× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 190 | 127 | 145 | 0.76× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6.1 | 6 | 6.3 | 1.03× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 59.1 | 62.1 | 61.2 | 1.04× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 766 | 860 | 799 | 1.04× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 7380 | 8854 | 7752 | 1.05× |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 350 | 343 | 376 | 1.08× |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1063 | 1112 | 1024 | 0.96× |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 333 | 423 | 399 | 1.2× |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 163 | 183 | 174 | 1.07× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 138 | 171 | 138 | 1× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1535 | 1699 | 1692 | 1.1× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 12.7 | 10.5 | 14.3 | 1.12× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 5179 | 4700 | 4702 | 0.91× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 12607 | 10458 | 14210 | 1.13× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 6.8 | 7 | 7.2 | 1.05× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 61.8 | 61.4 | 62.1 | 1.01× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 931 | 1181 | 975 | 1.05× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 8440 | 10307 | 8426 | 1× |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 325 | 304 | 292 | 0.9× |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1064 | 1116 | 1048 | 0.98× |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | – | – | 1175 | – |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | – | – | 2407 | – |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | – | – | 0.5 | – |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | – | – | 63827 | – |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | – | – | 63827 | – |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | – | – | 980 | – |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | – | – | 1048 | – |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | – | – | 0.9 | – |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | – | – | 25622 | – |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | – | – | 25622 | – |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | – | – | 1068 | – |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | – | – | 1184 | – |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | – | – | 0.9 | – |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | – | – | 4840 | – |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | – | – | 4840 | – |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | – | – | 1044 | – |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | – | – | 1220 | – |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | – | – | 0.9 | – |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | – | – | 10661 | – |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | – | – | 10661 | – |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | – | – | 2027 | – |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | – | – | 2407 | – |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | – | – | 0.8 | – |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | – | – | 30235 | – |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | – | – | 30235 | – |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | – | – | 1667 | – |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | – | – | 2407 | – |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | – | – | 0.7 | – |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | – | – | 13490 | – |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | – | – | 13490 | – |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | – | – | 4854 | – |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | – | – | 4854 | – |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | – | – | 915 | – |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | – | – | 1013 | – |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | – | – | 0.9 | – |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | – | – | 4527 | – |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | – | – | 4527 | – |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | – | – | 930 | – |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | – | – | 1048 | – |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | – | – | 0.9 | – |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | – | – | 19972 | – |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | – | – | 19972 | – |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | – | – | 1024 | – |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | – | – | 1048 | – |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | – | – | 1 | – |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | – | – | 10993 | – |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | – | – | 10993 | – |
<!-- vermessung-tabelle:ende -->

Baseline (2026-10-15, `3cfec32`, MacBook Air M4 unter Fremdlast — ein Spiel lief, Last 20–25) in Verhältnissen: die Schreibseite nimmt bis
etwa 2 200 Commands/s an (S5, 64 Sender), Ende zu Ende kommen 110–140 Vorgänge/s durch (S5), 20 Bildpaare/s (S1), etwa 50 Teile/s an einem
heißen Sammelvorgang (S3) — die Decke liegt im Konsum (Projektionen, Dirigent), nicht im Schreiben (PF-1, PF-4). Ein Bildpaar kostet etwa
30 Transaktionen und 260 Queries, ein Vorgang der Prüfdomäne 6–11 Transaktionen und 60 Queries. Im Leerlauf nach den Blöcken bleiben
780 bzw. 3 700 Queries/s bei 90 bzw. 200 % CPU (PF-2, PF-4, G-2: Polls lesen lange Streams ab 0), und der RSS fällt nicht zurück (bis 2,7 GB
nach S3 mit 64 Sendern, PF-5). Störungslauf (`2026-10-15_0433_3cfec32_baseline-stoerung`): 19 031 Vorgänge der Prüfdomäne und 181 Bildpaare mit
zwei harten Host-Abbrüchen und 10 s ohne Redis — alles genau einmal.

Phase 1 (2026-10-15, `b955d40`, gleiche Fremdlast; Emit-Ausgang, Emittenten-Cursor, Drossel je Ziel, Backoff des Dirigenten) gegen die Baseline:
Ende zu Ende S1 1,6 × (31 Paare/s), S2 1,5 ×, S3 heiß 1,9–2,0 × (≈ 100 Teile/s, Abbauzeit halbiert, Prozess-Ende ohne Nachlauf), S4 1,7 ×,
S5 1,1–1,4 ×; die Reaktion antwortet schneller (S5-64 p50 17 statt 27 ms, p99 127 statt 190 ms), Queries je Vorgang gleich oder weniger. Der
Leerlauf L1 zeigt mehr Arbeit (1 600 statt 260 Transaktionen/s): es sind die Erstweckungen der neuen S3-Streams durch den Heil-Poll jeder
Projektion (24 133 Checkpoint-Lesezugriffe, nur 109 fanden einen — PF-2, je neuer Stream × 5 Projektionen). Diese Fixkosten fielen in der
Baseline teils nach dem Messfenster an; nach ≈ 20 s ist der Host still (≈ 1 % CPU). Der Leerlauf ist deshalb erst nach Phase 3 (Batch-Konsum,
ein Heil-Poll nur für nachhängende Streams) vergleichbar. Störungslauf (`2026-10-15_0542_b955d40_phase-1-stoerung`): 22 190 Vorgänge und 181
Bildpaare mit zwei harten Abbrüchen und Redis-Ausfall — alles genau einmal.

Phase 2 (2026-10-15, `4caa176`/`860cb7a`; Rehydration nach Append-Fehler, Passivierung mit den E7-Werten 600/120/600 s) gegen Phase 1: S1–S4
0,92–1,07 ×, S5 0,81–0,89 × im Gesamtlauf. Der Abstand bei S5 ist Wärme, nicht Code: S5 allein, abwechselnd mit und ohne Passivierung gemessen
(Reihenfolge A-B-B-A, Ordner `…phase-2-ab-passivierung-*`), liefert mit Passivierung S5-8 208/198, ohne 189/161 Vorgänge/s — auf oder über
Phase 1. Neue Kennzahlen der Leerlauf-Blöcke: RSS am Fensterende und Höchststand, aktive Actors (`/monitoring/metrics`). Mit 600 s Leerlauf
passiviert im 15-s-Fenster nichts, und L1/L5 sind ohnehin nicht leer (Heil-Poll PF-2, Prozess-Poll PF-4 wecken Tausende Actors); das E5-Ziel
„RSS im Leerlauf < 50 %“ ist erst nach Phase 3 und 5 sinnvoll messbar. Ein Lauf mit 10 s Passivierung zeigte eine Todesspirale (der Backstop
weckte alle 15 s jeden gerade passivierten Vorgang kalt) — seitdem verlangt ein Boot-Guard ≥ 60 s für den Prozess-Manager. Störungslauf
(`…_phase-2-stoerung` mit Abbau nach Fortschritt): 29 609 Vorgänge und 181 Bildpaare, alles genau einmal, Abbau 310 s.

**Als korrekt verifiziert (Backend-Audit 2026-08, nicht erneut jagen):** Versionsarithmetik inkl. Inbox-Marken; OCC-Skip auf dem
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
Störungs-Seams der Prüfdomäne: `Pruefstand.Cluster(…, inboxGrenze, dienste)`, `Pruefstand.Dekoriere<T>`, `Pruefstand.FrageAsync` (Command mit
vorgegebener CommandId, Modus `Emittiert`).

**E2E-Messung:** `scripts/vermessung-e2e.sh --anlass <phase> --spalte "<Spalte>"` (Höchstlast, ≈ 5 min), `--art stoerung --anlass <phase>-stoerung`
(≈ 3 min); `--probe` ist ein verkürzter Funktionstest des Treibers und schreibt nicht ins Repo.
Danach `scripts/vermessung-tabelle.sh --schreibe` und den Ordner mitcommitten. Das Skript schaltet `pg_stat_statements` und `max_connections = 300`
am Postgres-Container ein (einmaliger Neustart) und startet den Redis der Vermessung.

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
