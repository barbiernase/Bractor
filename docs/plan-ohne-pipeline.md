# Plan: Funktionen nur in Katalogen — die Pipeline-Schicht geht auf

Stand 2026-11-09, **nur Plan, nichts umgesetzt; die vier offenen Punkte (§5) sind entschieden.** Er folgt aus Tobis Entscheidung vom 2026-11-09 zur offenen Annahme A4 aus
[plan-katalog-und-graphen.md](plan-katalog-und-graphen.md): **Funktionen gibt es nur noch in Katalogen.** Umgesetzt wird schrittweise, je Schritt auf
Tobis Wort. Seit dem 2026-11-09 liegt der Fokus auf Editor und Oberfläche: geprüft wird je Schritt mit `dotnet build` und in der Werkbank, die
Server-Suiten laufen erst zur Abnahme und nur auf Tobis Wort.

## 1. Worum es geht

`<Wurzel>.Pipeline` (Rolle `Ablaeufe` in `Abstractions/Projektstruktur.cs`) stammt aus dem Fluss-Konzept, bevor es Funktionen und Graphen gab. Heute
hat die Schicht nur noch eine Aufgabe: Sie ist der Ort, der den Katalog **und** die Domäne kennt. Ein Graph der Anwendung nennt einen Katalog-Graphen
und übersetzt dessen Ausgaben in Commands an Aggregate; dafür muss er über beiden liegen. Daneben hielt sie die Funktionen der Anwendung (A4).

Fallen die Funktionen der Anwendung weg, bleibt in der Schicht nur **Verdrahtung**: Graphen, Übersetzungen und die Nachrichten, mit denen ein Client einen
Graphen anstößt. Dafür braucht es kein eigenes Projekt und keinen eigenen Begriff. Der Graph liegt dann neben dem Aggregat, an das er schreibt
(`Domain/ImagePair/Graphen.cs`), so wie Commands und Events dort liegen.

Was damit wegfällt:

- die Rolle `Ablaeufe` samt Suffix `.Pipeline`, die Projekte `Domain.Pipeline` und `Pruefdomaene.Pipeline`;
- die Abbildung „Namespace der Abläufe → Domäne“ (`Projektstruktur.DomaeneDerAblaeufe`, `rahmen.ablaeufeSuffix`, im Editor `ablaufNs` und
  `ablaufDomaene`). Sie macht heute die Zuordnung eines Graphen zu seinem Band umständlich und hat am 2026-11-09 einen Graphen ins falsche Projekt gelegt;
- die Funktion der Anwendung als Primitive: Technik, Konfig-Records und Commands ohne Ziel zwischen Funktionen gibt es nur noch im Katalog;
- dass eine Funktion ein Event hört (die Antwort auf ihren eigenen Aggregat-Command, CQRS108). Eine Katalogfunktion kennt kein Event der Domäne.

Was bleibt: der Katalog als eigenes Projekt, das nur `Abstractions` (und `Core`, andere Kataloge) kennt (A8). Der Compiler prüft weiter, dass eine
Funktion die Domäne nicht kennt. Die Gegenrichtung — die Domäne kennt keine Funktionsnachricht — prüft künftig ein Analyzer statt der Projektgrenze (§3).

## 2. Was eine Funktion heute nur kann, weil sie die Domäne sieht

Der Bestand zeigt drei Fähigkeiten, die mit der Schicht wandern müssen. Die Beispieldomäne braucht keine davon mehr (ihre Funktionen liegen schon in
`Bildvorschau.Katalog`); die Prüfdomäne und die Sonde brauchen alle drei.

### 2.1 Ein Command an ein Aggregat

`Nachschub` gibt `BuchePostenZu` aus, `Zielbuchung` ebenso, `Auftragsabwicklung` gibt `ReservierePosten` und `BelegeKontingent` aus. Im Katalog
geht das nicht: Die Funktion gibt einen eigenen Command ohne Ziel aus, und eine Übersetzung im Graphen macht daraus den Command an das Aggregat. So ist
die Bildaufnahme schon gebaut. Das ist kein Verlust, sondern genau die eine Stelle zwischen Katalog und Domäne.

