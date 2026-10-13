# Akteure, Verträge, Clients

> **Status 2026-10-13: umgesetzt** bis auf §9 (offen); seit 2026-10-13 auch der Blazor-Arbeitsplatz als Client mit Vertrag (§4.6). Kanonische Referenz für *wer* etwas hineingibt (Akteur), *was er draußen
> zusagt* (Akteur-Vertrag) und *welche Software es über welche Leitung tut* (Client). Ersetzt die früheren Fassungen dieses Dokuments
> und `konzept-client-vertraege.md` (Historie in git). Editor-Darstellung: `docs/konzept-domaenen-editor.md` §12.

---

## 1. Begriffe

| Begriff | Frage | Code-Fakt | Beispiel |
|---|---|---|---|
| **Akteur** | *Wer* handelt fachlich, was darf er spontan hineingeben? | `record X : IMensch\|IMaschine\|IKi, IDarf<T>…` | Inspekteur darf `LabelBildPaar` |
| **Zusage** | Worauf antwortet ein Akteur *draußen*, und womit? | eine Methode `Auf(TEvent e)` mit Ausgabe | Klassifizierer: `ImagePairKomplett` → `KlassifiziereBildPaarDurchKi` |
| **Kenntnis** | Welches Event nimmt er nur zur Kenntnis? | `void Auf(TEvent e)` | Klassifizierer lädt bei `ModellAktiviert` das neue Modell |
| **Akteur-Vertrag** | Alle Zusagen und Kenntnisse eines Akteurs | `interface IX : IAkteurVertrag<X>` (auch in Teilen) | `IKlassifizierer` |
| **Client** | *Welche Software* hängt an der Leitung, was geht über sie? | `interface IX : IClientVertrag, …` | `IKlassifikationsWorker` (Python), `IArbeitsplatz` (Blazor) |
| **Identität** | Wer meldet sich an? | Token → Akteur-Menge (Composition Root) | Token `anna` → {Inspekteur, KIOperator} |

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

### 2.2 Katalog (`Domain.Akteure/Akteure.cs`)

| Akteur | Art | gibt selbst hinein (`IDarf`) | Vertrag |
|---|---|---|---|
| KameraSystem | Maschine | `DateiErkannt` (Quell-Nachricht über den Datei-Treiber) | — |
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

Entscheidet ein Akteur *drinnen* über einen Dienst, gehört der Dienst ihm: `interface IGutachten : IAkteurDienst<Gutachter>`. Ein
Pipeline-Handle, der ihn als **Parameter** nimmt, entscheidet im Auftrag dieses Akteurs (der einzige Akteur-Wechsel mitten in einer Kette);
seine Ausgaben muss der Akteur dürfen, im Konstruktor/Feld ist der Dienst verboten (CQRS060). Damit steht fest, wer den Dienst benutzt.
Im Bestand ungenutzt (klassifiziert wird nur draußen), als Sprachmittel aber erhalten. Mit dem Wegfall der Handle-Pipelines ist offen,
wer `IAkteurDienst` trägt (Zielbild `docs/konzept-werkbank.md` §12.2).

### 2.5 Person, Rolle und fachliches „Wer"

- **Eine Person kann mehrere Akteure verkörpern:** Token → Akteur-*Menge* (`AkteurOptionen.Token<Inspekteur, KIOperator>(…)` bzw. Konfig
  `"Akteure:Tokens"`). Das Tor ist opt-in (ohne Sektion `Akteure` bleibt der gRPC-Pfad offen).
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
Modellfreigeber und der Arbeitsplatz über `HoleDatensatzSamples` auch das TrainingsSystem). EINE Funktion für Generator, Python-Prepass,
Wissensgraph und Editor: `Abstractions.Akteurvertrag.Verkoerpert`.

### 4.3 Rechte und Stempel

- **Wirksam = Vertrag ∩ Token-Akteure** (`ClientVertrag.Rechte`): je verkörpertem Akteur ein Teil mit Commands = (Sendet ∩ IDarf) ∪
  Ausgaben seiner getragenen Zusagen, Queries = Fragt ∩ IDarf. Verkörpert das Token keinen Akteur des Clients → Ablehnung.
