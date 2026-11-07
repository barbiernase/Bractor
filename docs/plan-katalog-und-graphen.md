# Plan: Funktionskatalog, Übersetzung und die Modale für Funktion und Graph

Stand 2026-11-05, **Schritte 1–5 umgesetzt** (5 im Editor abgenommen bis zum Bauen, siehe dort), der Rest ist Plan; §1 „Wer Funktionen anstößt“ geht allem Folgenden vor. Er fasst die Diskussion vom 2026-11-04/05 zusammen. Umgesetzt wird schrittweise,
je Schritt auf Tobis Wort (E5). Jeder Schritt baut für sich, lässt alle Suiten grün und liefert seinen Teil im Editor gleich mit (§1.1).

## 1. Was entschieden ist

**Wer Funktionen anstößt (2026-11-05, nach Schritt 4 — geht allem Folgenden vor).** Nur **Clients** (ein Akteur mit `IDarf<T>`) und **Quellen**
(Dateiwächter, Taktgeber, Webhook, eigene, die lauschen) stoßen Funktionen an. Ein `Decide` übersetzt einen Command in Events (dazu Hinweise und
Fristen) — sonst nichts; dass seine Signatur auch einen Command ohne Ziel ausgeben könnte, bildet der Editor nicht ab. Es gibt **keine Arbeit für
Akteure** aus einem `Decide`, **keine Ereignisquelle**, keinen neuen Knotentyp dafür. Wir haben Eventual Consistency: der Client sieht den Stand über
die Projektionen (Push „aktualisiert“, er fragt neu); was nach einem Fakt weitergehen soll, stößt der Client an. Der Weg ist immer:

```
Client / Quelle ─▶ Funktion / Übersetzer ─▶ Command ─▶ Decide ─▶ Events ─▶ Projektionen ─▶ Client
```

**Ein `Auf` nimmt eine Nachricht.** Es gibt kein Bündel für Funktionen. Was später zusammen gebraucht wird, trägt die Nachricht mit.

**Funktionen und Aggregate sind voneinander isoliert.** Drei Schichten, Abhängigkeiten nur nach unten:

```
Anwendung (Pipeline)   Kompositionsgraphen + Übersetzungen         kennt Katalog UND Domäne
   │                          │
Domäne (Schreib-/Leseseite)   Katalog (<Name>.Katalog)
kennt keine Funktionsnachricht    Funktionen + ihre Nachrichten, kennt nur Abstractions
```

- Ein **Katalog** ist ein eigenes Projekt, das nur `Abstractions` referenziert und in mehreren Anwendungen verwendet wird. Es enthält
  Funktionen, ihre Nachrichten (Commands ohne Ziel, Hinweise), Konfig-Records und Technik. Den Code einer Katalogfunktion fasst eine Anwendung
  nie an. `Core/Katalog` (Dateiwächter, Taktgeber, Webhook) ist der Katalog des Frameworks.
- Die **Domäne** spricht nur ihre eigene Sprache. Ein `Decide` gibt einen eigenen Auftrag aus (Command ohne Ziel bei der Schreibseite) und weiß
  nicht, wer ihn bearbeitet.
- Die **Anwendung** komponiert je Setting einen Graphen. Nur sie kennt beide Seiten, und an den Grenzen stehen Übersetzungen.

**Die Übersetzung ist ein Knoten des Frameworks, kein Code des Modellierers.**

```csharp
public sealed record Trainingslauf(
    [Kopf] Uebersetze<TrainingZuRechnen, Vorverarbeite> Hinein,
    [Von(nameof(Hinein))] Vorverarbeitung Vorverarbeiten,                                          // Katalog: OneOf<Vorverarbeitet, NichtVorverarbeitbar>
    [Von(nameof(Vorverarbeiten))] Uebersetze<Vorverarbeitet, VermerkeVorverarbeitung> Zwischenstand,
    [Von(nameof(Vorverarbeiten))] Uebersetze<NichtVorverarbeitbar, VerwirfTraining> Absage,
    [Von(nameof(Vorverarbeiten))] Training Trainieren,                                             // Katalog
    [Von(nameof(Trainieren)), Feld(nameof(VermerkeModell.Pfad), nameof(ModellTrainiert.ModellPfad))]
        Uebersetze<ModellTrainiert, VermerkeModell> Ergebnis) : IGraph;
```

