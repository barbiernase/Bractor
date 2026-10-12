# Umsetzungsplan: Werkbank ohne Simulationsschicht

> **Status 2026-10-12: in Umsetzung — S0, P0, P1, P2 und P3 erledigt, P4 in Arbeit (siehe §17 Fortschritt).** Er setzt das Zielbild `docs/konzept-werkbank.md` um. Dort Entschiedenes
> (§12.1) wird hier nicht neu verhandelt. Der Plan ergänzt das Zielbild um drei Dinge:
> 1. den **Ist-Befund** aus einer vollständigen Code-Analyse (Editor-JS, Werkbank-Server, Extractor, Laufzeit, Build/Test);
> 2. die **Lücken**, die das Zielbild stillschweigend voraussetzt, die der Code aber nicht hat (§2);
> 3. je Phase **konkrete Arbeitspakete** mit Dateien, Reihenfolge, Tests und Fallen (§5–§14).
>
> Die Entscheidungen in §3 sind mit Empfehlung versehen. Ohne Widerspruch gilt die Empfehlung.
>
> **Vorgaben von Tobi (2026-10-12):** kein Spike (S1 entfällt, E7 ist entschieden), **keine Abwärtskompatibilität** — keine
> Migration von Bestandsdaten, keine Upcasts, keine Rücksicht auf alte Vorgangs-Ids, gespeicherte Layouts oder alte Konfig-Schlüssel.
> Was nicht mehr passt, wird zurückgesetzt bzw. neu angelegt.

---

## 0 · Kurzfassung

- Die neun Phasen des Zielbilds (P0–P8) bleiben. Davor kommt **S0 Grundlinie** (Messwerte, Datei-Split des Editors). Kein Spike:
  Die Gesten-Schleife liest nur aus den Domänen-Compilations (E7); gemessen wird direkt in P7.1.
- **Keine Abwärtskompatibilität:** Bestand wird umgebaut, nicht migriert.
- P7 („Eine Wahrheit“) ist mit Abstand die größte Phase. Sie wird in **vier Stufen** (P7.1–P7.4) geschnitten, jede einzeln grün.
- Ab P3 gilt eine **Bauregel für das Frontend:** Neue Editor-Funktionen bekommen ihre Daten fertig vom Server. Es entsteht kein
  neues Modell-Wissen im JS, das P7 wieder löschen müsste.
- Es gibt **20 Lücken** zwischen Zielbild und Code (§2). Die tragenden sind:
  - Die Proto-/Codegen-Kette kennt nur Konstruktor-Parameter, keine geerbten `required init`-Properties (betrifft `DateiGemeldet`).
  - Laufzeit-Knoten von Flüssen und Prozess-Regeln haben keine Namen, nur Indizes (betrifft die Wirker-Ids).
  - Kestrel spricht nur HTTP/2 (betrifft Einblick/SSE).
  - Der Extractor bricht ohne generierte Routing-Tabelle ab und verliert fehlerhaften Code still (betrifft „Code mit Fehlern lesen“).
  - Die LLM-Prüfung für decide/apply/fluss/saga hängt an `ModellSimulation` (betrifft P8).

---

## 1 · Ist-Befund (verdichtet)

### 1.1 Editor-Frontend (`GraphExtractor/HtmlPresenter.cs`, 5677 Zeilen)

- **Einbettung:** Ein einziger nicht-interpolierter Raw-String (`EditorBlock`, Z. 36–5676): CSS 36–592, HTML 593–618, eine JS-IIFE
  619–5675 mit ~120 Top-Level-Globals. GraphExtractor schreibt `editor.html`; SimHost liest die Datei je Anfrage. **Die JS/CSS-Teile
  lassen sich risikolos als statische Dateien herauslösen**: Es gibt keine C#-Interpolation, nur zwei `.Replace`, davon eins tot.
- **Modell im Browser:** `MODEL` (Z. 626) mit ~25 Sammlungen. `normalize()` (645–707) migriert unter anderem Code-Knoten.
  Persistenz:
  - localStorage plus `board-model.json` (`deSave` 5218);
  - Positionen in `KPOS`;
  - `mergeBoard` (5258–5330) mit `_herkunft`-Hash und „ungeschrieben“/Entwurf.
- **JS-Ableitungen** (~2000 Zeilen Modell-Logik):
  - `flussRegeln` 1667, `akteurMengen` 1694 (Spiegel von `DomainEditor/AkteurAnteile.cs`), `akteurSet` 1711, `akteurVon` 1789;
  - `derivedStores` 1205, `deriveMembership` 2270, `prepareSaga` 2282, Fluss-Logik 2527–2672;
  - `boardEdges` 3061, `groupKeyOf` 3184, `berechneSicht` 4419, `ketteVon`/`sliceVon` 4475, `domKey` 4697;
  - `compatible` 3645, `applyLink` 3664 (~45 Port-Zweige), `loeseLink` 3798, `renameRefs` 2415, `delNode` 3470, `grPruefe` 3895.
- **Schreiben:** Das ganze Modell geht per `POST /api/editor/write` an den Server (`deWrite` 5386). Code-Sync pollt alle 2 s.
- **Simulation:** 5419–5675 plus CSS und Hooks in `render`/`teilNeu`. Sie lässt sich sauber herauslösen.
- **P0-Ziele:**
  - Trigger/Pipeline-Handle/Frist/Dienst/HostSetting-Karten: 1325–1538, 1634–1647, 1982–1993, dazu ~100 verstreute Verweise.
  - LOD: 4638–4689.
  - Palette: 5028–5098, 4987.
  - Achtung: `handleKnoten` (1400–1443) baut auch Projektion/Reader/Reaktion und **bleibt**.
- **Reine View, die bleibt** (~2400 Zeilen JS, ~550 CSS): Karten-Rendering, `packLayout`, Rahmen, `drawEdges`, Minimap/Pan/Culling,
  ⊕-UI, Zuordnungs-Editor, Panel (`zeigeInspector` ist das Panel).
- **Kopplungsrisiko:** `akteurVon`/`domKey` füttern Layout und Rahmen. Der Server muss je Karte `{domäne, akteur, block, rolle}`
  liefern, bevor diese JS-Funktionen fallen.

### 1.2 Werkbank-Server heute (`SimHost/`, `DomainEditor/`)

- **Schreibpfad:**
  - Ganzes Modell → `BoardLeseseite.AusBoard` → `Scaffolder.Generiere` (alle Dateien) → `DateiSchreiber` (additiver Merge) →
    `DateiAbgleich.Alles()` (nur Elemente mit geändertem `Herkunft`-Stempel) → `dotnet build` je Projekt.
  - Roslyn dient nur zum **Finden** von Spannen; geändert wird per Text-Splice.
  - Es gibt kein `Renamer`, keinen `SymbolFinder` und keinen Workspace in SimHost.
- **Wiederverwendbar** (~4400 Zeilen):
  - Scaffolder (Anlegen);
  - die ~550 Zeilen Edit-Primitive in `DateiAbgleich` (RecordFelder, EnumWerte, DecideRueckgabe, ProzessRegeln, FlussKnoten,
    FunktionsSignatur, HandleRueckgabe, AkteurBefugnisse, ClientVertrag …);
  - Grammatik (Daten), Fluss/Module/AkteurAnteile (Server-Ableitungen);
  - PythonQuelle/PythonSync;
  - CodeSync (ohne den Prompt-Pfad);
  - LlmKonsole (~580 von 679 Zeilen).
- **Entfällt** (~1700 Zeilen): ModellSimulation, Musterwerte, Herkunft, BoardLeseseite, Validator, die Board-/Sim-/Validate-/
  Compile-Endpunkte.
- **Bauen:**
  - `ProjektPruefung` kompiliert in-memory **ohne Generatoren**, nur mit `StoreZugriffAnalyzer`, und nur für `store`-Slots.
  - Echte Builds laufen als Prozess (`dotnet build -v q`); Fehler werden als Text gekratzt (`": error"`), ohne Datei:Zeile-Struktur.
- **Prüfen:**
  - Validator-Regeln sind größtenteils schon Compiler-/Analyzer-Fakten (CQRS002/010/050/051/053/056/057/060–065/068/069).
  - Echt offen (Analyzer-Backlog):
    - GR-FLUSS-QUELLE/-WARTE/-ZEITLIMIT (heute nur Laufzeit im `PipelineBauer`);
    - GR-FLUSS-DRAHT („Fall außerhalb der Ausgänge“);
    - GR-QUERY, GR-GARANTIE, GR-ZUSTAND, GR-FALTUNG, GR-MODUL-AUSGANG-OFFEN, GR-GETRAGEN;
    - GR-ZYKLUS außerhalb von Prozessen.
  - Trigger-Regeln (GR-TRIGGER, GR-SELBST …) entfallen mit P0.

### 1.3 Extractor (`GraphExtractor/`)

- **Laden:** `MSBuildWorkspace.OpenSolutionAsync` über die ganze `.sln` in einem **kalten CLI-Prozess** (laut UI ≈ 1 min; gemessen
  nie). Es gibt keinen inkrementellen Pfad. `Sym.AllConcreteTypes` läuft über alle Metadaten inklusive BCL. `Projektlage` hält einen
  statischen, wachsenden `HashSet` (für einen Dauer-Server ungeeignet).
- **Datenfluss:** DomainExtractor → `DomainModel` → `ModellMapper.ZuEditorModell` → `ZuBoardJson`. Der Wissensgraph
  (`GraphBuilder`) speist nur Diagnosen und LLM-Kontexte.
- **Ids:** Drei Schemata nebeneinander, keines ist das Wirker-Format:
  - Graph `agg:`/`cmd:` mit `#n` bei Kollision (kollidiert mit `#` in `fluss:`/`prozess:`);
  - Board mit natürlichen Schlüsseln, aber **positionsabhängig** auf der Leseseite (`pj1`, `rk1`, `rd1`, `st1`, `wf_i_j` …);
  - `DomainEditor/Fluss.cs` mit eigenen Präfixen.
