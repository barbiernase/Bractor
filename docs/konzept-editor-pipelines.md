# Konzept: Pipelines im Domänen-Editor

> **Einordnung:** Dieses Dokument ist eine *Anwendung* der allgemeinen Kompositions-Sprache
> (`docs/konzept-editor-komposition.md`): Alphabet (Kanäle, Bausteine), Grammatik (§6 hier), Operatoren (Schleife, Warten,
> Verkettung), Kapselung und Linsen gelten dort allgemein. Die Darstellungen hier (Band, Matrix, Ablauf) sind **Linsen**, keine
> Abbildung des Bestands; die Bestands-Pipelines dienen nur als Beispiele.

> Stand 2026-09-30 · Konzept, **nicht umgesetzt**. Grundlage: Laufzeit-Aufnahme (Code-Stellen unten) und die Editor-Arbeit aus
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
| **Trigger** (`: IPipelineTrigger`) | Ingress (Webhook `MapPipelineWebhook`, Timer `TimerTrigger.Registrierung`, gRPC-Client), eine andere Pipeline | Cluster-Request an `Pipeline-{Id}`, 5 s, `PipelineAck` | verlierbar; heilt durch Re-Trigger |
| **Persistentes Event** (`IEvent`) | Aggregat-Log | eigener Pull-Pfad je Stream (`pull-pipeline-{Name}`, `IEmittentenCursor`, Signal + Poll) | at-least-once, parallel über Streams |
| **Transientes Event** (`ITransientEvent`) | Broker | Push an den Pipeline-Actor | verlierbar |
| **Selbst-Nachricht** (`: IPipelineSelfMessage`) | eigener `Selbst<T>`-Ausgang | `ReenterAfter` im Actor, Token ersetzt | verlierbar (nur im Speicher) |
| **Start** (`PipelineGestartet`) | Framework, je Actor-Start | Selbst-Kanal | einmal je Start, der einzige Wiederanlauf für Ticks |

**Ausgänge** (die OneOf-Varianten)

| Art | Wohin | Garantie |
|---|---|---|
| **Command** | Aggregat über `CommandEmitter` (einziger Emit-Weg, CQRS020/021) | ab Event-Eingang idempotent (Kausalität = Quelle + Version); ab Trigger/Selbst **nicht** idempotent (Kausalität = neue Guid) |
| **Trigger** | die Pipeline, die diesen Trigger-Typ behandelt | verlierbar |
| **Transientes Event** | Broker | verlierbar |
| **`Selbst<T>`** | derselbe Actor, nach Verzögerung | verlierbar; **aus einem Event-Handle nicht möglich** (Pull-Pfad ohne Mailbox → `NotSupportedException`) |
| **`Frist<TCmd>`** | Fristplan (Marten, DB-Uhr) → Command `TCmd(Guid)` am Ziel-Aggregat | durabel |
| **`FristStorno<TCmd>`** | löscht die Frist (deterministische FristId) | durabel |

Code-Stellen: `Abstractions/Planung.cs`, `Abstractions/PipelineContext.cs`, `Infrastructure/Pipeline/PipelineActorBase.cs`,
`PipelineTriggerSender.cs`, `PipelineEventPullBridge.cs`, `Infrastructure/Deadlines/FristPlaner.cs`,
`Domain.SourceGeneration/PipelineDispatchGenerator.cs`, `Infrastructure.SourceGeneration/PipelineActorGenerator.cs`.

## 3 · Ist im Editor und die Lücken

| Thema | Editor heute | Code | Lücke |
|---|---|---|---|
| Eingangsarten | Trigger / Event / Self | + transientes Event, + Start, persistent ≠ transient | Transport und Garantie unsichtbar; Start erscheint als „Self PipelineGestartet“ |
| Ausgänge | Command, Trigger, Self-Tick, Read-Fn | + transientes Event, + `Frist`, + `FristStorno` | Frist nur als Alt-Knoten; transiente Events fehlen |
| Frist | eigener Knoten „plant auf Event / storniert auf Event / Dauer aus HostSetting“ | Ausgang am Handle; Dauer ist ein Laufzeitwert im Rumpf | zwei Wahrheiten; der Knoten ist Vor-§11-Form |
| Trigger-Modus | Timer / Webhook / FileWatch / **Frist** | Ingress = Aufruf einer `[Ingress]`-Methode; Frist ist kein Ingress | „Frist“ als Modus ist falsch; Timer/Datei haben im Code kein Vorbild zum Schreiben |
| Verzögerung von `Selbst` | kein Feld (Rumpf) | Rumpf (`Selbst.In(msg, dauer)`) | richtig so, aber nicht erklärt |
| Handle-Form | automatisch (§13) | `Task` / Strom mit OneOf | erledigt |
| Dienste | „nutzt Dienst“-Ports, Dienst-Knoten | Konstruktor + DI-Bindung im Host | wird nicht geschrieben |
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

