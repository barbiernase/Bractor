# DDD-Muster — Showcase & Referenz

> Eine lauffähige, getestete Referenz der **taktischen DDD-Bausteine** im Idiom dieses
> Frameworks — **integriert und rein**, alles in `Domain/`, keine isolierte Parallelwelt.
> Tests: `Infrastructure.Pruefstand.Tests/Ddd/` (Ebene 1, Prüfstand) — **gültig und performant** belegt.

## Leitlinie

Die Muster laufen durch den **echten** Framework-Pfad (Decider/Applier, generierte
`AggregateHandlerFactory`, Proto-/Wire-Serialisierung), und die Domäne bleibt **rein**: sie
weiß nichts über Serialisierung, Proto, Marten oder Redis. Wo das Framework einen Baustein
bereits nativ trägt (Saga, Repository), ist das native Pendant die Referenz — kein Spielzeug
daneben.

## Wo jeder Baustein lebt

| DDD-Baustein | Ort | Kern |
|---|---|---|
| **Value Object** | `Domain/Verkauf/Geldwert.cs` + `Geldwert.Verhalten.cs` | `partial record : IWertobjekt`; Verhalten auf DEMSELBEN Typ (kein Extra-Typ, kein Attribut); benannte Fabriken `Euro`/`Null`/`Von`; Normalisierung in der Konstruktion; Verhalten `Plus`/`Mal`/`GroesserAls`/`KleinerAls` mit Währungs-Guard |
| **Entity** | `Domain/Verkauf/Wertobjekte.cs` → `Auftragsposition` | Identität = ArtikelNr; nur über die Wurzel erreichbar |
| **Aggregate Root + Invarianten** | `Domain/Verkauf/Verkaufsauftrag.cs` | Konsistenzgrenze; Kreditlimit/Status/Währung/Mengen-Merge; `Gesamtsumme` O(1) |
| **Domain Event** | `Domain/Verkauf/Events.cs` | persistent + `ITransientEvent`-Ablehnungen |
| **Factory** | `Domain/Verkauf/Geldwert.Verhalten.cs` (VO-Fabriken); Erzeugung via Creation-Command | benannte, normalisierende Erzeuger |
| **Decider / Applier** | `Domain/Verkauf/Decider.cs`, `Applier.cs` | reine Entscheidung (`OneOf`-Ablehnungen) + einzige Zustandsmutation |
| **Specification** | `Domain/Spezifikation/Spezifikation.cs` (generisch) + `Domain/Verkauf/Kunde.cs` (Beispiel) | `Und`/`Oder`/`Nicht`-Kombinatoren; komponierte Regel = Fachsprache |
| **Domain Service** | `Domain/Verkauf/WechselkursDienst.cs` | zustandslose Operation über `Geldwert`, keinem Aggregat zugehörig |
| **Saga / Process Manager** | *nativ:* die Prozess-Maschine (`Domain/**/…Prozess.cs`, Event-Regel-DAG) | durable Orchestrierung + Kompensation — `docs/architektur/03-prozess-maschine.md` |
| **Repository** | *nativ:* der Marten-Event-Store (`Infrastructure/Persistence/MartenEventStore.cs`) | Aggregat = Fold seiner Events; Laden per Replay, Speichern per Append |

Test-Dateien: `Ddd/VerkaufAggregatTests` (Aggregat/Entity/VO/Events über die generierte
Pipeline), `Ddd/GeldwertTests` (Value Object direkt), `Ddd/SpezifikationTests`,
`Ddd/WechselkursDienstTests`.

## Value-Object-Form (`partial record` + `IWertobjekt`)

Der Wert ist ein reiner Record; Erzeuger und Verhalten liegen als zweite `partial` auf
demselben Typ — **kein separater Operationen-Typ, kein Attribut, keine Extension-Klasse**. Der
Call-Site spricht Fachsprache:

```csharp
var kreditlimit = Geldwert.Euro(1000);
var prospektiv  = summe.Plus(preis.Mal(menge));
if (prospektiv.GroesserAls(kreditlimit)) yield return new KreditlimitUeberschritten(prospektiv, kreditlimit);
```

Konstruktion läuft nur typ-intern (Fabriken); im übrigen Fachcode steht **kein**
`new Geldwert(...)` — die Kapsel-Grenze ist der Typ selbst. Der leere Marker `IWertobjekt`
(analog `IState`/`IReadModel`) macht alle Wertobjekte **compile-time auffindbar**: ein späterer
Analyzer kann daraus die Kapsel erzwingen (`new`/`with` nur im Typ) oder die Wertobjekt-
Landkarte darstellen — reflexionsfrei, ohne dass der Fachcode etwas davon weiß.

## Reinheit der Domäne (Invariante 5)

`Domain/Verkauf/` und `Domain/Spezifikation/` wissen **nichts** über Serialisierung, Proto,
Wire, DTO-Mapper, Marten oder Redis — die einzige Kopplung ist `using Abstractions;` (die
Marker-Verträge `IState`/`IEvent`/`ICommand`/`IDecider`/`IWertobjekt`/`OneOf`). Als der
DTO-Mapper zunächst kaputten Code erzeugte, lag die Ursache im **Generator** — und dort wurde
sie behoben, nicht in der Domäne.

### Dabei behobener Framework-Bug (Generator)

`Geldwert` ist das erste Value Object, das von **mehreren** Messages geteilt wird. Das legte
einen latenten Generator-Bug offen: der `DomainGraphAnalyzer` markiert einen mehrfach
erreichten Typ mit dem Suffix `" (Ref)"`, und der `TypeAggregator` registrierte ihn fälschlich
als eigenen Typ `X (Ref)` → der `DtoMapper` erzeugte kaputten Code (`Map X (Ref)…`). Behoben an
zwei Stellen (der Marker ist ein Graph-Hinweis, kein Typname):
- `Core.SourceGeneration/TypeAggregator.cs` — Suffix beim Typschlüssel normalisieren.
- `Infrastructure.SourceGeneration/DtoMapperGenerator.cs` (`GetSimpleTypeName`) — Suffix beim
  Ableiten des C#-Bezeichners strippen (Feld-Mapping mit zwei gleichtypigen VO-Feldern).

Neue Framework-Typen brauchen zudem je eine Zeile in den handgepflegten STJ-Manifesten
(dokumentierter Schritt): `CqrsWireJsonContext` (Commands/Events/Signale) +
`EventJsonSerializerContext` (persistente Events).

## Performance-Ansatz

Die Perf-Tests prüfen mit **großzügigen Wall-Clock-Budgets** (kein harter Durchsatz-Assert,
der unter paralleler Test-Last flackert). Sie fangen katastrophale Regressionen — ein
versehentliches O(N²) beim Aggregat-Aufbau würde die Budgets sofort sprengen; der laufende
Saldo hält es O(1). Gemessen liegen alle weit unter Budget (1 M Value-Object-Additionen,
500 k komponierte Spezifikations-Auswertungen, 10 k Commands durch die generierte Pipeline —
je deutlich unter dem Budget).

## Build & Test

```bash
dotnet test Infrastructure.Pruefstand.Tests/Infrastructure.Pruefstand.Tests.csproj
```

> **Umgebungs-Hinweis:** .NET 9 ist seit Mai 2026 EOL und aus den Paket-Feeds entfernt. Gebaut
> mit dem **.NET-10-SDK**, ausgeführt per `DOTNET_ROLL_FORWARD=LatestMajor` auf der .NET-10-
> Runtime (net9.0-Targets unverändert). Ergebnis: **Prüfstand 146/146 grün**, stabil.
