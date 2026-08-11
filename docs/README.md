# Doku-Wegweiser

Diese `docs/` beschreibt ein selbstgebautes CQRS-/Event-Sourcing-Framework auf Proto.Actor +
Marten/PostgreSQL + Redis. Der Einstieg hängt davon ab, was du willst.

## Schnell-Einstieg nach Ziel

| Ich will … | Lies |
|---|---|
| **verstehen, wie das Backend heute funktioniert** | [`architektur/00-ueberblick.md`](architektur/00-ueberblick.md) → dann das Subsystem |
| **den aktuellen Stand + offene Baustellen** | [`backend-analyse-2026-08-11.md`](backend-analyse-2026-08-11.md), [`backend-neubau-fahrplan.md`](backend-neubau-fahrplan.md) |
| **wissen, WARUM es so gebaut ist** | [`design-philosophie.md`](design-philosophie.md) |
| **einen Prozess/eine Saga schreiben** | [`anleitung-prozess-schreiben.md`](anleitung-prozess-schreiben.md) → [`architektur/03-prozess-maschine.md`](architektur/03-prozess-maschine.md) |
| **testen oder Last fahren** | [`testen-und-lasttest.md`](testen-und-lasttest.md), [`teststrategie-ebenen.md`](teststrategie-ebenen.md) |

## Die Architektur-Referenz (lebend)

`architektur/` ist die maßgebliche Beschreibung des Ist-Zustands:

- [`00-ueberblick.md`](architektur/00-ueberblick.md) — Invarianten, „vier Konsumenten, eine
  Maschine", Achsen, Projektlandkarte.
- [`01-schreibseite.md`](architektur/01-schreibseite.md) — Command → Append, `CommandModus`,
  Batching, Inbox, Snapshots, Serialisierung.
- [`02-konsum-maschine.md`](architektur/02-konsum-maschine.md) — Pull-Adapter, Signal/Poll,
  Co-Commit, Emittenten-Cursor, Rebuilder, GA-1.
- [`03-prozess-maschine.md`](architektur/03-prozess-maschine.md) — Event-Regel-DAG,
  `ProzessManager`, EM-1, Korrelation, Azyklizität, Fan-out, Verkettung.
- [`04-feature-strom.md`](architektur/04-feature-strom.md) — Pipeline, Trigger, Deadlines,
  Monitoring, Dead-Letter.
- [`05-generatoren-und-analyzer.md`](architektur/05-generatoren-und-analyzer.md) — alle
  Generatoren, Diagnostik-Codes CQRS001–021.

## Herleitung & Konzept (Referenz, gültig)

- [`design-philosophie.md`](design-philosophie.md) — konsolidierter Einstieg ins „Warum".
- `zielbild-vereinheitlichte-konsumenten-maschine.md`, `gedankenmodell-system-als-graph.md`,
  `backend-neubau-einheitliche-maschine.md` — die vollständige, mehrstufige Herleitung.
- `prozess-neubau-event-regeln-dag.md` — Spezifikation des aktuellen Prozessmodells.
- `snapshot-konzept.md` — Snapshot-Design (umgesetzt).
- `spezifikation.md` — die Ursprungs-Spezifikation. ⚠ **Teilweise überholt:** Kap. 1–9
  (Naht/Signal/Reaktion) gültig, Kap. 10–15 (alte Prozess-Schrittlisten) durch den
  Event-Regel-DAG ersetzt → siehe `architektur/03-prozess-maschine.md`.

## Offene Arbeitspakete (vorwärtsgerichtet)

- `naechster-agent-prompt-schreibpfad-perf.md` — Schreibpfad-Perf (`wait_event` des
  parallelen Drains auflösen).
- `p5b-marking-cursor-handoff.md` + `prozess-marking-cursor-konzept.md` — P5b Marking-Cursor
  (O(N²) → O(N), bewusst zurückgestellt).

## Test & Betrieb

- [`testen-und-lasttest.md`](testen-und-lasttest.md) — drei Test-Ebenen + reale Fallstricke.
- [`teststrategie-ebenen.md`](teststrategie-ebenen.md) — die Ebenen-Strategie („mocke nicht,
  was du nicht besitzt").

## Archiv

`archiv/` enthält erledigte Handoffs, überholte Pläne und die alte Prozess-Schrittlisten-Welt
(Increments 1–5, vom Event-Regel-DAG gelöscht). Tote Historie, nur zur Nachverfolgung.

---

### Konventionen

- Kommentare/Domäne auf Deutsch (Bestand konsistent halten).
- Neue Verträge → `Abstractions`; Marten/Infra → `Infrastructure`.
- Nichts mit Runtime-Reflection (Invariante 4). Neue Dispatch-Logik = Generator erweitern.
- Neuer Command/Event/Query/Trigger braucht einen Proto-DTO:
  `dotnet run --project Proto.SourceGeneration` → `ProtoRepo` neu bauen → Infrastructure baut.
  (Signale sind bewusst ausgenommen.)
