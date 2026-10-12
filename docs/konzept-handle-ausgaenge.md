# Konzept — Handle-Ausgänge sichtbar machen (Projektion, Reader, Reaktion)

> **Gekürzt 2026-10-12** auf das mit dem Zielbild `docs/konzept-werkbank.md` Vereinbare.

> **Stand:** 2026-09-30 · **Status:** UMGESETZT (Entscheidung „nur Signatur“ + CQRS050, §10) · **Ort:** `GraphExtractor.Kern/DomainExtractor.cs`,
> `GraphExtractor.Kern/ModellMapper.cs`, `GraphExtractor/HtmlPresenter.cs`, `GraphExtractor.Kern/Sonde/`
> Verwandt: [konzept-domaenen-editor.md](konzept-domaenen-editor.md) §3/§6, [konzept-editor-panel-bearbeitung.md](konzept-editor-panel-bearbeitung.md) §8 (Zeilenraster),
> [konzept-llm-minimalkontext.md](konzept-llm-minimalkontext.md) (Slot-Schlüssel).

## 10 · Entscheidung: das WAS nur aus der Signatur (CQRS050)

**Frage:** Brauchen wir überall `OneOf`, oder reicht eine Code-Analyse der Rümpfe? **Antwort: `OneOf` (bzw. ein konkreter
Typ) in der Signatur — eine Rumpf-Analyse kann das grundsätzlich nicht leisten.**

- „Welche Typen kann diese Methode zurückgeben?“ ist im Allgemeinen unentscheidbar (Satz von Rice). Die Signatur ist eine
  vom **Compiler** garantierte Obergrenze; eine Rumpf-Suche ist eine Untergrenze, die Fehlendes **still** übersieht
  (Variable mit Interface-Typ, Hilfsmethode, Schleife über eine Liste, `await foreach`, `switch`-Ausdruck, Fabrik …). Jede
  neue Schreibweise wäre ein weiterer Flicken — der `?:`/Cast-Fix war genau so einer.
- Warum `OneOf` ursprünglich gewählt wurde: die Command→Event-Map (`GeneratedCommandRouting.Produziert`) liest die
  Decide-Signaturen **rein typbasiert, ohne yield-Analyse**; erst dadurch wurde der Azyklizitäts-Boot-Guard scharf (die
  vorherige Namespace-grobe Map hätte Falsch-Zyklen gemeldet). Invarianten 3/4: Routing über Typen, keine Reflection.
- Die Lücke war, dass nichts `OneOf` **erzwang**: Pipelines durften `IAsyncEnumerable<ICommand>` liefern, und ein nacktes
  `IEnumerable<IEvent>` am Decider hätte der `CommandAggregateMapGenerator` still geschluckt (leere Map ⇒ Guard dort blind).

**Umgesetzt:**

| Was | Wie |
|---|---|
| Regel | `Domain.SourceGeneration/AusgabeVertragAnalyzer.cs` — **CQRS050** (Build-Fehler): Decide/Projektion/Reaktion/Reader/Pipeline geben einen konkreten Typ oder `OneOf<…>` konkreter Typen zurück; offen = Interface, Typ-Parameter, `object`, abstrakte Klasse. `Task`/`void` erlaubt (reine Effekte). Rolle über Marker/Parameter-Typen. Bewiesen: 6 Prüfstand-Tests (`Analyzers/AusgabeVertragAnalyzerTests.cs`) + Gegenprobe im echten Build (eine Pipeline zurück auf `ICommand` ⇒ `error CQRS050`). |
| Extractor | `LiesHandleVertrag`: Ausgänge **nur** aus der Signatur (offene Signatur ⇒ keine Ausgänge, nichts geraten). Der Rumpf liefert nur noch: Guard je Ausgang, den Hinweis `ungesehen` (kein return/yield gefunden — heißt nicht „nie“) und die Effekte (Store-Aufrufe, `ScheduleSelf`). |
| Diagnosen | `HANDLE-OFFEN` = Fehler (Rückhalt, falls der Analyzer nicht lief). `AUSGANG-TOT` → `AUSGANG-UNGESEHEN` als **info** (Rumpf-Hinweis, kein Befund). |
| Sonde | Pipeline-Handle mit `OneOf` statt offen; zusätzlich eine deklarierte, nie ausgegebene Variante (`MeldeSchaden ungesehen`) — beweist: das WAS kommt aus der Signatur. |

