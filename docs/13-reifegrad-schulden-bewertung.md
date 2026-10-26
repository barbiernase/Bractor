# 13 — Reifegrad, Schulden & Bewertung

**Die eine Quelle für „offen“.** Andere Dokumente (`CLAUDE.md`, Kapitel 01–12, Konzepte) nennen Schulden höchstens kurz und verweisen
hierher. Stand: 2026-10-18, nach Phase 4 des Plans Garantien und Durchsatz, PF-14 und der Analyse wachsender Bestände (PF-23 bis PF-32). Maßstab sind die sechs Invarianten (`CLAUDE.md`) und die Werkbank-Leitsätze W1–W12
(`docs/konzept-werkbank.md` §2). Zahlen und Messwerte stehen in `docs/12-tests-und-vermessung.md`.

## 13.0 Offen auf einen Blick

**Umbau (2026-10-26):** [plan-pubsub-neustrukturierung.md](plan-pubsub-neustrukturierung.md) löst Broker, Signale, Prozess-Manager, Fluss- und
Prozess-Sprache, Handle-/Trigger-Welt und den Redis-Versionsindex ab. Viele Punkte unten (u. a. PF-4b, PF-6, PF-7, PF-10, PF-13, PF-25, PF-26,
W11-1, W11-2, AK-1, P2-5, P3-3) erledigen sich damit oder bekommen eine neue Form; sie werden mit der jeweiligen Phase des Plans abgeschlossen
oder umgeschrieben. Die Befunde der Actor-Praxis vom 2026-10-18 stehen im Plan §1.7.

| ID | Kurz | Prio |
|---|---|:--:|
| G-11 | Im Störungslauf übersprang eine Projektion einmal zwei Events eines Streams (Partition am Kopf, Guard dahinter) — Ursache und Test offen | P1 |
| G-12 | Eine fachliche Ablehnung erreicht den Client nie, wenn sein Vertrag ihren Typ nicht hört (der Arbeitsplatz hört keine): er wiederholt dreimal und meldet „unbestätigt“ | P1 |
| G-14 | Read-your-writes greift nicht für ein eben angelegtes Objekt: ohne Eintrag im Versions-Index gilt die Antwort als frisch („nicht gefunden“ gleich nach der Quittung) | P2 |
| G-13 | Zwei Knoten vergeben dieselben Sitzungs-Kennungen (Zähler je Prozess), der Broker überschreibt: eine gezielte Antwort kann bei der Sitzung des anderen Knotens landen | P1 |
| G-5 | Rehydration zählt die Version je materialisiertem Event: ein Split-Upcast verschiebt die Version | P2 |
| G-6 | Garantie ohne eigenen Nachweis-Test: Fehler nach dem Append im Aggregat-Actor (P10) | P2 |
| PF-13 | Unter S5-64: Reaktion p99 2–3,5 s (p50 5 ms) — ihr Nachschub wartet am heißen Posten hinter den Reservierungen (der S3-64-Rückgang ist mit „korreliert lesen“ erledigt: 1,85 × Baseline) | P3 |
| PF-4b | Rest von PF-4: keine Lease für den Prozess-Backstop im Cluster (jeder Knoten heilt alle offenen Vorgänge); Abnahme „S3 Zeit je Teil konstant über N“ und „10 000 offene Vorgänge im Leerlauf“ ohne Szenario im Treiber | P3 |
| PF-10 | Datei-Treiber meldet eine Datei nach der anderen ins Log (Rückstau): das Melde-Tempo ist die Decke von S1 — Entscheidung offen | P2 |
| VM-1 | Vermessungs-Treiber zählt Soll und Ist der Exactly-once-Prüfung in getrennten Abfragen, während der Host noch abbaut | P3 |
| PF-15 | Grundlast ohne Eingang wächst mit Projektionen × Partitionen: der Batch-Poll weckt alle 30 s jede Partition (je eine Abfrage), dazu jede Sekunde DB-Uhr und Fristen — heute L1 3,8 Queries/s, bei 50 Projektionen ≈ 16; seit Phase 3 dazu die Uhr (eine Abfrage je Sekunde) und je Funktion und Partition ein Poll | P3 |
| FB-1 | Ein Hinweis (`ITransientEvent`) als Eingang einer Funktion steht im generierten Hinweisplan, wird zur Laufzeit aber noch nicht zugestellt (schneller Kanal kommt mit Plan Phase 6) | P2 |
| FB-2 | Funktion als Baustein: Laufort Extern (gRPC, Python-Worker) fehlt — Boot-Fehler bis Plan Phase 7; die Quell-Nachricht vom Client gibt es nur über den JSON-Eingang, nicht über gRPC und die SDKs | P2 |
| FB-3 | Events aus Funktionen, Quellen und der Uhr senden kein Signal: eine Funktion, die auf sie hört, sieht sie erst mit dem Poll (bis 30 s) — eigener Wecker mit Plan Phase 6 | P3 |
| FB-4 | Werkzeuge: Extractor und Karten zeigen `Frist<TCmd>` als Ausgang eines Decide nicht als Kante zur Uhr; das Werkbank-Schema kennt nur die Datei-Konfig (Takt fehlt); Editor-Oberfläche zurückgestellt (Plan E4) | P3 |
| PF-6 | Read-your-writes-Index: ein Redis-Write je Event und Projektion, nicht abschaltbar (Schlüssel laufen seit PF-30 ab) | P2 |
| PF-25 | Rest: der Backstop gleicht alle 15 s den Heil-Plan im Speicher mit der ganzen Liste der offenen Vorgänge ab; nach einem Neustart weckt er alle auf einmal | P2 |
| PF-31 | Entscheidung offen: ein Wechsel der Snapshot-Schema-Version faltet heiße Aggregate einmal voll; ein neuer Emittent liest die Geschichte ab 0 | P3 |
| PF-26 | Rest: offene Warte-Knoten weckt der Takt alle 15 s; der Backstop deckelt bei 1 000 Weckungen pro Sekunde (≈ 15 000 Wartende) | P3 |
| PF-32 | Wachstum ohne Laufzeit-Risiko: Guards und Emittenten-Cursor je Stream nie gelöscht, DLQ ohne Aufräumen und Index, Einblick-Scans, Kopfzeilen 140 Byte je Event | P3 |
| PF-17 | Rest: die Zeitstempel-Regel kostet in Sim ≈ 20–35 µs je eingefügtem Event (Versatz je Zeile aus `uhr`); in Prod nur die Datenbankzeit | P3 |
| PF-7 | Quelle = ein Stream und ein Vorgang je Quell-Nachricht — Entscheidung offen für hochfrequente Quellen | P2 |
| W10-1 | Reflection im Codegen (`MetadataLoadContext`) und im Werkbank-Schema | P1 |
| W10-2 | Reflektive Serialisierung zur Laufzeit (Marten-Default, Marking-Store, Snapshot-`Clone`, HTTP-Antworten); kein Reflection-Verbot im Build | P1 |
| LLM-1 | 🤖-Rümpfe sind nur „baut und regelkonform“ geprüft, nicht fachlich: keine Beispiele je Slot, `Cqrs.Testing` nicht an Editor/🤖 angebunden | P1 |
| P1-1 | Multi-Node nur container-erprobt, Consul im `-dev`-Modus | P1 |
| P1-2 | Transport-Sicherheit: kein TLS; Identitäten für Prod fehlen | P1 |
| P1-7 | Deploy-Skripte veraltet (starten die Hosts ohne Umgebung, alte Konfig-Schlüssel) | P1 |
| W2-1 | Rest der Projektstruktur: Client-Framework hängt am Server-Framework, Generat-Namespaces, Identitäten aus einfachen Namen | P2 |
| W11-1 | Doppelte Wege: Handle-Pipeline/Trigger/`[Ingress]` ohne Nutzer, Prozess- vs. Fluss-DSL | P2 |
| W11-2 | Fristplaner hängt an der Handle-Welt (die Zeitlimits des Dirigenten laufen seit PF-4 über die Frist im Offen-Index); seit Phase 3 steht die Uhr (`Frist<TCmd>` aus dem Decide) daneben, die Handle-Frist heißt `FristPlanung<TCmd>` — Rückbau mit dem Alten | P2 |
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
| Konsum-/Prozess-Maschine | 🟢 / 🟡 (PF-13, PF-4b) |
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

