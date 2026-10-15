# 02 — Design-Prinzipien (aus dem Code abgeleitet)

Stand 2026-10-13, gegen den Code geprüft. Maßstab: docs/konzept-werkbank.md.

Dieses Kapitel benennt die Prinzipien, die sich **quer über alle Subsysteme** wiederholen — als im Code belegtes Muster, nicht als
Wunsch. Es ist das „Warum“ hinter der Architektur. Die sechs Invarianten selbst stehen in [01 §1.2](01-ueberblick.md) (und `CLAUDE.md`);
P1–P6 wiederholen sie nicht, sondern zeigen, **warum** sie so geschnitten sind und **wo** der Code sie einlöst. P14 und P15 kommen aus den
Werkbank-Leitsätzen (`docs/konzept-werkbank.md` §2).

## P1 — Alles außer dem Log ist abgeleitet und verwerfbar (Inv. 1)

**Warum:** Wenn nur der Log Wahrheit ist, darf jeder Beschleuniger kaputtgehen, ohne dass ein Ergebnis falsch wird. Jeder Cache hat
deshalb einen Voll-Read-Fallback:
- Der Redis-Version-Index degradiert bei Ausfall zu einer Warnung, der Command-Flow bricht nicht (`RedisVersionTracker`); mit
  `VersionIndex.Betrieb = Aus` in der Umgebung entfällt er ganz (`NullVersionTracker`).
- Snapshots sind best-effort; kein Treffer oder eine andere Snapshot-Version → Voll-Replay (`MartenSnapshotStore`, `AggregateRehydrator`).
- Der Prozess-Marking-Cursor (`MartenProzessMarkingStore`) ist ein Tail-Cursor; bei `RegelHash`-Abweichung oder Kaltstart faltet der
  Manager voll ab 0 (`ProzessManager`).
- Die Aggregat-Rehydration liest immer aus dem Log (`AggregateRehydrator`).

**Konsequenz:** Ein verlorener Redis, ein veralteter Snapshot, ein kaputter Marking-Cursor verschlechtern die *Latenz*, nie die
*Korrektheit*.

## P2 — Mehrere gestaffelte Liveness-Netze statt eines zuverlässigen Zustellwegs (Inv. 2)

**Warum:** Ein zuverlässiger Push-Kanal wäre teuer und trotzdem nicht beweisbar korrekt. Darum ist das Signal billig und verlierbar, und
die Korrektheit kommt aus der Kombination Signal + Poll + Log-Read:
- Das Aggregat publiziert Event und Signal fire-and-forget nach dem Commit (`AggregateActorBase`, `PublishFireAndForget`); ein Verlust ist
  normaler Betrieb (Debug-Level, keine Warnung).
- Der Poll (30 s) rückt seinen Cursor nur vor, wenn jede Weckung als „aufgeholt“ bestätigt zurückkommt (`Poller`).
- Für Prozesse und Flüsse gibt es zusätzlich einen 15-s-Backstop über einen durablen Offen-Index (`ProzessManagerWiring`).

Kein einzelner Zustellweg trägt die Korrektheit allein.

## P3 — Der Typ ist der Schlüssel (Inv. 3)

**Warum:** Ein handgebauter String kann still auseinanderlaufen; ein Typ nicht — der Compiler hält beide Seiten zusammen. Der CLR-Typ ist
deshalb durchgängig der Routing-Schlüssel, in jeder Sprache:
- Aggregat-Identität: `ClusterIdentity.Create(guid, aggregateType)` (`ProtoActorAggregateDispatcher`, `CommandEmitter`).
- Signal-Sharding: über `Payload.GetType()`, die konkrete Signal-Klasse (`BrokerPublisher`, `SignalEnvelope`).
- Command → Aggregat: abgeleitet aus den `IDecider<T>.Decide(TCommand)`-Signaturen (`CommandAggregateMapGenerator`).
- Quellen sind Typen, keine Namen aus der Konfig; der Konfig-Abschnitt `Quellen:<Typ>` folgt dem Typ.
- Python: `type(payload)` → Handler (`cqrs_client/dispatch.py`); Blazor: `Dictionary<Type, …>` + `message.GetType()` (`ClientBus`).

