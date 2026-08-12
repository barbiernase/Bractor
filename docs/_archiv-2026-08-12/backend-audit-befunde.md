# Backend-Audit — gefundene Probleme (Übergabe)

> **Zweck:** Handoff für einen Agenten, der diese Backend-Bugs fixt. Self-contained — kein
> Vorwissen aus der Audit-Session nötig. Jeder Befund hat Datei:Zeile, Fehlerszenario, warum
> es falsch ist, Fix-Richtung und einen Reproduktions-Hinweis.
>
> **Kontext:** Selbstgebautes CQRS/Event-Sourcing-Framework (Proto.Actor, Marten/PostgreSQL,
> Redis). Gesucht wurden STILLE Korrektheitsfehler: kompiliert, Happy-Path grün, aber unter
> bestimmten Bedingungen falsch. Reine Stil-/Perf-Themen sind NICHT enthalten.
>
> **Verifikationsstatus:**
> - **[VERIFIZIERT]** = am Code selbst gelesen und bestätigt.
> - **[PLAUSIBEL]** = starke Begründung, Laufzeitverhalten nicht selbst reproduziert — vor dem Fix bestätigen.

---

## Das Kernthema: der Heil-Backstop hat Löcher für terminale Events

Drei unabhängige Befunde (1, 2, 3) zeigen auf **dieselbe** strukturelle Schwäche: Der Poll-/
Selbst-Weckungs-Mechanismus — das Sicherheitsnetz, das „kein persistiertes Event bleibt
unverarbeitet" garantieren soll — heilt **nicht zuverlässig** für die **letzten Events vor
Stille** (Abschluss-/Terminal-Events). Alle drei sind nur gegen echtes Postgres/Cluster unter
transientem Fehler sichtbar; die In-Memory-Tests können sie strukturell nicht reproduzieren →
grüner Prüfstand verdeckt sie. **Das ist die wichtigste Baustelle.**

