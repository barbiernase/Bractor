# 09 — Python-SDK und Worker

Stand 2026-11-02, gegen den Code geprüft.

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

Er ist **kein** durabler Konsument (kein Cursor, keine Position), sondern ein Client am Rand. Was er tut, sagen die Akteure, die er verkörpert
(Grammatik in [10](10-entwickler-api.md) §10.3.6, Begriffe in [konzept-akteure.md](konzept-akteure.md)) — über seine Leitung gehen ihr `IDarf` und
ihr `IHoert`:

- **Arbeit** (`IHoert` eines Commands ohne Ziel): er nimmt sie an und antwortet mit Commands aus dem `IDarf` des Akteurs (§9.5);
- **Kenntnis** (`IHoert` eines Events oder Hinweises): Push über den Verteiler seines Knotens — verlierbar, ohne Ausgabe;
- **fragt** (`IDarf` auf eine Query): Queries mit typisierter Antwort;
- **sendet** (`IDarf` auf einen Command oder Hinweis): steht im Handshake; einen eigenen Sende-Aufruf hat das SDK nicht (§9.4).

Exactly-once bleibt serverseitig: die Antwort auf einen Auftrag sendet der Server, die erste gewinnt, die Command-Ids leitet er aus dem Auftrag ab
([11](11-garantien.md) §11.6). Der Zustand des Clients lebt im Prozess (`state`, in-memory); ihn zu sichern, ist Sache des Workers.

## 9.2 Wie Python-Code entsteht: generierte Basen und DTOs

Typ-Kenntnis kommt nicht aus Laufzeit-Introspektion der Domäne, sondern aus generierten Dateien. Der Entwickler erbt eine Basis und füllt
Methoden.

| Datei | Erzeugt von | Inhalt |
|---|---|---|
| `domain_client/generated/__init__.py` | `Domain.Client.Worker.Python.ML/build_python.sh` (`grpc_tools.protoc --python_betterproto_out` aus `Domain.Leitung/domain.proto`) | betterproto-DTOs (`…Dto`) der Anwendung; `Id<T>` ist darin ein UUID-Text |
| `cqrs_client/leitung/__init__.py` (SDK) | `Client.Infrastructure.Python/build_leitung.sh` (aus `Cqrs.Leitung/cqrs.proto`, committet) | die Leitung des Frameworks: `ClientMessage`/`ServerMessage`, Hüllen, `Nutzlast` |
| `domain_client/generated/vertraege.py` | `Cqrs.Codegen` (`PythonVertragsEmitter`) | je Client (`record X : IClient`) eine abstrakte Basis `<Client>Basis` mit `CLIENT`, `AKTEURE`, `VERTRAG_HASH`, `ARBEIT` (je Akteur: gehörter Command → `Arbeit(methode, antworten)`), `HOERT` (Kenntnis → Methode), `SPONTAN` (Commands und Hinweise der Akteure), `FRAGT` (ihre Queries); je gehörtem Command abstrakt `auf_<command>(c, h, state)`, je Kenntnis `auf_<event>(e, ctx, state)` |
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
| **client** | `CqrsClient[S]`: Lebenszyklus (Proxy, Mapper, Router, Verbindung, Versioning, `ArbeitsLaeufer`), Handshake, `query()` (typisierte Antwort), `run(host, port)`; je Verbindung eine Sitzung (Lesen + Verarbeiten), nach Abbruch neu verbinden und die Arbeit neu annehmen; Akteur-Token aus der Konfig oder `CQRS_AKTEUR_TOKEN` |
| **vertrag** | `ClientVertragBasis`, `VertragsVerletzung`, `VertragsMeta` — Basis der generierten Client-Basen (§9.4) |
| **arbeit** | `Arbeit`, `AkteursArbeit`, `ArbeitsLaeufer`, `Herkunft`, `ArbeitsVerletzung` — die Arbeit der verkörperten Akteure (§9.5) |
| **dispatch** | Typ-Dispatch: Eingangs-Typ → Methode (`HandleDescriptor`, `@handle.register`, Metaklasse `HandlerMeta`) |
| **registry** | `CategoryRegistry`: betterproto-Typ → EVENT/COMMAND/QUERY/QUERY_RESPONSE; `ergaenzt(...)` nimmt die Typen einer Client-Basis auf |
| **router** | `MessageRouter`: Verarbeitungsschleife über die Eingangs-Queue — `event` → Dispatch → Ausgaben nach Kategorie zurück; `arbeits_auftrag` → `ArbeitsLaeufer.nimm` (läuft entkoppelt) |
| **mapper** | `NutzlastTabelle` (`packe`/`entpacke`/`typ`, nur über die generierte Tabelle) und `PayloadMapper` (`wrap_command`, `wrap_transient_event`, `wrap_query`, `extract_*`) |
| **proxy** | bidirektionaler gRPC-Transport über grpclib-Lowlevel (`STREAM_STREAM`, unabhängiges Senden/Empfangen, ein Sende-Lock); Akteur-Token im Header `akteur-token`; Queries über Futures (30 s); Senden von Commands, Hinweisen, `ArbeitAnnehmen`, `ArbeitsErgebnis`, `ArbeitLebtMeldung`; `aktualisiert` wird nur geloggt |
| **connection** | `ConnectionManager`: Reconnect mit exponentiellem Backoff (1 s → 30 s), Handshake erneut, danach `on_connected` (die Arbeit annehmen) |
| **versioning** | `dict[UUID → höchste gesehene Version]`, aus Push-Events (`max(aggregate_version, stream_head_version)`), auch aus der `CommandQuittung` eines eigenen Commands (sie trägt die Version des Aggregats danach) |
| **proto_sync** | Laufzeit-Abgleich gegen `/api/proto/*` am Blazor-Host (§9.7) |

