# Plan: Akteure verwirklichen — Apps und Quellen, Graphen ohne Quellknoten

Stand 2026-11-13, zweite Fassung nach dem Abgleich mit dem Code (Commit 7c150f4d). Maßstab für den Umbau; umgesetzt wird Schritt für Schritt,
jeder Schritt auf Tobis Wort (E5). Nach der Umsetzung steht der Inhalt in den Kapiteln (docs/04, docs/10, docs/07, docs/11,
docs/konzept-akteure.md, CLAUDE.md) und der Plan im Git-Verlauf.

## Das Modell

Was hineinkommt, kommt von einem Akteur. Wir trennen, **was es gibt** (Konzept, Code, im Modal) von **was wo läuft** (Instanz, Konfig, auf der
Fläche und in der Umgebung):

1. **Nachrichten** entstehen wie heute im Aggregat- bzw. Katalog-Modal.
2. **Akteure** sagen, was sie hineingeben dürfen (`IDarf<T>`) und was zu ihnen kommt (`IHoert<T>`). Unverändert.
3. **Verwirklichung eines Akteurs** — womit er seine Nachrichten tatsächlich erzeugt: eine **App** (Blazor, Python) gegen den generierten
   Vertrag, oder eine **Quellart** (Dateiwächter, Taktgeber, Webhook, eine eigene) mit einer **Übersetzung auf der Akteur-Seite**, die aus ihrer
   Meldung eine Nachricht macht, die der Akteur darf. Diese Übersetzung ist Code und gehört zu den Akteuren.
4. **Client** — verkörpert Akteure (`IVerkoerpert<A>`, unverändert) und hat in einer Umgebung **Eingänge**: seine App-Verbindung und/oder
   beliebig viele Quellen, auch verschiedener Art und für verschiedene seiner Akteure.
5. **Umgebung** — je Client seine Eingänge mit Art, Akteur, Rechner und Werten (drei Verzeichnisse = drei Eingänge); je Graph genau eine Instanz
   mit der Zahl ihrer Stränge.

Dahinter liegt die Backend-Seite wie heute: die Nachricht des Akteurs geht an ein Aggregat oder, als Command ohne Ziel, an den Kopf eines Graphen
(direkt oder über eine Kopf-Übersetzung); seine Ausgänge gehen über Übersetzungen an Aggregate. **Ein Graph kennt keine Quelle mehr.**

Am Beispiel: der Dateiwächter des KameraSystems meldet `DateiGefunden`; die Übersetzung der Akteur-Seite macht daraus den Kopf-Command der
Bildaufbereitung (Vorschlag: `LiesBild` im Katalog `Bildvorschau`), den das KameraSystem darf; der Graph rechnet; `BildGedeutet ↦ NimmBildAuf`
bleibt die Übersetzung der Backend-Seite. `NimmBildAuf` trägt das KameraSystem über die Kette — sein `IDarf<NimmBildAuf>` und die Ausnahme in
konzept-akteure §2.1 entfallen.

## Entscheidungen

Die Punkte 9 bis 13 sind die Antworten auf die offenen Fragen des Berichts vom 2026-11-13, als sichere Vorgaben gesetzt; Tobi kann jede
zurücknehmen, bevor der Schritt beginnt, der sie braucht.

1. **Quellarten sind keine Funktionen.** Eine Quellart ist eine Klasse `: IQuellart` mit `Lausche(CancellationToken)`, die einen Strom ihrer
   Meldungen gibt. Ihr Konstruktor nimmt dasselbe wie der einer Funktion: Konfig-Records, `IZustand<T>`, Technik, `ILogger<T>` (die Sonde hat eine
   eigene Quelle mit Technik). Sie liegt in einem Katalog (die des Frameworks in `Core/Katalog`), nie in einem Graphen.
