# 11 — Garantien

Stand 2026-10-26 (Plan Pub/Sub-Neustrukturierung, Phase 2), gegen den Code geprüft. Ziel für den Umbau: docs/plan-pubsub-neustrukturierung.md.

Dieses Kapitel ist die **eine Quelle für „was garantiert das Framework?“** — je Weg die Garantie, der Mechanismus, der sie trägt, und die
Bedingung, unter der sie gilt. Wie die Mechanik im Einzelnen läuft, steht in [03](03-schreibseite.md) (Schreibseite) und
[04](04-konsum-und-prozess-maschine.md) (Konsum- und Prozess-Maschine); die Invarianten in [01](01-ueberblick.md) §1.2. Wo der Code eine
Garantie heute bricht, steht die Lücke mit ID in [13](13-reifegrad-schulden-bewertung.md) §13.0 (Reihe **G**); hier wird nur verwiesen.

## 11.1 Kurzfassung

- **Stark konsistent im Aggregat:** jede Entscheidung sieht den aktuellen Stand, kein Lost Update.
- **Wirkung genau einmal je Stream:** erreicht durch Deduplizierung (Inbox, deterministische CommandId) und Co-Commit — nicht durch einen
  zuverlässigen Transport. Zugestellt wird at-least-once.
- **Ordnung nur je Stream.** Über Streams hinweg ist alles eventual und ungeordnet, auch kausal.
- **Was der Build prüfen kann, prüft der Build** (§11.10); was erst beim Start feststeht, prüfen Boot-Guards.

Begriffe: *Zustellung* = wie oft eine Nachricht ankommt; *Wirkung* = wie oft sie den Zustand ändert. Exactly-once-Zustellung gibt es nicht,
exactly-once-Wirkung schon ([04](04-konsum-und-prozess-maschine.md) §4.3).

**Nachweis** (Spalte in jeder Tabelle): der Test bzw. Analyzer, der die Garantie beweist — unter Störung, wo eine Störung sie brechen könnte
(§11.14). Kürzel: **P** Prüfstand (`Infrastructure.Pruefstand.Tests`, store-frei über Seams), **I** Integration (`Infrastructure.Integration.Tests`,
echtes Marten), **PD** Prüfdomäne (`Pruefdomaene.Integration.Tests`, echter Cluster), **E2E** die Exactly-once-Prüfung jedes Laufs von
`scripts/vermessung-e2e.sh`, **VT** das Verhaltens-Netz (`Verhalten.Tests`: echte Hosts als Prozesse, nur von außen angesprochen — es muss
den Umbau unverändert überstehen). *Skip G-n* = der Test zeigt die offene Lücke G-n und ist bis zu ihrer Schließung übersprungen; eine Garantie ohne
Nachweis ist eine Lücke in [13](13-reifegrad-schulden-bewertung.md).

## 11.2 Schreibseite (Command → Events)

