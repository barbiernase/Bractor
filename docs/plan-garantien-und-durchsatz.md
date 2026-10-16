# Plan: Garantien und Durchsatz

> **Plan**, Stand 2026-10-15. Wird nach der Umsetzung gelöscht; die Historie liegt dann im Git-Verlauf. Wo er etwas Offenes nennt, ist die
> Quelle [13](13-reifegrad-schulden-bewertung.md) §13.0 (Reihen **G** und **PF**); Garantien: [11](11-garantien.md); Messwerte:
> [12](12-tests-und-vermessung.md) §12.5.
>
> **Ein Agent, ein Strang:** Diesen Plan setzt genau ein Agent um, direkt im Haupt-Arbeitsbaum auf `claude/werkbank-zielbild`. Parallel
> arbeitet niemand an diesem Repo; der Werkbank-Strang (§13.5 Schritte 1–10) ruht, bis der Plan abgeschlossen ist oder Tobi anders entscheidet.
> Die G- und PF-Befunde setzt kein anderer Strang um.

## 0 · Ziel und Maßstab

1. **Korrektheit:** Jede Garantie aus [11](11-garantien.md) gilt ohne Lücke — G-1 bis G-5 geschlossen.
2. **Durchsatz:** Die Decke der Konsumseite steigt um eine Größenordnung (PF-1); die Grundlast im Leerlauf geht gegen null (PF-2, PF-4);
   der Speicher bleibt im Dauerbetrieb flach (PF-5).
3. **Nachweis:** Jede Phase endet mit einer klaren Messung **Ende-zu-Ende an realen Domänen im echten Host** (§0.1), nicht nur mit grünen
   Tests und nie mit synthetischen Abkürzungen. Die Ergebnisse werden **gespeichert** (§0.1) und sind jederzeit nachrechenbar.
4. **Garantien geprüft:** Jede Garantie aus [11](11-garantien.md) hat einen Test, der sie unter Störung beweist; die Garantie-Suite und die
   Exactly-once-Prüfung der E2E-Läufe laufen **immer** (§0.2).

### 0.1 · Messprinzip (bindend für jede Phase)

- **Real:** gemessen wird der echte Host (`Host.Grpc`, Umgebung `vermessung`, eigene Datenbank) mit den realen Anwendungen des Repos — der
  Beispieldomäne (`Domain*`) und der Prüfdomäne (`Pruefdomaene*`). Keine eigens erfundenen Bench-Typen; braucht ein Mechanismus einen Nutzer,
  den keine Domäne hat (heute: Reaktion), bekommt die Prüfdomäne einen fachlich sinnvollen.
- **Ende-zu-Ende:** Last kommt nur über die echten Eingänge (Datei-Quelle, Leitung per gRPC mit Akteur-Token, JSON-Eingang); gemessen wird bis
  zum fachlich sichtbaren Ergebnis (Query über die Leitung liefert den Stand, Vorgang beendet). Nie direkt in die Datenbank schreiben, nie
  Actors direkt ansprechen. `scripts/vermessung-postgres.sh` erklärt Ursachen, ist aber nie Abnahme.
- **Szenarien (fest, in jeder Messung dieselben):**

  | # | Anwendung | Eingang → Ergebnis | prüft vor allem |
  |---|---|---|---|
  | S1 | Beispieldomäne, Bildeingang | Stoß von 600 TIFF-Paaren ins Quell-Verzeichnis → Fluss (Deutung, Paar, Verkleinerung, Kontrast) → `ImagePair` → Projektionen → Query „Bild verfügbar“ | Quelle, Dirigent, Funktionen, Konsum |
  | S2 | Beispieldomäne, Bildeingang mit Python-Worker (`Laufort = Extern`) | wie S1 (200 Paare), Funktionen im Worker über die Leitung | Funktions-Vermittlung, Lease |
  | S3 | Beispieldomäne, Sammelvorgang heiß | 8 bzw. 64 Sender je Teil `StarteTeilauftrag` + `SchließeTeilauftragAb` an EINEN Sammelvorgang → `TeilFertigProzess` → `TeilVerbucht` | Single-Writer-Decke, Prozess-Poll, Inbox, Retry-Sturm (PF-9) |
  | S4 | Beispieldomäne, Datensatz | Range über alle Bildpaare aus S1 aufnehmen, einfrieren → Datensatz eingefroren | Flüsse mit Lese-Fähigkeit, große Events |
  | S5 | Prüfdomäne (`Host.Pruefdomaene`) | 1, 8 bzw. 64 Sender, je zehn Vorgänge 8 Aufträge (Diamant), 1 Auftrag gegen ein volles Kontingent (Kompensation), 1 Verteilung (Fan-out, Count-Join); jede Reservierung unter dem Mindestbestand weckt die Reaktion `NachschubBeiMindestbestand` | Schreibdurchsatz verteilt, Prozess-Maschine, Emittenten |
  | L1, L5 | beide | 15 s Leerlauf nach den Szenarien | Grundlast (PF-2, PF-4) |

- **Höchstlast statt Hochtasten (seit 2026-10-15, auf Tobis Vorgabe: ein Lauf in Minuten, nicht in Stunden):** jeder Block sendet höchstens
  15 s so schnell, wie der Eingang annimmt (S3 höchstens 2 000 Teile, S5 höchstens 3 000 Vorgänge je Block), danach endet die Last und der
  Rückstand baut ab (höchstens 120 s). Kennzahlen je Block: angenommen je Sekunde (Schreibseite), sichtbar je Sekunde im Fenster und Durchsatz
  bis „alles sichtbar“ (Ende zu Ende), Rückstand am Fensterende und Abbauzeit, Latenz p50/p95/p99, Transaktionen und Queries je Sekunde und je
  Vorgang (`pg_stat_statements`, `track = top`; Transaktionen = COMMITs, weil `pg_stat_database` verzögert zählt), CPU und Speicher (RSS) des
  Hosts. Ein nicht abgebauter Rückstand ist ein Ergebnis, kein Messfehler. Was „sichtbar“ je Szenario heißt, steht in
  [12](12-tests-und-vermessung.md) §12.5. Kein Dauer- und kein Aufhollauf; der Störungslauf dauert etwa drei Minuten.
- **Klar:** ein Befehl startet die Messung (`scripts/vermessung-e2e.sh`, ≈ 5 min), feste Parameter, eine Wiederholung; Rechner, Commit-Hash,
  Fremdlast und Abweichungen stehen im Ergebnis. Vor der Phase wird gemessen (bzw. die letzte Messung gilt), nach der Phase dieselbe Messung
  auf demselben Rechner. Die Ergebnisse stehen als **eine fortlaufende Tabelle** in docs/12 §12.5 (Zeilen: Szenario × Kennzahl, Spalten:
  Baseline, Phase 1, Phase 2 …); verglichen werden Verhältnisse.
- **Gespeichert:** jeder Lauf schreibt sein Ergebnis selbst nach `vermessung/ergebnisse/<JJJJ-MM-TT_HHMM>_<commit>_<anlass>/` und wird mit
  der Phase committet — nur anhängen, nie überschreiben oder löschen. Inhalt:
  - `ergebnis.json`: Metadaten (Rechner, CPU, Commit-Hash, Branch, Umgebung, Parameter, Fremdlast, Anlass „Baseline“/„Phase n“), je Block
    alle Kennzahlen, das Ergebnis der Exactly-once-Prüfung (§0.2);
  - `latenzen/<szenario>.csv` (Rohwerte, gzip-komprimiert, wenn größer als 1 MB), `pg_stat_statements.csv`, `host.csv` (RSS/CPU über die Zeit);
  - `zusammenfassung.md`: die Tabelle, wie sie in §12.5 landet.

  Die Tabelle in §12.5 wird aus diesen Dateien erzeugt (`scripts/vermessung-tabelle.sh`), nicht von Hand abgeschrieben; jede Spalte verweist
  auf ihren Ergebnis-Ordner. Auch `scripts/vermessung-postgres.sh` legt seine Ausgabe dort ab.

### 0.2 · Garantie-Nachweis (bindend, läuft immer)

- **Garantie-Matrix:** in [11](11-garantien.md) bekommt jede Zeile von §11.2–§11.8 und §11.10 eine Spalte „Nachweis“ mit dem Test bzw. dem
  Analyzer, der sie beweist. Eine Garantie ohne Nachweis gilt als Lücke und steht in docs/13.
