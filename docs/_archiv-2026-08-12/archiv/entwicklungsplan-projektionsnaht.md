# Entwicklungsplan: Store-agnostische Projektions-Naht (Neuentwurf)

Dieses Dokument löst **Teil 4** von `docs/uebergabe-adapter-generator.md` ab und
präzisiert es. Es hält den in dieser Session konvergierten Zielentwurf fest: die
**technologie-agnostische** Exactly-once-Naht für Projektionen, die zugehörige API,
und den Umsetzungsplan **from scratch**.

Grundlage bleibt `docs/spezifikation.md` Kap. 4–7 (die Naht) und
`docs/Entwicklungsplan.md` (die Phasen des Gesamtvorhabens).

---

## 1. Die korrigierte Philosophie (Leitsatz)

> Das Framework **stellt Nahtpunkte bereit** und nimmt über den Effekt und die
> Speichertechnologie **nichts** an. Es liefert jedes Event geordnet, single-writer,
> geguardet an den Handler. *Ob* daraus „genau einmal wirksam" wird, entscheidet der
> **Store** — durch Co-Commit (Effekt + Marke atomar) oder durch Idempotenz.

Daraus die harten Konsequenzen, die jede Design-Entscheidung tragen:

- **Der Effekt ist beliebig und möglicherweise nicht idempotent** (laufende Summe,
  Zustandsmaschine, Append, Emit). Das Framework setzt **nie** Idempotenz voraus.
- **Co-Commit ist der primäre, allgemeine Mechanismus** — er trägt *jeden* Effekt.
  „Atomar" ist technologiespezifisch (SQL-Txn, Dokument-Session, immutable
  Conditional-Append, CAS); das Framework kennt es nicht und soll es nicht kennen.
- **Das Framework verlangt vom Effekt gar nichts.** Ohne Co-Commit **und** ohne
  Idempotenz **läuft es trotzdem** — es ist dann nur nicht *gültig* (eine Wiederholung
  wendet den Effekt erneut an → Doppelwirkung). Gültigkeit ist eine
  **Korrektheitseigenschaft, die der Entwickler wählt**, keine Betriebsvoraussetzung.
- Idempotenz ist damit *ein Weg zur Gültigkeit* — nie der Default, nie eine Pflicht.

---

## 2. Die API (vier Nahtpunkte)

Alles, was der Entwickler konsumiert. Store-agnostisch, `int version` als Position
(die Event-Store-Ordnung ist monoton pro Stream, Spec 5.3).

```csharp
// 1. Handler-Marker (bestehend)
public interface ISubscriber { string SubscriberId { get; } }

// 2. Anforderung: „auf den geordneten, single-writer, geguardeten Pull-Pfad"
public interface IExactlyOnceProjection : ISubscriber { }

// 3. Der Write-Kontext — trägt die kausale Position an den Write-Site
public sealed class WriteContext
{
    public string ReadModelId { get; }
    public Guid   StreamId    { get; }   // kausale Stream-Identität
    public int    Version     { get; }   // kausale Position im Log
    public void   Track<T>(Guid id) where T : IState;   // Deps-Index (optional)
}

// 4. Die Co-Commit-Naht — OPTIONAL, vom STORE implementiert.
//    Zwei semantische Operationen, kein Begin/Commit-Verb (Spec 7.2).
public interface IProjectionTracker
{
    Task<int> LastProcessedVersionAsync(string projectionId, Guid streamId, CancellationToken ct);
    Task      MarkProcessedAsync(string projectionId, Guid streamId, int version, CancellationToken ct);
}
```

### Was der Anwendungsentwickler schreibt

**① Die Projektion — rein, markiert, beliebiger (hier nicht-idempotenter) Effekt:**
```csharp
public partial class LaufendeSummeProjektion : ISubscriber, IExactlyOnceProjection
{
    private readonly ISummenStore _store;
    public LaufendeSummeProjektion(ISummenStore store) => _store = store;
    public string SubscriberId => "laufende-summe";

    public async Task Handle(BetragGebucht evt, IAggregateEnvelope env, ProjectionWriter writer)
        => await writer.Execute(env.AggregateId.ToString(), async ctx =>
        {
            // NICHT idempotent: += betrag. Doppelanwendung zählt doppelt.
            await _store.AddiereAsync(ctx.StreamId, evt.Betrag);
        });
}
```

