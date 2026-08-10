# P5(b) — Marking-Cursor: Handoff & Agenten-Prompt

> Auftrag für einen **frischen Agenten**, P5(b) umzusetzen. Diese Datei ist der Einstieg: erst der Kontext
> (warum, was genau, wo im Code, welche Fallen), unten der fertige **Prompt** zum Kopieren.
>
> **Governance:** P5(b) war bisher bewusst zurückgestellt (`docs/naechster-agent-prompt.md`: „NICHT anfangen").
> **Diese Datei ist die ausdrückliche Freigabe.** Alles andere Zurückgestellte (P5(b) war das eine, Multi-Node
> P7/P8 das andere) bleibt zurückgestellt.

## 1. Was P5(b) ist (in einem Satz)
Ein **nicht-autoritativer Cache** des gefalteten Prozess-Markings, damit der `ProzessManager` bei jeder Weckung
nur den **Tail** der Ziel-Streams nachfaltet statt alles **ab 0** — Reads von **O(N²) → O(N)**. Reine
Performance; die Korrektheit (P5(a), Terminal-/Poll-Routing) ist schon fertig.

Vollständige Herleitung, Darstellung, Proben, Aufwand-Ehrlichkeit: **`docs/prozess-marking-cursor-konzept.md`**
(§0–§8). Diese Datei ergänzt das Konzept um die **echten Code-Anker** und einen konkreten Fahrplan.

## 2. Der Ist-Zustand im Code (genaue Anker)
Der Manager ist `Infrastructure/Prozess/ProzessManager.cs` — ein Petri-Netz-Interpreter, „Struktur aus Code,
Marking aus dem Log". Die relevanten Stellen:

- **`WakeAsync`** (`ProzessManager.cs:64`) — EIN Schritt pro Weckung; ruft `FaltMarkingAsync`, entscheidet
  Vorwärts/Kompensation/Terminal. **Bleibt der Entscheider** — P5(b) ändert nur, WOHER das Marking kommt.
- **`FaltMarkingAsync`** (`ProzessManager.cs:202`) — **das Ziel des Umbaus.** Der Fixpunkt liest jeden
  Ziel-Stream **ab 0** über den nur-pro-Weckung-Cache `Lies(...)` (`:205–210`, `ReadStreamAsync(s, 0, ct)`).
  Genau dieses „ab 0 bei jeder Weckung" ist das O(N²).
- **`Kandidat`** (`ProzessManager.cs:198`) — trägt **DREI Achsen** je Transition, die `WakeAsync` liest:
  `ErgebnisDa` (irgendein Ziel-Event mit `CausationId == vorgang` — auch die Inbox-Marke), `WirkungDa`
  (ein DOMÄNEN-Event, kein `IProzessIntern` → kompensierbar + aktiviert Downstream-Joins), `AbgelehntDa`
  (die durable `KommandoAbgelehnt`-Marke). **Diese drei Achsen MUSS die kompakte Marking-Darstellung pro
  Vorgang erhalten** — sonst bricht die Vorwärts/Kompensations/Terminal-Logik.
- **`Belegungen`/`Matches`** (`:279`/`:297`) — Join- und Count-Join-Matching über die Tokens. Ein Token
  (`:180`) = `(Payload, Stream, Version)`. Nur eine **Wirkung** legt ein neues Token in den Fold (`:261–265`)
  und kann so einen Join scharf schalten — eine reine Marke (Noop/Ablehnung) ist inert.
- **`Vorgang`-Ableitung** (`ProzessId.FürTransition`, `:239`) mit Diskriminator `{ri}:{ci}:{cmd.AggregateId:N}`
  (Befund 7/8). Der Cursor muss denselben deterministischen `Vorgang` bilden — sonst zerfällt das Ergebnis-Matching.
- **`LadeStatusAsync`** (`ProzessManager.cs:377`) — faltet das **Manager-Log** (nur Entscheidungen:
  `ProzessGestartet`/`SchrittGescheitert`/`ProzessBeendet`). Klein, **bleibt unverändert** (das ist NICHT das O(N²)).

Konstruktion/Verdrahtung (hier kommt der neue Store rein):
- `ProzessManager` wird in **`ProzessManagerActor.cs:68`** gebaut; das Kind in **`ProzessManagerWiring.cs:28`**
  (`ProzessManagerKind.CreateKind`). Ein optionaler `IProzessMarkingStore` wird hier durchgereicht (wie heute
  `IProzessOffenIndex`/`IDeadLetterSink` optional sind).

## 3. Die Sicherheitsgarantie (warum das risikoarm ist — der tragende Punkt)
Der Manager feuert **at-least-once** (fire-and-forget, `_dispatch`), und das Ziel **dedupliziert** über den
deterministischen `Vorgang == CommandId` (Framework-Inbox). Ein **falscher/veralteter** Cache kann daher im
schlimmsten Fall eine Transition **erneut feuern** — die beim Empfänger **verpufft**. **Nie ein falscher
Effekt.** Und: der Manager KANN jederzeit ab 0 falten (Voll-Fold bleibt der Fallback bei fehlendem/stale Cache),
also bleibt „Marking aus dem Log" (Invariante 1) gewahrt — der Cache ist nur ein Beschleuniger. Das ist die
Lizenz, den Cursor überhaupt zu bauen.

## 4. Die harten Fallen (Konzept §4, hier mit Code-Bezug)
1. **Kompakte Darstellung ist Pflicht** (`docs/…-konzept.md` §4). „Alle Tokens roh persistieren" bringt O(N²)
   zurück. Nötig: Count-Join (`UndAlle<E>(n)`) → **Zähler + Done-Set** der quittierten Vorgänge; Fan-out
   (`SendeJe`) → **Done-Set der Zweige**; lineare Kanten → letzter quittierter Vorgang je Kante.
2. **Die drei Achsen pro Vorgang erhalten** (siehe `Kandidat`, `:198`). Ein Done-Set allein verliert
   Wirkung-vs-Ablehnung — genau die Unterscheidung, an der `WakeAsync` Kompensation vs. Terminal entscheidet.
   Der Cache muss je aufgelöstem Vorgang ein kleines Status-Tripel `(Wirkung? / Abgelehnt?+Grund / nur-Noop)`
   führen — plus die von einer Wirkung erzeugten **Downstream-Tokens** (kompakt), die Joins scharf schalten.
3. **Neue Ziel-Streams tauchen mitten im Fold auf** (`:261–265`): eine Wirkung aktiviert eine Downstream-
   Transition, deren Ziel ein NEUER Stream ist. Der Cursor liest solche Streams **einmalig ab 0** (neu = kurz)
   und nimmt sie in `StreamCursor` auf (Konzept §2, Schritt 4). Der inkrementelle Fixpunkt muss das können.
4. **`RegelHash`-Invalidierung** (Konzept §2/§5): ändern sich die Prozess-Regeln, ist der Cache ungültig →
   Voll-Fold ab 0. Ein `RegelHash` (Struktur-Hash der `ProzessRegeln`) existiert **noch nicht** — M3 baut ihn.

## 5. Fahrplan (M0–M3, aus Konzept §6, hier verschärft)
**M0 — Vertrag.** `ProzessMarking`-Record (Konzept §2) + `IProzessMarkingStore` (`LadeAsync`/`SchreibeAsync`,
best-effort). InMemory + Marten (Schema-Reg wie die anderen Docs in `CqrsServiceExtension.cs:128–…`). **Tor:** kompiliert, rein additiv.

**M0.5 — Nutznießer + Benchmark bauen (NEU, Voraussetzung für M3).** Es existiert **kein großer Prozess**.
Bau einen echten **breiten Fan-out-Prozess** (z. B. „Sammelüberweisung" / Massen-Auszahlung: `SendeJe` über N
Ziele + Count-Join `UndAlle<E>(n)`) als Domänen-Beispiel + einen Read-Zähler der Stream-Reads. Ohne den gibt es
nichts zu optimieren und keinen M3-Beleg. **Tor:** der Prozess läuft (klein) grün auf dem heutigen Voll-Fold.

**M1 — Inkrementeller Fixpunkt neben dem Voll-Fold.** `MarkingKompakt` + eine `FaltMarkingInkrementellAsync`
neben `FaltMarkingAsync` (der Voll-Fold bleibt). **Tor (Ebene 1, in-memory):** Cursor-Fold trifft bei JEDER
Weckung **dieselbe** Feuer-Entscheidung (denselben `(Cmd, Vorgang)`) wie der Voll-Fold ab 0 — für lineare,
Join-, Count-Join- UND Fan-out-Formen. Das ist der Kern-Beweis; siehe §6 Teststrategie.

**M2 — Cursor im `ProzessManager` verdrahten.** `FaltMarkingAsync` wählt: Cache vorhanden & `RegelHash` passt →
inkrementell; sonst → Voll-Fold (Fallback). Best-effort-Write nach der Weckung (außerhalb der
Entscheidungs-Transaktion; die Entscheidungen bleiben OCC im Log via `AppendAsync`). **Tor:** ALLE bestehenden
Prozess-Proben grün mit aktivem Cursor (Ebene 1 + die Saga-Integrationstests als Regressions-Orakel).

**M3 — Marten-Store + `RegelHash` + Live-Benchmark.** `RegelHash`-Generator (Struktur-Hash der Regeln),
Marten-`ProzessMarking`-Doc + Write-Hook, Threshold (Konzept §5, Muster: `SnapshotOptions`). **Tor (Ebene 2):**
der M0.5-Fan-out mit großem N (z. B. 1000) läuft mit **O(N) statt O(N²)** Stream-Reads (Read-Zähler beweist es),
Ergebnis identisch zum Voll-Fold.

## 6. Test-Strategie (kritisch — Verteiltes in-memory beweisen)
- **Es gibt heute KEINEN in-memory `ProzessManager`-Saga-Harness** (die Sagas sind nur Integrationstests). M1
  braucht einen: treibe den **echten** `ProzessManager` mit einem `FakeEventStore : IEventStoreRepository`
  (Muster: `Infrastructure.Pruefstand.Tests/Phase4/ProjectionRebuilderTests.cs`) und einem **Fake-`_dispatch`**,
  der emittierte Commands an die **echten** Aggregate über die generierte `AggregateHandlerFactory` +
  `InMemoryEventStore` routet (Muster: `Phase5/KontoAggregatTests.cs`). So laufen ganze Sagas in-memory,
  deterministisch, ohne Cluster.
- **Der Äquivalenz-Test ist die Wahrheit:** dieselbe Saga zweimal durch den Manager — Cursor AUS vs. Cursor AN —
  und Gleichheit der **Feuer-Sequenz + Terminal** assertieren. Für linear/Join/Count-Join/Fan-out je einmal.
- Die **fünf Proben** aus Konzept §6 umsetzen (Äquivalenz, Stale/fehlend→Voll-Fold, „während unten"-Tail,
  Duplikat-verpufft, Fan-out-Read-Zähler).
- **Regressions-Orakel Ebene 2:** die bestehenden Saga-Integrationstests müssen mit aktivem Cursor grün bleiben:
  `BestellSagaE2ETests`, `ReiseSagaE2ETests`, `ReiseSagaParallelE2ETests`, `ProzessManagerE2ETests`,
  `ProzessBackstopE2ETests`. **Immer sequentiell** (`Infrastructure.Integration.Tests/xunit.runner.json`).

## 7. Wiederverwenden (nicht neu erfinden)
- **Cursor-/best-effort-Store-Muster:** `Infrastructure/Persistence/MartenEmittentenCursor.cs` (eigene Session,
  KEIN Co-Commit, Verlust heilt der Voll-Fold) — exakt die Semantik, die der Marking-Cursor braucht.
- **Snapshot-Analogon (Threshold, Schema, Struktur-Hash):** `MartenSnapshotStore` + `SnapshotOptions` +
  `GeneratedSnapshotSchema` — Vorlage für `RegelHash` + `ProzessMarkingThreshold`.
- **Marten-Doc-Registrierung:** `CqrsServiceExtension.cs:128–143` (die `options.Schema.For<…>().Identity(…)`-Liste).
- **In-memory Fakes:** `FakeEventStore` (ProjectionRebuilderTests), `Infrastructure.Testing/InMemory*`.

## 8. Nicht anfassen / Invarianten
- **`LadeStatusAsync`** (Manager-Log-Fold) bleibt — das ist nicht das O(N²).
- **Kein autoritativer Zustand im Feld:** der Cache ist Beschleuniger; der Voll-Fold ab 0 bleibt jederzeit
  gültig und ist der Fallback. „Marking aus dem Log" (Invariante 1) bleibt gewahrt.
- Keine Runtime-Reflection (Inv. 4); `RegelHash` etc. compile-zeit/deterministisch.
- Multi-Node (P7/P8) NICHT anfangen. Deutsch für Kommentare/Domäne.

## 9. Umgebung / Build / Test (wie gehabt)
- Einmalig: `bash scripts/dev-infra-setup.sh` (native Postgres/Redis/Consul + .NET-10-SDK). Bei „Connection
  refused" erneut laufen lassen. Immer `export DOTNET_ROLL_FORWARD=LatestMajor`.
- Ebene 1: `dotnet test Infrastructure.Pruefstand.Tests/Infrastructure.Pruefstand.Tests.csproj` (aktuell 91/91).
- Ebene 2 (sequentiell!): `dotnet test Infrastructure.Integration.Tests/Infrastructure.Integration.Tests.csproj`
  (aktuell 33/33; der `SnapshotLive`-Cold-Boot-Test ist bimodal flaky — **NICHT an Timeouts drehen**).
- **Proto regenerieren** bei neuen Command-/Event-Typen (für M0.5): `dotnet run --project Proto.SourceGeneration`
  → `dotnet build ProtoRepo` → `dotnet build Infrastructure`.
- Vorbestehender `Domain.Client`-Build-Fehler (`_publish`) ist UNABHÄNGIG — ignorieren; das Backend baut grün.
- Konvention: jede Scheibe (M0…M3) beide Ebenen grün → committen → pushen. Auf einer Feature-Branch bleiben.

---

## 10. Prompt (für den neuen Agenten — kopieren)

```
Du arbeitest am Bractor-Backend (selbstgebautes CQRS/ES-Framework auf Proto.Actor/Marten/Redis). Der große
Backend-Neubau P0–P6 und der Feature-Strom sind fertig. Deine Aufgabe ist P5(b): der Prozess-Marking-Cursor
(reine Performance-Optimierung des ProzessManager-Folds, O(N²)→O(N) Stream-Reads).

Lies ZUERST, in dieser Reihenfolge: docs/p5b-marking-cursor-handoff.md (dein Auftrag, mit den echten
Code-Ankern), docs/prozess-marking-cursor-konzept.md (die vollständige Herleitung §0–§8), CLAUDE.md
(Fortschrittsblock oben), und Infrastructure/Prozess/ProzessManager.cs (FaltMarkingAsync/WakeAsync/Kandidat).

Kern in einem Satz: FaltMarkingAsync (ProzessManager.cs:202) liest heute bei JEDER Weckung jeden Ziel-Stream
ab 0 → O(N²). Baue einen nicht-autoritativen Marking-Cache (Cursor + Tail), der pro Weckung nur den Tail
nachfaltet. Sicher, weil der Manager at-least-once feuert und das Ziel über den deterministischen Vorgang
dedupliziert — ein stale Cache feuert höchstens erneut (verpufft), nie ein falscher Effekt; der Voll-Fold ab 0
bleibt der Fallback.

Setup (Umgebung ohne Docker-Hub, .NET 9 EOL): einmal `bash scripts/dev-infra-setup.sh` (installiert .NET-10-SDK
+ native Postgres/Redis/Consul; bei „Connection refused" erneut laufen lassen). Immer
`export DOTNET_ROLL_FORWARD=LatestMajor`. Ebene 1: dotnet test Infrastructure.Pruefstand.Tests/... (aktuell
91/91). Ebene 2 (sequentiell): dotnet test Infrastructure.Integration.Tests/... (aktuell 33/33; der
SnapshotLive-Cold-Boot-Test ist bimodal flaky — NICHT an Timeouts drehen). Den vorbestehenden
Domain.Client-Build-Fehler (_publish) ignorieren; das Backend baut grün.

Arbeite den Fahrplan aus docs/p5b-marking-cursor-handoff.md §5 ab, jede Scheibe einzeln beide Ebenen grün →
committen → pushen:
  M0  — Vertrag ProzessMarking + IProzessMarkingStore (InMemory + Marten, Schema-Reg).
  M0.5— NEU: einen echten breiten Fan-out-Prozess als Nutznießer + Benchmark bauen (SendeJe + UndAlle<E>(n)),
        sonst gibt es nichts zu optimieren/messen (Proto regenerieren für neue Typen).
  M1  — inkrementeller Fixpunkt (MarkingKompakt) NEBEN dem Voll-Fold; Äquivalenz-Beweis in-memory:
        Cursor-Fold == Voll-Fold (dieselbe Feuer-Entscheidung je Weckung) für linear/Join/Count-Join/Fan-out.
  M2  — Cursor im ProzessManager verdrahten (Voll-Fold bleibt Fallback bei fehlendem/stale RegelHash);
        alle bestehenden Prozess-Proben + Saga-Integrationstests grün mit aktivem Cursor.
  M3  — Marten-Store + RegelHash-Generator + Write-Hook; Live-Benchmark: großer Fan-out (N=1000) mit O(N)
        statt O(N²) Reads (Read-Zähler beweist es), Ergebnis identisch zum Voll-Fold.

Die harten Fallen stehen in docs/p5b-marking-cursor-handoff.md §4: die kompakte Darstellung (Zähler+Done-Set,
nicht rohe Tokens), die DREI Achsen pro Vorgang erhalten (ErgebnisDa/WirkungDa/AbgelehntDa — Kandidat,
ProzessManager.cs:198), neue Ziel-Streams mitten im Fold, RegelHash-Invalidierung. Test-Strategie §6:
es gibt heute KEINEN in-memory ProzessManager-Saga-Harness — bau einen (FakeEventStore + Fake-dispatch, der
emittierte Commands über die generierte AggregateHandlerFactory an die echten Aggregate routet; Muster:
Phase4/ProjectionRebuilderTests.cs + Phase5/KontoAggregatTests.cs). Der Äquivalenz-Test (Cursor AUS vs AN,
gleiche Feuer-Sequenz + Terminal) ist die Wahrheit.

Arbeitsweise: reiner Fachcode, keine Runtime-Reflection, alles über Typen/Generatoren. Verteilte Hänge
in-memory beweisen, nie im langsamen Integrationstest raten. LadeStatusAsync (Manager-Log-Fold) NICHT anfassen.
Multi-Node (P7/P8) NICHT anfangen. Deutsch für Kommentare. Bleibe auf einer Feature-Branch und pushe dorthin;
nach Main nur auf ausdrückliche Ansage.
```
