# Akteure, Verträge, Clients

> Detail-Referenz zu `docs/konzept-werkbank.md`; bei Widerspruch gilt das Zielbild. Stand 2026-10-13, gegen den Code geprüft.
>
> Referenz für *wer* etwas hineingibt (Akteur), *was er draußen zusagt* (Akteur-Vertrag) und *welche Software es über welche Leitung tut*
> (Client); Editor-Darstellung in §8. Abschnittsnummern sind stabil, weil Code-Kommentare darauf verweisen. Die Beispiele (Klassifizierer,
> Arbeitsplatz …) stammen aus der **Beispieldomäne** im Repo — sie ist Prüfling, nicht Architektur (konzept-werkbank W11). Offenes:
> `docs/13` AK-1; Zahlen: `docs/12`.

---

## 1. Begriffe

| Begriff | Frage | Code-Fakt | Beispiel |
|---|---|---|---|
| **Akteur** | *Wer* handelt fachlich, was darf er spontan hineingeben? | `record X : IMensch\|IMaschine\|IKi, IDarf<T>…` | Inspekteur darf `LabelBildPaar` |
| **Zusage** | Worauf antwortet ein Akteur *draußen*, und womit? | eine Methode `Auf(TEvent e)` mit Ausgabe | Klassifizierer: `ImagePairKomplett` → `KlassifiziereBildPaarDurchKi` |
| **Kenntnis** | Welches Event nimmt er nur zur Kenntnis? | `void Auf(TEvent e)` | Klassifizierer lädt bei `ModellAktiviert` das neue Modell |
| **Akteur-Vertrag** | Alle Zusagen und Kenntnisse eines Akteurs | `interface IX : IAkteurVertrag<X>` (auch in Teilen) | `IKlassifizierer` |
| **Client** | *Welche Software* hängt an der Leitung, was geht über sie? | `interface IX : IClientVertrag, …` | `IKlassifikationsWorker` (Python), `IArbeitsplatz` (Blazor) |
| **Identität** | Wer meldet sich an? | Token → Akteur-Menge (Umgebung, `Identitaeten:Tokens`) | Token `anna` → {Inspekteur, KIOperator} |

