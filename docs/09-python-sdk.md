# 09 — Python-SDK und Worker

Stand 2026-10-13, gegen den Code geprüft. Maßstab: docs/konzept-werkbank.md.

Zwei Teile, streng getrennt (W11):

- **Framework:** das domänenfreie Paket `cqrs_client` (`Client.Infrastructure.Python/cqrs_client/`) und der generische Funktions-Worker.
- **Beispieldomäne:** `Domain.Client.Worker.Python.ML/` — Bildklassifikation und Training der Bildinspektion, dazu die Python-Implementierungen
  externer Funktionen.

Python läuft nur mit dem Interpreter aus `CQRS_PYTHON` (lokal `gpu_env`); die Werkbank nimmt ihn für Worker und `build_python.sh`.

## 9.1 Gesamtbild

Der Python-Teil ist ein gleichberechtigter Client am selben bidirektionalen gRPC-Stream (`CqrsClientService.Connect`) wie der C#-Client
(Kapitel [08](08-frontend-blazor-client.md)) und spiegelt dessen Infrastruktur:

| Python (`cqrs_client`) | C#-Gegenstück |
|---|---|
| `mapper.PayloadMapper` | `ProtoMessageMapper` |
| `versioning.VersionTracker` | `Client.Infrastructure/Versioning/VersioningModule.cs` |
| `proxy.GrpcProxy` | `Client.Infrastructure/Connection/GrpcProxy.cs` |
| `vertrag.AkteurVertragBasis` | Client-Vertrag + `GeneratedWiring_<Assembly>` |

Er ist **kein** durabler Server-Konsument (kein Cursor, keine co-committete Marke), sondern ein externer Client: er hört Events laut Vertrag,
antwortet mit Commands (Zusagen), stellt Queries und führt Katalog-Funktionen mit Laufort `Extern` aus. Exactly-once bleibt serverseitig:
Zusagen tragen ihre Ursache, der Server leitet daraus die `CommandId` deterministisch ab und dedupliziert in der Inbox
(`konzept-akteure.md` §5.3); Funktions-Ergebnisse schreibt der Server genau einmal in den Ausführungs-Stream. Der Client-Zustand ist
in-memory.

## 9.2 Wie Python-Code entsteht: generierte Basen

Typ-Kenntnis kommt nicht aus Laufzeit-Introspektion der Domäne, sondern aus generierten Dateien. Der Entwickler erbt eine Basis und füllt
Methoden.

| Datei | Erzeugt von | Inhalt |
|---|---|---|
| `domain_client/generated/__init__.py` | `build_python.sh` (`protoc --python_betterproto_out` aus `ProtoRepo/domain.proto`) | betterproto-DTOs (`…Dto`) der Anwendung |
| `cqrs_client/leitung/__init__.py` (SDK) | `Client.Infrastructure.Python/build_leitung.sh` (aus `Cqrs.Leitung/cqrs.proto`) | die Leitung des Frameworks: `ClientMessage`/`ServerMessage`, Hüllen, `Nutzlast` |
| `domain_client/generated/vertraege.py` | `Cqrs.Codegen` | je Akteur-Vertrag (`IAkteurVertrag<A>`) bzw. Client-Vertrag (`IClientVertrag`) eine abstrakte Basis `<Name>Basis` mit `auf_<event>`, Vertrag, Hash, Zusagen, `FRAGT` |
| `domain_client/generated/funktionen.py` | `Cqrs.Codegen` | je Katalog-Funktion ohne Lese-Fähigkeit eine Basis `<Funktion>Basis` mit `FUNKTION`, `AUFTRAG`, `ERGEBNISSE`, abstrakt `rufe(auftrag, x)` |
| `domain_client/generated/funktions_tabelle.py` | `Cqrs.Codegen` | `TABELLE`: Funktion → (Basis, Modul, Klasse der Implementierung) für den generischen Worker |

