# 13 — Reifegrad, Schulden & Bewertung

**Die eine Quelle für „offen“.** Andere Dokumente (`CLAUDE.md`, Kapitel 01–12, Konzepte) nennen Schulden höchstens kurz und verweisen
hierher. Stand: 2026-10-13, Commit-Basis `1b1751a`. Maßstab sind die sechs Invarianten (`CLAUDE.md`) und die Werkbank-Leitsätze W1–W12
(`docs/konzept-werkbank.md` §2). Zahlen und Messwerte stehen in `docs/12-tests-und-vermessung.md`.

## 13.0 Offen auf einen Blick

| ID | Kurz | Prio |
|---|---|:--:|
| W10-1 | Reflection im Codegen (`MetadataLoadContext`) und im Werkbank-Schema | P1 |
| W10-2 | Reflektive Serialisierung zur Laufzeit (Marten-Default, Marking-Store, Snapshot-`Clone`, HTTP-Antworten); kein Reflection-Verbot im Build | P1 |
| W2-1 | Feste Projekt- und Namespace-Namen in Komposition, Codegen und Werkbank | P1 |
| T-1 | Testlücke seit `fb07f5e`: keine Saga-E2E-, Snapshot- und Marking-Äquivalenz-Tests | P1 |
| P1-1 | Multi-Node nur container-erprobt, Consul im `-dev`-Modus | P1 |
| P1-2 | Transport-Sicherheit: kein TLS; Identitäten für Prod fehlen | P1 |
| P1-6 | `CancellationToken.None` im generierten Pull-Emit-Pfad | P1 |
| P1-7 | Deploy-Skripte veraltet (starten die Hosts ohne Umgebung, alte Konfig-Schlüssel) | P1 |
| W11-1 | Doppelte Wege: Handle-Pipeline/Trigger/`[Ingress]` ohne Nutzer, Prozess- vs. Fluss-DSL, zwei Rumpf-Schreibwege | P2 |
| W11-2 | Zeitlimits nur über den 15-s-Backstop entdeckt; Fristplaner hängt an der Handle-Welt | P2 |
| W3-1 | Ableitungen im Editor-JS (Karten-Ids, Hör-Menge, Lambda-Parameter, Auto-Zuordnung, Kardinalität) | P2 |
| WB-1 | Werkbank: Geste p50 unter Fremdlast 346–470 ms (Ziel 300 ms), tote Endpunkte, Formular/Spiegel lesen `domain-model.json` | P2 |
| WZ-1 | Prüf-Lücken der Werkzeuge: Grammatik-Gate bestätigt sich selbst, Fixpunkt ohne Akteure/Verträge/Clients/Funktionen/Dienste, Sonde ohne `SendeJe`/Count-Join/Kompensation/`IMensch`/`IstClient` | P2 |
| AK-1 | Akteure/Clients: durable Zustellung an Clients, `als` im Envelope, Zusagen über C#, Anmeldung je Person | P2 |
| P2-4 | `BoundedInbox` nicht airtight (FIFO ab 10 000) | P2 |
| P2-5 | Marking-/Kompensations-Feinschliff, `KlärungNötig` ohne Test | P2 |
| P2-6 | Schreibpfad: paralleler Drain skaliert sublinear | P2 |
| P2-8 | Hartkodierte Poll-Intervalle und Test-Verbindungsdaten | P2 |
| P2-9 | Toter Lesepfad `LoadStateAsync`, Identitäts-Vertrag nur per Kommentar | P2 |
| P2-10 | Monitoring schmal, Default-Credentials, kein OTel, Logging per `Console.WriteLine` | P2 |
| P2-11 | Python: Registry von Hand gepflegt (`generate_registry.py` ist ein Stub) | P2 |
| P3-1 | Mojibake in drei Dateien | P3 |
| P3-2 | Toter Code und Kleinkram | P3 |
| P3-3 | Saga-DSL in `Cqrs.Testing` kennt `Rufe` nicht | P3 |

## 13.1 Gesamteinschätzung

