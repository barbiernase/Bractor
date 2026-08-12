# Backend-Neubau: Die eine, wartbare Maschine

> **Status:** Entwicklungs-Leitdokument für den starken Backend-Umbau. Self-contained — es leitet die
> Architektur von Grund auf her, erklärt vorab die vollständige Design-Philosophie, begründet jeden
> Schritt und endet mit einem klaren Entwicklungsplan.
>
> **Companion-Dokumente:** `docs/gedankenmodell-system-als-graph.md` (das Denkmodell),
> `docs/zielbild-vereinheitlichte-konsumenten-maschine.md` (das Zielbild v2),
> `docs/prozess-marking-cursor-konzept.md`, `docs/backend-audit-befunde.md`. Dieses Dokument fasst
> deren tragende Erkenntnisse zusammen und schärft sie an drei Stellen (die drei Neubau-Auflagen, §6).
>
> **Der Umbau in einem Satz:** Wir bauen die *Backend-Maschine* neu — nicht den Stack. Alle vier Säulen
> (**Proto.Actor, Source-Generatoren, Marten/PostgreSQL, Redis**), **gRPC**, die **Client-Struktur** und
> die **entwicklergeschriebene API** bleiben. Was neu entsteht, ist die *innere* Implementierung: aus einer
> organisch gewachsenen Feature-Sammlung wird **eine einheitliche, wartbare Maschine** mit *einer*
> Ableitungsquelle pro Graph-Kante.

---

## 0. Zweck, Geltung und Nicht-Ziele

### 0.1 Zweck

Das Backend ist funktional reich, aber an einer Stelle *auseinander*gewachsen: **ein Primitiv wurde an
vier Stellen unterschiedlich sicher nachgebaut**, und **dieselbe Graph-Relation wird von mehreren
Generatoren in unterschiedlicher Treue abgeleitet**. Das Ergebnis ist korrekt genug für den Happy-Path,
aber schwer zu warten und multi-node-untauglich. Dieser Umbau macht aus der Sammlung **eine Maschine**:

- *ein* Emit-Primitiv statt vier,
- *eine* Konsumenten-Maschinenklasse statt Projektion/Reaktion/Pipeline/Prozess-Sonderpfade,
- *eine* signaturgetriebene Ableitungsquelle pro Kante des Typ-Graphen,
- *ein* transaktionales Substrat (Marten) für alle Wahrheit und alle Co-Commits.

### 0.2 Woran wir festhalten (bewusst, unverhandelbar)

| Element | Rolle bleibt | Begründung |
|---|---|---|
| **Entwickler-API** (`IDecider`/`IApplier`/`Handle`/`IReader`/`IProzessDefinition`/`IPipelineHandler`) | unverändert | Der reine Kern sieht nie ein Bauteil (Inv. 5); die Maschine darunter darf sich ändern, die API nicht. |
| **Source-Generatoren** | bleiben das Rückgrat | Compile-Zeit-Typ-Routing statt Runtime-Reflection (Inv. 3/4). Der Umbau *kanonisiert* sie, wirft sie nicht weg. |
| **Marten / PostgreSQL** | Event-Store = einzige Wahrheit + alle Co-Commits | Ein transaktionales Substrat trägt Effekt+Marke atomar. |
| **Redis** | abgeleiteter, nicht-autoritativer Versions-/Deps-Index | Schneller Hinweis, nie Entscheidungsträger. |
| **Proto.Actor / Cluster** | Placement, Single-Activation, In-Order-Mailbox | Wir erben diese Garantien, bauen sie nicht nach. |
| **gRPC / `domain.proto`** | externe Client-Grenze | Domänen-Typen und Client-Vertrag bleiben. |
| **Client-Struktur** (`GrpcProxy`, Versioning/Deps, Client-Generatoren) | unverändert | Zwei externe Verträge bleiben: Client-OCC + Freshness/Deps. |

### 0.3 Nicht-Ziele

- **Kein Stack-Wechsel.** Keine andere DB, kein anderes Actor-Framework, kein anderer Transport.
- **Kein API-Bruch für den Entwickler.** Decider/Handle/Prozess schreibt man morgen genauso. *Additiv,
  abwärtskompatibel:* optionale Identitäts-Attribute (`[AggregatName]`/`[ProzessName]`, §4.6) treten
  neben die Marker, und die bisher *implizite* Konvention „pro Namespace genau ein Aggregat" entfällt
  (sie war nie Teil des Vertrags, nur eine Generator-Annahme).
- **Kein Client-Umbau.** Der Client spricht weiter ausschließlich gRPC.
- **Kein Backwards-Compat-Zwang *intern*.** Der Umbau *darf* interne Altlasten (Sentinel, tote Pfade,
  konkurrierende Generatoren) ersatzlos löschen — das ist erwünscht.

### 0.4 „Neubau" heißt hier: zu Ende denken, nicht neu erfinden