**Abgrenzung:** *Reaktion* ist im Projekt der **Backend-Baustein** (`ISubscriber`, ein interner Konsument, der auf Events hin Commands
ausgibt). Eine **Zusage** ist dasselbe Muster *draußen*, im Namen eines Akteurs, implementiert von einem Client. Die beiden Wörter werden
nicht vermischt.

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
public sealed record Klassifizierer : IKi,       IDarf<KlassifiziereEinzelBildDurchKi>, IDarf<HoleAktivesModell>;
public sealed record Inspekteur     : IMensch,   IDarf<LabelBildPaar>, IDarf<MarkiereAlsInspiziert>, IDarf<SucheImagePairs> /* … */;
```

- **Art** in der Basisliste: `IMensch`, `IMaschine`, `IKi` (alle `: IAkteur`). Ein Akteur ist immer ein Record, nie ein Dienst.
- **`IDarf<T>`** = was der Akteur *selbst* hineingibt: Command, Query, Quell-Nachricht oder Transient-Event (dazu `IDarf<IFunktion>` =
  darf diese Funktion rechnen) (CQRS058). Ein Command darf bei mehreren Akteuren stehen.
- Was ein Fluss oder Prozess daraus erzeugt, wird **nicht** deklariert — es trägt den Akteur der Kette (§2.3). Ein
  Command, den niemand per `IDarf` darf, kommt am Tor nie durch („nie von der GUI" ist damit ein Typ-Fakt).

### 2.2 Katalog der Beispieldomäne (`Domain.Akteure/Akteure.cs`)

| Akteur | Art | gibt selbst hinein (`IDarf`) | Vertrag |
|---|---|---|---|
| KameraSystem | Maschine | `DateiErkannt` (Quell-Nachricht über den Datei-Treiber) | — |
| Bildrechner | Maschine | `IBildVerkleinerung`, `IHistogrammAusgleich` (darf diese Funktionen rechnen; Python-Worker, Laufort `Extern`) | — |
| TrainingsSystem | Maschine | `HoleDatensatzSamples` | `ITrainingsSystem` |
| Klassifizierer | KI | `KlassifiziereEinzelBildDurchKi`, `HoleAktivesModell` | `IKlassifizierer` |
| Inspekteur | Mensch | Labeln, `MarkiereAlsInspiziert`, Lese-Queries der Bildpaare | — |
| Produktpruefer | Mensch | `LabelPhysischesProdukt`, `GetImagePair`, `SucheImagePairs` | — |
| KIOperator | Mensch | Datensätze, Training, `RegistriereModell`, deren Queries | — |
| Modellfreigeber | Mensch | `SetzeModellAktiv`, `ArchiviereModell`, `HoleModelle`, `HoleAktivesModell` | — |
| Disponent | Mensch | `StarteSammelvorgang`, `StarteTeilauftrag`, `SchließeTeilauftragAb` | — |

### 2.3 Von welchem Akteur kommt ein Command?

| Fall | Akteur | Beispiel |
|---|---|---|
| 1. kommt durchs **Tor** (Client) | der Akteur der Sitzung, der `IDarf<T>` hat (bzw. dessen Zusage es ist, §4.3) | `FriereEin` vom Arbeitsplatz → KIOperator |
| 2. gibt ein **Arm in einer Kette** aus (Fluss oder Prozess auf ein Event) | der Akteur des auslösenden Events, geerbt | `SchliesseEinfrierenAb` trägt den, der `FriereEin` schickte |
| 3. gibt ein **Arm ohne Kette** aus (Quell-Nachricht: Datei-Treiber, Webhook) | **der eine** Akteur mit `IDarf<T>` (sonst GR-INGRESS-EINDEUTIG) | `DateiErkannt` → KameraSystem |

Jede Kette beginnt bei einem Akteur (Fall 1 oder 3) und trägt ihn weiter (Fall 2). Statisch hat ein Command eine Akteur-*Menge*
(Fixpunkt über den Graphen, `DomainEditor/AkteurAnteile.cs`), zur Laufzeit genau einen (§5.1).

### 2.4 Dienst eines Akteurs

**Altlast.** `interface IGutachten : IAkteurDienst<Gutachter>` macht einen Dienst zum Dienst eines Akteurs: ein Handle der Handle-Pipeline,
der ihn als **Parameter** nimmt, entscheidet im Auftrag dieses Akteurs (Akteur-Wechsel mitten in einer Kette); seine Ausgaben muss der Akteur
dürfen, im Konstruktor/Feld ist der Dienst verboten (CQRS060). `IAkteurDienst<A>` ist nur an der Handle-Pipeline verdrahtet und hat keinen
Domänen-Nutzer; Rückbau offen (`docs/13` W11-1). Sein Zweck ist vermutlich durch eine Katalog-Funktion mit `IDarf<F>` gedeckt
(konzept-werkbank §12.2).

### 2.5 Person, Rolle und fachliches „Wer"

- **Eine Person kann mehrere Akteure verkörpern:** Token → Akteur-*Menge*, in der Umgebungs-Datei unter `Identitaeten:Tokens`
  (`"sim-arbeitsplatz": "Inspekteur,KIOperator,Modellfreigeber,Produktpruefer"`; `AddCqrs` liest den Abschnitt über `AddAkteure`). Das Tor ist
  opt-in: ohne Abschnitt `Identitaeten` bleibt der gRPC-Pfad offen. Die typisierte Form `AkteurOptionen.Token<TA, TB>(…)` benutzen nur Tests.
- **Ist das „Wer" selbst fachlich** (Vier-Augen: Freigeber ≠ Antragsteller), steht es als Feld in Command und Event — normale Modellierung.

---

## 3. Akteur-Vertrag: Zusagen und Kenntnis

### 3.1 Form

```csharp
public interface IKlassifizierer : IAkteurVertrag<Klassifizierer>
{
    OneOf<KlassifiziereBildPaarDurchKi> Auf(ImagePairKomplett e);   // Zusage: auf dieses Event antwortet er mit diesem Command
    void Auf(BildVerfuegbar e);                                     // Kenntnis
    void Auf(ModellAktiviert e);                                    // Kenntnis
}