Das Projekt ist ein **konzeptionell kohärentes** CQRS/ES-Framework: wenige tragende Ideen (Log = Wahrheit, Single-Activation,
generiertes typbasiertes Dispatching, ein Emit-Weg, scharfe Durabilitätsgrenze, vier Konsumenten als eine Maschine) sind mit Disziplin
durchgezogen — bis in den Multi-Node-Transport, die Clients (Blazor und Python mit Vertrag) und die Werkbank. Die Compile-Zeit-Guards
(62 Diagnose-IDs in 12 Analyzern und den Generatoren, dazu Boot-Guards) und die ehrliche Test-Kultur („nie faken, was man nicht besitzt“)
heben es über typische Eigenbau-Frameworks.

**Der Kern ist solide und gemessen grün.** Die offenen Punkte liegen fast durchweg an zwei Stellen: an den **Rändern** (Transport-Sicherheit,
Multi-Node-Betrieb) und an der **Allgemeingültigkeit** (W2/W10/W11): Komposition und Werkzeuge kennen die Projekte der Beispieldomäne beim Namen,
Werkzeuge lesen teils per Reflection, und neben den neuen Wegen (Fluss, Quelle, Umgebung) liegt noch die alte Handle-/Trigger-Welt.

| Subsystem | Reife |
|---|:--:|
| Schreibseite | 🟢 |
| Konsum-/Prozess-Maschine | 🟢 (Testlücke T-1) |
| Generatoren & Analyzer | 🟢 / 🟡 (W10-1, W2-1) |
| Multi-Node / Transport | 🟢 / 🟡 (P1-1, P1-2) |
| Werkbank (Extractor, Editor, Server) | 🟢 / 🟡 (W3-1, WB-1, WZ-1) |
| Clients (Blazor, Python) | 🟢 / 🟡 (AK-1, P1-2) |
| Tests & Vermessung | 🟢 / 🟡 (T-1) |

## 13.2 Stärken

1. **Invarianten-Disziplin über Sprachgrenzen.** Dieselben Invarianten tragen Backend, Blazor und Python.
2. **Compile-Zeit statt Laufzeit.** Analyzer, Generator-Diagnosen und Boot-Guards verwandeln ganze Fehlerklassen (Fehlrouting,
   Dangling-Command, unbounded Token, Wire-Drift, zyklische Regeln, falsche Fluss-Verdrahtung, offene Ausgänge) in Build-Fehler.
3. **Abläufe als typisierte DSL** (Prozess, Fluss) — der Fachcode ist nur die Regel; Dirigent, Korrelation, Marking und Kompensation sind einmal
   geschrieben.
4. **Ehrliche Nicht-Autoritativität.** Jeder Beschleuniger (Redis, Snapshot, Marking-Cursor) hat einen Voll-Read-Fallback.
5. **Selbstbeobachtung.** Die Werkbank zeigt das System als Karten aus dem Code; durchgespielt wird das echte System, sichtbar über die Spur.
6. **Test-Kultur.** Store-Semantik nur gegen echtes Marten; generative Vollständigkeits-Guards; Fremd-Domäne als Agnostik-Beweis (`--sonde`).

## 13.3 Befunde

Schweregrade: **P1** = wichtig, produktions- oder allgemeingültigkeitsrelevant · **P2** = Wartbarkeit/Hygiene · **P3** = kosmetisch.
Aktuell gibt es keinen P0 (kein bekannter aktiver Korrektheits-Bug).

### P1

