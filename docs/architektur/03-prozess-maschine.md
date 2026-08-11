# 03 — Prozess-Maschine (Event-Regel-DAG)

> Ein Prozess ist ein Petri-Netz: Events = Tokens, Commands = Transitionen. Verwandt:
> [00 Überblick](00-ueberblick.md), Entwickler-Anleitung
> `docs/anleitung-prozess-schreiben.md`.

## Das Modell — typisierte Regeln

Ein Prozess ist eine `IProzessDefinition` (`Abstractions/Prozess/ProzessRegeln.cs`) mit genau
einer Property `ProzessRegeln Regeln`. Die Notation ist eine fluent DSL
(`Abstractions/Prozess/ProzessBuilder.cs`):

```csharp
Prozess<TAuslöser>.Definiere(p => {
    p.Auf<E>().Sende<Cmd>(e => new Cmd(...)).RückgängigDurch<Gegen>(e => new Gegen(...));
    p.Auf<E1>().Und<E2>().Sende<Cmd2>((a, b) => new Cmd2(...));
});
```

- **`Prozess<TAuslöser>`** bindet den Auslöser-Event-Typ (`TAuslöser : IEvent`) — der Start
  ist ein Event, kein Sonder-Command.
- **Semantik:** Eine `Regel` ist eine Transition mit `Bedingung` (Konjunktion von
  Event-Typen), `Sende` (Match → Command-Liste), optional `RückgängigDurch` (Gegenzug),
  optional `Sammel` (Count-Join). `ProduziertCommands` wird beim Bauen per Typinferenz
  erfasst (für den Azyklizitäts-Check, ohne Runtime-Invoke).
- **Arität:** `Auf<E1>`, `.Und<E2>`, `.Und<E3>` (bis 3 Bedingungen) plus die
  Count-Join-Varianten.
- **`.Sende<Cmd>` ist typisiert** — das explizite Command-Typ-Argument ist Pflicht (sonst
  Diagnostik CQRS003), weil daraus die Command→Event-Kante des Azyklizitäts-Checks fällt.

**Herkunft:** DSL/`Regel`/`ProzessRegeln` sind handgeschrieben in `Abstractions`.
**Generiert** wird nur die Registry: `ProzessRegelnGenerator` (Domain) liest alle
`IProzessDefinition` und emittiert `Domain.Prozess.GeneratedProzessRegeln.Alle`. Die
Infrastruktur (Manager, Router, Startup) ist generisch/handgeschrieben — es gibt genau
**einen** Prozess-Generator.

## Der generische ProzessManager

`Infrastructure/Prozess/ProzessManager.cs` ist ein einziger generischer Interpreter, der das
frühere Prozess-Aggregat + den Treiber verschmilzt. Kern-Invariante: **Struktur aus Code
(Regeln), Marking aus dem Log** — bei jeder Weckung frisch gefaltet, nie in einem Feld
gehalten.

- **Log speichert NUR Entscheidungen** (`ProzessManagerEvents.cs`, alle `IProzessIntern`):
  `ProzessGestartet(ProzessName, AuslöserStream, AuslöserVersion)`,
  `SchrittGescheitert(Vorgang, Grund)`, `ProzessBeendet(Erfolg, Grund, KlärungNötig)`.
  Stream-Key = Korrelations-Guid; Append mit OCC über `Version = log.Count`.
- **Marking-Fold** (`FaltMarkingAsync`): Fixpunkt-Iteration. Wurzel-Token = Auslöse-Event
  (aus den im Log gemerkten Koordinaten). Pro Regel × Match wird der deterministische
  `vorgang` gerechnet (`ProzessId.FürTransition`, Diskriminator =
  `{RegelIndex}:{InstanzIndex}:{ZielAggregateId}`). Der Ausgang wird per Kausalität vom
  Ziel-Stream gefaltet (`CausationId == vorgang`). **Drei Achsen:**
  - `ErgebnisDa` — irgendein Ziel-Event mit dieser Kausalität (auch Inbox-Marke) → steuert
    „nicht neu feuern" + Terminal-Erkennung.
  - `WirkungDa` — ein **Domänen**-Event (kein `IProzessIntern`) → nur eine Wirkung ist
    kompensierbar und bringt ein neues Token in den Fold (aktiviert Joins).
  - `AbgelehntDa` — eine durable `KommandoAbgelehnt`-Marke → wird zu `SchrittGescheitert`.
