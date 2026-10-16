# 12 — Tests & Vermessung

**Die eine Quelle für Zahlen und Messwerte.** Andere Dokumente verweisen hierher. Offene Testlücken stehen als Befund in
`docs/13-reifegrad-schulden-bewertung.md` (G-6, P2-5, WZ-1). Was die Garantien beweist, steht je Zeile in [11](11-garantien.md) (Nachweis-Spalte).
Stand: 2026-10-16. Die Messungen bis zu diesem Tag (Höchstlast-Blöcke S1–S5, Phasen des Plans) stehen eingefroren in
[12-anhang-messarchiv.md](12-anhang-messarchiv.md).

## 12.1 Die Ebenen

Drei Ebenen, je ein Befehl. Die Teststrategie folgt den Invarianten „die Wahrheit ist der Log“ und „nie faken, was man nicht besitzt“: was ohne
Store deterministisch entscheidbar ist, prüft Ebene 1; Store-Semantik (Co-Commit, `seq_id`-Lücken, geteilte Dokumente, Inbox, Metadaten) prüft
**ausschließlich** Ebene 2 gegen echtes Marten. Es gibt bewusst **keinen `InMemoryEventStore`**; In-Memory-Doubles gibt es nur für nicht-autoritative
Randstores (Inv. 6).

| Ebene | Befehl | Inhalt | Dauer (Messserver) |
|---|---|---|---:|
| 1 · schnell, store-frei | `./pruefe.sh schnell` | ignorierte Quellen; Build; Codegen-Drift (`./codegen.sh --check`); der Build verändert den Checkout nicht; Editor-Parität (`GraphExtractor -- --check`); Agnostik-Sonde (`-- --sonde`); Prüfstand `Infrastructure.Pruefstand.Tests` (auch die Client-Tests); Python-SDK und -Worker (pytest) | ≈ 1½ min |
| 2 · echtes Marten | `./pruefe.sh alles` | Ebene 1, dann **nacheinander** `Infrastructure.Integration.Tests` (Store-Semantik und Cluster an der Beispielanwendung), `Pruefdomaene.Integration.Tests` (Framework domänenneutral an der zweiten Anwendung), `Werkbank.Abnahme.Tests` (echte Werkbank, echter Host in `sim`, eigene Sim-DB, Python-Worker) | ≈ 5 min |
| 3 · Ende zu Ende | `scripts/vermessung-e2e.sh --anlass <a>` · `--stoerung --anlass <a>` | Leistungsmessung (sechs Szenarien, je dreimal) bzw. Störungslauf, echte Hosts im Release-Build, eigene DBs; Exactly-once-Prüfung nach jedem Block (§12.5) | ≈ 6 bzw. 4 min |

- **Wo:** Ebene 2 und 3 laufen auf dem Messserver (`scripts/server.sh lauf ./pruefe.sh alles`, §12.6), Ebene 1 auch lokal. Der Server checkt den
  gepushten Commit frisch aus — ein Lauf dort prüft, was ein Clone sieht (früher eigenes Gate `pruefe-checkout.sh`).
- **Vor jedem Push:** `./pruefe.sh alles` auf dem Server grün; bei Leistungsfragen zusätzlich Ebene 3.
- **Garantien:** keine eigene Suite. Jeder Nachweis läuft in seiner Ebene; die Nachweis-Spalte von [11](11-garantien.md) nennt ihn, und
  `GarantieMatrixTests` (Prüfstand) hält Spalte und Code gleich. Offene Lücken sind Skip-Tests `G-n offen (docs/13)`.
- **Nicht in einer Ebene:** `GraphExtractor -- --grammatik` (eine Ausgabe, kein Test) und `-- --warm` (Messung der Werkbank, §12.5).

## 12.2 Zahlen

Stand 2026-10-16 (Messserver, `./pruefe.sh alles` grün):

| Projekt | `[Fact]` | `[Theory]` | Fälle | Dateien | Dauer |
|---|---:|---:|---:|---:|---:|
| Infrastructure.Pruefstand.Tests | 575 | 8 | 606 + 1 übersprungen (G-5) | 90 | ≈ 20 s |
| Infrastructure.Integration.Tests | 33 | 3 | 39 | 16 | ≈ 85 s |
| Pruefdomaene.Integration.Tests | 28 | 3 | 37 | 9 | ≈ 35 s |
| Werkbank.Abnahme.Tests | 1 | 0 | 1 | 2 | ≈ 90 s |
| Python: SDK / Worker | – | – | 34 / 21 | – | ≈ 3 s |

