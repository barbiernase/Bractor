# CLAUDE.md

Projektgedächtnis für Claude Code — bewusst schlank, nur Wissen, kein Changelog (Historie steht in Git).
Die Kapitel unter `docs/` beschreiben den Ist-Zustand; für Fakten gilt der Code. Grammatik („Wie schreibe ich X?“), Regeln R1–R20 und die
Entscheidungen E1–E16/A1–A8: `docs/10-entwickler-api.md`. Wegweiser: `docs/README.md`. Offenes und Schulden:
`docs/13-reifegrad-schulden-bewertung.md` §13.0. Zahlen und Messwerte: `docs/12-tests-und-vermessung.md`.

## Was das Projekt ist

Ein **allgemeines** CQRS-/Event-Sourcing-Framework auf **Proto.Actor** (virtuelle Cluster-Actors), **Marten/PostgreSQL**
(Event-Store, einzige Wahrheit). Events werden geordnet und genau einmal wirksam an
Projektionen zugestellt; gerechnet wird vor dem Aggregat in typisierten Funktionsgraphen, deren Commands genau einmal wirken; alles
Dispatchende ist zur Compile-Zeit generiert. Dazu gehört die **Werkbank**:
ein Editor, in dem eine Anwendung entworfen, als Code erzeugt, gebaut, im echten Host durchgespielt und über die Spur nachvollzogen wird.
Die Domänen im Repo (ImagePair, Datensatz, Training …) sind nur **Prüfling und Beispiel**, nicht der Maßstab.

## Die sechs Invarianten

1. Die Wahrheit ist der Log. Ordnung, Vollständigkeit und Wiederholbarkeit kommen NUR aus dem Event-Store-Read.
2. Der Weckruf trägt nur sein Ziel; er darf verloren, doppelt, ungeordnet sein (Wecker am Commit, Takt als Netz).
3. Routing über Typen — nie ein handgebauter Identitäts-String.
4. **Keine Reflection.** Die Laufzeit nutzt generierte Tabellen/Kontexte; Werkzeuge (Codegen, Schema, Extractor) lesen über Roslyn.
5. Der Fachcode bleibt rein: Cursor, Weckruf, Ordnung, Exactly-once, Sharding, Prozess-Maschinerie tauchen im Entwickler-Code nie auf.
6. Persistent genau dann, wenn ein durabler Konsument abhängt. Verlierbares (Tick, Ablehnung, UI-Feedback, Kenntnis-Push an Clients) bleibt
   auf dem schnellen Kanal. Was eine Quelle meldet, steht nicht im Log: durabel ist sie über ihren gesicherten Zustand (was sie gesehen hat,
   ihre laufende Nummer), gesichert, wenn die Kette des Elements fertig ist.


## Werkbank-Leitsätze (Kurzform; das ausführliche Werkbank-Konzept liegt im Git-Verlauf, der Editor heute: docs/10 §10.9)

- **W1** Code ist die einzige Wahrheit; gespeichert wird nur die Ansicht (`.werkbank/ansicht.json`). Unfertiges = Code, der nicht baut.
- **W2** Der Graph kommt aus dem Code über Roslyn: WAS etwas ist, sagen nur Code-Fakten (Marker, Attribute, Symbole, Signaturen), keine Domänen-Namen
  in Framework und Werkzeugen. WO etwas liegt, legt die feste Projekt- und Ordnerstruktur fest (`docs/07-projektstruktur.md`, eine Quelle, geprüft).
- **W3** Alles läuft im Backend: Ableitungen rechnet der Server (`GraphExtractor.Kern/Sicht`), der Editor ist eine dumme View.
- **W4** Jede Geste ist sofort ein Patch am Code; ↶ nimmt den Patch zurück.
- **W5** Prüfen = Bauen (Analyzer, Generatoren, Boot-Guards). `DomainEditor/Grammatik.cs` ist nur Daten (⊕, `--grammatik`).
- **W6** Keine Simulation: Durchspielen = echter Host in Umgebung `sim` gegen eine eigene, echte DB.
- **W7** Umgebung = ein Ordner (`umgebungen/<name>/umgebung.json`, je Instanz eines Graphen `graphen/<instanz>.json`); Struktur ist Code,
  Auswahl und Werte sind Konfig.
