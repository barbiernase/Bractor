# Analyse: Backend ↔ Domänen-Editor — Abdeckung, Praxistauglichkeit, Sync (2026-09-24)

> Ziel: eine **komplette Domänen-Definition im Editor**, Lücken (H-Rümpfe) per LLM füllen,
> **Editor und echter Code bleiben synchron**. Upcasting ist ausgeklammert.
> Grundlage: Code-Stand des Arbeitsbaums (nicht die Doku), Live-Test des Editors im Browser
> (SimHost :5178), `dotnet build GraphExtractor`/`SimHost` grün, Editor-Tests 13/13 grün.

## Update 2026-09-24 (später am Tag) — Extractor-Parität umgesetzt

Phase 0 + der Sync-Kern aus §4 sind gebaut (Details: `konzept-domaenen-editor.md` §10):
- **`--check`-Gate** (`GraphExtractor/ParitaetsPruefung.cs`): Inventar + Fixpunkt → **0 Befunde**
  (116 Typen → 39 Scaffolder-Dateien, 0 Compile-Fehler, M₁ == M₂). Mutationstest bestätigt: der alte
  Kopier-Ctor-Bug erzeugt 18 Befunde.
- **Extractor behoben/erweitert:** Kopier-Ctor ≠ Feld; Expression-Body-Sagas; VOs, Enums, Responses,
  ReadModels, Trigger-Felder, Reaktionen (yield Command) getrennt von Projektionen, Pull/Append-Achse,
  Reader-Responses + TrackDeps, Pipeline-Self-Kanal + ScheduleSelf; echte Apply-Methoden statt abgeleiteter;
  OneOf-Reihenfolge, Parameternamen, Defaults, Initialwerte, Handcode-Zusätze, Summary-Doku, Saga-Lambdas
  verbatim; Framework-Records raus aus dem Editor-Modell. Neue Diagnosen MISSING-APPLY/ENUM-ZERO/STORE-AMBIGUOUS.
- **Editor:** Boot = Merge (Code gewinnt; „✎ ungeschrieben"/„Entwurf" markiert), ↻ und `</> C# schreiben`
  lesen den Code neu ein (`/api/editor/extract`), Rümpfe gehen an Kompilieren/Testen, Ausgabe-Panel sichtbar,
  bewusst leere Rümpfe ≠ „fehlt". Live verifiziert: Kompilieren 0 Fehler (vorher 5), ▶ Testen führt die echte
  Decide-Logik aus (ErstelleDatensatz → DatensatzErstellt, zweimal → DatensatzExistiertBereits).
- **Offen bleibt** (Phase 2–5): nicht-additives Schreiben (Feld-/Signatur-Änderung, Rename, Delete über
  Roslyn), Leseseite/Pipeline/Betrieb schreibbar, LLM-Füllung.

## 0 · Kurzfazit (Stand vor dem Update)

1. **Der Editor zeichnet fast die ganze Architektur, schreibt aber nur einen Bruchteil.** Echt in C#
   landen nur: *neue* Records (Command/Event/Ablehnung/VO/Enum), *neue* Aggregat-/Saga-Dateien und
   *neue* `Decide`/`Apply`-Signaturen als `throw`-Stub (+ die `// 🤖 Prompt:`-Zeile). Leseseite,
   Reaktionen, Pipelines, Trigger, Fristen, Dienste, HostSettings, Queries/Responses sind **reine
   Editor-Artefakte** (`EditorModell.AusJson` verwirft sie still).
2. **Es gibt drei Wahrheiten ohne Merge:** C# (via manuellem `GraphExtractor` → `domain-model.json`),
   `board-model.json` und `localStorage`. Boot-Reihenfolge Board → localStorage → C#: existiert
   `board-model.json`, sieht der Editor keine Code-Änderung mehr. „↻ Vom Graph laden" **ersetzt**
   das Modell und verliert alle Editor-only-Knoten.
