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
- **Rollen, keine Domänen.** Die Projekte sind Rollen (Schreibseite, Leseseite, Übersetzungen …). Jede Domäne passt in dieselben Rollen; Domänen werden
  innerhalb der Projekte durch Bereichs-Ordner getrennt.
- **Eine Quelle, geprüft.** Die Tabellen unten gibt es im Code genau einmal. Liegt ein Typ im falschen Projekt oder Ordner, ist das ein Fehler im
  Build bzw. in `--check` — nie still.

## 7.2 Die Projekte einer Anwendung

| Projekt (Rolle) | gehört hinein | referenziert |
|---|---|---|
| `Domain` | die Domäne, **rein** (kennt keinen Katalog): Aggregate (State, Decider, Applier), Commands an Aggregate, Commands ohne Ziel eines Clients (an eine Kopf-Übersetzung), Events, Hinweise eines Decide, Wertobjekte, Enums; Queries an ein Aggregat und ihre Antworten (`Frage` im Decider — er sieht die Leseseite nicht) | Abstractions, Core |
| `Domain.Projections` | ReadModels, Queries an Reader, Antworten, Store-Fähigkeiten und -Bündel, Projektionen, Reader — ebenfalls ohne Katalog | Domain |
| `Domain.Uebersetzungen` | die **Übersetzungen** (`record … : IUebersetzung<TEin, TAus>`): die eine Naht zwischen Katalogen und Domäne | Domain, die Kataloge, die die Anwendung nutzt |
| `Domain.Akteure` | Akteure, Clients | Domain, Domain.Projections, Domain.Uebersetzungen |
| `Domain.Infrastructure` | Store-Implementierungen — auch die einer Lese-Fähigkeit, die ein Katalog deklariert (CQRS124) | Domain, Domain.Projections, Kataloge |
| `Domain.Leitung` | die Domänen-DTOs der Leitung (`domain.proto`, vom Codegen) und ihr Nutzlast-Mapper (`GeneratedNutzlast`) — nichts Handgeschriebenes | Domain, Domain.Projections, Domain.Uebersetzungen |
| `Domain.Laufzeit` | die Komposition: alle Laufzeit-Generate, `GeneratedAnwendung`, `AddCqrs` der Hosts, die STJ-Kontexte — nichts Handgeschriebenes | alle Rollen-Projekte + Infrastructure |
| `Domain.Client.Worker` | der .NET-Client, der auf die Fakten der Anwendung reagiert (der `ReaktionsWorker`, ein eigener Prozess) | Domain, Domain.Projections, Domain.Akteure, Domain.Leitung |
| `Domain.Client.Worker.Python.*` | Python-Clients: Implementierungen der generierten Client-Basen (`domain_client/`), Generate unter `domain_client/generated/` | — |
| `Domain.Client.Modules.*` | GUI-Clients (außerhalb des Editors, W12) | Client.Infrastructure |
| `<Name>.Katalog` | ein **Funktionskatalog** (keine Rolle *einer* Anwendung, eigene Wurzel): **die einzigen Funktionen und Graphen** — mit ihren Nachrichten (Commands ohne Ziel, Hinweise), Konfig-Records, Technik, Lese-Fähigkeiten, Wertobjekten, Enums; domänenneutral (`Guid` statt `Id<T>`); die Werkbank legt ihn an („＋ 📚 Katalog“) | Abstractions, Core, andere Kataloge |
| `umgebungen/` | je Umgebung ein Ordner `<name>/` mit `umgebung.json` und `graphen/<instanz>.json` je Instanz (`Projektstruktur.UmgebungsOrdner`, `UmgebungsDatei`, `GraphenOrdner`) | — |

**Rollen als Suffix zur Wurzel.** Die Projektnamen sind Suffixe zu einer Anwendungs-Wurzel (`""`, `.Projections`, `.Uebersetzungen`, `.Akteure`,
`.Infrastructure`, `.Leitung`, `.Laufzeit`); die Beispielanwendung hat die Wurzel `Domain`, eine zweite Anwendung (z. B. eine Test-Domäne) eine eigene. Nicht jede Rolle muss
existieren. Die Rolle eines Projekts folgt aus seinem Namen (`Projektstruktur.Zerlege`).