- **Garantie-Suite:** Tests mit `[Trait("Kategorie", "Garantie")]` — im Prüfstand für Logik über Seams, in den Integrations-Suiten gegen echtes
  Marten für Store-Semantik. Jede Garantie wird **unter Störung** geprüft, mindestens: doppelte und ungeordnete Zustellung, verlorenes Signal,
  Absturz zwischen Effekt und Checkpoint, Absturz zwischen Emit und Cursor, Timeout und Fehlschlag beim Emit, Versionskonflikt, mehrdeutiger
  Commit, Wiederholung einer alten CommandId (Fenster bzw. Horizont), zwei Aktivierungen (zwei Knoten), Poison-Stream, Neustart des Hosts.
- **Lücken zuerst als Test:** jede offene G-Lücke bekommt in Phase 0 einen Test, der sie zeigt, markiert mit
  `Skip = "G-n offen (docs/13)"`. Die Phase, die die Lücke schließt, entfernt das Skip; eine Lücke gilt erst als geschlossen, wenn ihr Test grün ist.
- **Exactly-once-Prüfung jedes E2E-Laufs:** nach jedem Lauf zählt der Treiber über Queries und den Log nach, dass jede Wirkung genau einmal
  eingetreten ist:
  - S1/S2: je Datei genau ein Vorgang, je Paar genau ein `ImagePair`, `BildVerfuegbar` genau einmal je Paar, Read-Model-Zählung = Log-Zählung;
  - S3: `Fertig = N` exakt, Barriere genau einmal erreicht;
  - S4: Mitglieder des eingefrorenen Datensatzes = aufgenommene Paare, genau ein `DatensatzEingefroren`;
  - S5: je Vorgang genau ein `ProzessBeendet`, jede Kompensation genau einmal, je Reaktions-Command genau eine Wirkung (Ziel-Events je
    `causationId` = 1).

  Schlägt eine Prüfung fehl, ist der Lauf ungültig und die Phase nicht abgenommen.
- **Störungslauf:** je Phase mindestens ein E2E-Lauf mit Störungen — Host mitten im Lauf hart beenden und neu starten, Redis zeitweise
  stoppen, Signale verlieren — mit derselben Exactly-once-Prüfung. Ergebnis im Ergebnis-Ordner. (Der Broker läuft im Host; einen einzelnen
  Broker-Shard kann man von außen nicht neu starten — Signale gehen mit jedem harten Abbruch verloren, und `PollerBackstopPostgresTests` prüft
  den verlorenen Weckruf gezielt.)
- **Immer:** die Garantie-Suite läuft vor jedem Commit (`dotnet test … --filter Kategorie=Garantie` auf Prüfstand und Integration, die
  Integration nacheinander); die Exactly-once-Prüfung läuft in jedem E2E-Lauf, auch bei reinen Performance-Messungen.

Ausgangslage aus der Messung (§12.5): eine Weckung je (Projektion, Stream) schafft etwa 8 200/s, ein Batch etwa 175 000 Effekte/s (Faktor
~20); die Inbox-Marke kostet 35–40 % im Group-Commit; ein Stream ab 0 statt ab Cursor kostet bei 100 000 Events Faktor > 2 000. Hochgerechnet
trägt die Konsumseite heute etwa 8 200 / K Events/s (K = replaybare Projektionen) — vor Marten und Netzwerk.

## 1 · Übersicht

| Phase | Inhalt | IDs | Aufwand | setzt voraus | Abnahme (Kurz) |
|---|---|---|---|---|---|
| 0 | Abgleich, E2E-Messung, Garantie-Nachweis | – | 7–9 PT | – | Baseline S1–S5 gespeichert; Garantie-Suite grün, Lücken als Skip-Tests |
| 1 | Emit-Ausgang und Emittenten-Cursor ✅ | G-1, G-2 (Emittent), G-7, P1-6, PF-9 | 5–7 PT | 0 | S5: Kosten der Reaktion unabhängig von der Stream-Länge |
| 2 | Actor-Lebenszyklus ✅ | G-3, PF-5 | 3–5 PT | 0 | RSS nach den Höchstlast-Blöcken fällt im Leerlauf zurück (Passivierung) |
| 3 | Batch-Konsum (umgesetzt, nicht abgenommen: S1-Ziel, §8) | PF-1, PF-2, G-4 | 15–25 PT | 1, 2 | Durchsatz bis „alles sichtbar“ S1 ≥ 3 × Baseline, S5 ohne Regression (E5, E9) |
| 4 | Inbox als Tabelle | G-2 (Inbox), PF-3 | 6–9 PT | 2, E1 | S3 heiß: genau einmal, mehr Teile/s als nach Phase 3 |
| 5 | Prozess-Poll und Backstop (umgesetzt vor Phase 4, §8) | PF-4 (W11-2-Anteil), G-8 | 3–5 PT | 1 | S3/S5: Zeit je Teil konstant über N; Leerlauf mit offenen Vorgängen nahe 0 Queries/s |
| 6 | Rest | PF-6, G-5, PF-7 (Entscheidung) | 2–3 PT | – | – |
| | **Summe** | | **41–63 PT** | | etwa 9–13 Wochen für eine Person |

Reihenfolge: erst Korrektheit und das wachsende Leck (1), dann Stabilität im Dauerbetrieb (2), dann die Decke (3), dann die nächste Decke —
den Schreibpfad (4), zuletzt Grundlast und Rest (5, 6). Nach Phase 1 ist der Auslöser von G-2 (Emittent sendet ab 0 erneut) weg; deshalb darf
die Inbox-Tabelle (4) nach dem Batch-Konsum kommen.

## 2 · Entscheidungen vorab

| # | Frage | Vorschlag | nötig bis |
|---|---|---|---|
| E1 | Inbox: **A** Tabelle als Dedup, Marke nur bei Noop/Ablehnung — oder **B** Tabelle als „Ausgang je Command“, keine Marken im Stream (Prozess-Manager, Clients, Spur ändern sich; W9 berührt) | **entschieden** (Tobi, 2026-10-15): **A** | Phase 4 |
| E2 | Aufbewahrungs-Horizont der Inbox (Exactly-once gilt innerhalb dieses Horizonts) | **entschieden** (Tobi, 2026-10-15): 30 Tage, Umgebung `Schreibpfad.InboxHorizontTage` | Phase 4 |
| E3 | Bestandsdaten | **entschieden** (Tobi, 2026-10-15): Sim-DBs zurücksetzen, kein Backfill (keine Prod-Daten) | Phase 4 |
| E4 | Partitionen je Projektion im Batch-Konsum | **entschieden** (Tobi, 2026-10-15): 8, Umgebung `Konsum.Partitionen`; ein Wechsel ist ein Neuaufbau; Single-Activation des Clusters + Versionsprüfung des Partitions-Checkpoints statt Lease | Phase 3 |
| E5 | Abnahme-Zielwerte je Szenario | **entschieden** (Tobi, 2026-10-15): die Werte aus §8 (Phase 3: Durchsatz S1/S5 ≥ 3 ×, Queries je Vorgang ≤ ⅓) | nach Phase 0 |
| E6 | Handle-/Trigger-Welt (W11-1) in Phase 1 zurückbauen oder nur anpassen? | nur minimal anpassen; der Rückbau bleibt W11-1 | Phase 1 |
| E7 | Leerlaufzeit der Passivierung | Aggregat 10 min, Adapter 2 min, Prozess-Manager 10 min; Umgebung | Phase 2 |
| E8 | Reaktionen (Emittenten) im Batch-Leser? | **entschieden** (Tobi, 2026-10-15): nein, sie bleiben je Stream ([04 §4.1.2](04-konsum-und-prozess-maschine.md)) | Phase 3 |
| E9 | E5-Ziel S5 in Phase 3? | **entschieden** (Tobi, 2026-10-15): nein — S5 hat keine Projektion, seine Decke ist der Dirigent (PF-4); S5-Ziel nach Phase 5; Phase 3: S1 ≥ 3 ×, S5 ohne Regression | Phase 3 |
| E10 | Partitions-Funktion | **entschieden** (Tobi, 2026-10-15): letztes UUID-Byte mod P (SQL und .NET gleich) | Phase 3 |
| E11 | Batch-Größe | **entschieden** (Tobi, 2026-10-15): 500 Events, Umgebung `Konsum.BatchGroesse` | Phase 3 |

