# Entwicklungsplan: Signalbasiertes CQRS- und Prozess-Framework

Dieser Plan beschreibt den vollständigen Weg, ein event-basiertes Framework von
Grund auf zu bauen, in dem Aggregate Ereignisse schreiben, Projektionen und
Reaktionen daraus abgeleitet werden und mehrschrittige Geschäftsvorgänge über
mehrere Aggregate hinweg orchestriert und im Fehlerfall fachlich ausgeglichen
werden. Das Dokument ist so geschrieben, dass es ohne weiteres Vorwissen
verständlich ist: Es erklärt zuerst, was gebaut wird, dann in welcher
Reihenfolge und warum genau so.

---

## 1. Was gebaut wird

Das System ruht auf einem Prinzip: **Die Wahrheit ist der Log.** Jedes Aggregat
schreibt seine rohen Ereignisse geordnet und lückenlos in einen Event-Store. Alles
andere — Projektionen, Reaktionen, Prozesse — ist daraus abgeleitet und jederzeit
neu berechenbar.

Nach dem Schreiben eines Ereignisses passieren zwei unabhängige Dinge. Erstens
geht das rohe Ereignis wie gewohnt an die Client-Systeme. Zweitens wird ein
**typisiertes Signal** veröffentlicht, das nur `(StreamId, Version)` trägt — ein
Weckruf, keine Nutzlast. Dieses Signal darf verloren gehen, doppelt oder
ungeordnet ankommen; nichts an der Korrektheit hängt an seiner Zustellung.

Ein zustandsloser **Receiver** (einer pro Node) fängt Signale und weckt einen
**Adapter** — genau eine Instanz pro Stream im gesamten Cluster. Der Adapter liest
die **echten** Ereignisse aus dem Log, ab seiner Fortschrittsmarke bis zum
aktuellen Ende, und leitet jedes an die passende Handler-Methode. Was der Handler
zurückgibt, entscheidet den Effekt: ein Repo-Write faltet eine Projektion, ein
Command löst eine Reaktion auf einem anderen Aggregat aus, ein Plan startet einen
Prozess.

Reihenfolge und „genau einmal wirksam" entstehen **nicht** auf dem Signalweg,
sondern aus dem geordneten Log-Read ab der Fortschrittsmarke. Ein periodischer
Poll fängt verlorene Signale auf.

```
Aggregat ──append──────────────────────────▶ Event-Store (Wahrheit)
   │                                              ▲
   │ Signal (StreamId, Version)                   │ Log-Read ab Marke
   ▼                                              │
Signal-Route ──▶ Receiver (je Node) ──▶ Adapter (ein Writer je Stream)
                                            │
                                            ▼
                                Handler-Methode (echtes Event)
                                Rückgabetyp = Effekt
                                ├─ Repo-Write  → Projektion
                                ├─ Command     → Reaktion
                                └─ Plan        → Prozess
```

