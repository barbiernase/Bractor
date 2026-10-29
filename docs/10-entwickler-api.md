# 10 — Entwickler-API: Wie schreibe ich X?

Stand 2026-10-29, gegen den Code geprüft. Dieses Kapitel ist die **eine Quelle für die Grammatik**: welche Bausteine es gibt, wie man sie
schreibt, wer was hört und erzeugt, welche Regel der Build prüft und welche Entscheidungen dahinter stehen. Wie es zur Laufzeit läuft, steht in
[03](03-schreibseite.md) (Schreibseite), [04](04-konsum-und-prozess-maschine.md) (Konsum, Funktionen, Uhr) und
[06](06-transport-multinode-betrieb.md) (Transport, Clients); was garantiert ist, in [11](11-garantien.md); wo etwas liegt, in
[07](07-projektstruktur.md). Alle Beispiele stammen aus der Sonde (`GraphExtractor.Kern/Sonde/*.cs.txt`), einer fremden Domäne, die jede
allgemeine Primitive einmal benutzt; die Beispieldomäne (Bildpaare, Datensätze, Training) und die Prüfdomäne sind Prüflinge, nicht der Maßstab.

## 10.1 Das Modell in wenigen Sätzen

Das **Log in Postgres ist die einzige Wahrheit.** Geschrieben wird von **Aggregaten** (was sie entschieden haben), **Quellen** (was von draußen
kam) und **Funktionen** (ihre festgehaltene Ausgabe) — jedes Mal genau einmal. Alles andere **liest das Log ab seiner eigenen Position**:
**Projektionen** falten Events in ihren Store und geben nichts aus; **Funktionen** nehmen einen Eingang und geben aus, was sie wollen — sie sind
Actors mit optional gekapseltem Zustand und tauschen untereinander auch **ohne Log** Hinweise aus; was fachlich zählt, verlässt dieses Netz als
Command an ein Aggregat. **Clients** hängen am Rand: sie schicken Commands, Quell-Nachrichten, Hinweise und Queries hinein und bekommen über
einen Verteiler je Knoten mit, was sie hören dürfen.

Der Entwickler schreibt nur, *was* fachlich passiert — in der **Signatur** einer Methode. `Auf(AlarmAusgeloest e)` in einer Funktion heißt „diese
Funktion hört dieses Event“, der Rückgabetyp sagt, was herauskommt. *Wie* es läuft (Cursor, Weckruf, Ordnung, Exactly-once, Partition,
Festhalten, Laufort), wählt der Generator aus dieser Signatur; nichts davon taucht im Fachcode auf (Invariante 5).

| Baustein | tut | Laufzeit |
|---|---|---|
| **Aggregat** | entscheidet: Command rein, Events, Hinweise und Fristen raus; einziger Schreiber seines Streams; darf direkt gefragt werden | virtueller Actor je `Id<T>` (Bündel, Inbox-Tabelle, Passivierung, Snapshot) — [03](03-schreibseite.md) |
| **Projektion** | hört Events, schreibt ihren Store über Fähigkeiten, gibt nichts aus | `BatchLeser` je (Projektion, Partition) — [04](04-konsum-und-prozess-maschine.md) §4.1, §4.3 |
| **Reader** | beantwortet Queries über Lese-Fähigkeiten | im Query-Dienst des Hosts |
| **Funktion** | ein Eingang (Event aus dem Log oder Hinweis im Speicher), beliebige Ausgaben (Commands, Events, Hinweise); optional Zustand und Ressourcen | `FunktionsActor` je (Funktion, Partition) bzw. einer je Funktion; Laufort Host, draußen (getragen von einem Client) oder Hand — §4.4 |
| **Quelle** | fester Treiber und Konfig bringen Nachrichten genau einmal ins Log | ein Treiber je Quelle im Cluster (Lease) — §4.7 |
| Store, Dienst | Technik hinter Projektion/Reader bzw. Funktion | – |
| Akteur, Client | wer welche Typen darf; welche Software für welche Akteure spricht | Sitzung als Actor je Verbindung, Verteiler je Knoten — [konzept-akteure.md](konzept-akteure.md) |
| *eingebaut:* Uhr, Verteiler | macht aus fälligen Fristen Commands; stellt Pushes und Hinweise an Clients zu | – |