- Welche Nachricht ein Übersetzungsknoten nimmt, sagen `[Von]` und `TEin`. Jeder Fall eines `OneOf` ist ein eigener Port, und den bestimmten
  Fall wählt man über den Typ. Gleiche Bedeutung heißt gleicher Typ. Bedeuten zwei Ausgaben Verschiedenes, sind es zwei Typen.
- Was er erzeugt, sagt `TAus`. Die Felder ordnet der Generator zu (§2), Abweichungen stehen als `[Feld(Ziel, Quelle)]` am Knoten.
- Eine Übersetzung hat keine Logik: aus einer Nachricht eine, Feld für Feld, ohne Bedingung. Alles darüber hinaus ist eine Funktion.
- Sie läuft **inline** auf dem Actor, auf dem die Nachricht gerade ist. Heraus geschieht das im Kind der vorigen Funktion, am Kopf beim Annehmen.
  Sie hat keinen eigenen Parent, keinen Zustand, keine Konfig, keinen Rechner und keinen Netzweg.

**Funktionen sind keine virtuellen Actors.** Virtuelle Actors bleiben bei Aggregaten, Lesern, Uhr und Zusteller. Zustandslose Funktionen rechnen
parallel auf jedem Host, eine Kette bleibt auf dem Rechner, der ihren Kopf annahm. Funktionen mit Zustand und Quellen laufen dort, wo ihre
Hardware ist (`Rechner` in der Instanz), ohne Umzug. Eine Funktion hat keine fachliche Identität. Wer einen Einzelschreiber je Id braucht, braucht
ein Aggregat. Der Plan führt keinen neuen Actor ein.

**Zwischenergebnisse.** Ein Kompositionsgraph darf an jeder Stelle übersetzen (Gabel über Kanten). Der Command geht hinaus, sobald die Übersetzung
läuft. Was lang, teuer oder auf anderer Hardware ist oder worüber die Domäne entscheiden soll, wird eine eigene Stufe: Graph → Aggregat → Events →
**Client** → nächster Graph (der Client sieht das Ergebnis über die Projektion und stößt die nächste Stufe an).

**Zwei Wege, Graphen zu komponieren.** Als Schachtel (Graph als Knoten: eine Instanz, gleicher Rechner) oder über eine Nachricht an den Kopf eines
anderen Graphen (eigene Instanz, eigener Rechner). Katalogbausteine sind fein, damit ein Setting Zwischenstände abgreifen kann.

**Zwei Modale.**
- Im **Funktions-Modal** entsteht der Vertrag einer Funktion: Eingänge · Auf · Ausgänge · Rahmen (Konfig, Technik, Zustand).
- Im **Graph-Modal** wird aus fertigen Verträgen komponiert, samt Übersetzungen.

### 1.1 Leitsatz: alles im Editor, und eine kleine Code-API

**Alles, was dieser Plan einführt, lässt sich im Editor anlegen, verbinden, ändern und löschen.** Das gilt auch für einen Katalog selbst:
Name und Inhalt bestimmt der Modellierer im Editor, kein Katalog ist im Framework vorgegeben. Jede Geste ist ein Patch am Code (W1, W4), der
Code bleibt die einzige Wahrheit.

**Was ein Entwickler im Code dafür kennen muss, ist wenig und fast alles schon da:**