Ein **Prozess** (etwa: „belaste das Konto, verbuche die Teile, markiere den
Auftrag — und wenn ein Schritt scheitert, gleiche die vorigen aus") ist der
Ersatz für die Transaktion, die es über Aggregat-Grenzen nicht geben kann. Er
besteht aus zwei generierten Teilen: einem **Prozess-Aggregat** als durablem
Gedächtnis (welche Schritte sind quittiert, welcher ist dran) und einem
**Treiber**, der die Schritte als Commands an die Ziel-Aggregate sendet, jede
Antwort quittiert und im Fehlerfall die gelungenen Schritte über ihre
Gegen-Commands ausgleicht. Kompensation radiert nichts — sie ist ein neuer
Geschäftsvorfall; die Geschichte wird länger, nie kürzer.

Ein tragendes Merkmal: **Alles, was zur Laufzeit dispatcht, wird zur Compile-Zeit
generiert.** Es gibt keine Reflection im Laufzeitpfad — keine dynamische
Instanziierung, kein `Invoke`, kein Assembly-Scan. Der Entwickler schreibt nur
Handler-Methoden, Decider, Repos und Plan-Records; Signal, Log-Read, Dispatch,
Fortschritt, Sharding, Poll, Prozess-Gedächtnis und Treiber sind generiert.

---

## 2. Die Prinzipien, die jede Entscheidung tragen

Diese Sätze begründen die gesamte Reihenfolge weiter unten.

**Die Wahrheit ist der Log.** Ordnung, Vollständigkeit und Wiederholbarkeit kommen
ausschließlich aus dem Event-Store-Read. Kein anderer Mechanismus trägt Wahrheit.

**Das Signal ist nur ein Weckruf.** Es trägt nur `(StreamId, Version)` und darf
verloren, doppelt und ungeordnet sein. Genau diese Anspruchslosigkeit erlaubt ein
einfaches, verlustbehaftetes PubSub als Transport.

**Routing läuft über Typen, nie über handgebaute Strings.** Welches Signal weckt
wen, welcher Output geht wohin, welcher Plan startet welchen Prozess — jede
Zuordnung ist eine Typ-Zuordnung, zur Compile-Zeit aufgelöst.

**Der Fachcode bleibt rein.** Cursor, Version, Signal, Ordnung, Exactly-once,
Sharding und Prozess-Maschinerie tauchen im Code des Entwicklers nie auf.

**Genau einmal wirksam, nicht genau einmal verarbeitet.** Im verteilten System
kann ein Ereignis mehrfach durch einen Handler laufen. Korrekt ist das System,
wenn jedes Ereignis genau einmal **wirksam** wird — sichergestellt dadurch, dass
Effekt und Fortschrittsmarke gemeinsam gültig werden (atomar im Store) oder der
Effekt idempotent ist.

---

## 3. Der Weg in sechs Phasen

Der Plan gibt **einen** Weg vor. Die Reihenfolge folgt zwei Regeln: Was andere
Teile tragen muss, kommt zuerst; und die gefährlichste Unbekannte — die
verteilte Exactly-once-Semantik — wird so früh wie möglich bewiesen, solange
Änderungen noch billig sind.

### Phase 0 — Fundament: die tragenden Verträge

Zuerst stehen die Abstraktionen, auf denen alles Weitere sitzt. Diese Phase ist
die einzige, in der sorgfältiges Nachdenken mehr zählt als Code, denn jeder hier
falsch geschnittene Vertrag zieht später einen Rattenschwanz durch alles Gebaute.

Gebaut wird: das Event-Envelope mit **Version pro Ereignis** und den
Metadatenfeldern (Korrelation, Verursacher, Zeitstempel), die später auch nach dem
Log-Read verfügbar sein müssen. Das Event-Store-Interface mit zwei Leseprimitiven
— „lies einen Stream ab Position N" (für Adapter und Treiber) und „lies die Streams
jenseits der globalen Fortschrittsgrenze" (für den Poller). Die Typ-Welt der
Handler-Ausgaben, in der ein Ausgabetyp abschließend deklariert, welche Effekte
möglich sind. Die Marker-Typen für Plan, für persistente Ablehnung und für den
optionalen Fortschritts-Tracker der Stores. Und der Zuschnitt der
Projekt-Compilationen, sodass generierte Prozess-Ereignisse später von den
Signal- und Registry-Generatoren gesehen werden.

**Das Tor:** Die Verträge kompilieren und sind so geschnitten, dass keine spätere
Phase sie aufbrechen muss. Es läuft noch nichts — und das ist richtig so.

*Anschlag: 2 Wochen.*

### Phase 1 — Die Schreibseite: die Wahrheit entsteht

Nun entsteht die einzige Wahrheitsquelle. Das Aggregat läuft als virtueller
Cluster-Actor, adressiert über `(AggregatTyp, AggregatId)`. Ein Command wird
verarbeitet: Der Decider entscheidet rein und ohne Nebenwirkung, die entstehenden
Ereignisse werden mit monotoner, lückenloser Version pro Ereignis durabel
geschrieben (echte optimistische Nebenläufigkeitsprüfung), der Applier faltet den
neuen Zustand. Direkt nach dem Commit wird das typisierte Signal veröffentlicht,
parallel zum unveränderten Ereignis an die Clients.

Zwei Generatoren entstehen hier: der Aggregat-Generator, der aus State, Decider
und Applier den Actor erzeugt, und der Signal-Typ-Generator, der pro Ereignistyp
den passenden Signaltyp samt Eintrag in Typ-Registry und Serialisierungs-Mapping
erzeugt.

**Das Tor:** Ein Command auf ein Test-Aggregat erzeugt einen geordneten,
lückenlosen Stream mit korrekter Version pro Ereignis, und das Signal erscheint
auf dem PubSub. Die Version pro Ereignis ist der Anker für alles Folgende — sie
muss hier stimmen.

*Anschlag: 3 Wochen.*

### Phase 2 — Das Rückgrat: Signal, Log-Read, Wirksamkeit

Dies ist die wichtigste Phase, deshalb kommt sie früh. Der Receiver fängt das
Signal und weckt den Adapter. Der Adapter liest seine Fortschrittsmarke, liest den
Stream ab dieser Position, verwirft per Guard alles bereits Angewandte, leitet
jedes neue Ereignis an die Handler-Methode, führt den Repo-Effekt aus und rückt
die Marke vor. Der Store führt dabei **eine Session pro Verarbeitungslauf** —
Effekt und Marke in einem einzigen Commit —, sodass beide gemeinsam gültig werden.
Das ist der Nahtpunkt, an dem aus „wirksam" ein „genau einmal wirksam" wird.

Bewusst läuft diese Phase **auf einem einzigen Node, ohne Cluster-Sharding**. Die
sequenzielle Semantik — Ordnung, Guard, Marke, gemeinsames Commit — muss sauber
sitzen, bevor Nebenläufigkeit zwischen Nodes hinzukommt. Verteilte Fehler isoliert
man leichter, wenn die Logik einzeln bereits bewiesen ist.

**Das Tor — die entscheidenden Crash-Proben mit einer einzigen Projektion:** Ein
verlorenes Signal heilt der nächste Read. Ein doppeltes Signal ist folgenlos.
Effekt und Marke werden gemeinsam gültig. Ein Absturz zwischen Effekt und Marke
führt zu Wiederholung, nie zu Doppelwirkung. Ist dieses Tor durchschritten, ist
das größte Risiko des gesamten Vorhabens erledigt, und alle weiteren Schätzungen
werden belastbar.

*Anschlag: 5 Wochen. Der Meilenstein, an dem das Projekt steht oder fällt.*

### Phase 3 — Output-Routing: Projektion, Reaktion, Command

Bisher schreibt ein Handler nur ins Repo. Jetzt bekommt der Adapter seinen
generierten Ausgabe-Switch: Was der Handler zurückgibt, wird nach Typ geroutet.
Ein Repo-Write faltet eine Projektion (bereits vorhanden). Ein Command geht als
Reaktion an ein Ziel-Aggregat, adressiert über die generierte Zuordnung
„Command-Typ → Aggregat-Typ". Der Empfänger erkennt Wiederholungen fachlich (ein
Decider, der eine bereits bekannte Korrelations-Id als „schon geschehen"
behandelt und keine Ereignisse produziert) und macht so einen doppelten Command
wirkungslos.

