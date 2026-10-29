# 04 — Konsum, Funktionen und Uhr

Stand 2026-10-28 (Plan Pub/Sub-Neustrukturierung Phase 7: Wecker am Commit, Verteiler je Knoten für die Clients, Stand je Id aus Guard und Log), gegen den Code geprüft.
Ziel für den weiteren Umbau: docs/plan-pubsub-neustrukturierung.md.

Was auf ein Event hin geschieht, ist eines von zwei Dingen: eine **Projektion** schreibt ein Read-Model (replaybar), eine **Funktion** sendet
Commands oder schreibt Ergebnis-Events (wirkt in die Welt, nie blind neu abgespielt). Beide sind durable Konsumenten des Logs und folgen
demselben Muster: der Wecker am Commit weckt schnell, der Takt je Partition heilt verlorene Weckrufe, gelesen wird geordnet aus dem Event-Store (Invariante 1), der
Fortschritt liegt durabel im Store. Was ein Konsument ist, folgt aus seiner Code-Form (Marker-Interface, Fähigkeits-Parameter, Rückgabetyp);
einen zweiten Marker gibt es nicht. Zeit kommt über die **Uhr**: ein Decide setzt eine Frist, die Uhr sendet sie fällig.

| Konsument | Code-Form | Schleife | Fortschritt | Neu abspielbar |
|---|---|---|---|---|
| Projektion | `IProjektion`; `Task Auf(Event[, Herkunft], Schreib-Fähigkeiten eines Stores)` | `BatchLeser` je (Projektion, Partition) | Fortschritt (Position, Revision, Generation) und Guard je Stream — beim Store der Klasse 1 in seiner Transaktion, sonst beim Framework (§4.3) | ja (Neuaufbau über Generationen) |
| Funktion | `IFunktion` mit `Auf(Eingang[, Herkunft][, Lese-Fähigkeiten])` | `FunktionsActor` je (Funktion, Partition) | Funktions-Stand (Position, Revision) und festgehaltene Ausgabe je Eingang | nein |

**Abläufe sind kein eigener Baustein mehr.** Was früher ein Prozess, eine Saga oder ein Fluss war, ist heute ein **Aggregat**, das den Ablauf
entscheidet (Zustand, Join, Kompensation, Frist), plus **Funktionen**, die die Kanten zwischen Aggregaten ziehen (Event → Command). §4.5 zeigt
das am Auftrag der Prüfdomäne.

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
so weit ist (Read-your-writes, Plan Pub/Sub-Neustrukturierung §2.7); reicht die Frist nicht, trägt die Antwort `noch_nicht`. Welche Ids eine
Antwort zeigt, findet der Generator selbst: jedes `Id<T>`-Feld der Antwort (auch geschachtelt, in Listen) und der Query, nach der gefragt wurde;
welche Projektionen hinter einer Frage stehen, folgt aus ihren Lese-Fähigkeiten (die Projektionen, die in denselben Store schreiben) — bei mehreren
gilt je Stream der kleinste Stand. Liegt der Fortschritt im Postgres des Logs (Marten-Store, Fortschritt beim Framework), liest eine Abfrage Guard und
Log zusammen (`IFortschrittImLog`).

Nach jedem Commit, der Events anwandte, meldet der Leser „P hat die Streams S weiterverarbeitet“ an die Verteiler (`Verteilung.MeldeVerarbeitet`) —
Mechanik nach dem Commit, die Projektion gibt dabei nichts aus. Eine Sitzung, die eine Antwort hält, die einen dieser Streams zeigt (oder, ohne
Ids, eine Antwort von P), nennt dem Client nach höchstens 200 ms die betroffenen Queries (`Aktualisiert`); er fragt neu. Die transitive
Abhängigkeit (eine Datensatz-Ansicht zeigt Labels von Bildpaaren) hat die Projektion beim Falten hergestellt — der Client kennt sie nicht.

**Position und Nachzügler.** Vor jeder Seite bestimmt `ReadPartitionAsync` die *gesetzte* Sequenz: bis dorthin ist jede Sequenz endgültig — der
Horizont der Vollständigkeit aus den Transaktionsnummern von Postgres, ohne Uhr ([03](03-schreibseite.md) §3.3, G-9; dieselbe Regel wie beim
Funktions-Actor, §4.4). Die Position rückt nie darüber hinaus, bei voller Seite nie über deren Ende und
nie über das erste Event eines hängenden Streams (unten). Jüngere Events werden trotzdem angewandt (Latenz); welche schon erledigt sind, merkt
sich die Aktivierung und schließt sie beim nächsten Lesen aus, nach einer Neuaktivierung fängt der Guard sie ab. So überholt die Position keine
noch unsichtbare kleinere Sequenz. Die Liste der Erledigten ist gedeckelt (`BatchLeser.ErledigtHoechstens`, 5 000, PF-28): steht der Horizont
(eine lange offene Transaktion irgendwo im selben Postgres) oder hängt ein Stream, wüchse sie sonst mit jedem Event und ginge mit jeder Seite
an die Datenbank. Ist sie voll, wendet der Leser nur noch an, was bis zur gesetzten Sequenz reicht, und wartet sonst; ein hängender Stream wird
dann geparkt statt hinter ihm weiter zu sammeln. Gesetzte Sequenz und „steht dahinter überhaupt ein Event?“ kommen aus **einer** Abfrage; steht hinter der
Position nichts, entfällt die Seite — eine Weckung ohne Neues kostet eine Abfrage statt zwei (PF-14; ebenso der Funktions-Actor). Gelesen wird ab einer Lese-Position im Speicher: durabel wird sie mit jedem Commit eines Batches; über fremde
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

