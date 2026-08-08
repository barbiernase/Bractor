# Übergabe: Reaktionen auf den Pull-Adapter (Schritt A)

> **✅ STATUS: ERLEDIGT.** Reaktionen laufen auf dem Pull-Adapter — kein Hang. Umgesetzt genau nach
> dem hier entschiedenen Weg (fire-and-forget via neuem `DetachedEmit.Wrap`, `system.Cluster()` in der
> Spawn-Factory, `HandlerOutputRouter` unverändert). Marker: `ImagePairReaktion : IPullSubscriber`.
> Beweis: `Infrastructure.Pruefstand.Tests/Phase3/ReaktionAufPullTests.cs` (in-memory, 2 Tests) +
> `ReaktionE2ETests` auf Pull (Signal-Pfad, 2s). Integrationstests jetzt sequentiell
> (`xunit.runner.json`, `parallelizeTestCollections:false`) — 4 echte Cluster parallel = Timing-Flakiness.
> Details siehe CLAUDE.md ⚠-Block. Nächster Schritt: (B) Push-Dispatch-Treiber löschen (Endzustand Spec 8).
> Das Dokument unten bleibt als Begründungs-Kontext erhalten.

Handover für die neue Session. Ziel: Reaktionen (und später alle Handler) auf **denselben
Pull-Adapter** heben wie die Projektionen — die EINE Maschine (Spec 8). Das ist zugleich die
Voraussetzung für den Prozess-Treiber (Gesamtplan Phase 5).

Grundlage: `docs/entwicklungsplan-projektionsnaht.md` (die neu gebaute Naht, Phasen 1–5) und
`docs/spezifikation.md` Kap. 4–9.

---

## 0. Mentales Modell (in drei Sätzen — zuerst lesen)

Das Framework tauscht **nur die Event-Quelle** unter dem bestehenden Handler-Aufruf aus:
Push (Broker, at-most-once) → **Pull** (Log-Read ab Marke, geordnet, vom Poll geheilt). Handler,
`DispatchAsync`, `emit` und Deps bleiben identisch — pull ergänzt genau **eine** durable Sache:
die Marke. Zwei orthogonale Achsen: **Transport** (Marker `IPullSubscriber` → Pull) und **Garantie**
(Store implementiert `IProjectionTracker` → exactly-once; sonst at-least-once).

---

## 1. Ist-Stand (GRÜN — nicht ohne Grund anfassen)

Phasen 1–5 der Naht sind fertig und grün gegen Docker (Postgres/Consul/Redis):
Prüfstand 35/35; Integration: CoCommitPostgres (3), SignalDeliveryCluster, LiveCommandE2E
(voller generierter Pull-Pfad inkl. `rm:`-Deps-Index in Redis **DB 1**), ReaktionE2E (auf **Push**).

Die generischen Bausteine (Framework, domänenfrei):
- `Infrastructure/Projections/ProjectionAdapter.cs` — die eine 7.3-Schleife (Marke lesen → Guard →
  dispatch → Deps → Marke). `IProjectionTracker?` (null → ab 0), optionaler `IReadModelDepsSink`.
- `Infrastructure/Projections/SignalAdapterActor.cs` — der Adapter als **virtueller Cluster-Actor**
  pro Stream. Factory liefert `(projectionId, IProjectionTracker?, dispatch)`; projectionId zur
  **Laufzeit** aus `projection.SubscriberId` (der Generator sieht die referenzierte DLL nur als Metadaten).
- `Infrastructure/Projections/PullPath.cs` — `PullPathRegistration` + `GenericPullStartupService`
  (spawnt pro Registrierung Receiver + Poller). Domänenfrei.
- `Infrastructure.SourceGeneration/PullPathGenerator.cs` — selektiert per **`IPullSubscriber`**,
  emittiert pro Handler Kind-Contributor (Tracker + Dispatch rein über DI, Store-Impl-Typ nie genannt)
  + `PullPathRegistration` + `PushSubscriberExclusions` + `AddGeneratedPullPaths()`.