Die Reihe **PF** sind Kosten der Garantien ([11](11-garantien.md) §11.12): die Garantie bleibt, der Mechanismus ist teurer als nötig.

**G-11 · Projektion übersprang Events im Störungslauf** (gemessen einmal, `vermessung/ergebnisse/2026-10-18_0405_5e2d293_server-wachstum-stoerung-1`).
In S1 fehlte im Read-Model ein komplettes Paar: für seinen Stream standen die Guards beider Bildpaar-Projektionen auf Version 3, die Versionen 4
(`BildVerfuegbar`) und 5 (`ImagePairKomplett`) waren nie angewandt, alle Partitionen standen am Kopf des Logs, nichts geparkt. Die drei Events
entstanden um 02:08:07 — laut `host.csv` drei Sekunden nachdem der Treiber den Prüfdomänen-Host hart beendet und neu gestartet hatte, fast
20 s nach dem Redis-Neustart (eine erste Lesart der Zeitachse, „während Redis stand“, war falsch). Beide Projektionen übersprangen dieselben
Events; gemeinsam ist ihnen der Horizont (einer je Host-Prozess) und die Seite derselben Partition. Den Batch-Leser hat der Umbau zu PF-28 nur
an einer Obergrenze geändert, die bei 3 258 Events nicht greift.
- *Geprüft, ohne Befund:* die Sequenz hat Cache 1; die Freigabe-Regel des Horizonts und die Reihenfolge Stream-Zeile vor `nextval` (auch für
  gemischte Group-Commits, `AnhaengeReihenfolgePostgresTests`); der Host öffnet je Partitions-Leser einen eigenen Store. Neu: I
  `HorizontStressGarantieTests` (16 Schreiber über Group-Commit und Bündel, ≈ 250 000 Events in 40 s, langsame Batch-Leser mit Neustarts —
  jedes Event genau einmal; zweimal grün). Eine erste Fassung teilte einen Store unter allen Lesern und verlor dadurch Effekte — ein Fehler
  des Tests, nicht des Hosts.
- *Gemessen:* nach dem Löschen allen Rest-Zustands (Mess- und Test-Datenbanken, Redis, Host-Logs) fünf Störungsläufe grün
  (`…_05*_server-g11-stoerung-1` bis `-5`); insgesamt 1 rot zu 27 grün.
- *Vermutet:* Rest-Zustand auf dem Server (Tobi), eine Lücke im Horizont beim harten Ende eines anderen Hosts am selben Postgres, oder ein
  Weg, auf dem Sequenzen als erledigt gelten, ohne angewandt zu sein. *Nächster Schritt:* `scripts/stoerungsserie.sh <N>` sichert beim
  nächsten roten Lauf Transaktionen und Commit-Zeiten der betroffenen Events (Commit-Zeitstempel sind auf dem Server eingeschaltet).

**G-12 · Fachliche Ablehnungen erreichen den Client nicht** (gefunden beim Verhaltens-Netz, Plan Pub/Sub-Neustrukturierung Phase 0). Das Aggregat
schickt eine Ablehnung gezielt an die Sitzung des Absenders (`AggregateActorBase.TryPublishRejectionAsync`, `TargetSubscriberId`), der Broker
stellt sie aber nur einer Sitzung zu, die den TYP abonniert hat (`BrokerShardActor`: je Typ eine Abonnenten-Tabelle). Die Sitzung abonniert
genau die Eingänge ihres Vertrags (`AkteurVertragsPruefung.Wende`) und immer `CommandFailed` — keine Ablehnungs-Typen. Der Blazor-Arbeitsplatz
bekommt deshalb auf einen abgelehnten Command (z. B. `FriereEin` auf einen leeren Datensatz → `DatensatzLeer`) keine Antwort, sendet dieselbe
Hülle dreimal (je 5 s) und meldet `CommandUnbestaetigt`, obwohl der Ausgang feststeht (docs/08 §8.4 sagt anderes). Nachweis:
`Verhalten.Tests/ClientVerhaltenTests.Eine_fachliche_Ablehnung_quittiert_den_Command_beim_Client` (Skip G-12). Behebt der Plan mit Phase 5
(Ablehnung als Hinweis an die Sitzung) und Phase 7 (Sitzung als Actor, gezielt über den Verteiler des Knotens).

