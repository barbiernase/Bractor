# 04 — Konsum, Funktionen und Uhr

Stand 2026-11-09, gegen den Code geprüft. Wie man Projektion, Reader, Funktion, Graph und Quelle schreibt, steht in [10](10-entwickler-api.md).

Durabler Konsument des Logs ist nur die **Projektion**: sie schreibt ein Read-Model (replaybar); der Wecker am Commit weckt schnell, der Takt je
Partition heilt verlorene Weckrufe, gelesen wird geordnet aus dem Event-Store (Invariante 1), der Fortschritt liegt durabel im Store. Gerechnet
wird vor dem Aggregat in **Funktionsgraphen**: eine Funktion ist eine Klasse mit typisierten Ports, ein Graph nennt Funktionen als Knoten, jede
Kante steht als `[Von(nameof(…))]` am nehmenden Knoten, jeder Kopf als `[Kopf]`; ein Graph läuft als Instanz, jede Kopf-Nachricht startet eine Kette (jeder Schritt rechnet je Nachricht). Funktionen lesen nie das Log, zwischen ihnen
fließen Commands; genau einmal wirkt, was einen Graphen verlässt, über die offenen Eingänge am Kopf und die Inbox der Ziele (§4.4). Funktionen und
Graphen liegen **nur in Katalogen** (domänenneutral); domänenspezifisch machen sie die **Übersetzungen** einer Anwendung (§4.4). Eine **Quelle**
ist eine Funktion des Katalogs am Kopf eines Graphen (§4.7). Was ein Baustein ist, folgt aus seiner Code-Form (Marker-Interface,
Fähigkeits-Parameter, Signatur); einen zweiten Marker gibt es nicht. Zeit kommt über die **Uhr**: ein Decide setzt eine Frist, die Uhr sendet sie fällig.

| Konsument | Code-Form | Schleife | Fortschritt | Neu abspielbar |
|---|---|---|---|---|
| Projektion | `IProjektion`; `Task Auf(Event[, Herkunft], Schreib-Fähigkeiten eines Stores)` | `BatchLeser` je (Projektion, Partition) | Fortschritt (Position, Revision, Generation) und Guard je Stream — beim Store der Klasse 1 in seiner Transaktion, sonst beim Framework (§4.3) | ja (Neuaufbau über Generationen) |
| Graph | `record X(Knoten…) : IGraph`, Knoten = Funktionen mit `Auf(Nachricht[, Herkunft][, Lese-Fähigkeiten][, CancellationToken])` | Graph-Actor je Instanz und Cluster-Knoten, Parent je Knoten, Kinder bis `Parallel` | offene Eingänge am Kopf (`cqrs_eingaenge`) bis zum „fertig“ der Kette; gesicherter Zustand je Instanz und Knoten (`cqrs_zustaende`) | nein (nie aus dem Log) |

**Abläufe sind kein eigener Baustein.** Eine Rechnung je Auslöser — Folge, Gabel, Wahl, je Element rechnen — ist ein **Graph**;
was zusammengeführt, gezählt oder abgewartet wird (fremde Rückmeldungen, alle Elemente, eine Frist, zurücknehmen), entscheidet ein **Aggregat**. §4.5 zeigt
beides an der Prüfdomäne.

## 4.1 Batch-Konsum der Projektionen

**Partition.** Jede replaybare Projektion hat `P` Partitionen (Umgebung `Konsum.Partitionen`, Standard 8). Ein Stream gehört genau einer:
letztes Byte der UUID in RFC-Reihenfolge modulo P — in .NET `id.ToByteArray(bigEndian: true)[15] % P` (`Abstractions/PartitionsCheckpoint.cs`,
`Partitionierung`), im Store in seiner Sprache (Postgres: `get_byte(uuid_send(stream_id), 15) % P`, `Infrastructure/Persistence/PartitionsAusdruck.cs`). Beide Seiten rechnen dasselbe, ohne Index
und ohne Hash-Abhängigkeit von Laufzeit oder PG-Version; für zufällige und gehashte Ids gleichverteilt. `P` ≤ 256; ein Wechsel von `P` ist ein
Neuaufbau (er gehört zum Stand der Leseseite, §4.3).

**Partitions-Leser** (`Infrastructure/Projections/BatchLeser.cs`, als `BatchLeserActor` ein virtueller Cluster-Actor, Kind
`batch-<Projektion>`, Identität = Partition; die Kennung der Projektion ist ihr Typname). Der Cluster hält eine Aktivierung je Partition; fällt
das bei einer Netzstörung kurz aus, schützt die Revisionsprüfung des Fortschritts (G-4, unten). Der Fähigkeits-Bereich (Store-Instanz, Puffer)
gehört der Aktivierung (Rahmen, [03](03-schreibseite.md) §3.6): beim Stopp bricht der Durchlauf ab, der Bereich wird erst frei, wenn er geendet
hat. Wo der Fortschritt liegt und wie ein Batch gültig wird, sagt die Klasse des Stores (§4.3, `Projektionsnaht`). Ein Durchlauf:
1. **Stand laden:** der Fortschritt der Partition `PartitionsCheckpoint { Id = "<projektion>:<p>", Position (globale Sequenz), Version (Revision),
   Geparkt, Generation }` — beim Store der Klasse 1 im Store, sonst beim Framework (`IFortschrittsAblage.LadeFortschrittAsync`).
2. **Seite lesen:** `IEventStoreRepository.ReadPartitionAsync` — Events mit `seq_id` jenseits der Position, nur Partition `p`, nur die
   **abonnierten Typen samt aller früheren Versionen, die per Upcasting in sie münden** (generierte Tabelle `GeneratedEventUpcasting.Herkunft`;
   ohne verlässliche Liste ohne Typfilter), ohne schon Erledigtes, `order by seq_id limit N` (`Konsum.BatchGroesse`, Standard 500).
   Materialisiert wird wie in `ReadStreamAsync` (Upcasting, Tombstones überspringen).
3. **Guard je Stream:** die Guards der Streams der Seite (`ProjectionCheckpoint`) in **einer** Abfrage (`LadeGuardsAsync`); Events mit
   `AggregateVersion <= Guard` überspringen (Wiederholung nach Neuaktivierung oder verlorenem Commit).
4. **Anwenden:** `Beginne(generation)` am Store, dann je Stream in Versionsreihenfolge über den generierten Dispatch das `Auf` der Projektion
   (mit `Herkunft` und den Fähigkeiten aus dem Bereich). Der Store puffert die Wirkungen aller Streams; vor jedem Event setzt der Leser den Stream
   des Events (`Stream(id)`), eine Schreib-Regel je Dokument (§4.3) prüft gegen ihn.
5. **Commit:** Klasse 1 schreibt die Wirkungen, die Guards der berührten Streams und den Fortschritt mit erwarteter Revision in **einer**
   Transaktion (`SchreibeMitFortschrittAsync`); Klasse 2 und 3 schreiben erst die Wirkungen (`SchreibeAsync`), dann hält das Framework Guards und
   Fortschritt mit erwarteter Revision fest (`FortschrittBeimFramework`). Hat dazwischen eine zweite Aktivierung oder ein Neuaufbau geschrieben,
   verliert der Fortschritt (`PartitionsKonfliktException`), der Leser lädt den Stand neu und liest von dort (G-4) — bei Klasse 1 ist dann nichts
   aus dem Batch durabel, bei Klasse 2 stehen die Wirkungen und ergeben wiederholt dasselbe.
6. **Weiter**, solange die Seite voll war.

Aus dem Guard je Stream folgt der **Stand je Id**, den eine Antwort trägt (`ProjektionsStaende`): der Guard (Klasse 1 mit den Wirkungen in
derselben Transaktion, Klasse 2/3 nach ihnen — nie vor den Daten sichtbar, sinkt nie), und dahinter das Log — folgen nach ihm nur Events, die die Projektion nicht hört, gilt ihr
Inhalt bis zum nächsten gehörten Event bzw. bis zum Kopf des Streams. So taugt der Stand als Version des nächsten Commands, auch wenn danach eine
Frist oder ein fremdes Event kam. Vor dem Reader wartet der Query-Dienst begrenzt (3 s, `MindestensFrist`), bis jeder Stream aus „mindestens S auf v“
so weit ist (Read-your-writes, docs/konzept-akteure.md §5); reicht die Frist nicht, trägt die Antwort `noch_nicht`. Welche Ids eine
Antwort zeigt, findet der Generator selbst: jedes `Id<T>`-Feld der Antwort (auch geschachtelt, in Listen) und der Query, nach der gefragt wurde;
welche Projektionen hinter einer Frage stehen, folgt aus ihren Lese-Fähigkeiten (die Projektionen, die in denselben Store schreiben) — bei mehreren
gilt je Stream der kleinste Stand. Liegt der Fortschritt im Postgres des Logs (Marten-Store, Fortschritt beim Framework), liest eine Abfrage Guard und
Log zusammen (`IFortschrittImLog`).

Nach jedem Commit, der Events anwandte, meldet der Leser „P hat die Streams S weiterverarbeitet“ an die Verteiler (`Verteilung.MeldeVerarbeitet`) —
Mechanik nach dem Commit, die Projektion gibt dabei nichts aus. Eine Sitzung, die eine Antwort hält, die einen dieser Streams zeigt (oder, ohne
Ids, eine Antwort von P), nennt dem Client nach höchstens 200 ms die betroffenen Queries (`Aktualisiert`); er fragt neu. Die transitive
Abhängigkeit (eine Datensatz-Ansicht zeigt Labels von Bildpaaren) hat die Projektion beim Falten hergestellt — der Client kennt sie nicht.

