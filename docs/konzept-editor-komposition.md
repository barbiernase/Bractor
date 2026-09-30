# Konzept: Der Editor als Kompositions-Sprache

> Stand 2026-09-30 · Konzept, **nicht umgesetzt**. Übergeordnet zu `docs/konzept-editor-pipelines.md` (Pipelines sind eine
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
| **Muster und Linsen** | Wie verwendet man Bewährtes wieder, und wie liest man Komplexes? | §6, §7 |

Leitsatz: **Wenn es sich verbinden lässt, kompiliert es. Wenn es kompiliert, lässt es sich einlesen.** Der Editor erlaubt nur
Verbindungen, die ein Analyzer oder der Compiler ohnehin erzwingt, und der Extractor liest alles aus Signaturen zurück
(Fixpunkt, `--check`).

## 2 · Alphabet

### 2.1 Nachrichtensorten (die Kanten)

Jede Kante trägt genau eine Sorte. Die Sorte bestimmt Kardinalität und Garantie (Invariante 6):

| Sorte | Bedeutung | Ziel-Kardinalität | Garantie |
|---|---|---|---|
| **Command** | Absicht | genau **ein** Aggregat | wie der Absender (ab Event idempotent) |
| **Event** | Fakt im Log | **beliebig viele** Konsumenten | durabel |
| **Transientes Event** | Hinweis, Ablehnung | beliebig viele | verlierbar |
| **Trigger** | Anstoß von außen oder aus einer Pipeline | genau **eine** Pipeline | verlierbar |
| **Selbst** | Zeit, lokal (Tick) | dieselbe Pipeline | verlierbar |
| **Frist** | Zeit, dauerhaft (Deadline) | Command an ein Aggregat | durabel |
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
| **Pipeline-Handle** | Trigger / Event / Selbst / Start | OneOf Command, Trigger, transient, Selbst, Frist | keiner (Soll, §4 Regel Z) |
| **Ingress** | Außenwelt (HTTP, Timer, Datei) | Trigger | keiner |
| **Store + ReadModel** | Fähigkeiten | – | ja (Lesemodell) |
| **Konfig / Dienst** | – | werden injiziert | – |

## 3 · Grammatik: Verbindungsregeln

Die Ports sind nach Sorte typisiert. Eine Kante ist nur erlaubt, wenn Ausgangs- und Eingangs-Sorte zusammenpassen und die
Kardinalität stimmt. Beispiel: Ein Command-Ausgang geht an genau einen Decider-Eingang. Eine Trigger-Nachricht wird von genau
einer Pipeline behandelt. Ein Event darf an beliebig viele Konsumenten gehen.

| Ausgang ↓ · Eingang → | Aggregat | Prozess | Projektion | Reaktion | Pipeline | Reader |
|---|---|---|---|---|---|---|
| **Command** | ✔ (genau 1) | – | – | – | – | – |
| **Event** | – | ✔ | ✔ | ✔ | ✔ | – |
| **Transientes Event** | – | – | ✔ | ✔ | ✔ | – |
| **Trigger** | – | – | – | – | ✔ (genau 1) | – |
| **Selbst** | – | – | – | – | ✔ (dieselbe) | – |
| **Frist** | ✔ (Command-Ziel) | – | – | – | – | – |
| **Query** | – | – | – | – | – | ✔ (genau 1) |

Zusätzliche Regeln, jede mit ihrem Gegenstück im Build:

- **Geschlossener Ausgang:** Ausgänge sind konkrete Typen im OneOf (CQRS050). Zeichnen heißt Typ wählen.
- **Handle-Form folgt dem Inhalt:** keine Ausgabe ⇒ `Task`, sonst Strom mit OneOf (CQRS057, §13 dort).
- **Selbst nur ohne Event-Eingang:** Ein Event-Handle hat keine Mailbox (Laufzeit-Ausnahme heute, Analyzer offen).
- **Kein persistentes Event aus Pipelines:** Fakten entstehen nur im Aggregat (heute still verworfen, Analyzer offen).
- **Frist-Command braucht `(Guid)`-Konstruktor** (CQRS056).
- **Garantie-Regel:** Führt ein verlierbarer Pfad in einen Schritt, der Durabilität braucht, wird die Kante markiert
  (gestrichelt) und der Validator warnt.
- **Zyklus-Regel:** Ein Kreis Command → Event → … → Command ist erlaubt, wenn er durch einen **Zustandsschritt** läuft
  (Aggregat); sonst verbietet ihn der Azyklizitäts-Guard (für Prozesse aktiv, für Reaktionen/Pipelines offen).

## 4 · Operatoren: wie man Verhalten komponiert

Es gibt keine neuen Laufzeit-Konzepte. Jeder Operator ist ein **Muster aus Bausteinen und Kanten**, das der Editor als eine
Geste anbietet und der Scaffolder in die bekannten Code-Formen schreibt.

| Operator | Bedeutung | Aus welchen Bausteinen |
|---|---|---|
| **Folge** | A, dann B | Kante: Trigger-Kette, oder Command → Aggregat → Event → Konsument |
| **Verzweigung** | A führt zu B *oder* C | OneOf-Ausgänge; die Wahl trifft der Rumpf |
| **Auffächern** | A führt zu B *und* C *und* … | ein Event an mehrere Konsumenten; `SendeJe` für Laufzeit-Mengen |
| **Zusammenführen (UND)** | erst wenn A *und* B | Prozess-Join (`Auf<A>().Und<B>()`) |
| **Zusammenführen (N)** | erst wenn alle N | Barriere-Aggregat (Zähler, N als Laufzeitwert), nicht als Collection |
| **Schleife** | wiederhole | `Selbst<T>` (lokal, verlierbar), neu gesät durch Start |
| **Warten / Zeitlimit** | wenn bis dann nicht … | `Frist<TCmd>` + `FristStorno<TCmd>` |
| **Rückkopplung** | Ergebnis beeinflusst den nächsten Schritt | Command → Aggregat (Zustand) → Event → zurück |
| **Kompensation** | mach rückgängig | `RückgängigDurch` im Prozess |
| **Lesen im Schritt** | Entscheidung mit Wissen | Lese-Fähigkeit als Parameter |

**Regel Z (Zustand):** Zustand lebt **nur** in Aggregaten (und in Lesemodellen). Pipelines, Reaktionen und Handles sind
zustandslose Übersetzer. Das macht beliebig große Kompositionen analysierbar, denn jede Schleife und jedes Gedächtnis
ist eine sichtbare Kante durch ein Aggregat. Die heutige FileWatch-Pipeline mit Feld `_seen` verletzt das (siehe Befunde
in `konzept-editor-pipelines.md` §8). Im Entwurf wird das Gedächtnis zu einem Aggregat.

## 5 · Kapselung: warum beliebige Komplexität lesbar bleibt

**Jeder Teilgraph kann zu einem Baustein zusammengeklappt werden, und das beliebig tief.** Pro Ebene sieht man wenige Knoten.
Tiefe ersetzt Breite.

- **Modul = Namespace.** Das ist ein Code-Fakt: Der Scaffolder schreibt schon je Namespace, der Extractor kennt ihn. Ein Modul
  kann Aggregate, Prozesse, Pipelines, Projektionen und Untermodule enthalten. Verschachtelung = Namespace-Hierarchie.
- **Die Schnittstelle wird abgeleitet, nicht deklariert.** Eingangs-Ports eines Moduls sind die Nachrichtensorten, die von außen
  hineinlaufen (Commands an seine Aggregate, Events, die seine Konsumenten hören, Trigger, Queries). Ausgangs-Ports sind die, die
  hinauslaufen. Eingeklappt ist ein Modul also wieder ein Baustein mit typisierten Ports, und die Grammatik aus §3 gilt für
  ihn wie für jeden anderen.
- **Kanten über Modulgrenzen** laufen immer durch einen Schnittstellen-Port. Auf jeder Ebene verbinden Kanten nur Geschwister.
  Das hält jedes Bild frei von Fernkanten, die quer über das Board laufen.
- **Öffentlich vs. intern (optional, Code-Fakt):** Nachrichten, die ein Modul nach außen zeigen soll, sind `public`, interne
  sind `internal`. Dann ist die Schnittstelle nicht nur abgeleitet, sondern auch erzwungen (vom Compiler).
- **Entwerfen von oben nach unten:** Man zeichnet ein eingeklapptes Modul mit Ports (z. B. „nimmt Auftrag an, meldet
  erledigt“), klappt es auf und füllt es. Die Ports werden zu den Typen, die innen erzeugt oder konsumiert werden müssen.
  Der Validator zeigt offene Ports („Eingang ohne Konsument“, „Ausgang ohne Erzeuger“).

## 6 · Muster: Wiederverwendung

Ein Muster ist ein **parametrisierter Teilgraph** mit Schnittstelle, z. B.:

| Muster | Parameter | Expandiert zu |
|---|---|---|
| **Poller** | Tick-Nachricht, Intervall | Start-Handle + `Selbst<Tick>`-Schleife |
| **Watchdog** | Start-Event, End-Events, Frist-Command | Handle mit `Frist`, Handles mit `FristStorno` |
| **Barriere** | Teil-Event, Fertig-Event | Zähl-Aggregat (Erwartet/Fertig/Abgeschlossen) + Prozess-Kanten |
| **Wiederholen mit Zeitlimit** | Versuch-Command, Erfolg/Fehlschlag | Aggregat mit Versuchszähler + `Frist` + Rückkopplung |
| **Saga** | Schritte, Kompensationen | Prozess-Regeln mit `RückgängigDurch` |
| **Anreicherung** | Eingang, Lese-Fähigkeit, Ausgang | Handle mit Fähigkeits-Parameter |

**Code-Träger (Entscheidung):** Der Extractor erkennt nur Code-Fakten, also kein Muster-Raten aus der Graphform.

- **Stufe 1, Makro:** Ein Muster wird beim Einfügen in Bausteine *expandiert*. Danach sind es gewöhnliche Bausteine; die
  „Muster-Klammer“ ist nur eine Editor-Sicht bis zum Neu-Einlesen.
- **Stufe 2, Muster als Code-Fakt:** z. B. ein Attribut `[Muster(typeof(Watchdog))]` auf dem erzeugten Modul oder ein generischer
  Framework-Baustein. Damit bleibt die Klammer auch nach dem Einlesen, und ein geändertes Muster kann neu expandiert werden.

## 7 · Linsen: wie man Komplexes liest

Eine Linse ist eine Sicht auf dasselbe Modell. Sie **ändert nichts** und ist beliebig kombinierbar:

| Linse | Zeigt | Gut für |
|---|---|---|
| **Landkarte (Schienen)** | Spalten Außenwelt · Übersetzer · Domäne · Lesen; Kanten nur zwischen Nachbarn | Überblick einer Ebene |
| **Matrix** | Bausteine × Ausgangssorten, Ziele als Zellen, keine Linien | Bearbeiten eines dichten Bausteins |
| **Ablauf** | eine *mögliche* Folge ab einem Anstoß (Command/Trigger/Start), Bahnen je Beteiligtem | „Was kann passieren, wenn …?“ |
| **Fokus/Slice** | alles, was von einem Knoten aus erreichbar ist (vorwärts/rückwärts) | Auswirkungen einer Änderung |
| **Garantie** | nur die Kanten einer Garantie-Klasse | „Wo ist mein Pfad verlierbar?“ |
| **Zeit** | nur Selbst/Frist-Kanten | Zeitverhalten |

Die drei Pipeline-Bilder aus der Diskussion (Schienen, Matrix, Ablauf) sind solche Linsen, nicht das Modell.

## 8 · Was heute fehlt (Editor + Code)

| Fehlt | Für | Aufwand |
|---|---|---|
| Kapselung: Modul-Knoten, einklappen/aufklappen, abgeleitete Ports | §5 | groß, das Kernstück |
| Grammatik-Matrix als EINE Quelle (Editor-Picker + Validator + Analyzer-Liste) | §3 | mittel |
| Operator-Gesten (Verzweigung/Schleife/Warten/Barriere als ein Klick) | §4 | mittel |
| Muster-Makros (Stufe 1) | §6 | mittel |
| Linsen Garantie/Zeit/Ablauf | §7 | klein bis mittel |
| Analyzer: Selbst aus Event-Handle, persistentes Event aus Pipeline, Doppel-Trigger, Zyklus-Guard für Reaktion/Pipeline | §3 | klein je Regel |
| Laufzeit: Pipeline-Singleton/Idempotenz (Voraussetzung, damit Regel Z und die Garantie-Striche stimmen) | §3, §4 | Entscheidung + mittel |

## 9 · Phasen

| Phase | Inhalt |
|---|---|
| **1** | Grammatik als Tabelle im Code (eine Quelle) → Picker bietet nur gültige Ziele, Validator nennt die verletzte Regel |
| **2** | Kapselung: Namespace-Module als Knoten, abgeleitete Ports, Kanten nur zwischen Geschwistern, Auf-/Zuklappen |
| **3** | Operator-Gesten + Muster-Makros (Poller, Watchdog, Barriere, Wiederholen, Saga) |
| **4** | Linsen Ablauf/Garantie/Zeit; Simulation mit virtueller Uhr über Module hinweg |
| **5** | Muster als Code-Fakt (Stufe 2), öffentlich/intern als erzwungene Modul-Schnittstelle |

## 10 · Offene Entscheidungen (mit Empfehlung)

1. **Modulgrenze = Namespace?** Empfehlung: ja (Code-Fakt, heute schon Ordnungseinheit). Alternative: eigenes Attribut.
2. **Schnittstelle nur abgeleitet oder auch erzwungen (`public`/`internal`)?** Empfehlung: erst abgeleitet, erzwingen als Option
   je Modul.
3. **Regel Z verbindlich (kein Zustand außerhalb von Aggregaten/Lesemodellen)?** Empfehlung: ja, als Analyzer auf Felder in
   Pipelines/Reaktionen (ausgenommen Konfig/Dienst/Logger).
4. **Muster: Makro oder Code-Fakt?** Empfehlung: Makro zuerst, Code-Fakt, sobald ein Muster geändert und neu expandiert werden soll.