- **W8** Quellen = Funktionen aus dem Katalog des Frameworks (`Core/Katalog`: `Dateiwaechter`, `Taktgeber`, `Webhook`) am Kopf eines Graphen; die
  Domäne nennt sie als Knoten, ihre Werte stehen in der Instanz.
- **W9** Spur: jeder Log-Eintrag trägt `ursache`, `wirker`, `ueber`, `stand`; der Wirker ist die Karten-Id.
- **W10** Keine Reflection, auch nicht in Werkzeugen (= Inv. 4).
- **W11** Allgemeines Framework: eine Primitive bleibt, wenn sie allgemein gebraucht wird — nicht, weil die Beispieldomäne sie nutzt. Je Absicht ein Weg.
- **W12** Keine GUIs im Editor (Clients nur als Record, der Akteure verkörpert), keine Schema-Evolution im Editor (`IUpcast` bleibt als Backend-Primitive), kein Deploy.

## Das tragende Bild

- **Drei Arten von Nachrichten:** ein **Command** ist eine Absicht an genau einen Empfänger — mit Ziel im Typ (`ICommand<TAggregat>`) an das
  Aggregat, ohne Ziel (`ICommand`) im Graphen an die Knoten, die seinen Erzeuger in `[Von]` nennen, sonst an den Kopf genau eines Graphen oder
  einen Akteur, der ihn hört (CQRS116); ein **Event** ist ein Fakt im Log, nur aus einem Decide; ein **Hinweis** (`ITransientEvent`) ist flüchtig und geht nur an Akteure
  draußen. Dazu Query und `Frist<TCmd>`.
- **Schreibseite:** Command → Aggregat-Actor (Single-Writer, Inbox-Tabelle) → `Decide` (`OneOf<Event, Hinweis, Frist<TCmd>, Command ohne Ziel>`,
  nie ein Command an ein Aggregat, CQRS114; ein Command ohne Ziel steht nicht im Log: je Empfänger — Instanz am Graph-Kopf oder Arbeit eines
  Akteurs — ein offener Eingang `cqrs_eingaenge` im selben Commit, der Zusteller bringt ihn hin) → Events → Group-Commit → **Wecker** am Commit
  (generierter Weckplan: Projektionen und Uhr) + Push an Clients (`Commitmeldung` → `Verteilung`: je Commit eine Nachricht an den Verteiler jedes
  Knotens). **Keine Marken im Log**:
  Noop und Ablehnung schreiben nur ihre Quittung in die Inbox, mit Kopf-Prüfung des Streams in derselben Transaktion
  (`cqrs_kopf`); eine Version je Aggregat (= Zahl seiner Events), beim Client aus Pushes und Antworten (`VersioningModule`).
- **Durabler Konsument des Logs ist die Projektion** (`IProjektion`, `Task Auf(Event[, Herkunft], Schreib-Fähigkeiten genau eines Stores)`,
  Kennung = Typname; `BatchLeser` je Projektion und Partition, replaybar; Wecker am Commit schnell — je Commit und Ziel ein `Wake`, Weckplan aus den
  Signaturen, beim Start geprüft: GR-WECKPLAN — + Takt 30 s je Partition).
- **Funktion** (docs/04 §4.4): eine Klasse `: IFunktion`, liest NIE das Log. Je `Auf` genau **eine Nachricht** (ein Command ohne Ziel; gerechnet
  je Ankunft — kein `T?`, `OneOf`, `IReadOnlyList` als Eingang, GR-EINGANG), dazu optional `Herkunft`, Lese-Fähigkeiten, `CancellationToken`; die Fälle der Rückgabe sind die Ausgangsports (`T`, `OneOf`, `IEnumerable`, `IAsyncEnumerable`:
  Commands und Hinweise). Ein Event hört sie nur als Antwort auf ihren eigenen Aggregat-Command (`CommandResult.Antwort`, CQRS108). Im Konstruktor:
  Konfig-Records, Technik (ohne Marker), `IZustand<T>`, `ILogger<T>`.
