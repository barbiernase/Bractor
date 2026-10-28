# 03 — Die Schreibseite (Command → Decider → Event → Store)

Stand 2026-10-28 (Plan Pub/Sub-Neustrukturierung Phase 6), gegen den Code geprüft. Ziel für den Umbau: docs/plan-pubsub-neustrukturierung.md.

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
  abgeleitete CommandId und den Modus `Emittiert`; mit Vertrag muss die Antwort im Vertrag stehen. Ziel-Id und Aggregat-Typ folgen dem Command
  (`ICommand<TAggregat>`, unten): nennt die Hülle der Leitung eine andere Id oder ein anderes Aggregat, wird der Command abgelehnt, bevor er
  irgendwohin geht (`ProtoMessageMapper`). Dann `_dispatcher.Dispatch(envelope)`, fire-and-forget.
- **JSON-Eingang** (Werkbank, nur Einblick-Port, nur mit `Transport.JsonEingang`): `POST /eingang/command`
  (`Infrastructure/Eingang/JsonEingang.cs`). Derselbe Weg wie gRPC — Token → Akteur-Tor → Befugnis, Akteur im Envelope, Wirker
  `akteur:<A>` — nur ohne Proto: Typ über die generierte Registry, Felder über den generierten Wire-Kontext. Anders als gRPC wartet er
  auf das `CommandResult` (`ProtoActorAggregateDispatcher.FrageAsync`, ein Versuch, eine Frage mit Frist). Ohne angegebene Version läuft der
  Command im Modus `Emittiert`. Die Antwort nennt Ausgang, Versionen (`version`, `ersteVersion`) und Ablehnung; die geschriebenen Events liest
  der Eingang nur auf Wunsch (`"zeigeEvents": true`) aus dem Log — die Werkbank wünscht sie, ein Treiber unter Last nicht.
- **Emittiert** (Reaktion, Prozess, Fluss — der Dirigent ist der Prozess-Manager): ausschließlich über `CommandEmitter.EmitAsync`
  (`Infrastructure/PubSub/CommandEmitter.cs`) — deterministische CommandId, `CommandModus.Emittiert`, eine Frage des Boten mit Pflicht-Frist
  (§3.6), at-least-once auf dem Draht; Akteur der Kette und Spur (`Ursache`, `Wirker`) reisen im Envelope. Andere Wege sind per Analyzer
  verboten (CQRS020/021; CQRS020 erkennt auch `FrageAsync<CommandResult>`).

Quell-Nachrichten gehen nicht durch einen Decider: der Treiber hängt sie als erstes Event eines Vorgangs-Streams mit deterministischer Id
an (`StartStream`, genau einmal je Kennung, `Infrastructure/Quellen/Quellen.cs`).

**(b) Routing — Dispatcher zu Cluster-Identität.**
Das Ziel eines Commands steht in seinem Typ: `record … : ICommand<TAggregat>` mit `Id<TAggregat> AggregateId` (Plan Pub/Sub-Neustrukturierung
§2.5). Die Framework-Sicht ist `ICommand.Ziel` (die Stream-Id), der `CommandEnvelope` leitet seine `AggregateId` daraus ab — keine zweite Angabe.
`ProtoActorAggregateDispatcher.Dispatch` (`Infrastructure/Aggregate/ActorSystem/AggregateDispatcher.cs`) baut die Ziel-Identität
typbasiert: `ClusterIdentity.Create(guid, envelope.AggregateType)`. Der Send ist eine Frage des Boten (`FrageAsync<CommandResult>`, Frist
10 s je Versuch, 3 Versuche). Sind die Versuche erschöpft: LogError, durabler Dead-Letter und ein gemeldetes (verlierbares) targeted
`CommandFailed` an die `OriginSessionId`, damit der Client nicht still hängt. Der Dispatcher selbst wiederholt nicht über die Versuche
hinaus; die Wiederholung bei Stille liegt im Client (gleichbleibende CommandId, die Aggregat-Inbox dedupliziert; Ausgang
`CommandUnbestaetigt` bei totaler Stille — Details in [08](08-frontend-blazor-client.md)). `AggregateDispatcherExtensions.ResolveAggregateType`
löst den Aggregat-Typ aus der generierten Map (`GeneratedPipelines.CommandAggregateTypes`, Passthrough auf den
`CommandAggregateMapGenerator`) — statt aus einem Magic String.

