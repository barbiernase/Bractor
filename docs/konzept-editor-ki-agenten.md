# Konzept — Lokale KI-Agenten im Domänen-Editor

**Stand: 2026-08-16 · Konzept, nicht implementiert.**

Ziel: den Domänen-Editor (`/editor`, `GraphExtractor/HtmlPresenter.cs` + `DomainEditor` +
`SimHost`) so aufbohren, dass ein **lokal laufendes** Sprachmodell die heute leeren Rümpfe von
Decidern/Appliern schreibt — und darüber hinaus den Repository-/Projektions-Boilerplate. Dazu
kommen drei neue Knotentypen (`agent` · `prompt` · `code`) plus ein vierter, der das Ganze erst
tragfähig macht (`beispiel`).

Das Dokument hat drei Teile: **A** was heute steht (die Naht, an der man ansetzt), **B** was die
Hardware wirklich hergibt (RTX 4080 Super, Qwen3.8-27B — ehrlich gerechnet), **C** das Konzept.

---

## A — Wo die Naht schon liegt

Der Editor ist bereits ein *Datenmodell mit deterministischem Übersetzer*, nicht ein
Code-Texteditor. Das ist der Grund, warum ein LLM hier überhaupt sauber andocken kann.

```
EditorModell (JSON)  ──Scaffolder──▶  C# in kanonischer Gestalt
       │                                      │
       ├──Validator────▶ Form-Befunde         ├──ModellRuntime──▶ Roslyn + ECHTE Domain-
       │                                      │                   Generatoren, in-memory
       └──ModellMapper◀── C# (Round-trip)     └──SagaLaufwerk───▶ store-freier Lauf
```

Drei Eigenschaften davon sind für das Konzept entscheidend:

1. **Der Rumpf ist schon ein Feld.** `DecideRegel.Rumpf` und `ApplyRegel.Rumpf` sind
   `string?`. Ist er leer, erzeugt der `Scaffolder` einen kompilierbaren
   `throw new NotImplementedException("TODO: …")`-Platzhalter
   (`DomainEditor/Scaffolder.cs:287`). **Genau dieses Feld ist der Andockpunkt.** Ein Agent
   schreibt nie eine Datei, nie ein Projekt, nie einen Namespace — er schreibt einen
   *Anweisungsblock in einen vorgegebenen Rahmen*. Alles um ihn herum bleibt deterministisch.

2. **Der Regelkreis existiert schon.** `POST /api/editor/compile` übersetzt das Modell in-memory
   mit denselben Generatoren wie der Cluster (`SimHost/ModellRuntime.cs`), `POST /api/editor/run`
   fährt einen Command über den store-freien Kern (`SagaLaufwerk`), und `Cqrs.Testing`
   (`Szenario` / `SagaSzenario`) hat eine `Gegeben → Wenn → Dann`-DSL, die bei Fehlschlag den
   **kompletten Entscheidungs-Bericht** druckt (Zustand vorher → gewählter Zweig → nachher).
   Das ist bereits ein perfektes Self-Repair-Signal — es sagt nicht nur *dass* es falsch war,
   sondern *welchen Zweig* der Decider warum wählte.

3. **Der Knoten-Editor ist ComfyUI-förmig.** Getippte Slots (`port(farbe)` → `__slot`-Info),
   Bézier-Kanten, `compatible(a,b)` prüft `dir` und `type`, `applyLink(a,b)` mutiert das MODEL.
   Neue Knotentypen sind additiv: ein Eintrag in `NODELABEL`, eine `xxxCard(body, ref)`-Funktion,
   eine Farbe im CSS, ein Zweig in `neuerKnoten`/`delNode`/`graphNodes`/`applyLink`. Der Editor
   ist für genau diese Erweiterung gebaut.

Was der Editor heute **nicht** kennt: Projektionen, Reaktionen, Read-Models, Stores, Queries,
Pipelines. Das ist die zweite, größere Lücke — und der Ort, wo „Repositories schreiben lassen"
hingehört (siehe Ausbaustufe 2).

---

## B — Hardware-Realität: Qwen3.8-27B auf einer RTX 4080 Super

### B.1 Was Qwen3.8-27B ist (recherchiert, 2026-08-16)

