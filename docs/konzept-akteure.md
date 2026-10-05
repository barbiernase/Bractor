# Konzept: Akteure

> Status: **umgesetzt** (2026-10-03) — Laufzeit (Generator, Tor am Handshake, Analyzer), Extractor/Editor-Modell, Akteur-Band im
> Editor. Abweichungen vom ersten Entwurf: §3.1 (Beantworten = `IDarf<Query>`), Umsetzungs-Landkarte §6.
> Ziel: Der Domänen-Editor soll DDD tragen — dazu fehlt die Rolle der **Akteure**: *Wer darf was entscheiden?*
>
> **Neu 2026-10-05: §9 „Akteur-Verträge" (Konzept, nicht umgesetzt)** — je Akteur ein C#-Vertrag, aus dem die Client-Schnittstellen
> (Python, Blazor) generiert werden; schließt die Lücke „woher wird das ausgelöst?" an den externen Akteuren.
>
> **Revision 2026-10-05 (§8 umgesetzt inkl. Laufzeit-Kette — §8.7):** §7 (Befund + Prüfung) und **§8 „Akteure als Domänen-Experten"**. Ein Dienst
> ist kein Akteur mehr. Akteure sind fachliche Experten (KameraSystem, Klassifizierer, Inspekteur, KIOperator, …), ein Command kann
> mehrere haben; deklariert wird nur `IDarf`, der Akteur eines Ketten-Commands folgt aus der Kette. §8 ersetzt §3.3b sowie §7.3/§7.4.

## 1. Befund (Ist-Zustand)

**Editor.** Es gibt keinen Akteur. Einziger Stellvertreter für Menschen und Fremdsysteme ist der Pseudo-Knoten
**„Außenwelt (Client)"** — abgeleitet: ein Command/eine Query ohne internen Erzeuger bekommt die Außenwelt als Quelle
(`DomainEditor/Fluss.cs:163-177`, `DomainEditor/Grammatik.cs:153`). Wer etwas auslöst, sieht man nie.

**Programmiermodell.**
- `CommandEnvelope.UserId` ist ein `string` mit Default `"system"`; kein Client setzt ihn, Marten persistiert ihn
  nicht (`MartenEventBatchWriter.cs` speichert nur Causation/Correlation), `Decide(cmd)` sieht ihn nie.
- Der generierte `ProjectionQueryService` baut einen leeren `QueryEnvelope` (`UserId = "anonymous"`).
- Keine Authentifizierung, keine Rollen, keine Mandanten (vgl. `docs/konzept-client-haertung.md` §4, P1-2).

**Capabilities-Handshake** (`Infrastructure/GrpcClient/CapabilitiesHandler.cs`, `ProtoRepo/domain.proto:47-52, 107-116`).
Beim `Connect` meldet jeder Client an, welche Typen er braucht (`message_types`, `handle_queries`, `handle_triggers`).
Der Server sortiert nur ein und antwortet mit `allowed_commands`, `supported_queries`, `allowed_triggers`, ….
- **Selbstauskunft ohne Durchsetzung:** `AllowedCommands` wird berechnet und zurückgeschickt, aber
  `HandleCommandAsync` prüft nie dagegen.
- Blazor nutzt den Alt-Pfad (nur `event_types`); der Server leitet „erlaubte Commands" als Geschwister-Commands
  desselben Aggregats ab (`MessageTypeMapping.cs:70-90`).
- Keine Identität: die Session ist ein Zähler (`session-0001`).

## 2. Leitlinie: so wenig Begriffe wie möglich

Verworfen: ein Akteur mit fünf Relationen (`ISendet`/`IFragt`/`IHört`/`IBeantwortet`/`IVerarbeitet`) — das sind
Wire-Begriffe, keine Fachsprache, und zu viel mental load. Im Event Modeling *löst* ein Akteur etwas aus und *sieht*
etwas; die fachliche Frage dahinter ist nur: **Wer darf das?**

Daraus:
- **Ein Begriff:** `IAkteur`. Keine Trennung Mensch/System — ein Fremdsystem ist ein Akteur mit Namen.
- **Ein Wort:** `IDarf<T>` — für **alles, was ein Akteur in das System hineingibt**.
- **Alles andere wird aus dem Graphen abgeleitet.**

## 3. Programmiermodell

```csharp
public sealed record Disponent : IAkteur,
    IDarf<SetzeModellAktiv>, IDarf<RegistriereModell>, IDarf<HoleModelle>;

public sealed record KlassifikationsWorker : IAkteur,
    IDarf<KlassifiziereBildPaarDurchKi>;

public sealed record Teamleiter : Disponent, IDarf<ArchiviereModell>;   // Vererbung = Rollen-Hierarchie
```

Passt zu den bestehenden Regeln: Akteur = **Typ** (Inv. 3), Relation = **Code-Fakt** in der Signatur (Extractor findet
sie ohne Namensraten), Muster wie das Store-Bündel (`: IStore, IFähigkeitA, …`). Der Command bleibt reiner Vertrag.

### 3.1 Was `IDarf<T>` abdeckt (alles, was hineingeht)

| Typ | Bedeutung |
|---|---|
| Command | darf auslösen |
| Query | darf fragen — bzw. als Zuständiger beantworten (nur Lücke, nur einer, §3.2) |
| Trigger (Client startet Pipeline) | darf starten — bzw. als Zuständiger verarbeiten (nur Lücke, nur einer) |
| Transient-Event (Hinweis vom Client) | darf veröffentlichen |

Umgesetzt anders als zuerst gedacht: **Beantworten** ist `IDarf<Query>`, nicht `IDarf<Response>` — eine extern beantwortete
Query hat im Graphen keinen Reader, also keine Query→Response-Kante, an der ein Response-Recht hängen könnte. Damit bleibt
es bei EINER Lesart: `IDarf<T>` = „T darf über mich ins System" (als Fragender oder als Zuständiger). Analyzer CQRS058
lässt nur Command/Query/Trigger/Transient zu; ein Event aus dem Log oder eine Response kann man nicht „dürfen".

**Regel ohne Ausnahme:** Ohne `IDarf` darf es niemand — auch bei Queries. Der Editor zeigt die Lücken.

### 3.2 Was abgeleitet wird

**Hören (Event-Abo).** Ein Akteur hört, was er ohnehin wissen darf:

| Weg im Graphen | Lesart |
|---|---|
| `IDarf<Cmd>` → Aggregat des Commands → dessen Events | „Du hörst das Ding, auf das du einwirkst" (inkl. Rückmeldung/Ablehnung) |
| `IDarf<Query>` → Reader → Projektion → deren Trigger-Events | „Du hörst, was du sowieso lesen darfst" |

Hör-Menge = Vereinigung. Kein Leck über das hinaus, was der Akteur fragen oder bewirken darf. Weg 1 liegt schon
generiert vor (`GeneratedCommandRouting.CommandToAggregate`/`CommandToEvents` — der Blazor-Alt-Pfad nutzt ihn
umgekehrt); Weg 2 steht im Graphen, muss generiert werden. Grenze: Filter je Event-*Typ*, nicht je Datensatz (wie bei
Queries).

**Zuständigkeit (`handle_queries` / `handle_triggers`).** Ein externer Client darf nur übernehmen, was
- im Graphen eine **Lücke** ist (Query ohne Reader, Trigger ohne Pipeline) — einen Server-Reader kann niemand kapern;
- noch **niemand** übernommen hat (Kardinalität `eins`, GR-QUERY/GR-TRIGGER);
- und er den Typ selbst darf (`IDarf<Query>` bzw. `IDarf<Trigger>`).

Rest-Risiko (bewusst): Ein berechtigter Worker, der sich zuerst als Zuständiger für einen Trigger einträgt, könnte ihn
verschlucken — er gewinnt dadurch kein Recht, das er nicht schon hat.

### 3.3 Handshake: von Selbstauskunft zu Befugnis

```
Connect + Token im gRPC-Header (kein Proto-Feld)
   │
   ▼  Composition Root: Token → Akteur-Typ            (einzige String-Stelle, Betrieb — nicht Domäne)
   │      services.AddAkteure(a => a.Token<Disponent>(geheim));   bzw. Konfig "Akteure:Tokens"
   ▼  GeneratedAkteurRechte[Disponent]                 (generiert aus IDarf<…> + Ableitungen §3.2)
   │
CapabilitiesResponse: allowed_commands / supported_queries / allowed_triggers / subscribed_events
                      = genau diese Mengen            (alle Felder existieren schon)
Session merkt sie sich → HandleCommand/Query/Trigger prüfen O(1) → sonst CommandFailed (existiert)
```