**(c) Aggregat-Actor — der Kern.**
`AggregateActorBase.ReceiveAsync`: die Id des Actors ist seine **Cluster-Identität** (beim `Started` gelesen), nicht die des ersten Envelopes.
Ein Command, dessen Ziel nicht diese Identität ist (falsch geroutet), wird abgelehnt, **bevor etwas geladen ist** (technischer Fehler an den
Absender und gezielt an die Sitzung gemeldet) — sonst lüde die Aktivierung den fremden Stream unter ihrer Identität. Das erste passende `CommandEnvelope`
löst `InitializeAsync` aus (Rehydration aus dem Log, ggf. ab Snapshot); dann reiht der Actor den Command ein. Jeder Command bekommt **immer**
eine Antwort an seinen Absender — sonst wiederholt der Aufrufer.

**Bündel** (Normalweg, `StarteBuendelAsync`): während ein Commit läuft, nimmt der Actor weitere Commands an und sammelt sie
(`context.ReenterAfter` — die Mailbox ist nicht blockiert). Ist der Commit durch, nimmt er bis zu `Schreibpfad.BuendelGroesse` (Standard 64)
wartende Commands und entscheidet sie nacheinander im Speicher (`EntscheideAsync`, je im Spur-Rahmen `Spur.Von(Ursache,
decide:<Aggregat>/<Command>, Wirker des Absenders)`): dieselbe Vorprüfung und dasselbe Decide wie der Einzel-Pfad unten, die Events eines
schreibenden Commands wendet er sofort an — der nächste entscheidet auf diesem Stand. Alle schreibenden Commands gehen als Teile
(`AppendTeil`: Events, Korrelation, Kausalität = CommandId, Akteur, Spur, Quittung) in **einen** Append (`AppendTeileAsync`, eine Transaktion,
im Group-Commit ein Auftrag). Geantwortet wird jedem erst nach diesem Commit (`AbschliessenAsync`: Cache, Pushes an Clients, Ablehnung an die Sitzung,
Antwort in Mailbox-Reihenfolge — das `CommandResult` trägt nur Ausgang, Versionen und Ablehnung, die Events stehen im Log); eine CommandId,
die zweimal im Bündel steht, bekommt die Antwort ihres ersten Auftretens. Scheitert der Append
(Versionskonflikt durch einen zweiten Schreiber, eine Wiederholung, die der Cache nicht kannte, ein mehrdeutiger Commit, ein DB-Fehler), sind
alle Entscheidungen des Bündels hinfällig: der Actor lädt frisch und behandelt jeden Command einzeln über `HandleCommandAsync` (`EinzelnAsync`)
— ein Command, den das Bündel doch geschrieben hatte, scheitert dort an seiner Quittung und bekommt den Ausgang des ersten Mals. Solange ein
Bündel unterwegs ist, schreibt der Actor keinen Snapshot (sein State trägt nicht committete Entscheidungen) und passiviert nicht. Bei verteilter
Last ist ein Bündel meist ein Command; ein heißes Aggregat teilt sich Commits statt je Command einen.

**Einzel-Pfad** (nach einem gescheiterten Bündel): `HandleCommandAsync` dispatcht exhaustiv über den Summentyp `CommandModus`:
- `Client c` → OCC gegen `c.ExpectedVersion`, idempotent (Inbox vor OCC)
- `Emittiert` → keine behauptete Version (`expectedVersion = _state.Version`), idempotent