| Garantie | Mechanismus | Bedingung / Lücke | Nachweis |
|---|---|---|---|
| **Atomar:** alle Events eines Commands und seine Quittung in der Inbox werden gemeinsam gültig oder keines | ein Append, eine Transaktion (`AggregateActorBase`, `BatchingEventAppender`, `InboxTabelle`); in beiden Anhänge-Modi von Marten (der Host läuft mit `Quick`) | – | I `GarantiePostgresTests.Ein_Append_wird_ganz_oder_gar_nicht_gueltig`, `…Effekt_und_Quittung_werden_gemeinsam_gueltig…` (je Rich und Quick); P `AppendBatchingTests` (Batch-Fehler isoliert den Schuldigen) |
| **Durabel nach Erfolg:** nach dem Append wird nie ein Fehler gemeldet ([02](02-design-prinzipien.md) P10) | ein Fehler nach dem Append rehydriert den Actor und quittiert Erfolg; scheitert der Append selbst, rehydriert der Actor und quittiert, was laut Inbox entschieden ist (die Quittung reist in derselben Transaktion) | so durabel wie Postgres (fsync, Platte, Replikation) | PD `SchreibGarantieTests.Ein_mehrdeutiger_Commit_wird_als_Erfolg_quittiert…`; der Pfad „Fehler nach dem Append im Actor“ ohne eigenen Test (G-6) |
| **Serialisierbar je Aggregat:** jede Entscheidung sieht den aktuellen Stand | Single-Writer-Actor je Stream (virtuelle Cluster-Identität) plus Versionsprüfung beim Append; nach einem Append-Konflikt rehydriert der Actor frisch und entscheidet einmal neu | – | I `GarantiePostgresTests.Zwei_Schreiber_mit_derselben_Version…`; PD `ProzessE2ETests.Nebenlaeufige_Vorgaenge…`; Verfügbarkeit: PD `SchreibGarantieTests.Nach_einem_Versionskonflikt…` |
| **Ein Command erreicht nur sein Ziel:** er wirkt nur in dem Aggregat, das sein Typ nennt, und nur unter dessen Identität | das Ziel steht im Typ (`ICommand<TAggregat>` mit `Id<TAggregat>`, Plan Pub/Sub-Neustrukturierung §2.5); der Envelope leitet seine Id daraus ab; die Leitung lehnt eine Hülle mit anderer Id oder anderem Aggregat ab (`ProtoMessageMapper`); der Aggregat-Actor nimmt seine Id aus der Cluster-Identität und lehnt einen Command mit fremdem Ziel ab, **bevor er etwas lädt** | – | PD `SchreibGarantieTests.Ein_Command_an_eine_fremde_Identitaet_wird_abgelehnt…`; P `TypisierteIdTests.Eine_Huelle_mit_fremdem_Ziel…` |
| **Passivierung ist folgenlos:** ein Actor, der nach Leerlauf stoppt, wird mit demselben Stand, derselben Inbox und demselben Cursor reaktiviert | durabler Zustand nur im Log, Snapshot, Inbox-Tabelle, Marke und Cursor; Snapshot beim Stoppen (PF-5); der Inbox-Cache des Actors ist danach leer, eine Wiederholung fängt der Schlüssel der Tabelle. Der Rahmen jedes Actors (`RahmenActor`) passiviert nie, solange er beschäftigt ist (Commit, wartende Commands, Arbeit neben der Mailbox), und gibt den Fähigkeits-Bereich nach Passivierung, Stopp und Neustart frei — erst, wenn keine Arbeit ihn mehr nutzt | – | PD `LebenszyklusGarantieTests.Passivierung_und_Reaktivierung…`, `SchreibGarantieTests.Eine_Wiederholung_nach_dem_Neustart…`; P `PassivierungTests`, `RahmenActorTests` (Bereich frei nach Passivierung, Stopp, Neustart; ein Durchlauf hält Passivierung und Freigabe auf), `LebenszyklusImClusterTests.Der_Batch_Leser_gibt_seinen_Bereich_erst_frei…`, `…Der_Stream_Adapter_gibt_seinen_Bereich_nach_der_Passivierung_frei` |
| **Client-OCC:** ein Client-Command mit behaupteter Version (≥ 0) scheitert, wenn das Aggregat sich seitdem geändert hat | Vorprüfung (`CommandVorpruefung`), Versionsprüfung beim Append | – | P `CommandVorpruefungTests` |
| **Emittierte Commands** (Reaktion, Prozess, Fluss, Zusage mit Kausalität, Client mit Version < 0) entscheiden auf dem **aktuellen** Stand | `CommandModus.Emittiert`: keine erwartete Version | gewollt: der Sender behauptet keinen Stand | P `CommandVorpruefungTests`; PD `ProzessE2ETests.Nebenlaeufige_Vorgaenge…` |
| **Ablehnungen** stehen nicht im Log; durabel ist nur die Marke `KommandoAbgelehnt` | `ITransientEvent` | – | P `TransientPersistiertSplitTests`, `AblehnungsMarkeTests`; PD `ProzessE2ETests.Scheitert_ein_Zweig…` |
| Invarianten **über mehrere Aggregate** | – | **keine Garantie**; nur eventual über Prozesse und Kompensation | – |

## 11.3 Zustellung und Idempotenz von Commands

| Weg | Zustellung | Wirkung | Bedingung / Lücke | Nachweis |
|---|---|---|---|---|
| **Client** (gRPC, `CqrsClientService`) | fire-and-forget über `ProtoActorAggregateDispatcher`: bis zu 3 Versuche je 10 s, dann Dead-Letter und `CommandFailed` an die Sitzung | **genau einmal, wenn der Client mit derselben CommandId wiederholt**; die Inbox prüft vor der Version | Ein Command, der den Host vor dem Dispatch verliert (Absturz), ist weg; der Client merkt es nur am fehlenden Ack und muss selbst wiederholen. Ohne Wiederholung gilt höchstens einmal. | P `CommandVorpruefungTests.Bereits_verarbeitet_an_stale_Version…`; I `EingangE2ETests.Client_wiederholt_mit_derselben_CommandId…` (über den JSON-Eingang); VT `ClientVerhaltenTests.Die_Quittung_eines_Commands_erreicht_den_Client…` (Quittung über die Korrelation, ohne Wiederholung), `…Ein_Client_sieht_jedes_gehoerte_Event…` (Version aus den Pushes = Kopf, OCC nimmt an); Ablehnung als Quittung: Skip G-12 |
| **Client mit Antwort** (JSON-Eingang, `FrageAsync`; optional `commandId` im Body) | ein Versuch, gebunden | wie oben | „keine Antwort“ heißt nicht „nicht gewirkt“ | I `EingangE2ETests.Client_wiederholt_mit_derselben_CommandId…`; E2E Störungslauf (Wiederholung bei Host-Abbruch) |
| **Ablehnung** | – | **idempotent:** eine Wiederholung liefert dieselbe Ablehnung, auch wenn sie jetzt wirken könnte | Quittung „abgelehnt“ in der Inbox (dazu die Marke `KommandoAbgelehnt` für den Prozess-Fold); innerhalb des Horizonts | P `CommandVorpruefungTests.Bereits_abgelehnt…`; PD `SchreibGarantieTests.Eine_Wiederholung_nach_dem_Neustart…` |
| **Emittiert** (`CommandEmitter`) | at-least-once, eine Frage mit Pflicht-Frist (5 s, `Bote`), höchstens 8 Sends je Ziel-Aggregat gleichzeitig (Drossel); der Emitter wiederholt nicht selbst, er meldet den **Ausgang** (zugestellt, abgelehnt, unbekannt). Bei „unbekannt“ wiederholt der Aufrufer mit derselben CommandId: die Frist bleibt liegen, der Emittenten-Cursor rückt nicht vor, der Dirigent wiederholt nach Backoff | **genau einmal** über die deterministische CommandId und die Inbox des Ziels (eindeutiger Schlüssel je CommandId, in der Transaktion der Events) | innerhalb des Horizonts `Schreibpfad.InboxHorizontTage` (Standard 30 Tage, E2); danach vergisst die Inbox die CommandId | P `EmitGarantieTests` (Ausgang, Drossel, Backoff), `BoteTests` (eine Frage ohne Antwort läuft in ihre Frist, wirft nie); PD `SchreibGarantieTests.Doppelt_und_gleichzeitig_zugestellter_Command…`, `…Eine_alte_CommandId_jenseits_der_Inbox_Grenze…` (G-2), `…Eine_Wiederholung_nach_dem_Neustart…`, `…Ein_Duplikat_mitten_im_Group_Commit…`, `…Die_Inbox_vergisst_erst_jenseits_des_Horizonts`; I `VertragsZusageE2ETests`; VT `ClientVerhaltenTests.Eine_Zusage_Antwort_wirkt_genau_einmal…` (zwei Träger, Neuverbinden, über die Leitung); P `KonsumGarantieTests.Absturz_zwischen_Emit_und_Cursor…`, `FristGarantieTests.Doppeltes_Feuern…`; E2E „keine CommandId zweimal gewirkt“ (Events je CommandId in genau einer Transaktion) |

