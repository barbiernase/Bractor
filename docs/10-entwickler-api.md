# 10 — Entwickler-API: Wie schreibe ich X?

Stand 2026-11-02, gegen den Code geprüft. Dieses Kapitel ist die **eine Quelle für die Grammatik**: welche Bausteine es gibt, wie man sie
schreibt, wer was hört und erzeugt, welche Regel der Build prüft und welche Entscheidungen dahinter stehen. Wie es zur Laufzeit läuft, steht in
[03](03-schreibseite.md) (Schreibseite), [04](04-konsum-und-prozess-maschine.md) (Konsum, Graphen, Uhr) und
[06](06-transport-multinode-betrieb.md) (Transport, Clients); was garantiert ist, in [11](11-garantien.md); wo etwas liegt, in
[07](07-projektstruktur.md). Die Beispiele stammen aus der Sonde (`GraphExtractor.Kern/Sonde/*.cs.txt`), einer fremden Domäne, die jede
allgemeine Primitive einmal benutzt; die Beispieldomäne (Bildpaare, Datensätze, Training) und die Prüfdomäne sind Prüflinge, nicht der Maßstab.

## 10.1 Das Modell in wenigen Sätzen

Das **Log in Postgres ist die einzige Wahrheit.** Geschrieben wird es nur von **Aggregaten** (was sie entschieden haben) — jedes Mal genau einmal.
**Projektionen** lesen das Log ab ihrer eigenen Position, falten Events in ihren Store und geben nichts aus. Gerechnet wird vor dem Aggregat in
**Graphen**: eine **Funktion** ist eine Klasse mit typisierten Ports, ein Graph nennt Funktionen als Knoten, jede Kante steht als
`[Von(nameof(…))]` am nehmenden Knoten, jeder Kopf als `[Kopf]`; ein Graph läuft als **Instanz** mit eigener Konfig, jede Kopf-Nachricht startet eine **Kette**. Eine **Quelle** ist eine Funktion am Kopf eines Graphen,
die lauscht. Zwischen allen fließen **Commands**; was einen Graphen verlässt, wirkt genau einmal über die Inbox seines Ziels. **Akteure** stehen
draußen: sie geben hinein, was sie dürfen, und bekommen, was sie hören — Arbeit oder Kenntnis; ein **Client** ist die Software, die Akteure
verkörpert.

Der Entwickler schreibt nur, *was* fachlich passiert — in der **Signatur** einer Methode und in der Basisliste eines Records. `Auf(VorschauErstellt
vorschau)` heißt „dieser Knoten rechnet, sobald eine Vorschau ankommt“, der Rückgabetyp sagt, was herauskommt; `record
Bildaufbereitung([Kopf] Bildeinlesen Einlesen, [Von(nameof(Einlesen))] Bildverkleinerung Verkleinern, …) : IGraph` sagt, welche Knoten zusammen
rechnen und wer von wem bekommt. *Wie* es läuft (Cursor, Weckruf, Ordnung,
Exactly-once, Partition, offene Eingänge, Ketten, Orte), wählt der Generator aus diesen Code-Fakten; nichts davon taucht im Fachcode auf
(Invariante 5).

| Baustein | tut | Laufzeit |
|---|---|---|
| **Aggregat** | entscheidet: Command rein, Events, Hinweise, Fristen und Commands ohne Ziel raus; einziger Schreiber seines Streams; darf direkt gefragt werden | virtueller Actor je `Id<T>` (Bündel, Inbox-Tabelle, Passivierung, Snapshot) — [03](03-schreibseite.md) |
| **Projektion** | hört Events, schreibt ihren Store über Fähigkeiten, gibt nichts aus | `BatchLeser` je (Projektion, Partition) — [04](04-konsum-und-prozess-maschine.md) §4.1, §4.3 |
| **Reader** | beantwortet Queries über Lese-Fähigkeiten | im Query-Dienst des Hosts |
| **Funktion** | ein Knoten: je `Auf` eine Nachricht rein, gerechnet je Ankunft (ein Event nur als Antwort auf den eigenen Command), Commands und Hinweise raus; optional Zustand und Technik | Parent je Knoten und Instanz, Kinder bis `Parallel` — §4.4 |
| **Graph** | nennt Knoten (Funktionen, Graphen, eine Funktion auch mehrfach); jede Kante als `[Von(nameof(…))]` am nehmenden Knoten, jeder Kopf als `[Kopf]`; läuft als Instanz, je Kopf-Nachricht eine Kette; jede Kante gesichert oder verlierbar | Graph-Actor je Instanz und Cluster-Knoten — §4.4 |
| **Quelle** | eine Funktion mit `Lausche` am Kopf eines Graphen (Katalog: `Dateiwaechter`, `Taktgeber`, `Webhook`) | eine Quellinstanz genau einmal im Cluster, auf ihrem Rechner — §4.7 |
| Store, Technik | Speicher hinter Projektion/Reader bzw. Technik im Konstruktor einer Funktion | – |
| **Akteur**, **Client** | wer was hineingibt und was zu ihm kommt; welche Software für welche Akteure spricht | Sitzung als Actor je Verbindung, Werkstatt und Verteiler je Knoten — [konzept-akteure.md](konzept-akteure.md) |
| *eingebaut:* Uhr, Verteiler | macht aus fälligen Fristen Commands; stellt Pushes und Hinweise an Clients zu | – |

**Warten und Zusammenführen** sind Zustand, keine Form der Eingänge: ein `Auf` nimmt eine Nachricht und rechnet, sobald sie ankommt. Was
zusammengehört, trägt die Nachricht mit; sonst führt der Zustand eines Knotens (`IZustand<T>`) oder ein **Aggregat** zusammen — fremde
Rückmeldungen, alle Elemente zählen, zurücknehmen, eine Frist überwachen: ein Vorgang ist ein Aggregat mit der Vorgangs-Id, Zeit ist eine Frist. Einen Dirigenten, eine Saga oder einen Builder gibt es nicht; die Kanten eines Graphen stehen als
Attribute an seinen Knoten (§10.3.4, §10.3.7).

