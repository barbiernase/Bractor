# Akteure, Verträge, Clients

> Stand 2026-10-29, gegen den Code geprüft.
>
> Referenz für *wer* etwas hineingibt (Akteur) und *welche Software es über welche Leitung tut* (Client mit seinem Vertrag), dazu die Laufzeit
> der Clients (§5: Sitzung, Verteiler, Antworten, Lesen). Die Grammatik steht in [10](10-entwickler-api.md) §10.3.6, die Garantien in
> [11](11-garantien.md), der Blazor-Client in [08](08-frontend-blazor-client.md), das Python-SDK in [09](09-python-sdk.md). Abschnittsnummern
> sind stabil, weil Code-Kommentare darauf verweisen. Die Beispiele (Klassifizierer, Arbeitsplatz …) stammen aus der **Beispieldomäne** — sie
> ist Prüfling, nicht Architektur (W11). Offenes: §9.

---

## 1. Begriffe

| Begriff | Frage | Code-Fakt | Beispiel |
|---|---|---|---|
| **Akteur** | *Wer* handelt fachlich, was darf er hineingeben? | `record X : IMensch\|IMaschine\|IKi, IDarf<T>…` (`Abstractions/Akteur.cs`) | Inspekteur darf `LabelBildPaar` |
| **Funktion draußen** | Was rechnet eine Software außerhalb des Hosts auf einen Eingang hin? | eine Funktions-Schnittstelle `interface IF : IFunktion { … Auf(E e); }` | `IBildpaarBeurteilung`: `ImagePairKomplett` → `KlassifiziereBildPaarDurchKi` |
| **Client-Vertrag** | *Welche Software* hängt an der Leitung, was geht über sie? | `interface IX : IClientVertrag, ITraegt<F>, ISendet<T>, IFragt<Q>, IHoert<E>` | `IKlassifikationsWorker` (Python), `IArbeitsplatz` (Blazor) |
| **trägt** | Welche Funktion rechnet der Client? | `ITraegt<F>` (früher „Zusage“) | der KlassifikationsWorker trägt `IBildpaarBeurteilung` |
| **hört** | Welches Event oder welchen Hinweis nimmt er nur zur Kenntnis? | `IHoert<E>` (früher „Kenntnis“) | der Worker lädt bei `ModellAktiviert` das neue Modell |
| **Identität** | Wer meldet sich an? | Token → Akteur-Menge (Umgebung, `Identitaeten:Tokens`) | Token `sim-arbeitsplatz` → {Inspekteur, KIOperator, Modellfreigeber, Produktpruefer} |

Was ein Akteur draußen rechnet, ist eine **Funktion**: ihre Signatur steht einmal in den Abläufen, ein Client trägt sie (`ITraegt<F>`), ein
Akteur darf sie rechnen (`IDarf<F>`). Für sie gelten dieselben Garantien wie für jede Funktion (Festhalten, Lease, die erste Antwort gewinnt —
[04](04-konsum-und-prozess-maschine.md) §4.4, [11](11-garantien.md) §11.6). Einen eigenen Akteur-Vertrag mit `Auf` oder einen Dienst eines
Akteurs gibt es nicht.

**Leitlinie:** so wenig Begriffe wie möglich, alles Code-Fakt in Basisliste oder Signatur (keine Namenskonvention, keine Attribute zum Raten).
Was abgeleitet werden kann (was ein Akteur hört, welche Akteure ein Client verkörpert, von wem eine Nachricht über die Kette stammt), wird nicht
deklariert.

```
            Identität (Token)            Client (Software, eine Leitung)          Akteur (fachlich)
  Token sim-klassifizierer ──►  KlassifikationsWorker ───────────────────────►  Klassifizierer
  Token sim-trainingssystem ─►  TrainingsWorker ─────────────────────────────►  TrainingsSystem
  Token sim-arbeitsplatz ────►  Arbeitsplatz ──────┬─────────────────────────►  Inspekteur
                                                   ├─────────────────────────►  KIOperator
                                                   ├─────────────────────────►  Modellfreigeber
                                                   └─────────────────────────►  Produktpruefer
  Wirksam ist immer:  Vertrag(Client) ∩ Akteure(Token)
```

---

## 2. Akteur

### 2.1 Deklaration

