# Die Werkbank

> **Kanonisches Dokument** für Werkbank, Editor und den Weg „Entwurf → Code → Lauf → Spur“. Es beschreibt den Stand des Codes
> (Gegenwart) und die Leitsätze, an denen jede Änderung gemessen wird. Wo andere Dokumente widersprechen, gilt dieses; für Fakten gilt der Code.
> Entstanden als Zielbild 2026-10-12, umgesetzt in den Phasen P0–P8 bis 2026-10-13 (Historie im Git-Verlauf, gelöschtes `docs/plan-werkbank-umsetzung.md`),
> neu gefasst 2026-10-13. Abschnittsnummern sind stabil, weil Code-Kommentare darauf verweisen.
>
> Offenes und Schulden stehen in `docs/13-reifegrad-schulden-bewertung.md` §13.0, Zahlen in `docs/12-tests-und-vermessung.md` §12.2.
> Detail-Referenzen: `konzept-editor-pipelines.md` §14 (Fluss), `konzept-akteure.md` (Akteure, Verträge, Clients),
> `konzept-llm-minimalkontext.md` (LLM-Kontext), `konzept-editor-komposition.md` (Sprache).

---

## 0 · Kurzfassung

Die Werkbank ist **eine** Oberfläche, auf der eine Anwendung entworfen, als Code erzeugt, gebaut, gestartet, durchgespielt und nachvollzogen wird.

- **Der Code ist das Modell.** Jede Geste im Editor schreibt sofort Code. Das Board wird immer aus dem Code gelesen — über Roslyn, nur aus
  Code-Fakten, ohne eingebaute Domäne. Was nicht baut, ist eine Karte mit dem Compiler-Fehler.
- **Alles läuft im Backend.** Der Werkbank-Server hält den Code in einem warmen Roslyn-Workspace, rechnet alle Ableitungen und schickt sie fertig
  an den Browser. Der Browser zeichnet nur.
- **Durchspielen heißt: das echte System laufen lassen.** Der echte Host läuft in der Umgebung `sim` gegen eine eigene, echte Datenbank. Sim und
  Prod unterscheiden sich nur in der Konfiguration, nie im Code.
- **Der Weg jedes Events steht im Log** (Spur: `ursache`, `wirker`, `ueber`, `stand`). Der Editor legt ihn über das Board.
- **Quellen sind fest codierte Treiber** (heute: Datei-Treiber). Die Domäne benennt eine Quelle nur als Typ; alles andere ist Konfig.
- **Das Backend** (Event-Store, Redis, Cluster, Funktions-Laufort, Dienste, Identitäten …) wird je Umgebung eingestellt.
- **Umfang:** Der Editor baut alles, was eine Anwendung ausmacht, außer GUIs (Clients werden als Vertrag vorbereitet) und Schema-Evolution.

---

## 1 · Umfang und Grenzen