Dazu `--check` (alle Bereiche inkl. „Lage“, ≈ 15 s) und `--sonde` (≈ 25 s). Gezählt: Zeilen `[Fact`/`[Theory`, Fälle = ausgeführte Tests
(expandierte Theorien). Vor der Vereinfachung (2026-10-16 früh): Prüfstand 599 + 1, Client-Tests 30 (eigenes Projekt), Integration 57, Prüfdomäne 39 —
weggefallen sind Doppel, Smoke-Tests und Tests von Test-Doubles (§12.3), keine Garantie hat ihren Nachweis verloren ([11](11-garantien.md)).

## 12.3 Was getestet ist

**Prüfstand (Ebene 1), nach Ordner (Tests):**

| Ordner | Tests | Inhalt |
|---|---:|---|
| `Garantien/` | 35 | die Logik-Garantien über Seams (Batch-Konsum, Emit-Ausgang und Drossel, Fristen, Konsum der Emittenten, Prozess-Poll, Offen-Index, Zeitlimits, Rehydration) und die Build-Garantien (CQRS010/011/052/053, Wire-Boot-Check) — je Zeile in [11](11-garantien.md) |
| `Analyzers/` | 90 | die Diagnose-Regeln (CQRS050–098) an Proben über eine eigene `CSharpCompilation` |
| Wurzel (`DomainEditor*`, `PythonQuelleTests`) | 76 | Editor-Modell, Scaffolder, Python-Quellen |
| `Funktionen/` | 61 | Katalog-Funktionen, `Rufe`, Fluss-DSL am echten Dirigenten, Vermittlung und Lease, Generator |
| `Akteure/` | 49 | Akteur-Rechte, Verträge, Clients, Hash Python = Server |
| `Extractor/` | 47 | Gesten (minimaler Patch), Sicht, tolerantes Lesen |
| `Beispieldomaene/` | 43 | Fachlogik des Prüflings (Datensatz, Trainingslauf, Sammelvorgang, Bildeingang, seine Flüsse) — kein Framework-Nachweis |
| `Clients/` | 30 | `VirtualCollection`, `PendingCommandTracker` (Blazor-Client) |
| `Serialisierung/` | 21 | Signale, Event-JSON, DTO-Mapper, Versionen im Envelope |
| `Quellen/`, `Umgebung/`, `Spur/`, `Dienste/` | 18 · 17 · 16 · 15 | Datei-Treiber, Umgebung und Projektions-Stand, Spur und Wirker, Dienst-Bindung |
| `Schreibseite/`, `Konsumenten/`, `Prozess/` | 13 · 11 · 7 | Group-Commit, Vorprüfung, Inbox-Cache, `CommandFailed`; Batch-Leser, Neuaufbau, GA-1, Emit-Id, Weckungen; Heilplan, Router, Poll-Filter |
| `HandleWelt/` | 13 | Handle-Pipeline, Timer, Webhook, Fristplaner — Code ohne Nutzer, fällt mit dem Rückbau W11-1 ([13](13-reifegrad-schulden-bewertung.md)) |
| übrige (`Lebenszyklus/`, `Werkbank/`, `Eingang/`, `Monitoring/`, `Doku/`, `PubSub/`) | 21 | Passivierung (Sammlung `Zeitkritisch`), Git-Kopf und Proto-Vergleich der Werkbank, JSON-Eingang, Health, `GarantieMatrixTests` |