**Warten** (Join, „alle N“, Zeitlimit, Kompensation) ist Zustand über mehrere Ereignisse und gehört in ein **Aggregat**: ein Vorgang ist ein
Aggregat mit der Vorgangs-Id, Fan-in zählt ein Zähl-Aggregat, Zeit ist eine Frist. Einen Dirigenten, eine Saga oder eine Fluss-Sprache gibt es
nicht (§10.3.7).

## 10.2 Nachrichten

| Nachricht | Bedeutung | Garantie | Code |
|---|---|---|---|
| **Command** | Absicht an genau ein Aggregat | wirkt genau einmal (Command-Id, Inbox) | `record … : ICommand<TAggregat>` mit `Id<TAggregat> AggregateId`; ein anlegender Command zusätzlich `ICreationCommand` |
| **Event** | Fakt im Log | durabel; wirkt bei jedem Hörer genau einmal | `record … : IEvent` |
| **Quell-Nachricht** | Event von draußen (Treiber oder Client) | genau einmal je Kennung | `record … : IQuellNachricht` mit `Kennung`, oder abgeleitet von einer Treiber-Basis (`DateiGemeldet`, `TaktGemeldet`) |
| **Hinweis** | flüchtig: Ablehnung, Fortschritt, Messwert, Live-Anzeige | höchstens einmal, nie im Log | `record … : ITransientEvent` |
| **Query** | Frage, wirkt nicht | synchron | `record … : IQuery` |
| **Antwort** | Rückgabe eines Readers oder Aggregats | trägt den Stand der gezeigten Ids | `record … : IQueryResponse` |
| **Frist** | „schick mir Command C nach Zeit t“ | mindestens einmal gefeuert, genau einmal wirksam | `Frist<TCmd>` als Ausgabe eines Decide |

Es gibt **keine Marken im Log**: ob ein Command gewirkt hat oder abgelehnt wurde, hält die Inbox-Tabelle je Command-Id (eine Wiederholung liefert
dieselbe Antwort). Ist eine Absage für einen Ablauf wichtig, ist sie ein **Fakt** (Event) — das entscheidet der Modellierer über den Typ; sonst
ist sie ein Hinweis an die Sitzung. Kanten ohne eigene Nachricht: **Fähigkeit** (Store-Funktion als Parameter), **Dienst** (im Konstruktor einer
Funktion), **Herkunft** (Parameter: Stream, Version, Log-Zeit, Akteur, `NeueId`, §10.4).

## 10.3 Wie schreibe ich …

Rückgaben sind immer ein konkreter Typ, `OneOf<…>` konkreter Typen, `T?` („nichts“), `IEnumerable<…>` bzw. `IAsyncEnumerable<…>` (Strom),
jeweils auch als `Task<…>`. **Was entstehen kann, steht nur in der Signatur** (R1); Generatoren, Extractor und Editor lesen keine Rümpfe.

### 10.3.1 … ein Aggregat

Ein Aggregat ist ein `partial` State mit einem `Decider` (entscheiden, fragen) und einem `Applier` (falten). Decider und Applier dürfen in eigenen
Dateien stehen (`partial`).

```csharp
public record MeldeMesswert(Id<Sensor> AggregateId, double Wert, DateTimeOffset GemessenAm) : ICommand<Sensor>;
public record AlarmAusgeloest(double Wert, DateTimeOffset GemessenAm) : IEvent;
public record SensorUnbekannt(Id<Sensor> SensorId) : ITransientEvent;                  // Hinweis: flüchtige Ablehnung

public partial class Sensor : IState
{
    public int Hintereinander { get; set; }

    public partial class Decider : IDecider<Sensor>
    {
        public IEnumerable<OneOf<GrenzwertUeberschritten, AlarmAusgeloest, WertWiederImRahmen, SensorUnbekannt>> Decide(MeldeMesswert c)
        {
            if (this.State.Version == 0) { yield return new SensorUnbekannt(c.AggregateId); yield break; }
            …
        }
    }

    public partial class Applier : IApplier<Sensor>
    {
        public void Apply(GrenzwertUeberschritten e) { this.State.Hintereinander = e.Hintereinander; }
        …
    }
}
```

