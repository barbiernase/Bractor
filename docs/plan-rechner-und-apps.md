# Plan: Rechner und Apps — Konzept und Instanz auf der Client-Seite

Stand 2026-11-13, erste Fassung nach dem Gespräch mit Tobi. Maßstab für den Umbau; umgesetzt wird Schritt für Schritt, jeder Schritt auf Tobis
Wort (E5). Baut auf dem Plan „Clients und Quellen“ auf (`docs/plan-clients-und-quellen.md`, Schritte 1–5 umgesetzt) und löst dessen Ort der
Eingänge ab. Nach der Umsetzung steht der Inhalt in den Kapiteln, und beide Pläne gehen in den Git-Verlauf.

## Worum es geht

Heute ist ein Client zugleich Konzept und Instanz. `record Linienrechner : IClient` ist der Begriff. In der Umgebung steht er unter seinem Typnamen
genau einmal (`Clients:Linienrechner`), mit einem Token, einer `Urls` und Eingängen, von denen jeder seinen eigenen Rechner nennt. Zwei echte
Linien-PCs gibt es so nicht als zwei Dinge, und fünf Arbeitsplätze in der Halle auch nicht. Ein Rechner ist nur ein String an drei Stellen
(`Cluster:Rechner` des Hosts, Instanz, Eingang). Der Dateiwächter läuft im Server-Host, der zufällig so heißt: Ein Linien-PC müsste dem Cluster
beitreten, um sein eigenes Verzeichnis zu beobachten.

Künftig gilt eine einzige Art, über Orte zu reden, links wie rechts: **Es gibt einen Pool von Rechnern, und alles, was einen Ort hat, wird einem
Rechner daraus zugewiesen.** Aggregate und Projektionen verteilen sich selbst über die Rechner des Backends. Apps, Quellen und Graph-Instanzen mit
Zustand oder GPU stehen dort, wo man sie hinstellt.

## Das Modell

Von links nach rechts auf der Fläche:

1. **Rechner (Instanz, Konfig).** Ein Rechner des Pools mit Adresse (IP oder Hostname). Auf ihm stehen **App-Instanzen**, beliebig viele, jede auf
   ihrem Port. Für eine App-Instanz steht dort, welche ihrer Nachrichten die App selbst sendet (Standard) und welche eine **Quelle** samt Übersetzung
   erzeugt, etwa zwei Dateiwächter auf zwei Ordnern.
2. **Client (Konzept, Code), unverändert.** `record Linienrechner : IClient, IVerkoerpert<KameraSystem>` sagt nur, wen er verkörpert. Was
   hinausgeht, ist die Vereinigung der `IDarf` seiner Akteure.
3. **Akteur (Konzept, Code), unverändert.** Die Spalte bleibt ganz links bei den Graphen. Sie gruppiert nicht mehr nach Client, weil die Clients
   eine eigene Spalte bekommen. Ein Akteur steht damit einmal und nicht mehr nur unter dem ersten Client, der ihn verkörpert.

Dazwischen kommt ein Begriff dazu, die **App (Konzept, Code)**: eine konkrete Software, die einen Client umsetzt. Sie ist die Datei, gegen die
verbindlich entwickelt wird. Ihr Vertrag sind die Nachrichten des Clients ohne die, die eine ihrer Quellen erzeugt, dazu alles, was seine Akteure
hören.

```csharp
// Domain.Akteure/Apps.cs
public sealed record Kameraeingang(
    [Uebersetzt<DateiGefunden_LiesBild_1>] Dateiwaechter Oben,
    [Uebersetzt<DateiGefunden_LiesBild_1>] Dateiwaechter Unten) : IApp<Linienrechner>;   // nur Quellen: keine eigene App-Software nötig

public sealed record Hallenarbeitsplatz : IApp<Arbeitsplatz>;                              // alles über die App (Razor)
public sealed record Klassifikation : IApp<KlassifikationsWorker>;                         // alles über die App (Python)
```

Rechts gilt dasselbe Bedienkonzept für das **Backend**: eine Karte mit den Rechnern, die das Backend bilden (zu Beginn nur `localhost`, editierbar).
Eine Graph-Instanz, die einen Ort braucht, bekommt einen dieser Rechner. Alles andere verteilt sich selbst.