`Cqrs.Codegen` läuft **automatisch** im Codegen-Schritt jedes Builds (Target `CqrsCodegenPrepass` in `ProtoRepo/ProtoRepo.csproj`), zusammen mit
`domain.proto` und den STJ-Kontexten; `./codegen.sh --check` prüft auf Drift. Die betterproto-DTOs erzeugt `build_python.sh` (Schritt 2 von ⟳ in
der Werkbank, `konzept-werkbank.md` §5); es hasht `domain.proto` (`generated/.proto_hash`) und entfernt vor `protoc` die `package`-Zeile. Die Leitung
erzeugt `build_leitung.sh` ebenso (committet); `GrpcProxy` nutzt die Route `/cqrs.CqrsClientService/Connect` (Paket `cqrs`). `Cqrs.Codegen` liest die gebauten Domänen-DLLs per
`MetadataLoadContext` und schreibt fest in das Projekt der Beispieldomäne (docs/13 W10-1, W2-1).

## 9.3 `cqrs_client` — Module

| Modul | Rolle |
|---|---|
| **client** | `CqrsClient[S]`: Lebenszyklus (Proxy, Mapper, Router, Verbindung, Versioning), Handshake, `query()` (typisierte Ask-API), `run(host, port)`; Akteur-Token aus der Konfig oder `CQRS_AKTEUR_TOKEN` |
| **vertrag** | `AkteurVertragBasis`, `Zusage`, `VertragsVerletzung` — Basis der generierten Vertragsklassen (§9.4) |
| **funktion** | `FunktionsBasis`, `Ausfuehrung`, `FunktionsVerletzung` — Basis der generierten Funktionsklassen (§9.5) |
| **dispatch** | Typ-Dispatch: Eingangs-Typ → Methode (`HandleDescriptor`, Metaklasse `HandlerMeta`); die Vertrags-Basis registriert ihre `auf_<event>`-Methoden darüber |
| **registry** | `CategoryRegistry`: ordnet betterproto-Typen EVENT/COMMAND/TRIGGER/QUERY/QUERY_RESPONSE zu |
| **router** | Verarbeitungsschleife: Event-Queue → Payload → Dispatch → jede Ausgabe nach Kategorie zurück; beantwortet Query- und Trigger-Weiterleitungen, nimmt Funktions-Aufträge an |
| **mapper** | Nutzlast: `NutzlastTabelle` (typ → DTO-Klasse aus dem Domänen-Modul), `packe`/`entpacke`; Hüllen bauen und lesen (`wrap_command`, `wrap_query`, `wrap_query_response`, `wrap_trigger`, `wrap_ergebnis`, `extract_*`) |
| **proxy** | bidirektionaler gRPC-Transport über grpclib-Lowlevel (`STREAM_STREAM`, unabhängiges Senden/Empfangen); Akteur-Token im Header `akteur-token`; Korrelation von Queries/Triggern über Futures (30 s) |
| **connection** | Reconnect mit exponentiellem Backoff (1 s → 30 s), Handshake erneut |
| **versioning** | `dict[UUID → höchste gesehene Version]` |
| **proto_sync** | Laufzeit-Abgleich gegen `/api/proto/*` am Blazor-Host (§9.7) |

**Routing über Typen:** eingehend `betterproto.which_one_of` → `type(payload)` → Methode; ausgehend `type(output)` → Kategorie → Envelope-Feld.
Commands aus einem Event-Handler gehen im Emittiert-Modus hinaus (`expected_version = -1`, keine OCC) und tragen die Ursache (Stream,
Version, Typ, Index) — der Server leitet daraus die `CommandId` ab.

## 9.4 Verträge: Zusagen, Kenntnis, Queries

Der Vertrag steht in C# (eine Quelle, `konzept-akteure.md` §3–§4); Python erbt die generierte Basis und implementiert je Eingang
`auf_<event>(self, event, ctx, state)` als Async-Generator.

Was die Basis übernimmt (`cqrs_client/vertrag.py`):
- **Dispatch** Event-Typ → `auf_<event>`; fehlt eine Methode, lässt sich die Klasse nicht instanziieren (ABC) — der Bruch fällt beim Start auf.
- **Ausgabe-Vertrag:** jedes `yield` wird gegen die Ausgänge der Zusage geprüft; ein Verstoß ist eine `VertragsVerletzung`, der Command geht
  nicht hinaus.