- Je Command ein `Decide`; jedes entschiedene Event hat ein `Apply`, jedes `Apply` ein entscheidendes Decide (R3). Ein Hinweis und eine Frist
  haben kein `Apply`.
- **Frist:** `yield return new Frist<BrichPruefungAb>(new BrichPruefungAb(c.AggregateId), TimeSpan.FromSeconds(30));` — die Uhr schickt den
  Command zur Zeit an **dasselbe** Aggregat; ist der Vorgang dann fertig, ist er ein Noop (§10.3.7).
- **Direkte Abfrage:** `public OneOf<Leihstand, NichtVerliehen> Frage(HoleLeihstand q) => …` im Decider, synchron, nur lesend. Die Query nennt
  ihr Aggregat mit genau einem Glied `Id<TAggregat>`; der Actor antwortet auf dem committeten Stand, die Antwort zeigt das Aggregat mit seiner
  Version als OCC-Stand ([03](03-schreibseite.md) §3.2). Query und Antwort liegen bei der Schreibseite.
- Keine Uhr, kein Zufall, kein `Guid.NewGuid()`, kein Lesen fremder Daten (R11); keine Collections, die über Vorgänge wachsen — Fan-in zählt ein
  Zähl-Aggregat.
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

### 10.3.4 … eine Funktion

```csharp
// im Host: eine Klasse, je Eingangstyp ein Auf (R2)
public sealed class SchadenBeiRueckgabe : IFunktion
{
    public MeldeSchaden? Auf(Zurueckgegeben e, Herkunft h) =>
        e.Zustand == Zustand.Beschaedigt ? new MeldeSchaden(h.NeueId<Schadensfall>(), e.LeserId) : null;
}

// draußen: eine Schnittstelle, getragen von einem Client (ITraegt) oder im Host umgesetzt; der Laufort steht in der Umgebung
public interface IImportLauf : IFunktion
{
    IAsyncEnumerable<OneOf<LegeArtikelAn, MeldeImportFortschritt, SchliesseImportAb>> Auf(ImportGestartet e, Herkunft h);
}
```

- Form: `Auf(Eingang[, Herkunft][, Lese-Fähigkeiten][, CancellationToken])`. Eingang ist ein Event aus dem Log oder ein Hinweis.
- **Ausgaben:** Commands (gehen über den einen Emit-Weg in den Aggregat-Mechanismus), Events (ohne Aggregat, kein `Apply`), Quell-Nachrichten,
  Hinweise. Ein Strom (`IAsyncEnumerable`) hat einen Fortschritt: nach einem Absturz geht es bei der nächsten Ausgabe weiter.
- **Festhalten:** das Framework hält die Ausgabe unter „Funktion, Eingang“ fest, bevor sie wirkt (A1); nur eine nachweislich **gleichbleibende**
  Funktion (Klasse im Host, kein Dienst, keine Lese-Fähigkeit, keine Uhr, kein Zufall, kein Zustand) spart das (R12).
- **Neue Ids** nur abgeleitet: `h.NeueId<T>()` bzw. `h.NeueId<T>(zeile)` (R13).
- **Dienst:** Technik hinter einer Funktion ist ein Domänen-Dienst — ein Interface mit `IDienst`, eine Implementierung, Konfig als Record
  (`Konfig:<Record>` in der Umgebung, Bindung `Dienste:<I>:Implementierung`); er steht nur im Konstruktor einer Funktion oder einer
  Dienst-Implementierung (R10). Eine Funktion mit Lese-Fähigkeit läuft im Host.
