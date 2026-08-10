# Review-Befunde Schreibpfad & Garantien (2026-08)

> Entstanden aus einem Lese-Review des Command-Schreibpfads im Rahmen der Schreibpfad-Perf-Arbeit
> (`docs/backend-perf-untersuchung-bericht.md`). Gelesen: `BatchingEventAppender`, `MartenEventBatchWriter`,
> `MartenEventStore`, `AggregateActorBase`, `BoundedInbox`, `CommandEmitter`, `ProzessManager` + die
> Entwickler-Anleitung. **Kein Code geändert** — reine Befund-Sammlung. Prüfstand-Baseline zum Zeitpunkt
> des Reviews: **99/99 grün**.
>
> Verwandt, aber getrennt: `docs/backend-audit-befunde.md` (früherer Audit, Befunde #1–#12 dort bereits
> behoben). Dieses Dokument sammelt **neue** Beobachtungen aus dem Batching-/Parallel-Drain-Kontext.

---

## Kurzfazit der Bewertung

Reifes, invarianten-getriebenes Framework: reine Domäne, deklarative Prozess-DSL, Garantien **ehrlich
abgegrenzt** (an jeder weichen Stelle nennt der Code die Schwäche und legt ein Netz drunter). Die
Denk-Dokumentation in den Kommentaren ist überdurchschnittlich (dokumentiert *warum*, inkl. verworfener
Alternativen). Die drei Befunde unten sind **kein** Widerspruch dazu — sie sind Randfälle bzw. latente
Hazards fürs spätere Wachstum, keine akuten Korrektheitsbrüche im heutigen Betrieb.

**Positiv-Befund vorweg (kein Bug):** Der parallele Commit-Drain führt **keinen** neuen Sequence-Gap-Fehler
ein. `MartenEventStore.ReadChangedStreamsAsync` rückt die Poll-HWM bereits zeitbasiert vor
(`_stragglerGrace`, Default 3 s) statt naiv auf `max(seq)` — die out-of-order-Commit-Sichtbarkeit
nebenläufiger Transaktionen war also schon vor dem Drain eingeplant. Sauber vorweggenommen.

---

## Befund A — Ambiguous-Commit, durch Batching von 1 auf N amplifiziert  ⚠ (neu, real)

**Wo:** `Infrastructure/Persistence/BatchingEventAppender.cs:169` (`FlushAsync` → `catch (Exception) →
IsolateAsync`).

**Was:** `FlushAsync` fängt **jede** Exception aus `WriteBatchAsync` und isoliert dann jeden Auftrag einzeln
über den inneren Store. Das ist korrekt für einen **sauberen OCC-Konflikt** (die Transaktion rollte
garantiert zurück → Einzel-Re-Append ist safe). Es ist **nicht** korrekt für einen **mehrdeutigen Commit**:
ein Netzwerk-Timeout / Connection-Reset, *nachdem* Postgres die Transaktion committet hat, aber *bevor* der
Client das ACK sieht (klassischer in-doubt/ambiguous commit).

**Ablauf im mehrdeutigen Fall:**
1. Der Batch **ist durabel** (Postgres hat committet), der Client sieht nur die Timeout-Exception.
2. `IsolateAsync` hängt jeden der N Aufträge einzeln neu an (`_inner.AppendEventsAsync`).
3. Jeder Re-Append trifft jetzt einen echten OCC-Konflikt (der Stream steht bereits auf der Zielversion)
   → `ConcurrencyException` → `p.Tcs.TrySetException(...)`.
4. Ergebnis: **ein** Netzwerk-Blip verwandelt einen *erfolgreichen* Batch von N Commands in **N
   falsch-negative** `CommandResult.Success=false`. Ohne Batching wäre der Radius genau 1.

**Auswirkung / Grenzen:**
- **Exactly-once bricht NICHT** — die OCC weist den Re-Append ab, es entsteht kein Doppeleffekt.
- **Prozess-/Emit-Pfad ist immun** — der `ProzessManager` faltet den durablen Effekt aus dem Ziel-Stream
  und ignoriert die Quittung (fire-and-forget). Ein falsch-negatives `CommandResult` verpufft dort.
- **Betroffen ist der Client-OCC-Pfad:** der Aufrufer bekommt „fehlgeschlagen" für einen Command, der in
  Wahrheit durabel ist (falsches Negativ). Ein naiver Client-Retry mit der alten `ExpectedVersion` wird
  danach sauber per OCC abgewiesen (kein Doppelschreiben) — aber die Fehler-Semantik ist verfälscht, und
  Batching vergrößert den Blast-Radius von 1 auf die Batch-Größe.

