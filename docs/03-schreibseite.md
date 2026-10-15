# 03 — Die Schreibseite (Command → Decider → Event → Store)

Stand 2026-10-13, gegen den Code geprüft. Maßstab: docs/konzept-werkbank.md.

Der Schreibpfad ist das dichte Zentrum des Frameworks. `AggregateActorBase`
(`Infrastructure/Aggregate/ActorSystem/AggregateActorBase.cs`) trägt fast alle Invarianten und Audit-Fixes und ist ausführlich
kommentiert. Die Schalter des Schreibpfads stehen für Nutzer in der Umgebungs-Datei (`umgebungen/umgebung.<name>.json`, Typen in
`Abstractions/Umgebung/UmgebungKonfig.cs`); `CqrsUmgebungExtensions` übersetzt sie in den internen `CqrsFrameworkBuilder`.

## 3.1 Command-Flow end-to-end

**(a) Eingänge.** Ein Command kommt auf einem von drei Wegen ins System; alle enden im selben Aggregat-Actor.
- **gRPC-Streaming-Gateway** (Clients: Blazor, Python): `CqrsClientServiceImpl.HandleCommandAsync`
  (`Infrastructure/GrpcClient/CqrsClientService.cs`) mappt die `CommandRequest` über `ProtoMessageMapper` auf ein `CommandEnvelope`
  und stempelt `OriginSessionId` (Targeted Delivery von Ablehnungen an den Aufrufer). Das **Akteur-Tor** (`Infrastructure/Akteure/AkteurTor.cs`)
  ordnet am Handshake das Token (Header `akteur-token`) einem Akteur zu; jeder Command wird gegen dessen `IDarf`-Menge geprüft, der Akteur
  wird vom Tor in den Envelope gestempelt, nicht vom Client behauptet. Ohne konfigurierte Identitäten ist das Tor offen; dann folgt der
  Akteur dem Modell (der eine `IDarf`-Halter). Eine Zusage von außen (Antwort auf ein Event, mit Kausalität) bekommt eine aus der Ursache
  abgeleitete CommandId und den Modus `Emittiert`; mit Vertrag muss die Antwort im Vertrag stehen. Fehlt der `AggregateType`, löst ihn der
  Server aus der generierten Command→Aggregat-Map auf. Dann `_dispatcher.Dispatch(envelope)`, fire-and-forget.
- **JSON-Eingang** (Werkbank, nur Einblick-Port, nur mit `Transport.JsonEingang`): `POST /eingang/command`
  (`Infrastructure/Eingang/JsonEingang.cs`). Derselbe Weg wie gRPC — Token → Akteur-Tor → Befugnis, Akteur im Envelope, Wirker
  `akteur:<A>` — nur ohne Proto: Typ über die generierte Registry, Felder über den generierten Wire-Kontext. Anders als gRPC wartet er
  auf das `CommandResult` (`ProtoActorAggregateDispatcher.FrageAsync`, ein Versuch, bounded). Ohne angegebene Version läuft der Command
  im Modus `Emittiert`.
- **Emittiert** (Reaktion, Prozess, Fluss — der Dirigent ist der Prozess-Manager): ausschließlich über `CommandEmitter.EmitAsync`
  (`Infrastructure/PubSub/CommandEmitter.cs`) — deterministische CommandId, `CommandModus.Emittiert`, bounded Token, at-least-once auf dem
  Draht; Akteur der Kette und Spur (`Ursache`, `Wirker`) reisen im Envelope. Andere Wege sind per Analyzer verboten (CQRS020/021).

Quell-Nachrichten gehen nicht durch einen Decider: der Treiber hängt sie als erstes Event eines Vorgangs-Streams mit deterministischer Id
an (`StartStream`, genau einmal je Kennung, `Infrastructure/Quellen/Quellen.cs`).

