# DDD-Muster — Showcase & Referenz

> Eine lauffähige, getestete Referenz der **taktischen DDD-Bausteine** im Idiom dieses
> Frameworks — auf ZWEI Ebenen:
> 1. **Integriert (echter Framework-Pfad):** `Domain/Verkauf/` — ein echtes Aggregat über
>    Decider/Applier, die GENERIERTE `AggregateHandlerFactory`, Proto- und Wire-Serialisierung.
> 2. **Pur (store-frei):** `Domain.DddPatterns/` — die framework-orthogonalen Muster
>    (Specification, Domain Service, Saga, Repository) als reine Domäne.
>
> Tests: `Infrastructure.Pruefstand.Tests/Ddd/` (Ebene 1, Prüfstand) — **gültig und performant** belegt.

## Integriert: `Domain/Verkauf/` — durch die echte Pipeline

Das Aggregat `Verkaufsauftrag` läuft NICHT isoliert, sondern durch den vollen Framework-Pfad
(wie Konto): `EroeffneVerkaufsauftrag`/`FuegePositionHinzu`/… → Decider (Invarianten,
`OneOf`-Ablehnungen) → generierte `AggregateHandlerFactory` → Applier. Die Commands/Events und
das **Value Object `Geldwert`** reisen real über den generierten DTO-/Proto-/Wire-Pfad — der
Proto-Generator (`dotnet run --project Proto.SourceGeneration`) wurde ausgeführt und `domain.proto`
neu erzeugt.

| Baustein | Beleg |
|---|---|
| **Value Object** | `Verkauf/Wertobjekte.cs` → `Geldwert` (unveränderlich, selbstvalidierend im init-Accessor, Arithmetik mit Währungs-Guard — Verhalten IM Objekt) |
| **Entity im Aggregat** | `Auftragsposition` (Identität = ArtikelNr) |
| **Aggregate Root + Invarianten** | `Verkaufsauftrag` (Kreditlimit/Status/Währung/Mengen-Merge; `Gesamtsumme` O(1)) |
| **Domain Event** | `Verkauf/Events.cs` (persistent + `ITransientEvent`-Ablehnungen) |
| **Decider/Applier** | reine Entscheidung + einzige Zustandsmutation, Framework-Idiom |

Test: `Ddd/VerkaufAggregatTests` (7 Tests, über die generierte Factory).

### Dabei behobener Framework-Bug (Generator)

`Geldwert` ist das erste Value Object, das von **mehreren** Messages geteilt wird. Das legte
einen latenten Generator-Bug offen: der `DomainGraphAnalyzer` hängt an einen mehrfach
erreichten Typ den Marker `" (Ref)"` an, und der `TypeAggregator` registrierte ihn fälschlich
als eigenen Typ `X (Ref)` → der `DtoMapper` erzeugte kaputten Code (`Map X (Ref)…`). Behoben an
zwei Stellen (der Marker ist ein Graph-Hinweis, kein Typname):
- `Core.SourceGeneration/TypeAggregator.cs` — Suffix beim Typschlüssel normalisieren.
- `Infrastructure.SourceGeneration/DtoMapperGenerator.cs` (`GetSimpleTypeName`) — Suffix beim
  Ableiten des C#-Bezeichners strippen (Feld-Mapping mit zwei gleichtypigen VO-Feldern).

Neue Framework-Typen brauchen zudem je eine Zeile in den handgepflegten STJ-Manifesten
(dokumentiert): `CqrsWireJsonContext` (Commands/Events/Signale) + `EventJsonSerializerContext`
(persistente Events).

### Reinheit der Domäne (Invariante 5)

`Domain/Verkauf/` weiß **nichts** über Serialisierung, Proto, Wire, DTO-Mapper, Marten oder
Redis — die einzige Kopplung ist `using Abstractions;` (die Marker-Verträge `IState`/`IEvent`/
`ICommand`/`IDecider`/`OneOf`). Das Value Object `Geldwert` trägt sein Verhalten als echte
Methoden im Objekt. Als der DTO-Mapper zunächst kaputten Code erzeugte, lag die Ursache im
Generator (`" (Ref)"`-Marker) — und **dort** wurde sie behoben. Die Domäne wurde NICHT an den
Serializer angepasst; sie bleibt rein.

## Pur: `Domain.DddPatterns/`

## Warum dieses Projekt existiert

