# Prüfauftrag: die Prozess-Schicht unabhängig nachvollziehen

Dieser Auftrag ist für einen **frischen Agenten ohne Vorwissen** über diese Session. Ziel: die neue
Prozess-Schicht (Event-Regel-DAG-Manager) und die Domänen-Reinheit **verifizieren** — und, als schärfste
Probe, das Muster aus der Entwickler-Anleitung an einem **komplett neuen Prozess** von Grund auf
reproduzieren. Läuft der neue Prozess in-memory und live grün, ist bewiesen: die Anleitung ist vollständig
und die Maschinerie generalisiert.

---

## 0. Pflichtlektüre (in dieser Reihenfolge)

1. **`docs/anleitung-prozess-schreiben.md`** — die Entwickler-Anleitung mit dem vollständigen Bestell-Saga-
   Beispiel (drei Ziel-Aggregate + Auslöser + Prozess + End-to-End-Fluss). Das ist deine Vorlage.
2. **`docs/prozess-neubau-event-regeln-dag.md`** — die Spezifikation des Modells; besonders §13 (Umsetzung +
   Forward-Check).
3. **`CLAUDE.md`** — der Phase-5-Block (Neubau + „Domänen-Reinheit hergestellt" / Framework-Inbox).
4. Referenz-Code, den du als Muster kopierst:
   - Aggregate: `Domain/Lager/Lager.cs`, `Domain/Zahlung/Zahlungskonto.cs`, `Domain/Versand/Versand.cs`
   - Auslöser: `Domain/Bestellung/Bestellauftrag.cs`
   - Prozess: `Domain/Bestellung/BestellProzess.cs`
   - Prüfstand-Test (Harness-Muster!): `Infrastructure.Pruefstand.Tests/Phase5/BestellSagaTests.cs`
   - Integrationstest-Muster: `Infrastructure.Integration.Tests/BestellSagaE2ETests.cs`

Kernmodell in einem Satz: **ein Prozess ist ein Petri-Netz** — Events sind Tokens, Commands sind
Transitionen; typisierte Regeln `Prozess<TAuslöser>.Definiere(p => p.Auf<E>().Und<E2>().Sende(...).RückgängigDurch(...))`;
ein generischer Manager pro Korrelation faltet sein Marking aus dem Log und feuert die aktivierten
Transitionen fire-and-forget. Die Aggregate sind REIN (keine Prozess-Mechanik); Idempotenz sichert eine
Framework-Inbox über die deterministische CommandId.

---

## 1. Ausgangsstand + Baseline verifizieren (Start-Checkpoint)

Der Arbeitsbaum ist grün. **Docker muss laufen** (Postgres/Consul/Redis) — die Integrationstests brauchen es.
`Domain.Client` baut vorbestehend NICHT (`_publish`) — **unabhängig, ignorieren**. Alle anderen
Solution-Fehler müssen 0 sein.

Verifiziere zuerst die Baseline (die Zahlen sind der erwartete IST-Zustand):

```
dotnet test Infrastructure.Pruefstand.Tests     # 53/53 (in-memory, kein Docker)
dotnet test Infrastructure.Integration.Tests    # 11/11 (SEQUENZIELL, Docker; xunit.runner.json schaltet Parallelität ab)
dotnet build Host.Grpc                           # 0 Fehler
```

Optional Boot-Smoke (kein Muss): `Host.Grpc` startet und loggt „=== Prozess-Manager (generisch): 3 Prozess(e) …",
lauscht auf :5001, 0 Exceptions.

Wenn diese Baseline nicht grün ist: **stopp** und melde das — nichts Weiteres bauen.

---

## 2. Reinheit + Struktur prüfen (2 Minuten)

Diese Checks belegen, dass die Domäne technisch unabhängig ist (Invariante 5):

- `git grep -n "Vorgang" Domain/` → **kein Treffer in Autoren-Dateien** (nur ggf. in generiertem Code unter
  `obj/`, das ignorieren).
- `git grep -n "IProzessSchrittCommand\|IVorgangEvent\|VerarbeiteteVorgaenge" Domain/ Abstractions/` → **leer**
  (diese Marker/Felder wurden gelöscht; Idempotenz ist Framework-Inbox).
- Öffne `Domain/Lager/Lager.cs`: der State hat NUR fachliche Felder (`Bestand`, `Reserviert`), der Decider
  hat KEINE Dedup-Zeile. Ein Command ist ein reines `record … : ICommand`.
- Öffne `Infrastructure/Aggregate/KommandoVerarbeitet.cs` + `Infrastructure/Aggregate/ActorSystem/AggregateActorBase.cs`
  (Suche `_verarbeiteteCommandIds` / `istIdempotent`): dort sitzt die Inbox — auf dem `AnyVersion`-Pfad wird die
  Marke `KommandoVerarbeitet(CommandId)` mit den Domänen-Events co-committet; `LoadStateAsync` überspringt sie
  beim Domänen-Falten (`payload is not IProzessIntern`), zählt sie aber in der Version.

---

## 3. DIE Aufgabe — einen NEUEN Prozess bauen (Reisebuchung), nur nach der Anleitung

Baue eine **Reisebuchungs-Saga** als DIAMANT — dieselbe Form wie die Bestell-Saga, aber eine neue Domäne.
Folge ausschließlich `docs/anleitung-prozess-schreiben.md`; kopiere die Muster-Dateien und passe sie an.

### Fachliches Bild
Aus einem Auslöser (`ReiseGebucht`) laufen zwei parallele Zweige — **Flugplatz reservieren** ∥ **Hotelzimmer
reservieren** — die sich vereinen: erst wenn BEIDE reserviert sind, wird die **Reise bestätigt**. Scheitert
ein Zweig (ausgebucht), wird der erfolgreiche Zweig freigegeben und die Reise NICHT bestätigt.

```
        BucheReise ──▶ ReiseGebucht
                        ├──▶ ReserviereFlug  ──▶ FlugReserviert  ─┐
                        └──▶ ReserviereHotel ──▶ HotelReserviert ─┤ (Join)
                                                                  ▼
                                                          BestaetigeReise ──▶ ReiseBestaetigt
```

### Was zu bauen ist (drei Ziel-Aggregate + Auslöser + Prozess)

Je in EIGENEM Namespace (pro Namespace genau EIN `IState`-Aggregat). Reine Domäne — kein `Vorgang`, keine
Dedup-Zeile.

1. **`Domain/Flug/Flugkontingent.cs`** — freie Plätze eines Flugs.
   - State: `int Plaetze;`
   - Commands: `RichteFlugEin(Guid AggregateId, int Plaetze)`, `ReserviereFlug(Guid AggregateId, int Anzahl)`,
     `GebeFlugFrei(Guid AggregateId, int Anzahl)` — reine `ICommand`.
   - Events: `FlugEingerichtet(int Plaetze)`, `FlugReserviert(int Anzahl)`, `FlugFreigegeben(int Anzahl)`;
     Ablehnung `FlugAusgebucht(Guid AggregateId, int Frei, int Angefordert) : ITransientEvent`.
   - Decider/Applier wie `Lager` (reservieren zieht ab, freigeben addiert; zu wenig → Ablehnung).

2. **`Domain/Hotel/Hotelkontingent.cs`** — analog zu Flug, mit `int Zimmer;` und
   `RichteHotelEin`/`ReserviereHotel`/`GebeHotelFrei`, Events `HotelEingerichtet/HotelReserviert/HotelFreigegeben`,
   Ablehnung `HotelAusgebucht`.

3. **`Domain/Reise/Reise.cs`** — das Bestätigungs-Ziel (Vereinigungspunkt).
   - State: `bool Bestaetigt;`
   - Command: `BestaetigeReise(Guid AggregateId, Guid Kunde) : ICommand`.
   - Event: `ReiseBestaetigt(Guid Kunde)`.
   - Decider: „nur einmal bestätigen" (`if (State.Bestaetigt) yield break;`) — analog `Versand`.

4. **`Domain/Reiseauftrag/Reiseauftrag.cs`** — der Auslöser (eigenes Aggregat).
   - State: `bool Gebucht;`
   - Command: `BucheReise(Guid AggregateId, Guid Reise, Guid Flug, Guid Hotel, Guid Kunde, int Plaetze, int Zimmer) : ICommand`.
     (`Reise` = eine EIGENE Guid für das Reise-Aggregat, ≠ AggregateId — jeder Stream braucht eine eindeutige Id.)
   - Event: `ReiseGebucht(Guid ReiseId, Guid Reise, Guid Flug, Guid Hotel, Guid Kunde, int Plaetze, int Zimmer)`.
     (ReiseId = AggregateId des Auftrags; `Reise` = die Bestätigungs-Aggregat-Id.)
   - Decider: `if (State.Version > 0) yield break; else yield ReiseGebucht(...)` — analog `Bestellauftrag`.

5. **`Domain/Reiseauftrag/ReiseProzess.cs`** — der Prozess (nur Regeln):
   ```csharp
   public sealed class ReiseProzess : IProzessDefinition
   {
       public ProzessRegeln Regeln => Prozess<ReiseGebucht>.Definiere(p =>
       {
           p.Auf<ReiseGebucht>()
               .Sende(e => new ReserviereFlug(e.Flug, e.Plaetze))
               .RückgängigDurch(e => new GebeFlugFrei(e.Flug, e.Plaetze));
           p.Auf<ReiseGebucht>()
               .Sende(e => new ReserviereHotel(e.Hotel, e.Zimmer))
               .RückgängigDurch(e => new GebeHotelFrei(e.Hotel, e.Zimmer));
           p.Auf<ReiseGebucht>().Und<FlugReserviert>().Und<HotelReserviert>()
               .Sende((e, f, h) => new BestaetigeReise(e.Reise, e.Kunde));
       });
   }
   ```

### Verdrahtung + Proto (Pflicht-Schritte aus der Anleitung §5/§9)
- Der Prozess wird **automatisch** gefunden (`IProzessDefinition` → `GeneratedProzessRegeln`), keine
  Handregistrierung. `AddGeneratedProzesse()` ruft der Host bereits auf.
- Neue nicht-interne Command-/Event-Typen brauchen Proto-DTOs:
  `dotnet run --project Proto.SourceGeneration` → `dotnet build ProtoRepo` → `dotnet build Infrastructure`.
  (Bei stale Generator-Zweifeln: `rm -rf <Projekt>/obj <Projekt>/bin`, `--no-incremental`.)

### Tests (zuerst in-memory, dann EINMAL live)
- **Prüfstand-Glue-Test** (`Infrastructure.Pruefstand.Tests/Phase5/ReiseSagaTests.cs`) — kopiere das Harness
  aus `BestellSagaTests.cs` (echter `KorrelationsRouter` + `ProzessManager`, Fake-Dispatch der
  `causationId = vorgang` stempelt und die Inbox per Vorgang mimt, Ziel-Events zurück durch den Router routet).
  Zwei Fälle:
  1. **Happy Path:** genug Plätze + Zimmer → beide reserviert → Reise **bestätigt** (`ProzessBeendet(true)`).
  2. **Kompensation:** Hotel ausgebucht (`Zimmer` zu klein) → Flug wird freigegeben, Reise NICHT bestätigt
     (`ProzessBeendet(false)`); der Flug-Bestand ist wieder auf Anfang.
- **Integrationstest** (`Infrastructure.Integration.Tests/ReiseSagaE2ETests.cs`) — kopiere `BestellSagaE2ETests.cs`:
  boote mit `AddGeneratedProzesse()`, dispatche `RichteFlugEin`/`RichteHotelEin` + EIN `BucheReise`, warte auf
  `ProzessBeendet(true)` im Manager-Log (Korrelation = `ProzessId.Für("ReiseProzess", auftragId, 1)`), prüfe:
  Flug/Hotel reserviert, `Reise.Bestaetigt == true`. Nur EINMAL laufen lassen, sequenziell.

**Disziplin (Memories `hang-diagnose-in-memory-nicht-integrationstest`, `prozess-terminal-selbstweckung`):**
verteilte Effekte/Hangs IN-MEMORY (Glue-Test) beweisen, nie im langsamen Integrationstest raten. Wenn der
Integrationstest hängt/timeout't, obwohl in-memory grün: prüfe die **Terminal-Selbstweckung** (das
Ergebnis-Event der LETZTEN Transition — `ReiseBestaetigt` — triggert KEINE Regel; nur die Selbst-Weckung nach
erfolgreichem Send erkennt „terminal" — das ist im Framework schon eingebaut, aber ein guter erster Verdacht,
falls „alle Effekte da, aber kein ProzessBeendet").

---

## 4. Abnahme-Checkliste

- [ ] Baseline war grün (Prüfstand 53, Integration 11, Host.Grpc 0 Fehler).
- [ ] Reinheits-Checks (§2) bestätigt.
- [ ] Neue Domäne gebaut: Flugkontingent, Hotelkontingent, Reise, Reiseauftrag, ReiseProzess — reine
      Aggregate, keine Prozess-Mechanik.
- [ ] Proto regeneriert, Infrastructure baut.
- [ ] `ReiseSagaTests` (Prüfstand): Happy Path + Kompensation **grün**.
- [ ] `ReiseSagaE2ETests` (Integration, sequenziell, einmal): **grün**.
- [ ] Volle Suiten weiter grün: Prüfstand **55** (53 + 2 neu), Integration **12** (11 + 1 neu).
- [ ] `Host.Grpc` bootet mit **4 Prozessen**, 0 Exceptions.
- [ ] `git grep "Vorgang" Domain/` weiterhin leer in Autoren-Dateien (auch in der neuen Domäne).
- [ ] Kurzbericht: Was war unklar/unvollständig an der Anleitung (falls etwas)? — genau das ist der Zweck
      dieses Prüfauftrags: die Anleitung an einem fremden Prozess auf Vollständigkeit testen.

---

## 5. Leitplanken (nicht verletzen)

- **Domäne bleibt rein** — kein `Vorgang`, keine Dedup-Zeile, kein `IProzess…`-Marker im Fachcode. Idempotenz
  ist Framework-Inbox (deterministische CommandId).
- **Jedes Aggregat eine eindeutige Guid** (Event-Store keyt per Guid) — darum hat die Reise eine eigene Id ≠ Auftrag.
- **Ein `IState` pro Namespace.**
- **In-memory zuerst**, dann Integration EINMAL, IMMER sequenziell.
- **Proto-Regen** bei neuen nicht-internen Typen; Manager-interne Typen sind `IProzessIntern` (proto-frei).
- **Nichts von Hand registrieren** — Prozesse/Aggregate/Projektionen finden die Generatoren; der Host ruft
  `AddGeneratedProzesse()`/`AddGeneratedPullPaths()`.

Am Ende: die neuen Prozess-Tests grün, die Abnahme-Checkliste erfüllt, und ein kurzer Bericht, ob die
Anleitung ausgereicht hat.