## 10.2 Nachrichten

| Nachricht | Bedeutung | Garantie | Code |
|---|---|---|---|
| **Command an ein Aggregat** | Absicht an genau ein Aggregat | wirkt genau einmal (Command-Id, Inbox) | `record … : ICommand<TAggregat>` mit `Id<TAggregat> AggregateId`; ein anlegender Command zusätzlich `ICreationCommand` |
| **Command ohne Ziel** | Absicht an genau einen Empfänger, den der Absender nicht nennt: im Graphen die Knoten, die seinen Erzeuger in `[Von]` nennen, sonst den Kopf genau eines Graphen oder einen Akteur, der ihn hört (Arbeit) | am Kopf und als Arbeit durabel bis zum „fertig“ der Kette bzw. bis zur Antwort; zwischen Knoten als Aufruf von Parent zu Parent; nie im Log | `record … : ICommand` |
| **Event** | Fakt im Log, nur aus einem Decide | durabel; wirkt bei jedem Hörer genau einmal | `record … : IEvent` |
| **Hinweis** | flüchtig: Ablehnung, Fortschritt, „nicht lesbar“, Live-Anzeige — nur an Akteure draußen | höchstens einmal, nie im Log | `record … : ITransientEvent` |
| **Query** | Frage, wirkt nicht | synchron | `record … : IQuery` |
| **Antwort** | Rückgabe eines Readers oder Aggregats | trägt den Stand der gezeigten Ids | `record … : IQueryResponse` |
| **Frist** | „schick mir Command C nach Zeit t“ | mindestens einmal gefeuert, genau einmal wirksam | `Frist<TCmd>` als Ausgabe eines Decide |

Es gibt **keine Marken im Log**: ob ein Command gewirkt hat oder abgelehnt wurde, hält die Inbox-Tabelle je Command-Id (eine Wiederholung liefert
dieselbe Antwort). Ist eine Absage für einen Ablauf wichtig, ist sie ein **Fakt** (Event) — das entscheidet der Modellierer über den Typ; sonst
ist sie ein Hinweis an die Sitzung. Kanten ohne eigene Nachricht: **Fähigkeit** (Store-Funktion als Parameter), **Technik** (im Konstruktor einer
Funktion), **Herkunft** (Parameter: Stream, Version, Log-Zeit, Akteur, `NeueId`, §10.4).

## 10.3 Wie schreibe ich …

Rückgaben sind immer ein konkreter Typ, `OneOf<…>` konkreter Typen, `IEnumerable<…>` bzw. `IAsyncEnumerable<…>` (Strom), jeweils auch als `Task<…>`.
**Was entstehen kann, steht nur in der Signatur** (R1); Generatoren, Extractor und Editor lesen keine Rümpfe.

### 10.3.1 … ein Aggregat

Ein Aggregat ist ein `partial` State mit einem `Decider` (entscheiden, fragen) und einem `Applier` (falten). Decider und Applier dürfen in eigenen
Dateien stehen (`partial`).

```csharp
public record MeldeSchaden(Id<Schadensfall> AggregateId, Guid LeserId) : ICommand<Schadensfall>, ICreationCommand;
public record SchadenGemeldet(Guid LeserId) : IEvent;
public record SchadenExistiert(Id<Schadensfall> Id) : ITransientEvent;                // Hinweis: flüchtige Ablehnung
public record SchaetzeSchaden(Id<Schadensfall> Fall, Guid LeserId) : ICommand;           // ohne Ziel: an den Kopf eines Graphen

public partial class Schadensfall : IState
{
    public bool Gemeldet { get; set; }

    public partial class Decider : IDecider<Schadensfall>
    {
        public IEnumerable<OneOf<SchadenGemeldet, SchaetzeSchaden, SchadenExistiert>> Decide(MeldeSchaden c)
        {
            if (this.State.Gemeldet) { yield return new SchadenExistiert(c.AggregateId); yield break; }
            yield return new SchadenGemeldet(c.LeserId);
            yield return new SchaetzeSchaden(c.AggregateId, c.LeserId);
        }
    }

    public partial class Applier : IApplier<Schadensfall>
    {
        public void Apply(SchadenGemeldet e) { this.State.Gemeldet = true; }
    }
}
```

- Je Command ein `Decide`; jedes entschiedene Event hat ein `Apply`, jedes `Apply` ein entscheidendes Decide (R3). Ein Hinweis, eine Frist und ein
  Command haben kein `Apply`.
- **Command ohne Ziel:** ein Decide gibt nie einen Command an ein Aggregat aus (CQRS114) — Aggregate reden nur über Graphen oder Akteure miteinander.
  Sein Empfänger ist der Kopf genau eines Graphen oder ein Akteur, der ihn hört (CQRS116); er geht nicht ins Log und zählt nicht in der Version, je
  Empfänger entsteht im selben Commit ein offener Eingang ([03](03-schreibseite.md) §3.2). Nie zusammen mit einer Ablehnung.
- **Frist:** `yield return new Frist<BrichPruefungAb>(new BrichPruefungAb(c.AggregateId), TimeSpan.FromSeconds(30));` — die Uhr schickt den
  Command zur Zeit an **dasselbe** Aggregat; ist der Vorgang dann fertig, ist er ein Noop (§10.3.7).
- **Direkte Abfrage:** `public OneOf<Leihstand, NichtVerliehen> Frage(HoleLeihstand q) => …` im Decider, synchron, nur lesend. Die Query nennt
  ihr Aggregat mit genau einem Glied `Id<TAggregat>`; der Actor antwortet auf dem committeten Stand, die Antwort zeigt das Aggregat mit seiner
  Version als OCC-Stand ([03](03-schreibseite.md) §3.2). Query und Antwort liegen bei der Schreibseite.
