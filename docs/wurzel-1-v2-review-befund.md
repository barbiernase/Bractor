# Wurzel-1 v2 — adversarialer Code-Review (unabhängig, frischer Blick)

> Haltung: „das Design ist falsch, bis der Code das Gegenteil zeigt." Jeder Fund mit Datei:Zeile.
> Prüfziel: `docs/wurzel-1-outbox-skizze-v2.md` gegen den echten Code. Scope: Ein-Knoten (Multi-Node NICHT
> als Mangel gewertet). v1/v1-Review überflogen, aber eigenständig am Code nachverifiziert.

---

## Kurzurteil

**Umsetzungsreif mit Auflagen** — deutlich tragfähiger als v1. Die ZWEI zentralen v2-Behauptungen halten
am Code: (b) der Refold in `ProzessManager.WakeAsync` schreibt nach erfolgreichem terminalem Send WIRKLICH
`ProzessBeendet` (bestätigt), und (d) die Co-Commit-Naht kann Effekt + Checkpoint + Outbox-Dokument in EINEM
`SaveChanges` (bestätigt am Vorbild). Die Idempotenz-Kette trägt. **Aber drei Löcher bleiben:** (V1) die
§3-Backstop-Vollständigkeit hat eine echte Lücke an der **START-Kante** — v2s „der Backstop trägt die
Liveness" überzieht; (V2) v2s **Empfehlung Option (b)** (durabler Offen-Index) ist mit dem heutigen
`AppendEventsAsync` **nicht co-committbar** → selbst eine Inkonsistenzquelle; (V3) §5 „Pipeline nur DLQ"
lässt eine **schon heute bestehende Doppelwirkung** bei verlorener Quittung unbenannt.

Nachbessern bei **V1, V2** (hoch); V3/V4 präzisieren.

---

## Checkliste §11 — Punkt für Punkt

### (a) §3 Backstop-Vollständigkeit — LOCH V1 (HOCH)

Für BEREITS gestartete Prozesse ist „`ProzessGestartet` ohne `ProzessBeendet`" **vollständig**: jeder
Terminal-Pfad appendet `ProzessBeendet` (`ProzessManager.cs:75` Erfolg, `:88` fehlgeschlagen), ein
Fehlschlag wird vorher durabel (`SchrittGescheitert`, `:99`) aber setzt KEIN Beendet bis terminal. Der
v1-Fall L6 (in-flight zwischen Schritt 1 fertig und Schritt-2-Weckung) IST damit geschlossen — ein solcher
Prozess hat Gestartet, kein Beendet → der Scan sieht ihn. **Positiv bestätigt.**

**Das Loch liegt an der START-Kante.** Der Prozess-START läuft NICHT über die Reaktions-Outbox, sondern der
`KorrelationsRouter` startet direkt aus dem Auslöse-Event (v2 §2, belegt `KorrelationsRouter.cs:59-62`):
`_wecke(korr, stream, version, prozessName)`. `wecke` ist **fire-and-forget** (`ProzessManagerWiring.cs:92`
`_ = _system.Cluster().RequestAsync<WakeAck>(...)`) und der Prozess-Poll-Cursor rückt danach **bedingungslos**
vor (`ProzessManagerWiring.cs:131-132` `hwm = changes.HighWaterMark; await _pollCursors.SetAsync(...)`).

Szenario: Boot → Poll scannt das Auslöser-Event (Seq S) → Weckung verpufft (Manager-Spawn-Race/Crash) →
Cursor rückt über S vor → nächster Boot `startHwm > S` (`:103`) → das Auslöser-Event wird nie wieder
gescannt → der Prozess **startet nie**, hat KEIN `ProzessGestartet` → für den §3-Backstop
(Gestartet-ohne-Beendet) **strukturell unsichtbar.** v2 §3 behauptet „der bestehende Prozess-Poll-Cursor
ist danach nur noch eine Latenz-Optimierung, keine Korrektheits-Abhängigkeit — der Backstop trägt die
Liveness." Für die START-Kante ist das **falsch**: die Start-Liveness hängt weiter am unbedingt
vorrückenden Poll-Cursor (das ist der Review-Befund L7, auf die Start-Kante angewandt). §6-Tabelle hat
keine Zeile „verlorener Start-Send".