- **Code mit Fehlern:**
  - `Projektlage.cs:59` **wirft**, wenn die generierte Routing-Tabelle fehlt. Damit ist `DeciderFallback` praktisch tot.
  - Unaufgelöste Basistypen lassen Typen **still verschwinden**.
  - `FlussRaw.Unbekannt` wird nie gemeldet.
  - `ToDictionary` über einfache Namen wirft bei halb editierten Duplikaten (`ModellMapper.cs:369–378`).
- **Typen ohne Karte heute** (~10–12, die künftigen Code-Karten): `SplitZuteiler`, `ImagePairFileName` (+ verschachtelte Typen),
  `DateiQuelle`, `IHistogramEqualizer`, `IImageResizer`, `HistogramEqualizer`, `ImageResizer`, `Zielpfad`,
  `DomainPipelineExtensions`, `MartenCoCommitStoreBase`.
- **Parität/Sonde:**
  - Die Inventar-Zählung (`SyntaxInventar`) überlebt als Grundlage für „Code ohne Karte“.
  - Board⇄Modell, Fixpunkt und die gezeichnete Sonde hängen an Board-JSON und `BoardLeseseite`.

### 1.4 Laufzeit (Programmiermodell)

- **Quellen:**
  - `QuellEingang.NimmAufAsync` schreibt genau einmal (`StartStream`, Stream =
    `MD5("quelle:"+FullName+":"+Kennung)`) und weckt je Pipeline den Dirigenten.
  - `QuellenDienst` läuft auf **jedem** Knoten.
  - `DateiQuelle` legt das Verzeichnis an, schreibt also, und verletzt damit „Treiber liest nur“.
  - `PreprocessingConfig` leitet ihren Pfad aus `WatchPath` ab.
  - `BenchPing : IQuellNachricht` hat keinen Treiber.
  - Eine gebündelte „Streams existieren?“-Abfrage gibt es nicht.
- **Host:**
  - `Host.Grpc/Program.cs` liest Konfig nur per `GetValue`.
  - **Kestrel ist HTTP/2-only.**
  - Der Cluster ist **fest auf Consul** verdrahtet (`Proto.Cluster.TestProvider` ist referenziert, aber unbenutzt).
  - Redis-Schlüssel haben **kein Präfix** (Konstanten in `RedisVersionTracker`, `ReadModelDepsWriter`).
  - `AddFunktion` steht als Code in `Program.cs:132–140`.
  - Die Trigger-Welt wird in keiner Domäne mehr benutzt. P0 ist reiner Editor-Rückbau.
- **Header:**
  - Der Akteur reist als `AsyncLocal` (`ImAuftrag`). Diesem Muster folgt `ursache`.
  - Stempelstellen:
    - `CommandEmitter.SendeAsync` (Z. 86) und `AggregateActorBase` (355/463/485) → `AppendEventsAsync`;
    - Header-Schreiber `MartenEventStore.cs:84` und `MartenEventBatchWriter.cs:87`;
    - Durchreichen in `BatchingEventAppender`.
  - Zurückgelesen werden nur `aggregate_type`/`akteur`.
  - Auftragswege (`AuftragAnbieten`, Proto `ArbeitsAuftrag`) tragen keine Ursache.
- **Uhr:**
  - `MartenDbClock` = `select now()`.
  - Das Warte-Zeitlimit vergleicht Event-Zeitstempel (Echtzeit) gegen `bereit+limit`. Die Entscheidung „abgelaufen“ ist durabel
    (`SchrittUmgeleitet`), ein Versatz ist also konsistent.
  - `DbNowAsync` (Nachzügler-Gnade) muss Echtzeit bleiben.
- **Funktionen:**
  - Der Vermittler kennt keine Liste offener Aufträge.
  - `PrüfeBindungen` verbietet „extern + Lese-Fähigkeit“, Hand muss dort erlaubt werden.
  - Ein Hand-Ausführer kann das Vermittler-Protokoll wie `FunktionsAbholer` nutzen.
- **Knoten-Identität zur Laufzeit:**
  - `PipelineKnoten.Index`/`Regel.Knoten` sind Deklarations-Indizes; Prozess-Regeln haben nur `RegelIndex`.
  - Variablennamen kennt nur Roslyn.
  - Der Extractor benennt anonyme Knoten selbst (`FlussLeser.cs:87`).
- **Hashes:**
  - Der Snapshot-Hash deckt nur die State-Form (Property-Namen und -Typen), keine Applier-Logik.
  - `ProjectionRebuilder` existiert, ist aber nirgends verdrahtet.
  - Einen Projektions-Code-Hash gibt es nicht.
- **Codegen:** Die Proto-/DTO-Kette liest an **vier Stellen nur Konstruktor-Parameter**: `Proto.SourceGeneration`
  `MultiCompilationAnalyzer`, `Cqrs.Codegen/ReflectionTypeAnalyzer.cs:114`, `Core.SourceGeneration/DomainGraphAnalyzer.cs:105`,
  `DtoMapperGenerator` (`new T(args)`).
- **Client-Eingang:** Es gibt nur gRPC `Connect` mit domänen-getypten Proto-Payloads. SimHost hat weder gRPC noch Proto.

### 1.5 Build, Test, Betrieb

- Ein normales `dotnet build` regeneriert Proto und STJ-Kontexte über den MSBuild-Prepass in `ProtoRepo.csproj:26–79`;
  `codegen.sh` ist vor allem `--check`.
- Python-Basen entstehen über `Cqrs.Codegen` plus `build_python.sh`.
- Es gibt kein `global.json` und kein `Directory.Build.props`. Ziel ist net9.0, die Generatoren laufen auf netstandard2.0.
- **Integrationstests:**
  - Verbindung hart kodiert auf `cqrs_events`.
  - Cluster-Tests setzen einen zufälligen `ClusterName`.
  - Kein Testcontainers.
- **CI:** `deploy.yml` deployt `main` per `deploy/deploy.sh` (Env `Pipeline__WatchPath`). **Jede Konfig-Umstellung muss `deploy.sh`
  mitziehen.** Ein Test- oder Codegen-Gate gibt es in CI nicht.
- **Doku-Drift:** `docs/12` nennt nicht mehr existierende Tests, und `LoadHarness/` fehlt, obwohl CLAUDE.md es nennt.

---

## 2 · Lücken zwischen Zielbild und Code

| # | Lücke | Wo das Zielbild sie voraussetzt | Behandlung (Phase) |
|---|---|---|---|
| L1 | Proto/Codegen/DtoMapper kennen nur Ctor-Parameter; `new DateiErkannt()` ohne `required`-Member kompiliert nicht | §3.2/§3.3 | P1.4: alle vier Analysatoren um geerbte `required init`-Properties erweitern, DtoMapper per Objekt-Initialisierer |
| L2 | Extractor `Felder()` liest nur deklarierte Syntax, geerbte Properties ergeben 0 Felder | §3, P1 Extractor | P1.8: Felder über Symbole inklusive Basistyp |
| L3 | Bestands-JSON von `DateiErkannt` hat alte Feldnamen | §3.6 | **entfällt** (keine Abwärtskompatibilität): alte Daten werden nicht gelesen; Prod-/Sim-DB bei Bedarf neu |
| L4 | `PreprocessingConfig` hängt an `WatchPath` | §3.6 (nicht erwähnt) | **E2**: Konfig der Funktions-Implementierung unter `Funktionen:<F>:Konfig` |
| L5 | `BenchPing : IQuellNachricht` ohne Treiber würde von CQRS070 getroffen | §3.2 | **E3** |
| L6 | `DateiQuelle` legt Verzeichnisse an | §3.1 „liest nur“ | P1: Der neue Treiber legt nichts an; fehlendes Verzeichnis = Warnung |
| L7 | Keine gebündelte Bekannt-Abfrage; `IEventStoreRepository` hat 4 Test-Fakes | §3.5 | P1.2: eigene schmale Fähigkeit `IStreamBestand` statt Repository-Erweiterung |
| L8 | Kestrel ist HTTP/2-only; SSE und Einblick brauchen HTTP/1.1 | §7.2 | **E6**: zweiter Endpunkt `Transport:EinblickPort` (HTTP/1.1) |
| L9 | Cluster fest auf Consul | §4.1 „Einzelknoten“ | P2.4: Einzelknoten über `Proto.Cluster.TestProvider` |
| L10 | Redis ohne Schlüssel-Präfix | §4.3 | P2.5: Präfix-Dienst in `RedisVersionTracker`/`ReadModelDepsWriter` |
| L11 | Fluss-Knoten und Prozess-Regeln haben zur Laufzeit nur Indizes | §7.1 Wirker-Ids | P4.1: generierte Index→Name-Tabelle; Benennung **E4** |
| L12 | Extractor wirft ohne Routing-Generat; Fehlerhaftes verschwindet still | §8.2 „Code mit Fehlern“ | P7.1 |
| L13 | Kein Einblick in offene Aufträge (Vermittler) | §6, §7.2 `/einblick/auftraege` | P4.7: Vermittler-Nachricht `ListeOffene` |
| L14 | Snapshot-Hash ohne Applier-Logik; Projektionen ohne Code-Hash; `ProjectionRebuilder` unverdrahtet | §4.6 | P3.4: generierte Code-Hashes (**E9**) plus Rebuild beim Start |
| L15 | „Event-Typ inkompatibel geändert“ ist nicht erkennbar | §4.6 | P3.4: Vergleich des generierten `domain.proto` (Feldnummern/Typen) gegen den Stand der Sim-DB |
| L16 | `stand` braucht einen Build-Hash, den Werkbank und Host gleich rechnen | §7.1/§7.2 | P4.3: MSBuild-Property `CqrsStand` → generierte Konstante; dieselbe Hash-Funktion im Werkbank-Server |
| L17 | Editor als Client: SimHost hat kein gRPC/Proto, Payloads sind domänen-getypt | §6 | **E5**: JSON-Eingang am Host hinter dem Akteur-Tor |
| L18 | LLM-Prüfung für decide/apply/fluss/saga läuft über `ModellSimulation` | §10 P8 | P7.4 vor P8: Prüfung über den warmen Workspace |
| L19 | Geste < 300 ms mit v1-`ISourceGenerator`s im Infrastructure-Projekt fraglich | §8.2 | **E7** (entschieden), Messung in P7.1 |
| L20 | Sonde und Parität lesen Board-JSON und `BoardLeseseite` | §9, P7 | P7.3: auf die Server-Kartenliste umstellen; „gezeichnet“ = Gesten-Skript |

