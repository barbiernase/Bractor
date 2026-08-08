# Prozess-Neubau: typisierte Event-Regeln über einem DAG-Manager

Entwurf (kein Code). Ersetzt die Prozess-Schicht (Plan-Schrittliste + Treiber + generiertes
Prozess-Aggregat) durch **typisierte Event→Command-Regeln**, ausgeführt von **einem** Prozess-Manager-
Actor pro Korrelation, dessen Zustand ein **Petri-Marking** ist, das aus dem Log gefaltet wird.

Ziel: die menschlich-geformte Über-Spezifikation entfernen (Status-Enum, Quittungs-Zeremonie, ordinale/
Handle-Referenzen, die `ITransientEvent`-Konvention) und dabei **maximale Parallelität offenhalten** —
sequenziell zuerst, DAG-fähig entworfen (dasselbe Muster wie `AbhaengigVon`).

---

## 1. Warum überhaupt neu ansetzen

Das jetzige Modell (fertig, 63/63 + 10/10) ist ein **orchestrierter Saga-Treiber**: ein zentraler Treiber
hält eine Schrittliste, sendet Schritt für Schritt, wartet auf Quittung, schreibt eine Quittung ins Prozess-
Aggregat. Das trägt, aber es hat vier Stellen, die ein *menschliches Koordinator-Bild* tragen statt eines
mechanischen Event-Modells:

1. **Status-Enum** (`Neu/Laeuft/Rueckabwicklung/…`) — ein Cache einer Faltung, kann driften.
2. **Quittungs-Zeremonie** (`MeldeSchrittErledigt`) — der Treiber schreibt Fortschritt von Hand ins
   Prozess-Log, statt ihn aus den Ziel-Events abzuleiten.
3. **Ordinale/Handle-Referenzen** (`Von(1)`, `abhängigVon: 1`) — Assoziation über Position, brüchig.
4. **`ITransientEvent` + kein persistentes Event = Nein** — die Ablehnung wird aus Markern rekonstruiert.

Das Event-Regel-Modell entfernt alle vier: die Abhängigkeit **ist** der Event-Typ, der Fortschritt **ist**
das Ziel-Event, die Ablehnung **wird** ein beobachtbares Event.

---

## 2. Das Modell in einem Satz

> Ein Prozess ist ein **Petri-Netz**: Events sind Tokens, Commands sind Transitionen. Eine Transition ist
> aktiviert, wenn alle Events ihrer Bedingung (für diese Korrelation) eingetroffen sind. „Prozess treiben"
> heißt: aktivierte Transitionen feuern.

- **Netz-Struktur** (die Regeln): statisch, pro Prozess-Typ gleich, ist **Code** (pure Funktion). Wird **nie**
  pro Instanz gespeichert.
- **Marking** (welche Events sind da, was wurde gefeuert, in welcher Kausalordnung): dynamisch, pro Instanz,
  liegt im **Log**. Wird bei jeder Weckung neu gefaltet.

Das ist die eine Regel, die alles trägt: **Struktur aus Code, Marking aus dem Log — nie im Actor-Feld.**

---

## 3. Die typisierte API (was der Anwender schreibt)

```csharp
public ProzessRegeln Regeln => Prozess<UeberweisungBeauftragt>.Definiere(p =>
{
    p.Auf<UeberweisungBeauftragt>().Sende(e => new ReserviereBetrag(e.Quelle, e.Betrag))
        .RückgängigDurch(e => new GebeReservierungFrei(e.Quelle));

    p.Auf<BetragReserviert>().Sende(e => new SchreibeGut(e.Ziel, e.Betrag))
        .RückgängigDurch(e => new StorniereGutschrift(e.Ziel, e.Betrag));

    // Join: erst wenn BEIDE Events da sind (hält Diamanten/maximale Parallelität offen)
    p.Auf<BetragReserviert>().Und<Gutgeschrieben>()
        .Sende((r, g) => new BucheReservierung(r.Quelle, reservierung: r.Vorgang));
});
```

Regeln der Notation:
- **Vorwärts-Abhängigkeit = der Event-Typ** in `Auf<…>` — keine Nummer, kein String, kein Handle.
- **Join = Konjunktion** `Auf<E1>().Und<E2>()` — Pflicht, sonst nur Ketten/Bäume, keine Diamanten (kappt
  spätere Parallelität).
- **Kompensation = `RückgängigDurch(…)`** am Schritt — läuft im Rollback, wenn das Erfolgs-Event dieses
  Schritts beobachtet wurde (es lief also).