**Das Tor:** Ein Ereignis löst über eine Reaktion einen Command auf einem
**anderen** Aggregat aus; ein doppelt gesendeter Command verpufft beim Empfänger.
Damit ist das Muster „mindestens einmal gesendet, genau einmal wirksam durch den
Empfänger" bewiesen — dasselbe Muster, das der Prozess später im Großen braucht.

*Anschlag: 3 Wochen.*

### Phase 4 — Robustheit: Sharding und Poll-Backstop

Jetzt wird das Rückgrat verteilungsfest. Der Adapter wird zum virtuellen
Cluster-Actor pro Stream: Single-Writer je Stream, aber volle Parallelität
zwischen verschiedenen Streams. Der Receiver läuft lokal je Node und überbrückt
die PubSub-Zustellung (die an registrierte Empfänger geht) zur virtuellen
Cluster-Identität des Adapters (die erst durch den ersten Aufruf entsteht). Der
Poller pro Handler-Klasse fängt den einen Fall ab, den das Coalescing nicht heilen
kann: das letzte verlorene Signal vor Stille. Er nutzt das globale Leseprimitiv
aus Phase 0. Ein schlanker Dienst pro Node hält die Poller wach, damit sie nach
einem Node-Ausfall wieder anlaufen.

**Das Tor:** Zwei Nodes, die dasselbe Signal empfangen, wecken dieselbe eine
Adapter-Instanz. Die Reihenfolge pro Stream bleibt erhalten, verschiedene Streams
laufen parallel, und ein komplett verlorenes Signal wird spätestens vom Poll
geheilt.