Dabei zeigt sich: Viele Funktionen der Prüfdomäne sind nur eine Abbildung Feld für Feld (`Nachschub`, `Gutschrift`, `Merkmalsbildung`, `Zielbuchung`,
der Antwort-Teil von `Auftragsabwicklung` und `Verteilabwicklung`). Sie werden ganz zu Übersetzungen und verschwinden als Funktion.

### 2.2 Die Antwort auf den eigenen Command

`Auftragsabwicklung` hört `PostenReserviert` als Antwort auf `ReservierePosten` und gibt daraufhin `VermerkeReservierung` an den Auftrag;
`Zielbuchung` hört `PostenZugebucht` und meldet es der Verteilung. Heute geht das, weil die Funktion das Event kennt (CQRS108). Eine Übersetzung schickt
ihren Command zwar schon ab (`FunktionsKindActor.UebersetzeAsync`), aber ihre Antwort hört niemand.

**Vorschlag: die Antwort-Übersetzung.** Eine Übersetzung darf auch ein Event nehmen — die Antwort auf den Command, den die Übersetzung erzeugt, die sie
in `[Von]` nennt:

```csharp
public sealed record Auftragsablauf(
    [Kopf] Auftragsabwicklung Abwickeln,                                                        // Katalog: gibt PostenAnfordern aus
    [Von(nameof(Abwickeln))] Uebersetze<PostenAnfordern, ReservierePosten> Reservieren,         // an das Aggregat Posten
    [Von(nameof(Reservieren))] Uebersetze<PostenReserviert, VermerkeReservierung> Vermerken,   // seine Antwort an den Auftrag
    [Von(nameof(Reservieren))] Uebersetze<ReservierungAbgelehnt, VermerkeAbsage> Absagen) : IGraph;
```

- Welche Events in Frage kommen, steht in der Signatur des `Decide` für `ReservierePosten` (R1). Ein anderes Event ist ein Build-Fehler.
- Die Antwort-Übersetzung läuft inline im selben Kind, direkt nach dem Senden (wie heute das Weiterrechnen einer Antwort). Ihr Command geht an ein
  Aggregat, an einen Knoten des Graphen oder an einen fremden Kopf, wie bei jeder Übersetzung. Die Kennung kommt aus Aufruf, Ort, Stelle und Nummer der
  Antwort (`Kettenvertrag.Antwort`), die Ursache ist das Antwort-Event.
- Geht die Antwort an den Knoten zurück, der die Übersetzung speist (ein Katalog-Command an die Funktion, die den ersten gab), ist das eine Antwort und
  keine Kante eines Kreises: CQRS115 zählt sie nicht. Das ist dieselbe Regel, die heute für CQRS108 gilt, nur ausdrücklich im Graphen.
- Eine Funktion hört danach **nie** ein Event. Ihre Eingänge sind nur Commands ohne Ziel (GR-EINGANG wird einfacher), `Traeger.Antworten` und der
  Antwort-Zweig in `RechneAsync` fallen weg.

Die Alternative wäre, Antworten ganz aufzugeben und jede Rückmeldung als Reaktion eines Clients auf das Fakt zu bauen (wie seit Schritt 6). Das kostet
einen Umweg über das Log und einen Client, wo heute ein Schritt reicht; ich empfehle die Antwort-Übersetzung.

### 2.3 Lesen aus einem Store der Anwendung

`Schaetzer` liest die Projektion Messreihe über die Lese-Fähigkeit `IHoleMessreihe`, die die Leseseite der Prüfdomäne deklariert. Ein Katalog kennt
die Stores der Anwendung nicht.

**Vorschlag: der Katalog deklariert die Fähigkeit, die Anwendung setzt sie um.** Eine Fähigkeit ist schon heute eine Schnittstelle (`: IReadStore`, genau
eine Funktion, CQRS051), die ein Store implementiert. Der Katalog deklariert seine eigene, mit seinen eigenen Typen:

```csharp
// Pruefdomaene.Katalog/Schaetzung
public interface IHoleReihe : IReadStore { Task<Reihe> HoleAsync(Guid stelle); }
public sealed class Schaetzer(IZaehlwerk zaehler) : IFunktion
{
    public async Task<Geschaetzt> Auf(Merkmal m, IHoleReihe reihe) { … }
}
```

Der Store der Anwendung in `<Wurzel>.Infrastructure` implementiert sie zusätzlich zu den Fähigkeiten seiner Leseseite und liest dafür aus seinem eigenen
Read-Model. Die Speicher-Rolle darf dazu Kataloge referenzieren. Welcher Store einer Instanz die Fähigkeit liefert, folgt aus dem Code: genau ein Store
der Anwendung implementiert sie, sonst ein Build-Fehler (wie heute bei den Fähigkeiten der Leseseite). Die Fähigkeits-Fabrik der Funktionen findet
so auch Katalog-Fähigkeiten. Der allgemeine Store-Test gilt unverändert.

### 2.4 Kleinkram, der mitwandert

- **Neue Ids:** `Herkunft.NeueId<T>()` verlangt einen Aggregat-Typ. Für den Katalog gibt es `Herkunft.NeueId(zweck)` als `Guid` (dieselbe Ableitung),
  die Übersetzung macht daraus ausdrücklich `Id<T>` (A1 erlaubt `Guid` → `Id<T>` über `[Feld]`). So braucht es `Neu = true` an `[Feld]` nicht.
- **Logik in Antworten:** `VermerkeAbsage` baut heute einen Text aus zwei Zahlen. Das ist Logik und gehört nicht in eine Übersetzung; der Command
  trägt künftig die Zahlen, den Text bildet die Projektion bzw. der Client.
- **Hinweise:** Eine Katalogfunktion darf Hinweise ausgeben (Katalog-Typen); die Akteure, die sie hören, kennen den Katalog (Rolle Akteure nutzt
  Kataloge schon heute).

## 3. Die neue Projektstruktur

| Rolle | Projekt | Was dort liegt | darf referenzieren |
|---|---|---|---|
| Schreibseite | `<Wurzel>` | Aggregate, Commands, Events, Hinweise, Wertobjekte, Enums, Aggregat-Queries — **und Graphen der Anwendung mit ihren Übersetzungen und den Nachrichten der Clients an ihre Köpfe** | Abstractions, Core, Kataloge |
| Leseseite | `<Wurzel>.Projections` | unverändert | Schreibseite |
| Speicher | `<Wurzel>.Infrastructure` | Stores; **dazu die Fähigkeiten aus Katalogen, die sie umsetzen** | Schreib-, Leseseite, Kataloge |
| Akteure | `<Wurzel>.Akteure` | unverändert | Schreib-, Leseseite, Kataloge |
| Leitung, Komposition | unverändert, ohne Pipeline | | |
| Katalog | `<Name>.Katalog` | Funktionen, ihre Commands ohne Ziel, Hinweise, Konfig, Technik, Graphen, Fähigkeiten zum Lesen | Abstractions, Core, Kataloge |

Weil die Schreibseite jetzt Kataloge referenziert, prüft ein **Analyzer**, was bisher die Projektgrenze garantierte:

- **CQRS122 (neu):** Zustand eines Aggregats, `Decide`, `Apply`, Commands an Aggregate, Events, Hinweise und Aggregat-Queries der Schreibseite nennen
  keinen Typ aus einem Katalog. Erlaubt sind Katalog-Typen nur in Graphen und in Übersetzungen.
- **CQRS098 (Lage)** kennt keine Funktion, keine Technik, keinen Konfig-Record außerhalb eines Katalogs mehr; ein Graph liegt in `Graphen.cs` im Bereich der
  Schreibseite; ein Command ohne Ziel der Anwendung (ein Client stößt damit einen Graphen an) in `Commands.cs` daneben.
