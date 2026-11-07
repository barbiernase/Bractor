# 09 — Python-SDK und Worker

Stand 2026-11-07, gegen den Code geprüft.

Zwei Teile, streng getrennt (W11):

- **Framework:** das domänenfreie Paket `cqrs_client` (`Client.Infrastructure.Python/cqrs_client/`).
- **Beispieldomäne:** `Domain.Client.Worker.Python.ML/` — die Clients für Klassifikation und Training der Bildinspektion.

Python läuft mit dem Interpreter der Umgebung: der des Workers (`Clients:<Name>:Worker:Python`), sonst `Umgebung:Python`, sonst `CQRS_PYTHON`,
sonst `python3` (eine Regel: `UmgebungKonfig.Python`, `Abstractions/Umgebung/UmgebungKonfig.cs`); die Werkbank nimmt ihn für Worker und
`build_python.sh`.

## 9.1 Gesamtbild

Der Python-Teil ist ein Client am selben bidirektionalen gRPC-Stream (`CqrsClientService.Connect`, `Cqrs.Leitung/cqrs.proto`) wie der C#-Client
(Kapitel [08](08-frontend-blazor-client.md)); auf dem Server ist er eine Sitzung wie jede andere (`Infrastructure/GrpcClient/SitzungsActor.cs`).

| Python (`cqrs_client`) | C#-Gegenstück |
|---|---|
| `mapper.PayloadMapper`, `mapper.NutzlastTabelle` | `ProtoMessageMapper`, `GeneratedNutzlast` |
| `versioning.VersionTracker` | `Client.Infrastructure/Versioning/VersioningModule.cs` |
| `proxy.GrpcProxy` | `Client.Infrastructure/Connection/GrpcProxy.cs` |
| `vertrag.ClientVertragBasis` | `GeneratedWiring_<Assembly>` aus `[assembly: IstClient<X>]` |

Er ist ein Client am Rand, kein Konsument beim Framework. Was er tut, sagen die Akteure, die er verkörpert (Grammatik in
[10](10-entwickler-api.md) §10.3.6, Begriffe in [konzept-akteure.md](konzept-akteure.md)) — über seine Leitung gehen ihr `IDarf` und ihr `IHoert`:

- **Reaktion** (`IHoert` eines Events): er antwortet mit Commands aus dem `IDarf` seiner Akteure (§9.5);
- **Kenntnis** (`IHoert` eines Events oder Hinweises, ohne Ausgabe): Push über den Verteiler seines Knotens — verlierbar;
- **fragt** (`IDarf` auf eine Query): Queries mit typisierter Antwort;
- **sendet** (`IDarf` auf einen Command oder Hinweis): steht im Handshake; Commands gibt er als Reaktion aus (§9.4).

Genau einmal wirkt eine Reaktion über den Läufer (§9.5): er liest das Log ab seiner gesicherten Position und leitet die Command-Ids ab
([11](11-garantien.md) §11.6). Der übrige Zustand des Clients lebt im Prozess (`state`, in-memory); ihn zu sichern, ist Sache des Workers.

## 9.2 Wie Python-Code entsteht: generierte Basen und DTOs

Typ-Kenntnis kommt nicht aus Laufzeit-Introspektion der Domäne, sondern aus generierten Dateien. Der Entwickler erbt eine Basis und füllt
Methoden.

| Datei | Erzeugt von | Inhalt |
|---|---|---|
| `domain_client/generated/__init__.py` | `Domain.Client.Worker.Python.ML/build_python.sh` (`grpc_tools.protoc --python_betterproto_out` aus `Domain.Leitung/domain.proto`) | betterproto-DTOs (`…Dto`) der Anwendung; `Id<T>` ist darin ein UUID-Text |
| `cqrs_client/leitung/__init__.py` (SDK) | `Client.Infrastructure.Python/build_leitung.sh` (aus `Cqrs.Leitung/cqrs.proto`, committet) | die Leitung des Frameworks: `ClientMessage`/`ServerMessage`, Hüllen, `Nutzlast` |
| `domain_client/generated/vertraege.py` | `Cqrs.Codegen` (`PythonVertragsEmitter`) | je Client (`record X : IClient`) eine abstrakte Basis `<Client>Basis` mit `CLIENT`, `AKTEURE`, `VERTRAG_HASH`, `HOERT` (gehörtes Event oder gehörter Hinweis → Methode), `SPONTAN` (Commands und Hinweise der Akteure), `FRAGT` (ihre Queries); je gehörtem Typ abstrakt `auf_<event>(e, ctx, state)` |
| `domain_client/generated/nutzlast_tabelle.py` | `Cqrs.Codegen` (`PythonNutzlastEmitter`) | `TABELLE`: `Nutzlast.typ` → DTO-Klasse — Gegenstück zu `GeneratedNutzlast`, dieselbe Namensregel (`NameSanitizer.NutzlastTyp`); der Client bekommt sie als `nutzlast=` |

