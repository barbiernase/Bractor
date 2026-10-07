# Konzept: Pipelines im Domänen-Editor

> **Einordnung:** Dieses Dokument ist eine *Anwendung* der allgemeinen Kompositions-Sprache
> (`docs/konzept-editor-komposition.md`): Alphabet (Kanäle, Bausteine), Grammatik (§6 hier), Operatoren (Schleife, Warten,
> Verkettung), Kapselung und Linsen gelten dort allgemein. Die Darstellungen hier (Band, Matrix, Ablauf) sind **Linsen**, keine
> Abbildung des Bestands; die Bestands-Pipelines dienen nur als Beispiele.

> Stand 2026-10-07 · **teilweise umgesetzt**: §12 (Ausgänge nach Typ, Frist als Ausgang, `veröffentlicht`, 2026-10-01) und §13
> (Laufzeit-Befunde aus §8 + Rundweg-Fehler, 2026-10-07). Weiter Konzept: Phasen 1, 3–5 (Kanal-Symbole, Garantie-Strich,
> Zeit-Spur, Ingress als Domänen-Fakt, Simulation). Grundlage: Laufzeit-Aufnahme (Code-Stellen unten) und die Editor-Arbeit aus
> `docs/konzept-handle-ausgaenge.md` §11–§13. Verwandt: `docs/konzept-domaenen-editor.md` §7 (Betrieb/Host),
> `docs/konzept-streaming-architektur.md` (Ingress-Konnektoren), `docs/04-konsum-und-prozess-maschine.md` §4.6 (P6.1/P6.2).

## 1 · Die Frage

Der Editor zeigt Pipelines heute als einen Konsumenten unter vielen: Karte mit Handles, je Handle ein Eingang und einige
Ausgangs-Ports. Das reicht nicht, weil eine Pipeline anders ist als Projektion, Reaktion oder Prozess:

- Sie ist die **Grenze zur Außenwelt und zur Zeit**. Webhooks, Timer und Dateien kommen als Trigger herein, geplante
  Selbst-Nachrichten und Fristen als Zeit.
- Sie hat **fünf Eingangskanäle mit verschiedenen Garantien** und **sechs Ausgangsarten**, auch hier mit verschiedenen
  Garantien. Welche davon verlierbar sind und welche durabel, ist Invariante 6, und der Editor zeigt es heute nicht.
- Ihr Programmiermodell hat sich mit §11 geändert: Planen ist ein Ausgang (`Selbst<T>`, `Frist<TCmd>`), kein Aufruf. Der Editor
  trägt aber noch Knoten aus der Zeit davor, den Frist-Knoten und den Trigger-Modus „Frist“.

Ziel: **Alles, was eine Pipeline ist und tut, ist im Editor sichtbar, zeichenbar und wird exakt geschrieben.** Das gilt für
Eingänge, Ausgänge, Garantien, Konfiguration und Ingress, jeweils als Code-Fakt aus der Signatur. Kein Rumpf-Fakt, keine
Namensregel.

## 2 · Was eine Pipeline zur Laufzeit ist (Ist, aus dem Code)

**Vertrag.** Eine Pipeline ist eine Klasse `: IPipelineHandler` mit `PipelineId`. Ihre Handles haben die Form
`Handle(TEingang, PipelineContext, Fähigkeit…)` (CQRS057). Es gibt zwei Rückgabe-Formen, je nach Ausgabemenge (§13 dort): **keine
Ausgabe ⇒ `Task`**, sonst **`IAsyncEnumerable<OneOf<…>>` bzw. `IEnumerable<OneOf<…>>`**, auch bei nur einem Ausgang.
`PipelineContext` ist reine Daten (`CorrelationId`, Quelle-Aggregat nur bei Event-Eingang). Konfig-Records und Dienste kommen
über den Konstruktor, Fähigkeiten als Parameter. Kein Store und kein Fristplan im Zustand (CQRS054).

**Eingänge**

| Kanal | Woher | Transport | Garantie |
|---|---|---|---|
| **Trigger** (`: IPipelineTrigger`) | Ingress (Webhook `MapPipelineWebhook`, Timer `TimerTrigger.Registrierung`, gRPC-Client), eine andere Pipeline | Cluster-Request an `Pipeline-{Id}` (die EINE Aktivierung im Cluster, §13), 5 s, `PipelineAck` | verlierbar; heilt durch Re-Trigger |
| **Persistentes Event** (`IEvent`) | Aggregat-Log | eigener Pull-Pfad je Stream (`pull-pipeline-{Name}`, `IEmittentenCursor`, Signal + Poll) | at-least-once, parallel über Streams |
| **Transientes Event** (`ITransientEvent`) | Broker | Push an den Pipeline-Actor | verlierbar |
| **Selbst-Nachricht** (`: IPipelineSelfMessage`) | eigener `Selbst<T>`-Ausgang | `ReenterAfter` im Actor, Token ersetzt | verlierbar (nur im Speicher) |
| **Start** (`PipelineGestartet`) | Framework, je Aktivierung (der `PipelineStartupService` jedes Knotens hält sie per `PipelineAktivieren` am Leben) | Selbst-Kanal | einmal je Aktivierung, der einzige Wiederanlauf für Ticks |