---

## 3 · Entscheidungen mit Empfehlung

| # | Frage | Empfehlung | Begründung |
|---|---|---|---|
| **E1** | Bestandsdaten `DateiErkannt` | **Entschieden: keine Abwärtskompatibilität.** Kein Upcast, kein `_v1`. `DateiErkannt` nutzt die Default-Kennung `PfadGroesseZeit`; alte Vorgänge werden nicht weiter verarbeitet | Vorgabe Tobi |
| **E2** | Wohin mit der Konfig einer Funktions-Implementierung (`PreprocessingConfig`) | Abschnitt `Funktionen:<F>` = `{ Laufort, Slots, Wiederholungen, Konfig: {…} }`. `Konfig` wird an den Konfig-Record im Ctor der Impl gebunden (generiert) | Struktur ist Code (der Record), Werte sind Konfig. Eine Stelle je Funktion |
| **E3** | `BenchPing` und künftige Quellen ohne Treiber | CQRS070 gilt nur für Typen, die von einer Treiber-Basis erben. Ein `IQuellNachricht` ohne Treiber-Basis bleibt erlaubt (Webhook-/Test-Quelle); der Extractor zeigt sie mit Kopf „⇥ ohne Treiber“ | Kein Zwangsumbau des Benchmarks; die Takt-Quelle (§12.2) löst es später |
| **E4** | Name einer Prozess-Regel in `prozess:<P>#<Regel>` | `<Auslöser>[+<Und>…]→<Ziel>` aus den Typ-Argumenten, z. B. `TeilFertig→MeldeFertig`. Eine Kollision ist ein Build-Fehler (neue Regel CQRS072) statt eines Suffixes | Index ist reihenfolge-abhängig. Typnamen sind Code-Fakten und stabil. Im Bestand gibt es nur einen Prozess (`TeilFertigProzess`) |
| **E5** | Wie handelt der Editor als Akteur | Der Host bekommt einen **JSON-Eingang** (`POST /eingang/command`, `/eingang/query`, Token-Header `akteur-token`), Typauflösung über die generierte Typ-Registry, derselbe `AkteurTor`. Schaltbar per Umgebung (`Transport:JsonEingang`: sim an, prod aus) | Echter Weg (Tor → Aggregat/Reader) ohne Proto im Werkbank-Server; Leitsatz 4 (nur Konfig unterscheidet). gRPC bleibt der Weg der echten Clients |
| **E6** | HTTP/1.1 für Einblick, SSE, Webhook | Zweiter Kestrel-Endpunkt `Transport:EinblickPort` mit `Http1AndHttp2`. Einblick, SSE, JSON-Eingang, Webhook und `/health` hängen dort | gRPC-Port bleibt unverändert; Webhook und `/health` sind heute nur mit h2c erreichbar |
| **E7** | Woraus liest die Gesten-Schleife | **Entschieden:** nur aus den **Domänen-Compilations** (inkrementelle Domain-Generatoren). Routing aus den Decide-Signaturen (`DeciderFallback` wird Primärweg der Werkbank); `GeneratedCommandRouting` ist Gegenprobe in ⟳ und `--check` | Die Infrastructure-Compilation mit v1-Generatoren ist für < 300 ms zu schwer. Reicht das nicht: inkrementeller Extractor je Dokument |
| **E8** | Editor-JS/CSS aus dem C#-String | In S0 als statische Dateien nach `SimHost/wwwroot/editor/` (`editor.css`, `editor.js`), zunächst eine Datei je Art, keine Modularisierung | Jede spätere Phase ändert das JS; ohne C#-Rebuild, mit echten Zeilennummern im Browser |
| **E9** | Code-Hash für Snapshots und Projektionen | Generator hasht normalisierte Syntax: Aggregat = State + Applier; Projektion = Klasse + benutzte Store-Fähigkeiten + Impl. Snapshot verwirft bei Abweichung (wie `SchemaVersion`); Projektion setzt beim Start zurück (`IProjectionTracker`-Reset, `ProjectionRebuilder` verdrahten) | Erfüllt §4.6 ohne Laufzeit-Reflection; Rebuild nur in Umgebungsart `sim` automatisch, in `prod` nur Meldung |
| **E10** | Sandbox-Pfad und Python-Interpreter je Umgebung | In der Umgebungs-Datei: `Quellen:<Q>:Pfad` (Sandbox-Default `~/cqrs-sim/<branch>/<Quelle>/`) und `Funktionen:<F>:Worker` (`Python`, `Skript`) | Werkbank startet Worker (P5) ohne Wissen im Code |

Fragen, die der Plan bewusst **nicht** stellt: alles aus Zielbild §12.1.

---

## 4 · Reihenfolge und Wellen

```
S0 Grundlinie ──► P0 Aufräumen ────────────────────────────────┐
             ├──► P1 Datei-Treiber ───────────────────────────┤
             ├──► P2 Umgebungen ──► P3 Werkbank-Server ──┬──► P5 Durchspielen ──► P6 Visualisierung ──┐
             │                  └─► P4 Spur ─────────────┘                                              ├──► P8 Simulation raus
             └─────────────────────────────────────────► P7.1 … P7.4 Eine Wahrheit (ab P3) ──────────┘
```

**Empfohlene Abarbeitung** (ein Strang):
S0 → P0 → P1 → P2 → P3 → P4 → P5 → P6 → P7.1 → P7.2 → P7.3 → P7.4 → P8.

- P1 vor P2, weil P2 den Quellen-Abschnitt nur noch umziehen muss.
- Jede Phase endet mit:
  - Prüfstand grün;
  - Integration sequentiell grün, wo Laufzeit betroffen ist;
  - `--check` und `--sonde` grün;
  - bei Editor-Änderungen ein Beleg im Browser;
  - Doku nachgezogen (CLAUDE.md-Stand, betroffene `docs/0x`);
  - ein Commit je Phase bzw. Stufe (ohne `Co-Authored-By`).

**Frontend-Bauregel ab P3:** Neue Ansichten (Host-Rahmen, Konsole, Aufträge, Spur, Vorgänge) bekommen ihre Daten fertig vom Server
(Schema, Felder, Karten-Ids, Kanten). Kein neues Ableiten im JS.

---

## 5 · S0 — Grundlinie

| Paket | Inhalt |
|---|---|
| S0.1 | Messen und festhalten: Prüfstand, Integration sequentiell (gegen `deploy-linux` Infra), `--check`, `--sonde`, Dauer von GraphExtractor und `dotnet build Host.Grpc` (Stopwatch in `GraphExtractor/Program.cs`, Ausgabe je Schritt). Ergebnis in `docs/12` §12.x „Grundlinie 2026-10“ (veraltete Testliste dort korrigieren) |
| S0.2 | **E8:** `EditorBlock` in `SimHost/wwwroot/editor/editor.css` + `editor.js` verschieben; `HtmlPresenter.EditorPage()` liefert nur noch die Hülle mit `<link>`/`<script>`. SimHost `UseStaticFiles` (no-store). Byte-gleiches Verhalten, im Browser belegt |
| S0.3 | Offene Kleinigkeit: `LoadHarness` existiert nicht mehr. CLAUDE.md und `deploy-multinode` (Profil `verify`) korrigieren oder Herkunft klären |

Abnahme: Editor lädt unverändert aus den statischen Dateien. Messwerte stehen in `docs/12`.

---

## 6 · P0 — Aufräumen (Editor ohne Trigger-Welt, ohne LOD, Palette mit Ankern)

| Paket | Inhalt | Dateien |
|---|---|---|
| P0.1 | Server liefert keine Trigger-Welt mehr: `ZuBoardJson` ohne `triggers`, Handle-`pipelines`, `frists`, `dienste`, `hostSettings`. `MapQuellWebhook` bleibt als Bindung an der Quellkarte | `GraphExtractor/ModellMapper.cs` (545–612), `CompositionRoot.cs` (nur noch Ingress der Quellen) |
| P0.2 | JS: Karten entfernen: `triggerCard`/`trigMsgLabel`, `pipelineCard`, `plHandleBody`, `fristCard`, `dienstCard`, `hostSettingCard`. Dazu die verstreuten Verweise: `normalize`, `uniq`, `neuerKnoten`, `flussRegeln`, `akteurSet`-Fälle, `graphNodes`, `boardEdges`, `ROLE_*`, `pipelineAgg`/`triggerKonsument`, `delNode`, `applyLink`/`loeseLink`-Zweige (`auftrag`, `aus`, `trigmsg`, `self`, `konfig`, `setting`, `dienst`, Frist), Grammatik-Teile, `drawEdges` 4257–4290, `EINHEIT_ARTEN`, `MERGE_KEYS`, `BAU_GRUPPEN`. `handleKnoten` bleibt für Projektion/Reader/Reaktion | `editor.js` |
| P0.3 | LOD raus: `setzeLod`, `pruefeLod`, `baueKarte`, `VIEW.lod`, Aufruf in `applyPan`/`vbStart`/`ansichtLeiste`, CSS 568–576. `zoomAuf`/`passeEin` bleiben | `editor.js`, `editor.css` |
| P0.4 | Palette auf Anker: Aggregat · Fluss · Funktion · Quelle · Akteur · Client · Projektion (Toolbar, `showPicker`, `bausteinMenue`). „Quelle“ legt vorerst einen `IQuellNachricht`-Record an; ab P1 einen Treiber-Typ | `editor.js` |
| P0.5 | `Grammatik.cs`: Trigger-/Selbst-/Frist-Regeln als „entfällt mit Trigger-Welt“ markieren (nicht löschen, solange `--grammatik` sie listet); Validator-Zweige dazu deaktivieren | `DomainEditor/Grammatik.cs`, `Validator.cs` |
| P0.6 | Parität: Die Trigger-Welt zählt als „nicht im Editor (bewusst)“ mit Anzahl, nicht als Fehler | `GraphExtractor/ParitaetsPruefung.cs` |