```json
// umgebungen/prod/umgebung.json — der Pool und das Backend
"Rechner": { "server-1": { "Adresse": "10.0.1.5" }, "linie-1": { "Adresse": "10.0.4.21" }, "halle2-pc3": { "Adresse": "10.0.7.3" } },
"Backend": { "Rechner": [ "server-1" ] }

// umgebungen/prod/apps/linie-1-kamera.json — eine App-Instanz
{ "App": "Kameraeingang", "Rechner": "linie-1", "Token": "…",
  "Oben":  { "Pfad": "/mnt/k1", "Fertig": "Stabil", "Kennung": "NameGroesse" },
  "Unten": { "Pfad": "/mnt/k2", "Fertig": "Stabil", "Kennung": "NameGroesse" } }

// umgebungen/prod/apps/halle2-arbeitsplatz.json
{ "App": "Hallenarbeitsplatz", "Rechner": "halle2-pc3", "Port": 5010, "Token": "…", "Start": { "Projekt": "Host.Blazor" } }
```

Der Vergleich mit der rechten Seite: Eine App ist zu ihren App-Instanzen, was ein Graph zu seinen Graph-Instanzen ist. Die Struktur steht im Code, Ort
und Werte stehen in der Umgebung. Ein Graph läuft je Umgebung genau einmal, weil er Empfänger ist und Empfänger eindeutig sein müssen. Eine App darf
beliebig oft stehen, weil sie Absender ist.

## Entscheidungen

Als sichere Vorgaben gesetzt; Tobi kann jede zurücknehmen, bevor der Schritt beginnt, der sie braucht. Die mit (T) hat Tobi im Gespräch selbst gesagt.

1. **Ein Pool, ein Bedienkonzept (T).** `Rechner` in `umgebung.json`: Name → Adresse. Alles mit Ort nennt einen Rechner daraus: App-Instanzen und
   Graph-Instanzen. Es gibt keine Regeln, was zusammen auf einem Rechner stehen darf; nur ein Port gehört je Rechner einer App-Instanz.
2. **Das Backend ist eine Karte (T)**: `Backend:Rechner`, zu Beginn `[ "localhost" ]`. Die Cluster-Art folgt daraus: ein Rechner heißt
   Einzelknoten, mehrere heißen Consul (`Cluster` behält nur Name und Consul-Adresse). `AdvertisedHost` ist die Adresse des Rechners. Ein Client
   verbindet sich mit dem Backend über dessen Adressen und den gRPC-Port; `Adresse` und `Urls` tippt niemand mehr.
3. **Jeder Prozess kennt seinen Rechner** über `CQRS_RECHNER` bzw. `--rechner` (Standard `localhost`) und nimmt sich aus der Umgebung, was diesem
   Rechner zugewiesen ist. So machen es die Hosts heute schon mit `Cluster:Rechner`, nur konsequent für alles. Kein Deploy (W12): Der Editor verteilt
   in der Konfig, er installiert nichts; in sim liegt alles auf `localhost`, und die Werkbank startet es lokal.
4. **Die App ist Code**: `record X(…) : IApp<TClient>` in `<Wurzel>.Akteure/Apps.cs` (flach wie Akteure und Clients). Grund: Ihr Vertrag ist
   verbindlich und muss in sim und prod derselbe sein (W1, W7). Ort, Port, Token und Werte stehen in der App-Instanz.
5. **Vertrag je App (T).** Je Nachricht des Clients entweder die App oder Quellen, nie beides in derselben App. Eine Nachricht mit Quelle steht nicht
   im Vertrag der App: C# `[assembly: IstApp<X>]` statt `IstClient<X>`, die Python-Basis je App, Hash und Handshake-Name je App. Der Client bleibt der
   Begriff, den Akteure verkörpern (CQRS063 unverändert).
