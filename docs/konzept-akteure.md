# Akteure und Clients

> Stand 2026-11-07, gegen den Code geprüft.
>
> Referenz für *wer* etwas hineingibt und was zu ihm kommt (Akteur) und *welche Software es über welche Leitung tut* (Client), dazu die Laufzeit
> der Clients (§5: Sitzung, Verteiler, Antworten, Lesen, Reaktionen). Die Grammatik steht in [10](10-entwickler-api.md) §10.3.6, die Garantien in
> [11](11-garantien.md), der Blazor-Client in [08](08-frontend-blazor-client.md), das Python-SDK in [09](09-python-sdk.md). Abschnittsnummern
> sind stabil, weil Code-Kommentare darauf verweisen. Die Beispiele (Klassifizierer, Arbeitsplatz …) stammen aus der **Beispieldomäne** — sie
> ist Prüfling, nicht Architektur (W11). Offenes: §9.

---

## 1. Begriffe

| Begriff | Frage | Code-Fakt | Beispiel |
|---|---|---|---|
| **Akteur** | *Wer* handelt fachlich? | `record X : IAkteur` (`Abstractions/Akteur.cs`) | `Inspekteur`, `Klassifizierer` |
| **darf** | Was gibt er selbst hinein? | `IDarf<T>`: ein Command, eine Query, ein Hinweis | der Inspekteur darf `LabelBildPaar` |
| **hört** | Was kommt zu ihm? | `IHoert<T>`: ein Event oder Hinweis — sein Client **reagiert** darauf (§3) oder nimmt es zur **Kenntnis** | der Klassifizierer hört `ImagePairKomplett` (er reagiert mit seinem Urteil) und `ModellAktiviert`; der Rechnungspruefer der Prüfdomäne hört `RechnungZurPruefungVorgelegt` |
| **Client** | *Welche Software* hängt an der Leitung, für wen spricht sie? | `record X : IClient, IVerkoerpert<A>…` | `KlassifikationsWorker` (Python), `Arbeitsplatz` (Blazor) |
| **verkörpert** | Für welche Akteure spricht der Client? | `IVerkoerpert<A>` am Client | der KlassifikationsWorker verkörpert den Klassifizierer |
| **Identität** | Wer meldet sich an? | Token → Akteur-Menge (Umgebung, `Identitaeten:Tokens`) | Token `sim-arbeitsplatz` → {Inspekteur, KIOperator, Modellfreigeber, Produktpruefer} |

**Leitlinie:** so wenig Begriffe wie möglich, alles Code-Fakt in Basisliste oder Signatur (keine Namenskonvention, keine Attribute zum Raten). Ein
Akteur sagt, was er hineingibt und was er hört; ein Client sagt nur, wen er verkörpert. Was über die Leitung eines Clients geht, folgt daraus und
wird nicht deklariert.

```
            Identität (Token)            Client (Software, eine Leitung)          Akteur (fachlich)
  Token sim-klassifizierer ──►  KlassifikationsWorker ───────────────────────►  Klassifizierer
  Token sim-trainingssystem ─►  TrainingsWorker ─────────────────────────────►  TrainingsSystem
  Token sim-arbeitsplatz ────►  Arbeitsplatz ──────┬─────────────────────────►  Inspekteur
                                                   ├─────────────────────────►  KIOperator
                                                   ├─────────────────────────►  Modellfreigeber
                                                   └─────────────────────────►  Produktpruefer
  Wirksam ist immer, für beide Richtungen:  verkörpert(Client) ∩ Akteure(Token)
```

---

## 2. Akteur

### 2.1 Deklaration

```csharp
public sealed record KameraSystem   : IAkteur, IDarf<NimmBildAuf>;
public sealed record Klassifizierer : IAkteur,
    IHoert<BildVerfuegbar>, IHoert<ImagePairKomplett>, IHoert<ModellAktiviert>,
    IDarf<KlassifiziereBildPaarDurchKi>, IDarf<KlassifiziereEinzelBildDurchKi>, IDarf<HoleAktivesModell>;
public sealed record Inspekteur     : IAkteur, IDarf<LabelBildPaar>, IDarf<MarkiereAlsInspiziert>, IDarf<SucheImagePairs> /* … */,
    IHoert<ImagePairErstellt>, IHoert<ImagePairKomplett> /* … */;
```

- Ein Akteur ist immer ein Record, nie ein Dienst; eine Art (Mensch, Maschine, KI) gibt es nicht.
- **`IDarf<T>`** = was der Akteur *selbst* hineingibt: einen Command (an ein Aggregat oder an den Kopf eines Graphen), eine Query, einen Hinweis
  (CQRS058; ein Event aus dem Log, eine Antwort oder eine Schnittstelle kann man nicht dürfen). Ein Typ darf bei mehreren Akteuren stehen.
