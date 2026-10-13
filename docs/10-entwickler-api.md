# 10 — Entwickler-API: „Wie schreibe ich X?"

Praktischer Leitfaden aus Entwicklersicht. Grundregel überall: **du schreibst nur Fachcode;
alles Dispatchende, Verdrahtende und Serialisierende wird generiert.** Marker-Interfaces sind
der primäre Auslöser — es gibt fast keine Attribute.

## 10.1 Trigger-Matrix (was löst welchen Generator aus?)

| Du schreibst… | …und es entsteht (generiert) |
|---|---|
| `class X : IState` + nested `Decider : IDecider<X>` + `Applier : IApplier<X>` | State-Properties (Id/Version/State), AggregateHandler, Factory-Eintrag, Actor, ClusterKind, Snapshot-Registrierung |
| `record E : IEvent` | Signal `StateChangeViaE`, EventJson-Pfad, Wire-Payload, Typ-Registry, Proto-DTO |
| `record E : IEvent, ITransientEvent` | wie Event, **aber kein Signal, kein Persist, kein Upcasting** |
| `record C : ICommand` | Command-Routing (via Decider-Signatur), Wire-Payload, Proto-DTO |
| `class : IProzessDefinition { … Prozess<E>.Definiere(…) }` | Regel-Registry-Eintrag + CQRS001/002/003-Prüfung |
| `class : ISubscriber` + `Handle(...)` | Subscriber-Dispatch, ProjectionQueryService |
| `class : ISubscriber, IPullSubscriber` | zusätzlich kompletter Pull-Pfad (Adapter-Kind, Store per DI) |
| `class : IPipelineHandler` + `Handle(...)` | Pipeline-Dispatch + Pipeline-Actor |
| `class : IReader<T>` + `Handle(...)` | Reader-Dispatch (Query-Routing) |
| `record V1 {}` + `class : IUpcast<V1, VAktuell>` | ganze Upcasting-Kette + Diskriminator + Marten-Registrierung (rein aus Typen) |
| `class Msg : IWireMessage` | Wire-Whitelist-Eintrag (cross-node) |
| Blazor: `IViewModel`, `IUiModule`, `Handle(T, MessageContext)` | Client-Dispatch, VM-Bus-Methoden, DI-Wiring |

Nur **zwei optionale Attribute**: `[AggregatName("…")]`, `[ProzessName("…")]` — entkoppeln die
persistierte Identität vom Klassennamen (Migrations-Stabilität). Kein `[Command]`/`[Event]`.

## 10.2 Ein neues Aggregat (Command + Event)

Siehe [03 §3.2](03-schreibseite.md) für das vollständige `Konto`-Beispiel. Kurz:
```csharp
public partial class Konto : IState { public decimal Saldo { get; set; } }
public record EroeffneKonto(Guid AggregateId, decimal Start) : ICommand, ICreationCommand;
public record KontoEroeffnet(decimal Start) : IEvent;
public partial class Konto {
  public partial class Decider : IDecider<Konto> {
    public IEnumerable<OneOf<KontoEroeffnet>> Decide(EroeffneKonto c) { yield return new KontoEroeffnet(c.Start); }
  }
  public partial class Applier : IApplier<Konto> {
    public void Apply(KontoEroeffnet e) => State.Saldo = e.Start;
  }
}
```
Build-Regeln (`ModellAnalyzer`, Werkbank P7.4): CQRS079 ein entschiedenes persistentes Event braucht sein `Apply` (sonst wirft das Falten),
CQRS080 (Warnung) ein gefaltetes Event, das das Aggregat in keinem Decide erzeugt, CQRS081 (Warnung) Enum-Wert 0 (geht auf dem Proto-Wire als
Default verloren). Über alle Domänen-Projekte (`GesamtbildAnalyzer` in der Laufzeit-Compilation, Meldung beginnt mit `'Typ'`): CQRS084 eine Query
beantwortet genau ein Reader, CQRS085 (Warnung) Event/Ablehnung/Response ohne Erzeuger, CQRS086 (Warnung) Command ohne Decider/Query ohne Reader,
CQRS087 (Warnung) Zusage, die kein Client trägt (sobald es Clients gibt), CQRS088 (Warnung) Kreis aus Projektionen/Reaktionen über transiente
Events. Für Konsumenten: CQRS082 (Warnung) Command ab einem transienten Eingang, CQRS083 (Warnung, Regel Z) Zustand in Instanzfeldern.

