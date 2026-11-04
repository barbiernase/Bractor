# Konzept: Projektionen im Editor — der Store von innen wie das Aggregat

Stand 2026-11-04, umgesetzt (docs/10 §10.9) bis auf die Schalter für `Herkunft` und `CancellationToken` an Auf und Frage (§6). Grundlage ist das Aggregat-Modal, wie es heute arbeitet (docs/10 §10.9): Karten mit Ports, drei
Detailstufen, Anlegen ohne Zuordnung, Partner 1:1 automatisch und auf gleicher Höhe, Sortierung nach neu, A–Z oder Eingängen. Dieses Konzept
überträgt Struktur und Vorgehen auf die Leseseite. Es beantwortet zwei Fragen: Welche Handhabungen laufen parallel (was gehört 1:1 zusammen und
steht deshalb auf einer Zeile, was ist frei)? Und welche Anforderungen aus dem Aggregat gelten hier ebenso, welche anders?

## 1. Der Block bleibt der Store

Im Aggregat ist das Modal ein Block mit eigenem Zustand: Commands gehen hinein, Events kommen heraus, der Zustand liegt rechts. Auf der
Leseseite ist das der **Store**, nicht die einzelne Projektion: Eine Projektion schreibt in genau einen Store (CQRS052), ein Reader liest aus ihm,
und der Zustand sind die Read-Models des Stores. Das Modal „🗄 IImagePairStore“ von heute bleibt also der Rahmen. Projektion und Reader sind die
Klassen, in denen die Handhabungen liegen. Sie stehen nicht als eigene Karten im Bild, sondern als Name an jeder Handhabung („Auf
BildVerfuegbar · ImagePairProjektion“), wie heute.

## 2. Die parallelen Handhabungen

Im Aggregat binden zwei Paare 1:1, Command ↔ Decide und Event ↔ Apply. Dazwischen liegt eine freie Beziehung: Ein Decide ergibt mehrere
Events, ein Event kann aus mehreren Decides kommen. Auf der Leseseite gibt es dieselbe Form, je Richtung einmal:

**Schreiben:** Event ↔ `Auf` · (frei) · Schreib-Fähigkeit ↔ Umsetzung im Store · Read-Model

**Lesen:** Query ↔ `Frage` · (frei) · Lese-Fähigkeit ↔ Umsetzung im Store · Read-Model, dazu die Antwort als Ausgang der Frage

- **Event ↔ Auf** ist 1:1 je Projektion (R2, CQRS101: je Event-Typ höchstens ein `Auf`). Das entspricht Command ↔ Decide.
- **Query ↔ Frage** ist 1:1 (R4: eine Query hat genau ein Ziel).
- **Fähigkeit ↔ Umsetzung** ist 1:1: Jede Fähigkeit hat genau eine Funktion (CQRS051) und die Store-Klasse genau eine Methode dafür. Das
  entspricht Event ↔ Apply. Auch das Apply ist die eine Stelle, an der etwas in den Zustand eingeht.
- **Auf ↔ Schreib-Fähigkeit** und **Frage ↔ Lese-Fähigkeit** sind frei, wie Decide ↔ Ergebnis. Das begründet der nächste Abschnitt.
- **Fähigkeit ↔ Read-Model** bindet ebenfalls nicht 1:1. Welches Read-Model eine Fähigkeit berührt, steht nur dann im Code, wenn ihre Signatur es
  nennt (`UpsertAsync(ImagePairReadModel)`, `FindByIdAsync → DatensatzReadModel?`). `SetKomplettAsync(Id<ImagePair>, DateTimeOffset)` nennt
  keins. Das Read-Model ist deshalb wie der Zustand im Aggregat eine eigene Spalte.

Daraus folgen dieselben zwei Paare wie im Aggregat: **Event · Auf** bzw. **Query · Frage** stehen oben bündig nebeneinander, ebenso
**Fähigkeit · Umsetzung**. Die Fähigkeiten folgen ihren Kanten, wie die Ergebnisse im Aggregat ihrem Decide folgen. Die Legefunktion des
Aggregats passt damit unverändert.