`Cqrs.Codegen` läuft **automatisch** im Codegen-Prepass jedes Builds (Target `CqrsCodegenPrepass`, `Cqrs.Codegen/CqrsCodegen.targets`), zusammen
mit `domain.proto` und den STJ-Kontexten; er schreibt die Python-Generate nur, wenn das Projekt des Python-Workers der Wurzel existiert
(`Projektstruktur.PythonWorker`), und liest die gebauten Domänen-DLLs per `MetadataLoadContext`. `./codegen.sh --check` prüft diese Generate auf
Drift. Die betterproto-DTOs erzeugt dagegen nur `build_python.sh`: es hasht `domain.proto` (`generated/.proto_hash`), entfernt vor `protoc` die
`package`-Zeile und schreibt flach nach `__init__.py`. Aufgerufen wird es von ⟳ der Werkbank (Schritt „Python-Basen“ nach dem Bauen,
`Werkbank/Server/WerkbankServer.cs`, mit dem Interpreter der Umgebung vorn im `PATH`) und von `deploy/deploy_clients.sh`; ohne diesen Schritt
passen DTOs und Nutzlast-Tabelle nicht zusammen (docs/13 FB-20). `build_leitung.sh` arbeitet ebenso; `GrpcProxy` setzt die Route
`/cqrs.CqrsClientService/Connect` (Paket `cqrs`) selbst.

## 9.3 `cqrs_client` — Module

| Modul | Rolle |
|---|---|
| **client** | `CqrsClient[S]`: Lebenszyklus (Proxy, Mapper, Router, Verbindung, Versioning, `ReaktionsLaeufer`), Handshake, `query()` (typisierte Antwort), `run(host, port)`; je Verbindung eine Sitzung (Lesen + Verarbeiten bzw. Lesen + Läufer), nach Abbruch neu verbinden; Akteur-Token aus der Konfig oder `CQRS_AKTEUR_TOKEN` |
| **vertrag** | `ClientVertragBasis`, `VertragsVerletzung`, `VertragsMeta` — Basis der generierten Client-Basen (§9.4) |
| **reaktionen** | `ReaktionsLaeufer`, `DateiReaktionsStand`, `ReaktionsStandImSpeicher`, `command_id` — die Reaktionen genau einmal (§9.5) |
| **dispatch** | Typ-Dispatch: Eingangs-Typ → Methode (`HandleDescriptor`, `@handle.register`, Metaklasse `HandlerMeta`) |
| **registry** | `CategoryRegistry`: betterproto-Typ → EVENT/COMMAND/QUERY/QUERY_RESPONSE; `ergaenzt(...)` nimmt die Typen einer Client-Basis auf |
| **router** | `MessageRouter`: Verarbeitungsschleife über die Eingangs-Queue — `event` → Dispatch → Ausgaben nach Kategorie zurück (ohne Stand, verlierbar) |
| **mapper** | `NutzlastTabelle` (`packe`/`entpacke`/`typ`, nur über die generierte Tabelle) und `PayloadMapper` (`wrap_command`, `wrap_transient_event`, `wrap_query`, `extract_*`) |
| **proxy** | bidirektionaler gRPC-Transport über grpclib-Lowlevel (`STREAM_STREAM`, unabhängiges Senden/Empfangen, ein Sende-Lock); Akteur-Token im Header `akteur-token`; Queries über Futures (30 s); Senden von Commands und Hinweisen, `lies_log` (`LiesLog` → `LogSeite`); `aktualisiert` wird nur geloggt |
| **connection** | `ConnectionManager`: Reconnect mit exponentiellem Backoff (1 s → 30 s), Handshake erneut |
| **versioning** | `dict[UUID → höchste gesehene Version]`, aus Push-Events (`max(aggregate_version, stream_head_version)`), auch aus der `CommandQuittung` eines eigenen Commands (sie trägt die Version des Aggregats danach) |
| **proto_sync** | Laufzeit-Abgleich gegen `/api/proto/*` am Blazor-Host (§9.7) |

