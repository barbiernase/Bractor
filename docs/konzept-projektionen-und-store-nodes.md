# Konzept — Projektionen & Store-Nodes im Domänen-Editor

> **Stand: 2026-08-24. Status: KONZEPT — nicht implementiert.** Erweiterung des visuellen
> Domänen-Editors (ComfyUI-Node-Modell, `GraphExtractor/HtmlPresenter.cs`, SimHost `/editor`) um
> die **Leseseite**: Read Models, Projektionen, Stores, Queries, Reader. Leitprinzip:
> **verdrahten → Interface/Struktur wird generiert → nur die echte Logik bleibt frei.** Und —
> zentrale Vorgabe: **so wenig LLM wie möglich; der LLM ist die Ausnahme an den Logik-Stellen,
> nicht der Default.** Das Meiste entsteht deterministisch oder aus einem strukturierten Vokabular.
>
> Verwandt: [04-konsum-und-prozess-maschine.md](04-konsum-und-prozess-maschine.md) (die vier
> Konsumenten, eine Maschine), [konzept-exactly-once-naht.md](konzept-exactly-once-naht.md)
> (Co-Commit), [sim-host-live-runtime](../) (die bestehende Live-Runtime).

## 1 · Ziel & Kernidee

Der Editor modelliert heute die **Schreibseite** (Command/Event/VO/Aggregat/Decider/Applier/Saga)
und erzeugt daraus deterministisch die handgeschriebenen C#-Dateien; die Roslyn-Generatoren füllen
den Rest; `ModellRuntime` fährt das Modell live über `SagaLaufwerk` (store-frei). Die **Leseseite**
(Projektionen) fehlt komplett.

Kernidee: Eine Projektion ist ein zweiter durabler Konsument, gespeist von denselben Events. Ihre
Anatomie ist ein **festes 8-Datei-Muster** (belegt an `Modell`/`Datensatz`/`ImagePair`):

```
{X}ReadModel.cs      : IReadModel                      ← das/die Dokument(e)
{X}Projektion.cs     : ISubscriber[, IPullSubscriber][, IAppendProjektion]
I{X}Store.cs         : I{X}WriteStore + I{X}ReadStore  ← die zwei Interfaces
{X}Queries.cs        : IQuery
{X}Responses.cs      : IQueryResponse
{X}Reader.cs         : IReader<{X}Projektion>
{X}Store.cs          : {X}Store : CoCommitBase (Marten Write)     ← Infrastructure
{X}StorePostgres.cs  : {X}StoreInMemory (Sim)                     ← Infrastructure
```

## 2 · Das Paradigma und das D/S/H-Prinzip (das Rückgrat)

> **Ein Interface ist nicht gezeichnet — es ist der Querschnitt der Verdrahtung.** Jeder Draht, der
> die Grenze *Projektion→Store* kreuzt, wird eine Write-Store-Methode; jeder Draht *Reader→Store*
> wird eine Read-Store-Methode. Signaturen = abgeleitet aus den Port-Typen.

Jedes Artefakt/jeder Rumpf gehört zu genau einer Klasse — Ziel: **D + S maximieren, H minimieren**:

- **D — Deterministisch abgeleitet.** Aus Verdrahtung + Typen, kein Tippen, **kein LLM**.
  (Records, Interfaces, der Projektion-Handle, Feld-Mappings, DI.)
- **S — Strukturiert.** Aus einem **typisierten Vokabular** gewählt (Dropdown + Pins), kein
  Freicode, **kein LLM**. (Effekt-Operationen, Filter-/Sort-/Page-Builder, Response-Mapping.)
- **H — Freie Logik.** Hand getippt **oder** LLM-gestützt — die **EINZIGE** Stelle, an der der LLM
  überhaupt auftaucht. Nur der Rest, der nicht ins Vokabular passt (ein neuartiger Transform, eine
  ungewöhnliche Aggregation). Immer von der Sim verifiziert.

Der Projektions-Handle selbst ist **reine Weiterleitung** (belegt: kein Handle im Codebase liest/
verzweigt) → **D**. Die gesamte Logik sitzt im **Store-Effekt** — und der ist zum Großteil **S**.

## 3 · Was wir schon haben