### 2.1 Analyse: nimmt ein Auf immer genau eine Schreib-Fähigkeit?

**Im Code: ja, ohne Ausnahme.** Über das ganze Repo nehmen alle echten `Auf` genau eine Schreib-Fähigkeit: 36 in der Beispieldomäne, 2 in der
Prüfdomäne, 6 in der Sonde, 10 in den Analyzer-Tests. Daneben gibt es nur Testfälle: fünf `Auf` ohne Fähigkeit (Analyzer-Tests, die etwas
anderes prüfen) und ein `Auf` mit zwei, der Negativtest für zwei Stores (CQRS052).
Umgekehrt hat fast jede Fähigkeit genau ein `Auf`: 24 der 26 Schreib-Fähigkeiten der Beispieldomäne. Die zwei Ausnahmen sind echte Teilung:
`AppendEintragAsync` nehmen alle zehn `Auf` der Historie, `SetStatusAsync` zwei.

**In den Regeln: nein.** Die Lesart (`Projektionsform.LiesAuf`) erlaubt beliebig viele Schreib-Fähigkeiten, auch keine, solange alle in
denselben Store schreiben. Lese-Fähigkeiten erlaubt sie nicht: Ein `Auf` kann nicht lesen.

**Warum der Bestand trotzdem 1:1 ist:** Weil ein `Auf` nicht lesen kann, braucht jede Änderung, die vom bisherigen Stand abhängt, eine
Fähigkeit, die genau diese Änderung im Store ausführt. In der Beispieldomäne stehen deshalb zwei Stile nebeneinander:

- **Zugeschnitten** (`ImagePairProjektion`, `DatensatzProjektion`): je Event eine Fähigkeit (`SetKomplettAsync`, `NimmPaarAufAsync`, …). Das
  `Auf` reicht nur Felder weiter. Die eigentliche Rechnung, wie sich das Read-Model ändert (`existing with { IstKomplett = true, … }`, Mitglieder
  hinzufügen und zählen), steht in der Store-Klasse in `Domain.Infrastructure`, verpackt in `EnqueueTransform` des Marten-Treibers.
- **Allgemein** (`ImagePairHistorieProjektion`): eine Fähigkeit `AppendEintragAsync` für alle Events. Die Rechnung (welcher Eintrag
  entsteht) steht im `Auf` in `Domain.Projections`.

**Ist 1:1 sinnvoll? Als Regel nicht, als Standard im Editor auch nicht.**

- Als **Regel** wäre es falsch. Ein Event, das zwei Read-Models desselben Stores ändert, ist legitim: ein Eintrag in die Historie plus der
  Stand, oder ein Mitglied im Datensatz plus der Rückverweis am Paar. `NimmPaarAufAsync` schreibt heute genau diese zwei Dokumente, versteckt in
  einer Fähigkeit (`BufferRueckwaerts`). Mit zwei Fähigkeiten im `Auf` stünde es offen in der Signatur. Eine Regel „genau eine“ würde die
  Zusammensetzung in den Store schieben. Und sie verböte dem Modellierer etwas, das die Laufzeit kann.
- Als **Standard im Editor** würde 1:1 den zugeschnittenen Stil festschreiben. Jedes neue `Auf` bekäme seine eigene Fähigkeit, und die Rechnung
  der Projektion landete jedes Mal in der Store-Klasse:
  - Sie hängt dort am Treiber und ist beim Wechsel der Store-Klasse neu zu schreiben (Marten → Datei-Ziel).
  - Sie ist nur gegen echtes Marten prüfbar, nicht im Prüfstand.
  - Sie liegt in der Speicher-Rolle statt bei der Leseseite.
  - Die Projektion selbst bleibt eine leere Weiterleitung, die Zahl der Fähigkeiten wächst mit der Zahl der Events.

  Im Aggregat ist es umgekehrt: Das Apply, also die Rechnung des Zustands, steht in der Domäne und ist rein.

