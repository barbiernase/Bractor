# Umsetzungsplan: Werkbank ohne Simulationsschicht

> **Status 2026-10-12: in Umsetzung — S0, P0, P1, P2, P3, P4, P5, P6, P7.1, P7.2, P7.3 und P7.4 erledigt (siehe §17 Fortschritt).** Er setzt das Zielbild `docs/konzept-werkbank.md` um. Dort Entschiedenes
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
- P7 („Eine Wahrheit“) ist mit Abstand die größte Phase. Sie wird in **fünf Stufen** (P7.1–P7.5) geschnitten, jede einzeln grün.
  P7.5 (ergänzt 2026-10-12) schließt die letzten Stellen, an denen heute noch Handcode nötig ist: Dienst-Bindung, Webhook, Python-Rümpfe
  externer Funktionen. Danach ist **alles außer den Quell-Treibern** im Editor definierbar und per ⟳ ausführbar.
- Ab P3 gilt eine **Bauregel für das Frontend:** Neue Editor-Funktionen bekommen ihre Daten fertig vom Server. Es entsteht kein
  neues Modell-Wissen im JS, das P7 wieder löschen müsste.
- Es gibt **23 Lücken** zwischen Zielbild und Code (§2; L21–L23 ergänzt nach P6). Die tragenden sind:
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
| L21 | Domänen-Dienste (`IImageResizer`, `IHistogramEqualizer` …) und ihre DI-Registrierung sind Handcode (`Domain.Pipeline/Infrastructure/DomainPipelineExtensions.cs` + eine Zeile in `Host.Grpc/Program.cs`); die Dienst-Karte ist in P0 entfallen, P2 hat Domänen-DI als Code belassen | §1 „alles bauen“, §8.5 | P7.5.1 Dienst-Bindung (**E11**) |
| L22 | Die Webhook-Bindung einer Quelle (`app.MapQuellWebhook<T>(route)` in `Program.cs`) liest der Extractor, schreiben kann sie niemand | §3, §6 | P7.5.2 Webhook als Konfig (**E12**) |
| L23 | Externe Funktionen in Python haben keinen Rumpf im Editor: der Worker (`funktions_worker.py`) ist handgeschrieben, `PythonSync` kennt nur Zusagen | §8.4 „Python-Rümpfe (Zusagen, externe Funktionen)“ | P7.5.3 Python-Basis + Rumpf je Funktion (**E13**) |

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

| **E11** | Woran erkennt man einen Domänen-Dienst, und wo wird er gebunden | Code-Fakt statt Namensregel: Marker `IDienst` (Abstractions) am Interface; jede Klasse, die es implementiert, ist eine Implementierung. Ein `DienstGenerator` erzeugt die Tabelle Interface → Implementierungen (ohne Reflection), `AddCqrs` bindet je Umgebung `Dienste:<I>:Implementierung` (Default: die einzige). Konfig-Records im Ctor einer Implementierung kommen generisch aus `Konfig:<Record>` (wie heute für Funktionen). Erlaubt nur im Ctor von Funktions- und Dienst-Implementierungen (Analyzer ab CQRS073, Zyklen = Boot-Fehler). Abgrenzung: `IAkteurDienst<A>` bleibt, was es ist (gehört einem Akteur, nur Handle-Parameter, CQRS060) | Dieselbe Mechanik wie Funktionen (Tabelle + Bindung aus der Umgebung) statt einer zweiten; `AddDomainPipelineServices` entfällt ersatzlos |
| **E12** | Webhook einer Quelle: Code oder Konfig | **Konfig**: `Quellen:<Typ>:Webhook` (Route, optional). `AddCqrs`/`LaufeAsync` mappen jede Quelle mit Route generisch aus `GeneratedQuellen` auf dem Einblick-Port; `MapQuellWebhook` in `Program.cs` entfällt, `[Ingress]` liest der Extractor dann aus der Umgebungs-Datei | Leitsatz „nur Konfig unterscheidet“; die Route ist Betrieb, nicht Domäne; der Editor schreibt sie mit dem generischen Formular-Renderer, ohne einen Schreiber für den Composition Root |
| **E13** | Python-Rumpf einer externen Funktion | Wie bei den Akteur-Verträgen: der Codegen erzeugt je `IFunktion` eine **Python-Basis** (getypter Auftrag aus den betterproto-DTOs, Ergebnis-Fälle als Union), die Implementierung liegt in `Domain.Client.Worker.Python.ML/funktionen/<funktion>.py` (`rufe(self, auftrag) -> Ergebnis`). Ein **generischer Worker** lädt die Implementierungen aus einer generierten Tabelle (kein Domänenwissen im Runner); `funktions_worker.py` geht darin auf. `PythonSync` liest und schreibt den Rumpf wie bei Zusagen | Ein Muster für alle Python-Rümpfe; „▶ Worker starten“ braucht keine Konfig je Funktion mehr außer dem Laufort |

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
S0 → P0 → P1 → P2 → P3 → P4 → P5 → P6 → P7.1 → P7.2 → P7.3 → P7.4 → P7.5 → P8.

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

**Umgesetzt mit diesen Abweichungen (2026-10-12):**
- **JSON-Eingang (P5.1):** `Infrastructure/Eingang/JsonEingang.cs` — `GET /eingang/rechte`, `POST /eingang/command` | `/query` (Body
  `{ typ, daten }`, Header `akteur-token`), nur auf dem Einblick-Port und nur mit `Transport:JsonEingang` (sim-main an, prod/multinode aus).
  Ein Command geht **mit Antwort** (`ProtoActorAggregateDispatcher.FrageAsync`, der einzige erlaubte Client-Weg laut CQRS020): Events,
  Ablehnungs-Event oder Fehler. Ohne `erwarteteVersion` im Modus *Emittiert* (eine Konsole kennt keine Version). Ohne Identitäten ist das
  Tor offen wie am gRPC-Port. JSON ↔ Nachricht über den generierten Wire-Kontext: der Codegen nimmt jetzt **Queries und Antworten** auf und
  setzt **`UseStringEnumConverter`** (Enums als Namen — betrifft nur die interne Actor-Plane, beide Seiten nutzen denselben Kontext);
  Feldnamen ohne Groß/Klein-Unterschied (Kopie der Kontext-Optionen, Resolver bleibt der Source-Generator).
- **Laufort Hand (P5.2):** kein eigener Abholer nach dem Muster `FunktionsAbholer`, sondern `HandAusfuehrer` = eine
  `FunktionsAnbieterSitzung` (dieselbe wie für Python-Worker), deren „Weiterreichen“ den Auftrag in eine Ablage legt; Lebenszeichen alle 10 s,
  solange der Mensch überlegt; Antwort über denselben Abschluss (genau ein Ergebnis, Ergebnis außerhalb des Vertrags = 400 statt Marke).
  Endpunkte `/eingang/auftraege` (mit Auftrag-JSON und Ergebnis-Fällen) und `/eingang/ergebnis` (`{ vorgang, typ, daten }` oder `{ vorgang, fehler }`).
  Hand ist auch mit Lese-Fähigkeit erlaubt (ein Mensch braucht keine Stores).
- **Worker (P5.5, E10):** `Funktionen:<F>:Worker` = `{ Python, Skript, Argumente, Token }`; die Werkbank setzt `CQRS_GRPC_HOST/PORT` und
  `CQRS_AKTEUR_TOKEN`. Weil das Tor in sim-main jetzt aktiv ist (Identitäten), braucht ein externer Worker einen Akteur mit `IDarf<F>` — neu:
  `Domain.Akteure.Bildrechner : IMaschine, IDarf<IBildVerkleinerung>, IDarf<IHistogrammAusgleich>`. Neuer Domänen-Worker
  `Domain.Client.Worker.Python.ML/funktions_worker.py` (OpenCV, Gegenstück zu `OpenCvBildaufbereitung`).
- **sim-main:** `Transport:JsonEingang`, `Identitaeten` mit einem Token je Akteur (`sim-<akteur>`), Worker-Konfig für beide Bildfunktionen;
  alle Funktionen bleiben im Host (Hand/Extern stellt man im 🖥 Host-Rahmen um).
- **Werkbank/Editor:** `SimHost/Werkbank/Durchspielen.cs` (Proxy `/api/werkbank/eingang/*`, Tokens, Formular-Schema eines Records **aus dem
  gelesenen Code** im Format des Umgebungs-Schemas — derselbe generische Renderer, erweitert um Guid/Zeit/typisierte Listen —, Datei ablegen
  erst daneben, dann umbenennen; Uhr vor) und `Worker.cs`. Editor: Leiste unten (⌨ Konsole · ⧗ Aufträge · ▶ Worker), ⧗ n an der
  Funktions-Karte und Antwort-Formular im Panel (gleichnamige Felder des Auftrags vorbelegt), Drop + Datei-Wahl an der Quellkarte, ⏩ in der
  Kopfzeile (+1 min/h/Tag/7 Tage, zurück auf 0). Vorgänge/Zeitleiste der Leiste folgen in P6.

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

**Umgesetzt mit diesen Abweichungen (2026-10-12):**
- **Vierter Header `ueber` (Absender):** Das Log kannte nur Ursache und Wirker — der Fluss-Knoten bzw. die Regel, die einen Command oder
  Auftrag abschickte, ging beim Aggregat bzw. der Funktion verloren (beide stempeln ihren eigenen Wirker). Ohne ihn lässt sich der Weg über die
  Fluss-Karten nicht nummerieren. Jetzt: `Spur.Kopf(Ursache, Wirker, Ueber)`; das Aggregat setzt `ueber` = Wirker aus dem Envelope, die Funktion
  = Wirker aus dem Auftrag (`AuftragAnbieten.Wirker`, die Spur des Absenders reist wie die Ursache); Header `ueber`, gelesen in `EventEnvelope.Ueber`
  und `SpurEintrag.Ueber`. `SchrittGescheitert`/`SchrittUmgeleitet` tragen jetzt den Wirker des gescheiterten Knotens (⏳/✕ an seiner Karte).
- **Weg = eine reine Funktion** `Abstractions/Spur/SpurWeg.Aus` (Header → Karten + gegangene Kanten; der erste Fluss-Draht über den Quell-Knoten
  aus dem gelesenen Code). Die Werkbank liefert ihn je Schritt fertig; das JS schlägt nur Karten-Ids nach.
- **Spur-Spiegel** `SimHost/Werkbank/SpurSpiegel.cs`: folgt dem laufenden Host (SSE `/einblick/neu` + Cursor `/einblick/spur?ab=`), je Host-Start ein
  neuer Lauf (auch nach jeder Sim-DB-Aktion, denn die startet den Host neu). Endpunkte `/api/werkbank/spur/puls`, `/spur/vorgaenge` (Filter Quelle,
  Akteur, Aggregat-Id, Zustand + Facetten), `/spur/vorgang/{k}` (Schritte mit Nummer, Karten, Kanten, Payload, Stand/alt, ↤/↦, Hinweise, Bahnen,
  offene Aufträge). Der Editor erfährt „neu“ als `spur` über den Fortschritts-Kanal.
- **Einblick-Host:** `SpurEintrag.Name` (Typname im Code) neben dem Marten-Namen; Vorgänge melden den Wurzel-Typ im Code-Namen; ein Command von
  außen, den das Aggregat ablehnte, ist ein **gescheiterter** Vorgang (Grund = die Ablehnung).
- **Zeitleiste:** Spalten = Schritte (gleich breit, darüber +Δt), Zeilen = Bausteine; reine Zeitachse wäre bei Millisekunden-Abständen unlesbar.
- **Fokus (P6.4):** EIN Mechanismus (`fokussiere`/`fokusNeu`, Chip „◎ Kette · ▣ Menge · ⟿ Vorgang · ⤢ · ✕“ in der Ansichts-Leiste); Rahmen-◎ setzen
  eine Menge, ein Klick auf eine Karte die Kette, ein Vorgang die Spur; ⤢/◎ im Panel-Kopf entfallen. **Die statische Kette rechnet weiter
  `ketteVon`/`sliceVon` aus den JS-Kanten** — sie zieht mit `boardEdges` in P7.3 auf den Server (dort steht ohnehin „Ketten“ auf der Liste); vorher
  hätte der Server eine zweite Kanten-Ableitung gebraucht.
- **Offen:** Rückstau einer Quelle im Puls (Stau zeigt nur offene Aufträge); „welche Projektion was verarbeitet hat“ (Tracker-Marken) bleibt aus P4
  offen; die Kante „Event → Fluss-Knoten“ gibt es auf dem Board nicht (der Draht davor trägt die Nummer).

---

## 13 · P7 — Eine Wahrheit (fünf Stufen)

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

### P7.5 Alles außer Quell-Treibern (ergänzt 2026-10-12)
Prüfung nach P6 (Frage: „Kann ich alles bis auf die Quell-Treiber im Editor definieren und ausführen?“): Anlegen und Rümpfe gehen für alle
Bausteine, Ändern kommt mit P7.2, Hilfscode mit der Code-Karte (P7.4). Drei Stellen bleiben danach Handcode — diese Stufe schließt sie.

