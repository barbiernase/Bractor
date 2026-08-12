# Snapshots — Konzept & Umsetzungsplan

Status: **umgesetzt & bewiesen (S0–S4).** Prüfstand 68 grün (6 neue Snapshot-Proben), Integration grün
inkl. Live-Round-Trip `mt_doc_snapshot_konto`, Host-Boot mit 13 Snapshot-Typen und der **vollständigen
Live-Schleife**: ein echter Cluster-Actor schreibt bei kleinem Threshold einen Snapshot nach Postgres, eine
spätere Rehydration seedet nachweislich daraus (SnapshotVersion > 0, Tail statt Voll-Replay) und
vervollständigt zum korrekten Endzustand (`SnapshotLiveE2ETests`). Threshold ist über
`CqrsFrameworkBuilder.SnapshotThreshold` → `SnapshotOptions` an den Actor verdrahtet (Default 200).

## 0. Worum es geht

Ein Aggregat ist ein virtueller Proto-Actor (Single-Writer je Stream). Bei **jeder**
Aktivierung faltet `AggregateActorBase.InitializeAsync` den Stream heute **zweimal
komplett von 0**:

1. `IEventStoreRepository.LoadStateAsync<TState>(id)` → Domänen-State (Applier).
2. `ReadStreamAsync(id, 0)` → die Framework-Inbox (`KommandoVerarbeitet`-Marken →
   `_verarbeiteteCommandIds`).

Da virtuelle Actors bei Idle deaktiviert werden, zahlt ein langlebiges Aggregat diese
**2×O(n)**-Kosten bei jeder Reaktivierung. Genau die in `spezifikation.md` §17 und
`Entwicklungsplan.md` Phase 6 vorgemerkte Kostenstelle.

**Ziel:** Rehydration auf **O(Tail)** bounden über einen Snapshot = abgeleiteter,
nicht-autoritativer jsonb-Cache pro Aggregat.

**Nicht-Ziele:** Projektions-Rebuild (braucht *jedes* Event, nicht den State),
Prozess-Marking (`ProzessManager.FaltMarkingAsync` — eigene Baustelle: Marking-Cursor),
Multi-Node-Serialisierung. **Keine Domänen-Berührung** — kein Marker, kein Attribut.

## 1. Randbedingungen aus den sechs Invarianten

- **Log = Wahrheit (1):** Snapshot ist ein Cache. Fehlt/veraltet er → Voll-Replay, nie
  ein Fehler. Bei Widerspruch gewinnt der Log.
- **Keine Reflection im Eigen-Code (4):** State-Serialisierung läuft über den
  Bibliotheks-Serializer (Marten-JSON) — wie Event-Serialisierung. Registrierung ist
  **generiert**, der Store generisch (kein per-Typ-Handschalter).
- **Fachcode bleibt rein (5):** Snapshots sind komplett Framework/generiert. Der
  Entwickler schreibt nichts dazu. Der **Schwellwert selektiert lange Streams von
  selbst** — kurze Auslöse-Aggregate erreichen N nie → kein Snapshot, kein Marker nötig.
- **Persistent nur bei durablem Konsumenten (6):** Der Snapshot-Write geht **NIE** in die
  Command-Transaktion. Best-effort, out-of-band, fire-and-forget. Ein Fehler ist folgenlos.

## 2. Die zwei nicht-offensichtlichen Korrektheits-Punkte

Ein naiver „nur State serialisieren"-Snapshot wäre **falsch**:

1. **Die Inbox MUSS mit in den Snapshot.** `_verarbeiteteCommandIds` wird heute aus
   *allen* `KommandoVerarbeitet`-Marken ab 0 gefaltet. Startet man nach dem Snapshot bei
   `v+1`, gehen die Marken vor `v` verloren → ein at-least-once wiederholter Reaktions-/
   Prozess-Command würde **doppelt wirken**. Deshalb trägt der Snapshot
   `ProcessedCommandIds`.
2. **Version zählt die Marken mit.** `state.Version` = Anzahl *aller* Events inkl.
   `IProzessIntern`-Marken. Der Tail wird ab `snapshot.Version + 1` gelesen, und jede
   Hülle (auch Marken) zählt die Version hoch — sonst bricht die OCC-Assertion beim
   nächsten Append.

## 3. Storage — nichts Neues

Marten *ist* die Dokument-Datenbank. Ein Snapshot ist ein weiterer Dokumenttyp, genau wie
`ProjectionCheckpoint` / `PollCursor` / `ImagePairReadModel`:

- **Dieselbe DB, derselbe `IDocumentStore`.** Kein zweiter Store, keine zweite Connection.
- **Nativ jsonb** (binary json) — das *ist* der Marten-Dokumentmechanismus.
- **Tabelle pro Aggregat**, von Marten abgeleitet: `Schema.For<Snapshot<Konto>>()` →
  `es.mt_doc_snapshot_konto`. `AutoCreateSchemaObjects = CreateOrUpdate` baut sie beim Boot.
- **Zurückholen** O(1) by-id: `session.LoadAsync<Snapshot<TState>>(aggregateId)`.

