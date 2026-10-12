# Zielbild: Die Werkbank

> **Status 2026-10-12: verbindliches Zieldokument, noch nicht umgesetzt.** Es fasst die Entscheidungen vom 2026-10-11/12 zusammen:
> Quellen als Treiber, Umgebungen statt Simulation, Spur, ein entschlackter Editor, Umfang. Wo ältere Dokumente widersprechen, gilt
> dieses. Widersprüchliche Konzepte wurden gelöscht bzw. gekürzt (§15). Ist-Dokumente (`01`–`13`) beschreiben den heutigen Code
> und werden mit der Umsetzung nachgezogen.
>
> **Umsetzungsplan:** `docs/plan-werkbank-umsetzung.md` (Ist-Befund, Lücken, Arbeitspakete).
>
> **Für den umsetzenden Agenten:** §2 (Leitsätze) und §14 (Leitplanken) sind Pflicht. Die Phasen in §13 sind nach Abhängigkeit
> geordnet und haben je eine messbare Abnahme. Entschiedenes steht in §12 und wird nicht neu verhandelt.

---

## 0 · Kurzfassung

Der Domänen-Editor wird zur **Werkbank**: Auf **einer** Oberfläche wird entworfen, Code erzeugt, gebaut, gestartet, durchgespielt und
nachvollzogen. Es gibt keine Simulationsschicht und kein eigenes Board-Modell mehr.

- **Der Code ist das Modell.** Jede Geste im Editor schreibt sofort Code. Das Board wird immer aus dem Code gelesen. Was nicht baut,
  ist eine rote Karte mit dem Compiler-Fehler.
- **Durchspielen heißt: das echte System laufen lassen.** Der echte Host läuft in der Umgebung `sim` gegen eine eigene, echte Datenbank.
  Simulation und Betrieb unterscheiden sich nur in der Konfiguration, nie im Code.
- **Der Weg jedes Events wird mitgeschrieben** (Spur: `ursache`/`wirker`/`stand`). Der Editor legt ihn über das Board.
- **Quellen sind fest codierte Treiber** (zuerst der Filewatcher). Die Domäne benennt eine Quelle nur als Typ, alles andere ist Konfig.
- **Das Backend** (Event-Store, Redis, Cluster, Funktions-Laufort …) wird im Editor je Umgebung eingestellt.
- **Umfang:** Der Editor baut alles, was eine Anwendung ausmacht. **Er baut keine GUIs** (Clients werden nur vorbereitet) und
  **ignoriert Schema-Evolution.**

---

## 1 · Umfang und Grenzen

