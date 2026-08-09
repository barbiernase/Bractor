# Backend-Neubau — Fahrplan

> Kompakter, umsetzbarer Begleiter zu `docs/backend-neubau-einheitliche-maschine.md` (dort: Herleitung,
> Philosophie, Begründung jeder Phase). Dieses Dokument ist die **Task-/Tor-Liste** zum Abhaken.
>
> **Leitsatz:** Wir bauen die *Backend-Maschine* neu, nicht den Stack. **Festgehalten:** API,
> Source-Generatoren, Marten/PostgreSQL, Redis, Proto.Actor, gRPC, Client-Struktur. **Ziel:** aus einer
> Feature-Sammlung *eine* einheitliche, wartbare Maschine.

## Fortschritt (umgesetzt)

- **A1 entschieden:** EIN `CommandEnvelope` (keine zwei Typen, keine Rückwärtskompat). Der `-1`-Sentinel
  wird durch einen **typisierten Summentyp** `CommandModus { Client(int ExpectedVersion) | Emittiert }`
  ersetzt (EM-2). Vorteil: die Version lebt nur im `Client`-Fall → „interne Emitter behaupten NIE eine
  Version" ist **strukturell**; `required Modus` killt Befund 10.
- **P0 ✅ grün** — Verträge additiv: `ICommandEmitter`+`EmitKausalität`, `IReplaybarerTracker`,
  `IEmittentenCursor`; `MartenProjectionTracker : IReplaybarerTracker`. Prüfstand 38/38.
- **P2 ✅ grün (gegen echtes Marten/Consul)** — `AnyVersion` gelöscht, `CommandModus` eingeführt; Actor mit
  zwei Eingängen `HandleClientCommand`/`HandleEmittedCommand` (dispatch per `switch (Modus)`); Befund 5
  (Live-Apply `is not IProzessIntern`) gefixt; alle Sender + DTO-Generator + Tests migriert; Proto
  unverändert. **Integration 25/25 (= Baseline-Oracle), Prüfstand 38/38.**
- **P3 ✅ grün** — Emit-Primitiv `CommandEmitter` (Infrastructure.PubSub): EINE deterministische Id-Ableitung
  `EmitId.Ableiten(EmitKausalität, ziel)` (ersetzt `ReaktionsId.For`), `Modus.Emittiert`, **bounded Token**
  (W2), Send-Seam (Fake-Cluster-testbar). **Reaktion (`HandlerOutputRouter`) und Pipeline
  (`PipelineActorBase.SendCommandAsync`) migriert** — die Pipeline-Retry-Schleife mit zufälliger CommandId +
  `CancellationToken.None` ist **gelöscht** (W1/W2 strukturell weg). Beweise: `EmitPrimitivTests` (W1: Re-Emit
  trägt dieselbe det. Id; W2: nie zurückkehrender Send bounded). **Prüfstand 43/43, Integration 25/25.**
  - *Bewusst offen (dokumentiert):* der **Prozess-Treiber** (`ProzessManagerActor.SendeAnZiel`) sendet noch
    eigenständig — er BRAUCHT die Quittung für `WeckeSelbst`/`MeldeFehlschlag`; Fold ins Primitiv = **P5**.
    Der **Pipeline-Trigger**-Pfad (`SendTriggerAsync`, `CancellationToken.None`) + die toten OCC-Helfer
    (`ResolveVersion`/`MaxRetries`/`DeadLetterAsync`) = **P6**. Analyzer (A6) folgt mit P5 (dann ist EM-1 voll).