## 3 · Phasen im Detail

### Phase 0 · Abgleich, E2E-Messung, Garantie-Nachweis (7–9 PT)

1. **Abgleich:** `git log` und docs/13 §13.0 prüfen — ist ein G- oder PF-Befund inzwischen berührt? Plan nachführen; Entscheidungen E1–E7
   gebündelt mit Tobi klären (ohne Antwort gelten die Vorschläge).
2. **Messaufbau nach §0.1:** Umgebung `umgebungen/umgebung.vermessung.json` (eigene DB, Quell-Verzeichnis, Funktionen Host bzw. Extern),
   ein Treiber für die Szenarien S1–S5, der nur echte Eingänge benutzt (Dateien erzeugen wie `PipelineFlussE2ETests`, Commands und Queries
   über die Leitung mit Akteur-Token), `pg_stat_statements` an, RSS/CPU des Host-Prozesses mitschreiben. Fehlt einem Szenario ein realer
   Baustein (heute: eine Reaktion in S5), bekommt die Prüfdomäne einen fachlich sinnvollen, an der richtigen Rolle (CQRS098).
3. **Ablage nach §0.1:** `vermessung/ergebnisse/` mit Ergebnis-Ordnern, `scripts/vermessung-tabelle.sh` erzeugt die Tabelle für §12.5. Die
   zwei Läufe von `scripts/vermessung-postgres.sh` vom 2026-10-15 als erste Einträge nachtragen.
4. **Garantie-Nachweis nach §0.2:** Garantie-Matrix in docs/11, Garantie-Suite (Trait `Garantie`) mit den Störfällen, je offene G-Lücke ein
   Skip-Test, Exactly-once-Prüfung und Störungslauf im E2E-Treiber.
5. **Baseline:** S1–S5 unter Höchstlast (Durchsatz, Rückstand, Latenz, Last und Speicher) und ein Störungslauf — gespeichert,
   Tabelle in §12.5.

*Abnahme:* `scripts/vermessung-e2e.sh` misst S1–S5 mit einem Befehl reproduzierbar und speichert das Ergebnis; Exactly-once-Prüfung grün auch
im Störungslauf (sonst ist das ein neuer G-Befund); Garantie-Suite grün bis auf die markierten Lücken; Baseline eingetragen; E5-Zielwerte
festgelegt.

### Phase 1 · Emit-Ausgang und Emittenten-Cursor (5–7 PT)

1. `CommandEmitter.EmitAsync` gibt einen **Ausgang** zurück (zugestellt, abgelehnt, unbekannt) statt Timeouts zu verschlucken und das
   `CommandResult` zu ignorieren. Aufrufer: `HandlerOutputRouter`, `ProzessManagerActor` (faltet weiter den durablen Ausgang), `FristScheduler`,
   `PipelineActorBase` (entfällt nach E6).
2. **G-1:** `FristScheduler.TickAsync` entfernt eine Frist nur bei „zugestellt“ oder „abgelehnt“; „unbekannt“ bleibt stehen.
3. **G-2 (Emittent):** im `ProjectionAdapter` lesen Signal **und** Poll eines Emittenten ab dem Cursor. Die Commands eines Tails gehen parallel
   hinaus und werden gemeinsam abgewartet; der Cursor rückt nur vor, wenn alle zugestellt sind.
4. `PullPathGenerator`: der generierte Emit-Pfad wartet (statt `DetachedEmit.Wrap`) mit gebundenem Token (**P1-6**).
5. `IEmittentenCursor`: Vertragstext „durabel, at-least-once ab Cursor, kein Reset“.
6. Tests: Prüfstand (`EmittentenPfadAdapterTests`, `EmittentenCursorTests`, Fristen mit Timeout- und Fehler-Seam); Integration: Absturz
   zwischen Emit und Cursor → erneutes Senden wird dedupliziert.
7. Doku: 04, 10, 11 (§11.6, §11.7), 13 (G-1, P1-6 → §13.6).

*Abnahme:* Tests grün; E2E-Messung S1–S5; in S5 hängen die Kosten der Reaktion nicht mehr von der Stream-Länge ab; keine Regression in S1–S4.

### Phase 2 · Actor-Lebenszyklus (3–5 PT)

1. **G-3:** ein Append-Fehler setzt `_initialized` zurück und rehydriert frisch. Liegt die CommandId danach im Log (Event mit `causationId`
   oder Marke), wird Erfolg quittiert — der mehrdeutige Commit wird kein falsches Negativ mehr (P10).
2. **PF-5:** Passivierung für Aggregat-Actor, Adapter und Prozess-Manager über einen Leerlauf-Timeout (E7); der Snapshot beim Stoppen bleibt.
3. Tests: Integration — Versionskonflikt durch einen zweiten Schreiber, mehrdeutiger Commit über einen Fehler-Seam im Appender; Prüfstand —
   Passivierung und Reaktivierung ergeben denselben Stand.

*Abnahme:* Tests grün (Konflikt und mehrdeutiger Commit heilen ohne Neustart); E2E-Messung S1–S5; RSS im Leerlauf nach den Blöcken deutlich
unter dem Höchststand (Passivierung).

### Phase 3 · Batch-Konsum (15–25 PT)

1. **Entwurf (2–3 PT), festgehalten in docs/04** — von Tobi freigegeben (Haltepunkt (b), 2026-10-15, E4, E8–E11 entschieden), umgesetzt und in
   [04 §4.1.1, §4.3](04-konsum-und-prozess-maschine.md) eingearbeitet: ein Batch-Leser je (Projektion, Partition) liest den globalen Sequenzbereich seitenweise
   (z. B. 500), gruppiert je Stream, wendet je Stream in Versionsreihenfolge an und committet Effekte und Checkpoints **aller** Streams des
   Batches in einer Transaktion. Zu klären und festzuhalten: Checkpoint je Partition (globale Position) und je Stream (Guard), Rolle des Signals
   (weckt die Partition), Konflikt bei geteilten Dokumenten (Batch verwerfen, halbieren, einzeln wiederholen), Rebuild als Strom, Lease je
   Partition im Cluster mit Versionsprüfung des Checkpoints (**G-4**).
2. Infrastruktur: Batch-Leser; `MartenCoCommitStoreBase` für mehrere Streams (Bereich je Batch statt je Stream); der Batch-Leser ersetzt den
   Heil-Poll der Projektionen (**PF-2**); eine Rückstands-Abfrage für `/health`.
3. Generator: `PullPathGenerator` erzeugt den Batch-Dispatch. Die Handle-Signaturen im Fachcode bleiben unverändert (Inv. 5).
4. ~~Emittenten laufen über denselben Leser mit Cursor je Partition~~ — entfällt (E8): Emittenten bleiben je Stream.
5. Rückbau: Adapter-Actor je (Projektion, Stream) und `SignalAdapterActor`-Pfad für Projektionen.
6. Tests: Absturz mitten im Batch (`CoCommitPostgresTests`), Ordnung je Stream innerhalb einer Partition, geteilte Dokumente, Rebuild, zwei
   Knoten mit Lease.
7. Doku: 03, 04, 11, 12, 13.

*Abnahme:* E2E-Messung S1–S5 erreicht die E5-Zielwerte (Richtwert: Durchsatz bis „alles sichtbar“ S1/S5 ≥ 5 × Baseline).

### Phase 4 · Inbox als Tabelle (6–9 PT, Variante nach E1)

1. Tabelle `inbox (command_id PK, stream_id, version, ausgang, zeit)` über Martens Schema-Erweiterung; Insert in derselben Session wie der
   Append (`MartenEventBatchWriter`, `MartenEventStore`); ein Duplikat meldet der eindeutige Schlüssel (23505), der `BatchingEventAppender`
   unterscheidet Duplikat und Konflikt.
2. Actor: Duplikat → idempotent quittieren (Pfad aus Phase 2); `BoundedInbox` bleibt als kleiner Cache vor dem Append.
3. Marke `KommandoVerarbeitet` nur noch bei Noop (Variante A); Inbox raus aus Rehydration und Snapshot (`AggregateRehydrator`,
   `Abstractions/Snapshot.cs`).