**Was Rumpf-Fakt bleibt:** ~~Guards, Store-Aufrufe, `ScheduleSelf`, HostSetting-Datenfluss~~ — mit §11 entfernt bzw. durch
typisierte Träger ersetzt. Übrig bleiben nur die Saga-Lambda-Texte (Argument-Mapping, reiner Code-Text, keine Kante).

**Offen (nicht Teil dieser Scheibe):** Reaktionen in den Azyklizitäts-Guard aufnehmen (die Ausgänge sind jetzt
typisiert).

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
```

**Umgesetzt:**

| Was | Wie |
|---|---|
| Vertrag | `Abstractions`: `IWriteStore`/`IReadStore` = Fähigkeits-Marker, neu `IStore` (Bündel); `IReadStore<T>` gelöscht. `IFaehigkeiten`/`IFaehigkeitsBereich`/`IFaehigkeitsFabrik` + `FaehigkeitenAus` (Tests). |
| Domäne | 5 Stores → 44 Fähigkeiten + 5 Bündel; die Postgres-Lese-Klassen sind `partial` Teil der Co-Commit-Klasse (`*Store.Lesen.cs`), die ungenutzte `ImagePairHistorieStoreInMemory` ist gelöscht. 5 Projektionen, 5 Reader ohne Store im Konstruktor. |
| Dispatch | Subscriber-/Reader-Dispatch nehmen `IFaehigkeiten` und rufen `Handle(…, faehigkeiten.Hole<IX>())` (Typ im Generat, keine Reflection). Query-Service: ein Bereich je Query. |
| DI | `ProjectionServicesGenerator`: jede Store-Klasse **Scoped**, umgeleitet unter jeder Fähigkeit + dem Bündel (eine Instanz je Bereich → Co-Commit). CQRS053 bei zwei Klassen je Fähigkeit. `DiFaehigkeitsFabrik` (ein `IServiceScope` je Bereich), `FristPlaner` (DB-Uhr + Fristplan). |
| Achse B | `PullPathGenerator` liest die Schreib-Fähigkeiten aller Handles (statt Konstruktor-Stores); ein Bereich je Adapter-Actor, Tracker = die aufgelöste Store-Instanz. **CQRS052** (Compile-Zeit): Schreib-Fähigkeiten aus mehreren Stores in einer Klasse. |
| Analyzer | `FaehigkeitAnalyzer`: **CQRS051** (Fähigkeit = genau eine Funktion, erbt keine andere), **CQRS054** (Store/Bündel/Fähigkeit/`IFristplan` in Ctor, Feld oder Eigenschaft eines Konsumenten, auch statisch), **CQRS055** (`new …Store()`). Gegenprobe im echten Build: die alte `TrainingFristPipeline` lieferte 2× CQRS054. |
| Extractor | Stores aus `IStore`-Bündeln (Fn = Fähigkeit), `HandleFaehigkeiten` aus den Parametern, `storefn`-Ausgänge aus den Parametern. Gelöscht: `ExtractGuards`, `Bedingungen`, `Zweige`, `StoreAufrufe`, `ScheduleSelfKnoten`, `ImplReferenziert` (ReadModel → Store nur noch über Fähigkeits-Signaturen). |
| Editor | Fn-Karte zeigt ihr Fähigkeits-Interface und „Fähigkeit von“ (Projektion/Reader); Handle-Ports „darf Store.Fn ▶“. Keine „wenn …“-Zeilen mehr. |
| Sonde | `Lesen.cs.txt` mit Fähigkeiten + Bündel und den vier Verschleierungen (Hilfsmethode, Delegate, fremdes Objekt, totes Lambda); `soll.txt` ohne Guards. |

**Gemessen:** Solution-Build 0 Fehler; Prüfstand 153/153 (neu: 5 Analyzer-Tests `FaehigkeitAnalyzerTests`, 2 Brücken-Tests
Frist/Selbst); `--check` grün (Fixpunkt + Inventar); `--sonde` grün (**46** Soll-Fakten, die Verschleierungs-Fälle
liefern exakt die Signatur-Kanten); Editor live: 44 Fn-Karten mit Fähigkeit, 57 „darf …“-Ports (36 Projektion, 19 Reader, 2 Pipeline),
keine „wenn …“-Zeile. **Nicht gemessen:** Integration (Docker lief nicht) — die Store-Lifetime (Scoped statt Transient/Singleton)
und der Fristen-Pfad sind dort noch zu bestätigen.

**Offen:** ~~Scaffolder schreibt die Leseseite (Fähigkeit + Bündel + Parameter) noch nicht~~ → geliefert, siehe §12; Reaktionen in den
Azyklizitäts-Guard aufnehmen (die Ausgänge sind typisiert).

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
| Fähigkeits-Name neuer Fn | **Abgeleitet + im Panel editierbar**: `I` + Methode ohne `Async`, vergeben ⇒ + Store-Name ohne `I`. Danach Code-Fakt (fest im Panel, der Extractor rät nie). |
| Rümpfe | **Kein Rumpf-Code außer Platzhalter** (`throw new NotImplementedException("TODO: …")`) — für Handles und Store-Methoden. Kein Marten-Wissen im Scaffolder. |
| Dateiorte | **Aus dem Code**: Fähigkeit → Datei des Bündels; Handle → Datei der Klasse; Store-Methode → Datei, in der die Schreib- bzw. Lese-Methoden der Impl liegen (nur wenn eindeutig, sonst die Deklaration). Neu ⇒ Verzeichnis des Namespace (Rahmen); unbekannt ⇒ nicht platzierbar. |
| Store-Impl | **Nur bei genau einer Impl-Klasse** je Bündel ergänzt (sonst Panel-Hinweis, nichts geraten). Neuer Store ⇒ neue Klasse; ihre Basis/Ctor nur, wenn ALLE Impls im Code eine gemeinsame Basis haben (`Rahmen.StoreBasis`, hier `MartenCoCommitStoreBase(IDocumentStore)`), ihr Namespace nur, wenn alle Impls in einem liegen (`Rahmen.StoreImplNamespace`). |
| Schreiber-Typ | `ProjectionWriter` liegt in Core (DomainEditor referenziert nur Abstractions): **aus den Projektions-Handles im Code gelesen** (3. Parameter, CQRS057), nur wenn eindeutig. Kein Handle im Code ⇒ keine neue Projektion (Analyzer-Kandidat). |

**Umgesetzt:**

| Was | Wie |
|---|---|
| Modell | `EditorModell.Lesen`, `RecordArt.Query/Antwort/ReadModel`, `Rahmen.ProjektionsSchreiber[Namespace]`/`StoreBasis`/`StoreImplNamespace`. |
| Extractor | verbatim je Handle: Parametername, Kontext-Namen, Fähigkeits-Parameter, Rückgabe, Modifizierer, Rumpf/Ausdruck, Datei (`HandleSigRaw`); je Klasse: Datei, Doku, Form, Basisliste, Attribute (ohne `[ProjectionReader]`), übrige Member als Zusatz, using-Direktiven inkl. Aliase (`KlassenQuelle`); je Fähigkeit Rückgabe/Parameter verbatim, Namespace, Datei, Doku; je Store Bündel-Datei/Doku und die einzige Impl (Deklaration, Schreib-/Lese-Datei). |
| Mapper | `ModellMapper.ZuEditorModell` füllt `Lesen` + die Leseseiten-Records; `ZuBoardJson` legt sie in die Board-Sammlungen (`sig`/`code`/`impl`), keine doppelten Query/Response-Records mehr, ReadModels nur in `readModels`. |
| Scaffolder | neue `DateiArt`s: `Schnittstellen` (Fähigkeit je Fn einzeilig + Bündel `: IStore, …`), `StoreImpl` (neue Klasse voll bzw. nur Methoden NEUER Fähigkeiten), `Konsument` (`partial class … : ISubscriber, IPullSubscriber[, IAppendProjektion]`, `SubscriberId`, `Handle(TEvent, IAggregateEnvelope, ProjectionWriter, Fähigkeit…)` → `Task` bzw. `IAsyncEnumerable<OneOf<…>>`), `Leser` (`[ProjectionReader(TrackDeps = …)]`, `IReader<P>`, `Handle(TQuery, IMessageEnvelope, ReadContext, Fähigkeit…)` → `Task<R>`/`Task<OneOf<…>>`). Bestehende Klassen: usings verbatim, abgeleitete nur für neue Handles (keine neuen Mehrdeutigkeiten). |
| DateiSchreiber | additiv: fehlende Fähigkeits-Interfaces + fehlende Basen im Bündel; fehlende Impl-Methoden (Schlüssel Name + Parametertypen); fehlende Handles (nur die im Modell neuen). **Parameter-Abgleich**: je bestehendem Handle werden die Parameter hinter dem Kontext (CQRS057: 2 bzw. 1 bei Pipelines) auf das Modell gebracht — hinzu/weg, vorhandene wörtlich, fehlende usings ergänzt, **nur die Parameterliste**. |
| Analyzer-Kandidaten | heute im Validator: `EDIT-FAEHIGKEIT-DUP` (zwei Fns, eine Fähigkeit), `EDIT-READER-OHNE-ANTWORT` (ohne Ausgabe-Vertrag nicht geschrieben), `EDIT-KONSUMENT-SCHREIBER`. |
| Parität | `--check`: der **Fixpunkt über die Leseseite**: in allen Domänen-Projekten werden Fähigkeiten, Bündel, Projektionen/Reaktionen, Reader, Queries, Responses, ReadModels durch Scaffolder-Dateien ersetzt, die Store-Impls bleiben stehen (sie müssen gegen die geschriebenen Fähigkeiten kompilieren). Gegenprobe: Scaffolder ohne Fähigkeits-Parameter ⇒ 10 Fixpunkt-Abweichungen + 25 Compile-Fehler. |
| Sonde | `Sonde/Gezeichnet.board.json`: eine im Editor gezeichnete Leseseite (neuer Store mit einer im Panel benannten und einer abgeleiteten Fähigkeit, Projektion, Reaktion, Reader, Query/Response/ReadModel) → Scaffolder → Fork → neu eingelesen; `soll.txt` von Hand um 15 Fakten erweitert (u. a. `faehigkeit …`-Zeilen). Das Format der gezeichneten Eingabe folgt dem Zielbild. |

**Gemessen:** Solution-Build 0 Fehler; Prüfstand **166/166** (neu: 11 `DomainEditorLeseseiteTests`, darunter Scaffolder-Ausgabe
kompiliert + besteht **alle** Analyzer aus `Domain.SourceGeneration` — CQRS050/051/054/055/057); `--check` grün (232 Typen in 4
Domänen-Projekten durch 71 Scaffolder-Dateien ersetzt, davon 44 Fähigkeiten, 5 Bündel, 5 Projektionen/Reaktionen, 5 Reader);
`--sonde` grün (**61** Soll-Fakten).

## 13 · Bestehendes ändern (2026-09-30)

**Regel des Abgleichs** (gilt für das sofortige Schreiben je Geste): die Geste ändert Signatur/Deklaration, Rümpfe und
Handcode gehören dem Code; Unverändertes bleibt Wort für Wort (auch Formatierung: mehrzeilige Parameterlisten, unveränderte
Prozess-Regeln samt Kommentaren); bricht dadurch ein Rumpf, meldet es der Bau.

Was jede Geste sofort schreibt:

| Geste | Was geschrieben wird |
|---|---|
| Feld an bestehendem Record hinzu/weg/Typ | Positions-Parameter bzw. Property-Felder (Feldregeln = `Codeformen.Feldregeln`, EINE Quelle für Extractor und Schreiber) |
| Enum-Wert | Member-Liste (vorhandene Member wörtlich) |
| State-Feld | Property einfügen/ersetzen/entfernen (Zusatz und `Id`/`Version` bleiben) |
| OneOf eines Deciders | nur das Typ-Argument der Rückgabe |
| Regel eines Prozesses | Anweisungen im `Definiere`-Lambda; unveränderte Regeln wörtlich; Ausdrucks-Lambda → Block bei zweiter Regel |
| Ausgänge eines Handles (Responses, sendet, veröffentlicht) | nur das Typ-Argument der Rückgabe; Wrapper bleibt (`Task`/`IAsyncEnumerable`/`IEnumerable`) |
| Parameter/Rückgabe einer Store-Fn | Fähigkeits-Interface + Impl-Methode (Parameter wörtlich, wo unverändert) |
| Pull/Append, TrackDeps, SubscriberId, Reader → Projektion | Basisliste (Marker, `IReader<P>`), `[ProjectionReader(TrackDeps = …)]`, Literal (auch über `const` derselben Klasse) |