```csharp
public sealed record KameraSystem   : IMaschine, IDarf<DateiErkannt>;
public sealed record Klassifizierer : IKi,       IDarf<IBildpaarBeurteilung>, IDarf<KlassifiziereEinzelBildDurchKi>, IDarf<HoleAktivesModell>;
public sealed record Inspekteur     : IMensch,   IDarf<LabelBildPaar>, IDarf<MarkiereAlsInspiziert>, IDarf<SucheImagePairs> /* … */;
```

- **Art** in der Basisliste: `IMensch`, `IMaschine`, `IKi` (alle `: IAkteur`). Ein Akteur ist immer ein Record, nie ein Dienst.
- **`IDarf<T>`** = was der Akteur *selbst* hineingibt: Command, Query, Quell-Nachricht oder Hinweis — und `IDarf<F>` auf eine
  Funktions-Schnittstelle = darf diese Funktion draußen rechnen (CQRS058). Ein Typ darf bei mehreren Akteuren stehen.
- Was eine Funktion oder eine Frist daraus erzeugt, wird **nicht** deklariert — es trägt den Akteur der Kette (§2.3). Ein Command, den niemand
  per `IDarf` darf, kommt am Tor nie durch („nie von der GUI“ ist damit ein Typ-Fakt).
- **Hören** wird abgeleitet (`AkteurRechteGenerator`): die Events der Aggregate seiner Commands und die Events der Projektionen hinter seinen
  Queries. Das gilt für eine Sitzung ohne Client-Vertrag; mit Vertrag hört sie genau dessen `IHoert` (§5.2).

### 2.2 Katalog der Beispieldomäne (`Domain.Akteure/Akteure.cs`)

| Akteur | Art | gibt selbst hinein (`IDarf`) |
|---|---|---|
| KameraSystem | Maschine | `DateiErkannt` (Quell-Nachricht über den Datei-Treiber) |
| Bildrechner | Maschine | `IBildaufbereitung` (darf die Bildaufbereitung draußen rechnen) |
| TrainingsSystem | Maschine | `ITraining`, `HoleDatensatzSamples` |
| Klassifizierer | KI | `IBildpaarBeurteilung`, `KlassifiziereEinzelBildDurchKi`, `HoleAktivesModell` |
| Inspekteur | Mensch | Labeln (Paar, Einzelbild, Region), `MarkiereAlsInspiziert`, Lese-Queries der Bildpaare |
| Produktpruefer | Mensch | `LabelPhysischesProdukt`, `GetImagePair`, `SucheImagePairs` |
| KIOperator | Mensch | Datensätze, Training, `RegistriereModell`, deren Queries |
| Modellfreigeber | Mensch | `SetzeModellAktiv`, `ArchiviereModell`, `HoleModelle`, `HoleAktivesModell` |
| Disponent | Mensch | `StarteSammelvorgang`, `StarteTeilauftrag`, `SchließeTeilauftragAb` |

### 2.3 Von welchem Akteur kommt ein Command?

| Fall | Akteur | Beispiel |
|---|---|---|
| 1. kommt durchs **Tor** (Client) | der Akteur der Sitzung, der `IDarf<T>` hat; verkörpert sie mehrere, der erste, der ihn darf (`AkteurRechte.AkteurFuer`) | `FriereEin` vom Arbeitsplatz → KIOperator |
| 2. gibt eine **Funktion** oder die **Uhr** in einer Kette aus | der Akteur des auslösenden Eingangs, geerbt — auch, wenn die Funktion draußen rechnet | `NimmRangeAuf` trägt den, der `FuegeRangeHinzu` schickte |
| 3. kommt **ohne Kette** (Quell-Nachricht eines Treibers, Command ohne Akteur der Kette) | **der eine** Akteur mit `IDarf<T>` (`AkteurHerkunft.EindeutigerHalter`); dürfen ihn mehrere oder keiner: kein Akteur (`system`) | `DateiErkannt` → KameraSystem |

Jede Kette beginnt bei einem Akteur (Fall 1 oder 3) und trägt ihn weiter (Fall 2). Wer eine Funktion draußen rechnet, muss sie dürfen
(`IDarf<F>`); das ist eine Befugnis am Tor, kein Akteur-Wechsel. Statisch hat eine Nachricht eine Akteur-*Menge* (Fixpunkt über den Graphen,
`DomainEditor/AkteurAnteile.cs`, §8), zur Laufzeit genau einen (§5.1).

### 2.4 Person, Rolle und fachliches „Wer“