4. Horizont statt Grenze (E2), Aufräum-Dienst mit Lease; Bestandsdaten nach E3.
5. Tests: Integration — Dedup über 10 000 hinaus, Duplikat mitten im Batch, Horizont; Prüfstand anpassen (`BoundedInboxTests`,
   `AblehnungsMarkeTests`).
6. Doku: 03, 04, 11 (§11.3: Fenster → Horizont), 12, 13.

*Abnahme:* Exactly-once über den Horizont nachgewiesen (Integration); E2E S3 heiß zählt genau einmal; Durchsatz S1 über dem Stand nach Phase 3.

### Phase 5 · Prozess-Poll und Backstop (3–5 PT)

1. `ReadChangedStreamsAsync` liefert die geladenen Events mit; der Prozess-Poll routet sie direkt statt jeden Stream ab 0 zu lesen.
2. Fälligkeits-Index für Zeitlimits (zusammen mit dem Fristplaner, W11-2); der Backstop weckt nur noch Fällige, die Voll-Heilung läuft selten;
   Lease im Cluster.

*Abnahme:* E2E S3: Zeit je Teil konstant von N = 1 000 bis 10 000; S5 mit 10 000 offenen Vorgängen im Leerlauf < 10 Queries/s; Zeitlimit-Genauigkeit < 1 s.

### Phase 6 · Rest (2–3 PT)

- **PF-6:** Read-your-writes-Index je Projektion abschaltbar.
- **G-5:** Rehydration zählt je gespeicherter Position; Test mit Split-Upcast.
- **PF-7:** Entscheidung zu hochfrequenten Quellen festhalten (Stream je Meldung gegen Dedup-Tabelle und Sammel-Streams).

## 4 · Arbeitsweise je Phase

- Ein Commit (oder wenige) je Phase; Prüfstand und die Integrations-Suiten **nacheinander** grün; **Garantie-Suite grün** (§0.2);
  `GraphExtractor -- --check`; E2E-Messung mit Exactly-once-Prüfung und ein Störungslauf nach §0.1/§0.2.
- Jede Phase entfernt das Skip der Lücken-Tests, die sie schließt; neue Garantien oder Mechanismen bekommen ihren Garantie-Test im selben Commit.
- Im selben Commit: Ergebnis-Ordner unter `vermessung/ergebnisse/`, Tabelle in §12.5, erledigte IDs von §13.0 nach §13.6, Garantien und
  Nachweis-Spalte in [11](11-garantien.md).
- Kein `InMemoryEventStore`: Store-Semantik nur gegen echtes Marten.

## 5 · Verhältnis zum Werkbank-Strang (§13.5)

Der Werkbank-Strang ruht während dieses Plans; es arbeitet kein zweiter Agent. Wo sich die Themen berühren, gilt:

| Berührung | Werkbank-Strang (später) | dieser Plan |
|---|---|---|
| Handle-/Trigger-Welt (`PipelineActorGenerator`, `PipelineActorBase`, `DetachedEmit`) | Rückbau W11-1 | passt sie in Phase 1 minimal an (E6) |
| Fristplaner, Zeitlimits | W11-2 | Phase 1 (G-1) und Phase 5 erledigen den Zeitlimit-Anteil; W11-2 übernimmt den Rest |
| `PullPathGenerator`, Konsum-Maschine | – | nur dieser Plan |

## 6 · Risiken

1. **Batch-Konsum und geteilte Dokumente:** ein Konflikt verwirft einen ganzen Batch; bei hoher Konkurrenz auf einem Dokument sinkt der
   Gewinn. Gegenmittel im Entwurf (Phase 3.1).
2. **Marten:** Insert per SQL in derselben Session wie der Append (Phase 4) und Co-Commit über viele Streams (Phase 3) sind vorgesehen, hier
   aber noch nicht genutzt — der erste Integrationstest der Phase klärt es.
3. **Abgewartete Emits** verlängern den Turn eines Emittenten; paralleles Senden je Tail dämpft es (Phase 1).
4. **Messung auf dem Entwickler-Rechner** schwankt unter Fremdlast (§12.5: Faktor 2 zwischen zwei Läufen); deshalb kurze Läufe, die man bei
   Bedarf wiederholt, Fremdlast vermerken, und Abnahmen vergleichen Verhältnisse zur Baseline auf demselben Rechner.

## 7 · Nicht in diesem Plan

Zellen und Föderation über Anlagen, Store-Sharding, flüchtige Quellen und Telemetrie, OT-Treiber (OPC UA, MQTT), Transport-Sicherheit
(P1-2), Multi-Node-Betrieb (P1-1), Observability (P2-10). Sie bauen auf diesem Plan auf und bekommen eigene Einträge in §13.0, wenn sie
anstehen.

## 8 · Stand der Umsetzung

**Phase 0** (abgeschlossen bis auf Haltepunkt (a), 2026-10-15):
- *Abgleich:* seit `e6e08ee` (Befunde G/PF eingetragen) hat kein Commit G- oder PF-Code berührt; G-1 bis G-5 sind im Code bestätigt
  (`CommandEmitter` verschluckt Timeout und `CommandResult`, `ProjectionAdapter` liest beim Poll eines Emittenten ab 0, `AggregateActorBase` ohne
  Rehydration nach Append-Fehler, `MarkProcessedAsync` per `Store`, `AggregateRehydrator` zählt je Event).
- *Entscheidungen E1–E7:* gebündelt an Tobi gegeben (2026-10-15); ohne Antwort gelten die Vorschläge der Tabelle §2.
- *Messaufbau:* `scripts/vermessung-e2e.sh` mit dem Treiber `Cqrs.Vermessung` (Messung unter Höchstlast, Störungslauf), Ablage und
  `scripts/vermessung-tabelle.sh`, Umgebungen `vermessung` und `vermessung-pruefdomaene`.
- *Garantie-Nachweis:* Garantie-Matrix ([11](11-garantien.md), Nachweis-Spalte, §11.14), Garantie-Suite (`Kategorie=Garantie`), je Lücke G-1 bis
  G-5 ein Test mit Skip — jeder einmal ohne Skip rot gesehen.

- *Baseline:* `vermessung/ergebnisse/2026-10-15_0429_3cfec32_baseline` (gültig, Exactly-once grün) und
  `…_0433_3cfec32_baseline-stoerung` (gültig, Exactly-once grün); Tabelle in docs/12 §12.5.
- *E5-Zielwerte (von Tobi am Haltepunkt (a) bestätigt, 2026-10-15):* Phase 3 — Durchsatz bis „alles sichtbar“ S1 ≥ 60 Paare/s (3 ×) und S5-8
  ≥ 400 Vorgänge/s (3 ×), Queries je Vorgang S1/S5 höchstens ein Drittel; Phase 5 — Leerlauf L1/L5 unter 10 Queries/s und 5 % CPU; Phase 2 —
  RSS im Leerlauf nach den Blöcken unter 50 % des Höchststands; Phase 4 — S3 heiß Durchsatz ≥ 1,5 × des Stands nach Phase 3. Dreifach statt
  fünffach, weil der Host schon bei 300–370 % CPU auf einem MacBook Air misst und Postgres auf demselben Rechner läuft.

**Haltepunkt (a)** (2026-10-15): Tobi bestätigt die E5-Zielwerte und nimmt G-7 und PF-9 **ganz** in Phase 1.

**Phase 1** (abgeschlossen, 2026-10-15, Commit `b955d40` und der Mess-Commit danach):
- *Umgesetzt:* `EmitAusgang` (zugestellt, abgelehnt, unbekannt) und `CommandResult.Abgelehnt`; G-1 im `FristScheduler`; G-2 (Emittent):
  Signal und Poll ab Cursor, Commands eines Tails parallel (`EmitAusgaenge`), Cursor nur nach bestätigtem Emit, `WakeAck.Aufgeholt`; P1-6 in
  beiden Generatoren; G-7 in `MartenEventStore` und `SpurLeser`; PF-9 über `DetachedProzessSend` (Sperre je Vorgang, Backoff 1–30 s) und
  `EmitDrossel` (8 je Ziel und Knoten). Entfallen: `DetachedEmit`, `Wake.VomPoll`.
