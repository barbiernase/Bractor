# 04 — Konsum- & Prozess-Maschine (vier Konsumenten, eine Maschine)

Stand 2026-10-13, gegen den Code geprüft. Maßstab: docs/konzept-werkbank.md.

Projektion, Reaktion, Prozess und Fluss sind **vier durable Konsumenten des Logs**. Sie folgen demselben Muster: ein Signal weckt
schnell, ein Poll heilt verlorene Signale, gelesen wird geordnet aus dem Event-Store (Invariante 1), der Fortschritt liegt durabel im
Store bzw. im Log. Es gibt keinen zweiten Marker und keine Taxonomie: was ein Konsument ist, folgt aus seiner Code-Form (Marker-Interface,
Fähigkeits-Parameter, Rückgabetyp). Konkret laufen zwei Schleifen:

| Konsument | Code-Form | Schleife | Fortschritt | Achse B |
|---|---|---|---|---|
| Projektion | `ISubscriber, IPullSubscriber`; `Handle` mit Schreib-Fähigkeit | `ProjectionAdapter` je (Konsument, Stream) | Marke im Store, co-committet | replaybar |
| Reaktion | `ISubscriber, IPullSubscriber`; `Handle` ohne Schreib-Fähigkeit, gibt Commands aus | `ProjectionAdapter` je (Konsument, Stream) | `IEmittentenCursor`, vorgerückt nach bestätigtem Emit | emittierend |
| Prozess | `IProzessDefinition`, `Prozess<T>.Definiere(…)` | Dirigent (`ProzessManager`) je Vorgang | Fold aus dem Log (+ Marking-Cursor) | emittierend |
| Fluss | `IPipeline`, `PipelineFluss.Definiere(…)` | derselbe Dirigent | wie Prozess | emittierend |

Ein Fluss wird in Prozess-Regeln übersetzt; Prozess und Fluss sind damit zwei Schreibweisen auf einem Dirigenten (§4.6). Wie man die
Bausteine schreibt: [10](10-entwickler-api.md).

## 4.1 Die gemeinsame Pull-/Signal-Schleife

Herzstück für Projektion und Reaktion ist `ProjectionAdapter` (`Infrastructure/Projections/ProjectionAdapter.cs`). Die store-agnostische
Schleife trägt die *Policy* (Marke lesen → ab Marke+1 geordnet lesen → Guard → dispatchen → Marke vorrücken), aber **keine Transaktion**.

`WakeAsync(streamId, ct)` → `bool` (bestätigt aufgeholt), für Signal und Poll gleich:
1. **Startposition:** mit Tracker → `LastProcessedVersionAsync`; mit Emittenten-Cursor → `LadeAsync − 1`; ohne beides bewusst `−1` (ab 0,
   at-least-once, nur für idempotente Effekte gültig).
2. **Geordnet lesen** ab `applied+1` über `IEventStoreRepository.ReadStreamAsync` — das tragende Pull-Leseprimitiv.
3. **Pre-Dispatch-Guard:** `if (e.AggregateVersion <= applied) continue;`.
4. **Dispatch** je Event mit einem frischen `ProjectionWriter` (`Core/ProjectionWriter.cs`), der nur sammelt, und den `EmitAusgaenge` der
   Weckung (`Infrastructure/PubSub/EmitAusgaenge.cs`, trägt das Token): ein ausgegebener Command wird sofort gesendet, sein Ausgang erfasst —
   die Commands eines Tails laufen parallel.
5. **Ausgänge abwarten:** bleibt ein Command „unbekannt“, endet die Weckung mit `false` — Marke bzw. Cursor bleiben stehen, die nächste
   Weckung sendet den Tail mit denselben CommandIds erneut (die Inbox des Ziels verwirft, was schon gewirkt hat; G-2).
6. **Marke vorrücken:** Tracker → `MarkProcessedAsync` (durabler Commit-Punkt); Emittenten-Cursor → `SchreibeAsync` (erst nach bestätigtem
   Emit; scheitert das Schreiben, sendet die nächste Weckung erneut).
