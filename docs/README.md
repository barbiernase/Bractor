# Dokumentation — CQRS/Event-Sourcing-Framework

> **Kapitel 01–13: Stand 2026-08-12** (Wegweiser und Konzept-Liste aktualisiert 2026-10-06). Die Kapitel wurden vollständig neu aus dem Code hergeleitet
> (agentische Analyse aller Subsysteme, ohne Rückgriff auf die alte Doku). Die
> frühere Dokumentation liegt unverändert in [`_archiv-2026-08-12/`](_archiv-2026-08-12/).
> Diese Neufassung ist als **Bewertungsgrundlage** angelegt.

## Was ist das?

Ein selbstgebautes, signalbasiertes **CQRS-/Event-Sourcing-Framework** auf .NET 9,
Proto.Actor (virtuelle Cluster-Actors), Marten/PostgreSQL (Event-Store = einzige Wahrheit)
und Redis (nicht-autoritativer Versions-Index). Alles Dispatchende wird zur Compile-Zeit
über Roslyn-Source-Generatoren erzeugt — **ohne Runtime-Reflection**. Dazu kommen ein
generierter Blazor-Client, ein Python-SDK samt ML-Worker, sowie ein Wissensgraph-Extractor
mit Live-Simulations-Runtime.

## Reifegrad auf einen Blick

| Subsystem | Reife | Kurzbewertung |
|---|:---:|---|
| Schreibseite (Command→Event→Store) | 🟢 | kohärent, gemessen grün, produktionsnah |
| Konsum-/Prozess-Maschine | 🟢 | stark; Co-Commit implementiert & guard-gehärtet (`ICoCommitTracker`, 2026-08-12) |
| Generatoren & Analyzer | 🟢 | 15 Build-Guards, reflexionsfrei, konsistent |
| Multi-Node / Wire-Transport | 🟢 / 🟡 | über alle Planes verdrahtet & bewiesen; Betrieb noch container-only |
| Graph-Extractor + SimHost (Domänen-Editor) | 🟡 | in der `.sln`, Parität `--check` + Sonde `--sonde` grün; LLM-Füllung, Simulation „als Akteur" offen |
| Python-SDK + ML-Worker | 🟡 | Kernpfad + generierte Vertrags-/Client-Basen, Tests (SDK 19, Worker 11); Registry noch handgepflegt |
| Frontend (Blazor-Client) | 🟡 | **Build 2026-08-12 repariert** (stale Referenz entfernt); modulare Kette (`Domain.Client.Modules.Blazor`) baut, alte Legacy-Projekte noch auf Disk |
| Tests & Vermessung | 🟢 | 280 Prüfstand grün (2026-10-06), Integration gegen echte Infra, ehrliche Perf-Belege |

🟢 solide · 🟡 mit erkannten Schulden · 🔴 aktuell blockiert. Details: [13-reifegrad-schulden-bewertung.md](13-reifegrad-schulden-bewertung.md).

## Lesepfade

**Für Bewerter / Reviewer (60 Min.):**
[01](01-ueberblick.md) → [02](02-design-prinzipien.md) → [11](11-feature-inventar.md) →
[12](12-tests-und-vermessung.md) → [13](13-reifegrad-schulden-bewertung.md).

**Für Architektur-Verständnis:**
[01](01-ueberblick.md) → [03](03-schreibseite.md) → [04](04-konsum-und-prozess-maschine.md) →
[05](05-generatoren-analyzer-proto.md) → [06](06-transport-multinode-betrieb.md).