| Absicht | Code | neu? |
|---|---|---|
| ein Katalog | ein Projekt `<Name>.Katalog` (die Rolle folgt aus dem Suffix, wie bei `.Pipeline`) | Rolle neu, kein Marker |
| eine Funktion | `sealed class X : IFunktion` mit `Auf(Nachricht) → Ausgänge` | nein |
| ihre Nachrichten | `record … : ICommand`, Hinweise `record … : ITransientEvent` | nein |
| Konfig, Technik, Zustand | Konstruktor: Konfig-Record, Schnittstelle, `IZustand<T>` | nein |
| ein Kompositionsgraph | `record X(…) : IGraph` mit `[Kopf]`, `[Von(nameof(…))]` | nein |
| eine Übersetzung | ein Knoten `Uebersetze<TEin, TAus>`, Abweichungen `[Feld(nameof(Ziel), nameof(Quelle))]` | **neu: ein Typ, ein Attribut** |

Neu im Code sind also genau ein generischer Knotentyp und ein Attribut. Alles andere ergibt sich aus Lage (Projekt, Ordner) und Signaturen.

## 2. Annahmen (sicherer Standard, gelten, solange Tobi nicht widerspricht)

- **A1 Feldzuordnung automatisch.** Für jeden Konstruktor-Parameter von `TAus` sucht der Generator eine Quelle, in dieser Reihenfolge:
  1. `[Feld]` am Knoten;
  2. eine Eigenschaft von `TEin` mit gleichem Namen, deren Typ in C# **implizit zuweisbar** ist;
  3. der Standardwert des Parameters;
  4. sonst ein Build-Fehler.

  Die Id des Ziel-Aggregats kommt also aus der Nachricht (ein Feld `Id<T>` gleichen Namens, oder ausdrücklich `[Feld]`, auch von einem `Guid`).
  Der frühere Rückgriff auf die Herkunft der Kette entfällt: keine Kette beginnt mehr in einem `Decide`.

  `Guid` → `Id<T>`, fremde Enums und fremde Wertobjekte werden nie stillschweigend zugeordnet.
- **A2 Die Zuordnung steht im Graph-Record**, als Typargumente und `[Feld]` am Knoten. Eine Übersetzung wiederverwenden heißt, den Knoten
  im anderen Graphen noch einmal hinzuschreiben.
- **A3 Kein Katalog-Command verlässt einen Kompositionsgraphen.** Jeder Fall wird übersetzt oder von einem Knoten genommen. Hinweise dürfen
  hinaus, an die Akteure, die sie hören. Ein `Verwirf<T>` kommt erst, wenn ein Setting es braucht.
- **A4 Anwendungsfunktionen bleiben vorerst erlaubt.** Funktionen mit Logik, die Domänentypen brauchen, bleiben in der Anwendung. Das sind die mit
  Lese-Fähigkeit auf einen Store (`RangeAufloesung`, `MitgliederEinfrieren`) und die mit Deutungslogik (`Bildeingang`). Isoliert ist, was im
  Katalog liegt. Ob Anwendungsfunktionen später ganz verschwinden, entscheidet Schritt 6 am Bestand.
- **A5 Generische Ausprägungen** (`BildErzeugt<Vorschau>`): Der Editor zeigt und verdrahtet sie, Anlegen kommt später (Schritt 8). Der Standard
  für verschiedene Bedeutungen sind eigene Typen.
- **A6 Ziehen aus dem Baukasten entfällt.** Eingesetzt wird über „＋“, der Baukasten bleibt zum Finden und Öffnen.
- **A7 Eine Funktion entsteht immer in einem Katalog oder in der Anwendung, nie lose auf der Fläche.** Der Picker der Fläche bietet „⬡ Graph“ und
  „📚 Katalog“, nicht mehr „ƒ Funktion“.
- **A8 Ein Katalog ist ein eigenes Projekt.** Nur so prüft der Compiler die Isolation, und nur so können zwei Anwendungen (Beispiel- und
  Prüfdomäne) denselben Katalog nutzen. Das Projekt legt der Editor an (Projektdatei, Eintrag in der Solution, Referenz der Anwendung), der
  Entwickler muss nichts davon von Hand tun.

## 3. Schritte

