# Konzept — LLM-Füllung von Code-Blöcken: Kontext nur aus dem Code

> **Gekürzt 2026-10-12** auf das mit dem Zielbild `docs/konzept-werkbank.md` Vereinbare.

> **Stand:** 2026-09-27 · **Status:** GEBAUT — Kontext-Erzeugung für alle Slot-Arten (§9) und die LLM-Konsole im SimHost: Füllen → Prüfen → Anpassen → Übernehmen (§12). Offen: Kartenwand, Vergleichstest auf der Zielhardware.
> **Ziel:** Ein lokales LLM (Zielhardware: RTX 4080 Super, 16 GB; Modell: Qwen3.8-27B) schreibt **nur den Rumpf** eines
> Code-Blocks. Alles, was es dafür wissen muss, wird **regelbasiert aus dem Domänen-Code und dem Graphen** abgeleitet —
> nichts ist ausgedacht, nichts semantisch geraten. Die **einzige** menschliche Eingabe ist der Auftrag (🤖 füllen).
>
> Verwandt: [konzept-domaenen-editor.md](konzept-domaenen-editor.md) (D/S/H, §10.5 Code-Fakten).

---

## 1 · Grundsätze

1. **Auslöser ist die Aktion 🤖 füllen am Rumpf-Port einer Karte.** Ohne diese Aktion passiert nichts.
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
🤖 füllen (intent) am Rumpf-Port ──▶ Slot (Decide, Apply, Projektion, …)

 1. Slot auflösen   Rumpf-Port → Slot-Art + Anker (Typ, Methode, Datei)             [Regel je Slot-Art, §4]
 2. Kontext bauen   fester Teil (Graph-Skelett) + Slot-Teil (§4) + intent             [§3]
 3. LLM aufrufen    Antwort = Rumpf  |  AUSSERHALB: braucht …
 4. Prüfen          gegen das echte Projekt (ProjektPruefung) bzw. Bauen
                    + Kartenwand: der Rumpf benutzt nur Symbole, die der Kontext deklariert
                    Fehler → nur die Befunde zurück an das LLM, max. 3 Runden, dann Mensch
 5. Schreiben       Rumpf + // 🤖 Prompt: <intent> in die Datei (CodeSync, Hash-Sperre), Diff zur Freigabe
```

---

## 3 · Aufbau des Kontexts

```
┌─ FESTER TEIL — für alle Slots gleich, einmal pro Sitzung verarbeitet (KV-Prefix-Cache) ───── ~9.000 Token
│  Anweisung (Ausgabeformat: nur Rumpf oder AUSSERHALB)
│  Graph-Skelett: alle Aggregate/Commands/Events/Prozesse/Stores/Projektionen/Reader,
│                 als Signaturen + Kanten (seit 2026-09-30 ohne Guards — Rumpf-Fakten fließen nicht mehr ein)
├─ SLOT-TEIL — je Slot ──────────────────────────────────────────────────────────── ~1.500–5.000 Token
│  Auftrag (intent) · Anker · Vertrag · Spielraum · Graph-Umfeld · Nachbar-Code-Blöcke · Kopplung
└─ Antwort ──────────────────────────────────────────────────────────────────────── ~1.000 Token
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
4. Erreichbar ist sonst nichts: der Generator erzeugt `new X.Decider(state)` (`FactoryGenerator.cs:51`) [S]
5. Herkunft des Commands: Client · Prozess X Regel i (Wenn-Events) · Fluss-Knoten Y [K]
6. Abnehmer je Ausgang: Apply-Slot (Status) · Projektionen · Prozesse (Auslöser/Bedingung) · Fluss-Knoten [K]
7. ~~Guards anderer Decide~~ (entfernt 2026-09-30: kein Rumpf-Fakt im Kontext)
8. Datenfluss: welche Decide welches State-Member lesen, welche Apply es schreiben [N]
9. Vertrag: Ausgänge ⊆ OneOf (Compiler); eine Ablehnung steht allein (Laufzeit, `AggregateActorBase.cs:306`) [S]

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
5. Präzedenz: im Bestand keine Reaktion (0) [N]

