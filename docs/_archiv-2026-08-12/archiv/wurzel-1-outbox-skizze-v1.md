# Wurzel 1 — Durable Outbox + effekt-bestätigter Fortschritt (Skizze zur Prüfung)

> **Status:** Design-Skizze, NICHT implementiert. Zweck: von einem zweiten Agenten/Menschen
> gegen den Code und die sechs Invarianten geprüft zu werden, bevor gebaut wird.
> **Scope:** Ein-Knoten (bewusste Entscheidung — Cross-Node ist außerhalb). Die relevante
> Bruchstelle ist der **Prozess-Neustart/Redeploy**, nicht Multi-Node.
> **Herkunft:** Wurzel-Analyse aus dem Backend-Audit (die zweite Wurzel — grobe Typ-Metadaten —
> ist bereits behoben: `docs/…` / Memory `command-event-map-in-decider-signaturen`).

---

## 1. Das Problem (Wurzel 1), präzise

Das Framework ruht auf einer Erlösungs-Idee: *„die Wahrheit ist der Log — Verlorenes leite ich
durch erneutes Lesen neu ab."* Diese Idee hat drei stillschweigende Vorbedingungen, die der Code
verletzt:

1. Es muss immer einen **künftigen Auslöser** geben, der die Neu-Ableitung anstößt.
2. Die **Marke**, die die Neu-Ableitung freischaltet, darf nie über unbestätigte Arbeit hinaus vorrücken.
3. Die Neu-Ableitung muss **kostenbeschränkt** sein.

Die gemeinsame Ursache: es fehlt eine **durable Aufzeichnung von „Absicht, die noch nicht bestätigt
erledigt ist"**. „Erledigt" wird aus dem **Kontrollfluss** erschlossen (Turn gelaufen), nicht aus
**bestätigter Wirkung**.

### 1.1 Zwei Gesichter derselben Wurzel

**Gesicht A — Ausgehende Wirkung geht verloren (fire-and-forget ohne Nachweis).**
- `Infrastructure/Prozess/DetachedProzessSend.cs` — der Prozess-Send ist fire-and-forget; das Ergebnis
  wird verworfen. Das Ergebnis-Event der LETZTEN Transition triggert keine Regel → nur eine
  **in-process Selbst-Weckung** erkennt „terminal". Stirbt der Knoten zwischen Ziel-Commit und
  Selbst-Weckung (Redeploy!), hängt der Prozess für immer im Nicht-Terminal-Zustand — alle Effekte da,
  aber kein `ProzessBeendet`.
- `Infrastructure/PubSub/DetachedEmit.cs` + `Infrastructure/PubSub/HandlerOutputRouter.cs` — die Reaktion
  (tracker=null) emittiert fire-and-forget; schlägt der Send fehl, ist nichts durabel, das ihn nachhält.
- `Infrastructure/Pipeline/PipelineActorBase.cs` (`MaxRetries = 3`) — nach 3 OCC-Konflikten wird der
  Command nur geloggt und **still fallengelassen**; kein Dead-Letter.
- `Infrastructure/Projections/ProjectionAdapter.cs` + `SignalAdapterActor.cs` — der `WakeAck`
  bestätigt „Turn gelaufen", NICHT „zugestellt". `Infrastructure/Projections/Poller.cs` rückt die HWM
  darauf vor → die Marke lügt.

**Gesicht B — Konsumenten-Fortschritt (Read-Seite) rückt über Unbestätigtes vor.**
- `Infrastructure/Persistence/MartenEventStore.cs` (`ReadChangedStreamsAsync`) + `MartenPollCursorStore.cs`
  — ein **skalarer globaler HWM-Cursor**. Er kann „Sequenz 11 erledigt, 10 noch offen" nicht ausdrücken
  (Marten vergibt `seq_id` beim INSERT *innerhalb* der Transaktion → eine spät committende Tx mit
  niedrigerer Sequenz wird nie wieder gescannt), er muss zum Finden geänderter Streams alles hinter dem
  Cursor scannen+deserialisieren, und ein einziger unbestätigter Stream friert ihn global ein.

**Diese Skizze adressiert primär Gesicht A** (die durable Outbox). Gesicht B ist dieselbe Wurzel,
aber ein **separates Design-Inkrement** (per-Konsument/per-Stream-Frontier statt skalarer Cursor) —
siehe §7. Bewusst getrennt, damit die Skizze kohärent und prüfbar bleibt.

