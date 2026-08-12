# Anleitung: einen komplexen Prozess schreiben (Event-Regel-DAG) — vollständiges Beispiel

Diese Anleitung führt eine **komplette, getestete Bestell-Saga** von der ersten Zeile bis zum laufenden
Prozess. Jede Datei, die der Entwickler schreibt, steht hier vollständig; danach der End-to-End-Fluss vom
einen Fach-Command bis zum Versand.

Der Code liegt real im Repo und ist grün: `Domain/Lager/`, `Domain/Zahlung/`, `Domain/Versand/`,
`Domain/Bestellung/`. Der End-to-End-Beweis ist der Integrationstest
`Infrastructure.Integration.Tests/BestellSagaE2ETests.cs` (gegen echtes Marten/Consul/Redis, inkl.
Kompensation bei ungedecktem Konto). Die Aggregat-Logik selbst ist store-frei im Prüfstand gedeckt
(z.B. `Infrastructure.Pruefstand.Tests/Phase5/KontoAggregatTests.cs`).

---

## 1. Das Bild: ein DIAMANT

Aus **einem** Auslöser laufen zwei parallele Zweige (Lager reservieren ∥ Konto belasten), die sich am
Versand wieder vereinen:

```
                        GibBestellungAuf            ← EIN Fach-Command (der „Nutzer")
                              │
                              ▼
                     BestellungAufgegeben           ← Event des Auslöser-Aggregats (startet den Prozess)
                    ┌─────────┴─────────┐
                    ▼                   ▼
          ReserviereBestand       BelasteKonto      ← zwei PARALLELE Zweige (Lager · Zahlung)
                    ▼                   ▼
          BestandReserviert       KontoBelastet
                    └─────────┬─────────┘
                              ▼
                          Versende                  ← JOIN: erst wenn BEIDE Zweige fertig sind
                              ▼
                          Versendet
```

Der Entwickler schreibt **vier Aggregate** (drei Ziele + ein Auslöser) und **einen Prozess**. Sonst nichts.

---

## 2. Die drei Ziel-Aggregate (reine Domäne)

Jedes Ziel ist ein normales Aggregat: **State + Commands + Events + Decider + Applier**, je in einem eigenen
Namespace (pro Namespace genau EIN `IState`). Id/Version/Handler/Actor/Signale/DTOs generiert das Framework.
Die Aggregate wissen **nichts** von Prozessen — ein Command ist ein Command.

### `Domain/Lager/Lager.cs`
```csharp
using Abstractions;
namespace Domain.Lager;

public partial class Lager : IState
{
    public int Bestand { get; set; }      // verfügbar
    public int Reserviert { get; set; }
}

public record RichteLagerEin(Guid AggregateId, int Bestand) : ICommand;
public record ReserviereBestand(Guid AggregateId, int Menge) : ICommand;
public record GebeBestandFrei(Guid AggregateId, int Menge) : ICommand;

public record LagerEingerichtet(int Bestand) : IEvent;
public record BestandReserviert(int Menge) : IEvent;
public record BestandFreigegeben(int Menge) : IEvent;
public record BestandReichtNicht(Guid AggregateId, int Verfuegbar, int Angefordert) : ITransientEvent;
public record LagerExistiertBereits(Guid AggregateId) : ITransientEvent;

public partial class Lager
{
    public partial class Decider : IDecider<Lager>
    {
        public IEnumerable<OneOf<LagerEingerichtet, LagerExistiertBereits>> Decide(RichteLagerEin cmd)
        {
            if (this.State.Version > 0) { yield return new LagerExistiertBereits(cmd.AggregateId); yield break; }
            yield return new LagerEingerichtet(cmd.Bestand);
        }
        public IEnumerable<OneOf<BestandReserviert, BestandReichtNicht>> Decide(ReserviereBestand cmd)
        {
            if (this.State.Bestand < cmd.Menge) { yield return new BestandReichtNicht(cmd.AggregateId, this.State.Bestand, cmd.Menge); yield break; }
            yield return new BestandReserviert(cmd.Menge);
        }
        public IEnumerable<OneOf<BestandFreigegeben>> Decide(GebeBestandFrei cmd)
            => new[] { new BestandFreigegeben(cmd.Menge) };
    }
    public partial class Applier : IApplier<Lager>
    {
        public void Apply(LagerEingerichtet evt)   => this.State.Bestand = evt.Bestand;
        public void Apply(BestandReserviert evt)   { this.State.Bestand -= evt.Menge; this.State.Reserviert += evt.Menge; }
        public void Apply(BestandFreigegeben evt)  { this.State.Reserviert -= evt.Menge; this.State.Bestand += evt.Menge; }
    }
}
```

