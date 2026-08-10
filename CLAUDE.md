# CLAUDE.md

Projektgedächtnis für Claude Code. Bewusst schlank — wird bei jeder Session
geladen. Volltexte liegen in `docs/` und werden bei Bedarf gelesen, nicht hier
eingebettet.

> **Backend-Neubau (starkes Refactoring, LÄUFT):** Herleitung + vollständige Design-Philosophie +
> Entwicklungsplan in `docs/backend-neubau-einheitliche-maschine.md`; abhakbarer Fahrplan (Phasen 0–8 +
> Feature-Strom, mit Toren) in `docs/backend-neubau-fahrplan.md`. Grundlagen dazu:
> `docs/gedankenmodell-system-als-graph.md`, `docs/zielbild-vereinheitlichte-konsumenten-maschine.md`,
> `docs/prozess-marking-cursor-konzept.md`, `docs/backend-audit-befunde.md`.
>
> **Fortschritt:** **P0 ✅** (Verträge `ICommandEmitter`/`EmitKausalität`, `IReplaybarerTracker`,
> `IEmittentenCursor` in `Abstractions`). **P2 ✅** (grün gegen echtes Marten): der `AnyVersion=-1`-Sentinel
> ist **gelöscht** und durch den typisierten `CommandModus { Client(int) | Emittiert }` ersetzt (ein
> Envelope, keine Rückwärtskompat); `AggregateActorBase` hat zwei Eingänge `HandleClientCommand`/
> `HandleEmittedCommand` (dispatch per `switch (Modus)`); Befund 5 (Live-Apply `is not IProzessIntern`)
> und Befund 10 (Footgun-Default, jetzt `required Modus`) erledigt. Proto/gRPC unverändert (nur
> Client-Commands kreuzen den Draht). **P3 ✅** — Emit-Primitiv `CommandEmitter` (EINE det. Id-Ableitung
> `EmitId`, `Modus.Emittiert`, bounded Token, Send-Seam); Reaktion + Pipeline migriert, die Pipeline-
> Retry-Schleife (zufällige CommandId + `CancellationToken.None`) gelöscht → **W1/W2 strukturell weg**
> (`EmitPrimitivTests`). *Offen:* Pipeline-Trigger-`None` + tote OCC-Helfer → **P6**. **P5(a) ✅** —
> präzises `CorrelationId`-Poll-Routing: der Terminal-Bug saß allein im Poll-Typ-Filter
> (`ProzessManagerWiring.cs:154`); Fix additiv (`ProzessPollFilter.SollRouten`: teilnehmend ODER Korrelation
> ∈ offene Prozesse) → terminale Ergebnis-Events werden event-getrieben geroutet, kein Über-Wecken.
> Korrektheit war schon durch den `ProzessOffenIndex`-Backstop gedeckt → P5(a) macht sie präzise;
> **beide Netze bleiben** (`WeckeSelbst` + `ProzessOffenIndex`, Auflage A2, NICHT retired). Prüfstand 47/47,
> Prozess/Saga/Reaktion-Integration 13/13 (SnapshotLive-Cold-Boot-Flake unabhängig). **P1a ✅** (erste
> P1-Scheibe, TG-1): die namespace-grobe `GeneratedEventCommandMapping` ist **gelöscht** (Generator + tote
> Fassade `EventCommandMapping.cs`); der einzige lebende Konsument (Blazor-Client-Capabilities:
> `CapabilitiesHandler` → `MessageTypeMapping.GetAllowedCommandNames`, event→Aggregat-Geschwister-Commands)
> leitet jetzt präzise aus `GeneratedCommandRouting` (CommandToAggregate + CommandToEvents) ab. **P1b ✅** —
> `Event→Signal` typ-getrieben: neuer Marker `IStateChangeSignal<TEvent>`, `StateChangeVia{X} :
> IStateChangeSignal<X>`, der `SignalFactoryGenerator` paart aus dem Typ-Argument statt Namens-Präfix
> (Präfix-/Namespace-Lookup gelöscht). **P1c ✅** (TG-3-Tor) — Attribute `[AggregatName]`/`[ProzessName]`;
> Identitäts-Kollision bricht den Build (CQRS011/012, bewiesen); `[AggregatName]` fließt konsistent in Routing
> UND ClusterKind (footgun-frei), `[ProzessName]` voller Resolver; Default = Typname → keine Migration.
> Prüfstand 50/50, Integration 24-25 (nur SnapshotLive-Flake). *Rest (Follow-up):* Actor-Klassenname
> `{TState.Name}Actor` → gleichnamige Aggregate koexistieren noch nicht (CS0101, von CQRS011 abgefangen);
> `aggregate_type`-Header noch Typname (informativ). **P1 im Kern fertig.** **Bugfixes ✅** — Befund 9
> (`BrokerIdentity` `& 0x7FFFFFFF` statt `Math.Abs`) + Befund 7/8 (Fan-out-Vorgang: RegelIndex + Instanz-Index
> in `ProzessManager`). Prüfstand 51/51, Saga/Prozess 11/11 no-regression. **P5 · Treiber-Fold Scheibe A ✅** (der gekoppelte Kern,
> `docs/handoff-treiber-fold.md` §7): durabler Marker `KommandoAbgelehnt(CommandId, Grund) : IEvent, IProzessIntern`
> (der Actor co-committet ihn auf dem EMITTIERTEN Ablehnungs-Pfad, eine Transaktion, Client-/OCC-Pfad unberührt);
> **Zwei-Mengen-Inbox** (`_verarbeiteteCommandIds` + `_abgelehnteCommandIds`, beide im Snapshot + `AggregateRehydrator`
> gefaltet) → Re-Delivery eines abgelehnten Vorgangs bleibt konsistent `Success:false`, NIE `true`; neue Fold-Achse
> `AbgelehntDa` im `ProzessManager` → `WakeAsync` stempelt daraus **vor** dem Vorwärts/Kompensations-Split ein durables
> `SchrittGescheitert`. **Kopplung (§4):** Marker + Zwei-Mengen-Inbox + Fold-Achse ZUSAMMEN, sonst stiller Falsch-Erfolg
> `ProzessBeendet(true)`. **Treiber sendet in A NOCH über die Quittung** (additiv/idempotent → safe). Beweis: store-freier
> `AblehnungsMarkeTests` + volle Saga-Suite als No-Regression-Oracle. **Prüfstand 54/54, Integration 25/25** (BestellSaga-
> Kompensation grün). **P5 · Treiber-Fold Scheibe B ✅** (der eigentliche EM-1-Abschluss): der Treiber sendet jetzt
> **fire-and-forget über das EINE Emit-Primitiv `CommandEmitter`** — **genau ein Emit-Weg, keine Quittung mehr**. Neue
> Überladung `EmitAsync(cmd, commandId, korrelation, ct)` (§5 Weg a: `vorgang` IST die CommandId → Fold-Match unverändert);
> `SendeAnZiel`/`MeldeFehlschlagAnManager`/`MeldeFehlschlag`/`NotiereFehlschlagAsync` **gelöscht**; `DetachedProzessSend` auf
> **emit + `danach`** reduziert (`WeckeSelbst` nach JEDEM Send im `finally`). Fehlschlag-Erkennung trägt allein der Fold
> (§6 eventual; `WeckeSelbst` + `ProzessOffenIndex` bleiben). Nachzug: `NächsteKompensationAsync` wertet eine
> `KommandoAbgelehnt`-Marke als *unvollziehbar* (KlärungNötig), nicht als „erledigt". Beweise: `DetachedProzessSendTests`
> neu + `EmitPrimitivTests` (+Überladung) + **volle Saga-Suite grün OHNE Quittung**. **P5 · Treiber-Fold Scheibe C ✅**
> (Analyzer A6 — EM-1 ERZWUNGEN): neuer Roslyn-`CommandEmitAnalyzer` (in `Infrastructure.SourceGeneration`, läuft auto
> auf `Infrastructure`): **CQRS020** = Build-Fehler bei rohem `RequestAsync<CommandResult>` außerhalb der zwei legitimen
> Sender (`CommandEmitter` + `ProtoActorAggregateDispatcher`); **CQRS021** = Build-Fehler bei `CancellationToken.None`/
> `default` auf einer Command-Kante (W2). Präzise via `T == Abstractions.CommandResult` → Trigger-Pfad (P6) bleibt außen vor.
> Beweise: End-to-end-Demonstration (temporäre Probe erzeugte CQRS020+021, danach gelöscht) + durabler `CommandEmitAnalyzerTests`
> (3, eigene `CSharpCompilation`). **Prüfstand 58/58, Integration 25/25. EM-1 voll erfüllt UND erzwungen.**
> *Offen/ehrlich:* der KlärungNötig-Pfad (Kompensation selbst abgelehnt) ist korrekt-per-Konstruktion, aber nicht integration-gedeckt.
> **P6.1 ✅** — `PipelineActorBase` entrümpelt: toter OCC-Ballast (`MaxRetries`/`ResolveVersion`/`DeadLetterAsync`/
> write-only `_versionCache`/toter `IDeadLetterSink`-Param, Generator angepasst) raus; `SendTriggerAsync`-`None` → bounded (W2).
> **P4 im Kern ✅** (die Konsumenten-Maschinenklasse, zwei Achsen): **P4.1** `IEmittentenCursor` real (`EmittentenCursorDoc` +
> `MartenEmittentenCursor` best-effort/kein-Co-Commit + `InMemoryEmittentenCursor` + DI/Schema); **P4.2** die eine Maschine
> (`ProjectionAdapter`/`SignalAdapterActor`) trägt jetzt REPLAYBAR (`IProjectionTracker`, Reset) ODER EMITTIEREND
> (`IEmittentenCursor`, KEIN Reset) — exklusiv (Guard), der `PullPathGenerator` wählt nach Store; **Korrektheit:** der
> detached-Emit heißt Cursor rückt nur auf dem **Signal-Pfad** vor, der **Poll heilt ab 0** (`Wake.VomPoll`) → at-least-once
> exakt erhalten; **P4.3** GA-1-Check (Boot/DI): `IAppendProjektion`-Opt-in + `GaEinsPruefung` bricht eine append-artige
> Projektion ohne Co-Commit-Tracker (in die Kind-Factory verdrahtet, `ImagePairHistorieProjection` markiert).
> **Zählerstände: Prüfstand 67, Integration 25/25.** **Test-Infra dieser Session:** `scripts/dev-infra-setup.sh` (native
> Postgres/Redis/Consul, kein Docker-Hub; .NET 10 SDK baut/läuft `net9.0` per Roll-Forward, da .NET 9 EOL).
> **P6 vollständig ✅** — **P6.2** (Event-Pfad-Fold): generierter `{Name}EventPullKind` + `PipelineEventPullBridge`
> ziehen den PERSISTIERTEN Pipeline-Event-Pfad auf die Pull-Maschine (emittierend, Achse B); **transiente Events
> bleiben Push** (`ITransientEvent` ist nicht im Log — Invariante 6). `PipelineActorBase`-Broker-Abo nur noch
> transient; `SendTriggerAsync`→`PipelineTriggerSender`. Live-Boot: `pull-pipeline-ImageProcessingPipeline` auf
> 1 Signal-Typ, 0 Exceptions. **Feature-Strom angefangen:** **Rebuild-Runner** (`ProjectionRebuilder`: „Replay =
> Adapter ab Marke -1", store-frei bewiesen) + **DLQ-Ops-Pfad** (`IDeadLetterReadStore` + Marten/InMemory:
> listen/filtern/zählen/auflösen, kein Auto-Replay). **Zählerstände: Prüfstand 79, Integration 25/25.**
> **Offen (Handoff `docs/handoff-feature-strom-rest.md`):** Timer/Webhook-Trigger, Prozess-Verkettung,
> Deadlines, Monitoring. **P5(b)** bewusst zurückgestellt (Konzept §8: erst bei großem Prozess — riskanter
> Join-Fixpunkt). Multi-Node (P7/P8) NICHT im aktuellen Scope.

