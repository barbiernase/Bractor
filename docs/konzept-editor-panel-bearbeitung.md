# Konzept — Bearbeiten nur im Panel, der Graph wird reine Landkarte

> **Stand:** 2026-09-29 · **Status:** UMGESETZT als Hybrid (§3.3, §8) · **Ort:** `GraphExtractor/HtmlPresenter.cs` (Editor-JS)
> Verwandt: [konzept-domaenen-editor.md](konzept-domaenen-editor.md) (§1–3 Node-Modell/Ports, §10 Merge),
> [konzept-llm-minimalkontext.md](konzept-llm-minimalkontext.md) §12 (🤖-Chat).

## 1 · Die Frage

Kann die **Detail-Sicht** weg — also aufklappbare Karten mit Formular und Port-Punkten direkt auf der Fläche — sodass
**Verbindungen und neue Elemente nur noch über das Panel** (Inspector) entstehen?

**Antwort: ja, und es vereinfacht den Editor deutlich.** Der Graph zeigt dann nur noch, *was es gibt und wie es
zusammenhängt* (kompakte Karten + Kanten, typreine Spalten). Das Panel ist die **einzige** Stelle, an der man etwas
ändert, und zwar immer für **genau einen** Knoten.

## 2 · Ist-Zustand: heute gibt es alles doppelt

| Aufgabe | Weg 1 (Fläche) | Weg 2 (Panel) |
|---|---|---|
| Felder/Namen bearbeiten | aufgeklappte Karte (`fuelleKoerper`) | Inspector (`fuelleKoerper`, dieselbe Funktion) |
| Verbinden | Port-Punkt ziehen → `compatible` → `applyLink` (59 `reg(…)`-Ports, `SLOTS`) | **nicht möglich**: `reg()` registriert im Inspector bewusst nichts (`INSP`) |
| Neues Element | Palette / Doppelklick-Picker → Position in Sichtmitte (`spawnPos`) | nur Sonderfälle (＋📝, 🤖 LLM-Knoten hinzufügen) |
| Kanten zeichnen | `drawEdges` (115 Zeilen, Slot → Slot, braucht gerenderte Port-Punkte) | — (`boardEdges` = Knotenebene, parallel gepflegt) |
| Ansichten | Kompakt ⇄ Voll, ▸/▾ je Karte (`_offen`/`_collapsed`), „Alle zu/auf“, LOD Landkarte/Ablauf/Detail | — |
| Positionen | zwei Layouts: `x/y` im Modell (Voll) **und** `KPOS` im Browser (Kompakt) | — |

Folgen, die wir in dieser Session gesehen haben: typgemischte Spalten durch Sichtmitte-Positionen, LLM-Knoten, die nach
dem Einlesen „verschwinden“, zwei Kanten-Quellen mit Drift-Risiko, ein Panel, das mehrere Knotentypen mischte.

## 3 · Zielbild

```
┌─────────────── Graph (Landkarte, nur lesen + wählen) ───────────────┐   ┌──────── Panel (einziger Editor) ────────┐
│ kompakte Karten · typreine Spalten · Kanten auf Knotenebene          │   │ Kopf: Typ · Name · ◎ · ✕                 │
│ Klick = auswählen (+ Slice-Fokus)  ·  Zoom: Landkarte / Ablauf        │──▶│ Felder des Knotens                       │
│ Simulation animiert weiter auf den Karten                            │   │ Verbindungen (typisierte Picker)         │
│ KEIN Aufklappen, KEINE Port-Punkte, KEIN Ziehen von Kanten           │   │   ◀ Eingang   Ausgang ▶   ＋ neu …       │
└──────────────────────────────────────────────────────────────────────┘   │ Nachbarn zum Springen                    │
                                                                           └──────────────────────────────────────────┘
```

### 3.1 Graph = Landkarte
- Jede Karte zeigt immer nur Kopf + Kurzfassung (heute `kurzfassung(n)`); kein Körper, keine Ports.
- Kanten kommen aus **einer** Quelle: `boardEdges()` (Knoten → Knoten). `drawEdges` mit Slot-Ankern entfällt.
- Layout ist **abgeleitet** (`packLayout`: Aggregat-Blöcke, typreine Spalten, 🤖 neben seinem 📝). Eine einzige
  Positionsquelle.
