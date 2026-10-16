# 04 — Konsum- & Prozess-Maschine (vier Konsumenten, eine Maschine)

Stand 2026-10-15 (Batch-Konsum, Plan Garantien und Durchsatz Phase 3), gegen den Code geprüft. Maßstab: docs/konzept-werkbank.md.

Projektion, Reaktion, Prozess und Fluss sind **vier durable Konsumenten des Logs**. Sie folgen demselben Muster: ein Signal weckt
schnell, ein Poll heilt verlorene Signale, gelesen wird geordnet aus dem Event-Store (Invariante 1), der Fortschritt liegt durabel im
Store bzw. im Log. Es gibt keinen zweiten Marker und keine Taxonomie: was ein Konsument ist, folgt aus seiner Code-Form (Marker-Interface,
Fähigkeits-Parameter, Rückgabetyp). Konkret laufen drei Schleifen:

| Konsument | Code-Form | Schleife | Fortschritt | Achse B |
|---|---|---|---|---|
| Projektion | `ISubscriber, IPullSubscriber`; `Handle` mit Schreib-Fähigkeit | `BatchLeser` je (Projektion, Partition) | Partitions-Checkpoint (Position, Version) und Guard je Stream, co-committet | replaybar |
| Reaktion | `ISubscriber, IPullSubscriber`; `Handle` ohne Schreib-Fähigkeit, gibt Commands aus | `EmittentenAdapter` je (Konsument, Stream) | `IEmittentenCursor`, vorgerückt nach bestätigtem Emit | emittierend |
| Prozess | `IProzessDefinition`, `Prozess<T>.Definiere(…)` | Dirigent (`ProzessManager`) je Vorgang | Fold aus dem Log (+ Marking-Cursor) | emittierend |
| Fluss | `IPipeline`, `PipelineFluss.Definiere(…)` | derselbe Dirigent | wie Prozess | emittierend |

Ein Fluss wird in Prozess-Regeln übersetzt; Prozess und Fluss sind damit zwei Schreibweisen auf einem Dirigenten (§4.6). Wie man die
Bausteine schreibt: [10](10-entwickler-api.md).

## 4.1 Die Pull-/Signal-Schleifen

Projektionen lesen **im Batch** (eine Transaktion für die Effekte vieler Streams), Emittenten **je Stream** (ihr Cursor rückt erst nach
bestätigtem Emit vor). Beide sind store-agnostisch: die Schleife trägt die *Policy*, die Transaktion trägt der Store (§4.3).

### 4.1.1 Batch-Konsum der Projektionen

**Partition.** Jede replaybare Projektion hat `P` Partitionen (Umgebung `Konsum.Partitionen`, Standard 8). Ein Stream gehört genau einer:
letztes Byte der UUID in RFC-Reihenfolge modulo P — in SQL `get_byte(uuid_send(stream_id), 15) % P`, in .NET
`id.ToByteArray(bigEndian: true)[15] % P` (`Abstractions/PartitionsCheckpoint.cs`, `Partitionierung`). Beide Seiten rechnen dasselbe, ohne Index
und ohne Hash-Abhängigkeit von Laufzeit oder PG-Version; für zufällige und gehashte Ids gleichverteilt. `P` ≤ 256; ein Wechsel von `P` ist ein
Neuaufbau (er gehört zum Stand der Leseseite, §4.3).

**Partitions-Leser** (`Infrastructure/Projections/BatchLeser.cs`, als `BatchLeserActor` ein virtueller Cluster-Actor, Kind
`batch-<Projektion>`, Identität = Partition). Der Cluster hält eine Aktivierung je Partition; fällt das bei einer Netzstörung kurz aus, schützt
die Versionsprüfung des Partitions-Checkpoints (G-4, unten). Ein Durchlauf:
1. **Stand laden:** `PartitionsCheckpoint { Id = "<projektion>:<p>", Position (globale Sequenz), Version, Geparkt }` über den Store
   (`IProjectionTracker.LadePartitionAsync`).
2. **Seite lesen:** `IEventStoreRepository.ReadPartitionAsync` — Events mit `seq_id` jenseits der Position, nur Partition `p`, nur die
   **abonnierten Typen samt aller früheren Versionen, die per Upcasting in sie münden** (generierte Tabelle `GeneratedEventUpcasting.Herkunft`;
   ohne verlässliche Liste ohne Typfilter), ohne schon Erledigtes, `order by seq_id limit N` (`Konsum.BatchGroesse`, Standard 500).
   Materialisiert wird wie in `ReadStreamAsync` (Upcasting, Tombstones überspringen).
3. **Guard je Stream:** die `ProjectionCheckpoint`s der Streams der Seite in **einer** Abfrage (`LadeGuardsAsync`); Events mit
   `AggregateVersion <= Guard` überspringen (Wiederholung nach Neuaktivierung oder verlorenem Commit).
4. **Anwenden:** je Stream in Versionsreihenfolge über den generierten `DispatchAsync` (unverändert). Der Store puffert die Effekte aller
   Streams; vor jedem Event setzt der Leser den Stream des Events (`Stream(id)`), die Schreib-Regel je Dokument (§4.3) prüft gegen ihn.
5. **Commit (eine Transaktion):** alle Effekte, die Guards der berührten Streams und der Partitions-Checkpoint mit erwarteter Version
   (`CommitAsync`). Hat dazwischen eine zweite Aktivierung oder ein Neuaufbau geschrieben, fällt der ganze Batch zurück
   (`PartitionsKonfliktException`), der Leser lädt den Stand neu und liest von dort (G-4).
6. **Danach** der Versions-Index (Redis) für die Events des Batches best-effort, parallel — nie über Uncommittetes.
7. **Weiter**, solange die Seite voll war.

**Position und Nachzügler.** Vor jeder Seite bestimmt `ReadPartitionAsync` die *gesetzte* Sequenz: die höchste, deren Event älter als die Karenz
ist (3 s, DB-Uhr; dieselbe Regel wie beim Poll der Emittenten). Die Position rückt nie darüber hinaus, bei voller Seite nie über deren Ende und
nie über das erste Event eines hängenden Streams (unten). Jüngere Events werden trotzdem angewandt (Latenz); welche schon erledigt sind, merkt
sich die Aktivierung und schließt sie beim nächsten Lesen aus, nach einer Neuaktivierung fängt der Guard sie ab. So überholt die Position keine
noch unsichtbare kleinere Sequenz. Gesetzte Sequenz und „steht dahinter überhaupt ein Event?“ kommen aus **einer** Abfrage; steht hinter der
Position nichts, entfällt die Seite — eine Weckung ohne Neues kostet eine Abfrage statt zwei (PF-14; ebenso der Prozess-Poll und der Poll der
Emittenten). Gelesen wird ab einer Lese-Position im Speicher: durabel wird sie mit jedem Commit eines Batches; über fremde
Events hinweg (leere Seite) höchstens einmal je Poll-Intervall — sonst committete unter Last jede Weckung nur eine Zahl. Grenzfall wie beim
Poll: eine Transaktion, die länger als die Karenz offen ist, kann überholt werden. Den Event-Zeitstempel setzt Marten beim Append mit der Uhr des
Clients; ein Versatz zur DB-Uhr verschiebt die Karenz um diesen Betrag (gemessen unter 1 ms).

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

