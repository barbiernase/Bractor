# Plan: Funktionskatalog, Übersetzung und die Modale für Funktion und Graph

Stand 2026-11-05, **Plan, nichts umgesetzt**. Er fasst die Diskussion vom 2026-11-04/05 zusammen und ersetzt den Entwurf
„Konzept: Funktionen, Graphen und ihre Nachrichten“. Umgesetzt wird schrittweise, je Schritt auf Tobis Wort (E5). Jeder Schritt baut für sich
und lässt alle Suiten grün.

## 1. Was entschieden ist

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
läuft. Was lang, teuer oder auf anderer Hardware ist oder worüber die Domäne entscheiden soll, wird eine eigene Stufe über das Aggregat: Graph →
Aggregat → Auftrag → Graph.

**Zwei Wege, Graphen zu komponieren.** Als Schachtel (Graph als Knoten: eine Instanz, gleicher Rechner) oder über eine Nachricht an den Kopf eines
anderen Graphen (eigene Instanz, eigener Rechner). Katalogbausteine sind fein, damit ein Setting Zwischenstände abgreifen kann.

**Zwei Modale.**
- Im **Funktions-Modal** entsteht der Vertrag einer Funktion: Eingänge · Auf · Ausgänge · Rahmen (Konfig, Technik, Zustand).
- Im **Graph-Modal** wird aus fertigen Verträgen komponiert, samt Übersetzungen.

## 2. Annahmen (sicherer Standard, gelten, solange Tobi nicht widerspricht)

- **A1 Feldzuordnung automatisch.** Für jeden Konstruktor-Parameter von `TAus` sucht der Generator eine Quelle, in dieser Reihenfolge:
  1. `[Feld]` am Knoten;
  2. eine Eigenschaft von `TEin` mit gleichem Namen, deren Typ in C# **implizit zuweisbar** ist;
  3. ein `Id<T>` eines Aggregats aus der Herkunft der Kette (`h.Id<T>()`), nur wenn jeder Kopf, von dem der Knoten erreichbar ist, aus einem
     `Decide` von `T` kommt;
  4. der Standardwert des Parameters;
  5. sonst ein Build-Fehler.

  `Guid` → `Id<T>`, fremde Enums und fremde Wertobjekte werden nie stillschweigend zugeordnet.
- **A2 Die Zuordnung steht im Graph-Record**, als Typargumente und `[Feld]` am Knoten. Eine Übersetzung wiederverwenden heißt, den Knoten
  im anderen Graphen noch einmal hinzuschreiben.
- **A3 Kein Katalog-Command verlässt einen Kompositionsgraphen.** Jeder Fall wird übersetzt oder von einem Knoten genommen. Hinweise dürfen
  hinaus, an die Akteure, die sie hören. Ein `Verwirf<T>` kommt erst, wenn ein Setting es braucht.
- **A4 Anwendungsfunktionen bleiben vorerst erlaubt.** Funktionen mit Logik, die Domänentypen brauchen, bleiben in der Anwendung. Das sind die mit
  Lese-Fähigkeit auf einen Store (`RangeAufloesung`, `MitgliederEinfrieren`) und die mit Deutungslogik (`Bildeingang`). Isoliert ist, was im
  Katalog liegt. Ob Anwendungsfunktionen später ganz verschwinden, entscheidet Schritt 6 am Bestand.
- **A5 Generische Ausprägungen** (`BildErzeugt<Vorschau>`): Der Editor zeigt und verdrahtet sie, Anlegen kommt später (Schritt 10). Der Standard
  für verschiedene Bedeutungen sind eigene Typen.
- **A6 Ziehen aus dem Baukasten entfällt.** Eingesetzt wird über „＋“, der Baukasten bleibt zum Finden und Öffnen.
- **A7 Eine Funktion entsteht immer in einem Katalog oder in der Anwendung, nie lose auf der Fläche.** Der Picker der Fläche bietet „⬡ Graph“ und
  „📚 Katalog“, nicht mehr „ƒ Funktion“.

## 3. Schritte

Je Schritt: lokal nur `dotnet build`, Tests ausschließlich über den Messserver (`scripts/server.sh lauf scripts/tests.sh schnell`, im
Hintergrund). Ein neues Konstrukt erweitert die Sonde (`soll.txt`), eine neue Regel auch die Grammatik. Die Doku zieht im selben Schritt mit.

### Schritt 1 — Drift aufräumen (klein, ohne Verhaltensänderung)

