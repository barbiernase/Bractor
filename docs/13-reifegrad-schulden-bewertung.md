# 13 — Reifegrad, Schulden & Bewertung

**Die eine Quelle für „offen“.** Andere Dokumente (`CLAUDE.md`, Kapitel 01–12, Konzepte) nennen Schulden höchstens kurz und verweisen
hierher. Stand: 2026-10-28, nach Phase 4b des Plans Pub/Sub-Neustrukturierung (Funktionen als Actors mit Zustand, Ressourcen je Aktivierung und
Hinweis-Netz; davor Phase 4: Dirigent, Reaktion, Fluss-DSL, Katalog-Form und Handle-/Trigger-Welt zurückgebaut). Maßstab sind die sechs Invarianten (`CLAUDE.md`) und die Werkbank-Leitsätze W1–W12
(`docs/konzept-werkbank.md` §2). Zahlen und Messwerte stehen in `docs/12-tests-und-vermessung.md`.

## 13.0 Offen auf einen Blick

**Umbau (2026-10-26):** [plan-pubsub-neustrukturierung.md](plan-pubsub-neustrukturierung.md) löst Broker, Signale, Prozess-Manager, Fluss- und
Prozess-Sprache, Handle-/Trigger-Welt und den Redis-Versionsindex ab. Mit Phase 4 sind W11-1, W11-2, PF-4b, PF-25, PF-26, P2-5, P3-3, FB-2, FB-3
und VM-1 erledigt (§13.6), mit Phase 4b FB-1 (Hinweise erreichen Funktionen über das Hinweis-Netz), mit Phase 5 die Marken im Log (keine
eigene ID; Noop und Ablehnung nur noch in der Inbox, eine Version je Aggregat, §13.6), mit Phase 6 die Signale und der Redis-Versionsindex
samt PF-6 (Wecker am Commit, Stand je Id aus dem Fortschritt der Projektion, §13.6); PF-7, PF-10, PF-13 und AK-1 werden mit den späteren Phasen
abgeschlossen oder umgeschrieben. Mit Phase 7 sind der Broker, G-12, G-13, G-14 und FB-10 erledigt (Sitzung als Actor, Verteiler je Knoten, „mindestens S auf v“, §13.6). Phase 9
(direkte Abfrage eines Aggregats) lässt FB-19 offen. Die Befunde der Actor-Praxis vom 2026-10-18 stehen im Plan §1.7.

