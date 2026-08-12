# 02 — Design-Prinzipien (aus dem Code abgeleitet)

Dieses Kapitel benennt die Prinzipien, die sich **quer über alle Subsysteme** wiederholen —
nicht als Wunsch, sondern als im Code belegtes Muster. Jedes Prinzip ist mit konkreten
Fundstellen unterlegt. Diese Prinzipien sind das eigentliche „Warum" hinter der Architektur.

## P1 — Der Log ist die einzige Wahrheit; alles andere ist abgeleitet und verwerfbar

Jeder Beschleuniger ist nicht-autoritativ und hat einen Voll-Read-Fallback:
- Redis-Version-Index degradiert bei Ausfall zu einer Warnung, der Command-Flow bricht nicht
  (`RedisVersionTracker.cs:96`); optional ganz abschaltbar (`NullVersionTracker`).
- Snapshots sind best-effort; kein Treffer → Voll-Replay (`MartenSnapshotStore.cs`).
- Der Prozess-Marking-Cursor ist ein „prozess-lokales Snapshot-Analogon"; bei
  `RegelHash`-Mismatch oder Kaltstart faltet der Manager voll ab 0 (`ProzessManager.cs`).
- Aggregat-Rehydration liest immer aus dem Log (`AggregateRehydrator`).

**Konsequenz für die Bewertung:** Korrektheit hängt nie an einem Cache. Ein verlorener
Redis, ein stale Snapshot, ein kaputter Marking-Cursor können die *Latenz* verschlechtern,
nie die *Korrektheit*.

## P2 — Das Signal ist nur ein Weckruf (schnell, verlierbar); der Poll ist die Sicherheit

Das Signal `(StreamId, Version)` wird fire-and-forget publiziert (`AggregateActorBase.cs:634`);
Verlust ist *normaler Betrieb* (Debug-Level, nicht Warnung). Die Korrektheit kommt aus der
Kombination Signal + 30-s-Poll + Log-Read. Der Poll-Cursor rückt zudem nur vor, wenn jede
Weckung als „aufgeholt" bestätigt wird (`Poller.cs:52`), und für Prozesse gibt es zusätzlich
einen 15-s-Backstop über einen durablen Offen-Index (`ProzessManagerWiring.cs`). Es gibt also
**mehrere gestaffelte Liveness-Netze**, kein einzelner Zustellweg trägt Korrektheit allein.

## P3 — Routing über Typen, nie über handgebaute Identitäts-Strings

Der CLR-Typ *ist* der Routing-Schlüssel — durchgängig, in jeder Sprache:
- Aggregat-Identität: `ClusterIdentity.Create(guid, aggregateType)` (`AggregateDispatcher.cs:60`).
- Signal-Sharding: über `Payload.GetType()`, die konkrete Signal-Klasse (`SignalEnvelope.cs`).
- Command→Aggregat: abgeleitet aus den `IDecider<T>.Decide(TCommand)`-Signaturen, nicht aus
  Namespaces (`CommandAggregateMapGenerator.cs`).
- Python: `type(payload)` → Handler-Dict, `type(output)` → Kategorie (`dispatch.py`, `registry.py`).
- Blazor: nicht-generische `Dictionary<Type, …>` + `message.GetType()` (`ClientBus.cs`).

Es gibt bewusst **kein** `[Command]`/`[Event]`/`[Aggregate]`-Attribut. Nur zwei *optionale*
Attribute (`[AggregatName]`, `[ProzessName]`) entkoppeln die *persistierte* Identität vom
Klassennamen — für Migrations-Stabilität, nicht fürs Routing.

## P4 — Keine Runtime-Reflection; alles Dispatchende ist generiert und gegen ein Manifest verriegelt

Jeder dispatchende Pfad ist ein generierter `switch`/`Dictionary` über konkrete Typen,
gebunden an einen **STJ-Source-Gen-Kontext** (`CqrsWireJsonContext`) statt an
`JsonSerializer.Deserialize(type, …)`. Der Trick, der die Kopplung *erzwingt*: Generatoren
referenzieren `Ctx.Default.{Typ}` **namentlich** — driftet die Typmenge, existiert die
Property nicht → **Build bricht** (`WireSerializerGenerator.cs:96`). So ist „alles Dispatchende
zur Compile-Zeit erzeugt und gegen das Serialisierungs-Manifest verriegelt" *strukturell*
garantiert, nicht per Disziplin. Ausnahmen (bewusst, pragmatisch): der JSON-Round-Trip-`Clone`
für Snapshots (`AggregateActorBase.cs:584`) und punktuelle Reflection im SimHost — beide
außerhalb des heißen Produktionspfads.

