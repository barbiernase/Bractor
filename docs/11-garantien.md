# 11 — Garantien

Stand 2026-10-16, gegen den Code geprüft. Maßstab: docs/konzept-werkbank.md.

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
(§11.14). Je Garantie genau ein Nachweis je Teil des Mechanismus; wo Logik und Store-Semantik getrennt zu beweisen sind, zwei. Form:
**P** `Klasse.Methode` Prüfstand (`Infrastructure.Pruefstand.Tests`, store-frei über Seams), **I** Integration (`Infrastructure.Integration.Tests`,
echtes Marten), **PD** Prüfdomäne (`Pruefdomaene.Integration.Tests`, echter Cluster); ein Methodenname darf mit `…` abgekürzt sein, ohne
Methode zählt die ganze Klasse. **E2E** = die Exactly-once-Prüfung der Läufe von `scripts/vermessung-e2e.sh` ([12](12-tests-und-vermessung.md)
§12.5). Der Prüfstand-Test `GarantieMatrixTests` hält diese Spalte gegen den Code: jeder genannte Nachweis existiert, jeder Test in einem
Ordner `Garantien/` steht hier. Eine Garantie ohne Nachweis ist eine Lücke in [13](13-reifegrad-schulden-bewertung.md).

## 11.2 Schreibseite (Command → Events)

| Garantie | Mechanismus | Bedingung / Lücke | Nachweis |
|---|---|---|---|
| **Atomar:** alle Events eines Commands und seine Quittung in der Inbox werden gemeinsam gültig oder keines | ein Append, eine Transaktion (`AggregateActorBase`, `BatchingEventAppender`, `InboxTabelle`); in beiden Anhänge-Modi von Marten (der Host läuft mit `Quick`) | – | I `GarantiePostgresTests.Effekt_und_Quittung_werden_gemeinsam_gueltig_oder_gar_nicht` (Rich und Quick) |
| **Durabel nach Erfolg:** nach dem Append wird nie ein Fehler gemeldet ([02](02-design-prinzipien.md) P10) | ein Fehler nach dem Append rehydriert den Actor und quittiert Erfolg; scheitert der Append selbst, rehydriert der Actor und quittiert, was laut Inbox entschieden ist (die Quittung reist in derselben Transaktion) | so durabel wie Postgres (fsync, Platte, Replikation) | PD `SchreibGarantieTests.Ein_mehrdeutiger_Commit…`; der Pfad „Fehler nach dem Append im Actor“ ohne eigenen Test (G-6) |
| **Serialisierbar je Aggregat:** jede Entscheidung sieht den aktuellen Stand | Single-Writer-Actor je Stream (virtuelle Cluster-Identität) plus Versionsprüfung beim Append; nach einem Append-Konflikt rehydriert der Actor frisch und entscheidet einmal neu | – | PD `SchreibGarantieTests.Im_Buendel_entscheidet…` (Entscheidung auf dem Stand des vorigen); I `GarantiePostgresTests.Zwei_Schreiber…` (Versionsprüfung unter echter Nebenläufigkeit, Rich und Quick); PD `SchreibGarantieTests.Nach_einem_Versionskonflikt…` (frisch rehydrieren, neu entscheiden); PD `SchreibGarantieTests.Ein_zweiter_Schreiber_mitten_im_Ansturm…` (Konflikt mitten im Bündel) |
| **Bündel:** Commands, die während eines Commits ankommen, entscheiden nacheinander im Speicher (jeder auf dem Stand nach dem vorigen) und reisen in einem Append | `AggregateActorBase` (höchstens `Schreibpfad.BuendelGroesse`, Standard 64); ein Konflikt lässt das Bündel scheitern, danach einzeln | – | PD `SchreibGarantieTests.Ein_heisser_Posten_buendelt…` |
| **Group-Commit:** ein Fehler im gemeinsamen Commit vieler Aggregate trifft nur den Schuldigen | `BatchingEventAppender`: scheitert der Batch, schreibt er jeden Auftrag einzeln | – | P `AppendBatchingTests` |
| **Passivierung ist folgenlos:** ein Actor, der nach Leerlauf stoppt, wird mit demselben Stand, derselben Inbox und demselben Cursor reaktiviert | durabler Zustand nur im Log, Snapshot, Inbox-Tabelle, Marke und Cursor; Snapshot beim Stoppen (PF-5); der Inbox-Cache des Actors ist danach leer, eine Wiederholung fängt der Schlüssel der Tabelle | – | PD `LebenszyklusGarantieTests` |
| **Client-OCC:** ein Client-Command mit behaupteter Version (≥ 0) scheitert, wenn das Aggregat sich seitdem geändert hat | Vorprüfung (`CommandVorpruefung`), Versionsprüfung beim Append | – | P `CommandVorpruefungTests.Frischer_Command_stale_Version_ist_Konflikt` |
| **Emittierte Commands** (Reaktion, Prozess, Fluss, Zusage mit Kausalität, Client mit Version < 0) entscheiden auf dem **aktuellen** Stand | `CommandModus.Emittiert`: keine erwartete Version | gewollt: der Sender behauptet keinen Stand | PD `ProzessE2ETests.Nebenlaeufige_Vorgaenge…` |
| **Ablehnungen** stehen nicht im Log; durabel ist nur die Marke `KommandoAbgelehnt` | `ITransientEvent` | – | PD `ProzessE2ETests.Scheitert_ein_Zweig…`; P `RehydrationGarantieTests.Neustart_zaehlt_die_Marken…` (Marken zählen in der Version, wirken nicht) |
| Invarianten **über mehrere Aggregate** | – | **keine Garantie**; nur eventual über Prozesse und Kompensation | – |