| ID | Kurz | Prio |
|---|---|:--:|
| G-18 | Nach dem harten Neustart unter Last bleibt mitunter eine Partition einer Funktion für immer stehen (VT `NeustartTests.Nach_dem_harten_Neustart_unter_Last…`, Funktion `Verteilabwicklung` der Prüfdomäne): ihre Position rückt nicht mehr vor, obwohl der Takt (2 s) weckt — die anderen sieben Partitionen laufen durch. Exactly-once bleibt (kein Eingang wirkt zweimal), die Vollständigkeit nicht: ≈ 100–800 Verteilungen bleiben offen. Daten (2026-10-29, Lauf auf `73ade32`): Partition 5 steht bei Position 229 997, die übrigen bei 296 194; der nächste Eingang dahinter (seq 229 998) ist ein `PostenReserviert` mit fremdem Beleg, für den die Funktion nichts ausgibt — er dürfte sofort fertig sein. Verdacht: ein Lauf, der nie endet, hält den einzigen Slot der Partition (PF-33), danach liest der Actor nicht mehr (`Lies`: keine freien Slots). Älter als Phase 9: gegen `3e16e9a` (Phase 8) 1 von 3 Läufen rot, gegen `73ade32` 3 von 4; davor war der Test in den Läufen der Phasen 7 und 8 grün | P1 |
| G-5 | Rehydration zählt die Version je materialisiertem Event: ein Split-Upcast verschiebt die Version | P2 |
| G-6 | Garantie ohne eigenen Nachweis-Test: Fehler nach dem Append im Aggregat-Actor (P10) | P2 |
| G-16 | Lesen nach Schreiben im Verhaltens-Netz einmal verfehlt (`ClientVerhaltenTests.Nach_dem_eigenen_Command_sieht_die_naechste_Query…`, 1 von 3 Läufen am 2026-10-28): die Query direkt nach der Quittung zeigte den eigenen Stand nicht; vermutlich derselbe Mechanismus wie G-14 oder der damalige Redis-Index (nach dem Commit geschrieben, überschreibend, mit Ablauf); seit Phase 6 kommt der Stand aus dem Guard der Projektion (mit den Wirkungen in einer Transaktion) — am 2026-10-28 mit diesem Stand wieder einmal in drei vollen Läufen rot — der Redis-Index war es also nicht. Seit Phase 7 wartet die Leseseite selbst auf „mindestens S auf v“ (keine Nachfrage im Client mehr); ob G-16 damit weg ist, zeigt erst eine Reihe voller Läufe | P2 |
| G-15 | Hinweise zwischen zwei Actors ohne zugesicherte Reihenfolge: der Plan (§2.9) sagt „zwischen zwei Actors bleibt die Reihenfolge“, der Bote meldet aber jede Nachricht für sich — zwei Hinweise desselben Absenders an dieselbe Adresse können sich überholen; ohne Nachweis-Test | P2 |
| PF-36 | Seit dem Wecker (Phase 6) liegt S5 unter Parallelität niedriger: A-B-B-A nur S5 gegen den Code von Phase 5 — S5-8 343/415 (A) gegen 243/293 (B) Vorgänge/s, Mittel 0,71 ×; S5-64 329/363 gegen 224/334, 0,81 ×; S5-1 dagegen 1,09 ×, S3-64 1,15 ×, S1 gleich. Die Läufe streuen stark (A in S5-8 343 bis 415); Transaktionen und Queries je Vorgang sind gleich, der Host ist in B weniger ausgelastet — er wartet. Ursache offen (Verdacht: Zeitpunkt des Weckrufs am Commit statt nach der Antwort des Aggregats im Zusammenspiel mit einem Slot je Partition, PF-33). Profil unter S5-8 nur für B (`…_server-pf36-sonde-b`, 255 Vorgänge/s, Host 4,5 Kerne): ≈ 66 % der arbeitenden Host-CPU liest Events (`LeseEventsAsync`) — bei einem Slot je Partition liest ein Funktions-Actor nach jedem fertigen Eingang wieder eine volle Seite (bis 500), um einen zu starten; davon 30 % in `EventEnvelope..ctor` (`Guid.NewGuid` als Vorgabe für `EventId`, `CausationId`, `CorrelationId`, vom Leser sofort überschrieben) und 24 % JSON. Dasselbe Profil für A (`…_server-pf36-sonde-a`, 220 Vorgänge/s, Host 4,3 Kerne) hat dieselbe Gestalt (`LeseEventsAsync` 64 %, `EventEnvelope..ctor` 30 %, JSON 23 %), Transaktionen und Queries je Vorgang sind gleich; unter dem Profiler liegt B sogar vorn (255 gegen 220). Der Unterschied A-B ist im Profil also nicht zu sehen — kein Umbau, solange er nicht belegt ist. B enthielt hier schon die Behebung von G-17 (`pg_sequence_last_value` in der Probe), das A-B-B-A noch nicht; ein neues A-B-B-A nur für S5-8 (≈ 20 min) klärt, ob die Senkung bleibt. Unabhängig davon ist das Neulesen der Seite der größte Hebel unter Last, in A wie in B | P2 |
| PF-34 | Eine Funktion mit Zustand rechnet einen Eingang zugleich (Slots gelten nicht) und startet während ihrer Sicherung nichts; die Last je Sicherung wächst mit dem Zustand (Intervall `Zustand:SichernAlleSekunden`) — ungemessen | P3 |
| PF-35 | Bindung je Adresse (A8) für jede Funktion draußen, auch ohne Zustand: die Adressen verteilen sich gerecht über die Träger, aber eine Adresse läuft nie über zwei Träger zugleich; bei einem Slot je Partition (PF-33) ohne Wirkung, mit vielen Slots je Partition ungemessen | P3 |
| PF-33 | Funktionen laufen ohne Eintrag in der Umgebung mit einem Slot je Partition: S5-8/S5-64 fallen auf ≈ 300 Vorgänge/s (0,22 × bzw. 0,13 × der Basis); mit 16 Slots 1,18 × bzw. 0,56 × — Standard und Rest bei 64 Sendern offen | P1 |
| PF-13 | Unter S5-64: der Nachschub (früher Reaktion, seit Phase 4 die Funktion `Nachschub`) wartet am heißen Posten hinter den Reservierungen — p99 im Sekundenbereich (Zahlen docs/12) | P3 |
| PF-10 | Datei-Treiber meldet eine Datei nach der anderen ins Log (Rückstau): das Melde-Tempo ist die Decke von S1 — Entscheidung offen | P2 |
| PF-15 | Grundlast ohne Eingang wächst mit Projektionen × Partitionen: der Batch-Poll weckt alle 30 s jede Partition (je eine Abfrage), dazu jede Sekunde DB-Uhr und Fristen — heute L1 3,8 Queries/s, bei 50 Projektionen ≈ 16; seit Phase 3 dazu die Uhr (eine Abfrage je Sekunde) und je Funktion und Partition ein Poll | P3 |
| FB-8 | „Python-Funktion hört einen Hinweis“ ist nur geteilt nachgewiesen: das SDK (pytest, Antwort als Ganzes) und der Weg des Servers über dieselbe Sitzung (`BausteinSitzung` hinter dem Hand-Ausführer) — kein echter Python-Prozess über gRPC, weil keine Anwendung mit Leitung eine Funktion draußen hat, die einen Hinweis hört | P2 |
| FB-11 | Ein Commit, dessen Ausgang mehrdeutig blieb (G-3: die Events stehen im Log, das `SaveChanges` meldete trotzdem einen Fehler), weckt niemanden — der Wecker hängt am erfolgreichen Commit; die Konsumenten holt erst der Takt (bis 30 s). Selten, folgenlos für Exactly-once | P3 |
| FB-13 | `TrainingsWaechterTests.Ein_Training_ohne_Meldung_bis_zur_Frist…` (Verhaltens-Netz, Frist des Trainings-Wächters über die Uhr) war am 2026-10-28 in einem von drei vollen Läufen nach 35 s rot, einzeln und in den beiden anderen Läufen grün; am 2026-10-28 abends (Phase 7, `d9b2676`) wieder einmal rot, diesmal mit Meldung: von zwei Trainings bekam der Träger (vier Slots) nur den Eingang des ersten, der zweite kam nicht in 30 s. Die Umgebung des Tests läuft mit dem Standard-Takt (30 s) — der Verdacht ist ein Weckruf an den Funktions-Actor, der nicht wirkte, und erst der Takt hätte den Eingang geholt (eine Frist ist es nicht: sie kommt im Test erst danach) | P2 |
| FB-9 | Wechselt die Zustands-Ablage einer Funktion (Datei ↔ Dokument) oder geht das Verzeichnis verloren, lädt die Aktivierung den gesicherten Zustand nicht mehr (lauter Fehler, kein stiller Neubeginn); eine Migration oder ein bewusster Neubeginn fehlt | P3 |
| FB-5 | Kreise Funktion ↔ Aggregat: keine Build-Prüfung (R6 des Plans). Ein Kreis ist der Normalfall eines Ablaufs (Vorgang → Funktion → Vorgang); ob und wie ein Kreis ohne fortschreitenden Zustand erkannt werden soll, ist offen | P3 |
| FB-6 | Quell-Nachricht vom Client nur über den JSON-Eingang: das Trigger-Protokoll der Leitung ist entfernt, einen gRPC-Weg (und SDK-Aufrufe) gibt es auch nach Phase 7 nicht — Phase 7 hat Sitzung, Verteiler, Lesen und Ausgangskorb gebaut, nicht `ISendet<Quell-Nachricht>` | P2 |
| FB-14 | Das Python-SDK kennt nur die Leitung von Phase 7 (neue Nachrichten werden erkannt, `aktualisiert` nur geloggt), aber weder Lesen ab Position noch Ausgangskorb noch neu Fragen — die stehen nur im C#-Client (`GrpcProxy.LiesLogAsync`, `IAusgangskorb`) | P3 |
| FB-15 | Überlast an einer Sitzung: über 1000 offenen Zustellungen verwirft der Verteiler (`Rueckstau`) und schreibt nur eine Warnung ins Log, keinen Spur-Hinweis; die Nachrichten des Clients selbst (Commands, Queries) laufen ohne Grenze in die Mailbox der Sitzung | P3 |
| FB-16 | Neuaufbau ohne Schutz vor altem Code: eine Generation trennt nur, was eine alte Aktivierung NACH dem Zurücksetzen noch mit ihrem alten Fortschritt schreibt; ein Knoten mit altem Projektions-Code, der danach den neuen Fortschritt lädt, schreibt mit altem Code in die neue Generation (die Generation trägt keinen Code-Stand). Dazu schaltet nur der `ProjectionRebuilder` erst nach dem Aufbau um; der Sim-Start (`ProjektionsStandDienst`) schaltet sofort um (leere Sicht während des Aufbaus), einen Neuaufbau in Prod löst nichts aus | P3 |
| FB-17 | Marten kann `Id<T>` nicht als Dokument-Identität (sein Codegen bricht an generischen Ids, Probe 2026-10-28): ein Read-Model mit typisierter Id bekommt eine generierte Speicher-Identität `Schluessel` (die UUID steht damit zweimal im JSON) und muss `partial` sein (CQRS111); LINQ über `Id<T>`-Felder geht über `RegisterValueType` je verwendetem `Id<X>` | P3 |
| FB-18 | Datei-Stores liegen ohne Eintrag `Stores:<Store>:Verzeichnis` unter dem Temp-Verzeichnis je Datenbank und Schema des Logs (`StoreOrte`); wird die Datenbank gelöscht, bleiben die Dateien — in Sim baut der Start dann neu auf (neue Generation), in Prod nicht. Der Rückstand (`KonsumRueckstand`) liest den Fortschritt aus Marten: richtig für den Marten-Store und den Fortschritt beim Framework, nicht für einen künftigen Store der Klasse 1 außerhalb von Marten | P3 |
| FB-19 | Die Antwort eines Aggregats auf eine direkte Query (Phase 9) wird nicht beobachtet: die Sitzung beobachtet nur Antworten mit Projektionen dahinter, ein späterer Commit des Aggregats bringt dem Client kein `Aktualisiert` — wer den Stand live braucht, hört die Events des Aggregats (`IHoert`) oder fragt neu. Ein Weg über die Verteiler hinge am heißen Pfad (je Commit eine Meldung, auch für Typen, die kein Client hört) | P3 |
| PF-37 | „P hat S weiterverarbeitet“ geht nach JEDEM Commit einer Projektion, der Events anwandte, als Nachricht an den Verteiler jedes Knotens — auch wenn keine Sitzung beobachtet; nicht gemessen (Plan §8: Zusammenfassungs-Takt und Grenze der Abhängigkeitsmengen „in Phase 7 messen“) | P3 |
| FB-7 | Eine Funktion draußen ohne verbundenen Träger staut ihre Eingänge still im Log (Rückstand in `/health`, aber keine eigene Meldung „kein Träger“); ein Träger, der Eingänge annimmt und nicht beantwortet, hält bei einem Slot je Partition die übrigen Streams der Partition bis zum Ablauf der Lease (30 s) auf | P3 |
| FB-4 | Werkzeuge: Extractor und Karten zeigen `Frist<TCmd>` als Ausgang eines Decide nicht als Kante zur Uhr; das Werkbank-Schema kennt nur die Datei-Konfig (Takt fehlt); der Zustandstyp einer Funktion (`IZustand<T>`) ist eine Code-Karte im geteilten Block statt an seiner Funktion, das Hinweis-Netz hat keine eigene Kante; Editor-Oberfläche zurückgestellt (Plan E4); die Oberfläche nennt noch den entfallenen Umgebungs-Abschnitt `VersionIndex` und einen Schalter „TrackDeps (Redis-Deps)“ | P3 |
| PF-31 | Entscheidung offen: ein Wechsel der Snapshot-Schema-Version faltet heiße Aggregate einmal voll; eine neue Funktion liest die Geschichte ab 0 | P3 |
| PF-32 | Wachstum ohne Laufzeit-Risiko: Guards je Stream und Festhalte-Streams je (Funktion, Eingang) nie gelöscht, DLQ ohne Aufräumen und Index, Einblick-Scans, Kopfzeilen 140 Byte je Event | P3 |
| PF-17 | Rest: die Zeitstempel-Regel kostet in Sim ≈ 20–35 µs je eingefügtem Event (Versatz je Zeile aus `uhr`); in Prod nur die Datenbankzeit | P3 |
| PF-7 | Quelle = ein Stream und ein Vorgang je Quell-Nachricht — Entscheidung offen für hochfrequente Quellen | P2 |
| W10-1 | Reflection im Codegen (`MetadataLoadContext`) und im Werkbank-Schema | P1 |
| W10-2 | Reflektive Serialisierung zur Laufzeit (Marten-Default, Snapshot-`Clone`, HTTP-Antworten); kein Reflection-Verbot im Build | P1 |
| W10-3 | Reflection im Test-Werkzeug: `Cqrs.Testing` spiegelt den Zustand eines Aggregats per Reflection (Vergleich in `Dann`) | P3 |
| LLM-1 | 🤖-Rümpfe sind nur „baut und regelkonform“ geprüft, nicht fachlich: keine Beispiele je Slot, `Cqrs.Testing` nicht an Editor/🤖 angebunden | P1 |
| P1-1 | Multi-Node nur container-erprobt, Consul im `-dev`-Modus | P1 |
| P1-2 | Transport-Sicherheit: kein TLS; Identitäten für Prod fehlen | P1 |
| P1-7 | Deploy-Skripte veraltet (starten die Hosts ohne Umgebung, alte Konfig-Schlüssel) | P1 |
| W2-1 | Rest der Projektstruktur: Client-Framework hängt am Server-Framework, Generat-Namespaces, Identitäten aus einfachen Namen | P2 |
| W3-1 | Ableitungen im Editor-JS (Karten-Ids, Hör-Menge, Lambda-Parameter, Auto-Zuordnung, Kardinalität) | P2 |
| WB-1 | Werkbank: Geste p50 unter Fremdlast 346–470 ms (Ziel 300 ms), LLM-Kontexte kalt, CLI `--schreiben`, Renamer ohne String-Literale | P2 |
| WB-4 | Die Werkbank kennt genau eine Anwendung (`StandardWurzel`, `ServerHost`); eine Anwendung anlegen ist nicht beschrieben — Entscheidung offen | P2 |
| WB-5 | Der Einblick der Werkbank kennt keinen fachlichen Zustand „fertig“ mehr (früher `ProzessBeendet`) und seit Phase 5 auch kein „gescheitert“ (eine Ablehnung steht nicht im Log, sie kommt nur als verlierbarer Spur-Hinweis): ein Vorgang läuft oder ruht | P3 |
| WZ-1 | Prüf-Lücken der Werkzeuge: Grammatik-Gate bestätigt sich selbst, Fixpunkt ohne Akteure/Verträge/Clients/Funktionen/Dienste, Sonde ohne `SendeJe`/Count-Join/Kompensation/`IMensch`/`IstClient` | P2 |
| AK-1 | Akteure/Clients: `als` im Envelope, Funktionen über C# tragen, Anmeldung je Person | P2 |
| P2-6 | Schreibpfad: paralleler Drain skaliert sublinear | P2 |
| P2-8 | Hartkodierte Test-Verbindungsdaten und übrige Intervalle (der Takt je Partition ist seit 2026-10-28 einstellbar: Umgebung `Konsum.TaktSekunden`) | P2 |
| P2-9 | Toter Lesepfad `LoadStateAsync`, Identitäts-Vertrag nur per Kommentar | P2 |
| P2-10 | Monitoring schmal, Default-Credentials, kein OTel, Logging per `Console.WriteLine` | P2 |
| P2-11 | Python: Registry von Hand gepflegt, Kleinkram im SDK | P2 |
| P3-1 | Mojibake in drei Dateien | P3 |
| P3-2 | Toter Code und Kleinkram | P3 |