- **Handshake:** der `CapabilitiesRequest` nennt Vertrag (bzw. Client-Name) + Hash; Fähigkeiten und Abos nimmt der Server aus seiner Tabelle,
  wirksam ist Vertrag ∩ Token.
- **Queries:** `await self.query(dto)` liefert die typisierte Antwort; bei einem Client-Vertrag ist eine Query außerhalb von `FRAGT` eine
  `VertragsVerletzung`.

Minimalbeispiel (Beispieldomäne, nach `Domain.Client.Worker.Python.ML/run_stub.py`):

```python
from domain_client.generated import ImagePairKomplettDto, KlassifiziereBildPaarDurchKiDto
from domain_client.generated.vertraege import KlassifikationsWorkerBasis

@dataclass
class StubState:
    events: int = 0

class StubClassifier(KlassifikationsWorkerBasis[StubState]):
    async def auf_image_pair_komplett(self, event: ImagePairKomplettDto, ctx, state: StubState):
        state.events += 1
        yield KlassifiziereBildPaarDurchKiDto(aggregate_id=str(ctx.aggregate_id), label=0)
    # … je weiterem Eingang des Vertrags eine auf_<event>-Methode

client = StubClassifier(registry=create_registry(), generated_module=generated)
client.run(host, port)
```

Der Entwickler schreibt nur Fachlogik: State-Dataclass, `auf_<event>`, `yield` von Commands. Transport, Envelopes, Versioning, oneof,
Reconnect, Vertragsprüfung liegen im SDK (Inv. 5).

**Weitere Wege im SDK:**
- **Queries beantworten:** leitet der Server eine Query an den Client weiter (`QueryForward`), ruft der Router den Handler; die erste Ausgabe,
  die eine Query-Antwort ist, verpackt `mapper.wrap_query_response` und geht als `QueryResponseFromClient` zurück; ohne Antwort bzw. bei
  Ausnahme ein Fehlercode (`NO_RESPONSE`, `HANDLER_ERROR`). Der Server nimmt einen Client dafür nur für Query-Typen, die er nicht selbst
  bedient.
- **Client→Client-Trigger:** eine Ausgabe der Kategorie TRIGGER verpackt `mapper.wrap_trigger`, `proxy.send_trigger` schickt sie und wartet
  auf die Quittung; eingehende `TriggerForward` beantwortet der Router mit `TriggerResult`. Das ist ein Weg der Trigger-Welt — ohne
  Domänen-Nutzer, Rückbau offen (docs/13 W11-1).

## 9.5 Katalog-Funktionen in Python (Laufort `Extern`)

Eine Katalog-Funktion ist ein C#-Vertrag (`IFunktion`); ihr Laufort ist Konfig der Umgebung (`Funktionen:<F>:Laufort = Extern`,
`konzept-werkbank.md` §4.1). Für jede Funktion ohne Lese-Fähigkeit gibt es eine generierte Basis (§9.2).

- **Implementierung:** `funktionen/<modul>.py` im Python-Projekt, eine Klasse erbt `<Funktion>Basis` und füllt `async def rufe(auftrag, x)`;
  `x` ist die `Ausfuehrung` (deterministische `ausfuehrungs_id`, Korrelation, Akteur). Das Ergebnis muss einer der `ERGEBNISSE` sein, sonst
  geht ein Fehler hinaus. Eine fehlende Implementierung legt ⟳ der Werkbank an; der Rumpf `rufe` steht an der Funktions-Karte.
- **Was das SDK übernimmt** (`cqrs_client/funktion.py`): nach jedem Handshake die Funktionen anbieten (Funktion + Slots), Aufträge annehmen
  (höchstens so viele wie Slots), alle 10 s ein Lebenszeichen, solange `rufe` läuft, Ergebnis oder Fehler zurück. Der Server holt die Aufträge
  beim Funktions-Vermittler; das Ergebnis schreibt er genau einmal (der Erste gewinnt).
