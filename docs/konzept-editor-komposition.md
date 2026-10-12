# Konzept: Der Editor als Kompositions-Sprache

> **Gekürzt 2026-10-12** auf das mit dem Zielbild `docs/konzept-werkbank.md` Vereinbare.

> Stand 2026-09-30 · Konzept; **Grammatik umgesetzt, Kapselung nur im Modell** (§11) — die Ebenen-Ansicht im Editor
> ist auf Wunsch zurückgenommen (kommt später, bewusst nicht automatisch). Übergeordnet zu `docs/konzept-editor-pipelines.md` (Pipelines sind eine
> Anwendung dieser Sprache) und zu `docs/konzept-domaenen-editor.md` (Palette, Bedienung). Programmiermodell:
> `docs/konzept-handle-ausgaenge.md` §10–§13.

## 1 · Anspruch

**Mit dem Editor lassen sich beliebig komplexe Domänen, Prozesse und Pipelines entwerfen, nicht nur bestehende abbilden.**
Beliebig komplex heißt: beliebig viele Bausteine, beliebig tief verschachtelt, mit Verzweigung, Zusammenführung, Schleifen,
Zeit und Rückkopplung. Trotzdem bleibt jede Ansicht lesbar, und alles Gezeichnete wird zu kompilierendem, geprüftem Code.

Das geht nicht mit mehr Knoten auf einer Fläche. Es geht wie bei jeder Sprache mit fünf Teilen:

| Teil | Frage | Abschnitt |
|---|---|---|
| **Alphabet** | Welche Bausteine und welche Nachrichtensorten gibt es? | §2 |
| **Grammatik** | Was darf man womit verbinden (und was kompiliert dann garantiert)? | §3 |
| **Operatoren** | Wie komponiert man Verhalten: Folge, Verzweigung, Schleife, Zeit …? | §4 |
| **Kapselung** | Wie bleibt Beliebiges überschaubar? Jeder Teilgraph wird ein Baustein. | §5 |
| **Muster** | Wie verwendet man Bewährtes wieder? | §6 |

Leitsatz: **Wenn es sich verbinden lässt, kompiliert es. Wenn es kompiliert, lässt es sich einlesen.** Der Editor erlaubt nur
Verbindungen, die ein Analyzer oder der Compiler ohnehin erzwingt, und der Extractor liest alles aus Signaturen zurück
(Fixpunkt, `--sonde`).

## 2 · Alphabet

### 2.1 Nachrichtensorten (die Kanten)

Jede Kante trägt genau eine Sorte. Die Sorte bestimmt Kardinalität und Garantie (Invariante 6):

| Sorte | Bedeutung | Ziel-Kardinalität | Garantie |
|---|---|---|---|
| **Command** | Absicht | genau **ein** Aggregat | wie der Absender (ab Event idempotent) |
| **Event** | Fakt im Log | **beliebig viele** Konsumenten | durabel |
| **Transientes Event** | Hinweis, Ablehnung | beliebig viele | verlierbar |
| **Quell-Nachricht** | Anstoß von außen (Datei, Webhook …), startet einen Vorgang | beliebig viele Flüsse | durabel (genau einmal je Kennung) |
| **Auftrag** | Aufruf einer Katalog-Funktion (`Rufe<F>`) | genau **eine** Funktion | durabel (Ergebnis im Log, Ausführungs-Id) |
| **Query → Response** | Lesen | genau ein Reader | synchron |
| **Fähigkeit** | Store-Funktion (Lesen/Schreiben) | genau ein Store | im Handle-Aufruf |

### 2.2 Bausteine (die Knoten)

Jeder Baustein ist **eine Funktion mit typisierten Ein- und Ausgängen**. Das Muster ist immer dasselbe: Eingang → Rumpf (frei)
→ OneOf der Ausgänge. Das Was steht in der Signatur, das Wann im Rumpf.