**Integration (Ebene 2):** Store-Semantik — Co-Commit (`CoCommitPostgresTests`), Batch-Leser gegen echte Sichtbarkeit und Nachzügler
(`SequenceGapPostgresTests`, `PollerBackstopPostgresTests`), geteilte Dokumente (`SchreibRegelPostgresTests` domänenneutral,
`GeteilteDokumentePostgresTests` die generierte Registrierung), `GarantiePostgresTests` (Atomarität von Effekt und Quittung, zwei Schreiber, Offen-Index im
Append, Poison und Tombstones im Poll, zwei Aktivierungen, Neuaufbau gegen einen laufenden Leser), Metadaten, Fristplan; Ende zu Ende im Cluster —
`EingangE2ETests` (Akteur-Tor, Client-Wiederholung), `PipelineFlussE2ETests` (Fluss von der Quelle bis zum Fakt, Python-Worker über gRPC, Lese-Fähigkeit,
Datei-Treiber über einen Neustart), `ZweiKnotenTests` (Command-Dispatch und PID-Zustellung zwischen zwei Knoten), `UmgebungE2ETests`,
`LeseseitenStandE2ETests`, `MonitoringHttpTests`.

**Prüfdomäne (Ebene 2):** `ProzessE2ETests` (Diamant, Kompensation, nebenläufige Vorgänge, Fan-out + Count-Join), `MarkingCursorAequivalenzTests`,
`SnapshotTests`, `UpcastingTests`, `Garantien/` — `SchreibGarantieTests` (Inbox, Bündel, Konflikte, mehrdeutiger Commit), `AblaufGarantieTests`
(Reaktion nach Absturz zwischen Emit und Cursor, Neustart des Hosts mitten im Vorgang), `LebenszyklusGarantieTests` (Passivierung).

**Python:** SDK `test_vertrag`, `test_funktion`, `test_client_query`, `test_client_reconnect`, `test_mapper_*`; Worker `test_vertraege`,
`test_funktionen`, `test_funktions_worker`, `test_training_worker`.

## 12.4 Was nicht getestet ist

- **Prozess-Verkettung** (ein Prozess-Ergebnis löst den nächsten aus) — die Prüfdomäne deckt Diamant, Kompensation, Nebenläufigkeit, Fan-out und
  Count-Join ab, Verkettung noch nicht.
- **`KlärungNötig`-Pfad** und der Kompensations-Warmpfad (`NächsteKompensationAsync` ab 0) (P2-5); **Fehler nach dem Append im Actor** (G-6).
- **`SendeJe`, Count-Join in der Sonde** — getestet (Prüfdomäne), aber nicht von `--sonde` abgedeckt (`docs/13` WZ-1).
- **Schreibpfad-Skalierung über mehrere Knoten, Shard-Rebalance, Knoten-Ausfall** — nur zwei Knoten im Test, die Messung läuft auf einem.
- **Blazor-Client** jenseits `VirtualCollection`/`PendingCommandTracker` (Bus, Store, Transport).

## 12.5 Messwerte

### Messverfahren (Ebene 3)

Gemessen wird der echte Host im Release-Build (`Host.Grpc` in `umgebungen/umgebung.vermessung.json`, `Host.Pruefdomaene` in
`umgebung.vermessung-pruefdomaene.json`, je eigene, frische DB, eigener Redis `cqrs-vermessung-redis`). Last kommt nur über die echten Eingänge
(Datei-Quelle, JSON-Eingang mit Akteur-Token), gezählt wird bis zum fachlich sichtbaren Ergebnis. Nach 30 s Warmlauf läuft jeder Lastblock
**dreimal** im selben Host (frische Aggregate, wachsendes Log); die Tabelle zeigt je Zelle den Median, in Klammern die Spanne, wenn sie 5 % übersteigt.