## 13.1 Gesamteinschätzung

Das Projekt ist ein **konzeptionell kohärentes** CQRS/ES-Framework: wenige tragende Ideen (Log = Wahrheit, Single-Activation,
generiertes typbasiertes Dispatching, ein Emit-Weg, scharfe Durabilitätsgrenze, vier Konsumenten als eine Maschine) sind mit Disziplin
durchgezogen — bis in den Multi-Node-Transport, die Clients (Blazor und Python mit Vertrag) und die Werkbank. Die Compile-Zeit-Guards
(62 Diagnose-IDs in 12 Analyzern und den Generatoren, dazu Boot-Guards) und die ehrliche Test-Kultur („nie faken, was man nicht besitzt“)
heben es über typische Eigenbau-Frameworks.

**Der Kern ist solide und gemessen grün.** Die offenen Punkte liegen fast durchweg an zwei Stellen: an den **Rändern** (Transport-Sicherheit,
Multi-Node-Betrieb) und an der **Allgemeingültigkeit** (W2/W10/W11): Komposition und Werkzeuge kennen die Projekte der Beispieldomäne beim Namen,
Werkzeuge lesen teils per Reflection. Die alte Handle-/Trigger-Welt und der Dirigent sind mit Phase 4 des Umbaus zurückgebaut.