**G-13 · Sitzungs-Kennungen kollidieren zwischen Knoten** (gefunden beim Verhaltens-Netz, Phase 0). `CqrsClientServiceImpl` vergibt
`session-NNNN` aus einem statischen Zähler je Prozess; die Abonnenten-Tabelle der Broker-Shards ist clusterweit und überschreibt bei gleicher
Kennung (`SubscriberRegistry`). Die ersten Sitzungen zweier Knoten heißen beide `session-0001`: eine gezielte Nachricht (Ablehnung,
`CommandFailed`) für die eine geht an die zuletzt angemeldete. In einem Test mit zwei Knoten im selben Prozess bliebe das unsichtbar (der Zähler
ist geteilt). Nachweis: `Verhalten.Tests/ZweiKnotenTests.Eine_Ablehnung_landet_bei_zwei_Knoten_an_der_richtigen_Sitzung` (zwei Host-Prozesse,
Skip G-13). Behebt der Plan mit Phase 7 (Sitzungs-Id nennt den Knoten, Verteiler je Knoten).

**G-14 · Read-your-writes nach dem Anlegen** (gefunden beim Verhaltens-Netz, Phase 0). Die Query-Brücke des Clients schickt die zuletzt
beschriebenen Ids mit (`expected_fresh_ids`), der Query-Dienst liest für sie die Stände der Projektion aus dem Versions-Index, und der Client
fragt nur nach, wenn ein Stand HINTER der ihm bekannten Version liegt (`QueryBridge.IstReadModelZurueck`). Für ein eben angelegtes Objekt hat
die Projektion noch keinen Eintrag geschrieben — kein Stand gilt als frisch, die Antwort ist „nicht gefunden“ (gemessen: `ErstelleDatensatz`,
Quittung, sofort `HoleDatensatz` → `DatensatzNichtGefunden`). Für Änderungen eines bekannten Objekts greift der Mechanismus. docs/11 §11.5
nennt read-your-writes ohnehin nur best-effort; Nachweis `Verhalten.Tests/ClientVerhaltenTests.Nach_dem_Anlegen_kennt_die_naechste_Query…`
(Skip G-14). Behebt der Plan mit Phase 7 („mindestens S auf v“: der Reader wartet begrenzt auf den Fortschritt seiner Projektion für S).

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
- Identitäten aus einfachen Typnamen (`BrokerIdentity`, `GeneratedAkteurRechte`). (Das vom Client gesetzte Cluster-Kind ist seit Phase 1 des
  Plans Pub/Sub-Neustrukturierung erledigt: Ziel und Aggregat folgen `ICommand<TAggregat>`, eine abweichende Hülle wird an der Leitung abgelehnt.)
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

**FB-1 … FB-4 · Funktion als Baustein und Uhr, bewusst offen nach Plan Phase 3** (2026-10-26). Phase 3 baut Funktion, Uhr und die neuen
Quellen neben das Alte; was erst spätere Phasen tragen, ist mit Absicht nicht vorgezogen, weil jede Vorab-Lösung ein zweiter Weg wäre, der
wieder verschwände. **FB-1:** der Generator kennt Hinweise als Eingang (`GeneratedFunktionsBausteine.Hinweisplan`), es fehlt der Zusteller —
das ist der schnelle Kanal aus Phase 6. **FB-2:** die Baustein-Form braucht für einen externen Worker einen Vertrag über Eingang und
Ausgaben auf der Leitung (`ITraegt`, Phase 7); bis dahin weist der Start `Laufort = Extern` für sie ab. Hand läuft schon über denselben
Vermittler (`BausteinSitzung`). **FB-3:** wer auf ein Event einer Funktion, Quelle oder der Uhr hört, wartet bis zum Poll; die Prüfdomäne
misst das nicht als Fehler, die Latenz ist aber spürbar. **FB-4:** reine Darstellungs-Lücken der Werkzeuge; die Garantien hängen nicht daran.

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

**PF-25 · Rest: Heil-Plan über die ganze Liste.** Seit PF-25 fragt der Prozess-Poll je Seite nur die eigenen Korrelationen ab, `/health` zählt
nur. Der Backstop liest aber alle 15 s die ganze Liste der offenen Vorgänge (`ListeOffeneAsync`) und hält je Knoten den Heil-Plan im Speicher;
nach einem Neustart ist er leer, und jeder offene Vorgang bekommt nach 15 s eine Heil-Weckung (bei 32 gleichzeitig). *Gerechnet:* bei 200 000
offenen Vorgängen ≈ 6 000 Blöcke und 3 MB je Abgleich. *Empfehlung:* die Heilung als zweite Frist im Offen-Index (Feld „nächste Heilung“ mit
Index, gesetzt beim Start in derselben Transaktion, verdoppelt nach jeder Heil-Weckung; Abfrage „die frühesten 1 000“ wie die Frist) — dann
entfallen Abgleich, Plan und Sturm. Offen: ob ein Send den Abstand zurücksetzt (ein Patch je Send).

**PF-31 · Volles Falten nach Schema-Wechsel, Emittent ab 0.** Passt die Schema-Version eines Snapshots nicht, faltet die erste Aktivierung den
ganzen Stream (bei einem heißen Aggregat Millionen Events, Minuten); ein neu hinzugefügter Emittent liest jeden abonnierten Stream ab 0.
*Entscheidung offen:* hinnehmen und beim Start warnen (Stream ohne gültigen Snapshot länger als N Events), bzw. fachlich festlegen, ob eine neue
Reaktion die Geschichte nachholt.

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

