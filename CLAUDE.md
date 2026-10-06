# CLAUDE.md

Projektgedächtnis für Claude Code. Bewusst schlank — wird bei jeder Session geladen.
Volltexte liegen in `docs/` und werden bei Bedarf gelesen, nicht hier eingebettet.

> **Einstieg in die Doku:** `docs/README.md` (Wegweiser). Ist-Zustand: `docs/01-ueberblick.md` bis
> `docs/13-reifegrad-schulden-bewertung.md` (flach, kanonisch). Stärken/Schulden/offene Baustellen: `docs/13-reifegrad-schulden-bewertung.md`. Das „Warum":
> `docs/02-design-prinzipien.md`. Wie schreibe ich X (inkl. Prozess): `docs/10-entwickler-api.md`. Akteure/Verträge/Clients:
> `docs/konzept-akteure.md`. Editor: `docs/konzept-domaenen-editor.md`. Altes liegt in `docs/_archiv-2026-08-12/` (nur Historie).

## Was das Projekt ist

Selbstgebautes, signalbasiertes CQRS-/Event-Sourcing-Framework auf **Proto.Actor** (virtuelle
Cluster-Actors), **Marten/PostgreSQL** (Event-Store, einzige Wahrheit) und **Redis**
(abgeleiteter, nicht-autoritativer Versions-Index). Events werden geordnet und **genau einmal
wirksam** an Projektionen, Reaktionen, Prozesse und Pipelines zugestellt — ohne
Runtime-Reflection, alles über Typen geroutet, alles Dispatchende zur Compile-Zeit generiert.

## Die sechs Invarianten (jede Entscheidung leitet sich hieraus ab)

1. Die Wahrheit ist der Log. Ordnung/Vollständigkeit/Wiederholbarkeit kommen NUR aus dem
   Event-Store-Read.
2. Das Signal ist nur ein Weckruf: trägt nur `(StreamId, Version)`, darf verloren, doppelt,
   ungeordnet sein.
3. Routing über Typen — nie ein handgebauter Identitäts-String.
4. Keine Runtime-Reflection. Alles generiert.
5. Der Fachcode bleibt rein. Cursor, Signal, Ordnung, Exactly-once, Sharding,
   Prozess-Maschinerie tauchen im Entwickler-Code nie auf.
6. Persistent genau dann, wenn ein durabler Konsument abhängt. Verlierbares (Tick,
   UI-Feedback, Datei-Trigger) bleibt auf dem schnellen Kanal.

## Das tragende Bild: vier Konsumenten, eine Maschine

Projektion, Reaktion, Prozess und Pipeline sind vier durable Konsumenten, die **dieselbe**
store-agnostische Pull-/Signal-Schleife (`ProjectionAdapter`) nutzen. Kein zweiter Marker,
keine Taxonomie — der Unterschied fällt aus Ctor-Stores + Rückgabetypen:
- **Achse B (replaybar vs. emittierend):** `IProjectionTracker` (Co-Commit + Reset) vs.
  `IEmittentenCursor` (best-effort, kein Reset). Beide gesetzt → Ctor wirft.
- **Transport:** Signal (schnell) + Poll (30 s, Sicherheit) wecken dieselbe Cluster-Identität.
- **Emit:** genau ein Weg (`CommandEmitter`), erzwungen durch den Analyzer **CQRS020/021**.

## Aktueller Stand (Kern 2026-08-11, ergänzt 2026-10-06)

**Kern fertig und in sich konsistent:** Schreibseite, Konsum-Maschine (Projektion + Reaktion),
Prozess-Maschine (Event-Regel-DAG). **Feature-Strom geliefert:** Timer-/Webhook-Trigger,
Deadlines/Fristen (`IDbClock`), Monitoring (`/health`, `/monitoring/metrics`), Dead-Letter
(Read+Sink), Pipeline P6.1/P6.2 zerlegt. **Schreibpfad-Perf:** Group-Commit-Batching mit
parallelem Drain (+48 %), STJ-Serializer (opt-in), optionaler Version-Index. **Snapshots** voll
verdrahtet. **P5b Marking-Cursor geliefert:** der Prozess-Fold ist von O(N²) auf O(N) Stream-Reads
(nicht-autoritativer Tail-Cursor, `IProzessMarkingStore` + Marten-`ProzessMarkingDoc`, HOT-Cache je
Actor; Voll-Fold bleibt Fallback bei fehlendem/stale `RegelHash`). **Feuer-gerichtete Reads:** warm
liest der Manager nur die seit dem letzten Fold befeuerten Streams nach → auch die DB-Roundtrips O(N²)→O(N)
(gemessen echtes Postgres: bis 9× schnellere Wall-Clock bei N=60; mit Aggregat-Historie 5,5×). Kaltstart
faltet voll (Invariante 1). Äquivalenz + Sagas grün.

