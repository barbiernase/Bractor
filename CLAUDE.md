# CLAUDE.md

Projektgedächtnis für Claude Code — bewusst schlank, nur Wissen, kein Changelog (Historie steht in Git).
**Maßstab im Zweig `pubsub_neustrukturierung` ist `docs/plan-pubsub-neustrukturierung.md`** (Zielbild, Grammatik, Garantien, Phasen des
Umbaus; noch nicht umgesetzt). Wo etwas widerspricht, gilt der Plan als Ziel und der Code als Ist. Wegweiser: `docs/README.md`.
Offenes und Schulden: `docs/13-reifegrad-schulden-bewertung.md` §13.0. Zahlen und Messwerte: `docs/12-tests-und-vermessung.md`.
Das Bild unten („Das tragende Bild“) beschreibt den **Ist-Zustand** nach Phase 6 des Umbaus.

## Was das Projekt ist

Ein **allgemeines** signalbasiertes CQRS-/Event-Sourcing-Framework auf **Proto.Actor** (virtuelle Cluster-Actors), **Marten/PostgreSQL**
(Event-Store, einzige Wahrheit). Events werden geordnet und genau einmal wirksam an
Projektionen, Reaktionen, Prozesse und Flüsse zugestellt; alles Dispatchende ist zur Compile-Zeit generiert. Dazu gehört die **Werkbank**:
ein Editor, in dem eine Anwendung entworfen, als Code erzeugt, gebaut, im echten Host durchgespielt und über die Spur nachvollzogen wird.
Die Domänen im Repo (ImagePair, Datensatz, Training …) sind nur **Prüfling und Beispiel**, nicht der Maßstab.

## Die sechs Invarianten

1. Die Wahrheit ist der Log. Ordnung, Vollständigkeit und Wiederholbarkeit kommen NUR aus dem Event-Store-Read.
2. Das Signal ist nur ein Weckruf: trägt nur `(StreamId, Version)`, darf verloren, doppelt, ungeordnet sein.
3. Routing über Typen — nie ein handgebauter Identitäts-String.
4. **Keine Reflection.** Die Laufzeit nutzt generierte Tabellen/Kontexte; Werkzeuge (Codegen, Schema, Extractor) lesen über Roslyn.
5. Der Fachcode bleibt rein: Cursor, Signal, Ordnung, Exactly-once, Sharding, Prozess-Maschinerie tauchen im Entwickler-Code nie auf.
6. Persistent genau dann, wenn ein durabler Konsument abhängt. Verlierbares (Tick, Ablehnung, UI-Feedback, Kenntnis-Push an Clients) bleibt
   auf dem schnellen Kanal. Quell-Nachrichten (z. B. vom Datei-Treiber) sind durabel: genau einmal je Kennung im Log.

Im Umbau ändert sich Invariante 2 zu „Der Weckruf trägt nur sein Ziel; er darf verloren, doppelt, ungeordnet sein“ (Plan §2.3).

## Werkbank-Leitsätze (Kurzform; das ausführliche Werkbank-Konzept liegt im Git-Verlauf, die Editor-Oberfläche ist im Umbau zurückgestellt)

- **W1** Code ist die einzige Wahrheit; gespeichert wird nur die Ansicht (`.werkbank/ansicht.json`). Unfertiges = Code, der nicht baut.
- **W2** Der Graph kommt aus dem Code über Roslyn: WAS etwas ist, sagen nur Code-Fakten (Marker, Attribute, Symbole, Signaturen), keine Domänen-Namen
  in Framework und Werkzeugen. WO etwas liegt, legt die feste Projekt- und Ordnerstruktur fest (`docs/07-projektstruktur.md`, eine Quelle, geprüft).
