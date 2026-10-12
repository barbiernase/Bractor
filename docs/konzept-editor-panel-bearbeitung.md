# Konzept — Bearbeiten nur im Panel

> **Gekürzt 2026-10-12** auf das mit dem Zielbild `docs/konzept-werkbank.md` Vereinbare.

> **Stand:** 2026-09-29 · **Status:** UMGESETZT als Hybrid (§3.3, §8) · **Ort:** `GraphExtractor/HtmlPresenter.cs` (Editor-JS)
> Verwandt: [konzept-domaenen-editor.md](konzept-domaenen-editor.md) (§1–3 Node-Modell/Ports),
> [konzept-llm-minimalkontext.md](konzept-llm-minimalkontext.md) §12 (🤖-Chat).

## 1 · Die Frage

Kann die **Detail-Sicht** weg — also aufklappbare Karten mit Formular und Port-Punkten direkt auf der Fläche — sodass
**Verbindungen und neue Elemente nur noch über das Panel** entstehen?

**Antwort: ja, und es vereinfacht den Editor deutlich.** Der Graph zeigt dann nur noch, *was es gibt und wie es
zusammenhängt* (kompakte Karten + Kanten, typreine Spalten). Das Panel ist die **einzige** Stelle, an der man etwas
ändert, und zwar immer für **genau einen** Knoten.

## 3 · Zielbild

```
┌─────────────────── Graph (nur lesen + wählen) ──────────────────────┐   ┌──────── Panel (einziger Editor) ────────┐
│ kompakte Karten · typreine Spalten · Kanten auf Knotenebene          │   │ Kopf: Typ · Name · Fokus · ✕             │
│ Klick = auswählen (+ Fokus)  ·  der Zoom zeigt nichts anderes         │──▶│ Felder des Knotens                       │
│ die Spur liegt über den Karten                                       │   │ Verbindungen (⊕)                         │
│ KEIN Aufklappen, KEINE Port-Punkte, KEIN Ziehen von Kanten           │   │   ◀ Eingang   Ausgang ▶   ＋ neu …       │
└──────────────────────────────────────────────────────────────────────┘   │ Nachbarn zum Springen                    │
                                                                           └──────────────────────────────────────────┘
```

### 3.1 Graph = nur lesen und wählen
- Jede Karte zeigt immer nur Kopf + Kurzfassung (heute `kurzfassung(n)`); kein Körper, keine Ports.
- Kanten kommen aus **einer** Quelle: `boardEdges()` (Knoten → Knoten). `drawEdges` mit Slot-Ankern entfällt.
- Layout: typreine Spalten (`packLayout`: Aggregat-Blöcke). Die Positionen liegen in der Ansichts-Datei, eine einzige
  Positionsquelle.
- Der Zoom zeigt nichts anderes; es gibt keine Ansichts-Stufen.
- Klick wählt aus und öffnet das Panel; der **Fokus** hebt die Nachbarschaft hervor.

### 3.2 Panel = einziger Editor (genau ein Knoten)
Drei Blöcke, für jeden Knotentyp gleich aufgebaut:

1. **Felder:** das heutige Formular (Name, Namespace, Felder, Häkchen wie Pull/Append …).
2. **Verbindungen:** jeder heutige Port wird eine Zeile mit
   - **Chips** der aktuell verbundenen Knoten (Klick = hinspringen, ✕ = lösen),
   - einem **⊕**, das *nur passende* Ziele auf dem Graphen leuchten lässt (§3.3),
   - **＋ neu**: legt einen Knoten des passenden Typs an, verbindet ihn sofort (`applyLink`) und springt hin.
3. **Nachbarn:** ◀ Eingang / Ausgang ▶ zum Springen (gibt es schon).

Beispiele (die Zeilen fallen aus den heutigen Ports ab, nicht neu erfunden):

| Knoten | Verbindungs-Zeilen im Panel |
|---|---|
| Command | ▶ entschieden von: Decider · ◀ gesendet von: Regel / Reaktion / Fluss |
| Decider | ◀ Command · ▲ Aggregat · ▶ Ergibt (OneOf-Liste, Reihenfolge per ↑↓) · ◀ Rumpf (Code aus der Datei, 🤖 füllen) |
| Applier | ◀ Event · ▲ Aggregat · ◀ Rumpf |
| Regel | → Prozess · ◀ Wenn (Liste, Join) · ▶ Dann (je Command, ×N-Häkchen) · ↩ Kompensation |
| Projektion | je Handle: ◀ Event · ▶ ruft Write-Fn (Liste) · ▶ veröffentlicht · ◀ Rumpf · Häkchen Pull/Append |
| Reader | ▶ liest Projektion · je Handle: ◀ Query · ▶ Read-Fn · ▶ Responses · ◀ Rumpf |
| Feld | ◀ Typ per ⊕ (Skalar/Record/Enum) — ersetzt den `ftype`-Port |