Der Titel sagt „Neubau", die Sache ist **Konsolidierung**: das Rückgrat (Log=Wahrheit, Pull-vom-Cursor,
co-committete Marke, typisierter Dispatch) ist *richtig und bewiesen* — es wird **nicht** umgeworfen,
sondern *zu Ende gedacht* (so das Zielbild v2: „Die Architektur ist gesund und muss zu Ende gedacht,
nicht neu gedacht werden"). „Neubau" meint die *innere Maschine* der vier Emit-Pfade, der mehrfach
abgeleiteten Kanten und der Transport-Ebene — nicht das Fundament.

---

## 1. Die Ausgangslage: die Diagnose

Fünf konkrete, code-belegte Symptome tragen den ganzen Umbau. Sie sind keine Bugs im engeren Sinn,
sondern *strukturelle* Uneinheitlichkeiten — genau das, was ein Neubau bereinigt.

### 1.1 „Command emittieren" existiert vierfach mit vier Sicherheitsniveaus

Dasselbe „schicke ein Command an ein Aggregat, exactly-once-wirksam" ist an vier Stellen verschieden
implementiert:

- **Aggregat-Dispatcher** (Client-Command, OCC) — korrekt.
- **`HandlerOutputRouter`** (Reaktion) — *richtig*: deterministische `ReaktionsId`, `AnyVersion`,
  bounded 3-s-Token, Empfänger-Dedup (`Infrastructure/PubSub/HandlerOutputRouter.cs:86,97,108`).
- **`ProzessManager` / `DetachedProzessSend`** — richtig, fire-and-forget mit deterministischem Vorgang.
- **`PipelineActorBase`** — **falsch**: pro Retry ein neuer Envelope mit zufälliger `CommandId`
  (`Infrastructure/Pipeline/PipelineActorBase.cs:237`), OCC-Pfad ohne Empfänger-Dedup (**W1** —
  Doppelanwendung bei verlorener/verspäteter Quittung), und `RequestAsync(..., CancellationToken.None)`
  (:251 — **W2**, unbounded Hang).

> **Merksatz:** Idempotenz ist heute nur erzwungen, wo der Sender *zufällig* richtig sendet. Ein Primitiv
> macht die Garantie *strukturell*.

### 1.2 Der `AnyVersion=-1`-Sentinel kodiert die Behandlung IN die Nachricht

`CommandEnvelope.ExpectedVersion` trägt einen Sentinel (`-1`, `Abstractions/CommandEnvelope.cs:15,19`),
auf den der Schreiber drei Verhaltensweisen aufhängt: OCC-Skip
(`Infrastructure/Aggregate/ActorSystem/AggregateActorBase.cs:192`), Inbox-Dedup (:213) und Co-Commit
der Marke (:300). Das verletzt den Grundsatz „die Garantie steckt nie in der Message" — die Behandlung
*reist* in einem magischen Feldwert mit.

### 1.3 Dieselbe Graph-Relation wird 4× in 3 Treuegraden abgeleitet

Die Relation „welche Events produziert ein Command/Aggregat" existiert im Generat vierfach:

| Ableitung | Treue | Quelle |
|---|---|---|
| `GeneratedCommandRouting.CommandToEvents` | **präzise** (Decide-OneOf) | `Infrastructure.SourceGeneration/CommandAggregateMapGenerator.cs:95,115-135` |
| `SubscriberDispatch` ProducedTypes | **präzise** (Handle-OneOf) | `Domain.SourceGeneration/SubscriberDispatchGenerator.cs:134-140` |
| `AggregateHandlerGenerator` | **nur Bool** „produziert IEvent" | `Domain.SourceGeneration/AggregateHandlerGenerator.cs:181,197` |
| `GeneratedEventCommandMapping` | **namespace-grob** | `Infrastructure.SourceGeneration/EventCommandMappingGenerator.cs:64-95` |

Kein Single Source of Truth. Für einen technischen Graphen ist das der Kern-Defekt.

### 1.4 Drei Kanten laufen über Konvention statt Signatur

- **`Event→Command`** per Namespace-Gruppierung (`EventCommandMappingGenerator.cs:64-95`) — erzwingt
  „pro Namespace genau ein Aggregat".
- **`Event→Signal`** per Namens-Präfix `StateChangeVia{Event}` (`SignalTypeGenerator.cs:82-86`).
- **Knoten-Identität** = einfacher Typname (`TState.Name`, `CommandAggregateMapGenerator.cs:77`; Prozess =
  Klassenname-String, `ProzessRegelnGenerator.cs:72`) → **Kollisionsrisiko** bei gleichnamigen Typen in
  verschiedenen Namespaces.

Die *Dispatch*-Kanten dagegen sind sauber typ-getrieben (reflexionsfreie `switch`-Emission aus
`Handle`/`Decide`/`Apply`-Parametertypen) — das ist die tragende Stärke, die wir behalten.

### 1.5 Die interne Transport-Ebene ist gar nicht im Typ-Graphen

Die Typ-Registry kennt nur Domänen-Kategorien (`IEvent/ICommand/IQuery/IQueryResponse/IPipelineTrigger/
IStateChangeSignal`, `Infrastructure.SourceGeneration/TypeRegistryGenerator.cs:63-71`). Die internen
Cluster-Nachrichten (`CommandEnvelope`, `EventEnvelope`, `SignalEnvelope`, `Wake`, `Publish`, `Ack`,
`ProzessWake`) implementieren keines davon → sie sind **keine Knoten im Graphen**. Der `DtoMapperGenerator`
mappt nur externe Domänen-Typen. Folge: der für Multi-Node nötige generierte Poly-Serializer **kann heute
nicht keyen** — die zu serialisierenden Knoten fehlen. Zusätzlich adressiert der Broker Subscriber per
**lokaler PID** (`Infrastructure/PubSub/BrokerSubscription.cs:42`, Zustellung `BrokerShardActor.cs:105,128`)
statt per `ClusterIdentity`, und hält sein Abo-Register in-memory (`BrokerShardActor.cs:21`).

> **Fazit der Diagnose:** Das Rückgrat (Log=Wahrheit, Pull-vom-Cursor, co-committete Marke, typisierter
> Dispatch) ist *richtig und bewiesen*. Die Uneinheitlichkeit sitzt in vier Emit-Pfaden, einem
> Message-Sentinel, mehrfach abgeleiteten Kanten, drei konventions-getriebenen Kanten und einer
> Transport-Ebene außerhalb des Graphen. Genau das räumt der Neubau auf.

---

## 2. Die Design-Philosophie (vorab, vollständig)

Alles Folgende leitet sich aus diesem Abschnitt ab. Wer nur diesen Abschnitt liest, versteht das *Warum*.

### 2.1 Die sechs Invarianten (sie bleiben, sie sind das Gesetz)

1. **Die Wahrheit ist der Log.** Ordnung/Vollständigkeit/Wiederholbarkeit kommen NUR aus dem
   Event-Store-Read.
2. **Das Signal ist nur ein Weckruf:** trägt nur `(StreamId, Version)`, darf verloren, doppelt, ungeordnet
   sein.
3. **Routing über Typen — nie ein handgebauter Identitäts-String.**
4. **Keine Runtime-Reflection.** Alles Dispatchende wird zur Compile-Zeit generiert.
5. **Der Fachcode bleibt rein.** Cursor, Signal, Ordnung, Exactly-once, Sharding, Prozess-Maschinerie
   tauchen im Entwickler-Code nie auf.
6. **Persistent genau dann, wenn ein durabler Konsument abhängt.** Verlierbares (Tick, UI-Feedback,
   Datei-Trigger) bleibt auf dem schnellen Kanal.

### 2.2 Das Domänen-Graph-Modell (aus `gedankenmodell-system-als-graph.md`)

**Das System ist ein typisierter Graph.** Knoten verarbeiten Nachrichten, Kanten *sind* Nachrichten.

- **Zwei Knotensorten:** *aktive* (Actors, gekapseltes Verhalten: Schreiber, Konsument, Poller, Broker)
  und *passive* (Stream/Store — Log, Read-Models, Indexe; bewusst KEIN Actor).
- **Fünf Bauteile:** ① Message (Daten), ② Actor (Verhalten), ③ Stream (Wahrheit, append-only, OCC),
  ④ Store (Ableitung, `Get/Set/Delete`), ⑤ Co-Commit (Stream.Append **und** Store.Set in *einer*
  Transaktion). „DLQ/Snapshot/Cursor/Deps/Marking" sind *Instanzen* von Stream/Store, keine neuen Bauteile.
- **Gezogen vs. geworfen:** Genau **eine** Message-Art wird gezogen (das **Event**, ab Cursor aus dem
  Stream); alles andere (Command, Signal, Weckruf, Query, Trigger) wird geworfen/gefragt und ist flüchtig.
  *Nur das Gezogene ist Wahrheit.*
- **Die Garantie steckt NIE in der Message.** ① Routing = Typ (Compile-Zeit, trägt keine Garantie).
  ② Zustellung = immer at-least-once (Signal + Poll). ③ Wirkungs-Garantie = der **Store am Commit-Punkt**
  (Co-Commit → exactly-once). ④ Freshness = abgeleiteter Hinweis. Vier orthogonale Achsen.

### 2.3 Der technische Graph (die Implementierung als Graph — die Schärfung dieses Umbaus)

Der Domänen-Graph ist etabliert. Der Neubau fügt eine zweite Lesart hinzu: **auch die Implementierung
ist ein Graph**, den die Generatoren zur Compile-Zeit aus den Typen ableiten. Für ihn gelten drei
zusätzliche Eigenschaften, die heute nur teilweise erfüllt sind — sie werden zu **Neubau-Invarianten**
(§6):

- **(TG-1) Eine Ableitungsquelle pro Kante, signaturgetrieben.** Jede Graph-Kante (Command→Aggregat,
  Command→Event, Event→Handler, Event→Signal, Query→Reader) wird aus *einer* kanonischen Quelle
  abgeleitet — den Methoden-Signaturen (`Decide`/`Apply`/`Handle` + deren `OneOf`-Rückgaben). Keine
  Namespace-Gruppierung, keine Namens-Präfixe, keine parallelen Ableitungen derselben Relation.
- **(TG-2) Die Transport-Ebene ist erstklassiger Graph-Knoten.** Interne Nachrichten (`CommandEnvelope`,
  `Wake`, `SignalEnvelope`, …) stehen in der Typ-Registry. Dann *ist* die Serialisierung (K1) eine
  Graph-Eigenschaft, und der Boot-Check „jeder interne Typ hat einen Serializer" ist ein
  Graph-Vollständigkeits-Check.
- **(TG-3) Knoten-Identität ist total und kollisionsfrei.** Kind-/Prozess-/Signal-Identitäten aus FQN
  oder explizitem Attribut, nie aus einfachem Typnamen.

### 2.4 Die zwei Primitive (aus dem Zielbild v2)

> Es gibt genau **zwei** Primitive: einen *Schreiber in den Log* (**P1**) und einen *durablen Konsumenten*,
> der ab Cursor liest, faltet, emittiert und seine Marke fortschreibt (**P2**).

- **P1 — Der Schreiber** (`AggregateActorBase`): der einzige Ort, an dem ein Effekt durabel wird.
  Command → Decider → OCC-Append → Apply → optionale Inbox-Marke → Signal.
- **P2 — Der durable Konsument:** liest ab Cursor, faltet, produziert Ausgaben, schreibt seine Marke
  fort, emittiert dann Commands (idempotent) und Signale (best-effort). Jede Weckung — Signal *oder* Poll
  — ist ein Schritt.

**Die v2-Ehrlichkeit:** „eine Maschine" ist eine Maschinen-*Klasse* mit **zwei orthogonalen Achsen**, nicht
*ein Kind mit einem Rückgabetyp*:

- **Achse A — Quell-Topologie:** *Ein-Strom* (Projektion, Reaktion, Pipeline-Event) liest *einen* Stream ab
  `int`-Cursor; *Korrelations-Multistrom* (Prozess) liest die *N* teilnehmenden Streams einer Korrelation.
- **Achse B — Effekt-Klasse:** *Replaybar* (Projektion: Read-Model, `Reset`/Rebuild erlaubt) vs.
  *Emittierend* (Reaktion/Pipeline/Prozess: Effekt ist das emittierte Command — **nie** blind replayen,
  „Command-yieldende Handler werden NICHT blind replayt").

### 2.5 Wo jede Garantie wohnt (ein Zuhause pro Garantie)

| Garantie | Zuhause |
|---|---|
| Single-Writer / In-Order | **Actor-Knoten** (Mailbox) |
| Ordnung der Wahrheit | **Stream** (Version) |
| Zustellung (at-least-once) | **werfen-Kante** (+ Poller-Knoten als Retry) |
| Exactly-once-Wirkung | **Store am Schreib-Knoten** (Co-Commit / Inbox) |
| Doppel-Schutz (Idempotenz) | **Empfänger** `Dedup` (det. CommandId) + **Store** (Co-Commit der Marke) |
| „Nicht verlieren" (Heilung) | **Poller-Knoten** (Wiederholung) |
| Freshness/Deps | **abgeleiteter Store (Redis)** (Hinweis) + **Client** (entscheidet) |

**Refactoring-Definition:** sicherstellen, dass jede Garantie an ihrem *einen* Zuhause sitzt — und nirgends
fehlt oder doppelt liegt.

---

## 3. Die Herleitung des Umbaus (warum diese Schritte, in dieser Reihenfolge)

Aus Diagnose (§1) + Philosophie (§2) fällt der Umbau **nicht als strikte Kette, sondern als DAG**: ein
gemeinsames Fundament, danach mehrere *unabhängige* Ströme. Die folgende Kette ist die *logische*
Herleitung; welche Schritte parallel laufen dürfen, zeigt der Abhängigkeitsgraph in §7 und im Fahrplan.

1. **Verträge zuerst.** Solange der `AnyVersion`-Sentinel die Behandlung in die Message kodiert und die
   Emit-Wege vierfach sind, kann keine Vereinheitlichung greifen. Also: *zwei explizite Schreiber-Eingänge*
   + *ein Emit-Primitiv-Vertrag* zuerst — sie sind die Grundlage, auf der alles Weitere sicher wird.
2. **Der Graph vor der Maschine.** Die Maschine routet über den generierten Typ-Graphen. Ist der Graph
   mehrdeutig (vier Ableitungen, drei Konventions-Kanten), erbt jede Maschine die Mehrdeutigkeit. Also:
   *kanonischer Kanten-Graph* (TG-1/TG-3) früh.
3. **Der Schreiber, dann das Emit.** P1 ist der Empfänger jedes Emits. Erst P1 (zwei Eingänge, Inbox) sauber,
   dann das Emit-Primitiv, das ihn *immer idempotent + bounded* anspricht (W1/W2 strukturell weg).
4. **Die Konsumenten-Maschine auf sauberer Emit-Grundlage.** Erst wenn Emit *ein* Baustein ist, lohnt die
   Verschmelzung der Konsumenten in *eine* Maschinen-Klasse (zwei Achsen).
5. **Der Prozess als Sonderfall der Maschine.** Korrelations-Multistrom + Emittierend — auf derselben Klasse,
   aber mit eigener Quell-Topologie; Marking aus dem Log (Wahrheit), Cursor als Cache, Terminal-Fix per
   `CorrelationId`-Poll-Routing.
6. **Die Pipeline ehrlich zerlegen.** Event-Pfad = Reaktion (in die Maschine falten); Trigger-Ingress =
   dünner Push-Adapter (kein Log-Cursor). Danach existiert *kein* gepushter Event-Konsument mehr.
7. **Transport in den Graphen (K1).** Erst jetzt, orthogonal: interne Nachrichten als Graph-Knoten,
   generierter Poly-Serializer, Boot-Check. Das macht Multi-Node zum *Decorator an kreuzenden Kanten*
   statt zur Graph-Erweiterung.
8. **Das Multi-Node-Tor.** Der eigentliche Rest-Aufwand ist *Verifikation*, nicht Code.

**Herleitungs-Schritt → Plan-Phase (§7).** Die acht Herleitungs-Schritte bündeln sich in neun Phasen:
Schritt 1 → **P0** (Verträge) + der Sentinel-Teil von **P2**; Schritt 2 → **P1** (Kanten-Graph);
Schritt 3 → **P2** (Schreiber) *und* **P3** (Emit); Schritt 4 → **P4** (Konsum-Maschine); Schritt 5 →
**P5** (Prozess); Schritt 6 → **P6** (Pipeline); Schritt 7 → **P7** (Transport/K1); Schritt 8 → **P8**
(Multi-Node). **Wichtig:** P1 ∥ P2 (beide nur von P0), und P5/P6/Feature-Strom/P7 hängen alle nur an P3
bzw. P1 — nicht linear aufeinander (§7-Graph).

---

## 4. Das Ziel im Detail (Bauteil für Bauteil)

Jedes Bauteil: **Was / Warum / Wie / Was bleibt gleich.**

### 4.1 Zwei explizite Schreiber-Eingänge (der Sentinel stirbt)

- **Was:** `AggregateActorBase` bekommt zwei klar getrennte Eingänge statt eines
  überladenen `CommandEnvelope.ExpectedVersion`:
  `HandleClientCommand(expectedVersion)` (OCC, externer Vertrag) und
  `HandleEmittedCommand(commandId)` (idempotent, interne Emitter).
- **Warum:** Die Garantie darf nicht in der Message reisen (§2.2). Zwei Eingänge machen die
  Behandlung am *Typ des Eingangs* explizit, nicht an einem magischen `-1`.
- **Wie:** Der OCC-Pfad bleibt byte-genau (Compare-and-Swap gegen Version N). Der Emit-Pfad
  co-committet die `KommandoVerarbeitet`-Marke mit den Domänen-Events in *einer* Marten-Transaktion
  (`AggregateActorBase.cs:300-310` → `MartenEventStore.cs:78,106`, ein `SaveChangesAsync`) und
  dedupliziert über die deterministische CommandId.
- **Bleibt gleich:** Decider/Applier, die Inbox-Semantik, die Version-pro-Event-Arithmetik.

### 4.2 Das Emit-Primitiv (`ICommandEmitter`) — ersetzt vier Pfade

- **Was:** Der *eine* Baustein „schicke ein Command an ein Aggregat, exactly-once-wirksam". Ersetzt den
  internen Teil des Dispatchers, `PipelineActorBase`-Send, `ProzessManager`/`DetachedProzessSend`,
  `HandlerOutputRouter`-Send.
- **Warum (die tragende Herleitung — Kanten als Decorator-Profil):** Eine werfen-Command-Kante ist ein
  Decorator-Stack: `StampDeterministicId ▷ (Serialize) ▷ BoundedTimeout ▷ Retry ▷ DeadLetterOnExhaust`
  (Sender) ‖ `Dedup(Inbox) ▷ CoCommit ▷ Handler` (Empfänger). Diese Decorators sind **nicht frei
  komponierbar** — es gilt die Invariante:
  > **`Retry` ⟹ `StampDeterministicId` (Sender) + `Dedup` (Empfänger).**
  > `Retry` *allein* ist **W1** (Doppel-Wirkung): ein Retry mit *neuer* CommandId erzeugt beim Empfänger
  > einen zweiten Effekt. Die drei sind ein untrennbares Paket über *beide* Seiten der Kante. Und jede
  > await-Kante braucht `BoundedTimeout`, sonst **W2** (Hang) — `CancellationToken.None` darf es nicht
  > geben. Genau das verletzt die heutige Pipeline (§1.1).

  Ein *einziger* Emit-Weg macht dieses Paket strukturell unausweichlich: er ist immer idempotent
  (deterministische CommandId aus Kausalität) + bounded Token + at-least-once auf dem Draht. Es gibt
  keinen zweiten Weg, der die Invariante brechen könnte.
- **Wie:**
  ```csharp
  namespace Abstractions;
  public interface ICommandEmitter
  {
      // Immer idempotent (det. CommandId aus Kausalität), bounded Token, at-least-once →
      // exactly-once-wirksam via Empfänger-Inbox. KEIN Versions-Argument.
      Task EmitAsync(ICommand cmd, EmitKausalität k, CancellationToken ct);
  }
  public readonly record struct EmitKausalität(Guid Korrelation, Guid Ursache, string Diskriminator);
  ```
  **Entwurfsaxiom:** *interne Emitter behaupten NIE eine Version.* OCC ist ausschließlich der externe
  Client-Vertrag. Ein echtes read-modify-write gegen eine bestimmte Version ist Sache des *Deciders*,
  nicht des Emits.
- **Bleibt gleich:** Der `HandlerOutputRouter`-*Fall* (IEvent → re-publish, ICommand → emit) bleibt als
  dünne Projektion auf das Primitiv.

### 4.3 Die Konsumenten-Maschinenklasse (zwei Achsen, zwei Marken)

- **Was:** Die parameterisierte Lese-Falt-Emit-Schleife (heute `ProjectionAdapter`) wird *die* Maschine.
  Sie variiert entlang der zwei Achsen aus §2.4 — nicht flach parametrisiert, sondern mit getrennten
  Kinds/Marken je Achsen-Kombination.
- **Warum:** Projektion, Reaktion, Pipeline-Event und Prozess sind alle P2. Eine Maschine statt vier
  Sonderpfaden = die Wartbarkeit.
- **Wie — zwei Marken-Interfaces (der Compile-Zeit-Schnitt):**
  ```csharp
  // Replaybare Konsumenten (Projektion): Effekt + Cursor co-committet, Reset erlaubt.
  public interface IReplaybarerTracker   // = heutiges IProjectionTracker inkl. Reset*
  { Task<int> LastProcessedVersionAsync(...); Task MarkProcessedAsync(...); Task ResetAsync(...); }

  // Emittierende Konsumenten (Reaktion/Pipeline/Prozess): NUR Cursor, KEIN Reset.
  public interface IEmittentenCursor
  { Task<long> LadeAsync(string partition, CancellationToken ct);
    Task SchreibeAsync(string partition, long bis, CancellationToken ct); }
  ```
  `Reset*` wird **nie** an einen Emittenten gegeben — der Compile-Zeit-Schnitt (zwei Interfaces)
  verhindert blindes Replayen von geld-bewegenden Emittenten.
- **Bleibt gleich:** Die Marker-API (`ISubscriber`+`Handle`, `IPipelineHandler`+`Handle`,
  `IProzessDefinition`). Der Generator bindet sie an die Maschine und wählt Kind + Marken-Interface nach
  der Achsen-Kombination.

| Ausprägung | Achse A | Achse B | Fold | Emit | Marke |
|---|---|---|---|---|---|
| Projektion | Ein-Strom | Replaybar | Read-Model | Signale | `IReplaybarerTracker` — Reset ✓ |
| Reaktion | Ein-Strom | Emittierend | — | Commands | `IEmittentenCursor` — Reset ✗ |
| Pipeline (Event) | Ein-Strom | Emittierend | — | Commands | `IEmittentenCursor` — Reset ✗ |
| Pipeline (Trigger) | Push-Ingress | Emittierend | — | Commands | *kein Log-Cursor* (§4.5) |
| Prozess | Korrelations-Multistrom | Emittierend (+Entscheidungs-Log) | Petri-Marking | Commands | Manager-Log + Cache-Cursor — Reset ✗ |

### 4.4 Der Prozess: Marking aus dem Log, Cursor als Cache, Terminal per Korrelation

- **Was:** Der Prozess ist P2 mit Korrelations-Multistrom-Quelle. Er bleibt das Petri-Netz
  (`IProzessDefinition`/`Regel`/`SammelBedingung`) — nur die *Ausführung* wird an die Maschine angepasst.

  **Vokabular (einmal definiert):**
  - **Petri-Netz-Modell:** Events = Tokens, Commands = Transitionen. Eine `Regel` (Transition) hat eine
    `Bedingung` (Konjunktion von Event-Typen) und feuert bei Erfüllung die `Sende`-Commands; `SammelBedingung`
    ist der **Count-Join** (dynamische Breite: „feuere erst, wenn ALLE N erwarteten Ergebnisse da sind").
  - **Vorgang:** die *deterministische* Id eines gefeuerten Schritts, abgeleitet aus der Kausalität
    (`ProzessId.FürTransition(...)`). Sie **ist** die CommandId des emittierten Commands → das Ziel-Aggregat
    dedupliziert darüber (Framework-Inbox). Zwei Weckungen derselben Ursache → gleicher Vorgang → Noop.
  - **Marking:** welche Transitionen schon gefeuert/quittiert sind — **nie in einem Feld gehalten**, sondern
    bei jeder Weckung aus den Ziel-Streams gefaltet (Ergebnis↔Transition über `CausationId == Vorgang`).
  - **`WeckeSelbst`:** die fire-and-forget-Selbstweckung des Managers nach erfolgreichem Send (heutiger
    Terminal-Notnagel). **`ProzessOffenIndex`:** ein durabler Marten-Index offener Prozesse, den ein
    periodischer Backstop-Loop weckt. Beide werden nach dem §4.4(c)-Tor *retirable*.
- **Warum & Wie (drei getrennte Dinge):**
  - **(a) Wahrheit = der Log.** Das Marking wird weiter *aus den Ziel-Streams gefaltet* (heute
    `ProzessManager.cs:180-238`, liest ab 0); die durablen Entscheidungen
    (`ProzessGestartet`/`SchrittGescheitert`/`ProzessBeendet`) sind das Manager-Log. Die Zwei-Achsen-Marke
    `ErgebnisDa` (aufgelöst) vs. `WirkungDa` (wirksam) **bleibt** — sie verhindert, dass ein Noop einen
    Downstream-Join scharf schaltet.
  - **(b) Performance = Cursor als *Cache*.** `docs/prozess-marking-cursor-konzept.md`: Cursor + Tail statt
    Voll-Read ab 0 (O(N²)→O(N) bei breiten/akkumulierenden Prozessen). **Best-effort, außerhalb der
    Entscheidungs-Transaktion** — verloren/inkonsistent → Voll-Fold heilt. Optimierung, kein Commit-Punkt.
    **Die eine Falle (zwingend beachten):** die persistierte Marking-Darstellung muss **verdichtet** sein,
    nicht roh. „Alle Tokens persistieren" bringt O(N²) zurück (ein Fan-out-Marking mit N Tokens wäre O(N)
    groß und würde bei jeder Weckung neu geladen). Also: Count-Join als **Zähler + Done-Set** (welche der N
    erwarteten Vorgänge quittiert sind), Fan-out als **Done-Set der fertigen Zweige** — nicht als volle
    Event-Payloads. Nur so bleibt das Update inkrementell (O(Tail)).
  - **(c) Terminal-Fix = `CorrelationId`-Poll-Routing.** Heute filtert der Poll geänderte Streams per
    `relevanteTypen` (`ProzessManagerWiring.cs:154`) → das Ergebnis-Event der letzten Transition weckt
    niemanden; nur `WeckeSelbst` (fire-and-forget) findet das Terminal. Fix: der Poll lässt den Typ-Filter
    fallen und routet *jedes* geänderte teilnehmende Stream-Event **per `CorrelationId`-Metadatum** an die
    richtige Korrelation. Dann weckt das Terminal-Event den Manager regulär. `WeckeSelbst` und der durable
    `ProzessOffenIndex`-Backstop werden retirable, *sobald* die `ProzessBackstopE2ETests` das nachweisen.
- **Bleibt gleich:** Das Prozessmodell, die Kompensation, der Count-Join, die deterministischen `ProzessId`/
  `Vorgang`, der `KorrelationsRouter` (im Prinzip).

### 4.5 Die Pipeline ehrlich zerlegt

- **Event-Pfad (Kanal 2):** Event rein → Command raus *ist* bereits eine Reaktion. → **in die
  Konsumenten-Maschine falten** (Ein-Strom/Emittierend). Bekommt Inbox-Idempotenz + bounded Token über das
  Emit-Primitiv — **W1/W2 damit auch hier weg**, und der letzte *gepushte* Event-Konsument verschwindet
  (heute `PipelineActorBase.cs:118-128` via Broker-Abo).
- **Trigger-Ingress (Kanal 1):** externe Trigger (FileWatcher/Timer/Webhook, `IPipelineTrigger`) sind keine
  Log-Events — kein Cursor, kein Fold, keine Marke. Bleibt ein **dünner Push-Adapter**, der über das
  Primitiv einen Command emittiert. `IPipelineSelfMessage`/`ScheduleSelf` bleiben lokales Detail.
- **Bleibt gleich:** Die drei Domänen-Pipelines (`BenchmarkPipeline`, `FileWatchPipeline`,
  `ImageProcessingPipeline`) und ihre Handler-API — nur der *Transport* unter `Handle` ändert sich.

### 4.6 Der kanonische Kanten-Graph (TG-1/TG-3) — der Generator-Umbau

- **Was:** *Eine* signaturgetriebene Ableitung pro Kante; die konkurrierenden/konventions-getriebenen
  Generatoren fallen weg.
- **Wie:**
  - **Command→Aggregat & Command→Event:** *nur* aus `GeneratedCommandRouting` (Decide-Signatur +
    OneOf-Rückgabe, `CommandAggregateMapGenerator.cs:95,115-135`). `GeneratedEventCommandMapping`
    (namespace-grob) wird gelöscht; Konsumenten (`MessageTypeMapping`, Proto) auf die präzise Map
    umgestellt.
  - **`AggregateHandlerGenerator`** liest künftig die vollen OneOf-Typargumente (heute nur ein Bool,
    `:181,197`) — damit auch der Schreiber die präzise Command→Event-Kante kennt.
  - **Event→Signal:** aus dem Event-Typ selbst (Marker/Attribut), nicht aus dem Namens-Präfix.
  - **Knoten-Identität:** FQN oder explizites `[AggregatName("…")]`/`[ProzessName("…")]`-Attribut statt
    `TState.Name` — kollisionsfrei; die „pro Namespace ein Aggregat"-Konvention entfällt.
- **Bleibt gleich:** Die reflexionsfreien *Dispatch*-Switches (`Handle`/`Decide`/`Apply`-Parametertypen) —
  sie sind schon korrekt und werden 1:1 übernommen. Die Build-Diagnostiken (CQRS001/002/003/010) bleiben und
  werden ergänzt.

### 4.7 Die Transport-Ebene als Graph-Knoten (TG-2 / K1)

- **Was:** Interne Nachrichten (`CommandEnvelope`, `EventEnvelope`, `SignalEnvelope`, `Wake`/`WakeAck`,
  `Publish`/`Ack`, `Subscribe`, `ProzessWake`/`MeldeFehlschlag`, `Activate`) kommen in die Typ-Registry;
  ein **generierter Poly-Serializer** keyt über sie.
- **Warum:** Single-Node führt *nie* eine Serialisierung aus → Lücken sind unsichtbar. Erst mit den
  Knoten im Graphen wird K1 eine prüfbare Graph-Eigenschaft und Multi-Node ein Decorator statt Neubau.
- **Wie:** Registrierung am `WithRemote`-Punkt (`Infrastructure/Extensions/CqrsServiceExtension.cs:335-340`,
  heute *ohne* Serializer); **Boot-Check** (jeder interne Typ hat einen Serializer → sonst bricht der
  Start); **laute Fehler** (die `_ = RequestAsync`-Stellen fangen + dead-lettern statt still zu droppen);
  **Round-trip-Test** früh mitlaufen lassen. Broker: Subscriber per `ClusterIdentity` statt lokaler PID,
  Abo-Entscheidung (durable vs. Poll-heilt) explizit treffen.
- **Bleibt gleich:** Der externe `domain.proto`/gRPC-Weg (`DtoMapperGenerator`) — K1 betrifft nur die
  *interne* Ebene.

### 4.8 Was am Rand unangetastet bleibt

| Rand | Impact | Was ändert sich |
|---|---|---|
| **Client (Commands/Queries/Events)** | keiner | Client behält OCC; Projektionen liefern weiter Read-Model + Deps |
| **gRPC / `domain.proto`** | keiner | Domänen-Typen unverändert; K1 ist intern |
| **Redis** | keiner | bleibt Versions-/Deps-Index; Cursor liegt in Postgres |
| **Marten/Postgres** | eingegrenzt | `ProzessOffen` entfällt nach dem §4.4-Tor; Emittenten-Cursor als Doc; `es`/`rm`/`dlq` unverändert |
| **Proto.Actor** | Vereinfachung | eine Maschinen-Klasse, weiter mehrere Kinds; Placement gleich |
| **Client-Struktur / Generatoren** | keiner | `GrpcProxy`, Versioning/Deps, Client-Generatoren unverändert |

**Rand-Risiko (aus Zielbild §8, bewusst behalten):** Der Client trackt Versionen auch aus Query-Deps.
Prozesse liefern heute keine Client-Queries — *falls* das Prozess-Marking je abfragbar wird, muss die
Deps-Berechnung dafür mitgezogen werden. Solange der Prozess server-intern bleibt, ist der Impact keiner.

---

## 5. Die Neubau-Invarianten (prüfbare Graph-Eigenschaften)

Zusätzlich zu den sechs Invarianten (§2.1) klopfen wir für den technischen Graphen fest:

- **(TG-1)** Jede Graph-Kante hat *eine* signaturgetriebene Ableitungsquelle. *Prüfbar:* kein Generator
  leitet eine Relation aus Namespace-Gruppierung oder Namens-Präfix ab; keine zwei Generatoren erzeugen
  dieselbe Relation.
- **(TG-2)** Die interne Transport-Ebene ist in der Typ-Registry; K1 ist ein Boot-Vollständigkeits-Check.
  *Prüfbar:* Round-trip-Test über *jeden* internen Typ ist grün.
- **(TG-3)** Knoten-Identitäten sind total und kollisionsfrei (FQN/Attribut, nie einfacher Name).
  *Prüfbar:* zwei gleichnamige Typen in verschiedenen Namespaces brechen den Build nicht die Laufzeit.
- **(EM-1)** Es gibt *genau einen* Emit-Weg (`ICommandEmitter`), immer idempotent + bounded.
  *Prüfbar:* Grep findet keine zweite `RequestAsync<CommandResult>`-Stelle außerhalb des Primitivs; kein
  `CancellationToken.None` auf einer Command-Kante.
- **(EM-2)** Kein Message-Sentinel kodiert Behandlung. *Prüfbar:* `AnyVersion` existiert nicht mehr; der
  Schreiber hat zwei getrennte Eingänge.
- **(GA-1)** Wirkungs-Garantie wohnt am Store am Commit-Punkt (Co-Commit = *eine* Marten-Transaktion) —
  an **zwei** Commit-Punkten: der **Aggregat-Inbox** (Phase 2) und der **append-artigen Projektion**
  (Phase 4). *Prüfbar:* die Inbox co-committet Marke+Events in einer Transaktion; und jede append-artige
  Projektion hat einen Co-Commit-Tracker (DI-/Boot-Check), sonst Build-Fehler.

---

## 6. Beweisstrategie (wie wir zeigen, dass nichts bricht)

Garantien sind Knoten-/Kanten-Eigenschaften → sie werden **pro Kante/Knoten in-memory (Fake-Cluster)**
bewiesen, nicht im langsamen, log-versteckenden Integrationstest (`memory/hang-diagnose-in-memory.md`).

- **Ebene 1 (Prüfstand, in-memory):** store-freie Logik + Fake-Cluster. W1 („werfen-Command-Kante mit
  verlorener Quittung → Empfänger wirkt genau einmal"), W2 („nie zurückkehrender Send blockiert `WakeAsync`
  nicht"), S15 („Noop-Token schaltet Join nicht scharf"), Prozess-Äquivalenz (Cursor-Fold == Voll-Fold).
- **Ebene 2 (Integration, echtes Marten/Consul/Redis, *sequentiell*):** Store-Semantik (Co-Commit =
  exactly-once; Poll-Straggler-Karenz; OCC-Konflikt). *Integrationstests immer sequentiell*
  (`Infrastructure.Integration.Tests/xunit.runner.json`).
- **Ebene 3 (Last-Harness):** Durchsatz + Cluster-Diagnose mit App-Logs (`LoadHarness/ --log debug`).
- **Multi-Node-Tor:** Zwei-Member-Test — ein Adapter je Stream, Ordnung erhalten, Poll heilt cross-node.

Jeder Bruch aus §1 bekommt *einen* isolierten Beweis an seinem Graph-Ort.

---

## 7. Der Entwicklungsplan

Der Plan ist ein **DAG, kein Zug**: ein gemeinsames Fundament (P0–P3), danach *vier unabhängige Ströme*,
die alle nur auf dem Fundament (Emit, P3 bzw. Graph, P1) aufsetzen — nicht aufeinander. Jede Phase hat ein
*Tor* (messbar) und ist so geschnitten, dass das System nach ihr grün ist. Der kompakte Fahrplan mit
Tasks/Toren steht in `docs/backend-neubau-fahrplan.md`.

```
  P0 Verträge ─┬─► P1 Kanten-Graph ───────────────────────────────► P7 Transport/K1 ─► P8 Multi-Node
               └─► P2 Schreiber+Inbox ─► P3 Emit-Primitiv ─┬─► P4 Konsum-Maschine ─► P6 Pipeline
                                                            ├─► P5 Prozess-festklopfen  (braucht P4 NICHT)
                                                            └─► Feature-Strom            (orthogonal)
```

*Lesart:* P1 ∥ P2 (beide nur von P0). Nach P3 verzweigt der Plan in **P4→P6** (die Vereinheitlichung),
**P5** (Prozess festklopfen — hängt an P3, **nicht** an P4), den **Feature-Strom** und **P7→P8**
(Multi-Node, orthogonal, hängt nur an P1). Die Nummerierung ist keine strikte Ausführungsreihenfolge.

> **Reihenfolge-Prinzip (aus Zielbild v2 §10/§13, hier bewusst übernommen):** *erst* Emit + Bugfixes
> (Fundament), *dann* die hochwertigen, risikoarmen Fixes (Prozess-Terminal-Fix, Features) — die große
> Konsumenten-Vereinheitlichung (P4) ist **zuletzt und de-scoped, keine Voraussetzung für P5/Features.**
> P5 und der Feature-Strom sind bewusst **nicht** hinter P4 gehängt: der `CorrelationId`-Terminal-Fix
> braucht die vereinheitlichte Maschine technisch nicht und liefert früh den größten Korrektheitsgewinn.

### Phase 0 — Verträge & Graph-Fundament festklopfen
*Ziel:* die Grundlage, auf der alles Weitere sicher wird — ohne Verhaltensänderung.
- `ICommandEmitter` + `EmitKausalität` als Vertrag (`Abstractions`).
- Zwei Schreiber-Eingänge *entwerfen* (`HandleClientCommand`/`HandleEmittedCommand`); `AnyVersion` als zu
  entfernenden Sentinel markieren (noch nicht löschen — Sender migrieren erst in P3).
- Marken-Interfaces `IReplaybarerTracker` / `IEmittentenCursor` (`Abstractions`).
- **Tor:** kompiliert; Verträge stehen; bestehende Tests unverändert grün.

### Phase 1 — Kanonischer Kanten-Graph (Generatoren) · TG-1, TG-3
*Ziel:* eine signaturgetriebene Ableitung pro Kante; kollisionsfreie Identitäten. *(Hängt nur an P0;
parallel zu P2.)* **Risikohinweis:** Big-Bang-Generator-Umbau — bei Bedarf pro Kante einzeln schneiden
(erst die grobe Map ersetzen, dann Event→Signal, dann Identität).
- `GeneratedEventCommandMapping` (namespace-grob) löschen; alle Konsumenten (`MessageTypeMapping`, Proto)
  auf `GeneratedCommandRouting` (präzise) umstellen.
- `AggregateHandlerGenerator` liest volle OneOf-Typargumente.
- `Event→Signal` aus Typ/Marker statt Namens-Präfix.
- Knoten-Identität auf FQN/Attribut umstellen; „pro Namespace ein Aggregat"-Konvention entfernen.
- **Tor:** jede Kante aus Signatur; kein Generator nutzt Namespace/Namens-Präfix; Build-Checks
  (CQRS001/002/003/010) grün; ein bewusster Namens-Kollisionstest bricht *nicht* die Laufzeit.

### Phase 2 — Der Schreiber (P1) + Inbox · GA-1 (Aggregat-Commit-Punkt)
*Ziel:* zwei explizite Eingänge, Co-Commit in *einer* Marten-Transaktion. **Der Sentinel bleibt hier noch
bestehen** (seine Löschung ist an die Sender-Migration in P3 gekoppelt).
- `HandleClientCommand`/`HandleEmittedCommand` umsetzen; beide Eingänge *neben* dem alten `AnyVersion`-Pfad
  bereitstellen (koexistieren, bis P3 alle Sender migriert hat).
- Inbox-Co-Commit (`KommandoVerarbeitet` + Events, ein `SaveChangesAsync`) am Emit-Eingang.
- Live-Apply `is not IProzessIntern`-Filter an die Rehydration angleichen (Symmetrie).
- **Tor:** OCC-Pfad byte-genau unverändert (Regressionsvergleich); Emit-Pfad exactly-once am Empfänger
  (Fake-Cluster, verlorene Quittung).

### Phase 3 — Das Emit-Primitiv · EM-1, EM-2 — W1/W2 strukturell weg
*Ziel:* die vier Emit-Pfade auf einen ziehen **und** den Sentinel entfernen.
- `ICommandEmitter`-Implementierung (det. CommandId, bounded Token, at-least-once).
- **Alle** Sender migrieren: Dispatcher-intern, `HandlerOutputRouter`, `ProzessManager`/`DetachedProzessSend`,
  `PipelineActorBase` → über das Primitiv auf `HandleEmittedCommand`.
- Erst **danach** `AnyVersion` löschen (jetzt sendet kein Emitter mehr mit dem Sentinel) und die
  `PipelineActorBase`-Retry-Schleife (zufällige CommandId + `CancellationToken.None`) entfernen.
- **Tor:** Grep findet keinen zweiten Emit-Weg, kein `CancellationToken.None` auf Command-Kanten,
  kein `AnyVersion` mehr; **Pipeline dedupliziert (W1) und hängt nicht (W2)** — Fake-Cluster-Test mit
  verlorener Quittung. *(Das Pipeline-Tor ist transitorisch — der Pipeline-Event-Pfad wird in P6 ohnehin in
  Reaktionen gefaltet; die Garantie reist dann mit dem Primitiv in die gefaltete Reaktion.)*

### Phase 4 — Die Konsumenten-Maschinenklasse (zwei Achsen) · GA-1 (Projektions-Commit-Punkt)
*Ziel:* Projektion/Reaktion/Pipeline-Event auf *einer* Klasse. *(Hängt an P3; die große, de-scopte
Vereinheitlichung — nicht Voraussetzung für P5/Features.)* **Risikohinweis:** re-homet mehrere Konsumenten
gleichzeitig; bei Bedarf Ausprägung für Ausprägung migrieren (erst Projektion, dann Reaktion).
- Die Lese-Falt-Emit-Schleife parametrisieren (Quell-Topologie × Effekt-Klasse); Kind + Marken-Interface
  je Kombination generieren.
- **GA-1-Build-/DI-Check umsetzen:** jede append-artige Projektion *muss* einen Co-Commit-`IReplaybarerTracker`
  haben — sonst Build-Fehler (verhindert stille at-least-once-Projektionen).
- **Tor:** alle Ein-Strom-Konsumenten laufen auf derselben Maschinen-Klasse; `Reset` nur bei
  `IReplaybarerTracker` verfügbar (Compile-Zeit); der GA-1-Check bricht eine bewusst tracker-lose
  Append-Projektion.

### Phase 5 — Prozess festklopfen (Terminal-Fix + Marking-Cache)
*Ziel:* Marking aus dem Log, Cursor als Cache, Terminal ohne `WeckeSelbst`. **Hängt an P3, NICHT an P4** —
der hochwertigste, risikoärmste Korrektheitsgewinn, deshalb früh und unabhängig von der Vereinheitlichung.
(Die spätere Migration des Prozess-Managers *auf* die Maschinenklasse aus P4 ist eine optionale Folge-
Konsolidierung, kein Teil dieses Tors.)
- (a) `CorrelationId`-Poll-Routing (Typ-Filter `relevanteTypen` fallen lassen). *Tor:* Terminal erkannt ohne
  `WeckeSelbst`; `ProzessBackstopE2ETests` grün → `WeckeSelbst`/`ProzessOffenIndex` retirable.
- (b) Marking-Cursor als **Cache** (kein Co-Commit; best-effort, außerhalb der Entscheidungs-Transaktion).
  **Verdichtete Darstellung zwingend** (Count-Join = Zähler+Done-Set, Fan-out = Done-Set der Zweige — nicht
  alle Tokens roh, sonst kehrt O(N²) zurück, §4.4(b)). *Tor:* Sagas grün, O(N²)→O(N) (Read-Zähler),
  Voll-Fold heilt Cache-Verlust/`RegelHash`-Mismatch.

### Phase 6 — Pipeline ehrlich zerlegen
*Ziel:* kein gepushter Event-Konsument mehr. *(Hängt an P4.)*
- Event-Pfad in die Konsumenten-Maschine falten (Reaktion); Trigger-Ingress als dünner Push-Adapter über
  das Primitiv.
- **Tor:** kein `BrokerSubscription`-Event-Abo außerhalb des Signal-Receivers; die drei Domänen-Pipelines
  laufen unverändert (Handler-API).

### Feature-Strom (orthogonal, hängt nur an P3) — aus Zielbild §11 wieder aufgenommen
*Ziel:* die im Zielbild benannten Feature-Lücken auf der sauberen Emit-Grundlage — **kein** Teil der
Vereinheitlichung, jederzeit parallel baubar.
- **DLQ-Replay** (Ops-/Read-Pfad auf `dlq`).
- **Timer/Webhook-Trigger** + `ITriggerRegistration` verdrahten (Trigger-Ingress bleibt Push, §4.5).
- **Projektions-Rebuild-Runner** (Vertrag + `Reset` existieren; leicht nach P4 — *eine* Rebuild-Schleife).
- **Deadlines/Timeouts** (fachlich; nach stabilem Prozessmodell — Timer-Token/Zeit-Event).
- **Prozess-Verkettung** (Modell ok; braucht Test/Beispiel).
- **Monitoring** (Metrics/Tracing/HealthChecks/Prozess-Sicht; profitiert von der uniformen Maschine).

### Bugfixes (entkoppelt, jederzeit)
- `BrokerIdentity.GetShardIndex`: `(hash & 0x7FFFFFFF) % ShardCount` statt `Math.Abs(hash)`
  (`BrokerIdentity.cs:63`, Overflow bei `int.MinValue`).
- Fan-out-Diskriminator: RegelIndex + Instanz-Index in die `Vorgang`-Id (latente Kollision).
- Gemischtes Decider-Ergebnis (Effekt + Ablehnung): heute Fail-fast (`AggregateActorBase.cs:257`) — im
  Neubau als Decider-Contract festschreiben.

### Phase 7 — Transport in den Graphen · TG-2 / K1 (orthogonal, hängt nur an P1)
*Ziel:* interne Nachrichten als Graph-Knoten; generierter Poly-Serializer.
- Interne Typen in die Registry; Poly-Serializer generieren; am `WithRemote`-Punkt registrieren; Boot-Check;
  laute Fehler; Broker auf `ClusterIdentity`-Adressierung + Abo-Entscheidung (durable vs. Poll-heilt).
- **Tor:** Round-trip-Test über *jeden* internen Typ grün; Boot bricht bei fehlendem Serializer.

### Phase 8 — Das Multi-Node-Tor (Verifikation)
*Ziel:* der eigentliche Rest-Aufwand — beweisen, nicht coden. *(Hängt an P7.)*
- Zwei-Member-Test (zwei ActorSystems, ein Consul-Cluster): ein Adapter je Stream, Ordnung erhalten, Poll
  heilt Totalverlust cross-node.
- **Tor:** Zwei-Node-Gate grün.

### Reihenfolge-Urteil

**P0–P3 sind das Fundament** (de-risken alles Folgende) — *zuerst* und *isoliert*. Danach laufen **P5**
(Prozess-Terminal-Fix, hoher Wert/geringes Risiko) und der **Feature-Strom** unabhängig von der großen
Vereinheitlichung **P4→P6**; **P7→P8** (Multi-Node) sind orthogonal und nur nötig, wenn Multi-Node
gebraucht wird. Das entspricht der Zielbild-Sequenz „erst Emit, dann Features/Prozess, Vereinheitlichung
zuletzt". „Erst alle Features, dann refactoren" ist *nicht* die richtige Reihenfolge — die Emit-/Graph-
Grundlage ist kein Feature, sondern das Fundament.

---

## 8. Kurzfassung — die Merksätze

- **Wir bauen die Maschine neu, nicht den Stack.** API, Generatoren, Marten, Redis, Proto.Actor, gRPC,
  Client bleiben — nur die innere Implementierung wird *eine* Maschine.
- **Zwei Primitive:** ein Schreiber (P1), ein durabler Konsument (P2, zwei Achsen). Alles andere fällt
  daraus.
- **Ein Emit-Primitiv** statt vier — W1/W2 strukturell weg; der Sentinel stirbt (Behandlung nie in der
  Message).
- **Ein kanonischer Kanten-Graph:** jede Kante aus *einer* Signatur-Quelle; keine Namespace-/Namens-Kanten;
  totale Knoten-Identität.
- **Prozess:** Marking aus dem Log (Wahrheit), Cursor als Cache, Terminal per `CorrelationId`-Poll-Routing.
- **Pipeline zerfällt ehrlich:** Event-Pfad → Reaktion, Trigger-Ingress → dünner Push.
- **Transport wird Graph-Knoten (K1):** dann ist Multi-Node ein Decorator, kein Neubau — und der eigentliche
  Rest-Aufwand ist die *Verifikation* (Zwei-Node-Tor).
- **Jede Garantie hat ein Zuhause** — Refactoring heißt: sicherstellen, dass es besetzt ist und nirgends
  fehlt oder doppelt liegt.
