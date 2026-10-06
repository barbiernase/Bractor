# Konzept & Anleitung — Der Domänen-Editor

> **Stand:** 2026-10-06 (§12 Akteure, Verträge, Clients) · **Status:** GEBAUT, teils ohne Codegen/Sim (je Abschnitt vermerkt).
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

> **Seit 2026-09-29 (Hybrid):** Die Fläche ist Landkarte (Karten immer kompakt, keine Ports, kein Freihand-Ziehen);
> bearbeitet und verbunden wird im Panel über den Verbinden-Modus — siehe
> [konzept-editor-panel-bearbeitung.md](konzept-editor-panel-bearbeitung.md). Die Bedienhinweise unten zu „Slot ziehen“
> und Aufklappen sind damit überholt; Port-Typen, Kanten und Modell gelten unverändert.

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
**Anschlussseiten dynamisch** (`drawEdges`): Auf der Fläche sind Karten eingeklappt; eine Kante dockt je Ende an der
Seite an, die dem Partner **zugewandt** ist (oben/unten/links/rechts — horizontaler vs. vertikaler Abstand der Karten),
nicht fest „Ausgang rechts, Eingang links". Mehrere Linien auf derselben Seite werden entlang der Seite verteilt (nach
Lage des Partners sortiert), die Kurve tritt senkrecht zur Seite aus. Sichtbare Slots (Panel) behalten ihren Punkt.

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
| **Garantie** | ☐ *Append-artig* (Default aus) | idempotenter Upsert → **at-least-once genügt** |
| | ☑ an | Append-artig (Ledger/Historie) → verlangt **Co-Commit-Store → exactly-once** (Boot-Check **GA-1**) |

**Faustregel:** Append-Häkchen an, sobald eine Write-Fn *anhängt* (Liste/Historie); bei *Upsert* aus.

**Am ReadModel: ☐ *geteilt*** (Marker `IGeteiltesReadModel`, Code-Fakt). Die Maschine garantiert einen Schreiber nur je
(Projektion, Stream). Ein Dokument, das mehrere Streams beschreiben (Singleton-Zeiger, Zähler über viele Aggregate), ist
*geteilt*: generiert mit Versionsprüfung, im Store nur über `EnqueueGeteilt` änderbar (ein Konflikt wiederholt den Stapel);
„neuer gewinnt“ dort nach Event-Zeit, nie nach Ankunft. Ohne Häkchen gehört das Dokument dem Stream seiner Id — eine Menge
über viele Streams besser als **Zeilen je Stream** (Id enthält den Besitzer) ablegen statt geteilt. Umschalten schreibt nur
diesen Marker in die Basisliste; die Karte zeigt „⇄ geteilt“. Schreibt der Store ein abgewähltes Dokument noch geteilt,
meldet es der Bau (`EnqueueGeteilt` verlangt den Marker). Regeln des Store-Rumpfs: `docs/10-entwickler-api.md` §10.3
(CQRS066/067); die 🤖-Konsole prüft einen Store-Rumpf damit **vor** dem Schreiben gegen das echte Projekt.
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

### 9.1 Domänen-Rahmen (visuelle Blöcke, hierarchisch)

Das Layout bleibt das bewährte: je Aggregat ein Block mit hochkant stehenden Rollen-Spalten, Code-Blöcke (📝) und
🤖-LLM-Plätze direkt unter ihrem Besitzer, Karten frei ziehbar. Darum liegen nur **rein visuelle Rahmen**:
- **Domänen-Zugehörigkeit aus dem Graphen steht über allem** (`domKey`): gehört ein Baustein laut Graph zu einem
  Aggregat (`groupKeyOf`: Projektion über ihre Events, Reader/Store/ReadModel/Query/Response über die Projektion, Code
  über den Besitzer), ist seine Domäne der Namespace dieses Aggregats — auch wenn die Klasse in `Domain.Projections`
  liegt. **Ablauf-Einheiten** (Pipeline/Reaktion + Handles + Trigger + Code/🤖 + Dienst, Prozess + Regeln) werden als
  Ganzes zugeordnet: die Domäne ihrer Aggregat-Nachbarn, wenn eindeutig — sonst die, an die sie **Commands schickt**
  (Lesen woanders ist nur Abhängigkeit; z. B. DatensatzResolverPipeline liest ImagePair, schreibt Datensatz → Datensatz).
  **Datentypen** ohne Aggregat (Feldtyp-VO/Enum/Konfig/Response) folgen schrittweise ihren Nachbarn. Erst danach zählt
  der eigene Namespace (z. B. BenchmarkPipeline ohne Domänen-Bezug, Inseln).
