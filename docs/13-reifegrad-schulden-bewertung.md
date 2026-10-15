# 13 — Reifegrad, Schulden & Bewertung

**Die eine Quelle für „offen“.** Andere Dokumente (`CLAUDE.md`, Kapitel 01–12, Konzepte) nennen Schulden höchstens kurz und verweisen
hierher. Stand: 2026-10-15, Phase 5 des Plans Garantien und Durchsatz. Maßstab sind die sechs Invarianten (`CLAUDE.md`) und die Werkbank-Leitsätze W1–W12
(`docs/konzept-werkbank.md` §2). Zahlen und Messwerte stehen in `docs/12-tests-und-vermessung.md`.

## 13.0 Offen auf einen Blick

| ID | Kurz | Prio |
|---|---|:--:|
| G-2 | Inbox-Fenster (10 000): eine CommandId, die jenseits der Grenze erneut kommt, wirkt erneut; der Emittenten-Anteil ist seit Phase 1 geschlossen | P1 |
| G-5 | Rehydration zählt die Version je materialisiertem Event: ein Split-Upcast verschiebt die Version | P2 |
| G-6 | Garantie ohne eigenen Nachweis-Test: Fehler nach dem Append im Aggregat-Actor (P10) | P2 |
| PF-13 | Nach PF-12 unter S5-64: Reaktion p99 2 s (p50 5 ms) — ihr Nachschub wartet am heißen Posten hinter den Reservierungen; S3-64 0,9 × Baseline (Latenz 0,81 ×) — mehr parallele Sends an den einen heißen Sammelvorgang | P3 |
| PF-4b | Rest von PF-4: keine Lease für den Prozess-Backstop im Cluster (jeder Knoten heilt alle offenen Vorgänge); Abnahme „S3 Zeit je Teil konstant über N“ und „10 000 offene Vorgänge im Leerlauf“ ohne Szenario im Treiber | P3 |
| PF-10 | Datei-Treiber meldet eine Datei nach der anderen ins Log (Rückstau): das Melde-Tempo ist die Decke von S1 — Entscheidung offen | P2 |
| VM-1 | Vermessungs-Treiber zählt Soll und Ist der Exactly-once-Prüfung in getrennten Abfragen, während der Host noch abbaut | P3 |
| PF-3 | Inbox-Marke je Command im Stream (auch ohne Event), Inbox im Snapshot | P2 |
| PF-6 | Read-your-writes-Index: ein Redis-Write je Event und Projektion, nicht abschaltbar | P2 |
| PF-7 | Quelle = ein Stream und ein Vorgang je Quell-Nachricht — Entscheidung offen für hochfrequente Quellen | P2 |
| W10-1 | Reflection im Codegen (`MetadataLoadContext`) und im Werkbank-Schema | P1 |
| W10-2 | Reflektive Serialisierung zur Laufzeit (Marten-Default, Marking-Store, Snapshot-`Clone`, HTTP-Antworten); kein Reflection-Verbot im Build | P1 |
| LLM-1 | 🤖-Rümpfe sind nur „baut und regelkonform“ geprüft, nicht fachlich: keine Beispiele je Slot, `Cqrs.Testing` nicht an Editor/🤖 angebunden | P1 |
| P1-1 | Multi-Node nur container-erprobt, Consul im `-dev`-Modus | P1 |
| P1-2 | Transport-Sicherheit: kein TLS; Identitäten für Prod fehlen | P1 |
| P1-7 | Deploy-Skripte veraltet (starten die Hosts ohne Umgebung, alte Konfig-Schlüssel) | P1 |
| W2-1 | Rest der Projektstruktur: Client-Framework hängt am Server-Framework, Generat-Namespaces, Identitäten aus einfachen Namen | P2 |
| W11-1 | Doppelte Wege: Handle-Pipeline/Trigger/`[Ingress]` ohne Nutzer, Prozess- vs. Fluss-DSL | P2 |
| W11-2 | Fristplaner hängt an der Handle-Welt (die Zeitlimits des Dirigenten laufen seit PF-4 über die Frist im Offen-Index) | P2 |
| W3-1 | Ableitungen im Editor-JS (Karten-Ids, Hör-Menge, Lambda-Parameter, Auto-Zuordnung, Kardinalität) | P2 |
| WB-1 | Werkbank: Geste p50 unter Fremdlast 346–470 ms (Ziel 300 ms), LLM-Kontexte kalt, CLI `--schreiben`, Renamer ohne String-Literale | P2 |
| WB-4 | Die Werkbank kennt genau eine Anwendung (`StandardWurzel`, `ServerHost`); eine Anwendung anlegen ist nicht beschrieben — Entscheidung offen | P2 |
| WZ-1 | Prüf-Lücken der Werkzeuge: Grammatik-Gate bestätigt sich selbst, Fixpunkt ohne Akteure/Verträge/Clients/Funktionen/Dienste, Sonde ohne `SendeJe`/Count-Join/Kompensation/`IMensch`/`IstClient` | P2 |
| AK-1 | Akteure/Clients: durable Zustellung an Clients, `als` im Envelope, Zusagen über C#, Anmeldung je Person | P2 |
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
| Schreibseite | 🟢 / 🟡 (G-5) |
| Konsum-/Prozess-Maschine | 🟡 (G-2 Inbox) |
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