→ **Widerlegt v2 §3 „Backstop trägt die Liveness" und §6-Vollständigkeit.** Fix: entweder den Start über
dieselbe durable Reaktions-Outbox (§4) führen, ODER den Router-Poll-Cursor **bedingt** vorrücken lassen
(wie die Projektions-`Poller`-Klasse, die auf Bestätigung wartet, `PullPath.cs:84-95` / `Poller`), ODER
den §3-Scan zusätzlich auf „Auslöser-Event ohne zugehörigen `ProzessGestartet`" ausweiten.

### (b) §3 Refold heilt terminal — HÄLT (bestätigt)

`WakeAsync` (`ProzessManager.cs:57`) faltet das Marking aus den Ziel-Streams (`:63`), `ErgebnisDa` wird per
Kausalität gefaltet (`:155` `zielEvents.FirstOrDefault(e => e.CausationId == vorgang.ToString())`). Nach
erfolgreichem terminalem Send: `pending = kandidaten.FirstOrDefault(!ErgebnisDa)` (`:68`) → null, und
`mz.Gescheitert.Count == 0` → **`AppendAsync(... new ProzessBeendet(true, ""))`** (`:75`). Das Ergebnis-Event
trägt `CausationId = CommandId = vorgang` (der Empfänger-Actor stempelt so, `AggregateActorBase.cs:247`) →
der Refold sieht die letzte Transition erledigt. **Bestätigt.** Idempotent: `if (mz.Beendet) return`
(`:60`) → doppelte Terminal-Weckung verpufft. Die Selbst-Weckung existiert (`ProzessManagerActor.cs:87-94`,
`DetachedProzessSend.cs:57-65`) — bei ihrem Verlust trägt NUR der (noch nicht gebaute) §3-Backstop. Konsistent.

### (c/§4.1) Reaktions-Checkpoint verträglich mit dem Pull-Adapter — HÄLT, mit Präzisierung (V4, MITTEL)

`ProjectionAdapter._tracker` ist optional (`ProjectionAdapter.cs:30,54-56,77`); ein echter Tracker für die
Reaktion ist mechanisch verträglich. Der **Push-Weg ist gelöscht** (CLAUDE.md B1) → es gibt keinen
Push-Pfad mehr zu brechen; der Signal-Weg (`SignalReceiverActor` → Wake) ist tracker-agnostisch.

**Präzisierung (kein Loch, aber v2 stellt es zu klein dar):** die Reaktion emittiert heute über
`_dispatch → HandlerOutputRouter → DetachedEmit.Wrap` (fire-and-forget Send, `DetachedEmit.cs:19-24`).
Damit der Checkpoint nicht wieder über einen nur-emittierten (verlierbaren) Send vorrückt, MUSS der
Reaktions-`_dispatch` von „sofort senden" auf „Outbox-Eintrag in den Store-Puffer schreiben" (wie
`ImagePairHistorieStore.AppendEintragAsync`, `:30-34`) umgebaut werden. Das ist ein Eingriff in den
**generierten Reaktions-Dispatch + die per-Stream-Adapter-Verdrahtung**, nicht bloß „einen `IProjectionTracker`
per DI dranhängen". v2 §4.1/§4.2 meint genau das, benennt aber nur den Tracker.

### (d/§4.2) Co-Commit Effekt + Checkpoint + Outbox in EINEM SaveChanges — HÄLT (bestätigt)

`ImagePairHistorieStore.MarkProcessedAsync` (`Domain.Infrastructure/ImagePairHistorieStore.cs:49-70`): EINE
`IdentitySession`, staged alle gepufferten Effekte (`:52-58`) UND den `ProjectionCheckpoint` (`:60-67`), EIN
`SaveChangesAsync` (`:69`). Ein zusätzliches `s.Store(outboxDoc)` vor `SaveChanges` liegt in **derselben Tx**
→ atomar. **Bestätigt.** (Gilt für den Reaktions-Store; NICHT für `MartenEventStore.AppendEventsAsync`, siehe V2.)

### (e/§4.3) ReaktionsId-Diskriminator aus stabilen Eingaben + Fan-out-Bedarf — HÄLT, v2 korrekt