- **Domäne = Namespace dieser Zuordnung**, hierarchisch: Unterdomänen (`Domain.Pipeline.Trainingslauf`) liegen gestrichelt im Rahmen
  ihrer Eltern-Domäne. `packLayout` ordnet auf oberster Ebene nach diesem Baum (alphabetisch, „ohne Domäne“ zuletzt;
  je Domäne: Aggregat-/Brücken-Blöcke, dann ihre Inseln, dann die Unterdomänen) und reserviert je Region Rand + Kopf,
  sodass sich Rahmen nie schneiden.
- Der Rahmen folgt den **echten** Kartenpositionen (`zeichneRahmen` aus `GEO`, nach jeder Messung) — zieht man eine
  Karte heraus, wächst er mit. Linie und Kopf wachsen beim Rauszoomen mit (lesbar in der Übersicht).
- **Menüleiste im Kopf:** `＋` Baustein in dieser Domäne (Namespace gesetzt; Decider/Applier/State ans Aggregat der
  Domäne, Regel an ihren Prozess) · `＋ ▤` Unterdomäne · `⤢` einpassen (auch Doppelklick) · `⋯` (Slice markieren,
  leere Domäne entfernen). Ansicht-Leiste `＋ Domäne`, Rechtsklick auf die Fläche ebenso.
- **Spalten-Rahmen:** innerhalb einer Domäne ist jede senkrechte Rollen-Spalte eines Blocks (Aggregat bzw. Brücke) eigens
  eingerahmt — Command | Decider | Aggregat · State | Event · Ablehnung | Applier | VO · Enum | Projektion |
  Projektion-Handle | Store-Fn | Read Model · Store | Query | Reader-Handle | Response | Reader (bzw. Prozess | Regel |
  Reaktion | Pipeline | Trigger … in der Brücke). 📝/🤖 zählen zur Spalte ihres Besitzers; Inseln haben einen eigenen
  Kasten. `layoutBlock` reserviert über jeder Spalte den Kopf (`SPK`). Kopf: Name · Anzahl · `＋` (legt eine Art dieser
  Spalte in Domäne + Aggregat des Blocks an; Handles/Store-Fns entstehen weiter am Besitzer) · `◎` (Spalte als Slice
  markieren) · Doppelklick = einpassen. Ein über `＋` angelegter, noch unverdrahteter Baustein behält bis zur
  Verdrahtung seine Spalte (`VIEW.heimBlk`) statt im Inselkasten zu landen.
- **Nur Sicht:** leere neue Domänen (`VIEW.domNeu`) und die Heimat namespace-loser Bausteine (`VIEW.heim`) leben in der
  Ansicht; im Code entsteht ein Namespace erst mit seinem ersten Typ. Code-Namespace geht vor (`domKey`).

### 9.2 Render-Architektur der Fläche (Zoom/Pan/Minimap)

Gemessen am vollen Bestand: ~550 Karten, ~800 Kanten-Pfade, ~24.500 DOM-Elemente, Inhalt bis ~7.000 × 11.300 px.
Regeln (alle in `HtmlPresenter.cs`, `applyPan`/`sichtFrame`/`messeWelt`/`kulle`):
- **Keine Dauer-GPU-Ebene.** `.gworld` trägt `will-change:transform` nur während einer Zieh-Geste (`.bewegt`:
  Pan, Minimap-Ziehen — reines Verschieben, Raster bleibt gültig). Dauerhaft gesetzt rasterte der Browser die
  ganze Welt in der Start-Zoomstufe; beim Rauszoomen sprengte das das Kachel-Budget → Karten luden nicht
  nach, flackerten, verschwanden — auch Inspector und Minimap, die sich das GPU-Budget teilen.
