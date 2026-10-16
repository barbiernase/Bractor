# 12 — Anhang: Messarchiv (bis 2026-10-16)

**Eingefroren.** Die Messungen bis zur Vereinfachung der Vermessung am 2026-10-16 — Höchstlast-Blöcke S1–S5 mit Leerlauf L1/L5 (Format 1 der
Ergebnis-Ordner), die Phasen des Plans Garantien und Durchsatz, die Postgres-Grundgrößen und die Tabellen beider Rechner. Nichts hier wird
fortgeschrieben; die laufende Messung und ihre Tabelle stehen in [12](12-tests-und-vermessung.md) §12.5. Die Ergebnis-Ordner liegen weiter unter
`vermessung/ergebnisse/` (nur anhängen). Szenarien von damals: S1 Bildeingang (Stoß 600 Paare), S2 derselbe mit Python-Worker (200 Paare), S3-8/S3-64
heißer Sammelvorgang (8 bzw. 64 Sender, höchstens 2 000 Teile), S4 Datensatz einfrieren, S5-1/8/64 Prüfdomäne verteilt (1/8/64 Sender, höchstens
3 000 Vorgänge), L1/L5 Leerlauf; heute entsprechen S1 dem Bildeingang, S3-64 dem heißen Schreiber, S5-1 und S5-64 Verteilt ruhig und an der Decke.
Die Werkzeuge von damals — Diagnose `--art leerlauf`, `--probe`, `scripts/vermessung-postgres.sh`, A-B-B-A über einen Worktree — gibt es nicht mehr;
sie stehen im Git-Verlauf.

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
| L1, L5 Leerlauf | ohne Eingang nach den Blöcken: erst der Nachlauf, bis alle Poll-Cursor und Projektions-Positionen am Kopf des Logs stehen (höchstens 180 s; Dauer und Queries als eigene Kennzahlen), dann 60 s (zwei Poll-Perioden) — bis 2026-10-16 15 s, 5 s nach dem letzten Block (PF-14) | Grundlast (PF-2, PF-4, PF-14) |

Kennzahlen je Block: angenommen je Sekunde (Schreibseite), sichtbar je Sekunde im Fenster und Durchsatz bis „alles sichtbar“ (Ende zu Ende),
Rückstand am Fensterende und Abbauzeit (ein nicht abgebauter Rückstand ist ein Ergebnis), Latenz p50/p95/p99, Transaktionen und Queries je
Sekunde und je Vorgang (`pg_stat_statements`, `track = top`, Transaktionen = COMMITs, ohne die Lese-Rolle des Treibers), CPU und RSS des Hosts.
Eine Aufwärmrunde vor S1 und S5 zählt nicht. Jeder Lauf auf frischen DBs; danach die Exactly-once-Prüfung ([11](11-garantien.md) §11.14) — mit
offenem Rückstand prüft sie „höchstens einmal, nichts doppelt“. Ablage: `vermessung/ergebnisse/<datum>_<commit>_<anlass>/` (nur anhängen); die
Tabellen erzeugt `scripts/vermessung-tabelle.sh --schreibe` aus diesen Ordnern — nie von Hand, je Rechner eine mit eigener Baseline
(MacBook Air M4 hier, der Messserver weiter unten; Werte verschiedener Rechner sind nicht vergleichbar).

**Tabelle MacBook Air M4** (bis Phase 3, unter Fremdlast):