6. **Eine Quelle ist ein Parameter der App**, wie ein Knoten ein Parameter des Graphen ist: Typ = Quellart, Name = Schlüssel ihrer Werte in der
   App-Instanz, `[Uebersetzt<U>]` nennt die Übersetzung der Akteur-Seite ausdrücklich (mehrere = Gabel). Die Quelle von `U` ist eine Meldung der
   Quellart, das Ziel ein Command, den ein verkörperter Akteur darf; der Akteur folgt daraus (dieselbe Regel wie heute `AkteurFuer`). Die
   Übersetzungen bleiben Code in `<Wurzel>.Akteure/Uebersetzungen.cs`. Mehrere Ordner sind mehrere Parameter; eine Zahl, die je Ort schwankt, ist eine
   Liste in den Werten der Quellart.
7. **App-Instanzen sind Dateien** `apps/<instanz>.json` wie Graph-Instanzen: `App`, `Rechner`, wahlweise `Port`, `Token`, `Start` (Projekt bzw.
   Skript für die Werkbank), je Quelle ihre Werte und `Technik`. Der Name der Instanz geht in die Ids ein, der Rechner ist ein Feld. Der Editor
   zeigt die Instanzen nach Rechner gruppiert.
8. **Der Rechnerdienst (T, „ein Weg“).** Ein Prozess des Frameworks je Rechner (`Host.Rechner`) trägt alle Quellen der App-Instanzen dieses
   Rechners, später weitere Elemente des Frameworks. Er gibt **über die Leitung** hinein wie eine App, mit dem Token der App-Instanz, auch auf einem
   Backend-Rechner. Die Hineingabe im Server-Host entfällt.
9. **Ids und Garantie wie heute**: Kennung des Elements aus App-Instanz, Quelle und Kennung der Meldung; ein Command an ein Aggregat mit daraus
   abgeleiteter Command-Id (Inbox), ein Command ohne Ziel über den offenen Eingang am Kopf (die Sitzung legt ihn schon heute idempotent an).
10. **Das Framework sichert keinen Zustand einer Quelle.** Was eine Quellart sich merkt (was sie schon gesehen hat), löst sie selbst, je nach
    Bedarf — im Speicher, in einer eigenen Datei oder gar nicht. Der Rahmen gibt ihr dafür nichts vor; `IZustand<T>` steht nicht mehr in ihrem
    Konstruktor (bleibt für Funktionen im Backend). Genau einmal hängt nicht daran: Meldet eine Quelle ein Element erneut, trägt es dieselbe Kennung
    aus dem Inhalt, es entstehen dieselben Ids, und die Inbox verwirft das Zweite. Postgres trägt nur, was die Aggregate brauchen.
    Kein eigener **Gegendruck**: Der Läufer gibt ein Element hinein, wartet auf die Quittung und nimmt das nächste; was danach im Backend wartet,
    hält der offene Eingang am Kopf, gerechnet wird nach den Strängen der Instanz (eventual consistency). `Offen` entfällt.
11. **Der Webhook ist eine Quelle wie jede andere**: Eine App mit Webhook-Parameter steht auf einem Rechner mit Port, und der Rechnerdienst nimmt
    dort an (`POST /eingang/webhook/<Pfad>`), 202 erst, wenn es durabel ist. Der Annahme-Endpunkt im Host entfällt.
12. **Das Backend bekommt dieselbe Geste**: Eine Graph-Instanz nennt einen Backend-Rechner oder keinen (dann läuft sie auf allen). Die Pflicht
    „Zustand braucht im Cluster einen Rechner“ bleibt, geprüft gegen die Backend-Karte statt gegen den Gossip allein.
13. **Konzept im Modal, Instanz auf der Fläche (E4).** Welche Nachricht eine App über welche Quelle erzeugt, ist Code. Es entsteht im Modal
    „Akteure, Clients und Apps“, das ein Doppelklick auf eine App-Instanz öffnet. Platz, Port, Token und Werte setzt man auf der Fläche; Rechner und
    Backend auf ihren Karten.
14. **Tokens bleiben**, wirksam bleibt „verkörpert ∩ Token“, befugt ist der Akteur. Die App-Instanz nennt ihr Token; der Abschnitt
    `Identitaeten:Tokens` bleibt.

## Wie ein Element hineinkommt (Laufzeit)