- **Transform sofort, Abgeleitetes einmal je Frame.** `applyPan` schreibt nur den Transform; Minimap-Rahmen,
  Culling und Schatten-LOD laufen gebündelt in `sichtFrame` (rAF) — ohne Layout-Lesen (Canvas-Größe aus
  `ResizeObserver` → `CV`, Knoten-Geometrie aus `GEO`).
- **Geometrie-Cache `GEO`** wird je Layout-Änderung (`drawEdges`) einmal gemessen; Welt- und Kanten-Ebene
  wachsen auf die Inhaltsgröße (statt fest 6000 × 4000, über die der Bestand hinausragte).
- **Culling:** Karten außerhalb Sichtfenster + ½ Fenster Rand bekommen `.weg` (`visibility:hidden` — Maße
  bleiben, Anker/Kanten/Einpassen stimmen weiter).
- **Schatten-LOD:** unter Zoom 0,45 entfallen die weichen Karten-Schatten (`.fern` → `--gsch:none`);
  Hervorhebungs-Schatten (Sim, Typ, Verbinden) bleiben.
- **Overlays gekapselt:** Inspector, Minimap, Filter sind eigene, `contain`-gekapselte Ebenen.

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

## 11 · Offene Punkte / Nicht-Ziele

- **Codegen der Composition-Root-Primitive** (Program.cs-Fragmente/DI-Extensions aus Trigger/Frist/
  Dienst/HostSetting) — Folge-Schritt, bewusst offen.
- **×N-Feld-Konsument** (`sendeJeCollectionFeld`-Eingangs-Slot) — der einzige noch fehlende Feld-Port.
- **Leseseiten-Sim** (Dict-`ICoCommitSession` + Event→Projektion + Reader-Run + Dokument-Inspektor) und
  **LLM-Fill** der H-Rümpfe — spätere Phasen; heute nur Verdrahtung.
- **>3 `Und`** im Scaffolder (Fluent-Builder-Grenze, s. §5).
- **VO-Behavior / Specification / Entity** — aus der Domänen-Analyse nicht gebraucht, kein Ziel.
- Scaffolder/`ModellMapper`/Proto werden von den reinen Editor-Erweiterungen **nicht** berührt.

## 12 · Akteure, Verträge, Clients im Editor

> Begriffe und Backend: `docs/konzept-akteure.md` (Akteur · Zusage/Kenntnis · Akteur-Vertrag · Client). Hier nur, wie der Editor sie zeigt
> und bearbeitet. Vorgabe (Tobi): die bestehende Darstellung bleibt; es kommt eine Gruppierungs-Ebene dazu, nichts wird ersetzt.

### 12.1 Bild

```
┌ ImagePair ─────────────────────────────────── 👤 KameraSystem › Klassifizierer › Inspekteur › … ┐      ┌ 🔌 KlassifikationsWorker ┐
│ ┌ ImagePair · ⚙ KameraSystem ─────────────────────────────────────────── ＋ ◎ ⤢ ⋯ ┐          │      ┊ verkörpert 🤖 Klassifizierer ┊
│ │ Trigger │ Pipeline │ Command │ Decider │ State │ Event │ Applier │ …            │          │      ┊ ┌ Client · Klassifikations… ┐┊
│ └─────────────────────────────────────────────────────────────────────────────────┘          │ ━━━━━┿━┥ ↺ trägt IKlassifizierer   │┊
│ ┌ ImagePair · 🤖 Klassifizierer ─ 🔌 KlassifikationsWorker ─────────────────────── ┐          │      ┊ │  ◀ ImagePairKomplett → …   │┊
│ │ Akteur │ 📜 Vertrag IKlassifizierer (Zusagen) │ Command │ Decider │ Event │ …   │          │      ┊ │ ▶ sendet als Klassifizierer │┊
│ └─────────────────────────────────────────────────────────────────────────────────┘          │      ┊ │ ? fragt als Klassifizierer  │┊
│ ┌ ImagePair · 👤 Inspekteur ─ 🔌 Arbeitsplatz ──────────────────────────────────── ┐          │      ┊ └────────────────────────────┘┊
│ │ Command … │ Query … │ Reader │ Projektion │ Read Model │ Response                 │          │      └┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┄┘
│ └─────────────────────────────────────────────────────────────────────────────────┘          │
└───────────────────────────────────────────────────────────────────────────────────────────────┘
```