**Katalog-Funktionen im Prozess (2026-10-07):** `Rufe<F>` neben `Sende<Cmd>` — eine Funktion (`IFunktion`: ein `IAuftrag<F>` hinein,
OneOf-Ergebnis-Events heraus) wird wie ein Aggregat gerufen; kurz und lang gleich, nur `.Zeitlimit(…)` (für jeden Aufruf). Generierter
Dispatch (`FunktionsGenerator`, CQRS068/069), `FunktionsAusfuehrer` (Slots, Wiederholen, genau ein Ergebnis im Ausführungs-Stream),
Bindung `AddFunktion<F, Impl>` + Boot-Guard; Editor: Funktions-Karte, „Dann ƒ", ⏳. Doku `docs/10-entwickler-api.md` §10.5a.
Offen: Python-/externer Ausführer, Saga-DSL (Cqrs.Testing) und Simulation kennen `Rufe` noch nicht.

**Akteure, Verträge, Clients (2026-10-06):** Akteure mit Befugnis und Kette bis in den Event-Header; Akteur-Verträge (Zusagen
draußen) mit generierter Python-Basis, Handshake + Hash, deterministischer CommandId; Clients (`IClientVertrag`, n:m zu Akteuren) mit
Rechten = Vertrag ∩ Token; alles im Editor (Rahmen je Domäne × Akteur, Client-Rahmen mit Leitungen). Offen: durable Zustellung an
Clients (`docs/konzept-akteure.md` §9).

**Tests (echt gemessen): Prüfstand 320/320 (2026-10-07, in-memory, store-frei); Integration gegen echtes Marten/Consul/Redis,
sequentiell (voll gezählt zuletzt 2026-08: 33/33; der `SnapshotLive`-Cold-Boot-Flake ausgenommen).**

**Bewusst offen (Priorität):**
1. **Cross-Node/Multi-Node** — **Iteration 1 + 2 geliefert; Multi-Node-Block geschlossen.** Generierter,
   reflexionsfreier Wire-Serializer (`CqrsWireSerializer` + `WireSerializerGenerator` →
   `GeneratedWire`/`GeneratedWirePoly` über `CqrsWireJsonContext`, Marker `IWireMessage`) am
   `WithRemote`-Punkt, Boot-Check, Round-trip. Iter. 1 = Command-/Pull-Plane; Iter. 2 = PubSub-/
   Pipeline-/Prozess-Plane (`PID`-Converter via `PID.FromAddress`). **Bewiesen:**
   `TwoNodeCommandDispatchTests`, `TwoNodePubSubSignalTests`, `RemotePidDeliverySmokeTests`; cross-node
   Saga (`LoadHarness --mode saga`) + echter 3-Node-Container-Betrieb (`deploy-multinode/`,
   `docs/06-transport-multinode-betrieb.md`). Weg B robust via Re-Assert (`ClientSubscriptionReassertTests`).
   **Offener Folge-Schritt:** Cold-Start-Schema-Migrator (Übergabe: `docs/_archiv-2026-08-12/multi-node-schema-migrator-handoff.md`).
2. **P5b-Restfeinschliff (klein):** die `MarkingKompakt`-Größe ist für einen extremen Fan-out noch
   O(N) (Payloads je Vorgang); die volle Zähler+Bitset-Verdichtung (Konzept §4) bleibt optionaler
   Feinschliff. Der O(N²)→O(N)-Read-Gewinn (das eigentliche Problem) ist voll geliefert. Die
   Kompensations-`NächsteKompensationAsync` liest noch ab 0 (Fehlerpfad, nicht die Warm-Schleife).
