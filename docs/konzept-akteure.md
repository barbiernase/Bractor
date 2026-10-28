# Akteure, Verträge, Clients

> Detail-Referenz zu `docs/konzept-werkbank.md`; bei Widerspruch gilt der Plan (`docs/plan-pubsub-neustrukturierung.md` §2.7). Stand
> 2026-10-26 (Plan Phase 4: Akteur-Vertrag mit `Auf` und `IAkteurDienst` zurückgebaut), gegen den Code geprüft.
>
> Referenz für *wer* etwas hineingibt (Akteur) und *welche Software es über welche Leitung tut* (Client mit seinem Vertrag); Editor-Darstellung
> in §8. Abschnittsnummern sind stabil, weil Code-Kommentare darauf verweisen. Die Beispiele (Klassifizierer, Arbeitsplatz …) stammen aus der
> **Beispieldomäne** im Repo — sie ist Prüfling, nicht Architektur (W11). Offenes: `docs/13` AK-1; Zahlen: `docs/12`.

---

## 1. Begriffe

| Begriff | Frage | Code-Fakt | Beispiel |
|---|---|---|---|
| **Akteur** | *Wer* handelt fachlich, was darf er hineingeben? | `record X : IMensch\|IMaschine\|IKi, IDarf<T>…` | Inspekteur darf `LabelBildPaar` |
| **Funktion draußen** | Was rechnet eine Software außerhalb des Hosts auf ein Event hin? | eine Funktions-Schnittstelle `interface IF : IFunktion { … Auf(E e); }` | `IBildpaarBeurteilung`: `ImagePairKomplett` → `KlassifiziereBildPaarDurchKi` |
| **Client-Vertrag** | *Welche Software* hängt an der Leitung, was geht über sie? | `interface IX : IClientVertrag, ITraegt<F>, ISendet<T>, IFragt<Q>, IHoert<E>` | `IKlassifikationsWorker` (Python), `IArbeitsplatz` (Blazor) |
| **trägt** | Welche Funktion rechnet der Client? | `ITraegt<F>` (früher: Zusage) | der KlassifikationsWorker trägt `IBildpaarBeurteilung` |
| **hört** | Welches Event nimmt er nur zur Kenntnis? | `IHoert<E>` (früher: Kenntnis, `void Auf`) | der Worker lädt bei `ModellAktiviert` das neue Modell |
| **Identität** | Wer meldet sich an? | Token → Akteur-Menge (Umgebung, `Identitaeten:Tokens`) | Token `anna` → {Inspekteur, KIOperator} |

**Was weggefallen ist.** Bis Phase 3 sagte ein **Akteur-Vertrag** (`IAkteurVertrag<A>` mit `Auf`-Methoden) zu, worauf ein Akteur draußen
antwortet; der Client trug Teile davon. Diese Antwort ist heute einfach eine **Funktion**, die draußen läuft: ihre Signatur steht einmal in den
Abläufen, ein Client trägt sie (`ITraegt<F>`), und ein Akteur darf sie rechnen (`IDarf<F>`). Damit gelten für sie dieselben Garantien wie für jede
Funktion (Festhalten, Lease, genau eine Antwort — [04](04-konsum-und-prozess-maschine.md) §4.4). Ebenso entfallen ist der „Dienst eines Akteurs“
(`IAkteurDienst<A>`): ein Akteur-Wechsel mitten in einer Kette ist eine Funktion mit `IDarf<F>`.

**Leitlinie:** so wenig Begriffe wie möglich, alles Code-Fakt in Basisliste oder Signatur (keine Namenskonvention, keine Attribute zum
Raten). Was abgeleitet werden kann (wer etwas über die Kette bewirkt, was ein Akteur hört, welche Akteure ein Client verkörpert), wird
nicht deklariert.