**Weckung** (`Infrastructure/Projections/BatchKonsum.cs`, `BatchKonsumStartupService`). Ein `SignalReceiverActor` je Projektion abonniert die
`StateChangeVia…`-Signale ihrer Event-Typen und weckt die Partition des Streams (`Wake` an `batch-<Projektion>/<p>`). Je Partition ist höchstens
eine Weckung unterwegs, was währenddessen kommt, wird zu genau einer weiteren; der Actor quittiert sofort und merkt sich während eines
Durchlaufs „noch einmal“ (der Durchlauf läuft neben der Mailbox, `ReenterAfter`). Der **Poll** weckt alle 30 s jede Partition — das Netz für
verlorene Signale; einen Heil-Poll je Stream gibt es für Projektionen nicht mehr. Die Batch-Leser passivieren nicht (es gibt nur Projektionen ×
Partitionen davon, der Poll weckt sie ohnehin).

**Rückstand.** `/health` und `/monitoring/metrics` melden über alle Projektionen die relevanten Events jenseits der Positionen
(`KonsumRueckstand`, eine Abfrage je Projektion) und die geparkten Streams.

### 4.1.2 Emittenten je Stream

Reaktionen (und der Fluss-Event-Pfad der Handle-Welt) laufen je (Konsument, Stream) im `EmittentenAdapter`
(`Infrastructure/Projections/EmittentenAdapter.cs`): ihr Effekt ist ein Command, ihr Cursor rückt erst nach bestätigtem Emit vor — ein Cursor
je Partition ließe einen unbekannten Emit die ganze Partition halten (E8). `WakeAsync(streamId, ct)` → `bool` (bestätigt aufgeholt), für Signal
und Poll gleich:
1. **Startposition:** `IEmittentenCursor.LadeAsync − 1`; ohne Cursor bewusst `−1` (ab 0, at-least-once, nur für idempotente Effekte gültig).
2. **Geordnet lesen** ab `applied+1` über `IEventStoreRepository.ReadStreamAsync`.
3. **Pre-Dispatch-Guard:** `if (e.AggregateVersion <= applied) continue;`.
4. **Dispatch** je Event mit einem frischen `ProjectionWriter` und den `EmitAusgaenge` der Weckung (`Infrastructure/PubSub/EmitAusgaenge.cs`,
   trägt das Token): ein ausgegebener Command wird sofort gesendet, sein Ausgang erfasst — die Commands eines Tails laufen parallel.
5. **Ausgänge abwarten:** bleibt ein Command „unbekannt“, endet die Weckung mit `false` — der Cursor bleibt stehen, die nächste Weckung sendet den
   Tail mit denselben CommandIds erneut (die Inbox des Ziels verwirft, was schon gewirkt hat).
6. **Cursor vorrücken:** `SchreibeAsync` erst nach bestätigtem Emit; scheitert das Schreiben, sendet die nächste Weckung erneut.

**Transport** (`Infrastructure/Projections/PullPath.cs`): ein `SignalReceiverActor` weckt die Cluster-Identität `(pull-<Typ>, StreamId)`; der
**Poll** (`Poller`, 30 s) liest über `ReadChangedStreamsAsync` seitenweise (1 000 Events) die Streams, die jenseits der Hochwassermarke ein
Event der **abonnierten Typen** bekommen haben (Diskriminatoren samt Upcasting-Herkunft, `BatchLeser.TypenAus`; nur Sequenz und Stream, keine
Daten), und rückt sie je Seite **nur** vor, wenn jede Weckung als `WakeAck(Aufgeholt: true)` zurückkommt (bounded 10 s) — kein terminaler,
unverarbeiteter Stream wird still übersprungen. Ein Stream ohne abonniertes Event weckt keinen Adapter (PF-12: früher bekam jeder geänderte
Stream des Logs einen Cursor). Der
Signal-Weckungen sind je Stream zusammengefasst (`Weckungen<TKey>`: höchstens eine unterwegs, eine hinterher; dieselbe Klasse weckt die
Partitionen des Batch-Konsums) — ohne das wurde unter Last jedes Signal ein eigener Read des Adapters (PF-12). Der
`SignalAdapterActor` je Stream serialisiert beide Quellen über seine Mailbox (Single-Writer je Stream) und passiviert nach
`Lebenszyklus.AdapterLeerlaufSekunden` (Standard 120; PF-5) — der Cursor liegt durabel im Store.

**Tombstones:** scheitert ein Append, füllt Marten die reservierten Sequenzen mit `Tombstone`-Events in einem eigenen Stream.
`MartenEventStore` meldet diesen Stream nicht als geändert und liest keine Tombstones (G-7, erledigt); Poll und Partitions-Leser laufen über sie
hinweg.

### 4.1.3 Last ohne Eingang

Kommt nichts hinein, arbeiten nur die Takte (gemessen auf dem Messserver, Zeitreihe vom 2026-10-16 im Messarchiv docs/12-anhang-messarchiv.md, PF-14):
- **Batch-Poll** alle 30 s je Projektion und Partition eine Weckung, ohne Neues eine Abfrage (Beispieldomäne: 5 Projektionen × 8 Partitionen =
  40 Abfragen je 30 s — der größte Posten, er wächst mit Projektionen × Partitionen);
- **Prozess-Poll** und **Poll der Emittenten** alle 30 s je eine Abfrage;
- **Backstop** jede Sekunde die DB-Uhr und die fälligen Fristen, alle 15 s die Offen-Liste; der `FristScheduler` alle 5 s DB-Uhr und Plan.

