# Domänen-Reinheit: die drei Leaks & ihre Auflösung (umsetzbar)

Arbeitsauftrag für einen umsetzenden Agenten. Ziel: die Prozess-Domäne von **technischem
Wissen befreien**, das dort nicht hingehört (Invariante 5 — „Cursor, Signal, Ordnung,
Exactly-once, Sharding, Prozess-Maschinerie tauchen im Entwickler-Code **nie** auf").

Der Befund ist eine Bestandsaufnahme des Überweisungs-Piloten (Phase 5). Der **Plan selbst
ist bereits sauber** (`Domain/Ueberweisung/UeberweisungsPlan.cs` — reine Beschreibung). Drei
Oberflächen *um ihn herum* tragen aber Mechanik im Fachgewand. Dieses Doc benennt sie, zeigt
das Zielbild und den konkreten Generatoren-/Datei-Weg.

> **Kontext-Doku (zuerst lesen):** `CLAUDE.md` (Phase-5-Block), `docs/uebergabe-prozess-generator.md`
> (der Aggregat-/Treiber-Generator — dieses Doc setzt darauf auf), `docs/spezifikation.md` Kap. 11
> (Prozess-Maschinerie, insb. 11.3 Determinismus, 11.4 Empfänger-Dedup).

---

## 0. Reihenfolge & Abhängigkeiten (PFLICHT zuerst)

1. **Voraussetzung:** Der Aggregat-/Treiber-Generator aus `docs/uebergabe-prozess-generator.md`
   ist fertig und grün (Prüfstand 56/56, Integration 9/9). Leak 2 **hängt davon ab** (braucht
   das generierte `StarteProzess` + die Plan→Start-Registry). Leak 1 nicht zwingend, aber die
   Co-Commit-Variante fasst den Aggregat-Append-Pfad an → sauberer *nach* dem stabilen Tor.
2. **Empfohlene Reihenfolge:** erst **Leak 1** (rein additiv, hebt uns bei der Wirkungs-Garantie
   auf „Framework liefert den Schlüssel, Wache generiert"), dann **Leak 2** (berührt die Emit-
   Signatur → mehr Blast-Radius). **Leak 3** ist eine bewusste Nicht-Änderung (siehe unten).
3. **Nach jedem Leak** die volle Abnahme fahren (Abschnitt „Abnahme" je Leak). Kein Leak wird
   „nebenbei" mit einem anderen vermischt.

### Verifikations-Disziplin (identisch zur Pilot-Session)
- **In-memory zuerst** (`Infrastructure.Pruefstand.Tests`, kein Docker). Verteilte Effekte im
  Fake-Cluster beweisen, nie im langsamen Integrationstest raten.
- **Erst dann** `Infrastructure.Integration.Tests` (Docker: Postgres/Consul/Redis) — **sequentiell**
  (`xunit.runner.json` schaltet Parallelität ab).
- **Stale Generator-State:** `obj/generated/**` lügt. Bei Zweifel `rm -rf <Projekt>/obj <Projekt>/bin`
  und `dotnet build --no-incremental`. Kompilierte Ausgabe landet nur mit
  `/p:EmitCompilerGeneratedFiles=true` unter `obj/Debug/net9.0/generated/**`.
- **Proto-Regen** bei JEDEM neuen Command/Event-Typ: `dotnet run --project Proto.SourceGeneration`
  → `dotnet build ProtoRepo` → `dotnet build Infrastructure`. Fehlt der DTO, bricht der
  `DtoMapperGenerator` mit „`{Name}Dto` nicht gefunden". **Ausnahme:** rein interne Marker-Events
  (siehe Leak 1) werden wie Signale aus dem DtoMapper ausgeschlossen.

---

## Leak 1 — `Vorgang` + Dedup-Wache: Exactly-once-Mechanik im Fachcode

### Symptom (Ist-Zustand)
Jeder Schritt-Command, jedes Schritt-Event und jeder Schritt-Decider fädelt die Korrelations-Id
`Vorgang` von Hand durch:

```csharp
// Domain/Konto/Commands.cs — Boilerplate in JEDEM Schritt-Command
public record ReserviereBetrag(Guid AggregateId, decimal Betrag, Guid Vorgang = default)
    : IProzessSchrittCommand
{ public IProzessSchrittCommand MitVorgang(Guid v) => this with { Vorgang = v }; }

// Domain/Konto/Events.cs — Vorgang NUR wegen Dedup im Event
public record BetragReserviert(Guid Vorgang, decimal Betrag) : IEvent;

// Domain/Konto/Decider.cs — dieselbe Zeile in JEDEM Schritt-Decider
if (this.State.VerarbeiteteVorgaenge.Contains(cmd.Vorgang)) yield break;

// Domain/Konto/Konto.cs — die Dedup-Menge + (in Applier.cs) das Falten in JEDEM Applier
public HashSet<Guid> VerarbeiteteVorgaenge { get; } = new();
```

### Warum das ein Leak ist
`Vorgang` ist **keine Kontodomäne** — es ist die Exactly-once-Korrelation der Prozess-Maschinerie
(Spec 11.4). Diese Domäne dedupliziert **nie** nach einem fachlichen Schlüssel, immer nur nach dem
generischen `Vorgang` → es ist reine Mechanik. So halten es andere Frameworks bewusst *nicht*:
NServiceBus/MassTransit/Wolverine kapseln das in einer transaktionalen **Inbox/Outbox** (Dedup per
Message-Id, gemeinsam mit dem Zustand committet); Temporal *liefert* den Idempotency-Key und hält die
Wache minimal. Wir sind für Event-Sourcing sogar besonders gut aufgestellt (der Append **ist** die
Transaktion → das Aggregat **ist** die ES-native Inbox) — wir **zeigen** die Mechanik nur, statt sie
zu generieren.

### Zielbild (was der Entwickler noch schreibt)
```csharp
// Command: kein Vorgang, kein MitVorgang mehr — nur die fachlichen Felder + ein Marker
public partial record ReserviereBetrag(Guid AggregateId, decimal Betrag) : IProzessSchritt;

// Event: kein Vorgang mehr
public record BetragReserviert(decimal Betrag) : IEvent;

// Decider: KEINE Dedup-Zeile mehr — nur die fachliche Regel
public IEnumerable<OneOf<BetragReserviert, KontoGesperrt, DeckungReichtNicht>> Decide(ReserviereBetrag cmd)
{
    if (this.State.Gesperrt)                { yield return new KontoGesperrt(cmd.AggregateId); yield break; }
    if (this.State.Verfuegbar < cmd.Betrag) { yield return new DeckungReichtNicht(this.State.Verfuegbar, cmd.Betrag); yield break; }
    yield return new BetragReserviert(cmd.Betrag);
}

// State: KEINE VerarbeiteteVorgaenge mehr — der Marker IProzessTeilnehmer genügt
public partial class Konto : IState, IProzessTeilnehmer { /* nur Saldo/Reserviert/Gesperrt */ }
```

### Lösung (empfohlen: Framework-eigene Dedup-Marke, „co-commit")
Der Kern: die Wirkungs-Dedup wird zur **Framework-Naht** — dieselbe Philosophie wie der Lese-seitige
`IProjectionTracker` (Effekt + Marke in EINER nativen Transaktion). Konkret:

1. **Neue Verträge (`Abstractions`):**
   - `IProzessSchritt : IProzessSchrittCommand` — Autoren-Marker; der Generator ergänzt `Vorgang`/`MitVorgang`.
   - `IProzessTeilnehmer` — Aggregat-Marker: „dieses Aggregat nimmt an Prozessen teil, dedupliziert Schritte".
   - `record ProzessSchrittMarke(Guid Vorgang) : IEvent` — die interne Dedup-Marke (persistent, aber
     **domänen-fremd**). Wie Signale **aus dem DtoMapper ausschließen** (interner Typ, kein Proto) — siehe
     `DtoMapperGenerator`-Ausschlussliste (dort stehen schon die Signale).

2. **Command-Mechanik generieren (`Domain.SourceGeneration/ProzessSchrittCommandGenerator.cs`, syntax-basiert):**
   findet jedes `record … : IProzessSchritt` und emittiert die `partial`-Ergänzung:
   ```csharp
   public partial record ReserviereBetrag : IProzessSchrittCommand
   {
       public Guid Vorgang { get; init; }
       public IProzessSchrittCommand MitVorgang(Guid v) => this with { Vorgang = v };
   }
   ```
   → `Vorgang`/`MitVorgang` verschwinden aus der Autoren-Datei, bleiben aber typkompatibel zum Treiber.

3. **Dedup-Wache + Marke im gemeinsamen Handler-Pfad (KEIN Decider-Eingriff):**
   die Wache gehört **nicht** in jeden Decider, sondern EINMAL in den gemeinsamen Command→Event-Pfad.
   Ort: `AggregateHandlerBase` (grep: `class AggregateHandlerBase`) bzw. die generierte
   `{State}AggregateHandler`-Hülle. Logik:
   - **vor** `Decide`: wenn `command is IProzessSchrittCommand c` **und** `state is IProzessTeilnehmer p`
     **und** `p` hat `c.Vorgang` bereits verarbeitet → **leere Eventliste** (Noop), fertig.
   - **nach** erfolgreichem `Decide` (mind. ein persistentes Event): zusätzlich `new ProzessSchrittMarke(c.Vorgang)`
     an die Eventliste hängen → landet im **selben** Append (eine Transaktion, co-commit).
   - Die Verarbeitet-Menge lebt als generierter Zustand auf `IProzessTeilnehmer`-Aggregaten (der
     `StatePropertyGenerator`/ein kleiner Zusatz emittiert `HashSet<Guid> VerarbeiteteVorgaenge`); ein
     generierter Applier-Zweig faltet `ProzessSchrittMarke` in die Menge. So bleibt sie **aus dem Log
     rekonstruierbar** (Crash-fest), ohne dass Domänen-Events `Vorgang` tragen.

   > **Warum eine eigene Marke statt Vorgang im Domänen-Event?** Damit die Domänen-Events (`BetragReserviert(decimal)`)
   > frei von Mechanik bleiben und trotzdem die Menge foldbar ist. Genau das „Marke neben dem Effekt, eine
   > Transaktion" ist der exactly-once-Nahtpunkt (Spec „Exactly-once — die ehrliche Aussage").

### Leichtere Alternative (falls der Append-Pfad zu riskant ist)
Nur Schritt 2 umsetzen (Command-Mechanik generieren): `Vorgang`/`MitVorgang` verschwinden aus dem
Command, **aber** Events tragen weiter `Vorgang` und die Dedup-Zeile bleibt im Decider. Kleiner, weniger
sauber — dokumentiere die Entscheidung, falls du sie wählst.

### Fallstricke
- `ProzessSchrittMarke` ist ein **neues persistentes Event** → entweder DtoMapper-Ausschluss (empfohlen,
  wie Signale) oder Proto-DTO erzeugen. Bei Ausschluss: sicherstellen, dass `AppendEventsAsync`/Marten das
  Falten nicht behindert (die Marke wird geschrieben, aber nie an Fremd-Konsumenten gemappt).
- `IProzessTeilnehmer` muss der State tragen — ein `partial`-Zusatz darf `VerarbeiteteVorgaenge` **nicht**
  doppelt deklarieren (der Autor deklariert sie im Zielbild NICHT mehr → Generator ist alleiniger Erzeuger).
- Records + generierte `init`-Property: die Autoren-Deklaration MUSS `partial record` sein, sonst kann der
  Generator nicht ergänzen.

### Abnahme
- `Infrastructure.Pruefstand.Tests/Phase5/KontoAggregatTests.cs` + `UeberweisungsTreiberTests.cs` bleiben
  **grün** — insbesondere `Crash_zwischen_Send_und_Quittung_wird_geheilt_ohne_Doppeleffekt` und
  `…nichts_wird_radiert` (die Dedup-Beweise). Nur Konstruktoren/Feldnamen dürfen sich ändern (der Test darf
  `Vorgang` nicht mehr am Command setzen müssen; wo er es tut → auf `MitVorgang` oder Marker umstellen).
- Prüfstand vollständig grün, dann Integration `ProzessTreiberE2ETests` einmal (sequentiell).
- Host.Grpc bootet ohne Exceptions; kein `ProzessSchrittMarke`-Mapping-Fehler.

---

## Leak 2 — Start-Bindung: Determinismus & Prozess-Start von Hand — ✅ ERLEDIGT (2026-08-06)

> **Umgesetzt wie unten beschrieben.** Emit-Signatur `Func<IMessagePayload,Task>` → `Func<IPipelineOutput,Task>`
> in `SubscriberDispatchGenerator` (+ Cast `(IPipelineOutput)oneOf.Value`) und `DetachedEmit`; Plan-Arm im
> `HandlerOutputRouter` (`IProzessPlan` → `ProzessId.Für(...)` + generiertes `StarteProzess`); die Plan→Start-
> Registry `GeneratedProzessStarts.StartFür` emittiert der `ProzessAggregatGenerator` (public, weil aus
> Infrastructure referenziert). `Ueberweisungen.Handle` yieldet jetzt `UeberweisungsPlan` — kein ProzessId/
> Envelope/StarteProzess mehr im Fachcode. Determinismus 1:1 (gleiche drei Argumente, nur in den Router
> verschoben). Test-Anpassung: `ReaktionAufPullTests` Fake-Emit-Typ `IMessagePayload` → `IPipelineOutput`
> (reine Typweitung, Assertions unverändert). Beweis: Prüfstand 56/56, Integration 9/9 (E2E mit dem
> vereinfachten Handler grün), Host.Grpc 0 Fehler.



### Symptom (Ist-Zustand)
```csharp
// Domain.Projections/Ueberweisungen.cs
public async IAsyncEnumerable<OneOf<StarteProzess>> Handle(
    UeberweisungBeauftragt evt, IAggregateEnvelope envelope, ProjectionWriter writer)
{
    var version = (envelope as IEventEnvelope)?.AggregateVersion ?? 0;             // Envelope aufbohren
    var prozId  = ProzessId.Für(nameof(UeberweisungsPlan), envelope.AggregateId, version); // Determinismus-Mechanik
    yield return new StarteProzess(prozId, evt.Quelle, evt.Ziel, evt.Betrag);      // Felder von Hand umkopieren
    await Task.CompletedTask;
}
```

### Warum das ein Leak ist
Der Entwickler weiß hier zu viel: dass ein idempotenter Start eine **deterministische Id** aus
`(Plan-Typ, Stream, Version)` braucht, dass die Version im Envelope steckt, dass man `StarteProzess`
statt „den Prozess starten" yieldet. Das ist Exactly-once- und Prozess-Maschinerie im Handler — der
Punkt, an dem wir **leakiger sind als alle anderen** (Axon `@StartSaga`+`associationProperty`, NServiceBus
`ConfigureHowToFindSaga`, MassTransit `CorrelateById` deklarieren Korrelation **einmal an der Grenze**;
das Framework leitet die Instanz-Id ab — kein Hand-Hash).

### Zielbild (was der Entwickler noch schreibt)
```csharp
// Domain.Projections/Ueberweisungen.cs — der Handler yieldet den PLAN
public IAsyncEnumerable<OneOf<UeberweisungsPlan>> Handle(
    UeberweisungBeauftragt evt, IAggregateEnvelope envelope, ProjectionWriter writer)
{
    yield return new UeberweisungsPlan(evt.Quelle, evt.Ziel, evt.Betrag);   // nur fachliche Felder
}
```
Kein `ProzessId`, kein Envelope-Gefummel, kein `StarteProzess`. Das Framework leitet ProzessId + Start ab.

### Lösung (Emit-Signatur auf `IPipelineOutput` weiten + Plan-Arm im Router)
Die Naht ist der Dispatch-/Emit-Pfad. Heute castet er hart auf `IMessagePayload`:

```csharp
// Domain.SourceGeneration/SubscriberDispatchGenerator.cs (Ist)
public async Task DispatchAsync(IAggregateEnvelope envelope, ProjectionWriter writer,
                                Func<IMessagePayload, Task> emit) { … }
//   case …: await emit((IMessagePayload)oneOf.Value);   // ← bricht bei IProzessPlan
```
`IProzessPlan : IPipelineOutput`, aber **nicht** `IMessagePayload` (siehe `Abstractions/Interfaces.cs:22/24`
und `IProzessPlan.cs`). Deshalb:

1. **Emit-Signatur weiten** auf `Func<IPipelineOutput, Task>` und den Cast fallenlassen:
   ```csharp
   // SubscriberDispatchGenerator (Ziel)
   Func<IPipelineOutput, Task> emit
   //   case …: await emit(oneOf.Value);   // IEvent/ICommand/IProzessPlan — der Router entscheidet
   ```
   Betrifft **alle** Aufrufstellen der Signatur (rückwärtskompatibel — IEvent/ICommand sind weiterhin
   `IPipelineOutput`): `HandlerOutputRouter` (grep) und die Pull-Adapter-Dispatch-Verdrahtung im
   `PullPathGenerator.cs` (dort steht das `dispatch`-Lambda mit `router.EmitFor(...)`).

2. **Plan-Arm im `HandlerOutputRouter`** (der Ort, der heute schon IEvent→publish / ICommand→Reaktion
   unterscheidet). Neuer Zweig:
   ```csharp
   case IProzessPlan plan:
       var prozId = ProzessId.Für(plan.GetType().Name, env.AggregateId, env.AggregateVersion);
       var start  = GeneratedProzessStarts.StartFür(plan, prozId);   // generierte Plan→StarteProzess-Fabrik
       // wie eine Reaktion senden: deterministische CommandId → Empfänger-Dedup (Doppelstart verpufft)
       await SendReaktionAsync(start, AggregateType: plan.GetType().Name + "Prozess", ct);
       break;
   ```
   Determinismus **1:1 wie heute** (`ProzessId.Für` mit denselben drei Argumenten) — nur wandert er aus
   dem Handler in den Router.

3. **Plan→Start-Registry generieren** — Zusatz zum Aggregat-Generator aus `uebergabe-prozess-generator.md`:
   `GeneratedProzessStarts.StartFür(IProzessPlan, Guid prozId) → StarteProzess`, das die Plan-Record-Felder
   auf die (ebenfalls generierten) `StarteProzess`-Felder abbildet. Der Generator kennt beide Seiten →
   triviale Zuordnung nach Feld-Reihenfolge/-Name.

### Fallstricke
- Die Signaturweitung ist der Blast-Radius: **jede** `DispatchAsync`/`emit`-Nutzung anfassen (No-op-Lambdas
  im Pull-Adapter bleiben gültig, nur der Delegattyp ändert sich). Erst `grep -r "IMessagePayload, Task"`.
- Der Handler-Rückgabetyp ändert sich zu `IAsyncEnumerable<OneOf<UeberweisungsPlan>>` → `IProzessPlan` muss
  vom `SubscriberDispatchGenerator` als Produced-Type akzeptiert werden (er liest OneOf-Arme generisch —
  prüfen, dass `IProzessPlan`-Arme kein `SubscribedTypes`/Proto-Problem erzeugen; Pläne sind **keine** Events).
- `env.AggregateVersion`: der Router braucht Zugriff auf die Version → `env as IEventEnvelope` **im Framework**
  (der Leak verschwindet aus der Domäne, die Mechanik bleibt korrekt an der Grenze).

### Abnahme
- `ProzessTreiberE2ETests.Ein_Fach_Command_treibt_die_ganze_Ueberweisung_end_to_end` bleibt **grün** mit dem
  vereinfachten Handler (kein manueller Start, kein manuelles Wecken) — der wichtigste Beweis.
- Doppelter Auslöser → gleiche ProzessId → Prozess-Aggregat dedupliziert (unverändert). Falls ein Test das
  prüft, muss er ohne Änderung grün bleiben.
- Prüfstand + Integration grün; Host.Grpc bootet.

---

## Leak 3 — `ITransientEvent` (bewusste NICHT-Änderung)

### Befund
```csharp
public record KontoGesperrt(Guid AggregateId) : ITransientEvent;      // NICHT im Log
public record DeckungReichtNicht(decimal Verfuegbar, decimal Angefordert) : ITransientEvent;
```

### Entscheidung: **behalten, nicht „reparieren".**
Die Trennung „geloggtes Faktum (`IEvent`) vs. Ablehnung (`ITransientEvent`)" ist teils Persistenz-Mechanik,
aber **„eine Absage ist kein historisches Faktum" ist eine legitime Fachaussage**. Sie ist minimal (ein
Interface-Marker), sie verschmutzt keine Signatur, und sie trägt Bedeutung, die der Prozess braucht (die
Absage kommt als `CommandResult.RejectionEvent` zurück und löst die Kompensation aus). Der Aufwand einer
„Bereinigung" stünde in keinem Verhältnis; schlimmer, er würde eine sinnvolle Domänen-Unterscheidung
verwischen.

