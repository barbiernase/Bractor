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
   Tests und nie mit synthetischen Abkürzungen.

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
  | S1 | Beispieldomäne, Bildeingang | TIFF-Paare ins Quell-Verzeichnis → Fluss (Deutung, Paar, Verkleinerung, Kontrast) → `ImagePair` → Projektionen → Query „Bild verfügbar“ | Quelle, Dirigent, Funktionen, Konsum |
  | S2 | Beispieldomäne, Bildeingang mit Python-Worker (`Laufort = Extern`) | wie S1, Funktionen im Worker über die Leitung | Funktions-Vermittlung, Lease |
  | S3 | Beispieldomäne, Sammelvorgang | `StarteSammelvorgang(N)` + N Teilaufträge abschließen → `TeilFertigProzess` → Barriere erreicht; N = 1 000 und 10 000 | langlebiger Stream, Prozess-Poll, Inbox |
  | S4 | Beispieldomäne, Datensatz | Range über alle Bildpaare aus S1 aufnehmen, einfrieren → Datensatz eingefroren | Flüsse mit Lese-Fähigkeit, große Events |
  | S5 | Prüfdomäne | M parallele Auftrags-Vorgänge (Diamant, Kompensation) und Verteilungen (Fan-out, Count-Join), dazu eine Reaktion | Prozess-Maschine, Emittenten |

- **Kennzahlen je Szenario:** Sättigungsdurchsatz (Rate steigern, bis die p99-Latenz über 2 s steigt oder der Rückstand wächst), Latenz
  Eingang → sichtbar p50/p95/p99 bei 50 % der Sättigung, Transaktionen und Queries je Sekunde (`pg_stat_statements`), CPU und Speicher (RSS)
  des Hosts. Dazu einmal je Phase ein **Dauerlauf** (S1 + S3 bei 50 % Sättigung, mindestens 8 h, in Phase 2 24 h) und die **Aufholzeit** nach
  1 h Host-Stillstand.
- **Klar:** ein Befehl startet die Messung (`scripts/vermessung-e2e.sh`), feste Parameter, drei Wiederholungen, Median; Rechner, Commit-Hash,
  Fremdlast und Abweichungen stehen im Ergebnis. Vor der Phase wird gemessen (bzw. die letzte Messung gilt), nach der Phase dieselbe Messung
  auf demselben Rechner. Die Ergebnisse stehen als **eine fortlaufende Tabelle** in docs/12 §12.5 (Zeilen: Szenario × Kennzahl, Spalten:
  Baseline, Phase 1, Phase 2 …); verglichen werden Verhältnisse.

Ausgangslage aus der Messung (§12.5): eine Weckung je (Projektion, Stream) schafft etwa 8 200/s, ein Batch etwa 175 000 Effekte/s (Faktor
~20); die Inbox-Marke kostet 35–40 % im Group-Commit; ein Stream ab 0 statt ab Cursor kostet bei 100 000 Events Faktor > 2 000. Hochgerechnet
trägt die Konsumseite heute etwa 8 200 / K Events/s (K = replaybare Projektionen) — vor Marten und Netzwerk.

## 1 · Übersicht

| Phase | Inhalt | IDs | Aufwand | setzt voraus | Abnahme (Kurz) |
|---|---|---|---|---|---|
| 0 | Abgleich und E2E-Messung | – | 4–6 PT | – | Baseline S1–S5 in §12.5 |
| 1 | Emit-Ausgang und Emittenten-Cursor | G-1, G-2 (Emittent), P1-6 | 5–7 PT | 0 | S5: Kosten der Reaktion unabhängig von der Stream-Länge |
| 2 | Actor-Lebenszyklus | G-3, PF-5 | 3–5 PT | 0 | Dauerlauf 24 h S1 + S3: Speicher flach |
| 3 | Batch-Konsum | PF-1, PF-2, G-4 | 15–25 PT | 1, 2 | Sättigung S1/S5 deutlich über Baseline (Ziel nach E5) |
| 4 | Inbox als Tabelle | G-2 (Inbox), PF-3 | 6–9 PT | 2, E1 | S3 mit N = 10 000+: genau einmal; Sättigung S1 höher als nach Phase 3 |
| 5 | Prozess-Poll und Backstop | PF-4 (W11-2-Anteil) | 3–5 PT | 1 | S3/S5: Zeit je Teil konstant über N; Leerlauf mit offenen Vorgängen nahe 0 Queries/s |
| 6 | Rest | PF-6, G-5, PF-7 (Entscheidung) | 2–3 PT | – | – |
| | **Summe** | | **38–60 PT** | | etwa 8–12 Wochen für eine Person |

