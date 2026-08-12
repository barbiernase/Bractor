# 09 — Python-SDK & ML-Worker

Zwei Projekte: das domänenfreie Framework-Paket `cqrs_client` und der domänenspezifische
ML-Worker `domain_client`.

## 9.1 Gesamtbild

Der Python-Teil ist ein **First-Class-Client-SDK**, das sich als gleichberechtigter Teilnehmer
an denselben bidirektionalen gRPC-Stream hängt wie der C#/Blazor-Client. Er ist bewusst als
**Spiegel der C#-Client-Infrastruktur** gebaut — nahezu jedes Modul benennt im Docstring sein
C#-Gegenstück:

| Python | C#-Gegenstück |
|---|---|
| `mapper.PayloadMapper` | `ProtoMessageMapper` |
| `versioning.VersionTracker` | `Versioning/VersioningModule.cs` |
| `proxy.GrpcProxy` | `Connection/GrpcProxy.cs` |
| `domain_registry` | `Mapping/MessageTypeMapping.cs` |
| `domain_client` (Worker) | `Domain.Client.ImagePair` |

Er ist **kein** durabler Server-Konsument im Sinne der Invarianten (kein Marten-Cursor, keine
co-committete Marke), sondern ein **externer Client-Subscriber** — architektonisch der
„Weg B / Client-Gateway"-Pfad: subscribed Events per Capabilities-Handshake, reagiert, emittiert
Commands zurück. Der Exactly-once-Garantiepunkt bleibt serverseitig; der Python-Client ist
best-effort und hält seinen State rein in-memory.

## 9.2 `cqrs_client` — Modul-Rollen

| Modul | Rolle |
|---|---|
| **client** | `CqrsClient[S]`-Basisklasse. Lifecycle: baut Capabilities aus registrierten Handler-Typen, verdrahtet Proxy/Mapper/Router/Connection/Versioning, startet 3 asyncio-Tasks |
| **dispatch** | Typ-Hint-basiertes Routing ohne Namenskonventionen. `@handle.register` + Metaklasse `HandlerMeta` liest den Type-Hint des 2. Parameters → `dict[type → Methode]`, O(1)-Lookup |
| **registry** | `CategoryRegistry`: klassifiziert betterproto-Typen in EVENT/COMMAND/TRIGGER/QUERY über invertierten Index; `capabilities_name()` strippt das `Dto`-Suffix |
| **router** | zentrale Verarbeitungsschleife: Event-Queue → Payload → dispatch → jeden yield kategorie-basiert zurückrouten; trägt `MessageContext` |
| **mapper** | oneof-Ent-/Verpackung. Baut `type → oneof-Feldname`-Map aus den betterproto-Fields — kein Feld-Mapping nötig (betterproto-Objekte *sind* die Payloads) |
| **proxy** | bidirektionaler gRPC-Transport über **grpclib-Lowlevel** (`STREAM_STREAM`), nicht den generierten Stub (unabhängiges Send/Receive); Query/Trigger-Korrelation via `Future`-Dicts + 30 s Timeout |
| **connection** | Reconnect mit exponentiellem Backoff (1s→30s); `monitor()` re-connectet + re-sendet Capabilities |
| **versioning** | In-Memory `dict[UUID → höchste gesehene Version]`; liefert `expected_version` beim Command-Send |
| **proto_sync** | Laufzeit-Sicherheitsnetz für Proto-Synchronisation (§9.3) |

**Typisiertes Routing:** Python-Typen selbst sind die Routing-Schlüssel (Parität zu Invariante 3).
Eingehend: `betterproto.which_one_of` → `type(payload)` → Handler-Dict. Ausgehend: `type(output)`
→ `CategoryRegistry.classify()` → Kanal → Mapper setzt das richtige Envelope-Feld. Capabilities
werden aus den Type-Hints abgeleitet; der Entwickler deklariert nie Strings. Verifiziert: der
C#-`CapabilitiesHandler` liest `request.MessageTypes` (das Feld, das Python mit Events+Commands
füllt).

## 9.3 `proto_sync.py` — Proto-Synchronisation

Single Source of Truth ist **`ProtoRepo/domain.proto`** (vom .NET-Build erzeugt):
1. **Build-Zeit (primär):** `build_python.sh` sha256-hasht `domain.proto`, ruft `protoc
   --python_betterproto_out` → `generated/__init__.py`. Kritischer Kniff: `sed 's/^package
   .*;//'` entfernt die `package CqrsSolution;`-Zeile vorher (sonst legt betterproto ein kaputtes
   Unterverzeichnis an). Hash → `generated/.proto_hash`.
2. **Laufzeit (Sicherheitsnetz):** `ensure_types_current()` holt `GET /api/proto/version`
   (SHA256) vom Blazor-Host, vergleicht mit lokalem Hash, regeneriert bei Abweichung durch
   Download von `GET /api/proto/domain.proto`. Beide Endpunkte existieren serverseitig
   (`ProtoEndpointExtensions.cs`).

Es generiert echten Python-Code (betterproto-Dataclasses), aber nur als Wrapper um `protoc` —
keine eigene Codegen-Logik. **Route-Detail** (sauber gelöst): weil das Package gestrippt wird,
generiert der Stub die falsche Route; `GrpcProxy` umgeht das mit der hartkodierten
voll-qualifizierten Route `/CqrsSolution.CqrsClientService/Connect`.

## 9.4 `Domain.Client.Worker.Python.ML` — der ML-Worker

**Fachlich:** industrielle Bildinspektion. Ein KI-Classifier vergleicht Bildpaare (Versionen
`dc0`/`dc2`) und klassifiziert Anomalien (0/1/2).

