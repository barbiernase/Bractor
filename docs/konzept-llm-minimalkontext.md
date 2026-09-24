# Konzept — Minimalkontext für ein (lokales) LLM: die Arbeitskarte

> **Stand:** 2026-09-24 · **Status:** K1 GEBAUT — Arbeitskarte als reine Graph-Projektion für Decide/Apply (§2–§4, §12); K2–K6 Konzept.
> **Frage:** Wie bauen wir ein System, das einem LLM — Zielgröße: ein **lokales ~27B-Modell** — nur
> den minimalen Kontext gibt, damit es *wirklich nur die Funktion* schreibt? Und: können wir präzise
> benennen und im Editor zeigen, **was** das Modell dafür wissen muss?
> **Kurzantwort:** Ja. Die Maschine dafür ist zu ~70 % schon da (Extractor = Code-Fakten, D/S/H,
> `// 🤖 Prompt:`-Naht, In-Memory-Compile, store-freie Szenarien). Es fehlt **ein** neues Artefakt —
> die **Arbeitskarte** je H-Slot — und **eine** neue Prüfung — die **Kartenwand** (Vokabular-Allowlist).
>
> Verwandt: [konzept-domaenen-editor.md](konzept-domaenen-editor.md) §2(b) D/S/H, §10.5 Code-Fakten ·
> [analyse-editor-backend-abdeckung-2026-09-24.md](analyse-editor-backend-abdeckung-2026-09-24.md) §5 LLM-Füllung ·
> [entscheidungs-debugger-und-test-dsl.md](entscheidungs-debugger-und-test-dsl.md) (Szenario-DSL).

---

## 1 · Befund: warum das hier ungewöhnlich gut geht

Die Architektur hat die LLM-Frage schon zu großen Teilen *strukturell* beantwortet, ohne dass sie so
formuliert war:

| Eigenschaft des Systems | Was sie für das LLM bedeutet |
|---|---|
| **Invariante 5** — Fachcode bleibt rein (kein Cursor, Signal, Sharding, Exactly-once im Entwickler-Code) | Das LLM muss **nichts** über Proto.Actor, Marten, Redis, Wire, Proto, DI wissen. Null Framework-Kontext. |
| **Invariante 4** + Generatoren | Alles Dispatchende ist generiert → das LLM schreibt nie Verdrahtung, nur Rümpfe. |
| **D/S/H** (Editor §2b) | Der Editor weiß bereits, *welche* Stellen H sind. Nur dort taucht das LLM auf. |
| **Extractor liest nur Code-Fakten** (§10.5) | Der Kontext kann **deterministisch aus Symbolen** gebaut werden, nicht aus Dateien/Heuristik. |
| **Signatur ist Vertrag** (`IEnumerable<OneOf<E1,E2,R>> Decide(Cmd)`) | Die erlaubten Ausgaben sind **typisch abgeschlossen** — ein ideales Guardrail. |
| `// 🤖 Prompt:` im Rumpf + Hash-Sperre (`SimHost/CodeSync.cs`) | Absicht ist versioniert, Datei-Konflikte werden erkannt. |
| `/api/editor/compile` (echte Generatoren, in-memory) + `Cqrs.Testing.Szenario` (store-frei) | Die Verifikationsschleife existiert; es fehlt nur der Aufrufer. |

**Messung (Bestand):** die ganze Domäne (`Domain*`) ≈ 200 KB ≈ **55 k Token**; allein der Ordner
`Domain/Datensatz` ≈ 25 KB ≈ **7 k Token**. Die Arbeitskarte für einen konkreten Rumpf
(Beispiel §3) ≈ **0,6–0,9 k Token**. Das ist der Hebel: **Faktor ~10 gegenüber „gib ihm den Ordner",
Faktor ~60 gegenüber „gib ihm die Domäne"** — und genau die Größenordnung, in der ein lokales
~27B-Modell zuverlässig arbeitet (lange Kontexte kann es zwar *annehmen*, die Treffsicherheit sinkt
aber spürbar mit der Menge irrelevanten Materials).

---

## 2 · Das Prinzip: die Karte ist eine Projektion des Graphen