### `Domain/Zahlung/Zahlungskonto.cs`
```csharp
using Abstractions;
namespace Domain.Zahlung;

public partial class Zahlungskonto : IState { public decimal Saldo { get; set; } }

public record RichteZahlungskontoEin(Guid AggregateId, decimal Guthaben) : ICommand;
public record BelasteKonto(Guid AggregateId, decimal Betrag) : ICommand;
public record ErstatteKonto(Guid AggregateId, decimal Betrag) : ICommand;

public record ZahlungskontoEingerichtet(decimal Guthaben) : IEvent;
public record KontoBelastet(decimal Betrag) : IEvent;
public record KontoErstattet(decimal Betrag) : IEvent;
public record KontoUngedeckt(Guid AggregateId, decimal Verfuegbar, decimal Angefordert) : ITransientEvent;

public partial class Zahlungskonto
{
    public partial class Decider : IDecider<Zahlungskonto>
    {
        public IEnumerable<OneOf<ZahlungskontoEingerichtet>> Decide(RichteZahlungskontoEin cmd)
        { if (this.State.Version > 0) yield break; yield return new ZahlungskontoEingerichtet(cmd.Guthaben); }
        public IEnumerable<OneOf<KontoBelastet, KontoUngedeckt>> Decide(BelasteKonto cmd)
        {
            if (this.State.Saldo < cmd.Betrag) { yield return new KontoUngedeckt(cmd.AggregateId, this.State.Saldo, cmd.Betrag); yield break; }
            yield return new KontoBelastet(cmd.Betrag);
        }
        public IEnumerable<OneOf<KontoErstattet>> Decide(ErstatteKonto cmd) => new[] { new KontoErstattet(cmd.Betrag) };
    }
    public partial class Applier : IApplier<Zahlungskonto>
    {
        public void Apply(ZahlungskontoEingerichtet evt) => this.State.Saldo = evt.Guthaben;
        public void Apply(KontoBelastet evt) => this.State.Saldo -= evt.Betrag;
        public void Apply(KontoErstattet evt) => this.State.Saldo += evt.Betrag;
    }
}
```

### `Domain/Versand/Versand.cs`
```csharp
using Abstractions;
namespace Domain.Versand;

public partial class Versand : IState { public bool Versandt { get; set; } }

public record Versende(Guid AggregateId, Guid Kunde) : ICommand;
public record Versendet(Guid Kunde) : IEvent;

public partial class Versand
{
    public partial class Decider : IDecider<Versand>
    {
        public IEnumerable<OneOf<Versendet>> Decide(Versende cmd)
        { if (this.State.Versandt) yield break; yield return new Versendet(cmd.Kunde); }   // fachlich: nur einmal
    }
    public partial class Applier : IApplier<Versand>
    { public void Apply(Versendet evt) => this.State.Versandt = true; }
}
```

---

## 3. Das Auslöser-Aggregat (hier startet alles)

Ein kleines Aggregat, dessen **Command** von außen kommt und dessen **Event** den Prozess auslöst. GENAU das
fehlte in der Kurzfassung — hier vollständig:

### `Domain/Bestellung/Bestellauftrag.cs`
```csharp
using Abstractions;
namespace Domain.Bestellung;

public partial class Bestellauftrag : IState { public bool Aufgegeben { get; set; } }

// Der Fach-Command, den der Aufrufer dispatcht. AggregateId = die Bestell-Id (der eigene Stream).
// Versand = eine EIGENE Id für das Versand-Aggregat (jeder Aggregat-Stream braucht eine eindeutige Guid).
public record GibBestellungAuf(Guid AggregateId, Guid Versand, Guid Kunde, Guid Artikel, int Menge, decimal Betrag) : ICommand;

// Der Auslöser-Event. Trägt die Felder, die die Prozess-Regeln brauchen (sie sehen nur den Event-Payload).
public record BestellungAufgegeben(Guid Bestellung, Guid Versand, Guid Kunde, Guid Artikel, int Menge, decimal Betrag) : IEvent;

public partial class Bestellauftrag
{
    public partial class Decider : IDecider<Bestellauftrag>
    {
        public IEnumerable<OneOf<BestellungAufgegeben>> Decide(GibBestellungAuf cmd)
        {
            if (this.State.Version > 0) yield break;   // schon aufgegeben → Noop (idempotent)
            yield return new BestellungAufgegeben(cmd.AggregateId, cmd.Versand, cmd.Kunde, cmd.Artikel, cmd.Menge, cmd.Betrag);
        }
    }
    public partial class Applier : IApplier<Bestellauftrag>
    { public void Apply(BestellungAufgegeben evt) => this.State.Aufgegeben = true; }
}
```

> **Das ist der Decider, der `GibBestellungAuf` entgegennimmt.** `AggregateType = "Bestellauftrag"` beim
> Dispatch ist genau der **Klassenname dieses Aggregats** — daran routet das Framework den Command an den
> generierten `Bestellauftrag`-Actor, der diesen Decider aufruft.

---

## 4. Der Prozess — nur die Regeln

### `Domain/Bestellung/BestellProzess.cs`
```csharp
using Abstractions;
using Domain.Lager;
using Domain.Zahlung;
using Domain.Versand;
namespace Domain.Bestellung;

public sealed class BestellProzess : IProzessDefinition
{
    public ProzessRegeln Regeln => Prozess<BestellungAufgegeben>.Definiere(p =>
    {
        // Zweig A — Bestand reservieren; Gegenzug gibt die Menge frei
        p.Auf<BestellungAufgegeben>()
            .Sende<ReserviereBestand>(e => new ReserviereBestand(e.Artikel, e.Menge))
            .RückgängigDurch<GebeBestandFrei>(e => new GebeBestandFrei(e.Artikel, e.Menge));

        // Zweig B — Konto belasten; Gegenzug erstattet
        p.Auf<BestellungAufgegeben>()
            .Sende<BelasteKonto>(e => new BelasteKonto(e.Kunde, e.Betrag))
            .RückgängigDurch<ErstatteKonto>(e => new ErstatteKonto(e.Kunde, e.Betrag));

        // Vereinigung (Diamant-Join) — versenden erst nach Reservieren UND Belasten
        p.Auf<BestellungAufgegeben>().Und<BestandReserviert>().Und<KontoBelastet>()
            .Sende<Versende>((e, b, k) => new Versende(e.Versand, e.Kunde));
    });
}
```

`Prozess<BestellungAufgegeben>` bindet den **Auslöser-Typ**: der Prozess startet, sobald ein
`BestellungAufgegeben` auftaucht. `p.Auf<X>()` triggert auf Event `X`; `.Und<Y>()` ist der Join;
`.Sende<Cmd>(...)` das Command; `.RückgängigDurch<Gegen>(...)` der Gegenzug. Mehr braucht die
Orchestrierung nicht.

> **Das Command-Typ-Argument ist Pflicht** (`.Sende<ReserviereBestand>`, nicht `.Sende(...)`). Daraus
> leitet der Generator die Command→Event-Kante für den Azyklizitäts-Boot-Guard ab; fehlt es, bricht der
> Build mit **CQRS003**. Ein `.Sende<TCmd>` ohne behandelnden Decider bricht mit **CQRS002**.

---

## 5. Verdrahtung — woher kommt der Prozess?

Du **registrierst den Prozess nirgends von Hand**. Ein Generator findet jede `IProzessDefinition` und trägt
sie in eine Registry `GeneratedProzessRegeln.Alle` ein. Der Host aktiviert die ganze Prozess-Schicht mit
**einer Zeile** (im `Program.cs`, nach `AddCqrsFramework`):

```csharp
builder.Services.AddGeneratedProzesse();
```

Das registriert das generische Manager-Kind + den Korrelations-Router. Der Router abonniert automatisch die
Signale aller teilnehmenden Events (hier: `BestellungAufgegeben`, `BestandReserviert`, `KontoBelastet`) —
weil `BestellProzess.Regeln` sie nennt. (Aggregate und Projektionen sind analog: die Generatoren erzeugen
Actor + Routing + DTOs aus den Klassen; für Projektionen ruft der Host `AddGeneratedPullPaths()`.)