| Szenario | Last | deckt ab | sichtbar, wenn |
|---|---|---|---|
| Bildeingang | Stoß von 600 TIFF-Paaren (96 × 64) ins Quell-Verzeichnis | Quelle (Datei-Treiber), Fluss mit vier Funktionen, Projektionen, Query | `GetImagePair` liefert das Paar komplett (je offenes Paar alle 100 ms) |
| Heißer Schreiber | 2 000 Teile von 64 Sendern an **einen** Sammelvorgang (je Teil `StarteTeilauftrag` + `SchließeTeilauftragAb`) | Single-Writer, Bündel, Prozess je Teil | `TeilVerbucht` im Log |
| Verteilt ruhig | Prüfdomäne, 1 Sender, 1 000 Vorgänge | Latenz eines Vorgangs ohne Warteschlange | `ProzessBeendet` im Log |
| Verteilt an der Decke | Prüfdomäne, 64 Sender, 3 000 Vorgänge; je zehn 8 Aufträge (Diamant), 1 Auftrag gegen ein volles Kontingent (Kompensation), 1 Verteilung (Fan-out 4, Count-Join); fast jede Reservierung weckt die Reaktion `NachschubBeiMindestbestand` | Schreiben verteilt, Prozess-Maschine, Reaktion | `ProzessBeendet` im Log; die Reaktion gesondert (Reservierung → Nachschub) |
| Neuaufbau | nach den Blöcken: Host stoppen, gespeicherten Projektions-Stand löschen (wirkt in Sim wie eine Code-Änderung), Host starten — alle 5 Projektionen × 8 Partitionen lesen das Log ab 0; dreimal | Decke der Projektionen (Batch-Konsum) | Rückstand in `/monitoring/metrics` = 0 (alle 50 ms gefragt) |
| Leerlauf (je Anwendung) | ohne Eingang: erst der Nachlauf, bis alle Poll-Cursor und Projektions-Positionen am Kopf des Logs stehen (höchstens 180 s), dann 60 s (zwei Poll-Perioden) | Grundlast | – |

Kennzahlen: **Durchsatz** = Vorgänge durch die Zeit vom ersten Start bis zum letzten sichtbaren; **Schreiben angenommen** = so schnell nimmt
der Eingang an (je Vorgang ein beantworteter Command bzw. eine Datei); **Latenz** p50/p99 vom Start bis sichtbar (unter einem Stoß ist das
überwiegend Warteschlange — die Latenz ohne Warteschlange zeigt „Verteilt ruhig“); **Reaktion** p99 = Reservierung bis Nachschub im Log;
**Kosten je Vorgang** = Transaktionen (COMMITs) und Queries aus `pg_stat_statements` (ohne die Lese-Rolle des Treibers) und CPU-Millisekunden des
Hosts; ohne Eingang Queries je Sekunde, CPU in Prozent eines Kerns, Dauer des Nachlaufs, RSS. Weitere Werte (Rückstand, Abbauzeit, CPU des ganzen
Rechners, aktive Actors, RSS-Verlauf) stehen in `ergebnis.json`. Nach jedem Block die Exactly-once-Prüfung ([11](11-garantien.md) §11.14), je
Anwendung die globale; ein Fehlschlag macht den Lauf ungültig. Der **Störungslauf** (`--stoerung`, ≈ 4 min): 90 s Bildeingang (2 Paare/s) und
Prüfdomäne (8 Sender, Wiederholung mit derselben CommandId) gleichzeitig, dabei der Host der Beispieldomäne hart beendet und nach 3 s neu
gestartet, Redis 10 s gestoppt, der Host der Prüfdomäne hart beendet; danach dieselbe Prüfung. Ablage: `vermessung/ergebnisse/<datum>_<commit>_<anlass>/`
(nur anhängen); die Tabelle erzeugt `scripts/vermessung-tabelle.sh --schreibe` aus den gültigen Läufen des jüngsten Commits.

### Stand

<!-- vermessung-tabelle:anfang -->
Commit `d394352`, Messserver; Leistung: [2026-10-16_1255_d394352_server-leistung-5](../vermessung/ergebnisse/2026-10-16_1255_d394352_server-leistung-5/), [2026-10-16_1301_d394352_server-leistung-6](../vermessung/ergebnisse/2026-10-16_1301_d394352_server-leistung-6/) (je Zelle Median über 6 Wiederholungen, in Klammern die Spanne, wenn sie 5 % übersteigt); Störung: [2026-10-16_1249_d394352_server-stoerung-4](../vermessung/ergebnisse/2026-10-16_1249_d394352_server-stoerung-4/), [2026-10-16_1252_d394352_server-stoerung-5](../vermessung/ergebnisse/2026-10-16_1252_d394352_server-stoerung-5/).

