# Konzept — Handle-Ausgänge sichtbar machen (Projektion, Reader, Reaktion, Pipeline)

> **Stand:** 2026-09-30 · **Status:** UMGESETZT (Schritte 1–5 + Pipelines auf OneOf, §9; Entscheidung „nur Signatur“ + CQRS050, §10) · **Ort:** `GraphExtractor/DomainExtractor.cs`,
> `GraphExtractor/ModellMapper.cs`, `GraphExtractor/HtmlPresenter.cs`, `GraphExtractor/Sonde/`
> Verwandt: [konzept-domaenen-editor.md](konzept-domaenen-editor.md) §3/§6, [konzept-editor-panel-bearbeitung.md](konzept-editor-panel-bearbeitung.md) §8 (Zeilenraster),
> [konzept-llm-minimalkontext.md](konzept-llm-minimalkontext.md) (Slot-Schlüssel).

## 1 · Die Frage

Beim Decider sieht man im Editor sofort, **was** er erzeugen kann: je Command ein Decider-Knoten, je OneOf-Variante ein
Ausgang mit Kante zum Event, dazu der Guard („weil State.Existiert“). Bei den Handle-Methoden von Projektion, Reader,
Reaktion und Pipeline sieht man das nicht. Ziel: **für jeden Handler sehen, welche Typen er erzeugen kann** — aus dem Code
abgeleitet, nie geraten.

## 2 · Ist-Zustand (gemessen am Bestand)

| Konsument | Signatur im Code (Bestand) | Was der Extractor liest | Im Modell | Im Editor |
|---|---|---|---|---|
| **Decider** | `IEnumerable<OneOf<E1…En>> Decide(Cmd)` | Signatur (Routing-Tabelle) **+ Guard je yield** (`ExtractGuards`) | `decider.ergibt[{event,guard}]` | **eigener Knoten je Command**, Ausgänge als Kanten Decider → Event |
| **Reader** | `Task<R>` (13×) / `Task<OneOf<R1,R2>>` (4×) | Signatur (`HandleResponses`) — **ohne** Guard, `return`s nicht gelesen | `reader.handles[].responses` | nur im Panel des Readers; auf dem Graphen eine Sammelkante Reader → Response für **alle** Queries |
| **Projektion** | `Task` (Seiteneffekt) | Store-Aufrufe (`fns`), ge-yieldete Events | `handles[].fns`, `handles[].publishes` | nur im Panel; Sammelkante Projektion → Store |
| **Reaktion** | `IAsyncEnumerable<OneOf<C…>>` | Element-Typen der Signatur (`YieldTypen`) | `handles[].sends/publishes` | nur im Panel (Bestand: 0 Reaktionen) |
| **Pipeline** | **4× `IAsyncEnumerable<ICommand>`**, 1× `IAsyncEnumerable<OneOf<DateiErkannt>>`, sonst `Task` | nur **tatsächliche** `yield`s (`AusgegebeneTypen`) + `ScheduleSelf` | `handles[].sends/emits/schedules` | nur im Panel |

**Befund — drei Ursachen:**

1. **Darstellung (Hauptursache).** Der Decider ist ein *eigener Knoten je Eingang*. Die übrigen Konsumenten sind *ein*
   Knoten mit *n* Handles darin — ihre Ausgänge verschmelzen auf dem Graphen zu Sammelkanten (Reader → alle Responses) und
   sind nur im Panel je Handle zu sehen. Welche Query welche Antwort liefern kann, zeigt der Graph nicht.
2. **Fehlende Fakten.** Guards gibt es nur beim Decider. Beim Reader werden die `return`s nicht gelesen (also kein
   „NichtGefunden, weil model is null“). Die Rückgabe-**Form** (einzeln `Task<R>` vs. Strom `IAsyncEnumerable<…>` vs.
   nichts `Task`) steht nirgends im Modell.
3. **Offene Signaturen.** `IAsyncEnumerable<ICommand>` sagt nichts: der Vertrag ist „irgendein Command“. Die konkreten
   Typen kennt nur der Rumpf (`yield`). Der Decider hat dieses Problem nicht, weil der Generator OneOf erzwingt.

## 3 · Zielbild: der Handle wird ein eigener Knoten — wie der Decider

Der Decider ist genau das: der *Handle eines Aggregats* für einen Command. Dasselbe Muster für alle Konsumenten:

```
   Aggregat (Hub)      ◀─ Decider[Cmd]  ─▶ Event | Event | Ablehnung        (heute)
   Reader (Hub)        ◀─ Handle[Query] ─▶ Response | Response              (neu)
   Projektion (Hub)    ◀─ Handle[Event] ─▶ ruft Store.Fn · veröffentlicht Event
   Reaktion (Hub)      ◀─ Handle[Event] ─▶ Command | Command · Event
   Pipeline (Hub)      ◀─ Handle[Eingang] ─▶ Command | Trigger | ↺ Self-Tick
```

- **Knoten „Handle“** (Art `handle`), abgeleitet aus `owner.handles[i]` — **kein neues Modell-Objekt**, nur eine eigene
  Karte. Identität = Slot-Schlüssel `art|Besitzer|Disc` (z. B. `reader|DatensatzReader|HoleDatensatz`), also dieselbe
  wie beim LLM-Kontext. Rename-fest über den Eingangstyp, keine Index-Ids.
- **Kanten:** Eingang (Event/Query/Trigger) → Handle → Ausgänge; Handle → Besitzer (Hub-Kante, wie Decider → Aggregat).
  Die Sammelkanten Reader → Response usw. entfallen.
- **Karte (Kurzfassung):** `HoleDatensatz → DatensatzAntwort | DatensatzNichtGefunden` — die OneOf-Varianten mit `|`,
  wie beim Command `→ Event`. Pipeline: `RangeAngefordert → NimmRangeAuf · ⚠ offen`.
- **Panel:** Abschnitt „Kann erzeugen (OneOf)“ mit je Variante einer Zeile: Typ ▶ · Herkunft (Signatur / yield) ·
  Guard („weil model is null“) — gleich aufgebaut wie die Ausgänge beim Decider.