| | |
|---|---|
| Veröffentlicht | **14.08.2026** (Familie angekündigt 03.08.2026), `Qwen/Qwen3.8-27B` auf Hugging Face |
| Lizenz | **Apache 2.0** — Gewichte offen, kommerziell nutzbar. *Open weight*, nicht *open source*: Trainingsdaten und -rezept sind nicht veröffentlicht |
| Größe | **27,78 Mrd. Parameter, dense** (kein MoE) |
| Architektur | hybride lineare + volle Attention, Vision-Turm, Multi-Token-Prediction-Head |
| Modalitäten | Text, Bild, Video |
| Kontext | nativ **262.144 Token** |
| Rohgewichte | 55,6 GB BF16 (18 Shards) |

Benchmarks (**Herstellerangaben von Alibaba, keine unabhängige Replikation**):

| Benchmark | Qwen3.8-27B | Vorgänger 3.6-27B |
|---|---:|---:|
| LiveCodeBench v6 | **90,3** | — |
| SWE-bench Pro | 61,7 | — |
| QwenSWEBench | 79,0 | — |
| Terminal-Bench 2.1 | **73,0** | 63,4 |
| DeepSWE 1.1 | 42,2 | 13,3 |
| OSWorld-Verified | 84,3 | 63,9 |
| SWE-MM | 38,6 | 25,7 |
| GPQA Diamond | 89,2 | — |

Die Sprünge gegenüber 3.6 sind groß, besonders bei den *agentischen* Maßen (Terminal-Bench,
DeepSWE, OSWorld) — also genau dort, wo es für uns zählt: mehrschrittig arbeiten, Werkzeuge
bedienen, Fehler lesen und nachbessern. Berichtet wird, das Modell schlage Claude Opus 4.6 auf
15 von 19 überlappenden Tests. **Das sind Marketing-Zahlen; verlassen sollte man sich auf keine
davon, bevor man den eigenen Fall gemessen hat.** Für unseren Zweck ist ohnehin ein anderer Test
maßgeblich: *„schreibt es einen Decide-Rumpf, der kompiliert und die Beispiele erfüllt?"* — und
den kann der Editor selbst fahren (siehe C.4).

### B.2 Passt es auf eine RTX 4080 Super? — Nein, nicht in brauchbarer Form

Die 4080 Super hat **16 GB**. Für ein dense-27B gilt:

| Quantisierung | ~VRAM nur Gewichte | Auf 16 GB? |
|---|---:|---|
| Q8_0 | ~30 GB | nein |
| Q6_K | ~23 GB | nein |
| Q4_K_M | **~17–19 GB** | **nein** — auch ohne Kontext schon drüber |
| Q3_K_M / IQ3 | ~13 GB | rechnerisch ja, praktisch grenzwertig |

Dazu kommt, was die Tabelle gern verschweigt: CUDA-Runtime, Vision-Turm-Puffer und vor allem der
**KV-Cache**. Für ein 27B kostet 64k Kontext rund **4 GB** obendrauf. Bei Q3_K_M (13 GB) bleiben
also ~3 GB für alles andere — das reicht für vielleicht 8–16k Kontext, und man sitzt permanent am
OOM-Rand. Die verbreitete Empfehlung lautet folgerichtig: **24 GB ist das Minimum** für dieses
Modell.

Der Ausweg „CPU-Offload" existiert, kostet aber **3–10× Geschwindigkeit**. Bei einem *dense*
Modell trifft das voll durch, weil jeder Token durch alle Parameter muss.

Und der Haken, der genau unseren Anwendungsfall trifft: **bei Q3 leidet zuerst die Formtreue** —
Tool-Call-Formate und strukturierte Ausgaben degradieren spürbar, während der Fließtext noch
passabel klingt. Wer Code und JSON will, sollte Q4 nicht unterschreiten. Auf 16 GB heißt das für
ein dense-27B: geht nicht.

### B.3 Was auf 16 GB wirklich läuft

| Modell | Quant | VRAM | Tempo (4080-Klasse) | Für uns |
|---|---|---:|---|---|
| **gpt-oss-20b** (MoE, ~3,6B aktiv) | Q4_K_M | ~12–13 GB | sehr schnell (30–140 tok/s je nach Messung) | **beste Wahl für den agentischen Kreis** — per RL auf Function-Calling trainiert, saubere Tool-Calls |
| **Qwen3-Coder-30B-A3B** (MoE, 3B aktiv) | Q4_K_M | ~17–18 GB | mit Teil-Offload noch gut, weil nur 3B aktiv | **stärkster Code-Kandidat**, MoE verzeiht Offload weit besser als dense |
| Gemma 4 12B | Q4/Q5 | ~8–10 GB | schnell | solide, natives Tool-Calling, für Struktur-Aufgaben |
| Qwen3.8-27B | Q3_K_M | ~13 GB + KV | 5–15 tok/s, OOM-nah | nur als *langsamer Hintergrund-Arbeiter* |