- **P5 · Treiber-Fold, Scheibe A ✅ grün** (der gekoppelte Kern, `docs/handoff-treiber-fold.md` §7). Die
  Fehlschlag-Erkennung bekommt eine **fold-basierte** Quelle NEBEN der Quittung (additiv → safe & isoliert):
  neuer durabler Marker `KommandoAbgelehnt(CommandId, Grund) : IEvent, IProzessIntern` (neben
  `KommandoVerarbeitet`); der Actor **co-committet** ihn auf dem EMITTIERTEN Ablehnungs-Pfad
  (`CoCommitAblehnungsMarkeAsync`, eine Transaktion, Client-/OCC-Pfad unberührt). **Zwei-Mengen-Inbox**
  (`_verarbeiteteCommandIds` **+** `_abgelehnteCommandIds`, beide `BoundedInbox`, beide im Snapshot
  `ProcessedCommandIds`/`RejectedCommandIds` und in `AggregateRehydrator` gefaltet): Re-Delivery eines
  abgelehnten Vorgangs liefert **konsistent Success:false**, NIE Success:true (schließt den §4-Falsch-Erfolg).
  Neue Fold-Achse `AbgelehntDa` im `ProzessManager.Kandidat`; `WakeAsync` stempelt daraus **vor** dem
  Vorwärts/Kompensations-Split ein durables `SchrittGescheitert` (die Kopplung §4: Marker + Zwei-Mengen-Inbox
  + Fold-Achse landen ZUSAMMEN, sonst läse der Vorwärtszweig den Marker als `ErgebnisDa` → `ProzessBeendet(true)`).
  **Treiber sendet in Scheibe A NOCH über die Quittung** (idempotent gegen den Fold: `Gescheitert.ContainsKey`).
  Beweis: store-freier Contract-Guard `AblehnungsMarkeTests` (der Marker ist `IProzessIntern` → Domänen-Fold-Skip
  + Proto-Ausschluss + Fold-Diskriminator) + die **volle Saga-Suite als No-Regression-Oracle**. **Prüfstand
  54/54, Integration 25/25** (BestellSaga-Kompensation grün; SnapshotLive-Flake diesen Lauf grün).
- **P5 · Treiber-Fold, Scheibe B ✅ grün** (der eigentliche EM-1-Abschluss): der Treiber sendet jetzt
  **fire-and-forget über das EINE Emit-Primitiv** `CommandEmitter` — **genau ein Emit-Weg**, keine Quittung mehr.
  Neue `CommandEmitter`-Überladung `EmitAsync(cmd, commandId, korrelation, ct)` (§5 Weg a: der deterministische
  `vorgang` IST die vorgegebene CommandId → der Fold-Match `CausationId == vorgang` bleibt unverändert).
  `SendeAnZiel` (rohes `RequestAsync<CommandResult>`), `MeldeFehlschlagAnManager`, die `MeldeFehlschlag`-Message
  und `ProzessManager.NotiereFehlschlagAsync` **gelöscht**; `DetachedProzessSend` auf **emit + `danach`** reduziert
  (`WeckeSelbst` nach JEDEM Send — Erfolg, Ablehnung, Timeout — im `finally`, sonst hinge ein abgelehnter Schritt
  bis zum Poll-Backstop, weil die Ablehnungs-Marke als `IProzessIntern` kein Signal erzeugt). Die Fehlschlag-
  Erkennung trägt jetzt **allein der Fold** (§6: von *sofort* auf *eventual* verschoben; `WeckeSelbst` +
  `ProzessOffenIndex` bleiben, A2). **Nachzug** umgesetzt: `NächsteKompensationAsync` wertet eine
  `KommandoAbgelehnt`-Marke auf dem Kompensations-Ziel als *unvollziehbar* (KlärungNötig, #12), NICHT als
  „erledigt" — da die Quittung dort ebenfalls entfällt. Beweise: `DetachedProzessSendTests` neu (Turn kehrt sofort
  zurück bei nie-zurückkehrendem Emit; `WeckeSelbst` nach Erfolg UND wenn der Emit wirft), `EmitPrimitivTests`
  (+Überladung stempelt `vorgang`), **volle Saga-Suite grün OHNE Quittung**. **Prüfstand 55/55, Integration 25/25**
  (BestellSaga-Kompensation + Gesperrtes-Zielkonto + Backstop + ReiseSaga(-Parallel) + SnapshotLive grün).
  *Ehrliche Einordnung:* der **KlärungNötig-Pfad** (Kompensation SELBST abgelehnt) ist korrekt-per-Konstruktion,
  aber **nicht integration-gedeckt** (kein Test provoziert eine abgelehnte Kompensation). **EM-1 ist damit im
  Kern erfüllt** (genau ein Emit-Weg).
