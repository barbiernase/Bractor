# 04 — Konsum- & Prozess-Maschine (vier Konsumenten, eine Maschine)

Stand 2026-10-13, gegen den Code geprüft. Maßstab: docs/konzept-werkbank.md.

Projektion, Reaktion, Prozess und Fluss sind **vier durable Konsumenten des Logs**. Sie folgen demselben Muster: ein Signal weckt
schnell, ein Poll heilt verlorene Signale, gelesen wird geordnet aus dem Event-Store (Invariante 1), der Fortschritt liegt durabel im
Store bzw. im Log. Es gibt keinen zweiten Marker und keine Taxonomie: was ein Konsument ist, folgt aus seiner Code-Form (Marker-Interface,
Fähigkeits-Parameter, Rückgabetyp). Konkret laufen zwei Schleifen:

| Konsument | Code-Form | Schleife | Fortschritt | Achse B |
|---|---|---|---|---|
| Projektion | `ISubscriber, IPullSubscriber`; `Handle` mit Schreib-Fähigkeit | `ProjectionAdapter` je (Konsument, Stream) | Marke im Store, co-committet | replaybar |
| Reaktion | `ISubscriber, IPullSubscriber`; `Handle` ohne Schreib-Fähigkeit, gibt Commands aus | `ProjectionAdapter` je (Konsument, Stream) | `IEmittentenCursor` (best-effort) | emittierend |
| Prozess | `IProzessDefinition`, `Prozess<T>.Definiere(…)` | Dirigent (`ProzessManager`) je Vorgang | Fold aus dem Log (+ Marking-Cursor) | emittierend |
| Fluss | `IPipeline`, `PipelineFluss.Definiere(…)` | derselbe Dirigent | wie Prozess | emittierend |

Ein Fluss wird in Prozess-Regeln übersetzt; Prozess und Fluss sind damit zwei Schreibweisen auf einem Dirigenten (§4.6). Wie man die
Bausteine schreibt: [10](10-entwickler-api.md).

## 4.1 Die gemeinsame Pull-/Signal-Schleife

Herzstück für Projektion und Reaktion ist `ProjectionAdapter` (`Infrastructure/Projections/ProjectionAdapter.cs`). Die store-agnostische
Schleife trägt die *Policy* (Marke lesen → ab Marke+1 geordnet lesen → Guard → dispatchen → Marke vorrücken), aber **keine Transaktion**.

`WakeAsync(streamId, vomPoll, ct)`:
1. **Startposition:** mit Tracker → `LastProcessedVersionAsync`; mit Emittenten-Cursor auf dem Signalpfad → `LadeAsync − 1`; ohne Tracker
   oder bei Poll-Heilung eines Emittenten bewusst `−1` (ab 0, at-least-once).
2. **Geordnet lesen** ab `applied+1` über `IEventStoreRepository.ReadStreamAsync` — das tragende Pull-Leseprimitiv.
3. **Pre-Dispatch-Guard:** `if (e.AggregateVersion <= applied) continue;`.
4. **Dispatch** je Event mit einem frischen `ProjectionWriter` (`Core/ProjectionWriter.cs`), der nur sammelt.
5. **Marke vorrücken:** Tracker → `MarkProcessedAsync` (durabler Commit-Punkt); Emittenten-Cursor → `SchreibeAsync` (best-effort).
6. **Versions-Index erst danach:** der verlierbare Index (`IReadModelDepsSink`) wird erst nach dem Marken-Commit publiziert — nie über
   Uncommittetes.

Wirft der Store eine `SchreibKonfliktException` (geteiltes Dokument, §4.3), wiederholt der Adapter den ganzen Stapel ab der Marke (bis zu
fünfmal, gestreut verzögert); danach übernimmt der Poll.

**Transport** (`Infrastructure/Projections/PullPath.cs`):
- **Signal** (schnell): `SignalReceiverActor` abonniert die `StateChangeVia…`-Signale der Eingangs-Event-Typen und weckt die
  per-Stream-Cluster-Identität.
- **Poll** (Sicherheit, 30 s): `Poller.PollOnceAsync` scannt über `ReadChangedStreamsAsync` jenseits der High-Water-Mark. Die HWM rückt
  **nur** vor, wenn jede Weckung als `WakeAck` bestätigt zurückkommt (bounded 10 s); sonst re-scannt der nächste Durchlauf dasselbe
  Fenster — kein terminaler, unverarbeiteter Stream wird still übersprungen.