Jeder Schritt ist eine Scheibe: Code-API, Generator bzw. Analyzer, Sicht und Editor zusammen. **Abgenommen wird im Editor**: Was der Schritt
einführt, wird in der Werkbank angelegt, gebaut und (wo es läuft) durchgespielt. Lokal nur `dotnet build`, Tests ausschließlich über den
Messserver (`scripts/server.sh lauf scripts/tests.sh schnell`, im Hintergrund). Ein neues Konstrukt erweitert die Sonde (`soll.txt`) und das
Gesten-Skript der Sonde, eine neue Regel die Grammatik; die Doku zieht im selben Schritt mit.

### Schritt 1 — Drift aufräumen ✓ (d3ef6017, 232c3320)

IFunktion-Doku und docs/10-Beispiel ohne Port-Formen, `BausteinEingang.Ports` samt Join-Resten entfernt, Schalter „Schnittstelle (läuft
draußen)“ nur noch für Technik. Schnelle Stufe grün.

### Schritt 2 — Kataloge anlegen und füllen ✓ (7001b54f, 7a088472, im Editor abgenommen)

Umgesetzt: Rolle `Katalog` und Primitive `Hinweis` in `Projektstruktur`/`Lageart` (CQRS098 im Katalog: Fakt, Aggregat-Command, Aggregat
gehören nicht hinein), `Projektlage` zählt genutzte Kataloge zur Anwendung (auch leere), `--check` prüft die Referenzrichtung, Codegen und
Werkbank-Stand nehmen die Kataloge der Abläufe mit, Geste `/api/werkbank/katalog` (anlegen, nutzen; kaltes Neuladen danach), Baukasten je
Katalog mit „＋ 📚 Katalog“ und „＋ Funktion“, Picker der Fläche „📚 Katalog“, im Funktionsformular „＋ neuer Eingang“; Sonde mit fremdem
Katalog `Sondenkatalog.Katalog` als eigenem Projekt. Hinweise liegen im Katalog in `Events.cs` (wie bei der Schreibseite), nicht in `Hinweise.cs`.
Abgenommen in einer Probe-Werkbank: Katalog `Probe` angelegt (Projekt, Solution, Referenz), darin `Funk/Peilen` mit neuem Eingang und Ausgang,
ganze Kette bis zum Host gebaut (Codegen nimmt die Katalognachrichten in Leitung und Wire-Kontext). Offen für Schritt 3: ein neuer Command ohne Ziel
bekommt aus dem alten Formular noch ein `Guid AggregateId`-Feld.

- **Code:** `Projektstruktur.cs` bekommt die Rolle `Katalog` (Suffix `.Katalog`, referenziert nur `Abstractions`, Sammeldateien `Commands.cs`,
  `Events.cs` (Hinweise), `Konfigs.cs`, `Enums.cs`, `Graphen.cs`; Funktion und Technik je eigene Datei). Die Anwendung (`.Pipeline`) darf Kataloge
  referenzieren, Schreib- und Leseseite nicht (Lage-Prüfung CQRS098).
- **Codegen:** Nachrichten eines Katalogs kommen in Leitung und Proto der Anwendung, sobald ein Akteur sie hört oder hineingibt.
- **Werkbank-Server:** die Geste „neuer Katalog“ schreibt die Projektdatei aus der Tabelle (Analyzer-Referenz, `Abstractions`), trägt sie in die
  Solution ein und gibt der Anwendung die Referenz; der Arbeitsbereich lädt danach kalt neu (das tut er schon bei geänderten Projektdateien).
  „Katalog nutzen“ setzt bzw. entfernt die Referenz einer Anwendung auf einen vorhandenen Katalog.
- **Extractor und Sicht:** Kataloge mit ihren Funktionen, Nachrichten, Konfig und Technik; je Funktion ihr Ort (Katalog oder Anwendung).
- **Editor:** Der Baukasten ist je Katalog gruppiert, mit „＋ Katalog“ (Name eingeben) und je Katalog „＋ Funktion“. Eine neue Funktion öffnet
  vorerst das bisherige Formular; das Modal folgt in Schritt 3.
