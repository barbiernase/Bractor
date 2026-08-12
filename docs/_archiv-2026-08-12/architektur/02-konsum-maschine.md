# 02 — Konsum-Maschine (Pull-Adapter, Signal/Poll)

> Die eine Schleife, die Projektion, Reaktion (und die Pipeline-Event-Zustellung) trägt.
> Verwandt: [00 Überblick](00-ueberblick.md), [03 Prozess-Maschine](03-prozess-maschine.md).

## Die eine Schleife

`Infrastructure/Projections/ProjectionAdapter.cs` ist die store-agnostische Kernschleife:

```
Marke lesen → ab Marke+1 geordnet lesen → Pre-Dispatch-Guard (AggregateVersion <= applied)
           → _dispatch → Marke vorrücken
```

Der Adapter selbst öffnet **keine** Transaktion — die gehört dem Store. Projektion,
Reaktion und der Pipeline-Event-Pfad nutzen dieselbe Schleife; es gibt keinen zweiten
Marker und keinen Handler-Zweig.

### Der Unterschied fällt aus Ctor-Stores + Rückgabetyp

Der `PullPathGenerator` erzeugt pro `IPullSubscriber` einen `{Name}PullAdapterKind` und
sammelt die Ctor-Parameter der Projektion als DI-Kandidaten:

- Hat der Konsument einen `IProjectionTracker` → **replaybar** (Projektion). Mehr als einer
  bricht (Spec 7.6).
- Kein Tracker → **emittierend** (Reaktion / Pipeline-Event) → bekommt den best-effort
  `IEmittentenCursor`.

Der **Achse-B-Schnitt ist im Adapter-Ctor erzwungen**: Tracker UND EmittentenCursor gesetzt
→ `InvalidOperationException`. Der Rückgabe-Unterschied fällt aus dem Dispatch: Der Handler
yieldet `IEvent` (→ re-publish) oder `ICommand` (→ Reaktion) — der `HandlerOutputRouter`
entscheidet per `switch (payload)`, kein Marker.

Der compile-zeit-getriebene Kern: `IReplaybarerTracker : IProjectionTracker` (mit `Reset*`)
vs. `IEmittentenCursor` (bewusst OHNE Reset). Blindes Replayen eines geld-bewegenden
Emittenten ist so strukturell unmöglich.

## Signal- vs. Poll-Pfad

**Per-Stream-Cluster-Actor** (`SignalAdapterActor`): Identität = `(AdapterKind, StreamId)`.
Der Cluster garantiert genau eine Instanz je Stream → Single-Writer; die Mailbox
serialisiert Signal- und Poll-Weckung desselben Streams. Aller durable Zustand liegt im
Store → Passivierung folgenlos.

Verdrahtung im `GenericPullStartupService` (`PullPath.cs`) pro `PullPathRegistration`:

- **Signal-Pfad (schnell):** `SignalReceiverActor` abonniert die `StateChangeVia{Event}`-
  Signale und weckt `(KindName, StreamId)` fire-and-forget mit `Wake(VomPoll:false)`. Signal
  darf verloren/doppelt/ungeordnet sein (Invariante 2).
- **Poll-Backstop (30 s):** `Poller` scannt via `ReadChangedStreamsAsync(HighWater)` das
  globale Log jenseits seiner HWM und weckt jeden geänderten Stream mit `Wake(VomPoll:true)`.
  Der Poller **awaitet** das `WakeAck` (bounded 10 s): der Cursor rückt nur vor, wenn JEDE
  Weckung bestätigt aufgeholt zurückkommt; eine unbestätigte Weckung hält die HWM.

**Durabler Poll-Cursor** (`MartenPollCursorStore`, `IPollCursorStore`): Start ab zuletzt
persistierter HWM → kein Re-Scan der Historie je Boot, holt aber „während unten" angehängte
Events nach.

**Wie Signalverlust heilt:** Coalescing fängt Duplikate/Verlust im Betrieb; den einen Fall,
den es nicht fängt — das letzte verlorene Signal vor Stille (oft ein Abschluss-Event) —
wandelt der Poll von „für immer verloren" in „≤ 1 Poll-Intervall Latenz". *Signal =
Geschwindigkeit, Poll = Sicherheit.*

## Co-Commit / IProjectionTracker

Commit-Punkt: `ProjectionAdapter.WakeAsync`.

- **Tracker vorhanden:** `MarkProcessedAsync(projectionId, streamId, last)`. **Ob**
  exactly-once, entscheidet allein der Store: Effekt + Marke in EINER Transaktion (Marten:
  dieselbe Session, ein `SaveChangesAsync`) → exactly-once; getrennt → at-least-once. Das
  Framework STELLT nur den Nahtpunkt BEREIT.