- **Eine Weckung = ein Schritt** (`WakeAsync`): (1) neu-Abgelehntes VOR dem Split zu
  `SchrittGescheitert` durabel machen (sonst stiller Falsch-Erfolg); (2) kein Fehler → erste
  `!ErgebnisDa`-Transition feuern, sonst `ProzessBeendet(true)`; (3) bei Fehler →
  Kompensation.

Actor-Mantel: `ProzessManagerActor` (virtueller Cluster-Actor, Identität = Korrelation,
`KindName = "prozess-manager"`).

## EM-1 — genau ein Emit-Weg

Der Manager feuert fire-and-forget über `CommandEmitter`, **keine Quittung mehr**:

- `EmittiereAnZiel` → `_emitter.EmitAsync(cmd, commandId: vorgang, korrelation, ct)`. Der
  deterministische `vorgang` IST die CommandId → Fold-Match unverändert.
- Envelope: `Modus = Emittiert` (keine Version), `CorrelationId = korrelation`, bounded Token
  (5 s), best-effort — Timeout wird nur geloggt, kein Retry (at-least-once, Re-Wake/Poll
  heilt).
- **`DetachedProzessSend.Wrap`**: der Send läuft detached, der Turn kehrt sofort zurück
  (struktureller Hang-Fix). Nach JEDEM Send (Erfolg/Ablehnung/Timeout) läuft `danach` =
  `WeckeSelbst` → Manager faltet neu.
- **Fehlschlag-Erkennung trägt allein der Fold** (Achse `AbgelehntDa`), nicht mehr eine
  out-of-turn-Quittung.

Die **`KommandoAbgelehnt`-Marke + die Zwei-Mengen-Inbox** liegen auf der Schreibseite
(siehe [01](01-schreibseite.md)) — sie und die Fold-Achse gehören zusammen.