- `Abstractions/IPullSubscriber.cs` (Transport-Marker), `Abstractions/IProjectionTracker.cs`
  (Garantie-Naht am Store), `Abstractions/ProjectionCheckpoint.cs`, `Core/IReadModelDepsSink.cs`.
- `Domain.Infrastructure/ImagePairHistorieStore.cs` — der Co-Commit-Store (Puffer/Flush, Transient).

**Schon vorbereitet für (A):** `Infrastructure/PubSub/HandlerOutputRouter.cs` — das Emit-Routing
(`ICommand → Reaktion` mit deterministischer CommandId + OCC-Retry; `IEvent → publish`) ist bereits
aus `SubscriberActorBase` **herausgezogen** und wird dort (Push) genutzt. Für (A) muss der **Pull-Adapter
denselben Router** benutzen statt des No-op-`emit`.

---

## 2. Die Aufgabe (A)

Der generierte Pull-Dispatch übergibt heute `_ => Task.CompletedTask` (No-op) als `emit` — deshalb
verpuffen Commands auf dem Pull-Pfad. (A) ist im Kern **eine Zeile**: den echten Router-`emit` übergeben.
Plus: `ImagePairReaktion` mit `IPullSubscriber` markieren (dann selektiert der Generator sie auf Pull,
der Push-Subscriber wird automatisch abgekoppelt). Reaktion hat **keinen** Store → `tracker = null` →
liest ab 0 → re-emittiert bei jeder Weckung → der **Empfänger dedupliziert** (deterministische CommandId +
Noop-Decider, Spec 9.3). Kein zweiter Marker, kein Handler-Typ-Zweig.

---

## 3. Warum der erste Versuch HING (Ursache — unbedingt lesen)

Der Pull-Adapter (`SignalAdapterActor`) ist ein **virtueller Cluster-Actor**. Ihn im Message-Turn ein
**blockierendes `cluster.RequestAsync`** an ein Fremd-Aggregat (den Reaktions-Command) machen zu lassen
hat den Cluster verklemmt — so stark, dass **auch die Historie** hing (nicht nur die Reaktion).

Zwei konkrete Fehler, die dabei gefunden wurden:
1. **`system.Cluster()` zu früh:** im Generator wurde es im `CreateKind`-Body aufgerufen — das läuft
   bei der Kind-Registrierung **vor** `WithCluster`/`StartMemberAsync`. Es muss in die **Spawn-Zeit-Factory**
   (`Props.FromProducer(() => …)`), wo der Cluster fertig ist.
2. **`CancellationToken.None`:** `cluster.RequestAsync(..., None)` → unbegrenzter Proto-Retry, kehrt nie zurück.

**Wichtig:** Ein *begrenzter* Timeout (3s) allein hat den Hang **nicht** behoben → der blockierende Call
aus dem Actor-Turn ist das eigentliche Hindernis, nicht nur das `None`.

---

## 4. Der entschiedene Weg

Der Reaktions-Send ist **at-least-once** (Spec 9.3: Empfänger dedupliziert, Re-Wake heilt) — er muss den
Adapter-Turn **nicht** blockieren.

- **Fire-and-forget (begrenzt):** der Router setzt den Command detached ab; `WakeAsync` läuft durch
  (lesen → dispatch → Send anstoßen → Marke). Kein blockierender Cluster-Call im Actor-Turn.
- **`system.Cluster()` nur zur Spawn-Zeit** (in der Factory, nicht im `CreateKind`-Body).
- Den **bestehenden `HandlerOutputRouter`** wiederverwenden; nur seinen Send aus Sicht des Adapters
  nicht-blockierend machen.

Das ist zugleich das Muster, das der **Prozess-Treiber** (Phase 5) braucht — Command absetzen, Signal-und-Log
als Sicherheitsnetz. (A) baut es also nicht nur für Reaktionen, sondern für das eigentliche Ziel.

