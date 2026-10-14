# LLM-Füllung von Code-Blöcken: Kontext nur aus dem Code

> Detail-Referenz zu `docs/konzept-werkbank.md` (A.5); bei Widerspruch gilt das Zielbild. Stand 2026-10-13, gegen den Code geprüft.
>
> **Ziel:** `claude -p` über das Abo dieses Rechners (nie die API) schreibt **nur den Rumpf** eines Code-Blocks. Alles, was es dafür wissen
> muss, wird **regelbasiert aus dem Domänen-Code und dem Graphen** abgeleitet — nichts ist ausgedacht, nichts semantisch geraten. Die
> **einzige** menschliche Eingabe ist der Auftrag (🤖 füllen). Code-Fakten statt Namenskonventionen: konzept-werkbank A.6.
> Messwerte in diesem Dokument sind **Beispielmessungen an der Beispieldomäne**; Offenes: `docs/13` (WB-1, W11-1).

---

## 1 · Grundsätze

1. **Auslöser ist die Aktion 🤖 füllen am Rumpf-Port einer Karte.** Ohne diese Aktion passiert nichts.
2. **Nur Code-Fakten.** Jede Kontext-Zeile kommt aus einem Symbol (Roslyn), einer Graph-/Editor-Kante, einem Nachbar-Rumpf
   oder wörtlich aus einem Kommentar. Keine Namensähnlichkeit, kein Ranking, keine Zusammenfassung, keine erfundene Prosa.
   Beschreibungen erscheinen nur, wo der Code einen Kommentar trägt.
3. **Der Auftrag ist die einzige menschliche Eingabe** (`intent`). Er steht als genau eine Zeile `// 🤖 Prompt:` im Rumpf.
4. **Vertragsregeln nur, wenn erzwungen.** Eine Regel steht im Kontext nur, wenn Compiler, Generator, Analyzer oder
   Framework-Laufzeit sie durchsetzen. Nicht erzwungene Regeln (z. B. „Decide ist rein") brauchen erst einen Analyzer.
5. **Das LLM schreibt nur den Rumpf** — oder antwortet `AUSSERHALB: braucht <Art> <Name>`, wenn der Auftrag nicht im
   Spielraum lösbar ist (neuer Ausgang, neues Feld, neue Kante). Strukturänderungen sind Editor-Arbeit (D/S), nie LLM-Arbeit.

---

## 2 · Ablauf (der eine Durchgang)

```
🤖 füllen (Auftrag) am Rumpf-Port ──▶ POST /api/llm/ausfuehren

 1. Slot auflösen   Rumpf-Port → Slot-Art + Anker (Datei, Klasse, Methode bzw. Lambda/Python-Anker)      [§4]
 2. Kontext bauen   fester Teil (Graph-Skelett) + Slot-Teil + Auftrag [+ aktueller Rumpf · Befund/Anpassung]  [§3]
 3. LLM aufrufen    claude -p, zustandslos je Runde → ```csharp-Block (Rumpf ohne äußere Klammern) | AUSSERHALB: braucht …
 4. Prüfen          C#: probeweise im warmen Workspace (Arbeitsbereich.PruefeProbeweiseAsync: Datei mit dem Kandidaten,
                    Compiler + Analyzer des Projekts, nur neue Fehler); Fluss-/Regel-Lambdas eigens; Python per Parser.
                    Befund → nächste Runde mit Befund (max. 3), dann Mensch
 5. Schreiben       nur ein geprüfter Rumpf: in die Datei, mit „// 🤖 Prompt: <Auftrag>“, Hash-Sperre gegen Zwischenänderungen;
                    der Patch kommt in den ↶-Verlauf der Werkbank
 6. Bauen           Build des Laufzeit-Projekts (alle Generatoren inkl. Codegen-Schritt); der warme Workspace liest den Code im
                    Speicher neu (Sekundenbruchteile, Messwerte docs/12) — nur die LLM-Kontexte entstehen danach im Hintergrund
                    kalt neu (GraphExtractor --kontexte)
