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

**Aktuell (2026-10-15, Phase 0 des Plans Garantien und Durchsatz):** Prüfstand **551 grün + 4 übersprungen** (Lücken G-1 ×2, G-2, G-5),
Integration **43 + 2** (G-4, G-7) sequentiell (≈ 110 s), Prüfdomäne **19 + 3** (G-2, G-3 ×2) sequentiell (≈ 90 s); Garantie-Suite im Prüfstand 138 grün + 4;
Werkbank-Abnahme **1/1** sequentiell (≈ 130 s), `Client.Infrastructure.CollectionTests` **30/30**, Python SDK **34/34**, Worker **21/21**;
`--check` grün (alle Bereiche inkl. „Lage“), `--sonde` grün (111 Soll-Fakten, Lage-Soll 56 Typen); Gate `./pruefe-checkout.sh` grün.

Statisch gezählt (2026-10-15):

| Projekt | `[Fact]` | `[Theory]` | Dateien |
|---|---:|---:|---:|
| Infrastructure.Pruefstand.Tests | 533 | 6 | 81 |
| Infrastructure.Integration.Tests | 45 | 0 | 21 |
| Pruefdomaene.Integration.Tests | 22 | 0 | 8 |
| Client.Infrastructure.CollectionTests | 30 | 0 | 2 |
| Werkbank.Abnahme.Tests | 1 | 0 | 2 |

Die Differenz zwischen 539 Methoden und 555 Läufen im Prüfstand kommt von den expandierten `[Theory]`-Fällen.

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
| Garantien (Störung über Seams, `Garantien/`) | 16 + 4 übersprungen | `FristGarantieTests` (Emit-Timeout und -Fehlschlag, Skip G-1), `KonsumGarantieTests` (doppelte/ungeordnete Weckungen, Absturz zwischen Emit und Cursor, nicht zugestellter Emit Skip G-2), `RehydrationGarantieTests` (Neustart stellt die Inbox her, Split-Upcast Skip G-5), `BuildGarantieTests` (CQRS010/011/052/053, Wire-Boot-Check) |

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
| `Garantien/GarantiePostgresTests` | 3 + 2 übersprungen | ein Append ganz oder gar nicht, zwei Schreiber ohne verlorenes Update, Poison-Stream hält die HWM ohne andere zu blockieren; gescheiterter Append legt den Poll still (Skip G-7), zwei Aktivierungen einer Projektion (Skip G-4) |
| `EingangE2ETests.Client_wiederholt_mit_derselben_CommandId…` | 1 | Client-Wiederholung über den JSON-Eingang wirkt genau einmal |
| Handle-Welt: `PipelineWebhookHttpTests`, `TimerTriggerSchedulingTests` | 2 | Trigger-Pfade ohne Domänen-Nutzer (`docs/13` W11-1) |

**Prüfdomäne (Ebene 2, `Pruefdomaene.Integration.Tests`):**