- **Besitzer-Karte** wird Hub: `5 Handles · Stores: IDatensatzReadStore` (wie `Aggregat: 6 Commands · 7 Events`).
- **Zeilenraster (§8 Panel-Konzept):** Der Code-Eingang wandert vom Besitzer an den Handle. Jeder Handle hat genau
  **einen** Code-Eingang → darunter genau 2 Plätze (📝 + 🤖), wie beim Decider. Der Besitzer hat keine Code-Stapel mehr.
  Damit ist das Raster auch für Projektion (bis 10 Handles) und Reader überall gleich getaktet.
- **Store-Funktionen** analog als eigene Knoten `Fn` unter dem Store (Impl-Rumpf je Fn) — Phase 3, gleicher Mechanismus.

## 4 · Welche Fakten fehlen — und woher sie kommen (nur Code-Fakten)

Je Handle ein neues Feld `ausgaenge` im Board-JSON (`ModellMapper.ZuBoardJson`); `responses`/`sends`/`emits`/`publishes`
bleiben als abgeleitete Kurzlisten erhalten (Kompatibilität, Scaffolder unberührt).

```jsonc
"handles": [{
  "query": "HoleDatensatz",
  "form": "einzeln",                       // einzeln = Task<R> | strom = IAsyncEnumerable/IEnumerable | nichts = Task/void
  "signaturOffen": false,                  // true, wenn das Element-Typargument ein Marker ist (ICommand, IEvent, IPipelineOutput …)
  "ausgaenge": [
    { "typ": "DatensatzAntwort",       "art": "response", "quelle": "beides",   "guard": "!(model is null)" },
    { "typ": "DatensatzNichtGefunden", "art": "response", "quelle": "beides",   "guard": "model is null" }
  ]
}]
```

| Fakt | Quelle im Code | Extractor |
|---|---|---|
| `form` | Rückgabetyp-Symbol: `Task`/`ValueTask` ohne Argument → nichts; `Task<T>` → einzeln; `IAsyncEnumerable<T>`/`IEnumerable<T>` → strom | neu, ein Helfer für alle vier Konsumenten |
| Varianten aus der **Signatur** | Typargumente von `OneOf<…>` bzw. das eine `T` (heute schon: Reader `HandleResponses`, Reaktion `YieldTypen`) | vereinheitlichen |
| `signaturOffen` | Element-Typ ist ein Vertrags-Marker (`Vertrag.cs`: `ICommand`, `IEvent`, `IPipelineOutput`, `IQueryResponse`) statt eines konkreten Typs | neu |
| Varianten aus dem **Rumpf** | `yield return x` / `return x` über den Ausdrucks-Typ, OneOf aufgefächert (heute `AusgegebeneTypen`, nur Pipeline) | auf Reader + Reaktion ausweiten |
| `quelle` | Vergleich Signatur ↔ Rumpf: `beides` · `signatur` (deklariert, nie ausgegeben) · `yield` (nur im Rumpf — nur bei offener Signatur möglich) | neu |
| `guard` | `ExtractGuards` verallgemeinern: `yield` **und** `return`, alle umschließenden `if`, verzweigungstreu — gleiche Regel wie beim Decider | verallgemeinern |
| `art` | Marker des Ausgabe-Typs: `IQueryResponse` → response, `ICommand` → command, `IPipelineTrigger` → trigger, `IEvent` → event (reaktiv), `ScheduleSelf` → self, Store-Aufruf → storefn | aus Bestehendem zusammenführen |

Projektionen haben die Form `nichts` — ihre „Ausgänge“ sind die **Effekte**: aufgerufene Store-Fns (`fns`, schon da) und
veröffentlichte Events (`publishes`, schon da). Beide kommen als `ausgaenge` mit `art: storefn/event` dazu, samt Guard
(ein `if` um den Store-Aufruf ist ebenfalls ein Zweig).

## 5 · Neue Diagnosen (Graph-Diagnosen wie `UNUSED-EVENT`)

| Code | Wann | Beispiel im Bestand |
|---|---|---|
| `HANDLE-OFFEN` | Strom-Handle mit offener Signatur (`IAsyncEnumerable<ICommand>`): der Vertrag steht nur im Rumpf | `DatensatzResolverPipeline` (2×), `ImageProcessingPipeline` (2×) — Vorschlag: `IAsyncEnumerable<OneOf<NimmRangeAuf, …>>` |
| `AUSGANG-TOT` | Variante in der Signatur, aber kein `return`/`yield` im Rumpf | (zu messen) |
| `RESPONSE-OHNE-QUERY` | Response wird von keinem Handle erzeugt | ersetzt die heutige Insel-Erkennung für Responses |

`HANDLE-OFFEN` ist nur ein Hinweis, kein Fehler: der Editor zeigt die Ausgänge trotzdem (aus den `yield`s, Herkunft
„yield“, gestrichelt), macht aber sichtbar, dass der Typ-Vertrag fehlt. Den OneOf-Vertrag für Pipelines zu *erzwingen*
wäre ein eigener Generator-/Analyzer-Schritt (analog CQRS020) und ist **nicht** Teil dieses Konzepts.

## 6 · Bearbeiten (Panel, Verbinden-Modus)

- Handle-Panel: `◀ Eingang` (⊕) · `▶ Kann erzeugen` (⊕, Liste, Reihenfolge = OneOf-Reihenfolge) · `◀ Rumpf` (📝/🤖).
  Die Ports gibt es heute schon (`qrsp`, `sagaCmd`, `evtOut`, `trigmsg`, `wcall`) — sie wandern nur von der Besitzer-Karte
  auf die Handle-Karte. `applyLink`/`loeseLink` bleiben unverändert (sie adressieren schon `handleIdx`).
- **＋ Handle** am Besitzer legt einen leeren Handle an (Eingang über ⊕ wählen), wie „+ Decider“.
- Schreiben: der Scaffolder deckt die Leseseite heute nicht → Änderungen an Ausgängen bleiben „✎ ungeschrieben“, bis der
  Leseseiten-Scaffolder kommt (eigener Schritt). Der 🤖-LLM bekommt die Ausgänge schon heute im Slot-Kontext (Rückgabe-
  Signatur); mit `ausgaenge` + Guards kann der Kontext die erlaubten Varianten explizit nennen.

## 7 · Umsetzung in Schritten