*Anschlag: 4 Wochen.*

### Phase 5 — Die Prozess-Maschinerie

> ⚠ **Mechanismus überholt.** Das Phase-5-*Ziel* (Prozesse, exactly-once-Schritte, Kompensation) gilt
> weiter; der hier skizzierte *Weg* — ge-yieldeter **Plan** → generiertes Aggregat + **Treiber** — wurde
> verworfen und durch einen Event-Regel-DAG ersetzt (`docs/prozess-neubau-event-regeln-dag.md`,
> `docs/anleitung-prozess-schreiben.md`). Die Tore der Phase bleiben unverändert gültig.

Nun das eigentliche Ziel. Der Plan-Ausgabetyp wird im Adapter-Routing behandelt:
Ein ge-yieldeter Plan schlägt in der generierten Tabelle „Plan-Typ →
Prozess-Kind" nach, leitet eine **deterministische Prozess-Id** aus dem
auslösenden Ereignis ab und startet den Prozess. Der große neue Baustein ist der
Prozess-Generator. Er erzeugt pro Plan-Typ ein vollständiges Aggregat (Zustand,
Decider mit Wiederholungs-Noop-Regeln, Applier, Ereignisse für Start, Schritt
erledigt, Schritt gescheitert, Rückabwicklung, Abschluss, Fehlschlag und
Klärungsbedarf) und den **Treiber**.

