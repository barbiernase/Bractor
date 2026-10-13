# Konzept: Pipelines im Domänen-Editor

> **Zielbild ist `docs/konzept-werkbank.md`; wo dieses Dokument widerspricht, gilt das Zielbild.** Dieses Dokument enthält nur noch:
> §12 Nachrichten mit zwei Seiten (gekürzt), §13.6 Ablauf als Kette, §14 Pipeline als Fluss (kanonisch: eine Pipeline ist eine frei
> verdrahtete Fläche aus Katalog-Knoten, Quelle → Funktionen → Commands) und den Zuordnungs-Editor. Das Handle-Modell davor
> (§1–§11, §13.1–§13.5) ist entfallen.

> **Einordnung:** Dieses Dokument ist eine *Anwendung* der allgemeinen Kompositions-Sprache
> (`docs/konzept-editor-komposition.md`): Alphabet (Kanäle, Bausteine), Grammatik, Operatoren (Schleife, Warten,
> Verkettung), Kapselung und Linsen gelten dort allgemein. Die Bestands-Pipelines dienen nur als Beispiele.

## 12 · Nachrichten mit zwei Seiten: „◀ kommt aus" / „geht an ▶" (2026-10-01, umgesetzt)

**Leitidee.** Jede Nachricht ist eine Karte mit **zwei Seiten**: ◀ *kommt aus* (Erzeuger) und *geht an* ▶ (Konsumenten). Das ist
dieselbe Kante wie der Ausgang am Erzeuger — **eine** Wahrheit (die OneOf-Signatur des Erzeugers), zwei Enden. „kommt aus" ist
keine zweite Liste am Command, sondern die umgekehrte Sicht; Verbinden von der Nachricht aus schreibt in den OneOf des Handles.

| Nachricht | ◀ kommt aus | geht an ▶ |
|---|---|---|
| Command | beliebig: Außenwelt (abgeleitet), Prozess, Reaktion | genau ein Aggregat (Decider) |
| Event | genau ein Aggregat (Decider); reaktiv veröffentlicht | beliebig: Applier, Prozess, Projektion, Reaktion, Pipeline |
| Transient | Decider (Ablehnung), Projektion/Reaktion/Pipeline (veröffentlicht) | beliebig: Projektion, Reaktion, Pipeline |
| Query / Response | Außenwelt / Reader | Reader / Außenwelt |

Die Kardinalität kommt aus der Grammatik (`rahmen.grammatik.konsume`), nicht aus dem Board-Code.

**Umsetzung.**
1. **Nachrichtenkarten** (Command/Event/Ablehnung/Query/Response) zeigen „◀ kommt aus" und „geht an ▶" als Listen der
   Partner (anklickbar = springen; ✕ an Handle-Ausgängen = lösen) plus je Seite einen ⊕-Port. Die Ablehnung/transientes Event hat
   jetzt einen Ausgang.
2. **Grenz-Partner:** ein Partner aus einer nicht geladenen Domäne erscheint als „↗ außerhalb · Namespace"; Klick lädt die Domäne
   dazu. Die Kurzzeile zeigt „↗ n außerhalb".

## 13 · Nachvollzug Programmiermodell ⇄ Editor (2026-10-07, umgesetzt)

§13.1–§13.5 entfallen (Handle-Modell), siehe Zielbild `docs/konzept-werkbank.md`.

### 13.6 Ablauf als Kette: jede Funktion ein Knoten (2026-10-07)

Vorgabe: „erst die, dann die“ — eine Funktion ist ein Knoten, die Reihenfolge eine Kante. Regel-Karten, Aufträge und Ergebnis-Events
dazwischen waren die Hürde. Jetzt (nur Editor-Darstellung + Bearbeiten; Code und Laufzeit unverändert — darunter liegen dieselben
Prozess-Regeln):

- **Kettenschritt** (`kettenschritt`): eine Regel mit genau einem Ziel (ƒ oder Command), ausgelöst von einem Event, optional zusammen
  mit dem Start-Event (Kontext), ohne Kompensation/Fan-out/Sammeln. Sie wird als **Kante** gezeichnet: Prozess (Start) bzw. die Funktion,
  die das auslösende Ergebnis liefert → nächster Schritt; Beschriftung „erst“/„dann“ (+ ⏳ bei Zeitlimit), Klick öffnet die Regel
  (Argumente, Zeitlimit). Andere Regeln bleiben Karten.
