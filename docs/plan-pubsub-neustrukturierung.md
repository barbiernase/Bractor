# Plan: Neustrukturierung von Pub/Sub, Bausteinen und Grammatik

> **Plan**, Stand 2026-10-26, Zweig `pubsub_neustrukturierung`. Noch nichts umgesetzt. Dieses Dokument ist für die Dauer des Umbaus der
> **Maßstab**; wo ein anderes Dokument oder der Code widerspricht, gilt der Plan als Ziel und der Code als Ist. Nach dem Umbau wird er in die
> Kapitel eingearbeitet (§9) und gelöscht.
>
> Grundlage: Code- und Messanalyse vom 2026-10-18 und 2026-10-25 (Weg eines Events, Event-Volumen je Vorgang, Pipeline-Mechanik, Handshake,
> Versions- und Abhängigkeits-Index), gegen `pg_stat_statements` des Laufs `2026-10-18_0359_229e39a_server-wachstum-basis-2` (Tag
> `vermessung-vor-schnitt`) geprüft. Zahlen „aus dem Code gerechnet“ sind nicht gemessen; die Actor-Seite ist nie profiliert worden.

## 0 · Ziel und Entscheidungen

**Ziel:** eine allgemein gültige Architektur mit einer kleinen Grammatik. Der Entwickler schreibt nur, *was* fachlich passiert — in der
Signatur einer Methode. *Wie* es läuft, wählt der Generator aus dieser Signatur. Die Beispieldomäne (ImagePair, Datensatz, Training) und die
Prüfdomäne sind Prüflinge, nicht der Maßstab: jede Regel muss für beliebige Anwendungen gelten (Zeitreihen, Blob-Ablagen, Massen-Import,
Genehmigungen, Integrationen …), und jeder Store-Typ muss hinter derselben Naht möglich sein.

**Entscheidungen (Tobi, 2026-10-26):**

| # | Frage | Entscheidung |
|---|---|---|
| E1 | Geltungsbereich (Mandant, Daten-Rechte je Objekt) | **nicht jetzt.** Zielbetrieb ist eine interne Produktionsfirma: jeder darf alles. Akteur-Rechte nach Typ (`IDarf<T>`, Vertrag ∩ Token) bleiben, wie sie sind. Der Platz für einen Geltungsbereich wird nicht verbaut (§2.8). |
| E2 | Direkte Abfrage eines Aggregats (exakter Stand) | **ja**, als eigener Lesepfad neben dem Reader (§2.6). |
| E3 | Typisierte Ids (`Id<TAggregat>` statt `Guid`) | **ja**, Pflicht für Command-Ziele; in Antworten die Grundlage für feine Updates an Clients (§2.5). |
| E4 | Editor-Oberfläche (Fluss-Karten, Darstellung) | **zurückgestellt.** Werkbank und GraphExtractor werden nur so weit mitgezogen, dass alles baut und `--check`/`--sonde` für die neuen Bausteine grün sind (§6, Phase 4). `Werkbank/wwwroot/editor/` wird nicht angefasst. |
| E5 | Implementieren | **nicht in diesem Schritt.** Dieser Commit enthält nur den Plan und das Aufräumen der Doku. |

**Annahmen (sicherer Default, gelten, solange Tobi nicht widerspricht):**

- **A1** Funktionen halten ihre Ausgabe **standardmäßig** im Log fest, bevor sie wirkt; nur nachweislich gleichbleibende Funktionen sparen das (§4.2).
- **A2** Die Zugehörigkeit zu einem Vorgang (etwa „diese Reservierung gehört zu Auftrag 4711“) ist ein **fachliches Feld** im Event, keine
  versteckte Korrelation in den Metadaten. Alternative wäre `Herkunft.Korrelation`; siehe §8.
- **A3** Wiederholungsfeste Stores (Klasse 2, §2.4) schreiben **mit der Event-Identität im Schlüssel oder bedingt nach Position** — reines Überschreiben reicht nicht.
- **A4** Der Neuaufbau einer Projektion geht über **Generationen** (neues Ziel aufbauen, dann umschalten); „Leeren“ ist der Sonderfall.

**Arbeitsweise:** wie in `CLAUDE.md` und im Gedächtnis: lokal nur bauen, alle Tests über `scripts/server.sh lauf …` auf dem Messserver, Messen kurz
(Minuten, eine Wiederholung), Bestand direkt umbauen ohne Übergangs- oder Kompatibilitätsschichten, Ursachen statt Flickwerk, Commits ohne
`Co-Authored-By`, nicht pushen ohne Tobis Wort. Jede Phase endet mit grüner Garantie-Suite (§5), `./codegen.sh --check`,
`GraphExtractor -- --check` und `./pruefe-checkout.sh`.

## 1 · Ausgangslage: was heute falsch läuft

### 1.1 Zu viele Events für zu wenig Fachlichkeit

Ein **Bildpaar** (zwei Dateien) erzeugt heute **18 Events in 11 neuen Streams**; nur **4** davon sind fachlich (`ImagePairErstellt`,
`BildVerfuegbar` ×2, `ImagePairKomplett`). Der Rest: 2 Quell-Nachrichten, 6 Funktions-Ergebnisse (je Aufruf ein eigener Stream), 4 Prozess-Events
(`ProzessGestartet`/`ProzessBeendet` je Datei), dazu `KommandoAbgelehnt` + `SchrittUmgeleitet`, weil der Fluss über eine absichtlich erzeugte
Ablehnung verzweigt. Gemessen (Messserver, Spalte „Wachstum Basis“, docs/12 §12.5): **22,6 Transaktionen und 171 Queries je Paar; 62 Paare/s Ende zu
Ende bei 610 % CPU**, während die Schreibseite 9 360/s annimmt. Beim heißen Sammelvorgang sind 40 % der Events je Teil Prozess-Mechanik für eine
einzige Kante Event → Command (`TeilFertigProzess`).

### 1.2 Eine Benachrichtigungs-Schicht, die nichts transportiert, was der Leser nicht selbst holt

- Nach jedem Commit baut das Aggregat **je Command und je Event** einen Envelope und ein Signal und schickt beide an je **3 Broker-Shards als
  Anfrage mit Quittung** — 6 Round-Trips je Event, auch ohne Abonnenten (`AggregateActorBase.PublishEventsAsync`, `BrokerPublisher`).
- **Nur das Aggregat veröffentlicht.** Events aus Quelle, Funktion und Prozess-Manager wecken keine Projektion und keine Reaktion — die sehen sie
  erst mit dem Poll (bis 30 s).
- Batch-Leser und Adapter werten nur den Stream aus, nie die Version; der Korrelations-Router liest das Event nach, obwohl die Korrelation im
  Signal steht (`KorrelationsRouter.cs`). Jeder Prozess-Schritt weckt den Manager doppelt (Router und Selbst-Weckung).
- **Im Cluster bekommt nur ein Knoten Signale**: gleiche Abonnenten-Id auf allen Knoten, die Registry überschreibt (`SubscriberRegistry`). Abos
  liegen im Speicher der Shards und gehen beim Umzug still verloren; die Client-Sitzungen melden sie deshalb in Schleife neu an
  (`SubscriptionTracker.ReassertAllAsync`).

### 1.3 Der Dirigent ist für Massenverarbeitung ungeeignet

Ein Fluss ist ein Petri-Netz-Vorgang je Quell-Nachricht (`ProzessManager`). Jede Weckung bietet **alle** offenen Funktions-Aufträge
**nacheinander** neu an (`ProzessManager.WakeAsync`; der Kommentar sagt „parallel“) und feuert **nur den ersten** offenen Command; der Fixpunkt
geht je Weckung über alle Tokens. Bei `Je`/`SendeJe` über N Elemente wird das O(N²) in Nachrichten und Rechenzeit. Rückstau gibt es nur an den
Funktions-Slots; in S1 waren 594 von 600 Vorgängen gleichzeitig offen, und der Heilplan weckt nach 15 s Stille — gerade dann, wenn Aufträge in der
Schlange stehen. Batch-Funktionen gibt es nicht.