| Szenario | Durchsatz (/s) | Schreiben angenommen (/s) | Latenz p50 (ms) | Latenz p99 (ms) | Reaktion p99 (ms) | Transaktionen je Vorgang | Queries je Vorgang | CPU je Vorgang (ms) |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Bildeingang: Quelle, Fluss, Projektion (Paare) | 82,8 (79,9–85,7) | 3 586 (3 092–4 737) | 3 633 (3 481–3 843) | 7 026 (6 752–7 317) | – | 20,9 | 146 | 117 (111–122) |
| Heißer Schreiber: ein Sammelvorgang, 64 Sender (Teile) | 1 039 (846–1 371) | 3 006 (1 740–3 208) | 822 (625–1 165) | 1 174 (863–1 529) | – | 1,1 (1,0–1,2) | 16,7 | 5,3 (3,4–6,1) |
| Verteilt ruhig: Prüfdomäne, 1 Sender (Vorgänge) | 298 (281–309) | 299 (282–311) | 16,6 (14,7–22,8) | 45,0 (34,4–831) | – | 6,9 (5,6–7,4) | 35,4 (31,9–38,2) | 17,8 (13,4–19,3) |
| Verteilt an der Decke: Prüfdomäne, 64 Sender (Vorgänge) | 1 126 (1 077–1 239) | 3 485 (2 896–3 884) | 1 028 (845–1 182) | 1 784 (1 545–1 906) | 266 (102–808) | 1,4 (0,8–1,7) | 27,2 (26,5–28,3) | 6,8 (6,3–8,3) |

| Szenario | Queries (/s) | CPU (% eines Kerns) | Nachlauf (s) | RSS am Ende (MB) | Neuaufbau (Events/s) | Neuaufbau (s) | Neuaufbau (Events) |
|---|---:|---:|---:|---:|---:|---:|---:|
| Leerlauf Beispieldomäne | 3,8 | 3,3 | 61,8 | 2 116 (2 048–2 183) | – | – | – |
| Leerlauf Prüfdomäne | 2,5 | 1,6 | 12,1 (11,0–13,1) | 1 266 | – | – | – |
| Neuaufbau aller Projektionen | – | 163 (144–191) | – | – | 56 776 (42 197–59 318) | 0,3 (0,2–0,3) | 14 480 |

| Störungslauf | Vorgänge Prüfdomäne | Bildpaare | Abbau nach der Last (s) | p99 Prüfdomäne (ms) | Exactly-once |
|---|---:|---:|---:|---:|:-:|
| [2026-10-16_1249_d394352_server-stoerung-4](../vermessung/ergebnisse/2026-10-16_1249_d394352_server-stoerung-4/) | 56 673 | 181 | 10,0 | 2 459 | grün |
| [2026-10-16_1252_d394352_server-stoerung-5](../vermessung/ergebnisse/2026-10-16_1252_d394352_server-stoerung-5/) | 56 069 | 181 | 10,3 | 3 087 | grün |
<!-- vermessung-tabelle:ende -->

### Einschätzung

Stand 2026-10-16, Commit `d394352`, Messserver (Intel i5-13500: 14 Kerne, 20 Threads), ohne Fremdlast; zwei Leistungsläufe mit je drei
Wiederholungen, zwei Störungsläufe. Der Code von Framework und Hosts ist derselbe wie in der letzten Spalte des Archivs („Nachlauf“, `7a1f9f9`) —
dazwischen haben sich nur Tests und Messtreiber geändert.

**Was das System heute schafft.** Ende zu Ende etwa **1 100–1 200 verteilte Prozess-Vorgänge je Sekunde** (je Vorgang ein Auftrag oder eine
Verteilung über drei bis sechs Aggregate, Prozess-Manager, Kompensation, Reaktion), **etwa 1 000 Teile je Sekunde an einem einzigen heißen
Aggregat** (Spanne 850–1 370) und **83 Bildpaare je Sekunde** durch Datei-Quelle, Fluss mit vier Funktionen und Projektionen. Ein Vorgang ohne
Warteschlange ist nach **17 ms** fertig (p99 45 ms; einzelne Wiederholungen mit Ausreißern bis 0,8 s). Die Schreibseite nimmt an der Decke **3 500 Vorgänge
je Sekunde** an (je einer ein Command), am heißen Schreiber 3 000 Teile (6 000 Commands), und kippt unter einem Stoß nicht; die Latenzen der Stoß-Szenarien (≈ 1 s bzw. 3,6 s im Median) sind Warteschlange, keine
Bearbeitungszeit. Ein **Neuaufbau aller fünf Projektionen** liest gut **50 000 Projektions-Events je Sekunde** (14 480 Events in 0,3 s — wegen der
Kürze nur auf etwa ±20 % genau). **Ohne Eingang** ist der Host still: 3,8 bzw. 2,5 Queries je Sekunde, 2–3 % eines Kerns. **Unter Störung**
(zwei harte Host-Abbrüche, 10 s ohne Redis) wirkten 56 000 Vorgänge und 181 Bildpaare je Lauf genau einmal; der Rückstand war 10 s nach dem
Ende der Last abgebaut.