- **Nicht eingeklappt (2026-10-11, Revision):** Auftrag und Ergebnis-Events einer Funktion sind IMMER eigene Karten — die Funktion ist
  definiert wie ein Decider: Spalten **Auftrag → ƒ Funktion (Rumpf aus der Datei, „🤖 füllen“) → Ergebnis**, auf einer Zeile, im selben Block und
  Akteur-Rahmen (Akteure der Funktion = die ihres Auftrags). Fluss-Knoten und Prozess-Regeln verweisen nur auf die ƒ-Karte.
- **Anordnung**: je Kettentiefe eine eigene Funktions-Spalte, Zeile = die des Vorgängers → Prozess → ƒ → ƒ auf einer Linie.
- **Bearbeiten**: am Prozess „bei ‹Auslöser› ▶ erst ⊕“, an jeder Funktion je Ergebnis „bei ‹Ergebnis› ▶ dann ⊕“ — eine Funktion oder
  einen Command auf dem Graphen anklicken hängt den nächsten Schritt an, nochmal anklicken (✓) bzw. ✕ löst ihn (2026-10-11: ⊕ statt
  Pool-Dropdown). Ein Command als Schritt bekommt den Start
  als Kontext in den Join (er braucht dessen Daten).

**Nachtrag (gleicher Tag) — wie Node-RED:** Nicht nur einfache Schritte, JEDE Prozess-Regel wird Kanten (`kettenKanten`):
Verzweigung = eine Kante je Ergebnis („bei BildNichtLesbar“), Zusammenführung = mehrere Drähte in einen Knoten („und“; UndAlle „alle“),
Fan-out „×N“, Kompensation gestrichelt rot „↩ rückgängig“, Zeitlimit „⏳“. Nur eine leere, gerade angelegte Regel bleibt Karte.
**Argumente** werden beim Anhängen automatisch zugeordnet (`autoArgs`: gleicher Name → Name endet gleich → einziger gleicher Typ;
Wert-Objekte aus ihren Feldern gebaut; jüngstes Ergebnis vor dem Start) und werden im **Zuordnungs-Editor** visuell geändert
(2026-10-11: Knoten + Drähte statt Tabelle, siehe §14 „Zuordnungs-Editor“; ein aus dem Code gelesener λ wird beim Öffnen in Knoten
zerlegt und erst bei einer Änderung ersetzt). Für den
echten Prozess trifft die Zuordnung exakt den handgeschriebenen Lambda von `MeldeBildVerfuegbar`; nur Konstanten ohne Quelle
(Höhe 512) bleiben `default`. Scaffolder: Argumentlisten jetzt auch für Aufträge (`Rufe`), nicht nur für Commands.


## 14 · Pipeline als Fluss (2026-10-07, kanonisch)

### 14.1 Befund, der zu diesem Schritt führte

Der Weg einer Datei lief über zwei Bausteine und einen Umweg: `FileWatchPipeline` (PollTick, Start) → Trigger `DateiErkannt` →
`ImageProcessingPipeline` (Dateiname deuten **im Rumpf**) → `NimmRohbildAuf` → Aggregat → `RohbildEingegangen` → Prozess → ƒ → ƒ →
Command. Vier Fehler: (1) die Verarbeitung begann erst NACH einem Umweg durchs Aggregat (`NimmRohbildAuf` existierte nur, um den
Trigger ins Log zu bringen); (2) die Quelle zeigte Mechanik (Tick, Start) statt Bedeutung; (3) Pipeline-Handle, Prozess-Regel und
Reaktion sind drei Bausteine für eine Idee; (4) das Board ordnete nach Art (≈15 Spalten), nicht nach Fluss.

### 14.2 Das Modell: eine Fläche, eine Knotenform, ein Katalog

**Eine Pipeline ist eine Fläche, auf der Knoten aus dem Katalog mit Drähten verbunden werden.** Sie beginnt an einer **Quelle**; in die
Aggregat-Welt geht es nur über einen Knoten, der ein Command sendet.

- **Jeder Knoten hat dieselbe Form: ein Eingang, OneOf-Ausgänge (ein Port je Fall).**
  - **Quelle** — kein Eingang, ein Ausgang. Quellen sind fest codierte Treiber + Konfig (Zielbild §3), oder ein **Event aus dem Log**
    (`p.Auf<E>()`), womit jede bisherige Event-Pipeline/Saga ein Sonderfall ist.
  - **Funktion** — Auftrag hinein, Ergebnis-Fälle heraus (`IFunktion`).
  - **Aggregat-Knoten** — Command hinein; Ausgänge sind die Events aus der Decide-Signatur. Der Fluss kann nach der Antwort
    weiterlaufen. *Aggregat und Funktion sind derselbe Knotentyp; nur das Aggregat hat Gedächtnis.*