**② Der Store — Fach-Write UND Co-Commit-Naht; die Transaktions-Technologie ist seine Sache:**
```csharp
// Der Store instanziiert per Stream (der Single-Writer-Actor sorgt dafür) →
// er darf eine ambient Transaktion über den Batch halten, ohne Race.
public sealed class SummenStore : ISummenStore, IProjectionTracker
{
    private ITx? _tx;   // nur innerhalb eines Wake gesetzt

    public Task<int> LastProcessedVersionAsync(string proj, Guid stream, CancellationToken ct)
        => _db.ReadMarkeAsync(proj, stream);          // -1 falls keine; reiner Read

    public async Task AddiereAsync(Guid stream, decimal betrag)
    {
        _tx ??= await _db.BeginAsync();               // Batch-Tx beim ersten Effekt öffnen
        await _tx.SummeErhoehenAsync(stream, betrag); // Effekt HINEIN
    }

    public async Task MarkProcessedAsync(string proj, Guid stream, int version, CancellationToken ct)
    {
        await _tx!.SchreibeMarkeAsync(proj, stream, version); // Marke in DIESELBE Tx
        await _tx.CommitAsync(ct);                            // EIN Commit → gemeinsam gültig
        _tx = null;
    }
    // Absturz vor Commit → Tx verworfen → nächster Read heilt → genau einmal wirksam.
}
```
`ITx`/`_db` stehen für **irgendein** Backend. Die zwei Naht-Aufrufe klammern den Batch;
das Framework kennt kein Begin/Commit.

**③ Registrierung — der Store wird auch als Tracker sichtbar gemacht:**
```csharp
services.AddScoped<SummenStore>();
services.AddScoped<ISummenStore>(sp      => sp.GetRequiredService<SummenStore>());
services.AddScoped<IProjectionTracker>(sp => sp.GetRequiredService<SummenStore>());
```

### Was der Entwickler NICHT schreibt (Framework / generiert)
Adapter-Schleife, Receiver, Poller, Cluster-Kind, Signal, Push-Abkopplung, jede
Marken-Buchhaltung, Ordnung, Guard, Sharding.

### Die Naht in einem Satz
> **Der Marker** (Projektion) sagt „ich brauche es". **`IProjectionTracker`** (Store)
> sagt „ich mache Marke + Effekt in meiner Technologie gemeinsam gültig". **Das
> Framework** ist blind und ruft nur an der richtigen Stelle.

---

## 3. Der Entwicklungsplan (from scratch)

Kein Backward-Compat: Altes wird beherzt gelöscht. Wiederverwendet wird nur der
generische Transport (`SignalReceiver`, `SignalAdapterActor`-Hülle, `Poller`,
`ReadStreamAsync`/`ReadChangedStreamsAsync`, Signal-Generatoren) — er deckt sich schon
mit der Spec.

### Phase 1 — Die Nahtpunkte (Verträge + `ctx` + Schleife)
- **Bauen:** `IExactlyOnceProjection`; `WriteContext` mit `StreamId`+`Version` (int);
  `ProjectionWriter` (Position aus dem Envelope); `IProjectionTracker` (optional,
  ≤1 Autorität, **kein** Framework-Default); `IReadModelDepsSink` (optional);
  `ProjectionAdapter` — reine 7.3-Schleife: `tracker?` (null → ab 0), Guard, Dispatch,
  `depsSink?`, Marke.
- **Löschen:** `ICoCommitProjectionStore`, `CoCommitProjectionAdapter`, jede
  `CommitBatch`-Mechanik.
- **Tor:** Unit-Test fährt die Schleife mit Tracker und ohne, grün.

### Phase 2 — Der generische Beweis (Risiko zuerst, VOR dem Generieren)
- **Bauen im Prüfstand:** ein **nicht-idempotenter** Effekt (Zähler `+=`) + ein
  Co-Commit-Store (ambient Tx). Absturzpunkt zwischen Effekt und Marke.
- **Proben:**
  - Store **ohne** `IProjectionTracker` → Absturz → Doppelwirkung (funktioniert, nicht gültig).
  - Store **mit** `IProjectionTracker` (Co-Commit) → Absturz → genau einmal wirksam.
  - Verlorenes Signal heilt der nächste Read; doppeltes Signal folgenlos (Guard).
- **Tor:** Proben grün — Adapter identisch, allein die Store-Naht entscheidet die Gültigkeit.

### Phase 3 — Transport an die neue Naht andocken (single-node)
- **Bauen:** `SignalAdapterActor` auf die neue Schleife (Factory-Tupel
  `(dispatch, IProjectionTracker?)`); Receiver + Poller wecken dieselbe
  per-Stream-Cluster-Identität.
- **Tor:** Signal + Poll wecken denselben Stream-Actor; live gegen echten Store,
  exactly-once erhalten.

### Phase 4 — Verdrahtungs-Generator + Push-Pfad für Projektionen raus
- **Bauen:** Generator in `Infrastructure.SourceGeneration`, selektiert per
  `IExactlyOnceProjection`; emittiert Cluster-Kind, Receiver-Spawn, Poller,
  `PushSubscriberExclusions`, Deps-Sink-Verdrahtung, **Tracker-Wahl compile-fest**
  (≥2 Autoritäten → Compile-Fehler), `AddGeneratedPullPaths()`.
- **Löschen:** die vier Handschrift-Glue-Dateien
  (`ImagePairHistorieCoCommitStore`, `ImagePairHistorieAdapterKind`,
  `ImagePairHistoriePullStartup`, `PullPathExtensions`), `AddImagePairHistoriePullPath()`
  in `Host.Grpc/Program.cs`, die Push-Dispatch-Erzeugung für Projektionen.
