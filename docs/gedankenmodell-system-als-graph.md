# Das System als Graph — das Gedankenmodell

> Status: **Gedankenmodell / Herleitung.** Dieses Dokument leitet die Architektur von Grund auf her:
> *warum* das System ein Graph ist, aus *welchen* festen Bauteilen es besteht, *wo* jede Garantie wohnt
> und *wie* man daraus refactort, ohne Garantien zu brechen.
> Companion zu `docs/zielbild-vereinheitlichte-konsumenten-maschine.md` (das *was* wir bauen); dieses Dokument
> ist das *wie man darüber denkt*. Vier Säulen bleiben: **Proto.Actor, Source-Generatoren, Marten/PostgreSQL,
> Redis.**

---

## 0. Lesart

Dieses Dokument ist eine **Herleitung**, kein Katalog. Es baut das Modell in einer Kette auf:
Beobachtung → Graph → Knoten → Bauteile → Kanten → Messages/Garantien → Kern-API → Komposition →
Refactoring/Bruch-Prävention. Jeder Abschnitt setzt den vorigen voraus. Am Ende steht eine Kurzfassung mit
allen Merksätzen.

---

## 1. Herleitung: warum überhaupt ein Graph?

Zwei Beobachtungen tragen alles Weitere:

1. **Alles ist eine Nachricht.** Command, Event, Signal, Query — es gibt im System nichts, was keine
   Nachricht ist. Jede Komponente *empfängt* Nachrichten und *erzeugt* Nachrichten.
2. **Der Log ist die einzige Wahrheit.** Der einzige folgenreiche Akt im System ist „ein Event an einen
   Stream anhängen". Alles andere ist *Ableitung* aus dem Log.

Aus (1) folgt: Jede Komponente hat dieselbe Form — **Nachricht rein → Nachricht raus**. Aus (2) folgt: Es
gibt zwei Rollen dieser Form — eine, die *Wahrheit schreibt* (in den Log), und eine, die *aus der Wahrheit
ableitet* (und dabei wieder Nachrichten erzeugt).

Wenn aber jede Komponente „Nachricht rein → Nachricht raus" ist und Komponenten sich über Nachrichten
verbinden, dann **ist das System ein Graph**:

> **Knoten = die Dinge, die Nachrichten verarbeiten. Kanten = die Nachrichten, die zwischen ihnen fließen.**

Der Rest des Dokuments ist die genaue Ausarbeitung dieses einen Satzes.

---

## 2. Die Knoten: zwei Sorten

Nicht jeder Knoten ist gleich. Der Graph hat **zwei** Knotensorten:

- **Aktive Knoten = Actors** (Verhalten): Schreiber, Projektion, Reaktion, Prozess-Manager, Poller,
  Broker-Shard.
- **Passive Knoten = Stream / Store** (Zustand): der Log, Read-Models, Indexe, DLQ, Snapshot, Cursor.

**Nur die aktiven Knoten kapseln wir in einen Actor.** Die passiven Knoten sind bewusst *keine* Actors — sie
sind geteilte Erinnerung, die ein Actor *in seinem Zug* berührt.

Warum bleiben Zustands-Knoten passiv? Machte man den Log zum Actor, liefen *alle* Schreibvorgänge durch
*eine* Mailbox → Flaschenhals, und die Pro-Aggregat-Parallelität wäre weg. Stattdessen ist jeder
Aggregat-Actor der **alleinige Torwächter seiner Scheibe** des Logs (Single-Writer), während der Log selbst
geteilter, passiver Speicher bleibt — auch von Konsumenten *gelesen*.

### Der Actor und der Zug

Ein **Actor** ist: eine **Identität** `(Art, Schlüssel)` + eine **Mailbox** (FIFO) + ein **Handler**. Die
Laufzeit (Proto.Cluster) garantiert drei Dinge, die wir *erben*, nicht bauen:

- **Single-Activation** — pro Identität höchstens eine lebende Instanz clusterweit.
- **In-Order / one-at-a-time** — die Mailbox verarbeitet genau eine Nachricht nach der anderen.
- **Ortsunabhängigkeit** — adressiert wird an die Identität, nicht an einen Ort.

Ein **Zug** ist die Bearbeitung *einer* Nachricht von Anfang bis Ende. Er hat immer dieselbe Form:

```
   Nachricht rein
      │
      ├─ (evtl.) aus einem Stream/Store lesen   ← Wahrheit/Zustand nachschlagen
      ├─ die reine Fachfunktion ausführen        ← der Kern (Kap. 6)
      └─ Nachrichten raus  +  (evtl.) schreiben   ← Kanten + Effekt
```

Der Zug ist gleichzeitig der **kritische Abschnitt** — innerhalb eines Zuges läuft auf *diesem* Actor nichts
anderes. Deshalb braucht es keine Locks: die Mailbox *ist* die Serialisierung.

---

## 3. Die festen Bauteile

Aus den Knoten fällt eine kleine, geschlossene Werkzeugkiste: **ein Daten-Bauteil, drei Struktur-Bauteile,
eine Verbindungs-Operation.** Mehr nicht — alles Weitere ist Instanz oder Komposition.

**① Message** *(Daten)*
`Umschlag{ An: Identität, Inhalt: Command|Event|Signal|Query, Spur: Ursache + deterministische ID }`.
Unveränderlich; kann verloren/dupliziert/umsortiert werden; die deterministische ID macht Duplikate
erkennbar. **Kein Verhalten.**

**② Actor** *(das einzige Verhalten)*
`Identität + Mailbox + Handler`. Garantien: Single-Activation, In-Order, Ortsunabhängigkeit.

**③ Stream** *(Speicher: Wahrheit)*
Append-only, geordnet, versioniert. Operationen: `Append(key, erwarteteVersion, records)` (OCC),
`Read(key, abVersion)`, `ReadGlobal(abSequenz)`. Material: **Postgres**. **Die einzige Wahrheit.**

**④ Store** *(Speicher: Ableitung)*
`Schlüssel → Wert`. `Get/Set/Delete`. Zwei Ausführungen: **transaktional** (kann mit einem `Stream.Append`
in *einer* Transaktion committen; Postgres) oder **flüchtig-schnell** (Redis). **Abgeleitet,
wiederherstellbar, nie Entscheidungsträger.**