- **Zustand** (R19): `IZustand<T>` im Konstruktor einer Funktions-Klasse; sonst keine veränderlichen Felder. Gesichert mit der Position (A5);
  mit `[EineAdresse]` hat die Funktion einen Zustand für alle Partitionen.

  ```csharp
  [EineAdresse]
  public sealed class Prognosemodell(IZustand<Gewichte> gewichte) : IFunktion, ILebenszyklus
  {
      public Task BeimAktivierenAsync(CancellationToken ct) => …;      // nach Bau und Laden, vor dem ersten Auf
      public Task VorDemSichernAsync(CancellationToken ct) => …;       // vor jeder Sicherung
      public void Auf(TemperaturGemeldet e, Herkunft h) { … }         // lernt aus dem Log
      public VermerkePrognose? Auf(Tendenz t) => …;                    // Hinweis rein, Command raus
  }
  ```

- **Hinweis-Netz:** ein Hinweis, den ein Aggregat, eine Funktion oder ein Client ausgibt, geht über eine generierte Tabelle direkt an die
  Funktionen, die ihn hören — im Speicher, höchstens einmal; jeder gehörte Hinweis braucht einen Erzeuger (R20).

### 10.3.5 … eine Quelle

```csharp
public sealed record KatalogDateiAbgelegt : DateiGemeldet;     // der Datei-Treiber befüllt die geerbten Felder, die Kennung sagt die Konfig
public sealed record Messzyklus : TaktGemeldet;                 // je Intervall genau eine Quell-Nachricht
public sealed record RueckgabeGescannt(Guid LeserId, string Code, IReadOnlyList<string> Seiten) : IQuellNachricht
{
    public string Kennung => Code;                              // von einem Client gesendet; genau einmal je Kennung
}
```

Die Domäne benennt eine Quelle nur als Typ; Treiber und Werte (Pfad, Intervall, Webhook) stehen in der Umgebung (`Quellen:<Typ>`,
[04](04-konsum-und-prozess-maschine.md) §4.7).

### 10.3.6 … Akteure und Clients

```csharp
public sealed record Theke : IAkteur, IDarf<LeiheAus>, IDarf<GibZurueck>, IDarf<OffeneAusleihen>;   // auch IMensch, IMaschine, IKi
public sealed record Archiv : IMaschine, IDarf<IRegal>;                                               // darf eine Funktion draußen rechnen

public interface ILesesaal : IClientVertrag, ITraegt<IRegal>, ISendet<LeiheAus>, IFragt<OffeneAusleihen>, IHoert<MahnungEroeffnet> { }
```

Alles, was hineinkommt, kommt von einem Akteur, der es darf (`IDarf<T>`, R16). Ein Client-Vertrag sagt, was eine Software an einer Leitung tut:
sendet (`ISendet`), fragt (`IFragt`), hört (`IHoert`, verlierbar), trägt Funktionen (`ITraegt`). Wirksam ist Vertrag ∩ Token (R17); ein C#-Client
erklärt sich mit `[assembly: IstClient<IX>]`. Ausführlich: [konzept-akteure.md](konzept-akteure.md).

### 10.3.7 … einen Ablauf

Ein Ablauf über mehrere Aggregate ist ein **Vorgangs-Aggregat** plus **Funktionen** als Kanten (Sonde `Fluss.cs.txt`): eine Funktion startet den
Vorgang (`StartePruefung` mit `h.NeueId<Seitenpruefung>()`), eine Funktion draußen verteilt die Arbeit als Strom (je Seite ein `ZaehleSeite`),
das Aggregat zählt die Rückmeldungen (Fan-in) und entscheidet den Abschluss, eine Frist desselben Aggregats bricht nach Zeit ab, eine weitere
Funktion zieht die Folge. Die Zugehörigkeit fremder Events zum Vorgang ist ein **fachliches Feld** im Event (A2). Kompensation ist ein Decide,
das den Gegenzug als Event entscheidet.

## 10.4 Typisierte Ids und Herkunft