Die entscheidende Einsicht: **MoE schlägt dense auf knappem VRAM.** Ein 30B-MoE mit 3B aktiven
Parametern verliert durch Offload wenig, ein 27B-dense verliert alles. Wer eine 4080 Super hat und
„die stärkste lokale Code-KI" will, nimmt **Qwen3-Coder-30B-A3B** — nicht Qwen3.8-27B.

### B.4 Warum Langsamkeit hier trotzdem verkraftbar ist

Das ist der Punkt, der das Konzept rettet: **wir bauen keinen Chat.** Ein Decide-Rumpf sind
150–400 Ausgabe-Token. Selbst bei 8 tok/s sind das 20–50 s, mit drei Selbstreparatur-Runden also
höchstens ~2 Minuten für einen Knoten, der danach kompiliert *und* seine Beispiele erfüllt. Ein
Knoten, den man anstößt und dessen Ampel man später anschaut, darf 2 Minuten brauchen.

Das ist keine Ausrede, sondern eine **Entwurfsvorgabe**: der Kreis muss asynchron, streamend und
mehrknotenfähig sein, damit Latenz nicht wehtut. Danach ist er auch mit dem 27B auf Q3 benutzbar —
nur eben spürbar zäher als mit einem MoE.

### B.5 Empfehlung

Der `agent`-Knoten trägt das Modell als **Feld**, nicht als Annahme. Konkret empfohlen: **zwei
Agent-Knoten mit unterschiedlichen Modellen**, weil die Aufgaben unterschiedlich sind.

- **Struktur-Agent** (Gemma 4 12B oder gpt-oss-20b): hohe Stückzahl, wenig Fantasie nötig —
  Apply-Rümpfe, State-Felder, Testfall-Gerüste, Store-Methoden nach Muster. Passt vollständig in
  VRAM, läuft schnell, hält Grammatiken gut ein.
- **Rumpf-Agent** (Qwen3-Coder-30B-A3B, ersatzweise Qwen3.8-27B Q3 mit kurzem Kontext):
  Decide-Rümpfe, wo die Fachlogik und die Fallunterscheidung sitzt. Wenige Aufrufe, darf dauern.

Der Vision-Turm von Qwen3.8 bringt uns nichts (siehe C.7) und kostet auf 16 GB nur Platz — falls
es eine text-only-Variante oder einen Quant ohne Vision-Gewichte gibt, die nehmen.

---

## C — Das Konzept

### C.1 Der Leitsatz

> **Invariante 7 — Der Agent schlägt vor, der Compiler entscheidet.**
>
> Kein von einem Modell erzeugter Text wird Teil des `EditorModell`, bevor er
> (a) die Form-Grammatik erfüllt, (b) den Reinheits-Prüfer besteht, (c) mit den echten
> Generatoren kompiliert und (d) seine Beispiel-Knoten grün fährt. Die Freigabe ist ein
> Mensch-Klick, kein Automatismus.

Das ist keine Zusatzregel, sondern die Übertragung der sechs bestehenden Invarianten auf eine
nicht-deterministische Quelle. Das Modell darf unzuverlässig sein — es sitzt an der einzigen
Stelle, an der Unzuverlässigkeit folgenlos bleibt, weil ein deterministisches Tor dahinter steht.

### C.2 Die neuen Knoten

#### `agent` — die Modell-Bindung

Wiederverwendbar: mehrere Prompt-Knoten stecken an einem Agenten.

```jsonc
{ "_id": "ag1", "name": "Rumpf-Agent",
  "endpunkt": "http://localhost:8080/v1",     // OpenAI-kompatibel: llama.cpp-server, Ollama, vLLM, LM Studio
  "modell": "qwen3-coder-30b-a3b-q4_k_m",
  "temperatur": 0.2, "seed": 7, "maxTokens": 800, "kontextFenster": 16384,
  "runden": 3 }                                // Budget für Selbstreparatur
```

Slots: `Agent ▶` (out, Typ `agent`). Kopf zeigt eine Ampel: erreichbar / Modell geladen / tok/s
der letzten Antwort. Kein Schlüssel, keine Cloud — der Endpunkt ist `localhost`.