```

- **🤖 füllen ist ein Chat:** die 1. Nachricht ist der Auftrag, jede weitere passt den aktuellen Rumpf an. Jede Nachricht ist ein
  vollständiger Durchgang; nie ein Verlauf, nur der letzte Rumpf reist mit. Prompt stabil → veränderlich (feste Anweisung · Skelett ·
  Slot-Teil · Auftrag · Rumpf/Befund), damit der Präfix-Cache greift.
- **Nicht geprüft** → nichts wird geschrieben; der Kandidat kommt als Vorschlag zurück (↻ Anpassen · ✓ trotzdem übernehmen · ✗ Verwerfen).
- **Schreibweg:** die Übernahme IST eine Rumpf-Geste wie ✎ — der Ort kommt aus dem Slot-Anker (`CodeSync.Ort`), geschrieben wird über
  `GestenDienst.RumpfAsync` (`GraphExtractor.Kern/Schreiben/Rumpf.cs`, C# und Lambdas) bzw. `PythonRumpfAsync` (`PythonSync`); Verlauf und ↶ der
  Werkbank, auf `main` gesperrt. Geprüft wird mit derselben Einsetzung (`Rumpf.MitRumpf`).
- Ohne LLM prüfbar: `dotnet run --project Werkbank -- --pruefe-rumpf <Slot-Id> <Datei>` (schreibt nichts).

---

## 3 · Aufbau des Kontexts

```
┌─ FESTER TEIL — für alle Slots gleich (Präfix-Cache) ─────────────────────────────────
│  Anweisung (Ausgabeformat: nur Rumpf oder AUSSERHALB)
│  Graph-Skelett: alle Aggregate/Commands/Events/Prozesse/Flüsse/Stores/Projektionen/Reader,
│                 als Signaturen + Kanten (keine Rumpf-Fakten)
├─ SLOT-TEIL — je Slot ─────────────────────────────────────────────────────────────────
│  AUFTRAG · ANKER · SIGNATUR · VERTRAG · ERREICHBAR · KOMMENTARE · TYPEN · GRAPH-UMFELD · NACHBAR-CODE-BLÖCKE · KOPPLUNG
└─ Antwort ─────────────────────────────────────────────────────────────────────────────
```

**Quellen-Kennzeichnung** in §4: **[S]** Symbol (Deklaration/Signatur) · **[K]** Kante (Graph, aus dem Code) · **[N]** Nachbar-Rumpf ·
**[L]** Auftrag (🤖 füllen) · **❌** heute nicht im Graphen.

---

## 4 · Informationen je Slot-Art

### Für alle Slot-Arten
1. Auftrag: `intent` aus 🤖 füllen [L]
2. Graph-Skelett (§3) [S+K]
3. Anker: Datei, Klasse, Methode, Rumpf-Status (leer / Stub / geschrieben) [S]
4. Doku- und Zeilenkommentare an Methode und Eingangstyp, wörtlich [S]
5. Transitive Hülle der Domänen-Typen: Records mit Konstruktor, Enums mit Werten, berechnete Member als Ausdruck, `///`-Texte [S]
6. **Alle Code-Blöcke derselben Klasse** (§5) [N]

### Decide(Command) — gefunden über `IDecider<T>`, 1. Parameter `ICommand`
1. Signatur + OneOf-Ausgänge; je Ausgang Event (`IEvent`) oder Ablehnung (`ITransientEvent`) [S]
2. Command-Felder, Erzeugungs-Command (`ICreationCommand`) [S]
3. State: alle Member mit Typ/Accessor, berechnete mit Ausdruck — nur lesbar [S]
4. Erreichbar ist sonst nichts: der Generator erzeugt `new X.Decider(state)` (`Domain.SourceGeneration/FactoryGenerator.cs`) [S]
5. Herkunft des Commands: Client · Prozess X Regel i (Wenn-Events) · Fluss-Knoten Y [K]
6. Abnehmer je Ausgang: Apply-Slot (Status) · Projektionen · Prozesse (Auslöser/Bedingung) · Fluss-Knoten [K]
7. Datenfluss: welche Decide welches State-Member lesen, welche Apply es schreiben [N]
8. Vertrag: Ausgänge ⊆ OneOf (Compiler); eine Ablehnung steht allein (Laufzeit, `AggregateActorBase`) [S]