2. **Eine Meldung ist kein Command.** `record … : IMeldung` mit `string Kennung` — die Kennung des Elements, aus seinem Inhalt: beim Dateiwächter
   seine Datei-Kennung (nach `Kennung` der Konfig), beim Taktgeber der Beginn des Intervalls, beim Webhook die Kennung des Absenders. Eine Meldung
   nimmt kein `Auf` und kein Kopf; sie wird nur übersetzt.
3. **Die Übersetzung der Akteur-Seite** ist eine gewöhnliche Übersetzung (`class {Meldung}_{Ziel}_{n}(TMeldung source) : ITranslate<TMeldung, TZiel>`)
   mit einer Meldung als Quelle, in `<Wurzel>.Akteure/Uebersetzungen.cs` (flach wie Akteure und Clients). Ihr Ziel ist ein Command (an ein Aggregat
   oder ohne Ziel) — eine Nachricht der Domäne oder eines Katalogs, die ein Akteur darf.
4. **Welche Quellarten ein Akteur haben kann, folgt aus den Typen:** eine Quellart passt zu ihm, wenn es eine Übersetzung von ihrer Meldung auf
   eine Nachricht seines `IDarf` gibt. Kein Marker am Akteur, kein `IFuer<A>`; mehrere passende Übersetzungen sind eine Gabel wie heute.
5. **Ein Eingang in der Umgebung** steht beim Client: Name, Art (die Quellart), Akteur, Rechner, die Werte ihrer Konfig, die Wahl ihrer Technik
   und die Grenze offener Elemente. Der Start prüft: der Client verkörpert den Akteur, die Art gibt es, mindestens eine Übersetzung passt, die
   Werte binden, im Cluster ein Rechner. Auswahl und Werte sind Konfig (W7), Struktur ist Code.
6. **Ein Eingang aus einer Quelle läuft im Host** und braucht kein Token (wirksam ist sein Akteur). Ein Webhook nimmt von draußen an und prüft
   einen Schlüssel aus seinen Werten (Schritt 5).
7. **Ein Graph hat keinen Quellknoten**; jeder Kopf nimmt Commands. Damit gilt die heutige Regel „ein Graph mit Command-Kopf läuft je Umgebung genau
   einmal“ für jeden Graphen. `Akteur` in der Instanz-Datei entfällt; die Kette trägt den Akteur ihres Auslösers.
8. **Ein Katalog-Graph nimmt am Kopf eigene Commands**, keine Meldung. Die Bildaufbereitung bekommt einen Kopf-Command des Katalogs statt
   `DateiGefunden`; so hängt ein Katalog an keiner Quellart. Die Übersetzung der Akteur-Seite zielt direkt darauf — eine Übersetzung, nicht zwei
   hintereinander.
9. **Der Client bleibt der Ort der Eingänge** (so von Tobi beschrieben): ein Client ist die Einheit, die Akteure verkörpert — über eine Leitung
   (App) oder im Host (Quellen). konzept-akteure §4.1 („eine Verbindung, eine Sitzung“) wird so umgeschrieben. Der Rechner steht je Eingang.
10. **Stränge gelten je Host** wie jede Grenze heute (Parallel, Warteschlange, Drossel): `Straenge` an der Instanz = gleichzeitige Ketten je Host,
    die die Instanz hält. Die Warteschlange am Kopf begrenzt keine Ketten mehr (ein Regler je Absicht). Standard wie heute die Warteschlange am Kopf
    (10 000), damit sich ohne Angabe nichts ändert. `Parallel` am Knoten bleibt die Obergrenze je Knoten; ein Knoten mit Zustand rechnet mit einem
    Kind.
11. **Die Kennung eines Elements kommt aus seinem Inhalt** (Entscheidung 2), nicht aus einer laufenden Nummer: `Herkunft.Ableite(eingang, Kennung)`.
    Grund: nach einem Neustart kann eine Quelle dieselben Elemente in anderer Reihenfolge melden (der Dateiwächter sortiert nach Pfad, fertig wird
    nach Zeit); mit Nummern landen dann zwei Bilder desselben Paars auf denselben Command-Ids — eines fiele weg, eines wirkte doppelt.
    Invariante 6 bekommt den Wortlaut: durabel ist eine Quelle über ihren gesicherten Zustand (was sie gesehen hat), gesichert, sobald ihre
    Nachricht durabel hineingegeben ist; die Kennung eines Elements kommt aus seinem Inhalt.
