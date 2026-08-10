# Performance-Untersuchung Schreibpfad — Bericht

> **Frage am Anfang:** Lohnt sich ein eigener, COPY-basierter Event-Store (statt Marten-INSERT),
> und ist der synchrone Redis-Zugriff im Command-Turn ein Problem? **Antwort:** Beide waren
> Fehlspuren. Der reale Hebel war ein **paralleler Commit-Drain** (+48 % Durchsatz, sicher,
> billig) — gefunden, weil wir **gemessen haben, bevor wir gebaut haben.**

Alle Zahlen: `--mode aggregate`, 4-Kern-localhost, Postgres/Redis/Consul nativ. Der Aggregate-Bench
schreibt den **`Modus.Emittiert`-Pfad** (2 Events/Command: Domain-Event + Exactly-once-Inbox-Marke) —
echte Client-OCC-Commands schreiben 1 Event und liegen real höher.

---

## Methodik: messen vor bauen

Jeder Kandidat wurde **erst isoliert vermessen**, bevor Aufwand floss. Das hat drei potenzielle
Baustellen (Serializer-Umbau, Redis-Detach, COPY-Event-Store) auf die eine reduziert, die wirklich trägt.

---

## Befund 1 — Serialisierung ist irrelevant (0 %)

STJ-Source-Gen-Serializer (reflection-frei) opt-in in Marten gesteckt und A/B gemessen.

| | Ø Durchsatz |
|---|---|
| Baseline (STJ-Reflection, Marten-Default) | ~3236 msg/s |
| STJ-Source-Gen an | ~3331 msg/s |

**~0 %, im Rauschen.** Beweis, dass es kein Messartefakt ist (`--mode serbench`):
- Der Source-Gen-Kontext wurde **nachweislich genutzt** (liefert TypeInfo für Events, nicht für `Dictionary<string,object>`; `Combine` wählt das erste Nicht-null).
- Source-Gen ist **real 2,48× schneller** roh (244 ns vs 606 ns/kleines Event).
- **Amdahl:** Serialisierung = 0,04–0,2 % der Append-Wall-Clock. Ein 2,48× schnellerer Serializer auf
  0,04 % ist end-to-end unsichtbar.

→ Der „Serialisierungs-Gewinn", der den COPY-Umbau mit-rechtfertigen sollte, **existiert in dieser Last nicht.**

---

## Befund 2 — Synchroner Redis-Zugriff ist irrelevant (0 %, localhost)

Der Version-Index wird pro Command **synchron im Turn vor `context.Respond`** aktualisiert. Analyse:
sein **einziger** Leser (`ReadModelDepsWriter`) liest **fremde** Aggregat-Versionen (die kausale kommt
aus dem Envelope, nicht aus Redis) → kein Read-your-writes-Bedarf → der Index ist seiner Natur nach
fire-and-forget. A/B mit No-op-Tracker (`EnableVersionTracking=false`):

| | Durchsatz | p50 |
|---|---|---|
| Redis-sync AN | ~3266 msg/s | 32 ms |
| Redis AUS | ~3350 msg/s | 33 ms |

**Weder Durchsatz noch Latenz messbar betroffen.** Der ~0,1-ms-localhost-Hop (pipelined) verschwindet
hinter den ~32 ms p50.

→ Detach **nicht gebaut** (kein Eingriff am heißen Actor-Pfad für 0 Gewinn). **Einschränkung:** gemessen
wurde localhost-Redis; mit **remote** Redis (mehrere ms RTT) säße der Hop auf dem Single-Writer-Pfad und
würde sehr wohl drücken — dann ist Detach (per-Actor verkettet) korrekte, billige Versicherung.

---

## Befund 3 — Der DB-Schreibpfad ist der Flaschenhals (Profil)

Group-Commit instrumentiert (echte `SaveChangesAsync`-Zeit + Batch-Größen):

- **97 % der Wall-Clock = serielle `SaveChangesAsync`-Zeit** → der Batcher hat EINEN Drain-Loop, ein
  Commit nach dem anderen. System ist **commit-gebunden.**
- Commit ~14–18 ms/Batch, **~0,11 ms/Event, ~konstant über Batch-Größen** → pro-Event-gebunden, fixe
  Pro-Commit-Kosten klein → **größere Batches helfen nicht** (Linger schadet sogar).
- **Nicht WAL-fsync:** `synchronous_commit=off` → keine Änderung.
- **Nicht CPU:** unter Volllast postgres ~0,68 Kern + dotnet ~0,13 Kern = **~0,8 von 4 → ~80 % idle.**
  Der Commit **wartet** (Round-Trip/Postgres-intern), rechnet nicht.
- **Parallelitäts-Probe** (2 Instanzen, dieselbe DB): 3363 → 5282 msg/s = **1,57×** (sub-linear; ein
  geteilter Serialisierungspunkt im Postgres-Schreibpfad — WAL-Insert/Sequence — kappt es unter 2×).