## Was das Projekt ist

Selbstgebautes, signalbasiertes CQRS-/Event-Sourcing-Framework auf **Proto.Actor**
(virtuelle Cluster-Actors), **Marten/PostgreSQL** (Event-Store, einzige Wahrheit)
und **Redis** (abgeleiteter, nicht-autoritativer Versions-Index). Ziel des
laufenden Ausbaus: Events geordnet und **genau einmal wirksam** an Projektionen,
Reaktionen und (später) Prozesse zustellen — ohne Runtime-Reflection, alles über
Typen geroutet, alles Dispatchende zur Compile-Zeit generiert.

## Aktueller Scope (diese Ausbaustufe)

Bis **einschließlich Sharding + Poll-Backstop**, plus Reaktionen (Command auf
Fremd-Aggregat). **Enthalten:** Log, Signal, Log-Read, Exactly-once-Nahtpunkt,
Replay, Projektionen, Reaktionen mit Empfänger-Dedup, Sharding, Poller.
**Bewusst NOCH NICHT:** Prozess-Aggregat/Treiber, Kompensation, Klärungszustand,
deterministische Prozess-Ids, Deadlines, Verkettung, Snapshots.

## Die sechs Invarianten (jede Entscheidung leitet sich hieraus ab)

1. Die Wahrheit ist der Log. Ordnung/Vollständigkeit/Wiederholbarkeit kommen NUR
   aus dem Event-Store-Read.
2. Das Signal ist nur ein Weckruf: trägt nur `(StreamId, Version)`, darf verloren,
   doppelt, ungeordnet sein.
3. Routing über Typen — nie ein handgebauter Identitäts-String.
4. Keine Runtime-Reflection. Kein `Activator.CreateInstance`, kein
   `MethodInfo.Invoke`, kein Assembly-Scan im Laufzeitpfad. Alles generiert.
5. Der Fachcode bleibt rein. Cursor, Signal, Ordnung, Exactly-once, Sharding,
   Prozess-Maschinerie tauchen im Entwickler-Code nie auf.
6. Persistent genau dann, wenn ein durabler Konsument abhängt. Verlierbares
   (Tick, UI-Feedback, Datei-Trigger) bleibt auf dem schnellen Kanal.

## Exactly-once — die ehrliche Aussage

