# Übergabe: Stand & nächster Umbau (Pull-Pfad-Generator + Tracking-Vereinfachung)

> ✅ **ERLEDIGT / HISTORISCH.** Dieser Umbau ist umgesetzt — er wurde store-agnostisch NEU gebaut,
> siehe `docs/entwicklungsplan-projektionsnaht.md` (Phasen 1–5 grün). Der Marker heißt jetzt
> `IPullSubscriber`. Der aktuelle nächste Schritt steht in `docs/uebergabe-reaktionen-auf-pull.md`.
> Dieses Dokument bleibt nur als Kontext der damaligen Entscheidung.

Dieses Dokument ist die Übergabe für die nächste Session. Es hält fest, **was gebaut
ist**, **welches Problem** wir beim Review gefunden haben, **welche Entscheidung** wir
getroffen haben, und **was konkret zu tun ist**. Der Appendix zeigt das **Zielbild** als
Code-Skizze.

Stand: Phasen 0–3 komplett, Phase 4 zur Hälfte (4a + 4b). **36 Tests grün**
(30 in-memory `Infrastructure.Pruefstand.Tests`, 6 Integration `Infrastructure.Integration.Tests`
gegen echtes Postgres/Consul/Redis). Alles baut außer `Domain.Client` (vorbestehendes,
unabhängiges Client-Generator-Refactoring).

---

## Teil 1 — Was wir haben (funktioniert, bleibt)

### Verträge (Abstractions)
- `IEventEnvelope : IAggregateEnvelope` (+`AggregateVersion`); `EventEnvelope` implementiert es.
- `IProjectionTracker` — der Exactly-once-Nahtpunkt (`LastProcessedVersionAsync`,
  `MarkProcessedAsync`, `ResetAsync`, `ResetAllAsync`).
- `IProjectionRebuild` (`IRebuildableProjection`, `IProjectionRebuilder`).
- `IStateChangeSignal : IMessagePayload` (StreamId, Version); `SignalEnvelope`.
- `IEventStoreRepository` erweitert: `ReadStreamAsync(streamId, fromVersion)` +
  `ReadChangedStreamsAsync(afterGlobalSequence)` (+`StreamChanges`).
- `AppendEventsAsync` mit optionalen `correlationId`/`causationId`/`aggregateType`.
- ⚠ `ICoCommitProjectionStore` — **existiert, wird aber gelöscht** (siehe Teil 3).

### Schreibseite (Phase 1)
- `Infrastructure/Aggregate/EventEnvelopeFactory.cs` — Version PRO Event (`baseVersion+i+1`).
- `AggregateActorBase.PublishEventsAsync` nutzt sie; stempelt Metadaten beim Append;
  publiziert nach Commit zusätzlich das Signal via `GeneratedSignalFactory` (best-effort).
- Marten `MetadataConfig` aktiv; `ReadStreamAsync` liest Metadaten zurück.

### Signal-Generatoren (Phase 1b/1c)
- `Domain.SourceGeneration/SignalTypeGenerator.cs` → `StateChangeVia{Event}` pro persistiertem Event.
- `Infrastructure.SourceGeneration/SignalFactoryGenerator.cs` → `GeneratedSignalFactory`
  (Event→Signal, reflection-frei) + `GeneratedSignalRoutes.EventToSignal`.
- `TypeRegistryGenerator` hat Kategorie **`Signals`**; `DtoMapperGenerator` schließt Signale
  aus (nur interne PubSub-Ebene, kein Proto).

### Rückgrat (Phase 2) — GENERISCHE Bausteine, korrekt platziert, bleiben
- `Infrastructure/Projections/CoCommitProjectionAdapter.cs` — Kernschleife (wird vereinfacht, s. Teil 3).
- `Infrastructure/Projections/SignalReceiver.cs` (Signal→Wake) + `SignalReceiverActor.cs` (Proto-Mantel, Broker-Subscription).
- `Infrastructure/Testing/InMemoryProjectionTracker.cs`; `Infrastructure/Persistence/MartenProjectionTracker.cs` (+`ProjectionCheckpoint`).