- Die Geschwister-Ableitung des Alt-Pfads entfällt.
- Python: `_declared_command_types` wird überflüssig (höchstens Abgleich „ich will, was ich nicht darf").
- Bonus: Blazor kennt nach dem Connect `allowed_commands` → kann Buttons ausblenden.
- Envelope: die Session stempelt den Akteur in `UserId` (umgesetzt). **Noch offen:** als Marten-Header persistieren
  (neben `aggregate_type`) und über `EmitKausalität`/`HandlerOutputRouter` weiterreichen („Automation X im Auftrag von Y").

### 3.3b Dienste als Akteure (die KI)

Ein Dienst kann Akteur sein — die KI entscheidet, wie ein Bildpaar klassifiziert wird. Gleiches Wort, kein neues Konzept:

```csharp
public interface IClassifierService : IAkteur, IDarf<KlassifiziereBildPaarDurchKi> { Task<ClassificationResult> ClassifyPairAsync(Guid id); }

public async IAsyncEnumerable<OneOf<KlassifiziereBildPaarDurchKi>> Handle(
    ImagePairKomplett evt, PipelineContext ctx, IClassifierService ki)   // ← im Auftrag der KI: steht in der Signatur
```

- **Wer entscheidet, steht in der Signatur:** der Dienst kommt als Handle-Parameter (wie eine Fähigkeit, aus der DI aufgelöst),
  nicht über den Konstruktor (Fähigkeiten statt Rumpf). Trennlinie: ein Dienst, der **fachlich entscheidet**, ist ein Akteur
  (Parameter); ein **Werkzeug** (`IImageResizer`, `IHistogramEqualizer`) bleibt im Konstruktor.
- **Build (CQRS060):** höchstens ein Akteur je Handle; der Handle gibt nur Commands aus, die der Akteur darf; ein Akteur-Dienst
  im Konstruktor/Feld eines Konsumenten ist ein Fehler. Nur am Pipeline-Handle (CQRS057).
- **Laufzeit:** der generierte Dispatch setzt `ImAuftrag.Von("IClassifierService")` um den Handle-Aufruf (fluss-lokal,
  `AsyncLocal`); `CommandEmitter` stempelt ihn als `UserId` — auf dem Pull- wie auf dem Actor-Pfad, ohne die Idempotenz (CommandId)
  zu berühren. Ohne Akteur bleibt es `system` (reine Automation).
- **Rechte-Tabelle:** der Vertrag ist der Akteur (`GeneratedAkteurRechte["IClassifierService"]`), seine Implementierung nicht.
- **Ein Akteur, zwei Verkörperungen:** dieselbe Fähigkeit „klassifizieren" gibt es heute draußen (Python-Worker, Record-Akteur
  `KlassifikationsWorker` am Tor) und drinnen (Dienst-Akteur in der Pipeline) — beide dürfen `KlassifiziereBildPaarDurchKi`.
- **Editor:** Dienst-Akteure stehen im Akteur-Band (Karte „⚙ Dienst"), mit „entscheidet in ▶" → Pipeline-Handle; am Handle
  „◀ im Auftrag von 👤 X" (⊕ = nur Dienst-Akteure leuchten, ✕ = lösen); Validator **GR-AUFTRAG** = CQRS060. Board-Feld
  `handles[].akteur`; „C# schreiben" setzt/entfernt den Parameter; ein bestehender Vertrag (mit Methoden) gehört dem Code —
  nur seine Basisliste wird abgeglichen, ein neuer wird `public interface X : IAkteur, IDarf<…> { }`.

### 3.4 Wenn das „Wer" fachlich ist

Beispiel Vier-Augen-Prinzip („Freigeber ≠ Antragsteller"): das „Wer" ist dann Domäne und steht **explizit als Feld**
in Command und Event — normale Modellierung, kein neues Konzept. (Option für später: ein typisierter
`Akteur<T>`-Parameter an `Decide`, vom generierten Dispatch aus der Session gefüllt.)

## 4. Editor

- **Ein neuer Knoten „Akteur"** — ersetzt die anonyme Außenwelt (die bleibt als „unbekannter Akteur", sichtbar markiert).
- **Eine Kante:** Akteur → Command/Query/Trigger. Im Panel am Command als **„Wer?"**: ⊕ → Akteure leuchten →
  anklicken = verbinden; der `DateiSchreiber` ergänzt additiv `IDarf<X>` am Akteur-Record.
- **Grammatik** (eine Quelle, `Grammatik.cs`): `akteur` erzeugt Command, Query, Trigger, Transient (**GR-AKTEUR**, Build:
  CQRS058/059 + Tor). **GR-AKTEUR-FEHLT** (Warnung, nur wenn es Akteure gibt): Command/Query/Trigger ohne internen Erzeuger
  und ohne Akteur — am Tor käme es nie durch.
- **Persona-Sicht:** Klick auf den Akteur → Slice: alles, was er auslösen kann, und was daraus folgt.

### 4.1 Eigenes Akteur-Band

Akteure bekommen ein **eigenes Band** — ein Block wie „⋯ Geteilt" (`SHARED_KEY`), nur mit eigenem Schlüssel
(`AKTEUR_KEY = "§akteure"`, Label „👤 Akteure") und eigener Rollen-Tabelle `ROLE_AKTEUR`. Das bestehende Layout bleibt:
das Band kommt **dazu**, es ersetzt keine Spalte und keinen Block.

```
┌ Domäne ImagePair ──────────────────────────────────────────────────────────────────────┐
│ ┌ 👤 Akteure ┐   ┌ Aggregat ImagePair ─────────────────────────────┐   ┌ ⋯ Geteilt ──┐ │
│ │ Disponent  │──▶│ Command │ Decider │ State │ Event │ … │ Reader  │   │ Saga │ …    │ │
│ │ KI-Worker  │──▶│                                                  │   │              │ │
│ └────────────┘   └──────────────────────────────────────────────────┘   └──────────────┘ │
└────────────────────────────────────────────────────────────────────────────────────────┘
```

- **Position: ganz links** — vor den Aggregat-Blöcken, also auf der Eingangsseite. Das Spalten-Lesen bleibt
  links→rechts (Akteur → Command → … → Reader), wie im Event Modeling „Akteur oben/vorn".
- **Spalten im Band** (`ROLE_AKTEUR`): zunächst genau eine — `akteur`. Platz für später: eine zweite Spalte
  „Bildschirm" (Brücke zum Blazor-Client), erst wenn das Frontend in den Editor kommt.
- **Domänen-Zugehörigkeit aus dem Graphen** (wie `domKey`): Liegen alle `IDarf`-Ziele eines Akteurs in einer Domäne,
  steht er im Akteur-Band **dieser** Domäne. Wirkt er über mehrere Domänen, steht er in einem Akteur-Band **auf
  oberster Ebene** (außerhalb der Domänen-Rahmen, ganz links). Ein Akteur ohne `IDarf` → oberste Ebene.
- **Kanten:** Akteur → Command/Query/Trigger laufen aus dem Band nach rechts in die Command-/Query-Spalten der Blöcke.
- **Rahmen-Menüleiste** wie bei den anderen Spalten (`.gspalte`, Kopf ＋/◎): ＋ legt einen neuen Akteur an.
- **„Außenwelt"** wandert ebenfalls in dieses Band — als graue Karte „unbekannter Akteur", solange Commands ohne
  Akteur existieren (sichtbarer Befund statt stiller Ableitung).
- **Laden-Filter / Domänen-Filter:** das Band gehört zur Domäne wie alles andere (`domKey`), damit „nur ImagePair
  laden" die Akteure dieser Domäne mitlädt.
- **Simulation (später):** in SimHost „als Disponent ausführen" — nicht erlaubte Commands werden abgelehnt.

## 5. Bewusst nicht Teil

- **Mandanten** — eigene Achse, eigenes Konzept.
- **Datensatz-Rechte** („nur eigene Datensätze") — Domäne (§3.4) bzw. Reader-Filter.
- **Transport-Sicherheit** (TLS, Token-Quelle OIDC/mTLS) — `docs/konzept-client-haertung.md` T1.

## 6. Umsetzungs-Landkarte (2026-10-03)

| Teil | Ort |
|---|---|
| Vertrag | `Abstractions/Akteur.cs` (`IAkteur`, `IDarf<T>`) |
| Befugnis-Tabelle (generiert) | `Infrastructure.SourceGeneration/AkteurRechteGenerator.cs` → `GeneratedAkteurRechte` (CQRS059) |
| Tor | `Infrastructure/Akteure/` — `AkteurRechte`, `AkteurOptionen` + `AddAkteure(...)`, `AkteurTor` (Erkennen, Handshake-Filter, `AkteurVerweigerung`) |
| Durchsetzung | `Infrastructure/GrpcClient/CqrsClientService.cs` — Token-Header `akteur-token` am `Connect` (sonst `Unauthenticated`), Capabilities gefiltert, Command → `CommandFailed`, Query/Transient → `AKTEUR_DARF_NICHT`, Trigger → negatives Ack, Subscribe gefiltert, `UserId` = Akteur |
| Opt-in | `Host.Grpc/Program.cs`: `AddAkteure(Configuration.GetSection("Akteure"))` — ohne Sektion offen wie bisher |
| Clients | Blazor `GrpcProxy.AkteurToken` (`Akteur:Token`), Python `GrpcProxy(akteur_token=…)` / `CQRS_AKTEUR_TOKEN` |
| Analyzer | `Domain.SourceGeneration/AkteurAnalyzer.cs` (CQRS058) |
| Beispiel-Akteure | `Domain.Projections/Akteure.cs` (Inspektor, Trainer, KlassifikationsWorker, TrainingsWorker); Dienst-Akteur `IClassifierService` (KI) in `Domain.Pipeline/ImageProcessing` |
| Dienste als Akteure | `Abstractions.ImAuftrag`, `PipelineDispatchGenerator` (Akteur-Parameter), `CommandEmitter` (UserId), `AkteurAnalyzer` CQRS060, `HandlerFormAnalyzer` (CQRS057 erlaubt den Parameter am Pipeline-Handle) |
| Extractor / Modell | `DomainExtractor.ReadAkteur`, `EditorModell.Akteure`, `Herkunft`, `ModellMapper`, Inventar in `ParitaetsPruefung` |
| Sprache | `Grammatik` (Baustein `akteur`, GR-AKTEUR, GR-AKTEUR-FEHLT), `Fluss` (`akt:`), `Module` (Akteur = benannte Außenwelt), `Validator` |
| Schreiben | `Scaffolder.AkteurDatei` (neu: `Akteure.cs` im Namespace-Verzeichnis), `DateiAbgleich.AkteurBefugnisse` (Basisliste nach Modell, auch Entziehen) |
| Editor | `HtmlPresenter.cs`: Akteur-Band (`AKTEUR_KEY`), Akteur-Karte (darf ⊕/✕, „hört" abgeleitet), `darf`-Port an Command/Query/Trigger, „◀ kommt aus" nennt Akteure, Persona-Slice |
| Tests | `Akteure/AkteurRechteTests`, `Analyzers/AkteurAnalyzerTests`, `DomainEditorAkteurTests`; Sonde (`Theke`, gezeichnete `Mahnstelle`) |

**Offen:** echte Authentifizierung hinter dem Token (TLS/OIDC, `konzept-client-haertung.md` T1); `Akteur<T>`-Parameter an
`Decide` (§3.4); Bildschirm-Spalte im Band; die Python-Worker melden sich noch ohne Token an (opt-in: Server ohne Akteure).



## 7. Revision: Rollen statt Dienst-Akteure (Entwurf 2026-10-05)

> Auftrag (Tobi): Der Dienst ist nicht der Akteur. Es gibt drei **Rollen** — **Classifier** (die KI), **Maschine** (erzeugt die
> Bilder), **Mensch** (kümmert sich um die Commands). Erst prüfen, ob man beim Dienst klar sagen kann, *wer* ihn benutzt; dann ein
> System, das Commands, Pipeline-Trigger und alles Weitere vom Akteur her verzahnt.

### 7.1 Nachvollzug: wer löst heute was aus?

Alle Commands der Domäne, ihr tatsächlicher Erzeuger im Code und ihr heutiger Stempel (`UserId`):

| Command | Erzeuger heute (Code-Fakt) | Stempel heute | Rolle (Soll) |
|---|---|---|---|
| `ErstelleImagePair`, `MeldeBildVerfuegbar` | `ImageProcessingPipeline.Handle(DateiErkannt)` ← `FileWatchPipeline` (Datei auf dem Share) | `system` | **Maschine** (über Arm) |
| `KlassifiziereBildPaarDurchKi` | **zweimal**: `ImageProcessingPipeline.Handle(ImagePairKomplett, …, IClassifierService)` *und* Python `classifier.py on_image_pair_komplett` | `IClassifierService` bzw. `KlassifikationsWorker` | **Classifier** |
| `KlassifiziereEinzelBildDurchKi` | niemand (Lücke) | — | Classifier |
| `LabelBildPaar`, `LabelPhysischesProdukt`, `MarkiereAlsInspiziert` | Blazor (`LabelingIntentHandler`, `DataEffects.razor`) | Token-Akteur bzw. `system` | **Mensch** |
| `LabelEinzelBild`, `LabelBildRegion` | niemand (Lücke) — `Inspektor` darf `LabelEinzelBild`, Blazor sendet es nie | — | Mensch |
| `ErstelleDatensatz`, `FuegeRangeHinzu`, `SetzeSplit`, `FriereEin`, `NimmPaarAuf`, `EntfernePaar` | Blazor (`DatensatzKomposition…`, `Kuratieren…`) | Token-Akteur | **Mensch** |
| `NimmRangeAuf`, `SchliesseEinfrierenAb` | `DatensatzResolverPipeline` (auf `RangeAngefordert`/`EinfrierenAngefordert`) | `system` | Mensch (über Arm) |
| `StarteTraining`, `BricheTrainingAb`, `RegistriereModell`, `SetzeModellAktiv`, `ArchiviereModell` | Blazor (`TrainingDashboard…`, `Modell…`) | Token-Akteur | **Mensch** |
| `MeldeTrainingBegonnen/Fortschritt/Abgeschlossen/Gescheitert` | Python `training_worker.py` | `TrainingsWorker` | **offen** (§7.6) |
| `MarkiereAlsHaengengeblieben` | Frist aus `TrainingFristPipeline` | `system` | offen (§7.6) |
| `StarteSammelvorgang`, `StarteTeilauftrag`, `MeldeTeilFertig` | Lücke bzw. `TeilFertigProzess` | `system` | Beispiel-Domäne, unverändert |

**Befunde**

1. **Der Dienst steht an der Stelle der Rolle.** `IClassifierService : IAkteur, IDarf<…>` — die KI *ist* der Vertrag. Dieselbe
   Fähigkeit gibt es ein zweites Mal als Record `KlassifikationsWorker`. Zwei Akteure für eine Rolle.
2. **Doppelte Klassifikation.** C#-Pipeline und Python-Worker reagieren beide auf `ImagePairKomplett` und senden beide
   `KlassifiziereBildPaarDurchKi`. Der C#-`ClassifierService` ist ein Platzhalter (immer `KeineAnomalie`). Läuft der Python-Worker,
   wird jedes Paar zweimal klassifiziert — einmal mit Fantasie-Label.
3. **Die Maschine kommt nicht vor.** Die ganze Bild-Kette (`DateiErkannt` → `ErstelleImagePair`, `MeldeBildVerfuegbar`) läuft als
   `system`; niemand darf `DateiErkannt` (`IDarf` fehlt).
4. **Die Menschen-Rollen passen nicht zum Client.** Der *eine* Blazor-Client (ein Token = ein Akteur) sendet 14 Commands; `Inspektor`
   deckt davon 1 ab (`MarkiereAlsInspiziert`, dafür das nie gesendete `LabelEinzelBild`), `Trainer` 5. Mit eingeschaltetem Tor
   würden `LabelBildPaar`, `LabelPhysischesProdukt`, `FuegeRangeHinzu`, `SetzeSplit`, `FriereEin`, `NimmPaarAuf`, `EntfernePaar`,
   `BricheTrainingAb` und `RegistriereModell` abgelehnt.
5. **Der Akteur reißt nach dem ersten Schritt ab.** `MartenEventBatchWriter` schreibt nur `aggregate_type` als Header; `UserId`
   landet nicht im Log. Jede Automation dahinter (`DatensatzResolverPipeline`, Prozesse, Fristen) stempelt `system` — der Mensch,
   der den Datensatz eingefroren hat, ist an `SchliesseEinfrierenAb` nicht mehr zu sehen.

### 7.2 Prüfung: Kann man beim Dienst klar sagen, wer ihn benutzt?

**Heute: halb.**

- *Vom Handle aus* ja: der Dienst ist ein Handle-Parameter (Code-Fakt), der Generator kennt jede Stelle. Heute gibt es genau eine:
  `ImageProcessingPipeline.Handle(ImagePairKomplett, …, IClassifierService)`.
- *Vom Dienst aus* nein:
  - Der Dienst nennt keine Rolle — er *ist* eine (Befund 1).
  - CQRS060 verbietet den Dienst nur im Ctor/Feld von **Konsumenten** (Subscriber/Reader/Pipeline). Jede andere Klasse (ein
    Blazor-Effect, ein Hosted Service, ein Domänen-Service) kann ihn über DI injizieren, und niemand sieht es. Die Menge der
    Benutzer ist also nicht geschlossen.
  - Der Python-Worker benutzt den Dienst gar nicht, trifft aber dieselbe Entscheidung. Im Graphen hängen die beiden nicht zusammen.

**Mit zwei Regeln: ja, vollständig aus Code-Fakten.**

1. Der Dienst nennt **seine Rolle** in der Basisliste: `interface IClassifierService : IBenutztVon<Classifier>`.
2. Ein Rollen-Dienst darf **nur als Handle-Parameter** auftreten. Ctor-, Feld- oder Property-Injektion ist in *jeder* Klasse ein
   Fehler (CQRS060 auf alle Typen ausgeweitet). Ausgenommen ist nur die Composition Root (`services.Add…`).

Damit gilt: *Wer benutzt den Dienst?* → die Rolle (deklariert) und genau die Handles mit diesem Parameter (abgeleitet,
abgeschlossen). *Wer entscheidet in diesem Handle?* → die Rolle des Dienst-Parameters.

### 7.3 Modell: drei Begriffe (überholt durch §8)

| Begriff | Code | Bedeutung |
|---|---|---|
| **Rolle** (= Akteur) | `record Classifier : IAkteur, IDarf<…>` | einziger Akteur-Typ; trägt die Befugnisse |
| **Dienst einer Rolle** | `interface IClassifierService : IBenutztVon<Classifier>` | Werkzeug, mit dem die Rolle *drinnen* entscheidet; selbst kein Akteur, kein `IDarf` |
| **Verkörperung** | Token → Rolle (Tor) · Rollen-Dienst (Handle-Parameter) · Ingress eines Triggers, den die Rolle darf | *wie* eine Rolle hineinkommt — Betrieb, nicht Domäne |

```csharp
namespace Domain.Akteure;

public sealed record Mensch : IAkteur,
    IDarf<LabelBildPaar>, IDarf<LabelPhysischesProdukt>, IDarf<LabelEinzelBild>, IDarf<MarkiereAlsInspiziert>,
    IDarf<ErstelleDatensatz>, IDarf<FuegeRangeHinzu>, IDarf<SetzeSplit>, IDarf<FriereEin>, IDarf<NimmPaarAuf>, IDarf<EntfernePaar>,
    IDarf<NimmRangeAuf>, IDarf<SchliesseEinfrierenAb>,                 // über den Arm DatensatzResolver
    IDarf<StarteTraining>, IDarf<BricheTrainingAb>, IDarf<RegistriereModell>, IDarf<SetzeModellAktiv>, IDarf<ArchiviereModell>,
    IDarf<SucheImagePairs>, IDarf<GetImagePair>, /* … alle Queries des Clients … */;

public sealed record Maschine : IAkteur,
    IDarf<DateiErkannt>,                                               // über den Arm FileWatch
    IDarf<ErstelleImagePair>, IDarf<MeldeBildVerfuegbar>;              // über den Arm ImageProcessing

public sealed record Classifier : IAkteur,
    IDarf<KlassifiziereBildPaarDurchKi>, IDarf<KlassifiziereEinzelBildDurchKi>;

// Domain.Pipeline — der Dienst gehört der Rolle, er IST sie nicht:
public interface IClassifierService : IBenutztVon<Classifier> { Task<ClassificationResult> ClassifyPairAsync(Guid id); }
```

`Inspektor`/`Trainer` gehen in `Mensch` auf. Eine Rollen-Hierarchie per Vererbung (§3) bleibt möglich, wenn der Mensch später
aufgeteilt werden soll. `KlassifikationsWorker` entfällt, der Python-Worker meldet sich mit dem Token der Rolle `Classifier` an.

### 7.4 Verzahnung: was hineinkommt, kommt von einem Akteur (Regeln übernommen in §8.3)

**Invariante (DDD):** Jeder Command, jede Query und jeder Trigger hat einen Akteur, also eine Rolle mit `IDarf<T>`. Das gilt ohne
Ausnahme: Es gibt kein `system` und keine Automation ohne Akteur. Pipeline, Prozess, Frist und Dienst sind **nie** Akteur. Sie sind
der **Arm**, über den eine Rolle ihren Command ins System bringt.

**Eine Quelle für das Wer: `IDarf`.** Der Akteur eines Commands ist die Rolle, die ihn darf, egal ob er übers Tor oder über einen
Arm kommt. Nichts wird vererbt, nichts geraten:

| Hineingehend | Weg | Akteur |
|---|---|---|
| `LabelBildPaar`, `FriereEin`, `StarteTraining`, … | Tor (Blazor) | Mensch |
| `NimmRangeAuf`, `SchliesseEinfrierenAb` | Arm `DatensatzResolverPipeline` | Mensch (der Resolver führt *seinen* Datensatz-Auftrag aus) |
| `DateiErkannt` (Trigger) | Arm `FileWatchPipeline` (Sensor am Share) | Maschine |
| `ErstelleImagePair`, `MeldeBildVerfuegbar` | Arm `ImageProcessingPipeline.Handle(DateiErkannt)` | Maschine |
| `KlassifiziereBildPaarDurchKi` | Tor (Python-Worker) **oder** Arm mit `IClassifierService`, nicht beides | Classifier |
| `MeldeTraining*`, Query `HoleDatensatzSamples` | Tor (Python-Trainings-Worker) | offen (§7.6) |
| `MarkiereAlsHaengengeblieben` | Arm Frist (`TrainingFristPipeline`) | offen (§7.6) |

**Regeln (Build + Editor, eine Quelle in `Grammatik`):**

1. **Herkunft:** Jeder Command, jede Query und jeder Trigger hat mindestens eine Rolle mit `IDarf`, sonst „kommt von niemandem“.
   Heute betrifft das `LabelBildRegion`, `KlassifiziereEinzelBildDurchKi`, `StarteSammelvorgang`, `StarteTeilauftrag`, `BenchPing`,
   die Bild-Kette und die Resolver-/Frist-Commands.
2. **Ein Arm, eine Rolle:** Alles, was ein Handle ausgibt, darf **dieselbe** eine Rolle. Daraus folgt die Rolle des Handles. Ein
   Handle, der Maschinen- und Classifier-Commands mischt, ist ein Fehler.
3. **Ein Weg hinein:** Ein Command kommt entweder direkt (Tor, nur wenn er im Graphen eine Lücke ist) oder über einen Arm.
   - Hat er einen Arm, lehnt das Tor ihn ab, auch für die berechtigte Rolle. Der Mensch kann `SchliesseEinfrierenAb` nicht am
     Resolver vorbei mit erfundener Mitgliederliste schicken.
   - Die doppelte Klassifikation (Befund 2) ist damit ein Build-Fehler statt ein Laufzeit-Unfall.
4. **Dienst passt zur Rolle:** Nimmt ein Handle einen Rollen-Dienst (`IBenutztVon<R>`), muss `R` die Rolle des Handles aus
   Regel 2 sein. *Wer benutzt den Dienst?* bleibt damit vollständig beantwortet (§7.2).

**Was nicht hineinkommt, braucht kein `IDarf`:** Events sind Folgen *im* Aggregat, keine Eingänge. Ein Arm, der über
Store-Fähigkeiten liest (`ISearchImagePairs` im Resolver), stellt keine Query. Was eine Rolle hört, bleibt abgeleitet (§3.2).

Durchgespielt, jede Spur beginnt bei einer Rolle:

```
Maschine   ─ DateiErkannt ─▶ [FileWatch]   ─▶ [ImageProcessing] ─ ErstelleImagePair, MeldeBildVerfuegbar ─▶ ImagePair ─▶ ImagePairKomplett
Classifier ─ hört ImagePairKomplett ─ (Python-Worker | [Arm + IClassifierService]) ─ KlassifiziereBildPaarDurchKi ─▶ ImagePair
Mensch     ─ FriereEin ─▶ Datensatz ─▶ EinfrierenAngefordert ─▶ [DatensatzResolver] ─ SchliesseEinfrierenAb ─▶ Datensatz
Mensch     ─ LabelBildPaar / MarkiereAlsInspiziert ─▶ ImagePair
```

**Laufzeit:** Der Generator schreibt eine Tabelle `Command/Trigger → Rolle` (aus `IDarf`, eindeutig für Arm-Ausgaben nach Regel 2).
Der `CommandEmitter` stempelt die Rolle aus der Tabelle, das Tor die Rolle des Tokens. Eine Kausalketten-Vererbung über Event-Header
ist dafür **nicht** nötig. Der Header `akteur` im Log bleibt nur als Audit („wer hat das bewirkt“), optional. Der Fachcode sieht
nichts davon (Inv. 5).

### 7.5 Editor

- **Akteur-Band = Rollen-Band:** nur noch Rollen-Karten. Ein Dienst hängt als „⚙ IClassifierService" **unter** seiner Rolle
  („benutzt von Classifier"), nicht als eigene Akteur-Karte.
- **Rollen-Spuren:** jeder Command, Trigger, jede Query und jeder Arm trägt die Farbe seiner Rolle. Über das Tor ist er voll
  gefüllt, über einen Arm gestrichelt. Das ist die Event-Modeling-Swimlane, abgeleitet statt gezeichnet.
- **Persona-Slice:** Klick auf *Maschine* zeigt Datei → Paar → komplett, dort beginnt die Classifier-Spur.
- **Grammatik:** `GR-HERKUNFT` (Regel 1, ersetzt die Warnung GR-AKTEUR-FEHLT als Fehler), `GR-ARM-ROLLE` (Regel 2 + 4),
  `GR-EIN-WEG` (Regel 3).

### 7.6 Offene Entscheidungen (fachlich, nicht technisch)

1. **Classifier drinnen oder draußen?** Empfehlung: **draußen** (Python-Worker, Token `Classifier`). Den C#-Platzhalter-Handle
   streichen, bis es einen echten In-Process-Classifier gibt. Sonst wird jedes Paar doppelt klassifiziert. Das Modell trägt beides,
   aktiv sein darf nur eins.
2. **Wem gehört der Trainings-Worker?** Er meldet `MeldeTraining*` und fragt `HoleDatensatzSamples`. Er ist ML, aber kein
   Classifier. Optionen: in die Rolle `Classifier` (KI = eine Rolle) oder eine vierte Rolle.
2b. **Wem gehört die Frist `MarkiereAlsHaengengeblieben`?** Nach der Invariante braucht auch sie eine Rolle. Kandidaten: der
   Mensch (er hat das Training gestartet und will nicht ewig warten) oder dieselbe Rolle wie der Trainings-Worker.
3. **Mensch als eine Rolle** (wie der eine Blazor-Client) oder aufgeteilt (Inspektor/Trainer, zwei Tokens)? Empfehlung: erst eine,
   denn Aufteilen ist später per Vererbung additiv.
4. **Identität ≠ Rolle:** *welcher* Mensch (Login) ist eine eigene Achse (OIDC, `konzept-client-haertung.md` T1). Der Header
   `akteur` trägt die Rolle; eine spätere `person` kommt daneben.

### 7.7 Umsetzungsschritte

| # | Schritt | Ort |
|---|---|---|
| 1 | `IBenutztVon<TRolle>` einführen; `IClassifierService` umhängen; Rollen `Mensch`/`Maschine`/`Classifier` statt `Inspektor`/`Trainer`/`KlassifikationsWorker` | `Abstractions/Akteur.cs`, `Domain.Projections/Akteure.cs`, `Domain.Pipeline/ImageProcessing/IClassifierService.cs` |
| 2 | Analyzer: Rollen-Dienst nur als Handle-Parameter (alle Typen), Rolle eindeutig, Dienst ohne `IDarf`; Ausgaben ⊆ `IDarf(Rolle)` | `AkteurAnalyzer` (CQRS060 erweitert) |
| 3 | Generator: Dienste nicht mehr als Akteure; Tabelle `Command/Trigger → Rolle`; Regeln 1–4 als Build-Diagnosen | `AkteurRechteGenerator`, `AkteurAnalyzer` |
| 4 | Stempel: `CommandEmitter`/`HandlerOutputRouter` aus der Tabelle; Tor lehnt Arm-Commands ab (Regel 3); optional Audit-Header `akteur` | `Infrastructure/PubSub`, `Infrastructure/Akteure`, `MartenEventBatchWriter` |
| 5 | Extractor/Modell/Grammatik/Editor: Rollen-Band, Dienst unter Rolle, Rollen-Spuren, zwei neue Regeln | `DomainExtractor`, `EditorModell`, `Grammatik`, `HtmlPresenter`, Sonde + `soll.txt` |
| 6 | Tests: Prüfstand (Regeln 1–4, Tabelle, Stempel, Tor-Ablehnung); Integration (Kette Mensch → Resolver trägt Mensch) | `Akteure/*Tests`, Integration |


## 8. Akteure als Domänen-Experten (umgesetzt 2026-10-05)

> Auftrag (Tobi): Mehr Akteure, als Domänen-Experten entworfen, nicht „Maschine/Mensch/KI“ als Sammeltopf. Die Bilder kommen vom
> **KameraSystem**, die KI ist der **Klassifizierer**, Datensätze erzeugt der **KIOperator**, gelabelt wird vom **Inspekteur**.
> Gesucht: weitere Akteure und eine deklarative, typbasierte Umsetzung.

### 8.1 Akteur-Katalog (aus der Fachsprache des Codes)

Die Domäne selbst nennt die Akteure schon in Kommentaren. `ImagePair/Commands.cs` gliedert in drei **Stränge**: „KI klassifiziert
Kamerabilder“, „Mensch labelt Kamerabilder“ und „Mensch labelt **physisches Produkt**“. `Datensatz/Commands.cs` sagt über
`NimmRangeAuf`/`SchliesseEinfrierenAb`: „Wird vom Resolver ausgelöst, **nie von der GUI**“. Diese Regel steht heute nur im Kommentar.

| Akteur | Art | Fachliche Aufgabe | gibt selbst hinein (`IDarf`) | wird in seinem Namen erzeugt (Kette, abgeleitet) |
|---|---|---|---|---|
| **KameraSystem** | Maschine | nimmt Bildpaare (dc0/dc2) an der Linie auf | `DateiErkannt` (Ingress `FileWatchPipeline`) | `ErstelleImagePair`, `MeldeBildVerfuegbar` (über `BildEingangPipeline`) |
| **Klassifizierer** | KI | beurteilt Bildpaare/Einzelbilder mit dem aktiven Modell | `KlassifiziereBildPaarDurchKi`, `KlassifiziereEinzelBildDurchKi`, `HoleAktivesModell` | — (Python-Worker am Tor) |
| **Inspekteur** | Mensch | befundet Kamerabilder (Strang 2) | `LabelBildPaar`, `LabelEinzelBild`, `LabelBildRegion`, `MarkiereAlsInspiziert`, `SucheImagePairs`, `GetImagePair`, `GetImagePairHistorie`, `GetImagePairStatistik`, `GetProduktionsTage`, `GetUnklassifizierteImagePairs` | — |
| **Produktprüfer** *(neu gefunden)* | Mensch | prüft das **physische Teil** und liefert damit die Ground Truth (Strang 3), nicht am Bildschirm | `LabelPhysischesProdukt`, `GetImagePair`, `SucheImagePairs` | — |
| **KIOperator** | Mensch | stellt Datensätze zusammen, kuratiert, startet und bricht Trainings ab, registriert Modelle | `ErstelleDatensatz`, `FuegeRangeHinzu`, `NimmPaarAuf`, `EntfernePaar`, `SetzeSplit`, `FriereEin`, `StarteTraining`, `BricheTrainingAb`, `RegistriereModell`, `HoleDatensaetze`, `HoleDatensatz`, `HoleDatensaetzeFuerPaar`, `HoleTrainingslaeufe`, `HoleTrainingslauf`, `HoleModelle`, `SucheImagePairs` | `NimmRangeAuf`, `SchliesseEinfrierenAb` (Resolver), `MarkiereAlsHaengengeblieben` (Frist) |
| **TrainingsSystem** *(neu)* | Maschine | GPU-Worker, der trainiert und Fortschritt meldet | `MeldeTrainingBegonnen`, `MeldeFortschritt`, `MeldeTrainingAbgeschlossen`, `MeldeTrainingGescheitert`, `HoleDatensatzSamples` | — (Python-Worker am Tor) |
| **Modellfreigeber** *(Vorschlag)* | Mensch | schaltet ein Modell für die Produktions-Inferenz scharf oder zieht es zurück | `SetzeModellAktiv`, `ArchiviereModell`, `HoleModelle`, `HoleAktivesModell` | — |
| Disponent *(Beispiel-Domäne)* | Mensch | startet Sammelvorgänge/Teilaufträge | `StarteSammelvorgang`, `StarteTeilauftrag` | `MeldeTeilFertig` (`TeilFertigProzess`) |

Begründungen für die Funde:
- **Produktprüfer ≠ Inspekteur:** Der Code trennt die Stränge bewusst. Das Label am physischen Produkt ist die Wahrheit, gegen
  die Bild-Labels und KI gemessen werden. Wer am Bildschirm befundet, darf diese Wahrheit nicht setzen.
- **Modellfreigeber:** `SetzeModellAktiv` wechselt laut Kommentar das Modell, das der Inferenz-Worker lädt, also das, was
  in der Produktion entscheidet. Das ist eine Freigabe-Entscheidung und ein typischer Vier-Augen-Kandidat (Trainieren ≠
  Freigeben). Wer das nicht will, gibt die zwei Rechte dem KIOperator.
- **TrainingsSystem ≠ Klassifizierer:** Der eine trainiert, der andere urteilt. Es sind zwei Prozesse mit verschiedenen Rechten.
- **Die Frist trägt den, der das Training gestartet hat** (heute der KIOperator). Sie ist sein Wecker, nicht der Akteur.
- **Benchmark (`BenchPing`)** ist Technik, keine Domäne. Er bekommt einen technischen Akteur `Lasttest` (Art Maschine) oder fällt
  aus der Herkunfts-Regel heraus, weil die Benchmark-Pipeline nur im Lasttest-Host registriert ist.

### 8.2 Deklarativ und typbasiert: `IDarf` reicht, der Rest folgt aus dem Graphen

> Zwischenstand verworfen (Tobi: „brauchen wir IHandeltFuer wirklich?“). `IHandeltFuer<A>` an Pipelines legt einen Arm auf
> **einen** Akteur fest. Kann ein Command von zwei Akteuren kommen, ist das falsch: Friert der KIOperator *oder* der Modellfreigeber
> einen Datensatz ein, handelt der Resolver mal für den einen, mal für den anderen. Der Akteur eines Arms ist also kein
> Klassen-Fakt, sondern folgt aus der Kette.

**Deklariert wird nur der Akteur selbst:**

```csharp
// Abstractions/Akteur.cs
public interface IAkteur { }
public interface IMensch : IAkteur { }  public interface IMaschine : IAkteur { }  public interface IKi : IAkteur { }
public interface IDarf<T> { }           // was der Akteur selbst hineingibt (Tor ODER Ingress drinnen)

// Domain.Akteure — ein Command darf bei mehreren Akteuren stehen
public sealed record KameraSystem   : IMaschine, IDarf<DateiErkannt>;
public sealed record Klassifizierer : IKi,      IDarf<KlassifiziereBildPaarDurchKi>, IDarf<KlassifiziereEinzelBildDurchKi>, IDarf<HoleAktivesModell>;
public sealed record Inspekteur     : IMensch,  IDarf<LabelBildPaar>, IDarf<MarkiereAlsInspiziert>, IDarf<FriereEin> /* … */;
public sealed record KIOperator     : IMensch,  IDarf<ErstelleDatensatz>, IDarf<FriereEin>, IDarf<StarteTraining> /* … */;
//                                                          ▲ FriereEin von ZWEI Akteuren — nichts weiter zu tun
```

Pipelines, Prozesse und Fristen bekommen **keine** Markierung.

**Wer ist der Akteur eines Commands?** Drei Fälle, in dieser Reihenfolge:

| Fall | Akteur | Beispiel |
|---|---|---|
| 1. Kommt durchs **Tor** | der Akteur des Tokens; er muss `IDarf<T>` haben | `FriereEin` vom Blazor-Client: Inspekteur *oder* KIOperator, je nach Token |
| 2. Gibt ein **Arm in einer Kette** aus (Handle auf ein Event, Prozess, Frist) | **der Akteur des auslösenden Events**, geerbt; braucht kein `IDarf` | `SchliesseEinfrierenAb` trägt den, der `FriereEin` geschickt hat |
| 3. Gibt ein **Arm ohne Kette** aus (Ingress: `Selbst<PollTick>`, Timer, Boot) | **der** Akteur mit `IDarf<T>`; genau einer, sonst Build-Fehler | `DateiErkannt` aus `FileWatchPipeline` → KameraSystem |

Jede Kette beginnt damit bei einem Akteur (Fall 1 oder 3) und trägt ihn weiter (Fall 2). So kommt alles von einem Akteur, und
`system` gibt es nicht.

**Mehrere Akteure je Command gehen in allen drei Fällen:**
- **Tor:** Mehrere Akteure stehen mit `IDarf<FriereEin>` da, und das Token entscheidet.
- **Kette:** Der Arm ist neutral. Statisch hat `SchliesseEinfrierenAb` die Akteur-*Menge* {Inspekteur, KIOperator}, zur Laufzeit
  genau den einen aus der Kette.
- **Direkt und über eine Kette:** Ein Inspekteur darf `ErstelleImagePair` per Hand hochladen (`IDarf`), und die KameraSystem-Kette
  erzeugt es ebenfalls. Beides ist korrekt gestempelt.

**„Nie von der GUI“ ohne Sonderregel:** `NimmRangeAuf` dürfen niemand per `IDarf`. Das Tor lehnt es ab, und es entsteht nur als
Ausgabe der Kette. Wer einen Ketten-Command doch direkt erlauben will, schreibt `IDarf` dazu. Das ist dann eine sichtbare
Entscheidung.

**Einzige Ausnahme: die Entscheidung *eines anderen* Akteurs mitten in der Kette.** Der In-Process-Klassifizierer läuft in einer
Kette, die beim KameraSystem beginnt. `KlassifiziereBildPaarDurchKi` ist aber die Entscheidung des Klassifizierers. Hier reicht die
Kette nicht. Der Akteur-Wechsel steht dort, wo er passiert, nämlich am Dienst:

```csharp
public interface IClassifierService : IAkteurDienst<Klassifizierer> { … }   // Handle(…, IClassifierService ki) ⇒ Akteur = Klassifizierer
```

Damit ist auch beantwortet, wer den Dienst benutzt (§7.2): der Klassifizierer, und zwar genau an den Handles mit diesem Parameter.
Bleibt der Klassifizierer draußen (Python-Worker am Tor, Empfehlung §7.6), entfällt auch dieses Wort. Dann gibt es nur `IDarf`.

**Mensch ≠ Person:** Eine Person kann mehrere Akteure verkörpern (Blazor: Inspekteur **und** KIOperator). Die Composition Root
bildet künftig ein Token auf eine Akteur-*Menge* ab (`a.Token<Inspekteur, KIOperator>(…)`). Das Tor prüft gegen die Vereinigung
und stempelt den Akteur, dessen `IDarf` passt (bei mehreren: den ersten in der Konfiguration oder einen, den der Client wählt).

### 8.3 Regeln (eine Quelle `Grammatik`, Build = Analyzer/Generator, Editor = Validator)

| # | Regel | Prüft |
|---|---|---|
| 1 | **Herkunft:** Jeder Command, jede Query und jeder Trigger hat statisch eine nicht-leere Akteur-Menge (Fixpunkt §8.4). | Lücken wie `LabelBildRegion` oder `StarteSammelvorgang` ohne Akteur fallen auf. |
| 2 | **Ingress eindeutig:** Eine Ausgabe ohne Kette (Fall 3) hat genau einen Akteur mit `IDarf`. | Sonst ist offen, wer `DateiErkannt` liefert. |
| 3 | **Akteur-Dienst:** `IAkteurDienst<A>` ist nur Handle-Parameter (nie Ctor/Feld, in keiner Klasse), höchstens einer je Handle, und die Ausgaben ⊆ `IDarf(A)`. | Geschlossen ist damit, wer den Dienst benutzt (bisher CQRS060). |
| 4 | **Eine Verkörperung je Entscheidung:** Ein Akteur darf auf dasselbe Event nicht über zwei Wege dieselbe Entscheidung treffen. | Die doppelte Klassifikation (Befund 2) wird zum Build-Fehler. |
| 5 | *optional* **Art:** `record SetzeModellAktiv(…) : ICommand, IVerlangt<IMensch>`; die ganze Akteur-Menge muss diese Art haben. | Die KI kann sich nicht selbst freigeben, auch nicht über eine Kette. |

### 8.4 Was abgeleitet wird

- **Akteur-Menge, statisch (Generator/Extractor), Fixpunkt:**
  `A(Cmd) = {IDarf-Halter, falls übers Tor} ∪ ⋃ A(Handle, das Cmd ausgibt)`;
  `A(Handle) = {A} bei IAkteurDienst<A>, sonst A(Eingangs-Event), ohne Kette: IDarf-Halter (Regel 2)`;
  `A(Event) = ⋃ A(Cmd), deren Decide es liefert` (`GeneratedCommandRouting.CommandToEvents`).
  Ergebnis in `GeneratedAkteurRechte`: je Akteur **„bewirkt“** (Commands, die in seinem Namen entstehen können).
- **Akteur, zur Laufzeit (genau einer, Kausalkette wie Correlation):**
  1. `MartenEventBatchWriter` schreibt den Akteur als Header `akteur`, `MartenEventStore` liest ihn in den Envelope.
  2. Der generierte Dispatch (Pipeline, Prozess, Reaktion) setzt `ImAuftrag.Von(envelope.Akteur)` um den Handle bzw.
     `ImAuftrag.Von(A)` bei einem `IAkteurDienst<A>`-Parameter.
  3. Eine `Frist` speichert den Akteur beim Planen mit und feuert in seinem Namen.
  4. Ein Prozess-Join (wartet auf mehrere Events) nimmt den Akteur des Events, das die Regel feuern lässt.
  5. `CommandEmitter` stempelt `ImAuftrag.Akteur`. Ist er leer, gilt Fall 3 aus der generierten Tabelle; gibt es auch dort keinen,
     ist das nach Regel 1/2 schon im Build aufgefallen.
- **hört:** wie bisher (§3.2).
- **Editor:** eine Swimlane je Akteur, gruppiert nach Art. Ein Command mit mehreren Akteuren erscheint als **eine** Karte mit
  Akteur-Chips, nicht doppelt. Direkt (Tor) ist voll gefüllt, über die Kette gestrichelt.

```
Maschine │ KameraSystem    ─ DateiErkannt ▸ [FileWatch] ▸ [BildEingang] ─ ErstelleImagePair, MeldeBildVerfuegbar ─┐
KI       │ Klassifizierer  ◂ hört ImagePairKomplett ─ KlassifiziereBildPaarDurchKi ──────────────────────────────────┤ ImagePair
Mensch   │ Inspekteur      ─ LabelBildPaar, MarkiereAlsInspiziert ───────────────────────────────────────────────────┤
Mensch   │ Produktprüfer   ─ LabelPhysischesProdukt ─────────────────────────────────────────────────────────────────┘
Mensch   │ KIOperator      ─ ErstelleDatensatz, FriereEin ▸ Datensatz ▸ [Resolver] ┄ SchliesseEinfrierenAb (KIOperator)
         │                 ─ StarteTraining ▸ Trainingslauf ▸ [TrainingFrist] ┄ MarkiereAlsHaengengeblieben (KIOperator)
Maschine │ TrainingsSystem ◂ hört TrainingAngefordert ─ MeldeTraining* ▸ Trainingslauf
Mensch   │ Modellfreigeber ─ SetzeModellAktiv ▸ Modell ─▸ (Klassifizierer hört ModellAktiviert)
```

### 8.5 Umsetzungsschritte

| # | Schritt | Ort |
|---|---|---|
| 1 | `IMensch`/`IMaschine`/`IKi`, `IAkteurDienst<A>` (nur falls In-Process-KI bleibt), optional `IVerlangt<T>` | `Abstractions/Akteur.cs` |
| 2 | Akteur-Katalog; `IClassifierService` umhängen oder den C#-Platzhalter streichen; `ImageProcessingPipeline` teilen (Bild-Eingang ≠ Klassifikation) | `Domain.Projections/Akteure.cs`, `Domain.Pipeline/*` |
| 3 | Fixpunkt + Regeln 1–5 als Diagnosen; Tabelle „Ingress → Akteur“ und „bewirkt“ | `AkteurRechteGenerator`, `AkteurAnalyzer` |
| 4 | Kausalkette: Header `akteur` schreiben/lesen; Dispatch setzt `ImAuftrag`; `Frist` trägt Akteur; Token → Akteur-Menge | `MartenEventBatchWriter`, `MartenEventStore`, Pipeline-/Prozess-Dispatch, `Fristplan`, `AkteurOptionen`/`AkteurTor` |
| 5 | Extractor/Modell/Grammatik/Editor: Akteur-Art, Akteur-Mengen, Swimlanes mit Chips, Regeln; Sonde + `soll.txt` | `DomainExtractor`, `EditorModell`, `Grammatik`, `HtmlPresenter` |
| 6 | Clients: Tokens (Classifier → Klassifizierer, Training → TrainingsSystem, Blazor → Inspekteur+KIOperator) | `appsettings`, `GrpcProxy` |
| 7 | Tests: Prüfstand (Fixpunkt, Regeln, Tor lehnt Ketten-Commands ab); Integration (Header-Round-trip; `FriereEin` von zwei Akteuren → `SchliesseEinfrierenAb` trägt jeweils den richtigen) | `Akteure/*Tests`, Integration |

### 8.6 Folgen für die Commands: keine

- **Kein Command, kein Event und kein Proto-DTO ändert sich.** Der Akteur reist im Envelope und im Event-Header, nicht im Command
  (Inv. 5, §3.4). Eine Proto-Regenerierung ist nicht nötig.
- **`…DurchKi` bleibt.** Das Suffix sieht nach einem Akteur im Namen aus, ist aber Fachlichkeit: Der Decider führt getrennte
  Stränge mit eigenen Events (`BildPaarDurchKiKlassifiziert` ≠ `BildPaarGelabelt`). Ein KI-Urteil ist ein anderes Faktum als ein
  Befund. Würde man die Stränge zu *einem* `LabelBildPaar` zusammenlegen und den Akteur entscheiden lassen, bräuchte `Decide` den
  Akteur. Das wäre genau das, was §3.4 nur für fachliches „Wer“ erlaubt.
- **Nur Kommentare:** „nie von der GUI“ (`NimmRangeAuf`, `SchliesseEinfrierenAb`) und „GUI-Command“ werden zu Typ-Fakten (fehlendes
  bzw. vorhandenes `IDarf`) und können weg.
- **Einzige mögliche Änderung, optional:** Regel 5 (`IVerlangt<IMensch>`) stünde in der Basisliste von z. B. `SetzeModellAktiv`. Ist
  das „Wer“ selbst fachlich (Vier-Augen: Freigeber ≠ Trainierender), gehört es als Feld in Command und Event (§3.4). Beides ist eine
  bewusste Domänen-Entscheidung je Command, keine Voraussetzung für das Akteur-Modell.

### 8.7 Umsetzung (2026-10-05)

| Teil | Ort | Stand |
|---|---|---|
| Arten + Dienst eines Akteurs | `Abstractions/Akteur.cs`: `IMensch`, `IMaschine`, `IKi`, `IAkteurDienst<TAkteur>` | ✅ |
| Katalog | `Domain.Pipeline/Akteure.cs` (zieht von `Domain.Projections` um — sieht jetzt auch `DateiErkannt`): KameraSystem, TrainingsSystem, Klassifizierer, Inspekteur, Produktpruefer, KIOperator, Modellfreigeber, Disponent | ✅ |
| Dienst ist kein Akteur | `IClassifierService : IAkteurDienst<Klassifizierer>`; Generator zählt nur Records; CQRS058 meldet ein Interface mit `IAkteur`; CQRS060 verbietet den Dienst in Ctor/Feld **jeder** Klasse | ✅ |
| Art in der Rechte-Tabelle | `AkteurRechte.Art` | ✅ |
| Ableitung direkt/Kette | `DomainEditor/AkteurAnteile.cs` (Fixpunkt auf `Fluss`), Board `rahmen.akteurMengen`, JS-Spiegel + `deAkteurParitaet()` (127 Nachrichten, gleich) | ✅ |
| Regeln | `GR-HERKUNFT` (ersetzt GR-AKTEUR-FEHLT), `GR-INGRESS-EINDEUTIG`, `GR-AUFTRAG` (Dienst → Akteur); `--check` nennt die Lücken (heute nur `BenchPing`) | ✅ (Build-Gegenstück offen) |
| Extractor/Modell/Scaffolder | `Akteur.Art`, `Akteur.Dienste`; `handles[].akteur` = Akteur (nicht Dienst); Scaffolder schreibt `record X : IMensch, IDarf<…>`; Sonde: `Gutachterin : IKi` + `IGebuehrenOrakel : IAkteurDienst<Gutachterin>` | ✅ |
| Editor | `docs/konzept-domaenen-editor.md` §12 | ✅ |
| Laufzeit-Kette: Log | `IEventStoreRepository.AppendEventsAsync(…, akteur)` → Header `akteur` (Einzel- und Batch-Append, `ImAuftrag.Header`); `ReadStreamAsync` → `EventEnvelope.UserId`; `AggregateActorBase` gibt `cmdEnvelope.UserId` mit | ✅ (Integrationstest `MetadataPostgresTests.Der_Akteur_reist_als_Header…` geschrieben, mangels Docker nicht gelaufen) |
| Laufzeit-Kette: Weitergabe | `Infrastructure/Akteure/AkteurHerkunft.cs`; Pipeline-Pull (`PipelineEventPullBridge`), transiente Events, Selbst-Nachrichten (Akteur beim Planen gemerkt), Reaktionen (`HandlerOutputRouter`), Fristen (`Frist.Akteur`, `FristScheduler`), Prozesse (Auslöser-Akteur mit `ProzessGestartet` geloggt, `FeuereAsync` im Auftrag) | ✅ |
| Ingress ohne Kette | Trigger kommen ohne Envelope → der eine Akteur mit `IDarf<Trigger>` (`EindeutigerHalter`), ebenso ein Command ohne Kette im `CommandEmitter` | ✅ |
| Token → Akteur-Menge | `AkteurOptionen.Token<A,B>(…)` bzw. Konfig `"Inspekteur,KIOperator"`; `AkteurRechte.Vereinige` + `AkteurFuer(Typ)` → der Envelope trägt den Teil-Akteur, der den Typ darf | ✅ |
| C#-Platzhalter-Klassifizierer | `IClassifierService`, `ClassifierService`, `ImageProcessingPipeline.Handle(ImagePairKomplett)` + `Handle(PaarNichtKomplett)` **gelöscht** — klassifiziert wird nur draußen (Python-Worker, Akteur Klassifizierer) | ✅ |

**Abweichungen vom Entwurf:**
- **Prozess = Akteur seines Auslösers:** ein Prozess-Command trägt den Akteur des Events, das den Prozess startete (ein Join
  über Events mehrerer Akteure bleibt beim Auslöser) — so muss das Marking keine Akteure je Token führen.
- **Trigger aus einer Kette** (eine Pipeline yieldet einen Trigger an eine andere): Trigger reisen ohne Envelope; ihr Akteur ist
  der eindeutige IDarf-Halter. Ein Ketten-Trigger, den niemand darf, trägt deshalb `system` (im Bestand nicht der Fall).
- **Die Frist `MarkiereAlsHaengengeblieben` trägt das TrainingsSystem**, nicht den KIOperator: abgeleitet aus der Kette
  `MeldeTrainingBegonnen` (TrainingsSystem) → `TrainingBegonnen` → TrainingFristPipeline.
- **Inspektor/Trainer → Inspekteur/KIOperator**; der Blazor-Client braucht ein Token für Inspekteur+KIOperator(+Modellfreigeber).
  Das Tor bleibt opt-in (`Host.Grpc` ohne Sektion `Akteure` = offen), die Python-Worker melden sich noch ohne Token an.
- `IAkteurDienst<A>` bleibt als Sprachmittel (Analyzer, Generator, Editor, Sonde), wird im Bestand aber nicht mehr benutzt.


## 9. Akteur-Verträge: generierte Schnittstellen für die Clients (Konzept 2026-10-05, nicht umgesetzt)

> Auftrag (Tobi): „Wir sollten eine Art Interface für die Clients erzeugen, gegen das die Clients (Python, Blazor …) programmieren."
> Anlass: die Lücken-Prüfung (§9.1) — überall dort, wo ein Akteur DRAUSSEN auf ein Event reagiert, bricht die sichtbare Kette ab.

### 9.1 Befund

**Die Kette ist innen geschlossen, an den externen Akteuren offen.** Diagnose im Editor (`deHerkunft()`) über alle 130 Nachrichten:

| Event | „geht an ▶" im Editor | was wirklich passiert (nur im Python-Code) | „◀ kommt aus" des Folge-Commands |
|---|---|---|---|
| `ImagePairKomplett` | nur Projektionen | `classifier.py on_image_pair_komplett` → `KlassifiziereBildPaarDurchKi` | „Akteur Klassifizierer · darf" |
| `TrainingAngefordert` | nur Projektion | `training_worker.py on_training_angefordert` → `MeldeTrainingBegonnen/Fortschritt/Abgeschlossen/Gescheitert` | „Akteur TrainingsSystem · darf" |
| `TrainingAbgebrochen` | nur Projektion | `training_worker.py on_training_abgebrochen` (bricht ab) | — |
| `BildVerfuegbar` | nur Projektionen | `classifier.py on_bild_verfuegbar` (merkt Pfade) | — |
| `ModellAktiviert` | nur Projektion | der Klassifizierer soll das neue Modell laden (Kommentar an `SetzeModellAktiv`) | — |

**Der Client erklärt selbst, was er ist** — der Server kann es nur hinnehmen oder (mit Tor) beschneiden:
- Python: `HandleDescriptor` registriert per Type-Hint (`@handle.register`), was der Client empfängt; `_declared_command_types` listet von
  Hand, was er sendet; `_build_capabilities_request` schickt beides als `message_types`/`handle_queries`/`handle_triggers`.
- Blazor: die IntentHandler geben `IEnumerable<object>` zurück (`LabelingIntentHandler`) — WAS ein Bildschirm auslösen kann, steht
  nirgends im Typ.
- „hört" ist am Akteur nur grob abgeleitet (alle Events der Aggregate seiner Commands + Projektionen seiner Queries), nicht das, worauf
  er tatsächlich reagiert.

**Reaktionen von außen sind nicht idempotent:** der Python-Client schickt einen Command mit frischer CommandId (`CommandEnvelopeDto`
hat keine Kausalität) — wird ein Event erneut zugestellt (Reconnect, Replay), klassifiziert der Worker doppelt. Intern löst das die
deterministische Emit-Id (`EmitId.Ableiten` aus Auslöse-Position) — draußen fehlt sie.

### 9.2 Idee: der Vertrag steht in der Domäne, der Client implementiert ihn

**Je Akteur ein Vertrag in C#** — derselbe Ort und dieselbe Sprache wie alles andere (Code-Fakt in der Signatur, keine Namenskonvention).
Daraus **generiert** das Framework je Client-Sprache eine Basis/Schnittstelle, gegen die der Client programmieren MUSS. Der Server
kennt damit jede Reaktion vorher, der Editor zeigt sie, der Handshake prüft sie, und ein Bruch fällt beim Bauen des Clients auf.

```
          Domäne (C#, eine Quelle)                       generiert                       Client programmiert dagegen
 record Klassifizierer : IKi, IDarf<…>         ┌─► Python: KlassifiziererBasis(ABC)  ──►  class Classifier(KlassifiziererBasis)
 interface IKlassifizierer :                   │
     IAkteurVertrag<Klassifizierer>  ──────────┼─► Blazor: IInspekteurClient (C#)   ──►  LabelingIntentHandler : …
   { OneOf<…> Auf(ImagePairKomplett e); … }    │
                                               └─► Server: Vertrags-Tabelle (Handshake, Tor, Editor-Graph)
```

### 9.3 Programmiermodell (C#)

```csharp
namespace Domain.Akteure;

public sealed record Klassifizierer : IKi, IDarf<HoleAktivesModell>;      // spontan: was er von sich aus fragt/sendet

/// Worauf der Klassifizierer reagiert — und was er dann hineingibt (Ausgabe-Vertrag in der Signatur, wie Decide/Handle).
public interface IKlassifizierer : IAkteurVertrag<Klassifizierer>
{
    OneOf<KlassifiziereBildPaarDurchKi> Auf(ImagePairKomplett e);          // Reaktion mit Ausgang
    void Auf(BildVerfuegbar e);                                            // nur zur Kenntnis (Pfade merken)
    void Auf(ModellAktiviert e);                                           // nur zur Kenntnis (Modell neu laden)
}

public sealed record TrainingsSystem : IMaschine, IDarf<HoleDatensatzSamples>;
public interface ITrainingsSystem : IAkteurVertrag<TrainingsSystem>
{
    IAsyncEnumerable<OneOf<MeldeTrainingBegonnen, MeldeFortschritt, MeldeTrainingAbgeschlossen, MeldeTrainingGescheitert>>
        Auf(TrainingAngefordert e);                                        // Strom: mehrere Meldungen je Reaktion
    void Auf(TrainingAbgebrochen e);
}
```

- **Ein Wort neu:** `IAkteurVertrag<TAkteur>` (Abstractions). Methoden heißen `Auf(TEvent)`; der Rückgabetyp ist der Ausgabe-Vertrag
  (`void` / konkreter Command / `OneOf<…>` / `IAsyncEnumerable<OneOf<…>>` für Ströme) — wie CQRS050 für Handles.
- Optional später: `Beantworte(TQuery)` (der Akteur ist zuständig für eine Query-Lücke, heute `handle_queries`) und `Verarbeite(TTrigger)`
  (Trigger-Lücke, heute `handle_triggers`) — gleiche Regel, gleicher Mechanismus.
- **`IDarf` bleibt für SPONTANES** (der Mensch klickt, der Worker fragt von sich aus). Was ein Akteur als **Reaktion** hineingibt, steht
  in den `Auf`-Signaturen und wird NICHT doppelt als `IDarf` deklariert — die generierte Rechte-Tabelle vereinigt beides
  (`Befugt = IDarf ∪ Ausgaben(Vertrag)`), `Hoert = Eingänge(Vertrag)` (+ Projektionen hinter seinen Queries).
- **Menschen** haben meist keinen `Auf`-Teil, der Commands erzeugt (sie handeln spontan); ihr Vertrag beschreibt, was ihr Bildschirm
  bekommt (`void Auf(X)` = Aktualisierung) — für Blazor die Grundlage typisierter IntentHandler (§9.5b).
- **Ein Vertrag je Akteur** (Analyzer), ein Akteur ohne Vertrag ist erlaubt (rein spontan).

### 9.4 Regeln (Analyzer + Grammatik, je mit Build-Gegenstück)

| ID | Regel |
|---|---|
| CQRS061 | `IAkteurVertrag<A>`: höchstens einer je Akteur; nur `Auf(TEvent)` (bzw. später `Beantworte`/`Verarbeite`); `TEvent` ist ein Event aus dem Log oder ein Transient-Event; je Event höchstens ein `Auf`. |
| CQRS062 | Ausgabe-Vertrag: nur konkrete Commands (bzw. `OneOf` davon), nie `ICommand`/`object` (wie CQRS050). |
| GR-VERTRAG | Editor: Reaktions-Kante Event → Akteur → Command; geprüft wie oben. |
| GR-VERKOERPERUNG | Reagiert ein Akteur im Vertrag auf ein Event, darf nicht zusätzlich ein interner Arm DESSELBEN Akteurs (Dienst) auf dasselbe Event dieselbe Entscheidung treffen (jetzt sichtbar → prüfbar, §8.3 Regel 4). |

### 9.5 Generierung

Alles aus der EINEN Quelle (Vertrag + Akteur-Record), reflexionsfrei; die Generate liegen neben den bestehenden.

**(a) Server** — `AkteurRechteGenerator` erweitert: je Akteur `Vertrag` (Event → erlaubte Ausgaben), `Befugt`, `Hoert` exakt. Daraus
die Handshake-Antwort (ohne Selbstauskunft) und die Prüfung „dieser Command antwortet auf dieses Event".

**(b) Python** — `Cqrs.Codegen` (der Prepass, der schon `domain.proto` und die STJ-Kontexte erzeugt) schreibt zusätzlich
`Domain.Client.Worker.Python.ML/domain_client/generated/vertraege.py` (Drift-Gate wie `codegen.sh --check`):

```python
# GENERIERT aus Domain.Akteure.IKlassifizierer — nicht von Hand ändern
class KlassifiziererBasis(CqrsClient[S], ABC):
    AKTEUR: ClassVar[str] = "Klassifizierer"
    VERTRAG_HASH: ClassVar[str] = "…"                      # Drift-Erkennung am Handshake
    @abstractmethod
    async def auf_image_pair_komplett(self, e: ImagePairKomplettDto, ctx, state: S) -> AsyncIterator[KlassifiziereBildPaarDurchKiDto]: ...
    @abstractmethod
    async def auf_bild_verfuegbar(self, e: BildVerfuegbarDto, ctx, state: S) -> None: ...
    @abstractmethod
    async def auf_modell_aktiviert(self, e: ModellAktiviertDto, ctx, state: S) -> None: ...
```

- Die Basis verdrahtet den Dispatch selbst (Event-Typ → `auf_…`), baut den `CapabilitiesRequest` aus dem Vertrag (kein
  `@handle.register`, kein `_declared_command_types` mehr) und **prüft jedes Yield**: ein DTO außerhalb des Ausgabe-Vertrags → Fehler
  (zur Laufzeit; statisch zusätzlich per pyright/mypy über die Typ-Annotation).
- Fehlt eine Methode, lässt sich der Worker nicht instanziieren (ABC) — der Vertragsbruch fällt beim Start auf, nicht beim ersten Event.
- `classifier.py`, `training_worker.py`, `run_stub.py` erben von der Basis; die freie Registrierung bleibt für Clients ohne Vertrag
  (Übergang) erhalten.

**(c) Blazor/C#** — ein Generator in `Client.SourceGeneration`: ein Modul erklärt `: IAkteurClient<Inspekteur>`; seine IntentHandler
geben `IEnumerable<OneOf<…>>` statt `IEnumerable<object>` zurück, und der Analyzer prüft: Ausgaben ⊆ Befugt(Inspekteur). Refresh-
Handler für `Auf(X)` werden aus dem Vertrag erwartet. (Phase 2 — erst nach den Python-Workern.)

### 9.6 Handshake: „ich bin Vertrag X"

- `CapabilitiesRequest` bekommt `vertrag` (Akteur-Name) + `vertrag_hash` (Proto-Feld im Framework-Teil von `domain.proto`). Der Server
  nimmt die Fähigkeiten aus der generierten Tabelle — die Selbstauskunft (`message_types` …) wird ignoriert bzw. nur noch abgeglichen.
- **Mit Tor:** das Token muss den Vertrags-Akteur verkörpern (sonst `Unauthenticated`). **Ohne Tor:** der Vertrag sagt, wer da ist —
  besser als der Rückfall „eindeutiger IDarf-Halter" (§8.7).
- **Hash ungleich** (Client gegen alte Domäne gebaut) → Warnung + Ablehnung im strikten Modus; nennt die abweichenden Methoden.

### 9.7 Kausalität und Idempotenz für Reaktionen von außen

- Antwortet der Client auf ein Event, schickt die generierte Basis die **Auslöse-Position** mit (`causation_event_id` bzw.
  Stream + Version im `CommandEnvelopeDto`). Der Server leitet daraus wie beim internen Emit eine **deterministische CommandId** ab
  (`EmitId.Ableiten`, Diskriminator = Vertragsmethode + Index im Strom) und stempelt `CommandModus.Emittiert` → die Empfänger-Inbox
  dedupliziert. Doppelt zugestellt ≠ doppelt klassifiziert.
- Der Command trägt den Vertrags-Akteur als `UserId`; die Correlation des auslösenden Events reist mit → die Kette (§8.4) läuft durch
  den externen Akteur hindurch weiter.

### 9.8 Graph, Editor, Sprache

- **Extractor:** `IAkteurVertrag<A>` + `Auf`-Signaturen → `EditorModell.Akteure[].Vertrag[] = { Eingang, Ausgaenge[], Strom }`.
- **Fluss / AkteurAnteile:** Kante `msg:Event → akt:A → msg:Command` mit Handle = Event; die Ausgaben gehören A (Akteur-Wechsel wie beim
  Dienst). Damit ist `KameraSystem → … → ImagePairKomplett → Klassifizierer → KlassifiziereBildPaarDurchKi` eine geschlossene Kette.
- **Editor:** im Akteur-Rahmen eine Spalte **„Reaktion"** (je `Auf` eine Karte: ◀ Event · Ausgänge ▶); „geht an ▶" des Events nennt
  „Klassifizierer · Reaktion", „◀ kommt aus" des Commands „Klassifizierer · Reaktion auf ImagePairKomplett". „hört" am Akteur = exakt die
  Vertrags-Events. Entwerfen: ⊕ „+ Reaktion" am Akteur → Event wählen → Ausgänge wählen; „C# schreiben" schreibt die `Auf`-Methode ins
  Vertrags-Interface (additiv, wie Handles), danach Codegen → die Python-Basis bekommt die neue abstrakte Methode.
- **Grammatik:** neue Sorte-×-Baustein-Paare `Event → Akteur (Reaktion)` und `Akteur → Command (Reaktion)`; GR-VERTRAG.
- **Sonde:** frei benannter Vertrag in der Sonden-Domäne + `soll.txt`-Zeilen; `--check` vergleicht Vertrag ⇄ Board.

### 9.9 Phasen

| Phase | Inhalt | Beweis |
|---|---|---|
| 1 | `IAkteurVertrag<A>`, CQRS061/062, Verträge `IKlassifizierer`/`ITrainingsSystem`, Rechte-Tabelle (Befugt/Hoert/Vertrag) | Prüfstand: Analyzer + Tabelle |
| 2 | Extractor/Modell/Fluss/AkteurAnteile/Grammatik + Editor-Spalte „Reaktion", Sonde | `--check`, `--sonde`, `deHerkunft()`: keine offene Stelle mehr an den Workern |
| 3 | Python-Generierung in `Cqrs.Codegen` + Drift-Gate; Worker erben die Basis; Yield-Prüfung | Python-Tests (`tests/`), Worker starten gegen Host |
| 4 | Handshake `vertrag`/`vertrag_hash`, Tor-Prüfung, Kausalität → deterministische CommandId | Prüfstand (Tor, Id-Ableitung), Integration (doppelt zugestellt → einmal wirksam) |
| 5 | Blazor: `IAkteurClient<A>`, typisierte IntentHandler, Analyzer | Blazor-Build |

### 9.10 Offen / bewusst nicht

- Statische Typprüfung in Python (pyright im CI) — optional; die Laufzeit-Prüfung der Yields reicht für den Anfang.
- Versionierung mehrerer Vertragsstände parallel (alter Worker während Rollout) — erst Hash + Warnung, später ggf. Vertrags-Versionen.
- Ein Akteur mit zwei Verkörperungen (zwei Worker-Prozesse desselben Vertrags) ist erlaubt — Lastverteilung ist Betrieb, nicht Domäne.
