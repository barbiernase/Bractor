# 09 — Python-SDK und Worker

Stand 2026-10-29, gegen den Code geprüft.

Zwei Teile, streng getrennt (W11):

- **Framework:** das domänenfreie Paket `cqrs_client` (`Client.Infrastructure.Python/cqrs_client/`) und der generische Funktions-Worker
  (`funktions_worker.py`, liegt heute im Projekt der Beispieldomäne, §9.5).
- **Beispieldomäne:** `Domain.Client.Worker.Python.ML/` — Klassifikation und Training der Bildinspektion, dazu die Python-Umsetzung einer
  Funktion draußen.

Python läuft mit dem Interpreter der Umgebung: der des Workers (`Funktionen:<F>:Worker:Python`), sonst `Umgebung:Python`, sonst `CQRS_PYTHON`,
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
| `vertrag.ClientVertragBasis` | Client-Vertrag + `GeneratedWiring_<Assembly>` |

Er ist **kein** durabler Konsument (kein Cursor, keine Position), sondern ein Client am Rand. Was er tut, sagt sein Client-Vertrag
(`IClientVertrag`, Grammatik in [10](10-entwickler-api.md) §10.3.6, Begriffe in [konzept-akteure.md](konzept-akteure.md)):

- **hört** (`IHoert<T>`): Events und Hinweise als Push über den Verteiler seines Knotens — verlierbar, nur zur Kenntnis;
- **fragt** (`IFragt<T>`): Queries mit typisierter Antwort;
- **trägt Funktionen** (`ITraegt<F>`): er rechnet die Eingänge einer Funktion draußen; Festhalten, Senden und Wiederholen bleiben beim
  Funktions-Actor des Servers (§9.5);
- **sendet** (`ISendet<T>`): steht im Handshake; einen eigenen Sende-Aufruf hat das SDK nicht (§9.4).

Exactly-once bleibt serverseitig: die Ausgaben einer getragenen Funktion hält der Server unter „Funktion, Eingang“ fest, die erste Antwort gewinnt,
gesendet wird mit abgeleiteten Command-Ids aus dem Festgehaltenen ([11](11-garantien.md) §11.6). Der Zustand des Clients lebt im Prozess
(`state`, in-memory); ihn zu sichern, ist Sache des Workers.

## 9.2 Wie Python-Code entsteht: generierte Basen und DTOs

Typ-Kenntnis kommt nicht aus Laufzeit-Introspektion der Domäne, sondern aus generierten Dateien. Der Entwickler erbt eine Basis und füllt
Methoden.

| Datei | Erzeugt von | Inhalt |
|---|---|---|
| `domain_client/generated/__init__.py` | `Domain.Client.Worker.Python.ML/build_python.sh` (`grpc_tools.protoc --python_betterproto_out` aus `Domain.Leitung/domain.proto`) | betterproto-DTOs (`…Dto`) der Anwendung; `Id<T>` ist darin ein UUID-Text |
| `cqrs_client/leitung/__init__.py` (SDK) | `Client.Infrastructure.Python/build_leitung.sh` (aus `Cqrs.Leitung/cqrs.proto`, committet) | die Leitung des Frameworks: `ClientMessage`/`ServerMessage`, Hüllen, `Nutzlast` |
| `domain_client/generated/vertraege.py` | `Cqrs.Codegen` (`PythonVertragsEmitter`) | je Client-Vertrag (`IClientVertrag`) eine abstrakte Basis `<Client>Basis` mit `CLIENT`, `AKTEURE`, `VERTRAG`, `VERTRAG_HASH`, `TRAEGT`, `HOERT`, `SPONTAN` (`ISendet`), `FRAGT`; je getragener Funktion eine Methode `<funktion>(e, h, state)`, je gehörtem Typ `auf_<typ>(e, ctx, state)` |
| `domain_client/generated/funktionen.py` | `Cqrs.Codegen` (`PythonFunktionsEmitter`) | je Funktions-Schnittstelle ohne Lese-Fähigkeit eine Basis `<Funktion>Basis` mit `FUNKTION`, `EINGAENGE` und je `Auf` abstrakt `auf_<eingang>(e, h)` |
| `domain_client/generated/funktions_tabelle.py` | `Cqrs.Codegen` | `TABELLE`: Funktion → (Basis, Modul, Klasse der Implementierung) für den generischen Worker |
| `domain_client/generated/nutzlast_tabelle.py` | `Cqrs.Codegen` (`PythonNutzlastEmitter`) | `TABELLE`: `Nutzlast.typ` → DTO-Klasse — Gegenstück zu `GeneratedNutzlast`, dieselbe Namensregel (`NameSanitizer.NutzlastTyp`); der Client bekommt sie als `nutzlast=` |

