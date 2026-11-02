# Plan: Commands und Funktionsgraphen

Stand 2026-11-02, entschieden mit Tobi. Dieser Plan ist der Maßstab für den Umbau von Quellen, Funktionen, Akteuren und Clients; nach dem Umbau
beschreiben ihn docs/04, docs/10, docs/11 und docs/konzept-akteure.md, und der Plan wandert in den Git-Verlauf.

## Ziel in einem Absatz

Es gibt drei Arten von Nachrichten: einen **Command** (Absicht, genau ein Empfänger), ein **Event** (Fakt im Log, nur aus einem Decide) und einen
**Hinweis** (flüchtig). Gerechnet wird in **Funktionsgraphen**: eine Funktion ist eine Klasse mit typisierten Ports, ein Graph ist ein eigener Typ,
der Funktionen (und andere Graphen) als Knoten nennt; die Kanten leitet der Build allein aus den Typen ab. Folge, Fork, Wahl, Join, Je, Sammeln,
Rennen und Warten mit Frist sind Formen von Signaturen, keine Knotenarten. Ein Graph ist eine Blaupause und läuft als **Instanz** mit eigener Konfig,
beliebig oft (zwei Dateiwächter für zwei Verzeichnisse). Eine **Quelle** ist nichts Eigenes: eine Funktion aus dem Framework-Katalog am Kopf eines
Graphen, durabel über ihren gesicherten Zustand. Ein **Akteur** sagt, was er hineingibt (`IDarf`) und was er hört (`IHoert`); ein **Client** sagt
nur, welche Akteure er verkörpert. Das ist die Grundlage einer typisierten, grafischen Kompositionssprache im Editor.

## So sieht es danach aus

```csharp
// ── Nachrichten ───────────────────────────────────────────────────────────────────────────────────────────────────────
public record NimmBildAuf(Id<ImagePair> AggregateId, string PairKey, DateTimeOffset ProduziertAm, string Pfad) : ICommand<ImagePair>;  // Ziel im Typ
public record BildGelesen(string Pfad, string Name, Bild Bild) : ICommand;           // Empfänger aus dem Graphen
public record BeurteilePaar(Id<ImagePair> Paar) : ICommand;                         // Empfänger: ein Akteur draußen
public record ImagePairKomplett(Id<ImagePair> Paar, DateTimeOffset Zeit) : IEvent;
public record BildNichtLesbar(string Pfad) : ITransientEvent;

// ── Funktion: Klasse, Ports = Typen ──────────────────────────────────────────────────────────────────────────────────────
public sealed class Bildeinlesen(IBildleser leser) : IFunktion
{ public async IAsyncEnumerable<OneOf<BildGelesen, BildNichtLesbar>> Auf(DateiGefunden d) { … } }       // Wahl
public sealed class Bildverkleinerung<TFormat>(IBildverkleinerer t, Verkleinerungskonfig k) : IFunktion where TFormat : IBildformat
{ public BildVerkleinert<TFormat> Auf(BildGelesen b) => …; }                                           // ┐ Fork: drei Knoten
public sealed class Kontrastanalyse(IHistogrammrechner t) : IFunktion { public Histogramm Auf(BildGelesen b) => …; }  // │ nehmen BildGelesen
public sealed class Kachelung : IFunktion { public IEnumerable<Kachel> Auf(BildGelesen b) => …; }       // ┘ Je
public sealed class Kachelpruefung(IKachelmodell m) : IFunktion { public KachelBefund Auf(Kachel k) => …; }
public sealed class Bildbefundung : IFunktion { public Bildbefund Auf(IReadOnlyList<KachelBefund> alle) => …; }       // Sammeln
public sealed class Bildmeldung : IFunktion                                                              // Join
{ public NimmBildAuf Auf(BildVerkleinert<Vorschau> v, Histogramm h, Bildbefund b, Herkunft herkunft) => …; }

// Quellfunktion aus dem Framework-Katalog: lauscht, Zustand gesichert
public sealed class Dateiwaechter(Dateiwaechterkonfig konfig, IZustand<Gesehen> gesehen) : IFunktion
{ public async IAsyncEnumerable<DateiGefunden> Lausche([EnumeratorCancellation] CancellationToken ct) { … } }

// Technik: schlichte Schnittstelle im Konstruktor, kein Marker
public interface IBildverkleinerer { Bild Verkleinere(Bild b, int breite); }

// ── Graph: Knoten als Parameter, Kanten aus den Typen ──────────────────────────────────────────────────────────────────
public sealed record Bildaufbereitung(
    Bildeinlesen Einlesen, Bildverkleinerung<Vorschau> Vorschau, Kontrastanalyse Kontrast,
    Kachelung Kacheln, Kachelpruefung Pruefung, Bildbefundung Befund, Bildmeldung Meldung) : IGraph;   // Signatur: DateiGefunden → NimmBildAuf | BildNichtLesbar
public sealed record Linienimport(Dateiwaechter Eingang, Bildaufbereitung Aufbereitung) : IGraph;  // Graph im Graphen
public sealed record Nachlieferung(Webhook Eingang, Dateiholer Holen, Bildaufbereitung Aufbereitung) : IGraph;

// ── Aggregat (unverändert bis auf die Ausgaben) ─────────────────────────────────────────────────────────────────────────
public IEnumerable<OneOf<ImagePairErstellt, ImagePairKomplett, BildVersionBereitsVerfuegbar, BeurteilePaar>> Decide(NimmBildAuf c) { … }

// ── Akteur und Client ──────────────────────────────────────────────────────────────────────────────────────────────────
public sealed record KameraSystem   : IAkteur, IDarf<NimmBildAuf>;
public sealed record Klassifizierer : IAkteur, IDarf<KlassifiziereBildPaarDurchKi>, IHoert<BeurteilePaar>, IHoert<ModellAktiviert>;
public sealed record Inspekteur     : IAkteur, IDarf<LabelBildPaar>, IDarf<GetImagePair>, IHoert<ImagePairKomplett>, IHoert<BildNichtLesbar>;
public sealed record KlassifikationsWorker : IClient, IVerkoerpert<Klassifizierer>;
public sealed record Arbeitsplatz          : IClient, IVerkoerpert<Inspekteur>, IVerkoerpert<KIOperator>, IVerkoerpert<Modellfreigeber>;
```

