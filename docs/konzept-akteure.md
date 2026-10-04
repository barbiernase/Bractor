# Konzept: Akteure

> Status: **umgesetzt** (2026-10-03) — Laufzeit (Generator, Tor am Handshake, Analyzer), Extractor/Editor-Modell, Akteur-Band im
> Editor. Abweichungen vom ersten Entwurf: §3.1 (Beantworten = `IDarf<Query>`), Umsetzungs-Landkarte §6.
> Ziel: Der Domänen-Editor soll DDD tragen — dazu fehlt die Rolle der **Akteure**: *Wer darf was entscheiden?*

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