| Szenario | Kennzahl | Baseline ([2026-10-15_0429_3cfec32_baseline](../vermessung/ergebnisse/2026-10-15_0429_3cfec32_baseline/)) | Phase 1 ([2026-10-15_0539_b955d40_phase-1](../vermessung/ergebnisse/2026-10-15_0539_b955d40_phase-1/)) | Phase 2 ([2026-10-15_0745_4caa176_phase-2](../vermessung/ergebnisse/2026-10-15_0745_4caa176_phase-2/)) | Phase 3 ([2026-10-15_1312_0b02802_phase-3](../vermessung/ergebnisse/2026-10-15_1312_0b02802_phase-3/)) | Phase 3 / Baseline |
|---|---|---:|---:|---:|---:|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 258 | 1600 | 1536 | 0.1 | 0× |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 777 | 5472 | 5182 | 602 | 0.77× |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 87.2 | 324 | 337 | 33.3 | 0.38× |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1743 | 2120 | 1875 | 617 | 0.35× |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 162 | 305 | 226 | 93.1 | 0.57× |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 3739 | 3588 | 4215 | 955 | 0.26× |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 203 | 158 | 244 | 160 | 0.79× |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 976 | 775 | 991 | 591 | 0.61× |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 1474 | 1583 | 1819 | 1360 | 0.92× |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 4.9 | 13.2 | 3 | 13.6 | 2.77× |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 19.9 | 31.2 | 33.3 | 26.2 | 1.32× |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 598 | 595 | 599 | 594 | 0.99× |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 29.8 | 18.8 | 17.7 | 22.4 | 0.75× |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 17286 | 11296 | 10354 | 11223 | 0.65× |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 29476 | 18725 | 17565 | 22243 | 0.75× |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 29.8 | 30.2 | 30.6 | 27.8 | 0.93× |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 258 | 228 | 227 | 239 | 0.93× |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 591 | 937 | 1016 | 728 | 1.23× |
| S1 Bildeingang Host, Stoß | Queries (/s) | 5104 | 7074 | 7532 | 6244 | 1.22× |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 360 | 370 | 393 | 278 | 0.77× |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1061 | 1287 | 1184 | 1061 | 1× |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 2424 | 1564 | 1517 | 1749 | 0.72× |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0 | 0 | 0 | 0 | – |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 16 | 23.3 | 22.8 | 20.6 | 1.28× |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 | 200 | 200 | 200 | 1× |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 12.4 | 8.5 | 8.7 | 9.6 | 0.77× |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 6154 | 4301 | 4777 | 5245 | 0.85× |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 12279 | 8464 | 8549 | 9498 | 0.77× |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 55.1 | 43.5 | 48 | 28.5 | 0.52× |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 314 | 306 | 305 | 209 | 0.67× |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 879 | 1002 | 1081 | 583 | 0.66× |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 5007 | 7046 | 6860 | 4286 | 0.86× |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 281 | 289 | 284 | 259 | 0.92× |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 69.8 | 79.2 | 77.7 | 105 | 1.5× |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1341 | 1776 | 1220 | 1459 | 1.09× |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 505 | 1137 | 1010 | 951 | 1.88× |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 21.2 | 40.4 | 39.4 | 39.9 | 1.88× |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 50.3 | 98 | 90.3 | 64.3 | 1.28× |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1916 | 1929 | 1922 | 1916 | 1× |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 35.8 | 18.6 | 20.2 | 29 | 0.81× |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0.6 | 0 | 0 | 0 | 0.04× |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 22828 | 11269 | 11941 | 14648 | 0.64× |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 35756 | 18819 | 20330 | 28669 | 0.8× |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 10.9 | 11.8 | 11.8 | 5.7 | 0.52× |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 67.3 | 59 | 60.7 | 39.3 | 0.58× |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 544 | 1144 | 1057 | 363 | 0.67× |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 3365 | 5740 | 5428 | 2509 | 0.75× |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 369 | 416 | 428 | 317 | 0.86× |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2725 | 2549 | 2388 | 2363 | 0.87× |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 119 | 195 | 265 | 164 | 1.38× |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 39.3 | 65.6 | 88 | 63.6 | 1.62× |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 54.4 | 105 | 100 | 83.8 | 1.54× |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1207 | 1326 | 1335 | 1225 | 1.01× |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 17.9 | 8.9 | 12.4 | 11.7 | 0.65× |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 2.7 | 0 | 0 | 0 | 0.02× |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 11111 | 5148 | 5222 | 10401 | 0.94× |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 21101 | 14858 | 15096 | 18085 | 0.86× |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 10.5 | 8.3 | 8.5 | 6.2 | 0.6× |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 57.6 | 50.6 | 50.4 | 43.8 | 0.76× |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 563 | 854 | 844 | 517 | 0.92× |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 3097 | 5227 | 4998 | 3632 | 1.17× |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 307 | 441 | 443 | 332 | 1.08× |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1843 | 2636 | 2407 | 1870 | 1.01× |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1091 | 1826 | 1828 | 1397 | 1.28× |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0.1 | 0.1 | 0.1 | 0.1 | 0.91× |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0.4 | 0.2 | 0.2 | 0.3 | 0.74× |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0 | 0 | 0 | 0 | 1× |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 6.4 | 3.2 | 3.2 | 3.2 | 0.5× |
| S4 Datensatz einfrieren | Transaktionen (/s) | 28.5 | 41.1 | 42.4 | 37.1 | 1.3× |
| S4 Datensatz einfrieren | Queries (/s) | 5564 | 4002 | 4134 | 3629 | 0.65× |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 258 | 216 | – | 195 | 0.76× |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 774 | 1307 | – | 823 | 1.06× |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 108 | 149 | 131 | 85.5 | 0.79× |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 107 | 147 | 130 | 84.2 | 0.79× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 108 | 149 | 131 | 85.2 | 0.79× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 21 | 27 | 17 | 20 | 0.95× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0 | 0 | 0 | 0.1 | 1.47× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 57.4 | 42.6 | 47.2 | 70.7 | 1.23× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 105 | 74.7 | 92 | 183 | 1.74× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10.6 | 10.5 | 10.5 | 10.6 | 1× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 66.4 | 63.9 | 63.7 | 63.9 | 0.96× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 1136 | 1556 | 1355 | 887 | 0.78× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 7099 | 9449 | 8217 | 5355 | 0.75× |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 365 | 368 | 377 | 320 | 0.88× |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 987 | 1329 | 997 | 959 | 0.97× |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2181 | 2956 | 2356 | 2125 | 0.97× |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0 | 0 | 0 | 0 | – |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 126 | 144 | 128 | 30.4 | 0.24× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 | 3000 | 3000 | 3000 | 1× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 22.4 | 19.9 | 22.1 | 97.2 | 4.35× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0 | 0 | 0 | 0.1 | 2.63× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 14771 | 12782 | 13996 | 46274 | 3.13× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 22324 | 19822 | 21980 | 96761 | 4.33× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 27.4 | 17.4 | 21 | 42.7 | 1.56× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 190 | 127 | 145 | 451 | 2.37× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6.1 | 6 | 6.3 | 6.7 | 1.1× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 59.1 | 62.1 | 61.2 | 66.6 | 1.13× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 766 | 860 | 799 | 203 | 0.27× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 7380 | 8854 | 7752 | 2015 | 0.27× |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 350 | 343 | 376 | 162 | 0.46× |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1063 | 1112 | 1024 | 919 | 0.86× |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 333 | 423 | 399 | 248 | 0.75× |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 163 | 183 | 174 | 103 | 0.64× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 138 | 171 | 138 | 111 | 0.8× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1535 | 1699 | 1692 | 1751 | 1.14× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 12.7 | 10.5 | 14.3 | 15 | 1.18× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 5179 | 4700 | 4702 | 9227 | 1.78× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 12607 | 10458 | 14210 | 14924 | 1.18× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 6.8 | 7 | 7.2 | 6.7 | 0.98× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 61.8 | 61.4 | 62.1 | 61.5 | 1× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 931 | 1181 | 975 | 731 | 0.79× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 8440 | 10307 | 8426 | 6735 | 0.8× |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 325 | 304 | 292 | 283 | 0.87× |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1064 | 1116 | 1048 | 934 | 0.88× |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | – | – | 1175 | 152 | – |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | – | – | 2407 | 2363 | – |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | – | – | 0.5 | 0.1 | – |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | – | – | 63827 | 10280 | – |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | – | – | 63827 | 10280 | – |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | – | – | 980 | 434 | – |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | – | – | 1048 | 959 | – |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | – | – | 0.9 | 0.5 | – |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | – | – | 25622 | 19745 | – |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | – | – | 25622 | 19745 | – |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | – | – | 1068 | 585 | – |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | – | – | 1184 | 1061 | – |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | – | – | 0.9 | 0.6 | – |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | – | – | 4840 | 2254 | – |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | – | – | 4840 | 2254 | – |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | – | – | 1044 | 1113 | – |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | – | – | 1220 | 1459 | – |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | – | – | 0.9 | 0.8 | – |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | – | – | 10661 | 1056 | – |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | – | – | 10661 | 1056 | – |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | – | – | 2027 | 1467 | – |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | – | – | 2407 | 2363 | – |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | – | – | 0.8 | 0.6 | – |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | – | – | 30235 | 10280 | – |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | – | – | 30235 | 10280 | – |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | – | – | 1667 | 1781 | – |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | – | – | 2407 | 1870 | – |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | – | – | 0.7 | 1 | – |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | – | – | 13490 | 6279 | – |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | – | – | 13490 | 6279 | – |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | – | – | 4854 | 2267 | – |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | – | – | 4854 | 2267 | – |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | – | – | 915 | 765 | – |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | – | – | 1013 | 959 | – |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | – | – | 0.9 | 0.8 | – |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | – | – | 4527 | 3218 | – |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | – | – | 4527 | 3218 | – |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | – | – | 930 | 512 | – |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | – | – | 1048 | 959 | – |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | – | – | 0.9 | 0.5 | – |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | – | – | 19972 | 18782 | – |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | – | – | 19972 | 18782 | – |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | – | – | 1024 | 363 | – |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | – | – | 1048 | 959 | – |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | – | – | 1 | 0.4 | – |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | – | – | 10993 | 9885 | – |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | – | – | 10993 | 9885 | – |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | – | – | – | 823 | – |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | – | – | – | 1061 | – |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | – | – | – | 0.8 | – |

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

Phase 3 (2026-10-15, `0b02802`; Batch-Konsum der Projektionen), gemessen unter starker Fremdlast (ein Spiel lief, Last 10 → 27 im
Höchstlast-Lauf, bis 82 im ersten Störungslauf; die Docker-VM zuletzt bei 330 % CPU) — die absoluten Zahlen sind deshalb schlechter als in
Phase 2, auch dort, wo Phase 3 nichts geändert hat (S5 hat keine Projektion, S5-64 0,24 × Phase 2). Was der Lauf trotzdem klar zeigt:
- **Leerlauf L1 ist leer:** 0,1 Transaktionen/s (Phase 2: 1 536), 602 Queries/s (5 182; der Rest sind Stream-Reads des Prozess-Polls, PF-4),
  33 % CPU (337 %), 10 280 aktive Actors (63 827), RSS am Fensterende 0,1 des Höchststands. Der Heil-Poll je Stream jeder Projektion ist weg
  (PF-2); im L1-Fenster liefen genau 40 Seiten-Abfragen (5 Projektionen × 8 Partitionen, ein Poll).
