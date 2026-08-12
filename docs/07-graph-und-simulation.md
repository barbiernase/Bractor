# 07 — Wissensgraph-Extractor & Live-Simulation

Ein dreigliedriges Werkzeug macht das System als traversierbaren Graph sichtbar und kann es
über eine Mini-Runtime **echt** simulieren. Reine Analyse-/Read-Werkzeuge, kein Teil des
Produktionspfads.

```
GraphExtractor (Roslyn, offline)  ──►  knowledge-graph.json  +  knowledge-graph.html
                                                                       │
                                                          statisch:    │  HTML direkt öffnen
                                                          live:         └─►  SimHost (:5178) serviert dieselbe HTML,
                                                                              reichert sie über /api/* mit echter Ausführung an
ProjectScanner  ──►  PROJECT_STRUCTURE.md   (unabhängiges, älteres Struktur-Dump-Tool)
```

## 7.1 GraphExtractor — was er tut

Ein Top-Level-Programm (`GraphExtractor/Program.cs`) in 5 Phasen:
1. `MSBuildWorkspace.OpenSolutionAsync` lädt die **echte Solution** (Zielprojekte fest:
   `Abstractions, Core, Domain, Domain.Projections, Domain.Pipeline, Infrastructure` —
   Infrastructure bewusst dabei, damit das Generat `GeneratedCommandRouting` als Syntaxbaum
   sichtbar wird).
2. `RoutingTruth.FromCompilations` — autoritative Routing-Wahrheit.
3. `DomainExtractor.Extract` — restliches Domänenmodell.
4. `GraphBuilder.Build` — Property-Graph + Sichten.
5. Serialisiert `knowledge-graph.json` + rendert `knowledge-graph.html`.

Er liest **Roslyn-Symbole**, nicht Text. Weil Symbole aus verschiedenen Compilations nie
referenzgleich sind, vergleicht `Sym.Implements` über voll-qualifizierte **String-Namen**
(`Symbols.cs`) — dasselbe Idiom wie die Multi-Compilation-Generatoren des Projekts.

### Die Routing-Wahrheit (`RoutingTruth.cs`) — der Kern-Neuwert
Der Extractor **parst das Laufzeit-Generat `GeneratedCommandRouting`** statt die Decider erneut
zu interpretieren — Kopplung an genau die Abbildung, die zur Laufzeit routet. Er holt das
Symbol, nimmt dessen generierten Syntaxbaum + `SemanticModel`, iteriert über die
`[typeof(X)] = …`-Zuweisungen der Dictionaries `CommandToAggregate`/`CommandToEvents` und löst
`typeof`-Ausdrücke über das Semantik-Modell auf (robust gegen Aliase).

**Fallback** (`DeciderFallback.cs`): ist das Generat nicht gebaut, leitet der Extractor dieselbe
Map aus den `IDecider<T>.Decide(TCommand)`-Signaturen ab und markiert `Source =
"decider-fallback"`. So bleibt der Graph auch ohne gebaute Infrastructure vollständig — mit
ehrlicher Provenienz.