**Ausgänge** (die OneOf-Varianten)

| Art | Wohin | Garantie |
|---|---|---|
| **Command** | Aggregat über `CommandEmitter` (einziger Emit-Weg, CQRS020/021) | ab Event-Eingang idempotent (Kausalität = Quelle + Version); ab Trigger/Selbst **nicht** idempotent (Kausalität = neue Guid) |
| **Trigger** | die Pipeline, die diesen Trigger-Typ behandelt | verlierbar |
| **Transientes Event** | Broker | verlierbar |
| **`Selbst<T>`** | derselbe Actor, nach Verzögerung | verlierbar; **aus einem Event-Handle nicht möglich** (Pull-Pfad ohne Mailbox → `NotSupportedException`) |
| **`Frist<TCmd>`** | Fristplan (Marten, DB-Uhr) → Command `TCmd(Guid)` am Ziel-Aggregat | durabel; fällig = Log-Zeit des auslösenden Events + Dauer (ohne Event: DB-Uhr) |
| **`FristStorno<TCmd>`** | löscht die Frist (deterministische FristId) | durabel |

Code-Stellen: `Abstractions/Planung.cs`, `Abstractions/PipelineContext.cs`, `Infrastructure/Pipeline/PipelineActorBase.cs`,
`PipelineTriggerSender.cs`, `PipelineEventPullBridge.cs`, `Infrastructure/Deadlines/FristPlaner.cs`,
`Domain.SourceGeneration/PipelineDispatchGenerator.cs`, `Infrastructure.SourceGeneration/PipelineActorGenerator.cs`.

## 3 · Ist im Editor und die Lücken

Stand der Aufnahme 2026-09-30; ~~durchgestrichen~~ = inzwischen erledigt (§12, §13).

| Thema | Editor heute | Code | Lücke |
|---|---|---|---|
| Eingangsarten | Trigger / Event / Self | + transientes Event, + Start, persistent ≠ transient | Transport und Garantie unsichtbar; Start erscheint als „Self PipelineGestartet“ und ist nicht zeichenbar |
| Ausgänge | „+ Ausgang ▶“: Command (sofort · ⏳ Frist · ✕⏳ Storno), Trigger, transientes Event, Self-Tick, Read-Fn | dieselben OneOf-Varianten | ~~Frist nur als Alt-Knoten; transiente Events fehlen~~ (§12) |
| Frist | Ausgang am Handle | Ausgang am Handle; Dauer ist ein Laufzeitwert im Rumpf | ~~zwei Wahrheiten~~ (§12); Reste des alten Frist-Knotens stehen noch im Board-JS (§13.3) |
| Trigger-Modus | Timer / Webhook / FileWatch | Ingress = Aufruf einer `[Ingress]`-Methode | ~~„Frist“ als Modus~~ (§12); Timer/Datei haben im Code kein Vorbild zum Schreiben, `IngressArt.Datei` trägt keine Methode |
| Verzögerung von `Selbst` | kein Feld (Rumpf) | Rumpf (`Selbst.In(msg, dauer)`) | richtig so, aber nicht erklärt |
| Handle-Form | automatisch (§13) | `Task` / Strom mit OneOf | erledigt |
| Dienste | „nutzt Dienst“-Ports, Dienst-Knoten | Konstruktor + DI-Bindung im Host | wird weder gelesen noch geschrieben (`dienste` fehlt in `PipelineKarte`) |
| Simulation | Pipelines laufen nicht mit | – | keine Vorschau, was ein Trigger bewirkt |
| Guardrails | keine pipelinespezifischen | siehe §6 | Fehlformen fallen erst zur Laufzeit auf |

## 4 · Leitidee

**Eine Pipeline ist der Übersetzer am Rand: Außenwelt und Zeit hinein, Domänen-Commands hinaus.** Der Editor zeigt sie deshalb
nicht als Kasten neben Projektionen, sondern als **Band am Rand des Boards**:

- links die **Quellen** (Ingress: Webhook, Timer, Datei, Client),
- in der Mitte die **Pipeline** mit ihren Handles, wie ein Decider mit Regeln,
- rechts die **Ziele** (Aggregate über Commands, andere Pipelines über Trigger, der Broker), dazu **die Zeit** als eigene Spur
  (Selbst-Schleifen, Fristen).

Drei Grundsätze aus den Invarianten:

1. **Nur Signatur.** Eingang = erster Parameter, Ausgänge = OneOf, Abhängigkeiten = Parameter bzw. Konstruktor. Was der Rumpf
   *wann* tut, zeigt der Editor nicht (wie bei Decidern).
2. **Garantie ist sichtbar (Invariante 6).** Jede Kante trägt ihre Garantie als Strich: **durchgezogen = durabel**
   (persistentes Event, Frist, Command ab Event), **gestrichelt = verlierbar** (Trigger, transientes Event, Selbst, Command ab
   Trigger/Selbst). Wer eine verlierbare Kante auf einen Pfad legt, der Durabilität braucht, sieht es am Strich.
3. **Die Form folgt dem Inhalt.** Man zeichnet Ausgänge; `Task` oder Strom und OneOf ergeben sich daraus (schon umgesetzt, §13).

## 5 · Zielbild: Knoten, Ports, Kanten