## 11.3 Zustellung und Idempotenz von Commands

| Weg | Zustellung | Wirkung | Bedingung / Lücke | Nachweis |
|---|---|---|---|---|
| **Client** (gRPC, `CqrsClientService`) | fire-and-forget über `ProtoActorAggregateDispatcher`: bis zu 3 Versuche je 10 s, dann Dead-Letter und `CommandFailed` an die Sitzung | **genau einmal, wenn der Client mit derselben CommandId wiederholt**; die Inbox prüft vor der Version | Ein Command, der den Host vor dem Dispatch verliert (Absturz), ist weg; der Client merkt es nur am fehlenden Ack und muss selbst wiederholen. Ohne Wiederholung gilt höchstens einmal. | P `CommandVorpruefungTests.Bereits_verarbeitet…` (Inbox vor Version) |
| **Client mit Antwort** (JSON-Eingang, `FrageAsync`; optional `commandId` im Body) | ein Versuch, gebunden | wie oben | „keine Antwort“ heißt nicht „nicht gewirkt“ | I `EingangE2ETests.Client_wiederholt_mit_derselben_CommandId…`; E2E Störungslauf (Wiederholung über Host-Abbrüche) |
| **Ablehnung** | – | **idempotent:** eine Wiederholung liefert dieselbe Ablehnung, auch wenn sie jetzt wirken könnte | Quittung „abgelehnt“ in der Inbox (dazu die Marke `KommandoAbgelehnt` für den Prozess-Fold); innerhalb des Horizonts | P `CommandVorpruefungTests.Bereits_abgelehnt…` (Cache des Actors); PD `SchreibGarantieTests.Eine_Wiederholung_nach_dem_Neustart…` (Inbox-Tabelle) |
| **Emittiert** (`CommandEmitter`) | at-least-once, gebunden (5 s), höchstens 8 Sends je Ziel-Aggregat gleichzeitig (Drossel); der Emitter wiederholt nicht selbst, er meldet den **Ausgang** (zugestellt, abgelehnt, unbekannt). Bei „unbekannt“ wiederholt der Aufrufer mit derselben CommandId: die Frist bleibt liegen, der Emittenten-Cursor rückt nicht vor, der Dirigent wiederholt nach Backoff | **genau einmal** über die deterministische CommandId und die Inbox des Ziels (eindeutiger Schlüssel je CommandId, in der Transaktion der Events) | innerhalb des Horizonts `Schreibpfad.InboxHorizontTage` (Standard 30 Tage, E2); danach vergisst die Inbox die CommandId | P `EmitGarantieTests.Der_Ausgang…`, P `EmitGarantieTests.Die_Drossel…`, P `EmitGarantieTests.Eine_volle_Drossel…` (Ausgang, Drossel); PD `SchreibGarantieTests.Doppelt_und_gleichzeitig…` (genau einmal); PD `SchreibGarantieTests.Ein_Duplikat_mitten_im_Group_Commit…` (die Inbox-Tabelle fängt, was der Cache vergessen hat); PD `SchreibGarantieTests.Die_Inbox_vergisst…` (Horizont); E2E „keine CommandId zweimal gewirkt“ |