public interface ITrainingsSystem : IAkteurVertrag<TrainingsSystem>
{
    IAsyncEnumerable<OneOf<MeldeTrainingBegonnen, MeldeFortschritt, MeldeTrainingAbgeschlossen, MeldeTrainingGescheitert>>
        Auf(TrainingAngefordert e);                                 // Zusage als Strom: mehrere Commands je Event
    void Auf(TrainingAbgebrochen e);
}
```

- Je `Auf(TEvent)` eine Zusage; der **Rückgabetyp ist der Ausgabe-Vertrag**: `void`, ein konkreter Command, `OneOf<…>` oder ein Strom
  `IAsyncEnumerable<OneOf<…>>` (CQRS062, wie CQRS050 bei Handles).
- **Befugt = IDarf ∪ Ausgaben der Zusagen** (was er zusagt, darf er, ohne zweites `IDarf`); **Hört = genau die Eingänge**.
- Implementiert wird der Vertrag nur draußen — von einem Client, der ihn trägt (§4).

### 3.2 Teile

Ein Akteur darf seinen Vertrag in **Teile** schneiden (mehrere Interfaces mit `IAkteurVertrag<A>`, auch voneinander geerbt). Über alle Teile
gilt: je Event höchstens ein `Auf` (CQRS061). Der ganze Vertrag ist die Vereinigung der Teile; ein Client kann einen einzelnen Teil tragen.

```csharp
public interface IPaarKlassifikation : IAkteurVertrag<Klassifizierer> { OneOf<KlassifiziereBildPaarDurchKi> Auf(ImagePairKomplett e); }
public interface IKlassifizierer : IPaarKlassifikation { void Auf(BildVerfuegbar e); void Auf(ModellAktiviert e); }
```

---

## 4. Client

### 4.1 Form

```csharp
namespace Domain.Clients;   // Domain.Akteure/Clients.cs — eigenes, leichtes Projekt: ein Client referenziert seinen Vertrag, nicht die Verarbeitung

public interface IKlassifikationsWorker : IClientVertrag,
    IKlassifizierer,                                 // trägt den Akteur-Vertrag (Zusagen im Namen des Klassifizierers)
    ISendet<KlassifiziereEinzelBildDurchKi>,         // spontan (muss ein Akteur dürfen)
    IFragt<HoleAktivesModell>                        // Query
{ }