- **Eine Person kann mehrere Akteure verkörpern:** Token → Akteur-*Menge* in der Umgebung unter `Identitaeten:Tokens`
  (`"sim-arbeitsplatz": "Inspekteur,KIOperator,Modellfreigeber,Produktpruefer"`); `AddCqrs` liest den Abschnitt über `AddAkteure`
  (`Infrastructure/Akteure/AkteurOptionen.cs`, Header `akteur-token`). Weitere Schlüssel: `Standard` (Akteur für Verbindungen ohne Token) und
  `VertragStreng` (§5.2). Das Tor ist opt-in: ohne Abschnitt `Identitaeten` bleibt der gRPC-Pfad offen (heute so in `prod` und `multinode`).
  Die typisierte Form `AkteurOptionen.Token<TA, TB>(…)` benutzen nur Tests.
- **Ist das „Wer“ selbst fachlich** (Vier-Augen: Freigeber ≠ Antragsteller), steht es als Feld in Command und Event — normale Modellierung.
  Die `Herkunft` trägt den Akteur ebenfalls ([10](10-entwickler-api.md) §10.4).

---

## 3. Funktionen draußen

Eine Funktion, die außerhalb des Hosts rechnet, ist eine Schnittstelle in den Abläufen (Rolle `Pipeline`, [07](07-projektstruktur.md)):

```csharp
public interface IBildpaarBeurteilung : IFunktion
{
    KlassifiziereBildPaarDurchKi Auf(ImagePairKomplett e);
}

public interface ITraining : IFunktion
{
    IAsyncEnumerable<OneOf<MeldeTrainingBegonnen, MeldeFortschritt, MeldeTrainingAbgeschlossen, MeldeTrainingGescheitert>>
        Auf(TrainingAngefordert e);                                 // ein Strom: mehrere Ausgaben je Eingang
}
```

- Je Eingang ein `Auf`; der **Rückgabetyp ist der Ausgabe-Vertrag** (CQRS100), wie bei jeder Funktion ([10](10-entwickler-api.md) §10.3.4).
- Wo sie läuft, sagt die Umgebung (`Funktionen:<F>:Laufort` = Host, Extern oder Hand); eine Schnittstelle ohne Umsetzung im Host läuft draußen,
  wenn ein Client sie trägt — trägt sie keiner, warnt der Build (CQRS087), und sie läuft nur mit einer Bindung von Hand.
- Die Antwort des Clients prüft der Server gegen die Signatur, hält sie fest (die erste gewinnt) und sendet sie selbst; der Client ist nur die
  Hand, die Garantie bleibt beim Funktions-Actor (§5.3).

---

## 4. Client

### 4.1 Form

```csharp
namespace Domain.Akteure;   // Domain.Akteure/Clients.cs — eigenes, leichtes Projekt: ein Client referenziert seinen Vertrag, nicht die Verarbeitung

public interface IKlassifikationsWorker : IClientVertrag,
    ITraegt<IBildpaarBeurteilung>,                   // rechnet diese Funktion
    ISendet<KlassifiziereEinzelBildDurchKi>,         // von sich aus (muss ein Akteur dürfen)
    IFragt<HoleAktivesModell>,                       // Query
    IHoert<BildVerfuegbar>, IHoert<ModellAktiviert>; // hören, verlierbar

public interface IArbeitsplatz : IClientVertrag,
    ISendet<LabelBildPaar>, ISendet<FriereEin>, ISendet<SetzeModellAktiv> /* … */,
    IFragt<SucheImagePairs>, IFragt<HoleDatensaetze> /* … */,
    IHoert<ImagePairKomplett> /* … Live-Aktualisierung */;
```

- Ein Client ist **eine Verbindung, eine Sitzung, ein Vertrag**. Er kann mehrere Akteure verkörpern, und ein Akteur kann über mehrere Clients
  laufen (n:m).
- Der Vertrag **stellt nur zusammen**: `ITraegt<F>`, `ISendet<T>`, `IFragt<Q>`, `IHoert<E>` und andere Client-Verträge; er deklariert selbst
  nichts (CQRS063). Gelesen wird er an einer Stelle (`Infrastructure.SourceGeneration/Clientvertragslesart.cs`), auch geerbte Client-Verträge.
- Handshake-Name = Interface ohne führendes I (`IKlassifikationsWorker` → `KlassifikationsWorker`, `Clientvertragsregeln.ClientName`); Akteur-
  und Client-Namen sind zusammen eindeutig (CQRS059).