### Reader.Handle(Query, Umschlag, ReadContext) — `IReader<T>`, 1. Parameter `IQuery`
1. Signatur, Query-Felder, OneOf-Antworten mit Konstruktoren [S]
2. Gebundene Projektion; `TrackDeps` (ob `ctx.Track` nötig) [S]
3. Verdrahtete Read-Funktionen mit Signatur/Rückgabe; ReadModels des Stores [K]
4. ❌ Helfer anderer Klassen (`ImagePairReader.ToAntwort`) haben keine Kante

### Store-Impl (je Fähigkeit) — Methode, die die eine Funktion einer `IWriteStore`/`IReadStore`-Fähigkeit implementiert
1. Signatur (Parameter, Rückgabe, Write/Read) [S]
2. ReadModels des Stores [K]
3. Basis-Primitive (`Enqueue`, `EnqueueStore`, `EnqueueTransform`) bzw. injizierter Dokument-Store [S]
4. Aufrufer: Projektions-/Reader-Handles, die die Funktion rufen, samt deren Achse [K]

### Weitere Slot-Arten (Anker-Schlüssel `Art|Besitzer|Disc`, aus `SlotInventar.cs` / `LlmKontext.cs`)
- **Funktion** `funktion|<Funktions-Schnittstelle>|<Auftrag>` — Rumpf der Implementierung von `RufeAsync` einer Katalog-Funktion
  (`IFunktion`); Vertrag: genau ein Ergebnis aus dem OneOf (CQRS068), Fähigkeits-Parameter nur lesend; Umfeld: rufende Fluss-Knoten und Prozesse.
- **Fluss** `fluss|<Fluss-Klasse>|<Knoten>#<Lambda-Index>` — ein Bau-Lambda eines Pipeline-Flusses (Lambda-Argument eines Fluss-Verbs je
  Anweisung des `Definiere`-Lambdas der Property `Fluss`); rein und deterministisch; Umfeld: der ganze Fluss wörtlich.
- **Saga** `saga|<Prozess-Klasse>|<Anweisung>#<Verb>` — ein Lambda-Argument eines Regel-Verbs (Sende/Rufe/SendeJe, RückgängigDurch…,
  UndAlle) im `Definiere`-Lambda von `Regeln`; rein und deterministisch; Umfeld: alle Regeln des Prozesses wörtlich.
- **Zusage** `zusage|<Client>|<Event>@<Klasse>` — Python-Rumpf einer Client-Implementierung (Klasse, die die generierte Basis erbt) je
  Zusage/Kenntnis; Signatur aus der Basis; Vertrag: eine Zusage liefert nur ihre Ausgaben per `yield`, eine Kenntnis nichts; erreichbar
  Event-DTO, `ctx`, `state`, `self`; Nachbar-Code = die Datei ohne diesen Rumpf.

---

## 5 · Nachbar-Code-Blöcke

Die Typen sagen, **was** verfügbar ist; nur Nachbar-Rümpfe zeigen, **wie** es in diesem System benutzt wird
(z. B. `writer.Execute(…, ctx => { ctx.Track<Agg>(…); … })` in 36/36 Projektions-Handles, die Marten-Teilmenge der Stores).

**Regel:** Mitgegeben werden **alle** Code-Blöcke derselben Klasse (Decider, Applier, Projektion, Reader,
Store-Impl) außer dem eigenen — keine Auswahl. Dazu die Rümpfe der gekoppelten Slots (§4, [K]/[N]).

**Kosten (gemessen, Qwen-BPE):**

| Slot-Art | Gruppen | Nachbar-Rümpfe je Slot, Median | Maximum |
|---|---:|---:|---:|
| Decide | 6 | 588 | 729 (ImagePair, 9 Rümpfe) |
| Apply | 6 | 159 | 391 |
| Projektion | 5 | 424 | 1.014 (ImagePairHistorieProjection) |
| Reader | 5 | 128 | 644 |
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
| Store-Impl | 56 | 63 % | 37 % | – | – | 29/56 | 3/56 |

- Der Wortschatz kommt aus Signatur + Graph; ohne Kanten fehlen Aggregat für `Track<Agg>`, gesendete Commands, ReadModels.
- „Von außen" sind genau die ❌-Lücken aus §4 (Helfer eines fremden Readers).
- Decide/Apply sind vollständig isoliert.

