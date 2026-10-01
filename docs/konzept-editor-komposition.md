# Konzept: Der Editor als Kompositions-Sprache

> Stand 2026-09-30 · Konzept; **Phase 1 (Grammatik) umgesetzt, Phase 2 (Kapselung) nur im Modell** (§11) — die Ebenen-Ansicht im Editor
> ist auf Wunsch zurückgenommen (kommt später, bewusst nicht automatisch); Phasen 3–5 offen. Übergeordnet zu `docs/konzept-editor-pipelines.md` (Pipelines sind eine
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
| Kapselung: Modul-Knoten, einklappen/aufklappen (abgeleitete Ports im Modell: §11.2; Editor-Ansicht offen) | §5 | groß, das Kernstück |
| ~~Grammatik-Matrix als EINE Quelle (Editor-Picker + Validator + Analyzer-Liste)~~ → umgesetzt, §11.1 | §3 | mittel |
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

**Entschieden in der Umsetzung (2026-09-30), je der Default:** (1) Modulgrenze = Namespace — ja. (2) Schnittstelle nur
abgeleitet; `public`/`internal` als Erzwingung bleibt Phase 5. (3) Regel Z in dieser Runde **kein Analyzer**, nur ein
Validator-Hinweis (`GR-ZUSTAND`, Schwere info). (4) Muster nicht Teil dieser Runde.

## 11 · Umgesetzt (2026-09-30)

### 11.1 Phase 1 — Grammatik als EINE Quelle

**Was.** Die Tabelle aus §3 (Sorte × Baustein-Eingang mit Kardinalität), die Erzeugungs-Spalte aus §2.2 (wer darf welche Sorte
erzeugen) und die Zusatzregeln liegen **einmal** im Code: `DomainEditor/Grammatik.cs` (Roslyn-frei). Jede Regel hat eine Id, einen
Namen, einen Satz und ihr **Build-Gegenstück** (Analyzer-/Generator-ID, Compiler, Boot-Guard oder „offen"). Daraus gespeist:

| Stelle | Wie |
|---|---|
| **Editor** | GraphExtractor legt die Grammatik als `rahmen.grammatik` ins Board (`ModellMapper.ZuBoardJson`), samt der Abbildung Editor-Port → Sorte/Baustein (`portSorte`, `portBaustein`, `recordSorte`, `ausgangSorte`). Das JS kodiert keine Regel: `grPruefe(O, I)` prüft jede Verbindung (Nachricht → Eingang: Sorte + Kardinalität; Ausgang → Nachricht: Erzeugung; Baustein → Baustein: Trigger-Kette, Ingress, Selbst). Im Verbinden-Modus leuchten nur erlaubte Ziele; typgleiche, aber verbotene Ziele sind rot „✕ Regel" und nennen beim Klick die Regel beim Namen (Modus bleibt, nichts wird verbunden). „+ plant Self-Tick" gibt es an Event-Handles nicht mehr (`GR-SELBST-OHNE-EVENT`). **📐 Grammatik** zeigt die Tabelle und „Regel → Build-Gegenstück". |
| **Validator** | `Validator.PruefeGrammatik` auf dem **Nachrichtenfluss** (`DomainEditor/Fluss.cs`: Erzeuger → Nachricht → Konsument aus Decide-OneOf, Apply, Prozess-Regeln, Handle-Eingang/-Ausgängen/-Fähigkeiten, Ingress; Außenwelt = Client/Ingress). Jeder Befund trägt die Regel-Id als Code und den Satz „Regel »Name« (Id; Build: …)". Geprüft: Sorte × Eingang, Kardinalität (`GR-COMMAND`, `GR-TRIGGER`, `GR-QUERY`, `GR-FALTUNG`), Selbst in fremder Pipeline (`GR-SELBST`), Erzeugung (`GR-AUS-*`), **Zusatzregeln** Selbst nur ohne Event-Eingang, kein persistentes Event aus Pipelines, Start höchstens einmal, Frist-Command braucht Ctor `(Guid)`, **Garantie** (Command ab Trigger/Selbst/Start bzw. ab transientem Event), **Zyklus ohne Zustandsschritt** (Tarjan über den Fluss ohne Aggregate und ohne Selbst-Kanten), **Regel Z** (Hinweis), offene Modul-Ports. |
| **Build-Gegenstück** | `dotnet run --project GraphExtractor -- --grammatik` druckt die Liste. `--check` prüft, dass jede genannte ID als Diagnose-Literal im Code existiert (10 IDs: CQRS002/003/010/020/050/051/052/053/056/057) und listet die Regeln ohne Gegenstück. |

**Regel Z als Code-Fakt.** Der Extractor liest je Pipeline/Projektion/Reaktion die **Zustandsfelder** aus dem Symbol (kein Rumpf):
Instanzfeld, das nicht `readonly` ist, oder `readonly` mit einem Referenztyp, der weder Konstruktor-Parametertyp (injiziert: Konfig,
Dienst, Logger) noch `string` ist. Träger: `PipelineKarte.Zustand`/`Konsument.Zustand` (nur gelesen, nie geschrieben; die Felder
stehen verbatim im `Zusatz`; nicht im Herkunfts-Stempel). Treffer im Code: `FileWatchPipeline` mit `_seen`, `_pending`.

**Offen (bewusst, nur gelistet):** die Analyzer für `GR-TRIGGER`, `GR-QUERY`, `GR-SELBST`, `GR-SELBST-OHNE-EVENT`,
`GR-KEIN-EVENT-AUS-PIPELINE`, `GR-GARANTIE`, `GR-ZUSTAND`, `GR-FALTUNG`, `GR-MODUL-AUSGANG-OFFEN` und die Erweiterung des
Azyklizitäts-Guards auf Reaktionen/Pipelines. Transiente Events haben im Editor noch keinen Konsum-Port (die Grammatik erlaubt sie,
die Oberfläche bietet sie nicht an). Garantie/Zyklus/Regel Z meldet nur der Validator — keine Live-Markierung der Einzelkanten.

### 11.2 Phase 2 — Kapselung

**Was.** Jeder Namespace ist ein Modul; die Hierarchie ist die Namespace-Hierarchie. `DomainEditor/Module.cs` leitet aus dem
Nachrichtenfluss die Ports ab: eine Kante Erzeuger → Nachricht bzw. Nachricht → Konsument, deren Enden auf verschiedenen Seiten der
Modulgrenze liegen, ist ein **Eingang** (Ziel drinnen) bzw. **Ausgang** (Quelle drinnen); die Außenwelt liegt außerhalb jedes Moduls.
Dazu die **offenen** Ports: eine Nachricht im Modul, die einen Konsumenten braucht und keinen hat (Command, Query, Trigger, Selbst →
„Eingang ohne Konsument", `GR-MODUL-EINGANG-OFFEN`), bzw. einen Erzeuger braucht und keinen hat (Event, Ablehnung, Response →
„Ausgang ohne Erzeuger", `GR-MODUL-AUSGANG-OFFEN`). Die Grammatik gilt für Modul-Knoten wie für jeden Baustein (dieselbe
Kardinalitäts-Prüfung beim Verbinden, auch wenn das Ziel eingeklappt ist).

**Editor: zurückgenommen (2026-10-01).** Eine erste Ebenen-Ansicht (Module eingeklappt als Karten mit Ports, aufklappen =
hineingehen) war gebaut und im Browser geprüft, wurde aber auf Wunsch wieder entfernt: die bisherige Oberfläche bleibt; die
Aufklapp-Semantik kommt später und nicht automatisch. Im Code geblieben sind nur die Ableitung in C# (`DomainEditor/Module.cs`,
Validator-Befunde „Eingang ohne Konsument"/„Ausgang ohne Erzeuger", `--check`, `/api/editor/module`). Der Stand der zurückgenommenen
Ansicht liegt nicht im Repo.

**Blank-Start (Domänen laden).** Der Editor startet leer; ein Start-Dialog wählt, welche Domänen (Namespaces) geladen werden
(leer, einzelne, alle). Nur Sicht: das Modell bleibt vollständig (Validator/Vorschau/Schreiben sehen alles), Nicht-Geladenes ist
unsichtbar und kein Verbindungsziel; Entwürfe sind immer sichtbar. Bedienung: `konzept-editor-panel-bearbeitung.md` §11.

**Parität/Idempotenz.** Kein neues Modell-Feld außer dem gelesenen Regel-Z-Fakt, nichts im Herkunfts-Stempel. `--check` vergleicht
die Modul-Ports aus dem Code-Modell mit denen aus dem Board.

### 11.3 Gemessen

| Prüfung | Ergebnis |
|---|---|
| `dotnet build` | 0 Fehler |
| Prüfstand | **179/179** (neu: 8 `DomainEditorKompositionTests` — Grammatik geschlossen + Gegenstücke, gültiges Modell ohne Verstoß, Regel beim Namen (GR-COMMAND/CQRS010), Pipeline-Zusatzregeln inkl. Regel Z als Hinweis, Trigger/Query genau einmal + Zyklus ohne Aggregat, Module/Ports/Eltern-Ebene, Top-down mit offenen Ports, Fähigkeit/Frist über Modulgrenzen) |
| `--check` | grün; **Grammatik:** 27 Regeln (16 Konsum, 17 Erzeugung), alle 10 genannten Build-IDs im Code gefunden; der echte Code verletzt keine Regel mit Build-Gegenstück — 2× `GR-GARANTIE` (ImageProcessing: Commands ab Trigger), 2× `GR-MODUL-AUSGANG-OFFEN` (nie erzeugte Ablehnung `ImagePairEingabeUngueltig`, Response `ModellAntwort`), 1× `GR-ZUSTAND` (FileWatch). **Module:** 13 Namespaces, 189 Ports (4 offen), Board = Code. Board ⇄ Modell inkl. Stempel-Idempotenz und Fixpunkt (242 Typen) unverändert grün |
| `--sonde` | grün, **67** Soll-Fakten (von Hand +1: `zustand pipeline Leihwesen.Lesen.MahnlaufPipeline | _gemahnt, _runden` — die Sonden-Pipeline hat dafür je ein Feld jeder Art bekommen: injiziert, readonly-Sammlung, veränderlich, readonly int/string, statisch) |
| Trockenlauf | `GraphExtractor` → `SimHost --trocken domain-model.json`: leer |
| Browser (SimHost `/editor`), 2026-10-01 | Bisherige Oberfläche unverändert, dazu: Start leer (0 Karten) mit Dialog; „ImagePair“ geladen ⇒ 73 Karten, 120 Kanten, keine ins Leere; **ungültige Verbindung:** Decider ⊕ Command ⇒ schon entschiedene Commands rot „✕ Regel“, Klick ⇒ „[GR-COMMAND] … Regel »Command → genau ein Aggregat« (Build: CQRS010, CQRS002)“, nichts verbunden; 👁 Vorschau auf unverändertem Board: „nichts zu schreiben“. (Die zurückgenommene Ebenen-Ansicht war vorher ebenfalls geprüft: Ports JS = C# 189 = 189.) |

### 11.4 Bewusst offen

- **Ebenen-/Aufklapp-Ansicht im Editor** — zurückgenommen, kommt später (bewusst nicht automatisch).
- Die Analyzer der offenen Regeln (§11.1), `public`/`internal` als erzwungene Schnittstelle (Phase 5), Muster/Operator-Gesten
  (Phase 3), Linsen (Phase 4).