- Keine Uhr, kein Zufall, kein `Guid.NewGuid()`, kein Lesen fremder Daten (R11); keine Collections, die über Vorgänge wachsen.
- Eine Invariante über viele Objekte gehört in **ein** Aggregat: „keine zwei Benutzer mit derselben Adresse“ ist das Aggregat `Adresse` mit einer
  aus der normalisierten Adresse abgeleiteten Id (Sonde `Konten.cs.txt`).

### 10.3.2 … eine Projektion und ihren Store

```csharp
public interface INotiereAusleihe : IWriteStore { Task SchreibeAsync(LeihEintrag eintrag); }      // eine Fähigkeit = eine Funktion
public interface IZaehleOffene : IReadStore { Task<IReadOnlyList<LeihEintrag>> OffeneAsync(Guid leserId); }
public interface ILeihBuch : IStore, INotiereAusleihe, ISchliesseAusleihe, IZaehleOffene { }    // der Store bündelt

public sealed class LeihBuchSpeicher : MartenStore, ILeihBuch { … }                            // Klasse 1 über die Basis

public sealed class Leihliste : IProjektion                                                     // Kennung = Typname
{
    public Task Auf(Ausgeliehen e, Herkunft h, INotiereAusleihe notiere) => …;
}
```

- Form: `Task Auf(Event[, Herkunft], Schreib-Fähigkeiten genau eines Stores[, CancellationToken])`; je Event-Typ höchstens ein `Auf` (R2). Eine
  Projektion gibt nichts aus.
- Fähigkeiten nur als Parameter, nie in Konstruktor oder Feld, nie `new …Store()` (R8). Eine Projektion schreibt in genau einen Store (R9); zwei
  Ziele sind zwei Projektionen.
- **Store-Klasse** (R14): die Implementierung erbt bzw. implementiert genau eine — `IAtomaresZiel` (Klasse 1, Wirkung und Fortschritt in einer
  Transaktion; Basis `MartenStore`), `IWiederholungsfestesZiel` (Klasse 2, jede Schreib-Fähigkeit nimmt die `Herkunft` und schreibt bedingt oder mit
  Event-Identität im Schlüssel; Basis `DateiZiel`), `INurMindestensEinmal` (Klasse 3, nur mit `[MindestensEinmal]` an der Projektion; Basis
  `DateiSink`). Jeder Treiber besteht den allgemeinen Store-Test (`Storetest`, [04](04-konsum-und-prozess-maschine.md) §4.3).
- Ein Read-Model mit `Id<T> Id` ist `partial` (der Generator ergänzt die Speicher-Identität `Schluessel`); in ein **geteiltes** Read-Model
  (`IGeteiltesReadModel`) schreiben mehrere Streams.
- Neuaufbau über Generationen (neues Ziel aufbauen, dann umschalten); für Klasse 1 ist es Leeren.

### 10.3.3 … einen Reader

```csharp
public sealed class Importauskunft : IReader
{
    public async Task<OneOf<ImportstandAntwort, ImportUnbekannt>> Frage(HoleImportstand q, IFindeImportstand finde) => …;
}
```

Form: `Frage(Query, Lese-Fähigkeiten[, CancellationToken])`. Der Reader nennt keine Projektion: dahinter steht, wer in denselben Store schreibt.
Die gezeigten Ids findet der Generator in den `Id<T>`-Feldern von Antwort und Query; der Stand je Id kommt aus dem Fortschritt der Projektion.
Eine Query hat genau ein Ziel — einen Reader **oder** ein Aggregat (R4).

### 10.3.4 … eine Funktion und einen Graphen

```csharp
// Funktionen: Klassen, Ports = Typen (Sonde Gebuehren.cs.txt)
public sealed class Schadensaufnahme : IFunktion                                       // WAHL: genau einer der Fälle je Aufruf
{
    public OneOf<SchadenErfasst, BegutachteVorOrt, NichtSchaetzbar> Auf(SchaetzeSchaden s) => …;
}
public sealed class Schadensschaetzer(IWertTabelle tabelle) : IFunktion                 // FORK, Zweig 1: Technik im Konstruktor
{
    public async Task<SchadenGeschaetzt> Auf(SchadenErfasst e) => …;
}
public sealed class Gutachtenanfrage(GutachtenKonfig konfig) : IFunktion                // FORK, Zweig 2: Konfig-Record (Werte am Knoten)
{
    public async Task<GutachtenErhalten> Auf(SchadenErfasst e) => …;
}
public sealed class MahnungNachSchaetzung : IFunktion                                  // JOIN mit Warten mit Frist
{
    public EroeffneMahnung Auf(SchadenGeschaetzt g, GutachtenErhalten? gutachten, Herkunft h) => new(h.NeueId<Mahnvorgang>(), …);
}

// Graph: Knoten als Parameter, der Kopf als [Kopf], jede Kante als Von am nehmenden Knoten
public sealed record Schadensbewertung(
    [Kopf] Schadensaufnahme Aufnahme,
    [Von(nameof(Aufnahme))] Schadensschaetzer Schaetzung,
    [Von(nameof(Aufnahme))] Gutachtenanfrage Gutachten,
    [Von(nameof(Schaetzung), nameof(Gutachten))] MahnungNachSchaetzung Mahnung) : IGraph;
```

- **Funktion:** eine Klasse `: IFunktion` (eine Schnittstelle mit `IFunktion` ist CQRS100). Je `Auf` genau **eine Nachricht** (ein Command
  ohne Ziel), dazu optional `Herkunft`, Lese-Fähigkeiten und ein `CancellationToken`; mehrere `Auf` sind mehrere Operationen desselben
  Knotens, je Eingangstyp höchstens eines (R2). Ein Event hört eine Funktion nur als **Antwort** auf ihren eigenen Command an ein Aggregat (das Ziel
  entschied es, CQRS108), einen Hinweis nie.