Beide wecken **dieselbe** Cluster-Identität `(Kind, StreamId)`; der per-Stream `SignalAdapterActor` serialisiert sie über seine Mailbox
(Single-Writer je Stream). Aller durable Zustand liegt im Store → Passivierung bei Idle ist folgenlos.

## 4.2 Die zwei Achsen

**Achse „Transport“:** `IPullSubscriber : ISubscriber` wählt den Pull-Pfad. Der `PullPathGenerator`
(`Infrastructure.SourceGeneration/PullPathGenerator.cs`) findet jede Klasse mit `ISubscriber` **und** `IPullSubscriber` und erzeugt je
Konsument ein Cluster-Kind (`pull-<Typ>`) samt Registrierung (`GeneratedPullPaths`).

**Achse B — replaybar vs. emittierend** — im `ProjectionAdapter`-Konstruktor als sich ausschließendes Paar:
```csharp
IProjectionTracker? tracker,          // replaybar: Co-Commit + Reset
IEmittentenCursor?  emittentenCursor  // emittierend: best-effort, KEIN Reset
// beide gesetzt → throw
```
- `IProjectionTracker`: `LastProcessedVersionAsync`/`MarkProcessedAsync` **+ `ResetAsync`/`ResetAllAsync`** (Reset = Umkehrung der Marke →
  Neuaufbau).
- `IEmittentenCursor` (`MartenEmittentenCursor`): nur `LadeAsync`/`SchreibeAsync`, **bewusst ohne Reset** — ein Emittent wird nie blind
  neu abgespielt.

**Woraus die Achse folgt: aus den Schreib-Fähigkeiten der Handles.** Ein Handle bekommt die Store-Funktionen, die er benutzen darf, als
Parameter (`Handle(evt, env, writer, INotiereAusleihe f)`; Fähigkeit = Interface `: IWriteStore` mit genau einer Funktion, CQRS051). Der
Generator sammelt die Schreib-Fähigkeiten aller Handles einer Klasse. Zur Laufzeit öffnet die generierte Kind-Fabrik je Adapter-Actor einen
**Fähigkeits-Bereich** (`IFaehigkeitsFabrik.Oeffne()`, `Infrastructure/Projections/DiFaehigkeitsFabrik.cs`): Stores sind Scoped, jede
Fähigkeit eines Stores liefert in diesem Bereich **dieselbe Instanz**. Ist diese Instanz ein `IProjectionTracker`, ist der Konsument
replaybar; hat er keine Schreib-Fähigkeit, bekommt er den `IEmittentenCursor`. Gehören die Schreib-Fähigkeiten zu mehreren Stores, bricht der
Build (**CQRS052**). Ein Store in Konstruktor, Feld oder Eigenschaft eines Konsumenten ist verboten (**CQRS054**), `new …Store()` ebenso
(**CQRS055**).

| Konsument | Schreib-Fähigkeiten der Handles | Rückgabe `Handle` | Achse B |
|---|---|---|---|
| Projektion | aus genau einem Store, der `ICoCommitTracker` trägt | `Task` (Effekt über `writer.Execute`) | replaybar |
| Reaktion | keine | `IAsyncEnumerable<Cmd>` bzw. `…<OneOf<…>>` | emittierend |
| Prozess / Fluss | — (kein `Handle`) | — | emittierend (Fold + Marking-Cursor, §4.5) |

## 4.3 Projektionen und der Co-Commit

Form (neutral aus der Agnostik-Sonde, `GraphExtractor.Kern/Sonde/Lesen.cs.txt`): `partial class Leihliste : ISubscriber, IPullSubscriber`
mit `Handle(Ausgeliehen evt, IAggregateEnvelope envelope, ProjectionWriter writer, INotiereAusleihe notiere)`. Je Event
`writer.Execute(readModelId, ctx => …)`: der Effekt über die Fähigkeit plus `ctx.Track<T>(id)` für den Versions-Index. Was entsteht, liest
der Generator nur aus der Signatur (CQRS050), nie aus dem Rumpf.

**Theorie (der gültige Kern).** Exactly-once-*Zustellung* gibt es nicht (Effekt und Quittung lassen sich über eine Grenze nie atomar
koppeln); erreichbar ist exactly-once-*Wirksamkeit*, und dafür gibt es genau zwei Mechanismen:

| Mechanismus | Voraussetzung | kostet |
|---|---|---|
| **A — atomarer Co-Commit:** Effekt + Marke in einer Transaktion | Effekt und Marke in derselben transaktionalen Ressource | den Store |
| **B — idempotenter Effekt** + at-least-once | jeder Effekt wiederholungsfest (Upsert) | den Entwickler |

B entkommt der Atomarität bei Append-Effekten nicht (ein „schon gesehen?“ müsste selbst atomar mit dem Append geschrieben werden). Die Naht
`IProjectionTracker` ist deshalb bewusst minimal und store-agnostisch: der Adapter ruft Effekt und Marke als zwei Schritte; ob sie gemeinsam
gültig werden, entscheidet der Store. Damit ein Store aus dieser Naht A macht, muss er implizite Bedingungen erfüllen: Store und Tracker sind
dieselbe Instanz, Effekte werden gepuffert statt durchgeschrieben, die Unit-of-Work folgt aus der Aufrufreihenfolge (`LastProcessedVersion`
= Stapel-Beginn, `MarkProcessed` = Commit), Read-your-writes im Stapel, eine Instanz je Stream (Scoped), und ein geworfener Dispatch
committet nie. Statisch beweisbar ist Atomarität nicht — sie bleibt eine Laufzeiteigenschaft der Store-Transaktion und wird per Crash-Test
gezeigt (`CoCommitPostgresTests`, docs/12).

**Umsetzung.** Diese Bedingungen erfüllt die eine Basisklasse `MartenCoCommitStoreBase` (`Core/MartenCoCommitStoreBase.cs`),
von der alle Projektions-Stores erben: Effekte werden nur gepuffert, `MarkProcessedAsync` spielt sie zusammen mit dem `ProjectionCheckpoint`
in **einer** Marten-`IdentitySession` ab, ein `SaveChangesAsync`. Die Fähigkeits-Bereiche (§4.2) liefern „Store == Tracker“ per
Konstruktion. Dazu die Schreib-Regel je Dokument: eigenes Dokument (Schlüssel = Stream), Zeile dieses Streams, oder geteiltes Dokument
(`IGeteiltesReadModel`, Versionsprüfung; ein Konflikt verwirft Stapel und Marke → `SchreibKonfliktException`, §4.1). Dass ein Store nur
über diese Puffer-Methoden schreibt und Lese- und Schreib-Fähigkeiten nicht vermischt, prüft der `StoreZugriffAnalyzer` (CQRS066/067).
- **Hebel 1 (umgesetzt):** Atomarität als eigener Typ. `ICoCommitTracker : IProjectionTracker` tragen nur co-committende Stores; der
  GA-1-Check (`Infrastructure/Projections/GaEinsPruefung.cs`, beim ersten Spawn) bricht, wenn eine `IAppendProjektion` keinen
  `ICoCommitTracker` hat.
- **Hebel 2 (offen):** eine framework-getriebene Unit-of-Work (der Adapter holt eine Einheit vom Store und routet Effekt und Marke
  hindurch), die Fehlverdrahtung strukturell unbaubar machte.

Der generische `MartenProjectionTracker` (eigene Session, Dual-Write) ist nirgends registriert — toter Code (docs/13 P3-2). Dass die
Basisklasse im Domänen-Projekt `Domain.Infrastructure` liegt statt im Framework, ist eine Schuld der Projektlage (docs/13 W2-1).

**Neuaufbau:** `ProjectionRebuilder` leert das Ziel, setzt die Marken zurück (`ResetAllAsync`) und lässt den normalen Adapter-Pfad ab 0
laufen — Emittenten haben kein Reset und liegen nie auf diesem Pfad. In Sim vergleicht `ProjektionsStandDienst`
beim Start den Code-Hash jeder Projektion (Klasse + Stores) mit `<schema>.stand` und baut bei Abweichung die ganze Leseseite neu auf; in Prod
meldet er nur (`docs/konzept-werkbank.md` §4.6).

## 4.4 Reaktionen & das Emit-Primitiv