### Reaktionen (Phase 3)
- `Domain/Reaktion/` — `Reaktionsempfaenger` mit **Noop-Decider** (dedupliziert per Set).
- `Infrastructure/Aggregate/ReaktionsId.cs` — deterministische Id `(StreamId, Version, Diskriminator)`.
- `SubscriberDispatchGenerator` emittiert Outputs als `IMessagePayload`; `SubscriberActorBase`
  hat einen **Output-Router**: `IEvent → publish`, `ICommand → SendReaktionAsync`
  (AggregateType aus `GeneratedPipelines.CommandAggregateTypes`, deterministische CommandId, OCC-Retry).
- `Domain.Projections/ImagePairReaktion.cs` — Demo-Reaktion (ImagePairErstellt → WirkeReaktion).

### Robustheit (Phase 4a + 4b)
- `Infrastructure/Projections/SignalAdapterActor.cs` — Adapter als **virtueller Cluster-Actor pro Stream**
  (Identität = StreamId; auf `Wake` läuft die Schleife). `AdapterMessages.cs` (`Wake`/`WakeAck`/`IClusterKindContributor`).
- `AddCqrsActorSystem` sammelt `IClusterKindContributor` ein (vor `StartMemberAsync`).
- `Infrastructure/Projections/Poller.cs` — Poll-Backstop über `ReadChangedStreamsAsync`; live im Host verdrahtet.

### Tests
- `Infrastructure.Pruefstand.Tests` (in-memory): Crash-Proben, Versionierung, Signal-Generator,
  Reaktions-Dedup/ReaktionsId, Poller.
- `Infrastructure.Integration.Tests` (Postgres/Consul/Redis): Co-Commit, Signal-Delivery über echten Broker,
  **LiveCommandE2E** (Command → Historie), **ReaktionE2E** (Reaktion auf Fremd-Aggregat).

### Wichtige Regel (etabliert)
Neue Domain-Typen brauchen Proto-DTOs, sonst bricht der `DtoMapperGenerator`. Ablauf:
`dotnet run --project Proto.SourceGeneration` → `ProtoRepo` neu bauen → Infrastructure baut.
(Signale sind ausgenommen.) `domain.proto` ist bereits regeneriert (enthält `WirkeReaktion`/`ReaktionGewirkt`).

---

## Teil 2 — Das Problem (warum umbauen)

Beim Review sind **zwei** Dinge aufgefallen:

1. **Domäne/Framework vermischt.** Ich habe handgeschriebene, domänenspezifische Klassen in
   `Infrastructure` (Framework) abgelegt — genau das, was die Architektur verhindern will
   (Invarianten 4/5). Konkret:
   - `Infrastructure/Projections/ImagePairHistorieCoCommitStore.cs`
   - `Infrastructure/Projections/ImagePairHistorieAdapterKind.cs`
   - `Infrastructure/Projections/ImagePairHistoriePullStartup.cs`
   - `Infrastructure/Projections/PullPathExtensions.cs` (`AddImagePairHistoriePullPath`)

2. **Co-Commit überkonstruiert.** Es entstanden zwischenzeitlich `ICoCommitProjectionStore`
   (mit `CommitBatchAsync`), Marker-Interface-Ideen und ein Unit-of-Work-Konzept. Das war
   zu viel — und die UoW-Variante hätte das Framework an Marten (`IDocumentSession`) gebunden.

---

## Teil 3 — Die Entscheidung (die Vereinfachung)

> **Exactly-once IST das Tracking.** Es reicht **ein** Interface: `IProjectionTracker`.
> `MarkProcessedAsync` ist der **Commit-Punkt**. WIE ein Repo Effekt + Marke gemeinsam
> gültig macht, ist Sache des Anwendungs-Entwicklers und seines Repos (Spec 7.8).

Daraus folgt:

- **Ein** Interface: `IProjectionTracker` (Name bleibt). Keine zweite Schnittstelle.
- ❌ `ICoCommitProjectionStore` / `CommitBatchAsync` — **löschen**. Kein extra Commit-Verb.
- ❌ Marker (`IExactlyOnce`/`ICoCommitProjection`) auf der Projektion — **nicht** nötig.
- ❌ Unit of Work / `IDocumentSession` im Framework — **nein** (bindet an Marten).
- ❌ Zwei Adapter-Modi — **nein**. Es gibt **eine** Schleife (Spec 7.3).