Die Basen lesen nur Signaturen (`PythonFunktionsform`, `Cqrs.Codegen/PythonFunktionsEmitter.cs`): je `Auf` Eingang, Ausgaben mit ihrer Art
(`COMMAND`, `EVENT`, `HINWEIS`), ob die Ausgabe ein Strom ist (`IEnumerable`/`IAsyncEnumerable`) und ob der Eingang ein Hinweis ist
(`ITransientEvent`). Eine Funktion mit einem Parameter außer Eingang, `Herkunft` und `CancellationToken` (Lese-Fähigkeit) läuft nur im Host und
bekommt keine Basis.

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
| **client** | `CqrsClient[S]`: Lebenszyklus (Proxy, Mapper, Router, Verbindung, Versioning, `FunktionsLaeufer`), Handshake, `query()` (typisierte Antwort), `run(host, port)`; je Verbindung eine Sitzung (Lesen + Verarbeiten), nach Abbruch neu verbinden und Funktionen neu anbieten; Akteur-Token aus der Konfig oder `CQRS_AKTEUR_TOKEN` |
| **vertrag** | `ClientVertragBasis`, `VertragsVerletzung`, `VertragsMeta` — Basis der generierten Client-Basen (§9.4) |
| **funktion** | `FunktionsBasis`, `GetrageneFunktion`, `FunktionsLaeufer`, `Eingang`, `Ausgabe`, `Herkunft`, `FunktionsVerletzung` — Funktionen draußen (§9.5) |
| **dispatch** | Typ-Dispatch: Eingangs-Typ → Methode (`HandleDescriptor`, `@handle.register`, Metaklasse `HandlerMeta`) |
| **registry** | `CategoryRegistry`: betterproto-Typ → EVENT/COMMAND/QUERY/QUERY_RESPONSE; `ergaenzt(...)` nimmt die Typen eines Vertrags auf |
| **router** | `MessageRouter`: Verarbeitungsschleife über die Eingangs-Queue — `event` → Dispatch → Ausgaben nach Kategorie zurück; `query_forward` → Antwort; `arbeits_auftrag` → `FunktionsLaeufer.nimm` (läuft entkoppelt) |
| **mapper** | `NutzlastTabelle` (`packe`/`entpacke`/`typ`, nur über die generierte Tabelle) und `PayloadMapper` (`wrap_command`, `wrap_transient_event`, `wrap_query`, `wrap_query_response`, `extract_*`) |
| **proxy** | bidirektionaler gRPC-Transport über grpclib-Lowlevel (`STREAM_STREAM`, unabhängiges Senden/Empfangen, ein Sende-Lock); Akteur-Token im Header `akteur-token`; Queries über Futures (30 s); Senden von Commands, Hinweisen, Query-Antworten, `FunktionenAnbieten`, `ArbeitsErgebnis`, `ArbeitLebtMeldung`; `aktualisiert` wird nur geloggt |
| **connection** | `ConnectionManager`: Reconnect mit exponentiellem Backoff (1 s → 30 s), Handshake erneut, danach `on_connected` |
| **versioning** | `dict[UUID → höchste gesehene Version]`, aus Push-Events (`max(aggregate_version, stream_head_version)`) |
| **proto_sync** | Laufzeit-Abgleich gegen `/api/proto/*` am Blazor-Host (§9.7) |