## 11.4 Ordnung

| Garantie | Gilt | Mechanismus / Folge | Nachweis |
|---|---|---|---|
| Events eines Streams werden in jedem Konsumenten in Versionsreihenfolge angewendet | **ja** | Projektion: ein Stream liegt in genau einer Partition, ein Leser je Partition liest nach globaler Sequenz (je Stream = Versionsreihenfolge), Guard je Stream; Emittent: ein Actor je (Konsument, Stream), Vorab-Prüfung der Version | P `BatchKonsumGarantieTests.Doppelte_und_ungeordnete_Seiten_und_Nachzuegler…`, `KonsumGarantieTests.Doppelte_und_ungeordnete_Weckungen…`; I `SequenceGapPostgresTests.Batch_Leser_ueberholt_einen_Nachzuegler_nicht…` |
| globale Ordnung über Streams | **nein** | die globale Sequenz von Marten ist nur Lese-Cursor (Poll, Partitions-Leser), kein Versprechen; Partitionen laufen parallel | – |
| kausale Ordnung über Streams (Ursache vor Wirkung) | **nein** | eine Projektion über X und Y kann die Wirkung in Y vor der Ursache in X sehen | – |
| Prozesse und Flüsse | ordnungsunabhängig | der Dirigent faltet eine Markierung (Petri-Netz); welche Bedingung zuerst eintrifft, ändert das Ergebnis nicht | PD `ProzessE2ETests.Diamant…`, `MarkingCursorAequivalenzTests` |

## 11.5 Projektionen (replaybar)