---

## 8 · Randfälle von Aufträgen

(a) im Spielraum lösbar · (b) braucht Strukturänderung im Editor → Antwort `AUSSERHALB` · (c) mehrere Slots gekoppelt ·
(d) falscher Konsument.

| # | Stelle | Auftrag (Beispiel) | Klasse | Was stattdessen passiert | Beleg im Bestand |
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
| 16 | Store-Impl | Filter/Paging | a | Basis-Primitive + ReadModel; Marten-Nutzung aus Nachbarn | `ImagePairStorePostgres` |
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
| Graph-Skelett | aus dem Editor-Modell (`ModellMapper.ZuBoardJson`); nur Signatur-Fakten, keine Guards |
| Auftrag | 1. `// 🤖 Prompt:` im Rumpf, 2. `--auftrag` (nur Einzel-Slot) |
| Slot-Arten | Decide, Apply, Projektion, Reaktion, Reader, Store-Impl, Funktion, Fluss, Saga, Zusage (§4) |
| Abschnitte | AUFTRAG · ANKER · SIGNATUR · VERTRAG · ERREICHBAR · KOMMENTARE · TYPEN · GRAPH-UMFELD · NACHBAR-CODE-BLÖCKE (alle der Klasse, inkl. Helfer) · KOPPLUNG |
| Kopplung | Decide → Apply der persistenten Ausgänge · Apply → erzeugende Decide · Projektion/Reader → Store-Impl der aufgerufenen Funktionen · Store-Impl → aufrufende Handles |

**Gemessen (Qwen-BPE):** Skelett 8.879 Token. Slot-Teil Median/Max: Decide 2.285/2.873 · Apply 1.702/2.000 ·
Projektion 2.613/2.949 · Reader 2.939/4.715 · Store-Impl 2.650/5.679. Größter Aufruf inkl. 1.000 Token Antwort: 15.558 — innerhalb des Budgets (§6).

**Bekannte Abweichungen:** Der Vertragssatz „Ablehnung nur allein" ist fester Text je Marker (die Prüfung steht in
`AggregateActorBase.cs:306`, wird aber nicht ausgelesen). Domänen-Helfer ohne Kante (`SplitZuteiler`) erscheinen nicht. Schreiben für alle Slot-Arten: `CodeSync.SetzeRumpf` (§12).

---

## 10 · Verworfen

- **Szenarien / Gegeben–Wenn–Dann** als Kontext oder Spezifikation: nicht aus dem Domänen-Code ableitbar.
- **Handgeschriebene Regelblöcke** (Reinheit, Idempotenz …) und BCL-Listen: Prosa, nicht erzwungen.
- **Relevanz-Auswahl** über Namensähnlichkeit: Heuristik.
- **Code oder JSON als Kontext**: 3–6× größer als das Graph-Skelett, sprengt auf 16 GB das Budget.

---

## 11 · Nächste Schritte

1. Echter Lauf mit `claude -p` (Abo) bzw. dem lokalen Qwen-Server; Token-Protokoll auswerten (greift der Präfix-Cache?).
2. Kartenwand: der Rumpf darf nur Symbole benutzen, die der Kontext deklariert (Semantic Model).
3. Prüfen gegen das echte Projekt (`ProjektPruefung`) vor dem Übernehmen für alle Slot-Arten.

---

## 12 · LLM-Code-Blöcke: im Editor ausführen, testen, übernehmen (SimHost)

### 12.1 · Im Editor: der ganze Weg mit 🤖 füllen am Rumpf-Port (Hauptweg, EIN Klick)

```
🤖 füllen ─▶ [▶ Senden] ─▶ claude -p (Abo) ─▶ Prüfung (Syntax; gegen das echte Projekt, ≤ 3 Reparaturrunden)
                                        ├─ geprüft ─▶ .cs schreiben (+ „// 🤖 Prompt:“) ─▶ Build des Laufzeit-Projekts
                                        │             (ALLE Code-Generatoren inkl. Proto/STJ-Prepass) ─▶ Code + Kontexte neu
                                        │             einlesen ─▶ ↶ Rückgängig
                                        └─ nicht geprüft ─▶ ↻ Anpassen · ✓ trotzdem übernehmen · ✗ Verwerfen
```