**PF-17 · Rest: Zeitstempel-Regel in Sim.** Die Horizont-Probe ist erledigt (§13.6). Offen bleibt: die Zeitstempel-Regel kostet in Sim
≈ 20–35 µs je eingefügtem Event, weil sie je Zeile den Versatz aus `uhr` liest; in Prod misst sie nur die Datenbankzeit. *Option:* den Versatz je
Transaktion einmal lesen (`set_config(…, true)`).

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

- **PF-26 · Rest: Warte-Knoten im Takt.** Ein offener Warte-Knoten liest seit PF-26 nur noch das Neue, wird aber weiter alle 15 s über seine Frist
  geweckt; ab ≈ 15 000 Wartenden reicht die Grenze des Backstops (1 000 je Sekunde) nicht, dann kämen auch fremde Zeitlimits zu spät. *Option:*
  über das Signal des Quell-Streams wecken (ein Verzeichnis „wer wartet auf Stream X“), der Takt nur als Netz mit wachsendem Abstand.
- **PF-32 · Wachstum ohne Laufzeit-Risiko** (Analyse 2026-10-18, docs/12 §12.5): Guards `ProjectionCheckpoint` je (Projektion, Stream) und
  Emittenten-Cursor je (Konsument, Stream) werden nie gelöscht (Zugriff über den Schlüssel; das Zurücksetzen löscht ohne Index auf
  `ProjectionId`); die Dead-Letter-Tabelle wird nie geleert und je `/health` gezählt, ohne Index sortiert; der Einblick (Sim) durchsucht je
  Vorgangsliste das ganze Log; die Beispieldomäne blättert mit `OFFSET` und lädt für Statistiken ganze Tabellen; Kopfzeilen kosten ≈ 140 Byte
  je Event, die Daten ≈ 70; Postgres auf dem Messserver läuft mit `shared_buffers = 160 MB`.

- **P3-1 · Mojibake** in `Domain.SourceGeneration/AggregateHandlerGenerator.cs`, `Infrastructure/Projections/ReadModelDepsReader.cs`,
  `Infrastructure/Serialization/ProtoMapper.cs`.
- **P3-2 · Toter Code und Kleinkram:** `CqrsFrameworkOptions`, `IViewModel` + `ViewModelGenerator`,
  auskommentierte Reflection-Scans (`EventTypeResolver`, `MartenEventTypeRegistration`), nicht gemappte
  Proto-Endpunkte in Host.Blazor (das Python-SDK ruft sie trotzdem), `BenchPing` ohne Treiber-Basis; Dateiname ≠ Klassenname bei einigen
  Generatoren; veraltete Kommentare und Meldungen (z. B. „drei Header“ in `Spur.cs`, „geteilt mit der Simulation“ in `ProzessManager.cs`, die Fehlermeldung in
  `AggregateDispatcherExtensions.ResolveAggregateType` spricht noch von der Namespace-Zuordnung; `ConnectionModule.cs` nennt die CommandId „deterministisch“, sie ist aber `Guid.NewGuid()` und nur über
  Wiederholungen gleich; `CqrsServiceExtension.cs` nennt noch `Cluster__Role`/`migrator|member` statt `Cluster__Rolle`/Allein|Migrator|Mitglied;
  `ProzessManager.cs` sagt „nie in einem Feld gehalten“ trotz HOT-Cache; `Cqrs.Codegen/Program.cs` nennt sich „reflexionsfrei“, liest aber per
  `MetadataLoadContext`; `deploy-multinode/Dockerfile` und `docker-compose.yml` erwähnen noch den entfernten LoadHarness).
- **P3-3 · Saga-DSL** in `Cqrs.Testing` kennt `Rufe` nicht (Voraussetzung für LLM-1).

## 13.4 Korrektheit vs. Reifegrad

Keine bekannten aktiven Korrektheits-Bugs mehr (G-1, G-7, G-3, G-4, G-2 erledigt, §13.6). **G-5** ist latent. Jede offene G-Lücke hat einen Test, der sie zeigt (Skip `G-n offen`, [11](11-garantien.md) §11.13).
Korrektheitsnah sind außerdem: **LLM-1** (vom LLM
geschriebene Logik nur auf „baut“ geprüft), **W10-1** (möglicher Hash-Unterschied zwischen Python-Basis und Server beim Handshake) und **WZ-1** (Prüfungen, die grün sind, ohne es zu beweisen). Alles andere ist Reifegrad,
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
   G-1, G-7, P1-6, PF-9), 2 (Actor-Lebenszyklus, G-3, PF-5), 3 (Batch-Konsum, PF-1, PF-2, G-4), 5 (Prozess-Poll, PF-4, G-8) und 4 (Inbox als Tabelle, G-2, PF-3)
   umgesetzt; Stand und Messung im Plan §8.
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