**Weckung** (Plan §2.3, `Infrastructure/Wecken/`). Es gibt **eine** Stelle, an der nach einem Commit geweckt wird: nach dem `SaveChanges` des
Group-Commits (`MartenEventBatchWriter`) und des Einzel-Appends (`MartenEventStore`, auch der Rückfall `IsolateAsync` und der Betrieb ohne
Bündeln). Alle Schreiber gehen durch sie — Aggregat, Quelle, festgehaltene Ausgabe einer Funktion. Der **Weckplan** (`WeckplanGenerator`, nur aus
Signaturen) sagt je Event-Typ, wen der Commit weckt: den Batch-Leser jeder Projektion, die den Typ hört (`batch-<Projektion>`, Partition des
Streams), jeden Funktions-Actor, der ihn aus dem Log hört (`funktion-<F>`, Partition bzw. die eine Adresse), und die Uhr bei `FristGesetzt`. Der
**Wecker** schickt je Commit und Ziel genau einen `Wake` (ohne Inhalt, verlierbar); je Ziel ist beim Absender höchstens einer unterwegs, was
währenddessen kommt, wird zu genau einem weiteren (`Weckungen`). Das Ziel quittiert sofort und merkt sich während eines Durchlaufs „noch
einmal“ (`RahmenActor.Wecke`). Der **Takt** (`BatchKonsumStartupService`) weckt alle 30 s jede Partition — das Netz für verlorene Weckrufe; beim
Start prüft er, dass jeder registrierte Hörer mit jedem gehörten Typ im Plan steht (Boot-Fehler sonst, GR-WECKPLAN; dass ein Hörer aus dem Log nur
konkrete, persistente Events hört, sichert im Build die Form: CQRS057 Projektion, CQRS100 Funktion). Weckplan und Batch-Kinds lesen eine Projektion
über dieselbe Lesart (`Projektionsform`) — sie können nicht auseinanderlaufen. Ein Commit, dessen Ausgang mehrdeutig blieb (G-3), weckt nicht — ihn holt der Takt. Die
Batch-Leser passivieren nicht (es gibt nur Projektionen × Partitionen davon, der Takt weckt sie ohnehin).

**Rückstand.** `/health` und `/monitoring/metrics` melden über alle Projektionen die relevanten Events jenseits der Positionen
(`KonsumRueckstand`, eine Abfrage je Projektion, gezählt über höchstens die ersten 1 000 Events hinter der kleinsten Position — darüber
„mehr“, PF-29; ein volles `count(*)` las bei einer Partition ohne Checkpoint je Abruf das ganze Log) und die geparkten Streams.

### 4.1.1 Last ohne Eingang

Kommt nichts hinein, arbeiten nur die Takte (gemessen auf dem Messserver, Diagnose `--art leerlauf`, docs/12 §12.5, PF-14):
- **Batch-Poll** alle 30 s je Projektion und Partition eine Weckung, ohne Neues eine Abfrage (Beispieldomäne: 5 Projektionen × 8 Partitionen =
  40 Abfragen je 30 s — er wächst mit Projektionen × Partitionen, PF-15);
- **Funktions-Poll** ebenso alle 30 s je Funktion und Partition;
- **Uhr** jede Sekunde die DB-Uhr und die fälligen Fristen (eine Abfrage über den Index von `cqrs_fristen`).

**Nachlauf.** Nach einer Last schreiben Batch-Leser und Funktions-Actors ihre Position über fremde Events einmal fest; danach ist der Host still.
Die Messung wartet diesen Nachlauf ab (alle Partitions-Stände am Kopf des Logs) und weist ihn eigens aus.

## 4.2 Woraus folgt, was ein Konsument ist

Eine **Projektion** ist eine Klasse mit `IProjektion` und je gehörtem Event einem `Task Auf(TEvent e[, Herkunft h], Schreib-Fähigkeiten…[,
CancellationToken ct])` (Plan Pub/Sub-Neustrukturierung §3.3); ihre Kennung ist der Typname (`Projektionsvertrag.Kennung`). Gelesen wird sie über
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

Eine Funktion erkennt der `FunktionsBausteinGenerator` an `IFunktion` und den `Auf`-Methoden (§4.4). Sie darf Lese-Fähigkeiten nehmen, nie
Schreib-Fähigkeiten: was sie bewirkt, geht als Command an ein Aggregat oder als Ergebnis-Event ins Log.