- **`IHoert<T>`** = was zu ihm kommt: ein Event oder ein Hinweis. Auf ein Event kann sein Client **reagieren** (§3) — mit Commands aus dem `IDarf`
  seiner Akteure; ohne Ausgabe ist es **Kenntnis** — gepusht, verlierbar. Einen Command (an ein Aggregat oder ohne Ziel), eine Query oder eine
  Antwort hört kein Akteur (CQRS058). Hören ist deklariert, nicht abgeleitet: gepusht wird nur, was ein Akteur hört.
- Was ein Graph oder eine Frist aus einem Eingang erzeugt, wird **nicht** deklariert — es trägt den Akteur der Kette (§2.3). Ein Command, den
  niemand per `IDarf` darf, kommt am Tor nie durch („nie von der GUI“ ist damit ein Typ-Fakt).
- **Verwirklichung:** womit ein Akteur seine Nachrichten tatsächlich erzeugt — eine App gegen seinen generierten Vertrag, oder eine **Quellart**
  (Dateiwächter, Taktgeber, Webhook, eine eigene): eine Übersetzung der Akteur-Seite in `Domain.Akteure/Uebersetzungen.cs` macht aus ihrer Meldung
  eine Nachricht, die er darf (`DateiGefunden_LiesBild_1` für `KameraSystem : IDarf<LiesBild>`). Welche Quellarten ihn verwirklichen können, folgt
  aus diesen Übersetzungen; welche laufen, sagen die Eingänge der Clients ([04](04-konsum-und-prozess-maschine.md) §4.7).

### 2.2 Katalog der Beispieldomäne (`Domain.Akteure/Akteure.cs`)

| Akteur | gibt selbst hinein (`IDarf`) | hört (`IHoert`: Reaktion oder Kenntnis) |
|---|---|---|
| KameraSystem | `LiesBild` (den Kopf der Bildaufbereitung; verwirklicht durch Dateiwächter) | – |
| TrainingsSystem | `MeldeTrainingBegonnen`, `MeldeFortschritt`, `MeldeTrainingAbgeschlossen`, `MeldeTrainingGescheitert`, `HoleDatensatzSamples`, `HoleTrainingslauf` | `TrainingAngefordert` (Reaktion: das Training) |
| Datensatzpfleger | `NimmRangeAuf`, `SchliesseEinfrierenAb`, `SucheImagePairs`, `GetImagePair` | `RangeAngefordert`, `EinfrierenAngefordert` (Reaktion: auflösen bzw. abschließen) |
| Klassifizierer | `KlassifiziereBildPaarDurchKi`, `KlassifiziereEinzelBildDurchKi`, `HoleAktivesModell` | `ImagePairKomplett` (Reaktion: das Urteil), `BildVerfuegbar`, `ModellAktiviert` |
| Inspekteur | Labeln (Paar, Einzelbild, Region), `MarkiereAlsInspiziert`, Lese-Queries der Bildpaare | Galerie und Statistik (`ImagePairErstellt`, `ImagePairKomplett`, Labels …) |
| Produktpruefer | `LabelPhysischesProdukt`, `GetImagePair`, `SucheImagePairs` | `ImagePairErstellt`, `PhysischesProduktGelabelt` |
| KIOperator | Datensätze, Training, `RegistriereModell`, deren Queries | Datensätze, Trainingsläufe, Modelle |
| Modellfreigeber | `SetzeModellAktiv`, `ArchiviereModell`, `HoleModelle`, `HoleAktivesModell` | `ModellRegistriert`, `ModellAktiviert`, `ModellArchiviert` |
| Disponent | `StarteSammelvorgang`, `StarteTeilauftrag`, `SchließeTeilauftragAb` | – |
| Teilmelder | `MeldeTeilFertig` | `TeilauftragAbgeschlossen` (Reaktion: das Teil am Sammelvorgang melden) |

Die Prüfdomäne (`Pruefdomaene.Akteure`): der `Rechnungspruefer` prüft Rechnungen über 10 000 von Hand (er hört `RechnungZurPruefungVorgelegt`,
gibt `GibRechnungFrei` oder `SperreRechnung`), der `Messwart` fordert Hochrechnungen an, die `Sachbearbeitung` gibt auf die Fakten der Abläufe
die Commands an die Köpfe ihrer Graphen (`RechnungGestellt` ↦ `Freizugeben`, `AuftragGestartet` ↦ `AuftragAbzuwickeln`, `PostenReserviert` mit
Fehlmenge ↦ `Unterschritten` …) — verkörpert vom Client `ReaktionsWorker` (`Pruefdomaene.Client.Worker`).

### 2.3 Von welchem Akteur kommt ein Command?