**Routing über Typen:** eingehend `betterproto.which_one_of` → `type(payload)` → Methode; ausgehend `type(output)` → Kategorie → Hülle.
Ein Command, den ein Handler ausgibt, geht als `CommandRequest` mit gewürfelter `command_id` und `expected_version = -1` hinaus — der Server
sendet ihn im Modus `Emittiert` ohne Versionsprüfung, die Inbox des Ziels dedupliziert über die Command-Id. Eine Ausgabe der Kategorie EVENT
geht als Hinweis (`TransientEventRequest`) an den Server, der ihn an den Verteiler und das Hinweis-Netz gibt; die Kennung würfelt der Server,
weil das SDK keine `causation_id` setzt.

## 9.4 Client-Vertrag: hören, fragen, senden

Der Vertrag steht in C# (eine Quelle, `Domain.Akteure/Clients.cs`):

```csharp
public interface IKlassifikationsWorker : IClientVertrag,
    ITraegt<IBildpaarBeurteilung>,
    ISendet<KlassifiziereEinzelBildDurchKi>,
    IFragt<HoleAktivesModell>,
    IHoert<BildVerfuegbar>, IHoert<ModellAktiviert>;
```

Python erbt die generierte Basis und füllt je getragener Funktion eine Methode und je gehörtem Typ `auf_<typ>` (nach
`Domain.Client.Worker.Python.ML/run_stub.py`):

```python
from cqrs_client.funktion import Herkunft
from domain_client.generated import BildVerfuegbarDto, ImagePairKomplettDto, KlassifiziereBildPaarDurchKiDto, ModellAktiviertDto
from domain_client.generated.vertraege import KlassifikationsWorkerBasis

@dataclass
class StubState:
    bilder: dict[UUID, dict[int, str]] = field(default_factory=dict)

class StubClassifier(KlassifikationsWorkerBasis[StubState]):
    async def auf_bild_verfuegbar(self, e: BildVerfuegbarDto, ctx, state: StubState):     # IHoert: gibt nichts aus
        state.bilder.setdefault(ctx.aggregate_id, {})[e.version] = e.pfad

    async def bildpaar_beurteilung(self, e: ImagePairKomplettDto, h: Herkunft, state: StubState):   # ITraegt
        return KlassifiziereBildPaarDurchKiDto(aggregate_id=h.stream, label=0)

    async def auf_modell_aktiviert(self, e: ModellAktiviertDto, ctx, state: StubState): ...

from domain_client.generated.nutzlast_tabelle import TABELLE   # generiert: Nutzlast.typ → DTO-Klasse
StubClassifier(registry=create_registry(), nutzlast=TABELLE).run(host, port)
```

Was die Basis übernimmt (`cqrs_client/vertrag.py`):
- **Hören:** Dispatch gehörter Typ → `auf_<typ>`; gibt die Methode etwas aus, ist das eine `VertragsVerletzung` — eine Ausgabe auf ein Event ist
  eine getragene Funktion, keine Kenntnis.
- **Vollständigkeit:** fehlt eine Methode, lässt sich die Klasse nicht instanziieren (ABC) — der Bruch fällt beim Start auf.
- **Registry:** die Typen aus `HOERT`, `SPONTAN` und `FRAGT` ergänzt die Basis in der übergebenen `CategoryRegistry`.
- **Handshake:** der `CapabilitiesRequest` nennt Client (`vertrag`) und `vertrag_hash`; Befugnisse nimmt der Server aus seiner Tabelle, wirksam
  ist Vertrag ∩ Token. Ein abweichender Hash kommt in `CapabilitiesResponse.vertrag_hash` zurück.
- **Fragen:** `await self.query(dto)` liefert die typisierte Antwort (`QueryRequest`/`QueryResponse`, Fehler als `proxy.ServerError`); eine
  Query außerhalb von `FRAGT` ist eine `VertragsVerletzung`. Read-your-writes (`mindestens`) setzt das SDK nicht.
- **Getragene Funktionen:** je Eintrag in `TRAEGT` eine `GetrageneFunktion` mit Zugriff auf `state`; Gleichzeitigkeit über `SLOTS`
  (Funktion → Zahl, Standard 1); Ablauf wie §9.5.