`Id<TAggregat>` ist das Ziel jedes Commands und der Verweis auf ein Aggregat überall sonst (Events, Queries, Antworten, Read-Models,
Fähigkeiten). Wirkung: Routing über Typen ohne Konvention (Invariante 3); Antworten sagen, was sie zeigen (der Stand jeder gezeigten Id kommt ohne
Zutun mit); Editor und LLM sehen, worauf ein Feld zeigt. Auf Leitung, Log und Wire ist `Id<T>` eine UUID (Namensregel `Abstractions/Idvertrag.cs`).

Die **Herkunft** (`Abstractions/Herkunft.cs`) ist der Parameter, über den ein Baustein von seinem Eingang erfährt: `Stream`, `Version`, `Zeit`
(Log-Zeit, nie die Uhr des Hosts), `Akteur`, `Id<T>()` (der Stream als Id eines Aggregats) und `NeueId<T>(zweck)` — dieselbe Eingabe ergibt beim
Wiederholen dieselbe Id. Ein Client darf neue Ids würfeln: er hält die Command-Id vor dem Senden fest (Ausgangskorb).

## 10.5 Wer hört, wer erzeugt

| Nachricht | darf in | Anzahl |
|---|---|---|
| Command | Aggregat | genau eins |
| Event (auch Quell-Nachricht) | Projektion, Funktion | beliebig viele |
| Event, vom Aggregat entschieden | sein `Apply` | genau das entscheidende Aggregat |
| Hinweis | Funktion (höchstens einmal, direkt an ihre Adresse), Client (`IHoert`) | beliebig viele; nie Projektion, nie Aggregat |
| Query | Reader **oder** Aggregat | genau eins |
| Frist | Uhr → Command an **dasselbe** Aggregat | genau eins |
| Antwort | der fragende Client | – |

| Baustein | erzeugt | sonst nichts |
|---|---|---|
| Aggregat | Events, Hinweise, Fristen; Antworten auf direkte Queries | keine Commands |
| Projektion | Wirkungen in ihrem Store (nur Schreib-Fähigkeiten) | keine Nachricht |
| Reader | Antworten (nur Lese-Fähigkeiten) | – |
| Funktion | Commands, Events, Quell-Nachrichten, Hinweise; nach außen nur über Dienste | – |
| Quelle | Quell-Nachrichten | – |
| Client | Commands, Quell-Nachrichten, Hinweise (`ISendet`), Queries (`IFragt`), Ausgaben getragener Funktionen (`ITraegt`) | – |
| Uhr | Commands aus fälligen Fristen | – |

## 10.6 Regeln und ihr Gegenstück im Build

Prüfen heißt Bauen (W5). Die Liste „Regel → Build-Gegenstück“ der Grammatik druckt `dotnet run --project GraphExtractor -- --grammatik` (aus
`DomainEditor/Grammatik.cs`); alle IDs mit Ort stehen in [05](05-generatoren-analyzer-proto.md) §5.3, die Garantien dazu in
[11](11-garantien.md) §11.10. Code-Kommentare zitieren die Regeln mit ihrer Nummer.