Der State steht als **echtes, einfach-kodiertes, typisiertes jsonb** im Dokument (Weg A:
generisches `Snapshot<TState>` pro Aggregat), nicht als doppelt-kodierter String.

## 4. Die Verbindung Actor ↔ Snapshot: die Aggregat-GUID

Der Actor ist ein virtueller Proto-Actor, dessen ClusterIdentity **die Aggregat-GUID** ist.
Das Snapshot-Dokument hat als `Id` **dieselbe GUID**. Kein Join, kein Mapping — die GUID
ist die Brücke. Angefasst nur im **Lebenszyklus des Aggregat-Actors** an drei Hooks:

- **Laden** bei Aktivierung (erster `CommandEnvelope` → `InitializeAsync`): Tail-Fold ab
  `snapshot.Version + 1` statt ab 0.
- **Schreiben** nach dem N-ten Event (Schwellwert, State in-turn einfrieren, DB-Write detached).
- **Schreiben** bei `Stopping` (Deaktivierung).

Die Signal-/Wake-/Routing-Schicht (Pull-Adapter, `SignalReceiver`, `ProzessManager.WakeAsync`)
bleibt **komplett unberührt** — das ist ein *anderes* „Wecken".

## 5. Verträge (`Abstractions/Snapshot.cs`)

```csharp
public sealed class Snapshot<TState> where TState : class, IState
{
    public Guid Id { get; set; }                     // = AggregateId
    public int Version { get; set; }                 // Stream-Position inkl. Marken
    public string SchemaVersion { get; set; } = "";  // Schema-Evolution (Struktur-Hash)
    public TState State { get; set; } = default!;
    public Guid[] ProcessedCommandIds { get; set; } = Array.Empty<Guid>();
    public DateTimeOffset UpdatedAt { get; set; }
}

public interface ISnapshotStore
{
    Task<Snapshot<T>?> TryLoadAsync<T>(Guid id, CancellationToken ct) where T : class, IState, new();
    Task SaveAsync<T>(Snapshot<T> snap, CancellationToken ct)          where T : class, IState, new();
    Task DeleteAsync<T>(Guid id, CancellationToken ct)                 where T : class, IState, new();
}
```

`EventStoreOptions.SnapshotThreshold` (Default 200) steuert N.

## 6. Der Fold-Umbau (`Infrastructure/Aggregate/AggregateRehydrator.cs`)

Die heute zwei Voll-Reads verschmelzen zu **einem** Tail-Fold, der State *und* Inbox baut —
für sich schon ein Gewinn, auch mit Snapshot=null. In einen puren Helfer extrahiert, damit
er in-memory ohne Proto-Actor beweisbar ist:

```csharp
public static async Task<RehydrationResult<TState>> LoadAsync<TState>(
    Guid id, ISnapshotStore? snapshots, IEventStoreRepository store,
    IAggregateHandlerFactory factory, string? expectedSchemaVersion, CancellationToken ct)
    where TState : class, IState, new()
{
    var snap = snapshots is null ? null : await snapshots.TryLoadAsync<TState>(id, ct);
    if (snap != null && expectedSchemaVersion != null && snap.SchemaVersion != expectedSchemaVersion)
        snap = null;                                              // Stale → Voll-Replay

    var state = snap?.State ?? new TState { Id = id, Version = 0 };
    var inbox = new HashSet<Guid>(snap?.ProcessedCommandIds ?? Array.Empty<Guid>());
    var handler = factory.CreateHandler(state);
    var tail = await store.ReadStreamAsync(id, (snap?.Version ?? 0) + 1, ct);
    foreach (var env in tail)
    {
        if (env.Payload is KommandoVerarbeitet m) inbox.Add(m.CommandId);
        else if (env.Payload is not IProzessIntern) handler.ApplyEvent(env.Payload);
        state.Version++;
    }
    state.Id = id;
    return new RehydrationResult<TState>(state, inbox, snap?.Version ?? 0);
}
```

`AggregateActorBase.InitializeAsync` ruft nur noch diesen Helfer. `LoadStateAsync` im Store
bleibt für andere Aufrufer (Tests, Prozess-Ziel-Loads) unangetastet.

## 7. Der Write-Pfad (Hooks in `AggregateActorBase`)

Beide **außerhalb** der Command-Transaktion, **capture-in-turn + fire-and-forget**:

```csharp
private void MaybeSnapshot(CancellationToken ct)   // nach erfolgreichem Append
{
    if (_snapshots is null || _state!.Version - _lastSnapshotVersion < _threshold) return;
    var snap = Freeze();                 // State serialisieren + Inbox kopieren JETZT (race-frei)
    _lastSnapshotVersion = _state.Version;
    _ = _snapshots.SaveAsync(snap, ct);  // NICHT awaiten
}
// case Stopping: dasselbe Freeze() wenn seit letztem Snapshot Events kamen.
```

`Freeze()` friert den State bei Version `v` ein — der spätere DB-Write kann das mutierende
`_state` nicht mehr korrumpieren (Single-Writer-Turn).

## 8. Storage-Naht generiert (S4)

