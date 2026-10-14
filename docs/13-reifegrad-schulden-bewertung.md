# 13 — Reifegrad, Schulden & Bewertung

**Die eine Quelle für „offen“.** Andere Dokumente (`CLAUDE.md`, Kapitel 01–12, Konzepte) nennen Schulden höchstens kurz und verweisen
hierher. Stand: 2026-10-15, Commit-Basis `ea463c6`. Maßstab sind die sechs Invarianten (`CLAUDE.md`) und die Werkbank-Leitsätze W1–W12
(`docs/konzept-werkbank.md` §2). Zahlen und Messwerte stehen in `docs/12-tests-und-vermessung.md`.

## 13.0 Offen auf einen Blick

| ID | Kurz | Prio |
|---|---|:--:|
| W10-1 | Reflection im Codegen (`MetadataLoadContext`) und im Werkbank-Schema | P1 |
| W10-2 | Reflektive Serialisierung zur Laufzeit (Marten-Default, Marking-Store, Snapshot-`Clone`, HTTP-Antworten); kein Reflection-Verbot im Build | P1 |
| LLM-1 | 🤖-Rümpfe sind nur „baut und regelkonform“ geprüft, nicht fachlich: keine Beispiele je Slot, `Cqrs.Testing` nicht an Editor/🤖 angebunden | P1 |
| P1-1 | Multi-Node nur container-erprobt, Consul im `-dev`-Modus | P1 |
| P1-2 | Transport-Sicherheit: kein TLS; Identitäten für Prod fehlen | P1 |
| P1-6 | `CancellationToken.None` im generierten Pull-Emit-Pfad | P1 |
| P1-7 | Deploy-Skripte veraltet (starten die Hosts ohne Umgebung, alte Konfig-Schlüssel) | P1 |
| W2-1 | Rest der Projektstruktur: Client-Framework hängt am Server-Framework, Generat-Namespaces, Identitäten aus einfachen Namen | P2 |
| W11-1 | Doppelte Wege: Handle-Pipeline/Trigger/`[Ingress]` ohne Nutzer, Prozess- vs. Fluss-DSL, zwei Rumpf-Schreibwege | P2 |
| W11-2 | Zeitlimits nur über den 15-s-Backstop entdeckt; Fristplaner hängt an der Handle-Welt | P2 |
| W3-1 | Ableitungen im Editor-JS (Karten-Ids, Hör-Menge, Lambda-Parameter, Auto-Zuordnung, Kardinalität) | P2 |
| WB-1 | Werkbank: Geste p50 unter Fremdlast 346–470 ms (Ziel 300 ms), tote Endpunkte, Formular/Spiegel lesen `domain-model.json` | P2 |
| WB-4 | Die Werkbank kennt genau eine Anwendung (`StandardWurzel`, `ServerHost`); eine Anwendung anlegen ist nicht beschrieben — Entscheidung offen | P2 |
| WZ-1 | Prüf-Lücken der Werkzeuge: Grammatik-Gate bestätigt sich selbst, Fixpunkt ohne Akteure/Verträge/Clients/Funktionen/Dienste, Sonde ohne `SendeJe`/Count-Join/Kompensation/`IMensch`/`IstClient` | P2 |
| AK-1 | Akteure/Clients: durable Zustellung an Clients, `als` im Envelope, Zusagen über C#, Anmeldung je Person | P2 |
| P2-4 | `BoundedInbox` nicht airtight (FIFO ab 10 000) | P2 |
| P2-5 | Marking-/Kompensations-Feinschliff, `KlärungNötig` ohne Test | P2 |
| P2-6 | Schreibpfad: paralleler Drain skaliert sublinear | P2 |
| P2-8 | Hartkodierte Poll-Intervalle und Test-Verbindungsdaten | P2 |
| P2-9 | Toter Lesepfad `LoadStateAsync`, Identitäts-Vertrag nur per Kommentar | P2 |
| P2-10 | Monitoring schmal, Default-Credentials, kein OTel, Logging per `Console.WriteLine` | P2 |
| P2-11 | Python: Registry von Hand gepflegt, Kleinkram im SDK | P2 |
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
| Konsum-/Prozess-Maschine | 🟢 |
| Generatoren & Analyzer | 🟢 / 🟡 (W10-1, W2-1) |
| Multi-Node / Transport | 🟢 / 🟡 (P1-1, P1-2) |
| Werkbank (Extractor, Editor, Server) | 🟢 / 🟡 (LLM-1, W3-1, WB-1, WB-4, WZ-1) |
| Clients (Blazor, Python) | 🟢 / 🟡 (AK-1, P1-2) |
| Tests & Vermessung | 🟢 |

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
die STJ-Kontexte und die Python-Generate (`vertraege.py`, `funktionen.py`, `funktions_tabelle.py`, `nutzlast_tabelle.py`). Daneben existiert mit `Proto.SourceGeneration` ein
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