Abnahme (Zielbild):
- Der Editor lädt den Bestand ohne diese Karten.
- `--check` ist grün.
- Ein Diff der Kartenliste vorher/nachher zeigt nur Trigger-Welt-Einträge.

---

## 7 · P1 — Datei-Treiber

Reihenfolge innerhalb der Phase: erst die Codegen-Kette (P1.4), weil alles andere daran baut.

| Paket | Inhalt | Dateien |
|---|---|---|
| P1.1 | `Abstractions/Quellen/DateiGemeldet.cs` (abstract record, `required init` wie §3.2). `Abstractions/Quellen/TreiberKatalog.cs`: die feste Zuordnung Basis → Treiber-Art (eine Zeile `Datei`), netstandard2.0-tauglich, in die Generator-Projekte verlinkt (Vorbild: `Aggregatvertrag.cs`) | neu |
| P1.2 | Fähigkeit `IStreamBestand.BekannteAsync(IReadOnlyList<Guid>) → ISet<Guid>`; Marten-Impl `select id from <schema>.mt_streams where id = any(@ids)` in Blöcken à 500 | `Abstractions`, `Infrastructure/Persistence/` |
| P1.3 | Store-freier Kern `Infrastructure/Quellen/Treiber/Datei/DateiScan.cs`: Muster, Unterordner, Kennungsbildung (`NameGroesse`, `PfadGroesseZeit`), Fertig-Erkennung (`Stabil` mit Fenster, `Marker` mit Endung), Wartezone, begrenzte „schon gemeldet“-Menge, Bekannt-Abgleich über `Func<…, Task<ISet<Guid>>>`. Treiber-Schleife `DateiTreiber.cs`: Start-Abgleich, Weckruf (Intervall bzw. FileSystemWatcher bei `EreignisUndAbfrage`), Melden nacheinander über `QuellEingang` (Rückstau), Zustand „gestört“ mit wachsender Pause, `IHealthCheck`. **Legt nichts an, schreibt nichts** | neu |
| P1.4 | Codegen-Kette für geerbte `required init`-Properties: `Core.SourceGeneration/DomainGraphAnalyzer.cs` (Properties ohne Ctor), `DtoMapperGenerator.cs` (Objekt-Initialisierer statt `new T(args)`), `Proto.SourceGeneration/MultiCompilationAnalyzer.cs`, `Cqrs.Codegen/ReflectionTypeAnalyzer.cs` + Python-Emitter. Prüfstand: Byte-Round-trip-Test für einen Record mit geerbten `required init`-Feldern (Muster aus dem DtoMapper-Test) | bestehend |
| P1.5 | `QuellenGenerator` (Infrastructure.SourceGeneration, symbol-basiert): findet `sealed record X : <Treiber-Basis>` in referenzierten Domänen-Assemblies und erzeugt `GeneratedQuellen.Alle` (Typ, Treiber-Art, Abschnitt = Typname, Fabrik `daten => new X { … }`, Wirker-Id `quelle:<Typ>`) | neu |
| P1.6 | Konfig `DateiQuellKonfig` (Record mit Enums `Erkennung`, `FertigArt`, `KennungArt`, `Laufmodus`) und Bindung per `EnableConfigurationBindingGenerator`; unbekannte Felder sind ein Fehler (`ErrorOnUnknownConfiguration`; mit dem Source-Generator verifizieren, sonst STJ mit `JsonUnmappedMemberHandling.Disallow`). Start-Prüfungen nach §3.4 | `Infrastructure/Quellen/` |
| P1.7 | Laufmodus: `EinmalImCluster` = Cluster-Identität `quelle/<Typ>` (Actor hält die Schleife, `QuellenDienst` je Knoten pingt sie periodisch an, damit sie nach Knotenausfall wieder aktiviert wird); `JederKnoten` = lokaler Hosted Service. `AddQuelle<,>` entfällt; `QuellenDienst` liest `GeneratedQuellen` + Konfig | `Infrastructure/Quellen/Quellen.cs` |
| P1.8 | Analyzer (Domain.SourceGeneration): **CQRS070** Quellen-Typ = `sealed record`, Treiber-Basis, keine eigenen Member; **CQRS071** `IQuelle<T>` nur in Infrastructure. **E3** beachten | neu |
| P1.9 | Bestand umziehen (§3.6): `DateiErkannt : DateiGemeldet`; Lambdas in `Bildeingang.cs` (`Dateiname`→`Name`, `DateigroesseBytes`→`Groesse`); kein Upcast (E1); `DateiQuelle.cs`, `FileWatchConfig.cs`, FileWatch-Teil in `DomainPipelineExtensions.cs` löschen; **E2** für `PreprocessingConfig` (bis P2: eigener Schlüssel `Funktionen:IBildVerkleinerung:Konfig:Ausgabe`); `Host.Grpc`: `AddQuelle` raus, Abschnitt `Quellen:DateiErkannt` in `appsettings.json`; `deploy/deploy.sh` + `deploy/cqrs-grpc.env` + `deploy-multinode` auf `Quellen__DateiErkannt__Pfad` umstellen, Kennung `PfadGroesseZeit` (Default) | Domain, Domain.Pipeline, Host.Grpc, deploy |
| P1.10 | Extractor: Quellen-Typ → Quellkarte (Kopf „📁 Datei-Treiber“, Felder aus Symbolen inkl. Basis, L2), Werte im Panel (P1: aus `appsettings.json`; P2: aus der Umgebung); `DomainPipelineExtensions` u. a. bleiben bis P7 kartenlos. Sonde: `Sonde/Quelle.cs.txt` + Zeilen in `soll.txt` + Inventar-Bucket | `GraphExtractor/` |

Tests:
- **Prüfstand:**
  - Kennungsbildung beider Modi.
  - Fertig-Erkennung (`Stabil`/`Marker`) über eine Uhr- und Dateisystem-Abstraktion im Kern.
  - Bekannt-Abgleich (store-frei über die Funktion).
  - Analyzer CQRS070/071.
- **Integration:**
  - Eine Datei im Temp-Verzeichnis ergibt genau einen Vorgang.
  - Neustart ergibt keinen zweiten.
  - Eine Marker-Datei wird erst mit `.done` gemeldet.
  - Ein nicht lesbares Verzeichnis ergibt `/health` „gestört“ und danach Erholung.
  - `PipelineFlussE2ETests` bleibt grün (`Verdrahte()` auf Konfig statt `watchPath`).

Fallen:
- Das Python-DTO (`domain_client/generated/__init__.py`) ändert sich mit, also `build_python.sh` laufen lassen.
- Die geerbten Properties müssen in **allen vier** Analysatoren gleich geordnet sein (Proto-Feldnummern).

---

## 8 · P2 — Umgebungen

| Paket | Inhalt | Dateien |
|---|---|---|
| P2.1 | Records der Umgebung in `Abstractions/Umgebung/` (Abschnitte §4.1 + **E2**, **E6**, **E10**). Lader `Infrastructure/Umgebung/UmgebungLader.cs`: `umgebungen/umgebung.<name>.json`, `${env:X}`-Ersetzung, Umgebungsvariablen überschreiben, strikte Bindung (wie P1.6). Generiertes JSON-Schema `umgebungen/umgebung.schema.json` (aus `Cqrs.Codegen`, Build-Zeit; Grundlage für den Formular-Renderer in P3) | neu |
| P2.2 | `builder.AddCqrs(umgebung)`: kapselt `AddCqrsFramework` (bleibt als untere Schicht für Integrationstests) plus alle generierten Registrierungen, Quellen, Funktionen, Akteure, Monitoring, Deadlines. `Host.Grpc/Program.cs` auf `--umgebung <name>` + diese Zeile schrumpfen. Dateien `umgebung.prod.json` (aus dem heutigen `deploy/`-Stand) und `umgebung.sim-main.json` | `Infrastructure/Extensions/`, `Host.Grpc/` |
| P2.3 | Funktions-Laufort als Konfig: `FunktionsGenerator` erweitern um die Implementierungen je `IFunktion` (symbol-basiert über referenzierte Assemblies) → `GeneratedFunktionsImpls` (Fabrik je Impl). `Funktionen:<F>:Laufort` = `Host` (Impl-Name, falls mehrere), `Extern`, `Hand`. Boot-Prüfung gegen die Tabelle; `PrüfeBindungen`: Hand erlaubt auch mit Lese-Fähigkeit (Hand läuft im Host-Prozess) | `Infrastructure.SourceGeneration/FunktionsGenerator.cs`, `Infrastructure/Funktionen/FunktionsExtensions.cs` |
| P2.4 | Cluster: `Einzelknoten` über `Proto.Cluster.TestProvider`, `Consul` wie heute; Cluster-Name aus der Umgebung | `CqrsServiceExtension.cs:412–484` |
| P2.5 | Redis: `aus` (Version-Index optional, gibt es) / `verwaltet` / `vorhanden`; Präfix-Dienst in `RedisVersionTracker.cs:36` und `ReadModelDepsWriter.cs:43` | Infrastructure |
| P2.6 | Siegel: Tabelle `<schema>.umgebung(name, art)`; der Migrator setzt sie bei leerer DB, jeder Start prüft Name/Art; der Cluster-Name wird mitgeprüft | `Infrastructure/Persistence/` |
| P2.7 | Uhr: Tabelle `<schema>.uhr(versatz interval)`; `MartenDbClock` = `now() + versatz`; bei Siegel `prod` immer 0 (Lesen ignoriert, Schreiben verboten). `DbNowAsync` bleibt Echtzeit | `MartenDbClock.cs` |
| P2.8 | Kestrel: gRPC-Port HTTP/2 wie heute + **E6** Einblick-Port HTTP/1.1; `/health`, Webhook dorthin | `Host.Grpc`, `Infrastructure` |
| P2.9 | Extractor: `CompositionRoot.cs` ersetzen durch Lesen der Umgebungs-Dateien nach Schema (Quellen-Werte, Funktions-Laufort, Identitäten); `MapQuellWebhook` bleibt Code-Fakt | `GraphExtractor/` |
| P2.10 | `deploy.sh`, `cqrs-grpc.env`, `deploy-multinode/docker-compose.yml`, `build.sh` auf `--umgebung prod` umstellen; Host.Blazor unberührt (eigener Client) | deploy |