| Garantie | Mechanismus | Bedingung / Lücke | Nachweis |
|---|---|---|---|
| **Wirkung genau einmal je Event** | Batch-Co-Commit: die Effekte aller Streams eines Batches, ihre Guards (`ProjectionCheckpoint`) und der `PartitionsCheckpoint` in einer Transaktion (`MartenCoCommitStoreBase`); der GA-1-Check erzwingt den Co-Commit für append-artige Projektionen | – | I `CoCommitPostgresTests.Absturz_mitten_im_Batch…`, `…Viele_Streams_eines_Batches…`; P `BatchKonsumGarantieTests.Absturz_mitten_im_Batch…`, `GaEinsPruefungTests`; E2E „Read-Model = Log“ (S1, S4) |
| **Zwei Aktivierungen** derselben Partition (Netzstörung im Cluster) wenden keinen Effekt doppelt an | der Partitions-Checkpoint wird mit erwarteter Version geschrieben (`Insert` bzw. `UpdateRevision`); die verlierende Aktivierung lädt neu, der Guard überspringt das schon Angewandte (G-4 geschlossen) | – | I `GarantiePostgresTests.Zwei_Aktivierungen_derselben_Projektion…`; P `BatchKonsumGarantieTests.Zwei_Aktivierungen…` |
| **geteilte Dokumente** ohne verlorene Updates | Versionsprüfung; ein Konflikt verwirft den Batch, der Leser halbiert ihn nach Streams und wiederholt einen einzelnen bis zu fünfmal, danach bleibt er bis zum nächsten Durchlauf stehen (die Position hält vor ihm); im selben Batch sehen sich zwei Streams über die Identity-Map | – | I `SchreibRegelPostgresTests` (zwei Partitions-Leser), `GeteilteDokumentePostgresTests` (vier Partitions-Leser auf einem Singleton); P `BatchKonsumGarantieTests.Ein_Konflikt…`, `…Ein_dauerhafter_Konflikt…` |
| **Vollständigkeit:** jedes committete Event kommt an | Signal weckt die Partition (schnell), der Poll weckt alle 30 s jede Partition; der Leser liest ab seiner durablen Position, die nur bis zur gesetzten Sequenz vorrückt — dem Horizont der Vollständigkeit aus den Transaktionsnummern von Postgres, ohne Uhr ([03](03-schreibseite.md) §3.3) —, bestimmt vor dem Lesen der Seite, in derselben Runde wie „steht dahinter etwas?“ (ohne Neues keine Seite, PF-14). Tombstones eines gescheiterten Appends liest niemand (`MartenEventStore`; der Host schreibt im Anhänge-Modus `Quick` keine mehr) | Eine lange offene Transaktion hält die Positionen an (Zustellung und Latenz laufen weiter), sie wird nie überholt (G-9 geschlossen); der Store warnt nach 30 s. Gilt im Anhänge-Modus `Quick` (Marten schreibt die Stream-Zeile vor den Events). | I `SequenceGapPostgresTests.Eine_lange_offene_Transaktion_wird_nicht_ueberholt` (gegen den alten Code rot gesehen), `…Batch_Leser_ueberholt_einen_Nachzuegler_nicht…`, `…Hinter_der_Marke_nichts…`, `AnhaengeReihenfolgePostgresTests`, `PollerBackstopPostgresTests.Batch_Leser_holt_ein_Event_mit_verlorenem_Signal…`; P `HorizontTests`, `BatchKonsumGarantieTests.Eine_volle_Seite…`; E2E Störungslauf |
| **Ein Poison-Stream hält niemanden auf** | der Stream des werfenden Events wird abgetrennt, ein werfender Commit halbiert; wirft er allein, wird er geparkt (im Partitions-Checkpoint, Dead-Letter einmal), je Poll-Intervall einzeln versucht und nach der Heilung ab seinem Guard nachgeholt | sichtbar in `/health` (`geparkteStreams`) und als Dead-Letter | I `GarantiePostgresTests.Ein_Poison_Stream_wird_im_Batch_geparkt…`; P `BatchKonsumGarantieTests.Ein_Poison_Stream…` |
| **Aktualität** | Signal | eventual: typisch Millisekunden, schlimmstenfalls ein Poll-Intervall | I `LiveCommandE2ETests`; E2E Latenz ([12](12-tests-und-vermessung.md) §12.5) |
| **Read-your-writes** | Redis-Versionsindex (`ctx.Track`) | **nur best-effort**; fällt Redis aus, verlängert sich nur die Wartezeit | E2E Störungslauf (Redis 30 s weg, Exactly-once bleibt); VT `ClientVerhaltenTests.Nach_dem_eigenen_Command_sieht_die_naechste_Query…` (Änderung, ohne Störung); nach dem Anlegen greift es nicht: Skip G-14 |
| **Neuaufbau** | Ziel leeren, Partitionen (neue Version, Position 0) und Guards zurücksetzen, dann je Partition der Batch-Leser ab 0, seitenweise (`ProjectionRebuilder`; in Sim `ProjektionsStandDienst` bei geändertem Code oder geänderter Partitionszahl); ein noch laufender Leser verliert seinen Commit | deterministisch nur bei deterministischen Handlern; der alte Stand wird vorher geleert | P `ProjectionRebuilderTests`, `ProjektionsStandTests`; I `GarantiePostgresTests.Ein_Neuaufbau_nimmt_einem_laufenden_Leser_den_Commit`, `LeseseitenStandE2ETests` |

## 11.6 Reaktionen (emittierend)

| Garantie | Bedingung | Nachweis |
|---|---|---|
| der Handler läuft **mindestens einmal** je Event; Signal und Poll lesen ab dem Cursor, der erst vorrückt, wenn jeder ausgegebene Command zugestellt oder abgelehnt ist (die Commands eines Tails gehen parallel hinaus); eine Weckung, die im Adapter scheitert, beantwortet er ehrlich mit „nicht aufgeholt“ — der Poll hält seine Marke | – | P `LebenszyklusImClusterTests.Eine_gescheiterte_Weckung_des_Stream_Adapters…`, `EmittentenPfadAdapterTests`, `KonsumGarantieTests.Absturz_zwischen_Emit_und_Cursor…`, `…Ein_nicht_zugestellter_Emit…`, `…Die_Commands_eines_Tails_gehen_parallel_hinaus`; PD `AblaufGarantieTests.Absturz_zwischen_Emit_und_Cursor…` |
| ausgegebene Commands wirken **genau einmal** | Inbox des Ziels, innerhalb des Horizonts (§11.3) | PD `AblaufGarantieTests.Reaktion_wirkt_genau_einmal…`, `…Absturz_zwischen_Emit_und_Cursor…`; P `ReaktionsIdTests`; E2E S5 „je Reaktions-Command genau eine Wirkung“ |
| **kein** Reset und kein Replay | zur Compile-Zeit wahr: der Emittenten-Pfad (`EmittentenAdapter`) nimmt keinen Tracker, `IEmittentenCursor` hat kein Reset | Typ (Compile-Zeit); P `EmittentenPfadAdapterTests.Jede_Weckung_faltet_ab_dem_Cursor` |
| ausgegebene Events (`IEvent` aus einem Handle) | verlierbar re-publiziert, nicht im Log | – |
| Seiteneffekte **außerhalb** von Commands | **nur at-least-once**, keine Deduplizierung | – |

## 11.7 Prozesse, Flüsse, Funktionen, Fristen