- **Sonde:** ein zweiter, fremder Katalog als eigene Compilation, dazu die Geste „neuer Katalog“ im Gesten-Skript.
- **Prüfdomäne:** Für die Integrationstests der späteren Schritte legen wir ihren Katalog im Editor an, als Teil der Abnahme.

**Fertig, wenn** sich in der Werkbank ein Katalog anlegen, mit einer Funktion samt Nachrichten füllen und von der Anwendung nutzen lässt, alles
baut, `--check` und `--sonde` grün sind und die schnelle Stufe grün ist.

### Schritt 3 — Das Funktions-Modal ✓ (ad3199e8, im Editor abgenommen)

Umgesetzt: `BausteinEingang.Asynchron` (die Form folgt aus Strom und Zahl der Ausgänge, eine eigene `Form` braucht es nicht), Scaffolder und Abgleich
schreiben `T`/`OneOf`/`IEnumerable` bzw. `Task`/`IAsyncEnumerable`; das Modal `funZeige` auf `innenZeige` (Spalten, Ports mit Vielfachheit, ＋ mit
gestrichelten Karten, ✎, ✕ mit Löschplan, „unverbunden“). Offen: neue Zustandsklasse (`IZustand<T>`) im Modal, Schalter „lauscht“ für eine neue Quelle,
`CancellationToken` als Schalter; die Technik heißt im Code noch „gerufen“.

- **Modell und Sicht:** `BausteinEingang` bekommt `Form` (eins, Wahl, Strom) und `Asynchron` statt `Strom`; Extractor liest, Scaffolder
  schreibt sie. Technik heißt im Modell Technik statt „gerufen“. Die Sicht rechnet je Funktion Operationen, Form und Vielfachheit der Ausgänge,
  Konstruktor, Lese-Fähigkeiten, Ort und Verwendung.
- **Editor:** gebaut auf `innenZeige` (Detailstufen, ✎, ＋ am Port, gestrichelte Karten, ✕ mit Löschplan). Spalten **Eingänge · Auf · Ausgänge ·
  Rahmen**; Eingang ↔ Auf 1:1, Auf → Ausgänge frei. ＋ Eingang (neue Nachricht samt `Auf`), ＋ Ausgang (Nachricht, Hinweis), ＋ Konfig,
  ＋ Technik (Schnittstelle mit Umsetzung), ＋ Zustand; am Auf die Form (genau eins, einer davon, keine bis viele), „asynchron“, Herkunft,
  CancellationToken, Lese-Fähigkeiten (nur Anwendungsfunktionen); Schalter „lauscht“ für eine Quelle. Katalogfunktionen bieten nur
  Katalognachrichten an, Anwendungsfunktionen auch „→ Command an ▣“ samt gestricheltem „＋ Auf für Antwort“.
- **Server:** Gesten für neue Funktion, Auf, Ausgang, Form, Konstruktor, Löschpläne (`Gesten.cs`, `DateiAbgleich.cs`, `Scaffolder.cs`). Das alte
  Formular bleibt nur für Technik.

**Fertig, wenn** sich eine Katalogfunktion samt Nachrichten, Konfig, Technik und Zustand vollständig im Modal anlegen und ändern lässt, baut und
die Werkbank-Abnahme grün ist.

### Schritt 4 — Das Graph-Modal ✓ (889602d8, im Editor abgenommen)

Umgesetzt im Editor (die Ports je Auf und ihre Vielfachheit folgen aus der Signatur im Modell; die Sicht blieb unverändert): Ports je Auf, Antworten
ohne ＋, ＋ an Ein- und Ausgängen mit gestrichelter „neue Funktion davor/dahinter“ (Ort: Anwendung oder ein Katalog, der den Typ kennt), „＋ Quelle“,
Empfänger am rechten Rand (Knoten eines anderen Graphen wird Kopf, neuer Graph mit Kopf), Baukasten mit „＋ einsetzen“ statt Ziehen, ✕ samt Funktion.
Neue Funktionen tragen die Id, die der Server ihnen gibt (`funktion:<Name>`), damit ein offenes Modal nach dem Schreiben offen bleibt.

