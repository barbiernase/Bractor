# Phase 0 — Stand und Begründung

Dieses Dokument hält fest, **was bereits implementiert ist und warum** — die
Ideenaufstellung hinter den erzeugten Dateien und den Edits. Es ist das
Übergabedokument für Claude Code: Wer hier durchliest, versteht den aktuellen
Zustand ohne den ursprünglichen Chat.

> **Stand (aktualisiert):** Phase 0 ist **vollständig integriert** — die Verträge
> liegen nicht mehr nur unter `docs/`, sondern in `Abstractions`/`Infrastructure`,
> die vier Edits sind angewandt, die Server-Kette baut grün. Phase 0.5 (Prüfstand)
> ist **gebaut und grün**: `Infrastructure.Pruefstand.Tests` fährt die vier
> Crash-Proben gegen die echte `ImagePairHistorieProjection` (5 Tests, 0 Fehler).
> Nächster Schritt: **Phase 1** (Version pro Event + Metadaten ins Log + Signal-Emit).
> Die `docs/*.cs`-Kopien bleiben als Referenz; die aktive Quelle ist der Projektcode.

## Warum Phase 0 zuerst

Phase 0 ist die einzige Phase, in der Nachdenken mehr zählt als Code. Jeder hier
falsch geschnittene Vertrag zieht später einen Rattenschwanz durch alles Gebaute.
Es läuft am Ende von Phase 0 noch nichts — das ist beabsichtigt. Geliefert werden
die tragenden Verträge plus die vollständige Replay-Vertragsebene, damit beim
Rückgrat (Phase 2) und beim Replay nichts nachgerüstet werden muss.

Leitprinzip der ganzen Sequenz: **erst zum Laufen bringen, dann korrekt machen,
dann beweisen** — und die teuren Crash-Proben so früh wie möglich, solange erst
eine Komponente existiert und Änderungen billig sind.

---

## Erzeugte Dateien

### `Abstractions/IEventEnvelope.cs`

**Was:** `IEventEnvelope : IAggregateEnvelope` mit `int AggregateVersion`.

**Warum:** Der Adapter (Phase 2) braucht die Position jedes Events für
Pre-Dispatch-Guard und Fortschrittsmarke — ohne auf den konkreten `EventEnvelope`
casten zu müssen. Append-artige Projektionen nutzen `(AggregateId,
AggregateVersion)` als Dedup-Schlüssel; spätere deterministische Identitäten
leiten sich aus `(StreamId, AggregateVersion)` ab.

**Designentscheidung:** eigenes Interface statt `AggregateVersion` direkt auf
`IAggregateEnvelope`. Commands tragen keine Event-Version (nur `ExpectedVersion`);
ein gemeinsames Feld würde Commands einen bedeutungslosen Wert aufzwingen.
`EventEnvelope` besitzt `AggregateVersion` bereits — es muss das Interface nur
zusätzlich deklarieren (rein additiv). Die `Handle`-Signaturen der Projektionen
bleiben in Phase 0 auf `IAggregateEnvelope` und wandern erst in Phase 2/3.

### `Abstractions/IProjectionTracker.cs`

**Was:** der Exactly-once-Nahtpunkt — `LastProcessedVersionAsync`,
`MarkProcessedAsync`, plus `ResetAsync` / `ResetAllAsync` für Replay.

**Warum genau diese Methoden:** Das Interface drückt nur aus, was das Framework
*semantisch* braucht — Resume-/Guard-Punkt lesen, Fortschritt vorrücken, für
Replay zurücksetzen. Eine Begin/Commit-Mechanik gehört bewusst NICHT hinein, weil
sie eine Implementierungsstrategie erzwingen würde. Die Version als Schlüssel
(statt einer Menge verarbeiteter Event-Ids) ist O(1), wächst nicht unbegrenzt und
bildet die Ordnung des Log-Reads ab.

**Kernaussage:** Das Framework STELLT den Nahtpunkt bereit und ruft ihn an der
richtigen Stelle auf — mehr nicht. Ob daraus exactly-once-wirksam oder
at-least-once wird, ist eine Eigenschaft der Store-Implementierung.

### `Abstractions/IProjectionRebuild.cs`

**Was:** `IRebuildableProjection` (Ziel leeren, vom Store implementiert) und
`IProjectionRebuilder` (Koordinator, Implementierung in Phase 2).