**Warum „still":** tritt nur unter DB-Stall / Netzwerk-Partition auf, erscheint in keinem grünen Test. Der
Code kennt die Ambiguität an anderer Stelle sogar (Audit-Fix #2, Kommentar zu `SaveChangesAsync` in
`CqrsFrameworkBuilder.CommandTimeoutSeconds`), zieht die Konsequenz im **Batch-Isolationspfad** aber noch
nicht.

**Fix-Richtung (Vorschlag, nicht umgesetzt):** Im `catch` von `FlushAsync` **OCC-Exceptions von
transienten/Connection-Exceptions trennen**. Nur bei einer eindeutigen OCC-/Rollback-Exception blind
isolieren. Bei einer mehrdeutigen (Timeout/Connection) zuerst **rekonzilieren**: pro Stream einmal
zurücklesen (`ReadStreamAsync`/Version prüfen) und einen bereits-auf-Zielversion-Stream als **Erfolg**
quittieren (`Tcs.TrySetResult`) statt als OCC-Fehlschlag. Kosten: ein Read pro betroffenem Stream nur im
seltenen Fehlerfall.

**Erreichbarkeit:** niedrig (nur unter DB-Stall/Partition), Auswirkung mittel (falsch-negative
Client-Quittungen, kein Datenverlust/Doppeleffekt). **Relevanz steigt mit K** (mehr parallele Drains,
remote-Postgres mit höherer Latenz → größeres Timeout-Fenster) — also genau im Zielkorridor der laufenden
Perf-Arbeit im Blick behalten.

---

## Befund B — Snapshot-Clone via System.Text.Json ohne Round-Trip-Garantie (latent)

**Wo:** `Infrastructure/Aggregate/ActorSystem/AggregateActorBase.cs:584` (`Clone`) →
`WriteSnapshotDetached` (Zeile 554).

**Was:** Der Snapshot friert den State per STJ-Serialize/Deserialize-Roundtrip ein; aus dem Snapshot wird
später rehydriert (`AggregateRehydrator`). Es gibt **keine Validierung**, dass der State STJ-verlustfrei
roundtrippt.

**Auswirkung:** Für die heutigen Aggregate (Konto/Lager/Versand — flache Werttypen) harmlos. Sobald ein
State ein Feld trägt, das STJ nicht verlustfrei serialisiert (polymorphe Felder, private Setter ohne
passende Ctor-Bindung, bestimmte Dictionary-Key-Typen, custom Werttypen ohne Converter), **weicht der
Snapshot-State still vom wahren State ab**. Die Rehydration seedet dann einen falschen Ausgangs-State, und
alle Events ab `snapshot.Version+1` applyen darauf → **stille State-Korruption**. Der Fehler manifestiert
sich erst nach *Snapshot + Deaktivierung + Reaktivierung* — nie im kurzen Test (Threshold Default 200, in
Tests klein/aus).

**Fix-Richtung:** Ein billiger Boot-/Dev-Guard: einmal `Clone(state)` gegen `state` vergleichen
(strukturelle Gleichheit) bevor der erste Snapshot geschrieben wird, oder ein Serialisierbarkeits-Check je
State-Typ beim Start (analog zur DTO-Registrierung). Alternativ den Deep-Copy-Mechanismus an den bereits
vorhandenen reflection-freien Event-JSON-Baustein koppeln, wenn State-Typen dort registriert werden.

**Erreichbarkeit:** heute null (State-Typen zu einfach), aber **wächst mit jeder reicheren Aggregat-Struktur**
— ein wartender stiller Bug, der genau dann zuschlägt, wenn ihn niemand mehr mit dem Snapshot-Pfad in
Verbindung bringt.

---

## Befund C — Inbox-Cap 10 000 als Exactly-once-Grenze (bekannt, ehrlich zu nennen)

**Wo:** `Infrastructure/Aggregate/BoundedInbox.cs` (Cap aus `CqrsFrameworkBuilder.InboxCap`, Default 10 000).

**Was:** Die Dedup-Menge des idempotenten (Emittiert-)Pfads verdrängt FIFO bei > `cap` distinkten Vorgängen.
Der Kommentar argumentiert korrekt: das Re-Delivery-Fenster ist kurz (Poll ~30 s; der Manager feuert einen
Vorgang nur bis zur ersten Marke), also kann die *normale* Maschinerie keinen > 10k-alten Vorgang
re-liefern.