### 1.4 Die Datenbank wird gescannt statt gefragt

8 × (Projektionen) Partitions-Leser, der Emittenten-Poll und der Prozess-Poll lesen dieselbe `seq_id`-Spanne je für sich; die Partition ist ein
Filter ohne Index (jede Partition liest ≈ P-mal so viele Zeilen, wie sie liefert). Der Prozess-Poll liest mit voller Payload und ohne Typfilter
(`ProzessPoll.DurchlaufAsync`). Jeder Leseaufruf fragt den Horizont eigens. Redis `agg:{id}` spiegelt nur `mt_streams.version`; auf `correlation_id`
gibt es keinen Index.

### 1.5 Zu viele Konstrukte für dieselbe Absicht

Auf ein Event reagieren: Reaktion, Prozess, Fluss, Handle-Pipeline (ohne Nutzer). Zeit: `Zeitlimit`, `Warte`, `Frist<TCmd>`/`FristStorno`/
`IFristplan`, `Selbst<T>`, Timer-Trigger, Frist im Offen-Index. Anstoß von außen: Quelle, `[Ingress]` (ohne Nutzer), Webhook-Trigger, Pipeline-Trigger,
Client-Command. Ablehnung: Ablehnungs-Event, Marke `KommandoAbgelehnt`, `CommandFailed`, `SchrittGescheitert`, ✕-Port. Dazu Mechanik im Fachcode:
`IPullSubscriber` (eine „Migrations-Krücke“, der Push-Pfad lebt noch), handgetippte `SubscriberId`, `ProjectionWriter.Execute`, `ctx.Track`,
`[ProjectionReader(TrackDeps = true)]`, Envelopes in Signaturen.

### 1.6 Clients

Wirksame Abos (Vertrag ∩ Token) werden am Handshake richtig ermittelt (`CqrsClientService.HandleCapabilitiesAsync`, `AkteurVertragsPruefung`),
dann aber an den Broker gegeben. Zwei Schreiber auf dem gRPC-Antwort-Stream (Lese-Schleife und `EventProxyActor`) verlieren still Nachrichten.
Ein Client kennt zwei Versionen je Aggregat, weil Noop- und Ablehnungs-Marken die Stream-Version erhöhen (`VersioningModule`); kennt er ein Aggregat
nur aus einer Query, schickt er Commands ohne Versionsprüfung. Read-your-writes ist best-effort über Redis mit bis zu vier Nachfragen
(`QueryBridge`). Für **transitive** Änderungen (eine Datensatz-Ansicht zeigt Labels von Bildpaaren) gibt es im Framework nichts: der Client muss den
Abhängigkeits-Graphen selbst kennen.

### 1.7 Befunde der Actor-Praxis, die im Zielbild weiter gelten

Aus der Analyse vom 2026-10-18 (nicht im Repo; die Teile, die nicht ohnehin mit dem Umbau entfallen):

- Der gRPC-Antwort-Stream einer Sitzung hat zwei Schreiber → wird mit der Sitzung als Actor gelöst (Phase 7).
- Cluster-Anfragen ohne Frist in Actors (Publish von Ablehnung/`CommandFailed`, Abmelden in `Stopping`, `BrokerManagerActor`) → zwei Verben
  `Melde` (verlierbar, ohne Antwort) und `Frage` (Antwort, Pflicht-Frist) (Phase 6).
- Fähigkeits-Bereiche (DI-Scope) werden in Stream-Adapter und Pipeline-Actor nie freigegeben; `BatchLeserActor` gibt seinen Bereich frei, während
  der Durchlauf ihn noch nutzt → ein Lebenszyklus im Framework für alle Actors (Bereich öffnen, beim Stopp abbrechen und begrenzt abwarten,
  freigeben; Fehler fangen und ehrlich antworten; Weckungen zusammenfassen „läuft → noch einmal“) (Phase 2).
- Aggregat: `_id` kommt aus dem ersten Envelope statt aus der Cluster-Identität; zwei fast gleiche Entscheidungswege (Bündel und Einzel); die
  Bündel-Fortsetzung wartet auf Redis (`TrackAsync` schreibt nicht monoton) (Phasen 1, 6, 8).
- Hausregel „ein Weg je Absicht im Actor“ ohne Build-Prüfung: kein `Task.Run`, `lock`, `_ = …Async`, `ContinueWith`, `.Result`, keine rohe
  Cluster-API in Actors; Parallelität nur durch mehr Actors; lange I/O per `ReenterAfter` (Phase 10).
- `CommandResult` trägt alle Events an jeden Absender zurück, gelesen nur im JSON-Eingang (Phase 6).

### 1.8 Offene Garantie-Lücke, die der Umbau erbt

**G-11** (docs/13): im Störungslauf übersprang eine Projektion einmal zwei Events eines Streams. Der Batch-Leser bleibt im Zielbild — die Lücke
muss vor Phase 8 geklärt oder mit einem roten Test eingegrenzt sein. Die Untersuchung läuft in einem anderen Worktree (`CqrsSolution-g11`).

## 2 · Zielbild

### 2.1 In wenigen Sätzen

Das **Log in Postgres ist die einzige Wahrheit.** Geschrieben wird von **Aggregaten** (was sie entschieden haben), **Quellen** (was von draußen
kam) und **Funktionen** (ihre festgehaltene Ausgabe, §4.2) — jedes Mal genau einmal. Alles andere **liest das Log ab seiner eigenen Position**:
**Projektionen** falten Events in ihren Store und geben nichts aus; **Funktionen** nehmen einen Eingang und geben aus, was sie wollen. **Clients**
hängen am Rand: sie schicken Commands, Quell-Nachrichten und Queries hinein und bekommen über einen **Verteiler je Knoten** mit, was sie
abonniert haben. Die **Signaturen sind der Graph**: `Auf(BildErfasst e)` in einer Funktion heißt „diese Funktion hört dieses Event“, der
Rückgabetyp sagt, was herauskommt. Daraus entstehen Generate, Editor-Graph und Laufzeit.

### 2.2 Bausteine und Laufzeit

| Baustein | tut | Laufzeit |
|---|---|---|
| **Aggregat** | entscheidet: Command rein, Events/Hinweise/Fristen raus; einziger Schreiber; darf direkt gefragt werden (§2.6) | virtueller Actor je typisierter Id (wie heute), Bündel, Passivierung |
| **Projektion** | hört Events, schreibt ihren Store über Fähigkeiten, gibt nichts aus | Leser je (Projektion, Partition) — der heutige Batch-Leser |
| **Reader** | beantwortet Queries über Lese-Fähigkeiten | Aufruf im Host oder draußen (Laufort) |
| **Funktion** | frei programmierbar: ein Eingang (Event oder Hinweis), beliebige Ausgaben | **ein Actor je (Funktion, Partition)**: Position, Fortschritt, Gleichzeitigkeit (Slots), Wiederholung mit Backoff, Festhalten der Ausgabe; Laufort Host / Worker / Client / Hand aus der Umgebung |
| **Quelle** | fester Treiber + Konfig bringt Nachrichten genau einmal ins Log | ein Treiber je Quelle im Cluster (Lease, wie heute) |
| Store, Dienst | Technik hinter Projektion/Reader bzw. Funktion | – |
| Akteur, Client | wer welche Typen darf; welche Software für welche Akteure spricht | Sitzung als Actor je Verbindung, Verteiler je Knoten |
| *eingebaut:* Uhr | macht aus fälligen Fristen Commands | eine Funktion des Frameworks |

