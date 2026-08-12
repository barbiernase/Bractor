# DDD-Muster — Showcase & Referenz

> Eine lauffähige, getestete Referenz der **taktischen DDD-Bausteine** im Idiom dieses
> Frameworks. Projekt: `Domain.DddPatterns/`. Tests: `Infrastructure.Pruefstand.Tests/Ddd/`.
> Alles store-frei (Ebene 1, Prüfstand) — **gültig und performant** belegt.

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