**Für Entwickler (neuer Baustein):**
[10-entwickler-api.md](10-entwickler-api.md) (praktisches „Wie schreibe ich X?"), rückverweisend
auf die jeweiligen Architektur-Kapitel.

**Für Betrieb / Ops:**
[06-transport-multinode-betrieb.md](06-transport-multinode-betrieb.md).

## Inhalt

| # | Datei | Inhalt |
|---|---|---|
| — | [README.md](README.md) | dieser Wegweiser |
| 01 | [01-ueberblick.md](01-ueberblick.md) | System, 27-Projekte-Landkarte, Gesamt-Datenfluss |
| 02 | [02-design-prinzipien.md](02-design-prinzipien.md) | aus dem Code abgeleitete Prinzipien (mit Belegen) |
| 03 | [03-schreibseite.md](03-schreibseite.md) | Command→Decider→Event→Store, Batching, Signal, Actor |
| 04 | [04-konsum-und-prozess-maschine.md](04-konsum-und-prozess-maschine.md) | vier Konsumenten, Pull-Schleife, Saga-DSL, Marking-Cursor |
| 05 | [05-generatoren-analyzer-proto.md](05-generatoren-analyzer-proto.md) | Generator-Tabelle, 15 CQRS-Codes, Proto-Flow |
| 06 | [06-transport-multinode-betrieb.md](06-transport-multinode-betrieb.md) | Wire-Serializer, Cluster, Cold-Start, Deploy, Config, Monitoring |
| 07 | [07-graph-und-simulation.md](07-graph-und-simulation.md) | GraphExtractor + SimHost + interaktives Board |
| 08 | [08-frontend-blazor-client.md](08-frontend-blazor-client.md) | Bus/Store-Stack, Modul-System, Build-Status |
| 09 | [09-python-sdk.md](09-python-sdk.md) | cqrs_client + ML-Worker |
| 10 | [10-entwickler-api.md](10-entwickler-api.md) | „Wie schreibe ich X?" für alle Bausteine |
| 11 | [11-feature-inventar.md](11-feature-inventar.md) | vollständige Feature-Aufstellung mit Status |
| 12 | [12-tests-und-vermessung.md](12-tests-und-vermessung.md) | Test-Ebenen, Zahlen, echte Messwerte, LoadHarness |
| 13 | [13-reifegrad-schulden-bewertung.md](13-reifegrad-schulden-bewertung.md) | Bewertungs-Dossier: Stärken, Risiken, Empfehlungen |

## Konzepte, Anleitungen, Analysen (neben den Kapiteln)

Status geprüft 2026-10-06. „umgesetzt" = steht im Code; „Konzept" = Entwurf, nicht gebaut.

| Datei | Thema | Status |
|---|---|---|
| [konzept-akteure.md](konzept-akteure.md) | **Akteure, Akteur-Verträge (Zusagen), Clients** — wer hineingibt, was er draußen zusagt, welche Software es tut | umgesetzt; durable Zustellung an Clients offen |
| [konzept-domaenen-editor.md](konzept-domaenen-editor.md) | **Domänen-Editor** — kanonische Referenz (Leitprinzipien, Palette, Schreib-/Leseseite, Prozess, Betrieb, Bedienung, SimHost, Akteure §12) | gebaut |
| [konzept-editor-panel-bearbeitung.md](konzept-editor-panel-bearbeitung.md) | Bearbeiten nur im Panel, Graph als Landkarte | umgesetzt |
| [konzept-handle-ausgaenge.md](konzept-handle-ausgaenge.md) | Handle-Ausgänge aus der Signatur (CQRS050) | umgesetzt |
| [konzept-llm-minimalkontext.md](konzept-llm-minimalkontext.md) | LLM-Füllung von Code-Blöcken, Kontext nur aus Code + Graph | gebaut (Kontexte + Konsole) |
| [konzept-editor-komposition.md](konzept-editor-komposition.md) | Editor als Kompositions-Sprache (Grammatik, Kapselung, Muster) | Phase 1 umgesetzt, Rest Konzept |
| [konzept-editor-pipelines.md](konzept-editor-pipelines.md) | Pipelines im Editor (Rand-Band, Kanäle mit Garantie, Frist als Ausgang) | Konzept |
| [konzept-pipeline-stroeme.md](konzept-pipeline-stroeme.md) | Pipeline-Schritte als Actors | Konzept |
| [analyse-editor-backend-abdeckung-2026-09-24.md](analyse-editor-backend-abdeckung-2026-09-24.md) | Abdeckung Backend ↔ Editor, Roadmap | Momentaufnahme 2026-09-24 |
| [konzept-exactly-once-naht.md](konzept-exactly-once-naht.md) | Verträge eines Co-Commit-Stores | Analyse, Hebel 1 umgesetzt |
| [konzept-client-haertung.md](konzept-client-haertung.md) | Client-Oberfläche: Zustellgarantie, Python-SDK, Transport-Sicherheit | Analyse, T2a/T2b/T3 umgesetzt |
| [konzept-streaming-architektur.md](konzept-streaming-architektur.md) | Streaming-Quellen (OPC UA, MQTT, Börsen-Feeds) | Konzept |
| [konzept-training-und-datensatz.md](konzept-training-und-datensatz.md) | Datensätze & Training (Modell-Lebenszyklus) | umgesetzt |
| [konzept-datensatz-kuratierung.md](konzept-datensatz-kuratierung.md) | Datensatz als Tag, Kuratieren beim Betrachten | umgesetzt |
| [konzept-galerie-datensatz-komposition.md](konzept-galerie-datensatz-komposition.md) | Galerie ↔ Einbild ↔ Datensatz-Komposition | Konzept |
| [entscheidungs-debugger-und-test-dsl.md](entscheidungs-debugger-und-test-dsl.md) | Test-DSL (Gegeben/Wenn/Dann) + Entscheidungs-Debugger | Anleitung |
| [ddd-muster-showcase.md](ddd-muster-showcase.md) | taktische DDD-Bausteine als getestete Referenz (`Domain/Verkauf/`) | Referenz (nicht in 01–13 eingearbeitet) |

**Archive (nur Historie, nicht als Ist lesen):** [`_archiv-2026-08-12/`](_archiv-2026-08-12/) (Doku vor der Neufassung),
[`_archiv-2026-10-06/`](_archiv-2026-10-06/) (erledigte Übergabe-Dokumente).

## Konventionen dieser Doku

- **Sprache:** Deutsch (Projektkonvention; Domäne und Kommentare sind durchgängig deutsch).
- **Belege:** Aussagen sind, wo möglich, mit `Datei:Zeile` unterlegt. Zeilennummern sind
  Momentaufnahmen (Stand 2026-08-12) und können driften.
- **Messwerte** sind als solche gekennzeichnet („gemessen 2026-08-12") und von
  Konzept-/Anspruchs-Aussagen getrennt.