Sechs Actor-Arten statt heute gut dreizehn samt Broker: Aggregat, Projektions-Leser, Funktion, Sitzung, Verteiler, Treiber.

**Funktion im Detail.** Eingänge desselben Streams laufen nacheinander, verschiedene Streams parallel bis zur Slot-Grenze. Der Actor holt neue
Eingänge erst, wenn Slots frei sind (Rückstau liegt im Log, nicht im Speicher). Kurze Funktionen bekommen Seiten (Batch-Aufruf, der Generator kann
die Batch-Form aus der Einzelform bauen); lange laufen einzeln mit Lebenszeichen und Frist. Eine Funktion, die aus einem Eingang viele Ausgaben macht
(Import, Training-Strom), hat einen **Fortschritt**: nach einem Absturz macht sie bei der nächsten Ausgabe weiter. Die Position bleibt beim ältesten
Unfertigen; was dahinter fertig ist, merkt sich der Actor (gedeckelt wie heute im Batch-Leser). Läuft der Code draußen (Python-Worker, Client, Mensch),
reicht der Actor den Eingang an die Sitzung weiter (Lease, Übernahme bei Abbruch, wie heute `FunktionsVermittlung`); **die Garantie bleibt beim
Actor**, der Worker ist nur die Hand.

**Warten** (Join, „alle N“, Zeitlimit, Kompensation) ist Zustand über mehrere Ereignisse und gehört in ein **Aggregat**: ein Vorgang ist ein
Aggregat mit der Vorgangs-Id; Fan-in zählt ein Zähl-Aggregat (wie `Sammelvorgang`); Zeit ist eine Frist. Eine eigene Prozess-Maschine
(Marking, Offen-Index, Heilplan, Backstop, Router) gibt es nicht mehr.

### 2.3 Wecken

Es gibt **eine** Stelle, an der nach einem Commit geweckt wird: am **Flush des Group-Commits** (`BatchingEventAppender` bzw.
`MartenEventBatchWriter`, samt dem Rückfall `IsolateAsync`) — nicht als Dekorator um `IEventStoreRepository`, sonst weckte er je Aggregat-Bündel.
Alle Schreiber (Aggregat, Quelle, Funktion, Uhr) gehen durch ihn. Ein **generierter Weckplan** sagt je Event-Typ, wen ein Commit weckt:
Partitionen der Projektionen, die den Typ hören; Partitionen der Funktionen, die ihn hören; die Uhr (Fristen); die Verteiler (Typen, die irgendein
Client hören darf). Geweckt wird gezielt, je Commit und Ziel einmal, per `Melde` (verlierbar, ohne Inhalt). Jedes Ziel fasst Weckungen selbst
zusammen („läuft → noch einmal“). Das Netz bleibt der Takt (Poll je Partition, 30 s). Neue Fassung von **Invariante 2**: „Der Weckruf trägt nur
sein Ziel; er darf verloren, doppelt, ungeordnet sein.“

### 2.4 Stores: drei Klassen hinter allgemeinen Schnittstellen

Das Framework schreibt keine Datenform vor (kein Dokument-, kein Tabellen-Modell). Eine Projektion bekommt **Fähigkeiten in Fachsprache** als
Parameter (heute schon: `: IWriteStore`/`: IReadStore`, ein Store bündelt sie mit `: IStore`); was dahinter steht — relationale Datenbank,
Zeitreihe, Blob-Store, Suchindex, Graph-Datenbank, SQLite beim Client —, weiß nur die Implementierung. Das Framework braucht von einem Store nur
eine von drei Zusagen:

| Klasse | Zusage | genau einmal, weil | Beispiele |
|---|---|---|---|
| **1 atomar** | Wirkung und Position des Lesers in **einer** Transaktion | ein Absturz lässt beides oder keins zurück; die Position wird mit erwarteter Version geschrieben (zwei Aktivierungen: die verlierende scheitert samt Wirkung) | relationale DB, Dokument-DB mit Transaktionen, Graph-DB mit Transaktionen, SQLite/IndexedDB beim Client |
| **2 wiederholungsfest** | jede Wirkung ergibt wiederholt dasselbe; die Position hält das **Framework** und rückt nach bestätigten Wirkungen vor | Wirkungen haben die **Event-Identität im Schlüssel** (Messpunkt mit Zeitpunkt, Datei unter Pfad aus Stream+Version) **oder** schreiben **bedingt** („nur wenn Position größer als gespeichert“) — sonst brächte eine zweite Aktivierung alte Werte zurück, und Lesen-Ändern-Schreiben zählte doppelt | Zeitreihe, Objektspeicher, Suchindex mit Version, Key-Value mit Bedingung |
| **3 mindestens einmal** | keine | – | Metrik-Sinks; nur mit ausdrücklicher Erlaubnis an der Projektion |

Allgemeine Schnittstellen (Namen sind Vorschläge; ersetzen `IProjectionTracker`/`ICoCommitTracker`/`IAppendProjektion`):

```csharp
interface IProjektionsZiel                 // jeder Store einer Projektion
{
    void Beginne();                                                    // Batch beginnt, Puffer leer
    Task SchreibeAsync(CancellationToken ct);                          // gepufferte Wirkungen schreiben (Klasse 2/3)
    Task NeueGenerationAsync(string generation, CancellationToken ct); // Neuaufbau (A4)
}
interface IAtomaresZiel : IProjektionsZiel                             // Klasse 1
{
    Task<Fortschritt> LadeFortschrittAsync(string projektion, int partition, CancellationToken ct);
    Task SchreibeMitFortschrittAsync(Fortschritt f, CancellationToken ct);   // Wirkungen + Fortschritt, eine Transaktion
}
interface IWiederholungsfestesZiel : IProjektionsZiel { }              // Klasse 2: Fortschritt beim Framework
interface INurMindestensEinmal : IProjektionsZiel { }                  // Klasse 3
```

`Fortschritt` = Position der Partition, Version je Stream (heute „Guard“), Geparkt — das Framework füllt ihn, der Store legt ihn in Klasse 1 nur
ab. Eine Projektion schreibt in **genau einen** Store (wie heute); zwei Ziele sind zwei Projektionen. Ob ein Treiber seine Klasse einhält, ist eine
Laufzeit-Eigenschaft: jeder Treiber besteht einmal den **allgemeinen Store-Test** (Batch-Abbruch, Wiederholung, zweite Aktivierung mit älterem
Stand, Neuaufbau), den das Framework mitliefert. Fertige Stores (der heutige Marten-Store als Klasse 1) sind Bequemlichkeit, nicht das Modell;
zum Nachweis der Allgemeinheit kommen ein Klasse-2-Treiber (Dateisystem/Objektspeicher) und ein Klasse-3-Sink hinzu (Phase 8).

Die Projektion selbst rechnet bei gleicher Eingabe gleich (keine Uhr, kein Zufall, kein Lesen fremder Daten; Zeit aus der `Herkunft`), sonst
stünden nach einem halb geschriebenen Batch Werte aus zwei Läufen nebeneinander.

### 2.5 Typisierte Ids (E3)

`Id<TAggregat>` ersetzt `Guid` als Ziel jedes Commands (heute `ICommand.AggregateId : Guid`) und überall, wo ein Aggregat referenziert wird
(Events, Antworten, Read-Models). Wirkung:
- **Routing über Typen** (Inv. 3) ohne Konvention: das Ziel steht im Typ.
- **Antworten sagen, was sie zeigen.** Der Generator findet `Id<…>`-Felder in den Antwort-Typen der Reader; das Framework hängt beim Antworten
  den Stand jeder gezeigten Id an (§3.3, §2.7) — ohne `ctx.Track`.