**Routing über Typen:** eingehend `betterproto.which_one_of` → `type(payload)` → Methode; ausgehend `type(output)` → Kategorie → Hülle.
Ein Command, den ein Handler ohne Client-Basis ausgibt, geht als `CommandRequest` mit gewürfelter `command_id` und `expected_version = -1` hinaus —
der Server sendet ihn im Modus `Emittiert` ohne Versionsprüfung, die Inbox des Ziels dedupliziert über die Command-Id. Eine Ausgabe der Kategorie
EVENT geht als Hinweis (`TransientEventRequest`) an den Server, der ihn an die Verteiler gibt (an die Akteure, die ihn hören); die Kennung würfelt
der Server, weil das SDK keine `causation_id` setzt.

## 9.4 Client-Basis: Arbeit, Kenntnis, Fragen

Akteur und Client stehen in C# (eine Quelle, `Domain.Akteure/Akteure.cs` und `Clients.cs`):

```csharp
public sealed record Klassifizierer : IAkteur,
    IHoert<PaarZuBeurteilen>,                                   // Arbeit: ein Command ohne Ziel
    IDarf<KlassifiziereBildPaarDurchKi>, IDarf<KlassifiziereEinzelBildDurchKi>, IDarf<HoleAktivesModell>,
    IHoert<BildVerfuegbar>, IHoert<ModellAktiviert>;            // Kenntnis, verlierbar

public sealed record KlassifikationsWorker : IClient, IVerkoerpert<Klassifizierer>;
```

Python erbt die generierte Basis und füllt je gehörtem Command `auf_<command>` und je Kenntnis `auf_<event>` (nach
`Domain.Client.Worker.Python.ML/run_stub.py`):