### Domänen-Extraktion inkl. Saga-DSL-Walk (`DomainExtractor.cs`)
Extrahiert semantisch über Marker-Interfaces (`IState`, `ICommand`, `IEvent`, `ISubscriber`,
`IReader<>`, `IPipelineHandler`, `IProzessDefinition`, …):
- **Aggregate**: Zustands-Properties, behandelte Commands, plus **Ablehnungen** (die transienten
  OneOf-Ausgänge — „der Sad-Path, den GeneratedCommandRouting bewusst auslässt").
- **Sagas**: echter **DSL-Walk über Syntax** — jede Statement im Definiere-Lambda ist eine
  Transition; die Fluent-Verben (`Auf/Und` → Bedingungs-Events, `UndAlle` → Count-Join, `Sende`
  → Command, `SendeJe` → Fan-out, `RückgängigDurch` → Kompensation) werden in Span-Reihenfolge
  gelesen. Join-Klassifikation: count / and / single.
- **Registrierte Prozesse**: parst `GeneratedProzessRegeln` auf die String-Schlüssel → wer
  wirklich verdrahtet ist (definiert-aber-nicht-registriert → Diagnose).
- **Projektionen/Reader/Pipelines**: emittierte Commands per Syntax-Walk über `new`-Ausdrücke.

### Graph-Assembler (`GraphBuilder.cs`)
Baut den `KnowledgeGraph` mit präfix-getypten stabilen Ids (`cmd:`, `evt:`, `agg:`, `proc:`,
`proj:`, `query:`, `pipe:`). Kanten nach Provenienz: `routedTo`/`produces(persisted)`
autoritativ aus RoutingTruth; `produces(rejection)` aus den Decidern;
`triggers/advances/sends/compensates` aus dem Prozess-DSL; `consumedBy/readsFrom/pipelineEmits`.
Anreicherungen: Command-Origin (process/pipeline/client), Prozess-Pattern (Fan-out/Verkettung/
Diamant/Linear), **Bounded Contexts** (fachlich statt Namespace, damit cross-context-Kanten
sichtbar werden).

Abgeleitete Sichten: **EventFanout** (Blast-Radius je Event), **CausalChains** (lesbare
Saga-Pfade), **Diagnostics** — u.a. `UNROUTED-SAGA-CMD` (**error**: Saga sendet Command, den
kein Aggregat behandelt → Runtime-Hang), `PROCESS-NOT-REGISTERED`, `DANGLING-EVENT`.

## 7.2 Output

**`knowledge-graph.json`** (`GraphModel.cs`): vier Keys `meta`, `nodes`, `edges`, `views`.
Realer Stand: **138 Nodes / 169 Edges**, `routingSource: "GeneratedCommandRouting"` (autoritativ
aktiv), 16 Aggregate, 38 Commands, 64 Events, 6 Prozesse, 3 Projektionen, 8 Queries, 3
Pipelines, 21 Contexts. Jeder Node trägt genau eine kind-spezifische Payload; jede Edge trägt
`provenance`. Bewusst **traversierbar** („was passiert, wenn Command X feuert?" = Kantenpfad).

**`knowledge-graph.html`** — das interaktive **Event-Modeling-Board** (`HtmlPresenter.cs`): ein
self-contained C#-Template-String mit eingebettetem JSON (kein Build, kein CDN). Kann:
- verschachteltes Layout (Contexts → Aggregate → Funktionen als „Command rein / OneOf-Events
  raus"; Sagas oben, Projektionen unten),
- Pan/Zoom, Hover-Tooltips, Kontext-Filter,
- **Trigger-Simulator (statisch)**: propagiert Token wellenweise durch den Graph (Sagas feuern,
  wenn ihre When-Events aktiv sind) — struktur-basiert, keine Werte,
- **LIVE-Umschaltung**: beim Laden `fetch('/api/schema')`. Antwortet ein Server (SimHost),
  erscheint das `● LIVE`-Badge, Commands bekommen Eingabeformulare, und der Simulator schickt
  echte Werte an `/api/step`.

## 7.3 SimHost — actor-freie Live-Runtime

ASP.NET-Minimal-API auf **`http://localhost:5178`** mit **nur einer Referenz: `Domain`**
(kein Marten, Proto.Actor, Redis, Infrastructure). Vier Routen: `GET /` (HTML),
`GET /api/schema` (Command-Feldschemata), `POST /api/step`, `POST /api/reset`.

Die Engine (`SimEngine.cs`) führt **dieselbe Logik wie der Cluster** aus, single-threaded,
in-memory:
1. Deserialisiert JSON-Werte in den echten Command-Typ.
2. Command-Queue-Schleife: holt den in-memory-State, erzeugt via **echter generierter
   `AggregateHandlerFactory.CreateHandler`** den Handler, ruft `handler.HandleCommand(c)` — der
   **echte generierte Decider** entscheidet **wertabhängig** über den OneOf-Zweig.
3. Trennt persistierte Events (auf State angewandt + Version-Bump) von Ablehnungen.
4. Treibt Sagas voran (`AdvanceSagas`): faltet Markings, matcht **die echten `Regel.Sende`** →
   Folge-Commands landen wieder in der Queue → die ganze Saga-Kaskade läuft wertabhängig durch.

Verifiziert: alle von SimEngine benutzten Verträge (`GeneratedProzessRegeln.Alle`,
`Regel.Sende/Bedingung/Sammel`, `IAggregateHandler.HandleCommand/ApplyEvent`, …) sind exakt die
**Produktions-Verträge**. Die Simulation faked nichts — sie re-hostet die generierte
Domänenlogik. Rückgabe ans Board: `frames` (Trace zum Animieren) + `states` (Aggregat-Snapshots
je Session).

## 7.4 ProjectScanner

Eigenständiges, **älteres** Tool (kein Roslyn): rekursiver Datei-Scan, `.sln` per Regex, `.csproj`
per XDocument → `PROJECT_STRUCTURE.md` (Übersicht, Ordnerbaum, Mermaid-Abhängigkeitsgraph,
Einstiegspunkte). Zweck: „LLM-Kontext". Hat **nichts** mit dem Wissensgraph zu tun — teilt nur
die Solution-Root-Suche als Idee.

## 7.5 Bedienung

- **Graph erzeugen**: `dotnet run --project GraphExtractor [pfad/zur.sln]` → schreibt
  `knowledge-graph.json` + `.html` neben die `.sln`.
- **Statisch ansehen**: `knowledge-graph.html` direkt im Browser öffnen.
- **Live ausführen**: `dotnet run --project SimHost` → `http://localhost:5178/`.
- **Struktur-Doku**: `dotnet run --project ProjectScanner`.

## 7.6 Erkannte Design-Prinzipien

1. **Eine Wahrheit, geparst statt nachgebaut** — der Extractor liest das Laufzeit-Generat.
2. **Ehrliche Provenienz + graceful degradation** — jede Kante trägt `provenance`; fehlt das
   Generat, sichtbar `decider-fallback`.
3. **Simulation ohne Fakes** — SimHost re-hostet die echte generierte Domänenlogik.
4. **Zero-Build-Artefakt** — self-contained HTML, progressive Enhancement zur Live-Runtime.
5. **Board = Event-Modeling-Sprache** — Command rein / Events raus / Sagas oben / Projektionen
   unten.

## 7.7 Reifegrad & Schulden

Konzeptionell reif und konsistent mit den Kernprinzipien; der Dämpfer ist Engineering-Hygiene:
- **Fast alles ungetrackt/neu** (11. Aug): `DeciderFallback`, `DomainExtractor`, `GraphBuilder`,
  `HtmlPresenter`, `RoutingTruth`, `Symbols`, das **ganze SimHost/** und `knowledge-graph.html`
  sind **nicht in git**; `GraphModule.cs` (alter Ansatz) ist gelöscht. Der Umbau ist nicht
  committet.
- **SimHost ist nicht in der `.sln`** — fällt aus dem `dotnet build` heraus, separat zu starten.
- **Reflection in SimEngine** (`ReadAggregateId`, `BumpVersion`) — pragmatisch, brüchig gegen
  Umbenennungen.
- **HtmlPresenter** = ~400-Zeilen-Template-String (JS/CSS als C#-String) — schwer testbar.
- **Fragile Board-Heuristiken** (Feld-Defaults per Regex, deterministische Fake-Guids) — nur für
  Demos.
- **ProjectScanner** ist ein isoliertes Nebengleis (Regex-`.sln`-Parsing, bricht bei `.slnx`).