- **Weniger Arbeit je Vorgang dort, wo Projektionen oder ihr Heil-Poll mitliefen:** S2 Transaktionen je Paar 0,59 × Phase 2 (28,5 statt 48),
  Queries 0,69 ×; S3 Transaktionen je Teil 0,48 × (64 Sender) bzw. 0,73 × (8 Sender) — die Erstweckungen jedes neuen S3-Streams durch jede
  Projektion entfallen.
- **S1 ist durch die Quelle begrenzt, nicht durch den Konsum:** je Sekunde werden genau halb so viele Paare komplett, wie der Datei-Treiber
  Dateien ins Log meldet (Zeitachse des Logs, gleich für Phase-2- und Phase-3-Code) — er meldet eine Datei nach der anderen (Rückstau,
  [11](11-garantien.md) §11.8), und die Leseseite hält in Echtzeit mit. Die Seiten der Partitions-Leser bleiben unter diesem Tröpfeln klein
  (≈ 3,5 Events je Commit; je Paar 7,6 Seiten und 5,8 Partitions-Commits). Von 239 Queries je Paar sind 64 das 100-ms-Polling des Treibers auf
  `GetImagePair` (wächst mit der Latenz) und 50 Stream-Reads des Dirigenten (PF-4).
- **A-B-B-A** (nur S1 und S5, A = Phase-2-Code `ee401da` aus einem Worktree, B = `0b02802`; Ordner `…phase-3-ab-s1s5-*`): S1 28,6 / 23,0 / 22,2 /
  11,3 Paare/s — im Rauschen der Fremdlast (A2 lief bei Last 31); S5-8 125 / 146 / 154 / 76, S5-64 97 / 120 / 122 / 130 Vorgänge/s — Phase 3 kostet
  S5 nichts.
- **Störungslauf:** der erste (`…_1333_0b02802_phase-3-stoerung`, Last bis 82) ist **ungültig** — nicht doppelt gewirkt (die Datenbank danach im
  Ruhezustand: jede CommandId höchstens einmal, keine doppelte Version, Reaktion 21 927 = 21 927), sondern nicht abgebaut: zwei Aufträge, deren
  Start-Signal beim harten Abbruch des Prüfdomäne-Hosts verloren ging, kann nur der Prozess-Poll starten; dessen Cursor war vor dem Abbruch noch
  nie geschrieben, also begann er nach dem Neustart bei 0 und liest jeden geänderten Stream ab Version 0 (PF-4) — unter dieser Last kam er in
  10 min nicht durch. Die Abweichung der Reaktions-Zeile (21 648 statt 21 646) ist ein Zählfehler des Treibers: Soll und Ist werden in zwei
  Abfragen gezählt, während der Host noch abbaut. Der zweite (`…_phase-3-stoerung-2`, Last 8 → 68) ist gültig: 24 699 Vorgänge und 182 Bildpaare
  mit zwei harten Abbrüchen und Redis-Ausfall, alles genau einmal, Abbau 371 s.

**Tabelle Messserver** (Hetzner, §12.6; ohne Fremdlast, Postgres und Redis im Host-Netzwerk — die Läufe vor der Baseline-Spalte liefen
noch über docker-proxy):