- **P5 · Treiber-Fold, Scheibe C ✅ grün (Analyzer A6 — EM-1 ERZWUNGEN).** Neuer Roslyn-`DiagnosticAnalyzer`
  `CommandEmitAnalyzer` (in `Infrastructure.SourceGeneration`, läuft automatisch auf `Infrastructure` via die
  bestehende Analyzer-Referenz — kein Extra-Wiring): **CQRS020** = Build-Fehler bei einem rohen
  `cluster.RequestAsync<CommandResult>`-Send AUSSERHALB der zwei legitimen Sender (`CommandEmitter` = Emit-Weg,
  `ProtoActorAggregateDispatcher` = Client-/OCC-Pfad); **CQRS021** = Build-Fehler bei `CancellationToken.None`/
  `default` auf einer Command-Kante (unbounded, W2 — gilt auch INNERHALB der erlaubten Typen als Regressions-
  Riegel). Präzise: syntaktischer Vorfilter (`RequestAsync<T>`) + semantische Bestätigung `T == Abstractions.
  CommandResult` → der Pipeline-**Trigger**-Pfad (anderer Ergebnistyp, bewusst noch `None`) fällt NICHT darunter
  (bleibt P6). Beweise: (a) End-to-end-Demonstration — eine temporäre Probe-Datei in `Infrastructure` erzeugte
  exakt CQRS020 (roh) + CQRS021 (None) bzw. NUR CQRS020 (bounded), danach gelöscht → 0 Fehler; (b) durabler
  Regressions-Guard `CommandEmitAnalyzerTests` (3, eigene `CSharpCompilation`): CQRS020+021 bei rohem None-Send,
  nur CQRS020 bei bounded, sauber beim erlaubten `CommandEmitter`. **Prüfstand 58/58, Integration 25/25.**
  **EM-1 ist damit voll erfüllt UND erzwungen.** *Offen (später):* P4 (Konsum-Maschine), P6 (Pipeline-Zerlegung +
  Trigger-`None` + tote OCC-Helfer), Feature-Strom.
- **P5(a) ✅ grün** — präzises `CorrelationId`-Poll-Routing. Befund: der Router (`RouteAsync`) routet ohnehin
  jedes Event mit parsebarer Korrelation; der Terminal-Bug saß allein im Poll-**Typ**-Filter
  (`ProzessManagerWiring.cs:154`). Fix **additiv** (`ProzessPollFilter.SollRouten`): route ein geändertes
  Stream-Event auch, wenn seine Korrelation zu einem OFFENEN Prozess gehört → das terminale Ergebnis-Event
  (Auslöser keiner Regel) wird jetzt **event-getrieben & präzise** geroutet, statt es dem Brute-Force-
  Backstop zu überlassen; **kein Über-Wecken** fremder Korrelationen. Kosten: 1× `ListeOffeneAsync` je
  Poll-Zyklus. Beweis Ebene-1 `ProzessPollFilterTests` (4). **Prüfstand 47/47; Integration: Prozess/Saga/
  Reaktion 13/13 grün** (einziger Ausfall = der dokumentierte SnapshotLive-Cold-Boot-Flake, bimodal
  bestätigt, prozess-unabhängig).
  - *Ehrliche Einordnung:* die Terminal-**Korrektheit** war schon durch den `ProzessOffenIndex`-Backstop
    (15s All-Scan) abgedeckt; P5(a) macht sie **präzise/event-getrieben** und legt die Grundlage, den
    Brute-Force-Backstop später zu entlasten. **Beide Netze bleiben** (A2): `WeckeSelbst` (Latenz auf dem
    Happy-Path) + `ProzessOffenIndex` (fully-stalled ohne Stream-Änderung). Bewusst NICHT retired.
- **P1a ✅ grün** (erste Scheibe von P1, TG-1). Befund: die grobe `GeneratedEventCommandMapping` hatte nur
  EINEN lebenden Konsumenten — den Blazor-Client-Capabilities-Pfad (`CapabilitiesHandler` →
  `MessageTypeMapping.GetAllowedCommandNames`, Event→erlaubte Commands = Aggregat-Geschwister); die Fassade
  `EventCommandMapping.cs` war toter Code. Umgesetzt: `MessageTypeMapping` leitet event→Geschwister-Commands
  jetzt **präzise aus `GeneratedCommandRouting` (CommandToAggregate + CommandToEvents)** ab, nicht mehr aus
  Namespace-Gruppierung. **Gelöscht:** `EventCommandMappingGenerator` + `GeneratedEventCommandMapping` +
  die tote Fassade. Beweis: `EventCommandDerivationTests` (Konto-Event → Konto-Commands, keine Fremd-Commands).
  **Prüfstand 49/49, Integration 25/25.**
