# 07 — Projektstruktur: wo liegt was, wie heißt es

> **Entwurf zur Freigabe (2026-10-14).** Die Projekt- und Ordnerstruktur einer Anwendung ist eine **feste Konvention des Frameworks** — bewusst gesetzt,
> damit Generatoren, Extractor und Werkbank wissen, wo etwas liegt, und damit ein Entwickler (und später ein geführter Entwicklungsprozess) weiß,
> wohin etwas gehört. Dieses Kapitel ist die eine Quelle; im Code spiegelt sie eine Tabelle (geplant: `Abstractions/Projektstruktur.cs`), die alle
> Werkzeuge lesen. Maßstab: `docs/konzept-werkbank.md` (Leitsatz W2).

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

| Werkzeug | nutzt die Struktur für |
|---|---|
| Laufzeit-Generatoren, Prozess-Registry | welche Projekte die Domäne bilden (statt fester Listen an mehreren Stellen) |
| Codegen-Schritt (Proto, STJ, Python-Basen) | Eingaben und Ausgabeorte |
| Scaffolder / Gesten | Zielprojekt, -ordner und -datei einer neuen Karte |
| Werkbank (Stand-Hash, ⟳) | was zum Stand gehört, welches Projekt der Host ist |
| `--check` | Lage-Prüfung: jeder Typ im Projekt und Ordner seiner Rolle |

## 7.6 Offen bis zur Umsetzung

- Im Code fehlt noch die Tabelle als eine Quelle; heute stehen Projektnamen verstreut (docs/13 W2-1).
- Die Beispieldomäne: Flüsse, Funktionen (Schnittstellen, Aufträge, Ergebnis-Events, Implementierungen), Dienste und Konfig-Records liegen
  seit 2026-10-14 in `Domain.Pipeline/<Bereich>/`, Quellen-Typen in `Domain/<Bereich>/Quellen.cs`; `Domain.Projections` und
  `Domain.Infrastructure` folgen der Struktur ebenfalls, Aggregat-Dateien heißen `<Aggregat>.Decider.cs`/`<Aggregat>.Applier.cs`, Projektionen
  `…Projektion`. Offen: `MartenCoCommitStoreBase` liegt noch oben in `Domain.Infrastructure` (gehört ins Framework, docs/13 W2-1).