**Wo die Decken liegen und warum** (CPU in Kernen, Median; „Rechner“ = alle Prozesse samt Postgres, aus `ergebnis.json`):

| Szenario | Host | Rechner | Decke |
|---|---:|---:|---|
| Verteilt an der Decke | 7,3 | 12,5 | **die Maschine.** Die Schreibseite nähme dreimal mehr an; jeder Vorgang zieht im Prozess-Manager acht bis zehn Folge-Commands nach sich, 6,8 ms Host-CPU und 27 Queries. Mehr Durchsatz heißt weniger Arbeit je Vorgang oder mehr Knoten (ungemessen). |
| Heißer Schreiber | 4,8 | 8,3 | **ein Single-Writer**, nicht die CPU: die Commits eines Aggregats laufen nacheinander, das Bündel (bis 64 Commands je Append) macht daraus 1,1 Transaktionen je Teil. Die große Spanne zeigt, wie empfindlich das Bündeln am Zeitpunkt hängt. |
| Bildeingang | 9,8 | 14,1 | **die Maschine** — und ein teurer Vorgang: 117 ms Host-CPU, 21 Transaktionen und 146 Queries je Bildpaar. Wohin die CPU geht (vier Funktionen auf 96 × 64-Bildern, Dirigent, fünf Projektionen, 27 Query-Abfragen des Treibers je Paar), ist ungeklärt (PF-16); dazu meldet der Datei-Treiber eine Datei nach der anderen (PF-10). |
| Neuaufbau | 1,6 | 2,2 | **keine Decke**: die Projektionen lesen schneller, als alle Szenarien Events erzeugen. |

**Was die Garantien kosten.** Die Mechanik, die „genau einmal“ und „nichts bleibt liegen“ trägt — Inbox-Quittung, Emittenten-Cursor,
Offen-Index der Vorgänge, Guards und Checkpoints der Projektionen —, macht 5–19 % der Datenbank-Zeit aus (Bildeingang 5 %, heißer Schreiber 10 %,
Verteilt an der Decke 13 %, ruhig 19 %; aus `pg_stat_statements.csv`); den Rest kosten Events schreiben und lesen. Unter Last teilen sich die
Vorgänge ihre Commits (Group-Commit, Bündel): an der Decke 1,4 Transaktionen und 6,8 ms CPU je Vorgang, ruhig 6,9 Transaktionen und 17,8 ms — ein
einzelner Vorgang ist also gut doppelt so teuer wie einer im Gedränge. Das ist der Preis der Einzel-Commits, nicht der Garantien.

**Gegen die Server-Baseline und die letzte Messung davor** (Archiv, gleiche Szenarien unter altem Namen):

| Kennzahl | Baseline (15.10., `ee5fced`) | „Nachlauf“ (16.10., `7a1f9f9`) | jetzt | jetzt / Baseline |
|---|---:|---:|---:|---:|
| Bildeingang, Paare/s (S1) | 57,8 | 82,6 | 82,8 | 1,4 × |
| Heißer Schreiber, Teile/s (S3-64) | 229 | 1 172 | 1 039 (846–1 371) | 4,5 × |
| Verteilt ruhig, Vorgänge/s · Latenz p50 (S5-1) | 224 · 37 ms | 296 · 16 ms | 298 · 17 ms | 1,3 × · 0,45 × |
| Verteilt an der Decke, Vorgänge/s (S5-64) | 206 | 1 281 | 1 126 (1 077–1 239) | 5,5 × |
| Reaktion p99 an der Decke | 52 ms | 330 ms | 266 ms | – |
| Leerlauf Queries/s Beispiel · Prüfdomäne (L1 · L5) | 351 · 2 293 | 3,8 · 2,6 | 3,8 · 2,5 | 0,01 × · 0,001 × |