- *Lücken-Tests ohne Skip, grün:* P `FristGarantieTests.Emit_Timeout…`, `…Technischer_Fehlschlag…` (G-1), P `KonsumGarantieTests.Ein_nicht_zugestellter_Emit…`
  (G-2 Emittent), I `GarantiePostgresTests.Ein_gescheiterter_Append…` (G-7, ohne den Filter rot gesehen). Neu: P `EmitGarantieTests`, zwei
  Konsum-Garantien, PD `AblaufGarantieTests.Absturz_zwischen_Emit_und_Cursor…`.
- *Messung:* `vermessung/ergebnisse/2026-10-15_0539_b955d40_phase-1` (gültig, Exactly-once grün) und `…_0542_b955d40_phase-1-stoerung`
  (gültig, grün). Gegen die Baseline: S1 1,57 ×, S2 1,45 ×, S3-8 1,92 ×, S3-64 1,95 ×, S4 1,67 ×, S5-1/8/64 1,37/1,23/1,14 × (Durchsatz bis alles
  sichtbar); Reaktion S5-64 p50 0,63 ×; Queries je Vorgang 0,88–1,05 ×. Keine Regression in S1–S4. Leerlauf L1 schlechter (Erstweckungen der
  S3-Streams durch den Heil-Poll, PF-2; docs/12 §12.5). **Abnahme: ja.**

**Phase 2** (abgeschlossen, 2026-10-15, Code `e47fdeb`, Boot-Guard `d7941e5`, Treiber `860cb7a`):
- *Umgesetzt:* G-3 — `AppendeAsync` im Aggregat-Actor (frisch rehydrieren, entschiedenen Ausgang quittieren, sonst einmal neu entscheiden);
  PF-5 — Passivierung über `ReceiveTimeout` (Aggregat, Adapter, Prozess-Manager), Umgebungs-Abschnitt `Lebenszyklus` (E7: 600/120/600 s),
  Boot-Guard Prozess-Manager ≥ 60 s; aktive Actors in `/monitoring/metrics` und `/health`.
- *Lücken-Tests ohne Skip, grün:* PD `SchreibGarantieTests.Nach_einem_Versionskonflikt…`, `…Ein_mehrdeutiger_Commit…` (G-3). Neu: PD
  `LebenszyklusGarantieTests`, P `PassivierungTests`.
- *Messung:* `vermessung/ergebnisse/2026-10-15_0745_4caa176_phase-2` (gültig, grün) und `…_1119_860cb7a_phase-2-stoerung` (gültig, grün,
  29 609 Vorgänge). S1–S4 0,92–1,07 × Phase 1; S5 im Gesamtlauf 0,81–0,89 ×, allein gemessen (A-B-B-A mit/ohne Passivierung) auf oder über
  Phase 1 — die Passivierung kostet keinen Durchsatz. Abgelegt, aber nicht gewertet: Läufe mit 10 s Passivierung (`…_0646_…`, Störungslauf
  `…_0706_…` rot: Todesspirale), Kontroll-Läufe unter Wärme (`…_0650_…`, `…_0654_…`), ein Lauf unter Steam-Update (`…_0723_…`), ein
  Störungslauf, der nur die feste Abbaufrist riss (`…_0804_…`).
- *E5 RSS:* nicht erreicht und im 5-min-Lauf nicht messbar (L1 49–55 %, L5 84–94 % des Höchststands): mit 600 s passiviert im 15-s-Fenster
  nichts, und die Leerlauf-Blöcke sind bis Phase 3/5 nicht leer (PF-2, PF-4). Neu bewerten nach Phase 3 und 5. **Abnahme: ja** (Tests,
  Garantien, E2E ohne Regression); das RSS-Ziel ist auf Phase 3/5 verschoben (Vorschlag, Tobi bestätigt).

**Phase 3** (umgesetzt, 2026-10-15, Code `0b02802` und der Mess-Commit danach; **Abnahme: nein** — S1-Ziel verfehlt, s. u.):
- *Umgesetzt:* `PartitionsCheckpoint` (Position, Version als Marten-Revision, Geparkt) und `Partitionierung` (letztes UUID-Byte mod P);
  `IProjectionTracker` ist die Batch-Naht (`LadePartitionAsync`, `LadeGuardsAsync`, `BeginneBatch`, `Stream`, `CommitAsync`, `ResetAllAsync`),
  `ICoCommitTracker` bleibt der Typ-Schnitt für GA-1; `MartenCoCommitStoreBase.CommitAsync` (Effekte, Guards als Bulk-Store, Partition per
  `Insert`/`UpdateRevision` in einem SaveChanges); `MartenEventStore.ReadPartitionAsync` (gesetzte Sequenz vor der Seite, Partitionsfilter,
  Typfilter aus der neuen generierten Tabelle `GeneratedEventUpcasting.Herkunft`); `BatchLeser`/`BatchLeserActor` (Position mit Karenz,
  Lese-Position im Speicher, Guard je Stream, Abtrennen/Halbieren, Parken, Konflikt-Wiederholung, Weckung coalesced) und
  `BatchKonsumStartupService` (Signal → Partition, Poll je Partition), Rückstand in `/health`; `PullPathGenerator`: `batch-<Typ>` für
  Projektionen, `pull-<Typ>` für Emittenten; Rückbau: `ProjectionAdapter` → `EmittentenAdapter` (nur Emittenten), `MartenProjectionTracker`
  und `IReplaybarerTracker` entfernt; `ProjectionRebuilder` und `ProjektionsStandDienst` auf Partitionen (Partitionszahl gehört zum Stand);
  Umgebung `Konsum` (Partitionen 8, BatchGroesse 500) in `UmgebungKonfig`, Lader, Editor, docs/konzept-werkbank.md.
- *Lücken-Tests ohne Skip, grün:* I `GarantiePostgresTests.Zwei_Aktivierungen…` (G-4, auf den Batch-Leser umgestellt). Neu: P
  `BatchKonsumGarantieTests` (7), `BatchLeserTests` (2); I `…Ein_Poison_Stream_wird_im_Batch_geparkt…`, `…Ein_Neuaufbau_nimmt…`,
  `SequenceGapPostgresTests.Batch_Leser…`, `PollerBackstopPostgresTests.Batch_Leser…`; umgebaut: `CoCommitPostgresTests` (Absturz mitten im
  Batch), `SchreibRegelPostgresTests`/`GeteilteDokumentePostgresTests` (zwei bzw. vier Partitions-Leser), `ProjectionRebuilderTests` (Strom).
- *Probe vor dem Commit* (`--probe`, nicht abgelegt): Exactly-once in allen Blöcken grün; Leerlauf L1 0,4 Queries/s und 2 % CPU (Phase 2:
  5 182 Queries/s, 337 %).
- *Messung* (starke Fremdlast: ein Spiel lief, Last 10 → 27, im ersten Störungslauf bis 82): `vermessung/ergebnisse/2026-10-15_1312_0b02802_phase-3`
  (gültig, Exactly-once grün). Gegen Baseline / Phase 2: S1 1,32 × / 0,79 × (26,2 Paare/s), S2 1,28 × / 0,90 ×, S3-8 1,54 × / 0,84 ×, S3-64
  1,28 × / 0,71 ×, S4 1,28 × / 0,76 ×, S5-1/8/64 0,79/0,80/0,24 × Baseline. Je Vorgang: S2 Transaktionen 0,59 × Phase 2, S3 0,48–0,73 ×, S1
  0,91 × (Queries 1,05 ×, davon 64 von 239 das Treiber-Polling). Leerlauf L1 0,1 Transaktionen/s (1 536), 602 Queries/s (5 182), 33 % CPU (337 %),
  RSS am Ende 0,1 des Höchststands. A-B-B-A S1/S5 gegen Phase-2-Code (`…phase-3-ab-s1s5-*`, A aus einem Worktree auf `ee401da`): S1 28,6 / 23,0 /
  22,2 / 11,3 Paare/s (Rauschen der Fremdlast), S5-8 125 / 146 / 154 / 76, S5-64 97 / 120 / 122 / 130 Vorgänge/s — Phase 3 kostet S5 nichts; der
  S5-Einbruch im Gesamtlauf ist Fremdlast.