| Subsystem | Reife |
|---|:--:|
| Schreibseite | 🟢 / 🟡 (G-5) |
| Konsum, Funktionen, Uhr | 🟢 / 🟡 (PF-13, G-15, FB-8) |
| Generatoren & Analyzer | 🟢 / 🟡 (W10-1, W2-1) |
| Multi-Node / Transport | 🟢 / 🟡 (P1-1, P1-2) |
| Werkbank (Extractor, Editor, Server) | 🟢 / 🟡 (LLM-1, W3-1, WB-1, WB-4, WZ-1) |
| Clients (Blazor, Python) | 🟢 / 🟡 (AK-1, P1-2) |
| Tests & Vermessung | 🟢 |

## 13.2 Stärken

1. **Invarianten-Disziplin über Sprachgrenzen.** Dieselben Invarianten tragen Backend, Blazor und Python.
2. **Compile-Zeit statt Laufzeit.** Analyzer, Generator-Diagnosen und Boot-Guards verwandeln ganze Fehlerklassen (Fehlrouting,
   Dangling-Command, unbounded Token, Wire-Drift, offene Ausgänge, Funktionen ohne Laufort) in Build-Fehler.
3. **Wenige Bausteine.** Ein Ablauf ist ein Aggregat plus Funktionen plus Frist — kein eigener Ablauf-Interpreter; Exactly-once kommt aus der
   Inbox des Aggregats und dem Festhalten der Funktion, beides einmal geschrieben.
4. **Ehrliche Nicht-Autoritativität.** Jeder Beschleuniger (Redis, Snapshot) hat einen Voll-Read-Fallback.
5. **Selbstbeobachtung.** Die Werkbank zeigt das System als Karten aus dem Code; durchgespielt wird das echte System, sichtbar über die Spur.
6. **Test-Kultur.** Store-Semantik nur gegen echtes Marten; generative Vollständigkeits-Guards; Fremd-Domäne als Agnostik-Beweis (`--sonde`).

## 13.3 Befunde

Schweregrade: **P0** = bekannter aktiver Korrektheits-Bug · **P1** = wichtig, produktions- oder allgemeingültigkeitsrelevant ·
**P2** = Wartbarkeit/Hygiene · **P3** = kosmetisch. Die Reihe **G** sind Lücken in den Garantien aus [11](11-garantien.md).

### P0

Keine offenen P0-Befunde (G-1 und G-7 erledigt in Phase 1 des Plans Garantien und Durchsatz, §13.6).

### P1

Die Reihe **PF** sind Kosten der Garantien ([11](11-garantien.md) §11.12): die Garantie bleibt, der Mechanismus ist teurer als nötig.