Der gemeinsame Kern `HandleCommandCoreAsync` (das Ziel ist schon beim Empfang geprüft):
1. **Vorprüfung** (`CommandVorpruefung.Prüfe`): erst der Inbox-Cache des Actors — bereits verarbeitete `CommandId` → idempotenter Erfolg,
   bereits abgelehnte → dieselbe Ablehnung —, dann OCC (`_state.Version != expectedVersion` → Versionskonflikt). Vor einem Versionskonflikt
   schlägt der Actor in der Inbox-Tabelle nach (`AusgangAsync`): eine Client-Wiederholung, die der Cache nicht mehr kennt, sieht eine
   weitergerückte Version. Die Reihenfolge sorgt dafür, dass ein wiederholt zugestellter Command (Emittiert immer, Client bei Wiederholung nach
   verlorenem Ack) dasselbe Ergebnis liefert statt eines Versionskonflikts.
2. **Decider**: `_handler.HandleCommand(payload).ToList()`.
3. **Noop** (keine Events) → nur die Quittung (Ausgang gewirkt) in die Inbox-Tabelle, kein Event: die Version bleibt
   (`QuittiereOhneEventsAsync`). Eine Wiederholung bleibt ein Noop.
4. Trennung persistenter Events von Ablehnungen (`ITransientEvent`); ein *gemischtes* Ergebnis oder mehrere Ablehnungen werfen laut.
5. **Reine Ablehnung** → kein Event, kein Apply, die Version bleibt; nur die Quittung (Ausgang abgelehnt). Danach geht die Ablehnung als
   Hinweis gezielt an die Sitzung des Aufrufers und an die Funktionen, die sie hören (verlierbar, der Turn wartet nicht), und als verlierbarer
   Hinweis an die Spur. Eine Ablehnung legt auch keinen Stream an. Ist sie für einen Ablauf wichtig, ist sie ein Fakt (Event) — das entscheidet
   der Modellierer über den Typ (Plan Pub/Sub-Neustrukturierung §3.1).
6. **Erfolg**: `AppendeAsync` schreibt die Events; die Quittung des Commands (`Quittung`, Ausgang gewirkt) reist in derselben Transaktion in
   die Inbox-Tabelle (§3.3). Jeder der drei Wege (Effekt, Noop, Ablehnung) trägt so eine Quittung; im Log stehen nur Effekte — **eine
   Version je Aggregat**, die Zahl seiner Events. Eine Quittung **ohne** Events gilt nur, wenn der Stream noch auf dem Stand der
   Entscheidung steht (Kopf-Prüfung `cqrs_kopf` in derselben Transaktion, §3.3) — sonst hätte eine zweite Aktivierung auf einem veralteten
   Stand dauerhaft abgelehnt oder nichts getan; sie scheitert wie ein Versionskonflikt.
   **Kennt die Inbox die `CommandId` schon** (eine Wiederholung, die der Cache nicht kannte — nach Passivierung, Neustart oder jenseits des
   Caches), scheitert der Append am Schlüssel, nichts ist geschrieben (`KommandoSchonEntschiedenException`), und der Actor quittiert den
   Ausgang des ersten Mals — auch wenn er jetzt anders entschieden hätte.
   **Scheitert ein Append sonst** (Versionskonflikt durch einen zweiten Schreiber, verlorene Quittung, DB-Fehler), rehydriert der Actor
   frisch aus dem Log (G-3) und schlägt in der Inbox nach. Steht die `CommandId` dort, hat der Command gewirkt bzw. ist abgelehnt — das wird
   quittiert (mehrdeutiger Commit, kein falsches Negativ, P10), die Pushes an Clients gehen nachträglich hinaus; geweckt hat der
   Wecker nicht (sein Commit galt als gescheitert), die Konsumenten holt der Takt. Sonst ist sicher nichts geschrieben:
   der Command wird **einmal** auf dem frischen Stand neu entschieden (ein Client-Command mit veralteter Version scheitert dabei regulär an der
   Vorprüfung); scheitert auch das, kommt ein technischer Fehler zurück.