**Auftrag an den Agenten:** hier **nichts** ändern. Dieser Abschnitt existiert, damit niemand Aufwand in
eine Scheinverbesserung steckt.

---

## Zusammenfassung (Abnahme-Tor gesamt)

| Leak | Status vorher | Auflösung | Blast-Radius |
|------|---------------|-----------|--------------|
| 1 `Vorgang`/Dedup | Mechanik im Fachcode | Marker `IProzessSchritt`/`IProzessTeilnehmer` + generierte `MitVorgang` + co-commit `ProzessSchrittMarke` + generierte Wache/Fold | additiv, aber Append-Pfad |
| 2 Start-Bindung | Determinismus von Hand | Emit-Signatur `IPipelineOutput` + Plan-Arm im Router + Plan→Start-Registry → „yield den Plan" | Signaturweitung (breit) |
| 3 `ITransientEvent` | leichte Persistenz-Mechanik | **behalten** (legitime Fachaussage) | keiner |

**Nach Umsetzung von 1 + 2 gilt:** der Entwickler beschreibt einen kompletten Prozess mit — dem **Plan**
(Record), dem **Ziel-Aggregat** (Fachregeln + Marker `IProzessTeilnehmer`, ohne Vorgang/Dedup-Zeile), dem
**Auslöser** und einer **Start-Bindung, die nur den Plan yieldet**. Exactly-once-Korrelation, Determinismus,
Dedup-Wache und Prozess-Start sind vollständig generiert/framework-seitig — Invariante 5 ist für die
Prozess-Domäne erfüllt.

**Endkontrolle:** Prüfstand 56/56 (+ ggf. neue Marker-Tests), Integration 9/9, Host.Grpc bootet ohne
Exceptions, `git grep -n "Vorgang" Domain/` liefert **keine** Treffer mehr in Autoren-Dateien (nur noch in
generiertem Code), und `Ueberweisungen.Handle` yieldet `UeberweisungsPlan`.