- **Senden (`ISendet`):** `SPONTAN` geht in den Handshake (`message_types`); einen Aufruf, mit dem ein Vertrags-Client von sich aus einen Command
  sendet, hat das SDK nicht.

Der Entwickler schreibt nur Fachlogik: State-Dataclass, die Methoden des Vertrags, Rückgaben bzw. `yield`. Transport, Hüllen, Versioning, oneof,
Reconnect und Vertragsprüfung liegen im SDK (Invariante 5).

**Ohne Vertrag:** ein `CqrsClient[S]` mit `@handle.register`-Methoden nennt im Handshake selbst, was er hört (`message_types`), sendet
(`_declared_command_types`); wirksam ist auch dann nur, was das Token erlaubt (`Infrastructure/GrpcClient/Handshake.cs`,
`Infrastructure/Akteure/AkteurTor.cs`). Seine Handler dürfen Commands und Hinweise ausgeben. Queries beantwortet ein Client nie: eine Query hat genau
ein Ziel, einen Reader oder ein Aggregat ([10](10-entwickler-api.md) §10.5) — ein Handler auf einer Query bricht den Start ab.

## 9.5 Funktionen draußen (Laufort `Extern`)

Eine Funktion draußen ist eine Schnittstelle `: IFunktion` in C# ([10](10-entwickler-api.md) §10.3.4); ihr Laufort steht in der Umgebung
(`Funktionen:<F>:Laufort = Extern`, `FunktionsKonfig`). In Python gibt es sie in zwei Formen, beide mit derselben Mechanik
(`FunktionsLaeufer`, `cqrs_client/funktion.py`):

- **getragen von einem Client-Vertrag** (`ITraegt<F>`): Methode `<funktion>(e, h, state)` der Client-Basis (§9.4);
- **als eigene Klasse:** erbt `<Funktion>Basis` aus `generated/funktionen.py`, füllt `async def auf_<eingang>(self, e, h)`, setzt `SLOTS`
  (bzw. `slots=`) und bekommt Werte als `konfig=`; die Instanz geht als `funktionen=[…]` an einen `CqrsClient`.

Die Methode gibt einen Wert, `None` oder einen Strom (Async-Generator) zurück; jede Ausgabe muss ein Fall der Signatur sein (`FunktionsBasis.pruefe`),
mehr als eine Ausgabe ohne Strom ist eine `FunktionsVerletzung`. `h` ist die `Herkunft` des Eingangs: `stream`, `version`, `zeit` (Log-Zeit),
`akteur`, `korrelation` und `schluessel` (der Schlüssel „Funktion, Eingang“, gleich bei jeder Wiederholung — zum Deduplizieren von Wirkungen
nach außen). Ids von Aggregaten stehen als UUID-Text im DTO, z. B. `aggregate_id=h.stream`.

**Der Weg eines Eingangs** (Server: `Infrastructure/Funktionen/`, `Infrastructure/GrpcClient/SitzungsActor.cs`):

1. Der `FunktionsActor` der Adresse (Partition bzw. `[EineAdresse]`) liest den Eingang aus dem Log (oder bekommt einen Hinweis aus dem
   Hinweis-Netz) und bietet ihn als `EingangAnbieten` dem `FunktionsVermittlerActor` der Funktion an (je Funktion einer im Cluster, nicht
   maßgeblich — geht er verloren, bietet der Actor neu an; `FunktionsVermittlung.cs`).
2. Der Client meldet nach jedem Handshake `FunktionenAnbieten` (Funktion + Slots, `FunktionsLaeufer.biete_an`). Seine Sitzung prüft die Befugnis
   (`IDarf<F>` des Akteurs bzw. `ITraegt<F>` des Vertrags; sonst `FUNKTION_NICHT_BEFUGT`, unbekannt `FUNKTION_UNBEKANNT`) und startet je Funktion
   eine Hol-Schleife (`BausteinSitzung`): Long-Poll beim Vermittler (`HoleEingaenge`, 4 s), so viele Eingänge wie Slots frei sind, jeder mit
   **Lease** (30 s, `FunktionsAnbieter.Lease`).