| Baustein | Eingang | Ausgänge | Zustand |
|---|---|---|---|
| **Aggregat** (Decide/Apply) | Command | OneOf Events (inkl. Ablehnung) | **ja**, die einzige Zustandsstelle |
| **Prozess** (Regel) | Event-Join (UND, Zählen) | Command (+ Kompensation) | vom Framework (Marking) |
| **Projektion** | Event | Schreib-Fähigkeiten, transiente Events | im Store (co-committet) |
| **Reaktion** | Event | Commands, transiente Events | keiner |
| **Reader** | Query | OneOf Responses, Lese-Fähigkeiten | keiner |
| **Fluss** (Pipeline) | Quell-Nachricht / Event (`p.Quelle<…>()` / `p.Auf<…>()`) | Commands, Aufträge | vom Framework (der Vorgang im Log) |
| **Katalog-Funktion** | Auftrag (+ Lese-Fähigkeiten) | OneOf Ergebnis-Events | keiner |
| **Store + ReadModel** | Fähigkeiten | – | ja (Lesemodell) |

## 3 · Grammatik: Verbindungsregeln

Die Ports sind nach Sorte typisiert. Eine Kante ist nur erlaubt, wenn Ausgangs- und Eingangs-Sorte zusammenpassen und die
Kardinalität stimmt. Beispiel: Ein Command-Ausgang geht an genau einen Decider-Eingang. Ein Auftrag geht an genau eine
Katalog-Funktion. Ein Event darf an beliebig viele Konsumenten gehen.

| Ausgang ↓ · Eingang → | Aggregat | Prozess | Projektion | Reaktion | Fluss | Funktion | Reader |
|---|---|---|---|---|---|---|---|
| **Command** | ✔ (genau 1) | – | – | – | – | – | – |
| **Event** | – | ✔ | ✔ | ✔ | ✔ | – | – |
| **Transientes Event** | – | – | ✔ | ✔ | – | – | – |
| **Quell-Nachricht** | – | – | – | – | ✔ | – | – |
| **Auftrag** | – | – | – | – | – | ✔ (genau 1) | – |
| **Query** | – | – | – | – | – | – | ✔ (genau 1) |

Zusätzliche Regeln, jede mit ihrem Gegenstück im Build:

- **Geschlossener Ausgang:** Ausgänge sind konkrete Typen im OneOf (CQRS050). Zeichnen heißt Typ wählen.
- **Handle-Form folgt dem Inhalt:** keine Ausgabe ⇒ `Task`, sonst Strom mit OneOf (CQRS057, §13 dort).
- **Garantie-Regel:** Führt ein verlierbarer Pfad in einen Schritt, der Durabilität braucht, wird die Kante markiert
  (gestrichelt) und ein Analyzer warnt (offen, §11.1).
- **Zyklus-Regel:** Ein Kreis Command → Event → … → Command ist erlaubt, wenn er durch einen **Zustandsschritt** läuft
  (Aggregat); sonst verbietet ihn der Azyklizitäts-Guard (für Prozesse aktiv, für Reaktionen offen). Ein Fluss ist per
  Konstruktion azyklisch.

## 4 · Operatoren: wie man Verhalten komponiert

Es gibt keine neuen Laufzeit-Konzepte. Jeder Operator ist ein **Muster aus Bausteinen und Kanten**, das der Editor als eine
Geste anbietet und der Scaffolder in die bekannten Code-Formen schreibt.

| Operator | Bedeutung | Aus welchen Bausteinen |
|---|---|---|
| **Folge** | A, dann B | Kante: Command → Aggregat → Event → Konsument; im Fluss ein Draht (`knoten.Bei<Fall>()`) |
| **Verzweigung** | A führt zu B *oder* C | OneOf-Ausgänge; die Wahl trifft der Rumpf; im Fluss führt ∨ `.Oder(…)` die Wege wieder zusammen |
| **Auffächern** | A führt zu B *und* C *und* … | ein Event an mehrere Konsumenten; `SendeJe` für Laufzeit-Mengen; im Fluss `p.Je(draht, x => x.Liste)` |
| **Zusammenführen (UND)** | erst wenn A *und* B | Prozess-Join (`Auf<A>().Und<B>()`); im Fluss ∧ `p.Alle(…)` |
| **Zusammenführen (N)** | erst wenn alle N | Barriere-Aggregat (Zähler, N als Laufzeitwert), nicht als Collection; im Fluss `je.Sammle(…)` |
| **Schleife** | wiederhole | nur über ein Aggregat: dessen Event ist die Quelle des nächsten Vorgangs (`p.Auf<E>()`); ein Fluss selbst ist azyklisch |
| **Warten / Zeitlimit** | wenn bis dann nicht … | `quelle.Strom().Warte<A, B>().Zeitlimit(t)`; `.Zeitlimit(t)` am Knoten mit Draht `BeiZeitlimit()` |
| **Rückkopplung** | Ergebnis beeinflusst den nächsten Schritt | Command → Aggregat (Zustand) → Event → zurück |
| **Kompensation** | mach rückgängig | `RückgängigDurch` im Prozess |
| **Lesen im Schritt** | Entscheidung mit Wissen | Lese-Fähigkeit als Parameter |