- **Graph** (docs/04 §4.4): `record X(Knoten …) : IGraph` in `Graphen.cs`; jeder Parameter ein Knoten (Funktion oder Graph, eine Funktion auch
  mehrfach). **Jede innere Kante steht am nehmenden Knoten** als `[Von(nameof(A), nameof(B))]` (eine genannte Quelle speist die Ports ihres Typs),
  jeder Kopf als `[Kopf]` (nimmt von außen, was keine Quelle speist; je Eingangstyp genau einer); nur eine Quelle trägt keins; was kein Knoten über
  `Von` nimmt, verlässt den Graphen (`GeneratedGraphen`, eine Lesart `Graphform`). Zwischen den Bausteinen bleibt es beim Typ-Routing mit genau einem
  Empfänger. Folge, Gabel, Wahl, Je sind Formen der Ausgaben; Zusammenführen, Sammeln, Warten stehen im Logik-Code (die Nachricht trägt mit, Zustand des
  Knotens oder ein Aggregat); Graph im
  Graphen über seine Signatur (`Von` hinein speist seine Köpfe, `Von(nameof(G))` nimmt seine Ausgänge); kreisfrei über die Knoten (CQRS115). Er läuft als **Instanz** (Datei
  `graphen/<instanz>.json`: `Graph`, bei Quellkopf `Akteur`, wahlweise `Rechner`, je Knoten Konfig-Werte, `Parallel`, `Warteschlange`, `Wiederholungen`, `Zeitlimit`,
  `Technik`), beliebig oft; ein Graph mit Command-Kopf je Umgebung einmal. Jede Kopf-Nachricht startet eine **Kette** (`Kette.cs`, kein
  Laufzettel): jeder Schritt (Parent) rechnet je ankommender Nachricht, schlägt jede Ausgabe in der generierten **Wegtabelle** nach und gibt sie
  direkt an die Parents ihrer Knoten — **gesichert** (Standard: „fertig“ erst nach den gesicherten Ausgaben, so reicht die Rückmeldung bis zum Kopf)
  oder **verlierbar** (`[Von(nameof(A), Verlierbar = true)]`: einmal, ohne Antwort); voll → „später“, der Absender schickt erneut. Haltbar ist nur
  der Kopf; Ids aus der Nachricht, aus der sie entstand (`Kettenvertrag`), Knoten und Stelle; ein scheiternder Knoten wird wiederholt, danach
  reicht „gescheitert“ zum Kopf (geparkt, `LaufGescheitert`). Laufzeit: Wurzel `funktionen` je Cluster-Knoten, Graph-Actor je Instanz (Halter und
  Kopf), Parent je Knoten, Kinder bis `Parallel`, Orte im Gossip-Zustand, Wege über den Bote.
- **Quellen** sind Katalogfunktionen mit `Lausche(CancellationToken)` am Kopf eines Graphen (`Dateiwaechter` → `DateiGefunden`, `Taktgeber` →
  `Takt`, `Webhook` → `Aufruf`; Commands in `Abstractions/Katalog.cs`); je Element eine Kette, nichts im Log; eine Quellinstanz läuft genau einmal
  im Cluster. **Zustand** `IZustand<T>` ist gesichert je Instanz und Knoten (`cqrs_zustaende`), nach beendeten Aufrufen mit deren Kennungen (nach einem Absturz zählt nichts doppelt).
- **Arbeit für Akteure** (docs/konzept-akteure.md §5): hört ein Akteur einen Command ohne Ziel (`IHoert<PaarZuBeurteilen>`), ist das seine Arbeit —
  eine eigene Instanz unter seinem Namen mit einem Knoten draußen; die `Werkstatt` des Knotens gibt den Auftrag einer Sitzung mit „verkörpert ∩
  Token“ und stellt ihn erneut zu, bis eine Antwort kommt (die erste gewinnt); die Antwort sind Commands seines `IDarf`, Ids aus dem Auftrag. Von
  Hand antwortet die Werkbank (`Umgebung:Hand`).
- **Reader** (`IReader`, `Frage(Query, Lese-Fähigkeiten)`): die gezeigten Ids findet der Generator in den `Id<T>`-Feldern von Antwort
  und Query, die Projektionen dahinter aus den Lese-Fähigkeiten (`GeneratedQueryDienst`); Stand je gezeigter Id = Guard der Projektionen, dahinter
  das Log (`ProjektionsStaende`); Read-your-writes „mindestens S auf v“ wartet begrenzt darauf, sonst `noch_nicht`.
