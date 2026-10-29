# 07 — Projektstruktur: wo liegt was, wie heißt es

> Die Projekt- und Ordnerstruktur einer Anwendung ist eine **feste Konvention des Frameworks** — bewusst gesetzt, damit Generatoren, Extractor
> und Werkbank wissen, wo etwas liegt, und damit ein Entwickler (und später ein geführter Entwicklungsprozess) weiß, wohin etwas gehört. Dieses
> Kapitel beschreibt sie; im Code steht sie genau einmal: `Abstractions/Projektstruktur.cs` (per Link auch in den Generatoren), geprüft von
> CQRS098 und `--check`. Maßstab: CLAUDE.md W1–W12 (Leitsatz W2).

## 7.1 Grundsatz: Ort ist Konvention, Bedeutung ist Code-Fakt

- **Wo etwas liegt und wie die Datei heißt**, legt diese Struktur fest. Werkzeuge dürfen sie kennen: der Codegen weiß, welche Projekte die Domäne
  bilden; der Scaffolder weiß, wohin eine neue Projektion gehört; `--check` prüft, dass alles am richtigen Ort liegt.
- **Was etwas ist**, erkennen Werkzeuge weiterhin nur an Markern und Signaturen (`IState`, `ICommand`, `IProjektion`, `IFunktion` …). Ein Typ wird nie
  über seinen Namen oder Ordner als Aggregat, Projektion usw. erkannt. Die Struktur ordnet, die Marker bestimmen.
- **Rollen, keine Domänen.** Die Projekte sind Rollen (Schreibseite, Leseseite, Abläufe …). Jede Domäne passt in dieselben Rollen; Domänen werden
  innerhalb der Projekte durch Bereichs-Ordner getrennt.
- **Eine Quelle, geprüft.** Die Tabellen unten gibt es im Code genau einmal. Liegt ein Typ im falschen Projekt oder Ordner, ist das ein Fehler im
  Build bzw. in `--check` — nie still.

## 7.2 Die Projekte einer Anwendung

| Projekt (Rolle) | gehört hinein | referenziert |
|---|---|---|
| `Domain` | Aggregate (State, Decider, Applier), Commands, Events, Ablehnungen, Wertobjekte, Enums, Quellen-Typen; Queries an ein Aggregat und ihre Antworten (`Frage` im Decider — er sieht die Leseseite nicht) | Abstractions, Core |
| `Domain.Projections` | ReadModels, Queries an Reader, Antworten, Store-Fähigkeiten und -Bündel, Projektionen, Reader | Domain |
| `Domain.Pipeline` | Funktionen — als Klasse (im Host) oder Schnittstelle (draußen) mit `Auf`, samt ihren Ausgabe-Events und den Umsetzungen einer Schnittstelle —, Domänen-Dienste (Schnittstelle, Implementierung), Konfig-Records | Domain, Domain.Projections |
| `Domain.Akteure` | Akteure, Client-Verträge | Domain, Domain.Projections, Domain.Pipeline |
| `Domain.Infrastructure` | Store-Implementierungen | Domain, Domain.Projections |
| `Domain.Leitung` | die Domänen-DTOs der Leitung (`domain.proto`, vom Codegen) und ihr Nutzlast-Mapper (`GeneratedNutzlast`) — nichts Handgeschriebenes | Domain, Domain.Projections, Domain.Pipeline |
| `Domain.Laufzeit` | die Komposition: alle Laufzeit-Generate, `GeneratedAnwendung`, `AddCqrs` der Hosts, die STJ-Kontexte — nichts Handgeschriebenes | alle Rollen-Projekte + Infrastructure |
| `Domain.Client.Worker.Python.*` | Python-Implementierungen getragener Funktionen (`funktionen/<modul>.py`), generierte Basen unter `domain_client/generated/` | — |
| `Domain.Client.Modules.*` | GUI-Clients (außerhalb des Editors, W12) | Client.Infrastructure |
| `umgebungen/` | `umgebung.<name>.json` je Umgebung (Ordnername: `Projektstruktur.UmgebungsOrdner`) | — |