Es gibt **kein** `[Command]`/`[Event]`/`[Aggregate]`-Attribut; Rollen sind Marker-Interfaces. Die vorhandenen Attribute haben je eine
enge Aufgabe und routen nichts:
- `[AggregatName]`, `[ProzessName]` (`Abstractions/IdentitaetsAttribute.cs`) entkoppeln die *persistierte* Identität vom Klassennamen.
- `[RoutingTabelle(Art)]` markiert die generierte Routing-Tabelle, damit der Extractor sie als Code-Fakt findet statt sie an der Syntax zu
  erraten.
- `[ProjectionReader]` steuert das Laden von Read-Model-Abhängigkeiten eines Readers.
- `[assembly: IstClient<IX>]` erklärt eine C#-Assembly zum Client eines Vertrags.
- `[Ingress]` ist Altlast der Handle-Welt, ohne Domänen-Nutzer, Rückbau offen (docs/13 W11-1).

## P4 — Keine Reflection; alles Dispatchende ist generiert und gegen ein Manifest verriegelt (Inv. 4, W10)

**Warum:** Reflection verschiebt Fehler vom Build in den Betrieb und macht Typmengen unsichtbar. Jeder dispatchende Pfad ist deshalb ein
generierter `switch` bzw. eine generierte Tabelle über konkrete Typen, gebunden an einen STJ-Source-Gen-Kontext (`CqrsWireJsonContext`)
statt an `JsonSerializer.Deserialize(type, …)`. Der Trick, der die Kopplung erzwingt: die Generatoren referenzieren
`Ctx.Default.{Typ}` **namentlich** (`WireSerializerGenerator`) — driftet die Typmenge, existiert die Property nicht, und der Build bricht.
Die Kontexte selbst (`CqrsWireJsonContext.g.cs`, `EventJsonSerializerContext.g.cs`) erzeugt der Codegen-Schritt im Build
(`CqrsCodegenPrepass` → `Cqrs.Codegen`).

Das Prinzip gilt ohne Ausnahme. Wo der Code es heute noch verletzt, ist das Schuld, keine Erlaubnis: `Cqrs.Codegen` liest die gebauten
DLLs per `MetadataLoadContext`, `Werkbank/Server/Schema.cs` liest Konfig-Typen per Reflection (docs/13 W10-1); Marten serialisiert per
Default reflektiv, der Snapshot-`Clone` ist ein JSON-Round-Trip, HTTP-Antworten nutzen anonyme Typen (docs/13 W10-2); die Test-DSL in
`Cqrs.Testing` nutzt punktuell Reflection.

## P5 — Die Maschinerie ist unsichtbar (Inv. 5)

**Warum:** Was der Entwickler nicht schreibt, kann er nicht falsch schreiben — und der Fachcode bleibt lesbar als Fachlogik. Alles
Maschinelle liegt im Framework oder im Generat:
- `Id`/`Version`, die `State`-Property und der Konstruktor von Decider/Applier werden generiert (`StateGenerator`, `Generator`).
- Idempotenz und Dedup liegen in Framework-Marken (`KommandoVerarbeitet`, `KommandoAbgelehnt`, `Infrastructure/Aggregate/`), die
  `IProzessIntern` sind — vom Applier übersprungen, in der Version mitgezählt. Aggregate tragen kein Dedup-Feld.
- Ein Prozess ist nur seine Regeln — kein Manager, kein Treiber, kein Korrelations-Code. Beispieldomäne: `Domain/Sammelvorgang/TeilFertigProzess.cs`
  ist ein einziges `p.Auf<…>().Sende<…>(…)`; neutral in der Agnostik-Sonde `GraphExtractor.Kern/Sonde/Gebuehren.cs.txt` (`SchadenBeiRueckgabe`).