- **Fan-out ist gratis**: zwei Regeln auf demselben Event → zwei parallele Zweige.
- **Korrelation ist typisiert und Pflicht** (siehe §5.1). `Prozess<TAuslöser>` bindet den Auslöser-Typ.

Statischer Check beim Build: der Regel-Graph (Command→produziert→Event→triggert→Command) muss **azyklisch**
sein. Das ersetzt die „rückwärts-only"-Compile-Regel der Schrittliste durch eine statische Analyse (der eine
Preis der höheren Ausdruckskraft).

---

## 4. Der Prozess-Manager-Actor

**Ein** Cluster-Kind, **eine** Instanz pro Korrelations-Id. Verschmilzt die heutigen zwei Actoren
(Prozess-Aggregat + Treiber) — die Trennung war ein Artefakt des Request-Reply-Orchestrierens.

Sein Stream = die Prozess-Geschichte (beobachtete Events, gefeuerte Commands, Ausgang). Im Speicher hält er
**nichts** außer der Korrelations-Id.

Lebenszyklus (virtueller Actor, wie heute):
- **Geburt**: erste Weckung an `(prozess-manager, korrelationId)`.
- **Weckung (der Turn)**:
  ```
  1. Log falten            → Marking (welche Events sah ich, was feuerte ich, Kausalität)
  2. Regeln (Code) + Marking → Ready-Menge (aktivierte, noch nicht gefeuerte Transitionen)
  3. feuere Ready          → Commands an Ziel-Aggregate (FIRE-AND-FORGET, kein await im Turn)
  4. schreibe „gefeuert C" ins eigene Log; wenn terminal, „abgeschlossen/fehlgeschlagen/klärung"
  5. return
  ```
- **Kein `await RequestAsync` im Turn** → die (A)-Hang-Klasse ist strukturell weg. Die Bestätigung kommt
  *später* als korreliertes Ziel-Event, das den Manager erneut weckt.
- **Passivierung**: idle → verschwindet. Nächste Weckung faltet alles neu. „Darf jederzeit sterben."

Der **DAG ist der Decider**: die Übergangslogik ist nicht handgeschrieben, sie ergibt sich aus den Regeln +
dem Marking. Der Generator (heute `ProzessAggregatGenerator`) emittiert künftig aus den `Regeln` einen
DAG-Deskriptor + die Enabling-/Kompensations-Auswertung, statt einer Status-Enum-Maschine.

---

## 5. Die drei tragenden neuen Stücke (hier steht/fällt es)

### 5.1 Korrelation als Erstbürger + Routing nach Korrelation
Heute weckt der Receiver `(kind, streamId)`. Neu: `(prozess-manager, korrelationId)`.
- **Jedes teilnehmende Event trägt die Korrelations-Id als Feld** (`Vorgang`/`prozId` reicht nicht als
  gehashter Wert — aus dem Hash kommt man nicht zurück). Das ist der MassTransit-`CorrelationId`-Ansatz.
- Ein **Korrelations-Router** weckt aus einem Event den richtigen Manager. Das ist die **einzige echte neue
  Infrastruktur** — bewusst bauen, hier scheitern solche Umbauten sonst. (Der bestehende SignalReceiver/Poller
  ist die Vorlage; er weckt nur nach anderem Schlüssel.)

### 5.2 Fehler werden beobachtbare Events
Heute ist eine Ablehnung ein `ITransientEvent` — nicht im Log, nur als `CommandResult`. Ein event-getriebener
Manager kann darauf nicht reagieren. Also: ein Fehlschlag wird ein **durables** Fakt (z.B. der Manager
schreibt `SchrittGescheitert(…)` in sein Log, wenn er die negative Quittung sieht — oder das Ziel emittiert
ein durables Ablehnungs-Event). Erst dann ist Kompensation event-getrieben auslösbar. Das ist die
`ITransientEvent`-Frage aus der Diskussion, im Choreographie-Modell aufgelöst.

### 5.3 Marking aus dem Log, nie im Actor
Der Manager faltet bei jeder Weckung. Kein DAG-Zustand in Feldern → kein Drift, keine Verlust-bei-Passivierung.
Das ist die Invariante-1-Treue (die Wahrheit ist der Log) auf die Prozess-Schicht angewandt.

---

## 6. Determinismus & Idempotenz (bleibt, nur verallgemeinert)