- **Generischer Worker** `funktions_worker.py --funktionen F1,F2 [--slots n] [--konfig NAME=WERT …] [--pfad <verz>]`: kennt keine Domäne,
  lädt je Funktion die Implementierung nach `funktions_tabelle.TABELLE` und prüft, dass sie die Basis erbt. Adresse und Akteur kommen aus
  `CQRS_GRPC_HOST`/`CQRS_GRPC_PORT`/`CQRS_AKTEUR_TOKEN`; der Akteur des Tokens braucht `IDarf<F>`. Gestartet über „▶ Worker“ in der Werkbank
  (`Funktionen:<F>:Worker` der Umgebung) oder von Hand. Der Worker liegt heute im Projekt der Beispieldomäne und importiert
  `domain_client.domain_registry` (docs/13 W2-1).

## 9.6 Beispieldomäne: Klassifikation und Training

Knapp, weil Prüfling, nicht Architektur:

- **Klassifikation** (`domain_client/classifier.py`, Start `run.py`; Rauchtest ohne Modell `run_stub.py`): erbt `KlassifikationsWorkerBasis`
  (Client-Vertrag `IKlassifikationsWorker`), sammelt Bildpfade je Aggregat, lädt auf `ImagePairKomplett` beide Bilder über `/api/files/` des
  Blazor-Hosts (`image_loader.py`), führt TorchScript-Inferenz in `asyncio.to_thread` aus und antwortet mit `KlassifiziereBildPaarDurchKi`.
- **Training** (`domain_client/training_worker.py`, Rauchtest `run_train_stub.py`): erbt `TrainingsWorkerBasis`, zieht Samples per Query
  (`HoleDatensatzSamples`) und meldet Fortschritt und Abschluss als Commands.
- **Funktionen:** `funktionen/bild_verkleinerung.py`, `funktionen/histogramm_ausgleich.py` (OpenCV).
- `run.py`/`run_stub.py` lesen Host/Port aus der eigenen `appsettings.json`; `domain_client/domain_registry.py` (die `CategoryRegistry` der
  Domäne) ist handgepflegt.

Tests: `Client.Infrastructure.Python/tests` (SDK) und `Domain.Client.Worker.Python.ML/tests` (Worker); Zahlen in
[12](12-tests-und-vermessung.md).

## 9.7 Proto-Abgleich zur Laufzeit

`proto_sync.ensure_types_current()` vergleicht den lokalen Hash mit `GET /api/proto/version` am Blazor-Host und lädt bei Abweichung
`GET /api/proto/domain.proto` nach. Die Gegenstelle (`Host.Blazor/ProtoEndpointExtensions.cs`, `MapProtoEndpoint`) wird nirgends gemappt; der
Abgleich ruft also ins Leere (docs/13 P3-2). Der tragende Weg ist der Build (§9.2).

## 9.8 Prinzipien

1. **Typen sind die Wahrheit, nicht Strings** (Inv. 3) — Python-Typen sind die Routing-Schlüssel.
2. **Fachcode bleibt rein** (Inv. 5) — Methoden sehen nur Payload + `ctx` + `state` bzw. Auftrag + Ausführung.
3. **Typ-Kenntnis aus generierten Basen** (W10) — Python ist dynamisch, aber was ein Worker hört, sendet, fragt und ausführt, steht in den aus
   dem C#-Vertrag generierten Basen, nicht in Laufzeit-Introspektion der Domäne.
4. **Eine Quelle:** der C#-Vertrag; `domain.proto` und die Python-Basen werden im Build daraus erzeugt.
5. **Async-Generatoren als Handler-Vertrag** — `yield` = „gib einen Command aus“ (Gegenstück zu C#s `CommandEmitter`).

## 9.9 Offen

Offene Punkte stehen nur in `docs/13`: kein TLS (`Channel(host, port)` unverschlüsselt), Bild-Download über `http://` (P1-2); durable Zustellung
an Clients (AK-1); Reflection im Codegen und abweichende Vertrags-Lesart zwischen Python-Emitter und Server (W10-1); generischer Worker im
Domänen-Projekt (W2-1); nicht gemappte Proto-Endpunkte (P3-2); Trigger-Weg (W11-1).