Der Rechnerdienst liest die Umgebung, nimmt die App-Instanzen seines Rechners und startet je Quelle einen Läufer. Der Läufer aktiviert die Quellart
mit Konfig-Werten und Technik-Wahl aus der App-Instanz und ruft `Lausche`. Je Meldung:

1. Kennung = aus App-Instanz, Quelle und `Kennung` der Meldung.
2. Übersetzen mit der Übersetzung, die der Parameter nennt (generierte Tabelle, reflexionsfrei).
3. Hineingeben über die Leitung im Namen der App-Instanz, mit den Nachrichten, die es heute schon gibt: Command-Id aus Kennung, Übersetzung und
   Stelle; warten bis zur Quittung (wiederholen mit derselben Id bei unbekanntem Ausgang), dann die nächste Meldung.

Wirker der Spur ist die Quelle (`quelle:<instanz>/<parameter>`, W9), Ursache die Kennung. Endet `Lausche` oder wirft es, beginnt der Läufer nach einer
Pause neu; was die Quellart dann noch einmal meldet, verwirft das Backend. Die Annahme eines Webhooks ist derselbe Weg ab Schritt 1.

Die Leitung bleibt, wie sie ist. Was im Host entfällt: `Infrastructure/Quellen/` (Läufer, Hineingabe, Annahmen, Endpunkt, `Eingaenge.Aus`), der
Läufer unter der Wurzel `funktionen`, die Annahme-Route auf dem Einblick-Port und der Zustand von Eingängen in `cqrs_zustaende`.

**Was dabei zu wissen ist** (kein Mechanismus, nur die Folge): Die Inbox merkt sich eine Command-Id 30 Tage (`InboxHorizontTage`). Meldet ein
Dateiwächter nach einem Neustart eine ältere Datei erneut, wirkt sie noch einmal; ein Command ohne Ziel startet nach einer fertigen Kette eine neue
(deren Commands an Aggregate verwirft die Inbox). Wer das nicht will, löst es in seiner Quellart (eine eigene Gesehen-Datei, nur Dateien jünger als
x, verarbeitete Dateien verschieben).

## Schritte

Jeder Schritt endet mit `dotnet build`, `--check`, `--sonde`, `--grammatik` und dem Blick im Browser. Er zieht die Kapitel nach, die er ändert.
Laufzeit-Suiten laufen nur auf dem Server und nur nach Rückfrage (Fokus Editor und GUI). Commit nur auf Tobis Wort.

### Schritt 1 — Rechner und Backend

- `UmgebungKonfig`: `Rechner` (Name → Adresse), `Backend:Rechner`; `Cluster:Art` und `Cluster:Rechner` entfallen, die Art folgt aus der Zahl der
  Backend-Rechner; der Prozess-Rechner aus `CQRS_RECHNER`/`--rechner`. Lader und Boot-Prüfung: jeder genannte Rechner steht im Pool, eine
  Graph-Instanz nennt nur Backend-Rechner, der Host läuft nur, wenn sein Rechner zum Backend gehört.
- Abgeleitete Adressen: gRPC-Adresse eines Clients aus dem Backend; `ClientKonfig.Adresse` entfällt.
- Umgebungen: `sim-main`, `vermessung`, `vermessung-pruefdomaene` mit `localhost`; `prod` mit `prod-1`; `multinode` mit `grpc1…3` (docker-compose
  setzt `CQRS_RECHNER`).
- Werkbank: die Karte **Rechner** im Kopf der linken Spalte (＋ Rechner mit Name und Adresse, Adresse ändern, ✕ gesperrt, solange etwas dort
  steht) und die Karte **Backend** über den Spalten Graphen bis Projektionen (＋/✕ Rechner aus dem Pool). An der Graph-Instanz wählt ⊕ einen
  Backend-Rechner. Die Sicht rechnet Befunde (W3).

**Stand Schritt 1 (2026-11-13, lokal gebaut und im Browser geprüft, Tests geschrieben, nicht gelaufen):** umgesetzt wie oben.
- Eine Regel für Start und Werkbank: `Abstractions/Umgebung/Rechnerregeln.cs` (Befunde je Karte, Name, Rechner des Prozesses); der Host prüft mit
  `CqrsUmgebungExtensions.PruefeRechner` (der Migrator eines Kaltstarts ist kein Knoten und braucht keinen Rechner).