## 11.4 Ordnung

| Garantie | Gilt | Mechanismus / Folge | Nachweis |
|---|---|---|---|
| Events eines Streams werden in jedem Konsumenten in Versionsreihenfolge angewendet | **ja** | Projektion: ein Stream liegt in genau einer Partition, ein Leser je Partition liest nach globaler Sequenz (je Stream = Versionsreihenfolge), Guard je Stream; Emittent: ein Actor je (Konsument, Stream), Vorab-Prüfung der Version | P `BatchKonsumGarantieTests.Doppelte_und_ungeordnete_Seiten…` (Projektion); P `KonsumGarantieTests.Doppelte_und_ungeordnete_Weckungen…` (Emittent); I `SequenceGapPostgresTests.Batch_Leser_ueberholt…` (Nachzügler mit echter Commit-Reihenfolge) |
| globale Ordnung über Streams | **nein** | die globale Sequenz von Marten ist nur Lese-Cursor (Poll, Partitions-Leser), kein Versprechen; Partitionen laufen parallel | – |
| kausale Ordnung über Streams (Ursache vor Wirkung) | **nein** | eine Projektion über X und Y kann die Wirkung in Y vor der Ursache in X sehen | – |
| Prozesse und Flüsse | ordnungsunabhängig | der Dirigent faltet eine Markierung (Petri-Netz); welche Bedingung zuerst eintrifft, ändert das Ergebnis nicht | PD `MarkingCursorAequivalenzTests` (kalt, verzögert, mit Router — dieselben Feuerungen) |

## 11.5 Projektionen (replaybar)