- Ansicht: **Landkarte** (Kacheln) und **Ablauf** (Karten), nur per Knopf umgeschaltet. Die Stufe „Detail“ entfällt.
- Klick wählt aus und öffnet das Panel; Slice-Fokus und Nachbar-Hervorhebung bleiben.

### 3.2 Panel = einziger Editor (genau ein Knoten)
Drei Blöcke, für jeden Knotentyp gleich aufgebaut:

1. **Felder:** das heutige Formular (Name, Namespace, Felder mit Typ-Dropdown, Häkchen wie Pull/Append …).
2. **Verbindungen:** jeder heutige Port wird eine Zeile mit
   - **Chips** der aktuell verbundenen Knoten (Klick = hinspringen, ✕ = lösen),
   - einem **Picker**, der *nur typ-passende* Ziele anbietet (dieselbe Regel wie heute `compatible`: gleicher
     Port-Typ, entgegengesetzte Richtung),
   - **＋ neu**: legt einen Knoten des passenden Typs an, verbindet ihn sofort (`applyLink`) und springt hin.
3. **Nachbarn:** ◀ Eingang / Ausgang ▶ zum Springen (gibt es schon).

Beispiele (die Zeilen fallen aus den heutigen Ports ab, nicht neu erfunden):

| Knoten | Verbindungs-Zeilen im Panel |
|---|---|
| Command | ▶ entschieden von: Decider · ◀ gesendet von: Regel / Reaktion / Pipeline / Frist |
| Decider | ◀ Command · ▲ Aggregat · ▶ Ergibt (OneOf-Liste, Reihenfolge per ↑↓) · ◀ Rumpf: 📝 (＋📝 · 🤖 LLM-Knoten hinzufügen) |
| Applier | ◀ Event · ▲ Aggregat · ◀ Rumpf |
| Regel | → Prozess · ◀ Wenn (Liste, Join) · ▶ Dann (je Command, ×N-Häkchen) · ↩ Kompensation |
| Projektion | je Handle: ◀ Event · ▶ ruft Write-Fn (Liste) · ▶ veröffentlicht · ◀ Rumpf · Häkchen Pull/Append |
| Reader | ▶ liest Projektion · je Handle: ◀ Query · ▶ Read-Fn · ▶ Responses · ◀ Rumpf |
| Pipeline | je Handle: Eingang (Trigger/Event/Self) · ▶ Commands · ▶ Trigger · ◀ Dienst · ◀ Rumpf |
| Frist | ◀ plant · ◀ storniert · ◀ Dauer (HostSetting) · ▶ fällig → Command |
| Feld | Typ-Dropdown (Skalar/Record/Enum) — ersetzt den `ftype`-Port |
| 📝 Code | Rumpf · 🤖 LLM-Knoten hinzufügen · ✎ Im Editor öffnen |
| 🤖 LLM | Chat (Prompts) · ▶ Senden |

### 3.3 Hybrid: im Panel starten, auf dem Graphen wählen (Verbinden-Modus)

Das Beste aus beiden Welten: **was** verbunden wird, sagt das Panel; **womit**, zeigt der Graph.

1. Im Panel hat jede Verbindungs-Zeile einen Knopf **⊕ auf dem Graphen wählen** (daneben bleibt die Liste mit Suche
   als Rückfall).
2. Klick → **Verbinden-Modus** für genau diesen Port: der Graph springt in die **Ablauf**-Ansicht, alle **passenden**
   Knoten leuchten (dieselbe Typregel `compatible`), alles andere wird abgeblendet (die Slice-Abblendung gibt es schon),
   bereits verbundene tragen ein ✓. Die Kamera passt die Kandidaten ein (`passeEin`).
3. **Klick auf eine leuchtende Karte = verbinden** (`applyLink`), Klick auf eine ✓-Karte = lösen. Einfache Ports
   (Decider → Command) beenden den Modus nach einem Klick; Listen-Ports (Ergibt, Wenn-Join, Write-Fns, Dann) bleiben
   offen, bis **Esc / Fertig**. Eine Vorschau-Kante folgt der Maus vom Quellknoten.
4. Hat die Ziel-Karte **mehrere** passende Ports (z. B. ein Store mit mehreren Write-Fns), öffnet der Klick eine kleine
   Auswahl direkt an der Karte.
5. **＋ neu** in derselben Zeile legt den passenden Knoten an, verbindet ihn und zeigt ihn leuchtend in seiner Spalte.