Schweregrade: **P0** = bekannter aktiver Korrektheits-Bug · **P1** = wichtig, produktions- oder allgemeingültigkeitsrelevant ·
**P2** = Wartbarkeit/Hygiene · **P3** = kosmetisch. Die Reihe **G** sind Lücken in den Garantien aus [11](11-garantien.md).

### P0

Keine offenen P0-Befunde (G-1 und G-7 erledigt in Phase 1 des Plans Garantien und Durchsatz, §13.6).

### P1

**G-2 · Inbox-Fenster** (früher P2-4). Die Inbox je Aggregat merkt sich die letzten `Schreibpfad.InboxGrenze` (Standard 10 000)
CommandIds, FIFO (`BoundedInbox`, auch im Snapshot). Kommt eine CommandId erneut, nachdem sie aus dem Fenster gefallen ist, wirkt der Command
erneut. Den häufigsten Auslöser — der Poll eines Emittenten gab ab Version 0 alles erneut aus — hat Phase 1 beseitigt (Emittenten-Cursor, §13.6);
offen bleibt das Fenster selbst (Wiederholung eines Clients oder eines Dirigenten nach mehr als 10 000 fremden Commands am selben Aggregat).
Test: PD `SchreibGarantieTests.Eine_alte_CommandId_jenseits_der_Inbox_Grenze…` (Skip G-2). *Empfehlung:* Inbox als durable Seitentabelle
statt Fenster im Stream (Plan Phase 4).

Die Reihe **PF** sind Kosten der Garantien ([11](11-garantien.md) §11.12): die Garantie bleibt, der Mechanismus ist teurer als nötig.

**PF-10 · Datei-Treiber meldet seriell.** Der Datei-Treiber meldet die nächste Datei erst, wenn die vorige im Log steht (Rückstau, docs/11 §11.8).
In S1 ist genau das die Decke: je Sekunde werden halb so viele Paare komplett, wie Dateien gemeldet werden (Phase-2- und Phase-3-Code gleich);
die Leseseite hält in Echtzeit mit. Das Ziel „S1 ≥ 3 × Baseline“ (Plan E5) ist deshalb mit dem Konsum nicht zu erreichen. *Optionen (Tobi):* in
Bündeln melden (mehrere `StartStream` in einer Transaktion, Rückstau je Bündel; „genau einmal je Kennung“ bleibt) oder das S1-Ziel neu fassen.

**VM-1 · Zählung im Treiber.** `Cqrs.Vermessung/Pruefdomaene.cs` zählt etwa „je Reservierung unter dem Mindestbestand genau ein Nachschub“ als
zwei Abfragen; baut der Host noch ab (Rückstand bei Fristende), kann Ist Soll überholen (Phase 3: 21 648 gegen 21 646, im Ruhezustand 21 927 =
21 927). *Empfehlung:* die Prüfungen erst nach dem Anhalten der Hosts zählen oder Soll und Ist in einer Abfrage.


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