**Das Framework ist blind gegenüber der Garantie.** Ein Codepfad:
```
applied = tracker.LastProcessedVersionAsync(projId, streamId)
events  = ReadStreamAsync(streamId, applied + 1)
foreach e (mit Guard): dispatch(e)          // Projektion schreibt in ihr Repo (puffert, wenn exactly-once gewünscht)
if last > applied: tracker.MarkProcessedAsync(projId, streamId, last)   // Commit-Punkt
```
- Repo **puffert** Effekte und flusht sie in `MarkProcessedAsync` gemeinsam mit der Marke → **exactly-once**.
- Repo schreibt sofort und trackt separat → **at-least-once** (Handler idempotent / Dedup-Schlüssel).
- Der `tracker` ist **das Repo des Entwicklers** (implementiert `IProjectionTracker`), wenn es
  co-committen will; sonst ein separater `MartenProjectionTracker`.

**Mehrere Stores?** Das Repo schreibt in seiner einen Transaktion, was es will — ein
Persistenz-Detail. **Verschiedene Backends** atomar (Marten + Redis) geht nicht (kein 2PC)
→ at-least-once/idempotent (Spec 7.6). Kein UoW nötig.

**Store-agnostisch:** Das Framework kennt nur `IProjectionTracker`. Marten steckt in der
Persistenz-/Domänen-Schicht. Backend austauschbar, indem man dort ein anderes tracker-fähiges
Repo bereitstellt.

---

## Teil 4 — Zu tun in der neuen Session (Umbauplan)

Reihenfolge, jeweils bauen + Tests grün halten:

1. **`ICoCommitProjectionStore` löschen** (`Abstractions/ICoCommitProjectionStore.cs`) und alle
   Nutzungen auf `IProjectionTracker` umstellen:
   - `CoCommitProjectionAdapter` (Konstruktor-Param `ICoCommitProjectionStore` → `IProjectionTracker`;
     `CommitBatchAsync`-Wrapper entfernen → schlichte Schleife, `MarkProcessedAsync` als Commit-Punkt).
   - `SignalAdapterActor` (Factory-Tupel-Typ anpassen).
   - `Infrastructure.Pruefstand.Tests/Pruefstand/InMemoryCoCommitStore.cs` → implementiert
     `IProjectionTracker`, `MarkProcessedAsync` flusht Puffer + Marke.

2. **Co-Commit-Repo zur Domäne verschieben.**
   `Infrastructure/Projections/ImagePairHistorieCoCommitStore.cs` → **`Domain.Infrastructure`**,
   implementiert `IImagePairHistorieWriteStore` + `IProjectionTracker`; puffert Appends, flusht
   bei `MarkProcessedAsync` (Effekt + `ProjectionCheckpoint` in EINER Marten-Session). In
   `DomainServiceExtension` registrieren.

3. **Handgeschriebenen Framework-Glue löschen:**
   `ImagePairHistorieAdapterKind.cs`, `ImagePairHistoriePullStartup.cs`, `PullPathExtensions.cs`
   und den Aufruf `AddImagePairHistoriePullPath()` in `Host.Grpc/Program.cs`.

4. **Adapter-/Receiver-Generator bauen** (`Infrastructure.SourceGeneration`, Geschwister von
   `SubscriberActorGenerator`). Pro Projektion (`ISubscriber`, die auf Pull läuft) emittiert er in
   `Infrastructure` (wie `GeneratedSubscribers.g.cs`):
   - den `IClusterKindContributor` (per-Stream-Adapter-Kind),
   - die Receiver-Spawn-Info (+ Poller),
   - ein generiertes `AddGeneratedPullPaths()`, das der Host aufruft.
   Der generierte Adapter resolved die Projektion + ihr Repo; nutzt das Repo als
   `IProjectionTracker`, wenn es das implementiert (→ exactly-once), sonst einen separaten Tracker.
   Push-Subscriber der migrierten Projektionen werden über `PushSubscriberExclusions` abgekoppelt
   (bleibt generisch).

5. **Verifizieren:** `LiveCommandE2ETests`, `ReaktionE2ETests`, `PollerTests` + Prüfstand grün;
   Host.Grpc bootet mit generiertem Pull-Pfad.

### Offene Entscheidung für die neue Session
Welche Projektionen bekommt der Generator auf den Pull-Pfad? Spec-Endzustand: **alle**
(Push-Pfad wird abgelöst). Empfehlung: zunächst nur ImagePairHistorie generiert durchziehen
(verifizierbar gegen die Tests), dann inkrementell die restlichen — nicht alle fünf auf einmal.

