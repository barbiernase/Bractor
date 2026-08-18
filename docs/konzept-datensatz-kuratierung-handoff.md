# Umsetzungs-Handoff — „Datensatz als Tag" (Kuratieren beim Betrachten)

> **Für den nächsten Agenten. Stand: 2026-08-18.** Dieser Handoff ist so geschrieben, dass du
> OHNE Vorwissen dieser Session loslegen kannst. Er sagt dir: was gebaut wird, worauf es aufsetzt
> (mit exakten Dateien/Mustern), die Phasen mit konkreten Schritten, und die Fallstricke, die in
> der Vorarbeit real aufgetreten sind.
>
> **Soll-Konzept (das „Warum/Was"):** [konzept-datensatz-kuratierung.md](konzept-datensatz-kuratierung.md)
> (+ Mockup `konzept-datensatz-kuratierung.board.html`). **Vorgänger-Konzept** (Schwerpunkt
> verschoben, teils überholt): [konzept-galerie-datensatz-komposition.md](konzept-galerie-datensatz-komposition.md).
> **Framework-Grundlagen:** `CLAUDE.md`, `docs/08-frontend-blazor-client.md`.

---

## 0. Der Auftrag in drei Sätzen

Ein **Datensatz** wird von einem „Korb, den man aus Suchfiltern füllt" zu einem **Tag, den man
beim Betrachten der Bilder vergibt**. Man aktiviert **ein** Sammel-Ziel (Datensatz), taggt Bilder
per Geste im **Einbild** (Taste `A`) und in der **Galerie** (Klick/Badge), inkl. **dedizierter
Mehrfach-Auswahl** (Rubber-Band); die Mitgliedschaft ist überall als Tag sichtbar, auch **rückwärts**
(„dieses Bild liegt in: …"). Lifecycle (Einfrieren → Split → Training) bleibt unverändert.

**Vom Nutzer bereits entschieden (nicht neu verhandeln):**
- ✅ **Genau EIN** aktives Sammel-Ziel (Wechsel per Dropdown).
- ✅ Die bisherige Komposition-Bühne bleibt als **„Verwalten"-Sicht** (Mitglieder sehen,
  aussortieren, Provenienz, Einfrieren) — **nicht** mehr der Kompositions-Ort.
- ✅ Reichweite: **Kern + Reverse-Tags** (inkl. serverseitigem Rückwärts-Index).
- Mehrfach-Auswahl nutzt **`NimmRangeAuf` wiederverwendet** (Herkunft „manuelle Auswahl"), KEIN
  neues Batch-Command.
- Aufnahme-Taste: Vorschlag `A` (final beim Bau bestätigen; Leertaste ist belegt).

---

## 1. Branch-Zustand, den du vorfindest (WICHTIG)

Branch `claude/imageoair-frontend-setup-0g0s4b`. In der Vorsession wurde bereits gebaut und **grün
kompiliert, aber NOCH NICHT COMMITTET** (Arbeitskopie). Du baust darauf auf:

- **Galerie (Phase 1 fertig)**: `Domain.Client.Modules.Blazor/Galerie/GalerieModule.cs` +
  `GalerieStage.razor` + generisches `Shared/VirtualGrid.razor` (+ `VirtualGrid.razor.js`). Grid
  über `Store.VirtualImagePairs`, Cursor-gekoppelt, `Columns` steuerbar, `AspectRatio` 7.46, Enter/
  Doppelklick → BILDER-Bühne. **Das ist dein Haupt-Aufsatzpunkt.**
- **Fixes (alle grün, live bewiesen):** Baum-Range-Fix; OCC-„erster-Klick-verpufft"-Fix
  (`VersioningModule`/`ConnectionModule`); Filter-Read-Your-Writes (`expected_fresh_ids`);
  MudTabs-Höhe (`Host.Blazor/Pages/_Host.cshtml`).
- Prüfstand-Baseline: **125/125** grün. Vor Beginn: `git status` ansehen; ggf. committen (der
  Nutzer wünscht Commits **ohne** `Co-Authored-By`-Trailer).

---

## 2. So arbeitest, baust und verifizierst du (Setup)

**Infra (Postgres/Redis/Consul)** muss laufen:
```bash
docker compose -f deploy-linux/docker-compose.infrastructure.yml up -d
```
Prüfen: `docker ps` → `cqrs-postgres` (5432), `cqrs-redis` (6379), `consul` (8500).

**Zwei Hosts** (Server + Frontend), im Hintergrund starten:
```bash
dotnet build Host.Grpc/Host.Grpc.csproj      # gRPC-Backend, Port 5001
dotnet build Host.Blazor/Host.Blazor.csproj  # Blazor-UI, Port 5010
dotnet run --project Host.Grpc/Host.Grpc.csproj -c Debug --no-build   # dann warten auf "listening on ...:5001"
dotnet run --project Host.Blazor/Host.Blazor.csproj -c Debug --no-build
```
UI: **http://localhost:5010**. **Bei jeder Server- ODER Client-Änderung den jeweiligen Host neu
bauen + neu starten** (kein Hot-Reload). Nach Proto-/Domain-Änderungen beide.

**Logs** (Command/Query-Fluss) stehen im Host-Stdout: `[GrpcProxy] Sent command: …`,
`Received event: …` — dein wichtigstes Debug-Werkzeug für „kommt der Command durch / scheitert er".

**Prüfstand (store-frei, immer grün halten):**
```bash
dotnet test Infrastructure.Pruefstand.Tests/Infrastructure.Pruefstand.Tests.csproj
```

---

## 3. Fallstricke, die in der Vorarbeit real zugeschlagen haben (LIES DAS)

1. **Generatoren laufen in-memory; die `.g.cs` auf Disk sind stale.** Ein neues `IUiModule` o. Ä.
   wird trotzdem beim Build eingezogen. Zum *Inspizieren* des echten Generats:
   `dotnet build … -p:EmitCompilerGeneratedFiles=true -p:CompilerGeneratedFilesOutputPath=obj/gen_check`
   und die Datei dort lesen — NICHT der `obj/…/generated`-Kopie trauen.
2. **Shell-Keybindings brauchen Fokus auf dem Shell-Container.** Ein Klick auf ein nicht
   fokussierbares Element (z. B. eine Galerie-Kachel) verliert den Fokus an `<body>` (ÜBER dem
   Container) → dessen `keydown` feuert nie. Lösung wie im `VirtualGrid`: das interaktive Element
   `tabindex="0"` geben, Fokus bei Mount + nach Klick per `_ref.FocusAsync()` zurückholen, Tasten
   lokal (`@onkeydown`) behandeln und den Rest bubblen lassen. `KeyboardEvent.key` ist `"Enter"`
   (nicht `"Return"`).
3. **Bühnen füllen die Höhe nur, weil `_Host.cshtml` MudTabs-Höhe durchreicht** (bereits gefixt).
   Neue scrollbare Flächen: Root `height:100%;overflow:hidden`, innere Scroller `min-height:0;
   overflow-y:auto`.
4. **OCC „erster Command auf frisch geladenem Aggregat":** bereits gefixt (Query-Deps seeden die
   OCC-Version nicht mehr; unbekannte Head-Version → `expected_version=-1` → Emittiert). Wenn du
   ein Aggregat NUR per Query kennst und dann commandierst, greift das automatisch. Nicht erneut
   „reparieren".
5. **Read-Your-Writes für gefilterte Reads:** bereits gefixt via `expected_fresh_ids` (Client
   schickt „zuletzt geschrieben"-IDs, Leseseite trackt sie als Deps). `ConnectionModule.MarkWritten`
   markiert automatisch bei jedem Command. **Für deine Tag-Commands (NimmPaarAuf etc.) gilt das
   schon** — die Galerie/Reads holen die Änderung ein.
6. **Proto-Regen ist Pflicht bei jedem neuen/geänderten Query/Response/Command/Event:**
   `dotnet run --project Proto.SourceGeneration` → dann ProtoRepo + Infrastructure bauen. **Queries/
   Query-Responses** laufen über den gRPC-QueryResponse-DTO → **KEINE** Änderung an den beiden
   handgepflegten STJ-Kontexten nötig. **Nur neue persistente EVENTS** müssten in
   `CqrsWireJsonContext` (Wire) + `EventJsonSerializerContext` (Marten) — das ist in diesem
   Vorhaben **nicht** der Fall (wir fügen keine neuen Events hinzu).
7. **`domain.proto` ist generiert** (`Proto.SourceGeneration/FileGenerator.cs`), nicht händisch
   editieren — den Generator ändern, dann regenerieren.

---

## 4. Architektur-Karte (worauf du aufsetzt)

### 4.1 Domäne `Datensatz` (Server, `Domain/Datensatz/`)
Alles Nötige existiert bereits:
- Commands ([Commands.cs](../Domain/Datensatz/Commands.cs)): `ErstelleDatensatz(id,name)`,
  `NimmPaarAuf(id, imagePairId)`, `EntfernePaar(id, imagePairId)`, `FuegeRangeHinzu(id, kriterien)`,
  `NimmRangeAuf(id, ids[], herkunft)`, `SetzeSplit(...)`, `FriereEin(id)`.
- Events: `PaarAufgenommen`, `PaarEntfernt`, `PaareAufgenommen(ids, herkunft)`, `DatensatzEingefroren`, …
- **Die Tag-Geste braucht KEINE neuen Commands** — sie dispatcht `NimmPaarAuf`/`EntfernePaar`
  (einzeln) bzw. `NimmRangeAuf` (Mehrfach-Auswahl, Herkunft „manuelle Auswahl").

### 4.2 Read-Seite Datensatz (`Domain.Projections/`)
- [DatensatzReadModel.cs](../Domain.Projections/DatensatzReadModel.cs): **hat bereits
  `List<Guid> Mitglieder`** (den Entwurfs-Korb). Für die Badges brauchst du diese IDs im Client.
- [DatensatzResponses.cs](../Domain.Projections/DatensatzResponses.cs): `DatensatzAntwort` hat
  Mitglieder **noch NICHT** → du fügst `IReadOnlyList<Guid> Mitglieder` hinzu (Proto-Regen).
- [DatensatzReader.cs](../Domain.Projections/DatensatzReader.cs): `HoleDatensatz`,
  `HoleDatensaetze`, `HoleDatensatzSamples`.
- [DatensatzProjektion.cs](../Domain.Projections/DatensatzProjektion.cs): reagiert auf
  `DatensatzErstellt/PaareAufgenommen/PaarAufgenommen/PaarEntfernt/SplitGesetzt/DatensatzEingefroren`.
  **Hier hängst du den Rückwärts-Index an** (Phase 3).
- Stores: [IDatensatzStore.cs](../Domain.Projections/IDatensatzStore.cs) (Read/Write), Postgres-Impl
  in `Domain.Infrastructure` (Schema `rm`). Muster für ein neues Read-Model: die ImagePair-Projektion.

### 4.3 Client-Frontend (`Domain.Client.Modules.Blazor/`)
- **Geteilter `Store`** (ns `Domain.Client.Modules`, partial über mehrere Dateien: `Data/Store.cs`,
  `Data/Store.Lifecycle.cs`, `Bilder/Store.Bilder.cs`, `Navigation/Store.Navigation.cs`,
  `Paarliste/Store.Paarliste.cs`, `Labeling/Store.Labeling.cs`). Er hält `VirtualImagePairs`,
  `Cursor`, `Suche`, `AktiveBereiche`. **Neue Notionen (`SammelZiel`, `Markierung`) kommen als
  neues Partial `Store.Datensatz.cs` dazu.**
- **Muster eines Moduls** (am Vorbild `DatensatzKomposition/` bzw. `Galerie/`): eine `…Module.cs`
  (implementiert `IStageModule`/`IHeaderModule`/… → **auto-entdeckt**, KEIN Handwiring), optional
  `…Store.cs` (Reducer, `Handle(TResponse/TClientEvent)`), `…RefreshHandler.cs`
  (`Handle(ServerEvent)` → Query dispatchen), `…IntentHandler.cs` (`Handle(IClientEvent)` → Command
  dispatchen), `…Panel.razor` (View), `…Events.cs`/`Intents.cs` (client-lokale `IClientEvent`).
- **Generierte Verdrahtung:** `Client.SourceGeneration` — `ModuleRegistryGenerator` (findet alle
  `IUiModule`), `HandleMethodGenerator` (alle `Handle(...)` → Bus), `WiringGenerator`. Du schreibst
  NUR die Module/Handler/Stores; die Verdrahtung ist automatisch.
- **Galerie** (dein Aufsatz): `Galerie/GalerieStage.razor` rendert `<VirtualGrid>` mit einer
  `Cell`-Vorlage je Paar (`ImagePairRecord`) + einen Thumbnail-Ladeeffekt. Cursor-Kopplung via
  `ImagePairAusgewaehlt` → `Store.Handle` → `Cursor.Select`.
- **Einbild** (Detail): `Bilder/BilderStage.razor` (zeigt DC0/DC2 des Cursor-Paars).
- **Labeling** (Muster für die Tag-Geste!): `Labeling/LabelingModule.cs` ist ein `IFooterModule`
  mit `KeyBindings` (`r/q/f/e/n` → `Label…Angefordert`); `LabelingIntentHandler` übersetzt →
  Command; `Store.Labeling.cs` patcht `VirtualImagePairs` auf die Label-Events. **Die Tag-Geste `A`
  baust du 1:1 nach diesem Muster.**
- **Stage-Wechsel per Nachricht** (bereits vorhanden): `StageWechselAngefordert(stageId)`
  (`Client.Infrastructure/Abstractions/ShellEvents.cs`), von der Shell abonniert. Nutzbar, falls du
  aus der Galerie/Verwalten-Sicht heraus Bühnen wechseln willst.

### 4.4 Query-/Command-Pfad (falls du am Wire arbeitest — Phase 1 & 3)
- Client: `QueryBridge` (Query → Response, RYW), `ConnectionModule` (Command → Wire). Neue Query
  wird per `queryBridge.Register<TQuery,TResponse>` generator-verdrahtet (du schreibst nur die
  `IQuery`/`IQueryResponse` + Reader-`Handle`).
- Server: `ProjectionQueryService` (generiert von `ProjectionQueryServiceGenerator`) dispatcht
  Queries an Reader; `CqrsClientService.HandleQueryAsync` ist der gRPC-Eintritt.

---

## 5. Die Phasen

> Jede Phase ist eigenständig lauffähig und live verifizierbar. Halte den Prüfstand grün; UI ist
> nur live prüfbar (Infra nötig).

### Phase 1 — Sammel-Ziel + Tag beim Betrachten (Kern)

**Ziel:** Ein aktives Sammel-Ziel (Datensatz) wählen/anlegen; Bilder per `A` (Einbild) bzw. Klick
(Galerie) taggen; Mitgliedschafts-Badge überall.

1. **Client-State** — neues Partial `Data/Store.Datensatz.cs` (ns `Domain.Client.Modules`):
   ```csharp
   public sealed record SammelZielKontext(Guid Id, string? Name, DatensatzStatus Status,
       IReadOnlySet<Guid> MitgliederIds, SplitKonfig Split /* + Balance-Felder n. Bedarf */);

   public partial class Store {
       public SammelZielKontext? SammelZiel { get; private set; }
       public bool IstMitglied(Guid pairId) => SammelZiel?.MitgliederIds.Contains(pairId) ?? false;
       // Handle(SammelZielGewaehlt) → HoleDatensatz auslösen (RefreshHandler) + Id merken
       // Handle(DatensatzAntwort) → SammelZiel aus MitgliederIds+Kopf aufbauen (nur wenn Id == aktives Ziel)
   }
   ```
2. **`DatensatzAntwort` um `Mitglieder` erweitern** ([DatensatzResponses.cs](../Domain.Projections/DatensatzResponses.cs)):
   `IReadOnlyList<Guid> Mitglieder` ergänzen; `DatensatzReader.ToAntwort` füllt aus
   `model.Mitglieder`. → **Proto-Regen** (`dotnet run --project Proto.SourceGeneration`) + ProtoRepo/
   Infrastructure bauen. (Query-Response → keine STJ-Kontext-Änderung.)
3. **Sammel-Ziel-Leiste** — neues `IHeaderModule` (`SammelZiel/SammelZielModule.cs` +
   `SammelZielBar.razor`): zeigt „Sammle in: <Name> · N Paare", Live-Balance, Dropdown zum Wechseln
   (füttert sich aus `HoleDatensaetze`), „neuen Datensatz anlegen" (dispatcht `ErstelleDatensatz` +
   `SammelZielGewaehlt`), „Einfrieren" (dispatcht `FriereEin`). Auswahl-Event
   `SammelZielGewaehlt(Guid)` als `IClientEvent`.
4. **Tag-Geste im Einbild** — ein `IFooterModule` mit `KeyBindings` (Muster: `LabelingModule`):
   `new("a","Aufnehmen/Entfernen", () => new DatensatzTagGetoggelt())`. Ein `IntentHandler`
   übersetzt: aktuelles Cursor-Paar + `SammelZiel` → `NimmPaarAuf` bzw. `EntfernePaar` (je nach
   `IstMitglied`). Im `BilderStage.razor` (oder einem kleinen Zusatz-Panel) den Toggle-Zustand
   „✓ in <Name>" anzeigen.
5. **Galerie-Badge + Klick-Toggle** — in `GalerieStage.razor` je Kachel ein `●`-Badge, wenn
   `Store.IstMitglied(rec.Id)`. Klick-Verhalten: der Einfach-Klick wählt heute den Cursor
   (`ImagePairAusgewaehlt`); für Toggle entweder ein Kachel-Button/Icon oder eine Modifier-Geste
   (z. B. Klick auf das Badge togglet). Dispatcht dieselben `NimmPaarAuf`/`EntfernePaar`.
6. **Refresh** — ein `RefreshHandler`, der bei `PaarAufgenommen/PaarEntfernt/PaareAufgenommen`
   **für das aktive Sammel-Ziel** `HoleDatensatz(id)` neu holt (Muster:
   `DatensatzKompositionRefreshHandler`) → `SammelZiel.MitgliederIds` wächst/schrumpft → Badges
   aktualisieren sich. (Read-Your-Writes greift automatisch, siehe §3.5.)

**Live-Akzeptanz:** neuen Datensatz anlegen → in Galerie/Einbild ein paar Bilder mit `A`/Klick
taggen → Badges erscheinen sofort → Leisten-Zähler + Balance ziehen mit. Command-Log zeigt
`NimmPaarAuf`/`EntfernePaar` → `PaarAufgenommen`/`PaarEntfernt` (kein `CommandFailed`).

### Phase 2 — Dedizierte Mehrfach-Auswahl (Rubber-Band)

**Ziel:** In der Galerie einen Satz Kacheln bewusst auswählen und als Batch taggen.

1. **`Markierung`** in `Store.Datensatz.cs`: `IReadOnlySet<Guid> Markierung` + Reducer (Kachel an/
   ab, Bereich setzen, leeren).
2. **Rubber-Band im `VirtualGrid`/`GalerieStage`**: Maus-Drag über dem Grid zieht ein Auswahl-
   Rechteck (rein clientseitig; Pixel→Zellindex über die schon vorhandene Zell-Geometrie
   `Cols`/`_cellH`). Shift/Strg-Klick als Alternative. Sichtbares Häkchen je markierter Kachel.
3. **Batch-Aufnahme**: Button „Auswahl (N) → Sammel-Ziel" dispatcht **`NimmRangeAuf(zielId,
   markierteIds, new RangeHerkunft(<leere Kriterien>, N))`** (Wiederverwendung, Session-
   Entscheidung; KEIN neues Command). Entfernen der Auswahl = Schleife `EntfernePaar` (idempotent).

**Live-Akzeptanz:** Rechteck über mehrere Kacheln ziehen → „→ Sammel-Ziel" → alle bekommen das
Badge; Command-Log zeigt ein `NimmRangeAuf` → `PaareAufgenommen`.

### Phase 3 — Reverse-Tags + Rückwärts-Index (Server)

**Ziel:** Beim Betrachten eines Bildes seine Datensatz-Zugehörigkeit als Chips zeigen („in:
Charge-Juni · Q3-Anomalien v2").

1. **Neues Read-Model + Store** (`Domain.Projections` + Postgres-Impl in `Domain.Infrastructure`,
   Schema `rm`): `DatensatzMitgliedschaftReadModel` — je (ImagePairId) die Liste der Datensätze,
   in denen es liegt (Id, Name, Status, Version). Muster: die bestehende Sample-Read-Model-
   Pflege. Zusammengesetzte oder listen-basierte Ablage — idempotent halten.
2. **Projektion erweitern** ([DatensatzProjektion.cs](../Domain.Projections/DatensatzProjektion.cs)):
   bei `PaarAufgenommen`/`PaareAufgenommen` den Rückwärts-Eintrag `ImagePairId → +Datensatz`
   setzen; bei `PaarEntfernt` entfernen; bei `DatensatzEingefroren` Status/Version aktualisieren.
   (Co-Commit-Store, wie die vorhandene Projektion — `IAppendProjektion`/`ICoCommitTracker`.)
3. **Query + Response + Reader**: `HoleDatensaetzeFuerPaar(Guid imagePairId) : IQuery` →
   `DatensaetzeFuerPaar(IReadOnlyList<DatensatzTag> Tags) : IQueryResponse`; Reader-`Handle` liest
   den Rückwärts-Index. → **Proto-Regen**. (Query-Response → keine STJ-Kontext-Änderung.)
4. **Client**: ein `RefreshHandler` lädt `HoleDatensaetzeFuerPaar(Cursor.Id)` cursor-getrieben
   (Muster: `DataEffects.razor` lädt cursor-getrieben Bilder/Historie); ein kleines Panel im
   Einbild zeigt die Chips. Klick auf einen Chip = `SammelZielGewaehlt` (dorthin wechseln).

**Live-Akzeptanz:** Bild in zwei Datensätze taggen → im Einbild erscheinen zwei Chips; in einem
anderen Datensatz erscheint der zusätzliche Chip; Chip-Klick macht ihn zum Sammel-Ziel.

### Begleitend — Korb-Bühne zur „Verwalten"-Sicht zurückstufen

Die bestehende `DatensatzKomposition/`-Bühne (Transfer-/Korb-Layout) wird **nicht** gelöscht,
sondern auf **Verwalten** reduziert: Mitglieder-Liste des aktiven Datensatzes, einzeln aussortieren
(`EntfernePaar`), Provenienz/Ranges, Split, **Einfrieren**. Den filter-getriebenen „ganze Range →
Datensatz"-Weg als optionalen Bulk-Saat behalten (er ist bereits korrekt: je Baum-Bereich eine
Range). Titel/Framing anpassen, damit klar ist: Kuratiert wird in Galerie/Einbild, hier wird
**verwaltet/eingefroren**.

---

## 6. Fertig-Kriterien (Definition of Done)

- Prüfstand bleibt **125/125** grün; `dotnet build` der Solution grün; beide Hosts starten.
- Phase 1–3 je **live** gegen echte Infra bewiesen (siehe Akzeptanz je Phase), Command-/Query-Log
  ohne `CommandFailed`.
- Keine Runtime-Reflection, keine Handverdrahtung (alles über die Generatoren); Aggregate bleiben
  rein (Tags sind Client-/Read-Sicht, kein Domänenzustand).
- Proto-Regen sauber gelaufen; **keine** STJ-Kontext-Änderung nötig gewesen (nur Queries/
  Responses/`Mitglieder`-Feld).
- Der Nutzer entscheidet über Commits (ohne `Co-Authored-By`).

---

## 7. Schnell-Referenz — was neu, was geändert, was wiederverwendet

| | Datei/Ort | Art |
|---|---|---|
| Neu | `Data/Store.Datensatz.cs` (SammelZiel, Markierung, IstMitglied) | Client-State-Partial |
| Neu | `SammelZiel/SammelZielModule.cs` (IHeaderModule) + `SammelZielBar.razor` | Sammel-Leiste |
| Neu | Tag-Geste: `IFooterModule` mit `KeyBindings` `a` + IntentHandler | Muster: `Labeling/` |
| Neu | `SammelZielGewaehlt`, `DatensatzTagGetoggelt` (`IClientEvent`) | client-lokale Events/Intents |
| Neu (Ph.3) | `DatensatzMitgliedschaftReadModel` + Projektion-Handler + `HoleDatensaetzeFuerPaar`/Response/Reader | Rückwärts-Index |
| Ändern | `DatensatzResponses.DatensatzAntwort` + `Mitglieder` → Proto-Regen | Read-Response |
| Ändern | `Galerie/GalerieStage.razor` (Badges + Klick-Toggle + Rubber-Band) | vorhandene Galerie |
| Ändern | `DatensatzKomposition/` → „Verwalten"-Sicht (Framing/Inhalt) | Rückstufung |
| Wiederverwenden | `NimmPaarAuf`/`EntfernePaar`/`NimmRangeAuf`/`FriereEin` | vorhandene Commands, KEINE neuen |

Viel Erfolg. Bei Unklarheit: erst das Soll-Konzept + Mockup ansehen, dann das jeweils genannte
Vorbild-Modul im Code lesen — das Muster ist überall gleich.