- `MartenSnapshotStore` — generisch, winzig (`LoadAsync`/`Store`/`Delete`), wie `MartenProjectionTracker`.
- `TypeRegistryGenerator` bekommt Kategorie `IState` → `GeneratedTypeRegistry.Aggregates`.
- Generiertes `RegisterGeneratedSnapshotTypes(StoreOptions)` (analog `RegisterEventTypes`):
  `opts.Schema.For<Snapshot<Konto>>().Identity(x => x.Id).DocumentAlias("snapshot_konto")`.
- **Schema-Version = Struktur-Hash** des State-Typs (Property-Namen+Typen, zur Compile-Zeit
  berechnet). State-Form ändert sich → Hash ändert sich → alte Snapshots invalidieren
  automatisch. Zero-touch (Invariante 5). Emittiert als `GeneratedSnapshotSchema.VersionOf<T>()`.
- DI: `ISnapshotStore`-Singleton (Marten live, InMemory im Prüfstand); der Actor-Generator
  reicht `snapshots` durch den Ctor.
- **Replay-Kohärenz:** `IProjectionRebuild`/Reset ruft `snapshotStore.DeleteAsync` mit.

## 9. Phasen & Tore

| Phase | Inhalt | Tor |
|---|---|---|
| S0 | Verträge (`Snapshot<T>`, `ISnapshotStore`, `SnapshotThreshold`) | kompiliert, keine Marten-Abhängigkeit |
| S1 | Fold-Umbau (`AggregateRehydrator`, `InitializeAsync`, Ctor-Param) | Prüfstand unverändert grün (Snapshot=null == alter Doppel-Read) |
| S2 | Laden + `InMemorySnapshotStore` | Proben 1–4 |
| S3 | Schreiben (Schwellwert + `Stopping`) | Proben 5–6 |
| S4 | Marten + Generator + DI + Replay | Host.Grpc bootet, `mt_doc_snapshot_*` da, Live-Rehydration aus Snapshot |

**Prüfstand-Proben (in-memory, im Stil der Crash-Proben):**

1. **Äquivalenz:** Snapshot@v + Tail == Voll-Replay ab 0 (feldgleicher State).
2. **Inbox-Erhalt:** Reaktions-Command mit Marke **vor** dem Snapshot verpufft weiterhin.
3. **Version-Alignment:** nächster OCC-Command nach Snapshot-Rehydration passt.
4. **Stale/fehlend:** Schema-Mismatch/gelöscht → sauberer Voll-Replay, identisches Ergebnis.
5. **Write-Fehler folgenlos:** `SaveAsync` wirft → Command trotzdem committet, Rehydration heilt.
6. **Capture-Race:** eingefrorener State ist der zu `v`, unkorrumpiert vom Folge-Command.

**Reihenfolge-Logik:** S0–S3 komplett in-memory beweisbar (erst Fake, dann live), erst S4
geht gegen Postgres. Nach S1 steht schon der halbe Gewinn (ein Read statt zwei).

## 10. Restpunkte / bewusste Grenzen

- **Threshold-Konfig — erledigt:** `CqrsFrameworkBuilder.SnapshotThreshold` (Default 200) → als
  `SnapshotOptions` in DI → der generierte Actor injiziert es. Tests setzen es klein.
- **Deaktivierungs-Realität:** `Stopping` ist bei Cluster-Passivierung best-effort und feuert bei
  Node-Crash nicht. Für sehr heiße (nie deaktivierte) Aggregate trägt allein der Schwellwert — so
  gewollt.
- **Prozess-Marking-Cursor:** separate Optimierung (nicht Teil dieses Konzepts).

## 11. Angefasste/neue Dateien

- `Abstractions/Snapshot.cs` (neu), `Abstractions/Options/EventStoreOptions.cs` (+Threshold)
- `Infrastructure/Aggregate/AggregateRehydrator.cs` (neu — der EINE Tail-Fold)
- `Infrastructure/Aggregate/ActorSystem/AggregateActorBase.cs` (Rehydrator + Write-Hooks + Ctor)
- `Infrastructure/Testing/InMemorySnapshotStore.cs` (neu), `Infrastructure/Persistence/MartenSnapshotStore.cs` (neu)
- `Infrastructure.SourceGeneration/SnapshotRegistrationGenerator.cs` (neu — Registrierung + Struktur-Hash)
- `Infrastructure.SourceGeneration/AggregateActorGenerator.cs` (reicht `snapshots` + Schema durch)
- `Infrastructure/Extensions/CqrsServiceExtension.cs` (DI + Marten-Registrierung)
- `Abstractions/Snapshot.cs` (+`SnapshotOptions`), `Infrastructure/Extensions/CqrsServiceExtension.cs`
  (`CqrsFrameworkBuilder.SnapshotThreshold` + `SnapshotOptions`-Registrierung)
- `Infrastructure.Pruefstand.Tests/Phase6/SnapshotRehydrationTests.cs` (6 Proben)
- `Infrastructure.Integration.Tests/SnapshotStorePostgresTests.cs` (Live-Round-Trip)
- `Infrastructure.Integration.Tests/SnapshotLiveE2ETests.cs` (vollständige Live-Schleife)