| Szenario | Kennzahl | Baseline ([2026-10-15_1652_ee5fced_server-baseline](../vermessung/ergebnisse/2026-10-15_1652_ee5fced_server-baseline/)) | PF-4 ([2026-10-15_1821_ffaeaea_server-pf-4](../vermessung/ergebnisse/2026-10-15_1821_ffaeaea_server-pf-4/)) | PF-12 ([2026-10-15_1940_5bda1bb_server-pf-12](../vermessung/ergebnisse/2026-10-15_1940_5bda1bb_server-pf-12/)) | Ohne Reset ([2026-10-15_2012_3011a1e_server-ohne-reset](../vermessung/ergebnisse/2026-10-15_2012_3011a1e_server-ohne-reset/)) | Korreliert lesen ([2026-10-15_2021_6e361a6_server-korreliert](../vermessung/ergebnisse/2026-10-15_2021_6e361a6_server-korreliert/)) | Inbox-Tabelle ([2026-10-15_2152_aef3d46_server-inbox](../vermessung/ergebnisse/2026-10-15_2152_aef3d46_server-inbox/)) | Schnell anhängen ([2026-10-15_2207_cf30aa0_server-quick](../vermessung/ergebnisse/2026-10-15_2207_cf30aa0_server-quick/)) | Prozess-Manager ([2026-10-16_0026_6a437ba_server-prozess-manager](../vermessung/ergebnisse/2026-10-16_0026_6a437ba_server-prozess-manager/)) | Bündel ([2026-10-16_0146_2390f1e_server-buendel](../vermessung/ergebnisse/2026-10-16_0146_2390f1e_server-buendel/)) | Nachlauf ([2026-10-16_0309_7a1f9f9_server-nachlauf](../vermessung/ergebnisse/2026-10-16_0309_7a1f9f9_server-nachlauf/)) | Nachlauf / Baseline |
|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0.1 | 0 | 0 | 0 | 7.5 | 5.7 | 5.7 | 5.7 | 5.7 | 0 | 0× |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 351 | 2.5 | 2.5 | 2.3 | 38.1 | 29.2 | 29.2 | 29.2 | 29.6 | 3.8 | 0.01× |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 24.5 | 3.7 | 6.3 | 3 | 7.4 | 6.3 | 7.3 | 7.4 | 7.4 | 2.6 | 0.11× |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 2143 | 2310 | 3011 | 3266 | 2078 | 2104 | 2125 | 2154 | 1784 | 2019 | 0.94× |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1895 | 2309 | 2894 | 3265 | 2077 | 2104 | 2124 | 1900 | 1722 | 2019 | 1.07× |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 2655 | 2420 | 3011 | 3266 | 2150 | 2104 | 2132 | 2154 | 1920 | 2139 | 0.81× |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 0.7 | 1 | 1 | 1 | 1 | 1 | 1 | 0.9 | 0.9 | 0.9 | 1.32× |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10280 | 10280 | 10280 | 10280 | 10280 | 10280 | 10280 | 10280 | 10280 | 10280 | 1× |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10280 | 10280 | 10280 | 10280 | 10280 | 10280 | 10280 | 10280 | 10280 | 10280 | 1× |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 381 | 390 | 0 | 0 | 8.5 | 7.4 | 6.6 | 11.8 | 10.7 | 0 | 0× |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2293 | 1932 | 2.5 | 2.5 | 41.5 | 91.3 | 85.6 | 110 | 113 | 2.6 | 0× |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 145 | 140 | 3.4 | 1.8 | 6.9 | 9.7 | 8.4 | 11.7 | 10.2 | 1.5 | 0.01× |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 1442 | 1471 | 1520 | 1780 | 1394 | 1273 | 1281 | 1218 | 1208 | 1416 | 0.98× |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 1384 | 1339 | 1520 | 1717 | 1394 | 1263 | 1281 | 1217 | 1145 | 1414 | 1.02× |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1563 | 1533 | 1612 | 1811 | 1394 | 1323 | 1389 | 1218 | 1213 | 1416 | 0.91× |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0.9 | 0.9 | 0.9 | 0.9 | 1 | 1 | 0.9 | 1 | 0.9 | 1 | 1.13× |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 27983 | 27630 | 18436 | 18436 | 18436 | 18436 | 18436 | 18436 | 18436 | 18436 | 0.66× |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 27983 | 27630 | 18436 | 18436 | 18436 | 18436 | 18436 | 18436 | 18436 | 18436 | 0.66× |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 4805 | 5648 | 5803 | 5125 | 5772 | 4731 | 3700 | 3573 | 4960 | 4493 | 0.94× |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0 | 0 | 0 | 0 | 0 | 0 | 30.8 | 6 | 0 | 15 | – |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 57.8 | 56.3 | 55.8 | 61.8 | 56.5 | 60.8 | 62 | 89.2 | 89 | 82.6 | 1.43× |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 | 600 | 600 | 600 | 600 | 600 | 595 | 599 | 600 | 598 | 1× |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 10.3 | 10.5 | 10.6 | 9.6 | 10.5 | 9.7 | 9.5 | 6.6 | 6.6 | 7.1 | 0.69× |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5363 | 5296 | 5309 | 4982 | 5072 | 5043 | 4863 | 3371 | 3407 | 3488 | 0.65× |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 10163 | 10447 | 10546 | 9498 | 10415 | 9650 | 9408 | 6458 | 6619 | 7134 | 0.7× |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 28.5 | 28.5 | 28.5 | 28.5 | 28.3 | 28.4 | 28.4 | 21 | 21.2 | 21.1 | 0.74× |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 218 | 218 | 218 | 215 | 216 | 216 | 192 | 144 | 145 | 146 | 0.67× |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1633 | 1586 | 1575 | 1753 | 1584 | 1708 | 1756 | 1858 | 1858 | 1733 | 1.06× |
| S1 Bildeingang Host, Stoß | Queries (/s) | 12507 | 12142 | 12082 | 13249 | 12085 | 13026 | 11899 | 12732 | 12665 | 11958 | 0.96× |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 786 | 768 | 814 | 806 | 822 | 803 | 820 | 994 | 984 | 1048 | 1.33× |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1705 | 1738 | 1730 | 1987 | 1876 | 1710 | 1799 | 1860 | 1583 | 1792 | 1.05× |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1699 | 1709 | 1650 | 1986 | 1827 | 1689 | 1794 | 1860 | 1583 | 1792 | 1.05× |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1705 | 1738 | 1730 | 1987 | 1876 | 1710 | 1799 | 1860 | 1583 | 1792 | 1.05× |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1× |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 | 2254 | 2254 | 2254 | 2254 | 2254 | 2254 | 2254 | 2254 | 2254 | 1× |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 | 2254 | 2254 | 2254 | 2254 | 2254 | 2254 | 2254 | 2254 | 2254 | 1× |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | 3900 | 3611 | 4025 | 5408 | 6772 | 3662 | 4697 | 2393 | 4089 | 2267 | 0.58× |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | – |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 50.1 | 55.7 | 57 | 57.1 | 57.5 | 58.6 | 60.1 | 85.6 | 86.8 | 89.3 | 1.78× |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | 200 | 200 | 200 | 200 | 200 | 200 | 200 | 200 | 200 | 200 | 1× |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | 3.9 | 3.5 | 3.5 | 3.5 | 3.4 | 3.4 | 3.3 | 2.3 | 2.3 | 2.2 | 0.55× |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | 2087 | 1890 | 1824 | 1920 | 1851 | 1780 | 1758 | 1258 | 1230 | 1159 | 0.56× |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | 3942 | 3535 | 3462 | 3465 | 3349 | 3361 | 3187 | 2253 | 2254 | 2161 | 0.55× |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | 28.6 | 28.6 | 28.7 | 28.6 | 28.6 | 28.6 | 28.6 | 21.4 | 21.6 | 21.7 | 0.76× |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | 191 | 189 | 189 | 189 | 189 | 189 | 166 | 127 | 127 | 126 | 0.66× |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | 1410 | 1554 | 1613 | 1610 | 1618 | 1652 | 1707 | 1790 | 1833 | 1875 | 1.33× |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | 9423 | 10263 | 10615 | 10625 | 10692 | 10904 | 9916 | 10579 | 10793 | 10937 | 1.16× |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | 435 | 327 | 408 | 353 | 395 | 362 | 356 | 397 | 365 | 357 | 0.82× |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | 318 | 420 | 337 | 376 | 335 | 338 | 385 | 591 | 534 | 445 | 1.4× |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | 1166 | 1213 | 1117 | 1229 | 1219 | 1241 | 1193 | 891 | 1071 | 1091 | 0.94× |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | 1166 | 1213 | 1117 | 1122 | 1180 | 1080 | 1169 | 891 | 1071 | 1000 | 0.86× |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | 1166 | 1213 | 1117 | 1229 | 1219 | 1241 | 1193 | 973 | 1071 | 1091 | 0.94× |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | 1 | 1 | 1 | 0.9 | 1 | 0.9 | 1 | 0.9 | 1 | 0.9 | 0.92× |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | 1056 | 1056 | 1056 | 1056 | 1056 | 1056 | 1056 | 1056 | 1056 | 1056 | 1× |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 1056 | 1056 | 1056 | 1056 | 1056 | 1056 | 1056 | 1056 | 1056 | 1056 | 1× |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 2559 | 2533 | 1666 | 1658 | 2075 | 2158 | 2209 | 3199 | 3305 | 2811 | 1.1× |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 101 | 95 | 60.8 | 58 | 66.4 | 73.4 | 90.6 | 117 | 411 | 327 | 3.24× |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 229 | 230 | 207 | 214 | 424 | 398 | 510 | 541 | 1350 | 1172 | 5.11× |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1921 | 1925 | 1927 | 1930 | 1936 | 1932 | 1918 | 1927 | 1751 | 1767 | 0.92× |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 7.9 | 7.9 | 8.5 | 8.1 | 3.8 | 4.1 | 3 | 3.1 | 0.9 | 1 | 0.13× |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0.5× |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 3614 | 3561 | 2937 | 2847 | 2157 | 2268 | 1813 | 1480 | 643 | 832 | 0.23× |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 7929 | 7922 | 8563 | 8207 | 3769 | 4093 | 3034 | 3067 | 890 | 1033 | 0.13× |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 5.7 | 5.7 | 5.3 | 5.2 | 5.3 | 5.2 | 5.3 | 2.2 | 1 | 1.2 | 0.21× |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 32.7 | 32.8 | 31.8 | 31.6 | 31.3 | 29.5 | 24.3 | 18.2 | 16.7 | 16.7 | 0.51× |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1262 | 1275 | 1071 | 1105 | 2149 | 1995 | 2569 | 1107 | 1161 | 1208 | 0.96× |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 7311 | 7387 | 6470 | 6659 | 12790 | 11217 | 11742 | 9214 | 19458 | 17396 | 2.38× |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 949 | 933 | 1001 | 1030 | 272 | 267 | 261 | 216 | 444 | 410 | 0.43× |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 2408 | 2384 | 2996 | 3247 | 2073 | 2104 | 2127 | 2150 | 1771 | 2108 | 0.88× |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 2407 | 2306 | 2996 | 3247 | 2073 | 2104 | 2127 | 2150 | 1771 | 2108 | 0.88× |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 2655 | 2420 | 2996 | 3247 | 2150 | 2104 | 2127 | 2150 | 1920 | 2108 | 0.79× |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 0.9 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 0.9 | 1 | 1.1× |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10280 | 10280 | 10280 | 10280 | 10280 | 10280 | 10280 | 10280 | 10280 | 10280 | 1× |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10280 | 10280 | 10280 | 10280 | 10280 | 10280 | 10280 | 10280 | 10280 | 10280 | 1× |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 426 | 498 | 505 | 521 | 881 | 926 | 1018 | 965 | 861 | 858 | 2.02× |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 149 | 176 | 171 | 184 | 302 | 305 | 326 | 335 | 783 | 770 | 5.17× |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 230 | 249 | 248 | 250 | 407 | 389 | 515 | 486 | 859 | 856 | 3.72× |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 1301 | 1292 | 1322 | 1295 | 1315 | 1342 | 1359 | 1306 | 183 | 206 | 0.16× |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 4 | 4 | 4.1 | 4.2 | 2.6 | 3 | 1.9 | 2 | 0 | 0 | 0× |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0.36× |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 3515 | 3181 | 3122 | 3023 | 1600 | 1515 | 1285 | 1400 | 16.5 | 15.9 | 0× |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 4033 | 4031 | 4090 | 4165 | 2635 | 2962 | 1914 | 2035 | 30.5 | 35 | 0.01× |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 6.1 | 6.2 | 6.1 | 6.1 | 6 | 6 | 6.1 | 3 | 1.7 | 1.7 | 0.27× |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 34.1 | 33.2 | 32.2 | 32.2 | 32.1 | 31 | 25 | 19.1 | 17.7 | 17.7 | 0.52× |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1375 | 1476 | 1476 | 1518 | 2419 | 2245 | 2967 | 1393 | 1347 | 1276 | 0.93× |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 7645 | 7958 | 7744 | 7972 | 12836 | 11532 | 12213 | 8863 | 13975 | 13386 | 1.75× |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 922 | 882 | 901 | 832 | 328 | 329 | 359 | 332 | 502 | 436 | 0.47× |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 2655 | 2420 | 2489 | 2645 | 2150 | 2091 | 2076 | 2100 | 1920 | 2075 | 0.78× |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 2402 | 2220 | 2432 | 2599 | 2022 | 2058 | 2075 | 2100 | 1792 | 2075 | 0.86× |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 2655 | 2420 | 2489 | 2645 | 2150 | 2091 | 2076 | 2100 | 1920 | 2075 | 0.78× |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 0.9 | 0.9 | 1 | 1 | 0.9 | 1 | 1 | 1 | 0.9 | 1 | 1.1× |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6279 | 6279 | 6279 | 6279 | 6279 | 6279 | 6279 | 6279 | 6279 | 6279 | 1× |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6279 | 6279 | 6279 | 6279 | 6279 | 6279 | 6279 | 6279 | 6279 | 6279 | 1× |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | 1654 | 1830 | 1710 | 1332 | 1775 | 1714 | 1634 | 1321 | 1784 | 1756 | 1.06× |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | 0.1 | 0.1 | 0.1 | 0.2 | 0.1 | 0.1 | 0.1 | 0.1 | 0.1 | 0.1 | 0.96× |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | 0.2 | 0.2 | 0.2 | 0.2 | 0.2 | 0.2 | 0.2 | 0.3 | 0.2 | 0.2 | 0.93× |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0.7× |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | 3.2 | 3.2 | 3.2 | 3.2 | 3.2 | 3.2 | 3.2 | 3.2 | 3.1 | 3.1 | 0.98× |
| S4 Datensatz einfrieren | Transaktionen (/s) | 39.6 | 45.4 | 41.8 | 34.3 | 42.3 | 40.8 | 40.4 | 23.7 | 29.5 | 30.5 | 0.77× |
| S4 Datensatz einfrieren | Queries (/s) | 3866 | 4442 | 4079 | 3354 | 4134 | 3985 | 3913 | 3258 | 4043 | 4180 | 1.08× |
| S4 Datensatz einfrieren | CPU Host (% eines Kerns) | 455 | – | 417 | – | 389 | 255 | – | 261 | – | – | – |
| S4 Datensatz einfrieren | RSS Host (max.) (MB) | 1791 | – | 1759 | – | 1922 | 1803 | – | 1965 | – | – | – |
| S4 Datensatz einfrieren | RSS Host am Fensterende (MB) | 1791 | – | 1759 | – | 1922 | 1803 | – | 1965 | – | – | – |
| S4 Datensatz einfrieren | RSS Host Höchststand des Laufs (MB) | 1791 | – | 1759 | – | 1922 | 1803 | – | 1965 | – | – | – |
| S4 Datensatz einfrieren | RSS am Ende / Höchststand (1) | 1 | – | 1 | – | 1 | 1 | – | 1 | – | – | – |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | 2267 | 2267 | 2267 | 2267 | 2267 | 2267 | 2267 | 2267 | 2267 | 2267 | 1× |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | 2267 | 2267 | 2267 | 2267 | 2267 | 2267 | 2267 | 2267 | 2267 | 2267 | 1× |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 224 | 263 | 252 | 268 | 267 | 280 | 297 | 299 | 298 | 296 | 1.32× |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 222 | 256 | 249 | 265 | 263 | 273 | 290 | 291 | 293 | 288 | 1.3× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 224 | 263 | 252 | 267 | 266 | 279 | 296 | 298 | 298 | 296 | 1.32× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 31 | 83 | 35 | 31 | 40 | 73 | 64 | 74 | 52 | 78 | 2.52× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0.48× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 37 | 29 | 30.4 | 28.9 | 25.8 | 24.5 | 22.1 | 15.7 | 15.6 | 16.3 | 0.44× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 75 | 57 | 61 | 57.5 | 56.1 | 52.4 | 50.3 | 36.9 | 41.5 | 36.2 | 0.48× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 10.1 | 10.1 | 10 | 10 | 10.2 | 10.2 | 10.5 | 7.4 | 7.4 | 7.3 | 0.73× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 63.5 | 64.1 | 62.8 | 62.8 | 65 | 64.5 | 52.7 | 35.9 | 35.9 | 35.9 | 0.56× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2228 | 2585 | 2481 | 2642 | 2674 | 2776 | 3081 | 2197 | 2151 | 2106 | 0.95× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 14038 | 16415 | 15569 | 16515 | 17027 | 17534 | 15453 | 10611 | 10477 | 10286 | 0.73× |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 776 | 662 | 697 | 671 | 640 | 629 | 671 | 474 | 483 | 454 | 0.58× |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1085 | 1048 | 1069 | 1222 | 1059 | 1120 | 997 | 1014 | 1011 | 1132 | 1.04× |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 1085 | 1034 | 1069 | 1151 | 1024 | 1005 | 967 | 926 | 934 | 1132 | 1.04× |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1085 | 1196 | 1099 | 1222 | 1059 | 1120 | 997 | 1014 | 1011 | 1132 | 1.04× |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 1 | 0.9 | 1 | 0.9 | 1 | 0.9 | 1 | 0.9 | 0.9 | 1 | 1× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6579 | 6842 | 6436 | 6436 | 6436 | 6436 | 6436 | 6436 | 6436 | 6436 | 0.98× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6579 | 6842 | 6436 | 6436 | 6436 | 6436 | 6436 | 6436 | 6436 | 6436 | 0.98× |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 6347 | 5965 | 538 | 450 | 1924 | 1050 | 2723 | 4491 | 3458 | 3171 | 0.5× |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 0 | 0 | 322 | 282 | 396 | 324 | 401 | 693 | 859 | 1018 | – |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 206 | 240 | 348 | 344 | 399 | 451 | 457 | 1246 | 1280 | 1281 | 6.21× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 3000 | 3000 | 1203 | 1122 | 2383 | 2074 | 2558 | 2537 | 2255 | 2037 | 0.68× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 14.1 | 12 | 3 | 2 | 6 | 3.8 | 5.5 | 1.7 | 1.5 | 1.4 | 0.1× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0.7× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 9706 | 7576 | 2501 | 2464 | 3474 | 3432 | 3163 | 1103 | 864 | 746 | 0.08× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 13983 | 11928 | 5160 | 6986 | 6032 | 5362 | 5459 | 1783 | 1489 | 1430 | 0.1× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 7.8 | 6.1 | 5.1 | 4.6 | 21 | 5.1 | 11.6 | 12 | 6.7 | 5.2 | 0.67× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 52 | 40.3 | 2017 | 2422 | 3490 | 2465 | 2671 | 640 | 98.6 | 330 | 6.34× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 6.5 | 5.8 | 4.6 | 4.4 | 4.2 | 4.3 | 4.4 | 1.1 | 1.5 | 1.1 | 0.17× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 64.9 | 54.8 | 50.7 | 50.3 | 54.1 | 45.9 | 43.7 | 26.6 | 28.8 | 26.3 | 0.41× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1331 | 1373 | 1544 | 1464 | 1651 | 1878 | 1944 | 1274 | 1674 | 1306 | 0.98× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 13298 | 12956 | 17036 | 16803 | 21070 | 20017 | 19457 | 32129 | 32292 | 30632 | 2.3× |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 739 | 788 | 1047 | 1040 | 688 | 666 | 707 | 699 | 664 | 655 | 0.89× |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1563 | 1496 | 1564 | 1810 | 1391 | 1322 | 1389 | 1212 | 1213 | 1331 | 0.85× |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1451 | 1496 | 1522 | 1810 | 1391 | 1322 | 1389 | 1212 | 1213 | 1331 | 0.92× |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1563 | 1496 | 1612 | 1810 | 1391 | 1322 | 1389 | 1212 | 1213 | 1331 | 0.85× |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 0.9 | 1 | 0.9 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1.08× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 19184 | 18842 | 18436 | 18436 | 18436 | 18436 | 18436 | 18436 | 18436 | 18436 | 0.96× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 19184 | 18842 | 18436 | 18436 | 18436 | 18436 | 18436 | 18436 | 18436 | 18436 | 0.96× |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 912 | 1007 | 479 | 515 | 515 | 828 | 496 | 1216 | 1080 | 1282 | 1.41× |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 304 | 371 | 362 | 398 | 421 | 518 | 438 | 941 | 917 | 964 | 3.17× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 362 | 341 | 368 | 396 | 437 | 485 | 451 | 1030 | 937 | 1083 | 2.99× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 2001 | 1894 | 736 | 680 | 549 | 1125 | 354 | 680 | 455 | 745 | 0.37× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 5 | 5.8 | 1.9 | 1.8 | 1 | 2.6 | 0.6 | 0.4 | 0.4 | 0.4 | 0.09× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 2327 | 2522 | 894 | 928 | 842 | 1071 | 610 | 217 | 370 | 228 | 0.1× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 4942 | 5761 | 2015 | 2408 | 2497 | 2775 | 2593 | 490 | 566 | 524 | 0.11× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 6.7 | 7.5 | 5.1 | 5.2 | 5.2 | 5.3 | 5.4 | 2.1 | 2.1 | 1.9 | 0.28× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 54.3 | 55.5 | 53.1 | 53.8 | 54.5 | 51.9 | 45 | 30.3 | 29.7 | 29.5 | 0.54× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2360 | 2460 | 1855 | 1971 | 2199 | 2548 | 2352 | 2021 | 1772 | 1867 | 0.79× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 19058 | 18244 | 19202 | 20520 | 22823 | 24944 | 19625 | 29239 | 25526 | 28741 | 1.51× |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 842 | 756 | 993 | 938 | 759 | 720 | 771 | 679 | 670 | 677 | 0.8× |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1226 | 1153 | 1612 | 1518 | 1266 | 1180 | 1200 | 1044 | 1094 | 1258 | 1.03× |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1226 | 1136 | 1594 | 1518 | 1262 | 1180 | 1184 | 1044 | 1094 | 1258 | 1.03× |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1226 | 1196 | 1612 | 1518 | 1266 | 1180 | 1200 | 1044 | 1094 | 1258 | 1.03× |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1 | 1× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 12579 | 12842 | 12436 | 12436 | 12436 | 12436 | 12436 | 12436 | 12436 | 12436 | 0.99× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 12579 | 12842 | 12436 | 12436 | 12436 | 12436 | 12436 | 12436 | 12436 | 12436 | 0.99× |
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | – | – | – | – | – | – | – | – | – | 48.3 | – |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | – | – | – | – | – | – | – | – | – | 585 | – |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | – | – | – | – | – | – | – | – | – | 15.1 | – |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | – | – | – | – | – | – | – | – | – | 3734 | – |