Damit bleibt das Räumliche (man sieht, *wo* das Ziel liegt), ohne Port-Punkte auf den Karten und ohne
Freihand-Ziehen, und für eine Vorführung wirkt es wie Verdrahten.

**Günstiger Einstieg:** Die Kandidaten lassen sich schon heute aus `SLOTS` berechnen. Jede Karte baut ihren Körper
(und registriert ihre Ports) auch eingeklappt, also genügt `compatible(from, s)` plus `s.closest(".gnode2")` → Knoten.
Der Port-Katalog (Schritt 1) wird erst nötig, wenn die Karten keinen Körper mehr bauen (Schritt 4).

### 3.4 Neue Elemente
- **Kontextuell (Hauptweg):** „＋ neu“ in einer Verbindungs-Zeile, z. B. am Command „＋ Decider“, am Decider
  „＋ Event“, am Rumpf „＋📝“. Der neue Knoten ist sofort richtig verdrahtet, damit entstehen keine Inseln.
- **Frei:** die Palette oben bleibt. Sie legt den Knoten ohne Position an, das Layout setzt ihn in seine Spalte, und das
  Panel öffnet sich sofort mit leerem Formular.

## 4 · Warum das trägt

- **Ein Weg je Aufgabe** (wie Emit/`CommandEmitter` im Backend): kein zweites Formular, keine zweite Kanten-Quelle,
  keine zweite Positionsquelle.
- **Das Modell ändert sich nicht:** `applyLink(O,I)` und `compatible` bleiben die Wahrheit. Nur die *Bedienoberfläche*
  der Ports wechselt vom Punkt auf der Fläche zum Picker im Panel.
- **Weniger Fehlerklassen:** Sichtmitte-Positionen, Spalten-Mischung, verwaiste Slot-Anker nach dem Einlesen und
  ▸/▾-Zustände fallen weg.
- **Schmal nutzbar:** kein Hantieren mit kleinen Port-Punkten, geht auch im schmalen Fenster.

## 5 · Was man aufgibt (ehrlich)

- **Die ComfyUI-Geste** „Punkt auf Punkt ziehen“ und das grüne Aufleuchten gültiger Ziele beim Ziehen. Ersatz: der
  Picker listet nur gültige Ziele. Das ist die Kernentscheidung, denn der Editor wurde bewusst als ComfyUI-Board gebaut.
- **Freies Anordnen per Hand:** Wird das Layout vollständig abgeleitet, entfällt Ziehen und Merken von Positionen.
  Optional behalten: Ziehen nur als flüchtige Ansicht, ohne Speicherung.
- **Alles auf einen Blick editieren** (mehrere Karten gleichzeitig aufgeklappt) geht nicht mehr. Es gibt immer genau einen
  Knoten im Panel.

## 6 · Umsetzung in Schritten (jeweils einzeln lieferbar)

| # | Schritt | Kern | Aufwand |
|---|---|---|---|
| 0 | **Verbinden-Modus (Hybrid, §3.3)** | ⊕ je Port-Zeile im Panel → Kandidaten aus `SLOTS` leuchten in der Ablauf-Ansicht → Klick verbindet/löst; Esc beendet. Rein additiv, sofort nutzbar. | M |
| 1 | **Port-Katalog** | Die 59 `reg(…)`-Stellen liefern ihre Port-Beschreibung (`type`, `dir`, Ziel) in ein Verzeichnis je Knoten, **ohne** dass die Karte gerendert sein muss. Voraussetzung für Picker, die alle Kandidaten kennen. | M |
| 2 | **Verbindungs-Zeilen im Panel** | Generisch aus dem Katalog: Chips + Picker (`compatible`) + ✕ lösen (neu: `loeseLink`, Gegenstück zu `applyLink`). | M |
| 3 | **＋ neu im Picker** | `neuerKnoten(kind)` ohne Position + `applyLink` + `waehle(neu)`. Palette ebenso ohne `spawnPos`. | S |
| 4 | **Fläche entschlacken** | Karten ohne Körper und Ports; `drawEdges` → Kanten aus `boardEdges`; Kompakt-/Voll-Schalter, ▸/▾, „Alle zu/auf“, LOD „Detail“ raus. | M |
| 5 | **Eine Positionsquelle** | `KPOS` und `x/y` zusammenführen (abgeleitetes Layout); Entscheidung Hand-Anordnung (§5). | S |
| 6 | **Aufräumen** | tote Pfade (Slot-Anker, `_offen`/`_collapsed`, `fokussiereNeu`, `spawnPos`), Doku `konzept-domaenen-editor.md` §1–3/§9 nachziehen. | S |