- **PF-23, PF-24, PF-25 (Teil), PF-26 (Teil), PF-27 bis PF-30 · Kosten, die mit der Datenmenge wuchsen (2026-10-18, Messserver; docs/12 §12.5,
  docs/03 §3.3, docs/04 §4.1/§4.5):** die Analyse wachsender Bestände fand Laufzeitpfade, deren Kosten mit der Geschichte statt mit der Last wuchsen.
  - **PF-23 Kompensation:** „ist der Gegenzug schon erledigt?“ las den ganzen Ziel-Stream ab 0 — in der Prüfdomäne die teuerste Abfrage (276 000
    Aufrufe, 105 Zeilen, 108 von 252 s Datenbankzeit eines Laufs). Jetzt aus dem Marking. Nachweis PD
    `MarkingCursorAequivalenzTests.Eine_Kompensation_liest_nicht…` (10 gegen 2 000 fremde Events im Posten; sabotiert: 3 980 Zeilen mehr).
  - **PF-24 kalter Fold:** ohne gespeicherten Stand las der Fold jeden Ziel-Stream ab 0, ohne Index auf der Korrelation über den ganzen Stream;
    der Cursor blieb am letzten eigenen Event, fremde dahinter las jede Weckung neu; das Marking ging nur alle 32 Weckungen durabel, beim
    Passivieren nie. Jetzt liefert der korrelierte Read den Kopf des Streams (Cursor bis dorthin) und liest ohne Cursor ab dem Beginn des
    Vorgangs; das Marking geht beim Stillwerden und beim Stoppen durabel, samt Auslöser- und Token-Zeit (ohne sie verlor ein kalt geladener
    Vorgang seine Zeitlimits). Nachweis PD `…Ein_kalter_Vorgang_ohne_gespeicherten_Stand…`, `…Fremde_Events_hinter_dem_eigenen_Ausgang…`,
    `…Beim_Passivieren_sichert…`, `EventReadGarantieTests` (Kopf, Untergrenze), I `AbfragePlanGarantieTests` (Stream mit 100 000 Events Geschichte:
    20 Blöcke; eine erste Form mit Hilfstabellen las mit veralteter Statistik 5 243); je sabotiert rot.
  - **PF-25 (Teil):** der Prozess-Poll las je Seite alle offenen Vorgänge, `/health` lud sie zum Zählen. Jetzt je Seite nur die Korrelationen der
    Seite (`OffeneAusAsync`, über den Schlüssel), `/health` zählt. Nachweis I `AbfragePlanGarantieTests` (200 000 offene; sabotiert rot). Rest: PF-25.
  - **PF-26 (Teil) Warte-Knoten:** las alle 15 s den Strom ab dem Auslöser; jetzt nur hinter dem zuletzt geprüften Event. P
    `PipelineFlussTests.Ein_wartender_Knoten…` (sabotiert 9 001 statt ≈ 1 000 Zeilen). Rest: PF-26.
  - **PF-27 Inbox:** BRIN auf `zeit` verfiel nach dem ersten Aufräumen (wiederverwendete Seiten, tote Zeilen); jetzt B-Baum, Aufräumen ab der
    letzten Grenze, die ältesten zuerst. I `InboxAufraeumenGarantieTests` (eine Million Quittungen: 606 Blöcke je Seite; BRIN 9 957, B-Baum ohne
    Untergrenze 1 994). Dabei gefunden: der Aufräumer las die DB-Zeit mit falschem Typ.
  - **PF-28 Ausschlussliste:** stand der Horizont, wuchs die Liste der Erledigten mit jedem Event und ging mit jeder Seite an die Datenbank
    (quadratisch). Jetzt höchstens 5 000; voll nur bis zur gesetzten Sequenz, hängende Streams werden geparkt. P
    `BatchLeserTests.Steht_der_Horizont…` (sabotiert 15 000 statt ≤ 5 100).
  - **PF-29 Rückstand:** `count(*)` ab der kleinsten Position, je Abruf von `/health`; jetzt über höchstens 1 000 Events. I `AbfragePlanGarantieTests`
    (sabotiert 11 565 Blöcke).
  - **PF-30 Redis:** keine Ablaufzeit, keine Speichergrenze, ein nie gelesenes Set `agg_idx` je Aggregat. Jetzt Ablauf
    (`VersionIndex.HaltbarkeitStunden`, 24), `agg_idx` entfällt, `maxmemory` in den Compose-Dateien.
  - Gemessen (E2E, gegen einen frischen Lauf des Ausgangsstands): Durchsatz 0,91–1,15 ×, Leerlauf unverändert, Exactly-once grün; der korrelierte
    Read kostet ≈ 30–40 µs mehr je Aufruf (Kopf und Untergrenze). Ein Störungslauf rot (G-11), der Wiederholungslauf grün.

- **PF-17 · Horizont-Probe und fällige Abfragen über den Index (2026-10-18; docs/12 §12.5):** die Frage „steht hinter der Position etwas?“
  (`exists(… seq_id > $1)`) durchsuchte die Tabelle von vorn — bei veralteter Statistik einer wachsenden Tabelle und seit PF-20 immer, weil die
  vorbereitete Anweisung einen allgemeinen Plan bekam (linear mit dem Log; bei Millionen Events Sekunden je Probe). Jetzt `order by seq_id limit 1`
  (jeder Plan nimmt den Index); dieselbe Falle hatten die fälligen Vorgänge des Backstops (jede Sekunde, ganze Tabelle der offenen) und die
  fälligen Fristen: Marten-Index auf das Feld, Abfrage sortiert und je Takt auf die frühesten 1 000 begrenzt. Die SQL-Texte der häufigen Abfragen
  stehen an einer Stelle (`EventStoreAbfragen`). Gemessen: Horizont-Abfrage S1 26 statt 474 µs, Datenbankzeit je Paar 0,67 ×. Nachweis: I
  `AbfragePlanGarantieTests` (500 000 Events, je 200 000 Dokumente; zugeschnittener und allgemeiner Plan, veraltete und frische Statistik; vor dem
  Umbau rot gesehen).

- **PF-22 · Rest der Marten-Bremse beim Lesen (2026-10-18; docs/03 §3.3, docs/12 §12.5):** der Check „wo bremst Marten noch“ fand Martens LINQ
  nur noch in `ReadPartitionAsync` (S1/S3 7 % der Host-Arbeit) und das Deserialisieren der Kopfzeilen zu einem Wörterbuch (S5 8,5 %); unter
  allen übrigen Marten-Aufrufen rechnet Marten selbst ≈ 1 % — alles nach SQL zu übersetzen lohnt nicht. Jetzt liest auch die Seite festes SQL,
  und die sechs Kopfzeilen eines Envelopes kommen als Text-Spalten. Gemessen: Host-CPU in S5-8/S5-64 ≈ 0,8 ×, S3-8 1,17 ×. Nachweis: PD
  `EventReadGarantieTests.Seiten_der_Projektionen_lesen_dasselbe_wie_Martens_LINQ` (mit falscher Grenze rot gesehen).