**W2-1 · Rest der Projektstruktur.** Struktur (eine Tabelle, geprüft), Leitung (Framework-Proto + Nutzlast) und Komposition (`Domain.Laufzeit`,
`IAnwendung`) sind seit 2026-10-14 umgesetzt (§13.6). Offen bleibt:
- Das Client-Framework `Client.Infrastructure` referenziert `Infrastructure` (Server-Framework) nur für `ProtoMessageMapper`; der Mapper gehört in
  ein schlankes Leitungs-Framework.
- Framework-Code im Domänen-Projekt: der generische `funktions_worker.py` in `Domain.Client.Worker.Python.ML`.
- Generate der Komposition tragen noch Framework-Namespaces (`Infrastructure.Mapping.GeneratedTypeRegistry` u. a. in `Domain.Laufzeit`).
- Identitäten aus einfachen Typnamen (`BrokerIdentity`, `GeneratedAkteurRechte`); der Client darf das Cluster-Kind eines Commands
  (`AggregateType`) selbst setzen (`CqrsClientService.cs`, `AggregateDispatcher.cs:75`).
*Empfehlung:* Leitungs-Framework für Client und Server, Generat-Namespaces = Komposition; unabhängig davon
`AggregateType` serverseitig aus der Routing-Tabelle, Identitäten aus vollen Typnamen.
*Plan (2026-10-14, noch nicht umgesetzt):*
1. **Leitungs-Framework = `Cqrs.Leitung`.** `ProtoMessageMapper` (`Infrastructure/Serialization/ProtoMapper*.cs`) hängt nur an `Abstractions`
   und `Cqrs.Leitung` — er zieht ohne neues Projekt nach `Cqrs.Leitung` (Namespace `Cqrs.Leitung`). `Client.Infrastructure` verliert die
   Referenz auf `Infrastructure` (einziger Nutzer: `GrpcProxy`); Server (`CqrsClientService`) und Blazor-Client nehmen den Mapper von dort.
   Prüfen: `--check` „Lage“ um eine Regel ergänzen, dass kein Client-Projekt das Server-Framework referenziert.
2. **Generat-Namespaces = Komposition.** Die Laufzeit-Generatoren emittieren heute in Framework-Namespaces (`Infrastructure.Mapping`,
   `.Serialization`, `.Persistence`, `.Prozess`, `.Pipeline`, `.Akteure`, `.Funktionen`, `.Dienste`, `.Quellen`, `.Projections.Generated`,
   `.Aggregate.ActorSystem`) — zwei Kompositionen in einem Prozess kollidierten, und das Framework scheint Typen zu besitzen, die es nicht
   hat. Ziel: `{Komposition}.Generiert` als einziger Namespace aller Generate (eine Konstante neben `Laufzeitprojekt`), der
   `AnwendungGenerator` verweist dorthin; der Codegen schreibt die STJ-Kontexte in denselben Namespace. Framework-Code darf sie nur über
   `IAnwendung` erreichen — direkte Verweise (z. B. `GeneratedPipelines.RegisterAllPipelines`, `GeneratedPullPaths`, `RegisteredSnapshotTypes`,
   `GeneratedSnapshotSchema` in Hosts und Tests) wandern hinter `IAnwendung` bzw. in das generierte `AddCqrs`. Reihenfolge: erst die
   Framework-Verweise auf `IAnwendung` umstellen (grün), dann je Generator den Namespace wechseln (je ein grüner Schritt).