**Warum:** Damit die Replay-Mechanik jetzt vollständig als Vertrag festliegt.
Rebuild-Ablauf (im Code dokumentiert): Ziel leeren → Marken auf -1 → jeden Stream
ab 0 lesen und dispatchen. Das ist exakt der normale Adapter-Pfad mit garantiert
leerem Ausgangszustand — deshalb kommt der ausführende Rebuilder mit dem Adapter
(Phase 2) und wird nicht doppelt gebaut.

**Festgeschriebene Grenze:** Replay ist projektions-lokal. Command-yieldende
Handler (Reaktionen) werden NICHT blind replayt — sonst würden geldbewegende
Commands neu gefeuert bzw. historische Prozesse neu angetrieben. Diese Grenze
steht als Kommentar in `IProjectionRebuilder`, damit sie später nicht verhandelbar
ist.

### `Infrastructure/MartenProjectionTracker.cs`

**Was:** die echte Marten-Implementierung des Trackers plus das
`ProjectionCheckpoint`-Dokument (Id-Form `"{projectionId}:{streamId}"`). Reset
löscht die Checkpoint-Dokumente.

**Warum / Phase-0-Stand:** Öffnet für Mark/Reset eine EIGENE Session → Fortschritt
und Effekt in getrennten Transaktionen → at-least-once (Handler idempotent). Das
ist bewusst: Der Nahtpunkt bleibt identisch, egal ob at-least-once oder
exactly-once. Der Co-Commit-Umbau (Fortschritt + Effekt über DIESELBE Session)
ist die Phase-2-Aufgabe und berührt den Session-Zuschnitt der Effekt-Stores, nicht
dieses Interface. Bis dahin sichert der Dedup-Schlüssel die append-artige
Projektion ab.

### `Testing/InMemoryProjectionTracker.cs`