**Folge fürs Konzept:** Der Editor bindet nur, was die Regeln binden: Event ↔ `Auf`, Query ↔ `Frage`, Fähigkeit ↔ Umsetzung. Die Fähigkeit
wählt man am Port des `Auf`, wie das Ergebnis am Decide: eine vorhandene des Stores (sie stehen hervorgehoben, die allgemeinen zuerst) oder
gestrichelt „＋ neue Schreib-Fähigkeit“. Ein `Auf` ohne Fähigkeit baut; sein Rumpf ist ein Platzhalter (W1). Ein `Auf` mit zwei Fähigkeiten zeigt
zwei Ports. Eine geteilte Fähigkeit steht als eine Karte mit mehreren Kanten hinein, so wie ein Event mit mehreren Decides.

Ob die Rechnung der Projektion grundsätzlich ins `Auf` gehört (allgemeine Fähigkeiten je Read-Model, etwa „ändere das Dokument mit dieser
Funktion“), ist eine Frage an das Framework, nicht an den Editor. Sie steht in §8 als eigene Entscheidung. Der Editor bleibt für beide Stile
offen.

## 3. Spalten

| Spalte | Schreiben (oberes Band) | Lesen (unteres Band) |
|---|---|---|
| Eingänge | Event | Query |
| Handhabung | `Auf` (mit Projektion) | `Frage` (mit Reader) |
| Fähigkeit | Schreib-Fähigkeit | Lese-Fähigkeit |
| Umsetzung | Methode der Store-Klasse | Methode der Store-Klasse |
| Read-Models | Zustand des Stores, gemeinsam für beide Bänder | |
| Antworten | – | Antwort der Frage (verlässt den Block) |

Schreiben steht oben, Lesen darunter, getrennt durch eine Linie, beide in denselben Spalten. So bleibt „links hinein, rechts hinaus“ wahr,
und die Read-Models stehen einmal in der Mitte beider Wege. Die Umsetzung ist neu als Karte: Heute steckt ihr Rumpf im Formular der Fähigkeit.
Als eigene Karte ist sie das Gegenstück zum Apply und trägt am Kopf die Store-Klasse (1 atomar, 2 wiederholungsfest, 3 mindestens einmal).

## 4. Anforderungen: was aus dem Aggregat übernommen wird

| Im Aggregat | Im Store |
|---|---|
| Doppelklick: nur Kopf ⇄ Ports, ✎ öffnet das Bearbeiten in der Karte | gleich; das eigene Formular des Glieds entfällt wie beim Aggregat |
| Klick hebt die Kette hervor, Bildlauf bleibt | gleich; die Kette läuft hier über beide Bänder (Read-Model verbindet Schreiben und Lesen) |
| Command entsteht immer mit Decide | ein Event bekommt sein Auf in einer Geste (das Event gibt es schon); eine Query entsteht immer mit Frage und Antwort. Die Fähigkeit wird gewählt, nicht mit angelegt |
| Event entsteht immer mit Apply | jede Fähigkeit entsteht mit ihrer Umsetzung (die Methode in der Store-Klasse, Platzhalter-Rumpf) |
| frei anlegen ohne Zuordnung, „unverbunden“, bis angeschlossen | Fähigkeit (mit Umsetzung), Read-Model und Antwort frei anlegbar; „unverbunden“, solange kein Auf bzw. keine Frage sie nimmt |
| ＋ am Port: Kandidaten hervorheben, gestrichelt Neues | ＋ Schreib-/Lese-Fähigkeit am Auf/an der Frage, ＋ Auf am Event, ＋ Antwort an der Frage, ＋ Frage an der Antwort |
| ⇅ in Commands und Ergebnisse: neu, A–Z, Eingänge | ⇅ in Eingänge und Fähigkeit, je Band; Eingänge = die Kanten, die in der Karte enden (bei einer Fähigkeit: wie viele Auf bzw. Fragen sie nehmen, eine allgemeine steht damit unten) |
| Zeitstempel beim Anlegen, nur in der Ansicht | gleich, Schlüssel je Handhabung (`Auf ImagePairProjektion.BildVerfuegbar`), je Fähigkeit (`IImagePairStore.SetKomplettAsync`) |
| keine Knöpfe in Decide und Apply | keine Knöpfe in Handhabung und Umsetzung; Anlegen nur über Eingänge, Fähigkeit, Read-Models |
| Rückgabe der Frage folgt ihren Antworten | gleich für die Frage im Reader (CQRS050) |