Eine Reaktion ist ein Pull-Konsument ohne Schreib-Fähigkeit, dessen `Handle` Commands ausgibt. Keine Domäne der Beispiel-Solution hat
derzeit eine; die Form zeigt die Sonde (`GraphExtractor.Kern/Sonde/Lesen.cs.txt`):
```csharp
public partial class MahnungBeiSchaden : ISubscriber, IPullSubscriber
{
    public string SubscriberId => "mahnung-bei-schaden";
    public async IAsyncEnumerable<EroeffneMahnung> Handle(SchadenGemeldet evt, IAggregateEnvelope envelope, ProjectionWriter writer)
    { … yield return new EroeffneMahnung(…); }
}
```
Der **Rückgabetyp** (konkret oder `OneOf<…>`, CQRS050) sagt, was entstehen kann. Die Ausgabe routet der `HandlerOutputRouter`
(`Infrastructure/PubSub/HandlerOutputRouter.cs`): `ICommand` → an das Ziel-Aggregat, `IEvent` → reaktiv über den Broker re-publiziert
(verlierbar, in Sim als Spur-Hinweis sichtbar).

Jeder Command geht durch das **eine** Emit-Primitiv `CommandEmitter.EmitAsync` (`Infrastructure/PubSub/CommandEmitter.cs`):
- deterministische `CommandId` aus `EmitKausalität` (Korrelation + auslösender Stream + Position/Command-Typ) → stabil über Re-Wakes,
- `CommandModus.Emittiert()` = **keine erwartete Version**,
- **bounded** (`_sendeFrist`, Standard 5 s), fire-and-forget, at-least-once; einen Timeout heilt der nächste Re-Wake bzw. Poll.

**Dedup macht die Framework-Inbox, nicht der Fachcode.** Der Aggregat-Actor co-committet mit den Domänen-Events eine
`KommandoVerarbeitet(CommandId)`-Marke (`Infrastructure/Aggregate/KommandoVerarbeitet.cs`, `IProzessIntern`: der Applier sieht sie nie);
eine fachliche Ablehnung eines emittierten Commands wird zur durablen `KommandoAbgelehnt`-Marke. Aus beiden faltet `AggregateActorBase` zwei
Inbox-Mengen (`BoundedInbox`) und erkennt eine Wiederholung **vor** der OCC-Prüfung → Noop. Die Inbox ist gedeckelt (docs/13 P2-4).

**Analyzer-Zwang** (`CommandEmitAnalyzer`): **CQRS020** (rohes `RequestAsync<CommandResult>` außerhalb von Emitter/Dispatcher),
**CQRS021** (Command-Send mit `CancellationToken.None`/`default`). Im generierten Pull-Emit-Pfad steht noch `CancellationToken.None`
(nur durch die Emitter-Frist gebunden, docs/13 P1-6).

## 4.5 Prozess-Maschine (der Dirigent)

**Definition (DSL).** Ein Prozess ist eine `IProzessDefinition` mit typisierten Event→Aufruf-Regeln über
`Prozess<TAuslöser>.Definiere(p => …)` (`Abstractions/Prozess/ProzessBuilder.cs`):
- `p.Auf<E>().Sende<Cmd>(e => …)` — Command an ein Aggregat; `.SendeJe<Cmd>(…)` — Fan-out dynamischer Breite;
- `.Rufe<F>(e => auftrag)` — Katalog-Funktion statt Aggregat (§4.6); `.Zeitlimit(t)` je Aufruf;
- `.RückgängigDurch<Cmd>(…)` / `.RückgängigDurchJe<Cmd>(…)` — Gegenzug für die Kompensation;
- `.Und<E2>()` (bis drei Events) — Join; `.UndAlle<ESammel>(e => n)` — Count-Join, die Breite steht im Auslöser.

Beispiel (Sonde, `GraphExtractor.Kern/Sonde/Gebuehren.cs.txt`):
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
In der Beispieldomäne ist `Domain/Sammelvorgang/TeilFertigProzess.cs` der kleinste Fall (ein `Auf → Sende`).

Der `ProzessRegelnGenerator` (Domain.SourceGeneration) legt jede Definition als Daten in `GeneratedProzessRegeln.Alle` (Name = Klassenname,
CQRS012; Regel-Namen `<Auslöser>[+<Und>]→<Ziel>`, CQRS072). Der `ProzessRegelDiagnosticGenerator` prüft CQRS001–003 (doppelter Auslöser,
Command ohne Decider, Command-Typ explizit). Jede Assembly mit Prozessen oder Flüssen hat ihre eigene Registry
`{Assembly}.Prozess.GeneratedProzessRegeln`; in der Komposition vereinigt der `ProzessVerbundGenerator` die Registries aller referenzierten
Rollen-Projekte zu `GeneratedProzessVerbund.Alle` (CQRS012: derselbe Name in zwei Assemblies; CQRS099: Prozesse ohne Registry), ohne eine getippte
Liste von Projekten. Das Framework liest den Verbund über `Anwendung.Prozesse` (`GeneratedProzesse.AlleRegeln`).