**P1-7 · Deploy-Skripte veraltet.** `deploy-linux/start-server.sh`, `deploy/deploy.sh`, `deploy/deploy_clients.sh`, `deploy/cqrs-blazor.env` und
`deploy-windows/start.ps1` starten die Hosts ohne `--umgebung`/`CQRS_UMGEBUNG` (Startfehler) und setzen alte Schlüssel (`Pipeline__WatchPath`,
`Blazor__Urls`, `GrpcServer__Address`, `ConnectionStrings__EventStore`); `deploy-*/grpc/appsettings.json` tragen noch `WatchPath`; `build.sh` enthält
eine Umgebung als Heredoc neben `umgebungen/*.json`. *Empfehlung:* Deploy auf `umgebungen/umgebung.prod.json` + Umgebungsvariablen umstellen.

### P2

**W11-1 · Doppelte Wege.** Ohne Domänen-Nutzer, aber verdrahtet: die `IPipelineHandler`-Maschinerie (`PipelineActorBase`, `PipelineEventPullBridge`,
`PipelineDispatchGenerator`, `GeneratedPipelines`), die Trigger-Welt (`IPipelineTrigger`, `ITriggerRegistration`, `TimerTrigger`, `WebhookTrigger`,
gRPC-Trigger-Pfad, Proto `Trigger*`), `[Ingress]` mit `GraphExtractor.Kern/CompositionRoot.cs`, `Selbst<T>`/`Frist<TCmd>`/`FristStorno<TCmd>` als
Handle-Ausgang, `IAkteurDienst<A>` (nur dort verdrahtet). Prozess-DSL und Fluss-DSL sind zwei Schreibweisen auf demselben Dirigenten
(Kompensation, Count-Join und `SendeJe` nur im Prozess; Quelle, Strom, Warte, `Je`/`Sammle` nur im Fluss). *Empfehlung:* Rückbau der
Handle-/Trigger-Welt, sobald Takt-Treiber und flüchtige Quellen da sind (`konzept-werkbank.md` §12.2); die DSL-Frage entscheiden. (Der zweite
Rumpf-Schreibweg ist seit 2026-10-15 weg, §13.6.)

**W11-2 · Zeitlimits.** Ein Zeitlimit im Dirigenten wird nur entdeckt, wenn der Backstop alle offenen Vorgänge weckt (alle 15 s) — Genauigkeit
15 s, Aufwand O(offene Vorgänge). Der durable Fristplaner mit Fälligkeits-Index (`FristScheduler`, `MartenFristplan`) existiert, hängt aber an der
Handle-Welt. *Empfehlung:* Fristplaner als Weck-Quelle der Zeitlimits in den Dirigenten.

**W3-1 · Ableitungen im Editor-JS.** `graphNodes` baut Karten-Ids parallel zu `Sicht.BaueKarten` (Transition-Id weicht bei fehlender `_id` ab);
`akteurHoert`, `flParameter`, `faehigkeitVorschlag` (weicht bei Namenskollision von `BoardLeseseite` ab), Auto-Zuordnung (`autoArgs`,
`flAutoArgs` mit zwei verschiedenen Namens-Heuristiken), Port-Kardinalität, Laufort aus dem rohen Umgebungs-Modell.
*Empfehlung:* alles über `board.sicht` bzw. `/api/werkbank/*`.

**WB-1 · Werkbank.** Geste p50 unter Fremdlast 346–470 ms (Ziel 300 ms; gemessen wird nur noch im Konsolen-Log `[Geste]`, der Endpunkt
`/api/werkbank/gesten` ist entfallen). CLI `--schreiben` (Rest von „C# schreiben“). LLM-Kontexte entstehen kalt (~1 min, `GraphExtractor --kontexte`
im selben Baum — läuft er beim ersten Start gleichzeitig mit ⟳, bauen zwei Prozesse dieselben Projekte); ihr Slot-Index kommt nicht aus dem warmen
Board, und `GraphExtractor` schreibt dabei weiter `domain-model.json` (liest in der Werkbank niemand mehr). Der Renamer ändert keine String-Literale.

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

**PF-3 · Inbox-Marke im Stream.** Jeder Command hinterlässt `KommandoVerarbeitet`, auch wenn er Events erzeugt, die die CommandId schon als
`causationId` tragen; Streams werden etwa doppelt so lang, Snapshots tragen bis `InboxGrenze` Ids. *Empfehlung:* Marke nur bei Noop und
Ablehnung, oder Inbox als Tabelle mit eindeutigem Schlüssel in derselben Transaktion (löst G-2 mit).