3. **Identitäten aus vollen Typnamen, `AggregateType` serverseitig.** `CqrsClientService` übernimmt ein vom Client gesetztes `AggregateType` und
   leitet es nur ab, wenn es leer ist — künftig immer aus `Anwendung.Routing.CommandToAggregate` (Client-Feld ignorieren, bei Abweichung
   ablehnen; Proto-Feld danach entfernen). Akteur-, Client- und Broker-Identitäten (`AkteurOptionen.Token<T>` mit `typeof(T).Name`,
   `GeneratedAkteurRechte`, `BrokerIdentity`) auf den vollen Typnamen bzw. eine generierte, eindeutige Id je Typ umstellen; CQRS-Diagnose
   für gleichnamige Typen in verschiedenen Namespaces, solange einfache Namen noch irgendwo als Schlüssel dienen (Umgebungs-Dateien nennen
   Akteure beim Namen — dort bleibt der einfache Name als Anzeigename, geprüft auf Eindeutigkeit).

**LLM-1 · 🤖-Rümpfe fachlich ungeprüft.** 🤖 füllen prüft einen Kandidaten nur probeweise im warmen Workspace (Compiler + Analyzer, bis drei
Runden, `konzept-llm-minimalkontext.md` §2) — ob der Rumpf tut, was der Auftrag sagt, prüft nichts. Die Test-DSL `Cqrs.Testing` (`Szenario`,
`SagaSzenario`, `FlussLaufwerk`) gibt es, sie wird aber nur von Hand im Prüfstand benutzt. Dabei sind die meisten Slots reine Funktionen
(`--slots`: Decide/Apply 100 % isoliert, Projektion 90 %) und die Store-Fähigkeiten kommen als Parameter — sie sind isoliert testbar, ohne einen
Event-Store zu faken. *Plan:* `konzept-llm-minimalkontext.md` §11 („Fachlich prüfen“): Beispiele je Slot als Testcode in einer neuen Rolle
`<Wurzel>.Beispiele`, Beispiele unabhängig vom Rumpf (Mensch, getrennter LLM-Aufruf ohne Rumpf, aus der Spur eines Sim-Laufs), 🤖-Schleife um
„Beispiele grün“ erweitert (eigener Prozess, keine Reflection in der Werkbank), Lücken in `Cqrs.Testing` (P3-3, Helfer für Projektion/Reaktion/
Reader aus den Signaturen generiert, pytest-Vorlage je Python-Basis), Kartenwand. Store-Implementierungen bleiben beim Marten-Test.

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

**WB-4 · Eine Anwendung je Werkbank.** Werkbank, Scaffolder und Extractor arbeiten auf `Projektstruktur.StandardWurzel` (11 Stellen) und bauen
`ServerHost`; die Prüfdomäne (zweite Anwendung) lässt sich nicht öffnen, und das Anlegen einer neuen Anwendung (Rollen-Projekte, Komposition, Host)
beschreibt das Zielbild nicht. *Entscheidung offen:* „eine Werkbank = eine Anwendung je Solution“ festschreiben oder die Wurzel wählbar machen
(Projektlage, Stand, Scaffolder, Host, Codegen-Prepass kennen sie bereits als Parameter).

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

**P2-11 · Python-Kleinschulden.** `domain_client/domain_registry.py` ist handgepflegt (die Kategorie je DTO ließe sich wie die Nutzlast-Tabelle
generieren); `VersionTracker.reset()` wird nie aufgerufen; `proxy._detect_route()` ist faktisch eine Konstante; die Köpfe der generierten
Python-Dateien verweisen auf `./codegen.sh --force` statt auf den Build.

### P3

- **P3-1 · Mojibake** in `Domain.SourceGeneration/AggregateHandlerGenerator.cs`, `Infrastructure/Projections/ReadModelDepsReader.cs`,
  `Infrastructure/Serialization/ProtoMapper.cs`.