- **P1b ✅ grün** — `Event→Signal` typ-getrieben. Neuer generischer Marker `IStateChangeSignal<TEvent>`
  (Abstractions); `SignalTypeGenerator` erzeugt `StateChangeVia{X} : IStateChangeSignal<X>`; der
  `SignalFactoryGenerator` paart Event↔Signal jetzt aus dem **Typ-Argument** statt per Namens-Präfix
  „StateChangeVia" + Namespace-Namenslookup (beides gelöscht). Der Signal-Name bleibt lesbar, ist aber nicht
  mehr Ableitungsquelle. Beweis: `SignalEmitTests` (Signal implementiert `IStateChangeSignal<ImagePairInspiziert>`;
  Factory/Registry weiter korrekt). Grep: keine Präfix-Ableitung mehr. **Prüfstand 50/50, Integration 25/25.**
- **P1c ✅ grün (Kern) — TG-3-Tor erfüllt.** Neue optionale Attribute `[AggregatName]`/`[ProzessName]`
  (Abstractions). **Detektion (das Tor):** zwei Aggregate/Prozesse mit gleicher aufgelöster Identität brechen
  den **Build** (CQRS011 / CQRS012) mit klarer Meldung statt still die Laufzeit — per temporärer Kollisionsprobe
  bewiesen (CQRS011 feuerte). **Konsistenz (footgun-frei):** `[AggregatName]` fließt in BEIDE Identitäts-Quellen
  — Routing (`CommandAggregateMapGenerator`) **und** ClusterKind (`AggregateActorGenerator`, war `nameof`) —
  kein Routing↔Kind-Mismatch. `[ProzessName]` ist ein **voller** Resolver (Prozess-Name ist nur ein
  Korrelations-String). Default unverändert = einfacher Typname → **keine Migration**; No-Regression 50/50 /
  24-25 (nur SnapshotLive-Flake).
  - *Ehrliche Rest-Punkte (Follow-up, nicht Tor-relevant):* der Actor-**Klassenname** bleibt `{TState.Name}Actor`
    → zwei GLEICHNAMIGE Aggregate koexistieren noch nicht (CS0101, aber sauber von CQRS011 abgefangen) — echte
    Koexistenz braucht Klassennamen-Disambiguierung. Der `aggregate_type`-**Header** (`AggregateActorBase.cs:310`,
    `typeof(TState).Name`) ist noch der Typname, nicht das Attribut (informativ, kein Routing). **P1d optional:**
    `AggregateHandlerGenerator` volle OneOf-Typargumente (geringer Nutzen).
- **P1 (Kanten-Graph) damit im Kern abgeschlossen** — jede Kante signaturgetrieben, Identitäts-Kollision =
  Build-Fehler.
- **P6.1 ✅ grün** — Pipeline-Actor entrümpelt + Trigger-Kante gebändigt. Toter OCC-Ballast aus
  `PipelineActorBase` entfernt (mit P3/EM-1 obsolet): `MaxRetries`, `ResolveVersion`, `DeadLetterAsync`, der
  write-only `_versionCache`+`TrackVersion`, der nie aufgerufene `IDeadLetterSink`-Ctor-Param (Generator an 3
  Stellen entsprechend angepasst). `SendTriggerAsync` sendete mit `CancellationToken.None` (W2) → jetzt bounded
  (5s) + `OperationCanceledException` sauber behandelt. **Prüfstand 58/58, Integration 24/25** (nur SnapshotLive-Flake).
- **P4.1 ✅ grün** — `IEmittentenCursor` real: `EmittentenCursorDoc` (Abstractions), `MartenEmittentenCursor`
  (best-effort, eigene Session, KEIN Co-Commit), `InMemoryEmittentenCursor` (Infrastructure.Testing), DI + Marten-
  Schema. Rein additiv (kein Konsument). **Prüfstand 62/62 (+4), Live-Boot grün.**