- **W3** Alles läuft im Backend: Ableitungen rechnet der Server (`GraphExtractor.Kern/Sicht`), der Editor ist eine dumme View.
- **W4** Jede Geste ist sofort ein Patch am Code; ↶ nimmt den Patch zurück.
- **W5** Prüfen = Bauen (Analyzer, Generatoren, Boot-Guards). `DomainEditor/Grammatik.cs` ist nur Daten (⊕, `--grammatik`).
- **W6** Keine Simulation: Durchspielen = echter Host in Umgebung `sim` gegen eine eigene, echte DB.
- **W7** Umgebung = eine Datei (`umgebungen/umgebung.<name>.json`); Struktur ist Code, Auswahl und Werte sind Konfig.
- **W8** Quellen = fest codierte Treiber + Konfig; die Domäne benennt eine Quelle nur als Typ.
- **W9** Spur: jeder Log-Eintrag trägt `ursache`, `wirker`, `ueber`, `stand`; der Wirker ist die Karten-Id.
- **W10** Keine Reflection, auch nicht in Werkzeugen (= Inv. 4).
- **W11** Allgemeines Framework: eine Primitive bleibt, wenn sie allgemein gebraucht wird — nicht, weil die Beispieldomäne sie nutzt. Je Absicht ein Weg.
- **W12** Keine GUIs im Editor (Clients nur als Vertrag), keine Schema-Evolution im Editor (`IUpcast` bleibt als Backend-Primitive), kein Deploy.

## Das tragende Bild (Ist-Zustand nach Plan-Phase 6)

- **Schreibseite:** Command → Aggregat-Actor (Single-Writer, Inbox-Tabelle) → `Decide` (`OneOf<Event, Hinweis, Frist<TCmd>>`) → Events →
  Group-Commit → **Wecker** am Commit (generierter Weckplan, für jeden Schreiber gleich: Aggregat, Quelle, Funktions-Ausgabe) + Push an Clients
  (Envelope, Broker bis Phase 7). **Keine Marken im Log**
  (Phase 5): Noop und Ablehnung schreiben nur ihre Quittung in die Inbox, mit Kopf-Prüfung des Streams in derselben Transaktion
  (`cqrs_kopf`); eine Version je Aggregat (= Zahl seiner Events), beim Client aus Pushes und Antworten (`VersioningModule`).
- **Zwei durable Konsumenten:** **Projektion** (`ISubscriber`, `Task Handle` mit Schreib-Fähigkeiten genau eines Stores; `BatchLeser` je
  Projektion und Partition, Co-Commit, replaybar) und **Funktion** (`IFunktion` mit `Auf(Eingang[, Herkunft][, Lese-Fähigkeiten])`;
  `FunktionsActor` je Funktion und Adresse — Partition, oder eine mit `[EineAdresse]` —, Ausgabe festgehalten oder gleichbleibend, nie replayt).
  Beide: Wecker am Commit schnell (je Commit und Ziel ein `Wake`, Weckplan aus den Signaturen, beim Start geprüft: GR-WECKPLAN, CQRS108) +
  Takt 30 s je Partition. Den Stand je gezeigter Id einer Antwort liefert der Guard der Projektion (`ProjektionsStaende`), kein Redis.
- **Funktion als Actor** (Plan §2.9, Phase 4b): Umsetzung und Dienste je Aktivierung (DI-Bereich, geschlossen beim Stoppen); gekapselter Zustand
  über `IZustand<T>` im Konstruktor (gesichert mit der Position in einer Transaktion, ein `Auf` zugleich, CQRS105); optionaler Lebenszyklus
  `ILebenszyklus` (`BeimAktivierenAsync` nach dem Laden, `VorDemSichernAsync` vor jeder Sicherung, CQRS107); **Hinweis-Netz**: Hinweise
  (`ITransientEvent`) aus Funktionen, Aggregaten und Clients gehen über die generierte Tabelle direkt per `Bote.Melde` an die hörenden Funktionen,
  ohne Log und Broker, höchstens einmal; Commands daraus mit Ids aus der Hinweis-Kennung (CQRS106: gehörter Hinweis braucht einen Erzeuger).
- **Abläufe** sind ein Vorgangs-Aggregat (Join, Zählung, Kompensation, Frist im Decide) plus Funktionen als Kanten; die Zugehörigkeit ist ein
  fachliches Feld (A2). Kein Dirigent, keine Saga, keine Fluss-DSL, keine Reaktion als eigene Art.
- **Uhr:** `Frist<TCmd>` aus dem Decide → `FristGesetzt` (`IRahmenEvent`) + Beilage `cqrs_fristen` → die Uhr sendet fällig mit Id aus dem Frist-Event.
- **Laufort** einer Funktion (Host/Extern/Hand) steht in der Umgebung; draußen über Vermittler + `BausteinSitzung` mit Lease. Technik hinter
  einer Funktion ist ein Domänen-Dienst (`IDienst`).