Zusammen gemessen L1 3,8 und L5 2,6 Abfragen/s bei 1,5–2,6 % CPU eines Kerns, unabhängig vom Bestand (mit dem Batch-Poll wächst es je
Projektion, PF-15). **Nachlauf:** unter Höchstlast rücken Prozess-Poll und Poll der
Emittenten ihre Marke oft nicht vor (eine Weckung an einen ausgelasteten Manager bzw. Adapter bleibt in 10 s unbestätigt, G-8, §4.1.2); der erste
Poll-Takt nach der Last liest dann einmal nach, was dazwischen kam (je Event einmal, Beispieldomäne ≈ 31 000 Events in ≈ 5 s), und die Batch-Leser
schreiben ihre Position über fremde Events einmal fest. Danach ist der Host still; die Messung wartet diesen Nachlauf ab und weist ihn eigens aus.

## 4.2 Die zwei Achsen

**Achse „Transport“:** `IPullSubscriber : ISubscriber` wählt den Pull-Pfad. Der `PullPathGenerator`
(`Infrastructure.SourceGeneration/PullPathGenerator.cs`) findet jede Klasse mit `ISubscriber` **und** `IPullSubscriber` und erzeugt je
Konsument die Verdrahtung (`GeneratedPullPaths`): für eine replaybare Projektion ein Batch-Kind `batch-<Typ>` samt `BatchRegistrierung` und
`ProjektionsStand`, für einen Emittenten ein Adapter-Kind `pull-<Typ>` samt `PullPathRegistration`. Die Handle-Signaturen im Fachcode sind
dieselben.

**Achse B — replaybar vs. emittierend** — zwei getrennte Pfade mit getrennten Typen, kein gemeinsamer Konstruktor:
- `IProjectionTracker` (Batch-Naht, nur Projektionen): `LadePartitionAsync`, `LadeGuardsAsync`, `BeginneBatch`, `Stream(id)`, `CommitAsync(stand,
  guards)` **+ `ResetAllAsync`** (Neuaufbau).
- `IEmittentenCursor` (`MartenEmittentenCursor`, nur Emittenten): nur `LadeAsync`/`SchreibeAsync`, **bewusst ohne Reset** — ein Emittent wird nie
  blind neu abgespielt; der Emittenten-Pfad nimmt keinen Tracker.

**Woraus die Achse folgt: aus den Schreib-Fähigkeiten der Handles.** Ein Handle bekommt die Store-Funktionen, die er benutzen darf, als
Parameter (`Handle(evt, env, writer, INotiereAusleihe f)`; Fähigkeit = Interface `: IWriteStore` mit genau einer Funktion, CQRS051). Der
Generator sammelt die Schreib-Fähigkeiten aller Handles einer Klasse: hat sie welche, ist sie replaybar und bekommt das Batch-Kind; die
generierte Fabrik öffnet je Partitions-Leser einen **Fähigkeits-Bereich** (`IFaehigkeitsFabrik.Oeffne()`,
`Infrastructure/Projections/DiFaehigkeitsFabrik.cs`): Stores sind Scoped, jede Fähigkeit eines Stores liefert in diesem Bereich **dieselbe
Instanz**, und diese Instanz muss ein `IProjectionTracker` sein (sonst bricht der Start laut). Ohne Schreib-Fähigkeit ist der Konsument
emittierend und bekommt den `IEmittentenCursor`. Gehören die Schreib-Fähigkeiten zu mehreren Stores, bricht der Build (**CQRS052**). Ein Store
in Konstruktor, Feld oder Eigenschaft eines Konsumenten ist verboten (**CQRS054**), `new …Store()` ebenso (**CQRS055**).

| Konsument | Schreib-Fähigkeiten der Handles | Rückgabe `Handle` | Achse B |
|---|---|---|---|
| Projektion | aus genau einem Store, der `ICoCommitTracker` trägt | `Task` (Effekt über `writer.Execute`) | replaybar |
| Reaktion | keine | `IAsyncEnumerable<Cmd>` bzw. `…<OneOf<…>>` | emittierend |
| Prozess / Fluss | — (kein `Handle`) | — | emittierend (Fold + Marking-Cursor, §4.5) |

## 4.3 Projektionen und der Co-Commit

Form (neutral aus der Agnostik-Sonde, `GraphExtractor.Kern/Sonde/Lesen.cs.txt`): `partial class Leihliste : ISubscriber, IPullSubscriber`
mit `Handle(Ausgeliehen evt, IAggregateEnvelope envelope, ProjectionWriter writer, INotiereAusleihe notiere)`. Je Event
`writer.Execute(readModelId, ctx => …)`: der Effekt über die Fähigkeit plus `ctx.Track<T>(id)` für den Versions-Index. Was entsteht, liest
der Generator nur aus der Signatur (CQRS050), nie aus dem Rumpf.

**Theorie (der gültige Kern).** Exactly-once-*Zustellung* gibt es nicht (Effekt und Quittung lassen sich über eine Grenze nie atomar
koppeln); erreichbar ist exactly-once-*Wirksamkeit*, und dafür gibt es genau zwei Mechanismen:

| Mechanismus | Voraussetzung | kostet |
|---|---|---|
| **A — atomarer Co-Commit:** Effekt + Fortschritt in einer Transaktion | Effekt und Fortschritt in derselben transaktionalen Ressource | den Store |
| **B — idempotenter Effekt** + at-least-once | jeder Effekt wiederholungsfest (Upsert) | den Entwickler |

B entkommt der Atomarität bei Append-Effekten nicht (ein „schon gesehen?“ müsste selbst atomar mit dem Append geschrieben werden). Die Naht
`IProjectionTracker` ist deshalb bewusst minimal und store-agnostisch: der Leser ruft Puffern und Commit als Schritte; ob sie gemeinsam
gültig werden, entscheidet der Store. Damit ein Store aus dieser Naht A macht, muss er implizite Bedingungen erfüllen: Store und Tracker sind
dieselbe Instanz, Effekte werden gepuffert statt durchgeschrieben, die Unit-of-Work folgt aus der Aufrufreihenfolge (`BeginneBatch` = Beginn,
`CommitAsync` = Commit), Read-your-writes im Batch (über Streams hinweg), eine Instanz je Partitions-Leser (Scoped), und ein geworfener Dispatch
committet nie. Statisch beweisbar ist Atomarität nicht — sie bleibt eine Laufzeiteigenschaft der Store-Transaktion und wird per Crash-Test
gezeigt (`CoCommitPostgresTests`, docs/12).