Die Referenzrichtung ist fest: `Domain` ← `Domain.Projections` ← `Domain.Akteure`; `Domain` ← `Domain.Uebersetzungen` ← `Domain.Akteure`;
`Domain.Projections` ← `Domain.Infrastructure`;
alle ← `Domain.Laufzeit` (die Komposition, die die Hosts referenzieren). Das Framework (`Infrastructure`, `Core`, `Cqrs.Leitung`) referenziert kein
Rollen-Projekt; es liest die Anwendung über `IAnwendung`, die die Komposition generiert und anmeldet.
**Kataloge und Übersetzungen** (`Projektstruktur.Katalog`, `IstKatalog`, `KatalogVon`; docs/04 §4.4): eine Anwendung hat **keine Funktionen und
Graphen** — sie nutzt sie aus Katalogen, und **Übersetzungen** machen sie domänenspezifisch. Ein Katalog kennt keine Anwendung, und **die Domäne
bleibt rein**: Schreib- und Leseseite referenzieren keinen Katalog. Kataloge nutzen dürfen nur die Übersetzungen (dort setzt die Geste „neuer
Katalog“ die Referenz), der Speicher (er setzt Lese-Fähigkeiten eines Katalogs um), die Akteure (sie geben und hören Nachrichten eines Katalogs),
Leitung und Komposition (`NutzenKataloge`); welche eine Anwendung nutzt, steht in deren Projektdateien (`KatalogeIn`). Eine Funktion oder ein Graph
in einer Anwendung, ein Fakt, ein Command an ein Aggregat oder ein Aggregat im Katalog ist CQRS098; eine Referenz der Domäne auf einen Katalog meldet
`--check`. Codegen und Werkbank-Stand nehmen die genutzten Kataloge mit.
Die Akteure sind der äußerste Ring: ein Akteur darf Commands, Queries und Hinweise aus allen Rollen und genutzten Katalogen nennen (`IDarf`) und
hören (`IHoert`); ein Client nennt nur Akteure.

## 7.3 Bereichs-Ordner und Namespaces

- In jedem Projekt gibt es je fachlichem Bereich einen Ordner: `<Projekt>/<Bereich>/` (z. B. `Domain/Ausleihe/`, `Domain.Projections/Ausleihe/`,
  `Domain.Uebersetzungen/Ausleihe/`). Ein Bereich ist meist ein Aggregat oder eine kleine Gruppe zusammengehöriger Aggregate; im Katalog ein
  Themenbereich seiner Funktionen (`Bildvorschau.Katalog/Aufbereitung/`).
- **Namespace = Projekt + Ordner:** `Domain.Ausleihe`, `Domain.Projections.Ausleihe`, `Domain.Uebersetzungen.Ausleihe`, `Bildvorschau.Katalog.Aufbereitung`.
- Derselbe Bereichs-Name in allen Projekten zeigt, was zusammengehört; die Zugehörigkeit im Editor kommt trotzdem aus dem Graphen, nicht aus dem Namen.

## 7.4 Dateien je Primitive

