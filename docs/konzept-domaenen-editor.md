# Konzept & Anleitung — Der Domänen-Editor

> **Stand:** 2026-09-18 · **Status:** GEBAUT (Editor-only), teils ohne Codegen/Sim (je Abschnitt vermerkt).
> **Ort:** ausschließlich `GraphExtractor/HtmlPresenter.cs`, Konstante `EditorBlock` (ein eingebettetes
> HTML/CSS/JS, aus C# als String erzeugt). **Die EINE Oberfläche:** `http://localhost:5178/editor`
> (`/` leitet dorthin um). Das frühere read-only Board (`knowledge-graph.html`) und seine SimEngine sind
> seit 2026-09-24 im Editor aufgegangen (Simulation §10.4).
> Scaffolder/`ModellMapper`/Proto sind – wo nicht ausdrücklich anders vermerkt – **unangetastet**.
>
> **Diese Datei ersetzt** (2026-09-18 zusammengeführt): `konzept-composition-root-editor.md`,
> `konzept-projektionen-und-store-nodes.md`, `konzept-saga-nodes.md`, `konzept-feld-ports.md`,
> `anleitung-editor-frist-und-leseachsen.md`.
>
> Verwandt: [04-konsum-und-prozess-maschine.md](04-konsum-und-prozess-maschine.md) (die vier
> Konsumenten, eine Maschine), [konzept-exactly-once-naht.md](konzept-exactly-once-naht.md)
> (Co-Commit), [07-graph-und-simulation.md](07-graph-und-simulation.md) (Extractor + SimHost),
> [anleitung-prozess-schreiben.md](anleitung-prozess-schreiben.md).

---

## 1 · Was der Editor ist

Ein vollständiger **ComfyUI-Node-Editor**: getippte Slots (farbige Punkte) + Bézier-Kanten statt
Dropdowns. Ein einziges `MODEL`-Objekt (`schemaVersion:"2"`, ~26 Sammlungen) ist die Wahrheit;
**alles andere ist Ableitung** — Aggregat-Zugehörigkeit, Store-Scope, Graph-Komponenten, Layout.

Bedienung: Kopf ziehen = verschieben (rastet 20 px); von einem Slot-Punkt ziehen = verbinden
(nur typgleiche Ports rasten ein, gültige Ziele ringeln beim Ziehen **grün**); Fläche ziehen = pannen;
Rad = zoomen; Doppelklick auf die Fläche = Knoten-Picker; Palette hovern = Typ hervorheben;
Ctrl/Cmd+Klick auf einen Palette-Button = der Reihe nach zum nächsten Knoten dieses Typs springen.
Minimap (klick-/ziehbar), Domänen-Filter (🗂, pro Browser persistiert) und ein Insel-Zähler
(„⚠ N Inseln" = unverbundene Knoten aus der Graph-Analyse) sind eingebaut.

Der Editor bildet inzwischen **fast die ganze Architektur** ab: Schreibseite, Prozess, Leseseite,
Ingress/Betrieb und die Logik-Naht (📝/🤖).

---

## 2 · Leitprinzipien (das Rückgrat)

**(a) Verdrahten statt deklarieren.** Ein Interface ist nicht gezeichnet — es ist der **Querschnitt
der Verdrahtung**: jeder Draht *Projektion→Store* wird eine Write-Methode, jeder *Reader→Store* eine
Read-Methode; Signaturen fallen aus den Port-Typen ab. Der Store-Scope eines Konsumenten wird aus
seinen Handle→Fn-Kanten **abgeleitet** (`derivedStores`), nicht deklariert. Aggregat-Zugehörigkeit
folgt dem Namespace (`deriveMembership`).

**(b) D/S/H — der LLM ist die Ausnahme.** Jedes Artefakt/jeder Rumpf gehört zu genau einer Klasse;
Ziel: **D + S maximieren, H minimieren.**
- **D — Deterministisch abgeleitet** aus Verdrahtung + Typen. Kein Tippen, kein LLM. (Records,
  Interfaces, Projektions-Handle, Feld-Mappings, DI.)
- **S — Strukturiert** aus einem typisierten Vokabular gewählt. Kein Freicode, kein LLM.
- **H — Freie Logik**, hand getippt **oder** LLM-gestützt — die **einzige** Stelle, an der der LLM
  überhaupt auftaucht. Immer sim-verifiziert.

**(c) Code als verdrahteter Wert.** Ein füllbarer Rumpf ist **kein Textfeld im Knoten**, sondern ein
**Code-Eingang (Port)**. Code fließt von einem **📝 Code-Knoten** (manuell getippt) oder **🤖 LLM-Knoten**
(generiert) mit `code ▶`-Ausgang — beliebig austauschbar. Der **Vertrag fließt rückwärts**: der
LLM-Knoten leitet Signatur/Event/Interfaces aus dem Ziel-Slot ab (nicht getippt). Das gilt **überall**,
auch für Decide-/Apply-Rümpfe; Domänen-Knoten enthalten **keine** Code-Textfelder mehr. Beim Laden
werden alte eingebettete Rümpfe automatisch in einen 📝-Knoten **migriert** und verdrahtet
(`normalize`). Ein leerer Rumpf-Port zeigt einen deutlichen „⚙ …fehlt"-Marker + Ein-Klick-Knöpfe
(＋📝 / ＋🤖), die den Knoten anlegen und sofort andocken — so werden „Code-Inseln" auffindbar.

**(d) Die eine sichtbare unreine Naht.** Invariante 5 (Fachcode bleibt rein) hat einen bewusst
erlaubten Gegenpol: den Composition Root (§7). Der Editor macht diese Grenze **sichtbar**, statt sie
in `Program.cs` zu verstecken — dieselbe Bewegung wie bei den Code-Inseln.

---

## 3 · Die Node-Palette (Ports + was generiert wird)

| Gruppe | Knoten | Kernports | Klasse |
|---|---|---|---|
| **Schreibseite** | Command · Event · Ablehnung · Value Object · Enum | typ-spezifische in/out (cmd/evt/qrsp…), `als Feldtyp ▶` bei VO/Enum | D |
| | Aggregat (Hub) | oben `State ◀`, links Decider (autom.), rechts Applier (autom.) | D |
| | State | `Aggregat ▶`, Feld-Zeilen (Typ per Dropdown) | D |
| | Decider | `Command ◀`, `Aggregat ▲`, **OneOf-Event-Ausgänge** (je Outcome ein Punkt), `Decide-Rumpf ◀ code` | Struktur D, Rumpf H |
| | Applier | `Event ◀`, `Aggregat ▲`, `Apply-Rumpf ◀ code` | Struktur D, Rumpf H |
| **Prozess** | Prozess (Saga-Rahmen) | `Auslöser ▲`, „Regeln ◀ anstecken" | D |
| | Regel | `→ Prozess`, **Wenn-Join ◀** (beliebig viele), **Dann ▶** (je Command; ×N-Marke), **↩** Kompensation | D/S (Args = Stub) |
| **Leseseite** | ReadModel | `Store ▶`, Dokument-Felder | D |
| | Store (Interface-Editor) | `Read Models ▲`, Write-/Read-Fn (Signatur + `Impl ◀ code`), je Fn ein Aufruf-Port | Interfaces D, Rümpfe S/H |
| | Projektion (Controller) | Trigger-`Event ◀` je Handle, `ruft Write-Fn ▶`, `veröffentlicht Event ▶` (reaktiv), `Controller ◀ code`; Achsen Pull/Append | D |
| | Reader (Controller) | `liest Projektion ▶` (IReader<TProjektion>), `Query ◀` je Handle, `ruft Read-Fn ▶`, OneOf-`Response ▶`, `Controller ◀ code` | D/S |
| | Reaktion (emittierend) | Trigger-`Event ◀`, `sendet Command ▶` (OneOf), `veröffentlicht Event ▶`, `Controller ◀ code` | D |
| | Query · Response | `query ▶` / `◀ von Reader` | D |
| **Betrieb/Host** | Trigger (Ingress) | Modus timer/webhook/filewatch/frist + Config, `erzeugt TriggerMsg ▶` | D/S |
| | Pipeline (4. Konsument) | Handle-Eingang Trigger/Event/Self, `yield Command ▶`, `erzeugt Trigger ▶`, `plant Self-Tick ↺`, `nutzt Dienst ◀`, `Rumpf ◀ code` | D, Rumpf H |
| | Frist (Drei-End-Relation) | `plant ◀ Event`, `storniert ◀ Event`, `Dauer ◀ HostSetting`, `fällig → Command ▶` | D |
| | Dienst (Vertrag→Impl) | `Vertrag ▶`, `Impl ◀ code` **oder** „extern"-Marker | D + H/extern |
| | HostSetting | `{Name,Typ,Default,EnvKey}`, `Wert ▶` | D |
| **Logik** | 📝 Code / 🤖 LLM | `code ▶`; Klick öffnet ein editierbares Modal (Text bzw. Intent) | H |

**Kanten-Wahrheit:** `boardEdges` (Knoten→Knoten) spiegelt exakt `drawEdges` (Slot→Slot). Daraus
Union-Find-Komponenten → mess-basiertes Shelf-Packing (`packLayout`) + Insel-Erkennung. Die
Typprüfung sitzt in `compatible` (gleicher Typ, entgegengesetzte Richtung; Feld-Wunsch-Constraints).

---

## 4 · Schreibseite

- **Record-zentrisch:** ein Editor für Command/Event/Ablehnung/Value Object; Kind per Dropdown.
  Command bekommt automatisch `AggregateId : Guid`; `Erzeugung`-Häkchen = `ICreationCommand`.
- **Aggregat = HUB, kein Feld-Träger:** State oben andocken, Decider (links) und Applier (rechts)
  werden **abgeleitet** angezeigt (Zugehörigkeit über Namespace, nicht per Hand-Kante). Interne
  Linien zeigen: welcher Decider erzeugt das Event welches Appliers.
- **Decider = OneOf:** je mögliches Event **ein** Ausgangs-Punkt (Punkt → Event ziehen). Das *WANN*
  macht der Decide-Rumpf (📝/🤖) — der Editor verdrahtet nur die möglichen Ausgänge.
- **Applier:** gespiegelt (Event rein, Aggregat oben, Apply-Rumpf als Code-Port).
- **Typ-Komposition:** ein VO-/Enum-Knoten hat `als Feldtyp ▶`; zieht man ihn auf den kleinen
  `◀`-Typ-Eingang eines Feldes, wird der Feldtyp gesetzt (Collection-/Array-/Nullable-Wrapper bleibt
  erhalten). Umbenennen zieht alle Feldtypen mit (`retypeFelder`). So komponieren sich Records
  visuell. **Dieser `ftype`-Port ist voll funktionsfähig.**

---

## 5 · Prozess: Saga-Rahmen + Regel-Knoten

Ersetzt die alte, überladene Transition-Karte (zweispaltig, mit komplettem Argument-Pin-Block und
kryptischen `t/r/g`-Rollen). Die Laufzeit ist ein **Petri-Netz** (Marking aus Tokens; eine Regel =
eine Transition, die Event-Typen joint und Commands feuert). Der Editor bildet das als **Fluss** ab.

**DSL-Wahrheit** (`Abstractions/Prozess/ProzessBuilder.cs`): eine Regel ist
`Bedingung[] + Sende + RückgängigDurch?`. Sie wird atomar beim `.Sende` registriert.

**Node-Typen:**
1. **Prozess** = Rahmen `Prozess<Auslöser>.Definiere`: Auslöser-Event oben, „berührt (abgeleitet)",
   „Regeln ◀ anstecken".
2. **Regel** = kleine **Kreuzung** (keine wiederholten Namen, keine Sektionen; Identität über
   Hover-Titel + Kante):
   - **Wenn ◀ (Join):** Event-Eingangs-Punkte untereinander, **beliebig viele** (keine 3er-Grenze —
     die ist nur ein Artefakt des handgeschriebenen Fluent-Builders, nicht des Modells).
   - **Dann ▶ (mehrere):** pro Dann ein Command-Ausgang mit Marke **×N** (Fan-out/`SendeJe`) + ein
     eigener **↩**-Kompensations-Ausgang. „+ Dann" hängt weitere Commands an **denselben Join**. Beim
     „C# erzeugen" **expandiert** die Regel zu N Regeln (eine je Dann), die sich die Bedingung teilen.
   - **KEIN Code/LLM-Port:** eine Regel trägt keine freie Logik. Der `Sende`-Rumpf ist mechanisches
     Feld-Mapping Event→Command-Parameter → **`default`-Stub** (`argListe` füllt fehlende mit
     `default`), von Hand im Code füllbar. Genau wie Decide/Apply-Stubs.

**Entfernt (2026-09-14): der Count-Join Σ (`UndAlle`/„sammle").** Er ist über ein selbst
modelliertes **Zähl-Aggregat** (zählt Teil-Events, feuert EIN Abschluss-Event) + normalen
Einzel-Trigger ausdrückbar → der Editor bleibt minimal. Der Fan-out `×N` bleibt. Siehe auch das
Muster `Domain/Sammelvorgang` (Erwartet/Fertig/Abgeschlossen als skalares Zähl-Aggregat).

**Scaffolder-Abbildung (unverändert):** ein Regel-Knoten = ein `SagaSchritt`; `prepareSaga()` baut
die `schritte` + `extraUsings`. **Codegen-Folge:** der Fluent-`ProzessBuilder` kann nur bis 3 `Und`;
für >3 müsste der Scaffolder die `Regel` direkt konstruieren (Bedingung-Liste) — separate Aufgabe,
das Editor-Modell ist bereits N-fähig.

---

## 6 · Leseseite: Projektionen, Stores, Reader

> **Status:** Verdrahtung GEBAUT & live verifiziert; Fill/Sim der Read-Seite = spätere Phase.

Das tragende Modell sind **zwei Code-Schichten gegen verdrahtete Verträge**:

- **Handle = Controller.** Der Editor verdrahtet nur **Trigger-Event(s)** + **Store-Scope** (mehrere
  Stores möglich, abgeleitet aus den Aufruf-Kanten). *Welche* Store-Funktionen in *welcher*
  Reihenfolge mit *welchen* Argumenten aufgerufen werden = **C#-Code** im Controller-Rumpf. Kein Join
  (den gibt es nur bei der Saga), keine Arg-Pins.
- **Store = Interface + Impl.** Der Store-Knoten ist ein **Interface-Editor**: Write-/Read-Funktionen
  mit Signatur (Name + Parameter [+ Rückgabe]); je Funktion ein `Impl ◀ code`-Port. ReadModels docken
  oben an (`Store ▶` → `Read Models ▲`).
- **Reader = Controller** mit explizitem **`IReader<TProjektion>`-Bindungsport** (`liest Projektion ▶`),
  Query-Eingängen und **OneOf-Responses** je Query (symmetrisch zu `decider.ergibt[]`).

**Die zwei orthogonalen Entwurfs-Achsen (am Projektion-Knoten):**

| Achse | Häkchen | Bedeutung |
|---|---|---|
| **Transport** | ☑ *Geordneter Pull* (Default) | `IPullSubscriber` — jedes Event genau einmal, in Reihenfolge (Normalfall) |
| | ☐ aus | Signal (`ISubscriber`) — schnell, best-effort, darf verloren/doppelt/ungeordnet sein (UI-Feedback/Ticks) |
| **Garantie** | ☐ *Append-artig* (Default aus) | idempotenter Upsert (last-writer-wins) → **at-least-once genügt** |
| | ☑ an | Append-artig (Ledger/Historie) → verlangt **Co-Commit-Store → exactly-once** (Boot-Check **GA-1**) |

**Faustregel:** Append-Häkchen an, sobald eine Write-Fn *anhängt* (Liste/Historie); bei *Upsert* aus.
Transport und Garantie sind unabhängig — jede Kombination gültig; ein Klartext-Hinweis unter den
Häkchen spiegelt die Semantik. Beide gesetzt (replaybar + emittierend) = Validator-Fehler (spiegelt
den Ctor-Guard).

**Reaktives Event veröffentlichen:** Projektion **und** Reaktion können nach dem Schreiben ein Event
`yield`en (HandlerOutputRouter → Broker-Re-Publish, **verlierbar, kein Log**) — im Editor als
gestrichelt-teal „veröffentlicht ▶". Zwei Event-Sorten: Log-Event (Aggregat, durabel) vs. reaktiv
(Broker, gestrichelt).

**Ehrliche Grenzen:** Ein Store = eine Transaktionsgrenze; zwei echte Grenzen = zwei Projektionen.
Marten-Exotik (computed indexes, Volltext, `.Include`-Joins) bleibt Store-Konfiguration außerhalb des
portablen Modells. Der frühere „Effekt-Vokabular"-Ansatz (Dropdown-Operationen wie Upsert / Feld
setzen / anhängen-dedup / Fan-out / Sekundär-Index) bleibt als **optionaler S-Baukasten** denkbar,
ist aber zugunsten „Code als verdrahteter Wert" (Controller-/Impl-Rümpfe als 📝/🤖) zurückgestellt.

---

## 7 · Betrieb/Host: der Composition Root

> **Status:** Editor-Board + Extractor-Round-trip IMPLEMENTIERT; **kein Codegen** (Folge-Schritt).

Die dritte Ebene neben Topologie und Logik — **wie das System am Boot verdrahtet/konfiguriert wird**
(Trigger-Quellen, Fristen, Laufzeit-Config, Dienst-Bindung). Sie ist fast reine Verdrahtung +
Blattwerte, also im Editor-Idiom ausdrückbar — **vier Primitive**, jedes mit 1:1-Entsprechung zum
realen `Host.Grpc/Program.cs`:

1. **Trigger-Quelle** → Pipeline (`erzeugt ⟨TriggerMsg⟩ ▶`). Modi timer/webhook/filewatch. Das
   `baueTrigger`-Lambda ist fast immer Identität → keine Code-Insel.
2. **Frist = Drei-End-Relation** (statt Ingress-Attrappe): `plant ◀ Event` · `storniert ◀ Event(s)` ·
   `Dauer ◀ HostSetting` · `fällig → Command @ Aggregat`. Der `Kontext`-String ist die stabile
   Identität (`AddDeadlines`-Router; fehlende „fällig →"-Kante = Boot-Fail-fast). `IDbClock`/
   `IFristplan`/`FristId` bleiben Framework-Interna (Invariante 5).
3. **Dienst-Bindung** (Vertrag→Impl): schließt zwei Löcher — Handler-Dependencies (`IClassifierService`
   …) **und** den freistehenden Domain-Service (`SplitZuteiler`, `ImagePairName`), ohne VO-Behavior/
   Specification/Entity einzuführen. Impl = 📝-Insel oder „externer Dienst"-Marker (HTTP/ML).
4. **HostSetting** `{Name,Typ,Default,EnvKey}` — operativer, **nicht** fachlicher Wert (Pfad/Intervall/
   Timeout); speist Trigger und Frist-Dauern.

**Round-trip (IMPLEMENTIERT):** `GraphExtractor/CompositionRoot.cs` (`CompositionRootExtractor`),
verdrahtet in `Program.cs` + `ModellMapper.ZuBoardJson`. Liest best-effort `Host.Grpc/Program.cs`
(`AddDeadlines`, `MapPipelineWebhook<T>`, `GetValue("Pipeline:*")`) und `DomainPipelineExtensions.cs`
(`AddSingleton<I,Impl>`, `new …Config(TimeSpan…)`). Gemessen an der realen Domäne: 1 Frist, 1 Webhook,
3 Dienst-Bindungen, 4 HostSettings.

**Bewusste Grenze:** nur die **domänen-gerichtete** Naht (Trigger→Pipeline, Frist→Command,
Dienst-Bindung, domänen-relevante Settings). Reine Infra/Deploy (gRPC-Port, Consul/Redis/Marten,
Cluster-Rollen, Monitoring) bleibt in `appsettings`/`docker-compose`. HostSetting für `WatchPath` — ja;
für den Redis-Endpunkt — nein.

---

## 8 · Feld- & Typ-Ports

Zwei verwandte Mechaniken, beide UI-seitig (keine neue Feld-Struktur; Quelle ist `(Owner, Feld)` mit
stabiler `_id` → rename-fest):

- **Typ-Port `ftype` — VOLL GEBAUT.** VO/Enum → kleiner `◀`-Typ-Eingang am Feld setzt den Feldtyp (§4).
- **Wert-Port `field ▶` — Fundament OHNE aktiven Konsumenten.** Jedes Objekt-Feld (Record/State/
  ReadModel) hat einen kleinen `field ▶`-Ausgang (`.sm`, dezent). Die Typprüfung passiert am
  **Eingang** (`compatible` kennt `want:"count"|"collection"`), nicht durch Weglassen des Ausgangs.
  **Der einzige vorgesehene Eingang (×N-Fan-out `sendeJeCollectionFeld`, `want:"collection"`) ist noch
  nicht als Slot gerendert** → `field ▶` ist derzeit nicht anschließbar. Der ursprünglich erste
  Konsument (Σ-Count-Join) wurde entfernt (§5); die Feld-Ports bleiben als Fundament.

**Rollen-Auflösung** (falls der ×N-Konsument kommt): ein Feld-Port trägt nur `(rec, field)`; die
Lambda-Rolle fällt aus der Verdrahtung ab (`i = t.wenn.indexOf(rec)` → `["t","r","g"][i]`), nicht aus
einer `wenn[0]`-Konvention.

---

## 9 · Bedienung — Kurzanleitung

**Verbinden allgemein:** von einem **Ausgang** (rechts, farbiger Punkt) auf einen **Eingang** (links)
ziehen. Gültige Ziele ringeln beim Ziehen **grün**; nur typgleiche Ports rasten ein. `+ …` in der
Palette (oder Doppelklick auf die Fläche) legt Knoten an.

**Frist — zwei Wege:**
- **Weg 1 (externer Wecker):** `+ Trigger` → Modus „⏳ Frist" → Dauer + Trigger-Nachricht (`+ Feld` für
  die Nutzlast) → `erzeugt … ▶` auf „+ Trigger andocken" einer Pipeline; dort `+ Command ▶` auf den
  Ziel-Command. Entspricht `IPipelineTrigger`.
- **Weg 2 (interner Timeout, idiomatisch — so macht es `TrainingFristPipeline`):** in einer Pipeline an
  einem Handle „+ plant Self-Tick ↺" → ＋ klicken. Legt `Tick · 30s · ↺` **und** automatisch einen
  „◀ Self Tick"-Handle an (gestrichelter Self-Loop). Delay = Frist, Namen sprechend machen, am
  Self-Handle den Timeout-Command verdrahten. Der Rumpf prüft „noch offen?" und feuert nur dann.
  Entspricht `ctx.ScheduleSelf` → `IPipelineSelfMessage`.

**Toolbar:** ↻ Vom Graph laden · ▦ Neu anordnen · 🗂 Domänen · ✓ Prüfen · ⚙ Kompilieren · ▶ Testen ·
`</>` C# erzeugen · 💾 Speichern · ⬇ Modell.

---

## 10 · Backend-Naht, Persistenz & Synchronität (SimHost)

**Grundsatz: der C#-Code ist die einzige Wahrheit.** Das Board ist eine Projektion des Codes plus Layout,
Entwürfe und (markierte) noch ungeschriebene Änderungen. Das ist geprüft, nicht gehofft (§10.3).

### 10.1 Endpunkte (`SimHost/Program.cs`)

| Endpunkt | Wirkung | UI |
|---|---|---|
| `GET /api/editor/model` | `domain-model.json` (aus C# extrahiert) | Boot, ↻ |
| `POST /api/editor/extract` | GraphExtractor über den **aktuellen** Code laufen lassen → `domain-model.json` neu | ↻ Vom Graph laden, nach `</> C# schreiben` |
| `GET/POST /api/editor/board` | `board-model.json` (Layout + Entwürfe) laden/speichern | Boot, 💾 |
| `POST /api/editor/write` | `DateiSchreiber`: Scaffolder-Ausgabe **additiv** in die echten `.cs` (neue Typen/Methoden; Handcode nie überschrieben) | `</> C# schreiben` |
| `POST /api/editor/validate` | Struktur-Diagnosen | ✓ Prüfen |
| `POST /api/editor/compile` | Schreibseite **in-memory** mit den echten Domain-Generatoren übersetzen (nur Aggregat-/Saga-Namespaces + transitiv referenzierte Typen) | ⚙ Kompilieren |
| `POST /api/editor/sim/step` | Command mit Werten in die Session → Kaskade (inkl. Sagas), Instanzen, Saga-Markings, Abdeckung; Hot-Reload | ▶ Simulation |
| `POST /api/editor/sim/reset`, `GET /api/editor/sim/state`, `GET /api/editor/sim/dsl` | Session verwerfen / Stand / als Test-DSL | ↺ · 📋 Als Test |
| `GET/POST /api/editor/code`, `POST /api/editor/open` | Rumpf-Spiegel Datei → Board, Prompt-Zeile Board → Datei, im IDE öffnen | 📝/🤖 |
| `POST /api/editor/build`, `/scaffold` | `dotnet build` bzw. Scaffolder-Vorschau | (derzeit kein UI) |

Prüfen/Kompilieren/Testen melden in ein **schwebendes Ausgabe-Panel** (erscheint automatisch, ✕ schließt).

### 10.5 Nichts hart kodiert — was woher kommt

Grundsatz (2026-09-24): **Jede Erkenntnis über die Domäne kommt aus einem Code-Fakt** — Marker-Interface, Attribut,
Symbol, Signatur, aufgelöster Aufruf, Datenfluss. Keine Namenskonvention, kein Namespace-Raten, keine Mehrheits-
Statistik, kein Default, der sich als Erkenntnis ausgibt. Wo der Code etwas nicht eindeutig sagt, bleibt es leer bzw.
wird als mehrdeutig gemeldet — nie geschätzt.

| Frage | Quelle (Code-Fakt) |
|---|---|
| Framework-Namen (Marker, DSL-Verben, `Frist.Kontext`, `IFristplan.PlaneAsync` …) | `GraphExtractor/Vertrag.cs` über `typeof`/`nameof` aus `Abstractions` (Umbenennung = Compile-Fehler); Scaffolder ebenso |
| Welche Projekte? | `Projektlage.cs`: Vertrag = Assembly von `IState`; **Laufzeit** = Projekt mit `[RoutingTabelle]`-Generat; Analyse = deren Referenz-Hülle; **Domäne** = Projekte, die im handgeschriebenen Quelltext einen Typ mit Domänen-Rolle deklarieren (Command, Event, State, Decider, Store, Konsument, Pipeline, Prozess, Wertobjekt …); **Hosts** = Programme mit handgeschriebenem Einstiegspunkt |
| Generiert oder handgeschrieben? | handgeschrieben = Projekt-**Dokument**; Generator-Ausgaben sind nie Dokumente (plus `<auto-generated`-Kopf für eingecheckte Prepass-Dateien) |
| Aggregat | `IState` + wer `IDecider<State>`/`IApplier<State>` implementiert — **egal wo** (geschachtelt oder nicht, beliebige Datei/Typform) |
| Decide/Apply/Handle | Parametertypen (`ICommand`, `IEvent`, `IAggregateEnvelope`, `PipelineContext`, `IQuery`) an **handgeschriebenen** Methoden — Methodennamen egal |
| Felder | Positions-Parameter eines Records **und** Auto-Properties (`{ get; init; }` …, `required`); Form (record/class/struct, Parameterliste ja/nein, weitere Basen, Attribute) wird mitgeführt und so zurückgeschrieben; Sammlungen per Symbol (`IEnumerable<T>` → `elementTyp`) |
| Guards | alle umschließenden `if` bis zum Rumpf, verzweigungstreu (`else` ⇒ `!(…)`), Event-Typ aus dem Symbol |
| Saga-DSL | Verben am **Methoden-Symbol** (Vertrags-Assembly + DSL-Namespace); `Regeln` über die Interface-Implementierung (auch explizit); Ketten auch über lokale Variablen |
| Stores | Marker **`IWriteStore`** / **`IReadStore<TWrite>`** (Paarung als Typ, Namen frei) — auch der Framework-Generator registriert danach; Store-Aufrufe über das aufgelöste Methoden-Symbol (auch über die konkrete Klasse); ReadModel → Store nur wenn eindeutig (sonst `storeKandidaten`) |
| Subscriber-/Pipeline-Id | Compile-Zeit-Konstante der Vertrags-Property (`const`, `nameof`, Verkettung); nicht konstant ⇒ leer |
| Emits (Pipeline/Reaktion) | nur tatsächlich **ausgegebene** Werte (`yield return`/`return`), nicht jedes konstruierte Objekt |
| Aggregat-Zugehörigkeit eines Records | Command → Aggregat seines Deciders; Event → Aggregat, das ihn erzeugt/faltet; VO/Enum → Aggregat, dessen Records/State ihn referenzieren — jeweils **eindeutig oder keinem** (im Editor: Decider/Applier per **▲ Aggregat**-Port verdrahtet) |
| Routing, registrierte Prozesse, DI-registrierte Store-Impl | Generat: `[RoutingTabelle(Art)]`-Properties, `["Name"] = new P()`, erzeugte Typen |
| Trigger-Ingress | Methoden mit **`[Ingress(Art, Ort = nameof(param))]`** (Webhook-Route, Timer-Intervall, Datei-Pfad) — am Symbol der aufgerufenen Methode, nicht an der Aufrufform |
| Dienste | DI-Registrierung (`Add*`/`TryAdd*`, auch Fabrik/Instanz) mit Domänen-Vertrag |
| HostSettings | Argumente DI-registrierter Konfig-Records → Herkunft über lokale Variablen, `??`, Parameter → Aufrufer im Host → `GetValue`, `IConfiguration`-Indexer, `Environment.GetEnvironmentVariable` |
| Frist | Lambda über `Frist` im Host — Zweige über `f.Kontext` als Ternär, `if`, `switch`-Ausdruck oder -Anweisung; plant/storniert über erreichte `IFristplan`-Aufrufe |
| Aggregat-Klassen-/Methodennamen, `AggregateId`, OneOf-/Join-Stelligkeit, Wire-Skalare | `Abstractions.Aggregatvertrag` (auch vom Generator gelesen), `nameof(ICommand.AggregateId)`, per Compilation gezählte `OneOf`/`RegelBauer`-Varianten, `ProtoScalarSpecs` (eine Quelle mit dem Proto-Codegen) |
| Wohin schreiben? | echte Dateipfade im Modell; Verzeichnis je Namespace nur wenn **alle** seine Typen in genau einem Verzeichnis liegen, sonst über den längsten bekannten Namespace/Projekt-Wurzel (MSBuild-Abbildung). Neuer Typ: eindeutige Datei gleicher Art im Namespace, sonst die kanonische Scaffolder-Datei (`Commands.cs` … bzw. `{Agg}.cs`, `{Agg}.Decider.cs`, `{Agg}.Applier.cs`). Ohne bekanntes Verzeichnis: **nicht platzierbar**, wird nicht geschrieben |
| Browser-Zwischenstand | `localStorage` je Solution (`rahmen.kennung`) — Repos vermischen sich nie |

**Beweis statt Behauptung — die Agnostik-Sonde** (`dotnet run --project GraphExtractor -- --sonde`): legt eine dem
Extractor unbekannte Domäne (`GraphExtractor/Sonde/*.cs.txt`, fremder Wurzel-Namespace, jede konventionsbrechende
gültige Schreibweise) im Speicher in die Solution, fährt die echte Pipeline und vergleicht mit einem **handgeschriebenen**
Soll (`Sonde/soll.txt`) — plus volle Parität (Inventar + Fixpunkt) auf dem Fork. `--sonde-ist` zeigt das Ist,
`--sonde-board <datei>` schreibt das Board-Modell inkl. Sonde (zum Ansehen im Editor).

### 10.4 Simulation (die EINE Laufzeit)

`SimHost/ModellSimulation.cs` ersetzt die alte, fest an `Domain.dll` gebundene `SimEngine` **und** das
frühere „▶ Testen": Modell → Scaffolder → In-Memory-Kompilat mit den **echten** Domain-Generatoren →
`SagaLaufwerk` (Cqrs.Testing, derselbe Kern wie die Test-DSL). SimHost referenziert die Domain bewusst
**nicht** mehr — simuliert wird immer das Modell (inkl. ungeschriebener Entwürfe); dank Fixpunkt (§10.3) ist
das verhaltensgleich zum echten Code.

Im Editor: **▶ Simulation** öffnet das Seitenpanel (unter 1100 px als Schublade).
- **Command senden:** nach Ziel-Aggregat gruppiert; Felder typgerecht (Guid = bestehende Instanz wählen
  oder neue Id, Enums als Auswahl, Zahlen, bool, JSON für VOs/Collections).
- **Animation auf den echten Knoten:** (Saga + gefeuerte Regel →) Command → Decider/Aggregat →
  Events/Ablehnungen (rot) → Applier/State; Kamera folgt (abschaltbar), Tempo wählbar. Verlauf-Einträge
  sind klickbar (erneut abspielen); je Event der gebundene Guard („weil fertig >= 1").
- **Instanzen** (Label „Sammelvorgang #1", geänderte Felder gelb), **laufende Sagas** (was angekommen ist,
  worauf welche Regel noch wartet).
- **Abdeckung:** Decider „x/y Zweige" (grün voll, gelb teilweise, blass nie gefahren), Applier, Regeln.
- **Hot-Reload:** Modell ändern → nächster Schritt übersetzt neu und spielt die bisherigen Wurzel-Commands
  gegen die neue Logik nach (Hinweis im Panel); Compile-Fehler erscheinen im Panel, der letzte gute Stand bleibt.
- **📋 Als Test:** die Session als `Szenario.Für<…>().Vorab(…).Wenn(…).Dann…`-Regressionstest.

Grenze: Schreibseite + Sagas; Projektionen/Reader/Pipelines werden (noch) nicht simuliert.

### 10.2 Merge statt Kaskade (`deBoot` / `deReload` → `mergeBoard`)

Jedes aus dem Code stammende Element bekommt beim Laden `ausCode`, `_codeKey` (Identität im Code, z. B.
`aggregat|command`) und `_herkunft` (Hash des Inhalts). Beim nächsten Laden gilt je Element:

| Lage | Ergebnis |
|---|---|
| im Code, im Board unverändert | **Code-Stand gewinnt**, Layout (x/y) bleibt |
| im Code, im Board geändert (Hash ≠ `_herkunft`) | Board-Stand bleibt, Knoten **„✎ ungeschrieben"** (gelb gestrichelt) |
| nur im Board, ohne `ausCode` | **„Entwurf"** (blau gestrichelt) — bleibt |
| `ausCode`, aber nicht mehr im Code | entfällt (im Code gelöscht) |

Rumpf-Quelle ist immer die echte Datei (Code-Knoten des Code-Stands). `</> C# schreiben` liest danach
automatisch neu ein — Geschriebenes wird Code-Stand; was der additive Schreiber nicht umsetzen konnte
(z. B. neues Feld an einem bestehenden Record), bleibt sichtbar „ungeschrieben". Umbenennen zieht auch
gelesene Saga-Lambdas nach; ein Command-Wechsel am Dann verwirft den (dann ungültigen) Lambda.

### 10.3 Paritäts-Prüfung (`dotnet run --project GraphExtractor -- --check`)

Schreibt nichts; Exit-Code ≠ 0 bei Abweichung — das Gate vor jedem Editor-/Extractor-Umbau.
- **Inventar:** unabhängige, syntaktische Zählung im Code (Commands, Events, Ablehnungen, VOs, Enums,
  Aggregate + Decide/Apply-Methoden, Sagas + Regeln, Queries, Responses, ReadModels, Projektionen/
  Reaktionen, Reader, Pipelines) gegen das Board-Modell. Fängt blinde Flecken des Extractors.
- **Fixpunkt:** M₁ = extract(Code); die abgedeckten Typen im Domain-Projekt werden im Speicher durch
  `Scaffolder.Generiere(M₁)` ersetzt, alles neu kompiliert (echte Generatoren), M₂ = extract(Fork).
  Gefordert: **0 Compile-Fehler und M₁ == M₂**.
- Stand 2026-09-24: 116 Typen → 39 Dateien, 0 Fehler, Inventar deckungsgleich.

Was der Round-trip dafür verlustfrei trägt: Record-Felder inkl. Defaults (`= null`), Handcode-Rümpfe
(`Zusatz`, `StateZusatz`, `DeciderZusatz`, `ApplierZusatz`), State-Initialwerte + `{ get; }`,
Deklarations-Reihenfolge, Parameternamen von Decide/Apply, **bewusst leere** Rümpfe (`""` ≠ Platzhalter),
echte Apply-Methoden (nicht aus Commands abgeleitet), OneOf-Reihenfolge, Summary-Doku, Saga-Lambdas
verbatim (`sendeAusdruck`, auch Expression-Body-`Definiere`), usings (explizit + aus Typ-Referenzen
abgeleitet). **Bewusst nicht:** Kommentare zwischen State-Properties und Doku jenseits `<summary>`.

**Inseln (Stand 2026-09-24, nach Extractor-Fix):** Die Insel-Erkennung zählt nur noch Fachknoten (Code-/LLM-Knoten
blähten Komponenten auf und versteckten unverbundene Stores/Pipelines). Der Extractor trennt **Konfigurations-Records**
(per DI in Pipeline/Subscriber/Reader/Store-Impl injiziert, Art `konfig`) von Value Objects und verfolgt **HostSettings**
semantisch: `GetValue("Pipeline:…")` in Program.cs → Parameter der DI-Extension → Feld des Konfig-Records → Pipeline
(Kanten HostSetting → Konfig → Pipeline). ReadModels werden auch über die Store-**Implementierung** zugeordnet
(`LoadAsync<T>`), Store-Transfer-Typen hängen am Store, Framework-Stores (DeadLetter) sind raus. Verbleibende Inseln
sind echt: `ImagePairEingabeUngueltig` (von keinem Decider erzeugt → Diagnose `UNUSED-EVENT`) und die bewusst wirkungslose
`BenchmarkPipeline`. Diagnose-Hook: `window.deGraph()` liefert Knoten + Kanten des Boards.

Zusätzliche Graph-Diagnosen: `UNUSED-EVENT` (Domänen-Event/Ablehnung in keiner Decide-Signatur), `MISSING-APPLY` (Event produziert, nicht gefaltet → Laufzeit-Crash),
`ENUM-ZERO` (Wert 0 geht auf dem Proto-Wire verloren), `STORE-AMBIGUOUS` (mehrere Impls je Store-Interface).

---

## 11 · Offene Punkte / Nicht-Ziele

- **Codegen der Composition-Root-Primitive** (Program.cs-Fragmente/DI-Extensions aus Trigger/Frist/
  Dienst/HostSetting) — Folge-Schritt, bewusst offen.
- **×N-Feld-Konsument** (`sendeJeCollectionFeld`-Eingangs-Slot) — der einzige noch fehlende Feld-Port.
- **Leseseiten-Sim** (Dict-`ICoCommitSession` + Event→Projektion + Reader-Run + Dokument-Inspektor) und
  **LLM-Fill** der H-Rümpfe — spätere Phasen; heute nur Verdrahtung.
- **>3 `Und`** im Scaffolder (Fluent-Builder-Grenze, s. §5).
- **VO-Behavior / Specification / Entity** — aus der Domänen-Analyse nicht gebraucht, kein Ziel.
- Scaffolder/`ModellMapper`/Proto werden von den reinen Editor-Erweiterungen **nicht** berührt.