- Store-Implementierungen werden im Generat nie genannt; ein Handle bekommt seine Store-Fähigkeiten als Parameter aus einem DI-Bereich
  (`PullPathGenerator`).
- Die Spur ist unsichtbar: jeder Log-Eintrag trägt die Header `ursache`, `wirker`, `ueber`, `stand` (`Abstractions/Spur/Spur.cs`,
  `Infrastructure/Persistence/SpurHeader.cs`); sie reisen fluss-lokal (`Spur.Von`) und im Command-Envelope. Kein Fachcode setzt sie.

## P6 — Der Log hält nur, was jemand durabel braucht (Inv. 6)

**Warum:** Jedes Event im Log ist für immer da und wird bei jedem Replay gelesen. Was niemand durabel braucht, gehört auf den schnellen
Kanal. Der heutige Musterfall ist die Grenze zwischen Quell-Nachricht und Ablehnung:
- Eine **Quell-Nachricht** (`IQuellNachricht`) startet einen Vorgang, auf den ein Fluss durabel wartet — sie wird genau einmal je Kennung
  das erste Event eines Vorgangs-Streams (`StartStream` mit deterministischer Id, `Infrastructure/Quellen/Quellen.cs`).
- Eine **Ablehnung** (`ITransientEvent`) ist die Antwort an den Aufrufer und steht nie im Log; durabel wird nur die Framework-Marke
  `KommandoAbgelehnt` (Inbox-Dedup, Prozess-Fold).
- Die Fluss-Token `ZeitlimitAbgelaufen`, `SchrittAbgelehnt`, `QuellStrom` leben nur im Dirigenten (`Abstractions/Fluss/Pipeline.cs`).
- Signale sind nie persistent.

## P7 — Eine Maschine, keine Taxonomie

**Warum:** Vier Konsumenten-Arten mit eigener Maschinerie wären vier Stellen für dieselben Ordnungs- und Exactly-once-Fehler. Projektion,
Reaktion, Prozess und Fluss teilen deshalb dasselbe Muster (Signal weckt, Poll heilt, geordnet aus dem Log, Fortschritt durabel) auf wenigen
Schleifen: Projektionen im Batch-Konsum (`BatchLeser` je Partition), Emittenten je Stream (`EmittentenAdapter` + `SignalAdapterActor`),
Prozesse und Flüsse im Dirigenten. Der Unterschied fällt aus zwei orthogonalen Achsen, nicht aus einem zweiten Marker:
- **Transport:** `IPullSubscriber` wählt Pull statt Push-Broker.
- **Garantie (Achse B):** `IProjectionTracker` (replaybar, Batch-Co-Commit + Reset) **vs.** `IEmittentenCursor` (emittierend, durabel ab
  Cursor, **kein** Reset). Welche Achse gilt, folgt aus den **Schreib-Fähigkeiten der Handles**: lösen sie einen Store auf, der zugleich
  `IProjectionTracker` ist, ist der Konsument replaybar und läuft im Batch-Konsum, sonst emittierend je Stream (`PullPathGenerator`). Ein Store
  im Konstruktor ist verboten (CQRS054); Schreib-Fähigkeiten aus mehreren Stores sind CQRS052.

Dass ein Emittent nicht zurückgesetzt werden kann, ist compile-zeit-wahr: der Emittenten-Pfad nimmt keinen Tracker, und `IEmittentenCursor`
hat kein Reset — blindes Replay geld-bewegender Emittenten ist unmöglich. Der Co-Commit replaybarer Projektionen (Effekte vieler Streams,
Guards und Partitions-Checkpoint in einer Transaktion) liegt in `Core/MartenCoCommitStoreBase.cs`; der GA-1-Check bricht, wenn eine
append-artige Projektion keinen Co-Commit-Tracker hat.

## P8 — Genau ein Weg, per Analyzer erzwungen (W11)

