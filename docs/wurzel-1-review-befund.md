# Wurzel-1-Outbox-Skizze — adversarialer Code-Review

> Haltung: „das Argument ist falsch, bis der Code das Gegenteil zeigt." Jeder Fund mit
> Datei:Zeile. Prüfziel: `docs/wurzel-1-outbox-skizze.md` gegen den echten Code.
> Urteil unten in §Fazit.

---

## Kurzurteil

**NICHT umsetzungsreif in der vorliegenden Form.** Das Ziel (durable Absicht statt
fire-and-forget) ist richtig und die *Idempotenz-Kette* trägt am Code (positiver Befund
unten). Aber der **zentrale Co-Commit-Punkt, auf dem die Skizze ruht, existiert für den
Prozess-Manager NICHT und ist für die Reaktion nicht dort, wo die Skizze ihn verortet.**
Zwei der sieben Argumente (Co-Commit-Machbarkeit, Vorrücken-auf-Durabilität) haben harte
Löcher; ein drittes (Idempotenz-Kette) hält nur für Prozess/Reaktion, nicht für die
Pipeline. Erst nachbessern bei **L1, L2, L4** (kritisch/hoch).

---

## L1 — KRITISCH: der Prozess-Manager hat keinen Co-Commit-Punkt für das Feuern

