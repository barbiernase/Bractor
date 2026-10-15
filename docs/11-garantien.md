# 11 — Garantien

Stand 2026-10-15, gegen den Code geprüft. Maßstab: docs/konzept-werkbank.md.

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
`scripts/vermessung-e2e.sh`. *Skip G-n* = der Test zeigt die offene Lücke G-n und ist bis zu ihrer Schließung übersprungen; eine Garantie ohne
Nachweis ist eine Lücke in [13](13-reifegrad-schulden-bewertung.md).

## 11.2 Schreibseite (Command → Events)

| Garantie | Mechanismus | Bedingung / Lücke | Nachweis |
|---|---|---|---|
| **Atomar:** alle Events eines Commands und seine Inbox-Marke werden gemeinsam gültig oder keines | ein Append, eine Transaktion (`AggregateActorBase`, `BatchingEventAppender`) | – | I `GarantiePostgresTests.Ein_Append_wird_ganz_oder_gar_nicht_gueltig`; P `AppendBatchingTests` (Batch-Fehler isoliert den Schuldigen) |
| **Durabel nach Erfolg:** nach dem Append wird nie ein Fehler gemeldet ([02](02-design-prinzipien.md) P10) | ein Fehler nach dem Append rehydriert den Actor und quittiert Erfolg | so durabel wie Postgres (fsync, Platte, Replikation). Ein mehrdeutiger Commit (committet, Quittung verloren) wird heute als Fehler gemeldet (G-3) | PD `SchreibGarantieTests.Ein_mehrdeutiger_Commit_wird_als_Erfolg_quittiert…` (Skip G-3); der Pfad „Fehler nach dem Append im Actor“ ohne eigenen Test (G-6) |
| **Serialisierbar je Aggregat:** jede Entscheidung sieht den aktuellen Stand | Single-Writer-Actor je Stream (virtuelle Cluster-Identität) plus Versionsprüfung beim Append | nach einem Append-Konflikt bleibt der Actor mit veraltetem Stand stehen (G-3): die Sicherheit bleibt, die Verfügbarkeit des Aggregats nicht | I `GarantiePostgresTests.Zwei_Schreiber_mit_derselben_Version…`; PD `ProzessE2ETests.Nebenlaeufige_Vorgaenge…`; Verfügbarkeit: PD `SchreibGarantieTests.Nach_einem_Versionskonflikt…` (Skip G-3) |
| **Client-OCC:** ein Client-Command mit behaupteter Version (≥ 0) scheitert, wenn das Aggregat sich seitdem geändert hat | Vorprüfung (`CommandVorpruefung`), Versionsprüfung beim Append | – | P `CommandVorpruefungTests` |
| **Emittierte Commands** (Reaktion, Prozess, Fluss, Zusage mit Kausalität, Client mit Version < 0) entscheiden auf dem **aktuellen** Stand | `CommandModus.Emittiert`: keine erwartete Version | gewollt: der Sender behauptet keinen Stand | P `CommandVorpruefungTests`; PD `ProzessE2ETests.Nebenlaeufige_Vorgaenge…` |
| **Ablehnungen** stehen nicht im Log; durabel ist nur die Marke `KommandoAbgelehnt` | `ITransientEvent` | – | P `TransientPersistiertSplitTests`, `AblehnungsMarkeTests`; PD `ProzessE2ETests.Scheitert_ein_Zweig…` |
| Invarianten **über mehrere Aggregate** | – | **keine Garantie**; nur eventual über Prozesse und Kompensation | – |

## 11.3 Zustellung und Idempotenz von Commands

