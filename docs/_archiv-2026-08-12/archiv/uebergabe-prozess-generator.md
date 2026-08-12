# Übergabe: Prozess-Maschinerie generieren (Phase 5, Verallgemeinerung)

Handover für einen neuen Agenten/eine neue Session. Ziel: den **handgeschriebenen Überweisungs-Piloten**
zur **generierten** Prozess-Maschinerie verallgemeinern (Spec 15) — pro `IProzessPlan` ein Prozess-Aggregat
+ Treiber-Verdrahtung, statt handgeschrieben.

Grundlage: `docs/spezifikation.md` Kap. 10–12 + 15; `CLAUDE.md` (Phase-5-Block ist aktuell).

---

## 0. Mentales Modell (zuerst lesen)

Der Entwickler schreibt (Spec 10.2) **nur zwei Dinge**: einen **Plan** (`record … : IProzessPlan` mit
`ProzessSchritte`) und eine **Handle-Bindung** (yieldet den Plan auf ein Auslöse-Event). Alles andere —
das **Prozess-Aggregat** (Zustandsmaschine) und der **Treiber** (Adapter auf dem Prozess-Stream mit
Command-mit-Quittung) — ist *für jeden Plan identisch* und wird deshalb **generiert**.

Der Pilot beweist die Form vollständig und **live** (Prüfstand + Integration grün). Diese Session
verallgemeinert nur — sie erfindet **nichts neu**. Abnahme: die handgeschriebenen Pilot-Teile löschen,
dieselben Tests bleiben grün.

---

## 1. Ist-Stand (GRÜN — die Vorlage, nicht ohne Grund anfassen)

Der Pilot läuft end-to-end aus EINEM Fach-Command: `BeauftrageUeberweisung` → `UeberweisungBeauftragt`
→ Handler yieldet `StarteProzess` → Prozess-Aggregat → Treiber (signal-getrieben, awaited `RequestAsync`,
**kein Hang**) → 3 Schritte → `Abgeschlossen`; Fehlerfall gleicht aus (`Fehlgeschlagen`, nichts radiert).
Tests: Prüfstand **56/56** (Phase5: `ProzessPlanTests`, `KontoAggregatTests`, `UeberweisungsProzessTests`,
`UeberweisungsTreiberTests`), Integration **9/9** (`ProzessTreiberE2ETests`: Happy Path, Kompensation, End-to-End).

**Verträge (bleiben, sind schon generisch):**
- `Abstractions/IProzessPlan.cs` — `IProzessPlan { ProzessSchritte Schritte }` + `ProzessSchritte.Start.Dann(cmd, rückgängig:…)`.
- `Abstractions/IProzessSchrittCommand.cs` — Marker mit `Vorgang` (Korrelation) + `MitVorgang(…)` (Treiber injiziert die deterministische Schritt-Id).
- `Abstractions/ProzessId.cs` — `Für(planTyp, streamId, version)` / `FürSchritt` / `FürRückabwicklung`.

**Die Vorlage (was verallgemeinert wird):**
- `Domain/Ueberweisung/UeberweisungsProzess.cs` + `Commands.cs`/`Events.cs`/`Decider.cs`/`Applier.cs`
  — das **Prozess-Aggregat** (Zustandsmaschine Spec 11.5). Die Logik ist plan-AGNOSTISCH; nur
  `ProzessGestartet` trägt die Plan-Felder flach.
- `Infrastructure/Prozess/UeberweisungsTreiber.cs` — die **Treiber-Logik** (falten → senden → Quittung
  → selbst-weitermachen → Kompensation). `_send`-Seam = einzige Cluster-Berührung.
- `Infrastructure/Prozess/TreiberActor.cs` (+ `UeberweisungsTreiberKind`) — der Treiber als virtueller
  Cluster-Actor; **der (A)-Fix ist drin**: `system.Cluster()` in der Spawn-Factory, bounded Token.
- `Infrastructure/Prozess/ProzessWiring.cs` — `AddUeberweisungsProzess()`: Kind + `PullPathRegistration`
  auf die Prozess-Stream-Signale (macht den Treiber signal-getrieben).

**Bleibt handgeschrieben (Entwickler-Code, NICHT generieren):**
- `Domain/Konto/` — Ziel-Aggregat (mit der EINEN Dedup-Zeile `VerarbeiteteVorgaenge`, Spec 11.4).
- `Domain/Ueberweisung/UeberweisungsPlan.cs` — der Plan.
- `Domain/Auftrag/Ueberweisungsauftrag.cs` — Trigger-Aggregat.
- `Domain.Projections/Ueberweisungen.cs` — die Handle-Bindung (Start).