- **Direkte Abfrage** (docs/03 §3.2): eine Query hat genau ein Ziel (R4, CQRS084/086) — einen Reader oder ein Aggregat. `Frage(Query)` im
  Decider (nur lesend, synchron; die Query nennt ihr Aggregat mit genau einem Glied `Id<TAggregat>`, CQRS057; Query und Antwort liegen bei der
  Schreibseite); der Query-Dienst schickt sie über `AggregatAbfrage` an den Aggregat-Actor, der im Turn auf dem committeten Stand antwortet (während
  eines Commits wartet sie bis danach); die Antwort zeigt das Aggregat mit seiner Version als OCC-Stand.
- **Store-Klassen** (docs/04 §4.3): ein Store implementiert genau eine — `IAtomaresZiel` (1: Wirkungen und Fortschritt in einer Transaktion,
  `MartenStore`), `IWiederholungsfestesZiel` (2: Fortschritt beim Framework nach den Wirkungen, Schreib-Fähigkeiten nehmen die `Herkunft`,
  `DateiZiel`), `INurMindestensEinmal` (3: nur mit `[MindestensEinmal]`, `DateiSink`); CQRS109/110; jeder Treiber besteht den allgemeinen
  Store-Test (`Storetest`); Neuaufbau über Generationen (`NeueGenerationAsync`/`UmschaltenAsync`, Klasse 1 = Leeren).
- **Clients** (docs/konzept-akteure.md §5): Sitzung als Actor je Verbindung (`SitzungsActor`, einziger Schreiber am gRPC-Stream, Id `<Knoten>#<Nr>`);
  **Verteiler** je Knoten (`VerteilerActor`, fester Name, Typ → Sitzungen, Rückstau mit Grenze) für Pushes, gezielte Antworten (Ablehnung,
  `CommandFailed`, ohne Abo) und Hinweise an die Akteure, die sie hören; nach jedem Projektions-Commit „P hat S weiterverarbeitet“ → die Sitzung
  nennt dem Client die betroffenen Queries (`Aktualisiert`), er fragt neu; Lesen ab Position (`LiesLog`, nur Hörbares, nur bis zum Horizont);
  Ausgangskorb im C#-Client. Kein Broker, keine Abos.
- **Aktivierung eines Knotens** (docs/04 §4.4): Umsetzung und Technik je Kind (DI-Bereich, geschlossen beim Stoppen; Technik lädt beim ersten
  Aufruf, Freigabe über `IAsyncDisposable`); Konfig-Werte und Technik-Wahl aus dem Knoten der Instanz (`GeneratedTechnik`); Zustand nur über
  `IZustand<T>` (CQRS105), eine Instanz mit Zustand oder Quelle läuft im Cluster auf ihrem **Rechner** (`Rechner` in der Instanz-Datei = `Cluster:Rechner` des Hosts;
  Pflicht, keine Sperre, kein Umzug bei Ausfall; doppelte Rechnernamen erkennt der Gossip).
- **Abläufe** sind Graphen (eine Rechnung je Auslöser: Folge, Gabel, Wahl, Je) plus ein Vorgangs-Aggregat für alles, was zusammengeführt oder
  abgewartet wird (Zählung, Kompensation, Frist im Decide); die Zugehörigkeit ist ein fachliches Feld (A2). Aggregate reden nur über Graphen oder
  Akteure miteinander. Kein Dirigent, keine Saga, kein Builder.
- **Uhr:** `Frist<TCmd>` aus dem Decide → `FristGesetzt` (`IRahmenEvent`) + Beilage `cqrs_fristen` → die Uhr sendet fällig mit Id aus dem Frist-Event.
- **Emit:** genau ein Weg (`CommandEmitter`, Id vom Aufrufer abgeleitet), erzwungen durch CQRS020/021.
- **Akteure und Clients:** ein Akteur (`record X : IAkteur`) sagt, was er hineingibt (`IDarf<T>`: Commands, Queries, Hinweise) und was zu ihm
  kommt (`IHoert<T>`: ein Command ohne Ziel ist Arbeit, ein Event oder Hinweis Kenntnis); alles, was hineinkommt, kommt von einem Akteur. Ein Client
  ist ein Record `X : IClient, IVerkoerpert<A>…` und nennt nur, wen er verkörpert; wirksam = verkörperte Akteure ∩ Token, für beide Richtungen.