```python
from cqrs_client.arbeit import Herkunft
from domain_client.generated import BildVerfuegbarDto, KlassifiziereBildPaarDurchKiDto, ModellAktiviertDto, PaarZuBeurteilenDto
from domain_client.generated.vertraege import KlassifikationsWorkerBasis

@dataclass
class StubState:
    bilder: dict[UUID, dict[int, str]] = field(default_factory=dict)

class StubClassifier(KlassifikationsWorkerBasis[StubState]):
    async def auf_bild_verfuegbar(self, e: BildVerfuegbarDto, ctx, state: StubState):        # Kenntnis: gibt nichts aus
        state.bilder.setdefault(ctx.aggregate_id, {})[e.version] = e.pfad

    async def auf_paar_zu_beurteilen(self, c: PaarZuBeurteilenDto, h: Herkunft, state: StubState):   # Arbeit des Klassifizierers
        return KlassifiziereBildPaarDurchKiDto(aggregate_id=c.paar, label=0)

    async def auf_modell_aktiviert(self, e: ModellAktiviertDto, ctx, state: StubState): ...

from domain_client.generated.nutzlast_tabelle import TABELLE   # generiert: Nutzlast.typ → DTO-Klasse
StubClassifier(registry=create_registry(), nutzlast=TABELLE).run(host, port)
```

Was die Basis übernimmt (`cqrs_client/vertrag.py`):
- **Arbeit:** je Akteur in `ARBEIT` eine `AkteursArbeit` mit Zugriff auf `state`; angenommen wird sie nach jedem Handshake, je Akteur mit
  `PLAETZE` (Standard 1); Ablauf §9.5.
- **Kenntnis:** Dispatch gehörter Typ → `auf_<event>`; gibt die Methode etwas aus, ist das eine `VertragsVerletzung` — eine Antwort gibt es nur
  auf Arbeit.
- **Vollständigkeit:** fehlt eine Methode, lässt sich die Klasse nicht instanziieren (ABC) — wer einen Akteur verkörpert, muss seine Arbeit
  annehmen können; der Bruch fällt beim Start auf.
- **Registry:** die Typen aus `ARBEIT` (Commands und ihre Antworten), `HOERT`, `SPONTAN` und `FRAGT` ergänzt die Basis in der übergebenen
  `CategoryRegistry`.
- **Handshake:** der `CapabilitiesRequest` nennt den Client (`vertrag`) und seinen Hash (`vertrag_hash`); Befugnisse nimmt der Server aus seiner
  Tabelle, wirksam ist „verkörpert ∩ Token“ für beide Richtungen. Ein abweichender Hash kommt in `CapabilitiesResponse.vertrag_hash` zurück.
- **Fragen:** `await self.query(dto)` liefert die typisierte Antwort (`QueryRequest`/`QueryResponse`, Fehler als `proxy.ServerError`); eine
  Query außerhalb von `FRAGT` ist eine `VertragsVerletzung`. Read-your-writes (`mindestens`) setzt das SDK nicht.
- **Senden:** `SPONTAN` geht in den Handshake (`message_types`); einen Aufruf, mit dem ein Client von sich aus einen Command sendet, hat das SDK
  nicht.

Der Entwickler schreibt nur Fachlogik: State-Dataclass, die Methoden der Basis, Rückgaben bzw. `yield`. Transport, Hüllen, Versioning, oneof,
Reconnect und Prüfungen liegen im SDK (Invariante 5).

**Ohne Client-Basis:** ein `CqrsClient[S]` mit `@handle.register`-Methoden nennt im Handshake selbst, was er hört (`message_types`), sendet
(`_declared_command_types`); wirksam ist auch dann nur, was das Token erlaubt (`Infrastructure/GrpcClient/Handshake.cs`,
`Infrastructure/Akteure/AkteurTor.cs`). Seine Handler dürfen Commands und Hinweise ausgeben. Queries beantwortet ein Client nie: eine Query hat genau
ein Ziel, einen Reader oder ein Aggregat ([10](10-entwickler-api.md) §10.5) — ein Handler auf einer Query bricht den Start ab.

**Dauerhafte Reaktionen** (Schritt 6): nennt die Konfig des Clients `reaktion_stand` (ein Verzeichnis, auch `CQRS_REAKTION_STAND`), reagieren
seine Handler nicht nur auf Pushes, sondern über das Log: der `ReaktionsLaeufer` (`cqrs_client/reaktionen.py`) liest ab dem gesicherten Stand
(`proxy.lies_log`), ruft je Event den Handler, sendet seine Commands mit abgeleiteten Ids (dieselbe Formel wie in C#: MD5, Guid-Bytes
little-endian; ohne `aggregate_id` an den Kopf eines Graphen) und sichert den Stand nach den Antworten. `reaktion_name` (Standard: der
Klassenname) ist der Schlüssel von Stand und Ids — über Neustarts gleich halten.