public interface IArbeitsplatz : IClientVertrag,
    ISendet<LabelBildPaar>, ISendet<FriereEin>, ISendet<SetzeModellAktiv> /* … */,
    IFragt<SucheImagePairs>, IFragt<HoleDatensaetze> /* … */
{
    void Auf(ImagePairKomplett e);                   // eigene Kenntnis (Live-Aktualisierung)
    /* … */
}
```

- Ein Client ist **eine Verbindung, eine Sitzung, ein Vertrag**. Er kann mehrere Akteure verkörpern, und ein Akteur kann über mehrere
  Clients laufen (n:m).
- Er **erbt Akteur-Vertrags-Teile** (deren Zusagen gehen im Namen ihres Akteurs über seine Leitung), nennt `ISendet<T>`/`IFragt<T>` und
  deklariert selbst **nur Kenntnis** (`void Auf(E)`) — eine Ausgabe gehört in einen Akteur-Vertrag, sonst hätte sie keinen Akteur (CQRS063).
- Handshake-Name = Interface ohne führendes I (`IKlassifikationsWorker` → `KlassifikationsWorker`); Akteur- und Client-Namen sind
  eindeutig (CQRS059).

### 4.2 Verkörpert (abgeleitet)

Die Akteure der getragenen Teile; dazu je `ISendet`/`IFragt` sein **einziger** `IDarf`-Halter (Pflicht); darf ein Typ **mehrere**, kommt nur
dann jemand hinzu, wenn noch **keiner** seiner Halter verkörpert ist (sonst wäre der KlassifikationsWorker über `HoleAktivesModell` auch
Modellfreigeber und der Arbeitsplatz über `HoleDatensatzSamples` auch das TrainingsSystem). EINE Funktion für Generator, Codegen (Python-Basen),
Wissensgraph und Editor-Sicht: `Abstractions.Akteurvertrag.Verkoerpert`.

### 4.3 Rechte und Stempel

- **Wirksam = Vertrag ∩ Token-Akteure** (`ClientVertrag.Rechte`): je verkörpertem Akteur ein Teil mit Commands = (Sendet ∩ IDarf) ∪
  Ausgaben seiner getragenen Zusagen, Queries = Fragt ∩ IDarf. Verkörpert das Token keinen Akteur des Clients → Ablehnung.
- **Gestempelt** wird eine Zusage im Namen des Akteurs, dessen Teil sie vorsieht (`AkteurRechte.AkteurFuerZusage`), ein spontaner
  Command im Namen des ersten Teil-Akteurs, der ihn darf.
- Der Client-Vertrag **schneidet aus der Befugnis, er erweitert sie nie** (CQRS064).

### 4.4 Innen und Rand

Ein Client hat eine Innenseite (Blazor: Intents, ClientEvents, Stores; Python: State) und einen Rand (den Vertrag). Nur der Rand geht über
die Leitung; client-interne Typen stehen nie im Vertrag.

### 4.5 Clients der Beispieldomäne (`Domain.Akteure/Clients.cs`)

| Client | verkörpert | trägt | sendet / fragt |
|---|---|---|---|
| `KlassifikationsWorker` (`classifier.py`, `run_stub.py`) | Klassifizierer | `IKlassifizierer` | `KlassifiziereEinzelBildDurchKi` / `HoleAktivesModell` |
| `TrainingsWorker` (`training_worker.py`) | TrainingsSystem | `ITrainingsSystem` | — / `HoleDatensatzSamples` |
| `Arbeitsplatz` (Blazor) | Inspekteur, KIOperator, Modellfreigeber, Produktpruefer | — | Commands und Queries der vier Akteure; Kenntnis der Events, auf die seine Handler hören |

Ein Ketten-Command wie `NimmRangeAuf` (nur aus dem Fluss) steht in keinem Client-Vertrag; die Mehrfach-Auswahl beim Kuratieren schickt je Paar
`NimmPaarAuf`.

### 4.6 C#-Client (Blazor): Code = Vertrag

- Die Client-Assembly nennt ihren Vertrag: `[assembly: IstClient<IArbeitsplatz>]` (`Domain.Client.Modules.Blazor/Arbeitsplatz.cs`).
- Der Client-Generator (`Client.SourceGeneration/WiringGenerator.cs`) liest ihn mit **derselben** Lesart wie der Server
  (`Infrastructure.SourceGeneration/Clientvertragslesart.cs`, per Link) und erzeugt `Vertrag` + `VertragHash` (gleicher Hash wie
  `GeneratedClientVertraege`), `ServerEventTypes` = Kenntnis + Zusage-Eingänge, `CommandTypes` = `ISendet`, die Query-Registrierung = `IFragt`.
  Keine Selbstauskunft mehr („alle Events, alle Commands“).
- Der Handshake nennt nur `vertrag` + `vertrag_hash`; nimmt der Server den Vertrag nicht an, startet der Client nicht, ein abweichender Hash
  wird gemeldet (im strengen Modus lehnt der Server ab).
- Build-Regeln (GR-CLIENT-CODE): **CQRS093** Client ohne Vertrag, **CQRS094** C#-Client trägt Vertrags-Teile (Zusagen beantworten heute nur
  die Python-Basen), **CQRS095** erzeugter Command ohne `ISendet`, **CQRS096** gestellte Query ohne `IFragt`, **CQRS097** `Handle(E, MessageContext)`
  auf ein Server-Event ohne Kenntnis (`Client.SourceGeneration/ClientVertrag.cs`).
- Verbindung aus der **Umgebung**: `Host.Blazor --umgebung <name>` liest `Clients:<Vertrag>` (`Token`, `Adresse` — leer = localhost:`Transport:GrpcPort`,
  `Urls`, `Dateien` für den File-Endpunkt). In `sim-main` verkörpert das Token `sim-arbeitsplatz` die vier Akteure.

---

## 5. Laufzeit

### 5.1 Die Kette

- Der Akteur reist als Event-Header **`akteur`** (`ImAuftrag.Header`): `MartenEventBatchWriter` schreibt ihn, `MartenEventStore` liest ihn
  in den Envelope (`UserId`), `AggregateActorBase` gibt ihn weiter.
- Wer einen Command aus einem Event erzeugt, setzt den Akteur dieses Events als `ImAuftrag.Von(…)`: der Prozess-Manager (Prozess und
  Fluss) und der `HandlerOutputRouter` (Commands einer Reaktion, `AkteurHerkunft.Aus(trigger.UserId)`). `CommandEmitter` stempelt
  `ImAuftrag.Akteur`; ist er leer, gilt Fall 3 (§2.3).
- Ein **Prozess** (auch ein Fluss) trägt den Akteur des Events, das ihn startete — auch über ein Zeitlimit (Beispieldomäne:
  `MarkiereAlsHaengengeblieben` sendet der `TrainingWaechter` am ⏳-Port eines `Warte`).
- Quell-Nachrichten reisen ohne Envelope: ihr Akteur ist der eindeutige `IDarf`-Halter (`AkteurHerkunft.EindeutigerHalter`, an `Quellen`
  übergeben).
- **Altlast** (Handle-Welt, ohne Domänen-Nutzer, Rückbau offen `docs/13` W11-1): die Handle-Pipeline setzt bei einem
  `IAkteurDienst<A>`-Parameter den Dienst-Akteur, ihre Trigger nehmen den eindeutigen Halter, eine **Frist** speichert den Akteur beim Planen
  (`Frist.Akteur`) und feuert in seinem Namen.

### 5.2 Handshake

- Der Client nennt im `CapabilitiesRequest` **`vertrag`** (Client-Name; als Übergang auch ein Akteur-Name mit Vertrag) und
  **`vertrag_hash`**. Der Server nimmt die Fähigkeiten aus **seiner** Tabelle, nicht aus der Selbstauskunft, und abonniert genau die
  Eingänge (Zusagen + Kenntnis) (`AkteurVertragsPruefung`). Der C#-Client schickt gar keine Selbstauskunft mehr (§4.6); die Python-Basen
  schicken sie noch mit, der Server vergleicht dann nur.
- Mit Tor muss das Token einen Akteur des Clients verkörpern (sonst `PermissionDenied`). Hash ≠ Server → Warnung, im strengen Modus
  (`Identitaeten:VertragStreng` = `true` in der Umgebung) Ablehnung.

### 5.3 Kausalität und deterministische CommandId

Antwortet ein Client auf ein Event, schickt die generierte Basis die Auslöse-Position mit (`causation_stream_id/version/type/index` im
`CommandEnvelopeDto`). Der Server prüft, ob die Antwort zugesagt ist (`AkteurRechte.Zugesagt`, sonst gezieltes `CommandFailed`), leitet die
**CommandId deterministisch** ab (`EmitId.Ableiten`, Diskriminator `Version:Event#Index:Command`) und stempelt `Emittiert` — die Inbox des
Ziels dedupliziert: doppelt zugestellt ≠ doppelt wirksam (`VertragsZusageE2ETests`). Die Id enthält keinen Client: zwei Clients, die
dieselbe Zusage tragen, erzeugen für dieselbe Antwort dieselbe Id.