**Position und Nachzügler.** Vor jeder Seite bestimmt `ReadPartitionAsync` die *gesetzte* Sequenz: bis dorthin ist jede Sequenz endgültig — der
Horizont der Vollständigkeit aus den Transaktionsnummern von Postgres, ohne Uhr ([03](03-schreibseite.md) §3.3, G-9). Die Position rückt nie darüber hinaus, bei voller Seite nie über deren Ende und
nie über das erste Event eines hängenden Streams (unten). Jüngere Events werden trotzdem angewandt (Latenz); welche schon erledigt sind, merkt
sich die Aktivierung und schließt sie beim nächsten Lesen aus, nach einer Neuaktivierung fängt der Guard sie ab. So überholt die Position keine
noch unsichtbare kleinere Sequenz. Die Liste der Erledigten ist gedeckelt (`BatchLeser.ErledigtHoechstens`, 5 000, PF-28): steht der Horizont
(eine lange offene Transaktion irgendwo im selben Postgres) oder hängt ein Stream, wüchse sie sonst mit jedem Event und ginge mit jeder Seite
an die Datenbank. Ist sie voll, wendet der Leser nur noch an, was bis zur gesetzten Sequenz reicht, und wartet sonst; ein hängender Stream wird
dann geparkt statt hinter ihm weiter zu sammeln. Gesetzte Sequenz und „steht dahinter überhaupt ein Event?“ kommen aus **einer** Abfrage; steht hinter der
Position nichts, entfällt die Seite — eine Weckung ohne Neues kostet eine Abfrage statt zwei (PF-14). Gelesen wird ab einer Lese-Position im Speicher: durabel wird sie mit jedem Commit eines Batches; über fremde
Events hinweg (leere Seite) höchstens einmal je Poll-Intervall — sonst committete unter Last jede Weckung nur eine Zahl. Eine Transaktion, die
lange offen ist, hält die Position an (nicht die Zustellung: jüngere Events werden weiter angewandt), sie wird nie überholt. Bis 2026-10-16 galt
hier eine Karenz von 3 s gegen den Event-Zeitstempel; eine länger offene Transaktion wurde überholt und ihr Event nie gelesen (G-9).

**Poison und Konflikt halten niemanden auf.**
- Wirft der Dispatch, ist der Batch verworfen; der Stream des werfenden Events wird abgetrennt und allein versucht, der Rest läuft ohne ihn.
  Wirft ein Commit (ohne Konflikt), wird der Batch nach Streams **halbiert**, bis der schuldige Stream allein steht. Wirft ein Stream allein,
  wird er **geparkt** (`Geparkt` im Partitions-Checkpoint, im selben Commit wie der Rest; Dead-Letter einmal je Parken). Seine weiteren Events
  überspringt der Leser; je Poll-Intervall versucht er ihn einzeln ab seinem Guard (`ReadStreamAsync`) und nimmt ihn heraus, sobald er
  durchläuft. Nichts geht verloren, die übrigen Streams laufen weiter, der Stand ist sichtbar (`/health`).
- Ein Konflikt an einem geteilten Dokument (`SchreibKonfliktException`) wird ebenso halbiert; ein einzelner Stream wird bis zu fünfmal gestreut
  wiederholt und bleibt danach bis zum nächsten Durchlauf stehen (nicht geparkt — ein Konflikt ist vorübergehend; die Position hält vor ihm).
  Innerhalb eines Batches sehen sich zwei Streams auf demselben geteilten Dokument über die Identity-Map (read-your-writes) — kein Konflikt.
- Wirft etwas anderes (Datenbank weg), endet der Durchlauf; der nächste beginnt beim durablen Stand.

**Weckung** (docs/03 §3.5, `Infrastructure/Wecken/`). Es gibt **eine** Stelle, an der nach einem Commit geweckt wird: nach dem `SaveChanges` des
Group-Commits (`MartenEventBatchWriter`) und des Einzel-Appends (`MartenEventStore`, auch der Rückfall `IsolateAsync` und der Betrieb ohne
Bündeln). Ins Log schreiben nur Aggregate. Der **Weckplan** (`WeckplanGenerator`, nur aus Signaturen) sagt je Event-Typ, wen
der Commit weckt: den Batch-Leser jeder Projektion, die den Typ hört (`batch-<Projektion>`, Partition des Streams), und die Uhr bei `FristGesetzt`. Der
**Wecker** schickt je Commit und Ziel genau einen `Wake` (ohne Inhalt, verlierbar); je Ziel ist beim Absender höchstens einer unterwegs, was
währenddessen kommt, wird zu genau einem weiteren (`Weckungen`). Das Ziel quittiert sofort und merkt sich während eines Durchlaufs „noch
einmal“ (`RahmenActor.Wecke`). Der **Takt** (`BatchKonsumStartupService`) weckt alle 30 s jede Partition — das Netz für verlorene Weckrufe; beim
Start prüft er, dass jeder registrierte Hörer mit jedem gehörten Typ im Plan steht (Boot-Fehler sonst, GR-WECKPLAN; dass ein Hörer aus dem Log nur
konkrete, persistente Events hört, sichert im Build die Form: CQRS057). Weckplan und Batch-Kinds lesen eine Projektion
über dieselbe Lesart (`Projektionsform`) — sie können nicht auseinanderlaufen. Ein Commit, dessen Ausgang mehrdeutig blieb (G-3), weckt nicht — ihn holt der Takt. Die
Batch-Leser passivieren nicht (es gibt nur Projektionen × Partitionen davon, der Takt weckt sie ohnehin).

**Rückstand.** `/health` und `/monitoring/metrics` melden über alle Projektionen die relevanten Events jenseits der Positionen
(`KonsumRueckstand`, eine Abfrage je Projektion, gezählt über höchstens die ersten 1 000 Events hinter der kleinsten Position — darüber
„mehr“, PF-29; ein volles `count(*)` las bei einer Partition ohne Checkpoint je Abruf das ganze Log) und die geparkten Streams.

### 4.1.1 Last ohne Eingang

Kommt nichts hinein, arbeiten nur die Takte (gemessen auf dem Messserver, Diagnose `--art leerlauf`, docs/12 §12.5, PF-14):
- **Batch-Poll** alle 30 s je Projektion und Partition eine Weckung, ohne Neues eine Abfrage (Beispieldomäne: 5 Projektionen × 8 Partitionen =
  40 Abfragen je 30 s — er wächst mit Projektionen × Partitionen, PF-15);
- **Zusteller** jede Sekunde je Knoten die fälligen offenen Eingänge (eine Abfrage über den Index von `cqrs_eingaenge`);
- **Quellen** nach ihrer Art: der Dateiwächter scannt sein Verzeichnis je Intervall (Standard 1 s), der Taktgeber wartet auf sein Intervall;
- **Uhr** jede Sekunde die DB-Uhr und die fälligen Fristen (eine Abfrage über den Index von `cqrs_fristen`).

**Nachlauf.** Nach einer Last schreiben die Batch-Leser ihre Position über fremde Events einmal fest; danach ist der Host still.
Die Messung wartet diesen Nachlauf ab (alle Partitions-Stände am Kopf des Logs) und weist ihn eigens aus.

## 4.2 Woraus folgt, was ein Konsument ist

Eine **Projektion** ist eine Klasse mit `IProjektion` und je gehörtem Event einem `Task Auf(TEvent e[, Herkunft h], Schreib-Fähigkeiten…[,
CancellationToken ct])` (docs/10 §10.3) — der Eingang darf auch ein **Bündel** von Events sein (`Buendel<A, B, …>`, §4.3.1); ihre Kennung ist der
Typname (`Projektionsvertrag.Kennung`). Gelesen wird sie über
**eine** Lesart (`Infrastructure.SourceGeneration/Projektionsform.cs`) — dieselbe für Batch-Kinds, Weckplan, Akteur-Rechte, Analyzer und Extractor.
Der `PullPathGenerator` erzeugt in der Komposition je Projektion den Dispatch (`switch` über den Event-Typ → ihr `Auf`, die `Herkunft` aus dem
Event, die Fähigkeiten aus dem Bereich), die gehörten Typen, ein Batch-Kind `batch-<Projektion>` und einen `Projektionseintrag`
(`GeneratedProjektionen`, `GeneratedPullPaths`). Den Code-Stand der Projektion schreibt ihre eigene Compilation als Assembly-Angabe
(`ProjektionsstandGenerator`); die Projektion selbst bleibt unberührt (kein `partial`).

Eine Projektion bekommt die Store-Funktionen, die sie benutzen darf, als Parameter (Fähigkeit = Interface `: IWriteStore` mit genau einer Funktion,
CQRS051). Die generierte Fabrik öffnet je Partitions-Leser einen **Fähigkeits-Bereich** (`IFaehigkeitsFabrik.Oeffne()`,
`Infrastructure/Projections/DiFaehigkeitsFabrik.cs`): Stores sind Scoped, jede Fähigkeit eines Stores liefert in diesem Bereich **dieselbe
Instanz**. Beim ersten Spawn prüft `Projektionslauf.Ziel`, dass alle Schreib-Fähigkeiten einer Projektion denselben Store auflösen, dass er ein
`IProjektionsZiel` mit genau einer Klasse ist und dass er Klasse 3 nur mit der Erlaubnis `[MindestensEinmal]` an der Projektion ist (R14; im Build
CQRS052, CQRS109, CQRS110). Ein Store in Konstruktor, Feld oder Eigenschaft eines Konsumenten ist verboten (**CQRS054**), `new …Store()` ebenso
(**CQRS055**). Eine Form, die der Dispatch nicht ruft (anderer Name, ein Parameter, der keine Fähigkeit ist, eine Rückgabe, ein Hinweis als
Eingang), ist CQRS057; zwei `Auf` für denselben Eingang CQRS101.