| Fall | Akteur | Beispiel |
|---|---|---|
| 1. kommt durchs **Tor** (Client, JSON-Eingang) | der Akteur der Sitzung, der `IDarf<T>` hat; verkörpert sie mehrere, der erste, der ihn darf (`AkteurRechte.AkteurFuer`) | `FriereEin` vom Arbeitsplatz → KIOperator |
| 2. gibt ein **Graph** oder die **Uhr** in einer Kette aus | der Akteur des Auslösers, geerbt (beim Eingang aus einer Quelle der Akteur des Eingangs) | `ReservierePosten` trägt die Sachbearbeitung, die `AuftragAbzuwickeln` gab; `NimmBildAuf` trägt das KameraSystem, dessen Dateiwächter die Datei fand |
| 3. kommt **ohne Kette** (Command ohne Akteur der Kette) | **der eine** Akteur mit `IDarf<T>` (`AkteurHerkunft.EindeutigerHalter`); dürfen ihn mehrere oder keiner: kein Akteur (`system`) | – |

Jede Kette beginnt bei einem Akteur (Fall 1 oder 3) und trägt ihn weiter (Fall 2). Statisch hat eine Nachricht eine Akteur-*Menge* (Fixpunkt über
den Graphen, `DomainEditor/AkteurAnteile.cs`, §8), zur Laufzeit genau einen (§5.1).

### 2.4 Person, Rolle und fachliches „Wer“

- **Eine Person kann mehrere Akteure verkörpern:** Token → Akteur-*Menge* in der Umgebung unter `Identitaeten:Tokens`
  (`"sim-arbeitsplatz": "Inspekteur,KIOperator,Modellfreigeber,Produktpruefer"`); `AddCqrs` liest den Abschnitt über `AddAkteure`
  (`Infrastructure/Akteure/AkteurOptionen.cs`, Header `akteur-token`). Weitere Schlüssel: `Standard` (Akteur für Verbindungen ohne Token) und
  `VertragStreng` (§5.2). Das Tor ist opt-in: ohne Abschnitt `Identitaeten` bleibt der gRPC-Pfad offen (heute so in `prod` und `multinode`).
  Die typisierte Form `AkteurOptionen.Token<TA, TB>(…)` benutzen nur Tests.
- **Ist das „Wer“ selbst fachlich** (Vier-Augen: Freigeber ≠ Antragsteller), steht es als Feld in Command und Event — normale Modellierung.
  Die `Herkunft` trägt den Akteur ebenfalls ([10](10-entwickler-api.md) §10.4).

---

## 3. Reaktion

Was nach einem Fakt weitergehen soll, stößt ein **Client** an, dessen Akteur es hört: ein Decide gibt nur Events, Hinweise und Fristen aus
(CQRS114), einen Command hört kein Akteur (CQRS058). Die Reaktion sind Commands aus dem `IDarf` der verkörperten Akteure — an ein Aggregat oder
ohne Ziel an den Kopf eines Graphen —, davor darf sie fragen (Queries ihres `IDarf`).

- **Genau einmal** ohne Festhalten beim Framework: der Client liest das Log ab seiner gesicherten Position, leitet die Command-Ids ab und sichert die
  Position nach den Antworten (§5.5). Jeder Command trägt das Event als `ursache`; die Spur folgt einem Vorgang so über die Reaktion hinweg.
- Ein Client, der reagiert, ist ein eigener Prozess (ein Worker), keine Oberfläche: in C# über `Client.Infrastructure/Reaktionen`, in Python ist
  die Methode `auf_<event>` der generierten Basis die Reaktion ([09](09-python-sdk.md) §9.4, §9.5).
- **Kenntnis** dagegen ist nur Push: verlierbar, ohne Ausgabe; wer es verlässlich braucht, liest ab Position oder fragt (§5.5).

Was ein Client rechnet, ist also nie eine Funktion eines Graphen: eine Funktion läuft im Host; was draußen geschieht, rechnet ein Client und gibt
es als Commands seines Akteurs zurück. Bewusst aufgegeben ist damit, denselben Schritt je Umgebung zwischen Host und draußen umzuschalten.

---

## 4. Client

### 4.1 Form

```csharp
namespace Domain.Akteure;   // Domain.Akteure/Clients.cs

public sealed record KlassifikationsWorker : IClient, IVerkoerpert<Klassifizierer>;
public sealed record TrainingsWorker      : IClient, IVerkoerpert<TrainingsSystem>;
public sealed record ReaktionsWorker      : IClient, IVerkoerpert<Datensatzpfleger>, IVerkoerpert<Teilmelder>;
public sealed record Arbeitsplatz         : IClient, IVerkoerpert<Inspekteur>, IVerkoerpert<Produktpruefer>, IVerkoerpert<KIOperator>,
                                                     IVerkoerpert<Modellfreigeber>;
public sealed record Linienrechner        : IClient, IVerkoerpert<KameraSystem>;   // ohne Leitung: Eingänge aus Quellen
```