```
            Identität (Token)            Client (Software, eine Leitung)          Akteur (fachlich)
  Token gpu-01 ──────────────►  KlassifikationsWorker ───────────────────────►  Klassifizierer
  Token train-01 ────────────►  TrainingsWorker ─────────────────────────────►  TrainingsSystem
  Token anna ────────────────►  Arbeitsplatz ──────┬─────────────────────────►  Inspekteur
     (Anna = Inspekteur)                           ├─────────────────────────►  KIOperator
                                                   ├─────────────────────────►  Modellfreigeber
                                                   └─────────────────────────►  Produktpruefer
  Wirksam ist immer:  Vertrag(Client) ∩ Akteure(Token)   — Anna am Arbeitsplatz = nur der Inspekteur-Anteil
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
- **`IDarf<T>`** = was der Akteur *selbst* hineingibt: Command, Query, Quell-Nachricht oder Hinweis — und `IDarf<F>` auf eine Funktions-Schnittstelle
  = darf diese Funktion draußen rechnen (CQRS058). Ein Typ darf bei mehreren Akteuren stehen.
- Was eine Funktion oder eine Frist daraus erzeugt, wird **nicht** deklariert — es trägt den Akteur der Kette (§2.3). Ein Command, den niemand
  per `IDarf` darf, kommt am Tor nie durch („nie von der GUI" ist damit ein Typ-Fakt).

### 2.2 Katalog der Beispieldomäne (`Domain.Akteure/Akteure.cs`)

| Akteur | Art | gibt selbst hinein (`IDarf`) |
|---|---|---|
| KameraSystem | Maschine | `DateiErkannt` (Quell-Nachricht über den Datei-Treiber) |
| Bildrechner | Maschine | `IBildaufbereitung` (darf die Bildaufbereitung draußen rechnen, Laufort `Extern`) |
| TrainingsSystem | Maschine | `ITraining`, `HoleDatensatzSamples` |
| Klassifizierer | KI | `IBildpaarBeurteilung`, `KlassifiziereEinzelBildDurchKi`, `HoleAktivesModell` |
| Inspekteur | Mensch | Labeln, `MarkiereAlsInspiziert`, Lese-Queries der Bildpaare |
| Produktpruefer | Mensch | `LabelPhysischesProdukt`, `GetImagePair`, `SucheImagePairs` |
| KIOperator | Mensch | Datensätze, Training, `RegistriereModell`, deren Queries |
| Modellfreigeber | Mensch | `SetzeModellAktiv`, `ArchiviereModell`, `HoleModelle`, `HoleAktivesModell` |
| Disponent | Mensch | `StarteSammelvorgang`, `StarteTeilauftrag`, `SchließeTeilauftragAb` |

### 2.3 Von welchem Akteur kommt ein Command?

| Fall | Akteur | Beispiel |
|---|---|---|
| 1. kommt durchs **Tor** (Client) | der Akteur der Sitzung, der `IDarf<T>` hat | `FriereEin` vom Arbeitsplatz → KIOperator |
| 2. gibt eine **Funktion** oder die **Uhr** in einer Kette aus | der Akteur des auslösenden Events, geerbt — auch, wenn die Funktion draußen rechnet | `NimmRangeAuf` trägt den, der `FuegeRangeHinzu` schickte |
| 3. kommt **ohne Kette** (Quell-Nachricht: Datei-Treiber, Takt) | **der eine** Akteur mit `IDarf<T>` (sonst GR-INGRESS-EINDEUTIG) | `DateiErkannt` → KameraSystem |

Jede Kette beginnt bei einem Akteur (Fall 1 oder 3) und trägt ihn weiter (Fall 2). Wer eine Funktion draußen rechnet, muss sie dürfen
(`IDarf<F>`); das ist eine Befugnis am Tor, kein Akteur-Wechsel. Statisch hat ein Command eine Akteur-*Menge* (Fixpunkt über den Graphen,
`DomainEditor/AkteurAnteile.cs`), zur Laufzeit genau einen (§5.1).

### 2.4 Dienst eines Akteurs

Entfallen (Phase 4). Was ein Akteur im Auftrag rechnet, ist eine Funktion mit `IDarf<F>` (§2.1).

### 2.5 Person, Rolle und fachliches „Wer"

- **Eine Person kann mehrere Akteure verkörpern:** Token → Akteur-*Menge*, in der Umgebungs-Datei unter `Identitaeten:Tokens`
  (`"sim-arbeitsplatz": "Inspekteur,KIOperator,Modellfreigeber,Produktpruefer"`; `AddCqrs` liest den Abschnitt über `AddAkteure`). Das Tor ist
  opt-in: ohne Abschnitt `Identitaeten` bleibt der gRPC-Pfad offen. Die typisierte Form `AkteurOptionen.Token<TA, TB>(…)` benutzen nur Tests.
- **Ist das „Wer" selbst fachlich** (Vier-Augen: Freigeber ≠ Antragsteller), steht es als Feld in Command und Event — normale Modellierung.

---

## 3. Funktionen draußen

Eine Funktion, die außerhalb des Hosts rechnet, ist eine Schnittstelle in den Abläufen (Rolle `Pipeline`, docs/07):

```csharp
public interface IBildpaarBeurteilung : IFunktion
{
    KlassifiziereBildPaarDurchKi Auf(ImagePairKomplett e);
}