Ein **Reader** ist eine Klasse mit `IReader` und je Query einer `Frage(TQuery q, Lese-Fähigkeiten…[, CancellationToken])`, die eine Antwort oder
`OneOf<…>` liefert (auch als `Task`). Der `QueryDienstGenerator` erzeugt den Query-Dienst der Anwendung (`GeneratedQueryDienst`): je Query die
Frage, die Projektionen dahinter (aus den Lese-Fähigkeiten: die Projektionen, die in denselben Store schreiben) und die gezeigten Ids (die
`Id<T>`-Felder von Antwort und Query) — ohne `ctx.Track` und ohne Reader-Attribut. Eine Query hat genau ein Ziel (R4): statt eines Readers kann
sie ein Aggregat beantworten (`Frage` im Decider, exakter Stand mit seiner Version, ohne Projektion dahinter — [03](03-schreibseite.md) §3.1 (d)).

Eine Funktion erkennt der `FunktionsBausteinGenerator` an `IFunktion` und den `Auf`-Methoden (bzw. `Lausche`), einen Graphen der
`GraphGenerator` am Record mit `IGraph` (§4.4). Eine Funktion darf Lese-Fähigkeiten nehmen, nie Schreib-Fähigkeiten: was sie bewirkt, geht als
Command an ein Aggregat.

## 4.3 Projektionen und Store-Klassen

Form (neutral aus der Agnostik-Sonde, `GraphExtractor.Kern/Sonde/Lesen.cs.txt`): `public sealed class Leihliste : IProjektion` mit
`public Task Auf(Ausgeliehen e, Herkunft h, INotiereAusleihe notiere) => notiere.SchreibeAsync(…)`. Eine Projektion gibt nichts aus und schreibt in
genau einen Store (CQRS052); sie rechnet bei gleicher Eingabe gleich — Zeit nur aus der `Herkunft` (R11). Was entsteht, liest der Generator nur
aus der Signatur (CQRS050), nie aus dem Rumpf. Read-Models, die ein Aggregat abbilden, tragen seine typisierte Id (`Id<T> Id`, dann `partial`:
der `ReadModelSchluesselGenerator` ergänzt die Speicher-Identität `Schluessel`, CQRS111 — Marten kann `Id<T>` nicht als Dokument-Identität);
Fähigkeiten nehmen `Id<T>`.

**Theorie (der gültige Kern).** Exactly-once-*Zustellung* gibt es nicht (Wirkung und Quittung lassen sich über eine Grenze nie atomar
koppeln); erreichbar ist exactly-once-*Wirksamkeit*. Das Framework schreibt keine Datenform vor; von einem Store braucht es nur eine von drei
Zusagen (docs/04 §4.3, `Abstractions/Projektionsziel.cs`), und jeder Store implementiert **genau eine**:

| Klasse | Schnittstelle | Zusage | genau einmal, weil | Treiber im Framework |
|---|---|---|---|---|
| **1 atomar** | `IAtomaresZiel` | Wirkungen und Fortschritt in **einer** Transaktion | ein Absturz lässt beides oder keins zurück; der Fortschritt wird mit erwarteter Revision geschrieben | `Core/MartenStore.cs` |
| **2 wiederholungsfest** | `IWiederholungsfestesZiel` | jede Wirkung ergibt wiederholt dasselbe; den Fortschritt hält das Framework und rückt ihn erst nach den Wirkungen vor | die Wirkung trägt die Event-Identität im Schlüssel oder schreibt bedingt nach der Version — darum nimmt jede Schreib-Fähigkeit die `Herkunft` (CQRS109) | `Core/DateiZiel.cs` (`DateiZiel`) |
| **3 mindestens einmal** | `INurMindestensEinmal` | keine | – (nur mit `[MindestensEinmal]` an der Projektion, CQRS110) | `Core/DateiZiel.cs` (`DateiSink`) |

Ob ein Treiber seine Klasse einhält, ist eine Laufzeit-Eigenschaft: jeder Treiber besteht den **allgemeinen Store-Test**
(`Infrastructure/Projections/Storetest.cs`, je Treiber eine Implementierung von `IStoretestTreiber`): Batch-Abbruch, Wiederholung (Wirkungen
geschrieben, Fortschritt nicht), zweite Aktivierung mit älterem Stand und Neuaufbau mit einer alten Aktivierung, die danach noch schreibt — gegen ein
echtes Log und den echten Batch-Leser (`StoretestPostgresTests` für alle drei Treiber). Die Prüfdomäne zeigt Klasse 2 und 3 an echten Projektionen
(`MessreiheProjektion` in ein Datei-Ziel, `MesszaehlerProjektion` in eine Senke; `StoreKlassenE2ETests`). Wo ein Datei-Store liegt, sagt die
Umgebung (`Stores:<Store>:Verzeichnis`), sonst das Temp-Verzeichnis je Datenbank und Schema des Logs (`StoreOrte`).

**Klasse 1 in Marten** (`MartenStore`): Wirkungen werden nur gepuffert, `SchreibeMitFortschrittAsync` spielt sie zusammen mit den Guards
(`ProjectionCheckpoint` je Stream) und dem Fortschritt (`PartitionsCheckpoint`) in **einer** Marten-`IdentitySession` ab, ein `SaveChangesAsync`
(`Core/MartenFortschritt.cs`). Der Fortschritt trägt eine numerische Revision (`Core/KonsumSchema.cs`): noch nie geschrieben → `Insert` (ein zweiter
Erster scheitert am Schlüssel), sonst `UpdateRevision` auf Revision + 1 (eine zweite Aktivierung scheitert) — **G-4 geschlossen**. Dazu die
Schreib-Regel je Dokument, geprüft gegen den Stream des laufenden Events: eigenes Dokument (Schlüssel = Stream), Zeile dieses Streams, oder
geteiltes Dokument (`IGeteiltesReadModel`, Versionsprüfung; ein Konflikt verwirft den Batch → `SchreibKonfliktException`, §4.1). Dass ein Store
nur über diese Puffer-Methoden schreibt und Lese- und Schreib-Fähigkeiten nicht vermischt, prüft der `StoreZugriffAnalyzer` (CQRS066/067).

**Klasse 2 und 3:** der Fortschritt liegt beim Framework (`FortschrittBeimFramework`, dieselben Dokumente im Log-Postgres, eigene Transaktion nach
den Wirkungen). Das `DateiZiel` legt je Wirkung eine Datei unter einem Pfad mit der Event-Identität ab (`LegeAb`) oder ersetzt eine Datei nur, wenn
die Version des Eingangs größer ist als die gespeicherte (`LegeAbWennNeuer`, unter einer Sperre geprüft) — immer über eine temporäre Datei und
`File.Move`. Die `DateiSink` hängt Zeilen an, ohne Bedingung.

**Neuaufbau über Generationen (A4):** `ProjectionRebuilder` gibt dem Store eine neue Generation (`NeueGenerationAsync`: ein leeres Ziel; gelesen
wird weiter die alte), setzt den Fortschritt aller Partitionen in dieser Generation auf Anfang (neue Revision — ein laufender Leser verliert seinen
nächsten Commit), lässt je Partition einen Batch-Leser ab 0 laufen — seitenweise, als Strom — und schaltet um (`UmschaltenAsync`). Geschrieben wird
immer in die Generation aus dem Fortschritt (`Beginne(generation)`): eine alte Aktivierung, die nach dem Zurücksetzen noch schreibt, trifft das neue
Ziel nicht. Für Klasse 1 ist die neue Generation das Leeren der eigenen Dokument-Typen (der Sonderfall; Wirkungen gelten dort ohnehin nur mit dem
Fortschritt). Funktionen haben keinen Neuaufbau und liegen nie auf diesem Pfad. In Sim vergleicht `ProjektionsStandDienst` beim Start den Stand
jeder Projektion (Code-Hash der Klasse + Stores + die Form des Konsums mit der Partitionszahl) mit `<schema>.stand` und gibt bei Abweichung jeder
Projektion eine neue Generation (Fortschritt auf Anfang, sofort umgeschaltet; der erste Takt weckt jede Partition); in Prod meldet er nur.

### 4.3.1 Bündel: Events eines Schlüssels

Ein `Auf` kann statt eines Events ein **Bündel** nehmen: `Task Auf(Buendel<WareReserviert, ZahlungEingegangen> b, Herkunft h, ISchreibeLieferung s)`
(`Abstractions/Buendel.cs`, 2 bis 16 verschiedene persistente Events, Eigenschaften `Erstes`, `Zweites` …). Es wird gerufen, sobald zum **Schlüssel**
von jedem Typ ein Event im Log steht, und bei jedem weiteren Event eines dieser Typen erneut — je Typ mit dem neuesten; die `Herkunft` ist die des
auslösenden Events. Ein Bündel hat keinen eigenen Namen: es heißt nach seinen Events (im Editor `WareReserviert ⋈ ZahlungEingegangen`).