- Ein Client ist die Software-Einheit, die Akteure verkörpert — über **eine Leitung** (eine Verbindung, eine Sitzung) und/oder über **Eingänge aus
  Quellen**, die im Host laufen (in der Umgebung `Clients:<Name>:Eingaenge`, je Eingang Art, Akteur, Rechner, Werte; drei Verzeichnisse = drei
  Eingänge, auch verschiedener Art und für verschiedene seiner Akteure). Er kann mehrere Akteure verkörpern, und ein Akteur kann über mehrere
  Clients laufen (n:m). Ein Eingang aus einer Quelle braucht kein Token: wirksam ist sein Akteur.
- Der Client ist ein Record und **nennt nur**, wen er verkörpert (`IVerkoerpert<A>`, nur konkrete Akteure); eigene Member, eigene Befugnisse oder
  ein eigenes Hören gibt es nicht (CQRS063, CQRS064). Gelesen wird er an einer Stelle (`Infrastructure.SourceGeneration/Clientvertragslesart.cs`).
- Handshake-Name = Name des Records (`Clientvertragsregeln.ClientName`); Akteur- und Client-Namen sind zusammen eindeutig (CQRS059).

### 4.2 Was über die Leitung geht (abgeleitet)

Das `IDarf` und das `IHoert` der verkörperten Akteure: einen Command oder einen Hinweis **sendet** er, eine Query **fragt** er
(`Clientvertragsregeln.Art`, eine Regel für Server-Generator, Client-Generator, Python-Codegen, Extractor und Editor-Sicht); ein gehörtes Event
oder einen Hinweis **hört** er. Ein eigenes Wort am Client gibt es dafür nicht — befugt ist der
Akteur, nicht die Software. Bekommt ein Akteur eine Befugnis dazu, ändert sich der Hash jedes Clients, der ihn verkörpert
(`Clientvertragsregeln.ClientKanon`).

### 4.3 Rechte

- **Wirksam = verkörperte Akteure ∩ Token-Akteure**, für beide Richtungen (`Infrastructure/Akteure/ClientVertrag.cs`, `Rechte`): je verkörpertem
  Akteur, den das Token nennt, sein `IDarf` (hinein) und sein `IHoert` (heraus). Ohne Tor gelten alle verkörperten Akteure.
  Verkörpert das Token keinen Akteur des Clients, lehnt der Handshake ab.
- Der Client **nennt keine eigene Befugnis** (CQRS064: verkörpern kann er nur konkrete Akteure).

### 4.4 Innen und Rand

Ein Client hat eine Innenseite (Blazor: Intents, Client-Events, Stores; Python: State) und einen Rand (die Akteure, die er verkörpert). Nur der Rand
geht über die Leitung; client-interne Typen stehen nie bei einem Akteur.

### 4.5 Clients der Beispieldomäne (`Domain.Akteure/Clients.cs`)

| Client | verkörpert | reagiert auf | sendet / fragt | hört außerdem |
|---|---|---|---|---|
| `KlassifikationsWorker` (`classifier.py`, `run_stub.py`) | Klassifizierer | `ImagePairKomplett` | `KlassifiziereBildPaarDurchKi`, `KlassifiziereEinzelBildDurchKi` / `HoleAktivesModell` | `BildVerfuegbar`, `ModellAktiviert` |
| `TrainingsWorker` (`training_worker.py`, `run_train_stub.py`) | TrainingsSystem | `TrainingAngefordert` | die `Melde…`-Commands / `HoleDatensatzSamples`, `HoleTrainingslauf` | – |
| `ReaktionsWorker` (`Domain.Client.Worker`, .NET) | Datensatzpfleger, Teilmelder | `RangeAngefordert`, `EinfrierenAngefordert`, `TeilauftragAbgeschlossen` | `NimmRangeAuf`, `SchliesseEinfrierenAb`, `MeldeTeilFertig` / `SucheImagePairs`, `GetImagePair` | – |
| `Linienrechner` (Eingänge: Dateiwächter je Linie) | KameraSystem | – | `LiesBild` (aus `DateiGefunden`) | – |
| `Arbeitsplatz` (Blazor) | Inspekteur, KIOperator, Modellfreigeber, Produktpruefer | – | alle Commands und Queries der vier Akteure | was die vier hören |

Ein Ketten-Command, den nur ein Graph ausgibt, steht bei keinem Akteur (Prüfdomäne: `ReservierePosten` aus der Auftragsabwicklung); die
Mehrfach-Auswahl beim Kuratieren schickt je Paar `NimmPaarAuf`.

### 4.6 C#-Client (Blazor): Code = Akteure