- **P3-2 · Toter Code und Kleinkram:** der Quellordner `Domain.Client.Modules.Blazor/Debug/` (`DebugModule`, `DebugStage.razor`) wird von der
  Build-Regel `[Dd]ebug/` in `.gitignore` geschluckt und war nie im Repo (lokal vorhanden, im Checkout fehlt das Modul — umbenennen oder bewusst
  entfernen; **Entscheidung Tobi**, bis dahin ist `./pruefe-checkout.sh` rot); `MartenProjectionTracker` (nirgends registriert) samt dem leeren Marker `IReplaybarerTracker`, `CqrsFrameworkOptions`, `IViewModel` + `ViewModelGenerator`,
  auskommentierte Reflection-Scans (`EventTypeResolver`, `MartenEventTypeRegistration`), nicht gemappte
  Proto-Endpunkte in Host.Blazor (das Python-SDK ruft sie trotzdem), `BenchPing` ohne Treiber-Basis; Dateiname ≠ Klassenname bei einigen
  Generatoren; veraltete Kommentare und Meldungen (z. B. „drei Header“ in `Spur.cs`, „geteilt mit der Simulation“ in `ProzessManager.cs`, „Client-/OCC-Pfad
  bleibt marken-frei“ in `AggregateActorBase` — der Client-Pfad läuft mit `istIdempotent: true`; die Fehlermeldung in
  `AggregateDispatcherExtensions.ResolveAggregateType` spricht noch von der Namespace-Zuordnung; `ConnectionModule.cs` nennt die CommandId „deterministisch“, sie ist aber `Guid.NewGuid()` und nur über
  Wiederholungen gleich; `CqrsServiceExtension.cs` nennt noch `Cluster__Role`/`migrator|member` statt `Cluster__Rolle`/Allein|Migrator|Mitglied;
  `ProzessManager.cs` sagt „nie in einem Feld gehalten“ trotz HOT-Cache; `Cqrs.Codegen/Program.cs` nennt sich „reflexionsfrei“, liest aber per
  `MetadataLoadContext`; `deploy-multinode/Dockerfile` und `docker-compose.yml` erwähnen noch den entfernten LoadHarness).
- **P3-3 · Saga-DSL** in `Cqrs.Testing` kennt `Rufe` nicht (Voraussetzung für LLM-1).

## 13.4 Korrektheit vs. Reifegrad

Kein Befund ist ein bekannter aktiver Korrektheits-Bug. Korrektheitsnah sind: **LLM-1** (vom LLM
geschriebene Logik nur auf „baut“ geprüft), **W10-1** (möglicher Hash-Unterschied zwischen Python-Basis und Server beim Handshake), **W2-1** (vom Client gesetztes Cluster-Kind) und **WZ-1** (Prüfungen, die grün sind, ohne es zu beweisen). Alles andere ist Reifegrad,
Allgemeingültigkeit oder Hygiene.

## 13.5 Empfohlene Reihenfolge

Ziel ist die vollständige Werkbank nach `docs/konzept-werkbank.md` (W1–W12). Zuerst das Sicherheitsnetz, dann je Absicht ein Weg, dann die
Ableitungen auf den Server, dann die Leitsatz-Verletzungen in den Werkzeugen, zuletzt Treiber, Rückbau und Produktion.

**Vorab zu entscheiden** (blockiert die genannten Schritte nicht, ändert aber ihren Umfang):
- **WB-4** eine Anwendung je Werkbank oder Wurzel wählbar;
- **W11-1** Prozess- und Fluss-DSL: eine Schreibweise oder beide;
- **`IAkteurDienst<A>`** behalten oder durch Funktion + `IDarf<F>` ersetzen;
- **Daten-Prüfung von ⟳** gegen das Proto (heute) oder gegen die gespeicherte JSON-Form der Events (Marten/STJ, die eigentliche Wahrheit);
- **Tor am Webhook** einer Quelle (Token → Akteur) und Identitäten für Prod (P1-2).