- `UmgebungKonfig`: `Rechner`, `Backend`, `DieserRechner` (nicht aus der Datei), abgeleitet `Einzelknoten`, `Adresse(r)`, `GrpcAdresse()`;
  `ClusterArt`, `Cluster:Art/Rechner/AdvertisedHost` und `ClientKonfig.Adresse` sind weg. Ohne Abschnitte gilt `localhost`.
- Ein Eingang aus einer Quelle nennt einen Rechner des Backends (sie lauscht bis Schritt 4 im Host).
- Die Sicht bekommt die Instanz-Dateien unter `Graphen` je Umgebung (`GraphExtractor.Kern/Umgebungen.cs`) und rechnet `flaeche.rechner`.
- Werkbank: Karten Rechner und Backend im Streifen unter den Spaltenköpfen, Gesten `/api/werkbank/rechner/…` und `/api/werkbank/backend/…`
  (`Werkbank/Server/Rechnerpool.cs`), Rechner einer Instanz als Wahl im Formular, Rechner am Chip der Instanz. Nebenbei behoben: der DOM-Helfer
  `h()` setzte `disabled:false` als Attribut (jeder so gemeinte Knopf war gesperrt).
- Folgen der Regel „ein Backend-Rechner = Einzelknoten“: `prod`, `vermessung` und `vermessung-pruefdomaene` liefen bisher als Consul mit einem Knoten
  und laufen jetzt als Einzelknoten (Messwerte von S1–S6 sind damit nicht mehr direkt mit den alten vergleichbar). Die Messumgebungen heißen ihren
  Rechner `localhost` statt `messrechner`.
- Offen: `build.sh` schreibt weiter eine veraltete `umgebung.linux.json` (war schon vorher ungültig); die Werkbank startet den Host ohne
  `--rechner` (in sim reicht `localhost`).

### Schritt 2 — Die App als Code, Vertrag je App

- `Abstractions`: `IApp<TClient>`, `UebersetztAttribute<TUebersetzung>`, `IstAppAttribute<TApp>` (statt `IstClient`). Lage: `Apps.cs` in
  `<Wurzel>.Akteure` (`Projektstruktur.cs`, `Lageart.cs`).
- Lesart `Appform` (eine Quelle für Generator, Analyzer, Extractor, Codegen): Client, Quellen (Parameter, Quellart, Übersetzungen, Ziel, Akteur),
  Vertrag (sendet, fragt, hört) und Hash.
- Analyzer mit Gegenstück in der Grammatik (neue Nummern ab CQRS125): Form der App (nur Parameter von Quellarten, jeder mit `[Uebersetzt]`);
  die Übersetzung passt zur Meldung der Quellart und zielt auf einen Command, den ein Akteur des Clients darf; eine Quellart steht nur als Parameter
  einer App; Namen von App, Client und Akteur sind zusammen eindeutig.
- Verträge: `GeneratedAppVertraege` (Server), Handshake mit dem App-Namen, `ClientVertragsPruefung` je App; C#-Client-Generator und CQRS093,
  095–097 gegen den Vertrag der App; `PythonVertragsEmitter` mit `<App>Basis`.
- Beispieldomäne und Prüfdomäne: je heutigem Client eine App (`Hallenarbeitsplatz`, `Klassifikation`, `Training`, `Reaktionen`, `Kameraeingang`);
  `Domain.Client.Modules.Blazor`, die Python-Worker und die .NET-Worker nennen ihre App.
- Sonde und `soll.txt`: eine App mit eigener Quellart und eine App ohne Quellen.
- Werkbank: das Modal heißt „Akteure, Clients und Apps“; eine App von innen zeigt die Nachrichten ihres Clients als Zeilen, je Zeile „App“ oder
  ihre Quellen mit dem Übersetzungsknoten; „＋ Quelle“ an einer Zeile wählt eine Quellart und eine passende Übersetzung (oder legt sie an, wie heute
  „＋ Verwirklichung“) und schreibt den Parameter; ✕ nimmt ihn heraus. „＋ App“ an einem Client. Die „Verwirklichungen“ des Akteurs gehen darin auf.