- **Gestempelt** wird eine Zusage im Namen des Akteurs, dessen Teil sie vorsieht (`AkteurRechte.AkteurFuerZusage`), ein spontaner
  Command im Namen des ersten Teil-Akteurs, der ihn darf.
- Der Client-Vertrag **schneidet aus der Befugnis, er erweitert sie nie** (CQRS064).

### 4.4 Innen und Rand

Ein Client hat eine Innenseite (Blazor: Intents, ClientEvents, Stores; Python: State) und einen Rand (den Vertrag). Nur der Rand geht über
die Leitung; client-interne Typen stehen nie im Vertrag.

### 4.5 Bestand und Befund

| Client | verkörpert | trägt | sendet / fragt |
|---|---|---|---|
| `KlassifikationsWorker` (`classifier.py`, `run_stub.py`) | Klassifizierer | `IKlassifizierer` | `KlassifiziereEinzelBildDurchKi` / `HoleAktivesModell` |
| `TrainingsWorker` (`training_worker.py`) | TrainingsSystem | `ITrainingsSystem` | — / `HoleDatensatzSamples` |
| `Arbeitsplatz` (Blazor) | Inspekteur, KIOperator, Modellfreigeber, Produktpruefer | — | 14 Commands / 13 Queries, 25 Kenntnis-Events |

**Befund §4.5 (2026-10-13 aufgelöst):** `NimmRangeAuf` bleibt dem Resolver (laut Domäne „nie von der GUI“) — die Mehrfach-Auswahl beim
Kuratieren schickt je Paar `NimmPaarAuf`. `HoleDatensatzSamples` darf jetzt auch der KIOperator (er liest die Balance der eingefrorenen
Version); der Arbeitsplatz fragt es. Dazu standen 13 Events, auf die Handler des Arbeitsplatzes hören (Datensätze, Modelle, Trainingsläufe,
`TrainingFortschritt`), nicht als Kenntnis im Vertrag — der Analyzer (§4.6) fand sie, jetzt stehen sie dort.

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
- Der generierte Dispatch (Prozess/Fluss, Konsument; im Ist auch die Handle-Pipeline) setzt `ImAuftrag.Von(envelope.Akteur)` um den Handle — bzw. den Dienst-Akteur bei
  `IAkteurDienst<A>`. `CommandEmitter` stempelt `ImAuftrag.Akteur`; ist er leer, gilt Fall 3 (§2.3).
- Ein **Prozess** (auch ein Fluss) trägt den Akteur des Events, das ihn startete. (`MarkiereAlsHaengengeblieben` sendet heute der
  `TrainingWaechter` am ⏳-Port eines `Warte` statt einer Frist.) Im Ist speichert eine **Frist** der Handle-Pipelines den Akteur beim
  Planen (`Frist.Akteur`) und feuert in seinem Namen.
- Quell-Nachrichten (und im Ist die Trigger der Handle-Pipelines) reisen ohne Envelope: ihr Akteur ist der eindeutige `IDarf`-Halter
  (`AkteurHerkunft.EindeutigerHalter`, auch im `QuellEingang`).

### 5.2 Handshake

- Der Client nennt im `CapabilitiesRequest` **`vertrag`** (Client-Name; als Übergang auch ein Akteur-Name mit Vertrag) und
  **`vertrag_hash`**. Der Server nimmt die Fähigkeiten aus **seiner** Tabelle, nicht aus der Selbstauskunft, und abonniert genau die
  Eingänge (Zusagen + Kenntnis) (`AkteurVertragsPruefung`). Der C#-Client schickt gar keine Selbstauskunft mehr (§4.6); die Python-Basen
  schicken sie noch mit, der Server vergleicht dann nur.
- Mit Tor muss das Token einen Akteur des Clients verkörpern (sonst `PermissionDenied`). Hash ≠ Server → Warnung, im strengen Modus
  (`"Akteure": { "VertragStreng": true }`) Ablehnung.

### 5.3 Kausalität und deterministische CommandId

