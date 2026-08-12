# 10 — Entwickler-API: „Wie schreibe ich X?"

Praktischer Leitfaden aus Entwicklersicht. Grundregel überall: **du schreibst nur Fachcode;
alles Dispatchende, Verdrahtende und Serialisierende wird generiert.** Marker-Interfaces sind
der primäre Auslöser — es gibt fast keine Attribute.

## 10.1 Trigger-Matrix (was löst welchen Generator aus?)

| Du schreibst… | …und es entsteht (generiert) |
|---|---|
| `class X : IState` + nested `Decider : IDecider<X>` + `Applier : IApplier<X>` | State-Properties (Id/Version/State), AggregateHandler, Factory-Eintrag, Actor, ClusterKind, Snapshot-Registrierung |
| `record E : IEvent` | Signal `StateChangeViaE`, EventJson-Pfad, Wire-Payload, Typ-Registry, Proto-DTO |
| `record E : IEvent, ITransientEvent` | wie Event, **aber kein Signal, kein Persist, kein Upcasting** |
| `record C : ICommand` | Command-Routing (via Decider-Signatur), Wire-Payload, Proto-DTO |
| `class : IProzessDefinition { … Prozess<E>.Definiere(…) }` | Regel-Registry-Eintrag + CQRS001/002/003-Prüfung |
| `class : ISubscriber` + `Handle(...)` | Subscriber-Dispatch, ProjectionQueryService |
| `class : ISubscriber, IPullSubscriber` | zusätzlich kompletter Pull-Pfad (Adapter-Kind, Store per DI) |
| `class : IPipelineHandler` + `Handle(...)` | Pipeline-Dispatch + Pipeline-Actor |
| `class : IReader<T>` + `Handle(...)` | Reader-Dispatch (Query-Routing) |
| `record V1 {}` + `class : IUpcast<V1, VAktuell>` | ganze Upcasting-Kette + Diskriminator + Marten-Registrierung (rein aus Typen) |
| `class Msg : IWireMessage` | Wire-Whitelist-Eintrag (cross-node) |
| Blazor: `IViewModel`, `IUiModule`, `Handle(T, MessageContext)` | Client-Dispatch, VM-Bus-Methoden, DI-Wiring |

Nur **zwei optionale Attribute**: `[AggregatName("…")]`, `[ProzessName("…")]` — entkoppeln die
persistierte Identität vom Klassennamen (Migrations-Stabilität). Kein `[Command]`/`[Event]`.

## 10.2 Ein neues Aggregat (Command + Event)

Siehe [03 §3.2](03-schreibseite.md) für das vollständige `Konto`-Beispiel. Kurz:
```csharp
public partial class Konto : IState { public decimal Saldo { get; set; } }
public record EroeffneKonto(Guid AggregateId, decimal Start) : ICommand, ICreationCommand;
public record KontoEroeffnet(decimal Start) : IEvent;
public partial class Konto {
  public partial class Decider : IDecider<Konto> {
    public IEnumerable<OneOf<KontoEroeffnet>> Decide(EroeffneKonto c) { yield return new KontoEroeffnet(c.Start); }
  }
  public partial class Applier : IApplier<Konto> {
    public void Apply(KontoEroeffnet e) => State.Saldo = e.Start;
  }
}
```
**⚠ Nach neuen Command/Event/Query/Trigger-Typen:** Proto regenerieren —
`dotnet run --project Proto.SourceGeneration` → ProtoRepo bauen → Infrastructure baut (Signale
sind ausgenommen). Sonst Build-Fehler (CQRS030 macht ihn laut).

## 10.3 Eine Projektion

`partial class X : ISubscriber, IPullSubscriber`, mit `SubscriberId` und einem
tracker-fähigen Write-Store im Ctor (→ automatisch **replaybar**):
```csharp
public async Task Handle(BetragReserviert evt, IAggregateEnvelope env, ProjectionWriter writer) =>
    await writer.Execute(env.AggregateId, ctx => {
        ctx.Track<Konto>(env.AggregateId);
        return _store.UpsertAsync(env.AggregateId, evt.Betrag);
    });
```
Alles Weitere (Kind, Receiver, Poller, Achsen-Schnitt) generiert `PullPathGenerator`. Optionaler
GA-1-Marker `IAppendProjektion` erzwingt einen Co-Commit-Tracker. Beispiel:
`Domain.Projections/ImagePairProjection.cs`.

> Hinweis: der produktive `MartenProjectionTracker` committet Effekt und Marke aktuell in
> getrennten Sessions (at-least-once) — siehe [04 §4.3](04-konsum-und-prozess-maschine.md).

## 10.4 Eine Reaktion (Command emittieren)

Dieselben Marker (`ISubscriber, IPullSubscriber`), **kein** tracker-fähiger Store → automatisch
**emittierend**. `Handle` gibt `IAsyncEnumerable<OneOf<TCmd>>` zurück und `yield return`t
Commands:
```csharp
public async IAsyncEnumerable<OneOf<WirkeReaktion>> Handle(ImagePairKomplett evt, IAggregateEnvelope env) {
    yield return new WirkeReaktion(env.AggregateId);
}
```
Nie selbst `RequestAsync` aufrufen (CQRS020) und nie `CancellationToken.None` auf eine
Command-Kante geben (CQRS021). Beispiel: `Domain.Projections/ImagePairReaktion.cs`.