Heißer Schreiber und Verteilt an der Decke liegen 0,9 × unter der Spalte „Nachlauf“, bei gleichem Code: gemessen wird jetzt dreimal im selben
Host mit wachsendem Log statt eines Blocks nach dem Zwischenpunkt mit 8 Sendern; die Spanne der neuen Läufe reicht über die alten Werte. Das ist
Messverfahren und Streuung, keine Regression.

**Was ungemessen bleibt:** mehr als ein Knoten und eine Datenbank über das Netz (alle Zahlen sind ein Rechner, Postgres lokal); Dauerbetrieb
(Speicher über Stunden, Passivierung nach 120–600 s, Aufräumen der Inbox über den Horizont); große Logs (Neuaufbau und Polls über Millionen
Events); viele offene Vorgänge im Leerlauf (PF-4b); der Durchsatz externer Python-Worker (seine Richtigkeit prüft die Integration); die CPU von
Postgres getrennt vom Rest des Rechners; die Kosten des Read-your-writes-Index (PF-6) für sich.

### Weitere Messwerte

- **Werkbank (2026-10-12/13):** warmes Lesen nach Text-Änderung p50 224 ms (`GraphExtractor -- --warm`); Geste p50 unter Fremdlast 346–470 ms
  (Ziel 300 ms, WB-1).
- **Negativ-Befunde, die nicht erneut untersucht werden müssen** (Schreibpfad, 2026-08): Serialisierung ist für den Durchsatz irrelevant (der
  STJ-Source-Gen-Serializer bleibt opt-in, `Schreibpfad.GeneriertesJson`); synchroner Redis-Zugriff im Command-Turn ist bei localhost-Redis
  irrelevant. Alle übrigen Messungen bis 2026-10-16 (Phasen, Postgres-Grundgrößen, A-B-B-A-Vergleiche): [Messarchiv](12-anhang-messarchiv.md).

**Als korrekt verifiziert (Backend-Audit 2026-08, nicht erneut jagen):** Versionsarithmetik inkl. Noop- und Ablehnungs-Marken; OCC-Skip auf dem
`AnyVersion`-Pfad; Append vor State-Mutation; Shard-Hash-Konsistenz Subscribe ↔ gezieltes Publish; Pre-Dispatch-Guard des Adapters je Stream (heute `EmittentenAdapter`);
Signal→Wake-Mapping; Kompensations-Dedup und Count-Join ohne verfrühtes Feuern; Snapshot-Schema-Gating; Metadaten-Readback.

## 12.6 Wie man einen Test schreibt

- **Prüfstand:** ein Aggregat über die generierte Fabrik treiben, FluentAssertions, Methodennamen `Deutsch_mit_Unterstrich`, Ordner nach Thema
  (nie nach Plan-Phase). Muster: `Beispieldomaene/DatensatzAggregatTests.cs`, `Beispieldomaene/SammelvorgangBarriereTests.cs`. Analyzer und Generatoren über eine eigene `CSharpCompilation`
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

**Garantie-Test:** in den Ordner `Garantien/` der passenden Ebene (Prüfstand über Seams, sonst gegen echtes Marten) und die Zeile in der
Nachweis-Spalte von [11](11-garantien.md) nachtragen — `GarantieMatrixTests` wird sonst rot. Zeigt der Test eine offene Lücke:
`[Fact(Skip = "G-n offen (docs/13)")]` — und einmal ohne Skip laufen lassen, er muss rot sein. Wer eine Garantie-Regel ändert, sieht ihren Test
einmal mit sabotierter Regel rot (auf dem Server: sabotieren, nur den Test laufen lassen, `git checkout -- .`, neu bauen). Störungs-Seams der
Prüfdomäne: `Pruefstand.Cluster(…, inboxCache, dienste, schema)`, `Pruefstand.Dekoriere<T>`, `Pruefstand.FrageAsync` (Command mit vorgegebener
CommandId, Modus `Emittiert`).

**Ende zu Ende messen:** `scripts/server.sh lauf scripts/vermessung-e2e.sh --anlass server-<anlass>` (Leistung, ≈ 6 min) bzw. `… --stoerung --anlass
server-<anlass>-stoerung` (≈ 4 min). Der Lauf legt `vermessung/ergebnisse/<datum>_<commit>_<anlass>/` an (nur anhängen) und holt ihn zurück; danach
`scripts/vermessung-tabelle.sh --schreibe` und den Ordner mitcommitten. Für eine Aussage „besser oder schlechter“ zwei Läufe je Stand — die Tabelle
zeigt die Spanne. Das Skript schaltet `pg_stat_statements` und `max_connections = 300` am Postgres-Container ein (einmaliger Neustart) und startet
den Redis der Vermessung.