---

## 2. Die Aufgabe: was generieren, wie zerlegt

**Empfohlene Zerlegung (minimiert Generat, maximiert generischen Kern):**

1. **Generischer Treiber-Kern (KEIN Generat, einmal schreiben):** einen `ProzessTreiber` (generisch statt
   `UeberweisungsTreiber`), der über ein Interface auf die gefaltete Sicht liest — z. B.
   `IProzessSicht { int? NaechsterVorwaertsSchritt; int? NaechsteRueckabwicklung; ProzessSchritte? Schritte; }`
   plus Quittung-Builder (die Per-Plan-Commands `MeldeSchrittErledigt` etc.). Die Prozess-States implementieren
   `IProzessSicht`; die Quittung-Commands liefert entweder das Interface oder ein kleiner generierter Adapter.
   → Der Treiber wird damit **einmalig** generisch; nur die Wiring-Teile sind pro Plan.

2. **Prozess-Generator (Domain-seitig, `Domain.SourceGeneration`):** emittiert pro `IProzessPlan`-Typ das
   **Prozess-Aggregat** als `partial`-Klassen (State + Decider + Applier + Commands + Events) — Vorlage 1:1
   `UeberweisungsProzess*`, nur `{Plan}` parameterisiert und `ProzessGestartet` mit den Plan-Feldern. Da es
   ein NORMALES Aggregat ist, greifen danach die bestehenden Generatoren (HandlerFactory, AggregateActor,
   Signal, DtoMapper) automatisch.

3. **Treiber-Wiring-Generator (`Infrastructure.SourceGeneration`):** emittiert pro Plan den Treiber-Kind
   (`{Plan}TreiberKind`) + die `PullPathRegistration` + eine `AddGeneratedProzesse()`-Sammelmethode (Muster:
   `PullPathGenerator` → `AddGeneratedPullPaths`). Der Host ruft dann EINEN Aufruf `AddGeneratedProzesse()`.

4. **Start-Bindung:** vorerst wie im Piloten — der Handle-yield von `StarteProzess` läuft über die
   Reaktions-Route. (Der elegante Spec-Weg „Handler yieldet den **Plan**" braucht die Emit-Signatur
   `IMessagePayload → IPipelineOutput` im `SubscriberDispatchGenerator` — eigener, größerer Schritt, NICHT
   Teil dieser Übergabe. Erst den Aggregat/Wiring-Generator liefern.)

