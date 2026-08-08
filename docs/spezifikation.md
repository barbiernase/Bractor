# Spezifikation: Signalbasierte Zustellung — Projektionen, Reaktionen und Prozesse

**Zweck dieses Dokuments.** Es beschreibt genau einen Weg, wie Events vom Aggregat zu
allen gelangen, die sie kennen oder auf sie handeln müssen — geordnet, genau einmal
wirksam, ohne Reflection und ohne das Prinzip „nur über Typen routen" zu verletzen.
Das reicht vom Mitschreiben (Projektionen) über das Reagieren (Event → Command) bis zu
Mehrschritt-Vorgängen über mehrere Aggregate mit fachlicher Rückabwicklung (Prozesse).
Es ist so geschrieben, dass ein Entwickler es umsetzen und den Aufwand bewerten kann.
Es gibt keine Varianten: wo eine Entscheidung getroffen wurde, ist sie getroffen, und
die Begründung steht dabei.

---

## 1. Kurzfassung (der eine Weg)

Das Aggregat schreibt sein rohes Event in den Event-Store — das ist und bleibt die
einzige Wahrheit. Nach dem Commit passieren zwei voneinander unabhängige Dinge: Das
Event geht wie gehabt über das bestehende PubSub an die Client-Systeme, und zusätzlich
wird ein **generierter, typisierter Signal-Typ** `StateChangeVia{Event}` über dasselbe
PubSub veröffentlicht. Dieses Signal trägt nur `(StreamId, Version)` — es ist ein
Weckruf, keine Nutzlast.

Pro Handler-Klasse existiert ein **generierter Adapter**. Ein zustandsloser
**Receiver** (einer pro Node) nimmt die Signale entgegen und leitet sie an die
Cluster-Identität des Adapters weiter — genau eine Instanz pro Stream im Cluster.
Wird der Adapter geweckt, **liest er die echten Events aus dem Event-Store** (ab
seiner Fortschrittsmarke bis zum Head), dispatcht jedes echte Event an die passende
`Handle`-Methode und **routet nach Typ, was der Handler zurückgibt**: Repo-Writes
falten Projektionen, Commands gehen an Ziel-Aggregate, und ein **Plan** startet einen
Prozess — ein generiertes Prozess-Aggregat als durables Gedächtnis plus einen
generierten Treiber, der die Schritte anstößt, quittiert und im Fehlerfall die
gelungenen Schritte fachlich ausgleicht.

Reihenfolge und Exactly-once entstehen **nicht** auf dem Signalweg, sondern aus dem
geordneten Log-Read. Deshalb darf das Signal verloren gehen, doppelt ankommen oder
ungeordnet sein — ein periodischer Poll fängt verlorene Signale auf.

```
Aggregat ──append──────────────────────────▶ Event-Store (Wahrheit)
   │                                              ▲
   │ Signal (StreamId, Version)                   │ Log-Read ab Marke
   ▼                                              │
Signal-Route (Typ) ──▶ Receiver (je Node) ──▶ Adapter (ein Writer je Stream)
                                                  │
                                                  ▼
                                     Handler: Handle(TEvent, …)
                                     Rückgabetyp = Effekt
                                     ├─ Repo-Write  → Projektion
                                     ├─ Command     → Reaktion
                                     └─ Plan        → Prozess (Aggregat + Treiber)
```

---

## 2. Grundprinzipien (die Invarianten)

Diese sechs Sätze gelten überall, ohne Ausnahme. Alles Weitere ist Ableitung.

1. **Die Wahrheit ist der Log.** Reihenfolge, Vollständigkeit und Wiederholbarkeit
   kommen ausschließlich aus dem Event-Store-Read. Kein anderer Mechanismus trägt
   Wahrheit.
2. **Das Signal ist ein Weckruf.** Es trägt nur `(StreamId, Version)`. Es darf verloren
   gehen, doppelt und ungeordnet sein. Nichts an der Korrektheit hängt an seiner
   Zustellung.
3. **Routing läuft über Typen.** Jede Zuordnung — welches Signal weckt wen, welcher
   Output geht wohin, welcher Plan startet welchen Prozess — ist eine Typ-Zuordnung.
   Es wird nie ein Identitäts-String von Hand konstruiert.
4. **Keine Runtime-Reflection.** Alle Typ-Zuordnungen, Dispatch-Tabellen und Registries
   werden zur Compile-Zeit generiert. Zur Laufzeit gibt es nur Typ-Token-Lookups und
   generierten Dispatch — kein `Activator.CreateInstance`, kein `MethodInfo.Invoke`,
   kein Assembly-Scan.
5. **Der Fachcode bleibt rein.** Ein Entwickler schreibt `Handle(TEvent, …)`-Methoden,
   Decider, Repos und Plan-Records. Cursor, Signal, Ordnung, Exactly-once, Sharding
   und Prozess-Maschinerie tauchen in seinem Code nie auf.
6. **Persistent genau dann, wenn ein durabler Konsument abhängt.** Eine Nachricht,
   deren Verlust einen Vorgang bricht, muss ein persistentes Event sein und über den
   Signal-Weg laufen. Eine Nachricht, deren Verlust folgenlos ist (Tick, UI-Feedback,
   Datei-Trigger), darf auf dem schnellen, verlustbehafteten Kanal bleiben.

---

## 3. Die zwei Kanäle

Das System hat genau zwei Zustellwege.

**Kanal 1 — durabel (Signal → Log-Read):** für alles, was im Event-Store steht.
Ordnung, Vollständigkeit, Wiederholung — die Maschinerie dieses Dokuments
(Kapitel 4–7).

**Kanal 2 — schnell (direkt, verlustbehaftet):** direkte Zustellung ohne Log, in drei
Sorten: `IPipelineTrigger` (z. B. Filewatcher → Pipeline über das generierte
`TriggerToPipelineId`-Mapping), `ITransientEvent` (z. B. `CommandFailed` als Targeted
Delivery an die UI) und `IPipelineSelfMessage` (Ticks, `ScheduleSelf`).

Der Kanal-Vertrag ist Invariante 6: **Was auf Kanal 2 läuft, muss verlierbar sein.**
Ein Tick kommt wieder; ein Datei-Trigger wird beim nächsten Scan erneut gefunden; ein
verpasstes UI-Feedback heilt der nächste Query. Bricht der Verlust einer Nachricht
einen Vorgang, gehört sie auf Kanal 1.

**Konsequenz — persistente Ablehnungen:** Fachliche Nein-Antworten, auf die ein
*Prozess* reagieren muss (Beispiel: `ReservierungAbgelehnt` wegen fehlender Deckung),
sind persistente Events — ein transientes Nein erzeugt kein Signal und ist für jeden
durablen Konsumenten unsichtbar. Nein-Antworten an *flüchtige* Konsumenten
(`CommandFailed` an die UI) bleiben transient. Die Regel ist nicht „alle Ablehnungen
persistent", sondern exakt Invariante 6.

Beide Kanäle dürfen in dieselbe Actor-Mailbox münden; die Verarbeitung bleibt dadurch
pro Instanz serialisiert. Eine **Ordnung zwischen den Kanälen** gibt es prinzipiell
nicht — eine Komponente, die „Tick kam nach Event X" voraussetzt, ist falsch
entworfen.

```
Kanal 1 (durabel):   Event ─▶ Log ─▶ Signal ─▶ Receiver ─▶ Adapter ─▶ Handle
Kanal 2 (schnell):   Trigger / Transient / Tick ───────────▶ Actor-Mailbox
Vertrag:             Kanal 2 = verlierbar. Sonst: Kanal 1.
```

---

## 4. Der Weg end-to-end

Jeder Schritt nennt, **wer** zuständig ist (Domäne = vom Entwickler geschrieben,
Framework = Mechanik, generiert oder Bibliothek) und **warum** er so ist.

### 4.1 Command → Event

Das Aggregat verarbeitet einen Command und produziert rohe Events.

- **Wer:** Domäne (Decider) für die Entscheidung; Framework für OCC und Rehydration.
- **Warum so:** Die Schreibseite garantiert geordnete, lückenlose Streams pro
  Aggregat, weil ein serialisierter Aggregat-Actor pro Stream schreibt. Diese
  Garantie ist die Grundlage für alles Folgende.

### 4.2 Append (die Wahrheit entsteht)

Die Events werden mit **monotoner, lückenloser Version pro Event** durabel in den
Stream geschrieben. Auch beim anschließenden Publizieren trägt jedes Event **seine
eigene** Version — nie den Endwert des Batches.

- **Wer:** Framework (Event-Store, Publish-Pfad).
- **Warum pro Event:** Ein Command kann mehrere Events erzeugen. Die Version pro Event
  ist der Anker für Reihenfolge, Dedup und deterministische Prozess-Identitäten
  (11.3); als Batch-Endwert wäre sie kein monotoner Anker mehr.

### 4.3 Fan-out des Signals

Nach dem Commit wird pro Event ein `StateChangeVia{Event}`-Signal mit
`(StreamId, Version)` über das bestehende PubSub veröffentlicht. Parallel und
unabhängig geht das rohe Event wie bisher an die Client-Systeme.