**Routing über Typen:** eingehend `betterproto.which_one_of` → `type(payload)` → Methode; ausgehend `type(output)` → Kategorie → Hülle.
Ein Command, den ein Handler ohne Client-Basis ausgibt, geht als `CommandRequest` mit gewürfelter `command_id` und `expected_version = -1` hinaus —
der Server sendet ihn im Modus `Emittiert` ohne Versionsprüfung, die Inbox des Ziels dedupliziert über die Command-Id. Eine Ausgabe der Kategorie
EVENT geht als Hinweis (`TransientEventRequest`) an den Server, der ihn an die Verteiler gibt (an die Akteure, die ihn hören); die Kennung würfelt
der Server, weil das SDK keine `causation_id` setzt.

## 9.4 Client-Basis: Reaktion, Kenntnis, Fragen

Akteur und Client stehen in C# (eine Quelle, `Domain.Akteure/Akteure.cs` und `Clients.cs`):

```csharp
public sealed record Klassifizierer : IAkteur,
    IHoert<BildVerfuegbar>, IHoert<ImagePairKomplett>, IHoert<ModellAktiviert>,   // Fakten: er reagiert bzw. nimmt sie zur Kenntnis
    IDarf<KlassifiziereBildPaarDurchKi>, IDarf<KlassifiziereEinzelBildDurchKi>, IDarf<HoleAktivesModell>;

public sealed record KlassifikationsWorker : IClient, IVerkoerpert<Klassifizierer>;
```

Python erbt die generierte Basis und füllt je gehörtem Event `auf_<event>` — die Reaktion (Commands des `IDarf`, ein Wert oder Stücke) oder
nichts (Kenntnis; nach `Domain.Client.Worker.Python.ML/run_stub.py`):

```python
from domain_client.generated import BildVerfuegbarDto, ImagePairKomplettDto, KlassifiziereBildPaarDurchKiDto, ModellAktiviertDto
from domain_client.generated.vertraege import KlassifikationsWorkerBasis

@dataclass
class StubState:
    bilder: dict[UUID, dict[int, str]] = field(default_factory=dict)

class StubClassifier(KlassifikationsWorkerBasis[StubState]):
    async def auf_bild_verfuegbar(self, e: BildVerfuegbarDto, ctx, state: StubState):        # Kenntnis: gibt nichts aus
        state.bilder.setdefault(ctx.aggregate_id, {})[e.version] = e.pfad

    async def auf_image_pair_komplett(self, e: ImagePairKomplettDto, ctx, state: StubState):  # Reaktion: das Urteil
        return KlassifiziereBildPaarDurchKiDto(aggregate_id=str(ctx.aggregate_id), label=0)

    async def auf_modell_aktiviert(self, e: ModellAktiviertDto, ctx, state: StubState): ...

from domain_client.generated.nutzlast_tabelle import TABELLE   # generiert: Nutzlast.typ → DTO-Klasse
StubClassifier(registry=create_registry(), nutzlast=TABELLE, config={"reaktion_stand": "…"}).run(host, port)
```

Was die Basis übernimmt (`cqrs_client/vertrag.py`):
- **Reaktion:** Dispatch gehörter Typ → `auf_<event>`; was sie ausgibt, muss ein Command des `IDarf` sein (`SPONTAN`, sonst
  `VertragsVerletzung`). Genau einmal wirkt sie mit einem Stand (§9.5); ohne Stand reagiert der Client nur auf Pushes, verlierbar.
- **Vollständigkeit:** fehlt eine Methode, lässt sich die Klasse nicht instanziieren (ABC) — wer einen Akteur verkörpert, muss auf alles, was er
  hört, eine Methode haben; der Bruch fällt beim Start auf.
- **Registry:** die Typen aus `HOERT`, `SPONTAN` und `FRAGT` ergänzt die Basis in der übergebenen
  `CategoryRegistry`.