- Die Client-Assembly nennt ihren Client: `[assembly: IstClient<Arbeitsplatz>]` (`Domain.Client.Modules.Blazor/Arbeitsplatz.cs`).
- Der Client-Generator (`Client.SourceGeneration/WiringGenerator.cs`, `ClientVertrag.cs`) liest ihn mit **derselben** Lesart wie der Server
  (`Clientvertragslesart`, per Link) und erzeugt `Vertrag` (den Client-Namen), `VertragHash` (derselbe Hash wie `GeneratedClientVertraege`),
  `ServerEventTypes` = das `IHoert` der Akteure (Kenntnis), `CommandTypes` und `RegisterQueries` = Commands bzw. Queries ihres `IDarf`.
- Der Handshake nennt nur `vertrag` und `vertrag_hash`. Nennt die Antwort einen anderen Client, startet die Verbindung nicht
  (`ConnectionModule.ConnectAsync`); ein abweichender Hash steht als Warnung im Client, im strengen Modus lehnt schon der Server ab (§5.2).
- Build-Regeln: §7 (CQRS093, 095–097).
- Verbindung aus der **Umgebung**: `Host.Blazor --umgebung <name>` liest `Clients:<Name>` (`Token`, `Adresse` — leer = localhost:`Transport:GrpcPort`,
  `Urls`, `Dateien`). In `sim-main` nennt das Token `sim-arbeitsplatz` die vier Akteure. Mehr: [08](08-frontend-blazor-client.md) §8.2, §8.6.

---

## 5. Laufzeit

### 5.1 Die Kette

- Der Akteur reist als Event-Header **`akteur`** (`ImAuftrag.Header`): `MartenEventBatchWriter` schreibt ihn, `MartenEventStore` liest ihn in den
  Envelope (`UserId`).
- Ein offener Eingang trägt den Akteur seines Auslösers; die Kette eines Graphen nimmt ihn mit (beim Eingang aus einer Quelle den Akteur des Eingangs), und
  der `CommandEmitter` stempelt ihn (`ImAuftrag.Akteur`) auf jeden Command der Kette; die Uhr nimmt den Akteur, der in der Frist steht
  (`UhrActor`). Ist er leer, gilt Fall 3 (§2.3).
- Von draußen schreibt der Akteur auch die Spur: ein Command vom Client trägt den Wirker `akteur:<A>` (`WirkerId.FuerAkteur`).

### 5.2 Verbindung und Handshake

- **Sitzung als Actor:** je gRPC-Verbindung prüft `CqrsClientServiceImpl` das Token (`AkteurTor.Erkenne`; ungültig → `Unauthenticated`) und
  startet einen `SitzungsActor` — den **einzigen Schreiber** auf den Antwort-Stream. Die Lese-Schleife der Verbindung reicht jede
  Client-Nachricht nur weiter (`VonClient`); was wartet (Query, offener Eingang am Kopf), läuft neben der Mailbox. Die Sitzungs-Id nennt den Knoten:
  `<Knoten>#<Nr>` (`Verteilung.SitzungsId`).
- **Client:** der Client nennt im `CapabilitiesRequest` `vertrag` (seinen Namen) und `vertrag_hash`. `ClientVertragsPruefung.Pruefe` sucht ihn in
  der generierten Tabelle (unbekannt → Ablehnung) und rechnet „verkörpert ∩ Token“ (§4.3; leer → Ablehnung). Hash ≠ Server → Warnung im Log, im
  strengen Modus (`Identitaeten:VertragStreng = true`) Ablehnung. Eine Ablehnung schickt `VERTRAG_ABGELEHNT` und beendet den Aufruf mit
  `PermissionDenied`.
- **Befugnis statt Selbstauskunft:** erlaubt ist die wirksame Befugnis, gehört wird genau, was die wirksamen Akteure hören
  (`ClientVertragsPruefung.Wende`). Ein Client **ohne** Client-Record nennt in `message_types`, was er hören, senden und fragen will; mit Tor gilt
  davon nur, was der Akteur des Tokens darf bzw. hört (`AkteurTor.Wende`), ohne Tor alles. `CommandFailed` darf jede Sitzung hören.
- Die `CapabilitiesResponse` nennt den angenommenen Client mit dem Server-Hash. Danach meldet die Sitzung ihre wirksame Hör-Liste beim
  **Verteiler** ihres Knotens an (`SitzungMeldetAn`, ersetzt die vorige; schon beim Start mit leerer Liste, damit gezielte Antworten sie vor dem
  Handshake erreichen).

### 5.3 Antworten und CommandId