**Der Schlüssel** folgt nur aus Code-Fakten (`Infrastructure.SourceGeneration/Buendelform.cs`, dieselbe Lesart für Generator und Extractor): je Event
die Ids, die es nennt — der Stream (das Aggregat, dessen Applier es faltet) und jedes Feld `Id<T>`. Der Schlüssel ist die eine Id, die alle gemeinsam
haben; je Event kommt er aus dem **Stream**, wenn `T` es faltet, sonst aus seinem einzigen **Feld** `Id<T>`. Haben die Events keine gemeinsam, ist
das Bündel **global** (je Typ das neueste Event); haben sie mehrere, wählt `[Ueber<T>]` am Parameter. Lässt er sich nicht ableiten (mehrdeutig, ein
Event mit zwei Feldern `Id<T>`, `[Ueber<T>]` mit einem Event ohne diese Id), bricht der Build (**CQRS118**, `PullPathGenerator`); die Form selbst
(nur persistente Events, je Typ eine Stelle) prüft CQRS057.

**Ausgewertet wird gegen das Log, ohne Zustand** (`Infrastructure/Projections/Buendel.cs`, `Buendelblick`): zum Event an der Sequenz `s` fragt der
Leser je übrigem Typ nach dem neuesten Event zum selben Schlüssel vor `s` — je Seite in einer Runde (`IEventStoreRepository.RueckblickAsync`,
Teilabfragen mit „sortiert, nur das erste“ und `union all`). Die Antwort hängt nur am Log bis `s`: gleich bei jedem Neuaufbau, jeder Partitionierung
und jeder Reihenfolge der Ankunft (Invariante 1). Das setzt voraus, dass jede Sequenz vor `s` endgültig ist — eine Projektion mit Bündel wendet darum
nur an, was bis zur gesetzten Sequenz reicht (§4.1, sonst nur bei voller Liste der Erledigten); steht der Horizont, wartet sie in kleinen Schritten
(10 × 20 ms), danach holt der Takt nach. Ein geparkter Stream wird ebenso nur bis zur Lese-Position versucht. Der Weckplan nennt jedes Event eines
Bündels.

**Woher die Partner kommen:** über den Stream liest der Rückblick den Index (Stream, Version) des Logs. Über ein Feld oder global liest er das
**Verzeichnis der Schlüssel** (`cqrs_rueckblick`, `Infrastructure/Persistence/RueckblickTabelle.cs`): je Event eines solchen Typs eine Zeile (Typ, Feld,
Schlüssel, Sequenz), geschrieben vom Append in derselben Transaktion (`BeilagenSchreiber`, die Sequenz aus `mt_events` nach dem Einfügen); was vor dem
Bündel im Log stand, trägt der erste Leser einmal nach (`SichereRueckblickAsync`, gemerkt in `cqrs_rueckblick_stand`). Eine eigene Tabelle statt eines
Index auf `mt_events`: den löschte Weasel beim Abgleich — Martens Event-Tabelle kennt keine fremden Indizes. Welche Typen hineingehören, sagt die
generierte Tabelle `GeneratedProjektionen.Rueckblick` (über `IAnwendung.Rueckblick`).

**Schreiben über Streams hinweg.** Ein Bündel über ein Feld oder global löst aus verschiedenen Streams aus, also in verschiedenen Partitionen und in
beliebiger Reihenfolge. Was es je Schlüssel festhält, schreibt es darum bedingt nach der Position des Auslösers im Log: `Herkunft.Sequenz` (über Streams
vergleichbar) bzw. `Herkunft.Stand` (die Sequenz, ohne sie die Version) — `DateiZiel.LegeAbWennNeuer` vergleicht so; ein geteiltes Read-Model der
Klasse 1 behält den Stand mit dem größeren `h.Sequenz`. Beispiel: `Pruefdomaene.Projections/Abrechnung/RechnungsbildProjektion.cs` (je Schlüsselart
ein Bündel), Garantie-Tests `BuendelGarantieTests`.

## 4.4 Funktionen und Graphen

**Funktion.** Eine Funktion ist eine Klasse `: IFunktion` (Name = Klassenname); jedes `Auf` ist eine Operation desselben Knotens. Ein `Auf`
nimmt **genau eine Nachricht** (einen Command ohne Ziel), dazu optional `Herkunft`, Lese-Fähigkeiten (`: IReadStore`) und ein `CancellationToken`,
und rechnet, sobald sie ankommt — kommen zwei, rechnet es zweimal. Die Fälle des Rückgabetyps sind die **Ausgangsports**: ein konkreter Typ,
`OneOf<…>`, `IEnumerable<…>` oder `IAsyncEnumerable<…>` (auch in `Task<…>`), mit Commands und Hinweisen — kein `void`, kein `T?`, kein
`ValueTask`, ein leerer Strom heißt „nichts“ (CQRS100). Ein Event hört eine Funktion nur als **Antwort** auf ihren eigenen Command an ein Aggregat
(das Ziel entschied es, das Kind rechnet gleich weiter, CQRS108); einen Hinweis hört sie nie. Im Konstruktor stehen Konfig-Records (Werte je
Instanz und Knoten), Technik (jede andere Schnittstelle oder Klasse, ohne Marker, CQRS089–092), `IZustand<T>` und `ILogger<T>`. Eine generische
Funktion (`Glaettung<TFenster>` der Sonde, als Knoten `Glaettung<Kurzfenster>`) ist eine Blaupause, je Typargument eines Knotens ein Baustein;
generische Commands fließen nur zwischen Knoten. Der `FunktionsBausteinGenerator` erzeugt je Funktion ihre Rechnung (`GeneratedFunktionsBausteine`:
je `Auf` der Eingang als Objekt, die Ausgaben in Reihenfolge, reflexionsfrei).

**Zusammenführen, Einsammeln, Warten** stehen im Logik-Code, nicht in der Signatur. Mehrere Nachrichten, `T?`, `OneOf<…>` oder
`IReadOnlyList<T>` als Eingang sind ein Formfehler (CQRS100, GR-EINGANG). Drei Wege, in dieser Reihenfolge:

- **Die Nachricht trägt mit, was danach gebraucht wird.** Die Vorschau der Bildaufbereitung trägt die Datei der Quelle, so deutet die letzte
  Funktion das Bild aus ihr allein (`Bildvorschau.Katalog/Dateinamendeutung.cs`):
  ```csharp
  public OneOf<BildGedeutet, DateinameUnbekannt> Auf(VorschauErstellt vorschau) => …;
  ```
- **Ein Knoten mit Zustand** nimmt jede Nachricht in einem eigenen `Auf` und hält die erste in `IZustand<T>`, bis die zweite da ist (bis dahin ein
  leerer Strom) — das Prognosemodell der Sonde (`Vorhersage.cs.txt`) führt so zwei Glättungen zusammen.
- **Ein Aggregat** zählt oder führt zusammen, wenn das Ergebnis fachlich zählt: je Element meldet sich ein Command, das Decide zählt mit und
  schließt nach dem letzten ab (Zähl-Aggregat, §4.5); Warten auf Zeit ist eine Frist im Decide.

**Graph.** Ein Graph ist ein Record `: IGraph` in `Graphen.cs` eines Katalogs (docs/07 §7.2); jeder Parameter ist ein **Knoten** (Name = Konfig-Schlüssel, Typ = eine
Funktion oder ein anderer Graph), die Reihenfolge bedeutet nichts. Dieselbe Funktion darf mehrfach stehen — zwei Knoten mit je eigener Konfig
(Vorschau und Daumenbild aus derselben Skalierung). Jede innere **Kante** steht am nehmenden Knoten: `[Von(nameof(A), nameof(B))]` nennt die Knoten
desselben Graphen, von denen er bekommt; eine genannte Quelle speist die Ports, deren Typ sie ausgibt, und mindestens einen. Jedes Argument ist
`nameof` eines anderen Parameters (kein String-Literal, nicht der Knoten selbst, kein Name doppelt), ein Umbenennen zieht der Compiler nach. Aus
Typen allein entsteht innen keine Kante: ein neuer Knoten bekommt nichts, was er nicht nennt, und eine Kette gleichen Typs (`Bild → Bild → Bild`)
ist kein Kreis. Ein **Kopf** trägt `[Kopf]` und nimmt von außen (von einem Client, vom Ausgang eines anderen Graphen), was keine
genannte Quelle speist — je Eingangstyp genau einer; ein Knoten kann beides tragen (ein `Auf` von außen, eines von innen). Jeder Knoten trägt
`[Kopf]`, `[Von]` oder beides; nur eine Quelle trägt keins, sie ist der einzige Kopf ihres Graphen. Was ein Knoten ausgibt und kein Knoten über `Von`
von ihm nimmt, verlässt den Graphen — als Command an ein Aggregat oder an den Kopf eines anderen Graphen (zwischen den Bausteinen
bleibt es beim Typ-Routing mit genau einem Empfänger, Invariante 3), als Hinweis an die Akteure, die ihn hören. Ein Graph ist kreisfrei über seine Knoten (einen Command an den eigenen Kopf darf er geben — das ist eine
neue Kette). Seine **Signatur** ergibt sich (Eingänge = Köpfe, Ausgänge = was ihn verlässt), deshalb ist ein Graph als Knoten in einem anderen eine
Schachtel: `Von` an ihm speist seine Köpfe, `[Kopf]` an ihm macht seine Köpfe zu Köpfen des äußeren, `Von(nameof(Schachtel))` an einem anderen
Knoten meint seine Ausgänge; seine Knoten heißen mit Pfad (`Aufbereitung.Verkleinern`). Zwischen Knoten steht nie Klebecode: passt ein Typ nicht,
ist der Adapter eine eigene kleine Funktion. Der `GraphGenerator` erzeugt `GeneratedGraphen` (Knoten aufgefaltet, je Operation die Ports mit Form
und Erzeugern, Köpfe, Ebenen); dieselbe Lesart (`Graphform`) liest der `GraphAnalyzer` (CQRS115: GR-GRAPH, GR-KANTE, GR-KOPF), der
Extractor und `--graph <Typ>`. Eine Funktion in keinem Graphen läuft nie (CQRS117, Warnung).
```csharp
// Bildvorschau.Katalog/Graphen.cs — eine Kette im Katalog; jede Nachricht trägt die Datei der Quelle mit
public sealed record Bildaufbereitung(
    [Kopf] Bildeinlesen Einlesen,
    [Von(nameof(Einlesen))] Bildverkleinerung Verkleinern,
    [Von(nameof(Verkleinern))] Kontrastausgleich Kontrast,
    [Von(nameof(Kontrast))] Dateinamendeutung Deuten) : IGraph;
// Bildvorschau.Katalog/Graphen.cs — Quelle am Kopf, die Bildaufbereitung als Schachtel; das gedeutete Bild verlässt den Graphen
public sealed record Linienimport(Dateiwaechter Eingang, [Von(nameof(Eingang))] Bildaufbereitung Aufbereitung) : IGraph;
```
Die übrigen Muster zeigt die Sonde (`GraphExtractor.Kern/Sonde/Vorhersage.cs.txt`, Kartenausgabe): dieselbe Funktion zweimal, eine Kette gleichen
Typs, ein Knoten mit `[Kopf]` und `[Von]`, ein Graph im Graphen, dessen Ausgang ein anderer Knoten per `Von(nameof(Filter))` nimmt.