| Garantie | Mechanismus | Bedingung / Lücke | Nachweis |
|---|---|---|---|
| jede Transition feuert mindestens einmal und wirkt genau einmal | deterministische CommandId, Inbox des Ziels; der Fold erkennt eine Wirkung an ihrem Event (`causation_id`), einen Noop an der Marke `KommandoVerarbeitet`, eine Ablehnung an `KommandoAbgelehnt` | Horizont der Inbox (§11.3) | PD `ProzessE2ETests`, `MarkingCursorAequivalenzTests`; E2E S3, S5; VT `BeispielAblaufTests` (Bildeingang, Datensatz, Sammelvorgang), `PruefdomaeneAblaufTests` (Auftrag, Kompensation, Verteilung, Nachschub), `TrainingsWaechterTests` — fachliches Soll im Log, unabhängig vom Mechanismus |
| der Dirigent bleibt unter Überlast lebendig: ein unbekannter Send wird erst nach Backoff je Vorgang (1 s … 30 s) wiederholt, nie zweimal gleichzeitig; höchstens 8 Sends je Ziel-Aggregat unterwegs | `DetachedProzessSend`, `EmitDrossel` | – | P `EmitGarantieTests.Nach_unbekanntem_Ausgang…`, `…Die_Drossel_begrenzt…`; E2E S3 (heißes Aggregat) |
| kein Vorgang bleibt still liegen | Prozess-Poll (seitenweise ab der Marke, bestätigte Weckungen — kein Ack oder „nicht aufgeholt“, weil die Weckung im Manager scheiterte, ist unbestätigt —, Marke je Seite) plus Backstop über den durablen Offen-Index — eine Obermenge der offenen Vorgänge, geschrieben als Beilage im Append des `ProzessGestartet` (eine Transaktion; Fristen im Sekundentakt, Heil-Weckung nach 15 s Stille mit Backoff bis 1 h) | ein Vorgang, dessen Start-Signal verloren ging, startet über den Poll; dessen Marke rückt nur bis vor die erste unbestätigte Weckung (G-8 erledigt). Ein still gewordener Vorgang ohne Ziel-Event (verlorene Selbst-Weckung bei unbekanntem Emit-Ausgang) wartet höchstens bis zur nächsten Heil-Weckung — nach langer Stille bis 1 h | P `ProzessPollGarantieTests` (unbestätigte Start-Weckung hält die Marke, kein Neustart beendeter Vorgänge, volle Seiten), `ProzessStartGarantieTests` (Eintrag und Ende reisen im Append von Start und Ende, Eintrag ohne Vorgang räumt ab); I `GarantiePostgresTests.Vorgangs_Beilagen_werden_mit_den_Events_gueltig…` (Rich und Quick), `HeilplanTests`; PD `AblaufGarantieTests.Ein_Neustart_des_Hosts…`; I `GarantiePostgresTests.Ein_gescheiterter_Append_blockiert_den_Poll_nicht`; E2E Störungslauf (harter Abbruch) |
| Kompensation in umgekehrter Reihenfolge | `ProzessManager` | scheitert sie, endet der Vorgang als `KlärungNötig` mit Dead-Letter; der Dead-Letter ist nur Beobachtung, kein Retry | PD `ProzessE2ETests.Scheitert_ein_Zweig…`; E2E S5 „jede Kompensation genau einmal“; `KlärungNötig` ohne Test (P2-5) |
| Prozess-Regeln sind azyklisch | Boot-Guard (`ProzessAzyklizität`) | – | P `FunktionsAufrufTests.Der_Azyklizitaets_Guard…` |
| Zeitlimits im Dirigenten feuern | Frist im Offen-Index, Backstop im Sekundentakt (PF-4) | Auflösung etwa 1 s; geht das Schreiben der Frist verloren, entdeckt die nächste Heil-Weckung den Ablauf | P `ProzessFristGarantieTests` (Frist = Ablauf, einmal geschrieben, fällig genau zum Ablauf, Aufräumen), `FunktionsAufrufTests.Ein_Zeitlimit_laesst_einen_haengenden_Aufruf_scheitern…`, `PipelineFlussTests.Bestellung_Zeitlimit_Port…`; I `PipelineFlussE2ETests` (Trainings-Wächter, Warten über die Frist) |
| eine Katalog-Funktion wird **mindestens einmal** ausgeführt | Laufort Extern: Lease 30 s beim `FunktionsVermittler`, nach Ausfall des Workers neu vergeben | **Funktionsrümpfe müssen idempotent sein**; ein Ergebnis nach Ablauf der Lease wird abgewiesen | P `FunktionsVermittlungTests.Abgelaufene_Lease…`, `…Erledigtes_verpufft…`; E2E S2 |
| **Fristen** (`IFristplan`) feuern mindestens einmal | `FristScheduler`: erst feuern, entfernt nur bei entschiedenem Ausgang (zugestellt oder abgelehnt); ein unbekannter Ausgang lässt die Frist für den nächsten Tick liegen | – | P `FristSchedulerTests`, `FristGarantieTests` (zugestellt, abgelehnt, Timeout, technischer Fehlschlag, doppeltes Feuern); I `DeadlineStorePostgresTests` |

## 11.8 Quellen