Reihenfolge: erst Korrektheit und das wachsende Leck (1), dann Stabilität im Dauerbetrieb (2), dann die Decke (3), dann die nächste Decke —
den Schreibpfad (4), zuletzt Grundlast und Rest (5, 6). Nach Phase 1 ist der Auslöser von G-2 (Emittent sendet ab 0 erneut) weg; deshalb darf
die Inbox-Tabelle (4) nach dem Batch-Konsum kommen.

## 2 · Entscheidungen vorab

| # | Frage | Vorschlag | nötig bis |
|---|---|---|---|
| E1 | Inbox: **A** Tabelle als Dedup, Marke nur bei Noop/Ablehnung — oder **B** Tabelle als „Ausgang je Command“, keine Marken im Stream (Prozess-Manager, Clients, Spur ändern sich; W9 berührt) | **A** | Phase 4 |
| E2 | Aufbewahrungs-Horizont der Inbox (Exactly-once gilt innerhalb dieses Horizonts) | 30 Tage, Umgebung `Schreibpfad.InboxHorizont` | Phase 4 |
| E3 | Bestandsdaten | Sim-DBs zurücksetzen; Backfill-SQL aus Marken und `causationId` nur, falls Prod-Daten bestehen | Phase 4 |
| E4 | Partitionen je Projektion im Batch-Konsum | fest 8, Umgebung; Lease je Partition im Cluster | Phase 3 |
| E5 | Abnahme-Zielwerte je Szenario | nach der Baseline festlegen; Richtwert Phase 3: Sättigung S1/S5 ≥ 5 × Baseline, Latenz p99 < 1 s bei 50 % | nach Phase 0 |
| E6 | Handle-/Trigger-Welt (W11-1) in Phase 1 zurückbauen oder nur anpassen? | nur minimal anpassen; der Rückbau bleibt W11-1 | Phase 1 |
| E7 | Leerlaufzeit der Passivierung | Aggregat 10 min, Adapter 2 min, Prozess-Manager 10 min; Umgebung | Phase 2 |

## 3 · Phasen im Detail

### Phase 0 · Abgleich und E2E-Messung (4–6 PT)

1. **Abgleich:** `git log` und docs/13 §13.0 prüfen — ist ein G- oder PF-Befund inzwischen berührt? Plan nachführen; Entscheidungen E1–E7
   gebündelt mit Tobi klären (ohne Antwort gelten die Vorschläge).
2. **Messaufbau nach §0.1:** Umgebung `umgebungen/umgebung.vermessung.json` (eigene DB, Quell-Verzeichnis, Funktionen Host bzw. Extern),
   ein Treiber für die Szenarien S1–S5, der nur echte Eingänge benutzt (Dateien erzeugen wie `PipelineFlussE2ETests`, Commands und Queries
   über die Leitung mit Akteur-Token), `pg_stat_statements` an, RSS/CPU des Host-Prozesses mitschreiben. Fehlt einem Szenario ein realer
   Baustein (heute: eine Reaktion in S5), bekommt die Prüfdomäne einen fachlich sinnvollen, an der richtigen Rolle (CQRS098).