- **PF-20 · Vorbereitete Anweisungen (2026-10-17, Entscheidung Tobi; docs/12 §12.5):** Postgres parste und plante jede Anweisung neu — gemessen
  die Hälfte der Backend-CPU. Jetzt setzt das Framework in der Event-Store-Verbindung `MaxAutoPrepare = 20` (Npgsql bereitet je Verbindung die
  häufigsten Texte vor; ein Wert in der Verbindung der Umgebung gilt). Gemessen: S5-64 1,32 × (2 619 Vorgänge/s), S5-8 1,39 ×, S4 1,56 ×; ein
  Störungslauf grün. Gefahrlos, weil im Verkehr kein Schema entsteht (PF-18).

- **PF-21 · Event-Reads ohne Martens LINQ (2026-10-17; docs/03 §3.3, docs/12 §12.5):** das CPU-Profil des Hosts unter S5 zeigte 58 % der
  arbeitenden Stichproben in Martens LINQ-Übersetzung (`ReduceToConstant`) und 17 % im Kompilieren dynamischer Methoden — `QueryAllRawEvents().Where(…)`
  wurde je Aufruf neu übersetzt, die eingefangenen Werte kompiliert. Jetzt lesen `ReadStreamAsync`, `ReadKorreliertAsync` und `ReadEventAsync`
  mit festem SQL (dasselbe, das Marten erzeugte), Typ über die generierte Registrierung, Daten und Kopfzeilen über Martens Serializer. Gemessen:
  S5-64 1,67 × (1 988 Vorgänge/s), Latenz p50 0,26 ×, Host-CPU je Vorgang ≈ 9 → 3 ms; S3-8 1,28 ×. Nachweis: PD `EventReadGarantieTests`
  (Feld für Feld gleich Martens LINQ: alte Bytes, Kopfzeilen, Korrelation, Grenzen, archiviert, Tombstones; mit falscher Versionsgrenze rot
  gesehen), I-Garantie-Suite grün (dabei gefunden: Stores ohne Metadaten-Spalten — das SQL liest fehlende als `null`).

- **Prozess-Fold liest ab dem Ausgang (2026-10-17; docs/04 §4.5, docs/12 §12.5):** der korrelierte Read eines frischen Vorgangs las einen viel
  beschriebenen Ziel-Stream ab Version 0 (S3/S5: 29–42 % der Datenbankzeit). Jetzt nennt die Antwort des Aggregats die erste geschriebene Version
  (`CommandResult.ErsteVersion` → `EmitErgebnis` → `ProzessWake.QuellVersion`), der Fold liest ab dort; den Cursor rückt er über die Lücke nur,
  wenn dort nichts Eigenes aussteht. Gemessen: 31–66 µs statt 186–417 µs je Read, Datenbankzeit je Vorgang 0,68–0,80 ×, Durchsatz im Band.
  Nachweis: PD `MarkingCursorAequivalenzTests.Mit_Hinweis_gleich_Cursor_aus` (Diamant mit Kompensation, Fan-out; kalt, Router, verzögert),
  `…Langer_Ziel_Stream_mit_Hinweis_liest_nur_den_Ausgang` (gegen den Stand davor rot gesehen).

- **PF-19 · Marten-Code zur Laufzeit — bewusst hingenommen (Tobi, 2026-10-17; docs/12 §12.5):** Marten erzeugt und kompiliert den Speicher-Code
  eines Dokumenttyps beim ersten Gebrauch nach dem Start (Standard-Modus). Gemessen: S4 ohne Aufwärmrunde 962–1 019 Paare/s, mit 1 847–1 942 —
  je Typ ≈ 0,05–0,1 s, einmal je Start, danach nichts. Die Messung misst den warmen Zustand (Start, Warmlauf, ungewertete Aufwärmrunde je Szenario,
  seit `9188b87` auch vor S4). Martens Arbeitsweise bleibt: der Standard-Modus ist der am breitesten getestete Weg; Code beim Build (`TypeLoadMode.Static`)
  und ein Vorwärmen beim Start sind verworfen.

- **P1-3 · Neustart mit Consul nach hartem Abbruch (2026-10-17, Messserver; docs/12 §12.5):** nach einem harten Abbruch führte Consul den toten
  Knoten noch einige Sekunden als Mitglied (`ConsulProviderConfig`: TTL 3 s, Abmeldung nach 10 s), der Cluster legte Broker-Aktivierungen auf ihn,
  und der Start versuchte jede genau einmal — `TimeoutException` aus `PubSubStartupService`, Abbruch (Störungsläufe „Prod-Konfig“ ungültig). Jetzt
  wiederholen Aktivieren und Abonnieren der Broker Timeouts mit Backoff bis 60 s (heute `ClusterExtensions.FrageMitWiederholungAsync`: je Versuch
  eine Frage des Boten mit Frist; seit Phase 2 des Plans Pub/Sub-Neustrukturierung zählt jeder Versuch ohne Antwort gleich, gleich aus welchem Grund). Gemessen: zwei Störungsläufe mit Consul grün (53 019 und 52 756 Vorgänge), in beiden gelang beim Host der Prüfdomäne eine Aktivierung
  im zweiten Versuch. Nachweis: P `BrokerWiederholungTests`.

- **PF-18 · Schema lazy im Betrieb (2026-10-17; docs/03 §3.3):** ein Standalone-Host legte Tabellen und Funktionen eines Dokumenttyps erst beim
  ersten Zugriff an, mitten im Verkehr (gemessen: S4 in Art Prod 0,2 s länger; in Sim verdeckt, weil der Abgleich der Leseseite beim Start alle
  Read-Models leert und dabei anlegt). Jetzt legt `SchemaBeimStart` (Rolle Standalone, nach dem Siegel, vor allen übrigen Diensten) das ganze
  Schema an — derselbe Weg wie der Migrator im Cluster (`CqrsSchemaMigrator.ApplyAllAsync`), der nun auch die Read-Models anlegt: Marten kennt sie
  erst beim ersten Zugriff, ein Mitglied ohne Schema-Recht hätte ihre Tabellen nie gefunden. Die Regel für den Event-Zeitstempel wird nur noch
  geschrieben, wenn sie fehlt oder anders lautet, und dann mit Sperrfrist: ein `create or replace trigger` bei jedem Start kollidierte mit Inserts
  des noch laufenden alten Hosts (Werkbank-Abnahme: Host antwortete 90 s nicht, zeitweise). Nachweis: I
  `UmgebungE2ETests.Ein_Prod_Host_legt_das_ganze_Schema_beim_Start_an` (ohne den Schritt rot gesehen),
  `EventZeitPostgresTests.Ein_unveraenderter_Stand_fasst_die_Regel_beim_Start_nicht_an`; Werkbank-Abnahme dreimal grün.