- *Störungslauf:* `…_1333_0b02802_phase-3-stoerung` **ungültig** (Exactly-once rot) — Ursache geklärt: nicht doppelt gewirkt (DB im Ruhezustand: keine
  CommandId zweimal, keine doppelte Version, Reaktion 21 927 = 21 927), sondern nicht abgebaut: zwei Vorgänge mit beim Host-Abbruch verlorenem
  Start-Signal startet nur der Prozess-Poll, und dessen erster Durchlauf nach dem Neustart (Cursor 0, jeder Stream ab Version 0, PF-4) kam unter
  Last 82 in 10 min nicht durch; dazu ein Zählfehler des Treibers (VM-1). Wiederholung `…_phase-3-stoerung-2` **gültig, Exactly-once grün**
  (24 699 Vorgänge, 182 Bildpaare, Abbau 371 s).
- *Abnahme (E5, E9):* S1 ≥ 3 × Baseline **nein** (1,32 ×), Queries je Vorgang S1 ≤ ⅓ **nein** (0,93 ×); S2–S5 ohne Regression gegen Phase 2: nach
  A-B-B-A für S5 ja, für S2–S4 unter dieser Fremdlast nicht belastbar (0,71–0,90 ×, ohne A-B-B-A); Exactly-once im Störungslauf ja (zweiter
  Lauf). Leerlauf L1 und RSS-Ziel (E5): L1 ist leer bis auf den Prozess-Poll (PF-4), RSS am Ende 0,1 des Höchststands — das E5-RSS-Ziel ist für L1
  erreicht, für L5 knapp (0,5). **Die Phase ist nicht abgenommen**: das S1-Ziel setzt voraus, dass der Konsum die Decke von S1 ist — gemessen ist
  es der Datei-Treiber (PF-10).

**Phase 4** (umgesetzt, 2026-10-15, nach Phase 5; Code `aef3d46` (Inbox-Tabelle), `cf30aa0` (schneller Anhänge-Modus), gemessen auf dem
Messserver; E1 = A, E2 = 30 Tage, E3 = zurücksetzen):
- *Umgesetzt:* Tabelle `cqrs_inbox` (`InboxTabelle`, Martens `ExtendedSchemaObjects`: `command_id` PK, `stream_id`, `abgelehnt`, `zeit`, BRIN auf
  `zeit`); `IEventStoreRepository.AppendEventsAsync(…, Quittung?)` schreibt die Quittung in derselben Transaktion (Einzel-Append eine Zeile,
  Group-Commit eine Anweisung für alle), `AusgangAsync` liest sie; Schlüsselverletzung → `KommandoSchonEntschiedenException`, der Appender
  isoliert wie bei einem Konflikt. Actor: Marke nur noch bei Noop/Ablehnung, Inbox raus aus Rehydration und Snapshot, `BoundedInbox` bleibt als
  Cache (1 000 je Ausgang); Nachschlagen nur nach einem gescheiterten Append und vor einem Versionskonflikt; eine erkannte Wiederholung
  veröffentlicht keine Signale. Polly wiederholt keine Eindeutigkeitsverletzungen mehr. Horizont `Schreibpfad.InboxHorizontTage` (ersetzt
  `InboxGrenze`), `InboxAufraeumer` (10 min, seitenweise, Advisory-Lock als Lease). Martens Anhänge-Modus `Quick` (Recherche im Marten-Quellcode
  8.20: bei Appends mit erwarteter Version vergibt Marten die Versionen selbst, OCC über `update … where version = erwartet`, Metadaten und
  Zeitstempel wie bisher, keine Tombstones). Messtreiber: „keine CommandId zweimal gewirkt“ zählt jetzt Transaktionen (`xmin`) je CommandId der
  Inbox statt Marken.
- *Lücken-Test ohne Skip, grün:* PD `SchreibGarantieTests.Eine_alte_CommandId_jenseits_der_Inbox_Grenze…` (G-2). Neu: PD `…Eine_Wiederholung_nach_dem_Neustart…`,
  `…Ein_Duplikat_mitten_im_Group_Commit…`, `…Die_Inbox_vergisst_erst_jenseits_des_Horizonts`; I `GarantiePostgresTests.Effekt_und_Quittung…` und
  die Append-/OCC-Garantien je in Rich und Quick. Gegenprobe (Actor ohne Quittung): 5 von 7 Schreib-Garantien rot. Umgebaut:
  `RehydrationGarantieTests`, `SnapshotTests`, `TwoNodeCommandDispatchTests` (Version 1 statt 2).
- *Messung (Server-Tabelle §12.5, Spalten „Inbox-Tabelle“, „Schnell anhängen“):* `…_2152_aef3d46_server-inbox`, `…_2207_cf30aa0_server-quick`
  (gültig, grün), Störungsläufe `…_2156_…`, `…_2210_…` (gültig, grün, 28 004 bzw. 28 502 Vorgänge). A-B-B-A gegen `3483a31`
  (`…_server-schreibweg-ab-*`): S3-8/S3-64 1,18/1,17 ×, S5-1/8/64 1,08/1,06/1,06 ×, S1 1,04 ×, S4 1,01 ×; Queries je Vorgang 0,78–0,89 ×; je Vorgang
  S5-8 Event-Inserts 11,5 → 6,9, Stream-Kopf-Reads 4,8 → 0, Sequenz-Abfragen 1,4 → 0, Transaktionen gleich (≈ 10 BEGIN/COMMIT).
- *Abnahme:* Exactly-once über den Horizont (Integration) **ja**; E2E S3 heiß genau einmal **ja** (alle Läufe); „S3 heiß ≥ 1,5 × des Stands
  nach Phase 3“ — auf dem Messserver gibt es keine Phase-3-Spalte; gegen die Server-Baseline S3-8 2,24 ×, S3-64 2,23 ×, davon diese Phase
  1,17–1,18 × (der Rest kam mit „Korreliert lesen“); „Durchsatz S1 über dem Stand nach Phase 3“ **ja** (62 gegen 57,8 Baseline, 1,04 × im
  A-B-B-A — S1 misst den Datei-Treiber, PF-10). Die im Group-Commit gemessenen 35–40 % der Marke zeigen sich Ende zu Ende nicht: die
  Transaktionen je Vorgang bleiben, und die Decke liegt in Konsum und Dirigent. **Abnahme: Vorschlag ja** (Tobi).
- *Neuer Befund:* PF-14 (Leerlauf L1/L5 wieder über dem E5-Ziel, seit „Korreliert lesen“; L5 86 Queries/s = Nachlauf der Reaktion, PF-13).

**Nachtrag Prozess-Manager** (2026-10-16, auf Tobis Wort, Code `c450a3e`…`6a437ba`, Messserver): der Manager liest nur, was er nicht schon
weiß (gezielte Weckungen, „gelesen, nichts da“, frischer Vorgang, Kopf-Zustand im Speicher), Offen-Index als Beilage im Append (docs/13 §13.6).
Messung `…_0026_6a437ba_server-prozess-manager` (gültig, grün), Störungsläufe `…_0030_…`/`…_0033_…` (grün), A-B-B-A `…_server-pm-ab-*`:
S5-64 457 → 1 246 Vorgänge/s, S5-8 451 → 1 030, S1 1,40 ×, S3/S4 unverändert; gegen die Server-Baseline S5-64 6,0 ×, S5-8 2,8 ×, S1 1,54 ×.
Damit ist das alte E5-Ziel „S5-8 ≥ 3 × Baseline“ fast erreicht (2,85 ×); S1 bleibt am Datei-Treiber (PF-10).

**Nachtrag Aggregat-Bündel** (2026-10-16, auf Tobis Wort, Code `2390f1e`, Messserver): ein Aggregat-Actor sammelt während seines Commits und
hängt die gesammelten Commands in einem Append an (docs/13 §13.6). Messung `…_0146_2390f1e_server-buendel` (gültig, grün), Störungsläufe
`…_0150_…`/`…_0153_…` (grün), A-B-B-A `…_server-bu-ab-*`: S3-8 1,72 ×, S3-64 2,29 ×, S5 unverändert. Gegen die Server-Baseline: S3-64 5,9 ×,
S3-8 3,7 ×, S5-64 6,2 ×, S5-8 2,6 ×, S1 1,54 × — das alte E5-Ziel „S3 heiß ≥ 1,5 × des Stands nach Phase 3“ ist auch gegen die Baseline weit
übertroffen.