| Garantie | Mechanismus | Bedingung / Lücke | Nachweis |
|---|---|---|---|
| **Wirkung genau einmal je Event** | Batch-Co-Commit: die Effekte aller Streams eines Batches, ihre Guards (`ProjectionCheckpoint`) und der `PartitionsCheckpoint` in einer Transaktion (`MartenCoCommitStoreBase`); der GA-1-Check erzwingt den Co-Commit für append-artige Projektionen | – | I `CoCommitPostgresTests` (Absturz mitten im Batch, viele Streams in einem Commit); P `GaEinsPruefungTests`; E2E „Read-Model = Log“ |
| **Zwei Aktivierungen** derselben Partition (Netzstörung im Cluster) wenden keinen Effekt doppelt an | der Partitions-Checkpoint wird mit erwarteter Version geschrieben (`Insert` bzw. `UpdateRevision`); die verlierende Aktivierung lädt neu, der Guard überspringt das schon Angewandte (G-4 geschlossen) | – | I `GarantiePostgresTests.Zwei_Aktivierungen…` |
| **geteilte Dokumente** ohne verlorene Updates | Versionsprüfung; ein Konflikt verwirft den Batch, der Leser halbiert ihn nach Streams und wiederholt einen einzelnen bis zu fünfmal, danach bleibt er bis zum nächsten Durchlauf stehen (die Position hält vor ihm); im selben Batch sehen sich zwei Streams über die Identity-Map | – | I `SchreibRegelPostgresTests`; I `GeteilteDokumentePostgresTests` (die generierte Registrierung, vier Leser); P `BatchKonsumGarantieTests.Ein_dauerhafter_Konflikt…` |
| **Vollständigkeit:** jedes committete Event kommt an | Signal weckt die Partition (schnell), der Poll weckt alle 30 s jede Partition; der Leser liest ab seiner durablen Position, die nur bis zur gesetzten Sequenz (älter als die Karenz) vorrückt — bestimmt vor dem Lesen der Seite, in derselben Abfrage wie „steht dahinter etwas?“ (ohne Neues keine Seite, PF-14). Tombstones eines gescheiterten Appends liest niemand (`MartenEventStore`; der Host schreibt im Anhänge-Modus `Quick` keine mehr) | Grenzfall: eine Transaktion, die länger als die Karenz (3 s) offen ist, kann die Position überholen; dann trägt nur das Signal. Die Karenz misst den Event-Zeitstempel (Client-Uhr) gegen die DB-Uhr. | I `PollerBackstopPostgresTests.Batch_Leser_holt…` (verlorenes Signal); I `SequenceGapPostgresTests.Hinter_der_Marke_nichts…` (Karenz); P `BatchKonsumGarantieTests.Eine_volle_Seite…`; I `GarantiePostgresTests.Ein_gescheiterter_Append…` (Tombstones); E2E Störungslauf |
| **Ein Poison-Stream hält niemanden auf** | der Stream des werfenden Events wird abgetrennt, ein werfender Commit halbiert; wirft er allein, wird er geparkt (im Partitions-Checkpoint, Dead-Letter einmal), je Poll-Intervall einzeln versucht und nach der Heilung ab seinem Guard nachgeholt | sichtbar in `/health` (`geparkteStreams`) und als Dead-Letter | I `GarantiePostgresTests.Ein_Poison_Stream_wird_im_Batch_geparkt…` |
| **Aktualität** | Signal | eventual: typisch Millisekunden, schlimmstenfalls ein Poll-Intervall | E2E Latenz ([12](12-tests-und-vermessung.md) §12.5) |
| **Read-your-writes** | Redis-Versionsindex (`ctx.Track`) | **nur best-effort**; fällt Redis aus, verlängert sich nur die Wartezeit | E2E Störungslauf (Redis 10 s weg, Exactly-once bleibt) |
| **Neuaufbau** | Ziel leeren, Partitionen (neue Version, Position 0) und Guards zurücksetzen, dann je Partition der Batch-Leser ab 0, seitenweise (`ProjectionRebuilder`; in Sim `ProjektionsStandDienst` bei geändertem Code oder geänderter Partitionszahl); ein noch laufender Leser verliert seinen Commit | deterministisch nur bei deterministischen Handlern; der alte Stand wird vorher geleert | P `ProjectionRebuilderTests` (ohne Verdopplung); I `GarantiePostgresTests.Ein_Neuaufbau…` (laufender Leser); I `LeseseitenStandE2ETests`, P `ProjektionsStandTests` (Auslöser in Sim); E2E Neuaufbau |

## 11.6 Reaktionen (emittierend)