## 4.3 Projektionen und Store-Klassen

Form (neutral aus der Agnostik-Sonde, `GraphExtractor.Kern/Sonde/Lesen.cs.txt`): `public sealed class Leihliste : IProjektion` mit
`public Task Auf(Ausgeliehen e, Herkunft h, INotiereAusleihe notiere) => notiere.SchreibeAsync(…)`. Eine Projektion gibt nichts aus und schreibt in
genau einen Store (CQRS052); sie rechnet bei gleicher Eingabe gleich — Zeit nur aus der `Herkunft` (R11). Was entsteht, liest der Generator nur
aus der Signatur (CQRS050), nie aus dem Rumpf. Read-Models, die ein Aggregat abbilden, tragen seine typisierte Id (`Id<T> Id`, dann `partial`:
der `ReadModelSchluesselGenerator` ergänzt die Speicher-Identität `Schluessel`, CQRS111 — Marten kann `Id<T>` nicht als Dokument-Identität);
Fähigkeiten nehmen `Id<T>`.

**Theorie (der gültige Kern).** Exactly-once-*Zustellung* gibt es nicht (Wirkung und Quittung lassen sich über eine Grenze nie atomar
koppeln); erreichbar ist exactly-once-*Wirksamkeit*. Das Framework schreibt keine Datenform vor; von einem Store braucht es nur eine von drei
Zusagen (Plan §2.4, `Abstractions/Projektionsziel.cs`), und jeder Store implementiert **genau eine**:

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

## 4.4 Funktionen

**Form.** Eine Funktion ist eine Klasse (läuft im Host) oder eine Schnittstelle (läuft draußen, ihre Umsetzungen sind Klassen) `: IFunktion`
mit Methoden `Auf(Eingang[, Herkunft][, Lese-Fähigkeiten])` — je Eingangstyp genau eine (R2, CQRS101). Was herauskommen kann, steht in der
Rückgabe: ein konkreter Typ, `OneOf<…>`, `T?`, `IEnumerable<…>` oder `IAsyncEnumerable<…>` (auch in `Task<…>`); erlaubt sind Commands und
Events und Hinweise. `Herkunft` nennt Stream, Version, Zeit und Akteur des Eingangs und leitet neue Ids ab (`h.NeueId<T>(…)`, R13). Eingänge sind
Events aus dem Log (auch Quell-Nachrichten und Ergebnisse anderer Funktionen) und **Hinweise** (`ITransientEvent`) aus dem Hinweis-Netz (unten);
aus einem Hinweis gehen nur Commands und Hinweise hervor, keine Events (CQRS100). Eine Funktion mit Zustand darf aus einem Eingang allein
lernen (`void`/`Task` ohne Ausgabe). Der `FunktionsBausteinGenerator` erzeugt Dispatch, Seiten-Form, den **Weckplan** (welche Log-Typen welche
Funktion wecken) und den **Hinweisplan** (welche Hinweis-Typen welche Funktion hören) als Tabelle `GeneratedFunktionsBausteine` (CQRS100).
Lage: Klasse und Schnittstelle in der Rolle Abläufe (`HostFunktion` bzw. `Funktion`, docs/07); ein Hinweis, den eine Funktion ausgibt, ebenda.

Beispiel (Beispieldomäne, `Domain.Pipeline/Sammelvorgang/TeilFertigMeldung.cs`) — die kleinste Funktion, eine Kante Event → Command:
```csharp
public sealed class TeilFertigMeldung : IFunktion
{
    public MeldeTeilFertig Auf(TeilauftragAbgeschlossen e) => new(e.SammelvorgangId);
}
```

**Funktions-Actor** (`Infrastructure/Funktionen/FunktionsActor.cs`, ein `RahmenActor` je Funktion und Adresse — die Partition, bzw. die eine
Adresse einer Funktion mit `[EineAdresse]`, die dann alle Partitionen liest): liest seine Seite ab der
Position aus dem Log, startet so viele Eingänge, wie Slots frei sind (Standard 1), je Stream höchstens einen (`FunktionsLauf`), und rückt die
Position nur bis vor den ersten laufenden Eingang und nie über die gesetzte Sequenz (Horizont wie §4.1), bei voller Seite nie über deren letztes
Event. Was von der Seite noch nicht starten kann, bleibt als **Vorrat** (höchstens eine Seite) und startet, sobald ein Slot frei ist; neu gelesen
wird erst, wenn der Vorrat nichts Startbares mehr hat — eine Seite trägt alle ihre Eingänge (G-18: vorher las ein Actor mit einem Slot nach jedem
Eingang eine volle Seite neu, ≈ 100 Eingänge/s je Partition). Der Stand liegt im Partitions-Checkpoint
mit der Id `funktion:<F>:<Partition>` und Revision (zwei Aktivierungen: die zweite bekommt einen Konflikt und lädt neu); so zählt `/health` den
Rückstand der Funktionen mit. Ein Eingang, der wirft, wartet im Backoff 1 … 30 s; nach den eingestellten Wiederholungen wird sein Stream geparkt
(Dead-Letter) und je Poll-Intervall einzeln versucht.