### 4.2 Verkörpert (abgeleitet)

Je getragener Funktion, `ISendet` und `IFragt` sein **einziger** `IDarf`-Halter (Pflicht); darf ein Typ **mehrere**, kommen sie alle nur dann
hinzu, wenn noch **keiner** seiner Halter verkörpert ist (sonst wäre der Arbeitsplatz über `HoleDatensatzSamples` auch das TrainingsSystem).
Was ein Client nur hört, verkörpert keinen Akteur. Eine Regel für Server-Generator, Python-Codegen, Extractor und Editor-Sicht:
`Abstractions/Clientvertragsregeln.cs` (`Verkoerpert`).

### 4.3 Rechte

- **Wirksam = Vertrag ∩ Token-Akteure** (`Infrastructure/Akteure/ClientVertrag.cs`, `Rechte`): je verkörpertem Akteur, den das Token nennt,
  Commands, Quell-Nachrichten und Hinweise = Sendet ∩ `IDarf`, Queries = Fragt ∩ `IDarf`, Funktionen = Traegt ∩ `IDarf`; gehört wird das ganze
  `IHoert` des Vertrags. Ohne Tor gelten alle verkörperten Akteure. Verkörpert das Token keinen Akteur des Clients, lehnt der Handshake ab;
  bietet der Client eine Funktion an, die keiner seiner Akteure rechnen darf, lehnt der Server das Angebot ab (`FUNKTION_NICHT_BEFUGT`).
- Der Client-Vertrag **schneidet aus der Befugnis, er erweitert sie nie** (CQRS064: was er trägt, sendet oder fragt, darf ein Akteur; hören kann
  er nur Events und Hinweise).

### 4.4 Innen und Rand

Ein Client hat eine Innenseite (Blazor: Intents, Client-Events, Stores; Python: State) und einen Rand (den Vertrag). Nur der Rand geht über die
Leitung; client-interne Typen stehen nie im Vertrag.

### 4.5 Clients der Beispieldomäne (`Domain.Akteure/Clients.cs`)

| Client | verkörpert | trägt | sendet / fragt | hört |
|---|---|---|---|---|
| `KlassifikationsWorker` (`classifier.py`, `run_stub.py`) | Klassifizierer | `IBildpaarBeurteilung` | `KlassifiziereEinzelBildDurchKi` / `HoleAktivesModell` | `BildVerfuegbar`, `ModellAktiviert` |
| `TrainingsWorker` (`training_worker.py`, `run_train_stub.py`) | TrainingsSystem | `ITraining` | — / `HoleDatensatzSamples` | `TrainingAbgebrochen` |
| `Arbeitsplatz` (Blazor) | Inspekteur, KIOperator, Modellfreigeber, Produktpruefer | — | Commands und Queries der vier Akteure | die Events, auf die seine Stores hören |

Die Bildaufbereitung (`IBildaufbereitung`, Akteur Bildrechner) trägt kein Client-Vertrag; sie rechnet der generische Funktions-Worker
(`funktions_worker.py`, Laufort `Extern`, [09](09-python-sdk.md) §9.5). Ein Ketten-Command wie `NimmRangeAuf` (nur aus der Funktion) steht in
keinem Client-Vertrag; die Mehrfach-Auswahl beim Kuratieren schickt je Paar `NimmPaarAuf`.

### 4.6 C#-Client (Blazor): Code = Vertrag

- Die Client-Assembly nennt ihren Vertrag: `[assembly: IstClient<IArbeitsplatz>]` (`Domain.Client.Modules.Blazor/Arbeitsplatz.cs`).
- Der Client-Generator (`Client.SourceGeneration/WiringGenerator.cs`, `ClientVertrag.cs`) liest ihn mit **derselben** Lesart wie der Server
  (`Clientvertragslesart`, per Link) und erzeugt `Vertrag`, `VertragHash` (derselbe Hash wie `GeneratedClientVertraege`), `ServerEventTypes` =
  `IHoert`, `CommandTypes` = `ISendet`, `RegisterQueries` = `IFragt`.
- Der Handshake nennt nur `vertrag` und `vertrag_hash`. Nennt die Antwort einen anderen Vertrag, startet die Verbindung nicht
  (`ConnectionModule.ConnectAsync`); ein abweichender Hash steht als Warnung im Client, im strengen Modus lehnt schon der Server ab (§5.2).