- **Editor:** dieselbe Mechanik, Anordnung nach Tiefe, Rand links „von außen“, rechts „verlässt“. Knotenkarten mit Ports je `Auf` und
  Vielfachheit; Antwort-Eingänge ohne ＋; Doppelklick öffnet das Funktions-Modal bzw. eine Ebene tiefer. ＋ am Ausgang: nehmende Knoten,
  gestrichelt „neue Funktion dahinter“ (legt sie im gewählten Katalog bzw. in der Anwendung an und öffnet sie). ＋ am Eingang: gebende Knoten,
  `[Kopf]`, gestrichelt „neue Funktion davor“. Rand links: ＋ Quelle; rechts: Ziel je Ausgang, ⚠ „kein Empfänger“, ＋ an einen Graphen bzw.
  neuer Graph mit Kopf. Graphen komponieren: ＋ Knoten → Graph (Schachtel) oder Verbindung über den rechten Rand. Baukasten ohne Ziehen (A6),
  generische Ausprägungen angezeigt und verdrahtet (A5). Kanten gesichert ⇄ verlierbar, lösen; ✕ an Knoten mit Löschplan; Reiter Instanzen.
- **Server und Sicht:** Komposition mit Ports je Operation, Rändern, Zielen und offenen Fällen; Gesten für Knoten davor/dahinter, Kopf, Quelle,
  Verbindung, Schachtel.

**Fertig, wenn** sich ein Graph aus Katalogfunktionen vollständig im Editor anlegen, verbinden und als Instanz durchspielen lässt.

### Schritt 5 — Die Übersetzung und die Eingänge ✓ (5ff77b6e bis zum Abschluss-Commit, im Editor abgenommen)

Umgesetzt: `Uebersetze<TEin, TAus>` und `FeldAttribute` (`Abstractions/Uebersetzung.cs`), Lesart `Uebersetzungsform` (Vergleich über volle Namen,
weil der Extractor Vertragstypen mal aus Quelle, mal aus Metadaten sieht); `Graphform` kennt Übersetzungen als Knoten mit einer Operation (nie zwei
hintereinander, am Kopf an Knoten oder ein Aggregat); CQRS119/CQRS121 im `GraphAnalyzer`, CQRS120 im Gesamtbild, GR-UEBERSETZUNG in der Grammatik;
`GraphGenerator` erzeugt je Übersetzung einen `UebersetzungPlan`; das Kind rechnet sie inline vor dem Emitter (`KnotenAusgabe.Ueber` für die Wege
der Übersetzung), der Graph-Actor am Kopf (mit Emitter). Wirker ist der Knoten (`knoten:<instanz>/<knoten>`) — keine eigene Wirker-Art. Prüfdomäne:
Katalog `Rechenwerk.Katalog` (Skalierer), Graph `Hochrechnung`, Garantie-Test `UebersetzungE2ETests` (genau einmal, derselbe Kopf nach Neustart).
Sonde: `Funkortung` (Schreibseite) und `Ablaeufe.Funkortung` (bei den Abläufen, nur sie kennen den Katalog), eine Geste setzt den Ausgang samt `[Feld]`.
Editor: ↦-Karte, „＋ Eingang“ (Quelle oder Client samt Akteur), „↦ ▣ Command an …“ (vorhanden oder neu samt Decide, Event und Apply), im Aggregat
„＋ Graph, der ihn schickt“ und wer ihn schickt, kein „Command ohne Ziel“ unter den Ergebnissen, Picker „⬡ Graph“ nur mit erstem Knoten (auch eine
neue Funktion in Anwendung oder Katalog), ein neuer Graph öffnet sich. Abgenommen in einer Probe-Werkbank: Katalog → Graph mit neuer Katalogfunktion →
Client-Eingang mit Akteur → ↦ Command an ein Aggregat (auch als Gabel) → „＋ Graph, der ihn schickt“ → `[Feld]` lösen und neu setzen; jeder Stand baut
bis zum Host. Nicht im Editor durchgespielt (lokal kein Postgres); „genau einmal“ belegt der Garantie-Test auf dem Server.

