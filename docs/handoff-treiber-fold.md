# Handoff: Treiber-Fold ins Emit-Primitiv (EM-1 vollenden)

> **Für einen frischen Agenten.** Self-contained. Dies ist die *heikelste* Änderung des Backend-Neubaus:
> sie fasst gleichzeitig den **Exact-once-Inbox** (das sicherheitskritischste Stück) und den
> **Saga-Kompensations-Fold** an. Lies dieses Dokument ganz, bevor du Code anfasst.

## 0. Wo wir stehen (bereits erledigt & grün)

Fundament des Neubaus steht: **P0** (Verträge), **P2** (Sentinel→`CommandModus`, zwei Schreiber-Eingänge),
**P3** (Emit-Primitiv `CommandEmitter`, W1/W2 strukturell weg), **P5(a)** (präzises CorrelationId-Poll-Routing),
**P1a/b/c** (kanonischer Kanten-Graph), **Bugfixes 9 + 7/8**. Zähler: **Prüfstand 51/51, Integration 25/25**
(nur der bekannte `SnapshotLiveE2ETests`-Cold-Boot-Flake ist instabil — bimodal, prozess-unabhängig, NICHT
timeout-tunebar; `memory/snapshot-e2e-flake-clusterboot.md`).

Details/Kontext: `docs/backend-neubau-fahrplan.md` („Fortschritt"), `CLAUDE.md` (oben), `docs/backend-neubau-
einheitliche-maschine.md` (Philosophie), `memory/hang-diagnose-in-memory.md`.

## 1. Ziel & Wert (ehrlich)

**EM-1 vollenden:** es soll *genau einen* Emit-Weg geben (`CommandEmitter`). Heute gibt es EINE
Ausnahme: der **Prozess-Treiber** (`ProzessManagerActor.SendeAnZiel`) sendet noch selbst per
`cluster.RequestAsync<CommandResult>`, **weil er die Quittung für die Fehlschlag-Erkennung braucht**.

Das ist **Eleganz/Konsolidierung, KEINE Korrektheit** — der Treiber funktioniert heute. Wert: ein einziger
Emit-Weg + erst danach ist der **Analyzer (Auflage A6)** möglich (Build-Fehler bei jedem rohen
`RequestAsync<CommandResult>` außerhalb des Primitivs). Nicht überstürzen; jede Scheibe beweisen.

## 2. Der heutige Fehlschlag-/Fold-/Weck-Pfad (verstehen, bevor du änderst)

Alle Dateien in `Infrastructure/Prozess/` bzw. `Infrastructure/Aggregate/ActorSystem/`.

1. **Feuern:** `ProzessManager.FeuereAsync` → `_dispatch(korrelation, cmd, vorgang, ct)`. Der Dispatch ist
   `DetachedProzessSend.Wrap(SendeAnZiel, MeldeFehlschlagAnManager, WeckeSelbst)` (`ProzessManagerActor.ErzeugeDispatch`).
2. **`SendeAnZiel`** (`ProzessManagerActor`): baut einen `CommandEnvelope { CommandId = vorgang,
   Modus = Emittiert, CorrelationId = korrelation, … }` und macht ein **bounded** `RequestAsync<CommandResult>`.
   Gibt die Quittung (`CommandResult?`) zurück.
3. **`DetachedProzessSend.RunDetached`** beobachtet die Quittung OUT-OF-TURN:
   - `result is { Success: false }` → `beiFehlschlag` = `MeldeFehlschlagAnManager` → schickt `MeldeFehlschlag`
     an die eigene Mailbox → `ProzessManager.NotiereFehlschlagAsync` schreibt ein durables
     **`SchrittGescheitert(vorgang, grund)`** ins Manager-Log → `WakeAsync`.
   - sonst (Erfolg ODER `result == null`) → `danach` = `WeckeSelbst` (Selbst-Weckung).
4. **Fold** (`ProzessManager.WakeAsync` → `FaltMarkingAsync`): faltet aus den Ziel-Streams je Transition zwei
   Achsen (record `Kandidat`):
   - `ErgebnisDa` = irgendein Ziel-Event mit `CausationId == vorgang` (auch die interne Inbox-Marke) →
     „nicht neu feuern".
   - `WirkungDa` = ein **Domänen**-Event (kein `IProzessIntern`) mit `CausationId == vorgang` → kompensierbar,
     aktiviert Joins.
   Der Fehlschlag kommt NICHT aus dem Fold, sondern aus `mz.Gescheitert` (gefaltet aus `SchrittGescheitert`
   im Manager-Log, `LadeStatusAsync`). `WakeAsync`: `if (mz.Gescheitert.Count == 0)` → Vorwärts-Zweig
   (erste `!ErgebnisDa`-Transition feuern, sonst `ProzessBeendet(true)`), sonst → Kompensations-Zweig.
5. **Empfänger-Ablehnung heute** (`AggregateActorBase.HandleCommandCoreAsync`, der Ablehnungs-Pfad — such nach
   „Reine Ablehnung"): bei einem `ITransientEvent` schreibt der Actor **BEWUSST KEINE Marke** (langer Kommentar
   dort erklärt: eine Marke + Inbox-Dedup-Rückgabe `Success:true` würde den Manager fälschlich auf Erfolg
   verzweigen lassen). Er liefert nur `CommandResult { Success=false, RejectionEvent=… }` (Targeted Delivery).
   Der **Erfolgs**-Pfad dagegen co-committet `KommandoVerarbeitet(CommandId)` (`Infrastructure/Aggregate/
   KommandoVerarbeitet.cs`, `IProzessIntern`) in die Inbox (`_verarbeiteteCommandIds`, eine `BoundedInbox`).

## 3. Das Ziel-Design (fold-basiert, ohne Quittung)

Ersetze die *quittungs*-basierte Fehlschlag-Erkennung durch einen **durablen Ablehnungs-Marker auf dem
Ziel-Stream**, den der Manager **faltet**:

1. **Neuer Marker** `KommandoAbgelehnt(Guid CommandId, string Grund) : IEvent, IProzessIntern` (neben
   `KommandoVerarbeitet`). Der Empfänger co-committet ihn auf dem **emittierten** Ablehnungs-Pfad
   (`Modus.Emittiert` + `ITransientEvent`-Ergebnis) — genau EINE native Transaktion, wie die Noop-Marke
   (`CoCommitInboxMarkeAsync`). Der Client-/OCC-Pfad bleibt unberührt (dort weiter Targeted-Delivery, keine Marke).
2. **Zwei-Mengen-Inbox** im `AggregateActorBase`: neben `_verarbeiteteCommandIds` (Erfolg) eine
   `_abgelehnteCommandIds`. Re-Delivery-Dedup:
   - CommandId in `verarbeitet` → `Success:true` (wie heute).
   - CommandId in `abgelehnt` → **`Success:false` + (rekonstruierte) Ablehnung**, NIE `Success:true`.
   Die Rehydration (`AggregateRehydrator`) faltet BEIDE Marken (`KommandoVerarbeitet` → verarbeitet,
   `KommandoAbgelehnt` → abgelehnt). `BoundedInbox` ggf. für beide Mengen.
3. **Neue Fold-Achse** `AbgelehntDa` im `Kandidat` (`FaltMarkingAsync`): Ziel-Stream trägt ein
   `KommandoAbgelehnt` mit `CausationId == vorgang`. In `WakeAsync`: für jeden `AbgelehntDa`-Kandidaten, dessen
   Vorgang noch NICHT in `mz.Gescheitert` steht, ein durables `SchrittGescheitert(vorgang, grund)` schreiben
   (die Quelle von `Gescheitert` wandert von der Quittung in den Fold). Danach läuft die Kompensation **exakt
   wie heute** (sie liest `Gescheitert`).
4. **Treiber fire-and-forget:** `SendeAnZiel` → über `CommandEmitter.EmitAsync` (Kausalität so, dass
   `EmitId.Ableiten` == `vorgang` ergibt — SIEHE §5!). `WeckeSelbst` läuft nach **jedem** Send (nicht nur bei
   nicht-negativer Quittung), damit der Manager neu faltet und den Marker (Erfolg ODER Ablehnung) sieht.
   `DetachedProzessSend` + `MeldeFehlschlag`-Message + `MeldeFehlschlagAnManager` + `NotiereFehlschlagAsync`-
   Aufruf über die Quittung entfallen.

## 4. ⚠ Der kritische Kopplungs-Befund (nicht ignorieren!)

**Marker + Zwei-Mengen-Inbox + Fold-Achse müssen ZUSAMMEN in EINEM Schritt landen. Nicht slicebar.**

Grund: Fügst du den `KommandoAbgelehnt`-Marker hinzu, OHNE die `AbgelehntDa`-Fold-Achse, dann liest der Fold
ihn als `ErgebnisDa = true` (er hat ja `CausationId == vorgang`). Im quittungs-verlorenen Fall (genau dem, den
der Fold heilen soll) hat `mz.Gescheitert` den Vorgang NICHT → `WakeAsync` geht in den Vorwärts-Zweig → die
Transition ist `ErgebnisDa` → gilt als „erledigt" → **der Manager schreibt fälschlich `ProzessBeendet(true)`**.
Das ist der stille Falsch-Erfolg (S15-Klasse), den die bewusste P2-Entscheidung („keine Marke auf dem
Ablehnungs-Pfad") heute vermeidet.

Deshalb: der Marker ist NUR sicher, wenn der Fold ihn im SELBEN Schritt als **Fehlschlag** (`AbgelehntDa` →
`SchrittGescheitert`) behandelt, und die Inbox eine Re-Delivery konsistent als Ablehnung (nicht Success)
beantwortet. Alles drei zusammen.

## 5. Determinismus: `vorgang` muss `EmitId.Ableiten` matchen

Heute stempelt der Treiber `CommandId = vorgang` (aus `ProzessId.FürTransition`, seit Bugfix 7/8 mit
Diskriminator `"{ri}:{ci}:{zielId:N}"`). Der `CommandEmitter` stempelt `CommandId = EmitId.Ableiten(k, zielId)`.
Damit der Marker mit `CausationId == vorgang` im Fold matcht, MUSS gelten: **die vom Treiber an `EmitAsync`
übergebene `EmitKausalität` erzeugt via `EmitId.Ableiten` denselben Guid wie der `vorgang`, den der Fold
berechnet.** Zwei Wege:
- (a) `EmitAsync`-Überladung, die eine **vorgegebene** CommandId akzeptiert (der Treiber übergibt `vorgang`
  direkt). Am einfachsten & am sichersten — der Fold-Match bleibt unverändert.
- (b) `ProzessId.FürTransition` und `EmitId.Ableiten` auf DIESELBE Ableitung ziehen und der Fold berechnet den
  Vorgang künftig via `EmitId`. Sauberer (eine Id-Ableitung, EM-1-Geist), aber invasiver — der Fold-Match
  (`FaltMarkingAsync`) und die Kompensation (`ProzessId.FürKompensation`) müssen mitgezogen werden.
**Empfehlung: (a) zuerst** (kleiner, beweisbar), (b) als optionale Nachkonsolidierung. Ohne diese
Übereinstimmung findet der Fold den Marker NICHT → Prozess hängt.

## 6. Verhaltens-Verschiebung (bewusst, dokumentieren)

Fehlschlag-Erkennung wandert von *sofort* (Quittung, synchron beobachtet) auf *eventual* (Selbst-Weckung/Poll,
weil `KommandoAbgelehnt` als `IProzessIntern` KEIN Signal erzeugt → nur `WeckeSelbst` nach dem Send bzw. der
`ProzessOffenIndex`-Backstop/Poll wecken den Manager, damit er den Marker faltet). Single-node über `WeckeSelbst`
schnell; multi-node poll-bounded. Korrekt, aber die Kompensation startet minimal später. **`WeckeSelbst` bleibt
also nötig** (nach jedem Send) — NICHT retiren. Ebenso `ProzessOffenIndex` (Auflage A2).

## 7. Umsetzungs-Scheiben (jede grün, jede bewiesen)

- **Scheibe A (der gekoppelte Kern):** `KommandoAbgelehnt` + Zwei-Mengen-Inbox (`AggregateActorBase` +
  `AggregateRehydrator`) + `AbgelehntDa`-Fold-Achse (`ProzessManager`) + `WakeAsync` schreibt
  `SchrittGescheitert` aus `AbgelehntDa`. **Treiber vorerst NOCH über die Quittung** — d.h. der Marker wird
  geschrieben UND die Quittung schreibt weiter `SchrittGescheitert`; das ist idempotent (`NotiereFehlschlag`
  prüft `Gescheitert.ContainsKey`). So ist Scheibe A **safe** (kein Falsch-Erfolg, weil die Quittung
  `Gescheitert` weiterhin füllt) und isoliert beweisbar. *Beweis Ebene 1 (Fake-Cluster):* Ablehnung →
  `KommandoAbgelehnt` → Fold liest `AbgelehntDa` → `SchrittGescheitert` → Kompensation, **auch wenn die
  Quittung unterdrückt wird** (Fake gibt `null` zurück).
- **Scheibe B (Treiber-Fold):** `SendeAnZiel` → `CommandEmitter` (fire-and-forget, §5-Determinismus);
  `WeckeSelbst` nach jedem Send; `DetachedProzessSend`/`MeldeFehlschlag`-Pfad entfernen. Jetzt trägt allein der
  Fold die Fehlschlag-Erkennung. *Beweis Ebene 1:* wie A, aber es GIBT keine Quittung mehr.
- **Scheibe C (Analyzer, Auflage A6):** Build-Fehler bei rohem `RequestAsync<CommandResult>` außerhalb
  `CommandEmitter` + bei `CancellationToken.None` auf Command-Kanten. (Der Client-Dispatcher
  `ProtoActorAggregateDispatcher` ist der EINE legitime OCC-Sender — allow-listen oder per Kanten-Profil
  unterscheiden. Es gibt keine Roslyn-Analyzer-Test-Harness im Repo → Harness anlegen oder manuell demonstrieren
  + ehrlich dokumentieren.)

## 8. Tor & Beweise

- **Ebene 1 (Prüfstand, Fake-Cluster, `memory/hang-diagnose-in-memory.md`):** Ablehnung → durabler
  `KommandoAbgelehnt` → Manager faltet `AbgelehntDa` → `SchrittGescheitert` → Kompensation, OHNE Quittung
  (Fake-Send liefert `null`). Zusatz: Re-Delivery eines abgelehnten Commands liefert konsistent Ablehnung
  (Zwei-Mengen-Inbox), NIE `Success:true`.
- **Ebene 2 (echtes Marten):** Co-Commit des `KommandoAbgelehnt` = EINE Transaktion (wie die Noop-Marke).
- **Ebene 3 (Cluster, SEQUENTIELL):** `BestellSagaE2ETests.Ungedecktes_Konto_gleicht_live_aus_und_versendet_nicht`
  (der Kompensations-Test) + ALLE Sagas (`ProzessManagerE2ETests`, `ReiseSaga*`, `ProzessBackstopE2ETests`)
  bleiben grün. Das ist der schärfste No-Regression-Check.

## 9. Guardrails (halten!)

- Die sechs Invarianten + TG/EM/GA. Keine Runtime-Reflection (Inv. 4) — neue Routing-Logik = Generator, nie Handschalter.
- Alle Säulen bleiben (API, Generatoren, Marten, Redis, Proto.Actor, gRPC, Client).
- **Verteilte/Actor-Hangs ZUERST in-memory (Fake-Cluster) beweisen**, nie im langsamen Integrationstest raten.
- Integrationstests IMMER **sequentiell** (`Infrastructure.Integration.Tests/xunit.runner.json`). Den
  SnapshotLive-Cold-Boot-Flake NICHT mit Timeouts „härten" — als bekannt akzeptieren.
- Bei neuen Domain-Typen Proto regenerieren (`dotnet run --project Proto.SourceGeneration`). `KommandoAbgelehnt`
  ist `IProzessIntern` → wie `KommandoVerarbeitet` vom Proto/DtoMapper ausgeschlossen; prüfen, dass kein DTO nötig ist.
- **Build:** `dotnet build`. Framework-Fehler ignorieren NUR in `Domain.Client` (bekannter, umbau-unabhängiger
  `_publish`-Bruch). Logik: `Infrastructure.Pruefstand.Tests`. Integration: `Infrastructure.Integration.Tests`.
- **Git:** Commits OHNE `Co-Authored-By`-Zeile; nur auf ausdrückliche Aufforderung.
- **Docs synchron halten:** `CLAUDE.md` (oben) + `docs/backend-neubau-fahrplan.md` („Fortschritt") nach jeder Scheibe.

## 10. Danach

Nach dem Treiber-Fold + Analyzer ist **EM-1 voll erfüllt** (genau ein Emit-Weg, erzwungen). Dann laut Fahrplan:
**P4** (Konsum-Maschine, braucht den GA-1-Marker A4), **P6** (Pipeline-Zerlegung + tote OCC-Helfer +
Trigger-`None`), dann **Feature-Strom** (v.a. Timer/Webhook-Trigger). **P5(b)** nur bei einem echten
Riesen-Prozess. **P7/P8 sind für reinen Single-Node gestrichen.**