- **P4.2 ✅ grün** — die Maschine wählt die Marke nach **Achse B** (Compile-Zeit-Schnitt). `ProjectionAdapter`/
  `SignalAdapterActor` tragen jetzt REPLAYBAR (`IProjectionTracker`, Reset) ODER EMITTIEREND (`IEmittentenCursor`,
  KEIN Reset) — zueinander exklusiv (Konstruktions-Guard). Der `PullPathGenerator` entscheidet: Store mit
  `IProjectionTracker` → replaybar; sonst → emittierend + Cursor aus DI. **Korrektheit:** der Reaktions-Emit ist
  detached fire-and-forget → der Cursor rückt nur auf dem **Signal-Pfad** vor (O(Tail)); die **Poll-Weckung heilt
  bewusst ab 0** (neues `Wake.VomPoll`) → at-least-once bleibt EXAKT erhalten (verlorener Emit re-emittiert vom
  30s-Poll, Empfänger-Inbox 10k dedupliziert). **Prüfstand 64/64 (+2), Integration 24/25.**
- **P4.3 ✅ grün** — GA-1-Check (Boot-/DI-Zeit): `IAppendProjektion`-Opt-in-Marker; `GaEinsPruefung.PrüfeCoCommit`
  bricht, wenn eine append-artige Projektion keinen Co-Commit-`IProjectionTracker` mitbringt (in die generierte
  Kind-Factory verdrahtet). `ImagePairHistorieProjection` markiert (ihr Store IST Tracker → passt). Bewusst
  DI-Check statt Roslyn (Domain.Projections referenziert nur Domain.SourceGeneration als Analyzer). **P4 damit
  im Kern vollständig. Prüfstand 67/67 (+3), Live-Boot grün.**
- **Offen für später:** **P6.2** (Event-Pfad-Fold — präziser Plan: `docs/handoff-p6.2-event-pfad-fold.md`;
  braucht zuerst einen Pipeline-Test-Harness); P5(b) Marking-Cursor; P7/P8.

> **Test-Infra (diese Session eingerichtet):** `scripts/dev-infra-setup.sh` — native Postgres/Redis/Consul (kein
> Docker-Hub-Zugang) + .NET 10 SDK (baut/läuft `net9.0` per `DOTNET_ROLL_FORWARD=LatestMajor`, da .NET 9 EOL).

## Legende

- **Tor** = messbare Abnahmebedingung; das System ist nach jeder Phase grün.
- **Ebene 1** = Prüfstand (in-memory, Fake-Cluster) · **Ebene 2** = Integration (echtes Marten/Consul/Redis,
  *sequentiell*) · **Ebene 3** = Last-Harness.
- Invarianten-Kürzel: **TG** = technischer Graph, **EM** = Emit, **GA** = Garantie (siehe Leitdokument §5).

## Abhängigkeitsgraph der Phasen

```
  P0 Verträge ─┬─► P1 Kanten-Graph ───────────────────────────► P7 Transport/K1 ─► P8 Multi-Node
               └─► P2 Schreiber+Inbox ─► P3 Emit ─┬─► P4 Konsum-Maschine ─► P6 Pipeline
                                                   ├─► P5 Prozess-festklopfen   (braucht P4 NICHT)
                                                   └─► Feature-Strom             (orthogonal)
```

**Kein Zug, ein DAG.** Fundament **P0–P3** zuerst und isoliert. Nach P3 verzweigen *vier unabhängige
Ströme*: **P4→P6** (Vereinheitlichung, de-scoped), **P5** (Prozess-Terminal-Fix — hängt an **P3**, nicht
an P4), **Feature-Strom** (orthogonal), **P7→P8** (Multi-Node — hängt nur an P1). Die Nummerierung ist
keine strikte Ausführungsreihenfolge. **Reihenfolge-Prinzip (Zielbild §10/§13):** erst Emit+Bugfixes, dann
die hochwertigen risikoarmen Fixes (P5/Features), die große Vereinheitlichung (P4) *zuletzt* — sie ist
**keine** Voraussetzung für P5 oder Features.