**Umsetzung.** Diese Bedingungen erfüllt die eine Basisklasse `MartenCoCommitStoreBase` (`Core/MartenCoCommitStoreBase.cs`, Framework), von der
alle Projektions-Stores erben: Effekte werden nur gepuffert, `CommitAsync` spielt sie zusammen mit den Guards (`ProjectionCheckpoint` je Stream)
und dem `PartitionsCheckpoint` in **einer** Marten-`IdentitySession` ab, ein `SaveChangesAsync`. Der Partitions-Checkpoint trägt eine
numerische Revision (`Core/KonsumSchema.cs`): noch nie geschrieben → `Insert` (ein zweiter Erster scheitert am Schlüssel), sonst
`UpdateRevision` auf Version + 1 (eine zweite Aktivierung scheitert an der Version) — **G-4 geschlossen**. Die Fähigkeits-Bereiche (§4.2)
liefern „Store == Tracker“ per Konstruktion. Dazu die Schreib-Regel je Dokument, geprüft gegen den Stream des laufenden Events: eigenes Dokument
(Schlüssel = Stream), Zeile dieses Streams, oder geteiltes Dokument (`IGeteiltesReadModel`, Versionsprüfung; ein Konflikt verwirft den Batch →
`SchreibKonfliktException`, §4.1.1). Dass ein Store nur über diese Puffer-Methoden schreibt und Lese- und Schreib-Fähigkeiten nicht vermischt,
prüft der `StoreZugriffAnalyzer` (CQRS066/067).
- **Hebel 1 (umgesetzt):** Atomarität als eigener Typ. `ICoCommitTracker : IProjectionTracker` tragen nur co-committende Stores; der
  GA-1-Check (`Infrastructure/Projections/GaEinsPruefung.cs`, beim ersten Spawn eines Partitions-Lesers) bricht, wenn eine `IAppendProjektion`
  keinen `ICoCommitTracker` hat.
- **Hebel 2 (offen):** eine framework-getriebene Unit-of-Work (der Leser holt eine Einheit vom Store und routet Effekt und Fortschritt
  hindurch), die Fehlverdrahtung strukturell unbaubar machte.

**Neuaufbau:** `ProjectionRebuilder` leert das Ziel, setzt Partitionen und Guards zurück (`ResetAllAsync`, `Core/KonsumZuruecksetzen.cs`) und
lässt je Partition einen Batch-Leser ab 0 laufen — seitenweise, als Strom, nie der ganze Log als Liste. Zurückgesetzt wird nicht durch Löschen,
sondern mit neuer Revision (Position 0, nichts geparkt): ein noch laufender Leser verliert seinen nächsten Commit, statt seinen alten Stand wieder
anzulegen. Emittenten haben kein Reset und liegen nie auf diesem Pfad. In Sim vergleicht `ProjektionsStandDienst` beim Start den Stand jeder
Projektion (Code-Hash der Klasse + Stores + die Form des Konsums mit der Partitionszahl) mit `<schema>.stand` und baut bei Abweichung die ganze
Leseseite neu auf (leeren, Partitionen und Guards zurücksetzen; der erste Poll weckt jede Partition); in Prod meldet er nur
(`docs/konzept-werkbank.md` §4.6).

## 4.4 Reaktionen & das Emit-Primitiv

Eine Reaktion ist ein Pull-Konsument ohne Schreib-Fähigkeit, dessen `Handle` Commands ausgibt. Keine Domäne der Beispiel-Solution hat
derzeit eine; die Form zeigt die Sonde (`GraphExtractor.Kern/Sonde/Lesen.cs.txt`):
```csharp
public partial class MahnungBeiSchaden : ISubscriber, IPullSubscriber
{
    public string SubscriberId => "mahnung-bei-schaden";
    public async IAsyncEnumerable<EroeffneMahnung> Handle(SchadenGemeldet evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    { … yield return new EroeffneMahnung(…); }
}
```
Der **Rückgabetyp** (konkret oder `OneOf<…>`, CQRS050) sagt, was entstehen kann. Die Ausgabe routet der `HandlerOutputRouter`
(`Infrastructure/PubSub/HandlerOutputRouter.cs`): `ICommand` → an das Ziel-Aggregat, `IEvent` → reaktiv über den Broker re-publiziert
(verlierbar, in Sim als Spur-Hinweis sichtbar).

Jeder Command geht durch das **eine** Emit-Primitiv `CommandEmitter.EmitAsync` (`Infrastructure/PubSub/CommandEmitter.cs`):
- deterministische `CommandId` aus `EmitKausalität` (Korrelation + auslösender Stream + Position/Command-Typ) → stabil über Re-Wakes,
- `CommandModus.Emittiert()` = **keine erwartete Version**,
- **bounded** (`_sendeFrist`, Standard 5 s), at-least-once, ohne eigenen Retry;
- liefert den **Ausgang** (`EmitAusgang`): `Zugestellt` (Erfolg, auch als erkannte Wiederholung), `Abgelehnt` (fachlich, durabel als Marke —
  `CommandResult.Abgelehnt`), `Unbekannt` (Frist abgelaufen, technischer Fehler, Transportfehler). Keine Ausnahme verlässt den Emitter. Wer
  „unbekannt“ bekommt, wiederholt an seinem durablen Punkt mit derselben CommandId: der Emittenten-Cursor bleibt stehen (§4.1), die Frist liegt
  weiter (§4.6), der Dirigent wiederholt nach Backoff (§4.5);
- **Drossel je Ziel** (`EmitDrossel`): höchstens 8 Sends je Ziel-Aggregat gleichzeitig unterwegs (je Knoten); der Rückstau eines heißen
  Aggregats liegt vor dem Send statt in seiner Mailbox, sodass die Sends in ihrer Frist bleiben (PF-9).

**Dedup macht die Framework-Inbox, nicht der Fachcode.** Die Inbox ist eine Tabelle des Event-Stores (`cqrs_inbox`, `InboxTabelle`,
[03 §3.3](03-schreibseite.md)): jeder Append des Aggregat-Actors trägt die Quittung seines Commands (gewirkt oder abgelehnt), geschrieben in
derselben Transaktion wie die Events und eindeutig je CommandId. Eine Wiederholung scheitert am Schlüssel, nichts wird geschrieben, der Actor
quittiert den Ausgang des ersten Mals. Vor dem Append erkennt ein kleiner Cache im Actor (`BoundedInbox`, je Ausgang 1 000 CommandIds) die
häufige Wiederholung kurz nach dem ersten Mal schon **vor** der OCC-Prüfung. Exactly-once gilt innerhalb des Horizonts
(`Schreibpfad.InboxHorizontTage`, Standard 30 Tage), nicht mehr innerhalb eines Fensters von CommandIds (G-2 geschlossen).

**Marken im Ziel-Stream nur noch für den Prozess-Fold** (E1, Variante A): eine Wirkung hinterlässt nur ihre Events — das erste trägt
`causation_id = CommandId`, daran erkennt der Prozess-Manager sie. Ein Noop hinterlässt die Marke `KommandoVerarbeitet(CommandId)`, eine
fachliche Ablehnung die Marke `KommandoAbgelehnt(CommandId, Grund)` (`Infrastructure/Aggregate/KommandoVerarbeitet.cs`, `IProzessIntern`:
der Applier sieht sie nie, die Version zählt sie). Was daraus garantiert ist: [11](11-garantien.md).