**Messserver, erste Läufe (2026-10-15, `a600166` = Phase-3-Code; Ordner `…_server-baseline`, `…_server-baseline-stoerung`; nicht in der
Tabelle oben, weil der Rechner ein anderer ist):** ohne Fremdlast, gegen Phase 3 auf dem MacBook (unter Fremdlast). Die **Arbeit je Vorgang ist
gleich** (Transaktionen je Vorgang 0,95–1,07 ×, Queries 0,77–1,0 ×), die Verhältnisse der Phasen bleiben also gültig. Ende zu Ende 2–3 × schneller
(S1 52 Paare/s, S2 47, S3 218–228 Teile/s, S5 192–307 Vorgänge/s), S5-64 6,3 × (191 statt 30; der Mac-Lauf litt besonders unter der Fremdlast),
Latenzen p50 0,23–0,51 ×; der Host nutzt dabei 7–10 Kerne statt 3. S4 ist 0,7–0,9 × (1 282 Paare/s; einzelne kurze Abfragen, hier zählt der
schnellere Einzelkern des M4). Die Decke bleibt dieselbe: die Schreibseite nimmt in S5-64 5 675 Commands/s an, Ende zu Ende kommen 191 Vorgänge/s
durch (Konsum, PF-4). Leerlauf L5 zeigt 377 Transaktionen/s und 2 100 Queries/s (Prozess-Poll, PF-4). Unter Last steht `docker-proxy` (Userland-Proxy
der Ports auf 127.0.0.1) bis 30 % CPU — jede DB-Verbindung läuft durch ihn. Störungslauf: 56 847 Vorgänge mit zwei harten Abbrüchen und
Redis-Ausfall, alles genau einmal, Abbau 419 s.