#### `prompt` — der Klartext + der **typisierte** Kontext

```jsonc
{ "_id": "p1", "auftrag": "decide",
  "text": "Reserviere nur, wenn Verfuegbar ≥ Betrag. Sonst DeckungReichtNicht mit dem verfügbaren Rest.",
  "agent": "ag1",
  "kontext": ["rec:ReserviereBetrag", "agg:Konto", "rec:BetragReserviert", "rec:DeckungReichtNicht"] }
```

Slots: `◀ Agent`, ein **offener Multi-Slot `◀ Kontext`** (nimmt Command-, Event-, State-,
Aggregat-Knoten an), `Auftrag ▶`.

**Das ist der wichtigste Entwurfspunkt des ganzen Konzepts:** `kontext` enthält
*Knoten-Referenzen*, keinen kopierten Text. Der Server rendert daraus beim Aufruf die kanonische
C#-Deklaration — dieselbe, die der `Scaffolder` ohnehin erzeugt. Folgen:

- Der Agent sieht immer den **aktuellen** Stand; kein Nachziehen von Prompt-Kopien.
- Benennt man einen Record um, zieht `renameRefs` die Prompt-Kanten mit — wie jede andere Kante.
- Der Kontext bleibt **klein**. Auf 16 GB ist das keine Eleganz, sondern Bedingung (B.2).
- Es ist Invariante 3 auf den Prompt angewandt: über Typen verdrahtet, nicht über handgebaute
  Strings.

Der Klartext bleibt reiner Klartext — die Reinheits- und Formregeln stehen in der
**System-Vorlage** je `auftrag`, nicht im Feld des Nutzers. Der Nutzer beschreibt Fachlichkeit,
sonst nichts.

#### `code` — der erzeugte, editierbare Rumpf

```jsonc
{ "_id": "c1", "auftrag": "p1",
  "rumpf": "if (State.Verfuegbar < cmd.Betrag)\n{\n    yield return new DeckungReichtNicht(State.Verfuegbar, cmd.Betrag);\n    yield break;\n}\nyield return new BetragReserviert(cmd.Betrag);",
  "verdikt": { "form": "ok", "kompilat": "ok", "beispiele": "3/3", "runden": 1 },
  "herkunft": { "agent": "ag1", "modell": "…", "promptHash": "…", "seed": 7, "zeit": "2026-08-16T21:03Z" },
  "freigegeben": true }
```

Slots: `◀ Auftrag`, `Rumpf ▶` (Typ `rumpf`). Körper: das vorhandene `codearea` (voll editierbar,
Handkorrektur jederzeit) + eine **Verdikt-Zeile** mit vier Ampeln (Form · Kompilat · Beispiele ·
Runden) + Knöpfe *Erzeugen* / *Nachbessern* / *Übernehmen*.

`herkunft` ist Pflicht: im Modell steht damit dauerhaft, **was ein Mensch und was eine Maschine
geschrieben hat**, mit welchem Modell und aus welchem Prompt. Nachvollziehbar, reproduzierbar
(Seed + Prompt-Hash), und beim Review sofort sichtbar. Handkorrektur setzt `herkunft.handEditiert`.

#### Am Decider/Applier: ein neuer Eingang `◀ Rumpf`

Steckt eine Rumpf-Kante, wird das `codearea` am Decider zur Anzeige (der Code-Knoten ist die
Quelle). *Lösen* trennt die Kante und macht den Rumpf wieder zur Handarbeit. **Entweder Kante
oder Hand — nie beides**, damit es genau eine Wahrheit gibt.

#### `beispiel` — die Abnahme (der Knoten, der alles trägt)

```jsonc
{ "_id": "b1", "ziel": "d1",
  "gegeben": ["KontoEroeffnet(100, Gesperrt: false)"],
  "wenn": "ReserviereBetrag(id, 200)",
  "dann": { "art": "abgelehnt", "typ": "DeckungReichtNicht" },
  "undZustand": "k.Saldo == 100" }
```

Das ist 1:1 die bestehende `Szenario`-DSL. Der Beispiel-Knoten ist dreifach nützlich:

1. **Abnahmekriterium** — der Agent ist fertig, wenn alle angesteckten Beispiele grün sind.
2. **Prompt-Kontext** — „das muss gelten" ist die mit Abstand wirksamste Anweisung an ein Modell
   dieser Größenklasse.