| Bereich | im Editor |
|---|---|
| Domäne: Aggregate, Commands, Events, Ablehnungen, Wertobjekte, Enums, Decide/Apply | **bauen** |
| Abläufe: Prozesse, Flüsse, Katalog-Funktionen (C#- und Python-Rümpfe), Domänen-Dienste mit Konfig-Records | **bauen** |
| Leseseite: Projektionen, Reaktionen, Stores (Fähigkeiten, Bündel, Implementierung), ReadModels, Reader, Queries/Antworten | **bauen** |
| Akteure, Akteur-Verträge (Zusagen, Kenntnisse, Python-Rümpfe), Client-Verträge | **bauen** |
| Quellen: Domänen-Typ + Treiber-Konfig (inkl. Webhook) | **bauen** |
| Domänen-Hilfscode, der keine Primitive ist | **bauen** als Code-Karte (§9) |
| Backend und Umgebungen: Event-Store, Redis, Cluster, Ports, Identitäten, Laufort, Dienste, Konfig, Clients, Uhr | **konfigurieren** |
| Bauen, Starten, Durchspielen, Spur | **ja** |
| Clients | **nur vorbereiten:** Client-Vertrag (`IClientVertrag`, `ISendet`/`IFragt`/`Auf`) und die daraus generierten Schnittstellen |
| GUIs (Blazor-Views, Intents, Client-Stores) | **nein** |
| Schema-Evolution | **nein im Editor.** `IUpcast` bleibt als Backend-Primitive für Prod (Entscheidung 2026-10-13); in Sim wird bei inkompatiblen Daten die Sim-DB zurückgesetzt (§4.6) |
| Framework: Generatoren, Analyzer, `Infrastructure`, Treiber | **nein:** wählen und konfigurieren, nicht schreiben |
| Deploy nach Prod | **nein**. Prod ist im Editor nur lesbar. |

---

## 2 · Leitsätze (bindend)

| # | Leitsatz | Bedeutung |
|---|---|---|
| **W1** | **Code ist die einzige Wahrheit.** | Kein gespeichertes Board-Modell; gespeichert wird nur die Ansicht (`.werkbank/ansicht.json`). Unfertiges ist Code, der nicht baut. Jeder Typ der Domänen-Projekte gehört zu einer Karte (§9). |
| **W2** | **Der Graph kommt aus dem Code, über Roslyn, nur aus Code-Fakten.** | WAS etwas ist, sagen Marker-Interfaces, Attribute, Symbole, Signaturen — nie Namen oder Ordner. Keine Domänen-Namen in Framework und Werkzeugen. WO etwas liegt, legt die feste Projekt- und Ordnerstruktur fest (`docs/07-projektstruktur.md`): eine Framework-Konvention, an einer Stelle definiert und geprüft, die Werkzeuge kennen dürfen. |
| **W3** | **Alles läuft im Backend.** | Ableitungen (Karten, Kanten, Akteur-Mengen, Zugehörigkeit, Kette, „was passt an diesen Port?“) rechnet der Server. Der Editor ist eine dumme View. |
| **W4** | **Jede Geste ist ein Patch am Code.** | Ausgeführt über den warmen Roslyn-Workspace; ↶ nimmt den Patch zurück. Kein „C# schreiben“, kein Zusammenführen, keine Entwürfe im Board. |
| **W5** | **Prüfen = Bauen.** | Regeln sind Analyzer, Generator-Diagnosen und Boot-Guards. Die Grammatik (`DomainEditor/Grammatik.cs`) ist Daten für ⊕ und `--grammatik`, kein zweiter Prüfer. |
| **W6** | **Keine Simulation.** | Durchgespielt wird das echte System. Was der Editor zeigt, ist passiert. |
| **W7** | **Umgebungen unterscheiden sich nur in Konfig.** | Struktur ist Code (was existiert), Auswahl und Werte sind Konfig (was aktiv ist, mit welchen Parametern). |
| **W8** | **Quellen = Treiber + Konfig.** | Treiber sind fest codiert und lesen nur. Die Domäne benennt Quellen als Typ, sie schreibt keine Mechanik. |
| **W9** | **Der Weg folgt aus dem Log.** | Die Spur ist Teil des Logs (Header). Verlierbares ist eine best-effort-Ergänzung, die das System selbst nie liest. |
| **W10** | **Keine Reflection.** | Weder zur Laufzeit (generierte Tabellen und Kontexte) noch in Werkzeugen (Codegen, Schema, Extractor lesen über Roslyn). |
| **W11** | **Allgemeines Framework, je Absicht ein Weg.** | Die Domänen im Repo sind Prüfling und Beispiel. Eine Primitive bleibt, wenn sie allgemein gebraucht wird, nicht weil die Beispieldomäne sie nutzt. Für jede Absicht gibt es genau einen Weg — in der Bedienung wie im Code. |
| **W12** | **Grenzen.** | Keine GUIs im Editor, keine Schema-Evolution im Editor, kein Deploy. |

Alle sechs Invarianten (`CLAUDE.md`) gelten. Inv. 3 (Routing über Typen) gilt auch für Quellen: Sie sind Typen, keine Namen aus der Konfig.
Wo der Code einen Leitsatz heute noch verletzt, steht es in §12.2 bzw. `docs/13` §13.0.

---

## 3 · Quellen = Treiber + Konfig

### 3.1 Begriffe

| Begriff | Was | Wo | Wer schreibt |
|---|---|---|---|
| **Treiber** | die Technik: Abfrage-Schleife, Fertig-Erkennung, Fehlerbehandlung, Health | Framework (`Infrastructure/Quellen/Treiber/…`) | wir, einmal je Technik |
| **Quelle** | eine konfigurierte Instanz eines Treibers | Domäne: ein Einzeiler (Typ); Werte: Umgebungs-Datei | Editor |
| **Quell-Nachricht** | was die Quelle meldet, mit Kennung (genau einmal) | der Quellen-Typ selbst | — |
| **Einstieg** | wo ein Fluss beginnt | `p.Quelle<T>()` | Editor |

Eine Quelle **hat keinen Rumpf.** Die Bedeutung („welches Bildpaar ist diese Datei?“) ist ein Fluss-Schritt, also eine Funktion. **Ein Treiber liest
nur;** Aufräumen am Ende eines Vorgangs ist ein Funktions-Knoten im Fluss. Clients sind keine Treiber; sie sind ein eigener Weg hinein (Akteure).

### 3.2 Code-Form

```csharp
// Framework (Abstractions/Quellen/DateiGemeldet.cs): vom Treiber befüllt, in der Domäne nur benannt.
public abstract record DateiGemeldet : IQuellNachricht
{
    public required string Name { get; init; }            // Dateiname
    public required string Pfad { get; init; }            // absolut, wie der Treiber-Host ihn sieht
    public required string RelativerPfad { get; init; }   // relativ zum konfigurierten Verzeichnis
    public required long Groesse { get; init; }
    public required DateTimeOffset GeaendertAm { get; init; }
    public required DateTimeOffset ErkanntAm { get; init; }
    public required string Kennung { get; init; }         // vom Treiber nach Konfig gebildet (§3.5)
}

// Domäne: die Quelle ist ein Typ, sonst nichts. Treiber = Datei (folgt aus dem Basistyp).
public sealed record DateiErkannt : DateiGemeldet;
public sealed record KameraSystem : IMaschine, IDarf<DateiErkannt>;   // in wessen Namen

// Fluss
var datei = p.Quelle<DateiErkannt>();
```

**CQRS070** (`Domain.SourceGeneration/QuellenTypAnalyzer.cs`): Ein Typ, der von einer Treiber-Basis erbt, ist ein `sealed record`, erbt **direkt**,
hat keine Parameterliste und keine eigenen Member (der Treiber könnte sie nicht befüllen). Welche Basis zu welchem Treiber gehört, steht an einer
Stelle: `Abstractions/Quellen/Treibervertrag.cs`. Eine `IQuellNachricht` ohne Treiber-Basis ist erlaubt (Entscheidung E3).

### 3.3 Generiert

Der `QuellenGenerator` (`Infrastructure.SourceGeneration`) findet jeden nicht-abstrakten Typ mit Treiber-Basis und erzeugt `GeneratedQuellen.Alle`:
je Quelle Typ, Treiber, Konfig-Abschnitt (= Typname), Wirker-Id `quelle:<Typ>` und eine Fabrik `daten => new DateiErkannt { … }`, damit der Treiber
ohne Reflection baut. Die Konfig wird über den Configuration-Binding-Source-Generator gebunden. Proto und Wire-Kontext tragen die geerbten Felder.

### 3.4 Konfig (Abschnitt `Quellen` der Umgebungs-Datei)

```json
"Quellen": {
  "DateiErkannt": {
    "Pfad": "${env:HOME}/cqrs-sim/${branch}/DateiErkannt",
    "Muster": ["*.tiff"],
    "Unterordner": false,
    "Erkennung": "Abfrage",
    "Intervall": "00:00:01",
    "Fertig": { "Art": "Stabil", "Fenster": "00:00:01", "Endung": ".done" },
    "Kennung": "PfadGroesseZeit",
    "Laufmodus": "EinmalImCluster",
    "Webhook": "/webhook/datei"
  }
}
```

Felder und Defaults: `Abstractions/Quellen/DateiQuellKonfig.cs` (leere `Muster` = alle Dateien; `Erkennung` Abfrage | EreignisUndAbfrage;
`Fertig.Art` Stabil | Marker; `Kennung` Default `PfadGroesseZeit`; `Laufmodus` Default `EinmalImCluster`; `Webhook` optional). Die Treiber-Wahl
steht **nicht** in der Konfig; sie ist ein Code-Fakt (Basistyp).

**Start-Prüfungen** (Boot-Fehler mit Karte): jede Quelle braucht einen Abschnitt; ein Abschnitt ohne Typ, ein unbekanntes Feld, ein leerer Pfad,
eine Webhook-Route ohne `/`, eine doppelte Route und ein Webhook ohne Einblick-Port sind Fehler. Ein nicht erreichbares Verzeichnis ist nur eine
Warnung; der Treiber versucht es weiter.

### 3.5 Der Datei-Treiber

1. **Start:** Er listet die passenden Dateien und fragt gebündelt (je 500) den Log, welche Vorgänge zu diesen Kennungen schon existieren
   (`IStreamBestand.BekannteAsync`, Marten: `mt_streams`). Es gibt keinen eigenen Treiber-Stand; die Wahrheit ist der Log.
2. **Schleife:** Ein Weckruf löst einen Scan aus — das Intervall oder, bei `EreignisUndAbfrage`, ein FileSystemWatcher-Ereignis. Das Ereignis weckt
   nur, der Scan entscheidet (Inv. 2).
3. **Fertig-Erkennung:** `Stabil` = Größe und Änderungszeit über das Fenster unverändert; `Marker` = `<datei><Endung>` existiert (Marker-Dateien
   selbst sind nie Kandidaten).
4. **Melden:** nacheinander über `QuellEingang` (genau einmal je Kennung, dann Weckung der Flüsse). Die nächste Meldung kommt erst, wenn die
   vorige im Log steht (Rückstau). „Schon gemeldet“ merkt sich der Treiber je relativem Pfad; verschwundene Dateien fallen heraus, eine ersetzte
   Datei mit neuer Kennung wird erneut gemeldet.
5. **Fehler:** Ist das Verzeichnis nicht lesbar, ist die Quelle „gestört“ (`/health` → Degraded) und der Treiber versucht es mit wachsender Pause
   (bis 1 min). Wirft er sonst etwas, startet er nach 5 s neu. Nichts geht verloren, der nächste Scan holt alles nach.

| `Kennung` | Bildung | Folge |
|---|---|---|
| `NameGroesse` | `{Name}\|{Groesse}` | eine ersetzte Datei gleicher Größe wird nicht erneut erkannt |
| `PfadGroesseZeit` (Default) | `{RelativerPfad}\|{Groesse}\|{GeaendertAm:O}` | eine ersetzte oder neu geschriebene Datei startet einen neuen Vorgang |

**Vorgang:** Stream-Id = `MD5("quelle:" + Typ.FullName + ":" + Kennung)`, Append auf Version 0 (ein zweites Melden derselben Kennung ist idempotent),
Spur `wirker = quelle:<Typ>`, Akteur = der eindeutige `IDarf`-Halter. **Laufmodus:** `EinmalImCluster` hält eine Lease bei der Cluster-Identität
`quelle/<Typ>` (wandert bei Ausfall mit); `JederKnoten` für lokale Verzeichnisse je Maschine.

### 3.6 Webhook

Der zweite Weg in eine Quelle ist `Quellen:<Typ>:Webhook` (Route) der Umgebung (Entscheidung E12). `LaufeAsync` mappt jede Quelle mit Route
generisch (`MapQuellWebhooks`) auf dem Einblick-Port: `POST <route>` mit der Nachricht selbst als JSON (über den Wire-Kontext), Antwort
`202 {vorgang}`, gleiche Nachricht = derselbe Vorgang. Der Webhook hat heute **kein** Akteur-Tor (offen, §12.2). Im Editor erscheint die Route als
Bindung „⇥ Webhook“ an der Quelle und wird im Host-Rahmen bearbeitet.

---

## 4 · Umgebungen und Backend

### 4.1 Die Umgebungs-Datei

Je Umgebung gibt es **eine** Datei `umgebungen/umgebung.<name>.json`. Die Abschnitte und Felder sind Typen in
`Abstractions/Umgebung/UmgebungKonfig.cs` — **sie sind das Schema** (die Werkbank rendert daraus das Formular). Ein unbekannter Abschnitt oder ein
unbekanntes Feld ist ein Startfehler.

| Abschnitt | Inhalt (Typ) |
|---|---|
| `Umgebung` | Name (= Dateiname), Art `Sim` \| `Prod` |
| `EventStore` | Betrieb `Aus` \| `Verwaltet` \| `Vorhanden`, Verbindung, Datenbank, Schema, Befehls-Timeout |
| `VersionIndex` | Redis: Betrieb, Endpunkt, Datenbank, Präfix |
| `Cluster` | Art `Einzelknoten` \| `Consul`, Name, Consul, AdvertisedHost, Rolle `Allein` \| `Migrator` \| `Mitglied` |
| `Schreibpfad` | generiertes JSON, Group-Commit, Batch-Größe, Linger, Drain, Snapshot-Schwelle, Inbox-Grenze |
| `Lebenszyklus` | Leerlauf bis zur Passivierung in Sekunden: Aggregat (600), Adapter (120), Prozess-Manager (600); 0 = nie |
| `Konsum` | Batch-Konsum der Projektionen: Partitionen je Projektion (8; ein Wechsel ist ein Neuaufbau), Events je Seite (500) |
| `Transport` | gRPC-Port, Einblick-Port (0 = aus), JSON-Eingang an/aus |
| `Identitaeten` | Token → Akteur-Menge (`"A,B"`, `"A+B"` oder Liste), Standard, VertragStreng; ohne Abschnitt ist das Akteur-Tor offen |
| `Quellen` | je Quelle die Treiber-Konfig (§3.4) |
| `Funktionen` | je Funktion: Laufort `Host` \| `Extern` \| `Hand`, Implementierung, Slots, Wiederholungen, Worker (Python, Skript, Argumente, Token) |
| `Dienste` | je Dienst-Interface die Implementierung (Default: die einzige) |
| `Konfig` | je Konfig-Record die Werte, Feld für Feld |
| `Clients` | je Client: Token, Adresse, Urls, Dateien |
| `Logging` | ASP.NET-Standard |

**Lader** (`Infrastructure/Umgebung/UmgebungLader.cs`): Name aus `--umgebung` bzw. `CQRS_UMGEBUNG`; Reihenfolge Datei → Umgebungsvariablen →
Kommandozeile; Platzhalter `${branch}` (aus `CQRS_BRANCH`, DB-tauglich gemacht) und `${env:NAME}` (fehlend = Fehler); Geheimnisse stehen nur als
Verweis darin. Gebunden wird streng und generiert (Configuration-Binding-Source-Generator).

### 4.2 Host

`Host.Grpc/Program.cs` ist `var umgebung = builder.AddCqrs(args); … await app.LaufeAsync(umgebung);` — ohne jede Domänen-Zeile. Was **existiert**,
liefern die generierten Tabellen (Aggregate, Prozesse, Flüsse, Funktionen, Dienste, Quellen, Wire); was **aktiv** ist, sagt die Umgebung.

`AddCqrs` (`Infrastructure/Umgebung/CqrsUmgebungExtensions.cs`) bindet der Reihe nach: Kestrel (gRPC h2; Einblick-Port HTTP/1.1+2), Umgebungs-Siegel,
Redis-Präfix, Framework, `IDbClock` mit Versatz, Projektions-Stand, Pull-Pfade, Prozesse und Flüsse, Monitoring, Fristen, Akteure (`Identitaeten`),
Quellen, Funktionen (Laufort aus der Umgebung), Dienste (transitiv, aus `Dienste` und `Konfig`), Einblick. `LaufeAsync` mappt gRPC, Monitoring,
Einblick, JSON-Eingang und Quell-Webhooks; mit Rolle `Migrator` wendet es nur das Schema an. Alle Implementierungen sind einkompiliert, ein
Umgebungswechsel ist nie ein Code-Wechsel. Boot-Fehler gehen als Zeile `CQRS-BOOT {text, karte, art}` auf stderr; die Werkbank hängt sie an die Karte.

### 4.3 Trennung der Umgebungen

| Ressource | `prod` | `sim-main` | Warum |
|---|---|---|---|
| Postgres | `cqrs_events` (vorhanden) | `cqrs_sim_${branch}` (verwaltet) | die Wahrheit, getrennt |
| Redis | Präfix `prod:` | Präfix `sim:${branch}:` | Index nicht autoritativ, darf nicht mischen |
| Cluster | Consul, Name `cqrs` | Einzelknoten, Name `cqrs-sim-${branch}` | **kritisch:** sonst treten Sim-Actors dem Prod-Cluster bei |
| Ports (gRPC/Einblick) | 5001/5002 | 5101/5102, JSON-Eingang an | Clients müssen wissen, wohin |
| Quellen | echte Pfade | Sandbox `~/cqrs-sim/<branch>/<Quelle>/` | Treiber lesen nur |
| Funktionen | Host/Extern | Host/Extern/Hand | Mensch als Ausführer |
| Identitäten | (offen, §12.2) | `sim-<akteur>`-Tokens | Durchspielen als Akteur |
| Uhr | Versatz immer 0 | verstellbar | Fristen und Zeitlimits durchspielen |

**Umgebungs-Siegel** (`Infrastructure/Umgebung/UmgebungsSiegel.cs`): Jede Datenbank hat die Tabelle `<schema>.umgebung(name, art, cluster)`. Der
Host startet nur, wenn Art, Name und Cluster-Name dazu passen; eine leere DB wird beim ersten Start gesiegelt, nur in Sim legt der Host die DB an.
Damit kann ein Sim-Host nie in die Prod-DB schreiben und umgekehrt. Die Tabelle `<schema>.stand` hält Proto-Stand und Code-Stände (§4.6).

### 4.4 Uhr

`IDbClock.JetztAsync` liefert in Sim `now() + versatz`; der Versatz steht in der Tabelle `<schema>.uhr` (kein Abschnitt der Umgebungs-Datei).
In Prod liest die Uhr keinen Versatz, und ein Prod-Host setzt ihn beim Start auf 0. Es betrifft nur Code, der die Zeit über `IDbClock` liest
(Zeitlimits, Warte-Knoten, Fristen). Treiber-Intervalle laufen in Echtzeit.

### 4.5 Infrastruktur

Aus der Umgebung erzeugt die Werkbank ein Docker-Compose-Projekt `cqrs-<umgebung>` (`.werkbank/<name>/docker-compose.yml`) mit den
**verwalteten** Diensten, die nicht erreichbar sind: Postgres, Redis (wenn nicht `Aus`), Consul (bei Cluster = Consul). Den Zustand prüft sie per
TCP-Probe und zeigt ihn in der Kopfzeile. Ein nicht erreichbarer Dienst mit `Vorhanden` ist ein Fehler.

### 4.6 Die Sim-Datenbank

| Aktion (`📸`, nur Sim) | Mechanik (`Werkbank/Server/SimDatenbank.cs`) |
|---|---|
| Zurücksetzen | DB löschen; der Host legt sie beim nächsten Start neu an und siegelt sie |
| Stand speichern / laden / löschen | `CREATE DATABASE <db>_stand_<x> TEMPLATE <db>` bzw. zurück; Host wird angehalten und neu gestartet |
| Prod-Stand übernehmen | `pg_dump \| pg_restore` in eine neue Sim-DB; Siegel und Uhr werden entfernt |
| Stände auflisten | „gültig“ wird beim Auflisten gegen das aktuelle Proto berechnet |

Wenn sich Code ändert, während Daten in der Sim-DB liegen:
- **Event-Form inkompatibel** (Event entfernt, Feld entfernt, Feldtyp geändert — `ProtoVergleich` gegen `stand.proto`): ⟳ fragt, ob die Sim-DB
  zurückgesetzt wird. Schema-Evolution ist kein Ziel des Editors (§1).
- **Projektion oder Store geändert** (Code-Hash je Projektion in `stand`): in Sim wird die Leseseite neu aufgebaut; in Prod gibt es nur eine Meldung.
- **Aggregat-Logik geändert:** Die Snapshot-Version enthält den Code-Hash von State + Applier; ein abweichender Snapshot wird verworfen.
- **Prozess oder Fluss geändert:** Voll-Fold bei abweichendem `RegelHash`.

---

## 5 · Der Kreislauf ⟳ (Entwurf → Code → Lauf)

| Schritt | Was passiert (`Werkbank/Server/WerkbankServer.cs`) | Wo ein Fehler erscheint |
|---|---|---|
| 0 Schreiben | Jede Geste hat schon Code bzw. Umgebungs-Datei geschrieben (§8.2) | — |
| 1 Bauen | `dotnet build Host.Grpc -p:CqrsStand=<stand>`. Im Build läuft vor dem Compile der Codegen (Target `CqrsCodegenPrepass` in `Domain.Leitung`): `domain.proto`, STJ-Kontexte, Python-Basen (`vertraege.py`, `funktionen.py`, `funktions_tabelle.py`, `nutzlast_tabelle.py`); danach alle Generatoren und Analyzer | **an der Karte** (innerste `_ort`-Spanne, sonst Typ/Member) |
| 2 Python | betterproto-DTOs (`build_python.sh`); fehlende Python-Implementierungen von Clients und externen Funktionen werden angelegt (`PythonSync`) | Schritt rot |
| 3 Lesen | warmer Workspace liest neu; die eigenen Regeln (CQRS…) holt die Werkbank direkt aus den Analyzern der warmen Compilations (`RegelBefundeAsync`) | an der Karte |
| 4 Daten prüfen | Proto-Vergleich mit der Sim-DB (§4.6); bei Unverträglichkeit Rückfrage „zurücksetzen?“ | Dialog |
| 5 Infra | fehlende Container starten, Quell-Verzeichnisse anlegen | Host-Rahmen rot |
| 6 Starten | Host-Prozess `--umgebung <name>` ersetzen; bereit, wenn `/health` antwortet | **Boot-Fehler an der Karte** (`CQRS-BOOT`) |

Bei einer Prod-Umgebung endet ⟳ nach Schritt 3 (Prod wird nicht aus dem Editor gestartet). Python-Worker externer Funktionen startet „▶ Worker“
(§6), nicht ⟳. Board, Build und Lauf tragen denselben Stand-Hash; wer weiterentwirft, während das System läuft, sieht „Entwurf ≠ Lauf“.

---

## 6 · Durchspielen: nur echte Eingänge

| Eingabe | Im Editor | Echter Weg dahinter |
|---|---|---|
| Datei | Drop auf die Quellkarte | die Werkbank legt die Datei in den Pfad der Quelle der aktiven Umgebung, der echte Treiber meldet sie |
| Webhook | — (von außen) | `POST <route>` am Einblick-Port (§3.6) |
| Als Akteur handeln | Konsole unten: Token/Akteur wählen, Command/Query als Formular | Werkbank leitet an den **JSON-Eingang** des Hosts (`/eingang/command|query`, Header `akteur-token`) → Akteur-Tor → Aggregat bzw. Reader |
| Funktion ohne Implementierung | offener Auftrag an der Funktions-Karte (⧗), Ergebnis-Fall wählen, Felder füllen | Laufort `Hand`: der Host hält die Aufträge (`HandAusfuehrer`), das Ergebnis geht durch denselben Abschluss wie bei jedem Ausführer |
| Python-Funktion | „▶ Worker“ an der Karte (nur Laufort `Extern`) | generischer Worker über gRPC mit einem Token, dessen Akteur `IDarf<F>` hat |
| Zeit | ⏩ in der Kopfzeile | Uhr-Versatz (§4.4) |
| Nochmal | Stand laden, Eingabe wiederholen | echte Daten, echte Kennung |

Der JSON-Eingang ist nur bei `Transport:JsonEingang` und Einblick-Port aktiv (Sim an, Prod aus; Entscheidungen E5/E6). Die Formulare kommen aus den
gelesenen Record-Feldern. **Haltepunkte gibt es bewusst nicht:** Sie bräuchten Sonderlogik in Dirigent und Konsumenten; ein menschlicher Ausführer
an der Funktion leistet dasselbe auf echtem Weg.

---

## 7 · Spur und Visualisierung

### 7.1 Spur-Daten

Jeder Log-Eintrag trägt vier Header, vom Framework gesetzt und unsichtbar für den Fachcode (Inv. 5). Die Namen stehen an einer Stelle
(`Abstractions/Spur/Spur.cs`), geschrieben werden sie an einer Stelle (`Infrastructure/Persistence/SpurHeader.cs`).

| Header | Inhalt |
|---|---|
| `ursache` | der Log-Eintrag, der ihn ausgelöst hat: `Stream:Version` |
| `wirker` | der Baustein, der geschrieben hat — **dieselbe Id wie die Karte im Editor** |
| `ueber` | der Absender des Commands bzw. Auftrags, den der Wirker verarbeitet hat (Fluss-Knoten, Regel, Reaktion, Akteur) |
| `stand` | Hash des Builds (`-p:CqrsStand`) |

- **Weitergabe:** Ursache, Wirker und Absender reisen per AsyncLocal (`Spur.Von`) um jeden Handle bzw. Fluss-Schritt; der `CommandEmitter` legt sie
  in den Command-Envelope, Aufträge tragen sie im `AuftragAnbieten`. Damit ist der Kausalgraph auch über Commands und Funktionen lückenlos.
  `CausationId` bleibt unangetastet, sie trägt den Prozess-Fold.
- **Wirker-Format** ist generiert, nie handgeschrieben; die eine Quelle ist `Abstractions/Spur/WirkerId.cs` (per Link in Generatoren und Extractor):
  `decide:<Aggregat>/<Command>`, `fluss:<Fluss>#<Knoten>`, `prozess:<Prozess>#<Regel>`, `funktion:<IFunktion>`, `quelle:<Typ>`,
  `reaktion:<Typ>`, `akteur:<Akteur>`, `projektion:<Typ>`. Regel-Namen sind `<Auslöser>[+<Und>]→<Ziel>` aus den Typ-Argumenten (CQRS072 bei
  Kollision). Projektionen schreiben nie ins Log; `projektion:` ist nur Karten-Id. Reaktion und Akteur erscheinen als `ueber`.
- **Der Weg** eines Eintrags über die Karten ist eine reine Funktion: `Abstractions/Spur/SpurWeg.Aus`.
- **Verlierbares** (Ablehnung, transientes Event) steht nur in Sim in der Tabelle `spur_hinweis` (best-effort, vom System nie gelesen,
  Aufbewahrung 7 Tage). Zeitlimit und Umleitung sind dagegen Log-Einträge des Dirigenten (`SchrittGescheitert`/`SchrittUmgeleitet`).
- **Lesen:** Der `SpurLeser` liest die Header direkt aus `mt_events` — in Sim und Prod gleich, ohne eigene Projektion. Ein Vorgang ist die
  Korrelation; sein Zustand (läuft · wartet · fertig · gescheitert) folgt aus den Prozess-Events.

### 7.2 Synchron halten

- **Einblick-API** des Hosts (nur Einblick-Port, nur lesend, `Infrastructure/Einblick/EinblickEndpunkte.cs`): `/einblick/stand`, `/spur?ab=`,
  `/vorgaenge`, `/vorgang/{korrelation}`, `/auftraege`, `/hinweise`, SSE `/neu` („neu bis `seq`“).
- **Spiegel** in der Werkbank (`Werkbank/Server/SpurSpiegel.cs`): folgt `/neu` und holt ab dem eigenen Cursor nach. Ein verlorenes Signal oder ein
  Neustart schadet nicht (dasselbe Muster wie Signal plus Pull im Kern). Der Editor bekommt fertige Daten über
  `/api/werkbank/spur/puls|vorgaenge|vorgang/{k}`.
- **Identität:** Board, Build und Lauf tragen denselben `stand`. Spuren älterer Stände erscheinen grau; existiert ihre Karte nicht mehr, zeigt der
  Editor „⚠ Karte gibt es nicht mehr“.

### 7.3 Ansichten im Editor

- **☰ Vorgänge:** Liste mit Filter nach Quelle, Akteur, Aggregat und Zustand.
- **Spur-Überlagerung** für den gewählten Vorgang: gegangene Kanten nummeriert ①②③ mit Zeit, nicht gegangene Ausgänge blass; ⧗ pulsiert, ✕/⏳ rot,
  Verlierbares gestrichelt; Klick auf eine Nummer → Panel mit Payload, Akteur, Knoten, Stand, „↤ verursacht von / ↦ bewirkte“.
- **⏱ Zeitleiste:** Bahnen je Baustein; der Schieber zeigt das Board zum Zeitpunkt t (Abspielen der Aufzeichnung, nie erneutes Ausführen).
- **〰 Puls**, wenn kein Vorgang gewählt ist: Zähler und „zuletzt“ an jeder Karte, Kantendicke nach Menge, Stau (offene Aufträge, Rückstau).
- **⚠ Meldungen:** Build-, Regel-, Lese- und Boot-Befunde.

---

## 8 · Der Editor

### 8.1 Oberfläche

```
┌──────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ Umgebung ▾ · ● Build (stand) · ● Läuft · Infra-Chips · ⟳ · 📸 · ⏩     ↻ · ▦ · 🗂 · 📐 · ↶             │
├─────────────────────────────────────────────────────────────────────────────────────┬────────────────┤
│ Board: Domäne × Akteur-Rahmen, 📜 Vertrags-, 🔌 Client-, ⛓ Fluss-, 🖥 Host-Rahmen     │ Panel           │
│        + Spur-Überlagerung                                                          │ (Bearbeiten,    │
│                                                                                     │  Werte, Payload)│
├─────────────────────────────────────────────────────────────────────────────────────┴────────────────┤
│ ☰ Vorgänge │ ⏱ Zeitleiste │ ⌨ Konsole (als Akteur) │ ⧗ Aufträge │ ▶ Worker │ ⚠ Meldungen │ 〰 Puls      │
└──────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

- **Rahmen:** je Domäne × Akteur (Reihenfolge abgeleitet: Ersteller → wer worauf reagiert → Art → Name; „ohne Akteur“ zuletzt); 📜 Vertrag je Akteur
  mit Zusage-Karten; 🔌 Client mit Anschlussleiste und Bündeln; ⛓ Fluss in der Heim-Domäne seiner Commands; 🖥 Host-Rahmen rechts neben den
  Clients. Rollen-Spalten innerhalb der Rahmen. Einzelkanten nur per Klick; der Zoom zeigt nichts anderes, nur größer.
- **🖥 Host-Rahmen:** Karten je Umgebungs-Abschnitt (§4.1) plus Uhr. **Ein generischer Formular-Renderer** zeichnet jedes Panel aus dem Schema
  (`GET /api/werkbank/schema`; Record → Felder, Enum → Auswahl); es gibt keine handgebauten Formulare je Karte. Dienste und Konfig-Records zeigen
  die Einträge aus dem gelesenen Code. Die Quellkarte zeigt „Werte in <Umgebung>“.

### 8.2 Lesen und Schreiben

**Lesen** (`Werkbank/Server/Arbeitsbereich.cs`, `GraphExtractor.Kern/Lesen.cs`): Die Werkbank lädt die Solution einmal kalt (`MSBuildWorkspace`,
`Projektlage`) und hält sie dann warm: Ein Dateiwächter übernimmt geänderte `.cs` nur im Speicher (`WithDocumentText`), `.py` und
`umgebungen/*.json` lösen ein Neulesen aus, `.csproj`/`.sln` ein kaltes Laden. Warm gelesen wird nur unterhalb der Laufzeit (E7: Routing aus den
Decide-Signaturen). Ein Lesen liefert `Lesung` → Board mit `board.sicht` (§8.3). **Code mit Fehlern wird gelesen:** eine unaufgelöste Basis ergibt
eine Code-Karte ⚠ bzw. einen Befund an der Karte; Fluss-Anweisung ohne Knoten, verworfene Regel, doppelter Name werden Lese-Befunde. Jede Karte
trägt `_ort {datei, start, ende, zeile}` und gegebenenfalls `_teile` (partial-Teile, Implementierungen, Vertrags-Teile).

**Projektlage** (`GraphExtractor.Kern/Projektlage.cs`) bestimmt die Rollen der Projekte nur aus Code-Fakten: Vertrag (`Abstractions`), Laufzeit
(das niedrigste Projekt mit `[RoutingTabelle]`-Generat), Domänen-Projekte (handgeschriebene Rollen-Marker), Hosts (Einstiegspunkt).

**Schreiben** (`GraphExtractor.Kern/Schreiben/`): Der Editor schickt ein Karten-Delta (`{karten: {Sammlung: [Karte]}, entfernt: {Sammlung: [Schlüssel]}}`)
an `POST /api/werkbank/geste`. Der Motor (`Gesten.Schreibe`) bildet Soll = Code-Stand + Delta, stempelt die Herkunft aus dem Code-Stand, ordnet die
Fluss-Knoten und schreibt:
- **Neues** über den Scaffolder (`DomainEditor/Scaffolder.cs`) und Mischen in bestehende Dateien (`TypenMergen`, `MethodenMergen`, …);
- **Geändertes** über `DateiAbgleich` (je Konstrukt ein Abgleich, nur wenn der Stempel abweicht; Signatur gehört dem Modell, Rumpf dem Code);
- **Entferntes** an `_ort` + `_teile` (Verweise bleiben stehen und werden zum Compiler-Fehler an der Karte).
- **Umbenennen** über den Roslyn-Renamer (`POST /api/werkbank/umbenennen`, alle Verweise auch in Lambdas; die Datei mit dem Typnamen zieht mit).
- Neue `using` werden einsortiert (`Usings.Ergaenze`).

Danach liest der warme Workspace neu; Compiler- und Regel-Befunde der berührten Dateien stehen sofort an der Karte. **↶** nimmt den letzten
Patch zurück (`.werkbank/verlauf.<branch>.json`, nur wenn die Dateien noch auf dem geschriebenen Stand sind). Die Ansicht liegt in
`.werkbank/ansicht.json`. **Gesten sind auf `main`/`master` gesperrt.** Welche Geste welchen Code schreibt: Anhang A.2.

### 8.3 Ableitungen und Prüfung nur auf dem Server

- **`board.sicht`** (`GraphExtractor.Kern/Sicht/`, `Sicht.Aus`) liefert je Karte (Editor-Id, Anhang A.7): Art, Domäne, Block, Rolle, Akteure,
  Akteur-Rahmen, Insel, Vertreter, Besitzer, Store-Scope, Aggregat, Tiefe. Dazu: Kanten, Prozess-Kette, Akteur-Reihenfolge, Flüsse (je Knoten Ports,
  Vorgänger, Tiefe, gehörte Events, Zuweisung, Drähte), Clients (Anschlussleiste), Ketten-Pfade und Partner („◀ kommt aus · geht an ▶“ je Nachricht,
  mit Lösen-Beschreibung).
- **⊕-Verbinden fragt den Server:** `POST /api/werkbank/passt` (Port + Kandidaten → passt/gesperrt mit Regel), Grammatik aus `rahmen.grammatik`.
  **Fokus:** `GET /api/werkbank/kette`.
- **Prüfen = Bauen:** Es gibt keinen Validator. Die eigenen Regeln (CQRS…) holt die Werkbank bei ⟳ für alle Domänen-Compilations und eine
  Gesamtbild-Compilation (`Arbeitsbereich.RegelBefundeAsync`) und nach jeder Geste für die berührten Dateien. Ein Befund hängt an der innersten
  Karte, deren `_ort` ihn enthält (`GET /api/werkbank/diagnosen`).

### 8.4 Rumpf an der Karte

- Der Rumpf-Port zeigt den Code **direkt aus der Datei** (Methoden-Rumpf, Lambda eines Fluss-Knotens bzw. einer Regel, Implementierung einer Store-Fn
  oder Funktion, Python-Anker); wo früher ein 📝-Block stand, steht eine Rumpf-Kachel (`rp:<Karte>:<i>`). Ein leerer Decide/Apply-Rumpf zeigt
  „⚙ Logik fehlt“.
- **✎** = Geste `POST /api/werkbank/rumpf` (`Schreiben/Rumpf.cs`): ersetzt genau diesen Bereich, gesperrt gegen geänderten Text; Python über
  `PythonSync`.
- **🤖 füllen** ist eine Aktion am Rumpf (Anhang A.5). Dienst-Rümpfe haben nur ✎.

### 8.5 Bedienung

| Absicht | ein Weg |
|---|---|
| Etwas Neues beginnen | **Palette mit Ankern:** Aggregat · ⛓ Fluss · ƒ Funktion · 📥 Quelle · 👤 Akteur · 🔌 Client · Projektion |
| Etwas Zugehöriges anlegen | „＋“ am Port, an dem es hängt (Command am Decider, Event am Ausgang, Auftrag an der Funktion …) |
| Verbinden / lösen | ⊕ im Panel → passende Karten leuchten (auch nicht geladene im Katalog) → anklicken |
| Bau-Argumente (Fluss-Eingang, Regel-Dann) | visueller Zuordnungs-Editor (`konzept-editor-pipelines.md`, „Zuordnungs-Editor“) |
| Fokussieren | **ein „Fokus“**: ohne Vorgang die statische Kette, mit Vorgang die echte Spur |
| Bauen und Starten | ⟳ |
| Rückgängig | ↶ |
| Domänen ein- und ausblenden | Filter (Ansicht) |

---

## 9 · Vollständigkeit: die Code-Karte

- **Domänen-Projekte** sind die von der Projektlage erkannten Projekte (Domäne, Abläufe, Leseseite, Akteure/Verträge/Clients, Quellen-Typen). Dazu
  gehören die Umgebungs-Dateien und die Python-Rümpfe der Worker; Frontend-Projekte gehören nicht dazu.
- **Code-Karte** (`code:T`): Jede Typ-Deklaration oberster Ebene, die keine Primitive ist (Hilfsklassen, Erweiterungen), wird eine Code-Karte (roher
  Code, Compiler-Fehler, ⚠ nur bei unaufgelöster Basis) in ihrer Domäne; partial-Teile liegen an einer Karte. Der Extractor rät nichts.
- **Gate:** `dotnet run --project GraphExtractor -- --check` meldet **„Code ohne Karte: 0“** (Pflicht) und den Anteil der Code-Karten (Kennzahl,
  soll sinken). `--check` prüft außerdem Inventar ⇄ Kartenliste, Board ⇄ Modell, Wirker-Ids ⇄ Karten, Routing-Gegenprobe und den Fixpunkt
  (Scaffolder → neu lesen → gleich). `--sonde` beweist dasselbe an einer fremden Domäne (Anhang A.1).

---

## 10 · Was es nicht mehr gibt

Diese Begriffe sind abgelöst; tauchen sie in Doku oder Code auf, ist die Stelle veraltet:

| Abgelöst | Ersetzt durch |
|---|---|
| Simulation im Editor (`ModellSimulation`, `Musterwerte`, `/api/editor/sim/*`, „▶ Simulation“, `Cqrs.Testing/Abdeckung`, `DslSchreiber`/„Als Test“) | Umgebung `sim` + Spur (§6, §7) |
| `SimHost` | `Werkbank` |
| Board als Wahrheit (`board-model.json`, localStorage-Modell, `mergeBoard`, Entwürfe, „C# schreiben“, 👁 Vorschau, `/api/editor/write`) | Code + Ansichts-Datei + Patch je Geste (§8.2) |
| JS-Ableitungen (`akteurSet`, `deriveMembership`, `derivedStores`, `compatible`, `boardEdges`, `grPruefe`) | `board.sicht`, `/passt`, `/kette` (§8.3) |
| `DomainEditor/Validator.cs`, `ProjektPruefung` | Analyzer/Boot-Guards; Prüfung im warmen Workspace |
| 📝/🤖-Karten, `codeSrc`, Andocken, Code-Sync-Poll, `/api/editor/code|open|compile` | Rumpf aus der Datei, ✎/🤖 als Aktion (§8.4) |
| Landkarte/Ablauf/LOD, semantischer Zoom, Slice/◎/Inspector | Rahmen + ein Fokus |
| Trigger-, Handle-Pipeline-, HostSetting-, Betriebs-Band-Karten | Quelle, Fluss, 🖥 Host-Rahmen |
| `IQuelle`, `AddQuelle`, `DateiQuelle`, `FileWatchConfig`, `MapQuellWebhook<T>` | Datei-Treiber + `Quellen:<Typ>` (§3) |
| `AddFunktion<F, Impl>` / Domänen-DI im Host | Laufort und Dienste aus der Umgebung (§4.2) |
| LoadHarness, `InMemoryEventStore` | — (Store-Semantik nur gegen echtes Marten) |

**Noch im Code, aber ohne Rolle im Editor** (Rückbau offen, §12.2): die Laufzeit-Trigger-Welt (`IPipelineTrigger`, `ITriggerRegistration`,
`TimerTrigger`, `WebhookTrigger`), die `IPipelineHandler`-Maschinerie samt `Selbst<T>`/`Frist<TCmd>`/`FristStorno<TCmd>` als Handle-Ausgang,
`[Ingress]` und `GraphExtractor.Kern/CompositionRoot.cs`.

---

## 11 · Die Werkbank im Code

| Teil | Ort |
|---|---|
| Server, Endpunkte | `Werkbank/Program.cs`, `Werkbank/Server/` (`WerkbankServer` ⟳, `Arbeitsbereich` warmer Workspace, `GestenDienst`, `Diagnosen`/`Karten`, `Umgebungen`, `Schema`, `Infra`, `SimDatenbank`, `HostProzess`, `Durchspielen`, `Worker`, `SpurSpiegel`, `Verlauf`) |
| Oberfläche | `Werkbank/wwwroot/editor/` (`index.html`, `editor.css`, `editor.js`) |
| LLM | `Werkbank/LlmKonsole.cs`, `Werkbank/CodeSync.cs`, `GraphExtractor.Kern/LlmKontext.cs`, `SlotInventar.cs` |
| Python-Rümpfe | `Werkbank/PythonSync.cs`, `DomainEditor/PythonQuelle.cs`, `GraphExtractor.Kern/PythonClients.cs` |
| Lesen | `GraphExtractor.Kern/` (`Lesen`, `Projektlage`, `DomainExtractor`, `FlussLeser`, `ModellMapper`, `Vertrag`) |
| Ableiten | `GraphExtractor.Kern/Sicht/` |
| Schreiben | `GraphExtractor.Kern/Schreiben/` (`Gesten`, `DateiSchreiber`, `DateiAbgleich`, `Rumpf`, `Umbenennen`, `Usings`), `DomainEditor/Scaffolder.cs` |
| Grammatik | `DomainEditor/Grammatik.cs` |
| Prüfen | `GraphExtractor.Kern/ParitaetsPruefung.cs` (`--check`), `GraphExtractor.Kern/Sonde/` (`--sonde`), CLI `GraphExtractor/` |
| Laufzeit-Gegenstück | `Infrastructure/Umgebung/`, `Infrastructure/Einblick/`, `Infrastructure/Eingang/`, `Infrastructure/Quellen/`, `Abstractions/Spur/` |

---

## 12 · Entscheidungen

### 12.1 Entschieden (nicht neu verhandeln)

1. Code ist die einzige Wahrheit; das Board wird nie als Modell gespeichert.
2. Keine Simulationsschicht; die Sim ist der echte Host in Umgebung `sim` gegen echtes Postgres/Marten.
3. Der Laufzeit-Host ist `Host.Grpc` mit `--umgebung`. Die Werkbank ist Server für Editor, Schreiben, Bauen, Compose, Prozesse, Konsole und Ausführer.
4. Eine Sim-DB je Git-Branch (`cqrs_sim_<branch>`).
5. Der Editor liest die Laufzeit über die Einblick-API des Hosts, nicht direkt aus der DB.
6. Quellen = fest codierte Treiber + Konfig; eine Quelle = ein Nachrichtentyp; die Treiber-Wahl ist ein Code-Fakt (Basistyp).
7. Treiber lesen nur.
8. Default-Laufmodus `EinmalImCluster`.
9. Keine Abwärtskompatibilität bei unseren Umbauten (2026-10-12): keine Migration alter Daten, Kennung Default `PfadGroesseZeit`.
10. Funktions-Laufort, Dienst-Implementierung und Konfig-Werte sind Konfig je Umgebung.
11. Spur-Header `ursache` (nicht `CausationId` umdeuten), Wirker auf Karten-Ebene.
12. Keine Haltepunkte.
13. Kein Deploy aus dem Editor; Prod nur lesend.
14. Keine GUIs im Editor; Clients nur vorbereiten.
15. Keine Schema-Evolution im Editor; inkompatible Sim-Daten → Sim-DB zurücksetzen. **`IUpcast` bleibt als Backend-Primitive** (2026-10-13).
16. 🖥 Host-Rahmen rechts neben den Client-Rahmen; kein Band, kein neues Layout.
17. Lokaler Multi-Node in der Sim als Option (Cluster = Consul).
18. **Keine Reflection, auch nicht in Werkzeugen** (W10, 2026-10-13).
19. **Allgemeines Framework, Domäne nur Prüfling, je Absicht ein Weg** (W11, 2026-10-13).

### 12.2 Offen

Allgemeine Schulden stehen in `docs/13` §13.0. Für die Werkbank im Besonderen:

- **Weitere Treiber:** Takt (Kennung = Zeitschlitz) für Zeit als Anstoß, OPC UA, Datenbank-Trigger; **flüchtige Quellen** für Telemetrie
  (`ITransientEvent`, Konflation; Inv. 6); **Client meldet Quell-Nachricht** (`ISendet<T>` mit `T : IQuellNachricht`). Danach Rückbau der
  Trigger-/Handle-Welt (§10), einschließlich der zugehörigen Grammatik-Regeln. Zeitlimits sollen dann über den durablen Fristplaner geweckt werden
  statt über den 15-s-Backstop.
- **Tor am Webhook** einer Quelle (Token → Akteur); Identitäten für Prod.
- **`IAkteurDienst<A>`**: heute nur an der Handle-Pipeline verdrahtet; vermutlich durch Funktion + `IDarf<F>` gedeckt.
- **Prozess- und Fluss-DSL** sind zwei Schreibweisen auf demselben Dirigenten (W11).
- **Leitsatz-Verletzungen im Code:**
  - W10: der Codegen liest per `MetadataLoadContext`; `Werkbank/Server/Schema.cs` liest die Konfig-Typen per Reflection aus der eigenen DLL
    (statt aus dem warmen Workspace).
  - W3: `editor.js` baut Karten-Ids (`graphNodes`) und rechnet Hör-Menge, Lambda-Parameter, Auto-Zuordnung und Port-Kardinalität selbst.
  - W1: die LLM-Kontexte (Slot-Index) entstehen kalt aus `GraphExtractor --kontexte`, nicht aus dem warmen Board; die CLI `--schreiben <board.json>`
    ist ein Rest von „C# schreiben“. (🤖 und Umgebungs-Werte schreiben seit 2026-10-15 als Geste; tote Endpunkte entfernt — docs/13 §13.6.)
- **Geste p50 unter Fremdlast 346–470 ms** (Ziel 300 ms). Der Renamer ändert keine String-Literale.
- **Pfade für entfernte Ausführer** (Python-Worker sehen ein Share anders gemountet): `RelativerPfad` ist vorbereitet.
- ~~Kein automatischer ⟳-Abnahmetest~~: `Werkbank.Abnahme.Tests` nimmt ⟳ → Host → Durchspielen → Spur → „▶ Worker“ → Geste/↶ → Daten-Prüfung
  ohne Browser ab (2026-10-15, docs/12 §12.6). Das Gate „frischer Checkout baut“ steht (heute `./pruefe.sh` auf dem frisch ausgecheckten Messserver, docs/12 §12.1).
- **Eine Anwendung je Werkbank?** (WB-4): Werkbank, Scaffolder und Extractor arbeiten auf `Projektstruktur.StandardWurzel` und bauen `ServerHost`; das
  Anlegen einer neuen Anwendung ist nicht beschrieben. Zu entscheiden: festschreiben oder Wurzel wählbar machen.
- **Daten-Prüfung (§4.6, ⟳ Schritt 4)** vergleicht das generierte Proto; gespeichert sind die Events als JSON (Marten/STJ). Zu entscheiden, ob
  die Prüfung gegen die JSON-Form laufen soll.

- **🤖-Rümpfe fachlich ungeprüft** (docs/13 LLM-1): geprüft wird nur „baut und regelkonform“; Plan in `konzept-llm-minimalkontext.md` §11.

Reihenfolge der Umsetzung: `docs/13` §13.5.

### 12.3 Entscheidungen aus der Umsetzung (E1–E13)

Nummern wie im Umsetzungsplan, weil Code-Kommentare sie zitieren.

| # | Entscheidung |
|---|---|
| E1 | Keine Abwärtskompatibilität für Bestandsdaten von `DateiErkannt`; alte Vorgänge werden nicht weiter verarbeitet. |
| E2 | *(abgelöst durch E11)* Konfig-Records kommen nicht aus `Funktionen:<F>`, sondern aus `Konfig:<Record>`. |
| E3 | CQRS070 gilt nur für Typen mit Treiber-Basis; eine `IQuellNachricht` ohne Treiber-Basis bleibt erlaubt (Webhook-/Test-Quelle). |
| E4 | Regel-Name `<Auslöser>[+<Und>]→<Ziel>` aus den Typ-Argumenten; Kollision = Build-Fehler CQRS072. |
| E5 | Der Editor handelt als Akteur über einen JSON-Eingang des Hosts (`/eingang/*`, Token-Header), schaltbar per `Transport:JsonEingang`. |
| E6 | Zweiter Kestrel-Endpunkt `Transport:EinblickPort` (HTTP/1.1+2) für Einblick, SSE, JSON-Eingang, Webhook und `/health`. |
| E7 | Die Gesten-Schleife liest nur aus den Domänen-Compilations; Routing aus den Decide-Signaturen; `GeneratedCommandRouting` ist Gegenprobe in `--check`. |
| E8 | Editor-JS/CSS sind statische Dateien (`Werkbank/wwwroot/editor/`). |
| E9 | Code-Hashes: Aggregat = State + Applier (Snapshot-Version), Projektion = Klasse + Stores; Neuaufbau nur in Sim automatisch, in Prod Meldung. |
| E10 | Sandbox-Pfad und Python-Interpreter stehen in der Umgebung (`Quellen:<Q>:Pfad`, `Funktionen:<F>:Worker`). |
| E11 | Domänen-Dienst = Marker `IDienst`; Implementierungen sind implementierende Klassen; `DienstGenerator` → `GeneratedDienste`; Bindung aus `Dienste:<I>:Implementierung` (Default: die einzige), Konfig-Records aus `Konfig:<Record>`; Analyzer CQRS089–092; Zyklen = Boot-Fehler. |
| E12 | Der Webhook einer Quelle ist Konfig (`Quellen:<Typ>:Webhook`), generisch gemappt auf dem Einblick-Port. |
| E13 | Python-Rumpf einer externen Funktion: generierte Basis je `IFunktion` (`generated/funktionen.py`), Implementierung in `funktionen/<modul>.py`, ein generischer Worker lädt aus einer generierten Tabelle. |

---

## 13 · Entstehung

Das Zielbild wurde am 2026-10-12 beschlossen und in neun Phasen umgesetzt: P0 Aufräumen, P1 Datei-Treiber, P2 Umgebungen, P3 Werkbank-Server, P4 Spur,
P5 Durchspielen, P6 Visualisierung, P7 Eine Wahrheit (warmer Workspace, Geste → Patch, Ableitungen auf dem Server, Prüfen = Bauen, alles außer
Treibern im Editor), P8 Simulation raus — abgeschlossen am 2026-10-13 mit der Abnahme an einer neuen Domäne, die nur über Gesten und ⟳ entstand.
Historie im Git-Verlauf (gelöschtes `docs/plan-werkbank-umsetzung.md`).

---

## 14 · Leitplanken für Änderungen

- **Die sechs Invarianten** und die Konventionen aus `CLAUDE.md` gelten; dazu die Leitsätze W1–W12.
- **Code-Fakten:** neue Erkennung über Marker/Attribut/Symbol; neues Konstrukt ⇒ Sonde + `soll.txt` erweitern. Die Sonde deckt alle allgemeinen
  Primitive ab, nicht nur die der Beispieldomäne.
- **Keine Reflection:** neue Dispatch-Logik = Generator; neue Werkzeuge lesen über Roslyn.
- **Editor-Vorgaben:** deklariert wird nur an der eigenen Karte, verbunden nur per ⊕; „＋“ am Port legt eine neue Karte an; kein semantischer Zoom;
  das Layout wird nicht ersetzt, nur ergänzt; Oberflächen-Änderungen über dieses Dokument hinaus nur nach Rückfrage.
- **Ein Weg:** keine neuen Schreib- oder Ableitungswege neben Geste und `board.sicht`; keine Ableitung im Browser.
- **Verifizieren:** Prüfstand, bei Laufzeit-Änderungen Integration sequentiell (`docs/12`), `--check` und `--sonde`, bei Editor-Änderungen im
  Browser (`http://localhost:5178/editor/`) geprüft und belegt.

---

## 15 · Verwandte Dokumente

| Dokument | Rolle |
|---|---|
| `konzept-editor-pipelines.md` §12–§14 | Detail-Referenz Fluss (DSL, Semantik, Laufzeit, Editor, Zuordnungs-Editor) |
| `konzept-akteure.md` | Detail-Referenz Akteure, Verträge, Clients |
| `konzept-llm-minimalkontext.md` | Detail-Referenz LLM-Kontext und Slots |
| `konzept-editor-komposition.md` | Sprache des Editors (Alphabet, Operatoren, Kapselung) |

Bei Widerspruch gilt dieses Dokument.

---

## Anhang A

### A.1 Lesen: Grundlagen

- **Geparst statt nachgebaut.** Kalt kommt die Routing-Wahrheit aus dem Laufzeit-Generat `GeneratedCommandRouting` (Syntaxbaum + Semantik-Modell,
  `GraphExtractor.Kern/RoutingTruth.cs`); warm und ohne Generat aus den `Decide`-Signaturen, mit ehrlich ausgewiesener Herkunft.
- **Symbole, nicht Text.** Symbole aus verschiedenen Compilations sind nie referenzgleich; verglichen wird über voll-qualifizierte Namen.
  Die Vertrags-Anker stehen in `GraphExtractor.Kern/Vertrag.cs` (per `typeof(...).FullName`) und in den Vertrags-Dateien von `Abstractions`.
- **Prozesse und Flüsse** werden als Syntax gelesen (jede Anweisung im `Definiere`-Lambda eine Regel bzw. ein Knoten; `FlussLeser`); Regel- und
  Knoten-Namen wie zur Laufzeit (`WirkerId`).
- **Sonde:** `GraphExtractor.Kern/Sonde/` enthält eine fremde Domäne („Leihwesen“) als Quelltext, ein handgeschriebenes Soll (`soll.txt`) und ein
  Gesten-Skript (`Gezeichnet.gesten.json`). `--sonde` liest, schreibt per Gesten, liest neu und vergleicht.
- **Graph-Diagnosen** sind nur noch Info (Routing-Herkunft, Event ohne Konsument); alle früheren Diagnosen sind Analyzer (A.4).

### A.2 Schreiben: Regeln und Geste → Code

**Regel des Abgleichs:** Die Signatur gehört dem Modell, Rumpf und Handcode gehören dem Code. Was eine Geste nicht betrifft, bleibt Wort für Wort
stehen — Defaults, Initialwerte, Handcode-Zusätze, Rümpfe (auch bewusst leere), Deklarations- und OneOf-Reihenfolge, Lambdas, `using`s,
Summary-Doku. Form-Regeln aus der Signatur (CQRS050): keine Ausgabe ⇒ `Task`; sonst `OneOf<…>` konkreter Typen; Fähigkeiten sind
Handle-Parameter, nie Konstruktor-Felder.

| Geste | Code (Methode) |
|---|---|
| Neues Aggregat | `Scaffolder.StateDatei`/`DeciderDatei`/`ApplierDatei` → `{A}.cs`, `{A}.Decider.cs`, `{A}.Applier.cs` |
| Command/Event/Ablehnung/Wertobjekt/Enum anlegen | `Scaffolder.TypDatei` oder `TypenMergen` in den Namespace-Block |
| Feld/Enum-Wert ändern | `DateiAbgleich.RecordFelder`/`StateFelder`/`EnumWerte` |
| Decide/Apply hinzufügen | `MethodenMergen` (Rumpf wirft, bis er gefüllt ist) |
| OneOf eines Decide / einer Handle ändern | `DecideRueckgabe` / `HandleRueckgabe` |
| Fähigkeit verbinden/lösen | `ParameterAbgleich` (nur die Parameter hinter dem Kontext) |
| Store, Fähigkeit, Implementierung | `Scaffolder.LeseseitenDateien`, `SchnittstellenMergen`, `StoreImplDatei`; Signatur: `MethodenSignatur` |
| Fluss-Knoten | neu `Scaffolder.FlussDatei`; bestehend `FlussKnoten` (unveränderte Knoten bleiben wörtlich) |
| Prozess-Regel | neu `Scaffolder.SagaDatei`; bestehend `ProzessRegeln` (nur der Ausdruck der `Regeln`-Property) |
| Funktion + C#-Implementierung | `Scaffolder.FunktionsDatei`/`FunktionsImplDatei`; `FunktionsSignatur`, `Konstruktor` |
| Dienst + Implementierung | `Scaffolder.DienstDatei`/`DienstImplDatei`; `DienstMethoden`, `Konstruktor` |
| Akteur `IDarf` | `AkteurBefugnisse` (Basisliste, auch Entziehen) |
| Zusage/Kenntnis im Vertrag | `AkteurVertrag` (nur der Haupt-Teil) |
| Client-Basisliste | `ClientVertrag` |
| Umbenennen | `Umbenennen.Async` (Roslyn-Renamer) |
| Löschen | `Gesten.Loeschen` an `_ort` + `_teile` |
| Rumpf ✎ | `Rumpf.Ersetze` (C#) bzw. `PythonSync` |
| Umgebungs-Werte | `Umgebungen.Schreibe` (direkt, keine Geste) |

### A.3 Editor-Regeln

- **Logik-Element-Muster:** alles mit Logik ist ◀ Eingang · Ausgänge ▶ (OneOf) · ⚙ Fähigkeiten · Rumpf — Decide/Apply, Handles, Store-Fns,
  Funktion (Signatur nur an ihrer Karte), Fluss-λ, Prozess-Regel-λ, Zusage/Kenntnis eines Akteurs (Python-Rumpf der Client-Implementierung).
- **Deklariert nur an der eigenen Karte, verbunden nur per ⊕.** Keine Zuweisungs-Dropdowns, keine Typ-Definition in einem fremden Knoten; „＋“ in
  der Zeile legt eine neue Karte an und verbindet sie. So wenig Karten wie möglich: was aus einer Signatur folgt, wird abgeleitet.
- **⊕-Modus:** passende Karten leuchten, Anklicken verbindet oder löst; der Katalog bietet auch nicht geladene passende Typen an („＋ neu“ legt
  eine Karte der Sorte an). Der Modus endet per Esc, ⊕ oder Klick ins Leere; die Kamera bewegt sich nur, wenn kein Kandidat im Bild ist.
  Abgelehnte Ziele zeigen „✕ Regel“ mit Regelname.
- **Leitsatz der Grammatik:** Wenn es sich verbinden lässt, kompiliert es. Die Grammatik darf enger sein als der Compiler, nie weiter.
- **Garantie sichtbar:** durchgezogen = durabel, gestrichelt = verlierbar (Inv. 6).
- **Domänen-Zugehörigkeit steht über allem** und kommt aus dem Graphen, nie aus dem Namespace. Ablauf-Bausteine stehen im Rahmen der Domäne, auf
  die sie wirken. Verarbeitung ist immer eine Katalog-Funktion, nie ein Rumpf am Rand.
- **Render-Regeln:** Transform sofort, Abgeleitetes im nächsten Frame, Culling, kein Layout-Lesen beim Verschieben, kein Dauer-`will-change`.

### A.4 Regeln: Grammatik → Build

Jede Grammatik-Regel (`DomainEditor/Grammatik.cs`, Liste: `--grammatik`) nennt ihr Gegenstück im Build (Analyzer, Generator-Diagnose, Compiler,
Boot-Guard oder „offen“). Die Regeln, die früher nur der Validator prüfte, sind Analyzer: Fluss CQRS073–078, Modell CQRS079–083 (u. a. MISSING-APPLY,
ENUM-ZERO, GR-GARANTIE, GR-ZUSTAND), Gesamtbild CQRS084–088 (GR-QUERY, GR-MODUL-*, GR-GETRAGEN, GR-ZYKLUS), Dienst CQRS089–092, Client-Code
CQRS093–097. Offen (ohne Build-Gegenstück) sind die Regeln der Trigger-/Selbst-Welt und GR-INGRESS-EINDEUTIG. Die vollständige Liste aller
Diagnose-IDs ist der Code selbst.

### A.5 LLM

Slots, Kontexte und Konsole: `konzept-llm-minimalkontext.md`. Es gilt `claude -p` nur über das Abo, nie die API. **🤖 füllen** am Rumpf läuft als ein
Durchgang (`POST /api/llm/ausfuehren`): Prompt aus Graph-Skelett + Slot-Teil + Auftrag, bis zu drei Runden; Prüfung im warmen Workspace
(`Arbeitsbereich.PruefeProbeweiseAsync`: Datei mit Kandidat, Compiler + Analyzer des Projekts, nur neue Fehler; Lambdas eigens, Python per Parser);
geprüft → schreiben → Laufzeit-Projekt bauen → neu lesen. Slot-Arten: `decide`, `apply`, `projektion`, `reaktion`, `reader`, `pipeline`, `store`,
`funktion`, `funktionpy`, `fluss`, `saga`, `zusage`. Die Kontexte entstehen noch kalt (`GraphExtractor --kontexte`, ~1 min). Die Übernahme ist eine
Rumpf-Geste wie ✎ (§8.4: Verlauf, ↶, Branch-Sperre). Fachlich geprüft wird ein 🤖-Rumpf noch nicht — Plan: Beispiele je Slot als Testcode, die Schleife
„baut → regelkonform → Beispiele grün“ (`konzept-llm-minimalkontext.md` §11, `docs/13` LLM-1).

### A.6 Code-Fakt je Primitive

| Primitive | Code-Fakt |
|---|---|
| Aggregat | `IState`, für das `IDecider<S>`/`IApplier<S>` existiert |
| Command | `ICommand` (+ `ICreationCommand`) |
| Event / Ablehnung | `IEvent` / `ITransientEvent` (als Decide-Ausgang) |
| Wertobjekt | `IWertobjekt` oder Record ohne Rolle |
| Enum | `enum` |
| Konfig-Record | Record im öffentlichen Konstruktor einer Implementierung |
| Query / Antwort | `IQuery` / `IQueryResponse` |
| ReadModel | `IReadModel` (+ `IGeteiltesReadModel`) |
| Store-Fähigkeit / Bündel | Interface `: IWriteStore`/`: IReadStore` mit genau einer Funktion / `: IStore, …` |
| Projektion / Reaktion | `ISubscriber` (+ `IPullSubscriber`); Reaktion, wenn ein Handle Commands liefert |
| Reader | `IReader<P>` |
| Prozess | `IProzessDefinition` + DSL-Verben per Symbol |
| Fluss | `IPipeline` + `Fluss`-Property + DSL-Verben per Symbol |
| Funktion / Auftrag | Interface `: IFunktion` / Record `: IAuftrag<F>`; Implementierung = implementierende Klasse |
| Dienst | Interface `: IDienst`; Implementierung = implementierende Klasse |
| Quelle | Record mit Treiber-Basis (`Treibervertrag`) |
| Akteur | `IAkteur` (`IMensch`/`IMaschine`/`IKi`) + `IDarf<T>` |
| Akteur-Vertrag | `IAkteurVertrag<A>` (Teile erlaubt); `Auf` mit Ausgabe = Zusage, `void Auf` = Kenntnis |
| Client | `IClientVertrag` (+ `ISendet<C>`, `IFragt<Q>`); C#-Client `[assembly: IstClient<IX>]` |
| Python-Implementierung | Klasse erbt eine Basis aus `generated/vertraege.py` bzw. `generated/funktionen.py` |
| Code-Karte | Typ-Deklaration ohne Abdeckung durch eine Karte, oder unaufgelöste Basis |

### A.7 Karten-Ids im Editor

Die Board-`_id` ist die Wirker-Id bzw. ein natürlicher Schlüssel; die Editor-Id ist Präfix + `_id` (`GraphExtractor.Kern/Sicht/Sicht.cs`).

| Präfix | Karte | | Präfix | Karte |
|---|---|---|---|---|
| `rec:` | Record (alle Arten) | | `fl:` / `fs:` | Fluss / Fluss-Knoten |
| `agg:` / `st:` | Aggregat / State | | `akt:` / `auf:` | Akteur / Zusage |
| `dec:` / `app:` | Decide / Apply | | `cl:` | Client |
| `enum:` | Enum | | `rm:` / `sto:` / `fn:` | ReadModel / Store / Store-Fn |
| `saga:` / `tr:` | Prozess / Regel | | `prj:` / `rdr:` / `rk:` | Projektion / Reader / Reaktion |
| `fk:` | Funktion | | `hd:` | Handle |
| `dn:` | Dienst | | `code:` | Code-Karte |
| `rp:` | Rumpf-Kachel (nur Editor) | | `host:` | Host-Abschnitt (nur Editor) |