- Editor und LLM sehen, worauf ein Feld zeigt.
- Wire/Proto/Python/Blazor: `Id<T>` wird als UUID übertragen; die Generatoren (`DtoMapperGenerator`, `FileGenerator`, Python-Basen,
  `Client.SourceGeneration`) bilden es ab. Neue Ids in Funktionen und Aggregaten nur **abgeleitet** (`Herkunft.NeueId("Mahnung")`), nie
  `Guid.NewGuid()`/`Id<T>.Neu()` im Fachcode der Server-Seite (§4.2). Clients dürfen neue Ids würfeln (sie halten die Command-Id ohnehin fest, §4.7).

### 2.6 Direkte Abfrage eines Aggregats (E2)

Eine Query darf statt an einen Reader an **ein Aggregat** gehen: Signatur `Frage(HoleKontostand q) → OneOf<…>` im Aggregat, nur lesend auf seinen
Zustand. Der Aggregat-Actor antwortet im Turn, also mit genau der Version, auf der der nächste Command entschieden würde (exakter Lesepfad,
stark konsistent). Die Antwort trägt die Version als OCC-Stand. Regel: eine Query hat genau ein Ziel — einen Reader **oder** ein Aggregat.

### 2.7 Clients

- **Handshake bleibt** in der Ermittlung (Vertrag ∩ Token, Server-Tabelle statt Selbstauskunft). Der alte zweite Weg „für den Blazor-Client“
  (`event_types` ohne `message_types`, `CapabilitiesHandler`) entfällt.
- **Sitzung = Actor je Verbindung**, einziger Schreiber auf den gRPC-Stream; die Lese-Schleife reicht nur weiter; Query-Weiterleitungen per
  `ReenterAfter`; begrenzte Mailbox, bei Überlast verwerfen + Spur-Hinweis.
- **Verteiler je Knoten** (lokaler Actor mit festem Namen): Tabelle Typ → Sitzungen dieses Knotens (optional Typ + Id), `Watch` auf die Sitzungen.
  Die Sitzung meldet nach dem Handshake ihre wirksame Liste an. Der Wecker schickt **eine Nachricht je Commit an den Verteiler jedes Knotens**
  (Mitgliederliste → `PID(Adresse, "verteiler")`), nur mit Typen, die irgendein Client hören darf (generierte Tabelle), in Log-Reihenfolge, den
  Envelope einmal gebaut (heute `EventEnvelopeFactory` im Aggregat). **Gezielt** (Ablehnung, `CommandFailed`): die Sitzungs-Id nennt den Knoten,
  die Nachricht geht an dessen Verteiler. Kein Dauer-Abo auf `CommandFailed` mehr. Verlierbar (Inv. 6). Proto.Clusters eigenes Pub/Sub wird bewusst
  nicht genutzt (Abos in einem Topic-Actor irgendwo — dieselbe Bauart, an der der Broker scheiterte).
- **Versionen:** ohne Marken im Log (§3.1) gibt es **eine** Version je Aggregat. Der Client bekommt sie aus Event-Pushes **und** aus Antworten
  (Stand je gezeigter Id, aus dem Fortschritt der Projektion bzw. direkt vom Aggregat, §2.6).
- **Read-your-writes:** die Quittung eines Commands nennt Stream und Version; die nächste Query trägt „mindestens S auf v“; der Reader wartet
  begrenzt auf den Fortschritt seiner Projektion für S (Store-unabhängig). Reicht die Frist nicht, sagt die Antwort „noch nicht so weit“.
  Ersetzt Redis-Abhängigkeiten, `expected_fresh_ids` und die Nachfrage-Schleife.
- **Updates, auch transitive:** beim Beantworten merkt sich die Sitzung (beim Verteiler) „Projektion P, Streams der gezeigten Ids“; Antworten
  ohne Ids → „Projektion P“ grob. Nach jedem Commit einer Projektion meldet der Rahmen an die Verteiler „P hat S weiterverarbeitet“; der Verteiler
  fasst je Sitzung zusammen (z. B. höchstens alle 200 ms). Der Client fragt neu. Die Projektion gibt dabei nichts aus — das ist Mechanik nach
  ihrem Commit. Die transitive Abhängigkeit (Datensatz zeigt Labels von Bildpaaren) hat die Projektion beim Falten hergestellt; der Client muss
  sie nicht kennen.
- **Verlässliches Lesen beim Client:** die Leitung bietet „lies das Log ab Position“, gefiltert auf das, was der Client hören darf. Damit führt ein
  Client eine **Projektion bei sich** (Laufort Client, Store-Klasse 1 oder 2) — mobile App, anderer Dienst, lokales Read-Model. Der Push bleibt die
  Abkürzung.
- **Client-Verträge:** `IAkteurVertrag<A>` mit `Auf` entfällt. Ein Client-Vertrag sagt `ISendet<T>` (Commands, Quell-Nachrichten, Hinweise),
  `IFragt<Q>`, `IHoert<T>` (Kenntnis, verlierbar) und `ITraegt<IFunktion>` (Funktionen, deren Code er ausführt — das sind die heutigen Zusagen).
  Python-Basen und Blazor-Wiring folgen daraus.

### 2.8 Was bewusst nicht kommt

Kein Geltungsbereich und keine Daten-Rechte je Objekt (E1); der Platz bleibt frei: die `Herkunft` trägt den Akteur, `Decide` darf ihn sehen; ein
Bereich wäre später eine weitere Kopfzeile plus Filter in Verteiler, Reader und Lesen ab Position. Kein Löschen/Aufbewahren-Konzept in diesem Umbau
(verschlüsselbare Felder bleiben als Option offen, §8). Keine Editor-Arbeit (E4). Keine Schema-Evolution im Editor; `IUpcast` bleibt Backend-Primitive.

## 3 · Die Grammatik

Ersetzt den Inhalt von `DomainEditor/Grammatik.cs` (Phase 4). Was neu ist, ist markiert.

### 3.1 Nachrichten

| Nachricht | Bedeutung | Garantie | Code |
|---|---|---|---|
| **Command** | Absicht an genau ein Aggregat | wirkt genau einmal (Command-Id, Inbox) | `record … : ICommand<TAggregat>` mit `Id<TAggregat>` (neu) |
| **Event** | Fakt im Log | durabel; wirkt bei jedem Hörer genau einmal | `record … : IEvent` |
| **Quell-Nachricht** | Event von draußen (Treiber oder Client) | genau einmal je Kennung | `record … : IQuellNachricht` |
| **Hinweis** | flüchtig: Ablehnung, Fortschritt, Messwert, Live-Anzeige | höchstens einmal | `record … : IHinweis` (heute `ITransientEvent`) |
| **Query** | Frage, wirkt nicht | synchron | `record … : IQuery` |
| **Antwort** | Rückgabe eines Readers oder Aggregats | trägt den Stand der gezeigten Ids | kein Marker, steht in der Signatur |

`Frist<TCommand>` ist ein Event des Frameworks; die Uhr macht daraus zur Zeit den Command. **Marken im Log entfallen**: `KommandoVerarbeitet`
und `KommandoAbgelehnt` gibt es nicht mehr; die Inbox-Tabelle hält „gewirkt/abgelehnt“ je Command-Id (Wiederholung liefert dieselbe Antwort, wie
heute). Ist eine Ablehnung für einen Ablauf wichtig, ist sie ein **Fakt** (Event) — das entscheidet der Modellierer über den Typ; sonst ein Hinweis
an die Sitzung. Abhängigkeiten (keine Nachrichten, aber Kanten): **Fähigkeit** (Store-Funktion als Parameter), **Dienst** (im Konstruktor einer
Funktion), **Herkunft** (Parameter: Stream, Version, Zeit, Akteur, `NeueId`).

### 3.2 Bausteine