Domäne (dünner Außenrahmen) → **Domäne × Akteur** (je ein Rahmen) → die bekannten Rollen-Spalten. Clients stehen als eigene, gestrichelte
Rahmen **rechts neben allen Domänen**.

### 12.2 Welcher Baustein steht in welchem Akteur-Rahmen?

Ganz aus dem Graphen, ohne neues Wort (`DomainEditor/AkteurAnteile.cs`, JS-Spiegel `akteurSet`/`akteurVon`):

| Seite | gehört zu A, wenn … |
|---|---|
| **Eingang** (Command/Query/Trigger) | A ∈ Akteur-Menge (direkt `IDarf` oder über die Kette, Akteur-Konzept §2.3) |
| **Schreibseite** | Decide eines Commands von A; die Events/Ablehnungen aus seiner Signatur; deren Applier; State, VO, Enum, die diese berühren |
| **Leseseite** | Queries von A → Reader-Handle → Reader → Projektion (+ Handles, Store-Fns, Read Model, Response) |
| **Ablauf** | Arme, deren Ausgaben zu A gehören (Pipeline/Prozess/Reaktion/Frist inkl. Handles, Trigger, Code 📝/🤖, Dienst) |

VO/Enum/Konfig/HostSetting/Dienst/Code/Response gehen mit ihrem Träger mit, öffnen aber keinen eigenen Akteur-Rahmen.

### 12.3 Reihenfolge der Akteur-Rahmen und Doppelungen

- Ein Baustein, der mehreren Akteuren gehört, steht **genau einmal** — im ersten Akteur-Rahmen seiner Domäne. Die Reihenfolge ist
  abgeleitet: (1) wer das Aggregat erschafft (Akteur des `ICreationCommand`), (2) wer worauf reagiert (B nach A, wenn B ein Event aus A's
  Anteil hört), (3) Art (Maschine → KI → Mensch), dann Name; „⚠ ohne Akteur" (rot, GR-HERKUNFT) zuletzt. Eine Domäne ohne Akteure sieht
  aus wie vorher (ein Rahmen).
- Spätere Rahmen zeigen im Kopf **„↥ auch"** mit Verweisen auf die Doppelungen (Klick = hinspringen); ein Rahmen, dessen Anteil schon
  ganz weiter oben steht, erscheint nur als Kopf. Kanten laufen sichtbar über Rahmen hinweg. Die Kartenzahl bleibt gleich (Parität).
- Im Kopf **⋯ → ↑/↓/↺** sortiert der Entwerfer um (`VIEW.akteurOrdnung[dom]`); geteilte Bausteine wandern mit.

### 12.4 Akteur: Kopf, Karte, Panel

- **Kopf:** `Domäne · Art-Symbol Akteur` (Klick = Akteur-Panel) · „↥ auch" · **🔌 Stecker-Zeile** (welche Clients hier stecken, §12.6) ·
  Zahl · ＋ (Command/Query/Trigger für diesen Akteur, `IDarf` sofort) · ◎ · ⤢ · ⋯. Gestrichelt = der Akteur wirkt hier nur über die Kette.