**Regel Z (Zustand):** Zustand lebt **nur** in Aggregaten (und in Lesemodellen). Reaktionen, Funktionen und Handles sind
zustandslose Übersetzer; ein Fluss kennt nur seinen Vorgang. Das macht beliebig große Kompositionen analysierbar, denn jede
Schleife und jedes Gedächtnis ist eine sichtbare Kante durch ein Aggregat.

## 5 · Kapselung: warum beliebige Komplexität lesbar bleibt

**Jeder Teilgraph kann zu einem Baustein zusammengeklappt werden, und das beliebig tief.** Pro Ebene sieht man wenige Knoten.
Tiefe ersetzt Breite.

- **Modul = Namespace.** Das ist ein Code-Fakt: Der Scaffolder schreibt schon je Namespace, der Extractor kennt ihn. Ein Modul
  kann Aggregate, Prozesse, Pipelines, Projektionen und Untermodule enthalten. Verschachtelung = Namespace-Hierarchie.
- **Die Schnittstelle wird abgeleitet, nicht deklariert.** Eingangs-Ports eines Moduls sind die Nachrichtensorten, die von außen
  hineinlaufen (Commands an seine Aggregate, Events, die seine Konsumenten hören, Queries). Ausgangs-Ports sind die, die
  hinauslaufen. Eingeklappt ist ein Modul also wieder ein Baustein mit typisierten Ports, und die Grammatik aus §3 gilt für
  ihn wie für jeden anderen.
- **Kanten über Modulgrenzen** laufen immer durch einen Schnittstellen-Port. Auf jeder Ebene verbinden Kanten nur Geschwister.
  Das hält jedes Bild frei von Fernkanten, die quer über das Board laufen.
- **Öffentlich vs. intern (optional, Code-Fakt):** Nachrichten, die ein Modul nach außen zeigen soll, sind `public`, interne
  sind `internal`. Dann ist die Schnittstelle nicht nur abgeleitet, sondern auch erzwungen (vom Compiler).
- **Entwerfen von oben nach unten:** Man zeichnet ein eingeklapptes Modul mit Ports (z. B. „nimmt Auftrag an, meldet
  erledigt“), klappt es auf und füllt es. Die Ports werden zu den Typen, die innen erzeugt oder konsumiert werden müssen.
  Offene Ports („Eingang ohne Konsument“, „Ausgang ohne Erzeuger“) meldet der Build (Analyzer, offen, §11.1).

## 6 · Muster: Wiederverwendung

Ein Muster ist ein **parametrisierter Teilgraph** mit Schnittstelle, z. B.:

| Muster | Parameter | Expandiert zu |
|---|---|---|
| **Watchdog** | Quelle, End-Events, Zeitlimit | Fluss mit `quelle.Strom().Warte<A, B>().Zeitlimit(t)`, Draht `BeiZeitlimit()` |
| **Barriere** | Teil-Event, Fertig-Event | Zähl-Aggregat (Erwartet/Fertig/Abgeschlossen) + Prozess-Kanten |
| **Wiederholen mit Zeitlimit** | Versuch-Command, Erfolg/Fehlschlag | Aggregat mit Versuchszähler + Rückkopplung |
| **Saga** | Schritte, Kompensationen | Prozess-Regeln mit `RückgängigDurch` |
| **Anreicherung** | Eingang, Lese-Fähigkeit, Ausgang | Katalog-Funktion mit Lese-Fähigkeit als Parameter |