---

## Phase 0 — Verträge & Graph-Fundament
**Zweck:** Grundlage ohne Verhaltensänderung.

- [ ] `ICommandEmitter` + `EmitKausalität(Guid Korrelation, Guid Ursache, string Diskriminator)` in `Abstractions`.
- [ ] Schreiber-Eingänge entwerfen: `HandleClientCommand(expectedVersion)` / `HandleEmittedCommand(commandId)`.
- [ ] `AnyVersion`-Sentinel als „zu entfernen" markieren (noch nicht löschen).
- [ ] Marken-Interfaces `IReplaybarerTracker` / `IEmittentenCursor` in `Abstractions`.

**Tor:** kompiliert; alle bestehenden Tests unverändert grün.

---

## Phase 1 — Kanonischer Kanten-Graph (Generatoren) · TG-1, TG-3
**Zweck:** eine signaturgetriebene Ableitung pro Kante; kollisionsfreie Identitäten.

- [ ] `GeneratedEventCommandMapping` (namespace-grob) löschen.
- [ ] Konsumenten (`MessageTypeMapping`, Proto/`EventCommandMapping`) auf präzises `GeneratedCommandRouting` umstellen.
- [ ] `AggregateHandlerGenerator`: volle OneOf-Typargumente lesen (statt nur Bool `ReturnsOneOf`).
- [ ] `Event→Signal` aus Typ/Marker ableiten statt Namens-Präfix `StateChangeVia{X}`.
- [ ] Knoten-Identität auf FQN oder `[AggregatName]`/`[ProzessName]`-Attribut; „pro Namespace ein Aggregat"-Konvention entfernen.

**Tor (Ebene 1):** kein Generator nutzt Namespace-Gruppierung/Namens-Präfix; keine zwei Generatoren erzeugen
dieselbe Relation; CQRS001/002/003/010 grün; ein bewusster Namens-Kollisionstest (gleicher Typname, zwei
Namespaces) bricht *nicht* die Laufzeit.

---

## Phase 2 — Der Schreiber (P1) + Inbox · GA-1 (Aggregat-Commit-Punkt)
**Zweck:** zwei explizite Eingänge; Co-Commit in *einer* Marten-Transaktion.
**Der Sentinel bleibt hier noch** — seine Löschung ist an die Sender-Migration in P3 gekoppelt.

- [ ] `HandleClientCommand`/`HandleEmittedCommand` umsetzen — *neben* dem alten `AnyVersion`-Pfad (koexistieren bis P3).
- [ ] Inbox-Co-Commit: `KommandoVerarbeitet` + Domänen-Events in *einem* `SaveChangesAsync`.
- [ ] `is not IProzessIntern`-Filter im Live-Apply angleichen an die Rehydration (Symmetrie).

**Tor (Ebene 1+2):** OCC-Pfad byte-genau unverändert (Regressionsvergleich); Emit-Pfad exactly-once am
Empfänger (Fake-Cluster, verlorene Quittung); Co-Commit = *eine* Transaktion (Ebene 2 gegen Marten).

---

## Phase 3 — Das Emit-Primitiv · EM-1, EM-2 (W1/W2 strukturell weg)
**Zweck:** vier Emit-Pfade → einer **und** der Sentinel stirbt.

- [ ] `ICommandEmitter`-Impl: deterministische CommandId aus `EmitKausalität`, bounded Token, at-least-once.
- [ ] **Alle** Sender migrieren: Dispatcher-intern, `HandlerOutputRouter`, `ProzessManager`/`DetachedProzessSend`, `PipelineActorBase` → über das Primitiv auf `HandleEmittedCommand`.
- [ ] Erst **danach**: `AnyVersion` löschen; `PipelineActorBase`-Retry-Schleife (zufällige CommandId + `CancellationToken.None`) entfernen.

**Tor (Ebene 1):** Grep findet keinen zweiten `RequestAsync<CommandResult>` außerhalb des Primitivs, kein
`CancellationToken.None` auf Command-Kanten, kein `AnyVersion` mehr; **Pipeline dedupliziert (W1)** und
**hängt nicht (W2)** — Fake-Cluster-Test mit verlorener Quittung. *(Pipeline-Tor transitorisch — der
Event-Pfad wird in P6 in Reaktionen gefaltet.)*