### Apply(Event) — `IApplier<T>`, 1. Parameter `IEvent`
1. Signatur + Event-Felder [S]
2. State: alle Member, schreibbar/berechnet; Helfer-Methoden des Appliers [S]
3. Erzeugende Decide [K]
4. Weitere Abnehmer des Events: Projektionen, Prozesse, Fluss-Knoten [K]
5. Datenfluss: welche Member andere Apply schreiben, welche Decide sie lesen, welche kein anderer Apply schreibt [N]
6. Ziele gleichen Typs je Event-Feld; Konstruktor, der exakt die Typfolge der Event-Felder nimmt [S]

### Projektion.Handle(Event, Umschlag, Writer) — `ISubscriber`, Parameter `IEvent` + `IAggregateEnvelope`
1. Signatur, Event-Felder, Umschlag (`AggregateId`, `CreatedAtUtc`) [S]
2. Achsen: geordneter Pull / Signal; Upsert (at-least-once, idempotent schreiben) / append-artig (Co-Commit, exactly-once, GA-1) [S]
3. Store-Scope: nur die Write-Fähigkeiten aus den Handle-Parametern, mit Signatur [S]
4. ReadModels des Stores mit Feldern [K]
5. Aggregat, das das Event erzeugt (für `Track<Agg>`) [K]
6. Veröffentlichtes reaktives Event, falls verdrahtet [K]
7. Kopplung: Store-Impl der aufgerufenen Funktionen [K]

### Reaktion.Handle(Event, …) — wie Projektion, Rückgabe `IAsyncEnumerable<OneOf<…>>`
1. Signatur, Event-Felder, Umschlag [S]
2. Ausgänge: gesendete Commands / veröffentlichte Events [S/K]
3. Ziel-Aggregat und Felder jedes gesendeten Commands [K]
4. Achse emittierend (`IEmittentenCursor`, kein Reset); Senden nur per `yield` (CQRS020/021) [S]

### Reader.Handle(Query, Umschlag, ReadContext) — `IReader<T>`, 1. Parameter `IQuery`
1. Signatur, Query-Felder, OneOf-Antworten mit Konstruktoren [S]
2. Gebundene Projektion; `TrackDeps` (ob `ctx.Track` nötig) [S]
3. Verdrahtete Read-Funktionen mit Signatur/Rückgabe; ReadModels des Stores [K]
4. ❌ Helfer anderer Klassen (Beispieldomäne: `ImagePairReader.ToAntwort`) haben keine Kante

### Store-Impl (je Fähigkeit) — Methode, die die eine Funktion einer `IWriteStore`/`IReadStore`-Fähigkeit implementiert
1. Signatur (Parameter, Rückgabe, Write/Read) [S]
2. ReadModels des Stores [K]
3. Basis-Primitive (`Enqueue`, `EnqueueStore`, `EnqueueTransform`) bzw. injizierter Dokument-Store [S]
4. Aufrufer: Projektions-/Reader-Handles, die die Funktion rufen, samt deren Achse [K]

### Weitere Slot-Arten (Anker-Schlüssel `Art|Besitzer|Disc`; C#-Slots aus `SlotInventar.cs`, Python-Slots aus `LlmKontext.cs`)
- **Funktion** `funktion|<Funktions-Schnittstelle>|<Auftrag>` — Rumpf der Implementierung von `RufeAsync` einer Katalog-Funktion
  (`IFunktion`); Vertrag: genau ein Ergebnis aus dem OneOf (CQRS068), Fähigkeits-Parameter nur lesend; Umfeld: rufende Fluss-Knoten und Prozesse.
- **Fluss** `fluss|<Fluss-Klasse>|<Knoten>#<Lambda-Index>` — ein Bau-Lambda eines Pipeline-Flusses (Lambda-Argument eines Fluss-Verbs je
  Anweisung des `Definiere`-Lambdas der Property `Fluss`); rein und deterministisch; Umfeld: der ganze Fluss wörtlich.
