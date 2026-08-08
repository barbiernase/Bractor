# Wurzel 1 — v2: Prozess-Backstop + Reaktions-/Pipeline-Outbox (Skizze zur Prüfung)

> **Status:** Design-Skizze v2, NICHT implementiert. Zur erneuten Prüfung.
> **Vorgeschichte:** v1 (`wurzel-1-outbox-skizze-v1.md`) schlug EINE universelle Outbox vor. Der
> adversarielle Review (`wurzel-1-review-befund.md`) hat das widerlegt: der zentrale Co-Commit-Punkt
> saß an zwei Stellen, die es im Code nicht gibt (Prozess-Manager appended beim Feuern NICHT; Reaktion
> hat keinen Checkpoint). v2 zerlegt Wurzel 1 in **zwei getrennte Mechanismen** und nutzt, was schon da ist.
> **Scope:** Ein-Knoten. Bruchstelle = Prozess-Neustart/Redeploy, nicht Multi-Node.

---

## 0. Was der Review geändert hat (Kurz-Delta v1 → v2)

| v1 behauptete | Review-Befund (belegt) | v2 |
|---|---|---|
| Outbox pro gefeuertem Prozess-Schritt, co-committet mit `SchrittErledigt` | `SchrittErledigt` existiert nicht; `FeuereAsync` sendet ohne Append (`ProzessManager.cs:104`); Log speichert bewusst nur Entscheidungen, Marking wird gefaltet | **Keine Prozess-Outbox.** Prozess-Backstop aus dem bestehenden Entscheidungs-Log (§3) |
| Reaktions-Outbox co-committet mit `ProjectionCheckpoint` | Reaktion läuft `tracker=null`, `MarkProcessedAsync` nie gerufen; lügende Marke ist der out-of-band Poll-Cursor (`ProjectionAdapter.cs:77`, `PullPath.cs:126`) | **Reaktions-Checkpoint zuerst einführen**, dann Outbox darauf (§4) |
| Pipeline-Command in die at-least-once-Outbox | Pipeline sendet OCC ohne deterministische Id; Inbox dedupliziert nur AnyVersion (`AggregateActorBase.cs:186`) → Doppel-Effekt | **Pipeline nur DLQ, kein Retry-Outbox** (§5), Vollausbau erst mit deterministischer Id |

Die **Idempotenz-Kette selbst hält** (Review bestätigt): Vorgang-Id aus stabilen Log-Eingaben
(`Abstractions/ProzessId.cs`), Sender setzt `CommandId = vorgang` (`ProzessManagerActor.cs:104`,
`HandlerOutputRouter.cs:94`), Empfänger dedupliziert davor und co-committet die Marke
(`AggregateActorBase.cs:186-248`). Nur die **Verortung** war falsch.

---

## 1. Das Problem (unverändert, präzise)

„Erledigt" wird aus dem Kontrollfluss erschlossen (Turn gelaufen), nicht aus bestätigter Wirkung. Es
fehlt eine durable Aufzeichnung von „Absicht, die noch nicht bestätigt erledigt ist". Zwei Gesichter:

- **A — ausgehende Wirkung geht verloren** (Terminal-Hang, Reaktions-Verlust, Pipeline-Drop, WakeAck-Lüge).
- **B — Konsumenten-Fortschritt (Read-Seite) rückt über Unbestätigtes vor** (Poll-Sequenz-Lücke,
  Voll-Scan, globaler Freeze). **Weiterhin separates Inkrement, nicht hier.**

v2 adressiert Gesicht A — jetzt aber mit **zwei** passenden Mechanismen statt einem falsch verorteten.

---

## 2. Erkenntnis: es sind zwei verschiedene Situationen

Der entscheidende Unterschied, den v1 verwischt hat:

- Der **Prozess-Manager** rekonstruiert seinen Zustand bei JEDER Weckung durch **Falten aus den
  Ziel-Streams** (`ProzessManager.cs:120-124,231`). Ein verlorener Send heilt darum **von selbst**: der
  nächste Fold sieht die Transition weiter „pending" (das Ziel hat kein Ergebnis-Event) und feuert erneut
  — mit derselben deterministischen Vorgang-Id, der Empfänger dedupliziert. **Er braucht keine Outbox,
  nur eine Weckung.** Das einzige Loch ist die fehlende Weckung im terminalen/verlorenen-Selbst-Weckung-Fall.
- Die **Reaktion** hat **kein** Refold-aus-Ziel. Sie liest einen Quell-Stream und emittiert; geht der
  Send verloren, gibt es nichts, das ihn rekonstruiert — außer einer erneuten Verarbeitung desselben
  Quell-Events. Dafür braucht sie einen **durablen Checkpoint + eine Outbox**.