### 5.1 Pipeline-Karte

- Kopf: Name, Namespace, **PipelineId**, **Konfigs** (Konstruktor), **Dienste** (Konstruktor, Port zum Dienst-Knoten).
- Hinweis-Zeile zur Laufzeit: „ein Actor je PipelineId (serielle Mailbox) · Events parallel je Stream“. Dieser Hinweis gilt erst,
  wenn die Befunde aus §8 behoben sind; bis dahin zeigt der Editor den Ist-Zustand als Warnung.
- Liste der Handle-Karten, gruppiert nach Kanal: **Start**, **Trigger**, **Events**, **Selbst**.

### 5.2 Handle-Karte (eigene Karte, wie der Decider)

**Eingangs-Port**, eine Art je Symbol:
- ⚡ Trigger (Record `: IPipelineTrigger`) — Port zu Ingress-Knoten oder Trigger-Ausgang einer anderen Pipeline
- ◆ persistentes Event (durabel, Pull) · ◇ transientes Event (verlierbar, Push)
- ↺ Selbst (Record `: IPipelineSelfMessage`) — der Rückkanal einer Selbst-Schleife
- ▶ Start (`PipelineGestartet`) — Framework-Eingang, höchstens einer je Pipeline, kein Domänen-Record

**Ausgangs-Ports** = die OneOf-Varianten, je Art ein Symbol, alle auf einer Karte:
- `sendet Command ▶` → Aggregat. Strich je nach Eingang: ab ◆ durchgezogen (idempotent), sonst gestrichelt.
- `erzeugt Trigger ▶` → Handle einer anderen Pipeline (Verkettung, z. B. FileWatch → ImageProcessing)
- `veröffentlicht ▶` → transientes Event (Broker), gestrichelt. **Neu**; ein persistentes Event ist hier nicht wählbar (§6).
- `plant Selbst ↺` → die Selbst-Nachricht; die Kante läuft als **Schleife** zurück auf den ↺-Handle derselben Pipeline.
  Anlegen erzeugt Record und Handle (so heute schon, §13).
- `plant Frist ⏳ Command` → die Frist-Kante endet am Ziel-Aggregat (durabel, mit Uhr-Symbol);
  `storniert Frist ✕⏳ Command` → dieselbe Frist, von einem anderen Handle. Die **Dauer** ist Rumpf (Laufzeitwert) und wird
  als „Dauer: im Rumpf“ ausgewiesen.
- `darf Store.Fn ▶` → Lese-Fähigkeit (Parameter).

**Form-Zeile**: „gibt nichts zurück (Task)“ bzw. „Strom · OneOf<…>“, abgeleitet, nicht editierbar.

### 5.3 Ingress-Knoten (Quelle)

Ein Knoten je Bindung im Host: Modus **Webhook** (Route), **Timer** (Intervall), **Datei** (Pfad/Muster). Er erzeugt genau eine
Trigger-Nachricht (Kante zum ⚡-Port). Die Trigger-Nachricht selbst ist eine **Record-Karte** (Felder), keine Unterform des
Ingress-Knotens. So kann derselbe Trigger aus mehreren Quellen kommen (Webhook + gRPC-Client).
Der Modus „Frist“ entfällt (§5.4).

### 5.4 Frist: vom Knoten zum Ausgang

Der Frist-Knoten ist die Form vor §11. Zielbild: **eine Frist ist ein Paar von Ausgängen** (`Frist<TCmd>` an einem Handle,
`FristStorno<TCmd>` an anderen) und **eine Kante zum Ziel-Aggregat**. Das Board kann das Paar als „Frist-Klammer“ hervorheben
(gleicher `TCmd` = gleiche Frist). Migration: Ein Frist-Knoten aus dem Code wird beim Einlesen zu diesen Ausgängen; die Felder
„Kontext“ und „Dauer aus HostSetting“ entfallen (der Kontext ist der Command-Typname, die Dauer ist Rumpf).

### 5.5 Zeit-Spur

Selbst-Schleifen und Fristen sind die **einzige Zeit** im System. Eine eigene Spur unter den Pipelines macht sichtbar, welche
Pipeline wovon zeitlich abhängt: ↺ gestrichelt (geht bei Neustart verloren, neu gesät durch ▶ Start), ⏳ durchgezogen.

## 6 · Guardrails: was der Editor verhindert (und der Build erzwingen sollte)

| Regel | Heute | Editor (Validator/Picker) | Build (Vorschlag) |
|---|---|---|---|
| `Selbst<T>` nur aus Trigger/Selbst/Start-Handles | Laufzeit-Ausnahme im Pull-Pfad | „plant Selbst“ an ◆-Handles nicht anbieten | neuer Analyzer (Handle mit `IEvent`-Eingang ⇒ kein `Selbst` im OneOf) |
| Persistentes `IEvent` als Ausgang | still verworfen (kein Fall im Dispatch-Switch) | nur transiente Events wählbar | Analyzer: `IEvent` ohne `ITransientEvent` im Pipeline-OneOf ⇒ Fehler |
| Trigger von zwei Pipelines behandelt | letzte gewinnt still (`TriggerToPipelineId`) | Fehler beim Verbinden | Generator-Diagnose |
| Frist-Command braucht Ctor `(Guid)` | CQRS056 | Picker zeigt nur passende Commands | besteht |
| `IAsyncEnumerable<X>` ohne OneOf | still `Task` im Generat | Scaffolder schreibt immer OneOf | Aufgabe liegt vor (§13) |
| Start-Handle höchstens einmal | – | ▶ nur einmal anlegbar | (Signatur-Fakt, ggf. CQRS057) |
| Zyklus Command → Event → Pipeline → Command | Azyklizitäts-Guard deckt Pipelines nicht ab | Zyklus markieren | Guard erweitern (offen seit §11) |