3. **Der Round-trip ist heute nicht einmal für den Bestand geschlossen.** Live gemessen: das aus dem
   echten Code extrahierte Modell → „⚙ Kompilieren" → **5 Compilerfehler**, obwohl der Code baut:
   - `CS8910` ×4: Der Extractor liest bei `record X() : IEvent` den synthetisierten Kopierkonstruktor
     als Feld `original: X` (`GraphExtractor/DomainExtractor.cs:587` — filtert
     `IsImplicitlyDeclared` nicht). Betrifft 14 Records.
   - `CS0246`: `prepareSaga` nimmt den Namespace des **Auslöser**-Events nicht in `extraUsings` auf
     (`GraphExtractor/HtmlPresenter.cs:1551`).
   - Zusätzlich: `TeilFertigProzess` (Expression-Lambda) wird mit **0 Regeln** extrahiert
     (`DomainExtractor.cs:359` sucht nur Block-Lambdas); VOs (15) und Enums (6) werden **nicht**
     extrahiert; FanOut/Count-Join fallen im `ModellMapper` weg.
4. **Rückkanal fehlt:** Kompilieren/Testen laufen gegen `throw`-Stubs (Rümpfe werden nie an den
   Server geschickt), und die Ausgabe von Prüfen/Kompilieren/Testen ist **unsichtbar**
   (`#de .col.right{display:none}`, `HtmlPresenter.cs:604`) — der ▶-Test-Dialog ist damit gar nicht
   bedienbar.
5. **LLM-Füllung:** Die Naht ist da (Prompt-Kommentar im echten Rumpf, Anker aus dem Graphen,
   Hash-Sperre, In-Memory-Compile mit echten Generatoren). Es gibt **keinen** LLM-Aufruf und keine
   Verifikationsschleife.

**Bewertung:** Als *Visualisierung/Explorationswerkzeug* stark, als *Autorenwerkzeug* heute nicht
tragfähig — nicht wegen fehlender Knoten, sondern weil der Schreib-/Lese-Kreis nicht geschlossen ist.
Die richtige nächste Investition ist **nicht** mehr Palette, sondern **Parität + Sync-Architektur**.

---

## 1 · Das Backend in einem Bild (was ein Domänen-Entwickler schreibt)

| Bereich | Handgeschrieben | Generiert (nichts zu tun) |
|---|---|---|
| Schreibseite | `partial class X : IState`; `record Cmd(Guid AggregateId,…) : ICommand` (opt. `ICreationCommand`); `record E : IEvent`; `record R : ITransientEvent` (Ablehnung, muss **allein** stehen); nested `Decider : IDecider<T>` mit `IEnumerable<OneOf<…≤5>> Decide(Cmd)`; nested `Applier` mit `Apply(E)` für **jedes** persistente Event; VOs, Enums (1-basiert!) | Id/Version, Handler/Factory, Actors, `GeneratedCommandRouting`, Snapshots+Schema-Hash, Signale, TypeRegistry, Inbox/OCC/Korrelation |
| Leseseite | `IQuery`, `IQueryResponse`, `IReadModel` (mit `Id`), `I{X}ReadStore`/`I{X}WriteStore` (Namensmuster!), Marten-Store auf `MartenCoCommitStoreBase` (3 Primitive), Projektion `ISubscriber, IPullSubscriber [, IAppendProjektion]` mit `Handle(E, IAggregateEnvelope, ProjectionWriter)`, Reader `IReader<TProj>` mit `Handle(Q, env, ReadContext)` → `Task<R>`/`Task<OneOf<…>>` | Dispatch, Pull-Pfad (Tracker vs. `IEmittentenCursor` aus Ctor-Stores), `ProjectionQueryService`, **gesamte DI** + Marten-Schema |
| Reaktion | wie Projektion, ohne Tracker-Store, `IAsyncEnumerable<OneOf<Cmd…/E…>>` (yield Cmd → Emitter, yield Event → Broker) | Emit-Pfad; CQRS020/021 |
| Prozess | `IProzessDefinition` mit `Prozess<T>.Definiere(p => p.Auf<>().Und<>()….Sende<>/SendeJe<>/UndAlle<>/RückgängigDurch[Je]<>)`, opt. `[ProzessName]` | Registry, Manager/Router/Cursor, CQRS001-003/012, Azyklizität |
| Pipeline | `IPipelineHandler` mit `Handle(T, PipelineContext)`; Kanal über T: `IPipelineTrigger` / `IEvent` / `ITransientEvent` / `IPipelineSelfMessage` (`ctx.ScheduleSelf`); yield `ICommand` / `IPipelineTrigger` | Actors, Registrierung, Event-Pulls |
| Betrieb | `TimerTrigger.Registrierung`, `MapPipelineWebhook<T>`, `IFristplan.PlaneAsync/EntferneAsync` + `AddDeadlines(f => Cmd)`, Dienst-DI (`AddDomainPipelineServices`), Settings, `CqrsFrameworkBuilder`-Optionen | — |
| Querschnitt | Proto/STJ via `./codegen.sh` (CQRS030 sonst); Test-DSL `Szenario`/`SagaSzenario` | — |