Schritte 1–3 sind **rein additiv**: Das Panel kann danach schon alles, und die Fläche funktioniert unverändert weiter.
Erst Schritt 4 nimmt etwas weg. So lässt sich der Panel-Weg im Alltag erproben, bevor die Fläche entschlackt wird.

**Prüfung je Schritt:** Editor-Parität `dotnet run --project GraphExtractor -- --check` (Modell unverändert),
Kantenmenge `deGraph()` vorher = nachher, und je Knotentyp einmal Verbinden, Lösen und ＋ neu im Panel.

## 7 · Offene Entscheidungen

1. **Hand-Anordnung**: ganz weg (reines Auto-Layout) oder nur flüchtig ziehen? *Empfehlung:* weg, wegen einer
   Positionsquelle und typreiner Spalten.
2. **ComfyUI-Geste als Zusatz behalten?** *Empfehlung:* nein, sonst bleiben zwei Wege und `drawEdges`/`SLOTS` leben weiter.

## 8 · Umgesetzt (2026-09-29)

Entschieden: **Hybrid** (§3.3). Die Fläche ist Landkarte, das Panel der einzige Editor, das Ziel wird auf dem Graphen gewählt.

| Was | Wie (Code) |
|---|---|
| Karten immer kompakt | `istZu()` ist immer wahr; ▸/▾, „▣ Kompakt“, „Alle zu/auf“ entfernt; `VIEW.kompakt`/`details` fest an. Die Zoomstufen Landkarte/Ablauf/Detail bleiben. |
| Panel = genau ein Knoten | `zeigeInspector` zeigt nur den gewählten Knoten + Nachbarn zum Springen. Alle Knotentypen (Decider, Code, LLM …) sind eigene Karten. |
| Ports nur im Panel | `port()` startet im Panel `vbStart`, auf der Fläche nichts; Freihand-Ziehen (`startLink`/`hitSlot`) entfernt. Im Panel erscheinen Ports als ⊕-Punkte. |
| Verbinden-Modus | `vbStart` → `vbZeige`: Kandidaten = `compatible(quelle, port)` über `SLOTS` (die Karten bauen ihren Körper unsichtbar, die Ports sind also registriert); Ablauf-Ansicht einpassen (`passeEin(…,0.74,0.4)`); Leiste mit Filter, „＋ neu“, „Fertig (Esc)“. |
| Verbinden / Lösen | Klick auf eine leuchtende Karte → `vbKlick` → `applyLink` bzw. `loeseLink` (neu: das Gegenstück zu `applyLink` für alle Port-Typen). ✓ = gezeichnete Kante (`KANTEN` aus `drawEdges`) genau an diesem Port; offene Sammel-Ports (`…:open`) zählen je Handle. |
| Einzel- vs. Listen-Port | `istEinzel(info)`: Einzel-Ports (Decider→Command, Applier→Event, Regel→Prozess, Rumpf …) beenden den Modus nach einem Klick. |
| Mehrere Anschlüsse an einer Karte | kleine Auswahl an der Karte (`.gvbwahl`), beschriftet mit dem Namen des Anschlusses (z. B. Store-Funktion). |
| ＋ neu | `neuArtFuer(info)` → `neuerKnoten(art,…,{still:true})` → gleich verbinden. |
| Neue Knoten ohne Sichtmitte | `neuerKnoten` ohne Position; `packLayout` legt sie unter den untersten Knoten derselben Art (gleiche Aggregat-Gruppe), bei Überlappung wird neu gepackt. Wechselt ein Knoten durch Verbinden die Gruppe, zieht er um (`mitUmzug`). Palette öffnet sofort das Panel des neuen Knotens. |

**Live geprüft (Editor im Browser):**
- Neuer Decider aus der Palette, das Panel öffnet sich sofort.
- ⊕ Command: 31 Commands leuchten, ein Klick verbindet, der Modus endet. ⊕ Aggregat: 6 leuchten.
- ⊕ Ausgang: 59 Events leuchten. Zwei Events verbunden (✓), eines per Klick gelöst, Esc beendet.
- ＋ neu: Event wird angelegt und verbunden; ＋ neu am Rumpf legt einen 📝-Block an und verbindet ihn.
- Projektions-Handle → Store: Die Auswahl listet die sechs Write-Fns mit Namen, ✓ nur an der Funktion dieses Handles.
- Der verbundene Decider zieht in die Decider-Spalte seines Aggregats um.

