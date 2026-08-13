# Entscheidungs-Debugger & Test-DSL — Anleitung

Wie du die neue Test-/Debug-DSL und den Board-Debugger selbst ausprobierst. Alles im Branch
`feature/entscheidungs-debugger-dsl`. **Nichts davon braucht Postgres/Consul/Redis** — der ganze
Baustein ist store-frei (er treibt die generierte Domänenlogik ohne Cluster).

> **Umgebungs-Hinweis:** Gebaut mit dem .NET-10-SDK; falls die Runtime meckert, den Befehlen
> `DOTNET_ROLL_FORWARD=LatestMajor ` voranstellen (wie im Rest des Projekts).

## Das Bild in einem Absatz

Es gibt **eine** store-freie Maschine (die generierten Decider/Applier + Saga-Regeln) und drei
Fassaden darauf: das **Test-DSL** (`Gegeben → Wenn → Dann`), der **SimHost/Board** (dieselbe
Ausführung, live und klickbar) und der **GraphExtractor** (die Soll-Karte inkl. der Guard-
Ausdrücke). Debuggen heißt: einen wertgebundenen Lauf auf die Karte legen und sehen, *welchen*
Zweig der Decider wählte und *warum*.

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
  durch den echten Decider (board-nah). Eine Ablehnung im Arrange wird laut.
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

Der ganze Prüfstand (sollte **171/171** grün sein):

```bash
dotnet test Infrastructure.Pruefstand.Tests/Infrastructure.Pruefstand.Tests.csproj
```

Einen echten Entscheidungs-Bericht sehen: schreib in einem Test bewusst eine falsche
Erwartung (z.B. `.Dann<BetragReserviert>()` gegen einen zu großen Betrag) und lies die
Fehlermeldung.

## 4. Das „Warum" (Guards) im Graphen erzeugen und ansehn

Der GraphExtractor hebt je Decider-Zweig den Guard-Ausdruck (`State.Verfuegbar < cmd.Betrag`)
an die Kante. Graph neu erzeugen:

```bash
dotnet run --project GraphExtractor
```

Danach die gehobenen Guards prüfen (34 Zweige tragen ihr „Warum"):

```bash
python3 -c "import json; g=json.load(open('knowledge-graph.json')); [print(n['name'],'→',o['event'],'wenn',o['guard']) for n in g['nodes'] if n.get('command') for o in n['command']['produces'] if o.get('guard')]"
```

## 5. Der Live-Debugger im Browser (SimHost + Board)

Das Board ausführen (aktualisiert `knowledge-graph.html`), dann den SimHost starten:

```bash
dotnet run --project GraphExtractor
```

```bash
dotnet run --project SimHost
```

Öffne **http://localhost:5178/**. Ein `● LIVE`-Badge erscheint, und links erscheint die
Sektion **„Aggregate · angelegte Instanzen"**. Jetzt kannst du:

0. **Ein konkretes Aggregat wählen und beobachten** — im Command-Formular ist das
   `aggregateId`-Feld ein **Instanz-Dropdown**: „➕ neu" legt eine frische Instanz an,
   sonst wählst du eine der schon angelegten (z.B. „Konto #1"). Die Liste zeigt jede
   Instanz mit ihren Werten; ein Klick fokussiert sie in der **Inspektor-Karte**, die nach
   jedem Command die geänderten Felder hervorhebt (alt durchgestrichen → neu grün) und eine
   kleine Änderungs-Historie führt.


1. **Ein Command mit Werten schicken** — im Selektor ein Command wählen, Werte eintragen, ▶.
   Es läuft der **echte** wertabhängige Decider; die getroffene Entscheidung leuchtet auf.
2. **Das „Warum" mit echten Werten sehen** — schick `EroeffneKonto` (Saldo 100), dann
   `ReserviereBetrag` mit Betrag 200. Die Ablehnung zeigt **„DeckungReichtNicht [weil 100 < 200]"**
   — der Guard, gebunden mit den tatsächlichen Werten.
3. **Abdeckung ein-/ausschalten** — Button **▦**: gefeuerte Zweige werden grün markiert,
   nie berührte grau gedimmt. Klick dich durch — dein Modell leuchtet auf, was du getestet hast.
4. **Board-Session als Test exportieren** — Button **🧪 Als Test**: erzeugt den `Szenario.Für<…>()
   …Gegeben/Vorab/Wenn/Dann`-Code deiner Session und kopiert ihn in die Zwischenablage. Einfügen,
   fertig ist der Regressionstest.

### Ohne Browser prüfen (curl)

```bash
curl -s -X POST http://localhost:5178/api/step -H 'Content-Type: application/json' -d '{"sessionId":"c","command":"EroeffneKonto","values":{"aggregateId":"22222222-2222-2222-2222-222222222222","startSaldo":100,"gesperrt":false}}' -o /dev/null
```

```bash
curl -s -X POST http://localhost:5178/api/step -H 'Content-Type: application/json' -d '{"sessionId":"c","command":"ReserviereBetrag","values":{"aggregateId":"22222222-2222-2222-2222-222222222222","betrag":200}}'
```

Der zweite Aufruf enthält im Frame-Text `⃠ ABGELEHNT: DeckungReichtNicht [weil 100 < 200]`.
Die Abdeckung und der DSL-Export:

```bash
curl -s http://localhost:5178/api/coverage
```

```bash
curl -s -X POST http://localhost:5178/api/dsl -H 'Content-Type: application/json' -d '{"sessionId":"c"}'
```

## 6. Was wo liegt

| Baustein | Datei |
|---|---|
| Aggregat-DSL (`Szenario.Für`) | `Cqrs.Testing/Szenario.cs` |
| Inspizierbare Trace + Bericht | `Cqrs.Testing/Trace.cs`, `Cqrs.Testing/Intern.cs` |
| Saga-DSL (`SagaSzenario.Mit`) | `Cqrs.Testing/Saga.cs` |
| Guard-Bindung (`70 < 200`) | `Cqrs.Testing/GuardBinder.cs` |
| Coverage-Sammler | `Cqrs.Testing/Abdeckung.cs` |
| Trace → DSL-Code | `Cqrs.Testing/DslSchreiber.cs` |
| Guards aus Decider-Syntax | `GraphExtractor/DomainExtractor.cs` (`ExtractGuards`) |
| Live-Runtime + Endpunkte | `SimHost/SimEngine.cs`, `SimHost/Program.cs` |
| Board (Canvas, Guard/Coverage/Export) | `GraphExtractor/HtmlPresenter.cs` |
| Beispiel-Tests | `Infrastructure.Pruefstand.Tests/Dsl/*.cs` |

## 7. Grenze (bewusst)

Der Debugger sieht **Fach-Logik und Saga-Choreografie** — nicht Store/Cluster (Co-Commit,
`seq_id`-Lücken, Dedup, cross-node). Das ist Absicht: dieselbe Reinheits-Grenze wie der Fachcode
(Invariante 5). Store-Semantik gehört auf **Ebene 2** (`Infrastructure.Integration.Tests`, echtes
Marten). Der Saga-Modus hier ist der schnelle (SimEngine-nah); der produktionstreue
`ProzessManager` hat seinen eigenen Harness in `Phase5`.
```