**Nachtrag PF-14 Leerlauf** (2026-10-16, Code `f55494b`, Treiber `3a1355b`/`7a1f9f9`, Messserver): die Leerlauf-Last nach den Blöcken (L1 ≈ 30,
L5 ≈ 113 Queries/s) war **Nachlauf, keine bleibende Grundlast** — belegt mit einer Zeitreihe über 5 min nach der Last und nach einem Neustart
(Diagnose `--art leerlauf`, docs/12 §12.5): der erste Poll-Takt nach der Last liest einmal nach, was Prozess- und Emittenten-Poll unter Last nicht
bestätigt vorrücken konnten (G-8), danach 2,4–5 Queries/s und 1–3 % CPU. Das 15-s-Fenster traf genau diesen Takt. Geändert: die Messung wartet
den Nachlauf ab (alle Konsumenten am Kopf, Dauer und Queries als Kennzahlen) und misst 60 s; ein Poll-Read ohne Neues kostet eine Abfrage statt
zwei. Messung `…_0309_7a1f9f9_server-nachlauf` (gültig, grün), Störungsläufe `…_0315_…`/`…_0318_…` (grün): L1 3,8 Queries/s und 2,6 % CPU, L5
2,6 und 1,5 % — **E5-Ziel Leerlauf erreicht**; Nachlauf L1 48 s, L5 15 s; Lastblöcke im Band der Läufe davor. Neu: PF-15 (die Grundlast wächst mit
Projektionen × Partitionen).

**Phase 5** (umgesetzt, 2026-10-15, auf Tobis Wort vor Phase 4; Code `22632bc`…`ffaeaea`, gemessen auf dem Messserver, docs/12 §12.6):
- *Umgesetzt:* PF-4 — `ProzessPoll` liest seitenweise (`ReadPartitionAsync`) und routet die geladenen Events, Starts nur für neue oder offene
  Vorgänge (`StreamsVorhandenAsync`), Marke je Seite; Backstop mit Fristen im Offen-Index (`ProzessOffen.Frist`, Sekundentakt, geschrieben erst
  bei Stille) und `Heilplan` (Heilung nach 15 s Stille, Backoff bis 1 h). G-8 — bestätigte Poll-Weckungen. Nebenbei: rohes SQL legte die
  Event-Tabellen nie an (der alte Poll tat es zufällig über LINQ) → `MartenEventStore.EventTabellenAsync`; der Treiber hat seitdem 30 s
  Warmlauf nach dem ersten Host-Start (ohne ihn maß der erste Block kalten JIT-Code: S3-8 −28 %, per Bisektion gefunden).
- *Lücken-Tests ohne Skip, grün:* P `ProzessPollGarantieTests.Eine_unbestaetigte_Start_Weckung…` (G-8, ohne die Regel rot gesehen). Neu: P
  `ProzessPollGarantieTests`, `ProzessFristGarantieTests`, `HeilplanTests`, `ProzessPollFilterTests` (neu gefasst).
- *Messung (Server-Tabelle §12.5):* `vermessung/ergebnisse/2026-10-15_1821_ffaeaea_server-pf-4` (gültig, grün) und
  `…_1825_ffaeaea_server-pf-4-stoerung` (gültig, grün, 51 027 Vorgänge, Abbau 316 s). Gegen die Server-Baseline: Leerlauf L1 351 → 2,5
  Queries/s, CPU 24 → 3,7 %; S5-1/S5-64 1,17/1,16 ×, S5-64 Queries je Vorgang 0,84 ×, Latenz p50 0,78 ×; S1–S4 0,98–1,11 ×, Kosten je Vorgang
  gleich. A-B-B-A gegen `365a3b0` (Ordner `…_server-pf-4-ab-*`, noch ohne Warmlauf): S5-64 1,10 ×, L1-CPU 0,17 ×; die dort gemessenen S1/S3-8-Verluste
  waren der kalte Start (mit Warmlauf S3-8 374 gegen 380 Teile/s). Nicht gewertet: `…_1713_…`/`…_1715_…` (Host nicht bereit: Tabellen fehlten),
  `…_1721_…`/`…_1723_…` (kalter Start).
- *Abnahme:* E5-Ziel Leerlauf **L1 ja** (2,5 Queries/s, 3,7 % CPU), **L5 nein** (1 930 Queries/s, 140 % CPU) — dort holt die Reaktion ihren
  Rückstand aus S5-64 auf, der Dirigent ist still (PF-12, neu). Nicht gemessen, weil der Treiber kein Szenario hat: „S3 Zeit je Teil konstant
  über N“ und „10 000 offene Vorgänge im Leerlauf < 10 Queries/s“ (rechnerisch: eine Abfrage je Sekunde für die Fristen, eine je 15 s für den
  Abgleich, Heilungen ≈ 10 000 / 1 h ≈ 3 Weckungen/s nach dem Backoff) — PF-4b. Zeitlimit-Genauigkeit < 1 s: im Prüfstand (Frist = Ablauf),
  Takt 1 s. **Abnahme: teilweise** (L5 hängt an PF-12).
- *Neue Befunde:* PF-11 (Emittenten-Poll ungeseitet), PF-12 (Reaktion hängt nach Höchstlast nach), PF-4b (Lease, fehlende Szenarien).

**Nachtrag PF-12** (2026-10-15, Code `562e793`…`5bda1bb`, Messserver): der L5-Rest war nicht die Reaktion, sondern der Emittenten-Poll (jeder
geänderte Stream bekam einen Cursor), der serielle Korrelations-Router (Read des Stream-Rests je Signal) und nicht zusammengefasste
Emittenten-Weckungen; dazu der Offen-Index nach statt vor dem `ProzessGestartet` (Störungslauf rot, behoben). Messung
`…_1940_5bda1bb_server-pf-12` (Spalte „PF-12“) und `…_1944_…`/`…_1947_…-stoerung-1/2` (gültig, grün): L5 2,5 Queries/s, 3,4 % CPU — **E5-Ziel
Phase 5 damit erreicht (L1 und L5)**; S5-64 1,69 × Baseline. Offen: PF-13, PF-4b. Abgelegt, nicht gewertet: `…_1912_…` (vor der Bündelung),
`…_1929_…-stoerung` (rot, 51 Vorgänge ohne Index-Eintrag — der Befund).

**Plan-Änderungen aus Phase 3:**
0. *Vorschlag an Tobi (offen):* E5 für Phase 3 neu fassen — S1 misst den seriellen Datei-Treiber (PF-10), nicht den Konsum. Entweder das
   S1-Ziel an PF-10 hängen (Melden in Bündeln, eigene Entscheidung) oder Phase 3 an dem messen, was sie ändert: Leerlauf (erreicht), Kosten je
   Vorgang mit Projektionen (S2, S3 erreicht), Aufholen nach Stillstand bzw. Neuaufbau (Szenario fehlt noch — Vorschlag S6 „Aufholen“: Host
   anhalten, Last schreiben, Host starten, Zeit bis kein Rückstand in `/health`). Neue Befunde: **G-8** (Prozess-Poll ohne bestätigte Weckung,
   Vorschlag: mit PF-4 in Phase 5), **PF-10**, **VM-1** (docs/13).
1. Der Code des Entwurfs weicht in drei Punkten ab (Entwurf in docs/04 korrigiert und eingearbeitet): (a) wirft der Dispatch, ist der Stream
   des werfenden Events bekannt — er wird abgetrennt und allein versucht, halbiert wird nur bei Commit-Fehlern; (b) die Lese-Position läuft im
   Speicher, durabel wird sie mit jedem Batch und über fremde Events höchstens je Poll-Intervall (die Probe zeigte sonst einen Commit je Weckung);
   (c) ein Neuaufbau setzt die Partitionen mit neuer Revision zurück statt sie zu löschen — Martens `UpdateRevision` legt ein gelöschtes Dokument
   sonst neu an (gegen echtes Postgres geprüft).
2. Den Event-Zeitstempel setzt Marten mit der Uhr des Hosts, nicht der DB (Kommentare im `MartenEventStore` korrigiert): Ursache der Flakes
   von `SequenceGapPostgresTests` und `GarantiePostgresTests.Ein_gescheiterter_Append…` (Karenz 0, bis 0,2 ms Versatz) — die Tests warten jetzt kurz.