**Code-Träger (Entscheidung):** Der Extractor erkennt nur Code-Fakten, also kein Muster-Raten aus der Graphform.

- **Stufe 1, Makro:** Ein Muster wird beim Einfügen in Bausteine *expandiert*. Danach sind es gewöhnliche Bausteine; eine
  „Muster-Klammer“ gibt es dann nicht (außer der Ansichts-Datei hält der Editor keinen eigenen Zustand).
- **Stufe 2, Muster als Code-Fakt:** z. B. ein Attribut `[Muster(typeof(Watchdog))]` auf dem erzeugten Modul oder ein generischer
  Framework-Baustein. Damit steht die Klammer im Code, und ein geändertes Muster kann neu expandiert werden.

## 10 · Offene Entscheidungen (mit Empfehlung)

1. **Modulgrenze = Namespace?** Empfehlung: ja (Code-Fakt, heute schon Ordnungseinheit). Alternative: eigenes Attribut.
2. **Schnittstelle nur abgeleitet oder auch erzwungen (`public`/`internal`)?** Empfehlung: erst abgeleitet, erzwingen als Option
   je Modul.
3. **Regel Z verbindlich (kein Zustand außerhalb von Aggregaten/Lesemodellen)?** Empfehlung: ja, als Analyzer auf Felder in
   Projektionen/Reaktionen (ausgenommen Konfig/Dienst/Logger).
4. **Muster: Makro oder Code-Fakt?** Empfehlung: Makro zuerst, Code-Fakt, sobald ein Muster geändert und neu expandiert werden soll.

**Entschieden in der Umsetzung (2026-09-30), je der Default:** (1) Modulgrenze = Namespace — ja. (2) Schnittstelle nur
abgeleitet; `public`/`internal` als Erzwingung bleibt offen. (3) Regel Z vorerst **nur als Hinweis** (`GR-ZUSTAND`, Schwere
info); als Analyzer offen. (4) Muster nicht Teil dieser Runde.

## 11 · Umgesetzt (2026-09-30)

### 11.1 Grammatik als EINE Quelle