## 9.5 Arbeit

Hört ein Akteur einen Command ohne Ziel, ist das seine Arbeit ([04](04-konsum-und-prozess-maschine.md) §4.4): die Werkstatt eines Cluster-Knotens
gibt sie einer Sitzung, deren Client den Akteur verkörpert und deren Token ihn nennt, und stellt sie erneut zu, bis eine Antwort kommt. Das SDK
(`cqrs_client/arbeit.py`, `ArbeitsLaeufer`) macht daraus Methodenaufrufe:

1. Nach jedem Handshake meldet der Client `ArbeitAnnehmen` (je Akteur mit Arbeit sein Name und seine Plätze). Die Sitzung prüft, dass sie den Akteur
   verkörpert und das Token ihn nennt (sonst `ARBEIT_NICHT_BEFUGT`; ein Akteur ohne Arbeit: `ARBEIT_UNBEKANNT_AKTEUR`), und meldet sich als
   **Arbeiter** in der Werkstatt ihres Knotens an.
2. Ein `ArbeitsAuftrag` nennt `vorgang` (die Kennung, gleich bei jeder Wiederholung), `fuer` (den Akteur), `eingang` (den Command als Nutzlast) und die
   Herkunft (`akteur`, `stream`, `version`, `zeit_utc`). Das SDK ruft `auf_<command>` dieses Akteurs entkoppelt von der Verarbeitungsschleife,
   höchstens so viele zugleich, wie er Plätze hat, und sendet alle 10 s `ArbeitLebtMeldung`, solange die Methode läuft (ohne Regung gibt der Server
   den Auftrag nach 60 s weiter).
3. Die Antwort — ein Wert, eine Liste, `None` (keine Antwort) oder Stücke (`yield`) — muss aus Commands bestehen, die der Akteur senden darf
   (`AkteursArbeit.pruefe`, sonst `ArbeitsVerletzung`); sie geht als `ArbeitsErgebnis` hinaus (`commands`, `ab` = Stelle des ersten Commands des
   Stücks, `fertig`). Eine Ausnahme oder Verletzung geht als `fehler` hinaus; der Server stellt den Auftrag erneut zu.
4. Der Server sendet die Commands selbst, mit Ids aus dem Auftrag; es zählt nur die Antwort des Arbeiters, der den Auftrag hält, und von ihm die
   erste.

`h` ist die `Herkunft` des Auftrags: `stream`, `version`, `zeit` (Log-Zeit), `akteur`, `korrelation` und `schluessel` (die Kennung des Auftrags, gleich
bei jeder Wiederholung — zum Deduplizieren von Wirkungen nach außen). Ids von Aggregaten stehen als UUID-Text im DTO, z. B. `aggregate_id=c.paar`.

- **Stücke:** ein Generator (Training) schickt jedes Stück sofort (`fertig = false`); der Server sendet es, bevor das nächste kommt. Rechnet ein
  anderer Arbeiter den Auftrag erneut, beginnt er von vorn — die Inbox der Ziele verwirft schon Gesendetes (dieselben Ids).
- **Zustand im Worker** (ein Modell) sichert das Framework nicht; der Worker lädt ihn selbst, etwa auf eine Kenntnis hin (`ModellAktiviert`).

Gestartet wird ein Python-Client über „▶ Worker“ in der Werkbank (`Werkbank/Server/Worker.cs`: `Clients:<Name>:Worker` der Umgebung — Skript,
Argumente, Interpreter, Token) oder von Hand; Adresse und Akteur kommen aus `CQRS_GRPC_HOST`/`CQRS_GRPC_PORT`/`CQRS_AKTEUR_TOKEN`.

## 9.6 Beispieldomäne: Klassifikation und Training

Knapp, weil Prüfling, nicht Architektur:

- **Klassifikation** (`domain_client/classifier.py`, Start `run.py`; Rauchtest ohne Modell `run_stub.py`): erbt `KlassifikationsWorkerBasis`
  (Client `KlassifikationsWorker`, Akteur `Klassifizierer`), merkt sich auf `BildVerfuegbar` die Bildpfade je Aggregat und beantwortet die Arbeit
  `PaarZuBeurteilen`: lädt beide Bilder über `/api/files/` des Blazor-Hosts (`image_loader.py`), führt TorchScript-Inferenz in `asyncio.to_thread`
  aus und gibt `KlassifiziereBildPaarDurchKi` zurück; ohne Bilder wirft sie (der Server stellt erneut zu). Auf `ModellAktiviert` lädt er das neue
  Modell.
- **Training** (`domain_client/training_worker.py`, Rauchtest `run_train_stub.py`): erbt `TrainingsWorkerBasis` (Client `TrainingsWorker`, Akteur
  `TrainingsSystem`), beantwortet `TrainingZuRechnen` in Stücken (`MeldeTrainingBegonnen`, `MeldeFortschritt` je Epoche,
  `MeldeTrainingAbgeschlossen` bzw. `MeldeTrainingGescheitert`), zieht die Samples per Query (`HoleDatensatzSamples`) und bricht auf
  `TrainingAbgebrochen` kooperativ ab; der Trainer ist ein Stub.
- `run.py`/`run_stub.py`/`run_train_stub.py` lesen Host/Port aus der eigenen `appsettings.json`, Umgebungsvariablen gehen vor;
  `domain_client/domain_registry.py` (die `CategoryRegistry` der Domäne) ist handgepflegt (docs/13 P2-11).

Tests: `Client.Infrastructure.Python/tests` (SDK: `test_arbeit`, `test_vertrag`, `test_client_query`, `test_client_reconnect`, `test_mapper_*`) und
`Domain.Client.Worker.Python.ML/tests` (`test_vertraege`, `test_training_worker`); Zahlen in [12](12-tests-und-vermessung.md).

## 9.7 Proto-Abgleich zur Laufzeit

`proto_sync.ensure_types_current()` vergleicht den lokalen Hash mit `GET /api/proto/version` am Blazor-Host und lädt bei Abweichung
`GET /api/proto/domain.proto` nach (`run.py` übergibt dazu `file_base_url` und `generated_dir`). Die Gegenstelle
(`Host.Blazor/ProtoEndpointExtensions.cs`, `MapProtoEndpoint`) wird nirgends gemappt; der Abgleich ruft also ins Leere (docs/13 P3-2). Der
tragende Weg ist der Build (§9.2).

## 9.8 Prinzipien

1. **Typen sind die Wahrheit, nicht Strings** (Invariante 3) — Python-Typen sind die Routing-Schlüssel.
2. **Fachcode bleibt rein** (Invariante 5) — Methoden sehen nur Payload bzw. Command + `ctx` bzw. `Herkunft` + `state`.
3. **Typ-Kenntnis aus generierten Basen** — was ein Client annimmt, hört, sendet und fragt, steht in den aus seinen Akteuren generierten Basen, nicht
   in Laufzeit-Introspektion der Domäne.
4. **Eine Quelle:** Akteure und Clients in C#; `domain.proto` und die Python-Basen werden daraus erzeugt.
5. **Die Garantie bleibt beim Server:** der Client ist bei Arbeit nur die Hand — gesendet wird beim Server, der Auftrag bleibt offen, bis eine
   Antwort kam.

## 9.9 Offen

Offene Punkte stehen nur in [13](13-reifegrad-schulden-bewertung.md) §13.0: kein TLS (`Channel(host, port)` unverschlüsselt), Bild-Download über
`http://` (P1-2); Python-DTOs ohne Drift-Prüfung (FB-20); Lesen ab Position, Ausgangskorb und neu Fragen fehlen im SDK (FB-14); Arbeit ohne
verbundenen Arbeiter staut still (FB-7); Reflection im Codegen (W10-1); handgepflegte Registry und Kleinkram im SDK (P2-11); nicht gemappte
Proto-Endpunkte (P3-2); Akteure und Clients (AK-1).