**Kern-Invariante: Struktur aus Code, Marking aus dem Log.** Der generische `ProzessManager` (`Infrastructure/Prozess/ProzessManager.cs`)
ist ein einmal geschriebener Petri-Netz-Interpreter; je Vorgang (Korrelation) ein virtueller `ProzessManagerActor`. Sein eigenes Log ist der
Stream der Korrelation (`ProzessGestartet`, `SchrittGescheitert`, `SchrittUmgeleitet`, `ProzessBeendet`).

**Start und Zustellung** (`KorrelationsRouter`, `ProzessManagerWiring.cs`): der Router abonniert die Signale aller beteiligten Events. Ein
Auslöser-Event startet einen Vorgang mit deterministischer Korrelation `ProzessId.Für(Name, Stream, Version)`; ein Ziel-Event trägt die
Korrelation in seinen Metadaten (vom Manager beim Feuern gesetzt) und weckt daraus. Der `ProzessManagerStartupService` pollt zusätzlich nach
Korrelation (30 s) und betreibt den **§3-Backstop** (15 s): er scannt den durablen `IProzessOffenIndex` und weckt jeden offenen Vorgang
(bounded 10 s) — das Netz für verlorene Weckungen.

**Fold.** Je Ziel-Stream ab `StreamCursor[s]+1` lesen, in `MarkingKompakt` einarbeiten, ein Fixpunkt zieht die Kandidaten. Drei getrennte
Ergebnis-Achsen je Vorgang:
- **ErgebnisDa** = aufgelöst (irgendein Ziel-Event, auch Noop-Marke) → nicht mehr feuern;
- **WirkungDa** = ein Domänen-Event (kein `IProzessIntern`) → kompensierbar, aktiviert Joins;
- **AbgelehntDa** = durable `KommandoAbgelehnt`-Marke → wird `SchrittGescheitert`.

Ohne die dritte Achse läse der Vorwärtszweig eine Ablehnung als „aufgelöst“ und schriebe fälschlich `ProzessBeendet(true)`.

**Ein Schritt je Weckung** (`WakeAsync`): Marking falten → neue Ablehnungen und abgelaufene Zeitlimits durabel als `SchrittGescheitert`
(im Fluss bei verdrahtetem Fehler-Port als `SchrittUmgeleitet`, §4.6) → ohne Fehlschlag: alle offenen Funktions-Aufträge anbieten
(parallel, ohne Selbst-Weckung — der Ausführer weckt mit dem Ergebnis), den ersten offenen Command feuern (sequenziell), sind keine Aufrufe
offen: `ProzessBeendet(true)` → mit Fehlschlag: **Kompensation** rückwärts über die erfolgreichen Regeln mit Gegenzug; ist ein Gegenzug selbst
abgelehnt, endet der Vorgang als `ProzessBeendet(false, KlärungNötig: true)` mit einem best-effort Dead-Letter-Eintrag (kein endloses
Neufeuern) — sonst `ProzessBeendet(false)`.

**Feuern** geht über `ProzessManagerActor` → `DetachedProzessSend.Wrap` → `CommandEmitter.EmitAsync` mit vorgegebener CommandId: der
deterministische **Vorgang ist die CommandId**. Der Fold matcht den durablen Ausgang über `CausationId == vorgang`, **ohne Quittung**. Nach
jedem Send weckt sich der Manager selbst, damit der Fold den Ausgang sieht (das Ergebnis der letzten Regel ist Auslöser keiner Regel).

**Marking-Cursor (P5b).** `MarkingKompakt` (`Abstractions/Prozess/ProzessMarking.cs`) hält je Ziel-Stream einen `StreamCursor` und verdichtete
Vorgangs-Marken statt roher Tokens:
- ein HOT-Cache je Manager-Instanz trägt das Marking über die Weckungen einer Aktivierung;
- **feuer-gerichtete Reads:** warm liest der Manager nur die seit dem letzten Fold befeuerten (und nie gesehenen) Ziel-Streams nach;
- durabel (`IProzessMarkingStore`, Marten-Dokument `ProzessMarkingDoc`) nur gedrosselt, Standard alle 32 Weckungen;
- **Kaltstart oder geänderter `RegelHash` (`ProzessRegelHash`) → Voll-Fold ab 0** (Invariante 1). Der Cache ist nicht-autoritativ; ein
  veralteter Stand kann höchstens erneut feuern, was an der Empfänger-Inbox verpufft.