**Festhalten** (`FunktionsArbeit`, `Festhalten.cs`). Eine **gleichbleibende** Funktion — Klasse ohne Konstruktor-Parameter und ohne
Fähigkeiten, die nur Commands ausgibt — rechnet beim Wiederholen dasselbe (R12, CQRS103 verbietet Uhr, Zufall, Datei, Netz); sie sendet
direkt, mit abgeleiteten Command-Ids (Funktion, Eingang, Stelle, Typ, Ziel). Jede andere **hält ihre Ausgabe fest**, bevor etwas wirkt: ein
Stream je (Funktion, Eingang) nimmt die Events der Ausgabe, je Command ein `BefehlFestgehalten` und als Ende `AusgabeFestgehalten`. Wer
zuerst festhält, gewinnt (Anhängen ab der erwarteten Version); ein zweiter Lauf verwirft seine Rechnung und sendet das Festgehaltene. Ein
Strom wird in Stücken zu 100 festgehalten, ein Abbruch setzt bei der nächsten Ausgabe fort. Hinweise (`ITransientEvent`) schickt nur der
Lauf, der festhielt. Commands gehen immer über den `CommandEmitter` (§4.6); ein unbekannter Ausgang heißt „später“, die Position bleibt.

**Laufort ist Konfig, nicht Code.** Die Umgebungs-Datei sagt je Funktion `Funktionen:<F>:Laufort` (`Host` · `Extern` · `Hand`), dazu
`Implementierung`, `Slots`, `Wiederholungen` (`Abstractions/Umgebung/UmgebungKonfig.cs`); `AddCqrs` bindet daraus beim Start
(`BindeFunktionen`), danach die Domänen-Dienste der Umsetzungen (`BindeDienste`, `GeneratedDienste`). Ohne Eintrag läuft eine Klasse im Host,
eine Schnittstelle mit genau einer Umsetzung ebenso, eine Schnittstelle ohne Umsetzung, die ein Client trägt (`ITraegt<F>`), draußen; sonst ist
es ein Boot-Fehler, der den Eintrag nennt. Eine Funktion mit Lese-Fähigkeit läuft nur im Host.
- **Host:** die Umsetzung **je Aktivierung** (`FunktionsAktivierung`): beim Aktivieren in einem eigenen DI-Bereich gebaut, samt ihren Diensten
  (im Bereich gebunden — eine Datenbank-Verbindung, ein geladenes Modell), beim Stoppen, Passivieren und vor einem Neustart in umgekehrter
  Reihenfolge freigegeben (`IAsyncDisposable`/`IDisposable`; `RahmenActor.FreigebenAsync`). Bei Slots > 1 teilen sich die Eingänge einer
  Aktivierung die Instanz (thread-sicher).
- **Extern und Hand:** der Actor bietet den Eingang über den **Vermittler** seiner Funktion an (`FunktionsVermittlerActor`, ein virtueller
  Cluster-Actor je Funktion; `EingangAnbieten`, nicht maßgeblich — geht er verloren, bietet der Actor neu an). Ausführer draußen holen per Pull,
  so viele wie Slots frei sind, mit **Lease** und Lebenszeichen, über eine `BausteinSitzung`: extern die Sitzung eines Clients über die Leitung
  (gRPC, z. B. der generische Python-Worker, der die Funktion trägt), von Hand der `HandAusfuehrer` (Werkbank, JSON-Eingang
  `/eingang/auftraege` und `/eingang/ergebnis`). Die Sitzung prüft die Antwort gegen die Signatur und hält sie fest — die erste gewinnt; ein
  Strom kommt in Stücken, jedes an seiner Stelle festgehalten. Die Garantie bleibt beim Actor: läuft die Lease ab, geht der Eingang an den
  nächsten Ausführer, der beim nächsten nicht festgehaltenen Stück weitermacht; wartet der Actor vergeblich, heißt der Eingang „später“. Jedes
  Angebot trägt die **Adresse** des Funktions-Actors: alle Eingänge einer Adresse rechnet derselbe Träger, solange er lebt (holt oder Lebenszeichen
  gibt) — ein Zustand im Worker sieht so alles, was zu ihm gehört (A8); eine freie Adresse bindet der Holende, wenn er nicht schon mehr trägt als
  ein anderer lebender Träger. Endet eine Sitzung, meldet sie sich ab (`TraegerAbgemeldet`) und ihre Adressen sind sofort frei.