12. **Kein Eingangsname in der Meldung.** Was je Eingang verschieden ist und fachlich zählt, trägt die Meldung aus ihrem Inhalt (der Dateiname
    nennt die Linie). Kommt ein Fall, der mehr braucht, steht der Eingang in der `Herkunft`, nicht im Typ.
13. **Reihenfolge je Schlüssel** gibt es zunächst nicht; Stränge = 1 heißt „der Reihe nach je Host“.

## Wie ein Element hineinkommt (Laufzeit)

Je Eingang mit lauschender Quellart ein **Läufer** (`Infrastructure/Quellen/`), im Cluster genau einmal auf seinem Rechner (dieselbe Regel und
dieselbe Gossip-Prüfung doppelter Rechnernamen wie heute für Instanzen). Er aktiviert die Quellart wie heute ein Kind seine Funktion (DI-Bereich,
Konfig-Werte und Technik-Wahl aus dem Eingang, Zustand aus `cqrs_zustaende` mit dem Eingang als Schlüssel) und ruft `Lausche`. Je Meldung:

1. Kennung = aus Client, Eingang und `Kennung` der Meldung.
2. Übersetzen über die generierte Tabelle „Meldungstyp → Übersetzungen“, gefiltert auf das `IDarf` des Akteurs (reflexionsfrei).
3. Hineingeben wie eine App, im Namen des Akteurs: ein Command an ein Aggregat über den Emitter (Id aus Kennung, Übersetzung und Stelle; gewartet
   bis zur Antwort, die Inbox ist durabel), ein Command ohne Ziel als offener Eingang am Kopf (`Folgenbeilagen.VonAkteur`, Zeile in
   `cqrs_eingaenge`, dann der schnelle Weg).
4. Ist die Nachricht durabel, sichert der Läufer den Zustand der Quellart (was sie gesehen hat) — höchstens so oft, wie es Last spart, immer nach
   einem fertigen Element und beim Stoppen.

**Gegendruck:** die Zeilen in `cqrs_eingaenge` bekommen die Spalte des Eingangs; hat ein Eingang so viele offen, wie seine Grenze erlaubt, wartet der
Läufer, bevor er die nächste Meldung nimmt. Endet `Lausche` oder wirft es, beginnt der Läufer nach einer Pause neu, mit dem gesicherten Zustand.

**Garantie:** was der gesicherte Zustand enthält, meldet die Quelle nicht wieder; was nach ihm kam, meldet sie mit derselben Kennung erneut —
eine offene Zeile wird nicht doppelt gelegt, ein Command an ein Aggregat wirkt über die Inbox einmal. Das Fenster zwischen „durabel“ und
„gesichert“ ist dasselbe wie heute zwischen „Kette fertig“ und „gesichert“.

Was im Graph-Actor, Parent, Kind und in der Aktivierung entfällt: Quellknoten, `Eingangsart.Quelle`, `QuelleHalten`, `QuellMeldung`, `Lausche`,
der Rückstau und das Sichern der Quelle, die Nummer der Aktivierung, `GraphPlan.Quelle`, `GraphInstanz.Akteur` und der Vorrang des Instanz-Akteurs
vor dem des Auslösers. Neu im Graph-Actor nur die Grenze der Stränge.

## Schritte

Jeder Schritt endet mit `dotnet build`, `--check`, `--sonde`, `--grammatik`, den Suiten, die er berührt (nur auf dem Server über
`scripts/server.sh`; Laufzeit-Suiten nur nach Rückfrage, solange der Fokus auf Editor und GUI liegt), und zieht die Kapitel nach, die er ändert.
Commit nur auf Tobis Wort.