**Übersetzung.** Funktionen und Graphen eines Katalogs kennen keine Domäne (ein Bezug ist ein `Guid`, keine `Id<T>`), und die Domäne kennt keinen
Katalog. Domänenspezifisch macht sie eine **Übersetzung** der Anwendung: eine Klasse `{TSource}_{TTarget}_{n}(TSource source) : ITranslate<TSource, TTarget>`
in `<Wurzel>.Uebersetzungen/<Bereich>/Uebersetzungen.cs` (`Abstractions/Uebersetzung.cs`; das Projekt referenziert Domäne und Kataloge, docs/07).
Ihr Konstruktor nimmt genau die Quelle (sonst CQRS119), `Translate()` baut das Ziel aus ihren Feldern, ohne Logik: `Ziel: source.Feld`, ein `Guid`
in eine `Id<T>` als `new(source.Feld)`, eine `Id<T>` in einen `Guid` als `source.Feld.Wert`. Ob das Ziel vollständig ist, prüft der Compiler. Der
Editor legt die Klasse an und schreibt `Translate()` aus der Verdrahtung (docs/10 §10.9). `TSource` ist ein Command ohne Ziel oder ein
Event (eine Antwort), `TTarget` ein Command (CQRS121); ein Graph nennt eine Übersetzung nie als Knoten (CQRS115). Die Lesart ist
`Uebersetzungsform` (per Link in Generator, Analyzer und Extractor; die Zuordnung liest der Editor aus `Translate()`), der `GraphGenerator` erzeugt
`GeneratedUebersetzungen` (je Übersetzung ihr Aufruf: Konstruktor mit der Nachricht, dann `Translate()`, reflexionsfrei).

**Wohin, sagt der Typ** (Invariante 3), für alle Instanzen gleich — wer je Instanz anderes will, braucht andere Typen:
- **Ausgang und Zwischenstand:** jede Ausgabe eines laufenden Graphen mit Typ `TSource` übersetzt das Kind gleich nach dem Rechnen (inline, ohne
  Parent, Zustand, Konfig oder Rechner) — einen Command an ein Aggregat sendet es sofort (Id aus Aufruf, Ort der Übersetzung und Stelle), einen
  Command ohne Ziel gibt es an den Kopf genau eines Graphen. Ein Ausgang eines laufenden Katalog-Graphen ohne Empfänger ist CQRS120.
- **Antwort:** antwortet das Aggregat auf den Command einer Übersetzung mit einem Event, das eine Übersetzung nimmt, rechnet das Kind sie gleich
  weiter (Antwort-Übersetzung; das Event muss eines sein, das das Decide des übersetzten Commands ausgeben kann, CQRS123). Ihr Ergebnis geht an den
  Kopf eines Graphen — darum nimmt ein Graph mit Quelle keine Antwort (er hat keinen Kopf), sie geht an einen eigenen Graphen.
- **Kopf:** einen Command ohne Ziel eines Clients (eine Nachricht der Domäne) übersetzt der Eingang in den Eingang eines Graphen
  (`Verkettung.Ziele`).
Der Wirker in der Spur ist die Übersetzung (`uebersetzung:<Name>`). Ein Graph eines Katalogs **läuft** in einer Anwendung, wenn eine Übersetzung oder
ein Akteur ihn bindet.
```csharp
// Pruefdomaene.Uebersetzungen/Messung/Uebersetzungen.cs — Client ↦ Katalog Rechenwerk (Kopf) und dessen Ergebnis ↦ Aggregat
public sealed class HochrechnungAngefordert_Skalieren_1(Id<Messstelle> Stelle, double Wert) : ITranslate<HochrechnungAngefordert, Skalieren>
{
    public Skalieren Translate() => new(Bezug: Stelle.Wert, Wert: Wert);
}

public sealed class Skaliert_MeldeAusreisser_1(Guid Bezug, double Wert) : ITranslate<Skaliert, MeldeAusreisser>
{
    public MeldeAusreisser Translate() => new(AggregateId: new(Bezug), Wert: Wert);
}
```

**Instanz.** Ein Graph ist eine Blaupause und läuft erst als **Instanz**: eine Datei je Instanz, `umgebungen/<name>/graphen/<instanz>.json`, mit
`Graph`, bei einem Quellkopf dem `Akteur` (er gibt hinein, was aus den Ausgängen des Graphen nach der Übersetzung entsteht), wahlweise dem `Rechner`, auf dem sie läuft, und je Knoten
die Felder seiner Konfig-Records sowie
`Parallel` (Standard 256), `Warteschlange` (10 000), `Wiederholungen` (3), `Vorwaermen`, `Zeitlimit` und `Technik` (die Umsetzung je Technik,
nötig bei mehreren):
```json
{ "Graph": "Linienimport", "Akteur": "KameraSystem",
  "Eingang": { "Pfad": "${env:HOME}/cqrs-sim/${branch}/DateiErkannt", "Fertig": "Stabil", "Kennung": "PfadGroesseZeit" },
  "Aufbereitung.Verkleinern": { "Wiederholungen": 1, "AusgabePfad": "${env:HOME}/cqrs-sim/${branch}/preprocessed" } }
```
Dieselbe Blaupause läuft beliebig oft (zwei Dateiwächter auf zwei Verzeichnissen sind zwei Instanzen des Linienimports). Der Start prüft jede
Instanz (`GraphInstanzen.Aus`): den Graphen gibt es, jeder Schlüssel ist ein Knoten, der Akteur existiert und darf jeden Command, der aus einem
Ausgang nach der Übersetzung entsteht (`IDarf`), und ein Graph mit Command-Kopf läuft je Umgebung genau einmal. Die Werte der Konfig-Records und die Wahl der Technik bindet
`BindeTechnik` streng (fehlend, unbekannt, mehrdeutig, Kreis über Technik: Boot-Fehler). Ohne Umgebung (Prüfstände, Tests) läuft je Graph eine
Instanz unter seinem Namen, ein Graph mit Quelle nur, wenn der Test die Konfig der Quelle nennt. Den Akteur einer Instanz mit Command-Kopf erbt
die Kette vom Auslöser; eine Quellinstanz trägt den Akteur ihrer Datei.

**Kette.** Eine Nachricht am Kopf startet eine **Kette** (`Infrastructure/Funktionen/Kette.cs`). Jeder Schritt — der Parent eines Knotens — nimmt
eine Nachricht als **Aufruf**, lässt ein Kind sein `Auf` rechnen und schlägt jede Ausgabe in der **Wegtabelle** nach (generiert aus den `[Von]`,
`GeneratedGraphen`): an welche Knoten sie geht. Dorthin schickt er sie als neuen Aufruf, direkt an deren Parent. Kommen zwei Nachrichten an einen
Schritt, rechnet er zweimal; ein Strom gibt je Element eine Nachricht, eine Wahl nur den Fall, der kam. Niemand führt Buch über die ganze Kette:
- **Gesichert** (der Standard): der Absender wartet auf das „fertig“ des Empfängers und meldet selbst erst „fertig“, wenn alle seine gesicherten
  Ausgaben fertig sind. So reicht die Rückmeldung wie bei verschachtelten Anrufen bis zum Kopf; der Kopf ist erledigt, wenn sie dort ankommt.
- **Verlierbar** (`[Von(nameof(A), Verlierbar = true)]`): der Absender schickt einmal, ohne Antwort; was dahinter geschieht, hält den Kopf nicht auf,
  und geht die Nachricht verloren, merkt es niemand.

Ausgaben an Knoten des Graphen gehen erst mit dem Erfolg des Aufrufs hinaus (eine Wiederholung liefert sie nicht doppelt), Commands an Aggregate
sofort. Haltbar ist allein der Kopf: fehlt sein „fertig“ (Absturz, Umzug), kommt er erneut, und die Kette rechnet neu — mit denselben Ids.

