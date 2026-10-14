# Plan: Garantien und Durchsatz

> **Plan**, Stand 2026-10-15. Wird nach der Umsetzung gelöscht; die Historie liegt dann im Git-Verlauf. Wo er etwas Offenes nennt, ist die
> Quelle [13](13-reifegrad-schulden-bewertung.md) §13.0 (Reihen **G** und **PF**); Garantien: [11](11-garantien.md); Messwerte:
> [12](12-tests-und-vermessung.md) §12.5.
>
> **Startbedingung:** Umgesetzt wird erst, wenn der laufende Agent im Werkbank-Strang (§13.5) seinen Schritt abgeschlossen und committet hat.
> Beide Stränge arbeiten nicht gleichzeitig im selben Arbeitsbaum (§5).

## 0 · Ziel und Maßstab

1. **Korrektheit:** Jede Garantie aus [11](11-garantien.md) gilt ohne Lücke — G-1 bis G-5 geschlossen.
2. **Durchsatz:** Die Decke der Konsumseite steigt um eine Größenordnung (PF-1); die Grundlast im Leerlauf geht gegen null (PF-2, PF-4);
   der Speicher bleibt im Dauerbetrieb flach (PF-5).
3. **Nachweis:** Jede Phase endet mit einer Messung gegen den echten Host (Phase 0), nicht nur mit grünen Tests.

Ausgangslage aus der Messung (§12.5): eine Weckung je (Projektion, Stream) schafft etwa 8 200/s, ein Batch etwa 175 000 Effekte/s (Faktor
~20); die Inbox-Marke kostet 35–40 % im Group-Commit; ein Stream ab 0 statt ab Cursor kostet bei 100 000 Events Faktor > 2 000. Hochgerechnet
trägt die Konsumseite heute etwa 8 200 / K Events/s (K = replaybare Projektionen) — vor Marten und Netzwerk.

## 1 · Übersicht

| Phase | Inhalt | IDs | Aufwand | setzt voraus | Abnahme (Kurz) |
|---|---|---|---|---|---|
| 0 | Abgleich und Lastprüfstand | – | 3–5 PT | Werkbank-Agent fertig | Baseline gegen den echten Host in §12.5 |
| 1 | Emit-Ausgang und Emittenten-Cursor | G-1, G-2 (Emittent), P1-6 | 5–7 PT | 0 | Poll-Kosten einer Reaktion unabhängig von der Stream-Länge |
| 2 | Actor-Lebenszyklus | G-3, PF-5 | 3–5 PT | 0 | Soak 24 h: Speicher flach; Konflikt und mehrdeutiger Commit heilen |
| 3 | Batch-Konsum | PF-1, PF-2, G-4 | 15–25 PT | 1, 2 | Konsum-Decke ≥ 10 × Baseline |
| 4 | Inbox als Tabelle | G-2 (Inbox), PF-3 | 6–9 PT | 2, E1 | Dedup über den Horizont; Append +30 % |
| 5 | Prozess-Poll und Backstop | PF-4 (W11-2-Anteil) | 3–5 PT | 1 | Leerlauf-Grundlast < 10 Queries/s bei 10 000 offenen Vorgängen |
| 6 | Rest | PF-6, G-5, PF-7 (Entscheidung) | 2–3 PT | – | – |
| | **Summe** | | **37–59 PT** | | etwa 8–12 Wochen für eine Person |

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
| E5 | Abnahme-Zielwerte | wie §1; Projektions-Rückstand p99 < 1 s bei 50 % der Decke | Phase 0 |
| E6 | Handle-/Trigger-Welt (W11-1) vor Phase 1 zurückbauen? | ja, wenn der Werkbank-Strang es ohnehin plant — spart `PipelineActorBase`/`PipelineActorGenerator` in 1.1/1.4 | Phase 1 |
| E7 | Leerlaufzeit der Passivierung | Aggregat 10 min, Adapter 2 min, Prozess-Manager 10 min; Umgebung | Phase 2 |

## 3 · Phasen im Detail

### Phase 0 · Abgleich und Lastprüfstand (3–5 PT)

1. **Abgleich:** nach Abschluss des Werkbank-Agenten `git log` und docs/13 §13.0 prüfen — ist ein G- oder PF-Befund inzwischen berührt? Plan
   nachführen; Entscheidungen E1–E7 mit Tobi klären.
2. **Lastprüfstand:** die Prüfdomäne bekommt eine Leseseite (`Pruefdomaene.Projections`, Rolle laut `Abstractions/Projektstruktur.cs`) mit
   fünf replaybaren Projektionen, einer Reaktion und einem Prozess auf einem kurzlebigen und einem langlebigen Aggregat. Ein Lastgenerator
   sendet Commands mit fester Rate über die Leitung (gRPC, Akteur-Token) gegen den echten Host (Umgebung `vermessung`, eigene DB).
3. **Gemessen:** Commands/s, Command-Latenz p50/p99, Projektions-Rückstand (Zeit bis Read-Model-Version ≥ v), Queries und Transaktionen/s
   (`pg_stat_statements`), Speicher des Hosts. Dazu ein **Soak** (24 h, feste Rate) und die **Aufholzeit** nach 1 h Stillstand.
4. **Ergebnis:** Baseline in §12.5; der Lauf ist ein Befehl (Skript neben `scripts/vermessung-postgres.sh`).

*Abnahme:* Baseline eingetragen, reproduzierbar.

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