**⚠ Nach neuen Command/Event/Query/Trigger-Typen:** Proto regenerieren —
`dotnet run --project Proto.SourceGeneration` → ProtoRepo bauen → Infrastructure baut (Signale
sind ausgenommen). Sonst Build-Fehler (CQRS030 macht ihn laut).

## 10.3 Eine Projektion

`partial class X : ISubscriber, IPullSubscriber`, mit `SubscriberId` und **ohne Store im Konstruktor**. Jeder
`Handle` bekommt genau die Store-Funktionen, die er benutzen darf, als **Fähigkeits-Parameter** (→ automatisch
**replaybar**, weil der Store dahinter der Co-Commit-Tracker ist):
```csharp
// Fähigkeit = Interface mit GENAU EINER Funktion (CQRS051); das Bündel (IStore) ist der Store.
public interface IReserviereBetrag : IWriteStore { Task ReserviereAsync(Guid konto, decimal betrag); }
public interface IFindeKonto       : IReadStore  { Task<KontoSicht?> FindeAsync(Guid konto); }
public interface IKontoStore : IStore, IReserviereBetrag, IFindeKonto { }   // eine Klasse implementiert es

public async Task Handle(BetragReserviert evt, IAggregateEnvelope env, ProjectionWriter writer, IReserviereBetrag store) =>
    await writer.Execute(env.AggregateId, ctx => {
        ctx.Track<Konto>(env.AggregateId);
        return store.ReserviereAsync(env.AggregateId, evt.Betrag);
    });
```
Der generierte Dispatch löst die Fähigkeiten aus einem DI-Bereich auf (eine Store-Instanz je Bereich → Effekt +
Marke in einer Transaktion). Alles Weitere (Kind, Receiver, Poller, Achsen-Schnitt aus den Schreib-Fähigkeiten)
generiert `PullPathGenerator`; Schreib-Fähigkeiten aus zwei Stores in einer Klasse sind CQRS052. Ein Store im
Konstruktor/Feld eines Konsumenten ist CQRS054, `new …Store()` CQRS055. Optionaler GA-1-Marker `IAppendProjektion`
erzwingt einen Co-Commit-Tracker. Beispiel: `Domain.Projections/ImagePairProjection.cs`.

**Schreib-Regel im Store (`MartenCoCommitStoreBase`).** Die Maschine garantiert einen Schreiber je (Projektion, Stream);
das Co-Commit macht einen Stapel atomar, isoliert aber nicht zwei parallele Stapel verschiedener Streams. Deshalb schreibt
eine Store-Methode auf genau einem von drei Wegen, und die Basis prüft das beim Puffern:

| Dokument | Weg | Beispiel |
|---|---|---|
| eigenes (Schlüssel = Stream des Stapels) | `EnqueueStore(id, doc)`, `EnqueueTransform`, `EnqueueAnlegenOderAendern` | `TrainingslaufReadModel` |
| Zeile dieses Streams (Id enthält den Besitzer) | `EnqueueZeile(besitzer, zeile)`, `EnqueueZeileEntfernen` | `DatensatzMitgliedschaftZeile` `{Datensatz}:{Paar}` |
| geteilt (mehrere Streams, Marker `IGeteiltesReadModel`) | `EnqueueGeteilt(id, alt => neu)` — Versionsprüfung; Konflikt verwirft den Stapel samt Marke, der Adapter wiederholt | `AktivesModellReadModel` |

Ein fremdes Dokument über den ersten Weg wirft sofort (statt eines stillen Lost Updates). Wo möglich Zeilen statt eines
geteilten Dokuments; „neuer gewinnt“ in einem geteilten Dokument nach Event-Zeit, nie nach Ankunft (zwischen Streams gibt
es keine Ordnung). Belegt gegen echtes Postgres: `GeteilteDokumentePostgresTests`, `SchreibRegelPostgresTests`.