**Nachgebessert (2026-09-29, zweite Runde):**
- **Modus bleibt offen**, bis Fertig/Esc. Auch bei Einzel-Ports: Ein Klick auf eine andere Karte **ersetzt** das Ziel
  (✓ wandert mit). Vorher endete der Modus nach dem ersten Klick, und der nächste Klick wählte eine andere Karte aus.
- **Kein Flackern:** Im Modus löst eine Änderung keinen Voll-Render mehr aus (`imModus` → `teilNeu`). Nur die
  betroffenen Karten, die Kurzfassungen, die Kanten und das Panel werden erneuert; Fläche, Zoom und Layout bleiben stehen.
  Umziehen gilt nur für neue, bisher unverbundene Knoten und läuft ohne Voll-Render (`packLayout`).
- **Tempo:** `drawEdges` hat Lesen (Anker) und Schreiben (DOM) verschränkt, was je Kante ein Voll-Layout erzwang
  (1352 ms bei 684 Kanten). Jetzt werden die Pfade gesammelt und einmal eingehängt. Ein Klick im Modus dauert
  **≈ 220 ms statt ≈ 1660 ms**; davon profitiert auch Verschieben und Neuaufbau.
- **Ansicht nur per Hand:** Die Zoomstufe „Detail“ ist weg. Landkarte ⇄ Ablauf schaltet nur der Knopf (`VIEW.lod`,
  gespeichert); der Zoom schaltet nichts mehr um. Ein Klick auf eine Landkarten-Kachel und der Verbinden-Modus wechseln
  auf Ablauf.

**Vereinfacht (2026-09-30, dritte Runde):** keine Leiste mehr (kein Filter, kein „＋ neu“). **⊕ im Panel = Modus an**,
passende Karten leuchten, **anklicken = verbinden / lösen**. Beenden: **Esc**, **derselbe ⊕ nochmal**, Klick ins Leere
oder Klick auf eine nicht passende Karte (die wird dann ausgewählt). Die Kamera bewegt sich nur, wenn kein passender
Knoten im Bild ist. Gegen Flackern ersetzt ein Klick nur noch die betroffenen Karten und die **tatsächlich geänderten**
Kurzfassungen (gemessen: 2 Karten + 1 Kurzfassung statt 445), das Panel behält seine Scroll-Position; ≈ 170 ms je Klick.
Neue Knoten entstehen über die Palette oben (Panel öffnet sich sofort).

**Schrift zoomt mit (2026-09-30):** Früher wurde die Schrift gegen den Zoom ausgeglichen (`--inv` = 1/Zoom, also
bildschirm-konstant). Jetzt hat sie feste Welt-Größen und skaliert wie die Karten. Kartentitel 12 px, gemessen: Karte und
Schrift beide ×1,77 bei ×1,77 Zoom. In der Landkarte richtet sich die Schrift nach der Kachelgröße, `--inv` ist ein fester
Faktor 4. `KPOS_VERSION` (2) packt die gespeicherte Anordnung einmalig neu, weil sich die Kartengrößen geändert haben.

**Code-Blöcke unter ihrem Knoten (2026-09-30):** Ein 📝 Code-Block liegt direkt **unter** dem Knoten, den er beschreibt
(Decider, Applier, Projektion, Reader, Pipeline, Store …), sein 🤖 LLM-Knoten direkt darunter. Das ergibt einen kleinen,
leicht eingerückten Stapel in derselben Spalte statt eigener Code-/LLM-Spalten (`layoutBlock` → `stapel`). Code-Blöcke
ohne Besitzer und LLM-Knoten ohne Block bekommen eine eigene Spalte. Gemessen: 168/168 Code-Blöcke unter ihrem Besitzer,
0 Überlappungen. `KPOS_VERSION` 3 packt die Anordnung einmalig neu.