public interface ITraining : IFunktion
{
    IAsyncEnumerable<OneOf<MeldeTrainingBegonnen, MeldeFortschritt, MeldeTrainingAbgeschlossen, MeldeTrainingGescheitert>>
        Auf(TrainingAngefordert e);                                 // ein Strom: mehrere Commands je Eingang
}
```

- Je Eingang ein `Auf`; der **Rückgabetyp ist der Ausgabe-Vertrag** (CQRS100), wie bei jeder Funktion.
- Wo sie läuft, sagt die Umgebung (`Funktionen:<F>:Laufort`); ohne Eintrag läuft eine Schnittstelle ohne Umsetzung im Host draußen, wenn ein
  Client sie trägt — sonst ist es ein Boot-Fehler (und im Build die Warnung CQRS087).
- Die Antwort des Clients prüft der Server gegen die Signatur, hält sie fest (die erste gewinnt) und sendet sie selbst; der Client ist nur die
  Hand, die Garantie bleibt beim Funktions-Actor ([04](04-konsum-und-prozess-maschine.md) §4.4, [11](11-garantien.md) §11.6).

---

## 4. Client

### 4.1 Form

```csharp
namespace Domain.Akteure;   // Domain.Akteure/Clients.cs — eigenes, leichtes Projekt: ein Client referenziert seinen Vertrag, nicht die Verarbeitung

public interface IKlassifikationsWorker : IClientVertrag,
    ITraegt<IBildpaarBeurteilung>,                   // rechnet diese Funktion
    ISendet<KlassifiziereEinzelBildDurchKi>,         // von sich aus (muss ein Akteur dürfen)
    IFragt<HoleAktivesModell>,                       // Query
    IHoert<BildVerfuegbar>, IHoert<ModellAktiviert>; // Kenntnis, verlierbar

public interface IArbeitsplatz : IClientVertrag,
    ISendet<LabelBildPaar>, ISendet<FriereEin>, ISendet<SetzeModellAktiv> /* … */,
    IFragt<SucheImagePairs>, IFragt<HoleDatensaetze> /* … */,
    IHoert<ImagePairKomplett> /* … Live-Aktualisierung */;