---

## 6. Auslösen — EIN Command, und der End-to-End-Fluss

Der Aufrufer öffnet die beteiligten Aggregate und dispatcht **einen** Fach-Command:

```csharp
var artikel = Guid.NewGuid(); var kunde = Guid.NewGuid();
var versand = Guid.NewGuid(); var auftrag = Guid.NewGuid();

dispatcher.Dispatch(Env(artikel, "Lager",          new RichteLagerEin(artikel, 10)));
dispatcher.Dispatch(Env(kunde,   "Zahlungskonto",  new RichteZahlungskontoEin(kunde, 100)));

// DER Auslöser — der Rest läuft von selbst:
dispatcher.Dispatch(Env(auftrag, "Bestellauftrag",
    new GibBestellungAuf(auftrag, versand, kunde, artikel, Menge: 3, Betrag: 50)));

// Env ist nur ein kleiner Helfer (identisch zu BestellSagaE2ETests.Env):
static CommandEnvelope Env(Guid id, string typ, ICommand payload)
    => new() { AggregateId = id, AggregateType = typ, Modus = new CommandModus.Client(0), Payload = payload };
```

> **`Modus` ist Pflicht** (`required`). Ein Client-Command trägt `CommandModus.Client(expectedVersion)`
> (OCC gegen die behauptete Version); interne Emitter nutzen `CommandModus.Emittiert` — das setzt aber das
> Framework, nie der Anwender. Der alte `ExpectedVersion`/`AnyVersion=-1`-Weg existiert nicht mehr.

**`AggregateType`** ist jeweils der **Klassenname des Ziel-Aggregats** (`"Lager"`, `"Zahlungskonto"`,
`"Bestellauftrag"`). Daran routet das Framework den Command an den richtigen generierten Actor.

Der komplette Fluss, Schritt für Schritt:

1. `Dispatch(… "Bestellauftrag" … GibBestellungAuf)` → das Framework routet an den **`Bestellauftrag`-Actor**
   (Identität = `(Bestellauftrag, auftrag)`) → dessen `Decider.Decide(GibBestellungAuf)` → yields
   **`BestellungAufgegeben`**, wird persistiert.
2. Nach dem Append feuert das Framework das **Signal** `StateChangeViaBestellungAufgegeben`.
3. Der **Korrelations-Router** (abonniert dieses Signal, weil `BestellProzess` den Auslöser bindet) leitet
   daraus eine deterministische **Korrelation** ab und startet den **Prozess-Manager** dieser Instanz.
4. Der Manager faltet sein Marking und feuert die aktivierten Transitionen: `ReserviereBestand` an das
   **Lager** und `BelasteKonto` an das **Zahlungskonto** (parallele Zweige, fire-and-forget).
5. Deren Ergebnis-Events (`BestandReserviert`, `KontoBelastet`) tragen die Korrelation in ihren Metadaten →
   der Router weckt den Manager erneut.
6. Sind BEIDE da, ist der Join scharf → der Manager feuert **`Versende`** an das **Versand**-Aggregat →
   `Versendet`. Keine Transition mehr aktivierbar → der Prozess ist **abgeschlossen**.

Scheitert ein Zweig (z.B. `KontoUngedeckt`), macht der Manager den bereits erfolgreichen Zweig über dessen
`RückgängigDurch` rückgängig (`GebeBestandFrei`) und beendet als **fehlgeschlagen** — `Versende` läuft nie.

Beobachten (wie im Integrationstest): auf die Ziel-Zustände warten (`Lager.Bestand == 7`,
`Zahlungskonto.Saldo == 50`, `Versand.Versandt == true`).

---

## 7. Die Bausteine der Regel-Sprache (Referenz)