Am Puffer vorbei schreiben (der übliche Marten-Weg `LightweightSession() … SaveChangesAsync()`) ist ein Build-Fehler:
**CQRS066** erlaubt in einem Co-Commit-Store von der Datenzugriffs-Bibliothek nur `Store.QuerySession()` → `LoadAsync`/
`LoadManyAsync`/`Query` (+ `ToListAsync`/`CountAsync` …) und verbietet, einen Zugang an fremden Code weiterzureichen;
**CQRS067** trennt die Rollen (Schreib-Fähigkeit liest nicht, Lese-Fähigkeit puffert nicht). Die Positivliste steht EINMAL in
`Abstractions/StoreZugriff.cs` (Analyzer `Projections.SourceGeneration/StoreZugriffAnalyzer.cs` und LLM-Kontext lesen sie).

> Hinweis: der produktive `MartenProjectionTracker` committet Effekt und Marke aktuell in
> getrennten Sessions (at-least-once) — siehe [04 §4.3](04-konsum-und-prozess-maschine.md).

## 10.4 Eine Reaktion (Command emittieren)

Dieselben Marker (`ISubscriber, IPullSubscriber`), **keine** Schreib-Fähigkeit → automatisch
**emittierend**. `Handle` gibt `IAsyncEnumerable<OneOf<TCmd>>` zurück und `yield return`t
Commands:
```csharp
public async IAsyncEnumerable<OneOf<WirkeReaktion>> Handle(ImagePairKomplett evt, IAggregateEnvelope env) {
    yield return new WirkeReaktion(env.AggregateId);
}
```
Die Rückgabe nennt die möglichen Commands **als Typen** (`OneOf<…>` konkreter Typen; `IAsyncEnumerable<ICommand>` o. ä. ist
CQRS050, gilt ebenso für Decide, Reader und Pipeline). Nie selbst `RequestAsync` aufrufen (CQRS020) und nie `CancellationToken.None` auf eine
Command-Kante geben (CQRS021). Beispiel: `Domain.Projections/ImagePairReaktion.cs`.

## 10.5 Ein Prozess / eine Saga

`sealed class X : IProzessDefinition` mit `ProzessRegeln Regeln => Prozess<TAuslöser>.Definiere(…)`.
Das ist **alles** — kein Manager, kein Treiber, kein Korrelations-Code:
```csharp
public sealed class UeberweisungsProzess : IProzessDefinition {
  public ProzessRegeln Regeln => Prozess<UeberweisungBeauftragt>.Definiere(p => {
    p.Auf<UeberweisungBeauftragt>().Sende<ReserviereBetrag>(e => new ReserviereBetrag(e.Von, e.Betrag))
                                   .RückgängigDurch<GebeFrei>(e => new GebeFrei(e.Von, e.Betrag));
    p.Auf<BetragReserviert>().Sende<SchreibeGut>(…);
    // Diamant: .Und<E2>().Und<E3>() ;  Fan-out: .UndAlle<E>(n) + SendeJe<Cmd>
  });
}
```
Build-Guards: CQRS001 (ein Auslöser je Prozess), CQRS002 (jeder gesendete Command hat einen
Decider), CQRS003 (explizites `T`), CQRS012 (eindeutiger Name), Azyklizitäts-Boot-Guard. Der
Host ruft einmalig `AddGeneratedProzesse()`. Beispiele: `Domain/Ueberweisung/`,
`Domain/Reiseauftrag/` (Diamant + Kompensation), `Domain/Sammelueberweisung/` (Fan-out).

## 10.5a Eine Katalog-Funktion rufen (`Rufe<F>`)