- **Wer:** Framework (Emit-Wiring, generiert).
- **Warum ein eigener Typ pro Event:** Das PubSub shardet und fächert **pro
  Message-Typ** an registrierte Subscriber. Damit nur die Handler geweckt werden, die
  den jeweiligen Event-Typ behandeln, muss das Signal nach diesem Event-Typ typisiert
  sein. Ein einziges generisches Signal würde alle wecken und die Selektivität
  zerstören.
- **Warum nur `(StreamId, Version)`:** Die Werte des Events kommen beim Read aus dem
  Log. Trüge das Signal das volle Event, müsste das PubSub geordnete, garantierte
  Zustellung leisten — was es nicht kann. Genau die Anspruchslosigkeit des Signals
  erlaubt das bestehende, verlustbehaftete PubSub.

### 4.4 Receiver wird beliefert, Adapter wird geweckt

Der zustandslose **Receiver** ist als PubSub-Subscriber für die
`StateChangeVia{Event}`-Typen seiner Handler-Klasse registriert — auf **jedem Node**
einer, mit node-eindeutiger SubscriberId. Er liest aus dem Signal nur
`(StreamId, Version)` und leitet per Cluster-Request als `Wake(StreamId)` an die
typisierte Identität des Adapters weiter: `(AdapterKind aus dem Handler-Typ,
StreamId)`.

- **Wer:** Framework (Receiver + Adapter, beide generiert).
- **Warum der Zwischenschritt:** PubSub stellt an registrierte PIDs zu; eine virtuelle
  Cluster-Identität existiert aber erst nach ihrer ersten Aktivierung und kann sich
  nicht selbst registrieren. Der Receiver ist die Brücke: PubSub liefert an eine
  registrierte PID, der Cluster findet daraus die eine Adapter-Instanz. Der Receiver
  darf mehrfach existieren, doppelt weiterleiten und Signale verlieren — alles
  folgenlos, denn die Wahrheit liegt beim Log-Read des Adapters.
- **Warum das bestehende PubSub genügt:** Es kann bereits typbasiertes Fan-out an
  selbst-registrierte Subscriber. Für den Transport wird nichts Neues gebaut.

### 4.5 Log-Read (das echte Event wird materialisiert)

Der Adapter liest den Stream `StreamId` ab seiner Fortschrittsmarke bis zum Head aus
dem Event-Store und erhält die **echten, typisierten Event-Instanzen** mit allen
Originalwerten.

- **Wer:** Framework (Adapter + Event-Store).
- **Warum lesen statt konstruieren:** Der Adapter erzeugt das Event nicht aus dem
  Signal (das kennt die Feldwerte nicht) — er **materialisiert** die Instanz, die das
  Aggregat geschrieben hat. Das ist die Stelle, an der Reihenfolge und Vollständigkeit
  real werden.
- **Warum „ab Marke bis Head" statt „ein Signal = ein Event":** Zwischen zwei
  Weckungen können mehrere Events aufgelaufen sein, auch anderer Typen, die derselbe
  Handler behandelt. Der Adapter verarbeitet **alle** neuen Events der Reihe nach.
  Das Signal sagt nur „schau nach"; was verarbeitet wird, bestimmt die Marke.
  Nebeneffekt: Das nächste Signal eines Streams heilt automatisch alle vorher
  verlorenen (Coalescing).

### 4.6 Dispatch an den Handler, Routing der Outputs

Für jedes gelesene Event ruft der Adapter über eine **generierte Dispatch-Tabelle**
die passende `Handle(TEvent, …)`-Methode auf. Übergeben wird die **echte
Event-Klasse**, nie der Signal-Typ. Was der Handler zurückgibt, routet der Adapter
**nach Typ** (Kapitel 8): Repo-Writes laufen sofort, Commands gehen an den
Dispatcher, Pläne starten Prozesse (Kapitel 11), Trigger gehen an ihre Pipeline.

- **Wer:** Framework (generierter Dispatch) ruft Domäne (Handler) auf.
- **Warum generiert:** Der Generator kennt aus den `Handle`-Signaturen, welcher Typ zu
  welcher Methode gehört, und aus den `OneOf`-Typargumenten, welche Outputs möglich
  sind. Das ist Overload-Auflösung zur Compile-Zeit — kein Runtime-`Invoke`, kein
  Switch von Hand, keine Reflection.
- **Wichtig — „Signal-Typ = wer wacht auf, Dispatch = was läuft":** Wird der Adapter
  von `StateChangeViaA` geweckt, dispatcht er trotzdem **alle** neuen Events (A, B, …)
  über seine volle Tabelle. Würde er nur Typ A verarbeiten, verlöre er B im selben
  Stream.

### 4.7 Effekt + Fortschritt

Der Handler faltet das Event über das Repo in einen Zustand (bzw. der Adapter führt
den gerouteten Effekt aus). Anschließend rückt die Fortschrittsmarke vor.

- **Wer:** Domäne (Handler + Repo) für die Faltung; Framework (Adapter) stößt das
  Vorrücken an, der Store führt die Marke (sofern er `IProjectionTracker`
  implementiert, siehe 5.5 / Kapitel 7).
- **Warum das Repo Domäne ist:** Es kapselt den konkreten Speicherzugriff. Der Handler
  drückt nur fachliche Absicht aus. Speicherform und Fachlogik sind verschiedene
  Achsen.
- **Warum die Fortschrittsmarke beim Store liegt:** Damit Effekt und Fortschritt
  *gemeinsam* gültig werden **können** — nur der Store kann beide in eine native
  Transaktion legen. Ob er das tut, ist seine Entscheidung (Kapitel 7); tut er es
  nicht, gilt At-least-once und die Handler müssen idempotent sein. Das Framework
  erzwingt hier nichts und besitzt selbst keine Transaktion.
- **Fehlerpfad = Wiederholung, kein Rollback:** Scheitert der Dispatch von Event k,
  bricht der Batch sofort ab (Ordnung!), die Marke rückt höchstens bis k-1 vor, und
  die nächste Weckung (Signal oder Poll) wiederholt ab k. Der Fehlerpfad ist derselbe
  Codepfad wie der Normalfall. Bei deterministisch scheiternden Events (Poison) gilt:
  Retry mit Backoff, nach n Versuchen Stream **parken** und alarmieren — nie
  automatisch überspringen; Skip ist eine Operator-Entscheidung (Marke manuell
  vorrücken).

### 4.8 Poll-Backstop

Zusätzlich zum Signal wird periodisch geprüft, ob Streams neue Events jenseits der
Fortschrittsmarke haben, und bei Bedarf ab Marke nachgelesen.

- **Wer:** Framework (Poller pro Handler-Klasse, generiert; Kapitel 14).
- **Warum nötig:** Das PubSub ist at-most-once; ein Signal kann verloren gehen. Das
  Coalescing aus 4.5 heilt verlorene Signale, sobald ein *späteres* Signal desselben
  Streams ankommt. Genau ein Fall bleibt: das **letzte Signal vor Stille** — danach
  kommt nie wieder eine Weckung, und ausgerechnet Abschluss-Events sind oft die
  wichtigsten. Der Poll wandelt „für immer verloren" in „höchstens ein Poll-Intervall
  Latenz". Signal = Geschwindigkeit, Poll = Sicherheit.
- **Stream-Quelle:** Woraus der Poller seine Stream-Liste bezieht, ist eine vor der
  Umsetzung zu treffende Entscheidung (19.1) — ohne sie bleibt der Restfall „erstes
  Signal eines neuen Streams verloren".

---

## 5. Bausteine

### 5.1 Signal-Typ `StateChangeVia{Event}`

Pro persistiertem Event-Typ wird ein Signal-Typ generiert.

- Implementiert `IMessagePayload` (Pflicht, sonst greifen `BrokerIdentity` und das
  Sharding nicht).
- Steht im Type-Registry und im Proto-Mapping (Pflicht, weil der Publish an die Shards
  ein Cluster-`RequestAsync` ist und cross-node serialisiert werden muss).
- Felder: `StreamId`, `Version`. Nichts weiter.

**Warum ein Typ und kein String/Enum:** Nur ein echter Typ läuft durch das typbasierte
Routing (Invariante 3); es gibt keine handgebaute Identität.

### 5.2 `IEventStoreRepository.ReadStreamAsync`

```csharp
Task<IReadOnlyList<EventEnvelope>> ReadStreamAsync(
    Guid streamId, int fromVersion, CancellationToken ct);
```

- Liefert die Events eines Streams ab `fromVersion`, geordnet, mit ihrer Version.
- Implementierung über die native Stream-Fetch-Funktion des Event-Stores.

**Warum:** Der gesamte Pull-Ansatz braucht „lies ab Position N" — das ist das eine
tragende neue Primitiv. Es wird identisch genutzt von Adaptern, Treibern (11.2) und
der Rehydration von Prozess-Aggregaten.

### 5.3 `IAggregateEnvelope.AggregateVersion`

Die Version jedes Events ist auf dem Envelope-Interface sichtbar, das Adapter und
Handler sehen.