**PF-10 · Datei-Treiber meldet seriell.** Der Datei-Treiber meldet die nächste Datei erst, wenn die vorige im Log steht (Rückstau, docs/11 §11.8).
In S1 ist genau das die Decke: je Sekunde werden halb so viele Paare komplett, wie Dateien gemeldet werden (Phase-2- und Phase-3-Code gleich);
die Leseseite hält in Echtzeit mit. Das Ziel „S1 ≥ 3 × Baseline“ (Plan E5) ist deshalb mit dem Konsum nicht zu erreichen. *Optionen (Tobi):* in
Bündeln melden (mehrere `StartStream` in einer Transaktion, Rückstau je Bündel; „genau einmal je Kennung“ bleibt) oder das S1-Ziel neu fassen.


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

**PF-33 · Slots der Funktionen** (Messung Phase 4, docs/12 §12.5). Ein Funktions-Actor startet je Partition höchstens so viele Eingänge, wie die
Umgebung Slots nennt (Standard 1), und jeder Eingang wartet auf seinen Emit (eine Frage an das Ziel-Aggregat). Ein Ablauf über mehrere Aggregate
hat mehrere Kanten, also mehrere Eingänge je Vorgang: mit einem Slot tragen acht Partitionen in der Prüfdomäne ≈ 300 Vorgänge/s, der Dirigent
vorher bis 2 326. Mit 16 Slots S5-8 1,18 × der Basis, S5-64 0,56 ×. *Entscheidung offen:* höherer Standard für gleichbleibende Funktionen (sie
halten nichts fest, Slots kosten nur gleichzeitige Emits) oder Slots je Umgebung setzen; danach den Rest bei 64 Sendern untersuchen (Kandidat: je
Stream nacheinander auf den 50 heißen Posten-Streams, auf denen `Nachschub` und `Auftragsabwicklung` hören).

**FB-4 … FB-7 · Funktionen und Uhr, bewusst offen nach Plan Phase 4** (2026-10-26). Was erst spätere Phasen tragen, ist mit Absicht nicht
vorgezogen, weil jede Vorab-Lösung ein zweiter Weg wäre, der wieder verschwände. (FB-1, Hinweise als Eingang, ist mit Phase 4b erledigt, §13.6.) **FB-4:** reine Darstellungs-Lücken
der Werkzeuge; die Garantien hängen nicht daran. **FB-5:** R6 des Plans wollte Kreise im Signatur-Graph als Warnung; aber jeder Ablauf mit
Vorgangs-Aggregat ist ein Kreis (Auftrag → `Auftragsabwicklung` → Posten → `Auftragsabwicklung` → Auftrag), und der Build soll nicht vor
Design-Entscheidungen warnen. Mit dem Dirigenten sind GR-ZYKLUS (CQRS088) und GR-GARANTIE (CQRS082) entfallen; eine Erkennung „Kreis ohne
fortschreitenden Zustand“ bräuchte mehr als Signaturen. **FB-6:** die Quell-Nachricht vom Client hatte über gRPC das Trigger-Protokoll; es ist mit
der Handle-Welt entfernt, der neue Weg kommt mit der Leitung aus Phase 7. **FB-7:** eine getragene Funktion ohne verbundenen Client arbeitet nichts
ab; der Rückstand steht in `/health`, eine eigene Meldung fehlt. Nimmt ein Träger Eingänge an und antwortet nicht, wartet die Partition (bei einem Slot)
bis zum Ablauf der Lease; gesehen im Verhaltens-Netz, dort trägt die Umgebung deshalb acht Slots. In der Messung hält dieselbe Lage die
Position der Funktion `IBildpaarBeurteilung` fest (kein Klassifizierer gestartet): L1 wird nie still.

**G-15, FB-8, FB-9, PF-34, PF-35 · Funktionen als Actors** (Plan Phase 4b, 2026-10-28). **G-15:** `Bote.Melde` stellt jede Meldung als eigene
Anfrage zu; zwei Hinweise desselben Absenders an dieselbe Adresse können sich überholen — der Plan versprach die Reihenfolge zwischen zwei Actors.
Fachlich hängt nichts daran (Hinweise sind höchstens einmal, was zählt, geht als Command ins Aggregat), aber die Zusage stimmt nicht; ein Weg wäre
ein Absender-Actor je Ziel oder `Send` an eine zwischengespeicherte PID. Einen Test, der die Überholung sicher herbeiführt, gibt es nicht. **FB-8:**
die Beispieldomäne hat keinen Hinweis, den eine Funktion draußen fachlich hören müsste; die Prüfdomäne hat keine Leitung. Kandidat ist das Beispiel
aus Plan §2.9 (Vorverarbeitung → Merkmale → Klassifikator mit Zustand) — es änderte den Bildeingang und S1, daher nicht ungefragt. **FB-9:** der
Verweis im Zustands-Dokument zeigt in die Ablage, mit der gesichert wurde; die Prüfdomäne zeigt es (der Absturz-Test sichert deshalb in einem eigenen
Schema und setzt dessen Zustand zu Beginn zurück). **PF-34:** Folge der Garantie „ein Schreiber, gesichert im Ruhepunkt mit der Position“ (Risiko 9
des Plans). **PF-35:** Folge von A8 — eine Bindung nur für Funktionen mit Zustand bräuchte einen Code-Fakt „Zustand draußen“ an der Schnittstelle.

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

**AK-1 · Akteure und Clients** (`konzept-akteure.md` §9): `als` im Envelope bei mehreren verkörperten Akteuren, Funktionen über C#-Clients tragen
(CQRS094), Anmeldung je Person; kein Boot-Check, ob `Clients:<Name>:Token` die Akteure des Vertrags verkörpert. (Die durable Zustellung an Clients
ist mit Phase 4 erledigt: was ein Client rechnet, ist eine getragene Funktion mit Lease und festgehaltener Antwort.)

