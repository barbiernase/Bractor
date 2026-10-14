# Pipeline als Fluss

> Detail-Referenz zu `docs/konzept-werkbank.md`; bei Widerspruch gilt das Zielbild. Stand 2026-10-13, gegen den Code geprüft.
>
> Inhalt: §12 Nachrichten mit zwei Seiten, §13.6 Ablauf als Kette (Prozess), §14 Pipeline als Fluss (DSL, Semantik, Laufzeit, Editor) und
> der Zuordnungs-Editor. §1–§11 und §13.1–§13.5 (das frühere Handle-Modell) gibt es nicht mehr; die Nummern bleiben, weil Code-Kommentare
> auf §12–§14 verweisen. Die Sprache dahinter (Alphabet, Grammatik, Operatoren, Kapselung) steht in `docs/konzept-editor-komposition.md`.
> Code-Beispiele kommen aus der Agnostik-Sonde (`GraphExtractor.Kern/Sonde/Fluss.cs.txt`, Domäne „Leihwesen“); wo Code der Beispieldomäne
> genannt wird, ist er als solcher gekennzeichnet. Offenes: `docs/13`; Zahlen: `docs/12`.

## 12 · Nachrichten mit zwei Seiten: „◀ kommt aus" / „geht an ▶"

**Leitidee.** Jede Nachricht ist eine Karte mit **zwei Seiten**: ◀ *kommt aus* (Erzeuger) und *geht an* ▶ (Konsumenten). Das ist
dieselbe Kante wie der Ausgang am Erzeuger — **eine** Wahrheit (die OneOf-Signatur des Erzeugers), zwei Enden. „kommt aus" ist
keine zweite Liste am Command, sondern die umgekehrte Sicht; Verbinden von der Nachricht aus schreibt in den OneOf des Handles.

| Nachricht | ◀ kommt aus | geht an ▶ |
|---|---|---|
| Command | beliebig: Akteur (abgeleitet), Fluss, Prozess, Reaktion | genau ein Aggregat (Decider) |
| Event | genau ein Aggregat (Decider) | beliebig: Applier, Fluss, Prozess, Projektion, Reaktion |
| Transient | Decider (Ablehnung), Projektion/Reaktion (veröffentlicht) | beliebig: Projektion, Reaktion |
| Query / Response | Außenwelt / Reader | Reader / Außenwelt |

Die Kardinalität kommt aus der Grammatik (`rahmen.grammatik.konsume`, eine Quelle `DomainEditor/Grammatik.cs`); die Partner je Nachricht
rechnet der Server (`board.sicht.partner`, konzept-werkbank §8.3).

1. **Nachrichtenkarten** (Command/Event/Ablehnung/Query/Response) zeigen „◀ kommt aus" und „geht an ▶" als Listen der
   Partner (anklickbar = springen; ✕ = lösen, als Beschreibung vom Server) plus je Seite einen ⊕-Port.
2. **Grenz-Partner:** ein Partner aus einer nicht geladenen Domäne erscheint als „↗ außerhalb · Namespace"; Klick lädt die Domäne
   dazu. Die Kurzzeile zeigt „↗ n außerhalb".

## 13 · Prozess im Editor

§13.1–§13.5 gibt es nicht mehr (Handle-Modell).

### 13.6 Ablauf als Kette: jede Funktion ein Knoten

Vorgabe: „erst die, dann die“ — eine Funktion ist ein Knoten, die Reihenfolge eine Kante. Darunter liegen unverändert die Prozess-Regeln
(`docs/04` §4.5, `docs/10` §10.5); die Kette ist Darstellung und Bearbeitung.

- **Regeln als Kanten** (`board.sicht.kette`, vom Server): jede Prozess-Regel mit Ziel wird als Kante gezeichnet, keine eigene Karte —
  Prozess (Start) bzw. die Funktion, die das auslösende Ergebnis liefert → nächster Schritt; Beschriftung „erst“/„dann“. Verzweigung = eine
  Kante je Ergebnis („bei BildNichtLesbar“), Zusammenführung = mehrere Drähte in einen Knoten („und“; „alle“), Fan-out „×N“, Kompensation
  gestrichelt rot „↩ rückgängig“, Zeitlimit „⏳“. Klick öffnet die Regel (Argumente, Zeitlimit).
