# Der Editor als Kompositions-Sprache

> Detail-Referenz zu `docs/konzept-werkbank.md`; bei Widerspruch gilt das Zielbild. Stand 2026-10-13, gegen den Code geprüft.
>
> Die Sprache des Editors: Alphabet, Grammatik, Operatoren, Kapselung, Muster. Eine Anwendung dieser Sprache ist der Fluss
> (`docs/konzept-editor-pipelines.md` §14); Bedienung und Oberfläche: konzept-werkbank §8. Die Regeln selbst stehen als Daten in
> `DomainEditor/Grammatik.cs` und werden im Build geprüft (konzept-werkbank A.4); diese Datei erklärt, warum sie so sind.

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
Verbindungen, die ein Analyzer oder der Compiler ohnehin erzwingt (⊕ fragt `POST /api/werkbank/passt`), und der Extractor liest alles aus
Signaturen zurück (Fixpunkt, `--sonde`).

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
| **Prozess** (Regel) | Event-Join (UND, Zählen) | Command oder Auftrag (`Sende`/`Rufe`, + Kompensation) | vom Framework (Marking) |
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
- **Handle-Form folgt dem Inhalt:** keine Ausgabe ⇒ `Task`, sonst Strom mit OneOf (CQRS057; `docs/10`).
- **Garantie-Regel:** Führt ein verlierbarer Pfad in einen Schritt, der Durabilität braucht, wird die Kante markiert
  (gestrichelt); sendet ein Konsument ab einem transienten Eingang einen Command, warnt der Build (CQRS082, GR-GARANTIE).
- **Zyklus-Regel:** Ein Kreis Command → Event → … → Command ist erlaubt, wenn er durch einen **Zustandsschritt** läuft
  (Aggregat oder Prozess, die beide Zustand im Log falten); sonst nicht (GR-ZYKLUS: Azyklizitäts-Boot-Guard für Prozesse, CQRS088 für
  Projektionen/Reaktionen über transiente Events). Ein Fluss ist per Konstruktion azyklisch.

## 4 · Operatoren: wie man Verhalten komponiert

Es gibt keine neuen Laufzeit-Konzepte. Jeder Operator ist ein **Muster aus Bausteinen und Kanten**, das der Editor als eine
Geste anbietet und der Scaffolder in die bekannten Code-Formen schreibt.

| Operator | Bedeutung | Aus welchen Bausteinen |
|---|---|---|
| **Folge** | A, dann B | Kante: Command → Aggregat → Event → Konsument; im Fluss ein Draht (`knoten.Bei<Fall>()`) |
| **Verzweigung** | A führt zu B *oder* C | OneOf-Ausgänge; die Wahl trifft der Rumpf; im Fluss führt ∨ `.Oder(…)` die Wege wieder zusammen |
| **Auffächern** | A führt zu B *und* C *und* … | ein Event an mehrere Konsumenten; `SendeJe` für Laufzeit-Mengen; im Fluss `draht.Je(x => x.Liste)` |
| **Zusammenführen (UND)** | erst wenn A *und* B | Prozess-Join (`Auf<A>().Und<B>()`); im Fluss ∧ `p.Alle(…)` |
| **Zusammenführen (N)** | erst wenn alle N | Barriere-Aggregat (Zähler, N als Laufzeitwert), nicht als Collection; im Fluss `je.Sammle(…)` |
| **Schleife** | wiederhole | nur über ein Aggregat: dessen Event ist die Quelle des nächsten Vorgangs (`p.Auf<E>()`); ein Fluss selbst ist azyklisch |
| **Warten / Zeitlimit** | wenn bis dann nicht … | `quelle.Strom().Warte<A, B>().Zeitlimit(t)`; `.Zeitlimit(t)` am Knoten mit Draht `BeiZeitlimit()` |
| **Rückkopplung** | Ergebnis beeinflusst den nächsten Schritt | Command → Aggregat (Zustand) → Event → zurück |
| **Kompensation** | mach rückgängig | `RückgängigDurch` im Prozess |
| **Lesen im Schritt** | Entscheidung mit Wissen | Lese-Fähigkeit als Parameter |

**Regel Z (Zustand):** Zustand lebt **nur** in Aggregaten (und in Lesemodellen). Reaktionen, Funktionen und Handles sind
zustandslose Übersetzer; ein Fluss kennt nur seinen Vorgang. Das macht beliebig große Kompositionen analysierbar, denn jede
Schleife und jedes Gedächtnis ist eine sichtbare Kante durch ein Aggregat. Im Build: Warnung CQRS083 (GR-ZUSTAND) für Instanzfelder
eines Konsumenten, die Zustand halten (ausgenommen injizierte Konfig, Dienste, Logger).

## 5 · Kapselung: warum beliebige Komplexität lesbar bleibt

**Jeder Teilgraph kann zu einem Baustein zusammengeklappt werden, und das beliebig tief.** Pro Ebene sieht man wenige Knoten.
Tiefe ersetzt Breite.

- **Modul = Namespace.** Das ist ein Code-Fakt: Der Scaffolder schreibt je Namespace, der Extractor kennt ihn. Ein Modul
  kann Aggregate, Prozesse, Flüsse, Projektionen und Untermodule enthalten. Verschachtelung = Namespace-Hierarchie.
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
  Offene Ports („Eingang ohne Konsument“, „Ausgang ohne Erzeuger“) meldet der Build (CQRS086 GR-MODUL-EINGANG-OFFEN, CQRS085
  GR-MODUL-AUSGANG-OFFEN).

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

## 7 · Stand im Code

- **Regeln = Analyzer, Grammatik als Daten** (konzept-werkbank A.4): `DomainEditor/Grammatik.cs` hält die Tabelle aus §3 (Sorte × Eingang,
  Kardinalität), die Erzeugungen aus §2.2 und die Zusatzregeln, je Regel mit Id, Satz und Build-Gegenstück. Der Extractor legt sie als
  `rahmen.grammatik` ins Board; ⊕ fragt den Server (`Sicht.Passt`), `--grammatik` druckt „Regel → Build-Gegenstück“, im Editor zeigt
  **📐 Grammatik** dieselbe Liste. Geprüft wird im Build (Analyzer, Generator-Diagnosen, Boot-Guards); welche Regeln noch kein
  Gegenstück haben, sagt `--grammatik` (Lücken des Gates: `docs/13` WZ-1).
- **Kapselung nur im Modell:** `DomainEditor/Module.cs` leitet je Namespace die Ports aus dem Nachrichtenfluss ab (eine Kante über die
  Modulgrenze ist Ein- bzw. Ausgang; offene Ports wie oben). Eine Ebenen-/Aufklapp-Ansicht gibt es im Editor bewusst nicht
  (konzept-werkbank §14: Oberfläche nur nach Rückfrage); `public`/`internal` als erzwungene Schnittstelle ist nicht umgesetzt.
- **Domänen laden:** der Editor startet leer; ein Start-Dialog wählt, welche Domänen (Namespaces) geladen werden. Das ist nur Sicht (die
  Wahl liegt in der Ansichts-Datei); Nicht-Geladenes ist kein Verbindungsziel, ein Partner „↗ außerhalb“ lädt seine Domäne dazu.
- **Muster** (§6) und Operator-Gesten (§4) sind nicht umgesetzt.