### 3.3 Hybrid: im Panel starten, auf dem Graphen wählen (Verbinden-Modus)

Das Beste aus beiden Welten: **was** verbunden wird, sagt das Panel; **womit**, zeigt der Graph.

1. Im Panel hat jede Verbindungs-Zeile einen Knopf **⊕ auf dem Graphen wählen**.
2. Klick → **Verbinden-Modus** für genau diesen Port: alle **passenden** Knoten leuchten (was passt, sagt der Server),
   alles andere wird abgeblendet, bereits verbundene tragen ein ✓. Die Kamera passt die Kandidaten ein (`passeEin`).
3. **Klick auf eine leuchtende Karte = verbinden** (`applyLink`), Klick auf eine ✓-Karte = lösen. Einfache Ports
   (Decider → Command) beenden den Modus nach einem Klick; Listen-Ports (Ergibt, Wenn-Join, Write-Fns, Dann) bleiben
   offen, bis **Esc / Fertig**. Eine Vorschau-Kante folgt der Maus vom Quellknoten.
4. Hat die Ziel-Karte **mehrere** passende Ports (z. B. ein Store mit mehreren Write-Fns), öffnet der Klick eine kleine
   Auswahl direkt an der Karte.
5. **＋ neu** in derselben Zeile legt den passenden Knoten an, verbindet ihn und zeigt ihn leuchtend in seiner Spalte.

Damit bleibt das Räumliche (man sieht, *wo* das Ziel liegt), ohne Port-Punkte auf den Karten und ohne
Freihand-Ziehen, und für eine Vorführung wirkt es wie Verdrahten.

### 3.4 Neue Elemente
- **Kontextuell (Hauptweg):** „＋ neu“ in einer Verbindungs-Zeile, z. B. am Command „＋ Decider“, am Decider
  „＋ Event“. Der neue Knoten ist sofort richtig verdrahtet, damit entstehen keine Inseln.
- **Frei:** die Palette bietet nur Anker (Aggregat · Fluss · Funktion · Quelle · Akteur · Client · Projektion). Sie legt
  den Knoten an, das Layout setzt ihn in seine Spalte, und das Panel öffnet sich sofort mit leerem Formular.

## 4 · Warum das trägt

- **Ein Weg je Aufgabe** (wie Emit/`CommandEmitter` im Backend): kein zweites Formular, keine zweite Kanten-Quelle,
  keine zweite Positionsquelle.
- **Der Code bleibt die Wahrheit:** was an einen Port passt, sagt der Server (aus der Grammatik). Nur die
  *Bedienoberfläche* der Ports wechselt vom Punkt auf der Fläche zum ⊕ im Panel.
- **Weniger Fehlerklassen:** Sichtmitte-Positionen, Spalten-Mischung, verwaiste Slot-Anker nach dem Einlesen und
  ▸/▾-Zustände fallen weg.
- **Schmal nutzbar:** kein Hantieren mit kleinen Port-Punkten, geht auch im schmalen Fenster.

## 5 · Was man aufgibt (ehrlich)

- **Die ComfyUI-Geste** „Punkt auf Punkt ziehen“ und das grüne Aufleuchten gültiger Ziele beim Ziehen. Ersatz: ⊕
  lässt nur gültige Ziele leuchten. Das ist die Kernentscheidung, denn der Editor wurde bewusst als ComfyUI-Board gebaut.
- **Positionen als eigener Zustand im Modell:** Positionen liegen nur noch in der Ansichts-Datei (eine Positionsquelle).
- **Alles auf einen Blick editieren** (mehrere Karten gleichzeitig aufgeklappt) geht nicht mehr. Es gibt immer genau einen
  Knoten im Panel.

## 8 · Umgesetzt (2026-09-29)

Entschieden: **Hybrid** (§3.3). Die Fläche zeigt nur, das Panel ist der einzige Editor, das Ziel wird auf dem Graphen gewählt.