| # | Regel | geprüft durch |
|---|---|---|
| R1 | Der Ausgang steht in der Signatur (`Decide`, `Auf`, `Frage`); der Graph entsteht nur daraus, nie aus Rümpfen | CQRS050, CQRS100, CQRS057 |
| R2 | Je Baustein und Eingangstyp höchstens ein `Auf` bzw. eine `Frage` | CQRS101 |
| R3 | Ein Command gehört genau einem Aggregat (`ICommand<TAggregat>`); jedes entschiedene Event hat ein `Apply` im selben Aggregat und umgekehrt; Events aus Funktionen und Quellen haben kein `Apply` | Compiler, CQRS010/011, CQRS079/080 |
| R4 | Eine Query hat genau ein Ziel (Reader oder Aggregat) | CQRS084/086, CQRS057 |
| R5 | Eine Frist steht nur in einem Decide und zielt auf einen Command des eigenen Aggregats | Generator (`Frist<TCmd>`), Prüfung beim Schreiben im Actor |
| R6 | Ein Kreis Funktion → Command → Aggregat → Event → dieselbe Funktion endet nur, wenn das Decide fortschreitenden Zustand trägt | **nicht im Build** (aus Signaturen nicht von einem Ablauf zu unterscheiden, GR-KREIS, docs/13 FB-5); der Test-Ablauf bricht einen Kreis ab (`Cqrs.Testing/Ablauf.cs`) |
| R7 | Ids sind typisiert; Command-Ziele sind `Id<T>` | Compiler, CQRS013 |
| R8 | Fähigkeiten nur als Parameter (eine Funktion je Fähigkeit, ein Store bündelt), nie in Konstruktor, Feld oder `new` | CQRS051, CQRS054, CQRS055 |
| R9 | Eine Projektion schreibt in genau einen Store | CQRS052 |
| R10 | Dienste nur im Konstruktor von Funktionen und Dienst-Implementierungen; eine Funktion mit Lese-Fähigkeit läuft im Host | CQRS089–092, Start-Prüfung |
| R11 | Aggregate und Projektionen rechnen bei gleicher Eingabe gleich: keine Uhr, kein Zufall, kein `Guid.NewGuid()`, kein Lesen fremder Daten | CQRS102 |
| R12 | Funktionen halten ihre Ausgabe fest, bevor sie wirkt; Ausnahme nur nachweislich gleichbleibend | CQRS103, Generator |
| R13 | Neue Ids auf der Server-Seite nur abgeleitet (`Herkunft.NeueId`) | CQRS104 |
| R14 | Jeder Store implementiert genau eine Store-Klasse; Klasse 3 nur mit `[MindestensEinmal]`; Schreib-Fähigkeiten eines Klasse-2-Stores nehmen die `Herkunft` | CQRS109, CQRS110, Start-Prüfung; Einhaltung: allgemeiner Store-Test |
| R15 | Die Garantie folgt dem Eingang: Event → genau einmal, Hinweis → höchstens einmal, Query → keine Wirkung | folgt aus den Typen |
| R16 | Alles von draußen kommt von einem Akteur, der es darf | Generator, Start-Prüfung |
| R17 | Ein Client sendet, fragt, hört und trägt nur, was seine Akteure dürfen; eine Funktion draußen braucht einen Träger oder eine Bindung | CQRS095–097, CQRS087 (Warnung), Start-Prüfung |
| R18 | Die Hausregel für Actors: ein Weg je Absicht (nur Framework-Code) | CQRS112/113 |
| R19 | Zustand einer Funktion nur über `IZustand<T>` im Konstruktor einer Klasse; eine Funktion mit Zustand ist nie gleichbleibend; Lebenszyklus nur an Funktions-Klassen im Host | CQRS105, CQRS107 |
| R20 | Jeder Hinweis-Typ, den eine Funktion hört, hat einen Erzeuger; jede Adresse der Routing-Tabelle existiert | CQRS106, Start-Prüfung |

Dazu die Projektstruktur ([07](07-projektstruktur.md), CQRS098): wo eine Primitive liegt, sagt die Tabelle `Abstractions/Projektstruktur.cs`.

## 10.7 Entscheidungen und Annahmen

Entschieden von Tobi (2026-10-26 bis 2026-10-28); Code-Kommentare zitieren sie mit ihrer Nummer.

