# Dokumentation — CQRS/Event-Sourcing-Framework

Stand 2026-11-02. Die Kapitel beschreiben den **Ist-Zustand** des Codes; für Fakten gilt der Code. Grammatik, Regeln und Entscheidungen stehen in
[10](10-entwickler-api.md), Offenes in [13](13-reifegrad-schulden-bewertung.md) §13.0, Zahlen in [12](12-tests-und-vermessung.md).

## Lesepfad

1. [10](10-entwickler-api.md) — wie man Aggregat, Projektion, Reader, Funktion, Graph, Quelle, Akteur und Client schreibt; Regeln und Entscheidungen.
2. [03](03-schreibseite.md) und [04](04-konsum-und-prozess-maschine.md) — wie Schreibseite, Projektionen, Graphen, Quellen und Uhr laufen.
3. [11](11-garantien.md) — was garantiert ist und welcher Test es beweist.
4. [konzept-akteure.md](konzept-akteure.md) → [08](08-frontend-blazor-client.md) → [09](09-python-sdk.md) — Akteure, Clients, Blazor, Python.
5. [12](12-tests-und-vermessung.md) — wie getestet und gemessen wird (vor jedem Test lesen); [13](13-reifegrad-schulden-bewertung.md) — was offen ist.

## Dokumente

| Datei | Inhalt |
|---|---|
| [03-schreibseite.md](03-schreibseite.md) | Command → Decide → Event → Log: Aggregat-Actor, Bündel, Group-Commit, Inbox, Horizont, was ein Decide ausgibt |
| [04-konsum-und-prozess-maschine.md](04-konsum-und-prozess-maschine.md) | Batch-Leser der Projektionen, Store-Klassen, Funktionen und Graphen (Instanz, Lauf, Zustand, Arbeit), Abläufe, Emit, Uhr, Quellen, Spur |
| [05-generatoren-analyzer-proto.md](05-generatoren-analyzer-proto.md) | Generatoren, Analyzer, Boot-Guards, Codegen im Build |
| [06-transport-multinode-betrieb.md](06-transport-multinode-betrieb.md) | Wire-Serializer, Cluster, Multi-Node, Umgebung als Ordner, Einblick und JSON-Eingang |
| [07-projektstruktur.md](07-projektstruktur.md) | feste Projekt- und Ordnerstruktur (eine Quelle mit `Abstractions/Projektstruktur.cs`) |
| [08-frontend-blazor-client.md](08-frontend-blazor-client.md) | Blazor-Client: Transport, Bus, Store, Versionen, Client und seine Akteure |
| [09-python-sdk.md](09-python-sdk.md) | Python-SDK: Client-Basen, Arbeit, Kenntnis, Fragen |
| [10-entwickler-api.md](10-entwickler-api.md) | Grammatik, Regeln R1–R20, Entscheidungen E1–E16 und Annahmen A1–A8 |
| [11-garantien.md](11-garantien.md) | Garantien je Weg mit Nachweis-Tests |
| [12-tests-und-vermessung.md](12-tests-und-vermessung.md) | Test-Ebenen, Messverfahren, Messwerte (Tabellen schreibt `scripts/vermessung-tabelle.sh`) |
| [13-reifegrad-schulden-bewertung.md](13-reifegrad-schulden-bewertung.md) | offene Befunde (§13.0), Erledigtes; die eine Quelle für „offen“ |
| [plan-explizite-kanten.md](plan-explizite-kanten.md) | Plan des Umbaus „Ausdrückliche Kanten in Graphen“ (`[Von(nameof(…))]`, `[Kopf]`); nach der Abnahme im Git-Verlauf |
| [konzept-akteure.md](konzept-akteure.md) | Akteure (`IDarf`, `IHoert`), Clients, Arbeit, Sitzung, Werkstatt, Verteiler |
| [../vermessung/README.md](../vermessung/README.md) | Ablage der Messläufe |

Gelöschte Dokumente (`01-ueberblick.md`, `02-design-prinzipien.md`, `konzept-werkbank.md` — Leitsätze W1–W12 in Kurzform in `CLAUDE.md` —, die
Editor- und LLM-Konzepte, die früheren Pläne) liegen im Git-Verlauf.

## Konventionen

- **Eine Quelle je Thema:** Grammatik → 10 und `DomainEditor/Grammatik.cs`; Garantien → 11; Offenes → 13 §13.0; Zahlen → 12; Projektstruktur → 07
  und `Abstractions/Projektstruktur.cs`; Regeln im Build → der Code (Analyzer, Generator-Diagnosen, Boot-Guards).
- **Gegenwart:** Kapitel beschreiben den Ist-Zustand; Belege als Datei/Klasse.
- **Sprache:** Deutsch.