**KlärungNötig:** Wird ein Gegenzug SELBST abgelehnt, gilt er als *unvollziehbar* (nicht
„erledigt"). Der Manager feuert ihn nicht endlos neu (kein Kompensations-Livelock), sondern
hält terminal `ProzessBeendet(false, …, KlärungNötig: true)` + best-effort
`IDeadLetterSink`-Eintrag. **Dieser Pfad ist korrekt-per-Konstruktion, aber nicht
integration-gedeckt.**

## Korrelation + Routing

- **Korrelation reist** als `CommandEnvelope.CorrelationId` → Ziel-Event-Metadatum (der Actor
  stempelt es). Ziel-Aggregate bleiben rein (kein Korrelations-Feld).
- **`KorrelationsRouter`**: aus Signal `(StreamId, Version)` das Event lesen. Registrierter
  **Auslöser**-Typ → Start mit deterministischer Korrelation
  `ProzessId.Für(prozessName, stream, version)`. Sonst → Korrelation aus `env.CorrelationId`
  parsen und wecken.
- **`ProzessPollFilter.SollRouten`**: route wenn `teilnehmend.Contains(typ)` ODER
  (`CorrelationId` ∈ offene Prozesse). Löst den Terminal-Bug: das Ergebnis-Event der
  **letzten** Transition ist Auslöser keiner Regel → wäre durch den Typ-Filter gefallen.
- **Doppelnetz (bewusst, Auflage A2):**
  1. **`WeckeSelbst`** nach jedem Send — der schnelle Terminal-/Fortschritts-Pfad.
  2. **`ProzessOffenIndex`-Backstop** (15 s) — scannt alle offenen Korrelationen und weckt
     sie direkt; fängt den fully-stalled Prozess OHNE Stream-Änderung. Plus ein Poll-Backstop
     (30 s) über `ReadChangedStreamsAsync`. Der Index ist best-effort-Hinweis, keine Wahrheit.

## Azyklizität (Boot-Guard)

`ProzessManagerStartupService.StartAsync` ruft
`Abstractions.ProzessAzyklizität.PrüfeAlle(registry, GeneratedCommandRouting.Produziert)` —
**aktiv, fail-fast am Start**. Kanten: `Bedingungs-Event → Command` (aus
`Regel.ProduziertCommands`) und `Command → produziertes Event` (aus der **präzisen**
`GeneratedCommandRouting.Produziert`, Decide-OneOf-Rückgaben). Zyklus →
`InvalidOperationException` mit Pfad. Zusätzlich: Auslöser-Kollision (zwei Prozesse mit
gleichem Auslöser-Typ) bricht den Boot.

## Fan-out

- **`SendeJe<Cmd>`** — ein Match → N Commands (je Ziel eins), jeder mit eigenem
  deterministischem Vorgang (Diskriminator = Ziel-AggregateId), ohne Zähler.
- **`UndAlle<TSammel>(t => t.Ziele.Count)`** — Count-Join (`SammelBedingung`): feuert erst,
  wenn ALLE N Instanzen da sind. Breite steht im Auslöser, kein Log-Zähler.

Beispiel: `Domain/Sammelueberweisung/SammelueberweisungsProzess.cs`.

## Verkettung (Ende A startet B)

Kein Framework-Eingriff — Verkettung fällt aus der bestehenden Auslöser-Erkennung:

- Prozess A (`GenehmigungsProzess`) auf `AntragGestellt` → `Genehmige` → terminales
  **persistiertes Domänen-Event** `VorgangGenehmigt`.
- Prozess B (`AktivierungsProzess`) hat `VorgangGenehmigt` als **Auslöser** → `Aktiviere`.
- Weil `VorgangGenehmigt` ein echtes Domänen-Event ist (mit Signal, NICHT `IProzessIntern`
  wie das inerte `ProzessBeendet`), startet der `KorrelationsRouter` daraus B als eigene,
  frisch korrelierte Instanz.

**Kontrast:** Ein internes Manager-Log-Event (`ProzessBeendet`) kann NICHT verketten — sein
Signal ist inert. Verkettung braucht ein persistiertes Domänen-Event als Anker.

## Offene Punkte

- **KlärungNötig-Pfad** ohne Testdeckung.
- **P5b Marking-Cursor:** `FaltMarkingAsync` liest jeden Ziel-Stream ab 0 → O(N²) je
  Weckung; bewusst zurückgestellte Optimierung (`docs/prozess-marking-cursor-konzept.md`,
  `docs/p5b-marking-cursor-handoff.md`).
- **Deadline↔Prozess:** kein DSL-Verb `NachFrist`/`MitFrist`; das Frist-Primitiv ist noch
  nicht in das Marking eingebunden (siehe [04](04-feature-strom.md)).

## Schlüsseldateien

`Infrastructure/Prozess/ProzessManager.cs`, `ProzessManagerActor.cs`,
`ProzessManagerWiring.cs`, `ProzessManagerEvents.cs`, `DetachedProzessSend.cs`,
`KorrelationsRouter.cs`, `ProzessPollFilter.cs`,
`Persistence/MartenProzessOffenIndex.cs`; `Infrastructure/PubSub/CommandEmitter.cs`;
`Abstractions/Prozess/ProzessBuilder.cs`, `ProzessRegeln.cs`, `ProzessAzyklizitaet.cs`,
`ProzessId.cs`, `IProzessOffenIndex.cs`; `Domain.SourceGeneration/ProzessRegelnGenerator.cs`.