- **Ein Draht ist ein Typ** (Ergebnis-Fall eines bestimmten Knotens). **Zuordnung statt `msg`:** die Felder eines Eingangs werden aus
  den Nachrichten am Draht gebaut; was ein Knoten aus einem früheren Schritt braucht, holt er als weiteren Draht (∧ mit einem
  Vorgänger — im Editor als „Kontext“ angezeigt). Die Funktionen bleiben domänenfrei.
- **Eine Quell-Nachricht = ein Vorgang.** Zwischen Vorgängen gibt es keine Joins — wer zwei Vorgänge zusammenbringen muss, braucht
  Gedächtnis, und das gehört ins Aggregat (Regel Z). Dessen Event ist die Quelle der nächsten Pipeline.

### 14.3 Fluss-Semantik: parallel, verzweigen, warten

| Form | Darstellung | Bedeutung |
|---|---|---|
| Ein Port, mehrere Drähte | Auffächern | **alle** bekommen die Nachricht — parallel |
| Mehrere Ports eines Knotens | OneOf-Fälle | **genau einer** feuert — Verzweigung (eine Weiche ist eine Funktion) |
| Eingang mit mehreren Drähten | Abzeichen | **∧ `Alle(…)`** warten, oder **∨ `.Oder(…)`** jeder Weg einzeln |
| Fehler-Ports | ⏳ / ✕ | **`BeiZeitlimit()`**, **`BeiAbgelehnt()`** — verdrahtet = Weg, frei = Vorgang scheitert (wie bisher). Eine Ablehnung des Aggregats (z. B. `ImagePairExistiertBereits`) ist **kein** Fall-Port: das Aggregat schreibt die Ablehnungs-Marke, sie kommt am ✕-Port an |
| je Element | gestrichelter Rahmen | **`p.Je(draht, x => x.Liste)`**: einmal je Element parallel; **`je.Sammle(…)`** wartet auf alle |

Der Editor **schlägt** ∧/∨ beim Zeichnen vor (Drähte aus sich ausschließenden Ports → ∨, aus Auffächern → ∧); im Code steht es immer
ausdrücklich. Warnung: „∧ wartet auf einen Weg, der nicht immer liefert“ (ein Fall des Vorgängers ist frei). Parallelität gibt es auf
zwei Ebenen: **im Vorgang** über Drähte, **zwischen Vorgängen** über die Cluster-Actors (ein Dirigent je Vorgang).

### 14.4 Entwurf zuerst: die Code-Form wird fürs Schreiben entworfen

**Jede Geste im Editor = genau ein Code-Fakt; der Schreiber erzeugt ihn, der Extractor liest ihn 1:1 zurück, ohne Ableitung.** Eine
Pipeline muss auf leerem Board entstehen können (Fixpunkt: schreiben → bauen → einlesen = dasselbe Board).

```csharp
public sealed class Bildeingang : IPipeline
{
    public PipelineFluss Fluss => PipelineFluss.Definiere(p =>
    {
        var datei    = p.Quelle<DateiErkannt>();
        var deuten   = datei.Rufe<IDateinameDeutung>(datei => new DeuteDateiname(datei.Dateiname, datei.Pfad));
        var paar     = p.Alle(deuten.Bei<ImagePairDateiGedeutet>(), datei).Sende<ErstelleImagePair>((deuten, datei) => new ErstelleImagePair(…));
        var vorschau = deuten.Bei<ImagePairDateiGedeutet>().Rufe<IBildVerkleinerung>(deuten => new VerkleinereBild(deuten.Pfad, 512))
            .Zeitlimit(TimeSpan.FromMinutes(5));
        var kontrast = vorschau.Bei<BildVerkleinert>().Rufe<IHistogrammAusgleich>(vorschau => new GleicheHistogrammAus(vorschau.Pfad));
        var melden   = p.Alle(kontrast.Bei<HistogrammAusgeglichen>(), paar.Bei<ImagePairErstellt>(), deuten.Bei<ImagePairDateiGedeutet>(), datei)
            .Sende<MeldeBildVerfuegbar>((kontrast, paar, deuten, datei) => new MeldeBildVerfuegbar(…))
            .Oder(p.Alle(kontrast.Bei<HistogrammAusgeglichen>(), paar.BeiAbgelehnt(), deuten.Bei<ImagePairDateiGedeutet>(), datei), (…) => …);
    });
}
```

