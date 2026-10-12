# Entscheidungs-Debugger & Test-DSL — Anleitung

> **Gekürzt 2026-10-12** auf das mit dem Zielbild `docs/konzept-werkbank.md` Vereinbare.

Wie du die Test-/Debug-DSL selbst ausprobierst. **Nichts davon braucht Postgres/Consul/Redis** — der ganze
Baustein ist store-frei (er treibt die generierte Domänenlogik ohne Cluster).

> **Umgebungs-Hinweis:** Gebaut mit dem .NET-10-SDK; falls die Runtime meckert, den Befehlen
> `DOTNET_ROLL_FORWARD=LatestMajor ` voranstellen (wie im Rest des Projekts).

## Das Bild in einem Absatz

Es gibt **eine** store-freie Maschine (die generierten Decider/Applier + Saga-Regeln) und darauf
als Fassade das **Test-DSL** (`Gegeben → Wenn → Dann`). Debuggen heißt: einen wertgebundenen Lauf
fahren und sehen, *welchen* Zweig der Decider wählte und *warum*.

---

## 1. Aggregat-Test schreiben (`Gegeben → Wenn → Dann`)

Ein Test lebt in `Infrastructure.Pruefstand.Tests`. Minimalform:

```csharp
using Cqrs.Testing;
using Domain.Konto;
using Infrastructure; // AggregateHandlerFactory

var id = Guid.NewGuid();
Szenario.Für<Konto>(new AggregateHandlerFactory(), id)
    .Gegeben(new KontoEroeffnet(100, Gesperrt: false))   // Historie als Events (Log = Wahrheit)
    .Wenn(new ReserviereBetrag(id, 200))                 // das getestete Command
    .DannAbgelehnt<DeckungReichtNicht>()                 // der erwartete OneOf-Zweig
    .UndZustand(k => k.Saldo == 100);                    // Wirkung (hier: keine)
```

- Zweite Given-Form: `.Vorab(new EroeffneKonto(id, 100))` fährt die Historie als **Commands**
  durch den echten Decider. Eine Ablehnung im Arrange wird laut.
- Weitere Zusicherungen: `.Dann(new BetragReserviert(30))` (exakter Wert), `.Dann<Event>()`,
  `.DannKeineAblehnung()`, `.Trace` (die inspizierbare Reise für eigene Prüfungen).
- **Bei Fehlschlag** druckt die Ausnahme den ganzen Entscheidungs-Bericht (Zustand-vorher →
  gewählter Zweig → nachher) — du siehst sofort, *warum* der Decider so entschied.

Vorlagen zum Abschauen: `Infrastructure.Pruefstand.Tests/Dsl/SzenarioDslTests.cs`.

## 2. Saga-/Prozess-Test schreiben (der Diamant + das halbgefüllte Join)

```csharp
using Cqrs.Testing;
using Domain.Auftrag; using Domain.Konto; using Domain.Ueberweisung;
using Infrastructure;

var quelle = Guid.NewGuid(); var ziel = Guid.NewGuid();

SagaSzenario.Mit(new AggregateHandlerFactory(), new UeberweisungsProzess())
    .Gegeben<Konto>(quelle, new KontoEroeffnet(20, Gesperrt: false)) // deckt die 50 NICHT
    .Gegeben<Konto>(ziel,   new KontoEroeffnet(0,  Gesperrt: false))
    .Wenn(new BeauftrageUeberweisung(Guid.NewGuid(), quelle, ziel, 50))
    .Feuert<ReserviereBetrag>()
    .FeuertNicht<SchreibeGut>()
    .SagaWartetAuf<BetragReserviert>("UeberweisungsProzess"); // ← das halbgefüllte Join
```

Weitere Zusicherungen: `.FeuertInReihenfolge<A, B, C>()`, `.KeineOffeneSaga()`,
`.KeinUnroutedCommand()` (fängt den `UNROUTED-SAGA-CMD`-Hang), `.EndzustandVon<TState>(id, …)`.
Vorlage: `Infrastructure.Pruefstand.Tests/Dsl/SagaDslTests.cs`.

## 3. Die DSL-Tests laufen lassen

Nur die neuen DSL-/Debugger-Tests:

```bash
dotnet test Infrastructure.Pruefstand.Tests/Infrastructure.Pruefstand.Tests.csproj --filter "FullyQualifiedName~Dsl"
```

Der ganze Prüfstand:

```bash
dotnet test Infrastructure.Pruefstand.Tests/Infrastructure.Pruefstand.Tests.csproj
```

Einen echten Entscheidungs-Bericht sehen: schreib in einem Test bewusst eine falsche
Erwartung (z.B. `.Dann<BetragReserviert>()` gegen einen zu großen Betrag) und lies die
Fehlermeldung.

## 6. Was wo liegt

| Baustein | Datei |
|---|---|
| Aggregat-DSL (`Szenario.Für`) | `Cqrs.Testing/Szenario.cs` |
| Inspizierbare Trace + Bericht | `Cqrs.Testing/Trace.cs`, `Cqrs.Testing/Intern.cs` |
| Saga-DSL (`SagaSzenario.Mit`) | `Cqrs.Testing/Saga.cs` |
| Beispiel-Tests | `Infrastructure.Pruefstand.Tests/Dsl/*.cs` |

Der Export einer Board-Session als Test entfällt. Export aus der Spur: offen.

## 7. Grenze (bewusst)

Der Debugger sieht **Fach-Logik und Saga-Choreografie** — nicht Store/Cluster (Co-Commit,
`seq_id`-Lücken, Dedup, cross-node). Das ist Absicht: dieselbe Reinheits-Grenze wie der Fachcode
(Invariante 5). Store-Semantik gehört auf **Ebene 2** (`Infrastructure.Integration.Tests`, echtes
Marten). Der Saga-Modus hier ist der schnelle; der produktionstreue
`ProzessManager` hat seinen eigenen Harness in `Phase5`.