- **🤖 füllen ist ein Chat:** Eingabe + **▶ Senden** (Cmd/Ctrl+Enter). Die 1. Nachricht ist der Auftrag, jede weitere passt den
  aktuellen Rumpf an (Anpassung). Jede Nachricht ist ein vollständiger Durchlauf. In der Datei steht immer genau EINE Prompt-Zeile.
- **Senden** = `POST /api/llm/ausfuehren`. Ein geprüfter Rumpf landet **sofort** in der Datei; danach baut der SimHost das **Laufzeit-Projekt**
  (`index.json` → `laufzeitProjekt`, vom Extractor abgeleitet: das Projekt mit der generierten Routing-Tabelle). Sein Build
  zieht die ganze Kette: Domain-Generatoren, Codegen-Prepass (ProtoRepo), Projektions-DI, Infrastructure-Generatoren.
- Nach dem Schreiben **ein** GraphExtractor-Lauf (`/api/editor/extract` = `--kontexte`); solange sind ▶/✓ gesperrt.

**Live geprüft (2026-09-29, Stellvertreter-CLI über `BRACTOR_CLAUDE`):** ▶ für `Decide(StarteSammelvorgang)`
→ 1 Runde geprüft → `Domain/Sammelvorgang/Decider.cs:13` geschrieben → `Infrastructure.csproj` grün (≈ 50 s) → eingelesen
(≈ 60 s) → Meldung „✓ geschrieben … · Generatoren grün“.

**Synchron halten — die Datei ist die einzige Wahrheit:**

| Was | Quelle | Mechanik |
|---|---|---|
| Rumpf am Rumpf-Port | `.cs` | Spiegel für **jede** Slot-Art: Decide/Apply über den Code-Anker, alle übrigen über `GET /api/llm/rumpf?id=` (Slot-Schlüssel → Datei · Klasse · Methode). Ein Hash-Wechsel (auch aus VS Code) aktualisiert die Anzeige. |
| Auftrag | `// 🤖 Prompt:` in der Datei | Decide/Apply sofort beim Tippen, alle Arten beim Übernehmen. |
| Kontexte + `domain-model.json` | Code | ein gemeinsamer Lauf nach jedem Schreiben; fehlen sie beim Start, erzeugt SimHost sie selbst im Hintergrund. |

**Geprüft (Fake-Anbieter, ohne API-Kosten, im echten Editor per Playwright):** Übernehmen: geschrieben + Build grün (13 s),
Neu-Einlesen ≈ 1 min; Rückgängig stellt die Datei wörtlich her.

### 12.2 · Die Konsole (`/konsole`) — Details, Prompt, Verlauf, Protokoll


```bash
dotnet run --project GraphExtractor   # editor.html + domain-model.json (die Kontexte erzeugt SimHost bei Bedarf selbst)
dotnet run --project SimHost          # → http://localhost:5178/editor  (Details: /konsole)
```

**Anbieter: ausschließlich Claude Code** (`SimHost/LlmKonsole.cs`, `ClaudeCliAnbieter`) — `claude -p --output-format json`
im leeren Temp-Verzeichnis, feste Anweisung + Kontext über stdin (nur Schalter, die jede CLI-Version kennt). Abgerechnet wird
über die **Anmeldung dieses Rechners (Abo), nie über die API**: `ANTHROPIC_API_KEY`/`ANTHROPIC_AUTH_TOKEN` werden dem
Kindprozess entzogen, ein gesetzter `ANTHROPIC_API_KEY` blockiert den Aufruf. Umgebung: `BRACTOR_CLAUDE` = Pfad der CLI
(Standard `claude` aus dem PATH), `BRACTOR_LLM_MODELL` = optional das Modell. Einmalig nötig: die CLI im Terminal starten
und `/login` (Abo) ausführen — sonst meldet der Aufruf „Not logged in“.

**Ablauf je Code-Block** (`SimHost/LlmKonsole.cs`, Seite `SimHost/KonsoleSeite.cs`):