Fachlich: Aggregat, Projektion, Reader, Funktion, Quelle. Technik: Store, Dienst. Rand: Akteur, Client. Eingebaut: Uhr, Verteiler.
(Heute 14 Bausteine, danach 9.)

### 3.3 Wortformen

```csharp
// Aggregat
public partial class Konto : IAggregat
{
    public IEnumerable<OneOf<Abgebucht, DeckungFehlt /*Hinweis*/, Frist<PruefeKonto>>> Decide(Buche c, Herkunft h) { … }
    public void Apply(Abgebucht e) { … }
    public KontostandAntwort Frage(HoleKontostand q) => …;              // direkte Abfrage (E2)
}

// Projektion: nur Events rein, nur Fähigkeiten eines Stores raus
public sealed class Kontoauszug : IProjektion
{
    public Task Auf(Abgebucht e, Herkunft h, ISchreibeBuchungszeile zeile) => zeile.Schreibe(h.Stream, h.Version, e.Betrag);
}

// Reader
public sealed class Kontoabfragen : IReader
{
    public OneOf<KontoAntwort, KontoUnbekannt> Frage(HoleKonto q, IFindeKonto finde) => …;   // KontoAntwort trägt Id<Konto>
}

// Funktion im Host: eine Klasse, je Eingangstyp ein Auf
public sealed class Mahnwesen(IMailversand mail) : IFunktion
{
    public OneOf<EroeffneMahnung, MahnungUnnoetig /*Hinweis*/>? Auf(ZahlungUeberfaellig e, Herkunft h) => …;
}

// Funktion draußen: als Schnittstelle, getragen von einem Client
public interface IBonitaetsPruefung : IFunktion
{
    OneOf<BonitaetBestaetigt, BonitaetAbgelehnt> Auf(KreditBeantragt e);
}

// Quelle
public sealed record ZahlungEingegangen : WebhookGemeldet;              // Treiber und Kennung aus der Konfig

// Store
public interface IKontoauszugStore : IStore, ISchreibeBuchungszeile, IFindeKonto { }
// Implementierung (Rolle Infrastruktur): … : IKontoauszugStore, IAtomaresZiel | IWiederholungsfestesZiel | INurMindestensEinmal

// Akteur und Client
public sealed record Kunde : IMensch, IDarf<Buche>, IDarf<HoleKonto>;
public interface IBankingApp : IClientVertrag, ISendet<Buche>, IFragt<HoleKonto>, IHoert<Abgebucht>;
public interface IScoringWorker : IClientVertrag, ITraegt<IBonitaetsPruefung>;
```

Rückgaben: konkreter Typ, `OneOf<…>`, `T?` („nichts“), `IEnumerable<…>`/`IAsyncEnumerable<…>` (Strom), jeweils auch als `Task<…>`.

### 3.4 Wer hört was

| Nachricht | darf in | Anzahl |
|---|---|---|
| Command | Aggregat | genau eins |
| Event (auch Quell-Nachricht) | Projektion, Funktion | beliebig viele |
| Event, vom Aggregat entschieden | sein `Apply` | genau das entscheidende Aggregat |
| Hinweis | Funktion (höchstens einmal), Client (`IHoert`) | beliebig viele; nie Projektion, nie Aggregat |
| Query | Reader **oder** Aggregat | genau eins |
| Frist | Uhr → Command an **dasselbe** Aggregat | genau eins |
| Antwort | der fragende Client | – |

### 3.5 Wer erzeugt was

| Baustein | erzeugt | sonst nichts |
|---|---|---|
| Aggregat | Events, Hinweise, Fristen; Antworten auf direkte Queries | keine Commands |
| Projektion | Wirkungen in ihrem Store (nur Schreib-Fähigkeiten) | keine Nachricht |
| Reader | Antworten (nur Lese-Fähigkeiten) | – |
| Funktion | Commands, Events, Quell-Nachrichten, Hinweise; nach außen nur über Dienste | – |
| Quelle | Quell-Nachrichten | – |
| Client | Commands, Quell-Nachrichten, Hinweise (`ISendet`), Queries (`IFragt`), Ausgaben getragener Funktionen (`ITraegt`) | – |
| Uhr | Commands aus fälligen Fristen | – |

### 3.6 Zusatzregeln und ihr Gegenstück im Build

| # | Regel | geprüft durch |
|---|---|---|
| R1 | Der Ausgang steht in der Signatur (`Decide`, `Auf`, `Frage`); der ganze Graph entsteht nur daraus, nie aus Rümpfen | Analyzer (heute CQRS050), erweitert auf `Auf`/`Frage` |
| R2 | Je Baustein und Eingangstyp höchstens ein `Auf` | Analyzer, **neu** |
| R3 | Ein Command gehört genau einem Aggregat (über `ICommand<TAggregat>`); jedes entschiedene Event hat ein `Apply` im selben Aggregat und umgekehrt; Events aus Funktionen/Quellen haben kein `Apply` | Generator/Analyzer (heute CQRS010, CQRS079/080) |
| R4 | Eine Query hat genau ein Ziel (Reader oder Aggregat) | Analyzer (heute CQRS084/086), erweitert |
| R5 | Eine Frist zielt auf einen Command des eigenen Aggregats | Analyzer, **neu** |
| R6 | Kreise Funktion → Command → Aggregat → Event → dieselbe Funktion werden gemeldet | Build-Warnung über den Signatur-Graphen, **neu** (ersetzt die Start-Prüfung `ProzessAzyklizität`) |
| R7 | Ids sind typisiert; Command-Ziele sind `Id<T>` | Compiler + Analyzer, **neu** |
| R8 | Fähigkeiten nur als Parameter (eine Funktion je Fähigkeit, ein Store bündelt), nie in Konstruktor/Feld/`new` | Analyzer (heute CQRS051/054/055) |
| R9 | Eine Projektion schreibt in genau einen Store | Generator (heute CQRS052) |
| R10 | Dienste nur im Konstruktor von Funktionen und Dienst-Implementierungen; eine Funktion mit Lese-Fähigkeit läuft im Host | Analyzer (heute CQRS089–092) + Start-Prüfung |
| R11 | Aggregate und Projektionen rechnen bei gleicher Eingabe gleich: keine Uhr, kein Zufall, kein `Guid.NewGuid()`, kein Lesen fremder Daten | Analyzer, **neu** |
| R12 | Funktionen halten ihre Ausgabe fest, bevor sie wirkt; Ausnahme nur nachweislich gleichbleibend (kein Dienst, keine Lese-Fähigkeit, keine Uhr, kein Zufall, Host) | Analyzer + Generator, **neu** |
| R13 | Neue Ids auf der Server-Seite nur abgeleitet (`Herkunft.NeueId`) | Analyzer, **neu** |
| R14 | Jeder Store implementiert genau eine Klasse; Klasse 3 nur mit ausdrücklicher Erlaubnis; Schreib-Fähigkeiten eines Klasse-2-Stores nehmen die `Herkunft` | Start-Prüfung + Analyzer, **neu**; Einhaltung: allgemeiner Store-Test |
| R15 | Die Garantie folgt dem Eingang: Event → genau einmal, Hinweis → höchstens einmal, Query → keine Wirkung | folgt aus den Typen |
| R16 | Alles von draußen kommt von einem Akteur, der es darf; für einen Client gilt Vertrag ∩ Token | Generator + Start-Prüfung (heute) |
| R17 | Ein Client sendet/fragt/hört/trägt nur, was seine Akteure dürfen; eine als Schnittstelle deklarierte Funktion braucht einen Träger (`ITraegt`) oder eine Bindung | Analyzer + Start-Prüfung (heute CQRS095–097, erweitert) |
| R18 | Die Hausregel für Actors (§1.7) | Analyzer, **neu** (Phase 10) |