```json
// eine Instanz je Datei; dieselbe Blaupause beliebig oft
// umgebungen/produktion/graphen/linie1.json
{ "Graph": "Linienimport", "Akteur": "KameraSystem",
  "Eingang": { "Verzeichnis": "/share/linie1", "Muster": "*.png", "Nachscan": "00:00:30" },
  "Aufbereitung.Vorschau": { "Breite": 256, "Parallel": 4 },
  "Aufbereitung.Pruefung": { "Parallel": 8, "Zeitlimit": "00:00:10", "Wiederholungen": 2 } }
// umgebungen/produktion/graphen/linie2.json
{ "Graph": "Linienimport", "Akteur": "KameraSystem", "Eingang": { "Verzeichnis": "/share/linie2", "Muster": "*.tif" } }
```

```
                         ┌──► Vorschau ───────────────────────────────────────────────┐
DateiGefunden ─► Einlesen ─BildGelesen─┼──► Kontrast ──────────────────────────────────────────────┼──► Meldung ═NimmBildAuf═► [[ImagePair]]
                    │                  └──► Kacheln ═Kachel*═► Pruefung (×n) ═KachelBefund*═► Befund ─┘
                    └─BildNichtLesbar (Hinweis) ─► Akteure, die ihn hören
```

## Entscheidungen

1. **Drei Arten von Nachrichten, ein Wort je Absicht.** Command (`ICommand<TAggregat>` mit Ziel im Typ, `ICommand` ohne), Event (`IEvent`), Hinweis
   (`ITransientEvent`), dazu Query (`IQuery`) und `Frist<TCmd>`. Was zwischen Funktionen fließt, ist ein Command — kein eigener Nachrichten-Marker.
   Folge, Zwischenergebnis, Quell-Nachricht, `DateiGemeldet`, `ClientGemeldet` entfallen.
2. **Ein Command, ein Empfänger, vom Absender aus:** (a) Ziel im Typ → das Aggregat; (b) sonst der Knoten **desselben Graphen**, der ihn nimmt (er
   bleibt in dieser Instanz und diesem Lauf); (c) sonst genau ein Empfänger in der Domäne — ein Akteur mit `IHoert<T>` oder der Kopf eines Graphen.
   Kein Empfänger, zwei Empfänger, oder ein Typ, der zugleich in einem Graphen genommen und draußen gehört wird: Build-Fehler. Absender und Empfänger
   kennen einander nicht; das `IDarf` eines Akteurs sagt nur, was er hineingeben darf.
3. **Decide gibt keine Aggregat-Commands aus.** Seine Ausgaben: Events, Hinweise, Commands ohne Ziel (an einen Akteur oder einen Graph-Kopf; durabel
   im selben Commit als offener Eingang) und `Frist<TCmd>` (unverändert). Aggregate reden nur über Graphen oder Akteure miteinander.
4. **Funktion = Klasse mit typisierten Ports.** Marker `IFunktion`, Name = Klassenname. Je `Auf` sind die Command-Parameter die Eingangsports, dazu
   optional `Herkunft`, Lese-Fähigkeiten (`: IReadStore`) und ein `CancellationToken`; mehrere `Auf` sind mehrere Operationen desselben Knotens. Die
   Fälle des Rückgabetyps sind die Ausgangsports: `T`, `OneOf<…>`, `IEnumerable<…>`, `IAsyncEnumerable<…>` (in `Task` nur als
   `IAsyncEnumerable`), mit Commands und Hinweisen; kein `void`, kein `T?`, kein `ValueTask`, ein leerer Strom ist „nichts“. Im Konstruktor:
   Konfig-Record (Werte je Instanz und Knoten), Technik, `IZustand<T>`, `ILogger<T>`. Eine Funktion darf das Event hören, das ihr eigener
   Aggregat-Command auslöst (`Auf(ImagePairKomplett e)`), eine Stufe tief. Generische Funktionen (`Bildverkleinerung<TFormat>`, `Entprelle<T>`)
   sind erlaubt; das Typargument unterscheidet gleiche Funktionen mit verschiedenem Ausgang.