| Garantie | Bedingung | Nachweis |
|---|---|---|
| der Handler läuft **mindestens einmal** je Event; Signal und Poll lesen ab dem Cursor, der erst vorrückt, wenn jeder ausgegebene Command zugestellt oder abgelehnt ist (die Commands eines Tails gehen parallel hinaus) | – | P `KonsumGarantieTests.Ein_nicht_zugestellter_Emit…`, P `KonsumGarantieTests.Ein_abgelehnter_Command…`, P `KonsumGarantieTests.Die_Commands_eines_Tails…`; I `GarantiePostgresTests.Ein_Poison_Stream_haelt_die_Hochwassermarke…`, I `PollerBackstopPostgresTests.Der_Poll_weckt_nur…` (Emittenten-Poll) |
| ausgegebene Commands wirken **genau einmal** | Inbox des Ziels, innerhalb des Horizonts (§11.3) | PD `AblaufGarantieTests.Absturz_zwischen_Emit_und_Cursor…`; P `EmitIdTests`; E2E „je Reaktions-Command genau eine Wirkung“ |
| **kein** Reset und kein Replay | zur Compile-Zeit wahr: der Emittenten-Pfad (`EmittentenAdapter`) nimmt keinen Tracker, `IEmittentenCursor` hat kein Reset | Typ (Compile-Zeit) |
| ausgegebene Events (`IEvent` aus einem Handle) | verlierbar re-publiziert, nicht im Log | – |
| Seiteneffekte **außerhalb** von Commands | **nur at-least-once**, keine Deduplizierung | – |

## 11.7 Prozesse, Flüsse, Funktionen, Fristen

| Garantie | Mechanismus | Bedingung / Lücke | Nachweis |
|---|---|---|---|
| jede Transition feuert mindestens einmal und wirkt genau einmal | deterministische CommandId, Inbox des Ziels; der Fold erkennt eine Wirkung an ihrem Event (`causation_id`), einen Noop an der Marke `KommandoVerarbeitet`, eine Ablehnung an `KommandoAbgelehnt` | Horizont der Inbox (§11.3) | PD `ProzessE2ETests.Diamant…`, PD `ProzessE2ETests.Fan_out…`; PD `MarkingCursorAequivalenzTests` (Cursor-Fold = Voll-Fold); P `FunktionsAufrufTests.Das_Ergebnis_einer_Funktion…`; E2E Verteilt, Heißer Schreiber |
| der Dirigent bleibt unter Überlast lebendig: ein unbekannter Send wird erst nach Backoff je Vorgang (1 s … 30 s) wiederholt, nie zweimal gleichzeitig; höchstens 8 Sends je Ziel-Aggregat unterwegs | `DetachedProzessSend`, `EmitDrossel` | – | P `EmitGarantieTests.Nach_unbekanntem_Ausgang…`, P `EmitGarantieTests.Nach_entschiedenem_Ausgang…`; E2E Heißer Schreiber |
| kein Vorgang bleibt still liegen | Prozess-Poll (seitenweise ab der Marke, bestätigte Weckungen, Marke je Seite) plus Backstop über den durablen Offen-Index — eine Obermenge der offenen Vorgänge, geschrieben als Beilage im Append des `ProzessGestartet` (eine Transaktion; Fristen im Sekundentakt, Heil-Weckung nach 15 s Stille mit Backoff bis 1 h) | ein Vorgang, dessen Start-Signal verloren ging, startet über den Poll; dessen Marke rückt nur bis vor die erste unbestätigte Weckung (G-8 erledigt). Ein still gewordener Vorgang ohne Ziel-Event (verlorene Selbst-Weckung bei unbekanntem Emit-Ausgang) wartet höchstens bis zur nächsten Heil-Weckung — nach langer Stille bis 1 h | P `ProzessPollGarantieTests`, P `ProzessPollFilterTests` (Poll); P `ProzessStartGarantieTests` (Offen-Index im Append); I `GarantiePostgresTests.Vorgangs_Beilagen…` (atomar, Rich und Quick); P `HeilplanTests` (Backoff); PD `AblaufGarantieTests.Ein_Neustart_des_Hosts…`; E2E Störungslauf |
| Kompensation in umgekehrter Reihenfolge | `ProzessManager` | scheitert sie, endet der Vorgang als `KlärungNötig` mit Dead-Letter; der Dead-Letter ist nur Beobachtung, kein Retry | PD `ProzessE2ETests.Scheitert_ein_Zweig…`; P `FunktionsAufrufTests.Scheitert_ein_spaeterer_Schritt…`; `KlärungNötig` ohne Test (P2-5) |
| Prozess-Regeln sind azyklisch | Boot-Guard (`ProzessAzyklizität`) | – | P `FunktionsAufrufTests.Der_Azyklizitaets_Guard…`, P `PipelineFlussTests.Ein_Fluss_ist_azyklisch…` |
| Zeitlimits im Dirigenten feuern | Frist im Offen-Index, Backstop im Sekundentakt (PF-4) | Auflösung etwa 1 s; geht das Schreiben der Frist verloren, entdeckt die nächste Heil-Weckung den Ablauf | P `ProzessFristGarantieTests`; P `FunktionsAufrufTests.Ein_Zeitlimit…`, P `PipelineFlussTests.Bestellung_Zeitlimit_Port…` |
| eine Katalog-Funktion wird **mindestens einmal** ausgeführt | Laufort Extern: Lease 30 s beim `FunktionsVermittler`, nach Ausfall des Workers neu vergeben | **Funktionsrümpfe müssen idempotent sein**; ein Ergebnis nach Ablauf der Lease wird abgewiesen | P `FunktionsVermittlungTests.Abgelaufene_Lease…`, P `FunktionsVermittlungTests.Erledigtes_verpufft…`; I `PipelineFlussE2ETests.Bildeingang_mit_Python_Worker…` |
| **Fristen** (`IFristplan`) feuern mindestens einmal | `FristScheduler`: erst feuern, entfernt nur bei entschiedenem Ausgang (zugestellt oder abgelehnt); ein unbekannter Ausgang lässt die Frist für den nächsten Tick liegen | – | P `FristGarantieTests`, P `FristSchedulerTests`; I `DeadlineStorePostgresTests`, I `UmgebungE2ETests.Uhr_Versatz…` |