3. **Echter Test** — `Cqrs.Testing/DslSchreiber.cs` existiert bereits; ein Klick exportiert die
   Beispiele als Prüfstand-Test in `Infrastructure.Pruefstand.Tests`. Was die KI baut, hinterlässt
   damit *dauerhafte* Regressionstests, nicht nur ein grünes Häkchen von gestern.

**Ohne diesen Knoten funktioniert das Ganze nicht.** Ein 20–30B-Modell rät nicht zuverlässig die
richtige Fachlogik. Es trifft aber sehr zuverlässig ein *überprüfbares* Ziel, wenn es die
Fehlermeldung zurückbekommt. Der Unterschied zwischen „KI schreibt Code" und „KI schreibt Code,
der nachweislich tut, was du gesagt hast", ist genau dieser Knoten.

### C.3 Die Rumpf-Kontrakte

Ein Rumpf ist **kein freies C#**. Er ist ein Anweisungsblock in einem bekannten Rahmen mit
bekannten Bindungen im Sichtbereich:

**Decide** — `IEnumerable<OneOf<E1,…,E5>> Decide(TCmd cmd)`, sichtbar: `cmd`, `this.State`
- erlaubt: `yield return new X(...)` mit X ∈ `Ergibt` · `yield break` · `if`/`else` ·
  `switch` · lokale `var` · reine Ausdrücke über `cmd`/`State`
- **verboten**: `await`, jede IO (`File`, `HttpClient`, `Console`), `DateTime.Now/UtcNow`
  (Zeit kommt aus `IDbClock` — Zeit ist keine Umgebungsvariable), `Random`, `static`-Zustand,
  Zugriff auf ein anderes Aggregat, **Schreiben auf `State`** (ein Decider entscheidet, er wirkt
  nicht)

**Apply** — `void Apply(TEvt evt)`, sichtbar: `evt`, `this.State`
- erlaubt: Zuweisungen an `State.…` · reine Ausdrücke über `evt`/`State` · Collection-Mutation
- **verboten**: `yield` · jede IO · Zeitquellen · Verzweigung über etwas anderes als `evt`/`State`
  (ein Applier muss beim Replay bitgleich dasselbe tun — das *ist* Invariante 1)

Diese Regeln sind heute Konvention im Kopf des Projekts. Der Konzeptvorschlag macht sie zum ersten
Mal **maschinell prüfbar** — und zwar für Menschen genauso wie für Modelle. Der Nebeneffekt ist
größer als der KI-Nutzen: die Reinheit des Fachcodes (Invariante 5) bekommt endlich einen Wächter.

### C.4 Der Regelkreis

Serverseitig in `SimHost`, gestreamt (SSE) an den Code-Knoten:

```
POST /api/agent/rumpf   { promptKnoten, modell (EditorModell), zielRegel }
```

| # | Schritt | Wer | Bei Fehler |
|---|---|---|---|
| 1 | **Kontext bauen** — aus den `kontext`-Referenzen die kanonischen Deklarationen + OneOf-Signatur + Beispiele | deterministisch (`Scaffolder`) | — |
| 2 | **Prompt rendern** — System-Vorlage je `auftrag` + Klartext + Kontext | deterministisch | — |
| 3 | **Erzeugen** unter **GBNF-Grammatik** | LLM | — |
| 4 | **Form prüfen** (`RumpfPruefer`, neu) — Roslyn-Syntax-Walk gegen C.3 | deterministisch | → Runde n+1 mit Befund |
| 5 | **Kompilieren** — `ModellRuntime.Kompiliere` mit eingesetztem Kandidaten | Roslyn + echte Generatoren | → Runde n+1 mit Diagnosen |
| 6 | **Beispiele fahren** — `Szenario`/`SagaSzenario` über `SagaLaufwerk` | store-frei | → Runde n+1 mit Entscheidungs-Bericht |
| 7 | **Grün** → `verdikt` setzen, `freigegeben=false` | — | nach `runden` Versuchen: rot stehen lassen |

Zu **Schritt 3**: Die Grammatik erzwingt die *Form*, nicht die Fachlichkeit. Für Decide etwa:

```gbnf
root      ::= anweisung+
anweisung ::= guard | ausgabe | "yield break;\n"
guard     ::= "if (" bedingung ") {\n" anweisung+ "}\n"
ausgabe   ::= "yield return new " event "(" args? ");\n"
event     ::= "BetragReserviert" | "DeckungReichtNicht"      # ← aus DecideRegel.Ergibt generiert!
```

