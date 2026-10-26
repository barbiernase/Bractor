# Dokumentation — CQRS/Event-Sourcing-Framework

Stand 2026-10-26, Zweig `pubsub_neustrukturierung`. Für die Dauer des Umbaus ist
[plan-pubsub-neustrukturierung.md](plan-pubsub-neustrukturierung.md) der **Maßstab** (Zielbild, Grammatik, Garantien, Schritte); die Kapitel
unten beschreiben den **Ist-Zustand** des Codes, den der Umbau ablöst. Wo beides widerspricht, gilt der Plan als Ziel und der Code als Ist.

## Lesepfad für den Umbau

1. [plan-pubsub-neustrukturierung.md](plan-pubsub-neustrukturierung.md) — was, warum, in welcher Reihenfolge.
2. [11](11-garantien.md) — was heute garantiert ist und welcher Test es beweist (muss grün bleiben).
3. [12](12-tests-und-vermessung.md) — wie getestet und gemessen wird (vor jedem Test lesen).
4. Ist-Zustand der betroffenen Teile: [03](03-schreibseite.md) (bleibt weitgehend), [04](04-konsum-und-prozess-maschine.md) (Konsum- und
   Prozess-Maschine, wird umgebaut), [05](05-generatoren-analyzer-proto.md), [06](06-transport-multinode-betrieb.md),
   [konzept-akteure.md](konzept-akteure.md) → [08](08-frontend-blazor-client.md) → [09](09-python-sdk.md).
5. [13](13-reifegrad-schulden-bewertung.md) — offene Lücken (besonders G-11).

## Dokumente

| Datei | Inhalt | Rolle im Umbau |
|---|---|---|
| [plan-pubsub-neustrukturierung.md](plan-pubsub-neustrukturierung.md) | Zielbild, Grammatik, Garantien, Phasen | **Maßstab**, wird nach dem Umbau eingearbeitet und gelöscht |
| [03-schreibseite.md](03-schreibseite.md) | Command → Decide → Event → Log: Aggregat-Actor, Bündel, Group-Commit, Inbox, Horizont | Ist; bleibt im Kern |
| [04-konsum-und-prozess-maschine.md](04-konsum-und-prozess-maschine.md) | Batch-Leser, Emittenten, Prozess-Manager, Fluss, Funktionen, Spur | Ist; Batch-Leser bleibt, der Rest wird abgelöst |
| [05-generatoren-analyzer-proto.md](05-generatoren-analyzer-proto.md) | Generatoren, Analyzer, Boot-Guards, Codegen im Build | Ist; Generatoren ändern sich |
| [06-transport-multinode-betrieb.md](06-transport-multinode-betrieb.md) | Wire-Serializer, Cluster, Multi-Node, Umgebung, Monitoring | Ist; Grundlage für Verteiler je Knoten |
| [07-projektstruktur.md](07-projektstruktur.md) | feste Projekt- und Ordnerstruktur (eine Quelle mit `Abstractions/Projektstruktur.cs`) | gilt; neue Primitive ⇒ Tabelle + `Lageart.cs` |
| [08-frontend-blazor-client.md](08-frontend-blazor-client.md) | Blazor-Client: Transport, Bus, Store, Versionen | Ist; Clients werden umgebaut |
| [09-python-sdk.md](09-python-sdk.md) | Python-SDK und Worker | Ist; Verträge werden umgebaut |
| [11-garantien.md](11-garantien.md) | Garantien je Weg mit Nachweis-Tests | gilt; muss nach jeder Phase grün sein |
| [12-tests-und-vermessung.md](12-tests-und-vermessung.md) | Test-Ebenen, Messverfahren, Messwerte (Tabellen schreibt `scripts/vermessung-tabelle.sh`) | gilt |
| [13-reifegrad-schulden-bewertung.md](13-reifegrad-schulden-bewertung.md) | offene Befunde (§13.0), Erledigtes | gilt; die eine Quelle für „offen“ |
| [konzept-akteure.md](konzept-akteure.md) | Akteure, Akteur-Verträge, Clients | Ist; Verträge werden zu `ISendet`/`IFragt`/`IHoert`/`ITraegt` |
| [../vermessung/README.md](../vermessung/README.md) | Ablage der Messläufe | gilt |

**Gelöscht** (veraltet oder für den Umbau nicht nötig; Stand im Git-Verlauf bei Commit `861ea7f`): `01-ueberblick.md`,
`02-design-prinzipien.md`, `10-entwickler-api.md` (entsteht nach dem Umbau neu), `konzept-werkbank.md` (Leitsätze W1–W12 in Kurzform in
`CLAUDE.md`; Editor zurückgestellt), `konzept-editor-komposition.md`, `konzept-editor-pipelines.md`, `konzept-llm-minimalkontext.md`,
`plan-garantien-und-durchsatz.md`. Verweise in Kapiteln und Code-Kommentaren auf diese Dateien zeigen dorthin; sie werden in Phase 11 des Plans
nachgezogen.

## Konventionen

- **Eine Quelle je Thema:** Ziel und Grammatik → Plan; Garantien → 11; Offenes → 13 §13.0; Zahlen → 12; Projektstruktur → 07 und
  `Abstractions/Projektstruktur.cs`; Regeln im Build → der Code (Analyzer, Generator-Diagnosen, Boot-Guards).
- **Gegenwart:** Kapitel beschreiben den Ist-Zustand; Belege als Datei/Klasse.
- **Sprache:** Deutsch.