**Zeitlimit.** Ein offener Aufruf, dessen Limit seit seiner Aktivierung (jüngstes gematchtes Event, DB-Zeit aus dem Log) gegen `IDbClock`
abgelaufen ist, scheitert wie eine Ablehnung. Es gibt keinen eigenen Timer: entdeckt wird der Ablauf erst bei der nächsten Weckung, im
Ruhezustand also durch den Backstop — Genauigkeit 15 s (docs/13 W11-2).

**Boot-Guards** (`ProzessManagerStartupService`): Azyklizität (`ProzessAzyklizität.PrüfeAlle`, gespeist aus `GeneratedCommandRouting.Produziert`
und den Ergebnissen der Funktionen), Auslöser-Kollision zwischen Prozessen (fail-fast), jede gerufene Funktion ist gebunden
(`FunktionsExtensions.PrüfeBindungen`).

Offen: Feinschliff an `MarkingKompakt` und an der Kompensation, `KlärungNötig` ohne Test (docs/13 P2-5); Saga-E2E- und
Marking-Äquivalenz-Tests fehlen seit dem Löschen der alten Beispiel-Domänen (docs/13 T-1).

## 4.6 Fluss auf dem Dirigenten und Katalog-Funktionen

**Fluss.** Eine Pipeline ist eine Klasse `: IPipeline` mit `PipelineFluss Fluss => PipelineFluss.Definiere(p => …)`
(`Abstractions/Fluss/Pipeline.cs`). Geschrieben wird vom Draht aus, so wie der Editor zeichnet:
- Quelle: `p.Quelle<T>()` (eine `IQuellNachricht`) oder `p.Auf<E>()` (ein Event aus dem Log) — genau eine je Pipeline;
- `draht.Rufe<F>(λ)` (Katalog-Funktion), `knoten.Bei<E>()` (ein Ausgang), `draht.Sende<Cmd>(λ)` (Command), `draht.Je(…)` / `Sammle(…)`;
- `p.Alle(a, b, …)` (∧), `.Oder(…)` (∨), `.Zeitlimit(t)`;
- Fehler-Ports `knoten.BeiZeitlimit()` (⏳) und `knoten.BeiAbgelehnt()` (✕);
- `quelle.Strom()` (Aggregat-Id und Version der Quelle als Draht) und `strom.Warte<A>().Zeitlimit(t)` (Frist als Rennen).

Beispiel (Sonde, `GraphExtractor.Kern/Sonde/Fluss.cs.txt`):
```csharp
public sealed class Ausleihfrist : IPipeline
{
    public PipelineFluss Fluss => PipelineFluss.Definiere(p =>
    {
        var ausgeliehen = p.Auf<Ausgeliehen>();
        var rueckgabe = ausgeliehen.Strom().Warte<Zurueckgegeben>().Zeitlimit(TimeSpan.FromDays(28));
        var offen = ausgeliehen.Rufe<IOffenZaehlung>(ausgeliehen => new ZaehleOffene(ausgeliehen.LeserId));
        var mahnung = p.Alle(rueckgabe.BeiZeitlimit(), offen.Bei<OffeneGezaehlt>(), ausgeliehen.Strom())
            .Sende<EroeffneMahnung>((rueckgabeZeit, offen, ausgeliehenStrom) => new EroeffneMahnung(ausgeliehenStrom.Id, offen.Anzahl));
    });
}
```
Der Variablenname ist die Knoten-Identität (Wirker `fluss:<Pipeline>#<Knoten>`); weil nur deklarierte Knoten verdrahtet werden können, ist ein
Fluss per Konstruktion azyklisch. Die Form prüft der `FlussAnalyzer` (CQRS073–078).