Gemeinsame Fix-Idee für 1–3: Der Poll-Cursor darf erst vorrücken/persistieren, wenn die
Verarbeitung **bestätigt** ist (Wake awaiten + „caught-up" zurückmelden statt fire-and-forget),
plus echte **Gap-Detection** im Scan (Straggler-Karenz), plus Selbst-Weckung auch bei verlorener
Quittung.

---

### Befund 1 — Poll-Cursor rückt über unbestätigte Fire-and-forget-Weckungen vor  ·  Schwere: HOCH  ·  [VERIFIZIERT]

**Dateien:** `Infrastructure/Projections/Poller.cs:41-44`, `Infrastructure/Projections/PullPath.cs:70-75`, `PullPath.cs:103-106`, Start ab HWM `PullPath.cs:84`.

**Was passiert:** Die Weckung ist `_ = _system.Cluster().RequestAsync<WakeAck>(identity, new Wake(), c); return Task.CompletedTask;` — der Task wird **verworfen** (fire-and-forget). `PollOnceAsync` rückt danach `_highWater = changes.HighWaterMark` **bedingungslos** vor; `PollLoopAsync` **persistiert** die HWM durabel; der nächste Boot setzt dort auf (kein Re-Scan).

**Fehlerszenario:** Ein Stream-Signal geht verloren (best-effort, normal — Invariante 2), der Poll ist die letzte Rettung. Der Poll enqueued die Weckung und rückt die HWM vor. Dann scheitert die Verarbeitung: der geweckte Adapter wirft beim Dispatch/`MarkProcessedAsync` (transienter Postgres-Fehler, Cluster-Churn), oder das `RequestAsync` selbst scheitert. Die Adapter-Marke rückt **nicht** vor, der Poll-Cursor ist aber schon vorbei. Auf einem **terminalen Stream** (keine neuen Events mehr) weckt nie wieder etwas → die Projektion hängt **still und dauerhaft** zurück.

**Warum falsch:** Der Poller-Doc-Kommentar (`Poller.cs:9-13`) verspricht, „für immer verloren" in „höchstens ein Poll-Intervall Latenz" zu wandeln. Im transienten Fehlerfall bricht genau diese Zusage. Die HWM bedeutet fälschlich „diese Streams sind aufgeholt", tatsächlich nur „diese Streams wurden einmal zum Wecken enqueued".

**Reproduktion (Ebene 2, echtes Marten):** Poller + echter `MartenEventStore` + ein Stub-Adapter, dessen `WakeAsync` beim ersten Aufruf wirft. Einen Stream anlegen, Signal unterdrücken, einen Poll laufen lassen, HWM prüfen → sie ist vorbei, obwohl der Adapter nichts verarbeitet hat. Kein neues Event → zweiter Poll weckt nicht.

---

### Befund 2 — Prozess-Selbst-Weckung fehlt bei verlorener Quittung (`result == null`)  ·  Schwere: HOCH  ·  [VERIFIZIERT]

**Dateien:** `Infrastructure/Prozess/DetachedProzessSend.cs:50-59`, `Infrastructure/Prozess/ProzessManagerActor.cs` (`SendeAnZiel` gibt bei Timeout `null` zurück), `Infrastructure/Prozess/ProzessManagerWiring.cs` (Poll-Relevanzfilter `relevanteTypen.Contains(...)`).

**Was passiert:** In `RunDetached` läuft bei `result is { Success: false }` → `beiFehlschlag`, bei `result is not null` → `danach` (= `WeckeSelbst`). Bei **`result == null`** (Timeout der Quittung) läuft **keiner der beiden Zweige** → keine Selbst-Weckung.

**Fehlerszenario (Diamant-Saga, z.B. `BestellProzess`/`ReiseProzess`):** Alle Zweige fertig, die terminale Join-Transition (`Versende`/`BestaetigeReise`) feuert. Das Ziel-Aggregat persistiert `Versendet` erfolgreich, aber die Quittung geht im Timeout verloren → `null`. Das Ergebnis-Event `Versendet` ist Auslöser **keiner** Regel → der `KorrelationsRouter` abonniert sein Signal nicht → der 30s-Poll filtert es weg (`Versendet` ∉ `relevanteTypen`, weil der Versand-Stream **kein** teilnehmendes Event trägt). **Der Prozess hängt für immer ohne `ProzessBeendet`, obwohl alle Effekte da sind.**

**Warum die linearen Sagas es verdecken:** In `UeberweisungsProzess`/`SammelueberweisungsProzess` landet die terminale Transition auf dem Quell-Konto, das früher schon das teilnehmende `BetragReserviert` bekam → der Poll findet dort ein relevantes Event und weckt zufällig neu. Bei Diamanten hat der terminale Ziel-Stream kein teilnehmendes Event → keine Heilung.

**Zweite Ausprägung (Fan-out):** In `SammelueberweisungsProzess` ist `Gutgeschrieben` nur `UndAlle`-Typ, nicht in einer `Bedingung` → nicht in `relevanteTypen`. Geht mitten im Fan-out eine `SchreibeGut`-Quittung verloren, stallt die `WeckeSelbst`-Kette und der Poll kann `Gutgeschrieben` nicht nachreichen.

**Fix-Richtung:** `WeckeSelbst` auch bei `result == null` anstoßen (der Effekt kann durabel persistiert sein), ODER dem Manager einen regel-unabhängigen Re-Wake-Backstop auf sein eigenes Log geben.

**Reproduktion (Ebene 1 möglich, Fake-Send):** Bestehende `ProzessManagerHangTests`-Machinerie nutzen; einen terminalen Send mit `null`-Quittung simulieren und prüfen, dass `ProzessBeendet` ausbleibt.

---

### Befund 3 — Postgres-Sequenz-Lücke in `ReadChangedStreamsAsync`  ·  Schwere: HOCH  ·  [PLAUSIBEL, hohe Konfidenz]

**Datei:** `Infrastructure/Persistence/MartenEventStore.cs:230-237`.

```csharp
.Where(e => e.Sequence > afterGlobalSequence).OrderBy(e => e.Sequence)...
var highWater = raw.Count > 0 ? raw[^1].Sequence : afterGlobalSequence;
```

**Was passiert:** Marten vergibt `seq_id` aus einer Postgres-Sequenz beim Insert *innerhalb der Transaktion*. Zwei nebenläufige Commits können in umgekehrter Seq-Reihenfolge sichtbar werden: T1 zieht `seq=100`, T2 zieht `seq=101`, **T2 committet zuerst**. Läuft der Poller-Scan im Fenster dazwischen, sieht er 101 (sichtbar), nicht 100 (noch uncommitted), setzt `HighWater=101`. Der nächste Scan filtert `Sequence > 101` → **Event 100 wird nie wieder gescannt.**

**Warum falsch:** Der Poller IST der Lücken-heilende Backstop. Genau dafür braucht man Gap-Detection — Martens eigener Async-Daemon hat einen `HighWaterDetector` mit Gap-Detection + Straggler-Karenz; diese naive `max(seq)`-Logik hat sie nicht. Fatal nur, wenn zusätzlich das Signal für Stream 100 verloren ging (Compound-Failure) — aber genau diesen Fall soll der Poller abfangen. **Der `InMemoryEventStore` kann es nie reproduzieren** (`++_globalSeq` synchron, keine Commit-Sichtbarkeitslücke) → alle Prüfstand-Tests grün.

**Vor dem Fix bestätigen:** Martens exakte Seq-Sichtbarkeits-Semantik gegen die verlinkte Marten-Version (8.20) reproduzieren — zwei nebenläufige Sessions mit interleaved Commit-Timing gegen echtes Postgres.

**Fix-Richtung:** Gap-Detection mit Straggler-Karenz (HWM nur bis zur höchsten *lückenlosen* Seq vorrücken; jüngste Seqs eine Karenzzeit zurückhalten), analog Martens `HighWaterDetector`.

---

## Aggregat-Schreibpfad

### Befund 4 — Gemischtes Effekt+Ablehnung → Ablehnung verschwindet still  ·  Schwere: MITTEL (latent)  ·  [VERIFIZIERT]

**Datei:** `Infrastructure/Aggregate/ActorSystem/AggregateActorBase.cs:210-232`.

```csharp
var persistentEvents = allEvents.Where(e => e is not ITransientEvent).ToList();
var rejections       = allEvents.Where(e => e is ITransientEvent).ToList();
if (rejections.Any() && !persistentEvents.Any()) { /* nur hier wird die Ablehnung zugestellt */ }
```

**Fehlerszenario:** Yieldet ein Decider `[EffektEvent, AblehnungsEvent]` zusammen (natürliches Muster „gemacht, aber mit Warnung"), ist `persistentEvents.Any()` true → der Ablehnungs-Zweig greift nicht → Fall-through, es wird nur `persistentEvents` (+Marke) geschrieben. Die `rejections`-Liste wird **nie wieder referenziert**: nicht appended, nicht per Targeted Delivery zugestellt, nicht in `CommandResult`. Aufrufer sieht `Success = true`. **Zusatz:** selbst im reinen Ablehnungsfall nur `rejections.First()` (Zeile 218) — mehrere Ablehnungen gehen verloren.

**Warum falsch:** Kompiliert, Happy-Path (Decider yieldet entweder/oder) grün. Der gemischte Pfad verliert Domäneninformation ohne Spur. Latent für die aktuellen Konto-Decider (immer entweder/oder), aber eine Falle beim ersten „Effekt + Hinweis"-Decider.

**Fix-Richtung:** Definieren, ob gemischte Ausgaben erlaubt sind. Falls ja: Ablehnungen auch im gemischten Fall zustellen. Falls nein: laut fehlschlagen (Decider-Contract-Verletzung), nicht still schlucken.

---

### Befund 5 — Live-Apply vs. Rehydration divergieren bei `IProzessIntern`-Persistent-Events  ·  Schwere: MITTEL (latent)  ·  [VERIFIZIERT]

**Dateien:** `AggregateActorBase.cs:252-256` (Live: appliziert **alle** persistenten Events ohne Filter) vs. `AggregateRehydrator.cs:57` (Rehydration: überspringt `IProzessIntern`; ebenso `InMemoryEventStore.cs:90`, `MartenEventStore.cs:156`).

**Fehlerszenario:** Ein persistentes **Domänen**-Event, das `IProzessIntern` implementiert (etwas anderes als die Framework-Marke `KommandoVerarbeitet`, die bewusst nicht in `persistentEvents` landet), wird live angewandt, nach Passivierung/Reload aber nicht → der In-Memory-State driftet still vom Live-State ab.

**Warum falsch:** Die Invariante „Live == Rehydration" ist nur per Konvention gehalten, nicht per Code — die beiden Apply-Pfade sind asymmetrisch. Heute latent (kein Domänen-Aggregat yieldet `IProzessIntern`-Persistent-Events).

**Fix-Richtung:** Der Live-Loop soll denselben `is not IProzessIntern`-Filter verwenden wie die Rehydration.

---

## In-Memory vs. Marten — Paritätslücken (verstecken echtes Verhalten in Tests)

> Diese Divergenzen sind gefährlich, weil sie **Tests grün halten, während Produktion (Marten)
> anders läuft**. Siehe `docs/teststrategie-ebenen.md` — Store-Semantik gehört auf echtes Marten.

### Befund 6 — `state.Id` vor (Marten) vs. nach (InMemory) dem Replay gesetzt  ·  MITTEL  ·  [PLAUSIBEL]
`MartenEventStore.cs:144` (`new TState { Id = aggregateId }` VOR der Fold-Schleife) vs. `InMemoryEventStore.cs:95` (`state.Id = aggregateId` NACH der Schleife). Ein Applier, der `state.Id` *während* des Replays liest, sieht in Tests `Guid.Empty`, in Prod die echte Id. Betrifft `LoadStateAsync` (Saga-/Reaktions-Tests). Fix: InMemory die Id ebenfalls vor dem Fold setzen.

### Befund 7 — Fan-out mit doppeltem Ziel blockiert den Count-Join  ·  MITTEL (latent)  ·  [PLAUSIBEL]
`Domain/Sammelueberweisung/SammelueberweisungsProzess.cs:27-30`, `Abstractions/ProzessId.cs:35`, `ProzessManager.cs:187`. Enthält `Ziele` denselben Ziel-Kontostand zweimal → identischer Vorgang (Diskriminator = Ziel-Id) → das Ziel dedupliziert → nur `N-1` `Gutgeschrieben`, aber der Count-Join erwartet `N` → `BucheReservierung` feuert nie, Prozess hängt. Fix: Vorgang-Diskriminator um einen Instanz-Index ergänzen.

### Befund 8 — Vorgang-Kollision: RegelIndex fehlt im Diskriminator  ·  MITTEL (latent)  ·  [PLAUSIBEL]
`Abstractions/ProzessId.cs:35-45`, `ProzessManager.cs:150-156`. Der Vorgang = `hash(korrelation, primär.Stream, primär.Version, commandTyp, ziel-Id)` — **ohne RegelIndex**. Zwei Regeln mit gleichem Auslöser-Token, gleichem Command-Typ und gleichem Ziel erzeugen denselben Vorgang → ein Ergebnis-Event matcht beide Transitionen (`FirstOrDefault(e => e.CausationId == vorgang)`). Aktuelle Sagas kollidieren nicht, aber weder Builder noch Manager verhindern die Definition. Fix: `RegelIndex` in den Vorgang-Diskriminator aufnehmen.

### Befund 9 — `Math.Abs(int.MinValue)` OverflowException in `GetShardIndex`  ·  NIEDRIG (~2⁻³²)  ·  [VERIFIZIERT]
`Infrastructure/PubSub/BrokerIdentity.cs:63`: `Math.Abs(hash) % ShardCount`. `hash` wird `unchecked` berechnet, kann `int.MinValue` sein; `Math.Abs(int.MinValue)` wirft (das `unchecked` deckt den `Math.Abs`-Aufruf nicht). Ein `subscriberId` (Session-GUID oder Projektionsname), der auf `int.MinValue` hasht, crasht Subscribe UND Targeted-Publish konsistent. Fix: `(hash & 0x7FFFFFFF) % ShardCount`.

### Befund 10 — `CommandEnvelope.ExpectedVersion`-Default = `AnyVersion` (-1)  ·  NIEDRIG (Footgun)  ·  [VERIFIZIERT]
`Abstractions/CommandEnvelope.cs:19`. Ein handgebauter `CommandEnvelope` ohne explizites `ExpectedVersion` läuft still auf den idempotenten Pfad: kein OCC + eine `KommandoVerarbeitet`-Inbox-Marke wird co-committet. Bekannte Call-Sites setzen es korrekt (heute nicht aktiv getriggert). Ein sichererer Default wäre `0` (lauter OCC-Konflikt statt stiller Stream-Verschmutzung). Vor Änderung: Reaktions-/Prozess-Pfade prüfen, die bewusst `AnyVersion` brauchen.

### Befund 11 — weitere Paritäts-/Defensiv-Lücken  ·  NIEDRIG/latent  ·  [PLAUSIBEL]
- `InMemoryEventStore.ReadStreamAsync` lässt `EventId`/`CreatedAtUtc` leer (`InMemoryEventStore.cs:119-127`) vs. Marten füllt sie. Heute liest kein Konsument diese Felder — latent.
- `InMemoryEventStore` ist nicht thread-safe (`_appendLog`/`_globalSeq` ohne Lock, `InMemoryEventStore.cs:38,49-54,66`) → parallele Appends verschiedener Aggregate → „Collection was modified"/torn read. Äußert sich als Test-Flakiness.
- `MartenProjectionTracker.MarkProcessedAsync` ist nicht monoton (`MartenProjectionTracker.cs:47-60`, kein Guard `version > current`) → bei out-of-order-Aufruf springt der Checkpoint zurück. Heute durch Actor-Serialisierung sicher.
- OCC-Mapping: Der Catch fängt nur `EventStreamUnexpectedMaxEventIdException` + `ExistingStreamIdCollisionException` (`MartenEventStore.cs:99,111`). Falls Marten 8.20 bei Append-mit-Zielversion stattdessen `Marten.Exceptions.ConcurrencyException` wirft, würde sie als technischer Fehler (→ `CommandFailed`) statt als retry-barer Konflikt behandelt. Gegen die Marten-Version prüfen.

---

## Als KORREKT verifiziert (nicht erneut jagen)

- Kern-Versionsarithmetik & Inbox-Zählung (Event i → `baseVersion+i+1`; Marke mitgezählt, nicht angewandt; keine Doppel-Inkremente).
- OCC-Skip auf dem `AnyVersion`-Pfad; `events.Count` inkl. Marke → OCC-Zielversion stimmt in beiden Stores.
- Append vor State-Mutation → bei Append-Fehler bleibt `_state` unangetastet.
- Shard-Hash-Konsistenz Subscribe ↔ Targeted-Publish (eine Funktion `BrokerIdentity.GetShardIndex`); `AllShards` deckt alle Shards ab.
- ProjectionAdapter-Guard (`e.AggregateVersion <= applied`), kein Off-by-one; Tracker liefert -1 für ungesehene Streams.
- Signal→Wake-Mapping (kein Closure-Capture-Bug, keine Kreuzverdrahtung); Poll-vs-Signal-Serialisierung über gleiche Cluster-Identität.
- Kompensations-Dedup (deterministischer Kompensations-Vorgang; abgelehnte/transiente Schritte werden korrekt nicht kompensiert); Count-Join feuert nicht verfrüht/doppelt.
- Snapshot-Schema-Gating; Deep-Clone friert State+Inbox in-turn ein; stale/fehlender Snapshot folgenlos.
- Metadaten-Readback gegen Marten (`HeadersEnabled=true`, Null-Header → `string.Empty`).

---

## Empfohlene Reihenfolge für den Fix-Agenten

1. **Befunde 1–3** zuerst (das Kernthema, direkt an der „kein Event-Verlust"-Anforderung). Erst einen **Ebene-2-Test** schreiben, der 1 (und, wenn bestätigt, 3) gegen echtes Marten reproduziert — dann fixen. Für 2 reicht ein Ebene-1-Test mit Fake-Send.
2. **Befunde 4, 9** — schnelle, sichere Fixes.
3. **Befunde 5, 6, 7, 8** — latente Fallen; Fix + je ein Test, der den Auslöser scharf macht.
4. **Befunde 10, 11** — defensive Härtung.

Teststrategie & Ebenen: `docs/teststrategie-ebenen.md`. Bekannte Cluster-Cold-Boot-Flakiness: `memory/snapshot-e2e-flake-clusterboot.md`.