3. **Schreibpfad-Perf** — paralleler Drain skaliert sublinear (`wait_event` offen).
4. **KlärungNötig-Pfad** korrekt-per-Konstruktion, aber ohne Testdeckung.

**Kleinere Schulden:** `DtoMapperGenerator` fragil (hartkodierte Enums, Encoding-Schäden);
`Reaktionsempfaenger`-Dedup-Menge (Domänen-Leak); Deadline-Primitiv nicht in einen Prozess
integriert; `CqrsFrameworkOptions` toter `[Obsolete]`-Typ.

## Konventionen

- Kommentare/Domäne auf Deutsch (Bestand konsistent halten).
- Neue Verträge → `Abstractions`; Marten/Infra → `Infrastructure`.
- Nichts mit Runtime-Reflection (Inv. 4). Neue Dispatch-Logik = Generator erweitern, nicht
  Handschalter.
- **Extractor/Editor erkennen nur Code-Fakten** (Marker, Attribute, Symbole) — nie Namenskonventionen oder
  Namespace-Raten. Stores: Fähigkeit = Interface `: IWriteStore`/`: IReadStore` mit GENAU EINER Funktion (CQRS051),
  Store = Bündel `: IStore, IFähigkeitA, …` (eine Klasse, Namen frei). Ingress-Methoden tragen `[Ingress(...)]`.
  Neues Konstrukt ⇒ Sonde (`GraphExtractor/Sonde/`) + `soll.txt` erweitern.
- **Fähigkeiten statt Rumpf-Analyse:** ein Handle (Projektion/Reader/Pipeline) bekommt die Store-Funktionen, die er
  benutzen darf, als Parameter (`Handle(evt, env, writer, IUpsertX store)`); der generierte Dispatch löst sie aus einem
  DI-Bereich auf. Kein Store/`IFristplan` in Ctor/Feld eines Konsumenten (CQRS054), kein `new …Store()` (CQRS055).
  Planen ist ein Ausgang: `Selbst<T>`, `Frist<TCmd>`, `FristStorno<TCmd>` im OneOf. Der Rumpf liefert dem
  Extractor NICHTS (keine Guards, keine Store-Aufrufe) — nur die Signatur zählt.
- **Grammatik = EINE Quelle:** Was sich im Editor womit verbinden lässt (Sorte × Eingang, Kardinalität, Zusatzregeln, je Regel ihr
  Build-Gegenstück) steht nur in `DomainEditor/Grammatik.cs`; Editor (`rahmen.grammatik`), Validator und `--check` lesen sie. Module
  = Namespaces, Ports abgeleitet (`DomainEditor/Module.cs`, nur Modell/Validator — Ebenen-Ansicht im Editor bewusst noch nicht).
- **Ausgabe-Vertrag in der Signatur (CQRS050):** Decide und jedes Handle geben einen konkreten Typ oder `OneOf<…>`
  konkreter Typen zurück (nie `ICommand`/`IEvent` …). WAS entstehen kann, lesen Generatoren/Extractor/Editor NUR aus der
  Signatur.
- **Akteure, Verträge, Clients** (`docs/konzept-akteure.md`; Editor §12 des Editor-Konzepts) — drei Begriffe, nicht vermischen:
  - **Akteur** = Domänen-Experte: `record X : IMensch|IMaschine|IKi, IDarf<T>…` — `IDarf` nur für das, was er SELBST hineingibt
    (CQRS058). Was eine Pipeline/ein Prozess/eine Frist daraus erzeugt, trägt den Akteur der Kette (abgeleitet, kein `IDarf`). Ein
    Dienst ist nie Akteur, er gehört einem (`IAkteurDienst<A>`, nur als Handle-Parameter, CQRS060).
  - **Akteur-Vertrag** = was ein Akteur DRAUSSEN auf Events tut: `interface IX : IAkteurVertrag<X> { OneOf<Cmd> Auf(E e); void Auf(E2 e); }`.
    Ein `Auf` mit Ausgabe ist eine **Zusage**, `void Auf` eine **Kenntnis** (CQRS061/062; mehrere Teile je Akteur erlaubt). **„Reaktion"
    ist nur der Backend-Baustein (`ISubscriber`) — nie für Zusagen verwenden.**
  - **Client** = die Software an EINER Leitung: `interface IX : IClientVertrag, ITeil…, ISendet<C>, IFragt<Q> { void Auf(E e); }` — trägt
    Vertrags-Teile (auch mehrerer Akteure), sendet/fragt nur, was ein Akteur darf, eigene Methoden nur Kenntnis (CQRS063–065). Verkörpert
    ist abgeleitet; wirksam am Handshake = Vertrag ∩ Token.
  - Generiert: `GeneratedAkteurRechte`, `GeneratedClientVertraege`, Python-Basen (`./codegen.sh`). Editor: Rahmen je Domäne × Akteur,
    📜-Vertrags-Rahmen mit Zusage-Karten, 🔌-Client-Rahmen mit Anschlussleiste und Bündeln; Einzelkanten nur per Klick, Zoom ändert nichts.