1. ~~WB-2/WB-3~~ Sicherheitsnetz steht (2026-10-15): ⟳-Abnahmetest inkl. „▶ Worker“ und Gate „frischer Checkout baut“ (§13.6).
2. **W11-1 (Schreibwege)/WB-1** je Absicht ein Weg: 🤖 schreibt über die Rumpf-Geste (nicht `CodeSync.SetzeRumpf`), Umgebungs-Werte mit Verlauf, ↶
   und Branch-Sperre, Werkbank löst Umgebungen wie der Host auf (ein Token-Parser, fehlendes `${env:X}` = Fehler); tote Endpunkte weg; Durchspielen,
   Spur-Spiegel und LLM-Konsole lesen das warme Board statt `domain-model.json`.
3. **LLM-1** 🤖 fachlich prüfen: neue Rolle `<Wurzel>.Beispiele` (Strukturtabelle, Lageart, CQRS098), Beispiele an der Karte (Gegeben/Wenn/Dann)
   als Testcode, unabhängige Herkunft der Beispiele, 🤖-Schleife „baut → regelkonform → Beispiele grün“, `Cqrs.Testing` vervollständigen (P3-3,
   generierte Helfer für Projektion/Reaktion/Reader, pytest-Vorlagen), Kartenwand (`konzept-llm-minimalkontext.md` §11).
4. **W3-1** Ableitungen auf den Server: Karten-Ids, Hör-Menge, Lambda-Parameter, Auto-Zuordnung, Port-Kardinalität aus `board.sicht`/`/api/werkbank/*`.
5. **W10-1** Formular-Schema aus dem warmen Workspace (statt `Schema.cs` per Reflection), Codegen auf Roslyn; danach **W10-2** Laufzeit-Serialisierung
   generiert und Reflection-Sperre.
6. **WZ-1** Prüfungen schärfen: Grammatik-Gate prüft Ort und Gegenstück, Fixpunkt über Akteure/Verträge/Clients/Funktionen/Dienste, Sonde für alle
   allgemeinen Primitive (`SendeJe`, Count-Join, Kompensation, `IMensch`, `IstClient`, Laufort, Webhook).
7. **W11-1/W11-2** Treiber (Takt, flüchtige Quellen, Client meldet Quell-Nachricht), danach Rückbau der Handle-/Trigger-Welt samt Grammatik-Regeln;
   Zeitlimits über den Fristplaner; DSL nach Entscheidung.
8. **W2-1** Rest (Leitungs-Framework = `Cqrs.Leitung`, Generat-Namespaces = Komposition, Identitäten aus vollen Typnamen).
9. **WB-1** Geste p50 unter Fremdlast auf 300 ms; Renamer für String-Literale; LLM-Kontexte warm.
10. **P1-1/P1-2/AK-1** Produktionshärtung (TLS, Identitäten, Consul, durable Client-Zustellung).

## 13.6 Erledigt (früher hier offen)

- **⟳-Abnahmetest (früher WB-2, 2026-10-15):** `Werkbank.Abnahme.Tests` fährt eine echte Werkbank als Prozess auf einer Kopie des Arbeitsbaums
  (eigener Branch, eigene Umgebung `abnahme`, eigene Sim-DB) und geht nur über HTTP: ⟳ (alle Schritte grün, Board-Stand = Lauf-Stand = Stand im
  Einblick), `ErstelleDatensatz` als Akteur über den JSON-Eingang (Tor verweigert einen fremden Akteur), Query an die Projektion, Vorgang und Spur
  (Wirker, Absender, Stand), „▶ Worker“ (Python-Verkleinerung, Laufort `Extern`) mit einem TIFF in der Quelle bis `BildVerfuegbar`, Geste (Feld mit
  Standardwert) → ⟳ grün → ↶ (Stand und Datei byte-gleich) → ⟳ „Daten prüfen“ meldet `DatensatzErstellt.notiz entfällt` → ⟳ mit Zurücksetzen
  (docs/12 §12.6). **Dabei gefunden:** ⟳ „Bauen“ hing bis zum 10-min-Zeitlimit und wurde rot, sobald der Build MSBuild-Knoten bzw. den
  Compiler-Server neu startete (sie erben die Ausgabe-Pipe; `WaitForExitAsync` wartet auf deren Ende) — lokal fiel das nie auf, weil schon Knoten
  liefen. `Prozesse.LaufeAsync` wartet jetzt auf das Prozessende. Außerdem hielt sich die Werkbank in einem `git worktree` für `main` (`.git` ist
  dort eine Datei) und sperrte alle Gesten — `GitKopf` folgt dem `gitdir:`-Zeiger (Prüfstand `GitKopfTests`).