| # | Schritt | Kern | Prüfung |
|---|---|---|---|
| 1 | **Fakten** | `DomainExtractor`: `form`, `signaturOffen`, Signatur- + Rumpf-Varianten, Guards (yield **und** return) für Reader/Reaktion/Pipeline/Projektion; `ModellMapper`: `ausgaenge` je Handle | `--sonde`: `Sonde/*.cs.txt` um je einen Reader mit OneOf + Guard und eine Pipeline mit offener Signatur erweitern, `soll.txt` von Hand ergänzen; `--check` grün |
| 2 | **Diagnosen** | `HANDLE-OFFEN`, `AUSGANG-TOT`, `RESPONSE-OHNE-QUERY` in `GraphBuilder` | Bestand: 4× `HANDLE-OFFEN` erwartet |
| 3 | **Handle-Knoten** | `graphNodes` + `boardEdges` um `handle` (Id = Slot-Schlüssel); Sammelkanten raus; Karte + Kurzfassung (`A \| B`); Besitzer als Hub | `deGraph()`: je Handle ein Knoten, keine Kante Reader → Response mehr |
| 4 | **Panel + Raster** | Ports auf die Handle-Karte; Code-Eingang am Handle (`codeEingaenge`), `findCodeOwner`/`konsolenId` → Handle | alle 168 Code-Blöcke unter ihrem Handle/Decider/Applier, 0 Überlappungen; Verbinden/Lösen je Port-Art |
| 5 | **Store-Fn-Knoten** (optional) | gleicher Mechanismus für Store-Funktionen | wie 3/4 |

Schritte 1–2 sind reine Extraktion (kein UI-Risiko) und liefern die Fakten auch für den LLM-Kontext; 3–4 sind der
sichtbare Gewinn.

## 8 · Offene Entscheidungen

1. **Handle als Knoten oder nur als Kurzfassung?** *Empfehlung:* Knoten — nur so gibt es Kanten je Query/Event und das
   gleichmäßige Raster (ein Code-Eingang je Knoten). Aufwand: mehr Knoten (Bestand: 5 Projektionen → 36 Handles,
   5 Reader → 17, 5 Pipelines → 11), dafür entfallen die Code-Stapel unter den Besitzern.
2. **Pipelines auf OneOf umstellen?** *Empfehlung:* ja, als eigener Schritt nach `HANDLE-OFFEN` — dann ist der Vertrag
   überall aus der Signatur ablesbar, wie beim Decider.
3. **Store-Fns als Knoten (Schritt 5)?** *Empfehlung:* erst nach 3/4 entscheiden (Store mit 18 Fns = 18 Knoten).

## 9 · Umgesetzt (2026-09-30)

Entschieden: Handle **und** Store-Fn als eigene Knoten, Pipelines auf `OneOf` umgestellt.

| Was | Wie (Code) |
|---|---|
| Fakten | `DomainExtractor.LiesHandleVertrag` je Handle (Projektion/Reader/Reaktion/Pipeline): `Form`, `SignaturOffen`, `Signatur`, `Ausgaenge` (Art aus dem Marker, Quelle signatur/rumpf/beides, Guard). Guard = `Bedingungen`: umschließende `if`/`else`/`?:` **plus vorangehende frühe Ausstiege** (`if (c) return …;` ⇒ `!(c)`), `!(!X)` → `X`, `c` + `!(c)` ⇒ immer. Rumpf-Werte über `Zweige` (auch `?:` und Casts) — gleicher Helfer jetzt auch für `sends` der Pipeline. Store-Aufrufe und `ScheduleSelf` als Effekte mit Guard. |
| Board-JSON | `ModellMapper.ZuBoardJson`: je Handle `form`, `signatur`, `signaturOffen`, `ausgaenge[{typ,art,quelle,guard,fn,store}]` (Store-Aufruf → Board-Fn-Id). |
| Diagnosen | `GraphBuilder`: `HANDLE-OFFEN`, `AUSGANG-TOT`, `RESPONSE-OHNE-QUERY` (Responses, die als Feld-/Element-Typ einer anderen Response vorkommen, zählen als lebendig). |
| Pipelines | `DatensatzResolverPipeline` (2×) und `ImageProcessingPipeline` (2×): `IAsyncEnumerable<ICommand>` → `IAsyncEnumerable<OneOf<…>>` (der Generator routet OneOf schon). Danach 0× `HANDLE-OFFEN`. |
| Sonde | `Sonde/Lesen.cs.txt` + `soll.txt`: je Handle eine `ausgang …`-Zeile; neuer Pipeline-Handle mit offener Signatur, frühem Ausstieg und `?:` hinter Cast. Die Sonde fand dabei, dass `sends` `?:`/Cast nicht sah → behoben. |
| Editor | Knotenarten `handle` (Id `hd:<Besitzer>:<Eingangstyp>`) und `fn` (Id `fn:<FnId>`), abgeleitet aus `owner.handles[i]` bzw. den Store-Fns. Die Ports je Handle/Fn wandern unverändert auf die eigene Karte → `drawEdges`, `applyLink`/`loeseLink` und der Verbinden-Modus funktionieren ohne Änderung. Besitzer-Karte = Hub mit Handle-Liste zum Springen; Hub-Kanten Handle → Besitzer, Fn → Store. Jede Ausgangs-Zeile zeigt `wenn <Guard>` bzw. „nur Signatur“/„nur im Rumpf“; Kopf zeigt die Signatur, offene Signatur als Warnung. Kurzfassung auf der Karte: `→ A \| B · ruft Fn`. |
| Raster | Code-Eingang sitzt jetzt am Handle/an der Fn (je genau 📝 + 🤖). Spalten: Projektion → Handles → Store-Fns → Store/ReadModel; Query → Reader-Handle → Response → Reader. Die Fn-Spalte wird zuletzt gepackt und steht auf der Höhe ihres ersten Aufrufers. |

**Gemessen (echter Editor):** 553 Karten (64 Handles, 44 Fns), 0 Überlappungen, 168/168 Code-Blöcke unter Handle/Fn/Decider/Applier,
keine Sammelkante Reader → Response mehr. Verbinden-Modus am Handle: „+ Response“ → 21 Kandidaten (2 ✓), verbinden + lösen;
„+ Read-Fn“ → 18 Fn-Karten; „+ Query andocken“ am Reader legt einen neuen Handle-Knoten mit Kanten an, ✕ löscht ihn wieder.
`--check` grün (Fixpunkt + Inventar), `--sonde` grün (44 Soll-Fakten), Prüfstand 140/140, Solution-Build 0 Fehler.