5. **Vielfachheit aus den Typen.** Ein Ausgang `T` liefert genau 1, ein `OneOf`-Fall 0 oder 1 (genau einer der Fälle), ein Strom 0 bis n. Ein
   Eingangsparameter sagt, was er erwartet — das ist die ganze Synchronisation:

   | Parameter | Bedeutung |
   |---|---|
   | `T t` | genau einer je Lauf; warten |
   | `T? t` | höchstens einer; feuern, wenn feststeht, dass keiner kommt, oder wenn das `Zeitlimit` des Knotens abläuft (dann `null`) |
   | `OneOf<A, B> erster` | der erste von beiden je Lauf; die übrigen Zweige werden abgebrochen |
   | `IReadOnlyList<T> alle` | alle `T` der Unterläufe eine Ebene tiefer, wenn ihre Erzeuger fertig sind |

   Der Build prüft die Vielfachheit vom nächsten gemeinsamen Vorfahren aus: ein `T`-Parameter hinter einem `OneOf` muss `T?` (oder `OneOf`) sein,
   hinter einem Strom `IReadOnlyList<T>`; ein falscher Parameter ist ein Fehler mit Vorschlag. Damit sind Folge, Fork, Join, exklusive Wahl,
   Zusammenfließen, Mehrfachwahl und ihr Zusammenführen, mehrere Instanzen (Je) mit Sammeln, Fork-Join je Element, Rennen und Warten mit Frist
   Typ-Formen. N von M ist `IReadOnlyList<T>` plus `Mindestens` in der Konfig; Schleifen gibt es im Graphen nicht (ein Graph darf einen Command an
   seinen eigenen Kopf geben — ein neuer Lauf); was über Läufe hinweg gilt (Fenster, Charge), steht im Zustand eines Knotens oder in einem Aggregat.
6. **Graph = eigener Typ, Kanten aus den Typen.** `record X(… Knoten …) : IGraph`; jeder Parameter ist ein Knoten (Name = Konfig-Schlüssel, Typ =
   Funktion oder Graph), die Reihenfolge bedeutet nichts. Kanten gibt es nicht zum Schreiben: gibt ein Knoten `T` aus, bekommt es jeder Knoten des
   Graphen, der `T` nimmt. Regeln: jeder Eingangsport hat einen Erzeuger im Graphen oder ist ein Eingang des Graphen; jeder Ausgang hat einen
   Empfänger im Graphen oder verlässt ihn (Command an ein Aggregat, einen Akteur, einen anderen Graphen; Hinweis); kreisfrei; genau ein Kopf je
   Eingangstyp. Die **Signatur** eines Graphen ergibt sich (Eingänge = Ports, die kein innerer Knoten erzeugt; Ausgänge = Ports, die kein innerer
   Knoten nimmt), deshalb ist ein Graph als Knoten in einem anderen erlaubt — das Abstraktionsmittel der Sprache. Zwischen Knoten steht nie Klebecode:
   passt ein Typ nicht, ist der Adapter eine eigene kleine Funktion. Kein Builder im Rumpf, keine ausdrücklichen Kanten, keine Knotenarten (Lehre
   aus dem früheren Fluss `IPipeline`, Verlauf ab `b5090b8`). Sichtbar wird der Graph im Editor, mit `--graph <Typ>` und in jedem Build-Befund.
7. **Lauf.** Jede Nachricht im Graphen gehört zu einem Lauf, den eine Kopf-Nachricht startet (ein Command aus der Domäne oder eine Meldung der
   Quellfunktion); ein Strom öffnet je Element einen Unterlauf (Pfad + Nummer). Ein Join paart nur im selben Pfad, `IReadOnlyList` sammelt eine
   Ebene tiefer, `T?` und Listen feuern über die Fertigmeldung der Erzeuger. Läufe verschiedener Köpfe kommen in einem Graphen nie zusammen: ein
   Graph ist eine Rechnung je Auslöser.
8. **Quellen sind Katalogfunktionen.** Eine Quellfunktion hat `Lausche(CancellationToken)` statt `Auf` und steht am Kopf eines Graphen. Katalog des
   Frameworks: `Dateiwaechter` (`DateiGefunden`), `Taktgeber` (`Takt`), `Webhook` (`Aufruf`), später `OpcUaLeser` (`Messwert`); ihre Commands liegen
   in `Abstractions`. Quell-Commands stehen nicht im Log. Ob eine Quelle Doppeltes vermeidet oder nach einem Neustart weitermacht, regelt sie über ihren
   Zustand (Gesehen-Liste, Sequenznummer, Hochwassermarke) — die Architektur liefert nur den gesicherten Zustand.
9. **Zustand ist gesichert.** `IZustand<T>` je Instanz und Knoten, ein Schreiber; gesichert, wenn der Lauf, der ihn geändert hat, fertig ist, in der
   Reihenfolge der Läufe (bisher: Cache der Aktivierung).
10. **Instanz = Blaupause + Konfig.** Ein Graph läuft erst als Instanz: eine Datei je Instanz mit `Graph`, bei Quellköpfen `Akteur`, und je Knoten
    (Pfad `Aufbereitung.Vorschau` für innere Graphen) die Felder seines Konfig-Records sowie `Parallel`, `Warteschlange`, `Wiederholungen`,
    `Zeitlimit`, `Technik`. Das Schema je Graphtyp erzeugt der Generator; der Boot prüft jede Instanz dagegen, prüft, dass der `Akteur` jeden
    Ausgangs-Command `IDarf`, und dass ein Graph mit Command-Kopf je Umgebung nur einmal läuft. Ein Graph mit Command-Kopf erbt den Akteur des
    Auslösers. `Funktionen:<F>` je Funktionstyp entfällt.
