# Dokumentation — CQRS/Event-Sourcing-Framework

> **Stand: 2026-08-12.** Diese Doku wurde vollständig neu aus dem Code hergeleitet
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
| Konsum-/Prozess-Maschine | 🟢 / 🟡 | stark; **eine** Lücke: kein echter Co-Commit (s.u.) |
| Generatoren & Analyzer | 🟢 | 15 Build-Guards, reflexionsfrei, konsistent |
| Multi-Node / Wire-Transport | 🟢 / 🟡 | über alle Planes verdrahtet & bewiesen; Betrieb noch container-only |
| Graph-Extractor + SimHost | 🟡 | konzeptionell reif, aber ungetrackt & nicht in der `.sln` |
| Python-SDK + ML-Worker | 🟡 | Kernpfad vollständig; Query-Antwort/Registry-Gen unfertig, keine Tests |
| Frontend (Blazor-Client) | 🟡 | **Build 2026-08-12 repariert** (stale Referenz entfernt); modulare Kette (`Domain.Client.Modules.Blazor`) baut, alte Legacy-Projekte noch auf Disk |
| Tests & Vermessung | 🟢 | 126 Prüfstand grün, 41 Integration, ehrliche Perf-Belege |

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

> **Ergänzend:** [`ddd-muster-showcase.md`](ddd-muster-showcase.md) — lauffähige, getestete
> Referenz der taktischen DDD-Bausteine (Value Object, Entity, Aggregate, Domain Event, Domain
> Service, Specification, Factory, Repository, Saga; u.a. `Domain/Verkauf/`). Dieser Showcase kam
> **nach** der Voll-Analyse hinzu und ist in den Kapiteln 01–13 noch nicht eingearbeitet.

## Konventionen dieser Doku

- **Sprache:** Deutsch (Projektkonvention; Domäne und Kommentare sind durchgängig deutsch).
- **Belege:** Aussagen sind, wo möglich, mit `Datei:Zeile` unterlegt. Zeilennummern sind
  Momentaufnahmen (Stand 2026-08-12) und können driften.
- **Messwerte** sind als solche gekennzeichnet („gemessen 2026-08-12") und von
  Konzept-/Anspruchs-Aussagen getrennt.