3. **Baseline:** S1–S5 mit Sättigung, Latenz, Last und Speicher, ein Dauerlauf von 8 h und die Aufholzeit; Tabelle in §12.5.

*Abnahme:* `scripts/vermessung-e2e.sh` misst S1–S5 mit einem Befehl reproduzierbar; Baseline eingetragen; E5-Zielwerte festgelegt.

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

*Abnahme:* Tests grün (Konflikt und mehrdeutiger Commit heilen ohne Neustart); E2E-Messung S1–S5; Dauerlauf 24 h S1 + S3 mit Speicher ±10 %.

### Phase 3 · Batch-Konsum (15–25 PT)

1. **Entwurf (2–3 PT), festgehalten in docs/04:** ein Batch-Leser je (Projektion, Partition) liest den globalen Sequenzbereich seitenweise
   (z. B. 500), gruppiert je Stream, wendet je Stream in Versionsreihenfolge an und committet Effekte und Checkpoints **aller** Streams des
   Batches in einer Transaktion. Zu klären und festzuhalten: Checkpoint je Partition (globale Position) und je Stream (Guard), Rolle des Signals
   (weckt die Partition), Konflikt bei geteilten Dokumenten (Batch verwerfen, halbieren, einzeln wiederholen), Rebuild als Strom, Lease je
   Partition im Cluster mit Versionsprüfung des Checkpoints (**G-4**).
2. Infrastruktur: Batch-Leser; `MartenCoCommitStoreBase` für mehrere Streams (Bereich je Batch statt je Stream); der Batch-Leser ersetzt den
   Heil-Poll der Projektionen (**PF-2**); eine Rückstands-Abfrage für `/health`.
3. Generator: `PullPathGenerator` erzeugt den Batch-Dispatch. Die Handle-Signaturen im Fachcode bleiben unverändert (Inv. 5).
4. Emittenten laufen über denselben Leser mit Cursor je Partition (setzt Phase 1 voraus).
5. Rückbau: Adapter-Actor je (Projektion, Stream) und `SignalAdapterActor`-Pfad für Projektionen.
6. Tests: Absturz mitten im Batch (`CoCommitPostgresTests`), Ordnung je Stream innerhalb einer Partition, geteilte Dokumente, Rebuild, zwei
   Knoten mit Lease.
7. Doku: 03, 04, 11, 12, 13.

*Abnahme:* E2E-Messung S1–S5 erreicht die E5-Zielwerte (Richtwert: Sättigung S1/S5 ≥ 5 × Baseline, Latenz p99 < 1 s bei 50 %); Aufholzeit nach 1 h Stillstand gemessen.

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

*Abnahme:* Exactly-once über den Horizont nachgewiesen (Integration); E2E S3 mit N = 10 000+ zählt genau einmal; Sättigung S1 über dem Stand nach Phase 3.

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

- Ein Commit (oder wenige) je Phase; Prüfstand und die Integrations-Suiten **nacheinander** grün; `GraphExtractor -- --check`; E2E-Messung nach §0.1.
- Doku im selben Commit: erledigte IDs von §13.0 nach §13.6, Garantien in [11](11-garantien.md) nachführen, Messwerte in §12.5.
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
4. **Messung auf dem Entwickler-Rechner** schwankt unter Fremdlast (§12.5: Faktor 2 zwischen zwei Läufen); deshalb drei Wiederholungen mit
   Median, Fremdlast vermerken, und Abnahmen vergleichen Verhältnisse zur Baseline auf demselben Rechner.

## 7 · Nicht in diesem Plan

Zellen und Föderation über Anlagen, Store-Sharding, flüchtige Quellen und Telemetrie, OT-Treiber (OPC UA, MQTT), Transport-Sicherheit
(P1-2), Multi-Node-Betrieb (P1-1), Observability (P2-10). Sie bauen auf diesem Plan auf und bekommen eigene Einträge in §13.0, wenn sie
anstehen.