| Weg | Zustellung | Wirkung | Bedingung / Lücke | Nachweis |
|---|---|---|---|---|
| **Client** (gRPC, `CqrsClientService`) | fire-and-forget über `ProtoActorAggregateDispatcher`: bis zu 3 Versuche je 10 s, dann Dead-Letter und `CommandFailed` an die Sitzung | **genau einmal, wenn der Client mit derselben CommandId wiederholt**; die Inbox prüft vor der Version | Ein Command, der den Host vor dem Dispatch verliert (Absturz), ist weg; der Client merkt es nur am fehlenden Ack und muss selbst wiederholen. Ohne Wiederholung gilt höchstens einmal. | P `CommandVorpruefungTests.Bereits_verarbeitet_an_stale_Version…`; I `EingangE2ETests.Client_wiederholt_mit_derselben_CommandId…` (über den JSON-Eingang) |
| **Client mit Antwort** (JSON-Eingang, `FrageAsync`; optional `commandId` im Body) | ein Versuch, gebunden | wie oben | „keine Antwort“ heißt nicht „nicht gewirkt“ | I `EingangE2ETests.Client_wiederholt_mit_derselben_CommandId…`; E2E Störungslauf (Wiederholung bei Host-Abbruch) |
| **Ablehnung** | – | **idempotent:** eine Wiederholung liefert dieselbe Ablehnung | durable Marke `KommandoAbgelehnt` | P `CommandVorpruefungTests.Bereits_abgelehnt…`, `RehydrationGarantieTests.Neustart_stellt_die_Inbox…` |
| **Emittiert** (`CommandEmitter`) | at-least-once, gebunden (5 s), ohne eigenen Retry; den Timeout heilt der nächste Re-Wake bzw. Poll | **genau einmal** über die deterministische CommandId und die Inbox des Ziels | nur innerhalb der letzten `Schreibpfad.InboxGrenze` (Standard 10 000) Commands je Ziel-Aggregat; zusammen mit dem Emittenten-Poll ab Version 0 kann ein älterer Command erneut wirken (G-2) | PD `SchreibGarantieTests.Doppelt_und_gleichzeitig_zugestellter_Command…`; I `VertragsZusageE2ETests`; P `KonsumGarantieTests.Absturz_zwischen_Emit_und_Cursor…`, `FristGarantieTests.Doppeltes_Feuern…`; E2E „keine CommandId zweimal gewirkt“; Fenster: PD `SchreibGarantieTests.Eine_alte_CommandId_jenseits_der_Inbox_Grenze…` (Skip G-2) |

## 11.4 Ordnung

| Garantie | Gilt | Mechanismus / Folge | Nachweis |
|---|---|---|---|
| Events eines Streams werden in jedem Konsumenten in Versionsreihenfolge angewendet | **ja** | ein Actor je (Konsument, Stream), Vorab-Prüfung der Version im `ProjectionAdapter` | P `KonsumGarantieTests.Doppelte_und_ungeordnete_Weckungen…` |
| globale Ordnung über Streams | **nein** | die globale Sequenz von Marten ist nur Lese-Cursor des Polls, kein Versprechen | – |
| kausale Ordnung über Streams (Ursache vor Wirkung) | **nein** | eine Projektion über X und Y kann die Wirkung in Y vor der Ursache in X sehen | – |
| Prozesse und Flüsse | ordnungsunabhängig | der Dirigent faltet eine Markierung (Petri-Netz); welche Bedingung zuerst eintrifft, ändert das Ergebnis nicht | PD `ProzessE2ETests.Diamant…`, `MarkingCursorAequivalenzTests` |

## 11.5 Projektionen (replaybar)

| Garantie | Mechanismus | Bedingung / Lücke | Nachweis |
|---|---|---|---|
| **Wirkung genau einmal je Event** | Co-Commit: Effekt und `ProjectionCheckpoint` in einer Transaktion (`MartenCoCommitStoreBase`); der GA-1-Check erzwingt den Co-Commit für append-artige Projektionen | genau eine Aktivierung je (Projektion, Stream). Der Checkpoint wird ohne Versionsprüfung geschrieben; zwei Aktivierungen (Netzstörung im Cluster) können einen Effekt doppelt anwenden (G-4) | I `CoCommitPostgresTests.Absturz_zwischen_Effekt_und_Marke…`; P `GaEinsPruefungTests`; E2E „Read-Model = Log“ (S1, S4); zwei Aktivierungen: I `GarantiePostgresTests.Zwei_Aktivierungen…` (Skip G-4) |
| **geteilte Dokumente** ohne verlorene Updates | Versionsprüfung, bis zu 5 Wiederholungen, sonst `SchreibKonfliktException` und Stapel verworfen | – | I `SchreibRegelPostgresTests`, `GeteilteDokumentePostgresTests` |
| **Vollständigkeit:** jedes committete Event kommt an | Signal (schnell) plus Poll (30 s) über die Hochwassermarke; der Poll rückt nur vor, wenn jede Weckung bestätigt ist (`Poller`) | Ein Stream, dessen Dispatch dauerhaft wirft, hält die Hochwassermarke fest: nichts geht verloren, aber das Fenster wächst. Grenzfall: eine Transaktion, die länger als die Karenz (3 s) offen ist, kann der Poll überspringen; dann trägt nur das Signal. | I `PollerBackstopPostgresTests` (verlorenes Signal), `SequenceGapPostgresTests`, `GarantiePostgresTests.Ein_Poison_Stream…`; E2E Störungslauf; Lücke: nach einem gescheiterten Append steht der Poll (I `GarantiePostgresTests.Ein_gescheiterter_Append_blockiert_den_Poll_nicht`, Skip G-7) |
| **Aktualität** | Signal | eventual: typisch Millisekunden, schlimmstenfalls ein Poll-Intervall | I `LiveCommandE2ETests`; E2E Latenz ([12](12-tests-und-vermessung.md) §12.5) |
| **Read-your-writes** | Redis-Versionsindex (`ctx.Track`) | **nur best-effort**; fällt Redis aus, verlängert sich nur die Wartezeit | E2E Störungslauf (Redis 30 s weg, Exactly-once bleibt) |
| **Neuaufbau** | Reset über `IReplaybarerTracker`, dann der normale Adapter-Pfad ab 0 (`ProjectionRebuilder`) | deterministisch nur bei deterministischen Handlern; der alte Stand wird vorher geleert | P `ProjectionRebuilderTests` |