---

## 5. Verifikations-Disziplin (die teuerste Lektion — nicht ignorieren)

**Diagnostiziere den Hang NICHT über den langsamen Integrationstest.** xUnit puffert die Host-`Console`-Logs
bis zum Testende → bei einem Hang siehst du **nichts** live, und jeder Zyklus kostet Minuten (Cluster-Startup).

Stattdessen:
1. Baue einen **schnellen In-Memory-Prüfstand-Test** mit einem **Fake-Cluster/Fake-Emit**, der beweist:
   Reaktion wird dispatcht → Command wird emittiert (deterministische Id) → Adapter-Turn läuft durch (kein Block).
   `Infrastructure.Pruefstand.Tests` (kein Docker) ist dafür da.
2. Erst wenn das grün ist: **einen** Integrationslauf `ReaktionE2E` auf Pull, ganz am Ende.

---

## 6. Exakte Dateien

- `Domain.Projections/ImagePairReaktion.cs` — `: ISubscriber, IPullSubscriber`.
- `Infrastructure.SourceGeneration/PullPathGenerator.cs` — echtes `emit` (Router) statt No-op;
  `system.Cluster()` in die Spawn-Factory; `using Infrastructure.PubSub;` wieder aufnehmen.
- `Infrastructure/PubSub/HandlerOutputRouter.cs` — Send nicht-blockierend (fire-and-forget, begrenzt).
- ggf. `Infrastructure/Projections/SignalAdapterActor.cs` + `ProjectionAdapter.cs` — nur falls der
  Wake-`CancellationToken` bis zum `emit` durchgereicht werden soll (für sauberen Shutdown).
- Tests: neuer schneller Prüfstand-Test zuerst; `ReaktionE2E` (auf Pull) zuletzt.

Beim Rückroll blieben bereits sicher im Baum (grün): `HandlerOutputRouter` (Push-Entdopplung) und die
Umbenennung `IExactlyOnceProjection → IPullSubscriber`.

---

## 7. Leitplanken (die Korrekturen dieser Session — nicht verletzen)

- **Eine Maschine, keine Taxonomie** (Spec 8): kein zweiter Marker, kein „Projektion-vs.-Reaktion"-Zweig.
  Der Unterschied fällt aus Ctor-Stores (0 bei Reaktion → tracker null) + Rückgabetypen (yield → emit).
- **Idempotenz ist NIE Default.** Der Normalfall ist der nicht-idempotente Effekt; Co-Commit ist der allgemeine Mechanismus.
- **Technologie-agnostisch, nur bereitstellen.** Garantie = Store-Interface, Zustellung = Handler-Marker. Zwei Achsen.
- **Keine Konzepte überladen.** Im Wesentlichen ändert sich nur der Transport unter der bestehenden `DispatchAsync`.
- **Nicht in Domäne denken** — allgemeine Architektur ableiten; die Domäne (ImagePair/Lagerbestand/…) ist nur ein Teststecker.

---

## 8. Der Endzustand danach (Richtung, nicht jetzt)

Wenn alle durablen Handler auf Pull sind: den **redundanten Push-Dispatch-Treiber** (`SubscriberActorBase`-
Dispatch) löschen — den **Broker-Kanal behalten** (der Pull-Adapter nutzt ihn zum Re-Publish reaktiver Events
und für die Signal-Zustellung). Danach Gesamtplan **Phase 5**: der Prozess-Treiber = derselbe Adapter mit
Command-Effekt (was (A) technisch vorbereitet).

Kandidat für einen ehrlichen Co-Commit-Kronzeugen bei der Migration weiterer Projektionen:
`LagerbestandProjection` (laufender Bestand `-= anzahl` — echter nicht-idempotenter Fold, zudem gemischt:
Write **und** reaktives Event `NachbestellungAngefordert`).