Skizze §4.2: „Wenn er eine Transition feuert, co-committet er den Outbox-Eintrag MIT der
Entscheidungs-Aufzeichnung (ProzessGestartet/**SchrittErledigt**) in derselben Marten-Tx."

Gegen den Code falsch, in drei Stufen:

1. **`SchrittErledigt` existiert nicht.** `grep` über die gesamte Codebasis: 0 Treffer.
   Das Manager-Log kennt nur `ProzessGestartet`, `SchrittGescheitert`, `ProzessBeendet`
   (`Infrastructure/Prozess/ProzessManagerEvents.cs:19,23,26`; Fold-Switch
   `ProzessManager.cs:263-277`).

2. **Das Feuern schreibt bewusst NICHTS durabel.** `ProzessManager.WakeAsync` feuert die
   erste offene Transition und kehrt SOFORT zurück, ohne Append:
   `ProzessManager.cs:68-76` (`await FeuereAsync(...); return;`). `FeuereAsync`
   (`:104-105`) ruft nur den fire-and-forget-`_dispatch`. Kein `AppendEventsAsync` am
   Feuer-Punkt — es gibt keine Transaktion, an die ein Outbox-Eintrag angehängt werden
   könnte.

3. **Das ist Design, kein Versehen.** Der Kommentar in
   `ProzessManagerEvents.cs:6-18` ist explizit: das Log speichert „bewusst nur
   ENTSCHEIDUNGEN, die sich NICHT aus den Ziel-Streams rekonstruieren lassen … welche
   Transitionen gefeuert/erfolgreich sind — FALTET der Manager bei jeder Weckung aus den
   autoritativen Ziel-Streams, nie aus einem Feld." Ein per-Feuer-Outbox-Eintrag ist genau
   ein solches Feld → er verletzt die Kern-Invariante des Managers („Marking aus dem
   Log der Ziel-Streams, Manager-Log = nur Entscheidungen").

Selbst der ERSTE Schritt ist nicht co-committet: `StarteAsync` appendet `ProzessGestartet`
in *einem* `SaveChanges` (`ProzessManager.cs:51`) und feuert dann in `WakeAsync` in einem
SEPARATEN Turn/SaveChanges (`:53`). Es gibt nirgends ein „Effekt + Outbox in einer Tx" für
das Feuern.

**Folge:** Das §4.2-Kernbild trägt nicht. Die Outbox für Prozesse erzwingt eine neue,
per-Transition-durable-Aufzeichnung im Manager-Log — das ist ein Umbau der Manager-
Invariante, kein „bloßes Anhängen an eine bereits existierende Entscheidung". Machbar
(Marten kann mehrere Events in einem `SaveChanges` — die Inbox beweist es), aber deutlich
größer und invasiver als die Skizze darstellt.

---

## L2 — KRITISCH/HOCH: die Reaktion hat keinen Checkpoint, mit dem man co-committen könnte

Skizze §4.2 (Reaktion): „Der Outbox-Eintrag wird MIT dem `ProjectionCheckpoint`-Vorrücken
co-committet (`IProjectionTracker.MarkProcessedAsync`)."

Am Code inkohärent:

- Die Reaktion läuft mit **`tracker = null`** (CLAUDE.md-Kontext + Verhalten
  `ProjectionAdapter.cs:54-56`: `applied = -1`, liest ab 0). `MarkProcessedAsync` wird für
  tracker-lose Pfade **nie** aufgerufen: `ProjectionAdapter.cs:77`
  (`if (_tracker is not null && last > applied)`). Es gibt also keinen
  `ProjectionCheckpoint`, an den sich ein Outbox-Eintrag hängen ließe.
- Die Marke, die für die Reaktion tatsächlich „vorrückt und lügt", ist der **skalare
  Poll-Cursor** (`MartenPollCursorStore`, `PullPath.cs:104,126`) bzw. die Selbst-Weckung.
  Er wird im **Poll-Loop** vorgerückt (`Poller.cs:73-74`, `PullPath.cs:126`) — in einem
  ANDEREN Prozess, einer ANDEREN Session als der Adapter-Turn, der den Send erzeugt. Ein
  Co-Commit „Outbox + diese Marke" ist damit nicht nur nicht implementiert, sondern
  strukturell nicht möglich (zwei getrennte Ausführungskontexte).
- Zusätzlich: der Default-`MartenProjectionTracker` co-committet **grundsätzlich nicht** —
  er öffnet für `MarkProcessedAsync` eine EIGENE Session (`MartenProjectionTracker.cs:47-60`,
  Klassen-Doc `:10-18` „EIGENE SESSION (at-least-once) … Dual-Write"). Co-Commit existiert
  heute nur in den Domänen-Stores (gepufferte Ops in derselben `IdentitySession`), nicht im
  Framework-Tracker.

**Was tatsächlich ginge** (und was die Skizze meint, aber falsch benennt): der
Reaktions-Adapter-Turn hat gar keinen lokalen Effekt außer dem Send selbst — der einzige
durable Write WÄRE der Outbox-Eintrag. Man müsste also den `DetachedEmit` durch einen
synchronen, durablen Outbox-Write ersetzen und **erst danach** das `WakeAck` senden. Das
ist ein Redesign des Reaktions-Turns, kein „Co-Commit mit dem Checkpoint". Die Skizze
beschreibt den Mechanismus falsch.

Beleg für den heutigen Verlust (Gesicht A, bestätigt): `pollWake` gibt `ack is not null`
zurück (`PullPath.cs:92`) — das `WakeAck` bestätigt nur „Turn gelaufen". Der Reaktions-Emit
ist `DetachedEmit.Wrap` → kehrt vor dem Send zurück (`DetachedEmit.cs:19-24`). Also
`WakeAck=true` → Poller rückt HWM vor (`Poller.cs:73`) → beim nächsten Boot
`startHighWater` = vorgerückt (`PullPath.cs:104`) → verlorener Send wird nie re-gescannt.

---

## L3 — HOCH: `AppendEventsAsync` kann heute kein Outbox-Dokument co-committen

Checklist §10.1: „kann `MartenEventStore.AppendEventsAsync` in EINER Marten-Tx Events UND
ein Outbox-Dokument schreiben?"

- Marten *generell* ja. Die **aktuelle Methode** nein: sie öffnet ihre eigene
  `LightweightSession` (`MartenEventStore.cs:78`), nimmt nur `IReadOnlyList<IEvent>`
  (`:58-64`) und ruft `SaveChangesAsync` (`:106`). Es gibt keinen Parameter/Seam, um ein
  Dokument (Option a, mutierbarer Status) in dieselbe Tx zu legen; die Session wird nicht
  nach außen gegeben.
- Das zitierte Vorbild („Spiegel der Inbox", §3) ist **kein Dokument-Co-Commit**: die
  `KommandoVerarbeitet`-Marke ist ein **Event im selben Stream**
  (`AggregateActorBase.cs:238-248`, angehängt an `persistentEvents`, ein `AppendEventsAsync`).
  Das trägt für Option (b) (append-only Log-Events) — aber nur, wenn es einen Append-Punkt
  am Feuern gibt (siehe L1: gibt es nicht). Für Option (a) (Marten-Dokument, die
  Empfehlung der Skizze) braucht es eine neue Signatur/einen Session-Seam.

Fazit §10.1: Antwort ist „nein, nicht mit dem aktuellen Code" — die Skizze setzt eine
Fähigkeit als vorhanden voraus, die erst gebaut werden muss; und die Dokument-Variante (a)
passt schlechter zur bestehenden event-basierten Co-Commit-Naht als die Skizze suggeriert.

---

## L4 — HOCH: Pipeline-Commands passen nicht in die at-least-once-Outbox

Skizze §4.2 (Pipeline) + §3/§4.3: „Empfänger-Inbox dedupliziert → at-least-once genügt."

Für Pipeline-Commands falsch:

- Die Pipeline sendet auf dem **OCC-Pfad**: `ExpectedVersion = version` (>= 0),
  Version-Retry bei Konflikt (`PipelineActorBase.cs:229,237,264-270`). Sie setzt **keine
  deterministische `CommandId`** — `CommandEnvelope.CommandId` bleibt auf dem Default
  `Guid.NewGuid()` (`CommandEnvelope.cs:17`; der Pipeline-Envelope `:233-241` setzt sie
  nicht).
- Die Empfänger-Inbox dedupliziert **nur** den idempotenten Pfad
  (`istIdempotent = ExpectedVersion < 0`, `AggregateActorBase.cs:186-191`). Pipeline-Commands
  (>= 0) werden **nicht** dedupliziert.
- Damit bricht die „Empfänger dedupliziert"-Prämisse: ein relayter Pipeline-Command
  erzeugt entweder einen ZWEITEN Effekt (falls die stale `ExpectedVersion` zufällig passt)
  oder einen OCC-Konflikt (falls nicht) → wird nie zugestellt, landet nur in der DLQ. Die
  §5-Tabelle „Eintrag → Relay-Retry → DLQ statt LogError" ist insofern ehrlich (DLQ), aber
  die Outbox **löst den Pipeline-Drop nicht** (keine Zustellung), und sie widerspricht der
  globalen „exactly-once-wirksam via Inbox"-Erzählung.
- **Ordnung** (Checklist §10.7): für die AnyVersion-Ziele von Prozess/Reaktion ist die
  Reihenfolge egal (kein OCC). Für Pipeline-Commands ist sie es NICHT (versionsabhängig).
  Die pauschale Antwort „reihenfolge-tolerant" gilt also nur, wenn Pipeline-Commands aus
  der Outbox draußen bleiben.

Empfehlung: Pipeline-Drops aus dem Outbox-Scope nehmen (oder Pipeline vorher auf den
deterministischen-CommandId/AnyVersion-Pfad heben — größerer Umbau).

Nebenbefund (Checklist §10.8, „kein neuer Hang"): die Pipeline hat bereits **unbounded**
Sends — `RequestAsync(..., CancellationToken.None)` in `SendCommandAsync`
(`PipelineActorBase.cs:246-247`) und `SendTriggerAsync` (`:423-424`). Wenn der Relay den
Pipeline-Pfad wiederverwendet, erbt er diese Hang-Quelle. Der Relay selbst muss (wie die
Skizze §4.3.2 fordert) bounded sein — die Vorlagen `SendeAnZiel`/`SendReaktionAsync` sind
es (5s/3s CTS, `ProzessManagerActor.cs:114`, `HandlerOutputRouter.cs:108`), die Pipeline
ist es nicht.

---

## L5 — HOCH: ein Effekt-Typ rückt die Marke vor, ohne appliziert ODER outboxed zu sein

Checklist §10.4: „gibt es einen Effekt-Typ, der weder durabel angewandt noch als
Outbox-Eintrag erfasst wird, über den der Checkpoint trotzdem vorrückt?" — **Ja, zwei:**

1. **Reaktive `IEvent`-Ausgaben.** Ein Handler/eine Reaktion kann statt eines `ICommand`
   ein `IEvent` yielden → `HandlerOutputRouter.RouteAsync` case `IEvent` →
   `PublishReactiveAsync` (`:46-47,54-75`). Das re-published NUR an den Broker
   (`_publisher.PublishAsync`, `:73`) — es wird **nirgends persistiert** (kein
   `AppendEventsAsync`) und die Outbox hält laut §4.1 nur **Commands**. Hängt ein durabler
   Konsument an diesem reaktiven Event, geht es beim Crash verloren, ist NICHT aus dem Log
   neu ableitbar (es steht in keinem Stream), und die Marke rückt über das auslösende Event
   vor. Loch in „advance-on-durability".

2. **Zukünftige Schritte eines mehrstufigen/Fan-out-Prozesses** (siehe L6): der Manager
   feuert pro Weckung nur EINE Transition (`ProzessManager.cs:68-70`, `FirstOrDefault(!ErgebnisDa)`).
   Nur der gerade gefeuerte Schritt wäre je in der Outbox; die noch offenen Folge-Schritte
   haben keinen Outbox-Eintrag. Der Cursor rückt aber weiter (s. L2/L7).

---

## L6 — MITTEL/HOCH: die Outbox ist KEIN vollständiges „offene-Arbeit"-Ledger

Skizze §4.6: „Die Outbox liefert die präzise Menge offener Arbeit — genau der durable
Ersatz für die verlorene Selbst-Weckung."

Überzogen. Weil der Manager sequenziell ist (eine Transition pro Weckung,
`ProzessManager.cs:68-70`) und für Folge-Schritte nichts Durables schreibt (L1), enthält
die Outbox zu jedem Zeitpunkt höchstens den EINEN gerade fliegenden Send. Ein Prozess, der
Schritt 1 gefeuert (Outbox-Eintrag → `Done`) aber noch nicht zu Schritt 2 re-geweckt wurde,
hat **keinen** offenen Outbox-Eintrag und **kein** `ProzessBeendet` — er ist für einen
outbox-basierten Scan unsichtbar. Nur ein Scan „`ProzessGestartet` ohne `ProzessBeendet`"
fängt ihn — und dieser Scan existiert nicht (kein Index/keine Query über Manager-Streams;
`ProzessManagerStartupService` verdrahtet nur Signal-Router + Poll, kein Open-Work-Scan).
Der „offene-Arbeit"-Backstop ist also neue, nicht vorhandene Infrastruktur, und die Outbox
ersetzt dafür nur einen Teil.

---

## L7 — MITTEL: der Prozess-Poll-Cursor rückt UNBEDINGT nach fire-and-forget vor

Zusätzliche „Marke lügt"-Stelle, die die Skizze nicht aufzählt (§1.1 nennt nur den
Projektions-`WakeAck`): `ProzessManagerStartupService.PollLoopAsync` weckt den Manager
fire-and-forget (`:92`, `_ = _system.Cluster().RequestAsync(...)`) und rückt danach
**bedingungslos** vor: `hwm = changes.HighWaterMark; await _pollCursors.SetAsync(...)`
(`:131-132`). Anders als die Projektions-`Poller`-Klasse (die auf `alleBestaetigt` wartet,
`Poller.cs:52-74`) gibt es hier keine Bestätigung. Ein verlorener letzter Wake vor dem
Cursor-Vorrücken → der terminale/offene Prozess wird nie wieder re-geroutet → genau der
Terminal-Hang, den die Skizze heilen will, überlebt den Cursor. Das verschärft L2/L6 und
untermauert, dass „Vorrücken auf Turn/Wake" an mehreren Stellen sitzt.

---

## L8 — MITTEL: Outbox-Id-Kollision bei Reaktions-Fan-out

Skizze §4.1: „Id ist deterministisch = der Vorgang / deterministische CommandId" und
kollidiert bei Recovery „auf derselben Id statt einen zweiten Effekt zu erzeugen."

Für Reaktionen ist die Id `ReaktionsId.For(streamId, version, commandTypeName)`
(`HandlerOutputRouter.cs:86`, `ReaktionsId.cs:21-27`) — der Diskriminator ist **nur der
Command-Typname**, NICHT die Ziel-`AggregateId` (anders als `ProzessId.FürTransition`, das
den Ziel-Diskriminator führt, `ProzessId.cs:35-37`). Yieldet eine Reaktion zwei Commands
desselben Typs an verschiedene Ziele, kollidieren sie auf derselben `CommandId` = derselben
Outbox-Id. Bei getrennten Empfänger-Inboxen ist die Doppel-Zustellung heute harmlos (zwei
verschiedene Aggregate). Aber als **Outbox-Schlüssel** würde der zweite Eintrag den ersten
überschreiben → ein Command geht verloren. Für die Outbox muss die Reaktions-Id um den
Ziel-Diskriminator erweitert werden (wie bei Prozessen).

---

## L9 — NIEDRIG/MITTEL: Scope-Grenze A vs. B nicht so sauber wie §6 behauptet

Checklist §10.5 / §6: Reaktionen UND Prozesse hängen am selben **skalaren** Poll-Cursor
(`MartenPollCursorStore`, per `SubscriberId`/`RouterId`), dessen Global-Freeze die Skizze
als Gesicht B ausklammert. Der Terminal-Re-Wake-Backstop, auf den die Outbox-Lösung für
Prozesse baut (§4.6), ERBT diesen Freeze: ein einziger kranker Stream friert die HWM →
neue terminale Events werden nicht mehr re-gescannt. Ausgangs-Ledger (Outbox) und
Eingangs-Ledger (Frontier) sind konzeptionell getrennt, aber die **Heilung** der Outbox-
Welt (Re-Wake) läuft über die Eingangs-Frontier. A ist damit ohne einen Minimal-Teil von B
nicht vollständig robust. Ehrlich zu benennen, nicht zwingend blockierend.

---

## Was am Code HÄLT (bestätigt, nicht bloß angenommen)

- **Idempotenz-Kette für Prozess & Reaktion (Checklist §10.2/§10.3): SOLIDE.**
  - (a) Vorgang-Id aus bei Recovery STABILEN Eingaben: `ProzessId.Für(name, stream, version)`
    (`ProzessId.cs:22-23`) und `ProzessId.FürTransition(korrelation, tokenStream,
    tokenVersion, cmdTyp, zielAggregat)` (`:35-37`) — alle Eingaben stammen aus dem Log
    (Auslöser-Koordinaten in `ProzessGestartet`, Ziel-Streams), kein Zeit/Zufall/Reihenfolge.
    Re-Fold nach Neustart erzeugt denselben Vorgang.
  - (b) Sender setzt `CommandId = vorgang`: `ProzessManagerActor.SendeAnZiel`
    (`:104` `CommandId = vorgang`) und `HandlerOutputRouter.SendReaktionAsync`
    (`:94-96` `CommandId = commandId`).
  - (c) Empfänger dedupliziert per genau dieser `CommandId` VOR jedem Effekt:
    `AggregateActorBase.cs:186-191` (Check läuft vor `HandleCommand`), Marke co-committet
    `:238-248`, beim Fold übersprungen aber mitgezählt `MartenEventStore.cs:170-172`.
  Diese Kette ist der belastbare Kern, auf dem die Outbox aufsetzen kann — für
  Prozess/Reaktion, NICHT für die Pipeline (L4).

- **Marten kann mehrere Events atomar in einem `SaveChanges`** (die Inbox beweist es,
  `AggregateActorBase.cs:242-248`). Eine Outbox als **Event-Form** (Option b) auf dem
  Manager-Stream ist mit der bestehenden `AppendEventsAsync`-API machbar — sobald ein
  Append-Punkt am Feuern eingeführt wird (der Preis von L1).

- **Ordnung** für AnyVersion-Ziele unnötig (kein OCC am Empfänger,
  `AggregateActorBase.cs:160-181` überspringt die Assertion bei `< 0`). Der Manager
  serialisiert ohnehin (eine Transition pro Weckung). ✅ — außer Pipeline (L4).

---

## Priorisierte Loch-Liste

| # | Schwere | Kern | Datei-Beleg |
|---|---|---|---|
| L1 | kritisch | Kein Co-Commit-Punkt beim Feuern; `SchrittErledigt` fiktiv; Feuern schreibt bewusst nichts durabel | `ProzessManager.cs:68-76,104`; `ProzessManagerEvents.cs:6-18` |
| L2 | kritisch/hoch | Reaktion hat `tracker=null` → kein Checkpoint zum Co-Committen; die lügende Marke ist der out-of-band Poll-Cursor | `ProjectionAdapter.cs:54-56,77`; `PullPath.cs:92,126`; `MartenProjectionTracker.cs:10-18,47-60` |
| L3 | hoch | `AppendEventsAsync` bietet keinen Session-/Doc-Seam; Inbox-Vorbild ist Event-, kein Dokument-Co-Commit | `MartenEventStore.cs:58-106`; `AggregateActorBase.cs:238-248` |
| L4 | hoch | Pipeline nutzt OCC ohne deterministische CommandId → Empfänger dedupliziert NICHT; Outbox stellt sie nicht zu, nur DLQ; Ordnung nötig | `PipelineActorBase.cs:229,233-247,264-270`; `AggregateActorBase.cs:186-191` |
| L5 | hoch | Reaktive `IEvent`-Ausgaben (broker-only, nie persistiert, nicht in Outbox) rücken die Marke vor | `HandlerOutputRouter.cs:46-47,54-75` |
| L6 | mittel/hoch | Outbox ≠ vollständige „offene Arbeit" (nur der fliegende Schritt); Open-Work-Scan existiert nicht | `ProzessManager.cs:68-70`; `ProzessManagerStartupService.cs` |
| L7 | mittel | Prozess-Poll-Cursor rückt bedingungslos nach fire-and-forget-Wake vor | `ProzessManagerStartupService.cs:92,131-132` |
| L8 | mittel | Reaktions-Id ohne Ziel-Diskriminator → Outbox-Id-Kollision bei Fan-out | `ReaktionsId.cs:21-27`; `HandlerOutputRouter.cs:86` |
| L9 | niedrig/mittel | A/B nicht sauber getrennt: Re-Wake-Heilung von A läuft über die geteilte skalare B-Frontier | `PullPath.cs:104,126`; `ProzessManagerStartupService.cs:103-132` |

---

## Fazit

**Nicht umsetzungsreif — erst nachbessern bei L1, L2, L4** (und L5 als klares
Korrektheits-Loch). Das Prinzip „co-commit intent, deliver async, advance on durability"
ist richtig und die Idempotenz-Kette (Prozess/Reaktion) trägt am Code. Aber die Skizze
verortet den Co-Commit an zwei Stellen, die es so nicht gibt:

1. Der Prozess-Manager schreibt beim Feuern **bewusst nichts** durabel (Marking wird aus
   den Ziel-Streams gefaltet). Eine Outbox erzwingt dort eine **neue per-Transition-
   Aufzeichnung** — ein Eingriff in die Manager-Kern-Invariante, kein Anhängen an
   Bestehendes. Das muss die Skizze explizit als solchen Umbau ausweisen (inkl. Effekt auf
   „Log = nur Entscheidungen").

2. Die Reaktion hat **keinen Checkpoint** (`tracker=null`); die relevante vorrückende Marke
   ist der skalare Poll-Cursor, der in einem anderen Prozess/Session vorgerückt wird. „Mit
   `MarkProcessedAsync` co-committen" ist dort nicht anwendbar. Der korrekte Mechanismus
   (synchroner durabler Outbox-Write im Adapter-Turn VOR dem `WakeAck`, `DetachedEmit`
   ersetzen) ist ein anderer als der beschriebene und muss so formuliert werden.

3. Pipeline-Commands gehören nicht in dieselbe at-least-once-Outbox (OCC, keine Dedup).
   Entweder aus dem Scope nehmen oder separat behandeln.

Empfehlung: Skizze auf **Prozess + Reaktion** fokussieren, den Co-Commit als Event-Form
(Option b) auf dem jeweiligen durablen Stream spezifizieren (Manager-Stream mit neuem
Feuer-Append; Reaktion mit eigenem Outbox-Write-Turn), die Pipeline und die reaktiven
`IEvent`-Ausgaben ausdrücklich ausklammern oder separat lösen, und §4.6 von „Outbox = alle
offene Arbeit" auf „Outbox = fliegende Sends; Terminal-Erkennung braucht zusätzlich den
`ProzessGestartet`-ohne-`ProzessBeendet`-Scan" korrigieren.