- Build-Regeln: §7 (CQRS093–097).
- Verbindung aus der **Umgebung**: `Host.Blazor --umgebung <name>` liest `Clients:<Name>` (`Token`, `Adresse` — leer = localhost:`Transport:GrpcPort`,
  `Urls`, `Dateien`). In `sim-main` verkörpert das Token `sim-arbeitsplatz` die vier Akteure. Mehr: [08](08-frontend-blazor-client.md) §8.2, §8.6.

---

## 5. Laufzeit

### 5.1 Die Kette

- Der Akteur reist als Event-Header **`akteur`** (`ImAuftrag.Header`): `MartenEventBatchWriter` schreibt ihn, `MartenEventStore` liest ihn in den
  Envelope (`UserId`).
- Wer aus einem Eingang etwas erzeugt, setzt dessen Akteur als `ImAuftrag.Von(…)` (`AkteurHerkunft.Aus`): die Funktion je Eingang
  (`FunktionsArbeit`, aus Event oder Hinweis; auch für eine Funktion draußen — der Eingang trägt ihn, `ArbeitsAuftrag.akteur`), die Uhr mit dem
  Akteur, der in der Frist steht (`UhrActor`). Der `CommandEmitter` stempelt `ImAuftrag.Akteur`; ist er leer, gilt Fall 3 (§2.3).
- Quell-Nachrichten eines Treibers reisen ohne Envelope: ihr Akteur ist der eindeutige `IDarf`-Halter (`AkteurHerkunft.EindeutigerHalter`, an
  `Quellen` übergeben).
- Von draußen schreibt der Akteur auch die Spur: ein Command vom Client trägt den Wirker `akteur:<A>` (`WirkerId.FuerAkteur`).

### 5.2 Verbindung und Handshake

- **Sitzung als Actor:** je gRPC-Verbindung prüft `CqrsClientServiceImpl` das Token (`AkteurTor.Erkenne`; ungültig → `Unauthenticated`) und
  startet einen `SitzungsActor` — den **einzigen Schreiber** auf den Antwort-Stream. Die Lese-Schleife der Verbindung reicht jede
  Client-Nachricht nur weiter (`VonClient`); was wartet (Query, Antwort einer Funktion draußen),
  läuft neben der Mailbox. Die Sitzungs-Id nennt den Knoten: `<Knoten>#<Nr>` (`Verteilung.SitzungsId`).
- **Vertrag:** der Client nennt im `CapabilitiesRequest` `vertrag` (Client-Name) und `vertrag_hash`. `ClientVertragsPruefung.Pruefe` sucht ihn
  in der generierten Tabelle (unbekannt → Ablehnung) und rechnet Vertrag ∩ Token (§4.3; leer → Ablehnung). Hash ≠ Server → Warnung im Log, im
  strengen Modus (`Identitaeten:VertragStreng = true`) Ablehnung. Eine Ablehnung schickt `VERTRAG_ABGELEHNT` und beendet den Aufruf mit
  `PermissionDenied`.
- **Befugnis statt Selbstauskunft:** erlaubt sind die Mengen der wirksamen Befugnis, gehört wird genau das `IHoert` des Vertrags
  (`ClientVertragsPruefung.Wende`). Ein Client **ohne** Vertrag nennt in `message_types`, was er hören, senden und fragen will; mit Tor gilt davon
  nur, was der Akteur des Tokens darf bzw. hört (`AkteurTor.Wende`), ohne Tor alles. `CommandFailed` darf jede Sitzung hören.
- Die `CapabilitiesResponse` nennt den angenommenen Vertrag mit dem Server-Hash. Danach meldet die Sitzung ihre wirksame Hör-Liste beim
  **Verteiler** ihres Knotens an (`SitzungMeldetAn`, ersetzt die vorige; schon beim Start mit leerer Liste, damit gezielte Antworten sie vor dem
  Handshake erreichen).
- **Getragene Funktionen:** der Client bietet sie nach dem Handshake an (`FunktionenAnbieten`: Funktion, Slots). Die Sitzung holt als
  `BausteinSitzung` beim Vermittler der Funktion (`FunktionsVermittlerActor`) Eingänge mit Lease (30 s, verlängert durch `ArbeitLebt`) und reicht
  sie als `ArbeitsAuftrag` weiter. Jeder Eingang trägt die Adresse seines Funktions-Actors; alle Eingänge einer Adresse rechnet derselbe Träger,
  solange er lebt ([10](10-entwickler-api.md) A8) — ein Zustand im Worker sieht so alles, was zu ihm gehört. Endet die Sitzung, sind ihre
  Adressen sofort frei. Hinter einer Adresse rechnet die Umsetzung im Host oder die tragende Sitzung; der Absender sieht den Unterschied nicht.