| Bereich | im Editor |
|---|---|
| Domäne: Aggregate, Commands, Events, Ablehnungen, Value Objects, Enums, Decide/Apply | **bauen** |
| Abläufe: Prozesse, Flüsse (Pipelines), Katalog-Funktionen (C#-Rümpfe, Python-Rümpfe) | **bauen** |
| Leseseite: Projektionen, Stores, Reader, Queries/Responses | **bauen** |
| Akteure, Akteur-Verträge, Zusagen (Python-Rümpfe der Worker) | **bauen** |
| Quellen: Domänen-Typ + Treiber-Konfig | **bauen** |
| Domänen-Hilfscode, den der Extractor nicht strukturiert erkennt | **bauen** als Code-Karte (§9) |
| Backend und Umgebungen: Event-Store, Redis, Cluster, Ports, Identitäten, Funktions-Laufort, Uhr | **konfigurieren** |
| Bauen, Starten, Durchspielen, Spur | **ja** |
| Clients | **nur vorbereiten:** Client-Vertrag (`IClientVertrag`, `ISendet`/`IFragt`/`Auf`) und die daraus generierten Schnittstellen. Die GUI entsteht außerhalb. |
| GUIs (Blazor-Views, Intents, Client-Stores) | **nein** |
| Schema-Evolution (`IUpcast`) | **nein**, wird ignoriert. Passen Sim-Daten nicht mehr, wird die Sim-DB zurückgesetzt (§4.6). |
| Framework: Generatoren, Analyzer, `Infrastructure`, **Treiber** | **nein:** Katalog, wählen und konfigurieren, nicht schreiben |
| Deploy nach Prod | **nein**. Prod ist im Editor nur lesbar (Spur, Vorgänge). |

---

## 2 · Leitsätze (bindend)

1. **Der Code ist die einzige Wahrheit.** Es gibt kein gespeichertes Board-Modell. Gespeichert wird nur eine **Ansichts-Datei**
   (Positionen, geladene Domänen, Akteur-Reihenfolge).
2. **Der Editor ist eine dumme View**, wie ein Read Model. Alle Ableitungen (Akteur-Mengen, Zugehörigkeit, Kette, Kanten, „was passt an
   diesen Port?“) rechnet der Server in C#. Der Browser zeichnet nur.
3. **Keine Simulationsschicht.** Durchgespielt wird das echte System. Was der Editor zeigt, ist passiert.
4. **Umgebungen unterscheiden sich nur in Konfig.** **Struktur ist Code** (was existiert), **Auswahl und Werte sind Konfig** (was aktiv
   ist, mit welchen Parametern).
5. **Treiber sind fest codiert.** Die Domäne benennt Quellen, schreibt aber keine Mechanik.
6. **Der Weg folgt aus dem Log** (Inv. 1). Die Spur ist Teil des Logs (Header). Verlierbares ist eine best-effort-Ergänzung, die das System
   selbst nie liest.
7. **Vollständigkeit per Konstruktion.** Jeder Typ der Editor-Projekte gehört zu einer Karte, strukturiert oder als Code-Karte.
8. **Je Absicht ein Weg** in der Bedienung.

Alle sechs Invarianten (CLAUDE.md) gelten unverändert. Insbesondere gilt Inv. 3 (Routing über Typen) auch für Quellen: Sie sind Typen,
keine Namen aus der Konfig. Inv. 4 (keine Reflection) gilt auch für Treiber-Fabriken und Konfig-Bindung, die beide generiert werden.

---

## 3 · Quellen = Treiber + Konfig (zuerst: Filewatcher)

### 3.1 Begriffe

| Begriff | Was | Wo | Wer schreibt |
|---|---|---|---|
| **Treiber** | die Technik: Abfrage-Schleife, Fertig-Erkennung, Fehlerbehandlung, Health | Framework (`Infrastructure/Quellen/Treiber/…`) | wir, einmal je Technik |
| **Quelle** | eine konfigurierte Instanz eines Treibers | Domäne: ein Einzeiler (Typ); Werte: Umgebungs-Datei | Editor |
| **Quell-Nachricht** | was die Quelle meldet, mit Kennung (genau einmal) | ist der Quellen-Typ selbst | — |
| **Einstieg** | wo ein Fluss beginnt | `p.Quelle<T>()` | Editor |

Eine Quelle **hat keinen Rumpf.** Die Bedeutung („welches Bildpaar ist diese Datei?“) ist ein Fluss-Schritt, also eine Funktion.
**Ein Treiber liest nur.** Er verschiebt, löscht und schreibt nichts in der Außenwelt. Aufräumen am Ende eines Vorgangs ist ein
Funktions-Knoten im Fluss.

Clients sind **keine** Treiber. Sie bleiben ein eigener Weg hinein (Akteur-Konzept). „Client meldet Quell-Nachricht“ ist offen (§12.2).

### 3.2 Code-Form

```csharp
// Framework (Abstractions): was jeder Datei-Treiber meldet — vom Treiber befüllt, in der Domäne nur benannt.
public abstract record DateiGemeldet : IQuellNachricht
{
    public required string Name { get; init; }              // Dateiname
    public required string Pfad { get; init; }              // absolut, wie der Treiber-Host ihn sieht
    public required string RelativerPfad { get; init; }     // relativ zum konfigurierten Verzeichnis
    public required long Groesse { get; init; }
    public required DateTimeOffset GeaendertAm { get; init; }
    public required DateTimeOffset ErkanntAm { get; init; }
    public required string Kennung { get; init; }           // vom Treiber nach Konfig gebildet (§3.5)
}

// Domäne: die Quelle ist ein Typ, sonst nichts. Treiber = Datei (folgt aus dem Basistyp).
public sealed record DateiErkannt : DateiGemeldet;
public sealed record KameraSystem : IMaschine, IDarf<DateiErkannt>;     // in wessen Namen (unverändert)

// Fluss: unverändert in der Form.
var datei = p.Quelle<DateiErkannt>();
```

Regeln (neuer Analyzer, Nummern ab CQRS070):
- Ein Quellen-Typ ist ein `sealed record`, der von einer Treiber-Basis erbt und **keine eigenen Member** hat (der Treiber könnte sie
  nicht befüllen).
- `IQuelle<T>` direkt zu implementieren ist nur in Treiber-Assemblies erlaubt, nie in einer Domäne.

### 3.3 Generiert (Inv. 4)

Ein `QuellenGenerator` findet jeden `sealed record X : <Treiber-Basis>` und erzeugt:
- `GeneratedQuellen.Alle`: je Quelle der Typ, der Treiber, der Konfig-Abschnitt (= Typname) und eine Fabrik
  `daten => new DateiErkannt { Name = …, … }`, damit der Treiber ohne Reflection baut;
- die Wirker-Id `quelle:<Typ>` (§7).

Die Konfig wird über den **Configuration-Binding-Source-Generator** (.NET 9, `EnableConfigurationBindingGenerator`) gebunden, nicht
über den reflexionsbasierten Binder.

Der Proto-Generator muss **geerbte `init`-Properties** in den DTO aufnehmen. Er liest heute Konstruktor-Parameter; das ist in P1 zu
prüfen bzw. zu ergänzen, sonst bricht der `DtoMapperGenerator`.

### 3.4 Konfig (Abschnitt der Umgebungs-Datei, §4.1)

```json
"Quellen": {
  "DateiErkannt": {
    "Pfad": "/mnt/kamera/out",
    "Muster": ["*.tiff"],
    "Unterordner": false,
    "Erkennung": "Abfrage",
    "Intervall": "00:00:01",
    "Fertig": { "Art": "Stabil", "Fenster": "00:00:01" },
    "Kennung": "NameGroesse",
    "Laufmodus": "EinmalImCluster"
  }
}
```

Die Treiber-Wahl steht **nicht** in der Konfig; sie ist ein Code-Fakt (Basistyp).

**Start-Prüfungen:**
- Jede Quelle braucht einen Abschnitt; ein Abschnitt ohne Typ ist ein Fehler.
- Unbekannte Felder sind ein Fehler.
- Ein beim Start nicht erreichbares Verzeichnis ist nur eine Warnung; der Treiber versucht es weiter.

**Host:** `AddQuelle<…>` und `FileWatchConfig` entfallen. Die Quellen kommen aus `GeneratedQuellen` und der Umgebung (§4.2).

### 3.5 Der Datei-Treiber

**Ablauf:**
1. **Start:** Er listet alle passenden Dateien und fragt **gebündelt** den Log, welche Vorgänge zu diesen Kennungen schon existieren
   (neue Repository-Methode, z. B. eine Abfrage auf `mt_streams` mit `id = any(@ids)`, je ~500 Ids). Nur Unbekannte kommen in die
   Wartezone. Es gibt keinen eigenen Treiber-Stand; die Wahrheit ist der Log.
2. **Schleife:** Ein Weckruf löst einen Scan aus. Der Weckruf ist das Intervall oder, bei `"Erkennung": "EreignisUndAbfrage"`, ein
   FileSystemWatcher-Ereignis. Das Ereignis weckt nur, der Scan entscheidet (Inv. 2). Auf CIFS gilt nur `Abfrage`.
3. **Fertig-Erkennung:**
   - `Stabil`: Größe und Änderungszeit über das Fenster unverändert;
   - `Marker`: Die Datei gilt als fertig, wenn `<datei><Endung>` existiert, z. B. `.done`.
4. **Melden:** nacheinander über den bestehenden `QuellEingang` (genau einmal je Kennung, Weckung der Flüsse). Die nächste Meldung kommt
   erst, wenn die vorige im Log steht (Rückstau). „Schon gemeldet“ merkt sich der Treiber in einer begrenzten Menge im Speicher;
   verschwundene Dateien fallen heraus.
5. **Fehler:** Ist das Verzeichnis nicht lesbar, geht der Treiber in den Zustand „gestört“ (`/health`) und versucht es mit wachsenden
   Pausen erneut. Nichts geht verloren, denn der nächste Scan holt alles nach.

**Kennung (Konfig `Kennung`):**

| Wert | Bildung | Folge |
|---|---|---|
| `NameGroesse` | `{Name}\|{Groesse}` | eine ersetzte Datei gleicher Größe wird nicht erneut erkannt |
| `PfadGroesseZeit` | `{RelativerPfad}\|{Groesse}\|{GeaendertAm:O}`, Default für neue Quellen | eine ersetzte oder neu geschriebene Datei startet einen neuen Vorgang |

Die Vorgangs-Id ist `MD5("quelle:" + Typ.FullName + ":" + Kennung)` (`QuellVorgang.StreamFür`). **Keine Abwärtskompatibilität**
(Vorgabe 2026-10-12): Alte Vorgänge und alte Payloads werden nicht migriert; auch `DateiErkannt` nutzt den Default `PfadGroesseZeit`.

**Laufmodus:**
- `EinmalImCluster` (Default): Eine Cluster-Identität `quelle/<Typ>` hält die Schleife und wandert bei Ausfall eines Knotens mit.
- `JederKnoten`: für lokale Verzeichnisse je Maschine.

### 3.6 Umzug des Bestands

| Heute | Danach |
|---|---|
| `Domain/ImagePair/Bildeingang.cs`: `record DateiErkannt(Pfad, Dateiname, DateigroesseBytes, ErkanntAm) : IQuellNachricht` | `sealed record DateiErkannt : DateiGemeldet;` (Felder `Name`, `Groesse`, `ErkanntAm` …); Fluss-Lambdas angepasst |
| `Domain.Pipeline/ImageProcessing/DateiQuelle.cs`, `FileWatchConfig.cs` | gelöscht; die Mechanik ist im Datei-Treiber |
| `Domain.Pipeline/Infrastructure/DomainPipelineExtensions.cs` (FileWatch-Teil) | gelöscht |
| `Host.Grpc/Program.cs`: `AddQuelle<DateiErkannt, DateiQuelle>()` | entfällt; `MapQuellWebhook<DateiErkannt>` bleibt vorerst |
| `WatchPath` aus appsettings/Env | `Quellen:DateiErkannt:Pfad` in der Umgebungs-Datei |

---

## 4 · Umgebungen und Backend

### 4.1 Die Umgebungs-Datei

Je Umgebung gibt es **eine** Datei `umgebungen/umgebung.<name>.json`; Umgebungsvariablen überschreiben wie gewohnt. Geheimnisse
stehen nur als Verweis darin (`"Passwort": "${env:CQRS_DB_PW}"`).

| Abschnitt | Inhalt |
|---|---|
| `Umgebung` | Name, Art (`sim` \| `prod`) |
| `EventStore` | Postgres: `verwaltet` (Docker, vom Editor) \| `vorhanden` (Verbindung); Datenbankname |
| `VersionIndex` | Redis: `aus` \| `verwaltet` \| `vorhanden`; Schlüssel-Präfix |
| `Cluster` | `Einzelknoten` \| `Consul` (Knoten 1–3 lokal); **Cluster-Name** |
| `Schreibpfad` | STJ an/aus, Group-Commit, Batch-Größe |
| `Transport` | gRPC-/HTTP-Ports, TLS |
| `Identitaeten` | Token → Akteur-Menge |
| `Quellen` | je Quelle die Treiber-Konfig (§3.4) |
| `Funktionen` | je Funktion der Laufort: `Host` (Implementierung, Slots) \| `Extern` (Python) \| `Hand` (Editor als Ausführer) |
| `Uhr` | Versatz (nur bei Art `sim`) |

### 4.2 Host

`Host.Grpc/Program.cs` schrumpft auf das Laden der Umgebung (`--umgebung <name>`) und **eine** Zeile `builder.AddCqrs(umgebung)`. Was
existiert, liefern die generierten Tabellen (Aggregate, Flüsse, Funktionen, Quellen, Wire …); was aktiv ist, sagt die Umgebung. Alle
Implementierungen sind einkompiliert, sodass ein Umgebungswechsel nie ein Code-Wechsel ist.

**Funktions-Laufort als Konfig:** `AddFunktion<F, Impl>` / `AddExterneFunktion<F>` wandern aus dem Code in den Abschnitt `Funktionen`. Die
Implementierungen sind Code-Fakten (Klassen, die das Interface implementieren), die Wahl trifft die Umgebung, und der Boot prüft sie
gegen die generierte Tabelle (Impl existiert; eine Funktion mit Lese-Fähigkeit läuft nie extern, wie heute).

### 4.3 Trennung der Umgebungen

| Ressource | `prod` | `sim` | Warum |
|---|---|---|---|
| Postgres | DB `cqrs` | DB `cqrs_sim_<branch>` | die Wahrheit, getrennt |
| Redis | Präfix `prod:` | Präfix `sim:<branch>:` | Index nicht autoritativ, darf nicht mischen |
| Cluster | Consul, Name `cqrs` | Einzelknoten, Name `cqrs-sim-<branch>` | **kritisch:** sonst treten Sim-Actors dem Prod-Cluster bei |
| Ports | wie deployt | eigene | Clients müssen wissen, wohin |
| Quellen | echte Pfade | Sandbox (`~/cqrs-sim/<branch>/<Quelle>/`) oder Mitlesen am echten Pfad | Treiber lesen nur, Mitlesen ist gefahrlos |
| Funktionen | Host/Extern | Host/Extern/Hand | Mensch als Ausführer |
| Uhr | Versatz gesperrt (0) | verstellbar | Fristen und Zeitlimits durchspielen |

**Umgebungs-Siegel:** Jede Datenbank hat eine Tabelle `umgebung(name, art)`. Der Host startet nur, wenn seine Umgebung zum Siegel
passt; dasselbe gilt für den Cluster-Namen. Damit kann ein Sim-Host nie in die Prod-DB schreiben und umgekehrt.

### 4.4 Uhr

`IDbClock.JetztAsync` liefert künftig `now() + versatz`, wobei der Versatz in der Datenbank steht. In einer DB mit Siegel `prod` ist er
immer 0 und nicht änderbar. Es betrifft nur Code, der die Zeit über `IDbClock` liest (Fristen, Zeitlimits, Warte-Knoten). Treiber-
Intervalle laufen in Echtzeit.

### 4.5 Infrastruktur

Aus der Umgebung **generiert** der Editor ein Docker-Compose-Projekt `cqrs-<umgebung>`: Postgres immer, Redis nur bei `verwaltet`,
Consul nur bei Cluster = Consul. Er startet, stoppt und überwacht die Container und zeigt ihren Zustand in der Kopfzeile. Eine Umgebung
mit `vorhanden` verwaltet er nicht.

### 4.6 Die Sim-Datenbank

| Aktion | Mechanik |
|---|---|
| Neu / Zurücksetzen | DB löschen, aus der leeren Schema-Vorlage neu anlegen, Siegel setzen |
| Stand speichern | Host anhalten → `CREATE DATABASE <db>_stand_<x> TEMPLATE <db>` → weiter |
| Stand laden | Host anhalten → `<db>` aus dem Stand neu anlegen → starten |
| Prod-Stand übernehmen | `pg_dump`/`pg_restore` in eine neue Sim-DB, Siegel auf `sim` |

Wenn sich Code ändert, während Daten in der Sim-DB liegen:
- **Projektion oder Reader geändert:** Betroffene Projektionen werden beim Start neu aufgebaut (`IProjectionTracker`-Reset).
- **Aggregat-Logik geändert:** Snapshots werden über den Code-Hash verworfen.
- **Prozess oder Fluss geändert:** Voll-Fold bei stalem `RegelHash` (gibt es).
- **Event-Typ inkompatibel geändert** (Feld entfernt/umbenannt, Typ umbenannt): **Die Sim-DB wird zurückgesetzt.** Gespeicherte Stände
  älterer Stände werden als ungültig markiert. Schema-Evolution ist kein Ziel (§1).

---

## 5 · Der Kreislauf ⟳ (Entwurf → Code → Lauf)

| Schritt | Was passiert | Wo ein Fehler erscheint |
|---|---|---|
| 1 Schreiben | Jede Geste hat schon Code bzw. Umgebungs-Datei geschrieben (§8.2) | — |
| 2 Bauen | Codegen-Prepass (`codegen.sh`: Proto + STJ-Kontexte), Proto-Regenerierung, `dotnet build` des Laufzeit-Hosts mit allen Generatoren und Analyzern, Python-Basen | **an der Karte** (Datei:Zeile → Karte), Liste unten |
| 3 Daten prüfen | Vergleich alter mit neuem Stand (§4.6) → Projektionen neu, Snapshots verwerfen oder Sim-DB zurücksetzen (mit Rückfrage) | Dialog |
| 4 Infra | fehlende Container starten, Siegel prüfen | Host-Rahmen-Karte rot |
| 5 Starten | Host-Prozess(e) `--umgebung <name>` ersetzen; Python-Worker externer Funktionen neu verbinden | **Boot-Guards an der Karte** (Wire-Check, Azyklizität, Bindungen, Quellen) |
| 6 Synchron | Board neu lesen; Stand-Hash von Board, Build und Lauf gleich; die Spur setzt am Cursor fort | — |

Wer weiterentwirft, während das System läuft, sieht „● Entwurf ≠ Lauf (n Änderungen)“ und startet mit ⟳ neu.

---

## 6 · Durchspielen: nur echte Eingänge

| Eingabe | Im Editor | Echter Weg dahinter |
|---|---|---|
| Datei | Drag & Drop auf die Quellkarte (oder Sandbox-Ordner öffnen) | der Editor-Server legt die Datei in den Sandbox-Ordner, der echte Treiber meldet sie |
| Als Akteur handeln | Konsole unten: Token/Akteur wählen, Command/Query als Formular (aus den Record-Feldern) | Editor ist **Client** mit Sim-Token → Akteur-Tor → Aggregat bzw. Reader |
| Funktion ohne Implementierung | offener Auftrag an der Funktions-Karte (⧗ n), Panel: Ergebnis-Fall wählen, Felder füllen | Editor ist **Ausführer** (`Laufort: Hand`) über das bestehende Pull-Protokoll |
| Python-Funktion | „▶ Worker starten“ an der Karte (oder auf einen externen warten) | echter Worker über gRPC |
| Zeit | ⏩ in der Kopfzeile | Uhr-Versatz (§4.4) |
| Nochmal | Stand laden, Eingabe wiederholen | echte Daten, echte Kennung |

**Haltepunkte gibt es bewusst nicht**: Sie bräuchten Sonderlogik in Dirigent und Konsumenten. Ein menschlicher Ausführer an der
Funktion leistet für Funktionen dasselbe auf echtem Weg.

---

## 7 · Spur und Visualisierung

### 7.1 Spur-Daten

Jeder Log-Eintrag bekommt vier Header, vom Framework gesetzt und unsichtbar für den Fachcode (Inv. 5):

| Header | Inhalt |
|---|---|
| `ursache` | der Log-Eintrag, der ihn ausgelöst hat: `Stream:Version` |
| `wirker` | der Baustein, der geschrieben hat: **dieselbe Id wie die Karte im Editor** |
| `ueber` | der Absender des Commands bzw. Auftrags, den der Wirker verarbeitet hat (Fluss-Knoten, Regel, Reaktion, Akteur) — der Weg über die Karten zwischen Ursache und Wirker (ergänzt in P6) |
| `stand` | Hash des Builds |

- **`ursache` reist wie heute der Akteur** (`ImAuftrag`): Der generierte Dispatch setzt sie um jeden Handle bzw. Fluss-Schritt, der
  `CommandEmitter` stempelt sie in den Command-Envelope, das Aggregat schreibt sie an seine Events. Damit ist der Kausalgraph auch über
  Commands hinweg lückenlos. `CausationId` bleibt unangetastet, sie trägt den Prozess-Fold.
- **`wirker` ist generiert, nie handgeschrieben.** Das Format steht an **einer** Stelle, die Generatoren und Extractor teilen (verlinkte
  Quelldatei, netstandard2.0-tauglich):
  - `decide:<Aggregat>/<Command>`
  - `fluss:<Pipeline>#<Knoten>`
  - `prozess:<Prozess>#<Regel>`
  - `funktion:<IFunktion>`
  - `quelle:<Typ>`
  - `projektion:<Typ>`
  - `reaktion:<Typ>`
  - `akteur:<Akteur>` (von außen)

  `--check` prüft: jede Wirker-Id hat eine Karte und umgekehrt.
- **Verlierbares** (Ablehnung, ⏳, transientes Event, Weckruf) kommt in die Tabelle `spur_hinweis`: best-effort, vom System nie gelesen, mit
  Aufbewahrungsfrist.
- **Spur-Projektion** (nur Umgebungsart `sim`): baut je Vorgang einen Graphen (Knoten = Log-Einträge, Kanten = `ursache`). Welche
  Projektion welches Event verarbeitet hat, kommt aus ihrer Tracker-Marke. In `prod` wird der Graph bei Bedarf über die `CorrelationId`
  gebaut (Index auf den Header).

### 7.2 Synchron halten

- **Daten:** Der Host meldet per SSE nur „neu bis `seq`“ (globale Marten-Sequenz). Der Editor-Server holt über die **Einblick-API** des
  Hosts (`GET /einblick/spur?ab=<seq>`, `/einblick/vorgaenge`, `/einblick/auftraege`, nur lesend) nach. Ein verlorenes Signal oder
  ein Neustart schadet nicht, denn es wird ab dem Cursor nachgelesen (dasselbe Muster wie Signal plus Pull im Kern).
- **Identität:** Board, Build und Lauf tragen denselben `stand`. Spuren älterer Stände erscheinen grau. Existiert ihre Karte nicht mehr,
  zeigen sie „⚠ Karte gibt es nicht mehr“.

### 7.3 Ansichten im Editor

- **Vorgänge** (untere Leiste): Liste mit Filter nach Quelle, Akteur, Aggregat-Id und Zustand (läuft · wartet · fertig · gescheitert).
- **Spur-Überlagerung** auf dem Board für den gewählten Vorgang:
  - gegangene Kanten nummeriert ①②③ mit Zeit, nicht gegangene Ausgänge blass;
  - ⧗ pulsiert, ✕/⏳ rot, Verlierbares gestrichelt;
  - Klick auf eine Nummer → Panel mit Payload, Akteur, Knoten, Stand, „↤ verursacht von / ↦ bewirkte“.
- **Zeitleiste** (untere Leiste): Bahnen je Baustein bzw. Stream. Der Schieber zeigt das Board zum Zeitpunkt t; das ist Abspielen der
  Aufzeichnung, nie erneutes Ausführen.
- **Live-Puls**, wenn kein Vorgang gewählt ist: Zähler und „zuletzt“ an jeder Karte, Kantendicke nach Menge, Stau (offene Aufträge,
  Rückstau einer Quelle) und nie benutzte Kanten.
- **Fragen an jedem Ding:** „↤ Woher kommt das?“ / „↦ Was hat das bewirkt?“, auch an einer Read-Model-Zeile.

---

## 8 · Der Editor danach

### 8.1 Oberfläche

```
┌───────────────────────────────────────────────────────────────────────────────────────────────────────┐
│ Umgebung: sim-main ▾ · ● Build grün (a3f2) · ● Läuft (1 Knoten) · Postgres ● Redis ● · ⟳ · ⏩ · 📸 · 🔎 │
├──────────────────────────────────────────────────────────────────────────────────────┬────────────────┤
│ Board: Domänen-, Akteur-, 📜 Vertrags-, 🔌 Client-, ⛓ Fluss-Rahmen (wie heute)          │ Panel           │
│        + 🖥 Host-Rahmen rechts neben den Client-Rahmen                                  │ (Bearbeiten,    │
│        + Spur-Überlagerung                                                             │  Werte, Payload)│
├──────────────────────────────────────────────────────────────────────────────────────┴────────────────┤
│ Vorgänge │ Zeitleiste │ Konsole (als Akteur) │ Aufträge │ Build-/Boot-Meldungen                         │
└───────────────────────────────────────────────────────────────────────────────────────────────────────┘
```

Die Ergänzungen sind additiv: Das Spalten-Layout bleibt, bearbeitet wird nur im Panel, der Zoom zeigt nichts anderes.

**🖥 Host-Rahmen:** Karten für Event-Store, Version-Index, Cluster, Schreibpfad, Transport, Identitäten, Quellen (Übersicht), Funktionen
(Laufort), Uhr. Die Werte hängen von „Umgebung ▾“ ab. **Ein generischer Formular-Renderer** zeichnet jedes Panel aus dem Konfig-Schema
(Record → Felder, Enums → Auswahl); es gibt keine handgebauten Formulare je Karte. Die Quellkarte im Akteur-Rahmen zeigt zusätzlich
„Werte in <Umgebung>“.

### 8.2 Schreiben und Lesen

- **Jede Geste ist ein Patch am Code** (bzw. an der Umgebungs-Datei), ausgeführt auf dem Server über einen **warmen Roslyn-Workspace**.
  Anlegen nutzt den Scaffolder/`DateiSchreiber`; Umbenennen den Roslyn-`Renamer` über alle Dateien; Signatur ändern und Löschen gehen
  samt Verweisen. Danach liest der Server inkrementell neu ein und schickt das Board mit allen Ableitungen. Ziel: unter 300 ms je Geste.
- **↶ Rückgängig** = den letzten Patch zurücknehmen. Ein Mechanismus für alles.
- **Unfertiges ist Code**, der nicht baut: Die Karte ist rot mit dem Fehler. Der Extractor muss deshalb **Code mit Fehlern** lesen
  können; unauflösbare Typen erscheinen als ⚠ an der Karte.
- Der Editor arbeitet immer auf einem eigenen Branch bzw. Worktree (passt zu `cqrs_sim_<branch>`).

### 8.3 Ableitungen und Prüfung nur auf dem Server

- Der Server schickt mit dem Board: Akteur-Mengen, Zugehörigkeit, Stores, Kette, Kanten, Diagnosen. **Keine JS-Spiegel** mehr.
- **⊕-Verbinden fragt den Server** „Was passt an diesen Port?“; die Antwort kommt aus `DomainEditor/Grammatik.cs`. Die Grammatik bleibt
  **Daten** (für ⊕ und `--grammatik`), nicht ein zweiter Prüfer.
- **Prüfen = Bauen:** Was heute nur `DomainEditor/Validator.cs` prüft, wird Analyzer oder Boot-Guard. Danach entfällt der Validator.

### 8.4 Code am Rumpf statt Code-Knoten

- 📝 und 🤖 sind **keine eigenen Karten** mehr. Der Rumpf-Port zeigt den Code **direkt aus der Datei** (Ausschnitt); ✎ bearbeitet
  genau diesen Bereich.
- 🤖 ist eine **Aktion** am Rumpf („🤖 füllen“). LLM-Slots, Kontexte und Konsole bleiben (`docs/konzept-llm-minimalkontext.md`).
- Python-Rümpfe (Zusagen, externe Funktionen) gehen genauso, über `PythonSync`.

### 8.5 Bedienung

| Absicht | ein Weg |
|---|---|
| Etwas Neues beginnen | **Palette mit Ankern:** Aggregat · Fluss · Funktion · Quelle · Akteur · Client · Projektion |
| Etwas Zugehöriges anlegen | „＋“ am Port, an dem es hängt (Command am Decider, Event am Ausgang, Auftrag an der Funktion …) |
| Verbinden | ⊕ im Panel → passende Karten leuchten → anklicken |
| Fokussieren | **ein „Fokus“**: ohne Vorgang die statische Kette, mit Vorgang die echte Spur (ersetzt Slice, Ketten-Fokus, ◎, Inspector; die Darstellung von Prozess-Regeln als Kanten, `konzept-editor-pipelines.md` §13.6, bleibt) |
| Bauen und Starten | ⟳ |
| Domänen ein- und ausblenden | Filter (Ansichts-Datei) |

---

## 9 · Vollständigkeit: die Code-Karte

- **Editor-Projekte** sind Domäne, Abläufe, Leseseite, Akteure/Verträge, Quellen-Typen, die Umgebungs-Dateien und die
  Python-Rümpfe der Worker. Frontend-Projekte gehören nicht dazu.
- **Code-Karte:** Jeder Typ in einem Editor-Projekt, den der Extractor nicht als Konstrukt erkennt (Hilfsklassen wie `ImagePairName`,
  `SplitZuteiler`, Erweiterungen), wird eine Code-Karte (roher Code, Compiler-Fehler, 🤖) in seiner Domäne. Der Extractor rät nichts,
  er zeigt den Rest roh.
- **Gate:** `dotnet run --project GraphExtractor -- --check` meldet **„Code ohne Karte: 0“** (Pflicht) und den Anteil der Code-Karten
  (Kennzahl, soll sinken).

---

## 10 · Was entfällt (Konsolidierte Liste)

| Entfällt | Datei / Symbol | Ersetzt durch | Phase |
|---|---|---|---|
| Simulation im Editor | `SimHost/ModellSimulation.cs`, `SimHost/Musterwerte.cs`, `/api/editor/sim/*`, `/api/llm/simulieren`, JS-Abschnitt „SIMULATION“ (Animation, Abdeckung, Fluss-Schritt) in `GraphExtractor/HtmlPresenter.cs` | Umgebung `sim` + Spur | P8 |
| Laufwerke als Editor-Motor | Nutzung von `Cqrs.Testing/Fluss.cs` (`FlussLaufwerk`) und `Saga.cs` (`SagaLaufwerk`) durch SimHost | — (für Prüfstand-Tests bleiben sie, solange Tests sie nutzen) | P8 |
| Board-Modell als Wahrheit | `board-model.json`, localStorage-Modell, `mergeBoard`, `_herkunft`/„ungeschrieben“, Entwürfe im Board, `deSave`, Knopf „C# schreiben“, 👁 Vorschau, `/api/editor/board`, `/api/editor/write` (als Sammel-Schreiben) | Code + Ansichts-Datei + Patch je Geste | P7 |
| Paritätsprüfung Code⇄Board | Großteil von `GraphExtractor.Kern/ParitaetsPruefung.cs` | Code-ohne-Karte-Gate, Wirker⇄Karte | P7 |
| JS-Ableitungen | `akteurSet`/`akteurVon`, `flussRegeln`, `deriveMembership`, `derivedStores`, `compatible`, `deAkteurParitaet` | Server-Ableitung | P7 |
| Validator | `DomainEditor/Validator.cs` | Analyzer/Boot-Guards | P7 |
| Code-Knoten | 📝/🤖-Karten, Andocken, `normalize`-Migration, Sperre über Hash in `SimHost/CodeSync.cs`/`DateiAbgleich.cs` | Rumpf aus der Datei, 🤖 als Aktion | P7 |
| Ingress-/Betriebs-UI | Palette: Trigger, Pipeline (Handle), Dienst, HostSetting; JS-Abschnitte „INGRESS/PIPELINE“ und „BETRIEB / HOST-BAND“; Handle-Karten | Quelle, Fluss, 🖥 Host-Rahmen | P0 |
| Composition-Root-Extraktion | `GraphExtractor.Kern/CompositionRoot.cs` (Ingress-, Dienst-, Frist-Erkennung) | Lesen der Umgebungs-Dateien nach Schema | P2 |
| Landkarte/Ablauf | LOD-Reste (`setzeLod`, `pruefeLod`, `VIEW.lod`) | Rahmen + Fokus + Minimap | P0 |
| Domänen-Quellklasse | `DateiQuelle`, `FileWatchConfig`, `AddQuelle<…>` im Host | Datei-Treiber + Konfig | P1 |
| Prüfen/Kompilieren/Alle ausführen | Knöpfe der Kopfleiste | ⟳ | P3 |

**Nicht in diesem Ziel:** der Rückbau der Laufzeit-Trigger-Welt (`IPipelineTrigger`, `ITriggerRegistration`, `TimerTrigger`,
`WebhookTrigger`, `TriggerStartupService`, `IPipelineHandler`-Maschinerie, gRPC-Trigger-Pfad). Er folgt, sobald es Takt-Treiber und
flüchtige Quellen gibt (§12.2). Der Editor zeigt die Trigger-Welt ab P0 nicht mehr.

---

## 11 · Was bleibt

- **Board:** Domänen-, Akteur-, 📜 Vertrags-, 🔌 Client-, ⛓ Fluss-Rahmen; Rollen-Spalten.
- **Panel:** ⊕/＋ und Zuordnungs-Editor.
- **Ein Schreibweg:** Scaffolder, `DateiSchreiber`, `PythonSync`, jetzt je Geste.
- **Ein Leseweg:** Extractor (nur Code-Fakten), Sonde (`--sonde`, `soll.txt`), Grammatik als eine Quelle.
- **LLM:** Slots und Kontexte, Konsole.
- **Neu:** Werkbank-Server, Host-Rahmen, Spur-Ansichten.

---

## 12 · Entscheidungen

### 12.1 Entschieden (nicht neu verhandeln)

1. Code ist die einzige Wahrheit; das Board wird nie als Modell gespeichert.
2. Keine Simulationsschicht; die Sim ist der echte Host in Umgebung `sim` gegen echtes Postgres/Marten.
3. Der Laufzeit-Host ist `Host.Grpc` mit `--umgebung`. SimHost wird zum Werkbank-Server (Editor, Schreiben, Bauen, Compose, Prozesse,
   Client- und Ausführer-Rolle, Einblick-Leser).
4. Eine Sim-DB je Git-Branch (`cqrs_sim_<branch>`).
5. Der Editor liest die Laufzeit über die Einblick-API des Hosts, nicht direkt aus der DB.
6. Quellen = fest codierte Treiber + Konfig; eine Quelle = ein Nachrichtentyp; die Treiber-Wahl ist ein Code-Fakt (Basistyp).
7. Treiber lesen nur.
8. Default-Laufmodus `EinmalImCluster`.
9. Keine Abwärtskompatibilität (2026-10-12): kein Upcast, keine Migration; Kennung überall Default `PfadGroesseZeit`.
10. Funktions-Laufort ist Konfig je Umgebung.
11. Neuer Header `ursache` (nicht `CausationId` umdeuten); Wirker auf Karten-Ebene; Spur-Projektion nur in `sim`.
12. Keine Haltepunkte.
13. Kein Deploy aus dem Editor; Prod nur lesend.
14. Keine GUIs im Editor; Clients nur vorbereiten.
15. Keine Schema-Evolution; inkompatible Sim-Daten → Sim-DB zurücksetzen.
16. 🖥 Host-Rahmen rechts neben den Client-Rahmen; kein Band, kein neues Layout.
17. Lokaler Multi-Node in der Sim als Option (Cluster = Consul, Knoten 1–3).

### 12.2 Offen (bewusst später)

- Weitere Treiber: Takt (Kennung = Zeitschlitz), OPC UA, Datenbank-Trigger (mit Stand/Wasserzeichen).
- Flüchtige Quellen für Telemetrie (`ITransientEvent`, Konflation; Inv. 6), danach Rückbau der Trigger-Welt.
- Client meldet Quell-Nachrichten (`ISendet<T>` mit `T : IQuellNachricht` → `QuellEingang`).
- Pfade für entfernte Ausführer (Python-Worker sehen ein Share anders gemountet): `RelativerPfad` ist vorbereitet.
- Tor am Webhook (`MapQuellWebhook` mit Token → Akteur).
- Wer `IAkteurDienst<A>` trägt, wenn es keine Handle-Pipelines mehr gibt (heute nur als Handle-Parameter erlaubt, CQRS060).
- `Selbst<T>`, `Frist<TCmd>`, `FristStorno<TCmd>` gehören zur Handle-Welt und fallen mit ihrem Rückbau; im Fluss gilt `Warte(…).Zeitlimit(…)`.
- `MapQuellWebhook<T>` bleibt vorerst ein Code-Fakt mit `[Ingress(Webhook)]`; ob die Route Konfig der Umgebung wird, entscheidet sich mit dem Tor.

---

## 13 · Umsetzung in Phasen

Die Reihenfolge folgt den Abhängigkeiten. Jede Phase endet grün:
- Prüfstand `dotnet test Infrastructure.Pruefstand.Tests/Infrastructure.Pruefstand.Tests.csproj`;
- wo Laufzeit betroffen ist, Integration **sequentiell** gegen echte Infra (`docs/12-tests-und-vermessung.md`);
- `GraphExtractor -- --check` und `-- --sonde`.

| Phase | Inhalt | Abnahme |
|---|---|---|
| **P0 Aufräumen** (ohne Voraussetzung) | Editor zeigt Trigger, Handle-Pipeline, Dienst, HostSetting und Betriebs-Band nicht mehr; LOD/Landkarte raus; Palette auf Anker (§8.5) | Editor lädt den Bestand ohne diese Karten; `--check` grün; kein Domänen-Konstrukt verschwindet unerklärt (nur Trigger-Welt) |
| **P1 Datei-Treiber** | `DateiGemeldet` (Abstractions); Datei-Treiber (`Infrastructure/Quellen/Treiber/Datei/`) nach §3.5; gebündelte Bekannt-Abfrage im Repository; `QuellenGenerator` (Tabelle, Fabrik, Wirker-Id); Konfig-Bindung per Source-Generator; Analyzer für Quellen-Typen; Proto für geerbte Properties; Bestand umziehen (§3.6); Extractor: Quellen-Typ → Quellkarte (Kopf „📁 Datei-Treiber“), Konfig-Werte im Panel; Sonde + `soll.txt` | Prüfstand: Kennungsbildung beider Modi, Fertig-Erkennung (Stabil/Marker), Bekannt-Abgleich (store-frei über eine Abfrage-Funktion). Integration: Datei in Temp-Verzeichnis → genau ein Vorgang; dieselbe Datei nach Neustart → kein zweiter; `PipelineFlussE2ETests` grün |
| **P2 Umgebungen** | `umgebungen/umgebung.<name>.json` + Schema; `--umgebung`; `AddCqrs(umgebung)`; Siegel-Tabelle + Boot-Prüfung; Cluster-Name und Redis-Präfix aus der Umgebung; Funktions-Laufort als Konfig; `IDbClock` mit Versatz; Composition-Root-Extraktion → Umgebungs-Dateien lesen | Integration: Host mit `sim` gegen DB mit Siegel `prod` startet nicht; zwei Umgebungen parallel auf einer Maschine stören sich nicht (eigene DB, Präfix, Cluster-Name); Versatz verschiebt eine Frist |
| **P3 Werkbank-Server** | SimHost: ⟳-Kette (Prepass, Proto, Build, Python-Basen) mit Diagnose → Karte; Compose aus der Umgebung generieren und verwalten; Host-Prozess(e) starten/ersetzen; Sim-DB: zurücksetzen, Stand speichern/laden, Prod-Stand übernehmen; Kopfzeile mit Status; Host-Rahmen mit generischem Formular | Im Browser: Compiler-Fehler erscheint an der richtigen Karte; nach Korrektur ⟳ → Host läuft gegen `cqrs_sim_<branch>`; Stand speichern/laden in Sekunden |
| **P4 Spur** | Header `ursache`/`wirker`/`stand` (Dispatch, `CommandEmitter`, Envelope, `MartenEventBatchWriter`); geteiltes Wirker-Format; `spur_hinweis`; Spur-Projektion; Einblick-API + SSE mit Cursor | Integration: Bildeingang-Vorgang hat lückenlose `ursache`-Kette von der Quelle bis `BildVerfuegbar`; jede Wirker-Id hat eine Karte (`--check`) |
| **P5 Durchspielen** | Editor als Client (Sim-Token, Konsole mit Formularen); `Laufort: Hand` + offene Aufträge an der Funktions-Karte; Datei-Drop auf Quellkarte; ⏩; Python-Worker starten | Im Browser: Bild ablegen → Vorgang läuft durch; Funktion ohne Implementierung per Hand beantworten → Fluss läuft weiter; Command als Akteur → Event sichtbar |
| **P6 Visualisierung** | Vorgänge-Liste, Spur-Überlagerung, Zeitleiste, Live-Puls, Fokus (ersetzt Slice/Kette/◎/Inspector), ↤/↦ | Im Browser: gewählter Vorgang zeigt nummerierten Weg; Zeitleiste schiebt; Puls zählt |
| **P7 Eine Wahrheit** | warmer Roslyn-Workspace im Server; Geste → Patch; Extractor liest Code mit Fehlern; Board-Persistenz/Merge raus; Server-Ableitungen statt JS-Spiegel; Validator → Analyzer; Rumpf aus der Datei, 🤖 als Aktion; Code-Karte + Gate „Code ohne Karte: 0“; Umbenennen/Löschen/Signatur über Roslyn; ↶ = Patch zurück | `--check`: Code ohne Karte = 0; Umbenennen eines Events ändert alle Verweise und baut; keine JS-Ableitungsfunktion mehr; Geste < 300 ms (gemessen) |
| **P8 Simulation raus** | §10 Zeile „Simulation“ und „Laufwerke“ | SimHost ohne `ModellSimulation`/`Musterwerte`/`/sim/*`; Prüfstand weiter grün |

P0, P1 und P2 sind unabhängig voneinander. P3 braucht P2. P4 kann nach P2 parallel zu P3 laufen. P5 und P6 brauchen P3 und P4. P7
kann nach P3 beginnen. P8 kommt zuletzt, denn vorher gäbe es ohne P3–P6 kein Durchspielen.

---

## 14 · Leitplanken für die Umsetzung

- **Die sechs Invarianten** (CLAUDE.md) und die Projekt-Konventionen gelten:
  - Kommentare und Domäne auf Deutsch;
  - keine Runtime-Reflection, neue Dispatch-Logik = Generator;
  - Extractor nur Code-Fakten, neues Konstrukt ⇒ Sonde + `soll.txt`;
  - kein `InMemoryEventStore`, Store-Semantik nur gegen echtes Marten;
  - Integration sequentiell;
  - neue Domain-Typen brauchen Proto-DTOs (in ⟳ automatisch).
- **Editor-Vorgaben:**
  - deklariert wird nur an der eigenen Karte, verbunden nur per ⊕, „＋“ in der Zeile legt eine neue Karte an;
  - kein semantischer Zoom;
  - das Layout wird nicht ersetzt, nur ergänzt;
  - Oberflächen-Änderungen über dieses Dokument hinaus nur nach Rückfrage.
- Jede Phase verifiziert sich selbst: Tests, `--check`/`--sonde`, bei Editor-Änderungen im Browser (`http://localhost:5178/editor`)
  geprüft und belegt.

---

## 15 · Abgelöste Dokumente

**Gelöscht** (Inhalt hier aufgegangen oder überholt):
- `konzept-quellen.md` — erste Fassung, Quellen als Domänen-Klassen;
- `konzept-streaming-architektur.md` — Trigger-basierte Konnektoren; die Einteilung Telemetrie vs. Faktum lebt in §12.2 weiter;
- `konzept-pipeline-stroeme.md` — überholt durch Pipeline als Fluss;
- `analyse-editor-backend-abdeckung-2026-09-24.md` — Analyse des alten Drei-Wahrheiten-Zustands;
- `07-graph-und-simulation.md` — Extractor-Grundlagen jetzt in Anhang A.1, Simulation entfällt.

**Gekürzt** auf das Vereinbare, mit Hinweis-Block oben. Abschnittsnummern bleiben, weil Code-Kommentare darauf verweisen:
- `konzept-domaenen-editor.md`
- `konzept-editor-pipelines.md` — §12, §13.6, §14, Zuordnungs-Editor
- `konzept-editor-komposition.md`
- `konzept-editor-panel-bearbeitung.md`
- `konzept-handle-ausgaenge.md`
- `konzept-llm-minimalkontext.md`
- `entscheidungs-debugger-und-test-dsl.md`
- `konzept-akteure.md` — Einzelkorrekturen

Diese Dokumente gelten nur noch als Detail-Referenz. Bei Widerspruch gilt dieses Dokument.

---

## Anhang A · Übernommener Bestand (gilt weiter)

### A.1 Extractor-Grundlagen (aus dem gelöschten Kapitel 07)

- **Geparst statt nachgebaut.** Die Routing-Wahrheit kommt aus dem Laufzeit-Generat `GeneratedCommandRouting`: Der Extractor liest
  dessen Syntaxbaum und löst die `typeof`-Ausdrücke über das Semantik-Modell auf (`GraphExtractor.Kern/RoutingTruth.cs`). Fehlt das Generat,
  leitet er dieselbe Abbildung aus den `Decide`-Signaturen ab und markiert die Herkunft ehrlich als `decider-fallback`.
- **Symbole, nicht Text.** Symbole aus verschiedenen Compilations sind nie referenzgleich, deshalb wird über voll-qualifizierte Namen
  verglichen (`Sym.Implements`), dasselbe Idiom wie in den Multi-Compilation-Generatoren.
- **Prozesse:**
  - die Saga-DSL wird als Syntax gelesen (jede Anweisung im `Definiere`-Lambda eine Regel);
  - Flüsse liest `GraphExtractor.Kern/FlussLeser.cs`;
  - welche Prozesse wirklich registriert sind, kommt aus `GeneratedProzessRegeln`.
- **Graph-Ids** tragen ein Präfix je Art (`cmd:`, `evt:`, `agg:`, `proc:` …). Kanten tragen ihre Herkunft (`provenance`).
- **Diagnosen** (künftig Analyzer-Kandidaten, §8.3):
  - `UNROUTED-SAGA-CMD`: ein Prozess sendet ein Command, das kein Aggregat behandelt → Hänger zur Laufzeit;
  - `PROCESS-NOT-REGISTERED`;
  - `DANGLING-EVENT`;
  - `UNUSED-EVENT`;
  - `MISSING-APPLY`;
  - `ENUM-ZERO`;
  - `STORE-AMBIGUOUS`.
- **Sichten:** Event-Auffächerung (Blast-Radius je Event) und kausale Ketten. Sie sind die statische Grundlage des Fokus (§8.5).

### A.2 Anforderungen an den Sofort-Schreiber (§8.2)

- **Regel des Abgleichs:** Die Signatur gehört dem Modell, Rumpf und Handcode gehören dem Code. Was eine Geste nicht betrifft, bleibt
  Wort für Wort stehen.
- **Verlustfrei:**
  - Defaults und Initialwerte, Handcode-Zusätze und Rümpfe (auch bewusst leere);
  - Deklarations-Reihenfolge, OneOf-Reihenfolge;
  - Prozess- und Fluss-Lambdas wortgetreu;
  - `using`s, Summary-Doku.
- **Umbenennen** zieht Verweise in Lambdas mit. Wechselt man das Command bzw. die Funktion an einer Regel oder einem Fluss-Knoten, wird
  dessen Lambda verworfen und die Zuordnung neu vorgeschlagen.
- **Form-Regeln aus der Signatur** (CQRS050):
  - keine Ausgabe ⇒ `Task`;
  - sonst `OneOf<…>` konkreter Typen, auch bei einem einzigen Ausgang;
  - Fähigkeiten (Store-Funktionen) sind Handle-Parameter, nie Konstruktor-Felder (CQRS051–056).
- Welche Geste welchen Code schreibt, steht in `konzept-handle-ausgaenge.md` §13 (Tabelle) und gilt für den Sofort-Schreiber.

### A.3 Editor-Regeln, die bleiben

- **Logik-Element-Muster** (`konzept-domaenen-editor.md` §2 c′): ◀ Eingang · Ausgänge ▶ · ⚙ Fähigkeiten · Rumpf. Ein leerer Rumpf ist
  sichtbar als „⚙ fehlt“, damit Code-Inseln auffindbar sind.
- **Deklariert nur an der eigenen Karte, verbunden nur per ⊕.** „＋“ in der Zeile legt eine neue Karte an und verbindet sie.
- **⊕-Modus** (`konzept-editor-panel-bearbeitung.md` §3.3): Passende Karten leuchten, Anklicken verbindet oder löst; der Modus endet per
  Esc, ⊕ oder Klick ins Leere. Die Kamera bewegt sich nur, wenn kein Kandidat im Bild ist. Abgelehnte Ziele zeigen „✕ Regel“ mit
  Regelname.
- **Leitsatz der Grammatik:** Wenn es sich verbinden lässt, kompiliert es. Die Grammatik ist eine Quelle (`DomainEditor/Grammatik.cs`),
  und jede Regel hat ihr Build-Gegenstück.
- **Garantie sichtbar:** durchgezogen = durabel, gestrichelt = verlierbar (Inv. 6).
- **Wohin Abläufe gehören:** Ablauf-Bausteine stehen im Rahmen der Domäne bzw. des Aggregats, auf das sie wirken. Verarbeitung ist immer
  eine Katalog-Funktion, nie ein Rumpf am Rand.
- **Render-Architektur** der Fläche (`konzept-domaenen-editor.md` §9.2): Transform sofort, Rest im nächsten Frame, Culling, kein
  Layout-Lesen beim Verschieben.

### A.4 Analyzer-Backlog (Prüfen = Bauen, §8.3)

- Grammatik-Regeln, die heute nur der Validator bzw. das Board prüft: GR-QUERY, GR-GARANTIE, GR-ZUSTAND, GR-FALTUNG, GR-MODUL-*,
  Regel Z (Gedächtnis gehört ins Aggregat; Zustandsfelder aus dem Symbol).
- dazu die Diagnosen aus A.1.

### A.5 LLM

Slots, Kontexte und Konsole bleiben (`konzept-llm-minimalkontext.md`). Es gilt `claude -p` nur über das Abo, nie die API. Der Ablauf ist
Füllen → Prüfen → Anpassen → Übernehmen, abgesichert über Hash-Sperre, Sicherung und Rückgängig; geprüft wird über die
`ProjektPruefung`. Die Slot-Arten umfassen u. a. `decide`, `apply`, `projektion`, `reaktion`, `reader`, `store`, `funktion`, `fluss`, `saga`
und `zusage`. Auslöser ist die Aktion 🤖 am Rumpf (§8.4).
