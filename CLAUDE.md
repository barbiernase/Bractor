# CLAUDE.md

Projektgedächtnis für Claude Code — bewusst schlank, nur Wissen, kein Changelog (Historie steht in Git).
**Maßstab ist `docs/konzept-werkbank.md`** (Zielbild, umgesetzt 2026-10-13); wo etwas widerspricht, gilt es. Wegweiser: `docs/README.md`.
Offenes und Schulden: `docs/13-reifegrad-schulden-bewertung.md` §13.0. Zahlen und Messwerte: `docs/12-tests-und-vermessung.md`.

## Was das Projekt ist

Ein **allgemeines** signalbasiertes CQRS-/Event-Sourcing-Framework auf **Proto.Actor** (virtuelle Cluster-Actors), **Marten/PostgreSQL**
(Event-Store, einzige Wahrheit) und **Redis** (nicht-autoritativer Versions-Index). Events werden geordnet und genau einmal wirksam an
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

## Werkbank-Leitsätze (Kurzform von `docs/konzept-werkbank.md`)

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

## Das tragende Bild

- **Schreibseite:** Command → Aggregat-Actor (Single-Writer, Inbox-Dedup) → `Decide` → Events → Group-Commit → Signal.
- **Vier durable Konsumenten, eine Maschine:** Projektion, Reaktion, Prozess, Fluss. Projektion und Reaktion laufen über die Pull-/Signal-Schleife
  `ProjectionAdapter` (Signal schnell + Poll 30 s). Ob replaybar (`IProjectionTracker`, Co-Commit) oder emittierend (`IEmittentenCursor`, kein Reset),
  folgt aus den **Schreib-Fähigkeiten der Handles** und dem Rückgabetyp — kein zweiter Marker.
- **Dirigent:** Prozesse (`IProzessDefinition`) und Flüsse (`IPipeline`, `PipelineFluss.Definiere`) laufen auf demselben `ProzessManager`
  (Event-Regeln, Marking-Cursor, Backstop). Verarbeitung ist immer eine **Katalog-Funktion** (`IFunktion`, `Rufe<F>`); Laufort Host/Extern/Hand
  steht in der Umgebung. Technik hinter einer Funktion ist ein **Domänen-Dienst** (`IDienst`), gebunden nach der Umgebung.
- **Emit:** genau ein Weg (`CommandEmitter`), erzwungen durch CQRS020/021.
- **Akteure:** alles, was hineinkommt, kommt von einem Akteur (`IDarf<T>`); Token → Akteur-Menge aus `Identitaeten`.

## Wo was liegt

| Bereich | Ort |
|---|---|
| Verträge, Marker, geteilte Regeln (eine Quelle) | `Abstractions/` (z. B. `Akteurvertrag.cs`, `Funktionsvertrag.cs`, `Dienstvertrag.cs`, `Projektstruktur.cs`, `Spur/WirkerId.cs`, `Spur/SpurWeg.cs`, `StoreZugriff.cs`, `Umgebung/UmgebungKonfig.cs`) |
| Laufzeit (Framework) | `Infrastructure/` (Prozess, Projections, Quellen/Treiber, Funktionen, Dienste, Umgebung, Einblick, Eingang, Akteure); referenziert keine Domäne, liest sie über `Komposition/Anwendung.cs` (`IAnwendung`) |
| Leitung | `Cqrs.Leitung/cqrs.proto` (Framework: Dienst, Hüllen, `Nutzlast`); je Anwendung `<Wurzel>.Leitung` (Domänen-DTOs + `GeneratedNutzlast`) |
| Komposition | `<Wurzel>.Laufzeit` (hier `Domain.Laufzeit`): Laufzeit-Generate, `GeneratedAnwendung`, `AddCqrs(args)` der Hosts |
| Generatoren und Analyzer | `*.SourceGeneration/`; Codegen vor dem Build (Proto, STJ-Kontexte, Python-Basen): `Cqrs.Codegen` über das Target in `Domain.Leitung` |
| Lesen, Sicht, Schreiben, Prüfen | `GraphExtractor.Kern/` (`Lesen`, `Sicht/`, `Schreiben/`, `Sonde/`, `ParitaetsPruefung`), CLI `GraphExtractor/` |
| Grammatik, Scaffolder | `DomainEditor/` |
| Werkbank | `Werkbank/` (Server `Werkbank/Server/`, Oberfläche `Werkbank/wwwroot/editor/`) |
| Hosts | `Host.Grpc` (`AddCqrs(args)` der Komposition + `LaufeAsync`), `Host.Blazor` (Client) — beide mit `--umgebung <name>` |
| Beispieldomäne (Prüfling) | `Domain*`, `Domain.Akteure`, `Domain.Client.*` |
| Prüfdomäne (zweite Anwendung, Framework-Tests) | `Pruefdomaene`, `Pruefdomaene.Laufzeit`, `Pruefdomaene.Integration.Tests` |

## Konventionen

- Kommentare und Domäne auf Deutsch. Neue Verträge → `Abstractions`; Marten/Infra → `Infrastructure`.
- **Nur Code-Fakten** (W2): neue Erkennung über Marker/Attribut/Symbol; neues Konstrukt ⇒ Sonde (`GraphExtractor.Kern/Sonde/`) + `soll.txt` erweitern.
- **Projektstruktur = eine Tabelle** (`Abstractions/Projektstruktur.cs`, docs/07): Rollen als Suffix zur Wurzel, Primitive → Projekt und Datei;
  nie Projektnamen tippen, sondern die Tabelle lesen. Falscher Ort = CQRS098 bzw. `--check` „Lage“; neue Primitive ⇒ `Lageart.cs` + Tabelle.
  Die Sonde muss alle allgemeinen Primitive abdecken, nicht nur die der Beispieldomäne.