**Zeilenraster (2026-09-30, ersetzt die Stapel):** Jede Karte belegt genau eine Rasterzeile (Höhe = höchste sichtbare Karte
+ 12 px). Ein Knoten mit Code-Eingängen (`codeEingaenge`: Decider, Applier, Dienst je 1; Projektion/Reader/Reaktion/Pipeline je
Handle; Store je Funktion) belegt **1 + je Eingang genau 2 Zeilen**: 📝 Code-Block und 🤖 LLM-Platz. Der LLM-Platz und ein leerer
Code-Eingang bleiben reserviert, auch die Einrückungs-Breite. Dadurch sind alle Besitzer gleich getaktet (Decider/Applier: 210 px)
und ein neuer 🤖 fällt in seinen Platz, ohne dass sich ein anderer Knoten bewegt. Die übrigen Knoten stehen in der Zeile ihres
Partners: Command → Decider, Event → Applier (sonst Decider), Ablehnung → Decider, Query/Response → Reader (erste freie Zeile ab
dort). Eine Raster-Signatur (sichtbare Knoten + Zahl ihrer Code-Eingänge) packt neu, sobald sich das Raster ändert.
Gemessen: 445 Knoten, 0 Überlappungen, 168/168 Code-Blöcke unter ihrem Besitzer, 31/31 Commands auf der Zeile ihres Deciders.
`KPOS_VERSION` 4.

**👁 Ausblenden (2026-09-30):** Auswahl-Menü in der Ansicht-Leiste (auch über 🗂 Domänen) mit Häkchen je Knotenart (z. B. 📝 Code,
🤖 LLM, Ablehnung, Value Object) plus „Ohne Code" und darunter die Domänen. Gespeichert je Browser und Solution (`VIEW.aus`).
Ausgeblendete Arten reservieren im Raster nichts (ohne Code rücken die Decider auf eine Zeile zusammen).

**Nicht umgesetzt (bewusst, nicht nötig):** Port-Katalog (§6 Schritt 1), denn die Karten registrieren ihre Ports weiter
unsichtbar. `drawEdges` bleibt (ankert an den Kopf-Seiten der kompakten Karten).

## 9 · Leseseite schreibt in den Code (2026-09-30)

Die Verbinden/Lösen-Aktionen der Leseseite haben jetzt einen typisierten Träger im Code (Volltext:
`docs/konzept-handle-ausgaenge.md` §12):

- **„+ Write-Funktion“/„+ Read-Funktion“** an der Store-Karte → neue Fn; im Fn-Panel **„Fähigkeit“** (leer = Vorschlag
  `I` + Methode ohne `Async`). „C# schreiben“ legt das Fähigkeits-Interface an, hängt es an die Basisliste des Bündels und ergänzt
  in der (einzigen) Impl-Klasse eine Platzhalter-Methode. Aus dem Code gelesene Fähigkeiten sind fest.
- **Handle „+ Write-Fn ▶“/„+ Read-Fn ▶“ → Fn anklicken** (verbinden) bzw. **✕** (lösen) = Fähigkeits-Parameter hinzu/weg — nur die
  Parameterliste; benutzt der Rumpf einen gelösten Parameter noch, meldet der Bau nach dem Schreiben den Compiler-Fehler im
  Ausgabe-Panel.
- **Neuer Store/Projektion/Reaktion/Reader, „+ Query andocken“** → neue Dateien (Platzhalter-Rümpfe) bzw. neue Handles in der
  bestehenden Klasse. Neuer Store: Impl-Klasse im Panel benennbar (leer = Name ohne `I`).
- Nach dem Schreiben liest der Editor den Code neu ein; die Leseseite wird dabei über „Store.Fn“ verglichen, Geschriebenes ist
  danach Code-Stand (nicht „ungeschrieben“).

## 10 · Bestehendes ändern, Betrieb, Vorschau (2026-09-30)

Auch Änderungen an Bestehendem gehen jetzt in den Code (Volltext `docs/konzept-handle-ausgaenge.md` §13): Felder, Enum-Werte,
State-Felder, OneOf, Prozess-Regeln, Handle-Ausgänge, Store-Fn-Signatur, Flags (Pull/Append/TrackDeps/SubscriberId/Projektion).
Neu im Panel: **SubscriberId** an Projektion/Reaktion, **Konfigs** an der Pipeline (Konstruktor nur bei neuer Pipeline), Palette
**„+ Konfig“**. **👁 Vorschau** zeigt vor dem Schreiben, was angelegt/geändert würde und was NICHT (mit Grund).