### 5.3 Antworten und CommandId

**Commands vom Client.** Die Sitzung setzt `OriginSessionId` auf ihre Id, stempelt mit Tor den Akteur (`AkteurFuer`, nicht vom Client
behauptet) und prüft die Befugnis; leitet `AggregateType` aus dem Command-Typ ab, wenn er fehlt; eine negative `ExpectedVersion` heißt „ohne
OCC“ (Modus `Emittiert`, nur Inbox-Dedup), sonst gilt OCC. Die Command-Id vergibt der Client und behält sie über alle Wiederholungen
(Ausgangskorb); die Inbox des Ziels lässt jede Id genau einmal wirken. Was zurückkommt, ist kein eigenes Quittungs-Protokoll, sondern was die
Sitzung zugestellt bekommt (§5.4): die Events, die sie hört, gezielt die fachliche Ablehnung (der Hinweis aus dem Decide) oder `CommandFailed`
(Versionskonflikt, technischer Fehler, Akteur darf nicht — `AkteurVerweigerung`, Platzierung erschöpft — `AggregateDispatcher`), jeweils mit
der Korrelation des Commands. Wie der C#-Client daraus Wiederholung und Ausgangskorb macht: [08](08-frontend-blazor-client.md) §8.4.

**Antworten getragener Funktionen.** Eine Antwort auf einen Eingang reist als Ausgabe-Liste zurück (`ArbeitsErgebnis`, bei einem Strom in
Stücken mit `ab`/`fertig`; Laufort Hand: `/eingang/ergebnis` des JSON-Eingangs). Der Server hält sie unter (Funktion, Eingang) fest, bevor sie
wirkt, und leitet die Command-Ids daraus ab (`FunktionsIds`, `Infrastructure/Funktionen/Festhalten.cs`) — doppelt geantwortet, von zwei Trägern
oder nach dem Neuverbinden, wirkt sie einmal (VT `ClientVerhaltenTests.Die_Antwort_eines_Traegers_wirkt_genau_einmal…`). Die Id enthält keinen
Client. Eine zu späte Antwort meldet `ARBEIT_UNBEKANNT`.

Ist der Eingang ein **Hinweis** aus dem Hinweis-Netz (`ArbeitsAuftrag.hinweis = true`, [04](04-konsum-und-prozess-maschine.md) §4.4), wird
nichts festgehalten: die Antwort kommt als Ganzes (auch ein Strom — das SDK sammelt ihn), ihre Commands und Hinweise gehen mit der Meldung an den
Funktions-Actor, der sie sendet; die Command-Ids leitet er aus der Kennung des Hinweises ab (`FunktionsIds.BefehlAusHinweis`). Höchstens einmal:
antwortet niemand in 30 s (`FunktionsArbeit.AntwortGeduld`), verfällt der Hinweis. Den Zustand eines Workers sichert das Framework nicht (A8);
was er laden oder schließen muss, steht in den optionalen Haken `beim_start`/`beim_ende` der Python-Basis ([09](09-python-sdk.md)).

**Hinweise vom Client** (`TransientEventRequest`, nur `ITransientEvent`, mit Tor nur mit `IDarf`): gehen an die Clients, die sie hören (Verteiler),
und an die Funktionen, die sie hören (Hinweis-Netz); die Kennung vergibt der Client (`CausationId`).

**Quell-Nachrichten vom Client** (`: ClientGemeldet`) kommen heute nur über den JSON-Eingang (`POST /eingang/quelle`, Kennung = die vom Client
vergebene Id, genau einmal im Log je Kennung), nicht über gRPC (§9).

### 5.4 Zustellung an Clients

- **Verteiler je Knoten** (`Infrastructure/Verteilen/`): ein lokaler Actor mit festem Namen `verteiler` (`VerteilerActor`), Tabelle Typ →
  Sitzungen seines Knotens und Sitzungs-Id → Sitzung; er beobachtet jede Sitzung (`Watch`) und trägt sie aus, wenn sie endet. Jeder Schreiber
  (Aggregat, Quelle, festgehaltene Ausgabe einer Funktion) meldet je Commit **eine** Nachricht (`Verteile`) an den Verteiler jedes Knotens, nur
  mit Typen, die irgendein Client hören darf (`Verteilung.Hoerbar`); gezielte Envelopes (`ZielSitzung`) nur an den Knoten, den die Sitzungs-Id
  nennt. Der Verteiler fasst je Commit und Sitzung zu einer `Zustellung` zusammen. Kein Broker, keine Abos im Cluster.