Details + Zeilenbelege: siehe Agenten-Inventur (in dieser Session), Kernquellen `Abstractions/Interfaces.cs`,
`Abstractions/Prozess/ProzessBuilder.cs`, `*.SourceGeneration/*`.

---

## 2 · Abdeckungs-Matrix Backend → Editor

Legende: **✅** voll · **◐** teilweise · **✗** fehlt · **E** = nur im Editor (geht nicht nach C#) ·
**A** = nur additiv (neue Namen) · **S** = als `throw`-/`default`-Stub

| Konstrukt | Editor zeigt | Editor legt an | → C# | C# → Editor (Extract) |
|---|---|---|---|---|
| Command (+`ICreationCommand`) | ✅ | ✅ | A (Feldänderung an Bestand: **verloren**) | ✅ |
| Event / Ablehnung | ✅ | ✅ | A | ✅ (parameterlose Records: **Bug `original`**) |
| Value Object | ✅ | ✅ | A | **✗** |
| Enum | ✅ | ✅ | A | **✗** |
| Aggregat + State-Felder (+ berechnete) | ✅ | ✅ | nur neue Datei | ✅ |
| Decider-Signatur (OneOf-Ausgänge) | ✅ | ✅ | A+S (Signaturänderung an Bestand: **verloren**) | ✅ (aus Routing) |
| Guard (`if … yield Ablehnung`) | ✗ (reist passiv mit) | ✗ | ✗ | ◐ (nur innerstes `if`) |
| Applier | ✅ | ✅ | A+S | ◐ (**abgeleitet**, nicht gelesen) |
| Decide-/Apply-Rumpf | ◐ (Vorschau per Poll) | ✗ (nur im IDE) | ✗ | ✅ |
| Prozess: Auf/Und≤3/Sende | ✅ | ✅ | nur neue Datei; Args = `default` | ◐ (Expression-Lambda → 0 Regeln) |
| Prozess: SendeJe (×N) | ◐ | ◐ | **kompiliert nicht** (`/* Collection */`) | ✗ (Mapper verliert FanOut) |
| Prozess: UndAlle (Count-Join) | ✗ (bewusst entfernt) | ✗ | tot | ✗ |
| Prozess: RückgängigDurch[Je] | ✅ | ✅ | nur neue Datei | ✅ |
| `[AggregatName]` / `[ProzessName]` | ✗ | ✗ | ✗ | ✗ |
| Query | ✅ | ✅ | **E** | ✅ |
| Response (`IQueryResponse`) | ✅ | ✅ | **E** | ✗ |
| ReadModel (`IReadModel`, Id) | ✅ | ✅ | **E** | ✗ |
| Store-Interfaces + Fns | ✅ | ✅ | **E** | ✅ (Regex) |
| Store-Impl (Co-Commit-Primitive) | ◐ | ◐ | **E** | ◐ |
| Projektion (Handles, Store-Aufrufe) | ✅ | ✅ | **E** | ✅ |
| Achse Replaybar/Emittierend, `IAppendProjektion` | ◐ (dekorativ) | ◐ | **E** | ✗ (`pull` fest, `append` fehlt) |
| Reader (`IReader<T>`, OneOf-Responses, `ctx.Track`) | ✅ | ✅ | **E** | ◐ (ohne Responses) |
| Reaktion (yield Cmd) | ✅ | ✅ | **E** | ✗ (wird als Projektion erkannt) |
| Reaktives Event (yield Event → Broker) | ✅ | ✅ | **E** | ✗ |
| Pipeline (Trigger/Event/Transient/Self-Kanal) | ✅ | ✅ | **E** | ◐ (Self als Event, `schedules` leer) |
| Trigger timer / webhook / filewatch | ✅ | ✅ | **E** | ◐ (nur Webhook) |
| Frist (plant/storniert/Dauer/fällig→Cmd) | ✅ | ✅ | **E** | ◐ (Heuristik `Contains("Plane")`) |
| Dienst (Vertrag→Impl) | ✅ | ✅ | **E** | ◐ |
| HostSetting / Framework-Optionen | ◐ | ◐ | **E** | ◐ |
| Proto/codegen nach Typänderung | — | — | ✗ (kein Build nach „C# schreiben") | — |
| Test-Szenarien (`Szenario`) | ◐ (Board „Als Test") | ◐ | ◐ (DSL-Text) | ✗ |
| Blazor-Client (Module/Store/Handler/VM/.razor) | ✗ | ✗ | ✗ | ✗ |

**Lesart:** Die Schreibseite ist *strukturell* abbildbar, aber nur *additiv* schreibbar. Die gesamte
rechte Hälfte der Architektur (Lesen, Reagieren, Betrieb) ist im Editor ein **Entwurf, der nie Code
wird** — und beim nächsten „↻ Vom Graph laden" verschwindet.

---

## 3 · Praxistauglichkeit (Live-Befund)

- **Dichte:** Der Bestand (6 Aggregate) ergibt ~390 Knoten, davon **173 Code-Knoten** (je Rumpf einer),
  31 Decider + 33 Applier + 31 Commands. Das Gesamtbild ist ohne Fokus nicht lesbar; Arbeiten geht
  nur über Domänen-Filter/Sprung. → Braucht **Fokus-Ansichten** (ein Aggregat/eine Slice) und
  **eingeklappte Rümpfe** (Code-Knoten als Badge am Decider statt eigener Knoten).
- **Feedback unsichtbar:** Prüfen/Kompilieren/Testen schreiben in ein ausgeblendetes Panel. Nutzer
  sieht nach „⚙ Kompilieren" *nichts* — die 5 Fehler fand ich nur über die Netzwerk-Antwort.
- **Irreführende Erfolgsmeldung:** „✓ Dateien aktuell" nach „C# schreiben", obwohl Feld-/Signatur-
  änderungen still verworfen wurden.
- **Code-Knoten nicht editierbar** (bewusst „IDE ist Wahrheit") — konsequent, aber dann ist ein
  eigener Knoten pro Rumpf zu teuer; ein Link „✎ im IDE" am Decider reicht.
- **Gefährliche Defaults:** automatisch abgeleitete Applier erzeugen `throw`-`Apply` in echten
  Dateien; ×N erzeugt nicht kompilierbaren Code; leere Absichts-No-op-Apply wird als „fehlt" markiert.
- **Framework-Records** (`KommandoAbgelehnt`, `ProzessGestartet`, …) erscheinen als editierbare Knoten.

---

## 4 · Sync-Architektur — Vorschlag (das Wichtigste)

### 4.1 Eine Wahrheit: der C#-Code

- **Das Editor-Modell ist immer eine Projektion des Codes**, nie eine eigene Wahrheit.
  `board-model.json` hält **nur** Layout + echte Editor-Metadaten (Positionen, eingeklappt, Notizen),
  **geschlüsselt über stabile Identität** (`Namespace.Name` bzw. Symbol-ID) — keine Domänenstruktur.
- **Entwürfe** (Knoten, die noch nicht geschrieben sind) sind explizit als *Entwurf* markiert
  (gestrichelt) und das Einzige, was nur im Board lebt. Nach „Schreiben" verschwindet die Markierung,
  weil der Knoten jetzt aus dem Code kommt.
- **Extractor in-process im SimHost** (Roslyn `MSBuildWorkspace` bzw. inkrementell über
  `AdhocWorkspace` + Dateisystem-Watcher): jede Dateiänderung (IDE oder Editor) → Re-Extract →
  Push an den Browser (SSE/WebSocket statt 2-s-Poll). Dann ist „↻ Vom Graph laden" überflüssig.

### 4.2 Schreiben = Roslyn-Chirurgie, nicht Anhängen

Aus „additiv" wird „**deklarativ abgleichen, Rümpfe schützen**": Für jedes Konstrukt ein
*Writer*, der die Struktur (Signatur, Felder, Interfaces, Attribute) auf den Soll-Stand bringt und
**Methodenrümpfe nie anfasst**:
- Felder hinzufügen/entfernen/umtypen/umbenennen an Records (Parameterliste),
- OneOf-Ausgänge einer bestehenden `Decide` ändern,
- Umbenennen über `Microsoft.CodeAnalysis.Rename.Renamer` (zieht alle Referenzen inkl. Handcode),
- Löschen nur mit Bestätigung + Referenz-Check (verweist Handcode darauf → Löschen blockieren).
- Nach jedem Schreiben: `dotnet build` (Prepass Proto/STJ läuft mit) → Diagnosen zurück an den Knoten.

### 4.3 Paritäts-Invariante + Round-trip-Test (Gate)

> **Was der Editor editierbar anbietet, muss (a) extrahierbar und (b) schreibbar sein.**
> Alles andere wird **read-only** angezeigt (grau, „noch nicht schreibbar"), nicht editierbar-und-verloren.

Getestet als **Fixpunkt** in `Infrastructure.Pruefstand.Tests` (GraphExtractor/SimHost referenzieren):
1. `extract(Domain)` → Modell M₁
2. `scaffold/write(M₁)` in eine **temporäre Kopie** → `extract` → M₂ → **M₁ == M₂**
3. M₁ in-memory kompilieren → **0 Fehler** (heute: 5)
4. Mutationsfälle: Feld +/−, Rename, OneOf-Ausgang +, neues Event → Soll-Diff im Code exakt.

Damit ist „Code und Editor synchron" nicht mehr Disziplin, sondern CI-Gate — analog zu `codegen.sh --check`.

---

## 5 · LLM-Füllung — wie es hier sauber passt

D/S/H steht schon im Konzept; es fehlt die Maschine. Vorschlag:

1. **Vertrag automatisch aus dem Slot** (existiert als Idee): Signatur, State-Typ, erlaubte Outcome-
   Typen (OneOf), Felder von Command/Event, verwandte Ablehnungen, Namespace-Nachbarn.
2. **Spezifikation = Szenarien, nicht Prosa.** Der Editor erzeugt aus dem Board Gegeben/Wenn/Dann-
   Beispiele (`Cqrs.Testing.Szenario`, gibt es) — der Prompt ist Absicht + Szenarien.
3. **Füllen → verifizieren → reparieren:** LLM schreibt den Rumpf **in die echte Datei**
   (Ort ist über den Code-Anker bekannt) → In-Memory-Compile (`/api/editor/compile`, gibt es) →
   Szenarien laufen (store-freier Kern) → Fehler zurück ins LLM (max. N Runden) → Diff zur Freigabe.
4. **Reihenfolge der H-Slots** nach Nutzen: Decide-Rümpfe (Guards/Ablehnungen) → Apply → Projektions-
   Handle + Store-Impl (3 Co-Commit-Primitive = kleines Vokabular) → Reader → Pipeline-Rümpfe →
   Saga-Argument-Lambdas (fast immer D: Feld-Mapping `e => new Cmd(e.X, …)` — erst deterministisch
   versuchen, LLM nur bei Mehrdeutigkeit).
5. Prompt bleibt als `// 🤖 Prompt:` im Code (gebaut) — damit ist auch die Absicht versioniert.

Voraussetzung: 4.1–4.3. Ohne geschlossenen Kreis schreibt ein LLM in Stubs, die der nächste
„Vom Graph laden"/Autosave wieder überdeckt.

---

## 6 · Roadmap (priorisiert)

**Phase 0 — Bestand runder machen (klein, sofort):**
1. Extractor: implizite Konstruktoren ignorieren (`!c.IsImplicitlyDeclared`) → CS8910 weg.
2. `prepareSaga`: Auslöser-Namespace in `extraUsings`.
3. Prozess-Extraktion auch für Expression-Lambdas; FanOut/Count im Mapper übernehmen.
4. VOs + Enums extrahieren (Records ohne Marker in Aggregat-Namespaces; `enum`-Symbole).
5. Ausgabe-Panel sichtbar machen (Toast/Drawer für Prüfen/Kompilieren/Testen).
6. Rümpfe beim Compile/Run mitschicken (aus Datei lesen, serverseitig — nicht aus dem Browser).
7. Framework-Namespaces aus der Palette filtern; No-op-Apply ≠ „fehlt".
8. Konzept-Doc §2c/§9/§10/§6 an den Ist-Zustand anpassen.

**Phase 1 — Sync-Kern:** Extractor in SimHost, Watcher + Push, `board-model.json` nur Layout/Entwurf
über stabile IDs, Round-trip-Fixpunkt-Test als Gate.

**Phase 2 — Schreibseite voll schreibbar (Roslyn-Writer):** Felder, OneOf, Rename, Delete, Guards
bleiben Rumpf; Build + Diagnosen zurück an den Knoten.

**Phase 3 — Leseseite schreibbar:** Query/Response/ReadModel-Records, Store-Interface-Paar (Namens-
muster), Store-Impl-Gerüst auf `MartenCoCommitStoreBase`, Projektion/Reader/Reaktion-Klassen mit
Handle-Signaturen (Rümpfe = H-Slots). Extractor: Reaktion ≠ Projektion (Rückgabetyp), Responses,
ReadModels, `IAppendProjektion`.

**Phase 4 — Prozess/Pipeline/Betrieb schreibbar:** Saga-Regeln chirurgisch (Fluent-Kette ist
strukturiert → gut schreibbar), Pipeline-Klassen, Composition-Root-Fragment als **generierte**
`AddDomainHost(...)`-Extension (Trigger/Frist-Mapping/Dienste/Settings) statt Program.cs-Patching.

**Phase 5 — LLM-Füllung** (§5) auf dem geschlossenen Kreis; zuerst Decide-Rümpfe mit Szenario-Spec.

**Phase 6 — UX:** Fokus-Ansicht je Aggregat/Slice, Code als Badge statt Knoten, Diff-Vorschau vor
jedem Schreiben.

**Nicht-Ziel (vorerst):** Blazor-Client im Editor (0 % abgedeckt; eigene Baustelle, s. Memory
„Front-End nicht im Editor").