7. **Post-Append-Grenze**: ab dem Commit ist der Command durabel. Applier mutiert `_state` in-memory (Einträge des Rahmens wie
   `FristGesetzt` werden nur in der Version mitgezählt), Snapshot ab Schwelle, Push an Clients. Wirft etwas davon, rehydriert der `catch` frisch aus dem Log und quittiert
   trotzdem **Erfolg** (kein falsches Negativ).

Snapshots (best-effort, ab `Schreibpfad.SnapshotSchwelle` und bei Deaktivierung, Version = Struktur-Hash + Code-Hash von State + Applier)
sind in [04 §4.8](04-konsum-und-prozess-maschine.md) beschrieben.

**Passivierung** (PF-5): hört ein Aggregat-Actor `Lebenszyklus.AggregatLeerlaufSekunden` lang nichts (Standard 600), stoppt er sich
(Rahmen, §3.6; Proto-`ReceiveTimeout`) — nie, solange ein Commit läuft oder Commands warten; beim Stoppen schreibt er seinen Snapshot, die
nächste Nachricht aktiviert ihn frisch (Snapshot + Tail; der Inbox-Cache ist dann leer, die Inbox-Tabelle bleibt). Emittenten-Adapter und Prozess-Manager passivieren genauso; die Batch-Leser der
Projektionen (je Projektion und Partition) nicht ([04](04-konsum-und-prozess-maschine.md) §4.1).

## 3.2 Das Aggregat-Modell (reiner Fachcode)

Beispiel aus der Agnostik-Sonde (`GraphExtractor.Kern/Sonde/Ausleihe.cs.txt`, Domäne „Leihwesen“, gekürzt). Ein Aggregat ist eine
`partial class : IState` mit zwei verschachtelten `partial`-Klassen `Decider : IDecider<T>` und `Applier : IApplier<T>`; Decider und
Applier dürfen auch in eigenen Dateien liegen (`Mahnvorgang.Decider.cs.txt`):

