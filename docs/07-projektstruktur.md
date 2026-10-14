# 07 — Projektstruktur: wo liegt was, wie heißt es

> Die Projekt- und Ordnerstruktur einer Anwendung ist eine **feste Konvention des Frameworks** — bewusst gesetzt, damit Generatoren, Extractor
> und Werkbank wissen, wo etwas liegt, und damit ein Entwickler (und später ein geführter Entwicklungsprozess) weiß, wohin etwas gehört. Dieses
> Kapitel beschreibt sie; im Code steht sie genau einmal: `Abstractions/Projektstruktur.cs` (per Link auch in den Generatoren), geprüft von
> CQRS098 und `--check`. Maßstab: `docs/konzept-werkbank.md` (Leitsatz W2).

## 7.1 Grundsatz: Ort ist Konvention, Bedeutung ist Code-Fakt

- **Wo etwas liegt und wie die Datei heißt**, legt diese Struktur fest. Werkzeuge dürfen sie kennen: der Codegen weiß, welche Projekte die Domäne
  bilden; der Scaffolder weiß, wohin eine neue Projektion gehört; `--check` prüft, dass alles am richtigen Ort liegt.
- **Was etwas ist**, erkennen Werkzeuge weiterhin nur an Markern und Signaturen (`IState`, `ICommand`, `ISubscriber`, `IFunktion` …). Ein Typ wird nie
  über seinen Namen oder Ordner als Aggregat, Projektion usw. erkannt. Die Struktur ordnet, die Marker bestimmen.
- **Rollen, keine Domänen.** Die Projekte sind Rollen (Schreibseite, Leseseite, Abläufe …). Jede Domäne passt in dieselben Rollen; Domänen werden
  innerhalb der Projekte durch Bereichs-Ordner getrennt.
- **Eine Quelle, geprüft.** Die Tabellen unten gibt es im Code genau einmal. Liegt ein Typ im falschen Projekt oder Ordner, ist das ein Fehler im
  Build bzw. in `--check` — nie still.

## 7.2 Die Projekte einer Anwendung

| Projekt (Rolle) | gehört hinein | referenziert |
|---|---|---|
| `Domain` | Aggregate (State, Decider, Applier), Commands, Events, Ablehnungen, Wertobjekte, Enums, Quellen-Typen, Prozesse | Abstractions, Core |
| `Domain.Projections` | ReadModels, Queries, Antworten, Store-Fähigkeiten und -Bündel, Projektionen, Reaktionen, Reader | Domain |
| `Domain.Pipeline` | Flüsse, Katalog-Funktionen (Schnittstelle, Auftrag, Ergebnis-Events, Implementierung), Domänen-Dienste (Schnittstelle, Implementierung), Konfig-Records | Domain, Domain.Projections |
| `Domain.Akteure` | Akteure, Akteur-Verträge, Client-Verträge | Domain, Domain.Projections, Domain.Pipeline |
| `Domain.Infrastructure` | Store-Implementierungen | Domain, Domain.Projections |
| `Domain.Client.Worker.Python.*` | Python-Implementierungen von Zusagen und externen Funktionen (`funktionen/<modul>.py`), generierte Basen unter `domain_client/generated/` | — |
| `Domain.Client.Modules.*` | GUI-Clients (außerhalb des Editors, W12) | Client.Infrastructure |
| `umgebungen/` | `umgebung.<name>.json` je Umgebung | — |

**Rollen als Suffix zur Wurzel.** Die Projektnamen sind Suffixe zu einer Anwendungs-Wurzel (`""`, `.Projections`, `.Pipeline`, `.Akteure`,
`.Infrastructure`); die Beispielanwendung hat die Wurzel `Domain`, eine zweite Anwendung (z. B. eine Test-Domäne) eine eigene. Nicht jede Rolle muss
existieren. Die Rolle eines Projekts folgt aus seinem Namen (`Projektstruktur.Zerlege`).