| Projekt | Datei | Inhalt |
|---|---|---|
| `Domain/<Bereich>/` | `<Aggregat>.cs` | State |
| | `<Aggregat>.Decider.cs`, `<Aggregat>.Applier.cs` | Decide- und Apply-Methoden |
| | `Commands.cs`, `Events.cs` | Commands an Aggregate und Commands ohne Ziel eines Clients; Events und Hinweise eines Decide |
| | `ValueObjects.cs`, `Enums.cs` | Wertobjekte, Enums |
| | `Queries.cs`, `Responses.cs` | Queries an das Aggregat und ihre Antworten (Primitive `AggregatQuery`/`AggregatAntwort`, erkannt an der `Frage` im Decider) |
| `Domain.Projections/<Bereich>/` | `ReadModels.cs`, `Queries.cs`, `Responses.cs` | Read-Models, Queries, Antworten |
| | `I<Store>.cs` | Store-Bündel mit seinen Fähigkeiten |
| | `<Projektion>.cs`, `<Reader>.cs` | je Klasse eine Datei |
| `Domain.Akteure/` | `Akteure.cs`, `Clients.cs` | Akteure (`record … : IAkteur`); Clients (`record … : IClient`) |
| `Domain.Uebersetzungen/<Bereich>/` | `Uebersetzungen.cs` | Übersetzungen (Primitive `Uebersetzung`, `record … : IUebersetzung<TEin, TAus>` mit `[Feld]`) |
| `Domain.Infrastructure/<Bereich>/` | `<Store>.cs`, `<Store>.Lesen.cs` | Store-Implementierung (Schreib- und Leseteil) |
| `<Name>.Katalog/<Bereich>/` | `<Funktion>.cs` | je Funktion eine Datei (Primitive `Funktion`, eine Klasse mit Rumpf) |
| | `<Technik>.cs` | je Technik (Schnittstelle oder Klasse im Konstruktor einer Funktion) und je Umsetzung eine Datei (Primitive `Technik`) |
| | `I<Fähigkeit>.cs` | Lese-Fähigkeiten, die seine Funktionen nehmen (umgesetzt im Speicher der Anwendung) |
| | `Commands.cs`, `Events.cs` | Commands ohne Ziel (Primitive `KnotenCommand`); Hinweise (Primitive `Hinweis`) |
| | `Graphen.cs`, `Konfigs.cs`, `ValueObjects.cs`, `Enums.cs` | Graphen (Primitive `Graph`, `record … : IGraph`), Konfig-Records, Wertobjekte, Enums |

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
| `--sonde` | die fremde Sonden-Domäne liegt absichtlich in einem Projekt (ihr Katalog, ihre Übersetzungen, Akteure mit Katalog-Nachrichten und Stores in ihren eigenen): die Lage-Prüfung muss jeden falsch liegenden Typ ins Projekt seiner Rolle weisen (`soll.txt`, `lage …`) | `GraphExtractor.Kern/Sonde.cs` |

WAS ein Typ ist (die Primitive), bestimmt `Domain.SourceGeneration/Lageart.cs` nur aus Markern und Signaturen: ein Command ohne Ziel ist immer
ein `KnotenCommand` und liegt im Katalog oder in der Domäne (ein Decide gibt keinen aus, CQRS114); eine Klasse mit `IFunktion` ist eine Funktion,
ein Record mit `IGraph` ein Graph, einer mit `IUebersetzung<,>` eine Übersetzung; Technik und Konfig-Records folgen aus den Konstruktoren der Funktionen und ihrer Umsetzungen (gelesen über
`Infrastructure.SourceGeneration/Funktionsform.cs`, dieselbe Lesart wie Generator und Extractor), Fähigkeiten aus dem Bündel — dieselbe Lesart für
Analyzer und `--check`.

## 7.6 Offen

- Die Wurzel der Werkzeuge (Werkbank, Scaffolder, `--check`) ist `Projektstruktur.StandardWurzel`; Generatoren leiten sie aus dem Projektnamen
  ab, der Codegen aus dem Projekt, das ihn auslöst (`--projekt`). Die zweite Anwendung der Solution ist die Prüfdomäne (`Pruefdomaene`,
  `Pruefdomaene.Uebersetzungen` zu den Graphen der Kataloge `Pruefwerk.Katalog` — Abrechnung, Import, Merkmalbildung — und `Rechenwerk.Katalog`;
  Auftrag und Verteilung sind Reaktionen ihres Workers, `Pruefdomaene.Akteure` mit `Rechnungspruefer`, `Messwart`, `Sachbearbeitung` und dem Client `ReaktionsWorker`, Leitung `Pruefdomaene.Leitung`,
  Komposition `Pruefdomaene.Laufzeit`, der Worker `Pruefdomaene.Client.Worker`, Host `Host.Pruefdomaene` für die E2E-Messung, Tests in
  `Pruefdomaene.Integration.Tests`); damit das Werkzeug ihre DLLs hat, referenziert `Cqrs.Codegen` auch sie. Mit ihrer Leitung startet sie den
  gRPC-Dienst: ihr Worker reagiert auf die Fakten über die Leitung wie jeder Client; eine Anwendung ohne Leitung (`IAnwendung.Nutzlast` ist dann
  `null`) nimmt nur den JSON-Eingang.
- Fremde Namespaces (außerhalb der Rollen-Projekte der Wurzel) platziert der Scaffolder wie bisher nach Namespace → Verzeichnis; die Lage-Prüfung
  meldet sie.