**Grenzen:** Lange Kanten bleiben, wo viele Handles dieselbe Fn aufrufen (z. B. `AppendEintragAsync` ← 10 Handles). Die
Decider-Guards nutzen weiterhin die alte Regel (ohne frühe Ausstiege), weil der Simulations-`GuardBinder` davon abhängt.

## 10 · Entscheidung: das WAS nur aus der Signatur (CQRS050)

**Frage:** Brauchen wir überall `OneOf`, oder reicht eine Code-Analyse der Rümpfe? **Antwort: `OneOf` (bzw. ein konkreter
Typ) in der Signatur — eine Rumpf-Analyse kann das grundsätzlich nicht leisten.**

- „Welche Typen kann diese Methode zurückgeben?“ ist im Allgemeinen unentscheidbar (Satz von Rice). Die Signatur ist eine
  vom **Compiler** garantierte Obergrenze; eine Rumpf-Suche ist eine Untergrenze, die Fehlendes **still** übersieht
  (Variable mit Interface-Typ, Hilfsmethode, Schleife über eine Liste, `await foreach`, `switch`-Ausdruck, Fabrik …). Jede
  neue Schreibweise wäre ein weiterer Flicken — der `?:`/Cast-Fix aus §9 war genau so einer.
- Warum `OneOf` ursprünglich gewählt wurde: die Command→Event-Map (`GeneratedCommandRouting.Produziert`) liest die
  Decide-Signaturen **rein typbasiert, ohne yield-Analyse**; erst dadurch wurde der Azyklizitäts-Boot-Guard scharf (die
  vorherige Namespace-grobe Map hätte Falsch-Zyklen gemeldet). Invarianten 3/4: Routing über Typen, keine Reflection.
- Die Lücke war, dass nichts `OneOf` **erzwang**: Pipelines durften `IAsyncEnumerable<ICommand>` liefern, und ein nacktes
  `IEnumerable<IEvent>` am Decider hätte der `CommandAggregateMapGenerator` still geschluckt (leere Map ⇒ Guard dort blind).

**Umgesetzt:**

| Was | Wie |
|---|---|
| Regel | `Domain.SourceGeneration/AusgabeVertragAnalyzer.cs` — **CQRS050** (Build-Fehler): Decide/Projektion/Reaktion/Reader/Pipeline geben einen konkreten Typ oder `OneOf<…>` konkreter Typen zurück; offen = Interface, Typ-Parameter, `object`, abstrakte Klasse. `Task`/`void` erlaubt (reine Effekte). Rolle über Marker/Parameter-Typen. Bewiesen: 6 Prüfstand-Tests (`Analyzers/AusgabeVertragAnalyzerTests.cs`) + Gegenprobe im echten Build (eine Pipeline zurück auf `ICommand` ⇒ `error CQRS050`). |
| Extractor | `LiesHandleVertrag`: Ausgänge **nur** aus der Signatur (offene Signatur ⇒ keine Ausgänge, nichts geraten). Pipeline-`sends`/`emits` aus der Signatur statt aus den yields (die yield-Analyse `AusgegebeneTypen`/`EmittedCommands` ist gelöscht). Der Rumpf liefert nur noch: Guard je Ausgang, den Hinweis `ungesehen` (kein return/yield gefunden — heißt nicht „nie“) und die Effekte (Store-Aufrufe, `ScheduleSelf`). |
| Diagnosen | `HANDLE-OFFEN` = Fehler (Rückhalt, falls der Analyzer nicht lief). `AUSGANG-TOT` → `AUSGANG-UNGESEHEN` als **info** (Rumpf-Hinweis, kein Befund). |
| Editor | Ausgangs-Zeilen: „wenn …“ bzw. „im Rumpf kein return/yield gefunden“, Tooltip „aus dem Rumpf gelesen: Erklärung, keine Garantie“. |
| Sonde | Pipeline-Handle mit `OneOf` statt offen; zusätzlich eine deklarierte, nie ausgegebene Variante (`MeldeSchaden ungesehen`) — beweist: das WAS kommt aus der Signatur. |

**Was Rumpf-Fakt bleibt:** ~~Guards, Store-Aufrufe, `ScheduleSelf`, HostSetting-Datenfluss~~ — mit §11 entfernt bzw. durch
typisierte Träger ersetzt. Übrig bleiben nur die Saga-Lambda-Texte (Argument-Mapping, reiner Code-Text, keine Kante).

**Offen (nicht Teil dieser Scheibe):** Reaktionen/Pipelines in den Azyklizitäts-Guard aufnehmen (die Ausgänge sind jetzt
typisiert); der `PipelineDispatchGenerator`-Zweig für `IEnumerable<ICommand>` ist durch CQRS050 unerreichbar und kann weg.

## 11 · Fähigkeiten statt Rumpf-Analyse (2026-09-30)

**Frage:** Guards, Store-Aufrufe, `ScheduleSelf` und `IFristplan`-Aufrufe waren die letzten Rumpf-Fakten, die der Editor als
Kanten/Ports zeigte. Eine temporäre Sonde zeigte: hinter Hilfsmethode, Delegate oder fremdem Objekt gehen sie verloren, ein nie
ausgeführtes Lambda erzeugt sogar eine falsche Kante. Der Editor soll Domänen **erzeugen** — dann braucht alles, was er zeichnet,
einen typisierten Träger, der auch nach einer Rumpf-Änderung (Mensch/LLM) exakt wieder eingelesen wird.

**Entscheidungen** (je die Empfehlung aus der Übergabe):

| Frage | Entscheidung |
|---|---|
| Granularität | **Fähigkeit je Funktion** (Interface mit genau einer Funktion) — nur so bleibt die Kante „Handle → Fn“ exakt. |
| Form | **Fähigkeit als Parameter von `Handle`** (`Handle(evt, env, writer, INimmRangeAuf store)`), keine Handler-Klassen. |
| `Selbst<T>`/`Frist<TCmd>` | **gleich mitgenommen**: Planen ist ein Ausgang im OneOf, kein Aufruf. |
| Betrieb-Band | **Frist typisiert** (aus `Frist<TCmd>`/`FristStorno<TCmd>` der Pipeline-Signaturen, Router generiert). **HostSetting-Datenfluss entfernt** (Ausdrucks-Datenfluss = Rumpf-Fakt; Settings nur noch als Entwurf im Editor). **Ingress** (`[Ingress]`-Attribut) und **Dienst-Bindung** (DI-Typargumente) bleiben — beides Typ-/Attribut-Fakten der Composition Root. |
| Guards | **entfernt** (Decider und Handles): Editor, Simulation „weil …“ (`GuardBinder` gelöscht), LLM-Skelett, Graph `Produces.Guard`, Sonde. Auch der Hinweis `AUSGANG-UNGESEHEN` ist weg. Das „Wann“ ist freier Code; der Editor zeigt dazu nichts. |

