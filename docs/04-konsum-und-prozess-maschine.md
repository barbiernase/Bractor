# 04 — Konsum- & Prozess-Maschine (die vier durablen Konsumenten)

Projektion, Reaktion, Prozess/Saga und Pipeline-Event sind **vier durable Konsumenten, die
dieselbe Maschine nutzen**. Kein zweiter Marker, keine Taxonomie — der Unterschied fällt aus
Konstruktor-Stores und Rückgabetypen.

## 4.1 Die gemeinsame Pull-/Signal-Schleife

Herzstück: `ProjectionAdapter` (`Infrastructure/Projections/ProjectionAdapter.cs`). Eine
store-agnostische Schleife trägt die *Policy* (Marke lesen → ab Marke+1 geordnet lesen →
Guard → dispatchen → Marke vorrücken), aber **keine Transaktion**.

`WakeAsync(streamId, vomPoll, ct)` (`:76`):
1. **Startposition**: mit Tracker → `LastProcessedVersionAsync`; mit Emittenten-Cursor auf dem
   Signalpfad → `LadeAsync − 1`; sonst (tracker-los / Poll-Heilung) bewusst `−1` (ab 0,
   at-least-once).
2. **Geordnet lesen** ab `applied+1` via `_eventStore.ReadStreamAsync` — das tragende
   Pull-Leseprimitiv.
3. **Pre-Dispatch-Guard**: `if (e.AggregateVersion <= applied) continue;`.
4. **Marke vorrücken**: Tracker → `MarkProcessedAsync` (durabler Commit-Punkt);
   Emittenten-Cursor → `SchreibeAsync` (best-effort).
5. **H2-Fix**: der verlierbare Versions-Index (`IReadModelDepsSink`) wird erst **nach** dem
   Marken-Commit publiziert — nie über Uncommittetes.

**Genau-einmal-wirksam** entsteht nicht im Adapter, sondern erst aus (a) Guard + Marke und
(b) einem Store, der Effekt und Marke co-committet. Der Adapter selbst hat „keine Transaktion".

**Transport** (`Infrastructure/Projections/PullPath.cs`):
- **Signal** (schnell): `SignalReceiverActor` abonniert die `StateChangeVia`-Signale der
  Input-Event-Typen und weckt fire-and-forget die per-Stream-Cluster-Identität.
- **Poll** (Sicherheit, 30 s): `Poller.PollOnceAsync` scannt via `ReadChangedStreamsAsync`
  jenseits der High-Water-Mark. Der Poll-Cursor rückt **nur** vor, wenn jede Weckung als
  `WakeAck` bestätigt zurückkommt (bounded 10 s) — sonst hält der Poller die HWM und
  re-scannt (Befund-1-Härtung: kein terminaler unverarbeiteter Stream wird still übersprungen).

Beide Quellen wecken **dieselbe** Cluster-Identität `(KindName, StreamId)`; der per-Stream
`SignalAdapterActor` serialisiert sie über seine Mailbox (Single-Writer je Stream, kein Race).
Aller durable Zustand liegt im Store → Passivierung bei Idle ist folgenlos.

## 4.2 Die zwei Achsen

**Achse „Transport"** — orthogonal: `IPullSubscriber : ISubscriber` wählt Pull statt
Push-Broker. Der `PullPathGenerator` findet jede `IPullSubscriber` und erzeugt Kind +
Registration.

**Achse B — replaybar vs. emittierend** — im `ProjectionAdapter`-Ctor als sich ausschließendes
Paar kodiert:
```csharp
IProjectionTracker?  tracker,          // replaybar: Co-Commit + Reset
IEmittentenCursor?   emittentenCursor  // emittierend: best-effort, KEIN Reset
// beide gesetzt → throw
```
- `IProjectionTracker`: `LastProcessedVersionAsync`/`MarkProcessedAsync` **+ `ResetAsync`**
  (Reset = Umkehrung der Marke → Rebuild).