---

## Phase 4 — Konsumenten-Maschinenklasse (zwei Achsen) · GA-1 (Projektions-Commit-Punkt)
**Zweck:** Projektion/Reaktion/Pipeline-Event auf *einer* Klasse. *(Hängt an P3; de-scoped, keine
Voraussetzung für P5/Features.)* **Riskanter Schnitt** — bei Bedarf Ausprägung für Ausprägung migrieren.

- [ ] Lese-Falt-Emit-Schleife parametrisieren: Achse A (Ein-Strom | Korrelations-Multistrom) × Achse B (Replaybar | Emittierend).
- [ ] Kind + Marken-Interface je Achsen-Kombination generieren (`IReplaybarerTracker` vs. `IEmittentenCursor`).
- [ ] **GA-1-Build-/DI-Check umsetzen:** append-artige Projektion ohne Co-Commit-`IReplaybarerTracker` → Build-Fehler.
- [ ] Marker-API unverändert (`ISubscriber`+`Handle`, `IPipelineHandler`+`Handle`, `IProzessDefinition`).

**Tor (Ebene 1+2):** alle Ein-Strom-Konsumenten auf derselben Maschinen-Klasse; `Reset` nur bei
`IReplaybarerTracker` (Compile-Zeit-Schnitt); der GA-1-Check bricht eine bewusst tracker-lose
Append-Projektion; Projektions-/Reaktions-E2E grün.

---

## Phase 5 — Prozess festklopfen (Terminal-Fix + Marking-Cache) · hängt an P3, NICHT an P4
**Zweck:** Marking aus dem Log; Cursor als Cache; Terminal ohne `WeckeSelbst`. **Der hochwertigste,
risikoärmste Korrektheitsgewinn — früh und unabhängig von der Vereinheitlichung.** (Die Migration des
Managers *auf* die P4-Maschinenklasse ist eine optionale Folge-Konsolidierung, kein Teil dieses Tors.)

- [ ] (a) `CorrelationId`-Poll-Routing: Typ-Filter (`relevanteTypen`, `ProzessManagerWiring.cs:154`) fallen lassen, per `CorrelationId`-Metadatum routen.
  - **Tor:** Terminal erkannt ohne `WeckeSelbst`; `ProzessBackstopE2ETests` grün → `WeckeSelbst` + `ProzessOffenIndex` retirable.
- [ ] (b) Marking-Cursor als **Cache** (best-effort, außerhalb der Entscheidungs-Transaktion; kein Co-Commit).
  - [ ] **Verdichtete Darstellung zwingend:** Count-Join = Zähler+Done-Set, Fan-out = Done-Set der Zweige — **nicht** alle Tokens roh (sonst kehrt O(N²) zurück).
  - **Tor:** Sagas grün; O(N²)→O(N) (Read-Zähler); Voll-Fold heilt Cache-Verlust/`RegelHash`-Mismatch.
- [ ] `ErgebnisDa`/`WirkungDa`-Zwei-Achsen-Marke bleibt (S15-Schutz).

---

## Phase 6 — Pipeline ehrlich zerlegen · hängt an P4
**Zweck:** kein gepushter Event-Konsument mehr.

- [ ] Event-Pfad (Kanal 2) in die Konsumenten-Maschine falten (Reaktion, Ein-Strom/Emittierend).
- [ ] Trigger-Ingress (Kanal 1) als dünner Push-Adapter über das Emit-Primitiv; `ScheduleSelf`/Self-Messages lokal.
- [ ] `IPipelineTrigger`/Timer/Webhook-Registrierung verdrahten.

**Tor (Ebene 1+2):** kein `BrokerSubscription`-Event-Abo außer im Signal-Receiver; die drei Domänen-Pipelines
(`Benchmark`, `FileWatch`, `ImageProcessing`) laufen unverändert über ihre Handler-API.

---

## Feature-Strom (orthogonal, hängt nur an P3) — aus Zielbild §11
**Zweck:** die benannten Feature-Lücken auf der sauberen Emit-Grundlage; **kein** Teil der Vereinheitlichung.