**Programmiermodell:**

```csharp
public interface IUpsertDatensatz : IWriteStore { Task UpsertAsync(DatensatzReadModel model); }       // Fähigkeit (CQRS051)
public interface IFindDatensatz   : IReadStore  { Task<DatensatzReadModel?> FindByIdAsync(Guid id); }
public interface IDatensatzStore  : IStore, IUpsertDatensatz, IFindDatensatz /* … */ { }              // Bündel = Store-Karte
public sealed partial class DatensatzStore : MartenCoCommitStoreBase, IDatensatzStore { … }          // eine Klasse (Lesen als partial)

public partial class DatensatzProjektion : ISubscriber, IPullSubscriber, IAppendProjektion             // Konstruktor OHNE Stores
{
    public Task Handle(PaareAufgenommen evt, IAggregateEnvelope env, ProjectionWriter writer, INimmRangeAuf store) => …;
}
public IAsyncEnumerable<OneOf<DateiErkannt, Selbst<PollTick>>> Handle(PollTick t, PipelineContext ctx);   // Selbst-Nachricht
public IEnumerable<OneOf<Frist<MarkiereAlsHaengengeblieben>>> Handle(TrainingBegonnen e, PipelineContext ctx); // Frist
```

**Umgesetzt:**

| Was | Wie |
|---|---|
| Vertrag | `Abstractions`: `IWriteStore`/`IReadStore` = Fähigkeits-Marker, neu `IStore` (Bündel); `IReadStore<T>` gelöscht. `IFaehigkeiten`/`IFaehigkeitsBereich`/`IFaehigkeitsFabrik` + `FaehigkeitenAus` (Tests/Simulation). `Planung.cs`: `IPlanung : IPipelineOutput`, `Selbst<T>`, `Frist<TCmd>`, `FristStorno<TCmd>`, `FristAuftrag`, `PipelineGestartet`. `PipelineContext` ist reine Daten (kein `ScheduleSelf`/`CancelScheduled` mehr). |
| Domäne | 5 Stores → 44 Fähigkeiten + 5 Bündel; die Postgres-Lese-Klassen sind `partial` Teil der Co-Commit-Klasse (`*Store.Lesen.cs`), die ungenutzte `ImagePairHistorieStoreInMemory` ist gelöscht. 5 Projektionen, 5 Reader, `DatensatzResolverPipeline` ohne Store im Konstruktor. `FileWatchPipeline` plant über `Selbst<PollTick>` (erster Tick in `Handle(PipelineGestartet)`), `TrainingFristPipeline` über `Frist`/`FristStorno` (kein `IFristplan`, kein `IDbClock` mehr). Host.Grpc: `AddDeadlines(GeneratedFristen.Baue)`. |
| Dispatch | Subscriber-/Reader-/Pipeline-Dispatch nehmen `IFaehigkeiten` und rufen `Handle(…, faehigkeiten.Hole<IX>())` (Typ im Generat, keine Reflection). Query-Service: ein Bereich je Query. Pipeline-Dispatch bekommt die Senke `plane`; `Frist<X>`-Fälle setzen den Kontext (Command-Typname) als Konstante ein. Der tote `IEnumerable<ICommand>`-Zweig ist weg. |
| DI | `ProjectionServicesGenerator`: jede Store-Klasse **Scoped**, umgeleitet unter jeder Fähigkeit + dem Bündel (eine Instanz je Bereich → Co-Commit). CQRS053 bei zwei Klassen je Fähigkeit. `DiFaehigkeitsFabrik` (ein `IServiceScope` je Bereich), `FristPlaner` (DB-Uhr + Fristplan). |
| Achse B | `PullPathGenerator` liest die Schreib-Fähigkeiten aller Handles (statt Konstruktor-Stores); ein Bereich je Adapter-Actor, Tracker = die aufgelöste Store-Instanz. **CQRS052** (Compile-Zeit): Schreib-Fähigkeiten aus mehreren Stores in einer Klasse. |
| Fristen | `PipelineActorBase.PlaneAsync`: `Selbst` → `ReenterAfter` (Token ersetzt), `FristAuftrag` → `FristPlaner`. Pull-Brücke: Frist ja, `Selbst` aus einem Event-Handle = `NotSupportedException` (keine Mailbox). `GeneratedFristen.Baue` aus allen `Frist<TCmd>`; **CQRS056**, wenn `TCmd` keinen Ctor `(Guid)` hat. |
| Analyzer | `FaehigkeitAnalyzer`: **CQRS051** (Fähigkeit = genau eine Funktion, erbt keine andere), **CQRS054** (Store/Bündel/Fähigkeit/`IFristplan` in Ctor, Feld oder Eigenschaft eines Konsumenten, auch statisch), **CQRS055** (`new …Store()`). Gegenprobe im echten Build: die alte `TrainingFristPipeline` lieferte 2× CQRS054. |
| Extractor | Stores aus `IStore`-Bündeln (Fn = Fähigkeit), `HandleFaehigkeiten` aus den Parametern (auch für Pipelines), `storefn`-Ausgänge aus den Parametern, `self`/`frist`/`fristStorno` aus `Selbst<T>`/`Frist<TCmd>`/`FristStorno<TCmd>`. Gelöscht: `ExtractGuards`, `Bedingungen`, `Zweige`, `StoreAufrufe`, `ScheduleSelfKnoten`, `ImplReferenziert` (ReadModel → Store nur noch über Fähigkeits-Signaturen). Composition Root: Frist aus den Signaturen, HostSetting-Datenfluss entfernt. |
| Editor | Fn-Karte zeigt ihr Fähigkeits-Interface und „Fähigkeit von“ (Projektion/Reader/Pipeline); Handle-Ports „darf Store.Fn ▶“; Pipeline-Handles haben Read-Fn-Ports (verbinden/lösen); Self-Planung ohne Verzögerungs-Feld (Rumpf). Keine „wenn …“-Zeilen mehr. |
| Sonde | `Lesen.cs.txt` mit Fähigkeiten + Bündel und den vier Verschleierungen (Hilfsmethode, Delegate, fremdes Objekt, totes Lambda) + Pipeline mit Lese-Fähigkeit, `Selbst<T>` und `Frist<TCmd>`; `soll.txt` ohne Guards. |