```csharp
// Commands — Record mit Id<TAggregat> AggregateId : ICommand<TAggregat> (das Ziel steht im Typ); ein erzeugender Command ist zusätzlich
// ICreationCommand (der C#-Client sendet ihn mit erwarteter Version 0).
public record LeiheAus(Id<Leihvorgang> AggregateId, Signatur Medium, Guid LeserId, int Tage = 14) : ICommand<Leihvorgang>, ICreationCommand;

// Events — persistent : IEvent ; Ablehnung : ITransientEvent (nie im Log). Verweise auf Aggregate sind Id<T>.
public record Ausgeliehen(Signatur Medium, Guid LeserId, int Tage) : IEvent;
public record BereitsAusgeliehen(Id<Leihvorgang> AusleiheId) : ITransientEvent;

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
sowie `Id`/`Version` (`StateGenerator`) sind generiert. OCC, Weckruf, Cursor, Idempotenz und Spur erscheinen ausschließlich im Framework.
Noop und Ablehnung stehen nicht im Log; „gewirkt/abgelehnt“ je CommandId hält die Inbox-Tabelle des Stores. Einträge des Rahmens
(`IRahmenEvent`, heute nur `FristGesetzt`) werden beim Falten übersprungen (aber versionsgezählt) — der Decider sieht sie nie. Command → Aggregat folgt aus
`ICommand<TAggregat>`, nicht aus Namespaces; das Decide muss im Ziel-Aggregat stehen (CQRS010), ein Command ohne typisiertes Ziel baut nicht
(CQRS013, `CommandAggregateMapGenerator`, `ModellAnalyzer`).

## 3.3 Event-Store (Marten)

`Infrastructure/Persistence/MartenEventStore.cs`:
- **Append** (`AppendEventsAsync`): `expectedVersion == 0` → `session.Events.StartStream` (wirft bei Doppel-Erstellung); `> 0` →
  `session.Events.Append(id, expectedVersion + count, events)`. Martens Anhänge-Modus ist **`Quick`**: weil jeder Append seine erwartete
  Version trägt, vergibt Marten die Versionen selbst und schreibt in **einer** Runde — `insert`/`update mt_streams … where version = erwartet`
  (die OCC, auch gegen eine gleichzeitige Transaktion: das `update` wartet auf die Zeilensperre und findet danach nichts), dann die Events
  mit `nextval` im Insert. Der Standard (`Rich`) las vorher außerhalb der Transaktion je Stream die Version, holte die Sequenzen ab und
  hinterließ bei einem gescheiterten Commit Tombstones (G-7).
- **Beilagen** (`Abstractions/Beilage.cs`, `BeilagenSchreiber`): Framework-Buchhaltung, die ein Append in derselben Transaktion mitschreibt —
  `Quittung` (Inbox, unten), `FristFaellig` (Eintrag im Fälligkeits-Verzeichnis der Uhr, mit `FristGesetzt`,
  [04 §4.7](04-konsum-und-prozess-maschine.md)), die Kennung einer Quell-Nachricht. Ein Weg für Einzel-Append und Group-Commit; die Quittungen eines Batches
  gehen in eine Anweisung, die Dokumente als Operationen derselben Session.
- **Inbox** (`InboxTabelle`): Tabelle `cqrs_inbox` im Event-Schema (`command_id` Primärschlüssel, `stream_id`, `abgelehnt`, `zeit` =
  `now()` der schreibenden Transaktion, B-Baum auf `zeit`), ein von Marten verwaltetes Schema-Objekt (`Storage.ExtendedSchemaObjects`).
  Trägt der Append eine `Quittung`, schreibt `QueueSqlCommand` sie in derselben Transaktion (Marten führt sie nach den Events aus); eine
  Schlüsselverletzung auf `cqrs_inbox` wird zu `KommandoSchonEntschiedenException`. Martens Wiederholungs-Policy (Polly) nimmt
  Eindeutigkeitsverletzungen aus — sie sind deterministisch und liefen sonst viermal mit Rollback. `AusgangAsync` liest per Primärschlüssel.
  **Quittung ohne Events** (Noop, Ablehnung): der Append trägt nur Beilagen; statt der Events prüft er den **Kopf** seines Streams — die
  Funktion `cqrs_kopf(stream, erwartet)` (Schema-Objekt neben der Tabelle) sperrt die Zeile in `mt_streams` (`for update`) und scheitert mit
  SQLSTATE `CQ409`, wenn der Stream nicht auf der erwarteten Version steht (ein fehlender Stream steht auf 0). Das wird zu
  `ConcurrencyException`, wie der Versionskonflikt eines Appends mit Events; Polly nimmt `CQ409` ebenfalls aus. Im Group-Commit eine Anweisung
  für alle Köpfe des Batches (`select cqrs_kopf(s, v) from unnest(…)`).
  Der `InboxAufraeumer` (je Knoten, alle 10 min, seitenweise, Lease über einen Transaktions-Advisory-Lock) löscht Quittungen älter als
  `Schreibpfad.InboxHorizontTage` (Standard 30, E2): innerhalb des Horizonts wirkt ein Command genau einmal. Er sucht über den Index, die
  ältesten zuerst, und erst ab der Grenze, bis zu der er zuletzt gelöscht hat (abzüglich 5 min) — so kostet eine Seite, was sie löscht, nicht die
  Größe der Tabelle und nicht die toten Einträge früherer Seiten (PF-27; BRIN verfiel, sobald VACUUM Seiten für neue Quittungen freigab).
- **OCC**: `EventStreamUnexpectedMaxEventIdException` und die Doppel-Erstellung werden zu `ConcurrencyException`.
- **Zeitstempel** (G-10): den setzt die Datenbank beim Einfügen, mit derselben Uhr, die `IDbClock` liest (`EventZeit`, Regel `before insert` auf
  `mt_events`; Ausdruck `UmgebungsDbClock.Ausdruck`, in Sim samt Versatz). Marten schickt die Uhr des Hosts mit, die Regel ersetzt sie. Angelegt
  beim Start mit dem übrigen Schema bzw. vom Migrator, sonst vor dem ersten Schreiben je Store (Weasel kennt keine Trigger) — geschrieben nur, wenn
  sie fehlt oder anders lautet, mit Sperrfrist; ein Mitglied ohne Schema-Recht prüft nur, dass sie da ist.
- **Schema beim Start** (PF-18): ein Standalone-Host legt vor allen übrigen Diensten das ganze Schema an (`SchemaBeimStart`), der Migrator im
  Cluster ebenso (`CqrsSchemaMigrator.ApplyAllAsync`): Martens konfigurierte Objekte, die Read-Models aus der generierten Liste (Marten kennt sie
  sonst erst beim ersten Zugriff) und die Regel für den Event-Zeitstempel. Im Verkehr entsteht kein Schema mehr.
- **Metadaten** in **derselben** Transaktion: `CorrelationId`, `CausationId` und die Header
  - `aggregate_type` — der Aggregat-Typ,
  - `akteur` — der Akteur der Kette (`ImAuftrag.Header`; reist mit den Events zu Flüssen, Prozessen, Fristen),
  - die Spur `ursache` (`Stream:Version` des auslösenden Log-Eintrags), `wirker` (Karten-Id des schreibenden Bausteins, z. B.
    `decide:Leihvorgang/LeiheAus`), `ueber` (der Absender des Commands), `stand` (Code-Stand des Hosts) — geschrieben an einer Stelle
    (`Infrastructure/Persistence/SpurHeader.cs`, Konstanten in `Abstractions/Spur/Spur.cs`), fehlende Werte werden weggelassen.
- **Read** (`ReadStreamAsync`, `ReadKorreliertAsync`, `ReadEventAsync`): festes SQL auf `mt_events` (`stream_id = $1 and version >= $2`, beim
  korrelierten Read dazu `correlation_id = $3` und in derselben Anweisung der **Kopf** des Streams — bis dorthin hat der Leser alles Eigene
  gesehen, denn jedes Anhängen wartet am Stream-Kopf auf das vorige —, ohne Cursor beginnt er hinter dem letzten Event vor dem Start des Vorgangs
  (vor dem ersten Event seines Logs gibt es nichts Eigenes; PF-24); beim Einzel-Read `version = $2`; immer `is_archived = false`, geordnet nach Version) statt Martens
  LINQ, das jede Abfrage neu übersetzte und dabei die eingefangenen Werte kompilierte (PF-21). Den .NET-Typ zur Spalte `type` nennt die generierte
  Registrierung, mit der Marten die Typen auch schreibt; die Daten liest Martens Serializer, von den Kopfzeilen nur die sechs, die ein Envelope
  trägt, als Text-Spalten (`headers->>'aggregate_type'`, `'akteur'`, Spur; PF-22); Spalten, die Marten nach seiner Metadaten-Konfiguration nicht
  führt, liest das SQL als `null`. Materialisiert wird ein `EventEnvelope` (Akteur aus dem Header als `UserId`). Dabei läuft jedes Event durch die Upcasting-Naht `GeneratedEventUpcasting.Aufwerten` (1:1 oder 1:N mit
  `SubIndex`) — eine Backend-Primitive für Prod (`IUpcast`), die heute keine Domäne nutzt; ohne Upcast ist sie ein Durchreichen.
- **Horizont der Vollständigkeit** (`Horizont`, G-9): bis zu welcher globalen Sequenz `seq_id` jedes Event endgültig ist — committet und
  sichtbar oder nie (zurückgerollt). Postgres zieht die Sequenz im Insert innerhalb der Transaktion; nebenläufige Commits werden in umgekehrter
  Reihenfolge sichtbar. Ohne Uhr: eine **Probe** (eine kurze eigene Transaktion ohne Warten auf die Platte, eine Runde) liest erst den Stand des
  Sequenzzählers, dann ihre eigene Transaktionsnummer und die älteste noch laufende. Den Stand liest sie mit `pg_sequence_last_value`, unter der
  Sperre der Sequenz-Seite (`EventStoreAbfragen.Zaehlerstand`): ein gewöhnliches `select last_value` liest ohne Sperre und sah mitunter den Wert,
  den ein `nextval` gerade vorausloggt (jeder 32. schreibt einen WAL-Eintrag und steht dafür kurz bis zu 32 darüber) — die Probe gab so Sequenzen
  frei, die noch niemand gezogen hatte, und Leser rückten über deren Events (G-17, Nachweis I `ZaehlerstandPostgresTests`). Im Anhänge-Modus `Quick` schreibt Marten in jeder
  Transaktion zuerst die Stream-Zeile, dann die Events mit `nextval` — jede Transaktion hat ihre Nummer, bevor sie eine Sequenz zieht
  (Wächter: I `AnhaengeReihenfolgePostgresTests`); Nummern steigen. Läuft keine kleinere Nummer als die der Probe mehr, ist alles bis zu ihrem
  Stand endgültig. Eine lange offene Transaktion hält den Horizont an, statt überholt zu werden; nach 30 s warnt der Store einmal je Minute mit
  der ältesten schreibenden Transaktion (`pg_stat_activity`). Je Store ein Horizont, geteilt von allen Lesern eines Prozesses; solange eine Probe
  offen ist, kommt höchstens alle 5 ms eine neue (dazwischen prüft eine Runde nur die älteste laufende Nummer); ein Leser ohne
  Mitleser (ein Poll) wartet bis 250 ms auf seine eigene Probe, damit seine Marke nicht ein Poll-Intervall zurücksteht. Im Modus `Rich` (zieht
  die Sequenzen vor der Transaktion) verweigert der Horizont den Dienst.
- **Poll-Backstop** (`ReadChangedStreamsAsync`, Emittenten und Dirigent): die High-Water-Mark rückt nur bis zum Horizont vor — schützt gegen
  in umgekehrter Reihenfolge sichtbare Commits, auch gegen eine Transaktion, die lange offen ist.
- **Partitions-Seite** (`ReadPartitionAsync`, Batch-Konsum der Projektionen): erst die gesetzte Sequenz (der Horizont, in derselben Runde wie
  „steht dahinter etwas?“), dann die Events einer Partition (`PartitionsAusdruck`: `get_byte(uuid_send(stream_id), 15) % P`) jenseits der Position, nur die abonnierten
  Typen samt Upcasting-Herkunft, ohne schon Erledigtes, `order by seq_id limit N` — festes SQL wie die Event-Reads (PF-22); materialisiert wie `ReadStreamAsync`
  ([04](04-konsum-und-prozess-maschine.md) §4.1.1).

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
  Postgres-Transaktion. Metadaten und Spur-Header **pro Event** gesetzt (Voraussetzung fürs Bündeln verschiedener Korrelationen); die
  Quittungen aller Appends in **einer** Anweisung (`insert … select * from unnest(…)`), ebenso die Kopf-Prüfungen der Appends ohne Events.
- **Fehlermodell**: der Batch ist all-or-nothing; wirft er (z. B. OCC-Konflikt eines Streams, eine Quittung, die die Inbox schon kennt),
  wiederholt `IsolateAsync` jeden Auftrag **einzeln** über den inneren Store → nur der Schuldige fällt, der innere Store meldet Konflikt
  (`ConcurrencyException`) und Wiederholung (`KommandoSchonEntschiedenException`) getrennt. Beim Herunterfahren werden verbliebene Aufträge isoliert geschrieben.
- **Verteilt korrekt, weil node-lokal**: Proto.Cluster garantiert Single-Activation — ein Stream lebt auf genau einem Node, zwei Nodes
  bündeln nie denselben Stream, OCC pro Stream bleibt ohne Cross-Node-Koordination korrekt.

Messwerte: [12](12-tests-und-vermessung.md).

## 3.5 Nach dem Commit: Wecker und Push

Was nach einem Commit geschieht, ist für jeden Schreiber gleich (Aggregat, Quelle, festgehaltene Ausgabe einer Funktion) und nie Teil der Wahrheit:

- **Wecker** (`Infrastructure/Wecken/Wecker.cs`, Plan §2.3): nach dem `SaveChanges` des Group-Commits und des Einzel-Appends schlägt er je
  geschriebenem Event den generierten Weckplan nach und weckt jedes Ziel einmal — die Partitionen der Projektionen und Funktionen, die den Typ
  hören, und die Uhr bei einer Frist. Der Weckruf trägt nur sein Ziel, darf verloren, doppelt, ungeordnet sein; das Ziel liest ab seiner Position,
  der Takt je Partition (30 s) holt Verlorenes. Einzelheiten [04](04-konsum-und-prozess-maschine.md) §4.1.
- **Push an Clients** (`Infrastructure/Verteilen/Commitmeldung.cs`): je Event der Envelope mit Version und Kopf nach dem Commit, als EINE Nachricht
  je Commit an den Verteiler jedes Knotens (`Verteilung`, nur Typen, die irgendein Client hören darf), der ihn an die Sitzungen seines Knotens
  reicht, die ihn hören — verlierbar (Invariante 6). Ablehnung und `CommandFailed` gehen gezielt: die Sitzungs-Id nennt den Knoten, die Nachricht
  geht an dessen Verteiler und dort an genau diese Sitzung, gleich, was sie hört ([06](06-transport-multinode-betrieb.md) §6.2).

Es gibt keine Signal-Typen, keinen Broker und keinen Versionsindex. Die Version, die eine Query-Antwort je gezeigter Id trägt, kommt aus dem
Fortschritt der Projektion ([04](04-konsum-und-prozess-maschine.md) §4.1).

## 3.6 Proto.Actor: virtuelle Cluster-Actors

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
- **Lebenszyklus** (Plan Pub/Sub-Neustrukturierung Phase 2): jeder Framework-Actor erbt `RahmenActor`
  (`Infrastructure/Lebenszyklus/RahmenActor.cs`). Er öffnet den Fähigkeits-Bereich der Aktivierung, bricht beim Stopp die Arbeit neben der
  Mailbox ab und wartet begrenzt (10 s) auf sie, gibt den Bereich erst frei, wenn nichts ihn mehr nutzt, passiviert nur, wenn der Actor nicht
  beschäftigt ist, beantwortet einen Fehler ehrlich (danach startet Proto den Actor neu) und fasst Weckungen zusammen („läuft → noch einmal“).
- **Zwei Verben** (`Infrastructure/Lebenszyklus/Bote.cs`): Framework-Code spricht einen virtuellen Actor nur über den `Bote` an —
  `Melde` (verlierbar, ohne Antwort, intern gebunden) oder `FrageAsync<T>` (Antwort mit Pflicht-Frist; ohne Antwort `null`, wirft nie).
  Ohne Frist wiederholt Proto.Cluster eine Anfrage unbegrenzt (Platzierung, tote PID); keine Stelle ruft `Cluster.RequestAsync` mehr selbst.

`Infrastructure/Aggregate/ActorSystem/ClusterKindFactory.cs` (Klasse `ClusterKindsFactory`) ist vollständig auskommentiert — toter Code
aus der Zeit vor dem Generator (docs/13 P3-2).

## 3.7 Schulden im Schreibpfad

Offenes steht in einer Quelle, [13 §13.0](13-reifegrad-schulden-bewertung.md): der parallele Drain skaliert sublinear (P2-6), Split-Upcasts verschieben die Version (G-5), der Snapshot-`Clone` ist ein JSON-Round-Trip (W10-2), der tote
Lesepfad `LoadStateAsync` neben `AggregateRehydrator` (P2-9).