**W10-1 · Reflection in Werkzeugen.** `Cqrs.Codegen` liest die gebauten Domänen-DLLs per `MetadataLoadContext` und erzeugt daraus `domain.proto`,
die STJ-Kontexte und die Python-Basen (`vertraege.py`, `funktionen.py`, `funktions_tabelle.py`). Daneben existiert mit `Proto.SourceGeneration` ein
zweiter, Roslyn-basierter Leseweg, der schon abweicht (Filter `IProzessIntern`). Der Python-Vertrags-Emitter liest Verträge anders als
`Infrastructure.SourceGeneration/Clientvertragslesart.cs` (Ausgaben nicht auf `ICommand` gefiltert, Kenntnis nicht auf Events), der
Handshake-Hash kann also abweichen. `Werkbank/Server/Schema.cs` liest die Konfig-Typen per `GetProperties`/`Activator` aus der eigenen DLL
(das Formular veraltet nach einer Änderung der Konfig-Typen bis zum Neustart).
*Empfehlung:* ein Codegen-Kern über `IAssemblySymbol`, der nur bestehende Roslyn-Leser nutzt (`DomainGraphAnalyzer`/`FileGenerator` wie der
DtoMapper, `Clientvertragslesart`, Lesart des `FunktionsGenerator`); Wirte: warmer Workspace bei ⟳ und ein MSBuild-Schritt mit
`CSharpCompilation` über MetadataReferences. Schema aus den Symbolen des warmen Workspace. `Proto.SourceGeneration` darin aufgehen lassen.

**W10-2 · Reflektive Serialisierung zur Laufzeit.** Marten serialisiert Events und Dokumente per Default reflektiv (`Schreibpfad.GeneriertesJson`
= false; Fallback `DefaultJsonTypeInfoResolver` in `CqrsServiceExtension.cs`); `MartenProzessMarkingStore` nutzt `JsonSerializer` mit
`GetType()` und Typ per FullName; `AggregateActorBase.Clone` macht einen JSON-Round-Trip; `JsonEingang`, `EinblickEndpunkte`, Monitoring und
`Quellen` antworten mit anonymen Typen; Minimal-API ohne Request-Delegate-Generator; Typprüfungen per `IsAssignableFrom`/`IsInstanceOfType`;
`ActivatorUtilities` im generierten Client-Wiring. Es gibt **keinen** Analyzer und keine BannedApi gegen `System.Reflection`.
*Empfehlung:* generierter Kontext als Standard (plus Dokument-Kontext), `Kopie()` vom State-Generator, benannte Records + Kontext für HTTP,
`EnableRequestDelegateGenerator`, `JsonSerializerIsReflectionEnabledByDefault=false` in den Hosts als Sperre.

**W2-1 · Feste Projekt- und Namespace-Namen.**
- `Infrastructure.csproj` referenziert Domänen-Projekte; `Infrastructure` ist damit zugleich Framework und Komposition.
- `ProzessManagerWiring.AlleRegeln` vereinigt fest `Domain.Prozess` + `Domain.Pipeline.Prozess`; `ProzessRegelnGenerator` prüft
  `AssemblyName != "Domain"` — Prozesse in einer dritten Assembly würden still nie registriert.
- Generate in festen Namespaces: `ProjectionQueryServiceGenerator` (`Domain.Projections`), `ProjectionServicesGenerator`
  (`Domain.Infrastructure.Generated`), `FactoryGenerator` (`Infrastructure.AggregateHandlerFactory` je Domänen-Assembly).
- Projektlisten: `Cqrs.Codegen/Program.cs`, `Proto.SourceGeneration/Program.cs`, `codegen.sh`, `ProtoRepo.csproj` (Inputs **ohne**
  `Domain.Akteure` → eine reine Vertragsänderung überspringt den Codegen), `Werkbank/Server/WerkbankServer.cs` (Stand-Hash, Host-Projekt,
  Debug-Pfad, Python-Projekt), `Werkbank/Server/Worker.cs` (generischer Worker im Domänen-Projekt).
- Das Client-Framework `Client.Infrastructure` referenziert `Domain` und `Infrastructure` (`Client.Infrastructure.csproj`).
- Framework-Code im Domänen-Projekt: `MartenCoCommitStoreBase` (die Co-Commit-Basis aller Stores) liegt in `Domain.Infrastructure`, der generische
  `funktions_worker.py` in `Domain.Client.Worker.Python.ML`.