**Was.** Die Tabelle aus §3 (Sorte × Baustein-Eingang mit Kardinalität), die Erzeugungs-Spalte aus §2.2 (wer darf welche Sorte
erzeugen) und die Zusatzregeln liegen **einmal** im Code: `DomainEditor/Grammatik.cs` (Roslyn-frei). Jede Regel hat eine Id, einen
Namen, einen Satz und ihr **Build-Gegenstück** (Analyzer-/Generator-ID, Compiler, Boot-Guard oder „offen"). Daraus gespeist:

| Stelle | Wie |
|---|---|
| **Editor** | GraphExtractor legt die Grammatik als `rahmen.grammatik` ins Board (`ModellMapper.ZuBoardJson`), samt der Abbildung Editor-Port → Sorte/Baustein (`portSorte`, `portBaustein`, `recordSorte`, `ausgangSorte`). Das JS kodiert keine Regel: `grPruefe(O, I)` prüft jede Verbindung (Nachricht → Eingang: Sorte + Kardinalität; Ausgang → Nachricht: Erzeugung). Im Verbinden-Modus leuchten nur erlaubte Ziele; typgleiche, aber verbotene Ziele sind rot „✕ Regel" und nennen beim Klick die Regel beim Namen (Modus bleibt, nichts wird verbunden). **📐 Grammatik** zeigt die Tabelle und „Regel → Build-Gegenstück". |
| **Analyzer (Backlog)** | Prüfen = Bauen: jede Regel wird Analyzer oder Boot-Guard und meldet unter ihrer Regel-Id mit dem Satz „Regel »Name« (Id; Build: …)". Zu prüfen auf dem **Nachrichtenfluss** (Erzeuger → Nachricht → Konsument aus Decide-OneOf, Apply, Prozess-Regeln, Handle-Eingang/-Ausgängen/-Fähigkeiten): Sorte × Eingang, Kardinalität (`GR-COMMAND`, `GR-QUERY`, `GR-FALTUNG`), Erzeugung (`GR-AUS-*`), **Garantie** (Command ab transientem Event), **Zyklus ohne Zustandsschritt**, **Regel Z** (Hinweis), offene Modul-Ports. |
| **Build-Gegenstück** | `dotnet run --project GraphExtractor -- --grammatik` druckt die Liste. Ein Test/Build-Gate prüft, dass jede genannte ID als Diagnose-Literal im Code existiert, und listet die Regeln ohne Gegenstück. |

**Regel Z als Code-Fakt.** Der Extractor liest je Projektion/Reaktion die **Zustandsfelder** aus dem Symbol (kein Rumpf):
Instanzfeld, das nicht `readonly` ist, oder `readonly` mit einem Referenztyp, der weder Konstruktor-Parametertyp (injiziert: Konfig,
Dienst, Logger) noch `string` ist. Träger: `Konsument.Zustand` (nur gelesen, nie geschrieben; die Felder stehen verbatim im `Zusatz`).

**Offen (bewusst, nur gelistet):** die Analyzer für `GR-QUERY`, `GR-GARANTIE`, `GR-ZUSTAND`, `GR-FALTUNG`,
`GR-MODUL-AUSGANG-OFFEN` und die Erweiterung des Azyklizitäts-Guards auf Reaktionen. Transiente Events haben im Editor noch keinen
Konsum-Port (die Grammatik erlaubt sie, die Oberfläche bietet sie nicht an).

### 11.2 Kapselung

**Was.** Jeder Namespace ist ein Modul; die Hierarchie ist die Namespace-Hierarchie. `DomainEditor/Module.cs` leitet aus dem
Nachrichtenfluss die Ports ab: eine Kante Erzeuger → Nachricht bzw. Nachricht → Konsument, deren Enden auf verschiedenen Seiten der
Modulgrenze liegen, ist ein **Eingang** (Ziel drinnen) bzw. **Ausgang** (Quelle drinnen); die Außenwelt liegt außerhalb jedes Moduls.
Dazu die **offenen** Ports: eine Nachricht im Modul, die einen Konsumenten braucht und keinen hat (Command, Query →
„Eingang ohne Konsument", `GR-MODUL-EINGANG-OFFEN`), bzw. einen Erzeuger braucht und keinen hat (Event, Ablehnung, Response →
„Ausgang ohne Erzeuger", `GR-MODUL-AUSGANG-OFFEN`). Die Grammatik gilt für Modul-Knoten wie für jeden Baustein (dieselbe
Kardinalitäts-Prüfung beim Verbinden, auch wenn das Ziel eingeklappt ist).

**Editor: zurückgenommen (2026-10-01).** Eine erste Ebenen-Ansicht (Module eingeklappt als Karten mit Ports, aufklappen =
hineingehen) war gebaut und im Browser geprüft, wurde aber auf Wunsch wieder entfernt: die bisherige Oberfläche bleibt; die
Aufklapp-Semantik kommt später und nicht automatisch. Im Code geblieben sind nur die Ableitung in C# (`DomainEditor/Module.cs`,
Befunde „Eingang ohne Konsument"/„Ausgang ohne Erzeuger", `/api/editor/module`). Der Stand der zurückgenommenen
Ansicht liegt nicht im Repo.

**Blank-Start (Domänen laden).** Der Editor startet leer; ein Start-Dialog wählt, welche Domänen (Namespaces) geladen werden
(leer, einzelne, alle). Nur Sicht (die Wahl liegt in der Ansichts-Datei): Nicht-Geladenes ist unsichtbar und kein Verbindungsziel;
der Code bleibt vollständig. Bedienung: `konzept-editor-panel-bearbeitung.md` §11.

### 11.4 Bewusst offen

- **Ebenen-/Aufklapp-Ansicht im Editor** — zurückgenommen, kommt später (bewusst nicht automatisch).
- Die Analyzer der offenen Regeln (§11.1), `public`/`internal` als erzwungene Schnittstelle, Muster/Operator-Gesten.