**PF-6 · Read-your-writes-Index.** `ReadModelDepsWriter` schreibt je Event und Projektion nach Redis, für eine Garantie, die nur best-effort ist.
*Empfehlung:* je Projektion abschaltbar, an nur für Projektionen, auf die Clients warten.

**PF-7 · Quelle als Stream je Nachricht.** Genau einmal je Kennung entsteht über einen eigenen Stream und Vorgang je Quell-Nachricht. Für
hochfrequente Quellen (Maschinendaten) ist das teuer. *Entscheidung offen:* Dedup-Tabelle je Kennung und Sammel-Streams gegen „ein Vorgang je
Meldung“; zusammen mit den flüchtigen Quellen (W11-2/§13.5 Schritt 7).

**G-6 · Garantie ohne eigenen Nachweis-Test** (Garantie-Matrix, [11](11-garantien.md)). Für „durabel nach Erfolg“ (P10) prüft der
mehrdeutige Commit den Append selbst (PD `SchreibGarantieTests`, seit Phase 2 grün); dass ein Fehler **nach** dem Append im Actor frisch
rehydriert und Erfolg quittiert, hat keinen Test. Abgleich Phase 2: die Schritte nach dem Append (Version-Index, Snapshot, Publish) fangen ihre
Fehler selbst; erreichbar ist der Pfad nur über einen werfenden Applier — der bricht dann auch die Rehydration (vergiftetes Aggregat,
Dead-Letter). *Empfehlung:* entweder den Pfad auf den Applier-Fall zurückschneiden oder einen Prüfstand-Test mit einem Actor über eine
werfende Applier-Naht.

**G-5 · Versionszählung bei Split-Upcasts.** `AggregateRehydrator` zählt `Version++` je materialisiertem Event; ein Split-Upcast liefert aus
einer gespeicherten Position mehrere Events (`MartenEventStore`, `SubIndex`), die Version läuft dem Log davon, und jeder Append scheitert.
Latent: heute gibt es keinen Split-Upcast. *Empfehlung:* je gespeicherter Position zählen (`AggregateVersion`), Prüfstand-Test mit Split.

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
- **P3-2 · Toter Code und Kleinkram:** `CqrsFrameworkOptions`, `IViewModel` + `ViewModelGenerator`,
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