Das Framework stellt NUR einen Nahtpunkt bereit (`IProjectionTracker`), es
garantiert die Wirksamkeit NICHT selbst. Ob aus „wirksam" ein „genau einmal
wirksam" wird, entscheidet die Store-Implementierung: Effekt + Marke in EINER
nativen Transaktion → exactly-once-wirksam; getrennt → at-least-once, Handler
müssen idempotent sein. Append-artige Projektionen brauchen Co-Commit ODER einen
Dedup-Schlüssel `(AggregateId, AggregateVersion)`.

## Festgeklopfte Entscheidungen (nicht ohne Grund umwerfen)

- **Envelope-Schnitt:** eigenes `IEventEnvelope : IAggregateEnvelope` mit
  `AggregateVersion` — Commands bekommen keinen bedeutungslosen Versionswert.
- **Testvehikel:** bestehende ImagePair-Domäne; `ImagePairHistorieProjection`
  ist das scharfe, append-artige Ziel für die Crash-Proben (Upserts verstecken
  Doppelverarbeitung, Historien decken sie auf).
- **Replay ist first-class:** Tracker kann zurücksetzen; Ziel-Löschen hat einen
  eigenen Vertrag; Re-Driver hat seine Schnittstelle. Der ausführende Rebuilder
  ist die Read-und-Dispatch-Schleife des Adapters → kommt mit Phase 2.
- **Replay-Grenze:** Replay ist projektions-lokal. Command-yieldende Handler
  (Reaktionen) werden NICHT blind replayt.
- **Poller-Stream-Quelle (Entsch. 19.1):** globaler Store-Scan über
  Sequenz/High-Water-Mark. Redis ist zu flüchtig, durable Liste ein Extra-Write.
- **Rückgrat: Eigenbau statt Martens Projektions-Daemon** — bewusst, weil
  Proto.Cluster-Single-Writer, cross-aggregate Command-Routing und EIN
  compile-zeit-dispatchtes Handler-Modell (inkl. Reaktionen/Prozesse) gebraucht
  werden.

## Phasen & Tore (Details: docs/entwicklungsplan.md)

- **0 Verträge + Prüfstand** — kompiliert, Schnitt trägt alles Folgende.
- **1 Schreibseite** — Command → geordneter Stream, Version PRO Event, Signal
  erscheint.
- **2 Rückgrat (Meilenstein)** — eine Projektion überlebt Signalverlust,
  Doppel-Weckung, Absturz. Single-node, KEIN Sharding. Drei Takte:
  laufen → korrekt (Guard + Session-Co-Commit) → beweisen (Crash-Proben).
- **3 Output-Routing** — Reaktion feuert Command auf Fremd-Aggregat; Duplikat
  verpufft beim Empfänger (Noop-Decider + deterministische CommandId).
- **4 Sharding + Poll** — zwei Nodes → eine Adapter-Instanz; Poll heilt
  Totalverlust.

## Naht-/Reaktions-Umbau (erledigt; Kontext archiviert: docs/archiv/uebergabe-reaktionen-auf-pull.md)

**Erledigt (diese Ausbaustufe):** Die Projektions-Naht wurde **store-agnostisch neu gebaut**
(Plan + Zielbild: `docs/archiv/entwicklungsplan-projektionsnaht.md`; Phasen 1–5 grün gegen Docker).
`ICoCommitProjectionStore`/`CommitBatchAsync` gelöscht; **ein** Nahtpunkt `IProjectionTracker`
(Commit-Punkt `MarkProcessedAsync`); `ProjectionAdapter` = die 7.3-Schleife; die Pro-Projektion-
Verdrahtung ist **generiert** (`PullPathGenerator` → `AddGeneratedPullPaths()`); der Domänen-Store
liegt in `Domain.Infrastructure`; `ProjectionCheckpoint` in `Abstractions`; der Transport-Marker
heißt **`IPullSubscriber`** (NICHT mehr `IExactlyOnceProjection`). Kein Domänen-Glue mehr im Framework.

**✅ Erledigt — Schritt (A): Reaktionen laufen jetzt auf DEMSELBEN Pull-Adapter wie die Projektionen**
(die EINE Maschine, Spec 8; Voraussetzung für den Prozess-Treiber Phase 5). Der zweite Versuch trug —
kein Hang. Der Kern war minimal, ohne zweiten Marker/Handler-Zweig:
- `ImagePairReaktion : ISubscriber, IPullSubscriber` → der `PullPathGenerator` selektiert sie auf Pull,
  koppelt den Push-Subscriber automatisch ab (`PushSubscriberExclusions`). Parameterloser Ctor →
  `tracker = null` → liest ab 0 → re-emittiert je Weckung → der **Empfänger dedupliziert** (Noop-Decider +
  deterministische `ReaktionsId`, Spec 9.3).
- Der generierte Pull-Dispatch übergibt jetzt das **echte `emit`** des bestehenden `HandlerOutputRouter`
  (statt No-op). `system.Cluster()` steht in der **Spawn-Factory** (`Props.FromProducer` → läuft im
  Actor-`Started`, Cluster fertig), NICHT bei der Kind-Registrierung.
- **Der Hang-Fix:** neuer `Infrastructure/PubSub/DetachedEmit.cs` — `Wrap()` macht das emit AUS SICHT DES
  virtuellen Adapter-Actors fire-and-forget (der `cluster.RequestAsync` an das Fremd-Aggregat blockiert den
  Actor-Turn nicht mehr; at-least-once, Re-Wake/Poll heilt). Der `HandlerOutputRouter` blieb UNVERÄNDERT
  (Push nutzt ihn weiter synchron — grün, nicht angefasst).

Bewiesen: `Infrastructure.Pruefstand.Tests/Phase3/ReaktionAufPullTests.cs` (2 Tests, in-memory, Fake-Emit) —
(1) Reaktion emittiert über den Pull-Adapter ihr `WirkeReaktion`, (2) ein NIE zurückkehrender Send blockiert
`WakeAsync` nicht, liefert die Ausgabe aber trotzdem. Prüfstand 37/37. `ReaktionE2ETests` läuft jetzt auf Pull,
grün (2s isoliert = **Signal-Pfad**, nicht erst der 30s-Poll).

**⚠ Test-Harness:** `Infrastructure.Integration.Tests/xunit.runner.json` schaltet die Parallelisierung AB
(`parallelizeTestCollections: false`). Grund: jede Testklasse fährt einen ECHTEN Consul-Cluster hoch —
4 parallel konkurrieren um Postgres/Consul/Redis, wodurch der Reaktions-Signal-Pfad unter Last so verzögert
wurde, dass der 30s-Testtimeout den 30s-Poll-Backstop kreuzte (Timing-Flakiness, KEIN Logikfehler; sequentiell
6/6 grün). **Integrationstests immer sequentiell laufen lassen.**