**Commands vom Client.** Die Sitzung setzt `OriginSessionId` auf ihre Id, stempelt mit Tor den Akteur (`AkteurFuer`, nicht vom Client
behauptet) und prüft die Befugnis; leitet `AggregateType` aus dem Command-Typ ab, wenn er fehlt; eine negative `ExpectedVersion` heißt „ohne
OCC“ (Modus `Emittiert`, nur Inbox-Dedup), sonst gilt OCC. Die Command-Id vergibt der Client und behält sie über alle Wiederholungen
(Ausgangskorb); die Inbox des Ziels lässt jede Id genau einmal wirken. Zurück kommt, gezielt an die Sitzung und mit der Korrelation des
Commands (§5.4): die **Quittung** (`CommandQuittung`: Ausgang und Version des Aggregats danach, vom Dispatcher, sobald das Aggregat geantwortet
hat — auch für einen Noop), die fachliche Ablehnung (der Hinweis aus dem Decide) oder `CommandFailed` (Versionskonflikt, technischer Fehler,
Akteur darf nicht — `AkteurVerweigerung`, Platzierung erschöpft — `AggregateDispatcher`); dazu die Events, die seine Akteure hören. Alles
verlierbar. Wie der C#-Client daraus Wiederholung und Ausgangskorb macht: [08](08-frontend-blazor-client.md) §8.4.

**Commands ohne Ziel vom Client** (gRPC oder JSON-Eingang): die Sitzung prüft das `IDarf` und legt je Instanz, deren Graph ihn am Kopf nimmt, einen
offenen Eingang (`SitzungsActor.AnKopfAsync`, Kennung aus Command-Id und Instanz — wer wiederholt, schickt dieselbe Id und legt keinen zweiten);
erst dann quittiert sie (`CommandQuittung` mit seiner Korrelation). Nennt die Hülle ein Fakt als `ursache` (`Stream:Version`), ist es die Herkunft
des Eingangs: die Kette und ihre Commands tragen das Fakt als Ursache. Nimmt kein Graph ihn am Kopf, antwortet sie `KEIN_EMPFAENGER`.

**Hinweise vom Client** (`TransientEventRequest`, nur `ITransientEvent`, mit Tor nur mit `IDarf`): gehen an die Sitzungen der Akteure, die sie hören
(Verteiler); die Kennung vergibt der Client (`CausationId`).

### 5.4 Zustellung an Clients

- **Verteiler je Knoten** (`Infrastructure/Verteilen/`): ein lokaler Actor mit festem Namen `verteiler` (`VerteilerActor`), Tabelle Typ →
  Sitzungen seines Knotens und Sitzungs-Id → Sitzung; er beobachtet jede Sitzung (`Watch`) und trägt sie aus, wenn sie endet. Ein Aggregat meldet je
  Commit **eine** Nachricht (`Verteile`) an den Verteiler jedes Knotens, nur mit Typen, die irgendein Akteur hört (`Verteilung.Hoerbar`); gezielte
  Envelopes (`ZielSitzung`) nur an den Knoten, den die Sitzungs-Id nennt. Der Verteiler fasst je Commit und Sitzung zu einer `Zustellung` zusammen.
  Kein Broker, keine Abos im Cluster.
- **Kenntnis:** verlierbarer Push (Invariante 6) an jede Sitzung, deren wirksame Liste den Typ nennt — Events aus dem Log und Hinweise (aus Graphen,
  gescheiterte Ketten, von Clients). **Gezielt**, gleich was die Sitzung hört: die Ablehnung eines Aggregats und `CommandFailed` an die Sitzung des
  Commands (`OriginSessionId`); die Ablehnung dazu an jede Sitzung, deren Akteure sie hören — jede Sitzung einmal.
- **Rückstau:** liest ein Client nicht nach, verwirft der Verteiler über 1000 offenen Zustellungen je Sitzung (`Rueckstau`).
- Wer Kenntnis durabel braucht, liest ab Position oder fragt per Query (§5.5).

### 5.5 Lesen: Queries, Aktualisierung, Log

- **Queries** (`QueryRequest`, mit Tor nur mit `IDarf`) beantwortet der Query-Dienst des Hosts — ein Reader oder ein Aggregat
  ([10](10-entwickler-api.md) §10.3.1, §10.3.3). Die Antwort trägt je gezeigter Id den Stand (`deps`); bei einem Aggregat seine Version als
  OCC-Stand. Daraus und aus den Pushes führt der Client **eine Version je Aggregat** (`Client.Infrastructure/Versioning/VersioningModule.cs`) als
  `ExpectedVersion` des nächsten Commands.
- **Read-your-writes:** der Client nennt „mindestens S auf v“ je Stream, den er zuletzt beschrieben hat (`QueryRequest.mindestens`); die
  Leseseite wartet begrenzt, bis die Projektion jeden so weit verarbeitet hat, sonst sagt die Antwort `noch_nicht` (Daten = der Stand davor).
- **Aktualisiert:** die Sitzung merkt sich, was ihre Antworten zeigen (Streams der gezeigten Ids, höchstens 200 Antworten; über 1000 Ids oder
  ohne Ids grob die ganze Projektion) und meldet das dem Verteiler (`SitzungBeobachtet`). Nach jedem Projektions-Commit, der Events anwandte, geht
  „P hat S weiterverarbeitet“ (`Verarbeitet`) an den Verteiler jedes Knotens und weiter an die betroffenen Sitzungen; diese nennen dem Client,
  zusammengefasst höchstens alle 200 ms, die betroffenen Queries (`Aktualisiert`) — er fragt neu. Verlierbar.