**Ids und Herkunft.** Command-Ids leitet das Framework ab, nie der Entwickler, und zwar aus der Nachricht, nie aus einer Reihenfolge: die Kennung
eines Aufrufs aus der Kennung seiner Nachricht, dem Ort (`instanz/knoten`) und der Operation (`Kettenvertrag.Aufruf`); die Nachricht, die er an
Knoten desselben Graphen gibt, aus Aufruf, Ort und Stelle (`Kettenvertrag.Weiter`); die Command-Id aus Aufruf, Stelle, Typ und Ziel
(`Kettenvertrag.Befehl`; die Stelle zählt über den ganzen Aufruf, auch Ausgaben auf Antworten). Am Kopf ist die Nachricht der Eingang, bei einer
Quelle Instanz und Kennung ihrer Meldung (`IMeldung`, `Kettenvertrag.Quelle`: aus dem Inhalt des Elements, nie aus seiner Stelle). Neue Aggregat-Ids
leitet die Funktion über `h.NeueId<T>(zweck)` ab (R13). `Herkunft` nennt bei einem Eingang eines Clients, der auf ein Fakt reagiert, dessen Stream und
Version, aus einer Quelle die Kennung ihrer Kette als Stream (Version 0) und die Zeit, dazu den Akteur der Kette.

**Fehler und Rückstau.** Das `Zeitlimit` eines Knotens begrenzt eine Rechnung (danach gilt sie als gescheitert). Wirft ein Knoten, rechnet ihn der
Parent bis `Wiederholungen` erneut; danach antwortet er „gescheitert“, das über die gesicherten Kanten bis zum Kopf reicht: der durable Kopf wird
geparkt (`cqrs_eingaenge`, Spalte `geparkt` mit dem Grund, nie mehr fällig), und `LaufGescheitert` (`Abstractions/Graph.cs`) geht als Hinweis an die
Akteure, die ihn hören. Andere Zweige laufen zu Ende; ihre Commands wirken über die Inbox genau einmal. Hat ein Schritt so viele Aufrufe offen, wie
seine `Warteschlange` erlaubt, antwortet er „später“; der Absender schickt nach einer Pause erneut — der Rückstau reicht so bis zum Kopf.

**Laufzeit** (`Infrastructure/Funktionen/`). Je Cluster-Knoten spawnt der Host nach dem Beitritt eine **Wurzel** `funktionen` (kein virtueller
Actor), darunter je Instanz einen **Graph-Actor** `funktionen/<instanz>` — den Halter ihrer Schritte und ihren Kopf — und darunter je Knoten einen
**Parent** `funktionen/<instanz>/<knoten>` mit Kindern bis `Parallel`. Die Adresse eines Schritts folgt aus Instanz, Knotenpfad und Rechner; die
Aufrufe einer Kette bleiben auf dem Rechner, der ihren Kopf annahm (Parent an Parent, ohne Netz). Ob ein Cluster-Knoten eine Instanz **hält**
(Eingänge an sie bekommt), meldet die Wurzel sofort im Gossip-Zustand (`FunktionsOrte`). Eine Instanz, die einen **Rechner** nennt, legt nur der Host
mit diesem `Cluster:Rechner` an (`EigenerRechner`); jeder Host meldet seinen Namen im Gossip-Zustand, und tragen zwei denselben, hält nur das Mitglied
mit der kleineren Id die festgelegten Instanzen (der andere meldet den Fehler). Die **Wege** (`FunktionsWege`) schicken einen Eingang an den eigenen
Knoten, wenn er die Instanz hält, sonst reihum an einen, der sie hält (`Bote.FrageAnKnotenAsync`). Der Graph-Actor erkennt einen doppelten Kopf an der
Kennung (in Arbeit: er hängt sich an; fertig: sofort „fertig“, Gedächtnis 10 000); sind schon so viele Ketten offen, wie die Warteschlange des Kopfs
erlaubt, heißt die Antwort „später“. Der Parent erkennt einen doppelten Aufruf ebenso an seiner Kennung. Ein **Kind** rechnet einen Aufruf zugleich
und bringt jede Ausgabe hinaus: einen Command an ein Aggregat der Reihe nach über den Emitter (§4.6; „unbekannt“ → derselbe Command erneut, bis acht
Versuche) — die **Antwort** trägt die Events, die das Ziel entschied (`CommandResult.Antwort`, auch bei einer erkannten Wiederholung), und hört die
Funktion einen dieser Typen, rechnet das Kind ihn gleich weiter (Kennung `Kettenvertrag.Antwort`); einen Command ohne Ziel an einen Knoten desselben
Graphen im Ergebnis an seinen Parent, der ihn nach der Wegtabelle weitergibt; einen an den Kopf einer anderen Instanz dorthin, und es wartet, bis
deren Kette fertig ist („später“ oder 60 s ohne Antwort → erneut, mit derselben Kennung); einen Hinweis verlierbar an die Verteiler. Reihenfolge nur
bei `Parallel` 1.

**Aktivierung** (`FunktionsAktivierung`, eine je Kind): ein eigener DI-Bereich, darin die Umsetzung über ihre generierte Fabrik und ihre Technik
(`GeneratedTechnik`; Konfig-Records und Technik-Wahl liest der Bereich aus dem Knoten der Instanz, `KnotenBereich`). Technik lädt beim ersten Aufruf;
beim Stoppen wird erst die Umsetzung, dann der Bereich mit seiner Technik freigegeben (`IAsyncDisposable`/`IDisposable`).

**Zustand** (`IZustand<T>`). Steht `IZustand<T>` im Konstruktor einer Funktion oder einer Technik in ihrer Konstruktor-Kette, hat der Knoten
gesicherten Zustand — je Instanz und Knoten eine Zeile in `cqrs_zustaende` (`ZustandsAblage`; das Bild aus eigenem und Technik-Zustand, ohne
Reflection gepackt). Die Aktivierung lädt ihn beim Beginn; nach jedem Aufruf geht ein Schnappschuss an den Parent, der den letzten sichert, dessen
Aufruf und alle früheren beendet sind — mit den Kennungen dieser Aufrufe (`§gesehen`). Kommt nach einem Absturz einer davon erneut, antwortet der
Parent „fertig“, ohne neu zu rechnen: was der Stand enthält, zählt nicht doppelt. Ein Schreiber: eine Instanz mit Zustand oder
Quelle nennt im Cluster ihren **Rechner** (Pflicht, sonst Boot-Fehler) und läuft nur dort — eine Quelle an ihrer Steuerung, ein Modell auf seiner
GPU. Fällt der Rechner aus, zieht nichts um: die durablen Eingänge warten in `cqrs_eingaenge`, die Quelle setzt nach dem Neustart ab ihrem
gesicherten Stand fort. Im Einzelknoten-Betrieb ist der eine Host jeder Rechner. Was aus Fakten gelernt wird, gehört weiter in eine Projektion, die Funktion liest es per Lese-Fähigkeit:
```csharp
public sealed class Schaetzer(IZaehlwerk zaehler) : IFunktion         // Prüfdomäne, Graph Merkmalbildung
{
    public async Task<VermerkeSchaetzung> Auf(Merkmal m, IHoleMessreihe reihe) => …;   // Command rein, Messreihe (Projektion) lesen, Command raus
}
public sealed class Zaehlwerk(IZustand<Zaehlstand> stand) : IZaehlwerk { … }         // Technik mit Zustand: gesichert mit dem Knoten des Schätzers
```
Zustand in Feldern oder Eigenschaften verbietet CQRS105 (R19).

**Offene Eingänge am Kopf** (`Infrastructure/Eingaenge/`). Durabel ist eine Kette nur an ihrem Kopf. Ein Command ohne Ziel von einem Client (Sitzung
`SitzungsActor.AnKopfAsync` oder JSON-Eingang, docs/03 §3.1) legt je Instanz, deren Graph ihn am Kopf nimmt, eine Zeile in `cqrs_eingaenge` (Kennung
aus Command-Id und Instanz, Nachricht als Diskriminator und Wire-JSON, Herkunft, Fälligkeit, Versuche, geparkt); erst dann quittiert die Sitzung dem
Client. Nennt der Command ein Fakt als `ursache`, ist dessen Stream und Version die Herkunft, sonst die Kennung selbst ohne Vorgänger im Log. Direkt
danach geht der Eingang auf dem **schnellen Weg** an die Instanz (verlierbar); die Zeile wird erst nach 5 s fällig. Der
**Zusteller** (`ZustellerActor`, ein virtueller Cluster-Actor, Kind `zusteller`, Takt je Knoten jede Sekunde, höchstens ein Durchlauf zugleich)
liest die fälligen Zeilen und bringt jede über die Wege hin: „fertig“ trägt sie aus (das tut auch der Graph-Actor, wenn das „fertig“ der Kette ankommt),
„angenommen“ macht sie nach 60 s wieder fällig, „später“ oder ein Fehler nach 2 s. Der Einblick zeigt sie je Instanz (`/einblick/auftraege`).
Zwischen Knoten desselben Graphen reist nichts als Eingang — das tragen die Aufrufe der Kette.

**Hinweise** (`Infrastructure/Funktionen/Hinweise.cs`). Ein Hinweis aus einem Knoten oder eine gescheiterte Kette geht nur an die Verteiler und
von dort an die Sitzungen der Akteure, die ihn hören — ohne Log, höchstens einmal. Was ein Knoten braucht, ist ein Command.