- **Tor:** Host bootet nur noch über den generierten Pull-Pfad.

### Phase 5 — Live-Durchstich + Deps-Index geschlossen
- **Tor:** E2E (Command → Projektion materialisiert), Poller heilt Totalverlust,
  Deps-Sink führt den Versions-Index auch auf dem Pull-Pfad (der frühere Fund, dass der
  Pull-Pfad `writer.GetResults()` heute wegwirft).

**Offener Scope:** Reaktionen laufen weiter auf dem Push-Pfad — in einem späteren Cut
auf den Adapter ziehen (Spec-Endzustand).

---

## 3a. Umsetzungsstand — Phasen 1–5 fertig ✅

Alle fünf Phasen implementiert und grün (gegen Docker: Postgres/Consul/Redis).

- **1 Nahtpunkte:** `IExactlyOnceProjection`, `WriteContext(StreamId,Version)`, `ProjectionWriter`,
  `IProjectionTracker` (2 Kern-Methoden + Reset), `IReadModelDepsSink`, `ProjectionAdapter`
  (7.3-Schleife). `ICoCommitProjectionStore`/`CoCommitProjectionAdapter` gelöscht.
- **2 Beweis:** generischer nicht-idempotenter Zähler (Prüfstand) — getrennte Marke → Doppelwirkung,
  Co-Commit → genau einmal. Adapter identisch.
- **3 Transport:** `SignalAdapterActor` auf `ProjectionAdapter`; Alt-Tests migriert.
- **4 Generator:** `PullPathGenerator` (selektiert per Marker) emittiert Kind + Receiver/Poller +
  Push-Exclusion + `AddGeneratedPullPaths()`. Store nach `Domain.Infrastructure`,
  `ProjectionCheckpoint` nach `Abstractions`. Vier Glue-Dateien + `AddImagePairHistoriePullPath` weg.
- **5 Deps-Index:** `ReadModelDepsWriter : IReadModelDepsSink`, im Pull-Pfad verdrahtet; `rm:`-Index
  wird pull-seitig geführt (belegt in Redis DB 1).

**Zwei Umsetzungs-Erkenntnisse:**
- Der Generator läuft auf `Infrastructure`; Projektionen kommen aus der referenzierten
  `Domain.Projections`-**DLL (Metadaten)** → `DeclaringSyntaxReferences` ist leer, ein
  `SubscriberId`-Literal ist NICHT statisch lesbar. Lösung: die projectionId (Mark + Deps) wird zur
  **Laufzeit** aus `projection.SubscriberId` gelesen; Kind-/Receiver-Namen sind klassennamen-abgeleitet.
- `Infrastructure → Domain.Infrastructure` ist die Referenz-Richtung → der Domänen-Store kann
  Framework-Typen NICHT nutzen; `ProjectionCheckpoint` liegt deshalb in `Abstractions`.

**Tests:** Prüfstand 35/35; Integration (CoCommitPostgres 3, SignalDeliveryCluster, LiveCommandE2E
inkl. Deps-Assertion, ReaktionE2E) grün. Offen (bewusst): 4c/4d Multi-Node, Reaktionen auf Pull.

---

## 4. Entscheidungs-Log dieser Session

- **`int version`** als Positions-Typ (kein opaker Cursor) — die Stream-Ordnung ist
  monoton (Spec 5.3), ein opaker Cursor verkompliziert Guard/Poll ohne realen Nutzen.
- **Anforderung ≠ Mechanismus.** Die *Anforderung* („ich brauche exactly-once") ist eine
  Eigenschaft der **Projektion** → Marker `IExactlyOnceProjection`. Der *Mechanismus*
  (Marke atomar mit Effekt) bleibt beim **Store** → `IProjectionTracker` (Spec 5.4/7.1:
  nur der Store kann das, sonst Dual-Write). Verschiebt Spec Kap. 8 bewusst von „das
  Interface des Stores wählt die Garantie" zu „die Projektion fordert, der Store liefert".
- **Höchstens EIN Tracker (Mark-Autorität) pro Projektion** (Spec 7.6: kein 2PC). Der
  Generator prüft das compile-fest; ≥2 → Compile-Fehler. Weitere Stores müssen dann
  idempotent sein oder man akzeptiert at-least-once.
- **Kein Framework-Default-Tracker** (keine Marten-Annahme im Kern). Fehlt ein Tracker,
  liest das Framework ab 0 — Gültigkeit dann nur, wenn der Effekt idempotent ist.
- **`ctx` trägt die kausale Position** (`StreamId`, `Version`). Damit fällt die Marke aus
  derselben kausalen Quelle wie der bestehende Deps-Versions-Index — nicht doppelt gerechnet.
- **Idempotenz ist kein Default.** Der frühere Plan „Historie per Dedup-Schlüssel
  idempotent machen" ist verworfen; der Beweis läuft generisch über einen
  nicht-idempotenten Zähler im Prüfstand, nicht über eine bestimmte Domänenprojektion.