## 5. Anforderungen: was hier anders ist

**Events stammen aus dem Aggregat und sind hier fix.** Ein Event ist ein Fakt aus einem Decide; die Projektion kann nur darauf reagieren.
Im Store wird deshalb kein Event angelegt, umbenannt oder geändert:

- Die Event-Karte hat kein ✎ und kein ✕. Der Doppelklick schaltet nur die Detailstufe, die Felder stehen zum Lesen da.
- Ein Verweis „▣ im Aggregat öffnen“ führt zu seinem Aggregat. Geändert wird es dort.
- Der ＋ in „Eingänge“ (Schreiben) bietet nur vorhandene Events an, die die Projektion noch nicht hört (gestrichelt, wie die Übernahme im
  Aggregat). Ein Klick legt das `Auf` an, seine Fähigkeit wählt man danach am Port.
- Fehlt ein Event, entsteht es zuerst im Aggregat; danach steht es hier zur Wahl.

Gleiches gilt für alles, was zum Event gehört (seine Felder und deren Typen): Die Projektion liest sie, sie bestimmt sie nicht.

**Queries entstehen hier sehr wohl**, mit Frage und einer Antwort (eine Geste, wie „Query mit Frage und Antwort“ im Aggregat). Die
Lese-Fähigkeit wählt man am Port der Frage, wie beim `Auf`. Query und Antwort liegen auf der Leseseite (`Strukturort` nach Primitive). Die Falle aus dem Aggregat (der Ort einer Antwort wird beim
Anlegen fest und zieht nicht um) entfällt also. Eine zweite Query mit gleichem Ziel verhindert R4 schon heute.

**Wer bekommt das neue Auf bzw. die neue Frage?** Hat der Store genau eine Projektion bzw. einen Reader, diese. Hat er mehrere, fragt das
Anlegen nach (Auswahl im kleinen Menü, darunter „＋ neue Projektion“ bzw. „＋ neuer Reader“). Hat er keine, entsteht sie mit. Der Name
folgt dem Store (`ImagePairProjektion`, `ImagePairReader`). Zwei Projektionen in einem Store sind erlaubt. Zwei Ziele einer Projektion nicht
(CQRS052): Das Anlegen bietet nur Fähigkeiten dieses Stores an.

**Eine neue Fähigkeit braucht eine Signatur.** Sie entsteht entweder frei im Kopf der Spalte Fähigkeit oder gestrichelt am Port eines `Auf`
bzw. einer `Frage`. Vorschlag beim Anlegen (Name abfragen, wie beim Command), immer samt Umsetzung:

- Am Port des `Auf BildVerfuegbar`: Name vorgeschlagen `IVermerkeBildVerfuegbar`, Methode `VermerkeAsync`, Parameter die `Id<T>`-Felder des
  Events. Bei einem Store der Klasse 2 kommt `Herkunft` dazu (CQRS109), sonst ist sie optional.
- Am Port der `Frage HoleBildPaarStand`: `IFindeBildPaarStand`, Methode `FindeAsync`, Parameter die `Id<T>`-Felder der Query, Rückgabe
  `Task<…ReadModel?>`, sobald ein Read-Model gewählt ist, bis dahin `Task`.
- Frei im Spaltenkopf: nur Name und Methode, ohne Parameter.
- Der Rumpf von `Auf`, `Frage` und Umsetzung ist ein Platzhalter (W1: Unfertiges baut nicht). „🤖 füllen“ bleibt der Weg zum Rumpf.