**Gemessen:** Solution-Build 0 Fehler; Prüfstand 153/153 (neu: 5 Analyzer-Tests `FaehigkeitAnalyzerTests`, 2 Brücken-Tests
Frist/Selbst); `--check` grün (Fixpunkt + Inventar, 0 HostSettings); `--sonde` grün (**46** Soll-Fakten, die Verschleierungs-Fälle
liefern exakt die Signatur-Kanten); Editor live: 44 Fn-Karten mit Fähigkeit, 57 „darf …“-Ports (36 Projektion, 19 Reader, 2 Pipeline),
keine „wenn …“-Zeile. **Nicht gemessen:** Integration (Docker lief nicht) — die Store-Lifetime (Scoped statt Transient/Singleton)
und der Fristen-Pfad sind dort noch zu bestätigen.

**Offen:** ~~Scaffolder schreibt die Leseseite (Fähigkeit + Bündel + Parameter) noch nicht~~ → geliefert, siehe §12; Reaktionen/Pipelines in den
Azyklizitäts-Guard aufnehmen (die Ausgänge sind typisiert); Editor-Band für HostSettings ist nur noch Entwurf (kein Code-Träger).

**Nachprüfung „alles aus dem Code abgeleitet?“ (2026-09-30):** Extractor/Editor/SimHost enthalten keine Domänen-Namen und keine
Namensregeln (Treffer sind Anzeige-Labels oder abgeleitet, z. B. Command-Herkunft „Client“ = kein interner Erzeuger). Behoben:
(1) `Frist`/`FristStorno`/`Guid` wurden in Pipeline-Dispatch und Fristen-Router über Namens-Strings erkannt → jetzt über das Symbol;
(2) Editor (Handler = Typen) und Dispatch (Name `Handle` + feste Form) konnten still auseinanderlaufen → **CQRS057**
(`HandlerFormAnalyzer`, Gegenprobe: umbenannter Handler = Build-Fehler); (3) toter Text-Parser `ExtractPipelineId` gelöscht.
**Bewusst stehen gelassen (Framework-Vertrag, keine Domäne):** der Methodenname `Handle`, die Vertrags-Metadatennamen
(`"Abstractions.IWriteStore"` …) in den Generatoren (die referenzieren Abstractions nicht), `OneOf` per Name+Namespace (Familie
mit variabler Stelligkeit). **Offen (älter):** die generierten Artefakte liegen in fest verdrahteten Namespaces
(`Domain.Projections.ProjectionQueryService`, `Domain.Prozess.GeneratedProzessRegeln`, `Domain.Infrastructure.Generated`) und
werden von Infrastructure/ProjectionServicesGenerator unter diesem Namen gesucht — funktioniert, bindet das Framework aber an die
Projektnamen dieser Solution.

## 12 · Editor → Code für die Leseseite (2026-09-30)

**Frage:** Der Editor LIEST die Leseseite vollständig aus Signaturen (Store-Karten, Fn-Karten je Fähigkeit, „darf Store.Fn ▶“-Ports),
SCHREIBEN konnte er sie nicht: `EditorModell` hatte keine Leseseite, der Scaffolder kannte nur Records/Aggregate/Sagas, und
Editor-Änderungen (Fn anlegen, Handle ↔ Fn verbinden/lösen, Query andocken, neuer Store/Projektion/Reader) landeten nirgends.

**Entscheidungen** (je die Empfehlung, Annahmen benannt):

| Frage | Entscheidung |
|---|---|
| Wo lebt die Leseseite? | **Eigene Records in `EditorModell.Lesen`** (`Store`/`Faehigkeit`/`StoreImpl`, `Konsument` = Projektion+Reaktion, `Leser`, `PipelineKarte`, gemeinsamer `Handle`). Query/Response/ReadModel sind **Record-Arten** (`query`/`queryresponse`/`readmodel`). Das Board behält seine Anzeige-Sammlungen; die Code-Fakten reisen darin mit (`sig` je Fn/Handle, `code` je Klasse, `impl`/`datei` je Store). `DomainEditor.BoardLeseseite.AusBoard` liest daraus das typisierte Modell. |
| Fähigkeits-Name neuer Fn | **Abgeleitet + im Panel editierbar**: `I` + Methode ohne `Async`, vergeben ⇒ + Store-Name ohne `I`. Danach Code-Fakt (fest im Panel, der Extractor rät nie). |
| Rümpfe | **Kein Rumpf-Code außer Platzhalter** (`throw new NotImplementedException("TODO: …")`) — für Handles und Store-Methoden. Kein Marten-Wissen im Scaffolder. |
| Dateiorte | **Aus dem Code**: Fähigkeit → Datei des Bündels; Handle → Datei der Klasse; Store-Methode → Datei, in der die Schreib- bzw. Lese-Methoden der Impl liegen (nur wenn eindeutig, sonst die Deklaration). Neu ⇒ Verzeichnis des Namespace (Rahmen); unbekannt ⇒ nicht platzierbar. |
| Store-Impl | **Nur bei genau einer Impl-Klasse** je Bündel ergänzt (sonst Panel-Hinweis, nichts geraten). Neuer Store ⇒ neue Klasse; ihre Basis/Ctor nur, wenn ALLE Impls im Code eine gemeinsame Basis haben (`Rahmen.StoreBasis`, hier `MartenCoCommitStoreBase(IDocumentStore)`), ihr Namespace nur, wenn alle Impls in einem liegen (`Rahmen.StoreImplNamespace`). |
| Schreiber-Typ | `ProjectionWriter` liegt in Core (DomainEditor referenziert nur Abstractions): **aus den Projektions-Handles im Code gelesen** (3. Parameter, CQRS057), nur wenn eindeutig. Kein Handle im Code ⇒ keine neue Projektion (Validator meldet es). |

**Umgesetzt:**