7. **Versions-Index erst danach:** der verlierbare Index (`IReadModelDepsSink`) wird erst nach dem Marken-Commit publiziert — nie über
   Uncommittetes.

Wirft der Store eine `SchreibKonfliktException` (geteiltes Dokument, §4.3), wiederholt der Adapter den ganzen Stapel ab der Marke (bis zu
fünfmal, gestreut verzögert); danach übernimmt der Poll.

**Transport** (`Infrastructure/Projections/PullPath.cs`):
- **Signal** (schnell): `SignalReceiverActor` abonniert die `StateChangeVia…`-Signale der Eingangs-Event-Typen und weckt die
  per-Stream-Cluster-Identität.
- **Poll** (Sicherheit, 30 s): `Poller.PollOnceAsync` scannt über `ReadChangedStreamsAsync` jenseits der High-Water-Mark. Die HWM rückt
  **nur** vor, wenn jede Weckung als `WakeAck(Aufgeholt: true)` zurückkommt (bounded 10 s); sonst re-scannt der nächste Durchlauf dasselbe
  Fenster — kein terminaler, unverarbeiteter Stream wird still übersprungen. Ein scheiternder Durchlauf wird als Warnung gemeldet.
- **Tombstones:** scheitert ein Append, füllt Marten die reservierten Sequenzen mit `Tombstone`-Events in einem eigenen Stream.
  `MartenEventStore` meldet diesen Stream nicht als geändert und liest keine Tombstones (G-7, erledigt); die HWM läuft über sie hinweg.

Beide wecken **dieselbe** Cluster-Identität `(Kind, StreamId)`; der per-Stream `SignalAdapterActor` serialisiert sie über seine Mailbox
(Single-Writer je Stream). Aller durable Zustand liegt im Store → die Passivierung ist folgenlos: nach `Lebenszyklus.AdapterLeerlaufSekunden`
(Standard 120) ohne Weckung stoppt der Adapter (PF-5, `Passivierung`), die nächste Weckung aktiviert ihn ab Marke bzw. Cursor.

## 4.2 Die zwei Achsen

**Achse „Transport“:** `IPullSubscriber : ISubscriber` wählt den Pull-Pfad. Der `PullPathGenerator`
(`Infrastructure.SourceGeneration/PullPathGenerator.cs`) findet jede Klasse mit `ISubscriber` **und** `IPullSubscriber` und erzeugt je
Konsument ein Cluster-Kind (`pull-<Typ>`) samt Registrierung (`GeneratedPullPaths`).

