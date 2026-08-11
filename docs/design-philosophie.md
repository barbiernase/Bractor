# Design-Philosophie

> Das **„Warum"** hinter der Architektur — konsolidierter Einstieg. Die drei
> Herleitungs-Dokumente (unten verlinkt) enthalten die vollständige, mehrstufige Herleitung
> mit adversarialen Reviews; dieses Dokument fasst ihre gemeinsame Kernidee zusammen. Das
> „Wie es heute funktioniert" steht in `docs/architektur/`.

## Der Ausgangspunkt

Das Backend ist organisch gewachsen und dabei an einer Stelle *auseinander*gewachsen: **ein
Primitiv wurde an vier Stellen unterschiedlich sicher nachgebaut.** Der Neubau (08.–10.08)
hat diese vier Stellen auf ein gemeinsames Primitiv zurückgeführt — ohne
Rückwärtskompatibilitäts-Zwang, Altlasten (Sentinel, tote Pfade) durften ersatzlos
verschwinden.

## Zwei Primitive, nicht vier Maschinen

Es gibt genau **zwei** Primitive:

- **P1 — Der Schreiber** (`AggregateActorBase`): der einzige Ort, an dem ein Effekt durabel
  wird. Command → Decider → OCC-Append → Apply → optionale Inbox-Marke → Signal. Zwei
  Idempotenz-Achsen: der **OCC-Pfad** (der Client behauptet eine Version, die Version *ist*
  die Absicherung) und der **Idempotenz-Pfad** (interne Emitter behaupten keine Version; die
  Empfänger-Inbox dedupliziert über die deterministische CommandId).
- **P2 — Der durable Konsument:** liest ab Cursor, faltet, produziert Ausgaben, schreibt
  seine Marke fort, emittiert Commands (idempotent) und Signale (best-effort). Jede Weckung —
  Signal *oder* Poll — ist ein Schritt.

Projektion, Reaktion, Pipeline-Event-Pfad und Prozess sind **alle P2**.

## „Eine Maschine, keine Taxonomie" — aber ehrlich

Die Leitidee ist: kein zweiter Marker, kein „Projektion-vs-Reaktion"-Zweig. Die
Unterschiede sollen aus Konstruktor-Stores und Rückgabetypen fallen, nicht aus einer
Typ-Hierarchie.

Die **ehrliche Präzisierung** (v2): „eine Maschine" heißt *nicht* „ein Kind mit einem
Rückgabetyp". Die Ausprägungen fallen aus **zwei orthogonalen Achsen**, die man nicht
wegparametrisieren kann:

- **Quell-Topologie** — ein einzelner Stream (Projektion/Reaktion) vs. ein
  korrelations-gefaltetes Marking über viele Streams (Prozess).
- **Effekt-Klasse (Achse B)** — replaybarer, co-committeter Read-Model-Effekt
  (`IProjectionTracker` + Reset) vs. emittierter, geld-bewegender Ausgang
  (`IEmittentenCursor`, kein Reset).

Wer „ein Kind" wörtlich baut, verzweigt intern doch wieder nach Rolle — deshalb der saubere
Achsen-Schnitt statt einer flachen Parametrisierung.

## Warum die Vereinheitlichung sich gelohnt hat

Der Audit fand vier reale, mit gültigem Anwendercode erreichbare Schwächen — alle vom Neubau
strukturell beseitigt:

| Symptom | Vorher | Nachher |
|---|---|---|
| **W1** — Doppelanwendung | Pipeline sendet mit OCC + zufälliger CommandId → keine Empfänger-Dedup | `CommandEmitter` mit deterministischer CommandId → Inbox-Dedup |
| **W2** — unbounded Hang | Pipeline sendet mit `CancellationToken.None` | bounded Token, erzwungen durch CQRS021 |
| **S15** — stiller Falsch-Erfolg | „aufgelöst" ≠ „wirksam" ging im Re-Fold verloren | Drei Fold-Achsen (`ErgebnisDa`/`WirkungDa`/`AbgelehntDa`) + `KommandoAbgelehnt`-Marke |
| **Terminal-Hänger** | Ergebnis-Event der letzten Transition triggert keine Regel | Selbst-Weckung + `ProzessPollFilter` (Korrelation ∈ offen) + Offen-Index-Backstop |

Das Emit-Primitiv ist auf **einen** Baustein reduziert und per Roslyn-Analyzer erzwungen —
„genau ein Emit-Weg" (EM-1) ist Compile-Zeit-Invariante, nicht Konvention.

## Das Denkmodell: System als Graph

Das Gesamtsystem wird als gerichteter Graph gedacht: Aggregate/Prozesse sind Knoten, Events
und Commands sind Kanten. Der Prozess ist ein Petri-Netz-Teilgraph (Events = Tokens, Commands
= Transitionen). Diese Sicht macht zwei Dinge prüfbar:

- **Azyklizität** — die Command→Event→Command-Kanten müssen ein DAG sein (Boot-Guard aus
  `GeneratedCommandRouting.Produziert`).
- **Marking als Faltung** — der Prozess-Zustand ist kein gehaltenes Feld, sondern eine
  Faltung des Graphen aus dem Log bei jeder Weckung.

## Die sechs Invarianten

Sie sind die verdichtete Form dieser Philosophie und stehen in
[architektur/00-ueberblick.md](architektur/00-ueberblick.md#die-sechs-invarianten). Kurz:
Log ist Wahrheit · Signal ist nur Weckruf · Routing über Typen · keine Runtime-Reflection ·
Fachcode bleibt rein · persistent nur bei durablem Konsumenten.

## Die vollständige Herleitung

- **`docs/zielbild-vereinheitlichte-konsumenten-maschine.md`** — das Zielbild v2 (zwei
  Primitive, zwei Achsen, Rollen-Schnitt). Weitgehend umgesetzt.
- **`docs/gedankenmodell-system-als-graph.md`** — das Graph-Denkmodell in voller Tiefe.
- **`docs/backend-neubau-einheitliche-maschine.md`** — Herleitung + Design-Philosophie +
  ursprünglicher Entwicklungsplan des Neubaus.