11. **Fehler und Abbruch.** Wirft ein Knoten, wird er nach `Wiederholungen` erneut versucht; danach ist der Lauf gescheitert: der Kopf wird geparkt,
    die offenen Zweige bekommen ihr `CancellationToken`, an die Akteure, die ihn hören, geht `LaufGescheitert`. Dasselbe Abbrechen gilt für die
    Verlierer eines Rennens.
12. **Technik ohne Marker.** Jede Schnittstelle oder Klasse im Konstruktor einer Funktion, die weder Konfig-Record noch `ILogger<T>` noch
    `IZustand<T>` ist, ist Technik; der Generator bindet sie (auch geschachtelt), die Instanz wählt bei mehreren Umsetzungen (`Technik`).
    `ILebenszyklus` entfällt (keine Nutzung); eine Technik lädt beim ersten Aufruf, Freigabe über `IAsyncDisposable`.
13. **Akteur und Client.** Ein Akteur ist `record X : IAkteur` mit `IDarf<T>` (was er hineingibt: Commands, Queries) und `IHoert<T>` (was zu ihm
    hinkommt: ein Command ist Arbeit, ein Event oder Hinweis Kenntnis). Die Arten `IMensch`, `IMaschine`, `IKi` entfallen (heute nur Symbol und
    Sortierung im Editor), ebenso der Client-Vertrag als Interface mit eigenem `IHoert`. Ein **Client** ist ein Record `X : IClient, IVerkoerpert<A>…`
    in `Domain.Akteure/Clients.cs` und nennt nur, wen er verkörpert (Karte im Editor, Handshake-Name = Record-Name, Hash aus seinen Akteuren). Wirksam
    ist an jeder Sitzung „verkörpert ∩ Token“, für beide Richtungen. Arbeit gibt die Werkstatt genau einer solchen Sitzung und stellt sie erneut zu,
    bis eine Antwort kommt (die erste gewinnt); die Antwort ist ein gewöhnlicher Command über das `IDarf`. Wer einen Akteur verkörpert, muss seine
    Arbeit annehmen können: die generierte Basis verlangt je gehörtem Command einen Handler. Laufort, `Implementierung` und `IDarf<IFunktion>`
    entfallen; bewusst aufgegeben: denselben Schritt je Umgebung zwischen Host und draußen umzuschalten, und ein eigener Kenntnis-Satz je Software.
14. **Hinweise gehen nur an Akteure draußen.** Aggregate und Funktionen geben Hinweise aus; Funktionen hören keine (das Hinweis-Netz zu Funktionen
    entfällt). Ein Hinweis geht an die Sitzungen der Akteure, die ihn hören; eine Ablehnung zusätzlich gezielt an die Sitzung des Absenders.
15. **Ids und Herkunft.** Command-Ids leitet das Framework ab, nie der Entwickler: im Graphen aus Instanz, Lauf-Pfad, Knoten und Stelle; der Lauf-Kopf
    ist die Id des Commands bzw. bei einer Quelle Instanz + laufende Nummer (gesichert mit dem Zustand); die Antwort eines Akteurs aus der Id des
    Auftrags. Aggregat-Ids über `h.NeueId<T>(schlüssel)`, deterministisch. `Herkunft` behält den Typ: nach einem Decide Stream, Version, Log-Zeit;
    aus einer Quelle Instanz, Nummer, Zeit; dazu Akteur und Korrelation. Die Spur zeigt je Lauf den Pfad durch den Graphen.
16. **Ablage nach Erzeuger.** Schreibseite `Commands.cs` (Aggregat-Commands und was ein Decide abgibt), `Events.cs` (Events, Hinweise); `.Pipeline`
    `<Bereich>/Funktionen.cs`, `Graphen.cs`, `Commands.cs` (was nur Knoten einander geben), `Konfigs.cs`, Technik in eigenen Dateien; `.Akteure`
    `Akteure.cs`, `Clients.cs`; Framework-Katalog `Infrastructure/Funktionen/Katalog/`, seine Commands in `Abstractions`. `Folgen.cs`,
    `Zwischenergebnisse.cs`, `Quellen.cs` entfallen.

## Transport je Kante

| Kante | Weg | Garantie |
|---|---|---|
| Knoten → Knoten im Graphen | im Speicher auf dem Cluster-Knoten der Instanz; Fork-Zweige und Unterläufe nebeneinander bis `Parallel` | mindestens einmal; bricht ein Lauf ab, startet er vom Kopf neu |
| Graph → Aggregat | Emitter → Inbox | genau einmal (abgeleitete Id + Inbox) |
| Decide oder Akteur → Graph-Kopf | offener Eingang (`cqrs_eingaenge`), schneller Weg, Zusteller | offen, bis der Lauf fertig ist |
| Graph oder Decide → Akteur draußen | Werkstatt → Sitzung mit „verkörpert ∩ Token“ | erneut zugestellt bis zur Antwort, die erste gewinnt |
| Akteur → Aggregat oder Graph-Kopf | Sitzung → Tor (`IDarf` ∩ Token) | genau einmal am Empfänger |
| Quellinstanz | genau einmal im Cluster, Ort im Gossip-Zustand | Zustand und Nummer nach fertigem Lauf gesichert |
| Event → Projektion, Kenntnis → Akteur, Query, Frist | unverändert (Log/Wecker/Takt, Verteiler, Query-Dienst, Uhr) | unverändert |