- **G-9 · Eine lange offene Transaktion überholte die Positionen (2026-10-16; docs/03 §3.3, docs/04 §4.1.1, docs/11 §11.4):** Batch-Leser,
  Prozess-Poll und Poll der Emittenten rückten ihre Marke bis zur höchsten Sequenz vor, deren Event älter als 3 s war (Karenz gegen den
  Event-Zeitstempel). Blieb eine Transaktion länger offen (Sperre, Platten-Hänger, Störung), lag ihr Event nach dem Commit unter der Marke und
  wurde nie gelesen — auch das Signal half nicht, der Leser liest ab seiner Lese-Position (gemessen: der neue Test ist gegen den alten Code rot,
  die Marke stand auf der Sequenz hinter der noch laufenden Transaktion). Jetzt bestimmt der **Horizont der Vollständigkeit** die gesetzte
  Sequenz ohne Uhr aus den Transaktionsnummern von Postgres (`Horizont`, `MartenEventStore.GesetztAsync`): Zählerstand, dann die eigene
  Transaktionsnummer; frei, sobald keine kleinere mehr läuft. Voraussetzung (Wächter-Test): Marten schreibt im Modus `Quick` die Stream-Zeile vor
  den Events. Eine lange offene Transaktion hält die Positionen an statt überholt zu werden; der Store warnt nach 30 s. Nebenbei: die
  Partitionsformel als SQL liegt hinter der Naht (`PartitionsAusdruck`), `Konsum.Karenz` und der Parameter `stragglerGrace` sind weg. Nachweis:
  I `SequenceGapPostgresTests.Eine_lange_offene_Transaktion_wird_nicht_ueberholt`, `AnhaengeReihenfolgePostgresTests`; P `HorizontTests`.
  Gemessen (Messserver, `a78c21d`, docs/12 §12.5): gültig, zwei Störungsläufe grün; S1 Datenbankzeit je Paar 20,3 → 9,5 ms (gesetzte Sequenz
  entfällt, Partitionsseite 7,1 → 0,6 ms), Durchsatz im Band (S1 1,13 ×, S5 0,93–1,08 ×). Rest: PF-17.

- **G-10 · Zwei Uhren im Zeitlimit (2026-10-16; docs/03 §3.3, docs/konzept-werkbank.md §4.4):** die Aktivierung eines Zeitlimits ist der
  Zeitstempel des jüngsten gematchten Events, verglichen mit der DB-Uhr (`IDbClock`) — den Zeitstempel setzte Marten aber mit der Uhr des Hosts.
  Mit mehreren Knoten lief ein Zeitlimit je nach Uhr des schreibenden Knotens früher oder später ab; in der Sim stand nach einem Zeitsprung jedes
  neue Event in der ungesprungenen Zeit, jede neue Aktivierung wirkte um den Sprung alt (gemessen: ohne die Regel liegt der Zeitstempel um den
  Versatz daneben). Jetzt setzt die Datenbank den Zeitstempel (`EventZeit`: Regel `before insert` auf `mt_events`) mit demselben Ausdruck,
  den `IDbClock` liest (`UmgebungsDbClock.Ausdruck`, in Sim samt Versatz; `MartenDbClock` ebenso) — eine Uhr für alles Fachliche. Angelegt vor
  dem ersten Schreiben je Store bzw. vom Migrator, ein Mitglied ohne Schema-Recht prüft nur (Weasel kennt keine Trigger). Nachweis: I
  `EventZeitPostgresTests` (beide ohne die Regel rot gesehen), Werkbank-Abnahme grün. Gemessen: ≈ 20–35 µs je Event-Insert in Sim (PF-17).

- **Leerlauf: Nachlauf statt Grundlast (früher PF-14, 2026-10-16, Messserver; docs/04 §4.1.3, docs/12 §12.5):** L1 ≈ 30 und L5 ≈ 113
  Queries/s im Leerlauf waren keine bleibende Last, sondern der erste Poll-Takt nach der Last, den das 15-s-Fenster traf: unter Höchstlast rücken
  Prozess-Poll und Emittenten-Poll ihre Marke nicht vor (Weckungen an ausgelastete Manager bleiben unbestätigt, G-8) und lesen danach einmal
  nach (L1 31 000, L5 64 000 Events in ≈ 5 s), die Batch-Leser schreiben ihre Position einmal fest, in L5 arbeitet der Router noch Signale ab.
  Belegt mit einer Zeitreihe über 5 min nach der Last und nach einem Neustart (Diagnose `--art leerlauf`): danach 2,4–5 Queries/s, 1–3 % CPU.
  Geändert: die Messung wartet den Nachlauf ab (alle Konsumenten am Kopf) und weist Dauer und Queries eigens aus, das Fenster umfasst zwei
  Poll-Perioden; ein Poll-Read ohne Neues kostet eine Abfrage statt zwei (`ReadPartitionAsync`, `ReadChangedStreamsAsync`). Gemessen: L1 3,8
  Queries/s und 2,6 % CPU, L5 2,6 und 1,5 % — E5-Ziel erreicht; Nachlauf L1 48 s, L5 15 s; zwei Störungsläufe grün. Nachweis: I
  `SequenceGapPostgresTests.Hinter_der_Marke_nichts…` (Gegenprobe: schluckt die Abkürzung junge Events, ist er rot). Rest: PF-15.