- **Ein Eingang je Auf (GR-EINGANG):** ein `Auf` rechnet je ankommender Nachricht; mehrere Nachrichten, `T?`, `OneOf<…>` oder
  `IReadOnlyList<T>` als Eingang sind CQRS100 mit Hinweis. Zusammenführen: die Nachricht trägt mit, was danach gebraucht wird; sonst je Nachricht
  ein `Auf` und der Zustand des Knotens hält die erste bis zur zweiten; was fachlich zählt (alle Elemente, fremde Rückmeldungen), zählt ein
  Aggregat. Ein Ausgang `T` liefert genau einen, ein `OneOf`-Fall keinen oder einen, ein Strom keinen bis viele — je Element eine Nachricht.
  Folge, Gabel, Wahl und Je sind so Formen der Ausgaben; Zusammenführen, Sammeln, Rennen und Warten stehen im Logik-Code.
- **Ausgaben:** Commands und Hinweise — kein Event, kein `void`, kein `T?`, kein `ValueTask`; ein leerer Strom heißt „nichts“ (CQRS100). Ein Strom
  (`IAsyncEnumerable`): jede Ausgabe geht hinaus, bevor die nächste kommt.
- **Graph:** `record X(Knoten …) : IGraph` in `Graphen.cs`; jeder Parameter ist ein Knoten (Name = Konfig-Schlüssel, Typ = eine Funktion — eine
  generische mit Typargument — oder ein Graph), die Reihenfolge bedeutet nichts; dieselbe Funktion darf mehrfach stehen (je Knoten eigene Konfig).
  **Kanten** stehen am nehmenden Knoten: `[Von(nameof(A), nameof(B))]` nennt die Knoten, von denen er bekommt, und jede genannte Quelle speist die
  Ports, deren Typ sie ausgibt (mindestens einen; nur `nameof` eines anderen Parameters, kein Name doppelt). Ein **Kopf** trägt `[Kopf]` und nimmt
  von außen, was keine genannte Quelle speist — je Eingangstyp genau einer; ein Knoten darf beides tragen. Jeder Knoten trägt eins davon, nur eine
  Quelle keins. Was ein Knoten ausgibt und kein Knoten über `Von` von ihm nimmt, verlässt den Graphen. Kreisfrei über die Knoten (eine Kette `Bild → Bild → Bild` ist kein Kreis); ein Graph im Graphen ist eine Schachtel mit seiner
  Signatur — `Von` an ihm speist seine Köpfe, `[Kopf]` an ihm macht sie zu Köpfen des äußeren, `Von(nameof(Schachtel))` nimmt seine Ausgänge
  (CQRS115: GR-GRAPH, GR-KANTE, GR-KOPF). Eine Funktion in keinem Graphen läuft nie (CQRS117). Passt ein Typ nicht, ist der Adapter eine
  eigene kleine Funktion — Klebecode zwischen Knoten gibt es nicht. Zeigen: `dotnet run --project GraphExtractor -- --graph <Typ>`.
- **Genau einmal** ohne Festhalten: der Kopf einer Kette ist ein offener Eingang, bis ihr „fertig“ über die gesicherten Kanten zurückkommt; rechnet
  ein Knoten erneut, wirkt von jedem Command der erste (Ids aus der Nachricht, dem Knoten und der Stelle; die Inbox des Ziels verwirft das Zweite).
  Eine verlierbare Kante (`[Von(nameof(A), Verlierbar = true)]`) hält den Kopf nicht auf; was auf ihr verloren geht, kommt nicht wieder. Eine Funktion, die draußen
  etwas bewirkt (eine Datei schreibt), muss Wiederholung vertragen.
- **Neue Ids** nur abgeleitet: `h.NeueId<T>()` bzw. `h.NeueId<T>(zweck)` (R13).
- **Technik** (R10): jede Schnittstelle oder Klasse im Konstruktor einer Funktion, die weder Konfig-Record noch `ILogger<T>` noch `IZustand<T>` ist —
  ohne Marker, nur im Konstruktor (CQRS089), bindbar (CQRS090–092). Sie läuft im Prozess ihres Halters, lädt beim ersten Aufruf und wird über
  `IAsyncDisposable` freigegeben; bei mehreren Umsetzungen wählt der Knoten der Instanz (`Technik`).
- **Zustand** (R19): `IZustand<T>` im Konstruktor einer Funktion oder einer Technik-Umsetzung; sonst keine veränderlichen Felder (CQRS105). Er ist
  gesichert je Instanz und Knoten, wenn der Aufruf und alle früheren beendet sind, mit ihren Kennungen (nach einem Absturz zählt nichts
  doppelt); eine Instanz mit Zustand rechnet im Cluster auf genau einer
  Aktivierung. Was fachlich gelernt wird, gehört in eine Projektion, die Funktion liest es über eine Lese-Fähigkeit.

  ```csharp
  public sealed class Prognosemodell(IZustand<Gewichte> gewichte, IStatistik statistik) : IFunktion      // Sonde Vorhersage.cs.txt
  {
      public IEnumerable<OneOf<VermerkePrognose, Unwetter>> Auf(Geglaettet<Kurzfenster> kurz) => …;   // hält kurz im Zustand,
      public IEnumerable<OneOf<VermerkePrognose, Unwetter>> Auf(Geglaettet<Langfenster> lang) => …;   // rechnet, wenn beide da sind (Wahl)
  }
  ```

### 10.3.5 … eine Instanz und eine Quelle