- **Emit:** genau ein Weg (`CommandEmitter`, Id vom Aufrufer abgeleitet), erzwungen durch CQRS020/021.
- **Akteure und Clients:** alles, was hineinkommt, kommt von einem Akteur (`IDarf<T>`); ein Client-Vertrag trägt Funktionen (`ITraegt<F>`),
  sendet (`ISendet<T>`), fragt (`IFragt<T>`) und hört (`IHoert<T>`); wirksam = Vertrag ∩ Token.
- Noch alt (spätere Phasen): der Broker für alles, was an Clients geht (Pushes, Ablehnungen, `CommandFailed`, Hinweise; Verteiler je Knoten
  in Phase 7), Projektionsform mit `ProjectionWriter`/`Track` (8).

## Wo was liegt

| Bereich | Ort |
|---|---|
| Verträge, Marker, geteilte Regeln (eine Quelle) | `Abstractions/` (z. B. `Akteurvertrag.cs`, `Funktionsvertrag.cs`, `Dienstvertrag.cs`, `Projektstruktur.cs`, `Spur/WirkerId.cs`, `Spur/SpurWeg.cs`, `StoreZugriff.cs`, `Umgebung/UmgebungKonfig.cs`) |
| Laufzeit (Framework) | `Infrastructure/` (Projections, Wecken, Funktionen, Uhr, Quellen/Treiber, Dienste, Umgebung, Einblick, Eingang, Akteure); referenziert keine Domäne, liest sie über `Komposition/Anwendung.cs` (`IAnwendung`) |
| Leitung | `Cqrs.Leitung/cqrs.proto` (Framework: Dienst, Hüllen, `Nutzlast`); je Anwendung `<Wurzel>.Leitung` (Domänen-DTOs + `GeneratedNutzlast`) |
| Komposition | `<Wurzel>.Laufzeit` (hier `Domain.Laufzeit`): Laufzeit-Generate, `GeneratedAnwendung`, `AddCqrs(args)` der Hosts |
| Generatoren und Analyzer | `*.SourceGeneration/`; Codegen vor dem Build (Proto, STJ-Kontexte, Python-Basen): `Cqrs.Codegen` über das Target in `Domain.Leitung` |
| Lesen, Sicht, Schreiben, Prüfen | `GraphExtractor.Kern/` (`Lesen`, `Sicht/`, `Schreiben/`, `Sonde/`, `ParitaetsPruefung`), CLI `GraphExtractor/` |
| Grammatik, Scaffolder | `DomainEditor/` |
| Werkbank | `Werkbank/` (Server `Werkbank/Server/`, Oberfläche `Werkbank/wwwroot/editor/`) |
| Hosts | `Host.Grpc` (`AddCqrs(args)` der Komposition + `LaufeAsync`), `Host.Blazor` (Client) — beide mit `--umgebung <name>`; `Host.Pruefdomaene` (Messung S5) |
| Beispieldomäne (Prüfling) | `Domain*`, `Domain.Akteure`, `Domain.Client.*` |
| Prüfdomäne (zweite Anwendung, Framework-Tests) | `Pruefdomaene`, `Pruefdomaene.Projections`, `Pruefdomaene.Laufzeit`, `Pruefdomaene.Integration.Tests` |
| Vermessung (E2E-Treiber, Ablage) | `Cqrs.Vermessung`, `scripts/vermessung-*.sh`, `vermessung/ergebnisse/` |

## Konventionen

- Kommentare und Domäne auf Deutsch. Neue Verträge → `Abstractions`; Marten/Infra → `Infrastructure`.
- **Nur Code-Fakten** (W2): neue Erkennung über Marker/Attribut/Symbol; neues Konstrukt ⇒ Sonde (`GraphExtractor.Kern/Sonde/`) + `soll.txt` erweitern.
- **Projektstruktur = eine Tabelle** (`Abstractions/Projektstruktur.cs`, docs/07): Rollen als Suffix zur Wurzel, Primitive → Projekt und Datei;
  nie Projektnamen tippen, sondern die Tabelle lesen. Falscher Ort = CQRS098 bzw. `--check` „Lage“; neue Primitive ⇒ `Lageart.cs` + Tabelle.
  Die Sonde muss alle allgemeinen Primitive abdecken, nicht nur die der Beispieldomäne.