**(b) Routing — Dispatcher zu Cluster-Identität.**
`ProtoActorAggregateDispatcher.Dispatch` (`Infrastructure/Aggregate/ActorSystem/AggregateDispatcher.cs`) baut die Ziel-Identität
typbasiert: `ClusterIdentity.Create(guid, envelope.AggregateType)`. Der Send ist `cluster.RequestAsync<CommandResult>(identity, envelope, ct)`
mit bounded Token (10 s je Versuch, 3 Versuche). Sind die Versuche erschöpft: LogError, durabler Dead-Letter und ein targeted
`CommandFailed` an die `OriginSessionId`, damit der Client nicht still hängt. Der Dispatcher selbst wiederholt nicht über die Versuche
hinaus; die Wiederholung bei Stille liegt im Client (gleichbleibende CommandId, die Aggregat-Inbox dedupliziert; Ausgang
`CommandUnbestaetigt` bei totaler Stille — Details in [08](08-frontend-blazor-client.md)). `AggregateDispatcherExtensions.ResolveAggregateType`
löst den Aggregat-Typ aus der generierten Map (`GeneratedPipelines.CommandAggregateTypes`, Passthrough auf den
`CommandAggregateMapGenerator`) — statt aus einem Magic String.

**(c) Aggregat-Actor — der Kern.**
`AggregateActorBase.ReceiveAsync`: das erste `CommandEnvelope` löst `InitializeAsync` aus (Rehydration aus dem Log, ggf. ab Snapshot);
dann läuft `HandleCommandAsync` im Spur-Rahmen `Spur.Von(Ursache, decide:<Aggregat>/<Command>, Wirker des Absenders)`. Es wird **immer**
`context.Respond(...)` aufgerufen — sonst wiederholt Proto.Actor.

`HandleCommandAsync` dispatcht exhaustiv über den Summentyp `CommandModus`:
- `Client c` → OCC gegen `c.ExpectedVersion`, idempotent (Inbox vor OCC)
- `Emittiert` → keine behauptete Version (`expectedVersion = _state.Version`), idempotent

Der gemeinsame Kern `HandleCommandCoreAsync`:
1. Guard: falsche AggregateId → Fehler.
2. **Vorprüfung** (`CommandVorpruefung.Prüfe`): erst die Framework-Inbox — bereits verarbeitete `CommandId` → idempotenter Erfolg, bereits
   abgelehnte → dieselbe Ablehnung —, dann OCC (`_state.Version != expectedVersion` → Versionskonflikt). Die Reihenfolge sorgt dafür, dass
   ein wiederholt zugestellter Command (Emittiert immer, Client bei Wiederholung nach verlorenem Ack) dasselbe Ergebnis liefert statt
   eines Versionskonflikts.
3. **Decider**: `_handler.HandleCommand(payload).ToList()`.
4. **Noop** (keine Events) → trotzdem eine `KommandoVerarbeitet`-Marke co-committen (sonst bliebe ein Prozess-Schritt ewig offen).
5. Trennung persistenter Events von Ablehnungen (`ITransientEvent`); ein *gemischtes* Ergebnis oder mehrere Ablehnungen werfen laut.
6. **Reine Ablehnung** → kein Append der Ablehnung, kein Apply; eine `KommandoAbgelehnt`-Marke wird co-committet, die Ablehnung geht
   targeted an den Aufrufer und als verlierbarer Hinweis an die Spur.
7. **Erfolg**: die `KommandoVerarbeitet`-Marke wird an die Events angehängt, dann `AppendeAsync` — Effekt und Marke in einer Transaktion.
   **Scheitert ein Append** (Effekt, Noop- oder Ablehnungs-Marke; Versionskonflikt durch einen zweiten Schreiber, verlorene Quittung, DB-Fehler),
   rehydriert der Actor frisch aus dem Log (G-3). Steht die `CommandId` danach in der Inbox, hat der Command gewirkt bzw. ist abgelehnt — das
   wird quittiert (mehrdeutiger Commit, kein falsches Negativ, P10), die Signale gehen nachträglich hinaus. Sonst ist sicher nichts geschrieben:
   der Command wird **einmal** auf dem frischen Stand neu entschieden (ein Client-Command mit veralteter Version scheitert dabei regulär an der
   Vorprüfung); scheitert auch das, kommt ein technischer Fehler zurück.
8. **Post-Append-Grenze**: ab dem Commit ist der Command durabel. Applier mutiert `_state` in-memory (Marken werden nur in der Version
   mitgezählt), Redis-Track, Snapshot ab Schwelle, Publish. Wirft etwas davon, rehydriert der `catch` frisch aus dem Log und quittiert
   trotzdem **Erfolg** (kein falsches Negativ).

