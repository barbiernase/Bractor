# Plan: Akteure verwirklichen — Apps und Quellen, Graphen ohne Quellknoten

Stand 2026-11-12. Maßstab für den Umbau; nach der Umsetzung geht der Inhalt in die Kapitel (docs/04, docs/10, docs/konzept-akteure.md) und
der Plan in den Git-Verlauf.

## Das Modell

Was hineinkommt, kommt von einem Akteur. Der Weg dahin in fünf Schritten, jeder an seinem Ort:

1. **Konzepte** — die Nachrichten der Domäne (Commands, Queries). Sie entstehen wie heute im Aggregat- bzw. Katalog-Modal.
2. **Akteure** — wer welches Konzept hineingeben darf (`IDarf<T>`) und was zu ihm kommt (`IHoert<T>`). Unverändert.
3. **Verwirklichung eines Akteurs** — womit er seine Nachrichten tatsächlich erzeugt:
   - eine **App** (Blazor, Python): ein Entwickler programmiert gegen den generierten Vertrag des Akteurs;
   - eine oder mehrere **Quellen** (Dateiwächter, Taktgeber, Webhook): eine Quellart meldet, was sie findet; eine **Übersetzung auf der
     Akteur-Seite** macht aus der Meldung eine Nachricht, die der Akteur darf (`DateiGefunden ↦ NimmBildAuf`). Die Übersetzung gehört dem
     Akteur, nicht dem Graphen und nicht dem Client.
4. **Client** — die Software-Einheit, die Akteure verkörpert (`IVerkoerpert<A>`, unverändert). Ein Client kann **mehrere Eingänge** haben:
   eine App-Verbindung und/oder beliebig viele Quellen, auch verschiedener Arten, für verschiedene verkörperte Akteure.
5. **Umgebung** — je Client seine Eingänge mit Werten: die App mit Token und Adresse, jede Quelle mit Art, Akteur, Rechner und Werten
   (drei Verzeichnisse = drei Eingänge). Am Graphen die Zahl der Stränge.

Dahinter liegt die **Backend-Seite** wie heute: die Nachricht des Akteurs geht an ein Aggregat (`ICommand<T>`) oder als Command ohne Ziel
über eine Übersetzung an den Kopf eines Graphen; dessen Ausgänge gehen über Übersetzungen an Aggregate. Ein Graph kennt keine Quelle mehr.

**Zwei Übersetzungen, zwei Seiten:** auf der Akteur-Seite *Quelle → Nachricht des Akteurs* (fachlich: wie der Akteur seine Absicht aus dem
erzeugt, was draußen passiert), auf der Backend-Seite *Nachricht → Kopf eines Graphen* bzw. *Ausgang → Aggregat* (wie die Anwendung einen
domänenfreien Graphen anbindet). Beide sind Übersetzungen im selben Sinn (`ITranslate`, Feld für Feld, Code).

## Entscheidungen

1. **Quellarten sind keine Funktionen.** Dateiwächter, Taktgeber, Webhook tragen `IQuellart` statt `IFunktion`; sie behalten `Lausche`, ihren
   Konfig-Record und ihr `IZustand<T>`. Ihre Meldungen (`DateiGefunden`, `Takt`, `Aufruf`) bleiben Records des Frameworks; jede trägt
   zusätzlich den Namen des Eingangs, aus dem sie kam.
2. **Die Übersetzung der Akteur-Seite nennt ihren Akteur:**
   `class DateiGefunden_NimmBildAuf_1(DateiGefunden source) : ITranslate<DateiGefunden, NimmBildAuf>, IFuer<KameraSystem>` — in
   `<Wurzel>.Akteure` (neue Lage in der Projektstruktur). Regeln (Build): das Ziel ist eine Nachricht, die der Akteur darf; je Akteur und
   Meldung höchstens eine. Damit stören sich zwei Akteure mit derselben Quellart nie.
3. **Welche Quellen ein Akteur haben kann, folgt aus seinen Übersetzungen**: eine Quellart ist für ihn möglich, wenn es eine Übersetzung von
   ihrer Meldung gibt. Kein weiterer Marker.
4. **Ein Eingang in der Umgebung nennt Art, Akteur, Rechner und Werte.** Der Akteur muss vom Client verkörpert sein, die Übersetzung
   (Akteur, Meldung der Art) muss es geben — das prüft der Host beim Start (Boot-Guard) und die Werkbank beim Anlegen. Auswahl und Werte sind
   Konfig (W7), Struktur ist Code.
5. **Ein Eingang aus einer Quelle sendet nur** (kein `IHoert`); er braucht kein Token — er läuft im Host, wirksam ist sein Akteur.
6. **Ein Graph hat keinen Quellknoten**; ein Kopf nimmt Commands. Je Umgebung gibt es genau eine Instanz eines Graphen.
7. **Werte, die je Eingang verschieden sind, reisen in der Nachricht** (der Name des Eingangs steht in der Meldung; die Übersetzung trägt weiter,
   was der Graph braucht). Keine zweite Graph-Instanz dafür.
8. **Parallelität ist eine Zahl an der Graph-Instanz** (gleichzeitige Ketten je Host); `Parallel` am Knoten ist nur noch eine Obergrenze, ein
   Knoten mit Zustand rechnet wie heute mit einem Kind. Verteilt wird wie heute (eine Instanz ohne Rechner bedient jeder Host). Eine
   Reihenfolge je Schlüssel gibt es zunächst nicht.

## Phasen

Jede Phase endet mit Build, `--check`, `--sonde`, `--grammatik` und den Suiten, die sie berührt (über `scripts/server.sh`).

### 1. Verträge, Projektstruktur, Lesarten