**Zustand** (`IZustand<T>`, Plan §2.9, A5, A6). Steht `IZustand<T>` im Konstruktor einer Funktions-Klasse, hat sie gekapselten, durablen Zustand —
kein Domänen-Zustand, keine Zustands-Events, keine Faltung aus dem Log; Vorbild ist der Aggregat-Actor:
```csharp
[EineAdresse]                                                   // ein Zustand für alle Messstellen (sonst einer je Partition)
public sealed class Schaetzer(IZustand<Schaetzmodell> modell) : IFunktion
{
    public void Auf(MesswertErfasst e, Herkunft h) => modell.Wert.Lerne(h.Stream, e.Wert);           // lernt aus dem Log
    public VermerkeSchaetzung Auf(Merkmal m) => …;                                                 // Hinweis rein, Command raus
}
```
(Prüfdomäne `Pruefdomaene.Pipeline/Messung`.) Der Generator erkennt den Konstruktor-Parameter, reicht den Zustand aus der Aktivierung hinein (nie aus
dem Container) und sichert ihn über den Speicher-Kontext der Komposition (`EventJsonSerializerContext`, der Codegen nimmt jedes `T` auf — ohne
Reflection). Der Actor lädt Zustand und Stand beim Aktivieren, bevor das erste `Auf` läuft; er rechnet **einen** Eingang zugleich (Log oder Hinweis);
er sichert nur in einem **Ruhepunkt** — kein Eingang in Arbeit, der Zustand enthält genau die Log-Eingänge bis zur Position — Zustand und Stand
in **einer Transaktion**, beide mit erwarteter Version (`FunktionsZustandsDokument` je Adresse, Id `zustand:<F>:<Adresse>`). Ein großer Zustand
liegt als Datei in einer Ablage der Umgebung (`Funktionen:<F>:Zustand:Ablage = Datei`, `Verzeichnis`): erst die Datei, dann Position und Verweis;
ein Absturz dazwischen lässt nur eine verwaiste Datei, die gleich wieder entfernt wird. Gesichert wird höchstens so oft wie
`Zustand:SichernAlleSekunden` (Standard 1 s, wie der Stand) und beim Stoppen, sofern gerade Ruhe ist. Nach einem Absturz liest die nächste
Aktivierung ab der gesicherten Position erneut: `Auf` läuft auf dem gesicherten Zustand auch für Eingänge, deren Ausgabe schon festgehalten ist
(gesendet wird das Festgehaltene, Hinweise nicht noch einmal) — jeder Log-Eingang wirkt so **genau einmal** auf den gesicherten Zustand; in
derselben Aktivierung läuft `Auf` je Eingang genau einmal, auch beim Wiederholen (`EingangsRechnung`). Verliert eine Sicherung gegen eine zweite
Aktivierung derselben Adresse, beginnt die Aktivierung über die Supervision neu (Freigabe, Bau, Laden des Gewinner-Stands, Lebenszyklus). Eine Funktion mit Zustand ist nie gleichbleibend (sie
hält fest, R12); Zustand in Feldern oder Eigenschaften verbietet CQRS105 (R19) — er ginge beim Umzug still verloren. Rechnet eine Funktion draußen,
lebt ein Zustand im Worker (A8, gebunden an die Adresse); ihn zu sichern, ist Sache des Workers.

**Lebenszyklus** (`ILebenszyklus`, optional, E10). Muss eine Funktion beim Aktivieren etwas laden — ein Netz auf die GPU, eine Verbindung —,
implementiert sie `ILebenszyklus` und überschreibt, was sie braucht: `BeimAktivierenAsync` läuft je Aktivierung nach dem Bau und dem Laden des Zustands,
vor dem ersten `Auf` (bis dahin warten Eingänge und Hinweise in der Mailbox); `VorDemSichernAsync` läuft vor jeder Sicherung im Ruhepunkt und beim
Stoppen (Gewichte von der GPU nach `Zustand.Wert`); das Ende ist `IAsyncDisposable` der Funktion, danach werden ihre Dienste frei. Wirft
`BeimAktivierenAsync`, ist die Aktivierung gescheitert, nicht ein Eingang: sie beginnt neu, kein Stream wird geparkt. Die geladene Ressource steht in
einem `readonly`-Feld; was gesichert werden muss, gehört in `IZustand<T>`. Beispiel ist der Schätzer der Prüfdomäne, der auf einem Arbeitsmodell der
Aktivierung rechnet. Eine Funktion mit Lebenszyklus ist nie gleichbleibend; CQRS107 erlaubt ihn nur an Funktions-Klassen und `VorDemSichernAsync` nur
mit Zustand. Draußen hat die Python-Basis die Gegenstücke `beim_start` (einmal je Worker, vor dem Anbieten) und `beim_ende`.

