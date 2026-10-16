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

(NEU)

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