- `Abstractions`: `IQuellart`, `IFuer<TAkteur>`; `Katalog.cs`: Meldungen mit dem Namen des Eingangs; `Projektstruktur.cs` + `Lageart.cs`: die
  Übersetzung der Akteur-Seite liegt in `<Wurzel>.Akteure`.
- `Core/Katalog`: Dateiwächter, Taktgeber, Webhook auf `IQuellart`.
- Generatoren: eine **Quellform** (aus der Funktionsform herausgelöst); die Graphform verliert den Quellknoten; eine generierte Tabelle
  „Akteur × Meldung → Übersetzung“ für die Laufzeit (keine Reflection).
- Analyzer + Grammatik (je Regel ein Gegenstück in `DomainEditor/Grammatik.cs`): Übersetzung mit `IFuer<A>` zielt auf eine Nachricht, die `A`
  darf; je Akteur und Meldung höchstens eine; eine Quellart ist kein Knoten eines Graphen; die Regeln zu `Lausche` am Kopf entfallen.
- `GraphExtractor.Kern`: Akteure mit ihren Verwirklichungen lesen; Sonde und `soll.txt` umstellen (ein Akteur mit Quelle statt eines Graphen mit
  Quellknoten).

### 2. Laufzeit

- **Eingangsläufer** je Quell-Eingang (`Infrastructure/Quellen/`): im Cluster genau einmal, auf seinem Rechner; ruft `Lausche`, hält das Gesehene
  als Zustand (`cqrs_zustaende`, Schlüssel = Eingang), übersetzt über die generierte Tabelle und gibt die Nachricht im Namen des Akteurs
  hinein wie eine App (an ein Aggregat über den Emitter, ohne Ziel über den offenen Eingang am Kopf). Id aus Eingang und laufender Nummer;
  gesichert, wenn der Eingang fertig ist (Invariante 6); „später“ hält ihn an.
- **Graph-Actor**: Quelle, `Eingangsart.Quelle`, ihr Rückstau und das Sichern der Quelle entfallen; neu die Grenze gleichzeitiger Ketten.
- Boot-Guard: jeder Eingang hat einen verkörperten Akteur und eine Übersetzung.
- Garantie-Tests: nichts doppelt nach Neustart eines Eingangs, Gegendruck bei vollen Strängen, mehrere Eingänge eines Clients nebeneinander.

### 3. Konfiguration

- `ClientKonfig`: neben Token und Adresse (App) eine Liste **Eingaenge** (Name, Art, Akteur, Rechner, Werte der Quellart).
- `GraphInstanzKonfig`: `Akteur` entfällt, `Parallel` kommt dazu; `Rechner` bleibt für Graphen mit Zustand.
- Umgebungen `sim-main`, `prod`, `multinode`, `vermessung-pruefdomaene` umstellen; Instanzschema der Werkbank nachziehen.

### 4. Werkbank-Server

- Sicht `akteure` erweitert: je Akteur seine Verwirklichungen (Quellarten mit Übersetzung; App = generierter Vertrag); je Client verkörperte
  Akteure; je Umgebung die Eingänge.
- Fläche: Spalte **Clients** ganz links (je Client eine Karte mit ihren Eingängen, Kante zu den Akteuren); die Spalte Quellen entfällt; ein Graph
  steht, wenn er eine Instanz in der Umgebung hat.
- Gesten: Verwirklichung anlegen (Übersetzung mit `IFuer<A>`, Code); Eingang anlegen, Werte, Rechner, Stränge (Konfig).

### 5. Editor

- **Akteur-Modal**: links Akteure und Clients; ein Akteur zeigt, was er darf und hört, und seine Verwirklichungen (Quelle: Art und Übersetzung
  Feld für Feld; App: der Vertrag), „＋ Verwirklichung“; ein Client zeigt, wen er verkörpert. Keine Werte.
- Hauptansicht: Spalte Clients mit ihren Eingängen (Art, Werte, Rechner), an der Graph-Instanz die Stränge und die Werte ihrer Knoten.
- Katalog-Modal: Quellen verschwinden; die Technik-Wahl („gewählt in …“) wandert an den Knoten der Graph-Instanz (`KnotenKonfig.Technik`), der
  Editor liest sie heute noch aus einem älteren Abschnitt der Umgebung — vereinheitlichen.

### 6. Beispiel- und Prüfdomäne

- Beispieldomäne: `KameraSystem` mit Dateiwächter-Verwirklichung, ein Client mit drei Eingängen in `sim-main`, die Bildaufbereitung mit
  Command-Kopf.
- Prüfdomäne und Vermessung (`Cqrs.Vermessung/Bildeingang.cs`) umstellen.
- Abnahme: `scripts/tests.sh voll`; Messung nur nach Rückfrage.

## Risiken

- **Umfang:** 43 Dateien nennen heute Quelle bzw. `Lausche` (Generatoren, Laufzeit, Extractor, Werkbank, Tests).
- **„Genau einmal“ am Eingang** muss dieselbe Garantie geben wie heute die Quelle im Graph-Actor — die Garantie-Tests kommen vor dem Umbau der
  Werkbank.
- **Tests auf dem Server:** die Phasen 2 und 6 brauchen Laufzeit-Suiten; der jetzige Fokus (Editor, lokal bauen) gilt für sie nicht.

## Offen

- **Reihenfolge je Schlüssel** in parallelen Strängen: erst bei Bedarf (Schlüssel aus einem Feld des Kopf-Commands).
- **Werte eines Eingangs in der Übersetzung**: ob die Übersetzung der Akteur-Seite mehr als den Namen des Eingangs lesen darf (z. B. einen
  Wert, den der Eingang konfiguriert).
- **Name von `IFuer<TAkteur>`** — passt er neben `[Ueber<T>]`, oder ein anderes Wort?