**✅ Boot-Härtung (Host.Grpc bootet sauber verifiziert, 2 Fixes):** Der tracker-lose Reaktionspfad + Poll
erzeugte beim ersten Boot einen OCC-Storm gegen den Singleton-Empfänger (~500 nicht-publizierbare
`CommandFailed`, hunderte `Concurrency conflict`, verstärkt durch Alt-Testdaten). Zwei Wurzeln behoben:
- **Versions-Sentinel `CommandEnvelope.AnyVersion` (= -1):** Reaktions-/Prozess-Commands behaupten KEINE
  Empfänger-Version. `AggregateActorBase` überspringt die OCC-Assertion bei `ExpectedVersion < 0` (aufgelöst =
  aktuelle Actor-Version für Append + Versions-Stempel); Idempotenz sichert die deterministische CommandId +
  Noop-Decider, nicht OCC. `HandlerOutputRouter` sendet Reaktionen mit `AnyVersion` (kein garantierter
  „expected 0 vs. actual N"-Konflikt mehr). **Bestehende Commands (ExpectedVersion ≥ 0) sind byte-genau unberührt.**
- **Durabler Poll-Cursor (`IPollCursorStore` / `PollCursor`; `MartenPollCursorStore` + `InMemoryPollCursorStore`):**
  der Poller startet ab der zuletzt persistierten HWM statt ab 0 → kein Re-Scan der ganzen Historie je Boot,
  holt aber „während unten" angehängte Events nach (Cursor < deren Sequenz). Verdrahtet in
  `GenericPullStartupService` (Start-HWM je Pfad laden, nach jedem Poll persistieren) + Marten-Schema-Reg +
  DI in `AddCqrsFramework`. Naive „ab aktueller HWM starten" wäre FALSCH gewesen (verlöre die „während unten"-Events).
Beweis: Prüfstand 38/38 (neu: `PollerTests.Poller_setzt_nach_Neustart_bei_persistierter_HWM_auf_statt_ab_0`);
Host.Grpc Boot A `ab HWM 0` (Historie einmal, 0 CommandFailed/0 Konflikte) → Boot B `ab HWM 778`, **0** Reaktions-
Re-Emits. Integration sequentiell 6/6.

**⏳ Schritt (B) läuft — (B0) Migration der restlichen Handler auf Pull, dann (B1) Push-Treiber löschen.**
Auf Pull sind jetzt: `ImagePairHistorieProjection`, `ImagePairReaktion`, **neu `ImagePairProjection`**.
- *ImagePairProjection migriert:* neuer Co-Commit-Store `Domain.Infrastructure/ImagePairStore.cs`
  (`IProjectionTracker` + `IImagePairWriteStore`) nach dem `ImagePairHistorieStore`-Muster — Writes puffern als
  aufgeschobene Session-Ops, `MarkProcessedAsync` spielt sie in EINER Marten-`IdentitySession` (read-your-writes:
  ein späteres `Set*` sieht das frühere `Upsert` im selben Batch) + Checkpoint in EINEM SaveChanges → exactly-once.
  **Read-Seite unverändert** Singleton `ImagePairStorePostgres` (`IImagePairReadStore`); nur die Write-Reg wurde
  auf transient Co-Commit umgestellt. Marker `: ISubscriber, IPullSubscriber`. Beweis: `LiveCommandE2ETests`
  prüft jetzt zusätzlich, dass das ImagePair-Read-Model per Pull materialisiert; Host.Grpc bootet mit 3 Pull-Pfaden
  sauber (Boot C: „3 Adapter-Kinds", 3× „Push übersprungen", 0 Exceptions).
**✅ (B1) erledigt — der redundante Push-Dispatch-Treiber ist gelöscht (Endzustand Spec 8: EINE Maschine).**
Entfernt: `SubscriberActorBase`, `SubscriberStartupService`, `Infrastructure.SourceGeneration/SubscriberActorGenerator`
(erzeugte Push-Actors + `GeneratedSubscribers`) sowie zwei tote auskommentierte Dateien. Der **Broker-Kanal bleibt**
(Signal-Zustellung + Re-Publish reaktiver Events via `BrokerPublisher`); `ReadModelDepsWriter`/`IReadModelDepsSink`
bleiben (Pull-Adapter-Deps-Index). Nachzug: die Projektions-Logik-Singletons braucht der generierte
`ProjectionQueryService` (nur für ihre `SubscriberId`) — jetzt domänenseitig in `DomainServiceExtension` registriert,
statt vom (entfernten) Push-Generator. Beweis: Prüfstand 38/38, Integration 6/6, Host.Grpc bootet mit gRPC + Broker
+ 3 Pull-Pfaden, **ohne** „Subscriber Startup", 0 Exceptions.

**✅ Phase 2 erledigt — die Codebasis ist ImagePair-only.** Entfernt: Demo-Domänen **Counter, Todo, LagerArtikel,
Profil** (`Domain/`-Ordner) + Projektionen **AuditLog/TodoProjection/LagerbestandProjection** samt Reader/Stores/
Queries/ReadModels (`Domain.Projections`, `Domain.Infrastructure`) + tote `ProtoClusterExtensions.cs` + Todo-Enum-
Einträge im `DtoMapperGenerator` + `ConsoleApp` (Profil-Demo) aus der Solution. **Behalten: ImagePair + Reaktion**
(Reaktion ist Ziel von `ImagePairReaktion`) + Pipeline. Proto **regeneriert** (`dotnet run --project Proto.SourceGeneration`
→ `ProtoRepo/domain.proto` 1322→678 Zeilen, 0 Demo-Typen). Beweis: Prüfstand 38/38, Integration 6/6; Host.Grpc bootet
mit gRPC + Cluster + 3 Pull-Pfaden, **0** Demo-Aggregat-Shards, 0 Exceptions.
Hinweis: Orphan-Verzeichnisse (DebugApp, Host, Host.Persistence.Scenarios, Client.Harness, Client.Test,
Infrastructure.PubSub.Application) liegen auf der Platte, sind aber NICHT in `CqrsSolution.sln` → build-irrelevant.