- **Akteur-Karte** in der ersten Domäne, in die er selbst hineingibt (Spalte „Akteur").
- **Panel:** Art, **darf ⊕/✕**, „bewirkt über die Kette" (abgeleitet), „wirkt in [Domänen]", Dienst + „entscheidet in", **Zusagen ◀**
  (je `Auf` eine Zeile, „+ Zusage ⊕ (Event)"), „🔌 getragen von" (Clients, ⚠ GR-GETRAGEN, Port für „trägt ⊕"), „hört" (abgeleitet).
- Zuordnung per Ziehen gibt es nicht: wem eine Karte gehört, folgt aus `IDarf`, Kette und Vertrag. Löschen eines Eingangs entfernt ihn aus
  allen `IDarf`-Listen, Verträgen und Client-Rändern.

### 12.5 Akteur-Vertrag: 📜-Rahmen und Zusage-Karten

- Der Vertrag steht **geschlossen** neben der Akteur-Karte als Rahmen „📜 Vertrag IX" (Kopf: Name · ◀ Eingänge · Ausgänge ▶ · ＋ Zusage · ◎
  · ⤢), darin je `Auf(Event)` eine Karte **Zusage** (◀ Event · Ausgänge ▶; ohne Ausgang = Kenntnis). Die Kanten führen zu den Events und
  Commands hinüber; die Kette läuft so sichtbar durch den Akteur draußen (`flussRegeln`: `msg:E → akt:A → msg:C`).
- **Zusage-Panel:** Vertrag-Name (nur im Entwurf frei), ◀ Auf Event, Ausgänge ▶ (⊕/✕), „Strom", die Signatur als Zeile.
- „◀ kommt aus" eines Commands nennt „Akteur · Zusage auf E", „geht an ▶" eines Events „Akteur · Zusage" samt tragenden Clients (🔌).
- **Schreiben:** Scaffolder legt das Interface neben den Akteur; der Abgleich hängt fehlende `Auf` an, ersetzt geänderte Rückgaben,
  streicht entfernte (nur im Haupt-Vertrag; weitere Teile liest der Editor). Danach `./codegen.sh` → Python-Basis.

### 12.6 Client: Rahmen, Anschlussleiste, Leitungen

- **Client-Rahmen** je `IClientVertrag`, rechts neben allen Domänen (gestrichelt, Kopf: 🔌 Name · verkörperte Akteure · ◎ · ⤢).
- **Die Karte ist die Anschlussleiste** — der Vertrag, in jeder Ansicht lesbar: „↺ trägt IX · als Akteur" mit je Zusage einer Zeile,
  „▶ sendet als …", „? fragt als …" (gruppiert nach Akteur; ⚠ wenn ihn kein Akteur darf), „◀ hört". Jede Nachricht steht beim Namen.
- **Leitungen:** je Client × Ziel-Rahmen **ein Bündel**, in Worten beschriftet („sendet A, B · fragt Q · hört E …", lange Listen „… und N
  weitere"). Ziel einer getragenen Zusage ist der 📜-Rahmen ihres Akteurs, sonst der Rahmen, in dem die Nachrichten-Karte steht.
  Durchgezogen = trägt eine Zusage mit Ausgabe (die Kette hängt durabel an diesem Client), gestrichelt = nur Senden/Fragen/Hören.
- **Zoom ändert nichts.** Einzelkanten gibt es nur durch eine Handlung: Klick auf eine Leisten-Zeile (genau diese Nachricht), ein Bündel
  oder einen 🔌-Knopf der Stecker-Zeile (die Leitung zu diesem Rahmen). Das Auffächern endet, wenn die Auswahl wechselt.
- **Panel:** Name/Namespace (Name nur im Entwurf frei), Handshake-Name und generierte Basis, verkörpert (abgeleitet), **trägt ⊕/✕**
  (Akteure mit Vertrag leuchten), **sendet/fragt ⊕/✕** (Commands, Queries, Trigger — eine Query landet in „fragt"), **hört ⊕/✕**
  (Events), die Leitungen je Ziel-Rahmen (Klick = auffächern). ✓-Karte im Verbinden-Modus = lösen.
- **Schreiben:** neuer Client → Interface in `Clients.cs` des Namespace; bestehender → Abgleich von Basisliste (Teile, `ISendet`, `IFragt`;
  fremde Basistypen bleiben) und Kenntnis-Methoden.

### 12.7 Ableitung, Parität, Diagnose

- **Eine Quelle:** C# (`AkteurAnteile`, `Validator`, `Grammatik`) und JS-Spiegel rechnen gleich; das Board bekommt die Akteur-Mengen als
  `rahmen.akteurMengen`, die Grammatik als `rahmen.grammatik`.
- **`--check`** vergleicht je Akteur Befugnisse und Vertrag, je Client den Rand (Code ⇄ Board); **`--sonde`** enthält frei benannte
  Akteure, Verträge und Clients (n:m) sowie gezeichnete, über den Scaffolder geschriebene.
- **Browser-Diagnose:** `deAkteure()`, `deAkteurParitaet()`, `deHerkunft()`, `deClients()`. Layout-Version `KPOS_VERSION 8`.
- **Offen:** Simulation „als Akteur/Client", Slice entlang der Kette über Domänen, Client-Innenseite (Blazor-Intents/Stores).