Eine Funktion, die außerhalb des Systems etwas bewirkt (eine Datei schreibt), muss Wiederholung vertragen; genau einmal sind die Commands, die den
Graphen verlassen. Kein Dirigent je Vorgang, keine Regeln im Log: die Buchführung eines Laufs (wer wartet auf wen) liegt im Speicher der Instanz,
haltbar ist allein der Kopf.

## Offen (mit Vorschlag)

1. **Name `IGraph`** — kollidiert sprachlich mit dem Graphen des Extractors. Alternativen: `IKomposition`, `IAblauf`. Vorschlag: `IGraph`, der
   Extractor-Graph heißt im Code ohnehin `Sicht`/`Karte`.
2. **Umgebung als Ordner** (`umgebungen/<name>/umgebung.json` + `graphen/<instanz>.json`) — ändert W7 von „eine Datei“ zu „ein Ordner“. Alternative:
   Instanzen als Abschnitt `Graphen` in der einen Datei. Vorschlag: Ordner.
3. **Akteur je Instanz in der Konfig**, im Boot gegen sein `IDarf` geprüft. Alternative: ein Code-Fakt am Akteur (bräuchte ein `IDarf` auf etwas, das
   kein Command ist). Vorschlag: Konfig.
4. **Wie eine C#-Client-Assembly ihren Client nennt** (heute `[assembly: IstClient<…>]`, nur für den Client-Analyzer). Wird in Schritt 6 entschieden.

## Präzisierungen aus der Umsetzung

Beim Umbau nachgezogen (Schritt 1–2), passend zu den Entscheidungen, ohne neuen Begriff für den Entwickler:

- **Framework-Sicht `IAggregatCommand`** (Entscheidung 1): `ICommand` hat kein Ziel mehr; `ICommand<T>` erbt die nicht-generische Sicht
  `IAggregatCommand { Guid Ziel }`, über die Emitter, Aggregat-Actor, Leitung und Uhr das Ziel lesen. Der Fachcode schreibt sie nie (CQRS013:
  von Hand implementiert oder `Id<T> AggregateId` ohne Typargument ist ein Fehler).
- **Lage** (Entscheidung 16): neue Primitive `Technik` (eigene Datei bei den Abläufen), `Graph` (`Graphen.cs`) und `KnotenCommand` (ein Command ohne
  Ziel, den kein Decide abgibt: `Commands.cs` der Abläufe). Hinweise, auch die aus Funktionen, liegen in `Events.cs` der Schreibseite.
- **Instanz je Cluster-Knoten** (Entscheidung 10, Transport): jeder Cluster-Knoten hält von jeder Instanz einen Graph-Actor; ein Lauf bleibt in dem,
  der seinen Kopf annahm. Genau einmal im Cluster läuft eine Instanz, deren Knoten Zustand haben (Sperre je Instanz), und — ab Schritt 4 — eine
  Quellinstanz. Eine Instanz mit Knoten draußen hält ein Cluster-Knoten, sobald für jede ihrer Funktionen ein Arbeiter angemeldet ist.
- **Ohne Umgebung** (Prüfstände, Tests) läuft je Graph eine Instanz unter seinem Namen; mit Umgebung nur, was ihr Ordner `graphen` nennt.
- **Akteur einer Quell-Instanz** darf, was den Graphen verlässt (Beispiel: `KameraSystem : IDarf<NimmBildAuf>`).
- **Ein Command ohne Ziel von einem Akteur** über Leitung bzw. JSON-Eingang an einen Graph-Kopf kommt mit Schritt 4 (dort werden die
  Quell-Nachrichten der Clients zu Commands).

Aus Schritt 3:

- **Graph-Actor als Koordinator** (Entscheidungen 5, 7, 11): die Buchführung eines Laufs (`Infrastructure/Funktionen/Lauf.cs`) kennt je Pfad und
  Operation einen Aufruf; die Parents sind Warteschlange, Kinder, Wiederholungen und Abbruch, sie merken sich keine Kennungen mehr (das tut der
  Graph-Actor für die Köpfe). Ausgaben an Knoten des Graphen werden mit dem Erfolg des Aufrufs sichtbar (eine Wiederholung liefert sie nicht doppelt);
  Commands an Aggregate gehen sofort. Ids: `Kettenvertrag.Aufruf(Lauf, Ort, Pfad, Auf)`, darunter wie bisher Befehl/Antwort/Ausgabe aus Aufruf und
  Stelle; die Stelle zählt über den ganzen Aufruf (auch Ausgaben aus Antworten). Eingänge (`EingangZustellen`) gibt es nur noch am Kopf.
- **Feuern**: ein Aufruf, dessen Ports alle leer ausgehen (lauter `T?` = null), feuert nicht; eine leere Liste ist ein Wert (der Strom kam, ohne
  Element). Das `Zeitlimit` des Knotens begrenzt eine Rechnung (danach Wiederholung) und das Warten eines Auf auf seine `T?`-Ports, sobald die übrigen
  da sind. Wer nach einem Rennen oder einer Frist nicht mehr gebraucht wird und nichts nach draußen gibt, wird abgebrochen.