### Schritt 1 — Kennung aus dem Inhalt (klein, vor dem Umbau)

- `IMeldung` mit `Kennung` in `Abstractions`; `DateiGefunden`, `Takt`, `Aufruf` tragen sie (der Dateiwächter setzt seine Datei-Kennung).
- Heutiger Graph-Actor: die Kennung einer Quell-Kette aus Instanz und `Kennung` statt Nummer.
- Garantie-Test (Prüfstand, Kategorie Garantie — die Aussage hängt nur an den Ids, nicht am Store): Absturz, nach dem Neustart andere
  Reihenfolge — jede Id trägt dasselbe Element, keins fällt weg, keins wirkt doppelt. Er bleibt das Netz für Schritt 3.
- Eine eigene Quelle, deren Elemente noch keine Meldung sind (die Sonde), zählt bis Schritt 3 nach der Nummer; dann wird `IMeldung` Pflicht.

### Schritt 2 — Stränge an der Instanz (klein)

- `GraphInstanzKonfig.Straenge`; der Graph-Actor nimmt so viele Ketten zugleich an, sonst „später“; die Warteschlange am Kopf zählt nicht mehr.
- Instanz-Formular der Werkbank: das Feld Stränge; Prüfstand-Test der Grenze; der Umgebungs-Lader liest `Straenge` als Feld der Instanz, nicht als
  Knoten. Auch die Quelle einer Instanz wartet auf einen freien Strang (bisher auf die Warteschlange ihres Knotens).

### Schritt 3 — Quellen als Eingänge (der Umbau, in einem Zug)

Baut erst wieder als Ganzes; in dieser Reihenfolge:

1. **Verträge und Lage:** `IQuellart`; Meldungen nicht mehr `ICommand`; `Projektstruktur.cs` + `Lageart.cs`: Quellart und Meldungen im Katalog,
   die Übersetzung mit einer Meldung als Quelle in `<Wurzel>.Akteure`.
2. **Lesarten und Generatoren:** eine Quellform (aus der Funktionsform gelöst, dieselben Regeln für Konstruktor und `Lausche`); die Graphform
   verliert den Quellknoten; die Tabelle „Meldungstyp → Übersetzungen“; Technik- und Zustands-Bindung auch für Quellarten.
3. **Analyzer und Grammatik** (je Regel ein Gegenstück in `DomainEditor/Grammatik.cs`): eine Quellart ist kein Knoten; eine Meldung nimmt kein
   `Auf` und kein Kopf; eine Übersetzung mit einer Meldung liegt bei den Akteuren und zielt auf einen Command, den ein Akteur darf; die Regeln zu
   `Lausche` am Kopf und zum Akteur der Instanz entfallen.
4. **Laufzeit:** der Läufer (oben); Graph-Actor, Parent, Kind, Aktivierung, Wurzel, `Rechner.Pruefe` ohne Quelle; `cqrs_eingaenge` mit der Spalte
   des Eingangs; Boot-Guard der Eingänge; Herkunft und Spur eines Eingangs (Wirker = der Eingang, W9).
5. **Konfig:** `ClientKonfig.Eingaenge`; `GraphInstanzKonfig` ohne `Akteur`; der Umgebungs-Lader; Umgebungen `sim-main`, `prod`, `multinode`,
   `vermessung` (drei Verzeichnisse in `sim-main` als drei Eingänge eines Clients), `vermessung-pruefdomaene` ohne Änderung.
6. **Beispieldomäne:** Kopf-Command der Bildaufbereitung im Katalog, `Linienimport` entfällt, die Übersetzung `DateiGefunden ↦ <Kopf>` bei den
   Akteuren, `KameraSystem` darf den Kopf statt `NimmBildAuf`, ein Client für die Linie. Integrations-Tests und Verhaltens-Netz, die heute
   `DateiGefunden` an den Kopf geben oder den Linienimport anlegen, auf Eingänge umstellen.