**Analyzer-Zwang** (`CommandEmitAnalyzer`): **CQRS020** (rohes `RequestAsync<CommandResult>` außerhalb von Emitter/Dispatcher),
**CQRS021** (Command-Send mit `CancellationToken.None`/`default`). Der generierte Pull-Emit-Pfad reicht das Token der Weckung durch
(`EmitAusgaenge.Token`).

## 4.5 Prozess-Maschine (der Dirigent)

**Definition (DSL).** Ein Prozess ist eine `IProzessDefinition` mit typisierten Event→Aufruf-Regeln über
`Prozess<TAuslöser>.Definiere(p => …)` (`Abstractions/Prozess/ProzessBuilder.cs`):
- `p.Auf<E>().Sende<Cmd>(e => …)` — Command an ein Aggregat; `.SendeJe<Cmd>(…)` — Fan-out dynamischer Breite;
- `.Rufe<F>(e => auftrag)` — Katalog-Funktion statt Aggregat (§4.6); `.Zeitlimit(t)` je Aufruf;
- `.RückgängigDurch<Cmd>(…)` / `.RückgängigDurchJe<Cmd>(…)` — Gegenzug für die Kompensation;
- `.Und<E2>()` (bis drei Events) — Join; `.UndAlle<ESammel>(e => n)` — Count-Join, die Breite steht im Auslöser.

Beispiel (Sonde, `GraphExtractor.Kern/Sonde/Gebuehren.cs.txt`):
```csharp
public sealed class SchadenBeiRueckgabe : IProzessDefinition
{
    ProzessRegeln IProzessDefinition.Regeln => Prozess<Zurueckgegeben>.Definiere(p =>
    {
        p.Auf<Zurueckgegeben>().Sende<MeldeSchaden>(e => new MeldeSchaden(Guid.NewGuid(), e.LeserId));
        p.Auf<SchadenGemeldet>().Rufe<ISchadensSchaetzung>(s => new SchaetzeSchaden(s.LeserId, 25m)).Zeitlimit(TimeSpan.FromMinutes(5));
        p.Auf<SchadenGeschaetzt>().Sende<EroeffneMahnung>(g => new EroeffneMahnung(Guid.NewGuid(), g.Betrag));
    });
}
```
In der Beispieldomäne ist `Domain/Sammelvorgang/TeilFertigProzess.cs` der kleinste Fall (ein `Auf → Sende`).

Der `ProzessRegelnGenerator` (Domain.SourceGeneration) legt jede Definition als Daten in `GeneratedProzessRegeln.Alle` (Name = Klassenname,
CQRS012; Regel-Namen `<Auslöser>[+<Und>]→<Ziel>`, CQRS072). Der `ProzessRegelDiagnosticGenerator` prüft CQRS001–003 (doppelter Auslöser,
Command ohne Decider, Command-Typ explizit). Jede Assembly mit Prozessen oder Flüssen hat ihre eigene Registry
`{Assembly}.Prozess.GeneratedProzessRegeln`; in der Komposition vereinigt der `ProzessVerbundGenerator` die Registries aller referenzierten
Rollen-Projekte zu `GeneratedProzessVerbund.Alle` (CQRS012: derselbe Name in zwei Assemblies; CQRS099: Prozesse ohne Registry), ohne eine getippte
Liste von Projekten. Das Framework liest den Verbund über `Anwendung.Prozesse` (`GeneratedProzesse.AlleRegeln`).

**Kern-Invariante: Struktur aus Code, Marking aus dem Log.** Der generische `ProzessManager` (`Infrastructure/Prozess/ProzessManager.cs`)
ist ein einmal geschriebener Petri-Netz-Interpreter; je Vorgang (Korrelation) ein virtueller `ProzessManagerActor`. Sein eigenes Log ist der
Stream der Korrelation (`ProzessGestartet`, `SchrittGescheitert`, `SchrittUmgeleitet`, `ProzessBeendet`).

**Start und Zustellung** (`KorrelationsRouter`, `ProzessManagerWiring.cs`): der Router abonniert die Signale aller beteiligten Events und
routet sie parallel (bis 32, `KorrelationsRouterActor`; zustandslos, Weckungen idempotent — PF-12). Ein Auslöser-Signal startet einen Vorgang mit
deterministischer Korrelation `ProzessId.Für(Name, Stream, Version)`, ohne Read (der Signal-Typ nennt das Event, generierte Tabelle
`Signale.EventToSignal`); ein Ziel-Event trägt die Korrelation in seinen Metadaten (vom Manager beim Feuern gesetzt) — der Router liest genau dieses
Event (`ReadEventAsync`) und weckt daraus — mit Stream und Version des Events als Quelle. Den **Offen-Index** schreibt der Manager als
Beilage im Append des `ProzessGestartet` (`VorgangOffen`) und räumt ihn als Beilage im Append des `ProzessBeendet` ab (`VorgangBeendet`), je in
einer Transaktion ([03 §3.3](03-schreibseite.md)): es gibt keinen gestarteten Vorgang ohne Eintrag (ein Abbruch zwischen Start und Eintrag ließ
früher Vorgänge liegen, die niemand weckte). Der `ProzessManagerStartupService` betreibt dazu:
- den **Prozess-Poll** (`ProzessPoll`, 30 s, PF-4): er liest das Log seitenweise ab seiner Hochwassermarke (`ReadPartitionAsync`, eine
  Partition, 500 Events) und routet die geladenen Events direkt (`ProzessPollFilter`) — ein Auslöser startet seinen Vorgang nur, wenn es ihn
  noch nicht gibt oder er offen ist (`StreamsVorhandenAsync`, eine Abfrage je Seite), jedes andere Event weckt nur über die Korrelation eines
  offenen Vorgangs. Die Weckungen sind bestätigt (WakeAck, 10 s, parallel bis 32); die Marke rückt nur bis vor die erste unbestätigte (G-8) und
  wird je Seite gespeichert;