Snapshots (best-effort, ab `Schreibpfad.SnapshotSchwelle` und bei Deaktivierung, Version = Struktur-Hash + Code-Hash von State + Applier)
sind in [04 §4.7](04-konsum-und-prozess-maschine.md) beschrieben.

**Passivierung** (PF-5): hört ein Aggregat-Actor `Lebenszyklus.AggregatLeerlaufSekunden` lang nichts (Standard 600), stoppt er sich
(`Infrastructure/Lebenszyklus/Passivierung.cs`, Proto-`ReceiveTimeout`); beim Stoppen schreibt er seinen Snapshot, die nächste Nachricht
aktiviert ihn frisch (Snapshot + Tail, Inbox inklusive). Adapter und Prozess-Manager passivieren genauso ([04](04-konsum-und-prozess-maschine.md)).

## 3.2 Das Aggregat-Modell (reiner Fachcode)

Beispiel aus der Agnostik-Sonde (`GraphExtractor.Kern/Sonde/Ausleihe.cs.txt`, Domäne „Leihwesen“, gekürzt). Ein Aggregat ist eine
`partial class : IState` mit zwei verschachtelten `partial`-Klassen `Decider : IDecider<T>` und `Applier : IApplier<T>`; Decider und
Applier dürfen auch in eigenen Dateien liegen (`Mahnvorgang.Decider.cs.txt`):

```csharp
// Commands — Record mit Guid AggregateId : ICommand; ein erzeugender Command ist ICreationCommand
// (der C#-Client sendet ihn mit erwarteter Version 0).
public record LeiheAus(Guid AggregateId, Signatur Medium, Guid LeserId, int Tage = 14) : ICreationCommand;

// Events — persistent : IEvent ; Ablehnung : ITransientEvent (nie im Log).
public record Ausgeliehen(Signatur Medium, Guid LeserId, int Tage) : IEvent;
public record BereitsAusgeliehen(Guid AusleiheId) : ITransientEvent;

// State — nur fachliche Properties. Id/Version werden generiert.
public partial class Leihvorgang : IState
{
    public Guid LeserId { get; set; }
    public bool Offen { get; set; }

    // Decider — ein Decide je Command; der Rückgabetyp nennt alle möglichen Ausgänge (OneOf, CQRS050).
    public partial class Decider : IDecider<Leihvorgang>
    {
        public IEnumerable<OneOf<Ausgeliehen, BereitsAusgeliehen>> Decide(LeiheAus befehl)
        {
            if (this.State.Offen) { yield return new BereitsAusgeliehen(befehl.AggregateId); yield break; }
            yield return new Ausgeliehen(befehl.Medium, befehl.LeserId, befehl.Tage);
        }
    }

    // Applier — ein Apply je Event, mutiert State.
    public partial class Applier : IApplier<Leihvorgang>
    {
        public void Apply(Ausgeliehen e) { this.State.LeserId = e.LeserId; this.State.Offen = true; }
    }
}
```

**Wie der Fachcode rein bleibt:** `this.State` und der Konstruktor `Decider(Leihvorgang state)` (`Domain.SourceGeneration/Generator.cs`)
sowie `Id`/`Version` (`StateGenerator`) sind generiert. OCC, Signal, Cursor, Idempotenz und Spur erscheinen ausschließlich im Framework.
Die Dedup-Marken (`KommandoVerarbeitet`/`KommandoAbgelehnt`, `Infrastructure/Aggregate/KommandoVerarbeitet.cs`) sind `IProzessIntern`,
werden beim Falten übersprungen (aber versionsgezählt) — der Decider sieht sie nie. Command → Aggregat folgt aus den Decide-Signaturen,
nicht aus Namespaces (`CommandAggregateMapGenerator`).

## 3.3 Event-Store (Marten)

`Infrastructure/Persistence/MartenEventStore.cs`:
- **Append** (`AppendEventsAsync`): `expectedVersion == 0` → `session.Events.StartStream` (wirft bei Doppel-Erstellung); `> 0` →
  `session.Events.Append(id, expectedVersion + count, events)`.