## 11.6 Reaktionen (emittierend)

| Garantie | Bedingung | Nachweis |
|---|---|---|
| der Handler läuft **mindestens einmal** je Event | – | P `EmittentenPfadAdapterTests`, `KonsumGarantieTests.Absturz_zwischen_Emit_und_Cursor…`; nicht zugestellter Emit: P `KonsumGarantieTests.Ein_nicht_zugestellter_Emit…` (Skip G-2, nötig sobald der Poll ab dem Cursor liest) |
| ausgegebene Commands wirken **genau einmal** | Inbox-Fenster des Ziels (§11.3, G-2) | PD `AblaufGarantieTests.Reaktion_wirkt_genau_einmal…`; P `ReaktionsIdTests`; E2E S5 „je Reaktions-Command genau eine Wirkung“ |
| **kein** Reset und kein Replay | zur Compile-Zeit wahr: `IEmittentenCursor` implementiert `IReplaybarerTracker` nicht | Typ (Compile-Zeit); P `EmittentenPfadAdapterTests.Tracker_und_Cursor_zugleich…` |
| ausgegebene Events (`IEvent` aus einem Handle) | verlierbar re-publiziert, nicht im Log | – |
| Seiteneffekte **außerhalb** von Commands | **nur at-least-once**, keine Deduplizierung | – |

## 11.7 Prozesse, Flüsse, Funktionen, Fristen

| Garantie | Mechanismus | Bedingung / Lücke | Nachweis |
|---|---|---|---|
| jede Transition feuert mindestens einmal und wirkt genau einmal | deterministische CommandId, Inbox des Ziels, Marke `KommandoVerarbeitet` macht auch einen Noop als „erledigt“ sichtbar | Inbox-Fenster (G-2) | PD `ProzessE2ETests`, `MarkingCursorAequivalenzTests`; E2E S3, S5 |
| kein Vorgang bleibt still liegen | Prozess-Poll plus Backstop über den durablen Offen-Index (15 s) | ein Vorgang, dessen Start-Signal verloren ging, startet nur über den Poll — der steht nach einem gescheiterten Append still (G-7) | PD `AblaufGarantieTests.Ein_Neustart_des_Hosts…`; E2E Störungslauf (harter Abbruch); Lücke G-7 (I, Skip) |
| Kompensation in umgekehrter Reihenfolge | `ProzessManager` | scheitert sie, endet der Vorgang als `KlärungNötig` mit Dead-Letter; der Dead-Letter ist nur Beobachtung, kein Retry | PD `ProzessE2ETests.Scheitert_ein_Zweig…`; E2E S5 „jede Kompensation genau einmal“; `KlärungNötig` ohne Test (P2-5) |
| Prozess-Regeln sind azyklisch | Boot-Guard (`ProzessAzyklizität`) | – | P `FunktionsAufrufTests.Der_Azyklizitaets_Guard…` |
| Zeitlimits im Dirigenten feuern | Backstop | Auflösung etwa 15 s (W11-2) | P `FunktionsAufrufTests.Ein_Zeitlimit_laesst_einen_haengenden_Aufruf_scheitern…`, `PipelineFlussTests.Bestellung_Zeitlimit_Port…` |
| eine Katalog-Funktion wird **mindestens einmal** ausgeführt | Laufort Extern: Lease 30 s beim `FunktionsVermittler`, nach Ausfall des Workers neu vergeben | **Funktionsrümpfe müssen idempotent sein**; ein Ergebnis nach Ablauf der Lease wird abgewiesen | P `FunktionsVermittlungTests.Abgelaufene_Lease…`, `…Erledigtes_verpufft…`; E2E S2 |
| **Fristen** (`IFristplan`) feuern mindestens einmal | `FristScheduler`: erst feuern, dann entfernen | **heute verletzt:** ein Timeout oder ein Fehlschlag des Emits zählt als gefeuert, die Frist wird entfernt (G-1) | P `FristSchedulerTests`, `FristGarantieTests.Zugestellte_Frist…`; I `DeadlineStorePostgresTests`; Lücke: P `FristGarantieTests.Emit_Timeout…`, `…Technischer_Fehlschlag…` (Skip G-1) |

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
- **Ablehnungen an den Aufrufer**, **Kenntnis-Push an Clients**, UI-Feedback: schneller Kanal (Inv. 6); wer es durabel braucht, fragt per Query nach.
- **Dead-Letter:** best-effort, nur zur Beobachtung.