*Abnahme:* Tests grün; im Lastprüfstand sind die Poll-Kosten einer Reaktion auf dem langlebigen Aggregat konstant statt linear.

### Phase 2 · Actor-Lebenszyklus (3–5 PT)

1. **G-3:** ein Append-Fehler setzt `_initialized` zurück und rehydriert frisch. Liegt die CommandId danach im Log (Event mit `causationId`
   oder Marke), wird Erfolg quittiert — der mehrdeutige Commit wird kein falsches Negativ mehr (P10).
2. **PF-5:** Passivierung für Aggregat-Actor, Adapter und Prozess-Manager über einen Leerlauf-Timeout (E7); der Snapshot beim Stoppen bleibt.
3. Tests: Integration — Versionskonflikt durch einen zweiten Schreiber, mehrdeutiger Commit über einen Fehler-Seam im Appender; Prüfstand —
   Passivierung und Reaktivierung ergeben denselben Stand.

*Abnahme:* Soak 24 h mit Speicher ±10 %; Konflikt und mehrdeutiger Commit heilen ohne Neustart.

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
   Knoten mit Lease, Lastprüfstand.
7. Doku: 03, 04, 11, 12, 13.

*Abnahme:* Konsum-Decke im Lastprüfstand ≥ 10 × Baseline; Projektions-Rückstand p99 < 1 s bei 50 % der Decke.

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

*Abnahme:* Exactly-once über den Horizont nachgewiesen; Append-Durchsatz im Lastprüfstand +30 % gegenüber Phase 3.

### Phase 5 · Prozess-Poll und Backstop (3–5 PT)

1. `ReadChangedStreamsAsync` liefert die geladenen Events mit; der Prozess-Poll routet sie direkt statt jeden Stream ab 0 zu lesen.
2. Fälligkeits-Index für Zeitlimits (zusammen mit dem Fristplaner, W11-2); der Backstop weckt nur noch Fällige, die Voll-Heilung läuft selten;
   Lease im Cluster.

*Abnahme:* Leerlauf-Grundlast < 10 Queries/s bei 10 000 offenen Vorgängen; Zeitlimit-Genauigkeit < 1 s.

### Phase 6 · Rest (2–3 PT)

- **PF-6:** Read-your-writes-Index je Projektion abschaltbar.
- **G-5:** Rehydration zählt je gespeicherter Position; Test mit Split-Upcast.
- **PF-7:** Entscheidung zu hochfrequenten Quellen festhalten (Stream je Meldung gegen Dedup-Tabelle und Sammel-Streams).

## 4 · Arbeitsweise je Phase

- Ein Commit (oder wenige) je Phase; Prüfstand und beide Integrations-Suiten **nacheinander** grün; `GraphExtractor -- --check`; Lastprüfstand.
- Doku im selben Commit: erledigte IDs von §13.0 nach §13.6, Garantien in [11](11-garantien.md) nachführen, Messwerte in §12.5.
- Kein `InMemoryEventStore`: Store-Semantik nur gegen echtes Marten.

## 5 · Abstimmung mit dem Werkbank-Strang (§13.5)

| Berührung | Werkbank-Strang | dieser Plan | Regel |
|---|---|---|---|
| Handle-/Trigger-Welt (`PipelineActorGenerator`, `PipelineActorBase`, `DetachedEmit`) | W11-1 Rückbau (§13.5 Schritt 7) | Phase 1 | Rückbau zuerst (E6), sonst passt Phase 1 sie an |
| Fristplaner, Zeitlimits | W11-2 (§13.5 Schritt 7) | Phase 1 (G-1), Phase 5 | Phase 5 und W11-2 als ein Schritt |
| `PullPathGenerator` | – | Phase 1, Phase 3 | nur dieser Plan |
| docs 04, 11, 12, 13 | nachgeführt | nachgeführt | nie gleichzeitig im selben Arbeitsbaum |

Die Werkbank-Schritte LLM-1, W3-1, W10-1 und WZ-1 berühren `Werkbank/`, `GraphExtractor*` und `Cqrs.Codegen` — fachlich getrennt von diesem
Plan. Parallel nur in getrennten Worktrees und mit Abgleich vor jedem Commit.

## 6 · Risiken

1. **Batch-Konsum und geteilte Dokumente:** ein Konflikt verwirft einen ganzen Batch; bei hoher Konkurrenz auf einem Dokument sinkt der
   Gewinn. Gegenmittel im Entwurf (Phase 3.1).
2. **Marten:** Insert per SQL in derselben Session wie der Append (Phase 4) und Co-Commit über viele Streams (Phase 3) sind vorgesehen, hier
   aber noch nicht genutzt — der erste Integrationstest der Phase klärt es.
3. **Abgewartete Emits** verlängern den Turn eines Emittenten; paralleles Senden je Tail dämpft es (Phase 1).
4. **Messung auf dem Entwickler-Rechner** schwankt unter Fremdlast (§12.5: Faktor 2 zwischen zwei Läufen); Abnahmen vergleichen deshalb
   Verhältnisse zur Baseline desselben Laufs.

## 7 · Nicht in diesem Plan

Zellen und Föderation über Anlagen, Store-Sharding, flüchtige Quellen und Telemetrie, OT-Treiber (OPC UA, MQTT), Transport-Sicherheit
(P1-2), Multi-Node-Betrieb (P1-1), Observability (P2-10). Sie bauen auf diesem Plan auf und bekommen eigene Einträge in §13.0, wenn sie
anstehen.