**Ausführung.** `PipelineFluss.Definiere` übersetzt den Fluss in `ProzessRegeln` mit Knoten-Herkunft (`IstFluss`); der
`ProzessRegelnGenerator` registriert `IPipeline` neben `IProzessDefinition`, und **derselbe** `ProzessManager` führt sie aus — Fold,
Marking-Cursor, Backstop und Zeitlimit wie in §4.5. Welche Tokens eine Regel belegen (Herkunft, Je-Teile, Sammeln), entscheidet der reine
Kern `FlussBelegung` (`Abstractions/Prozess/FlussBelegung.cs`), den auch das `FlussLaufwerk` in `Cqrs.Testing` für Prüfstand-Tests nutzt.
- **Fehler-Ports als Tokens:** ist der ⏳- bzw. ✕-Port eines Knotens verdrahtet, wird ein Zeitlimit bzw. eine Ablehnung nicht zum
  Fehlschlag, sondern im Log als `SchrittUmgeleitet` vermerkt und im Dirigenten zum Token `ZeitlimitAbgelaufen` bzw. `SchrittAbgelehnt`
  (beide `IProzessIntern`, nie im Log) — der Fluss läuft auf dem verdrahteten Zweig weiter.
- **Warte:** ein Warte-Knoten liest den Strom der Quelle nach ihrer Version; das erste erwartete Event vor Ablauf (DB-Zeit) gewinnt, sonst
  entsteht `ZeitlimitAbgelaufen`.
- **Quell-Einstieg:** `QuellEingang.NimmAufAsync` (`Infrastructure/Quellen/Quellen.cs`) hängt eine Quell-Nachricht als erstes Event eines
  Vorgangs-Streams an, dessen Id deterministisch aus Typ und `Kennung` folgt (Version 0 → **genau einmal** im Log, auch bei Neustart oder
  zweitem Knoten), und weckt dann jede Pipeline, die mit `p.Quelle<T>()` beginnt; eine verlorene Weckung heilt der Poll des
  Korrelations-Routers. Gespeist wird er vom fest codierten Treiber (Datei) bzw. vom Webhook aus der Konfig (`docs/konzept-werkbank.md` §3).

**Katalog-Funktionen.** Eine Funktion ist ein Interface `: IFunktion` mit genau einer Methode
`Task<OneOf<E1, …>> RufeAsync(TAuftrag, IAusfuehrung[, Lese-Fähigkeiten])`, `TAuftrag : IAuftrag<F>`, Ergebnisse persistente Events. Der
`FunktionsGenerator` erzeugt den reflexionsfreien Dispatch `GeneratedFunktionen` (CQRS068/069). Prozess (`Rufe<F>`) und Fluss rufen sie gleich:
1. Der Dirigent **bietet** den Auftrag an (`FunktionsAnbieter.BieteAnAsync`) — an den **Vermittler** seiner Funktion, einen virtuellen
   Cluster-Actor je Funktion (`FunktionsVermittlerActor`, Identität = voller Funktionsname). Der Vermittler ist nicht maßgeblich: geht er
   verloren, bietet der Dirigent jeden offenen Auftrag bei der nächsten Weckung neu an.