**Warum:** Der Adapter braucht die Position pro Event für Pre-Dispatch-Guard und
Reihenfolge. Sie ist der Dedup-Schlüssel für append-artige Projektionen (7.5) und der
Rohstoff für deterministische Prozess-Identitäten (11.3). Ohne sie am Interface müsste
man auf konkrete Typen casten.

### 5.4 Fortschritts-Ablage (`IProjectionTracker`)

Pro `(Handler, Stream)` existiert eine durable Fortschrittsmarke: die höchste bereits
angewandte Version. Sie wird **vom Store geführt**, nicht vom Adapter — über das
optionale Interface `IProjectionTracker` (7.2). Der Adapter liest sie beim Aufwachen
und stößt ihr Vorrücken an; die konkrete Ablageform gehört dem Store.

**Warum beim Store:** Nur der Store kann Fortschritt und Effekt in dieselbe native
Transaktion legen und damit gemeinsam gültig machen. Läge die Marke in einem vom
Effekt getrennten Store, wäre sie ein Dual-Write — Effekt und Marke könnten bei Crash
divergieren. Der Adapter besitzt deshalb bewusst keine Transaktion (7.1).

**Session-Zuschnitt:** Co-Commit setzt voraus, dass der Store **eine Session pro
Batch** führt (alle Effekte + Marke, ein Commit) statt einer Session pro
Einzeloperation. Per-Event-Commit ist die zulässige Alternative — feinerer
Resume-Punkt, mehr Transaktionen; die Wahl trifft der Store.

### 5.5 Adapter (die eine Verarbeitungsmaschine, generiert)

Pro Handler-Klasse ein Adapter, gleich welchen Effekt deren Handler haben. Er:

- wird vom Receiver (5.6) für die `StateChangeVia{Event}`-Typen seiner Handler
  geweckt,
- besitzt den Pre-Dispatch-Guard,
- liest die Fortschrittsmarke über `IProjectionTracker` (sofern der Store sie
  anbietet),
- liest bei Weckung/Poll den Stream ab dieser Position,
- dispatcht die echten Events an die `Handle`-Methoden,
- **routet die Outputs nach Typ** (Kapitel 8): Repo-Write, Command, Plan, Trigger,
- ist Single-Writer pro Stream (Kapitel 6),
- stößt das Vorrücken der Fortschrittsmarke über den Store an.

**Warum eine Maschine statt drei:** Projektion, Reaktion und Prozess-Start
unterscheiden sich nur im Effekt — nicht in Signal, Marke, Log-Read, Ordnung oder
Sharding. Eine Maschine mit Output-Routing hält Invariante 5 und den Generator klein.

### 5.6 Receiver (zustandslos, generiert)

Pro Handler-Klasse und Node ein lokaler Actor:

- registriert sich beim PubSub für die `StateChangeVia{Event}`-Typen der Klasse
  (SubscriberId node-eindeutig),
- liest aus dem Signal nur `(StreamId, Version)`,
- leitet per `RequestAsync` als `Wake(StreamId)` an `(AdapterKind, StreamId)` weiter.

**Warum zustandslos und mehrfach erlaubt:** Er trifft keine Entscheidung und hält
nichts. Verlust, Duplikat, Wettrennen zwischen Nodes — alles landet bei derselben
Adapter-Instanz und wird dort vom Guard neutralisiert.

---

## 6. Sharding und Single-Writer

Die Verarbeitung eines Streams muss **pro Stream serialisiert und von genau einer
Instanz im Cluster** ausgeführt werden.

- **Warum pro Stream serialisiert:** Sonst zerstörst du die Reihenfolge, die du gerade
  aus dem Log gelesen hast.
- **Warum genau eine Instanz:** Sonst verarbeiten zwei Instanzen denselben Stream und
  rücken denselben Fortschritt konkurrierend vor — doppelte Arbeit und
  Marken-Konflikte.
- **Warum verschiedene Streams parallel dürfen:** Zwischen Streams gibt es keine
  Ordnungsbeziehung. Bei vielen Aggregaten ergibt das hohe Parallelität.

**Umsetzung:** Der Adapter läuft als **virtueller Cluster-Actor** unter einer
typisierten Identität — Kind-Name generiert aus dem Handler-Typ, `StreamId` als
Wert-Schlüssel; dasselbe Muster wie die Aggregat-Adressierung
`(AggregatTyp, AggregatId)`. Aktiviert wird er on demand durch das erste `Wake`; bei
Idle passiviert er. Passivierung ist unkritisch, weil sein gesamter Zustand (die
Marke) durabel im Store liegt und bei Aktivierung gelesen wird. Es wird kein
Identitäts-String von Hand gebaut.

Der Weg dorthin ist zweistufig, weil PubSub an PIDs zustellt und Cluster-Identitäten
erst durch Requests entstehen:

```
Signal ──▶ Shard (Typ-Route) ──▶ Receiver-PID (je Node, zustandslos)
                                     │  cluster.RequestAsync(Kind + StreamId, Wake)
                                     ▼
                              Adapter-Instanz (genau eine im Cluster)
```

Nur der zustandslose Teil darf mehrfach existieren; alles mit Zustand pro Schlüssel
lebt als Cluster-Identität. Eine einzige Design-Entscheidung — Sharding pro Stream
über eine typisierte Identität — liefert damit gleichzeitig Parallelität, Reihenfolge
und Single-Writer.

---

## 7. Garantie: Exactly-once wirksam

Das Ziel ist nicht „jedes Event läuft genau einmal durch den Handler" (im verteilten
System unmöglich), sondern „jedes Event ist genau einmal **wirksam**".

Der entscheidende Punkt dieses Kapitels: **Das Framework stellt dafür nur einen
Nahtpunkt bereit — es garantiert die Wirksamkeit nicht selbst.** Ob aus „wirksam"
tatsächlich „genau einmal wirksam" wird, entscheidet die Store-Implementierung. Das
Framework liefert die zwei semantischen Operationen, die es braucht, um ein bereits
angewandtes Event nicht erneut zu dispatchen, und ruft sie an der richtigen Stelle
auf. Alles Weitere gehört dem Store.

### 7.1 Die eine Bedingung

> **Effekt (Repo-Write) und Fortschritt (Verarbeitungsmarke) müssen gemeinsam gültig
> werden — atomar oder durch Idempotenz.**

Erfüllt wird sie nicht vom Framework, sondern vom Store. Das Framework besitzt die
*Policy* (welche Version kommt als Nächstes, Pre-Dispatch-Guard, Log-Read ab Position,
Poll). Der Store besitzt den *Mechanismus* (wie und wann die Marke durabel wird und ob
sie mit dem Effekt in einer Transaktion liegt). Der Adapter stellt **keine**
Transaktion bereit — er kann es nicht, ohne die Speicherform des Stores zu kennen,
und genau das soll er nicht.

Merksatz für den Fehlerpfad (4.7): **Vorwärts durch Wiederholung statt rückwärts durch
Rollback** — der Store verwirft Uncommittetes, die Idempotenz neutralisiert
Committetes, und die Marke entscheidet, was als geschehen gilt.

### 7.2 Der Vertrag: `IProjectionTracker` (zusätzlich, optional)

Das bestehende Projektions-/Write-Interface (das Repo mit seinen fachlichen
`Set…`/`Upsert…`-Methoden) bleibt **unverändert** bestehen. Daneben tritt ein
zusätzliches, optionales Interface. Ein Store, der Fortschritts-getriebene
Deduplizierung unterstützen will, implementiert es; ein Store ohne bleibt voll
funktionsfähig (7.4, Fallback).

```csharp
namespace Abstractions;

/// <summary>
/// Optionale Zusatzfähigkeit, die ein Projektions-Store implementieren KANN.
/// Liefert dem Framework die zwei semantischen Operationen, die es braucht,
/// um ein Event nicht erneut anzuwenden: den Fortschritt lesen und vorrücken.
///
/// Das Framework STELLT diesen Nahtpunkt BEREIT. Es schreibt nicht vor, WIE
/// der Store den Fortschritt ablegt, und es garantiert NICHT, dass Fortschritt
/// und fachlicher Effekt gemeinsam durabel werden — das ist allein Sache des
/// Stores. Das Projektions-/Write-Interface bleibt unverändert daneben bestehen.
/// </summary>
public interface IProjectionTracker
{
    /// <summary>
    /// Höchste Stream-Version, die für diesen Handler bereits durabel
    /// angewandt wurde, oder -1 wenn noch nichts. Der Adapter überspringt
    /// jedes Event mit version &lt;= diesem Wert und liest den Stream ab
    /// version + 1.
    /// </summary>
    Task<int> LastProcessedVersionAsync(
        string projectionId, Guid streamId, CancellationToken ct);

    /// <summary>
    /// Vermerkt, dass der Handler für diesen Stream auf
    /// <paramref name="version"/> vorgerückt ist. Ob der Store das in
    /// dieselbe Transaktion wie seine Effekt-Schreibvorgänge legt, ist
    /// ausschließlich seine Entscheidung.
    /// </summary>
    Task MarkProcessedAsync(
        string projectionId, Guid streamId, int version, CancellationToken ct);
}
```