| Baustein | Bedeutung |
|---|---|
| `Prozess<TAuslöser>.Definiere(p => …)` | bindet den Auslöser-Event, sammelt die Regeln |
| `p.Auf<E>()` | Transition, die auf Event `E` triggert |
| `.Und<E2>()` / `.Und<E3>()` | **Join**: feuert erst, wenn AUCH `E2`/`E3` da ist (Diamant) |
| `.UndAlle<E>(t => t.Anzahl)` | **Count-Join** (dynamische Breite): feuert erst nach ALLEN N `E` |
| `.Sende<Cmd>(e => new Cmd(…))` | ein Command aus dem Match (Typ-Argument Pflicht → CQRS002/003) |
| `.SendeJe<Cmd>(e => e.Liste.Select(x => new Cmd(x)))` | **Fan-out**: N Commands aus einem Match |
| `.RückgängigDurch<Gegen>(e => new Gegen(…))` | Kompensation |
| zwei Regeln auf demselben Event | zwei parallele Zweige (Fan-out gratis) |

---

## 7.5 Weitere Muster (an echten Beispiel-Prozessen)

Der Diamant oben ist nur eines von mehreren Mustern. Alle laufen auf derselben Maschine — sie
unterscheiden sich nur in den Regeln. Die vollständigen, grünen Beispiele im Repo:

| Muster | Beispiel-Prozess | Kern |
|---|---|---|
| **Linear + Join + Datenfluss** | `Domain/Ueberweisung/UeberweisungsProzess.cs` | Kette reservieren → gutschreiben → buchen; jeder Schritt mit `.Und<…>()`-Join auf frühere Events + `RückgängigDurch` |
| **Fan-out / dynamische Breite** | `Domain/Sammelueberweisung/SammelueberweisungsProzess.cs` | `.SendeJe<SchreibeGut>(t => t.Ziele.Select(…))` (N Commands, je Ziel) + `.UndAlle<Gutgeschrieben>(t => t.Ziele.Count)` (Count-Join, bucht erst nach allen N). Breite steht im Auslöser, kein Zähler |
| **Zweiter Diamant** | `Domain/Reiseauftrag/ReiseProzess.cs` | Flug ∥ Hotel → Join an der Reise — belegt, dass das Diamant-Muster wiederholbar ist |
| **Prozess-Verkettung** | `Domain/Vorgang/GenehmigungsProzess.cs` → `AktivierungsProzess.cs` | Prozess A endet mit einem **persistierten** Domänen-Event (`VorgangGenehmigt`); Prozess B hat genau dieses Event als Auslöser → B startet „gratis" aus der Auslöser-Erkennung. **Wichtig:** ein internes `ProzessBeendet` kann NICHT verketten (sein Signal ist inert) — es braucht ein echtes Domänen-Event als Anker |

Details zum Modell (Marking-Fold, Korrelation, Azyklizität, Fan-out/Count-Join):
`docs/architektur/03-prozess-maschine.md`.

---

## 8. Was das Framework übernimmt (schreibst du NICHT)

- **Command-Routing:** `AggregateType` → generierter Actor → dein Decider.
- **Prozess-Start & Korrelation:** eine Instanz pro Auslöser, deterministisch; doppelter Auslöser → gleiche
  Instanz → verpufft. Korrelation reist unsichtbar in den Event-Metadaten.
- **Marking & Fortschritt:** der Manager faltet bei jeder Weckung aus den Ziel-Streams; kein Zustandsfeld.
- **Exactly-once:** die Framework-Inbox dedupliziert nach deterministischer CommandId (co-committete interne
  Marke) — ein Schritt wirkt genau einmal, ohne eine Zeile im Fachcode.
- **Kompensation, Crash-Heilung, fire-and-forget-Zustellung.**

---

## 9. Fallstricke (kurz)

- **Jedes Aggregat braucht eine EINDEUTIGE Guid** (der Event-Store keyt Streams per Guid). Darum hat die
  Sendung eine eigene `Versand`-Id ≠ Bestell-Id.
- **Ein `IState`-Aggregat pro Namespace** (die Command→Aggregat-Zuordnung leitet den Namen aus dem Namespace ab).
- **Neue nicht-interne Command-/Event-Typen brauchen Proto-DTOs:** einmal
  `dotnet run --project Proto.SourceGeneration` → `dotnet build ProtoRepo` → `dotnet build Infrastructure`.
- **`AddGeneratedProzesse()`** im Host nach `AddCqrsFramework` — der EINE Aufruf für die ganze Prozess-Schicht.
- **Idempotenz gehört NICHT in die Domäne** — keine Dedup-Zeile, kein `Vorgang`. Das Framework injiziert die
  deterministische CommandId und dedupliziert in der Inbox (Invariante 5).