**Warum:** Zwei Wege für dieselbe Absicht driften auseinander, und jeder Weg muss eigens korrekt sein. Wo es mehrere gäbe, wird einer
festgelegt und der Rest zur Build-Zeit verboten:
- **Ein Emit-Primitiv:** `CommandEmitter.EmitAsync` (deterministische CommandId, `CommandModus.Emittiert`, bounded Token). Ein roher
  `RequestAsync<CommandResult>` außerhalb der Allow-Liste ist **CQRS020**, ein `CancellationToken.None` auf einer Command-Kante **CQRS021**
  (`CommandEmitAnalyzer`).
- Ein Ausgabe-Vertrag: Decide und jedes Handle geben einen konkreten Typ oder `OneOf<…>` konkreter Typen zurück (**CQRS050**).
- Ein Signal je Event, eine Typ-Registry, ein Wire-Kontext, ein Prozess je Auslöser-Event.

Wo der Code heute noch zwei Wege hat (Handle-Pipeline neben Fluss, Prozess- neben Fluss-DSL), steht es in docs/13 W11-1.

## P9 — Fail-fast am Build und beim Start statt still zur Laufzeit (W5)

**Warum:** Ein stiller Laufzeitfehler in einem verteilten System ist teuer zu finden. Analyzer und Generator-Diagnosen (Übersicht in
[05](05-generatoren-analyzer-proto.md)) verwandeln konkrete Stille-Bugs in Compile-Fehler — doppelter Saga-Auslöser, Dangling-Command,
Identitäts-Kollision, unbounded Token, Wire-Drift, offener Ausgabe-Vertrag, Store im Konstruktor, Fluss- und Modell-Regeln, Akteur- und
Client-Verträge, Dienst-Bindung. Im Code steht es wörtlich: „Das war bisher nur ein Kommentar im Generat; jetzt bricht es den Build“
(`CommandAggregateMapGenerator`).

Was erst beim Start feststeht, prüfen Boot-Guards und brechen hart ab (als `BootFehler` mit Karte, gemeldet als `CQRS-BOOT {json}`):
- Umgebung fehlt; unbekannte Abschnitte oder Felder der Umgebungs-Datei (`UmgebungLader`).
- Umgebungs-Siegel: eine Datenbank gehört genau einer Umgebung (`UmgebungsSiegel`).
- Quellen-Konfig: Abschnitt ohne Quellen-Typ, Quelle ohne Abschnitt, fehlender Pfad, Webhook ohne Route/Einblick-Port oder doppelt
  (`QuellenDienst`, `Quellen`).
- Funktions-Bindung: unbekannte Funktion, Laufort Host ohne genau eine Implementierung, Lese-Fähigkeit außerhalb des Hosts
  (`FunktionsExtensions`).
- Dienst-Bindung: fehlende oder mehrdeutige Implementierung, Kreis, fehlender oder unbekannter Konfig-Record bzw. -Feld (`Dienste`).
- Azyklizität des Regelsatzes über Prozesse und Funktionen (`ProzessManagerWiring` → `ProzessAzyklizität`).
- Wire-Check: jeder Wire-Typ ist serialisierbar (`WireSerializerBootCheck`).
- Akteur-Optionen: jeder in der Konfig genannte Akteur existiert als `IAkteur`-Typ (`AkteurOptionen.Pruefe`).
- Cluster-Join mit 30-s-Grenze (`ClusterStartupService`).

## P10 — Scharfe Durabilitätsgrenze

**Warum:** Ein falsches Negativ nach einem erfolgreichen Commit ist schlimmer als jeder Folgefehler — ein Prozess würde einen gelungenen
Schritt kompensieren. Vor dem Append darf ein Command als Fehler quittieren; **nach** dem Append ist Erfolg garantiert. Die
In-Memory-Nebenwirkungen (State-Mutation, Redis, Snapshot, Publish) sind best-effort — wirft eine davon, rehydriert der Actor frisch aus
dem Log und quittiert trotzdem Erfolg (`AggregateActorBase`, Post-Append-`catch`). Der Batch-Task löst erst beim Commit
(`BatchingEventAppender`): Enqueue ist nicht durabel.