**Vom Draht aus, nicht vom Bauer aus.** `p.Rufe<F>(draht, λ)` geht in C# nicht: gibt man den Funktionstyp ausdrücklich an, kann der
Compiler den Typ des Lambda-Parameters nicht mehr ableiten. Deshalb beginnt jeder Aufruf am Draht (`draht.Rufe<F>(λ)`); `p` setzt nur
die Quelle und bündelt Drähte (`p.Alle(…)`). Die Lambda-Parameter heißen wie die Knoten, von denen die Drähte kommen — so liest sich die
Zuordnung im Code wie die Tabelle im Editor („Pfad ← vorschau.Pfad“).

| Geste | Code-Fakt |
|---|---|
| Knoten | `var name = p.Quelle/Auf/Rufe/Sende/Je<…>(…)` — der **Variablenname ist die Knoten-Identität** im Editor; zur Laufzeit die Deklarations-Reihenfolge. Damit darf dieselbe Funktion beliebig oft im Fluss stehen. |
| Draht | `knoten.Bei<Fall>()` (bzw. die Quelle selbst), davon aus `.Rufe<F>(λ)` / `.Sende<C>(λ)` / `.Je(x => x.Liste)` |
| ∧ / ∨ | `p.Alle(…)` (bis 4 Drähte) / `.Oder(draht | p.Alle(…), λ)` |
| Fehler-Port | `.Zeitlimit(t)` am Knoten, Draht `knoten.BeiZeitlimit()` / `knoten.BeiAbgelehnt()` |
| Strom der Quelle | `quelle.Strom()` — zweiter Ausgang einer Quelle: `QuellStrom(Id, Version)` (bei `p.Auf<E>()` das Aggregat des Events). Ein Draht wie jeder andere (Typ + Herkunft), im Lambda `quelleStrom.Id` |
| Warten (Rennen) | `var ende = quelle.Strom().Warte<A, B>().Zeitlimit(t)` — das erste A/B im Strom NACH der Quelle; Ausgänge `ende.Bei<A>()`, `ende.BeiZeitlimit()`. Zeitlimit ist Pflicht; ein Event nach dem Limit zählt nicht |
| je-Rahmen | `var je = draht.Je(x => x.Liste)`; `je.Rufe<F>(e => …)`; `je.Sammle(draht).Sende<C>((q, liste) => …)` |
| Zuordnung | Lambda nur in Zuordnungsform (`x.Feld`, Konstante) — der Schreiber erzeugt nur diese, also bleibt sie als Tabelle bearbeitbar |
| Quelle-Einstellung | Umgebungs-Datei, Abschnitt `Quellen` (Zielbild §3.4) — nicht in der Pipeline |

**Ein Fluss ist per Konstruktion azyklisch** (man verdrahtet nur schon deklarierte Knoten). Invariante 3 bleibt gewahrt: die Knoten-
Identität ist ein Symbol innerhalb der Definition, Nachrichten werden weiter über Typen geroutet.

Entwerfbar ohne bestehenden Code: neue Funktion (nur Signatur; Rumpf = Stub, 🤖 oder Python), neue Quelle = Domänen-Typ auf eine
Treiber-Basis + Konfig, neues Command (Scaffolder). Eine Funktion ohne Implementierung beantwortet man per Hand (`Laufort: Hand`,
Zielbild §6).

### 14.5 Laufzeit: Dirigent, Vermittler, Ausführer

- **Dirigent = ein virtueller Actor je Vorgang** (der bestehende Prozess-Manager; ein Fluss wird in Prozess-Regeln übersetzt). Er rechnet
  nicht, er gibt alle bereiten Aufträge zugleich ab. Die Tokens tragen zusätzlich ihre **Herkunft** (Knoten) und ihre **Teile**
  (Element-Index je Je-Rahmen): ein Draht matcht nur Tokens seines Knotens, ein ∧ nur Tokens desselben Elements.
- **Quelle**: ein Treiber (`IQuelle<T>`, Zielbild §3) läuft im Host; jede Nachricht wird mit deterministischer Id (`Kennung`) als
  erstes Event eines Vorgangs-Streams angehängt (`StartStream` → genau einmal, auch wenn mehrere Knoten dieselbe Datei sehen) und weckt
  den Dirigenten.
  Das löst nebenbei „Commands ab Trigger nicht idempotent“ und „FileWatch markiert vor dem Ack“.
- **Vermittler = ein Actor je Funktion**, nicht maßgeblich (heilt sich aus den erneuten Übergaben des Dirigenten). **Ausführer holen
  sich Arbeit (Pull)**, sobald sie einen Slot frei haben: C# im Host, Python-Worker über gRPC, GPU-Rechner. Jeder geholte Auftrag hat
  eine Lease (verlängert per Lebenszeichen); läuft sie ab, wird er neu ausgegeben. Das Ergebnis wird genau einmal geschrieben
  (OCC auf dem Ausführungs-Stream). Der Auftrag selbst liegt im Log (Ausführungs-Stream, Version 1) — so können ihn entfernte
  Ausführer lesen, ohne dass der Vermittler Nutzdaten trägt.