**Bestand.** Beispieldomäne: der **Linienimport** bei ImagePair (`Dateiwaechter` → **Bildaufbereitung** aus `Bildvorschau.Katalog`:
`Bildeinlesen` → `BildEingelesen` → `Bildverkleinerung` → `BildVerkleinert` → `Kontrastausgleich` → `VorschauErstellt` (trägt die Datei der Quelle
mit) → `Dateinamendeutung` → `BildGedeutet`, im Ablauf übersetzt ↦ `NimmBildAuf` (flache Felder, das Decide baut `BildMeta` und `BildVersion`);
`BildNichtLesbar` und `DateinameUnbekannt` sind Hinweise des Katalogs; die **Bildaufnahme** ist dasselbe ohne Dateiwächter, Kopf `DateiGefunden`), die Pflege eines Datensatzes und die Meldung eines fertigen Teils als **Reaktionen eines Clients** (Schritt 6, kein Graph mehr: der
`ReaktionsWorker` hört als `Datensatzpfleger` `RangeAngefordert` bzw. `EinfrierenAngefordert`, liest per Query und gibt `NimmRangeAuf` bzw.
`SchliesseEinfrierenAb`, als `Teilmelder` `TeilauftragAbgeschlossen` und gibt `MeldeTeilFertig` an den Sammelvorgang, den das Event nennt);
ebenso reagieren die Python-Worker: der `Klassifizierer` auf `ImagePairKomplett` mit seinem Urteil, das `TrainingsSystem` auf
`TrainingAngefordert` in Stücken. Prüfdomäne — die Köpfe ihrer Graphen speist ihr `ReaktionsWorker`
(`Pruefdomaene.Client.Worker`, Akteur `Sachbearbeitung`): auf jedes Fakt, das einen Ablauf beginnt oder fortsetzt, gibt er dessen Command hinein
(`RechnungGestellt` ↦ `Freizugeben`, `AuftragGestartet` ↦ `AuftragAbzuwickeln`, `PostenReserviert` mit Fehlmenge ↦ `Unterschritten` …), mit dem
Fakt als Ursache, also als Herkunft des Eingangs. Der **Abrechnungsablauf** (`Freigabeautomat` gibt eine Rechnung bis 10 000 frei, darüber
legt er sie dem Rechnungsprüfer vor — `LegeRechnungZurPruefungVor`, auf das Fakt reagiert der `Rechnungspruefer`;
`Gutschrift`, `Mahnwesen` mit Technik `IMahnversand`), der **Auftragsablauf** (§4.5), der **Importablauf** (`Importannahme` mit Kopf
`ImportAngefordert` von einem Akteur, `Importlauf` als Strom), die **Merkmalbildung** (`Merkmalsbildung` → `Merkmal` gabelt zu `Ausreisserpruefung`
und `Schaetzer`, der Zustand hat), der **Nachschubablauf** (`Nachschub`, Kopf `Unterschritten`), der **Verteilungsablauf** und die **Zubuchung**
(Je und Sammeln, §4.5). Kein `Decide` gibt einen Command ohne Ziel aus; was danach geschieht, stößt ein Client an.

## 4.5 Abläufe: Aggregat, Graph und Frist

Ein Ablauf über mehrere Aggregate hat zwei Teile. Was **je Auslöser** gerechnet wird — Folge, Gabel, Wahl, je Element rechnen — ist ein Graph. Was
zusammengeführt oder abgewartet wird — fremde Rückmeldungen, alle Elemente zählen, zurücknehmen, eine Frist überwachen —, ist ein Aggregat: sein
Decide entscheidet jeden Schritt aus seinem Zustand, sein Log ist der Ablauf. Aggregate reden nur über Graphen und Clients, die auf ihre Fakten
reagieren, miteinander (ein Decide gibt keinen Command aus, CQRS114). Die Zugehörigkeit eines fremden Events zu einem Ablauf ist ein **fachliches
Feld** (A2), kein Korrelations-Header.

**Beispiel: der Auftrag der Prüfdomäne** (`Pruefdomaene/Auftrag/`, `Pruefdomaene.Client.Worker/Reaktionslauf.cs`). Ein Auftrag reserviert einen
Posten und belegt ein Kontingent (zwei unabhängige Zweige); erst wenn beide gelungen sind, ist er bestätigt; scheitert einer, wird zurückgenommen,
was gelang. Gerechnet wird hier nichts — der Ablauf besteht aus Fakten und den Reaktionen eines Clients:
- `StarteAuftrag` → `AuftragGestartet`; der `ReaktionsWorker` hört es und gibt `ReservierePosten` und `BelegeKontingent`, beide mit dem **Beleg**
  `(Belegart.Auftrag, AuftragId)` als fachlichem Feld.
- Posten und Kontingent entscheiden für sich (`PostenReserviert`, `ReservierungAbgelehnt` …) und tragen den Beleg im Event; der Worker hört sie
  und gibt `VermerkeReservierung`, `VermerkeBelegung` bzw. `VermerkeAbsage` an den Auftrag.
- Das Decide des Auftrags entscheidet **Join** und **Kompensation**: sind Reservierung und Belegung vermerkt, folgt `AuftragBestaetigt`; kommt
  eine Absage, folgt `AuftragGescheitert` und für jeden gelungenen Zweig ein Fakt, das den Gegenzug anfordert (`PostenfreigabeAngefordert`,
  `KontingententlastungAngefordert`) — genau einmal, weil der Zustand es sich merkt; eine verspätete Rückmeldung nimmt sich selbst zurück. Der
  Worker sendet daraus den Gegenzug (`GibPostenFrei`, `EntlasteKontingent`).

**Rechnen im Graphen, Zählen im Aggregat** (Graph `Merkmalbildung` in `Pruefwerk.Katalog`, `Pruefdomaene.Uebersetzungen/Messung/`): ein neuer Messwert geht über die
Übersetzung in den Kopf der Merkmalbildung; sie bildet das Merkmal, prüft auf Ausreißer und schätzt mit Zustand und einer Lese-Fähigkeit des
Katalogs (umgesetzt vom Store der Messreihe), die Übersetzungen machen aus Ausreißer und Schätzung Commands an die Messstelle. Was eingesammelt
oder gezählt wird, entscheidet ein Aggregat (Zähl-Aggregat: je Element meldet sich ein Command, das Decide zählt mit und schließt nach dem
letzten ab) — nicht der Graph.

**Frist statt Zeitlimit.** Ein Decide gibt `Frist<TCmd>(Befehl, Nach)` aus; die Uhr (§4.7) sendet den Befehl fällig an dasselbe Aggregat, und
dessen Decide entscheidet, ob er noch etwas bedeutet. Beispiel: der Trainings-Wächter der Beispieldomäne setzt beim Start eines Trainingslaufs
eine Frist und erklärt den Lauf bei Ablauf ohne Lebenszeichen für gescheitert. Das `Zeitlimit` eines Knotens dagegen begrenzt nur eine einzelne Rechnung.

**Ohne Dirigent.** Es gibt keinen Fold über fremde Streams, keinen Builder und keine ausdrücklichen Kanten. Was garantiert ist, folgt aus den
Bausteinen: das Aggregat entscheidet unter Single-Writer und Inbox, der Kopf einer Kette bleibt offen, bis ihr „fertig“ ankommt, die Commands einer
Kette wirken über die Inbox ihrer Ziele genau einmal, die Uhr sendet jede Frist mindestens einmal. Garantien und Nachweise:
[11](11-garantien.md). Ein Kreis Graph ↔ Aggregat ist der Normalfall eines Ablaufs; der Build warnt davor nicht (R6, docs/13).

## 4.6 Das Emit-Primitiv und die Inbox

Jeder Command an ein Aggregat, den das Framework sendet (ein Knoten eines Graphen, die Uhr), geht durch das **eine**
Emit-Primitiv `CommandEmitter.EmitAsync` (`Infrastructure/PubSub/CommandEmitter.cs`):
- die `CommandId` gibt der Aufrufer deterministisch vor (Knoten: aus Aufruf — Nachricht, Ort, Operation —, Stelle, Typ und Ziel; Uhr: aus Stream
  und Version des Frist-Events) → stabil über jede Wiederholung;
- `CommandModus.Emittiert()` = **keine erwartete Version**;
- eine **Frage** des Boten mit Pflicht-Frist (Standard 5 s; [03](03-schreibseite.md) §3.6), at-least-once, ohne eigenen Retry;
- liefert den **Ausgang** (`EmitAusgang`): `Zugestellt` (Erfolg, auch als erkannte Wiederholung), `Abgelehnt` (fachlich, durabel als Quittung in der Inbox),
  `Unbekannt` (Frist abgelaufen, technischer Fehler, Transportfehler). Keine Ausnahme verlässt den Emitter. Wer „unbekannt“ bekommt, wiederholt
  mit derselben CommandId: das Kind eines Knotens sendet erneut (bis acht Versuche, danach scheitert der Aufruf und der Parent wiederholt ihn; der
  Kopf der Kette bleibt offen), die Uhr lässt die Frist liegen;
- **Drossel je Ziel** (`EmitDrossel`): höchstens 8 Sends je Ziel-Aggregat gleichzeitig unterwegs (je Knoten); der Rückstau eines heißen
  Aggregats liegt vor dem Send statt in seiner Mailbox (PF-9).

**Dedup macht die Framework-Inbox, nicht der Fachcode.** Die Inbox ist eine Tabelle des Event-Stores (`cqrs_inbox`, `InboxTabelle`,
[03 §3.3](03-schreibseite.md)): jeder Append des Aggregat-Actors trägt die Quittung seines Commands (gewirkt oder abgelehnt), geschrieben in
derselben Transaktion wie die Events und eindeutig je CommandId. Eine Wiederholung scheitert am Schlüssel, nichts wird geschrieben, der Actor
quittiert den Ausgang des ersten Mals. Vor dem Append erkennt ein kleiner Cache im Actor (`BoundedInbox`, je Ausgang 1 000 CommandIds) die
häufige Wiederholung kurz nach dem ersten Mal schon **vor** der OCC-Prüfung. Exactly-once gilt innerhalb des Horizonts
(`Schreibpfad.InboxHorizontTage`, Standard 30 Tage).

