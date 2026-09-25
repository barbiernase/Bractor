# Konzept — LLM-Füllung von Code-Blöcken: Kontext nur aus dem Code

> **Stand:** 2026-09-25 · **Status:** Kontext-Erzeugung GEBAUT für alle Slot-Arten (`--kontext`, `--kontexte`, §9); LLM-Aufruf, Prüfung und Schreiben (§2 Schritte 3–5) noch nicht.
> **Ziel:** Ein lokales LLM (Zielhardware: RTX 4080 Super, 16 GB; Modell: Qwen3.8-27B) schreibt **nur den Rumpf** eines
> Code-Blocks. Alles, was es dafür wissen muss, wird **regelbasiert aus dem Domänen-Code und dem Graphen** abgeleitet —
> nichts ist ausgedacht, nichts semantisch geraten. Die **einzige** menschliche Eingabe ist der Auftrag am LLM-Knoten.
>
> Verwandt: [konzept-domaenen-editor.md](konzept-domaenen-editor.md) (D/S/H, Code-/LLM-Knoten, §10.5 Code-Fakten) ·
> [analyse-editor-backend-abdeckung-2026-09-24.md](analyse-editor-backend-abdeckung-2026-09-24.md) (Sync-Architektur).

---

## 1 · Grundsätze

1. **Auslöser ist der LLM-Knoten.** Kontext entsteht nur, wenn im Editor ein 🤖 LLM-Knoten an einem Code-Block hängt
   (`MODEL.llmNodes[] = { name, intent, promptZiel }`, `promptZiel` → Code-Block). Ohne LLM-Knoten passiert nichts.
2. **Nur Code-Fakten.** Jede Kontext-Zeile kommt aus einem Symbol (Roslyn), einer Graph-/Editor-Kante, einem Nachbar-Rumpf
   oder wörtlich aus einem Kommentar. Keine Namensähnlichkeit, kein Ranking, keine Zusammenfassung, keine erfundene Prosa.
   Beschreibungen erscheinen nur, wo der Code einen Kommentar trägt.