Offen:
1. **`veröffentlicht` (transientes Event)** als Ausgang im Panel; der Abgleich kann es schon (OneOf-Typ-Argument).
2. **Frist als Ausgang** im Panel (`Frist`/`FristStorno`), Migration des Frist-Knotens.
3. **Ingress für Timer/Datei**: Es gibt im Code keine Bindung als Vorbild, also kann der Schreiber nichts kopieren. Vorschlag:
   Ingress-Bindungen werden selbst zum **Domänen-Code-Fakt**, zum Beispiel eine statische Methode mit `[Ingress(...)]` im
   Pipeline-Namespace, die der Host generiert einsammelt. Das entspricht dem Router-Muster von `GeneratedFristen`. Damit
   wandert die Bindung aus `Program.cs` in die Domäne und wird schreib- und lesbar wie alles andere.
4. **Dienst-Bindung** (Konstruktor der Pipeline + DI im Host): wie Punkt 3, erst ein Code-Träger, dann Schreiben.
5. **Konstruktor einer bestehenden Pipeline** (neue Konfig/Dienst): Heute ist er Handcode; mit 3./4. wird er ein Signatur-Fakt.

## 8 · Voraussetzung: Laufzeit-Befunde, die das Editor-Bild heute falsch machen würden

Der Editor darf nur versprechen, was die Laufzeit hält. Die Aufnahme hat Abweichungen gefunden (geprüft, Code-Stellen):

| Befund | Stelle | Folge für das Bild |
|---|---|---|
| **Zwei Instanzen je Pipeline**: `PipelineStartupService` spawnt jede Pipeline lokal auf **jedem** Knoten, *zusätzlich* ist sie als Cluster-Kind `Pipeline-{Id}` registriert | `Infrastructure/Pipeline/PipelineStartupService.cs:45-51`, `CqrsServiceExtension.cs:444/516` | „ein serieller Actor“ stimmt nicht; beide bekommen ▶ Start. FileWatch pollt auf jedem Knoten |
| **Singleton-Handler** für alle Instanzen und alle Event-Streams | `PipelineActorGenerator.cs:307` | Handler-Felder (z. B. FileWatch `_seen`) sind nicht durch eine Mailbox geschützt |
| **Commands ab Trigger/Selbst nicht idempotent** (Kausalität = `Guid.NewGuid()`) | `PipelineActorBase.cs:210-214` | ein Re-Trigger kann doppelt wirken; die Strich-Regel (§4) muss das zeigen |
| **Persistentes Event im OneOf still verworfen** | `PipelineDispatchGenerator.cs:427-450` | siehe §6 |
| **Trigger → zwei Pipelines: letzte gewinnt still** | `PipelineActorGenerator.cs:98-139` | siehe §6 |
| **Fähigkeits-Bereich je Actor/Stream, nie freigegeben** | `DiFaehigkeitsFabrik.cs:17`, Generator :341/:485 | Store-Instanzen leben so lange wie der Actor (Lese-Sichten evtl. veraltet) |
| Doku veraltet: `IAsyncEnumerable<ICommand>` in `docs/04` §4.6, `ctx.ScheduleSelf` in `docs/konzept-domaenen-editor.md` §7 | – | nachziehen |

Empfehlung: **Die ersten beiden Befunde vor dem Editor-Umbau klären.** Entweder Pipelines sind Cluster-Singletons (dann entfällt
der lokale Spawn und der Start kommt vom Cluster-Actor), oder sie sind bewusst je Knoten (dann gehört das als Eigenschaft in die
Signatur, etwa als Marker, und der Editor zeigt es). Heute ist es beides zugleich.

## 9 · Simulation (später)

Pipelines in die Editor-Simulation aufnehmen: Ein ⚡-Knopf je Trigger-Record (Felder als Eingabe, wie bei Commands), eine
**virtuelle Uhr** für ↺ und ⏳ (vorspulen), Commands laufen in die vorhandene Aggregat- und Saga-Simulation. Dienste und
Fähigkeiten brauchen dafür Doubles aus dem Editor (Dienst-Knoten mit Code-Insel). Das zeigt, was ein Trigger in der Domäne
auslöst, bevor Code läuft.

## 10 · Umsetzung in Phasen

| Phase | Inhalt | Voraussetzung |
|---|---|---|
| **0** | Laufzeit-Befunde §8 entscheiden (Singleton vs. je Knoten, Idempotenz ab Trigger, Event-Ausgang, Doppel-Trigger) | – |
| **1** | Board-Darstellung: Kanal-Symbole ▶ ◆ ◇ ⚡ ↺, Garantie-Strich an allen Pipeline-Kanten, Start als Framework-Eingang, Form-Zeile | – |
| **2** | Ausgänge vervollständigen: `veröffentlicht` (transient), `Frist`/`FristStorno` als Ausgang, Frist-Knoten migrieren, Modus „Frist“ entfernen | 1 |
| **3** | Guardrails §6 im Editor (Picker/Validator) + Analyzer-Vorschläge | 1 |
| **4** | Ingress als Domänen-Code-Fakt (Timer/Datei/Webhook) + Dienst-Bindung → schreib- und lesbar | 0 |
| **5** | Zeit-Spur + Simulation mit virtueller Uhr | 2 |

## 11 · Offene Entscheidungen (mit Empfehlung)

1. **Pipeline: Cluster-Singleton oder je Knoten?** Empfehlung: Cluster-Singleton je PipelineId (Trigger landen ohnehin dort,
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