- **OCC**: `EventStreamUnexpectedMaxEventIdException` und die Doppel-Erstellung werden zu `ConcurrencyException`.
- **Metadaten** in **derselben** Transaktion: `CorrelationId`, `CausationId` und die Header
  - `aggregate_type` — der Aggregat-Typ,
  - `akteur` — der Akteur der Kette (`ImAuftrag.Header`; reist mit den Events zu Flüssen, Prozessen, Fristen),
  - die Spur `ursache` (`Stream:Version` des auslösenden Log-Eintrags), `wirker` (Karten-Id des schreibenden Bausteins, z. B.
    `decide:Leihvorgang/LeiheAus`), `ueber` (der Absender des Commands), `stand` (Code-Stand des Hosts) — geschrieben an einer Stelle
    (`Infrastructure/Persistence/SpurHeader.cs`, Konstanten in `Abstractions/Spur/Spur.cs`), fehlende Werte werden weggelassen.
- **Read** (`ReadStreamAsync`): `Where(StreamId == && Version >= from).OrderBy(Version)`, materialisiert `EventEnvelope` (Akteur aus dem
  Header als `UserId`). Dabei läuft jedes Event durch die Upcasting-Naht `GeneratedEventUpcasting.Aufwerten` (1:1 oder 1:N mit
  `SubIndex`) — eine Backend-Primitive für Prod (`IUpcast`), die heute keine Domäne nutzt; ohne Upcast ist sie ein Durchreichen.
- **Poll-Backstop** (`ReadChangedStreamsAsync`): globale Marten-Sequenz `seq_id` mit **Straggler-Karenz** (Default 3 s): die
  High-Water-Mark rückt nur bis zur höchsten Sequenz vor, deren Event älter als die Karenz ist — schützt gegen nebenläufig in umgekehrter
  Seq-Reihenfolge sichtbare Commits. Referenzzeit ist die DB-Uhr (`select now()`), kein App/DB-Versatz.

## 3.4 Group-Commit-Batching + paralleler Drain

`Infrastructure/Persistence/BatchingEventAppender.cs` — Dekorator um `IEventStoreRepository`; nur der Schreib-Hotpath wird gebündelt,
Reads werden durchgereicht. Schalter in der Umgebung: `Schreibpfad.GroupCommit` (an/aus), `BatchGroesse` (Default 256), `LingerMs`
(Default 0), `Drain` (Default 4).

- **Hotpath**: baut ein `Pending` mit `TaskCompletionSource`, schreibt in einen unbounded Channel, gibt `Tcs.Task` zurück. Der Task löst
  **erst beim Batch-Commit** — die scharfe Durabilitätsgrenze. Single-Writer bleibt (der Actor wartet vor dem nächsten Command desselben
  Streams).
- **Drain**: `Drain` parallele Schleifen. Jede sammelt bis `BatchGroesse` oder ein optionales `LingerMs`-Fenster (0 = selbstregulierend:
  unter Last füllt sich die Queue, während der vorige Commit läuft), dann `FlushAsync`.
- **Commit** (`MartenEventBatchWriter.WriteBatchAsync`): N Appends in **einer** `LightweightSession` → ein `SaveChangesAsync` = eine
  Postgres-Transaktion. Metadaten und Spur-Header **pro Event** gesetzt (Voraussetzung fürs Bündeln verschiedener Korrelationen).
- **Fehlermodell**: der Batch ist all-or-nothing; wirft er (z. B. OCC-Konflikt eines Streams), wiederholt `IsolateAsync` jeden Auftrag
  **einzeln** über den inneren Store → nur der Schuldige fällt. Beim Herunterfahren werden verbliebene Aufträge isoliert geschrieben.
- **Verteilt korrekt, weil node-lokal**: Proto.Cluster garantiert Single-Activation — ein Stream lebt auf genau einem Node, zwei Nodes
  bündeln nie denselben Stream, OCC pro Stream bleibt ohne Cross-Node-Koordination korrekt.

Messwerte: [12](12-tests-und-vermessung.md).

## 3.5 Redis Version-Index (nicht-autoritativ)