Eine **Katalog-Funktion** ist eine Schnittstelle mit genau einer Methode: ein Auftrag hinein, OneOf-Ergebnis-Events heraus.
Ein Prozess ruft sie mit `Rufe<F>` so, wie er ein Aggregat mit `Sende<Cmd>` ruft. Ob sie 50 ms oder Stunden braucht, ist nur
ein Zeitlimit, keine eigene Architektur.
```csharp
public sealed record SchaetzeSchaden(Guid LeserId, decimal Neuwert) : IAuftrag<ISchadensSchaetzung>;   // der EINE Eingang
public record SchadenGeschaetzt(decimal Betrag) : IEvent;                                               // Ergebnisse = persistente Events
public record NichtSchaetzbar(string Grund) : IEvent;

public interface ISchadensSchaetzung : IFunktion
{
    Task<OneOf<SchadenGeschaetzt, NichtSchaetzbar>> RufeAsync(SchaetzeSchaden auftrag, IAusfuehrung x);
}

// im Prozess: der Aufruf-Knoten — die Funktion steht ausdrücklich im Typ-Argument (CQRS003)
p.Auf<SchadenGemeldet>().Rufe<ISchadensSchaetzung>(s => new SchaetzeSchaden(s.LeserId, 25m)).Zeitlimit(TimeSpan.FromMinutes(5));
p.Auf<SchadenGeschaetzt>().Sende<EroeffneMahnung>(g => new EroeffneMahnung(Guid.NewGuid(), g.Betrag));
```
Die Implementierung ist eine Klasse, die das Interface implementiert (Code-Fakt); den Laufort wählt die Umgebung
(`Funktionen:ISchadensSchaetzung` = `{ "Laufort": "Host", "Slots": 2, "Wiederholungen": 1 }`, bei mehreren Implementierungen mit
`"Implementierung"`). Gebaut wird sie über eine generierte Fabrik (`GeneratedFunktionen.Implementierungen`, Konstruktor direkt, ohne
Reflection) — was ihr Konstruktor braucht (Dienste, Konfig-Records), bindet die Dienst-Bindung (§10.5b).