- **P7.5.1 Dienst-Bindung (E11, L21):**
  - Abstractions: Marker `IDienst`; Generator `DienstGenerator` → `GeneratedDienste` (Interface → Implementierungen, Konfig-Records je Ctor);
    `AddCqrs` bindet aus `Dienste:<I>` der Umgebung (Schema-Abschnitt `Dienste`, Host-Rahmen-Karte „🧩 Dienste“); Boot-Guard: fehlende oder
    mehrdeutige Implementierung, Zyklus.
  - Analyzer (ab CQRS073): ein `IDienst` nur im Ctor einer Funktions- bzw. Dienst-Implementierung; nie im Fachcode von Aggregat/Projektion/
    Reaktion (dort bleiben Fähigkeiten als Parameter).
  - Extractor + Sonde/`soll.txt`: Dienst-Karte (Signatur + Implementierungen als Logik-Element: Eingang · Ausgang · Rumpf 📝/🤖), Kante
    Funktions-Implementierung → Dienst; Scaffolder legt Interface + Implementierungs-Stub an, verbunden wird per ⊕ (Ctor-Parameter als Geste).
  - Bestand: `IImageResizer`/`IHistogramEqualizer` werden `IDienst`, `PreprocessingConfig` kommt aus `Konfig:`; `DomainPipelineExtensions.cs`
    und die Zeile in `Host.Grpc/Program.cs` entfallen.
- **P7.5.2 Webhook als Konfig (E12, L22):** `QuellKonfig.Webhook` (Route), generisches Mapping aus `GeneratedQuellen` auf dem Einblick-Port,
  `MapQuellWebhook` aus `Program.cs` raus (der Host ist dann `AddCqrs` + `LaufeAsync`); Extractor liest die Route aus den Umgebungs-Dateien;
  Quellkarte zeigt „⇥ Webhook /…“ je Umgebung, bearbeitet im Host-Rahmen. Integration: POST auf die Route startet genau einen Vorgang.
- **P7.5.3 Python-Rümpfe externer Funktionen (E13, L23):** Python-Basis je Funktion aus dem Codegen (`build_python.sh`, Teil der ⟳-Kette),
  Implementierungs-Datei je Funktion, generischer Worker mit generierter Tabelle, `PythonSync` für den Rumpf (✎/🤖 an der Funktions-Karte, wenn
  der Laufort `Extern` ist), `funktions_worker.py` aufgelöst. Python-Tests (Worker) + Fluss-E2E mit Python-Worker grün.

Abnahme:
- Auf einem leeren Branch wird **nur im Editor** eine neue Domäne gebaut: Aggregat (Commands, Events, Decide/Apply), Fluss aus einer
  Datei-Quelle mit Webhook, eine C#-Funktion mit einem neuen Dienst, eine Python-Funktion (Laufort Extern), Projektion + Reader, Akteur +
  Client. ⟳ ist grün, der Vorgang läuft durch (Datei-Drop und Webhook), die Spur zeigt den ganzen Weg.
- `git diff` dieser Domäne enthält nur, was Gesten geschrieben haben; `Program.cs` und der Quell-Treiber sind unverändert.
- `--check` meldet „Code ohne Karte = 0“, und keiner dieser Bausteine ist eine bloße Code-Karte.

---