- `IEmittentenCursor`: nur `LadeAsync`/`SchreibeAsync`, **bewusst kein Reset** („Replay eines
  Emittenten bewegt echtes Geld").

Der Schnitt ist compile-zeit-strukturell: `IReplaybarerTracker : IProjectionTracker` trägt
Reset; der Emittenten-Cursor nicht. Die Achse wird zur Wiring-Zeit entschieden — der Generator
inspiziert die Ctor-Argumente (`PullPathGenerator.cs:139`): tracker-fähiger Store → replaybar;
sonst → emittierend (bekommt `IEmittentenCursor`).

| Konsument | Ctor-Store | Rückgabetyp `Handle` | Achse B |
|---|---|---|---|
| Projektion | tracker-fähiger Write-Store | `Task` + `writer.Execute` | replaybar |
| Reaktion | keiner | `IAsyncEnumerable<OneOf<Cmd>>` | emittierend |
| Prozess/Saga | eigener Petri-Manager | (kein `Handle`) | emittierend (Marking-Cursor) |
| Pipeline-Event | `PipelineEventPullBridge` | `IAsyncEnumerable<ICommand>` | emittierend |

## 4.3 Projektionen

Beispiel `Domain.Projections/ImagePairProjection.cs`: `partial class : ISubscriber,
IPullSubscriber`. Pro Event `writer.Execute(readModelId, ctx => …)`: DB-Upsert in den
injizierten Write-Store + `ctx.Track<T>(id)` für den Versions-Index. Der `ProjectionWriter`
(`Core/ProjectionWriter.cs`) sammelt nur; der Adapter liest nach dem Marken-Commit aus und
reicht die Ergebnisse an den `IReadModelDepsSink`.

> **⚠ Kern-Befund für die Bewertung — Co-Commit ist NICHT implementiert.** Architektonisch
> soll Effekt + `ProjectionCheckpoint` in **einer** Marten-Session committen
> (`Abstractions/IProjectionTracker.cs`). Der reale `MartenProjectionTracker` tut das
> **nicht**: er ist explizit als „⚠ PHASE-0-STAND — EIGENE SESSION (at-least-once)" markiert
> (`Infrastructure/Persistence/MartenProjectionTracker.cs:11`) und öffnet in
> `MarkProcessedAsync` eine **eigene** `LightweightSession`. Damit ist die versprochene
> Exactly-once-Wirksamkeit für den einzigen produktiven Store **vorgesehen, aber nicht
> eingelöst** — de facto Dual-Write/at-least-once, abgesichert nur über den Dedup-Schlüssel
> `(AggregateId, Version)` + idempotente Upserts. Der GA-1-Check würde bei einer echten
> `IAppendProjektion` durchgehen (der Tracker existiert ja), obwohl kein echter Co-Commit
> stattfindet. Siehe [13](13-reifegrad-schulden-bewertung.md), Risiko #1.

## 4.4 Reaktionen & das Emit-Primitiv

Reaktion `Domain.Projections/ImagePairReaktion.cs`: reine, feldlose Funktion, gibt
`IAsyncEnumerable<OneOf<WirkeReaktion>>` zurück — der **Rückgabetyp** (ein `ICommand`) wählt
den Effekt. Der Adapter routet den Output über `HandlerOutputRouter`: `ICommand` → Reaktion an
Fremd-Aggregat, `IEvent` → Re-Publish.

Der Command geht durch das **eine** Emit-Primitiv `CommandEmitter.EmitAsync`
(`Infrastructure/PubSub/CommandEmitter.cs`):
- deterministische `CommandId` aus `EmitKausalität` (Korrelation + Ursache + Diskriminator) →
  Empfänger-Inbox dedupliziert (W1),
- `CommandModus.Emittiert()` = **keine Version**,
- **bounded** Token (`_sendeFrist` default 5 s) → kein Infinit-Hang (W2),
- fire-and-forget, at-least-once; Timeout heilt der nächste Re-Wake/Poll.

Empfänger-Dedup liegt beim Ziel-Aggregat: der Decider erkennt eine bereits verarbeitete
`ReaktionId` → Noop; die durable Inbox-Marke `KommandoVerarbeitet` ist `IProzessIntern`.

**Analyzer-Zwang** (`CommandEmitAnalyzer`): **CQRS020** (jedes rohe `RequestAsync<CommandResult>`
außerhalb der Allow-Liste), **CQRS021** (Command-Send mit `CancellationToken.None`/`default`).

## 4.5 Prozess-/Saga-Maschine

**Definition (DSL).** Ein Prozess ist ein `IProzessDefinition` mit typisierten
Event→Command-Regeln über den Fluent-Builder `Prozess<TAuslöser>.Definiere(...)`:
- `Auf<E>().Sende<Cmd>(...)` + `.RückgängigDurch<Cmd>(...)` (Kompensation),
- `.Und<E2>().Und<E3>()` — Join/Konjunktion (Diamant),
- `.UndAlle<ESammel>(n)` / `SendeJe<Cmd>` — Count-Join / Fan-out dynamischer Breite ohne
  Zähler (die Breite steht im Auslöser).

Reale Beispiele: `Domain/Ueberweisung/UeberweisungsProzess.cs` (3-Wege-Join),
`Domain/Reiseauftrag/ReiseProzess.cs` (echter Diamant + Kompensation je Zweig),
`Domain/Sammelueberweisung/SammelueberweisungsProzess.cs` (`SendeJe` + `UndAlle`).

**Kern-Invariante: Struktur aus Code, Marking aus dem Log.** Der generische `ProzessManager`
(`Infrastructure/Prozess/ProzessManager.cs`) ist ein einmal geschriebener
Petri-Netz-Interpreter, der Prozess-Aggregat (eigenes Entscheidungs-Log) und Treiber verschmilzt.

**Fold** (`FalteAsync`): je Ziel-Stream ab `StreamCursor[s]+1` lesen, in ein `MarkingKompakt`
einarbeiten; ein Fixpunkt-Loop zieht Kandidaten. Drei getrennte Ergebnis-Achsen je Vorgang
(K2-Audit-Fix):
- **ErgebnisDa** = aufgelöst (irgendein Ziel-Event, auch Noop-Marke) → steuert „nicht mehr feuern".
- **WirkungDa** = ein Domänen-Event (kein `IProzessIntern`) → kompensierbar, aktiviert Joins.
- **AbgelehntDa** = durable `KommandoAbgelehnt`-Marke → wird zu `SchrittGescheitert`.

Ohne die dritte Achse läse der Vorwärtszweig eine Ablehnung als „aufgelöst" und schriebe
fälschlich `ProzessBeendet(true)` (der stille Falsch-Erfolg).

**Ein Schritt je Weckung** (`WakeAsync`): Marking falten → neue Ablehnungen zu
`SchrittGescheitert` → wenn nichts gescheitert: erste offene Transition feuern oder
`ProzessBeendet(true)` → sonst Kompensation rückwärts oder `KlärungNötig`-Terminal.

**Feuern** ist fire-and-forget über `ProzessManagerActor` → `DetachedProzessSend.Wrap` →
`CommandEmitter.EmitAsync`. Der deterministische **Vorgang IST die CommandId** → der Fold
matcht den durablen Ausgang über `CausationId == vorgang`, **keine Quittung** (der strukturelle
Hang-Fix). Nach jedem Send **Selbst-Weckung**, damit der Fold den durablen Marker sieht —
nötig, weil das Ergebnis-Event der letzten Transition Auslöser keiner Regel ist.

**Marking-Cursor (O(N²) → O(N)).** `MarkingKompakt` hält je Ziel-Stream einen `StreamCursor`
+ verdichtete `VorgangMarke`n statt roher Tokens:
- HOT-Cache je Manager-Instanz trägt Korrektheit über die Weckungen einer Aktivierung;
- **feuer-gerichtete Reads**: warm liest der Manager nur die seit dem letzten Fold befeuerten
  Ziel-Streams nach → DB-Roundtrips ~O(1) pro Weckung;
- durabler Store `IProzessMarkingStore` nur gedrosselt (alle 32 Weckungen);
- **Kaltstart / RegelHash-Mismatch → Voll-Fold ab 0** (Invariante 1). Der Cache ist
  nicht-autoritativ; stale kann höchstens re-feuern (verpufft am Empfänger).

Gemessener Gewinn (echtes Postgres, Projektnotiz): bis **9×** schnellere Wall-Clock bei N=60,
mit Aggregat-Historie **5,5×**.

**Zustellung & Liveness** (`ProzessManagerWiring.cs`, `KorrelationsRouter.cs`):
- `KorrelationsRouter` weckt nach **Korrelation** (statt StreamId).
- **Azyklizitäts-Boot-Guard** aktiv: `ProzessAzyklizität.PrüfeAlle` speist sich aus
  `GeneratedCommandRouting.Produziert` — ein zyklischer Regelsatz bricht am Boot.
- **Auslöser-Kollision** → fail-fast (H6).
- **§3-Backstop** (15 s): scannt den durablen `IProzessOffenIndex` und weckt jeden offenen
  Prozess bounded — das Netz für verlorene Selbst-Weckungen.

## 4.6 Pipeline (P6.1 / P6.2)

`PipelineActorBase<THandler>` ist das serverseitige Gegenstück zum gRPC-Client: empfängt
Trigger/Events, sendet Commands. Die P6.1/P6.2-Zerlegung trennt Transporte nach Persistenz
(Invariante 6):
- **P6.2 — persistierte Events** laufen über die **Pull-Maschine** (Reaktion, emittierend):
  `PipelineEventPullBridge` adaptiert die generierte `DispatchEventAsync` auf den Pull-Dispatch.
- **P6.1 / Rest-Push** — nur noch **transiente Events** (`ITransientEvent`) bleiben auf dem
  verlierbaren Broker; Trigger + Self-Messages ebenso.

Beispiel `Domain.Pipeline/ImageProcessing/ImageProcessingPipeline.cs`: `Handle(DateiErkannt
trigger, …)` yieldet Commands (Trigger-Pfad), `Handle(ImagePairKomplett evt, …)` reagiert auf
ein persistiertes Event (Pull-Pfad).

## 4.7 Dead-Letter & Snapshots

- **Dead-Letter**: `MartenDeadLetterSink` schreibt ein durables `DeadLetter`-Dokument je toter
  Zustellung (Schema `dlq`), best-effort. Read-Seite: `MartenDeadLetterReadStore`.
- **Snapshots**: `MartenSnapshotStore` — `Snapshot<TState>` als jsonb je Aggregat, reflexionsfrei
  registriert. **Nicht-autoritativ**: kein Treffer → Voll-Replay.

## 4.8 Schulden (Auszug — vollständig in [13](13-reifegrad-schulden-bewertung.md))

1. **Kein echter Co-Commit** (§4.3) — die zentrale Lücke.
2. **Kompensations-Read nicht cursor-optimiert**: `NächsteKompensationAsync` liest weiter ab 0.
3. **`MarkingKompakt` O(N) bei extremem Fan-out** (Payloads je Vorgang; Zähler/Bitset-Verdichtung fehlt).
4. **Domänen-Leak**: `Reaktionsempfaenger.VerarbeiteteReaktionen` wächst unbegrenzt.
5. **`CancellationToken.None`** im generierten Pull-Emit-Pfad (nur durch die 5-s-Frist bounded;
   CQRS021 greift dort syntaktisch nicht).
6. Poll-Intervalle (30 s / 15 s) hartkodiert.