Tests (Integration, sequentiell):
- Ein Host mit `sim` gegen eine DB mit Siegel `prod` startet nicht.
- Zwei Umgebungen parallel auf einer Maschine stören sich nicht (eigene DB, Präfix, Cluster-Name).
- Ein Versatz verschiebt eine Frist.
- Ein Versatz lässt ein Warte-Zeitlimit ablaufen, und das spätere Ergebnis-Event zählt nicht mehr (`SchrittUmgeleitet` ist endgültig).
- Prüfstand: Lader (`${env:}`, Überschreiben, unbekanntes Feld = Fehler).

Falle: Die Integrationstests bauen Hosts direkt mit `AddCqrsFramework`. Dieser Weg muss erhalten bleiben, sonst ist die ganze Suite
umzubauen.

---

## 9 · P3 — Werkbank-Server

Der Werkbank-Server bleibt vorerst das Projekt `SimHost`; er wird in P8 optional umbenannt. Neue Teile kommen in einen Ordner
`SimHost/Werkbank/`.

| Paket | Inhalt |
|---|---|
| P3.1 | **⟳-Kette** `Kreislauf.cs`: (1) `dotnet build Host.Grpc -c Debug -p:CqrsStand=<hash>` (der Prepass für Proto und STJ läuft im Build mit); (2) `build_python.sh`; (3) Diagnosen strukturiert: MSBuild-Format `datei(zeile,spalte): error CODE: text [proj]` parsen, alternativ `-bl` + Binlog-Reader. Karten-Zuordnung über Datei + Zeilenspanne, die der Extractor je Karte mitliefert (`EditorModell` hat `Datei`, Spannen ergänzen). Ausgabe: `Diagnose(kartenId?, datei, zeile, code, text)` |
| P3.2 | **Infra** `Infra.cs`: aus der Umgebung `cqrs-<umgebung>/docker-compose.yml` generieren (Postgres immer, Redis nur `verwaltet`, Consul nur bei `Consul`), `docker compose up/down/ps` steuern, Zustand an die Kopfzeile. `vorhanden` wird nicht verwaltet |
| P3.3 | **Host-Prozesse** `HostProzess.cs`: `dotnet Host.Grpc.dll --umgebung <name>` aus dem Build-Ausgang starten, bei ⟳ ersetzen (Graceful Stop, dann Start), Logs puffern. **Boot-Guards strukturiert:** Boot-Fehler im Host als eine maschinenlesbare Zeile (`CQRS-BOOT {json}` mit Typ bzw. Wirker-Id) → Karte |
| P3.4 | **Daten prüfen** (§4.6, **E9**, L14/L15): Generator-Hashes (Aggregat, Projektion) und `domain.proto` des letzten Laufs liegen in der Sim-DB (Tabelle `stand`). Vor dem Start vergleichen: Projektion geändert → Reset und Rebuild beim Start (`ProjectionRebuilder` verdrahten); Applier geändert → Snapshots dieses Aggregats verwerfen; Proto-Feld entfernt/umbenannt/Typ geändert → Dialog „Sim-DB zurücksetzen“ |
| P3.5 | **Sim-DB** `SimDatenbank.cs` (Npgsql, nur Art `sim`): Neu/Zurücksetzen (Verbindungen beenden, `DROP`, aus Schema-Vorlage `<db>_vorlage` anlegen, Siegel), Stand speichern (`CREATE DATABASE … TEMPLATE`), Stand laden, Prod-Stand übernehmen (`pg_dump`/`pg_restore` per Container, Siegel auf `sim`). Gespeicherte Stände tragen den Proto-Stand; inkompatible Stände sind ungültig |
| P3.6 | **Kopfzeile + Host-Rahmen** (Frontend-Bauregel): Umgebung ▾, Build-Status mit Stand-Hash, Lauf-Status, Postgres/Redis, ⟳, 📸 (Stand speichern/laden). Der 🖥-Host-Rahmen rechts neben den Client-Rahmen; **ein generischer Formular-Renderer** zeichnet aus `umgebung.schema.json`; Speichern schreibt die Umgebungs-Datei. Knöpfe „Prüfen/Kompilieren/Alle ausführen“ entfallen (§10) |
| P3.7 | Endpunkte `/api/werkbank/…` (umgebung, kreislauf, infra, host, simdb, diagnosen) plus ein SSE-Kanal für Fortschritt |

Abnahme (Browser):
- Ein absichtlicher Compiler-Fehler erscheint an der richtigen Karte.
- Nach der Korrektur läuft der Host nach ⟳ gegen `cqrs_sim_<branch>`.
- Stand speichern/laden dauert Sekunden.
- Ein Boot-Guard-Fehler (z. B. ungebundene Funktion) erscheint an der Funktions-Karte.

**Umgesetzt mit diesen Abweichungen (2026-10-12):**
- **Karten-Zuordnung (P3.1)** über Typ + Member aus dem Syntaxbaum der Datei (`SimHost/Werkbank/Diagnosen.cs`), nicht über Zeilenspannen
  des Extractors. Der Editor löst Typ/Member bzw. die Boot-Karte (`funktion:`/`quelle:`/`host:`) in eine Karte auf; ab P7 über stabile Ids.
- **`-p:CqrsStand`** wird nicht gesetzt — es gibt bis P4 (Spur) keinen Verbraucher.
- **Daten prüfen (P3.4):** der Proto-Stand liegt in der Sim-DB (`<schema>.stand`, Schlüssel `proto`, von der Werkbank nach einem gelungenen
  Start geschrieben) und reist so mit jedem gespeicherten Stand. Die Code-Hashes (**E9**) prüft der **Host** beim Start selbst:
  Snapshot-Version = Struktur-Hash + `{State}AggregateHandler.CodeHash` (State + Applier, ohne Decider); Projektion = `CodeHash` der Klasse +
  `StoreStand` der Store-Klasse (`ProjektionsStandDienst`, vor dem Pull-Pfad). Geändert → in Sim wird die **ganze Leseseite** geleert (alle
  `IReadModel`-Typen; die Store-Basis nimmt seit P3 nur noch `IReadModel`), Marken und Poll-Cursor auf 0, der erste Poll (jetzt nach 2 s statt
  30 s) baut aus dem Log neu auf; in Prod nur eine Meldung. Ganz statt je Projektion, weil Read-Models nicht einer Projektion gehören müssen.
- **Zurücksetzen (P3.5)** = `DROP DATABASE`; der Host legt die DB beim Start leer an und siegelt sie (keine `<db>_vorlage`).
  **Prod-Stand übernehmen** = `pg_dump | pg_restore` in `postgres:16-alpine` (wie die Deployments), danach Siegel/Uhr/Stand weg.
- **Prod-Umgebung:** ⟳ endet nach dem Lesen — gestartet wird Prod über das Deployment, nie als Prozess der Werkbank.
- **Uhr-Karte:** der Versatz steht in der Sim-DB (`<schema>.uhr`), nicht in der Umgebungs-Datei; er wirkt ohne Neustart.
- **Offen:** Nach „Prod-Stand übernehmen" fehlt der Proto-Stand der Herkunft — Unverträglichkeiten zeigen sich erst als
  Deserialisierungsfehler im Host-Log (gefunden mit dem lokalen `cqrs_events`: ein `datei_erkannt` von vor P1). Abhilfe (P4): der Host schreibt
  seinen Proto-Stand beim Start in jeder Umgebung. Consul hat keinen eigenen `Betrieb` (folgt dem Event-Store).

---

## 10 · P4 — Spur