3. Die Sitzung reicht den Eingang als `ArbeitsAuftrag` weiter (`vorgang`, `funktion`, `eingang` als Nutzlast, `akteur`, `stream`, `version`,
   `zeit_utc`, `ab`, `hinweis`). Das SDK rechnet ihn entkoppelt von der Verarbeitungsschleife, höchstens `SLOTS` zugleich, und sendet alle 10 s
   `ArbeitLebtMeldung`, solange er läuft.
4. Die Antwort kommt als `ArbeitsErgebnis` (`ausgaben` als `FunktionsAusgabe` mit Art `command`/`event`/`hinweis`, `ab`, `fertig`, `fehler`).
   Der Server prüft sie gegen die Signatur, hält sie unter „Funktion, Eingang“ fest (`IAusgabeAblage`, die erste gewinnt) und meldet
   `EingangBeantwortet` an den Funktions-Actor, der aus dem Festgehaltenen sendet. Ein `fehler` (auch eine Ausnahme oder eine Verletzung der
   Signatur im SDK) zählt beim Actor gegen die Wiederholungen.

- **Strom:** jede Ausgabe geht sofort als Stück (`fertig = false`, `ab` = ihre Stelle); der Server hält es fest und sendet es, bevor das nächste
  kommt. Übernimmt ein neuer Träger, nennt `ab` im Auftrag, wie viele Ausgaben schon festgehalten sind — das SDK lässt sie aus.
- **Hinweis als Eingang** (`hinweis = true`): höchstens einmal, nichts wird festgehalten; die Antwort kommt als Ganzes (auch ein Strom), ihre
  Commands und Hinweise sendet der Funktions-Actor ([04](04-konsum-und-prozess-maschine.md) §4.4).
- **Bindung je Adresse:** alle Eingänge eines Funktions-Actors rechnet derselbe Träger, solange er lebt (Lease je Funktions-Actor, `Vermittlung`);
  endet seine Sitzung, meldet sie ihn ab (`TraegerAbgemeldet`), seine Adressen sind sofort frei. So sieht ein Zustand im Worker (ein Modell)
  alles, was zu ihm gehört — gesichert wird er nicht vom Framework.
- **Lebenszyklus** (optional, Gegenstück zu `ILebenszyklus`): `async def beim_start(self)` einmal je Worker, bevor die Funktion angeboten wird
  (wirft es, startet der Worker nicht); `async def beim_ende(self)` einmal beim Herunterfahren, nachdem laufende Eingänge abgewartet sind
  (`FunktionsLaeufer.starte`/`beende`). Eine getragene Funktion eines Vertrags hat diese Haken nicht; sie nutzt den Zustand des Clients.

**Generischer Worker** `funktions_worker.py --funktionen F1,F2 [--slots n] [--konfig NAME=WERT …] [--pfad <verz>]` (Standard 2 Slots): kennt keine
Domäne, lädt je Funktion die Implementierung nach `funktions_tabelle.TABELLE` (`funktionen/<modul>.py`) und prüft, dass sie die Basis erbt.
Adresse und Akteur kommen aus `CQRS_GRPC_HOST`/`CQRS_GRPC_PORT`/`CQRS_AKTEUR_TOKEN`; der Akteur des Tokens braucht `IDarf<F>`. Gestartet über
„▶ Worker“ in der Werkbank (`Werkbank/Server/Worker.cs`: `Funktionen:<F>:Worker` der Umgebung, sonst dieser Worker mit `--funktionen <F>`) oder
von Hand. Er liegt im Projekt der Beispieldomäne und importiert `domain_client.domain_registry` (docs/13 W2-1).

## 9.6 Beispieldomäne: Klassifikation, Training, Bildaufbereitung

Knapp, weil Prüfling, nicht Architektur:

- **Klassifikation** (`domain_client/classifier.py`, Start `run.py`; Rauchtest ohne Modell `run_stub.py`): erbt `KlassifikationsWorkerBasis`
  (`IKlassifikationsWorker`), merkt sich auf `BildVerfuegbar` die Bildpfade je Aggregat, trägt `IBildpaarBeurteilung`: lädt beide Bilder über
  `/api/files/` des Blazor-Hosts (`image_loader.py`), führt TorchScript-Inferenz in `asyncio.to_thread` aus und gibt
  `KlassifiziereBildPaarDurchKi` zurück; ohne Bilder wirft sie (der Funktions-Actor wiederholt). Auf `ModellAktiviert` lädt er das neue Modell.