## 11.10 Garantien zur Build- und Startzeit

| Garantie | Wo | Nachweis |
|---|---|---|
| was ein Decide oder Handle erzeugen kann, steht vollständig in der Signatur | CQRS050 | P `AusgabeVertragAnalyzerTests` |
| jedes entschiedene Event hat ein `Apply`, jedes `Apply` ein entscheidendes Decide | CQRS079/080 | P `ModellAnalyzerTests` |
| ein Command gehört genau einem Aggregat, Identitäten sind eindeutig | CQRS010/011 | P `BuildGarantieTests` |
| genau ein Emit-Weg, jeder Command-Send ist zeitlich gebunden | CQRS020/021 | P `CommandEmitAnalyzerTests` (im generierten Pull-Emit-Pfad noch `CancellationToken.None`, P1-6) |
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
| Projektion wirkt genau einmal (§11.5) | je (Projektion, Stream, Weckung) Tracker lesen, Tail lesen, eine Transaktion (`ProjectionAdapter`, `MartenCoCommitStoreBase`) | Events × Projektionen, sobald viele Streams je wenige Events haben | Batch-Co-Commit: viele Streams, ihre Effekte und Checkpoints in einer Transaktion (PF-1) |
| Ordnung je Stream (§11.4) | ein virtueller Actor je (Konsument, Stream) | Projektionen × Streams Actors; ohne Passivierung bis zum Shutdown im Speicher | partitionierte Konsumenten (`Hash(Stream) mod P`) in globaler Sequenz (PF-1); Passivierung (PF-5) |
| Vollständigkeit trotz verlierbarem Signal (§11.5) | jede Projektion scannt alle 30 s selbst alle neuen Events ohne LIMIT und weckt jeden geänderten Stream nacheinander (`Poller`, `PullPath`, `MartenEventStore.ReadChangedStreamsAsync`) | Events × Projektionen × Knoten; nach Stillstand das ganze Rückstandsfenster im Speicher | eine Abfrage „Stream-Kopf > Checkpoint“, nur nachhängende Streams wecken; ein gemeinsamer, seitenweiser Change-Feed (PF-2) |
| Emittierter Command wirkt genau einmal (§11.3, §11.6) | der Poll eines Emittenten liest ab Version 0 und gibt alles erneut aus | Stream-Länge je Poll und geändertem Stream | durabler Cursor, vorgerückt nach bestätigtem Emit (G-2) |
| Command wirkt genau einmal (§11.3) | Marke `KommandoVerarbeitet` je Command im Stream, auch ohne Event; Inbox im Snapshot | Streams etwa doppelt so lang (Append, Rehydration, Prozess-Reads); Snapshot bis `InboxGrenze` Ids | Events tragen die CommandId schon als `causationId`; Marke nur bei Noop und Ablehnung, oder Inbox als Tabelle mit eindeutigem Schlüssel in derselben Transaktion (PF-3, G-2) |
| kein Vorgang bleibt liegen (§11.7) | Prozess-Poll liest jeden geänderten Stream ab 0 und verwirft die schon geladenen Events; Backstop weckt alle 15 s alle offenen Vorgänge (`ProzessManagerWiring`) | Vorgangslänge² bzw. Zahl offener Vorgänge, auch im Leerlauf | geladene Events direkt routen; Fälligkeits-Index für Zeitlimits statt Weckung aller (PF-4) |
| Read-your-writes, best-effort (§11.5) | ein Redis-Write je Event und Projektion (`ReadModelDepsWriter`) | Events × Projektionen | je Projektion abschaltbar (PF-6) |
| Serialisierbar je Aggregat (§11.2) | der Actor wartet in jedem Turn auf seinen Commit | Commit-Latenz begrenzt den Durchsatz **eines** Streams | grundsätzlich; Abhilfe ist Modellierung (keine heißen Aggregate), Pipelining wäre ein eigener Entwurf |
| Quelle genau einmal je Kennung (§11.8) | ein Stream je Quell-Nachricht, ein Vorgang je Stream | Quell-Nachrichten: je Nachricht ein Stream, Dirigent und Actors | Designentscheidung, keine reine Optimierung: Dedup-Tabelle je Kennung und Sammel-Streams verlören „ein Vorgang je Meldung“ (PF-7) |

