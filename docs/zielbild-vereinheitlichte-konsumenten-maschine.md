# Zielbild: Die vereinheitlichte Konsumenten-Maschine (v2)

> Status: **Entwurf / Zielbild** (nicht umgesetzt). v2 nach adversarialem Eleganz-Review +
> code-verifiziertem Feature-Inventar (2026-08-08). v1 überverkaufte den Prozess-Teil; v2 schneidet
> die Rollen so, dass sie tatsächlich tragen.
> Hält an den vier Säulen fest: **Proto.Actor, Source-Generatoren, Marten/PostgreSQL, Redis**.
> Kein Backwards-Compat-Zwang — der Umbau darf Altlasten (Sentinel, tote Pfade) ersatzlos löschen.

## 0. Zweck

Das Backend ist organisch gewachsen und dabei an einer Stelle *auseinander*gewachsen: **ein Primitiv
wurde an vier Stellen unterschiedlich sicher nachgebaut.** Dieses Dokument benennt das Primitiv, schneidet
die Rollen sauber und zeigt, welche Teile *wirklich* zu einer Maschine verschmelzen — und welche bewusst
getrennt bleiben. Kein Neubau: das Rückgrat (Log = Wahrheit, Pull-vom-Cursor, co-committete Marke) ist
richtig und bewiesen.

**Die zentrale v2-Ehrlichkeit:** „eine Maschine, keine Taxonomie" gilt als *Leitidee*, nicht als *ein Kind
mit einem Rückgabetyp*. Die Ausprägungen fallen aus **zwei orthogonalen Achsen** — nicht aus einer flachen
Parametrisierung. Wer „ein Kind" wörtlich baut, verzweigt intern doch wieder nach Rolle.

## 1. Die Diagnose in einem Satz

> Es gibt genau **zwei** Primitive — einen *Schreiber in den Log* (P1) und einen *durablen Konsumenten,
> der ab Cursor liest, faltet, emittiert und seine Marke fortschreibt* (P2). Projektion, Reaktion,
> Pipeline-Event-Pfad und Prozess sind P2 — aber sie unterscheiden sich in **zwei Achsen**
> (Quell-Topologie, Effekt-Klasse), die man nicht wegparametrisieren kann. Und „Command emittieren"
> existiert heute vierfach mit vier Sicherheitsniveaus.

Konkrete Folgen (aus dem Audit, alle mit *gültigem* Anwendercode erreichbar):

- **W1** — die Pipeline sendet mit OCC + zufälliger CommandId → keine Empfänger-Dedup → Doppelanwendung
  bei verlorener/verspäteter Quittung.
- **W2** — die Pipeline sendet mit `CancellationToken.None` → unbounded Hang.
- **S15** — der Prozess erkennt Fortschritt aus dem Re-Fold; ein Noop mitten im Ablauf ist aufgelöst aber
  nicht wirksam → wenn man die Wirkung/Aufgelöst-Unterscheidung verliert, droht stiller Falsch-Erfolg.
- **Terminal-Hänger** — das Ergebnis-Event der letzten Transition triggert keine Regel; der Poll filtert
  es per Typ weg → ohne Selbst-Weckung/Backstop kein `ProzessBeendet`.

---

## 2. Das Modell: zwei Primitive

### P1 — Der Schreiber (`AggregateActorBase`)

Der **einzige** Ort, an dem ein Effekt durabel wird. Command → Decider → OCC-Append → Apply → optionale
Inbox-Marke → Signal. Bleibt im Kern unverändert. Zwei Idempotenz-Achsen:

- **OCC-Pfad** — der externe Client behauptet eine Version; die Version *ist* die Absicherung.
- **Idempotenz-Pfad** — interne Emitter behaupten keine Version; die **Empfänger-Inbox** dedupliziert über
  die deterministische CommandId (`KommandoVerarbeitet` co-committet mit den Domänen-Events).

### P2 — Der durable Konsument

Liest ab Cursor, faltet, produziert Ausgaben, schreibt seine Marke fort, emittiert dann Commands
(idempotent) und Signale (best-effort). Jede Weckung — Signal *oder* Poll — ist ein Schritt.