- **Handshake:** der `CapabilitiesRequest` nennt den Client (`vertrag`) und seinen Hash (`vertrag_hash`); Befugnisse nimmt der Server aus seiner
  Tabelle, wirksam ist „verkörpert ∩ Token“ für beide Richtungen. Ein abweichender Hash kommt in `CapabilitiesResponse.vertrag_hash` zurück.
- **Fragen:** `await self.query(dto)` liefert die typisierte Antwort (`QueryRequest`/`QueryResponse`, Fehler als `proxy.ServerError`); eine
  Query außerhalb von `FRAGT` ist eine `VertragsVerletzung`. Read-your-writes (`mindestens`) setzt das SDK nicht.
- **Senden:** `SPONTAN` geht in den Handshake (`message_types`); Commands gehen als Ausgabe einer Reaktion hinaus, einen eigenen Sende-Aufruf
  hat das SDK nicht.

Der Entwickler schreibt nur Fachlogik: State-Dataclass, die Methoden der Basis, Rückgaben bzw. `yield`. Transport, Hüllen, Versioning, oneof,
Reconnect und Prüfungen liegen im SDK (Invariante 5).

**Ohne Client-Basis:** ein `CqrsClient[S]` mit `@handle.register`-Methoden nennt im Handshake selbst, was er hört (`message_types`), sendet
(`_declared_command_types`); wirksam ist auch dann nur, was das Token erlaubt (`Infrastructure/GrpcClient/Handshake.cs`,
`Infrastructure/Akteure/AkteurTor.cs`). Seine Handler dürfen Commands und Hinweise ausgeben. Queries beantwortet ein Client nie: eine Query hat genau
ein Ziel, einen Reader oder ein Aggregat ([10](10-entwickler-api.md) §10.5) — ein Handler auf einer Query bricht den Start ab.

## 9.5 Reaktionen genau einmal

Nennt die Konfig des Clients `reaktion_stand` (ein Verzeichnis, auch `CQRS_REAKTION_STAND`; die Werkbank setzt ihn), reagieren seine Methoden
nicht nur auf Pushes, sondern über das Log. Der `ReaktionsLaeufer` (`cqrs_client/reaktionen.py`, Gegenstück zu `Client.Infrastructure/Reaktionen`):

1. liest ab der gesicherten Position (`proxy.lies_log`: nur Hörbares, bis zum Horizont) — ein Push weckt ihn nur; liegt der `horizont` der Seite
   unter seiner Position (eine neue Datenbank), liest er von vorn;
2. ruft je Event die Methode `auf_<event>` und sendet jedes Stück, sobald es entsteht (ein Training meldet so Beginn und Fortschritt, während es
   rechnet); jeder Command trägt das Event als `ursache` (Spur `Stream:Version`), ein Command ohne `aggregate_id` geht an den Kopf eines Graphen;