- **Hören:** verlierbarer Push (Invariante 6) an jede Sitzung, deren wirksame Liste den Typ nennt — Events aus dem Log und Hinweise (aus
  Funktionen und von Clients). **Gezielt**, gleich was die Sitzung hört: die Ablehnung eines Aggregats und `CommandFailed` an die Sitzung des
  Commands (`OriginSessionId`).
- **Rückstau:** liest ein Client nicht nach, verwirft der Verteiler über 1000 offenen Zustellungen je Sitzung (`Rueckstau`).
- **Funktionen:** durabel über den Funktions-Actor — der Eingang bleibt offen, bis eine Antwort festgehalten ist; ein Client, der wegbricht,
  verliert seine Lease, der Eingang geht an den nächsten Träger.
- Wer es durabel braucht, liest ab Position oder fragt per Query (§5.5).

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
- **Keine Queries an einen Client:** ein Client fragt, er beantwortet nie — eine Query hat genau ein Ziel, einen Reader oder ein Aggregat (R4).

---

## 6. Generierung (eine Quelle: die Signaturen)

| Generat | Ort | Inhalt |
|---|---|---|
| `GeneratedAkteurRechte` | `Infrastructure.SourceGeneration/AkteurRechteGenerator.cs` | je Akteur: Art, Commands, Queries, Quellen, Hinweise, Funktionen (alles aus `IDarf`), Hören (abgeleitet, §2.1) |
| `GeneratedClientVertraege` | dito | je Client: Traegt, Sendet, Fragt, Hoert, Verkörpert, Client-Hash |
| `GeneratedWiring_<Assembly>` (C#-Client) | `Client.SourceGeneration/WiringGenerator.cs` | `Vertrag`, `VertragHash`, `ServerEventTypes` (`IHoert`), `CommandTypes` (`ISendet`), `RegisterQueries` (`IFragt`) — aus `[assembly: IstClient<IX>]` |
| `domain_client/generated/vertraege.py` | `Cqrs.Codegen/PythonVertragsEmitter.cs` (Codegen-Schritt im Build) | je Client `<Client>Basis` (`CLIENT`, `AKTEURE`, `VERTRAG_HASH`, `TRAEGT` je Funktion und Eingang, `HOERT`, `SPONTAN` = `ISendet`, `FRAGT`, abstrakte Methode je getragener Funktion und je gehörtem Typ) |

- **Kanon und Hash** an einer Stelle (`Abstractions/Clientvertragsregeln.cs`: `Kanon`, `ClientKanon`, `Hash` = 16 Hex von SHA-256): Server,
  Python und Extractor ergeben denselben Hash; ein Client gegen einen anderen Stand fällt am Handshake auf. Die Generatoren laufen nur in der
  Komposition (`Laufzeitprojekt.Ist`).
- **Python-Basis** (`Client.Infrastructure.Python/cqrs_client/vertrag.py`): bietet die getragenen Funktionen nach jedem Handshake an, reicht ihre
  Eingänge an die Methode weiter und gibt deren Ausgaben zurück (ein Strom stückweise); verdrahtet gehörter Typ → `auf_<event>`, verweigert den
  Start bei fehlender Methode (ABC), prüft `query()` gegen `FRAGT`, meldet Vertrag und Hash am Handshake. Mehr: [09](09-python-sdk.md) §9.4.

---

## 7. Regeln (Build ⇄ Grammatik)

| Build | Grammatik | Regel |
|---|---|---|
| CQRS058 | GR-AKTEUR | `IDarf<T>` nur für Hineingehendes (Command, Query, Quell-Nachricht, Hinweis, Funktions-Schnittstelle), nur an einem `IAkteur` |
| CQRS059 | GR-AKTEUR | Akteur- und Client-Namen eindeutig (Generator-Diagnose) |
| CQRS063 | GR-CLIENT | Client: Interface; erbt nur `ITraegt`/`ISendet`/`IFragt`/`IHoert`/Client-Verträge; deklariert selbst nichts |
| CQRS064 | GR-CLIENT-BEFUGT | Trägt/Sendet/Fragt: ein Akteur hat `IDarf<T>`; Hört = Event oder Hinweis |
| CQRS087 | GR-GETRAGEN | eine Funktion als Schnittstelle ohne Umsetzung im Host trägt mindestens ein Client (Warnung; sonst nur Laufort Hand) |
| CQRS093–097 | GR-CLIENT-CODE | C#-Client: Vertrag genannt (093), trägt keine Funktionen (094), erzeugte Commands in `ISendet` (095), gestellte Queries in `IFragt` (096), `Handle(E, MessageContext)` auf ein Server-Event nur mit `IHoert` (097) — `Client.SourceGeneration/ClientVertrag.cs` |
| — (Laufzeit-Tor) | GR-HERKUNFT | jeder Command und jede Query kommt von einem Akteur, direkt oder über die Kette (Warnung in der Grammatik; das Tor weist ohne `IDarf` ab) |

Die Grammatik ist eine Quelle (`DomainEditor/Grammatik.cs`, [10](10-entwickler-api.md) §10.6: R16, R17); `--check` prüft, dass jede genannte
Build-ID im Code vorkommt.

---

## 8. Editor

Die Editor-Oberfläche ist zurückgestellt ([10](10-entwickler-api.md) E4). Was die Werkzeuge heute zu Akteuren und Clients ableiten (W3: gerechnet
wird im Backend):

- **Akteur-Mengen** (`DomainEditor/AkteurAnteile.cs`): je Nachricht die Akteure **direkt** (`IDarf<T>`) und **über die Kette** (aus dem Eingang
  des Bausteins, der sie erzeugt — Decide, Frist, Funktion; die Ausgaben einer Funktion draußen stammen von dem, der sie rechnen darf), als
  Fixpunkt über Mengen. Der Extractor legt sie als `rahmen.akteurMengen` ins Board (`GraphExtractor.Kern/ModellMapper.cs`).
- **Sicht** (`GraphExtractor.Kern/Sicht/`): je Karte die Akteure und den Akteur-Rahmen (Rahmen je Domäne × Akteur, Reihenfolge: wer das Ding
  erschafft, dann Maschine → KI → Mensch, dann Name; was keinem Akteur zuzuordnen ist, steht im Rahmen „ohne Akteur“, GR-HERKUNFT), je Client die
  verkörperten Akteure (`Clientvertragsregeln.Verkoerpert`) und die **Anschlussleiste** (Ports trägt/sendet/fragt/hört mit Akteur und Ziel-Karte).
- Gesten schreiben Akteur-Record bzw. Client-Interface als Code (`DomainEditor/Scaffolder.cs`, `GraphExtractor.Kern/Schreiben/DateiAbgleich.cs`).

Zur Laufzeit trägt ein Command genau einen Akteur (§2.3, §5.1).

---

## 9. Offen

Steht in [13](13-reifegrad-schulden-bewertung.md) §13.0:

- **AK-1** — `als` im Envelope bei mehreren verkörperten Akteuren (heute der erste, der darf), Funktionen über eine C#-Leitung tragen (CQRS094),
  Anmeldung je Person am Arbeitsplatz (heute ein Token je Umgebung), kein Start-Check, ob `Clients:<Name>:Token` die Akteure des Vertrags
  verkörpert.
- **FB-6** — Quell-Nachricht vom Client nur über den JSON-Eingang, nicht über gRPC.
- **FB-14** — das Python-SDK kennt weder Lesen ab Position noch Ausgangskorb noch neu Fragen nach `Aktualisiert`.
- **FB-15** — Überlast an einer Sitzung: Verwerfen nur mit Log-Warnung; Nachrichten des Clients ohne Grenze in der Mailbox.
- **FB-19** — Antworten eines Aggregats auf eine direkte Query werden nicht beobachtet (kein `Aktualisiert`).
- **FB-7** — eine Funktion draußen ohne verbundenen Träger staut ihre Eingänge still.
- **P1-2** — kein TLS; Identitäten für Prod fehlen (in `prod`/`multinode` gibt es keinen Abschnitt `Identitaeten`, das Tor ist dort offen).
- **WZ-1** — der Fixpunkt prüft Akteure, Verträge und Clients nicht; die Sonde deckt `IMensch` und `IstClient` nicht ab.

Messungen und Testzahlen: [12](12-tests-und-vermessung.md).