**Host-Netzwerk statt docker-proxy (2026-10-15, `ab58b0e`, A-B-B-A, Ordner `…_server-netz-{proxy,host}-{1,2}`):** seitdem laufen Postgres und die
Redis des Messservers im Host-Netzwerk (`docker-compose.messserver.yml`). Gegen den Proxy, Mittel der beiden B- gegen die beiden A-Läufe: Durchsatz
Ende zu Ende 1,01–1,10 × (Median ≈ 1,06 ×; S5-1 1,10 ×, S5-64 1,07 ×, S3-64 1,06 ×), Latenz p50 0,90–0,94 × (S3-8 1,04 ×, im Rauschen), Arbeit je
Vorgang unverändert. Ein kleiner, gleichgerichteter Effekt — die Decke im Konsum (PF-4) bleibt. Die Server-Läufe vorher (`…_server-baseline`)
liefen noch über den Proxy.

**PF-4 auf dem Messserver (2026-10-15, `ffaeaea`, Spalte „PF-4“ der Server-Tabelle):** Leerlauf L1 351 → 2,5 Queries/s und 24 → 3,7 % CPU (der
Prozess-Poll las vorher jeden geänderten Stream ab 0); S5-1/S5-64 1,17/1,16 ×, S5-64 Queries je Vorgang 0,84 ×; S1–S4 ohne Regression, Kosten je
Vorgang gleich. L5 bleibt bei ≈ 1 900 Queries/s: dort holt die Reaktion ihren Rückstand aus S5-64 auf (PF-12), nicht der Dirigent. **Warmlauf:** seit
diesem Lauf wartet der Treiber nach dem ersten Start eines Hosts 30 s (`warmlauf_s` im Ergebnis). Ohne ihn maß der erste Block kalten JIT-Code
(S3-8 −28 %, mehr CPU bei gleicher Arbeit); die Läufe davor bekamen diese Zeit zufällig, weil `/health` auf einer frischen DB erst grün wurde,
als der alte Prozess-Poll nach 30 s die Event-Tabellen anlegte. Läufe ohne diese Zufalls-Wartezeit (`…_1721_…`, A-B-B-A `…_server-pf-4-ab-*`)
sind für S1/S3-8 deshalb nicht vergleichbar.