1. **Füllen** — jede Runde ein neuer, zustandsloser Aufruf. Prompt stabil → veränderlich: feste Anweisung · Graph-Skelett
   (eigene Guards entfernt) · Slot-Teil (ohne Auftrags-Abschnitt) · Auftrag · [aktueller Rumpf · Befund bzw. Anpassung].
   Nie ein Verlauf; nur der letzte Rumpf reist mit.
2. **Antwort** — ein ```csharp-Block (Rumpf ohne äußere Klammern) oder `AUSSERHALB: braucht …` (→ Editor-Struktur).
3. **Prüfen** — Syntax für alle Slot-Arten; dann gegen das **echte Projekt**
   (`SimHost/ProjektPruefung.cs`: Rumpf im Speicher eingesetzt, Referenzen wie im Build aus `project.assets.json`, mit den
   Store-Regeln CQRS066/067 — ein Schreibweg am Puffer vorbei wird so Befund, bevor er in die Datei geht). Heute für
   Store-Funktionen, Ziel für alle Slot-Arten (§11); übrige: Compile beim Bauen nach dem Übernehmen. Bei Befund automatisch
   nächste Runde (max. 3). Ohne LLM prüfbar:
   `POST /api/llm/pruefen {id, rumpf}` bzw. `dotnet run --project SimHost -- --pruefe-rumpf <Slot-Id> <Datei>`.
4. **Anpassen** — Wunschtext → neue Runde mit Kontext + Auftrag + aktuellem Rumpf + Anpassung.
5. **Übernehmen** — in der Konsole auf Klick (im Editor automatisch nach bestandener Prüfung): `CodeSync.SetzeRumpf` schreibt Rumpf + `// 🤖 Prompt: <Auftrag>` (Ausdrucks-Rumpf → Block),
   Hash-Sperre gegen Zwischenänderungen, Sicherung in `.llm-kontext/sicherung/`. Danach **Generatoren bauen**
   (`dotnet build` des Laufzeit-Projekts; ohne diese Angabe das `.csproj` der Datei) und **Rückgängig** (stellt den alten Rumpf wörtlich wieder her).
6. **Protokoll** — jede Runde mit Token (Eingabe, aus Cache, Ausgabe) in `.llm-kontext/protokoll.jsonl`.
   Im Browser: Knopf **Protokoll** (bzw. `/konsole#protokoll`) zeigt alle Aufrufe, neueste zuerst, mit Summen
   (Aufrufe, geprüft, übernommen, Eingabe-/Cache-/Ausgabe-Token, Dauer), Auftrag, Befunden und aufklappbarem Inhalt; ein
   Klick auf den Slot öffnet ihn. Je Slot zeigt der Kasten „Verlauf dieses Slots" dieselben Zeilen, jeder frühere Rumpf ist
   „als Kandidat laden" zurückholbar. Daten: `GET /api/llm/protokoll?id=&max=` (Token normalisiert: Eingabe = nicht
   gecacht, bei OpenAI `prompt_tokens − cached_tokens`; ohne Anbieter-Zahlen Schätzung ≈ Zeichen/3,3).

Aus dem Editor: an der Aktion 🤖 füllen „▶ In der LLM-Konsole füllen" (öffnet `/konsole#id=…&auftrag=…`); alle 172 Code-Blöcke
des Editor-Modells finden ihren Slot (Store-Slots mit mehreren Implementierungen: die erste, wählbar in der Liste).

**Getestet (ohne echtes Modell):** Übernehmen schreibt korrekt eingerückt; Projekt baut; zweites Übernehmen mit altem Hash wird abgelehnt; Rückgängig stellt Datei wörtlich her (auch
Ausdrucks-Rumpf `=> …` ↔ Block). OpenAI-Weg gegen einen Fake-Server: `AUSSERHALB` erkannt, Token inkl. Cache gelesen.

**Grenzen:** Prüfen gegen das echte Projekt vor dem Übernehmen erst für Store-Funktionen (sonst Syntax + Bauen danach). Nach dem Übernehmen sind die
Kontexte veraltet („Kontexte neu erzeugen", ≈ 1 min). Kartenwand (Symbol-Allowlist) noch nicht.