Projektstruktur (docs/07, `Abstractions/Projektstruktur.cs`, `Domain.SourceGeneration/Lageart.cs`): Primitive Prozess, Fluss, Reaktion und
Auftrag entfallen; Funktion (Klasse = im Host, Schnittstelle = draußen) bleibt; neu sind Store-Klasse (Fakt am Store) und typisierte Ids (kein
eigener Ort). Jede neue Primitive ⇒ `Lageart.cs` + Tabelle + Sonde.

## 4 · Garantien

### 4.1 Die drei Bedingungen

Genau einmal **wirken** (nicht zustellen) heißt an jeder Übergabe: (1) **nichts geht verloren** — jedes Event wird mindestens einmal verarbeitet;
(2) **eine Wiederholung ist wirkungslos**; (3) **nur einer gewinnt**, auch wenn kurz zwei Aktivierungen laufen. Bedingung 1 tragen für alle Leser
gleich: Position rückt erst nach bestätigten Wirkungen vor; Position rückt nur bis zum Horizont der Vollständigkeit (Transaktionsnummern von
Postgres, heute schon); verlorene Weckrufe holt der Takt; ein Poison-Event wird geparkt, nicht übersprungen.

### 4.2 Übergaben

| Übergabe | genau einmal durch | Bedingung, Grenze |
|---|---|---|
| Quelle → Log | Kennung als Schlüssel: Version 0 des Streams (ein Stream je Nachricht) oder eindeutiger Index (ein Stream je Schlüssel) | was „gleich“ ist, sagt die Konfig |
| Funktion → Ausgaben | **Ausgabe festhalten** unter „Funktion F, Eingang X“ (eindeutig), dann aus diesem Eintrag senden; ein zweiter Lauf findet den Eintrag | Standard (A1); ohne Festhalten nur bei nachweislich gleichen Läufen (R12) — sonst wirken zwei verschiedene Ausgaben (anderes Read-Model, ML, `NewGuid` → anderes Ziel → andere Command-Id) |
| Commands aus Funktionen | abgeleitete Command-Id (Funktion, Eingang Stream+Version, Stelle, Typ, Ziel), Inbox des Ziels | Inbox-Horizont 30 Tage; hängt eine Funktion länger zurück, muss das Framework warnen, bevor es passiert |
| Command → Aggregat | Events und Quittung in einer Transaktion, Quittung eindeutig je Command-Id; ein Schreiber + Versionsprüfung | wie heute |
| Log → Projektion, Klasse 1 | Wirkung + Fortschritt in einer Transaktion; Fortschritt mit erwarteter Version | Projektion rechnet gleich (R11) |
| Log → Projektion, Klasse 2 | Event-Identität im Schlüssel oder bedingtes Schreiben; Fortschritt beim Framework nach Bestätigung | sonst Klasse 3 |
| Frist → Aggregat | Command-Id aus dem Frist-Event; die Uhr ist gleichbleibend und hält nichts fest | Auflösung ≈ 1 s |
| Client → Aggregat | dieselbe Command-Id beim Wiederholen | der Client speichert die Id **vor** dem Senden (Ausgangskorb im Client-SDK, §4.7) |
| Funktion beim Client | die erste Antwort gewinnt am Schlüssel „F, X“; Lease-Ablauf → nächster Worker | – |
| nach außen (Mail, API) | **nur mindestens einmal**; der Dienst bekommt eine stabile Id | genau einmal nur, wenn das fremde System die Id nutzt |
| Push an Clients, „P hat S weiterverarbeitet“ | **keine Garantie, gewollt** | verlässlich nur über Lesen ab Position (§2.7) |

### 4.3 Gegen docs/11, Abschnitt für Abschnitt

- **Schreibseite (§11.2):** unverändert. Neu: keine Marken im Log; eine Version je Aggregat; Ablehnungen bleiben idempotent über die Inbox.
- **Commands (§11.3):** unverändert (Client wiederholt mit derselben Id; emittiert über abgeleitete Id + Inbox).
- **Ordnung (§11.4):** je Stream in Projektionen unverändert; Funktionen: Eingänge eines Streams nacheinander. Über Streams keine Ordnung. „Prozesse
  ordnungsunabhängig“ leistet jetzt das Vorgangs-Aggregat in seinem `Decide` (Fachcode statt Framework-Versprechen).
- **Projektionen (§11.5):** genau einmal, zwei Aktivierungen, geteilte Dokumente, Vollständigkeit, Poison, Neuaufbau: unverändert (derselbe Leser).
  **Read-your-writes** wird exakt statt best-effort (Fortschritt statt Redis). Neue Bedingung: Store-Klasse (§2.4).
- **Reaktionen (§11.6) → Funktionen:** mindestens einmal ausgeführt, Ausgaben genau einmal wirksam, kein Reset/Replay, Wirkungen nach außen
  mindestens einmal.
- **Prozesse, Flüsse, Fristen (§11.7):** „kein Vorgang bleibt liegen“ entfällt als eigene Maschine (Aggregate warten passiv; liegen bleiben kann
  nur ein Funktions-Eingang, den Position + Takt holen). Fristen: mindestens einmal gefeuert, genau einmal wirksam. Kompensation ist Fachcode mit
  denselben Übergabe-Garantien. Funktion extern: Lease wie heute.
- **Quellen (§11.8):** unverändert; neu „ein Stream je Schlüssel“ als Option.
- **Bewusst ohne Garantie (§11.9):** Weckrufe, Pushes, „P hat S weiterverarbeitet“, Hinweise, Dead-Letter. Redis entfällt.
- **Build (§11.10):** R1 wird die tragende Regel; die Analyzer der entfallenen Bausteine entfallen.

### 4.4 Nachweis

1. Je Übergabe ein Absturz-Test mit Abbruch genau zwischen zwei Schritten (nach den Commands vor der Position; nach dem Festhalten vor den Commands;
   mitten im Batch; zwei Aktivierungen gleichzeitig; zwei Worker mit verschiedenen Antworten), Vergleich gegen das Log.
2. Der **allgemeine Store-Test** je Treiber (§2.4).
3. Die Exactly-once-Prüfung jedes Messlaufs und der Störungslauf, wie heute (docs/11 §11.14, docs/12).
4. Gegenprobe je neuem Mechanismus: im Server-Lauf per `sed` sabotieren, der Test muss rot werden.

## 5 · Was bleibt, was entfällt, was neu ist

**Bleibt:** Aggregat-Actor (Bündel, Inbox-Tabelle, Group-Commit, Horizont, Passivierung, Snapshots), Batch-Leser der Projektionen samt Wächtern,
Parken, Neuaufbau-Logik; Quellen und Treiber; Funktions-Vermittlung mit Lease (wandert in den Funktions-Actor); `CommandEmitter` (wird `Frage` mit
Pflicht-Frist, Drossel je Ziel bleibt); Akteur-Rechte, Handshake-Ermittlung; Spur (`ursache`, `wirker`, `ueber`, `stand`; Wirker `funktion:<F>`,
`decide:<A>/<C>`, `uhr`); Umgebung als Datei; Codegen-Prepass; Prüfstand/Integration/Prüfdomäne/Vermessung.

**Entfällt** (mit Generatoren, Analyzern und Tests, die nur sie prüfen):
- Broker: `Infrastructure/PubSub/` außer `CommandEmitter`/`EmitAusgaenge` (wird mitgezogen), `Abstractions/SignalEnvelope.cs`,
  `IStateChangeSignal.cs`, `Messaging/IMessagePublisher.cs`, `Options/PubSubOptions.cs`; `Domain.SourceGeneration/SignalTypeGenerator.cs`,
  `Infrastructure.SourceGeneration/SignalFactoryGenerator.cs`; Signal-Typen in Wire und Proto; der Push-Pfad der Subscriber.