- **Training** (`domain_client/training_worker.py`, Rauchtest `run_train_stub.py`): erbt `TrainingsWorkerBasis` (`ITrainingsWorker`), trägt
  `ITraining` als Strom (`MeldeTrainingBegonnen`, `MeldeFortschritt` je Epoche, `MeldeTrainingAbgeschlossen` bzw. `MeldeTrainingGescheitert`),
  zieht die Samples per Query (`HoleDatensatzSamples`) und bricht auf `TrainingAbgebrochen` kooperativ ab; der Trainer ist ein Stub.
- **Funktion als eigene Klasse:** `funktionen/bildaufbereitung.py` (`IBildaufbereitung`, OpenCV: verkleinern, Kontrast ausgleichen, PNG nach
  `konfig["AusgabePfad"]`) — das Gegenstück zur Umsetzung im Host, gestartet über den generischen Worker.
- `run.py`/`run_stub.py`/`run_train_stub.py` lesen Host/Port aus der eigenen `appsettings.json`; `domain_client/domain_registry.py` (die
  `CategoryRegistry` der Domäne) ist handgepflegt (docs/13 P2-11).

Tests: `Client.Infrastructure.Python/tests` (SDK: Vertrag, Funktion, Query, Reconnect, Mapper) und `Domain.Client.Worker.Python.ML/tests`
(Verträge, Funktionen, generischer Worker, Training); Zahlen in [12](12-tests-und-vermessung.md).

## 9.7 Proto-Abgleich zur Laufzeit

`proto_sync.ensure_types_current()` vergleicht den lokalen Hash mit `GET /api/proto/version` am Blazor-Host und lädt bei Abweichung
`GET /api/proto/domain.proto` nach (`run.py` übergibt dazu `file_base_url` und `generated_dir`). Die Gegenstelle
(`Host.Blazor/ProtoEndpointExtensions.cs`, `MapProtoEndpoint`) wird nirgends gemappt; der Abgleich ruft also ins Leere (docs/13 P3-2). Der
tragende Weg ist der Build (§9.2).

## 9.8 Prinzipien

1. **Typen sind die Wahrheit, nicht Strings** (Invariante 3) — Python-Typen sind die Routing-Schlüssel.
2. **Fachcode bleibt rein** (Invariante 5) — Methoden sehen nur Payload + `ctx` + `state` bzw. Eingang + `Herkunft` (+ `state`).
3. **Typ-Kenntnis aus generierten Basen** — was ein Client hört, sendet, fragt und trägt, steht in den aus dem C#-Vertrag generierten Basen, nicht
   in Laufzeit-Introspektion der Domäne.
4. **Eine Quelle:** der C#-Vertrag; `domain.proto` und die Python-Basen werden daraus erzeugt.
5. **Die Garantie bleibt beim Server:** der Client ist bei Funktionen nur die Hand — festgehalten und gesendet wird beim Funktions-Actor.

## 9.9 Offen

Offene Punkte stehen nur in [13](13-reifegrad-schulden-bewertung.md) §13.0: kein TLS (`Channel(host, port)` unverschlüsselt), Bild-Download über
`http://` (P1-2); Python-DTOs ohne Drift-Prüfung (FB-20); Lesen ab Position, Ausgangskorb und neu Fragen fehlen im SDK (FB-14); Python-Funktion
mit Hinweis-Eingang nur geteilt nachgewiesen (FB-8); Funktion draußen ohne Träger staut still (FB-7); Bindung je Adresse ungemessen (PF-35);
Reflection im Codegen und abweichende Vertrags-Lesart zwischen Python-Emitter und Server (W10-1); generischer Worker im Domänen-Projekt (W2-1);
handgepflegte Registry und Kleinkram im SDK (P2-11); nicht gemappte Proto-Endpunkte (P3-2); Akteure und Clients (AK-1).