7. **Werkzeuge, so weit, dass sie stimmen:** Extractor und Sicht ohne Quellknoten (die Spalte Quellen bleibt leer), `AkteurAnteile`, Sonde und
   `soll.txt` (ein Akteur mit eigener Quellart statt eines Graphen mit Quellknoten), `Durchspielen` legt Dateien je Eingang ab; „＋ Quelle“ im
   Katalog-Modal entfällt.
8. **Tests:** der Garantie-Test aus Schritt 1 bleibt grün; dazu Gegendruck bei voller Grenze und mehrere Eingänge eines Clients nebeneinander.

### Schritt 4 — Werkbank: Sicht und Editor

- **Sicht** (Server, W3): je Akteur seine Verwirklichungen (Quellarten mit Übersetzung, App = Vertrag); je Client seine Eingänge aus der Umgebung
  (`rahmen.umgebungen`); je Graph seine Instanz mit Strängen — der Editor filtert nichts mehr selbst (heute die Instanz-Chips).
- **Modal „Akteure und Clients“** (Konzept, Code): an einem Akteur in der Spalte Herkunft seine Verwirklichungen, „＋ Verwirklichung“ wählt eine
  Quellart und eine Nachricht seines `IDarf` und legt die Übersetzung an; Feldzuordnung mit derselben Mechanik wie heute. Keine Werte.
- **Fläche** (Instanz, Konfig): eine eigene **Spalte Clients ganz links** (entschieden 2026-11-13) — je Client eine Karte mit seinen Eingängen
  (App-Verbindung bzw. je Quelle Art, Rechner, Werte), „＋ Eingang“ an der Karte (nur Arten, zu denen ein verkörperter Akteur passt),
  Doppelklick auf einen Eingang öffnet das Formular; je Eingang eine Kante zu dem Akteur, für den er hineingibt (in der Gruppe des Clients in
  der Spalte daneben, wie heute). An der Graph-Karte ihre Instanz mit Strängen. Die Spalte Quellen entfällt; die Fläche hat damit wieder
  sieben Spalten: Clients, Client-Ausgang, Graphen, Übersetzungen, Aggregate, Projektionen, Client-Eingang (E4 in docs/10 nachziehen).
- **Katalog-Modal:** Quellen verschwinden; „gewählt in …“ einer Technik rechnet der Server aus `KnotenKonfig.Technik` der Instanzen (heute liest der
  Editor einen Abschnitt, den es nicht mehr gibt).
- Abnahme: `Werkbank.Abnahme.Tests` und im Browser.

### Schritt 5 — Webhook als Eingang

Ein Webhook lauscht nicht: jeder Host nimmt `POST /eingang/webhook/<Pfad>` an (eigener Schalter statt des Sim-JSON-Eingangs), prüft den Schlüssel
des Eingangs, übersetzt und legt die Nachricht durabel ab (Kennung aus Eingang und Kennung des Absenders), erst dann 202. Heute antwortet er,
bevor etwas durabel ist, im Cluster liest nur der Rechner-Host sein Postfach, und eine Wiederholung wirkt doppelt.

### Schritt 6 — Abschluss

`scripts/tests.sh voll`; Messung S1 (Bildeingang über den Dateiwächter) nur nach Rückfrage; die Kapitel tragen den Ist-Zustand, der Plan geht in den
Git-Verlauf.

## Risiken

- **Umfang von Schritt 3:** rund 40 Dateien nennen heute Quelle bzw. `Lausche` (Generatoren, Laufzeit, Extractor, Werkbank, Tests, Sonde).
- **Garantie am Eingang:** sie muss gleich gut sein wie heute — deshalb der Test in Schritt 1 vor dem Umbau.
- **Leistung:** ein Element geht künftig über `cqrs_eingaenge` (eine Zeile mehr je Datei); S1 zeigt, ob das zählt.
- **Tests auf dem Server:** Schritte 1, 2, 3 und 5 brauchen Laufzeit-Suiten; je Schritt frage ich vorher.
