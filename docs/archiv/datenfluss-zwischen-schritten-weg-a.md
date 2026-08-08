# Datenfluss zwischen Schritten — Weg A (sequenziell zuerst, graph-fähig entworfen)

Handover für einen neuen Agenten/eine neue Session. Ziel: einen Prozess-Schritt einen **früheren**
Schritt referenzieren lassen (klassisches Saga-Bedürfnis: „buche GENAU die Reservierung aus Schritt 1"),
über **deterministische Ids** — und zwar so, dass ein späterer **Dataflow-Scheduler / paralleler Treiber /
Planer** ohne Rückbau ergänzt werden kann. Diese Runde läuft der Prozess weiter **strikt nacheinander**;
die *Abhängigkeitskanten* stehen aber schon im Plan.

Grundlage: `docs/spezifikation.md` Kap. 10–13; `docs/uebergabe-prozess-generator.md`;
`docs/domaenen-reinheit-leaks.md`. Memory: `generator-sichtbarkeit-syntax-vs-symbol`, `hang-diagnose-in-memory`.

---

## 0. Ausgangsstand (GRÜN — das erbst du)

Der Prozess-Pilot ist **generiert und mehr-prozess-fähig**:
- **Generator** `Domain.SourceGeneration/ProzessAggregatGenerator.cs` emittiert pro `IProzessPlan` das
  selbst-enthaltene Prozess-Aggregat; Typnamen sind **pro Prozess eindeutig präfixiert**
  (`Ueberweisung_StarteProzess`, `Ueberweisung_ProzessGestartet`, …) und tragen `IProzessIntern`
  (aus dem Proto-Mapping ausgeschlossen). Deshalb koexistieren beliebig viele Prozesse (DTO/Registry/
  Marten-Alias kollisionsfrei).
- **Wiring** `Infrastructure.SourceGeneration/ProzessWiringGenerator.cs` → `AddGeneratedProzesse()`
  (Treiber-Kind + Signal-Pull-Pfad pro Plan).
- **Generischer Treiber** `Infrastructure/Prozess/ProzessTreiber.cs` (`ProzessTreiber<TProzess>`), liest die
  gefaltete Sicht `Abstractions/IProzessSicht.cs`; **sequenziell, Single-Writer** (`while`-Schleife,
  await pro Schritt). Actor: `Infrastructure/Prozess/TreiberActor.cs` (der (A)-Hang-Fix: `system.Cluster()`
  in der Spawn-Factory, bounded Token — NICHT anfassen).
- **Start-Bindung** yieldet den PLAN (`Domain.Projections/Ueberweisungen.cs`); der `HandlerOutputRouter`
  leitet ProzessId ab und startet (Leak 2 erledigt).
- **Zwei Beispiel-Prozesse:** `Domain/Ueberweisung/UeberweisungsPlan.cs` (linear, feste Schritte),
  `Domain/Sammelueberweisung/SammelueberweisungsPlan.cs` (dynamische Schrittzahl). Ziel-Aggregat
  `Domain/Konto/`.
- **Verträge (Abstractions):** `IProzessPlan.cs` (mit `ProzessSchritte` + `.Dann`), `IProzessSchrittCommand.cs`
  (`Vorgang` + `MitVorgang`), `ProzessId.cs` (`Für`/`FürSchritt`/`FürRückabwicklung`), `IProzessSicht.cs`.

**Baseline verifizieren (Docker läuft: Postgres/Consul/Redis):**
```
dotnet test Infrastructure.Pruefstand.Tests                 # 58/58
dotnet test Infrastructure.Integration.Tests                # 9/9 (sequentiell)
dotnet build Host.Grpc                                       # 0 Fehler
```
Hinweis: `Domain.Client` baut vorbestehend NICHT (`_publish`-Refactoring) — unabhängig, ignorieren.
Alle Solution-Fehler außerhalb `Domain.Client` müssen 0 sein.

**Offen (nicht dein Scope, aber kennen):** Leak 1 (`Vorgang`/Dedup-Mechanik noch im `Konto`-Fachcode,
`docs/domaenen-reinheit-leaks.md`). Weg A ist davon unabhängig.

---

## 1. Das Problem konkret

Ein Schritt kann heute keinen **früheren** Schritt referenzieren. Kanonisch: eine Reservierung mit **Id**.
- Schritt 1 `ReserviereBetrag` reserviert unter der Korrelation `Vorgang₁`.
- Schritt 3 `BucheReservierung` müsste **genau** die Reservierung `Vorgang₁` buchen — nicht „irgendeine".

Warum es nicht geht, steht direkt in der API:
```csharp
public ProzessSchritte Schritte { get; }   // Property OHNE Eingang
```
`Schritte` ist eine **pure Funktion der Record-Felder** (Spec 10.3, crash-deterministisch). Die einzelnen
`.Dann(cmd, …)` bekommen **fixe** Commands, die kein Ergebnis und keine Id eines anderen Schritts sehen.

**Der Hebel:** Die Schritt-Ids sind **deterministisch** (`Vorgang_n = ProzessId.FürSchritt(prozId, n)`).
Deshalb muss Schritt 3 NICHT auf die Laufzeit-Ausgabe von Schritt 1 warten, um sie zu *kennen* — er kann
`Vorgang₁` vorab ableiten. Wir brauchen also KEINEN Laufzeit-Datenfluss (das wäre „Weg B", größer und
determinismus-brechend), sondern eine **deterministische Kante** „Schritt 3 hängt von Schritt 1 ab", die
der Treiber beim Senden auflöst. Das bleibt eine reine Funktion der Record-Felder — Spec 10.3 hält.

---

## 2. Der Entwurf (Weg A)

### 2.1 Leitprinzip (das MUSS tragen)
1. **Deterministische Kante statt Laufzeit-Ausgabe.** Ein abhängiger Schritt referenziert einen früheren
   über dessen (vorab ableitbaren) `Vorgang`. Kein Warten „bis Ergebnis vorliegt" zum *Kennen* der Id.
2. **Der Plan trägt die Kanten EXPLIZIT** — auch wenn der Treiber sie diese Runde nur linear abarbeitet.
   Genau das macht den späteren Scheduler additiv: der Graph ist schon da, nur das Scheduling fehlt.
3. **Kanten zeigen nur RÜCKWÄRTS** (auf kleinere Schritt-Indizes). Damit ist die lineare Reihenfolge eine
   gültige topologische Ordnung → der sequenzielle Treiber ist korrekt, und der Graph ist garantiert ein DAG.
4. **Crash/Determinismus unverändert:** aufgelöste Referenz = `ProzessId.FürSchritt(prozId, k)`,
   deterministisch → Re-Send nach Crash identisch → Ziel dedupliziert. Kein neues Exactly-once-Problem.
5. **Kompensation unverändert** (Reverse-Order; das Gegen-Command darf symmetrisch ebenfalls Refs nutzen).

### 2.2 Kandidaten-API (Startpunkt — der Agent darf verfeinern, das MUSS aber erfüllt bleiben)
Kern ist eine Erweiterung von `ProzessSchritte` (`Abstractions/IProzessPlan.cs`). Ein Schritt wird von einem
festen Command zu einem **Command-Builder über aufgelöste Referenzen** + trägt seine **Kanten**:

```csharp
// Vom Treiber gefüllt: Schritt-Index → dessen deterministischer Vorgang.
public readonly struct SchrittRefs
{
    // intern kennt der Treiber prozId; Von(k) = ProzessId.FürSchritt(prozId, k)
    public Guid Von(int schrittNr);
}

public sealed class Schritt
{
    public Func<SchrittRefs, ICommand> Baue;              // Command aus aufgelösten Refs
    public Func<SchrittRefs, ICommand>? BaueRueckgaengig; // optionales Gegen-Command
    public IReadOnlyList<int> AbhaengigVon;               // die KANTEN — explizit, für den späteren Scheduler
}

public sealed class ProzessSchritte
{
    public IReadOnlyList<Schritt> Alle { get; }

    // Rückwärtskompatibel: bestehende Pläne (feste Commands, keine Refs) bleiben unverändert gültig.
    public ProzessSchritte Dann(ICommand schritt, ICommand? rückgängig = null);

    // Neu: abhängiger Schritt — Builder + explizite Kanten (Rückwärts-Indizes, 1-basiert).
    public ProzessSchritte Dann(
        Func<SchrittRefs, ICommand> schritt,
        Func<SchrittRefs, ICommand>? rückgängig = null,
        params int[] abhängigVon);
}
```

Beispiel-Plan (das Abnahme-Vehikel):
```csharp
public record UeberweisungMitReservierungsIdPlan(Guid Quelle, Guid Ziel, decimal Betrag) : IProzessPlan
{
    public ProzessSchritte Schritte => ProzessSchritte.Start
        .Dann(new ReserviereBetrag(Quelle, Betrag), rückgängig: new GebeReservierungFrei(Quelle, Betrag)) // 1
        .Dann(new SchreibeGut(Ziel, Betrag),        rückgängig: new StorniereGutschrift(Ziel, Betrag))     // 2
        .Dann(refs => new BucheReservierung(Quelle, Betrag, reservierung: refs.Von(1)), abhängigVon: 1);    // 3
}
```

### 2.3 Treiber (diese Runde: weiter sequenziell)
`ProzessTreiber<TProzess>` (`Infrastructure/Prozess/ProzessTreiber.cs`): pro Schritt `n`
- baue `SchrittRefs`, das `Von(k)` auf `ProzessId.FürSchritt(prozId, k)` auflöst,
- `cmd = schritt.Baue(refs)`, dann wie bisher `.MitVorgang(ProzessId.FürSchritt(prozId, n))`, senden, Quittung.
- Reihenfolge bleibt linear (`n = QuittierteSchritte + 1`); `AbhaengigVon` wird **gespeichert, aber noch nicht
  zum Scheduling** benutzt. (Das ist der Andockpunkt für den späteren Scheduler — nicht wegoptimieren.)

`IProzessSicht.Schritte` liefert weiterhin die `ProzessSchritte`; der Treiber liest `Alle[n-1]`.

### 2.4 Ziel-Aggregat (Domänen-Arbeit im Beispiel — als Anwender schreiben)
Damit „buche Reservierung `Vorgang₁`" fachlich trägt, muss `Konto` Reservierungen **per Id** führen:
`BetragReserviert` trägt die Reservierungs-Id (= der Vorgang des Reserve-Schritts); `BucheReservierung`
nimmt ein Feld `reservierung` und bucht **genau diese**. Das ist normale Domänen-Modellierung (kein
Framework-Eingriff) — der Agent schreibt sie wie ein Anwender. (Wähle das klarste Beispiel; die Reservierung-
mit-Id ist kanonisch, ein anderes darf es auch sein, solange es eine echte Rückwärts-Kante braucht.)

---

## 3. Was ändern — und was NICHT

**Ändern:**
- `Abstractions/IProzessPlan.cs` — `ProzessSchritte`/`Schritt`/`SchrittRefs` + neues `.Dann` (Kanten + Builder).
- `Infrastructure/Prozess/ProzessTreiber.cs` — Refs auflösen + injizieren (Vorwärts UND Rückwärts/Kompensation).
- Domänen-Beispiel: neues Ziel-Verhalten in `Domain/Konto/` (Reservierung per Id) + neuer Plan + Start-Bindung.
- Tests (Prüfstand-Treiber-Test + ggf. ein Integrationstest).

**NICHT ändern (begründet):**
- **`ProzessAggregatGenerator` bleibt unberührt.** Schritte sind Plan-Sache; das Prozess-Aggregat kennt nur
  `ProzessGestartet`(Plan-Felder) → rekonstruiert den Plan → `Schritte` rechnet sich neu (pure Funktion). Die
  Kanten/Builder sind Teil dieser Rechnung, nicht des Aggregat-Logs. **Verifiziere** das (Aggregat-Generat baut
  ohne Änderung).
- Der (A)-Hang-Fix im `TreiberActor` (Spawn-Factory-`Cluster()`, bounded Token).
- Eindeutige Namens-/`IProzessIntern`-Mechanik (Mehr-Prozess-Fähigkeit).

---

## 4. Explizit SPÄTER (aber jetzt nicht verbauen)

Der Endzustand ist ein **Dataflow-Prozess**: der Plan IST der Abhängigkeitsgraph; ein **Scheduler/paralleler
Treiber** fächert unabhängige Knoten (verschiedene Ziel-Aggregate!) gleichzeitig aus und ordnet nur entlang der
Kanten. Das ist echte Parallelität **zwischen** Aggregaten (actor-modell-konform) — kein Prozess bindet Aggregate
zusammen, der heutige sequenzielle Treiber *serialisiert nur künstlich*.

**Diese Runde liefert das NICHT** — aber der Entwurf muss es additiv erlauben:
- Kanten stehen explizit im Plan (`AbhaengigVon`), rückwärts-only → DAG.
- Der Treiber liest schon aus einer „was ist dran"-Sicht; halte dieses Konzept so, dass „dran = alle
  Abhängigkeiten quittiert" (Graph-Ready-Set) später den linearen Zähler ersetzen kann, ohne Plan/Aggregat zu ändern.
- Kein Vertrag (Plan-API, `IProzessSicht`) darf Linearität *voraussetzen*; er darf sie nur *noch nicht ausnutzen*.

Später separat: `ProzessSchritte` um explizite Parallel-Gruppen ODER rein datenfluss-getriebene Ableitung;
Scheduler mit Fan-out/Join; Kompensation über parallele Zweige; Planer/Execution-Graph-Visualisierung.

---

## 5. Stolpersteine (aus der Vorsession — PFLICHT)
- **Stale Generator-State:** `obj/generated/**` lügt. Bei Zweifel `rm -rf <Projekt>/obj <Projekt>/bin`,
  `--no-incremental`. Kompilierte Ausgabe nur mit `/p:EmitCompilerGeneratedFiles=true`.
- **Determinismus ist die Korrektheit:** `Von(k)` MUSS `ProzessId.FürSchritt(prozId, k)` sein (dieselbe
  Ableitung wie `MitVorgang`), sonst bricht die Crash-Heilung.
- **Rückwärts-Kanten erzwingen** (Index < aktueller Schritt); Vorwärts-/Zyklus-Referenz = Fehler (kein DAG).
- **Kompensation:** wenn ein Gegen-Command Refs braucht, symmetrisch über `BaueRueckgaengig(refs)` — die
  Reverse-Order-Logik im Treiber bleibt.
- **In-memory zuerst** (Fake-Cluster, Prüfstand), dann EINMAL Integration (Docker, sequentiell). Verteilte
  Effekte nie im Integrationstest raten.
- **Proto-Regen** nur bei neuen NICHT-internen Command/Event-Typen. Neue `Konto`-Events/Commands brauchen DTOs
  (`dotnet run --project Proto.SourceGeneration` → ProtoRepo → Infrastructure). Prozess-interne Typen sind
  `IProzessIntern` (ausgeschlossen) — aber `Konto`-Typen NICHT, die brauchen Proto.

---

## 6. Verifikation & Abnahme-Tor
1. **In-memory:** ein Prüfstand-Treiber-Test mit dem Reservierungs-Id-Plan (Fake-Cluster) beweist:
   Schritt 3 bucht GENAU die Reservierung aus Schritt 1 (Happy Path); Crash zwischen Send/Quittung heilt ohne
   Doppeleffekt (deterministische Ref); Kompensation gleicht die richtige Reservierung aus.
2. **Bestehendes bleibt grün:** die alten Pläne (`UeberweisungsPlan`, `SammelueberweisungsPlan`) laufen
   unverändert über das rückwärtskompatible `.Dann` → **Prüfstand ≥ 58/58**, **Integration 9/9**.
3. **Einmal live:** ein Integrationstest treibt den Reservierungs-Id-Plan end-to-end (oder der bestehende
   E2E bleibt grün + ein neuer für den Datenfluss).
4. **Generat unberührt:** `ProzessAggregatGenerator`-Ausgabe baut ohne Änderung; Host.Grpc 0 Fehler.
5. **Forward-Check (Design-Abnahme):** die Kanten (`AbhaengigVon`) sind im Plan abrufbar und rückwärts-only;
   dokumentiere in 2–3 Sätzen, wie der spätere Scheduler sie liest — als Beleg, dass nichts verbaut ist.

## 7. Leitplanken
- **Nichts neu erfinden am Determinismus** — `ProzessId`/`Vorgang` 1:1.
- **Sequenziell jetzt, Graph-fähig entworfen** — Kanten speichern, nicht nutzen; keine Linearitäts-Annahme im Vertrag.
- **Kein Prozess bindet Aggregate** — die Refs sind logische Kanten, keine Transaktion/kein Lock über Aggregate.
- **Rückwärtskompatibel** — bestehende `.Dann(cmd, rückgängig)`-Pläne bleiben wortgleich gültig.
- **Verteilte Hangs in-memory beweisen**, nie im Integrationstest raten.

Bevor du Code schreibst: fasse dein Verständnis in 3–5 Sätzen zusammen, skizziere den konkreten Plan
(Dateien, Reihenfolge, welcher Test zuerst) und warte auf OK. Dann in kleinen, einzeln verifizierten Schritten.

---

## 8. UMGESETZT (diese Runde) + Forward-Check

**Entscheidung:** Reservierung wurde **vereinheitlicht id-basiert** (statt additiv). Dadurch ist der
bestehende `UeberweisungsPlan` selbst das Datenfluss-Beispiel — Schritt 3 bucht GENAU die Reservierung
aus Schritt 1 über `refs.Von(1)`. Ein separater `…MitReservierungsIdPlan` wäre ein Quasi-Duplikat und
entfiel bewusst.

**Was steht:**
- `Abstractions/IProzessPlan.cs`: `SchrittRefs` (`Von(k) = ProzessId.FürSchritt(prozId, k)` — identisch zu
  `MitVorgang`), `Schritt` (`Baue`/`BaueRueckgaengig`/`AbhaengigVon`), `ProzessSchritte.Alle → IReadOnlyList<Schritt>`.
  Zwei `.Dann`: fixe Ergonomie-Form (keine Kanten) + Builder-Form mit `params int[] abhängigVon`, die
  **rückwärts-only erzwingt** (`1 ≤ k < aktuelle Nr`, sonst `ArgumentException` → garantierter DAG).
- `Infrastructure/Prozess/ProzessTreiber.cs`: löst Refs beim Senden auf (`new SchrittRefs(prozessId)`),
  vorwärts UND Kompensation; Reihenfolge bleibt strikt linear; `AbhaengigVon` wird **gespeichert, nicht gelesen**.
- `Domain/Konto/`: `OffeneReservierungen` (Id→Betrag); `Buche`/`GebeReservierungFrei` treffen per Id
  (`ReservierungNichtGefunden` als fachliche Ablehnung). Beide Bestandspläne migriert (Reserve-Kompensation
  und Schluss-Buchung referenzieren `refs.Von(1)`).
- Beweise: Prüfstand 63/63 (neu: `DatenflussZwischenSchrittenTests` — Köder-Reservierung gleicher Höhe;
  nur die deterministische Referenz trifft die richtige; + Kompensation-Präzision + Crash-Heilung bei Schritt 3);
  Integration 10/10 (neu: `Datenfluss_Schritt_3_bucht_live_GENAU_die_Reservierung_aus_Schritt_1` — das gebuchte
  Event trägt live `ProzessId.FürSchritt(prozId, 1)`). Host.Grpc 0 Fehler. `ProzessAggregatGenerator` unberührt
  (liest nur `Alle.Count`) — baut unverändert.

**Forward-Check (wie der spätere Scheduler die Kanten liest):** Der Graph liegt schon im Plan — jeder
`Schritt.AbhaengigVon` ist die Menge seiner Rückwärts-Kanten. Ein Dataflow-Scheduler ersetzt den heutigen
linearen Zähler (`n = QuittierteSchritte + 1`) durch ein **Ready-Set**: „dran sind alle noch offenen Schritte,
deren `AbhaengigVon` vollständig quittiert ist". Weil die Kanten rückwärts-only sind (DAG), ist die heutige
lineare Reihenfolge bereits eine gültige topologische Ordnung → der Scheduler ist eine **additive**
Verallgemeinerung, kein Umbau: `SchrittRefs`/`Baue`/`IProzessSicht`/das Aggregat bleiben wortgleich, nur die
„was ist dran"-Auswahl im Treiber wird graph- statt zähler-basiert. Die dann herausfallende Parallelität ist
echte Parallelität ZWISCHEN Aggregaten (verschiedene Ziel-Konten) — kein Prozess bindet Aggregate zusammen.