| Was | Wie (Code) |
|---|---|
| Karten immer kompakt | `istZu()` ist immer wahr; ▸/▾, „▣ Kompakt“, „Alle zu/auf“ entfernt; `VIEW.kompakt`/`details` fest an. |
| Panel = genau ein Knoten | `zeigeInspector` zeigt nur den gewählten Knoten + Nachbarn zum Springen. |
| Ports nur im Panel | `port()` startet im Panel `vbStart`, auf der Fläche nichts; Freihand-Ziehen (`startLink`/`hitSlot`) entfernt. Im Panel erscheinen Ports als ⊕-Punkte. |
| Verbinden-Modus | `vbStart` → `vbZeige`: Kandidaten = `compatible(quelle, port)` über `SLOTS` (die Karten bauen ihren Körper unsichtbar, die Ports sind also registriert); Kandidaten einpassen (`passeEin`); Leiste mit Filter, „＋ neu“, „Fertig (Esc)“. |
| Verbinden / Lösen | Klick auf eine leuchtende Karte → `vbKlick` → `applyLink` bzw. `loeseLink` (neu: das Gegenstück zu `applyLink` für alle Port-Typen). ✓ = gezeichnete Kante (`KANTEN` aus `drawEdges`) genau an diesem Port; offene Sammel-Ports (`…:open`) zählen je Handle. |
| Einzel- vs. Listen-Port | `istEinzel(info)`: Einzel-Ports (Decider→Command, Applier→Event, Regel→Prozess, Rumpf …) beenden den Modus nach einem Klick. |
| Mehrere Anschlüsse an einer Karte | kleine Auswahl an der Karte (`.gvbwahl`), beschriftet mit dem Namen des Anschlusses (z. B. Store-Funktion). |
| ＋ neu | `neuArtFuer(info)` → `neuerKnoten(art,…,{still:true})` → gleich verbinden. |
| Neue Knoten ohne Sichtmitte | `neuerKnoten` ohne Position; `packLayout` legt sie unter den untersten Knoten derselben Art (gleiche Aggregat-Gruppe), bei Überlappung wird neu gepackt. Wechselt ein Knoten durch Verbinden die Gruppe, zieht er um (`mitUmzug`). Palette öffnet sofort das Panel des neuen Knotens. |

**Nachgebessert (2026-09-29, zweite Runde):**
- **Modus bleibt offen**, bis Fertig/Esc. Auch bei Einzel-Ports: Ein Klick auf eine andere Karte **ersetzt** das Ziel
  (✓ wandert mit). Vorher endete der Modus nach dem ersten Klick, und der nächste Klick wählte eine andere Karte aus.
- **Kein Flackern:** Im Modus löst eine Änderung keinen Voll-Render mehr aus (`imModus` → `teilNeu`). Nur die
  betroffenen Karten, die Kurzfassungen, die Kanten und das Panel werden erneuert; Fläche, Zoom und Layout bleiben stehen.
  Umziehen gilt nur für neue, bisher unverbundene Knoten und läuft ohne Voll-Render (`packLayout`).
- **Tempo:** `drawEdges` hat Lesen (Anker) und Schreiben (DOM) verschränkt, was je Kante ein Voll-Layout erzwang
  (1352 ms bei 684 Kanten). Jetzt werden die Pfade gesammelt und einmal eingehängt. Ein Klick im Modus dauert
  **≈ 220 ms statt ≈ 1660 ms**; davon profitiert auch Verschieben und Neuaufbau.
- **Ansicht nur per Hand:** Die Zoomstufe „Detail“ ist weg; der Zoom schaltet nichts mehr um.

**Vereinfacht (2026-09-30, dritte Runde):** keine Leiste mehr (kein Filter, kein „＋ neu“). **⊕ im Panel = Modus an**,
passende Karten leuchten, **anklicken = verbinden / lösen**. Beenden: **Esc**, **derselbe ⊕ nochmal**, Klick ins Leere
oder Klick auf eine nicht passende Karte (die wird dann ausgewählt). Die Kamera bewegt sich nur, wenn kein passender
Knoten im Bild ist. Gegen Flackern ersetzt ein Klick nur noch die betroffenen Karten und die **tatsächlich geänderten**
Kurzfassungen (gemessen: 2 Karten + 1 Kurzfassung statt 445), das Panel behält seine Scroll-Position; ≈ 170 ms je Klick.
Neue Knoten entstehen über die Palette oben (Panel öffnet sich sofort).

**Schrift zoomt mit (2026-09-30):** Früher wurde die Schrift gegen den Zoom ausgeglichen (`--inv` = 1/Zoom, also
bildschirm-konstant). Jetzt hat sie feste Welt-Größen und skaliert wie die Karten. Kartentitel 12 px, gemessen: Karte und
Schrift beide ×1,77 bei ×1,77 Zoom. `KPOS_VERSION` (2) packt die gespeicherte Anordnung einmalig neu, weil sich die Kartengrößen geändert haben.

**Zeilenraster (2026-09-30):** Jede Karte belegt genau eine Rasterzeile (Höhe = höchste sichtbare Karte + 12 px). Ein Knoten
steht in der Zeile seines Partners: Command → Decider, Event → Applier (sonst Decider), Ablehnung → Decider, Query/Response →
Reader (erste freie Zeile ab dort). Eine Raster-Signatur (sichtbare Knoten) packt neu, sobald sich das Raster ändert.