**Warum genau diese zwei Funktionen und kein Scope:** Das Interface drückt nur aus,
was das Framework *semantisch* braucht — Resume-/Guard-Punkt lesen, Fortschritt
vorrücken. Eine Begin/Commit-Mechanik ist **eine** mögliche Implementierung, gehört
aber nicht in den Vertrag, weil sie eine Implementierungsstrategie erzwingen würde.

**Warum die Version als Schlüssel und keine Event-Id-Menge:** Der Log-Read liefert die
Events eines Streams geordnet und lückenlos. Ein monotoner Fortschritt
`(projectionId, streamId, version)` ist O(1), wächst nicht unbegrenzt und bildet die
Ordnung ab. Eine Menge „verarbeiteter Event-Ids" wäre schwächer und unbegrenzt groß.

### 7.3 Abwicklung im Adapter

```csharp
// Im Adapter, pro Aufwachen/Poll für einen Stream:

int applied = -1;
if (_store is IProjectionTracker tracker)
    applied = await tracker.LastProcessedVersionAsync(_projectionId, streamId, ct);

var events = await _eventStore.ReadStreamAsync(streamId, applied + 1, ct);

int last = applied;
foreach (var e in events)
{
    if (e.AggregateVersion <= applied) continue;   // Pre-Dispatch-Guard
    await DispatchAsync(e, writer, emit);           // Effekt + Output-Routing
    last = e.AggregateVersion;
}

if (_store is IProjectionTracker t && last > applied)
    await t.MarkProcessedAsync(_projectionId, streamId, last, ct);
```

- **Der Pre-Dispatch-Guard** verwirft alles, was schon durabel angewandt wurde — den
  Normalfall eines wiederholten Reads (Poll, doppeltes Signal, Neustart).
- **Es gibt genau eine Autorität** dafür, „was ist angewandt": der vom Store
  gelieferte `LastProcessedVersionAsync`. Die Leseposition des Adapters ist nur eine
  Optimierung.

### 7.4 Zwei Store-Realitäten (nicht erzwungen, nur benannt)

- **Store faltet den Fortschritt in seine Effekt-Transaktion** (bei Marten: beides
  über dieselbe Session, ein `SaveChangesAsync`, eine Postgres-Transaktion) → Effekt
  und Fortschritt werden gemeinsam gültig → **exactly-once-wirksam**. Der Fehlerpfad
  (Verwerfen der uncommitteten Session) gehört ebenfalls dem Store.
- **Store schreibt den Fortschritt separat** → Crash-Fenster zwischen Effekt-Commit
  und `MarkProcessedAsync` → **at-least-once**; beim Neustart läuft die Batch erneut.

Beides ist zulässig. `IProjectionTracker` zu implementieren ist **keine Zusage von
Atomarität** — Reviews müssen das pro Store verifizieren.

**Store ohne `IProjectionTracker` (Fallback):** `applied` bleibt -1, es wird stets ab
Stream-Anfang gelesen, der Guard ist wirkungslos. Voll unterstützt — die Handler
**müssen** dann idempotent sein.

### 7.5 Konsequenz für append-artige Projektionen

Idempotente Effekte (Set/Upsert nach Schlüssel) sind gegen Doppelverarbeitung immun.
**Nicht** idempotent sind Effekte, die *anhängen* (Historien-Projektionen). Eine
solche Projektion braucht genau eines von beidem: einen Store mit atomarem Co-Commit
(7.4, erster Fall) **oder** einen **Dedup-Schlüssel**
`(AggregateId, AggregateVersion)` als deterministische Dokument-Id bzw.
Unique-Constraint, sodass ein wiederholter Append zum No-op wird. Das Framework
erzwingt keine der Varianten; die Wahl ist eine fachliche Zusage der Projektion.

### 7.6 Grenze: Wirkungen außerhalb des Stores

Der Tracker deckt exakt den **lokalen Repo-Effekt** ab — nicht Wirkungen nach außen.
Reaktive Emits und Deps-Writes (Redis) sind Cluster-/Netzwerk-Calls außerhalb der
Store-Transaktion; bei einem Retry können sie erneut ausgehen. Das ist die Grenze
jeder Nicht-Outbox-Lösung: Downstream-Konsumenten müssen idempotent sein, oder man
legt den Emit als Outbox-Dokument in die Store-Transaktion und relayt separat.

Für **Reaktionen und Prozesse** ist dieser Fall der Normalfall — ihr Effekt ist immer
ein Command. Die Kapitel 9.3 und 11.4 bauen die geforderte Empfänger-Idempotenz
deshalb systematisch aus: deterministische Identitäten plus Noop-Decider.

### 7.7 Rebuild (Replay)

Ein bewusster Neuaufbau ist derselbe Codepfad mit Fortschritt = -1 und geleertem Ziel.
Es wird nie in einen befüllten Zustand replayt. Das Zurücksetzen der Marke ist eine
Store-Operation, kein neues Framework-Konzept. Grundsatz: Abgeleitetes darf
zurückgespult werden, weil es aus der Wahrheit neu berechenbar ist; der Log selbst
spult nie zurück.

### 7.8 Verantwortungsgrenze in einem Satz

Das Framework stellt `IProjectionTracker` bereit und ruft es an der richtigen Stelle
auf — mehr nicht; ob daraus exactly-once-wirksam oder at-least-once wird, ist eine
Eigenschaft der Store-Implementierung, und für append-artige Projektionen liegt die
Pflicht zur Atomarität oder zum Dedup-Schlüssel bei der Projektion, nicht beim
Framework.

---

## 8. Was der Entwickler schreibt: ein Handler, Routing per Rückgabetyp

Es gibt **eine Sorte Handler-Klasse** — keine Taxonomie aus Projektions-, Reflex- und
Prozess-Klassen. Was eine Klasse *ist*, ergibt sich vollständig aus dem, was ihre
`Handle`-Methoden tun und zurückgeben:

| Der Handler … | … ist faktisch | Garantie-Anforderung |
|---|---|---|
| schreibt nur ins Repo | eine Projektion | Idempotenz oder Co-Commit (7.4/7.5) |
| yieldet Commands | eine Reaktion | Empfänger-Dedup per Noop-Decider (9.3) |
| yieldet einen Plan | ein Prozess-Start | deterministische ProzessId (11.3), Rest generiert |
| mischt das | alles zugleich | jeweils pro Output-Typ |

Die Regel in einem Satz: **Die Signatur wählt die Route (wann werde ich geweckt), der
Rückgabetyp wählt den Effekt (was passiert), das Interface des Stores wählt die
Garantie.** Nirgendwo eine Config, ein String, eine Registrierung.

Der Rückgabetyp ist das bestehende `OneOf`-Muster: Die Typ-Argumente deklarieren zur
Compile-Zeit abschließend, welche Outputs möglich sind; der private Konstruktor mit
impliziten Konversionen erzwingt den Kontrakt; der Generator liest die Typ-Argumente
statisch aus der Signatur. `IProzessPlan` hängt dafür — wie `IPipelineTrigger` —
unter `IPipelineOutput` (10.2). „Yieldet nichts" (`yield break`) ist der legale Fall
„diese Event-Instanz betrifft mich nicht".

Zwei Analyzer-Regeln wachen über die Reinheit (Invariante 5), statt sie per
Interface-Krücke zu erzwingen:

- **Keine mutablen Instanzfelder in Handler-Klassen.** Wer ein Feld braucht, braucht
  ein Gedächtnis — und ein Gedächtnis ist ein Aggregat (9.1). Compile-Warnung mit
  genau diesem Hinweis.
- **`Schritte` eines Plans ist pure Funktion der Record-Felder** (10.3) — kein
  `DateTime.Now`, kein I/O; sonst wäre der rehydrierte Plan ein anderer als der
  gestartete.

Der Fehlerbegriff für Handler bleibt der aus 4.7: Wer wirft, wird wiederholt. Wer
fachlich „nein" sagen will, tut das nie per Exception, sondern über die Domäne
(Noop oder Ablehnungs-Event des Ziel-Aggregats).

---

## 9. Reaktion oder Prozess: die Entscheidungsregel

Ein Event „braucht" nie einen Prozess — **Konsumenten** brauchen einen. Der Auslöser
ist rein technisch erkennbar: **Zustand.**

### 9.1 Der Tripwire-Test

> Ist `Handle(Event, Envelope) → Outputs` als feldlose, pure Funktion schreibbar?
> **→ Reaktion.** Zustandslos, pro Stream shardbar, at-least-once genügt.
>
> Braucht die Reaktion ein Instanzfeld, ein früheres Event, eine Uhr oder ein Undo?
> **→ Prozess.** Der Zustand wird ein eigener Aggregat-Stream; der Handler yieldet
> einen Plan.

Der Tripwire ist buchstäblich: *„Ich brauche ein Feld" = „Ich brauche einen Prozess."*
Ein Instanzfeld in einer Handler-Klasse überlebt keinen Crash, divergiert bei zwei
Instanzen und wird beim Replay falsch aufgebaut — genau die drei Probleme, die ein
Aggregat-Stream löst.