- **Aggregat-Bündel (2026-10-16, Messserver; docs/03 §3.1):** ein heißes Aggregat war an „ein Command je Commit“ gebunden (Single-Writer +
  sicherer Commit ≈ 4 ms). Jetzt sammelt der Actor während des Commits (`ReenterAfter`), entscheidet die gesammelten nacheinander im Speicher
  und hängt sie als Teile eines Appends an (`AppendTeil`, `AppendTeileAsync`; Metadaten, Spur und Quittung je Command); scheitert der Append,
  lädt er frisch und behandelt jeden einzeln (Tobi: Sammeln während des Commits, Rückfall einzeln, immer an, `Schreibpfad.BuendelGroesse` 64).
  Gemessen (A-B-B-A): S3-8 1,72 ×, S3-64 2,29 × (Latenz p50 S3-8 1,4 s → 15 ms), S5 1,01–1,07 ×; zwei Störungsläufe grün. Nachweis: PD
  `SchreibGarantieTests.Ein_heisser_Posten_buendelt…`, `…Im_Buendel_entscheidet_jeder_Command_auf_dem_Stand_nach_dem_vorigen`,
  `…Ein_zweiter_Schreiber_mitten_im_Ansturm…` (Gegenprobe: ohne Anwenden im Bündel fallen alle Schreib-Garantien).

- **Prozess-Manager liest nur, was er nicht schon weiß (2026-10-16, Messserver; docs/04 §4.5, docs/03 §3.3):** die Datenbankarbeit je Vorgang
  lag zur Hälfte im Lesen des Managers, die meisten Ziel-Stream-Reads kamen leer zurück (0,4 Zeilen je Abfrage). Jetzt: Router-Weckungen
  mit Event lesen nur dessen Stream (nichts, wenn schon gefaltet), Weckungen nach Send bzw. Funktions-Ergebnis tragen Ziel und Vorgang, ein
  leer gelesener Ziel-Stream gilt als gesehen, ein frischer Vorgang lädt nichts aus dem Store und liest nie gefeuerte Ziele nicht, der
  Kopf-Zustand des eigenen Logs bleibt im Speicher. Der Offen-Index ist eine Beilage im Append von `ProzessGestartet`/`ProzessBeendet` (eine
  Transaktion; `IProzessOffenIndex.MarkiereOffenAsync` entfällt), der Marking-Stand wird nur gelöscht, wo er liegen kann. Gemessen: S5-64
  2,7 ×, S5-8 2,3 ×, S1 1,40 × (A-B-B-A), Transaktionen je Vorgang S5-64 4,4 → 1,1, zwei Störungsläufe grün. Nachweis: PD
  `MarkingCursorAequivalenzTests` (kalt, mit Router-Weckungen, verzögert, Abfragen-Zählung), I `GarantiePostgresTests.Vorgangs_Beilagen…`
  (Gegenprobe rot), P `ProzessStartGarantieTests`. Eine rote Gegenprobe für das sparsame Lesen fehlt: zwei Sabotagen hält das erneute Feuern
  des offenen Commands auf, die dritte (Cursor springt zu weit) lief ohne Ergebnis zu lange und wurde abgebrochen.

- **Inbox als Tabelle (früher G-2, PF-3; Plan Garantien und Durchsatz, Phase 4, 2026-10-15, Messserver; docs/03 §3.1/§3.3, docs/04 §4.4):**
  - **E1 = A** (Tobi): die Inbox ist eine Tabelle `cqrs_inbox` (CommandId eindeutig, Stream, abgelehnt, Zeit), je Command eine Quittung in der
    Transaktion seiner Events (Group-Commit: eine Anweisung je Batch). Eine Wirkung schreibt keine Marke mehr; die Marken `KommandoVerarbeitet`
    (Noop) und `KommandoAbgelehnt` bleiben für den Prozess-Fold. Inbox nicht mehr in Rehydration und Snapshot; im Actor nur ein Cache (1 000).
    Eine Wiederholung, die der Cache nicht kennt, scheitert am Schlüssel — der Actor quittiert den Ausgang des ersten Mals, auch wenn er jetzt
    anders entschiede. Martens Wiederholung (Polly) nimmt Eindeutigkeitsverletzungen aus (sonst viermal Rollback des Batches).
  - **E2:** Horizont statt Fenster — `Schreibpfad.InboxHorizontTage` (30), `InboxAufraeumer` mit Advisory-Lock als Lease. **E3:** keine
    Bestandsdaten zu übernehmen (Sim-DBs werden neu angelegt).
  - **Schnell anhängen:** Martens Anhänge-Modus `Quick` — eine Schreib-Runde ohne Vorab-Read der Stream-Köpfe und ohne Sequenz-Abfrage; OCC
    über `update … where version = erwartet`, Metadaten unverändert, keine Tombstones mehr. Vereinbar mit Group-Commit und Garantien (Tests
    in beiden Modi).
  - Lücken-Test ohne Skip: PD `SchreibGarantieTests.Eine_alte_CommandId_jenseits_der_Inbox_Grenze…`; neu: `…Eine_Wiederholung_nach_dem_Neustart…`,
    `…Ein_Duplikat_mitten_im_Group_Commit…`, `…Die_Inbox_vergisst_erst_jenseits_des_Horizonts`, I `GarantiePostgresTests.Effekt_und_Quittung…`
    (je Rich und Quick). Mit sabotierter Regel (keine Quittung) fallen fünf der sieben Schreib-Garantien.
  - Gemessen (docs/12 §12.5, A-B-B-A gegen den Stand davor): S3 1,17–1,18 ×, S5 1,06–1,08 ×, S1 1,04 ×, S4 1,01 ×; Queries je Vorgang 0,78–0,89 ×;
    S5-8 je Vorgang Event-Inserts 11,5 → 6,9, Stream-Kopf-Reads 4,8 → 0. Zwei Störungsläufe grün. Offen daneben war PF-14 (Leerlauf, inzwischen erledigt).

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
    PD `AblaufGarantieTests.Absturz_zwischen_Emit_und_Cursor…` neu. Das Inbox-Fenster selbst schloss Phase 4 (G-2, oben).
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