- Die XML-Doku von `IFunktion` (`Abstractions/Funktion.cs`) erlaubt keine Port-Formen mehr (GR-EINGANG).
- Das Beispiel in docs/10 §10.3.4 übernimmt die Kette der Sonde statt `GutachtenErhalten?`.
- `BausteinEingang.Ports`, die Join-Kommentare in `Scaffolder.FunktionsParameter` und `DateiAbgleich.Nimmt` fallen weg.
- Im Funktionsformular fällt der Schalter „Schnittstelle (läuft draußen)“ weg (E9).

**Fertig, wenn** der Build und die schnelle Stufe grün sind.

### Schritt 2 — Der Katalog als Rolle

- `Projektstruktur.cs`: neue Rolle `Katalog`, Suffix `.Katalog`, referenziert nur `Abstractions`. Sammeldateien: `Commands.cs`, `Hinweise.cs`,
  `Konfigs.cs`, `Enums.cs`, `Graphen.cs`; Funktion und Technik je eigene Datei. Die Rolle `Ablaeufe` (Pipeline) darf Kataloge referenzieren,
  Schreib- und Leseseite dürfen es nicht.
- `Lageart.cs`/`LageAnalyzer` (CQRS098): Primitive im Katalog, Prüfung der Referenzrichtung. Ein Hinweis aus einer Funktion liegt im Katalog bzw.
  in der Anwendung, nicht mehr bei der Schreibseite.
- Codegen (`Cqrs.Codegen`): Nachrichten eines Katalogs kommen in Leitung und Proto, sobald ein Akteur sie hört (Hinweise) oder hineingibt.
- Extractor und Werkbank laden die Kataloge, die eine Anwendung referenziert, und zeigen sie im Baukasten je Katalog.
- Ein Probekatalog `Pruefkatalog.Katalog` mit zwei Funktionen für die Prüfdomäne; die Sonde bekommt einen zweiten, fremden Katalog als eigene
  Compilation.

**Fertig, wenn** `--check` und `--sonde` grün sind und der Probekatalog baut und in der Werkbank erscheint.

### Schritt 3 — Die Übersetzung im Framework

- `Abstractions`: `Uebersetze<TEin, TAus>` (versiegelt, ohne Mitglieder, nur als Knotentyp) und `FeldAttribute(ziel, quelle)`.
- Eine Lesart `Uebersetzungsform.cs` (wie `Graphform`, per Link in Generator, Analyzer und Extractor): Ports aus den Typargumenten, Zuordnung
  nach A1, Herkunfts-Prüfung über die Köpfe des Graphen und die Signaturen der `Decide`.
- `Graphform`: Ein Knoten ist eine Funktion, ein Graph **oder eine Übersetzung**. Die Regeln zu Ports, Köpfen und Kanten gelten unverändert.
- `GraphGenerator`/`FunktionsBausteinGenerator`: je Übersetzungsknoten eine erzeugte Methode
  (`static VermerkeModell Ergebnis(ModellTrainiert e, Herkunft h) => new(h.Id<Datensatz>(), e.ModellPfad, e.Genauigkeit)`). In der Wegtabelle
  führt (Knoten, Typ) auf die Übersetzung.
- Laufzeit (`Kette.cs`, Kind, Graph-Actor): die Übersetzung inline rechnen, heraus vor dem Emitter, am Kopf vor dem ersten Knoten. Die
  Command-Id kommt wie bisher aus Aufruf und Stelle. Wirker in der Spur ist `uebersetzung:<graph>/<knoten>` (`WirkerId`).
- Analyzer, neue Regeln ab CQRS119:
  - Zuordnung unvollständig oder Typ nicht zuweisbar;
  - `Id<T>` nicht aus der Herkunft ableitbar;
  - Katalog-Command verlässt den Kompositionsgraphen (A3);
  - `TEin` bzw. `TAus` ist keine Nachricht.

  Jede Regel bekommt ihr Gegenstück in `Grammatik.cs`.
- Tests:
  - Prüfstand: Zuordnung, Wegtabelle, Inline-Rechnung, store-frei;
  - Analyzer-Tests;
  - Prüfdomäne: ein Kompositionsgraph mit dem Probekatalog, der genau einmal an ein Aggregat schreibt, auch nach einem Neustart mitten in der
    Kette (Kategorie `Garantie`).
- Doku: docs/04 §4.4, docs/05, docs/10 (Grammatik, R-Regel, Entscheidung), docs/11.

