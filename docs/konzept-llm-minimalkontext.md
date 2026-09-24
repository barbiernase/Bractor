# Konzept — Minimalkontext für ein (lokales) LLM: die Arbeitskarte

> **Stand:** 2026-09-24 · **Status:** K1 GEBAUT (Arbeitskarte für Decide/Apply als CLI, §12); K2–K6 Konzept.
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

## 2 · Das Prinzip: geschlossene Welt je Slot

> **Das LLM bekommt keine Dateien. Es bekommt eine Karte.**
> Eine Karte beschreibt eine **geschlossene Welt**: eine fixe Signatur, ein endliches Vokabular
> (genau die Symbole, die es benutzen darf), ein paar Regeln der Slot-Art, die Absicht und die
> Beispiele, die bestehen müssen. Was nicht auf der Karte steht, existiert für das LLM nicht — und
> darf im Ergebnis auch nicht vorkommen (die Kartenwand prüft das, §5).

Drei Sätze tragen das Konzept:

1. **Karte = Ableitung, nie Handarbeit.** Sie wird zu 100 % aus dem Extractor-Modell + Roslyn-Symbolen
   erzeugt (Klasse D). Keine Namenskonvention, kein Raten — dieselbe Regel wie §10.5.
2. **Das LLM schreibt nur den Rumpf zwischen `{` und `}`.** Signatur, `using`s, Klasse, Datei, Platzierung
   bestimmt die Maschine. Das LLM kann sie nicht einmal ausdrücken.
3. **Vokabular der Karte = Allowlist der Prüfung.** Was auf der Karte steht, ist exakt das, was die
   Kartenwand zulässt. Präsentation und Guardrail sind *dasselbe Objekt*.

---

## 3 · Die Arbeitskarte — Aufbau am echten Beispiel

Slot: `Datensatz.Decider.Decide(SetzeSplit)` (H, Schreibseite). So sähe die Karte aus, die das LLM
**vollständig** bekommt (≈ 700 Token):

```text
## AUFGABE
Schreibe NUR den Methodenrumpf (ohne Signatur, ohne geschweifte Klammern des Rumpfs, ohne using).

## SIGNATUR (fix)
IEnumerable<OneOf<SplitGesetzt, DatensatzBereitsEingefroren, SplitUngueltig>> Decide(SetzeSplit cmd)
Verfügbar: this.State (Typ Datensatz, nur lesen), cmd.

## ABSICHT   (aus // 🤖 Prompt: + <summary>)
Split-Override setzen. Eingefroren → ablehnen. Anteile müssen gültig sein (SplitKonfig.IstGueltig).

## VOKABULAR (abschließend)
Eingang   SetzeSplit(Guid AggregateId, int TrainProzent, int ValProzent, int TestProzent, int Seed)
Zustand   Datensatz  (nur lesen)
            bool IstEingefroren          // Status == Eingefroren
            bool Existiert               // Stream hat ≥ 1 Event
            SplitKonfig Split
Ausgänge  SplitGesetzt(int TrainProzent, int ValProzent, int TestProzent, int Seed)   [Event]
          DatensatzBereitsEingefroren(Guid DatensatzId)                             [Ablehnung]
          SplitUngueltig(int TrainProzent, int ValProzent, int TestProzent)          [Ablehnung]
Typen     record SplitKonfig(int TrainProzent, int ValProzent, int TestProzent, int Seed)
            bool IstGueltig             // alle ≥ 0 und Summe == 100
            static SplitKonfig Default
BCL       Vergleiche, Arithmetik, System.Linq auf Sammlungen, string, Guid, Math

## REGELN (Slot-Art: Decide)
R1 Rein: kein I/O, kein await, kein DateTime.Now/UtcNow, kein Guid.NewGuid, kein Random.
R2 Nur `yield return new <Ausgang>(...)` mit Typen aus AUSGÄNGE.
R3 Eine Ablehnung steht ALLEIN: `yield return new <Ablehnung>(...); yield break;`
R4 Idempotent ohne Wirkung → `yield break;` ohne Event.
R5 Zustand nie verändern (das tut Apply).

## BEISPIEL (Nachbar-Rumpf derselben Art, aus dem echten Code)
// Decide(EntfernePaar)
if (this.State.IstEingefroren) { yield return new DatensatzBereitsEingefroren(cmd.AggregateId); yield break; }
if (!this.State.IstDraftMitglied(cmd.ImagePairId)) yield break;
yield return new PaarEntfernt(cmd.ImagePairId);

## MUSS BESTEHEN (Szenarien)
S1 Gegeben: DatensatzEingefroren(...)           Wenn: SetzeSplit(_,70,15,15,1) Dann: DatensatzBereitsEingefroren
S2 Gegeben: DatensatzErstellt("x")              Wenn: SetzeSplit(_,50,50,10,1) Dann: SplitUngueltig(50,50,10)
S3 Gegeben: DatensatzErstellt("x")              Wenn: SetzeSplit(_,80,10,10,7) Dann: SplitGesetzt(80,10,10,7)
```