| Garantie | Mechanismus | Nachweis |
|---|---|---|
| **genau einmal je Kennung im Log** | `StartStream` mit deterministischer Id auf Version 0 (`Quellen`); ein zweites Melden ist idempotent, auch über den Webhook | P `DateiTreiberTests.Vorgangs_Id_folgt_aus_Typ_und_Kennung`, `…Treiber_meldet_neue_Dateien_genau_einmal…`; I `PipelineFlussE2ETests.Datei_Treiber_meldet_eine_Datei_genau_einmal_auch_nach_Neustart`; E2E S1/S2 „je Datei genau eine Quell-Nachricht“ |
| kein eigener Treiber-Stand, Neustart idempotent | beim Start fragt der Treiber den Log (`IStreamBestand`) | P `DateiTreiberTests.Schon_im_Log_bekannte_Dateien…`; I `PipelineFlussE2ETests.Datei_Treiber…_Neustart`; E2E Störungslauf |
| Rückstau | die nächste Meldung erst, wenn die vorige im Log steht | P `DateiTreiberTests.Rueckstau_die_naechste_Datei_erst…` |
| einmal im Cluster | Lease bei der Cluster-Identität `quelle/<Typ>` (`EinmalImCluster`) | P `DateiTreiberTests.Lease_hat_genau_einen_Halter…` |

„Gleich“ heißt gleiche **Kennung**; was als gleich gilt, legt die Konfig fest (`PfadGroesseZeit`, `NameGroesse`,
[konzept-werkbank.md](konzept-werkbank.md) §3.5).

## 11.9 Bewusst ohne Garantie

- **Signale:** dürfen verloren, doppelt oder ungeordnet sein (Inv. 2); ihr Verlust kostet nur Latenz.
- **Redis-Versionsindex** und **Snapshots:** Beschleuniger mit Voll-Read-Rückfall ([02](02-design-prinzipien.md) P1).
- **Ablehnungen an den Aufrufer**, **Kenntnis-Push an Clients**, UI-Feedback: schneller Kanal (Inv. 6), gemeldet (`Bote.Melde`, der Absender wartet
  nicht); wer es durabel braucht, fragt per Query nach.
  Heute erreicht eine fachliche Ablehnung einen Client gar nicht, dessen Vertrag ihren Typ nicht hört (G-12), und zwischen zwei Knoten können
  gezielte Antworten bei der falschen Sitzung landen (G-13) — beides mehr als „verlierbar“.
- **Dead-Letter:** best-effort, nur zur Beobachtung.

## 11.10 Garantien zur Build- und Startzeit

| Garantie | Wo | Nachweis |
|---|---|---|
| was ein Decide oder Handle erzeugen kann, steht vollständig in der Signatur | CQRS050 | P `AusgabeVertragAnalyzerTests` |
| jedes entschiedene Event hat ein `Apply`, jedes `Apply` ein entscheidendes Decide | CQRS079/080 | P `ModellAnalyzerTests` |
| ein Command gehört genau dem Aggregat in seinem Typ (`ICommand<TAggregat>`), sein Decide steht dort; ohne typisiertes Ziel baut kein Command; Identitäten sind eindeutig | Compiler (`ICommand.Ziel`), CQRS010/011, CQRS013 | P `BuildGarantieTests` (010/011, Routing aus dem Typ), `ModellAnalyzerTests` (013) |
| genau ein Emit-Weg, jeder Command-Send ist zeitlich gebunden | CQRS020/021 (CQRS020 auch für `FrageAsync<CommandResult>`); der generierte Pull-Emit-Pfad reicht das Token der Weckung durch (`EmitAusgaenge`) | P `CommandEmitAnalyzerTests` |
| keine Cluster-Nachricht ohne Frist: jede ist eine Meldung (verlierbar, intern gebunden) oder eine Frage (Pflicht-Frist) | eine Klasse, `Bote`; keine Stelle der Laufzeit ruft `Cluster.RequestAsync` selbst | P `BoteTests`, `LebenszyklusImClusterTests.Eine_Frage_an_einen_Actor_der_nie_antwortet…` (gegen Proto.Cluster, das ohne Frist unbegrenzt wiederholt) |
| Store-Zugriff nur über Fähigkeiten, Konsumenten ohne eigenen Zustand | CQRS051–055, CQRS066/067, CQRS083 | P `FaehigkeitAnalyzerTests` (051, 054, 055), `BuildGarantieTests` (052, 053), `StoreZugriffAnalyzerTests`, `ModellAnalyzerTests` (083) |
| jeder Wire-Typ ist serialisierbar, ohne Reflection | Generatoren (namentliche Kontext-Referenz), `WireSerializerBootCheck` | P `BuildGarantieTests.Jeder_Wire_Typ_der_Anwendung_ist_serialisierbar` |
| Akteur-Rechte, Dienst- und Funktions-Bindung, Umgebungs-Schema, Quellen-Konfig | Boot-Guards ([02](02-design-prinzipien.md) P9) | P `AkteurRechteTests`, `DienstBindungTests`, `UmgebungLaderTests`, `DateiTreiberTests.Konfig_…` |
| eine Datenbank gehört genau einer Umgebung | `UmgebungsSiegel` | I `UmgebungE2ETests.Sim_Host_startet_nicht_gegen_eine_Datenbank_mit_Siegel_Prod` |

## 11.11 Was daraus für den Fachcode folgt