- **Neue Dispatch-Logik = Generator erweitern**, nie Handschalter, nie Reflection.
- **Typisierte Ids:** ein Command ist `record … : ICommand<TAggregat>` mit `Id<TAggregat> AggregateId` (das Ziel steht im Typ; Framework-Sicht
  `ICommand.Ziel`, CQRS013/CQRS010); Verweise auf Aggregate in Events, Queries, Antworten sind `Id<T>` (UUID auf Leitung, Log, Wire; Namensregel
  `Abstractions/Idvertrag.cs`). Read-Models und Store-Fähigkeiten bleiben bis zur neuen Projektionsform (Plan Phase 8) bei `Guid`.
- **Ausgabe-Vertrag in der Signatur (CQRS050):** Decide und jedes Handle geben einen konkreten Typ oder `OneOf<…>` konkreter Typen zurück.
  WAS entstehen kann, lesen Generatoren/Extractor/Editor NUR aus der Signatur; Rümpfe liefern nichts.
- **Fähigkeiten statt Rumpf-Analyse:** ein Handle bekommt die Store-Funktionen als Parameter (`: IWriteStore`/`: IReadStore`, genau eine Funktion,
  CQRS051; Bündel `: IStore`). Nie Store/`IFristplan` in Ctor/Feld (CQRS054), nie `new …Store()` (CQRS055).
- **Grammatik = eine Quelle** (`DomainEditor/Grammatik.cs`): neue Regel ⇒ Analyzer + Test + Gegenstück in der Grammatik.
- **Akteur und Client-Vertrag** nicht vermischen (`docs/konzept-akteure.md`): Akteur = `IDarf` für das, was er selbst hineingibt (auch auf
  Funktions-Schnittstellen); Client-Vertrag = was eine Software an einer Leitung tut: trägt Funktionen (`ITraegt<F>`, früher „Zusage“), sendet,
  fragt, hört (`IHoert<T>`, früher „Kenntnis“); wirksam = Vertrag ∩ Token, C#-Client per `[assembly: IstClient<IX>]`.
- **Kein `InMemoryEventStore`:** Store-Semantik nur gegen echtes Marten (Integration); der Prüfstand testet nur store-freie Logik.
- **Actors:** jeder Framework-Actor erbt `RahmenActor` (Bereich, Stopp, Passivierung, Fehlerantwort, Zusammenfassen); Cluster-Nachrichten nur
  über den `Bote` — `Melde` (verlierbar) oder `FrageAsync` (Pflicht-Frist), nie `Cluster.RequestAsync` direkt (`Infrastructure/Lebenszyklus/`).
- **Proto und STJ-Kontexte entstehen automatisch** beim Build (Target `CqrsCodegenPrepass` in `Domain.Leitung/Domain.Leitung.csproj`, auch bei ⟳);
  `./codegen.sh --check` prüft Drift (alle Anwendungen). Kein manueller Schritt. Der Prepass (`Cqrs.Codegen/CqrsCodegen.targets`) hängt an der
  Leitung bzw. ohne Leitung an der Komposition; die Wurzel folgt aus dem Projektnamen.
- **Werkbank:** Gesten nie auf `main`; „🤖 füllen“ nur über `claude -p` (Abo, nie API).

## Build / Test / Werkzeuge

**Vor Test oder Messung `docs/12-tests-und-vermessung.md` lesen.** Integration immer sequentiell; xUnit schluckt App-Logs.

- Build: `dotnet build`
- Prüfstand (store-frei, immer grün): `dotnet test Infrastructure.Pruefstand.Tests/Infrastructure.Pruefstand.Tests.csproj`
- Integration (Postgres/Consul, sequentiell): `dotnet test Infrastructure.Integration.Tests/Infrastructure.Integration.Tests.csproj`
- Prüfdomäne (Framework domänenneutral, echtes Marten, sequentiell, nie parallel zur anderen Integrations-Suite):
  `dotnet test Pruefdomaene.Integration.Tests/Pruefdomaene.Integration.Tests.csproj`