- Namespace-Ausschlusslisten statt Fakt (`Ctorbindung.cs` mit `OpenCvSharp`, `FunktionsGenerator`, `QuellenGenerator`, `WiringGenerator`).
- Identitäten aus einfachen Typnamen (`BrokerIdentity`, `GeneratedAkteurRechte`); der Client darf das Cluster-Kind eines Commands
  (`AggregateType`) selbst setzen (`CqrsClientService.cs`, `AggregateDispatcher.cs:75`).
*Empfehlung:* ein Laufzeit-/App-Projekt als Komposition; Registrierung über generierte Assembly-Attribute; `GraphExtractor.Kern/Projektlage.cs`
als einzige Quelle der Projekt-Rollen; `AggregateType` serverseitig aus `GeneratedCommandRouting`.

**T-1 · Testlücke seit Commit `fb07f5e` (2026-08-18).** Mit den Beispiel-Domänen Konto/Überweisung/Reise/Lager wurden die Saga-E2E-Tests,
`SnapshotStorePostgresTests`/`SnapshotLiveE2ETests`, die Upcasting-Tests und die Marking-Cursor-Äquivalenz gelöscht. Prozess-Kompensation,
Diamant-Sagas, Snapshot-Roundtrip und die Gleichheit Tail-Cursor-Fold == Voll-Fold sind heute ungetestet.
*Empfehlung:* domänenneutral wiederherstellen (Test-Domäne im Testprojekt, nicht in der Beispieldomäne).

**P1-1 · Multi-Node nur container-erprobt.** Cross-Node ist über alle Planes verdrahtet und bewiesen (`TwoNodeCommandDispatchTests`,
`RemotePidDeliverySmokeTests`, 3-Node-Compose), der Regelbetrieb ist aber Single-Node; Consul läuft im `-dev`-Modus (kein Quorum).
*Empfehlung:* Consul mit Quorum/Persistenz, Betriebsrezept.

**P1-2 · Transport-Sicherheit.** gRPC ist h2c ohne TLS, Blazor und Python verbinden unverschlüsselt. Authentifizierung über Akteur-Token,
Akteur-Tor und Vertrag ∩ Token ist da; die Prod-Umgebung hat aber keine `Identitaeten` (Tor offen), der Quell-Webhook hat kein Tor, eine Anmeldung
je Person fehlt. *Empfehlung:* TLS (h2) bzw. mTLS, Identitäten für Prod, Tor am Webhook.

**P1-6 · `CancellationToken.None` im generierten Pull-Emit-Pfad** (`PullPathGenerator.cs:238`). Gebunden nur durch die interne Frist des
Emitters; CQRS021 greift im Generat nicht. *Empfehlung:* gebundenen Token durchreichen.

**P1-7 · Deploy-Skripte veraltet.** `deploy-linux/start-server.sh`, `deploy/deploy.sh`, `deploy/deploy_clients.sh`, `deploy/cqrs-blazor.env` und
`deploy-windows/start.ps1` starten die Hosts ohne `--umgebung`/`CQRS_UMGEBUNG` (Startfehler) und setzen alte Schlüssel (`Pipeline__WatchPath`,
`Blazor__Urls`, `GrpcServer__Address`, `ConnectionStrings__EventStore`); `deploy-*/grpc/appsettings.json` tragen noch `WatchPath`; `build.sh` enthält
eine Umgebung als Heredoc neben `umgebungen/*.json`. *Empfehlung:* Deploy auf `umgebungen/umgebung.prod.json` + Umgebungsvariablen umstellen.

### P2