`ReaktionsId.For(streamId, aggregateVersion, discriminator)` (`ReaktionsId.cs:21-27`), Aufruf mit
`discriminator = command.GetType().Name` (`HandlerOutputRouter.cs:86`). Eingaben (Auslöser-StreamId,
dessen Version, Command-Typname) stammen aus dem Log → bei Recovery **stabil** (kein Zeit/Zufall/Reihenfolge).
**Aber** der Diskriminator ist NUR der Typname, NICHT die Ziel-`AggregateId` → zwei gleiche-Typ-Commands
an verschiedene Ziele aus EINEM Quell-Event → Id-Kollision. Als reine Empfänger-Dedup heute harmlos
(getrennte Inboxen); als **Outbox-Schlüssel** überschreibt der zweite Eintrag den ersten → ein Command
geht verloren. v2 §4.3 fordert die Erweiterung um den Ziel-Diskriminator — **korrekt und nötig.** Vorbild
existiert: `ProzessId.FürTransition(..., diskriminator=cmd.AggregateId)` (`ProzessId.cs:35-37`). Die
erweiterte Id bleibt stabil (Ziel-`AggregateId` ist deterministisch aus dem Quell-Event). Heute nur EINE
Reaktion, EIN Command (`ImagePairReaktion.cs:29`) → Kollision latent. **v2 korrekt.**

### (f/§4.5) Verlierbar-Klassifikation — HÄLT (heute trivial sicher)

Die einzige Reaktion heute yieldet `WirkeReaktion` = **ICommand** (`ImagePairReaktion.cs:29`; grep über
`Domain.Projections`: genau ein `yield return`). **Es existiert heute KEINE reaktive `IEvent`-Ausgabe.** Der
Pfad `HandlerOutputRouter.PublishReactiveAsync` (`:54-75`) ist ungenutzt. Also ist „verlierbar" vacuously
korrekt — kein durabler Konsument hängt an einem reaktiven Event, weil keins existiert. Die offene
Entscheidung 4.5 ist real für die Zukunft (sobald jemand ein `IEvent` yieldet), aber **kein aktuelles Loch.**

### (g/§5) Pipeline „nur DLQ, kein Retry" sicher? — TEILWEISE FALSCH, LOCH V3 (MITTEL)

Die Pipeline **retryt heute schon** — v2s „kein Retry" beschreibt den Ist-Zustand ungenau.
`SendCommandAsync` (`PipelineActorBase.cs:231-281`): bei verlorener Quittung `result == null` → `continue`
(`:249-253`), bei OCC-Konflikt Versions-Korrektur + Retry (`:264-270`). Der `CommandEnvelope` wird **pro
Versuch neu** erzeugt (`:233-241`) → jeweils `Guid.NewGuid()`-Default-`CommandId`, `ExpectedVersion = version`
(OCC). Die Empfänger-Inbox dedupliziert NUR den idempotenten Pfad (`AggregateActorBase.cs:186`
`istIdempotent = ExpectedVersion < 0`) → Pipeline-Commands **nicht.**

Szenario „Effekt angewandt, Ack verloren": Versuch 1 wendet an (V → V+1), Ack `null` → Versuch 2 mit stale
V → OCC-Konflikt → Korrektur auf V+1 (`:265-269`) → Versuch 3 trifft → **ZWEITER Effekt.** Doppelwirkung,
schon **ohne** jede Outbox. v2 §5 ändert NUR den finalen stillen Drop (`:279`) in einen DLQ-Eintrag — der
doppel-anwendende Retry-Pfad bleibt unangetastet. **Antwort auf Checklist g: NEIN**, der (retryte) Send ist
nicht idempotenz-abgesichert; „nur DLQ" beseitigt den stillen Verlust, aber NICHT die vorbestehende
Doppelwirkung. v2 ist ehrlich, dass der Vollausbau deterministische Ids braucht — benennt aber die schon
heute bestehende Doppelwirkung-bei-Retry NICHT. → Präzisieren; ggf. den `result==null`-Retry entschärfen,
bis deterministische Ids da sind.

Nebenbefund: der Pipeline-Send ist **unbounded** (`PipelineActorBase.cs:246-247`
`RequestAsync(..., CancellationToken.None)`). Kein v2-Relay-Problem (das Relay fasst die Pipeline nicht an),
aber der spätere §5-Vollausbau erbt diese Hang-Quelle.