| Paket | Inhalt |
|---|---|
| P4.1 | **Wirker-Format, eine Quelle:** `Abstractions/Spur/WirkerId.cs` (netstandard2.0, in Generatoren und Extractor verlinkt) mit den acht Formen aus §7.1 und der Regel für anonyme Fluss-Knoten (heute `FlussLeser.cs:87`, dorthin umziehen). Prozess-Regel-Namen nach **E4** + CQRS072 |
| P4.2 | **Namen zur Laufzeit:** `ProzessRegelnGenerator` (sieht Syntax) emittiert je Pipeline/Prozess `KnotenNamen[index]` bzw. `RegelNamen[index]`; `ProzessRegeln` bekommt die Tabelle |
| P4.3 | **Stempeln:** `AsyncLocal` `Spur` (Ursache, Wirker) nach dem Muster `ImAuftrag`, gesetzt in: generiertem Pipeline-Dispatch, `HandlerOutputRouter` (Reaktion), `ProzessManager.FeuereAsync`/`BeauftrageAsync` (Ursache = jüngster echter Match-Token, synthetische Versionen wie −2 überspringen), `FunktionsAusfuehrer` (Wirker `funktion:F`, Ursache aus dem Auftrag), `QuellEingang` (`quelle:T`), `CqrsClientService` (`akteur:A`, Ursache aus den vorhandenen Kausal-Feldern). `CommandEnvelope` + `AuftragAnbieten` + Proto `ArbeitsAuftrag` bekommen `Ursache`/`Wirker` (Wire-Kontext und Proto regenerieren). Aggregat: Wirker `decide:<Agg>/<Cmd>` beim Append. Header-Schreiben in `MartenEventStore.cs:84`, `MartenEventBatchWriter.cs:87`, `BatchingEventAppender`. `stand` = generierte Konstante aus MSBuild-Property `CqrsStand` (L16) |
| P4.4 | **Zurücklesen:** `EventEnvelope` mit `Ursache`/`Wirker`/`Stand`; neues `LeseAbSeqAsync(seq, n)` (globale Sequenz inklusive Header) |
| P4.5 | `spur_hinweis` (best-effort: Ablehnung, ⏳, transientes Event, Weckruf) mit Aufbewahrungsfrist; das System liest sie nie |
| P4.6 | **Spur-Projektion** (Framework, nur Art `sim`): Knoten = Log-Einträge, Kanten = `ursache`, je Vorgang (`CorrelationId`), plus Vorgangs-Zustand (läuft · wartet · fertig · gescheitert). Welche Projektion was verarbeitet hat, kommt aus den Tracker-Marken. In `prod`: Index auf den Correlation-Header, Graph bei Bedarf |
| P4.7 | **Einblick-API** auf dem Einblick-Port (E6): `GET /einblick/spur?ab=<seq>`, `/einblick/vorgaenge`, `/einblick/auftraege` (Vermittler-Nachricht `ListeOffene`, L13), SSE `/einblick/neu` („neu bis seq“, ausgelöst vom Appender) |
| P4.8 | `--check`: jede Wirker-Id hat eine Karte und umgekehrt. Dafür braucht der Extractor **stabile Karten-Ids gleich Wirker-Ids**: die positionsabhängigen Ids `pj1`, `rk1`, `rd1`, `st1`, `wf_i_j` … in `ModellMapper` durch natürliche Schlüssel ersetzen. Gespeicherte Positionen gehen dabei verloren (kein Umzug) |

Tests (Integration):
- Ein Bildeingang-Vorgang hat eine lückenlose `ursache`-Kette von der Quelle bis `BildVerfuegbar`, auch über Funktionen und
  Commands hinweg.
- Der Wirker jedes Eintrags ist eine Karte (`--check`).

Prüfstand:
- WirkerId-Format.
- Generator-Namen gleich den Extractor-Namen (beide über `WirkerId.cs`).

Falle: Der `CommandEnvelope` ist eine `IWireMessage`. Ohne `codegen.sh`/Build-Prepass bricht der Wire-Boot-Check.

---

## 11 · P5 — Durchspielen

| Paket | Inhalt |
|---|---|
| P5.1 | **Als Akteur handeln** (E5): JSON-Eingang am Host (`/eingang/command`, `/eingang/query`, Typauflösung über generierte Registry, `AkteurTor` mit Token). Sim-Tokens stehen in `umgebung.sim-*.json` unter `Identitaeten`. Werkbank: Konsole unten, Token/Akteur wählen, Formular aus den Record-Feldern (Feld-Schema liefert der Server), Antwort und Ablehnung anzeigen |
| P5.2 | **Laufort Hand:** `HandAbholer` im Host nach dem Muster `FunktionsAbholer` (Vermittler `HoleArbeit`, `ArbeitLebt`, Ergebnis über `FunktionsAusfuehrer.SchreibeErgebnisAsync`), nach außen über `/eingang/auftraege` (Liste, JSON) und `/eingang/ergebnis` (Fall + Felder, getypt über die Registry). Werkbank: ⧗ n an der Funktions-Karte, Panel mit Ergebnis-Fall und Formular |
| P5.3 | **Datei-Drop** auf die Quellkarte: Der Werkbank-Server legt die Datei in den Sandbox-Pfad der Umgebung (E10); der echte Treiber meldet sie |
| P5.4 | **⏩** in der Kopfzeile: Versatz in der Sim-DB setzen (nur Art `sim`; der Werkbank-Server darf die Sim-DB direkt verwalten, §4.6) |
| P5.5 | **▶ Worker starten** an Funktionen mit `Laufort: Extern`: Python-Prozess nach `Funktionen:<F>:Worker` (E10) starten, Log ins Meldungsfeld |

Abnahme (Browser):
- Ein abgelegtes Bild lässt den Vorgang durchlaufen.
- Eine Funktion ohne Implementierung wird per Hand beantwortet, und der Fluss läuft weiter.
- Ein Command als Akteur macht ein Event sichtbar.
- Ein Python-Worker wird gestartet und bedient seine Funktion.

---

## 12 · P6 — Visualisierung

Alles nur View auf Einblick-Daten (Frontend-Bauregel). Der Werkbank-Server liest per SSE-Signal plus Cursor nach (`/einblick/spur?ab=`)
und hält den Cursor; ein verlorenes Signal oder ein Neustart schadet nicht.

| Paket | Inhalt |
|---|---|
| P6.1 | Untere Leiste: Vorgänge (Filter nach Quelle, Akteur, Aggregat-Id, Zustand), Zeitleiste (Bahnen je Baustein, Schieber = Abspielen der Aufzeichnung), Aufträge, Build-/Boot-Meldungen |
| P6.2 | Spur-Überlagerung: nummerierte Kanten ①②③ mit Zeit, nicht gegangene Ausgänge blass, ⧗ pulsiert, ✕/⏳ rot, Verlierbares gestrichelt; Klick → Panel mit Payload, Akteur, Knoten, Stand, ↤/↦ |
| P6.3 | Live-Puls ohne gewählten Vorgang: Zähler, „zuletzt“, Kantendicke, Stau, nie benutzte Kanten |
| P6.4 | **Ein „Fokus“** ersetzt Slice, Ketten-Fokus, ◎ und ⤢ (`sliceVon`/`ketteVon`/`clientSlice`/`wendeFokusAn`, 4467–4537, Inspector-Header 4546). Ohne Vorgang die statische Kette, die **der Server liefert** (GraphBuilder hat kausale Ketten); mit Vorgang die echte Spur. Der Inspector bleibt als Panel |
| P6.5 | Ältere Stände grau; fehlende Karte → „⚠ Karte gibt es nicht mehr“ |

Abnahme (Browser):
- Der gewählte Vorgang zeigt den nummerierten Weg.
- Die Zeitleiste lässt sich schieben.
- Der Puls zählt.

---

## 13 · P7 — Eine Wahrheit (vier Stufen)

### P7.1 Warmer Workspace und fehlertolerantes Lesen
- GraphExtractor wird **Bibliothek plus CLI**: Kern nach `GraphExtractor.Kern` (oder gleiches Projekt als Library), CLI ruft ihn.
  SimHost referenziert den Kern.
- Statische Zustände raus (`Projektlage.Dokumente`).
- `Arbeitsbereich.cs` in SimHost:
  - `MSBuildWorkspace` einmal laden;
  - ein FileSystemWatcher auf die Editor-Projekte ruft `WithDocumentText` auf;
  - Neu-Lesen nach **E7**.
- Fehlertoleranz:
  - `Projektlage` wirft nicht mehr; ohne Routing-Generat gilt der Decide-Fallback.
  - Unaufgelöste Basistypen werden Code-Karten mit ⚠.
  - `FlussRaw.Unbekannt` und verworfene Regeln werden Diagnosen an der Karte.
  - `ToDictionary` wird tolerant (Duplikat = Diagnose).
- Die Karten tragen Datei plus Spanne (für P3.1 und den Rumpf-Ausschnitt).

### P7.2 Geste → Patch
- Endpunkt `POST /api/werkbank/geste {art, ziel, …}` → Patch (Liste von Datei-Edits) → Workspace → Neu-Lesen → Board zurück.
  Gemessen und geloggt wird jede Geste.
- **Gesten-Katalog:** aus `konzept-handle-ausgaenge.md` §13 und den heutigen JS-Mutationen:
  - `neuerKnoten`;
  - die ~45 Zweige von `applyLink`/`loeseLink`;
  - `renameRefs`, `delNode`;
  - die Fluss-Operationen `flVerbinde`/`flLoese`/`flUmbenennen`/`flLoeschen`;
  - Zuordnungs-Editor `zoSchreib`;
  - Rumpf-Edit.

  Je Geste ein Test im Prüfstand: Patch auf eine Fixture-Datei, erwarteter Code.
- **Umsetzung:**
  - Anlegen über den Scaffolder;
  - Ändern über die Edit-Primitive aus `DateiAbgleich` (umgebaut von `Arbeitsbereich`-Strings auf Workspace-Dokumente);
  - Umbenennen über `Renamer.RenameSymbolAsync`;
  - Löschen und Signatur ändern über `SymbolFinder.FindReferencesAsync`.
- Regeln aus Anhang A.2 (verlustfrei, Reihenfolgen, Lambdas wortgetreu; wechselt das Ziel einer Regel, wird das Lambda verworfen
  und neu vorgeschlagen).
- **↶** = Patch-Stapel (inverse Edits je Geste, eine Datei `werkbank.verlauf.json` je Branch). Die LLM-Sicherung geht in denselben
  Stapel.
- JS: Jede Mutation ruft die Geste, nimmt das Board, rendert. Es entfallen:
  - `MODEL`-Persistenz, `mergeBoard`, `normalize`-Migrationen, `_herkunft`/„ungeschrieben“/Entwürfe;
  - `deSave`, „C# schreiben“, 👁;
  - die Endpunkte `/api/editor/board` und `/api/editor/write`;
  - `Herkunft.cs`, `BoardLeseseite.cs`.
- Positionen, Filter und Akteur-Reihenfolge kommen in die **Ansichts-Datei** `werkbank.ansicht.json` (Schlüssel = Karten-Id).
- Fortan arbeitet der Editor auf einem eigenen Branch bzw. Worktree.