**W11-1 · Doppelte Wege.** Ohne Domänen-Nutzer, aber verdrahtet: die `IPipelineHandler`-Maschinerie (`PipelineActorBase`, `PipelineEventPullBridge`,
`PipelineDispatchGenerator`, `GeneratedPipelines`), die Trigger-Welt (`IPipelineTrigger`, `ITriggerRegistration`, `TimerTrigger`, `WebhookTrigger`,
gRPC-Trigger-Pfad, Proto `Trigger*`), `[Ingress]` mit `GraphExtractor.Kern/CompositionRoot.cs`, `Selbst<T>`/`Frist<TCmd>`/`FristStorno<TCmd>` als
Handle-Ausgang, `IAkteurDienst<A>` (nur dort verdrahtet). Prozess-DSL und Fluss-DSL sind zwei Schreibweisen auf demselben Dirigenten
(Kompensation, Count-Join und `SendeJe` nur im Prozess; Quelle, Strom, Warte, `Je`/`Sammle` nur im Fluss). 🤖 schreibt über `CodeSync.SetzeRumpf`
mit eigener Sicherung neben der ✎-Geste. *Empfehlung:* Rückbau der Handle-/Trigger-Welt, sobald Takt-Treiber und flüchtige Quellen da sind
(`konzept-werkbank.md` §12.2); die DSL-Frage entscheiden; einen Rumpf-Schreibweg.

**W11-2 · Zeitlimits.** Ein Zeitlimit im Dirigenten wird nur entdeckt, wenn der Backstop alle offenen Vorgänge weckt (alle 15 s) — Genauigkeit
15 s, Aufwand O(offene Vorgänge). Der durable Fristplaner mit Fälligkeits-Index (`FristScheduler`, `MartenFristplan`) existiert, hängt aber an der
Handle-Welt. *Empfehlung:* Fristplaner als Weck-Quelle der Zeitlimits in den Dirigenten.

**W3-1 · Ableitungen im Editor-JS.** `graphNodes` baut Karten-Ids parallel zu `Sicht.BaueKarten` (Transition-Id weicht bei fehlender `_id` ab);
`akteurHoert`, `flParameter`, `faehigkeitVorschlag` (weicht bei Namenskollision von `BoardLeseseite` ab), Auto-Zuordnung (`autoArgs`,
`flAutoArgs` mit zwei verschiedenen Namens-Heuristiken), Port-Kardinalität, Laufort aus dem rohen Umgebungs-Modell.
*Empfehlung:* alles über `board.sicht` bzw. `/api/werkbank/*`.