```

- Ein Client ist **eine Verbindung, eine Sitzung, ein Vertrag**. Er kann mehrere Akteure verkörpern, und ein Akteur kann über mehrere
  Clients laufen (n:m).
- Der Vertrag **stellt nur zusammen**: `ITraegt<F>`, `ISendet<T>`, `IFragt<Q>`, `IHoert<E>` und andere Client-Verträge; er deklariert selbst
  nichts (CQRS063).
- Handshake-Name = Interface ohne führendes I (`IKlassifikationsWorker` → `KlassifikationsWorker`); Akteur- und Client-Namen sind
  eindeutig (CQRS059).

### 4.2 Verkörpert (abgeleitet)

Je getragene Funktion, `ISendet` und `IFragt` sein **einziger** `IDarf`-Halter (Pflicht); darf ein Typ **mehrere**, kommt nur dann jemand
hinzu, wenn noch **keiner** seiner Halter verkörpert ist (sonst wäre der KlassifikationsWorker über `HoleAktivesModell` auch Modellfreigeber
und der Arbeitsplatz über `HoleDatensatzSamples` auch das TrainingsSystem). EINE Funktion für Generator, Codegen (Python-Basen), Extractor und
Editor-Sicht: `Abstractions.Clientvertragsregeln.Verkoerpert`.

### 4.3 Rechte

- **Wirksam = Vertrag ∩ Token-Akteure** (`ClientVertrag.Rechte`): je verkörpertem Akteur Commands = Sendet ∩ IDarf, Queries = Fragt ∩ IDarf,
  Funktionen = Traegt ∩ IDarf. Verkörpert das Token keinen Akteur des Clients → Ablehnung; bietet er eine Funktion an, die sein Akteur nicht
  darf, lehnt der Server das Angebot ab.
- Der Client-Vertrag **schneidet aus der Befugnis, er erweitert sie nie** (CQRS064: was er trägt, sendet oder fragt, darf ein Akteur; hören
  kann er nur Events und Hinweise).

### 4.4 Innen und Rand

Ein Client hat eine Innenseite (Blazor: Intents, ClientEvents, Stores; Python: State) und einen Rand (den Vertrag). Nur der Rand geht über
die Leitung; client-interne Typen stehen nie im Vertrag.

### 4.5 Clients der Beispieldomäne (`Domain.Akteure/Clients.cs`)

| Client | verkörpert | trägt | sendet / fragt | hört |
|---|---|---|---|---|
| `KlassifikationsWorker` (`classifier.py`, `run_stub.py`) | Klassifizierer | `IBildpaarBeurteilung` | `KlassifiziereEinzelBildDurchKi` / `HoleAktivesModell` | `BildVerfuegbar`, `ModellAktiviert` |
| `TrainingsWorker` (`training_worker.py`) | TrainingsSystem | `ITraining` | — / `HoleDatensatzSamples` | `TrainingAbgebrochen` |
| `Arbeitsplatz` (Blazor) | Inspekteur, KIOperator, Modellfreigeber, Produktpruefer | — | Commands und Queries der vier Akteure | die Events, auf die seine Handler hören |

Die Bildaufbereitung (`IBildaufbereitung`) trägt der generische Funktions-Worker (`funktions_worker.py`, Laufort `Extern`, Messung S2). Ein
Ketten-Command wie `NimmRangeAuf` (nur aus der Funktion) steht in keinem Client-Vertrag; die Mehrfach-Auswahl beim Kuratieren schickt je Paar
`NimmPaarAuf`.

### 4.6 C#-Client (Blazor): Code = Vertrag

- Die Client-Assembly nennt ihren Vertrag: `[assembly: IstClient<IArbeitsplatz>]` (`Domain.Client.Modules.Blazor/Arbeitsplatz.cs`).
- Der Client-Generator (`Client.SourceGeneration/WiringGenerator.cs`) liest ihn mit **derselben** Lesart wie der Server
  (`Infrastructure.SourceGeneration/Clientvertragslesart.cs`, per Link) und erzeugt `Vertrag` + `VertragHash` (gleicher Hash wie
  `GeneratedClientVertraege`), `ServerEventTypes` = `IHoert`, `CommandTypes` = `ISendet`, die Query-Registrierung = `IFragt`.
- Der Handshake nennt nur `vertrag` + `vertrag_hash`; nimmt der Server den Vertrag nicht an, startet der Client nicht, ein abweichender Hash
  wird gemeldet (im strengen Modus lehnt der Server ab).
- Build-Regeln (GR-CLIENT-CODE): **CQRS093** Client ohne Vertrag, **CQRS094** C#-Client trägt Funktionen (Funktionen tragen heute nur die
  Python-Basen), **CQRS095** erzeugter Command ohne `ISendet`, **CQRS096** gestellte Query ohne `IFragt`, **CQRS097** `Handle(E, MessageContext)`
  auf ein Server-Event ohne `IHoert` (`Client.SourceGeneration/ClientVertrag.cs`).
- Verbindung aus der **Umgebung**: `Host.Blazor --umgebung <name>` liest `Clients:<Vertrag>` (`Token`, `Adresse` — leer = localhost:`Transport:GrpcPort`,
  `Urls`, `Dateien` für den File-Endpunkt). In `sim-main` verkörpert das Token `sim-arbeitsplatz` die vier Akteure.

---

## 5. Laufzeit

### 5.1 Die Kette

- Der Akteur reist als Event-Header **`akteur`** (`ImAuftrag.Header`): `MartenEventBatchWriter` schreibt ihn, `MartenEventStore` liest ihn
  in den Envelope (`UserId`), `AggregateActorBase` gibt ihn weiter.
- Wer einen Command aus einem Event erzeugt, setzt den Akteur dieses Events als `ImAuftrag.Von(…)`: der Funktions-Actor je Eingang
  (`AkteurHerkunft.Aus(e.UserId)`, auch für eine Funktion draußen — der Eingang trägt ihn), die Uhr mit dem Akteur, der in der Frist steht.
  `CommandEmitter` stempelt `ImAuftrag.Akteur`; ist er leer, gilt Fall 3 (§2.3).
- Quell-Nachrichten reisen ohne Envelope: ihr Akteur ist der eindeutige `IDarf`-Halter (`AkteurHerkunft.EindeutigerHalter`, an `Quellen`
  übergeben).

### 5.2 Handshake

- Der Client nennt im `CapabilitiesRequest` **`vertrag`** (Client-Name) und **`vertrag_hash`**. Der Server nimmt die Fähigkeiten aus **seiner**
  Tabelle, nicht aus der Selbstauskunft, abonniert genau die gehörten Events und bietet die getragenen Funktionen beim Vermittler an
  (`BausteinSitzung`, Lease je Eingang). Jedes Angebot trägt die Adresse des Funktions-Actors; alle Eingänge einer Adresse rechnet dieselbe
  Sitzung, solange sie lebt (Plan §2.9, A8) — ein Zustand im Worker (ein Modell) sieht so alles, was zu ihm gehört. C# und Python teilen sich die
  Adresse: hinter ihr rechnet die Umsetzung im Host oder die Sitzung, die die Funktion trägt; der Absender sieht den Unterschied nicht.
- Mit Tor muss das Token einen Akteur des Clients verkörpern (sonst `PermissionDenied`). Hash ≠ Server → Warnung, im strengen Modus
  (`Identitaeten:VertragStreng` = `true` in der Umgebung) Ablehnung.

### 5.3 Antworten und CommandId

Eine Antwort auf einen Eingang reist als Ausgabe-Liste zurück (gRPC bzw. JSON-Eingang `/eingang/ergebnis`), mit der Kennung des Eingangs.
Der Server hält sie unter (Funktion, Eingang) fest und leitet die CommandIds daraus ab — doppelt geantwortet, von zwei Trägern oder nach dem
Neuverbinden, wirkt einmal (VT `ClientVerhaltenTests.Die_Antwort_eines_Traegers_wirkt_genau_einmal…`). Die Id enthält keinen Client.

Ist der Eingang ein **Hinweis** aus dem Hinweis-Netz (`ArbeitsAuftrag.hinweis = true`, Plan §2.9), wird nichts festgehalten: die Antwort kommt als
Ganzes (auch ein Strom — das SDK sammelt ihn), ihre Commands und Hinweise gehen mit der Meldung an den Funktions-Actor, der sie sendet; die
CommandIds leitet er aus der Kennung des Hinweises ab. Höchstens einmal: antwortet niemand in 30 s, verfällt der Hinweis. Die generierte
Python-Basis kennt den Eingang als Hinweis (`Eingang(…, hinweis=True)`); den Zustand eines Workers sichert das Framework nicht (A8). Was ein Worker
laden oder schließen muss, steht in den optionalen Haken der Basis: `beim_start` (einmal, bevor er die Funktion anbietet) und `beim_ende` (beim
Herunterfahren) — das Gegenstück zu `ILebenszyklus` im Host.

### 5.4 Zustellung an Clients

- **Funktionen:** durabel über den Funktions-Actor — der Eingang bleibt offen, bis eine Antwort festgehalten ist; ein Client, der wegbricht,
  verliert seine Lease, der Eingang geht an den nächsten Träger.
- **Hören:** verlierbarer Push (Inv. 6) an jede Sitzung, deren wirksame Liste (Vertrag ∩ Token) den Typ nennt — über den Verteiler ihres
  Knotens; Antworten auf eigene Commands (Ablehnung, `CommandFailed`) gezielt, auch ohne den Typ zu hören. Wer es durabel braucht, liest ab Position
  oder fragt per Query.

---

## 6. Generierung (eine Quelle: die Signaturen)

| Generat | Ort | Inhalt |
|---|---|---|
| `GeneratedAkteurRechte` | `Infrastructure.SourceGeneration/AkteurRechteGenerator.cs` | je Akteur: Art, Commands, Queries, Quellen, Hinweise, Funktionen (alles aus `IDarf`), Hash |
| `GeneratedClientVertraege` | dito | je Client: Traegt, Sendet, Fragt, Hoert, Verkörpert, Client-Hash |
| `GeneratedWiring_<Assembly>` (C#-Client) | `Client.SourceGeneration/WiringGenerator.cs` | `Vertrag`, `VertragHash`, `ServerEventTypes` (`IHoert`), `CommandTypes` (`ISendet`), `RegisterQueries` (`IFragt`) — aus `[assembly: IstClient<IX>]` |
| `domain_client/generated/vertraege.py` | `Cqrs.Codegen/PythonVertragsEmitter.cs` (Codegen-Schritt im Build) | je Client `<Client>Basis` (`CLIENT`, `AKTEURE`, `TRAEGT` je Funktion und Eingang, `HOERT`, `SPONTAN`, `FRAGT`, abstrakte Methode je getragener Funktion und je gehörtem Event) |

- **Kanon und Hash** an einer Stelle (`Abstractions/Clientvertragsregeln.cs`: `Kanon`, `ClientKanon`, `Hash` = 16 Hex von SHA-256): Server,
  Python und Extractor ergeben denselben Hash; ein Client gegen einen anderen Stand fällt am Handshake auf.
- **Python-Basis** (`cqrs_client/vertrag.py`): bietet die getragenen Funktionen nach jedem Handshake an, reicht ihre Eingänge an die Methode
  weiter und gibt deren Ausgaben zurück (ein Strom stückweise); verdrahtet den Dispatch gehörtes Event → `auf_<event>`, verweigert den Start bei
  fehlender Methode (ABC), prüft `query()` gegen `FRAGT`, meldet Vertrag + Hash am Handshake.

---

## 7. Regeln (Build ⇄ Editor-Grammatik)

| Build | Grammatik | Regel |
|---|---|---|
| CQRS058 | GR-AKTEUR | `IDarf<T>` nur für Hineingehendes (Command, Query, Quell-Nachricht, Hinweis, Funktions-Schnittstelle), nur an einem `IAkteur` |
| CQRS059 | — | Akteur- und Client-Namen eindeutig |
| CQRS063 | GR-CLIENT | Client: Interface; erbt nur `ITraegt`/`ISendet`/`IFragt`/`IHoert`/Client-Verträge; deklariert selbst nichts |
| CQRS064 | GR-CLIENT-BEFUGT | Trägt/Sendet/Fragt: ein Akteur hat `IDarf<T>`; Hört = Event oder Hinweis |
| CQRS087 | GR-GETRAGEN | eine Funktion als Schnittstelle ohne Umsetzung im Host trägt mindestens ein Client (Warnung; sonst nur Laufort Hand) |
| CQRS093–097 | GR-CLIENT-CODE | C#-Client: Vertrag genannt, trägt keine Funktionen, erzeugte Commands/Queries im Vertrag, gehörte Server-Events in `IHoert` (§4.6) |
| — (Laufzeit-Tor) | GR-HERKUNFT | jeder Command/Query/Quell-Nachricht kommt von einem Akteur (direkt oder Kette) |
| — | GR-INGRESS-EINDEUTIG | eine Quell-Nachricht ohne Kette hat genau einen `IDarf`-Halter |

Entfallen mit Phase 4: CQRS060 (`IAkteurDienst`), CQRS061/062 (Form und Ausgabe eines Akteur-Vertrags), CQRS065 (eine Methode je Event im
Client). Die Grammatik ist eine Quelle (`DomainEditor/Grammatik.cs`); `--check` prüft, dass jede genannte Build-ID im Code vorkommt (Lücken
dieses Gates: `docs/13` WZ-1).

---

## 8. Editor

Die Editor-Oberfläche ist im Umbau zurückgestellt (Plan E4); was hier steht, beschreibt den Stand vor Phase 4 und wird mit ihr neu gefasst.
Bedienung allgemein: konzept-werkbank §8. Jede Geste schreibt sofort Akteur-Record bzw. Client-Interface (konzept-werkbank §8.2); die Rahmen,
Akteur-Mengen und Anschlussleisten rechnet der Server (`board.sicht`, konzept-werkbank §8.3).

### 8.1 Akteur-Rahmen

**Rahmen je Domäne × Akteur**; Doppelungen stehen beim ersten Akteur in Ablauf-Reihenfolge, „↥ auch" verweist. Was keinem Akteur zugeordnet
werden kann, steht im Rahmen „ohne Akteur“ (GR-HERKUNFT).

### 8.2 Funktionen draußen

Eine Funktion als Schnittstelle ist eine Karte der Abläufe wie jede andere Funktion (◀ Eingang · Ausgänge ▶); getragen wird sie vom Client
(§8.3), gerechnet im Namen des Akteurs, der sie darf.

### 8.3 Client-Rahmen

**🔌 Client-Rahmen** rechts neben den Domänen; die Karte ist die **Anschlussleiste** (trägt / sendet als … / fragt als … / hört). Je
Client × Ziel-Rahmen **ein Bündel**, in Worten beschriftet (durchgezogen = trägt eine Funktion). Der Zoom ändert nichts;
Einzelkanten nur durch einen Klick (Leisten-Zeile, Bündel, 🔌 in der Stecker-Zeile eines Akteur-Rahmens).

### 8.4 Was abgeleitet wird: von welchem Akteur kommt was?

Statisch hat jede Nachricht eine Akteur-*Menge*, als Fixpunkt über den Graphen (`DomainEditor/AkteurAnteile.cs`): **direkt** über `IDarf<T>`,
**über die Kette** aus dem Eingang des Bausteins, der sie erzeugt (Decide, Funktion). Das Board trägt die Mengen als `rahmen.akteurMengen`; Akteure je Karte, Akteur-Rahmen und Pfade liefert
`board.sicht`. Zur Laufzeit trägt ein Command genau einen Akteur (§2.3, §5.1; `Infrastructure/Akteure/AkteurHerkunft.cs`).

### 8.5 Bearbeiten

Im Panel über ⊕: Akteur „+ darf ⊕"; Client „+ trägt ⊕", sendet/fragt, „hört ⊕". „＋“ am Port legt eine neue Karte an und verbindet sie
(konzept-werkbank A.3); eine geänderte Client-Basisliste schreibt der Abgleich einzeilig.

---

## 9. Offen

Eine Quell-Nachricht vom Client über gRPC (Plan Phase 7; heute nur über den JSON-Eingang), `als` im CommandEnvelope bei mehreren verkörperten
Akteuren (heute: der erste), Funktionen über eine C#-Leitung tragen (CQRS094), Anmeldung je Person am Arbeitsplatz (heute ein Token je Umgebung)
und Identitäten für Prod (in `prod`/`multinode` gibt es keinen Abschnitt `Identitaeten`, das Tor ist dort offen): `docs/13` AK-1 und P1-2.
Messungen und Testzahlen: `docs/12`.