### P7.3 Ableitungen nur auf dem Server
- Der Server schickt je Karte `{domäne, akteurRahmen, block, rolle, storeScope, mitgliedschaft, diagnosen}` sowie Kanten, Inseln,
  Akteur-Mengen und Ketten.
- `GET /api/werkbank/passt?port=` beantwortet ⊕ aus `Grammatik.cs` plus dem C#-Gegenstück von `compatible`.
- Löschen in dieser Reihenfolge, jeweils mit vorübergehendem Paritäts-Vergleich JS ⇄ Server (Muster `deAkteurParitaet`), dann
  ersatzlos:
  1. `akteurMengen`/`akteurSet`/`akteurVon`;
  2. `flussRegeln`;
  3. `deriveMembership`/`groupKeyOf`;
  4. `derivedStores`;
  5. `boardEdges`/`components`;
  6. `domKey`/`einheitDom`;
  7. `berechneSicht`;
  8. `compatible`/`grPruefe`;
  9. Client-Ableitungen.
- Parität und Sonde auf die Server-Kartenliste umstellen. Die „gezeichnete“ Sonde wird ein Gesten-Skript statt
  `Gezeichnet.board.json`.

### P7.4 Prüfen = Bauen, Code-Karte, Rumpf aus der Datei
- **Validator → Analyzer** (Backlog §1.2 und A.4). Neue Regeln ab CQRS073:
  - GR-FLUSS-QUELLE/-WARTE/-ZEITLIMIT (heute Laufzeit);
  - GR-FLUSS-DRAHT;
  - GR-QUERY, GR-GARANTIE, GR-ZUSTAND, GR-FALTUNG, GR-MODUL-*, GR-GETRAGEN, Regel Z;
  - GR-ZYKLUS für Flüsse;
  - die Diagnosen aus A.1 (UNROUTED-SAGA-CMD …).

  Danach `Validator.cs` löschen und die betroffenen `DomainEditor*Tests` auf Analyzer-Tests umstellen. Scaffolder-Tests bleiben.
- **Code-Karte:** Jeder Typ in Editor-Projekten ohne Konstrukt wird eine Code-Karte (roher Code, Diagnosen, 🤖). `--check` meldet
  „Code ohne Karte: 0“ (Pflicht) und den Anteil der Code-Karten.
- **Rumpf aus der Datei:** Die 📝/🤖-Karten entfallen (`codeNodeCard`, `llmNodeCard`, Andocken, `codeSrc`, Code-Sync-Poll,
  `/api/editor/code`). Der Rumpf-Port zeigt einen Ausschnitt aus der Datei, ✎ bearbeitet die Spanne als Geste. „🤖 füllen“ wird
  eine Aktion.
- **LLM-Prüfung umstellen (L18):** decide/apply/fluss/saga prüfen gegen den warmen Workspace (Patch probeweise anwenden,
  Diagnosen der betroffenen Projekte, verwerfen) statt `ModellSimulation`. `ProjektPruefung` geht darin auf.

Abnahme (Zielbild):
- `--check` meldet „Code ohne Karte = 0“.
- Das Umbenennen eines Events ändert alle Verweise (auch in Lambdas) und baut.
- Es gibt keine JS-Ableitungsfunktion mehr (Grep-Liste aus §1.1 leer).
- Eine Geste dauert < 300 ms (p50 gemessen; p95 dokumentiert).

---

## 14 · P8 — Simulation raus

- Löschen:
  - `SimHost/ModellSimulation.cs`, `Musterwerte.cs`;
  - `/api/editor/sim/*`, `/api/editor/compile`, `/api/llm/simulieren`;
  - der JS-Abschnitt SIMULATION (`SIM`, `simPanel`, `simAnimiere`, `simOverlay` …) samt CSS und Hooks;
  - die Projekt-Referenz SimHost → Cqrs.Testing;
  - `DslSchreiber` (nur SimHost nutzte ihn; der Knopf „Als Test“ entfällt mit der Simulation).
- Bleibt: `Cqrs.Testing` (Laufwerke, Szenario) für die vier Prüfstand-Dateien `FlussLaufwerkTests`, `UmgezogeneFluesseTests`,
  `SammelvorgangKaskadeTests`, `SammelvorgangBarriereTests`.
- Optional: `SimHost` → `Werkbank` umbenennen (`.claude/launch.json`, Doku).
- Doku:
  - CLAUDE.md „Aktueller Stand“ und Build/Test;
  - `docs/01`, `04`, `05`, `10`, `12`, `13`;
  - Status des Zielbilds auf „umgesetzt“.

Abnahme:
- SimHost läuft ohne Simulation.
- Der Prüfstand ist grün.
- Ein Grep nach `ModellSimulation|/sim/` ist leer.

---

## 15 · Risiken

| Risiko | Wirkung | Gegenmaßnahme |
|---|---|---|
| Gesten-Latenz (L19) | P7 erreicht 300 ms nicht | E7; Messung ab P7.1; notfalls inkrementeller Extractor je Dokument |
| Proto-Feldreihenfolge bei geerbten Properties (L1) | Bruch der Python-/Client-DTOs | Eine Ordnungsregel (Basis vor Ableitung, Deklarations-Reihenfolge) in allen vier Analysatoren; Byte-Round-trip-Test |
| Deploy bricht bei Konfig-Umzug | Prod-Host startet nicht | P1.9/P2.10 ziehen `deploy/` im selben Commit mit; vor dem Merge nach `main` einmal `deploy-multinode` hochfahren |
| Spur-Header-Volumen | Größere Events | Drei kurze Strings je Event; Messung im Last-Test (Group-Commit-Durchsatz vorher/nachher) |
| Viele Editor-Umbauten ohne Browser-Tests | Regressionen | Jede Stufe im Browser belegen (Screenshot und Kartenzahl); Paritäts-Vergleiche in P7.3 vor dem Löschen |

---

## 16 · Messwerte (wird während der Umsetzung gefüllt)

| Messung | Wert | Datum |
|---|---|---|
| Prüfstand | 389/389 grün, 8 s | S0 (2026-10-12) |
| Integration (sequentiell, echte Infra) | 33/33 grün, 63 s | S0 (2026-10-12) |
| GraphExtractor kalt (Default-Lauf) | 13,8 s (nicht ≈ 1 min, wie die UI behauptete) | S0 (2026-10-12) |
| `--check` / `--sonde` | 17,7 s / 23,2 s, beide grün | S0 (2026-10-12) |
| `dotnet build CqrsSolution.sln` (inkrementell, nichts geändert) | 14 s | S0 (2026-10-12) |
| Domänen-Recompile nach Text-Änderung (warmer Workspace) | — | P7.1 |
| Rename über alle Projekte | — | P7.2 |
| Geste p50 / p95 | — | P7.2 |

---

## 17 · Fortschritt