**⏳ Phase 5 (laufend) — die Prozess-Maschinerie (Spec 10–12).** Pilot: **minimale Konto-/Überweisungs-Domäne**
(Spec-Referenz), da ImagePair-only. Entschieden: Schritt-Dedup über ein **Korrelationsfeld** `Vorgang` im Command,
das der Treiber injiziert (`IProzessSchrittCommand.MitVorgang`) — konsistent mit `WirkeReaktion.ReaktionId`, kein
Framework-Eingriff.
- *Increment 1 (fertig, 4 Tests):* Plan-API `Abstractions/IProzessPlan.cs` (`IProzessPlan`, `ProzessSchritte.Start.Dann(…)`
  — „Dann" als einziges Verb; die Spec-Form mit statischem+Instanz-`Dann` ist CS0111) + deterministische Ids
  `Infrastructure/Aggregate/ProzessId.cs` (`Für`/`FürSchritt`/`FürRückabwicklung`).
- *Increment 2a (fertig, 5 Tests):* Ziel-Aggregat `Domain/Konto/` (State/Commands/Events/Decider/Applier) — Schritte
  bewegen den Saldo, Dedup über `VerarbeiteteVorgaenge` verpufft Duplikate, Fehlerfall = `ITransientEvent`-Ablehnung
  (Gesperrt/Deckung), Kompensation radiert nicht. In-memory über die generierte `AggregateHandlerFactory` (Version
  im Test selbst hochgezählt, wie der Actor). Marker `Abstractions/IProzessSchrittCommand.cs`. Proto regeneriert.
- *Increment 2b (fertig, 6 Tests):* `Domain/Ueberweisung/` — `UeberweisungsPlan` (3 Schritte reservieren→gutschreiben→
  buchen, je mit Gegen-Command) + Prozess-Aggregat `UeberweisungsProzess` (Zustandsmaschine Spec 11.5:
  Neu→Läuft→{Abgeschlossen | Rückabwicklung→{Fehlgeschlagen | KlärungNötig}}). „Dran ist Schritt n" ergibt sich aus
  der Faltung (`NaechsterVorwaertsSchritt`/`NaechsteRueckabwicklung` — genau das, was der Treiber liest). Command=imperativ
  (`Melde…`), Event=Vergangenheit (Namen dürfen nicht kollidieren). Bewiesen: Happy Path, Fehler@2→Ausgleich@1→Fehlgeschlagen,
  Fehler@1→sofort fehlgeschlagen, Doppelstart/Out-of-order=Noop, gescheiterte Rückabwicklung→KlärungNötig. Proto regeneriert.
- *Increment 3 — Treiber-LOGIK (fertig, 3 Tests):* `Infrastructure/Prozess/UeberweisungsTreiber.cs` — der Adapter auf
  dem Prozess-Stream mit Command-mit-Quittung (Spec 11.2). Bei jeder Weckung lädt er den Prozess-Zustand frisch
  (`LoadStateAsync`), liest „dran ist Schritt n" aus der Faltung, injiziert den deterministischen `Vorgang`
  (`MitVorgang`), sendet ans Ziel, wartet auf `CommandResult`, meldet Erfolg/Ablehnung zurück ans Prozess-Aggregat
  und macht als Single-Writer sofort weiter (`while`-Schleife). Der `_send`-Seam (`Func<ICommand,ct,Task<CommandResult?>>`)
  IST die einzige Cluster-Berührung → live ein bounded `cluster.RequestAsync`, im Prüfstand ein **Fake-Cluster**
  (routet an die echten Aggregate über `InMemoryEventStore` + generierte Factory). Bewiesen: Happy Path bewegt beide
  Konten; Fehler beim Gutschreiben (Zielkonto gesperrt) gleicht die Reservierung aus, **nichts radiert** (Hin+Gegen
  im Gedächtnis); Crash zwischen Send und Quittung heilt ohne Doppeleffekt (Ziel dedupliziert via `Vorgang`).
- *Increment 4 — Treiber LIVE (fertig, 2 Integrationstests): ✅ die Hang-Frage ist beantwortet — KEIN Hang.*
  `Infrastructure/Prozess/TreiberActor.cs` (+ `UeberweisungsTreiberKind`): der Treiber als virtueller Cluster-Actor
  je Prozess-Stream. Auf `Wake` AWAITET er `cluster.RequestAsync` ans Ziel-/Prozess-Aggregat — genau der blockierende
  Call, an dem (A) hing. Mit dem (A)-Fix (`system.Cluster()` in der **Spawn-Factory** `Props.FromProducer`, NICHT bei
  Kind-Registrierung; **bounded** Token statt `None`) läuft er sauber durch. Send-Seam: `ExpectedVersion = AnyVersion`
  (fachliche Dedup, kein OCC), AggregateType per `GeneratedPipelines.CommandAggregateTypes` (Namespace-Konvention).
  `ProzessTreiberE2ETests` gegen Postgres/Consul/Redis: Happy Path treibt alle 3 Schritte live (Konten 100→70 / 0→30,
  ~6s); Fehlerfall (Zielkonto gesperrt) gleicht die Reservierung live aus → Fehlgeschlagen, Saldo wieder 100, nichts
  radiert (~7s). **Lektion revidiert:** der awaited RequestAsync aus dem Adapter-Turn hängt NICHT per se — (A)s Hang war
  das zu frühe `system.Cluster()` + `None`, nicht das Blockieren.
- *Increment 4b — Treiber SIGNAL-GETRIEBEN (fertig): kein manuelles Wecken mehr.* `Infrastructure/Prozess/ProzessWiring.cs`
  (`AddUeberweisungsProzess`) registriert den Treiber-Kind + eine `PullPathRegistration` auf die Prozess-Stream-Signale
  (ProzessGestartet/SchrittErledigt/…) → der generische `GenericPullStartupService` spawnt Receiver+Poller, die den
  Treiber bei jeder Prozess-Zustandsänderung wecken. `StarteProzess` (via Dispatcher) → `ProzessGestartet`-Signal →
  Treiber → jede Quittung erzeugt das nächste Signal → Prozess läuft signal-getrieben bis Abgeschlossen/Fehlgeschlagen
  (~3s beide `ProzessTreiberE2ETests`). Lektion: Treiber ist so schnell, dass er den `Laeuft`-Zwischenzustand
  überspringt — Tests nur auf den Endzustand prüfen.
- *Increment 4c — Start-Bindung aus einem Auslöse-Event (fertig): der Pilot läuft end-to-end aus EINEM Fach-Command.*
  `ProzessId` nach **`Abstractions`** verschoben (Domänen-Handler darf es aufrufen). Trigger-Aggregat
  `Domain/Auftrag/Ueberweisungsauftrag.cs` (Command `BeauftrageUeberweisung` → Event `UeberweisungBeauftragt`; EIGENER
  Namespace, weil pro Namespace genau EIN Aggregat gilt — sonst bricht die Command→Aggregat-Zuordnung des Generators).
  Handler `Domain.Projections/Ueberweisungen.cs` (`: IPullSubscriber`): reagiert auf `UeberweisungBeauftragt`, yieldet
  `StarteProzess` mit **deterministischer** ProzessId (`ProzessId.Für(nameof(UeberweisungsPlan), auslöser, version)`) —
  technisch eine Reaktion über die (A)-Route; doppelter Auslöser → gleiche ProzessId → Prozess-Aggregat dedupliziert.
  `ProzessTreiberE2ETests.Ein_Fach_Command_treibt_die_ganze_Ueberweisung_end_to_end`: EIN `BeauftrageUeberweisung`
  → UeberweisungBeauftragt → Handler → StarteProzess → Prozess → Treiber (signal-getrieben) → 3 Schritte →
  Abgeschlossen, Konten 100→70/0→30, ~2s. **Kein manuelles Wecken, kein manueller Start.**
  (Der ganz elegante Spec-Weg — Handler yieldet den PLAN statt StarteProzess — braucht die Emit-Signatur
  `IMessagePayload → IPipelineOutput` im `SubscriberDispatchGenerator`; bewusst später.)
- *Increment 5 — Datenfluss zwischen Schritten, Weg A (fertig):* ein Schritt referenziert einen FRÜHEREN über
  dessen deterministische Id. `Abstractions/IProzessPlan.cs` neu: `SchrittRefs` (`Von(k)=ProzessId.FürSchritt(prozId,k)` —
  IDENTISCH zu `MitVorgang`, sonst bräche Crash-Heilung), `Schritt` (`Baue`/`BaueRueckgaengig`/`AbhaengigVon`),
  `Alle → IReadOnlyList<Schritt>`; `.Dann` als Command-**Builder** über aufgelöste Refs + **explizite Kanten**
  (`params int[] abhängigVon`, **rückwärts-only erzwungen** → DAG). Der `ProzessTreiber` löst Refs beim Senden auf
  (vorwärts + Kompensation), bleibt **strikt linear**; Kanten werden **gespeichert, nicht gescheduled** (Andockpunkt
  für den späteren Dataflow-Scheduler: Ready-Set „alle Deps quittiert" ersetzt additiv den linearen Zähler — Plan/
  Aggregat/`IProzessSicht` unverändert). Reservierung **vereinheitlicht id-basiert** (`Konto.OffeneReservierungen`;
  `Buche`/`GebeReservierungFrei` treffen per Id, `ReservierungNichtGefunden` als Ablehnung) → der bestehende
  `UeberweisungsPlan` IST das Beispiel (Schritt 3 = `refs.Von(1)`), ein separater Plan entfiel bewusst.
  **`ProzessAggregatGenerator` unberührt** (liest nur `Alle.Count`) — baut unverändert. Beweise: Prüfstand
  `DatenflussZwischenSchrittenTests` (Köder-Reservierung gleicher Höhe → nur die deterministische Ref trifft die
  richtige; Kompensation-Präzision; Crash@Schritt-3-Heilung), Integration `Datenfluss_…_live` (gebuchtes Event
  trägt live `FürSchritt(prozId,1)`). Kontext (archiviert, Weg A überholt): **docs/archiv/datenfluss-zwischen-schritten-weg-a.md**.
- *Danach:* Crash-Probe live; Treiber + Prozess-Aggregat **generieren** (Spec 15); paralleler Dataflow-Scheduler
  (liest die `AbhaengigVon`-Kanten); Phase 6 (Monitoring/Verkettung/Deadlines).

### ⚠ NEUBAU (aktuell) — die Prozess-Schicht ist jetzt ein Event-Regel-DAG-Manager (docs/prozess-neubau-event-regeln-dag.md)
Die GESAMTE obige Schrittlisten-Schicht (Increments 1–5) ist **gelöscht und ersetzt** durch ein Petri-Netz:
Events = Tokens, Commands = Transitionen. **Ein Prozess ist jetzt typisierte Regeln** (`IProzessDefinition`,
`Prozess<TAuslöser>.Definiere(p => p.Auf<E>().Und<E2>().Sende<Cmd>(…).RückgängigMit(…))`), kein Plan/Schrittliste mehr.
- **Ersetzt (gelöscht):** `IProzessPlan`/`ProzessSchritte`/`Schritt`/`SchrittRefs`/`IProzessSicht`, `ProzessTreiber`/
  `TreiberActor`, `ProzessAggregatGenerator`/`ProzessWiringGenerator`, `GeneratedProzesse/Starts/Handlers`,
  Plan-Yield-Arm im `HandlerOutputRouter`, `UeberweisungsPlan`/`SammelueberweisungsPlan`, `Ueberweisungen.cs` +
  alle alten Phase-5-Tests. **Behalten:** `IProzessSchrittCommand` (Vorgang-Injektion), `ProzessId`, `IProzessIntern`, Konto.
- **Kern:** EIN generischer `ProzessManager` (Infrastructure/Prozess) verschmilzt Aggregat+Treiber; Log = nur
  Entscheidungen (`ProzessGestartet`/`SchrittGescheitert`/`ProzessBeendet`), **Marking bei jeder Weckung aus den
  Ziel-Streams gefaltet** (Ergebnis↔Transition per Vorgang via `IVorgangEvent`, Invariante 1). Feuert FIRE-AND-FORGET
  (`DetachedProzessSend`) → (A)-Hang strukturell weg.
- **Korrelation** reist als `CommandEnvelope.CorrelationId` → Ziel-Event-Metadatum → `KorrelationsRouter` weckt
  `(prozess-manager, Korrelation)`. Konto bleibt REIN (kein Korrelations-Feld). Fehler wird durabel, wo der Manager die
  negative Quittung sieht.
- **Nur EIN Generator:** `ProzessRegelnGenerator` → `GeneratedProzessRegeln.Alle` (DAG-Deskriptor). Infra (Manager-Kind,
  Router, `ProzessManagerStartupService`) ist generisch/handgeschrieben. Host: `AddGeneratedProzesse()`.
- **Live-Lektion (kritisch):** das Ergebnis-Event der LETZTEN Transition ist Auslöser KEINER Regel → sein Signal
  abonniert der Router nicht → nur die **Selbst-Weckung nach erfolgreichem Send** erkennt „terminal". Ohne sie hängt der
  Prozess nach vollendeter Wirkung (Symptom: alle Effekte da, aber kein `ProzessBeendet`).
- **Fan-out/dynamische Breite:** `SendeJe` (N Commands, je eigener Vorgang via Diskriminator=Ziel) + `UndAlle<E>(n)`
  (Count-Join: buche erst nach allen N; Breite aus dem Auslöser, kein Zähler).
- **Azyklizität:** Boot-Guard ist **AKTIV** (`Infrastructure/Prozess/ProzessManagerWiring.cs:80-85`,
  `ProzessAzyklizität.PrüfeAlle`), gespeist aus der PRÄZISEN Command→Event-Map
  `Infrastructure.Mapping.GeneratedCommandRouting.Produziert` (aus den Decide-OneOf-Rückgaben, `CommandAggregateMapGenerator`).
  Die alte aggregat-grobe `GeneratedEventCommandMapping` (namespace-basiert) wird dafür NICHT mehr genutzt.
- **Zweites Beispiel (Diamant):** die **Bestell-Saga** (`Domain/Bestellung/BestellProzess.cs` + Ziel-Aggregate
  `Domain/{Lager,Zahlung,Versand}/`) — aus EINEM Auslöser zwei PARALLELE Zweige (reservieren ∥ belasten), die sich am
  Versand per `.Und<>()`-Join vereinen; Zweig-Kompensation bei ungedecktem Konto. In-memory + live grün. **Anleitung
  für Entwickler: docs/anleitung-prozess-schreiben.md** (was man schreibt vs. was das Framework liefert).
- **✅ Domänen-Reinheit hergestellt (Leak 1 gelöst) — die Aggregate sind jetzt REIN.** Kein `Vorgang`,
  keine `VerarbeiteteVorgaenge`-Dedup-Menge, kein `IProzessSchrittCommand`/`IVorgangEvent`/id-basierte
  Reservierungen mehr (alles gelöscht). Die Idempotenz-Dedup ist eine **Framework-Inbox** im
  `AggregateActorBase`: auf dem `AnyVersion`-Pfad (Reaktion/Prozess) co-committet er eine interne Marke
  `KommandoVerarbeitet(CommandId)` MIT den Domänen-Events (eine Transaktion, exactly-once) und verpufft einen
  wiederholten Command per CommandId. Die Marke ist `IProzessIntern` → `LoadStateAsync` (beide Stores)
  überspringt sie beim Domänen-Falten, zählt sie aber in der Version. Der Manager feuert mit
  `CommandId = vorgang` (deterministisch) und matcht Ergebnisse über `EventEnvelope.CausationId` (der Actor
  stempelt CausationId = CommandId); Korrelation reist über `CorrelationId`. Reservierungen sind
  mengenbasiert. Der OCC-Pfad (Client-Commands, `ExpectedVersion >= 0`) ist byte-genau unberührt.
- **Zählerstände: Prüfstand 53, Integration 11** (alte id-basierte Weg-A-Tests entfielen). Host.Grpc 0 Fehler,
  bootet mit 3 Prozessen. Kontext: **docs/prozess-neubau-event-regeln-dag.md** §13, **docs/anleitung-prozess-schreiben.md**
  (Entwickler-Anleitung, reine Domäne).

### Leitplanken (Lektionen dieser Session — nicht verletzen)
- **Eine Maschine, keine Taxonomie** (Spec 8): kein zweiter Marker, kein „Projektion-vs.-Reaktion"-Zweig —
  Unterschiede fallen aus Ctor-Stores + Rückgabetypen.
- **Idempotenz ist NIE Default**; der Normalfall ist der nicht-idempotente Effekt, Co-Commit der allgemeine Mechanismus.
- **Technologie-agnostisch, nur bereitstellen**: die *Garantie* sagt das **Store-Interface** (`IProjectionTracker`),
  die *Zustellung* der **Handler-Marker** — zwei getrennte Achsen.
- **Keine Konzepte überladen** — im Wesentlichen ändert sich nur der *Transport* unter der bestehenden `DispatchAsync`.
- **Verteilte Hangs in-memory (Fake-Cluster) beweisen**, nie im langsamen, log-versteckenden Integrationstest raten.

## Aktueller Stand

> ⚠ **Hinweis:** Die Phasen-2–4-Beschreibungen unten sind der HISTORISCHE Stand vor dem
> Naht-Neubau (siehe ⚠-Block oben). Dateien wie `ImagePairHistoriePullStartup`,
> `ImagePairHistorieCoCommitStore`, `ImagePairHistorieAdapterKind`, `PullPathExtensions`,
> `ICoCommitProjectionStore`, `CoCommitProjectionAdapter` **existieren nicht mehr** — die Naht
> ist neu, store-agnostisch und generiert (`docs/archiv/entwicklungsplan-projektionsnaht.md`).

**Phase 0 — integriert:** Die Verträge liegen im Projektcode (nicht mehr nur unter
`docs/`): `IEventEnvelope`, `IProjectionTracker` (inkl. Reset), `IProjectionRebuild`
in `Abstractions`; `MartenProjectionTracker` (+`ProjectionCheckpoint`) in
`Infrastructure/Persistence`; `InMemoryProjectionTracker` in `Infrastructure/Testing`.
Die vier Edits sind angewandt: `Interfaces.cs` (+`ReadStreamAsync`), `CommandEnvelope.cs`
(`EventEnvelope : IEventEnvelope`), `MartenEventStore.cs` + `InMemoryEventStore.cs`
(+`ReadStreamAsync`), Marten-Config (`ProjectionCheckpoint`-Schema). Server-Kette baut grün.

**Phase 0.5 — Prüfstand grün:** `Infrastructure.Pruefstand.Tests` fährt die vier
Crash-Proben gegen die echte, append-artige `ImagePairHistorieProjection` (5 Tests, 0
Fehler): verlorenes Signal heilt der nächste Read; doppeltes Signal folgenlos; Effekt+Marke
gemeinsam gültig; Absturz zwischen Effekt und Marke → 4a getrennte Marke = Doppelwirkung
(at-least-once), 4b Co-Commit = genau einmal wirksam. Der Adapter ist in 4a/4b identisch —
die Store-Implementierung entscheidet die Garantie. `dotnet test Infrastructure.Pruefstand.Tests`.

**Phase 1 — Schreibseite: fertig (alle Teilschritte grün, 18 Prüfstand-Tests).**
- *1a-i* Version PRO Event: `EventEnvelopeFactory.BuildPerEvent` (Event i → baseVersion+i+1),
  genutzt in `AggregateActorBase.PublishEventsAsync` statt Batch-Endwert.
- *1a-ii* Metadaten ins Log: `AppendEventsAsync` + optionale correlationId/causationId/aggregateType;
  Marten `MetadataConfig` aktiv (SetHeader `aggregate_type`), `ReadStreamAsync` liest sie zurück;
  InMemoryEventStore führt sie mit.
- *1b* Signal-Typ-Generator: `Domain.SourceGeneration/SignalTypeGenerator` erzeugt pro persistiertem
  Event ein `StateChangeVia{Event}(Guid StreamId, int Version) : IStateChangeSignal` (38 Stück, 1:1);
  neue Registry-Kategorie `GeneratedTypeRegistry.Signals`; DtoMapper schließt Signale aus (nur interne
  PubSub-Ebene, kein Proto).
- *1c* Emit-Wiring: `Infrastructure.SourceGeneration/SignalFactoryGenerator` erzeugt `GeneratedSignalFactory`
  (reflection-freies `evt switch`); der Actor publiziert nach dem Commit zusätzlich das Signal via
  `SignalEnvelope` (best-effort, darf verloren gehen).

**Phase 2 — Rückgrat: läuft.**
- *2a Co-Commit — fertig, gegen ECHTES Postgres (3 Integrationstests):* `ICoCommitProjectionStore`
  (Abstractions), `CoCommitProjectionAdapter` (batch-atomare Kernschleife), `ImagePairHistorieCoCommitStore`
  (eine Marten-IdentitySession → Effekt + `ProjectionCheckpoint` in EINEM SaveChanges). Absturz zwischen
  Effekt und Marke → nichts durabel → Neustart → genau ein Eintrag. `Infrastructure.Integration.Tests`
  braucht laufendes Postgres (docker-compose.infrastructure.yml; lief hier auf localhost:5432).
- *2b Receiver — fertig:* `SignalReceiver` (Kern, Signal→Wake), generierte `GeneratedSignalRoutes.EventToSignal`,
  `SignalReceiverActor` (Proto-Mantel, abonniert Signal-Typen via BrokerSubscription). Kern in-memory getestet;
  `SignalDeliveryClusterTests` fährt einen echten Consul-Single-Node-Cluster hoch und beweist die volle Kette
  Signal → Broker → Receiver → Adapter → Co-Commit in Postgres. Single-node laufen rohe CLR-Signale ohne
  Proto-Serialisierung durch (in-process) — cross-node erst Phase 4.
- *2c Host-Verdrahtung — fertig, läuft in **Host.Grpc**:* `AddImagePairHistoriePullPath()` (nach AddCqrsFramework)
  koppelt den Push-Subscriber der ImagePairHistorie ab (`PushSubscriberExclusions` → SubscriberStartupService
  überspringt ihn) und spawnt `ImagePairHistoriePullStartup` (Hosted Service) → `SignalReceiverActor` auf 10
  Signal-Typen + `CoCommitProjectionAdapter` + `ImagePairHistorieCoCommitStore`. Host bootet sauber verifiziert:
  Log zeigt „– ImagePairHistorieProjection (Push übersprungen)" und „Pull-Pfad: Receiver auf 10 Signal-Typen",
  StateChangeVia-Broker-Shards aktiv, gRPC :5001, keine Exceptions.
- **Live-Durchstich — fertig:** `LiveCommandE2ETests` fährt einen In-Process-Host mit der vollen Host.Grpc-
  Verdrahtung hoch, schickt einen echten `ErstelleImagePair` über `IAggregateDispatcher` (AggregateType="ImagePair")
  und belegt, dass die Historie über den Pull-Pfad wächst. Postgres bestätigt: Historie-Docs in `rm`, Co-Commit-
  Checkpoints in `es`. Die ganze Kette (Command → Event+Signal → Broker → Receiver → Adapter → Co-Commit) läuft live.
- Stores/Adapter: `Infrastructure.Integration.Tests` (Postgres+Consul), `Infrastructure.Pruefstand.Tests` (in-memory).

## Phase 2 — Rückgrat: funktional komplett (Meilenstein)
Das entscheidende Tor ist durch: exactly-once im echten Store, Signalzustellung über echten Broker,
Pull-Pfad live in Host.Grpc.

## Phase 3 — Output-Routing (Reaktionen): fertig (Tor erreicht)
- *3a Empfänger-Garantie:* `Domain/Reaktion/` (`Reaktionsempfaenger` mit **Noop-Decider**, dedupliziert per
  `VerarbeiteteReaktionen`-Set) + `Infrastructure/Aggregate/ReaktionsId.cs` (deterministische Id aus
  (StreamId, Version, Diskriminator)). „Duplikat verpufft" am echten Decider bewiesen.
- *3b Reaktions-Routing:* `SubscriberDispatchGenerator` emittiert jetzt `IMessagePayload` (Cast von OneOf.Value),
  `SubscriberActorGenerator`-Override + `SubscriberActorBase` bekamen einen **Output-Router**: IEvent → publish,
  ICommand → `SendReaktionAsync` (AggregateType aus `GeneratedPipelines.CommandAggregateTypes`, deterministische
  CommandId, `ClusterIdentity` + OCC-Retry). Demo-Handler `Domain.Projections/ImagePairReaktion` (auf ImagePairErstellt
  → `WirkeReaktion`).
- *3c Live:* `ReaktionE2ETests` — echter ErstelleImagePair → ImagePairErstellt → Reaktion → WirkeReaktion →
  Reaktionsempfaenger.ReaktionGewirkt, gegen Postgres/Consul. Tor erreicht.
- Achtung Signaturänderung: `DispatchAsync(..., Func<IMessagePayload,Task> emit)` — betrifft alle Subscriber
  (generiert) + Pull-Adapter-Dispatch-Helfer (No-op-Lambda unverändert gültig).

## Phase 4 — Robustheit (Sharding + Poll): 4a + 4b fertig
- *4a Adapter als Cluster-Actor pro Stream — fertig, live:* `SignalAdapterActor` (virtueller Cluster-Actor,
  Identität = StreamId; auf `Wake` läuft die Co-Commit-Schleife), `IClusterKindContributor` +
  `ImagePairHistorieAdapterKind` (Kind `"imagepair-historie-adapter"`, actor-eigene Co-Commit-Instanz je Stream →
  keine geteilte Session), Registrierung via `AddCqrsActorSystem`-Schleife (vor StartMemberAsync). Der
  `ImagePairHistoriePullStartup`-Receiver leitet `Wake` an `(KindName, StreamId)` statt an einen lokalen Adapter.
  `SignalReceiverActor`/`SignalReceiver` blieben unverändert (nur der Wake-Delegat ändert sich) → Phase-2-Tests grün.
  LiveCommand + Reaktion laufen weiterhin, jetzt über den Cluster-Adapter.
- *4b Poll-Backstop — fertig + live:* globales Leseprimitiv `ReadChangedStreamsAsync` (Marten: globale `Sequence`;
  InMemory: Append-Log) + `Poller`; im `ImagePairHistoriePullStartup` als 30s-Loop verdrahtet, weckt über DIESELBE
  Cluster-Identität wie das Signal → der per-Stream-Actor serialisiert beide (Race gelöst). 3 Prüfstand-Tests.
- *offen — die harte Multi-Node-Hälfte:*
  - *4c cross-node-Serialisierung* — der interne Plane schickt rohe CLR ohne registrierten Serializer (single-node
    in-process ok). Multi-node braucht einen (poly-)Serializer für Wake/WakeAck/SignalEnvelope/CommandEnvelope/Publish/
    EventEnvelope. Nicht-trivial (polymorphe Payloads).
  - *4d Multi-Node-Tor* — Zwei-Member-Test (zwei ActorSystems, ein Consul-Cluster): ein Adapter je Stream, Ordnung
    erhalten, Poll heilt Totalverlust. Fehleranfälligste Testart.

## ⚠ WICHTIG — Proto-Regenerierung bei neuen Domain-Typen
Jeder neue Command/Event/Query/Trigger braucht einen Proto-DTO, sonst bricht der `DtoMapperSourceGenerator`
(auf Infrastructure) den Build (`{Name}Dto nicht gefunden`). Ablauf: `dotnet run --project Proto.SourceGeneration`
(regeneriert `ProtoRepo/domain.proto` aus Domain/Domain.Projections/Domain.Pipeline) → `ProtoRepo` neu bauen
(Grpc.Tools) → Infrastructure baut. Signale sind die Ausnahme (bewusst aus dem DtoMapper ausgeschlossen, nur intern).

Offener Punkt aus 1b: `MessageTypeMapping`/cross-node-Serialisierung der Signale (bestehende Lücke,
betrifft alle Nachrichten) — spätestens für Phase 4.

**Nebenbefund (nicht dieser Umbau):** `Domain.Client` baut derzeit nicht (`_publish` fehlt,
laufendes Client-Generator-Refactoring) — unabhängig von Phase 0/0.5/1, per Revert-Test belegt.

## Phase-1-Lücke — ✅ geschlossen (1a-ii)

`AppendEventsAsync` schrieb früher NACKTE Domain-Events; `CorrelationId`/`CausationId`/
`AggregateType` standen nicht im Log. Behoben: Marten-`MetadataConfig` aktiv, der Actor
setzt beim Append `session.CorrelationId`/`CausationId` + Header `aggregate_type`,
`ReadStreamAsync` rekonstruiert sie. (Marten-Ende-zu-Ende gegen Postgres ist das
Integrations-Tor; der Vertrag ist über den InMemory-Store getestet.)

## Konventionen

- Kommentare/Domäne auf Deutsch (Bestand konsistent halten).
- Neue Verträge nach `Abstractions`; Marten/Infra nach `Infrastructure.Persistence`.
- Nichts mit Runtime-Reflection lösen (Inv. 4). Neue Dispatch-Logik = Generator
  erweitern, nicht Handschalter.
- Vor dem Anfassen des Rückgrats (Phase 2): **docs/spezifikation.md Kapitel 4–7
  lesen**. Für Phasen-Begründungen: docs/entwicklungsplan.md.

## Build / Test

**⚠ Testen & Lasttest: docs/testen-und-lasttest.md ZUERST LESEN.** Drei Ebenen (Prüfstand
in-memory / Integration gegen echte Infra / Last-Harness `LoadHarness/`), plus die realen
Fallstricke: Integration sequentiell lassen, der bekannte `SnapshotLiveE2ETests`-Cold-Boot-Flake
(NICHT an Timeouts drehen), und xUnit schluckt App-Logs (Cluster-Diagnose → Last-Harness `--log debug`).

- Build: `dotnet build`
- Test (Logik, immer grün): `dotnet test Infrastructure.Pruefstand.Tests/Infrastructure.Pruefstand.Tests.csproj`
- Integration (braucht Postgres/Consul/Redis, sequentiell): `dotnet test Infrastructure.Integration.Tests/Infrastructure.Integration.Tests.csproj`
- Last/Durchsatz (bootet EINEN Cluster, kein Flake): `dotnet run --project LoadHarness -- --accounts 500 --credits 40 --concurrency 128 --log warning`
- Infra hochfahren: `docker compose -f deploy-linux/docker-compose.infrastructure.yml up -d`
- Hosts: `Host_Blazor`, `Host_Grpc` (siehe deploy.sh).
