# Aufräum-Plan — verifizierte Lösch-/Konsolidierungsliste

> Stand der Analyse: Erreichbarkeit ab **Host.Grpc** + **Host.Blazor**, plus Referenz-/Konsumenten-Verifikation
> jedes Kandidaten. **Noch nichts gelöscht.** Jeder Punkt ist einzeln belegt.
>
> ⚠ Ausgespart: `Infrastructure/Aggregate/AggregateActorBase.cs` + `AggregateRehydrator.cs` — der Snapshot-Agent
> arbeitet dort aktiv. Kein Punkt dieser Liste liegt in diesen Dateien.

---

## A) Verifiziert tot — Code (0 lebende Referenzen)

Ganze Dateien löschbar:

- [ ] `Core/Session.cs` — `Session<TAggregate>` (altes Session-Muster); `Session<` nirgends instanziiert
- [ ] `Infrastructure/InMemoryRepository.cs` — `InMemoryAggregateRepository`; referenziert nur sich + Messenger
- [ ] `Infrastructure/InMemoryAggregateMessenger.cs` — referenziert nur sich
- [ ] `Domain.Projections/ImagePairStoreInMemory.cs` (320 Z.) — 0 externe Refs; DI nutzt `ImagePairStorePostgres` (Read) + `ImagePairStore` (Write)
- [ ] `Core/ReadContext.cs` — komplett auskommentiert (lebt in `Abstractions/Interfaces.cs`)
- [ ] `Core/PipelineContext.cs` — komplett auskommentiert (lebt in `Abstractions/PipelineContext.cs`)
- [x] `docs/IEventEnvelope.cs` — ✅ gelöscht (Doku-Aufräumung)
- [x] `docs/IProjectionTracker.cs` — ✅ gelöscht
- [x] `docs/IProjectionRebuild.cs` — ✅ gelöscht
- [x] `docs/MartenProjectionTracker.cs` — ✅ gelöscht
- [x] `docs/InMemoryProjectionTracker.cs` — ✅ gelöscht (übrige docs/*.md-Historie → docs/archiv/)
- [ ] `Proto.SourceGeneration/Model.cs` — 0-Byte-Rest
- [ ] `Proto.SourceGeneration/ModelProto.cs` — 0-Byte-Rest
- [ ] `Proto.SourceGeneration/Aggregator.cs` — tot, bereits per `<Compile Remove>` (csproj:18) aus dem Build
- [ ] `Proto.SourceGeneration/Analyzer.cs` — tot, `<Compile Remove>` (csproj:19)
- [ ] `Proto.SourceGeneration/TypeAggregator.cs` — tot, `<Compile Remove>` (csproj:20)
- [ ] `Proto.SourceGeneration/DomainGraphAnalyzer.cs` — tot, `<Compile Remove>` (csproj:21)
- [ ] `Proto.SourceGeneration/CompilationTypeResolver.cs` — tot, `<Compile Remove>` (csproj:22)

Nur **Teile** lebender Dateien (Datei behalten, Symbol entfernen):

- [ ] `Abstractions/Prozess/ProzessRegeln.cs:14` — nur `interface IKorreliert` (nie implementiert). **Datei behalten** — enthält die lebende DSL `Regel`/`ProzessRegeln`
- [ ] `Abstractions/ProzessId.cs:29,36` — nur `FürSchritt` + `FürRückabwicklung` (nur in Kommentaren referenziert). Live: `Für`/`FürTransition`/`FürKompensation`

---

## B) Verwaiste Gesamt-Projekte — untracked (`git ls-files` = 0, kein git-Verlust)

- [ ] `ReactiveState/` — nicht in sln, von keinem csproj referenziert, nie committet (Generator-Dateien 0-Byte)
- [ ] `ReactiveState.Application/` — nur Demo-Konsole für ReactiveState
- [ ] `ReactiveState.SourceGeneration/` — alle Quelldateien 0-Byte
- [ ] `ReactiveState.Tests/` — Tests für ReactiveState
- [ ] `Infrastructure.PubSub/` — leer (0 cs); aktiver Broker liegt in `Infrastructure/PubSub/`
- [ ] `Infrastructure.PubSub.Tests/` — Tests für den toten PubSub-Rest
- [ ] `Client.Avalonia/` — 5-Zeilen-Stub, leerer `Program`
- [ ] `Client.Infrastructure.Tests/` — 5-Zeilen-Stub, leerer `Program`

> Optional vorher sichern: `git stash -u` oder Branch, falls Ideen aus ReactiveState (Compute-Graph) später gewünscht.

---

## Client) Alter Client-Stack — Entfernung NACH deiner Migrations-Bestätigung

**Aktiver Stack (behalten):** `Host.Blazor` → `Client.Infrastructure` + `Client.SourceGeneration` + `Domain.Client.Modules.Blazor` (alle in sln).
`Domain.Client.Modules.Blazor` ist self-contained (referenziert `Domain.Client`/`Ui.Blazor` nicht).

**Alter Stack (git-getrackt, NICHT in sln, nur untereinander + über tote Host.Blazor-Fäden verbunden):**

- [ ] `Domain.Client/` (20 Dateien, ns `Domain.Client.ImagePair`) — Konsumenten: nur sich + `Domain.Client.Ui.Blazor`
- [ ] `Domain.Client.Ui.Blazor/` (11 Dateien) — Konsumenten: **niemand** außer toter Host.Blazor-csproj-Ref

Zwei tote Fäden in Host.Blazor kappen (Voraussetzung fürs saubere Entfernen):

- [ ] `Host.Blazor/Program.cs:6` — `using Domain.Client.ImagePair;` löschen (tot: kein Typ daraus genutzt; `Add…()` liegen in `Microsoft.Extensions.DependencyInjection`)
- [ ] `Host.Blazor/Host.Blazor.csproj` — `<ProjectReference … Domain.Client.Ui.Blazor>` entfernen (Host.Blazor rendert nichts daraus)

> ⚠ **Unterschied zu B:** Diese zwei Projekte sind **git-getrackt und haben uncommittete Änderungen**
> (`MainStage.razor`, `Labeling.razor`, `_Imports.razor`, `FileHandler.cs`, `ImagePairWorkspace.cs`,
> `NavigationHandler.cs` + neue untracked `Sections/`, `FooterBar.*`, `GlobalEffects.*`, `UserIntent*.cs`).
> Löschen verliert diese Stände.
> **→ ENTSCHEIDUNG NÖTIG: Ist die Migration nach `Domain.Client.Modules.Blazor` inhaltlich vollständig?**
> Erst „ja" → auf die Löschliste.

---

## C) Nicht tot — deine Entscheidung nötig

- [ ] **Gruppe-B-InMemory** = Substrat des schnellen In-Memory-Prüfstands. Für das Starten der Hosts **irrelevant** (Runtime fasst sie nie an), aber 16 Prüfstand-Testdateien hängen daran (inkl. `Phase6/SnapshotRehydrationTests`, `CrashProbeTests`). Löschen = schneller Prüfstand weg, nur noch langsame Integrationstests.
  - `Infrastructure/InMemoryEventStore.cs`
  - `Infrastructure/Testing/InMemoryProjectionTracker.cs`
  - `Infrastructure/Testing/InMemoryPollCursorStore.cs`
  - `Domain.Infrastructure/ImagePairHistoryInMemory.cs` (`ImagePairHistorieStoreInMemory`)
- [ ] `ProjectScanner/` — Dev-Tool, in sln, von niemandem referenziert, von dir nicht als Tool genannt. Behalten oder löschen?

---

## D) ⚠ Schutznetz — sieht tot aus, ist aber LEBENDIG (NICHT löschen)

- `Core/AggregateHandlerBase.cs` — **Basisklasse jedes generierten Aggregat-Handlers** (`AggregateHandlerGenerator.cs:78` emittiert `: AggregateHandlerBase<…>`). Wurde in der Analyse fälschlich als ungenutzt gemeldet.
- Gruppe-B-InMemory (siehe C) — Test-Substrat, kein toter Code.
- `Proto.SourceGeneration` (Tool: Proto-Gen), `GraphExtractor` (Tool: Wissensgraph), `ProtoRepo` (Build-Abhängigkeit von Infrastructure) — behalten.

---

## E) Keine Löschung — sln-/Build-Hygiene

- [ ] `Domain/Domain.csproj` — doppelte `<ProjectReference … Domain.SourceGeneration>` (einmal entfernen)

> Hinweis: Der frühere Vorschlag „Domain.Client + Ui.Blazor in die sln aufnehmen" ist **gestrichen** —
> sie sind alter Stack (siehe Abschnitt Client). Die sln bildet den aktiven Zustand bereits korrekt ab.

---

## Empfohlene Reihenfolge

1. **A + B** — verifiziert gefahrlos, sofort machbar (nach Abschluss des Snapshot-Agenten wegen Nachbar-Dateien in `Infrastructure/`).
2. **E** — 1 Zeile.
3. **Client** — nach deiner Migrations-Bestätigung.
4. **C** — bewusste Entscheidung (Prüfstand-Substrat / ProjectScanner).