- **Saga** `saga|<Prozess-Klasse>|<Anweisung>#<Verb>` — ein Lambda-Argument eines Regel-Verbs (Sende/Rufe/SendeJe, RückgängigDurch…,
  UndAlle) im `Definiere`-Lambda von `Regeln`; rein und deterministisch; Umfeld: alle Regeln des Prozesses wörtlich.
- **Zusage** `zusage|<Client>|<Event>@<Klasse>` — Python-Rumpf einer Client-Implementierung (Klasse, die die generierte Basis erbt) je
  Zusage/Kenntnis; Signatur aus der Basis; Vertrag: eine Zusage liefert nur ihre Ausgaben per `yield`, eine Kenntnis nichts; erreichbar
  Event-DTO, `ctx`, `state`, `self`; Nachbar-Code = die Datei ohne diesen Rumpf.
- **Python-Funktion** `funktionpy|<Funktions-Schnittstelle>@<Klasse>` — Rumpf `rufe` der Python-Implementierung einer externen Funktion
  (`Domain.Client.Worker.Python.ML/funktionen/<modul>.py`, Basis aus `generated/funktionen.py`); geprüft per Parser.
- **Pipeline** `pipeline|…` — Rumpf eines Handles der Handle-Pipeline: **Altlast** ohne Domänen-Nutzer (`docs/13` W11-1).

---

## 5 · Nachbar-Code-Blöcke

Die Typen sagen, **was** verfügbar ist; nur Nachbar-Rümpfe zeigen, **wie** es in diesem System benutzt wird
(z. B. `writer.Execute(…, ctx => { ctx.Track<Agg>(…); … })` in den Projektions-Handles, die Marten-Teilmenge der Stores).

**Regel:** Mitgegeben werden **alle** Code-Blöcke derselben Klasse (Decider, Applier, Projektion, Reader,
Store-Impl) außer dem eigenen — keine Auswahl. Dazu die Rümpfe der gekoppelten Slots (§4, [K]/[N]).
Gibt es keine Nachbarn (erster Slot seiner Art in der Klasse), werden Slots derselben Art aus anderen Klassen genommen.

**Kosten (Beispielmessung an der Beispieldomäne, 2026-09, Qwen-BPE-Token):**

| Slot-Art | Gruppen | Nachbar-Rümpfe je Slot, Median | Maximum |
|---|---:|---:|---:|
| Decide | 6 | 588 | 729 |
| Apply | 6 | 159 | 391 |
| Projektion | 5 | 424 | 1.014 |
| Reader | 5 | 128 | 644 |
| Store-Impl | 5 | 343 | 2.488 |

---

## 6 · Budget

Der Anbieter ist ausschließlich `claude -p` (§12); dessen Kontextfenster ist für einen Slot nicht die Grenze. Das Budget unten stammt aus
einer früheren Kalibrierung auf ein lokales Modell mit 16 GB und ist nur noch als **Größenordnung** gültig (Beispielmessung an der
Beispieldomäne, 2026-09):

| Teil | Token |
|---|---:|
| Graph-Skelett | ~8.800 |
| Slot-Teil ohne Nachbarn | ~1.500–2.400 |
| Nachbar-Code-Blöcke | ≤ 2.500 |
| Antwort | ~1.000 |
| **Summe** | **~14.000–15.000** |

Zum Vergleich damals: Domänen-Quelltext ~60.000, `domain-model.json` mit Rümpfen ~50.000 Token. Der feste Teil ist für alle Slots gleich und
wird als Präfix wiederverwendet. Heute geht das ganze Skelett mit; ein regelbasierter Schnitt auf den Teil, der vom Slot über Graph-Kanten
erreichbar ist, ist nicht umgesetzt.

---

## 7 · Messung: wie isoliert sind die Code-Block-Stellen?

`dotnet run --project GraphExtractor -- --slots` klassifiziert jedes Symbol jedes Rumpfs und prüft, woher das Wissen
kommen kann: Spielraum (Signatur, State, Felder, geerbte Member), Graph-Kante, nur Nachbar, oder von außen.