Kostenlos zur Laufzeit sind die Garantien aus §11.10 (Build und Start) sowie Signal, Publish und Snapshot (fire-and-forget bzw. best-effort).

## 11.13 Lücken

Stehen nur in [13](13-reifegrad-schulden-bewertung.md) §13.0: **G-1** Fristverlust, **G-2** Inbox-Fenster mit Emittenten-Poll ab 0,
**G-3** Actor nach Append-Konflikt, **G-4** Projektions-Checkpoint ohne Versionsprüfung, **G-5** Versionszählung bei Split-Upcasts,
**G-6** Garantien ohne eigenen Nachweis-Test, **G-7** Tombstones eines gescheiterten Appends legen den Poll still. Jede Lücke G-1 bis G-5 und G-7
hat einen Test, der sie zeigt (Skip `G-n offen (docs/13)`); geschlossen
ist sie erst, wenn ihr Test grün ist.

## 11.14 Wie die Garantien geprüft werden

- **Garantie-Suite:** alle Tests mit `[Trait("Kategorie", "Garantie")]` — im Prüfstand die Logik über Seams, in Integration und Prüfdomäne die
  Store-Semantik gegen echtes Marten. Sie läuft vor jedem Commit:
  `dotnet test Infrastructure.Pruefstand.Tests/… --filter Kategorie=Garantie`, danach nacheinander dieselbe Filterung auf
  `Infrastructure.Integration.Tests` und `Pruefdomaene.Integration.Tests`. Grün heißt: alles grün bis auf die übersprungenen Lücken-Tests.
- **Unter Störung** geprüft sind: doppelte und ungeordnete Zustellung (`KonsumGarantieTests`, `SchreibGarantieTests`), verlorenes Signal
  (`PollerBackstopPostgresTests`), Absturz zwischen Effekt und Checkpoint (`CoCommitPostgresTests`), Absturz zwischen Emit und Cursor
  (`KonsumGarantieTests`), Timeout und Fehlschlag beim Emit (`FristGarantieTests`, `KonsumGarantieTests`, Skip G-1/G-2), Versionskonflikt und
  mehrdeutiger Commit (`SchreibGarantieTests`, Skip G-3), alte CommandId jenseits der Inbox-Grenze (Skip G-2), zwei Aktivierungen
  (`GarantiePostgresTests`, Skip G-4), Poison-Stream und gescheiterter Append (`GarantiePostgresTests`, Skip G-7), Neustart des Hosts
  (`AblaufGarantieTests`, `RehydrationGarantieTests`).
- **Ende zu Ende:** jeder Lauf von `scripts/vermessung-e2e.sh` zählt nach dem Lauf über Queries und den Log nach, dass jede Wirkung genau
  einmal eingetreten ist (je Szenario, dazu global: keine CommandId zweimal gewirkt, keine Version doppelt; mit offenem Rückstand „höchstens
  einmal“); der **Störungslauf** tut das nach harten Host-Abbrüchen und einem Redis-Ausfall ([12](12-tests-und-vermessung.md) §12.5). Ein Fehlschlag macht den Lauf ungültig.