**WB-1 · Werkbank.** Geste p50 unter Fremdlast 346–470 ms (Ziel 300 ms). Tote Endpunkte: `/api/werkbank/verlauf|gesten|lesen|host/stopp|infra/*`,
`GET /kreislauf`, `/api/editor/module|build`, `/api/llm/pruefen|rumpf`; CLI `--schreiben` (Rest von „C# schreiben“). Formular und Spur-Spiegel lesen
`domain-model.json` vom Datenträger statt aus dem warmen Board. Umgebungs-Werte werden ohne Verlauf und ohne Branch-Sperre geschrieben; die Werkbank
löst Umgebungs-Dateien anders auf als der Host (fehlendes `${env:X}` wird still leer) und liest `Identitaeten.Tokens` mit einem dritten Parser.
LLM-Kontexte entstehen kalt (~1 min). Der Renamer ändert keine String-Literale.

**WZ-1 · Prüf-Lücken der Werkzeuge.** Das Grammatik-Gate in `--check` sucht die IDs als Text in der Solution und findet sie auch in `Grammatik.cs`
selbst; die Ortsangaben prüft es nicht (CQRS056 steht dort falsch als Analyzer in Domain.SourceGeneration). 18 Diagnose-IDs haben keine
Grammatik-Regel. Der Fixpunkt prüft Akteure, Verträge, Clients, Funktionen und Dienste nicht; Funktion und Dienst werden im Inventar nur nach
Namen verglichen. Die Sonde deckt `SendeJe`, Count-Join, Kompensation, `IMensch`, `IstClient`, Laufort und Webhook nicht ab.

**AK-1 · Akteure und Clients** (`konzept-akteure.md` §9): durable Zustellung an Clients (Zusagen mit Ausgabe sind durable Abhängigkeiten, werden
aber verlierbar zugestellt), `als` im Envelope bei mehreren verkörperten Akteuren, Zusagen über C#-Clients (CQRS094), Anmeldung je Person; kein
Boot-Check, ob `Clients:<Name>:Token` die Akteure des Vertrags verkörpert.

**P2-4 · `BoundedInbox` nicht airtight** (FIFO ab 10 000). Akzeptierter Tradeoff; ggf. persistente Inbox-Marke als Rückfall.

**P2-5 · Marking-/Kompensations-Feinschliff.** `MarkingKompakt` ist bei extremem Fan-out O(N) (Payloads je Vorgang); `NächsteKompensationAsync`
liest ab 0; der `KlärungNötig`-Pfad ist korrekt per Konstruktion, aber ohne Test.

**P2-6 · Schreibpfad.** Der parallele Drain skaliert sublinear (`wait_event`).

**P2-8 · Hartkodierte Intervalle und Verbindungsdaten** (Poll 30 s, Backstop 15 s; Connection-Strings je Integrationstest-Klasse).

**P2-9 · Toter Lesepfad und Kommentar-Vertrag.** `IEventStoreRepository.LoadStateAsync` wird außer von Test-Doubles nicht genutzt (Rehydration
läuft über `AggregateRehydrator`); die Aggregat-Identität ist zwischen `AggregateActorGenerator` und `CommandAggregateMapGenerator` nur per
Kommentar abgestimmt.

**P2-10 · Monitoring und Logging.** Nur `/health` + Kennzahlen; Default-Credentials `postgres/postgres`; kein OpenTelemetry; `GrpcProxy` (C#-Client)
loggt per `Console.WriteLine`.

**P2-11 · Python-Kleinschulden.** `generate_registry.py` erzeugt nichts (die Datei wird nie geschrieben), `domain_client/domain_registry.py` ist
handgepflegt; `VersionTracker.reset()` wird nie aufgerufen; `proxy._detect_route()` ist faktisch eine Konstante; die Köpfe der generierten
Python-Dateien verweisen auf `./codegen.sh --force` statt auf den Build.

### P3

- **P3-1 · Mojibake** in `Domain.SourceGeneration/AggregateHandlerGenerator.cs`, `Infrastructure/Projections/ReadModelDepsReader.cs`,
  `Infrastructure/Serialization/ProtoMapper.cs`.
- **P3-2 · Toter Code und Kleinkram:** `MartenProjectionTracker` (nirgends registriert) samt dem leeren Marker `IReplaybarerTracker`, `CqrsFrameworkOptions`, `IViewModel` + `ViewModelGenerator`,
  `ClusterKindFactory.cs` (auskommentiert), auskommentierte Reflection-Scans (`EventTypeResolver`, `MartenEventTypeRegistration`), nicht gemappte
  Proto-Endpunkte in Host.Blazor (das Python-SDK ruft sie trotzdem), `BenchPing` ohne Treiber-Basis; Dateiname ≠ Klassenname bei einigen
  Generatoren; veraltete Kommentare und Meldungen (z. B. „drei Header“ in `Spur.cs`, „geteilt mit der Simulation“ in `ProzessManager.cs`, „Client-/OCC-Pfad
  bleibt marken-frei“ in `AggregateActorBase` — der Client-Pfad läuft mit `istIdempotent: true`; die Fehlermeldung in
  `AggregateDispatcherExtensions.ResolveAggregateType` spricht noch von der Namespace-Zuordnung; `ConnectionModule.cs` nennt die CommandId „deterministisch“, sie ist aber `Guid.NewGuid()` und nur über
  Wiederholungen gleich; `CqrsServiceExtension.cs` nennt noch `Cluster__Role`/`migrator|member` statt `Cluster__Rolle`/Allein|Migrator|Mitglied;
  `ProzessManager.cs` sagt „nie in einem Feld gehalten“ trotz HOT-Cache; `Cqrs.Codegen/Program.cs` nennt sich „reflexionsfrei“, liest aber per
  `MetadataLoadContext`; `deploy-multinode/Dockerfile` und `docker-compose.yml` erwähnen noch den entfernten LoadHarness).
- **P3-3 · Saga-DSL** in `Cqrs.Testing` kennt `Rufe` nicht.

## 13.4 Korrektheit vs. Reifegrad

Kein Befund ist ein bekannter aktiver Korrektheits-Bug. Korrektheitsnah sind: **T-1** (nicht mehr getestete Prozess-/Snapshot-Pfade),
**W10-1** (möglicher Hash-Unterschied zwischen Python-Basis und Server beim Handshake), **W2-1** (still nicht registrierte Prozesse in einer
weiteren Assembly; vom Client gesetztes Cluster-Kind) und **WZ-1** (Prüfungen, die grün sind, ohne es zu beweisen). Alles andere ist Reifegrad,
Allgemeingültigkeit oder Hygiene.

## 13.5 Empfohlene Reihenfolge

1. **Wissen aufräumen** (Doku, Gedächtnis — läuft, 2026-10-13).
2. **T-1** Tests domänenneutral wiederherstellen — Sicherheitsnetz vor jedem Umbau.
3. **W2-1** Komposition und Projekt-Rollen aus Code-Fakten; **WZ-1** Prüfungen schärfen (Sonde für alle allgemeinen Primitive).
4. **W10-1/W10-2** Codegen auf Roslyn, Laufzeit-Serialisierung generiert, Reflection-Sperre.
5. **W11-1/W11-2** Rückbau der Handle-/Trigger-Welt nach Takt-Treiber und flüchtigen Quellen; Fristplaner im Dirigenten; DSL-Frage.
6. **W3-1/WB-1** Werkbank: letzte Ableitungen auf den Server, ein Rumpf-Schreibweg, tote Endpunkte weg.
7. **P1-1/P1-2/AK-1** Produktionshärtung (TLS, Identitäten, Consul, durable Client-Zustellung).

## 13.6 Erledigt (früher hier offen)

- **Co-Commit (früher P0-1):** implementiert und gegen echtes Postgres bewiesen (`MartenCoCommitStoreBase`, `CoCommitPostgresTests`); der
  Atomaritäts-Guard (`ICoCommitTracker`, GA-1) schließt das frühere false-green. Optional bleibt eine framework-getriebene Unit-of-Work.
- **Frontend-Build (früher P0-2)** und das Löschen der Legacy-Projekte `Domain.Client`/`Domain.Client.Ui.Blazor`.
- **Command-Zustellgarantie (früher P1-4):** `CommandFailed` bei Dispatcher-Erschöpfung; deterministische CommandId und Inbox-Dedup vor OCC
  auf dem Client-Pfad, Client-Retry. Optional bleibt ein Server-Ack.
- **Python-SDK (früher P1-5):** Query-Antwort, Client→Client-Trigger, Tests (SDK 28, Worker 20), Akteur-Token, generierte Vertrags- und
  Funktions-Basen.
- **Upcasting 1:N (früher P1-3):** nicht mehr offen — Schema-Evolution ist kein Ziel des Editors; `IUpcast` bleibt als Backend-Primitive (1:1).
- **Domänen-Leak `Reaktionsempfaenger` (früher P2-1):** mit der Beispieldomäne entfallen.
- **Proto-Typ-Maps (früher P2-2):** eine Quelle `Core.SourceGeneration/ProtoScalarSpec.cs`.
- **Extractor/Werkbank in der Solution (früher P2-3);** Simulationsschicht entfernt (Werkbank P8).
- **Multi-Node Iteration 1+2** samt Cold-Start-Schema-Migrator (`Infrastructure/Extensions/CqrsSchemaMigrator.cs`).

## 13.7 Was diese Bewertung offen lässt

- Ein formales **Sicherheits-Audit** (AuthN/AuthZ, Tenant-Isolation, TLS end-to-end) wurde nicht durchgeführt.
- **Absolute Perf-Zahlen** (+48 % Batching, 9× Prozess-Reads) stammen aus Läufen des inzwischen entfernten LoadHarness und wurden seitdem nicht
  reproduziert.
- Nebenbefund beim Laden des Workspaces: Marten 8.20.0 meldet zwei kritische Advisories (GHSA-rfx3-98h7-v3xp, GHSA-vmw2-qwm8-x84c).