## Wo was liegt

| Bereich | Ort |
|---|---|
| Verträge, Marker, geteilte Regeln (eine Quelle) | `Abstractions/` (z. B. `Akteur.cs`, `Funktion.cs`, `Graph.cs`, `Katalog.cs`, `Zustand.cs`, `Funktionsvertrag.cs`, `Projektstruktur.cs`, `Spur/WirkerId.cs`, `Spur/SpurWeg.cs`, `StoreZugriff.cs`, `Umgebung/UmgebungKonfig.cs`) |
| Laufzeit (Framework) | `Infrastructure/` (Projections, Wecken, Verteilen, Funktionen — Graph-Actor, Parent, Kette, Werkstatt, Arbeit —, Eingaenge, Uhr, Umgebung, Einblick, Eingang, Akteure); referenziert keine Domäne, liest sie über `Komposition/Anwendung.cs` (`IAnwendung`) |
| Katalog der Quellfunktionen, Store-Treiber | `Core/Katalog/` (`Dateiwaechter`, `Taktgeber`, `Webhook`), `Core/` (`MartenStore`, `DateiZiel`) |
| Leitung | `Cqrs.Leitung/cqrs.proto` (Framework: Dienst, Hüllen, `Nutzlast`); je Anwendung `<Wurzel>.Leitung` (Domänen-DTOs + `GeneratedNutzlast`) |
| Komposition | `<Wurzel>.Laufzeit` (hier `Domain.Laufzeit`): Laufzeit-Generate, `GeneratedAnwendung`, `AddCqrs(args)` der Hosts |
| Generatoren und Analyzer | `*.SourceGeneration/`; Codegen vor dem Build (Proto, STJ-Kontexte, Python-Basen): `Cqrs.Codegen` über das Target in `Domain.Leitung` |
| Lesen, Sicht, Schreiben, Prüfen | `GraphExtractor.Kern/` (`Lesen`, `Sicht/`, `Schreiben/`, `Sonde/`, `ParitaetsPruefung`), CLI `GraphExtractor/` |
| Grammatik, Scaffolder | `DomainEditor/` |
| Werkbank | `Werkbank/` (Server `Werkbank/Server/`, Oberfläche `Werkbank/wwwroot/editor/`) |
| Hosts | `Host.Grpc` (`AddCqrs(args)` der Komposition + `LaufeAsync`), `Host.Blazor` (Client) — beide mit `--umgebung <name>`; `Host.Pruefdomaene` (Messung S5) |
| Beispieldomäne (Prüfling) | `Domain*`, `Domain.Akteure`, `Domain.Client.*` |
| Prüfdomäne (zweite Anwendung, Framework-Tests) | `Pruefdomaene`, `Pruefdomaene.Projections`, `Pruefdomaene.Pipeline`, `Pruefdomaene.Akteure`, `Pruefdomaene.Laufzeit`, `Pruefdomaene.Integration.Tests` |
| Umgebungen | `umgebungen/<name>/umgebung.json` und `umgebungen/<name>/graphen/<instanz>.json` |
| Vermessung (E2E-Treiber, Ablage) | `Cqrs.Vermessung`, `scripts/vermessung-*.sh`, `vermessung/ergebnisse/` |

## Konventionen

- Kommentare und Domäne auf Deutsch. Neue Verträge → `Abstractions`; Marten/Infra → `Infrastructure`.
- **Nur Code-Fakten** (W2): neue Erkennung über Marker/Attribut/Symbol; neues Konstrukt ⇒ Sonde (`GraphExtractor.Kern/Sonde/`) + `soll.txt` erweitern.
- **Projektstruktur = eine Tabelle** (`Abstractions/Projektstruktur.cs`, docs/07): Rollen als Suffix zur Wurzel, Primitive → Projekt und Datei;
  nie Projektnamen tippen, sondern die Tabelle lesen. Falscher Ort = CQRS098 bzw. `--check` „Lage“; neue Primitive ⇒ `Lageart.cs` + Tabelle.
  Die Sonde muss alle allgemeinen Primitive abdecken, nicht nur die der Beispieldomäne.