**Fertig, wenn** die schnelle Stufe grün ist, die Garantie-Suite grün ist und `--grammatik` die neuen Regeln listet.

### Schritt 4 — Modell und Sicht für Funktionen

- `EditorModell`: `BausteinEingang` bekommt `Form` (eins, Wahl, Strom) und `Asynchron` statt `Strom`. Der Extractor liest die Form aus der
  Signatur, der Scaffolder schreibt sie.
- Technik heißt im Modell und in der Sicht Technik, nicht mehr „gerufen“ (`istGerufen`, `GerufenZu`).
- Die Sicht rechnet je Funktion:
  - ihre Operationen mit Eingang (Nachricht oder Antwort),
  - Form und Vielfachheit der Ausgänge,
  - Konstruktor (Konfig, Technik, Zustand, Logger),
  - Lese-Fähigkeiten,
  - Ort (Katalog oder Anwendung),
  - Verwendung in Graphen.

**Fertig, wenn** der `--check` grün ist und die Sonde die Form je `Auf` in `soll.txt` führt.

### Schritt 5 — Das Funktions-Modal

Gebaut auf der Mechanik von `innenZeige`: Detailstufen, ✎, ＋ am Port, gestrichelte Karten, ✕ mit Löschplan.

- Spalten: **Eingänge · Auf · Ausgänge · Rahmen.**
  - Eingang ↔ Auf ist 1:1 (R2).
  - Auf → Ausgänge ist frei.
  - Im Rahmen stehen Konfig, Technik, Zustand und Logger.
- **Anlegen:**
  - ＋ Eingang: neue Nachricht samt `Auf`, im Namespace der Funktion;
  - ＋ Ausgang: neue Nachricht oder neuer Hinweis;
  - ＋ Konfig: neuer Record, nur bindbare Feldtypen;
  - ＋ Technik: Schnittstelle mit Umsetzung;
  - ＋ Zustand: Klasse.
- **Am Auf:** die Form als Schalter (genau eins, einer davon, keine bis viele) und „asynchron“, dazu Herkunft, CancellationToken und
  Lese-Fähigkeiten (bei Anwendungsfunktionen).
- **Katalogfunktionen** bieten nur Katalognachrichten an. Anwendungsfunktionen dürfen zusätzlich „→ Command an ▣“ ausgeben; das Antwort-Event
  steht dann gestrichelt als „＋ Auf für Antwort“ daneben.
- **Quelle:** Der Schalter „lauscht“ macht aus der Funktion eine Quelle (`Lausche`).
- **Neue Funktion:** aus dem Graph-Modal (Schritt 7) oder über „📚 Katalog → ＋ Funktion“.
- Server: Gesten für neue Funktion, neues und entferntes Auf, Ausgang, Form, Konstruktor, Löschpläne (`Gesten.cs`, `DateiAbgleich.cs`,
  `Scaffolder.cs`). Das alte Formular `funktionsKarte` bleibt nur für Technik.

**Fertig, wenn** sich eine Katalogfunktion samt Nachrichten, Konfig, Technik und Zustand vollständig im Modal anlegen lässt, baut und die
Werkbank-Abnahme grün ist.

### Schritt 6 — Bestand umziehen

- Die Bildaufbereitung (`Bildeinlesen`, `Bildverkleinerung`, `Kontrastausgleich`, Technik, `BildNichtLesbar`) wird zu
  `Bildverarbeitung.Katalog`. Der Linienimport komponiert sie in der Anwendung. `Bildeingang` bleibt Anwendungsfunktion (Deutung des Dateinamens,
  A4) oder wird Katalogfunktion mit `Guid Paar` plus Übersetzung. Das entscheiden wir hier.
- `TeilFertigMeldung` wird `Uebersetze<TeilFertig, MeldeTeilFertig>`.
- Prüfdomäne: Die reinen Abbildungen (Teile von `Auftragsabwicklung`, `Gutschrift` …) werden geprüft und, wo sie nur zuordnen, zu Übersetzungen.
- Am Bestand entscheiden wir A4 neu: Bleiben Anwendungsfunktionen, oder ziehen auch Funktionen mit Store-Zugriff um?
- Das Verhaltens-Netz (`Verhalten.Tests`) bleibt grün, die Umgebungen (`umgebungen/*/graphen/*.json`) ziehen mit.

**Fertig, wenn** die volle Stufe grün ist und die Fläche die Graphen wie vorher zeigt.