| Was | Wie |
|---|---|
| Modell | `EditorModell.Lesen` (s. o.), `RecordArt.Query/Antwort/ReadModel`, `Rahmen.ProjektionsSchreiber[Namespace]`/`StoreBasis`/`StoreImplNamespace`. |
| Extractor | verbatim je Handle: Parametername, Kontext-Namen, Fähigkeits-Parameter, Rückgabe, Modifizierer, Rumpf/Ausdruck, Datei (`HandleSigRaw`); je Klasse: Datei, Doku, Form, Basisliste, Attribute (ohne `[ProjectionReader]`), übrige Member als Zusatz, using-Direktiven inkl. Aliase (`KlassenQuelle`); je Fähigkeit Rückgabe/Parameter verbatim, Namespace, Datei, Doku; je Store Bündel-Datei/Doku und die einzige Impl (Deklaration, Schreib-/Lese-Datei). |
| Mapper | `ModellMapper.ZuEditorModell` füllt `Lesen` + die Leseseiten-Records; `ZuBoardJson` legt sie in die Board-Sammlungen (`sig`/`code`/`impl`), keine doppelten Query/Response-Records mehr, ReadModels nur in `readModels`. |
| Scaffolder | neue `DateiArt`s: `Schnittstellen` (Fähigkeit je Fn einzeilig + Bündel `: IStore, …`), `StoreImpl` (neue Klasse voll bzw. nur Methoden NEUER Fähigkeiten), `Konsument` (`partial class … : ISubscriber, IPullSubscriber[, IAppendProjektion]`, `SubscriberId`, `Handle(TEvent, IAggregateEnvelope, ProjectionWriter, Fähigkeit…)` → `Task` bzw. `IAsyncEnumerable<OneOf<…>>`), `Leser` (`[ProjectionReader(TrackDeps = …)]`, `IReader<P>`, `Handle(TQuery, IMessageEnvelope, ReadContext, Fähigkeit…)` → `Task<R>`/`Task<OneOf<…>>`). Bestehende Klassen: usings verbatim, abgeleitete nur für neue Handles (keine neuen Mehrdeutigkeiten). |
| DateiSchreiber | additiv: fehlende Fähigkeits-Interfaces + fehlende Basen im Bündel; fehlende Impl-Methoden (Schlüssel Name + Parametertypen); fehlende Handles (nur die im Modell neuen). **Parameter-Abgleich**: je bestehendem Handle werden die Parameter hinter dem Kontext (CQRS057: 2 bzw. 1 bei Pipelines) auf das Modell gebracht — hinzu/weg, vorhandene wörtlich, fehlende usings ergänzt, **nur die Parameterliste**. |
| SimHost | `/api/editor/write` und `/validate` lesen das Board über `BoardLeseseite`; nach dem Schreiben werden die betroffenen Projekte gebaut, Fehler kommen im Bericht zurück (Editor zeigt sie im Ausgabe-Panel). Simulation ohne Leseseite (`Lesen = null`). |
| Editor | Fn-Karte: Fähigkeit aus dem Code fest, neu = Feld mit Vorschlag; Store-Karte: Impl (Code) bzw. benennbar (neu). `mergeBoard` vergleicht die Leseseite über „Store.Fn“ statt Fn-Ids und ohne Code-Fakten — Geschriebenes wird nach dem Neu-Einlesen Code-Stand statt „ungeschrieben“. |
| Validator | `EDIT-FAEHIGKEIT-DUP` (zwei Fns, eine Fähigkeit), `EDIT-READER-OHNE-ANTWORT` (ohne Ausgabe-Vertrag nicht geschrieben), `EDIT-KONSUMENT-SCHREIBER`. |
| Parität | `--check`: neu **Board ⇄ Modell** (die aus dem Board gelesene Leseseite = die aus dem Code) und der **Fixpunkt über die Leseseite**: in allen Domänen-Projekten werden Fähigkeiten, Bündel, Projektionen/Reaktionen, Reader, Queries, Responses, ReadModels durch Scaffolder-Dateien ersetzt, die Store-Impls bleiben stehen (sie müssen gegen die geschriebenen Fähigkeiten kompilieren). Gegenprobe: Scaffolder ohne Fähigkeits-Parameter ⇒ 10 Fixpunkt-Abweichungen + 25 Compile-Fehler. |
| Sonde | `Sonde/Gezeichnet.board.json`: eine im Editor gezeichnete Leseseite (neuer Store mit einer im Panel benannten und einer abgeleiteten Fähigkeit, Projektion, Reaktion, Reader, Query/Response/ReadModel) wird an das Board der Sonde gehängt → `BoardLeseseite` → Scaffolder → Fork → neu eingelesen; `soll.txt` von Hand um 15 Fakten erweitert (u. a. `faehigkeit …`-Zeilen). |

**Gemessen:** Solution-Build 0 Fehler; Prüfstand **166/166** (neu: 11 `DomainEditorLeseseiteTests`, darunter Scaffolder-Ausgabe
kompiliert + besteht **alle** Analyzer aus `Domain.SourceGeneration` — CQRS050/051/054/055/057); `--check` grün (232 Typen in 4
Domänen-Projekten durch 71 Scaffolder-Dateien ersetzt, davon 44 Fähigkeiten, 5 Bündel, 5 Projektionen/Reaktionen, 5 Reader);
`--sonde` grün (**61** Soll-Fakten). **Live im Browser** (SimHost): an `IModellStore` Fn `MarkiereGeprueftAsync(Guid modellId)` mit
Fähigkeit `IMarkiereModellGeprueft` angelegt, Handle `ModellProjektion.Handle(ModellAktiviert)` verbunden, „C# schreiben“ →
`IModellStore.cs` (+Interface, +Basis), `ModellStore.cs` (+Platzhalter-Methode), `ModellProjektion.cs` (nur Parameterliste) →
betroffene Projekte gebaut, 0 Fehler → neu eingelesen: Kante + Fähigkeit aus dem Code. Lösen von `ISetzeModellAktiv` (Rumpf nutzt
sie) ⇒ Parameter weg, Rumpf unverändert, **CS0103 im Editor gemeldet**. Die Demo-Änderungen an der Domäne wurden zurückgesetzt.

**Grenzen (bewusst):** additiv — eine im Editor entfernte Fn/Fähigkeit bleibt im Bündel und in der Impl stehen; Rückgabetyp und
Parameter einer BESTEHENDEN Fn bzw. die Response-Liste eines bestehenden Reader-Handles ändert „C# schreiben“ nicht (nur die
Fähigkeits-Parameter der Handles); neue Pipelines/Pipeline-Handles werden nicht gescaffoldet (nur Fähigkeits-Parameter bestehender).
Eine neue Store-Impl ohne gemeinsame Basis ist kein Co-Commit-Tracker — die Basis wählt dann der Mensch.