1. Eine echte Invariante gehört in **ein** Aggregat. Was über mehrere Aggregate gilt, ist eventual und braucht im Fehlerfall eine Kompensation.
2. Projektionen nehmen keine Ordnung über Streams an.
3. Funktionsrümpfe und Seiteneffekte in Reaktionen sind idempotent.
4. Clients wiederholen mit derselben CommandId; ohne Wiederholung gilt für sie höchstens einmal.
5. Handler von replaybaren Projektionen sind deterministisch, sonst ist ein Neuaufbau nicht derselbe Stand.

## 11.12 Was die Garantien kosten

Die meisten Kosten hängen nicht an der Garantie, sondern am Mechanismus, der sie heute trägt. Die Tabelle nennt je Garantie den Mechanismus,
wie seine Kosten wachsen, und den Hebel, der dieselbe Garantie billiger trüge; offen sind die Hebel in
[13](13-reifegrad-schulden-bewertung.md) §13.0 (Reihe **PF**). Zahlen gehören in [12](12-tests-und-vermessung.md); hier steht nur, womit
die Kosten wachsen.

| Garantie | Mechanismus heute | Kosten wachsen mit | Hebel bei gleicher Garantie |
|---|---|---|---|
| Projektion wirkt genau einmal (§11.5) | Batch-Co-Commit: je Seite (bis 500 Events einer Partition) eine Abfrage für die Guards und eine Transaktion für Effekte, Guards und Partition (`BatchLeser`, `MartenCoCommitStoreBase`) | Seiten, nicht mehr Events × Projektionen; unter Last je Weckung eine kleine Seite | – (umgesetzt, PF-1) |
| Ordnung je Stream (§11.4) | Projektion: ein Leser je (Projektion, Partition), P fest (Standard 8); Emittent: ein virtueller Actor je (Konsument, Stream), passiviert nach Leerlauf (PF-5) | Projektionen × P Leser; Emittenten × aktive Streams Actors | – (umgesetzt, PF-1) |
| Vollständigkeit trotz verlierbarem Signal (§11.5) | Projektion: der Poll weckt alle 30 s jede Partition, der Leser liest seitenweise ab seiner Position (eine Abfrage, im Leerlauf leer); Emittent: Poll über `ReadChangedStreamsAsync` seitenweise, nur Streams mit abonniertem Event, ohne Daten (`Poller`, `PullPath`) | Projektionen × P Abfragen je 30 s; beim Emittenten eine Abfrage je 1 000 abonnierte Events und je Emittent | – (umgesetzt, PF-11/PF-12) |
| Emittierter Command wirkt genau einmal (§11.3, §11.6) | durabler Emittenten-Cursor, vorgerückt nach bestätigtem Emit; die Commands eines Tails werden gemeinsam abgewartet | Tail-Länge je Weckung (nicht mehr die Stream-Länge) | – (umgesetzt) |
| Command wirkt genau einmal (§11.3) | Inbox-Tabelle: je Command eine Quittung (Primärschlüssel-Insert, je Group-Commit eine Anweisung) in der Transaktion der Events; Marke im Stream nur bei Noop und Ablehnung; ein Cache je Actor vor dem Append | Inbox-Zeilen je Horizont (30 Tage, B-Baum auf der Zeit, Aufräumen alle 10 min ab der letzten Grenze — je Seite, was sie löscht, PF-27); eine Wiederholung, die der Cache nicht kennt, rollt den Group-Commit zurück (danach einzeln) | – (PF-3, PF-27 erledigt) |
| kein Vorgang bleibt liegen (§11.7) | Prozess-Poll liest das Log seitenweise und routet die geladenen Events (`ProzessPoll`; offen oder nicht je Seite über den Schlüssel, PF-25); Backstop: fällige Fristen (eine Abfrage je Sekunde), Heil-Weckung nach Stille mit Backoff bis 1 h (`Heilplan`) | Events im Poll-Fenster; offene Vorgänge / 1 h im Leerlauf; der Abgleich des Heil-Plans liest alle 15 s die ganze Liste der offenen Vorgänge | Heilung als Frist im Offen-Index statt Liste im Speicher (PF-25, Rest); Lease im Cluster offen |
| Vorgang wirkt genau einmal und sieht jedes eigene Ergebnis (§11.7) | Fold je Ziel-Stream ab dem Cursor, nur die eigene Korrelation, Cursor bis zum Kopf; ohne Cursor ab dem Beginn des Vorgangs; die Kompensation liest aus dem Marking; Marking durabel beim Stillwerden und beim Stoppen (PF-23, PF-24) | Events, die seit dem Start bzw. seit der letzten Weckung in die Ziel-Streams kamen — nicht ihre Geschichte | – (umgesetzt) |
| Read-your-writes, best-effort (§11.5) | ein Redis-Write je Event und Projektion (`ReadModelDepsWriter`), Schlüssel mit Ablauf (Standard 24 h, PF-30) | Events × Projektionen; der Speicher mit den in der Haltbarkeit geschriebenen Aggregaten und Read-Models | je Projektion abschaltbar (PF-6) |
| Serialisierbar je Aggregat (§11.2) | der Actor wartet in jedem Turn auf seinen Commit | Commit-Latenz begrenzt den Durchsatz **eines** Streams | grundsätzlich; Abhilfe ist Modellierung (keine heißen Aggregate), Pipelining wäre ein eigener Entwurf |
| Quelle genau einmal je Kennung (§11.8) | ein Stream je Quell-Nachricht, ein Vorgang je Stream | Quell-Nachrichten: je Nachricht ein Stream, Dirigent und Actors | Designentscheidung, keine reine Optimierung: Dedup-Tabelle je Kennung und Sammel-Streams verlören „ein Vorgang je Meldung“ (PF-7) |