- **Funktion wie ein Decider:** Auftrag und Ergebnis-Events einer Funktion sind immer eigene Karten — Spalten **Auftrag → ƒ Funktion (Rumpf
  aus der Datei, „🤖 füllen“) → Ergebnis** auf einer Zeile, im selben Block und Akteur-Rahmen (Akteure der Funktion = die ihres Auftrags).
  Fluss-Knoten und Prozess-Regeln verweisen nur auf die ƒ-Karte.
- **Anordnung:** je Kettentiefe eine eigene Funktions-Spalte, Zeile = die des Vorgängers → Prozess → ƒ → ƒ auf einer Linie.
- **Bearbeiten:** am Prozess „bei ‹Auslöser› ▶ erst ⊕“, an jeder Funktion je Ergebnis „bei ‹Ergebnis› ▶ dann ⊕“ — eine Funktion oder
  einen Command auf dem Graphen anklicken hängt den nächsten Schritt an, nochmal anklicken (✓) bzw. ✕ löst ihn. Ein Command als Schritt
  bekommt den Start als Kontext in den Join (er braucht dessen Daten).
- **Argumente:** beim Anhängen ordnet der Editor die Argumente vorläufig zu (`autoArgs` in `editor.js`: gleicher Name → Name endet gleich →
  einziger gleicher Typ; Wert-Objekte aus ihren Feldern gebaut; jüngstes Ergebnis vor dem Start) und schreibt sie mit der Geste. Das ist
  eine **Namensheuristik im Browser** und nur ein Vorschlag, den der Nutzer prüfen muss; dass sie im JS liegt, ist Schuld `docs/13` W3-1.
  Geändert wird im **Zuordnungs-Editor** (unten); Konstanten ohne Quelle bleiben `default`. Der Scaffolder schreibt Argumentlisten für
  Commands (`Sende`) und Aufträge (`Rufe`).

## 14 · Pipeline als Fluss

### 14.1 Herkunft

Früher lief der Weg einer Datei über eine Handle-Pipeline, einen Trigger und einen Umweg durchs Aggregat, und Pipeline-Handle,
Prozess-Regel und Reaktion waren drei Bausteine für eine Idee. Der Fluss ersetzt das: Quelle → Funktionen → Commands auf einer Fläche
(Historie im Git-Verlauf).

### 14.2 Das Modell: eine Fläche, eine Knotenform, ein Katalog

**Eine Pipeline ist eine Fläche, auf der Knoten aus dem Katalog mit Drähten verbunden werden.** Sie beginnt an einer **Quelle**; in die
Aggregat-Welt geht es nur über einen Knoten, der ein Command sendet.

- **Jeder Knoten hat dieselbe Form: ein Eingang, OneOf-Ausgänge (ein Port je Fall).**
  - **Quelle** — kein Eingang, ein Ausgang. Quellen sind fest codierte Treiber + Konfig (konzept-werkbank §3), oder ein **Event aus dem Log**
    (`p.Auf<E>()`), womit jede bisherige Event-Pipeline/Saga ein Sonderfall ist.
  - **Funktion** — Auftrag hinein, Ergebnis-Fälle heraus (`IFunktion`).
  - **Aggregat-Knoten** — Command hinein; Ausgänge sind die Events aus der Decide-Signatur. Der Fluss kann nach der Antwort
    weiterlaufen. *Aggregat und Funktion sind derselbe Knotentyp; nur das Aggregat hat Gedächtnis.*
- **Ein Draht ist ein Typ** (Ergebnis-Fall eines bestimmten Knotens). **Zuordnung statt `msg`:** die Felder eines Eingangs werden aus
  den Nachrichten am Draht gebaut; was ein Knoten aus einem früheren Schritt braucht, holt er als weiteren Draht (∧ mit einem
  Vorgänger — im Editor als „Kontext“ angezeigt). Die Funktionen bleiben domänenfrei.