Ein Graph läuft erst als **Instanz**: eine Datei je Instanz in der Umgebung, `umgebungen/<name>/graphen/<instanz>.json`, mit `Graph`, bei einem
Quellkopf dem `Akteur` (er gibt hinein, was den Graphen verlässt, und muss es dürfen) und je Knoten (Pfad bei inneren Graphen) die Felder seiner
Konfig-Records sowie `Parallel`, `Warteschlange`, `Wiederholungen`, `Vorwaermen`, `Zeitlimit`, `Technik`. Dieselbe Blaupause läuft beliebig oft;
ein Graph mit Command-Kopf läuft je Umgebung genau einmal. `Rechner` legt die Instanz auf einen Host fest (seinen `Cluster:Rechner`), etwa eine
Quelle an ihre Steuerung oder ein Modell auf seine GPU; mit Zustand oder Quelle ist er im Cluster Pflicht, ohne läuft die Instanz auf jedem Host. Der Start prüft jede Instanz gegen ihren Graphen ([04](04-konsum-und-prozess-maschine.md)
§4.4).

Eine **Quelle** ist eine Funktion mit `Lausche(CancellationToken)` statt `Auf`, am Kopf ihres Graphen (sie ist dann sein einziger Kopf, CQRS115).
Das Framework bringt einen Katalog mit (`Core/Katalog`: `Dateiwaechter` → `DateiGefunden`, `Taktgeber` → `Takt`, `Webhook` → `Aufruf`); die Domäne
nennt ihn als Knoten (`Katalogimport(Dateiwaechter Eingang, Katalogeingang Annahme, …) : IGraph`, Sonde `Katalogimport.cs.txt`). Eine eigene Quelle
schreibt man genauso:

```csharp
public sealed class Sensorabfrage(SensorabfrageKonfig konfig, IZustand<Abfragestand> stand, ISensorbus bus) : IFunktion   // Sonde Messnetz.cs.txt
{
    public async IAsyncEnumerable<Rohwert> Lausche([EnumeratorCancellation] CancellationToken ct) { … }
}
public sealed record Messkette(Sensorabfrage Abfrage, Messwertaufbereitung Aufbereitung) : IGraph;
```

Was eine Quelle meldet, steht nicht im Log; Doppeltes vermeiden und nach einem Neustart weitermachen regelt sie über ihren gesicherten Zustand
(Gesehen-Liste, Sequenznummer, Hochwassermarke) — jedes Element startet eine Kette.

### 10.3.6 … Akteure und Clients

```csharp
// Sonde Lesen.cs.txt
public sealed record Theke : IAkteur, IDarf<LeiheAus>, IDarf<GibZurueck>, IDarf<OffeneAusleihen>, IDarf<RueckgabeGescannt>,
    IHoert<NichtAusgeliehen>, IHoert<NichtSchaetzbar>, IHoert<LaufGescheitert>;              // Kenntnis: Hinweise
public sealed record Archiv : IAkteur, IHoert<BegutachteVorOrt>, IDarf<EroeffneMahnung>, IHoert<Ausgeliehen>;   // Arbeit + Antwort + Kenntnis

public sealed record Lesesaal : IClient, IVerkoerpert<Theke>, IVerkoerpert<Archiv>;
```

Alles, was hineinkommt, kommt von einem Akteur, der es darf (`IDarf<T>`: Command, Query, Hinweis; R16). Was zu ihm kommt, hört er (`IHoert<T>`):
ein Command ohne Ziel ist **Arbeit** — die Werkstatt gibt ihn einer Sitzung, die den Akteur verkörpert, bis eine Antwort kommt (die erste gewinnt;
die Antwort sind Commands seines `IDarf`) —, ein Event oder Hinweis ist **Kenntnis** (verlierbar, ohne Ausgabe). Ein **Client** ist ein Record, der
nur nennt, wen er verkörpert; über seine Leitung gehen genau `IDarf` und `IHoert` dieser Akteure. Wirksam ist „verkörpert ∩ Token“, für beide
Richtungen (R17); ein C#-Client erklärt sich mit `[assembly: IstClient<X>]`. Ausführlich: [konzept-akteure.md](konzept-akteure.md).

### 10.3.7 … einen Ablauf

Ein Ablauf über mehrere Aggregate ist ein **Vorgangs-Aggregat** plus **Graphen** als Kanten (Sonde `Fluss.cs.txt`): ein Akteur gibt einen Command
ohne Ziel an den Kopf des Graphen (`RueckgabeGescannt`); der Start legt den Vorgang an (`StartePruefung` mit `h.NeueId<Seitenpruefung>()`) und hört
dessen Event als Antwort auf seinen Command; die Antwort gibt als Strom je Seite eine Nachricht (Je), der Prüfer meldet jede Seite dem Vorgang
(`VermerkeSeite`), der mitzählt und nach der letzten abschließt; eine Frist desselben Aggregats bricht nach Zeit ab. Was zusammengeführt, gezählt
oder zurückgenommen wird (fremde Rückmeldungen, Zählung, Kompensation), entscheidet das Aggregat — es gibt dazu Commands ohne Ziel an Graphen aus. Die Zugehörigkeit fremder Events zum Vorgang ist ein
**fachliches Feld** im Event (A2). Kompensation ist ein Decide, das den Gegenzug als Command ohne Ziel ausgibt.

## 10.4 Typisierte Ids und Herkunft

`Id<TAggregat>` ist das Ziel jedes Commands an ein Aggregat und der Verweis auf ein Aggregat überall sonst (Events, Commands ohne Ziel, Queries,
Antworten, Read-Models, Fähigkeiten). Wirkung: Routing über Typen ohne Konvention (Invariante 3); Antworten sagen, was sie zeigen (der Stand jeder
gezeigten Id kommt ohne Zutun mit); Editor und LLM sehen, worauf ein Feld zeigt. Auf Leitung, Log und Wire ist `Id<T>` eine UUID (Namensregel
`Abstractions/Idvertrag.cs`). Command-Ids leitet das Framework ab, nie der Entwickler.