Der Framework-Kern demonstriert die *transportierten* Bausteine (Aggregat via generierter
Handler-Factory, Prozess/Saga durable über den Event-Regel-DAG). Was bislang **nicht** als
zusammenhängende Referenz existierte, sind die reinen taktischen Modellierungs-Muster —
Value Object, Entity, Domain Service, Specification, Factory, Repository. Dieses Projekt
schließt genau die Lücke: jedes Muster einmal sauber, mit Gültigkeits- **und**
Performance-Test.

## Bewusste Isolation (warum ein eigenes Projekt)

`Domain.DddPatterns` referenziert **nur** `Abstractions` (Marker `IState`/`OneOf`) und wird
von `Infrastructure` **nicht** referenziert. Damit sieht der `DtoMapper`/Proto-Generator
seine Typen nie — die Muster bleiben vom fragilen Proto-Zwang entkoppelt (jedes neue
`ICommand`/`IEvent` in `Domain/` bricht sonst den Infrastructure-Build über CQRS030). Der
Showcase bleibt so eigenständig grün, ohne `domain.proto` anzufassen.

## Die Muster — Datei → Test

| DDD-Baustein | Umsetzung | Kern-Beleg |
|---|---|---|
| **Value Object** | `Gemeinsam/Geld.cs`, `Prozent.cs`, `Emailadresse.cs` | unveränderlich, selbstvalidierend, gleich per Wert, Verhalten im Objekt (Arithmetik mit Währungs-Guard); 1M Additionen korrekt & schnell |
| **Entity** | `Bestellung/Bestellposition.cs` | Identität statt Wertgleichheit (ArtikelNr); nur über die Wurzel erreichbar |
| **Aggregate Root** | `Bestellung/Bestellung.cs` | Konsistenzgrenze; Invarianten (Kreditlimit, Offen-Status, Mengen-Verschmelzung); Gesamtsumme O(1) via laufendem Saldo |
| **Domain Event** | `Bestellung/Ereignisse.cs` | Fakten in Vergangenheitsform; einzige Zustands-Mutation läuft über sie (event-sourced) |
| **Factory** | `Bestellung.Eroeffne` + `Bestellung.AusHistorie` | Erzeugung mit Invariante + Rekonstitution aus der Historie |
| **Repository** | `Repository/IRepository.cs`, `EventSourcedRepository.cs` | Illusion einer Aggregat-Sammlung; Replay beim Laden, Append beim Speichern, optimistische Nebenläufigkeit |
| **Specification** | `Spezifikation/Spezifikation.cs`, `KundenSpezifikationen.cs` | atomare Regeln + `Und`/`Oder`/`Nicht`-Kombinatoren; komponierte Regel = Fachsprache |
| **Domain Service** | `Dienste/WechselkursDienst.cs` | zustandslose Operation, die keinem Aggregat gehört (Währungsumrechnung) |
| **Saga / Process Manager** | `Prozess/BestellAbwicklungsSaga.cs` + `MiniAggregate.cs` | Konsistenz über zwei Aggregate mit **Kompensation** bei Fehlschlag des zweiten Schritts |

Test-Dateien: `ValueObjectTests`, `SpezifikationTests`, `AggregatUndRepositoryTests`,
`DomainServiceUndSagaTests` — **29 Tests, Teil der 155/155 des Prüfstands**.

## Performance-Ansatz

Die Perf-Tests prüfen mit **großzügigen Wall-Clock-Budgets** (kein harter Durchsatz-Assert,
der unter paralleler Test-Last flackert). Sie fangen katastrophale Regressionen — z. B.
würde ein versehentliches O(N²) beim Aggregat-Aufbau (20 000² = 400 M Ops) die Budgets
sofort sprengen; der laufende Saldo hält es O(1). Gemessen liegen alle weit unter Budget
(1 M Value-Object-Additionen und 500 k komponierte Spezifikations-Auswertungen je deutlich
unter einer Sekunde).

## Build & Test

```bash
dotnet test Infrastructure.Pruefstand.Tests/Infrastructure.Pruefstand.Tests.csproj
```

> **Umgebungs-Hinweis:** .NET 9 ist seit Mai 2026 EOL und aus den Paket-Feeds entfernt. Die
> Muster wurden mit dem **.NET-10-SDK** gebaut und per `DOTNET_ROLL_FORWARD=LatestMajor` auf
> der .NET-10-Runtime ausgeführt (net9.0-Targets bleiben unverändert). Ergebnis: **155/155
> grün** (126 Bestand + 29 neu), dreimal stabil.