**Stand Schritt 2 (2026-11-13, lokal gebaut, `--check`, `--sonde`, `--grammatik` grün, im Browser geprüft, Tests geschrieben, nicht gelaufen):**
umgesetzt wie oben, mit diesen Abweichungen:
- Die Quellen einer App sind Parameter mit `[Uebersetzt<U>]`; die Quellarten des Frameworks sieht `Domain.Akteure` schon heute über
  `Bildvorschau.Katalog → Core` (kein neuer Bezug — dass damit Marten im Blazor-Client steckt, bleibt ein Thema von Schritt 4).
- Server: `GeneratedAppVertraege` neben `GeneratedClientVertraege` (dieselbe Form `ClientVertrag`, dazu `Client`); der Handshake prüft gegen die Apps.
  Das Tor bleibt beim Akteur: dass eine App die Nachricht ihrer Quelle nicht selbst sendet, hält der Build (CQRS095) bzw. die Python-Basis (`SPONTAN`).
- `IstClientAttribute` ist weg; `GeneratedWiring` nennt `Vertrag` (die App) und `Client` (den Schlüssel der Umgebung, bis Schritt 3).
- Python: `<App>Basis` mit `APP` (Handshake) und `CLIENT`; die Worker erben `KlassifikationsAppBasis` bzw. `TrainingsAppBasis`.
- Editor: Karte `app` in der Sicht (`ap:…`), Modal „Akteure, Clients und Apps“ mit „＋ App“ am Client und einer schlichten App-Ansicht (je Command
  App oder Quellen, „＋ Quelle“, ✕) — noch nicht in Bändern wie Aggregat und Store (`innenZeige`); eine neue Übersetzung entsteht weiter über
  „＋ Verwirklichung“ am Akteur.
- Sonde: `Ablagerechner` mit `AblageApp` (Dateiwächter und Webhook des Frameworks als Quellen) und `KatalogpflegeApp` ohne Quellen.

### Schritt 3 — App-Instanzen in der Umgebung

- `apps/<instanz>.json` (`AppInstanzKonfig`); der Abschnitt `Clients` entfällt samt `ClientKonfig`, `EingangKonfig`, `WorkerKonfig`. Lader und
  Prüfungen: Rechner im Pool, Port je Rechner eindeutig, Werte gehören zu den Quellen der App, Token nennt einen Akteur ihres Clients (schließt eine
  Lücke von AK-1).
- Hosts lesen ihre Instanz: `Host.Blazor --umgebung <u> --app <instanz>` (Urls = Rechner + Port), Python und .NET-Worker über `CQRS_APP`.
- Quellen laufen in diesem Schritt noch im Server-Host, aber schon aus den App-Instanzen (Ort = Instanz und Parameter) und nur, wenn ihr Rechner zum
  Backend gehört; sonst ein Befund „läuft ab Schritt 4“. So bleibt `sim-main` durchspielbar.
- Werkbank, Fläche: links die Spalte **Rechner** (je Rechner eine Karte, darin seine App-Instanzen mit Port und je Nachricht „App“ bzw. die Quellen
  mit Werten und Übersetzungsknoten), daneben die Spalte **Clients**, dann die Akteure ohne Gruppen; Kanten Instanz → Client → Akteur. „＋ App“ an
  einer Rechner-Karte (App wählen, Name, Port), Doppelklick auf eine Instanz öffnet ihr Formular (Port, Token, Start, Werte je Quelle), auf ihren
  Kopf die App im Modal. Durchspielen startet die App-Instanzen auf `localhost` statt der heutigen Worker; „Datei ablegen“ je Quelle.

**Stand Schritt 3 (2026-11-13, lokal gebaut, `--check`, `--sonde`, `--grammatik` grün, im Browser geprüft, Tests angepasst, nicht gelaufen):**
- `AppInstanzKonfig` (App, Rechner — ohne Angabe `localhost` —, Port, Token, `Start`, Dateien, je Quelle `QuelleKonfig` mit Offen, Technik, Werten),
  Dateien unter `apps/`; `ClientKonfig`, `EingangKonfig`, `WorkerKonfig` und der Abschnitt `Clients` sind weg. `Rechnerregeln.Befunde` prüft dazu
  Rechner im Pool und Port je Rechner (nicht der des Hosts auf einem Backend-Rechner).