- den **§3-Backstop** über den durablen `IProzessOffenIndex`: jede Sekunde weckt er die Vorgänge mit fälliger **Frist** (Zeitlimit,
  Warte-Knoten, s. u.), alle 15 s gleicht er den **Heil-Plan** (`Heilplan`, im Speicher des Knotens) mit den offenen Vorgängen ab: eine
  Heil-Weckung nach 15 s Stille, danach mit verdoppeltem Abstand bis 1 h; jede Weckung durch Signal oder Poll setzt zurück. Das ist das Netz für
  verlorene Weckungen ohne Ziel-Event (unbekannter Emit-Ausgang, Absturz). Je Vorgang läuft höchstens eine Backstop-Weckung zugleich (bounded
  10 s). Im Cluster betreibt jeder Knoten seinen Backstop (eine Lease fehlt noch, docs/13 PF-4).

**Fold.** Je Ziel-Stream ab `StreamCursor[s]+1` nur die Events der eigenen Korrelation lesen (`ReadKorreliertAsync`: jede Wirkung, Noop- und
Ablehnungs-Marke eines Befehls und jedes Funktionsergebnis trägt sie; in viel beschriebenen Streams waren sonst fast alle gelesenen Events fremd), in `MarkingKompakt` einarbeiten, ein Fixpunkt zieht die Kandidaten. Drei getrennte
Ergebnis-Achsen je Vorgang:
- **ErgebnisDa** = aufgelöst (irgendein Ziel-Event, auch Noop-Marke) → nicht mehr feuern;
- **WirkungDa** = ein Domänen-Event (kein `IProzessIntern`) → kompensierbar, aktiviert Joins;
- **AbgelehntDa** = durable `KommandoAbgelehnt`-Marke → wird `SchrittGescheitert`.

Ohne die dritte Achse läse der Vorwärtszweig eine Ablehnung als „aufgelöst“ und schriebe fälschlich `ProzessBeendet(true)`.

**Ein Schritt je Weckung** (`WakeAsync`): Marking falten → neue Ablehnungen und abgelaufene Zeitlimits durabel als `SchrittGescheitert`
(im Fluss bei verdrahtetem Fehler-Port als `SchrittUmgeleitet`, §4.6) → ohne Fehlschlag: alle offenen Funktions-Aufträge anbieten
(parallel, ohne Selbst-Weckung — der Ausführer weckt mit dem Ergebnis), den ersten offenen Command feuern (sequenziell), sind keine Aufrufe
offen: `ProzessBeendet(true)` → mit Fehlschlag: **Kompensation** rückwärts über die erfolgreichen Regeln mit Gegenzug; ist ein Gegenzug selbst
abgelehnt, endet der Vorgang als `ProzessBeendet(false, KlärungNötig: true)` mit einem best-effort Dead-Letter-Eintrag (kein endloses
Neufeuern) — sonst `ProzessBeendet(false)`.

**Feuern** geht über `ProzessManagerActor` → `DetachedProzessSend` (eine Instanz je Manager) → `CommandEmitter.EmitAsync` mit vorgegebener
CommandId: der deterministische **Vorgang ist die CommandId**. Der Fold matcht den durablen Ausgang über `CausationId == vorgang`; der
Emit-Ausgang entscheidet nur über das Wecken (PF-9): nach „zugestellt“ oder „abgelehnt“ weckt sich der Manager sofort selbst, damit der Fold den
Ausgang sieht (das Ergebnis der letzten Regel ist Auslöser keiner Regel); nach „unbekannt“ erst nach einem Backoff je Vorgang (1 s, 2 s, 4 s …
höchstens 30 s, gestreut). Solange der Send eines Vorgangs läuft oder sein Backoff wartet, sendet keine andere Weckung (Signal, Poll, Backstop)
ihn erneut. Mit der Drossel je Ziel (§4.4) baut ein überlastetes Aggregat seinen Rückstau ab, statt dass Wiederholungen ihn vervielfachen.

**Passivierung** (PF-5): ein Manager ohne Nachricht stoppt nach `Lebenszyklus.ProzessManagerLeerlaufSekunden` (Standard 600). Ein still
wartender Vorgang passiviert; ihn wecken seine Frist, ein Event oder die nächste Heil-Weckung. Der Kaltstart liest den Marking-Cursor bzw. faltet
voll.

**Marking-Cursor (P5b).** `MarkingKompakt` (`Abstractions/Prozess/ProzessMarking.cs`) hält je Ziel-Stream einen `StreamCursor` und verdichtete
Vorgangs-Marken statt roher Tokens:
- ein HOT-Cache je Manager-Instanz trägt das Marking über die Weckungen einer Aktivierung; ein Vorgang, der in ihr startet, beginnt sein Marking
  im Start (Auslöser schon gelesen, kein Laden aus dem Store) und gilt als **frisch**: jeder Ziel-Stream, in den diese Aktivierung nie gefeuert
  hat, ist sicher leer;
- **gezielte Reads** (warm): weckt der Router wegen eines Events, liest der Manager nur dessen Stream — und gar nichts, wenn sein Cursor schon so
  weit ist; nach einem Send bzw. einem Funktions-Ergebnis (`ProzessWake.Vorgang`) nur den Ziel-Stream — und gar nichts, wenn der Ausgang des
  Vorgangs schon gefaltet ist; ohne Quelle die Streams, in die er gefeuert hat und deren Ergebnis noch aussteht (`dirty`). Ein gelesener
  Ziel-Stream ohne eigenes Event gilt als gesehen (Cursor bis zur gelesenen Version) und wird nicht „auf Verdacht“ erneut gelesen. Dass dabei
  nichts verloren geht, trägt das erneute Feuern: jede Weckung feuert den ersten offenen Command wieder (der Send-Takt verwirft das Duplikat) und
  markiert damit seinen Ziel-Stream;
- der Kopf-Zustand des eigenen Logs bleibt im Speicher und wird mit jedem eigenen Append fortgeschrieben (der Manager ist sein einziger Schreiber,
  jeder Append trägt die erwartete Version); ein gescheiterter Append, ein `SchrittUmgeleitet` (seine Log-Zeit zählt) und jede Backstop-Weckung
  lesen frisch;
- durabel (`IProzessMarkingStore`, Marten-Dokument `ProzessMarkingDoc`) nur gedrosselt, Standard alle 32 Weckungen; gelöscht wird es mit dem Ende
  nur, wenn es geschrieben oder geladen wurde;
- **Kaltstart, Backstop oder geänderter `RegelHash` (`ProzessRegelHash`) → Voll-Fold ab 0** (Invariante 1). Der Cache ist nicht-autoritativ;
  ein veralteter Stand kann höchstens erneut feuern, was an der Empfänger-Inbox verpufft. Den Beweis „gleiche Feuer-Entscheidung wie der
  Voll-Fold“ führt `MarkingCursorAequivalenzTests` gegen echtes Marten, auch kalt, mit Router-Weckungen und mit verzögerter Ausführung.