- **Neue Dispatch-Logik = Generator erweitern**, nie Handschalter, nie Reflection.
- **Ausgabe-Vertrag in der Signatur (CQRS050):** Decide und jedes Handle geben einen konkreten Typ oder `OneOf<…>` konkreter Typen zurück.
  WAS entstehen kann, lesen Generatoren/Extractor/Editor NUR aus der Signatur; Rümpfe liefern nichts.
- **Fähigkeiten statt Rumpf-Analyse:** ein Handle bekommt die Store-Funktionen als Parameter (`: IWriteStore`/`: IReadStore`, genau eine Funktion,
  CQRS051; Bündel `: IStore`). Nie Store/`IFristplan` in Ctor/Feld (CQRS054), nie `new …Store()` (CQRS055).
- **Grammatik = eine Quelle** (`DomainEditor/Grammatik.cs`): neue Regel ⇒ Analyzer + Test + Gegenstück in der Grammatik.
- **Akteur, Akteur-Vertrag, Client** nicht vermischen (`docs/konzept-akteure.md`): Akteur = `IDarf` für das, was er selbst hineingibt;
  Vertrag = was er draußen tut (`Auf` mit Ausgabe = **Zusage**, `void Auf` = **Kenntnis**; „Reaktion“ heißt nur der Backend-Baustein `ISubscriber`);
  Client = Software an einer Leitung, wirksam = Vertrag ∩ Token, C#-Client per `[assembly: IstClient<IX>]`.
- **Kein `InMemoryEventStore`:** Store-Semantik nur gegen echtes Marten (Integration); der Prüfstand testet nur store-freie Logik.
- **Proto und STJ-Kontexte entstehen automatisch** beim Build (Target `CqrsCodegenPrepass` in `Domain.Leitung/Domain.Leitung.csproj`, auch bei ⟳);
  `./codegen.sh --check` prüft Drift (alle Anwendungen). Kein manueller Schritt. Der Prepass (`Cqrs.Codegen/CqrsCodegen.targets`) hängt an der
  Leitung bzw. ohne Leitung an der Komposition; die Wurzel folgt aus dem Projektnamen.
- **Werkbank:** Gesten nie auf `main`; „🤖 füllen“ nur über `claude -p` (Abo, nie API).

## Build / Test / Werkzeuge

**Vor Test oder Messung `docs/12-tests-und-vermessung.md` lesen.** Integration immer sequentiell; xUnit schluckt App-Logs.

- Build: `dotnet build`
- Prüfstand (store-frei, immer grün): `dotnet test Infrastructure.Pruefstand.Tests/Infrastructure.Pruefstand.Tests.csproj`
- Integration (Postgres/Consul/Redis, sequentiell): `dotnet test Infrastructure.Integration.Tests/Infrastructure.Integration.Tests.csproj`
- Prüfdomäne (Framework domänenneutral, echtes Marten, sequentiell, nie parallel zur anderen Integrations-Suite):
  `dotnet test Pruefdomaene.Integration.Tests/Pruefdomaene.Integration.Tests.csproj`
- Infra hochfahren: `docker compose -f deploy-linux/docker-compose.infrastructure.yml up -d`
- Werkbank: `dotnet run --project Werkbank` → http://localhost:5178/editor/ (in `.claude/launch.json` als „Werkbank“). ⟳ baut und startet den echten
  Host in der gewählten Umgebung; Bedienung (Palette, ⊕, ✎, 🤖, ↶, Spur): `docs/konzept-werkbank.md` §6–§8.
- Hosts: `dotnet run --project Host.Grpc -- --umgebung sim-main` bzw. `Host.Blazor -- --umgebung sim-main` (Sim-DB `cqrs_sim_<branch>` über `CQRS_BRANCH`).
- Multi-Node (3 Nodes + Consul): `docker compose -f deploy-multinode/docker-compose.yml up -d --build` (`docs/06-transport-multinode-betrieb.md`).
- Editor-Parität (Code ⇄ Lesen ⇄ Karten, schreibt nichts): `dotnet run --project GraphExtractor -- --check`
- Agnostik-Sonde (fremde Domäne + Gesten-Skript + Fixpunkt): `dotnet run --project GraphExtractor -- --sonde`
- Grammatik als Liste „Regel → Build-Gegenstück“: `dotnet run --project GraphExtractor -- --grammatik`
- Warmer Workspace messen: `dotnet run --project GraphExtractor -- --warm`
- LLM-Kontexte: `dotnet run --project GraphExtractor -- --kontexte <verz>` bzw. `--kontext <Disc>`, Slots: `--slots` (`docs/konzept-llm-minimalkontext.md`)

## Wo steht was

| Frage | Dokument |
|---|---|
| Zielbild, Leitsätze, Bedienung der Werkbank | `docs/konzept-werkbank.md` |
| Wie schreibe ich X? | `docs/10-entwickler-api.md` |
| Warum? | `docs/02-design-prinzipien.md` |
| Akteure, Verträge, Clients | `docs/konzept-akteure.md` |
| Fluss (Pipeline) im Detail | `docs/konzept-editor-pipelines.md` §14 |
| Offen, Schulden | `docs/13-reifegrad-schulden-bewertung.md` §13.0 |
| Tests, Zahlen, Messwerte | `docs/12-tests-und-vermessung.md` |

**Hinweis (2026-10-13):** Die Doku wird gerade gegen das Zielbild aufgeräumt; Kapitel 01–11 enthalten noch veraltete Stellen
(z. B. gelöschte Beispiel-Domänen, „Ctor-Stores“, manueller Proto-Schritt). Für Fakten gilt der Code, für das Ziel `docs/konzept-werkbank.md`.