**Read-Models hängen an der Signatur.** Eine Kante Fähigkeit → Read-Model gibt es nur, wenn die Signatur es nennt (Parametertyp oder Rückgabe).
Das ist ein Code-Fakt (W2); geraten wird nichts. Anschließen heißt die Signatur ändern, also im ✎ der Fähigkeit (Parameter bzw. Rückgabe). Einen
eigenen ＋-Port dafür gibt es nicht, solange die Geste nicht eindeutig ist (soll das Read-Model Parameter sein oder Rückgabe, ganz oder als Teil?).
Ein Read-Model, das keine Fähigkeit nennt, steht „unverbunden“ in seiner Spalte.

**Regeln, die das Anlegen von selbst einhält:** Fähigkeit nur als Parameter, nie im Konstruktor (CQRS054, CQRS055); genau eine Funktion je
Fähigkeit (CQRS051); Schreiben und Lesen getrennt (CQRS066/067: das Band bestimmt `IWriteStore` bzw. `IReadStore`); ein Read-Model mit
`Id<T> Id` wird `partial` angelegt (CQRS111); Zeit nur aus der `Herkunft` (R11), deshalb steht sie im Vorschlag des `Auf` als Parameter.
Klasse 3 nur mit `[MindestensEinmal]` (CQRS110): Bei einem Store der Klasse 3 legt das erste `Auf` das Attribut an der Projektion mit an und sagt
es in der Meldung.

**Ein Reader, der über mehrere Stores liest** (`HoleDatensatzPaare` nimmt eine Fähigkeit aus `IDatensatzStore` und eine aus `IImagePairStore`),
steht im Modal seines Stores (Besitz, wie heute). Im fremden Store erscheint die Frage nur leise am Rand mit „gehört zu IDatensatzStore“. So
sieht man dort, wer mitliest, ohne dass die Frage zweimal bearbeitet wird.

**Entfernen:** „✕ Auf“ entfernt die Handhabung; ihre Fähigkeiten bleiben und stehen danach „unverbunden“, wenn sie niemand sonst nimmt. ✕ am
Port löst nur die Kante. „✕ Fähigkeit“ geht nur, wenn sie niemand mehr nimmt; die Umsetzung geht mit.

## 6. Alles im Editor anlegen

Die Vorgabe aus dem Aggregat gilt hier ebenso: Jeder Baustein der Leseseite entsteht im Editor, ohne Handarbeit im Code. Die Liste geht alle
Bausteine durch. Sie nennt, wo man sie anlegt, was in derselben Geste mit entsteht und was dafür heute fehlt.