## 14 · P8 — Simulation raus (✅ 2026-10-13, §17)

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
| Prüfstand | 435/435 grün, 8 s | P4 (2026-10-12) |
| Integration (sequentiell, echte Infra) | 33/33 grün, 63 s | S0 (2026-10-12) |
| GraphExtractor kalt (Default-Lauf) | 13,8 s (nicht ≈ 1 min, wie die UI behauptete) | S0 (2026-10-12) |
| `--check` / `--sonde` | 17,7 s / 23,2 s, beide grün | S0 (2026-10-12) |
| `dotnet build CqrsSolution.sln` (inkrementell, nichts geändert) | 14 s | S0 (2026-10-12) |
| Prüfstand / Integration | 443/443 (9 s) / 39/39 (96 s) | P5 (2026-10-12) |
| Prüfstand / Integration | 448/448 (7 s) / 39/39 (97 s) | P6 (2026-10-12) |
| Prüfstand / `--check` / `--sonde` | 453/453 (8 s) / ✅ / ✅ | P7.1 (2026-10-12) |
| GraphExtractor kalt (CLI): Laden / Übersetzen / Lesen | 4,0 s / 6,1 s / 1,2 s (Lesen vorher 4,3 s: Typ-Suche ohne Metadaten, Implementierungs-Index) | P7.1 (2026-10-12) |
| Warmer Workspace, Lesen ohne Änderung (nur Domänen-Compilations, E7) | 106 ms (Routing 3, Typen 2, Extraktion 83, Board 16) | P7.1 (2026-10-12) |
| Domänen-Recompile nach Text-Änderung (warmer Workspace) | p50 **224 ms** Übersetzen + Lesen (Übersetzen ≈ 105 ms, Lesen ≈ 120 ms; min 220, max 342; vorher 1,6 s) — `GraphExtractor -- --warm` | P7.1 (2026-10-12) |
| SimHost: kaltes Laden des Workspace beim Start / warm nach einer Datei | 10,2 s + 1,2 s Lesen / 108–128 ms + 120 ms | P7.1 (2026-10-12) |
| ⟳ „Neu lesen“ | 0 ms ohne Änderung, sonst ≈ 250 ms (vorher 13,8–55 s als Prozess) | P7.1 (2026-10-12) |
| Prüfstand / `--check` / `--sonde` | 471/471 / ✅ / ✅ | P7.2 (2026-10-12) |
| Geste einzeln im Browser (Rechner wenig belastet) | Feld anlegen 330–455 ms, Aggregat anlegen 225 ms (3 Dateien), Aggregat löschen 288 ms, ⊕ Decider-Ausgang 1032 ms | P7.2 (2026-10-12) |
| Geste p50 / p95 (API-Serie, 20 Gesten über 5 Dateien + Decider-Ausgänge, je mit ↶) | **979 ms / 1251 ms** — Schreiben 110–310 ms, Lesen (warm) 670–1040 ms, Befunde < 60 ms; gemessen unter Fremdlast (Load 5–13), dieselbe CLI-Messung `--warm` stieg dabei von 209–224 ms auf ≈ 1 s | P7.2 (2026-10-12) |
| Rename über alle Projekte (Roslyn-Renamer, inkl. Prüfstand-Test und eingechecktem Wire-Kontext) | 2,05 s (4 Dateien, Rechner wenig belastet); 5,6 / 6,6 s (10 Dateien, unter Fremdlast) | P7.2 (2026-10-12) |
| Prüfstand / `--check` / `--sonde` | 476/476 (10 s) / ✅ 14,7 s (Server-Kartenliste: 410 Karten, 728 Kanten, Code ohne Karte 0, leere Geste ohne Patch) / ✅ 18,6 s (102 Fakten, 4 Gesten → 10 Dateien im Fork) | P7.3 (2026-10-12) |
| Sicht (Ableitungen) beim Lesen | warm 26 ms (eigene Phase; Board 17 ms, Lesen gesamt 130 ms, vorher 106 ms); kalt im SimHost 56 ms; Text-Änderung p50 250 ms Übersetzen + Lesen (`--warm`, Load 3,5; P7.1: 224 ms) | P7.3 (2026-10-12) |
| `board.sicht` | 166 KB von 516 KB Board (kompakt): 410 Karten, 728 Kanten, 35 zusammengezogene, 103 Ketten-Pfade | P7.3 (2026-10-12) |
| ⊕ `POST /api/werkbank/passt` / Fokus `GET /api/werkbank/kette` | ≈ 57 ms je Port (2838 Kandidaten, Body ≈ 300 KB, Load 3–4) / ≈ 4,5 ms je Karte | P7.3 (2026-10-12) |
| Parität JS ⇄ Server vor dem Löschen | Sicht: 410 Karten in Detail- UND Kompakt-Ansicht gleich (Domäne, Block, Akteure, Akteur-Rahmen, Inseln, Rolle, Vertreter, Store-Scope, Mitgliedschaft, Tiefe, Kanten in gleicher Reihenfolge, zusammengezogene Kanten, Kette, Akteur-Reihenfolge, Fluss-Heimat, Clients, Pfade, Akteur-Domänen); ⊕: 2679 Ports × 2838 Kandidaten gleich; Fokus-Kette: 410 Karten × 2 Ansichten gleich | P7.3 (2026-10-12) |
| `--check` mit Code-Karten-Gate | ✅ 14,9 s (Load 4): 419 Karten, Code ohne Karte 0 bei 307 Typ-Deklarationen, 9 Code-Karten = 2,1 % (`DomainPipelineExtensions`, `HistogramEqualizer`, `IHistogramEqualizer`, `IImageResizer`, `ImagePairFileName`, `ImageResizer`, `MartenCoCommitStoreBase`, `SplitZuteiler`, `Zielpfad` — die Liste aus §1.3 ohne das gelöschte `DateiQuelle`) | P7.4.1 (2026-10-12) |
| Regeln (Analyzer) bei ⟳ über den warmen Stand | 0,7–1,0 s für „Neu lesen“ samt Analyzer aller Domänen-Projekte + Gesamtbild (12 Warnungen: 7× CQRS081, 4× CQRS077, 1× CQRS085; 0 Fehler) — vorher im Build nur sichtbar, wenn das Projekt neu übersetzt wurde | P7.4.2 (2026-10-12) |
| LLM-Prüfung probeweise im warmen Workspace | 61 ms (Decide, ok), 189 ms (Decide mit CS1729), 73 ms (Regel-Lambda mit CS0029) über `/api/llm/pruefen`; CLI `--pruefe-rumpf` 13–15 s (kalter Workspace) — vorher Modell-Kompilat der Simulation bzw. eigene Compilation je Store-Projekt | P7.4.3 (2026-10-12) |
| Geste „+ Feld“ samt Regeln der berührten Datei | 435 ms (Schreiben 109, Lesen 307, Befunde inkl. Analyzer 19; Load ≈ 6) | P7.4.3 (2026-10-12) |
| Prüfstand / Integration | 479/479 (21 s, −19 Validator-Tests, +21 Analyzer-Tests) / 39/39 (1 min 39 s) | P7.4.2 (2026-10-12) |
| Rumpf-Geste (✎ am Rumpf-Port) | C#-Decide: 1159 ms (Schreiben 10, Lesen 798, Befunde inkl. Analyzer 351; Load ≈ 7); Python-Zusage: 297 ms (Schreiben 6, Lesen 154 — `.py` liest das Board neu, Befunde 137) | P7.4.4 (2026-10-12) |
| Prüfstand / `--check` / `--sonde` | 484/484 (19 s, +5 Rumpf-Gesten-Tests) / ✅ / ✅ | P7.4.4 (2026-10-12) |
| Abnahme: Event umbenennen | `ImagePairDateiGedeutet` → 7 Dateien (Ereignis, Funktions-Signatur, alle `Bei<…>()` in den Fluss-Lambdas, Prüfstand-/Integrations-Tests, eingecheckte Wire-/Event-Kontexte) in 4,8 s; danach `dotnet build Host.Grpc` grün (35 s, der Codegen-Prepass zieht `domain.proto` und die Python-Basis nach); ↶ + Bau → `git status` sauber. Nicht erfasst: zwei String-Literale `"ImagePairDateiGedeutet"` in `EingangE2ETests.cs` (JSON-Name als Text, kein Symbol) | P7.4 (2026-10-12) |
| Abnahme: Geste p50 / p95 (API-Serie: 15× „+ Feld“ an 5 Records in 5 Dateien, 5× „+ Ausgang“ am Decider, je mit ↶) | Load 16,6→15,1: **346 / 849 ms** (min 205); Load 5,9→6,2: **470 / 736 ms** (min 360) — Schreiben 40–120 ms, warmes Lesen 250–520 ms, Befunde inkl. Analyzer 18–131 ms. Fremdlast auf dem Rechner: Steam + zwei Spiele (≈ 2,3 Kerne). Ziel < 300 ms (p50) unter dieser Last nicht erreicht; P7.2 (gleiche Serie): 979 / 1251 ms bei Load 5–13 | P7.4 (2026-10-12) |
| Prüfstand / Integration / `--check` / `--sonde` | 507/507 (14 s) / 39/39 (1 min 38 s; ein erster Gesamtlauf hatte 2× `SequenceGapPostgresTests` rot — einzeln und im Wiederholungslauf grün, Reihenfolge-Flake ohne Bezug zu Diensten) / ✅ 416 Karten, Code ohne Karte 0, Code-Karten 9 → 4 = 1,0 % / ✅ 111 Fakten | P7.5.1 (2026-10-13) |
| Prüfstand / Integration / `--check` / `--sonde` | 509/509 (18 s) / 39/39 (1 min 43 s; `EingangE2ETests` mit Webhook aus der Konfig) / ✅ (Module-Ports 175 → 178: Quell-Nachrichten kommen immer von außen) / ✅ 111 Fakten | P7.5.2 (2026-10-13) |
| Prüfstand / Integration / Python / `--check` / `--sonde` | 513/513 (15 s) / 39/39 (1 min 42 s; Fluss-E2E mit dem generischen Python-Worker) / Worker 20/20, SDK 28/28 / ✅ / ✅ 111 Fakten | P7.5.3 (2026-10-13) |
| Abnahme P7.5: Vorgang Datei-Drop → `StandErfasst` | 0,40 s von der Quell-Nachricht bis zum Event (Quelle → ƒ ILeseZaehler in Python → ƒ IPruefeStand in C# mit Dienst → Decide), 6 Einträge über 4 Streams; der Datei-Treiber hat das erst durch den Drop angelegte Verzeichnis erst nach seinem Backoff (≈ 30 s) gesehen. Webhook: `202` in einigen ms, zweimal dieselbe Nachricht = derselbe Vorgang | P7.5 (2026-10-13) |
| Prüfstand / Integration / Python / `--check` / `--sonde` | Zielbild: 515/515 (16 s, +2 Gesten-Tests) / 39/39 (1 min 41 s) / Worker 20/20, SDK 28/28 / ✅ 416 Karten, Code-Karten 4 / ✅; Abnahme-Branch mit Domäne `Zaehler`: `--check` ✅ 452 Karten, 768 Kanten, Code ohne Karte 0, Code-Karten 4 (keine aus `Zaehler`), Prüfstand 511/515 (3 Bestands-Zählungen verschieben sich mit der neuen Domäne, 1 Timing-Ausreißer unter Load 5,3 — im Wiederholungslauf grün) | P7.5 (2026-10-13) |
| Prüfstand / Integration / `--check` / `--sonde` | P8.2: 515/515 (20 s) / 39/39 (1 min 41 s) / ✅ 15 s / ✅; P8.3 nach dem Umbenennen: 515/515 (14 s) / — (keine Laufzeit berührt) / ✅ / ✅ — Load 7–10 | P8 (2026-10-13) |
| Löschumfang P8.1 + P8.2 | 1325 Zeilen weg (ModellSimulation 556, Musterwerte 99, DslSchreiber + Abdeckung, Endpunkte, LLM-Simulieren; `editor.js` 4783 → 4509 Zeilen, `editor.css` 680 → 634); SimHost ohne Core/Cqrs.Testing/Domain-/Projections-Generatoren: Warnungen beim Bau von SimHost 125 → 0 | P8 (2026-10-13) |
| Browser vor/nach (Alles laden) | 601 Karten / 742 Kanten vor P8 = nach P8.2 = nach P8.3; Kopfzeile ohne „▶ Simulation“, keine Konsolen-Fehler | P8 (2026-10-13) |
| ⟳ / Datei-Drop / Geste | ⟳ grün (Bauen 3,4–3,6 s, Neu lesen 0,4–0,6 s, Host läuft gegen `cqrs_sim_claude_werkbank_zielbild`); Drop auf `DateiErkannt` → Vorgang ⛓ Bildeingang fertig (3 Einträge, 2 Streams), in ☰ Vorgänge sichtbar; Geste „+ Feld“ 497–524 ms, ↶ 240–298 ms, danach `git status` ohne Domänen-Änderung (Load 7–9) | P8 (2026-10-13) |
| P7.4.5-Rest: Parität JS ⇄ Server vor dem Löschen | 442 Fälle, 0 Abweichungen: 139 Nachrichten × ein/aus (Karte, Text, Marke, ✕ ja/nein; 120 `loesen`-Beschreibungen gegen die Karte geprüft, 8 Arten), 5 Flüsse × Slice, 16 Knoten × Ports/Vorgänger/Tiefe/gehörte Events/Zuweisung/Draht-Typen, 22 Drähte (Typ = Typ des Quell-Ports), Katalog-Sorten; danach im Browser vor/nach: 601 Karten, 742 Kanten, Karten-Text aller 601 Karten und `deHerkunft` (139) gleich; ✕ „darf“ und ✕ Draht als Geste + ↶ sauber | P7.4.5 (2026-10-13) |
| `board.sicht` mit Partnern und Fluss-Sicht | 236 KB von 639 KB Board (P7.3: 166 von 516 KB); `editor.js` 4509 → 4451 Zeilen; Prüfstand 518/518 (+3 Sicht-Tests), `--check`/`--sonde` ✅ | P7.4.5 (2026-10-13) |
---

## 17 · Fortschritt

| Phase | Stand | Was konkret |
|---|---|---|
| S0 | ✅ 2026-10-12 | Grundlinie gemessen (§16). Editor-Oberfläche aus `GraphExtractor/HtmlPresenter.cs` (gelöscht) nach `SimHost/wwwroot/editor/` (`index.html`, `editor.css`, `editor.js`), SimHost liefert statisch (no-store). `LoadHarness`-Reste (CLAUDE.md, `deploy-multinode`) entfernt |
| P0 | ✅ 2026-10-12 | Server (`ModellMapper.ZuBoardJson`) liefert keine `pipelines`/`triggers`/`selbstNachrichten`/`dienste`/`hostSettings` mehr; Quell-Webhooks bleiben als Bindung am Fluss. JS: Trigger-, Handle-Pipeline-, Frist-, Dienst-, HostSetting-Karten samt Kanten, Ports, Ableitungen, Umbenennen/Löschen entfernt; Landkarte/LOD entfernt (die Karten-Darstellung ist fest: Klasse `ablauf`); Palette und Doppelklick-Auswahl = Anker (Aggregat · Fluss · Funktion · Quelle · Akteur · Client · Projektion); „Quelle" legt bis P1 einen `IQuellNachricht`-Record mit `Kennung` an. Das Domänen-Menü „＋ in Domäne" bietet weiter alle Bausteine der Domäne an (ohne Trigger-Welt) — das ist der Weg für „Zugehöriges anlegen", solange nicht jede Art einen „＋"-Port hat. Sonde: die Trigger-Welt-Konstrukte (`MahnlaufPipeline`, `Vormerkpruefung`, Trigger, Selbst-Nachrichten, `IAkteurDienst`) entfernt, `soll.txt` nachgezogen. `Grammatik.cs`/`Validator.cs` unverändert (fallen in P7). Belegt: `--check` ✅, `--sonde` ✅ (78 Fakten), Prüfstand 389/389, Browser: 580 Karten, keine Trigger-Welt, Akteur-Parität JS = C#, ⊕-Modus leuchtet |
| P1 | ✅ 2026-10-12 | **Codegen:** `TypeNode.ViaInitialisierer` — alle vier Analysatoren (Reflection/Codegen, Roslyn/DtoMapper, Proto-Tool) lesen ohne Ctor-Parameter die public init/set-Properties (auch geerbte, Basis zuerst); der DtoMapper baut per Objekt-Initialisierer. Nebenbei behoben: der Record-Kopier-Konstruktor landete als Feld `original` im Proto. **Abstractions:** `DateiGemeldet`, `Treibervertrag` (verlinkt, in Generatoren `internal`), `IStreamBestand`; `IQuelle`/`IQuelle<T>` gelöscht (damit entfällt CQRS071 — es gibt nichts mehr zu implementieren). **Infrastructure:** `QuellenGenerator` → `GeneratedQuellen`; Datei-Treiber (`DateiScan` store-frei, `DateiTreiber`), `DateiQuellKonfig` per Binding-Source-Generator (strikt), `AddQuellen` mit Start-Prüfungen, `QuellenDienst` mit Lease (`quelle`-Kind, `QuellenLease`) für `EinmalImCluster`, `QuellenZustand` + Health-Check, `MartenStreamBestand`. **Analyzer CQRS070.** **Bestand:** `DateiErkannt : DateiGemeldet`, `DateiQuelle`/`FileWatchConfig` gelöscht, `AddDomainPipelineServices(preprocessedPath)`, Host `AddQuellen`, Deploy auf `Quellen__DateiErkannt__Pfad`; Prod-Deploy mit `Kennung=NameGroesse` (gleiche Vorgangs-Ids → kein Neu-Verarbeiten des Shares). `PreprocessedPath` bleibt bis P2 `Pipeline:PreprocessedPath` (E2 folgt mit der Umgebungs-Datei). Erkennungs-Wert heißt `EreignisUndAbfrage` (Enum statt „Ereignis+Abfrage"). **Editor:** Treiber und geerbte Felder als Code-Fakt (`Record.Treiber`, `Feld.Geerbt`), Quellkarte „📁 Datei-Treiber" mit Konfig-Werten je Host (`rahmen.quellenKonfig`, bis P2 aus `appsettings.json`), Anker „Quelle" legt `sealed record X : DateiGemeldet` an (`rahmen.treiber`). Python-DTOs regeneriert (ohne das scheitert der Python-Worker-Test — gehört in die ⟳-Kette, P3). Belegt: Prüfstand 409/409 (20 neu), Integration 34/34 (neu: Treiber → Vorgang, nach Neustart keine zweite Meldung), `--check` ✅, `--sonde` ✅ (79 Fakten, neu: Datei-Quelle) |
| P2 | ✅ 2026-10-12 | **Umgebungs-Typen** in `Abstractions/Umgebung/UmgebungKonfig.cs` (zugleich das Formular-Schema für P3). **Lader** `Infrastructure/Umgebung/UmgebungLader.cs`: `--umgebung`/`CQRS_UMGEBUNG`, Datei neben dem Programm oder aufwärts, Env/Argumente überschreiben, `${env:}`/`${branch}`, nur bekannte Abschnitte, strikte Bindung. **`AddCqrs(args)` + `LaufeAsync`** ersetzen die Host-Verdrahtung (Host.Grpc/Program.cs: 3 Zeilen + Webhook). **Siegel** (`<schema>.umgebung`, Name/Art/Cluster; Sim-DB wird bei Bedarf angelegt), **Uhr** (`<schema>.uhr`, `UmgebungsDbClock`, Versatz nur Sim), **Redis-Präfix** (`RedisPraefix`, KeyspaceIsolation), **Einzelknoten-Cluster** (Proto `TestProvider`), **Einblick-Port** HTTP/1.1. **Funktions-Laufort als Konfig:** `FunktionsGenerator` erzeugt `Namen`, `Implementierungen` (generische Bindung je Impl) und `Extern`; `BindeFunktionen` prüft gegen die Tabelle; `Hand` folgt in P5. **Konfig-Records der Domäne** unter `Konfig:<Record>` (abweichend von E2: ein Abschnitt je Konfig-Record statt je Funktion, weil sich zwei Funktionen `PreprocessingConfig` teilen; `AddDomainPipelineServices(IConfiguration)` bleibt als eine Zeile im Programm — Domänen-DI ist Code). Umgebungen `sim-main`, `prod`, `multinode`; Deploy (`deploy.sh`, systemd-Env, Multi-Node-Compose, `build.sh` → `umgebung.linux.json`, `start-server.sh`) auf `--umgebung`. **Extractor** liest `umgebungen/*.json` (`rahmen.umgebungen`) statt appsettings; Quellkarte zeigt Werte je Umgebung. Belegt: Prüfstand 420/420 (11 neu), Integration 37/37 (neu: Siegel-Konflikt, zwei Umgebungen parallel inkl. Redis-Präfix, Versatz verschiebt Frist), Host-Smoke mit `--umgebung sim-main` (DB angelegt, Siegel, Einzelknoten, `/health` über HTTP/1.1, Quelle erholt sich). **Nicht gefahren:** Multi-Node-Compose und Prod-Deploy (nur umgestellt). |
| P3 | ✅ 2026-10-12 | **Werkbank-Server** (`SimHost/Werkbank/`): ⟳-Kette `KreislaufAsync` (Bauen → Python-Basen → Neu lesen → Daten prüfen → Infrastruktur → Host ersetzen; Prod endet nach dem Lesen), Compiler-Befunde mit Typ/Member (`Diagnosen`), `HostProzess` (Start/Ersetzen, `/health`, Boot-Zeile `CQRS-BOOT {json}` mit Karte, Stopp beim Ende von SimHost), `Infra` (Compose `cqrs-<umgebung>` für verwaltete Dienste), `SimDatenbank` (zurücksetzen, Stand speichern/laden/löschen mit Gültigkeit aus dem Proto-Vergleich, Prod-Stand übernehmen, Uhr; ohne Pool, übrige Verbindungen werden getrennt), `Fortschritt` (SSE `/api/werkbank/ereignisse`), Endpunkte `/api/werkbank/*` (zustand, schema, umgebung, aktiv, kreislauf, diagnosen, host, simdb, uhr, infra). **Daten prüfen / E9:** `Abstractions/Kodehash.cs` + `Domain.SourceGeneration/Kodeform.cs` (Token-Folge ohne Leerraum/Kommentare); `CodeHash` im Aggregat-Handler (State + Applier) → Snapshot-Version; `CodeHash` in jeder Projektion, `StoreStand`/`ReadModelTyp` aus dem `ProjectionServicesGenerator`, `ProjektionsStand` aus dem `PullPathGenerator`; `ProjektionsStandDienst` (Sim: Leseseite leeren + neu aufbauen, Prod: Meldung); Store-Basis nur noch `IReadModel`; erster Poll nach 2 s. **Editor:** Kopfzeile (Umgebung ▾ · Build mit Stand · Lauf inkl. „Entwurf ≠ Lauf"/„Konfig geändert" · Postgres/Redis/Consul · ⟳ · 📸), 🖥 Host-Rahmen rechts neben den Clients mit **einem generischen Formular-Renderer** aus `/api/werkbank/schema` (klasse/je/frei, enum/bool/zahl/dauer/liste/map; Speichern schreibt die Umgebungs-Datei), Uhr-Karte, Befunde und Boot-Fehler an der Karte (⛔ + Panel), Meldungen/Host-Log im Ausgabe-Panel; Knöpfe Prüfen/Kompilieren/Alle ausführen entfernt. Belegt: Prüfstand 423/423 (3 neu), Integration 38/38 (neu: Leseseite baut nach Code-Änderung in Sim neu auf, sonst nicht), `--check` ✅, `--sonde` ✅. **Browser-Abnahme:** CS0029 in `Decide(FuegeRangeHinzu)` → am Decider; nach Korrektur ⟳ grün, Host gegen `cqrs_sim_claude_werkbank_zielbild`; Stand speichern 12,8 s / laden 14,0 s (Kopie < 1 s, Rest = Host anhalten 2 s + Boot), Events zurück; `IRangeSuche` aus der Umgebung entfernt → Boot-Fehler an der Funktions-Karte; Prod-Stand übernehmen 8,9 s (2028 Events), Host siegelt auf Sim und baut 5 Projektionen neu auf. Neu lesen dauerte unter Last 55 s (S0: 13,8 s). |
| P4 | ✅ 2026-10-12 | **Schnitt 1 — Namen und Ids (P4.1, P4.2, P4.8):** `Abstractions/Spur/WirkerId.cs` (acht Formen, Regel-Name nach E4 `A+B→C`, Count-Join `A+S*→C`, Knoten-Namen inkl. anonymer Knoten; per Link im Domain-Generator `internal`). `ProzessRegelnGenerator` liest die Namen aus dem Quelltext (Fluss: Anweisungen im `Definiere`-Lambda; Prozess: `Sende`/`SendeJe`/`Rufe` der Regel-Bauer, Bedingung aus deren Typ-Argumenten) und emittiert `….Regeln.Benannt("P", …)`; **CQRS072** bei gleichem Regel-Namen. `ProzessRegeln.Benannt/Name/KnotenNamen/RegelNamen/Wirker(i)`; falsche Namenszahl = Boot-Fehler. Nebenbei behoben: die Registry rief `new X().Regeln` — bei expliziter Interface-Implementierung kompilierte das nicht (jetzt Cast aufs Interface; die Sonde meldet Fehler in generierten Dateien nicht). `FlussLeser` benennt über `WirkerId`. **Stabile Karten-Ids:** `ModellMapper.StabileIds` setzt `_id` = Wirker-Id für Decider, Fluss-Knoten, Prozess-Regeln, Funktionen, Akteure, Projektionen, Reaktionen; natürliche Schlüssel für Applier, Clients, Pipelines, Reader, Stores, Store-Fns (`wf:IStore.Methode`), ReadModels (statt `pj1`/`rk1`/`rd1`/`st1`/`wf_i_j`). JS übernimmt die Regel-Id in die Transition, State-Id `state:<Agg>`. **Befunde über die Id:** `SimHost/Werkbank/Karten.cs` ordnet Typ + Member über das gelesene Board einer Karten-Id zu (`Diagnose.Karte`); das JS sucht nur noch per `karteVonId` (Fallback Name nur für noch nie gelesene Typen). **`--check`:** Bereich „Wirker“ vergleicht die Namen der generierten Registry mit den Karten-Ids (17 = 17). Belegt: Prüfstand 429/429 (6 neu: Format, Regel-/Knoten-Namen, Laufzeit-Namen der Domäne, Generator-Probe, CQRS072), `--check` ✅, `--sonde` ✅ (fand den Fehler mit expliziter Implementierung), Browser: CS0029 in `Decide(FuegeRangeHinzu)` → `decide:Datensatz/FuegeRangeHinzu` → ⛔ an der Decider-Karte · **Schnitt 2 — Stempeln und Zurücklesen (P4.3, P4.4 Envelope):** `Abstractions/Spur/Spur.cs` (Header `ursache`/`wirker`/`stand`, fluss-lokaler Kopf nach dem Muster `ImAuftrag`, `Ursache = Stream:Version`). Gesetzt von: Aggregat-Actor (`decide:<Agg>/<Cmd>`, Ursache aus dem Envelope), `CommandEmitter` (Envelope `Ursache`/`Wirker`), `ProzessManager` (Ursache = jüngster echter Match-Token, −1/−2 übersprungen; Wirker = Regel bzw. Fluss-Knoten; auch Kompensation), `FunktionsAusfuehrer` (`funktion:F`; Ursache reist im `AuftragAnbieten` über Vermittler und Python-Sitzung — der Worker sieht sie nie, ein Proto-Feld war unnötig), `QuellEingang` (`quelle:T`, Wurzel), `HandlerOutputRouter` (`reaktion:T`, vom Pull-Pfad-Generator), `CqrsClientService` (`akteur:A`; Ursache aus den Kausal-Feldern einer Zusage). Geschrieben in `MartenEventStore` (je Session), `MartenEventBatchWriter` (je Event; `BatchingEventAppender` hält die Spur beim Einreihen fest), gelesen in `ReadStreamAsync` (`EventEnvelope.Ursache/Wirker/Stand`, auch Live-Hüllen). **Stand:** Host.Grpc schreibt beim Bauen aus `-p:CqrsStand` eine Konstante (`obj/CqrsStand.g.cs`, ModuleInitializer, keine Reflection); die Werkbank baut mit ihrem Kopfzeilen-Stand. **Offener Punkt aus P3 erledigt:** der Host schreibt beim Start in JEDER Umgebung `proto` (als Ressource eingebettet) und `code` in `<schema>.stand`; die Werkbank schreibt das Proto nicht mehr selbst, „Prod-Stand übernehmen“ behält das Proto der Herkunft. Wire-Kontext/Proto: keine Drift (nur Properties an bestehenden Typen). Belegt: Prüfstand 434/434 (5 neu, u. a. lückenlose Ursache-Kette über Funktionen und Commands in der Fluss-Welt), Integration 38/38 (`Bildeingang_Quelle_bis_Fakt…` läuft die Kette im echten Log von `BildVerfuegbar` bis `quelle:DateiErkannt`) · **Schnitt 3 — Einblick (P4.4–P4.7):** `Infrastructure/Einblick/`: `SpurLeser` (`LeseAbSeqAsync(seq, n)` über `mt_events.seq_id` samt Headern, `VorgangAsync`, `VorgaengeAsync`), `SpurHinweise` + `SpurHinweisSchreiber` (`<schema>.spur_hinweis`, gebündelt, 7 Tage, nur Sim; Arten `ablehnung` und `transient`), `SpurSignal` (vom Appender nach jedem Commit), `EinblickEndpunkte` nur auf dem Einblick-Port: `/einblick/stand`, `/spur?ab=`, `/vorgaenge`, `/vorgang/{korrelation}`, `/auftraege` (Vermittler-Nachricht `ListeOffene` → `OffeneAuftraege`, L13), `/hinweise?ab=`, SSE `/neu` („neu bis seq“, geweckt vom Appender, sonst alle 5 s). **Abweichungen:** (1) Die „Spur-Projektion“ ist keine gespeicherte Projektion, sondern eine Abfrage über das Log — Knoten = Einträge, Kanten = `ursache`; dasselbe in Sim und Prod, keine zweite Wahrheit. Ein Vorgang = eine `CorrelationId`; startet ein Prozess aus einer Quell-Nachricht, ist deren Eintrag die Wurzel (über `ProzessGestartet.AuslöserStream`) und erscheint nicht als eigener Vorgang; die Einträge des Prozess-Managers tragen keine `CorrelationId` (sein Stream ist die Korrelation) — die Abfrage leitet sie ab, die Laufzeit bleibt unverändert. Zustand: `ProzessBeendet` → fertig/gescheitert, sonst läuft (Aktivität < 5 s) bzw. wartet. (2) „Welche Projektion was verarbeitet hat“ (Tracker-Marken) und der Prod-Index auf den Correlation-Header sind offen (P6 bzw. bei Bedarf). (3) ⏳ und Weckrufe kommen nicht in `spur_hinweis`: ein Zeitlimit steht als `SchrittGescheitert`/`SchrittUmgeleitet` im Log, Weckrufe wären Rauschen. **Gefunden mit dem Einblick (vorbestehender Fehler, behoben):** der Abholer parkte `HoleArbeit` 10 s, der Cluster wiederholt eine Anfrage aber nach 5 s — der Vermittler gab neue Arbeit an die tote erste Anfrage, der Auftrag hing bis zum Lease-Ablauf (in `sim-main` blieb jeder Bildeingang nach der Deutung stehen). Jetzt ersetzt eine neuere Anfrage desselben Arbeiters die geparkte, und Abholer/Python-Sitzung warten 4 s. Wire-Kontext: `ListeOffene`/`OffeneAuftraege` im Codegen-Kopf (28 Framework-Typen). Belegt: Prüfstand 435/435, Integration (Einblick im Bildeingang-E2E: Vorgang fertig mit Wurzel `quelle:DateiErkannt`, Graph ab der Wurzel), Werkbank live: zwei Dateien in `sim-main` → beide Vorgänge fertig, `/einblick/vorgang/…` zeigt die Kette mit Stand `610f342a`, SSE meldet `bis`, gRPC-Port bedient `/einblick` nicht |
| P5 | ✅ 2026-10-12 | **Host:** JSON-Eingang `/eingang/*` hinter dem Akteur-Tor (Command mit Antwort, Query, Rechte je Token), Laufort `Hand` (`HandAusfuehrer` über die Anbieter-Sitzung, `/eingang/auftraege`, `/eingang/ergebnis`), `Transport:JsonEingang`, `Funktionen:<F>:Worker`; Wire-Kontext mit Queries/Antworten und Enums als Namen; Akteur `Bildrechner`; Python-Worker `funktions_worker.py`. **Werkbank/Editor:** Konsole als Akteur mit Formularen aus dem Code, ⧗ Aufträge an der Funktions-Karte, Datei-Drop auf die Quellkarte, ⏩, ▶ Worker starten/stoppen + Log. Belegt: Prüfstand 443/443 (8 neu: JSON ↔ Nachricht, Laufort Hand, Umgebungs-Dateien binden streng), Integration 39/39 (neu: `EingangE2ETests` — Command/Ablehnung/403/401/Query als Akteur, Hand-Antwort lässt den Bildeingang bis `ImagePairErstellt` laufen), `--check` ✅, `--sonde` ✅, Python 15/15. **Browser-Abnahme (sim-main):** als KIOperator `ErstelleDatensatz` → `DatensatzErstellt`, nochmal → Ablehnung `DatensatzExistiertBereits`, `HoleDatensatz` → `DatensatzAntwort`; IDateinameDeutung auf Hand, IBildVerkleinerung auf Extern, ⟳; ▶ Worker startet den Python-Worker (Token `sim-bildrechner`, Funktion angeboten); Bild auf die Quellkarte fallen gelassen → Treiber meldet → ⧗ 1 an der Karte → Antwort im Panel → Python verkleinert (683×512) → C# gleicht aus → Vorgang `fertig` mit Wurzel `quelle:DateiErkannt`; ⏩ +1 Tag setzt den Versatz der Sim-DB |
| P6 | ✅ 2026-10-12 | **Host:** Header `ueber` (Absender) an Aggregat- und Funktions-Einträgen, Wirker an `SchrittGescheitert`/`SchrittUmgeleitet`, `SpurEintrag.Name`/`Ueber`, abgelehnter Command von außen = gescheiterter Vorgang. **`SpurWeg.Aus`** (Abstractions, rein). **Werkbank:** `SpurSpiegel` (SSE + Cursor → Puls; Vorgänge mit Filtern/Facetten; Vorgang als nummerierter Weg mit Bahnen, Hinweisen, offenen Aufträgen). **Editor:** Leiste unten ☰ Vorgänge (Filter Quelle/Akteur/Aggregat-Id/Zustand, Weg-Liste) · ⏱ Zeitleiste (Bahnen je Baustein, Schieber + ▶ Abspielen) · ⚠ Meldungen (⟳-Schritte, Befunde/Boot → Karte) · 〰 Puls an/aus; Spur-Überlagerung (Karten/Kanten orange, ①②③ an Karte und Kante mit +Δt, ✕/⏳ rot, ältere Stände grau, Verlierbares gestrichelt, ⧗ pulsiert), Schritt-Panel (Payload, Knoten, über, Akteur, Stand, ↤/↦, „⚠ Karte gibt es nicht mehr“); Live-Puls (Zähler + „zuletzt“, Kantendicke, Stau, nie benutzte Kanten blass); EIN Fokus (Chip, ersetzt Slice/◎/⤢). Belegt: Prüfstand 448/448 (5 neu: Absender, `SpurWeg`), Integration 39/39 (Bildeingang: `ueber` am Fakt und an jedem Funktions-Ergebnis; Eingang: Ablehnung = gescheiterter Vorgang mit Absender `akteur:KIOperator`), `--check` ✅, `--sonde` ✅. **Browser (sim-main):** Datei abgelegt → Vorgang ①–⑥ von `quelle:DateiErkannt` über den Fluss bis `BildVerfuegbar`, alle vorhandenen Kanten getroffen (inkl. Draht `datei → deuten`), Schieber auf ③ blendet ④–⑥ aus; Ablehnung als KIOperator → ✕ am Decider; ohne Vorgang Puls an 21 Karten, Klick auf eine Karte = „◎ Kette“ |
| P7.1 | ✅ 2026-10-12 | **Bibliothek + CLI:** `GraphExtractor.Kern` (alles außer `Program.cs`, Sonde samt Ressourcen; `RootNamespace` bleibt `GraphExtractor`), die CLI `GraphExtractor` ruft ihn; SimHost referenziert den Kern (statt `Codeformen.cs` zu verlinken), SimHost/Prüfstand auf Roslyn 5.3 (Marten zieht 4.14 — `Microsoft.CodeAnalysis`/`Scripting` gepinnt, sonst NU1608). **Kein statischer Zustand:** `Quelltexte` (handgeschrieben = Dokument der Solution) je Solution-Stand statt des wachsenden `Projektlage.Dokumente`; durchgereicht an DomainExtractor, RoutingTruth, Parität, Sonde, SlotInventar, KontextBauer, CompositionRoot. **Ein Lesepfad:** `Lesen.LiesAsync` → `Lesung` (Routing, Dom, Graph, Composition Root, Board als Knoten, Phasen-Zeiten) für CLI und Werkbank. **Warmer Workspace** `SimHost/Werkbank/Arbeitsbereich.cs`: MSBuildWorkspace einmal kalt (beim Start im Hintergrund), FileSystemWatcher über die Solution → `WithDocumentText`/`AddDocument`/`RemoveDocument` nur im Speicher (gleicher Inhalt = kein neuer Stand), `.csproj`/`.props`/`.sln` → kalt neu; warm gelesen wird nach **E7** nur aus den Compilations unterhalb der Laufzeit (`Projektlage.NurDomänenAsync`), Routing aus den Decide-Signaturen (`RoutingTruth.DecideSignaturen`, keine Warnung), Composition Root aus dem kalten Lesen. Quelle für `/api/editor/model`, `/api/editor/extract` (Kontexte zieht die Konsole danach im Hintergrund nach), ⟳ „Neu lesen“ und die Karten der Befunde (`Karten.Zuordnen` über `KartenIds.Zu` im Kern, eine Regel); schreibt weiter `domain-model.json` (Spur, Durchspielen, Konsole lesen die Datei). Neu: `GET/POST /api/werkbank/lesen` (Zustand, Messungen), SSE `gelesen`. `CodeSync.Extrahiere` (alter Prozess-Weg) gelöscht. **Gegenprobe E7 in `--check`:** Bereich „Routing“ vergleicht Decide-Signaturen mit `GeneratedCommandRouting` (31 = 31). **Fehlertoleranz (L12):** `Projektlage` wirft ohne Routing-Generat nicht mehr (Laufzeit = niedrigstes Projekt, das alle Aggregat-Projekte zusammenführt; Routing dann aus den Decide-Signaturen als Warnung); unaufgelöste Basistypen → `DomainModel.Unaufgeloest` → **Code-Karte ⚠** (`codeKarten`, Karten-Id `code:T`, roher Code, Grund) bzw. Befund an der vorhandenen Karte, ein Record mit unaufgelöster Basis wird **kein** Value Object mehr; `FlussRaw.Unbekannt` → Befund `LESEN-FLUSS` am Fluss; verworfene Prozess-Regel → `LESEN-REGEL`; doppelte Namen (`ToDictionary` im ModellMapper/GraphBuilder) → `LESEN-DOPPELT` statt Ausnahme. Lese-Befunde stehen im Board (`lesebefunde`) und kommen über `/api/werkbank/diagnosen` und ⟳ als Befunde an die Karte (Schwere „warning“). **Karten tragen Datei + Spanne:** `_ort {datei, start, ende, zeile}` an allen 290 Karten (Typen über `DomainModel.TypOrte`, Decide/Apply über ihre Methode, Fluss-Knoten und Prozess-Regeln über ihre Anweisung, Handles über ihre Methode). **Editor (nur ergänzt):** Karten-Art `code` (⚠ Code, gestrichelt, kein ✕, Panel = Grund + Ort + roher Code). **Schneller gelesen:** `Sym.AllConcreteTypes` nur über die eigenen Assemblies der analysierten Compilations (keine 37 000 Metadaten-Typen mehr), `ExtractStores` über einen Implementierungs-Index, „registriert im Generat“ bindet nur Stellen mit passendem Namen — Board und Graph inhaltlich unverändert (Vergleich gegen den alten Extractor aus HEAD). Belegt: Prüfstand 453/453 (5 neu: `LesenToleranzTests` — Code-Karte, Teil-Auflösung, Fluss-Befund, Doppelname, Datei+Spanne; In-Memory-Workspace), `--check` ✅ (neu: Routing-Gegenprobe), `--sonde` ✅, `--warm` ✓ (warmes Board = kaltes Board, neuer Typ erscheint warm). **Browser (SimHost):** Probe-Datei `record WarmProbeKaputt(string Name) : IEvnt` angelegt → warm gelesen (128 ms + 120 ms) → Code-Karte „⚠ Code · WarmProbeKaputt“ ⛔ 1 im Rahmen Datensatz, Panel zeigt `[LESEN-BASIS] … — Domain/Datensatz/WarmProbe.cs:6` und den Code; Datei gelöscht → Karte weg (290 Karten); ⟳ über die API: „Neu lesen“ 0 ms (Code unverändert). Integration nicht gefahren (Laufzeit unverändert). |
| P7.2 | ✅ 2026-10-12 | **Motor im Kern** (`GraphExtractor.Kern/Schreiben/`): `DateiSchreiber`/`DateiAbgleich` aus SimHost umgezogen; `Dateipuffer` merkt sich je Datei vorher/nachher (= Patch, Gegenstück für ↶, Datei löschen möglich). **Geste = Karten-Delta** (`KartenDelta`: geänderte/neue Karten ganz, entfernte als natürlicher Schlüssel — `KartenSchluessel`, dieselbe Regel im JS `GKEY`): `Gesten.Schreibe` wendet das Delta auf das Board des Code-Stands an, stempelt die Herkunft **aus dem Code-Stand** (`Gesten.Stempeln`, nie aus dem Browser) und schreibt über den bestehenden Schreiber (Scaffolder für Neues, Abgleich für Geändertes); **Löschen** an `_ort` + `_teile` (weitere partial-Deklarationen, getrennte Decider/Applier-Klassen, Funktions-Implementierung, Vertrags-Teile — neu im Extractor), samt Doku-Kommentar, Handles einer bleibenden Klasse an ihrer Methode; eine Datei ohne Typ entfällt. **Umbenennen** (`Umbenennen.Async`): Symbol am Ort der Karte (Typ, Fluss-Knoten-Variable) → Roslyn-`Renamer` über die ganze Solution, alle Verweise auch in Lambdas. **Behoben:** der Abgleich des Akteur-Vertrags schrieb unberührte Zusagen in die OneOf-Form um (`T Auf(E)` → `OneOf<T>`); jetzt nur bei anderen Ausgängen. **SimHost:** `GestenDienst` (eine Geste nach der anderen; nie auf `main`/`master`; Schreiben → Speichern → Verlauf → `Arbeitsbereich.UebernimmAsync` → Compiler-Befunde NUR der berührten Dateien aus dem warmen Workspace, bis zum nächsten ⟳ an der Karte; jede Geste gemessen und geloggt), `Verlauf` (`.werkbank/verlauf.<branch>.json`, ↶ nur, solange die Dateien unverändert sind; die 🤖-Übernahme der LLM-Konsole geht in denselben Stapel), Ansichts-Datei `.werkbank/ansicht.json` (Positionen, Domänen-Filter, Akteur-Reihenfolge, 🤖-Knoten). Endpunkte `POST /api/werkbank/geste`, `/umbenennen`, `/rueckgaengig`, `GET /api/werkbank/verlauf`, `/gesten` (p50/p95), `GET/POST /api/werkbank/ansicht`; `/api/werkbank/diagnosen` mischt je Datei Bau- und Gesten-Befunde. Neue Python-Client-Implementierungen legt jetzt ⟳ nach dem Bauen an (früher „C# schreiben“). **Entfallen:** `/api/editor/board`, `/api/editor/write`, `/api/editor/scaffold`, `board-model.json`, `CodeSync.Extrahiere`. **Editor:** nach jeder Änderung (render → `autosave`) rechnet der Editor das Delta seiner Karten zum zuletzt gelesenen Code-Stand und schickt es als Geste (250 ms entprellt; solange ein Eingabefeld den Fokus hat, wartet sie); das Board kommt vom Server zurück (bei weiteren Änderungen in der Zwischenzeit geht erst die nächste Geste). Karten aus dem Code werden über den Server umbenannt (Name im Panel, Fluss-Knoten), ↶ in der Kopfzeile. **Entfallen im JS:** Board-Persistenz (localStorage, `board-model.json`), `mergeBoard` mit `_herkunft`/„ungeschrieben“/Entwürfen (ersetzt durch `uebernimm`: nur Ansicht, Code-Knoten-Bindungen, 🤖), `deSave`, „💾 Speichern“, „</> C# schreiben“, „👁 Vorschau“, Alt-Format-Migrationen in `normalize`; Diagnose `deGestenDelta()`. **Abweichungen vom Plan:** (1) Die ~45 Zweige von `applyLink`/`loeseLink` bleiben im JS — sie ändern Karten, und die Karten-Änderung ist die Geste; der Server übersetzt sie in den Patch (Gesten-Katalog = die Tabelle in `konzept-handle-ausgaenge.md` §13, je Zeile ein Test). (2) `Herkunft.cs` und `BoardLeseseite.cs` bleiben, aber nur noch auf dem Server: als „was besitzt der Code“ (Stempel aus dem Code-Stand) und als Übersetzung Karte → Modell; im Editor gibt es keine Herkunft mehr. (3) Rumpf-Edit als eigene Geste kommt mit P7.4 (✎ an der Spanne); heute ändert ein Rumpf nur die 🤖-Übernahme (im Verlauf). Belegt: Prüfstand 471/471 (18 neu: `GestenTests` — leerer Patch ohne Änderung, Feld, Enum-Wert, Decider-Ausgang, neuer Command, Record/Decider/Aggregat (zwei Dateien)/Handle löschen, Fähigkeit am Handle, Akteur darf, Client sendet, Zusage, Funktions-Ergebnis, Prozess-Regel, Fluss-Knoten, Umbenennen inkl. Lambdas, Fluss-Variable umbenennen; Sonden-Domäne als echte Dateien), `--check` ✅, `--sonde` ✅. Integration nicht gefahren (Laufzeit unverändert). **Browser (SimHost):** nach dem Laden kein Delta; „+ Feld“ an `DatensatzErstellt` → `Events.cs` geändert, ↶↶ → Datei wie vorher; Name im Panel `DatensatzExistiertBereits` → `DatensatzGibtEsSchon` → 4 Dateien (Decider, Events, Prüfstand-Test, Wire-Kontext), ↶; „+ Aggregat“ → 3 Dateien, ✕ an der Karte → alle 3 wieder weg; ⊕ „+ Ausgang“ am Decider → nur das OneOf-Argument, ↶. Gefunden und behoben dabei: gespiegelte Rümpfe (Code-Sync) lösten leere Gesten in Schleife aus — `rumpf` zählt nicht zum Vergleich. |
| P7.3 | ✅ 2026-10-12 | **Server-Sicht** `GraphExtractor.Kern/Sicht/` (`Sicht.Aus(board)`; Eingabe ist das Board des Code-Stands): Karten wie im Editor (dieselben Ids: `rec:`, `dec:`, `hd:prj:…`, `fs:`, `auf:` …; Prozess-Regeln als Transitionen, State-Karten), Kanten in derselben Reihenfolge (die Domäne von Datentypen folgt ihren Nachbarn in Kanten-Reihenfolge), Block (Aggregat/geteilt/Akteur-Band), Mitgliedschaft (Aggregat je Record), Store-Scope, Inseln, Akteure je Karte (aus `rahmen.akteurMengen`), Akteur-Reihenfolge je Domäne und Akteur-Rahmen, Domäne (Graph → Einheit → Nachbarn → Namespace; Akteur in der ersten Domäne, in der er wirkt), Rolle, Vertreter (kompakt), zusammengezogene Kanten, Prozess-Kette + Tiefe, Fluss-Heimat, Client-Anschlussleiste (Ports, verkörperte Akteure, Teil → Akteur), Ketten-Pfade „👤 über die Kette“, „wirkt in“, „berührt“ des Prozesses. Sortierung wie `localeCompare` (ICU). `ModellMapper.ZuBoard` legt sie als **`board.sicht`** ins Board; `Lesen.LiesAsync` rechnet sie als eigene Phase „Sicht“. **Endpunkte:** `POST /api/werkbank/passt` (⊕: Port + Kandidaten → passt/gesperrt mit Regel; Anschluss-Regel + `rahmen.grammatik` = `DomainEditor.Grammatik`), `GET /api/werkbank/kette?karte=&details=` (statische Kette für den einen Fokus); die Sicht je Lesung hält `Arbeitsbereich.MitSicht`. **Editor:** in Plan-Reihenfolge gelöscht, jeweils nachdem ein vorübergehender Paritätsvergleich JS ⇄ Server gleich war (§16): `akteurMengen`/`akteurSet`/`akteurVon`/`akteurOrdnung`/`akteurDomaenen` (+ `handleAkteure`/`fnLeser`/`storeAkteure`/`projektionAkteure`/`codeBesitzerKnoten`, `deAkteurParitaet`) → `flussRegeln`/`kettenPfad` → `deriveMembership`/`groupKeyOf`/`recordAgg`/`typAgg`/`sagaAggs`/`funktionAgg`/`mitUmzug` → `derivedStores` → `boardEdges`/`components`/`islandInfo` → `domKey`/`einheitDom`/`graphDom`/`datenDom`/`aggDom`/`flussHeim` → `berechneSicht`-Rechnung (`direkteBesitzer`, Vertreter, `kettenKanten`, Tiefe, Kontrakt) und `ketteVon`/`sliceVon`/`clientSlice` → `compatible`/`grPruefe` (+ Grammatik-Helfer) → `clientAkteure`/`clientPorts`/`teilAkteur`/`teilZusagen`. Die gleichnamigen Zugriffe (`domKey`, `groupKeyOf`, `akteurVon` …) schlagen jetzt nur in `board.sicht` nach; darüber liegen nur Ansichts-Wünsche (Heimat neuer Karten, gewünschte Akteur-Reihenfolge). ⊕ (`vbKandidaten`/`vbZeige`) fragt den Server asynchron, der Katalog (Nachrichten ohne Karte) reist in derselben Anfrage; die Fokus-Kette kommt je Code-Stand gecacht. Eine Karte, die nach dem Board vom Server aus „geteilt“ in einen Aggregat-Block wandert, verliert ihre Position (statt `mitUmzug`). **L20:** `--check` prüft das Inventar gegen die Server-Kartenliste (Karten + Kanten: Befugnis = Kanten Akteur → Nachricht, Vertrag = Zusage-Karten, Regeln = Regel-Karten, Wirker = `fs:`/`tr:`-Karten) und meldet „Code ohne Karte: N“; neu: **leere Geste schreibt nichts** (Gesten-Motor auf dem unveränderten Board). Die „gezeichnete“ Sonde ist ein **Gesten-Skript** (`Sonde/Gezeichnet.gesten.json`, 4 Gesten mit Neu-Lesen dazwischen, spätere Gesten verweisen auf die Ids des Code-Stands) über den Gesten-Motor in den Speicher-Fork (`Dateipuffer` mit austauschbarer Lesequelle); `Gezeichnet.board.json` gelöscht. Das Sonden-Inventar nimmt die Mitgliedschaft aus der Sicht, `soll.txt` hat 23 neue, von Hand abgeleitete `sicht`-Zeilen (Domäne/Block der Leseseite, Reaktionen, Prozess, Akteure, Insel). **Gefunden und behoben:** `vbPartner` warf an Anker-Zeilen (Decider/Applier am Aggregat, Regel am Prozess, ohne Port-Beschreibung) — ⊕ „+ Decider/+ Applier“ am Aggregat und ⊕ „▲ Aggregat“ am Decider/Applier zeigten deshalb nichts; jetzt zählt eine Anker-Zeile als „verbunden“ (gelöst wird über den offenen Port). **Abweichungen vom Plan:** (1) ⊕ ist `POST` statt `GET ?port=`: Ports sind gezeichnete Anschlüsse, der Server kennt sie nicht — der Editor schickt Port und Kandidaten (Schlüssel + Beschreibung), das Urteil fällt auf dem Server; „schon verbunden → lösbar“ entscheidet der Editor an seinen Kanten. (2) Statt neun einzelner Paritätsläufe ein gemeinsamer Lauf über alle Ableitungen vor dem ersten Löschen (alle gleich), dann in Plan-Reihenfolge gelöscht. (3) Code-Blöcke (📝/🤖) gibt es nur im Editor (bis P7.4): sie folgen der Karte, an der sie hängen (ihre Code-Kanten bleiben im JS; der Rumpf eines Clients steht in dessen Namespace-Domäne wie bisher). (4) Die Akteur-Reihenfolge zählt alle Karten (nicht nur die geladenen Domänen). (5) Die Kompakt-Ansicht ist seit P0 nicht erreichbar (`VIEW.details` wird beim Laden gesetzt); der Server liefert Vertreter und zusammengezogene Kanten trotzdem (Parität in beiden Ansichten belegt). (6) `applyLink`/`loeseLink` bleiben Karten-Änderungen (Karten-Delta trägt): **Empfehlung** — als Server-Geste „verbinde Port A mit Karte B“ erst umziehen, wenn auch die Ports (Panel-Anschlüsse) vom Server kommen; sonst entstünde eine zweite Port-Semantik neben `Sicht.Passt`. **Bleibt im JS** (nicht im Plan-Katalog): `partnerVon` (◀ kommt aus / geht an ▶ samt ✕), `flussSlice` (◎ eines Fluss-Rahmens), die Katalog-Sorten je Port (`VB_SORTE`), das Layout (`packLayout`) und die Port-Kanten (`drawEdges`). Belegt: Prüfstand 476/476 (5 neu: `SichtTests` — Karten/Kanten/Kette, Domäne + Block aus dem Graphen, Einklappen + Insel, statische Kette, ⊕ mit Kardinalität „genau 1“ und lösbarer Verbindung), `--check` ✅, `--sonde` ✅. Integration nicht gefahren (Laufzeit unverändert). **Browser (SimHost):** vor/nach dem Umzug identisch: 590 Karten mit denselben Positionen, 902 gezeichnete Kanten, 37 Rahmen und 124 Spalten mit derselben Geometrie, dieselben Rahmenköpfe und Akteur-Zuordnung (`deAkteure`); ⊕ an 130 Ports von 24 Karten (je Art) dieselben Kandidaten und Sperren — bis auf die 4 vorher abstürzenden Aggregat-Ports (jetzt 31 Decider/33 Applier bzw. 6 Aggregate, 8 davon ✓); Fokus-Kette und Panels (Kettenpfad, „wirkt in“, „verkörpert“, „berührt“) vom Server; ⊕ „+ Ausgang“ am Decider → nur das OneOf-Argument, ↶; „+ Aggregat“ (ohne Sicht bis zum Board) → ↶. |
| P7.4.1 | ✅ 2026-10-12 | **Code-Karte als Gate:** `DomainExtractor` merkt sich jede handgeschriebene Typ-Deklaration oberster Ebene (`Deklarationen`, partial je Teil); `ModellMapper.CodeKarten` macht aus jeder, die keine Karte über `_ort`/`_teile` abdeckt (`ModellMapper.Abdeckung`, auch verschachtelte Karten wie Fluss-Knoten), eine Code-Karte `code:T` (roh, partial-Teile als `_teile`, Domäne aus dem Namespace). Dazu gehören jetzt die Store-Implementierung als `_teile` der Store-Karte und je Store-Funktion `_ort` = ihre Fähigkeit (vorher 44 Fähigkeits-Interfaces ohne Spanne). **`--check`:** Soll = jede Typ-Deklaration aus der Syntax (`SyntaxInventar.Deklarationen`), Ist = die Spannen der Server-Kartenliste samt Besitzer (ein Fluss ist der ⛓-Rahmen seiner Knoten) — „Code ohne Karte: 0“ ist Pflicht (vorher nur je bekanntem Konstrukt), dazu Anteil und Namen der Code-Karten. **Sonde:** `Fristrechnung` (statische Hilfsklasse in zwei partial-Teilen) → `code … teile=2`, von Hand in `soll.txt`. **Editor (nur Anzeige):** Code-Karte ohne ⚠, wenn nichts unaufgelöst ist („Hilfscode — kein Baustein erkannt, roh aus der Datei“, grau gestrichelt). Belegt: Prüfstand 477/477 (neu: Hilfscode → eine Code-Karte, Store-Impl/Fähigkeiten in Karten-Spannen), `--check` ✅, `--sonde` ✅ (103 Fakten). Integration nicht gefahren (Laufzeit unverändert). **Browser (SimHost):** vor/nach — 590 → 599 Karten (9 Code-Karten in ihren Domänen-Rahmen, +3 Spalten „📝 Code“, ein neuer Rahmen `Domain.Infrastructure`), 875 Kanten gleich, die übrigen Karten unverändert bis auf die Verschiebung rechts vom gewachsenen `Domain.Pipeline`-Rahmen; Panel der Code-Karte; „+ Feld“ an `DatensatzErstellt` → Geste (2,1 s unter Last) → ↶ → `git status` sauber. |
| P7.4.2 + P7.4.3 | ✅ 2026-10-12 | **Validator → Analyzer:** `Domain.SourceGeneration/FlussAnalyzer.cs` liest das `Definiere`-Lambda wie `FlussLeser`/`ProzessRegelnGenerator` (Kern-Aufruf je Anweisung, außen `.Zeitlimit`/`.Oder`) und prüft aus Signaturen: CQRS073 GR-FLUSS-QUELLE, CQRS074 GR-FLUSS-WARTE, CQRS075 GR-FLUSS-ZEITLIMIT, CQRS076 GR-FLUSS-DRAHT (Fall außerhalb der Ausgänge, Ablehnung statt ✕-Port, ✕-Port am Warten), CQRS077 GR-FLUSS-WARTET (Warnung, je Knoten/Fall einmal), CQRS078 Fluss sendet Command ohne Decider. `ModellAnalyzer.cs`: CQRS079 MISSING-APPLY, CQRS080 GR-FALTUNG, CQRS081 ENUM-ZERO, CQRS082 GR-GARANTIE (transienter Eingang → Command), CQRS083 GR-ZUSTAND/Regel Z. `Infrastructure.SourceGeneration/GesamtbildAnalyzer.cs` — Regeln, deren Erzeuger und Konsumenten in verschiedenen Projekten liegen, in der Laufzeit-Compilation aus Metadaten-Signaturen (Prozess-/Fluss-Rümpfe braucht keine: beide sind Zustandsschritte und erzeugen keine Events): CQRS084 GR-QUERY, CQRS085 GR-MODUL-AUSGANG-OFFEN/UNUSED-EVENT/RESPONSE-OHNE-QUERY, CQRS086 GR-MODUL-EINGANG-OFFEN, CQRS087 GR-GETRAGEN, CQRS088 GR-ZYKLUS (Projektionen/Reaktionen über transiente Events). Ohne Quell-Ort in dieser Compilation beginnt jede Meldung mit `'Typ'`. **Gelöscht:** `DomainEditor/Validator.cs`, `/api/editor/validate`, die Graph-Diagnosen UNROUTED-SAGA-CMD/PROCESS-NOT-REGISTERED/MISSING-APPLY/UNUSED-EVENT/ENUM-ZERO/STORE-AMBIGUOUS/HANDLE-OFFEN/RESPONSE-OHNE-QUERY und das Board-Feld `diagnosen`; `Grammatik.cs` ist Daten (Gegenstücke nachgetragen, `Beschreibe` weg), offen bleiben nur die Trigger-Welt-Regeln (GR-TRIGGER, GR-SELBST, GR-SELBST-OHNE-EVENT, GR-KEIN-EVENT-AUS-PIPELINE, GR-INGRESS-EINDEUTIG). Tests: die Validator-Tests sind gestrichen bzw. auf Ableitungen umgestellt (GR-HERKUNFT über `AkteurAnteile`), neu `Analyzers/FlussAnalyzerTests` (inkl. gezeichnet → Scaffolder → Build meldet den offenen Fall), `ModellAnalyzerTests`, `GesamtbildAnalyzerTests` (Domäne als Metadaten-Referenz). **Werkbank:** ⟳ holt die eigenen Regeln selbst aus den Analyzern der warmen Compilations (`Arbeitsbereich.RegelBefundeAsync`: je Domänen-Projekt seine Analyzer, das Gesamtbild auf einer leeren Compilation über den Domänen-Compilations) — das inkrementelle Bauen druckt für unveränderte Projekte keine Warnungen mehr; je Geste laufen die Regeln auf den berührten Dateien mit. Ein Befund hängt an der innersten Karte, deren `_ort` (neu: `bis` = letzte Zeile) die Zeile enthält (Fluss-Knoten statt ganzem Fluss), sonst Typ + Member; `Diagnosen.Lies` liest auch ortlose Fehler (`CSC : error CQRS084: 'Typ' …`). Editor: Warnungen als „⚠ n“ (gelb) statt „⛔“. **LLM-Prüfung (L18):** `Arbeitsbereich.PruefeProbeweiseAsync` (Datei mit Kandidat in den warmen Stand, Compiler + CQRS-Analyzer des Projekts auf dieser Datei, nur neue Fehler, Fork verworfen); `LlmKonsole.PruefeAsync` nutzt es für jede C#-Methode (decide/apply/store/projektion/reader/funktion …, vorher nur decide/apply über das Modell-Kompilat) und für Fluss-/Regel-Lambdas (`CodeSync.MitLambda`); `ProjektPruefung.cs` gelöscht; `--pruefe-rumpf` lädt den Workspace. `ModellSimulation` bleibt nur für die Konsolen-Simulation (P8). **Abweichung:** Schritte 2 und 3 in einem Commit (die Werkbank-Seite `Arbeitsbereich` trägt beide). Belegt: Prüfstand 479/479, Integration 39/39 (Analyzer im Domänen-Build: nur Warnungen), `--check` ✅ (35 Build-IDs), `--sonde` ✅. Browser: ⟳ → 12 Regel-Warnungen an ihren Karten (CQRS077 an `fluss:Bildeingang#paar`/`#melden`, CQRS081 an den Enums, CQRS085 an `ImagePairEingabeUngueltig`), Meldungen-Leiste gelb, Klick wählt den Knoten; `/api/llm/pruefen` (kaputt/gut/Regel-Lambda); Geste + ↶, `git status` sauber. |
| P7.4.4 | ✅ 2026-10-12 | **Rumpf aus der Datei:** **Kern** `Schreiben/Rumpf.cs` — `RumpfOrt` (Datei + Spanne der Methode bzw. der Anweisung + Lambda-Index), `Rumpf.Ersetze` (genau den Rumpf zwischen den Klammern, Ausdrucks-Rumpf wird Block, bzw. genau den i-ten Argument-Lambda der Anweisung; Sperre: der Bereich muss noch so aussehen wie gezeigt — ohne Prompt-Zeile, Leerraum egal; eine Prompt-Zeile im Text bleibt), `Rumpf.Lies`, `MitMethodenRumpf` (dieselbe Einsetzung nutzt jetzt `CodeSync` für die 🤖-Übernahme). **Extractor:** `_rumpf` an Store-Funktionen und Katalog-Funktionen (die Methode der Implementierung; `_ort` bleibt die Fähigkeit bzw. das Interface), `_ort.bis` (letzte Zeile). **SimHost:** Geste `POST /api/werkbank/rumpf` (C#: `ort`, Python: `python`-Anker über `PythonSync.MitRumpf`; Verlauf/↶ wie jede Geste), `.py`-Änderungen lassen das Board warm neu lesen (die Python-Rümpfe gehören dazu; nur `.cs` geht in den Workspace); gelöscht: `/api/editor/code` (GET/POST), `/api/editor/open`, `CodeAnker`/`Aufloesen`/`Lese`/`SetzePrompt`/`Oeffne` in `CodeSync`. **Editor:** `MODEL.codeNodes/llmNodes`, `codeSrc`, Andocken, `codeNodeCard`, `llmNodeCard`, `codeKanten`, `CODE_ADJ`, `findCodeOwner`, `konsolenId`, Code-Sync-Poll und die Code-Drähte sind weg; `rumpfTeile(n)` liefert je Karte ihre Rümpfe (Text, Ort, LLM-Slot) aus dem Board, `rumpfKacheln` macht daraus die abgeleiteten Rumpf-Kacheln (`rp:<Karte>:<i>`, Name wie der frühere 📝-Block — die Kartenhöhe taktet das Zeilenraster), `codePort` zeigt am Besitzer den Ausschnitt mit ✎ (Textfeld → ✓ Speichern = Geste) und 🤖 (Auftrag/Anpassung → `/api/llm/ausfuehren`; Vorschlag ✓/✗, ↶ nach Übernahme). Der Platz unter jeder Kachel, auf dem früher der 🤖-Knoten stand, bleibt reserviert (Lage unverändert). Belegt: Prüfstand 484/484 (neu in `GestenTests`: Methoden-Rumpf, Prompt-Zeile bleibt, Fluss-Lambda, Sperre, `_rumpf` an Store-Fn), `--check` ✅, `--sonde` ✅. Integration nicht gefahren (Laufzeit unverändert). **Browser:** 599 Karten (die 170 📝-Karten sind 170 Rumpf-Kacheln), 875 → 713 Kanten (genau die gestrichelten 📝-Drähte), nach „▦ Neu anordnen“ dieselben Positionen wie nach P7.4.1 (Stichprobe 13 Karten + Rahmen; Kachel +16/+67 unter dem Besitzer wie der 📝-Block); ✎ am Decide-Rumpf → genau eine Zeile in `Decider.cs`, Prompt-Zeile erhalten → ↶; ✎ an einer Python-Zusage → `classifier.py`, Board neu gelesen → ↶; `git status` sauber; keine Konsolen-Fehler. **Abweichung:** der erste Lauf nach dem Umbau packt das Raster einmal neu (neue Kachel-Ids) — die Ansicht ist danach dieselbe. |
| P7.4.5 | ✅ 2026-10-12 (Prüfung + Empfehlung) | **Grep-Liste aus §1.1 im heutigen `editor.js`:** gelöscht sind alle Ableitungen — `flussRegeln`, `akteurMengen`, `derivedStores`, `deriveMembership`, `boardEdges`, `ketteVon`/`sliceVon`, `compatible`, `grPruefe` (P7.3), dazu jetzt `findCodeOwner`, `codeKanten`, `konsolenId` (P7.4.4). Unter ihrem Namen geblieben sind nur **Nachschlagen** in `board.sicht` (`domKey`, `groupKeyOf`, `akteurVon`, `akteurSet`, `berechneSicht` = Sichtbarkeit/Vertreter aus der Sicht + Ansichts-Wünsche), **Gesten-Erzeuger** (`applyLink`, `loeseLink`, `renameRefs`, `delNode` — sie ändern Karten, die Karten-Änderung ist die Geste, P7.2) und die **Serialisierung** der bearbeitbaren Regeln (`prepareSaga`: Transitionen → `sagas.schritte` für das Delta). Echte Ableitungen außerhalb der Liste: `partnerVon` (◀ kommt aus / geht an ▶), `flussSlice` (◎ am Fluss-Rahmen), die Fluss-Helfer (`flPorts`, `flVor`, `flHoert`, `flDrahtTyp`, `flZuweisung`) und `VB_SORTE` (Katalog-Sorten je Port). **Empfehlung:** (1) `partnerVon` + `flussSlice` + Fluss-Helfer als Teil von `board.sicht` auf den Server ziehen — je Nachricht eine Partner-Liste `{karte, richtung, text, marke, lösen}` (das ✕ bleibt eine Geste im JS, beschrieben durch `lösen` = Art + Karte, kein Nachrechnen), je Fluss seine Ports/Drahttypen und der Slice als Kette über `fl:<Id>` (`GET /api/werkbank/kette` kennt Fluss-Rahmen noch nicht); `VB_SORTE` gehört in `Grammatik.EditorPortSorte`. Am besten vor P8, wo ohnehin das JS der Simulation fällt. (2) `applyLink`/`loeseLink` erst dann als Server-Geste „verbinde Port A mit Karte B“, wenn die Panel-Ports selbst vom Server kommen (eine Port-Tabelle für `passt` und `verbinde`) — sonst entstünde eine zweite Port-Semantik neben `Sicht.Passt` (wie in P7.3 empfohlen). **Abnahme P7.4:** „Code ohne Karte = 0“ ✅ (Pflicht in `--check`, 9 Code-Karten = 2,1 %); Umbenennen eines Events samt Lambdas + Bau ✅ (siehe §16); Grep-Liste: keine Ableitung mehr unter den gelisteten Namen, die Rest-Ableitungen außerhalb der Liste stehen oben als Empfehlung; Geste p50/p95 gemessen (§16) — unter Fremdlast 346–470 ms p50, das 300-ms-Ziel ist auf diesem Rechner unter Last nicht belegt. |
| P7.5.1 | ✅ 2026-10-13 | **Dienst-Bindung (E11, L21):** Marker `Abstractions.IDienst` + `Dienstvertrag` (verlinkt, in Generatoren `internal`). **Generator** `DienstGenerator` → `GeneratedDienste` (Namen, Implementierungen mit Fabrik `sp => new Impl(…)`, `Braucht` je Funktions-/Dienst-Implementierung, Konfig-Records mit Feld-für-Feld-Bindung über `KonfigLeser`, Defaults des Records, unbekannte Felder = Fehler); die gemeinsame Lesart der Konstruktoren `Ctorbindung` (auch im Analyzer verlinkt). Funktions-Implementierungen werden jetzt ebenfalls per generierter Fabrik gebaut (`AddFunktion<F, Impl>(slots, w, fabrik)`, `FunktionsBindung.Impl`). **Laufzeit** `Infrastructure/Dienste/Dienste.cs`: `BindeDienste` in `AddCqrs` nach `BindeFunktionen` — bindet transitiv, was die gebundenen Funktions-Implementierungen brauchen, nach `Dienste:<I>:Implementierung` (Default: die einzige) und `Konfig:<Record>`; Boot-Fehler mit Karte (`dienst:<I>`, `host:Dienste`, `host:Konfig`): keine/mehrere Implementierungen, unbekannter Name, Kreis, fehlender/fremder Konfig-Abschnitt. Schema: `UmgebungKonfig.Dienste` (`DienstKonfig`), Abschnitt `Dienste`. **Analyzer** `Domain.SourceGeneration/DienstAnalyzer.cs` — abweichend vom Plan-Text („ab CQRS073“, die Nummern waren seit P7.4 vergeben): CQRS089 GR-DIENST (Dienst nur im Konstruktor einer Implementierung), CQRS090 Konstruktor bindbar (Dienste, Konfig-Records, `ILogger<T>`), CQRS091 Konfig-Feld bindbar, CQRS092 Implementierung öffentlich/nicht generisch; Grammatik: Sorte `dienst` 🧩, Baustein `dienstimpl`, Regeln GR-DIENST, GR-DIENST-BINDBAR, GR-DIENST-GEBUNDEN, Editor-Port `dienst` (⊕ = Gebrauch). **Bestand:** `IImageResizer`/`IHistogramEqualizer : IDienst`, `ImageResizer`/`HistogramEqualizer` öffentlich, `DomainPipelineExtensions.cs` gelöscht, `Host.Grpc/Program.cs` ohne `AddDomainPipelineServices`; Integrationstests bekommen `Konfig:PreprocessingConfig` aus der Konfig statt der Hand-Registrierung. **Extractor:** `DienstRaw` (Methoden, Implementierungen mit Konstruktor und Rümpfen je Methode), `FunktionRaw.ImplKonstruktor`, Konfig-Records auch aus Funktions-/Dienst-Implementierungen (`PreprocessingConfig` ist jetzt `konfig`); Board `dienste` (`_id` `dienst:<I>`, `_ort` Interface, `_teile` Implementierungen, `_rumpf` je Methode), `funktionen[].konstruktor`; Sicht: Karten `dn:`, Kanten Funktion → Dienst und Dienst → Dienst, Block = Block der nutzenden Funktionen; `--check`-Inventar „Dienst“. **Schreiben:** Scaffolder (Interface, Implementierung mit throw-Stubs, Funktions-Implementierung mit Primär-Konstruktor), Abgleich (Methoden-Signaturen je Name, fehlende Methoden als Stub an jeder Implementierung, entfernte am Interface; Konstruktor-Parameterliste von Funktions- und Dienst-Implementierungen), Herkunft/Stempel/`KartenSchluessel` mit `dienste`. **Sonde:** `IWertTabelle` + `LineareWertTabelle(WertTabellenKonfig)` + `Schadensschaetzer(IWertTabelle)`, gezeichnet: neue Funktion mit C#-Implementierung + neuer Dienst `IPruefgeraet`; `soll.txt` von Hand (dienst-, konfig-, funktion-impl- und sicht-Zeilen). **Editor:** Dienst-Karte 🧩 (Kurzfassung, Panel: genutzt von, Signatur editierbar, Implementierungen mit Konstruktor ⊕/✕, Rumpf-Ports mit ✎, „＋ Implementierung“, Wahl je Umgebung), an der Funktion „🧩 braucht (Konstruktor)“ mit ⊕ und „＋“ (neuer Dienst), „＋ C#-Implementierung anlegen“; „Dienst“ im Domänen-Menü; Host-Rahmen „🧩 Dienste“ und „⚙ Konfig“ aus dem Code (Schema mit `eintraege` je Dienst bzw. Konfig-Record, der generische Renderer zeichnet sie). Belegt: Prüfstand 507/507 (+23: Generator, Bindung, Analyzer je Regel, 5 Gesten), `--check` ✅ (Code-Karten 9 → 4 = 1,0 %), `--sonde` ✅ (111 Fakten), Integration siehe §16. **Browser (vor/nach gegen einen Worktree auf 231407d):** 600 → 599 Karten (−5 Code-Karten, +2 Dienste, +2 Rumpf-Kacheln), 728 → 730 Kanten (genau Funktion → Dienst ×2); im ImagePair-Block eine neue Spalte (VO/Enum-Spalte +312 px), die Domänen-Rahmen packen sich neu, weil der Pipeline-Rahmen ohne Code-Karten schmaler ist; Geste „+ Methode“ am Dienst (Interface + Stub) und ⊕ Dienst an `IRangeSuche` (Konstruktor + `using`) je mit ↶ → `git diff` wie vorher |
| P7.5.2 | ✅ 2026-10-13 | **Webhook als Konfig (E12, L22):** `DateiQuellKonfig.Webhook` (Route; `AddQuellen` prüft „beginnt mit /“). `MapQuellWebhooks(u)` in `LaufeAsync` (Quellen.cs): je konfigurierter Quelle mit Route ein `POST` auf dem Einblick-Port (`RequireHost`), Body über den generierten Wire-Kontext (`EingangJson`) → `QuellEingang.NimmAufAsync` → `202` mit Vorgangs-Id, `400` bei falschem Body; Boot-Fehler ohne Einblick-Port bzw. bei doppelter Route (Karte `quelle:<Typ>`). `MapQuellWebhook<T>` gelöscht, **`Host.Grpc/Program.cs` = `AddCqrs(args)` + `LaufeAsync`**. Umgebungen `sim-main`, `prod`, `multinode`: `"Webhook": "/webhook/datei"`; `build.sh` (linux) mit `GRPC_EINBLICK_PORT`/`GRPC_WEBHOOK` (Default: aus — der alte Webhook lag nur auf dem h2c-Port), Multi-Node-Compose gibt den Einblick-Port 5002 von grpc1 frei, `deploy/cqrs-grpc.env` erklärt den Schlüssel. **Extractor:** die Bindung „Webhook“ an der Fluss-Quelle kommt aus `rahmen.umgebungen` (je Umgebung, mit Datei), nicht mehr aus der Composition Root (der `IQuellNachricht`-Zweig dort ist gelöscht); `Fluss.cs`: eine Quell-Nachricht kommt immer von außen (Treiber oder Webhook). **Werkbank:** `umgebungen/*.json` lassen das Board warm neu lesen (Wächter + sofort nach dem Schreiben im Host-Rahmen). **Editor:** Quellkarte „⇥ Webhook /… (Umgebung)“ je Umgebung bzw. „kein Webhook“, Fluss-Quelle „⇥ Webhook POST … (Umgebung)“; bearbeitet im 🖥 Host-Rahmen (Quellen → Feld `Webhook` aus dem Schema). Belegt: Prüfstand 509/509 (+2: Route, kein Einblick-Port), Integration 39/39 (`EingangE2ETests`: zwei POSTs derselben Nachricht → `202`, derselbe Vorgang, genau ein Event mit Wirker `quelle:DateiErkannt`, genau ein Auftrag an die Hand-Deutung; falscher Body `400`), `--check` ✅, `--sonde` ✅. Browser: Karten/Kanten unverändert (600/730), Webhook an Quellkarte und Fluss-Quelle; Host-Rahmen-Weg (Umgebung schreiben → Board zeigt `/webhook/kamera` → zurück) → Datei wie vorher. Offen: eine Quelle ohne Treiber-Basis (`BenchPing`) hat keinen Konfig-Abschnitt und damit keinen Webhook (vorher nur über `MapQuellWebhook<T>` im Programm möglich, genutzt wurde es nicht) |
| P7.5.3 | ✅ 2026-10-13 | **Python-Rümpfe externer Funktionen (E13, L23):** Regel für Basis/Modul/Klasse in `Abstractions.Funktionsvertrag` (`PythonBasis`, `PythonModul`: `IBildVerkleinerung` → `funktionen/bild_verkleinerung.py`, Klasse `BildVerkleinerung`; in Generatoren `internal`). **Codegen:** neben `generated/funktionen.py` (Basen, gab es schon) die Tabelle des generischen Workers `generated/funktions_tabelle.py` (Funktion → Basis, Modul, Klasse; `codegen.sh`-Drift-Gate). **SDK:** `FunktionsBasis(slots, konfig)` — `self.konfig` (Name → Text). **Python:** `funktionen/bild_verkleinerung.py` + `histogramm_ausgleich.py` (aus dem alten Worker), `funktions_worker.py` ist jetzt GENERISCH (lädt nach Tabelle, prüft die Vererbung, `--funktionen`, `--slots`, `--konfig NAME=WERT`, `--pfad` für eigene Implementierungen); `sim-main`-Worker-Argumente auf `--funktionen … --konfig OutputPath=…`. **Abweichung:** `rufe(self, auftrag, x)` behält `x` (Ausführungs-Id zum Deduplizieren, wie `IAusfuehrung`), der Plan-Text nannte `rufe(self, auftrag)`. **Extractor:** `PythonClients.Funktionen` (Code-Fakt: Klasse erbt die generierte Basis) → `Funktion.Python {Datei, Klasse, Basis, Methode, Signatur, Rumpf}` im Board. **Werkbank:** ⟳ „Python-Basen“ legt für jede Funktion mit `Laufort = Extern` in der aktiven Umgebung eine fehlende Implementierung aus der Basis an (`PythonSync.FunktionenAnlegen`, Platzhalter `raise NotImplementedError`); „▶ Worker starten“ ohne eigenen `Funktionen:<F>:Worker` = generischer Worker `--funktionen <F>` mit einem Token der Umgebung, dessen Akteur `IDarf<F>` hat. **LLM:** Slot `funktionpy|<F>@<Klasse>` (eigene Anweisung: genau ein Ergebnis per `return`, Prüfung: Python-Parser + Ergebnisse aus dem OneOf), derselbe Anker wie Zusagen (PythonSync). **Editor:** an der Funktions-Karte „Rumpf · rufe (Python)“ mit ✎/🤖 (Laufort je Umgebung im Titel) bzw. der Hinweis, dass ⟳ die Implementierung anlegt; Rumpf-Kachel `rp:fk:…:1` („<Klasse>.rufe (Python)“). **E2E:** `PipelineFlussE2ETests` startet den generischen Worker mit einer Test-Implementierung per `--pfad` (der eigene Test-Worker entfällt). Belegt: Prüfstand 513/513 (+4: Basis lesen, neue Implementierung, Modul-Regel, Bestand als Code-Fakt), Integration 39/39, Python Worker 20/20 (+5: Tabelle, Laden mit Slots/Konfig, Fehlerfälle, echtes Bild durch beide Implementierungen), SDK 28/28, `--check` ✅, `--sonde` ✅. Browser: 600 → 602 Karten (zwei Python-Rumpf-Kacheln), 730 Kanten; ✎ am Python-Rumpf von `IBildVerkleinerung` (Geste über PythonSync) und ↶ → Datei wie vorher |
| P7.5 Abnahme | ✅ 2026-10-13 | **Nur im Editor gebaut** (Branch `claude/werkbank-abnahme-p75`, Commit „Domäne Zaehler“ als Beleg, nicht für `main`) — über dieselben Endpunkte wie die Oberfläche (`/api/werkbank/geste`, `/umbenennen`, `/rumpf`, Host-Rahmen, ⟳), ohne eine Zeile Handcode: Aggregat `Zaehler` (`ErfasseStand` → `StandErfasst` | `StandRueckwaerts`, Decide/Apply per ✎), Datei-Quelle `ZaehlerFotoErkannt : DateiGemeldet` mit `Quellen:ZaehlerFotoErkannt` samt `Webhook: /webhook/zaehler` (Host-Rahmen), Fluss `Ablesung` (foto → lesen → pruefen → erfassen, Zuordnungen als Argument-Liste), C#-Funktion `IPruefeStand` (Laufort Host) mit neuem Dienst `IGrenzwerte`/`FesteGrenzwerte` (⊕ am Konstruktor, Methode und Implementierung per Umbenennen benannt), Python-Funktion `ILeseZaehler` (Laufort Extern: ⟳ legt `funktionen/lese_zaehler.py` an, ✎ `rufe`), Projektion `ZaehlerListe` + Reader `ZaehlerAuskunft` (`HoleZaehler` → `ZaehlerAntwort` | `ZaehlerUnbekannt`, Store `IZaehlerStore` mit zwei Fähigkeiten), Akteure `Zaehlerkamera` (`IDarf<ZaehlerFotoErkannt>`), `Ablesegeraet` (`IDarf<ILeseZaehler>`), `Hausverwaltung` (`IDarf<HoleZaehler>`), Client `IZaehlerPortal`, Identitäten im Host-Rahmen. **⟳ grün** (Bauen 0 Fehler, Python-Basen, Neu lesen 0 Analyzer-Fehler, Infra, Host `sim-main`). **Durchgespielt:** Datei-Drop `Z123_4567.jpg` → fertig (① Quelle ② `ZaehlerGelesen` vom generischen Python-Worker mit Token `sim-ablesegeraet` ③ `StandPlausibel` ④ `StandErfasst`, je mit Wirker/über/Ursache); Webhook `POST :5102/webhook/zaehler` zweimal → derselbe Vorgang, fertig; `kaputt.jpg` → `ZaehlerUnlesbar`; `Z123_4000` → Ablehnung `StandRueckwaerts` (Hinweis am Decide, Fluss-Schritt gescheitert — ohne `BeiAbgelehnt` gewollt); `HoleZaehler` als Hausverwaltung → `ZaehlerAntwort` (Z123/4567, Z124/100) bzw. `ZaehlerUnbekannt`, als Zaehlerkamera `403`. Browser: 40 Karten der Domäne inkl. Python-Rumpf-Kachel, Spur-Überlagerung ①–④ mit Δt über Fluss-Knoten, beide Funktionen und Decide. **`git diff`:** nur Gesten-Dateien plus was ⟳ erzeugt (`domain.proto`, Python-DTOs/Basen/Tabelle, Wire-/Event-Kontexte); `Host.Grpc/Program.cs` und `Infrastructure/Quellen` 0 Zeilen. `--check`: Code ohne Karte 0, keine Code-Karte aus `Zaehler`. **Gefunden und behoben** (Commit „Korrekturen aus der Abnahme“, +2 Gesten-Tests): Umbenennen eines Typs in `<Name>.cs` zieht die Datei mit (Patch entfernt + neu); ein Ausdrucks-Rumpf (`=> throw …`) las sich im Board anders als in `Rumpf.Lies` — die Sperre des ✎ griff (`Rumpf.AlsAnweisung` für beide), beim Ersetzen entstand eine Leerzeile vor `{`; Dienst-Methoden und -Implementierungen ohne `_ort` (Name im Panel war ein Patch statt Umbenennen); drei getrackte `.pyc` aus dem Index. **Beobachtet, offen:** (1) Der Datei-Treiber sieht ein fehlendes Quellverzeichnis erst nach seinem exponentiellen Backoff (bis ≈ 30 s) — der Drop legt es an; Vorschlag: ⟳ legt die Verzeichnisse der Sim-Quellen an (Treiber bleibt unberührt). (2) Ein Akteur/Client, der Typen der Leseseite nennt (`IDarf<HoleZaehler>`), kann nicht im Domänen-Projekt liegen (Projekt-Richtung) — er gehört nach `Domain.Akteure`/`Domain.Clients`; der Editor legt ihn dort an, wenn man die Domäne so wählt. (3) Der Scaffolder setzt neue `using` vor die vorhandenen (ungeordnet). (4) Drei Prüfstand-Tests zählen den Bestand exakt (Clients, Dienste, „jede Funktion hat genau eine C#-Implementierung“ — eine reine Python-Funktion hat keine); sie gelten für `main` und wären beim Übernehmen einer neuen Domäne anzupassen. Optional (`partnerVon`, `flussSlice`, Fluss-Helfer, `VB_SORTE` → `board.sicht`) nicht angefasst |
| P8 | ✅ 2026-10-13 | **P8.1** (`e77ace7`): `LlmKonsole` ohne `ModellSimulation` — `_sim` brauchte nur noch `Simuliere` (Kandidat in der Modell-Simulation durchspielen, `/api/llm/simulieren`, Kasten „Simulation mit dem Kandidaten“ in `KonsoleSeite`); entfällt ersatzlos, weil die Werkbank dasselbe echt kann (✓ Übernehmen → ⟳ → durchspielen → ↶). Der Prüfhinweis der Konsole sagt jetzt „probeweise im warmen Workspace“. **P8.2** (`87334b8`): gelöscht `ModellSimulation.cs`, `Musterwerte.cs`, `/api/editor/sim/*`, `/api/editor/compile`, `Cqrs.Testing/DslSchreiber.cs` (Knopf „📋 Als Test“) und `Cqrs.Testing/Abdeckung.cs` (nur die Simulation nutzte sie), im Editor der Abschnitt SIMULATION (`SIM`, `simPanel`, `simAnimiere`, `simOverlay`, Fluss-Durchspielen mit Musterwerten), `deCompile` (⚙ Kompilieren des Modells), `payload(mitVorschlag)`, `istSimulierbar`, die Hooks `simNachRender`, der Kopfzeilen-Knopf „▶ Simulation“ und `#de-sim` samt CSS (auch tote `.test`-Regeln). SimHost referenziert nur noch DomainEditor + GraphExtractor.Kern: Core, Cqrs.Testing und die Domain-/Projections-Generatoren waren nur für das Modell-Kompilat da (die Roslyn-5.3-Pins bleiben). `Cqrs.Testing` bleibt für die vier Prüfstand-Dateien; Kommentare dort ohne Simulations-Verweise. **P8.3** (`cec970b`): `SimHost` → `Werkbank` (Projekt `Werkbank/Werkbank.csproj`, Namespace `Werkbank`; der Unterordner `SimHost/Werkbank/` heißt `Werkbank/Server/`, Namespace `Werkbank.Server`, die Klasse `Werkbank` heißt `WerkbankServer` — sonst verdeckt der Namespace den Typ; `.sln`, `.claude/launch.json` Konfiguration „Werkbank“, Badge „Werkbank live“, Verweise in Kern/CLI/Doku; `deploy*` kannte SimHost nicht). Vorher Tobis laufende SimHost-Instanz (PID 15679, lief aus `SimHost/bin`) auf Wunsch beendet. **P8.4**: Doku (CLAUDE.md, `01`, `02`, `10`, `11`, `12`, `13`, README, Zielbild-Status „umgesetzt“); `04`/`05` erwähnten die Simulation nicht. Grep `ModellSimulation|/sim/|simuliere|Musterwerte|DslSchreiber` über Werkbank, GraphExtractor*, DomainEditor: leer; in `docs/` (ohne Archiv) nur noch Plan-/Zielbild-Text, der die Löschung selbst beschreibt (§1, §2, §13 P7.4, §14, §17; Zielbild §10/§13). Fallen aus P8: der Werkbank-Server lief als Tobis Instanz aus demselben `bin` — vor einem Projekt-Umzug alle SimHost-/Werkbank-Prozesse beenden; ein Namespace gleich dem Klassennamen (`Werkbank.Werkbank`) bricht die Auflösung in `Program.cs` (Top-Level im globalen Namespace) → Klasse umbenennen; `localStorage`-Schnappschüsse hängen am Origin, mit `autoPort` wechselt der Port → Vor/Nach-Zahlen zusätzlich notieren. Offen: P7.4.5-Rest (`partnerVon`, `flussSlice`, Fluss-Helfer, `VB_SORTE` → `board.sicht`), aus der P7.5-Abnahme: ⟳ legt Sim-Quell-Verzeichnisse an, Scaffolder sortiert neue `using` ein |
| P7.4.5-Rest | ✅ 2026-10-13 | **Server:** `GraphExtractor.Kern/Sicht/Sicht.Partner.cs` — `PartnerVon` (1:1 aus `partnerVon`/`flussPartner`/`akteurePartner`: dieselben Zeilen, dieselbe Reihenfolge) → `board.sicht.partner[Nachricht] = {ein, aus}` mit `{id, text, mark, loesen?}`; `loesen` beschreibt das ✕ (`{karte, feld, wert}` = Wert aus einer Liste der Karte, `{karte, zusage}` = Zusage entfernen, `{karte, eingang, draht}` = Draht lösen). Fluss-Sicht `board.sicht.fluesse[fl:…] = {heim, slice, knoten[fs:…] = {ports (mit `typ` = Nachricht des Ports), vor, tiefe, hoert, zuweisung, draehte}}`. `Grammatik.EditorKatalogSorte` → `rahmen.grammatik.katalogSorte` (vorher `VB_SORTE`). Die Deklarations-Reihenfolge der Fluss-Knoten setzt der Schreiber (`FlussPipeline.Geordnet`, in `Gesten.Schreibe`), nicht mehr der Editor (`flOrdne` gelöscht). **Editor:** `partnerVon` = Nachschlagen + `loese(…)` (die Geste an der Karte); `flPorts`/`flVor`/`flHoert`/`flZuweisung`/`flTiefen`/`flussSlice` = Nachschlagen in der Fluss-Sicht; `flDrahtTyp` = Typ des Quell-Ports; gelöscht `flAusgaenge`, `flOrdne`, `flussPartner`, `akteurePartner`, `hdId`, `VB_SORTE`. Bleibt bewusst im JS: die Gesten-Erzeuger (`flVerbinde`, `flLoese`, `flAutoArgs`/`flQuellen`, `flBedingung` — sie bauen die Karten-Änderung, wie `applyLink`) und die Anzeige `flSignatur`/`flDecideEvents`. Fallen: ein Knoten, den es im Code noch nicht gibt, hat bis zum nächsten Board keine Ports (wie jede Sicht seit P7.3); `innerText` weggecullter Karten ist leer → Vor/Nach-Vergleich über `textContent`; Vor-Stand per `git show HEAD:…editor.js` in die Datei und neu laden (statisch, no-store) |