Der Treiber ist der Adapter aus den Phasen 2 und 4 auf dem Prozess-Stream, nur ist
sein Effekt ein Command mit Quittung: Er faltet den Prozess-Zustand („dran ist
Schritt n"), sendet den Schritt-Command an das Ziel-Aggregat und wartet auf dessen
Antwort. Kommt eine als Ablehnung markierte Tatsache zurück, quittiert er den
Fehlschlag und gleicht die bereits gelungenen Schritte in Gegenreihenfolge über
ihre Gegen-Commands aus. Erschöpft sich das Retry-Budget einer Rückabwicklung,
eskaliert er in den Klärungszustand mit Alarm — nie Endlosschleife, nie stilles
Überspringen. Weil der Treiber Single-Writer seines Prozesses ist, darf er nach
einer bestätigten Quittung den Folgeschritt sofort in derselben Wachphase
anstoßen; der Signal-und-Log-Weg bleibt das Sicherheitsnetz für den Absturzfall.

Determinismus trägt die Korrektheit: Die Prozess-Id ist deterministisch aus Typ
und auslösendem Ereignis, sodass ein doppelter Start beim Prozess-Aggregat als
Noop verpufft. Die Command-Id jedes Schritts ist deterministisch aus Prozess-Id
und Schrittnummer, sodass ein doppelt gesendeter Schritt beim Ziel-Aggregat als
Duplikat erkennbar ist.

Der Pilot ist ein realer Vorgang über drei Aggregate mit Kompensation.

**Das Tor:** Der Happy Path läuft durch (alle Schritte, Abschluss). Der Fehlerfall
gleicht die gelungenen Schritte aus, und die Ziel-Aggregate zeigen danach beide
Buchungen — Hin- und Gegenbewegung, denn nichts wurde radiert. Ein Absturz
zwischen beliebigen Nachrichten führt zu Fortsetzung, nie zu Doppelwirkung. Ein
doppelter Start verpufft.

*Anschlag: 6 Wochen.*

### Phase 6 — Härtung und Ausbau

Das Framework ist funktional; jetzt wird es produktionsreif. Verkettung: Ein
Abschluss-Ereignis eines Prozesses startet über einen gewöhnlichen Handler den
nächsten Plan — Prozesse verketten sich über Ereignisse, statt intern zu
verzweigen. Eine Monitoring-Projektion über die Prozess-Streams zeigt alle
hängenden Vorgänge; sie ist eine gewöhnliche Projektion, kein Sonderfall.
Poison-Handling: Ein deterministisch scheiterndes Ereignis wird mit Backoff
wiederholt, nach n Versuchen wird der Stream geparkt und Alarm ausgelöst, nie
automatisch übersprungen. Für sehr lange Streams werden Snapshots eingeführt,
damit Rehydration nicht linear wächst. Unter Last werden die Nebenläufigkeits-
konflikte der Prozess-Commands über den bestehenden Antwortkanal in einer
Retry-Schleife aufgefangen. Und als bewusste Ausbaustufe kommen Deadlines hinzu —
durabler Zeitzustand im Prozess-Stream für „wenn nach einer Frist kein Ereignis
eintrifft, dann ...", der einen mailbox-lokalen Timer überlebt.

**Das Tor:** Verkettung, Monitoring, Poison-Parken und Nebenläufigkeit unter Last
sind belegt.

*Anschlag: 4 Wochen, teils fortlaufend.*

---

## 4. Meilensteine im Überblick

Jedes Tor ist eine Frage, die mit „ja, durch Test bewiesen" beantwortet sein muss,
bevor die nächste Phase beginnt.

| Nach Phase | Das Tor |
|---|---|
| 0 | Verträge kompilieren, Schnitt trägt alle folgenden Phasen |
| 1 | Command → geordneter Stream, Version pro Ereignis, Signal erscheint |
| 2 | Eine Projektion überlebt Signalverlust, Doppel-Weckung und Absturz — genau einmal wirksam |
| 3 | Reaktion feuert Command auf Fremd-Aggregat; Duplikat verpufft beim Empfänger |
| 4 | Zwei Nodes → eine Adapter-Instanz; Ordnung erhalten; Poll heilt Totalverlust |
| 5 | Pilot-Prozess: Happy Path, Kompensation und Crash-Probe grün |
| 6 | Verkettung, Monitoring, Poison-Parken, Nebenläufigkeit unter Last belegt |

Das entscheidende Tor ist das nach Phase 2. Davor ist die Kernannahme des Systems
unbewiesen; danach ist sie belastbar.

---

## 5. Zeit und Besetzung

Die Anschläge summieren sich auf **rund 27 Wochen** — etwa **sechs bis sieben
Monate** für eine starke Person bis zu einem soliden, getesteten Zustand samt
Pilot-Prozess. Ein lauffähiger Pilot (durch Phase 5, Happy Path) ist nach etwa
**zwölf bis vierzehn Wochen** erreichbar, weil die Phasen 0 bis 4 ihn tragen.

Bei zwei Personen besitzt eine das Rückgrat (Phasen 0, 2, 4) und eine den
Fach- und Generator-Layer (Phasen 1, 3, 5), mit einem gemeinsamen harten
Review-Tor nach Phase 2. Der Durchsatz steigt dabei eher um die Hälfte als aufs
Doppelte, weil die Generatoren voneinander abhängen — aber die Review-Qualität an
den zwei heikelsten Stellen (das Rückgrat in Phase 2 und der Treiber in Phase 5)
steigt deutlich, und genau dort zahlt sie sich aus.

---

## 6. Die zwei stillen Kostenfallen

Zwei Stellen sind teurer, als sie wirken, und verdienen von Anfang an Aufmerksamkeit.

Der **Session-Zuschnitt in Phase 2** ist kein Interface-Detail, sondern prägt, wie
jeder Store und der Schreibpfad gebaut sind: Effekt und Fortschrittsmarke müssen in
einem gemeinsamen Commit landen, nicht in getrennten Schreibvorgängen. Wird das
von Beginn an so entworfen, ist es ein Vorteil; bemerkt man es erst später, ist es
ein Umbau durch alle Stores.

Die **verteilte Testbarkeit in den Phasen 4 und 5** ist der Faktor, der die
Kalenderzeit am unzuverlässigsten macht. Doppelte Weckungen, ein Absturz zwischen
zwei Schreibvorgängen, Nebenläufigkeitskonflikte unter Last — das sind die Fehler,
die im einfachen Test nie auftauchen. Die früh gezogenen Crash-Proben aus Phase 2
sind die beste Versicherung dagegen: Sie machen die teuren Überraschungen billig,
weil sie auftreten, solange erst eine einzige Komponente existiert.