### Schritt 7 — Das Graph-Modal

- Gebaut auf derselben Mechanik, Anordnung nach Tiefe der Kette, Rand links „von außen“ und rechts „verlässt“.
- Knotenkarten mit Ports je `Auf` und Vielfachheit; Antwort-Eingänge ohne ＋ (Rücklauf vom Aggregat); Doppelklick öffnet das Funktions-Modal,
  bei einem Graphen im Graphen eine Ebene tiefer.
- **＋ am Ausgang** hebt die nehmenden Knoten hervor. Gestrichelt stehen „neue Funktion dahinter“ (Funktion mit `Auf(T)` und Knoten, öffnet das
  Funktions-Modal) und „→ ▣ Aggregat“ (Übersetzungsknoten samt Command, vorhanden oder neu mit `Decide` beim Aggregat).
- **＋ am Eingang** hebt die gebenden Knoten und den Rand `[Kopf]` hervor. Gestrichelt steht „neue Funktion davor“.
- Die **Übersetzungskarte ↦** zeigt links die Felder von `TEin` und rechts die Parameter von `TAus`. Jede Verbindung nennt ihre Regel (Name,
  Herkunft, Standard), ein offenes Ziel hat ein ＋, das die passenden Quellen hervorhebt; ein Klick schreibt `[Feld]`. Die Zuordnung rechnet der
  Server.
- **Rand links:** ＋ Quelle, ＋ Kopf aus einem Auftrag der Domäne (setzt einen `Uebersetze`-Kopf). **Rand rechts:** je Ausgang sein Ziel; ⚠ für
  einen offenen Fall aus dem Katalog (A3) und „kein Empfänger“; ＋ an einen Graphen (Verbindung) bzw. neuer Graph mit Kopf für den Typ.
- **Graphen komponieren:** ＋ Knoten → Graph (Schachtel) oder Verbindung über den rechten Rand. Der Baukasten ist nach Kataloge gruppiert, ohne
  Ziehen (A6). Generische Ausprägungen werden angezeigt und verdrahtet (A5).
- Kanten: gesichert ⇄ verlierbar, lösen (wie heute); ✕ an Knoten mit Löschplan. Der Reiter Instanzen bleibt.

**Fertig, wenn** sich der Trainingslauf aus §1 vollständig im Editor anlegen lässt (Graph, Übersetzungen, neuer Command samt Decide), der
erzeugte Code baut und die Werkbank-Abnahme ihn durchspielt.

### Schritt 8 — Fläche und Baukasten

- Picker der Fläche nach A7.
- Die Fläche zeigt die obersten Graphen und die Verbindungen zwischen ihnen. Kataloge stehen im Baukasten, nicht auf der Fläche.
- Ein Auftrag der Domäne ohne Graph, der ihn nimmt, ist an seinem Aggregat sichtbar („wartet auf einen Graphen“).

### Schritt 9 — Abnahme und Doku

- Volle Stufe (`tests.sh voll`) mit `--check`, `--sonde`, `--grammatik` und Codegen-Drift; dazu `./pruefe-checkout.sh`.
- Doku: docs/04, 05, 07 (neue Rolle), 10 (Grammatik, Regeln, Entscheidungen E17 ff., Editor §10.9), 11, 13 (offene Punkte), README, CLAUDE.md
  (tragendes Bild: Katalog, Übersetzung).
- Messung nur nach Frage. Die Inline-Übersetzung sollte die Kette nicht messbar verlangsamen.

### Schritt 10 — später, nur bei Bedarf

- **Anlegen generischer Ausprägungen** (A5): Record mit Typparametern im Modell, Ausprägungen als eigene Karte, „＋ neue Ausprägung“.
- **`Verwirf<T>`** (A3), wenn ein Setting einen Katalogfall bewusst fallen lassen will.
- **`Neu = true`** an `[Feld]` für anlegende Commands (`h.NeueId<T>(knoten)`).

## 4. Was offen bleibt

- A4 (Anwendungsfunktionen ja oder nein) entscheiden wir am Bestand in Schritt 6.
- Wie heißen die Kataloge der Beispiel- und der Prüfdomäne? Vorschlag: `Bildverarbeitung.Katalog`, `Pruefkatalog.Katalog`.
- Was wird aus `Core/Katalog`? Es bleibt der Katalog des Frameworks und folgt denselben Regeln, als Rolle wird es nicht umbenannt.