## 11.8 Quellen

| Garantie | Mechanismus | Nachweis |
|---|---|---|
| **genau einmal je Kennung im Log** | `StartStream` mit deterministischer Id auf Version 0 (`Quellen`); ein zweites Melden ist idempotent, auch über den Webhook | P `DateiTreiberTests.Vorgangs_Id_folgt…`, P `DateiTreiberTests.Treiber_meldet_neue_Dateien…`, P `DateiTreiberTests.Gemeldet_wird_nicht_wiederholt…`; I `PipelineFlussE2ETests.Datei_Treiber_meldet…`; E2E Bildeingang |
| kein eigener Treiber-Stand, Neustart idempotent | beim Start fragt der Treiber den Log (`IStreamBestand`) | P `DateiTreiberTests.Schon_im_Log…`; I `PipelineFlussE2ETests.Datei_Treiber_meldet…` (Neustart) |
| Rückstau | die nächste Meldung erst, wenn die vorige im Log steht | P `DateiTreiberTests.Rueckstau…` |
| einmal im Cluster | Lease bei der Cluster-Identität `quelle/<Typ>` (`EinmalImCluster`) | P `DateiTreiberTests.Lease…` |

„Gleich“ heißt gleiche **Kennung**; was als gleich gilt, legt die Konfig fest (`PfadGroesseZeit`, `NameGroesse`,
[konzept-werkbank.md](konzept-werkbank.md) §3.5).

## 11.9 Bewusst ohne Garantie

- **Signale:** dürfen verloren, doppelt oder ungeordnet sein (Inv. 2); ihr Verlust kostet nur Latenz.
- **Redis-Versionsindex** und **Snapshots:** Beschleuniger mit Voll-Read-Rückfall ([02](02-design-prinzipien.md) P1).
- **Ablehnungen an den Aufrufer**, **Kenntnis-Push an Clients**, UI-Feedback: schneller Kanal (Inv. 6); wer es durabel braucht, fragt per Query nach.
- **Dead-Letter:** best-effort, nur zur Beobachtung.

## 11.10 Garantien zur Build- und Startzeit