| Baustein | Wo im Editor | Entsteht mit | Heute |
|---|---|---|---|
| **Store** (Bündel `I…Store`) | Fläche, Kopf der Spalte Projektionen: „＋ Store“ (Name, Store-Klasse 1, 2 oder 3 wählen) | seine Umsetzungsklasse in der Speicher-Rolle mit der Basis der gewählten Klasse; danach öffnet das Modal | fehlt: Die Fläche legt nur eine Projektion an, und eine neue Umsetzung erbt nur dann eine Basis, wenn alle bestehenden dieselbe haben (`StoreBasis`) |
| **Projektion** | im Store beim ersten `Auf` bzw. „＋ neue Projektion“ im Auswahlmenü; „＋“ der Fläche fragt nach dem Store | – | Die Fläche legt sie ohne Store an |
| **Reader** | im Store bei der ersten Query bzw. „＋ neuer Reader“ | – | wie Projektion |
| **Auf** | Eingänge (Schreiben): vorhandene Events, die die Projektion noch nicht hört | – | – |
| **Event** | nicht hier: Es stammt aus dem Aggregat und ist im Store fix (nur lesen, „▣ im Aggregat öffnen“) | – | – |
| **Query, Frage, Antwort** | Eingänge (Lesen): „＋ Query mit Frage und Antwort“ | alle drei | – |
| **weitere Antwort** | ＋ Antwort am Port der Frage, frei im Kopf der Spalte Antworten | – | Rückgabe der Frage folgt (§7) |
| **Schreib- bzw. Lese-Fähigkeit** | Kopf der Spalte Fähigkeit, gestrichelt am Port von `Auf` bzw. `Frage` | ihre Umsetzung (Methode in der Store-Klasse) | vorhanden im Modell (`Faehigkeit.ImplRumpf`), fehlt im Modal |
| **Read-Model** | Kopf der Spalte Read-Models: Name und Art (s. u.) | Eintrag in den Dokument-Typen der Umsetzung (Klasse 1) | fehlt im Modal; die Dokument-Typen schreibt heute niemand nach |
| **Felder** von Read-Model, Antwort, Query | ✎ der Karte | – | wie im Aggregat |
| **Wertobjekt, Aufzählung** (`HistorieEintrag`, `SplitKonfig`, `Klassifikation`) | im Typ eines Felds bzw. Parameters: „＋ neues Wertobjekt“, „＋ neue Aufzählung“ | im Namespace des Stores | fehlt, im Aggregat ebenso |
| **Parameter einer Fähigkeit**, ihre Rückgabe | ✎ der Fähigkeit | – | – |
| `Herkunft`, `CancellationToken` an `Auf`/`Frage` | ✎ der Handhabung (Schalter) | bei Klasse 2 die `Herkunft` an jeder Schreib-Fähigkeit von selbst | – |
| `[MindestensEinmal]` | von selbst beim ersten `Auf` in einen Store der Klasse 3 | – | fehlt |
| **Rümpfe** von `Auf`, `Frage`, Umsetzung | ✎ (Rumpf), „🤖 füllen“ | Platzhalter beim Anlegen | – |
| **Verzeichnis** eines Datei-Stores | Formular der Umgebung (`Stores:<Store>:Verzeichnis`), wahlweise | – | Konfig, nicht Code (W7); ohne Angabe gilt das Temp-Verzeichnis |
| **Wer fragen darf** (`IDarf<Query>`) | auf der Fläche „＋ darf“ am Akteur; im Modal steht an der Query „von …“ bzw. „niemand darf fragen“ | – | Hinweis im Modal fehlt |

**Read-Model anlegen.** Die Art ist eine Wahl des Modellierers. Sie entscheidet, wie die Umsetzung schreiben darf (Schreib-Regel je Dokument,
docs/04 §4.3):

- **eigenes Dokument eines Aggregats:** `Id<T> Id`, das Aggregat wird gewählt, das Read-Model entsteht `partial` (CQRS111);
- **Zeile eines Streams:** eine Id, die den Besitzer enthält (z. B. `{DatensatzId}:{ImagePairId}`);
- **geteilt** (`IGeteiltesReadModel`): mehrere Streams schreiben hinein, mit Versionsprüfung.

Bei einem Store der Klasse 1 gehört das Read-Model in die Dokument-Typen der Umsetzung, das ist heute `base(store, typeof(ImagePairReadModel))`.
Beim Anlegen im Modal wird es dort eingetragen. Das ist eine Geste am Store, kein Raten: Das Read-Model entsteht in diesem Store.

**Der Store braucht seine Klasse beim Anlegen.** Die Basis der Umsetzung kommt aus dem Framework, nicht aus dem Bestand: Klasse 1
`MartenStore`, Klasse 2 `DateiZiel`, Klasse 3 `DateiSink` (Core). Heute nimmt der Editor die Basis nur, wenn alle bestehenden Umsetzungen
dieselbe haben. Bei `MartenStore` schriebe er den Konstruktor als `(IDocumentStore store, Type[] dokumente) : base(store, dokumente)` nach, und
diesen Parameter kann die DI nicht auflösen. Beides gehört zum Bau (§7).

## 7. Was dafür zu bauen ist (später, nicht jetzt)