**👁 Ausblenden (2026-09-30):** Auswahl-Menü in der Ansicht-Leiste (auch über 🗂 Domänen) mit Häkchen je Knotenart (z. B. Ablehnung,
Value Object) und darunter die Domänen. Gespeichert in der Ansichts-Datei. Ausgeblendete Arten reservieren im Raster nichts.

## 9 · Leseseite schreibt in den Code (2026-09-30)

Die Verbinden/Lösen-Aktionen der Leseseite haben jetzt einen typisierten Träger im Code (Volltext:
`docs/konzept-handle-ausgaenge.md` §12):

- **„+ Write-Funktion“/„+ Read-Funktion“** an der Store-Karte → neue Fn; im Fn-Panel **„Fähigkeit“** (leer = Vorschlag
  `I` + Methode ohne `Async`). Die Geste schreibt sofort: Sie legt das Fähigkeits-Interface an, hängt es an die Basisliste des Bündels und ergänzt
  in der (einzigen) Impl-Klasse eine Platzhalter-Methode. Aus dem Code gelesene Fähigkeiten sind fest.
- **Handle „+ Write-Fn ▶“/„+ Read-Fn ▶“ → Fn anklicken** (verbinden) bzw. **✕** (lösen) = Fähigkeits-Parameter hinzu/weg — nur die
  Parameterliste; benutzt der Rumpf einen gelösten Parameter noch, wird die Karte rot mit dem
  Compiler-Fehler.
- **Neuer Store/Projektion/Reaktion/Reader, „+ Query andocken“** → neue Dateien (Platzhalter-Rümpfe) bzw. neue Handles in der
  bestehenden Klasse. Neuer Store: Impl-Klasse im Panel benennbar (leer = Name ohne `I`).
- Nach dem Schreiben liest der Editor den Code neu ein.

## 11 · Grammatik und Module in der Bedienung (2026-09-30)

Volltext: `docs/konzept-editor-komposition.md` §11 (Grammatik, Kapselung).

**Verbinden nach der Grammatik.** Der Verbinden-Modus fragt für jeden typgleichen Kandidaten zusätzlich die Grammatik
(`rahmen.grammatik`, eine Quelle: `DomainEditor/Grammatik.cs`):

- **grün leuchtend** = erlaubt (wie bisher, ✓ = schon verbunden — bestehende Verbindungen bleiben immer lösbar);
- **rot „✕ Regel"** = typgleich, aber verboten (z. B. ein Command, den schon ein anderes Aggregat entscheidet; eine Query, die
  schon ein Reader beantwortet). Hover zeigt den Grund, ein
  Klick verbindet **nicht**, sondern nennt die Regel beim Namen (Flash + Ausgabe-Panel, z. B.
  „[GR-COMMAND] … — Regel »Command → genau ein Aggregat« (GR-COMMAND; Build: CQRS010, CQRS002)"); der Modus bleibt offen.
- **📐 Grammatik** (Kopfleiste) listet Sorte → Eingang mit Kardinalität, wer was erzeugt, und je Regel ihr Build-Gegenstück.
- Die übrigen Grammatik-Befunde meldet der **Build** als Analyzer (Code = Regel-Id), u. a. Garantie, Zyklus ohne Aggregat,
  Regel Z (Zustand in Projektion/Reaktion, Hinweis) und offene Modul-Ports (offen, `konzept-editor-komposition.md` §11.1).

**Module als Ebenen** (eingeklappte Modul-Karten, aufklappen = hineingehen) waren kurz gebaut und sind zurückgenommen — die
Oberfläche bleibt wie in §8–§9; die Aufklapp-Semantik kommt später.

**Blank-Start und Domänen laden (2026-09-30).** Der Editor startet **leer**; ein Start-Dialog („Womit starten?") listet die Module
(Namespaces unter der Wurzel, eingerückt nach Hierarchie, mit Anzahl der Code-Elemente). **◻ Leer starten**, einzelne Domänen
anhaken (ein Häkchen am Eltern-Modul lädt alles darunter) und **Laden**, oder **Alles laden**. Wieder öffnen über **🗂 Domänen laden**
(Kopfleiste) bzw. den 📂-Knopf in der Ansicht-Leiste; die letzte Wahl ist vorausgewählt (aus der Ansichts-Datei). Das ist reine
Editor-Sicht: Nicht Geladenes wird nicht gezeigt, ist kein Verbindungsziel und erscheint auf den Schnittstellen nicht; der Code
bleibt vollständig. Der alte Aggregat-Filter liegt unter „👁 Ausblenden".