Antwortet ein Client auf ein Event, schickt die generierte Basis die Auslöse-Position mit (`causation_stream_id/version/type/index` im
`CommandEnvelopeDto`). Der Server prüft, ob die Antwort zugesagt ist (`AkteurRechte.Zugesagt`, sonst gezieltes `CommandFailed`), leitet die
**CommandId deterministisch** ab (`EmitId.Ableiten`, Diskriminator `Version:Event#Index:Command`) und stempelt `Emittiert` — die Inbox des
Ziels dedupliziert: doppelt zugestellt ≠ doppelt wirksam (`VertragsZusageE2ETests`). Die Id enthält keinen Client: zwei Clients, die
dieselbe Zusage tragen, erzeugen für dieselbe Antwort dieselbe Id.

### 5.4 Zustellung an Clients — heute und Ziel

- **Heute:** verlierbarer Push (Inv. 6) an **jede** abonnierte Sitzung (Broadcast); `SubscriptionTracker` heilt nur das Abo.
- **Ziel (offen, §9):** Kenntnis bleibt verlierbar. Eine **Zusage mit Ausgabe** ist eine durable Abhängigkeit der Kette → je getragenem
  Vertrags-Teil ein Emittenten-Cursor (dieselbe Konsum-Maschine), nachholend beim Reconnect; tragen mehrere Clients denselben Teil, je
  StreamId genau einer (Zuteilung, Übernahme bei Abbruch). Die deterministische CommandId (§5.3) deckt den Übergabemoment ab.

---

## 6. Generierung (eine Quelle: die Signaturen)