**PF-31 · Volles Falten nach Schema-Wechsel, neue Funktion ab 0.** Passt die Schema-Version eines Snapshots nicht, faltet die erste Aktivierung den
ganzen Stream (bei einem heißen Aggregat Millionen Events, Minuten); eine neu hinzugefügte Funktion liest das Log ab 0 und wirkt auf jedes alte
Event (die Inbox verwirft nur, was innerhalb ihres Horizonts schon wirkte). *Entscheidung offen:* hinnehmen und beim Start warnen, bzw. fachlich
festlegen, ob eine neue Funktion die Geschichte nachholt oder am Kopf beginnt.

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

- **PF-32 · Wachstum ohne Laufzeit-Risiko** (Analyse 2026-10-18, docs/12 §12.5): Guards `ProjectionCheckpoint` je (Projektion, Stream) und
  Festhalte-Streams je (Funktion, Eingang) werden nie gelöscht (Zugriff über den Schlüssel; das Zurücksetzen löscht ohne Index auf
  `ProjectionId`); die Dead-Letter-Tabelle wird nie geleert und je `/health` gezählt, ohne Index sortiert; der Einblick (Sim) durchsucht je
  Vorgangsliste das ganze Log; die Beispieldomäne blättert mit `OFFSET` und lädt für Statistiken ganze Tabellen; Kopfzeilen kosten ≈ 140 Byte
  je Event, die Daten ≈ 70; Postgres auf dem Messserver läuft mit `shared_buffers = 160 MB`.

- **P3-1 · Mojibake** in `Domain.SourceGeneration/AggregateHandlerGenerator.cs`, `Infrastructure/Projections/ReadModelDepsReader.cs`,
  `Infrastructure/Serialization/ProtoMapper.cs`.
- **P3-2 · Toter Code und Kleinkram:** `CqrsFrameworkOptions`, `IViewModel` + `ViewModelGenerator`,
  auskommentierte Reflection-Scans (`EventTypeResolver`, `MartenEventTypeRegistration`), nicht gemappte
  Proto-Endpunkte in Host.Blazor (das Python-SDK ruft sie trotzdem), `BenchPing` ohne Treiber-Basis; Dateiname ≠ Klassenname bei einigen
  Generatoren; veraltete Kommentare und Meldungen (z. B. „drei Header“ in `Spur.cs`, die Fehlermeldung in
  `AggregateDispatcherExtensions.ResolveAggregateType` spricht noch von der Namespace-Zuordnung; `ConnectionModule.cs` nennt die CommandId „deterministisch“, sie ist aber `Guid.NewGuid()` und nur über
  Wiederholungen gleich; `CqrsServiceExtension.cs` nennt noch `Cluster__Role`/`migrator|member` statt `Cluster__Rolle`/Allein|Migrator|Mitglied; `Cqrs.Codegen/Program.cs` nennt sich „reflexionsfrei“, liest aber per
  `MetadataLoadContext`; `deploy-multinode/Dockerfile` und `docker-compose.yml` erwähnen noch den entfernten LoadHarness).

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

- **FB-20 · Python-DTOs im Codegen-Prepass (2026-10-29; docs/09 §9.2):** die betterproto-DTOs des Workers (`generated/__init__.py`) erzeugte nur
  `build_python.sh` (in der Werkbank Schritt 2 von ⟳); eine neue Domänen-Nachricht ließ den Worker beim Import scheitern, bis jemand das Skript
  startete, und weder `codegen.sh --check` noch das Gate merkten es (Phase 9: `AblaufE2ETests.Bildeingang_mit_Python_Worker…` rot). Jetzt erzeugt
  sie der Prepass gleich nach `domain.proto` (`Cqrs.Codegen/PythonDtoErzeuger`): passt der sha256 des Protos zu `generated/.proto_hash`, ruft er
  kein Python (ein frischer Checkout baut ohne Interpreter), sonst `grpc_tools.protoc` mit betterproto über `CQRS_PYTHON`, sonst
  `/opt/miniconda3/envs/gpu_env/bin/python` (die Werkbank gibt den Interpreter ihrer Umgebung mit); fehlt er, scheitert der Build laut. Auch ein
  übersprungener Lauf (DLLs unverändert) prüft den Hash. `__init__.py` und `.proto_hash` stehen in der Artefakt-Liste — `codegen.sh --check` und
  das Gate (Build verändert den Checkout) sehen veraltete DTOs. `build_python.sh` ist entfallen (kein zweiter Weg); ⟳ legt in Schritt 2 nur noch
  die Python-Implementierungen an. *Gegenprobe:* ein Domänen-Feld ergänzt, `domain.proto` neu, DTOs alt committet → `./codegen.sh --check` auf
  dem Server rot.

- **G-17 und G-11 · Leser rückten über Events, die noch niemand gezogen hatte (2026-10-28, Messserver; docs/03 §3.3, docs/11 §11.4):** die Probe
  des Horizonts der Vollständigkeit las den Sequenzzähler mit `select last_value from mt_events_sequence` — ohne Sperre der Sequenz-Seite. Jeder
  32. `nextval` schreibt einen WAL-Eintrag und setzt dafür in der Seite kurz den vorausgeloggten Wert (bis zu 32 über dem zuletzt vergebenen);
  eine Probe, die ihn sah, gab Sequenzen frei, die noch niemand gezogen hatte. Die Transaktionen, die sie danach zogen, bekamen höhere Nummern,
  als die Probe abwartete, ihre Events wurden erst nach der Freigabe sichtbar, und die Leser (Funktionen wie Projektionen) rückten über sie.
  Bei Funktionen fehlten so Wirkungen (im Störungslauf 50–56 Nachschübe, 8–40 Zubuchungen der Verteilung, Vorgänge offen; „Eingang erledigt,
  Command ohne Quittung“), bei Projektionen fehlten Events im Read-Model (G-11: Guard dahinter, Partition am Kopf). Der harte Neustart war nur
  ein Lastfall: im Test fehlten Wirkungen auch vor dem Abbruch. Jetzt liest die Probe `pg_sequence_last_value` unter der Sperre
  (`EventStoreAbfragen.Zaehlerstand`). *Belege:* im protokollierten Lauf lasen genau zwei Proben einen Stand 31 über der höchsten vergebenen
  Sequenz, je 31 Zeilen darunter trugen spätere Transaktionsnummern — genau die zwei Verlust-Schübe des Laufs; die Funktions-Actors hatten die
  fehlenden Eingänge nie begonnen (Debug-Spur), ihre Seite meldete den Horizont über den damals unsichtbaren Events. *Nachweise:* I
  `ZaehlerstandPostgresTests` (acht ziehende Verbindungen, ein Leser vergleicht Stand und nächste Sequenz; ≈ 60 000 Proben), VT `NeustartTests`
  (Prüfdomäne im Consul-Cluster, acht Sender über den JSON-Eingang, SIGKILL und Neustart unter Last; je Reservierung unter dem Mindestbestand
  genau ein Nachschub, je Verteilung jedes Ziel genau einmal und ein Abschluss; Kategorie Last). Mit der Behebung dreimal grün, zwei
  Störungsläufe gültig (`…_server-pubsub-g17-stoerung-1/2`); Gegenprobe (Zähler wieder ohne Sperre) beide Tests rot. Ein erster Umbau, der die
  Sequenz im Trigger der Event-Zeilen nach der Transaktionsnummer neu zog, ließ `NeustartTests` dreimal rot und ist zurückgenommen — Martens
  Reihenfolge (Stream-Zeile vor den Events, `AnhaengeReihenfolgePostgresTests`) war nie das Problem.