- **Gescheiterter Lauf**: `LaufGescheitert` liegt in `Abstractions` (`Graph.cs`) und geht über den Hinweis-Weg; geparkt wird in `cqrs_eingaenge`
  (Spalte `geparkt` mit dem Grund, nie mehr fällig).
- **Generische Funktionen**: die offene Definition ist eine Blaupause, je Typargument eines Knotens ein Baustein (`Bildverkleinerung<Vorschau>`);
  generische Commands fließen nur zwischen Knoten (nicht über die Leitung, nicht am Kopf).
- **Domäne**: die Bildaufbereitung ist ein Fork-Join ohne Kacheln — `BildEingelesen` gabelt in die Vorschau-Kette und direkt zum Bildeingang, der
  beide joint (die Python-Worker bleiben unverändert); Je + Sammeln zeigen die Prüfdomäne (`Zubuchung`) und die Laufzeit-Tests.
- **Spur je Lauf mit Pfad** zieht mit den Graph-Karten in Schritt 7: der Wirker ist eine Karten-Id (W9), Karten für Knoten gibt es erst dort.

Aus Schritt 4:

- **Katalog in `Core/Katalog`** statt `Infrastructure/Funktionen/Katalog`: eine Domäne nennt die Quellfunktion als Knoten im Graph-Record
  (`Linienimport(Dateiwaechter Eingang, Bildaufbereitung Aufbereitung)`) und sieht `Infrastructure` nicht; `Core` ist schon die Bibliothek der Treiber, die
  die Domäne benennt (Stores). Die Katalog-Commands (`DateiGefunden`, `Takt`, `Aufruf`) liegen in `Abstractions`, Codegen und Lesart nehmen sie in Leitung
  und Wire auf (ein Python-Worker kann sie bekommen). Katalogfunktionen nehmen keinen `ILogger` (der Codegen liest Konstruktoren ohne Logging-Assembly).
- **Gesicherter Zustand**: Tabelle `cqrs_zustaende` je Instanz und Knoten (Bild aus eigenem und Technik-Zustand, ohne Reflection gepackt; bei einer Quelle
  die laufende Nummer). Das Kind schickt nach jedem Aufruf einen Schnappschuss, der Graph-Actor sichert den letzten, dessen Lauf und alle früheren fertig
  sind (auch gescheiterte zählen als fertig: ein gescheiterter Quell-Lauf meldet `LaufGescheitert`, die Quelle wiederholt ihn nicht). Die Sperre bleibt je
  Instanz (gröber als je Knoten, aber genau ein Schreiber).
- **Quelle**: das Kind des Quellknotens lauscht, solange der Cluster-Knoten die Instanz hält (Sperre); je Element Nummer + Schnappschuss an den Graph-Actor,
  das nächste erst, wenn er den Lauf annahm (Gegendruck über die Warteschlange des Quellknotens). Lauf-Kennung `quelle:<instanz>:<nummer>`, Herkunft und
  Ursache aus Instanz und Nummer (kein Log-Eintrag). Der Webhook hält nichts fest (Postfach je Pfad, `POST /eingang/webhook/<pfad>`).
- **Ohne Umgebung** läuft ein Graph mit Quelle nur, wenn der Test die Konfig seiner Quelle nennt (eine Quelle braucht ihre Werte).
- **Akteur → Graph-Kopf**: der JSON-Eingang nimmt an `/eingang/command` auch Commands ohne Ziel (`IDarf`), legt einen offenen Eingang (Kennung aus der
  Command-Id) und nimmt den schnellen Weg; `/eingang/quelle` entfällt. Die Leitung folgt mit Schritt 6 (Client als Record).

## Schritte

Jeder Schritt baut den Bestand direkt um (keine Abwärtskompatibilität), endet mit grünem `dotnet build` und `scripts/server.sh lauf scripts/tests.sh
schnell` und ist ein eigener Commit. Grammatik, Analyzer und Test kommen je Regel zusammen (W5).

### Schritt 1 — Technik ohne Marker (Entscheidung 12)

- `Funktionsform`: gerufen = jeder Typ im Konstruktor einer Funktion, der nicht Konfig, Logger oder Zustand ist; `IstGerufen` über `IFunktion`
  entfällt. `Ctorbindung`, `GerufeneGenerator`, `GerufenAnalyzer` (CQRS089–092) darauf umstellen.
- `ILebenszyklus`, `LebenszyklusAnalyzer` (CQRS107), `Funktionsvertrag.Lebenszyklus`, der Ruf in `FunktionsAktivierung` entfallen.
- Domäne: `IImageResizer`, `IHistogramEqualizer` u. a. ohne `: IFunktion`; Prüfdomäne entsprechend.

### Schritt 2 — Commands, Funktion als Klasse, Graph mit Folge, Fork und Wahl (Entscheidungen 1–4, 6, 10, 15, 16)

- `Abstractions`: `ICommand` ohne Typargument als Command ohne Ziel; `Ziel` wandert in `ICommand<T>` (CQRS013 kehrt sich um); `IGraph`.
  `Entscheidungsausgabe.IstFolge` → Command ohne Ziel.
- `Funktionsform`: Ports aus der Signatur (Command-Parameter, Rückgabefälle), Vielfachheit der Ausgänge; vorerst nur ein Command-Parameter je `Auf`.
  `Nachrichtenform` (schlicht), `VorDemAggregat`, `Keine`, `ValueTask`, `T?`-Rückgabe entfallen.