### 9.2 Die vier Muster, die den Tripwire auslösen

Alle vier sind derselbe Auslöser in Verkleidung — „die Reaktion ist eine Funktion
*mehrerer* Ereignisse über die Zeit, nicht eines":

1. **Fan-in / Korrelation:** „Sende Y, wenn A *und* B da sind." Erkennbar am Schema:
   eine Korrelations-Id taucht in Events verschiedener Aggregat-Typen auf. (Fan-*out*
   — ein Event, drei Commands — bleibt dagegen zustandslos.)
2. **Daten aus einem früheren Event:** Der Command braucht ein Feld, das im
   auslösenden Event fehlt. Erst prüfen, ob das Event die Daten schlicht mitführen
   kann — das ist oft die billigere Lösung als ein Prozess.
3. **Deadline:** „Wenn nach 30 Minuten kein X kam, dann Y." `ScheduleSelf` lebt in der
   Mailbox und stirbt mit dem Node — eine prozessrelevante Frist braucht durablen
   Zustand (19.3).
4. **Kompensation:** Um den Gegen-Command zu bauen, muss man wissen, was bisher
   gelang.

### 9.3 Warum Reaktionen ohne eigene Dedup auskommen

Der Effekt einer Reaktion ist ein Command — ein Cluster-Call, der nie mit einer
Fortschrittsmarke co-committen kann (7.6). Es gilt also **immer at-least-once beim
Versand**, und die Dedup liegt per Design beim **Ziel-Aggregat**:

- **OCC:** Ein doppelter Command mit veralteter erwarteter Version wird abgelehnt.
- **Noop-Decider:** Der Decider erkennt den Wiederholungsfall fachlich („kenne diese
  TransferId schon") und produziert keine Events → Erfolg ohne Wirkung, keine
  Signale, die Kaskade verpufft.
- **Deterministische CommandId:** Der Adapter leitet sie aus
  `(StreamId, AggregateVersion)` des auslösenden Events ab — ein Duplikat ist damit
  *als Duplikat erkennbar*, nicht nur zufällig unschädlich.

### 9.4 Das „manchmal"-Problem

Dasselbe Event kann mal zu einem Prozess gehören und mal nicht
(`BetragGutgeschrieben`: Überweisung vs. Gehaltseingang). Die Regel:

> **Route = Typ, Zugehörigkeit = Inhalt.** Der Typ bringt das Event zu allen
> potenziellen Interessenten; ob eine Instanz einen Prozess betrifft, entscheidet ein
> Feld darin (die Korrelations-Id). `yield break` ist die legale Antwort „betrifft
> mich nicht".

Zwei Smells mit Ausweg: Kann der Inhalt die Zugehörigkeit *nicht* entscheiden, fehlt
dem Event sein Korrelationsfeld (die Tatsache „im Rahmen von Vorgang T1" ist Teil der
Tatsache). Ist die Verteilung extrem schief (99 % Instanzen ohne Prozess-Bezug),
liefert Invariante 3 das Werkzeug: **zwei Event-Typen** — der Decider wählt per
Return-Typ, welche Tatsache er festhält, und der Prozess hängt nur an einer der
beiden Routen. Was fürs Routing zählen soll, wird Typ; was nur fürs Verhalten zählt,
bleibt Inhalt.

---

## 10. Der Plan

> ⚠ **Überholt (Kap. 10–12).** Der hier beschriebene Prozess-Ansatz — Plan als
> `IProzessPlan`/`ProzessSchritte.Dann`, generiertes Prozess-Aggregat + separater **Treiber**,
> Zustandsmaschine — wurde verworfen und durch einen Event-Regel-DAG (Petri-Netz) ersetzt:
> **`docs/prozess-neubau-event-regeln-dag.md`** (aktuell) + **`docs/anleitung-prozess-schreiben.md`**
> (Entwickler-Anleitung). Kapitel 1–9 (Invarianten, zwei Kanäle, Log-Read-Naht, `IProjectionTracker`,
> „eine Maschine") beschreiben weiter den aktuellen Stand. Kap. 10–12 nur noch als historischer Kontext.

### 10.1 Die Anforderung in einem Satz

> „Diese Schritte müssen klappen, und zwar alle — wenn nicht, gleiche die bereits
> gelungenen fachlich aus."

Das ist der Ersatz für die Transaktion, die es über Aggregat-Grenzen nicht geben
kann. Wichtig ist das ehrliche Wort **ausgleichen** statt „zurücknehmen": Die
Zwischenschritte stehen im Log und wurden gesehen (keine Isolation) — Kompensation
ist ein *neues* Gegen-Event, nie ein Radieren. Der Zustand kehrt zurück, indem die
Geschichte länger wird, nie kürzer. Und weil „mach X rückgängig" eine
Domänen-Entscheidung ist (was ist das Gegenteil einer Gutschrift, wenn das Geld schon
ausgegeben ist?), kann das Framework Kompensation orchestrieren, aber nie erfinden.

### 10.2 Die API — vollständig

Der Entwickler schreibt genau zwei Dinge. Erstens den Plan als **benannten
Domänen-Typ** — damit gilt „ein Typ = eine Route" auch für Prozesse:

```csharp
public record ÜberweisungsPlan(Guid Quelle, Guid Ziel, decimal Betrag) : IProzessPlan
{
    public ProzessSchritte Schritte => ProzessSchritte
        .Dann(new ReserviereBetrag(Quelle, Betrag),
              rückgängig: new GebeReservierungFrei(Quelle))
        .Dann(new SchreibeGut(Ziel, Betrag),
              rückgängig: new StorniereGutschrift(Ziel, Betrag))
        .Dann(new BucheReservierung(Quelle));
}
```

Zweitens die Bindung — eine ganz normale `Handle`-Methode; die Signatur sagt *wann*,
der `OneOf`-Arm sagt *was*:

```csharp
public partial class Überweisungen : ISubscriber
{
    public IEnumerable<OneOf<ÜberweisungsPlan, ManuellePrüfungAngefordert>>
        Handle(ÜberweisungAngefordert evt, IAggregateEnvelope env)
    {
        if (evt.Betrag <= 0)          yield break;                            // kein Prozess
        else if (evt.Betrag > 10_000) yield return new ManuellePrüfungAngefordert(evt);
        else                          yield return new ÜberweisungsPlan(
                                          evt.QuellKonto, evt.ZielKonto, evt.Betrag);
    }
}
```

Die Abstraktionen dahinter:

```csharp
namespace Abstractions;

/// <summary>
/// Marker für Plan-Typen. Erbt von IPipelineOutput, damit Pläne — wie
/// IPipelineTrigger — als OneOf-Arm ge-yielded werden können und der
/// Generator sie statisch aus den Signaturen liest.
/// </summary>
public interface IProzessPlan : IPipelineOutput
{
    ProzessSchritte Schritte { get; }
}

/// <summary>
/// Unveränderliche Schrittliste. „Dann" ist bewusst das einzige Verb:
/// Pläne verzweigen nicht (Kapitel 13). Jeder Schritt ist ein Command plus
/// optional sein fachliches Gegenteil.
/// </summary>
public sealed class ProzessSchritte
{
    public static ProzessSchritte Dann(ICommand schritt, ICommand? rückgängig = null);
    public ProzessSchritte Dann(ICommand schritt, ICommand? rückgängig = null);
    public IReadOnlyList<(ICommand Schritt, ICommand? Rückgängig)> Alle { get; }
}
```

### 10.3 Warum der Plan Daten ist, nicht Code

Der Plan wandert als Payload in das `ProzessGestartet`-Event (11.1). Deshalb:

- **Rehydrierbar:** Nach einem Crash rekonstruiert sich `Schritte` deterministisch
  aus den Record-Feldern — kein Closure-Problem; die Kompensations-Commands sind
  jederzeit neu berechenbar.
- **Deployment-fest:** Laufende Prozesse laufen mit *ihrem* Plan zu Ende; neue Starts
  bekommen den neuen. Kein Migrations-Drama mitten im Vorgang.
- **Analyzer-Pflicht:** `Schritte` ist pure Funktion der Record-Felder (Kapitel 8).

Der Warnpfahl: Sobald jemand `if` oder Schleifen *im Plan* will, baut er eine
Programmiersprache in ein Datenformat. Verzweigungslogik gehört in Handler und
Decider; der vorgesehene Ausweg ist die Verkettung (Kapitel 13).

### 10.4 Invarianten-Prüfung

Invariante 3: Der Plan-Typ ist die Route (generierte Tabelle
`Plan-Typ → Prozess-Kind`, das Geschwister von `TriggerToPipelineId`). Invariante 4:
Der Generator liest `OneOf`-Typargumente statisch; zur Laufzeit nur der generierte
Output-Switch. Invariante 5: kein Cursor, keine Version, keine Persistenz im
Fachcode. Invarianten 1/2: Der Plan transportiert nichts — er wird geloggt und
gelesen wie alles andere.

---

## 11. Prozess-Aggregat und Treiber (beide generiert)

### 11.1 Der Prozess ist ein Aggregat

Das Prozess-Gedächtnis braucht: Durabilität, genau eine Instanz,
Duplikat-Erkennung, Rekonstruktion nach Crash, Benachrichtigung bei Änderung. Das ist
Wort für Wort die Definition des Aggregats — also wird **kein neues Konstrukt**
gebaut, sondern pro Plan-Typ ein Aggregat generiert:

- **State (gefaltet, nie gespeichert):** Plan, Status, Menge quittierter Schritte.
- **Commands:** `StarteProzess(plan)`, `SchrittErledigt(n)`,
  `SchrittGescheitert(n, grund)`, `RückabwicklungErledigt(n)`,
  `RückabwicklungGescheitert(n, grund)`.
- **Events:** `ProzessGestartet(plan)`, `SchrittErledigt(n)`,
  `SchrittGescheitert(n, grund)`, `RückabwicklungErledigt(n)`,
  `ProzessAbgeschlossen`, `ProzessFehlgeschlagen(grund)`, `KlärungNötig(grund)`.
- **Decider-Regeln (für jeden Plan identisch, deshalb generierbar):** unbekannter
  Zustandsübergang oder bereits bekannte Quittung → Noop; sonst das passende Event.

Beispiel-Stream `ÜberweisungsProzess T1` (Fehlerfall):

```
1  ProzessGestartet        (Plan als Daten)
2  SchrittErledigt(1)
3  SchrittGescheitert(2, "Konto gesperrt")
4  RückabwicklungErledigt(1)
5  ProzessFehlgeschlagen("Konto gesperrt")
```

„Dran ist Schritt 2" ist nirgendwo gespeichert — es *ergibt sich* aus der Faltung,
wie ein Kontostand aus Buchungen. Rehydration, Signale, Debug-Sichtbarkeit,
Projektionen auf Prozess-Streams (Monitoring): alles geschenkt, weil es ein echtes
Aggregat ist.

### 11.2 Der Treiber ist ein Adapter

Das Prozess-Aggregat ruft nicht selbst beim Ziel-Aggregat an, weil **Decider pur
sind** — beim Rehydrieren würde sonst jeder Neustart die Commands erneut feuern. Es
gilt die Arbeitsteilung des ganzen Systems: *Das Aggregat merkt sich, der Adapter
handelt.*

Der Treiber ist der ganz normale Adapter aus Kapitel 5.5 **auf dem Prozess-Stream** —
Signal, Marke, Log-Read, Guard, Single-Writer `(TreiberKind, ProzessId)` — nur sein
Effekt ist ein Command mit Quittung:

1. Weckung → Faltung lesen → „dran ist Schritt n".
2. `RequestAsync<CommandResult>` an das Ziel-Aggregat (der bestehende Antwortkanal
   mit `Success`, `RejectionEvent`, `NewVersion`).
3. Erfolg → `SchrittErledigt(n)` ans Prozess-Aggregat; fachliche Ablehnung →
   `SchrittGescheitert(n, grund)`.
4. Nach `SchrittGescheitert`: die quittierten Schritte in **Gegenreihenfolge** über
   ihre `rückgängig`-Commands ausgleichen, jede Rückabwicklung genauso quittieren.
5. Scheitert eine Rückabwicklung endgültig (Retry-Budget erschöpft): `KlärungNötig`
   + Alarm — nie Endlosschleife, nie automatisches Überspringen.

**Latenz-Abkürzung:** Der Treiber ist Single-Writer seines Prozesses — nach einer
bestätigten Quittung darf er den Folgeschritt **sofort in derselben Wachphase**
anstoßen. Der Signal→Log-Read-Weg bleibt das Sicherheitsnetz für den Crash-Fall.
Merksatz: *Das Log ist die Wahrheit, das Selbst-Weitermachen ist die Abkürzung.*

### 11.3 Deterministische Identitäten

- **ProzessId** = deterministisch aus `(PlanTyp, StreamId, Version)` des auslösenden
  Events. Jede Wiederholung des Starts (doppeltes Signal, Poll, Crash nach Send)
  erzeugt dieselbe Id → das Prozess-Aggregat antwortet beim zweiten `StarteProzess`
  mit Noop. Der Plan-Typ gehört in die Id, damit **dasselbe Event mehrere
  verschiedene Prozesse** kollisionsfrei starten kann.
- **CommandId** eines Schritt-Commands = deterministisch aus `(ProzessId, SchrittNr)`
  (Rückabwicklungen mit eigenem Namensraum). Duplikate sind damit als Duplikate
  erkennbar.

### 11.4 Garantie-Aussage (die ehrliche)

Der Prozess-Effekt ist immer ein Cluster-Call — er kann **nie** mit einer Marke
co-committen. Deshalb lautet die Garantie nicht „exactly-once gesendet", sondern:

> **At-least-once beim Versand, exactly-once wirksam durch den Empfänger.**
> Voraussetzung: Jeder Schritt- und Rückabwicklungs-Command ist beim Ziel-Aggregat
> idempotent per Korrelations-Id (Noop-Muster im Decider). Das ist die eine Pflicht,
> die die Domäne für die Prozessteilnahme erbringt — eine Zeile pro Decider.

Das ist exakt der in 7.6 vorgesehene Weg für Wirkungen außerhalb des Stores.

### 11.5 Zustandsmaschine (generiert aus einem n-Schritte-Plan)

```
            StarteProzess
                 │
                 ▼
   ┌────── Läuft (Schritt k) ◀────────────┐
   │             │                        │
   │   SchrittErledigt(k), k<n ───────────┘   (k := k+1)
   │             │
   │   SchrittErledigt(n)
   │             ▼
   │       Abgeschlossen
   │
   │   SchrittGescheitert(k)
   ▼             ▼
        Rückabwicklung (j = k-1 … 1)
                 │
   RückabwicklungErledigt(alle) ─▶ Fehlgeschlagen
   RückabwicklungGescheitert     ─▶ KlärungNötig (+ Alarm, Mensch entscheidet)
```

**Entwurfsregeln für Pläne** (fachlich, aus 10.1 abgeleitet): Den am ehesten fachlich
scheiternden Schritt zuerst — dann ist im häufigsten Fehlerfall nichts auszugleichen.
Und jeder Kompensations-Pfad braucht einen Boden: entweder ein garantiert
durchführbarer Gegen-Command oder der explizite Weg nach `KlärungNötig`.

### 11.6 Invarianten-Prüfung

Der Prozess-Stream ist ein gewöhnlicher Stream (Inv. 1). Treiber-Weckung läuft über
gewöhnliche Signale (Inv. 2). Prozess-Kind und Treiber-Kind kommen aus dem Plan-Typ
(Inv. 3). Alles generiert, kein Laufzeit-Invoke (Inv. 4). Der Entwickler sieht weder
Aggregat noch Treiber (Inv. 5). Schritt-Ausgänge, auf die ein Prozess reagieren muss,
sind persistente Events bzw. `CommandResult`-Antworten (Inv. 6).

---

## 12. Referenz-Szenario: die Überweisung, Nachricht für Nachricht

Einheitliches Format: `Nr · Absender → Empfänger · Nachricht`. Legende:
**[E]** = Eintrag ins Log (durabler Punkt — hier darf alles abstürzen),
**[R]** = Ruf/Signal (darf verloren gehen), **[C]** = Command (darf doppelt
ankommen), **[L]** = Log-Read.

### 12.1 Happy Path

| Nr | Absender → Empfänger | Nachricht |
|---|---|---|
| 1 | Auslöser-Aggregat → Log S | **[E]** `ÜberweisungAngefordert` → Zeile 7 |
| 2 | Auslöser-Aggregat → Receiver (alle Nodes) | **[R]** „S, Zeile 7 ist neu" |
| 3 | Receiver → Adapter (S) | **[R]** `Wake(S)` — Cluster findet die eine Instanz |
| 4 | Adapter (S) → Log S | **[L]** liest ab Marke (6) → echtes Event aus Zeile 7 |
| 5 | Adapter (S) | `Handle` yieldet `ÜberweisungsPlan`; Marke → 7 |
| 6 | Adapter (S) → Prozess T1 | **[C]** `StarteProzess(Plan)` — Id aus (PlanTyp, S, 7) |
| 7 | Prozess T1 → Log T1 | **[E]** `ProzessGestartet` + Plan → Zeile 1 (Duplikat von 6? Noop) |
| 8 | Log T1 → Treiber (T1) | **[R]**+**[L]** Ruf; Faltung: „dran ist Schritt 1" |
| 9 | Treiber → Konto A | **[C]** `ReserviereBetrag(T1, 100 €)` — wartet auf Antwort |
| 10 | Konto A → Log A | **[E]** `BetragReserviert(T1)` · Antwort: ok |
| 11 | Treiber → Prozess T1 | **[C]** `SchrittErledigt(1)` → **[E]** Zeile 2 |
| 12 | Treiber → Konto B | **[C]** `SchreibeGut(T1, 100 €)` · ok → **[E]** Zeile 3 |
| 13 | Treiber → Konto A | **[C]** `BucheReservierung(T1)` · ok → **[E]** Zeile 4 |
| 14 | Prozess T1 → Log T1 | **[E]** `ProzessAbgeschlossen` → Zeile 5 · Ende |

Parallel und unabhängig davon bedienen dieselben Signale alle anderen Zuhörer —
Kontoauszug-Projektion, Statistik, Client-PubSub. Der Prozess konsumiert nichts
exklusiv; er ist ein weiterer Abonnent derselben Typ-Routen.

### 12.2 Fehlerfall mit Kompensation (zweigt nach Nr. 11 ab)

| Nr | Absender → Empfänger | Nachricht |
|---|---|---|
| 12 | Treiber → Konto B | **[C]** `SchreibeGut(T1, 100 €)` |
| 13 | Konto B → Treiber | Antwort: abgelehnt — „Konto gesperrt" (fachliches Nein) |
| 14 | Treiber → Prozess T1 | **[C]** `SchrittGescheitert(2)` → **[E]** Zeile 3 · Rückwärtsgang |
| 15 | Log T1 → Treiber | **[R]**+**[L]** „gleiche Erledigtes aus, rückwärts" — nur Schritt 1 |
| 16 | Treiber → Konto A | **[C]** `GebeReservierungFrei(T1)` |
| 17 | Konto A → Log A | **[E]** `ReservierungFreigegeben(T1)` · ok — nichts wurde radiert |
| 18 | Treiber → Prozess T1 | **[C]** `RückabwicklungErledigt(1)` → **[E]** Zeile 4 |
| 19 | Prozess T1 → Log T1 | **[E]** `ProzessFehlgeschlagen` → Zeile 5 · Ende |

Der Kontoauszug von A zeigt danach beide Zeilen — Reservierung und Freigabe. Beides
ist wirklich passiert; Kompensation ist ein Vorwärts-Geschäftsvorfall.

### 12.3 Crash-Probe (zwischen Nr. 10 und 11)

Der Treiber-Node stirbt, nachdem Konto A committet hat, bevor die Quittung ankam.
Neustart (nächster Ruf oder Poll): Die Faltung von T1 sagt weiterhin „Schritt 1
offen" → der Treiber sendet `ReserviereBetrag(T1)` **erneut** → Konto-A-Decider:
„TransferId bekannt" → Noop, Antwort ok → Quittung, weiter mit Schritt 2. Keine
Doppelbuchung, kein Aufräumen — die Wiederholung war harmlos, weil der Empfänger
Duplikate erkennt. Dieselbe Probe funktioniert zwischen **jeder**
Nachrichten-Nummer: Jeder [E]-Punkt ist ein legaler Absturzpunkt.

---

## 13. Verkettung statt Verzweigung

Entscheidungen mitten im Prozess („bei über 10.000 € Compliance-Freigabe nach der
Reservierung") leben nie im Plan:

1. **Beim Start, wenn möglich:** Steht die Information im auslösenden Event, baut der
   Handler schlicht verschiedene Pläne (oder Plan-Typen). Die Verzweigung passiert
   vor dem Start; jeder Plan bleibt eine dumme Liste.
2. **Unterwegs, wenn nötig:** Die Entscheidung fällt in einem **Decider** — der wählt
   per Return-Typ, welche Tatsache er festhält (`BetragReserviert` vs.
   `BetragReserviertMitPrüfvorbehalt`). Auf dem zweiten Typ sitzt ein gewöhnlicher
   Handler, der den Folge-Plan yieldet; dessen Abschluss-Event startet die
   Fortsetzung.

> **Pläne verzweigen nicht; Pläne verketten sich über Events.** Verzweigungslogik
> liegt in Handlern und Decidern — dort, wo sie testbar, replaybar und fachlich
> begründet ist.

Muss die *erste* Kette bei Ablehnung der zweiten ausgeglichen werden, ist auch das
nur ein Handler: Er hört auf das Ablehnungs-Event der zweiten Kette und yieldet den
Kompensations-Command (oder einen kleinen Ausgleichs-Plan) für die erste — dieselbe
Mechanik, kein Sonderweg.

---

## 14. Deployment: wer wohnt wo

Zwei Wohnorte, eine Entwurfsregel: **Zustand pro Schlüssel → virtueller
Cluster-Actor (Wohnort B). Zustandslos → lokal pro Node (Wohnort A).**

| Element | Wohnort | Kind / Identity | Gespawnt von | Wann |
|---|---|---|---|---|
| Signal-Routen (Broker) | B | ShardKind/ManagerKind, `StateChangeViaX_{i}` | PubSub-Startup aktiviert | Boot |
| Receiver (je Handler-Klasse) | **A** | lokale PID, SubscriberId node-eindeutig | Receiver-Startup | Boot, jeder Node |
| Adapter | B | `{Handler}Adapter` + StreamId | erstes `Wake` | on demand |
| Poller (je Handler-Klasse) | B | `{Handler}Poller` + fix `"0"` | Boot-Ping, dann Self-Tick | Boot + selbst |
| Prozess-Aggregat | B | `{Plan}Prozess` + ProzessId | erstes `StarteProzess` | on demand |
| Treiber | B | `{Plan}Treiber` + ProzessId | erstes Signal des Prozess-Streams | on demand |
| Domänen-Aggregate, Pipelines, Broker | B | bestehende Kinds | wie bisher | wie bisher |

Boot-Sequenz (die bestehende Hosted-Service-Kette): ActorSystem-Bau registriert
zusätzlich Adapter-, Poller-, Prozess- und Treiber-Kinds (nur Rezepte) →
Cluster-Beitritt → Broker-Aktivierung (Signal-Typen sind weitere Einträge derselben
Schleife) → Receiver-Spawn (lokal) → Poller-Ping.

Eigenschaften, die aus der Karte folgen: **Keine Komponente kennt den Wohnort einer
anderen** (alles Kind + Identity). Passivierung ist überall unkritisch, weil aller
Zustand durabel ist (Marken im Store, Prozesse im Log) — mit einer Ausnahme: der
Poller hält sich per Self-Tick wach, weil er ohne eingehende Requests passiviert
würde. Heiße Streams halten ihren Adapter dauerhaft aktiv; Millionen kalter Streams
erzeugen kurzlebige Aktivierungen (Passivierungs-Timeout tunen, kein
Strukturproblem).

---

## 15. Code-Generierung

Viel generierter Output, aber wenige Generatoren, die bestehende erweitern statt neue
Welten zu bauen.

| Generator | Erzeugt | Basis |
|---|---|---|
| Signal-Typ-Generator | `StateChangeVia{Event} : IMessagePayload` je persistiertem Event-Typ; Eintrag in Type-Registry und Proto-Mapping | neu, klein |
| Receiver-Generator | pro Handler-Klasse einen zustandslosen Receiver (PubSub-Registrierung, Weiterleitung als `Wake`) | Teil des heutigen Subscriber-Generators |
| Adapter-Generator | pro Handler-Klasse einen Adapter: Fortschritts-Abwicklung (`IProjectionTracker`), Log-Read, Guard, Dispatch, Output-Routing, per-Stream-Kind | Evolution des Subscriber-Generators |
| Dispatch-Generator | typisierte `Handle`-Dispatch-Tabelle je Handler-Klasse; Output-Switch über die `OneOf`-Arme (Command/Trigger/Plan) | Evolution des bestehenden Dispatch-Generators |
| Emit-Wiring | Aggregat-Commit veröffentlicht `StateChangeVia{Event}` (parallel zum unveränderten Event → Client-PubSub) | Ergänzung im Publish-Pfad |
| Poller-Generator | pro Handler-Klasse ein Poller-Kind (Singleton, Self-Tick) | neu, klein |
| Plan-Routing-Generator | Tabelle `Plan-Typ → Prozess-Kind` (Geschwister von `TriggerToPipelineId`) | neu, klein |
| Prozess-Generator | pro Plan-Typ: Prozess-Aggregat (State, Decider, Applier, Events) und Treiber-Adapter | neu |

**Warum vertretbar:** Einmaliger Generator-Aufwand; zur Laufzeit reflection-freier,
typsicherer Code. Der Fachcode wird trivial — bis hin zum Prozess, den niemand von
Hand schreibt.

---

## 16. Touchpoints im bestehenden Code

| Ort | Änderung | Art |
|---|---|---|
| `IEventStoreRepository` | `ReadStreamAsync(streamId, fromVersion)` | additiv |
| Event-Store-Implementierung | Umsetzung via nativem Stream-Fetch | additiv |
| `IAggregateEnvelope` | `AggregateVersion` exponieren | additiv |
| Aggregat-Publish-Pfad | Version pro Event stempeln | Fix |
| Aggregat-Publish-Pfad | `StateChangeVia{Event}` emittieren | additiv |
| PubSub | unverändert (trägt zusätzlich Signale) | keine |
| Subscriber → Receiver + Adapter | Push-Dispatch ersetzen durch Signal → Marke lesen → Log-Read → Dispatch → Output-Routing | Kern-Umbau, lokal |
| Subscriber-Startup | spawnt nur noch Receiver (lokal, je Node); Adapter sind Cluster-Kinds | Änderung |
| `IProjectionTracker` | neues optionales Interface | neu, additiv |
| Store-Implementierungen | optional `IProjectionTracker`; ggf. Session-Zuschnitt „eine Session pro Batch" | neu, optional |
| `OneOf`-Constraint-Welt | `IProzessPlan : IPipelineOutput` (Marker) | additiv |
| ClusterConfig | zusätzliche Kinds: Adapter, Poller, Prozess, Treiber (alle generiert) | additiv |
| Generatoren | siehe Kapitel 15 | neu/Evolution |
| Projektionen und Repos | Write-Interface unverändert | additiv/optional |

---

## 17. Umsetzungsreihenfolge

So bleibt das System zwischen den Schritten lauffähig. Schritte 7–9 setzen jeden der
Schritte 1–6 voraus und keinen mehr.

1. `ReadStreamAsync` + `AggregateVersion`-Exposition + Per-Event-Versionsstempelung.
   (Fundament, ändert noch kein Verhalten.)
2. Signal-Typ-Generator + Type-/Proto-Registry-Eintrag. (Signale existieren, werden
   noch nicht konsumiert.)
3. Emit-Wiring: Signale werden zusätzlich veröffentlicht. (Noch niemand hört zu.)
4. `IProjectionTracker` + Receiver/Adapter mit Log-Read und Dispatch, zunächst ohne
   Cluster-Sharding, eine Projektion umstellen.
5. Per-Stream-Kinds + Poller (inkl. der in 19.1 getroffenen Discovery-Entscheidung).
6. Restliche Projektionen umstellen, alten Push-Pfad entfernen. Append-artige
   Projektionen mit Co-Commit oder Dedup-Schlüssel versehen (7.5).
7. `IProzessPlan`-Marker + Plan-Arm im Output-Routing + Plan-Routing-Tabelle.
   (Pläne kompilieren und routen; ein Plan-Typ ohne generierten Prozess erzeugt bis
   Schritt 8 eine klare Startup-Diagnose.)
8. Prozess-Generator: Prozess-Aggregat + Treiber; ein Pilot-Prozess (Referenz:
   Überweisung, Kapitel 12).
9. Kompensations-Pfad, Klärungs-Zustand, Prozess-Monitoring-Projektion („alle
   hängenden Prozesse" — eine gewöhnliche Projektion auf den Prozess-Streams).

---

## 18. Bewertungskriterien und Risiken

**Korrektheit — zu prüfen:**

- Reihenfolge pro Stream bleibt bei zufälliger Signal-Reihenfolge erhalten (Log-Read
  liefert sie).
- Kein Event geht verloren, wenn ein Signal verloren geht (Coalescing + Poll).
- Kein Event wird doppelt wirksam, **sofern der Store Fortschritt und Effekt atomar
  co-committet** (7.4). Andernfalls At-least-once — Handler idempotent, Appends
  dedupliziert (7.5).
- Ein von `StateChangeViaA` geweckter Adapter verarbeitet auch aufgelaufene B-Events.
- Doppelter Prozess-Start verpufft (deterministische Id + Noop, 11.3); wiederholte
  Schritt-Commands verpuffen (Empfänger-Noop, 11.4); ein Crash zwischen beliebigen
  Nachrichten aus Kapitel 12 führt zu Fortsetzung, nie zu Doppelwirkung.

**Reflection — zu prüfen:** Kein `Activator.CreateInstance`, kein
`MethodInfo.Invoke`, kein Assembly-Scan im Laufzeitpfad — auch nicht im Prozess-Pfad
(Plan-Routing ist eine generierte Tabelle über `OneOf`-Typargumente).

**Performance — Kostenstellen:**

- Ein zusätzlicher Signal-Publish pro Event pro interessierter Handler-Klasse.
- Ein indizierter Stream-Read pro Weckung/Poll (unter Last durch Coalescing oft
  sublinear).
- Poll-Grundlast pro Handler-Klasse (Intervall tunen).
- Single-Writer pro Stream ist der Durchsatz-Engpass je Stream; skaliert über die
  Zahl der Streams.
- Ein Prozess-Schritt kostet einen Command-Roundtrip plus zwei Appends
  (Fachereignis + Schritt-Quittung). Der Preis ist bewusst: Jeder Schritt ist erst
  notiert, dann kommt der nächste. Latenz je Vorgang in der Größenordnung
  50–150 ms — richtig für Geschäftsvorgänge, falsch für Request/Response (dafür:
  Query-Weg). Heiße geteilte Aggregate serialisieren; Prozesse untereinander sind
  perfekt parallel.

**Risiken:**

- Signal-Typ muss zwingend im Proto-/Type-Registry stehen, sonst routet er nicht
  cross-node. (Generator-Pflicht.)
- Getrennte Fortschritts-Schreibung = Dual-Write → nur At-least-once; append-artige
  Projektionen brauchen dann zwingend den Dedup-Schlüssel.
- `IProjectionTracker` implementieren ist keine Atomaritäts-Garantie — pro Store
  reviewen.
- Reaktive Emits/Deps-Writes außerhalb der Store-Transaktion (7.6) — Downstreams
  idempotent halten oder Outbox.
- Sehr lange Streams: Rehydration/Rebuild wächst linear — Snapshots vormerken.
- Bibliotheken (Event-Store-Serialisierung, Actor-Framework) nutzen intern
  Reflection; „reflection-frei" gilt für den eigenen Code.

---

## 19. Offene Entscheidungen (vor den genannten Schritten zu treffen)

Diese drei Punkte sind bewusst nicht entschieden, weil ihnen jeweils eine echte
Abwägung zugrunde liegt — Lücken im Sinne von „hier fehlt noch ein Beschluss", nicht
Risiken im Sinne von „könnte schiefgehen".

### 19.1 Stream-Quelle des Pollers (vor Schritt 5)

Der Poller braucht eine Quelle für „welche Streams gibt es / wo steht der Head".
Kandidaten: (a) globaler Event-Store-Scan über Sequenz/High-Water-Mark —
vollständig, Store-nah; (b) Abgleich gegen den bestehenden Versions-Tracker (Redis)
— billig, aber flüchtig und selbst nur abgeleitet; (c) durable Stream-Liste pro
Handler-Klasse — explizit, aber ein weiterer Write. Empfehlungstendenz: (a), weil
nur der Store vollständig ist und die Poll-Frequenz niedrig sein darf.

### 19.2 ExpectedVersion bei Prozess-Commands (vor Schritt 8)

Der Treiber kennt die aktuelle Version des Ziel-Aggregats nicht; unter Last sind
OCC-Konflikte der Normalfall. Kandidaten: Retry-Schleife über das bestehende
`CommandResult.NewVersion` (der Antwortkanal existiert genau dafür) oder ein
„append ohne Versionsprüfung"-Modus für als kommutativ deklarierte Prozess-Commands.
Startpunkt: Retry-Schleife, weil sie nichts Neues braucht.

### 19.3 Deadlines (Ausbaustufe, nach Schritt 9)

„Nach 30 Minuten ohne X → Y" braucht durablen Zeitzustand (`ScheduleSelf` stirbt mit
dem Node). Der skizzierte Weg: ein Schritt-Attribut im Plan
(`wartetAuf: typeof(XyzEvent), spätestens: …`), das der Generator in ein
`DeadlineGesetzt`-Event im Prozess-Stream übersetzt; der Treiber registriert beim
Log-Read einen Timer und quittiert Ablauf als `SchrittGescheitert("Timeout")`.
Dasselbe Attribut deckt Schritte ab, deren Ausgang nicht synchron im `CommandResult`
liegt, sondern später als Event eintrifft (asynchrone Genehmigungen). Bewusst
Ausbaustufe — der Kernweg setzt es nicht voraus.

---

## 20. Das Gesamtbild in vier Sätzen

1. Das Aggregat schreibt das rohe Event (Wahrheit im Log); daraus wird ein
   typisiertes `StateChangeVia{Event}`-Signal, das über das typ-geshardete PubSub via
   Receiver genau die Adapter weckt, deren Handler den Event-Typ behandeln.
2. Der Adapter holt bei Weckung die **echten** Events ab seiner Marke aus dem Log und
   dispatcht sie typsicher an die `Handle`-Methoden; Reihenfolge kommt aus dem
   Log-Read, Exactly-once-Wirksamkeit stellt der Store über das optionale
   `IProjectionTracker`-Interface her (atomar oder per Idempotenz).
3. Was der Handler zurückgibt, entscheidet den Effekt: Repo-Write (Projektion),
   Command (Reaktion) oder Plan (Prozess) — ein Plan erzeugt ein generiertes
   Prozess-Aggregat mit eigenem Stream und einen Treiber, der die Schritte anstößt,
   quittiert und im Fehlerfall die gelungenen fachlich ausgleicht, mit
   at-least-once-Versand und exactly-once-Wirksamkeit durch Noop-Decider an
   deterministischen Identitäten.
4. Der Entwickler schreibt nur `Handle`-Methoden, Decider, Repos und Plan-Records;
   Signal, Routing, Log-Read, Dispatch, Fortschritt, Sharding, Poll,
   Prozess-Gedächtnis und Treiber sind generiert oder Framework, reflection-frei und
   über Typen geroutet.