## P11 — Ableiten statt deklarieren

**Warum:** Was abgeleitet ist, kann nicht veralten. Topologien und Mengen werden aus Typkanten gefolgert, nicht von Hand gepflegt:
- **Command → Aggregat** aus den Decide-Signaturen (`CommandAggregateMapGenerator`); daraus auch die Command → Event-Kanten für den
  Azyklizitäts-Guard.
- **Snapshot-Version:** Struktur-Hash der State-Form plus Code-Hash von State + Applier (`SnapshotRegistrationGenerator`,
  `AggregateHandlerGenerator`) — eine geänderte Form oder Apply-Logik invalidiert Snapshots automatisch.
- **Hör-Menge eines Akteurs:** mit Vertrag genau dessen Eingänge, sonst die Events der Aggregate seiner Commands (`AkteurRechte`).
- **Verkörpert:** welche Akteure ein Client verkörpert, folgt aus seinen Vertrags-Teilen und den `IDarf`-Haltern — eine Regel für Server,
  Python, Wissensgraph und Editor (`Abstractions.Akteurvertrag.Verkoerpert`).
- **Wirker-Namen** aus Typen und Lambda-Variablen (`Abstractions/Spur/WirkerId.cs`, Regel-Namen aus dem `ProzessRegelnGenerator`).
- Signal-Typen, Wire-Whitelist, Typ-Registry: aus Symbolen abgeleitet.

Die Upcasting-Primitive (`IUpcast`, `EventUpcastingGenerator`) folgt demselben Muster (alt/aktuell aus der Kanten-DAG), ist aber eine
Backend-Primitive für Prod ohne Domänen-Nutzer und kein Editor-Konzept.

## P12 — In-memory testbar durch Seams, Store-Semantik nie gefaked

**Warum:** Logik soll ohne Cluster und DB beweisbar sein, Store-Verhalten nur gegen den echten Store. Jeder Transport-Berührungspunkt ist
ein injizierbarer Delegat (`CommandEmitter.SendeSeam`, `ProzessManager._dispatch`, `Poller._wake`), sodass der Prüfstand die *Logik* ohne
Cluster/DB prüft. Umgekehrt gilt streng: es gibt keinen `InMemoryEventStore`; Store-Semantik wird ausschließlich gegen echtes Marten
getestet. Prinzip: nie faken, was man nicht besitzt. Ebenen und Zahlen: [12](12-tests-und-vermessung.md).

## P13 — Symmetrie über Sprach- und Prozessgrenzen

**Warum:** Die Invarianten sind die Systemsprache, nicht backend-lokal. Jeder Client folgt denselben Regeln:
- **Python:** dasselbe Typ-Dispatch-Muster, dieselbe Reinheit; die Verträge kommen als generierte Basen (`generated/vertraege.py`,
  `generated/funktionen.py`, `generated/funktions_tabelle.py`, `generated/nutzlast_tabelle.py`, erzeugt vom Codegen-Schritt im Build). Der Handshake nennt Vertrag + Hash,
  der Server nimmt die Fähigkeiten aus seiner eigenen Tabelle; `proto_sync.py` ist das Laufzeit-Netz gegen Proto-Drift.
- **Blazor:** der Arbeitsplatz ist ein Client mit Vertrag (`[assembly: IstClient<IArbeitsplatz>]`); der Client-Generator liest den Vertrag
  mit derselben Lesart wie der Server (`Clientvertragslesart`, gleicher Hash), die Analyzer CQRS093–097 halten den Code dagegen.
- Wirksam ist an jeder Leitung Vertrag ∩ Token (Akteur-Tor).

Wo die Python-Lesart noch von der des Servers abweicht, steht es in docs/13 W10-1.

## P14 — Code ist die einzige Wahrheit; gelesen werden nur Code-Fakten (W1, W2)