- **GR-EINGANG** wird enger: eine Funktion nimmt nur Commands ohne Ziel. CQRS108 meldet jedes Event an einer Funktion.
- **Antwort-Übersetzung:** CQRS119 erlaubt als `TEin` ein Event, wenn `[Von]` genau eine Übersetzung mit Aggregat-Command nennt und deren `Decide`
  das Event ausgeben kann; sonst ein Build-Fehler mit eigener Meldung (CQRS123, neu).

Eine Alternative wäre, die Schicht unter ehrlichem Namen (`.Ablaeufe`) zu behalten, nur mit Graphen und Übersetzungen. Dann bliebe die Projektgrenze, aber
auch die Namespace-Abbildung und ein Projekt mehr für jede Anwendung. Ich empfehle das Auflösen: die Zugehörigkeit eines Graphen zu seiner Domäne steht
dann direkt im Namespace, wie bei allem anderen.

## 4. Schritte

Jeder Schritt baut für sich. Abgenommen wird im Editor: was der Schritt einführt, wird in der Werkbank gezeigt bzw. angelegt und gebaut.

### Schritt 1 — Antwort-Übersetzung und Fähigkeiten aus dem Katalog

- `Uebersetzungsform`: Event als `TEin` (§2.2), CQRS119/123; Generator der Wegtabelle: je Übersetzung mit Aggregat-Command ihre Antwort-Übersetzungen;
  `FunktionsKindActor.UebersetzeAsync` rechnet sie nach dem Senden; CQRS115 zählt die Antwort an den Ausgangsknoten nicht als Kreis.
- Fähigkeits-Fabrik: Fähigkeiten aus einem Katalog, umgesetzt von genau einem Store der Anwendung (§2.3); die Speicher-Rolle nutzt Kataloge.
- `Herkunft.NeueId(zweck)` (§2.4).
- Sicht und Editor: die Antwort-Übersetzung steht als eigene Karte in der Spalte Übersetzungen, ihre Kante kommt vom Aggregat (Rücklauf); das Modal
  der Zuordnung zeigt links die Felder des Events.
- Sonde: je ein Beispiel für Antwort-Übersetzung und Katalog-Fähigkeit, `soll.txt` erweitert.

### Schritt 2 — Prüfdomäne ohne Pipeline

- Neues Projekt `Pruefdomaene.Katalog`: `Freigabeautomat`, `Mahnwesen` (mit `IMahnversand`), `Importlauf` (mit `IZeilenquelle`), `Importannahme`,
  `Ausreisserpruefung`, `Schaetzer` (mit `IZaehlwerk` und `IHoleReihe`), `Zielaufteilung` und der Ausgabe-Teil von `Auftragsabwicklung` und
  `Verteilabwicklung`, je mit eigenen Commands ohne Ziel.
- Was nur abbildet, wird Übersetzung: `Nachschub`, `Gutschrift`, `Merkmalsbildung`, `Zielbuchung`, die Antwort-Teile (§2.1, §2.2).
- Die Graphen ziehen nach `Pruefdomaene/<Bereich>/Graphen.cs`, die Nachrichten der Clients an ihre Köpfe nach `Pruefdomaene/<Bereich>/Commands.cs`;
  der `ReaktionsWorker` gibt dieselben Commands wie heute.
- Der Store der Messung setzt `IHoleReihe` um.
- `Pruefdomaene.Pipeline` und seine Referenzen (Akteure, Leitung, Laufzeit, Client.Worker, Solution) entfallen.

### Schritt 3 — Beispieldomäne ohne Pipeline

- `Linienimport` und `Bildaufnahme` ziehen nach `Domain/ImagePair/Graphen.cs`, ihre Client-Nachrichten daneben. `Domain` referenziert
  `Bildvorschau.Katalog` und `Bildverarbeitung.Katalog`.
- Tobis Versuche (`Domain.Pipeline/Datensatz/Graphen.cs`, `Domain/Pipeline/ImagePair/*`) ziehen nur auf sein Wort mit.
- `Domain.Pipeline` und seine Referenzen (Akteure, Leitung, Laufzeit, Integrationstests, Codegen, Solution) entfallen; die Umgebungen bleiben (sie nennen
  Graphen, keine Projekte).

### Schritt 4 — die Rolle verschwindet