## 10.5 Ein Prozess / eine Saga

`sealed class X : IProzessDefinition` mit `ProzessRegeln Regeln => Prozess<TAuslöser>.Definiere(…)`.
Das ist **alles** — kein Manager, kein Treiber, kein Korrelations-Code:
```csharp
public sealed class UeberweisungsProzess : IProzessDefinition {
  public ProzessRegeln Regeln => Prozess<UeberweisungBeauftragt>.Definiere(p => {
    p.Auf<UeberweisungBeauftragt>().Sende<ReserviereBetrag>(e => new ReserviereBetrag(e.Von, e.Betrag))
                                   .RückgängigDurch<GebeFrei>(e => new GebeFrei(e.Von, e.Betrag));
    p.Auf<BetragReserviert>().Sende<SchreibeGut>(…);
    // Diamant: .Und<E2>().Und<E3>() ;  Fan-out: .UndAlle<E>(n) + SendeJe<Cmd>
  });
}
```
Build-Guards: CQRS001 (ein Auslöser je Prozess), CQRS002 (jeder gesendete Command hat einen
Decider), CQRS003 (explizites `T`), CQRS012 (eindeutiger Name), Azyklizitäts-Boot-Guard. Der
Host ruft einmalig `AddGeneratedProzesse()`. Beispiele: `Domain/Ueberweisung/`,
`Domain/Reiseauftrag/` (Diamant + Kompensation), `Domain/Sammelueberweisung/` (Fan-out).

## 10.6 Eine Pipeline

`partial class X : IPipelineHandler` mit `PipelineId` + `Handle(TTrigger, PipelineContext)` /
`Handle(TEvent, PipelineContext)`, `yield`t `ICommand`. Persistierte Events laufen über den
Pull-Pfad, transiente über den Broker (P6.1/P6.2). Beispiel:
`Domain.Pipeline/ImageProcessing/ImageProcessingPipeline.cs`.

## 10.7 Schema-Evolution (Upcasting)

Alte Version als Record behalten, eine `IUpcast`-Kante zur neuen Gestalt deklarieren:
```csharp
public record LagerEingerichtet_v1(int Bestand) : IEvent;          // alt, nur lesbar
public record LagerEingerichtet(int AnfangsBestand) : IEvent;      // aktuell
public class LagerUpcast : IUpcast<LagerEingerichtet_v1, LagerEingerichtet> {
    public LagerEingerichtet Aufwerten(LagerEingerichtet_v1 alt) => new(alt.Bestand);
}
```
Der Generator folgert Richtung + Diskriminator (`_v2`) aus der DAG; alte Bytes behalten ihren
Basisnamen (keine Migration). Guards CQRS040–046. **1:N-Split ist aktuell per CQRS046 blockiert**
(Consumer-Fabric fehlt).

## 10.8 Eine neue Blazor-UI (Modul)

Siehe [08 §8.7](08-frontend-blazor-client.md). Kurz: Modul-Klasse (`IStageModule`/`ISidebarModule`/
…) + `.razor`-View (`@inject Store`/`EffectScope`) + Store (`StoreBase`, `Handle` = Reducer) +
optional Refresh-/IntentHandler. Commands/Queries/Events kommen aus den Backend-Assemblies; das
Wiring ist generiert.

## 10.9 Ein Python-Worker

Siehe [09 §9.5](09-python-sdk.md). Kurz: State-dataclass + `CqrsClient[State]` erben + Handler mit
Type-Hints (`async def on_x(self, event: XDto, ctx, state)`, `yield`t Commands) +
`client.run(host, port)`.

## 10.10 Einen Test schreiben

Siehe [12 §12.7](12-tests-und-vermessung.md). Kurz — **Ebene 1 (Prüfstand, store-frei)** über die
generierte Factory:
```csharp
var konto = new Konto();
var h = new AggregateHandlerFactory().CreateHandler(konto);
foreach (var e in h.HandleCommand(new EroeffneKonto(Guid.NewGuid(), 20))) h.ApplyEvent(e);
var evs = h.HandleCommand(new ReserviereBetrag(konto.Id, 30));
evs.Should().ContainSingle().Which.Should().BeOfType<DeckungReichtNicht>();
```
**Store-Semantik gehört nach Ebene 2** (echtes Marten) — nie faken. Es gibt keinen
`InMemoryEventStore`.

## 10.11 Die Reinheits-Grenze — was NICHT im Fachcode steht

Bewusst unsichtbar (Framework/Generat): `Id`/`Version`, `State`-Property + Ctor, OCC, Signal,
Cursor, Exactly-once/Dedup (`KommandoVerarbeitet`-Marken), Sharding, Prozess-Maschinerie,
Serialisierung, Proto-DTOs, ClusterKinds, DI-Wiring. Wenn eines davon in deinem Fachcode
auftaucht, ist etwas falsch verdrahtet.