- **Gate „frischer Checkout baut“ (früher WB-3, 2026-10-15):** `./pruefe-checkout.sh` meldet lokal vorhandene Quellen, die eine Ignore-Regel
  schluckt (`git check-ignore -v`), baut den Commit `HEAD` in einem temporären `git worktree`, fährt dort `./codegen.sh --check` und den Prüfstand
  und verlangt einen unveränderten Checkout nach dem Build (docs/12 §12.6). Erster Lauf auf `2876a53`: Build, Drift und Prüfstand im frischen
  Checkout grün; rot nur durch `Domain.Client.Modules.Blazor/Debug/` (P3-2, Entscheidung offen). Dabei gefunden: `CLAUDE.md`, docs/06 und docs/12
  nannten `deploy-linux/docker-compose.infrastructure.yml` — das ist eine Ausgabe von `build.sh` und fehlt im Checkout; die Vorlage liegt im
  Wurzelverzeichnis (`docker-compose.infrastructure.yml`).

- **Daten-Prüfung von ⟳ repariert (2026-10-15):** `Werkbank/Server/ProtoVergleich.cs` suchte die Events im Event-Umschlag von `domain.proto`, den es
  seit der getrennten Leitung (`0bc84a7`) dort nicht mehr gibt — gegen einen alten gespeicherten Stand meldete sie jedes Event als verschwunden,
  zwischen zwei neuen Ständen prüfte sie nichts. Sie liest jetzt den Abschnitt „EVENT PAYLOADS“ (alte Stände weiterhin über den Umschlag);
  Prüfstand-Test `ProtoVergleichTests` gegen das echte `domain.proto`. ⟳ in der Werkbank durchgespielt: Bauen, Python-Basen, Neu lesen,
  Daten prüfen, Infrastruktur, Host starten grün; Board- und Lauf-Stand gleich.

- **Hartkodiertes entfernt (2026-10-14, früher Teil von W2-1):** Generatoren durchsuchen nur noch die eigene Assembly und die, die den Vertrag
  referenzieren (`Infrastructure.SourceGeneration/Vertragsassemblies.cs`, auch in `Ctorbindung`, `FunktionsGenerator`, `DienstGenerator`,
  `QuellenGenerator`, `WiringGenerator`) statt Namespace-Ausschlusslisten; Konfig-Records = Records aus Vertrags-Assemblies. Der
  Python-Interpreter ist ein Wert der Umgebung (`Umgebung:Python`, eine Regel `UmgebungKonfig.Python`); Host- und Werkzeug-DLL kommen als
  `TargetPath` vom Build-System (Werkbank `HostProzess`, `CqrsCodegen.targets`, `codegen.sh`); Proto-Paket und C#-Namespace aus dem
  Leitungs-Projekt (`domain.leitung`, kein Default mehr im `FileGenerator`); der Ordner `umgebungen/` steht in `Projektstruktur.UmgebungsOrdner`.
  Dabei gefunden: `Cqrs.Leitung/cqrs.proto` war seit `0bc84a7` nie im Repo (`.gitignore` ignorierte jedes `cqrs.proto`) — ein frischer
  Checkout baute nicht; die Regel gilt jetzt nur den Altdateien `/cqrs.proto`, `/Domain/cqrs.proto`.