- **Python**: eine Funktion ist ein Vertrag, der Laufort Konfig der Umgebung (Zielbild §4). Ein Worker meldet „ich biete F, n Slots“
  an und bekommt eine generierte Basis-Klasse (`rufe(auftrag, x) -> Ergebnis`). Pipeline und Board ändern sich nicht, wenn eine Funktion umzieht.
  Bewusst zu lösen: Daten als Verweis (gemeinsamer Speicher), Lebenszeichen bei langen Läufen, Ausführungs-Id für Außenwirkung.

### 14.6 Editor

- **Pipeline-Rahmen** als weiterer Rahmen-Typ (wie 📜 Vertrag / 🔌 Client). Innen Anordnung nach **Flusstiefe**, nicht nach Art;
  Aggregat-Rahmen bleiben unverändert, ein Command-Knoten springt per Klick zum Decider.
- **Katalog** (Quellen · Funktionen · Aggregate/Commands · Pipelines) mit Signatur; ⊕ am Port → passende Knoten/Einträge leuchten,
  „＋ neu …“ legt Unbekanntes an.
- Abzeichen ∧/∨ am Eingang, Fehler-Ports ⏳/✕, je-Rahmen gestrichelt, Zuordnung „Feld ← Draht.Feld“ im visuellen Zuordnungs-Editor.
  Der Laufort einer Funktion (C#/Python) ist Konfig der Umgebung (🖥 Host-Rahmen), nicht am Knoten.
- Entfällt für Flüsse: Handle-Karten, Trigger-Records, Selbst-Schleifen, Start-Handle, Regel-Karten.
- **Pipeline in der Domäne (2026-10-09):** ein Knoten = eine ZUGEWIESENE Funktion — sein Eingang und seine Ausgänge ergeben sich
  aus deren Interface, im Knoten wird nichts definiert: ƒ = Auftrag → Ergebnisse (`IFunktion`), ▶ = Command → Decide-Events, ⛲ → Quell-
  Nachricht, ◆/⧗ ← Event. Der Knoten wählt nur die Funktion, ordnet die Drähte dem Eingang zu und trägt ⏳. Drähte verbinden Knoten direkt
  (beschriftet mit dem Typ des Ausgangs). Die Verbindung zur Domäne ist die Zuweisung: ƒ ⇢ Funktions-Karte (Katalog, Signatur mit
  ◀ Auftrag ⊕ / Ergebnisse ⊕ ▶ — Auftrag und Ergebnisse als Details eingeklappt), ▶ → Command im Aggregat, ⛲ ⇢ Nachricht. Der Rahmen
  steht IN der Domäne, auf die der Fluss wirkt (Aggregate seiner Commands), unmittelbar über dem Akteur-Rahmen mit diesen Commands.
  Nachrichten-Panel nennt die Fluss-Knoten (▶ sendet, ⛲ meldet, Drähte/◆/⧗ hören); der Fokus läuft über die Zuweisungen.
- **Funktions-Karte = der Knoten, der das Interface erzeugt (2026-10-09):** Eingang und Ausgänge sind Record-Karten, per ⊕ angesteckt
  (◀ Auftrag, Ergebnisse ▶ — sie stehen sichtbar daneben: Auftrag | ƒ | Ergebnis, eingeklappt nur in einer Prozess-Kette). Dazu ein
  Rumpf „RufeAsync“ wie am Decider: der Rumpf aus der Datei, „🤖 füllen“ als Aktion (Slot `funktion|IX|Auftrag`). Aus dem Code gelesen:
  die C#-Klasse, die das Interface implementiert (Code-Fakt über das Interface). Neue Funktion: die Geste legt `public sealed class X : IX`
  an (immer `async`, throw-Platzhalter); „🤖 füllen“ legt sie bei unbekanntem Slot selbst an und füllt dann den Rumpf. Läuft sie extern
  (Python), ist das Konfig der Umgebung (Laufort).
- **Nachrichten aus anderen Bereichen (2026-10-09, gilt für JEDEN Nachrichten-Port ⊕):** im Verbinden-Modus erscheint neben den
  leuchtenden Karten ein Katalog „Nachricht aus anderen Bereichen“ — alle Records der passenden Sorte, die nicht als Karte auf dem Board
  stehen (Domäne nicht geladen, eingeklappt), grammatik-geprüft, durchsuchbar; anklicken = verbinden + Domäne dazuladen. Dazu „＋ neu“:
  eine neue Nachricht der Sorte als eigene Karte anlegen und sofort verbinden (an der Funktion: `{Stamm}Auftrag`/`{Stamm}Erledigt`).
  Ein gelöschtes Ergebnis/ein gelöschter Auftrag verschwindet auch aus der Funktion.

### 14.7 Abnahme

1. Beispiele (Bildeingang mit doppelter Funktion und ∧; Bestellung mit Verzweigung, ∨ und Zeitlimit-Port; Video mit je-Rahmen)
   laufen im Prüfstand gegen den echten Dirigenten.
2. Dieselben Flüsse entstehen auf leerem Board, werden geschrieben, gebaut und identisch wieder eingelesen (Sonde + Fixpunkt).
3. Danach erst Umzug des Bestands (Bildeingang zuerst).

### 14.8 Bewusst offen

„Der Erste gewinnt“ zwischen parallelen Wegen (heute nur über Zeitlimit-Rennen bzw. `Warte` gegen ein Zeitlimit); transientes
Veröffentlichen und „weiter an Pipeline“ als Knoten; flüchtige (nicht persistierte) Pipelines für Telemetrie; Unterfluss als Funktion.

### 14.9 Stand der Umsetzung (2026-10-09)

**Laufzeit**
- `Abstractions/Fluss/Pipeline.cs`: `IPipeline`, `PipelineFluss.Definiere`, `PipelineBauer` (`Quelle`/`Auf`/`Alle`), `Draht<T>`
  (`Rufe`/`Sende`/`Je`), `RufKnoten`/`SendeKnoten` (`Bei`/`BeiZeitlimit`/`BeiAbgelehnt`/`Zeitlimit`/`Oder`), `JeKnoten`/`SammelDraht`,
  `IQuelle<T>`/`IQuellNachricht`, **`QuellKnoten.Strom()` → `StromDraht` (`QuellStrom(Id, Version)`) und `StromDraht.Warte<…>()` →
  `WarteKnoten`** (Regel mit `WartetAuf`, Formfehler „Warten ohne Zeitlimit“ beim Definieren). Übersetzt in Prozess-Regeln mit
  Knoten-Herkunft (`Regel.Knoten/VonKnoten/JeKnoten/WartetAuf`, erweiterte `SammelBedingung`, `ProzessRegeln.QuellKnoten/Umleiten*`).
- **Der reine Kern des Dirigenten** liegt in `Abstractions/Prozess/FlussBelegung.cs` (`FlussBelegung.Belegungen/Matches/TeileDesAufrufs`,
  `JeTeile`, `IFlussToken`) — der Prozess-Manager faltet seine Tokens aus dem Log, das `FlussLaufwerk` (`Cqrs.Testing`, Prüfstand) hält sie im Speicher; beide entscheiden
  mit DEMSELBEN Kern, was feuert.
- Dirigent (`Infrastructure/Prozess/ProzessManager.cs`): Tokens tragen Herkunft und Je-Teile, dazu der `QuellStrom`-Token der Quelle; ein
  **Warte-Knoten** liest den Strom der Quelle bei jeder Weckung nach (kein Cursor; geweckt vom §3-Backstop, ≤ 15 s), ein Event nach dem
  Zeitlimit zählt nicht. Fehler an verdrahteten Ports werden `SchrittUmgeleitet` und dann `ZeitlimitAbgelaufen`/`SchrittAbgelehnt`-Tokens.
- Registry je Assembly: `ProzessRegelnGenerator` emittiert `{Assembly}.Prozess.GeneratedProzessRegeln`; `GeneratedProzesse.AlleRegeln()`
  vereinigt Domain und Domain.Pipeline (Flüsse, deren Funktionen Fähigkeiten der Leseseite nehmen).
- **Katalog-Funktionen mit Lese-Fähigkeit**: `RufeAsync(Auftrag, IAusfuehrung, ISucheX suche, …)` — nur `IReadStore`-Interfaces (sonst
  CQRS068); der generierte Dispatch öffnet je Aufruf einen Fähigkeits-Bereich (`IFaehigkeitsFabrik`), `GeneratedFunktionen.Faehigkeiten`;
  Boot-Guard: eine solche Funktion nie `AddExterneFunktion` (läuft nur im Host); keine Python-Basis.
- Quellen (`Infrastructure/Quellen/Quellen.cs`): `QuellEingang`, `QuellenDienst`, `AddQuelle<T, Q>()` (entfällt mit dem
  Datei-Treiber, Zielbild §3.4), der Webhook einer Quelle als Konfig `Quellen:<Typ>:Webhook` (seit Werkbank P7.5.2; `MapQuellWebhooks` in `LaufeAsync`, der
  Editor liest die Route aus den Umgebungs-Dateien).
- Vermittlung (`FunktionsVermittlung.cs`, `FunktionsVermittlerActor.cs`, `FunktionsAbholer`, `AddExterneFunktion<F>()`). **Akteur-Tor für
  angebotene Funktionen**: `IDarf<IFunktion-Typ>` = „der Akteur darf diese Funktion rechnen“ (CQRS058 lässt es zu, `AkteurRechte.Funktionen`
  generiert); mit angemeldetem Akteur nimmt `FunktionsAnbieterSitzung.Biete` nur befugte Funktionen an (Fehler `FUNKTION_NICHT_BEFUGT`),
  ohne Tor/Akteur offen wie jede andere Nachricht.
- Python: `FunktionsBasis`, Angebot am Handshake, Slots + Lebenszeichen. **Reconnect behoben**: früher endeten Lese- und
  Verarbeitungsschleife mit der ersten Verbindung (der Monitor verband neu und bot die Funktionen wieder an, aber niemand las mehr);
  jetzt je Verbindung eine Sitzung, danach Reconnect und neue Sitzung (`test_client_reconnect.py`).

**Bestand umgezogen — keine Domänen-Pipeline nutzt mehr `IPipelineHandler`**
- `Bildeingang` (Domain/ImagePair, 2026-10-07).
- Datensatz-Resolver → **`DatensatzRangeAufloesung`** (`p.Auf<RangeAngefordert>()` → ƒ `IRangeSuche` mit `ISearchImagePairs` →
  `NimmRangeAuf(angefordertStrom.Id, …)`) und **`DatensatzEinfrieren`** (ƒ `IMitgliederEinfrieren` mit `IFindImagePair` →
  `SchliesseEinfrierenAb`), Domain.Pipeline/Datensatz; Implementierungen `RangeSuche`/`MitgliederEinfrieren` (gebunden in Host.Grpc).
- Trainings-Frist → **`TrainingWaechter`** (Domain/Trainingslauf): `begonnen.Strom().Warte<TrainingAbgeschlossen, TrainingGescheitert,
  TrainingAbgebrochen>().Zeitlimit(6 h)`, am ⏳-Port `MarkiereAlsHaengengeblieben` — ein Rennen statt `Frist` + `FristStorno`
  (`TrainingFristConfig` entfällt; das Limit ist ein Code-Fakt).
- Benchmark → **`Benchmark`**: `BenchPing` ist Quell-Nachricht, der Fluss nur die Quelle (gemessen: Quelle → Log → Dirigent → beendet).
  Der `LoadHarness` aus docs/12 existiert im Repo nicht mehr.

**Editor**
- Modell `EditorModell.Fluesse` (+ `FlussArt.Warte`, `FlussSchritt.WarteAuf`, `FlussPort.Strom`), `Funktion.Faehigkeiten`; Extractor
  (`FlussLeser`: `.Strom()`, `.Warte<…>()`; Funktions-Signatur mit Fähigkeiten), Scaffolder (Lambda-Parameter `quelleStrom`, `knotenZeit`),
  Grammatik (GR-FLUSS-WARTE, GR-FUNKTION-FAEHIGKEIT, Fähigkeit → Funktion), Prüfung (heute `Validator`, wird Analyzer, Zielbild
  §8.3), Board (⧗-Knoten, „⇢ Strom“-Port mit „⧗ warte auf ein Event im Strom …“, Zuordnung `Id/Version` des Stroms; Funktions-Karte
  „⚙ liest“), Sonde/soll.txt (Code-Fluss + gezeichneter Fluss mit Warten/Strom, Funktion mit Fähigkeit).
- **LLM-Slots**: `SlotInventar`/`KontextBauer` kennen `funktion` (Implementierung von `RufeAsync`) und `fluss` (jeder Bau-Lambda, Anker =
  Anweisung + Lambda-Index). Die LLM-Konsole füllt Fluss-Lambdas mit eigener Anweisung (genau ein Lambda, rein, Zuordnungsform), prüft
  Stelligkeit + In-Memory-Compile und schreibt den Lambda zurück (`CodeSync.LambdaAnker`, Rückgängig inkl.). Gemessen (`--slots`): alle 11
  Fluss-Lambdas liegen zu 100 % im eigenen Spielraum (ihre Parameter) — die Zuordnung braucht selten ein LLM, die Funktions-Rümpfe schon
  (23 % Wissen von außen).

**Gemessen (2026-10-09):** Prüfstand 381/381; `--sonde` (92 Soll-Fakten) grün; Python SDK 28/28, Worker 15/15; Integration
gegen Marten/Consul/Redis (sequentiell) 33/33, darin `PipelineFlussE2ETests` (4): Bildeingang Quelle → Dirigent → Vermittler → Abholer → Fakt,
derselbe mit **Python-Worker über gRPC**, Trainings-Wächter (Warten + Backstop), Datensatz-Range mit echter Lese-Fähigkeit.

**Offen:** Unterfluss als Funktion; „der Erste gewinnt“ ohne Zeitlimit; flüchtige Flüsse (Telemetrie); Takt-Treiber, Client meldet
Quell-Nachrichten (Zielbild §12.2); ein Warte-Knoten wird nur vom Backstop geweckt (kein Signal-Routing für fremd-korrelierte Events);
Rückbau des Handle-Modells (§14.10).

### 14.10 Entscheidung: das Handle-Modell (`IPipelineHandler`) entfällt als Domänen-Baustein

Nach dem Umzug hat keine Domänen-Pipeline mehr einen Handle. Alles, was die Handles taten, ist jetzt ein Fluss-Knoten oder eine Quelle:
Verarbeitung = Katalog-Funktion (auch mit Lese-Fähigkeit), Frist/Storno = `Warte` gegen ein Zeitlimit, Datei = Treiber (Zielbild §3) /
Webhook = `Quellen:<Typ>:Webhook` (Konfig), Event→Command = `p.Auf<E>()` → `Sende`. Eine „Quell-Implementierung“ als Handle braucht es nicht: eine Quelle
ist ein Treiber (kein Log, kein Fluss, nur Nachrichten liefern) — der Rest ist Fluss.

**Daher:** neue Pipelines werden als Fluss gebaut („+ ⛓ Pipeline (Fluss)“); der Handle-Baustein („+ Pipeline“) entfällt im Editor
sofort (Zielbild P0). Nur der **Rückbau** der Laufzeit-Maschinerie (PipelineActor-/PipelineDispatch-Generator, `PipelineActorBase`,
Handle-Trigger über gRPC, Timer-/Webhook-Trigger, Fristplan für Handles, Analyzer-Zweige) bleibt eine eigene Aufgabe, sobald es
**Takt-Treiber**, **Client meldet Quell-Nachrichten** und **flüchtige Quellen** (Inv. 6: Verlierbares bleibt auf dem schnellen Kanal)
gibt (Zielbild §10, §12.2) — bis dahin bleibt das Handle-Modell zur Laufzeit technisch erhalten.

### Zuordnungs-Editor (visuell, 2026-10-11)

Der Eingang eines Aufrufs (Fluss-Knoten ƒ/▶ je Eingang, Prozess-Regel „Dann") wird nicht als Text oder Tabelle gebaut, sondern in einem
eigenen Knoten-Editor (Panel: „🔀 Zuordnung im Knoten-Editor“, Kurzfassung „Feld ← Quelle“ antippen):

- **Links die Eingänge** — je Lambda-Parameter eine Karte (Name wie der Scaffolder ihn vergibt: Vorgänger-Knoten bzw. `t, r, g`) mit
  ihren Feldern als Ausgängen; Wert-Objekt-Felder sind eine Ebene aufgeklappt.
- **Mitte Bau-Knoten** — `new V(…)` für ein Wert-Objekt (seine Felder sind wieder Eingänge, beliebig tief) und `ƒx` für einen freien
  C#-Ausdruck (mit Vorschlägen der Quell-Ausdrücke).
- **Rechts das Ziel** — Auftrag bzw. Command mit seinen Feldern als Eingängen; „＋ new V“ an einem Wert-Objekt-Feld legt den Bau-Knoten
  an, „ƒx“ einen Ausdruck, ✕ (oder Klick auf den Draht) löst.
- **Drähte** ziehen (oder Quelle anklicken, dann Feld anklicken); nur typgleiche Ziele leuchten grün, falsche Typen werden abgelehnt.
  „↻ automatisch“ schlägt alles neu vor; unten steht der entstehende Lambda.
- **Speicherung:** die Geste schreibt den Lambda `p => new Ziel(…)` sofort in den Code (Zielbild §8.2).
  Ein Lambda aus dem Code wird beim Öffnen zerlegt (Parameter-Namen angeglichen, `new V(…)` → Bau-Knoten, Unbekanntes → `ƒx`); ist er
  kein einfacher Konstruktor, zeigt der Editor ihn und ersetzt ihn erst bei der ersten Änderung.