## 7 · Schreiben (Code): Stand und Rest

Schon umgesetzt (§13): neue Pipeline und neue Handles, Konfig-Konstruktor, OneOf- bzw. `Task`-Form, Selbst-Record,
Trigger-Record, Fähigkeits-Parameter, Rückgabe-Abgleich bei geänderten Ausgängen, Webhook-Bindung nach Vorbild.

Seit §12 (2026-10-01) erledigt: `veröffentlicht` (transientes Event) und Frist/FristStorno als Ausgang im Panel. Seit §13
(2026-10-07): neue Konfig einer bestehenden Pipeline wird in den Konstruktor geschrieben; ein umverdrahteter Eingang schreibt den
Eingangs-Typ der bestehenden Methode um.

Offen:
3. **Ingress für Timer/Datei**: Es gibt im Code keine Bindung als Vorbild, also kann der Schreiber nichts kopieren. Vorschlag:
   Ingress-Bindungen werden selbst zum **Domänen-Code-Fakt**, zum Beispiel eine statische Methode mit `[Ingress(...)]` im
   Pipeline-Namespace, die der Host generiert einsammelt. Das entspricht dem Router-Muster von `GeneratedFristen`. Damit
   wandert die Bindung aus `Program.cs` in die Domäne und wird schreib- und lesbar wie alles andere.
4. **Dienst-Bindung** (Konstruktor der Pipeline + DI im Host): wie Punkt 3, erst ein Code-Träger, dann Schreiben.
5. **Konstruktor einer bestehenden Pipeline**: eine neue Konfig schreibt der Abgleich (§13); eine entfernte Konfig, ein Dienst,
   Umbenennen/Verschieben der Klasse werden nur gemeldet (der Rumpf kann sie nutzen).

## 8 · Voraussetzung: Laufzeit-Befunde, die das Editor-Bild heute falsch machen würden

Der Editor darf nur versprechen, was die Laufzeit hält. Die Aufnahme hat Abweichungen gefunden (geprüft, Code-Stellen):

| Befund | Stelle | Folge für das Bild |
|---|---|---|
| ~~**Zwei Instanzen je Pipeline**~~ — **behoben 2026-10-07 (§13.1)**: kein lokaler Spawn mehr, nur die Cluster-Aktivierung `Pipeline-{Id}` | `Infrastructure/Pipeline/PipelineStartupService.cs` | war: FileWatch pollte auf jedem Knoten |
| **Singleton-Handler** für den Pipeline-Actor UND den Pull-Actor des Event-Pfads | `PipelineActorGenerator.cs` (`AddSingleton`) | Handler-Felder sind nur geschützt, solange der Zustand allein auf dem Trigger-/Selbst-Kanal lebt (so bei FileWatch) |
| **Commands ab Trigger/Selbst nicht idempotent** (Kausalität = `Guid.NewGuid()`) | `PipelineActorBase.cs:210-214` | ein Re-Trigger kann doppelt wirken; die Strich-Regel (§4) muss das zeigen |
| **Persistentes Event im OneOf still verworfen** | `PipelineDispatchGenerator.cs:427-450` | siehe §6 |
| **Trigger → zwei Pipelines: letzte gewinnt still** | `PipelineActorGenerator.cs:98-139` | siehe §6 |
| **Fähigkeits-Bereich je Actor/Stream, nie freigegeben** | `DiFaehigkeitsFabrik.cs:17`, Generator :341/:485 | Store-Instanzen leben so lange wie der Actor (Lese-Sichten evtl. veraltet) |
| ~~Doku veraltet~~ (nachgezogen 2026-10-07) | – | – |
| ~~**Frist rückte bei jedem Poll nach hinten**~~ — **behoben 2026-10-07 (§13.1)** | `FristPlaner.cs` | – |

Entschieden (2026-10-07): **Pipelines sind Cluster-Singletons** je PipelineId (§11.1, umgesetzt in §13.1). „Je Knoten“ bliebe
ein expliziter Signatur-Marker, falls eine Pipeline je an lokale Ressourcen gebunden sein muss.

## 9 · Simulation (später)

Pipelines in die Editor-Simulation aufnehmen: Ein ⚡-Knopf je Trigger-Record (Felder als Eingabe, wie bei Commands), eine
**virtuelle Uhr** für ↺ und ⏳ (vorspulen), Commands laufen in die vorhandene Aggregat- und Saga-Simulation. Dienste und
Fähigkeiten brauchen dafür Doubles aus dem Editor (Dienst-Knoten mit Code-Insel). Das zeigt, was ein Trigger in der Domäne
auslöst, bevor Code läuft.

## 10 · Umsetzung in Phasen