- Signal-Empfang: `Infrastructure/Projections/SignalReceiver*.cs`, `Weckungen.cs` (das Zusammenfassen wandert in den Lebenszyklus),
  `SignalAdapterActor.cs`, `EmittentenAdapter.cs`, der Emittenten-Teil von `PullPath.cs`/`Poller.cs`.
- Dirigent: `Infrastructure/Prozess/` (Manager, Actor, Wiring, Router, Poll, Heilplan, `DetachedProzessSend`, Prozess-Events),
  `MartenProzessMarkingStore`, `MartenProzessOffenIndex`, `Abstractions/Prozess/` (Builder, Regeln, Marking, `FlussBelegung`, Azyklizität,
  Graph, Regel-Hash), `Abstractions/Fluss/Pipeline.cs`, `ProzessId`, `IProzessIntern`; Generatoren `ProzessRegelnGenerator`,
  `ProzessRegelDiagnosticGenerator`, `ProzessVerbundGenerator`, `FlussAnalyzer`.
- Handle-/Trigger-Welt: `Infrastructure/Pipeline/`, `Infrastructure/Deadlines/` (ersetzt durch die Uhr), `Planung.cs`, `Frist.cs` (alt),
  `PipelineContext`, `PipelineAck`, `IngressAttribute`, `IPipelineHandler`/`IPipelineTrigger`/`IPipelineSelfMessage`;
  `PipelineActorGenerator`, `PipelineDispatchGenerator`; `TriggerHandlerRegistry`; Client→Client-Trigger im Python-SDK.
- Reaktion als eigene Art (`HandlerOutputRouter`), `IPullSubscriber`, `SubscriberId`, `ProjectionWriter`/`Execute`/`Track`,
  `ProjectionReaderAttribute`/`TrackDeps`, `ReadContext`, `IReadModelDepsReader`, `ReadModelDepsWriter/Reader`, `RedisVersionTracker`,
  `IVersionTracker` — **Redis entfällt ganz** (Kandidat: Compose-Dateien und Umgebungen ohne Redis).
- Marken: `KommandoVerarbeitet`, `KommandoAbgelehnt`; Funktions-Auftrag `IAuftrag<F>`, `IAusfuehrung`, Ausführungs-Stream je Aufruf.
- Akteur-Vertrag mit `Auf` (`IAkteurVertrag<A>`), `IAkteurDienst` (nur Handle-Welt); `ICreationCommand`; `IQueryResponse`. `ImAuftrag`
  (Akteur-Kopfzeile) bleibt.
- Im Werkzeug: Prozess-/Fluss-Lesen in `GraphExtractor.Kern` (`FlussLeser.cs`, Teile von `DomainExtractor`, `Sicht/*`), `DomainEditor/Fluss.cs`,
  Fluss-/Saga-DSL in `Cqrs.Testing` (`Fluss.cs`, `Saga.cs`), Sonde-Texte `Fluss.cs.txt`, `Gebuehren.cs.txt` (umschreiben statt löschen).

**Neu:** `Id<T>`; `IFunktion` mit `Auf`; Funktions-Actor (Position, Fortschritt, Slots, Festhalten, Laufort); Uhr mit `Frist<TCmd>`;
Quell-Nachricht vom Client; Weckplan-Generator + Wecker am Flush; `Melde`/`Frage`; Actor-Lebenszyklus im Framework; Sitzung als Actor; Verteiler;
`IHoert`/`ITraegt`; Lesen ab Position über die Leitung; Store-Klassen-Schnittstellen + allgemeiner Store-Test + Fortschritt beim Framework;
Neuaufbau über Generationen; direkte Aggregat-Abfrage; Analyzer R2, R5–R7, R11–R14, R18.

## 6 · Schritte

Reihenfolge nach Abhängigkeit. Jede Phase ist für sich baubar und grün; Messen nur, wo der heiße Pfad sich ändert (kurz, A-B, Messserver).
Schätzungen in Personentagen (PT), grob.

### Phase 0 · Netz und Basis (2–3 PT)

1. **Verhaltens-Tests, bevor etwas fällt** (gegen den heutigen Code grün): Client sieht jedes Event mit richtiger Kopf-Version; Blazor-Quittung über
   die Korrelation; Ablehnung landet bei zwei Knoten an der richtigen Sitzung; Client hört nur Vertrag ∩ Token; Zusage-Antwort dedupliziert;
   Read-your-writes nach eigenem Command; Exactly-once je Domänen-Ablauf (Bildeingang, Datensatz, Sammelvorgang, Auftrag mit Kompensation,
   Verteilung, Trainings-Wächter) als fachliches Soll, unabhängig vom Mechanismus.
2. **Basis-Messung** S1, S3-64, S5-64, L1 (≈ 15 min) plus ein Profil (`dotnet-trace`, `dotnet-counters`, ≈ 5 min) unter S5-64; Ablage
   `vermessung/ergebnisse/`.
3. Drei **fremde Szenarien** als Sonde neben die Beispiele (`GraphExtractor.Kern/Sonde/`, `soll.txt`), damit das Modell nicht auf die Beispiele
   passt: Benutzer mit eindeutiger E-Mail (Eindeutigkeits-Aggregat), Sensor-Strom mit Alarm (Hinweis-Funktion + Zähl-Aggregat), Massen-Import mit
   Fortschritt (eine Quell-Nachricht, Funktion mit Strom-Ausgabe).

### Phase 1 · Typisierte Ids (3–5 PT)

`Id<T>` in `Abstractions`; `ICommand<TAggregat>`; Generatoren (Routing, Wire, Proto, STJ, Python-Basen, Blazor-Wiring) bilden `Id<T>` als UUID ab;
Beispiel- und Prüfdomäne, Clients, Tests umstellen; `_id` des Aggregats aus der Cluster-Identität, fremde Id abgelehnt, bevor geladen wird.
Abnahme: Build, alle Suiten grün, `./codegen.sh --check`, Python- und Blazor-Abnahme.

### Phase 2 · Actor-Lebenszyklus und zwei Verben (3–4 PT)

Eine Basis/Middleware für alle Framework-Actors (Bereich, Stopp, Fehler, Zusammenfassen); `Melde`/`Frage` (Pflicht-Frist) in einer Klasse;
`CommandEmitter` auf `Frage`; `CommandResult` nur Ausgang, Versionen, Ablehnung. Kein Verhalten ändert sich. Abnahme: Prüfstand-Tests je
Lebenszyklus-Fall (Bereich freigegeben nach Passivierung, Fehler beantwortet, Weckungen zusammengefasst).

### Phase 3 · Funktion als Baustein und Uhr (6–9 PT)

1. `IFunktion` mit `Auf(Eingang[, Herkunft][, Lese-Fähigkeiten])` und freien Ausgaben; Generator für Dispatch, Weckplan-Einträge und Batch-Form.
2. **Funktions-Actor** je (Funktion, Partition): Position und Fortschritt beim Framework, Slots, Backoff, Festhalten der Ausgabe (R12) mit
   eindeutigem Schlüssel, Senden aus dem festgehaltenen Eintrag, Laufort Host/Worker/Client/Hand über die bestehende Vermittlung mit Lease;
   Takt als Netz.
3. **Uhr** mit `Frist<TCmd>` (Fälligkeits-Verzeichnis, Command-Id aus dem Frist-Event); **Takt-Quelle** als Treiber (ersetzt Timer-Trigger).
4. **Quell-Nachricht vom Client** (Kennung = Command-Id); Quelle „ein Stream je Schlüssel“ als Option.
5. Analyzer R2, R11–R13. Absturz-Tests aus §4.4 für Funktion und Uhr.
Noch neben dem Alten; nichts wird hier gelöscht.

### Phase 4 · Domänen umschreiben, Altes zurückbauen (6–10 PT)