---

## 2. Das Prinzip

**Co-commit the intent, deliver asynchronously, advance on durability.**

1. Wenn ein Aktor/Adapter eine **ausgehende Wirkung** erzeugt (ein Command an ein Fremd-Aggregat),
   schreibt er einen **Outbox-Eintrag** („dieser Send ist offen") in **derselben** Transaktion wie die
   Zustandsänderung, die ihn verursacht hat.
2. Der Fortschritts-Marker (Checkpoint/HWM) rückt vor, sobald der Effekt **oder sein durabler
   Outbox-Eintrag committet** ist — **nicht** wenn der Send zugestellt ist. Durabilität, nicht
   Zustellung, macht die Neu-Ableitung unnötig.
3. Ein separater **Relay** stellt offene Outbox-Einträge zu, mit Retry, und markiert sie `Done` bei
   bestätigter Quittung bzw. `Dead` (DLQ) bei Erschöpfung/dauerhaftem Fehler.

Kernpunkt: **Die Outbox ist keine neue Subsystem-Erfindung — sie ist der Spiegel der bereits
existierenden Inbox.**

---

## 3. Was schon da ist und wiederverwendet wird (die Inbox als Spiegel)

Der Schreibpfad hat bereits **exakt dieses Muster auf der Eingangsseite**:

- `Infrastructure/Aggregate/ActorSystem/AggregateActorBase.cs` co-committet auf dem AnyVersion-Pfad
  eine Marke `KommandoVerarbeitet(CommandId)` MIT den Domänen-Events in **einer** `AppendEventsAsync`
  (`Infrastructure/Persistence/MartenEventStore.cs`) → exactly-once-wirksam. Ein wiederholter Command
  gleicher `CommandId` verpufft (`Infrastructure/Aggregate/KommandoVerarbeitet.cs`, `IProzessIntern`).
- Die Projektions-Naht co-committet Effekt + `ProjectionCheckpoint` in EINER Marten-`IdentitySession`
  (`Abstractions/IProjectionTracker.cs`, `Abstractions/ProjectionCheckpoint.cs`,
  `Infrastructure/Persistence/MartenProjectionTracker.cs`, Domänen-Co-Commit-Stores in
  `Domain.Infrastructure/`).

Die **Inbox** macht Zustellung idempotent beim EMPFÄNGER. Die **Outbox** macht Absicht durabel beim
SENDER. Zusammen: at-least-once-Zustellung (Outbox) × Empfänger-Dedup (Inbox) = **exactly-once-wirksam
end-to-end** — dieselbe „der Nahtpunkt garantiert, die Store-Impl entscheidet"-Philosophie.

---

## 4. Design

### 4.1 Der Outbox-Eintrag

```
OutboxEintrag {
    Id            : Guid      // DETERMINISTISCH = der Vorgang (ProzessId.FürTransition / deterministische CommandId)
    ZielAggregat  : string    // AggregateType (aus GeneratedCommandRouting.CommandToAggregate)
    ZielId        : Guid      // AggregateId
    Command       : <payload> // der zu sendende Command (serialisiert wie Events)
    Korrelation   : string    // CorrelationId (Prozess-/Reaktions-Kontext)
    Status        : Pending | Done | Dead
    Versuche      : int
    LetzterFehler : string?
    ErstelltSeq   : long      // globale Sequenz beim Co-Commit (Reihenfolge des Relays)
}
```

Die **Id ist deterministisch** (der schon existierende `Abstractions/ProzessId.cs`-Vorgang). Damit ist
ein wiederholter Send beim Empfänger via Inbox ein Noop, und ein doppelt geschriebener Outbox-Eintrag
(Recovery) kollidiert auf derselben Id statt einen zweiten Effekt zu erzeugen.

### 4.2 Wo Einträge geschrieben werden (immer Co-Commit)

- **Prozess-Manager** (`Infrastructure/Prozess/ProzessManager.cs`): Wenn er eine Transition feuert,
  co-committet er den Outbox-Eintrag MIT der Entscheidungs-Aufzeichnung (ProzessGestartet/SchrittErledigt)
  in **derselben** Marten-Transaktion (Marten kann Events + Dokumente in einem `SaveChanges`).
  **Ersetzt** `DetachedProzessSend` fire-and-forget.
- **Reaktion** (`Infrastructure/PubSub/HandlerOutputRouter.cs`): Die Reaktion läuft auf demselben
  Pull-Adapter wie Projektionen. Der Outbox-Eintrag wird MIT dem `ProjectionCheckpoint`-Vorrücken
  co-committet (`IProjectionTracker.MarkProcessedAsync`). **Ersetzt** `DetachedEmit` fire-and-forget +
  tracker=null.
- **Pipeline** (`Infrastructure/Pipeline/PipelineActorBase.cs`): Der bisher still gedroppte Command nach
  3 Retries wird ein Outbox-Eintrag statt eines `LogError` → landet bei Erschöpfung in der DLQ, nie im Nichts.

Ausdrücklich **nicht** in der Outbox: Client-Antworten, Ablehnungen, UI-Feedback, Ticks (verlierbar,
Invariante 6 — die bleiben auf dem schnellen Kanal).

### 4.3 Der Relay

Ein Zustell-Treiber (per-Node Hosted-Service ODER per-Ziel-Stream Cluster-Actor — Entscheidung offen, §9):

1. Liest `Pending`-Einträge in `ErstelltSeq`-Reihenfolge.
2. Sendet via `cluster.RequestAsync<CommandResult>` mit **bounded** Token (nicht `CancellationToken.None`
   — das war eine der Hang-Ursachen).
3. Erfolgs-Quittung → `Done`.
4. Timeout/technischer Fehler → `Versuche++`, `LetzterFehler`, bleibt `Pending` (Backoff).
5. Erschöpfung (`Versuche >= N`) ODER klassifiziert-dauerhafte Ablehnung → `Dead` (DLQ).

Weil die Empfänger-Inbox dedupliziert, ist eine verlorene `Done`-Markierung folgenlos (der nächste
Relay-Lauf sendet erneut → Empfänger-Noop → dann `Done`). At-least-once genügt.

### 4.4 DLQ = der `Dead`-Zustand (kein separater Kasten)

Die DLQ ist **keine vierte Sache**, sondern der Terminal-Zustand der Outbox. Ohne ihn hätte der Relay am
Ende des Retry-Budgets nur zwei schlechte Optionen: ewig weiter-retrien (Poison blockiert) oder still
droppen (heutiges Verhalten). `Dead` gibt den dritten Eimer: **geparkt, sichtbar, wieder-abspielbar**
(Replay ist im Framework first-class). Ein `Dead`-Eintrag kann selbst eine Monitoring-Reaktion triggern.

### 4.5 Fortschritts-Vorrücken — auf Durabilität, nicht Zustellung

Der `WakeAck`/Checkpoint darf über Version V vorrücken, sobald **jeder** Effekt bis V entweder durabel
angewandt ODER als durabler Outbox-Eintrag co-committet ist. Das ist der Fix für „WakeAck lügt": nicht
„erst nach Zustellung bestätigen", sondern „vor dem Turn-Ende ist die Absicht durabel" — der Relay
erledigt die Zustellung entkoppelt. Kein Hang (Eintrag überlebt Neustart), kein Doppel-Effekt (Empfänger
dedupliziert), kein stiller Verlust (DLQ fängt Erschöpfung).

### 4.6 Terminal-Erkennung ohne in-process Selbst-Weckung

Die heutige terminale Erkennung hängt an einer flüchtigen in-process Selbst-Weckung. Ersatz: ein
**periodischer „offene-Arbeit"-Backstop**, der Prozesse mit `ProzessGestartet` ohne `ProzessBeendet`
(bzw. mit offenen Outbox-Einträgen) direkt weckt. Die Outbox liefert die **präzise Menge offener Arbeit**
— genau der durable Ersatz für die verlorene Selbst-Weckung. (Ersetzt das Raten via teilnehmender
Ziel-Events, das für terminale Streams strukturell nicht greift.)

---

## 5. Symptom → Mechanismus

| Audit-Symptom | Löst die Outbox? | Mechanismus |
|---|---|---|
| Terminal-Hang (Prozess) nach Redeploy | ✅ | Send ist durabel; „offene-Arbeit"-Backstop weckt neu; Empfänger dedupliziert |
| Verlorene Reaktion (tracker=null) | ✅ | Outbox-Eintrag co-committet mit Checkpoint; Relay stellt zu |
| Pipeline: stiller Drop nach 3 Retries | ✅ | Eintrag → Relay-Retry → DLQ statt `LogError` |
| WakeAck bestätigt „Turn" statt „zugestellt" | ✅ | Vorrücken auf Durabilität des Eintrags, nicht Zustellung |
| Stille detached Exceptions (`Console.WriteLine`) | ✅ | `LetzterFehler` + `Dead`-Einträge sind beobachtbar |
| Ein kranker Stream friert HWM (Sende-Poison) | ✅ (teilw.) | Poison-Send → `Dead`, blockiert nicht mehr; Read-Seite-Freeze siehe §7 |
| Redis synchron im Schreib-Turn | ➖ | Verwandt (invertierte Klassifikation), aber eigener Quick-Fix: detached feuern |

---

## 6. Was das NICHT löst (Scope-Grenzen — bitte prüfen, ob korrekt abgegrenzt)

- **Read-Seite / Gesicht B** (Poll-Sequenz-Lücke, Voll-Scan, globaler Freeze): braucht per-Stream/
  per-Konsument-Frontier statt skalarem `MartenPollCursorStore`-Cursor. **Separates Inkrement.** Die
  Outbox ist die AUSGANGS-Ledger; die Konsumenten-Frontier ist die EINGANGS-Ledger — nicht verwechseln.
- **Consumer-Poison** (ein Event, das eine Projektion konstant beim Dispatch wirft): braucht eine
  **eingangsseitige** DLQ (Poison-Event nach N Versuchen skippen + festhalten) — analog, aber getrennt
  von der Outbox (die nur unzustellbare AUSGEHENDE Commands hält).
- **Cross-Node** (fehlende Serialisierung des internen Plane): bewusst außerhalb (Ein-Knoten).
- **Redis-Entkopplung**: eigener kleiner Fix (fire-and-forget statt awaited), nicht Teil der Outbox.

---

## 7. Invarianten-Abgleich (die sechs)

1. **Wahrheit ist der Log** — ✅ Outbox-Einträge sind co-committet mit der verursachenden Aufzeichnung;
   optional sogar als Log-Events (`IProzessIntern`, §9). Die Zustellung leitet sich aus durabler Absicht ab.
2. **Signal ist nur Weckruf** — ✅ unverändert; die Outbox ist der durable Backstop unter dem Signal.
3. **Routing über Typen** — ✅ Ziel-Auflösung via `GeneratedCommandRouting.CommandToAggregate` (Wurzel-2-Ergebnis).
4. **Keine Runtime-Reflection** — ✅ nichts Neues; Serialisierung wie bei Events.
5. **Fachcode bleibt rein** — ✅ Outbox lebt im Framework (Manager/Adapter), nie im Domänen-Code; die
   deterministische Id kommt aus dem bestehenden Vorgang-Mechanismus.
6. **Persistent genau dann, wenn ein durabler Konsument abhängt** — ✅ nur Must-happen-Sends gehen in die
   Outbox; Verlierbares (UI/Tick/Ablehnung) bleibt fire-and-forget.

---

## 8. Offene Entscheidungen (für die Review)

1. **Speicherform der Outbox:** (a) mutierbares Marten-Dokument (Status Pending→Done, einfach zu
   queryen) **oder** (b) append-only Log-Events (`IProzessIntern`-Marken, maximal invariant-1-treu, aber
   Status per Faltung). Empfehlung der Skizze: (a) — im selben `SaveChanges` wie der Effekt.
2. **Relay-Topologie:** per-Node Hosted-Service (einfacher, ein Scanner) vs. per-Ziel-Stream Cluster-Actor
   (natürliche Serialisierung, mehr Teile).
3. **`Done`-Markierung:** einfaches Update (verlorenes Update = harmloser Re-Send) vs. Co-Commit mit dem
   Konsum der Quittung. Skizze: einfaches Update genügt (Empfänger-Inbox macht Re-Send folgenlos).
4. **Backpressure/Kompaktierung:** `Done`-Einträge periodisch löschen; `Dead` behalten. Wachstum bounded?
5. **„Offene-Arbeit"-Backstop-Intervall** und ob er die Prozess-Manager direkt oder über die Outbox weckt.

---

## 9. Verifikation / Akzeptanzkriterien (Crash-Proben, Stil `Infrastructure.Pruefstand.Tests`)

Die bestehenden vier Crash-Proben (verlorenes Signal / doppeltes Signal / Effekt+Marke atomar / Absturz
zwischen Effekt und Marke) sind die Vorlage. Neu, für die Outbox:

- **P1 Terminal-Neustart:** Prozess feuert letzte Transition, Ziel committet, dann „Knoten-Neustart"
  (Actor-Neuaufbau) VOR der Selbst-Weckung → nach Recovery erreicht der Prozess `ProzessBeendet`,
  genau einmal, kein Doppel-Effekt.
- **P2 Verlorener Reaktions-Send auf terminalem Stream:** Send schlägt N-mal fehl → Eintrag `Dead`,
  Checkpoint ist trotzdem vorgerückt, keine Doppelwirkung; `Dead` beobachtbar.
- **P3 Idempotenz-Kette:** derselbe Outbox-Eintrag zweimal zugestellt (Recovery) → Empfänger-Inbox
  dedupliziert → genau ein Effekt.
- **P4 Poison-Isolation:** ein dauerhaft unzustellbarer Send blockiert die Zustellung anderer Streams
  nicht (kein globaler Freeze).
- **P5 Fortschritt-auf-Durabilität:** Absturz zwischen Outbox-Co-Commit und Zustellung → Relay heilt,
  Checkpoint bleibt korrekt (kein übersprungenes/doppeltes Event).

Alle in-memory gegen Fake-Cluster beweisbar (siehe Memory `hang-diagnose-in-memory` — verteilte Hangs
NICHT im langsamen Integrationstest jagen). Store-Semantik (Co-Commit-Atomarität) zusätzlich gegen echtes
Marten (Ebene 2).

---

## 10. Für die prüfende Instanz — Checkliste

Bitte diese Behauptungen gegen den Code verifizieren (nicht annehmen):

- [ ] **Co-Commit ist technisch möglich:** kann `MartenEventStore.AppendEventsAsync` in EINER Marten-
      Transaktion Events UND ein Outbox-Dokument schreiben? (Marten: Events + Docs in einem `SaveChanges`.)
- [ ] **Die deterministische Vorgang-Id** (`Abstractions/ProzessId.cs`) ist bei Recovery WIRKLICH identisch,
      sonst bricht die Idempotenz-Kette (P3).
- [ ] **Empfänger-Inbox greift für Outbox-Re-Sends:** `AggregateActorBase` dedupliziert per `CommandId` —
      stimmt die Outbox-Id = die gesendete `CommandId`?
- [ ] **Vorrücken-auf-Durabilität ist sicher:** gibt es einen Effekt-Typ, der weder durabel angewandt noch
      als Outbox-Eintrag erfasst wird und über den der Checkpoint trotzdem vorrückt? (Wäre ein Loch.)
- [ ] **Scope-Grenze §6 korrekt:** löst die Outbox WIRKLICH nicht die Read-Seite (Sequenz-Lücke)? Oder
      überlappt sie doch? (Ausgangs- vs. Eingangs-Ledger sauber getrennt?)
- [ ] **Invariante 6:** wandert versehentlich etwas Verlierbares (Ablehnung/UI/Tick) in die Outbox?
- [ ] **Ordnung:** braucht der Relay strikte per-Ziel-Reihenfolge, oder ist die Empfänger-Version/Inbox
      reihenfolge-tolerant? (Beeinflusst §8.2.)
- [ ] **Kein neuer Hang:** ist jeder `RequestAsync` im Relay bounded (nicht `CancellationToken.None`)?

---

## 11. Verworfene Alternativen (kurz)

- **„WakeAck erst nach Zustellung":** macht den Adapter-Turn synchron abhängig vom Fremd-Aggregat →
  reintroduziert genau den Hang, den die Detached-Sends vermeiden sollten. Die Outbox entkoppelt statt zu blocken.
- **Nur DLQ ohne Outbox:** fängt den Terminal-Hang NICHT — ein nie-beobachteter, verlorener Send erzeugt
  nie eine „gescheitert"-Beobachtung, also auch keinen DLQ-Eintrag. Die Outbox muss die Absicht ZUERST
  durabel kennen.
- **Größeres Retry-Budget in der Pipeline:** verschiebt den stillen Drop nur, beseitigt ihn nicht.