- **Vorgang-Id aus Kausalität**: das gefeuerte Command bekommt eine deterministische Id aus
  `(korrelationId, triggerEvent.StreamId, triggerEvent.Version, commandType)`. Deterministisch **und**
  eindeutig pro Auslöser → löst den Fan-out (N `SchreibeGut` aus N `BetragReserviert`) ohne Zähler.
- **Idempotentes Feuern**: Re-Weckung rechnet die Ready-Menge neu und feuert ggf. erneut; das **Ziel
  dedupliziert** über die Vorgang-Id (unverändert wie heute). „Schon gefeuert" ist außerdem im Manager-Log.
- **Poll-Backstop** bleibt (heilt verlorene letzte Weckung), weckt jetzt nach Korrelation.

---

## 7. Kompensation

- Jede Regel trägt ihr `RückgängigDurch`. Ein Fehlschlag setzt eine „Abbruch"-Marke; der Manager feuert die
  Rücknahme jedes Schritts, dessen Erfolgs-Event im Marking steht.
- **Reihenfolge = transponierte Kausalität** (rückwärts durch den DAG ab dem Fehler). Solange **sequenziell**
  gefahren wird, ist das eine lineare Gegenreihenfolge (wie heute). Erst bei Parallelität wird es eine
  partielle Ordnung — dann liest der Manager sie aus der aufgezeichneten Kausalität (causation) des Logs.
- „Gegen-Command ist wirklich das Inverse" bleibt eine **Geschäfts-Aussage**, nicht mechanisch prüfbar —
  `RückgängigDurch` stellt es sichtbar neben den Vorwärts-Schritt, mehr kann kein Framework.

---

## 8. Staffelung: sequenziell zuerst, DAG-fähig entworfen

Exakt das Muster, das bei `AbhaengigVon` getragen hat:
- **Jetzt**: der Manager rechnet die Ready-Menge, feuert aber **eine** aktivierte Transition pro Turn (nimm
  die erste). Volle Korrektheit, kein paralleler Scheduler, Kompensation linear. Der DAG ist da, wird nur
  nicht ausgenutzt.
- **Später (additiv)**: „feuere **alle** aktivierten" + Kompensation entlang der transponierten Kausalität.
  Regeln/Manager-Vertrag/Log-Format bleiben gleich. Die Parallelität, die herausfällt, ist echte Parallelität
  **zwischen** Aggregaten (verschiedene Konten) — actor-modell-konform.

Kein Vertrag darf Linearität *voraussetzen*; er darf sie nur *noch nicht ausnutzen*.

---

## 9. Was ersetzt / was bleibt

**Ersetzt** (Prozess-Schicht):
- `IProzessPlan`/`ProzessSchritte`/`Schritt`/`SchrittRefs`/`IProzessSicht`/`IProzessSchrittCommand`
- `ProzessTreiber<T>`, `TreiberActor<T>`
- `ProzessAggregatGenerator` (Status-Enum-Maschine), `ProzessWiringGenerator`
- `GeneratedProzesse/Starts/Handlers`, die Start-Bindung-über-Plan im `HandlerOutputRouter`

**Bleibt** (unangetastet, wird wiederverwendet):
- Event-Store inkl. `ReadStreamAsync`/`ReadChangedStreamsAsync`, Broker, `StateChangeVia`-Signale
- Die Ziel-Aggregat-Maschinerie (Konto) + Dedup über `Vorgang`
- `ProzessId`-Hashing (umgewidmet: Korrelation + kausale Command-Ids)
- Die Pull-Adapter-Maschine (umgewidmet als Korrelations-Router/Manager-Wecker)

**Neu**:
- `ProzessRegeln` + Regel-Builder (`Prozess<T>.Definiere`, `Auf<>().Und<>().Sende().RückgängigDurch()`)
- `ProzessManagerActor` (ein Kind, Instanz pro Korrelation)
- Korrelations-Router
- Regel-Generator (Regeln → DAG-Deskriptor + Enabling/Kompensation), Azyklizitäts-Check

---

## 10. Umbauweg (KEINE Rückwärtskompatibilität — direkter Ersatz)

Es gibt keinen Alt-Pfad, kein duales API. Die alte Prozess-Schicht wird gelöscht, die Pilot-Pläne werden in
die Regel-Form umgeschrieben. Die *In-memory-zuerst*-Reihenfolge bleibt — aber als **Hang-Diagnose-Disziplin**
(den neuen Kern billig beweisen, bevor man das Alte rausreißt), NICHT als Kompatibilitäts-Zwang.