- Node-Editor-Infrastruktur (getippte Slots, Bézier-Kanten, Pan/Zoom, abgeleitete Zugehörigkeit,
  Typ-Kompatibilität, Einklappen, Picker) — `HtmlPresenter` `EditorBlock`.
- Schreibseite: `DomainEditor` (EditorModell + Scaffolder + Validator).
- SimHost: `/scaffold` `/validate` `/compile` `/run` + `ModellRuntime` (echte Generatoren in-memory
  + `SagaLaufwerk`) + Compile/Run-Self-Repair-Schleife.
- **Die Lese-Generatoren existieren bereits:** `SubscriberDispatchGenerator`,
  `ProjectionReaderDispatchGenerator`, `ProjectionQueryServiceGenerator`,
  `ProjectionServicesGenerator` (DI). ⇒ Dispatch + DI entstehen **geschenkt**, sobald die
  Handdateien da sind.
- In-Memory-Store-Präzedens (`ImagePairHistorieStoreInMemory`, Dict).
- Event-Trace aus `SagaLaufwerk` (▶ Testen liefert die emittierten Events schon).

## 4 · Die Node-Palette (Ports + was generiert wird)

| Node | Ports | Generiert | Klasse |
|---|---|---|---|
| **▤ ReadModel** | rechts OUT `→ Store` | `record {X}ReadModel : IReadModel` + Marten-Schema | **D** |
| **🗄 Store** | oben IN: ReadModels · links: **Effekt-Ops** (Inlets, Projektion andocken) · rechts: **Query-Ops** (Outlets, Reader andocken) | `I{X}WriteStore` + `I{X}ReadStore` (Querschnitt der Ports) + Marten-Impl + InMemory-Impl | Interfaces **D**, Rümpfe **S**/H |
| **⚡ Projektion** | links IN: Events (je Event = ein Handle) · rechts OUT `→ Store` · Körper: SubscriberId, Achse-B, Transport, Append | `{X}Projektion` + `Handle(evt, env, writer)` je Event (reine Weiterleitung) + SubscriberDispatch | **D** |
| **❓ Query / Response** | Query.out → Reader.in; Reader.out → Response | Records + Proto/Registry | **D** |
| **🔍 Reader** | links IN: Queries · Ports zu Store-Query-Ops + Response · Körper: `[ProjectionReader(TrackDeps)]` | `{X}Reader : IReader<{X}Projektion>` + ReaderDispatch + QueryService | **D**/S |

**Achsen (die eine echte Entwurfsentscheidung), als Dropdowns am Projektion-Hub:**
- **Achse B (Garantie):** replaybar (`IProjectionTracker`, co-commit + Reset) vs. emittierend
  (`IEmittentenCursor`, kein Reset). Beide gesetzt → Validator-Fehler (spiegelt den Ctor-Guard).
- **Transport:** `IPullSubscriber` (geordneter Pull) vs. `ISubscriber` (Signal).
- **Append (`IAppendProjektion`)** als Häkchen. Validator-Regel = GA-1: Append **ohne** Co-Commit-
  Store → Fehler (spiegelt den Boot-Check).

## 5 · Das Effekt-Vokabular (der Schlüssel zur LLM-Minimierung)

Ein Store-Effekt läuft über eines von drei Co-Commit-Primitiven (`MartenCoCommitStoreBase`):
`EnqueueStore` (Upsert) · `EnqueueTransform<T>` (Load→ändern→Store) · `Enqueue(session=>…)` (roh).
Die **meisten** realen Effekte sind Kompositionen eines kleinen, typisierten Vokabulars — **S, kein
LLM**. Jede Operation ist Dropdown + Pins (Feld ← Event-Arg):

| Effekt-Op (Vokabular) | erzeugt (Beispiel) | Primitiv | Klasse |
|---|---|---|---|
| **Upsert** (Doc aus Event-Feldern) | `EnqueueStore(new {X}{ Id=…, Feld=evt.Feld })` | Store | **D** (Feld-Pins) |
| **Feld setzen** | `existing with { Feld = arg }` | Transform | **S** |
| **In Liste anhängen (dedup)** | Union + `!Contains` | Transform | **S** |
| **Aus Liste entfernen** | `list.Remove`/`Where(≠)` | Transform | **S** |
| **Zähler = Liste.Count** (neu rechnen) | `Anzahl = list.Count` | Transform | **S** |
| **Singleton setzen** (last-writer-wins) | `EnqueueStore(new Singleton{…})` | Store | **S** |
| **Fan-out** (je Element ein abgeleitetes Doc) | `foreach(x in evt.Coll) s.Store(new {Y}{ Id=MakeId(…) })` | Roh | **S** |
| **Sekundär-Index pflegen** (key→ids, +/−, idempotent) | `Load(key) ?? new; add/remove id; Store` | Roh | **S** |
| **Freie Logik** | beliebiges C# | Transform/Roh | **H** (Hand **oder** LLM) |