Die **Herkunft** (`Abstractions/Herkunft.cs`) ist der Parameter, über den ein Baustein von seinem Eingang erfährt: `Stream`, `Version`, `Zeit`
(Log-Zeit, nie die Uhr des Hosts; aus einer Quelle die Zeit ihres Elements), `Akteur`, `Id<T>()` (der Stream als Id eines Aggregats) und
`NeueId<T>(zweck)` — dieselbe Eingabe ergibt beim Wiederholen dieselbe Id. Ein Client darf neue Ids würfeln: er hält die Command-Id vor dem Senden
fest (Ausgangskorb).

## 10.5 Wer hört, wer erzeugt

Die Tabellen folgen der Grammatik (`DomainEditor/Grammatik.cs`, `Konsume` und `Erzeugungen`).

| Nachricht | darf in | Anzahl |
|---|---|---|
| Command an ein Aggregat | das Aggregat in seinem Typ | genau eins |
| Command ohne Ziel | die Knoten desselben Graphen, die ihn nehmen; sonst den Kopf genau eines Graphen oder einen Akteur (`IHoert`, Arbeit) | genau ein Empfänger |
| Event | Projektion; Akteur (`IHoert`, Kenntnis); eine Funktion nur als Antwort auf ihren eigenen Command | beliebig viele |
| Event, vom Aggregat entschieden | sein `Apply` | genau das entscheidende Aggregat |
| Hinweis | Akteur draußen (`IHoert`, über den Verteiler an seine Sitzungen) | beliebig viele; nie Funktion, Projektion, Aggregat |
| Query | Reader **oder** Aggregat | genau eins |
| Frist | Uhr → Command an **dasselbe** Aggregat | genau eins |
| Fähigkeit | Projektion (schreibend), Reader und Funktion (lesend) | beliebig viele |
| Technik | Funktion (im Konstruktor) | beliebig viele |
| Antwort | der Fragende | – |

| Baustein | erzeugt | sonst nichts |
|---|---|---|
| Aggregat | Events, Hinweise, Fristen, Commands ohne Ziel; Antworten auf direkte Queries | keine Commands an Aggregate |
| Projektion | Wirkungen in ihrem Store (nur Schreib-Fähigkeiten) | keine Nachricht |
| Reader | Antworten (nur Lese-Fähigkeiten) | – |
| Funktion | Commands (an Aggregate oder ohne Ziel), Hinweise; nach außen nur über Technik | keine Events |
| Akteur, Client | was die Akteure dürfen (`IDarf`): Commands, Queries, Hinweise; auf Arbeit Commands | – |
| Uhr | Commands aus fälligen Fristen | – |

## 10.6 Regeln und ihr Gegenstück im Build

Prüfen heißt Bauen (W5). Die Liste „Regel → Build-Gegenstück“ der Grammatik druckt `dotnet run --project GraphExtractor -- --grammatik` (aus
`DomainEditor/Grammatik.cs`); alle IDs mit Ort stehen in [05](05-generatoren-analyzer-proto.md) §5.3, die Garantien dazu in
[11](11-garantien.md) §11.10. Code-Kommentare zitieren die Regeln mit ihrer Nummer.

| # | Regel | geprüft durch |
|---|---|---|
| R1 | Der Ausgang steht in der Signatur (`Decide`, `Auf`, `Lausche`, `Frage`), die Knoten eines Graphen mit `[Kopf]` und `[Von]` in seinem Record; der Graph entsteht nur daraus, nie aus Rümpfen | CQRS050, CQRS100, CQRS057, CQRS115 |
| R2 | Je Baustein und Eingangstyp höchstens ein `Auf` bzw. eine `Frage` | CQRS101 |
| R3 | Ein Command an ein Aggregat gehört genau einem Aggregat (`ICommand<TAggregat>`); jedes entschiedene Event hat ein `Apply` im selben Aggregat und umgekehrt; ein Decide gibt keinen Command an ein Aggregat aus | Compiler, CQRS010/011, CQRS079/080, CQRS114 |
| R4 | Eine Query hat genau ein Ziel (Reader oder Aggregat) | CQRS084/086, CQRS057 |
| R5 | Eine Frist steht nur in einem Decide und zielt auf einen Command des eigenen Aggregats | Generator (`Frist<TCmd>`), Prüfung beim Schreiben im Actor |
| R6 | Ein Kreis Graph → Command → Aggregat → Command ohne Ziel → derselbe Graph endet nur, wenn das Decide fortschreitenden Zustand trägt | **nicht im Build** (aus Signaturen nicht von einem Ablauf zu unterscheiden, GR-KREIS, docs/13 FB-5); der Test-Ablauf bricht einen Kreis ab (`Cqrs.Testing/Ablauf.cs`) |
| R7 | Ids sind typisiert; Command-Ziele sind `Id<T>` | Compiler, CQRS013 |
| R8 | Fähigkeiten nur als Parameter (eine Funktion je Fähigkeit, ein Store bündelt), nie in Konstruktor, Feld oder `new` | CQRS051, CQRS054, CQRS055 |
| R9 | Eine Projektion schreibt in genau einen Store | CQRS052 |
| R10 | Technik nur im Konstruktor einer Funktion bzw. einer Umsetzung, bindbar und eindeutig gewählt | CQRS089–092, Start-Prüfung (`BindeTechnik`) |
| R11 | Aggregate und Projektionen rechnen bei gleicher Eingabe gleich: keine Uhr, kein Zufall, kein `Guid.NewGuid()`, kein Lesen fremder Daten | CQRS102 |
| R12 | Eine Funktion ist eine Klasse, liest nie das Log und gibt nie ein Event aus: Ports sind Commands (ein Event nur als Antwort auf den eigenen Command), Ausgaben Commands und Hinweise | CQRS100, CQRS108 |
| R13 | Neue Ids auf der Server-Seite nur abgeleitet (`Herkunft.NeueId`) | CQRS104 |
| R14 | Jeder Store implementiert genau eine Store-Klasse; Klasse 3 nur mit `[MindestensEinmal]`; Schreib-Fähigkeiten eines Klasse-2-Stores nehmen die `Herkunft` | CQRS109, CQRS110, Start-Prüfung; Einhaltung: allgemeiner Store-Test |
| R15 | Die Garantie folgt dem Eingang: Event → genau einmal, Hinweis → höchstens einmal, Query → keine Wirkung | folgt aus den Typen |
| R16 | Alles von draußen kommt von einem Akteur, der es darf | Generator, Start-Prüfung, Akteur-Tor |
| R17 | Ein Client sendet, fragt, hört und nimmt Arbeit nur für die Akteure an, die er verkörpert, geschnitten mit dem Token | CQRS063/064, CQRS093–097, Handshake |
| R18 | Die Hausregel für Actors: ein Weg je Absicht (nur Framework-Code) | CQRS112/113 |
| R19 | Zustand einer Funktion nur über `IZustand<T>` im Konstruktor | CQRS105, CQRS100 |
| R20 | Ein Command ohne Ziel hat genau einen Empfänger (im Graphen die Knoten, die seinen Erzeuger in `Von` nennen; sonst Graph-Kopf oder Akteur); ein Graph ist kreisfrei über seine Knoten, jeder Knoten trägt `[Kopf]` oder `[Von(nameof(…))]`, je Eingangstyp ein Kopf, ein `Auf` nimmt genau eine Nachricht (CQRS100); jede Instanz passt zu ihrem Graphen | CQRS116, CQRS115, CQRS117 (Warnung), Start-Prüfung (`GraphInstanzen.Aus`) |