| Generat | Ort | Inhalt |
|---|---|---|
| `GeneratedAkteurRechte` | `Infrastructure.SourceGeneration/AkteurRechteGenerator.cs` | je Akteur: Art, Commands (IDarf ∪ Zusage-Ausgaben), Queries, Trigger, Transient, Hört, Vertrag (Event → Ausgaben über alle Teile), Ströme, Hash |
| `GeneratedClientVertraege` | dito | je Client: getragene Zusagen je Akteur, Sendet, Fragt, Kenntnis, Verkörpert, Client-Hash |
| `GeneratedWiring_<Assembly>` (C#-Client) | `Client.SourceGeneration/WiringGenerator.cs` | `Vertrag`, `VertragHash`, `ServerEventTypes` (Eingänge), `CommandTypes` (ISendet), `RegisterQueries` (IFragt) — aus `[assembly: IstClient<IX>]` |
| `domain_client/generated/vertraege.py` | `Cqrs.Codegen/PythonVertragsEmitter.cs` (`./codegen.sh`) | je Akteur-Vertrag `<Akteur>Basis`, je Client `<Client>Basis` (`CLIENT`, `AKTEURE`, `ZUSAGEN` mit Akteur je Eintrag, `SPONTAN`, `FRAGT`, abstrakte `auf_<event>`) |

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
| CQRS060 | GR-AUFTRAG, GR-VERKOERPERUNG | Dienst nur als Handle-Parameter; Ausgaben ⊆ Befugnis; dieselbe Entscheidung nicht drinnen *und* als Zusage |
| CQRS061 | GR-VERTRAG | Vertrag ist Interface, nur `Auf(Event)`, je Event einmal über alle Teile des Akteurs |
| CQRS062 | GR-VERTRAG | Ausgabe einer Zusage: nur konkrete Commands (void / T / OneOf / Strom) |
| CQRS063 | GR-CLIENT | Client: Interface; erbt nur Teile/ISendet/IFragt/Client-Verträge; eigene Methoden nur Kenntnis |
| CQRS064 | GR-CLIENT-BEFUGT | Sendet/Fragt: ein Akteur hat `IDarf<T>`; Sendet = Command/Trigger/Transient (Quell-Nachricht offen, Zielbild §12.2), Fragt = Query |
| CQRS065 | GR-CLIENT | je Event höchstens eine Methode im Client (getragen oder eigen) |
| CQRS093–097 | GR-CLIENT-CODE | C#-Client: Vertrag genannt, keine Teile, erzeugte Commands/Queries im Vertrag, gehörte Server-Events Kenntnis (§4.6) |
| — (Laufzeit-Tor) | GR-HERKUNFT | jeder Command/Query/Quell-Nachricht kommt von einem Akteur (direkt oder Kette) |
| — | GR-INGRESS-EINDEUTIG | eine Quell-Nachricht ohne Kette hat genau einen `IDarf`-Halter |
| — | GR-GETRAGEN | sobald es Clients gibt: jede Zusage mit Ausgabe trägt mindestens ein Client (sonst bricht die Kette) |

Die Grammatik ist eine Quelle (`DomainEditor/Grammatik.cs`); ein Build-/Test-Gate prüft, dass jede genannte Build-ID im Code existiert.

---

## 8. Editor (Kurzfassung — Volltext `docs/konzept-domaenen-editor.md` §12)

- **Rahmen je Domäne × Akteur**; Doppelungen stehen beim ersten Akteur in Ablauf-Reihenfolge, „↥ auch" verweist.
- **📜 Vertrags-Rahmen** neben der Akteur-Karte, darin je `Auf` eine Karte **Zusage** (◀ Event · Ausgänge ▶).
- **🔌 Client-Rahmen** rechts neben den Domänen; die Karte ist die **Anschlussleiste** (trägt / sendet als … / fragt als … / hört). Je
  Client × Ziel-Rahmen **ein Bündel**, in Worten beschriftet (durchgezogen = trägt eine Zusage mit Ausgabe). Der Zoom ändert nichts;
  Einzelkanten nur durch einen Klick (Leisten-Zeile, Bündel, 🔌 in der Stecker-Zeile eines Akteur-Rahmens).
- Bearbeiten im Panel über ⊕: Akteur „darf ⊕", „+ Zusage ⊕ (Event)"; Client „trägt ⊕", „sendet/fragt ⊕", „hört ⊕". Jede Geste
  schreibt sofort Akteur-Record, Vertrags-Interface bzw. Client-Interface (Zielbild §8.2).

---

## 9. Stand und offen

**Gemessen 2026-10-06:** Prüfstand 280/280; Python SDK 19/19, Worker 11/11; Integration `VertragsZusageE2ETests` + `MetadataPostgresTests`
grün gegen echte Infra; `--check` (8 Akteure, 2 Verträge, 3 Clients / 43 Rand-Ports) und `--sonde` (Lesesaal = 2 Akteure, Archiv über 2
Clients, gezeichnetes Mahnportal) grün. Live: `run_stub.py` meldet sich als `KlassifikationsWorker`, der Server nimmt den Vertrag an und
abonniert genau die 3 Vertrags-Events.

**Gemessen 2026-10-13 (Blazor als Client, §4.6):** Prüfstand 526/526 (+5 `ClientCodeVertragTests`: Code im Vertrag ohne Befund,
CQRS093–097, gleicher Hash wie der Server); Integration 39/39; Python Worker 20/20, SDK 28/28; `--check`/`--sonde` grün. Live: `Host.Blazor
--umgebung sim-main` meldet sich als `Arbeitsplatz` an (Token `sim-arbeitsplatz`), der Host nimmt den Vertrag an („verkörpert Inspekteur,
KIOperator, Modellfreigeber, Produktpruefer“; 14 Commands, 25 Events, 13 Queries); der Arbeitsplatz hydriert, „Inspiziert“ landet als
Inspekteur, „R“ (OK) als `LabelPhysischesProdukt` des Produktprüfers in der Spur.

**Offen:**
1. **Durable Zustellung + Zuteilung** an Clients (§5.4).
2. **`als` im CommandEnvelope** für spontane Commands, die mehrere Akteure eines Clients dürfen (heute: der erste).
3. **Zusagen über eine C#-Leitung** (CQRS094 verbietet sie heute; die Python-Basen beantworten sie mit Kausalität).
4. **Anmeldung je Person** am Arbeitsplatz: heute ein Token je Umgebung (`Clients:Arbeitsplatz:Token`), das die vier Akteure verkörpert.
5. In `prod`/`multinode` gibt es noch keine Identitäten — das Tor ist dort offen (der Vertrag gilt trotzdem).
6. Der Abgleich schreibt eine geänderte Client-Basisliste einzeilig; weitere Vertrags-Teile eines Akteurs liest der Editor, schreibt sie
   aber nicht.