| Klasse | Tests | Beweisgegenstand |
|---|---:|---|
| `ProzessE2ETests` | 4 | im Cluster: Diamant (Fork + `Und`-Join), Kompensation (ein Zweig abgelehnt → `RückgängigDurch`, `ProzessBeendet(false)`), fünf nebenläufige Vorgänge je genau einmal, `SendeJe`-Fan-out + `UndAlle`-Count-Join |
| `SnapshotTests` | 3 | `MartenSnapshotStore` über die generierte Registrierung (Roundtrip samt Inboxen, Überschreiben, Löschen); Actor über der Schwelle → Snapshot, Rehydration seedet daraus (Snapshot + Tail == Voll-Fold), fremde Schema-Version → Voll-Replay |
| `UpcastingTests` | 5 | generierte Tabelle (Diskriminator `_v2`, Kette, unbekannt → laut); echter Seam: alte Bytes werden beim Lesen aufgewertet, neue Writes tragen `_v2` |
| `MarkingCursorAequivalenzTests` | 4 | `ProzessManager` direkt gegen Marten, Cursor aus vs. an (`MartenProzessMarkingStore`): identische Feuerungen für Diamant, Kompensation, Fan-out + Count-Join (Cursor liest weniger); falscher RegelHash → Voll-Fold |
| `Garantien/SchreibGarantieTests` | 1 + 3 übersprungen | doppelt und gleichzeitig zugestellter Command wirkt genau einmal; alte CommandId jenseits der Inbox-Grenze (Skip G-2), Heilung nach Versionskonflikt und mehrdeutiger Commit (Skip G-3) |
| `Garantien/AblaufGarantieTests` | 2 | die Reaktion `NachschubBeiMindestbestand` wirkt genau einmal, obwohl ihr Poll alles erneut ausgibt; Neustart des Hosts mitten im Vorgang führt ihn zu Ende (eigenes Schema — im geteilten Schema `pruef_e2e` steht der Poll nach den Tombstones der G-3-Tests still, G-7) |

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
| Weckung je (Projektion, Stream): Tracker lesen, Tail lesen, Effekt + Checkpoint in einer Transaktion (`ProjectionAdapter`) | 8 200/s (16 Verb.), 3 300/s (4) | 4 100/s, 1 700/s |
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
| Szenario | Kennzahl | Baseline ([2026-10-15_0429_3cfec32_baseline](../vermessung/ergebnisse/2026-10-15_0429_3cfec32_baseline/)) |
|---|---|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 258 |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 777 |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 87.2 |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1743 |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 162 |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 3739 |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 203 |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 976 |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 1474 |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 4.9 |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 19.9 |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 598 |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 29.8 |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 17286 |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 29476 |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 29.8 |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 258 |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 591 |
| S1 Bildeingang Host, Stoß | Queries (/s) | 5104 |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 360 |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1061 |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 2424 |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0 |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 16 |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 12.4 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 6154 |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 12279 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 55.1 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 314 |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 879 |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 5007 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 281 |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 69.8 |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1341 |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 505 |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 21.2 |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 50.3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1916 |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 35.8 |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0.6 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 22828 |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 35756 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 10.9 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 67.3 |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 544 |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 3365 |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 369 |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2725 |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 119 |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 39.3 |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 54.4 |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1207 |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 17.9 |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 2.7 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 11111 |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 21101 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 10.5 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 57.6 |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 563 |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 3097 |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 307 |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1843 |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1091 |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0.1 |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0.4 |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0 |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 6.4 |
| S4 Datensatz einfrieren | Transaktionen (/s) | 28.5 |
| S4 Datensatz einfrieren | Queries (/s) | 5564 |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 258 |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 774 |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 108 |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 107 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 108 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 21 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 57.4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 105 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10.6 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 66.4 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 1136 |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 7099 |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 365 |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 987 |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2181 |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 126 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 22.4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 14771 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 22324 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 27.4 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 190 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6.1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 59.1 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 766 |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 7380 |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 350 |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1063 |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 333 |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 163 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 138 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1535 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 12.7 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 5179 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 12607 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 6.8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 61.8 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 931 |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 8440 |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 325 |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1064 |
<!-- vermessung-tabelle:ende -->

Baseline (2026-10-15, `3cfec32`, MacBook Air M4 unter Fremdlast — ein Spiel lief, Last 20–25) in Verhältnissen: die Schreibseite nimmt bis
etwa 2 200 Commands/s an (S5, 64 Sender), Ende zu Ende kommen 110–140 Vorgänge/s durch (S5), 20 Bildpaare/s (S1), etwa 50 Teile/s an einem
heißen Sammelvorgang (S3) — die Decke liegt im Konsum (Projektionen, Dirigent), nicht im Schreiben (PF-1, PF-4). Ein Bildpaar kostet etwa
30 Transaktionen und 260 Queries, ein Vorgang der Prüfdomäne 6–11 Transaktionen und 60 Queries. Im Leerlauf nach den Blöcken bleiben
780 bzw. 3 700 Queries/s bei 90 bzw. 200 % CPU (PF-2, PF-4, G-2: Polls lesen lange Streams ab 0), und der RSS fällt nicht zurück (bis 2,7 GB
nach S3 mit 64 Sendern, PF-5). Störungslauf (`2026-10-15_0433_3cfec32_baseline-stoerung`): 19 031 Vorgänge der Prüfdomäne und 181 Bildpaare mit
zwei harten Host-Abbrüchen und 10 s ohne Redis — alles genau einmal.

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
- **`SequenceGapPostgresTests`** flaken im Gesamtlauf gelegentlich, einzeln grün.
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