**Rollen als Suffix zur Wurzel.** Die Projektnamen sind Suffixe zu einer Anwendungs-Wurzel (`""`, `.Projections`, `.Pipeline`, `.Akteure`,
`.Infrastructure`, `.Leitung`, `.Laufzeit`); die Beispielanwendung hat die Wurzel `Domain`, eine zweite Anwendung (z. B. eine Test-Domäne) eine eigene. Nicht jede Rolle muss
existieren. Die Rolle eines Projekts folgt aus seinem Namen (`Projektstruktur.Zerlege`).

Die Referenzrichtung ist fest: `Domain` ← `Domain.Projections` ← `Domain.Pipeline` ← `Domain.Akteure`; `Domain.Projections` ← `Domain.Infrastructure`;
alle ← `Domain.Laufzeit` (die Komposition, die die Hosts referenzieren). Das Framework (`Infrastructure`, `Core`, `Cqrs.Leitung`) referenziert kein
Rollen-Projekt; es liest die Anwendung über `IAnwendung`, die die Komposition generiert und anmeldet.
Deshalb gehören Funktionen mit Lese-Fähigkeit (brauchen Leseseiten-Typen) und Akteure mit `IDarf<Query>` nicht nach `Domain`. Die Akteure sind der
äußerste Ring: ein Akteur darf jede Eingangsart haben — Command, Query, Quelle, Funktion (`IDarf<IFunktion>`, er bietet sie an) — und sein
Vertrag hört auf beliebige Events, auch auf Ergebnis-Events von Funktionen.

## 7.3 Bereichs-Ordner und Namespaces

- In jedem Projekt gibt es je fachlichem Bereich einen Ordner: `<Projekt>/<Bereich>/` (z. B. `Domain/Ausleihe/`, `Domain.Projections/Ausleihe/`,
  `Domain.Pipeline/Ausleihe/`). Ein Bereich ist meist ein Aggregat oder eine kleine Gruppe zusammengehöriger Aggregate.
- **Namespace = Projekt + Ordner:** `Domain.Ausleihe`, `Domain.Projections.Ausleihe`, `Domain.Pipeline.Ausleihe`.
- Derselbe Bereichs-Name in allen Projekten zeigt, was zusammengehört; die Zugehörigkeit im Editor kommt trotzdem aus dem Graphen, nicht aus dem Namen.

## 7.4 Dateien je Primitive

| Projekt | Datei | Inhalt |
|---|---|---|
| `Domain/<Bereich>/` | `<Aggregat>.cs` | State |
| | `<Aggregat>.Decider.cs`, `<Aggregat>.Applier.cs` | Decide- und Apply-Methoden |
| | `Commands.cs`, `Events.cs` | Commands; Events und Ablehnungen |
| | `ValueObjects.cs`, `Enums.cs` | Wertobjekte, Enums |
| | `Quellen.cs` | Quellen-Typen (`sealed record X : DateiGemeldet;`) |
| | `Queries.cs`, `Responses.cs` | Queries an das Aggregat und ihre Antworten (Primitive `AggregatQuery`/`AggregatAntwort`, erkannt an der `Frage` im Decider) |
| `Domain.Projections/<Bereich>/` | `ReadModels.cs`, `Queries.cs`, `Responses.cs` | Read-Models, Queries, Antworten |
| | `I<Store>.cs` | Store-Bündel mit seinen Fähigkeiten |
| | `<Projektion>.cs`, `<Reader>.cs` | je Klasse eine Datei |
| `Domain.Akteure/` | `Akteure.cs`, `Clients.cs` | Akteure; Client-Verträge |
| `Domain.Pipeline/<Bereich>/` | `Funktionen.cs`, `Events.cs` | Funktions-Schnittstellen (draußen); Ausgabe-Events der Funktionen |
| | `<Funktion>.cs` | je Funktion als Klasse (im Host, Primitive `HostFunktion`) eine Datei |
| | `<Implementierung>.cs` | je Funktions- bzw. Dienst-Implementierung eine Datei |
| | `I<Dienst>.cs`, `Konfigs.cs` | Dienst-Schnittstelle; Konfig-Records |
| `Domain.Infrastructure/<Bereich>/` | `<Store>.cs`, `<Store>.Lesen.cs` | Store-Implementierung (Schreib- und Leseteil) |

Typnamen sind frei; Klassen tragen ihren Namen als Dateinamen, Sammeldateien (`Commands.cs` …) bündeln Records einer Art. Hilfscode, der keine
Primitive ist, liegt im Bereichs-Ordner seines Projekts und erscheint im Editor als Code-Karte.