Die Alternative `event` wird **aus dem Modell erzeugt**. Damit kann das Modell buchstäblich keinen
Event-Namen erfinden, der nicht am OneOf-Ausgang hängt — die häufigste und ärgerlichste Fehlerart
ist auf Dekodier-Ebene ausgeschlossen, nicht erst im Compiler. Ebenso fallen Prosa-Vorspann,
Markdown-Fences und `return` statt `yield return` weg. Kosten: 5–15 % Durchsatz. Das ist der mit
Abstand billigste Zuverlässigkeitsgewinn im ganzen Entwurf, und er wirkt am stärksten genau dort,
wo kleine/stark quantisierte Modelle schwach sind.

Zu **Schritt 6**: Der Fehlschlag-Bericht der DSL zeigt *Zustand vorher → gewählter Zweig →
nachher*. Das ist als Reparatur-Eingabe deutlich besser als eine Compiler-Meldung, weil es dem
Modell sagt, **welche Verzweigung** es falsch gelegt hat.

**Modell-Sicht auf den Kreis:** Der Agent bekommt keine Werkzeuge im Sinne von Shell/Dateisystem.
Er hat genau eine Aufgabe — Text in einem Rahmen — und der Rahmen wird ihm vom Server geprüft.
Das ist absichtlich viel weniger als ein „Coding-Agent" und deswegen auf lokaler Hardware
überhaupt erreichbar.

### C.5 Wie das die sechs Invarianten wahrt

| # | Invariante | Wie gewahrt |
|---|---|---|
| 1 | Log ist die Wahrheit | Der Agent fasst keinen Store an. Der Reinheits-Prüfer verbietet Zeit-/IO-Quellen im Applier — Replay bleibt bitgleich. |
| 2 | Signal ist nur Weckruf | unberührt |
| 3 | Routing über Typen | Prompt-Kontext = Knoten-Referenzen; die Grammatik lässt nur Event-Namen aus `Ergibt` zu |
| 4 | Keine Runtime-Reflection | Erzeugung passiert zur **Entwurfszeit** im Editor. Was in die Domäne geht, ist gewöhnlicher C#, den der bestehende Generator übersetzt. **Ein LLM läuft nie im Cluster.** |
| 5 | Fachcode bleibt rein | Erstmals maschinell erzwungen (C.3), statt nur vereinbart |
| 6 | Persistent nur bei durablem Konsumenten | Der Editor-Kreis ist vollständig flüchtig |

### C.6 Ausbaustufe 2 — Repositories und Projektionen

Hier ist der Hebel am größten, weil dieser Code heute **komplett von Hand** entsteht und im Editor
gar nicht vorkommt. Ein Blick in `Domain.Projections` zeigt das Muster: pro Projektion ein
ReadModel-Record, ein Write-Store-Interface, ein Read-Store-Interface, die Projektionsklasse mit
`Handle`-Methoden pro Event, Query-Records, ein Reader — und dazu in `Infrastructure` die
Marten-Implementierung des Stores. Viel Umfang, wenig Erfindung, starkes Muster. **Das ist genau
das Profil, bei dem ein 20–30B-Modell zuverlässig ist**, wenn es die vorhandenen Stores als
Muster im Kontext hat.

Neue Knoten: `readmodel` · `projektion` · `store` · `query` · `reader`.

Arbeitsteilung:

- **Deterministisch (`Scaffolder`)**: der ReadModel-Record; das Store-Interface — *ableitbar aus
  den Methoden, die die Projektion aufruft*; das Projektions-Gerüst mit korrekten
  `Handle(TEvt evt, IAggregateEnvelope envelope, ProjectionWriter writer)`-Signaturen, `SubscriberId`,
  und den richtigen Markern.
- **Agent**: die `Handle`-Rümpfe und die **Marten-Store-Implementierung**.

Der Validator muss hier die **Achse-B-Regel vorziehen**, damit der Agent nicht in die Falle
generiert: append-artige Projektionen (`IAppendProjektion`) brauchen den Co-Commit-Store
(`IProjectionTracker`), emittierende Konsumenten den `IEmittentenCursor` — beides gesetzt wirft
im Ctor. Das gehört als Form-Befund in den Editor, bevor irgendein Modell schreibt.