- **Testlücke T-1 geschlossen (2026-10-14):** domänenneutral als zweite Anwendung `Pruefdomaene` (nur Schreibseite) mit Komposition
  `Pruefdomaene.Laufzeit` und Testprojekt `Pruefdomaene.Integration.Tests` (16 Tests gegen echtes Marten/Consul/Redis, eigene Schemata):
  Prozess-Diamant, Kompensation, Nebenläufigkeit, `SendeJe` + Count-Join, Snapshot (Store + Live), Upcasting (Tabelle + Seam),
  Marking-Cursor-Äquivalenz. Dafür: Codegen mit Wurzel-Parameter (`--wurzel`/`--projekt`/`--alle`, früher W2-1; Artefakte je vorhandener Rolle,
  Hash je Wurzel, Proto-Scan nur über die Referenz-Hülle der Anwendung), gemeinsamer Prepass `Cqrs.Codegen/CqrsCodegen.targets`, `Projektlage`
  bevorzugt die Komposition der `StandardWurzel`. Gefundener Fehler: der STJ-Kontext registrierte die früheren Gestalten einer
  `IUpcast`-Kante nicht (Komposition mit Upcasting baute nicht) — `JsonContextEmitter` nimmt jetzt alle Typ-Argumente der Kante.

- **Python-Nutzlast-Tabelle generiert (früher P2-11, 2026-10-14):** `Cqrs.Codegen` erzeugt `domain_client/generated/nutzlast_tabelle.py`
  (`TABELLE`: typ → DTO-Klasse, feste Importe, dieselben Kategorien wie `GeneratedNutzlast`); die Namensregel steht einmal in
  `Core.SourceGeneration/NameSanitizer.NutzlastTyp`/`DtoName` (C#-Mapper, Proto, Python-Emitter). `NutzlastTabelle`/`PayloadMapper`/`CqrsClient`
  nehmen nur noch diese Tabelle (`nutzlast=`); `typ_von` und `CategoryRegistry.capabilities_name` sind entfallen (Kausalität: `payload.typ`).

- **Werkbank-Stand ohne Generate (2026-10-14):** `Stand()` hasht nur noch die Rollen-Projekte mit handgeschriebenem Code
  (`Projektstruktur.HandgeschriebeneProjekte`: ohne Leitung und Komposition). Vorher verschob ein Build, der die STJ-Kontexte in
  `Domain.Laufzeit/Serialization` neu schrieb, den Stand selbst (Spur-Stand ≠ Board-Stand).

- **Komposition getrennt (früher W2-1, 2026-10-14):** `Infrastructure` ist reines Framework ohne Domänen-Referenz; die Komposition
  `Domain.Laufzeit` trägt alle Laufzeit-Generate, `GeneratedAnwendung : IAnwendung` (AnwendungGenerator) und das `AddCqrs` der Hosts; die Leitung
  der Anwendung `Domain.Leitung` (vormals `ProtoRepo`) trägt Domänen-DTOs und `GeneratedNutzlast : INutzlastTabelle`; `AggregateHandlerFactory`
  im Namespace ihrer Assembly; der Query-Dienst der Leseseite hinter `Abstractions.IQueryDienst`.
- **Leitung getrennt (früher P2-12, 2026-10-14):** Framework-Proto `Cqrs.Leitung/cqrs.proto` (Dienst, Hüllen) mit `Nutzlast { typ, daten }`;
  `domain.proto` enthält nur noch die Domänen-DTOs. Kein `oneof` über Domänen-Typen mehr — ein Umzug ändert die Leitung nicht.
- **Projektstruktur als eine Quelle (früher W2-1, 2026-10-14):** `Abstractions/Projektstruktur.cs` (Rollen als Suffix zur Wurzel, Primitive → Rolle
  und Datei), CQRS098 (`LageAnalyzer`), `--check` „Lage“, Sonde mit Soll-Lage; Prozess-Registry je Assembly + generierter Verbund (CQRS099 statt
  still nicht registrierter Prozesse); Codegen ohne Projektlisten (Hash über die gebauten DLLs, `Domain.Akteure` inklusive);
  `Proto.SourceGeneration/Program.cs` (zweiter Leseweg) gelöscht; Werkbank und Scaffolder lesen die Tabelle; `MartenCoCommitStoreBase` liegt in
  `Core`; Beispieldomäne umgezogen (Flüsse/Funktionen in `Domain.Pipeline`, Referenzrichtung `Domain.Pipeline` ← `Domain.Akteure`).
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