Kostenlos zur Laufzeit sind die Garantien aus §11.10 (Build und Start) sowie Signal, Publish und Snapshot (fire-and-forget bzw. best-effort).

## 11.13 Lücken

Stehen nur in [13](13-reifegrad-schulden-bewertung.md) §13.0: **G-5** Versionszählung bei Split-Upcasts, **G-6** Garantien ohne eigenen
Nachweis-Test, **G-11** eine Projektion übersprang in einem Störungslauf zwei Events (einmal beobachtet, Ursache und Test offen), **G-12** eine
fachliche Ablehnung erreicht einen Client nie, dessen Vertrag ihren Typ nicht hört, **G-13** zwei Knoten vergeben dieselben Sitzungs-Kennungen
(gezielte Antworten können bei der falschen Sitzung landen), **G-14** read-your-writes greift nicht für ein eben angelegtes Objekt. Jede offene Lücke hat einen Test, der sie zeigt (Skip `G-n offen (docs/13)`); geschlossen ist sie erst, wenn ihr Test grün ist.
Geschlossen: **G-1** Fristverlust, **G-7** Tombstones legen den Poll still, der Emittenten-Anteil von **G-2** (Phase 1); **G-3** Actor nach
Append-Konflikt (Phase 2); **G-4** Projektions-Checkpoint ohne Versionsprüfung (Phase 3, Batch-Konsum); **G-8** Prozess-Poll ohne bestätigte
Weckung (Phase 5, `ProzessPollGarantieTests`); **G-2** Inbox-Fenster (Phase 4, Inbox als Tabelle: Fenster → Horizont); **G-9** eine lange
offene Transaktion überholt die Positionen (Horizont der Vollständigkeit statt Karenz, `SequenceGapPostgresTests.Eine_lange_offene_Transaktion…`); **G-10** Zeitlimits rechneten ab dem Event-Zeitstempel (Uhr des Hosts)
gegen die DB-Uhr (jetzt setzt die Datenbank den Zeitstempel mit derselben Uhr, `EventZeitPostgresTests`).

## 11.14 Wie die Garantien geprüft werden

- **Garantie-Suite:** alle Tests mit `[Trait("Kategorie", "Garantie")]` — im Prüfstand die Logik über Seams, in Integration und Prüfdomäne die
  Store-Semantik gegen echtes Marten. Sie läuft vor jedem Commit:
  `dotnet test Infrastructure.Pruefstand.Tests/… --filter Kategorie=Garantie`, danach nacheinander dieselbe Filterung auf
  `Infrastructure.Integration.Tests` und `Pruefdomaene.Integration.Tests`. Grün heißt: alles grün bis auf die übersprungenen Lücken-Tests.
- **Unter Störung** geprüft sind: doppelte und ungeordnete Zustellung bzw. Seiten und Nachzügler (`KonsumGarantieTests`,
  `BatchKonsumGarantieTests`, `SequenceGapPostgresTests`, `SchreibGarantieTests`), verlorenes Signal (`PollerBackstopPostgresTests`), Absturz
  mitten im Batch (`CoCommitPostgresTests`, `BatchKonsumGarantieTests`), Absturz zwischen Emit und Cursor (`KonsumGarantieTests`,
  `AblaufGarantieTests`), Timeout und Fehlschlag beim Emit (`EmitGarantieTests`, `FristGarantieTests`, `KonsumGarantieTests`), Überlast eines
  Ziels (`EmitGarantieTests`: Drossel, Backoff), Versionskonflikt, mehrdeutiger Commit, Wiederholung nach Neustart und mitten im Group-Commit (`SchreibGarantieTests`), Konflikt an geteilten
  Dokumenten zwischen Partitionen (`SchreibRegelPostgresTests`, `GeteilteDokumentePostgresTests`), Passivierung und Reaktivierung
  (`LebenszyklusGarantieTests`), alte CommandId jenseits des Inbox-Caches und der Horizont der Inbox (`SchreibGarantieTests`), zwei Aktivierungen und Neuaufbau gegen einen laufenden
  Leser (`GarantiePostgresTests`, `BatchKonsumGarantieTests`), Poison-Stream und gescheiterter Append (`GarantiePostgresTests`,
  `BatchKonsumGarantieTests`), Neustart des Hosts (`AblaufGarantieTests`, `RehydrationGarantieTests`).
- **Ende zu Ende:** jeder Lauf von `scripts/vermessung-e2e.sh` zählt nach dem Lauf über Queries und den Log nach, dass jede Wirkung genau
  einmal eingetreten ist (je Szenario, dazu global: keine CommandId zweimal gewirkt, keine Version doppelt; mit offenem Rückstand „höchstens
  einmal“); der **Störungslauf** tut das nach harten Host-Abbrüchen und einem Redis-Ausfall ([12](12-tests-und-vermessung.md) §12.5). Ein Fehlschlag macht den Lauf ungültig.