### Phase 4 danach (noch offen)
- **4c cross-node-Serialisierung** — der interne Plane schickt rohe CLR ohne registrierten
  Serializer (single-node in-process ok). Multi-node braucht einen (poly-)Serializer für
  `Wake`/`WakeAck`/`SignalEnvelope`/`CommandEnvelope`/`Publish`/`EventEnvelope`.
- **4d Multi-Node-Tor** — Zwei-Member-Test: ein Adapter je Stream, Ordnung erhalten, Poll heilt Totalverlust.

---

## Appendix — Zielbild (Code-Skizzen)

### A. Der EINE Nahtpunkt (unverändert, Name bleibt)
```csharp
// Abstractions/IProjectionTracker.cs — bleibt wie in Phase 0.
public interface IProjectionTracker
{
    Task<int> LastProcessedVersionAsync(string projectionId, Guid streamId, CancellationToken ct);
    Task      MarkProcessedAsync(string projectionId, Guid streamId, int version, CancellationToken ct);
    Task      ResetAsync(string projectionId, Guid streamId, CancellationToken ct);
    Task      ResetAllAsync(string projectionId, CancellationToken ct);
}
```

### B. Die EINE Adapter-Schleife (Spec 7.3 — kein CommitBatchAsync)
```csharp
public async Task WakeAsync(Guid streamId, CancellationToken ct = default)
{
    var applied = await _tracker.LastProcessedVersionAsync(_projectionId, streamId, ct);
    var events  = await _eventStore.ReadStreamAsync(streamId, applied + 1, ct);

    var last = applied;
    foreach (var e in events)
    {
        if (e.AggregateVersion <= applied) continue;   // Guard
        await _dispatch(e);                             // Projektion → Repo (puffert bei exactly-once)
        last = e.AggregateVersion;
    }

    if (last > applied)
        await _tracker.MarkProcessedAsync(_projectionId, streamId, last, ct);   // Commit-Punkt
}
```

### C. Ein exactly-once-Repo (Domänen-Infra, Marten dahinter)
```csharp
// Domain.Infrastructure — der Entwickler entscheidet, WIE co-committet wird.
public sealed class ImagePairHistorieCoCommitStore
    : IImagePairHistorieWriteStore, IImagePairHistorieReadStore, IProjectionTracker
{
    // AppendEintragAsync → in Puffer (KEIN Commit).
    // LastProcessedVersionAsync → Marke lesen.
    // MarkProcessedAsync → Puffer + ProjectionCheckpoint in EINER Marten-Session, ein SaveChanges. Puffer leeren.
    //   (Absturz vor MarkProcessedAsync → Puffer verworfen → nächster Read heilt → exactly-once.)
}
```
Für **at-least-once**: das Repo committet sofort und implementiert `IProjectionTracker` NICHT;
das Framework nutzt dann einen separaten `MartenProjectionTracker`.

### D. Der Generator-Output (analog GeneratedSubscribers, in Infrastructure)
Pro Pull-Projektion:
```csharp
// GeneratedPullPaths.g.cs
public static class GeneratedPullPaths
{
    public static IServiceCollection AddGeneratedPullPaths(this IServiceCollection services)
    {
        services.AddSingleton(new PushSubscriberExclusions(new[] { "ImagePairHistorieProjection" }));
        services.AddSingleton<IClusterKindContributor, ImagePairHistorieAdapterKind_Generated>();
        services.AddHostedService<PullStartupService>();   // generisch, iteriert registrierte Descriptors
        return services;
    }
}
```
Der Host ruft nur `AddGeneratedPullPaths()`. **Kein handgeschriebener Domänen-Glue im Framework.**

### E. Platzierungs-Regeln
| Art | Wo |
|---|---|
| Generische Maschinerie (Adapter, Receiver, Poller, SignalAdapterActor, `IProjectionTracker`) | Infrastructure / Abstractions |
| Domänen-Repos (auch exactly-once-Repos) | Domain.Infrastructure |
| Reine Projektionen/Reaktionen | Domain.Projections (rein, kein Marker) |
| Pro-Projektion-Verdrahtung (Adapter-Kind, Receiver-Spawn, Registrierung) | **generiert** → Infrastructure |