3. Die Basisklasse `MartenCoCommitStoreBase` liegt längst im Framework (`Core/`), nicht in `Domain.Infrastructure` (docs/04 korrigiert).

**Plan-Änderungen aus Phase 2:**
1. Die Leerlauf-Blöcke messen zusätzlich RSS am Fensterende, RSS-Höchststand des Laufs und aktive Actors; das E5-Ziel „RSS < 50 %“ wird nach
   Phase 3/5 geprüft (dann ist der Leerlauf leer); die Vermessung bleibt bei den E7-Standardwerten.
2. Der Störungslauf baut nach Fortschritt ab (höchstens 10 min, Hänger = 60 s ohne fertigen Vorgang) statt mit fester Frist von 3 min; die
   Gleichheits-Prüfungen von S1 und S4 sind rückstandsfest.
3. Boot-Guard: Leerlauf des Prozess-Managers 0 oder ≥ 60 s (4 × Backstop).
4. G-6 nach dem Code korrigiert (docs/13): der Pfad „Fehler nach dem Append“ ist nur über einen werfenden Applier erreichbar.

**Plan-Änderungen aus Phase 1:**
1. G-7 und PF-9 gehören zu Phase 1 (Tobi); Phase 5 bleibt nur PF-4. Die Drossel je Ziel sitzt im Emitter (ein Weg für alle Aufrufer), der Backoff
   im Sende-Takt des Dirigenten (`DetachedProzessSend`), nicht im `ProzessManager` — dessen Dispatch-Naht und alle Prüfstand-Fakes blieben gleich.
2. G-2 ist geteilt: der Emittenten-Anteil ist geschlossen; der Test PD `SchreibGarantieTests.Eine_alte_CommandId_jenseits_der_Inbox_Grenze…`
   bleibt bis Phase 4 übersprungen (das Fenster selbst).
3. Der Leerlauf L1 ist bis Phase 3 kein sauberer Vergleich: er enthält die Erstweckungen der neuen Streams durch den Heil-Poll (PF-2), deren
   Lage im Messfenster vom Tempo der Blöcke davor abhängt.

**Plan-Änderungen aus Phase 0:**
1. Die Prüfdomäne bekam eine Leseseite (`Pruefdomaene.Projections`: Reaktion `NachschubBeiMindestbestand`, Felder `Mindestbestand`/`Fehlmenge`
   am Posten) und einen Host (`Host.Pruefdomaene`, derselbe Laufzeit-Host mit ihrer Komposition). Damit eine Anwendung ohne Leitung booten
   kann, startet `AddCqrs` gRPC nur noch mit Leitung (`IAnwendung.Nutzlast` nullable).
2. Der JSON-Eingang nimmt eine optionale `commandId` (Client-Wiederholung mit derselben CommandId, §11.3; Störungslauf).
3. Gemessen wird unter Höchstlast in Minuten (Tobi, 2026-10-15): die erste Fassung (Rampe bis zur Sättigung, drei Wiederholungen, 8-h-Dauerlauf,
   1 h Stillstand, ≈ 12 h) war auf einem MacBook Air unverhältnismäßig und wurde nach 27 min abgebrochen (Ordner `…_baseline`, ungültig: S3 mit
   N = 10 000 baute sich bei 14 Teilen/s nicht ab). Jetzt je Block höchstens 15 s Volllast mit 1/8/64 Sendern, dann Abbau.
4. Neuer Befund **G-7** (P0, docs/13): ein gescheiterter Append hinterlässt Marten-Tombstones, an deren Stream jeder Poll hängen bleibt — der
   Neustart-Test der Prüfdomäne fand es. Er gehört in keine Phase; Vorschlag: mit Phase 1 schließen (gleiche Stelle, der Lesepfad der Polls).
5. Neuer Befund **PF-9** (P1): Retry-Sturm des Dirigenten unter Überlast; Vorschlag: mit Phase 1 (Emit-Ausgang) bzw. Phase 5 (Backstop).
6. Die Garantie-Matrix fand Garantien ohne eigenen Test; ergänzt wurden CQRS010/011/052/053, der Wire-Boot-Check, Rückstau der Quelle und
   Zeitlimit; offen bleibt der Post-Append-Pfad (G-6).
7. `--probe` (verkürzter Funktionstest des Treibers) schreibt nicht nach `vermessung/ergebnisse/` — Probeläufe während der Entwicklung des
   Treibers sind keine Messläufe.

## 9 · Übergabe an den nächsten Agenten (Stand 2026-10-16, nach Phase 4, zwei Nachträgen und PF-14)

- **Neu seit der letzten Übergabe:** PF-14 geklärt und erledigt (§8, Nachtrag PF-14 Leerlauf): Nachlauf, keine Grundlast; der Leerlauf wird erst
  nach dem Nachlauf gemessen, ein Poll-Read ohne Neues kostet eine Abfrage. Server-Tabelle §12.5, letzte Spalte „Nachlauf“
  (`…_0309_7a1f9f9_server-nachlauf`). Für Leerlauf-Fragen gibt es die Diagnose `scripts/vermessung-e2e.sh --art leerlauf` (Zeitreihe, kein
  Messlauf, Ordner im Temp-Verzeichnis des Servers, per `scp` holen).
- **Offen:** Tobis Abnahme von Phase 4, der Haltepunkt aus Phase 3 (Punkt 0 in §8, unverändert), PF-13, PF-4b, PF-15; danach Phase 6.

- **Wo es steht:** Phase 0, 1, 2 fertig und abgenommen; Phase 3 umgesetzt und gemessen, **nicht abgenommen** (S1-Ziel verfehlt, die Decke von S1 ist
  der Datei-Treiber, PF-10); Phase 5 (PF-4, G-8) umgesetzt und gemessen, mit dem Nachtrag PF-12 E5-Ziel erreicht (L1 und L5 unter 10 Queries/s und 5 % CPU; der scheinbare
  Rückfall seit „Korreliert lesen“ war Nachlauf im Messfenster, PF-14 erledigt); Phase 4 (G-2, PF-3) umgesetzt und gemessen, Abnahme bei Tobi;
  offen PF-4b, PF-13, PF-15. Gemessen wird
  seit 2026-10-15 auf dem Messserver (`scripts/server.sh`, docs/12 §12.6) mit eigener Tabelle; die Mac-Tabelle endet mit Phase 3.
  **Haltepunkt (offen):** Tobi entscheidet über den Vorschlag in §8 („Plan-Änderungen aus Phase 3“, Punkt 0: E5-Ziel für S1 neu fassen).
- **Danach:** Phase 6 (PF-6, G-5, PF-7) nach §3; daneben PF-13 (Reaktion unter S5-64), PF-15 (Grundlast je Projektion) und PF-4b.
- **Messen je Phase:** auf dem Messserver (`scripts/server.sh lauf scripts/vermessung-e2e.sh --anlass server-<phase> --spalte "<Phase>"`),
  Höchstlast ≈ 4 min, Störungslauf ≈ 5–8 min, Tabelle per Skript (Tabelle „messserver“). A-B-B-A über einen Worktree des alten Stands auf dem Server. Code committen, dann messen, dann Ergebnis committen. Nicht auf Abkühlung oder Fremdlast warten (Tobi), Fremdlast im Bericht nennen.
- **Vor jedem Commit:** `dotnet build`; Prüfstand; Garantie-Suite (Prüfstand, dann nacheinander Integration und Prüfdomäne); die vollen
  Integrations-Suiten; `GraphExtractor -- --check`; `./codegen.sh --check`; bei Generator-Änderungen das Generat inspizieren; bei Umgebungs- oder
  Editor-Änderungen die Werkbank-Abnahme. Commits ohne Co-Authored-By; nicht pushen ohne Tobis Wort.
- **Fallen:** Nach einer Gegenprobe mit sabotiertem Fix alle Testprojekte neu bauen. Läufe direkt hintereinander verlieren auf dem MacBook Air
  Durchsatz (Wärme) — Vergleiche lieber A-B-B-A. Integrations-Tests gegen langlebige Schemata: ein frischer Projektions-Schlüssel beginnt am Kopf
  des Logs (`BatchHilfe.StarteAmKopfAsync`); Karenz-0-Tests warten den Uhrenversatz ab. `git stash` nimmt neue Dateien nicht mit — nicht zum
  Zählen „vorher“ benutzen.