**Warum:** Ein zweites Modell neben dem Code (Board, Konfig-Namen, Konventionen) läuft auseinander. Es gibt kein gespeichertes
Board-Modell; der Graph entsteht über Roslyn aus Marker-Interfaces, Attributen, Symbolen und Signaturen — nie aus Namenskonventionen oder
Namespace-Raten (Code-Fakt je Primitive: `docs/konzept-werkbank.md` Anhang A.6). Jede Geste im Editor ist ein Patch am Code, jeder Typ der
Domänen-Projekte gehört zu einer Karte. Die Agnostik-Sonde (`GraphExtractor.Kern/Sonde/`, Domäne „Leihwesen“) beweist, dass der Extractor
eine unbekannte Domäne nur aus Code-Fakten liest. Wo Framework und Werkzeuge noch feste Projekt- oder Namespace-Namen kennen: docs/13 W2-1.

## P15 — Prüfen = Bauen (W5)

**Warum:** Ein zweiter Prüfer neben dem Compiler hat eigene Lücken und eigene Fehler. Regeln sind Analyzer, Generator-Diagnosen und
Boot-Guards (P9); einen separaten Validator gibt es nicht. Die Grammatik (`DomainEditor/Grammatik.cs`) ist Daten für ⊕ und `--grammatik`,
jede Regel nennt ihr Build-Gegenstück. Die Werkbank holt die Befunde aus den Analyzern der warmen Compilations und hängt sie an die Karte
(`docs/konzept-werkbank.md` §2, §8.3).

---

## P16 — Clients lesen über typisierte Queries; Nachrichten tragen Pfade, keine Blobs

**Warum:** Ein externer Client (Python-Worker, Blazor) liest nie direkt die Datenbank und keine exportierte Datei, sondern stellt eine
typisierte Query über denselben Kanal wie jeder andere Client. Direktzugriff auf Postgres würde ihn an die physische Marten-Form
(`rm.mt_doc_…`, JSONB) koppeln — eine Schemaänderung bräche ihn still — und bräuchte Datenbank-Zugang auf seiner Maschine; eine Datei wäre
ein zweites Artefakt, das veraltet. So behält der Server die Schema-Hoheit, und der Client bleibt reiner Client. Große Mengen werden
paginiert (Seite/Seitengröße in der Query); echtes Server-Streaming liegt außerhalb des Query-Vertrags und wäre eine eigene Entscheidung.
Events und Queries tragen **Pfade**, keine Binärdaten; die Bytes kommen über einen Datei-Endpunkt. Die Python-Seite fragt mit `query()`
(`Client.Infrastructure.Python/cqrs_client/client.py`), siehe [09](09-python-sdk.md).

## P17 — Reproduzierbarkeit als Event: einfrieren statt verweisen

**Warum:** Was ein späterer Vorgang reproduzierbar nutzen soll, wird als unveränderlicher Stand **ins Log** eingefroren (ein Event, das die
Mitgliedschaft bzw. Eingaben festhält), nicht als Datei daneben abgelegt und nicht über Live-Daten referenziert. Der Auslöser hält die
Version des eingefrorenen Stands und alle Parameter fest; Provenienz ist damit abfragbar wie jedes andere Event, und der Audit-Trail folgt
aus dem Log (Inv. 1). Beispieldomäne: ein Datensatz wird eingefroren, ein Trainingslauf nennt Datensatz und Version.

---

## Wo die Prinzipien (noch) nicht eingelöst sind

Abweichungen vom Anspruch stehen nicht hier, sondern in einer Quelle: [13 §13.0](13-reifegrad-schulden-bewertung.md). Die wichtigsten
Bezüge: Reflection in Werkzeugen und zur Laufzeit (W10-1, W10-2, zu P4), feste Projekt- und Namespace-Namen (W2-1, zu P14), doppelte Wege
(W11-1, zu P8), Ableitungen im Editor-JS (W3-1, zu P11/P14).