Die Referenzrichtung ist fest: `Domain` ← `Domain.Projections` ← `Domain.Pipeline` ← `Domain.Akteure`; `Domain.Projections` ← `Domain.Infrastructure`.
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
| | `<Prozess>.cs` | ein Prozess je Datei |
| `Domain.Projections/<Bereich>/` | `ReadModels.cs`, `Queries.cs`, `Responses.cs` | Read-Models, Queries, Antworten |
| | `I<Store>.cs` | Store-Bündel mit seinen Fähigkeiten |
| | `<Projektion>.cs`, `<Reaktion>.cs`, `<Reader>.cs` | je Klasse eine Datei |
| `Domain.Akteure/` | `Akteure.cs`, `Clients.cs` | Akteure mit ihren Verträgen; Client-Verträge |
| `Domain.Pipeline/<Bereich>/` | `<Fluss>.cs` | ein Fluss je Datei |
| | `Funktionen.cs`, `Auftraege.cs`, `Events.cs` | Funktions-Schnittstellen; Aufträge; Ergebnis-Events |
| | `<Implementierung>.cs` | je Funktions- bzw. Dienst-Implementierung eine Datei |
| | `I<Dienst>.cs`, `Konfigs.cs` | Dienst-Schnittstelle; Konfig-Records |
| `Domain.Infrastructure/<Bereich>/` | `<Store>.cs`, `<Store>.Lesen.cs` | Store-Implementierung (Schreib- und Leseteil) |

Typnamen sind frei; Klassen tragen ihren Namen als Dateinamen, Sammeldateien (`Commands.cs` …) bündeln Records einer Art. Hilfscode, der keine
Primitive ist, liegt im Bereichs-Ordner seines Projekts und erscheint im Editor als Code-Karte.

## 7.5 Was die Werkzeuge damit tun

| Werkzeug | nutzt die Struktur für | Ort |
|---|---|---|
| Prozess-Registry | je Assembly `{Assembly}.Prozess.GeneratedProzessRegeln`, in der Laufzeit vereinigt; Prozesse ohne Registry = CQRS099 | `ProzessRegelnGenerator`, `ProzessVerbundGenerator` |
| Leseseiten-Generate | Namespace = Projekt (`ProjectionQueryService`, `{Projekt}.Generated`), Leseseite der Wurzel | `ProjectionQueryServiceGenerator`, `ProjectionServicesGenerator` |
| Codegen-Schritt (Proto, STJ, Python-Basen) | Eingaben (`CodegenProjekte`) und Ausgabeorte; erzeugt nur bei geänderten DLLs (Hash) | `Cqrs.Codegen`, `Domain.Leitung.csproj`, `codegen.sh` |
| Scaffolder / Gesten | Zielprojekt (Rolle), -ordner (Bereich aus dem Namespace der Karte) und -datei einer neuen Karte; Umbenennen zieht `<Typ>.<Teil>.cs` mit | `DomainEditor/Strukturort.cs`, `Schreiben/Umbenennen.cs` |
| Werkbank (Stand-Hash, ⟳) | was zum Stand gehört, welches Projekt der Host ist, wo der Python-Worker liegt | `Werkbank/Server/WerkbankServer.cs`, `HostProzess.cs`, `Worker.cs` |
| Analyzer CQRS098 | jede Primitive im Projekt ihrer Rolle (im Build, an der Karte) | `Domain.SourceGeneration/LageAnalyzer.cs` |
| `--check` (Bereich „Lage“) | Rollen-Projekte, Referenzrichtung, Codegen-Referenzen, je Typ Projekt, Ordner, Namespace und Datei | `GraphExtractor.Kern/Lagepruefung.cs` |
| `--sonde` | die fremde Sonden-Domäne liegt absichtlich in einem Projekt: die Lage-Prüfung muss jeden ihrer Typen ins Projekt seiner Rolle weisen (`soll.txt`, `lage …`) | `GraphExtractor.Kern/Sonde.cs` |

WAS ein Typ ist (die Primitive), bestimmt `Domain.SourceGeneration/Lageart.cs` nur aus Markern und Signaturen (Ergebnis-Events aus der
Funktions-Signatur, Konfig-Records aus dem Implementierungs-Konstruktor, Fähigkeiten aus dem Bündel) — dieselbe Lesart für Analyzer und `--check`.

## 7.6 Offen

- `Infrastructure` ist zugleich Framework und Komposition (referenziert die Domänen-Projekte; nur dort laufen die Laufzeit-Generatoren) —
  docs/13 W2-1. Die Wurzel der Werkzeuge ist bis dahin `Projektstruktur.StandardWurzel`.
- Fremde Namespaces (außerhalb der Rollen-Projekte der Wurzel) platziert der Scaffolder wie bisher nach Namespace → Verzeichnis; die Lage-Prüfung
  meldet sie.