**Plan-Typ → Prozess-Kind-Tabelle** (Spec 10.4, „Geschwister von `TriggerToPipelineId`") fällt aus Schritt 3.

---

## 3. Die Stolpersteine (die den Agenten sonst ausbremsen — PFLICHTLEKTÜRE)

- **Stale Generator-Inkrement-State:** persistierte `.g.cs` unter `obj/generated/**` sind oft ein ALTER
  Snapshot und lügen; die Wahrheit ist die kompilierte Ausgabe (die nur mit `/p:EmitCompilerGeneratedFiles=true`
  überhaupt auf Platte landet, unter `obj/Debug/net9.0/generated/**`). Bei Zweifel **hart cleanen**:
  `rm -rf <Projekt>/obj <Projekt>/bin` und `--no-incremental` bauen. Das hat in der Pilot-Session mehrfach
  Zeit gekostet — nie aus einem stale Snapshot schließen.
- **EIN `IState` pro Namespace.** Die Command→Aggregat-Zuordnung (`PipelineActorGenerator`,
  `EventCommandMappingGenerator`) leitet den Aggregat-Namen aus dem **Namespace** ab. Zwei Aggregate in
  einem Namespace brechen den Build (genau daran ist im Piloten der Trigger gebrochen → eigener Namespace
  `Domain.Auftrag`). Generierte Prozess-Aggregate also je in einen eigenen Namespace.
- **Proto-Regen-Zyklus** bei neuen Command/Event-Typen (die generierten Prozess-Commands/Events!):
  `dotnet run --project Proto.SourceGeneration` → `dotnet build ProtoRepo` → `dotnet build Infrastructure`.
  Fehlt der DTO, bricht der `DtoMapperGenerator` mit „`{Name}Dto` nicht gefunden". **Wichtig:** generierte
  Typen aus einem Source-Generator sieht `Proto.SourceGeneration` u. U. NICHT (es scannt Quell-Symbole von
  Domain/Domain.Projections/Domain.Pipeline). → Prüfen, ob generierte Prozess-Commands/Events im Proto landen;
  falls nicht, ist das der erste zu lösende Knoten (evtl. muss der Prozess-Generator auch einen Proto-Beitrag
  liefern oder die Typen müssen als Quellcode-Partial sichtbar sein).
- **Determinismus nicht verlieren:** `ProzessId` (Start) + `Vorgang` (Schritt-Dedup) sind die Korrektheits-
  grundlage. Der generierte Code muss sie 1:1 wie der Pilot verwenden.
- **Treiber-Actor:** `system.Cluster()` MUSS in der Spawn-Factory bleiben, Token bounded — der (A)-Hang-Fix.
  Nicht „vereinfachen".

---

## 4. Verifikations-Disziplin (PFLICHT)

1. **In-memory zuerst** (`Infrastructure.Pruefstand.Tests`, kein Docker): der generierte Prozess-Aggregat
   + generischer Treiber müssen dieselben Prüfstand-Tests bestehen wie der Pilot (Zustandsmaschine, Treiber-
   Fake-Cluster). Am besten: die bestehenden `UeberweisungsProzessTests`/`UeberweisungsTreiberTests` gegen
   den GENERIERTEN Prozess laufen lassen (Pilot-Handschrift entfernt).
2. **Erst dann** die Integrationstests `ProzessTreiberE2ETests` (gegen Docker: Postgres/Consul/Redis) —
   sequentiell (`xunit.runner.json` schaltet Parallelität ab; **immer sequentiell** laufen lassen).
3. **Abnahme-Tor:** die handgeschriebenen `UeberweisungsProzess*`- und `UeberweisungsTreiber`-Dateien
   LÖSCHEN, den Generator die Äquivalente erzeugen lassen, **alle** Tests bleiben grün (Prüfstand 56/56,
   Integration 9/9). Kein Test darf umgeschrieben werden müssen außer Namens-/Namespace-Anpassungen.
4. Host bootet: `Host.Grpc` startet mit dem generierten Prozess-Kind, keine Exceptions.

Build/Test:
```
dotnet build <Projekt> --no-incremental
dotnet test Infrastructure.Pruefstand.Tests
dotnet test Infrastructure.Integration.Tests   # Docker läuft; sequentiell
```

---

## 5. Leitplanken (nicht verletzen)

- **Nichts neu erfinden** — der Pilot ist die exakte Vorlage. Generat = dieselbe Form, parameterisiert.
- **Idempotenz/Dedup ist Domänensache** (`Vorgang` im Command); der Generator verdrahtet, erfindet keine Dedup.
- **Determinismus trägt die Korrektheit** — `ProzessId`/`Vorgang` unverändert übernehmen.
- **Eine Maschine** — der Treiber ist der bestehende Pull-Adapter-Mechanismus (Signal → Wake); kein Sonderweg.
- **Verteilte Hangs in-memory beweisen** (Fake-Cluster), nie im langsamen Integrationstest raten.
- **Kein Domänen-Glue im Framework** — generierte Wiring-Teile kennen den Plan nur als Metadaten (Muster: `PullPathGenerator`).

---

## 6. Exakte Vorlage-Dateien (1:1 als Muster)

- Prozess-Aggregat: `Domain/Ueberweisung/{UeberweisungsProzess,Commands,Events,Decider,Applier}.cs`
- Treiber-Logik: `Infrastructure/Prozess/UeberweisungsTreiber.cs`
- Treiber-Actor + Kind: `Infrastructure/Prozess/TreiberActor.cs`
- Wiring: `Infrastructure/Prozess/ProzessWiring.cs`
- Muster-Generatoren zum Abschauen: `Infrastructure.SourceGeneration/PullPathGenerator.cs`
  (Kind-Contributor + Registrierung + `Add…`-Sammelmethode), `Domain.SourceGeneration/HandlerFactoryGenerator.cs`
  (Aggregat-Scan über `IState`).
- Tests, die grün bleiben müssen: `Infrastructure.Pruefstand.Tests/Phase5/*`, `Infrastructure.Integration.Tests/ProzessTreiberE2ETests.cs`.

Reihenfolge-Empfehlung: (1) generischer `ProzessTreiber` + `IProzessSicht`, gegen die bestehenden Treiber-
Tests grün. (2) Prozess-Aggregat-Generator, gegen die Zustandsmaschinen-Tests grün. (3) Wiring-Generator +
`AddGeneratedProzesse`. (4) Pilot-Handschrift löschen, Integration grün. Klein, einzeln verifiziert.