**Keine Marken im Log.** Ein Noop und eine fachliche Ablehnung schreiben kein Event, nur ihre
Quittung (gewirkt bzw. abgelehnt) — die Version des Ziels bleibt, ein Aggregat hat **eine** Version, die Zahl seiner Events. Eine Wiederholung
derselben CommandId bekommt denselben Ausgang, auch nach einem Neustart (Inbox vor OCC). Die Quittung ohne Events gilt nur auf dem Stand, auf dem
entschieden wurde (Kopf-Prüfung, [03 §3.3](03-schreibseite.md)). Die Ablehnung selbst ist ein Hinweis, gezielt an die Sitzung des Aufrufers
(verlierbar). Braucht ein Ablauf sie, ist sie ein Fakt (Event, etwa `ReservierungAbgelehnt` der Prüfdomäne).

**Analyzer-Zwang** (`CommandEmitAnalyzer`): **CQRS020** (`RequestAsync<CommandResult>` oder `FrageAsync<CommandResult>` außerhalb von
Emitter/Dispatcher), **CQRS021** (roher Command-Send mit `CancellationToken.None`/`default`).

## 4.7 Uhr und Quellen

**Uhr** (`Abstractions/Uhr.cs`, `Infrastructure/Uhr/`). Ein Decide gibt `Frist<TCmd>(Befehl, Nach)` aus; der generierte Handler macht daraus
das Event `FristGesetzt` (ein `IRahmenEvent`), und der Actor legt in derselben Transaktion eine Beilage ins Fälligkeits-Verzeichnis
`cqrs_fristen` (fällig = Zeitstempel des Events + Dauer, Datenbank-Uhr). Die Uhr ist ein Actor einmal im Cluster (`uhr/0`), jeder Knoten tickt
sie jede Sekunde an (`UhrTakt`, verlierbar). Sie liest die fälligen Fristen über den Index (`FristVerzeichnis.FaelligeAbfrage`), sendet sie mit
der Command-Id aus Stream und Version des Frist-Events und trägt sie erst nach entschiedenem Ausgang aus — doppelt gefeuert wirkt es über die
Inbox des Ziels einmal. In Sim verschiebt ein Uhr-Versatz die Datenbank-Uhr (Durchspielen).

**Quellen** sind Funktionen aus dem Katalog des Frameworks (`Core/Katalog/`). Eine Quellfunktion hat `Lausche(CancellationToken)` statt `Auf` —
ein `IAsyncEnumerable` ihrer Commands — und steht am Kopf eines Graphen, als dessen einziger Kopf (CQRS115); die Domäne nennt sie als Knoten
(`Linienimport(Dateiwaechter Eingang, Bildaufbereitung Aufbereitung)` im Katalog `Bildvorschau`), ihre Werte stehen in der Instanz. Was sie meldet, ist ein gewöhnlicher
Command ohne Ziel (`Abstractions/Katalog.cs`), den der nächste Knoten nimmt; Codegen und Leitung kennen die Katalog-Commands wie die der Domäne.

| Katalogfunktion | meldet | Konfig (am Knoten) | Zustand |
|---|---|---|---|
| `Dateiwaechter` | `DateiGefunden` je fertige, noch nicht gemeldete Datei | `Pfad`, `Muster`, `Unterordner`, `Erkennung` (`Abfrage` oder `EreignisUndAbfrage`), `Intervall`, `Fertig` (`Stabil` über ein `Fenster` oder `Marker` mit `Endung`), `Kennung` (`NameGroesse` oder `PfadGroesseZeit`) | `Gesehen`: relativer Pfad → Kennung |
| `Taktgeber` | `Takt` je Intervall (Beginn, auf das Intervall gerundet, UTC) | `Intervall` (mindestens 1 s) | `Taktstand`: das letzte gemeldete Intervall |
| `Webhook` | `Aufruf` je Aufruf an `POST /eingang/webhook/<Pfad>` (Postfach je Pfad, voll → „später“) | `Pfad` | keiner: ein Aufruf, dessen Kette nicht fertig wurde, wiederholt der Absender — mit derselben Kennung dieselbe Meldung, dieselben Ids |

Der Dateiwächter **liest** nur (er legt nichts an, verschiebt und löscht nichts); ein Weckruf (Intervall, wahlweise ein Dateisystem-Ereignis, das
nur weckt) löst einen Scan aus, der Scan entscheidet. Quell-Commands stehen **nicht im Log**: ob eine Quelle Doppeltes vermeidet oder nach einem
Neustart weitermacht, regelt sie über ihren **gesicherten Zustand** (§4.4) — die Architektur liefert nur ihn. Das Kind des Quellknotens lauscht,
solange der Cluster-Knoten die Instanz hält (eine Quellinstanz läuft genau einmal im Cluster, auf ihrem Rechner); je Element gehen die nächste
laufende Nummer und der Schnappschuss des Zustands an den Graph-Actor, das nächste Element erst, wenn er die Kette angenommen hat (Gegendruck über
die Warteschlange des Kopfs). Kennung und Herkunft folgen aus Instanz und der **Kennung der Meldung** (`IMeldung`: beim Dateiwächter relativer
Pfad, Größe und letzte Änderung, beim Taktgeber der Beginn des Intervalls, beim Webhook die Kennung des Absenders) — meldet die Quelle nach einem
Neustart dasselbe Element erneut, auch in anderer Reihenfolge, entstehen dieselben Ids, und ein zweites Mal startet der Graph-Actor keine Kette, solange
er das Element kennt; eine eigene Quelle ohne Meldung zählt bis zum Umbau der Quellen (docs/plan-clients-und-quellen.md) nach der Nummer. Gesichert
werden Zustand und Nummer, sobald die Kette und alle früheren beendet sind — auch eine gescheiterte Kette zählt als beendet (er meldet `LaufGescheitert`, die Quelle wiederholt ihn nicht). Endet das
Lauschen (die Quelle warf oder ihr Strom endete), beginnt der Parent es nach einer Pause neu, mit dem zuletzt gesicherten Stand. Eine eigene Quelle
ist eine Funktion mit `Lausche` wie die des Katalogs.

## 4.8 Dead-Letter & Snapshots

- **Dead-Letter:** `MartenDeadLetterSink` schreibt ein durables `DeadLetter`-Dokument je toter Zustellung (Schema `dlq`), best-effort —
  Beobachtung, kein kritischer Pfad (z. B. ein geparkter Stream einer Projektion, ein vergifteter Aggregat-Actor). Lesen: `MartenDeadLetterReadStore`.
- **Snapshots:** `MartenSnapshotStore` — `Snapshot<TState>` als jsonb je Aggregat, Registrierung generiert (`SnapshotRegistrationGenerator`).
  **Nicht-autoritativ:** kein Treffer oder eine andere `SchemaVersion` → Voll-Replay (`AggregateRehydrator`). Die Schema-Version ist der
  Struktur-Hash des States plus der Code-Hash von State und Applier (`{State}AggregateHandler.CodeHash`) — eine geänderte Apply-Logik
  invalidiert alte Snapshots. Geschrieben wird nach einer Schwelle von Events und bei Deaktivierung: der State wird im Actor-Turn
  kopiert (die Inbox reist nicht mit — sie ist eine Tabelle); der DB-Write läuft nach der Schwelle neben der Mailbox (`Neben`, der Turn wartet nicht), bei Deaktivierung abgewartet mit 5 s Frist. Die Kopie ist ein JSON-Round-Trip (docs/13 W10-2).

## 4.9 Spur der Konsumenten

Jeder Log-Eintrag trägt die Header `ursache`, `wirker`, `ueber`, `stand` (`Abstractions/Spur/Spur.cs`, geschrieben in
`Infrastructure/Persistence/SpurHeader.cs`). Die Konsumenten setzen sie per `Spur.Von(…)` um ihren Ausgang; der `CommandEmitter` legt Ursache
und Wirker in den Command-Envelope, das Ziel-Aggregat schreibt mit `wirker = decide:<Aggregat>/<Command>` und dem Absender als `ueber`:
- **Knoten eines Graphen:** Ursache = die Ursache seiner Kette (das Fakt, auf das der Client reagierte, der den Kopf speiste; bei einer Antwort das
  Event, das das Ziel entschied), Wirker am Command = der Knoten, `knoten:<Instanz>/<Knoten>` — die Spur zeigt so den Schritt der Kette. Der Akteur
  der Kette reist mit. Was zwischen Knoten fließt, steht nie im Log.
- **Quelle:** eine Quell-Kette hat keinen Log-Eintrag als Wurzel; seine Ursache ist der abgeleitete Stream der Instanz an der laufenden Nummer.
- **Uhr:** Ursache = das Frist-Event, Wirker `uhr` (ohne Karte).
- **Akteur:** ein Command von draußen trägt `ueber = akteur:<Akteur>`; reagiert ein Client auf ein Event, nennt die Hülle es als `ursache`
  (`Stream:Version`, der Läufer setzt sie), sonst ist die Ursache leer.
- **Projektion** schreibt nie ins Log; `projektion:<Typ>` ist nur Karten-Id.

Das Format steht an einer Stelle (`Abstractions/Spur/WirkerId.cs`), den Weg eines Eintrags über die Karten rechnet `SpurWeg`.

## 4.10 Offen

Offene Punkte stehen nur in [13](13-reifegrad-schulden-bewertung.md): der Webhook nur mit JSON-Eingang und je Prozess (GF-2), Durchsatz einer Instanz
mit Zustand (PF-34), Instanzen ohne Ort (FV-4), Kreise Graph ↔ Aggregat (FB-5), hartkodierte Intervalle P2-8, reflektive Snapshot-Kopie W10-2,
Projektlage W2-1.