**Hinweis-Netz** (`HinweisNetz.cs`, Plan §2.9, E7, E8). Hinweise, die nicht zur Fachdomäne gehören (Merkmale, Messwerte, Ablehnungen), gehen im
Speicher von Baustein zu Funktions-Actor — nie in den Log, kein Broker, keine Abos. Die Routing-Tabelle ist der generierte Hinweisplan (Hinweis-Typ →
Funktionen); gibt eine Funktion, ein Aggregat (eine Ablehnung aus dem Decide) oder ein Client (`TransientEvent` über die Leitung) einen Hinweis aus,
schickt der Rahmen ihn per `Bote.Melde` direkt an jede Adresse: die Partition seines Herkunfts-Streams bzw. die eine Adresse (A6). Er trägt eine
**Kennung**, die der Absender ableitet (A7: aus Funktion, Eingang und Stelle; aus der Command-Id bei einem Aggregat). Im Funktions-Actor hat ein
Hinweis keine Position: er wartet in einer gedeckelten Schlange auf einen Slot (mit Zustand auf den Ruhepunkt) und läuft **höchstens einmal**;
Commands daraus tragen eine Id aus Funktion, Hinweis-Kennung, Stelle, Typ und Ziel — bleibt einer „unbekannt“, sendet der Actor dieselben
Commands erneut, ohne neu zu rechnen. Eine Funktion, die nur Hinweise hört, hat keinen Log-Leser, keine Position und keinen Takt. Draußen geht ein
Hinweis denselben Weg wie ein Log-Eingang (Vermittler, Sitzung, `ArbeitsAuftrag` mit `hinweis = true`), nur wird die Antwort nicht festgehalten:
sie kommt mit ihren Commands und Hinweisen zum Actor, der sendet. Hört niemand einen Hinweis, den eine Funktion hören will, bricht der Build (R20,
CQRS106); eine Adresse der Tabelle ohne Funktion ist ein Boot-Fehler. Hinweise an Clients gehen über die Verteiler an die Sitzungen, die sie hören.

**Wecken.** Jede Funktion mit Log-Eingängen meldet sich wie eine Projektion beim `BatchKonsumStartupService` an (Takt, Prüfung gegen den
Weckplan); der Wecker am Commit weckt ihre Partition bzw. ihre eine Adresse (§4.1). Den Clients meldet jeder Schreiber nach seinem Commit
den Envelope je Event an die Verteiler (`Infrastructure/Verteilen/Commitmeldung.cs`).

## 4.5 Abläufe: Aggregat, Funktion und Frist

Ein Ablauf mit Zustand — warten, zusammenführen, zählen, zurücknehmen, eine Frist überwachen — ist ein Aggregat: sein Decide entscheidet jeden
Schritt aus seinem Zustand, sein Log ist der Ablauf. Die Kanten zu anderen Aggregaten zieht eine Funktion, die selbst nichts festhält. Die
Zugehörigkeit eines fremden Events zu einem Ablauf ist ein **fachliches Feld** (A2), kein Korrelations-Header.

**Beispiel: der Auftrag der Prüfdomäne** (`Pruefdomaene/Auftrag/`, `Pruefdomaene.Pipeline/Auftrag/Auftragsabwicklung.cs`). Ein Auftrag
reserviert einen Posten und belegt ein Kontingent (zwei unabhängige Zweige); erst wenn beide gelungen sind, ist er bestätigt; scheitert einer,
wird zurückgenommen, was gelang.
- `StarteAuftrag` → `AuftragGestartet`. Die Funktion `Auftragsabwicklung` macht daraus `ReservierePosten` und `BelegeKontingent`, beide mit dem
  **Beleg** `(Belegart.Auftrag, AuftragId)` als fachlichem Feld.
- Posten und Kontingent entscheiden für sich (`PostenReserviert`, `ReservierungAbgelehnt` …) und tragen den Beleg im Event. Die Funktion bildet
  sie auf `VermerkeReservierung`, `VermerkeBelegung` bzw. `VermerkeAbsage` am Auftrag ab — nur, wenn der Beleg ein Auftrag ist.
- Das Decide des Auftrags entscheidet **Join** und **Kompensation**: sind Reservierung und Belegung vermerkt, folgt `AuftragBestaetigt`; kommt
  eine Absage, folgt `AuftragGescheitert` und für jeden gelungenen Zweig ein Gegenzug-Event (`PostenFreigabeAngeordnet`) — genau einmal, weil
  der Zustand es sich merkt; eine verspätete Rückmeldung eines Zweigs nimmt sich selbst zurück. Die Funktion sendet den Gegenzug (`GibPostenFrei`).

**Fan-out und Zählung** (`Verteilung`): `ZubuchungenAngeordnet` trägt die Ziele, die Funktion sendet je Ziel `BuchePostenZu` (ein Strom von
Commands), und jede Zubuchung mit dem Beleg der Verteilung wird dort gezählt (`ZaehleZubuchung`); beim N-ten schließt das Decide ab. Es gibt
keine Collections im Aggregat, nur einen Zähler (Haltung B); die Breite steht im Start.

**Frist statt Zeitlimit.** Ein Decide gibt `Frist<TCmd>(Befehl, Nach)` aus; die Uhr (§4.7) sendet den Befehl fällig an dasselbe Aggregat, und
dessen Decide entscheidet, ob er noch etwas bedeutet. Beispiel: der Trainings-Wächter der Beispieldomäne setzt beim Start eines Trainingslaufs
eine Frist und erklärt den Lauf bei Ablauf ohne Lebenszeichen für gescheitert.