Keine bekannten aktiven Korrektheits-Bugs mehr (G-1, G-7, G-3, G-4 erledigt, §13.6). **G-2** (Inbox-Fenster) bricht eine Garantie
unter Last, **G-5** ist latent. Jede offene G-Lücke hat einen Test, der sie zeigt (Skip `G-n offen`, [11](11-garantien.md) §11.13).
Korrektheitsnah sind außerdem: **LLM-1** (vom LLM
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

0. **G und PF** (Garantie-Lücken und ihre Kosten) laufen als eigener Strang nach [plan-garantien-und-durchsatz.md](plan-garantien-und-durchsatz.md):
   Phase 0 Abgleich und E2E-Messung, 1 Emit-Ausgang und Emittenten-Cursor (G-1, G-2 Emittent, G-7, P1-6, PF-9), 2 Actor-Lebenszyklus (G-3, PF-5), 3 Batch-Konsum (PF-1, PF-2,
   G-4), 4 Inbox als Tabelle (G-2, PF-3), 5 Prozess-Poll (PF-4), 6 Rest. Ein Agent setzt ihn allein um; die Schritte 1–10 unten ruhen so lange.
   Gemessen wird nach jeder Phase Ende-zu-Ende an den realen Domänen (Plan §0.1). Stand: Phase 0, 1 (Emit-Ausgang, Emittenten-Cursor,
   G-1, G-7, P1-6, PF-9), 2 (Actor-Lebenszyklus, G-3, PF-5) und 3 (Batch-Konsum, PF-1, PF-2, G-4) umgesetzt; Stand und Messung im Plan §8.
1. ~~WB-2/WB-3~~ Sicherheitsnetz steht (2026-10-15): ⟳-Abnahmetest inkl. „▶ Worker“ und Gate „frischer Checkout baut“ (§13.6).
2. ~~W11-1 (Schreibwege)/WB-1~~ je Absicht ein Weg (2026-10-15, §13.6).
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

- **Leerlauf der Prüfdomäne (früher PF-11, PF-12; 2026-10-15, Messserver; docs/04 §4.1, §4.5):** die Annahme „die Reaktion hängt nach“
  war falsch (sie antwortet in Echtzeit, p99 37 ms). L5 war nicht leer wegen dreier Schichten, die nacheinander sichtbar wurden:
  - **Emittenten-Poll weckte jeden geänderten Stream des Logs:** der Adapter las ihn ab 0 und legte einen Cursor an (9 274 Cursor statt 50).
    Jetzt liest der Poll seitenweise nur Streams mit abonniertem Event, ohne Daten (`ReadChangedStreamsAsync` mit Typfilter, PF-11).
  - **Korrelations-Router seriell:** je Signal ein Read des ganzen Stream-Rests (S5-64: 63 000 Reads, 3,9 Mio. Zeilen), die Mailbox staute
    sich bis in den Leerlauf. Jetzt parallel (32), Auslöser ohne Read, sonst genau ein Event (`ReadEventAsync`, auch für den Auslöser im Manager).
  - **Signale der Emittenten nicht zusammengefasst:** je Signal eine Weckung und ein Read (48 000 in L5). Jetzt `Weckungen<TKey>` (eine
    unterwegs, eine hinterher), dieselbe Klasse wie im Batch-Konsum.
  - Dabei gefunden (Störungslauf rot, 51 Vorgänge liegen geblieben): der Offen-Index entstand *nach* dem `ProzessGestartet`; ein Abbruch
    dazwischen ließ gestartete Vorgänge ohne Eintrag zurück, die weder Backstop noch der Poll (seit PF-4 startet er nur offene oder neue)
    weckten. Jetzt Eintrag vor dem Start (`ProzessStartGarantieTests`, ohne die Reihenfolge rot gesehen).
  - Gemessen (`…_1940_5bda1bb_server-pf-12`, zwei Störungsläufe grün): L5 2 292 → 2,5 Queries/s, 145 → 3,4 % CPU (E5-Ziel L5 erreicht);
    S5-64 1,69 × Baseline, Latenz p50 0,26 ×, Queries je Vorgang 0,78 ×; S5-8 Latenz 0,38 ×. Rest: PF-13.
- **Prozess-Poll und Backstop (früher PF-4, G-8; Plan Garantien und Durchsatz, Phase 5, vor Phase 4 auf Tobis Wort, 2026-10-15; docs/04 §4.5):**
  - **PF-4:** der Prozess-Poll (`ProzessPoll`) liest das Log seitenweise ab seiner Marke (`ReadPartitionAsync`, 500 Events) und routet die
    geladenen Events direkt — keine Stream-Reads ab Version 0 mehr; Starts nur für Vorgänge, die es noch nicht gibt oder die offen sind
    (`StreamsVorhandenAsync`, eine Abfrage je Seite); Marke je Seite gespeichert. Backstop: fällige **Fristen** (Zeitlimit, Warte-Knoten) im
    Sekundentakt aus dem Offen-Index (`ProzessOffen.Frist`, geschrieben erst, wenn der Vorgang still wird), Heil-Weckung nach 15 s Stille mit
    Backoff je Vorgang bis 1 h (`Heilplan`) statt alle 15 s alle. Gemessen: Leerlauf L1 351 → 2,5 Queries/s, 24 → 3 % CPU (E5-Ziel L1 erreicht).
  - **G-8:** die Poll-Weckungen sind bestätigt (WakeAck), die Marke rückt nur bis vor die erste unbestätigte — P `ProzessPollGarantieTests`
    (ohne die Regel rot gesehen).
  - Nebenbei: rohes SQL legte die Event-Tabellen nie an; auf einer frischen DB tat das zufällig der alte Prozess-Poll (LINQ). Jetzt legt
    der erste rohe Leser sie an (`MartenEventStore.EventTabellenAsync`).
- **Batch-Konsum der Projektionen (früher PF-1, PF-2, G-4; Plan Garantien und Durchsatz, Phase 3, 2026-10-15; docs/04 §4.1.1, §4.3):**
  - **PF-1:** replaybare Projektionen laufen je (Projektion, Partition) in einem `BatchLeser` (virtueller Actor `batch-<Typ>`, 8 Partitionen nach
    dem letzten UUID-Byte, Umgebung `Konsum`): eine Seite (bis 500 Events, Typfilter samt Upcasting-Herkunft) → Guards in einer Abfrage → je
    Stream in Versionsreihenfolge anwenden → Effekte, Guards und Partitions-Checkpoint in **einer** Transaktion. Poison-Streams werden geparkt
    (der Rest läuft weiter, `/health` zeigt sie), Konflikte an geteilten Dokumenten halbieren den Batch. Der Adapter je Stream bleibt nur für
    Emittenten (`EmittentenAdapter`, E8).
  - **PF-2:** kein Heil-Poll je Stream mehr für Projektionen — der Poll weckt alle 30 s jede Partition, der Leser liest ab seiner Position
    (leer im Leerlauf); die Position läuft über fremde Events im Speicher und wird höchstens einmal je Poll-Intervall durabel. Der Neuaufbau
    (`ProjectionRebuilder`, `ProjektionsStandDienst`) liest je Partition seitenweise statt den Log als Liste.
  - **G-4:** der Partitions-Checkpoint trägt eine numerische Revision; jeder Commit erwartet die geladene Version, eine zweite Aktivierung oder
    ein Neuaufbau dazwischen verwirft den Batch (der Neuaufbau setzt mit neuer Revision zurück, statt zu löschen). I
    `GarantiePostgresTests.Zwei_Aktivierungen…` ohne Skip; neu I `…Ein_Poison_Stream_wird_im_Batch_geparkt…`,
    `…Ein_Neuaufbau_nimmt_einem_laufenden_Leser_den_Commit`, `SequenceGapPostgresTests.Batch_Leser…`,
    `PollerBackstopPostgresTests.Batch_Leser…`; P `BatchKonsumGarantieTests`, `BatchLeserTests`.
  - Mit erledigt aus P3-2: der tote `MartenProjectionTracker` und der leere Marker `IReplaybarerTracker` sind entfernt.

- **Actor-Lebenszyklus (früher G-3, PF-5; Plan Garantien und Durchsatz, Phase 2, 2026-10-15):**
  - **G-3:** jeder Append des Aggregat-Actors (Effekt, Noop- und Ablehnungs-Marke) läuft über `AppendeAsync`. Scheitert er, rehydriert der Actor
    frisch; steht die CommandId danach in der Inbox, wird der durable Ausgang quittiert (mehrdeutiger Commit, P10), sonst einmal auf dem frischen
    Stand neu entschieden. PD `SchreibGarantieTests.Nach_einem_Versionskonflikt…` und `…Ein_mehrdeutiger_Commit…` ohne Skip.
  - **PF-5:** Passivierung über Proto-`ReceiveTimeout` für Aggregat, Adapter und Prozess-Manager (`Infrastructure/Lebenszyklus/Passivierung.cs`),
    Leerlauf aus der Umgebung (`Lebenszyklus`, E7: 600/120/600 s); der Snapshot beim Stoppen bleibt. `/monitoring/metrics` und `/health` melden
    die aktiven Actors. PD `LebenszyklusGarantieTests`, P `PassivierungTests`.

- **Emit-Ausgang und Emittenten-Cursor (früher G-1, G-7, P1-6, PF-9 und der Emittenten-Anteil von G-2; Plan Garantien und Durchsatz,
  Phase 1, 2026-10-15):**
  - `CommandEmitter.EmitAsync` liefert einen `EmitAusgang` (zugestellt, abgelehnt, unbekannt) statt Timeouts zu verschlucken; keine Ausnahme
    verlässt den Emitter. `CommandResult.Abgelehnt` trennt die durable fachliche Ablehnung vom technischen Fehler.
  - **G-1:** `FristScheduler.TickAsync` entfernt eine Frist nur bei entschiedenem Ausgang. P `FristGarantieTests` ohne Skip.
  - **G-2 (Emittent):** Signal und Poll eines Emittenten lesen ab dem Cursor; die Commands eines Tails gehen parallel hinaus
    (`EmitAusgaenge`) und werden gemeinsam abgewartet, der Cursor rückt nur nach bestätigtem Emit vor; „nicht aufgeholt“ hält den Poll
    (`WakeAck.Aufgeholt`). `Wake.VomPoll` und `DetachedEmit` sind entfallen. P `KonsumGarantieTests.Ein_nicht_zugestellter_Emit…` ohne Skip,
    PD `AblaufGarantieTests.Absturz_zwischen_Emit_und_Cursor…` neu. Offen bleibt das Inbox-Fenster selbst (G-2, Phase 4).
  - **P1-6:** der generierte Pull-Emit-Pfad (`PullPathGenerator`, `PipelineActorGenerator`) reicht das Token der Weckung durch.
  - **G-7:** `MartenEventStore` meldet Tombstone-Streams nicht als geändert und liest keine Tombstones; die Spur zeigt sie nicht; ein
    scheiternder Poll-Durchlauf wird als Warnung gemeldet. I `GarantiePostgresTests.Ein_gescheiterter_Append…` ohne Skip (ohne den Filter rot
    gesehen).
  - **PF-9:** `DetachedProzessSend` je Manager sperrt einen Vorgang, solange sein Send läuft oder sein Backoff wartet, und weckt nach
    „unbekannt“ erst nach Backoff (1 s … 30 s); `EmitDrossel` begrenzt die Sends je Ziel-Aggregat (8 je Knoten). P `EmitGarantieTests`.

- **Je Absicht ein Weg in der Werkbank (früher Teil von W11-1 und WB-1, 2026-10-15):**
  - 🤖 schreibt über die Rumpf-Geste wie ✎ (`GestenDienst.RumpfAsync` bzw. `PythonRumpfAsync` mit Prompt-Zeile; Ort aus dem Slot-Anker über
    `CodeSync.Ort`). Geprüft wird mit derselben Einsetzung (`Rumpf.MitRumpf`), der Patch steht im Verlauf, ↶ der Werkbank nimmt ihn zurück, auf
    `main` ist er gesperrt. `CodeSync.SetzeRumpf/SetzeLambda/…Zurueck`, `PythonSync.SetzeRumpf/Zurueck`, die Sicherung in `.llm-kontext/sicherung/`
    und `/api/llm/rueckgaengig` sind entfallen; Editor und Konsole nutzen das eine ↶.
  - Umgebungs-Werte sind eine Geste (`GestenDienst.UmgebungAsync`: Verlauf, ↶, Branch-Sperre).
  - Die Werkbank löst Umgebungen wie der Host auf: `UmgebungLader.LadeDatei` per Link (wie in `Host.Blazor`), Binder-Generator statt Reflection,
    fehlendes `${env:X}` = Fehler, strenge Bindung. Tokens über eine Regel `Abstractions.AkteurTokens` (Host-Tor, Konsole, Worker-Token).
  - Tote Endpunkte entfernt: `/api/werkbank/verlauf|gesten|lesen|host/stopp|infra/*`, `GET /api/werkbank/kreislauf`, `/api/editor/module|build`,
    `/api/llm/pruefen|rumpf|rueckgaengig` (vorher geprüft: kein Aufrufer in Editor, Konsole, Tests).
  - Durchspielen (Formulare) und Spur-Spiegel (Quell-Knoten) lesen das warme Board; der Arbeitsbereich schreibt kein `domain-model.json` mehr,
    `/api/editor/model` fällt nicht mehr auf die Datei zurück.
  - Die LLM-Konsole las `domain-model.json` nie direkt; offen bleibt, dass ihr Slot-Index kalt entsteht (WB-1).
  - Ein Prozess-Weg auch für den Build nach einer Übernahme und das Erzeugen der Kontexte (`Prozesse.LaufeAsync`, sonst dieselbe
    Pipe-Falle wie bei ⟳).
  - Der Abnahmetest prüft Umgebungs-Geste + ↶, 🤖-Übernahme (Methode und Fluss-Lambda) + ↶, Formular, Tokens und den Quell-Knoten der Spur.

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
  Checkout grün; rot nur durch den nie versionierten Quellordner `Domain.Client.Modules.Blazor/Debug/` (`[Dd]ebug/` schluckte ihn) — auf
  Entscheidung entfernt (2026-10-15, war ohne Verweise), seitdem ist das Gate grün. Dabei gefunden: `CLAUDE.md`, docs/06 und docs/12
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