Beispielmessung an der Beispieldomäne (2026-09; aktuelle Werte liefert `--slots`):

| Art | Slots | Spielraum | + Graph-Kante | nur Nachbar | von außen | injizierte Felder | geteilte Helfer |
|---|---:|---:|---:|---:|---:|---:|---:|
| Decide | 31 | 100 % | – | – | – | 0/31 | 0/31 |
| Apply | 33 | 100 % | – | – | – | 0/33 | 4/33 |
| Projektion | 36 | 90 % | 10 % | – | – | 36/36 | 0/36 |
| Reader | 17 | 98 % | – | – | 2 % | 17/17 | 0/17 |
| Store-Impl | 56 | 63 % | 37 % | – | – | 29/56 | 3/56 |

- Der Wortschatz kommt aus Signatur + Graph; ohne Kanten fehlen Aggregat für `Track<Agg>`, gesendete Commands, ReadModels.
- „Von außen" sind genau die ❌-Lücken aus §4 (Helfer eines fremden Readers).
- Decide/Apply sind vollständig isoliert; Fluss-Lambdas liegen im eigenen Spielraum (ihre Parameter).

---

## 8 · Randfälle von Aufträgen

(a) im Spielraum lösbar · (b) braucht Strukturänderung im Editor → Antwort `AUSSERHALB` · (c) mehrere Slots gekoppelt ·
(d) falscher Konsument. Die Spalte „Beleg“ nennt Code der Beispieldomäne.

| # | Stelle | Auftrag (Beispiel) | Klasse | Was stattdessen passiert | Beleg (Beispieldomäne) |
|---|---|---|---|---|---|
| 3 | Decide | neuer Ablehnungsgrund | b | OneOf-Ausgang im Editor verdrahten | — |
| 4 | Decide | Regel über Zustand, den es noch nicht gibt | b + c | State-Feld + die Apply-Slots, die es schreiben | — |
| 5 | Decide | Idempotenz | a / c | im Spielraum, wenn der State es trägt; sonst wie 4 | `NimmPaarAuf`, `EntfernePaar` |
| 6 | Apply | Werte prüfen/ablehnen | d | gehört in Decide (Apply ist void, läuft beim Replay) | — |
| 7 | Apply | Wert, den das Event nicht trägt | b + c | Event-Feld + alle erzeugenden Decide | — |
| 8 | Projektion | Command senden | d | Reaktion (CQRS020/021) | — |
| 9 | Projektion | Daten eines anderen Aggregats | b + c | Store-Fn mit Join (zweiter Slot: Store-Impl) oder Event trägt die Daten | `DatensatzStore` |
| 10 | Projektion | neue Store-Funktion | b + c | Interface-Fn im Editor + Store-Impl-Slot | — |
| 11 | Reader | Daten aus zwei Stores | b | zweiter Store als Kante | `DatensatzReader` |
| 12 | Reader | „wie in Reader Y" | c | Helfer über Klassengrenzen — heute ohne Kante | `ImagePairReader.ToAntwort` |
| 16 | Store-Impl | Filter/Paging | a | Basis-Primitive + ReadModel; Marten-Nutzung aus Nachbarn | `ImagePairStore` |
| 18 | alle | Umbenennen / Signatur ändern | b | Editor-Struktur, kein Rumpf | — |
| 19 | alle | „wie bei Slot Y" | c | Y als Editor-Kante „Vorlage ◀" (nicht per Textsuche) | — |

Die Maschine versteht den Auftragstext nicht. Sie liefert den vollständigen Spielraum, liest strukturelle Signale aus
dem Editor (z. B. Query an einem Decider) und gibt dem LLM den Antwortkanal `AUSSERHALB`.

---

## 9 · Werkzeuge

```bash
dotnet run --project GraphExtractor -- --kontexte <verz>                       # 00-graph-skelett.txt + je Code-Block ein Slot-Teil + übersicht.md
dotnet run --project GraphExtractor -- --kontext <Disc> [--auftrag "…"]        # ein Slot-Teil auf die Konsole
dotnet run --project GraphExtractor -- --slots                                 # Isolations-Messung (§7)
```

