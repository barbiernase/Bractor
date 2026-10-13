# Dokumentation — CQRS/Event-Sourcing-Framework

Stand 2026-10-13, gegen den Code geprüft. Maßstab: [konzept-werkbank.md](konzept-werkbank.md).

Dieser Wegweiser sagt nur, **wo** etwas steht. Reifegrad und Offenes stehen in [13](13-reifegrad-schulden-bewertung.md)
(§13.0 Index, §13.1 Einschätzung), Zahlen und Messwerte in [12](12-tests-und-vermessung.md).

## Was ist das?

Ein selbstgebautes, signalbasiertes **CQRS-/Event-Sourcing-Framework** auf .NET 9, Proto.Actor (virtuelle Cluster-Actors),
Marten/PostgreSQL (Event-Store = einzige Wahrheit) und Redis (nicht-autoritativer Versions-Index). Alles Dispatchende ist generiert,
ohne Reflection. Darauf sitzen Clients mit Vertrag (Blazor, Python) und die **Werkbank**: eine Oberfläche, auf der eine Anwendung
entworfen, als Code erzeugt, gebaut, im echten Host durchgespielt und über die Spur nachvollzogen wird. Kurzfassung: [01](01-ueberblick.md).

## Lesepfade

**Einstieg (30 Min.):** [01](01-ueberblick.md) → [konzept-werkbank.md](konzept-werkbank.md) §0–§2 → [02](02-design-prinzipien.md).

**Bewertung:** [01](01-ueberblick.md) → [02](02-design-prinzipien.md) → [13](13-reifegrad-schulden-bewertung.md) →
[12](12-tests-und-vermessung.md).

**Architektur:** [01](01-ueberblick.md) → [03](03-schreibseite.md) → [04](04-konsum-und-prozess-maschine.md) →
[05](05-generatoren-analyzer-proto.md) → [06](06-transport-multinode-betrieb.md).

**Werkbank und Editor:** [konzept-werkbank.md](konzept-werkbank.md), Detail-Referenzen in der Konzept-Tabelle unten.

**Neuer Baustein:** [10](10-entwickler-api.md) („Wie schreibe ich X?“), rückverweisend auf die Architektur-Kapitel.

**Clients:** [konzept-akteure.md](konzept-akteure.md) (Akteure, Verträge, Clients) → [08](08-frontend-blazor-client.md) (Blazor) →
[09](09-python-sdk.md) (Python).

**Betrieb:** [06](06-transport-multinode-betrieb.md), Umgebungen in [konzept-werkbank.md](konzept-werkbank.md) §4.

## Kapitel

| # | Datei | Inhalt |
|---|---|---|
| 01 | [01-ueberblick.md](01-ueberblick.md) | das System in einem Absatz, die sechs Invarianten (eine Quelle), Projekt-Landkarte, Gesamt-Datenfluss |
| 02 | [02-design-prinzipien.md](02-design-prinzipien.md) | aus dem Code abgeleitete Prinzipien mit Belegen |
| 03 | [03-schreibseite.md](03-schreibseite.md) | Command → Decide → Event → Log: Aggregat-Actor, Batching, Signal, Inbox |
| 04 | [04-konsum-und-prozess-maschine.md](04-konsum-und-prozess-maschine.md) | vier Konsumenten, eine Pull-/Signal-Schleife; Prozess-Manager als Dirigent; Marking-Cursor |
| 05 | [05-generatoren-analyzer-proto.md](05-generatoren-analyzer-proto.md) | Generatoren, Analyzer, Boot-Guards, Codegen im Build (Proto, STJ-Kontexte, Python-Basen) |
| 06 | [06-transport-multinode-betrieb.md](06-transport-multinode-betrieb.md) | Wire-Serializer, Cluster, Multi-Node, Deploy, Monitoring |
| 07 | — | entfallen (2026-10-12): in [konzept-werkbank.md](konzept-werkbank.md) aufgegangen |
| 08 | [08-frontend-blazor-client.md](08-frontend-blazor-client.md) | Blazor-Client: Bus/Store-Stack, Modul-System, Client mit Vertrag |
| 09 | [09-python-sdk.md](09-python-sdk.md) | Python-SDK `cqrs_client` und Python-Worker |
| 10 | [10-entwickler-api.md](10-entwickler-api.md) | „Wie schreibe ich X?“ für alle Bausteine |
| 11 | — | entfallen (2026-10-13): Status und Schulden stehen in [13](13-reifegrad-schulden-bewertung.md) |
| 12 | [12-tests-und-vermessung.md](12-tests-und-vermessung.md) | Test-Ebenen, Zahlen, Messwerte, Fallstricke |
| 13 | [13-reifegrad-schulden-bewertung.md](13-reifegrad-schulden-bewertung.md) | Reifegrad, Stärken, offene Befunde mit IDs, Erledigtes |

## Konzepte und Referenzen

| Datei | Thema | Status |
|---|---|---|
| [konzept-werkbank.md](konzept-werkbank.md) | Werkbank und Editor: Code als einzige Wahrheit, Gesten als Patch, Umgebungen, Durchspielen im echten Host, Spur | **kanonisch** |
| [konzept-akteure.md](konzept-akteure.md) | Akteure, Akteur-Verträge (Zusagen, Kenntnisse), Clients | Detail-Referenz |
| [konzept-editor-pipelines.md](konzept-editor-pipelines.md) | Pipeline als Fluss (§12–§14) | Detail-Referenz Fluss, umgesetzt |
| [konzept-llm-minimalkontext.md](konzept-llm-minimalkontext.md) | LLM-Füllung von Rümpfen, Kontext nur aus Code und Graph | Detail-Referenz |
| [konzept-editor-komposition.md](konzept-editor-komposition.md) | Editor als Kompositions-Sprache (Grammatik, Kapselung, Muster) | Sprache, Detail-Referenz |

Die Beispieldomäne im Repo ist Prüfling, nicht Architektur (konzept-werkbank W11); sie hat keine eigenen Dokumente.
Frühere Konzepte, Pläne und Archive sind gelöscht; ihre Historie liegt im Git-Verlauf.

## Konventionen dieser Doku

- **Eine Quelle je Thema.** Andere Dokumente nennen ein Thema höchstens kurz und verweisen:

  | Thema | Quelle |
  |---|---|
  | Invarianten | [01](01-ueberblick.md) §1.2 |
  | Werkbank, Editor, Umgebungen, Spur | [konzept-werkbank.md](konzept-werkbank.md) |
  | Konsum-Maschine | [04](04-konsum-und-prozess-maschine.md) |
  | Offenes und Schulden | [13](13-reifegrad-schulden-bewertung.md) (§13.0) |
  | Zahlen und Messwerte | [12](12-tests-und-vermessung.md) |
  | Regeln (was womit verbunden werden darf) | der Code (Analyzer, Generator-Diagnosen, Boot-Guards) und `dotnet run --project GraphExtractor -- --grammatik` |
  | Umgebungs-Schema | `Abstractions/Umgebung/UmgebungKonfig.cs` |

- **Gegenwart:** Kapitel beschreiben den Ist-Zustand des Codes. Belege als Datei/Klasse, nicht als Zeilennummer.
- **Sprache:** Deutsch (Projektkonvention).