> **Die Karte wird vollständig aus dem Code erzeugt — ohne LLM, ohne Heuristik, ohne erfundene Prosa.**
> Quelle ist ausschließlich der Wissensgraph des Extractors (Routing, OneOf-Ausgänge, Guards, Saga-/Pipeline-/
> Projektions-Kanten) und Roslyn (Symbole, Signaturen, Datenfluss, Tests). Jede Zeile der Karte gehört zu genau
> einer der drei Klassen:
>
> | Klasse | Was | Beispiel |
> |---|---|---|
> | **Fakt** | direkt ein Symbol/eine Kante | Signatur, Ausgänge, Felder, State-Member, „kommt von Client" |
> | **Regel** (B1–B5) | feste, nummerierte Ableitung über Fakten — Typgleichheit, Kanten, Datenfluss; **nie** Namensähnlichkeit | „`DatensatzBereitsEingefroren` wird in 6 anderen Decide unter `State.IstEingefroren` erzeugt" |
> | **Kommentar** | verbatim aus dem Code (`///`, `//`, `// 🤖 Prompt:`), mit Herkunft | „/// Gültig, wenn die Anteile nicht-negativ sind …" |
>
> **Beschreibungen gibt es nur, wo der Code einen Kommentar trägt.** Ein Feld ohne Kommentar erscheint als
> `Typ Name { get; set; }` — sonst nichts. Was nicht ableitbar ist, steht als Lücke auf der Karte („keine").

Drei Sätze tragen das:

1. **Vom Graphen her denken.** Die Frage ist nicht „was steht in der Datei?", sondern „welche Knoten und Kanten
   berührt dieser Slot?" — Eingang (Command-/Event-Knoten), Ausgänge (`produces`-Kanten), Zustand (State-Knoten),
   Herkunft (`sends`/`pipelineEmits`-Kanten auf den Command), Abnehmer (`consumedBy`/`triggers`/`advances`,
   Apply-Slots) und die Nachbar-Slots desselben Aggregats (Guards, Datenfluss).
2. **Das LLM schreibt nur den Rumpf zwischen `{` und `}`.** Signatur, `using`s, Klasse, Datei bestimmt die Maschine.
3. **Was die Karte deklariert, ist die Allowlist der Prüfung** (Gegenprobe heute auf Symbol-Identität, Kartenwand K2).

**Was NICHT ableitbar ist — und deshalb vom Menschen als Code kommen muss:**
- die **Absicht** eines neuen Rumpfs → als Kommentar (`// 🤖 Prompt:` im Rumpf, `///` am Command),
- die **Bedingungen neuer Logik** → nur indirekt über B1 (wie Nachbarn denselben Ausgang schützen),
- die **Spezifikation** → als Szenario-Test (`Szenario.Für…Gegeben…Wenn…Dann`). Die Simulation („📋 Als Test")
  zeichnet nur das *Ist*-Verhalten auf und kann für einen fehlenden Rumpf nichts liefern.
- **Verhaltensregeln** wie „Decide ist rein" → stehen nur dann auf der Karte, wenn Compiler oder Framework sie
  erzwingen (Abschnitt VERTRAG); sonst wären sie Prosa. Reinheit bräuchte dafür erst einen Analyzer (K2).

---

## 3 · Die Arbeitskarte — echte Ausgabe

`dotnet run --project GraphExtractor -- --karte SetzeSplit` (gekürzt: Typen-Doku und B4-Zeilen):

```text
## AUFGABE
Rumpf von: IEnumerable<OneOf<SplitGesetzt, DatensatzBereitsEingefroren, SplitUngueltig>> Decide(SetzeSplit cmd)
Erreichbar: cmd (SetzeSplit), this.State (Datensatz)

## VERTRAG
V1 Ausgaben ⊆ {SplitGesetzt, DatensatzBereitsEingefroren, SplitUngueltig}   — erzwungen: Compiler (OneOf-Signatur)
V2 Ablehnung (DatensatzBereitsEingefroren, SplitUngueltig) nur als einzige Ausgabe   — erzwungen: Laufzeit (Aggregat-Actor wirft bei gemischtem Ergebnis)

## KOMMENTARE (verbatim aus dem Code)
[// vor Decide(SetzeSplit)] SPLIT — optionaler Override
[// vor SetzeSplit] SPLIT — optionaler Override (Default 70/15/15)

## TYPEN (abschließend; /// = Doku-Kommentar aus dem Code)
Eingang   SetzeSplit(Guid AggregateId, int TrainProzent, int ValProzent, int TestProzent, int Seed)   [Command]
Ausgang   SplitGesetzt(int TrainProzent, int ValProzent, int TestProzent, int Seed)   [Event]
          DatensatzBereitsEingefroren(Guid DatensatzId)   [Ablehnung]
          SplitUngueltig(int TrainProzent, int ValProzent, int TestProzent)   [Ablehnung]
Zustand   Datensatz   /// …
            string? Name { get; set; }
            DatensatzStatus Status { get; set; }
            SplitKonfig Split { get; set; }   /// Split-Konfiguration (Default 70/15/15, optional überschrieben).
            bool IstEingefroren => Status == DatensatzStatus.Eingefroren
            …
Typen     SplitKonfig(int TrainProzent, int ValProzent, int TestProzent, int Seed)   [Typ]   /// …
            bool IstGueltig => TrainProzent >= 0 && … == 100   /// Gültig, wenn die Anteile nicht-negativ sind und sich zu 100 summieren.
          enum DatensatzStatus { Entwurf, Eingefroren }   [Enum]
          …

## GRAPH-UMFELD
SetzeSplit kommt von: Client
SplitGesetzt geht an: Apply(SplitGesetzt) [geschrieben] · Projektion DatensatzProjektion
DatensatzBereitsEingefroren geht an: Aufrufer (Ablehnung, nicht im Log)

## BEZÜGE (regelbasiert abgeleitet)
B1 DatensatzBereitsEingefroren — in 6 anderen Decide: `State.IstEingefroren` in 6 (FuegeRangeHinzu, NimmRangeAuf, …)
B2 int: SplitGesetzt.TrainProzent, …, SplitUngueltig.TestProzent ← cmd.TrainProzent, cmd.ValProzent, cmd.TestProzent, cmd.Seed, State.EingefroreneVersion, …
B2 Guid: DatensatzBereitsEingefroren.DatensatzId ← cmd.AggregateId, State.Id
B4 State.IstEingefroren: gelesen von Decide(EntfernePaar), Decide(FriereEin), …
…
## BEISPIEL (B5)          // Decide(EntfernePaar) — echter Nachbar-Rumpf
## SPEZIFIKATION          keine — für diesen Slot liegt keine Spezifikation als Code vor
```

(Die frühere Fassung dieses Abschnitts zeigte eine **erfundene** Karte mit ausgedachten Szenarien und einem
handgeschriebenen Regelblock. Beides ist ersetzt.)

---

## 4 · Die Ableitungsregeln

| Regel | Frage des Rumpf-Schreibers | Ableitung (nur Fakten) | Quelle |
|---|---|---|---|
| **V1** | Was darf ich ausgeben? | OneOf-Typargumente der Signatur | Symbol; erzwungen vom Compiler |
| **V2** | Darf eine Ablehnung mit Events kombiniert werden? | Ausgänge mit Marker `ITransientEvent` | Marker; erzwungen zur Laufzeit im Aggregat-Actor |
| **Umfeld** | Wer schickt das? Wer hört zu? | `sends`/`compensates`/`pipelineEmits`-Kanten auf den Command, `Origin`; Apply-Slot + `EventFanout` je Ausgang; bei Apply: `produces`-Kanten auf das Event inkl. Guard | Wissensgraph |
| **B1** | Unter welcher Bedingung wird dieser Ausgang üblicherweise erzeugt? | derselbe Ausgangs-**Typ** in anderen Decide desselben Aggregats → deren Guard (umschließende `if`, aus dem Syntaxbaum) | Graph (`CommandOutcome.Guard`) |
| **B2** | Woher kommen die Werte? | je Parameter-/Feld-**Typ**: alle erreichbaren Werte bzw. setzbaren Ziele **exakt gleichen Typs** (inkl. Nullbarkeit); Sammlungen über ihren Elementtyp; generierte Member (Id/Version) sind kein Apply-Ziel | Symbole |
| **B3** | Muss ich einen Wert zusammenbauen? | ein setzbares State-Member, dessen Typ-Konstruktor **exakt die Typfolge** der Event-Felder nimmt | Symbole |
| **B4** | Welche Zustands-Member sind im Spiel? | Datenfluss der Nachbar-Slots: liest / schreibt / ruft `.Methode()`; bei Apply zusätzlich „von keinem anderen Apply geschrieben" | Roslyn Semantic Model |
| **B5** | Wie sieht so ein Rumpf hier aus? | Nachbar derselben Art mit den meisten gemeinsamen Ausgängen, dann der kürzeste | Graph + Syntax |
| **Szenarien** | Was muss gelten? | Test-DSL-Ketten (über ihre **Form**: generischer Typ über `IState`, Methode mit genau einem `ICommand`) — Decide: `Wenn(Cmd)`; Apply: Kette erwähnt das Event und prüft den Zustand | Test-Projekte |
| **Typen** | Welche Typen gibt es? | Eingang, Ausgänge, Zustand, Helfer + **vollständige** transitive Hülle der Domänen-Quelltypen | Symbole |

**Bewusst NICHT verwendet:** Namensähnlichkeit (auch nicht `cmd.Seed` ↔ `SplitGesetzt.Seed`), Relevanz-Ranking,
Zusammenfassungen, Default-Texte, Kürzungen von Code. Deshalb ist B2 bei häufigen Typen (`int`) breit — das ist
der ehrliche Preis dafür, nichts zu raten.

Heute gebaut für Decide und Apply. Für Projektion/Reader/Reaktion/Pipeline gilt dasselbe Schema; die Graph-Kanten
(`consumedBy`, `readsFrom`, Store-Aufrufe, `pipelineEmits`) existieren bereits — nur die Karten-Projektion fehlt (K6).

---

## 5 · Guardrails: fünf Wände, von billig nach teuer

Jede Wand liefert **maschinenlesbare, auf Rumpfzeilen gemappte** Befunde — genau das Futter für die
Reparaturschleife (§6).

| # | Wand | Was sie prüft | Wo (neu/bestehend) |
|---|---|---|---|
| W0 | **Ausgabeform** | Antwort ist genau *ein* Rumpf (kein Markdown-Zaun, keine Signatur, keine `using`, keine Typdeklaration). Bei lokalen Servern (llama.cpp/vLLM/Ollama) optional **grammatik-gezwungen** (JSON `{ "rumpf": "…" }`). | neu, trivial |
| W1 | **Syntax** | Roslyn parst den Rumpf als `Block`; keine lokalen Typen/Funktionen mit Seiteneffekt. | neu, trivial |
| W2 | **Kartenwand** (Kern) | Jedes im Rumpf **gebundene Symbol** (Semantic Model) ∈ Vokabular der Karte ∪ BCL-Allowlist der Slot-Art. Plus Verbotsliste je Art (Decide: `DateTime.Now`, `Guid.NewGuid`, `Random`, `Task`, `System.IO`, `HttpClient`, `this.State.*` als Zuweisungsziel …). | **neu** — als Roslyn-Analyzer über dem In-Memory-Kompilat |
| W3 | **Compile** | echte Domain-Generatoren, 0 Fehler, keine neuen Warnungen; bestehende Analyzer (CQRS001–003/012/020/021/030) greifen mit. | besteht: `/api/editor/compile` (`ModellSimulation.Kompiliere`) |
| W4 | **Verhalten** | alle Karten-Szenarien grün (store-frei, dieselbe Maschine wie Test-DSL/Sim); bei Sagas die Kaskade. | besteht: `Cqrs.Testing.Szenario`, `ModellSimulation` |
| W5 | **Mensch** | Diff nur des Rumpfs, mit Karte + Szenario-Ergebnis daneben; Hash-Sperre gegen Zwischenänderungen. | besteht teils: `CodeSync`-Hash; Diff-UI neu |

**W2 ist das eigentliche neue Guardrail.** Sie macht aus „bitte nur die Funktion schreiben" eine
*prüfbare* Aussage: das LLM kann nicht unbemerkt in Framework, Nachbar-Aggregate oder I/O greifen.
Nebeneffekt: Reinheitsregeln für Decide (kein I/O, keine Uhr, State nur lesen) wären auch für **Menschen** wertvoll → als echter
Analyzer (`CQRS04x`, Decider-Reinheit) auch außerhalb des LLM-Pfads einsetzbar — konsistent mit
Invariante 5 und der Art, wie CQRS020/021 heute schon Emit erzwingen. Erst als Analyzer dürfen sie in den VERTRAG der Karte.

---

## 6 · Der Ablauf (Füllen → Prüfen → Reparieren)

```
 Editor: H-Slot „⚙ fehlt"  ──🤖──►  Karte bauen (D)  ──►  LLM  ──►  W0 W1 W2 W3 W4
                                        ▲                                   │
                                        └──── nur die Befunde (≤ N=3) ──────┘
                                                                            │ grün
                                                                            ▼
                                                   Diff + Karte + Szenarien  ──►  W5 Mensch  ──►  CodeSync schreibt Rumpf
```

0. **(optional) Szenario-Vorschlag.** Hat der Slot keine Szenarien, bittet der Editor das LLM zuerst
   um *Szenarien* (nicht Code) auf Basis derselben Karte. Der Mensch hakt sie ab. Spezifikation =
   Beispiele, nicht Prosa — und der Mensch prüft Beispiele viel schneller als Code.
1. **Karte bauen** (deterministisch, aus Modell + Symbolen).
2. **Generieren**: System-Prompt = fester Kurztext (Ausgabeformat: nur der Rumpf); User = Karte. Kein Chat-Verlauf.
3. **Prüfen** W0–W4 in dieser Reihenfolge; die erste rote Wand bricht ab.
4. **Reparieren**: *neue* Anfrage = Karte + letzter Rumpf + **nur** die Befunde (Zeile, Code, Meldung,
   bei W4 zusätzlich Soll/Ist der Szenario-Ausgabe aus `SzenarioTrace`). Kein wachsender Verlauf —
   jede Runde ist wieder minimal. Max. N=3 Runden, dann an den Menschen (mit allen Befunden).
5. **Schreiben**: `CodeSync` ersetzt **nur** den Rumpf (heute schreibt es nur die Prompt-Zeile —
   Erweiterung um „Rumpf ersetzen" mit derselben Hash-Sperre). Die `// 🤖 Prompt:`-Zeile bleibt stehen.

**Warum das mit ~27B lokal trägt:** Die Aufgabe ist *klein, geschlossen, geprüft*. Ein Modell dieser
Größe schreibt 5–20 Zeilen C# mit bekanntem Vokabular zuverlässig; was es *nicht* zuverlässig kann —
verstreuten Kontext aus 50 k Token korrekt zusammensuchen, Framework-Konventionen erraten — wird ihm
vollständig abgenommen. Fehler, die bleiben (Tippfehler, falsche Ablehnung, vergessenes `yield break`),
fängt W3/W4 und die Reparaturrunde mit präzisem Feedback.

---

## 7 · Präsentation im Editor: „Was muss das LLM wissen?"

Die Frage des Nutzers ist wörtlich darstellbar, weil Karte und Guardrail dasselbe Objekt sind.

- **Karten-Ansicht am 🤖-Knoten**: Klick zeigt die Karte genau so, wie das LLM sie bekommt —
  gegliedert in die Abschnitte aus §3.1, mit **Token-Zähler** je Abschnitt und gesamt
  (Ampel: ≤ 2 k grün, ≤ 4 k gelb, darüber rot → Signal, dass der Slot zu breit geschnitten ist).
- **Rückwärts-Kanten**: jeder Vokabular-Eintrag verlinkt auf seinen Knoten (Command, Event, State-Feld,
  VO). Im Graphen leuchtet so der **Kontext-Schatten** des Slots auf — alles andere wird gedimmt.
  Das ist die visuelle Antwort auf „was muss das LLM wissen": genau der beleuchtete Teil.
- **Verstoß-Ansicht**: W2-Befunde werden als rote Kante vom 🤖-Knoten zu dem Symbol gezeichnet, das
  *nicht* auf der Karte stand (z. B. „griff auf `DateTime.Now` zu", „las Nachbar-Aggregat").
- **Karten-Export**: „📋 Karte kopieren" — dieselbe Karte in jedes beliebige Werkzeug (lokales Modell,
  Cloud-Modell, Mensch). Die Karte ist modell-agnostisch.
- **Abdeckung**: Übersicht aller H-Slots mit Status *fehlt / Karte zu groß / keine Szenarien /
  LLM-gefüllt+geprüft / Hand*. Das ist gleichzeitig die Antwort auf „wie viel H ist noch übrig" (Ziel D/S/H: H minimieren).

---

## 8 · Architektur-Einordnung (wo es hingehört)

| Baustein | Ort | Begründung |
|---|---|---|
| `Arbeitskarte` + `KartenBauer` (Projektion von Wissensgraph + Domänenmodell + Roslyn, Regeln B1–B5) | `GraphExtractor/Arbeitskarte.cs` | braucht Graph + Symbole; gleiche Code-Fakt-Regel (§10.5) |
| `Kartenwand` (W2) | Analyzer-Klasse, im In-Memory-Compile von `ModellSimulation` eingehängt | kein neuer Build-Pfad; Befunde = normale Diagnosen |
| Decider-Reinheit als Produkt-Analyzer | `Domain.SourceGeneration` (neben CQRS0xx) | nützt auch ohne LLM |
| LLM-Client | `SimHost` (ein Endpunkt `POST /api/editor/fill`), **OpenAI-kompatible** Schnittstelle, Basis-URL konfigurierbar | deckt Ollama / llama.cpp-Server / vLLM / LM Studio lokal und Cloud-Anbieter gleichermaßen ab; keine Modell-Bindung im Code |
| Rumpf schreiben | `SimHost/CodeSync.cs` erweitern (heute: nur Prompt-Zeile) | Hash-Sperre + Roslyn-Span bestehen schon |
| Karten-/Schatten-/Verstoß-UI | `HtmlPresenter.EditorBlock` | die EINE Oberfläche |

**Keine Verletzung der Invarianten:** keine Runtime-Reflection im Produktcode (der LLM-Pfad lebt nur
im Entwicklungswerkzeug SimHost/GraphExtractor, das Roslyn ohnehin nutzt); Routing bleibt über Typen;
Fachcode bleibt rein — die Kartenwand *erzwingt* Invariante 5 sogar zusätzlich.

**Nicht-Ziele:** das LLM erzeugt keine Records, Signaturen, Verdrahtung, Sagas-Struktur, Stores-Interfaces
(alles D/S im Editor); keine Agenten-Schleife mit Dateizugriff; kein Fine-Tuning (erst messen, §9).

---

## 9 · Messbarkeit — bevor wir bauen, beweisen

Ein **Karten-Benchmark** macht die Frage „reicht ein lokales ~27B-Modell?" empirisch statt gefühlt:

1. **Korpus aus dem Bestand:** alle heute handgeschriebenen H-Rümpfe (31 Decide, 33 Apply, Projektions-/
   Reader-/Pipeline-Handles). Jeder wird zu *Karte + Soll-Rumpf*.
2. **Ausblenden & füllen:** Rumpf entfernen, Karte bauen, Modell füllen lassen (0 und bis 3 Reparaturrunden).
3. **Bewerten** (keine Textgleichheit — Verhalten): W0–W4 grün? Plus die **bestehenden** Tests
   (Prüfstand) mit dem LLM-Rumpf an Stelle des Originals.
4. **Kennzahlen:** pass@1, pass@≤3 Runden, Token je Karte, Latenz; aufgeschlüsselt je Slot-Art.
   Verglichen: Karte vs. „ganze Datei" vs. „ganzer Ordner" als Kontext — das belegt den Minimalkontext-Effekt.
5. Szenarien für den Bestand kommen aus existierenden Tests oder werden aus einem Sim-Lauf erzeugt.

Erwartung (Hypothese, zu prüfen): Decide/Apply hoch, Projektions-Handle mittel (Store-Semantik),
Pipeline am schwächsten (I/O, Paginierung) → bestimmt die Reihenfolge der Einführung.

---

## 10 · Umsetzungsschritte (Vorschlag, klein beginnend)

| Phase | Inhalt | Ergebnis |
|---|---|---|
| **K1** | `Arbeitskarte` + `KartenBauer` nur für **Decide/Apply**; CLI `dotnet run --project GraphExtractor -- --karte <Aggregat>.<Command>` gibt die Karte als Text aus | Karte sichtbar, Token messbar — noch ohne LLM |
| **K2** | Kartenwand W2 im In-Memory-Compile; W0/W1 | Guardrail prüfbar, auch für Hand-Rümpfe |
| **K3** | Benchmark §9 gegen ein lokales Modell (OpenAI-kompatibler Endpunkt), Decide/Apply | Zahlen statt Hypothese |
| **K4** | `POST /api/editor/fill` + Reparaturschleife + `CodeSync` Rumpf-Schreiben + Diff im Editor | End-to-End im Editor |
| **K5** | Karten-Ansicht, Kontext-Schatten, Verstoß-Kanten, H-Abdeckung im Editor | die „Präsentation" |
| **K6** | weitere Slot-Arten (Projektion → Reader → Reaktion → Pipeline), jeweils erst nach Benchmark | schrittweise Ausweitung |

**Abhängigkeit:** K4 setzt voraus, dass der geschriebene Rumpf nicht wieder überdeckt wird — für
Decide/Apply ist das mit dem Sync-Kern (Analyse §4, Phase 0/1 geliefert) gegeben; für die Leseseite
erst mit Phase 3 der Editor-Roadmap (Leseseite schreibbar).

---

## 11 · Offene Entscheidungen

1. **Szenario-Pflicht?** Füllen nur, wenn ≥ 1 Szenario existiert (strenger, besser) — oder Compile+W2
   reicht für den Anfang (schneller)? Empfehlung: Pflicht für Decide, optional für Apply.
2. **Beispiel-Rumpf aus dem Bestand** in die Karte (hilft kleinen Modellen deutlich, kostet ~100 Token,
   kann aber Stil-Fehler kopieren)? Empfehlung: ja, genau einer, deterministisch gewählt.
3. **Verhaltensregeln (Reinheit von Decide, Determinismus von Apply)** als echte Analyzer bauen? Erst dann dürfen
   sie auf die Karte (VERTRAG nennt nur Erzwungenes).
4. **Grammatik-gezwungene Ausgabe** (W0) nur für lokale Server oder generell über JSON-Schema?

---

## 12 · K1 geliefert — die Arbeitskarte als Graph-Projektion (2026-09-24)

**Bau:** `GraphExtractor/Arbeitskarte.cs` (`Arbeitskarte`, `KartenBauer`, `KartenCli`). Läuft nach dem Graph-Aufbau des
Extractors und liest Wissensgraph + Domänenmodell; kein Eingriff in Extractor-Pfad, Scaffolder oder Editor, `--check` grün.

```bash
dotnet run --project GraphExtractor -- --karte                      # Übersicht aller 64 Slots + Größen + Gegenprobe
dotnet run --project GraphExtractor -- --karte SetzeSplit           # eine Karte (Disc oder Aggregat.Disc)
dotnet run --project GraphExtractor -- --karten <verz>              # alle Karten als .txt + übersicht.md + karten.json
```

**Revision (gleicher Tag):** Die erste Fassung enthielt einen handgeschriebenen Regelblock (R1–R7), eine BCL-Zeile,
einen Relevanz-Schnitt mit Wortgleichheit und eine Gegenprobe auf Namensebene. Alles entfernt bzw. ersetzt:
Regelblock → VERTRAG (nur Erzwungenes); Relevanz-Schnitt → voller Zustand + B4 (Datenfluss); Gegenprobe → Symbol-Identität.

**Messung (Bestand, 64 Slots = 31 Decide + 33 Apply):**

| | Median | Max |
|---|---:|---:|
| Arbeitskarte (echter Qwen-BPE) | **1 563** | 2 377 |
| Aggregat-Ordner | ≈ 6 300 | — |
| Domänen-Projekte gesamt | ≈ 69 000 | — |

- Die Karte ist gegenüber der ersten Fassung (Median 975) größer: voller Zustand, vollständige Typ-Hülle, Graph-Umfeld
  und B1–B4 statt eines gekürzten Ausschnitts. Immer noch ~¼ des Ordners — und ohne ausgelassene Symbole.
- **Gegenprobe (Symbol-Identität):** alle 60 geschriebenen Rümpfe benutzen nur Domänen-Symbole, die ihre Karte deklariert.
- **Kommentare:** kein Rumpf trägt eine `// 🤖 Prompt:`-Zeile; 33 der 64 Karten haben gar keinen Kommentar zum Slot.
- **Szenarien:** nur `Sammelvorgang` hat Szenario-Tests (5 Karten). Für 59 Slots liegt keine Spezifikation als Code vor.
- **B2 ist breit** bei häufigen Typen (`int`: 7 Ziele × 7 Quellen bei `SetzeSplit`) — Folge des Verzichts auf Namen.
  Engere Zuordnung ginge nur über zusätzliche Code-Fakten (z. B. typisierte Wertobjekte statt `int`).

**Nächster Schritt:** K2 (Kartenwand: LLM-Rumpf gegen die deklarierten Symbole auf dem Semantic Model prüfen) und
K3 (Benchmark gegen ein lokales Modell über einen OpenAI-kompatiblen Endpunkt).

---

## 13 · Wie isoliert sind die Code-Block-Stellen? Brauchen wir die Nachbarn? (2026-09-24)

**Werkzeug:** `dotnet run --project GraphExtractor -- --slots` (`GraphExtractor/SlotInventar.cs`). Findet ALLE Stellen, an denen ein
Code-Block hängt, über Marker und Signatur (Decide, Apply, Projektion, Reaktion, Reader, Pipeline, Store-Implementierung,
Saga-Lambda) und klassifiziert jedes gebundene Symbol jedes Rumpfs: **P** Parameter · **S** State · **F** injiziertes Feld ·
**H** Helfer der Klasse · **X** anderer Slot · **D** Domäne · **R** Framework · **B** BCL/NuGet. Für D/R wird geprüft, woher das
Wissen kommen kann: aus dem **Spielraum** (transitive Hülle der Typen aus Signatur, State, Feldern, Helfern und geerbten
Membern), zusätzlich über eine **Graph-Kante** (Pipeline → gesendete Commands, Store → seine ReadModels, Projektion → Aggregat
des Events), nur über **Nachbarn** (andere Slots derselben Klasse) bzw. **Artgenossen** (Slots derselben Art), oder **von außen**.

### 13.1 Messung (Bestand)

| Art | Slots | D/R-Symbole je Rumpf | Spielraum | +Graph | +Nachbar | von außen | injizierte Felder | geteilte Helfer |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| Decide | 31 | 5,3 | 100 % | 0 % | 0 % | 0 % | 0/31 | 0/31 |
| Apply | 33 | 2,5 | 100 % | 0 % | 0 % | 0 % | 0/33 | 4/33 (`SetBild`) |
| Projektion | 36 | 10,1 | 90 % | 10 % | 0 % | 0 % | 36/36 (1 Store) | 0/36 |
| Reader | 17 | 7,5 | 98 % | 0 % | 0 % | 2 % | 17/17 | 0/17 |
| Pipeline | 11 | 4,4 | 65 % | 17 % | 0 % | 19 % | 7/11 | 3/11 |
| Store-Impl | 56 | 5,8 | 63 % | 37 % | 0 % | 0 % | 29/56 (Marten) | 3/56 |
| Saga-Lambda | 1 | 2,0 | 100 % | 0 % | 0 % | 0 % | — | — |

**Lesart:**
- **Das „Was“ (welche Symbole) liefern Signatur + Graph fast vollständig.** Nachbarn tragen zum Wortschatz nach der
  Korrektur 0 % bei. Der Graph ist dabei nicht optional: ohne die Kanten fehlen bei Projektionen das Aggregat für
  `Track<Agg>` (10 %), bei Pipelines die gesendeten Commands (17 %), bei Stores die ReadModels (37 %).
- **Das „Wie“ liefern die Nachbarn.** Alle 36 Projektions-Rümpfe folgen demselben Protokoll
  (`writer.Execute(key, async ctx => { ctx.Track<Agg>(envelope.AggregateId); await _store.X(…, envelope.CreatedAtUtc); })`),
  13/17 Reader rufen `ctx.Track`, die Stores benutzen eine kleine Teilmenge der Marten-API (`QuerySession`, `LoadAsync`,
  `Store`, `LightweightSession`) bzw. die Basis-Primitive `EnqueueTransform/EnqueueStore/Enqueue`. Die Typen *erlauben* das,
  aber nur die Nachbarn *zeigen*, wie es hier gemacht wird.
- **Von außen** kommt Wissen nur an zwei Stellen: Pipelines benutzen Domänen-Helfer ohne Kante (`SplitZuteiler`,
  `ImagePairFileName`), ein Reader benutzt den statischen Helfer eines anderen Readers (`ImagePairReader.ToAntwort`).
- **Isolation:** Decide/Apply sind vollständig isoliert (Generator: `new X.Decider(state)` — nichts anderes erreichbar).
  Projektion/Reader hängen an genau einem Store. **Pipelines sind die am wenigsten isolierte Stelle:** Dienste, Logger,
  Konfigurationen und **geteilter veränderlicher Klassenzustand** (`FileWatchPipeline._seen/_pending`, von mehreren
  Methoden benutzt) — ein Handle ist dort nicht allein verstehbar.

### 13.2 Randfälle von Aufträgen (LLM-Queries) an einem Code-Block

Ein Auftrag im Editor kann (a) im Spielraum lösbar sein, (b) eine **Strukturänderung** brauchen (Graph/Editor, D/S — nicht
Aufgabe des LLM), (c) **mehrere Slots gekoppelt** betreffen oder (d) am **falschen Konsumenten** hängen.

| # | Stelle | Auftrag (Beispiel) | Klasse | Was der Kontext zeigen muss / was stattdessen passiert | Beleg im Bestand |
|---|---|---|---|---|---|
| 1 | Decide | braucht Read-Model-Daten („nur Paare aus Suche X") | b | nicht erreichbar → Zwei-Phasen-Muster: Event → Pipeline liest Store → Command mit Daten | `FuegeRangeHinzu` → `RangeAngefordert` → `DatensatzResolverPipeline` → `NimmRangeAuf` |
| 2 | Decide | braucht die Uhrzeit („älter als 2 h") | b | keine Uhr im Decider → Zeit als Command-Feld oder Frist (`IFristplan` in Pipeline, fällig → Command) | `TrainingFristPipeline` (`IDbClock`, `IFristplan`) → `MarkiereAlsHaengengeblieben` |
| 3 | Decide | neuer Ablehnungsgrund | b | OneOf-Ausgang fehlt → erst im Editor verdrahten, dann neue Karte | — |
| 4 | Decide | Regel über Zustand, den es noch nicht gibt („max. 3 Ranges") | b + c | State-Feld (D) **und** die Apply-Slots, die es schreiben müssen → Kopplungs-Kontext aus B4 | — |
| 5 | Decide | Idempotenz („zweimal = nichts tun") | a / c | im Spielraum, wenn der State es trägt (`IstDraftMitglied`); sonst wie 4 | `NimmPaarAuf`, `EntfernePaar` |
| 6 | Apply | „ungültige Werte ignorieren" | d | Apply entscheidet nicht (void, Replay) → gehört in Decide | — |
| 7 | Apply | braucht Wert, den das Event nicht trägt | b + c | Event-Feld (D) **und** alle erzeugenden Decide-Slots (Graph: `produces`-Kanten auf das Event) | — |
| 8 | Projektion | „danach Command X senden" | d | Projektion emittiert nicht (CQRS020/021) → Reaktion | — |
| 9 | Projektion | braucht Daten eines anderen Aggregats (Join) | b + c | nur der eigene Store ist injiziert → Store-Fn mit Join (**zweiter Slot**: Store-Impl) oder Event trägt die Daten | `DatensatzStore` |
| 10 | Projektion | neue Store-Funktion nötig | b + c | Interface-Fn (D, Kante Handle→Fn) **plus** Store-Impl-Slot | — |
| 11 | Reader | Daten aus zwei Stores | b | zweiter Store als Kante (Ctor) | `DatensatzReader` (`_store` + `_imagePairs`) |
| 12 | Reader | „wie in Reader Y formatieren" | c | Wiederverwendung über Klassengrenzen — heute Wissen von außen | `ImagePairReader.ToAntwort` |
| 13 | Pipeline | externer Dienst (Klassifizierer, Bildbearbeitung) | b | Dienst-Knoten (Vertrag → Impl); nur der Vertrag ist Spielraum | `ImageProcessingPipeline` |
| 14 | Pipeline | Zustand zwischen Aufrufen merken | c | geteilter Klassenzustand → alle Methoden, die ihn benutzen, gehören in den Kontext; verlierbar (Inv. 6) | `FileWatchPipeline._seen/_pending` |
| 15 | Pipeline | Domänen-Berechnung (Split zuteilen) | b | Domänen-Helfer ohne Kante → als Dienst/Kante verdrahten, sonst von außen | `SplitZuteiler` |
| 16 | Store-Impl | Abfrage mit Filter/Paging | a | Spielraum = Basis-Primitive + ReadModel; Marten-API nur über Nachbarn (Präzedenz) | `ImagePairStorePostgres.SearchAsync` |
| 17 | Saga | Ziel-Command braucht Feld, das kein Bedingungs-Event trägt | b | Event-Feld (D) oder Pipeline statt Saga | — |
| 18 | alle | Auftrag widerspricht einem Szenario | — | W4 fängt es; Szenario gewinnt | — |
| 19 | alle | Umbenennen/Signatur ändern | b | kein H-Auftrag — Editor-Struktur | — |
| 20 | alle | „wie bei Slot Y" | c | expliziter Verweis → Y als Präzedenz in den Kontext (als Editor-Kante „Vorlage ◀", nicht per Textsuche) | — |

**Erkennung ohne Semantik:** Die Maschine kann den Auftragstext nicht verstehen. Sie kann aber (1) den Spielraum vollständig
angeben, (2) strukturelle Signale aus dem Editor lesen (Query an einen Decider gehängt, Command-Ausgang an einer Projektion)
und (3) dem LLM einen **Antwortkanal** geben: statt eines Rumpfs darf es strukturiert antworten
`AUSSERHALB: braucht <Art> <Name>` (z. B. „OneOf-Ausgang `RangeZuGross`", „State-Feld `AnzahlRanges : int`"). Die Kartenwand
(K2) prüft umgekehrt, dass ein gelieferter Rumpf den Spielraum nicht verlässt.

### 13.3 Folgerung: Kontext in drei Schichten — ja, mit Nachbarn

| Schicht | Frage | Quelle | fehlt, wenn … |
|---|---|---|---|
| **Spielraum** | Was steht zur Verfügung? | Signatur + State + Felder + geerbte Member + **Graph-Kanten** | nie (aus Generator/Graph ableitbar, auch für leere Slots und Entwürfe) |
| **Präzedenz** | Wie wird es hier benutzt? | Nachbarn (gleiche Klasse), sonst **Artgenossen** (gleiche Art, andere Klasse), sonst ein Referenz-Slot der Art | erster Slot seiner Art im ganzen System — dann Referenz-Slot (z. B. aus der Sonde) |
| **Kopplung** | Was muss mit geändert/gelesen werden? | B4 (State-Datenfluss), `produces`-Kanten, geteilte Helfer/Felder | nur bei gekoppelten Aufträgen (Fälle 4, 7, 9, 10, 14) |

Dazu immer **Auftrag** (Editor/Kommentar) und **Spezifikation** (Szenarien), und der Antwortkanal `AUSSERHALB`.

**Zwei konkrete Konsequenzen:**
1. **Wo die Präzedenz ein festes Protokoll ist, wird sie D statt H.** Projektions-Handles folgen zu 36/36 demselben
   Gerüst → der Scaffolder erzeugt `writer.Execute(…, async ctx => { ctx.Track<Agg>(…); /* H */ })` deterministisch (das
   Aggregat liefert der Graph), und der H-Slot schrumpft auf den inneren Block. Analog `ctx.Track` im Reader.
2. **Pipelines brauchen die Klasse, nicht die Methode, als Kontext-Einheit**, sobald Felder geteilt veränderlich sind.