**Was bewusst NICHT auf der Karte steht:** die Datei, andere Decide-Methoden, der Applier, andere
Aggregate, Projektionen, Proto/Marten/Actors, `IDecider`, `OneOf`-Implementierung, Generatoren,
Namespaces, `using`s, Framework-Doku, CLAUDE.md. Auch nicht: State-Member, die mit dem Slot nichts zu
tun haben (s. §4 Relevanz-Schnitt) — die Karte zeigt `IstEingefroren`, nicht `DraftMitglieder`.

### 3.1 Karten-Abschnitte (allgemein)

| Abschnitt | Quelle (Code-Fakt) | Klasse |
|---|---|---|
| Signatur | Methoden-Symbol am Anker (`CodeAnker`: Kind + Disc) | D |
| Absicht | `// 🤖 Prompt:` im Rumpf, `<summary>` von Methode/Command/Event | Mensch |
| Vokabular | Parametertypen, OneOf-Argumente, deren Ctor-Parameter; State-Member; transitiv referenzierte VOs/Enums (Tiefe 1–2); **nur öffentliche Oberfläche**, Rümpfe nur bei *Expression-bodied Helfern* als Kommentar-Kurzform | D |
| Regeln | fester Regelblock je **Slot-Art** (§4), versioniert im Repo | D (statisch) |
| Beispiel | 1 Nachbar-Rumpf **gleicher Slot-Art im selben Aggregat/Konsumenten**, deterministisch gewählt (kürzester mit Ablehnungs-Muster; sonst keiner) | D |
| Szenarien | Editor-Sim-Session („📋 Als Test"), vorhandene `Szenario`-Tests, oder vom Menschen bestätigte, vom LLM *vorgeschlagene* Szenarien (§6 Schritt 0) | Mensch/D |

---

## 4 · Slot-Arten: was je Art auf die Karte muss

Die Karte ist je Slot-Art verschieden — aber immer klein, weil die Art das Vokabular hart begrenzt.

| Slot-Art | Signatur-Form | Vokabular (nur das!) | Regeln (Kern) | Verifikation |
|---|---|---|---|---|
| **Decide** | `IEnumerable<OneOf<…>> Decide(Cmd)` | Cmd-Felder, **relevante** State-Member, OneOf-Ausgänge + Ctors, VOs/Enums | R1–R5 (s. §3) | Compile + Szenarien (store-frei) |
| **Apply** | `void Apply(Evt)` | Evt-Felder, **schreibbare** State-Member, VOs/Enums | nur Zustand setzen; keine Entscheidungen; keine Events; bewusst leerer Rumpf erlaubt | Compile + Fold-Szenario (Gegeben → Zustand) |
| **Projektion-Handle** | `Task Handle(Evt, IAggregateEnvelope, ProjectionWriter)` | Evt-Felder, `envelope.AggregateId/CreatedAtUtc`, **nur die verdrahteten** Store-Fns (aus den Handle→Fn-Kanten, nicht das ganze Interface), ReadModel-Felder | Muster `writer.Execute(key, async ctx => { ctx.Track<Agg>(id); await _store.X(...); })` als **Gerüst D**, LLM füllt nur den inneren Block | Compile; (später Leseseiten-Sim) |
| **Reader-Handle** | `Task<OneOf<R…>> Handle(Q, env, ReadContext)` | Query-Felder, verdrahtete Read-Fns, Response-Ctors | nur lesen; `ctx.Track` Gerüst D | Compile; (später Sim) |
| **Reaktion** | `IAsyncEnumerable<OneOf<Cmd…/E…>> Handle(Evt, …)` | Evt-Felder, erlaubte Commands/Events + Ctors | nur `yield`; Emit ausschließlich per `yield` (CQRS020/021 prüft ohnehin) | Compile + CQRS020/021 |
| **Pipeline-Handle** | `IAsyncEnumerable<ICommand> Handle(T, PipelineContext)` | T-Felder, **verdrahtete** Dienst-Verträge (nur Signaturen), `ctx.SourceAggregateId`, `ctx.ScheduleSelf`, erlaubte Commands | I/O nur über verdrahtete Dienste; kein Store-Schreiben | Compile (+ Dienst-Fakes aus Vertrag, später) |
| **Store-Impl** | Fn des `I…WriteStore` | ReadModel, 3 Co-Commit-Primitive | eher **S** als H (kleines Vokabular) → erst deterministisch versuchen | Integration (echtes Marten) |
| **Saga-Argumente** | `e => new Cmd(…)` | Auslöser-Event-Felder, Ziel-Command-Ctor | **zuerst D** (Feld-Mapping über Name+Typ); LLM nur bei Mehrdeutigkeit, dann als *Auswahl*, nicht Freitext | Compile + Saga-Sim |

**Relevanz-Schnitt (wichtig für kleine Modelle):** Die Karte zeigt nicht *alle* State-Member, sondern

1. die, die im Absichtstext/Szenarien namentlich vorkommen,
2. die, die **Nachbar-Rümpfe derselben Art** lesen (Datenfluss aus dem Semantic Model),
3. berechnete Helfer (`bool Ist…`) grundsätzlich (billig, hoher Nutzen),
4. Rest als **eine Zeile „weitere: Name:Typ, …"** — sichtbar, aber ohne Doku.

So bleibt die Welt vollständig (nichts Nötiges fehlt), aber die Aufmerksamkeit liegt auf dem Relevanten.

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
Nebeneffekt: die Decide-Reinheitsregeln (R1, R5) sind auch für **Menschen** wertvoll → als echter
Analyzer (`CQRS04x`, Decider-Reinheit) auch außerhalb des LLM-Pfads einsetzbar — konsistent mit
Invariante 5 und der Art, wie CQRS020/021 heute schon Emit erzwingen.

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
2. **Generieren**: System-Prompt = fester Kurztext + Regelblock der Art; User = Karte. Kein Chat-Verlauf.
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
| `Arbeitskarte` (Modell: Signatur, Vokabular-Einträge mit Symbol-ID, Regeln, Beispiel, Szenarien) + `KartenBauer` | `GraphExtractor` (neben `DomainExtractor`) | braucht Roslyn-Symbole + Extractor-Modell; gleiche Code-Fakt-Regel (§10.5) |
| Regelblöcke je Slot-Art | `GraphExtractor/Karten/*.txt` (versioniert) | statisch, reviewbar; Framework-Namen über `Vertrag.cs` (`nameof`) eingesetzt |
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
3. **Sprache der Karte:** Deutsch (konsistent mit Domäne) — Regelblock ggf. zusätzlich englisch testen,
   falls das lokale Modell auf englische Instruktionen messbar besser reagiert (Benchmark K3 entscheidet).
4. **Grammatik-gezwungene Ausgabe** (W0) nur für lokale Server oder generell über JSON-Schema?

---

## 12 · K1 geliefert — die Arbeitskarte als CLI (2026-09-24)

**Bau:** `GraphExtractor/Arbeitskarte.cs` (`Arbeitskarte`, `KartenBauer`, `KartenCli`) + Regelblöcke
`GraphExtractor/Karten/{decide,apply}.txt` (EmbeddedResource). Kein Eingriff in Extractor-Pfad, Scaffolder oder Editor;
`--check` bleibt grün.

```bash
dotnet run --project GraphExtractor -- --karte                      # Übersicht aller 64 Slots + Größenvergleich + Gegenprobe
dotnet run --project GraphExtractor -- --karte SetzeSplit           # eine Karte (Disc oder Aggregat.Disc)
dotnet run --project GraphExtractor -- --karten <verz>              # alle Karten als .txt + übersicht.md + karten.json
```

**Was die Karte aus Code-Fakten zieht:**

| Abschnitt | Quelle |
|---|---|
| Slots | Typen mit `IDecider<T>`/`IApplier<T>`; Methoden, deren 1. Parameter `ICommand`/`IEvent` ist (handgeschrieben) |
| Rumpf-Status | `geschrieben` · `leer` (bewusster Marker) · `fehlt` (= `throw new NotImplementedException`, per Symbol) |
| Signatur/Ausgänge | Methoden-Symbol; `OneOf`-Typargumente; Ablehnung = `ITransientEvent` |
| Absicht | `// 🤖 Prompt:` im Rumpf, `<summary>` der Methode, Banner-Kommentare vor Methode und Eingangstyp |
| Zustand | öffentliche State-Member; ≤ 8 handgeschriebene → alle, sonst Relevanz-Schnitt (von Nachbar-Rümpfen benutzt ∪ bool-Helfer bei Decide ∪ Wortgleichheit) — Rest als „weitere: Name:Typ“ |
| Helfer | vorhandene Nicht-Slot-Methoden der Decider-/Applier-Klasse (nur Signatur) — **nachgerüstet, weil die Gegenprobe sie vermisste** |
| Typen | transitiv (Tiefe 2) aus Eingang, Ausgängen, relevantem Zustand, Helfer-Parametern — nur Domänen-Quelltypen; Records mit Primär-Ctor + öffentlicher Zusatz-Oberfläche, Enums mit Werten |
| Beispiel | Nachbar-Rumpf derselben Art: meiste geteilte Ausgänge, dann kürzester |
| Szenarien | aus Test-Projekten über die **Form** der Test-DSL (generischer Typ über einen `IState`, Methode mit genau einem `ICommand`) — Decide: `Wenn(Cmd)`, Apply: Kette erwähnt das Event und prüft den Zustand (Lambda) |

**Messung (Bestand, 64 Slots = 31 Decide + 33 Apply):**

| | Median | Max |
|---|---:|---:|
| Arbeitskarte (echter Qwen-BPE) | **975** | 1 391 |
| Aggregat-Ordner (was man sonst mitgäbe) | ≈ 6 300 | — |
| Domänen-Projekte gesamt | ≈ 69 000 | — |

- **Gegenprobe (Vorstufe W2):** alle 60 geschriebenen Rümpfe benutzen **nur** Domänen-Symbole, die auf ihrer Karte
  stehen (Namensebene). Der erste Lauf fand 4 Lücken (`Applier.SetBild`) → Abschnitt „Helfer“ ergänzt → 60/60.
- **Token-Schätzung** (Zeichen/3,3) gegen den Qwen-BPE kalibriert: Verhältnis echt/Schätzung 0,99 über alle Karten.
- **Befund Absicht:** kein Rumpf im Bestand trägt eine `// 🤖 Prompt:`-Zeile; die Absicht kommt heute aus Doku/Bannern
  und ist oft dünn (z. B. `SetzeSplit`: nur „SPLIT — optionaler Override (Default 70/15/15)“). Das Wissen steckt dann
  in Typ-Doku (`SplitKonfig.IstGueltig`) — die Karte bringt es mit.
- **Befund Szenarien:** nur `Sammelvorgang` hat Szenario-Tests (5 Karten mit Szenarien). Für die übrigen 59 Slots
  ist das die größte Lücke der Spezifikation → stützt Entscheidung 1 (§11: Szenario-Pflicht für Decide).
- **Fixkosten:** Aufgabe + Regeln ≈ 350 Token je Karte. Bei winzigen Aggregaten ist die Karte daher größer als die
  Decider-Datei allein — die Datei allein reicht aber nicht (ihr fehlen State, Events, VOs).

**Nächster Schritt:** K2 (Kartenwand als echte Prüfung auf dem Semantic Model eines LLM-Rumpfs, statt Namensebene) und
K3 (Benchmark gegen ein lokales Modell über einen OpenAI-kompatiblen Endpunkt).