- **Neue Dispatch-Logik = Generator erweitern**, nie Handschalter, nie Reflection.
- **Typisierte Ids:** ein Command an ein Aggregat ist `record … : ICommand<TAggregat>` mit `Id<TAggregat> AggregateId` (das Ziel steht im Typ;
  Framework-Sicht `IAggregatCommand.Ziel`, CQRS013/CQRS010), ein Command ohne Ziel `record … : ICommand` (Empfänger aus dem Graphen bzw. dem
  `IHoert` eines Akteurs, CQRS116); Command-Ids leitet das Framework ab, nie der Entwickler; Verweise auf Aggregate in Events, Queries, Antworten sind `Id<T>` (UUID auf Leitung, Log, Wire; Namensregel
  `Abstractions/Idvertrag.cs`), auch in Read-Models und Store-Fähigkeiten (ein Read-Model mit `Id<T> Id` ist `partial`, der Generator ergänzt
  die Speicher-Identität `Schluessel`, CQRS111).
- **Ausgabe-Vertrag in der Signatur (CQRS050):** Decide, Auf und Frage geben einen konkreten Typ oder `OneOf<…>` konkreter Typen zurück.
  WAS entstehen kann, lesen Generatoren/Extractor/Editor NUR aus der Signatur; Rümpfe liefern nichts.
- **Fähigkeiten statt Rumpf-Analyse:** ein Auf bzw. eine Frage bekommt die Store-Funktionen als Parameter (`: IWriteStore`/`: IReadStore`, genau eine Funktion,
  CQRS051; Bündel `: IStore`). Nie Store/`IFristplan` in Ctor/Feld (CQRS054), nie `new …Store()` (CQRS055).
- **Grammatik = eine Quelle** (`DomainEditor/Grammatik.cs`): neue Regel ⇒ Analyzer + Test + Gegenstück in der Grammatik.
- **Akteur und Client** nicht vermischen (`docs/konzept-akteure.md`): Akteur = `IDarf` für das, was er selbst hineingibt, und `IHoert` für das,
  was zu ihm kommt (Arbeit oder Kenntnis), in `Akteure.cs`; Client = Record in `Clients.cs`, der nur nennt, wen er verkörpert (`IVerkoerpert<A>`) —
  über seine Leitung gehen genau `IDarf` und `IHoert` seiner Akteure, kein eigenes Wort dafür; wirksam = verkörperte Akteure ∩ Token, C#-Client
  per `[assembly: IstClient<Arbeitsplatz>]`.
- **Graph statt Klebecode:** die Kanten stehen als `[Von(nameof(…))]` am nehmenden Knoten (kein Builder, keine Liste, keine Generics als
  Verdrahtung); passt ein Typ nicht, ist der Adapter eine eigene kleine Funktion. Technik ist, was der Konstruktor einer Funktion nimmt und weder Konfig-Record noch `ILogger<T>` noch `IZustand<T>` ist
  (CQRS089–092), ohne Marker.
- **Kein `InMemoryEventStore`:** Store-Semantik nur gegen echtes Marten (Integration); der Prüfstand testet nur store-freie Logik.
- **Actors:** jeder Framework-Actor erbt `RahmenActor` (Bereich, Stopp, Passivierung, Fehlerantwort, Zusammenfassen); Cluster-Nachrichten nur
  über den `Bote` — `Melde` (verlierbar) oder `FrageAsync` (Pflicht-Frist), nie `Cluster.RequestAsync` direkt (`Infrastructure/Lebenszyklus/`).
  Hausregel als Build-Prüfung (R18, CQRS112/113, `Hausregel.SourceGeneration`): in Actors und dem, was sie besitzen, kein `Task.Run`, `lock`,
  `_ = …Async`, `ContinueWith`, `.Result` (außer nach `{ IsCompletedSuccessfully: true }`), keine rohe Cluster-API; lange I/O über `Neben`;
  Ausnahme nur mit `[HausregelAusnahme("Begründung")]`.
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
- **Test-Stufen auf dem Server:** `scripts/server.sh lauf scripts/tests.sh schnell` je Teilschritt (≈ 7 min: Prüfstand, Integration ohne
  Kategorie `Last`, Prüfdomäne, Verhaltens-Netz), `… tests.sh voll` zur Abnahme (dazu `Last`, Codegen-Drift, Client, Python, `--check`, `--sonde`,
  `--grammatik`, Gate), `… tests.sh suite <Projekt> [Filter]` gezielt; Dauer je Suite und rote Tests mit Meldung. Tests, die auf den Takt warten,
  laufen mit kurzem Takt (Prüfdomäne 2 s); Latenz-Tests des Weckers behalten 30 s, sonst bestünden sie auch ohne Wecker.