- `Projektstruktur`: Rolle `Ablaeufe` und `DomaeneDerAblaeufe` weg; Graph und Client-Command bei der Schreibseite; Funktion, Technik, Konfig nur im
  Katalog; `NutzenKataloge` = Schreibseite, Speicher, Akteure, Leitung, Komposition.
- `Lageart`, CQRS098, CQRS122 (neu), GR-EINGANG/CQRS108 enger; Grammatik (`DomainEditor/Grammatik.cs`) mit Gegenstücken.
- `Strukturort`, `Scaffolder`, `Schreiben/Kataloge.cs`: ein neuer Graph landet im Bereich seiner Domäne; „Funktion der Anwendung“ gibt es nicht mehr.
- Extractor und Sicht: das Band eines Graphen ist die Domäne seines Namespace (`GraphBand` ohne Umweg); `rahmen.ablaeufeSuffix` entfällt.
- `Cqrs.Codegen`, `scripts/server-einrichten.sh`, Projektdateien.
- Laufzeit: `Traeger.Antworten` und der Antwort-Zweig in `FunktionsKindActor.RechneAsync` fallen weg.

### Schritt 5 — Sonde

- Die rund 30 Funktionen der Sonde (Leihwesen, Kundenportal, Anlagen, Warenkatalog, Wetterdienst) ziehen in Sonden-Kataloge wie heute schon
  `Katalog.Peilung.*`; was nur abbildet, wird Übersetzung. `Ablaeufe.Funkortung.cs.txt` geht in die Schreibseite der Funkortung auf.
- `soll.txt` (Lage, Besitz, Sicht) und das Gesten-Skript (`Gezeichnet.gesten.json`) ziehen nach; die Sonde muss weiter alle allgemeinen Primitive
  abdecken, jetzt mit Antwort-Übersetzung und Katalog-Fähigkeit.

### Schritt 6 — Editor

- „Wo liegt die neue Funktion?“ bietet nur Kataloge (`komFunktionNeu`), ebenso „ƒ neue Funktion der Anwendung, die sie nimmt …“ am Kopf.
- „＋ Graph“ am Band legt den Graphen im Namespace der Domäne an; `ablaufNs` und `ablaufDomaene` entfallen.
- Antwort-Übersetzungen auf der Fläche (Karte, Rücklauf-Kante vom Aggregat) und im Modal; „＋ ↦“ an einem Aggregat-Command bietet zusätzlich
  „Antwort übersetzen“ für jedes Event seines `Decide`.

### Schritt 7 — Doku und Abnahme

- docs/04 (§4.4 Funktionen und Graphen, §4.5 Abläufe), 05, 07 (Rollen), 10 (Grammatik, Regeln, A4/A8 in plan-katalog-und-graphen.md), 11, 13,
  konzept-funktionen-im-editor.md, README, CLAUDE.md.
- Abnahme auf Tobis Wort: volle Stufe mit `--check`, `--sonde`, `--grammatik`, Codegen-Drift, `./pruefe-checkout.sh`, Werkbank-Abnahme.

## 5. Entschieden (2026-11-09, Tobi: „Ja, alle vier“)

1. **Analyzer statt Projektgrenze** (§3): die Schreibseite referenziert Kataloge, CQRS122 hält die Domäne frei von Katalog-Typen; keine Schicht
   `.Ablaeufe`. Damit gilt §1 „Drei Schichten“ in plan-katalog-und-graphen.md nur noch für den Katalog (er kennt die Domäne nicht).
2. **Antwort an den Ausgangsknoten ist erlaubt** (§2.2): sie ist eine Antwort, keine Kante eines Kreises (CQRS115 zählt sie nicht).
3. **Katalog-Fähigkeit im Store der Anwendung** (§2.3), keine Lese-Übersetzung.
4. **Tobis Versuchsdateien ziehen in Schritt 3 mit** (`Domain.Pipeline/Datensatz/Graphen.cs`, `Domain/Pipeline/ImagePair/*`) — gelesen als Zustimmung zur
   ersten der beiden genannten Möglichkeiten.