**Ehrliche Grenze, die man nicht wegreden darf:** Store-Code trifft echtes Marten. Der store-freie
Prüfstand kann ihn **nicht** abnehmen — `CLAUDE.md` sagt zu Recht „nie faken, was man nicht
besitzt", und ein `InMemoryEventStore` ist ausgeschlossen. Für Stufe 2 endet der Regelkreis
deshalb bei **Schritt 5 (Kompilat)**; die echte Abnahme ist der Integrationstest gegen Postgres,
und der läuft nicht im Editor. Der Code-Knoten muss das anzeigen — Verdikt `Beispiele: n/a
(Integration)` statt eines erfundenen grünen Hakens.

### C.7 Was ausdrücklich **nicht** gebaut werden sollte

- **Kein Vision-Einsatz auf dem Board.** Qwen3.8-27B kann Bilder lesen, und die Versuchung ist
  groß, den Wissensgraphen *anschauen* zu lassen. Der Graph liegt als JSON vor. Ein Modell, das
  das Bild statt der Daten liest, verletzt den Geist von Invariante 3 und tauscht eine exakte
  Quelle gegen eine unscharfe. Der Vision-Turm kostet auf 16 GB nur VRAM.
- **Kein Agent im Cluster.** Keine Reaktion, kein Prozess, kein Decider ruft je ein Modell. Die
  Laufzeit bleibt deterministisch und reflexionsfrei. Der Agent ist ein *Werkzeug am Schreibtisch*.
- **Kein freier Dateisystem-/Shell-Zugriff.** Der Agent schreibt Rümpfe in einen Rahmen, sonst
  nichts. Damit ist der Fehlerraum klein genug, dass lokale Modelle ihn beherrschen — und die
  Sicherheitsfrage stellt sich gar nicht erst.
- **Kein automatisches Übernehmen.** `freigegeben` ist ein Klick. Grün heißt „erfüllt, was du
  aufgeschrieben hast" — nicht „ist fachlich richtig".

### C.8 Was am Bestand angefasst werden muss

Vier konkrete Befunde aus dem Code, die vor Stufe 1 fällig sind:

1. **Stabile IDs müssen ins Modell.** Heute sind `_id` bei Decider/Applier/State/Transition
   **client-seitig** vergeben (`normalize()` in `HtmlPresenter.cs`) und fehlen in
   `DomainEditor/EditorModell.cs`. System.Text.Json verwirft sie beim Round-trip, und der
   `ModellMapper` (C# → Modell) kennt sie ohnehin nicht. Agent-/Prompt-/Code-/Beispiel-Knoten
   verdrahten sich aber **über** diese IDs. Also: `Id` als echte Eigenschaft in die
   Modell-Records, stabil über Round-trip und Neuladen.

2. **`ModellRuntime` leakt Assemblies.** `Assembly.Load(ms.ToArray())` ohne Unload, gecacht nach
   Modell-Hash — der Kommentar nennt das „für ein Editor-Werkzeug vertretbar". Bei einer
   Agenten-Schleife mit drei Reparatur-Runden pro Knoten und vielen Knoten ist es das **nicht
   mehr**. Nötig: `AssemblyLoadContext(isCollectible: true)` plus Cache-Deckel.

3. **Der Kompilierpfad braucht einen Kandidaten-Modus.** Heute wird immer das ganze Modell
   übersetzt. Für den Kreis will man „Modell + ein ersetzter Rumpf" prüfen, ohne das
   gespeicherte Modell anzufassen — sonst zerschießt ein Fehlversuch den Arbeitsstand.

4. **`Validator` und `RumpfPruefer` sollten dieselbe `Befund`-Form sprechen.** Der Code-Knoten
   zeigt dann Form-, Struktur- und Compiler-Befunde in einer Liste. Neue Codes: `EDIT-KI-01`
   (verbotenes Konstrukt), `-02` (unbekannter Event am Ausgang), `-03` (Decider schreibt State),
   `-04` (Zeitquelle im Rumpf), `-05` (Rundenbudget erschöpft).

### C.9 Phasen

| Phase | Inhalt | Abnahme |
|---|---|---|
| **P1** | `ILlmLaufwerk` (OpenAI-kompatibel) + `agent`/`prompt`/`code`-Knoten + Decide-Rümpfe + GBNF + `RumpfPruefer` + Kompilat-Schleife. Ein Modell. | Ein Decide-Rumpf entsteht per Prompt, kompiliert, hängt an der Kante |
| **P2** | `beispiel`-Knoten + Abnahme über `Szenario` + `DslSchreiber`-Export | Der Agent repariert sich an einem roten Beispiel selbst grün |
| **P3** | Apply-Rümpfe, State-Felder, Saga-Argumente (dieselbe Mechanik, andere Vorlage + Grammatik) | Ein Aggregat vollständig per Prompt gebaut |
| **P4** | Ausbaustufe 2: `readmodel`/`projektion`/`store`/`query`/`reader` + Marten-Implementierung | Eine Projektion samt Store kompiliert; Integrationstest von Hand |
| **P5** | Zwei-Modell-Split (Struktur-/Rumpf-Agent), Stapellauf „alle roten Rümpfe füllen", Kosten-/Tempo-Anzeige | Ein leeres Aggregat wird in einem Lauf gefüllt |

### C.10 Risiken

- **Quant-Degradation trifft die Formtreue zuerst.** Auf Q3 leidet strukturierte Ausgabe stärker
  als Prosa. Die Grammatik federt das ab, aber die *Fachlogik* wird trotzdem schlechter. Deshalb:
  Beispiel-Knoten sind Pflicht, nicht Kür — und man muss messen statt hoffen.
- **Kontext-Budget.** 262k nominal sind auf 16 GB irrelevant; realistisch sind 8–16k. Der
  kuratierte Kontext (C.2) ist daher tragende Konstruktion, kein Feinschliff.
- **Beispiele als falsche Sicherheit.** Grün heißt „erfüllt die aufgeschriebenen Fälle". Ein
  Modell, das nur die Beispiele erfüllt, kann daneben Unsinn tun. Gegenmittel: die Beispiele
  schreibt der Mensch **vor** dem Prompt, und der Reinheits-Prüfer deckt die Klasse von Fehlern
  ab, die Beispiele nicht sehen.
- **Der Editor wird zum zweiten Backend.** Er ist es teilweise schon (`ModellRuntime` +
  `SagaLaufwerk`). Mit Stufe 2 wächst das. Gegenmittel: der Editor darf **nie** eine eigene
  Semantik bekommen — er ruft ausschließlich `Scaffolder`, die echten Generatoren und
  `Cqrs.Testing` auf. Jede Sonderlogik im Editor wäre eine zweite Wahrheit.

---

## Quellen (Recherche B, Stand 2026-08-16)

- [Qwen/Qwen3.8-27B · Hugging Face](https://huggingface.co/Qwen/Qwen3.8-27B) — offizielles Repo, Apache 2.0, 14.08.2026
- [Qwen3.8-27B: Specs, Benchmarks & Verdict — kingy.ai](https://kingy.ai/blog/qwen3-8-27b-specs-benchmarks-local-hardware/)
- [Qwen3.8-27B: A Comprehensive Technical Analysis — Local AI Zone](https://local-ai-zone.github.io/blog/qwen3-8-27b-comprehensive-analysis.html)
- [Qwen 3.8 Benchmarks: What's Actually Verified So Far — Yotta Labs](https://www.yottalabs.ai/post/qwen-3-8-benchmarks-what-is-verified-2026)
- [Qwen3.8-27B VRAM Requirements: 13GB to 54GB — OrcaRouter](https://www.orcarouter.ai/blog/qwen-3-8-27b-vram-requirements)
- [Qwen 3.8 27B VRAM Requirements: Runs on 24 / 32 / 48 GB GPUs — CanItRun](https://canitrun.dev/models/qwen3.8-27b/)
- [Best Local LLM for RTX 4080 & 4080 Super (2026) — openclawdc.com](https://openclawdc.com/blog/best-local-llm-rtx-4080/)
- [RTX 4080 SUPER 16GB Local LLM (2026) — modelfit.io](https://modelfit.io/gpu/rtx-4080-super/)
- [GPU VRAM, CPU Offload, and llama.cpp: The Real Performance Cliff](https://sergiiob.dev/posts/gpu-vram-cpu-offload-llama-cpp-deep-dive/)
- [16 GB VRAM LLM benchmarks with llama.cpp — glukhov.org](https://www.glukhov.org/llm-performance/benchmarks/best-llm-on-16gb-vram-gpu/)
- [Grammar and Structured Output — llama.cpp/DeepWiki](https://deepwiki.com/ggml-org/llama.cpp/8.1-grammar-and-structured-output)
- [llama.cpp grammars/README.md](https://github.com/ggml-org/llama.cpp/blob/master/grammars/README.md)
- [We ran Qwen3.6-27B on $800 of consumer GPUs — LLMKube](https://llmkube.com/blog/qwen3-6-27b-bakeoff)
