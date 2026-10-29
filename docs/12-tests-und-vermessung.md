# 12 — Tests & Vermessung

**Die eine Quelle für Zahlen und Messwerte.** Andere Dokumente verweisen hierher. Offene Testlücken stehen als Befund in
`docs/13-reifegrad-schulden-bewertung.md` (T-1, P2-5). Stand: 2026-10-15.

## 12.1 Die Test-Ebenen

Die Teststrategie folgt den Invarianten „die Wahrheit ist der Log“ und „nie faken, was man nicht besitzt“.

| Ebene | Projekt | Läuft gegen | Testet | Parallel? |
|---|---|---|---|---|
| 1 — Prüfstand | `Infrastructure.Pruefstand.Tests` | in-memory, **store-frei** | Decider/Applier, Fold, Routing, Weckplan und Wecker, Wire, Analyzer und Generatoren (über eine eigene `CSharpCompilation`), Extractor/Gesten/Sicht, Funktionen, Akteure, Quellen-Treiber, Umgebung, Spur | ja |
| 2 — Integration | `Infrastructure.Integration.Tests` | **echtes** Marten/Postgres, Consul, Kestrel | Store-Semantik und Ende-zu-Ende-Verhalten im Cluster (Beispielanwendung) | **nein** (sequentiell erzwungen) |
| 2 — Prüfdomäne | `Pruefdomaene.Integration.Tests` | **echtes** Marten/Postgres, Consul; eigene Schemata `pruef_*` | Framework-Verhalten domänenneutral an der zweiten Anwendung `Pruefdomaene`: Prozess-Diamant, Kompensation, Fan-out + Count-Join, Snapshot, Upcasting, Marking-Cursor-Äquivalenz | **nein** (sequentiell erzwungen; nie gleichzeitig mit der anderen Integrations-Suite) |
| Verhaltens-Netz | `Verhalten.Tests` | **echte** Hosts als Prozesse (`Host.Grpc`, `Host.Pruefdomaene`) in einer eigenen Umgebung je Klasse (Art Sim, eigene DB, eigenes Quell-Verzeichnis, bei Bedarf zwei Knoten über Consul) — angesprochen nur von außen: gRPC-Leitung mit dem Client-Stack des Blazor-Arbeitsplatzes bzw. roh, JSON-Eingang, Quell-Verzeichnis, Queries | das Verhalten, das der Umbau nicht ändern darf: was ein Client sieht, darf, quittiert bekommt, liest; Exactly-once je Ablauf als fachliches Soll im Log — nie Prozess-Events, Marken oder Framework-Verdrahtung | **nein** (sequentiell; nie gleichzeitig mit den anderen Integrations-Suiten oder einer Messung) |
| Client (C#) | `Client.Infrastructure.CollectionTests` | in-memory | `VirtualCollection`, `PendingCommandTracker` | ja |
| Client (Python) | `Client.Infrastructure.Python/tests`, `Domain.Client.Worker.Python.ML/tests` | pytest | SDK (Vertrag, Funktion, Query, Reconnect, Mapper), Worker (Verträge, Funktionen, generischer Worker) | ja |
| Werkbank-Abnahme | `Werkbank.Abnahme.Tests` | **echte** Werkbank (Prozess) auf einer Kopie des Arbeitsbaums, echter Host in `sim`, eigene Sim-DB, Python-Worker | ⟳ (alle Schritte, Stände), JSON-Eingang als Akteur, Projektion, Spur, „▶ Worker“ bis zum Fakt, Geste/↶ byte-gleich, Daten-Prüfung meldet ein entferntes Feld | **nein** (sequentiell; nie gleichzeitig mit den anderen Integrations-Suiten) |
| Werkbank (Oberfläche) | `Werkbank` + Browser | echter Host gegen die Sim-DB | Sichtprüfung im Browser (Board, Panels, Spur-Überlagerung) — die Abläufe prüft die Abnahme oben | n/a |
| Werkzeug-Gates | `GraphExtractor -- --check`, `-- --sonde` | Solution bzw. fremde Domäne im Speicher | Code ⇄ Karten, Fixpunkt, Wirker, Routing; Agnostik | n/a |
| Gate frischer Checkout | `./pruefe-checkout.sh` | der Commit `HEAD` in einem temporären `git worktree` | ignorierte Quellen, Build, Codegen-Drift, Prüfstand, Build verändert den Checkout nicht | n/a |
| Garantie-Suite | `--filter Kategorie=Garantie` auf Prüfstand, Integration, Prüfdomäne | wie die jeweilige Ebene | jede Garantie aus [11](11-garantien.md) unter Störung; offene Lücken als Skip-Tests (`G-n offen (docs/13)`) | je Ebene wie oben |
| E2E-Vermessung | `scripts/vermessung-e2e.sh` (Treiber `Cqrs.Vermessung`) | echte Hosts (`Host.Grpc`, `Host.Pruefdomaene`, Release) in den Umgebungen `vermessung` bzw. `vermessung-pruefdomaene`, eigene DBs, Python-Worker | Höchstlast mit 1/8/64 Sendern: Durchsatz, Rückstand, Latenz, Last je Vorgang; Exactly-once nach jedem Lauf; Störungslauf (§12.5) | **nein** (nie gleichzeitig mit einer Integrations-Suite oder der Werkbank-Abnahme) |

**Prinzip:** Ebene 1 testet nur, was ohne Store deterministisch entscheidbar ist. Store-Semantik (Co-Commit-Atomarität, `seq_id`-Lücken, geteilte
Dokumente, Metadaten) wird **ausschließlich** gegen echtes Marten geprüft. Es gibt bewusst **keinen `InMemoryEventStore`**; In-Memory-Doubles gibt
es nur für nicht-autoritative Randstores (Inv. 6).

## 12.2 Zahlen

**Aktuell (2026-10-15, Phase 3 des Plans Garantien und Durchsatz):** Prüfstand **582 grün + 1 übersprungen** (Lücke G-5),
Integration **49 + 0** sequentiell (≈ 105 s), Prüfdomäne **23 + 1** (G-2) sequentiell (≈ 100 s); Garantie-Suite: Prüfstand 159 + 1,
Integration 31 + 0, Prüfdomäne 18 + 1;
Werkbank-Abnahme **1/1** sequentiell (≈ 110 s), `Client.Infrastructure.CollectionTests` **30/30**, Python SDK **34/34**, Worker **21/21**;
`--check` grün (alle Bereiche inkl. „Lage“), `--sonde` grün (111 Soll-Fakten, Lage-Soll 56 Typen); Gate `./pruefe-checkout.sh` grün.
**Stand 2026-10-26 (Umbau-Phase 0):** Verhaltens-Netz **13 grün + 3 übersprungen** (G-12, G-13, G-14; ohne Skip rot
gesehen) sequentiell (≈ 2 min); Garantie-Suite Prüfstand 179 + 1, Integration 50 + 0 (ein Lauf davor rot an
`SequenceGapPostgresTests.Eine_lange_offene_Transaktion…` mit einem Marten-Befehlsfehler, einzeln und im Wiederholungslauf grün), Prüfdomäne 46 + 0;
`--sonde` grün mit den drei fremden Szenarien (226 Soll-Fakten, Lage-Soll 80 Typen); Client 30/30.
**Stand 2026-10-26 (Umbau-Phase 1 — typisierte Ids, `3011d8a`):** Prüfstand **620 grün + 1 übersprungen** (G-5),
davon Garantie 184 + 1; Integration **69/69** (≈ 6 min, davon 50 Garantie); Prüfdomäne **52/52** (davon 47 Garantie, neu
`SchreibGarantieTests.Ein_Command_an_eine_fremde_Identitaet…`); Verhaltens-Netz **13 grün + 3 übersprungen** (G-12, G-13, G-14, unverändert);
Client 30/30, Python SDK 34/34, Worker 21/21; `codegen --check`, `--check`, `--sonde` und `./pruefe-checkout.sh` grün. Neu im Prüfstand:
`Ids/TypisierteIdTests` (Ziel im Typ, Log/Wire/Leitung als UUID, abweichende Hülle abgelehnt), CQRS013 in `ModellAnalyzerTests`, Routing aus dem
Typ in `BuildGarantieTests`.
**Stand 2026-10-26 (Umbau-Phase 2 — Actor-Lebenszyklus und zwei Verben, `1cc53e9`):** Prüfstand **638 grün + 1
übersprungen** (G-5), davon Garantie 202 + 1; neu 18 Tests in `Lebenszyklus/` (`RahmenActorTests`, `BoteTests`, `LebenszyklusImClusterTests`),
Gegenprobe je Mechanismus per `sed` im Server-Lauf rot gesehen (Freigabe ohne Warten, kein Zusammenfassen, keine Fehlerantwort, Frage ohne Frist,
Passivierung trotz Arbeit). Integration **69/69** (50 Garantie), Prüfdomäne **52/52** (47 Garantie), Verhaltens-Netz **13 grün + 3 übersprungen**
(G-12, G-13, G-14, Tests unverändert), Client 30/30, Python SDK 34/34, Worker 21/21, Werkbank-Abnahme 1/1; `codegen --check`, `--check`, `--sonde`
und `./pruefe-checkout.sh` grün. Keine Messung (der heiße Pfad ist bis auf gebundene Fristen unverändert).
**Stand 2026-10-26 (Umbau-Phase 3 — Funktion als Baustein, Uhr, Quellen vom Client, `1a793bb`):** Prüfstand
**686 grün + 1 übersprungen** (G-5), davon Garantie 231 + 1; neu 48 Tests (Funktion als Baustein und Uhr 40, `WiederholungAnalyzerTests` 4,
Takt in `QuellenTypAnalyzerTests` 2, Lage der Host-Funktion 1, `RahmenActorTests` 1). Integration **69/69** (50 Garantie, ≈ 6 min), Prüfdomäne
**66/66** (61 Garantie; neu `FunktionsBausteinE2ETests` 5, `UhrE2ETests` 3, `QuellenE2ETests` 2, `FunktionsGarantieTests` 4), Verhaltens-Netz
**13 grün + 3 übersprungen** (G-12, G-13, G-14, Tests unverändert), Client 30/30, Python SDK 34/34, Worker 21/21, Werkbank-Abnahme 1/1;
`codegen --check`, `--check` (0 Befunde), `--sonde` (226 Soll-Fakten, 79 Lage-Typen), `--grammatik` und `./pruefe-checkout.sh` grün.
Gegenprobe je neuem Mechanismus per `sed` im Server-Lauf rot gesehen: Festhalten abgeschaltet (zwei Aktivierungen), Command-Id gewürfelt statt
abgeleitet (Abbruch vor der Position), Konflikt beim Festhalten als gewonnen gewertet (zwei Worker), Frist-Id gewürfelt (doppelt gefeuert),
Position über laufende Eingänge, Eingänge eines Streams parallel, Quelle je Schlüssel ohne Kennungs-Verzeichnis, Fortschritt eines Stroms
vergessen, `Neben` ohne Schutz vor synchronem Wurf. Keine Messung: der heiße Pfad der Aggregate ist bis auf die Prüfung auf Fristen im
Append unverändert; die Uhr fügt eine Abfrage je Sekunde Grundlast hinzu (docs/13 PF-15).
**Stand 2026-10-26 (Umbau-Phase 4 — Domänen umgeschrieben, Altes zurückgebaut, `a2b812b`):** Prüfstand **471 grün + 1
übersprungen** (G-5), davon Garantie 187 + 1 — gegenüber Phase 3 sind rund 215 Tests der Alt-Welt entfallen (Dirigent, Marking, Fluss-DSL,
Katalog-Funktion, Handle-/Trigger-Welt, Emittenten, Akteur-Vertrag und -Dienst, Werkzeug-Tests zu Fluss und Saga). Integration **61/61** (44
Garantie), Prüfdomäne **45/45** (40 Garantie; neu `AblaufE2ETests` 6, entfallen `ProzessE2ETests`, `MarkingCursorAequivalenzTests`), Verhaltens-Netz
**13 grün + 3 übersprungen** (G-12, G-13, G-14; geändert ist nur die Umgebung: `IBildpaarBeurteilung` mit acht Slots, docs/13 FB-7), Client
30/30, Python SDK 33/33, Worker 21/21, Werkbank-Abnahme 0 + 1 übersprungen (Editor, docs/10 E4); `codegen --check`, `--check` (0 Befunde), `--sonde`
(229 Soll-Fakten, 70 Lage-Typen), `--grammatik` und `./pruefe-checkout.sh` grün. Gegenprobe je neuem oder geändertem Mechanismus per `sed` im
Server-Lauf rot gesehen: der Applier überspringt `IRahmenEvent` nicht mehr (Trainingslauf), CQRS057 für Projektionen aus, CQRS087 aus, die
Quelle meldet ihren Commit nicht (FB-3: dreimal je unter 5 s), die Leitung bietet getragene Funktionen nicht an (FB-2: Python-Worker), die Uhr
als Vergleich je Zeile (Fälligkeits-Abfrage liest das ganze Verzeichnis).

**Stand 2026-10-28 (Umbau-Phase 4b — Funktionen als Actors: Zustand, Ressourcen, Hinweis-Netz):** Prüfstand **500 grün + 1
übersprungen** (G-5), davon Garantie 209 + 1; neu u. a. `FunktionsAktivierungTests`, `HinweisNetzTests`, `ZustandAnalyzerTests`, Zustand und eine
Adresse in `FunktionsBausteinGeneratorTests`, A8 in `FunktionsVermittlungTests`, R20 in `GesamtbildAnalyzerTests`. Integration **61/61** (44 Garantie),
Prüfdomäne **53/53** (48 Garantie; neu `Garantien/FunktionsZustandGarantieTests` 8 am allgemeinen Fall Messung), Verhaltens-Netz **13 grün + 3
übersprungen** (G-12, G-13, G-14) in zwei von drei Läufen; im dritten scheiterte einmal `Nach_dem_eigenen_Command_sieht_die_naechste_Query…`
(Lesen nach Schreiben über den Versionsindex, docs/13 G-16 — nicht von Phase 4b berührt). Eine Teständerung: `Die_Antwort_eines_Traegers…` trennt den
Träger erst, wenn der Server seine Antworten gelesen hat (die rohe Leitung schließt hart; eine noch nicht gesendete Antwort ging verloren, seit
abgemeldete Träger ihre Adressen sofort freigeben und der erste Träger mehr alte Eingänge auf einmal bekommt — die Zusicherung ist unverändert).
Dazu im Vermittler: ein Träger, dessen Sitzung endet, meldet sich ab, und jede geparkte Anfrage wird bedient. Client 30/30, Python SDK 35/35
(neu: Antwort auf einen Hinweis als Ganzes), Worker 21/21; `codegen --check`, Leitung `--check`, `--check` (0 Befunde), `--sonde` (246 Soll-Fakten mit
dem neuen Szenario Wetterdienst, 76 Lage-Typen), `--grammatik` (mit GR-FUNKTION-ZUSTAND, GR-HINWEIS-NETZ) und `./pruefe-checkout.sh` grün.
Gegenprobe je neuem Mechanismus per `sed` im Server-Lauf rot gesehen (13): Nachrechnen auf dem gesicherten Zustand (Prüfstand und Prüfdomäne), Laden
beim Aktivieren (Umzug), Position nur zusammen mit dem Zustand (Absturz, Umzug), Zustand und Stand mit erwarteter Version (zwei Sicherungen),
Freigabe der Ressourcen beim Stoppen, Versand des Hinweis-Netzes (Prüfstand und Prüfdomäne), Command-Id aus der Hinweis-Kennung (Prüfstand und
Prüfdomäne), Ausgaben der Antwort draußen auf einen Hinweis, Bindung an einen Träger (A8), eine Adresse liest alle Partitionen. Keine Messung: der
heiße Pfad bestehender Funktionen ist unverändert (die Umsetzung entsteht je Aktivierung statt je Knoten, die Dienste je Aktivierung; je Eingang
kommt nichts hinzu), die Bindung je Adresse wirkt bei einem Slot je Partition nicht (docs/13 PF-35).

**Stand 2026-10-28 (Phase 4b Nachtrag E10 — Lebenszyklus einer Funktion, `3080b2c`):** Prüfstand **507 grün + 1 übersprungen** (G-5; neu
`LebenszyklusAnalyzerTests` 4, Lebenszyklus in `FunktionsAktivierungTests` 2, nie gleichbleibend in `FunktionsBausteinGeneratorTests` 1), Prüfdomäne
**54/54** (neu `Verliert_eine_Sicherung_beginnt_die_Aktivierung_neu…`; der Test „zwei Aktivierungen“ läuft danach mit einem Knoten weiter), Integration
Abläufe mit Python-Worker 5/5, Verhaltens-Netz **13 grün + 3 übersprungen**, Python SDK 36/36 (neu: `beim_start`/`beim_ende`), Worker 21/21;
`codegen --check`, `--check`, `--sonde` (247 Soll-Fakten), `--grammatik` (mit GR-FUNKTION-LEBENSZYKLUS), `./pruefe-checkout.sh` grün. Gegenproben rot
gesehen: Lebenszyklus nicht nach dem Laden gerufen (Umzug), `VorDemSichernAsync` nicht vor der Sicherung (Absturz, Umzug, Prüfstand), kein Neubeginn
nach verlorenem Konflikt (der neue Test; der Test mit zwei Knoten erzwingt den Konflikt nicht und blieb unter dieser Sabotage grün). Ein Lauf war rot an
einem Fehler im Actor — die Sicherung lief als `async`-Lambda an `Func<…, Task>` und verlor ihre neue Version; behoben.
**Stand 2026-10-28 (Umbau-Phase 5 — Marken aus dem Log, `ad66d61`):** Prüfstand **505 grün + 1 übersprungen** (G-5;
entfallen 5 Tests der Marken — `AblehnungsMarkeTests` 3, ein Rundlauf der Marke, ein Spur-Weg der Ablehnung —, neu `VersioningModuleTests` 3),
Integration **63/63** (neu `GarantiePostgresTests.Eine_Quittung_ohne_Events_gilt_nur_auf_dem_Kopf…` je Rich und Quick; `EingangE2ETests`: eine
Ablehnung von außen schreibt nichts und erscheint nicht im Einblick), Prüfdomäne **55/55** (neu `SchreibGarantieTests.Eine_Ablehnung_auf_einem_veralteten_Stand…`;
der Neustart-Test prüft dazu: keine Ablehnung legt einen Stream an, ein Client mit alter Version bekommt nach dem Neustart dieselbe Ablehnung),
Verhaltens-Netz **14 grün + 3 übersprungen** (neu `ClientVerhaltenTests.Kennt_der_Client_ein_Aggregat_nur_aus_einer_Antwort…`; G-12, G-13, G-14
unverändert), Client 30/30, Python SDK 36/36, Worker 21/21, Werkbank-Abnahme 0 + 1 (E4); `codegen --check`, `--check`, `--sonde` (247
Soll-Fakten), `--grammatik`, `./pruefe-checkout.sh` grün. Gegenproben rot gesehen: Kopf-Prüfung abgeschaltet (Store-Test in beiden Modi,
veraltete Ablehnung in der Prüfdomäne), Versionen aus Antworten ignoriert (`VersioningModuleTests` 2 von 3, Verhaltens-Test). Ein Lauf war an
`--check` und `--sonde` rot (CS0029 im Fixpunkt: die Frist fiel aus dem Decide-Ausgang) und im nächsten auf demselben Commit grün — der Extractor
verglich `Frist` über die Symbol-Identität, und MSBuild lädt Core und Infrastructure auf dem Server mit Fehlern (NuGet-Audit zu Marten); jetzt über den
Namen wie die übrigen Vertrags-Typen, danach zweimal hintereinander grün. Messung (eine Wiederholung, `2026-10-28_0358_ad66d61_server-pubsub-phase5`,
gültig, Exactly-once in allen Szenarien grün, neu geprüft „kein abgelehnter Command hat geschrieben“) gegen Phase 4: S1 162 gegen 159 Paare/s bis
alles sichtbar, S3-64 1392 gegen 1469 Teile/s, S5-64 306 gegen 302 Vorgänge/s, Transaktionen und Queries je Vorgang gleich — keine Änderung über das
Rauschen hinaus. Erwartet: die Szenarien entscheiden kaum Noops oder Hinweis-Ablehnungen (die Absage in S5 ist ein Fakt), der Weg mit Events ist unverändert.
**Stand 2026-10-28 (Umbau-Phase 6 — Wecker statt Broker, ohne Redis, `4f43172`):** Prüfstand **504 grün + 1
übersprungen** (G-5), davon Garantie 217 + 1 (entfallen die 10 Signal-Tests `SignalTypeGeneratorTests`, `SignalEmitTests`,
`SignalWireRoundTripTests`; neu `WeckplanGeneratorTests` 5, `WeckerTests` 4), Integration **63/63 (davon 47 Garantie)** (`SignalDeliveryClusterTests` entfällt; neu
`LiveCommandE2ETests.Ein_Commit_weckt_die_Projektion…`, der Stand je Id aus dem Fortschritt statt des Redis-Schlüssels), Prüfdomäne **59/59** (neu
`Garantien/WeckerGarantieTests` 4), Verhaltens-Netz **15 grün + 3 übersprungen** (G-12, G-13, G-14 übersprungen; in einem von drei vollen Läufen zwei rot: `ClientVerhaltenTests.Nach_dem_eigenen_Command…` (G-16) und `TrainingsWaechterTests` (FB-13), einzeln je dreimal grün; neu `ZweiKnotenTests.Nach_dem_harten_Ende_eines_Knotens…`, zweimal je ≈ 20 s grün;
die Client-Tests aus Phase 0 und 5 unverändert grün, auch die Version aus der Antwort über den Stand der Projektion), Client 30/30, Python SDK 36/36,
Worker 21/21; `codegen --check`, `--check`, `--sonde`, `--grammatik` (GR-WECKPLAN) und `./pruefe-checkout.sh` grün. Gegenproben rot gesehen: Wecker
ohne Weckruf (Latenz-Tests der Funktion, der Quelle und der Projektion, Zwei-Knoten-Test nach gut 3 min), Weckplan ohne Projektionen
(Generator-Test und Boot-Fehler beim Start), CQRS108 nie gemeldet, Start-Prüfung ohne Wirkung (`WeckerTests`), Takt ohne Weckruf
(`WeckerGarantieTests.Gehen_die_Weckrufe…` nach 53 s rot). Die Uhr-Latenz unterscheidet Weckruf und Takt nicht (ihr Takt ist 1 s).
Messung A-B auf Tobis Wort (A = Code von Phase 5 `6a132d2` aus einem Worktree, B = Phase 6; S1, S3, S5; beide gültig, Exactly-once grün):
S1 159 → 158 Paare/s, S3-64 1 454 → 1 668 Teile/s (1,15 ×), S3-8 1 309 → 1 342, S5-1 400 → 448 Vorgänge/s, aber S5-8 343 → 243 und
S5-64 329 → 224. Ein zweites Paar nur für S5 (B, dann A; Ordner `…_server-pubsub-phase6-s5-{b2,a2}`) ergibt A-B-B-A: S5-8 Mittel 0,71 ×,
S5-64 0,81 ×, S5-1 1,09 × — die Läufe streuen stark, die Senkung in S5-8 aber in beiden Paaren (PF-36, Ursache offen). Transaktionen und Queries je
Vorgang sind gleich. Der **Störungslauf ist ungültig** (`…_server-pubsub-phase6-stoerung`: 56 Nachschübe und 8 Zubuchungen fehlen, 4 Vorgänge
offen) — und ebenso mit dem Code von Phase 5 (`…_server-pubsub-phase6-ab-a-stoerung`: 50 und 40, 12 offen): Wirkungen von Funktionen gehen nach dem
harten Neustart verloren, nicht durch den Wecker (G-17, docs/13). Ein Lastlauf dauert auf dem Messserver ≈ 12 min statt der früher genannten 5.

**Stand 2026-10-28 (G-17 und G-11 behoben, `acb3620`):** die Probe des Horizonts liest den Sequenzzähler mit `pg_sequence_last_value` (docs/03
§3.3). Neu Integration `Garantien/ZaehlerstandPostgresTests` (Garantie) und Verhaltens-Netz `NeustartTests` (Garantie und Last: Prüfdomäne im
Consul-Cluster, acht Sender über den JSON-Eingang, 40 s Last, SIGKILL nach 12 s, Neustart nach 3 s, dann das fachliche Soll im Log; ≈ 2–4 min;
in `tests.sh schnell` wie die Last-Tests der Integration ausgenommen). Ohne die Behebung war `NeustartTests` in 13 von 15 Läufen rot (2–14
Nachschübe, bis 180 Zubuchungen fehlten, auch schon vor dem Abbruch), danach dreimal grün; Gegenprobe (Zähler wieder ohne Sperre): beide Tests rot,
`ZaehlerstandPostgresTests` mit ≈ 60 000 Proben in 8 s. Zwei Störungsläufe gültig, Exactly-once grün (`…_server-pubsub-g17-stoerung-1/2`: S5 113 194
und 108 342 Vorgänge, je 182 bzw. 181 Paare in S1).
**Stand 2026-10-28 (Umbau-Phase 7 — Clients, `403f058` … `70cda54`, Start-Sperre `d9b2676`):** volle Stufe auf
`70cda54`: Prüfstand **508 grün + 1 übersprungen** (neu `VerteilerTests` 5, `AusgangskorbTests` 2; entfallen `BrokerIdentityTests`,
`BrokerWiederholungTests`), Integration **64/64** (mit Last), Prüfdomäne **59/59**, Verhaltens-Netz **21 grün, ohne Skip** (G-12, G-13, G-14 grün;
neu `ClientVerhaltenTests.Eine_fachliche_Ablehnung_erreicht_die_Sitzung_an_der_rohen_Leitung`, `…Eine_Datensatz_Ansicht_zeigt_das_Label…`
(transitive Aktualisierung), `…Wer_wieder_verbindet_liest_ab_seiner_Position…`), Client 30/30, Python SDK und Worker grün, `codegen --check`,
`--check`, `--sonde`, `--grammatik` und Gate grün. Rot war nur `NeustartTests` (dreimal; mit dem Code von Phase 6 `09ae06d` grün in 173 s, auch
mit abgeschalteter Verteilung rot): mit dem Broker fiel seine Aktivierung beim Start, die unausgesprochen wartete, bis der Cluster nach dem harten
Ende eines Knotens wieder platzierte — jetzt die Start-Sperre `Bereitschaft`; danach `NeustartTests` grün in 133 s, `schnell` grün bis auf FB-13
(einmal, danach das Verhaltens-Netz 21/21). Gegenproben rot gesehen: Verteiler ohne gezielte Zustellung (Ablehnung im Client-Stack und an der rohen
Leitung, `CommandFailed` am Akteur-Tor, zwei Knoten), Read-your-writes ohne Warten (`Nach_dem_eigenen_Command…`), Beobachtung abgeschaltet
(transitive Aktualisierung), Seite des Logs ohne Einträge (Wiederverbinden), Ausgangskorb ohne Ablegen (`AusgangskorbTests`). Nicht gemessen
(Umbau-Phase 7 verlangt keine Messung; PF-37).

**Stand 2026-10-28 (Umbau-Phase 8 — Stores und Projektionsform, `86092d0` … `b01b789`):** volle Stufe auf `496e384`
grün bis auf `--check` und `--sonde` (die Werkzeuge schrieben `using`-Aliase und `partial` an Read-Models nicht zurück; behoben, auf `b01b789` beide
grün): Prüfstand **519 grün + 1 übersprungen** (neu `StoreKlassenTests` 5, `StoreKlasseAnalyzerTests` 5, Formfälle in `HandlerFormAnalyzerTests`;
entfallen `GaEinsPruefungTests`), Integration **67/67** (mit Last; neu `StoretestPostgresTests` 3: der allgemeine Store-Test für Marten, Datei-Ziel
und Senke), Prüfdomäne **60/60** (neu `StoreKlassenE2ETests`), Verhaltens-Netz **22/22** unverändert (mit `NeustartTests`), Client 30/30, Python SDK
und Worker grün, `codegen --check`, `--grammatik` und Gate grün. Rot unterwegs: Test-Stores ohne Read-Model-Schema und ohne `Id<T>`-Konverter
(Marten-Codegen bricht an `Id<T>` als Identität; jetzt `GeneratedProjectionServices.RegistriereSchema` und `IdSerialisierung` in den Fixtures),
Altzeilen `{"Wert": …}` aus diesem Lauf in den langlebigen Test-Datenbanken (gelöscht), zwei Testannahmen (Stand = Zahl der eigenen Events —
Funktionen der Messung hängen mehr an; ein zu grober Gesten-Text). `FB-13` trat in der ersten schnellen Stufe einmal auf, danach nicht mehr.
Gegenproben per `sed` im Server-Lauf rot gesehen: Klasse 1 ohne Revisionsprüfung (Store-Test Marten, Fall Neuaufbau), Klasse 2 ohne Bedingung
(Store-Test Datei, zweite Aktivierung: Stand 3 statt 5), Neuaufbau ohne neue Generation (Store-Test Datei: die alte Aktivierung sichtbar, 4 Einträge,
Stand 1002), Fortschritt vor den Wirkungen (`StoreKlassenTests`), Start-Prüfung lässt Klasse 3 ohne Erlaubnis zu, CQRS109, CQRS110 und CQRS111
abgeschaltet (`StoreKlasseAnalyzerTests`), Query-Dienst ohne gezeigte Ids bzw. ohne Projektionen hinter der Frage (`StoreKlassenE2ETests`).
Messung A-B (A = `567cfe0` im Worktree `/opt/bractor-a`, B = `b01b789`; S1, S3; beide gültig, Exactly-once grün; Ordner
`…_server-pubsub-phase8-ab-{a,b}`): S1 158 → 147 Paare/s, p50 2,6 → 2,9 s, Queries je Vorgang 90,5 → 122, Transaktionen gleich (9,7 → 9,8);
S3-64 1 466 → 1 513 Teile/s (p50 632 → 561 ms), S3-8 1 334 → 1 334 (p99 76 → 56 ms), Queries und Transaktionen je Vorgang gleich. Die zusätzlichen
Queries in S1 sind der Stand je gezeigter Id: er wird jetzt bei jeder Query gelesen, weil eine Antwort auch die Id ihrer Query zeigt (vorher nur nach
`ctx.Track` eines gefundenen Read-Models) — `mt_doc_projectioncheckpoint` 2 837 → 12 867, die Stand-Abfrage 630 → 10 651 Aufrufe (PF-38).
**PF-38 behoben (`b16b10a`):** liegt der Fortschritt im Postgres des Logs (Marten-Store, Fortschritt beim Framework, `IFortschrittImLog`), liest
der Stand je Id Guard und Log in einer Abfrage. Schnelle Stufe grün; Gegenprobe (Guard-Feld falsch gelesen) rot gesehen (`LiveCommandE2ETests`:
Stand verfehlt; `StoreKlassenE2ETests`: Read-your-writes `noch_nicht`). Neues Paar nur S1 (A = `567cfe0`, B = `b16b10a`, Ordner
`…_server-pubsub-pf38-ab-{a,b}`, gültig, grün): 155 → 152 Paare/s, p50 2,79 → 2,80 s, p99 3,81 → 3,84 s, Queries je Vorgang 91,8 → 104 (vorher 122),
Transaktionen gleich. Was bleibt, ist die eine Stand-Abfrage je Query (627 → 10 345) — gewollt, weil eine Antwort jetzt auch „noch nicht da“ mit Stand zeigt.

**Stand 2026-10-29 (Umbau-Phase 9 — direkte Aggregat-Abfrage, `562a84c` … `73ade32`):** volle Stufe auf `73ade32` grün bis
auf einen Verhaltens-Test: Prüfstand **527 grün + 1 übersprungen** (neu: R4 in `GesamtbildAnalyzerTests` 2, Form der Frage im Decider in
`HandlerFormAnalyzerTests` 1 + 5 Fälle), Integration **67/67** (mit Last), Prüfdomäne **64/64** (neu `AggregatAbfrageGarantieTests` 4: Antwort =
Stand des nächsten Commands, unter 60 gleichzeitigen Commands jede Antwort = Log an ihrer Version, eine Frage während eines scheiternden Commits,
eine Frage an eine fremde Identität), Verhaltens-Netz **22 von 23** (neu `ClientVerhaltenTests.Ein_Client_fragt_das_Aggregat_direkt…`, grün; rot
`NeustartTests` — G-18, älter als Phase 9: derselbe Test gegen `3e16e9a` 1 von 3 rot, gegen `73ade32` 3 von 4, jedes Mal eine stehende Partition
von `Verteilabwicklung`), Client 30/30, Python SDK und Worker grün, `codegen --check`, `--check`, `--sonde`, `--grammatik` und Gate grün. Rot unterwegs
(schnelle Stufe auf `562a84c`): der Python-Worker meldete sich nicht an — die betterproto-DTOs waren nicht neu erzeugt (FB-20, `build_python.sh`
nachgeholt); der neue Verhaltens-Fall nutzte einen JSON-Knoten zweimal; die Sonde zeigte Query und Antworten des Aggregats als Insel (die Sicht
bekam die Kante Query → Aggregat → Antwort). Gegenproben per `perl` im Server-Lauf rot gesehen: die Frage wartet nicht während des Commits (die
Antwort nennt den hinfälligen Stand), die Version der Antwort um eins verschoben, die Identität der Frage nicht geprüft, das Aggregat nicht als Ziel
im Gesamtbild (CQRS086 statt still, CQRS084 fehlt), CQRS057 für die Frage im Decider abgeschaltet, die wartende Frage nach dem Commit nicht
beantwortet (zuerst grün: Proto.Cluster stellt die unbeantwortete Anfrage nach ≈ 5 s erneut zu, dann antwortet der Actor — der Test verlangt jetzt
die Antwort binnen 2 s nach dem Commit, danach rot). Keine Messung: der heiße Pfad ist bis auf eine leere Warteschlange je Commit unverändert.

**Stand 2026-10-29 (Umbau-Phase 10 — Hausregel für Actors als Build-Prüfung, `90798a7`, `daee0c5`):** volle Stufe
auf `90798a7` grün bis auf 7 neue Prüfstand-Fälle: Integration **67/67** (mit Last), Prüfdomäne **64/64**, Verhaltens-Netz **23/23** (auch
`NeustartTests`, G-18 bleibt offen), Client 30/30, Python SDK und Worker grün, Codegen-Drift, `--check`, `--sonde`, `--grammatik` grün; das Gate rot
nur wegen derselben 7 Fälle. Die 7 Fälle waren Fehler der Proben, nicht des Analyzers: `Task.Run(…)` als Anweisung ist zugleich ein eigener Faden
und ein nicht abgewarteter Task — zwei Meldungen, wo der Test eine erwartete (die Meldungen zeigen nebenbei alle fünf Besitz-Wege: Feld, primärer
Konstruktor, Typ-Argument, Auto-Property, Array im abgeleiteten Actor). Nachlauf auf `daee0c5`: Prüfstand **552 grün + 1 übersprungen** (neu
`HausregelAnalyzerTests` 25: jedes der 15 Muster im Actor, 5 Besitz-Wege, außerhalb und im nicht besessenen geschachtelten Typ still, fertiger Task
und `await` erlaubt, nur nach Typ geprüftes Muster nicht, begründete Ausnahme still, leere Begründung CQRS113), Gate grün. Gegenproben am echten
Code als Build (Analyzer): je Muster eine Methode eingesetzt — `FunktionsActor` (Task.Run, new Thread, lock, nicht abgewarteter Task, .Result,
GetResult, `Cluster.RequestAsync`, `IContext.RequestAsync`, leere Begründung → CQRS113), `FunktionsLauf` als Feld-Typ (StartNew, `_ = …Async`),
`Rueckstau` als Feld-Typ (Parallel, .Wait()), `Uhrwerk` als Typ des primären Konstruktors (ThreadPool, ContinueWith): alle 15 rot. Keine Messung:
der heiße Pfad ist unverändert (der Snapshot läuft wie vorher neben dem Turn, das Dead-Letter-Schreiben nur beim Parken).

**Stand 2026-10-29 (Umbau-Phase 11 und die Befunde FB-22/FB-23, `de80da0` … `7ea2092`):** Phase 11 (Doku, Plan gelöscht) auf `de80da0` voll grün
bis auf `NeustartTests` (G-18). Auf `7ea2092` (FB-23: kein Client beantwortet Queries, zweiter Query-Weg zurückgebaut; FB-22: `CommandQuittung`
je Client-Command aus dem Dispatcher, der C#-Client verbindet selbst neu und sendet den Ausgangskorb nach, auch was ohne Verbindung kam): volle
Stufe grün bis auf `NeustartTests` (G-18) — Prüfstand **558 grün + 1 übersprungen** (neu `DispatcherQuittungTests` 4, `ClientVerbindungTests` 3;
entfallen der Zuständigkeits-Test der Query-Weiterleitung), Integration **67/67**, Prüfdomäne **64/64**, Verhaltens-Netz **23 von 24** (neu
`ClientVerhaltenTests.Ein_Noop_wird_dem_Client_quittiert…`; die Ablehnung an der rohen Leitung prüft jetzt auch ihre Quittung), Client 30/30,
Python SDK und Worker grün, Drift, `--check`, `--sonde`, `--grammatik`, Gate grün. In der schnellen Stufe davor war einmal
`TwoNodeCommandDispatchTests` rot („Request timed out“, ohne den Dispatcher) — dreimal einzeln wiederholt grün, ein Wackler beim Aufbau zweier
Knoten. Gegenproben am Server rot gesehen: Dispatcher ohne Quittung (Verhaltens-Fall rot), Client übernimmt die Version der Quittung nicht,
kein Wiederverbinden, Command ohne Verbindung nicht im Korb, ein wartender Command nach dem Wiederverbinden nicht gleich nachgesendet. Keine
Messung: der Messtreiber geht über den JSON-Eingang (Frage mit Antwort), den die Quittung nicht berührt; den gRPC-Weg misst die Vermessung bisher
nicht.

**Stand 2026-10-29 (G-18 behoben, `d0ccce2`, `71d3b75`):** kein Hänger, sondern zu wenig Durchsatz je Partition (docs/13 §13.6): der
Funktions-Actor startet jetzt aus dem Vorrat der gelesenen Seite, statt je Eingang eine Seite neu zu lesen; `NeustartTests` wartet zusätzlich auf
Rückstand 0 (`/monitoring/metrics`), bevor er „vollständig“ abnimmt. Schnelle Stufe auf `71d3b75` grün: Prüfstand **530 grün + 1 übersprungen**
(neu `Funktionen/FunktionsActorLeseTests` 1 und in `FunktionsLaufTests` 2), Integration ohne Last 65/65, Prüfdomäne 64/64, Verhaltens-Netz ohne
Last 22/22. `NeustartTests` 5 von 5 grün, je 77–85 s (vorher 184–232 s, die roten schöpften die Nachlauf-Frist von 3 min aus); auf `d0ccce2`, noch
mit der alten Abnahme, 2 von 3 (rot: 4 von 15 055 Nachschüben, abgenommen, während `Auftragsabwicklung` noch Rückstand hatte). Gegenproben per
`sed`: ohne Start aus dem Vorrat `FunktionsActorLeseTests` rot (201 statt höchstens 7 Lesungen), die alte Grenze einer vollen Seite rot
(Position 100 statt 2); `NeustartTests` ohne Vorrat einmal grün in 197 s — wackelt, deshalb nicht die Gegenprobe. Merge in `pubsub_neustrukturierung` nach der ersten
Gesamtprüfung. S5 A-B-B-A danach (2026-10-29, `…_pf36-vorrat-a1/b1/b2/a2`, A = `90798a7` ohne Vorrat, B = `f49bbf5`, alle gültig,
Exactly-once grün): S5-8 300/214 (A) gegen 833/866 (B) Vorgänge/s, ≈ 3,3 ×, p99 8,2/12,3 s gegen 2,1/2,2 s, Abbau 8/12 s gegen 1,8 s;
S5-64 297/250 gegen 933/940, ≈ 3,4 ×, p99 9,8/11,6 s gegen 2,9 s; Queries je Vorgang 39 → 27 (S5-8) bzw. 38 → 23 (S5-64); S5-1 gleich
(433/448 gegen 446/399) — mit einem Sender gibt es keinen Rückstau, also nichts neu zu lesen. Die B-Läufe liegen eng beieinander; die
Streuung von S5 (PF-36) kam aus dem Neulesen. Nach dem Merge (`2076038`) schnelle Stufe grün: Prüfstand **561 grün + 1 übersprungen**, Integration ohne Last
65/65, Prüfdomäne 64/64, Verhaltens-Netz ohne Last 23/23; `NeustartTests` einzeln grün in 79 s.

**Messreihe Slots und Partitionen (PF-33, 2026-10-29, `ec956e5`, Diagnose ohne Spalte):** nur S5, Slots der Funktionen `Auftragsabwicklung`,
`Verteilabwicklung` und `Nachschub` und `Konsum.Partitionen` über Umgebungsvariablen, je ein Lauf, dazu jede Sekunde CPU von Postgres und Host und
die Wartezustände der Verbindungen (`…_pf33-postgres/proben.csv`). Alle gültig, Exactly-once grün.

| Slots / Partitionen | S5-1 | S5-8 | S5-64 | p99 S5-64 |
|---|---:|---:|---:|---:|
| 1 / 8 (Standard bis `c6b6ec8`) | 443 | 800 | 807 | 3,4 s |
| 4 / 8 | 433 | 1 745 | 1 996 | 1,2 s |
| 16 / 8 | 406 | 1 784 | 2 326 | 0,9 s |
| 1 / 32 | 415 | 1 721 | 1 948 | 1,2 s |
| 16 / 32 | 396 | 1 684 | 1 533 | 1,4 s |

S5-8 ist ab 4 Slots durch die Sender begrenzt (angenommen ≈ sichtbar, p99 40 ms), S5-64 läuft noch in den Rückstau. Der Host nutzt 2,5–3,7 Kerne,
Postgres meist unter 4; Sperren warten nie, LWLock und IO kaum. Die Proben von Postgres sind für die kurzen Blöcke grob (2–10 je Szenario): ein
S5-Block hat 3 000 Vorgänge, bei 64 Sendern in 0,3–1 s angenommen — die Messung zeigt den Hebel der Slots, nicht die Decke. Transaktionen je
Vorgang fallen mit mehr Slots von 2,4 auf 0,5–1 (größere Gruppen im Group-Commit).

**Decke von S5 (PF-33, 2026-10-29, `c6b6ec8`, Standard jetzt 4 Slots, Diagnose ohne Spalte):** S5 mit 8 und 64 Sendern, Fenster 20 s, höchstens
40 000 Vorgänge je Block (`--sender S5=8+64 --fenster 20 --hoechstens S5=40000`), je ein Lauf, Proben wie oben (`…_pf33-decke-postgres`). Alle
gültig, Exactly-once grün. „Durchgehend“ = Vorgänge / (Fenster + Abbau); „Abbau“ = was nach dem Fenster noch sichtbar wurde, je Sekunde.

| Slots / Partitionen | S5-8 (20 s) | p99 S5-8 | S5-64 durchgehend | angenommen | Abbau | p99 S5-64 | Postgres-CPU Mittel / Spitze (S5-64) |
|---|---:|---:|---:|---:|---:|---:|---:|
| 4 / 8 (Standard) | 1 569 | 50 ms | 1 758 | 8 505 | 2 113 | 19,7 s | 4,1 / 14,8 Kerne |
| 16 / 8 | 1 561 | 40 ms | 2 028 | 3 933 | 2 963 | 14,6 s | 8,4 / 16,2 Kerne |
| 4 / 32 | 1 450 | 40 ms | 1 485 | 3 468 | 1 929 | 21,3 s | 10,9 / 19,3 Kerne |

S5-8 hält 20 s lang ≈ 1 500–1 570 Vorgänge/s ohne Rückstau (die acht Sender warten je Vorgang auf die Quittung — das ist ihr Takt, nicht die
Decke). Bei 64 Sendern nimmt die Schreibseite bis 8 500 Vorgänge/s an, 40 000 also in 5–12 s; die Funktionen bauen den Rückstau danach mit
≈ 2 100 (4 Slots) bzw. ≈ 3 000 (16 Slots) Vorgängen/s ab. Die Decke durchgehend liegt damit bei ≈ 1 800–2 000 Vorgängen/s, beim Abbau allein
bei 2 100–3 000. Der Host nutzt im Mittel 3,2–3,4 Kerne (Spitze 5,7). Postgres steigt mit Slots und Partitionen bis 11 Kerne im Mittel und
fast 20 in der Spitze, also die ganze Maschine; LWLock wartet bei 32 Partitionen im Mittel 3,4 Verbindungen, Sperren nie. Mehr Partitionen
bringen deshalb nichts mehr, sie verlagern Last auf Postgres. Queries je Vorgang 18–23 (S5-64) bzw. 34–42 (S5-8, kleinere Gruppen).
Die DB-Zeit unter S5-64 (`pg_stat_statements`) trägt das Lesen der Konsumenten je Partition: 67 % bei 4 Slots (2,9 Aufrufe je Vorgang, 0,47 ms je Aufruf), 87 % bei 16 Slots (1,5 je Vorgang, 3,0 ms je Aufruf); danach das Anhängen der Events (17 % bzw. 7 %) und die Versionsprüfung der Streams (6 % bzw. 2 %). Weiter in docs/13 PF-40.

Statisch gezählt:

| Projekt | `[Fact]` | `[Theory]` | Dateien |
|---|---:|---:|---:|
| Infrastructure.Pruefstand.Tests | 464 | 8 | 76 |
| Infrastructure.Integration.Tests | 49 | 6 | 23 |
| Pruefdomaene.Integration.Tests | 53 | 0 | 12 |
| Verhalten.Tests | 16 | 0 | 5 |
| Client.Infrastructure.CollectionTests | 31 | 0 | 2 |
| Werkbank.Abnahme.Tests | 1 | 0 | 1 |

Gezählt (2026-10-28, nach Umbau-Phase 4b): Zeilen `[Fact` bzw. `[Theory`, Dateien = Dateien mit Tests. Die Differenz zu
den Läufen kommt von den expandierten `[Theory]`-Fällen.

## 12.3 Was getestet ist

**Prüfstand (Ebene 1), nach Bereich (Testmethoden, Stand Umbau-Phase 4):**

| Bereich | Tests | Klassen |
|---|---:|---|
| Analyzer | 74 | `ZustandAnalyzerTests` (CQRS105, R19: Felder, Setter, Sammlungen, IZustand außerhalb einer Funktions-Klasse), `AkteurAnalyzerTests` (17), `FaehigkeitAnalyzerTests` mit `HandlerFormAnalyzerTests` (CQRS051/054/055, CQRS057 auch für eine Projektion, die etwas ausgibt), `ModellAnalyzerTests`, `QuellenTypAnalyzerTests`, `StoreZugriffAnalyzerTests`, `AusgabeVertragAnalyzerTests`, `GesamtbildAnalyzerTests` (CQRS084–087: Funktion ohne Umsetzung und ohne Träger; CQRS106, R20: gehörter Hinweis ohne Erzeuger), `LageAnalyzerTests`, `WiederholungAnalyzerTests` (R2/R11/R12/R13), `CommandEmitAnalyzerTests` |
| Editor-Modell, Scaffolder, Grammatik | 55 | `DomainEditorLeseseiteTests`, `DomainEditorAkteurTests` (Kette über eine Funktion, Akteur darf eine Funktion draußen rechnen), `DomainEditorScaffolderTests`, `DomainEditorStrukturortTests`, `DomainEditorKompositionTests` (Grammatik nach docs/10: Hinweis nur an Funktion und Client, Projektion erzeugt nichts, Frist über die Uhr), `PythonQuelleTests` |
| Extractor, Gesten, Sicht, Werkbank | 41 | `GestenTests` (22), `SichtTests`, `LesenToleranzTests`, `UsingsTests`, `GitKopfTests`, `ProtoVergleichTests` |
| Funktionen und Uhr | 60 | `FunktionsBausteinGeneratorTests` (Dispatch, erlaubte Rückgaben, draußen und Umsetzungen, gleichbleibend, Hinweisplan, Zustand im Konstruktor samt Kontext und eine Adresse, lernendes `Auf`, CQRS100/101), `FunktionsLaufTests` (Slots, ein Stream nacheinander, Position, gedeckelte Liste, Parken, Stand eines geparkten Streams rückt mit), `FunktionsArbeitTests` (Festhalten, Abbruch, zwei Läufe, Strom, unbekannter Emit-Ausgang, Hinweise vom Gewinner, draußen mit und ohne Antwort, zwei Worker, Antwort außerhalb der Signatur; mit Zustand: `Auf` je Eingang einmal, Nachrechnen bei festgehaltener Ausgabe, kein Festhalten ohne Ausgabe), `FunktionsAktivierungTests` (Umsetzung und Dienste je Aktivierung, Freigabe-Reihenfolge), `HinweisNetzTests` (Routing an die Adresse, Kennungen, Command-Ids aus der Kennung, Wiederholen ohne Neurechnen, Boot-Fehler), `FunktionsVermittlungTests` (dazu A8: Bindung je Adresse), `BildeingangTests`, `UhrTests` |
| Akteure, Clients | 34 | `AkteurRechteTests`, `ClientVertragTests`, `ClientCodeVertragTests` |
| Schreibpfad, Serialisierung, Ids | 20 | `EventJsonSerializationTests`, `EventVersioningTests`, `AppendBatchingTests`, `DtoMapperRoundTripTests`, `TypisierteIdTests`, `CommandVorpruefungTests`, `DispatcherCommandFailedTests` |
| Beispiel-Aggregate | 40 | `DatensatzAggregatTests`, `TrainingslaufAggregatTests` (Frist des Wächters im Decide), `SammelvorgangBarriereTests`, `SammelvorgangKaskadeTests`, `BoundedInboxTests` |
| Quellen, Umgebung, Spur, Dienste | 59 | `DateiTreiberTests` (18), `UmgebungLaderTests` (Laufort-Bindung der Funktionen: genannte Umsetzung, unbekannte Umsetzung, Extern nur ohne Lese-Fähigkeit, Standard ohne Eintrag, unbekannter Name, Hand), `ProjektionsStandTests`, `SpurTests`, `WirkerIdTests`, `SpurWegTests`, `DienstBindungTests` (15) |
| Konsum, Projektionen, Wecken | 28 | `BatchLeserTests`, `ProjectionRebuilderTests`, `GaEinsPruefungTests`, `WeckungenTests`, `WeckplanGeneratorTests` (Kinds aus den Signaturen, eine Adresse, Uhr, Hinweis nicht im Plan, CQRS108), `WeckerTests` (Ziele je Commit, Group-Commit, verlorener Weckruf, Start-Prüfung), `TransientPersistiertSplitTests`, `DeadLetterReadStoreTests` |
| Lebenszyklus, zwei Verben (Sammlung „Zeitkritisch“) | 19 | `RahmenActorTests`, `BoteTests`, `PassivierungTests` (Leerlauf stoppt, Nachricht schiebt hinaus, 0 = nie), `LebenszyklusImClusterTests` (Batch-Leser gibt seinen Bereich erst nach dem Durchlauf frei; eine Frage an einen schweigenden Actor läuft in ihre Frist) |
| Übrige | 9 | `BackendMonitoringTests`, `EingangJsonTests` |
| Clients: Verteiler, Ausgangskorb | 7 | `VerteilerTests` (je Sitzung eine Zustellung in Log-Reihenfolge, nur Gehörtes; gezielte Antwort nur an ihre Sitzung, auch ohne Abo; beendete Sitzung ausgetragen, neue Anmeldung ersetzt; über der Grenze des Rückstaus verworfen; Sitzungs-Id nennt den Knoten), `AusgangskorbTests` (nach dem Abriss dieselbe Command-Id an die neue Sitzung, mit der Antwort erledigt; der Datei-Korb übersteht einen Neustart) |
| Garantien (Störung über Seams, `Garantien/`) | 33 + 1 übersprungen | `HorizontTests`, `BatchKonsumGarantieTests`, `EmitGarantieTests` (Emit-Ausgang je Antwort, Drossel je Ziel, volle Drossel meldet unbekannt), `RehydrationGarantieTests` (Split-Upcast Skip G-5), `BuildGarantieTests` |

**Integration (Ebene 2):**

| Klasse | Tests | Beweisgegenstand |
|---|---:|---|
| `CoCommitPostgresTests` | 3 | Batch-Co-Commit: Effekte vieler Streams, Guards und Partitions-Checkpoint in einer Marten-Session; Absturz mitten im Batch → nichts gültig |
| `GeteilteDokumentePostgresTests`, `SchreibRegelPostgresTests` | 4 + 5 | Single-Writer je Dokument, OCC bei geteilten Dokumenten zwischen Partitions-Lesern |
| `SequenceGapPostgresTests` | 4 | `seq_id`-Lücke; eine lange offene Transaktion wird nicht überholt (G-9, über die gesetzte Sequenz des Partitions-Reads); der Batch-Leser überholt einen Nachzügler nicht |
| `BatchNachholPostgresTests` | 1 | der Batch-Leser holt ein Event mit verlorenem Weckruf beim nächsten Takt nach (auch eines, das während des Durchlaufs kam) |
| `Garantien/AnhaengeReihenfolgePostgresTests`, `Garantien/ZaehlerstandPostgresTests`, `Garantien/EventZeitPostgresTests`, `DbUhrPostgresTests` | 1 + 1 + 3 + 1 | Wächter des Horizonts: Martens Reihenfolge, der Zählerstand der Probe eilt nie voraus (G-17, acht ziehende Verbindungen, ≈ 8 s); eine Uhr (G-10); DB-Uhr mit Versatz |
| `MetadataPostgresTests` | 3 | Header-Readback |
| `LiveCommandE2ETests`, `TwoNodeCommandDispatchTests`, `RemotePidDeliverySmokeTests` | 4 | Command → Event → Projektion im Cluster, Stand je Id aus dem Fortschritt der Projektion; Latenz: der Commit weckt die Projektion (dreimal unter 5 s); Cross-Node |
| `AblaufE2ETests` | 5 | Beispieldomäne im echten Cluster: Bildeingang Quelle → Bildaufbereitung → Bildeingang → Fakt, auch mit Python-Worker, der die Bildaufbereitung über gRPC trägt (Laufort Extern, FB-2); Wächterfrist im Stream des Laufs; Lese-Fähigkeit; Datei-Treiber genau einmal auch nach Neustart |
| `UmgebungE2ETests`, `LeseseitenStandE2ETests`, `EingangE2ETests`, `MonitoringHttpTests` | 4 + 1 + 2 + 2 | Siegel, Uhr-Versatz über das Fälligkeits-Verzeichnis, Projektions-Stand nach Code-Änderung, JSON-Eingang (Hand), Client-Wiederholung, `/health` |
| `Garantien/GarantiePostgresTests` | 7 | ein Append ganz oder gar nicht, zwei Schreiber ohne verlorenes Update, Frist und Events gemeinsam gültig; Batch-Konsum: zwei Aktivierungen (G-4), Poison parken und nachholen, Neuaufbau nimmt einem laufenden Leser den Commit |
| `Garantien/AbfragePlanGarantieTests` | 2 Theorien (4 Läufe) | die häufigen Abfragen (Horizont „dahinter?“, Seite einer Partition, Stream ab Version, Rückstand ab Position 0, fällige Fristen der Uhr) lesen bei 500 000 Events und 200 000 Fristen weniger als 200 Blöcke — mit zugeschnittenem und allgemeinem Plan, veralteter und frischer Statistik |
| `Garantien/HorizontStressGarantieTests`, `Garantien/HorizontAbbruchGarantieTests`, `Garantien/InboxAufraeumenGarantieTests` | 1 + 1 + 1 | G-11-Stress; Abbruch am Horizont; Aufräumen der Inbox je Seite (PF-27) |

**Prüfdomäne (Ebene 2, `Pruefdomaene.Integration.Tests`):**

| Klasse | Tests | Beweisgegenstand |
|---|---:|---|
| `AblaufE2ETests` | 6 | Abläufe als Aggregat + Funktion im Cluster: Auftrag reserviert und belegt je einmal, einmal bestätigt; scheitert ein Zweig, wird der gelungene genau einmal zurückgenommen und nie bestätigt; scheitern beide, wird nichts zurückgenommen; nebenläufige Aufträge auf denselben Aggregaten; Verteilung bucht jedes Ziel einmal und schließt nach allen ab; reicht die Quelle nicht, scheitert die Verteilung ohne Zubuchung |
| `SnapshotTests`, `UpcastingTests` | 3 + 5 | Snapshot-Ablage und Rehydration; Upcasting über den echten Seam |
| `Garantien/SchreibGarantieTests` | 11 | doppelt und gleichzeitig zugestellter Command, alte CommandId jenseits des Caches, Wiederholung nach Neustart (Absage und Ablehnung), Duplikat im Group-Commit, Horizont der Inbox, heißer Posten, Bündel, zweiter Schreiber, Versionskonflikt, mehrdeutiger Commit, fremde Identität |
| `Garantien/EventReadGarantieTests` | 3 | festes SQL liest dasselbe wie Martens LINQ (auch Partitions-Seiten), archivierte Streams und Tombstones liest keiner (G-7) |
| `Garantien/LebenszyklusGarantieTests` | 1 | Passivierung und Reaktivierung: derselbe Stand, dieselbe Inbox |
| `FunktionsBausteinE2ETests` | 5 | gleichbleibend ohne Festhalten, mit Dienst festgehalten, Strom stückweise, Schnittstelle mit einer Umsetzung im Host, Laufort Hand |
| `UhrE2ETests` | 3 | fällige Frist mit der Id aus ihrem Event, vor der Fälligkeit erledigt, doppelt gefeuert wirkt einmal |
| `QuellenE2ETests` | 2 | Quell-Nachricht vom Client genau einmal und über eine Funktion weiter — dreimal je unter 5 s bei 30 s Takt (der Wecker am Commit der Quelle); ein Stream je Schlüssel |
| `Garantien/FunktionsGarantieTests` | 4 | Abbruch nach dem Festhalten vor den Commands, Abbruch nach den Commands vor der Position, zwei Aktivierungen, zwei Worker |
| `Garantien/FunktionsZustandGarantieTests` | 9 | am allgemeinen Fall Messung (Umbau-Phase 4b; der Schätzer rechnet mit Lebenszyklus auf einem Arbeitsmodell): verliert eine Sicherung, beginnt die Aktivierung neu mit dem Stand des Gewinners; Hinweis-Kette bis zum Command ohne Log-Eintrag des Hinweises; Umzug einer Aktivierung (der Zustand kommt mit); Absturz zwischen Sicherung (Datei) und Position — jeder Log-Eingang wirkt genau einmal auf den gesicherten Zustand; zwei Aktivierungen derselben Adresse; zwei Sicherungen mit derselben Version (eine gewinnt ganz); Ressource beim Stoppen geschlossen; Commands aus einem doppelten Hinweis wirken einmal; eine Funktion draußen hört einen Hinweis über die Sitzung der Leitung |
| `Garantien/WeckerGarantieTests` | 4 | Latenz Funktion (dreimal unter 5 s) und Uhr (Frist von 1 s in Sekunden); verlorene Weckrufe holt der Takt genau einmal nach; doppelte Weckrufe wirken genau einmal |
| `Garantien/AblaufGarantieTests` | 2 | die Funktion `Nachschub` wirkt genau einmal über Wecker und Takt; Neustart des Hosts mitten im Auftrag führt ihn zu Ende |

**Verhaltens-Netz (`Verhalten.Tests`):** `BeispielAblaufTests` (3), `PruefdomaeneAblaufTests` (4), `TrainingsWaechterTests` (1),
`ClientVerhaltenTests` (11: Pushes mit Kopf-Version, Quittung über die Korrelation, Ablehnung als Quittung im Client-Stack und an der rohen Leitung,
Vertrag ∩ Token, Träger-Antwort genau einmal, Lesen nach Schreiben, nach dem Anlegen (G-14), Version aus der Antwort, transitive Aktualisierung,
Wiederverbinden mit Nachlesen), `ZweiKnotenTests` (2: Weckrufe nach dem harten Ende eines Knotens; Ablehnung an der richtigen Sitzung bei zwei
Knoten, G-13), `NeustartTests` (1, Kategorie Last).

**Client:** `VirtualCollectionTests` (26), `PendingCommandTrackerTests` (5). **Python:** SDK `test_vertrag`, `test_funktion`, `test_client_query`,
`test_client_reconnect`, `test_mapper_*` (inkl. Nutzlast-Tabelle); Worker `test_vertraege`, `test_funktionen`, `test_funktions_worker`, `test_training_worker`.

## 12.4 Was nicht getestet ist

- **Kreise Funktion ↔ Aggregat ohne Fortschritt** — der Build erkennt sie nicht (docs/13 FB-5).
- **Echter Python-Prozess mit Hinweis-Eingang über gRPC** — nachgewiesen sind das SDK (pytest) und der Weg des Servers über dieselbe Sitzung
  (docs/13 FB-8).
- **Reihenfolge der Hinweise zwischen zwei Actors** — nicht zugesichert, ohne Test (docs/13 G-15).
- **Spur über Funktionen und Commands als lückenlose Kette** — nur indirekt (Treiber S3 ordnet über die Ursache zu); der Prüfstand-Test dazu lief
  über die entfernte Katalog-Welt.
- **Werkbank-Abnahme** — ihr einziger Test ist zurückgestellt (Editor, docs/10 E4).
- **Paralleler Drain / Schreibpfad-Skalierung** — kein Assertion-Test (früher nur im LoadHarness beobachtet).
- **Shard-Rebalance / Node-Ausfall** — nur simuliert.
- **Blazor-Client** jenseits `VirtualCollection`/`PendingCommandTracker` (Bus, Store, Transport).

## 12.5 Messwerte

**Werkbank (2026-10-12/13):** warmes Lesen nach Text-Änderung p50 224 ms (`GraphExtractor -- --warm`); Geste p50 unter Fremdlast 346–470 ms
(Ziel 300 ms).

**Prozess-Maschine (P5b, gegen echtes Postgres):** Marking-Cursor und feuer-gerichtete Reads machen den warmen Fold bis 9× schneller bei N = 60
(5,5× mit Aggregat-Historie); die Stream-Reads sinken von O(N²) auf O(N).

**Schreibpfad (2026-08, mit dem inzwischen entfernten LoadHarness, 4-Kern-localhost):** Group-Commit-Batching mit parallelem Drain +48 % Durchsatz.
Negativ-Befunde, die nicht erneut untersucht werden müssen:
- Serialisierung ist für den Durchsatz irrelevant (~0 %); der STJ-Source-Gen-Serializer brachte nichts (bleibt opt-in, `Schreibpfad.GeneriertesJson`).
- Synchroner Redis-Zugriff im Command-Turn ist bei localhost-Redis irrelevant (~0 %); relevant erst bei Remote-Redis.
- Die CPU ist unter Volllast zu ~80 % idle; der Hebel ist Parallelität, nicht schnellere Einzel-Commits. Ein eigener COPY-basierter Event-Store
  ist zurückgestellt.

**Postgres-Grundgrößen der Konsum- und Schreibseite (2026-10-15, `scripts/vermessung-postgres.sh`).** Bedingungen: Postgres 16 im Container
(Docker Desktop, macOS, 10 Kerne), `pgbench` im Container (ohne Netzwerk), rohes SQL ohne Marten, `synchronous_commit = on`, 200 000 Streams,
rund 2,6 Mio. Events, 15 s je Lauf; zwei Läufe, der zweite unter Fremdlast auf dem Rechner (Rohwerte:
`vermessung/ergebnisse/2026-10-15_000*_nachgetragen_postgres-grundgroessen-lauf*/`). Die absoluten Werte sind eine Obergrenze (Marten,
Netzwerk und Proto.Actor kommen im echten Host dazu); **belastbar sind die Verhältnisse**, sie waren in beiden Läufen gleich.

| Operation (bildet nach) | Lauf 1 | Lauf 2 |
|---|---|---|
| Weckung je (Projektion, Stream): Tracker lesen, Tail lesen, Effekt + Checkpoint in einer Transaktion (bis Phase 2 der Pfad der Projektionen) | 8 200/s (16 Verb.), 3 300/s (4) | 4 100/s, 1 700/s |
| leere Weckung (Poll-Heilung, nichts zu tun): zwei Lese-Queries | 37 000/s (16), 13 400/s (4) | 16 700/s, 8 200/s |
| Batch: 500 Effekte + 500 Checkpoints in einer Transaktion (4 Verb.) | 350 Tx/s = 175 000 Effekte/s | 182 Tx/s = 91 000 Effekte/s |
| Group-Commit, 256 Appends je Tx, ohne Inbox-Marke (4 Verb.) | 510–780 Tx/s | 554 Tx/s |
| dasselbe mit `KommandoVerarbeitet` je Command | 363 Tx/s | 332 Tx/s |
| Poll-Fenster: 30 000 Events mit Payload lesen (nur DB) | 9 ms | 9 ms |
| nachhängende Streams als eine Abfrage über 200 000 Streams | 92 ms | 105 ms |
| langer Stream (100 000 Events) ab 0 | 48 ms | 194 ms |
| derselbe ab Cursor (letzte 10) | 0,02 ms | 0,03 ms |

Folgerungen (Verhältnisse):
- **Batch-Konsum gegen Weckung je Stream: Faktor ~20–22** Effekte/s (docs/13 PF-1). Mit Netzwerk zwischen Host und DB wird der Abstand größer.
- **Inbox-Marke im Stream kostet 35–40 %** Durchsatz im Group-Commit (PF-3).
- **Stream ab 0 gegen ab Cursor: Faktor > 2 000** bei 100 000 Events; wächst linear mit der Stream-Länge (G-2, PF-4).
- Der Poll-Scan selbst ist billig (9 ms je 30 000 Events); teuer sind die Weckungen je Stream danach (PF-2).
- Hochgerechnet auf Weckung je Event trägt die Konsumseite heute etwa 8 200 / K Events/s (K = replaybare Projektionen): bei K = 5 rund 1 600,
  bei K = 20 rund 400 — vor dem Overhead von Marten und Netzwerk. Der Schreibpfad ist nicht die Decke. Gegen den echten Host gemessen ist das
  noch nicht — das tut die E2E-Messung unten.

**Ende zu Ende an den realen Domänen, unter Höchstlast (seit 2026-10-15, `scripts/vermessung-e2e.sh`, ≈ 5 min, Plan Garantien und Durchsatz
§0.1).** Gemessen wird der echte Host im Release-Build (`Host.Grpc` in `umgebungen/umgebung.vermessung.json`, `Host.Pruefdomaene` in
`umgebung.vermessung-pruefdomaene.json`, je eigene DB; bis Phase 5 dazu ein eigener Redis), Last nur über die echten Eingänge, gezählt bis zum
fachlich sichtbaren Ergebnis. Beide Umgebungen sind **Art Prod** — im Host läuft derselbe Code wie im Betrieb (seit 2026-10-16, Spalte „Art Prod“;
vorher Sim: die Uhr las je Event den Versatz, und Spur-Hinweise wurden geschrieben) — und folgen seit 2026-10-17 (Spalte „Prod-Konfig“) der
Konfiguration von `umgebung.prod.json`: Cluster über Consul (ein Knoten), 2 Slots je Bildfunktion, Datei fertig nach 1 s Stabilität, Erkennung per
Abfrage, Kennung Name und Größe, Webhook. Bewusst anders: das Muster `*.tiff` (der Treiber legt Fertig-Marken daneben), der JSON-Eingang ist an
(über ihn treibt der Treiber S3–S5 an), die Worker-Abschnitte für S2 (Prod rechnet im Host), und
Treiber, Host und Postgres teilen einen Rechner. Jeder Block sendet höchstens 15 s so schnell, wie der Eingang annimmt, dann baut der Rückstand ab:

| Block | Eingang | sichtbar, wenn |
|---|---|---|
| S1 Bildeingang Host | Stoß von 600 TIFF-Paaren (96 × 64) ins Quell-Verzeichnis, verschoben, dann Fertig-Marke | `GetImagePair` liefert das Paar komplett (je offenes Paar alle 100 ms) |
| S2 wie S1, Bild-Funktionen im Python-Worker (`Laufort Extern`, zwei Worker) | Stoß von 200 Paaren | wie S1 |
| S3-8, S3-64 Sammelvorgang heiß | 8 bzw. 64 Sender, je Teil `StarteTeilauftrag` + `SchließeTeilauftragAb` an EINEN Sammelvorgang (höchstens 2 000 Teile) | `TeilVerbucht` des Teils im Log (keine Query vorhanden) |
| S4 Datensatz | als KI-Operator `ErstelleDatensatz`, `FuegeRangeHinzu` (alle Paare aus S1), `FriereEin` | `HoleDatensatz`: alle Paare Mitglied, dann `Eingefroren` |
| S5-1, S5-8, S5-64 Prüfdomäne verteilt | 1, 8 bzw. 64 Sender, je zehn Vorgänge 8 Aufträge (Diamant), 1 Auftrag gegen ein volles Kontingent (Kompensation), 1 Verteilung (Fan-out 4, Count-Join), höchstens 3 000 je Block; jede Reservierung unter dem Mindestbestand weckt die Reaktion | `ProzessBeendet` im Log; die Reaktion gesondert (Reservierung → Nachschub) |
| S6-8, S6-64 Clients über gRPC | 8 bzw. 64 C#-Clients (Stack des Blazor-Arbeitsplatzes, je eine gRPC-Sitzung): je Vorgang `NimmPaarAuf` bis zur Quittung, höchstens 50 Paare je Datensatz, höchstens 3 000 je Block | `HoleDatensatz` des Clients (read-your-writes) zeigt das Paar; dazu Quittung p50/p99, kein Command unbestätigt |
| L1, L5 Leerlauf | ohne Eingang nach den Blöcken: erst der Nachlauf, bis alle Poll-Cursor und Projektions-Positionen am Kopf des Logs stehen (höchstens 180 s; Dauer und Queries als eigene Kennzahlen), dann 60 s (zwei Poll-Perioden) — bis 2026-10-16 15 s, 5 s nach dem letzten Block (PF-14) | Grundlast (PF-2, PF-4, PF-14) |

Kennzahlen je Block: angenommen je Sekunde (Schreibseite), sichtbar je Sekunde im Fenster und Durchsatz bis „alles sichtbar“ (Ende zu Ende),
Rückstand am Fensterende und Abbauzeit (ein nicht abgebauter Rückstand ist ein Ergebnis), Latenz p50/p95/p99, Transaktionen und Queries je
Sekunde und je Vorgang (`pg_stat_statements`, `track = top`, Transaktionen = COMMITs, ohne die Lese-Rolle des Treibers), CPU und RSS des Hosts,
„erster Vorgang sichtbar nach“. Bei einem Stoß (S1, S2) entfallen „sichtbar im Fenster“ und „Rückstand am Fensterende“ — der Stoß ist angenommen,
bevor das erste Paar fertig ist, beide wären immer 0 bzw. der ganze Stoß. Die Fremdlast vor und nach dem Lauf ist die CPU je Prozess über eine
Sekunde aus `/proc` (nicht `ps -o pcpu`, das den Durchschnitt über die Lebenszeit eines Prozesses nennt).
Eine Aufwärmrunde vor S1 und S5 zählt nicht. Jeder Lauf auf frischen DBs; danach die Exactly-once-Prüfung ([11](11-garantien.md) §11.14) — mit
offenem Rückstand prüft sie „höchstens einmal, nichts doppelt“. Ablage: `vermessung/ergebnisse/<datum>_<commit>_<anlass>/` (nur anhängen); die
Tabellen erzeugt `scripts/vermessung-tabelle.sh --schreibe` aus diesen Ordnern — nie von Hand, je Rechner eine mit eigener Baseline
(MacBook Air M4 hier, der Messserver weiter unten; Werte verschiedener Rechner sind nicht vergleichbar).

**Tabelle MacBook Air M4** (bis Phase 3, unter Fremdlast):

<!-- vermessung-tabelle:mac:anfang -->
(noch keine Spalte)
<!-- vermessung-tabelle:mac:ende -->

Baseline (2026-10-15, `3cfec32`, MacBook Air M4 unter Fremdlast — ein Spiel lief, Last 20–25) in Verhältnissen: die Schreibseite nimmt bis
etwa 2 200 Commands/s an (S5, 64 Sender), Ende zu Ende kommen 110–140 Vorgänge/s durch (S5), 20 Bildpaare/s (S1), etwa 50 Teile/s an einem
heißen Sammelvorgang (S3) — die Decke liegt im Konsum (Projektionen, Dirigent), nicht im Schreiben (PF-1, PF-4). Ein Bildpaar kostet etwa
30 Transaktionen und 260 Queries, ein Vorgang der Prüfdomäne 6–11 Transaktionen und 60 Queries. Im Leerlauf nach den Blöcken bleiben
780 bzw. 3 700 Queries/s bei 90 bzw. 200 % CPU (PF-2, PF-4, G-2: Polls lesen lange Streams ab 0), und der RSS fällt nicht zurück (bis 2,7 GB
nach S3 mit 64 Sendern, PF-5). Störungslauf (`2026-10-15_0433_3cfec32_baseline-stoerung`): 19 031 Vorgänge der Prüfdomäne und 181 Bildpaare mit
zwei harten Host-Abbrüchen und 10 s ohne Redis — alles genau einmal.

Phase 1 (2026-10-15, `b955d40`, gleiche Fremdlast; Emit-Ausgang, Emittenten-Cursor, Drossel je Ziel, Backoff des Dirigenten) gegen die Baseline:
Ende zu Ende S1 1,6 × (31 Paare/s), S2 1,5 ×, S3 heiß 1,9–2,0 × (≈ 100 Teile/s, Abbauzeit halbiert, Prozess-Ende ohne Nachlauf), S4 1,7 ×,
S5 1,1–1,4 ×; die Reaktion antwortet schneller (S5-64 p50 17 statt 27 ms, p99 127 statt 190 ms), Queries je Vorgang gleich oder weniger. Der
Leerlauf L1 zeigt mehr Arbeit (1 600 statt 260 Transaktionen/s): es sind die Erstweckungen der neuen S3-Streams durch den Heil-Poll jeder
Projektion (24 133 Checkpoint-Lesezugriffe, nur 109 fanden einen — PF-2, je neuer Stream × 5 Projektionen). Diese Fixkosten fielen in der
Baseline teils nach dem Messfenster an; nach ≈ 20 s ist der Host still (≈ 1 % CPU). Der Leerlauf ist deshalb erst nach Phase 3 (Batch-Konsum,
ein Heil-Poll nur für nachhängende Streams) vergleichbar. Störungslauf (`2026-10-15_0542_b955d40_phase-1-stoerung`): 22 190 Vorgänge und 181
Bildpaare mit zwei harten Abbrüchen und Redis-Ausfall — alles genau einmal.

Phase 2 (2026-10-15, `4caa176`/`860cb7a`; Rehydration nach Append-Fehler, Passivierung mit den E7-Werten 600/120/600 s) gegen Phase 1: S1–S4
0,92–1,07 ×, S5 0,81–0,89 × im Gesamtlauf. Der Abstand bei S5 ist Wärme, nicht Code: S5 allein, abwechselnd mit und ohne Passivierung gemessen
(Reihenfolge A-B-B-A, Ordner `…phase-2-ab-passivierung-*`), liefert mit Passivierung S5-8 208/198, ohne 189/161 Vorgänge/s — auf oder über
Phase 1. Neue Kennzahlen der Leerlauf-Blöcke: RSS am Fensterende und Höchststand, aktive Actors (`/monitoring/metrics`). Mit 600 s Leerlauf
passiviert im 15-s-Fenster nichts, und L1/L5 sind ohnehin nicht leer (Heil-Poll PF-2, Prozess-Poll PF-4 wecken Tausende Actors); das E5-Ziel
„RSS im Leerlauf < 50 %“ ist erst nach Phase 3 und 5 sinnvoll messbar. Ein Lauf mit 10 s Passivierung zeigte eine Todesspirale (der Backstop
weckte alle 15 s jeden gerade passivierten Vorgang kalt) — seitdem verlangt ein Boot-Guard ≥ 60 s für den Prozess-Manager. Störungslauf
(`…_phase-2-stoerung` mit Abbau nach Fortschritt): 29 609 Vorgänge und 181 Bildpaare, alles genau einmal, Abbau 310 s.

Phase 3 (2026-10-15, `0b02802`; Batch-Konsum der Projektionen), gemessen unter starker Fremdlast (ein Spiel lief, Last 10 → 27 im
Höchstlast-Lauf, bis 82 im ersten Störungslauf; die Docker-VM zuletzt bei 330 % CPU) — die absoluten Zahlen sind deshalb schlechter als in
Phase 2, auch dort, wo Phase 3 nichts geändert hat (S5 hat keine Projektion, S5-64 0,24 × Phase 2). Was der Lauf trotzdem klar zeigt:
- **Leerlauf L1 ist leer:** 0,1 Transaktionen/s (Phase 2: 1 536), 602 Queries/s (5 182; der Rest sind Stream-Reads des Prozess-Polls, PF-4),
  33 % CPU (337 %), 10 280 aktive Actors (63 827), RSS am Fensterende 0,1 des Höchststands. Der Heil-Poll je Stream jeder Projektion ist weg
  (PF-2); im L1-Fenster liefen genau 40 Seiten-Abfragen (5 Projektionen × 8 Partitionen, ein Poll).
- **Weniger Arbeit je Vorgang dort, wo Projektionen oder ihr Heil-Poll mitliefen:** S2 Transaktionen je Paar 0,59 × Phase 2 (28,5 statt 48),
  Queries 0,69 ×; S3 Transaktionen je Teil 0,48 × (64 Sender) bzw. 0,73 × (8 Sender) — die Erstweckungen jedes neuen S3-Streams durch jede
  Projektion entfallen.
- **S1 ist durch die Quelle begrenzt, nicht durch den Konsum:** je Sekunde werden genau halb so viele Paare komplett, wie der Datei-Treiber
  Dateien ins Log meldet (Zeitachse des Logs, gleich für Phase-2- und Phase-3-Code) — er meldet eine Datei nach der anderen (Rückstau,
  [11](11-garantien.md) §11.8), und die Leseseite hält in Echtzeit mit. Die Seiten der Partitions-Leser bleiben unter diesem Tröpfeln klein
  (≈ 3,5 Events je Commit; je Paar 7,6 Seiten und 5,8 Partitions-Commits). Von 239 Queries je Paar sind 64 das 100-ms-Polling des Treibers auf
  `GetImagePair` (wächst mit der Latenz) und 50 Stream-Reads des Dirigenten (PF-4).
- **A-B-B-A** (nur S1 und S5, A = Phase-2-Code `ee401da` aus einem Worktree, B = `0b02802`; Ordner `…phase-3-ab-s1s5-*`): S1 28,6 / 23,0 / 22,2 /
  11,3 Paare/s — im Rauschen der Fremdlast (A2 lief bei Last 31); S5-8 125 / 146 / 154 / 76, S5-64 97 / 120 / 122 / 130 Vorgänge/s — Phase 3 kostet
  S5 nichts.
- **Störungslauf:** der erste (`…_1333_0b02802_phase-3-stoerung`, Last bis 82) ist **ungültig** — nicht doppelt gewirkt (die Datenbank danach im
  Ruhezustand: jede CommandId höchstens einmal, keine doppelte Version, Reaktion 21 927 = 21 927), sondern nicht abgebaut: zwei Aufträge, deren
  Start-Signal beim harten Abbruch des Prüfdomäne-Hosts verloren ging, kann nur der Prozess-Poll starten; dessen Cursor war vor dem Abbruch noch
  nie geschrieben, also begann er nach dem Neustart bei 0 und liest jeden geänderten Stream ab Version 0 (PF-4) — unter dieser Last kam er in
  10 min nicht durch. Die Abweichung der Reaktions-Zeile (21 648 statt 21 646) ist ein Zählfehler des Treibers: Soll und Ist werden in zwei
  Abfragen gezählt, während der Host noch abbaut. Der zweite (`…_phase-3-stoerung-2`, Last 8 → 68) ist gültig: 24 699 Vorgänge und 182 Bildpaare
  mit zwei harten Abbrüchen und Redis-Ausfall, alles genau einmal, Abbau 371 s.

**Tabelle Messserver** (Hetzner, §12.6; ohne Fremdlast, Postgres und Redis im Host-Netzwerk — die Läufe vor der Baseline-Spalte liefen
noch über docker-proxy):

<!-- vermessung-tabelle:messserver:anfang -->
| Szenario | Kennzahl | Baseline ([2026-10-26_0539_b1af87e_server-pubsub-basis](../vermessung/ergebnisse/2026-10-26_0539_b1af87e_server-pubsub-basis/)) | Phase 4 ([2026-10-26_1841_a2b812b_server-pubsub-phase4](../vermessung/ergebnisse/2026-10-26_1841_a2b812b_server-pubsub-phase4/)) | Phase 5 ([2026-10-28_0358_ad66d61_server-pubsub-phase5](../vermessung/ergebnisse/2026-10-28_0358_ad66d61_server-pubsub-phase5/)) | Phase 6 ([2026-10-28_0536_4f43172_server-pubsub-phase6](../vermessung/ergebnisse/2026-10-28_0536_4f43172_server-pubsub-phase6/)) | Phase 6 / Baseline |
|---|---|---:|---:|---:|---:|---:|
| L1 Leerlauf Beispieldomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 45.2 | – | – | – | – |
| L1 Leerlauf Beispieldomäne | Queries im Nachlauf (1) | 716 | 1491 | 1491 | 1469 | 2.05× |
| L1 Leerlauf Beispieldomäne | Transaktionen (/s) | 0.6 | 0 | 0 | 2.9 | 5.11× |
| L1 Leerlauf Beispieldomäne | Queries (/s) | 5.2 | 7.1 | 7.1 | 7.1 | 1.37× |
| L1 Leerlauf Beispieldomäne | CPU Host (% eines Kerns) | 2.5 | 2.1 | 2 | 2 | 0.78× |
| L1 Leerlauf Beispieldomäne | RSS Host (max.) (MB) | 1314 | 1118 | 1263 | 1242 | 0.95× |
| L1 Leerlauf Beispieldomäne | RSS Host am Fensterende (MB) | 1314 | 1095 | 1263 | 1242 | 0.95× |
| L1 Leerlauf Beispieldomäne | RSS Host Höchststand des Laufs (MB) | 1315 | 1119 | 1405 | 1242 | 0.94× |
| L1 Leerlauf Beispieldomäne | RSS am Ende / Höchststand (1) | 1 | 1 | 0.9 | 1 | 1× |
| L1 Leerlauf Beispieldomäne | Aktive Actors am Fensterende (1) | 10267 | 5064 | 5068 | 4992 | 0.49× |
| L1 Leerlauf Beispieldomäne | Aktive Actors, Höchststand der Fensterenden (1) | 10267 | 5064 | 5068 | 4992 | 0.49× |
| L5 Leerlauf Prüfdomäne | Nachlauf bis alle Konsumenten am Kopf (s) | 19.1 | 2 | 0 | 26.1 | 1.37× |
| L5 Leerlauf Prüfdomäne | Queries im Nachlauf (1) | 1777 | 257 | 0 | 320 | 0.18× |
| L5 Leerlauf Prüfdomäne | Transaktionen (/s) | 0.1 | 0 | 2.3 | 0 | 0× |
| L5 Leerlauf Prüfdomäne | Queries (/s) | 2.6 | 5.3 | 5.9 | 5.9 | 2.23× |
| L5 Leerlauf Prüfdomäne | CPU Host (% eines Kerns) | 1.6 | 1.3 | 1.1 | 1.1 | 0.73× |
| L5 Leerlauf Prüfdomäne | RSS Host (max.) (MB) | 985 | 654 | 907 | 797 | 0.81× |
| L5 Leerlauf Prüfdomäne | RSS Host am Fensterende (MB) | 985 | 652 | 907 | 797 | 0.81× |
| L5 Leerlauf Prüfdomäne | RSS Host Höchststand des Laufs (MB) | 1212 | 778 | 988 | 987 | 0.81× |
| L5 Leerlauf Prüfdomäne | RSS am Ende / Höchststand (1) | 0.8 | 0.8 | 0.9 | 0.8 | 0.99× |
| L5 Leerlauf Prüfdomäne | Aktive Actors am Fensterende (1) | 18436 | 9507 | 9528 | 9458 | 0.51× |
| L5 Leerlauf Prüfdomäne | Aktive Actors, Höchststand der Fensterenden (1) | 18436 | 9507 | 9528 | 9458 | 0.51× |
| S1 Bildeingang Host, Stoß | angenommen (Paare/s) | 7031 | 7049 | 9241 | 8192 | 1.17× |
| S1 Bildeingang Host, Stoß | sichtbar im Fenster (Paare/s) | 0 | 0 | 0 | 0 | – |
| S1 Bildeingang Host, Stoß | Durchsatz bis alles sichtbar (Paare/s) | 61.9 | 159 | 162 | 158 | 2.56× |
| S1 Bildeingang Host, Stoß | Rückstand am Fensterende (Vorgänge) | 600 | 600 | 600 | 600 | 1× |
| S1 Bildeingang Host, Stoß | Abbauzeit (s) | 9.6 | 3.7 | 3.6 | 3.7 | 0.39× |
| S1 Bildeingang Host, Stoß | Latenz p50 (ms) | 5939 | 2606 | 2555 | 2745 | 0.46× |
| S1 Bildeingang Host, Stoß | Latenz p99 (ms) | 9610 | 3694 | 3640 | 3726 | 0.39× |
| S1 Bildeingang Host, Stoß | Transaktionen je Vorgang (1) | 22.6 | 9.7 | 9.6 | 9.7 | 0.43× |
| S1 Bildeingang Host, Stoß | Queries je Vorgang (1) | 171 | 89.8 | 89.8 | 90.5 | 0.53× |
| S1 Bildeingang Host, Stoß | Transaktionen (/s) | 1395 | 1524 | 1525 | 1494 | 1.07× |
| S1 Bildeingang Host, Stoß | Queries (/s) | 10575 | 14164 | 14238 | 13886 | 1.31× |
| S1 Bildeingang Host, Stoß | CPU Host (% eines Kerns) | 623 | 841 | 841 | 855 | 1.37× |
| S1 Bildeingang Host, Stoß | RSS Host (max.) (MB) | 1211 | 915 | 861 | 1103 | 0.91× |
| S1 Bildeingang Host, Stoß | RSS Host am Fensterende (MB) | 1067 | 881 | 861 | 1006 | 0.94× |
| S1 Bildeingang Host, Stoß | RSS Host Höchststand des Laufs (MB) | 1211 | 915 | 861 | 1103 | 0.91× |
| S1 Bildeingang Host, Stoß | RSS am Ende / Höchststand (1) | 0.9 | 1 | 1 | 0.9 | 1.04× |
| S1 Bildeingang Host, Stoß | Aktive Actors am Fensterende (1) | 2254 | 1051 | 1043 | 990 | 0.44× |
| S1 Bildeingang Host, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | 2254 | 1051 | 1043 | 990 | 0.44× |
| S3-64 Sammelvorgang heiß, 64 Sender | angenommen (Teile/s) | 3661 | 5050 | 5083 | 4593 | 1.25× |
| S3-64 Sammelvorgang heiß, 64 Sender | sichtbar im Fenster (Teile/s) | 573 | 667 | 508 | 740 | 1.29× |
| S3-64 Sammelvorgang heiß, 64 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1341 | 1469 | 1392 | 1668 | 1.24× |
| S3-64 Sammelvorgang heiß, 64 Sender | Rückstand am Fensterende (Vorgänge) | 1687 | 1736 | 1800 | 1678 | 0.99× |
| S3-64 Sammelvorgang heiß, 64 Sender | Abbauzeit (s) | 0.9 | 1 | 1 | 0.8 | 0.81× |
| S3-64 Sammelvorgang heiß, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0 | – | – | – | – |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p50 (ms) | 591 | 667 | 696 | 513 | 0.87× |
| S3-64 Sammelvorgang heiß, 64 Sender | Latenz p99 (ms) | 962 | 968 | 1036 | 752 | 0.78× |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen je Vorgang (1) | 1 | 0.6 | 0.6 | 0.6 | 0.59× |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries je Vorgang (1) | 16.7 | 10.9 | 10.9 | 10.8 | 0.65× |
| S3-64 Sammelvorgang heiß, 64 Sender | Transaktionen (/s) | 1111 | 789 | 720 | 844 | 0.76× |
| S3-64 Sammelvorgang heiß, 64 Sender | Queries (/s) | 18943 | 14467 | 12824 | 15871 | 0.84× |
| S3-64 Sammelvorgang heiß, 64 Sender | CPU Host (% eines Kerns) | 290 | 478 | 460 | 246 | 0.85× |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host (max.) (MB) | 1305 | 1107 | 1332 | 1242 | 0.95× |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host am Fensterende (MB) | 1305 | 1107 | 1332 | 1242 | 0.95× |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1305 | 1107 | 1405 | 1242 | 0.95× |
| S3-64 Sammelvorgang heiß, 64 Sender | RSS am Ende / Höchststand (1) | 1 | 1 | 0.9 | 1 | 1× |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors am Fensterende (1) | 10267 | 5064 | 5068 | 4992 | 0.49× |
| S3-64 Sammelvorgang heiß, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 10267 | 5064 | 5068 | 4992 | 0.49× |
| S3-8 Sammelvorgang heiß, 8 Sender | angenommen (Teile/s) | 1066 | 1305 | 1346 | 1361 | 1.28× |
| S3-8 Sammelvorgang heiß, 8 Sender | sichtbar im Fenster (Teile/s) | 906 | 1172 | 1193 | 1188 | 1.31× |
| S3-8 Sammelvorgang heiß, 8 Sender | Durchsatz bis alles sichtbar (Teile/s) | 1064 | 1260 | 1316 | 1342 | 1.26× |
| S3-8 Sammelvorgang heiß, 8 Sender | Rückstand am Fensterende (Vorgänge) | 301 | 204 | 227 | 254 | 0.84× |
| S3-8 Sammelvorgang heiß, 8 Sender | Abbauzeit (s) | 0 | 0.1 | 0 | 0 | 5.67× |
| S3-8 Sammelvorgang heiß, 8 Sender | Prozess-Ende nach dem Abbau (s) | 0 | – | – | – | – |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p50 (ms) | 13.4 | 23 | 27.8 | 13.1 | 0.98× |
| S3-8 Sammelvorgang heiß, 8 Sender | Latenz p99 (ms) | 48.7 | 89 | 111 | 54.7 | 1.12× |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen je Vorgang (1) | 1.6 | 1.5 | 1.5 | 1.5 | 0.96× |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries je Vorgang (1) | 17.5 | 11.4 | 11.4 | 12.2 | 0.69× |
| S3-8 Sammelvorgang heiß, 8 Sender | Transaktionen (/s) | 1427 | 1683 | 1728 | 24.6 | 0.02× |
| S3-8 Sammelvorgang heiß, 8 Sender | Queries (/s) | 15966 | 13093 | 13460 | 200 | 0.01× |
| S3-8 Sammelvorgang heiß, 8 Sender | CPU Host (% eines Kerns) | 361 | 468 | 315 | 9.9 | 0.03× |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host (max.) (MB) | 1290 | 1106 | 1397 | 1241 | 0.96× |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host am Fensterende (MB) | 1290 | 1106 | 1397 | 1240 | 0.96× |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1290 | 1106 | 1397 | 1241 | 0.96× |
| S3-8 Sammelvorgang heiß, 8 Sender | RSS am Ende / Höchststand (1) | 1 | 1 | 1 | 1 | 1× |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors am Fensterende (1) | 6266 | 3063 | 3067 | 2991 | 0.48× |
| S3-8 Sammelvorgang heiß, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6266 | 3063 | 3067 | 2991 | 0.48× |
| S5-1 Prüfdomäne verteilt, 1 Sender | angenommen (Vorgänge/s) | 405 | 387 | 397 | 448 | 1.11× |
| S5-1 Prüfdomäne verteilt, 1 Sender | sichtbar im Fenster (Vorgänge/s) | 396 | 379 | 385 | 429 | 1.08× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 404 | 385 | 395 | 448 | 1.11× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Rückstand am Fensterende (Vorgänge) | 63 | 68 | 92 | 131 | 2.08× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Abbauzeit (s) | 0 | 0 | 0 | 0 | 0.67× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p50 (ms) | 11.6 | 8.3 | 8.1 | 7.2 | 0.62× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Latenz p99 (ms) | 28.8 | 25.3 | 26 | 16.2 | 0.56× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen je Vorgang (1) | 7.1 | 5.4 | 5.4 | 5.4 | 0.76× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries je Vorgang (1) | 35.3 | 46.5 | 46.8 | 46 | 1.3× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Transaktionen (/s) | 2800 | 126 | 127 | 2305 | 0.82× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Queries (/s) | 13906 | 1092 | 1101 | 19719 | 1.42× |
| S5-1 Prüfdomäne verteilt, 1 Sender | CPU Host (% eines Kerns) | 369 | 28.4 | 28 | 362 | 0.98× |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host (max.) (MB) | 1005 | 778 | 874 | 853 | 0.85× |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host am Fensterende (MB) | 974 | 654 | 874 | 851 | 0.87× |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS Host Höchststand des Laufs (MB) | 1089 | 778 | 874 | 853 | 0.78× |
| S5-1 Prüfdomäne verteilt, 1 Sender | RSS am Ende / Höchststand (1) | 0.9 | 0.8 | 1 | 1 | 1.12× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors am Fensterende (1) | 6436 | 3507 | 3528 | 3458 | 0.54× |
| S5-1 Prüfdomäne verteilt, 1 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 6436 | 3507 | 3528 | 3458 | 0.54× |
| S5-64 Prüfdomäne verteilt, 64 Sender | angenommen (Vorgänge/s) | 2664 | 7532 | 7403 | 8015 | 3.01× |
| S5-64 Prüfdomäne verteilt, 64 Sender | sichtbar im Fenster (Vorgänge/s) | 1977 | 82.8 | 101 | 80.2 | 0.04× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 2326 | 302 | 306 | 224 | 0.1× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Rückstand am Fensterende (Vorgänge) | 774 | 2967 | 2959 | 2970 | 3.84× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Abbauzeit (s) | 0.2 | 9.5 | 9.4 | 13 | 79.35× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Prozess-Ende nach dem Abbau (s) | 0 | – | – | – | – |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p50 (ms) | 177 | 5210 | 5229 | 5614 | 31.79× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Latenz p99 (ms) | 557 | 9590 | 9425 | 13031 | 23.39× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p50 (ms) | 3.7 | – | – | – | – |
| S5-64 Prüfdomäne verteilt, 64 Sender | Reaktion p99 (ms) | 105 | – | – | – | – |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen je Vorgang (1) | 0.8 | 3.6 | 3.6 | 3.8 | 4.98× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries je Vorgang (1) | 26.5 | 37 | 36.8 | 37.8 | 1.42× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Transaktionen (/s) | 1593 | 1060 | 1095 | 836 | 0.52× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Queries (/s) | 55447 | 10813 | 11085 | 8310 | 0.15× |
| S5-64 Prüfdomäne verteilt, 64 Sender | CPU Host (% eines Kerns) | 484 | 399 | 399 | 272 | 0.56× |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host (max.) (MB) | 1122 | 776 | 988 | 987 | 0.88× |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host am Fensterende (MB) | 1122 | 653 | 900 | 858 | 0.76× |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS Host Höchststand des Laufs (MB) | 1122 | 778 | 988 | 987 | 0.88× |
| S5-64 Prüfdomäne verteilt, 64 Sender | RSS am Ende / Höchststand (1) | 1 | 0.8 | 0.9 | 0.9 | 0.87× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors am Fensterende (1) | 18436 | 9507 | 9528 | 9458 | 0.51× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 18436 | 9507 | 9528 | 9458 | 0.51× |
| S5-8 Prüfdomäne verteilt, 8 Sender | angenommen (Vorgänge/s) | 1385 | 1994 | 1962 | 2014 | 1.45× |
| S5-8 Prüfdomäne verteilt, 8 Sender | sichtbar im Fenster (Vorgänge/s) | 1281 | 360 | 328 | 351 | 0.27× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Durchsatz bis alles sichtbar (Vorgänge/s) | 1374 | 306 | 311 | 243 | 0.18× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Rückstand am Fensterende (Vorgänge) | 226 | 2459 | 2499 | 2477 | 10.96× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Abbauzeit (s) | 0 | 8.3 | 8.1 | 10.9 | 639.35× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p50 (ms) | 27.3 | 3671 | 4166 | 4066 | 148.73× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Latenz p99 (ms) | 57.1 | 8514 | 7987 | 10860 | 190.09× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen je Vorgang (1) | 2.1 | 3.7 | 3.8 | 3.9 | 1.8× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries je Vorgang (1) | 31.9 | 38.8 | 39.1 | 39.9 | 1.25× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Transaktionen (/s) | 2698 | 1109 | 1166 | 915 | 0.34× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Queries (/s) | 40236 | 11722 | 11980 | 9464 | 0.24× |
| S5-8 Prüfdomäne verteilt, 8 Sender | CPU Host (% eines Kerns) | 627 | 323 | 362 | 237 | 0.38× |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host (max.) (MB) | 1045 | 745 | 913 | 903 | 0.86× |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host am Fensterende (MB) | 1045 | 638 | 874 | 849 | 0.81× |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS Host Höchststand des Laufs (MB) | 1089 | 778 | 913 | 903 | 0.83× |
| S5-8 Prüfdomäne verteilt, 8 Sender | RSS am Ende / Höchststand (1) | 1 | 0.8 | 1 | 0.9 | 0.98× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors am Fensterende (1) | 12436 | 6507 | 6528 | 6458 | 0.52× |
| S5-8 Prüfdomäne verteilt, 8 Sender | Aktive Actors, Höchststand der Fensterenden (1) | 12436 | 6507 | 6528 | 6458 | 0.52× |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p50 (ms) | – | 2.7 | 2.7 | 2.2 | – |
| S5-64 Prüfdomäne verteilt, 64 Sender | Nachschub p99 (ms) | – | 185 | 253 | 41.2 | – |
| S2 Bildeingang mit Python-Worker, Stoß | angenommen (Paare/s) | – | – | 7103 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | sichtbar im Fenster (Paare/s) | – | – | 0 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | Durchsatz bis alles sichtbar (Paare/s) | – | – | 56.9 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | Rückstand am Fensterende (Vorgänge) | – | – | 200 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | Abbauzeit (s) | – | – | 3.5 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p50 (ms) | – | – | 2562 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | Latenz p99 (ms) | – | – | 3487 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen je Vorgang (1) | – | – | 11.4 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | Queries je Vorgang (1) | – | – | 106 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | Transaktionen (/s) | – | – | 643 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | Queries (/s) | – | – | 5988 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Host (% eines Kerns) | – | – | 210 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | CPU Worker (% eines Kerns) | – | – | 258 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host (max.) (MB) | – | – | 802 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host am Fensterende (MB) | – | – | 625 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | RSS Host Höchststand des Laufs (MB) | – | – | 802 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | RSS am Ende / Höchststand (1) | – | – | 0.8 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors am Fensterende (1) | – | – | 645 | – | – |
| S2 Bildeingang mit Python-Worker, Stoß | Aktive Actors, Höchststand der Fensterenden (1) | – | – | 645 | – | – |
| S4 Datensatz einfrieren | Durchsatz bis alles sichtbar (Paare/s) | – | – | 2823 | – | – |
| S4 Datensatz einfrieren | Dauer Range aufnehmen (s) | – | – | 0.1 | – | – |
| S4 Datensatz einfrieren | Dauer Einfrieren (s) | – | – | 0.1 | – | – |
| S4 Datensatz einfrieren | Transaktionen je Vorgang (1) | – | – | 0 | – | – |
| S4 Datensatz einfrieren | Queries je Vorgang (1) | – | – | 3.1 | – | – |
| S4 Datensatz einfrieren | Transaktionen (/s) | – | – | 53.1 | – | – |
| S4 Datensatz einfrieren | Queries (/s) | – | – | 8402 | – | – |
| S4 Datensatz einfrieren | Aktive Actors am Fensterende (1) | – | – | 1055 | – | – |
| S4 Datensatz einfrieren | Aktive Actors, Höchststand der Fensterenden (1) | – | – | 1055 | – | – |
<!-- vermessung-tabelle:messserver:ende -->

**Messserver, erste Läufe (2026-10-15, `a600166` = Phase-3-Code; Ordner `…_server-baseline`, `…_server-baseline-stoerung`; nicht in der
Tabelle oben, weil der Rechner ein anderer ist):** ohne Fremdlast, gegen Phase 3 auf dem MacBook (unter Fremdlast). Die **Arbeit je Vorgang ist
gleich** (Transaktionen je Vorgang 0,95–1,07 ×, Queries 0,77–1,0 ×), die Verhältnisse der Phasen bleiben also gültig. Ende zu Ende 2–3 × schneller
(S1 52 Paare/s, S2 47, S3 218–228 Teile/s, S5 192–307 Vorgänge/s), S5-64 6,3 × (191 statt 30; der Mac-Lauf litt besonders unter der Fremdlast),
Latenzen p50 0,23–0,51 ×; der Host nutzt dabei 7–10 Kerne statt 3. S4 ist 0,7–0,9 × (1 282 Paare/s; einzelne kurze Abfragen, hier zählt der
schnellere Einzelkern des M4). Die Decke bleibt dieselbe: die Schreibseite nimmt in S5-64 5 675 Commands/s an, Ende zu Ende kommen 191 Vorgänge/s
durch (Konsum, PF-4). Leerlauf L5 zeigt 377 Transaktionen/s und 2 100 Queries/s (Prozess-Poll, PF-4). Unter Last steht `docker-proxy` (Userland-Proxy
der Ports auf 127.0.0.1) bis 30 % CPU — jede DB-Verbindung läuft durch ihn. Störungslauf: 56 847 Vorgänge mit zwei harten Abbrüchen und
Redis-Ausfall, alles genau einmal, Abbau 419 s.

**Host-Netzwerk statt docker-proxy (2026-10-15, `ab58b0e`, A-B-B-A, Ordner `…_server-netz-{proxy,host}-{1,2}`):** seitdem laufen Postgres und die
Redis des Messservers im Host-Netzwerk (`docker-compose.messserver.yml`). Gegen den Proxy, Mittel der beiden B- gegen die beiden A-Läufe: Durchsatz
Ende zu Ende 1,01–1,10 × (Median ≈ 1,06 ×; S5-1 1,10 ×, S5-64 1,07 ×, S3-64 1,06 ×), Latenz p50 0,90–0,94 × (S3-8 1,04 ×, im Rauschen), Arbeit je
Vorgang unverändert. Ein kleiner, gleichgerichteter Effekt — die Decke im Konsum (PF-4) bleibt. Die Server-Läufe vorher (`…_server-baseline`)
liefen noch über den Proxy.

**PF-4 auf dem Messserver (2026-10-15, `ffaeaea`, Spalte „PF-4“ der Server-Tabelle):** Leerlauf L1 351 → 2,5 Queries/s und 24 → 3,7 % CPU (der
Prozess-Poll las vorher jeden geänderten Stream ab 0); S5-1/S5-64 1,17/1,16 ×, S5-64 Queries je Vorgang 0,84 ×; S1–S4 ohne Regression, Kosten je
Vorgang gleich. L5 bleibt bei ≈ 1 900 Queries/s: dort holt die Reaktion ihren Rückstand aus S5-64 auf (PF-12), nicht der Dirigent. **Warmlauf:** seit
diesem Lauf wartet der Treiber nach dem ersten Start eines Hosts 30 s (`warmlauf_s` im Ergebnis). Ohne ihn maß der erste Block kalten JIT-Code
(S3-8 −28 %, mehr CPU bei gleicher Arbeit); die Läufe davor bekamen diese Zeit zufällig, weil `/health` auf einer frischen DB erst grün wurde,
als der alte Prozess-Poll nach 30 s die Event-Tabellen anlegte. Läufe ohne diese Zufalls-Wartezeit (`…_1721_…`, A-B-B-A `…_server-pf-4-ab-*`)
sind für S1/S3-8 deshalb nicht vergleichbar.

**PF-12 auf dem Messserver (2026-10-15, `5bda1bb`, Spalte „PF-12“):** Leerlauf L5 2 292 → 2,5 Queries/s und 145 → 3,4 % CPU gegen die
Server-Baseline — der Rest war der Emittenten-Poll (jeder geänderte Stream bekam einen Cursor), der serielle Korrelations-Router und nicht
zusammengefasste Emittenten-Weckungen (docs/13 §13.6). S5-64 1,69 ×, Latenz p50 0,26 ×; S5-8 Latenz 0,38 ×; S1–S4 0,9–1,14 ×. Zwei Störungsläufe
grün (je ≈ 19 800 Vorgänge, Rückstand am Ende 715–908 statt 32 763, Abbau 11 s statt 316 s). Offen: Reaktion p99 2 s unter S5-64 und S3-64
0,9 × (PF-13).

**Weniger Datenbankarbeit je Vorgang (2026-10-15, Messserver, Spalten „Ohne Reset“ und „Korreliert lesen“):**
- *Ohne Verbindungs-Reset* (`3011a1e`): Npgsql setzt eine Verbindung bei der Rückgabe an den Pool nicht mehr per `DISCARD ALL` zurück (ein
  Drittel aller Anweisungen). Wirkung klein: 1,0–1,1 × in den Szenarien (S1 1,11 ×, S5-8 1,08 ×) — die Anweisung war billig.
- *Korreliert lesen* (`6e361a6`): der Prozess-Manager liest in seinen Ziel-Streams nur die Events seiner Korrelation. Je Vorgang gelesene
  Zeilen: S3 2 007 → 6, S5-8 714 → 104, S5-64 1 106 → 163; DB-Zeit fürs Lesen S3 9,1 → 0,8 ms, S5 4,3–6,0 → 2,4–3,2 ms. Durchsatz gegen „Ohne
  Reset“: S3-64 1,98 ×, S3-8 1,63 ×, S5-64 1,16 ×, S5-8 1,10 ×; Host-CPU in S5 0,66–0,81 ×. Störungslauf grün (29 794 Vorgänge).
  Gegen die Server-Baseline damit S3-64 1,85 ×, S3-8 1,77 ×, S5-64 1,94 ×, S5-8 1,21 ×.

**Schlanker Schreibweg (Umbau-Phase 4, 2026-10-15, Messserver, Spalten „Inbox-Tabelle“ und „Schnell anhängen“):**
- *Inbox als Tabelle* (`aef3d46`, `…_2152_…_server-inbox`, Störungslauf `…_2156_…` grün, 28 004 Vorgänge): eine Wirkung schreibt keine
  Marke mehr, die Quittung geht als eine Anweisung je Group-Commit in `cqrs_inbox`. Je Vorgang (pg_stat_statements, Nenner = Vorgänge des
  Blocks) S5-8: Event-Inserts 11,5 → 6,9, Inbox-Inserts 0 → 1,2, DB-Zeit 3,6 → 2,9 ms; S5-64 Event-Inserts 12,0 → 6,7, DB-Zeit 4,3 → 3,3 ms;
  S3-64 8 → 5. Durchsatz gegen „Korreliert lesen“ (je ein Lauf): S5 1,05–1,13 ×, S1 1,08 ×, S3 0,94–0,96 ×.
- *Schnell anhängen* (`cf30aa0`, Martens `EventAppendMode.Quick`, `…_2207_…_server-quick`, Störungslauf `…_2210_…` grün, 28 502 Vorgänge):
  der Vorab-Read der Stream-Köpfe und die Sequenz-Abfrage entfallen (S5-8 je Vorgang 4,9 + 1,5 → 0, S1 6 + 16,7 → 0); Queries je Vorgang S5
  0,81–0,86 ×, S3 0,78 ×, S1 0,89 × gegen „Inbox-Tabelle“.
- *A-B-B-A* gegen `3483a31` (vor beiden; Ordner `…_server-schreibweg-ab-{a1,b1,b2,a2}`, S1/S3/S4/S5, alle gültig und grün), Mittel B gegen
  Mittel A: Durchsatz bis alles sichtbar S3-8 1,18 ×, S3-64 1,17 ×, S5-1 1,08 ×, S5-8 1,06 ×, S5-64 1,06 ×, S1 1,04 ×, S4 1,01 ×; Queries je
  Vorgang 0,78–0,89 ×; Latenz p50 S3 0,83–0,87 ×, S5-1 0,88 ×. Unter Höchstlast steigt die Latenz in S5-8/S5-64 (1,32 ×): die Schreibseite nimmt
  mehr an (S5-8 1 006 bzw. 729 statt 554 bzw. 586 Vorgänge/s), die Warteschlange bis „sichtbar“ wird länger.
- *Leerlauf:* L1 29 Queries/s (wie „Inbox-Tabelle“), L5 41 → 86 Queries/s, CPU 7–10 %. Seit „Korreliert lesen“ über dem E5-Ziel (< 10); in L5 sind
  es der Reaktions-Adapter (Cursor und Stream-Rest je Posten, je 470 im Fenster) — der Nachlauf der Reaktion aus S5-64 (PF-13), nicht der
  Schreibweg; in L1 lesen Prozess-Poll (Offen-Liste, Seite, Marke, vorhandene Streams, je 50–63) und Batch-Leser (gesetzte Sequenz 102, neue
  Partitions-Checkpoints 23) das Log der Blöcke davor nach („Ohne Reset“: 52 Anweisungen im Fenster, jetzt 626). Nicht weiter untersucht.

**Prozess-Manager liest nur, was er nicht schon weiß (2026-10-16, Messserver, `6a437ba`, Spalte „Prozess-Manager“, docs/04 §4.5):** gezielte
Weckungen (Router mit Event, Send und Funktions-Ergebnis mit Vorgang), „gelesen, nichts da“, frischer Vorgang ohne Store-Laden, Kopf-Zustand
des Manager-Logs im Speicher; dazu der Offen-Index als Beilage im Append von Start und Ende. Je Vorgang (pg_stat_statements) S5-8:
Event-Reads 23,1 → 10,2, Dokument-Reads 4,8 → 2,9, BEGIN/COMMIT 10,8 → 4,2, DB-Zeit 3,0 → 1,6 ms; S5-64 Event-Reads 22,8 → 7,9, BEGIN/COMMIT
8,7 → 2,1, DB-Zeit 3,5 → 1,7 ms; S1 Event-Reads 64 → 36, Dokument-Reads 57 → 39. Durchsatz bis alles sichtbar gegen „Schnell anhängen“
(je ein Lauf): S5-64 457 → 1 246 Vorgänge/s (2,7 ×), S5-8 451 → 1 030 (2,3 ×), S5-1 1,0 × (Latenz p50 0,71 ×), S1 1,44 ×, S2 1,42 ×;
Transaktionen je Vorgang S5-64 4,4 → 1,1. A-B-B-A gegen `cf30aa0` (Ordner `…_server-pm-ab-{a1,b1,b2,a2}`, S1/S3/S4, gültig, grün): S1 1,40 ×,
S3-64 1,06 ×, S3-8 0,96 × (die Grenze ist der einzelne Schreiber), S4 1,02 × (die 0,81 × im Einzellauf waren Rauschen: S4 dauert 0,4 s);
Queries je Vorgang 0,75 ×, Transaktionen S3 0,41–0,50 ×. Zwei Störungsläufe grün (`…_0030_…`, `…_0033_…`: 54 079 und 56 337 Vorgänge statt
≈ 28 500; S5-p99 37–41 s, im Band der Läufe seit PF-12 von 36–55 s — die 12 s von „Schnell anhängen“ waren der Ausreißer). Leerlauf L5
86 → 110 Queries/s (mehr angenommen, mehr Nachlauf der Reaktion, PF-14).

**Aggregat-Bündel (2026-10-16, Messserver, `2390f1e`, Spalte „Bündel“, docs/03 §3.1):** ein Aggregat-Actor sammelt Commands, während sein
Commit läuft, entscheidet sie nacheinander im Speicher und hängt sie in einem Append an (höchstens 64, `Schreibpfad.BuendelGroesse`). Gegen
„Prozess-Manager“ (je ein Lauf): S3-64 541 → 1 350 Teile/s (2,5 ×), S3-8 486 → 859 (1,8 ×; Latenz p50 1 400 → 17 ms, p99 2 035 → 31 ms — kein
Rückstau mehr am heißen Sammelvorgang), S3 Transaktionen je Vorgang 2,2 → 1,0 bzw. 3,0 → 1,7; S1/S2/S4/S5 unverändert. A-B-B-A gegen
`6a437ba` (Ordner `…_server-bu-ab-{a1,b1,b2,a2}`, S3/S5, gültig, grün): S3-8 1,72 ×, S3-64 2,29 ×, S5-1/8/64 1,01/1,01/1,07 × (die 0,91 × von
S5-8 im Einzellauf waren Rauschen). Zwei Störungsläufe grün (`…_0150_…`, `…_0153_…`: 55 307 und 57 016 Vorgänge; S5-p99 4,5 s bzw. 37 s — das
Band hängt am Abbruchzeitpunkt). Gegen die Server-Baseline jetzt S3-64 5,9 ×, S3-8 3,7 ×, S5-64 6,2 ×, S5-8 2,6 ×, S1 1,54 ×.

**Leerlauf: Nachlauf statt Grundlast (PF-14, 2026-10-16, Messserver, `7a1f9f9`, Spalte „Nachlauf“, docs/04 §4.1.3):**
- *Diagnose* (`--art leerlauf`, `3a1355b`, Zeitreihe in 10-s-Scheiben, kein Messlauf): ohne Last (frischer Host, leere DB) 2,5 Queries/s, alle
  30 s eine Scheibe mit 10 (Batch-Poll: 40 Partitionen je gesetzte Sequenz und Seite). Nach den Blöcken, L1: bei t ≈ 20 s 43 Queries/s — der
  Prozess-Poll liest 62 Seiten (das ganze Log der Blöcke, 31 000 Events) in ≈ 5 s; seine Marke stand seit Beginn der Last, weil Weckungen an die
  ausgelasteten Manager unbestätigt blieben (G-8); bei t ≈ 50 s 12 (die Batch-Leser schreiben ihre Position über fremde Events fest); danach
  300 s lang 2,4 und 10,7 im Wechsel (Mittel 5,2), CPU 2–3,5 %. L5: t ≈ 10 s 360 Queries/s (3 575 Einzel-Reads des Korrelations-Routers — Signale
  der schon beendeten S5-64-Vorgänge), t ≈ 20 s 156 (Prozess-Poll 130 Seiten mit 64 000 Events, Emittenten-Poll 450 Weckungen mit Cursor- und
  Stream-Read), danach 2,4–3,0 Queries/s und 1–2 % CPU. Nach einem Neustart auf demselben Bestand eine Scheibe mit 8–16 Queries/s, dann dasselbe
  Bild. **Es bleibt nichts:** das alte 15-s-Fenster, 5 s nach dem letzten Block, traf den ersten Poll-Takt nach der Last — seit „Korreliert
  lesen“ baut sich die Last schneller ab, und das Fenster fiel regelmäßig darauf (vorher je nach Lage daneben: 2,5 Queries/s).
- *Messverfahren:* erst der Nachlauf, bis alle Poll-Cursor und Projektions-Positionen am Kopf des Logs stehen (höchstens 180 s), dann 60 s
  (zwei Poll-Perioden). Neue Kennzahlen „Nachlauf bis alle Konsumenten am Kopf“ und „Queries im Nachlauf“. Die L1/L5-Werte der Spalten davor
  stammen aus dem alten Fenster und sind nur der Richtung nach vergleichbar.
- *Weniger je Takt* (`f55494b`): gesetzte Sequenz und „steht dahinter etwas?“ in einer Abfrage, ohne Neues entfällt die Seite — ein
  Batch-Poll-Takt kostet 40 statt 80 Abfragen, Prozess- und Emittenten-Poll je eine statt zwei.
- *Gemessen* (`…_0309_7a1f9f9_server-nachlauf`, gültig, grün): L1 3,8 Queries/s und 2,6 % CPU (Fenster in „Bündel“: 29,6 und 7,4; Baseline
  351 und 24,5), L5 2,6 Queries/s und 1,5 % (113 und 10,2; Baseline 2 293 und 145) — **E5-Ziel Leerlauf (< 10 Queries/s, < 5 % CPU) erreicht**.
  Nachlauf L1 48 s mit 585 Queries, L5 15 s mit 3 734. Lastblöcke gegen „Bündel“ (je ein Lauf) 0,87–1,16 ×, Queries je Vorgang 0,91–1,01 × —
  im Band der Läufe desselben Codes (S3-64 1 215–1 350 Teile/s, Reaktion p99 S5-64 98–348 ms in „Bündel“ und seinen A-B-B-A-Läufen). Zwei
  Störungsläufe grün (`…_0315_…`, `…_0318_…`: 50 486 und 47 908 Vorgänge). RSS am Ende weiter 0,9–1,0 des Höchststands (passiviert wird erst
  nach 120–600 s, PF-5).

**Horizont der Vollständigkeit und eine Uhr (G-9, G-10; 2026-10-16, Messserver, `a78c21d`, Spalte „Horizont + Uhr“):** gültig,
Exactly-once grün; zwei Störungsläufe grün (`…_1628_…`, `…_1631_…`: 57 192 und 53 555 Vorgänge). Durchsatz bis alles sichtbar gegen „Nachlauf“ (je
ein Lauf): S1 1,13 ×, S5-1 1,08 ×, S2 und S3-64 0,99 ×, S5-8 und S5-64 0,93 ×, S3-8 0,89 ×, S4 0,73 × (S4 dauert 0,3 s) — im Band der Einzelläufe;
Reaktion p99 S5-64 330 → 260 ms. Je Vorgang (`pg_stat_statements`):
- *S1:* Datenbankzeit je Paar 20,3 → 9,5 ms. Die Abfrage der gesetzten Sequenz (Rückwärts-Scan gegen die Uhr, 7,7 ms) entfällt; die
  Partitionsseite kostet 0,6 statt 7,1 ms, weil die Positionen Millisekunden statt ≥ 3 s hinter dem Kopf liegen und der Leser kein Karenz-Fenster
  mehr durchsucht. Neu: Proben und Prüfungen des Horizonts, zusammen 3,0 ms (≈ 0,45 ms je Aufruf, mehr als erwartet — PF-17). Transaktionen je
  Paar 21 → 25: die Proben sind eigene, kurze Transaktionen (ohne Warten auf die Platte) und zählen als COMMIT.
- *Zeitstempel-Regel:* je Event-Insert ≈ 20–35 µs mehr (S5-64 0,33 → 0,48 ms je Vorgang, S3-64 0,22 → 0,38 ms). Die Vermessung läuft als Sim, die
  Regel liest dort je Zeile den Versatz aus `uhr`; in Prod ist der Ausdruck nur die Datenbankzeit (PF-17).
- *Leerlauf:* L1 3,8 → 5,2 Queries/s (eine Probe je Poll), L5 2,6 unverändert — unter dem E5-Ziel; Queries im Nachlauf L5 3 734 → 1 978.

**Vermessung als Art Prod (2026-10-17, Messserver, `0658d0d`, Spalte „Art Prod“):** im Host läuft derselbe Code wie im Betrieb (docs/12 §12.5
oben). Gültig, Exactly-once grün; zwei Störungsläufe grün (`…_server-art-prod-stoerung-1/2`: 55 537 und 52 926 Vorgänge). Gegen „Horizont + Uhr“
(Sim, je ein Lauf): S1–S3 und S5 0,91–1,03 ×; die Zeitstempel-Regel kostet in Prod ≈ 7–10 µs je Event-Insert statt 20–35 µs (S5-64 Datenbankzeit
je Vorgang 1,78 → 1,66 ms, wieder wie vor dem Umbau; S3-64 0,99 → 0,81; S1 9,5 → 8,7). **S4 0,75 ×** (962 Paare/s; Range aufnehmen und Einfrieren je
0,3 statt 0,1/0,2 s), reproduzierbar (vier weitere Läufe 969–1 019, Ordner `…_0658d0d_server-s4-ab-*-<commit>` — trotz des Namens alle mit
`0658d0d` gelaufen, ein Fehler im Aufruf). Ursache: in Sim leert der Abgleich der Leseseite beim Start alle Read-Models
und legt damit deren Tabellen an; in Prod legt Marten sie erst beim ersten Zugriff an — in S4 für vier Dokumenttypen (Tabellen-Inspektion,
Funktionen, `CREATE TABLE`) mitten in der Messung (PF-18). A-B-B-A nur S1/S4 in Sim gegen den Stand vor G-9/G-10 (`7a1f9f9` gegen `a78c21d`,
Ordner `…_server-s4-ab-{a1,b1,b2,a2}`): S4 1 728 / 1 683 / 1 724 / 1 349 Paare/s, S1 88 / 90 / 91 / 85 — der Code von G-9/G-10 kostet S4 nichts.

**Messung mit der Konfiguration von `umgebung.prod.json` (2026-10-17, Messserver, `432c031`, Spalte „Prod-Konfig“):** gültig, Exactly-once grün.
Gegen „Art Prod“ (Einzelknoten, 8 Slots): S1 55,7 statt 91,5 Paare/s (0,61 ×; 2 Slots je Bildfunktion und 1 s Stabilität der Datei-Quelle, Host-CPU
6,7 statt 10 Kerne, Latenz p50 6,2 s), S2 44,6 statt 91 (0,49 ×), S3 und S5 1,00–1,11 ×, Leerlauf unverändert — Consul mit einem Knoten kostet nichts
Messbares. S4 bleibt bei 976 Paaren/s (Range und Einfrieren je 0,3 s), obwohl seit PF-18 kein Schema mehr im Verkehr entsteht (null DDL in S4): die
Erklärung oben (Schema) war falsch. Vermutet: Marten erzeugt und kompiliert den Code eines Dokumenttyps beim ersten Gebrauch zur Laufzeit; in Sim
fasst der Abgleich der Leseseite beim Start jeden Read-Model-Typ an, in Prod geschieht es mitten in S4, das keine Aufwärmrunde hat (PF-19).
**Störungsläufe ungültig** (`…_server-prod-konfig-stoerung-1/2`): nach dem harten Abbruch startet der Host der Beispieldomäne nicht wieder — die
Aktivierung der Broker beim Start läuft in den Timeout, weil Consul den toten Knoten noch als Mitglied führt und der Cluster Aktivierungen auf ihn
legt; der Start versucht es genau einmal und bricht ab (Exit 134). Mit dem Einzelknoten gab es das nicht (P1-3). Seit `edbab04` wiederholen
Aktivieren und Abonnieren der Broker mit Backoff: zwei Störungsläufe mit Consul grün (`…_server-consul-neustart-stoerung-1/2`: 53 019 und 52 756
Vorgänge; beim Host der Prüfdomäne gelang je eine Aktivierung im zweiten Versuch).

**S4 mit Aufwärmrunde (PF-19, 2026-10-17, Messserver, `9188b87`, nur S1/S4, Prod-Konfig):** vor dem gewerteten Datensatz läuft ein eigener einmal
ganz durch (nicht gewertet). S4 1 942 und 1 847 Paare/s, Range 0,1 s, Einfrieren 0,2 s (ohne Aufwärmen in Prod 962–1 019, je 0,3 s; in Sim 1 683–1 728)
— die Verlangsamung war die erste Nutzung nach dem Start. Dass es Martens Code-Erzeugung zur Laufzeit ist, folgt aus dem Unterschied Sim/Prod: in
beiden laufen dieselben Wege von S4 zum ersten Mal; nur in Sim hat der Abgleich der Leseseite die Read-Model-Typen beim Start schon benutzt.
Seitdem misst S4 mit Aufwärmrunde (Spalten davor für S4 nicht vergleichbar).

**Engpass-Sonden (2026-10-17, Messserver, `03b25d0`, Diagnose, nicht in der Tabelle):** neben dem unveränderten Treiber schreibt
`scripts/sonde-engpass.py` die CPU je Prozessgruppe (Host, Postgres-Backends, WAL-Writer, Treiber, Redis, Consul) und je logischem Kern, den
CPU- und IO-Druck des Kernels (PSI „some“: Anteil der Zeit, in der mindestens ein Thread auf CPU bzw. IO wartete), die heißen Host-Threads,
alle 100 ms `pg_stat_activity` (Zustand und Wartegrund je Verbindung), jede Sekunde `pg_stat_wal` und über `dotnet-counters` Threadpool und
Npgsql-Pool mit; `scripts/sonde-engpass.sh --anlass … [--knopf …]` startet S3 und S5 mit drei Wiederholungen und legt Rohdaten und Auswertung je
Abschnitt unter `sonde/` in den Ergebnis-Ordner, `scripts/sonde-auswertung.py --vergleich <ordner …>` stellt Läufe nebeneinander. Je Lauf genau
ein Knopf; Werte sind Mediane über drei Wiederholungen (S3-64 und S5-64 dauern je nur 2–3 s, die Einzelwerte streuen um ± 10 %).
- *Ohne Knopf* (`…_server-sonde-keiner-2`): **S5-64 ist durch die CPU des Rechners begrenzt.** 1 240 Vorgänge/s; Host 7,1 und Postgres-Backends
  5,4 Kerne, in der Spitze 80 % aller logischen Kerne bei CPU-Druck bis 0,26 (auf den sechs P-Kernen laufen beide Hyperthreads; Mittel 68 %,
  0,19). Gerechnet je Vorgang ≈ 5–7 ms Host-CPU und 3,5–5,5 ms Postgres-CPU; bei ≈ 13 nutzbaren Kernen liegt die Decke damit bei ≈ 1 300/s.
  Npgsql-Pool (24 von 100 belegt, keine wartende Anfrage), Threadpool (Warteschlange kurz bis 109), Sperren (keine Verbindung wartet auf eine
  Sperre) und Redis (< 0,1 Kern) sind es nicht. **S3 ist nicht durch ein Betriebsmittel begrenzt** (39–42 % Auslastung, CPU-Druck ≤ 0,07,
  2,9–4,1 aktive Postgres-Verbindungen), sondern durch die Kette am einen heißen Aggregat (siehe Knöpfe). Je Vorgang führt Postgres nur
  0,8–1,7 ms Anweisungen aus (`pg_stat_statements`), verbraucht aber 2,8–5,5 ms CPU — rund zwei Drittel außerhalb der Ausführung.
- *synchronous_commit aus* (`…_sync-aus`, Obergrenze für das Warten aufs fsync): S5-1 288 → 449 (1,56 ×), S3-8 679 → 962 (1,42 ×), S3-64
  1 130 → 1 400 (1,24 ×), Latenz p50 0,6–0,8 × — die seriellen Ketten (ein Sender; der eine Schreiber am heißen Aggregat) warten auf die Platte.
  S5-8 0,97 ×, S5-64 0,75 × (931 statt 1 240): der Eingang nimmt schneller an (bis 8 600/s), die Arbeit bleibt dieselbe, der Abbau dauert länger —
  an der CPU-Decke verschiebt das nur Arbeit nach vorn.
- *Drossel je Ziel 8 → 64* (`…_drossel-64`): **S3-64 1 130 → 2 020 Teile/s (1,79 ×), sichtbar im Fenster 336 → 1 460 (4,3 ×), Latenz p50 793 →
  137 ms**; S3-8 und S5 unverändert (dort bindet die Drossel nicht). Am heißen Sammelvorgang konkurrieren 64 Sender des Eingangs mit den höchstens
  8 Sends des Prozess-Managers um die Bündel des einen Schreibers — der Manager bekam etwa ein Achtel.
- *Max Auto Prepare=20* (`…_auto-prepare-20`, Npgsql bereitet die häufigsten Anweisungen je Verbindung vor): **Postgres-Backend-CPU 0,5–0,7 ×**
  (S5-64 5,4 → 3,1 Kerne, S5-8 4,3 → 2,1, S5-1 2,3 → 1,3); Durchsatz S5-64 1 240 → 1 400 (1,13 ×), S5-8 1,14 ×, S5-1 1,17 ×, S3-8 1,15 ×,
  S3-64 1,11 ×; CPU-Druck in S5-64 0,19 → 0,11. Die Hälfte der Postgres-CPU war Parsen und Planen (PF-20).
- *Drain 4 → 8* (`…_drain-8`, parallele Commit-Schleifen des Group-Commits): S3-8 1,10 ×, S3-64 1,13 ×, S5-1 1,09 ×, S5-8 und S5-64 1,0 ×;
  mehr Verbindungen warten auf WAL (S5-64 1,0 → 2,4), die Last steigt — eine kleine Wirkung nur auf den seriellen Ketten.
- *Driftkontrolle* (`…_2103_…_server-sonde-keiner`, derselbe Code am Ende der Kette): gegen den ersten Lauf ohne Knopf 0,97–1,01 × — die
  Wirkungen oben liegen über der Drift.
- **Folgerung (Decke):** S5-64 steht an der CPU des einen Rechners, den Host und Postgres teilen; die größten Posten sind die Host-CPU je Vorgang
  (5–7 ms, noch nicht aufgeschlüsselt) und das Parsen und Planen in Postgres (PF-20). S3 steht am einen Schreiber des heißen Aggregats: wie viel
  davon der Prozess-Manager bekommt, regelt die Drossel je Ziel (Schritt „Drossel und Bündel“), wie schnell die Kette läuft, das fsync je Commit.
  Die Läufe `…_drossel-64`, `…_auto-prepare-20` und `…_drain-8` tragen „-geaendert“ im Namen: der Knopf änderte den Checkout (`sonde/knopf.txt`).

**Prozess-Fold liest ab dem Ausgang (2026-10-17, Messserver, `61f8e32`, Spalte „Fold ab Hinweis“, docs/04 §4.5):** gültig, Exactly-once grün;
Störungsläufe `…_fold-hinweis-stoerung-2` und `-3` grün (`-1` ungültig: die Prüfabfrage des Treibers sprengte mit einem parallelen Plan das Shared
Memory des Containers, Fehler 53100 — seit `76d3692` ohne parallele Pläne, wie auf `claude/tests-und-messung`). Der korrelierte Read kostet
31–66 µs statt 186–417 µs je Aufruf, sein Anteil an der Datenbankzeit fällt von 29–42 % auf 6–9 %, die Datenbankzeit je Vorgang auf 0,68–0,80 ×
(S3-64 0,97 → 0,71 ms, S5-64 1,74 → 1,19 ms; `pg_stat_statements`, gegen „Prod-Konfig“). Durchsatz gegen „Prod-Konfig“ (je ein Lauf) 0,85–1,10 ×,
S4 1,98 × (seit „Prod-Konfig“ mit Aufwärmrunde) — im Band der Einzelläufe: die Ausführung in Postgres ist nicht die Grenze (Engpass-Sonden oben).

**CPU-Profil des Hosts unter S5 (2026-10-17, Messserver, `61f8e32`, Diagnose, `…_server-sonde-profil/sonde/profil.md`):** `dotnet-trace`
(Stacks aller .NET-Threads, ≈ 100 Hz) für 15 s ab Beginn von S5-8, also S5-8 und S5-64; `scripts/sonde-engpass.sh --profil S5-8-w1`, Auswertung
`scripts/sonde-profil.py`. Auf Linux markiert der Sampler fast jede Stichprobe als nativen Code; Arbeiten und Warten trennt deshalb der innerste
.NET-Rahmen (Semaphore, Monitor, Socket-Schleife = Warten). Von 56 372 Stichproben arbeiten 45 284 (≈ 30 Threads im Mittel bei ≈ 7 Kernen CPU des
Hosts — ein Teil davon steht also in Sperren der Laufzeit). Davon: **58 % in Martens LINQ-Übersetzung `ReduceToConstant`**, 7 %
`DynamicMethod.GetMethodDescriptor`, 10 % `FastExpressionCompiler` — Marten wertet bei jeder Abfrage über `QueryAllRawEvents().Where(…)` die
eingefangenen Variablen aus, indem es sie zu Code kompiliert. Ausgelöst von `MartenEventStore.ReadKorreliertAsync` (16 388 Stichproben),
`ReadStreamAsync` (8 615) und `ReadEventAsync` (5 065). Nach Bibliothek: Marten 66 %, FastExpressionCompiler 10 %, Npgsql 7 %, Framework 7 %,
Polly 2 %, Proto.Actor 2 %, Redis 1 % (PF-21).

**Event-Reads ohne LINQ (PF-21, 2026-10-17, Messserver, `83f0375`, Spalte „Reads ohne LINQ“, docs/03 §3.3):** gültig, Exactly-once grün; zwei
Störungsläufe grün (`…_reads-sql-stoerung-1/2`). Gegen „Fold ab Hinweis“ (je ein Lauf): **S5-64 1 188 → 1 988 Vorgänge/s (1,67 ×), Latenz p50
1 051 → 277 ms, Reaktion p99 697 → 275 ms**, Host-CPU im Fenster 7,3 → 4,1 Kerne; S3-8 1,28 ×, S3-64 1,13 ×, S5-1 1,11 ×, S5-8 1,02 × (Latenz
p50 231 → 38 ms), S1/S2 1,03–1,05 ×, S4 0,95 ×, Leerlauf unverändert. Profil unter S5 (`…_server-sonde-profil-reads-sql`): arbeitende
Stichproben 45 284 → 11 863, Martens LINQ-Übersetzung und das Kompilieren dynamischer Methoden sind verschwunden; Host-CPU je Vorgang in
S5-64 gerechnet ≈ 9 → 3 ms. Was jetzt oben steht: Npgsql 29 % (Senden über den Socket 16 %), Framework 22 %, `Abstractions` 13 %, Marten 11 %
(Schreiben), Proto.Actor 5 %, Redis 4 %; einzelne Posten `Dictionary.Resize` 8 %, `Materialisiere` 5 %, MD5 5 % und SHA256 2 % (deterministische
Ids), `Guid.NewGuid` 3 %. Der Rechner steht in S5-64 bei 60 % mit CPU-Druck 0,28 — Host und Postgres-Backends (4,3 Kerne) teilen ihn weiter.

**Wo Marten noch bremst (2026-10-17, Messserver, Profile `…_server-sonde-profil-reads-sql` (S5) und `…_server-sonde-profil-s1-s3`
(S1, S3; 40 s), Diagnose):** eine Stichprobe zählt als „in Martens LINQ“, wenn sie in der Übersetzung rechnet (`Marten.Linq`,
`FastExpressionCompiler`, dynamische Methoden); zugeordnet wird sie der eigenen Methode direkt über der Übersetzung (tiefere Stacks enthalten
oft fremde Fortsetzungen, die nach einer Datenbank-Antwort im selben Thread weiterlaufen). Gemessen, Anteil an der Arbeit des Hosts:
- *Martens LINQ:* S5 0,3 %; S1/S3 7,6 %, davon 7,2 % `ReadPartitionAsync` (Seiten der Projektionen) — alles übrige (Offen-Index, Fristplan,
  Startup) unter 0,1 %. Laden per Id (`LoadAsync`) übersetzt nichts.
- *Alles unterhalb eines Marten-Aufrufs* (Laden, Speichern, Anhängen, Sitzungen): S5 21 %, S1/S3 28 %. Davon rechnet Marten selbst (Marten,
  Weasel, JasperFx an der Spitze) 1 % bzw. 7 % (fast ganz die LINQ-Übersetzung oben); der Rest ist Npgsql und das Senden über den Socket,
  Laufzeit (Tasks, Wörterbücher) und JSON — das bliebe mit eigenem SQL.
- *Kopfzeilen beim Lesen:* in S5 8,5 % in `SystemTextJsonSerializer.FromJson` mit `Dictionary.Resize` an der Spitze — das Deserialisieren der
  Kopfzeilen zu `Dictionary<string, object>` in den eigenen Event-Reads (dasselbe tat Martens LINQ-Weg vorher).
- *Datenbankseite* (`pg_stat_statements`, „Reads ohne LINQ“): Marten fügt je Event eine Anweisung ein und schreibt den Stream-Kopf einzeln (S5-64
  6,8 + 5,2 Anweisungen je Vorgang, S1 18 + 17); Postgres parst und plant jede (PF-20) — unabhängig davon, wer das SQL baut.
Folgerung: alles nach SQL zu übersetzen lohnt nicht; es lohnen `ReadPartitionAsync` als festes SQL wie die übrigen Event-Reads und die
Kopfzeilen direkt als Spalten (`headers->>'…'`) statt als Wörterbuch (PF-22). Auf der Datenbankseite ist der Hebel PF-20.

**Vorbereitete Anweisungen als Standard (PF-20, 2026-10-17, Messserver, `7a752af`, Spalte „Auto-Prepare“):** Npgsql `MaxAutoPrepare = 20` in
der Verbindung des Frameworks. Gültig, Exactly-once grün, ein Störungslauf grün (`…_auto-prepare-stoerung-1`). Gegen „Reads ohne LINQ“ (je ein
Lauf): **S5-64 1 988 → 2 619 Vorgänge/s (1,32 ×), Latenz p50 277 → 133 ms**, S5-8 1,39 ×, S5-1 1,21 ×, S4 1,56 ×, S3-8 1,07 ×, S1/S2 0,97–1,03 ×,
S3-64 0,91 × (Einzelwert; S3-64 streut zwischen Läufen desselben Codes um ± 15 %), Leerlauf unverändert.

**PF-22: Seiten der Projektionen ohne LINQ, Kopfzeilen als Spalten (2026-10-18, Messserver, `8948277`, Spalte „PF-22“):** gültig, Exactly-once
grün, ein Störungslauf grün. Gegen „Auto-Prepare“ (je ein Lauf): Durchsatz S3-8 1,17 ×, S5-64 1,06 × (2 771 Vorgänge/s), übrige 1,00–1,03 ×;
Host-CPU im Fenster S5-64 7,2 → 5,6 Kerne, S5-8 6,1 → 4,8, S2 2,4 → 2,1 — die erwartete kleine Entlastung, im Durchsatz meist im Band.

**Diagnose der Horizont-Probe (PF-17, 2026-10-18, Messserver, `…_server-sonde-horizont-2`, S1 und L1):** `scripts/sonde-engpass.sh --horizont
cqrs_vermessung` misst jede Sekunde die Teile der Probe einzeln mit `EXPLAIN (ANALYZE, BUFFERS)`, das `exists(… seq_id > X)` mit X knapp hinter
dem Kopf (50) und weit zurück (2 000), einmal mit zugeschnittenem, einmal mit allgemeinem Plan (wie ihn eine vorbereitete Anweisung nach einigen
Aufrufen nimmt). Gemessen:
- *Zählerstand, älteste Transaktion:* 0,01–0,04 ms — nicht die Ursache.
- *`exists` knapp hinter dem Kopf, während S1 die Tabelle füllt:* auch der zugeschnittene Plan durchsucht die Tabelle von vorn (Seq Scan,
  Median 0,74 ms, 480 Puffer) — die Statistik der frisch wachsenden Tabelle ist veraltet, Postgres hält sie für klein. Das sind die 0,4–0,57 ms je
  Probe aus „Horizont + Uhr“.
- *Danach, mit frischer Statistik:* zugeschnitten über den Index (Index Only Scan, 0,15 ms, 3 Puffer); **mit allgemeinem Plan weiter Seq Scan:
  3,1–3,6 ms, 790 Puffer bei 11 000 Events** — linear mit der Größe der Tabelle. Das ist die Probe im Leerlauf seit „Auto-Prepare“ (3,4 ms): Npgsql
  bereitet sie vor, Postgres wechselt zum allgemeinen Plan. Bei Millionen Events wären das Sekunden je Probe.
Folgerung: die Abfrage so stellen, dass jeder Plan den Index nimmt — etwa `select seq_id … where seq_id > $1 order by seq_id limit 1` statt
`exists(…)`; oder „steht dahinter etwas?“ aus dem Zählerstand beantworten, den die Probe ohnehin liest (PF-17).

**Abfragen über den Index, unabhängig von der Datenmenge (PF-17, 2026-10-18, Messserver, `3622027`, Spalte „Plan-Index“):** die Horizont-Frage
„steht dahinter etwas?“ als `order by seq_id limit 1` statt `exists`; Marten-Index auf `ProzessOffen.Frist` und `Frist.Fällig`, die Abfragen der
fälligen sortiert und je Takt auf die frühesten 1 000 begrenzt. Nachweis `AbfragePlanGarantieTests` (vor dem Umbau rot: Horizont mit allgemeinem
Plan 8 196 Blöcke, fällige Vorgänge 6 064). Gemessen, gültig, ein Störungslauf grün: Horizont-Abfrage in S1 26 statt 474 µs, im Leerlauf 79 statt
1 455 µs; ihr Anteil an der Datenbankzeit von S1 35 → 3 %, Datenbankzeit je Paar 9,7 → 6,5 ms. Durchsatz gegen „PF-22“ 0,94–1,01 ×, S3-64 1,30 ×
(Einzelwert) — S1 begrenzt der Datei-Treiber (PF-10).

**Wachsende Bestände (PF-23 bis PF-32, 2026-10-18, Messserver, Spalte „Wachstum“):** die Frage war, was mit der Datenmenge statt mit der Last
teurer wird. Bestandsaufnahme aus Code, `pg_stat_statements` und `pg_stat_user_tables` der Messdatenbanken (docs/13 §13.6, offen §13.0).
- *Gemessen, Zuwachs:* die Prüfdomäne hinterließ nach einem Lauf je Vorgang 7,3 Events, 2 Streams und 5,2 Quittungen; die Event-Tabelle belegt
  ≈ 520 Byte je Event (Kopfzeilen ≈ 140, Daten ≈ 70). *Gerechnet:* ≈ 4,7 kB je Vorgang, bei dauerhaft 10 Vorgängen pro Sekunde ≈ 4 GB am Tag;
  die Inbox pendelt sich bei 30 Tagen Horizont auf ≈ 135 Mio. Zeilen (16 GB) ein. Gelöscht wird nur in der Inbox und im Offen-Index.
- *Gemessen, teuerste Abfrage:* der Read „Stream ab Version“ der Kompensation lieferte 105 Zeilen je Aufruf und trug 108 von 252 s Datenbankzeit
  eines Prüfdomänen-Laufs; die heißen Posten-Streams waren nach 5 Minuten bis zu 3 400 Events lang, der Kontingent-Stream 8 484 (davon 8 483
  Ablehnungs-Marken). *Gerechnet:* linear mit der Länge des Ziel-Streams, bei 1 Mio. Events ≈ 3 s je Kompensation.
- *Behoben, je mit Test vorher rot (Sabotage im Server-Checkout):* PF-23 Kompensation aus dem Marking; PF-24 korrelierter Read mit Kopf und
  Untergrenze, Marking beim Stillwerden und Stoppen durabel; PF-25 (Teil) Poll ohne Vollliste; PF-26 (Teil) Warte-Zeiger; PF-27 Inbox über
  B-Baum mit Untergrenze (gemessen bei einer Million Quittungen: BRIN 9 957 Blöcke je Seite, B-Baum ohne Untergrenze 1 994, mit 606; bei zwei
  Millionen mit einer Million toter Einträge 14 936 → 5 Blöcke); PF-28 Ausschlussliste gedeckelt; PF-29 Rückstand gedeckelt; PF-30 Redis mit
  Ablauf.
- *Plan-Falle:* die erste Form des korrelierten Reads (zwei Hilfstabellen, lateraler Join) las mit veralteter Statistik 5 243 Blöcke —
  `max(version)` lief als Aggregat über den ganzen Stream, der Join machte die Untergrenze zum Filter. Die jetzige (zwei einfache Teile,
  `union all`) liest 8 bzw. 30 Blöcke mit jedem Plan; Ausführung ≈ halb, Planen ≈ ein Viertel der ersten Form.
- *Gemessen, E2E* (`…_0343_5e2d293_server-wachstum-2` gegen den frisch gemessenen Ausgangsstand `…_0359_229e39a_server-wachstum-basis-2`, je ein
  Lauf): gültig, Exactly-once grün; Durchsatz S1 1,01 ×, S2 0,93 ×, S3-8 0,91 ×, S3-64 1,15 ×, S4 1,52 ×, S5-1 0,91 ×, S5-8 1,04 ×, S5-64 1,03 ×
  (im Band der Einzelläufe); Leerlauf L1/L5 unverändert. Der korrelierte Read kostet ≈ 30–40 µs mehr je Aufruf (S5-1: Datenbankzeit 1,16 ×); in
  S5 sparen die Kompensationen dafür die Stream-Reads (S5-64 Stream-Reads 938 → 196 ms). Der erste Lauf des Ausgangsstands war ungültig (S4
  nach 300 s nicht vollständig, `…_0350_229e39a_…-basis`), der zweite gültig.
- *Störungslauf:* `…_0405_…-stoerung-1` rot — eine Projektion übersprang zwei Events eines Streams, während Redis angehalten war (G-11, docs/13);
  `…_0417_…-stoerung-2` grün.
- Lokal einmal gesehen, nicht reproduziert: der volle Prüfstand hing in `PipelineFlussTests.Bestellung_Oder…(karte)`, während daneben gebaut
  wurde; zwölf Wiederholungen der Fluss- und Garantie-Tests und zwei volle Läufe danach grün.

**Basis des Umbaus Pub/Sub-Neustrukturierung (2026-10-26, Messserver, `b1af87e`, Spalte „Baseline“, Umbau-Phase 0):** der erste Lauf nach dem
Schnitt und die Referenz für alle Phasen des Plans; gültig, Exactly-once grün (`…_0539_b1af87e_server-pubsub-basis`). Gemessen S1, S3, S5 (der
Treiber wählt S3-64 und S5-64 nur mit ihren Geschwistern; L1 und L5 laufen mit): S1 61,9 Paare/s bis alles sichtbar bei 22,6 Transaktionen und
171 Queries je Paar (623 % CPU); S3-64 1 341 Teile/s, Latenz p50 591 ms; S5-64 2 326 Vorgänge/s, p50 177 ms; L1 5,2 Queries/s und 2,5 % CPU.
*Profil unter gehaltener S5-64-Last* (Diagnose, `…_0545_b1af87e_server-sonde-profil-pubsub-basis`, `--sender S5=64 --fenster 120 --hoechstens
S5=1000000`, `dotnet-trace` 60 s ab Beginn des Blocks, `dotnet-counters` über den Lauf): zwei Minuten lang nimmt der Eingang 2 475 Vorgänge/s an,
durch kommen 2 322/s — am Fensterende stehen 18 400 im Rückstand (Abbau 37 s, Latenz p99 63 s). Der kurze Block der Tabelle (≈ 1 s) verdeckt,
dass S5-64 auf Dauer nicht trägt. Host 5,6 und Postgres-Backends 5,8 Kerne, Rechner zu 65 % ausgelastet (CPU-Druck 0,35), Npgsql im Mittel 40
Verbindungen belegt. Im Host: der Prozess-Manager trägt 23 % der Arbeit (inklusiv), davon sein Fold über den korrelierten Read 18 %; Senden über
den Socket 17 %; die deterministischen Ids (MD5 4,3 %, SHA-256 2 %) und `Guid.NewGuid` 2,6 %; Redis 5,8 %; Proto.Actor/Cluster 9 %.

**Phase 4 gegen die Basis (2026-10-26, Messserver, `a2b812b`, Spalte „Phase 4“, `…_1841_a2b812b_server-pubsub-phase4`):** gültig, Exactly-once
grün (S5 jetzt am Log geprüft: Aufträge, Kompensation über den Beleg, Verteilung, Nachschub; Soll und Ist in einer Abfrage, VM-1). **S1** 159
Paare/s bis alles sichtbar statt 62 (2,6 ×), Latenz p50 2,6 s statt 5,9 s, 9,7 Transaktionen und 90 Queries je Paar statt 22,6 und 171 — der
Bildeingang ist zwei Funktionen statt eines Flusses auf dem Dirigenten. **S3** 64 Sender 1 469 Teile/s (1,09 ×, p50 667 statt 591 ms), 8 Sender
1 260 (1,18 ×; p50 23 statt 13 ms), Queries je Teil 0,65 ×. **S5-1** 385 Vorgänge/s (0,95 ×, p50 8 statt 12 ms). **S5-8 und S5-64 brechen ein:**
306 bzw. 302 Vorgänge/s statt 1 374 bzw. 2 326 (0,22 × bzw. 0,13 ×), Latenz p50 3,7 bzw. 5,2 s. Ursache (Diagnose
`…_1852_a2b812b-geaendert_server-pubsub-phase4-diag-slots16`, nur S5, die Umgebung auf dem Server geändert): die Funktionen der Prüfdomäne laufen
ohne Eintrag in der Umgebung mit **einem Slot je Partition**; jeder Eingang wartet auf seinen Emit, acht Partitionen tragen höchstens acht
Eingänge zugleich. Mit 16 Slots für `Auftragsabwicklung`, `Verteilabwicklung` und `Nachschub` liefert S5-8 1 617/s (1,18 ×, p50 16 ms) und S5-64
1 294/s (0,56 ×, p50 505 ms) — ein Rest bleibt bei 64 Sendern (docs/13 PF-33). Nachschub (früher Reaktion) unter S5-64 p50 2,7 ms, p99 185 ms
(Basis 3,7 bzw. 105). **L1** wird nicht still: eine draußen getragene Funktion (`IBildpaarBeurteilung`) hat in der Messung keinen Träger, ihre
Position bleibt stehen („Funktionen ab 80“), der Nachlauf endet nie (docs/13 FB-7); L1 7,1 statt 5,2 Queries/s. **L5** 5,3 statt 2,6 Queries/s
(je Funktion und Partition ein Poll, dazu die Uhr; PF-15), Nachlauf 2 s statt 19 s.

**Als korrekt verifiziert (Backend-Audit 2026-08, nicht erneut jagen):** Versionsarithmetik inkl. Noop- und Ablehnungs-Marken; OCC-Skip auf dem
`AnyVersion`-Pfad; Append vor State-Mutation; Shard-Hash-Konsistenz Subscribe ↔ gezieltes Publish; Pre-Dispatch-Guard des Adapters je Stream (heute `EmittentenAdapter`);
Signal→Wake-Mapping; Kompensations-Dedup und Count-Join ohne verfrühtes Feuern; Snapshot-Schema-Gating; Metadaten-Readback.

## 12.6 Wie man einen Test schreibt

- **Prüfstand:** ein Aggregat über die generierte Fabrik treiben, FluentAssertions, Methodennamen `Deutsch_mit_Unterstrich`. Muster:
  `Ddd/DatensatzAggregatTests.cs`, `Aggregate/SammelvorgangBarriereTests.cs`. Analyzer und Generatoren über eine eigene `CSharpCompilation`
  (Muster: `Analyzers/*Tests.cs`). Gesten gegen eine Solution im Speicher (Muster: `Extractor/GestenTests.cs`).
- **Test-DSL `Cqrs.Testing`** (nur Prüfstand): `Szenario` (Gegeben → Wenn → Dann über die generierte Fabrik) für ein Aggregat und
  `Ablauf` für Aggregat + Funktion (eine Funktion mit gewähltem Ausgang je Eingang, die Kanten laufen bis zur Ruhe).
- **Framework-Verhalten domänenneutral:** in die Prüfdomäne (`Pruefdomaene/`, eine eigene Anwendung mit Komposition `Pruefdomaene.Laufzeit`)
  einen Typ ergänzen und in `Pruefdomaene.Integration.Tests` testen (Vorlage `AblaufE2ETests`, Helfer `Pruefstand.cs`: Cluster, eigenes Schema,
  Voll-Fold-Zustand) — nicht an die Beispieldomäne binden.
- **Integration:** Store-Semantik gegen echtes Marten, Vorlage `CoCommitPostgresTests`. Infrastruktur extern starten
  (`docker compose -f docker-compose.infrastructure.yml up -d` bzw. `docker start cqrs-postgres cqrs-consul`), kein
  Testcontainers. Fixtures je Klasse; Sequentialität per `xunit.runner.json`; Verbindungsdaten je Klasse hartkodiert (`docs/13` P2-8).
- **Python:** nur mit dem Interpreter der Umgebung (`Umgebung:Python` in `umgebung.<name>.json`, sonst `CQRS_PYTHON`, sonst `python3`; lokal
  `gpu_env`), weil betterproto/pytest dort installiert sind. Der Fluss-E2E-Test mit Python-Worker liest ihn aus `umgebung.sim-main.json`.

**Test-Stufen (seit 2026-10-28, `scripts/tests.sh`):** `schnell` je Teilschritt — Build, Prüfstand, Integration ohne die Kategorie `Last` (die
beiden Stresstests des Horizonts, zusammen ≈ 3,8 min), Prüfdomäne, Verhaltens-Netz: ≈ 7 min statt ≈ 13,5 (Integration 301 → 74 s, Prüfdomäne
343 → 181 s, Verhaltens-Netz 145 → 108 s). `voll` zur Abnahme mit `Last`, Codegen-Drift, Client, Python, `--check`, `--sonde`, `--grammatik` und Gate.
Der Takt je Partition ist einstellbar (Umgebung `Konsum.TaktSekunden`); `Pruefstand.Cluster` und der Prüfdomänen-Host des Verhaltens-Netzes laufen
mit 2 s, damit Tests, die auf den Takt warten (Nachholen, Position über fremde Events), nicht je 30 s stehen. Ein Test, der zeigt, dass ein Commit
weckt, ohne auf den Takt zu warten, verlangt den Standard (`takt: Konsum.StandardTakt`) — sonst bestünde er auch ohne Wecker (Gegenprobe gesehen).

**Garantie-Test:** in den Ordner `Garantien/` der passenden Ebene (Prüfstand über Seams, sonst gegen echtes Marten), Klasse oder Methode mit
`[Trait("Kategorie", "Garantie")]`, und die Zeile in der Nachweis-Spalte von [11](11-garantien.md) nachtragen. Zeigt der Test eine offene Lücke:
`[Fact(Skip = "G-n offen (docs/13)")]` — und einmal ohne Skip laufen lassen, er muss rot sein. Die Phase, die die Lücke schließt, entfernt das Skip.
Störungs-Seams der Prüfdomäne: `Pruefstand.Cluster(…, inboxCache, dienste, schema)`, `Pruefstand.Dekoriere<T>`, `Pruefstand.FrageAsync` (Command mit
vorgegebener CommandId, Modus `Emittiert`).

**E2E-Messung:** `scripts/vermessung-e2e.sh --anlass <phase> --spalte "<Spalte>"` (Höchstlast, ≈ 5 min), `--art stoerung --anlass <phase>-stoerung`
(≈ 3 min); `--probe` ist ein verkürzter Funktionstest des Treibers und schreibt nicht ins Repo. `--art leerlauf` ist eine Diagnose (PF-14),
kein Messlauf: dieselben Blöcke, der Leerlauf aber als Zeitreihe in 10-s-Scheiben (vor der Last 60 s, danach 300 s, nach einem Neustart des Hosts
auf demselben Bestand 150 s) mit Statements und Konsumenten-Ständen je Scheibe (`zeitreihe.csv`, `zeitreihe_statements.csv`, Ordner im
Temp-Verzeichnis wie bei `--probe`); `--szenarien S1,S3,S4,S5` lässt S2 weg.
Engpass-Diagnose (nur Messserver): `scripts/sonde-engpass.sh --anlass server-sonde-<x> [--knopf sync-aus|drossel-64|auto-prepare-20|drain-8]`
— der Treiber mit S3/S5 und drei Wiederholungen, daneben die Sonde (§12.5 „Engpass-Sonden“); kein Messlauf für die Tabelle.
Danach `scripts/vermessung-tabelle.sh --schreibe` und den Ordner mitcommitten. Das Skript schaltet `pg_stat_statements` und `max_connections = 300`
am Postgres-Container ein (einmaliger Neustart).

**Messserver (seit 2026-10-15):** Messungen, Integrations-Suiten und das Gate laufen auf einem eigenen Rechner (Hetzner, bare metal, Intel
i5-13500 mit 6 P- und 8 E-Kernen/20 Threads, 62 GiB, NVMe-RAID1, Ubuntu 24.04, keine GPU) statt auf dem MacBook. Der Code kommt nur über GitHub
dorthin: `scripts/server.sh lauf <befehl …>` verlangt einen gepushten `HEAD`, checkt ihn auf dem Server aus (`/opt/bractor`, detached), führt den
Befehl in tmux aus (übersteht SSH-Abbrüche), liest die Ausgabe mit und holt neue Ordner aus `vermessung/ergebnisse/` per rsync zurück; `--los`
startet ohne zu warten, `warte`/`status`/`log` holen später ab. Ein Lauf zur Zeit. `scripts/server.sh einrichten` (idempotent,
`scripts/server-einrichten.sh`) installiert das .NET-SDK in der Version des Macs, die Python-Umgebung der Worker (torch nur CPU, verlinkt unter dem
Pfad aus `Umgebung:Python`), die Bibliotheken der nativen OpenCvSharp-Runtime und startet die Infrastruktur-Container (Projekt `cqrs`; Postgres und
Redis im Host-Netzwerk ohne docker-proxy, `docker-compose.messserver.yml`, `--netz proxy` für Vergleiche; alles nur auf 127.0.0.1, die Einrichtung prüft das). Nach außen ist nichts offen; die übrigen Dienste des Servers (Caddy, andere Apps) bleiben unberührt. Messwerte des Servers sind nicht mit
denen des MacBooks vergleichbar — Läufe dort tragen ein eigenes Anlass-Präfix (`server-…`) und erst eine eigene Baseline-Spalte, wenn die
Tabelle nach Rechner getrennt ist.

**Gesamtprüfung (für Meilensteine, ≈ 65 min mit A/B-Nachmessung):** `scripts/server.sh lauf scripts/gesamtpruefung.sh [--vergleich <ergebnis-ordner>] [--spalte <name>]
[--ohne-ab]` prüft einen Commit in einem Lauf, nacheinander: (1) die volle Test-Stufe — jeder rote Test wird bis zu zweimal einzeln wiederholt und
heißt danach „rot“ oder „wackelt“, mit seiner Nummer in docs/13, wenn bekannt (`scripts/gesamtpruefung.py`, `BEKANNT`); (2) die Messung S1–S6 mit
Exactly-once; (3) den Störungslauf; (4) die Engpass-Sonde über S1, S5 und S6 mit CPU-Profilen der Abschnitte S1, S5-64 und S6-64
(`sonde-engpass.sh --profil` nimmt mehrere Abschnitte und zeichnet jeden in der Reihenfolge des Treibers von seinem Beginn bis zu seinem Ende auf, höchstens 15 s; S6 läuft hier ohne Deckel das ganze Fenster). Der Bericht `<Messung>/gesamtpruefung.md` nennt je Teil eine Ampel, jede Kennzahl gegen
den Vergleich (ohne Angabe die letzte frühere Gesamtprüfung, sonst die Spalte „Phase 6“) mit Urteil „im Rauschen“, „besser“ oder „auffällig“ —
auffällig erst außerhalb des Rausch-Bands der Kennzahl (aus den bisherigen Streuungen gleicher Commits, z. B. Durchsatz ±15 %, Kosten je Vorgang
±5 %; S5-8 und S5-64 streuen auch beim selben Commit um bis zu 1,5 × und haben für Durchsatz, p99 und Abbauzeit ein Band von ±40 %, PF-36) und über einer Mindest-Abweichung in ihrer Einheit — sowie die Profile. Ist ein Szenario auffällig, misst die Gesamtprüfung es A gegen B nach
(A = Commit des Vergleichs im Worktree `/opt/bractor-a`, nur Szenarien, die es dort gibt) und hängt das Ergebnis an den Bericht.

**Erste Gesamtprüfung (2026-10-29, Messserver, `05ed98a` gegen Phase 6 `4f43172`, 66 min, Ordner `…_gesamtpruefung{,-stoerung,-profil,-ab-a,-ab-b}`):**
Korrektheit gelb — alles grün bis auf `NeustartTests` (G-18), das in beiden Wiederholungen grün war; Messung und Störungslauf gültig,
Exactly-once grün. S6 (neu): 8 Clients 399, 64 Clients 2 836 Vorgänge/s bis sichtbar, Quittung p50 1,7 bzw. 4,7 ms, bis sichtbar p50 19 bzw.
22 ms, kein Wiederverbinden, kein Command unbestätigt. Auffällig gegen Phase 6 und von der A/B-Nachmessung erklärt: S1 Queries je Vorgang
90 → 105 (der Stand je gezeigter Id, eine Abfrage je Query seit Phase 7b, PF-39); L5 Nachlauf 3 → 55 s (die Prüfdomäne hat seit Phase 8 zwei
Projektionen, deren Partitionen ohne Events erst der Takt an den Kopf rückt — in A gab es sie nicht, kein Rückschritt); S3 CPU gegen Phase 6
(dessen 9,9 % in S3-8 waren unplausibel, A/B gleich); S5-8/S5-64 in A/B 0,83 × bzw. 0,74 ×, gegen die Spalte Phase 6 aber 1,34 × bzw. 1,20 ×
— die Streuung von PF-36, seither mit eigenem Band. Profil S5-64: Events lesen ist der größte Posten (PF-36). Werkzeug-Fehler des ersten Laufs,
behoben: das Profil von S6-64 fehlte (die Abschnitte wurden in der genannten statt der gefahrenen Reihenfolge erwartet), das von S1 lief 15 s
in die beiden S6-Blöcke hinein.

**Gate „frischer Checkout baut“ (vor jedem Push):** `./pruefe-checkout.sh` (Optionen `--verz <dir>` für den Ort des Worktrees, `--ohne-tests`).
Es meldet zuerst lokal vorhandene Dateien, die eine Ignore-Regel schluckt (mit der greifenden Regel aus `git check-ignore -v`; Build- und
Cache-Ausgaben, `deploy-*/` aus `build.sh` und die bewusst ignorierten Altdateien sind ausgenommen), baut dann den Commit `HEAD` in einem
temporären `git worktree` (nur committete Dateien — nicht committete Änderungen prüft es nicht), fährt dort `./codegen.sh --check` und den
Prüfstand und verlangt, dass der Build den Checkout nicht verändert. Der Worktree wird immer entfernt; Exit-Code 0 = grün. Dauer ≈ 2½ min (144 s auf `e26bc15`).

**Verhaltens-Netz (`Verhalten.Tests`, Umbau-Phase 0):** jede Klasse schreibt sich eine Umgebung
(`Geruest/Testumgebung.cs`, `umgebung.verhalten-….json` im Temp-Verzeichnis, gefunden über `CQRS_UMGEBUNGEN`), löscht ihre Datenbank und
startet die gebauten Hosts als Prozesse (`Geruest/Hostlauf.cs`; Ports je Prozess über `Transport__GrpcPort`/`__EinblickPort`, so teilen zwei
Knoten eine Umgebung). Gesprochen wird nur über äußere Wege: `Geruest/Arbeitsplatz.cs` (Bus, Verbindung, Query-Brücke, Versionen und das
generierte Wiring des Blazor-Arbeitsplatzes, ohne Stores), `Geruest/RoheLeitung.cs` (die Leitung ohne Client-Stack: Funktion tragen und Eingänge beantworten,
Command mit behaupteter Version, alles sehen, was eine Sitzung bekommt), `Geruest/JsonEingang.cs`, `Geruest/Bildpaare.cs` (TIFF-Paare ins
Quell-Verzeichnis). Geprüft wird das fachliche Soll im Log (`Geruest/Log.cs`, `mt_events` je Stream und Typ) und in Antworten. Zeit über die
Uhr der Sim (`Testumgebung.StelleUhrVorAsync`), nicht über Warten. Läuft nur auf dem Messserver:
`scripts/server.sh lauf dotnet test Verhalten.Tests/Verhalten.Tests.csproj` (braucht Postgres, Consul).

**Werkbank-Abnahme (Ebene 2, `Werkbank.Abnahme.Tests`):** braucht vorher `dotnet build` (startet die gebaute Werkbank-DLL als Prozess, Black-Box über
HTTP). `Werkbanklauf` kopiert alle Quell-Dateien (`git ls-files -co --exclude-standard`, also auch nicht committete Änderungen) in ein Temp-Verzeichnis,
legt dort `git init -b abnahme-<id>` an (Branch → Sim-DB `cqrs_sim_abnahme_<id>`, Cluster) und schreibt `umgebung.abnahme.json` aus
`sim-main` mit freien Ports, Daten-Pfaden im Temp-Verzeichnis und der Bildaufbereitung auf Laufort `Extern`. Ihr einziger Test ist bis zur
neuen Editor-Oberfläche zurückgestellt (docs/10 E4). Arbeitsbaum, `.werkbank/` und Sim-DB des
Entwicklers bleiben unberührt; am Ende SIGTERM an die Werkbank (sie hält Host und Worker an), eigene DB löschen, Kopie löschen.

## 12.7 Fallstricke

- **xUnit schluckt App-Logs** (Console, ILogger) bis zum Testende — Hänger zuerst im Prüfstand isolieren (Glue-Test mit der echten
  Zustell-Selektion), Cluster-Diagnose mit einem eigenständigen Programm mit sichtbaren Logs.
- **Cluster-Cold-Boot:** Konvergiert die Identitäts-Platzierung beim ersten Start nicht, hilft kein längerer Timeout; viele Cluster-Boot-Läufe
  hintereinander degradieren die lokale Infra (zwischendurch neu starten).
- **Der Horizont setzt den Anhänge-Modus `Quick` voraus:** ein Test-Store, der über `ReadPartitionAsync` oder `ReadChangedStreamsAsync` liest,
  braucht `opts.Events.AppendMode = Quick` (wie der Host), sonst verweigert der Store. Eine lange offene Transaktion irgendwo im selben Postgres
  (auch in einer anderen Datenbank) hält die Positionen an — ein offenes `psql` mit schreibender Transaktion lässt Positions-Tests warten.
- **Zeit-Tests nie parallel:** Prüfstand-Tests mit echter Uhr (Leerlauf, Takte im Millisekunden-Bereich) gehören in die Sammlung
  `Zeitkritisch` (`DisableParallelization`); im parallelen Lauf verschieben CPU-hungrige Nachbarn (Analyzer-Kompilate) ihre Takte um Hunderte ms.
- `[Trait("Kategorie", "Garantie")]` markiert die Garantie-Suite; `Skip` gibt es nur für offene G-Lücken (`G-n offen (docs/13)`). Flake-Handling
  bleibt Konvention.
- **Messung:** `pg_stat_database` schreibt seine Zähler verzögert fort (Sekunden) — für Messfenster taugt nur `pg_stat_statements`; mit
  `track = all` zählte es auch Statements innerhalb von Funktionen (Martens `mt_immutable_timestamptz` je Zeile) mit. Fremdlast auf dem Rechner
  (ein Spiel, ein Build) verschiebt den Durchsatz um den Faktor 2 und mehr — sie steht je Szenario im Ergebnis; Läufe darunter vergleichen nur
  Verhältnisse. E2E-Läufe nie gleichzeitig mit einer Integrations-Suite.
- **Kindprozesse mit umgeleiteter Ausgabe:** `dotnet build/restore/run` startet MSBuild-Knoten und den Compiler-Server, die die Pipe erben —
  wer auf das Ende der Ausgabe wartet (`WaitForExitAsync`, `ReadToEnd`), hängt bis zum Zeitlimit. Auf das Prozessende warten (`Werkbank/Server/Prozesse.cs`).

## 12.8 Prinzipien

1. **Nie faken, was man nicht besitzt** — Store-Semantik nur gegen echtes Marten.
2. **Die Testebene folgt der Frage, nicht dem Feature.**
3. **Generative Vollständigkeits-Guards** statt punktueller Fälle (Wire-Boot-Check, Azyklizitäts-Guard, „Code ohne Karte: 0“).
4. **Beweiskraft explizit** — jede Integrationsklasse sagt, warum grün = bewiesen.
5. **Determinismus durch Sequentialität und direktes Treiben.**
6. **Domänenneutral testen:** Framework-Verhalten gehört in eine Test- oder Sonde-Domäne, nicht an die Beispieldomäne gebunden (W11).