- [ ] **DLQ-Replay** (Ops-/Read-Pfad auf `dlq`).
- [ ] **Timer/Webhook-Trigger** + `ITriggerRegistration` verdrahten (Trigger-Ingress bleibt Push).
- [ ] **Projektions-Rebuild-Runner** (Vertrag + `Reset` da; leicht nach P4 — *eine* Rebuild-Schleife).
- [ ] **Deadlines/Timeouts** (nach stabilem Prozessmodell — Timer-Token/Zeit-Event).
- [ ] **Prozess-Verkettung** (Modell ok; braucht Test/Beispiel).
- [ ] **Monitoring** (Metrics/Tracing/HealthChecks/Prozess-Sicht; profitiert von der uniformen Maschine).

---

## Phase 7 — Transport in den Graphen · TG-2 / K1 (orthogonal)
**Zweck:** interne Nachrichten als Graph-Knoten; generierter Poly-Serializer.

- [ ] Interne Typen (`CommandEnvelope`, `EventEnvelope`, `SignalEnvelope`, `Wake`/`WakeAck`, `Publish`/`Ack`, `Subscribe`, `ProzessWake`/`MeldeFehlschlag`) in die Typ-Registry.
- [ ] Poly-Serializer generieren (reflexionsfrei, über die Registry) und am `WithRemote`-Punkt (`CqrsServiceExtension.cs:335-340`) registrieren.
- [ ] Boot-Check: jeder interne Typ hat einen Serializer → sonst Start-Abbruch.
- [ ] Laute Fehler: `_ = RequestAsync`-Stellen fangen + dead-lettern statt stillem Drop.
- [ ] Broker: Subscriber per `ClusterIdentity` statt lokaler PID; Abo-Entscheidung (durable vs. Poll-heilt) treffen.

**Tor (Ebene 1):** Round-trip-Test über *jeden* internen Typ grün (serialisiert+deserialisiert = gleich);
Boot bricht bei fehlendem Serializer.

---

## Phase 8 — Multi-Node-Tor (Verifikation)
**Zweck:** der eigentliche Rest-Aufwand — beweisen, nicht coden.

- [ ] Zwei-Member-Test: zwei ActorSystems, ein Consul-Cluster.

**Tor (Ebene 2, zwei Nodes):** ein Adapter je Stream; Ordnung erhalten; Poll heilt Totalverlust cross-node.

---

## Unabhängige Bugfixes (jederzeit, entkoppelt)

- [x] **Befund 9 ✅** `BrokerIdentity.GetShardIndex`: `(hash & 0x7FFFFFFF) % ShardCount` statt `Math.Abs(hash)` (`BrokerIdentity.cs:63`). Guard-Test `BrokerIdentityTests`.
- [x] **Befund 7/8 ✅** Fan-out-Diskriminator: **RegelIndex + Instanz-Index** in den `Vorgang`-Diskriminator (`ProzessManager.cs`, Vorwärts + Kompensation) — zwei Regeln gleicher Kausalität kollidieren nicht (8), Fan-out ans selbe Ziel bekommt distinkte Vorgänge (7). No-Regression: Saga/Prozess 11/11 (inkl. Fan-out + Kompensation).
- [ ] Gemischtes Decider-Ergebnis (Effekt + Ablehnung): bereits als Fail-fast behandelt — beim Neubau als Contract festschreiben (`AggregateActorBase.cs:257`).

## Querschnitts-Regeln (während des ganzen Umbaus halten)

- Adressierung nur per `ClusterIdentity` — nie lokale PID.
- Alle Wahrheit + alle Co-Commits in *einem* Marten; Redis nur abgeleitet.
- Interne Messages sind reine Daten — kein `Func`/Delegate/`Task`/Closure im Payload (Seam als Dependency ist ok).
- Zeit-Entscheidungen per DB-Uhr, nie per Node-`DateTime.UtcNow`.
- Korrektheit aus dem Log (OCC/Idempotenz/Poll), nie aus „ist eh derselbe Prozess".
- Serializer-Round-trip-Test früh mitlaufen lassen.
- Integrationstests immer *sequentiell* (`Infrastructure.Integration.Tests/xunit.runner.json`).