| Garantie | Wo | Nachweis |
|---|---|---|
| was ein Decide oder Handle erzeugen kann, steht vollständig in der Signatur | CQRS050 | P `AusgabeVertragAnalyzerTests` |
| jedes entschiedene Event hat ein `Apply`, jedes `Apply` ein entscheidendes Decide | CQRS079/080 | P `ModellAnalyzerTests` |
| ein Command gehört genau einem Aggregat, Identitäten sind eindeutig | CQRS010/011 | P `BuildGarantieTests.Ein_Command_mit_zwei_Aggregaten…`, P `BuildGarantieTests.Zwei_Aggregate…` |
| genau ein Emit-Weg, jeder Command-Send ist zeitlich gebunden | CQRS020/021; der generierte Pull-Emit-Pfad reicht das Token der Weckung durch (`EmitAusgaenge`) | P `CommandEmitAnalyzerTests` |
| Store-Zugriff nur über Fähigkeiten, Konsumenten ohne eigenen Zustand | CQRS051–055, CQRS066/067, CQRS083 | P `FaehigkeitAnalyzerTests` (051, 054, 055), P `BuildGarantieTests.Ein_Konsument…`, P `BuildGarantieTests.Eine_Faehigkeit…` (052, 053), P `StoreZugriffAnalyzerTests` (066, 067), P `ModellAnalyzerTests.Ein_Konsument_mit_Zustand…` (083) |
| jeder Wire-Typ ist serialisierbar, ohne Reflection | Generatoren (namentliche Kontext-Referenz), `WireSerializerBootCheck` | P `BuildGarantieTests.Jeder_Wire_Typ…` |
| Akteur-Rechte, Dienst- und Funktions-Bindung, Umgebungs-Schema, Quellen-Konfig | Boot-Guards ([02](02-design-prinzipien.md) P9) | P `AkteurRechteTests`, P `DienstBindungTests`, P `UmgebungLaderTests`, P `DateiTreiberTests.Konfig_…`, P `PassivierungTests.Ein_zu_kurzer_Leerlauf…` |
| eine Datenbank gehört genau einer Umgebung | `UmgebungsSiegel` | I `UmgebungE2ETests.Sim_Host_startet_nicht…` |

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
| Command wirkt genau einmal (§11.3) | Inbox-Tabelle: je Command eine Quittung (Primärschlüssel-Insert, je Group-Commit eine Anweisung) in der Transaktion der Events; Marke im Stream nur bei Noop und Ablehnung; ein Cache je Actor vor dem Append | Inbox-Zeilen je Horizont (30 Tage, BRIN auf der Zeit, Aufräumen alle 10 min); eine Wiederholung, die der Cache nicht kennt, rollt den Group-Commit zurück (danach einzeln) | – (PF-3 erledigt) |
| kein Vorgang bleibt liegen (§11.7) | Prozess-Poll liest das Log seitenweise und routet die geladenen Events (`ProzessPoll`); Backstop: fällige Fristen (eine Abfrage je Sekunde), Heil-Weckung nach Stille mit Backoff bis 1 h (`Heilplan`) | Events im Poll-Fenster; offene Vorgänge / 1 h im Leerlauf | – (umgesetzt, PF-4); Lease im Cluster offen |
| Read-your-writes, best-effort (§11.5) | ein Redis-Write je Event und Projektion (`ReadModelDepsWriter`) | Events × Projektionen | je Projektion abschaltbar (PF-6) |
| Serialisierbar je Aggregat (§11.2) | der Actor wartet in jedem Turn auf seinen Commit | Commit-Latenz begrenzt den Durchsatz **eines** Streams | grundsätzlich; Abhilfe ist Modellierung (keine heißen Aggregate), Pipelining wäre ein eigener Entwurf |
| Quelle genau einmal je Kennung (§11.8) | ein Stream je Quell-Nachricht, ein Vorgang je Stream | Quell-Nachrichten: je Nachricht ein Stream, Dirigent und Actors | Designentscheidung, keine reine Optimierung: Dedup-Tabelle je Kennung und Sammel-Streams verlören „ein Vorgang je Meldung“ (PF-7) |