`GraphExtractor.Kern/LlmKontext.cs` (`KontextBauer`, `KontextCli`), Slot-Erkennung aus `GraphExtractor.Kern/SlotInventar.cs`.

| Teil | Umsetzung |
|---|---|
| Graph-Skelett | aus dem Editor-Modell (`ModellMapper.ZuBoardJson`); nur Signatur-Fakten |
| Auftrag | 1. `// 🤖 Prompt:` im Rumpf, 2. `--auftrag` (nur Einzel-Slot) |
| Slot-Arten | `decide`, `apply`, `projektion`, `reaktion`, `reader`, `store`, `funktion`, `fluss`, `saga`, `pipeline` (Altlast); Python: `zusage`, `funktionpy` (§4) |
| Abschnitte | AUFTRAG · ANKER · SIGNATUR · VERTRAG · ERREICHBAR · KOMMENTARE · TYPEN · GRAPH-UMFELD · NACHBAR-CODE-BLÖCKE (alle der Klasse, inkl. Helfer) · KOPPLUNG |
| Kopplung | Decide → Apply der persistenten Ausgänge · Apply → erzeugende Decide · Projektion/Reader → Store-Impl der aufgerufenen Funktionen · Store-Impl → aufrufende Handles |

**Bekannte Abweichungen:** Der Vertragssatz „Ablehnung nur allein" ist fester Text je Marker (die Prüfung steht in `AggregateActorBase`,
wird aber nicht ausgelesen). Domänen-Helfer ohne Kante (Beispieldomäne: `SplitZuteiler`) erscheinen nicht.

---

## 10 · Verworfen

- **Szenarien / Gegeben–Wenn–Dann** als Kontext oder Spezifikation: nicht aus dem Domänen-Code ableitbar.
- **Handgeschriebene Regelblöcke** (Reinheit, Idempotenz …) und BCL-Listen: Prosa, nicht erzwungen.
- **Relevanz-Auswahl** über Namensähnlichkeit: Heuristik.
- **Code oder JSON als Kontext**: 3–6× größer als das Graph-Skelett.

---

## 11 · Offen

**Fachlich prüfen (Plan, `docs/13` LLM-1).** Heute ist ein 🤖-Rumpf geprüft, wenn er baut und die Analyzer schweigen (§2, Schritt 4) — ob er tut,
was der Auftrag sagt, prüft nichts. Die Slots sind dafür gemacht, isoliert getestet zu werden: Decide und Apply sind reine Funktionen, Regeln und
Fluss-Lambdas sind Daten, Katalog-Funktionen haben genau einen Auftrag und ein OneOf-Ergebnis, Projektion/Reaktion/Reader bekommen ihre
Store-Fähigkeiten als Parameter (CQRS051/054). Der Weg:

| Slot | isolierter Test |
|---|---|
| `decide`, `apply` | `Cqrs.Testing.Szenario`: Gegeben(Events) → Wenn(Command) → Dann(Events \| Ablehnung) bzw. erwarteter State |
| `saga` (Prozess-Regel), `fluss`, `pipeline` | `SagaSzenario` (lernt `Rufe`, `docs/13` P3-3), `FlussLaufwerk` mit gewähltem Ausgangsfall je Funktion |
| `funktion` | Auftrag → genau ein OneOf-Ergebnis; Lese-Fähigkeiten als kleine Testfunktion |
| `projektion`, `reaktion`, `reader` | aufzeichnende Fähigkeiten (aus den Signaturen generiert, keine Reflection) → Aufrufe bzw. Ausgaben prüfen |
| `funktionpy`, `zusage` | pytest gegen die generierte Basis (`rufe` bzw. `auf_<event>`) |
| `store` | **nicht isoliert**: nur gegen echtes Marten (Integration, Vorlage `Pruefdomaene.Integration.Tests`) |

1. **Beispiele sind Code.** An der Karte eine Tabelle Gegeben/Wenn/Dann je Slot; gespeichert als Testcode in der neuen Rolle `<Wurzel>.Beispiele`
   (`Abstractions/Projektstruktur.cs`, docs/07) — Code ist die Wahrheit (W1), ↶ nimmt auch ein Beispiel zurück.