### 5.4 Zustellung an Clients — heute und Ziel

- **Heute:** verlierbarer Push (Inv. 6) an **jede** abonnierte Sitzung (Broadcast); `SubscriptionTracker` heilt nur das Abo.
- **Ziel (offen, `docs/13` AK-1):** Kenntnis bleibt verlierbar. Eine **Zusage mit Ausgabe** ist eine durable Abhängigkeit der Kette → je getragenem
  Vertrags-Teil ein Emittenten-Cursor (dieselbe Konsum-Maschine), nachholend beim Reconnect; tragen mehrere Clients denselben Teil, je
  StreamId genau einer (Zuteilung, Übernahme bei Abbruch). Die deterministische CommandId (§5.3) deckt den Übergabemoment ab.

---

## 6. Generierung (eine Quelle: die Signaturen)

| Generat | Ort | Inhalt |
|---|---|---|
| `GeneratedAkteurRechte` | `Infrastructure.SourceGeneration/AkteurRechteGenerator.cs` | je Akteur: Art, Commands (IDarf ∪ Zusage-Ausgaben), Queries, Trigger (Altlast), Transient, Hört, Vertrag (Event → Ausgaben über alle Teile), Ströme, Hash |
| `GeneratedClientVertraege` | dito | je Client: getragene Zusagen je Akteur, Sendet, Fragt, Kenntnis, Verkörpert, Client-Hash |
| `GeneratedWiring_<Assembly>` (C#-Client) | `Client.SourceGeneration/WiringGenerator.cs` | `Vertrag`, `VertragHash`, `ServerEventTypes` (Eingänge), `CommandTypes` (ISendet), `RegisterQueries` (IFragt) — aus `[assembly: IstClient<IX>]` |
| `domain_client/generated/vertraege.py` | `Cqrs.Codegen/PythonVertragsEmitter.cs` (Codegen-Schritt im Build) | je Akteur-Vertrag `<Akteur>Basis`, je Client `<Client>Basis` (`CLIENT`, `AKTEURE`, `ZUSAGEN` mit Akteur je Eintrag, `SPONTAN`, `FRAGT`, abstrakte `auf_<event>`) |

- **Kanon und Hash** an einer Stelle (`Abstractions/Akteurvertrag.cs`: `Kanon`, `ClientKanon`, `Hash` = 16 Hex von SHA-256): Server,
  Python und Wissensgraph ergeben denselben Hash; ein Client gegen einen anderen Stand fällt am Handshake auf.
- **Python-Basis** (`cqrs_client/vertrag.py`): verdrahtet den Dispatch Event → `auf_<event>`, verweigert den Start bei fehlender Methode
  (ABC), prüft jedes Yield gegen die Zusage (`VertragsVerletzung`), prüft `query()` gegen `FRAGT`, meldet Vertrag + Hash am Handshake.

---

## 7. Regeln (Build ⇄ Editor-Grammatik)

| Build | Grammatik | Regel |
|---|---|---|
| CQRS058 | GR-AKTEUR | `IDarf<T>` nur für Hineingehendes, nur an einem `IAkteur` |
| CQRS059 | — | Akteur- und Client-Namen eindeutig |
| CQRS060 | GR-AUFTRAG, GR-VERKOERPERUNG | `IAkteurDienst` (Altlast, §2.4) nur als Handle-Parameter; Ausgaben ⊆ Befugnis; dieselbe Entscheidung nicht drinnen *und* als Zusage |
| CQRS061 | GR-VERTRAG | Vertrag ist Interface, nur `Auf(Event)`, je Event einmal über alle Teile des Akteurs |
| CQRS062 | GR-VERTRAG | Ausgabe einer Zusage: nur konkrete Commands (void / T / OneOf / Strom) |
| CQRS063 | GR-CLIENT | Client: Interface; erbt nur Teile/ISendet/IFragt/Client-Verträge; eigene Methoden nur Kenntnis |
| CQRS064 | GR-CLIENT-BEFUGT | Sendet/Fragt: ein Akteur hat `IDarf<T>`; Sendet = Command, Transient-Event oder Trigger (Altlast, `docs/13` W11-1); „Client meldet Quell-Nachricht“ ist noch kein Laufzeitweg (konzept-werkbank §12.2); Fragt = Query |
| CQRS065 | GR-CLIENT | je Event höchstens eine Methode im Client (getragen oder eigen) |
| CQRS093–097 | GR-CLIENT-CODE | C#-Client: Vertrag genannt, keine Teile, erzeugte Commands/Queries im Vertrag, gehörte Server-Events Kenntnis (§4.6) |
| — (Laufzeit-Tor) | GR-HERKUNFT | jeder Command/Query/Quell-Nachricht kommt von einem Akteur (direkt oder Kette) |
| — | GR-INGRESS-EINDEUTIG | eine Quell-Nachricht ohne Kette hat genau einen `IDarf`-Halter |
| — | GR-GETRAGEN | sobald es Clients gibt: jede Zusage mit Ausgabe trägt mindestens ein Client (sonst bricht die Kette) |

Die Grammatik ist eine Quelle (`DomainEditor/Grammatik.cs`, konzept-werkbank A.4); `--check` prüft, dass jede genannte Build-ID im Code
vorkommt (Lücken dieses Gates: `docs/13` WZ-1).

---

## 8. Editor

Bedienung allgemein: konzept-werkbank §8. Jede Geste schreibt sofort Akteur-Record, Vertrags-Interface bzw. Client-Interface (konzept-werkbank
§8.2); die Rahmen, Akteur-Mengen und Anschlussleisten rechnet der Server (`board.sicht`, konzept-werkbank §8.3).

### 8.1 Akteur-Rahmen

**Rahmen je Domäne × Akteur**; Doppelungen stehen beim ersten Akteur in Ablauf-Reihenfolge, „↥ auch" verweist. Was keinem Akteur zugeordnet
werden kann, steht im Rahmen „ohne Akteur“ (GR-HERKUNFT).

### 8.2 Vertrags-Rahmen

**📜 Vertrags-Rahmen** neben der Akteur-Karte, darin je `Auf` eine Karte **Zusage** (◀ Event · Ausgänge ▶) mit dem Python-Rumpf der
Client-Implementierung als Rumpf (konzept-werkbank §8.4).

### 8.3 Client-Rahmen

**🔌 Client-Rahmen** rechts neben den Domänen; die Karte ist die **Anschlussleiste** (trägt / sendet als … / fragt als … / hört). Je
Client × Ziel-Rahmen **ein Bündel**, in Worten beschriftet (durchgezogen = trägt eine Zusage mit Ausgabe). Der Zoom ändert nichts;
Einzelkanten nur durch einen Klick (Leisten-Zeile, Bündel, 🔌 in der Stecker-Zeile eines Akteur-Rahmens).

### 8.4 Was abgeleitet wird: von welchem Akteur kommt was?

Statisch hat jede Nachricht eine Akteur-*Menge*, als Fixpunkt über den Graphen (`DomainEditor/AkteurAnteile.cs`): **direkt** über `IDarf<T>`,
**über die Kette** aus dem Eingang des Bausteins, der sie erzeugt (Decide, Handle, Prozess-Regel, Fluss-Knoten), und aus der Ausgabe einer
Zusage im Vertrag eines Akteurs. Das Board trägt die Mengen als `rahmen.akteurMengen`; Akteure je Karte, Akteur-Rahmen und Pfade liefert
`board.sicht`. Zur Laufzeit trägt ein Command genau einen Akteur (§2.3, §5.1; `Infrastructure/Akteure/AkteurHerkunft.cs`).

### 8.5 Bearbeiten

Im Panel über ⊕: Akteur „+ darf ⊕", „+ Zusage ⊕ (Event)"; Client „+ trägt ⊕", sendet/fragt, „hört ⊕". „＋“ am Port legt eine neue Karte an
und verbindet sie (konzept-werkbank A.3). Weitere Vertrags-Teile eines Akteurs (§3.2) liest der Editor, er schreibt nur den Haupt-Vertrag
(`DomainEditor/Scaffolder.cs`); eine geänderte Client-Basisliste schreibt der Abgleich einzeilig.

---

## 9. Offen

Durable Zustellung und Zuteilung an Clients (§5.4), `als` im CommandEnvelope bei mehreren verkörperten Akteuren (heute: der erste), Zusagen
über eine C#-Leitung (CQRS094), Anmeldung je Person am Arbeitsplatz (heute ein Token je Umgebung) und Identitäten für Prod (in `prod`/`multinode`
gibt es keinen Abschnitt `Identitaeten`, das Tor ist dort offen): `docs/13` AK-1 und P1-2. Messungen und Testzahlen: `docs/12`.