Beobachtung an `DatensatzStore`: `NimmRangeAuf` = *anhängen-dedup + aus-Ausgeschlossen-entfernen +
neu-zählen* → **drei** Vokabel-Ops, **kein LLM**. `FriereEin` = *Feld setzen + Fan-out* → **S**.
`BufferRueckwaerts` = *Sekundär-Index* → **S**. Erst ein Effekt außerhalb des Vokabulars ist **H**.

**Idempotenz** ist Vokabel-Eigenschaft: „anhängen-dedup", „Zähler=Count", „MakeId"-Fan-out sind
per Konstruktion idempotent → die Sim prüft das (dasselbe Event zweimal → gleiches Dokument).

## 6 · Query-Node: zwei Zonen, strukturiert (kein LLM für den Normalfall)

Jede Read-Op (Query-Node) hat zwei Zonen — beide **S/D**, LLM nur als Ausnahme:

- **Zone 1 — Filter/Sort/Page** über `IQueryable<T>` (Marten-übersetzbar). Strukturiert: je Query-
  Parameter ein Prädikat-Pin (Param → ReadModel-Feld + Operator `== < > >= <= contains`, optional
  `if HasValue`), plus Sort-Feld+Richtung und Skip/Take aus Parametern. Belegt an `SearchAsync`:
  komplett aus solchen Zeilen zusammengesetzt → **S, kein LLM**.
- **Zone 2 — Shape** (nach `.ToList()`): Response-Konstruktion = Feld-Mapping Doc→Response (**D**,
  Pins). Aggregation/Gruppierung (`GetStatistik`, `GetVerlauf`) = Vokabel „group-by Feld, count je
  Kategorie" (**S**) oder, wenn ungewöhnlich, **H**.