**Achse B — replaybar vs. emittierend** — im `ProjectionAdapter`-Konstruktor als sich ausschließendes Paar:
```csharp
IProjectionTracker? tracker,          // replaybar: Co-Commit + Reset
IEmittentenCursor?  emittentenCursor  // emittierend: durabel, ab Cursor, KEIN Reset
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
- **bounded** (`_sendeFrist`, Standard 5 s), at-least-once, ohne eigenen Retry;
- liefert den **Ausgang** (`EmitAusgang`): `Zugestellt` (Erfolg, auch als erkannte Wiederholung), `Abgelehnt` (fachlich, durabel als Marke —
  `CommandResult.Abgelehnt`), `Unbekannt` (Frist abgelaufen, technischer Fehler, Transportfehler). Keine Ausnahme verlässt den Emitter. Wer
  „unbekannt“ bekommt, wiederholt an seinem durablen Punkt mit derselben CommandId: der Emittenten-Cursor bleibt stehen (§4.1), die Frist liegt
  weiter (§4.6), der Dirigent wiederholt nach Backoff (§4.5);
- **Drossel je Ziel** (`EmitDrossel`): höchstens 8 Sends je Ziel-Aggregat gleichzeitig unterwegs (je Knoten); der Rückstau eines heißen
  Aggregats liegt vor dem Send statt in seiner Mailbox, sodass die Sends in ihrer Frist bleiben (PF-9).

**Dedup macht die Framework-Inbox, nicht der Fachcode.** Der Aggregat-Actor co-committet mit den Domänen-Events eine
`KommandoVerarbeitet(CommandId)`-Marke (`Infrastructure/Aggregate/KommandoVerarbeitet.cs`, `IProzessIntern`: der Applier sieht sie nie);
eine fachliche Ablehnung eines emittierten Commands wird zur durablen `KommandoAbgelehnt`-Marke. Aus beiden faltet `AggregateActorBase` zwei
Inbox-Mengen (`BoundedInbox`) und erkennt eine Wiederholung **vor** der OCC-Prüfung → Noop. Die Inbox ist gedeckelt (docs/13 G-2). Was daraus garantiert ist: [11](11-garantien.md).

**Analyzer-Zwang** (`CommandEmitAnalyzer`): **CQRS020** (rohes `RequestAsync<CommandResult>` außerhalb von Emitter/Dispatcher),
**CQRS021** (Command-Send mit `CancellationToken.None`/`default`). Der generierte Pull-Emit-Pfad reicht das Token der Weckung durch
(`EmitAusgaenge.Token`).

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

**Feuern** geht über `ProzessManagerActor` → `DetachedProzessSend` (eine Instanz je Manager) → `CommandEmitter.EmitAsync` mit vorgegebener
CommandId: der deterministische **Vorgang ist die CommandId**. Der Fold matcht den durablen Ausgang über `CausationId == vorgang`; der
Emit-Ausgang entscheidet nur über das Wecken (PF-9): nach „zugestellt“ oder „abgelehnt“ weckt sich der Manager sofort selbst, damit der Fold den
Ausgang sieht (das Ergebnis der letzten Regel ist Auslöser keiner Regel); nach „unbekannt“ erst nach einem Backoff je Vorgang (1 s, 2 s, 4 s …
höchstens 30 s, gestreut). Solange der Send eines Vorgangs läuft oder sein Backoff wartet, sendet keine andere Weckung (Signal, Poll, Backstop)
ihn erneut. Mit der Drossel je Ziel (§4.4) baut ein überlastetes Aggregat seinen Rückstau ab, statt dass Wiederholungen ihn vervielfachen.

**Passivierung** (PF-5): ein Manager ohne Nachricht stoppt nach `Lebenszyklus.ProzessManagerLeerlaufSekunden` (Standard 600). Offene Vorgänge
weckt der Backstop alle 15 s und hält sie damit aktiv; beendete fallen heraus. Der Kaltstart liest den Marking-Cursor bzw. faltet voll.

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
Handle-Ausgang (durabler Fristplaner `FristScheduler`/`MartenFristplan`; er entfernt eine Frist erst bei entschiedenem Emit-Ausgang, ein
unbekannter lässt sie für den nächsten Tick liegen). Sie ist kein Weg für neuen Code; Rückbau offen (docs/13 W11-1).

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

Offene Punkte stehen nur in [13](13-reifegrad-schulden-bewertung.md): Testlücke T-1,
Handle-Pipeline/Trigger-Welt und doppelte DSL W11-1, Zeitlimits über den Backstop W11-2, Garantie-Lücken G-2 (Inbox-Fenster), G-4 (Checkpoint), Marking/Kompensation/
`KlärungNötig` P2-5, hartkodierte Intervalle P2-8, reflektive Serialisierung (Marking-Store, Snapshot-Kopie) W10-2, Projektlage W2-1,
toter `MartenProjectionTracker` P3-2.

## 4.10 Entwurf: Batch-Konsum der Projektionen (Phase 3, noch nicht umgesetzt)

> **Entwurf** für Phase 3 des Plans Garantien und Durchsatz (Haltepunkt (b): erst nach Tobis Freigabe bauen). Er ersetzt für **replaybare
> Projektionen** den Adapter je (Projektion, Stream) und den Heil-Poll je Stream (PF-1, PF-2) und schließt G-4. Handle-Signaturen und Fachcode
> bleiben unverändert (Inv. 5); neue Dispatch-Logik nur im Generator. Nach dem Bau wird dieser Abschnitt in §4.1–§4.3 eingearbeitet.

### 4.10.1 Warum, gemessen

Phase 2 (`vermessung/ergebnisse/2026-10-15_0745_4caa176_phase-2`, `pg_stat_statements.csv`):
- **S1** (5 replaybare Projektionen): je Weckung (Projektion, Stream) drei Sessions — Marke lesen, Tail lesen, Commit. Im Block 18 000
  Transaktionen, 114 000 Session-Öffnungen (`DISCARD ALL`), 36 000 Stream-Reads, 6 250 Checkpoint-Reads, 5 000 Checkpoint-Upserts für 600 Paare.
  Dazu weckt der Heil-Poll jeden geänderten Stream für jede Projektion, auch fremde (L1: 24 000 Erstweckungen, PF-2).
- **S5** (Prüfdomäne): **keine** replaybare Projektion. Die 79 000 Stream-Reads je Block kommen vom Dirigenten (Folds, Prozess-Poll ab 0,
  Rehydration; PF-4). Batch-Konsum hebt S5 kaum — siehe E9.
- Postgres-Grundgröße (§12.5): ein Batch von 500 Effekten + Checkpoints in einer Transaktion ≈ 20 × so viele Effekte/s wie eine Weckung je Stream.

### 4.10.2 Bausteine

**Partition.** Jede replaybare Projektion hat `P` Partitionen (E4: 8, Umgebung `Konsum.Partitionen`). Ein Stream gehört genau einer:
`partition = letztes Byte der UUID (RFC-Reihenfolge) mod P` — in SQL `get_byte(uuid_send(stream_id), 15) % P`, in .NET
`id.ToByteArray(bigEndian: true)[15] % P`. Beide Seiten rechnen dasselbe, ohne Index und ohne Hash-Abhängigkeit von Laufzeit oder PG-Version;
für zufällige und abgeleitete (gehashte) Ids gleichverteilt. `P` ≤ 256; ein Wechsel von `P` ist ein Neuaufbau.

**Partitions-Leser** (`BatchLeser`, virtueller Cluster-Actor, Kind `batch-<Projektion>`, Identität = Partition). Single-Activation im Cluster;
zusätzlich Fencing über die Version des Partitions-Checkpoints (G-4, unten). Ein Durchlauf:
1. **Stand laden:** `PartitionsCheckpoint { Id = "<projektion>:<p>", Position (globale Sequenz), Version (OCC), Geparkt (Stream → Version) }`.
2. **Seite lesen:** Events mit `seq_id > Position`, Partition `p`, **Typ ∈ abonnierte Typen der Projektion**, `order by seq_id limit N`
   (N = `Konsum.BatchGroesse`, Vorschlag 500). Der Typfilter nimmt die Marten-Diskriminatoren der `SubscribedTypes` **und aller früheren
   Versionen, die per Upcasting in sie münden** (aus der generierten Upcasting-Tabelle); ohne verlässliche Liste: ohne Typfilter lesen (richtig,
   nur langsamer). Neues Leseprimitiv `IEventStoreRepository.ReadPartitionAsync(nach, p, P, typen, limit)` — Marten-LINQ mit `MatchesSql` für den
   Partitionsfilter, Materialisierung wie `ReadStreamAsync` (Upcasting, Tombstones überspringen).
3. **Guard je Stream:** die `ProjectionCheckpoint`s der Streams der Seite in **einer** Abfrage laden (`id = any(@ids)`); Events mit
   `AggregateVersion <= Guard` überspringen (Nachzügler-Wiederholung, Übergang aus dem alten Pfad).
4. **Anwenden:** je Stream in Versionsreihenfolge, über den generierten `DispatchAsync` (unverändert). Der Store puffert Effekte vieler Streams;
   vor jedem Event setzt der Leser den Stream des Events (`Stream(streamId)`), die Schreib-Regel je Dokument (§4.3) prüft weiter gegen diesen.
5. **Commit (eine Transaktion):** alle Effekte, die Guards aller berührten Streams (Bulk-Upsert) und der Partitions-Checkpoint mit erwarteter
   Version. Konflikt am Partitions-Checkpoint ⇒ ein zweiter Leser oder ein Neuaufbau war schneller ⇒ Batch verwerfen, Stand neu laden (G-4).
6. **Danach:** Versions-Index (Redis) für alle Events des Batches best-effort (Pipeline), wie heute erst nach dem Commit.
7. **Weiter**, solange die Seite voll war; sonst ruhen bis zum nächsten Signal oder Poll.

**Position und Nachzügler.** Die Position rückt nur bis zur höchsten Sequenz vor, deren Event älter als die Karenz ist (DB-Uhr, 3 s — dieselbe
Regel wie heute im `Poller`). Jüngere Events werden trotzdem angewandt (Latenz), der nächste Durchlauf liest sie erneut, der Guard überspringt
sie. So überholt die Position keine noch unsichtbare kleinere Sequenz.

**Weckung.** Das Signal weckt die Partition des Streams (`Wake` an `batch-<Projektion>/<p>`), coalesced: läuft ein Durchlauf, merkt sich der Leser
„noch einmal“. Der Poll weckt alle 30 s jede Partition (Backstop für verlorene Signale) — kein Heil-Poll je Stream mehr (PF-2 erledigt).

**Neuaufbau.** Partitions-Checkpoints und Guards einer Projektion löschen; die Leser lesen ab 0 seitenweise (Strom statt Liste,
`ProjectionRebuilder` und `ProjektionsStandDienst` setzen die Partitions-Checkpoints zurück).

**Rückstand.** `/health` und `/monitoring/metrics` melden je Projektion die Summe über die Partitionen: relevante Events jenseits der Position, und
die geparkten Streams.

### 4.10.3 Die drei Risiken und ihre Lösung

1. **Poison-Event darf keine Partition anhalten.** Wirft der Dispatch: Batch verwerfen, **halbieren** (nach Streams) und die Hälften einzeln
   versuchen, bis der schuldige Stream allein steht. Dieser wird **geparkt** (`Geparkt[stream] = Version vor dem Event`, im selben Commit wie der
   Rest); seine weiteren Events überspringt der Leser. Ein geparkter Stream wird bei jedem Poll einzeln versucht und fällt aus `Geparkt`, sobald er
   durchläuft. Garantie wie heute: nichts geht verloren, andere Streams laufen weiter; neu: der Stand ist sichtbar (`/health`, Dead-Letter einmal je
   Parken).
2. **Konflikt an geteilten Dokumenten** (`SchreibKonfliktException`): wie Poison halbieren; ein einzelner Stream wird bis zu 5-mal gestreut
   wiederholt (heutige Regel), dann bleibt er für den nächsten Durchlauf stehen (nicht geparkt — ein Konflikt ist vorübergehend). Innerhalb eines
   Batches sehen sich zwei Streams auf demselben geteilten Dokument über die Identity-Map (read-your-writes) — kein Konflikt.
3. **Emittenten (Reaktionen) bleiben auf dem Pfad je Stream** (E8): ihr Effekt ist ein Command, ihr Cursor rückt je Stream nach bestätigtem Emit
   vor (Phase 1). Ein Cursor je Partition ließe einen unbekannten Emit die ganze Partition halten. Gemessen ist ihr Anteil klein (S5-8: 3 600
   Cursor-Operationen gegen 79 000 Stream-Reads). Der Batch-Leser gilt nur für Konsumenten mit `IProjectionTracker`.

### 4.10.4 Garantien (docs/11) und ihr Nachweis

| Garantie | trägt im Batch-Konsum | Nachweis (neu bzw. umgebaut) |
|---|---|---|
| Wirkung genau einmal je Event (§11.5) | Effekte + Guards + Partitions-Checkpoint in einer Transaktion | I `CoCommitPostgresTests`: Absturz mitten im Batch (nach Effekten, vor Commit) → nichts gültig, Wiederholung genau einmal |
| zwei Aktivierungen (G-4) | Partitions-Checkpoint mit erwarteter Version | I `GarantiePostgresTests.Zwei_Aktivierungen…` ohne Skip (auf den Batch-Leser umgestellt) |
| Ordnung je Stream (§11.4) | ein Stream in genau einer Partition, Versionsreihenfolge, Guard | P: doppelte/ungeordnete Seiten, Nachzügler hinter der Karenz → Reihenfolge 1…n genau einmal |
| Vollständigkeit (§11.5) | Position mit Karenz, Signal + Poll je Partition | I `PollerBackstopPostgresTests` (verlorenes Signal), `SequenceGapPostgresTests` (Nachzügler) auf den Batch-Leser |
| Poison hält niemanden auf | Halbieren + Parken | I: ein Stream wirft dauerhaft → Rest der Partition committet, Stream geparkt, nach Heilung nachgeholt |
| geteilte Dokumente (§11.5) | Versionsprüfung, Halbieren | I `SchreibRegelPostgresTests`, `GeteilteDokumentePostgresTests` gegen zwei Partitions-Leser |
| Neuaufbau (§11.5) | Reset der Partitions-Checkpoints + Guards, Strom | P/I `ProjectionRebuilderTests` |
| Read-your-writes (best-effort) | Index nach dem Commit | E2E Störungslauf |
| Exactly-once Ende zu Ende | — | E2E Höchstlast und Störungslauf |

### 4.10.5 Umbau (Reihenfolge)

1. Abstractions: `PartitionsCheckpoint`; `IProjectionTracker` wird zur Batch-Naht (`BeginneBatch(projektion, partition)`, `Stream(id)`,
   `CommitAsync(position, guards, erwarteteVersion)`), `ICoCommitTracker` bleibt der Typ-Schnitt für GA-1 — je Absicht ein Weg, kein zweiter Tracker.
2. `MartenCoCommitStoreBase`: Puffer für viele Streams, Commit mit Guards (Bulk) und Partitions-Checkpoint (Optimistic Concurrency); Reset auf
   Partitionen.
3. `MartenEventStore.ReadPartitionAsync`; Typ-Diskriminatoren je Projektion (Generator, inkl. Upcasting-Vorgänger).
4. `BatchLeser` (Actor) + Halbieren/Parken; Weckung (Signal → Partition, Poll je Partition); Rückstand für `/health`.
5. Generator `PullPathGenerator`: für replaybare Projektionen `batch-<Typ>`-Kind mit Partitionen statt `pull-<Typ>`; Emittenten unverändert.
6. Rückbau: `SignalAdapterActor`/`ProjectionAdapter`-Pfad und `Poller` für Projektionen; `ProjectionRebuilder`, `ProjektionsStandDienst` umstellen.
7. Umgebung `Konsum` (`Partitionen`, `BatchGroesse`), Doku 03/04/11/12/13.

### 4.10.6 Offene Entscheidungen (vor dem Bau)

| # | Frage | Vorschlag |
|---|---|---|
| E4 | Partitionen je Projektion | 8, Umgebung `Konsum.Partitionen`; Wechsel = Neuaufbau |
| E8 | Reaktionen (Emittenten) im Batch-Leser? | nein, sie bleiben je Stream (§4.10.3 Punkt 3); Plan §3 Schritt 4 entfällt |
| E9 | E5-Ziel S5 ≥ 3 × in Phase 3? | nein: S5 hat keine Projektion, seine Decke ist der Dirigent (PF-4) — das S5-Ziel gilt nach Phase 5; Phase 3 misst S1 ≥ 3 × und S5 ohne Regression |
| E10 | Partitions-Funktion | letztes UUID-Byte mod P (SQL und .NET gleich, ohne Index) |
| E11 | Batch-Größe | 500 Events, Umgebung `Konsum.BatchGroesse` |