## 7.5 Was die Werkzeuge damit tun

| Werkzeug | nutzt die Struktur für | Ort |
|---|---|---|
| Leseseiten-Generate | Namespace = Projekt (`ProjectionQueryService`, `{Projekt}.Generated`), Leseseite der Wurzel | `ProjectionQueryServiceGenerator`, `ProjectionServicesGenerator` |
| Codegen-Schritt (Proto, STJ, Python-Generate) | je Anwendung (`--wurzel`, `--projekt`, `--alle` = je Komposition eine) Eingaben (`CodegenProjekte`, nur vorhandene) und Ausgabeorte (Proto nur mit Leitung, Python nur mit Worker); erzeugt nur bei geänderten DLLs (Hash je Wurzel) | `Cqrs.Codegen`, `Cqrs.Codegen/CqrsCodegen.targets` (importiert von der Leitung bzw. ohne Leitung von der Komposition), `codegen.sh` |
| Projektlage (`--check`, Werkbank) | die Komposition der `StandardWurzel` ist die Laufzeit der Werkzeuge; eine zweite Anwendung derselben Solution bleibt außen vor | `GraphExtractor.Kern/Projektlage.cs` |
| Scaffolder / Gesten | Zielprojekt (Rolle), -ordner (Bereich aus dem Namespace der Karte) und -datei einer neuen Karte; Umbenennen zieht `<Typ>.<Teil>.cs` mit | `DomainEditor/Strukturort.cs`, `Schreiben/Umbenennen.cs` |
| Werkbank (Stand-Hash, ⟳) | was zum Stand gehört, welches Projekt der Host ist, wo der Python-Worker liegt | `Werkbank/Server/WerkbankServer.cs`, `HostProzess.cs`, `Worker.cs` |
| Analyzer CQRS098 | jede Primitive im Projekt ihrer Rolle (im Build, an der Karte) | `Domain.SourceGeneration/LageAnalyzer.cs` |
| `--check` (Bereich „Lage“) | Rollen-Projekte, Referenzrichtung, Codegen-Referenzen, je Typ Projekt, Ordner, Namespace und Datei | `GraphExtractor.Kern/Lagepruefung.cs` |
| `--sonde` | die fremde Sonden-Domäne liegt absichtlich in einem Projekt: die Lage-Prüfung muss jeden ihrer Typen ins Projekt seiner Rolle weisen (`soll.txt`, `lage …`) | `GraphExtractor.Kern/Sonde.cs` |

WAS ein Typ ist (die Primitive), bestimmt `Domain.SourceGeneration/Lageart.cs` nur aus Markern und Signaturen (Ergebnis-Events aus der
Funktions-Signatur — bei der Baustein-Form die Fälle jedes `Auf`, gelesen über `Infrastructure.SourceGeneration/Funktionsform.cs`, dieselbe Lesart
wie Generator und Extractor —, eine Klasse mit `IFunktion` und `Auf` ist eine Funktion im Host, Konfig-Records aus dem Implementierungs-Konstruktor, Fähigkeiten aus dem Bündel) — dieselbe Lesart für Analyzer und `--check`.

## 7.6 Offen

- Die Wurzel der Werkzeuge (Werkbank, Scaffolder, `--check`) ist `Projektstruktur.StandardWurzel`; Generatoren leiten sie aus dem Projektnamen
  ab, der Codegen aus dem Projekt, das ihn auslöst (`--projekt`). Die zweite Anwendung der Solution ist die Prüfdomäne (`Pruefdomaene`,
  `Pruefdomaene.Pipeline` mit den Funktionen `Auftragsabwicklung`, `Verteilabwicklung` und `Nachschub`, Komposition `Pruefdomaene.Laufzeit`, Host `Host.Pruefdomaene` für die
  E2E-Messung, Tests in `Pruefdomaene.Integration.Tests`); damit das Werkzeug ihre DLLs hat, referenziert `Cqrs.Codegen` auch sie. Sie hat keine
  Leitung — eine Anwendung ohne Leitung startet keinen gRPC-Dienst (`IAnwendung.Nutzlast` ist dann `null`); hinein geht es über den JSON-Eingang
  und die Quellen.
- Fremde Namespaces (außerhalb der Rollen-Projekte der Wurzel) platziert der Scaffolder wie bisher nach Namespace → Verzeichnis; die Lage-Prüfung
  meldet sie.