Dazu die Projektstruktur ([07](07-projektstruktur.md), CQRS098): wo eine Primitive liegt, sagt die Tabelle `Abstractions/Projektstruktur.cs`.

## 10.7 Entscheidungen und Annahmen

Entschieden von Tobi; Code-Kommentare zitieren sie mit ihrer Nummer.

| # | Frage | Entscheidung |
|---|---|---|
| E1 | Geltungsbereich (Mandant, Daten-Rechte je Objekt) | nicht jetzt: im Zielbetrieb (interne Produktionsfirma) darf jeder alles; Akteur-Rechte nach Typ bleiben. Der Platz ist frei (§10.8). |
| E2 | Direkte Abfrage eines Aggregats | ja, als eigener Lesepfad neben dem Reader (§10.3.1) |
| E3 | Typisierte Ids | ja, Pflicht für Command-Ziele; in Antworten die Grundlage für feine Updates an Clients (§10.4) |
| E4 | Editor-Oberfläche | eine Fläche in sechs Spalten in der Reihenfolge des Ablaufs, Domänen als Bänder, Nachrichten als Ports; Graph, Aggregat und Store von innen als Modal; gerechnet in der Sicht (§10.9) |
| E5 | Umsetzen | schrittweise, je Schritt auf Tobis Wort |
| E6 | Darf eine Funktion Zustand haben? | ja, gekapselt, kein Domänen-Zustand (Gewichte, ein Zähler, eine Gesehen-Liste); `IZustand<T>` im Konstruktor, gesichert je Instanz und Knoten nach fertigem Lauf; Gelerntes gehört in Projektionen |
| E7 | Was fließt zwischen Funktionen? | ein Command ohne Ziel, als Aufruf von Schritt zu Schritt, nie im Log (Invariante 6); kein eigener Nachrichten-Marker |
| E8 | Wie kommt er an? | im Graphen über die Kanten, die als `[Von(nameof(…))]` am nehmenden Knoten stehen; zwischen den Bausteinen über den Typ mit genau einem Empfänger (generierte Graph-Tabelle); an einen Kopf über einen offenen Eingang oder den Boten; kein Broker, keine Abos |
| E9 | C# und Python | eine Funktion läuft im Host; was draußen gerechnet wird, ist Arbeit eines Akteurs, die ein Client annimmt; denselben Schritt je Umgebung zwischen Host und draußen umzuschalten ist bewusst aufgegeben |
| E10 | Lebenszyklus einer Funktion | kein eigener: Technik lädt beim ersten Aufruf, Freigabe über `IAsyncDisposable` |
| E11 | Nachrichtenarten | drei: Command (mit oder ohne Ziel), Event, Hinweis; dazu Query und Frist |
| E12 | Empfänger eines Commands | genau einer, vom Absender aus: Ziel im Typ → Aggregat; sonst der Knoten desselben Graphen; sonst der Kopf genau eines Graphen oder ein Akteur, der ihn hört |
| E13 | Graph | ein eigener Typ (`IGraph`), Knoten als Parameter mit `[Kopf]` und `[Von(nameof(…))]` (ausdrückliche Kanten, eine Funktion auch mehrfach; aus Typen allein entsteht innen keine Kante), Ablaufformen als Formen der Ausgaben (Zusammenführen im Logik-Code); läuft als Instanz mit eigener Konfig, beliebig oft |
| E14 | Quellen | Katalogfunktionen am Kopf eines Graphen, nicht im Log; durabel über den gesicherten Zustand |
| E15 | Umgebung | ein Ordner (`umgebung.json` + `graphen/<instanz>.json`); der Akteur einer Quellinstanz steht in ihrer Datei, geprüft gegen sein `IDarf` |
| E16 | Akteur und Client | Akteur = `IDarf` + `IHoert` (ohne Art); Client = Record, der nur verkörpert; ein C#-Client nennt ihn mit `[assembly: IstClient<X>]`; Hinweise gehen nur an Akteure |

Annahmen (sicherer Default, gelten, solange Tobi nicht widerspricht):