`Infrastructure/Persistence/RedisVersionTracker.cs`: schneller verteilter Index für Stale-Detection auf der Leseseite (Wahrheit bleibt
der Log). Datenmodell: Hash `agg:{id}` (Felder `v`/`t`/`ts`) + Sekundär-Set `agg_idx:{type}`, mit Schlüssel-Präfix je Umgebung
(`VersionIndex.Praefix`). Geschrieben **nach** erfolgreichem Append, per Redis-Pipeline, bei **jedem** Track indexiert. Graceful
Degradation: `RedisConnectionException`/`TimeoutException` → nur Warning, der Command-Flow bricht nicht. Ganz abschaltbar über
`VersionIndex.Betrieb = Aus` in der Umgebung → `NullVersionTracker`.

## 3.6 Signal-Mechanik `(StreamId, Version)`

- **Vertrag**: `IStateChangeSignal` (`Abstractions/IStateChangeSignal.cs`) trägt nur `Guid StreamId` + `int Version`. Die typisierte
  Variante `IStateChangeSignal<TEvent>` trägt die Event→Signal-Kante am Typ.
- **Erzeugung**: pro persistiertem Event ein `StateChangeVia{Event}` (`Domain.SourceGeneration/SignalTypeGenerator.cs`); die Instanz baut
  `GeneratedSignalFactory.Create(payload, aggregateId, version)` (`SignalFactoryGenerator`, liest das Typ-Argument, keinen Namens-Präfix).
- **Verteilung**: im Erfolgspfad **fire-and-forget** über den `BrokerPublisher` (`PublishFireAndForget` in `AggregateActorBase`). Kein
  Warten auf Shard-Acks — der Command ist bereits committet, Zustellung ist best-effort, durable Konsumenten heilen über Log-Read + Poll.
  Jedes Event trägt seine eigene Position, nicht den Batch-Endwert.
- **Routing**: der `BrokerPublisher` shardet über `Payload.GetType()` — die konkrete Signal-Klasse —, sodass nur Empfänger dieses
  Event-Typs geweckt werden.

## 3.7 Proto.Actor: virtuelle Cluster-Actors

Wiring in `Infrastructure/Extensions/CqrsServiceExtension.cs`:
- `ActorSystem` mit `PartitionIdentityLookup`; der Cluster-Provider folgt `Cluster.Art` der Umgebung: `Einzelknoten` (Mitgliedschaft
  in-memory, ohne Consul — so läuft eine Sim) oder `Consul` (`ConsulProvider`, mehrere Knoten).
- **Aggregat-Kinds generiert**: `GeneratedAggregates.GetAllKinds(system)`. Pro `IState`-Klasse erzeugt der `AggregateActorGenerator`
  (a) `XActor : AggregateActorBase<X>` mit DI-Konstruktor und (b) `AddTransient<XActor>()` + `new ClusterKind(identität, PropsFor<XActor>())`.
  Die Kind-Identität = `[AggregatName("…")]` oder Typname — konsistent zum Routing.
- **Identität pro Command**: `ClusterIdentity.Create(aggregateId, aggregateType)` — der Stream-Guid *ist* die virtuelle Actor-Identität.
  Single-Activation → Single-Writer pro Stream (tragend für Batching und OCC).
- **Multi-Node**: generierter Wire-Serializer `CqrsWireSerializer` an `WithRemote`; `WireSerializerBootCheck` bricht bei einer Lücke ab.
  Siehe [06](06-transport-multinode-betrieb.md).

`Infrastructure/Aggregate/ActorSystem/ClusterKindFactory.cs` (Klasse `ClusterKindsFactory`) ist vollständig auskommentiert — toter Code
aus der Zeit vor dem Generator (docs/13 P3-2).

## 3.8 Schulden im Schreibpfad

Offenes steht in einer Quelle, [13 §13.0](13-reifegrad-schulden-bewertung.md): der parallele Drain skaliert sublinear (P2-6), die
`BoundedInbox` verdrängt ab `Schreibpfad.InboxGrenze` FIFO (G-2), Split-Upcasts verschieben die Version (G-5), der Snapshot-`Clone` ist ein JSON-Round-Trip (W10-2), der tote
Lesepfad `LoadStateAsync` neben `AggregateRehydrator` (P2-9), der Client darf den `AggregateType` selbst setzen (W2-1).
