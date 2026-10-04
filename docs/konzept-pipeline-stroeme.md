# Konzept — Schritte sind Actors

> Status: Konzept (2026-10-01), nicht umgesetzt. Die heutigen Pipelines sind nur Beispiele.
> Ersetzt die erste, zu schwere Fassung (vier Achsen, Marker-Familie) — bewusst auf das Actor-Bild reduziert.

## Das ganze Modell

**Ein Schritt ist ein Actor. Die Nachricht sagt, an welchen.**

```csharp
public record BildEingegangen(string PairKey, string Pfad) : IFaktum
{
    public string Id => PairKey;          // ← an welchen Actor
}

public class Vorverarbeitung : ISchritt
{
    public OneOf<MeldeBildVerfuegbar> Handle(BildEingegangen m) { … }
}
```

Genau wie beim Aggregat (`Command.AggregateId` → Aggregat-Actor) — nichts Neues zu lernen.

## Zwei Fragen, mehr nicht

**1. Welche Id?** — daraus folgt Ordnung und Parallelität, wie bei jedem Actor:

| Id | Verhalten |
|---|---|
| gleiche Id | nacheinander (eine Mailbox) |
| verschiedene Ids | parallel, über alle Knoten verteilt |
| immer derselbe Wert | alles strikt nacheinander |
| eindeutig je Nachricht | voll parallel |

**2. Darf die Nachricht verloren gehen?**

| | Mailbox | Garantie |
|---|---|---|
| `: IFaktum` | liegt im Log | überlebt Absturz, wirkt genau einmal, Reihenfolge je Id |
| sonst | normale Actor-Mailbox | schnellstmöglich, kann bei Absturz verloren gehen |

## Hochverfügbarkeit

Ein virtueller Actor gehört keinem Knoten. Fällt ein Knoten aus, wacht der Actor beim nächsten
Eingang auf einem anderen auf. Ist seine Mailbox der Log (`IFaktum`), liegt dort noch alles,
was er nicht verarbeitet hatte. Kurze Pause, kein Verlust.

## Was der Entwickler nicht sieht

Dass eine haltbare Mailbox ein Stream im Log ist, dass Signal + Poll den Actor wecken, dass eine Marke
den Fortschritt hält, dass Command-Ids deterministisch sind. Das ist die bestehende Pull-Maschine
(`SignalAdapterActor`, `ProjectionAdapter`, `Poller`, `CommandEmitter`) — heute nur für Aggregat-Streams
genutzt, künftig für jede haltbare Mailbox.

Wie viele Actors **gleichzeitig rechnen** dürfen (CPU/GPU), stellt der Host ein — nicht der Schritt.

## Ehrliche Grenzen (drei Sätze)

- Eine Id ist nie parallel zu sich selbst — ein Schlüssel mit sehr viel Last bleibt ein Engpass.
- „Genau einmal" gilt für die Wirkung; der Handler kann nach einem Absturz erneut laufen.
- Externe Aufrufe (HTTP, ML-Dienst) bekommen eine stabile Ausführungs-Id; genau einmal nur, wenn das Ziel sie nutzt.

## Was dafür gebaut werden muss

1. `ISchritt` + `Id` an der Nachricht → Generator erzeugt den Actor-Kind (wie beim Aggregat).
2. Haltbare Mailbox: `IFaktum` wird an den Stream der Id angehängt, dann weckt die Pull-Maschine den Actor.
3. Host-Setting für gleichzeitige Ausführungen je Knoten.

Der heutige Pipeline-Actor (eine feste Identität, synchrones `PipelineAck`) entfällt dadurch.
