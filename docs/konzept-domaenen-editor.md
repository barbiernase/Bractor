# Konzept & Anleitung — Der Domänen-Editor

> **Gekürzt 2026-10-12** auf das, was mit dem Zielbild `docs/konzept-werkbank.md` vereinbar ist. Simulation, Board-Merge/-Persistenz, Betrieb/Host-Band (Trigger, Dienst, HostSetting) und Code-Knoten sind entfernt; dafür gilt das Zielbild.

> **Stand:** 2026-10-07 (§3/§9/§10 Pipelines nachgezogen; §12 Akteure, Verträge, Clients 2026-10-06) · **Status:** GEBAUT, teils ohne Codegen (je Abschnitt vermerkt).
> **Ort:** ausschließlich `GraphExtractor/HtmlPresenter.cs`, Konstante `EditorBlock` (ein eingebettetes
> HTML/CSS/JS, aus C# als String erzeugt). **Die EINE Oberfläche:** `http://localhost:5178/editor`
> (`/` leitet dorthin um). Das frühere read-only Board (`knowledge-graph.html`) ist seit 2026-09-24 im
> Editor aufgegangen.
>
> **Diese Datei ersetzt** (2026-09-18 zusammengeführt): `konzept-composition-root-editor.md`,
> `konzept-projektionen-und-store-nodes.md`, `konzept-saga-nodes.md`, `konzept-feld-ports.md`,
> `anleitung-editor-frist-und-leseachsen.md`.
>
> Verwandt: [04-konsum-und-prozess-maschine.md](04-konsum-und-prozess-maschine.md) (die vier
> Konsumenten, eine Maschine), [konzept-exactly-once-naht.md](konzept-exactly-once-naht.md)
> (Co-Commit), [konzept-werkbank.md](konzept-werkbank.md) Anhang A.1 (Extractor-Grundlagen).

---

## 1 · Was der Editor ist

> **Seit 2026-09-29 (Hybrid):** Auf der Fläche sind Karten immer kompakt (keine Ports, kein Freihand-Ziehen);
> bearbeitet und verbunden wird im Panel über den Verbinden-Modus — siehe
> [konzept-editor-panel-bearbeitung.md](konzept-editor-panel-bearbeitung.md). Port-Typen und Kanten gelten unverändert.

Ein **Node-Editor**: getippte Ports + Bézier-Kanten statt Dropdowns. **Der C#-Code ist die Wahrheit;** das
Board wird daraus gelesen, **alles andere ist Ableitung** (vom Server gerechnet) — Aggregat-Zugehörigkeit,
Store-Scope, Graph-Komponenten, Layout.

Bedienung: bearbeitet wird nur im Panel (Karte anklicken), verbunden per **⊕** (passende Karten leuchten →
anklicken); Fläche ziehen = pannen; Rad = zoomen.
Minimap (klick-/ziehbar), Domänen-Filter (🗂) und ein Insel-Zähler
(„⚠ N Inseln" = unverbundene Knoten aus der Graph-Analyse) sind eingebaut.

Der Editor bildet inzwischen **fast die ganze Architektur** ab: Schreibseite, Prozess, Leseseite
und die Rümpfe der Logik-Elemente.

---

## 2 · Leitprinzipien (das Rückgrat)

**(a) Verdrahten statt deklarieren.** Ein Interface ist nicht gezeichnet — es ist der **Querschnitt
der Verdrahtung**: jeder Draht *Projektion→Store* wird eine Write-Methode, jeder *Reader→Store* eine
Read-Methode; Signaturen fallen aus den Port-Typen ab. Der Store-Scope eines Konsumenten wird aus
seinen Handle→Fn-Kanten **abgeleitet** (vom Server), nicht deklariert. Auch die Aggregat-Zugehörigkeit
leitet der Server ab.

**(b) D/S/H — der LLM ist die Ausnahme.** Jedes Artefakt/jeder Rumpf gehört zu genau einer Klasse;
Ziel: **D + S maximieren, H minimieren.**
- **D — Deterministisch abgeleitet** aus Verdrahtung + Typen. Kein Tippen, kein LLM. (Records,
  Interfaces, Projektions-Handle, Feld-Mappings, DI.)
- **S — Strukturiert** aus einem typisierten Vokabular gewählt. Kein Freicode, kein LLM.
- **H — Freie Logik**, hand getippt **oder** LLM-gestützt — die **einzige** Stelle, an der der LLM
  überhaupt auftaucht.

**(c) Rumpf als Port.** Ein Rumpf ist ein Port der Karte; er zeigt den Code aus der Datei, ein leerer Rumpf ist
sichtbar als „⚙ fehlt", 🤖 füllt ihn (Zielbild `docs/konzept-werkbank.md` §8.4).

**(c′) Jedes Logik-Element folgt EINEM Muster (2026-10-11).** Alles, was Logik hat, ist ein Element aus **◀ Eingang** (ein Typ),
**Ausgänge ▶** (`OneOf<…>` konkreter Typen, `void` = Kenntnis, oder ein Strom), **⚙ Fähigkeiten** (Parameter) und einem **Rumpf**
(Code aus der Datei, 🤖 füllen). Die Typen von Ein- und Ausgang sind **eigene Karten** (Command, Event, Auftrag …): am Element stehen sie
nur als **Verweis** (Name + Felder zum Lesen, Klick öffnet die Karte), verbunden wird per **⊕** (Verbinden-Modus), „**＋**" in der Zeile legt
eine neue Typ-Karte an und verbindet sie sofort. **Deklariert wird nur an der eigenen Karte, zugewiesen nur per ⊕ — kein Dropdown, keine
Typ-Definition in einem fremden Knoten** (2026-10-11, Revision). Wo der Rumpf lebt, bestimmt der Code:

| Logik-Element | Eingang → Ausgänge | Rumpf lebt in | Slot (LLM) |
|---|---|---|---|
| Decide / Apply | Command → OneOf<Event> / Event → State | C# Aggregat | `decide`/`apply` |
| Projektion-/Reaktion-/Reader-Handle | Event/Query → OneOf<…> (+ Store-Fähigkeiten) | C# | `projektion`/`reaktion`/`reader` |
| Store-Fähigkeit | Parameter → Rückgabe | C# Store-Impl | `store` |
| Katalog-Funktion | Auftrag → OneOf<Ergebnis> (+ Lese-Fähigkeiten) | C# Impl (`RufeAsync`) | `funktion` |
| Fluss-Knoten (Zuordnung) | Fälle am Draht → Auftrag/Command | Lambda im Fluss | `fluss` (`Pipeline\|knoten#i`) |
| Prozess-Regel (Aufruf/Kompensation) | Event der Regel → Command/Auftrag | Lambda der Regel | `saga` (`Prozess\|Regel#Verb`) |
| **Zusage / Kenntnis** (Akteur draußen) | Event → OneOf<Command> / void / Strom | **Client-Implementierung** (Python `auf_…`) | `zusage` (`Client\|Event@Klasse`) |

Die **Funktion** hat ihren Signatur-Baustein (Auftrag ⊕, Ergebnisse ⊕, ⚙ Lese-Fähigkeiten ⊕ → Read-Fn, angeboten von ⊕ → Akteur, Rumpf)
**nur auf ihrer Karte**, und auf der Fläche steht sie wie ein Decider: **Auftrag-Karte → ƒ → Ergebnis-Karten** auf einer
Zeile (nie eingeklappt; „＋" an Eingang/Ausgang legt die Typ-Karte an); wer sie ruft — Prozess-Regel „Dann rufe", Fluss-Knoten ƒ „ruft", Ablauf „dann …" —, verbindet sich per ⊕ mit ihr
(Port-Typ `rufe` bzw. `kette`) und zeigt nur „Interface: Auftrag → Ergebnisse" plus Sprung zur Karte. Ebenso im Fluss: ⛲/◆ „meldet"
und ⧗ „warte auf" ⊕ → Event, ▶ „sendet" ⊕ → Command, Drähte ⊕ von einem Ausgang in einen späteren Knoten derselben Pipeline (∧/∨
automatisch, kein Kreis), „je Element"/„sammle" ⊕ → Je-Rahmen; „＋ƒ/＋▶/＋⧉/＋⧗" am Ausgang legt einen neuen Knoten an. Eine
geänderte Signatur wird an Interface UND Implementierung abgeglichen, der `IAuftrag<F>`-Marker umgehängt. Lambdas zeigen ihren Rumpf
aus dem Code (🤖 füllen); der Rumpf IST der Lambda verbatim.

**(d) Die eine sichtbare unreine Naht.** Invariante 5 (Fachcode bleibt rein) hat einen bewusst
erlaubten Gegenpol: das Backend. Der Editor zeigt es im 🖥 Host-Rahmen (Zielbild `docs/konzept-werkbank.md` §8.1).

---

## 3 · Bausteine und ihre Ports (+ was generiert wird)

| Gruppe | Knoten | Kernports | Klasse |
|---|---|---|---|
| **Schreibseite** | Command · Event · Ablehnung · Value Object · Enum | typ-spezifische in/out (cmd/evt/qrsp…), `als Feldtyp ▶` bei VO/Enum | D |
| | Aggregat (Hub) | oben `State ◀`, links Decider (autom.), rechts Applier (autom.) | D |
| | State | `Aggregat ▶`, Feld-Zeilen (Typ per Dropdown) | D |
| | Decider | `Command ◀`, `Aggregat ▲`, **OneOf-Event-Ausgänge** (je Outcome ein Punkt), `Decide-Rumpf ◀ code` | Struktur D, Rumpf H |
| | Applier | `Event ◀`, `Aggregat ▲`, `Apply-Rumpf ◀ code` | Struktur D, Rumpf H |
| **Prozess** | Prozess (Saga-Rahmen) | `Auslöser ▲`, „Regeln ◀ anstecken" | D |
| | Regel | `→ Prozess`, **Wenn-Join ◀** (beliebig viele), **Dann ▶** (je Command; ×N-Marke), **↩** Kompensation | D/S (Args = Stub) |
| **Leseseite** | ReadModel | `Store ▶`, Dokument-Felder | D |
| | Store (Interface-Editor) | `Read Models ▲`, Write-/Read-Fn (Signatur + `Impl ◀ code`), je Fn ein Aufruf-Port | Interfaces D, Rümpfe S/H |
| | Projektion (Controller) | `Event ◀` je Handle, `ruft Write-Fn ▶`, `veröffentlicht Event ▶` (reaktiv), `Controller ◀ code`; Achsen Pull/Append | D |
| | Reader (Controller) | `liest Projektion ▶` (IReader<TProjektion>), `Query ◀` je Handle, `ruft Read-Fn ▶`, OneOf-`Response ▶`, `Controller ◀ code` | D/S |
| | Reaktion (emittierend) | `Event ◀`, `sendet Command ▶` (OneOf), `veröffentlicht Event ▶`, `Controller ◀ code` | D |
| | Query · Response | `query ▶` / `◀ von Reader` | D |

**Kanten:** liefert der Server (aus dem Code abgeleitet), ebenso die Antwort auf „was passt an diesen Port?" (⊕, aus
`DomainEditor/Grammatik.cs`). Daraus Komponenten → mess-basiertes Shelf-Packing (`packLayout`) + Insel-Erkennung.
**Anschlussseiten dynamisch** (`drawEdges`): Auf der Fläche sind Karten eingeklappt; eine Kante dockt je Ende an der
Seite an, die dem Partner **zugewandt** ist (oben/unten/links/rechts — horizontaler vs. vertikaler Abstand der Karten),
nicht fest „Ausgang rechts, Eingang links". Mehrere Linien auf derselben Seite werden entlang der Seite verteilt (nach
Lage des Partners sortiert), die Kurve tritt senkrecht zur Seite aus. Sichtbare Slots (Panel) behalten ihren Punkt.

---

## 4 · Schreibseite

- **Record-zentrisch:** ein Editor für Command/Event/Ablehnung/Value Object; Kind per Dropdown.
  Command bekommt automatisch `AggregateId : Guid`; `Erzeugung`-Häkchen = `ICreationCommand`.
- **Aggregat = HUB, kein Feld-Träger:** State oben andocken, Decider (links) und Applier (rechts)
  werden **abgeleitet** angezeigt (Zugehörigkeit vom Server abgeleitet, nicht per Hand-Kante). Interne
  Linien zeigen: welcher Decider erzeugt das Event welches Appliers.
- **Decider = OneOf:** je mögliches Event **ein** Ausgangs-Punkt (per ⊕ mit dem Event verbunden). Das *WANN*
  macht der Decide-Rumpf (🤖 füllen) — der Editor verdrahtet nur die möglichen Ausgänge.
- **Applier:** gespiegelt (Event rein, Aggregat oben, Apply-Rumpf als Code-Port).
- **Typ-Komposition:** ein VO-/Enum-Knoten hat `als Feldtyp ▶`; verbindet man ihn per ⊕ mit dem kleinen
  `◀`-Typ-Eingang eines Feldes, wird der Feldtyp gesetzt (Collection-/Array-/Nullable-Wrapper bleibt
  erhalten). Umbenennen zieht alle Feldtypen mit. So komponieren sich Records
  visuell. **Dieser `ftype`-Port ist voll funktionsfähig.**

---

## 5 · Prozess: Saga-Rahmen + Regel-Knoten

Ersetzt die alte, überladene Transition-Karte (zweispaltig, mit komplettem Argument-Pin-Block und
kryptischen `t/r/g`-Rollen). Die Laufzeit ist ein **Petri-Netz** (Marking aus Tokens; eine Regel =
eine Transition, die Event-Typen joint und Commands feuert). Der Editor bildet das als **Fluss** ab.

**DSL-Wahrheit** (`Abstractions/Prozess/ProzessBuilder.cs`): eine Regel ist
`Bedingung[] + Sende + RückgängigDurch?`. Sie wird atomar beim `.Sende` registriert.

**Node-Typen:**
1. **Prozess** = Rahmen `Prozess<Auslöser>.Definiere`: Auslöser-Event oben, „berührt (abgeleitet)",
   „Regeln ◀ anstecken".
2. **Regel** = kleine **Kreuzung** (keine wiederholten Namen, keine Sektionen; Identität über
   Hover-Titel + Kante):
   - **Wenn ◀ (Join):** Event-Eingangs-Punkte untereinander, **beliebig viele** (keine 3er-Grenze —
     die ist nur ein Artefakt des handgeschriebenen Fluent-Builders, nicht des Modells).
   - **Dann ▶ (mehrere):** pro Dann ein Command-Ausgang mit Marke **×N** (Fan-out/`SendeJe`) + ein
     eigener **↩**-Kompensations-Ausgang. „+ Dann" hängt weitere Commands an **denselben Join**. Beim
     Schreiben **expandiert** die Regel zu N Regeln (eine je Dann), die sich die Bedingung teilen.
   - **KEIN Code/LLM-Port:** eine Regel trägt keine freie Logik. Der `Sende`-Rumpf ist mechanisches
     Feld-Mapping Event→Command-Parameter → **`default`-Stub** (`argListe` füllt fehlende mit
     `default`), von Hand im Code füllbar. Genau wie Decide/Apply-Stubs.

**Katalog-Funktionen (2026-10-07).** Neben „Dann ▶ Command" kennt die Regel **„Dann ƒ Funktion"** (`Rufe<F>`): im Panel
„+ Dann ƒ (Katalog-Funktion rufen)", Funktion per ⊕, darunter Auftrag → Ergebnisse der gewählten Funktion. Jeder Aufruf (Command
oder Funktion) hat ein **⏳ Zeitlimit**-Feld (C#-Ausdruck verbatim, z. B. `TimeSpan.FromSeconds(30)`). Die **Funktions-Karte**
(Palette „+ ƒ Funktion", legt ihren Auftrag gleich mit an) zeigt: ◀ gerufen von Regeln · ◀ Auftrag (Record der Art „Auftrag") ·
Ergebnisse ▶ (Events). Kanten: Regel → Funktion, Auftrag → Funktion (gestrichelt), Funktion → Ergebnis-Events. Wo die Implementierung
läuft, ist Konfig der Umgebung (Funktions-Laufort), nicht Teil des Graphen. Code-Fakten: `IFunktion`, `IAuftrag<F>`, `Rufe<F>`, `Zeitlimit`
(Extractor, Sonde); Grammatik: Baustein `funktion`, Sorte `auftrag` (GR-FUNKTION-AUFTRAG, GR-AUS-FUNKTION, GR-FUNKTION-GEBUNDEN).

**Entfernt (2026-09-14): der Count-Join Σ (`UndAlle`/„sammle").** Er ist über ein selbst
modelliertes **Zähl-Aggregat** (zählt Teil-Events, feuert EIN Abschluss-Event) + normalen
Einzel-Auslöser ausdrückbar → der Editor bleibt minimal. Der Fan-out `×N` bleibt. Siehe auch das
Muster `Domain/Sammelvorgang` (Erwartet/Fertig/Abgeschlossen als skalares Zähl-Aggregat).

**Scaffolder-Abbildung (unverändert):** ein Regel-Knoten = ein `SagaSchritt`; `prepareSaga()` baut
die `schritte` + `extraUsings`. **Codegen-Folge:** der Fluent-`ProzessBuilder` kann nur bis 3 `Und`;
für >3 müsste der Scaffolder die `Regel` direkt konstruieren (Bedingung-Liste) — separate Aufgabe,
das Editor-Modell ist bereits N-fähig.

---

## 6 · Leseseite: Projektionen, Stores, Reader

> **Status:** Verdrahtung GEBAUT & live verifiziert.

Das tragende Modell sind **zwei Code-Schichten gegen verdrahtete Verträge**:

- **Handle = Controller.** Der Editor verdrahtet nur **auslösende Event(s)** + **Store-Scope** (mehrere
  Stores möglich, abgeleitet aus den Aufruf-Kanten). *Welche* Store-Funktionen in *welcher*
  Reihenfolge mit *welchen* Argumenten aufgerufen werden = **C#-Code** im Controller-Rumpf. Kein Join
  (den gibt es nur bei der Saga), keine Arg-Pins.
- **Store = Interface + Impl.** Der Store-Knoten ist ein **Interface-Editor**: Write-/Read-Funktionen
  mit Signatur (Name + Parameter [+ Rückgabe]); je Funktion ein `Impl ◀ code`-Port. ReadModels docken
  oben an (`Store ▶` → `Read Models ▲`).
- **Reader = Controller** mit explizitem **`IReader<TProjektion>`-Bindungsport** (`liest Projektion ▶`),
  Query-Eingängen und **OneOf-Responses** je Query (symmetrisch zu `decider.ergibt[]`).

**Die zwei orthogonalen Entwurfs-Achsen (am Projektion-Knoten):**

| Achse | Häkchen | Bedeutung |
|---|---|---|
| **Transport** | ☑ *Geordneter Pull* (Default) | `IPullSubscriber` — jedes Event genau einmal, in Reihenfolge (Normalfall) |
| | ☐ aus | Signal (`ISubscriber`) — schnell, best-effort, darf verloren/doppelt/ungeordnet sein (UI-Feedback/Ticks) |
| **Garantie** | ☐ *Append-artig* (Default aus) | idempotenter Upsert → **at-least-once genügt** |
| | ☑ an | Append-artig (Ledger/Historie) → verlangt **Co-Commit-Store → exactly-once** (Boot-Check **GA-1**) |

**Faustregel:** Append-Häkchen an, sobald eine Write-Fn *anhängt* (Liste/Historie); bei *Upsert* aus.

**Am ReadModel: ☐ *geteilt*** (Marker `IGeteiltesReadModel`, Code-Fakt). Die Maschine garantiert einen Schreiber nur je
(Projektion, Stream). Ein Dokument, das mehrere Streams beschreiben (Singleton-Zeiger, Zähler über viele Aggregate), ist
*geteilt*: generiert mit Versionsprüfung, im Store nur über `EnqueueGeteilt` änderbar (ein Konflikt wiederholt den Stapel);
„neuer gewinnt“ dort nach Event-Zeit, nie nach Ankunft. Ohne Häkchen gehört das Dokument dem Stream seiner Id — eine Menge
über viele Streams besser als **Zeilen je Stream** (Id enthält den Besitzer) ablegen statt geteilt. Umschalten schreibt nur
diesen Marker in die Basisliste; die Karte zeigt „⇄ geteilt“. Schreibt der Store ein abgewähltes Dokument noch geteilt,
meldet es der Bau (`EnqueueGeteilt` verlangt den Marker). Regeln des Store-Rumpfs: `docs/10-entwickler-api.md` §10.3
(CQRS066/067); die 🤖-Konsole prüft einen Store-Rumpf damit **vor** dem Schreiben gegen das echte Projekt.
Transport und Garantie sind unabhängig — jede Kombination gültig; ein Klartext-Hinweis unter den
Häkchen spiegelt die Semantik. Beide gesetzt (replaybar + emittierend) = Fehler (Analyzer bzw.
Ctor-Guard).

**Reaktives Event veröffentlichen:** Projektion **und** Reaktion können nach dem Schreiben ein Event
`yield`en (HandlerOutputRouter → Broker-Re-Publish, **verlierbar, kein Log**) — im Editor als
gestrichelt-teal „veröffentlicht ▶". Zwei Event-Sorten: Log-Event (Aggregat, durabel) vs. reaktiv
(Broker, gestrichelt).

**Ehrliche Grenzen:** Ein Store = eine Transaktionsgrenze; zwei echte Grenzen = zwei Projektionen.
Marten-Exotik (computed indexes, Volltext, `.Include`-Joins) bleibt Store-Konfiguration außerhalb des
portablen Modells. Der frühere „Effekt-Vokabular"-Ansatz (Dropdown-Operationen wie Upsert / Feld
setzen / anhängen-dedup / Fan-out / Sekundär-Index) bleibt als **optionaler S-Baukasten** denkbar,
ist aber zugunsten freier Rümpfe (Controller/Impl, 🤖 füllen) zurückgestellt.

---

## 8 · Feld- & Typ-Ports

UI-seitig (keine neue Feld-Struktur; Quelle ist `(Owner, Feld)` mit stabiler `_id` → rename-fest):

- **Typ-Port `ftype` — VOLL GEBAUT.** VO/Enum → kleiner `◀`-Typ-Eingang am Feld setzt den Feldtyp (§4).

---

## 9 · Bedienung — Kurzanleitung

### 9.1 Domänen-Rahmen (visuelle Blöcke, hierarchisch)

Das Layout bleibt das bewährte: je Aggregat ein Block mit hochkant stehenden Rollen-Spalten, Karten frei ziehbar.
Darum liegen nur **rein visuelle Rahmen**:
- **Domänen-Zugehörigkeit aus dem Graphen steht über allem** (der Server rechnet sie): gehört ein Baustein laut Graph zu einem
  Aggregat (Projektion über ihre Events, Reader/Store/ReadModel/Query/Response über die Projektion), ist seine Domäne
  der Namespace dieses Aggregats — auch wenn die Klasse in `Domain.Projections`
  liegt. **Ablauf-Einheiten** (Fluss, Reaktion + Handles, Prozess + Regeln) werden als
  Ganzes zugeordnet: die Domäne ihrer Aggregat-Nachbarn, wenn eindeutig — sonst die, an die sie **Commands schickt**
  (Lesen woanders ist nur Abhängigkeit; z. B. DatensatzResolverPipeline liest ImagePair, schreibt Datensatz → Datensatz).
  **Datentypen** ohne Aggregat (Feldtyp-VO/Enum/Konfig/Response) folgen schrittweise ihren Nachbarn. Erst danach zählt
  der eigene Namespace (z. B. BenchmarkPipeline ohne Domänen-Bezug, Inseln).
- **Domäne = Namespace dieser Zuordnung**, hierarchisch: Unterdomänen (`Domain.Pipeline.Trainingslauf`) liegen gestrichelt im Rahmen
  ihrer Eltern-Domäne. `packLayout` ordnet auf oberster Ebene nach diesem Baum (alphabetisch, „ohne Domäne“ zuletzt;
  je Domäne: Aggregat-/Brücken-Blöcke, dann ihre Inseln, dann die Unterdomänen) und reserviert je Region Rand + Kopf,
  sodass sich Rahmen nie schneiden.
- Der Rahmen folgt den **echten** Kartenpositionen (`zeichneRahmen` aus `GEO`, nach jeder Messung) — zieht man eine
  Karte heraus, wächst er mit. Linie und Kopf wachsen beim Rauszoomen mit (lesbar in der Übersicht).
- **Menüleiste im Kopf:** `＋` Baustein in dieser Domäne (Namespace gesetzt; Decider/Applier/State ans Aggregat der
  Domäne, Regel an ihren Prozess) · `＋ ▤` Unterdomäne · `⤢` einpassen (auch Doppelklick) · `⋯` (leere Domäne
  entfernen). Ansicht-Leiste `＋ Domäne`, Rechtsklick auf die Fläche ebenso.
- **Spalten-Rahmen:** innerhalb einer Domäne ist jede senkrechte Rollen-Spalte eines Blocks (Aggregat bzw. Brücke) eigens
  eingerahmt — Command | Decider | Aggregat · State | Event · Ablehnung | Applier | VO · Enum | Projektion |
  Projektion-Handle | Store-Fn | Read Model · Store | Query | Reader-Handle | Response | Reader (bzw. Prozess | Regel |
  Reaktion … in der Brücke). Inseln haben einen eigenen Kasten. `layoutBlock` reserviert über jeder Spalte den Kopf (`SPK`). Kopf: Name · Anzahl · `＋` (legt eine Art dieser
  Spalte in Domäne + Aggregat des Blocks an; Handles/Store-Fns entstehen weiter am Besitzer) · Doppelklick = einpassen. Ein über `＋` angelegter, noch unverdrahteter Baustein behält bis zur
  Verdrahtung seine Spalte (`VIEW.heimBlk`) statt im Inselkasten zu landen.
- **Nur Sicht:** leere neue Domänen (`VIEW.domNeu`) und die Heimat namespace-loser Bausteine (`VIEW.heim`) leben in der
  Ansicht; im Code entsteht ein Namespace erst mit seinem ersten Typ. Code-Namespace geht vor.

### 9.2 Render-Architektur der Fläche (Zoom/Pan/Minimap)

Gemessen am vollen Bestand: ~550 Karten, ~800 Kanten-Pfade, ~24.500 DOM-Elemente, Inhalt bis ~7.000 × 11.300 px.
Regeln (alle in `HtmlPresenter.cs`, `applyPan`/`sichtFrame`/`messeWelt`/`kulle`):
- **Keine Dauer-GPU-Ebene.** `.gworld` trägt `will-change:transform` nur während einer Zieh-Geste (`.bewegt`:
  Pan, Minimap-Ziehen — reines Verschieben, Raster bleibt gültig). Dauerhaft gesetzt rasterte der Browser die
  ganze Welt in der Start-Zoomstufe; beim Rauszoomen sprengte das das Kachel-Budget → Karten luden nicht
  nach, flackerten, verschwanden — auch Fokus und Minimap, die sich das GPU-Budget teilen.
- **Transform sofort, Abgeleitetes einmal je Frame.** `applyPan` schreibt nur den Transform; Minimap-Rahmen,
  Culling und Schatten-LOD laufen gebündelt in `sichtFrame` (rAF) — ohne Layout-Lesen (Canvas-Größe aus
  `ResizeObserver` → `CV`, Knoten-Geometrie aus `GEO`).
- **Geometrie-Cache `GEO`** wird je Layout-Änderung (`drawEdges`) einmal gemessen; Welt- und Kanten-Ebene
  wachsen auf die Inhaltsgröße (statt fest 6000 × 4000, über die der Bestand hinausragte).
- **Culling:** Karten außerhalb Sichtfenster + ½ Fenster Rand bekommen `.weg` (`visibility:hidden` — Maße
  bleiben, Anker/Kanten/Einpassen stimmen weiter).
- **Schatten-LOD:** unter Zoom 0,45 entfallen die weichen Karten-Schatten (`.fern` → `--gsch:none`);
  Hervorhebungs-Schatten (Spur, Typ, Verbinden) bleiben.
- **Overlays gekapselt:** Fokus, Minimap, Filter sind eigene, `contain`-gekapselte Ebenen.

---

## 10 · Backend-Naht, Persistenz & Synchronität (SimHost)

**Grundsatz: der C#-Code ist die einzige Wahrheit.** Das Board ist eine Projektion des Codes.

### 10.1 Endpunkte (`SimHost/Program.cs`)

| Endpunkt | Wirkung | UI |
|---|---|---|
| `GET /api/editor/model` | `domain-model.json` (aus C# extrahiert) | Boot, ↻ |
| `POST /api/editor/extract` | GraphExtractor über den **aktuellen** Code laufen lassen → `domain-model.json` neu | ↻ |
| `GET/POST /api/editor/code`, `POST /api/editor/open` | Rumpf aus der Datei, im IDE öffnen | Rumpf, 🤖 füllen |
| `POST /api/editor/build` | `dotnet build` | (derzeit kein UI) |

Graph-Diagnosen: `UNUSED-EVENT` (Domänen-Event/Ablehnung in keiner Decide-Signatur), `MISSING-APPLY` (Event produziert, nicht gefaltet → Laufzeit-Crash),
`ENUM-ZERO` (Wert 0 geht auf dem Proto-Wire verloren), `STORE-AMBIGUOUS` (mehrere Impls je Store-Interface).

### 10.5 Nichts hart kodiert — was woher kommt

Grundsatz (2026-09-24): **Jede Erkenntnis über die Domäne kommt aus einem Code-Fakt** — Marker-Interface, Attribut,
Symbol, Signatur, aufgelöster Aufruf, Datenfluss. Keine Namenskonvention, kein Namespace-Raten, keine Mehrheits-
Statistik, kein Default, der sich als Erkenntnis ausgibt. Wo der Code etwas nicht eindeutig sagt, bleibt es leer bzw.
wird als mehrdeutig gemeldet — nie geschätzt.

| Frage | Quelle (Code-Fakt) |
|---|---|
| Framework-Namen (Marker, DSL-Verben, `Frist.Kontext`, `IFristplan.PlaneAsync` …) | `GraphExtractor.Kern/Vertrag.cs` über `typeof`/`nameof` aus `Abstractions` (Umbenennung = Compile-Fehler); Scaffolder ebenso |
| Welche Projekte? | `Projektlage.cs`: Vertrag = Assembly von `IState`; **Laufzeit** = Projekt mit `[RoutingTabelle]`-Generat; Analyse = deren Referenz-Hülle; **Domäne** = Projekte, die im handgeschriebenen Quelltext einen Typ mit Domänen-Rolle deklarieren (Command, Event, State, Decider, Store, Konsument, Pipeline, Prozess, Wertobjekt …); **Hosts** = Programme mit handgeschriebenem Einstiegspunkt |
| Generiert oder handgeschrieben? | handgeschrieben = Projekt-**Dokument**; Generator-Ausgaben sind nie Dokumente (plus `<auto-generated`-Kopf für eingecheckte Prepass-Dateien) |
| Aggregat | `IState` + wer `IDecider<State>`/`IApplier<State>` implementiert — **egal wo** (geschachtelt oder nicht, beliebige Datei/Typform) |
| Decide/Apply/Handle | Parametertypen (`ICommand`, `IEvent`, `IAggregateEnvelope`, `IQuery`) an **handgeschriebenen** Methoden — für den Extractor ist der Methodenname egal; die Dispatch-Generatoren rufen aber nur `Handle` in der CQRS057-Form (Abweichung = Build-Fehler) |
| Felder | Positions-Parameter eines Records **und** Auto-Properties (`{ get; init; }` …, `required`); Form (record/class/struct, Parameterliste ja/nein, weitere Basen, Attribute) wird mitgeführt und so zurückgeschrieben; Sammlungen per Symbol (`IEnumerable<T>` → `elementTyp`) |
| Guards | alle umschließenden `if` bis zum Rumpf, verzweigungstreu (`else` ⇒ `!(…)`), Event-Typ aus dem Symbol |
| Saga-DSL | Verben am **Methoden-Symbol** (Vertrags-Assembly + DSL-Namespace); `Regeln` über die Interface-Implementierung (auch explizit); Ketten auch über lokale Variablen |
| Stores | Marker **`IWriteStore`** / **`IReadStore<TWrite>`** (Paarung als Typ, Namen frei) — auch der Framework-Generator registriert danach; Store-Aufrufe über das aufgelöste Methoden-Symbol (auch über die konkrete Klasse); ReadModel → Store nur wenn eindeutig (sonst `storeKandidaten`) |
| Subscriber-Id | Compile-Zeit-Konstante der Vertrags-Property (`const`, `nameof`, Verkettung); nicht konstant ⇒ leer |
| Ausgänge (Reaktion/Decide) | nur die **Signatur**: konkreter Typ oder `OneOf<…>` (CQRS050) — der Rumpf liefert nichts |
| Aggregat-Zugehörigkeit eines Records | Command → Aggregat seines Deciders; Event → Aggregat, das ihn erzeugt/faltet; VO/Enum → Aggregat, dessen Records/State ihn referenzieren — jeweils **eindeutig oder keinem** (im Editor: Decider/Applier per **▲ Aggregat**-Port verdrahtet) |
| Routing, registrierte Prozesse, DI-registrierte Store-Impl | Generat: `[RoutingTabelle(Art)]`-Properties, `["Name"] = new P()`, erzeugte Typen |
| Aggregat-Klassen-/Methodennamen, `AggregateId`, OneOf-/Join-Stelligkeit, Wire-Skalare | `Abstractions.Aggregatvertrag` (auch vom Generator gelesen), `nameof(ICommand.AggregateId)`, per Compilation gezählte `OneOf`/`RegelBauer`-Varianten, `ProtoScalarSpecs` (eine Quelle mit dem Proto-Codegen) |
| Wohin schreiben? | echte Dateipfade im Modell; Verzeichnis je Namespace nur wenn **alle** seine Typen in genau einem Verzeichnis liegen, sonst über den längsten bekannten Namespace/Projekt-Wurzel (MSBuild-Abbildung). Neuer Typ: eindeutige Datei gleicher Art im Namespace, sonst die kanonische Scaffolder-Datei (`Commands.cs` … bzw. `{Agg}.cs`, `{Agg}.Decider.cs`, `{Agg}.Applier.cs`). Ohne bekanntes Verzeichnis: **nicht platzierbar**, wird nicht geschrieben |
| Ansicht (Positionen, geladene Domänen, Akteur-Reihenfolge) | Ansichts-Datei (Zielbild `docs/konzept-werkbank.md` §2) |

**Beweis statt Behauptung — die Agnostik-Sonde** (`dotnet run --project GraphExtractor -- --sonde`): legt eine dem
Extractor unbekannte Domäne (`GraphExtractor.Kern/Sonde/*.cs.txt`, fremder Wurzel-Namespace, jede konventionsbrechende
gültige Schreibweise) im Speicher in die Solution, fährt die echte Pipeline und vergleicht mit einem **handgeschriebenen**
Soll (`Sonde/soll.txt`). `--sonde-ist` zeigt das Ist,
`--sonde-board <datei>` schreibt das Board inkl. Sonde (zum Ansehen im Editor; das Format folgt dem Zielbild).

## 11 · Offene Punkte / Nicht-Ziele

- **>3 `Und`** im Scaffolder (Fluent-Builder-Grenze, s. §5).
- **VO-Behavior / Specification / Entity** — aus der Domänen-Analyse nicht gebraucht, kein Ziel.

## 12 · Akteure, Verträge, Clients im Editor

> Begriffe und Backend: `docs/konzept-akteure.md` (Akteur · Zusage/Kenntnis · Akteur-Vertrag · Client). Hier nur, wie der Editor sie zeigt
> und bearbeitet. Vorgabe (Tobi): die bestehende Darstellung bleibt; es kommt eine Gruppierungs-Ebene dazu, nichts wird ersetzt.

### 12.1 Bild

```
┌ ImagePair ─────────────────────────────────── 👤 KameraSystem › Klassifizierer › Inspekteur › … ┐      ┌ 🔌 KlassifikationsWorker ┐
│ ┌ ImagePair · ⚙ KameraSystem ─────────────────────────────────────── ＋ Fokus ⤢ ⋯ ┐          │      ┊ verkörpert 🤖 Klassifizierer ┊
│ │ Command │ Decider │ State │ Event │ Applier │ …                                 │          │      ┊ ┌ Client · Klassifikations… ┐┊
│ └─────────────────────────────────────────────────────────────────────────────────┘          │ ━━━━━┿━┥ ↺ trägt IKlassifizierer   │┊
│ ┌ ImagePair · 🤖 Klassifizierer ─ 🔌 KlassifikationsWorker ─────────────────────── ┐          │      ┊ │  ◀ ImagePairKomplett → …   │┊
│ │ Akteur │ 📜 Vertrag IKlassifizierer (Zusagen) │ Command │ Decider │ Event │ …   │          │      ┊ │ ▶ sendet als Klassifizierer │┊
│ └─────────────────────────────────────────────────────────────────────────────────┘          │      ┊ │ ? fragt als Klassifizierer  │┊
│ ┌ ImagePair · 👤 Inspekteur ─ 🔌 Arbeitsplatz ──────────────────────────────────── ┐          │      ┊ └────────────────────────────┘┊
│ │ Command … │ Query … │ Reader │ Projektion │ Read Model │ Response                 │          │      └┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┘
│ └─────────────────────────────────────────────────────────────────────────────────┘          │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
```

Domäne (dünner Außenrahmen) → **Domäne × Akteur** (je ein Rahmen) → die bekannten Rollen-Spalten. Clients stehen als eigene, gestrichelte
Rahmen **rechts neben allen Domänen**.

### 12.2 Welcher Baustein steht in welchem Akteur-Rahmen?

Ganz aus dem Graphen, ohne neues Wort (`DomainEditor/AkteurAnteile.cs`, vom Server gerechnet):

| Seite | gehört zu A, wenn … |
|---|---|
| **Eingang** (Command/Query/Quell-Nachricht) | A ∈ Akteur-Menge (direkt `IDarf` oder über die Kette, Akteur-Konzept §2.3) |
| **Schreibseite** | Decide eines Commands von A; die Events/Ablehnungen aus seiner Signatur; deren Applier; State, VO, Enum, die diese berühren |
| **Leseseite** | Queries von A → Reader-Handle → Reader → Projektion (+ Handles, Store-Fns, Read Model, Response) |
| **Ablauf** | Arme, deren Ausgaben zu A gehören (Pipeline/Prozess/Reaktion/Frist inkl. Handles) |

VO/Enum/Konfig/Response gehen mit ihrem Träger mit, öffnen aber keinen eigenen Akteur-Rahmen.

### 12.3 Reihenfolge der Akteur-Rahmen und Doppelungen

- Ein Baustein, der mehreren Akteuren gehört, steht **genau einmal** — im ersten Akteur-Rahmen seiner Domäne. Die Reihenfolge ist
  abgeleitet: (1) wer das Aggregat erschafft (Akteur des `ICreationCommand`), (2) wer worauf reagiert (B nach A, wenn B ein Event aus A's
  Anteil hört), (3) Art (Maschine → KI → Mensch), dann Name; „⚠ ohne Akteur" (rot, GR-HERKUNFT) zuletzt. Eine Domäne ohne Akteure sieht
  aus wie vorher (ein Rahmen).
- Spätere Rahmen zeigen im Kopf **„↥ auch"** mit Verweisen auf die Doppelungen (Klick = hinspringen); ein Rahmen, dessen Anteil schon
  ganz weiter oben steht, erscheint nur als Kopf. Kanten laufen sichtbar über Rahmen hinweg. Die Kartenzahl bleibt gleich (Parität).
- Im Kopf **⋯ → ↑/↓/↺** sortiert der Entwerfer um (`VIEW.akteurOrdnung[dom]`); geteilte Bausteine wandern mit.

### 12.4 Akteur: Kopf, Karte, Panel

- **Kopf:** `Domäne · Art-Symbol Akteur` (Klick = Akteur-Panel) · „↥ auch" · **🔌 Stecker-Zeile** (welche Clients hier stecken, §12.6) ·
  Zahl · ＋ (Command/Query/Quell-Nachricht für diesen Akteur, `IDarf` sofort) · Fokus · ⤢ · ⋯. Gestrichelt = der Akteur wirkt hier nur über die Kette.
- **Akteur-Karte** in der ersten Domäne, in die er selbst hineingibt (Spalte „Akteur").
- **Panel:** Art, **darf ⊕/✕**, „bewirkt über die Kette" (abgeleitet), „wirkt in [Domänen]", Dienst + „entscheidet in", **Zusagen ◀**
  (je `Auf` eine Zeile, „+ Zusage ⊕ (Event)"), „🔌 getragen von" (Clients, ⚠ GR-GETRAGEN, Port für „trägt ⊕"), „hört" (abgeleitet).
- Zuordnung per Ziehen gibt es nicht: wem eine Karte gehört, folgt aus `IDarf`, Kette und Vertrag. Löschen eines Eingangs entfernt ihn aus
  allen `IDarf`-Listen, Verträgen und Client-Rändern.

### 12.5 Akteur-Vertrag: 📜-Rahmen und Zusage-Karten

- Der Vertrag steht **geschlossen** neben der Akteur-Karte als Rahmen „📜 Vertrag IX" (Kopf: Name · ◀ Eingänge · Ausgänge ▶ · ＋ Zusage · Fokus
  · ⤢), darin je `Auf(Event)` eine Karte **Zusage** (◀ Event · Ausgänge ▶; ohne Ausgang = Kenntnis). Die Kanten führen zu den Events und
  Commands hinüber; die Kette läuft so sichtbar durch den Akteur draußen (`msg:E → akt:A → msg:C`).
- **Zusage-Panel:** Vertrag-Name, ◀ Auf Event, Ausgänge ▶ (⊕/✕), „Strom", die Signatur als Zeile.
- „◀ kommt aus" eines Commands nennt „Akteur · Zusage auf E", „geht an ▶" eines Events „Akteur · Zusage" samt tragenden Clients (🔌).
- **Schreiben:** Scaffolder legt das Interface neben den Akteur; der Abgleich hängt fehlende `Auf` an, ersetzt geänderte Rückgaben,
  streicht entfernte (nur im Haupt-Vertrag; weitere Teile liest der Editor). Der Build danach erzeugt die Python-Basis (Codegen-Prepass).
- **Rumpf · umgesetzt in (2026-10-11):** die Zusage ist ein Logik-Element — ihr Verhalten steht in der Implementierung jedes Clients, der
  den Vertrags-Teil trägt. Das Zusage-Panel zeigt je Implementierung (🐍 Klasse · Datei · 🔌 Client) den Rumpf-Port (🤖 füllen, Slot
  `zusage|Client|Event@Klasse`). Code-Fakt: eine Python-Klasse, die die generierte `<Client>Basis` erbt (`PythonClients`, `PythonQuelle`).

### 12.6 Client: Rahmen, Anschlussleiste, Leitungen

- **Client-Rahmen** je `IClientVertrag`, rechts neben allen Domänen (gestrichelt, Kopf: 🔌 Name · verkörperte Akteure · Fokus · ⤢).
- **Die Karte ist die Anschlussleiste** — der Vertrag, in jeder Ansicht lesbar: „↺ trägt IX · als Akteur" mit je Zusage einer Zeile,
  „▶ sendet als …", „? fragt als …" (gruppiert nach Akteur; ⚠ wenn ihn kein Akteur darf), „◀ hört". Jede Nachricht steht beim Namen.
- **Leitungen:** je Client × Ziel-Rahmen **ein Bündel**, in Worten beschriftet („sendet A, B · fragt Q · hört E …", lange Listen „… und N
  weitere"). Ziel einer getragenen Zusage ist der 📜-Rahmen ihres Akteurs, sonst der Rahmen, in dem die Nachrichten-Karte steht.
  Durchgezogen = trägt eine Zusage mit Ausgabe (die Kette hängt durabel an diesem Client), gestrichelt = nur Senden/Fragen/Hören.
- **Zoom ändert nichts.** Einzelkanten gibt es nur durch eine Handlung: Klick auf eine Leisten-Zeile (genau diese Nachricht), ein Bündel
  oder einen 🔌-Knopf der Stecker-Zeile (die Leitung zu diesem Rahmen). Das Auffächern endet, wenn die Auswahl wechselt.
- **Panel:** Name/Namespace, Handshake-Name und generierte Basis, verkörpert (abgeleitet), **trägt ⊕/✕**
  (Akteure mit Vertrag leuchten), **sendet/fragt ⊕/✕** (Commands, Queries, Quell-Nachrichten — eine Query landet in „fragt"), **hört ⊕/✕**
  (Events), die Leitungen je Ziel-Rahmen (Klick = auffächern). ✓-Karte im Verbinden-Modus = lösen.
- **Schreiben:** neuer Client → Interface in `Clients.cs` des Namespace; bestehender → Abgleich von Basisliste (Teile, `ISendet`, `IFragt`;
  fremde Basistypen bleiben) und Kenntnis-Methoden.
- **⚙ Implementierung (Verhalten draußen):** je Implementierung (🐍 Klasse · Datei) je Zusage/Kenntnis eine Zeile „◀ Event → Ausgänge ▶" mit
  Rumpf-Port. „+ 🐍 Python-Implementierung" legt eine neue Klasse im Python-Paket (`rahmen.pythonPaket`) an; ⟳ baut dabei die Basis — je Methode
  die Signatur der Basis und `raise NotImplementedError`, dann je Methode den Rumpf mit 🤖 füllen.
  Die 🤖-Konsole schreibt Python-Rümpfe chirurgisch (`PythonSync`: Sperre über Hash, Prüfung mit `ast.parse` + Ausgabe-Vertrag, Rückgängig).
- **Anlegen:** Palette „+ 🔌 Client", „+ 👤 Akteur" — neue landen im Namespace des Akteur- bzw. Client-Katalogs. „darf ⊕" bietet alle Sorten
  an, die die Grammatik einem Akteur erlaubt (Command · Query · Transient · Quell-Nachricht); „ƒ bietet an" = `IDarf<IFunktion>`.

### 12.7 Ableitung, Parität, Diagnose

- **Eine Quelle:** C# (`AkteurAnteile`, `Grammatik`) rechnet — auch über Flüsse und Katalog-Funktionen; das Board bekommt die
  Akteur-Mengen als `rahmen.akteurMengen`, die Grammatik als `rahmen.grammatik`.
- **`--sonde`** enthält frei benannte Akteure, Verträge und Clients (n:m) sowie gezeichnete, über den Scaffolder geschriebene.
- **Browser-Diagnose:** `deAkteure()`, `deClients()`. Layout-Version `KPOS_VERSION 8`.
- **Offen:** Editor als Client mit Sim-Token (Zielbild `docs/konzept-werkbank.md` §6), Fokus entlang der Kette über Domänen.