- **A1** Eine Funktion hält nichts fest: genau einmal wirkt eine Kette über den offenen Eingang an ihrem Kopf und die abgeleiteten Command-Ids.
- **A2** Die Zugehörigkeit zu einem Vorgang ist ein fachliches Feld im Event, keine versteckte Korrelation in den Metadaten.
- **A3** Wiederholungsfeste Stores (Klasse 2) schreiben mit der Event-Identität im Schlüssel oder bedingt nach Position.
- **A4** Der Neuaufbau einer Projektion geht über Generationen; Leeren ist der Sonderfall.
- **A5** Eine Instanz mit Zustand oder Quelle rechnet im Cluster auf genau einem Host: dem Rechner, den sie nennt (`Rechner` in ihrer Datei,
  `Cluster:Rechner` des Hosts; Pflicht im Cluster). Fällt er aus, zieht nichts um, die durablen Eingänge warten.
- **A6** Jeder Cluster-Knoten hat von jeder Instanz einen Graph-Actor `funktionen/<instanz>` mit je Knoten einem Parent; eine Kette bleibt auf dem Rechner, der ihren Kopf annahm; ein
  Eingang geht an den eigenen Knoten, sonst reihum (Gossip).
- **A7** Ein Hinweis trägt eine Kennung, die der Absender vergibt.
- **A8** Arbeit gibt die Werkstatt dem Arbeiter mit dem meisten freien Platz; einen Zustand im Worker sichert der Worker.

## 10.8 Was bewusst nicht kommt

Kein Geltungsbereich und keine Daten-Rechte je Objekt (E1); der Platz bleibt frei: die `Herkunft` trägt den Akteur, `Decide` darf ihn sehen; ein
Bereich wäre später eine weitere Kopfzeile plus Filter in Verteiler, Reader und Lesen ab Position. Kein Löschen- und Aufbewahren-Konzept (Option:
verschlüsselbare Felder als Typ-Fakt). Keine Schema-Evolution im Editor; `IUpcast` bleibt Backend-Primitive. Kein Dirigent, keine Saga, kein
Builder und keine Kantenliste (die Kanten stehen als `Von` an den Knoten), keine Generics als Verdrahtung, keine Knotenarten, keine Reaktion als eigene Art, keine Schleifen im Graphen. Offenes steht in
[13](13-reifegrad-schulden-bewertung.md) §13.0.

## 10.9 Der Editor

Der Editor der Werkbank (`Werkbank/wwwroot/editor/`) ist ein Knoten-Editor; was er zeigt, rechnet der Server in der Sicht
(`GraphExtractor.Kern/Sicht/`, W3), er ordnet nur an und zeichnet.

- **Die Fläche** (`board.sicht.flaeche`, `Sicht.Flaeche.cs`) ordnet nach dem Ablauf, von links nach rechts: Client-Ausgang (je Akteur, was er darf),
  Quellen (die Quellknoten der Graphen), Graphen (nur die obersten, mit ihrer Signatur), Aggregate, Projektionen (Projektion und Reader je Store
  gruppiert), Client-Eingang (je Akteur, was er hört, dazu die Antworten auf seine Queries). Ein Akteur steht also zweimal. Die Domänen sind Bänder
  über Quellen bis Projektionen; das Band eines Graphen sind die Aggregate, an die er schreibt.
- **Nachrichten sind Ports** an den Karten, mit Sorte (Command, Event, Hinweis, Query, Antwort, Frist). Deklariert wird ein Typ am Port seines
  Besitzers (`Sicht.Besitz`).
- **Klick und Doppelklick:** ein Klick auf eine Karte oder einen Port hebt seine Kette bzw. seine Kanten hervor; ein Doppelklick öffnet ihn im
  Modal — Graph, Aggregat und Store von innen, ein Glied (Decide, Auf, Projektion, Reader, eine Nachricht des Aggregats) in seinem Block gewählt,
  alles andere (Akteur, Client, Instanz, Umgebung, Funktion) als Formular. Ein Panel neben der Fläche gibt es nicht; Brotkrumen führen zurück.
- **Kanten** laufen von Port zu Port und haben eine Art: schreiben, Quelle, lesen, Kenntnis, Arbeit, Rücklauf (ein Command ohne Ziel aus dem
  Decide an einen Graph-Kopf, die Event-Antwort an die Funktion, die den Command gab), liest (eine Lese-Fähigkeit am Store).
- **Von innen** (Modal): ein Graph zeigt seine Komposition (`board.sicht.komposition`, `Sicht.Komposition.cs`: Knoten nach Tiefe, je `[Von]` eine
  Kante, Köpfe und Ausgänge, Technik und Konfig als Chips) mit dem Baukasten (`board.sicht.baukasten`) und seinen Instanzen; ein Aggregat
  Commands → Decide → Ergebnisse → Apply → Zustand; ein Store Events und Queries → Auf bzw. Frage → Store-Funktionen → Read-Models. Bearbeitet wird
  im Modal mit dem Formular des gewählten Glieds.
- **Gesten** sind Änderungen am Board, die der Server als Patch in den Code schreibt (↶ nimmt ihn zurück): vom Akteur auf eine Karte ziehen
  (`IDarf`), einen Ausgang auf einen Akteur (`IHoert`), ein Event auf eine Projektion (ein `Auf`), Klick auf eine Kante zum Lösen; im Modal
  Funktion, Graph oder Quelle aus dem Baukasten auf die Leinwand (ein Knoten), Technik oder Konfig auf einen Knoten (Konstruktor), Ausgang auf
  Knoten (`[Von]`), „＋ Kopf“ auf Knoten (`[Kopf]`), Klick auf eine Kante (verlierbar, lösen). Was aus Signaturen folgt, ändert man im Formular.
- `--check` prüft die Fläche mit (Kanten an vorhandenen Ports, je Typ höchstens ein deklarierender Port).