**Marten-Whitelist** (Zone 1 darf nur diese an `session.Query<T>()` schicken): `Where`
(Vergleiche, `&&`/`||`/`!`, Enum, nullable `.HasValue/.Value`, verschachtelte Pfade), `OrderBy(Desc)`,
`Skip`, `Take`, `Count`, `Any`, `Contains` (IN), `Load`/`LoadMany`. Alles Reichere gehört in Zone 2
(nach Materialisierung, freies C# — auf Dict und Marten identisch). Sim-grün ⇒ Marten-nah **per
Konstruktion**; das Marten-Gate (siehe §8) fängt die Restkanten.

## 7 · „Ein Rumpf, zwei Backends" — die zwei schmalen Abstraktionen

Damit ein Rumpf **Sim (Dict)** und **Produktion (Marten)** bedient:

- **Write:** Effekt-Rümpfe laufen gegen eine **minimale** `ICoCommitSession` (`LoadAsync<T>(id)`,
  `Store(doc)`, `StoreMany(docs)`), nicht die volle `IDocumentSession`. Produktion = Marten-Session-
  Adapter; Sim = Dict (`Dictionary<Type, Dictionary<object,object>>`). Reines Key-Value-Load/Store →
  **kein Übersetzungsproblem** (anders als LINQ). Refactor: `MartenCoCommitStoreBase.Enqueue` auf
  `ICoCommitSession` umstellen (+ die 5 bestehenden Stores).
- **Read:** Query-Rümpfe als `Func<IQueryable<TReadModel>, TResult>` mit synchronem LINQ →
  `dict.Values.AsQueryable()` (Sim) | `session.Query<T>()` (Marten). Einzige Naht: terminale
  Operation (`ToList` vs `ToListAsync`) im dünnen Backend-Wrapper.

## 8 · Live-Runtime & Verifikation

- **Sim (in-memory, keine Infra) — der schnelle Loop:** `ModellRuntime` erweitern um (a) die
  Dict-`ICoCommitSession`, (b) den `SagaLaufwerk`-Event-Trace durch `Projektion.DispatchAsync`
  schicken → Dokumente materialisieren, (c) Reader-Query über `dict.AsQueryable()` → Antwort.
  **▶ Testen** bekommt einen **ReadModel-Dokument-Inspektor** + eine **Query-Antwort-Fläche**.
  „Zweimal einspeisen" prüft Idempotenz.
- **Marten-Gate (echte Infra, jetzt via Docker) — der finale Riegel:** ein generierter Round-trip-
  Test fährt Effekte + Query-Ops gegen echtes Marten/Postgres → fängt Übersetzungs-/Semantik-
  Kanten, die die Sim nicht sieht. Gleiche Naht wie Prüfstand (store-frei) ↔ Integration (Marten).

## 9 · End-to-End-Durchlauf (Beispiel `Datensatz`)

1. **Zeichnen:** ReadModel `DatensatzReadModel` → Store; Event `PaareAufgenommen` → Projektion-Hub
   (co-commit, Pull, Append=✓) → Store; Draht erzeugt Effekt-Op `NimmRangeAuf`; Query `HoleDatensaetze`
   → Reader → Query-Op `GetAlle` → Response `DatensatzListe`.
2. **Scaffold (D):** alle 8 Dateien; Projektion+Handle+Dispatch+Interfaces+Reader fertig.
3. **Effekt/Query (S):** `NimmRangeAuf` = *anhängen-dedup + aus-Ausgeschlossen-entfernen + Zähler=Count*
   (drei Vokabel-Ops). `GetAlle` = *sort ProduziertAm desc* (Zone 1) + Feld-Mapping (Zone 2). **Kein LLM.**
4. **▶ Simulieren:** Command-Trace → `PaareAufgenommen` → Dict-Dokument; Query → Antwort; Inspektor
   zeigt Mitglieder (dedupliziert?), Zähler; zweimal → identisch (Idempotenz).
5. **Marten-Gate:** Round-trip-Test grün → shipping.

## 10 · Was neu zu bauen ist + Phasen

| # | Neu | Klasse |
|---|---|---|
| 1 | Modell: ReadModel/Store(Effekt-Ops+Query-Ops)/Projektion(Handle-Regeln+Achsen+Append)/Reader | mechanisch |
| 2 | Editor: 5 Node-Typen + Ports + Verdrahtung | folgt Slot-Mustern |
| 3 | Scaffolder + Validator: 8 Dateien + Vokabel-Ops + Guardrails (Achse-B, GA-1) | analog Aggregat |
| 4 | **Runtime: Dict-`ICoCommitSession` + Event→Projektion + Reader-Run + Dokument-Inspektor** | Kern |
| 5 | **Framework-Refactor:** `Enqueue`→`ICoCommitSession`; Query-Ops als `IQueryable`-Rümpfe | klein–mittel |
| 6 | Marten-Gate-Testtier | klein |
| 7 | (optional, später) LLM-Fill NUR für **H**-Rümpfe; Compile/Run/Sim = Verifizierer | Ausnahme |

**Phasen:** (1) Modell + Scaffold der 8 Dateien (Vokabel-Ops als S), Golden-Test gegen die echte
`Modell`-Projektion. (2) Dict-Session + ▶ Testen erweitern. (3) Read-Seite live + Marten-Gate.
(4) LLM-Fill nur für die H-Restfläche.

## 11 · Ehrliche Grenzen

- **H bleibt Code** (neuartige Transforms/Aggregationen außerhalb des Vokabulars) — Hand oder LLM,
  aber klein und sim-verifiziert. Ziel ist, das Vokabular so zu wählen, dass H selten ist.
- **Marten-Exotik** (computed indexes, Volltext, `.Include`-Joins, duplizierte Felder) ist Store-
  *Konfiguration* außerhalb des portablen Modells → bleibt Hand.
- **Sim beweist Semantik**, nicht Exactly-once-unter-Fehlern / geordneten Pull / Redis-Deps — das
  bleiben Infra-Belange (das Marten-Gate deckt die Übersetzung, nicht die Ausfallsemantik).
- **Mehrere Stores je Projektion** sind architektonisch unerwünscht (bräche Co-Commit-Atomizität):
  ein Store = eine Transaktionsgrenze mit N Read-Model-Dokumenten; zwei echte Grenzen = zwei
  Projektionen. Beides modelliert der Editor natürlich.