1. **In-memory-Kern zuerst** (Fake-Cluster, Prüfstand): Regeln → DAG → Marking-Faltung → Ready → feuern, mit
   Korrelation + Fehler-als-Event. Sequenziell. Beweist API + Kern. (Leitplanke: verteilte Effekte in-memory
   beweisen, nie im Integrationstest raten.)
2. **Pilot-Pläne umschreiben + Altes löschen**: `UeberweisungsPlan` und `SammelueberweisungsPlan` in die
   `Regeln`-Form heben; die alte Prozess-Schicht (Plan/Schrittliste, `ProzessTreiber`/`TreiberActor`,
   `ProzessAggregatGenerator`/`ProzessWiringGenerator`, `GeneratedProzesse/Starts/Handlers`, die
   Start-Bindung-über-Plan) samt ihrer Prüfstand-/Integrationstests **löschen**. Kein Nebeneinander.
3. **Ein Prozess live** (Integration, einmal, sequentiell) end-to-end gegen Postgres/Consul/Redis.
4. **Sammelüberweisung live** → beweist Fan-out/dynamische Breite.

Nicht betroffen (bleibt baubar/grün): die Ziel-Aggregate (Konto), die Aggregat-/Pipeline-Maschinerie und
Host.Grpc — nur die Prozess-Schicht wird ersetzt. Die alten Prozess-Tests werden durch **neue** ersetzt; die
Zählerstände ändern sich, das ist erwünscht — Abnahmeziel sind die neuen Tests (§12), nicht die alten Zahlen.

---

## 11. Offene Fragen / Risiken (bewusst benannt)

- **Korrelations-Index**: „alle Events dieser Korrelation" braucht entweder ein eigenes Manager-Log
  (Observations werden reinprojiziert) oder einen Read-Index. Entscheidung: eigenes Manager-Log (foldbar,
  replaybar, beobachtbar) — konsistent mit der Event-Sourcing-Maschine.
- **Join-Semantik** unter Wiederholung: `Und<>` muss monoton sein (Event-Ankunft akkumuliert), damit die
  Ready-Menge konfluent bleibt.
- **Azyklizitäts-Check**: Regel-Graph statisch prüfen (Command→Event→Command). Roslyn-Analyzer beim Build.
- **Fehler-als-Event**: sauberer Ort (Manager-Log vs. Ziel-Emit) noch zu entscheiden — Vorschlag: der Manager
  schreibt es, wenn er die negative Quittung sieht (hält die Ziel-Aggregate rein).
- **Terminal-Erkennung**: „fertig" = keine Transition mehr aktivierbar und kein offener Send. Aus dem Marking
  ableitbar; genau definieren.

---

## 12. Abnahme (wie wir es beweisen)

- Prüfstand (in-memory, Fake-Cluster): lineare Kette, Fan-out, Join, Kompensation (richtiger Umfang +
  Reihenfolge), Crash-Heilung (Re-Weckung feuert deterministisch neu, Ziel dedupliziert), Azyklizitäts-Check.