## P5 — Der Fachcode bleibt rein

Der Entwickler schreibt nur Fachlogik. Alles Maschinelle ist unsichtbar:
- `Id`/`Version`, `State`-Property und Konstruktoren werden in Decider/Applier generiert.
- Idempotenz/Dedup/Korrelation liegen in Framework-Marken (`KommandoVerarbeitet`,
  `KommandoAbgelehnt`), die `IProzessIntern` sind — vom Applier übersprungen, in der Version
  mitgezählt. Aggregate tragen **kein** Dedup-Feld (Kommentar `Konto.cs:8`: „REINE Domäne").
- Ein Prozess ist *nur* seine Regeln — kein Manager, kein Treiber, kein Korrelations-Code
  (`ReiseProzess.cs`).
- Store-Impl-Typen werden im Generat *nie* genannt; Konsumenten bekommen ihren Store per DI
  (`PullPathGenerator`: „Store-Impl-Typ wird NIE genannt").

**Eine bemerkenswerte Ausnahme-Schuld:** `Reaktionsempfaenger.VerarbeiteteReaktionen` hält
die Dedup-Menge doch in der Domäne (`Domain/Reaktion/Reaktionsempfaenger.cs`) — das Prinzip
ist hier noch nicht durchgezogen (Domänen-Leak, siehe [13](13-reifegrad-schulden-bewertung.md)).

## P6 — Persistent genau dann, wenn ein durabler Konsument abhängt

Die P6.1/P6.2-Zerlegung der Pipeline ist der Musterfall: persistierte Events laufen über die
durable Pull-Maschine, nur transiente Events (`ITransientEvent`) bleiben auf dem verlierbaren
Broker (`PipelineActorBase.cs`). Ablehnungen sind `ITransientEvent` und nie im Log. Signale
sind nie persistent. Das hält den Log frei von allem, was niemand durabel braucht.

## P7 — Eine Maschine, keine Taxonomie

Die vier durablen Konsumenten teilen `ProjectionAdapter` + `SignalAdapterActor`. Der
Unterschied fällt technisch aus **zwei orthogonalen Achsen**, kodiert über Konstruktor-Stores
und Rückgabetypen — nicht über einen zweiten Marker:
- **Transport:** `IPullSubscriber` wählt Pull statt Push-Broker.
- **Garantie (Achse B):** `IProjectionTracker` (replaybar, Co-Commit + Reset) **vs.**
  `IEmittentenCursor` (emittierend, best-effort, **kein** Reset). Beide gesetzt → Konstruktor
  wirft. Der Generator entscheidet die Achse zur Wiring-Zeit aus den Konstruktor-Argumenten
  (`PullPathGenerator.cs:139`).

Dass ein Emittent nicht zurückgesetzt werden kann, ist **compile-zeit-wahr**: der Reset liegt
auf `IReplaybarerTracker`, den ein Emittenten-Cursor nicht implementiert — „blindes Replayen
geld-bewegender Emittenten ist compile-zeit-unmöglich" (`IEmittentenCursor.cs`).

## P8 — Genau EIN Weg, per Analyzer erzwungen

Wo es mehrere Wege gäbe, wird genau einer festgelegt und der Rest zur Build-Zeit verboten:
- **Ein Emit-Primitiv:** `CommandEmitter.EmitAsync`. Jeder rohe `RequestAsync<CommandResult>`
  außerhalb der Allow-Liste ist **CQRS020** (Fehler); ein `CancellationToken.None` auf einer
  Command-Kante ist **CQRS021** (unbounded Kante = Hang-Klasse).
- Ein Signal je Event, eine Typ-Registry, ein Wire-Kontext, ein Prozess je Auslöser-Event.

## P9 — Fail-fast am Build statt still zur Laufzeit

15 Diagnose-Codes (CQRS001–046, siehe [05 §2](05-generatoren-analyzer-proto.md)) verwandeln
konkrete Laufzeit-Stille-Bugs in Compile-Fehler: doppelter Saga-Auslöser, Dangling-Command,
Identitäts-Kollision, unbounded Token, zyklischer Upcast, Wire-Drift. Dazu Boot-Guards, die
den Start hart abbrechen: `WireSerializerBootCheck` (Serializer-Lücke), Azyklizitäts-Guard
(zyklischer Regelsatz), 30-s-Cluster-Join-Timeout. Explizit im Code: „das war bisher nur ein
Kommentar im Generat; jetzt bricht es den Build" (`CommandAggregateMapGenerator.cs:34`).

## P10 — Scharfe Durabilitätsgrenze

Vor dem Append darf ein Command als Fehler quittieren; **nach** dem Append ist Erfolg
garantiert, und die In-Memory-Nebenwirkungen (State-Mutation, Redis, Snapshot, Publish) sind
best-effort — wirft eine davon, rehydriert der Actor frisch aus dem Log und quittiert trotzdem
Erfolg (`AggregateActorBase.cs:364`). Der Batch-Task löst *erst* beim Commit
(`BatchingEventAppender.cs:19`): Enqueue ≠ durabel.

## P11 — Ableiten statt deklarieren

Topologien werden aus Typkanten gefolgert, nicht von Hand gepflegt:
- **Upcasting:** „frühere vs. aktuelle Version" wird aus der `IUpcast`-DAG *gefolgert* (Quelle
  einer Kante = alt, Blatt = aktuell); der Diskriminator versioniert sich selbst
  (`_v2`, `_v3`) aus der DAG-Position (`EventUpcastingGenerator.cs`).
- **Snapshot-Schema-Version:** FNV-1a-Struktur-Hash über die State-Form — eine Formänderung
  invalidiert Snapshots automatisch (`SnapshotRegistrationGenerator.cs`).
- **Signal-Set, Command→Event-Kanten, Wire-Whitelist:** alle aus Symbolen abgeleitet.

## P12 — In-memory testbar durch Seams

Jeder Transport-Berührungspunkt ist ein injizierbarer Delegat (`CommandEmitter.SendeSeam`,
`ProzessManager._dispatch`, `Poller._wake`), sodass Ebene-1-Tests die *Logik* ohne Cluster/DB
prüfen. Umgekehrt gilt streng: **Store-Semantik wird nie gefaked** — es gibt keinen
`InMemoryEventStore`; sie wird ausschließlich gegen echtes Marten getestet (Ebene 2). Siehe
[12](12-tests-und-vermessung.md). Prinzip: „nie faken, was man nicht besitzt".

## P13 — Symmetrie über Sprachgrenzen

Das Python-SDK ist eine bewusste, architekturtreue Portierung des C#-Clients: dieselben vier
Nachrichtenklassen auf einem bidirektionalen Stream, derselbe Typ-Dispatch, dieselbe
Reinheit, ein gemeinsamer `domain.proto`-Vertrag mit Hash-Verifikation gegen Drift
(`proto_sync.py`). Der Blazor-Client wiederholt dasselbe Muster (Redux/Flux, ein Bus, ein
Signal, generiertes Wiring). Die Invarianten sind **nicht** backend-lokal — sie sind die
Systemsprache.

---

## Wo die Prinzipien (noch) nicht eingelöst sind

Ehrlichkeit als Teil der Bewertungsgrundlage — diese Punkte widersprechen dem Anspruch und
sind in [13](13-reifegrad-schulden-bewertung.md) priorisiert:

| Prinzip | Verletzung |
|---|---|
| P1/Exactly-once | `MartenProjectionTracker` macht **keinen** echten Co-Commit (getrennte Session, at-least-once) |
| P5 (Reinheit) | `Reaktionsempfaenger`-Dedup-Menge lebt in der Domäne (unbegrenzt wachsend) |
| P4 (reflexionsfrei) | Snapshot-`Clone` per JSON-Round-Trip; SimHost nutzt punktuell Reflection |
| P8/P9 (ein Weg, fail-fast) | Der generierte Pull-Emit-Pfad reicht `CancellationToken.None` durch — CQRS021 greift dort syntaktisch nicht |
| P11 (ableiten) | `CqrsWireJsonContext` und `domain_registry.py` sind hand-gepflegt (Boot-Check/Compile-Guard fangen Vergessen ab) |