---

## 3. Die Rollen

Jede Rolle: **Was / Warum / Wie / Heute / Ziel**. (Rollen, die v1 falsch schnitt, sind mit **⟳ v2**
markiert.)

### Rolle 1 — Der Log (Event-Store / Marten). Wahrheit. `AppendEventsAsync`, `ReadStreamAsync`, `ReadChangedStreamsAsync`. **Unverändert.**

### Rolle 2 — Der Schreiber (`AggregateActorBase`, P1). Single-Writer pro Stream, OCC + Inbox + Version-pro-Event. **Kern unverändert**; er ist der Empfänger des Emit-Primitivs.

### Rolle 3 — Das Emit-Primitiv (`ICommandEmitter`) — ersetzt vier Emit-Pfade

- **Was:** Der *eine* Baustein „schicke ein Command an ein Aggregat, exactly-once-wirksam". Ersetzt
  `AggregateDispatcher`(interner Teil), `PipelineActorBase`, `ProzessManager`/`DetachedProzessSend`,
  `HandlerOutputRouter`.
- **Warum:** Idempotenz ist heute *nur am Empfänger* erzwungen und *nur* für Sender, die zufällig richtig
  senden. Die Pipeline tut es falsch → W1/W2. Ein Primitiv macht die Garantie strukturell.