| # | Frage | Entscheidung |
|---|---|---|
| E1 | Geltungsbereich (Mandant, Daten-Rechte je Objekt) | nicht jetzt: im Zielbetrieb (interne Produktionsfirma) darf jeder alles; Akteur-Rechte nach Typ bleiben. Der Platz ist frei (§10.8). |
| E2 | Direkte Abfrage eines Aggregats | ja, als eigener Lesepfad neben dem Reader (§10.3.1) |
| E3 | Typisierte Ids | ja, Pflicht für Command-Ziele; in Antworten die Grundlage für feine Updates an Clients (§10.4) |
| E4 | Editor-Oberfläche | zurückgestellt; Werkbank und GraphExtractor bauen und `--check`/`--sonde` sind grün, `Werkbank/wwwroot/editor/` bleibt unberührt |
| E5 | Umsetzen | phasenweise, je Phase auf Tobis Wort |
| E6 | Darf eine Funktion Zustand haben? | ja, gekapselt und durabel, kein Domänen-Zustand (ein lernendes Netz, ein Cache, eine Verbindung); `IZustand<T>` im Konstruktor, Ressourcen je Aktivierung, keine neuen Event-Typen |
| E7 | Nachrichten zwischen Funktionen ohne Log | ja, als Hinweis (`ITransientEvent`), im Speicher, nie im Log (Invariante 6) |
| E8 | Wie kommen sie an? | hart verdrahtet: generierte Tabelle Hinweis-Typ → Adressen, direkt über den Boten; kein Broker, keine Abos, kein Pub/Sub von Proto.Cluster |
| E9 | C# und Python | eine Adresse je Funktion; dahinter rechnet die Umsetzung im Host oder die Sitzung eines Clients, der sie trägt; Ausgaben Commands und Hinweise |
| E10 | Lebenszyklus einer Funktion | optional, `ILebenszyklus` mit `BeimAktivierenAsync` und `VorDemSichernAsync`; das Ende ist `IAsyncDisposable`; draußen `beim_start`/`beim_ende` im Python-SDK |

Annahmen (sicherer Default, gelten, solange Tobi nicht widerspricht):

- **A1** Funktionen halten ihre Ausgabe standardmäßig im Log fest, bevor sie wirkt; nur nachweislich gleichbleibende sparen das.
- **A2** Die Zugehörigkeit zu einem Vorgang ist ein fachliches Feld im Event, keine versteckte Korrelation in den Metadaten.
- **A3** Wiederholungsfeste Stores (Klasse 2) schreiben mit der Event-Identität im Schlüssel oder bedingt nach Position.
- **A4** Der Neuaufbau einer Projektion geht über Generationen; Leeren ist der Sonderfall.
- **A5** Der Zustand einer Funktion wird mit ihrer Position gesichert (eine Transaktion; bei großem Zustand erst die Ablage, dann Position und
  Verweis), höchstens einmal je Sekunde und immer beim Stoppen, Passivieren und Umziehen; nach einem Absturz wirkt jeder Log-Eingang genau einmal
  auf den gesicherten Zustand, Hinweise im Flug gehen verloren.
- **A6** Die Adresse einer Funktion ist `funktion-<F>/<Partition>`; ein Hinweis geht an die Partition seines Herkunfts-Streams; mit
  `[EineAdresse]` gibt es eine Adresse für alles.
- **A7** Ein Hinweis trägt eine Kennung, die der Absender vergibt; Command-Ids aus einem Hinweis-Eingang werden daraus abgeleitet.
- **A8** Rechnet eine Funktion mit Zustand draußen, lebt der Zustand im Worker; der Funktions-Actor bindet seine Eingänge an eine tragende
  Sitzung (Lease je Funktions-Actor). Das Sichern ist Sache des Workers.

## 10.8 Was bewusst nicht kommt

Kein Geltungsbereich und keine Daten-Rechte je Objekt (E1); der Platz bleibt frei: die `Herkunft` trägt den Akteur, `Decide` darf ihn sehen; ein
Bereich wäre später eine weitere Kopfzeile plus Filter in Verteiler, Reader und Lesen ab Position. Kein Löschen- und Aufbewahren-Konzept (Option:
verschlüsselbare Felder als Typ-Fakt). Keine Schema-Evolution im Editor; `IUpcast` bleibt Backend-Primitive. Kein Dirigent, keine Saga, keine
Fluss-Sprache, keine Reaktion als eigene Art. Offenes steht in [13](13-reifegrad-schulden-bewertung.md) §13.0.