**Messserver:** Ebene 2 und 3 laufen auf einem eigenen Rechner (Hetzner, bare metal, Intel i5-13500 mit 6 P- und 8 E-Kernen/20 Threads, 62 GiB,
NVMe-RAID1, Ubuntu 24.04, keine GPU). Der Code kommt nur über GitHub dorthin: `scripts/server.sh lauf <befehl …>` verlangt einen gepushten `HEAD`,
checkt ihn auf dem Server aus (`/opt/bractor`, detached, vorher `git clean`), führt den Befehl in tmux aus (übersteht SSH-Abbrüche), liest die Ausgabe
mit und holt neue Ordner aus `vermessung/ergebnisse/` zurück; `--los` startet ohne zu warten, `warte`/`status`/`log` holen später ab. Ein Lauf zur
Zeit. Arbeitsweise: lokal committen, auf einen Wegwerf-Zweig pushen (`git push -f origin HEAD:refs/heads/pruef/<name>`), dort `./pruefe.sh alles`
(und Gegenproben, Messung) laufen lassen, erst grün den eigentlichen Zweig pushen und den Prüfzweig löschen. `scripts/server.sh einrichten`
(idempotent, `scripts/server-einrichten.sh`) installiert das .NET-SDK in der Version des Macs, die Python-Umgebung der Worker (torch nur CPU,
verlinkt unter dem Pfad aus `Umgebung:Python`), die Bibliotheken der nativen OpenCvSharp-Runtime und startet die Infrastruktur-Container
(Postgres und Redis im Host-Netzwerk, `docker-compose.messserver.yml`; alles nur auf 127.0.0.1). Die übrigen Dienste des Servers (Caddy, andere
Apps) bleiben unberührt. `dotnet test` schreibt dort englisch (`Passed!`/`Failed!`).

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
- `Skip` gibt es nur für offene G-Lücken (`G-n offen (docs/13)`). Flake-Handling bleibt Konvention.
- **Messung:** `pg_stat_database` schreibt seine Zähler verzögert fort (Sekunden) — für Messfenster taugt nur `pg_stat_statements`; mit
  `track = all` zählte es auch Statements innerhalb von Funktionen (Martens `mt_immutable_timestamptz` je Zeile) mit. Fremdlast auf dem Rechner
  (ein Spiel, ein Build) verschiebt den Durchsatz um den Faktor 2 und mehr — sie steht je Szenario im Ergebnis; Läufe darunter vergleichen nur
  Verhältnisse. Ebene 3 nie gleichzeitig mit Ebene 2 oder einer anderen Messung; `./pruefe.sh alles` startet Integration, Prüfdomäne und
  Werkbank-Abnahme nacheinander (ein `dotnet test` auf der Solution startete sie parallel).
- **Kindprozesse mit umgeleiteter Ausgabe:** `dotnet build/restore/run` startet MSBuild-Knoten und den Compiler-Server, die die Pipe erben —
  wer auf das Ende der Ausgabe wartet (`WaitForExitAsync`, `ReadToEnd`), hängt bis zum Zeitlimit. Auf das Prozessende warten (`Werkbank/Server/Prozesse.cs`).

## 12.8 Prinzipien

1. **Nie faken, was man nicht besitzt** — Store-Semantik nur gegen echtes Marten.
2. **Die Testebene folgt der Frage, nicht dem Feature.**
3. **Generative Vollständigkeits-Guards** statt punktueller Fälle (Wire-Boot-Check, Azyklizitäts-Guard, „Code ohne Karte: 0“).
4. **Beweiskraft explizit** — jede Integrationsklasse sagt, warum grün = bewiesen.
5. **Determinismus durch Sequentialität und direktes Treiben.**
6. **Domänenneutral testen:** Framework-Verhalten gehört in eine Test- oder Sonde-Domäne, nicht an die Beispieldomäne gebunden (W11).