- **Lesen ab Position** (`LiesLog` → `LogSeite`): das Log ab einer Position, nur die Typen der Hör-Liste der Sitzung, die im Log stehen (Hinweise
  nie), höchstens 1000 Einträge je Seite und nur bis zum Horizont der Vollständigkeit — wer ab `bis` weiterliest, verpasst nichts. So führt ein
  Client eine Projektion bei sich oder liest nach dem Wiederverbinden nach.
- **Reaktionen** (§3): was nach einem Fakt weitergehen soll, stößt ein Client an. Er hört das Event
  (`IHoert`), liest, was er braucht (Queries), und gibt Commands hinein (`IDarf`) — an ein Aggregat oder ohne Ziel an den Kopf eines Graphen.
  Genau einmal ohne Festhalten beim Framework: der Läufer (`Client.Infrastructure/Reaktionen`, `cqrs_client.reaktionen`) liest das Log ab seiner
  gesicherten Position (`LiesLog`; ein Push weckt ihn nur), leitet jede Command-Id aus Reaktion, Position, Nummer und Stelle ab (in C# und Python
  dieselbe Formel) und sichert die Position erst, wenn jeder Command der Seite beantwortet ist. Nach einem Neustart rechnet er dieselben Ids; die
  Inbox des Ziels bzw. der offene Eingang am Kopf verwirft die Wiederholung. Liegt der `horizont` einer Log-Seite unter der eigenen Position (eine
  neue Datenbank), liest er von vorn. Jeder Command trägt das Event, auf das reagiert wird, als Ursache (`ursache` der Hülle, Spur): an einem
  Aggregat stempelt der Server es an die Events, am Kopf wird es die Herkunft des Eingangs — die Ursache-Kette bleibt über die Reaktion hinweg
  lückenlos, ein Vorgang ist seine Korrelation samt ihren Folgen über `ursache`. Ein Client, der reagiert, ist ein eigener Client (ein Worker),
  keine Oberfläche. In der Beispieldomäne ist das der `ReaktionsWorker` (`Domain.Client.Worker`): als
  `Datensatzpfleger` löst er Ranges auf und schließt das Einfrieren ab, als `Teilmelder` meldet er jedes fertige Teil am Sammelvorgang. Die
  Werkbank startet einen .NET-Worker über `Clients:<Name>:Worker:Projekt` (`dotnet run --project`), Prüfstände lassen ihn neben dem Host laufen
  (Prüfdomäne: in-process über gRPC; Verhaltens-Netz: `Hostlauf` startet den Worker der Anwendung je Host; Messung: `Cqrs.Vermessung/Reaktionsworker.cs`).
- **Keine Queries an einen Client:** ein Client fragt, er beantwortet nie — eine Query hat genau ein Ziel, einen Reader oder ein Aggregat (R4).

---

## 6. Generierung (eine Quelle: die Basislisten)