2. **Abholer** holen per Pull, so viele wie Slots frei sind, mit Lease und Long-Poll: im Host der `FunktionsAbholer` (C#-Implementierung),
   extern eine `FunktionsAnbieterSitzung` über gRPC (generischer Python-Worker), von Hand der `HandAusfuehrer` (Werkbank, JSON-Eingang).
3. Abgeschlossen wird immer über `FunktionsAusfuehrer.SchreibeErgebnisAsync`: **genau ein** Ergebnis im Ausführungs-Stream (Stream-Id =
   Vorgang, Anhängen ab Version 0; rechnen zwei, gewinnt eines). Wirft die Funktion auch nach den Wiederholungen, meldet ein externer
   Ausführer einen Fehler oder ein Ergebnis außerhalb des Vertrags, liegt dort stattdessen eine `KommandoAbgelehnt`-Marke. Danach weckt der Ausführer den Vorgang; der Fold liest das Ergebnis wie
   das Event eines Aggregats.

**Laufort ist Konfig, nicht Code.** Die Umgebungs-Datei sagt je Funktion `Funktionen:<F>:Laufort` (`Host` · `Extern` · `Hand`), dazu
`Implementierung`, `Slots`, `Wiederholungen` (`Abstractions/Umgebung/UmgebungKonfig.cs`). `AddCqrs` bindet daraus beim Start
(`BindeFunktionen`, Fabriken aus `GeneratedFunktionen`), danach die Domänen-Dienste der Implementierungen (`BindeDienste`, `GeneratedDienste`).
Eine Funktion mit Lese-Fähigkeit läuft nur im Host. Details: `docs/konzept-editor-pipelines.md` §14, [10 §10.5a/§10.6](10-entwickler-api.md).

**Altlast: die Handle-Pipeline.** Daneben ist noch die frühere Pipeline-Maschinerie verdrahtet, ohne Domänen-Nutzer: `IPipelineHandler` mit
`PipelineActorBase`, `PipelineEventPullBridge` (persistierte Events über den Pull-Pfad, emittierend), `PipelineStartupService`, die
Trigger-Welt (`IPipelineTrigger`, `ITriggerRegistration`, `TimerTrigger`, `WebhookTrigger`) und `Selbst<T>`/`Frist<TCmd>`/`FristStorno<TCmd>` als
Handle-Ausgang (durabler Fristplaner `FristScheduler`/`MartenFristplan`). Sie ist kein Weg für neuen Code; Rückbau offen (docs/13 W11-1).

## 4.7 Dead-Letter & Snapshots

- **Dead-Letter:** `MartenDeadLetterSink` schreibt ein durables `DeadLetter`-Dokument je toter Zustellung (Schema `dlq`), best-effort —
  Beobachtung, kein kritischer Pfad (z. B. ein Vorgang in `KlärungNötig`, ein vergifteter Aggregat-Actor). Lesen: `MartenDeadLetterReadStore`.
- **Snapshots:** `MartenSnapshotStore` — `Snapshot<TState>` als jsonb je Aggregat, Registrierung generiert (`SnapshotRegistrationGenerator`).
  **Nicht-autoritativ:** kein Treffer oder eine andere `SchemaVersion` → Voll-Replay (`AggregateRehydrator`). Die Schema-Version ist der
  Struktur-Hash des States plus der Code-Hash von State und Applier (`{State}AggregateHandler.CodeHash`) — eine geänderte Apply-Logik
  invalidiert alte Snapshots. Geschrieben wird nach einer Schwelle von Events und bei Deaktivierung: State und Inbox werden im Actor-Turn
  kopiert, der DB-Write läuft danach fire-and-forget. Die Kopie ist ein JSON-Round-Trip (docs/13 W10-2).

## 4.8 Spur der Konsumenten

Jeder Log-Eintrag trägt die Header `ursache`, `wirker`, `ueber`, `stand` (`Abstractions/Spur/Spur.cs`, geschrieben in
`Infrastructure/Persistence/SpurHeader.cs`). Die Konsumenten setzen sie per `Spur.Von(…)` um ihren Ausgang; der `CommandEmitter` legt Ursache
und Wirker in den Command-Envelope, das Ziel-Aggregat schreibt mit `wirker = decide:<Aggregat>/<Command>` und dem Absender als `ueber`:
- **Reaktion:** `HandlerOutputRouter` — Ursache = auslösendes Event (`Stream:Version`), Wirker `reaktion:<Typ>`.
- **Prozess / Fluss:** `ProzessManager.FeuereAsync`/`BeauftrageAsync` — Ursache = jüngster echter Match-Token, Wirker `prozess:<P>#<Regel>`
  bzw. `fluss:<P>#<Knoten>` (`ProzessRegeln.Wirker`).
- **Funktion:** die Spur reist im `AuftragAnbieten`; `FunktionsAusfuehrer` schreibt das Ergebnis mit `funktion:<F>`, der Absender steht als
  `ueber`.
- **Quelle:** `QuellEingang` schreibt mit `quelle:<Typ>` und ohne Ursache (Wurzel).
- **Projektion** schreibt nie ins Log; `projektion:<Typ>` ist nur Karten-Id.

Format, Lesen und Ansichten: `docs/konzept-werkbank.md` §7.

## 4.9 Offen

Offene Punkte stehen nur in [13](13-reifegrad-schulden-bewertung.md): Testlücke T-1, `CancellationToken.None` im Pull-Emit-Pfad P1-6,
Handle-Pipeline/Trigger-Welt und doppelte DSL W11-1, Zeitlimits über den Backstop W11-2, `BoundedInbox` P2-4, Marking/Kompensation/
`KlärungNötig` P2-5, hartkodierte Intervalle P2-8, reflektive Serialisierung (Marking-Store, Snapshot-Kopie) W10-2, Projektlage W2-1,
toter `MartenProjectionTracker` P3-2.