| Phase | Stand | Was konkret |
|---|---|---|
| S0 | ✅ 2026-10-12 | Grundlinie gemessen (§16). Editor-Oberfläche aus `GraphExtractor/HtmlPresenter.cs` (gelöscht) nach `SimHost/wwwroot/editor/` (`index.html`, `editor.css`, `editor.js`), SimHost liefert statisch (no-store). `LoadHarness`-Reste (CLAUDE.md, `deploy-multinode`) entfernt |
| P0 | ✅ 2026-10-12 | Server (`ModellMapper.ZuBoardJson`) liefert keine `pipelines`/`triggers`/`selbstNachrichten`/`dienste`/`hostSettings` mehr; Quell-Webhooks bleiben als Bindung am Fluss. JS: Trigger-, Handle-Pipeline-, Frist-, Dienst-, HostSetting-Karten samt Kanten, Ports, Ableitungen, Umbenennen/Löschen entfernt; Landkarte/LOD entfernt (die Karten-Darstellung ist fest: Klasse `ablauf`); Palette und Doppelklick-Auswahl = Anker (Aggregat · Fluss · Funktion · Quelle · Akteur · Client · Projektion); „Quelle" legt bis P1 einen `IQuellNachricht`-Record mit `Kennung` an. Das Domänen-Menü „＋ in Domäne" bietet weiter alle Bausteine der Domäne an (ohne Trigger-Welt) — das ist der Weg für „Zugehöriges anlegen", solange nicht jede Art einen „＋"-Port hat. Sonde: die Trigger-Welt-Konstrukte (`MahnlaufPipeline`, `Vormerkpruefung`, Trigger, Selbst-Nachrichten, `IAkteurDienst`) entfernt, `soll.txt` nachgezogen. `Grammatik.cs`/`Validator.cs` unverändert (fallen in P7). Belegt: `--check` ✅, `--sonde` ✅ (78 Fakten), Prüfstand 389/389, Browser: 580 Karten, keine Trigger-Welt, Akteur-Parität JS = C#, ⊕-Modus leuchtet |
| P1 | ✅ 2026-10-12 | **Codegen:** `TypeNode.ViaInitialisierer` — alle vier Analysatoren (Reflection/Codegen, Roslyn/DtoMapper, Proto-Tool) lesen ohne Ctor-Parameter die public init/set-Properties (auch geerbte, Basis zuerst); der DtoMapper baut per Objekt-Initialisierer. Nebenbei behoben: der Record-Kopier-Konstruktor landete als Feld `original` im Proto. **Abstractions:** `DateiGemeldet`, `Treibervertrag` (verlinkt, in Generatoren `internal`), `IStreamBestand`; `IQuelle`/`IQuelle<T>` gelöscht (damit entfällt CQRS071 — es gibt nichts mehr zu implementieren). **Infrastructure:** `QuellenGenerator` → `GeneratedQuellen`; Datei-Treiber (`DateiScan` store-frei, `DateiTreiber`), `DateiQuellKonfig` per Binding-Source-Generator (strikt), `AddQuellen` mit Start-Prüfungen, `QuellenDienst` mit Lease (`quelle`-Kind, `QuellenLease`) für `EinmalImCluster`, `QuellenZustand` + Health-Check, `MartenStreamBestand`. **Analyzer CQRS070.** **Bestand:** `DateiErkannt : DateiGemeldet`, `DateiQuelle`/`FileWatchConfig` gelöscht, `AddDomainPipelineServices(preprocessedPath)`, Host `AddQuellen`, Deploy auf `Quellen__DateiErkannt__Pfad`; Prod-Deploy mit `Kennung=NameGroesse` (gleiche Vorgangs-Ids → kein Neu-Verarbeiten des Shares). `PreprocessedPath` bleibt bis P2 `Pipeline:PreprocessedPath` (E2 folgt mit der Umgebungs-Datei). Erkennungs-Wert heißt `EreignisUndAbfrage` (Enum statt „Ereignis+Abfrage"). **Editor:** Treiber und geerbte Felder als Code-Fakt (`Record.Treiber`, `Feld.Geerbt`), Quellkarte „📁 Datei-Treiber" mit Konfig-Werten je Host (`rahmen.quellenKonfig`, bis P2 aus `appsettings.json`), Anker „Quelle" legt `sealed record X : DateiGemeldet` an (`rahmen.treiber`). Python-DTOs regeneriert (ohne das scheitert der Python-Worker-Test — gehört in die ⟳-Kette, P3). Belegt: Prüfstand 409/409 (20 neu), Integration 34/34 (neu: Treiber → Vorgang, nach Neustart keine zweite Meldung), `--check` ✅, `--sonde` ✅ (79 Fakten, neu: Datei-Quelle) |
| P2 | ✅ 2026-10-12 | **Umgebungs-Typen** in `Abstractions/Umgebung/UmgebungKonfig.cs` (zugleich das Formular-Schema für P3). **Lader** `Infrastructure/Umgebung/UmgebungLader.cs`: `--umgebung`/`CQRS_UMGEBUNG`, Datei neben dem Programm oder aufwärts, Env/Argumente überschreiben, `${env:}`/`${branch}`, nur bekannte Abschnitte, strikte Bindung. **`AddCqrs(args)` + `LaufeAsync`** ersetzen die Host-Verdrahtung (Host.Grpc/Program.cs: 3 Zeilen + Webhook). **Siegel** (`<schema>.umgebung`, Name/Art/Cluster; Sim-DB wird bei Bedarf angelegt), **Uhr** (`<schema>.uhr`, `UmgebungsDbClock`, Versatz nur Sim), **Redis-Präfix** (`RedisPraefix`, KeyspaceIsolation), **Einzelknoten-Cluster** (Proto `TestProvider`), **Einblick-Port** HTTP/1.1. **Funktions-Laufort als Konfig:** `FunktionsGenerator` erzeugt `Namen`, `Implementierungen` (generische Bindung je Impl) und `Extern`; `BindeFunktionen` prüft gegen die Tabelle; `Hand` folgt in P5. **Konfig-Records der Domäne** unter `Konfig:<Record>` (abweichend von E2: ein Abschnitt je Konfig-Record statt je Funktion, weil sich zwei Funktionen `PreprocessingConfig` teilen; `AddDomainPipelineServices(IConfiguration)` bleibt als eine Zeile im Programm — Domänen-DI ist Code). Umgebungen `sim-main`, `prod`, `multinode`; Deploy (`deploy.sh`, systemd-Env, Multi-Node-Compose, `build.sh` → `umgebung.linux.json`, `start-server.sh`) auf `--umgebung`. **Extractor** liest `umgebungen/*.json` (`rahmen.umgebungen`) statt appsettings; Quellkarte zeigt Werte je Umgebung. Belegt: Prüfstand 420/420 (11 neu), Integration 37/37 (neu: Siegel-Konflikt, zwei Umgebungen parallel inkl. Redis-Präfix, Versatz verschiebt Frist), Host-Smoke mit `--umgebung sim-main` (DB angelegt, Siegel, Einzelknoten, `/health` über HTTP/1.1, Quelle erholt sich). **Nicht gefahren:** Multi-Node-Compose und Prod-Deploy (nur umgestellt). |
| P3 | ✅ 2026-10-12 | **Werkbank-Server** (`SimHost/Werkbank/`): ⟳-Kette `KreislaufAsync` (Bauen → Python-Basen → Neu lesen → Daten prüfen → Infrastruktur → Host ersetzen; Prod endet nach dem Lesen), Compiler-Befunde mit Typ/Member (`Diagnosen`), `HostProzess` (Start/Ersetzen, `/health`, Boot-Zeile `CQRS-BOOT {json}` mit Karte, Stopp beim Ende von SimHost), `Infra` (Compose `cqrs-<umgebung>` für verwaltete Dienste), `SimDatenbank` (zurücksetzen, Stand speichern/laden/löschen mit Gültigkeit aus dem Proto-Vergleich, Prod-Stand übernehmen, Uhr; ohne Pool, übrige Verbindungen werden getrennt), `Fortschritt` (SSE `/api/werkbank/ereignisse`), Endpunkte `/api/werkbank/*` (zustand, schema, umgebung, aktiv, kreislauf, diagnosen, host, simdb, uhr, infra). **Daten prüfen / E9:** `Abstractions/Kodehash.cs` + `Domain.SourceGeneration/Kodeform.cs` (Token-Folge ohne Leerraum/Kommentare); `CodeHash` im Aggregat-Handler (State + Applier) → Snapshot-Version; `CodeHash` in jeder Projektion, `StoreStand`/`ReadModelTyp` aus dem `ProjectionServicesGenerator`, `ProjektionsStand` aus dem `PullPathGenerator`; `ProjektionsStandDienst` (Sim: Leseseite leeren + neu aufbauen, Prod: Meldung); Store-Basis nur noch `IReadModel`; erster Poll nach 2 s. **Editor:** Kopfzeile (Umgebung ▾ · Build mit Stand · Lauf inkl. „Entwurf ≠ Lauf"/„Konfig geändert" · Postgres/Redis/Consul · ⟳ · 📸), 🖥 Host-Rahmen rechts neben den Clients mit **einem generischen Formular-Renderer** aus `/api/werkbank/schema` (klasse/je/frei, enum/bool/zahl/dauer/liste/map; Speichern schreibt die Umgebungs-Datei), Uhr-Karte, Befunde und Boot-Fehler an der Karte (⛔ + Panel), Meldungen/Host-Log im Ausgabe-Panel; Knöpfe Prüfen/Kompilieren/Alle ausführen entfernt. Belegt: Prüfstand 423/423 (3 neu), Integration 38/38 (neu: Leseseite baut nach Code-Änderung in Sim neu auf, sonst nicht), `--check` ✅, `--sonde` ✅. **Browser-Abnahme:** CS0029 in `Decide(FuegeRangeHinzu)` → am Decider; nach Korrektur ⟳ grün, Host gegen `cqrs_sim_claude_werkbank_zielbild`; Stand speichern 12,8 s / laden 14,0 s (Kopie < 1 s, Rest = Host anhalten 2 s + Boot), Events zurück; `IRangeSuche` aus der Umgebung entfernt → Boot-Fehler an der Funktions-Karte; Prod-Stand übernehmen 8,9 s (2028 Events), Host siegelt auf Sim und baut 5 Projektionen neu auf. Neu lesen dauerte unter Last 55 s (S0: 13,8 s). |
| P4 | ◐ 2026-10-12 | **Schnitt 1 — Namen und Ids (P4.1, P4.2, P4.8):** `Abstractions/Spur/WirkerId.cs` (acht Formen, Regel-Name nach E4 `A+B→C`, Count-Join `A+S*→C`, Knoten-Namen inkl. anonymer Knoten; per Link im Domain-Generator `internal`). `ProzessRegelnGenerator` liest die Namen aus dem Quelltext (Fluss: Anweisungen im `Definiere`-Lambda; Prozess: `Sende`/`SendeJe`/`Rufe` der Regel-Bauer, Bedingung aus deren Typ-Argumenten) und emittiert `….Regeln.Benannt("P", …)`; **CQRS072** bei gleichem Regel-Namen. `ProzessRegeln.Benannt/Name/KnotenNamen/RegelNamen/Wirker(i)`; falsche Namenszahl = Boot-Fehler. Nebenbei behoben: die Registry rief `new X().Regeln` — bei expliziter Interface-Implementierung kompilierte das nicht (jetzt Cast aufs Interface; die Sonde meldet Fehler in generierten Dateien nicht). `FlussLeser` benennt über `WirkerId`. **Stabile Karten-Ids:** `ModellMapper.StabileIds` setzt `_id` = Wirker-Id für Decider, Fluss-Knoten, Prozess-Regeln, Funktionen, Akteure, Projektionen, Reaktionen; natürliche Schlüssel für Applier, Clients, Pipelines, Reader, Stores, Store-Fns (`wf:IStore.Methode`), ReadModels (statt `pj1`/`rk1`/`rd1`/`st1`/`wf_i_j`). JS übernimmt die Regel-Id in die Transition, State-Id `state:<Agg>`. **Befunde über die Id:** `SimHost/Werkbank/Karten.cs` ordnet Typ + Member über das gelesene Board einer Karten-Id zu (`Diagnose.Karte`); das JS sucht nur noch per `karteVonId` (Fallback Name nur für noch nie gelesene Typen). **`--check`:** Bereich „Wirker“ vergleicht die Namen der generierten Registry mit den Karten-Ids (17 = 17). Belegt: Prüfstand 429/429 (6 neu: Format, Regel-/Knoten-Namen, Laufzeit-Namen der Domäne, Generator-Probe, CQRS072), `--check` ✅, `--sonde` ✅ (fand den Fehler mit expliziter Implementierung), Browser: CS0029 in `Decide(FuegeRangeHinzu)` → `decide:Datensatz/FuegeRangeHinzu` → ⛔ an der Decider-Karte |