- **Phase 7 Pub/Sub-Neustrukturierung (2026-10-28, Messserver; docs/03 §3.5, docs/04 §4.1/§4.4, docs/06 §6.2, docs/08 §8.3/§8.4, docs/11
  §11.3/§11.5/§11.9):** Clients. Die Sitzung ist ein Actor (`SitzungsActor`, einziger Schreiber am gRPC-Stream; die Lese-Schleife reicht nur
  weiter; was wartet, läuft neben der Mailbox; die Antworten eines Trägers der Reihe nach), ihre Id nennt den Knoten. Je Knoten ein Verteiler
  (`VerteilerActor`, fester Name, Typ → Sitzungen, `Watch`, Rückstau mit Grenze); ein Commit geht als eine Nachricht an den Verteiler jedes Knotens,
  nur mit hörbaren Typen, gezielte Antworten an den Knoten der Sitzung. Broker, Abos, `SubscriptionTracker`, der alte Handshake-Weg (`event_types`,
  `CapabilitiesHandler`) und Subscribe/Unsubscribe der Leitung sind entfernt. **G-12** (Ablehnung erreicht den Client nicht) und **G-13**
  (Sitzungs-Kennungen kollidieren zwischen Knoten) sind damit geschlossen. Read-your-writes „mindestens S auf v“: der Query-Dienst wartet begrenzt,
  bis der Stand seiner Projektion reicht, sonst `noch_nicht`; keine Nachfrage-Schleife im Client (**G-14** geschlossen). Der Stand je Id liest nach
  dem Guard das Log — nicht gehörte Events danach bremsen nicht, die Version einer Antwort taugt für den nächsten Command (**FB-10** erledigt).
  „P hat S weiterverarbeitet“ nach jedem Commit einer Projektion; die Sitzung hält je Antwort Projektion und gezeigte Streams und nennt dem Client
  nach höchstens 200 ms die Queries (`Aktualisiert`), der C#-Client fragt neu (transitive Aktualisierung). Lesen ab Position über die Leitung
  (`LiesLog`, nur was die Sitzung hören darf, nur bis zum Horizont). Ausgangskorb im C#-Client (`IAusgangskorb`, `DateiAusgangskorb`). Mit dem
  Broker fiel eine unausgesprochene Start-Sperre: seine Aktivierung beim Start wiederholte, bis jede Platzierung antwortete; ohne sie arbeitete ein
  nach hartem Ende neu gestarteter Host Minuten gegen Aktivierungen auf dem toten Knoten (`NeustartTests` dreimal rot, mit dem Code von Phase 6
  grün) — jetzt ausdrücklich: `Bereitschaft` im `ClusterStartupService`. Grenzen: FB-6, FB-14, FB-15, PF-37.
- **Phase 8 Pub/Sub-Neustrukturierung (2026-10-28, Messserver; docs/04 §4.1–§4.3, docs/05, docs/11 §11.5/§11.10):** Projektion `IProjektion`
  mit `Auf(Event[, Herkunft], Fähigkeiten)`, Reader `IReader` mit `Frage(Query, Lese-Fähigkeiten)`, ohne Writer, Track, Envelope und Kennungs-String;
  eine Lesart (`Projektionsform`) für Batch-Kinds, Weckplan, Query-Dienst, Akteur-Rechte, Analyzer und Extractor — **FB-12** damit erledigt. Store-Klassen
  1/2/3 mit Fortschritt beim Framework für 2 und 3, Neuaufbau über Generationen, allgemeiner Store-Test für drei Treiber, R14 als CQRS109/110 und
  Start-Prüfung, Read-Models und Fähigkeiten auf `Id<T>` (CQRS111). Grenzen: FB-16, FB-17, FB-18. Dabei **PF-38**: der Stand je gezeigter Id wird
  bei jeder Query gelesen (auch „nicht gefunden“) und kostete zwei Abfragen (S1 90,5 → 122 Queries je Vorgang); jetzt eine, wo der Fortschritt im
  Postgres des Logs liegt (`IFortschrittImLog`) — S1 A-B 155 → 152 Paare/s, 91,8 → 104 Queries je Vorgang (docs/12).
- **Phase 6 Pub/Sub-Neustrukturierung (2026-10-28, Messserver; docs/03 §3.5, docs/04 §4.1/§4.4, docs/06, docs/11 §11.5/§11.10):** Wecker statt
  Broker-Signalen — der `WeckplanGenerator` erzeugt aus den Signaturen je Event-Typ die Ziele (Batch-Leser der Projektionen, Funktions-Actors je
  Partition bzw. die eine Adresse, die Uhr bei einer Frist; CQRS108), der Wecker weckt nach dem Commit des Group-Commits und des Einzel-Appends je
  Commit und Ziel einmal, der Takt je Partition prüft beim Start den Plan (GR-WECKPLAN). Signale samt Typen, Generatoren, Wire, Proto und Empfang
  sind entfernt, ebenso der Redis-Versionsindex: der Stand je gezeigter Id kommt aus dem Guard der Projektion (`ProjektionsStaende`), **PF-6** ist
  damit erledigt (kein Redis-Write je Event, kein Redis-Aufruf im Command-Turn, Redis entfällt im Betrieb). Grenzen: FB-11, FB-12, PF-36; G-17 kam im Störungslauf ans Licht (schon mit dem Code von Phase 5; erledigt, oben).