- Generat: `GeneratedAppVertraege` trägt je App ihre Quellen (`AppQuelle`: Parameter, Quellart, Übersetzungen). `Eingaenge.Aus` macht daraus je
  Instanz und Quelle einen Eingang (Ort `<Instanz>/<Quelle>`, Wirker `eingang:<Instanz>/<Quelle>`), prüft App, Werte, Token (es nennt einen Akteur
  des Clients) und wählt den Akteur, der darf, was die Übersetzung gibt. Quellen laufen weiter im Host — nur Instanzen auf einem Rechner des Backends;
  die übrigen nennt der Start „außerhalb des Backends“.
- Host.Blazor: `--app <instanz>` bzw. `CQRS_APP` (ohne: die einzige Instanz seiner App, `AppInstanzwahl`), hört unter Adresse und Port der Instanz.
  Die Werkbank startet App-Instanzen mit `Start` (Reiter „▶ Apps“) und setzt `CQRS_UMGEBUNG`, `CQRS_APP`, `CQRS_RECHNER`, Token und gRPC-Adresse.
- Werkbank: Fläche mit der Spalte Rechner (je Rechner eine Karte mit Adresse, Backend, ✎, ✕, „＋ App“ und seinen Instanzen samt Quellen), daneben
  Clients (Apps, Reaktionen), die Akteure ohne Gruppen; Kanten Rechner → Client (`app`), Client → Akteur (`verkoerpert`), Quelle → Akteur
  (`eingang`). Formular je Instanz (Rechner, Port, Token, Dateien, Start, je Quelle aus der Quellart), im Host-Rahmen je Instanz eine Karte. Die
  Pool-Karte über den Clients ist in die Spalte aufgegangen; die Reaktionen stehen als „↻ reagiert“ an der Client-Karte statt als Bogen.
- Umgebungen: `sim-main` (arbeitsplatz mit Start Host.Blazor, klassifikation, training, reaktionen, linie1), `prod`, `multinode`, `vermessung`;
  `start-server.sh` setzt `Apps__…`. `build.sh` schreibt weiter das alte Format (wie schon vor Schritt 1).

### Schritt 4 — Der Rechnerdienst

- `Host.Rechner` (Komposition über `AddCqrsRechner(args)`), Läufer und Annahme auf der Client-Seite (`Client.Infrastructure/Quellen/`), aufgebaut
  auf Verbindung und Ausgangskorb (Ids ableiten, auf die Quittung warten).
- Quellarten des Frameworks ohne `IZustand`: der Dateiwächter merkt sich, was er gesehen hat, im Speicher und meldet nach einem Neustart alles im
  Ordner erneut; der Taktgeber meldet ab dem laufenden Intervall. Braucht eine eigene Quellart mehr, löst sie es selbst.
- Generat für den Rechnerdienst: Quellarten-Bausteine, Übersetzungen der Akteur-Seite, Quellen je App — ohne Marten und Proto.Actor, soweit möglich
  (sonst referenziert er die Laufzeit-Komposition; siehe Risiken).
- Entfernt: `Infrastructure/Quellen/`, der Läufer unter `funktionen`, die Annahme-Route im Host.
- Werkbank startet je Rechner mit Quellen den Rechnerdienst; Verhaltens-Netz (`Hostlauf`) und Multinode ebenso.
- Tests: der Garantie-Test „Kennung aus dem Inhalt“ über die Leitung (Absturz, andere Reihenfolge, nichts doppelt, nichts fehlt), dazu „Neustart:
  alles neu gemeldet, nichts wirkt doppelt“; Webhook 202 erst nach durabel; zwei Instanzen derselben App auf zwei Rechnern.

**Stand Schritt 4 (2026-11-13, lokal gebaut, `--check`, `--sonde`, `--grammatik` grün; Rechnerdienst allein gestartet; Tests angepasst, nicht gelaufen):**
- Quellarten des Frameworks ohne `IZustand`: Dateiwächter und Taktgeber merken sich im Speicher, was sie gemeldet haben, und melden nach einem Neustart
  erneut (dieselbe Kennung). CQRS100 verbietet `IZustand<T>` an einer Quellart; CQRS105 (veränderliche Felder) gilt für sie nicht. `Offen` ist weg.