- Neuer Graph-Leser (eine Lesart für Generator, Analyzer, Extractor): Knoten aus den Parametern, Kanten aus den Typen, Kopf, Ausgänge, Signatur.
  `GesamtbildAnalyzer`: Empfänger-Regel (Entscheidung 2), Graph-Regeln (Entscheidung 6), Decide ohne Aggregat-Commands.
- Laufzeit: je Instanz ein Graph-Actor auf einem Cluster-Knoten (Ort im Gossip-Zustand), je Knoten ein Parent mit Kindern bis `Parallel`; die
  `Verkettung` wird zur Kantentabelle je Graph (generiert); offene Eingänge nur am Kopf; Ids aus Instanz, Lauf, Knoten, Stelle;
  `FunktionsKindActor` gibt Aggregat-Commands über den Emitter, Commands an Knoten im Graphen weiter, alles andere an seinen Empfänger.
- Instanz-Konfig: Schema je Graph (Generator), `UmgebungKonfig` und Boot-Prüfung (Schema, Akteur ∩ `IDarf`, Command-Kopf einmal).
- Projektstruktur: `Primitive.Folge`, `Zwischenergebnis`, `FunktionsErgebnis` → Command; neu `Primitive.Graph` (`Graphen.cs`); `Lageart`,
  `LageAnalyzer` (CQRS098), docs/07.
- Domäne und Prüfdomäne umziehen: jede heutige Funktion wird Knoten eines Graphen; Folgen und Zwischenergebnisse werden Commands; die
  Anordnungs-Events der Prüfdomäne (`PostenFreigabeAngeordnet`, `KontingentEntlastungAngeordnet`, `ZubuchungenAngeordnet`) werden Commands aus dem
  Decide, die Beleg-Prüfungen in den Antwort-`Auf` entfallen.
- Tests: `FunktionsBausteinGeneratorTests`, `FunktionsLaufzeitTests`, `ZustellwerkTests`, `FolgeGarantieTests`, `FunktionsGarantieTests`,
  `AblaufE2ETests` (Bildkette), Analyzer-Tests, neue Graph-Lesart-Tests; Garantie-Suite.

### Schritt 3 — Join, Je, Sammeln, Rennen, Warten mit Frist; Graph im Graphen (Entscheidungen 5–7, 11)

- `Funktionsform`: mehrere Command-Parameter je `Auf`, Parameterformen `T`, `T?`, `OneOf<…>`, `IReadOnlyList<T>`; Vielfachheits-Prüfung vom
  nächsten gemeinsamen Vorfahren, mit Vorschlag im Befund.
- Laufzeit: Lauf-Pfad je Nachricht, Join-Buchführung je Lauf im Graph-Actor, Fertigmeldung der Erzeuger, `Zeitlimit`, Abbruch der übrigen Zweige
  (Rennen, gescheiterter Lauf), `LaufGescheitert`, Parken des Kopfes.
- Graph als Knoten: Signatur eines Graphen, Konfig-Pfade (`Aufbereitung.Vorschau`), Spur je Lauf mit Pfad.
- Generische Funktionen als Knoten (`Bildverkleinerung<Vorschau>`).
- Domäne: die Bildaufbereitung als Fork-Join (Vorschau ∥ Kontrast ∥ Kacheln → Meldung); Prüfdomäne Verteilung als Je + Sammeln (`SendeJe`/`UndAlle`
  im alten `ProzessBuilder` entfallen, falls noch vorhanden).
- Tests: je Muster ein Laufzeit-Test (Fork-Join, Je + Sammeln, Fork-Join je Element, Rennen mit Abbruch, Warten mit Frist, Wahl + Zusammenführen),
  Absturz mitten im Lauf (Neustart vom Kopf, Ausgänge genau einmal).

### Schritt 4 — Zustand gesichert, Quellen als Katalogfunktionen (Entscheidungen 8, 9)

- `IZustand<T>` gesichert je Instanz und Knoten, nach fertigem Lauf in Reihenfolge; `ZustandsSperre` je Instanz und Knoten; `Abstractions/Zustand.cs`.
- `Abstractions`: Quellfunktion (`Lausche`), Katalog-Commands `DateiGefunden`, `Takt`, `Aufruf`; `IQuellNachricht`, `DateiGemeldet`,
  `TaktGemeldet`, `ClientGemeldet`, `QuellStromArt`, `QuellKonfig` entfallen.
- Laufzeit: `QuellEingang` (Append ins Log, `QuellVorgang`, `QuellKennung`) entfällt; `Dateiwaechter`, `Taktgeber`, `Webhook` unter
  `Infrastructure/Funktionen/Katalog/`; eine Quellinstanz genau einmal im Cluster; `/eingang/quelle` entfällt.
- `QuellenGenerator`, `QuellenTypAnalyzer` (CQRS070) entfallen bzw. gehen in der Graph-Lesart auf; Spur: der Wirker eines Quell-Laufs ist die Instanz.
- Tests: `DateiTreiberTests`, `QuellenE2ETests`, `AblaufE2ETests.Datei_Treiber_meldet…`, zwei Instanzen desselben Graphen auf zwei Verzeichnissen,
  Neustart mit gesichertem Zustand; Garantien docs/11 §11.8 neu.