2. **Beispiele sind unabhängig vom Rumpf.** Sie kommen vom Menschen, aus einem getrennten LLM-Aufruf, der nur Signatur und Auftrag sieht (nie den
   Rumpf), oder aus der Spur eines echten Sim-Laufs (Event-Folge + Command + beobachtete Events → Regressions-Szenario). Sonst bestätigt sich das LLM
   selbst.
3. **Die 🤖-Schleife wird „baut → regelkonform → Beispiele grün“.** Rote Beispiele gehen als Befund in die nächste Runde. Ausgeführt wird in einem
   eigenen Prozess (`dotnet test --filter` auf das Beispiel-Projekt bzw. pytest) — die Werkbank lädt keine Assemblies (W10).
4. **Ohne Beispiel** zeigt die Karte „ungeprüft“; ⟳ meldet die Zahl ungeprüfter 🤖-Rümpfe.

- **Kartenwand:** der Rumpf darf nur Symbole benutzen, die der Kontext deklariert (Semantic Model) — nicht umgesetzt.
- Die Kontexte entstehen kalt (`GraphExtractor --kontexte`, ~1 min; `docs/13` WB-1); bis dahin sind sie nach einer Übernahme veraltet.

---

## 12 · Konsole und Anbieter

```bash
dotnet run --project Werkbank         # → http://localhost:5178/editor  (Details: /konsole)
```

Die Werkbank hält den Code im warmen Workspace und erzeugt fehlende LLM-Kontexte beim Start im Hintergrund.

**Anbieter: ausschließlich Claude Code** (`Werkbank/LlmKonsole.cs`, `ClaudeCliAnbieter`) — `claude -p --output-format json`
im leeren Temp-Verzeichnis, feste Anweisung + Kontext über stdin. Abgerechnet wird über die **Anmeldung dieses Rechners (Abo), nie über die
API**: `ANTHROPIC_API_KEY`/`ANTHROPIC_AUTH_TOKEN` werden dem Kindprozess entzogen, ein gesetzter `ANTHROPIC_API_KEY` blockiert den Aufruf.
Umgebung: `BRACTOR_CLAUDE` = Pfad der CLI (Standard `claude` aus dem PATH), `BRACTOR_LLM_MODELL` = optional das Modell. Einmalig nötig: die
CLI im Terminal starten und `/login` (Abo) ausführen — sonst meldet der Aufruf „Not logged in“.

### 12.1 · Im Editor

„🤖 füllen“ ist eine Aktion am Rumpf-Port einer Karte und läuft als der Durchgang aus §2. Der Rumpf-Port zeigt den Rumpf, wie der warme
Workspace ihn liest (konzept-werkbank §8.4); eine Übernahme ist ein Eintrag im ↶-Verlauf wie jede Geste. „▶ In der LLM-Konsole füllen“
öffnet denselben Slot in der Konsole (`/konsole#id=…&auftrag=…`).

### 12.2 · Die Konsole (`/konsole`)

Dieselben Schritte wie §2, einzeln bedienbar (`Werkbank/KonsoleSeite.cs`): **Füllen** (bis zu drei geprüfte Runden), **Anpassen**
(Wunschtext → neue Runde mit aktuellem Rumpf), **Übernehmen** (Hash-Sperre, Rumpf-Geste, danach Bauen) und **Rückgängig** (das ↶ der
Werkbank: nimmt den letzten Patch zurück).

**Protokoll:** jede Runde mit Token (Eingabe, aus Cache, Ausgabe) in `.llm-kontext/protokoll.jsonl`. Im Browser zeigt **Protokoll** (bzw.
`/konsole#protokoll`) alle Aufrufe, neueste zuerst, mit Summen, Auftrag, Befunden und aufklappbarem Inhalt; je Slot zeigt „Verlauf dieses
Slots" dieselben Zeilen, jeder frühere Rumpf ist „als Kandidat laden" zurückholbar. Daten: `GET /api/llm/protokoll?id=&max=` (ohne
Anbieter-Zahlen Schätzung ≈ Zeichen/3,3).
