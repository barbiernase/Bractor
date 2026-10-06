# Akteure, Verträge, Clients

> **Status 2026-10-06: umgesetzt** bis auf §9 (offen). Kanonische Referenz für *wer* etwas hineingibt (Akteur), *was er draußen
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
- **`IDarf<T>`** = was der Akteur *selbst* hineingibt: Command, Query, Trigger oder Transient-Event (CQRS058). Ein Command darf bei
  mehreren Akteuren stehen.
- Was eine Pipeline, ein Prozess oder eine Frist daraus erzeugt, wird **nicht** deklariert — es trägt den Akteur der Kette (§2.3). Ein
  Command, den niemand per `IDarf` darf, kommt am Tor nie durch („nie von der GUI" ist damit ein Typ-Fakt).

### 2.2 Katalog (`Domain.Pipeline/Akteure.cs`)

| Akteur | Art | gibt selbst hinein (`IDarf`) | Vertrag |
|---|---|---|---|
| KameraSystem | Maschine | `DateiErkannt` (Ingress `FileWatchPipeline`) | — |
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
| 2. gibt ein **Arm in einer Kette** aus (Handle auf ein Event, Prozess, Frist) | der Akteur des auslösenden Events, geerbt | `SchliesseEinfrierenAb` trägt den, der `FriereEin` schickte |
| 3. gibt ein **Arm ohne Kette** aus (Ingress: Datei, Timer, `Selbst<T>`) | **der eine** Akteur mit `IDarf<T>` (sonst GR-INGRESS-EINDEUTIG) | `DateiErkannt` → KameraSystem |

Jede Kette beginnt bei einem Akteur (Fall 1 oder 3) und trägt ihn weiter (Fall 2). Statisch hat ein Command eine Akteur-*Menge*
(Fixpunkt über den Graphen, `DomainEditor/AkteurAnteile.cs`), zur Laufzeit genau einen (§5.1).

### 2.4 Dienst eines Akteurs

Entscheidet ein Akteur *drinnen* über einen Dienst, gehört der Dienst ihm: `interface IGutachten : IAkteurDienst<Gutachter>`. Ein
Pipeline-Handle, der ihn als **Parameter** nimmt, entscheidet im Auftrag dieses Akteurs (der einzige Akteur-Wechsel mitten in einer Kette);
seine Ausgaben muss der Akteur dürfen, im Konstruktor/Feld ist der Dienst verboten (CQRS060). Damit steht fest, wer den Dienst benutzt.
Im Bestand ungenutzt (klassifiziert wird nur draußen), als Sprachmittel aber erhalten.

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
namespace Domain.Clients;   // Domain.Pipeline/Clients.cs

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

Die Akteure der getragenen Teile; dazu je `ISendet`/`IFragt` die `IDarf`-Halter — **nur, wenn keiner der Teil-Akteure den Typ schon darf**
(sonst wäre der KlassifikationsWorker über `HoleAktivesModell` auch Modellfreigeber). Generator, Python-Prepass, Wissensgraph und Editor
rechnen gleich.

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
| `Arbeitsplatz` (Blazor) | Inspekteur, KIOperator, Modellfreigeber, Produktpruefer | — | 14 Commands / 12 Queries, 12 Kenntnis-Events |

**Befund (offen, Domänen-Entscheidung):** Der Blazor-Code schickt `NimmRangeAuf` (Kuratieren) und fragt `HoleDatensatzSamples`
(Komposition) — beides darf kein Akteur des Arbeitsplatzes. Es steht deshalb nicht im Vertrag; mit Tor würde es abgelehnt. Entweder `IDarf`
ergänzen oder den Client-Code ändern.

---

## 5. Laufzeit

### 5.1 Die Kette

- Der Akteur reist als Event-Header **`akteur`** (`ImAuftrag.Header`): `MartenEventBatchWriter` schreibt ihn, `MartenEventStore` liest ihn
  in den Envelope (`UserId`), `AggregateActorBase` gibt ihn weiter.
- Der generierte Dispatch (Pipeline, Prozess, Konsument) setzt `ImAuftrag.Von(envelope.Akteur)` um den Handle — bzw. den Dienst-Akteur bei
  `IAkteurDienst<A>`. `CommandEmitter` stempelt `ImAuftrag.Akteur`; ist er leer, gilt Fall 3 (§2.3).
- Eine **Frist** speichert den Akteur beim Planen (`Frist.Akteur`) und feuert in seinem Namen; ein **Prozess** trägt den Akteur des Events,
  das ihn startete. (Die Frist `MarkiereAlsHaengengeblieben` trägt so das TrainingsSystem.)
- Trigger reisen ohne Envelope: ihr Akteur ist der eindeutige `IDarf`-Halter (`AkteurHerkunft.EindeutigerHalter`).

### 5.2 Handshake

- Der Client nennt im `CapabilitiesRequest` **`vertrag`** (Client-Name; als Übergang auch ein Akteur-Name mit Vertrag) und
  **`vertrag_hash`**. Der Server nimmt die Fähigkeiten aus **seiner** Tabelle, nicht aus der Selbstauskunft, und abonniert genau die
  Eingänge (Zusagen + Kenntnis) (`AkteurVertragsPruefung`).
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
| CQRS064 | GR-CLIENT-BEFUGT | Sendet/Fragt: ein Akteur hat `IDarf<T>`; Sendet = Command/Trigger/Transient, Fragt = Query |
| CQRS065 | GR-CLIENT | je Event höchstens eine Methode im Client (getragen oder eigen) |
| — (Laufzeit-Tor) | GR-HERKUNFT | jeder Command/Query/Trigger kommt von einem Akteur (direkt oder Kette) |
| — | GR-INGRESS-EINDEUTIG | ein Trigger ohne Kette hat genau einen `IDarf`-Halter |
| — | GR-GETRAGEN | sobald es Clients gibt: jede Zusage mit Ausgabe trägt mindestens ein Client (sonst bricht die Kette) |

Die Grammatik ist eine Quelle (`DomainEditor/Grammatik.cs`); `--check` prüft, dass jede genannte Build-ID im Code existiert.

---

## 8. Editor (Kurzfassung — Volltext `docs/konzept-domaenen-editor.md` §12)

- **Rahmen je Domäne × Akteur**; Doppelungen stehen beim ersten Akteur in Ablauf-Reihenfolge, „↥ auch" verweist.
- **📜 Vertrags-Rahmen** neben der Akteur-Karte, darin je `Auf` eine Karte **Zusage** (◀ Event · Ausgänge ▶).
- **🔌 Client-Rahmen** rechts neben den Domänen; die Karte ist die **Anschlussleiste** (trägt / sendet als … / fragt als … / hört). Je
  Client × Ziel-Rahmen **ein Bündel**, in Worten beschriftet (durchgezogen = trägt eine Zusage mit Ausgabe). Der Zoom ändert nichts;
  Einzelkanten nur durch einen Klick (Leisten-Zeile, Bündel, 🔌 in der Stecker-Zeile eines Akteur-Rahmens).
- Bearbeiten im Panel über ⊕: Akteur „darf ⊕", „+ Zusage ⊕ (Event)"; Client „trägt ⊕", „sendet/fragt ⊕", „hört ⊕". „C# schreiben"
  schreibt Akteur-Record, Vertrags-Interface und Client-Interface (neu additiv, bestehend chirurgisch über den Herkunfts-Stempel).

---

## 9. Stand und offen

**Gemessen 2026-10-06:** Prüfstand 280/280; Python SDK 19/19, Worker 11/11; Integration `VertragsZusageE2ETests` + `MetadataPostgresTests`
grün gegen echte Infra; `--check` (8 Akteure, 2 Verträge, 3 Clients / 43 Rand-Ports) und `--sonde` (Lesesaal = 2 Akteure, Archiv über 2
Clients, gezeichnetes Mahnportal) grün. Live: `run_stub.py` meldet sich als `KlassifikationsWorker`, der Server nimmt den Vertrag an und
abonniert genau die 3 Vertrags-Events.

**Offen:**
1. **Durable Zustellung + Zuteilung** an Clients (§5.4).
2. **`als` im CommandEnvelope** für spontane Commands, die mehrere Akteure eines Clients dürfen (heute: der erste).
3. **Blazor:** `IArbeitsplatz` gegen den Client-Code prüfen (typisierte IntentHandler, Capabilities aus dem Vertrag), Editor-Innenseite.
4. **Befund §4.5** (`NimmRangeAuf`, `HoleDatensatzSamples` am Arbeitsplatz).
5. Python-Worker melden sich noch ohne Token an; `Host.Grpc` hat das Tor nicht konfiguriert (opt-in).
6. Der Abgleich schreibt eine geänderte Client-Basisliste einzeilig; weitere Vertrags-Teile eines Akteurs liest der Editor, schreibt sie
   aber nicht.