- Messung (schlank, eine Wiederholung): S1 (Dateiwächter) und S2 (Bildkette) gegen den letzten Stand in docs/12.

### Schritt 5 — Hinweise nur an Akteure draußen (Entscheidung 14)

- `HinweisNetz` zu Funktionen und CQRS106 entfallen; Hinweise aus Funktionen gehen nur noch an den Verteiler; `AggregateActorBase`,
  `SitzungsActor`, `CqrsClientService` ohne Weiterleitung an Funktionen.
- Prüfdomäne Messung: `Merkmal` wird ein Command im Graphen (Fork an Ausreißerprüfung und Schätzer). Garantie „Commands aus einem Hinweis“
  (docs/11 §11.6) entfällt.

### Schritt 6 — Akteure hören, Client als Record (Entscheidung 13)

- `Abstractions/Akteur.cs`: `IMensch`, `IMaschine`, `IKi` entfallen; `IClientVertrag` wird `IClient` (Record, nur `IVerkoerpert<A>`); `IHoert<T>`
  am Akteur, auch für Commands ohne Ziel; `IDarf<IFunktion>` entfällt. Offen 4 entscheiden.
- `GesamtbildAnalyzer` zählt `IHoert<TCmd>` eines Akteurs als Empfänger; `AkteurAnalyzer`, `AkteurRechteGenerator` (ohne `Art`, mit `Hoert` je
  Akteur), `Clientvertragslesart`/`Clientvertragsregeln`, Client-Analyzer CQRS063/064/093–097, `Infrastructure/Akteure/ClientVertrag.cs`,
  `AkteurRechte` (Hören ∩ Token wie Senden).
- Laufzeit: `Werkstatt` nimmt Commands für einen Akteur an und gibt sie einer Sitzung mit passendem „verkörpert ∩ Token“; `HandAusfuehrer` entfällt
  oder wird eine solche Sitzung; Funktions-Schnittstellen (`Funktionsform.Draussen`, `Umsetzt`, `Primitive.HostFunktion`) entfallen.
- Python: `Cqrs.Codegen/PythonFunktionsEmitter` erzeugt je Client die Basis aus den gehörten Commands und Events und dem `IDarf` seiner Akteure.
- Domäne: `Clients.cs` mit Records (`Arbeitsplatz`, `KlassifikationsWorker`, `TrainingsWorker`), ihr `IHoert` wandert an die Akteure;
  `IBildpaarBeurteilung`, `ITraining` werden Commands, die `Klassifizierer` bzw. `TrainingsSystem` hören.
- Werkzeuge, die die Art lesen: `DomainExtractor`, `Vertrag.cs`, `Sicht.Ableitungen` (`ArtRang`), `DomainEditor` (`EditorModell`, `Scaffolder`),
  `editor.js` (`ART_SYM`).
- Tests: Werkstatt, Hand-Kette (`EingangE2ETests`), Python (`tests.sh voll`), Akteur-Rechte, Client-Code-Vertrag, Akteur- und Editor-Tests.

### Schritt 7 — Werkzeuge und Editor-Grundlage

- `GraphExtractor.Kern` (`Lesen`, `Sicht`, `Schreiben`, `ParitaetsPruefung`): Kartenarten Graph (mit Signatur und Instanzen), Funktion (Ports),
  Technik, Akteur (`IDarf`, `IHoert`), Client (verkörperte Akteure); Domänengraph und Graph-Ansicht per Klick; Kanten nur aus Signaturen und
  Basislisten; Gesten: Knoten hinzufügen = Parameter am Graph-Record, Adapter vorschlagen, Instanz anlegen = Konfigdatei.
- `--graph <Typ>`: der Graph als Text (Knoten, Kanten, Vielfachheit, Signatur).
- Spur je Lauf mit Pfad: der Wirker eines Knotens ist seine Karte (Instanz, Knoten), die Spur zeigt den Pfad des Laufs (aus Schritt 3 verschoben).
- Sonde neu schreiben (heute noch das alte Funktionsmodell) und `soll.txt`: alle Muster aus Entscheidung 5, Graph im Graphen, generische Funktion,
  Quellfunktion; `--check`, `--sonde`, `--grammatik` grün.
- `DomainEditor/Grammatik.cs`: Regeln je Entscheidung mit Build-Gegenstück.

### Schritt 8 — Doku und Abnahme

- CLAUDE.md (Invariante 6, tragendes Bild: Funktion, Graph, Quellen, Akteur, Client), docs/03 (Decide-Ausgaben), docs/04 §4.4/§4.7, docs/06,
  docs/07, docs/08 (Blazor), docs/09 (Python), docs/konzept-akteure.md, docs/10 (Grammatik, Tabellen §10.5), docs/11 (§11.6, §11.8), docs/13 §13.0.
- Abnahme: `scripts/server.sh lauf scripts/tests.sh voll`; die Gesamtprüfung nur nach Frage.

## Was bewusst gleich bleibt

Aggregat, Decide-Form, Frist, Inbox, Projektion, Reader, Store-Klassen, direkte Abfrage, offene Eingänge mit Zusteller (jetzt nur am Kopf eines
Graphen), Werkstatt für Arbeit draußen, Verteiler und Sitzung, Command-Ids aus der Eingangs-Kennung.