- Verhaltens-Netz des Umbaus (echte Hosts als Prozesse, nur von außen angesprochen, sequentiell, nie parallel zu Integration oder Messung):
  `dotnet test Verhalten.Tests/Verhalten.Tests.csproj` (docs/12 §12.6)
- Garantie-Suite (vor jedem Commit, Integration nacheinander): `--filter Kategorie=Garantie` auf Prüfstand, Integration, Prüfdomäne;
  offene Lücken sind `Skip = "G-n offen (docs/13)"` (`docs/11-garantien.md` §11.14)
- E2E-Messung (echter Host, Exactly-once, nie parallel zu Integrations-Suiten): `scripts/vermessung-e2e.sh --anlass <a> --spalte <s>`,
  Tabelle `scripts/vermessung-tabelle.sh --schreibe` (`docs/12-tests-und-vermessung.md` §12.5/§12.6)
- **Messserver** (Messungen, Integration, Gate — statt auf dem MacBook): `scripts/server.sh lauf <befehl …>` führt den gepushten `HEAD` dort aus
  und holt `vermessung/ergebnisse/` zurück; `einrichten`, `status`, `warte`, `log` (docs/12 §12.6)
- Infra hochfahren: `docker compose -f docker-compose.infrastructure.yml up -d`
- Werkbank-Abnahme (echte Werkbank auf einer Kopie, eigene Sim-DB, sequentiell, nach `dotnet build`):
  `dotnet test Werkbank.Abnahme.Tests/Werkbank.Abnahme.Tests.csproj`
- Werkbank: `dotnet run --project Werkbank` → http://localhost:5178/editor/ (in `.claude/launch.json` als „Werkbank“). ⟳ baut und startet den echten
  Host in der gewählten Umgebung (Oberfläche im Umbau zurückgestellt, Plan E4).
- Hosts: `dotnet run --project Host.Grpc -- --umgebung sim-main` bzw. `Host.Blazor -- --umgebung sim-main` (Sim-DB `cqrs_sim_<branch>` über `CQRS_BRANCH`).
- Multi-Node (3 Nodes + Consul): `docker compose -f deploy-multinode/docker-compose.yml up -d --build` (`docs/06-transport-multinode-betrieb.md`).
- Gate „frischer Checkout baut“ (vor jedem Push): `./pruefe-checkout.sh` (Worktree des Commits: ignorierte Quellen, Build, Drift, Prüfstand)
- Editor-Parität (Code ⇄ Lesen ⇄ Karten, schreibt nichts): `dotnet run --project GraphExtractor -- --check`
- Agnostik-Sonde (fremde Domäne + Gesten-Skript + Fixpunkt): `dotnet run --project GraphExtractor -- --sonde`
- Grammatik als Liste „Regel → Build-Gegenstück“: `dotnet run --project GraphExtractor -- --grammatik`
- Warmer Workspace messen: `dotnet run --project GraphExtractor -- --warm`
- LLM-Kontexte: `dotnet run --project GraphExtractor -- --kontexte <verz>` bzw. `--kontext <Disc>`, Slots: `--slots`

## Wo steht was

| Frage | Dokument |
|---|---|
| Ziel des Umbaus, neue Grammatik, Garantien, Schritte | `docs/plan-pubsub-neustrukturierung.md` |
| Was ist heute garantiert (Wirkung, Ordnung, Zustellung)? | `docs/11-garantien.md` |
| Wie funktioniert es heute? | `docs/03` (Schreibseite), `docs/04` (Konsum, Funktionen, Uhr), `docs/05` (Generatoren), `docs/06` (Transport) |
| Akteure, Verträge, Clients | `docs/konzept-akteure.md`, `docs/08` (Blazor), `docs/09` (Python) |
| Offen, Schulden | `docs/13-reifegrad-schulden-bewertung.md` §13.0 |
| Tests, Zahlen, Messwerte | `docs/12-tests-und-vermessung.md` |

**Hinweis (2026-10-26):** Für den Umbau wurden veraltete und nicht benötigte Dokumente gelöscht (Liste in `docs/README.md`, Stand im Git-Verlauf
bei `861ea7f`). Verweise darauf in Kapiteln und Code-Kommentaren werden in Phase 11 des Plans nachgezogen. Für Fakten gilt der Code.