→ **Prozess = Backstop (Weckung). Reaktion = Outbox (durable Absicht).** Zwei Mechanismen, ein Prinzip
(„advance on durability, not on turn-completion").

---

## 3. Mechanismus 1 — Prozess-Backstop (keine Outbox)

Der Manager appended bereits `ProzessGestartet` und `ProzessBeendet` (`ProzessManager.cs:51,75,88`). Ein
Prozess ist **offen**, solange `ProzessGestartet` ohne `ProzessBeendet` im Korrelations-Log steht.

**Der Backstop:** ein periodischer, durabler Scan, der jeden offenen Prozess direkt weckt
(`ProzessManagerActor` per Korrelation). Der Manager faltet neu und:
- verlorener Vorwärts-Send → Transition noch pending → feuert erneut (deterministische Id, Empfänger dedupliziert);
- terminaler Schritt, dessen Ergebnis schon im Ziel-Stream steht, aber die Selbst-Weckung verloren ging
  → Fold sieht alle Transitionen erledigt → appended `ProzessBeendet`.

Damit heilt der Backstop **Terminal-Hang UND verlorenen Send**, ausschließlich aus dem Log, das ohnehin
existiert — **ohne Feuer-Append, ohne Invarianten-Bruch** („Log = nur Entscheidungen" bleibt).

**Offene Entscheidung 3.1 — Aufzählung der offenen Prozesse:** (a) Marten-Query über die Manager-Streams
nach `ProzessGestartet`-Events ohne `ProzessBeendet` (event-typ-gefiltert), ODER (b) ein durabler
Offen-Index (beim Gestartet setzen, beim Beendet löschen). (a) ist read-lastig aber zustandsfrei; (b) ist
ein Extra-Write, aber O(offen) statt O(Historie). Empfehlung: (b), weil der Scan sonst mit der Gesamtzahl
je gelaufener Prozesse wächst.

**Konsequenz:** der bestehende Prozess-Poll-Cursor (`ProzessManagerStartupService.cs:131`, Review L7) ist
danach nur noch eine **Latenz-Optimierung**, keine Korrektheits-Abhängigkeit — der Backstop trägt die Liveness.

---

## 4. Mechanismus 2 — Reaktions-Outbox (mit vorgeschaltetem Checkpoint)

### 4.1 Vorbedingung: Reaktionen bekommen einen durablen Checkpoint

Heute: Reaktion `tracker=null`, Fortschritt via out-of-band Poll-Cursor (nicht co-commit-fähig). v2
gibt der Reaktion einen **echten `IProjectionTracker`** — dasselbe Co-Commit-Muster wie die Projektionen
(`Domain.Infrastructure/…Store.cs`: Effekt + `ProjectionCheckpoint` in EINER `IdentitySession`/`SaveChanges`).

### 4.2 Die Outbox reitet auf diesem Co-Commit

Beim Verarbeiten eines Quell-Events schreibt die Reaktion den **Outbox-Eintrag** (der zu sendende Command)
in **dieselbe** `IdentitySession` wie den Checkpoint-Vorschub → ein `SaveChanges`, atomar. Kein
`AppendEventsAsync`-Seam nötig (Review L3): der Co-Commit läuft über die Marten-Dokument-Session des Trackers.

### 4.3 Outbox-Eintrag

```
OutboxEintrag {
    Id            : Guid   // = deterministische ReaktionsId (Abstractions/ReaktionsId.cs) = gesendete CommandId
    ZielAggregat  : string // via GeneratedCommandRouting.CommandToAggregate (Wurzel-2-Ergebnis)
    ZielId        : Guid
    Command       : <payload>
    Korrelation   : string
    Status        : Pending | Done | Dead
    Versuche      : int
    LetzterFehler : string?
}
```

**Pflicht-Companion (Review L8): `ReaktionsId` um einen Ziel-/Fan-out-Diskriminator erweitern**
(`ReaktionsId.cs:21` hat heute keinen) — sonst kollidieren zwei Reaktions-Commands aus einem Quell-Event
auf derselben Id. Analog zur bereits bekannten Prozess-Fan-out-Diskriminator-Lücke.

### 4.4 Relay + DLQ

Ein Relay (per-Node Hosted-Service ODER per-Ziel-Actor, Entscheidung offen) liest `Pending`, sendet mit
**bounded** `RequestAsync`, → `Done` bei Quittung, `Versuche++` bei Timeout, `Dead` (DLQ) bei Erschöpfung.
`Dead` ist beobachtbar + replay-bar. Verlorene `Done`-Markierung ist folgenlos (Empfänger dedupliziert).

### 4.5 Reaktive IEvent-Ausgaben (Review L5) — explizit als verlierbar klassifiziert

`HandlerOutputRouter` publiziert reaktive `IEvent`-Ausgaben nur an den Broker (`HandlerOutputRouter.cs:54-75`),
nie persistiert. v2-Entscheidung: **diese Ausgaben sind verlierbar (schneller Kanal, Invariante 6)** — die
durable Garantie gilt NUR für Commands. Rückt der Checkpoint über ein reaktives Event vor, ist das bewusst.
**Falls je ein durabler Konsument von einem reaktiven Event abhängt**, muss dieses Event persistiert werden
(dann fällt es unter dieselbe Outbox-Behandlung) — bis dahin: bewusst losable. (Offene Entscheidung 4.5: ist
diese Klassifikation für ALLE heutigen reaktiven Events korrekt?)

---

## 5. Mechanismus 3 (minimal) — Pipeline: DLQ statt stillem Drop

Pipeline-Commands sind OCC (`ExpectedVersion>=0`) ohne deterministische Id; die Empfänger-Inbox
dedupliziert sie NICHT (`AggregateActorBase.cs:186`, Review L4). Ein at-least-once-Retry-Outbox wäre
darum **unsicher** (verlorene Quittung → Doppel-Effekt).

**v2-Minimalfix:** der bisher stille Drop nach 3 Retries (`PipelineActorBase.cs:279`) wird ein
**DLQ-Eintrag** (beobachtbar), **kein** Retry-Outbox. Damit verschwindet der stille Verlust, ohne eine
Exactly-once-Garantie vorzutäuschen, die der OCC-Pfad nicht hergibt.

**Vollausbau (später, eigene Entscheidung):** Pipeline-Commands deterministische Ids geben und über den
Inbox-Dedup-Pfad routen — dann passen sie in dieselbe Reaktions-Outbox. Größerer Eingriff, hier nur benannt.

---

## 6. Symptom → Mechanismus (v2)

| Audit-Symptom | Mechanismus in v2 |
|---|---|
| Terminal-Hang (Prozess) nach Redeploy | §3 Backstop (offene-Prozesse-Scan + Refold) |
| Verlorener Vorwärts-Send (Prozess) | §3 Refold feuert erneut (deterministische Id) |
| Verlorene Reaktion (tracker=null) | §4 Reaktions-Checkpoint + Outbox + Relay |
| Pipeline: stiller Drop nach 3 Retries | §5 DLQ-Eintrag |
| WakeAck bestätigt „Turn" statt „zugestellt" (Reaktion) | §4.2 Vorrücken auf Co-Commit-Durabilität |
| Stille detached Exceptions | §4.4/§5 `LetzterFehler` + `Dead` beobachtbar |
| Prozess-Poll-Cursor lügt (L7) | §3 macht ihn zur reinen Optimierung |
| Fan-out-Id-Kollision (L8) | §4.3 Ziel-Diskriminator in `ReaktionsId` |
| Reaktive IEvent-Ausgaben nicht durabel (L5) | §4.5 explizit als verlierbar klassifiziert |

---

## 7. Was v2 weiterhin NICHT löst (Scope-Grenzen)

- **Gesicht B / Read-Seite** (Poll-Sequenz-Lücke, Voll-Scan, globaler Freeze): separates Inkrement
  (per-Stream/per-Konsument-Frontier statt skalarem `MartenPollCursorStore`-Cursor).
- **Consumer-Poison** (unverarbeitbares Eingangs-Event): eingangsseitige DLQ, getrennt von der Outbox.
- **Cross-Node** (Serialisierung des internen Plane): bewusst außerhalb.
- **Redis-Entkopplung** (`AggregateActorBase` awaited Redis synchron): eigener Quick-Fix.
- **Pipeline-Exactly-once-Retry:** erst mit deterministischen Ids (§5 Vollausbau).

---

## 8. Invarianten-Abgleich

1. **Wahrheit ist der Log** — ✅ Prozess-Backstop nutzt NUR das Entscheidungs-Log (kein Feuer-Append → die
   „nur Entscheidungen, Marking gefaltet"-Invariante bleibt intakt, im Gegensatz zu v1). Reaktions-Outbox
   ist co-committet mit dem Reaktions-Checkpoint.
2. **Signal ist nur Weckruf** — ✅ Backstop + Relay sind die durablen Rückfälle darunter.
3. **Routing über Typen** — ✅ via `GeneratedCommandRouting.CommandToAggregate`.
4. **Keine Runtime-Reflection** — ✅.
5. **Fachcode bleibt rein** — ✅ Backstop/Outbox im Framework; deterministische Ids aus bestehendem Mechanismus.
6. **Persistent genau dann, wenn ein durabler Konsument abhängt** — ✅ nur Commands (must-happen) in Outbox;
   reaktive Events explizit als verlierbar klassifiziert (§4.5) statt implizit.

---

## 9. Offene Entscheidungen (für die Review)

- 3.1 Offene-Prozesse-Aufzählung: Query vs. durabler Offen-Index.
- 4.4 Relay-Topologie: per-Node vs. per-Ziel-Actor.
- 4.5 Sind ALLE heutigen reaktiven `IEvent`-Ausgaben wirklich verlierbar? (Einzeln prüfen.)
- 5 Wann lohnt der Pipeline-Vollausbau (deterministische Ids)?
- Kompaktierung: `Done`-Einträge löschen, `Dead` behalten — Wachstum bounded?

---

## 10. Verifikation / Akzeptanzkriterien (Crash-Proben, Stil `Infrastructure.Pruefstand.Tests`)

- **P1 Prozess-Terminal-Neustart:** letzte Transition feuert, Ziel committet, „Neustart" vor Selbst-Weckung
  → Backstop weckt offenen Prozess → `ProzessBeendet` genau einmal, kein Doppel-Effekt.
- **P2 Prozess-verlorener-Vorwärts-Send:** Send verpufft → nächste Weckung refold → erneut gefeuert →
  Empfänger dedupliziert → genau ein Effekt.
- **P3 Reaktions-Outbox-Atomarität:** Absturz zwischen Effekt/Checkpoint-Co-Commit und Zustellung → Relay
  heilt; Checkpoint korrekt; genau ein Effekt (Empfänger-Dedup).
- **P4 Fan-out-Id-Eindeutigkeit:** zwei Reaktions-Commands aus einem Quell-Event → zwei verschiedene
  Outbox-Ids → beide wirken (kein Verpuffen des zweiten).
- **P5 Pipeline-DLQ:** Command scheitert N-mal → `Dead`-Eintrag beobachtbar, kein stiller Drop, kein Retry-Doppel.
- **P6 Poison-Isolation:** ein dauerhaft unzustellbarer Reaktions-Send blockiert andere nicht (kein Freeze).

In-memory gegen Fake-Cluster (Memory `hang-diagnose-in-memory`); Co-Commit-Atomarität zusätzlich gegen echtes Marten.

---

## 11. Für die prüfende Instanz — Checkliste

- [ ] **§3 Backstop-Vollständigkeit:** Gibt es einen offenen Prozess-Zustand, den weder „Gestartet ohne
      Beendet" noch der Offen-Index sichtbar macht? (v1-Loch L6 wirklich geschlossen?)
- [ ] **§3 Refold heilt terminal:** Sieht der Fold nach erfolgreichem terminalem Send WIRKLICH „alle
      Transitionen erledigt" und appended `ProzessBeendet`? (Am `ProzessManager.WakeAsync`-Fold prüfen.)
- [ ] **§4.1 Reaktions-Checkpoint:** Ist das Einführen eines `IProjectionTracker` für Reaktionen mit dem
      generischen Pull-Adapter (`ProjectionAdapter`/`PullPath`) verträglich, ohne die Push-/Signal-Wege zu brechen?
- [ ] **§4.2 Co-Commit:** Kann die Marten-`IdentitySession` des Trackers Effekt + Checkpoint + Outbox-Dokument
      in EINEM `SaveChanges`? (Vorbild: `Domain.Infrastructure/…Store.cs`.)
- [ ] **§4.3 ReaktionsId-Diskriminator:** Aus welchen Eingaben wird er gebaut, und sind die bei Recovery stabil?
- [ ] **§4.5 Verlierbar-Klassifikation:** Hängt HEUTE ein durabler Konsument an einer reaktiven `IEvent`-Ausgabe?
- [ ] **§5 Pipeline:** Ist „nur DLQ, kein Retry" wirklich sicher, oder braucht auch der einmalige Send eine
      Idempotenz-Absicherung gegen den Ablauf „Effekt angewandt, Ack verloren, Turn wirft"?
- [ ] **Invariante 6:** Wandert im v2-Design versehentlich Verlierbares in die Outbox?
- [ ] **Kein neuer Hang:** jeder `RequestAsync` in Backstop/Relay bounded (nicht `CancellationToken.None`)?

---

## 12. Verworfen (aus v1, mit Grund)

- **Universelle Feuer-Outbox im Prozess-Manager:** bricht „Log = nur Entscheidungen" und hat keinen
  Append-Zeitpunkt (Review L1). Ersetzt durch den Backstop, der das vorhandene Refold nutzt.
- **Reaktions-Outbox ohne Checkpoint:** es gibt keine co-commit-fähige Marke (Review L2). v2 führt den
  Checkpoint zuerst ein.
- **Pipeline im at-least-once-Retry-Outbox:** OCC ohne Dedup → Doppel-Effekt (Review L4). Ersetzt durch DLQ.