3. **Der Auftrag ist die einzige menschliche Eingabe** (`intent`). Er landet versioniert als `// 🤖 Prompt:` im Rumpf.
4. **Vertragsregeln nur, wenn erzwungen.** Eine Regel steht im Kontext nur, wenn Compiler, Generator, Analyzer oder
   Framework-Laufzeit sie durchsetzen. Nicht erzwungene Regeln (z. B. „Decide ist rein") brauchen erst einen Analyzer.
5. **Das LLM schreibt nur den Rumpf** — oder antwortet `AUSSERHALB: braucht <Art> <Name>`, wenn der Auftrag nicht im
   Spielraum lösbar ist (neuer Ausgang, neues Feld, neue Kante). Strukturänderungen sind Editor-Arbeit (D/S), nie LLM-Arbeit.

---

## 2 · Ablauf

```
🤖 LLM-Knoten (intent) ──promptZiel──▶ Code-Block ──hängt an──▶ Slot (Decide, Apply, Projektion, …)

 1. Slot auflösen   Code-Block → Slot-Art + Anker (Typ, Methode, Datei)             [Regel je Slot-Art, §4]
 2. Kontext bauen   fester Teil (Graph-Skelett) + Slot-Teil (§4) + intent             [§3]
 3. LLM aufrufen    Antwort = Rumpf  |  AUSSERHALB: braucht …
 4. Prüfen          In-Memory-Compile mit den echten Generatoren (/api/editor/compile)
                    + Kartenwand: der Rumpf benutzt nur Symbole, die der Kontext deklariert
                    Fehler → nur die Befunde zurück an das LLM, max. 3 Runden, dann Mensch
 5. Schreiben       Rumpf + // 🤖 Prompt: <intent> in die Datei (CodeSync, Hash-Sperre), Diff zur Freigabe
```

---

## 3 · Aufbau des Kontexts

```
┌─ FESTER TEIL — für alle Slots gleich, einmal pro Sitzung verarbeitet (KV-Prefix-Cache) ───── ~9.000 Token
│  Anweisung (Ausgabeformat: nur Rumpf oder AUSSERHALB)
│  Graph-Skelett: alle Aggregate/Commands/Events/Prozesse/Stores/Projektionen/Reader/Pipelines,
│                 als Signaturen + Kanten + Guards; die Guards des EIGENEN Slots werden entfernt
├─ SLOT-TEIL — je Slot ──────────────────────────────────────────────────────────── ~1.500–5.000 Token
│  Auftrag (intent) · Anker · Vertrag · Spielraum · Graph-Umfeld · Nachbar-Code-Blöcke · Kopplung
└─ Antwort ──────────────────────────────────────────────────────────────────────── ~1.000 Token
```

**Quellen-Kennzeichnung** in §4: **[S]** Symbol (Deklaration/Signatur) · **[K]** Kante (Editor-Verdrahtung bzw. Graph;
bei leerem Rumpf kommt sie aus dem Editor, nicht aus dem Code) · **[N]** Nachbar-Rumpf · **[L]** LLM-Knoten ·
**❌** heute nicht im Graphen.

---

## 4 · Informationen je Slot-Art

### Für alle Slot-Arten
1. Auftrag: `intent` des LLM-Knotens [L]
2. Graph-Skelett (§3) [S+K]
3. Anker: Datei, Klasse, Methode, Rumpf-Status (leer / Stub / geschrieben) [S]
4. Doku- und Zeilenkommentare an Methode und Eingangstyp, wörtlich [S]
5. Transitive Hülle der Domänen-Typen: Records mit Konstruktor, Enums mit Werten, berechnete Member als Ausdruck, `///`-Texte [S]
6. **Alle Code-Blöcke derselben Klasse** (§5) [N]

### Decide(Command) — gefunden über `IDecider<T>`, 1. Parameter `ICommand`
1. Signatur + OneOf-Ausgänge; je Ausgang Event (`IEvent`) oder Ablehnung (`ITransientEvent`) [S]
2. Command-Felder, Erzeugungs-Command (`ICreationCommand`) [S]
3. State: alle Member mit Typ/Accessor, berechnete mit Ausdruck — nur lesbar [S]
4. Erreichbar ist sonst nichts: der Generator erzeugt `new X.Decider(state)` (`FactoryGenerator.cs:51`) [S]
5. Herkunft des Commands: Client · Prozess X Regel i (Wenn-Events) · Pipeline Y [K]
6. Abnehmer je Ausgang: Apply-Slot (Status) · Projektionen · Prozesse (Auslöser/Bedingung) · Pipelines [K]
7. Guards anderer Decide desselben Aggregats für denselben Ausgangstyp [N]
8. Datenfluss: welche Decide welches State-Member lesen, welche Apply es schreiben [N]
9. Vertrag: Ausgänge ⊆ OneOf (Compiler); eine Ablehnung steht allein (Laufzeit, `AggregateActorBase.cs:306`) [S]

### Apply(Event) — `IApplier<T>`, 1. Parameter `IEvent`
1. Signatur + Event-Felder [S]
2. State: alle Member, schreibbar/berechnet; Helfer-Methoden des Appliers [S]
3. Erzeugende Decide samt Guard [K]
4. Weitere Abnehmer des Events: Projektionen, Prozesse, Pipelines [K]
5. Datenfluss: welche Member andere Apply schreiben, welche Decide sie lesen, welche kein anderer Apply schreibt [N]
6. Ziele gleichen Typs je Event-Feld; Konstruktor, der exakt die Typfolge der Event-Felder nimmt [S]

### Projektion.Handle(Event, Umschlag, Writer) — `ISubscriber`, Parameter `IEvent` + `IAggregateEnvelope`
1. Signatur, Event-Felder, Umschlag (`AggregateId`, `CreatedAtUtc`) [S]
2. Achsen: geordneter Pull / Signal; Upsert (at-least-once, idempotent schreiben) / append-artig (Co-Commit, exactly-once, GA-1) [S]
3. Store-Scope: nur die verdrahteten Write-Funktionen mit Signatur (Handle → Fn) [K]
4. ReadModels des Stores mit Feldern [K]
5. Aggregat, das das Event erzeugt (für `Track<Agg>`) [K]
6. Veröffentlichtes reaktives Event, falls verdrahtet [K]
7. Kopplung: Store-Impl der aufgerufenen Funktionen [K]

### Reaktion.Handle(Event, …) — wie Projektion, Rückgabe `IAsyncEnumerable<OneOf<…>>`
1. Signatur, Event-Felder, Umschlag [S]
2. Ausgänge: gesendete Commands / veröffentlichte Events [S/K]
3. Ziel-Aggregat und Felder jedes gesendeten Commands [K]
4. Achse emittierend (`IEmittentenCursor`, kein Reset); Senden nur per `yield` (CQRS020/021) [S]
5. Präzedenz: im Bestand keine Reaktion (0) [N]

### Reader.Handle(Query, Umschlag, ReadContext) — `IReader<T>`, 1. Parameter `IQuery`
1. Signatur, Query-Felder, OneOf-Antworten mit Konstruktoren [S]
2. Gebundene Projektion; `TrackDeps` (ob `ctx.Track` nötig) [S]
3. Verdrahtete Read-Funktionen mit Signatur/Rückgabe; ReadModels des Stores [K]
4. ❌ Helfer anderer Klassen (`ImagePairReader.ToAntwort`) haben keine Kante

### Pipeline.Handle(Eingang, PipelineContext) — `IPipelineHandler`, Parameter `PipelineContext`
1. Signatur, Eingangsfelder, Kanal (Trigger / Event / Ablehnung / Self) [S]
2. `ctx.SourceAggregateId`, `ctx.ScheduleSelf` [S]
3. Gesendete Commands, erzeugte Trigger, geplante Self-Ticks [K]
4. Dienst-Verträge (Signaturen), Konfig-Records mit HostSettings, Trigger-Quelle bzw. Frist [K]
5. Klassenfelder, die mehrere Handles benutzen (geteilter, verlierbarer Zustand — Inv. 6) [S/N]
6. Injizierte Felder mit Kategorie über Marker (Store / Konfig / Domänen-Dienst / Framework / Bibliothek) — damit ist auch
   der Read-Store-Zugriff sichtbar (`DatensatzResolverPipeline._imagePairStore [Store]`) [S]. ❌ Im Graphen/Editor fehlt diese Kante weiterhin.
7. ❌ Domänen-Helfer (`SplitZuteiler`, `ImagePairFileName`) sind nicht als Dienst verdrahtet

### Store-Impl (je Write-/Read-Funktion) — Methode, die eine Funktion eines `IWriteStore`/`IReadStore`-Interfaces implementiert
1. Signatur (Parameter, Rückgabe, Write/Read) [S]
2. ReadModels des Stores [K]
3. Basis-Primitive (`Enqueue`, `EnqueueStore`, `EnqueueTransform`) bzw. injizierter Dokument-Store [S]
4. Aufrufer: Projektions-/Reader-Handles, die die Funktion rufen, samt deren Achse [K]

### Ohne LLM-Slot
Regel (`Sende`-Argumente: Feldabbildung, D/S laut Editor-Konzept §5) · Trigger · Frist · HostSetting. Dienst-Impl nur,
wenn nicht extern (dann: Vertrag + nutzende Pipelines).

---

## 5 · Nachbar-Code-Blöcke

Die Typen sagen, **was** verfügbar ist; nur Nachbar-Rümpfe zeigen, **wie** es in diesem System benutzt wird
(z. B. `writer.Execute(…, ctx => { ctx.Track<Agg>(…); … })` in 36/36 Projektions-Handles, die Marten-Teilmenge der Stores).

**Regel:** Mitgegeben werden **alle** Code-Blöcke derselben Klasse (Decider, Applier, Projektion, Reader, Pipeline,
Store-Impl) außer dem eigenen — keine Auswahl. Dazu die Rümpfe der gekoppelten Slots (§4, [K]/[N]).

**Kosten (gemessen, Qwen-BPE):**

| Slot-Art | Gruppen | Nachbar-Rümpfe je Slot, Median | Maximum |
|---|---:|---:|---:|
| Decide | 6 | 588 | 729 (ImagePair, 9 Rümpfe) |
| Apply | 6 | 159 | 391 |
| Projektion | 5 | 424 | 1.014 (ImagePairHistorieProjection) |
| Reader | 5 | 128 | 644 |
| Pipeline | 5 | 102 | 551 |
| Store-Impl | 5 | 343 | 2.488 (IImagePairWriteStore, 18 Rümpfe) |

Gibt es keine Nachbarn (erster Slot seiner Art in der Klasse), werden Slots derselben Art aus anderen Klassen genommen.

---

## 6 · Budget auf der Zielhardware

| Teil | Token (gemessen/geschätzt) |
|---|---:|
| Graph-Skelett (202 Zeilen, diese Domäne) | 8.815 |
| Slot-Teil ohne Nachbarn | ~1.500–2.400 |
| Nachbar-Code-Blöcke | ≤ 2.500 |
| Antwort | ~1.000 |
| **Summe** | **~14.000–15.000** |

Zum Vergleich: Domänen-Quelltext 60.639 · `domain-model.json` mit Rümpfen 50.324 · `knowledge-graph.json` 44.471 Token.

Qwen3.8-27B hat laut den Quellen 16 Schichten mit voller und 48 mit linearer Attention; nur die 16 füllen den KV-Cache
(~64 KB/Token). Auf 16 GB wird UD-Q3_K_XL (13,4 GB) empfohlen; Q4_K_M (17,1 GB) braucht CPU-Auslagerung. Mit Q3 bleiben
grob 1,5 GB KV-Cache → **~20.000–25.000 Token** (eigene Rechnung, nicht gemessen; mit q8-KV etwa doppelt). Das Budget reicht.
Der feste Teil ist für alle Slots gleich → llama.cpp/vLLM verwenden den KV-Cache des Präfixes wieder.

**Grenze:** ~1.500 Token Skelett je Aggregat. Ab ~15–20 Aggregaten wird das Skelett auf den Bounded Context des Slots
geschnitten (alles über Graph-Kanten Erreichbare) — regelbasiert, nicht heuristisch.

Quellen: [pasqualepillitteri.it](https://pasqualepillitteri.it/en/news/11335/qwen3-8-27b-run-local-16gb-lm-studio-unsloth) ·
[dev.to](https://dev.to/purpledoubled/run-qwen-38-27b-locally-real-gguf-sizes-the-kv-cache-trick-and-the-template-trap-114j) ·
[orcarouter.ai](https://www.orcarouter.ai/blog/qwen-3-8-27b-gguf)

---

## 7 · Messung: wie isoliert sind die Code-Block-Stellen?

`dotnet run --project GraphExtractor -- --slots` klassifiziert jedes Symbol jedes Rumpfs und prüft, woher das Wissen
kommen kann: Spielraum (Signatur, State, Felder, geerbte Member), Graph-Kante, nur Nachbar, oder von außen.

| Art | Slots | Spielraum | + Graph-Kante | nur Nachbar | von außen | injizierte Felder | geteilte Helfer |
|---|---:|---:|---:|---:|---:|---:|---:|
| Decide | 31 | 100 % | – | – | – | 0/31 | 0/31 |
| Apply | 33 | 100 % | – | – | – | 0/33 | 4/33 |
| Projektion | 36 | 90 % | 10 % | – | – | 36/36 | 0/36 |
| Reader | 17 | 98 % | – | – | 2 % | 17/17 | 0/17 |
| Pipeline | 11 | 65 % | 17 % | – | 19 % | 7/11 | 3/11 |
| Store-Impl | 56 | 63 % | 37 % | – | – | 29/56 | 3/56 |

- Der Wortschatz kommt aus Signatur + Graph; ohne Kanten fehlen Aggregat für `Track<Agg>`, gesendete Commands, ReadModels.
- „Von außen" sind genau die ❌-Lücken aus §4 (Domänen-Helfer in Pipelines, Helfer eines fremden Readers).
- Decide/Apply sind vollständig isoliert. Pipelines am wenigsten: Dienste, Konfiguration, geteilter Klassenzustand
  (`FileWatchPipeline._seen/_pending`) — dort ist die Klasse die Kontext-Einheit.

---

## 8 · Randfälle von Aufträgen

(a) im Spielraum lösbar · (b) braucht Strukturänderung im Editor → Antwort `AUSSERHALB` · (c) mehrere Slots gekoppelt ·
(d) falscher Konsument.

| # | Stelle | Auftrag (Beispiel) | Klasse | Was stattdessen passiert | Beleg im Bestand |
|---|---|---|---|---|---|
| 1 | Decide | braucht Read-Model-Daten | b | Zwei-Phasen-Muster: Event → Pipeline liest Store → Command mit Daten | `FuegeRangeHinzu` → `RangeAngefordert` → `DatensatzResolverPipeline` → `NimmRangeAuf` |
| 2 | Decide | braucht die Uhrzeit | b | Zeit als Command-Feld oder Frist (fällig → Command) | `TrainingFristPipeline` → `MarkiereAlsHaengengeblieben` |
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
| 13 | Pipeline | externer Dienst | b | Dienst-Knoten (Vertrag → Impl) | `ImageProcessingPipeline` |
| 14 | Pipeline | Zustand zwischen Aufrufen | c | ganze Klasse als Kontext; verlierbar (Inv. 6) | `FileWatchPipeline` |
| 15 | Pipeline | Domänen-Berechnung | b | Domänen-Helfer als Dienst verdrahten | `SplitZuteiler` |
| 16 | Store-Impl | Filter/Paging | a | Basis-Primitive + ReadModel; Marten-Nutzung aus Nachbarn | `ImagePairStorePostgres` |
| 17 | Regel | Ziel-Command braucht Feld, das kein Event trägt | b | Event-Feld oder Pipeline statt Regel | — |
| 18 | alle | Umbenennen / Signatur ändern | b | Editor-Struktur, kein Rumpf | — |
| 19 | alle | „wie bei Slot Y" | c | Y als Editor-Kante „Vorlage ◀" (nicht per Textsuche) | — |

Die Maschine versteht den Auftragstext nicht. Sie liefert den vollständigen Spielraum, liest strukturelle Signale aus
dem Editor (z. B. Query an einem Decider) und gibt dem LLM den Antwortkanal `AUSSERHALB`.

---

## 9 · Ist-Stand der Werkzeuge (gebaut)

```bash
dotnet run --project GraphExtractor -- --kontexte <verz>                       # 00-graph-skelett.txt + je Code-Block ein Slot-Teil + übersicht.md
dotnet run --project GraphExtractor -- --kontext SetzeSplit [--auftrag "…"]     # ein Slot-Teil auf die Konsole
dotnet run --project GraphExtractor -- --slots                                 # Isolations-Messung (§7)
```

`GraphExtractor/LlmKontext.cs` (`KontextBauer`, `KontextCli`), Slot-Erkennung aus `GraphExtractor/SlotInventar.cs`.

| Teil | Umsetzung |
|---|---|
| Graph-Skelett | aus dem Editor-Modell (`ModellMapper.ZuBoardJson`); beim eigenen Decide werden die Guards entfernt |
| Auftrag | 1. LLM-Knoten in `board-model.json` (`intent` → `promptZiel` → Code-Knoten → `codeSrc` des Slots), 2. `// 🤖 Prompt:` im Rumpf, 3. `--auftrag` (nur Einzel-Slot). Ohne Auftrag: Hinweis „kein LLM-Knoten" |
| Slot-Arten | Decide, Apply, Projektion, Reaktion, Reader, Pipeline, Store-Impl (184 Code-Blöcke im Bestand) |
| Abschnitte | AUFTRAG · ANKER · SIGNATUR · VERTRAG · ERREICHBAR · KOMMENTARE · TYPEN · GRAPH-UMFELD · NACHBAR-CODE-BLÖCKE (alle der Klasse, inkl. Helfer) · KOPPLUNG |
| Kopplung | Decide → Apply der persistenten Ausgänge · Apply → erzeugende Decide · Projektion/Reader → Store-Impl der aufgerufenen Funktionen · Store-Impl → aufrufende Handles |

**Gemessen (Qwen-BPE):** Skelett 8.879 Token. Slot-Teil Median/Max: Decide 2.285/2.873 · Apply 1.702/2.000 ·
Projektion 2.613/2.949 · Reader 2.939/4.715 · Pipeline 1.860/3.983 · Store-Impl 2.650/5.679. Größter Aufruf inkl. 1.000
Token Antwort: 15.558 — innerhalb des Budgets (§6).

**Bekannte Abweichungen:** Der Vertragssatz „Ablehnung nur allein" ist fester Text je Marker (die Prüfung steht in
`AggregateActorBase.cs:306`, wird aber nicht ausgelesen). Bei geschriebenen Slots stammen „Store-Aufrufe (verdrahtet)" aus
dem eigenen Rumpf, weil es noch kein Board gibt; bei leeren Slots kommen sie aus der Editor-Verdrahtung. Domänen-Helfer ohne
Kante (`SplitZuteiler`) erscheinen nicht. `CodeSync` löst weiterhin nur Decide/Apply-Anker auf.

---

## 10 · Verworfen

- **Szenarien / Gegeben–Wenn–Dann** als Kontext oder Spezifikation: nicht aus dem Domänen-Code ableitbar.
- **Handgeschriebene Regelblöcke** (Reinheit, Idempotenz …) und BCL-Listen: Prosa, nicht erzwungen.
- **Relevanz-Auswahl** über Namensähnlichkeit: Heuristik.
- **Code oder JSON als Kontext**: 3–6× größer als das Graph-Skelett, sprengt auf 16 GB das Budget.

---

## 11 · Nächste Schritte

1. LLM-Aufruf (OpenAI-kompatibler Endpunkt, lokaler Server) mit Präfix = Graph-Skelett, danach Slot-Teil.
2. Prüfung: In-Memory-Compile + Kartenwand; `AUSSERHALB`-Antwortkanal auswerten.
3. `CodeSync` für alle Slot-Arten (Rumpf schreiben + `// 🤖 Prompt:`).
4. Graph-Kanten Pipeline → Read-Store und Pipeline → Domänen-Helfer (Code-Fakt: Konstruktor-Parameter).
5. Vergleichstest auf der Zielhardware: Slot-Teil allein vs. mit Graph-Skelett.