- **Code:** `Abstractions`: `Uebersetze<TEin, TAus>` (versiegelt, nur als Knotentyp) und `FeldAttribute`. Eine Lesart `Uebersetzungsform.cs`
  (per Link in Generator, Analyzer, Extractor): Ports aus den Typargumenten, Zuordnung nach A1. `Graphform`: ein Knoten ist Funktion, Graph
  **oder Übersetzung**, Regeln unverändert. Eine Übersetzung kann Kopf sein (Eingang: Client-Nachricht ↦ Katalog-Eingang) oder hinten stehen
  (Ausgang: Funktionsnachricht ↦ Command an ein Aggregat).
- **Generator und Laufzeit:** je Übersetzungsknoten eine erzeugte Methode; Wegtabelle (Knoten, Typ) → Übersetzung; inline gerechnet (heraus im
  Kind vor dem Emitter, am Kopf beim Annehmen); Command-Id aus Aufruf und Stelle; Wirker `uebersetzung:<graph>/<knoten>`.
- **Analyzer** ab CQRS119: Zuordnung unvollständig oder Typ nicht zuweisbar; Katalog-Command verlässt den Kompositionsgraphen (A3);
  `TEin`/`TAus` keine Nachricht. Gegenstücke in `Grammatik.cs`.
- **Editor:**
  - Graph-Modal, linker Rand „＋ Eingang“: **Quelle** (Katalog oder eigene) oder **Client** (vorhandene oder neue Nachricht in der Anwendung, dazu
    der Akteur, der sie darf → `IDarf<T>`; Kopf ist eine Funktion der Anwendung, die sie nimmt, oder die Eingangs-Übersetzung in einen Katalog).
  - „＋“ an einem Ausgang bietet „→ ▣ Command an …“ (vorhanden oder neu samt `Decide`) und legt die Ausgangs-Übersetzung an.
  - Aggregat-Modal: an jedem Command „＋ Graph, der ihn schickt“ (neuer Graph im Bereich des Aggregats mit der Ausgangs-Übersetzung als letztem
    Knoten, öffnet ihn); unter den Ergebnissen eines `Decide` **kein „Command ohne Ziel“** mehr (weder Spaltenkopf noch gestrichelt).
  - Die **↦-Karte** zeigt links die Felder von `TEin`, rechts die Parameter von `TAus`, je Verbindung ihre Regel; ＋ an einem offenen Ziel hebt
    passende Quellen hervor, ein Klick schreibt `[Feld]`. Die Zuordnung rechnet der Server.
  - Der Picker der Fläche legt keinen leeren Graphen mehr an (der baut nicht, CQRS115): ein Graph entsteht mit seinem ersten Knoten (aus dem
    Aggregat, über „＋ Eingang“ oder mit einer Funktion).
- **Tests:** Prüfstand (Zuordnung, Wegtabelle, Inline-Rechnung), Analyzer-Tests, Prüfdomäne: eine Client-Nachricht über einen Katalog genau einmal
  an ein Aggregat, auch nach einem Neustart mitten in der Kette (Kategorie `Garantie`).
- **Doku:** docs/04 §4.4, 05, 10 (Grammatik, Regel, Entscheidung, Editor §10.9), 11.

**Fertig, wenn** im Editor ein Client-Eingang → Katalogfunktion → Ausgangs-Übersetzung → Command an ein Aggregat vollständig entsteht, baut, durchgespielt
(Konsole „als Akteur“) genau einmal schreibt und Garantie-Suite und schnelle Stufe grün sind.