- **EmittentenCursor (emittierend):** `SchreibeAsync(Partition, last+1)` — kein Co-Commit,
  reiner Fortschritts-Cache. Verlust heilt der Voll-Fold (ab 0), der Empfänger dedupliziert
  über die Framework-Inbox → höchstens ein Re-Emit.
- **Partition** = `{projectionId}:{streamId}` (sonst kollidierten zwei Konsumenten desselben
  Streams).

## GA-1 — der Boot-Guard

`GaEinsPruefung` prüft: eine `IAppendProjektion` (append-artig, Doppelverarbeitung
korrumpiert) OHNE Co-Commit-Tracker bricht mit klarer Meldung beim Boot/Spawn, statt still
at-least-once doppelte Appends zu schreiben. Gilt nur für replaybare Append-Projektionen; ein
Emittent trägt den Marker nie.

**Deps-Index-Reihenfolge:** Der verlierbare Versions-Index (`IReadModelDepsSink`) wird
während der Schleife nur gepuffert und ERST NACH dem durablen Co-Commit publiziert — sonst
meldete er eine Version, deren Effekt noch nicht committet ist.

## EmittentenCursor

`Abstractions/IEmittentenCursor.cs` = Marke für emittierende Konsumenten. Der durable Effekt
ist das emittierte Command (idempotent am Empfänger), nicht ein co-committetes Read-Model.
Trägt `LadeAsync`/`SchreibeAsync` und bewusst **kein Reset**. `MartenEmittentenCursor`: ein
`EmittentenCursorDoc` pro Partition, eigene kurze Session, kein Co-Commit.

Kernlogik im Adapter:
- **Signal-Pfad** (`!vomPoll`): ab dem best-effort Cursor → kein Re-Emit ab 0.
- **Poll-Pfad** (`vomPoll`): bewusst ab 0 → so bleibt die at-least-once-Garantie exakt (ein
  verlorener detached-Emit wird spätestens vom nächsten Poll re-emittiert). Das ist der
  ganze Grund für das `VomPoll`-Flag in `Wake`.

## Rebuild

`ProjectionRebuilder`: „Replay IST der Adapter-Pfad mit Fortschritt = −1." `RebuildAsync`:
(1) Ziel leeren (`IRebuildableProjection.ClearAsync`), (2) `tracker.ResetAllAsync`,
(3) globaler Scan `ReadChangedStreamsAsync(0)`, jeden Stream über den normalen Adapter ab 0
re-dispatchen. **Replay-Grenze:** nur replaybare Konsumenten (mit Reset) sind rebuildbar;
Emittenten strukturell nicht — der Rebuilder re-dispatcht nur Repo-Write-Effekte, nie
geld-bewegende Ausgänge.

## Emit-Ausgang: HandlerOutputRouter / DetachedEmit / CommandEmitter

- **`HandlerOutputRouter`** — context-frei (Cluster aus dem `ActorSystem`), aus dem
  Pull-Adapter nutzbar. `IEvent` → re-publish (Broker); `ICommand` → `SendReaktionAsync`
  über `CommandEmitter`.
- **`DetachedEmit.Wrap`** — macht das Emit aus Sicht des virtuellen Adapter-Actors
  fire-and-forget: ein awaiteter `cluster.RequestAsync` ans Fremd-Aggregat verklemmte sonst
  den Cluster-Turn. At-least-once, Re-Wake/Poll heilt. **Das ist der ursprüngliche
  Hang-Fix.**
- **`CommandEmitter`** — das EINE Emit-Primitiv (EM-1): deterministische CommandId
  (`EmitId.Ableiten`), `CommandModus.Emittiert`, bounded Token (5 s). Erzwungen durch den
  `CommandEmitAnalyzer` (CQRS020/021, siehe [05](05-generatoren-und-analyzer.md)).

## Offene Punkte

- **De facto single-node** — kein Serializer für Wake/WakeAck/SignalEnvelope cross-node.
- `CancellationToken.None` im generierten Pull-Dispatch (`PullPathGenerator`) — Emit selbst
  ist gebounded (5 s), aber die Kette zum Adapter-`ct` ist gebrochen.

## Schlüsseldateien

`Infrastructure/Projections/ProjectionAdapter.cs`, `SignalAdapterActor.cs`,
`AdapterMessages.cs`, `PullPath.cs`, `SignalReceiver.cs`, `Poller.cs`,
`ProjectionRebuilder.cs`, `GaEinsPruefung.cs`, `ReadModelDepsWriter.cs`;
`Infrastructure/PubSub/HandlerOutputRouter.cs`, `DetachedEmit.cs`, `CommandEmitter.cs`;
`Infrastructure/Persistence/MartenEmittentenCursor.cs`, `MartenPollCursorStore.cs`;
`Abstractions/IEmittentenCursor.cs`, `IReplaybarerTracker.cs`, `IProjectionTracker.cs`,
`IAppendProjektion.cs`; `Infrastructure.SourceGeneration/PullPathGenerator.cs`.