| Phase | Inhalt | Voraussetzung |
|---|---|---|
| **0** | Laufzeit-Befunde §8 entscheiden — Singleton ✔ (§13.1); offen: Idempotenz ab Trigger, Event-Ausgang, Doppel-Trigger | – |
| **1** | Board-Darstellung: Kanal-Symbole ▶ ◆ ◇ ⚡ ↺, Garantie-Strich an allen Pipeline-Kanten, Start als Framework-Eingang, Form-Zeile | – |
| **2** | Ausgänge vervollständigen: `veröffentlicht` (transient), `Frist`/`FristStorno` als Ausgang, Frist-Knoten migrieren, Modus „Frist“ entfernen | 1 |
| **3** | Guardrails §6 im Editor (Picker/Validator) + Analyzer-Vorschläge | 1 |
| **4** | Ingress als Domänen-Code-Fakt (Timer/Datei/Webhook) + Dienst-Bindung → schreib- und lesbar | 0 |
| **5** | Zeit-Spur + Simulation mit virtueller Uhr | 2 |

## 11 · Offene Entscheidungen (mit Empfehlung)

1. **Pipeline: Cluster-Singleton oder je Knoten?** ✔ Entschieden und umgesetzt (§13.1): Cluster-Singleton je PipelineId (Trigger landen ohnehin dort,
   Selbst-Ticks sollen nicht n-fach laufen). „Je Knoten“ nur, wenn eine Pipeline an lokale Ressourcen gebunden ist, und dann
   als expliziter Signatur-Marker.
2. **Idempotenz ab Trigger/Selbst?** Empfehlung: Kausalität aus dem Trigger ableiten (z. B. eine Trigger-Id im Umschlag), damit
   ein Re-Trigger nicht doppelt wirkt. Bis dahin zeigt der Editor diese Kanten gestrichelt.
3. **Dürfen Pipelines Schreib-Fähigkeiten haben?** Heute bietet der Editor nur Lese-Fns an. Pipelines sind emittierend (Achse B,
   `IEmittentenCursor`), ein Store-Schreiben wäre also nicht co-committet. Empfehlung: nur lesen; Schreiben geht über Commands.
4. **Ingress im Host oder in der Domäne?** Empfehlung: Domäne (§7.3), sonst bleibt Timer/Datei im Editor unschreibbar.
5. **Frist-Knoten ganz entfernen oder als Sicht behalten?** Empfehlung: als **Sicht** (Frist-Klammer über `Frist`/`FristStorno`
   desselben Commands), nicht als eigenständiges Modell-Element.

## 12 · Nachrichten mit zwei Seiten: „◀ kommt aus" / „geht an ▶" (2026-10-01, umgesetzt)

**Befund (GUI).** Eine Pipeline gibt ein OneOf zurück; die Laufzeit routet jede Variante **nach ihrem Typ** (Command → Aggregat,
Trigger → Pipeline, transientes Event → Broker, `Selbst<T>` → derselbe Actor, `Frist<T>` → Fristplan → Aggregat). Das Board machte
es umgekehrt: je Ausgangsart ein eigener Port und eine eigene Liste (`sends`/`emits`/`schedules`), Kanten/Picker/Kurzzeile je Art von
Hand. Was in keine Schiene passte, war unsichtbar:

- **Frist/FristStorno** standen nur im alten Frist-Knoten (Composition-Root-Sicht), nicht am Handle — die Handles der
  `TrainingFristPipeline` hatten keinen einzigen Ausgang.