- **Eine Quell-Nachricht = ein Vorgang.** Zwischen Vorgängen gibt es keine Joins — wer zwei Vorgänge zusammenbringen muss, braucht
  Gedächtnis, und das gehört ins Aggregat. Dessen Event ist die Quelle der nächsten Pipeline.

### 14.3 Fluss-Semantik: parallel, verzweigen, warten

| Form | Darstellung | Bedeutung |
|---|---|---|
| Ein Port, mehrere Drähte | Auffächern | **alle** bekommen die Nachricht — parallel |
| Mehrere Ports eines Knotens | OneOf-Fälle | **genau einer** feuert — Verzweigung (eine Weiche ist eine Funktion) |
| Eingang mit mehreren Drähten | Abzeichen | **∧ `Alle(…)`** warten, oder **∨ `.Oder(…)`** jeder Weg einzeln |
| Fehler-Ports | ⏳ / ✕ | **`BeiZeitlimit()`**, **`BeiAbgelehnt()`** — verdrahtet = Weg, frei = Vorgang scheitert. Eine Ablehnung des Aggregats ist **kein** Fall-Port: das Aggregat schreibt die Ablehnungs-Marke, sie kommt am ✕-Port an |
| je Element | gestrichelter Rahmen | **`draht.Je(x => x.Liste)`**: einmal je Element parallel; **`je.Sammle(…)`** wartet auf alle |

Der Editor **schlägt** ∧/∨ beim Zeichnen vor (Drähte aus sich ausschließenden Ports → ∨, aus Auffächern → ∧); im Code steht es immer
ausdrücklich. Warnung im Build: „∧ wartet auf einen Weg, der nicht immer liefert“ (ein Fall des Vorgängers ist frei; `FlussAnalyzer`,
GR-FLUSS-WARTET). Parallelität gibt es auf
zwei Ebenen: **im Vorgang** über Drähte, **zwischen Vorgängen** über die Cluster-Actors (ein Dirigent je Vorgang).

### 14.4 Entwurf zuerst: die Code-Form wird fürs Schreiben entworfen

**Jede Geste im Editor = genau ein Code-Fakt; der Schreiber erzeugt ihn, der Extractor liest ihn 1:1 zurück, ohne Ableitung.** Eine
Pipeline muss auf leerem Board entstehen können (Fixpunkt: schreiben → bauen → einlesen = dasselbe Board).

```csharp
// Agnostik-Sonde (GraphExtractor.Kern/Sonde/Fluss.cs.txt), gekürzt
public sealed class RueckgabePruefung : IPipeline
{
    public PipelineFluss Fluss => PipelineFluss.Definiere(scan =>
    {
        var eingang = scan.Quelle<RueckgabeGescannt>();
        var seiten = eingang.Je(e => e.Seiten);
        var seite = seiten.Rufe<ISeitenPruefung>(s => new PruefeSeite(s));
        var schnell = eingang.Rufe<ISchadensSchaetzung>(e => new SchaetzeSchaden(e.LeserId, 10m))
            .Zeitlimit(TimeSpan.FromSeconds(30));
        var gruendlich = eingang.Rufe<ISchadensSchaetzung>(e => new SchaetzeSchaden(e.LeserId, 100m));
        var mahnung = scan.Alle(gruendlich.Bei<SchadenGeschaetzt>(), eingang).Sende<EroeffneMahnung>((g, e) => new EroeffneMahnung(e.LeserId, g.Betrag))
            .Oder(schnell.BeiZeitlimit(), z => new EroeffneMahnung(Guid.Empty, 0m));
        seiten.Sammle(seite.Bei<SeiteGeprueft>()).Sende<MeldeSchaden>((e, geprueft) => new MeldeSchaden(e.LeserId, e.LeserId));
        mahnung.BeiAbgelehnt().Sende<MeldeSchaden>(a => new MeldeSchaden(Guid.Empty, Guid.Empty));
    });
}

public sealed class Ausleihfrist : IPipeline
{
    public PipelineFluss Fluss => PipelineFluss.Definiere(p =>
    {
        var ausgeliehen = p.Auf<Ausgeliehen>();
        var rueckgabe = ausgeliehen.Strom().Warte<Zurueckgegeben>().Zeitlimit(TimeSpan.FromDays(28));
        var offen = ausgeliehen.Rufe<IOffenZaehlung>(ausgeliehen => new ZaehleOffene(ausgeliehen.LeserId));
        var mahnung = p.Alle(rueckgabe.BeiZeitlimit(), offen.Bei<OffeneGezaehlt>(), ausgeliehen.Strom())
            .Sende<EroeffneMahnung>((rueckgabeZeit, offen, ausgeliehenStrom) => new EroeffneMahnung(ausgeliehenStrom.Id, offen.Anzahl));
    });
}
```