3. leitet jede Command-Id aus Reaktion, Position, Nummer und Stelle ab (`command_id`, dieselbe Formel wie in C#: MD5, Guid-Bytes little-endian)
   — nach einem Neustart rechnet er dieselben Ids, die Inbox des Ziels bzw. der offene Eingang am Kopf verwirft die Wiederholung;
4. sichert die Position erst, wenn jeder Command der Seite beantwortet ist (Quittung, Event, Ablehnung oder Fehler mit seiner Korrelation).

`reaktion_name` (Standard: der Klassenname) ist der Schlüssel von Stand und Ids — über Neustarts gleich halten. Ids von Aggregaten stehen als
UUID-Text im DTO, z. B. `aggregate_id=str(ctx.aggregate_id)`.

- **Stücke:** ein Generator (Training) schickt jedes Stück sofort. Bricht der Worker mitten in einer Reaktion ab, rechnet er sie nach dem Neustart
  von vorn — die Inbox der Ziele verwirft schon Gesendetes (dieselben Ids).
- **Zustand im Worker** (ein Modell) sichert das Framework nicht; der Worker lädt ihn selbst, etwa auf eine Kenntnis hin (`ModellAktiviert`).

Gestartet wird ein Python-Client über „▶ Worker“ in der Werkbank (`Werkbank/Server/Worker.cs`: `Clients:<Name>:Worker` der Umgebung — Skript,
Argumente, Interpreter, Token) oder als eigener Prozess; Adresse und Akteur kommen aus `CQRS_GRPC_HOST`/`CQRS_GRPC_PORT`/`CQRS_AKTEUR_TOKEN`.

## 9.6 Beispieldomäne: Klassifikation und Training

Knapp, weil Prüfling, nicht Architektur:

- **Klassifikation** (`domain_client/classifier.py`, Start `run.py`; Rauchtest ohne Modell `run_stub.py`): erbt `KlassifikationsWorkerBasis`
  (Client `KlassifikationsWorker`, Akteur `Klassifizierer`), merkt sich auf `BildVerfuegbar` die Bildpfade je Aggregat und reagiert auf
  `ImagePairKomplett`: lädt beide Bilder über `/api/files/` des Blazor-Hosts (`image_loader.py`), führt TorchScript-Inferenz in `asyncio.to_thread`
  aus und gibt `KlassifiziereBildPaarDurchKi` zurück; ohne Bilder bleibt das Paar unbeurteilt (ein Fehler hielte jede spätere Reaktion an). Auf
  `ModellAktiviert` lädt er das neue Modell.
- **Training** (`domain_client/training_worker.py`, Rauchtest `run_train_stub.py`): erbt `TrainingsWorkerBasis` (Client `TrainingsWorker`, Akteur
  `TrainingsSystem`), reagiert auf `TrainingAngefordert` in Stücken (`MeldeTrainingBegonnen`, `MeldeFortschritt` je Epoche,
  `MeldeTrainingAbgeschlossen` bzw. `MeldeTrainingGescheitert`), zieht die Samples per Query (`HoleDatensatzSamples`) und liest nach jeder Epoche
  den Lauf (`HoleTrainingslauf`): steht er auf „abgebrochen“, hört er kooperativ auf; der Trainer ist ein Stub.
- `run.py`/`run_stub.py`/`run_train_stub.py` lesen Host/Port aus der eigenen `appsettings.json`, Umgebungsvariablen gehen vor;
  `domain_client/domain_registry.py` (die `CategoryRegistry` der Domäne) ist handgepflegt (docs/13 P2-11).

Tests: `Client.Infrastructure.Python/tests` (SDK: `test_reaktionen`, `test_vertrag`, `test_client_query`, `test_client_reconnect`, `test_mapper_*`) und
`Domain.Client.Worker.Python.ML/tests` (`test_vertraege`, `test_training_worker`); Zahlen in [12](12-tests-und-vermessung.md).

## 9.7 Proto-Abgleich zur Laufzeit

`proto_sync.ensure_types_current()` vergleicht den lokalen Hash mit `GET /api/proto/version` am Blazor-Host und lädt bei Abweichung
`GET /api/proto/domain.proto` nach (`run.py` übergibt dazu `file_base_url` und `generated_dir`). Die Gegenstelle
(`Host.Blazor/ProtoEndpointExtensions.cs`, `MapProtoEndpoint`) wird nirgends gemappt; der Abgleich ruft also ins Leere (docs/13 P3-2). Der
tragende Weg ist der Build (§9.2).

## 9.8 Prinzipien

1. **Typen sind die Wahrheit, nicht Strings** (Invariante 3) — Python-Typen sind die Routing-Schlüssel.
2. **Fachcode bleibt rein** (Invariante 5) — Methoden sehen nur Payload, `ctx` und `state`; Position, Ids und Ursache setzt der Läufer.
3. **Typ-Kenntnis aus generierten Basen** — was ein Client hört, sendet und fragt, steht in den aus seinen Akteuren generierten Basen, nicht
   in Laufzeit-Introspektion der Domäne.
4. **Eine Quelle:** Akteure und Clients in C#; `domain.proto` und die Python-Basen werden daraus erzeugt.
5. **Genau einmal ohne Festhalten beim Framework:** der Client hält seine Position selbst; Wiederholungen verwerfen die Inbox des Ziels und der
   offene Eingang am Kopf über die abgeleiteten Ids.

## 9.9 Offen

Offene Punkte stehen nur in [13](13-reifegrad-schulden-bewertung.md) §13.0: kein TLS (`Channel(host, port)` unverschlüsselt), Bild-Download über
`http://` (P1-2); Python-DTOs ohne Drift-Prüfung (FB-20); Ausgangskorb und neu Fragen fehlen im SDK (FB-14); Reflection im Codegen (W10-1); handgepflegte Registry und Kleinkram im SDK (P2-11); nicht gemappte
Proto-Endpunkte (P3-2); Akteure und Clients (AK-1).