Was passiert: Der Prozess-Manager übergibt **alle** offenen Aufträge (parallel, auch bei jeder weiteren Weckung — der Ausführer
dedupliziert über die Ausführungs-Id = Vorgang) an den `FunktionsAusfuehrer`. Der rechnet im Slot und schreibt **genau ein**
Ergebnis in den Ausführungs-Stream (Stream-Id = Vorgang, `CausationId` = Vorgang): den OneOf-Fall als Event, bei einer Ausnahme
nach den Wiederholungen die `KommandoAbgelehnt`-Marke. Der Fold liest es wie das Event eines Aggregats — ein Ergebnis aktiviert
`Auf<Ergebnis>`, eine Marke wird `SchrittGescheitert` → Kompensation. **Zeitlimit** (für Command UND Funktion): ab dem jüngsten
gematchten Event (DB-Zeit); läuft es ab, scheitert der Schritt („Zeitlimit"), der §3-Backstop weckt ohnehin periodisch.

Regeln: CQRS068 (feste Form), CQRS069 (ein Auftrag je Funktion), CQRS003 (`Rufe` mit explizitem Typ), Boot-Guard
„jede gerufene Funktion ist gebunden", Azyklizitäts-Guard (Funktion → Ergebnisse aus der Signatur). Kein `Zeitlimit` hinter
`UndAlle`, kein `Rufe` mit Fan-out (Validator). Ergebnis-Events sind normale Domänen-Typen: Proto regenerieren wie bei jedem
neuen Event. Beispiel: Sonde `GraphExtractor.Kern/Sonde/Gebuehren.cs.txt`; Laufzeit-Beweis `Infrastructure.Pruefstand.Tests/Funktionen/`.

## 10.5b Ein Domänen-Dienst (`IDienst`) hinter einer Funktion

Technik, die eine Funktions-Implementierung benutzt (Bild skalieren, ein Modell laden …), ist ein **Domänen-Dienst**: ein Interface mit dem
Marker `IDienst`, jede öffentliche Klasse, die es implementiert, ist eine Implementierung. Die Funktions-Implementierung nimmt ihn **im
Konstruktor** — zusammen mit Konfig-Records (Werte aus der Umgebung) und `ILogger<T>`:
```csharp
public interface IImageResizer : IDienst { Mat Resize(Mat input, int targetHeight); }
public sealed class ImageResizer : IImageResizer { public Mat Resize(Mat input, int targetHeight) { … } }

public record PreprocessingConfig(string OutputPath);                       // Konfig-Record: Werte aus Konfig:PreprocessingConfig

public sealed class OpenCvBildVerkleinerung(IImageResizer resizer, PreprocessingConfig config) : IBildVerkleinerung { … }
```
Umgebung (`umgebungen/umgebung.<name>.json`) — keine Zeile im Programm:
```json
"Dienste": { "IImageResizer": { "Implementierung": "ImageResizer" } },     // nur nötig, wenn es mehrere gibt
"Konfig":  { "PreprocessingConfig": { "OutputPath": "${env:HOME}/cqrs-sim/${branch}/preprocessed" } }
```
Generiert (`DienstGenerator` → `GeneratedDienste`): je Dienst seine Implementierungen mit Fabrik, je Implementierung (Funktion oder Dienst) was
ihr Konstruktor braucht, je Konfig-Record die Bindung Feld für Feld (Text, Zahl, Wahrheitswert, Dauer, Guid, Enum, Text-Liste; ein Default im
Record gilt, wenn das Feld fehlt; unbekannte Felder sind ein Fehler). `AddCqrs` bindet nach den Funktionen genau das, was die gebundenen
Funktions-Implementierungen transitiv brauchen. Boot-Fehler (mit Karte `dienst:<IX>` bzw. `host:Konfig`): keine Implementierung, mehrere
ohne Wahl, unbekannter Name, Kreis über Dienste, fehlender oder fremder Konfig-Abschnitt.

Regeln im Build (`Domain.SourceGeneration/DienstAnalyzer.cs`): CQRS089 ein Dienst nur im Konstruktor einer Funktions-/Dienst-Implementierung
(nie als Handle-/Decide-Parameter, Feld oder Eigenschaft im Fachcode — dort sind es Fähigkeiten), CQRS090 der Konstruktor nimmt nur Dienste,
Konfig-Records und `ILogger<T>`, CQRS091 Konfig-Felder sind bindbar, CQRS092 die Implementierung ist öffentlich und nicht generisch.
Abgrenzung: `IAkteurDienst<A>` gehört einem Akteur und ist nur Handle-Parameter (CQRS060) — etwas anderes.

Im Editor: Dienst-Karte 🧩 (Signatur = Methoden, Implementierungen mit Konstruktor, Rümpfe aus der Datei mit ✎), an der Funktion „🧩 braucht
(Konstruktor)" mit ⊕ bzw. „＋" (neuer Dienst als eigene Karte); „＋ C#-Implementierung anlegen" an einer Funktion ohne Implementierung; die Wahl
je Umgebung im 🖥 Host-Rahmen („🧩 Dienste", „⚙ Konfig" mit den Feldern aus dem Code). Beispiel: Sonde `Gebuehren.cs.txt` (`IWertTabelle`).

## 10.6 Eine Pipeline als Fluss (Quelle → Funktionen → Commands)

Kanonisch seit 2026-10-07 (`docs/konzept-editor-pipelines.md` §14). Eine Pipeline ist eine Klasse `: IPipeline`, deren Knoten vom
Draht aus verdrahtet werden. Der Variablenname ist die Identität des Knotens (dieselbe Funktion darf mehrfach vorkommen).
```csharp
public sealed class Bildeingang : IPipeline
{
    public PipelineFluss Fluss => PipelineFluss.Definiere(p =>
    {
        var datei    = p.Quelle<DateiErkannt>();                                   // oder p.Auf<Event>() — Event aus dem Log
        var deuten   = datei.Rufe<IDateinameDeutung>(datei => new DeuteDateiname(datei.Dateiname, datei.Pfad));
        var vorschau = deuten.Bei<ImagePairDateiGedeutet>().Rufe<IBildVerkleinerung>(deuten => new VerkleinereBild(deuten.Pfad, 512))
            .Zeitlimit(TimeSpan.FromMinutes(5));                                    // ⏳-Port: vorschau.BeiZeitlimit()
        var paar     = p.Alle(deuten.Bei<ImagePairDateiGedeutet>(), datei)          // ∧ (bis 4 Drähte), Kontext = ein Vorgänger
            .Sende<ErstelleImagePair>((deuten, datei) => new ErstelleImagePair(…));
        // ∨: .Oder(paar.BeiAbgelehnt(), …) — ✕-Port = Ablehnung des Aggregats bzw. gescheiterte Funktion
        // Je:  var je = x.Bei<Zerlegt>().Je(z => z.Bilder); je.Rufe<F>(b => …); je.Sammle(f.Bei<E>()).Sende<C>((z, liste) => …);
    });
}
```
Eine Quelle ist ein fest codierter **Treiber** plus Konfig (docs/konzept-werkbank.md §3): die Domäne benennt sie nur als Typ —
`public sealed record DateiErkannt : DateiGemeldet;` (CQRS070: sealed record, keine eigenen Member). Der Basistyp wählt den Treiber
(`DateiGemeldet` = Datei-Treiber, befüllt Name, Pfad, RelativerPfad, Groesse, GeaendertAm, ErkanntAm, Kennung); der `QuellenGenerator` erzeugt
`GeneratedQuellen` (Fabrik ohne Reflection), der Host bindet alles mit `services.AddQuellen(configuration)`. Die Werte stehen im Abschnitt
`Quellen:<Typ>` (Pfad, Muster, Unterordner, Erkennung `Abfrage|EreignisUndAbfrage`, Intervall, Fertig `Stabil|Marker`, Kennung
`PfadGroesseZeit|NameGroesse`, Laufmodus `EinmalImCluster|JederKnoten`); unbekannte Felder und Abschnitte ohne Typ sind Start-Fehler. Der
Treiber liest nur, gleicht beim Start gebündelt mit dem Log ab und meldet nacheinander (Rückstau); ein unlesbares Verzeichnis macht `/health`
„degraded". Ein zweiter Weg in dieselbe Quelle ist der **Webhook als Konfig** (Plan E12): `"Quellen": { "DateiErkannt": { …, "Webhook": "/webhook/datei" } }`
— `LaufeAsync` mappt ihn auf dem Einblick-Port (`Transport:EinblickPort`, HTTP/1.1; ohne Port ist ein Webhook ein Boot-Fehler): `POST` mit der
Nachricht als JSON (Feldnamen wie der Typ, gelesen über den Wire-Kontext) → `202 Accepted` mit der Vorgangs-Id, dieselbe Nachricht zweimal = derselbe
Vorgang, ein falscher Body = `400`. Im Programm steht dafür nichts. Ein `IQuellNachricht` ohne Treiber-Basis bleibt erlaubt (Test-Quellen, die
Tests direkt über `QuellEingang` melden); einen Webhook hat nur eine Quelle mit Konfig-Abschnitt.
Jede Nachricht wird genau einmal je Kennung ins Log geschrieben und startet je Pipeline einen Vorgang. Funktionen bindet der Host wie
in §10.5a; eine Funktion, die ein Python-Worker rechnet, mit Laufort `Extern` (§10.9; der Worker erbt die generierte
`<Funktion>Basis` aus `domain_client/generated/funktionen.py`, bietet sie am Handshake an und holt Aufträge per Pull). Pipelines
liegen in der Domain-Assembly (Registry wie bei Prozessen). Beispiel: `Domain/ImagePair/Bildeingang.cs`; Beweise:
`Infrastructure.Pruefstand.Tests/Funktionen/PipelineFlussTests.cs`, `BildeingangTests.cs`.
Build-Regeln des Flusses (`FlussAnalyzer`, Werkbank P7.4 — vorher erst beim Start bzw. nur im Editor): CQRS073 genau eine Quelle, CQRS074 Warten
mit Zeitlimit, CQRS075 `BeiZeitlimit()` nur an einem Knoten mit Zeitlimit, CQRS076 `Bei<E>()` nur mit einem Ausgang des Knotens (Ablehnung am
✕-Port: `BeiAbgelehnt()`; ein Warten hat keinen), CQRS077 (Warnung) ∧ wartet auf einen Weg, der nicht immer liefert, CQRS078 `Sende<C>` ohne Decider.

## 10.6b Eine Pipeline im Handle-Modell (Bestand)

`partial class X : IPipelineHandler` mit `PipelineId` + `Handle(TTrigger, PipelineContext)` /
`Handle(TEvent, PipelineContext)` → `IAsyncEnumerable<OneOf<Cmd…, Trigger…>>` (CQRS050: die möglichen Ausgaben als
Typen, nie `ICommand`) oder `Task` für reine Effekte. Lese-Fähigkeiten als Parameter wie beim Reader. **Planen ist
ein Ausgang, kein Aufruf:** `Selbst<T>` (Selbst-Nachricht nach Verzögerung, erste über `Handle(PipelineGestartet, ctx)`),
`Frist<TCmd>` (durable Frist → `TCmd` an die Ziel-Id; der Router ist generiert, `TCmd` braucht einen Ctor `(Guid)`,
sonst CQRS056; fällig = Log-Zeit des auslösenden Events + Dauer, ohne Event die DB-Uhr — ein erneutes Lesen desselben Events
verschiebt sie nicht) und `FristStorno<TCmd>`:
```csharp
public IEnumerable<OneOf<Frist<MarkiereAlsHaengengeblieben>>> Handle(TrainingBegonnen evt, PipelineContext ctx)
{ yield return new Frist<MarkiereAlsHaengengeblieben>(ctx.SourceAggregateId!.Value, _config.Timeout); }
```
Persistierte Events laufen über den Pull-Pfad (dort kein `Selbst<T>` — keine Mailbox), transiente über den Broker
(P6.1/P6.2). Je PipelineId gibt es genau eine Aktivierung im Cluster; ihr Zustand (Felder) ist nur auf dem Trigger-/Selbst-Kanal
seriell — Event-Handles laufen im Pull-Actor auf demselben Singleton-Handler. Beispiele: `Domain.Pipeline/Trainingslauf/TrainingFristPipeline.cs`, `Domain.Pipeline/Datensatz/DatensatzResolverPipeline.cs` (noch nicht als Fluss umgezogen).

## 10.7 Schema-Evolution (Upcasting)

Alte Version als Record behalten, eine `IUpcast`-Kante zur neuen Gestalt deklarieren:
```csharp
public record LagerEingerichtet_v1(int Bestand) : IEvent;          // alt, nur lesbar
public record LagerEingerichtet(int AnfangsBestand) : IEvent;      // aktuell
public class LagerUpcast : IUpcast<LagerEingerichtet_v1, LagerEingerichtet> {
    public LagerEingerichtet Aufwerten(LagerEingerichtet_v1 alt) => new(alt.Bestand);
}
```
Der Generator folgert Richtung + Diskriminator (`_v2`) aus der DAG; alte Bytes behalten ihren
Basisnamen (keine Migration). Guards CQRS040–046. **1:N-Split ist aktuell per CQRS046 blockiert**
(Consumer-Fabric fehlt).

## 10.8 Eine neue Blazor-UI (Modul)

Siehe [08 §8.7](08-frontend-blazor-client.md). Kurz: Modul-Klasse (`IStageModule`/`ISidebarModule`/
…) + `.razor`-View (`@inject Store`/`EffectScope`) + Store (`StoreBase`, `Handle` = Reducer) +
optional Refresh-/IntentHandler. Commands/Queries/Events kommen aus den Backend-Assemblies; das
Wiring ist generiert.

## 10.9 Ein Python-Worker

Siehe [09 §9.5](09-python-sdk.md). Kurz: State-dataclass + `CqrsClient[State]` erben + Handler mit
Type-Hints (`async def on_x(self, event: XDto, ctx, state)`, `yield`t Commands) +
`client.run(host, port)`.

**Eine Katalog-Funktion in Python (Laufort Extern, Plan E13).** Der Codegen erzeugt je Funktion ohne Lese-Fähigkeit eine Basis
(`domain_client/generated/funktionen.py`: `<Name>Basis` mit `FUNKTION`, `AUFTRAG`, `ERGEBNISSE` und `async def rufe(self, auftrag, x)`) und die Tabelle
des generischen Workers (`generated/funktions_tabelle.py`: Funktion → Basis, Modul, Klasse). Die Implementierung liegt in
`Domain.Client.Worker.Python.ML/funktionen/<modul>.py` (`IBildVerkleinerung` → `bild_verkleinerung.py`, Klasse `BildVerkleinerung`, Regel in
`Abstractions.Funktionsvertrag`), erbt die Basis und füllt `rufe` — genau ein Ergebnis per `return`, Werte der Implementierung in `self.konfig`:
```python
class BildVerkleinerung(BildVerkleinerungBasis):
    async def rufe(self, auftrag: VerkleinereBildDto, x: Ausfuehrung) -> BildVerkleinertDto | BildNichtLesbarDto:
        ausgabe = Path(self.konfig.get("OutputPath", "."))
        ...
        return BildVerkleinertDto(pfad=str(ziel), breite_pixel=breite, hoehe_pixel=hoehe)
```
Gestartet wird der GENERISCHE Worker (kein Domänenwissen): `python funktions_worker.py --funktionen IBildVerkleinerung [--slots 2] [--konfig NAME=WERT …]
[--pfad <verz mit eigenem Paket funktionen>]`, Adresse und Token aus `CQRS_GRPC_HOST`/`CQRS_GRPC_PORT`/`CQRS_AKTEUR_TOKEN`. In der Werkbank genügt der
Laufort: `Funktionen:<F>:Laufort = Extern` → ⟳ legt eine fehlende Implementierung aus der Basis an (Platzhalter `raise NotImplementedError`), der Rumpf
steht an der Funktions-Karte („rufe · Python“, ✎ und 🤖 — Slot `funktionpy|<F>@<Klasse>`), „▶ Worker starten“ nimmt ohne eigenen
`Funktionen:<F>:Worker` den generischen Worker mit `--funktionen <F>` und ein Token der Umgebung, dessen Akteur `IDarf<F>` hat.

## 10.10 Einen Test schreiben

Siehe [12 §12.7](12-tests-und-vermessung.md). Kurz — **Ebene 1 (Prüfstand, store-frei)** über die
generierte Factory:
```csharp
var konto = new Konto();
var h = new AggregateHandlerFactory().CreateHandler(konto);
foreach (var e in h.HandleCommand(new EroeffneKonto(Guid.NewGuid(), 20))) h.ApplyEvent(e);
var evs = h.HandleCommand(new ReserviereBetrag(konto.Id, 30));
evs.Should().ContainSingle().Which.Should().BeOfType<DeckungReichtNicht>();
```
**Store-Semantik gehört nach Ebene 2** (echtes Marten) — nie faken. Es gibt keinen
`InMemoryEventStore`.

Für Kaskaden über mehrere Aggregate, Prozesse und Flüsse gibt es die Test-DSL `Cqrs.Testing`
(`Szenario.Für<T>(…)`, `SagaSzenario`, `FlussLaufwerk`; Beispiele `SammelvorgangKaskadeTests`, `FlussLaufwerkTests`).
**Ausprobieren** ist kein Test: in der Werkbank ⟳ (echter Host in Umgebung `sim`, eigene DB), dann über die Leiste unten
durchspielen (Konsole als Akteur, Datei-Drop auf die Quellkarte, ⏩, ▶ Worker) und in der Spur ansehen
([konzept-werkbank.md](konzept-werkbank.md) §6/§7). Eine Simulation im Editor gibt es seit Werkbank P8 nicht mehr.

## 10.11 Die Reinheits-Grenze — was NICHT im Fachcode steht

Bewusst unsichtbar (Framework/Generat): `Id`/`Version`, `State`-Property + Ctor, OCC, Signal,
Cursor, Exactly-once/Dedup (`KommandoVerarbeitet`-Marken), Sharding, Prozess-Maschinerie,
Serialisierung, Proto-DTOs, ClusterKinds, DI-Wiring. Wenn eines davon in deinem Fachcode
auftaucht, ist etwas falsch verdrahtet.