1. Beispiel- und Prüfdomäne, Sonde: Reaktion → Funktion; Prozesse und Flüsse → Vorgangs-Aggregate + Funktionen + Fristen (Bildeingang mit
   „anlegen oder ergänzen“ im Aggregat; Datensatz-Flüsse; Trainings-Wächter als Frist; Auftrag mit Gegenzug als Auftrags-Aggregat; Verteilung
   über Zähl-Aggregat; `TeilFertigProzess` → Funktion); Zusagen → `ITraegt`; Kenntnis → `IHoert`.
2. **Rückbau** von Dirigent, Fluss-/Prozess-Sprache, Handle-/Trigger-Welt, Reaktion, Akteur-Vertrag mit `Auf` (§5) samt Generatoren, Analyzern
   und Tests; `DomainEditor/Grammatik.cs` auf §3; `GraphExtractor.Kern` liest die neuen Bausteine, sodass `--check`, `--sonde`, `--grammatik`
   grün sind; `Cqrs.Testing` mit Szenarien für Aggregat + Funktion statt Saga/Fluss. Werkbank-Server baut; Oberfläche unberührt (E4);
   Werkbank-Abnahmetests, die Fluss/Prozess voraussetzen, bekommen `Skip = "Editor zurückgestellt (plan-pubsub-neustrukturierung E4)"`.
3. Messung A-B gegen Phase 0 (S1, S3-64, S5-64): erwartet deutlich weniger Events und Transaktionen je Vorgang.

### Phase 5 · Marken aus dem Log (2–3 PT)

`KommandoVerarbeitet`/`KommandoAbgelehnt` entfallen (nur noch Inbox-Tabelle); eine Version je Aggregat; `VersioningModule` mit einer Version;
Ablehnungen als Hinweis an die Sitzung oder fachliches Event. Abnahme: Ablehnung idempotent nach Neustart, Client-OCC mit Version aus der Antwort.

### Phase 6 · Wecker statt Broker (4–6 PT)

Weckplan-Generator (Event-Typ → Partitionen der Projektionen und Funktionen, Uhr, Verteiler; Prüfung: jeder Hörer steht mit jedem gehörten Typ im
Plan); Wecker am Flush inkl. `IsolateAsync`; Broker, Signale, Receiver, Push-Pfad, Signal-Generatoren, Redis-Versionsindex zurückbauen;
Prozess-/Emittenten-Polls entfallen (Takt je Partition bleibt). Tests: verlorene und doppelte Meldung, zwei Knoten mit hartem Ende eines Knotens
(Weckungen kommen an, ohne auf den Takt zu warten), Störungslauf. Messung A-B-B-A.

### Phase 7 · Clients (5–8 PT)

Sitzung als Actor (einziger Schreiber, begrenzt, `ReenterAfter`); Verteiler je Knoten; Handshake meldet an; alter Handshake-Weg und
`SubscriptionTracker` fallen; gezielte Nachrichten über den Knoten in der Sitzungs-Id; Read-your-writes „mindestens S auf v“; Stand je Id in
Antworten; Beobachtung „P hat S weiterverarbeitet“ mit Zusammenfassen; Lesen ab Position über die Leitung; Ausgangskorb im Client-SDK
(Command-Id vor dem Senden speichern); Python-SDK und Blazor auf `ISendet`/`IFragt`/`IHoert`/`ITraegt`. Abnahme: die Client-Tests aus Phase 0
unverändert grün, dazu transitive Aktualisierung (Datensatz-Ansicht nach fremdem Label) und Wiederverbinden mit Nachlesen.

### Phase 8 · Stores und Projektionsform (5–8 PT)

Projektions-Signatur `Auf(Event[, Herkunft], Fähigkeiten)` ohne Writer/Track/Envelope/SubscriberId; Store-Klassen-Schnittstellen (§2.4) statt
`IProjectionTracker`/`ICoCommitTracker`/`IAppendProjektion`; Fortschritt beim Framework für Klasse 2; Neuaufbau über Generationen; allgemeiner
Store-Test; Marten-Store als Klasse 1; ein Klasse-2-Treiber (Dateisystem/Objektspeicher) und ein Klasse-3-Sink als Nachweis; Analyzer R14.
Vorbedingung: G-11 geklärt oder eingegrenzt (§1.8). Messung A-B (S1, S3).

### Phase 9 · Direkte Aggregat-Abfrage (1–2 PT)

`Frage(Query)` im Aggregat (R4), Antwort mit Version; Wire/Leitung; Test „Antwort entspricht dem Stand, auf dem der nächste Command entscheidet“.

### Phase 10 · Hausregel als Build-Prüfung (2–3 PT)

Analyzer R18 über alle `Proto.IActor`-Typen und die Typen, die ein Actor als Feld besitzt (Fehler; Ausnahme nur per Attribut mit Begründung);
`Infrastructure.csproj` bindet den Analyzer ein. Gegenprobe je Verbots-Muster.

### Phase 11 · Doku einarbeiten, Plan löschen (2–3 PT)

Neues `docs/10-entwickler-api.md` („Wie schreibe ich X?“ nach §3); docs/03, 04, 05, 06, 08, 09, 11, 12, 13 und `konzept-akteure.md` auf den neuen
Stand; Invariante 2 neu (§2.3) in `CLAUDE.md`; Verweise im Code auf gelöschte Dokumente nachziehen; dieser Plan wird gelöscht.

Summe grob 40–65 PT.

## 7 · Risiken

1. **Lücke im Weckplan** merkt man nicht am Verlust, sondern an Latenz bis zum Takt → Generator-Prüfung „jeder Hörer mit jedem gehörten Typ“ und je
   Konsumentenart ein Latenz-Test.
2. **Festhalten kostet** je Funktionslauf einen Eintrag; bei kurzen Funktionen im Host ist R12 die Ausnahme — wird sie zu eng, steigt die Last.
3. **Veraltete PID beim Melden** (Aktivierung umgezogen) → Dead Letters, der Takt heilt; der Zwei-Knoten-Test mit hartem Ende muss es zeigen.
4. **Ausgeschriebene Abläufe** (Kompensation als Aggregat + Funktionen) sind mehr Code als `RückgängigDurch`; Prüfstein ist die Prüfdomäne in Phase 4.
5. **Lange Funktionen halten die Position** ihrer Partition; die Liste „dahinter fertig“ braucht eine Grenze, sonst wächst sie.
6. **Große Abhängigkeitsmengen** in Antworten (ein Datensatz mit 10 000 Paaren) → ab einer Grenze nur die Position statt aller Versionen.
7. **Typisierte Ids** berühren jede Domäne, alle Generatoren und beide Client-SDKs; ein Fehler dort bricht viel — Phase 1 allein und vollständig grün.
8. **Werkbank** verliert vorübergehend Fluss-/Prozess-Karten (E4); das Abnahme-Netz der Werkbank ist bis zur Editor-Arbeit dünner.

## 8 · Offen

- **A2** Zugehörigkeit zum Vorgang: fachliches Feld (Vorschlag) oder `Herkunft.Korrelation`.
- **Löschen und Aufbewahren** (Recht auf Löschung, Archiv): eigenes Konzept nach dem Umbau; Option „verschlüsselbare Felder als Typ-Fakt“.
- **Funktionen auf Hinweisen** (R15) sind im Zielbild; ob es dafür schon in Phase 3 einen Nutzer gibt (Sensor-Sonde), entscheidet sich dort.
- **Grenze für Abhängigkeitsmengen** (Risiko 6) und **Zusammenfassungs-Takt** des Verteilers: in Phase 7 messen.

## 9 · Nach dem Umbau

Phase 11 arbeitet den Plan in die Kapitel ein; danach gelten wieder die Kapitel als Ist und `CLAUDE.md` als Gedächtnis. Gelöschte Dokumente (siehe
`docs/README.md`) liegen im Git-Verlauf.