- Integration (einmal, sequentiell): ein Prozess end-to-end signal-getrieben bis Abgeschlossen/Fehlgeschlagen.
- Forward-Check: der DAG-Deskriptor ist abrufbar; dokumentiere, wie „feuere alle aktivierten" additiv andockt.
```

---

## 13. UMGESETZT (dieser Umbau) + Forward-Check

**Direkter Ersatz vollzogen — kein Alt-Pfad mehr.** Die Schrittlisten-Schicht (`IProzessPlan`/`ProzessSchritte`/
`Schritt`/`SchrittRefs`/`IProzessSicht`, `ProzessTreiber`/`TreiberActor`, `ProzessAggregatGenerator`/
`ProzessWiringGenerator`, `GeneratedProzesse/Starts/Handlers`, die Plan-Yield-Route im `HandlerOutputRouter`)
ist gelöscht. `IProzessSchrittCommand` (Vorgang-Injektion) und `ProzessId`-Hashing bleiben — der neue Kern nutzt sie.

**Der Kern (alles neu):**
- **Regeln = Daten** (`Abstractions/Prozess/`): `ProzessRegeln`/`Regel` + Builder `Prozess<TAuslöser>.Definiere` mit
  `Auf<>().Und<>().Und<>()` (Join, Arität 1–3), `.Sende<TCmd>` / `.SendeJe<TCmd>` (Fan-out), `.UndAlle<TSammel>(n)`
  (Count-Join, dynamische Breite), `.RückgängigDurch` / `.RückgängigDurchJe` (Kompensation, auch fan-out-weise).
  `IProzessDefinition` ist, was der Anwender schreibt.
- **EIN generischer Manager** (`Infrastructure/Prozess/ProzessManager.cs`), kein per-Prozess-Aggregat: sein Log
  speichert nur ENTSCHEIDUNGEN (`ProzessGestartet`/`SchrittGescheitert`/`ProzessBeendet`); das **Marking faltet er
  bei jeder Weckung aus den Ziel-Streams** (Ergebnis-Event ↔ Transition per Vorgang, `IVorgangEvent`) — Invariante 1,
  kein Feld-Zustand. Feuert FIRE-AND-FORGET (`DetachedProzessSend`) → die (A)-Hang-Klasse ist strukturell weg.
- **Korrelation als Erstbürger** ohne Domänen-Eingriff: sie reist als `CommandEnvelope.CorrelationId` → landet in den
  Ziel-Event-Metadaten; der `KorrelationsRouter` (umgewidmeter SignalReceiver + Poll) weckt `(prozess-manager,
  Korrelation)`. Konto blieb rein. Fehler wird durabel, wo der Manager die negative Quittung sieht (`SchrittGescheitert`).
- **Nur EIN Generator** (`Domain.SourceGeneration/ProzessRegelnGenerator`): der DAG-Deskriptor `GeneratedProzessRegeln.Alle`
  (Prozess-Name → Regeln). Die Infrastruktur (Manager-Kind, Router, Startup) ist generisch/handgeschrieben — sie liest
  nur die Registry. `AddGeneratedProzesse()` = der eine Host-Aufruf.

**Wichtige Nahtstelle (Live-Lektion):** das ERGEBNIS-Event der LETZTEN Transition ist Auslöser KEINER Regel → sein
Signal abonniert der Router nicht → nur eine **Selbst-Weckung nach erfolgreichem Send** (`DetachedProzessSend.danach`)
erkennt „terminal". Zwischenschritte weckt ohnehin ihr Ergebnis-Event; die Selbst-Weckung ist idempotent (Ziel dedupliziert).

**Beweise:** Prüfstand 54 (lineare Kette, Join, Datenfluss, Kompensation Umfang+Präzision, Crash-Heilung, Fan-out,
Count-Join, Azyklizitäts-Algorithmus, Hang-Freiheit, voller Glue-Ablauf über echten Router+Manager). Integration 9
(3 neu: Überweisung end-to-end, Kompensation, Sammelüberweisung Fan-out — signal-getrieben). Host.Grpc bootet mit
2 Prozessen, 0 Exceptions.

**Azyklizität:** der Zyklus-Kern (`ProzessGraph`) + der Regel-Check (`ProzessAzyklizität`) sind implementiert und im
Prüfstand bewiesen. Als Boot-Guard braucht er eine PRÄZISE Command→Event-Abbildung (pro Decider); die vorhandene
`GeneratedEventCommandMapping` ist aggregat-GROB (Command → alle Aggregat-Events) → erzeugt Falsch-Zyklen zwischen
zwei Schritten desselben Aggregats. Der Boot-Guard/Roslyn-Analyzer dockt additiv an, sobald eine präzise Map (eigener
Generator aus den Decider-Signaturen) vorliegt — das ist der eine offene Rest von §10.

**Forward-Check — wie „feuere ALLE aktivierten" additiv andockt:** Der DAG-Deskriptor ist abrufbar
(`GeneratedProzessRegeln.Alle[name].Regeln`). Der Manager rechnet bei jeder Weckung bereits die VOLLSTÄNDIGE
Ready-Menge (`kandidaten` in `FaltMarkingAsync`) und feuert diese Runde nur die ERSTE (`FirstOrDefault(!ErgebnisDa)`).
„Feuere alle aktivierten" ersetzt genau diese eine Auswahl durch eine Schleife über ALLE pending Kandidaten (jeder
fire-and-forget) — Marking-Faltung, Vorgang-Ableitung, Terminal-Erkennung und Log-Format bleiben WORTGLEICH; die
Kompensation verallgemeinert sich von reverse-Regel-Reihenfolge (linear) zur transponierten Kausalität. Kein Vertrag
(Regeln, Manager-Log, Engine-API) setzt Linearität voraus — der Kandidatensatz IST schon der ganze Ready-Set, der
sequenzielle Fahrer wählt nur einen. Die dann herausfallende Parallelität ist echte Parallelität ZWISCHEN Aggregaten.