**Was:** In-Memory-Tracker für den Single-Node-Prüfstand, inkl. Reset und einem
`Snapshot()`-Helfer (simuliert „Marken überstehen einen Node-Neustart durabel").

**Warum:** Ermöglicht die Crash-Proben OHNE Cluster. Fehler werden NICHT hier
injiziert, sondern im Effekt-Store (Phase 0.5) — dieser Tracker bleibt ehrlich
simpel, kein verstecktes Verhalten.

---

## Edits an bestehenden Dateien — ✅ angewandt

Chirurgisch, kein Neuschrieb. Alle vier sind umgesetzt (Fundorte in Klammern);
die Codeblöcke unten dokumentieren, *was* eingefügt wurde.

**1. `Interfaces.cs` (Abstractions)** — in `IEventStoreRepository`, nach
`LoadStateAsync`:

```csharp
Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(
    Guid streamId, int fromVersion, CancellationToken ct);
```

Das eine tragende neue Leseprimitiv des Pull-Ansatzes. `fromVersion` = angewandte
Marke + 1. Genutzt von Adaptern, (späteren) Treibern und dem Rebuilder.

**2. `CommandEnvelope.cs` (Abstractions)** — eine Zeile:

```csharp
public record EventEnvelope : IEventEnvelope   // vorher: IAggregateEnvelope
```

Die Property `AggregateVersion` existiert dort bereits.

**3. `MartenEventStore.cs` (Infrastructure)** — Methode einfügen:

```csharp
public async Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(
    Guid streamId, int fromVersion, CancellationToken ct)
{
    await using var session = _store.LightweightSession();

    var raw = await session.Events
        .QueryAllRawEvents()
        .Where(e => e.StreamId == streamId && e.Version >= fromVersion)
        .OrderBy(e => e.Version)
        .ToListAsync(ct);

    var result = new List<EventEnvelope>(raw.Count);
    foreach (var e in raw)
    {
        if (e.Data is not IEvent domain) continue;
        result.Add(new EventEnvelope
        {
            EventId          = e.Id,
            AggregateId      = streamId,
            AggregateVersion = (int)e.Version,
            CreatedAtUtc     = e.Timestamp,
            CorrelationId    = e.CorrelationId ?? string.Empty, // Phase-1-Lücke
            CausationId      = e.CausationId  ?? string.Empty,  // Phase-1-Lücke
            AggregateType    = string.Empty,                    // Phase-1-Lücke
            Payload          = domain
        });
    }
    return result;
}
```

**4. Marten-Konfiguration** (wo `StoreOptions` gesetzt wird, vermutlich
`CqrsServiceExtension.cs` oder `Program.cs`):

```csharp
opts.Schema.For<ProjectionCheckpoint>().Identity(x => x.Id);
```

---

## Aus dem Code gefallene Architektur-Lücke (gehört in Phase 1)

Der heutige `AppendEventsAsync` schreibt **nackte** Domain-Events, nicht die
Envelope-Metadaten. `CorrelationId`, `CausationId` und der `AggregateType`-String
leben derzeit nur im transienten `EventEnvelope`, den der Actor beim Publish baut —
sie stehen NICHT im Log und sind nach einem Log-Read nicht rekonstruierbar (die
drei mit `Phase-1-Lücke` markierten Felder oben laufen deshalb leer).

Das ist kein Detail: Die Verträge verlangen, dass diese Metadaten auch nach dem
Log-Read verfügbar sind. Der Fix (Marten-Metadaten aktivieren; beim Append
`session.CorrelationId`/`CausationId` setzen) gehört zum Per-Event-Versions-Fix in
Phase 1. Für die Crash-Proben von Phase 2 reicht der aktuelle Stand, weil die nur
`AggregateVersion` und `Payload` brauchen.

---

## Wie der Replay damit vollständig aufgeht

Die Mechanik steht lückenlos als Vertrag, mit zwei echten Tracker-Implementierungen:

- `ResetAsync` / `ResetAllAsync` löschen die Marke.
- `IRebuildableProjection.ClearAsync` leert das Ziel.
- `IProjectionRebuilder` orchestriert: leeren → Marken auf -1 → ab 0 neu lesen
  und dispatchen.

Der einzige noch fehlende Baustein ist der ausführende Rebuilder — bewusst
identisch mit der Read-und-Dispatch-Schleife des Adapters, deshalb in Phase 2.

---

## Phase 0.5 — Prüfstand — ✅ erledigt

Gebaut als Projekt **`Infrastructure.Pruefstand.Tests`** (in der Solution):

- **`Pruefstand/PruefstandAdapter.cs`** — die minimale Single-Node-Kernschleife
  (Marke lesen → ab Marke+1 lesen → Guard → Effekt → Marke), wortgleich zu Spec 7.3.
  Bewusst NICHT der generierte Cluster-Adapter (Phase 2) — nur sein sequenzieller Kern,
  damit die Proben jetzt fahrbar sind.
- **`Pruefstand/PruefstandFaults.cs`** — der eine Absturzpunkt „nach Effekt-Write,
  vor `MarkProcessedAsync`" (einmalig bewaffnbar).
- **`Pruefstand/CoCommitHistorieStore.cs`** — Effekt-Store, der Effekt + Marke ATOMAR
  committet (In-Memory-Pendant zu „eine Session, ein SaveChangesAsync") → exactly-once.
- **`CrashProbeTests.cs`** — die vier Proben gegen die echte, append-artige
  `ImagePairHistorieProjection` (über deren generierte `DispatchAsync`).

Fehler-Injektion liegt bewusst NICHT im `InMemoryProjectionTracker`, sondern im
Effekt-Pfad (Absturz im Adapter zwischen Effekt und Marke); Signalverlust/-Duplikat
werden auf Testebene modelliert (Wake weglassen bzw. zweimal aufrufen).

**Tor Phase 2 (die vier Proben) — alle grün:**

| Probe | Aussage | Ergebnis |
|---|---|---|
| 1 | verlorenes Signal heilt der nächste Read (Coalescing) | ✅ |
| 2 | doppeltes Signal folgenlos (Pre-Dispatch-Guard) | ✅ |
| 3 | Effekt und Marke gemeinsam gültig (Co-Commit) | ✅ |
| 4a | getrennte Marke → Absturz erzeugt Doppelwirkung (at-least-once, ehrlich gezeigt) | ✅ |
| 4b | Co-Commit → Absturz → Wiederholung, GENAU EIN Eintrag (exactly-once wirksam) | ✅ |

Kernbeleg (Spec 7.8): Der Adapter ist in 4a und 4b **identisch** — allein die
Store-Implementierung entscheidet über die Garantie.

Reproduzierbar: `dotnet test Infrastructure.Pruefstand.Tests`

---

## Nächster Schritt: Phase 1 — Schreibseite

Version **pro Event** (statt Batch-Endwert) stempeln, die Metadaten
`CorrelationId`/`CausationId`/`AggregateType` beim Append ins Log schreiben (die
oben markierte Phase-1-Lücke schließen) und das `StateChangeVia{Event}`-Signal
emittieren. Tor: Command → geordneter, lückenloser Stream mit korrekter Version pro
Event, Signal erscheint auf dem PubSub.