### (Invariante 6) Wandert Verlierbares in die Outbox? — HÄLT

v2 §4.5 klassifiziert reaktive `IEvent`s explizit als verlierbar; nur Commands (must-happen) in die Outbox.
Konsistent mit Invariante 6. **Kein Verlierbares in der Outbox.**

### (h) Kein neuer Hang — jeder RequestAsync bounded? — HÄLT (Vorbilder existieren)

Bounded: `SendeAnZiel` 5s (`ProzessManagerActor.cs:113-114`), `SendReaktionAsync` 3s
(`HandlerOutputRouter.cs:106-108`), `WeckeSelbst` 5s (`:90-91`), `MeldeFehlschlagAnManager` 5s (`:123-124`),
`pollWake` 10s (`PullPath.cs:87-88`). Der Prozess-Poll-`wecke` (`ProzessManagerWiring.cs:92`) hat kein
eigenes `CancelAfter`, ist aber fire-and-forget → blockiert nichts (potenzieller Leak, kein Hang).
Backstop/Relay KÖNNEN bounded gebaut werden. **Hält, sofern beim Bau eingehalten.**

---

## Zusätzlicher adversarieller Fund

### V2 (HOCH) — §3.1 Empfehlung (b) „durabler Offen-Index" ist nicht co-committbar

v2 §3.1 empfiehlt Option (b): Offen-Index beim `Gestartet` setzen, beim `Beendet` löschen (O(offen) statt
O(Historie)). **Aber:** der Manager appendet `ProzessGestartet`/`ProzessBeendet` über
`ProzessManager.AppendAsync → _store.AppendEventsAsync` (`ProzessManager.cs:287-288`), und
`MartenEventStore.AppendEventsAsync` öffnet eine EIGENE `LightweightSession` (`MartenEventStore.cs:78`),
nimmt nur `IReadOnlyList<IEvent>` (`:58-64`) und ruft `SaveChangesAsync` (`:106`) — **kein Seam für ein
Index-Dokument in derselben Tx** (identisch zum v1-Review-Befund L3, gilt weiter). Ein Offen-Index wäre
also ein SEPARATER Write → Drift-Fenster: Crash zwischen `ProzessGestartet`-Append und Index-Set →
Prozess offen, aber NICHT im Index → für den Index-Scan unsichtbar. Das ist **genau das Loch, das v2
schließen will**, nur anders erzeugt — v2s Empfehlung (b) importiert die Krankheit.

Option (a) (event-typ-gefilterte Marten-Query über die Manager-Streams nach `ProzessGestartet` ohne
`ProzessBeendet`) ist dagegen **zustandsfrei und sicher** und **machbar**: die Manager-Events sind
Marten-registriert und rücklesbar (`LadeStatusAsync` liest `ProzessGestartet` zurück,
`MartenEventStore.cs:198-232`; die `IProzessIntern`-Events zählt der TypeRegistry-Generator symbol-basiert
mit, `ProzessManagerEvents.cs:28-33`). → **Empfehlung umkehren: (a), nicht (b)** — oder (b) nur, wenn der
Index als co-committetes Event auf dem Manager-Stream mitgeschrieben wird (dann ist er aber kein O(offen)-Doc mehr).

---

## Was am Code HÄLT (bestätigt)

- **Refold-heilt-terminal** (§3, Checklist b): `ProzessManager.cs:63-75` faltet + appendet `ProzessBeendet`. ✅
- **Co-Commit-Naht** (§4.2, Checklist d): `ImagePairHistorieStore.cs:49-70` — Effekt + Checkpoint in einem
  `SaveChanges`; Outbox-Doc trivial dazu. ✅
- **Idempotenz-Kette** (Prozess & Reaktion): deterministische Id aus stabilen Log-Eingaben
  (`ProzessId.cs:22-45`, `ReaktionsId.cs:21-27`), Sender setzt `CommandId = vorgang`
  (`ProzessManagerActor.cs:104`, `HandlerOutputRouter.cs:94`), Empfänger dedupliziert VOR dem Effekt +
  co-committet die `KommandoVerarbeitet`-Marke (`AggregateActorBase.cs:186-191, 238-260`), beim Fold
  übersprungen aber mitgezählt (`MartenEventStore.cs:170-172`). ✅