| Generat | Ort | Inhalt |
|---|---|---|
| `GeneratedAkteurRechte` | `Infrastructure.SourceGeneration/AkteurRechteGenerator.cs` | je Akteur: Commands, Queries, Hinweise (aus `IDarf`), gehörte Events und Hinweise (aus `IHoert`) |
| `GeneratedClientVertraege` | dito | je Client: die verkörperten Akteure und der Client-Hash |
| `GeneratedWiring_<Assembly>` (C#-Client) | `Client.SourceGeneration/WiringGenerator.cs` | `Vertrag`, `VertragHash`, `ServerEventTypes` (`IHoert` der Akteure), `CommandTypes` und `RegisterQueries` (ihr `IDarf`) — aus `[assembly: IstClient<X>]` |
| `domain_client/generated/vertraege.py` | `Cqrs.Codegen/PythonVertragsEmitter.cs` (Codegen-Schritt im Build) | je Client `<Client>Basis` (`CLIENT`, `AKTEURE`, `VERTRAG_HASH`, `HOERT`, `SPONTAN`, `FRAGT`, abstrakte Methode `auf_<event>` je gehörtem Typ) |

- **Kanon und Hash** an einer Stelle (`Abstractions/Clientvertragsregeln.cs`: `Kanon`, `ClientKanon`, `Hash` = 16 Hex von SHA-256): Server,
  Python und Extractor ergeben denselben Hash; ein Client gegen einen anderen Stand fällt am Handshake auf. Die Generatoren laufen nur in der
  Komposition (`Laufzeitprojekt.Ist`).
- **Python-Basis** (`Client.Infrastructure.Python/cqrs_client/vertrag.py`, `reaktionen.py`): verdrahtet je gehörtem Typ `auf_<event>`, prüft jede
  Ausgabe gegen `SPONTAN` (sonst `VertragsVerletzung`), sendet Stücke einzeln über den Läufer, verweigert den Start bei fehlender Methode (ABC),
  prüft `query()` gegen `FRAGT`, meldet Client und Hash am Handshake. Mehr: [09](09-python-sdk.md) §9.4, §9.5.

---

## 7. Regeln (Build ⇄ Grammatik)

| Build | Grammatik | Regel |
|---|---|---|
| CQRS058 | GR-AKTEUR | `IDarf<T>` nur für Hineingehendes (Command, Query, Hinweis), `IHoert<T>` nur für ein Event oder einen Hinweis (einen Command hört kein Akteur) — beides nur an einem `IAkteur` |
| CQRS059 | GR-AKTEUR | Akteur- und Client-Namen eindeutig (Generator-Diagnose) |
| CQRS063 | GR-CLIENT | Client: ein Record, der nur `IClient` und `IVerkoerpert<A>` nennt; deklariert selbst nichts |
| CQRS064 | GR-CLIENT-BEFUGT | Verkörpert = ein konkreter Akteur |
| CQRS116 | GR-COMMAND | ein Command ohne Ziel hat genau einen Empfänger: einen Knoten desselben Graphen oder den Kopf genau eines Graphen |
| CQRS093, 095–097 | GR-CLIENT-CODE | C#-Client: Client genannt (093), erzeugte Commands (095) und gestellte Queries (096) darf ein verkörperter Akteur, `Handle(E, MessageContext)` auf ein Server-Event nur, wenn einer es hört (097) — `Client.SourceGeneration/ClientVertrag.cs` |
| — (Laufzeit-Tor) | GR-HERKUNFT | jeder Command und jede Query kommt von einem Akteur, direkt oder über die Kette (Warnung in der Grammatik; das Tor weist ohne `IDarf` ab) |

Die Grammatik ist eine Quelle (`DomainEditor/Grammatik.cs`, [10](10-entwickler-api.md) §10.6: R16, R17); `--check` prüft, dass jede genannte
Build-ID im Code vorkommt.

---

## 8. Editor

Auf der Fläche des Editors steht ein Akteur zweimal: links mit dem, was er darf, rechts mit dem, was er hört ([10](10-entwickler-api.md) §10.9).
Was die Werkzeuge heute zu Akteuren und Clients ableiten (W3: gerechnet
wird im Backend):

- **Akteur-Mengen** (`DomainEditor/AkteurAnteile.cs`): je Nachricht die Akteure **direkt** (`IDarf<T>`) und **über die Kette** (aus dem Eingang
  des Bausteins, der sie erzeugt — Decide, Frist, Funktion), als Fixpunkt über Mengen. Der Extractor legt sie als `rahmen.akteurMengen` ins Board
  (`GraphExtractor.Kern/ModellMapper.cs`).
- **Sicht** (`GraphExtractor.Kern/Sicht/`): je Karte die Akteure und den Akteur-Rahmen (Rahmen je Domäne × Akteur; was keinem Akteur zuzuordnen
  ist, steht im Rahmen „ohne Akteur“, GR-HERKUNFT), je Akteur `IDarf` und `IHoert`, je Client die verkörperten Akteure (`IVerkoerpert`).
- Gesten schreiben Akteur- und Client-Record als Code (`DomainEditor/Scaffolder.cs`, `GraphExtractor.Kern/Schreiben/DateiAbgleich.cs`).

Zur Laufzeit trägt ein Command genau einen Akteur (§2.3, §5.1).

---

## 9. Offen

Steht in [13](13-reifegrad-schulden-bewertung.md) §13.0:

- **AK-1** — `als` im Envelope bei mehreren verkörperten Akteuren (heute der erste, der darf), Anmeldung je Person am Arbeitsplatz (heute ein
  Token je Umgebung), kein Start-Check, ob `Clients:<Name>:Token` die Akteure des Clients nennt.
- **FB-14** — das Python-SDK kennt weder Ausgangskorb noch neu Fragen nach `Aktualisiert` (ab Position liest nur sein Reaktions-Läufer).
- **FB-15** — Überlast an einer Sitzung: Verwerfen nur mit Log-Warnung; Nachrichten des Clients ohne Grenze in der Mailbox.
- **FB-19** — Antworten eines Aggregats auf eine direkte Query werden nicht beobachtet (kein `Aktualisiert`).
- **P1-2** — kein TLS; Identitäten für Prod fehlen (in `prod`/`multinode` gibt es keinen Abschnitt `Identitaeten`, das Tor ist dort offen).
- **WZ-1** — der Fixpunkt prüft Akteure und Clients nicht (das Inventar von `--check` und die Sonde schon).

Messungen und Testzahlen: [12](12-tests-und-vermessung.md).