**PF-12 auf dem Messserver (2026-10-15, `5bda1bb`, Spalte „PF-12“):** Leerlauf L5 2 292 → 2,5 Queries/s und 145 → 3,4 % CPU gegen die
Server-Baseline — der Rest war der Emittenten-Poll (jeder geänderte Stream bekam einen Cursor), der serielle Korrelations-Router und nicht
zusammengefasste Emittenten-Weckungen (docs/13 §13.6). S5-64 1,69 ×, Latenz p50 0,26 ×; S5-8 Latenz 0,38 ×; S1–S4 0,9–1,14 ×. Zwei Störungsläufe
grün (je ≈ 19 800 Vorgänge, Rückstand am Ende 715–908 statt 32 763, Abbau 11 s statt 316 s). Offen: Reaktion p99 2 s unter S5-64 und S3-64
0,9 × (PF-13).

**Weniger Datenbankarbeit je Vorgang (2026-10-15, Messserver, Spalten „Ohne Reset“ und „Korreliert lesen“):**
- *Ohne Verbindungs-Reset* (`3011a1e`): Npgsql setzt eine Verbindung bei der Rückgabe an den Pool nicht mehr per `DISCARD ALL` zurück (ein
  Drittel aller Anweisungen). Wirkung klein: 1,0–1,1 × in den Szenarien (S1 1,11 ×, S5-8 1,08 ×) — die Anweisung war billig.
- *Korreliert lesen* (`6e361a6`): der Prozess-Manager liest in seinen Ziel-Streams nur die Events seiner Korrelation. Je Vorgang gelesene
  Zeilen: S3 2 007 → 6, S5-8 714 → 104, S5-64 1 106 → 163; DB-Zeit fürs Lesen S3 9,1 → 0,8 ms, S5 4,3–6,0 → 2,4–3,2 ms. Durchsatz gegen „Ohne
  Reset“: S3-64 1,98 ×, S3-8 1,63 ×, S5-64 1,16 ×, S5-8 1,10 ×; Host-CPU in S5 0,66–0,81 ×. Störungslauf grün (29 794 Vorgänge).
  Gegen die Server-Baseline damit S3-64 1,85 ×, S3-8 1,77 ×, S5-64 1,94 ×, S5-8 1,21 ×.

**Schlanker Schreibweg (Plan Phase 4, 2026-10-15, Messserver, Spalten „Inbox-Tabelle“ und „Schnell anhängen“):**
- *Inbox als Tabelle* (`aef3d46`, `…_2152_…_server-inbox`, Störungslauf `…_2156_…` grün, 28 004 Vorgänge): eine Wirkung schreibt keine
  Marke mehr, die Quittung geht als eine Anweisung je Group-Commit in `cqrs_inbox`. Je Vorgang (pg_stat_statements, Nenner = Vorgänge des
  Blocks) S5-8: Event-Inserts 11,5 → 6,9, Inbox-Inserts 0 → 1,2, DB-Zeit 3,6 → 2,9 ms; S5-64 Event-Inserts 12,0 → 6,7, DB-Zeit 4,3 → 3,3 ms;
  S3-64 8 → 5. Durchsatz gegen „Korreliert lesen“ (je ein Lauf): S5 1,05–1,13 ×, S1 1,08 ×, S3 0,94–0,96 ×.
- *Schnell anhängen* (`cf30aa0`, Martens `EventAppendMode.Quick`, `…_2207_…_server-quick`, Störungslauf `…_2210_…` grün, 28 502 Vorgänge):
  der Vorab-Read der Stream-Köpfe und die Sequenz-Abfrage entfallen (S5-8 je Vorgang 4,9 + 1,5 → 0, S1 6 + 16,7 → 0); Queries je Vorgang S5
  0,81–0,86 ×, S3 0,78 ×, S1 0,89 × gegen „Inbox-Tabelle“.
- *A-B-B-A* gegen `3483a31` (vor beiden; Ordner `…_server-schreibweg-ab-{a1,b1,b2,a2}`, S1/S3/S4/S5, alle gültig und grün), Mittel B gegen
  Mittel A: Durchsatz bis alles sichtbar S3-8 1,18 ×, S3-64 1,17 ×, S5-1 1,08 ×, S5-8 1,06 ×, S5-64 1,06 ×, S1 1,04 ×, S4 1,01 ×; Queries je
  Vorgang 0,78–0,89 ×; Latenz p50 S3 0,83–0,87 ×, S5-1 0,88 ×. Unter Höchstlast steigt die Latenz in S5-8/S5-64 (1,32 ×): die Schreibseite nimmt
  mehr an (S5-8 1 006 bzw. 729 statt 554 bzw. 586 Vorgänge/s), die Warteschlange bis „sichtbar“ wird länger.
- *Leerlauf:* L1 29 Queries/s (wie „Inbox-Tabelle“), L5 41 → 86 Queries/s, CPU 7–10 %. Seit „Korreliert lesen“ über dem E5-Ziel (< 10); in L5 sind
  es der Reaktions-Adapter (Cursor und Stream-Rest je Posten, je 470 im Fenster) — der Nachlauf der Reaktion aus S5-64 (PF-13), nicht der
  Schreibweg; in L1 lesen Prozess-Poll (Offen-Liste, Seite, Marke, vorhandene Streams, je 50–63) und Batch-Leser (gesetzte Sequenz 102, neue
  Partitions-Checkpoints 23) das Log der Blöcke davor nach („Ohne Reset“: 52 Anweisungen im Fenster, jetzt 626). Nicht weiter untersucht.