**Ohne Dirigent.** Es gibt keinen Fold über fremde Streams, keinen Marking-Cursor, keinen Backstop und keine Prozess-Korrelation mehr. Was
garantiert ist, folgt aus den Bausteinen: das Aggregat entscheidet unter Single-Writer und Inbox, die Funktion liefert jeden Eingang
mindestens einmal und ihre Commands über die Inbox genau einmal wirksam, die Uhr sendet jede Frist mindestens einmal. Garantien und Nachweise:
[11](11-garantien.md). Ein Kreis Funktion ↔ Aggregat ist der Normalfall eines Ablaufs; der Build warnt davor nicht (R6, docs/13).

## 4.6 Das Emit-Primitiv und die Inbox

Jeder Command, den das Framework sendet (Funktion, Uhr), geht durch das **eine** Emit-Primitiv `CommandEmitter.EmitAsync`
(`Infrastructure/PubSub/CommandEmitter.cs`):
- die `CommandId` gibt der Aufrufer deterministisch vor (Funktion: aus Funktion, Eingang, Stelle, Typ und Ziel; Uhr: aus Stream und Version
  des Frist-Events) → stabil über jede Wiederholung;
- `CommandModus.Emittiert()` = **keine erwartete Version**;
- eine **Frage** des Boten mit Pflicht-Frist (Standard 5 s; [03](03-schreibseite.md) §3.6), at-least-once, ohne eigenen Retry;
- liefert den **Ausgang** (`EmitAusgang`): `Zugestellt` (Erfolg, auch als erkannte Wiederholung), `Abgelehnt` (fachlich, durabel als Quittung in der Inbox),
  `Unbekannt` (Frist abgelaufen, technischer Fehler, Transportfehler). Keine Ausnahme verlässt den Emitter. Wer „unbekannt“ bekommt, wiederholt
  an seinem durablen Punkt mit derselben CommandId: die Funktion lässt den Eingang offen (die Position bleibt), die Uhr lässt die Frist liegen;
- **Drossel je Ziel** (`EmitDrossel`): höchstens 8 Sends je Ziel-Aggregat gleichzeitig unterwegs (je Knoten); der Rückstau eines heißen
  Aggregats liegt vor dem Send statt in seiner Mailbox (PF-9).

**Dedup macht die Framework-Inbox, nicht der Fachcode.** Die Inbox ist eine Tabelle des Event-Stores (`cqrs_inbox`, `InboxTabelle`,
[03 §3.3](03-schreibseite.md)): jeder Append des Aggregat-Actors trägt die Quittung seines Commands (gewirkt oder abgelehnt), geschrieben in
derselben Transaktion wie die Events und eindeutig je CommandId. Eine Wiederholung scheitert am Schlüssel, nichts wird geschrieben, der Actor
quittiert den Ausgang des ersten Mals. Vor dem Append erkennt ein kleiner Cache im Actor (`BoundedInbox`, je Ausgang 1 000 CommandIds) die
häufige Wiederholung kurz nach dem ersten Mal schon **vor** der OCC-Prüfung. Exactly-once gilt innerhalb des Horizonts
(`Schreibpfad.InboxHorizontTage`, Standard 30 Tage).

**Keine Marken im Log** (Plan Pub/Sub-Neustrukturierung Phase 5). Ein Noop und eine fachliche Ablehnung schreiben kein Event, nur ihre
Quittung (gewirkt bzw. abgelehnt) — die Version des Ziels bleibt, ein Aggregat hat **eine** Version, die Zahl seiner Events. Eine Wiederholung
derselben CommandId bekommt denselben Ausgang, auch nach einem Neustart (Inbox vor OCC). Die Quittung ohne Events gilt nur auf dem Stand, auf dem
entschieden wurde (Kopf-Prüfung, [03 §3.3](03-schreibseite.md)). Die Ablehnung selbst ist ein Hinweis: gezielt an die Sitzung des Aufrufers und
an die Funktionen, die sie hören (verlierbar). Braucht ein Ablauf sie, ist sie ein Fakt (Event, etwa `ReservierungAbgelehnt` der Prüfdomäne).

**Analyzer-Zwang** (`CommandEmitAnalyzer`): **CQRS020** (`RequestAsync<CommandResult>` oder `FrageAsync<CommandResult>` außerhalb von
Emitter/Dispatcher), **CQRS021** (roher Command-Send mit `CancellationToken.None`/`default`).

## 4.7 Uhr und Quellen

**Uhr** (`Abstractions/Uhr.cs`, `Infrastructure/Uhr/`). Ein Decide gibt `Frist<TCmd>(Befehl, Nach)` aus; der generierte Handler macht daraus
das Event `FristGesetzt` (ein `IRahmenEvent`), und der Actor legt in derselben Transaktion eine Beilage ins Fälligkeits-Verzeichnis
`cqrs_fristen` (fällig = Zeitstempel des Events + Dauer, Datenbank-Uhr). Die Uhr ist ein Actor einmal im Cluster (`uhr/0`), jeder Knoten tickt
sie jede Sekunde an (`UhrTakt`, verlierbar). Sie liest die fälligen Fristen über den Index (`FristVerzeichnis.FaelligeAbfrage`), sendet sie mit
der Command-Id aus Stream und Version des Frist-Events und trägt sie erst nach entschiedenem Ausgang aus — doppelt gefeuert wirkt es über die
Inbox des Ziels einmal. In Sim verschiebt ein Uhr-Versatz die Datenbank-Uhr (Durchspielen).