### Schritt 6 — Bestand umziehen und Abbau

**Stand 2026-11-07:** die Beispieldomäne ist umgezogen — kein `Decide` gibt dort einen Auftrag aus. Die Bildaufbereitung ist ein Katalog
(`Bildvorschau.Katalog`), der Linienimport übersetzt sein Ergebnis (`BildGedeutet` ↦ `NimmBildAuf`). Auf die Fakten reagieren Clients: der
`ReaktionsWorker` (.NET, `Domain.Client.Worker`: Range, Einfrieren, Teilmeldung) und die Python-Worker (Klassifizieren auf `ImagePairKomplett`,
Training auf `TrainingAngefordert`, Abbruch über den Stand des Laufs). Dazu: die Command-Hülle trägt die Ursache (die Spur bleibt über eine
Reaktion lückenlos, ein Vorgang folgt ihr), die Log-Seite den Horizont (ein Client liest nach einer neuen Datenbank von vorn). Offen: die
Prüfdomäne (ihre Aufträge an Graph-Köpfe und die Arbeit des Rechnungsprüfers) und der Abbau mit der Build-Regel.

- Jeder Auftrag aus einem `Decide` wird ein Event, auf das ein Client reagiert: `PaarZuBeurteilen`, `TrainingZuRechnen`, `RangeAufzuloesen`,
  `EinfrierenAbzuschliessen`, `TeilFertig`, `AuftragAbzuwickeln` samt Gegenzügen, `Zubuchen` (Beispiel- und Prüfdomäne, Sonde).
- Abbau: Arbeit für Akteure (`IHoert<Command ohne Ziel>`, Werkstatt, `ArbeitsAuftrag`/`ArbeitsErgebnis`, `Umgebung:Hand`, Reiter „Aufträge“), der
  offene Eingang aus dem `Decide` (`cqrs_eingaenge` im selben Commit), die Kante „Arbeit“ der Fläche; danach eine Build-Regel: ein `Decide` gibt
  nur Events, Hinweise und Fristen aus. Die Garantie-Tests der Abläufe laufen danach über Clients.
- Die Bildaufbereitung wird ein Katalog (über den Editor bzw. per Verschieben); der Linienimport komponiert ihn mit Übersetzungen. `Bildeingang`
  bleibt Anwendungsfunktion oder wird Katalogfunktion plus Übersetzung (hier entscheiden). `TeilFertigMeldung` wird eine Übersetzung.
- A4 am Bestand neu entscheiden. Umgebungen ziehen mit, das Verhaltens-Netz bleibt grün.

**Fertig, wenn** die volle Stufe grün ist und kein `Decide` mehr einen Command ohne Ziel ausgibt.

### Schritt 7 — Fläche und Abnahme

- Picker der Fläche nach A7; die Fläche zeigt die obersten Graphen und ihre Verbindungen, Kataloge stehen im Baukasten; ein Command, den kein Graph und kein
  Akteur schickt, ist an seinem Aggregat sichtbar.
- Volle Stufe mit `--check`, `--sonde`, `--grammatik`, Codegen-Drift und `./pruefe-checkout.sh`; Werkbank-Abnahme.
- Doku: docs/04, 05, 07 (neue Rolle), 10 (Grammatik, Regeln, Entscheidungen, Editor §10.9), 11, 13, README, CLAUDE.md.

### Schritt 8 — später, nur bei Bedarf

- Anlegen generischer Ausprägungen (A5), `Verwirf<T>` (A3), `Neu = true` an `[Feld]` für anlegende Commands.

## 4. Was offen bleibt

- A4 (Anwendungsfunktionen ja oder nein) entscheiden wir am Bestand in Schritt 6.
- `Core/Katalog` bleibt der Katalog des Frameworks (Quellen) und folgt denselben Regeln; ob er als Projekt in die Rolle umzieht, entscheiden wir,
  wenn der Editor Kataloge anlegen kann.