**Prozess-Manager liest nur, was er nicht schon weiß (2026-10-16, Messserver, `6a437ba`, Spalte „Prozess-Manager“, docs/04 §4.5):** gezielte
Weckungen (Router mit Event, Send und Funktions-Ergebnis mit Vorgang), „gelesen, nichts da“, frischer Vorgang ohne Store-Laden, Kopf-Zustand
des Manager-Logs im Speicher; dazu der Offen-Index als Beilage im Append von Start und Ende. Je Vorgang (pg_stat_statements) S5-8:
Event-Reads 23,1 → 10,2, Dokument-Reads 4,8 → 2,9, BEGIN/COMMIT 10,8 → 4,2, DB-Zeit 3,0 → 1,6 ms; S5-64 Event-Reads 22,8 → 7,9, BEGIN/COMMIT
8,7 → 2,1, DB-Zeit 3,5 → 1,7 ms; S1 Event-Reads 64 → 36, Dokument-Reads 57 → 39. Durchsatz bis alles sichtbar gegen „Schnell anhängen“
(je ein Lauf): S5-64 457 → 1 246 Vorgänge/s (2,7 ×), S5-8 451 → 1 030 (2,3 ×), S5-1 1,0 × (Latenz p50 0,71 ×), S1 1,44 ×, S2 1,42 ×;
Transaktionen je Vorgang S5-64 4,4 → 1,1. A-B-B-A gegen `cf30aa0` (Ordner `…_server-pm-ab-{a1,b1,b2,a2}`, S1/S3/S4, gültig, grün): S1 1,40 ×,
S3-64 1,06 ×, S3-8 0,96 × (die Grenze ist der einzelne Schreiber), S4 1,02 × (die 0,81 × im Einzellauf waren Rauschen: S4 dauert 0,4 s);
Queries je Vorgang 0,75 ×, Transaktionen S3 0,41–0,50 ×. Zwei Störungsläufe grün (`…_0030_…`, `…_0033_…`: 54 079 und 56 337 Vorgänge statt
≈ 28 500; S5-p99 37–41 s, im Band der Läufe seit PF-12 von 36–55 s — die 12 s von „Schnell anhängen“ waren der Ausreißer). Leerlauf L5
86 → 110 Queries/s (mehr angenommen, mehr Nachlauf der Reaktion, PF-14).

**Aggregat-Bündel (2026-10-16, Messserver, `2390f1e`, Spalte „Bündel“, docs/03 §3.1):** ein Aggregat-Actor sammelt Commands, während sein
Commit läuft, entscheidet sie nacheinander im Speicher und hängt sie in einem Append an (höchstens 64, `Schreibpfad.BuendelGroesse`). Gegen
„Prozess-Manager“ (je ein Lauf): S3-64 541 → 1 350 Teile/s (2,5 ×), S3-8 486 → 859 (1,8 ×; Latenz p50 1 400 → 17 ms, p99 2 035 → 31 ms — kein
Rückstau mehr am heißen Sammelvorgang), S3 Transaktionen je Vorgang 2,2 → 1,0 bzw. 3,0 → 1,7; S1/S2/S4/S5 unverändert. A-B-B-A gegen
`6a437ba` (Ordner `…_server-bu-ab-{a1,b1,b2,a2}`, S3/S5, gültig, grün): S3-8 1,72 ×, S3-64 2,29 ×, S5-1/8/64 1,01/1,01/1,07 × (die 0,91 × von
S5-8 im Einzellauf waren Rauschen). Zwei Störungsläufe grün (`…_0150_…`, `…_0153_…`: 55 307 und 57 016 Vorgänge; S5-p99 4,5 s bzw. 37 s — das
Band hängt am Abbruchzeitpunkt). Gegen die Server-Baseline jetzt S3-64 5,9 ×, S3-8 3,7 ×, S5-64 6,2 ×, S5-8 2,6 ×, S1 1,54 ×.

**Leerlauf: Nachlauf statt Grundlast (PF-14, 2026-10-16, Messserver, `7a1f9f9`, Spalte „Nachlauf“, docs/04 §4.1.3):**
- *Diagnose* (`--art leerlauf`, `3a1355b`, Zeitreihe in 10-s-Scheiben, kein Messlauf): ohne Last (frischer Host, leere DB) 2,5 Queries/s, alle
  30 s eine Scheibe mit 10 (Batch-Poll: 40 Partitionen je gesetzte Sequenz und Seite). Nach den Blöcken, L1: bei t ≈ 20 s 43 Queries/s — der
  Prozess-Poll liest 62 Seiten (das ganze Log der Blöcke, 31 000 Events) in ≈ 5 s; seine Marke stand seit Beginn der Last, weil Weckungen an die
  ausgelasteten Manager unbestätigt blieben (G-8); bei t ≈ 50 s 12 (die Batch-Leser schreiben ihre Position über fremde Events fest); danach
  300 s lang 2,4 und 10,7 im Wechsel (Mittel 5,2), CPU 2–3,5 %. L5: t ≈ 10 s 360 Queries/s (3 575 Einzel-Reads des Korrelations-Routers — Signale
  der schon beendeten S5-64-Vorgänge), t ≈ 20 s 156 (Prozess-Poll 130 Seiten mit 64 000 Events, Emittenten-Poll 450 Weckungen mit Cursor- und
  Stream-Read), danach 2,4–3,0 Queries/s und 1–2 % CPU. Nach einem Neustart auf demselben Bestand eine Scheibe mit 8–16 Queries/s, dann dasselbe
  Bild. **Es bleibt nichts:** das alte 15-s-Fenster, 5 s nach dem letzten Block, traf den ersten Poll-Takt nach der Last — seit „Korreliert
  lesen“ baut sich die Last schneller ab, und das Fenster fiel regelmäßig darauf (vorher je nach Lage daneben: 2,5 Queries/s).
- *Messverfahren:* erst der Nachlauf, bis alle Poll-Cursor und Projektions-Positionen am Kopf des Logs stehen (höchstens 180 s), dann 60 s
  (zwei Poll-Perioden). Neue Kennzahlen „Nachlauf bis alle Konsumenten am Kopf“ und „Queries im Nachlauf“. Die L1/L5-Werte der Spalten davor
  stammen aus dem alten Fenster und sind nur der Richtung nach vergleichbar.
- *Weniger je Takt* (`f55494b`): gesetzte Sequenz und „steht dahinter etwas?“ in einer Abfrage, ohne Neues entfällt die Seite — ein
  Batch-Poll-Takt kostet 40 statt 80 Abfragen, Prozess- und Emittenten-Poll je eine statt zwei.
- *Gemessen* (`…_0309_7a1f9f9_server-nachlauf`, gültig, grün): L1 3,8 Queries/s und 2,6 % CPU (Fenster in „Bündel“: 29,6 und 7,4; Baseline
  351 und 24,5), L5 2,6 Queries/s und 1,5 % (113 und 10,2; Baseline 2 293 und 145) — **E5-Ziel Leerlauf (< 10 Queries/s, < 5 % CPU) erreicht**.
  Nachlauf L1 48 s mit 585 Queries, L5 15 s mit 3 734. Lastblöcke gegen „Bündel“ (je ein Lauf) 0,87–1,16 ×, Queries je Vorgang 0,91–1,01 × —
  im Band der Läufe desselben Codes (S3-64 1 215–1 350 Teile/s, Reaktion p99 S5-64 98–348 ms in „Bündel“ und seinen A-B-B-A-Läufen). Zwei
  Störungsläufe grün (`…_0315_…`, `…_0318_…`: 50 486 und 47 908 Vorgänge). RSS am Ende weiter 0,9–1,0 des Höchststands (passiviert wird erst
  nach 120–600 s, PF-5).