**Vom Draht aus, nicht vom Bauer aus.** `p.Rufe<F>(draht, λ)` geht in C# nicht: gibt man den Funktionstyp ausdrücklich an, kann der
Compiler den Typ des Lambda-Parameters nicht mehr ableiten. Deshalb beginnt jeder Aufruf am Draht (`draht.Rufe<F>(λ)`); `p` setzt nur
die Quelle und bündelt Drähte (`p.Alle(…)`). Der Schreiber benennt die Lambda-Parameter wie die Knoten, von denen die Drähte kommen — so
liest sich die Zuordnung im Code wie im Zuordnungs-Editor; frei benannte Parameter (wie `e`, `g` oben) liest der Extractor ebenso.

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
| Quelle-Einstellung | Umgebungs-Datei, Abschnitt `Quellen` (konzept-werkbank §3.4) — nicht in der Pipeline |

**Ein Fluss ist per Konstruktion azyklisch** (man verdrahtet nur schon deklarierte Knoten). Invariante 3 bleibt gewahrt: die Knoten-
Identität ist ein Symbol innerhalb der Definition, Nachrichten werden weiter über Typen geroutet.

Entwerfbar ohne bestehenden Code: neue Funktion (nur Signatur; Rumpf = Stub, 🤖 oder Python), neue Quelle = Domänen-Typ auf eine
Treiber-Basis + Konfig, neues Command (Scaffolder). Eine Funktion ohne Implementierung beantwortet man per Hand (`Laufort: Hand`,
konzept-werkbank §6).

### 14.5 Laufzeit: Dirigent, Vermittler, Ausführer

- **Dirigent = ein virtueller Actor je Vorgang** (der bestehende Prozess-Manager; ein Fluss wird in Prozess-Regeln übersetzt). Er rechnet
  nicht, er gibt alle bereiten Aufträge zugleich ab. Die Tokens tragen zusätzlich ihre **Herkunft** (Knoten) und ihre **Teile**
  (Element-Index je Je-Rahmen): ein Draht matcht nur Tokens seines Knotens, ein ∧ nur Tokens desselben Elements.
- **Quelle**: ein fest codierter Treiber läuft im Host (heute der Datei-Treiber, `Infrastructure/Quellen/Treiber/Datei/`; Werte unter
  `Quellen:<Typ>` der Umgebung, dazu optional `Quellen:<Typ>:Webhook`; konzept-werkbank §3). Jede Nachricht wird mit deterministischer
  Stream-Id aus Typ + `Kennung` als erstes Event eines Vorgangs-Streams angehängt (`StartStream` → genau einmal, auch wenn mehrere Knoten
  dieselbe Datei sehen; `Infrastructure/Quellen/Quellen.cs`) und weckt den Dirigenten.
- **Vermittler = ein Actor je Funktion** (`FunktionsVermittlerActor`), hält die offenen Aufträge samt Inhalt im Speicher und ist nicht
  maßgeblich: der Dirigent bietet jeden offenen Auftrag bei jeder Weckung erneut an (§3-Backstop), daraus baut sich der Vermittler wieder
  auf. **Ausführer holen sich Arbeit (Pull)**, sobald sie einen Slot frei haben: C# im Host (`FunktionsAbholer`), Python-Worker über gRPC
  (`FunktionsAnbieterSitzung`). Jeder geholte Auftrag hat eine Lease (verlängert per Lebenszeichen); läuft sie ab, wird er neu ausgegeben.
  Das Ergebnis wird genau einmal wirksam: `FunktionsAusfuehrer` schreibt es per `StartStream` in den Ausführungs-Stream (Stream-Id =
  Vorgang; rechnen zwei denselben Auftrag, gewinnt genau ein Ergebnis) und weckt den Dirigenten.