Kostenlos zur Laufzeit sind die Garantien aus §11.10 (Build und Start) sowie Signal, Publish und Snapshot (fire-and-forget bzw. best-effort).

## 11.13 Lücken

Stehen nur in [13](13-reifegrad-schulden-bewertung.md) §13.0: **G-5** Versionszählung bei Split-Upcasts (Test, der sie zeigt:
P `RehydrationGarantieTests.Split_Upcast…`, übersprungen mit `G-5 offen (docs/13)`), **G-6** der Pfad „Fehler nach dem Append im Actor“ ohne
eigenen Test. Eine offene Lücke hat, wo es geht, einen Test, der sie zeigt (Skip `G-n offen (docs/13)`); geschlossen ist sie erst, wenn ihr Test
grün ist. Geschlossen: **G-1** Fristverlust, **G-7** Tombstones legen den Poll still, der Emittenten-Anteil von **G-2** (Phase 1); **G-3** Actor
nach Append-Konflikt (Phase 2); **G-4** Projektions-Checkpoint ohne Versionsprüfung (Phase 3, Batch-Konsum); **G-8** Prozess-Poll ohne
bestätigte Weckung (Phase 5, `ProzessPollGarantieTests`); **G-2** Inbox-Fenster (Phase 4, Inbox als Tabelle: Fenster → Horizont).

## 11.14 Wie die Garantien geprüft werden

- **In den Ebenen der Test-Suite** ([12](12-tests-und-vermessung.md) §12.1): die Logik über Seams im Prüfstand (`./pruefe.sh schnell`), die
  Store-Semantik gegen echtes Marten in Integration und Prüfdomäne (`./pruefe.sh alles`). Es gibt keine eigene Garantie-Suite: jeder Nachweis
  läuft in seiner Ebene mit, grün heißt alles grün bis auf die übersprungenen Lücken-Tests. Neue Garantie-Tests gehören in einen Ordner
  `Garantien/` ihrer Ebene und in die Nachweis-Spalte oben — `GarantieMatrixTests` (Prüfstand) hält beides gleich.
- **Unter Störung** geprüft sind: doppelte und ungeordnete Zustellung bzw. Seiten und Nachzügler (`KonsumGarantieTests`,
  `BatchKonsumGarantieTests`, `SequenceGapPostgresTests`, `SchreibGarantieTests`), verlorenes Signal (`PollerBackstopPostgresTests`), Absturz
  mitten im Batch (`CoCommitPostgresTests`), Absturz zwischen Emit und Cursor (`AblaufGarantieTests`), Timeout und Fehlschlag beim Emit
  (`EmitGarantieTests`, `FristGarantieTests`, `KonsumGarantieTests`), Überlast eines Ziels (`EmitGarantieTests`: Drossel, Backoff),
  Versionskonflikt, mehrdeutiger Commit, Wiederholung nach Neustart und mitten im Group-Commit, Horizont der Inbox (`SchreibGarantieTests`),
  Konflikt an geteilten Dokumenten zwischen Partitionen (`SchreibRegelPostgresTests`, `GeteilteDokumentePostgresTests`), Passivierung und
  Reaktivierung (`LebenszyklusGarantieTests`), zwei Aktivierungen und Neuaufbau gegen einen laufenden Leser, Poison-Stream und gescheiterter
  Append (`GarantiePostgresTests`), Neustart des Hosts (`AblaufGarantieTests`, `RehydrationGarantieTests`).
- **Ende zu Ende:** jeder Lauf von `scripts/vermessung-e2e.sh` zählt nach dem Lauf über Queries und den Log nach, dass jede Wirkung genau
  einmal eingetreten ist (je Szenario, dazu global: keine CommandId zweimal gewirkt, keine Version doppelt; mit offenem Rückstand „höchstens
  einmal“); der **Störungslauf** (`--stoerung`) tut das nach harten Host-Abbrüchen und einem Redis-Ausfall ([12](12-tests-und-vermessung.md)
  §12.5). Ein Fehlschlag macht den Lauf ungültig.