- **Kein `InMemoryEventStore`:** Store-Semantik nur gegen echtes Marten (Integration). Der
  Prüfstand testet nur store-freie Logik. Nie faken, was man nicht besitzt.
- **Proto-Regenerierung bei neuen Domain-Typen:** jeder neue Command/Event/Query/Trigger
  braucht einen Proto-DTO, sonst bricht der `DtoMapperGenerator`. Ablauf:
  `dotnet run --project Proto.SourceGeneration` → `ProtoRepo` neu bauen → Infrastructure baut.
  (Signale sind bewusst ausgenommen.)

## Build / Test

**⚠ Vor Test/Lasttest: `docs/12-tests-und-vermessung.md` lesen.** Drei Ebenen (Prüfstand in-memory
/ Integration gegen echte Infra / Last-Harness), plus reale Fallstricke: Integration
**sequentiell** lassen; der bekannte `SnapshotLiveE2ETests`-Cold-Boot-Flake ist Consul-Boot,
NICHT Timeout-tunebar; xUnit schluckt App-Logs (Cluster-Diagnose → Last-Harness `--log debug`).

- Build: `dotnet build`
- Test (Logik, immer grün): `dotnet test Infrastructure.Pruefstand.Tests/Infrastructure.Pruefstand.Tests.csproj`
- Integration (braucht Postgres/Consul/Redis, sequentiell): `dotnet test Infrastructure.Integration.Tests/Infrastructure.Integration.Tests.csproj`
- Domänen-Editor + Simulation (einzige Oberfläche): `dotnet run --project GraphExtractor` (erzeugt editor.html + domain-model.json), dann `dotnet run --project SimHost` → http://localhost:5178/editor; Bearbeiten nur im Panel: Karte anklicken, ⊕-Punkt im Panel → passende Knoten leuchten → anklicken = verbinden/lösen (`docs/konzept-editor-panel-bearbeitung.md`); 🤖-LLM-Knoten am Code-Block = Chat: Prompt senden → `claude -p` (Abo, nie API) → geprüft → .cs geschrieben → Laufzeit-Projekt gebaut (alle Generatoren) → neu eingelesen (Konzept §12); Kontexte erzeugt SimHost selbst
- Editor-Parität (Code ⇄ Extraktion ⇄ Editor, schreibt nichts): `dotnet run --project GraphExtractor -- --check`
- Grammatik als Liste „Regel → Build-Gegenstück": `dotnet run --project GraphExtractor -- --grammatik`
- Agnostik-Sonde (unbekannte Domäne im Speicher gegen handgeschriebenes Soll + Fixpunkt): `dotnet run --project GraphExtractor -- --sonde`
- LLM-Kontext je Code-Block (`docs/konzept-llm-minimalkontext.md`): `dotnet run --project GraphExtractor -- --kontexte <verz>` (Graph-Skelett + Slot-Teile) bzw. `--kontext <Disc> [--auftrag "…"]`; Isolation aller Code-Block-Stellen: `--slots`
- Last/Durchsatz: `dotnet run --project LoadHarness -- --accounts 500 --credits 40 --concurrency 128 --log warning`
- Infra hochfahren: `docker compose -f deploy-linux/docker-compose.infrastructure.yml up -d`
- Multi-Node (3 Nodes + 1 Consul, echt containerisiert): `docker compose -f deploy-multinode/docker-compose.yml up -d --build` (Anleitung + Ergebnis: `docs/06-transport-multinode-betrieb.md`)
- Hosts: `Host_Blazor`, `Host_Grpc` (siehe deploy.sh).