- **Gesamtprüfung** (Meilensteine, ≈ 65 min, nur nach Frage): `scripts/server.sh lauf scripts/gesamtpruefung.sh` — volle Stufe (rote Tests
  wiederholt: rot oder wackelt), Messung S1 und S3–S6 (S6 = Clients über gRPC), Störungslauf, Sonde mit CPU-Profilen; Bericht `gesamtpruefung.md` gegen
  den Vergleich mit Rausch-Bändern, auffällige Szenarien A/B nachgemessen (docs/12 §12.6).
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
  Host in der gewählten Umgebung; die Fläche in sechs Spalten, Graph, Aggregat und Store von innen (docs/10 §10.9).
- Hosts: `dotnet run --project Host.Grpc -- --umgebung sim-main` bzw. `Host.Blazor -- --umgebung sim-main` (Sim-DB `cqrs_sim_<branch>` über `CQRS_BRANCH`).
- Multi-Node (3 Nodes + Consul): `docker compose -f deploy-multinode/docker-compose.yml up -d --build` (`docs/06-transport-multinode-betrieb.md`).
- Gate „frischer Checkout baut“ (vor jedem Push): `./pruefe-checkout.sh` (Worktree des Commits: ignorierte Quellen, Build, Drift, Prüfstand)
- Editor-Parität (Code ⇄ Lesen ⇄ Karten, schreibt nichts): `dotnet run --project GraphExtractor -- --check`
- Agnostik-Sonde (fremde Domäne + Gesten-Skript + Fixpunkt): `dotnet run --project GraphExtractor -- --sonde`
- Grammatik als Liste „Regel → Build-Gegenstück“: `dotnet run --project GraphExtractor -- --grammatik`
- Ein Graph als Text (Knoten, Kanten mit Vielfachheit, Köpfe, Signatur): `dotnet run --project GraphExtractor -- --graph <Typ>`
- Warmer Workspace messen: `dotnet run --project GraphExtractor -- --warm`
- LLM-Kontexte: `dotnet run --project GraphExtractor -- --kontexte <verz>` bzw. `--kontext <Disc>`, Slots: `--slots`

## Wo steht was

| Frage | Dokument |
|---|---|
| Wie schreibe ich X? Grammatik, Regeln, Entscheidungen | `docs/10-entwickler-api.md` |
| Was ist heute garantiert (Wirkung, Ordnung, Zustellung)? | `docs/11-garantien.md` |
| Wie funktioniert es heute? | `docs/03` (Schreibseite), `docs/04` (Konsum, Funktionen und Graphen, Quellen, Uhr), `docs/05` (Generatoren), `docs/06` (Transport) |
| Akteure, Clients, Arbeit | `docs/konzept-akteure.md`, `docs/08` (Blazor), `docs/09` (Python) |
| Offen, Schulden | `docs/13-reifegrad-schulden-bewertung.md` §13.0 |
| Tests, Zahlen, Messwerte | `docs/12-tests-und-vermessung.md` |

Gelöschte Dokumente (Werkbank-Konzept, Editor- und LLM-Konzepte, die Pläne Pub/Sub-Neustrukturierung, Commands und Funktionsgraphen, Ausdrückliche Kanten in Graphen, Graphen ohne Lauf-Steuerung und Eine Instanz läuft auf ihrem Rechner) liegen im Git-Verlauf.
