# 10 — Entwickler-API: „Wie lege ich X an?"

> Stand 2026-10-13, gegen den Code geprüft. Maßstab: [docs/konzept-werkbank.md](konzept-werkbank.md).

Wegweiser für jede Primitive des Frameworks. Grundregel: **du schreibst nur Fachcode; alles Dispatchende, Verdrahtende und
Serialisierende wird generiert** (Inv. 4, W10). Eine Primitive ist ein **Code-Fakt** — ein Marker-Interface, eine Signatur, selten ein
Attribut —, nie ein Name oder Namespace (W2). Es gibt zwei gleichwertige Wege, die denselben Code ergeben: die Geste in der Werkbank (jede Geste
ist ein Patch am Code, W4; Liste Geste → Code: [konzept-werkbank §A.2](konzept-werkbank.md)) oder der Code von Hand. Geprüft wird beim Bauen
(Analyzer, Generator-Diagnosen, Boot-Guards — W5); die Regeln stehen als Daten mit ihrem Build-Gegenstück in `DomainEditor/Grammatik.cs`
(`dotnet run --project GraphExtractor -- --grammatik`).

Jeder Abschnitt hat dasselbe Raster: **Zweck** · **Werkbank** (Karte, Geste, Port) · **Code** (Beispiel, meist aus der Agnostik-Sonde
`GraphExtractor.Kern/Sonde/*.cs.txt`, Domäne „Leihwesen") · **Build** (CQRS-IDs, Grammatik-Regel) · **Generiert / Laufzeit** · **Mehr**.

## 10.1 Übersicht

| Primitive | Code-Fakt | Werkbank-Anker | § |
|---|---|---|---|
| Aggregat | `IState` + `IDecider<S>` / `IApplier<S>` | Palette „+ Aggregat" | 10.2.1 |
| Command | `ICommand` (`ICreationCommand`) | „＋" am Decider | 10.2.2 |
| Event | `IEvent` | „＋" am Decide-Ausgang | 10.2.3 |
| Ablehnung | `ITransientEvent` im Decide-OneOf | „＋" am Decide-Ausgang | 10.2.4 |
| Wertobjekt / Enum | Record ohne Rolle (`IWertobjekt` optional) / `enum` | Baustein-Menü der Domäne | 10.2.5 |
| ReadModel | `IReadModel` (`IGeteiltesReadModel`) | Baustein-Menü | 10.3.1 |
| Store-Fähigkeit | Interface `: IWriteStore` / `: IReadStore`, genau eine Funktion | an der Store-Karte | 10.3.2 |
| Store-Bündel + Implementierung | `: IStore, IFähigkeit…` + eine Klasse | Baustein-Menü | 10.3.3 |
| Projektion | `ISubscriber, IPullSubscriber`, Handles mit Schreib-Fähigkeit | Palette „+ Projektion" | 10.3.4 |
| Query / Antwort | `IQuery` / `IQueryResponse` | Baustein-Menü, „＋" am Reader | 10.3.5 |
| Reader | `IReader<P>` | Baustein-Menü | 10.3.6 |
| Reaktion | `ISubscriber, IPullSubscriber`, Handle liefert Commands | Baustein-Menü | 10.4 |
| Prozess | `IProzessDefinition` + `Prozess<T>.Definiere` | Baustein-Menü (Prozess, Regel) | 10.5 |
| Katalog-Funktion | Interface `: IFunktion` + Record `: IAuftrag<F>` | Palette „+ ƒ Funktion" | 10.5a |
| Domänen-Dienst | Interface `: IDienst` | „🧩 braucht" an der Funktion | 10.5b |
| Konfig-Record | Record im Konstruktor einer Implementierung | Baustein-Menü, 🖥 „⚙ Konfig" | 10.5c |
| Fluss | `IPipeline` + `PipelineFluss.Definiere` | Palette „+ ⛓ Fluss" | 10.6 |
| Quelle | `sealed record X : DateiGemeldet;` | Palette „+ 📥 Quelle" | 10.6a |
| Akteur | `IAkteur` (`IMensch`/`IMaschine`/`IKi`) + `IDarf<T>` | Palette „+ 👤 Akteur" | 10.7 |
| Akteur-Vertrag | `IAkteurVertrag<A>` (Zusage / Kenntnis) | 📜-Rahmen am Akteur | 10.7a |
| Client-Vertrag | `IClientVertrag` (+ `ISendet<C>`, `IFragt<Q>`); C#: `[assembly: IstClient<IX>]` | Palette „+ 🔌 Client" | 10.8 |
| Umgebung | `umgebungen/umgebung.<name>.json` | 🖥 Host-Rahmen | 10.9 |

**Werkbank-Wege allgemein** ([konzept-werkbank §8.5](konzept-werkbank.md)): Neues beginnt an der **Palette** (Aggregat · ⛓ Fluss · ƒ Funktion ·
📥 Quelle · 👤 Akteur · 🔌 Client · Projektion); jede weitere Sorte legt das **Baustein-Menü** einer Domäne an („⋯" bzw. Rechtsklick am
Domänen-Rahmen → „＋ Baustein…"); Zugehöriges entsteht mit **„＋" am Port**, an dem es hängt; **⊕** im Panel verbindet oder löst (passende Karten
leuchten, der Server prüft über `POST /api/werkbank/passt`). Deklariert wird nur an der eigenen Karte; Rümpfe stehen aus der Datei am Rumpf-Port
(✎ bzw. „🤖 füllen"). Der ⊕-Leitsatz: was sich verbinden lässt, kompiliert.

**Attribute** gibt es wenige, alle optional oder Framework-intern: `[AggregatName("…")]`, `[ProzessName("…")]` (persistierte Identität vom
Klassennamen entkoppeln; Kollision = CQRS011 bzw. CQRS012), `[ProjectionReader(TrackDeps = …)]` (Reader, §10.3.6), `[assembly: IstClient<IX>]`
(C#-Client, §10.8), `[RoutingTabelle]` (markiert das Routing-Generat, nicht für Fachcode) und `[Ingress]` (Altlast, §10.13).

**Proto, STJ-Kontexte, Python-Basen entstehen automatisch.** Im Build läuft vor dem Compile der Codegen (Target `CqrsCodegenPrepass` in
`Domain.Leitung/Domain.Leitung.csproj` → `Cqrs.Codegen`): `domain.proto`, `CqrsWireJsonContext.g.cs`, `EventJsonSerializerContext.g.cs`,
`generated/vertraege.py`, `funktionen.py`, `funktions_tabelle.py`, `nutzlast_tabelle.py`. Ein neuer Command, ein Event, eine Query braucht keinen Handgriff; scheitert
der DTO-Mapper, meldet es CQRS030. (Der Codegen liest heute per `MetadataLoadContext` — Schuld, docs/13 W10-1.)

## 10.2 Schreibseite

### 10.2.1 Aggregat (State · Decider · Applier)

**Zweck:** eine konsistente Entscheidungseinheit — Commands werden gegen den gefalteten Zustand entschieden, heraus kommen Events oder eine
Ablehnung. **Werkbank:** Palette „+ Aggregat" schreibt `{A}.cs`, `{A}.Decider.cs`, `{A}.Applier.cs`; ein neues Decide/Apply bekommt einen
werfenden Rumpf („⚙ Logik fehlt"), gefüllt mit ✎ oder „🤖 füllen". **Code** (Sonde `Ausleihe.cs.txt`):
```csharp
public partial class Leihvorgang : IState
{
    public Guid LeserId { get; set; }
    public bool Offen { get; set; }

    public partial class Decider : IDecider<Leihvorgang>
    {
        public IEnumerable<OneOf<Ausgeliehen, BereitsAusgeliehen>> Decide(LeiheAus befehl)
        {
            if (this.State.Offen) { yield return new BereitsAusgeliehen(befehl.AggregateId); yield break; }
            yield return new Ausgeliehen(befehl.Medium, befehl.LeserId, befehl.Tage);
        }
    }

    public partial class Applier : IApplier<Leihvorgang>
    {
        public void Apply(Ausgeliehen e) { this.State.LeserId = e.LeserId; this.State.Offen = true; }
    }
}
```
`Id`/`Version` und `State` erzeugt der Generator; Decider/Applier dürfen in eigenen Dateien stehen (Sonde `Mahnvorgang.Decider.cs.txt`).
**Build:** CQRS050 (Decide gibt konkrete Typen bzw. `OneOf<…>` zurück, nie `IEvent`), CQRS079 (entschiedenes persistentes Event ohne `Apply` —
Fehler), CQRS080 (gefaltetes Event, das kein Decide erzeugt — Warnung), CQRS010 (Command bei mehreren Aggregaten), CQRS011 (Identitäts-Kollision);
GR-AUS-AGGREGAT, GR-FALTUNG, GR-COMMAND. **Generiert / Laufzeit:** AggregateHandler (mit Code-Hash von State + Applier für die Snapshot-Version),
Factory, Actor und `GeneratedCommandRouting` aus den Decide-Signaturen. Ein virtueller Cluster-Actor je Aggregat-Id ist der einzige Schreiber:
Inbox-Dedup → Decide → Append (Group-Commit) → Apply → Signal; Spur-Wirker `decide:A/C`. **Mehr:** [03 §3.1–§3.2](03-schreibseite.md).

### 10.2.2 Command

**Zweck:** eine Absicht an genau ein Aggregat. **Werkbank:** „＋" am Decider (bzw. ⊕ an einem vorhandenen Command); Felder im Panel.
**Code:** `public record LeiheAus(Guid AggregateId, Signatur Medium, Guid LeserId, int Tage = 14) : ICreationCommand;` — `ICommand` verlangt
`Guid AggregateId`; `ICreationCommand` markiert den anlegenden Command (eine Lesart für Extractor und LLM, keine eigene Laufzeit-Semantik).
**Build:** CQRS010, CQRS086 (Command ohne Decider, Warnung), CQRS002 bzw. CQRS078 (Prozess bzw. Fluss sendet einen Command ohne Decider);
GR-COMMAND, GR-AKTEUR (wer ihn hineingibt, braucht `IDarf<C>`). **Generiert / Laufzeit:** Routing-Eintrag, Proto-DTO, Wire-Eintrag. Von außen
kommt ein Command über gRPC oder den JSON-Eingang durch das Akteur-Tor; intern gibt es genau einen Weg, `ICommandEmitter` (deterministische
Command-Id + Inbox = genau einmal wirksam; erzwungen durch CQRS020/021). **Mehr:** [03 §3.1](03-schreibseite.md), [04 §4.4](04-konsum-und-prozess-maschine.md).

### 10.2.3 Event

**Zweck:** ein durabler Fakt im Log — das eigene Aggregat faltet ihn, beliebig viele Konsumenten hören ihn. **Werkbank:** „＋" am Decide-Ausgang;
„◀ kommt aus · geht an ▶" im Panel zeigt Erzeuger und Konsumenten. **Code:** `public record Ausgeliehen(Signatur Medium, Guid LeserId, int Tage) : IEvent;`
**Build:** CQRS079/080, CQRS085 (Event ohne Erzeuger, Warnung), CQRS057 (Handle-Form der Konsumenten); GR-EVENT, GR-FALTUNG. **Generiert /
Laufzeit:** Signal `StateChangeVia{E}`, Event-Typ-Registrierung für Marten, STJ- und Proto-Eintrag. Ordnung und Vollständigkeit kommen nur aus
dem Stream-Read; das Signal ist nur Weckruf (Poll 30 s als Sicherheit). Jeder Log-Eintrag trägt die Spur-Header `ursache`/`wirker`/`ueber`/`stand`.
Ein **transientes Event** (`ITransientEvent`, kein Persist, kein Signal) ist verlierbar; es entsteht als Ablehnung (§10.2.4) oder als Ausgang
einer Projektion/Reaktion (GR-TRANSIENT, GR-AUS-PROJEKTION). **Mehr:** [03 §3.3, §3.6](03-schreibseite.md).

### 10.2.4 Ablehnung

**Zweck:** die fachliche Absage eines Commands — kein Fakt, eine Rückmeldung. **Werkbank:** „＋" am Decide-Ausgang, Art Ablehnung.
**Code:** `public record BereitsAusgeliehen(Guid AusleiheId) : ITransientEvent;` im OneOf des Decide (es gibt keinen eigenen Marker).
**Build:** CQRS085; GR-TRANSIENT, GR-AUS-AGGREGAT. **Generiert / Laufzeit:** Proto und Wire wie ein Event, aber kein Signal und keine
Marten-Registrierung. Eine Ablehnung ist das einzige Ergebnis des Decide. Ein Client-Command bekommt sie als Antwort; bei einem emittierten
Command co-committet das Aggregat die Marke `KommandoAbgelehnt` — ein Prozess wertet sie als gescheiterten Schritt (Kompensation), ein Fluss als
✕-Port `BeiAbgelehnt()`. **Mehr:** [03 §3.1](03-schreibseite.md), [04 §4.5](04-konsum-und-prozess-maschine.md).

### 10.2.5 Wertobjekt und Enum

**Zweck:** unveränderliche Feldtypen. **Werkbank:** Baustein-Menü („Value Object", „Enum"); verwendet werden sie über den Feldtyp (Port `ftype`).
**Code:** `public record Signatur(string Regal, int Nummer);` (Record ohne Rolle; der Marker `IWertobjekt` ist erlaubt, wird aber nicht
verlangt und von keiner Regel ausgewertet) · `public enum Zustand { Neu = 1, Gebraucht = 2, Beschaedigt = 3 }`. **Build:** CQRS081 (Enum-Wert 0
geht auf dem Proto-Wire als Default verloren — 1-basiert nummerieren, Warnung). **Generiert:** ein DTO je Wertobjekt, sobald eine Nachricht es
trägt; im Wire-Kontext stehen Enums als Namen.

## 10.3 Leseseite

Projektionen materialisieren Events in Dokumente; Reader beantworten Queries daraus. Der Fachcode sieht nur **Fähigkeiten** — Interfaces mit genau
einer Store-Funktion, die ein Handle als Parameter bekommt. Kein Store im Konstruktor oder Feld eines Konsumenten (CQRS054), kein `new …Store()`
(CQRS055). Beispiel für alles: Sonde `Lesen.cs.txt`; Beispieldomäne: `Domain.Projections/`, Stores in `Domain.Infrastructure/`.

### 10.3.1 ReadModel (+ geteilt)

**Zweck:** ein Dokument der Leseseite. **Werkbank:** Baustein-Menü „Read Model". **Code:**
```csharp
public class LeihEintrag : IReadModel { public Guid Id { get; set; } public Guid LeserId { get; set; } public bool Offen { get; set; } }

// geteilt: mehrere Streams schreiben hinein → Versionsprüfung
public record Ausleihzaehler : IReadModel, IGeteiltesReadModel { public string Id { get; init; } = "gesamt"; public int Offen { get; init; } }
```
**Generiert / Laufzeit:** Marten-Dokument mit Identität `Id` (registriert in `AddGeneratedProjectionServices`, Teil von `AddCqrs`); ein geteiltes
Dokument mit Optimistic Concurrency. Wo möglich Zeilen je (Stream, Schlüssel) statt eines geteilten Dokuments (Schreib-Regel §10.3.3).

### 10.3.2 Store-Fähigkeit

**Zweck:** genau eine Store-Funktion als Typ — die Obergrenze dessen, was ein Handle darf, steht in seiner Signatur. **Werkbank:** an der
Store-Karte als Schreib- bzw. Lese-Fn; ein Handle bekommt sie per ⊕ an seinem Fähigkeits-Port (Geste `ParameterAbgleich`).
**Code:** `public interface INotiereAusleihe : IWriteStore { Task SchreibeAsync(LeihEintrag eintrag); }` ·
`public interface IZaehleOffene : IReadStore { Task<IReadOnlyList<LeihEintrag>> OffeneAsync(Guid leserId); }` (Namen frei).
**Build:** CQRS051 (genau eine Funktion), CQRS053 (Fähigkeit in mehreren Stores), CQRS067 (Schreib-Fähigkeit liest nicht, Lese-Fähigkeit puffert
nicht); GR-FAEHIGKEIT, GR-FUNKTION-FAEHIGKEIT. **Laufzeit:** der generierte Dispatch löst die Fähigkeiten aus einem DI-Bereich auf — eine
Store-Instanz je Bereich, damit Effekt und Marke in einer Transaktion landen.

### 10.3.3 Store-Bündel + Implementierung

**Zweck:** das Bündel ist die Transaktionsgrenze aller seiner Fähigkeiten; die Implementierung puffert die Effekte und committet sie mit der Marke.
**Werkbank:** Baustein-Menü „Store"; die Implementierung gehört zur Store-Karte, jede Store-Fn steht mit ihrem Rumpf an ihrer Fähigkeit (✎).
**Code:** `public interface ILeihBuch : IStore, INotiereAusleihe, ISchliesseAusleihe, IZaehleOffene { }` und genau eine Klasse, die es
implementiert. Produktiv erbt sie von `MartenCoCommitStoreBase` (`Core/`) und ist damit `IProjectionTracker` mit Co-Commit:
```csharp
public sealed partial class ModellStore : MartenCoCommitStoreBase, IModellStore        // Beispieldomäne, Domain.Infrastructure/ModellStore.cs
{
    public ModellStore(IDocumentStore store) : base(store) { }
    public Task UpsertAsync(ModellReadModel model) => EnqueueStore(model.Id, model);
}
```
**Schreib-Regel.** Die Maschine garantiert einen Schreiber je (Projektion, Stream); das Co-Commit macht einen Stapel atomar, isoliert aber nicht zwei
parallele Stapel verschiedener Streams. Deshalb schreibt eine Store-Methode auf genau einem von drei Wegen, und die Basis prüft das beim Puffern:

| Dokument | Weg | Beispiel (Beispieldomäne) |
|---|---|---|
| eigenes (Schlüssel = Stream des Stapels) | `EnqueueStore(id, doc)`, `EnqueueTransform`, `EnqueueAnlegenOderAendern` | `TrainingslaufReadModel` |
| Zeile dieses Streams (Id enthält den Besitzer) | `EnqueueZeile(besitzer, zeile)`, `EnqueueZeileEntfernen` | `DatensatzMitgliedschaftZeile` `{Datensatz}:{Paar}` |
| geteilt (Marker `IGeteiltesReadModel`) | `EnqueueGeteilt(id, alt => neu)` — Versionsprüfung; ein Konflikt verwirft den Stapel samt Marke, der Adapter wiederholt | `AktivesModellReadModel` |

Ein fremdes Dokument über den ersten Weg wirft sofort (statt eines stillen Lost Updates). „Neuer gewinnt" in einem geteilten Dokument nach
Event-Zeit, nie nach Ankunft. Am Puffer vorbei zu schreiben ist ein Build-Fehler: **CQRS066** erlaubt in einem Co-Commit-Store von der
Datenzugriffs-Bibliothek nur `Store.QuerySession()` → `LoadAsync`/`LoadManyAsync`/`Query` (+ `ToListAsync`/`CountAsync` …) und verbietet, einen
Zugang weiterzureichen; **CQRS067** trennt die Rollen. Die Positivliste steht einmal in `Abstractions/StoreZugriff.cs`.
**Generiert / Laufzeit:** DI (scoped, die eine Instanz unter jeder Fähigkeit) und ein Code-Hash je Store; ändert sich der Store, baut Sim die
Leseseite beim Start neu auf (`ProjektionsStandDienst`). **Mehr:** [04 §4.3](04-konsum-und-prozess-maschine.md).

### 10.3.4 Projektion

**Zweck:** ein replaybarer durabler Konsument — Events in ReadModels, genau einmal wirksam über Co-Commit. **Werkbank:** Palette „+ Projektion";
je Event eine Handle-Karte, Schreib-Fähigkeiten per ⊕, Rumpf am Handle. **Code** (Sonde `Lesen.cs.txt`):
```csharp
public partial class Leihliste : ISubscriber, IPullSubscriber
{
    public string SubscriberId => "leihliste";

    public async Task Handle(Ausgeliehen evt, IAggregateEnvelope envelope, ProjectionWriter writer, INotiereAusleihe notiere)
        => await writer.Execute(envelope.AggregateId.ToString(), async ctx =>
        {
            ctx.Track<Leihvorgang>(envelope.AggregateId);
            await notiere.SchreibeAsync(new LeihEintrag { Id = envelope.AggregateId, LeserId = evt.LeserId, Offen = true });
        });
}
```
Handle-Form: `Handle(TEvent, IAggregateEnvelope, ProjectionWriter, Fähigkeit…)`; ohne Ausgabe `Task`, sonst ein Strom transienter Events als
`OneOf<…>`. `ProjectionWriter.Execute(string readModelId, …)` und `ctx.Track<A>(id)` melden die Abhängigkeiten für Reader mit `TrackDeps`.
**Build:** CQRS057 (Form), CQRS050, CQRS052 (Schreib-Fähigkeiten aus mehreren Stores), CQRS054/055, CQRS083 (Zustand in Instanzfeldern,
Warnung), CQRS088 (Kreis über transiente Events); GR-AUS-PROJEKTION, GR-ZUSTAND. Der Opt-in-Marker `IAppendProjektion` (für nicht-idempotente
Effekte) lässt den Start brechen, wenn kein Co-Commit-Tracker dahintersteht (GA-1). **Generiert / Laufzeit:** Subscriber-Dispatch und Pull-Pfad
(`PullPathGenerator`). **Achse B folgt aus den Schreib-Fähigkeiten der Handles:** lösen sie einen Store auf, der `IProjectionTracker` ist, ist der
Konsument replaybar (Projektion); ohne Schreib-Fähigkeit ist er emittierend (§10.4). Der `ProjectionAdapter` liest ab Marke + 1 aus dem Store,
dispatcht und committet Effekt + Marke; geweckt über Signal und Poll. **Mehr:** [04 §4.1–§4.3](04-konsum-und-prozess-maschine.md).

### 10.3.5 Query und Query-Antwort

**Zweck:** eine synchrone Leseanfrage an genau einen Reader und ihre Antworten. **Werkbank:** Baustein-Menü „Query"/„Response"; am Reader-Handle
„＋" bzw. ⊕ für Eingang und Antworten. **Code:** `public record OffeneAusleihen : IQuery { public Guid LeserId { get; init; } }` ·
`public record OffeneAusleihenAntwort(int Anzahl) : IQueryResponse;` · `public record KeineAusleihen() : IQueryResponse;`
**Build:** CQRS084 (genau ein Reader), CQRS086 (Query ohne Reader, Warnung), CQRS085 (Antwort ohne Erzeuger, Warnung); GR-QUERY, GR-AUS-READER,
GR-AKTEUR (`IDarf<Q>`). **Laufzeit:** über gRPC oder den JSON-Eingang durch das Akteur-Tor in den generierten `ProjectionQueryService`; kein
Log-Eintrag, keine Spur.

### 10.3.6 Reader

**Zweck:** beantwortet Queries über die Lese-Fähigkeiten einer Projektion. **Werkbank:** Baustein-Menü „Reader"; je Query ein Handle,
Lese-Fähigkeiten per ⊕ (auch aus mehreren Stores). **Code:**
```csharp
[ProjectionReader(TrackDeps = false)]                                    // optional; Default TrackDeps = true
public partial class LeihAuskunftLeser : IReader<Leihliste>
{
    public async Task<OneOf<OffeneAusleihenAntwort, KeineAusleihen>> Handle(OffeneAusleihen query, IMessageEnvelope envelope, ReadContext ctx, IZaehleOffene zaehle)
    {
        var offen = await zaehle.OffeneAsync(query.LeserId);
        return offen.Count == 0 ? new KeineAusleihen() : new OffeneAusleihenAntwort(offen.Count);
    }
}
```
**Build:** CQRS057, CQRS050, CQRS054/055, CQRS084. **Generiert / Laufzeit:** Reader-Dispatch und `ProjectionQueryService`; je Query ein
Fähigkeits-Bereich; mit `TrackDeps` liest er die Abhängigkeiten aus dem Redis-Index. **Mehr:** [04 §4.3](04-konsum-und-prozess-maschine.md).

## 10.4 Reaktion

**Zweck:** ein emittierender durabler Konsument — Event → Command. Kein eigener Marker: die Rolle folgt aus der Signatur (ein Handle liefert
Commands, keine Schreib-Fähigkeit). „Reaktion" heißt nur dieser Backend-Baustein, nie eine Zusage eines Akteurs (§10.7a). **Werkbank:**
Baustein-Menü „Reaktion"; Ausgänge per „＋"/⊕. **Code** (Sonde `Lesen.cs.txt`):
```csharp
public partial class MahnungBeiSchaden : ISubscriber, IPullSubscriber
{
    public string SubscriberId => "mahnung-bei-schaden";

    public async IAsyncEnumerable<EroeffneMahnung> Handle(SchadenGemeldet evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    {
        await Task.CompletedTask;
        yield return new EroeffneMahnung(Guid.NewGuid(), 5m);
    }
}
```
**Build:** CQRS050 (Ausgabe als konkreter Typ oder `OneOf<…>`, nie `ICommand`), CQRS057, CQRS020 (nie selbst `RequestAsync`), CQRS021 (kein
`CancellationToken.None` auf eine Command-Kante), CQRS082 (Command ab transientem Eingang, Warnung); GR-AUS-REAKTION, GR-GARANTIE.
**Generiert / Laufzeit:** derselbe Pull-Pfad wie die Projektion, aber mit `IEmittentenCursor` (best-effort, kein Reset). Jeder Command geht über
`ICommandEmitter` mit deterministischer Id (Stream, Version, Command-Typ) — genau einmal wirksam über die Inbox des Empfängers; Spur-Wirker
`reaktion:T`. Transiente Ausgänge gehen verlierbar über den Broker. **Mehr:** [04 §4.4](04-konsum-und-prozess-maschine.md).

## 10.5 Prozess

**Zweck:** ein typisiertes Regel-Netz über mehrere Aggregate: jede Regel ist eine Konjunktion von Event-Typen, die Commands sendet oder eine
Katalog-Funktion ruft; mit Kompensation und Zeitlimit. Abläufe ab einer Quelle mit Funktions-Knoten schreibt man als Fluss (§10.6); beide laufen auf
demselben Dirigenten (die Doppelung der DSLs ist offen, docs/13 W11-1). **Werkbank:** Baustein-Menü „Prozess" und „Regel"; die Regel zeigt
WENN (+ und) → Dann; „+ Dann" (weiterer Command), „+ Dann ƒ" (Funktion rufen), „×N" (`SendeJe`), ↩ Kompensation, ⏳ Zeitlimit; Bau-Argumente im
Zuordnungs-Editor, Lambdas am Rumpf-Port. Ein Count-Join wird gelesen und erhalten („+alle"), angelegt wird er im Code. **Code** (Sonde
`Gebuehren.cs.txt`):
```csharp
public sealed class SchadenBeiRueckgabe : IProzessDefinition
{
    ProzessRegeln IProzessDefinition.Regeln => Prozess<Zurueckgegeben>.Definiere(p =>
    {
        p.Auf<Zurueckgegeben>().Sende<MeldeSchaden>(e => new MeldeSchaden(Guid.NewGuid(), e.LeserId));
        p.Auf<SchadenGemeldet>().Rufe<ISchadensSchaetzung>(s => new SchaetzeSchaden(s.LeserId, 25m)).Zeitlimit(TimeSpan.FromMinutes(5));
        p.Auf<SchadenGeschaetzt>().Sende<EroeffneMahnung>(g => new EroeffneMahnung(Guid.NewGuid(), g.Betrag));
    });
}
```
Verben (`Abstractions/Prozess/ProzessBuilder.cs`): `Auf<E>()`, Join `.Und<E2>()` (bis drei Events), Count-Join `.UndAlle<S>(e => n).Sende<C>((e, liste) => …)`,
`.Sende<C>(…)`, Fan-out `.SendeJe<C>(e => …)`, `.Rufe<F>(…)` (§10.5a), danach `.Zeitlimit(t)` und `.RückgängigDurch<C>(…)` bzw. `RückgängigDurchJe`.
**Build:** CQRS001 (zwei Prozesse mit demselben Auslöser), CQRS002 (gesendeter Command ohne Decider), CQRS003 (Typ-Argument explizit), CQRS012
(Prozessname doppelt), CQRS072 (Regelname doppelt); Boot-Guards Azyklizität, Funktions-Bindung, Auslöser-Kollision; GR-AUS-PROZESS, GR-ZYKLUS.
**Generiert / Laufzeit:** `GeneratedProzessRegeln` mit Prozess- und Regelnamen (Spur-Wirker `prozess:P#A→C`). Ein generischer Prozess-Manager je
Korrelation faltet das Marking aus dem Log (mit nicht-autoritativem Marking-Cursor), sendet mit deterministischer Command-Id, kompensiert
rückwärts; ein Zeitlimit läuft gegen die DB-Uhr (`IDbClock`) ab Aktivierung der Regel (entdeckt über den Backstop, docs/13 W11-2). **Mehr:** [04 §4.5](04-konsum-und-prozess-maschine.md).

## 10.5a Katalog-Funktion

**Zweck:** ein zustandsloser Aufruf — ein Auftrag hinein, genau ein OneOf-Ergebnis-Event heraus; gerufen wie ein Aggregat. Kurz oder lang ist nur
ein Zeitlimit. Verarbeitung ist immer eine Katalog-Funktion, nie ein Rumpf am Rand. **Werkbank:** Palette „+ ƒ Funktion" (Funktion mit Auftrag und
einem Ergebnis); an ihrer Karte Auftrag, Ergebnisse (OneOf), „⚙ liest" (Lese-Fähigkeit per ⊕), „🧩 braucht" (§10.5b), „＋ C#-Implementierung
anlegen", Rumpf (✎/🤖; bei Laufort `Extern` der Python-Rumpf). Gerufen wird sie aus einer Prozess-Regel („+ Dann ƒ") oder einem Fluss („＋ƒ").
**Code** (Sonde `Gebuehren.cs.txt`, `Fluss.cs.txt`):
```csharp
public sealed record SchaetzeSchaden(Guid LeserId, decimal Neuwert) : IAuftrag<ISchadensSchaetzung>;   // der EINE Eingang
public record SchadenGeschaetzt(decimal Betrag) : IEvent;                                               // Ergebnisse = persistente Events
public record NichtSchaetzbar(string Grund) : IEvent;

public interface ISchadensSchaetzung : IFunktion
{
    Task<OneOf<SchadenGeschaetzt, NichtSchaetzbar>> RufeAsync(SchaetzeSchaden auftrag, IAusfuehrung x);
}

public sealed class Schadensschaetzer(IWertTabelle tabelle) : ISchadensSchaetzung { /* RufeAsync … */ }   // Implementierung = Code-Fakt

// mit Lese-Fähigkeit (nur Laufort Host): Parameter nach IAusfuehrung
public interface IOffenZaehlung : IFunktion { Task<OneOf<OffeneGezaehlt>> RufeAsync(ZaehleOffene auftrag, IAusfuehrung x, IZaehleOffene offene); }
```
**Laufort** ist Konfig, nicht Code: `Funktionen:<F>` = `{ "Laufort": "Host" | "Extern" | "Hand", "Implementierung": …, "Slots": 2,
"Wiederholungen": 1, "Worker": { … } }` (Default `Host`; `Implementierung` nur bei mehreren C#-Implementierungen).
- **Host:** die C#-Implementierung, gebaut über die generierte Fabrik `GeneratedFunktionen.Implementierungen` (Konstruktor direkt, ohne Reflection).
- **Extern:** ein Python-Worker über gRPC. Der Codegen erzeugt je Funktion ohne Lese-Fähigkeit eine Basis in `domain_client/generated/funktionen.py`
  (`<Name>Basis` mit `FUNKTION`, `AUFTRAG`, `ERGEBNISSE`, `async def rufe(self, auftrag, x)`) und die Tabelle `generated/funktions_tabelle.py`.
  Die Implementierung liegt in `Domain.Client.Worker.Python.ML/funktionen/<modul>.py` (Regel `Abstractions.Funktionsvertrag`; ⟳ legt eine fehlende
  aus der Basis an), erbt die Basis und gibt genau ein Ergebnis zurück; Werte in `self.konfig`. Gestartet wird der generische Worker
  `funktions_worker.py --funktionen <F> [--slots n] [--konfig NAME=WERT …]` mit `CQRS_GRPC_HOST`/`CQRS_GRPC_PORT`/`CQRS_AKTEUR_TOKEN` — in der
  Werkbank „▶ Worker"; der Akteur des Tokens braucht `IDarf<F>`.
- **Hand:** der Host hält die Aufträge (`HandAusfuehrer`), die Werkbank beantwortet sie (⧗ Aufträge) — ein Mensch als Ausführer beim Durchspielen.

**Build:** CQRS068 (feste Form: genau `RufeAsync(TAuftrag, IAusfuehrung[, Lese-Fähigkeit…])`, persistente Ergebnisse), CQRS069 (ein Auftrag je
Funktion), CQRS003 (`Rufe<F>` mit explizitem Typ); Boot-Guard „jede gerufene Funktion ist gebunden" (`FunktionsExtensions`); GR-FUNKTION-AUFTRAG,
GR-AUS-FUNKTION, GR-FUNKTION-FAEHIGKEIT, GR-FUNKTION-GEBUNDEN. **Generiert / Laufzeit:** `GeneratedFunktionen` (Auftrag → Funktion, Ergebnisse,
Fähigkeiten, Fabriken, Dispatch). Der Dirigent bietet alle offenen Aufträge an; ein Vermittler-Actor je Funktion verteilt sie an den Abholer des
Lauforts; jeder schließt über `FunktionsAusfuehrer` ab: **genau ein** Ergebnis im Ausführungs-Stream (bei einer Ausnahme nach den Wiederholungen die
Marke `KommandoAbgelehnt` → gescheiterter Schritt). Das Ergebnis aktiviert `Auf<Ergebnis>` bzw. `Bei<Ergebnis>()` wie ein Aggregat-Event;
Spur-Wirker `funktion:F`. **Mehr:** [konzept-editor-pipelines §14.5](konzept-editor-pipelines.md), [09](09-python-sdk.md),
[konzept-werkbank §6](konzept-werkbank.md).

## 10.5b Domänen-Dienst

**Zweck:** Technik hinter einer Funktions-Implementierung (Bild skalieren, Tabelle nachschlagen …); die Umgebung wählt die Implementierung.
**Werkbank:** Dienst-Karte 🧩 (Methoden, Implementierungen mit Konstruktor, Rümpfe mit ✎); an der Funktion „🧩 braucht (Konstruktor)" per ⊕ bzw.
„＋" (neuer Dienst als eigene Karte); die Wahl je Umgebung im 🖥 Host-Rahmen („🧩 Dienste"). **Code** (Sonde `Gebuehren.cs.txt`):
```csharp
public interface IWertTabelle : IDienst
{
    decimal Restwert(decimal neuwert, int alterJahre);
    Task<bool> IstSchaetzbarAsync(Guid leserId);
}
public sealed class LineareWertTabelle(WertTabellenKonfig konfig) : IWertTabelle { … }   // jede öffentliche implementierende Klasse
public sealed class Schadensschaetzer(IWertTabelle tabelle) : ISchadensSchaetzung { … }   // Bedarf im Konstruktor
```
Umgebung: `"Dienste": { "IWertTabelle": { "Implementierung": "LineareWertTabelle" } }` — nur nötig, wenn es mehrere gibt. **Build:** CQRS089
(ein Dienst nur im Konstruktor einer Funktions-/Dienst-Implementierung — nie als Handle-/Decide-Parameter, Feld oder Eigenschaft im Fachcode),
CQRS090 (der Konstruktor nimmt nur Dienste, Konfig-Records und `ILogger<T>`), CQRS091 (Konfig-Felder bindbar), CQRS092 (Implementierung
öffentlich, nicht generisch); GR-DIENST, GR-DIENST-BINDBAR, GR-DIENST-GEBUNDEN. **Generiert / Laufzeit:** `GeneratedDienste` (Fabriken, Bedarf je
Konstruktor, Konfig-Bindung). `AddCqrs` bindet nach den Funktionen transitiv, was deren Implementierungen brauchen; Boot-Fehler bei fehlender,
mehrdeutiger oder unbekannter Implementierung, Kreis oder Konfig-Fehler (Karte `dienst:<IX>` bzw. `host:Konfig`). **Mehr:**
[konzept-werkbank §4.1–§4.2](konzept-werkbank.md).

## 10.5c Konfig-Record

**Zweck:** Werte aus der Umgebung für eine Funktions- oder Dienst-Implementierung. Kein Marker: Konfig-Record ist ein Record im öffentlichen
Konstruktor einer Implementierung. **Werkbank:** Baustein-Menü „Konfig"; die Werte je Umgebung im 🖥 Host-Rahmen („⚙ Konfig", Felder aus dem Code).
**Code:** `public record WertTabellenKonfig(decimal Mindestwert, int Abschreibungsjahre = 5);` — Umgebung:
`"Konfig": { "WertTabellenKonfig": { "Mindestwert": 5 } }`. **Build:** CQRS090/091 (bindbare Feldtypen: Text, Zahl, Wahrheitswert, Dauer, Guid,
Enum, Text-Liste). **Laufzeit:** Feld für Feld aus `Konfig:<Record>` gebunden; ein Default im Record gilt, wenn das Feld fehlt; unbekannte Felder
und ein fehlender Abschnitt sind Boot-Fehler.

## 10.6 Fluss

**Zweck:** eine Pipeline als Fläche aus Knoten — Quelle (oder Event aus dem Log) → Katalog-Funktionen → Commands, vom Draht aus verdrahtet. Der
Variablenname ist die Identität des Knotens. **Werkbank:** Palette „+ ⛓ Fluss" (⛓-Rahmen in der Heim-Domäne seiner Commands); an einem Ausgang
„＋ƒ" (Funktions-Knoten), „＋▶" (Command-Knoten), „＋⧉" (Je-Rahmen), am Strom „＋⧗" (Warten mit Zeitlimit); was ein Knoten ruft oder sendet, per
⊕; ⏳ Zeitlimit im Panel; Bau-Argumente im Zuordnungs-Editor. **Code** (Sonde `Fluss.cs.txt`):
```csharp
public sealed class RueckgabePruefung : IPipeline
{
    public PipelineFluss Fluss => PipelineFluss.Definiere(scan =>
    {
        var eingang    = scan.Quelle<RueckgabeGescannt>();                         // oder p.Auf<Event>() — Event aus dem Log
        var seiten     = eingang.Je(e => e.Seiten);                                // Je-Rahmen
        var seite      = seiten.Rufe<ISeitenPruefung>(s => new PruefeSeite(s));
        var schnell    = eingang.Rufe<ISchadensSchaetzung>(e => new SchaetzeSchaden(e.LeserId, 10m)).Zeitlimit(TimeSpan.FromSeconds(30));
        var gruendlich = eingang.Rufe<ISchadensSchaetzung>(e => new SchaetzeSchaden(e.LeserId, 100m));   // dieselbe Funktion, anderer Knoten
        var mahnung    = scan.Alle(gruendlich.Bei<SchadenGeschaetzt>(), eingang)                         // ∧ (2–4 Drähte)
            .Sende<EroeffneMahnung>((g, e) => new EroeffneMahnung(e.LeserId, g.Betrag))
            .Oder(schnell.BeiZeitlimit(), z => new EroeffneMahnung(Guid.Empty, 0m));                     // ∨ über den ⏳-Port
        seiten.Sammle(seite.Bei<SeiteGeprueft>()).Sende<MeldeSchaden>((e, geprueft) => new MeldeSchaden(e.LeserId, e.LeserId));
        mahnung.BeiAbgelehnt().Sende<MeldeSchaden>(a => new MeldeSchaden(Guid.Empty, Guid.Empty));      // ✕-Port: Ablehnung
    });
}

// Warten am Strom (Frist als Rennen): das erste Folge-Event im Stream der Quelle — oder ⏳
var ausgeliehen = p.Auf<Ausgeliehen>();
var rueckgabe   = ausgeliehen.Strom().Warte<Zurueckgegeben>().Zeitlimit(TimeSpan.FromDays(28));
p.Alle(rueckgabe.BeiZeitlimit(), ausgeliehen.Strom()).Sende<EroeffneMahnung>((t, strom) => new EroeffneMahnung(strom.Id, 0m));
```
Verben (`Abstractions/Fluss/Pipeline.cs`): Einstieg `p.Quelle<T>()` / `p.Auf<E>()`; am Draht `.Rufe<F>(…)`, `.Sende<C>(…)`, `.Je(…)`, `.Strom()`;
`p.Alle(…)` (∧), `.Oder(…)` (∨) an Ruf- und Sende-Knoten; `.Zeitlimit(t)`; Ausgänge `Bei<E>()`, `BeiZeitlimit()`, `BeiAbgelehnt()` (Ablehnung des
Aggregats bzw. gescheiterte Funktion); im Je-Rahmen `Sammle(…)`; am Strom `Warte<A[, B …]>()`. **Build** (`FlussAnalyzer`): CQRS073 (genau eine
Quelle), CQRS074 (Warten mit Zeitlimit), CQRS075 (`BeiZeitlimit()` nur mit Zeitlimit), CQRS076 (`Bei<E>()` nur mit einem Ausgang des Knotens),
CQRS077 (∧ wartet auf einen Weg, der nicht immer liefert, Warnung), CQRS078 (`Sende<C>` ohne Decider); dazu Compiler und Würfe beim Definieren;
GR-AUS-FLUSS, GR-FLUSS-QUELLE/-NAME/-DRAHT/-WARTE/-ZEITLIMIT/-JE/-WARTET. **Generiert / Laufzeit:** der `ProzessRegelnGenerator` übersetzt den Fluss
in Regeln mit Knoten-Herkunft (Knoten-Namen aus dem Quelltext, Spur-Wirker `fluss:P#knoten`); der Dirigent (Prozess-Manager, ein Actor je Vorgang)
führt ihn mit demselben Belegungs-Kern aus (`FlussBelegung`); jede Quell-Nachricht startet einen Vorgang. Je Auslöser gibt es einen Ablauf
(Boot-Guard Auslöser-Kollision). **Mehr:** [konzept-editor-pipelines §14](konzept-editor-pipelines.md), [04 §4.6](04-konsum-und-prozess-maschine.md).

## 10.6a Quelle (+ Webhook)

**Zweck:** ein Weg von außen in einen Fluss. Eine Quelle ist nur ein Typ; der Treiber ist fest codiert und liest nur, die Werte stehen in der
Umgebung (W8). **Werkbank:** Palette „+ 📥 Quelle"; die Quellkarte zeigt „Werte in <Umgebung>" (bearbeitet im 🖥 Host-Rahmen, Abschnitt Quellen)
und die Bindung „⇥ Webhook /… (Umgebung)"; Durchspielen per Datei-Drop auf die Karte. **Code** (Sonde `Fluss.cs.txt`):
`public sealed record AusweisAbgelegt : DateiGemeldet;` — der Basistyp wählt den Treiber (`DateiGemeldet` = Datei-Treiber, befüllt Name, Pfad,
RelativerPfad, Groesse, GeaendertAm, ErkanntAm, Kennung). Umgebung:
```json
"Quellen": { "AusweisAbgelegt": { "Pfad": "${env:HOME}/cqrs-sim/${branch}/AusweisAbgelegt", "Muster": ["*.pdf"],
             "Fertig": { "Art": "Stabil", "Fenster": "00:00:01" }, "Kennung": "PfadGroesseZeit", "Laufmodus": "EinmalImCluster",
             "Webhook": "/webhook/ausweis" } }
```
Weitere Felder: `Unterordner`, `Erkennung` (`Abfrage` | `EreignisUndAbfrage`), `Intervall`, `Fertig.Art` `Marker` mit `Endung`, `Kennung`
`NameGroesse`, `Laufmodus` `JederKnoten`. **Webhook als Konfig:** mit `Webhook` mappt `LaufeAsync` die Route auf dem Einblick-Port — `POST` mit der
Nachricht als JSON (Feldnamen wie der Typ, gelesen über den Wire-Kontext) → `202 Accepted` mit Vorgangs-Id; dieselbe Nachricht zweimal = derselbe
Vorgang; ohne Einblick-Port ist ein Webhook ein Boot-Fehler. Ein `IQuellNachricht` ohne Treiber-Basis (eigene `Kennung`, wie `RueckgabeGescannt`
oben) bleibt für Test-Quellen erlaubt, die direkt über `QuellEingang` melden. **Build:** CQRS070 (sealed record ohne eigene Member); Boot: jede
Treiber-Quelle braucht ihren Abschnitt, unbekannte Felder sind Fehler, Webhook-Routen eindeutig; GR-QUELLE. **Generiert / Laufzeit:**
`GeneratedQuellen` (Fabrik je Quelle); `AddCqrs` bindet sie. Der Treiber gleicht beim Start gebündelt mit dem Log ab und meldet nacheinander; jede
Nachricht wird **genau einmal je Kennung durabel ins Log** geschrieben (deterministische Stream-Id, Spur-Wirker `quelle:T`); ein unlesbares
Verzeichnis macht `/health` „degraded". **Mehr:** [konzept-werkbank §3](konzept-werkbank.md).

## 10.7 Akteur

**Zweck:** ein Domänen-Experte, von dem alles kommt, was hineingeht; er deklariert nur, was er **selbst** hineingibt. **Werkbank:** Palette
„+ 👤 Akteur"; Befugnisse per ⊕ (Geste schreibt die Basisliste, auch Entziehen); das Board ordnet Rahmen je Domäne × Akteur. **Code** (Sonde
`Lesen.cs.txt`): `public sealed record Theke : IAkteur, IDarf<LeiheAus>, IDarf<GibZurueck>, IDarf<OffeneAusleihen>;` ·
`public sealed record Gutachterin : IKi, IDarf<EroeffneMahnung>;` — Marker `IAkteur` oder genauer `IMensch`/`IMaschine`/`IKi`. `IDarf<T>` gilt für einen Command, eine
Query, eine Quell-Nachricht oder eine Funktion (`IDarf<IFunktion-Typ>` = darf sie als externer Worker rechnen). Was ein Prozess, Fluss oder eine
Frist daraus erzeugt, trägt den Akteur der Kette (abgeleitet, kein `IDarf`). **Build:** CQRS058 (Befugnis nur für Hineingegebenes, nur an einem
Akteur, ein Dienst ist kein Akteur), CQRS059 (Namens-Kollision); GR-AKTEUR. **Generiert / Laufzeit:** `GeneratedAkteurRechte`. Das Akteur-Tor
erkennt den Akteur am Token (Header `akteur-token`, Abschnitt `Identitaeten` der Umgebung) und prüft jeden Command und jede Query; die Kette reist
im Event-Header `akteur`. Ohne `Identitaeten` ist das Tor offen (Prod-Identitäten fehlen, docs/13 P1-2). **Mehr:**
[konzept-akteure §2, §5](konzept-akteure.md).

## 10.7a Akteur-Vertrag (Zusage / Kenntnis)

**Zweck:** was ein Akteur **draußen** auf Events tut. `Auf(E)` mit Ausgabe ist eine **Zusage**, `void Auf(E)` eine **Kenntnis**. **Werkbank:**
📜-Rahmen am Akteur mit Zusage-Karten (anlegen per „＋", Event per ⊕); die Zusage zeigt „getragen von" (Clients) und den Python-Rumpf der
Client-Implementierung (Slot `zusage|Client|Event@Klasse`, ✎/🤖). **Code:**
```csharp
public sealed record Archiv : IMaschine;
public interface IRegal : IAkteurVertrag<Archiv>
{
    MeldeSchaden Auf(Zurueckgegeben e);                                         // Zusage, ein Command
    IAsyncEnumerable<OneOf<EroeffneMahnung, MeldeSchaden>> Auf(SchadenGemeldet e); // Zusage als Strom
    void Auf(Ausgeliehen e);                                                    // Kenntnis
}
```
Ein Vertrag darf in Teile zerfallen (mehrere `IAkteurVertrag<A>` je Akteur). **Build:** CQRS061 (Form: Interface, nur `Auf(TEvent)`, je Event
einmal), CQRS062 (Ausgabe konkrete Commands), CQRS087 (Zusage, die kein Client trägt, Warnung); GR-VERTRAG, GR-GETRAGEN. **Generiert / Laufzeit:**
Kanon + Hash je Vertrag; Python-Basis in `generated/vertraege.py`. Am Handshake prüft der Server Vertrag und Hash; Commands aus einer Zusage
bekommen eine deterministische Command-Id (Modus „emittiert", genau einmal wirksam). Die Zustellung an Clients ist heute verlierbar (docs/13
AK-1). **Mehr:** [konzept-akteure §3, §5](konzept-akteure.md).

## 10.8 Client-Vertrag (+ `IstClient`)

**Zweck:** die Software an **einer** Leitung: welche Vertrags-Teile sie trägt (auch mehrerer Akteure), was sie sendet und fragt, was sie selbst zur
Kenntnis nimmt. Wirksam ist Vertrag ∩ Token. **Werkbank:** Palette „+ 🔌 Client"; 🔌-Rahmen mit Anschlussleiste; Basisliste per ⊕; „+ Python-
Implementierung" (⟳ legt sie aus der generierten Basis an). **Code** (Sonde `Lesen.cs.txt`):
```csharp
public interface ILesesaal : IClientVertrag, IRegal, ISendet<LeiheAus>, IFragt<OffeneAusleihen>
{
    void Auf(MahnungEroeffnet e);                                               // eigene Kenntnis des Clients
}
```
Welche Akteure ein Client verkörpert, ist abgeleitet (`Abstractions.Akteurvertrag.Verkoerpert`). **Build:** CQRS063 (Form), CQRS064 (sendet/fragt
nur, was ein Akteur per `IDarf` darf), CQRS065 (je Event höchstens eine Methode, sonst wäre `auf_<event>` mehrdeutig); GR-CLIENT, GR-CLIENT-BEFUGT. **Generiert / Laufzeit:** `GeneratedClientVertraege`
(Server) und dieselbe Lesart (`Clientvertragslesart`) im Client-Generator; der Handshake lehnt ein Token ab, das die Akteure des Vertrags nicht
verkörpert. Verbindung je Umgebung aus `Clients:<Name>` (Token, Adresse, Urls, Dateien).
- **Python-Client:** die Implementierung erbt die generierte `<Client>Basis` aus `domain_client/generated/vertraege.py` und füllt je Event
  `auf_<event>` (Zusage: `yield` der Commands; Kenntnis: `None`); Start z. B. `client.run(host, port)` (Muster `run_stub.py`, Beispieldomäne). Mehr: [09](09-python-sdk.md).
- **C#-Client (Blazor):** `[assembly: IstClient<IArbeitsplatz>]` (Beispieldomäne, `Domain.Client.Modules.Blazor/Arbeitsplatz.cs`) — Name, Hash, Abos,
  Commands und Queries kommen aus dem Vertrag; der Code wird dagegen gehalten: CQRS093 (Client ohne Vertrag), CQRS094 (C#-Client trägt keine
  Vertrags-Teile), CQRS095/096 (Command/Query nicht im Vertrag), CQRS097 (gehörtes Event nicht im Vertrag); GR-CLIENT-CODE. Module und Views:
  [08](08-frontend-blazor-client.md).

**Mehr:** [konzept-akteure §4](konzept-akteure.md).

## 10.9 Umgebung

**Zweck:** alles, was zwischen Umgebungen verschieden ist — Auswahl und Werte, nie Struktur (W7). Je Umgebung eine Datei
`umgebungen/umgebung.<name>.json`; das Schema sind die Typen in `Abstractions/Umgebung/UmgebungKonfig.cs` (Abschnitte `UmgebungsAbschnitte`);
ein unbekannter Abschnitt oder ein unbekanntes Feld ist ein Startfehler. **Werkbank:** Umgebung in der Kopfzeile wählen; 🖥 Host-Rahmen = je
Abschnitt eine Karte, gezeichnet von einem generischen Formular-Renderer aus dem Schema; Werte werden direkt geschrieben (keine Geste).
**Code:** `Host.Grpc/Program.cs` ist nur `var umgebung = builder.AddCqrs(args); … await app.LaufeAsync(umgebung);` — Prozesse, Flüsse, Projektionen,
Quellen, Funktionen und Dienste bindet `AddCqrs` aus den generierten Tabellen; es gibt keine Domänen-Zeile im Programm. Start:
`dotnet run --project Host.Grpc -- --umgebung sim-main`. Auszug (neutral):
```json
{
  "Umgebung":     { "Name": "sim-main", "Art": "Sim" },
  "Transport":    { "GrpcPort": 5101, "EinblickPort": 5102, "JsonEingang": true },
  "Identitaeten": { "Tokens": { "sim-theke": "Theke", "sim-lesesaal": "Archiv,Theke" } },
  "Funktionen":   { "ISchadensSchaetzung": { "Laufort": "Host", "Slots": 2, "Wiederholungen": 1 } },
  "Dienste":      { "IWertTabelle": { "Implementierung": "LineareWertTabelle" } },
  "Konfig":       { "WertTabellenKonfig": { "Mindestwert": 5 } },
  "Clients":      { "Lesesaal": { "Token": "sim-lesesaal" } }
}
```
| Abschnitt | Inhalt |
|---|---|
| `Umgebung`, `EventStore`, `VersionIndex`, `Cluster`, `Schreibpfad`, `Transport` | Betrieb: Art `Sim`/`Prod`, Datenbank, Redis, Einzelknoten/Consul, Group-Commit, Ports, JSON-Eingang |
| `Identitaeten` | `Tokens` (Token → Akteur-Menge), `Standard`, `VertragStreng` — §10.7 |
| `Quellen` | je Quelle die Treiber-Konfig, optional `Webhook` — §10.6a |
| `Funktionen` | je Funktion Laufort, Implementierung, Slots, Wiederholungen, Worker — §10.5a |
| `Dienste`, `Konfig` | Implementierung je Dienst, Werte je Konfig-Record — §10.5b, §10.5c |
| `Clients` | je Client Token, Adresse, Urls, Dateien — §10.8 |
| `Logging` | ASP.NET-Standard |

Platzhalter `${branch}` (aus `CQRS_BRANCH`) und `${env:NAME}`; Geheimnisse nur als Verweis. Das Umgebungs-Siegel verhindert, dass ein Sim-Host in
eine Prod-DB schreibt. **Mehr:** [konzept-werkbank §4](konzept-werkbank.md), [06 §6.1, §6.5](06-transport-multinode-betrieb.md).

## 10.10 Ausprobieren

Ausprobiert wird das echte System, keine Simulation (W6). **⟳** in der Kopfzeile baut (`-p:CqrsStand`, Codegen + alle Generatoren und Analyzer),
legt fehlende Python-Implementierungen an, liest neu, prüft die Sim-DB gegen das neue Proto (bei Unverträglichkeit Rückfrage „zurücksetzen?"),
startet fehlende Infrastruktur und den Host in der gewählten Umgebung; Fehler stehen an der Karte (Compiler, CQRS…, `CQRS-BOOT`). **Durchspielen**
nur über echte Eingänge: Datei-Drop auf die Quellkarte, Webhook von außen, Konsole als Akteur (Command/Query über den JSON-Eingang), ⧗ Aufträge
einer Funktion mit Laufort `Hand`, „▶ Worker" für `Extern`, ⏩ Uhr vor. Die **Spur** (Header `ursache`/`wirker`/`ueber`/`stand` an jedem
Log-Eintrag) zeigt den Weg über die Karten: ☰ Vorgänge, ⏱ Zeitleiste, ⚠ Meldungen, 〰 Puls, ein Fokus. 📸 speichert und lädt Sim-DB-Stände.
**Mehr:** [konzept-werkbank §5–§7](konzept-werkbank.md).

## 10.11 Testen

Drei Ebenen: **Prüfstand** (in-memory, store-frei) — ein Aggregat über die generierte `AggregateHandlerFactory` treiben, Analyzer und Generatoren
über eine eigene `CSharpCompilation`, Gesten gegen eine Solution im Speicher; Test-DSL `Cqrs.Testing` (`Szenario.Für<T>(…)`, `SagaSzenario`,
`FlussLaufwerk` mit demselben Belegungs-Kern wie der Dirigent). **Integration** — Store-Semantik nur gegen echtes Marten/Redis/Consul, sequentiell;
es gibt keinen `InMemoryEventStore`, nie faken, was man nicht besitzt. **Editor-Parität** — `dotnet run --project GraphExtractor -- --check`
(„Code ohne Karte: 0") und `--sonde`. Neues Konstrukt im Extractor ⇒ Sonde und `soll.txt` erweitern. Vorlagen, Befehle und Fallstricke:
[12 §12.6–§12.7](12-tests-und-vermessung.md).

## 10.12 Die Reinheits-Grenze

Bewusst unsichtbar im Fachcode (Framework oder Generat): `Id`/`Version`, OCC, Signal, Cursor, Exactly-once/Dedup (`KommandoVerarbeitet`-Marken),
Sharding, Prozess-Maschinerie, Serialisierung, Proto-DTOs, ClusterKinds, DI-Wiring, Spur-Header. Taucht eines davon im Fachcode auf, ist etwas falsch
verdrahtet (Inv. 5). Bekannte Reibung: `ProjectionWriter.Execute`/`ctx.Track` stehen heute im Projektions-Handle.

## 10.13 Altlasten — nicht für Neues verwenden

Noch im Code, ohne Domänen-Nutzer und ohne Rolle im Editor; Rückbau offen (docs/13 W11-1). Analyzer-Zweige und Tests kennen sie noch.

| Altlast | Heute stattdessen |
|---|---|
| Handle-Pipeline `IPipelineHandler` (`PipelineId`, `Handle(…, PipelineContext)`, Pipeline-Actor, CQRS056) | Fluss (§10.6) + Katalog-Funktion (§10.5a) |
| Trigger-Welt `IPipelineTrigger`, `ITriggerRegistration`, `TimerTrigger`, `WebhookTrigger` | Quelle mit Treiber + Konfig, Webhook als Konfig (§10.6a) |
| `[Ingress]` | Quelle; Webhook-Route in der Umgebung |
| `Selbst<T>`, `Frist<TCmd>`, `FristStorno<TCmd>` als Handle-Ausgang (Fristplaner, `GeneratedFristen`) | `.Zeitlimit(t)` bzw. `Strom().Warte<…>().Zeitlimit(t)` im Fluss/Prozess (Fristplaner an der Handle-Welt: docs/13 W11-2) |
| `IAkteurDienst<A>` als Handle-Parameter (CQRS060) | Domänen-Dienst (§10.5b) hinter einer Funktion; Akteur über `IDarf` |

## 10.14 Upcast (Prod, nicht im Editor)

`IUpcast` ist eine Backend-Primitive für die Prod-Datenbank: ändert sich die Gestalt eines persistierten Events, wird die alte Byte-Form beim
**Lesen** übersetzt, nicht migriert. Im Editor gibt es keine Schema-Evolution (W12) — in Sim fragt ⟳ bei unverträglicher Event-Form, ob die
Sim-DB zurückgesetzt wird ([konzept-werkbank §4.6](konzept-werkbank.md)). **Code:** die alte Gestalt als Record behalten, eine Kante zur nächsten:
```csharp
public record Ausgeliehen_V1(Signatur Medium, Guid LeserId);                          // frühere Gestalt: schlichter Record, nur lesbar
public sealed class AusgeliehenUpcast : IUpcast<Ausgeliehen_V1, Ausgeliehen>
{
    public Ausgeliehen Wandle(Ausgeliehen_V1 alt) => new(alt.Medium, alt.LeserId, 14);   // rein, deterministisch, ohne I/O
}
```
Frühere vs. aktuelle Version leitet der Generator aus den Kanten ab, ebenso Kette und Speicher-Diskriminator (alte Bytes behalten ihren Namen).
**Build:** CQRS040 (mehrdeutige Kante), CQRS041 (Kette endet nicht an einem aktuellen Event), CQRS042 (Zyklus), CQRS044 (Merge), CQRS045
(Split-Sekundärziel mit eigener Kette), CQRS046 (Split 1:N noch blockiert). **Generiert / Laufzeit:** `GeneratedEventUpcasting` (Kette,
Diskriminator, Marten-Registrierung), angewandt im Read-Pfad des Event-Stores. **Mehr:** [03 §3.3](03-schreibseite.md).