- `Client.Infrastructure/Quellen/`: `Abgabe` (ein Command mit Id hinein, warten auf die Antwort mit seiner Korrelation, sonst noch einmal mit derselben
  Id), `Hineingabe` (Ids aus der Kennung, Ursache das Element), `QuellLaeufer`, `Annahmen` (Pfad und Port), `Quellenlauf` (je Instanz eine Verbindung
  mit ihrem Token, Handshake mit dem Vertrag ihrer App, neu verbinden nach einer Unterbrechung) und `Rechnerdienst` (Prozess: Umgebung, Instanzen des
  Rechners, `BindeTechnik`, Annahme auf den Ports, `/health`).
- `Host.Rechner` referenziert die Komposition (`Domain.Laufzeit`) und `Client.Infrastructure` — „erst schwer“: Marten und Proto.Actor liegen mit im
  Paket, laufen aber nicht. Ein eigenes, schlankes Generat bleibt offen.
- Im Host entfallen Läufer, Hineingabe, Annahme-Route und der Zustand von Eingängen; `Eingaenge.Aus` bleibt als geprüfter Plan (der Host prüft die
  App-Instanzen beim Start, `PruefeApps`), ohne Filter auf das Backend.
- Wirker in der Spur ist der Akteur (die Leitung bleibt, wie sie ist), Ursache das Element — nicht `quelle:<instanz>/<parameter>` wie im Plan.
- Werkbank: ⟳ baut `Host.Rechner` mit und startet je Rechner dieser Maschine mit App-Instanzen den Rechnerdienst (Zeile im Reiter „▶ Apps“).
  Verhaltens-Netz: `Hostlauf` startet ihn als Prozess neben dem Host auf `rechner-1`. Multinode: Container `rechnerdienst-grpc1`. Vermessung: neben dem
  Host, `vermessung-e2e.sh` baut ihn.
- Tests: `RechnerdienstTests` (Leitung im Speicher) statt `QuellLaeuferTests`, `AnnahmeTests` neu für die Client-Seite, `AblaufE2ETests` mit dem
  Quellenlauf in-process gegen den gRPC-Host (Neustart: erneut gemeldet, wirkt einmal).

### Schritt 5 — Abschluss

Kapitel: docs/04 §4.4 und §4.7, 06 §6.5, 07, 08, 09, 10 (§10.3.5, §10.3.6, §10.9, E4, E14–E16, neue Entscheidung zu Rechner und App), 11 (Quellen),
konzept-akteure §4 und §5, CLAUDE.md — dort auch Invariante 6 neu gefasst: was eine Quelle meldet, steht nicht im Log, und das Framework sichert
nichts für sie; genau einmal kommt aus der Kennung aus dem Inhalt. `scripts/tests.sh voll` und Messung S1 (Bildeingang über den Dateiwächter) nach Rückfrage. Die Pläne gehen in
den Git-Verlauf.

## Risiken

- **Umfang von Schritt 4**: Die Quellen-Laufzeit zieht vom Host auf die Client-Seite; rund 15 Dateien in `Infrastructure`, dazu Tests, Werkbank,
  Verhaltens-Netz und Multinode.
- **Leistung**: Jede Datei geht künftig über die Leitung statt in-process. S1 zeigt, ob das zählt; sonst bündelt der Läufer mehrere Elemente je Seite.
- **Abhängigkeiten des Rechnerdiensts**: Liegen die generierten Tabellen nur in der Laufzeit-Komposition, schleppt ein Linien-PC Marten und
  Proto.Actor mit. Ziel ist ein eigenes, schlankes Generat; zur Not erst schwer, dann schlank.
- **Vertrag je App**: Clients ohne App-Record (rohe `message_types`) bleiben möglich; die Prüfung am Handshake ändert den Namen, nicht das Tor.
- **Tests auf dem Server**: Schritte 1, 3 und 4 brauchen Laufzeit-Suiten; je Schritt frage ich vorher.