- **Editor** (`editor.js`): Das Store-Modal bekommt die Mechanik des Aggregats. Sinnvoll ist, sie zuerst allgemein zu machen und das Aggregat
  darauf umzustellen, statt sie zu kopieren (W11, je Absicht ein Weg):
  - Detailstufen, ✎, Ports mit ＋ und Kandidaten, gestrichelte Karten, „unverbunden“, Zeitstempel, ⇅, Bildlauf.
  - Die Legefunktion bleibt die des Aggregats (Paare aus zwei Spalten, die rechte Seite folgt den Kanten); der Store legt sie je Band einmal.
  - Fläche: „＋ Store“ im Kopf der Spalte Projektionen; „＋ Projektion“ nur mit Store.
  - Typ-Auswahl in ✎ mit „＋ neues Wertobjekt“ und „＋ neue Aufzählung“ (für Aggregat und Store).
  - Event-Karten im Store ohne ✎ und ✕, mit „▣ im Aggregat öffnen“.
- **Sicht** (`GraphExtractor.Kern/Sicht`):
  - Kanten Fähigkeit → Read-Model aus der Signatur.
  - Die Umsetzung als eigenes Glied mit der Store-Klasse.
  - „Unverbunden“ für Fähigkeiten ohne Handhabung und Read-Models ohne Fähigkeit.
- **Schreiben** (`DateiAbgleich`, Scaffolder):
  - Die Rückgabe einer bestehenden `Frage` im Reader folgt ihren Antworten, wie beim Aggregat (`FrageRueckgaben`). Zu prüfen ist, ob der
    `Handle` mit `Ausgaenge` das heute schon umschreibt.
  - Eine neue Fähigkeit im Bündel-Interface samt Methode in der Store-Klasse (`Faehigkeit.ImplRumpf` gibt es schon).
  - `[MindestensEinmal]` an der Projektion.
  - Neuer Store mit gewählter Klasse: Basis aus dem Framework je Klasse statt `StoreBasis` aus dem Bestand, Konstruktor richtig (bei Klasse 1
    die Dokument-Typen).
  - Read-Model einem Store der Klasse 1 zuordnen: Dokument-Typ im Konstruktor der Umsetzung nachtragen.
- **Prüfen:** Sonde und `soll.txt` um die Gesten erweitern (neues Auf, Fähigkeit am Port anschließen und neu anlegen, zweite Fähigkeit an einem
  Auf, neue Query mit Frage, freies Read-Model, geteilte Fähigkeit);
  `--check` und `--sonde` grün; in einer Wegwerf-Kopie durchklicken.

## 8. Offene Entscheidungen

1. **Umsetzung als eigene Spalte oder im ✎ der Fähigkeit?** Empfehlung: eigene Spalte. Sie ist das Gegenstück zum Apply, zeigt die Store-Klasse
   und macht sichtbar, wo der Code wirklich schreibt. Dagegen spricht die Breite: Das Modal hat dann sechs Spalten.
2. **Wohin gehört die Rechnung einer Projektion?** (Frage an das Framework, unabhängig vom Editor, §2.1.) Heute meist in die Store-Klasse, je
   Event eine zugeschnittene Fähigkeit. Die Alternative sind allgemeine Fähigkeiten je Read-Model (anlegen, ändern mit einer Funktion des alten
   Stands, Zeile anhängen). Dann rechnet das `Auf` in der Domäne, rein und im Prüfstand prüfbar, wie das Apply. Der Store setzt je Read-Model
   wenige Funktionen einmal um. Zu prüfen wäre, ob CQRS053 und die Klasse-2-Pflicht (`Herkunft`, bedingtes Schreiben) dazu passen. Empfehlung:
   getrennt entscheiden; der Editor wird so gebaut, dass beide Stile gehen, und schreibt keinen vor.
3. **Ein Modal für beide Richtungen oder zwei Reiter (Schreiben, Lesen)?** Empfehlung: ein Modal mit zwei Bändern. Die Kette über das Read-Model
   (wer schreibt, was eine Frage liest) ist gerade das, was man sehen will.