**Restrisiko:** ein **echt verspätetes Draht-Duplikat** auf einem sehr heißen, geteilten Ziel-Aggregat —
wenn zwischen ursprünglichem Send und verspäteter Zustellung > 10 000 distinkte emittierte Commands am
selben Aggregat vorbeigelaufen sind (innerhalb der 5-s-Send-Frist des `CommandEmitter`). Dann ist die
Dedup-Marke verdrängt → der wiederholte Command wird **erneut wirksam** → Doppeleffekt, still.

**Auswirkung:** schmal und heute unerreichbar (keine so heißen geteilten Ziele), aber es ist die eine Stelle,
an der „exactly-once effektiv" zu „at-least-once" degradiert, **ohne Signal**. Als dokumentierter Tradeoff
akzeptabel; relevant, sobald ein zentrales, viel-frequentiertes Ziel-Aggregat entsteht (z. B. ein globales
Konto/Ledger, an dem viele Prozesse ziehen). Dann: Cap pro Aggregat-Typ konfigurierbar hochsetzen, oder für
solche Ziele einen unbegrenzten/store-gestützten Inbox-Schlüssel `(AggregateId, CommandId)` erwägen.

---

## Perf-Cliff (kein Bug) — Marking-Faltung liest Ziel-Streams voll ab 0

**Wo:** `Infrastructure/Prozess/ProzessManager.cs:202` (`FaltMarkingAsync`), `Lies(...)` →
`_store.ReadStreamAsync(s, 0, ct)`.

**Was:** Bei **jeder** Weckung faltet der Manager sein Marking, indem er jeden beteiligten Ziel-Stream
**komplett ab Version 0** liest; der Fixpunkt-Loop (`while (geändert)`) kann Streams mehrfach anfassen (der
Read ist pro Weckung gecacht, aber nicht über Weckungen hinweg).

**Auswirkung:** O(Stream-Länge) pro Weckung pro Ziel. Für langlebige Prozesse mit **heißen** Ziel-Aggregaten
(tausende Events) wird die Marking-Faltung teuer — sie ist der plausible **nächste** Flaschenhals, sobald
der reine Aggregat-Append durch den parallelen Drain / COPY schneller ist. Korrektheit unberührt; reine
Skalierung der Prozess-Schicht, nicht der Aggregat-Schreibseite. **Nicht Teil des aktuellen
Schreibpfad-Scopes**, aber gut zu wissen, bevor die Prozess-Last steigt.

**Fix-Richtung (später):** die Faltung inkrementell machen (ab dem zuletzt gefalteten Versionsstand je
Ziel-Stream lesen, analog zum `IEmittentenCursor`-Muster), statt jedes Mal ab 0.

---

## Nicht-Befunde / bewusst geprüft und für sauber befunden

- **Single-Activation trägt den parallelen Drain:** der Actor awaited seine Batch-Quittung (`p.Tcs.Task`)
  vor dem nächsten Command → nie zwei Appends desselben Streams gleichzeitig im Batch → zwei parallele
  Commits berühren nie dieselbe `(Stream, Version)`. Bestätigt.
- **Co-Commit der Inbox-Marke:** die `KommandoVerarbeitet`/`KommandoAbgelehnt`-Marke reist in **derselben**
  `AppendEventsAsync`-Liste wie der Domänen-Effekt → selber `Pending` → selbe Batch-Transaktion → atomar.
  Exactly-once-Naht durch das Batching unberührt.
- **Durabilität vor Quittung:** `FlushAsync` setzt `Tcs.TrySetResult` erst **nach** `WriteBatchAsync`
  (Commit) → „append vor Mutation" bleibt.
- **Poll-Sequence-Gap:** durch `_stragglerGrace` zeitbasiert abgefangen (siehe Kurzfazit).

---

## Rangfolge (Empfehlung)

1. **Befund A** zuerst adressieren, wenn K/Parallelität hochgedreht oder auf remote-Postgres gegangen wird
   (dort wächst das Ambiguitätsfenster). Fix ist billig und lokal (Exception-Typ-Split + Read-back-Rekonzil
   im seltenen Fehlerfall).
2. **Befund B** mit einem billigen Boot-Guard absichern, bevor reichere State-Typen entstehen.
3. **Befund C** im Kopf behalten; erst handeln, wenn ein zentrales heißes Ziel-Aggregat kommt.
4. **Perf-Cliff** erst relevant, wenn die Prozess-Last die Aggregat-Schreiblast einholt.