**Zeitlimit und Frist.** Ein offener Aufruf, dessen Limit seit seiner Aktivierung (jüngstes gematchtes Event, DB-Zeit aus dem Log) gegen
`IDbClock` abgelaufen ist, scheitert wie eine Ablehnung. Damit der Ablauf auch im Ruhezustand entdeckt wird, schreibt der Manager die **Frist**
in den Offen-Index (`ProzessOffen.Frist`, PF-4): das früheste offene Zeitlimit, für einen offenen Warte-Knoten spätestens in 15 s (er liest
seinen Strom nur bei einer Weckung). Geschrieben wird nur, wenn sie früher liegt als die zuletzt geschriebene — eine spätere bleibt liegen, die
frühere weckt einmal umsonst und schreibt dann die richtige; weckt der Backstop wegen der Frist (`ProzessWake.FristFaellig`), schreibt der Manager
immer neu (auch `null`), und ein beendeter Vorgang räumt dabei seinen liegengebliebenen Eintrag ab. Genauigkeit: eine Sekunde (Takt des
Backstops), durabel über Neustarts.

**Boot-Guards** (`ProzessManagerStartupService`): Azyklizität (`ProzessAzyklizität.PrüfeAlle`, gespeist aus `GeneratedCommandRouting.Produziert`
und den Ergebnissen der Funktionen), Auslöser-Kollision zwischen Prozessen (fail-fast), jede gerufene Funktion ist gebunden
(`FunktionsExtensions.PrüfeBindungen`).

Offen: Feinschliff an `MarkingKompakt` und an der Kompensation, `KlärungNötig` ohne Test (docs/13 P2-5); Saga-E2E- und
Marking-Äquivalenz-Tests fehlen seit dem Löschen der alten Beispiel-Domänen (docs/13 T-1).

## 4.6 Fluss auf dem Dirigenten und Katalog-Funktionen

**Fluss.** Eine Pipeline ist eine Klasse `: IPipeline` mit `PipelineFluss Fluss => PipelineFluss.Definiere(p => …)`
(`Abstractions/Fluss/Pipeline.cs`). Geschrieben wird vom Draht aus, so wie der Editor zeichnet:
- Quelle: `p.Quelle<T>()` (eine `IQuellNachricht`) oder `p.Auf<E>()` (ein Event aus dem Log) — genau eine je Pipeline;
- `draht.Rufe<F>(λ)` (Katalog-Funktion), `knoten.Bei<E>()` (ein Ausgang), `draht.Sende<Cmd>(λ)` (Command), `draht.Je(…)` / `Sammle(…)`;
- `p.Alle(a, b, …)` (∧), `.Oder(…)` (∨), `.Zeitlimit(t)`;
- Fehler-Ports `knoten.BeiZeitlimit()` (⏳) und `knoten.BeiAbgelehnt()` (✕);
- `quelle.Strom()` (Aggregat-Id und Version der Quelle als Draht) und `strom.Warte<A>().Zeitlimit(t)` (Frist als Rennen).

Beispiel (Sonde, `GraphExtractor.Kern/Sonde/Fluss.cs.txt`):
```csharp
public sealed class Ausleihfrist : IPipeline
{
    public PipelineFluss Fluss => PipelineFluss.Definiere(p =>
    {
        var ausgeliehen = p.Auf<Ausgeliehen>();
        var rueckgabe = ausgeliehen.Strom().Warte<Zurueckgegeben>().Zeitlimit(TimeSpan.FromDays(28));
        var offen = ausgeliehen.Rufe<IOffenZaehlung>(ausgeliehen => new ZaehleOffene(ausgeliehen.LeserId));
        var mahnung = p.Alle(rueckgabe.BeiZeitlimit(), offen.Bei<OffeneGezaehlt>(), ausgeliehen.Strom())
            .Sende<EroeffneMahnung>((rueckgabeZeit, offen, ausgeliehenStrom) => new EroeffneMahnung(ausgeliehenStrom.Id, offen.Anzahl));
    });
}
```
Der Variablenname ist die Knoten-Identität (Wirker `fluss:<Pipeline>#<Knoten>`); weil nur deklarierte Knoten verdrahtet werden können, ist ein
Fluss per Konstruktion azyklisch. Die Form prüft der `FlussAnalyzer` (CQRS073–078).

**Ausführung.** `PipelineFluss.Definiere` übersetzt den Fluss in `ProzessRegeln` mit Knoten-Herkunft (`IstFluss`); der
`ProzessRegelnGenerator` registriert `IPipeline` neben `IProzessDefinition`, und **derselbe** `ProzessManager` führt sie aus — Fold,
Marking-Cursor, Backstop und Zeitlimit wie in §4.5. Welche Tokens eine Regel belegen (Herkunft, Je-Teile, Sammeln), entscheidet der reine
Kern `FlussBelegung` (`Abstractions/Prozess/FlussBelegung.cs`), den auch das `FlussLaufwerk` in `Cqrs.Testing` für Prüfstand-Tests nutzt.
- **Fehler-Ports als Tokens:** ist der ⏳- bzw. ✕-Port eines Knotens verdrahtet, wird ein Zeitlimit bzw. eine Ablehnung nicht zum
  Fehlschlag, sondern im Log als `SchrittUmgeleitet` vermerkt und im Dirigenten zum Token `ZeitlimitAbgelaufen` bzw. `SchrittAbgelehnt`
  (beide `IProzessIntern`, nie im Log) — der Fluss läuft auf dem verdrahteten Zweig weiter.
- **Warte:** ein Warte-Knoten liest den Strom der Quelle nach ihrer Version; das erste erwartete Event vor Ablauf (DB-Zeit) gewinnt, sonst
  entsteht `ZeitlimitAbgelaufen`.
- **Quell-Einstieg:** `QuellEingang.NimmAufAsync` (`Infrastructure/Quellen/Quellen.cs`) hängt eine Quell-Nachricht als erstes Event eines
  Vorgangs-Streams an, dessen Id deterministisch aus Typ und `Kennung` folgt (Version 0 → **genau einmal** im Log, auch bei Neustart oder
  zweitem Knoten), und weckt dann jede Pipeline, die mit `p.Quelle<T>()` beginnt; eine verlorene Weckung heilt der Poll des
  Korrelations-Routers. Gespeist wird er vom fest codierten Treiber (Datei) bzw. vom Webhook aus der Konfig (`docs/konzept-werkbank.md` §3).