- **„Gestartet ohne Beendet" schließt v1-L6** (in-flight zwischen Schritten). ✅
- **Push-Weg gelöscht** → §4.1 bricht keinen Push-Pfad. ✅
- **Reaktive IEvents heute nicht vorhanden** → §4.5 heute sicher. ✅

---

## Priorisierte Loch-Liste

| # | Schwere | Kern | Datei-Beleg | Widerlegt/schwächt |
|---|---|---|---|---|
| V1 | hoch | START-Kante unsichtbar für den §3-Backstop; Start-Liveness hängt weiter am unbedingt vorrückenden Poll-Cursor | `ProzessManagerWiring.cs:88-95,131-132`; `KorrelationsRouter.cs:59-62` | §3 „Backstop trägt die Liveness", §6-Vollständigkeit |
| V2 | hoch | §3.1-Empfehlung (b) Offen-Index nicht co-committbar → eigene Inkonsistenzquelle; (a) wäre sicher | `ProzessManager.cs:287-288`; `MartenEventStore.cs:58-64,78,106` | §3.1 Empfehlung |
| V3 | mittel | Pipeline retryt schon; bei verlorener Quittung Doppelwirkung; „nur DLQ" adressiert sie nicht | `PipelineActorBase.cs:233-247,249-270`; `AggregateActorBase.cs:186` | §5 „nur DLQ … ohne Exactly-once vorzutäuschen" (unvollständig) |
| V4 | mittel | §4.1 ist ein Dispatch-Umbau (Emit→Outbox-Puffer), nicht bloß Tracker-DI | `ProjectionAdapter.cs:66`; `DetachedEmit.cs:19-24` | §4.1/§4.2 Darstellung |
| V5 | niedrig/mittel | ReaktionsId ohne Ziel-Diskriminator → Outbox-Key-Kollision bei Fan-out (v2 fordert Fix bereits) | `ReaktionsId.cs:21-27`; `HandlerOutputRouter.cs:86` | bestätigt §4.3 (kein Widerspruch) |

---

## Fazit

**Umsetzungsreif — erst nachbessern bei V1 und V2.** v2 hat die zwei kritischen v1-Löcher (L1 fehlender
Feuer-Co-Commit; L2 Reaktions-Checkpoint falsch verortet) sauber umschifft: der Prozess-Backstop nutzt
das vorhandene Refold statt einer Feuer-Outbox (§3, am Code bestätigt), und die Reaktions-Outbox reitet
auf der real existierenden Co-Commit-Naht (§4.2, am Code bestätigt). Das ist ein echter Fortschritt.

Zu schließen vor der Umsetzung:

1. **START-Kanten-Liveness (V1):** der §3-Backstop (Gestartet-ohne-Beendet) sieht einen getriggerten,
   aber nie gestarteten Prozess NICHT. Entweder den Start über die Reaktions-Outbox (§4) führen, oder den
   Router-Poll-Cursor bedingt vorrücken lassen (wie die Projektions-`Poller`), oder den Scan auf
   „Auslöser ohne `ProzessGestartet`" ausweiten. v2s „der Backstop trägt die Liveness" ist so, wie
   formuliert, für die Start-Kante nicht erfüllt.

2. **§3.1 auf Option (a) umstellen (V2):** die empfohlene (b) ist mit `AppendEventsAsync` nicht atomar
   → sie erzeugt genau die Drift, die der Backstop beheben soll. Die zustandsfreie Typ-Query (a) ist
   sicher und machbar.

3. **§5 ehrlich zu Ende denken (V3):** die Doppelwirkung bei verlorener Quittung besteht schon heute im
   Retry-Loop; „nur DLQ" beseitigt sie nicht. Entweder den `result==null`-Retry bis zur deterministischen
   Id entschärfen, oder das Risiko explizit im Design ausweisen.

4. **§4.1 präzise als Dispatch-Umbau formulieren (V4)** und **§4.3-Fan-out-Diskriminator (V5)** wie von
   v2 gefordert umsetzen — beides ist tragfähig, nur größer als „Tracker dranhängen".

Der Reaktions-Outbox-Kern (§4) und der Refold-Backstop (§3, für gestartete Prozesse) sind belastbar.