**Quellen** (`Infrastructure/Quellen/`). Was von draußen hereinkommt, ohne ein Command zu sein, ist eine **Quell-Nachricht**
(`IQuellNachricht` mit `Kennung`): der Datei-Treiber (`DateiGemeldet`), der Takt-Treiber (`TaktGemeldet`, Konfig `Intervall` ≥ 1 s; Kennung =
Beginn des Intervalls, gleich auf jedem Knoten) und die Quell-Nachricht vom Client (`ClientGemeldet`, JSON-Eingang `/eingang/quelle`, Kennung =
Command-Id des Clients; über gRPC erst mit Phase 7). `QuellEingang.NimmAufAsync` hängt sie **genau einmal je Kennung** an: je Quelle wählt die
Konfig `Strom` — `JeNachricht` (ein Stream je Kennung, Id deterministisch aus Typ und Kennung, Anhängen ab Version 0) oder `JeSchluessel` (ein
Stream je Schlüssel; die Kennung steht in derselben Transaktion im Kennungs-Verzeichnis `cqrs_quell_kennungen`, angehängt am Kopf mit
Versionsprüfung). Danach meldet der Eingang den Commit (§4.4); Funktionen nehmen Quell-Nachrichten wie jedes andere Event aus dem Log.

## 4.8 Dead-Letter & Snapshots

- **Dead-Letter:** `MartenDeadLetterSink` schreibt ein durables `DeadLetter`-Dokument je toter Zustellung (Schema `dlq`), best-effort —
  Beobachtung, kein kritischer Pfad (z. B. ein geparkter Stream einer Projektion oder Funktion, ein vergifteter Aggregat-Actor). Lesen: `MartenDeadLetterReadStore`.
- **Snapshots:** `MartenSnapshotStore` — `Snapshot<TState>` als jsonb je Aggregat, Registrierung generiert (`SnapshotRegistrationGenerator`).
  **Nicht-autoritativ:** kein Treffer oder eine andere `SchemaVersion` → Voll-Replay (`AggregateRehydrator`). Die Schema-Version ist der
  Struktur-Hash des States plus der Code-Hash von State und Applier (`{State}AggregateHandler.CodeHash`) — eine geänderte Apply-Logik
  invalidiert alte Snapshots. Geschrieben wird nach einer Schwelle von Events und bei Deaktivierung: der State wird im Actor-Turn
  kopiert (die Inbox reist nicht mit — sie ist eine Tabelle), der DB-Write läuft danach fire-and-forget. Die Kopie ist ein JSON-Round-Trip (docs/13 W10-2).

## 4.9 Spur der Konsumenten

Jeder Log-Eintrag trägt die Header `ursache`, `wirker`, `ueber`, `stand` (`Abstractions/Spur/Spur.cs`, geschrieben in
`Infrastructure/Persistence/SpurHeader.cs`). Die Konsumenten setzen sie per `Spur.Von(…)` um ihren Ausgang; der `CommandEmitter` legt Ursache
und Wirker in den Command-Envelope, das Ziel-Aggregat schreibt mit `wirker = decide:<Aggregat>/<Command>` und dem Absender als `ueber`:
- **Funktion:** Ursache = der Eingang (`Stream:Version`), Wirker `funktion:<F>`; der Akteur der Kette reist mit. Festgehaltene Ergebnis-Events
  stehen mit derselben Spur im Ausgabe-Stream.
- **Uhr:** Ursache = das Frist-Event, Wirker `uhr` (ohne Karte).
- **Quelle:** `QuellEingang` schreibt mit `quelle:<Typ>` und ohne Ursache (Wurzel).
- **Akteur:** ein Command von draußen trägt `ueber = akteur:<Akteur>`.
- **Projektion** schreibt nie ins Log; `projektion:<Typ>` ist nur Karten-Id.

Das Format steht an einer Stelle (`Abstractions/Spur/WirkerId.cs`), den Weg eines Eintrags über die Karten rechnet `SpurWeg`.

## 4.10 Offen

Offene Punkte stehen nur in [13](13-reifegrad-schulden-bewertung.md): Reihenfolge der Hinweise zwischen zwei Actors (G-15), echter Python-Prozess
mit Hinweis-Eingang im Testnetz (FB-8), Wechsel der Zustands-Ablage (FB-9), Kosten des Zustands und der Bindung (PF-34, PF-35), eigener Wecker am
Flush (Phase 6), Quell-Nachricht vom Client über gRPC (Phase 7), Kreise Funktion ↔ Aggregat (R6), hartkodierte Intervalle P2-8, reflektive
Snapshot-Kopie W10-2, Projektlage W2-1.