- **⟳ v2 — Entwurfsaxiom:** **Interne Emitter behaupten NIE eine Version.** OCC (Compare-and-Swap gegen
  Version N) ist ausschließlich der *externe Client-Vertrag*. Die heutige Pipeline-OCC ist ein Unfall der
  Implementierung („serverseitiger Client"), keine fachliche Notwendigkeit — ein echtes
  Read-modify-write gegen eine bestimmte Version ist Sache des *Deciders*, nicht des Emits. Damit ist
  „ein Baustein" wirklich einer (kein OCC-Modus nötig).

  ```csharp
  namespace Abstractions;

  public interface ICommandEmitter
  {
      // Immer idempotent (deterministische CommandId aus Kausalität), bounded Token, at-least-once auf
      // dem Draht → exactly-once wirksam via Empfänger-Inbox. KEIN Versions-Argument.
      Task EmitAsync(ICommand cmd, EmitKausalität k, CancellationToken ct);
  }
  public readonly record struct EmitKausalität(Guid Korrelation, Guid Ursache, string Diskriminator);
  // CommandId = deterministisch(k, cmd.AggregateId). Zwei Weckungen derselben Ursache → gleiche Id → Noop.
  ```
- **Heute:** vierfach, uneinheitlich. **Ziel:** eine Implementierung; der externe Client behält OCC.

### Rolle 4 — Die Empfänger-Inbox (`KommandoVerarbeitet`). Trennt Zustellung (at-least-once) von Wirkung (exactly-once); hält die Aggregate rein. **Unverändert** — bekommt durch Rolle 3 automatisch die Pipeline als Nutzer (W1-Fix).

### Rolle 5 — Der durable Konsument, geschnitten in ZWEI Achsen — ⟳ v2

- **Was:** Die parameterisierte Lese-Falt-Emit-Schleife (heute `ProjectionAdapter`). **Aber:** sie ist
  *keine* flache Parametrisierung. Sie variiert entlang zweier **orthogonaler** Achsen:

  **Achse A — Quell-Topologie:**
  - **Ein-Strom** (Projektion, Reaktion, Pipeline-Event-Pfad): liest *einen* Stream ab `int`-Cursor.
  - **Korrelations-Multistrom** (Prozess): liest die *N* teilnehmenden Streams einer Korrelation; der
    „Cursor" ist eine `Map<Guid,int>` (bzw. der Manager-Log-Kopf) — eine andere Lese-Topologie, keine
    Zahl.

  **Achse B — Effekt-Klasse:**
  - **Replaybar** (Projektion): durabler Effekt = Read-Model; co-committet mit dem Cursor; **darf `Reset`/
    Rebuild** (idempotenter Wiederaufbau).
  - **Emittierend** (Reaktion, Pipeline-Event, Prozess): durabler Effekt = *keiner auf der Konsumentenseite*
    (der Effekt ist das emittierte Command, idempotent am Empfänger) bzw. beim Prozess = seine
    *Entscheidungs-Events* im eigenen Log. **Darf NIE `Reset`/blind-Replay** — Replay eines Emittenten
    bewegt echtes Geld (CLAUDE.md: „Command-yieldende Handler werden NICHT blind replayt").

- **⟳ v2 — Konsequenz für die Verträge:** Es gibt **eine Maschinen-Klasse**, aber **getrennte Kinds/Marken
  je Achsen-Kombination** — nicht *ein* `KonsumSchritt` für alles. Der Fortschritts-/Replay-Vertrag wird
  nach Effekt-Klasse getrennt:

  ```csharp
  // Replaybare Konsumenten (Projektion): Effekt + Cursor co-committet, Reset erlaubt.
  public interface IReplaybarerTracker   // = das heutige IProjectionTracker, inkl. Reset*
  { Task<int> LastProcessedVersionAsync(...); Task MarkProcessedAsync(...); Task ResetAsync(...); }

  // Emittierende Konsumenten (Reaktion/Pipeline/Prozess): NUR Cursor-Fortschritt, KEIN Reset.
  public interface IEmittentenCursor
  { Task<long> LadeAsync(string partition, CancellationToken ct);
    Task SchreibeAsync(string partition, long bis, CancellationToken ct); }
  ```

  `IProjectionTracker.Reset*` wird **niemals** an einen Emittenten gegeben — der Compile-Zeit-Schnitt
  verhindert das (zwei Interfaces statt eines).
- **Heute:** nur Ein-Strom/Replaybar (Projektion) + Ein-Strom/Emittierend (Reaktion) laufen auf dem
  Adapter. **Ziel:** alle Ausprägungen laufen auf *derselben Maschinen-Klasse*, aber mit dem zu ihrer
  Achsen-Kombination passenden Kind + Marken-Interface.

### Rolle 6 — Die Fortschrittsmarke. Zwei Interfaces (Rolle 5): `IReplaybarerTracker` (Co-Commit + Reset) vs. `IEmittentenCursor` (nur Fortschritt). Garantie am Store, Transport am Marker — zwei Achsen, jetzt auch im Typsystem getrennt.

### Rolle 7 — Der Transport (Signal + Poll). Zwei unabhängige Weckquellen; der per-Partition-Actor serialisiert beide. **⟳ v2 — kritischer Zusatz:** der Poll muss für Emittenten **per `CorrelationId` routen können**, nicht nur per `StreamId`/Typ (siehe §5).

### Rolle 8 — Der Korrelations-/Signal-Router. Übersetzt ein Event-Signal in eine Weckung des zuständigen Konsumenten (`StreamId` für Projektion, `Korrelation` für Prozess). **Unverändert im Prinzip**, aber trägt den §5-Terminal-Fix.

### Rolle 9 — Interne Serialisierung (K1) — ⟳ v2: EIGENER, ORTHOGONALER MEILENSTEIN

- **Was:** Ein generierter Serializer für die internen Cluster-Nachrichten (`CommandEnvelope`,
  `Wake`, `Publish`/`EventEnvelope`/`SignalEnvelope`, …).
- **⟳ v2:** K1 hat **nichts** mit der Konsumenten-Vereinheitlichung zu tun — man kann K1 ohne die eine
  Maschine fixen und die Maschine ohne K1 bauen. Die polymorphe Payload-Serialisierung ist der schwierige
  Teil (CLAUDE.md Phase 4c: „nicht-trivial"). v1s „Multi-node als Abfallprodukt" war zu optimistisch. →
  **Separater Meilenstein, nur nötig wenn Multi-node gebraucht wird** (siehe §9).

### Rolle 10 — Der Versions-/Deps-Index (Redis). Abgeleiteter, nicht-autoritativer Index. **Unverändert.** Leitplanke: nie auf die Emit- oder Cursor-Achse ziehen.

---

## 4. Wie die Ausprägungen aus den zwei Achsen fallen — ⟳ v2

| Ausprägung | Achse A (Quelle) | Achse B (Effekt) | Fold | Emit | Marke / Reset |
|---|---|---|---|---|---|
| **Projektion** | Ein-Strom | Replaybar | Read-Model | Signale | `IReplaybarerTracker` — Reset ✓ |
| **Reaktion** | Ein-Strom | Emittierend | — | Commands | `IEmittentenCursor` — Reset ✗ |
| **Pipeline (Event)** | Ein-Strom | Emittierend | — | Commands | `IEmittentenCursor` — Reset ✗ |
| **Pipeline (Trigger)** | **Push-Ingress** | Emittierend | — | Commands | *kein Log-Cursor* (§6) |
| **Prozess** | Korrelations-Multistrom | Emittierend (+ Entscheidungs-Log) | Petri-Marking (Log=Wahrheit, Cache) | Commands | Manager-Log + Cache-Cursor — Reset ✗ |

Der Entwickler schreibt weiter nur seine Fachlogik über die bestehenden Marker (`ISubscriber`+`Handle`,
`IPipelineHandler`+`Handle`, `IProzessDefinition`). Der Generator bindet sie an die Maschinen-Klasse und
wählt Kind + Marken-Interface nach der Achsen-Kombination.

---

## 5. Prozess: Marking bleibt aus dem Log, Cursor ist Cache, Terminal-Fix ist Poll-Routing — ⟳ v2

v1 wollte das Marking zu einem *co-committeten autoritativen* Read-Model machen. **Das ist falsch** —
es widerspricht dem bereits durchdachten `docs/prozess-marking-cursor-konzept.md` und schafft zwei
Wahrheiten neben dem Log (Inv.-1-Spannung). v2 trennt drei Dinge, die v1 vermischt hat:

**(a) Wahrheit — bleibt der Log.** Das Marking wird weiterhin *aus den Ziel-Streams gefaltet*
(`FaltMarkingAsync`); die durablen Prozess-Entscheidungen (`ProzessGestartet`/`SchrittGescheitert`/
`ProzessBeendet`) sind das Manager-Log. Die Zwei-Achsen-Marke `ErgebnisDa` (aufgelöst) vs. `WirkungDa`
(wirksam) **bleibt** — sie ist der K2-Fix und der Grund, dass ein Noop keinen Downstream-Join scharf
schaltet. Ein „monotoner Cursor als Fortschritt" (v1) würde genau diese Unterscheidung verlieren und
**S15 verschlimmern**.

**(b) Performance — der Cursor ist ein nicht-autoritativer Cache.** Genau `prozess-marking-cursor-konzept.md`:
Cursor + Tail statt Voll-Read ab 0, um den O(N²)-Re-Fold großer/breiter Prozesse zu vermeiden. **Best-effort,
außerhalb der Entscheidungs-Transaktion** — verloren/inkonsistent → Voll-Fold heilt. Er ist eine
Optimierung, kein Commit-Punkt.

**(c) Terminal & S15 — der eigentliche Fix ist das Poll-Routing, nicht der Cursor.** Das Ergebnis-Event der
letzten Transition triggert keine Regel; heute filtert der Poll es per `relevanteTypen` weg → nur
`WeckeSelbst` (fire-and-forget) findet das Terminal. **Fix:** der Poll lässt den Typ-Filter fallen und
routet *jedes* geänderte teilnehmende Stream-Event **per `CorrelationId`-Metadatum** (das der Manager beim
Feuern stempelt) an die richtige Korrelation. Dann weckt das Terminal-Event den Manager regulär → Terminal
wird erkannt.
- **Erst danach** ist `WeckeSelbst` entbehrlich (Reihenfolge!).
- Der durable `ProzessOffenIndex`-Backstop wird retirable, **sobald** das CorrelationId-Poll-Routing die
  Terminal- *und* Orphan-Fälle nachweislich abdeckt (die bestehenden `ProzessBackstopE2ETests` sind das
  Tor). Bis dahin bleibt er.

**Kostenwahrheit:** Das CorrelationId-Poll-Routing braucht einen Metadaten-Read pro geändertem Stream —
das ist *nicht* „gratis wie eine Projektion". Und der inkrementelle Multi-Stream-Fold mit Join ist der
aufwendigste Teil des ganzen Umbaus, nicht der einfachste. v2 nennt ihn so.

---

## 6. Pipeline: Event-Pfad = Reaktion, Trigger-Ingress = Push — ⟳ v2

Die Pipeline ist **nicht** ein einheitlicher P2-Konsument. Sie zerfällt ehrlich in zwei Teile:

- **Event-Pfad (Kanal 2):** Event rein → Command raus. Das *ist* bereits eine Reaktion und bringt nichts
  Eigenes. → **In Reaktionen falten** (Ein-Strom/Emittierend, Rolle 5). Bekommt Inbox-Idempotenz +
  bounded Token über das Emit-Primitiv (W1/W2 weg).
- **Trigger-Ingress (Kanal 1):** externe Trigger (FileWatcher/Timer/Webhook, `IPipelineTrigger`) sind
  **keine Log-Events, nicht persistiert** — sie haben keinen Cursor, keinen Fold, keine co-committbare
  Marke. Der Trigger-Ingress bleibt ein **dünner Push-Adapter**, der einen Command emittiert (über das
  Primitiv, idempotent). `IPipelineSelfMessage`/`ScheduleSelf` bleiben lokales Detail.

„Vereinheitlichung" heißt hier also präzise: *lösche den Event-Pfad der Pipeline, falte ihn in Reaktionen;
behalte einen dünnen Trigger-Ingress* — nicht „Pipeline wird `IDurableConsumer`".

---

## 7. Was unangetastet bleibt

- Das **Log-als-Wahrheit / Pull / Co-Commit**-Rückgrat.
- Der **Schreiber** (`AggregateActorBase`) im Kern.
- Das **Petri-Netz-Prozessmodell** (`IProzessDefinition`/`Regel`/`SammelBedingung`) — nur die *Ausführung*
  wird berührt, das Modell nicht.
- Die **Zwei-Achsen-Marke** (`ErgebnisDa`/`WirkungDa`) im Prozess.
- Die **Marker-Taxonomie** in `Interfaces.cs`.
- **Redis** als abgeleiteter Index. Die **sechs Invarianten**.

---

## 8. Auswirkungen auf die Ränder (Client, Redis, Postgres, ActorSystem, gRPC)

Der Umbau ist **fast vollständig server-intern**. Der Client spricht ausschließlich gRPC (`GrpcProxy`,
eine `Connect`-Bidi-Stream-RPC) — kein direkter Redis-/Postgres-Zugriff. Zwei externe Verträge bleiben
bewusst erhalten: (1) der Client macht selbst OCC (`VersioningModule` → `ExpectedVersion`); (2) das
Freshness-/Deps-Modell (Events mit `AggregateVersion` + Query-`Deps` aus Redis).

| Rand | Impact | Was ändert sich |
|---|---|---|
| **Client (Commands/Queries/Events)** | keiner (Events: nur cross-node besser) | Client behält OCC; Projektionen liefern weiter Read-Model + Deps |
| **gRPC / `domain.proto`** | keiner | Domänen-Typen unverändert; Rolle 9 betrifft die *interne* Ebene |
| **Redis** | keiner | bleibt Versions-/Deps-Index; Cursor liegt in Postgres |
| **Postgres / Marten** | eingegrenzt | `ProzessOffen` entfällt (nach §5-Tor); Emittenten-Cursor als Doc; `es`/`rm`/`dlq` unverändert |
| **ActorSystem / Proto.Actor** | Vereinfachung | eine Maschinen-Klasse, aber weiter mehrere Kinds; Placement gleich |
| **DLQ / Snapshots** | keiner | Schreiber-Kern + `dlq` unverändert |

**Rand-Risiko:** Der Client trackt Versionen auch aus Query-Deps. Prozesse liefern heute keine
Client-Queries — falls das Marking je abfragbar wird, muss die Deps-Berechnung mitgezogen werden.

**Ohne Backwards-Compat sauberer:** `AnyVersion = -1`-Sentinel abschaffen → zwei explizite Schreiber-Eingänge
`HandleClientCommand(expectedVersion)` / `HandleEmittedCommand(commandId)`; `PipelineActorBase`-Retry-Schleife
ganz löschen; grobe `GeneratedEventCommandMapping` (namespace-basiert) entfernen (nur präzise
`GeneratedCommandRouting` behalten); alte Kind-Registrierungen droppen.

---

## 9. K1 / interne Serialisierung — separater Meilenstein

Orthogonal zur Vereinheitlichung (Rolle 9). Nötig nur für echtes Multi-node. Der schwierige Teil ist die
polymorphe Payload-Serialisierung (`CommandEnvelope.Payload : ICommand`, `Wake`, …). Weg offen:
generierte Protobuf-Contracts für die internen Envelopes *oder* ein generierter Poly-Serializer über die
bestehende Typ-Registry (beide reflexionsfrei). *Tor:* Zwei-Node-Test — ein Adapter je Stream, Ordnung
erhalten, Poll heilt cross-node.

---

## 10. Migrations-Reihenfolge & Tore — ⟳ v2 (nach Korrektheitsgewinn, nicht nach Eleganz)

1. **Bugfixes zuerst (isoliert, kein Architektur-Umbau):**
   - **Emit-Primitiv (`ICommandEmitter`)** — die vier Emit-Pfade auf einen ziehen, immer idempotent +
     bounded Token; `AnyVersion`-Sentinel abschaffen. *Tor:* Reaktion/Prozess unverändert grün; **Pipeline
     dedupliziert (W1) und hängt nicht mehr (W2)** — Fake-Cluster-Test mit verlorener Quittung.
   - **Fan-out-Diskriminator** (RegelIndex + Instanz-Index in die Vorgang-Id) — latente Id-Kollision weg.
   - **`BrokerIdentity.GetShardIndex`** `& 0x7FFFFFFF` statt `Math.Abs` — Overflow-Crash weg.
2. **Unabhängige Feature-Lücken** auf der sauberen Emit-Grundlage: DLQ-Replay, Timer/Webhook-Trigger,
   Projektions-Rebuild-Runner.
3. **Prozessmodell festklopfen:** (a) Poll-Routing per `CorrelationId`; *Tor:* Terminal ohne `WeckeSelbst`,
   Backstop-Tests grün. (b) Marking-Cursor als **Cache** (kein Co-Commit); *Tor:* Sagas grün, O(N²) weg,
   Voll-Fold heilt Cache-Verlust. (c) Dann prozessnahe Features (Deadlines, Verkettung).
4. **Optional / eigener Meilenstein:** K1 (Multi-node), Monitoring, die vollständige Konsumenten-
   Vereinheitlichung (nur mit der Achsen-Trennung aus Rolle 5 — und nur wenn der Gewinn den Umbau trägt).

---

## 11. Feature-Inventar — haben / brauchen (code-verifiziert 2026-08-08)

**Solide vorhanden:** Log/Append/OCC, Version-pro-Event, Inbox/Idempotenz, Snapshots, Rehydration, Signal,
Poll-Backstop (Befund 1 gefixt), Straggler-Karenz (Befund 3), Projektions-Naht/Co-Commit, Reaktionen,
Prozess-Manager, Regel-Builder (Aritäten 1–3), Kompensation + KlärungNötig, Fan-out + Count-Join,
Korrelations-Router, Azyklizitäts-Boot-Guard (aktiv), Selbst-Weckung-Fix (Befund 2), Query-Seite +
Deps/Freshness, gRPC-Connect.

**Echte Lücken + Reihenfolge-Urteil:**

| Lücke | Status | VOR/NACH Emit-Refactor | Begründung |
|---|---|---|---|
| W1/W2 (Pipeline-Emit) | Bug | = Schritt 1 | zuerst; de-riskt alles |
| Fan-out-Diskriminator | Bug (latent) | JETZT, unabhängig | reiner Bugfix `ProzessId`/`ProzessManager` |
| `GetShardIndex` `Math.Abs(int.MinValue)` | Bug (latent) | JETZT, unabhängig | `& 0x7FFFFFFF` |
| DLQ-Replay | fehlt | unabhängig | Ops-/Read-Pfad auf `dlq` |
| Projektions-Rebuild-Runner | fehlt (Vertrag+Reset da) | leicht NACH | vereinheitlichte Schleife → EIN Rebuild |
| Deadlines/Timeouts (fachlich) | fehlt | NACH (stabiles Prozessmodell) | neues Primitiv (Timer-Token/Zeit-Event) |
| Prozess-Verkettung | teilweise (Modell ok) | unabhängig | braucht Test/Beispiel + evtl. expliziten Trigger |
| Timer/Webhook-Trigger + `ITriggerRegistration` verdrahten | fehlt | unabhängig | Trigger-Ingress bleibt Push (§6) |
| Cross-Node/Serialisierung (K1) | fehlt | eigener Meilenstein | orthogonal |
| Monitoring (Metrics/Tracing/HealthChecks/Prozess-Sicht) | teilweise | leichter NACH | profitiert von uniformer Maschine |
| Generischer `MartenProjectionTracker` = at-least-once | by design | — | keine Lücke; dokumentierte Eigenschaft |

**Antwort auf „erst alle Features, dann refactoren":** nicht in Reinform. Der Emit-Fix ist keine Feature,
sondern die **Grundlage** — zuerst. Dann Features auf sauberer Grundlage. Die große Vereinheitlichung
zuletzt und de-scoped; sie ist **keine Voraussetzung** für die Features.

---

## 12. Offene Entwurfsfragen

- **Cursor-Granularität des Prozess-Caches:** globale Sequenz gefiltert per Korrelation, oder `Map<Guid,int>`
  je teilnehmendem Stream? (Perf vs. Cache-Komplexität — der Knackpunkt aus `prozess-marking-cursor-konzept.md`.)
- **Count-Join im Cache:** die Breite bleibt Funktion des Auslösers; der Join-Zustand gehört ins gefaltete
  Marking — der Dedup-Fall (gleiche Ziele) muss dort *sichtbar* sein, nicht am Re-Fold verloren
  (verwandt mit dem Fan-out-Diskriminator-Bugfix).
- **Deadlines:** als Timer-Token im Marking (durabler Timer-Wheel) oder als Zeit-Event auf einem
  System-Stream? Beides bleibt „Struktur aus Code, Marking aus Log".
- **Serialisierungs-Weg (Rolle 9):** generierte Protobuf-Contracts oder Poly-Serializer.

---

## 13. Kurzfassung

> Die Architektur ist gesund und muss **zu Ende gedacht**, nicht neu gedacht werden. **Sofort und
> isoliert baubar (de-riskt alles Folgende):** das Emit-Primitiv (ein Baustein, immer idempotent, kein
> OCC intern) + die drei Bugfixes. **Danach** Features auf der sauberen Grundlage. **Das Prozessmodell**
> wird geschärft, nicht umgeworfen: Marking bleibt aus dem Log (Wahrheit), der Cursor ist ein
> nicht-autoritativer *Cache*, und der Terminal-/S15-Fix ist **Poll-Routing per `CorrelationId`** — nicht
> der Cursor. **Die Pipeline** zerfällt ehrlich (Event-Pfad → Reaktion; Trigger-Ingress → Push). **Die
> eine Maschine** ist eine Maschinen-*Klasse* mit zwei orthogonalen Achsen (Quell-Topologie, Effekt-Klasse)
> und getrennten Marken (`IReplaybarerTracker` vs. `IEmittentenCursor`) — nicht ein Kind mit einem
> Rückgabetyp. **K1/Multi-node** ist ein orthogonaler, separater Meilenstein. „Erst alle Features, dann
> refactoren" ist nicht die richtige Reihenfolge — erst der Emit-Fix, dann Features, Vereinheitlichung
> zuletzt.