**Katalog-Funktionen.** Eine Funktion ist ein Interface `: IFunktion` mit genau einer Methode
`Task<OneOf<E1, …>> RufeAsync(TAuftrag, IAusfuehrung[, Lese-Fähigkeiten])`, `TAuftrag : IAuftrag<F>`, Ergebnisse persistente Events. Der
`FunktionsGenerator` erzeugt den reflexionsfreien Dispatch `GeneratedFunktionen` (CQRS068/069). Prozess (`Rufe<F>`) und Fluss rufen sie gleich:
1. Der Dirigent **bietet** den Auftrag an (`FunktionsAnbieter.BieteAnAsync`) — an den **Vermittler** seiner Funktion, einen virtuellen
   Cluster-Actor je Funktion (`FunktionsVermittlerActor`, Identität = voller Funktionsname). Der Vermittler ist nicht maßgeblich: geht er
   verloren, bietet der Dirigent jeden offenen Auftrag bei der nächsten Weckung neu an.
2. **Abholer** holen per Pull, so viele wie Slots frei sind, mit Lease und Long-Poll: im Host der `FunktionsAbholer` (C#-Implementierung),
   extern eine `FunktionsAnbieterSitzung` über gRPC (generischer Python-Worker), von Hand der `HandAusfuehrer` (Werkbank, JSON-Eingang).
3. Abgeschlossen wird immer über `FunktionsAusfuehrer.SchreibeErgebnisAsync`: **genau ein** Ergebnis im Ausführungs-Stream (Stream-Id =
   Vorgang, Anhängen ab Version 0; rechnen zwei, gewinnt eines). Wirft die Funktion auch nach den Wiederholungen, meldet ein externer
   Ausführer einen Fehler oder ein Ergebnis außerhalb des Vertrags, liegt dort stattdessen eine `KommandoAbgelehnt`-Marke. Danach weckt der Ausführer den Vorgang; der Fold liest das Ergebnis wie
   das Event eines Aggregats.

**Laufort ist Konfig, nicht Code.** Die Umgebungs-Datei sagt je Funktion `Funktionen:<F>:Laufort` (`Host` · `Extern` · `Hand`), dazu
`Implementierung`, `Slots`, `Wiederholungen` (`Abstractions/Umgebung/UmgebungKonfig.cs`). `AddCqrs` bindet daraus beim Start
(`BindeFunktionen`, Fabriken aus `GeneratedFunktionen`), danach die Domänen-Dienste der Implementierungen (`BindeDienste`, `GeneratedDienste`).
Eine Funktion mit Lese-Fähigkeit läuft nur im Host. Details: `docs/konzept-editor-pipelines.md` §14, [10 §10.5a/§10.6](10-entwickler-api.md).

**Altlast: die Handle-Pipeline.** Daneben ist noch die frühere Pipeline-Maschinerie verdrahtet, ohne Domänen-Nutzer: `IPipelineHandler` mit
`PipelineActorBase`, `PipelineEventPullBridge` (persistierte Events über den Pull-Pfad, emittierend), `PipelineStartupService`, die
Trigger-Welt (`IPipelineTrigger`, `ITriggerRegistration`, `TimerTrigger`, `WebhookTrigger`) und `Selbst<T>`/`Frist<TCmd>`/`FristStorno<TCmd>` als
Handle-Ausgang (durabler Fristplaner `FristScheduler`/`MartenFristplan`; er entfernt eine Frist erst bei entschiedenem Emit-Ausgang, ein
unbekannter lässt sie für den nächsten Tick liegen). Sie ist kein Weg für neuen Code; Rückbau offen (docs/13 W11-1).

## 4.7 Dead-Letter & Snapshots

- **Dead-Letter:** `MartenDeadLetterSink` schreibt ein durables `DeadLetter`-Dokument je toter Zustellung (Schema `dlq`), best-effort —
  Beobachtung, kein kritischer Pfad (z. B. ein Vorgang in `KlärungNötig`, ein vergifteter Aggregat-Actor). Lesen: `MartenDeadLetterReadStore`.
- **Snapshots:** `MartenSnapshotStore` — `Snapshot<TState>` als jsonb je Aggregat, Registrierung generiert (`SnapshotRegistrationGenerator`).
  **Nicht-autoritativ:** kein Treffer oder eine andere `SchemaVersion` → Voll-Replay (`AggregateRehydrator`). Die Schema-Version ist der
  Struktur-Hash des States plus der Code-Hash von State und Applier (`{State}AggregateHandler.CodeHash`) — eine geänderte Apply-Logik
  invalidiert alte Snapshots. Geschrieben wird nach einer Schwelle von Events und bei Deaktivierung: der State wird im Actor-Turn
  kopiert (die Inbox reist nicht mit — sie ist eine Tabelle), der DB-Write läuft danach fire-and-forget. Die Kopie ist ein JSON-Round-Trip (docs/13 W10-2).

## 4.8 Spur der Konsumenten

Jeder Log-Eintrag trägt die Header `ursache`, `wirker`, `ueber`, `stand` (`Abstractions/Spur/Spur.cs`, geschrieben in
`Infrastructure/Persistence/SpurHeader.cs`). Die Konsumenten setzen sie per `Spur.Von(…)` um ihren Ausgang; der `CommandEmitter` legt Ursache
und Wirker in den Command-Envelope, das Ziel-Aggregat schreibt mit `wirker = decide:<Aggregat>/<Command>` und dem Absender als `ueber`:
- **Reaktion:** `HandlerOutputRouter` — Ursache = auslösendes Event (`Stream:Version`), Wirker `reaktion:<Typ>`.
- **Prozess / Fluss:** `ProzessManager.FeuereAsync`/`BeauftrageAsync` — Ursache = jüngster echter Match-Token, Wirker `prozess:<P>#<Regel>`
  bzw. `fluss:<P>#<Knoten>` (`ProzessRegeln.Wirker`).
- **Funktion:** die Spur reist im `AuftragAnbieten`; `FunktionsAusfuehrer` schreibt das Ergebnis mit `funktion:<F>`, der Absender steht als
  `ueber`.
- **Quelle:** `QuellEingang` schreibt mit `quelle:<Typ>` und ohne Ursache (Wurzel).
- **Projektion** schreibt nie ins Log; `projektion:<Typ>` ist nur Karten-Id.

Format, Lesen und Ansichten: `docs/konzept-werkbank.md` §7.

## 4.9 Offen

Offene Punkte stehen nur in [13](13-reifegrad-schulden-bewertung.md): Testlücke T-1,
Handle-Pipeline/Trigger-Welt und doppelte DSL W11-1, Zeitlimits über den Backstop W11-2, Marking/Kompensation/
`KlärungNötig` P2-5, hartkodierte Intervalle P2-8, reflektive Serialisierung (Marking-Store, Snapshot-Kopie) W10-2, Projektlage W2-1.