**⑤ Co-Commit** *(Verbindungs-Operation, kein „Ding")*
`Stream.Append` **und** `Store.Set` in *einer* Transaktion — beides oder nichts. Bindet **Effekt an
Position** → der Zug ist absturzsicher. Nur mit transaktionalem Store.

### Zwei Achsen klassifizieren jedes Bauteil

- **Wahrheit ↔ Ableitung.** `Stream` ist Wahrheit; jeder `Store` (Cursor, Snapshot, Index, Deps, Marking)
  ist Ableitung — jederzeit aus dem Stream wiederherstellbar.
- **Durabel ↔ flüchtig.** Events und DLQ sind durabel; Signale, Weckrufe, Quittungen dürfen verloren gehen.

### Kompositionsregeln

1. **Verhalten = immer ein Actor.** Kein Verhalten → ein Stream oder Store (passiv).
2. **Wahrheit = immer ein Stream. Ableitung = immer ein Store.** „DLQ", „Snapshot", „Cursor", „Deps",
   „Marking", „Versionsindex" sind **keine Bauteile** — sie sind *benannte Instanzen* von Stream bzw. Store.
3. **Ein durabler Effekt wird nur via Co-Commit mit seiner Position geschrieben.** Flüchtige Ableitungen
   (Redis) werden *nach* dem Co-Commit gesetzt, nie vorher.

---

## 4. Die Kanten: gezogen vs. geworfen

Es gibt nur **zwei Zustellwege**, und der Unterschied ist die halbe Garantie-Geschichte.

- **Gezogen (pull).** Genau **eine** Message-Art wird gezogen: das **Event**. Ein Konsument *holt* Events aus
  dem `Stream`, ab seinem Lesezeichen. Nichts „liefert" sie ihm.
- **Geworfen / gefragt (push).** Alles andere — Command, Signal, Weckruf, Query, Trigger — wird
  *hingeschickt* (werfen = fire-and-forget; fragen = request/response).

Daraus fällt die zentrale Asymmetrie:

> **Nur das Gezogene ist Wahrheit. Alles Geworfene ist flüchtig** — entweder eine *Bitte* um einen durablen
> Schreibvorgang (Command), ein *Zeiger/Weckruf* auf die Wahrheit (Signal), oder eine *Frage* (Query).
> Verliert man etwas Geworfenes, ist die Wahrheit trotzdem im Stream — man holt sie beim nächsten Zug.

**Kardinalität der Kanten:**
- **Command-Kante 1:1** — ein Command trifft *genau ein* Aggregat (beim Bauen erzwungen, CQRS010).
- **Event-Kante 1:N** — Fan-out an mehrere Projektionen/Reaktionen.
- **Join N:1** — der Count-Join eines Prozesses.

```
  [Client] ═ask═▶ (Aggregat) ─write─▶ [Log: Stream] ─pull─▶ (Konsument) ─write─▶ [Read-Model: Store]
                      │                                          │
                   throw(Signal)                             throw(Command, 1:1)
                      ▼                                          ▼
                 (Broker) ─throw─▶ (Konsument weckt)      (anderes Aggregat) …

   ( ) = Actor (Verhalten, gekapselt)     [ ] = Stream/Store (passiv, KEIN Actor)
```

### Zwei Graph-Ebenen

- **Statischer Typ-Graph** (Compile-Zeit): Kinds und ihre Flüsse. *Hier* sind die Kanten **eindeutig und
  statisch bestimmt** — das *ist* „reines Typ-Routing". Diesen Graphen bauen die **Generatoren**.
- **Dynamischer Instanz-Graph** (Laufzeit): jeder Knoten fächert in viele Actor-Instanzen auf, je Identität.
  Die Kante zeigt auf eine **Adresse** (`An` im Umschlag) — nie auf Payload-Inhalt. Geroutet wird nach
  **Typ** (welche Kind) + **Adresse** (welche Instanz), niemals durch Hineinschauen in die Nachricht.

---

## 5. Messages & ihre Garantien

### Der Katalog

| Message | Weg | Dauerhaftigkeit | Zustellung | Ordnung | Idempotenz am Empfänger |
|---|---|---|---|---|---|
| **Event** | gezogen | **durabel** | Pull ab Cursor | geordnet (Stream-Version) | eingebaut (OCC/Version) |
| **Ablehnung** (`ITransientEvent`) | geworfen | flüchtig | at-most-once (Antwort) | — | nein |
| **Command — Client (OCC)** | gefragt | flüchtig *(Wirkung wird durabel)* | fragen | Mailbox des Aggregats | via OCC-Version |
| **Command — emittiert** | geworfen | flüchtig | at-least-once (+Poll/Retry) | Mailbox des Aggregats | **nötig** (det. ID + Inbox) |
| **Signal** | geworfen | flüchtig | at-most-once → geheilt vom Poll | ungeordnet | eingebaut (Doppelweckung folgenlos) |
| **Weckruf / Poll-Wake** | geworfen | flüchtig | at-least-once (Poll wiederholt) | Mailbox des Konsumenten | eingebaut (Zug idempotent) |
| **Query / Antwort** | gefragt | flüchtig | fragen | — | nein (kein Effekt) |
| **Trigger** (extern) | geworfen | flüchtig | at-most-once | Mailbox der Pipeline | (wird zu idempotentem Command) |

### Die Garantie steckt NICHT in der Message

Der wichtigste Fallstrick: **Man kann die Garantie nicht an der Message ablesen.** Eine Message ist inerte
Daten. Drei Ebenen, streng getrennt:

- **Am *Typ* (Compile-Zeit, via Marker):** nur die **Kind** (Command/Event/Signal/Query) und **durabel ↔
  flüchtig** (z. B. `IEvent` → Log, außer `ITransientEvent`). Das liest der Generator am *Typ*, nicht an der
  Instanz.
- **An der *Kante* + am *Store*:** die eigentlichen Garantien. Die **Zustellung** ist Eigenschaft der Kante
  (ihres Profils, Kap. 7); die **Wirkungs-Garantie** ist Eigenschaft des **Stores am Schreib-Knoten**
  (Kap. 5, unten). Beweis, dass sie *nicht* in der Message steckt: „Command" hat im Katalog **zwei Zeilen** —
  gleicher Typ, zwei Garantien.
- **An der *Instanz* (Laufzeit):** **nichts.** Man verzweigt nie auf Message-Daten (das wäre
  Runtime-Reflection, Verstoß gegen Invariante 4/3).

### Die vier orthogonalen Achsen

```
   ① ROUTING          ② ZUSTELLUNG            ③ WIRKUNGS-GARANTIE        ④ FRESHNESS/DEPS
   wer bekommt was     kommt es an?            genau einmal wirksam?      ist meine Sicht frisch?
   ── Typ ──           ── immer ──             ── der STORE ──            ── abgeleiteter Hinweis ──
   Compile-Zeit        at-least-once           am Commit-Punkt            ctx.Track → Index;
   (Generator)         (Signal + Poll)         (Co-Commit? → exactly-once)  Entscheidung beim Client
```

**Achse ③ ist der springende Punkt.** `IProjectionTracker` sagt es selbst: *„Das Framework stellt diesen
Nahtpunkt bereit … es garantiert NICHT, dass Fortschritt und Effekt gemeinsam durabel werden — das ist
allein Sache des Stores."* Legt der Store *Effekt + Marke* in **eine** Transaktion → exactly-once. Getrennt
(oder ohne Nahtpunkt, dann liest er ab 0) → at-least-once, Handler muss idempotent sein.

Und dasselbe Prinzip bei **Prozessen**: die Wirkungs-Garantie sitzt *nicht* am Prozess-Manager (der emittiert
at-least-once), sondern am **Empfänger-Aggregat**, dessen Store die Domänen-Events **zusammen mit der
Inbox-Marke** (`KommandoVerarbeitet`) in einer Append-Transaktion co-committet. Gleicher Satz, anderer
Commit-Punkt.

> **Merksatz:** ① Routing = Typ (trägt keine Garantie). ② Zustellung = immer at-least-once. ③
> Wirkungs-Garantie = der Store am Commit-Punkt (Co-Commit → exactly-once). ④ Freshness = abgeleiteter
> Hinweis. Das Framework stellt überall nur die **Möglichkeit** bereit; *ob* exactly-once, entscheidet der
> **Store** — nie die Message, nie das Routing, nie der Transport.

---

## 6. Die Kern-API sitzt IM Zug (Kern ↔ Maschine)

Die entwicklergeschriebene API — **Decider, Applier, Handle, Reader, IProzessDefinition** — sind **keine
Bauteile.** Sie sind der **reine Kern**, den der Actor in seinem Zug aufruft.

```
   MASCHINE (Framework):  Actor · Stream · Store · Co-Commit
        ruft im Zug auf ▼
   KERN (dein Code, REIN): Decider · Applier · Handle · Reader · IProzessDefinition
```

Regel (Invariante 5): **Der Kern sieht nur einfache Werte** — `State`, `Command`, `Event` — und **nie** ein
Bauteil. Deshalb *kann* die API unangetastet bleiben, wenn wir die Maschine darunter vereinheitlichen.

| Kern-API | Art | Rastet ein in | Stelle des Zuges | Sieht nur |
|---|---|---|---|---|
| **Decider** | reine Funktion `State × Command → Events \| Ablehnung` | Schreiber | Entscheiden | State, Command |
| **Applier** | reine Funktion `State × Event → State` | Schreiber + Rehydrierung/Replay | Falten | State, Event |
| **Handle** (Projektion) | reine Funktion `Event → Read-Model-Writes` | Projektion | pro Event | Event, `writer` |
| **Handle** (Reaktion/Pipeline) | reine Funktion `Event → yields Commands` | Consumer | pro Event | Event |
| **Reader** | reine Funktion `Query → Antwort` | Query-Pfad | Lesen | Read-Model, `ctx.Track` |
| **IProzessDefinition** | reine **Deklaration** der Regeln | generischer Prozess-Actor | einmal gelesen, dann interpretiert | Event-/Command-*Typen* |

Abbildung auf die Basis-Items: Ein **Decider** liefert `Events` (durabel → Co-Commit in den Stream) *oder*
eine `Ablehnung` (flüchtig → Antwort-Message, nie gespeichert). Der Decider *entscheidet nur*; die Maschine
ordnet ein.

---

## 7. Kanten sind komponierbar (Decorator-Profile)

Eine Kante ist *ein Send von A nach B*. Die Querschnitts-Verhalten legen sich als **Decorator-Stack** darum —
exakt das gRPC-Interceptor-Muster:

```
   Sender:    StampDeterministicId ▷ Serialize(falls remote) ▷ BoundedTimeout ▷ Retry ▷ DeadLetterOnExhaust ▷ RawSend
   Empfänger: Dedup(Inbox) ▷ CoCommit ▷ Handler
```

Die Empfänger-Seite (`Dedup ▷ CoCommit`) ist genau der Ort, an dem die **Store-Garantie (Achse ③)** die Kante
trifft.

### Komposition ist eingeschränkt — Invarianten

„Einfach Retry dazu" geht **nicht** frei. Die Decorators haben Abhängigkeiten:

> **Invariante: `Retry` ⟹ `StampDeterministicId` (Sender) + `Dedup` (Empfänger).**
> Retry allein = W1 (Doppel-Wirkung). Die drei sind ein untrennbares Paket über *beide* Seiten der Kante.

Weitere Pflicht-Decorators:
- `Serialize` auf **jeder** Kante über die Maschinengrenze (sonst K1 — stiller Drop).
- `BoundedTimeout` auf **jeder** await-Kante (sonst W2 — Hang).
- `DeadLetterOnExhaust` statt stillem Verwerfen auf **jeder** werfen-Kante.

### Zwei Wege, eine Kanten-Eigenschaft zu realisieren

- **Als Wrapper um den Send** (flüchtig, pro-Call): Timeout, Serialize, sofortiger Retry, Id-Stempel,
  Dead-Letter-on-exhaust.
- **Als eigener Knoten im Graph** (durabel, systemweit): der **Poller** *ist* der Retry für pull-Kanten; der
  **Broker** *ist* die Fan-out-Schicht; die **DLQ** *ist* die durable Ausfall-Senke.

> Flüchtig/sofort → Wrapper. Durabel/systemweit → eigener Actor/Store-Knoten.

### Was *nicht* Decorator ist

**In-Order/Single-Writer** = die Mailbox = der Actor selbst. **Co-Commit** = eine Fähigkeit des Stores. Beide
wohnen im **Knoten**, nicht in der Kante.

### Komponiert wird pro Kanten-*Sorte* (Profil), nicht pro Call

```
throw-command = StampId ▷ Serialize? ▷ Bounded ▷ Retry ▷ DeadLetter  ‖  Dedup ▷ CoCommit ▷ Handler
ask-query     = Serialize? ▷ Bounded                                   ‖  Handler (read-only)
throw-signal  = Serialize? ▷ Bounded   (Retry = Poller-KNOTEN)         ‖  idempotenter Zug
pull          = kein Send; Read ab Cursor; „Retry" = Poller-Knoten + lücken-bewusster Cursor
```

Das **Emit-Primitiv** ist nichts anderes als die *einmal korrekt komponierte* `throw-command`-Kante. Der
`Serialize`-Decorator (K1) ist die Schicht, die man allen Profilen für den cross-node-Fall hinzufügt. Genau
die gRPC-Analogie: die Pipeline wird *einmal* zusammengesteckt, alle Calls fließen hindurch.

---

## 8. Wie die Elemente aus den Bauteilen komponiert sind

Für jedes Element: **Bauteile** (reine Teileliste) und **Zug** (Verhalten) — strikt getrennt.

| Element | Actor? | Bauteile (Teileliste) |
|---|---|---|
| **Schreiber** | ja ⟨Aggregat⟩ | `Stream[event-log]` · `Store[snapshot]` · Co-Commit(Events+Inbox-Marke im Stream) |
| **Projektion** | ja ⟨Stream⟩ | `Stream[event-log]`(Read) · `Store[read-model,tx]` · `Store[cursor,tx]` · `Store[deps,flüchtig]` · Co-Commit(read-model+cursor) |
| **Reaktion** | ja ⟨Stream⟩ | `Stream[event-log]`(Read) · `Store[cursor,tx]` |
| **Prozess** | ja ⟨Korrelation⟩ | `Stream[entscheidungs-log]` · `Store[marking-cache,flüchtig]` · N×`Stream[ziel-log]`(Read) · `Stream[dlq]`(Klärung) |
| **Poller** | ja ⟨global⟩ | `Stream.ReadGlobal` · `Store[poll-cursor,tx]` |
| **Broker** | ja ⟨Shard⟩ | `Store[abo-register,flüchtig]` |
| **DLQ** | nein | `Stream[dlq]` (geschrieben als Nebenwirkung eines gescheiterten Zuges) |
| **Deps / Versionsindex** | nein | `Store[…,flüchtig]` (geschrieben *nach* dem Co-Commit; gelesen bei Query) |

In allen Teilelisten kommen **nur** Actor, Stream, Store, Co-Commit vor. Das ist der Beweis der Sparsamkeit.

---

## 9. Wo jede Garantie wohnt (ein Zuhause pro Garantie)

| Garantie | Zuhause |
|---|---|
| Single-Writer / In-Order | **Actor-Knoten** (Mailbox) |
| Ordnung der Wahrheit | **Stream** (Version) |
| Zustellung (at-least-once) | **werfen-Kante** (+ Poller-Knoten als Retry) |
| Exactly-once-Wirkung | **Store am Schreib-Knoten** (Co-Commit / Inbox) |
| Doppel-Schutz (Idempotenz) | **Empfänger-Kante** `Dedup` + **Store** (Co-Commit der Marke) |
| „Nicht verlieren" (Heilung) | **Poller-Knoten** (Wiederholung) |
| Freshness/Deps | **abgeleiteter Store** (Hinweis) + **Client** (entscheidet) |

Refactoring heißt: *stelle sicher, dass jede Garantie an ihrem einen Zuhause sitzt — und nirgends fehlt.*

---

## 10. Wie das Modell dem Refactoring hilft & Brüche verhindert

### Der Hebel

1. **Es sagt, *was* verschmolzen werden muss:** gleiche Kanten-Sorte → *eine* Implementierung (die vier
   Emit-Pfade sind dieselbe `throw-command`-Kante → das Emit-Primitiv).
2. **Es sagt, *wo* jede Garantie wohnt** (Kap. 9) → einmal erzwingen, nicht verstreut.
3. **Es macht Brüche zu prüfbaren Graph-Invarianten** (der Typ-Graph ist statisch → Build-Fehler).
4. **Es sagt, was *nicht* verschmolzen werden darf:** zwei Knotensorten (Log bleibt passiv); der Prozess ist
   ein Korrelations-Multistrom-Knoten (andere Quell-Topologie).
5. **Orthogonalität** (Routing ⟂ Zustellung ⟂ Store-Garantie) → die Refactoring-Schritte stören sich nicht.

### Bruch-Prävention — drei Klassen

| Bruch | Graph-Ort | Prävention | Klasse |
|---|---|---|---|
| **W1** Doppel-Wirkung | werfen-Command-Kante | *ein* Emit-Profil: `Id`+`Dedup` untrennbar; kein zweiter Emit-Weg | **strukturell** |
| **W2** Hang | await-Kante | jeder Send über das Profil, immer `Bounded`; `None` existiert nicht | **strukturell** |
| **K1** stiller cross-node-Verlust | knoten-übergreifende Kante | *ein* generierter `Serialize`-Decorator; Fehlschlag **laut** (DLQ) | **strukturell** + Build-Check |
| **S14** nicht-gemapptes Command | Command-Kante (Prozess) | Build-Check: jede Regel-Command-Kante ⊆ `CommandToAggregate` | **Build-Fehler** |
| Command-Mehrdeutigkeit | Command-Kante | Build-Check: genau ein Aggregat (CQRS010, existiert) | **Build-Fehler** |
| Prozess-Zyklus | Prozess-Teilgraph | Azyklizitäts-Guard (existiert) | **Build-Fehler** |
| **G4** stille at-least-once-Projektion | Schreib-Knoten | Build-/DI-Check: Append-Projektion **muss** Co-Commit-Tracker haben | **Build-Fehler** |
| **S15** Noop → Falsch-Erfolg | Join-Kante | Join feuert **nur** auf `WirkungDa` (echtes Event-Token), nie auf bloße Auflösung | Maschinen-Invariante |
| **S13** / Fan-out-Kollision | Instanz-Kante | Emit-Identität trägt volle Kanten-Koordinaten; Join zählt **realisierte** Effekte | Maschinen-Invariante |
| **P10** Straggler-Skip | pull-Kante / Cursor | lücken-bewusster Cursor: HWM nur über bestätigt-vollständiges Präfix | Maschinen-Invariante |
| **H2** Deps auf Uncommittetes | abgeleiteter Knoten | Deps/Index **nach** dem Co-Commit schreiben | Maschinen-Invariante |

Die gefährlichsten (stillen) Brüche — W1, W2, K1 — werden **strukturell** wegdesignt (nur ein Weg). Die
routing-artigen werden **Build-Fehler** (der Typ-Graph ist statisch bekannt). Der Rest lebt als *eine*
Maschinen-Invariante — einmal richtig statt viermal riskant.

### Wie wir *beweisen*, dass nichts bricht

Garantien sind Knoten-/Kanten-Eigenschaften → man testet sie **pro Kante/Knoten in-memory** (Fake-Cluster),
nicht im langsamen Integrationstest:
- W1 → „werfen-Command-Kante mit verlorener Quittung → Empfänger wirkt genau einmal".
- S15 → „Noop-Token schaltet Join **nicht** scharf".
- P10 → „HWM rückt nicht über eine Lücke".
Jeder Bruch = ein isolierter Beweis an seinem Graph-Ort.

---

## 11. Multi-Node: was es braucht & die Single-Node-Disziplinen

Im Graph-Modell ändert Multi-Node **nichts an den Knoten, Garantien oder Stores**. Es ändert nur eines:
**manche Kanten überqueren eine Maschinengrenze.** Weil alle Wahrheit über *ein* Substrat (Marten) + Redis
läuft, ist der **Speicher schon geteilt** — kein per-Node-Zustand. Die schwere Korrektheit (OCC, Idempotenz,
Co-Commit, Poll, Rehydrate-on-Move) ist log-basiert und ändert sich zwischen 1 und N Nodes nicht.

> **Multi-Node = die kreuzenden Kanten serialisierbar machen + Proto.Cluster fürs Placement + Korrektheit
> aus dem Log (nicht aus dem Node).** Du baust nicht zwei Systeme, sondern das System + einen
> Transport-Schalter an den kreuzenden Kanten.

### Das eine echte Muss

Der `Serialize`-Decorator (Kap. 7) für *alle* internen Kanten-Messages (`CommandEnvelope`/`CommandResult`,
`EventEnvelope`/`SignalEnvelope`, `Publish`/`Ack`, `Subscribe`, `Wake`/`WakeAck`, `ProzessWake`/
`MeldeFehlschlag`, `Activate`, Broker-Status). Der harte Teil ist die **polymorphe Nutzlast**
(`Payload : ICommand`/`IEvent`) → ein **generierter Poly-Serializer, gekeyt über die Typ-Registry**
(reflexionsfrei), registriert am `WithRemote`-Punkt ([CqrsServiceExtension.cs:339](../Infrastructure/Extensions/CqrsServiceExtension.cs#L339)).
Plus: **Boot-Check** (jeder interne Typ hat einen Serializer → sonst bricht der Start) und **laute Fehler**
(die `_ = RequestAsync`-Stellen fangen + dead-lettern, statt stumm zu droppen).

### Was schon cross-node trägt (nicht neu bauen)

- **Actor-Umzug ist folgenlos** — Rehydrate aus dem Log (`AggregateRehydrator` + Snapshot).
- **Single-Writer clusterweit** = Proto.Cluster Single-Activation, **mit Marten-OCC als Split-Brain-Backstop**
  (der Log ist der letzte Schiedsrichter im Handover-Fenster).
- **Poll wird cross-node, sobald der Serializer steht** (liest die geteilte DB, wirft serialisierbare `Wake`).
- **DB-Uhr** statt Node-Uhr für Zeit-Entscheidungen; **alle Stores geteilt** (ein Marten, ein Redis).

### Die Single-Node-Disziplinen (billig vorab, teuer als Retrofit)

Die Migration fällt **nur dann** klein aus, wenn der Single-Node-Bau keine Single-Node-Annahmen einbäckt.
Diese Regeln von Tag eins einhalten:

- [ ] **Adressierung nur per `ClusterIdentity` (Kind, Schlüssel)** — nie eine lokale PID/Referenz.
- [ ] **Alle Wahrheit im geteilten Substrat** (ein Marten) — kein node-lokaler durabler Zustand.
- [ ] **Interne Messages sind reine Daten** — *kein* `Func`/Delegate/`Task`/Closure im **Payload**. (Ein Seam
      als *injizierte Actor-Abhängigkeit* ist okay; ein Seam *in der Nachricht* ist die Landmine — vgl.
      `DetachedProzessSend`/`DetachedEmit`.)
- [ ] **Entscheidungen per DB-Uhr**, nie per Node-`DateTime.UtcNow`.
- [ ] **Korrektheit aus dem Log** (OCC/Idempotenz/Poll), nie aus „ist eh derselbe Prozess".
- [ ] **Serializer-Round-trip-Test früh mitlaufen lassen** — Single-Node führt *nie* eine Serialisierung aus,
      also sind Serialisierungslücken single-node **unsichtbar**. Der Round-trip-Test (jeder interne Typ
      serialisiert+deserialisiert = gleich) erzwingt die „reine Daten"-Invariante kontinuierlich statt am Ende.

### Code klein, Verifikation ist der Rest-Aufwand

Was single-node **nie ausgeführt** wird, ist die Move-/Rebalance-Semantik (Umzüge, Split-Brain-Fenster,
Shard-Bewegungen). Der Code dafür ist *designt*, aber erst mit ≥2 Nodes *bewiesen*.

> **Die Code-Migration ist klein und additiv. Der eigentliche Rest-Aufwand ist die *Verifikation* (das
> Zwei-Node-Gate)** — dort verstecken sich die Überraschungen, nicht im Code.

### Vorgehen

1. **Kompletter Redesign single-node** (Emit-Primitiv, vereinheitlichte Konsumenten-Maschine, ein Substrat,
   Bugfixes, Features) — ~90 % des Werts, vollständig single-node beweisbar. Dabei die Disziplinen oben halten.
2. **Serializer registrieren + Boot-Check + laute Fehler** (wenig Code, generiert).
3. **Broker-Abo-Entscheidung** (durable vs. Poll-heilt).
4. **Zwei-Node-Gate** (der echte Verifikations-Aufwand).

Eigener Meilenstein, orthogonal zur Konsumenten-Vereinheitlichung.

## 12. Grenzen des Modells (was es NICHT behauptet)

- **Nicht jeder Knoten ist ein Actor.** Zustands-Knoten (Log, Read-Models, Indexe) bleiben passiv — mit
  Absicht.
- **Die Uniformität ist nicht flach.** Der Prozess-Knoten hat eine andere *Quell-Topologie*
  (Korrelations-Multistrom) und Kompensation als prozess-exklusiven Zweig — es ist *eine Maschinen-Klasse mit
  Achsen*, nicht *ein Kind mit einem Rückgabetyp* (siehe Zielbild §12).
- **Das Prozess-Marking ist ein Cache, keine zweite Wahrheit.** Die Wahrheit bleibt der Log
  (`prozess-marking-cursor-konzept.md`).
- **K1 (Serialisierung) ist orthogonal** — ein eigener Meilenstein, kein Nebenprodukt der Vereinheitlichung.

---

## 13. Kurzfassung — die Merksätze

- **Das System ist ein typisierter Graph.** Knoten verarbeiten Nachrichten, Kanten *sind* Nachrichten.
- **Zwei Knotensorten:** aktive (Actors, gekapselt) und passive (Stream/Store). Nur Verhalten wird zum Actor.
- **Fünf Bauteile:** Message, Actor, Stream, Store, Co-Commit. „DLQ/Snapshot/Cursor/Deps/Marking" sind
  Instanzen, keine Bauteile.
- **Wahrheit wird gezogen** (Event aus dem Stream, geordnet); **alles andere wird geworfen** und ist flüchtig.
- **Garantie steckt nie in der Message.** Kind + durabel/flüchtig = Typ (Compile-Zeit); Zustellung = Kante;
  Wirkungs-Garantie = Store am Commit-Punkt; die Instanz wird nie inspiziert.
- **Der Kern (Decider/Applier/Handle/Reader/Prozess) sitzt rein im Zug** — die Maschine wird vereinheitlicht,
  der Kern nicht angetastet.
- **Kanten sind komponierbar** (Decorator-Profile pro Kanten-Sorte), aber mit Invarianten (`Retry` ⟹
  `Id`+`Dedup`). Schwere Kanten-Eigenschaften werden eigene Knoten (Poller/Broker/DLQ).
- **Jede Garantie hat ein Zuhause** — Refactoring = sicherstellen, dass es besetzt ist und nirgends fehlt.
- **Brüche verhindern:** die stillen strukturell (ein Weg), die routing-artigen als Build-Fehler, den Rest
  als eine Maschinen-Invariante — jeder per Kanten-Test in-memory bewiesen.
- **Multi-Node = derselbe Graph, nur der `Serialize`-Decorator an den kreuzenden Kanten.** Speicher ist schon
  geteilt, Korrektheit log-basiert. Erst komplett single-node bauen — aber mit den Disziplinen (reine
  Message-Daten, kein node-lokaler Zustand, DB-Uhr, Serializer-Round-trip-Test früh) — dann fällt die
  Migration klein aus; der echte Rest-Aufwand ist die *Verifikation* (Zwei-Node-Gate), nicht der Code.