→ Diagnose: **ein serieller Commit-Loop, dessen Commits warten (nicht rechnen, nicht fsyncen), bei
80 % idler CPU.** Der Hebel ist Parallelität, nicht schnellere Einzel-Commits.

---

## Befund 4 — Paralleler Commit-Drain (der Gewinn, ausgeliefert)

K unabhängige Drain-Loops teilen sich den Channel, jeder committet auf eigener Session/Connection.
**Sicher durch Single-Activation:** ein Stream liegt nie gleichzeitig in zwei Batches (der Actor awaited
seine Batch-Quittung), → zwei parallele Commits berühren nie dieselbe `(Stream, Version)`. Isolations-Retry
+ Exactly-once-Inbox bleiben unberührt.

| DrainPar | Durchsatz | vs. seriell | Exactly-once |
|---|---|---|---|
| 1 | 3249 msg/s | — | ✓ |
| 2 | 4589 | +41 % | ✓ |
| **4 (Default)** | **4805** | **+48 %** | ✓ |
| 8 | 4713 | +45 % | ✓ |

Knie bei 2, Peak bei 4. **Korrektheit unter echter Parallelität in jedem Lauf verifiziert** (alle Salden exakt).

---

## Was sich gelohnt hat

| Kandidat | Gemessener Effekt | Ergebnis |
|---|---|---|
| STJ-Source-Gen-Serializer | 0,04 % → **0 %** | verworfen (Baustein für COPY bleibt) |
| Redis-sync detachen | **~0 %** (localhost) | nicht gebaut (nur remote relevant) |
| **Paralleler Commit-Drain** | **+48 %** | **umgesetzt, neuer Default** |
| COPY-Event-Store | (senkt zusätzlich Pro-Event-Kosten) | zurückgestellt (2-Wochen-Wette) |

Die billige, sichere Verbesserung war **nicht** die, um die die Diskussion ursprünglich kreiste (COPY),
sondern die, die erst das Profil sichtbar gemacht hat.

---

## Gelieferte Artefakte

- **`BatchingEventAppender`**: paralleler Commit-Drain, `AppendDrainParallelism` (Default 4). *(fa677f3)*
- **`GeneratedEventJson` + `EventJsonSerializerContext`**: reflection-freier Event↔JSON-Dispatch
  (Baustein, falls COPY je kommt; opt-in via `UseGeneratedJsonSerializer`). *(28aec18)*
- **`NullVersionTracker` + `EnableVersionTracking`**: Redis-loser Betrieb + Messvehikel. *(88c88b3)*
- **Profiling-Werkzeuge**: `BatchWriterStats` (Commit-Zeit/Batch-Größen), LoadHarness `--mode serbench`,
  Env-Toggles `BRACTOR_DRAIN_PAR` / `BRACTOR_BATCH_MAX` / `BRACTOR_SOURCEGEN_JSON` / `BRACTOR_VERSION_TRACKING`.
  *(8af6f21, 93b0168)*

---

## Ehrliche Einschränkungen

- **Sub-linear:** 4 Streams → ~1,5×, nicht 4× — Postgres-interne Schreib-Serialisierung kappt. Der exakte
  `wait_event` unter Last ist **noch nicht** aufgelöst (`pg_stat_activity`) → würde sagen, ob K>4 auf
  größerer Hardware weiter skaliert.
- **Default 4** ist das Optimum *dieser* 4-Kern-localhost-Box; pro Deployment tunebar. Remote-Postgres
  (höhere Latenz → mehr Wait) begünstigt höheres K.
- **Bench-Pfad:** die Zahlen sind der `Modus.Emittiert`-Pfad (2 Events/Command). Client-OCC-Commands
  schreiben 1 Event → real höher.
- Alle Messungen **single-node localhost**. Cross-node/remote nicht vermessen.

---

## Wie wir weitermachen (Hebel-Rangfolge)

1. **`wait_event` auflösen** (`pg_stat_activity` unter Last): *wogegen* warten die parallelen Drains?
   Sagt, ob mehr Parallelität auf Produktions-Hardware trägt und ob ein Postgres-Tuning (WAL, Sequence-Cache)
   den geteilten Serialisierungspunkt löst. **Billig, hoher Erkenntniswert — empfohlener nächster Schritt.**
2. **DrainPar pro Deployment tunen** (mehr Kerne / remote-Postgres → höheres K messen).
3. **COPY-Event-Store** (Weg 2, eigene Tabelle + `UNIQUE(stream,version)`): senkt die ~0,11 ms/Event-Insert-
   Kosten zusätzlich; nur, wenn (1)+(2) nicht reichen. Aufwand ~2 Wochen + Ownership (siehe frühere Analyse);
   der reflection-freie JSON-Baustein steht bereits.
4. **Remote-Redis-Detach** (nur falls produktiv remote-Redis): per-Actor verketteter fire-and-forget.

**Nicht weiterverfolgen:** Serializer-Optimierung (bewiesen wertlos), größere Batches/Linger (bewiesen wertlos).