- **Transiente Events** (Board-Art „Ablehnung") hatten keinen Ausgang: `PaarNichtKomplett → ImageProcessingPipeline` wurde nicht
  gezeichnet und war nicht neu verdrahtbar; „veröffentlicht" fehlte am Pipeline-Handle. Der Schreiber hätte einen transienten
  OneOf-Typ beim „C# schreiben" entfernt.
- **Teil-Laden** („Domänen laden"): Pipelines liegen in `Domain.Pipeline.*`, ihre Commands im Aggregat-Namespace — ohne beide
  geladen endeten die Handles im Leeren; nicht Geladenes war kein Ziel und nicht einmal benannt.
- **Trigger-Ketten** liefen Handle → Handle an der Trigger-Nachricht vorbei.

**Leitidee.** Jede Nachricht ist eine Karte mit **zwei Seiten**: ◀ *kommt aus* (Erzeuger) und *geht an* ▶ (Konsumenten). Das ist
dieselbe Kante wie der Ausgang am Erzeuger — **eine** Wahrheit (die OneOf-Signatur des Erzeugers), zwei Enden. „kommt aus" ist
keine zweite Liste am Command, sondern die umgekehrte Sicht; Verbinden von der Nachricht aus schreibt in den OneOf des Handles.

| Nachricht | ◀ kommt aus | geht an ▶ |
|---|---|---|
| Command | beliebig: Außenwelt (abgeleitet), Prozess, Reaktion, Pipeline, Pipeline per Frist ⏳ (✕⏳ Storno) | genau ein Aggregat (Decider) |
| Event | genau ein Aggregat (Decider); reaktiv veröffentlicht | beliebig: Applier, Prozess, Projektion, Reaktion, Pipeline |
| Transient | Decider (Ablehnung), Projektion/Reaktion/Pipeline (veröffentlicht) | beliebig: Projektion, Reaktion, Pipeline |
| Trigger | Ingress (Code-Fakt `[Ingress]`), Pipeline; ohne Erzeuger: Außenwelt | genau eine Pipeline |
| Query / Response | Außenwelt / Reader | Reader / Außenwelt |

Die Kardinalität kommt aus der Grammatik (`rahmen.grammatik.konsume`), nicht aus dem Board-Code.

**Umsetzung.**
1. **Ein Ausgangs-Port je Pipeline-Handle** („+ Ausgang ▶", Port-Typ `aus`). Die Art folgt aus der angeklickten Karte: Command →
   `sends`, transientes Event → `publishes`, Trigger-Karte → `emits`; ein persistentes Event ist gesperrt (GR-KEIN-EVENT-AUS-PIPELINE).
   Je Command-Zeile wählt man **sofort · ⏳ Frist · ✕⏳ Storno** (`hd.fristen = [{command, art: frist|storno}]`).
2. **Nachrichtenkarten** (Command/Event/Ablehnung/Query/Response/Trigger) zeigen „◀ kommt aus" und „geht an ▶" als Listen der
   Partner (anklickbar = springen; ✕ an Handle-Ausgängen = lösen) plus je Seite einen ⊕-Port. Die Ablehnung/transientes Event hat
   jetzt einen Ausgang. Die Trigger-Karte hat „◀ kommt aus"; Ketten laufen Handle → Trigger-Karte → Handle.
3. **Grenz-Partner:** ein Partner aus einer nicht geladenen Domäne erscheint als „↗ außerhalb · Namespace"; Klick lädt die Domäne
   dazu. Die Kurzzeile zeigt „↗ n außerhalb".
4. **Frist ist ein Ausgang**, kein Knoten: der Mapper leitet keinen Frist-Knoten mehr ab, die Palette bietet ihn nicht mehr an; der
   Trigger-Modus „Frist" entfällt.
5. **Extractor/Mapper/Schreiber:** Ausgangs-Art `transient` (vor `event`); Pipeline-Handles tragen `publishes` und `fristen`;
   `BoardLeseseite` schreibt sie zurück (`Frist<T>`/`FristStorno<T>`, transiente Typen bleiben erhalten).
6. **`--check`:** jede Variante einer Pipeline-Signatur muss am Board-Handle als Ausgang stehen.

## 13 · Nachvollzug Programmiermodell ⇄ Editor (2026-10-07, umgesetzt)

Die Pipeline wurde einmal vollständig nachverfolgt: Code → Generatoren (`PipelineDispatchGenerator`, `PipelineActorGenerator`) →
Laufzeit (`PipelineActorBase`, `PipelineEventPullBridge`, `PipelineTriggerSender`, Fristplan) und Code → Extractor → Board → Schreiber.
Ohne Änderungen im Editor ist der Rundweg verlustfrei (`--check`: Board ⇄ Modell, Fixpunkt mit allen fünf Pipelines). Behoben:

### 13.1 Laufzeit

1. **Frist rückte bei jedem Poll nach hinten.** Ein emittierender Konsument liest beim Poll den Stream bewusst ab 0 neu
   (at-least-once). Commands dedupliziert der Empfänger, aber `Frist<TCmd>` ging direkt an den `FristPlaner`, und der rechnete
   `jetzt + Dauer`. Folge: jede Bewegung des Streams (z. B. `TrainingFortschritt`) verschob die 6-h-Frist von `TrainingBegonnen`, sie
   feuerte nie. **Jetzt** ist die Basis die Log-Zeit des auslösenden Events (`PipelineContext.SourceEventZeit` → `FristAuftrag.Ab`); jedes
   erneute Lesen ergibt dieselbe Fälligkeit. Ohne Event (Trigger/Selbst) bleibt es die DB-Uhr. Feuert eine schon gefeuerte Frist nach
   einem Replay erneut, dedupliziert der Empfänger (`FristId.FürZustellung`). Prüfstand: `FristPlanerTests`, `PipelineEventPullBridgeTests`.
2. **Doppelte Pipeline-Instanz.** Der `PipelineStartupService` spawnte jede Pipeline zusätzlich lokal auf jedem Knoten. **Jetzt**
   gibt es nur die Cluster-Identität (`PipelineTriggerSender.Identitaet`); der Dienst jedes Knotens schickt ihr alle 30 s (nach Fehlschlag
   2 s) `PipelineAktivieren` — die erste Nachricht aktiviert sie, jede weitere wird nur quittiert, nach einem Knoten-Ausfall aktiviert der
   nächste Durchlauf sie anderswo neu. `GetPipelineSpawnInfos` ist durch `GetPipelineIds` ersetzt. Neue Wire-Nachricht
   `PipelineAktivieren` (Emitter + `CqrsWireJsonContext.g.cs`, `codegen.sh` ohne Drift).

### 13.2 Editor-Rundweg

1. **Umverdrahteter/umbenannter Eingang ging verloren.** `BoardLeseseite` bevorzugte `input`, das JS pflegte aber `event`/`selfName`
   bzw. die Trigger-Karte. **Jetzt** gilt das bearbeitete Feld (Trigger über `trigId` oder `prod.id`), `input` nur als Rückfall; das JS
   hält `input` beim Umverdrahten, beim Umbenennen eines Records und beim Umbenennen der Trigger-Nachricht mit. Weicht der Eingang vom
   Code ab, reist der alte als `Handle.EingangImCode` mit, und „C# schreiben“ schreibt den Eingangs-Typ der bestehenden Methode um.
2. **Konfig umbenennen** griff nur im Command-Zweig von `renameRefs` — jetzt für jeden Record.
3. **Änderungen an einer bestehenden Pipeline-Karte** (Konfigs, Name, Namespace) waren nicht im Herkunfts-Stempel. **Jetzt** sind sie
   es; eine neue Konfig wird im Konstruktor ergänzt (Parameter, Feld, Zuweisung, `using`), Umbenennen/Verschieben und eine entfernte
   Konfig werden ausdrücklich gemeldet.

### 13.3 Weiter offen (gefunden, nicht behoben)

Laufzeit: Commands ab Trigger/Selbst ohne Dedup (§11.2); Response/beliebiger Typ im Pipeline-OneOf wird still verworfen; `Selbst<T>` in
einem Event-Handle fällt erst zur Laufzeit auf (kein Analyzer, §6); Trigger → zwei Pipelines: letzte gewinnt; FileWatch markiert eine
Datei vor dem `PipelineAck` als gesehen und sät beim Start den Bestand als gesehen — eine fehlgeschlagene Datei wird nie verarbeitet;
teure Event-Handles (z. B. `DatensatzResolverPipeline`) laufen bei jedem Poll erneut (Wirkung dedupliziert, Arbeit nicht).

Editor: „nutzt Dienst“ ohne Wirkung; ein Akteur mit mehreren Diensten bekommt immer den ersten; fehlendes `using` für Akteur-Dienste;
ein `emits`-Name ohne Trigger-Karte erzeugt keinen Record; Umbenennen eines Self-Ticks bricht die Schleife; der Start-Handle ist nicht
zeichenbar; gelöschte Handles bleiben ohne Meldung im Code; Extractor (jeder Methodenname) und Dispatch-Generator (nur `Handle`) erkennen
verschiedene Handles; verschachtelte Selbst-Nachrichten (`FileWatchPipeline.PollTick`) sind keine Records; Reste des alten Frist-Knotens
im Board-JS; Commands nur per Frist erscheinen im Graph mit Herkunft „client“. `--check`/Sonde vergleichen Ingress und Dienste nicht; die
Sonde deckt `FristStorno`, Event-Eingang, `publishes`, Trigger-Kette, `Task`-Form und Ingress nicht ab.

### 13.4 Verarbeitung = Prozess aus Katalog-Funktionen (2026-10-07, umgesetzt)

Die `ImageProcessingPipeline` war ein Monolith: Dateiname deuten, OpenCV-Resize, Histogramm-Ausgleich und Melden in EINEM Rumpf —
im Editor unsichtbar, weil der Rumpf dem Extractor nichts liefert. Jetzt gilt die Trennung aus `konzept-pipeline-stroeme.md`/§11 der
Katalog-Funktionen („die Prozess-DSL ist der DAG“):

```
DateiErkannt ─(Pipeline: deuten)─▶ ErstelleImagePair + NimmRohbildAuf ─▶ RohbildEingegangen
RohbildEingegangen ─ƒ IBildVerkleinerung─▶ BildVerkleinert ─ƒ IHistogrammAusgleich─▶ HistogrammAusgeglichen
RohbildEingegangen + HistogrammAusgeglichen ─▶ MeldeBildVerfuegbar
```

- **Funktions-Katalog** `Domain/Bildaufbereitung/Funktionen.cs`: Auftrag → OneOf-Ergebnis-Events, domänenfrei (Bilder als Pfade).
- **Prozess** `Domain/ImagePair/BildaufbereitungProzess.cs`: je Rohbild eine Instanz; Paar-Id und Metadaten über den Join mit dem Auslöser.
- **Implementierung** `Domain.Pipeline/ImageProcessing/OpenCvBildaufbereitung.cs`, gebunden in `Host.Grpc/Program.cs` (`AddFunktion`, 2 Slots).
- **ImagePair**: neuer Eingang `NimmRohbildAuf` → `RohbildEingegangen` (je Version genau einmal, Zustand `Dc0/Dc2Eingegangen`).
- **Editor**: die zwei Funktionen erscheinen als ƒ-Karten (Auftrag → Ergebnisse), der Prozess als Rahmen mit drei Regeln
  („WENN RohbildEingegangen → ƒ IBildVerkleinerung ⏳“ …). Eine weitere Funktion ist eine neue ƒ-Karte + „Dann ƒ“ an einer Regel.
- Validator: GR-ZYKLUS meldete Prozess → ƒ → Ergebnis → Prozess als Kreis — der Prozess zählt jetzt wie das Aggregat als Zustandsschritt.
- Prüfstand: `BildaufbereitungProzessTests` (echter Prozess, Fake-Funktionen), `ImagePairRohbildTests`.

Offen: `BildNichtLesbar` hat keine fachliche Folge-Regel (der Prozess endet ohne Meldung); der Python-/externe Ausführer fehlt noch
(nur C#-Bindung); Proto-`oneof`-Nummern verschieben sich beim Regenerieren — Python-/Blazor-Clients neu generieren.

### 13.5 Ein Ablauf = ein Block (Editor-Anordnung, 2026-10-07)

Befund: Pipeline, Prozess, Regeln und Funktionen lagen im Brücken-Block „§geteilt“, nach ART in Spalten sortiert — rückwärts zum
Fluss (Prozess · Regel · Funktion · Pipeline · Trigger) und getrennt vom Aggregat-Block; die Kette lief quer über das Board.
Jetzt (nur Darstellung, `HtmlPresenter.cs`):

- **Zuordnung (`groupKeyOf`)**: Ablauf-Bausteine stehen im Block des Aggregats, auf das sie WIRKEN, wenn es eindeutig ist —
  Pipeline über ihre Commands (ein ausgegebener Trigger zählt mit der Pipeline, die ihn verarbeitet), Trigger über seine Pipeline,
  Prozess/Regel über die berührten Aggregate, Funktion über die Prozesse, die sie rufen, Auftrag/Ergebnis mit ihrer Funktion.
  Wirkt etwas auf mehrere Aggregate, bleibt es im Brücken-Block (wie bisher).
- **Spalten (`ROLE_AGG`)** in Flussfolge: Trigger → Pipeline → Pipeline-Handle → Command → Decider → Aggregat → Event → Applier →
  Prozess → Regel → Auftrag → Funktion → **Ergebnis ƒ** → Value Object → Leseseite.
- **Akteur-Rahmen**: Regeln/Funktionen/Ergebnisse ohne eigene Kette erben den Akteur ihres Prozesses — die Kette bleibt in EINEM
  Rahmen (hier „ImagePair · KameraSystem“).
- **Ketten-Fokus**: Klick auf Prozess/Funktion/Auftrag leuchtet die ganze Kette (über Regeln, Funktionen, Ergebnisse) auf.

### 13.6 Ablauf als Kette: jede Funktion ein Knoten (2026-10-07)

Vorgabe: „erst die, dann die“ — eine Funktion ist ein Knoten, die Reihenfolge eine Kante. Regel-Karten, Aufträge und Ergebnis-Events
dazwischen waren die Hürde. Jetzt (nur Editor-Darstellung + Bearbeiten; Code und Laufzeit unverändert — darunter liegen dieselben
Prozess-Regeln):

- **Kettenschritt** (`kettenschritt`): eine Regel mit genau einem Ziel (ƒ oder Command), ausgelöst von einem Event, optional zusammen
  mit dem Start-Event (Kontext), ohne Kompensation/Fan-out/Sammeln. Sie wird als **Kante** gezeichnet: Prozess (Start) bzw. die Funktion,
  die das auslösende Ergebnis liefert → nächster Schritt; Beschriftung „erst“/„dann“ (+ ⏳ bei Zeitlimit), Klick öffnet die Regel
  (Argumente, Zeitlimit). Andere Regeln bleiben Karten.
- **Eingeklappt**: Auftrag und Ergebnis-Events einer Funktion stehen nur IN ihrem Knoten (ein Ergebnis bleibt Karte, wenn es außer
  Kettenschritten noch jemand liest).
- **Anordnung**: je Kettentiefe eine eigene Funktions-Spalte, Zeile = die des Vorgängers → Prozess → ƒ → ƒ auf einer Linie.
- **Bearbeiten**: am Prozess „Ablauf ▶ erst …“, an jeder Funktion „Ablauf ▶ dann …“ (bei mehreren Ergebnissen „weiter bei …“) —
  „＋ aus dem Pool wählen“ (Funktionen, Commands) hängt den nächsten Schritt an, ✕ löst ihn. Ein Command als Schritt bekommt den Start
  als Kontext in den Join (er braucht dessen Daten).

**Nachtrag (gleicher Tag) — wie Node-RED:** Nicht nur einfache Schritte, JEDE Prozess-Regel wird Kanten (`kettenKanten`):
Verzweigung = eine Kante je Ergebnis („bei BildNichtLesbar“), Zusammenführung = mehrere Drähte in einen Knoten („und“; UndAlle „alle“),
Fan-out „×N“, Kompensation gestrichelt rot „↩ rückgängig“, Zeitlimit „⏳“. Nur eine leere, gerade angelegte Regel bleibt Karte.
**Argumente** werden beim Anhängen automatisch zugeordnet (`autoArgs`: gleicher Name → Name endet gleich → einziger gleicher Typ;
Wert-Objekte aus ihren Feldern gebaut; jüngstes Ergebnis vor dem Start) und sind im Schritt-Panel als Tabelle „Feld ← Quelle“
änderbar („↻ automatisch zuordnen“; ein aus dem Code gelesener λ bleibt, bis man „⇄ als Zuordnung bearbeiten“ wählt). Für den
echten Prozess trifft die Zuordnung exakt den handgeschriebenen Lambda von `MeldeBildVerfuegbar`; nur Konstanten ohne Quelle
(Höhe 512) bleiben `default`. Scaffolder: Argumentlisten jetzt auch für Aufträge (`Rufe`), nicht nur für Commands.