- **Phase 5 Pub/Sub-Neustrukturierung (2026-10-28, Messserver; docs/03 §3.2/§3.3, docs/04 §4.6, docs/11 §11.2/§11.3):** keine Marken im Log —
  `KommandoVerarbeitet`/`KommandoAbgelehnt` entfallen. Ein Noop und eine Ablehnung schreiben nur ihre Quittung in die Inbox-Tabelle; die Version
  bleibt, ein Aggregat hat eine Version (die Zahl seiner Events), eine Ablehnung legt keinen Stream mehr an. Weil kein Append mehr die Version
  einer solchen Entscheidung prüft, prüft die Quittung ohne Events den Kopf ihres Streams in derselben Transaktion (`cqrs_kopf`, sperrt die
  Kopfzeile): eine zweite Aktivierung auf veraltetem Stand kann nicht dauerhaft ablehnen. Beim Client eine Version je Aggregat, auch aus Antworten
  (`VersioningModule`) — ein Aggregat, das er nur aus einer Antwort kennt, wird mit OCC geschrieben statt ungeprüft. Grenze: FB-10. Nebenbei der
  Extractor: `Frist` im Decide-Ausgang über den Namen statt über die Symbol-Identität (flackernder `--check`, docs/12 §12.2).
- **Phase 4b Pub/Sub-Neustrukturierung (2026-10-28, Messserver; docs/04 §4.4, docs/11 §11.6/§11.10):** Funktionen sind Actors mit gekapseltem,
  durablem Zustand (`IZustand<T>`, gesichert mit der Position, eine Adresse als Code-Fakt), Ressourcen je Aktivierung (Umsetzung und Dienste im
  DI-Bereich der Aktivierung, geschlossen beim Stoppen) und einem hart verdrahteten Hinweis-Netz ohne Log und Broker (Routing-Tabelle aus dem
  Hinweisplan, `Bote.Melde` an die Adresse, Kennung vom Absender, Commands mit Ids aus der Kennung). Damit erledigt:
  - **FB-1 Hinweis als Eingang einer Funktion:** zugestellt über das Hinweis-Netz — von Funktionen, Aggregaten (Ablehnung) und Clients
    (`TransientEvent`), im Host und draußen (`ArbeitsAuftrag.hinweis`). Nachweis P `HinweisNetzTests`; PD `FunktionsZustandGarantieTests`
    (Hinweis-Kette bis zum Command, Commands aus einem doppelten Hinweis wirken einmal, eine Funktion draußen hört einen Hinweis).
  - Die Lücke „gleichbleibende Funktion mit Feldern“ (ohne Nummer, Plan R19): CQRS105 verbietet veränderliche Felder, Setter und Sammlungen hinter
    `readonly` in Funktions-Klassen. Nachweis P `ZustandAnalyzerTests`.

- **Phase 4 Pub/Sub-Neustrukturierung (2026-10-26, Messserver; docs/04, docs/11 §11.6/§11.7):** die Domänen sind auf Aggregat + Funktion + Frist
  umgeschrieben, das Alte ist zurückgebaut — Dirigent (`ProzessManager`, Prozess- und Fluss-DSL, Offen-Index, Marking, Backstop, Heil-Plan),
  Reaktion als eigene Art (Emittenten-Adapter, Poll der Emittenten), Handle-/Trigger-Welt (`IPipelineHandler`, `IPipelineTrigger`, Timer, Webhook,
  `[Ingress]`, `FristPlanung<TCmd>`, Fristplaner), Katalog-Form der Funktion (`RufeAsync`, `IAuftrag`), Akteur-Vertrag mit `Auf` und
  `IAkteurDienst`. Damit erledigt:
  - **W11-1 Doppelte Wege** und **W11-2 Zeitlimits:** es gibt je Absicht einen Weg; Zeit ist die Frist aus dem Decide (Uhr, `cqrs_fristen`).
  - **PF-4b, PF-25, PF-26, P2-5:** Backstop, Heil-Plan, Warte-Knoten, Marking und `KlärungNötig` gibt es nicht mehr; ein Ablauf ist ein Aggregat,
    das seinen Zustand selbst trägt.
  - **P3-3 Saga-DSL ohne `Rufe`:** `Cqrs.Testing` prüft Aggregat und Funktion (Ablauf-Szenarien), die Saga-DSL ist entfernt.
  - **FB-2 Laufort Extern für die Baustein-Form:** über die bestehende Sitzung und den Vermittler mit Lease (`BausteinSitzung`, Strom in Stücken,
    Fortsetzung ab der festgehaltenen Stelle); ein Client trägt die Funktion (`ITraegt<F>`). Nachweis I `AblaufE2ETests.Bildeingang_mit_Python_Worker…`,
    VT `ClientVerhaltenTests.Die_Antwort_eines_Traegers_wirkt_genau_einmal…`, E2E S2. Die Quell-Nachricht vom Client über gRPC ist als FB-6 offen.
  - **FB-3 Signal aus Quellen und Funktionen:** jeder Schreiber meldet nach dem Commit dasselbe (`Commitmeldung`: Envelope und Signal je Event).
    Nachweis PD `QuellenE2ETests.Eine_Quell_Nachricht_vom_Client…` (dreimal je unter 5 s bei 30 s Takt; ohne die Meldung rot).
  - **VM-1 Zählung im Treiber:** Soll und Ist, die beide aus dem Log kommen, stehen in einer Abfrage (ein Schnappschuss); der Treiber prüft S5 am
    Log (Auftrag, Kompensation über den Beleg, Verteilung, Nachschub) statt an `ProzessBeendet`.

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