**Als Konsument:** ein out-of-process **Reaktion**-Äquivalent — subscribed zwei Events, reagiert,
emittiert ein Command:
- `on_bild_verfuegbar(BildVerfuegbarDto)`: sammelt Bildpfade pro Aggregat in-memory.
- `on_image_pair_komplett(ImagePairKomplettDto)`: lädt beide Bilder per HTTP von `/api/files/`,
  konvertiert zu Tensoren, führt Torch-Inferenz aus (`asyncio.to_thread`, um den Event-Loop
  nicht zu blockieren), und **yieldet** `KlassifiziereBildPaarDurchKiDto` → Router verpackt es
  als Command und schickt es zurück.

Kopplung: `image_loader.py` (Bild-Download, URL-Aufbau identisch zu C#s `LocalFilePathResolver`);
`domain_registry.py` (hand-gepflegte `CategoryRegistry`); `generated/` (betterproto-Typen).
Torch-Modell: TorchScript (`torch.jit.load`), device auto-detected, `argmax` über gestapeltes
dc0/dc2-Batch.

**Betriebsmodi:** `run.py` = Produktiv (lädt `appsettings.json` — dieselbe Konvention wie C#,
aktiviert Proto-Sync + Torch); `run_stub.py` = Rauchtest ohne Torch (nur Nachrichtenfluss).

## 9.5 Öffentliche API (Minimalbeispiel aus `run_stub.py`)

```python
# 1. State als dataclass
@dataclass
class StubState:
    events: int = 0

# 2. Von CqrsClient[State] erben, Handler mit Type-Hints registrieren
class StubClassifier(CqrsClient[StubState]):
    _declared_command_types = [KlassifiziereBildPaarDurchKiDto]   # Sende-Capabilities

    @handle.register
    async def on_komplett(self, event: ImagePairKomplettDto, ctx, state: StubState):
        yield KlassifiziereBildPaarDurchKiDto(aggregate_id=str(ctx.aggregate_id), label=0)

# 3. Registry + generiertes Modul reichen, run() blockierend starten
client = StubClassifier(registry=create_registry(), generated_module=generated)
client.run(host, port)
```

Der Entwickler schreibt **nur** Domänenlogik: State-dataclass, typ-annotierte Handler, `yield`
von Commands. Transport, Envelopes, Versioning, oneof, Reconnect — alles im Framework verborgen
(Parität zu Invariante 5). Capabilities werden aus den Type-Hints abgeleitet.

## 9.6 Design-Prinzipien (Parität zur .NET-Seite)

1. **Typen sind die Wahrheit, nicht Strings** (Invariante 3).
2. **Fachcode bleibt rein** (Invariante 5) — Handler sehen nur Payload + `ctx` + `state`.
3. **Keine Reflection auf der heißen Bahn** (Invariante 4, adaptiert) — Typ→Feld/Kategorie-Maps
   werden einmalig beim Start gebaut, danach O(1)-Lookups.
4. **Single Source of Truth = `domain.proto`** mit Hash-Verifikation gegen Drift.
5. **betterproto ersetzt handgeschriebene Konverter** (schlanker als C#s `ProtoMessageMapper`).
6. **Async-Generatoren als Handler-Vertrag** — `yield` = „emittiere ein Command/Event" (direktes
   Gegenstück zu C#s `CommandEmitter`).
7. **Vier Nachrichtenklassen, ein Transport** (Parität zum „vier Konsumenten, eine Maschine"-Bild).

## 9.7 Reifegrad & Schulden

**Solide/produktionsnah:** `cqrs_client`-Kern konsistent, sauber, gut dokumentiert; Event→Command-
Pfad vollständig; Reconnect + Backoff + Proto-Hash-Verifikation durchdacht; der ML-Worker ist
real (echtes Torch, Event-Loop-schonend).

**Unfertig / Schulden:**
1. **Query-Beantwortung kaputt/unvollständig** — `router._handle_query_forward` hat ein
   explizites `# TODO: Set the correct oneof field`; die `QueryResponsePayloadDto` wird leer
   gesendet. Ein Python-Client kann Events/Commands, aber **keine Queries beantworten** (für den
   ML-Worker irrelevant).
2. **Client→Client-Trigger nicht implementiert** (nur Warnung).
3. **`generate_registry.py` ist ein Stub** — sammelt und *printet* nur, schreibt die Datei nie;
   `domain_registry.py` ist de facto **handgepflegt**.
4. **Keinerlei Tests** auf der Python-Seite.
5. **`VersionTracker.reset()` nie aufgerufen** — nach Reconnect potenziell veraltete
   `expected_version`.
6. **Toter Code:** `proxy._detect_route()`.
7. **Sicherheit:** kein TLS (`Channel(host, port)` plain), kein Auth-Token, `user_id` leer,
   Bild-Download über `http://`. Für den „Prod-Security"-Anspruch fehlt hier alles.
8. **Read-Loop schluckt Exceptions** (geloggt) — kein Dead-Letter, kein Retry. Best-effort,
   inhärent für externe Clients, aber nicht abgesichert.

**Fazit:** eine architekturtreue Portierung des C#-Client-Frameworks — derselbe typgetriebene,
reflection-arme Zustellansatz, dieselben Invarianten, ein gemeinsamer Proto-Vertrag. Der
Event→Command-Pfad (der Kern für ML-Worker) ist vollständig und elegant; Query-Antwort und
Registry-Generierung sind angefangen aber unfertig, und es fehlen Tests sowie
Transport-Sicherheit.