- **Bindung und Laufort** stehen in der Umgebung unter `Funktionen:<F>` (`Laufort` Host/Extern/Hand, `Implementierung`, `Slots`,
  `Wiederholungen`, `Worker`; `Abstractions/Umgebung/UmgebungKonfig.cs`); `AddCqrs` bindet daraus (`BindeFunktionen`). Pipeline und Board
  ändern sich nicht, wenn eine Funktion umzieht.
- **Python**: ein Worker meldet „ich biete F, n Slots“ an und implementiert eine generierte Basis-Klasse (`rufe(auftrag, x) -> Ergebnis`,
  `generated/funktionen.py`; der generische `funktions_worker.py` lädt die Implementierung, konzept-werkbank §12.3 E13). Offen: Pfade für
  entfernte Ausführer (konzept-werkbank §12.2).

### 14.6 Editor

- **Pipeline-Rahmen** als weiterer Rahmen-Typ (wie 📜 Vertrag / 🔌 Client). Innen Anordnung nach **Flusstiefe**, nicht nach Art;
  Aggregat-Rahmen bleiben unverändert, ein Command-Knoten springt per Klick zum Decider.
- **Verbinden** über ⊕ am Port → passende Knoten/Einträge leuchten (`POST /api/werkbank/passt`), „＋ neu …“ legt Unbekanntes an.
- Abzeichen ∧/∨ am Eingang, Fehler-Ports ⏳/✕, je-Rahmen gestrichelt, Zuordnung „Feld ← Draht.Feld“ im visuellen Zuordnungs-Editor.
  Der Laufort einer Funktion (C#/Python) ist Konfig der Umgebung (🖥 Host-Rahmen), nicht am Knoten.
- Für Flüsse gibt es keine Handle-Karten, Trigger-Records, Selbst-Schleifen, Start-Handle oder Regel-Karten.
- **Fluss-Knoten-Karten** (`fs:<Knoten>`, Besitzer = der Fluss): ein Knoten ist eine ZUGEWIESENE Funktion bzw. ein zugewiesener Typ —
  seine Ausgänge ergeben sich aus der Signatur, im Knoten wird nichts definiert: ƒ = Auftrag → Ergebnisse (`IFunktion`), ▶ = Command →
  Decide-Events (ohne Ablehnungen; die kommen am ✕-Port an), ⛲ → Quell-Nachricht (+ „⇢ Strom“), ◆ ← Event, ⧗ ← erwartete Events. Die Ports je
  Knoten (mit Nachricht), Vorgänger, Tiefe, gehörte Events und Zuweisung liefert der Server (`board.sicht.fluesse`). Der Knoten wählt nur die
  Funktion, ordnet die Drähte dem Eingang zu und trägt ⏳. Drähte verbinden Knoten direkt (beschriftet mit dem Typ des Ausgangs). Die
  Verbindung zur Domäne ist die Zuweisung: ƒ ⇢ Funktions-Karte, ▶ → Command im Aggregat, ⛲ ⇢ Nachricht. Der Rahmen steht IN der Domäne, auf
  die der Fluss wirkt (Aggregate seiner Commands), unmittelbar über dem Akteur-Rahmen mit diesen Commands. Das Nachrichten-Panel nennt die
  Fluss-Knoten (▶ sendet, ⛲ meldet, Drähte/◆/⧗ hören); der Fokus läuft über die Zuweisungen.
- **Funktions-Karte = der Knoten, der das Interface erzeugt:** Auftrag und Ergebnisse sind eigene Record-Karten, per ⊕ angesteckt
  (◀ Auftrag, Ergebnisse ▶), sichtbar daneben: Auftrag | ƒ | Ergebnis (wie §13.6). Dazu ein
  Rumpf „RufeAsync“ wie am Decider: der Rumpf aus der Datei, „🤖 füllen“ als Aktion (Slot `funktion|IX|Auftrag`). Aus dem Code gelesen:
  die C#-Klasse, die das Interface implementiert (Code-Fakt über das Interface). Neue Funktion: die Geste legt `public sealed class X : IX`
  an (immer `async`, throw-Platzhalter); „🤖 füllen“ legt sie bei unbekanntem Slot selbst an und füllt dann den Rumpf. Läuft sie extern
  (Python), ist das Konfig der Umgebung (Laufort).
- **Nachrichten aus anderen Bereichen (gilt für JEDEN Nachrichten-Port ⊕):** im Verbinden-Modus erscheint neben den
  leuchtenden Karten ein Katalog „Nachricht aus anderen Bereichen“ — alle Records der passenden Sorte, die nicht als Karte auf dem Board
  stehen (Domäne nicht geladen, eingeklappt), grammatik-geprüft, durchsuchbar; anklicken = verbinden + Domäne dazuladen. Dazu „＋ neu“:
  eine neue Nachricht der Sorte als eigene Karte anlegen und sofort verbinden (an der Funktion: `{Stamm}Auftrag`/`{Stamm}Erledigt`).
  Ein gelöschtes Ergebnis/ein gelöschter Auftrag verschwindet auch aus der Funktion.

### 14.7 Abnahme

Flüsse entstehen auf leerem Board, werden geschrieben, gebaut und identisch wieder eingelesen: die Agnostik-Sonde enthält einen Code-Fluss
und einen gezeichneten Fluss (`--sonde`, `GraphExtractor.Kern/Sonde/`); die Prüfstand-Tests (`FlussLaufwerkTests`, `UmgezogeneFluesseTests`)
prüfen gegen denselben Belegungs-Kern wie der Dirigent, `PipelineFlussE2ETests` (Integration) gegen den echten Dirigenten, inklusive
Python-Worker über gRPC.
Zahlen: `docs/12`.

### 14.8 Bewusst offen

„Der Erste gewinnt“ zwischen parallelen Wegen (heute nur über Zeitlimit-Rennen bzw. `Warte` gegen ein Zeitlimit); transientes
Veröffentlichen und „weiter an Pipeline“ als Knoten; flüchtige (nicht persistierte) Flüsse für Telemetrie; Unterfluss als Funktion;
Takt-Treiber und „Client meldet Quell-Nachricht“ (konzept-werkbank §12.2); ein Warte-Knoten wird nur vom Backstop geweckt (`docs/13` W11-2).

### 14.9 Wo es im Code steht

**Laufzeit**
- `Abstractions/Fluss/Pipeline.cs`: `IPipeline`, `PipelineFluss.Definiere`, `PipelineBauer` (`Quelle`/`Auf`/`Alle`), `Draht<T>`
  (`Rufe`/`Sende`/`Je`), `RufKnoten`/`SendeKnoten` (`Bei`/`BeiZeitlimit`/`BeiAbgelehnt`/`Zeitlimit`/`Oder`), `JeKnoten`/`SammelDraht`,
  `QuellKnoten.Strom()` → `StromDraht` (`QuellStrom(Id, Version)`) und `StromDraht.Warte<…>()` → `WarteKnoten` (Regel mit `WartetAuf`,
  Formfehler „Warten ohne Zeitlimit“ beim Definieren). Die Quell-Nachricht ist ein Typ auf `IQuellNachricht` (bzw. eine Treiber-Basis wie
  `DateiGemeldet`). Übersetzt in Prozess-Regeln mit Knoten-Herkunft (`Regel.Knoten/VonKnoten/JeKnoten/WartetAuf`, `SammelBedingung`,
  `ProzessRegeln.QuellKnoten/Umleiten*`).
- **Der reine Kern des Dirigenten** liegt in `Abstractions/Prozess/FlussBelegung.cs` (`FlussBelegung.Belegungen/Matches/TeileDesAufrufs`,
  `JeTeile`, `IFlussToken`) — der Prozess-Manager faltet seine Tokens aus dem Log, das `FlussLaufwerk` (`Cqrs.Testing`, Prüfstand) hält sie
  im Speicher; beide entscheiden mit DEMSELBEN Kern, was feuert.
- Dirigent (`Infrastructure/Prozess/ProzessManager.cs`): Tokens tragen Herkunft und Je-Teile, dazu der `QuellStrom`-Token der Quelle; ein
  **Warte-Knoten** liest den Strom der Quelle bei jeder Weckung nach (kein Cursor; geweckt vom §3-Backstop), ein Event nach dem Zeitlimit
  zählt nicht. Fehler an verdrahteten Ports werden `SchrittUmgeleitet` und dann `ZeitlimitAbgelaufen`/`SchrittAbgelehnt`-Tokens.
- Registry je Assembly: `ProzessRegelnGenerator` emittiert `{Assembly}.Prozess.GeneratedProzessRegeln`; der `ProzessVerbundGenerator` vereinigt
  in der Komposition die Registries aller referenzierten Rollen-Projekte zu `GeneratedProzessVerbund.Alle` (CQRS012 über Assemblies, CQRS099
  Prozesse ohne Registry); das Framework liest ihn über `Anwendung.Prozesse` (`GeneratedProzesse.AlleRegeln()`).
- **Katalog-Funktionen mit Lese-Fähigkeit**: `RufeAsync(Auftrag, IAusfuehrung, ISucheX suche, …)` — nur `IReadStore`-Interfaces (sonst
  CQRS068); der generierte Dispatch öffnet je Aufruf einen Fähigkeits-Bereich (`IFaehigkeitsFabrik`), `GeneratedFunktionen.Faehigkeiten`;
  Boot-Guard: eine solche Funktion läuft nur im Host (nie `AddExterneFunktion`, also nie Laufort `Extern`); keine Python-Basis.
- Quellen (`Infrastructure/Quellen/`): `QuellEingang`, `QuellenDienst`, `AddQuellen(configuration)` mit `GeneratedQuellen`, der Datei-Treiber
  unter `Treiber/Datei/`, der Webhook einer Quelle als Konfig `Quellen:<Typ>:Webhook` (`MapQuellWebhooks` in `LaufeAsync`; der Editor liest
  die Route aus den Umgebungs-Dateien).
- Vermittlung (`Infrastructure/Funktionen/`: `FunktionsVermittlung`, `FunktionsVermittlerActor`, `FunktionsAbholer`, `FunktionsAnbieterSitzung`,
  `HandAusfuehrer`). **Akteur-Tor für angebotene Funktionen**: `IDarf<IFunktion-Typ>` = „der Akteur darf diese Funktion rechnen“ (CQRS058
  lässt es zu, `AkteurRechte.Funktionen` generiert); mit angemeldetem Akteur nimmt `FunktionsAnbieterSitzung.Biete` nur befugte Funktionen an
  (Fehler `FUNKTION_NICHT_BEFUGT`), ohne Tor offen wie jede andere Nachricht.
- Python: `FunktionsBasis` (`cqrs_client/funktion.py`), Angebot am Handshake, Slots + Lebenszeichen; je Verbindung eine Sitzung, nach einem
  Abbruch Reconnect und neue Sitzung.

**Beispieldomäne:** alle Domänen-Pipelines sind Flüsse und liegen in `Domain.Pipeline/<Bereich>/` (docs/07) — `Bildeingang` (ImagePair),
`DatensatzRangeAufloesung` und `DatensatzEinfrieren` (Datensatz, Funktionen mit Lese-Fähigkeit), `TrainingWaechter` (Trainingslauf: `Warte`
gegen ein Zeitlimit statt `Frist` + `FristStorno`), `Benchmark` (Benchmark). Keine nutzt `IPipelineHandler`.

**Editor und Werkzeuge**
- Modell `EditorModell.Fluesse` (`FlussArt.Warte`, `FlussSchritt.WarteAuf`, `FlussPort.Strom`), `Funktion.Faehigkeiten`; Extractor
  (`FlussLeser`: `.Strom()`, `.Warte<…>()`; Funktions-Signatur mit Fähigkeiten); Schreiber (Lambda-Parameter `quelleStrom`, `knotenZeit`;
  Knoten-Reihenfolge `FlussPipeline.Geordnet`); Grammatik (GR-FLUSS-*, GR-FUNKTION-FAEHIGKEIT); geprüft im Build vom
  `Domain.SourceGeneration/FlussAnalyzer.cs` (CQRS073–078); Board (⧗-Knoten, „⇢ Strom“-Port, Zuordnung `Id/Version` des Stroms;
  Funktions-Karte „⚙ liest“); Sonde (Code-Fluss + gezeichneter Fluss mit Warten/Strom, Funktion mit Fähigkeit).
- **LLM-Slots** (`docs/konzept-llm-minimalkontext.md`): `funktion` (Implementierung von `RufeAsync`; `GraphExtractor.Kern/SlotInventar.cs`),
  `funktionpy` (Python-Rumpf einer externen Funktion; `GraphExtractor.Kern/LlmKontext.cs`) und `fluss` (jeder Bau-Lambda, Anker = Klasse + Anweisung + Lambda-Index). Die
  LLM-Konsole füllt Fluss-Lambdas (genau ein Lambda, Zuordnungsform), prüft Stelligkeit und den Kandidaten im warmen Workspace
  (`Arbeitsbereich.PruefeProbeweiseAsync`) und schreibt den Lambda als Rumpf-Geste zurück (derselbe Weg wie ✎, Verlauf und ↶).

### 14.10 Entscheidung: das Handle-Modell (`IPipelineHandler`) entfällt als Domänen-Baustein

Alles, was die Handles taten, ist ein Fluss-Knoten oder eine Quelle: Verarbeitung = Katalog-Funktion (auch mit Lese-Fähigkeit),
Frist/Storno = `Warte` gegen ein Zeitlimit, Datei = Treiber / Webhook = `Quellen:<Typ>:Webhook` (Konfig), Event→Command = `p.Auf<E>()` →
`Sende`. Neue Pipelines sind Flüsse („⛓ Pipeline“); im Editor gibt es keinen Handle-Baustein. Die Laufzeit-Maschinerie der Handle-Welt ist
noch verdrahtet, hat keinen Domänen-Nutzer und wird zurückgebaut, sobald Takt-Treiber, „Client meldet Quell-Nachricht“ und flüchtige
Quellen da sind: `docs/13` W11-1, konzept-werkbank §10 und §12.2.

### Zuordnungs-Editor

Der Eingang eines Aufrufs (Fluss-Knoten ƒ/▶ je Eingang, Prozess-Regel „Dann") wird nicht als Text oder Tabelle gebaut, sondern in einem
eigenen Knoten-Editor (Panel: „🔀 Zuordnung im Knoten-Editor“, Kurzfassung „Feld ← Quelle“ antippen):

- **Links die Eingänge** — je Lambda-Parameter eine Karte (Name wie der Scaffolder ihn vergibt: Vorgänger-Knoten bzw. `t, r, g`) mit
  ihren Feldern als Ausgängen; Wert-Objekt-Felder sind eine Ebene aufgeklappt.
- **Mitte Bau-Knoten** — `new V(…)` für ein Wert-Objekt (seine Felder sind wieder Eingänge, beliebig tief) und `ƒx` für einen freien
  C#-Ausdruck (mit Vorschlägen der Quell-Ausdrücke).
- **Rechts das Ziel** — Auftrag bzw. Command mit seinen Feldern als Eingängen; „＋ new V“ an einem Wert-Objekt-Feld legt den Bau-Knoten
  an, „ƒx“ einen Ausdruck, ✕ (oder Klick auf den Draht) löst.
- **Drähte** ziehen (oder Quelle anklicken, dann Feld anklicken); nur typgleiche Ziele leuchten grün, falsche Typen werden abgelehnt.
  „↻ automatisch“ schlägt alles neu vor (Namensheuristik `flAutoArgs`, eine andere als `autoArgs` in §13.6; nur Vorschlag, `docs/13` W3-1); unten steht der
  entstehende Lambda.
- **Speicherung:** die Geste schreibt den Lambda `p => new Ziel(…)` sofort in den Code (konzept-werkbank §8.2).
  Ein Lambda aus dem Code wird beim Öffnen zerlegt (Parameter-Namen angeglichen, `new V(…)` → Bau-Knoten, Unbekanntes → `ƒx`); ist er
  kein einfacher Konstruktor, zeigt der Editor ihn und ersetzt ihn erst bei der ersten Änderung.