## 13 · Bestehendes ändern + Betrieb schreiben (2026-09-30)

**Frage:** Nach §12 schrieb „C# schreiben“ nur NEUES; Änderungen an Bestehendem (Feld, Enum-Wert, State-Feld, OneOf, Prozess-Regel,
Handle-Ausgänge, Store-Fn-Signatur, Flags) wurden still verworfen („nichts zu schreiben“), Pipelines/Trigger/Konfig gar nicht.

**Kernentscheidung: Herkunfts-Stempel.** Beim Einlesen bekommt jedes änderbare Element einen Hash seines Modell-Inhalts
(`DomainEditor.Herkunft`). „C# schreiben“ gleicht ein bestehendes Element NUR ab, wenn sein Inhalt vom Stempel abweicht (= im Editor
geändert). Damit schreibt ein unverändertes Board garantiert nichts — auch wo die Textform des Codes von der Scaffolder-Form abweicht.
Gehasht wird, was der Scaffolder wirklich schreibt (Prozess: ein verbatim gelesenes Lambda hat Vorrang vor Stub-Argumenten des
Browsers). **Regel des Abgleichs:** das Modell besitzt Signatur/Deklaration, der Code Rümpfe und Handcode; Unverändertes bleibt Wort
für Wort (auch Formatierung: mehrzeilige Parameterlisten, unveränderte Prozess-Regeln samt Kommentaren); bricht dadurch ein Rumpf,
meldet es der Bau.

| Editor-Aktion | Was geschrieben wird |
|---|---|
| Feld an bestehendem Record hinzu/weg/Typ | Positions-Parameter bzw. Property-Felder (Feldregeln = `Codeformen.Feldregeln`, EINE Quelle für Extractor und Schreiber) |
| Enum-Wert | Member-Liste (vorhandene Member wörtlich) |
| State-Feld | Property einfügen/ersetzen/entfernen (Zusatz und `Id`/`Version` bleiben) |
| OneOf eines Deciders | nur das Typ-Argument der Rückgabe |
| Regel eines Prozesses | Anweisungen im `Definiere`-Lambda; unveränderte Regeln wörtlich; Ausdrucks-Lambda → Block bei zweiter Regel |
| Ausgänge eines Handles (Responses, sendet, veröffentlicht, erzeugt Trigger, plant Selbst) | nur das Typ-Argument der Rückgabe; Wrapper bleibt (`Task`/`IAsyncEnumerable`/`IEnumerable`); Fristen unverändert |
| Parameter/Rückgabe einer Store-Fn | Fähigkeits-Interface + Impl-Methode (Parameter wörtlich, wo unverändert) |
| Pull/Append, TrackDeps, SubscriberId/PipelineId, Reader → Projektion | Basisliste (Marker, `IReader<P>`), `[ProjectionReader(TrackDeps = …)]`, Literal (auch über `const` derselben Klasse) |
| Neue Pipeline / neuer Handle | `partial class : IPipelineHandler`, `PipelineId`, Konfig-Konstruktor, `Handle(TEingang, PipelineContext, Fähigkeit…)` → **immer** `IAsyncEnumerable<OneOf<…>>` (der Dispatch-Generator erkennt nur OneOf — auch bei einem Ausgang) |
| Neuer Trigger, Self-Tick, Konfig | Records `: IPipelineTrigger` / `: IPipelineSelfMessage` / ohne Marker (neue Record-Arten `trigger`/`selbst`/`konfig`) |
| Trigger mit Modus + Ort | neue Ingress-Bindung nach dem Vorbild einer bestehenden desselben Modus (Anweisung kopiert, Typ- und Ort-Argument ersetzt). Kein Vorbild (z. B. Timer) ⇒ Meldung „von Hand anlegen“ |
| Code-/LLM-Entwurf an neuem Handle / neuer Store-Fn | wird ihr Rumpf (`async`, wenn nötig) |

**Nicht still:** geänderte Elemente ohne Schreib-Regel (z. B. nur Doku, Konstruktor einer bestehenden Pipeline) meldet der Bericht als
„nicht geschrieben“. **👁 Vorschau** im Editor = Trockenlauf (`/api/editor/write?trocken=true`); Kommandozeile:
`dotnet run --project SimHost -- --trocken|--schreiben <board.json>`.

**Gemessen:** Build 0 Fehler; Prüfstand **170/170**; `--check` grün inkl. **Stempel-Idempotenz** (unverändertes Board = 0 geänderte
Elemente) und Fixpunkt über **242** Typen (jetzt mit Pipelines, Konfig/Trigger/Selbst); `--sonde` **66** Soll-Fakten (gezeichnete
Pipeline mit Konfig, Trigger, Self-Tick). **Szenario** (alle 12 Aktionen auf einmal gegen die echte Domäne, per CLI): geschrieben →
Solution baut mit 0 Fehlern → neu eingelesen → Trockenlauf leer → `--check` grün → zurückgesetzt. **Browser:** Vorschau auf dem
unveränderten Board „nichts zu schreiben“ (fand vorher den Stub-Argument-Fall im Prozess); TrackDeps umgeschaltet ⇒ genau eine Änderung.

**Entscheidung Pipeline-Form:** zwei Formen, nach der Ausgabemenge — **keine** Ausgabe ⇒ `Task` (die geschlossene leere Menge, nur
Effekte; genutzt von `BenchmarkPipeline` und `ImageProcessingPipeline.Handle(PaarNichtKomplett)`; ein Verbot erzwänge einen nie
ausgegebenen Scheintyp im OneOf); **eine oder mehrere** ⇒ `IAsyncEnumerable<OneOf<…>>`/`IEnumerable<OneOf<…>>`, auch bei einem Ausgang.
`IAsyncEnumerable<X>` ohne OneOf ist keine gebrauchte Form, sondern ein Vertragsloch: CQRS050 lässt sie zu, der `PipelineDispatchGenerator`
behandelt sie still als `Task` (Fehler erst im Generat) ⇒ wird ein Build-Fehler (offene Aufgabe), KEINE zweite Form im Generator. Der
Scaffolder hält die Regel ein (neuer Handle ohne Ausgang ⇒ `Task`; `Task` + Ausgang ⇒ Strom; alle Ausgänge gelöst ⇒ `Task`).

