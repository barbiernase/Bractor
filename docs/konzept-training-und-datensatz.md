# Konzept — Datensätze & Training (KI-Modell-Lebenszyklus)

> **Stand: 2026-08-17. Status: KONZEPT — nicht implementiert.** Dieses Dokument entwirft die
> Erweiterung, mit der aus kontinuierlich gesammelten, gelabelten Bildpaaren **Datensätze**
> zusammengeführt und daraus **KI-Modelle trainiert** werden — reproduzierbar, event-sourced und
> im Stil des bestehenden Frameworks (sechs Invarianten, vier Konsumenten, generiert,
> typgetrieben). Training läuft in **Python**, Datensatz-Erzeugung/-Verwaltung in **C#**, die GUI
> in **Blazor** (Slot-Module: Sidebar / Main-View).

Verwandte Doku: [08-frontend-blazor-client.md](08-frontend-blazor-client.md),
[09-python-sdk.md](09-python-sdk.md), [04-konsum-und-prozess-maschine.md](04-konsum-und-prozess-maschine.md).

---

## 1. Ziel & Leitidee

Wir sammeln kontinuierlich Bildpaare (`ImagePair`) und labeln sie (KI + Mensch). Über die
**bestehende Suche** (`ImagePairFilter` → `SearchAsync`) bekommen wir jederzeit eine **Range**
passender Bilder. Diese Ranges wollen wir zu **Datensätzen** zusammenführen, einfrieren und daraus
**Modelle trainieren**.

**Leitidee in einem Satz:** *Du suchst, du schiebst die Treffer in einen Datensatz, du frierst ein
— den Rest (Auflösen, Split, Manifest, Reproduzierbarkeit) macht das System unsichtbar.*

`ImagePair` und die Suche bleiben **unverändert**. Es kommen drei fokussierte Aggregate dazu:

| Aggregat | Verantwortung | Phase |
|---|---|:---:|
| **`Datensatz`** | kuratierte, **eingefrorene, versionierte** Menge gelabelter Bildpaare | 1 |
| **`Trainingslauf`** | ein Trainings-Job: angefordert → läuft → Fortschritt → fertig/gescheitert | 1 |
| **`Modell`** | trainiertes Artefakt: registriert, mit Metriken, „aktiv setzen" → schließt den Kreis zur Inferenz | 2 |

---

## 2. Aggregat `Datensatz`

### 2.1 Lebenszyklus

```
Entwurf  ──(Ranges hinzufügen, manuell +/-)──►  Entwurf  ──FriereEin──►  Eingefroren (v1, immutable)
                                                                              └─► Manifest materialisiert
```

- **Entwurf**: ein **wachsender Korb konkreter Bildpaar-Referenzen**, gefüllt aus **mehreren**
  Such-Ranges (Union, dedupliziert) + manuellen Deltas. Solange Entwurf → zeigt der Korb den
  **aktuellen** Label-/Vollständigkeitsstand aus dem Read-Model (live).
- **Eingefroren**: `FriereEin` friert für **jedes Mitglied den Label-Stand *zum Zeitpunkt des
  Einfrierens*** fest, berechnet den **Split** und vergibt eine `DatensatzVersion`. Danach
  **immutable** → reproduzierbar. Ein späteres Neu-Labeln eines Bildpaars ändert den
  eingefrorenen Datensatz **nicht**.

### 2.2 Range hinzufügen — Variante A (Client sucht, Server löst auf)

Der Decider ist rein (kein I/O) — die Suche darf nicht *im* Decider laufen. Deshalb:

```
GUI  ──FügeRangeHinzu(datensatzId, kriterien)──►  Datensatz
                                                    │ RangeAngefordert(kriterien)   (Event)
        ┌───────────────────────────────────────────┘
        ▼  server-seitiger Resolver (Pull-/Pipeline-Stil, injizierter IImagePairReadStore)
   SearchAsync(filter) → PaareAufgenommen(imagePairIds[], herkunft)  ──►  Datensatz (Union, dedup)
```

**Vorteile:** Der Nutzer fasst **nie IDs** an (er denkt in *Suchen*), es skaliert auf große
Ranges, und die **Kriterien landen als Provenienz** im Log („diese Range kam aus *dieser*
Suche"). Genau deshalb kann ein Datensatz **mehrere** Ranges elegant vereinen:

> „Mein Datensatz = *Juni-Anomalien* + *Juli-OK* + ein paar manuelle."

### 2.3 Split & Klassenbalance

- **Split (train/val/test) beim Einfrieren automatisch & deterministisch**: stratifiziert nach
  Klasse, Default **70/15/15**, fester Seed. Im Normalfall **null Knöpfe** — optional
  überschreibbar (`SetzeSplit`). So ist der Datensatz „fertig" und **vergleichbar** (zwei
  Trainings auf *v1* sind es wirklich).
- **Klassenbalance = nur angezeigte Kennzahl**, **kein Rebalancing**. Wir werfen keine Daten weg,
  um zu balancieren — Ungleichgewicht behandelt später das **Training** über Klassengewichte. Das
  hält den Datensatz ehrlich und die UI verständlich.

### 2.4 Commands / Events

| Command | Event(s) | Bemerkung |
|---|---|---|
| `ErstelleDatensatz(id, name)` `: ICreationCommand` | `DatensatzErstellt` | |
| `FügeRangeHinzu(id, kriterien)` | `RangeAngefordert` → *(Resolver)* → `PaareAufgenommen(ids, herkunft)` | Variante A |
| `NimmPaarAuf(id, imagePairId)` / `EntfernePaar(id, imagePairId)` | `PaarAufgenommen` / `PaarEntfernt` | manuelles Delta |
| `SetzeSplit(id, train, val, test, seed)` | `SplitGesetzt` | optionaler Override |
| `FriereEin(id)` | `EinfrierenAngefordert` → *(Resolver)* → `DatensatzEingefroren(version, mitglieder[])` | Snapshot Label+Split |
| *(automatisch)* | `DatensatzMaterialisiert(manifestPfad, anzahl)` | Manifest auf Platte |

**Ablehnungs-Events** (`ITransientEvent`): `DatensatzExistiertBereits`,
`DatensatzBereitsEingefroren` (jede Änderung nach dem Einfrieren), `DatensatzLeer` (Einfrieren
ohne Mitglieder), `RangeLeer`.

**Wichtig — Wahrheit liegt im Log:** `DatensatzEingefroren` trägt die **vollständige** eingefrorene
Mitgliedschaft `{ImagePairId, Label, Split}` im Event. Damit ist der Datensatz aus dem Log
reproduzierbar; das `manifest.json` (§4) ist nur eine **Datei-Projektion** davon (wie die
vorverarbeiteten Bilder — abgeleitet, nicht autoritativ).

### 2.5 Read-Model (`DatensatzReadModel`, Schema `rm`)

`Name`, `Status`, `AnzahlMitglieder`, `Klassenbalance`, `AnteilMitMenschLabel`, `Version`,
`Ranges[]` (Herkunft), `ManifestPfad`. Bespielt von `DatensatzProjektion` (Pull-Subscriber,
Co-Commit) — exakt wie `ImagePairProjection`.

---

## 3. Aggregat `Trainingslauf`

### 3.1 Lebenszyklus

```
Angefordert ──►  Läuft ──(Fortschritt je Epoche)──►  Abgeschlossen
     │                                                     ▲
     └────────►  Gescheitert / Abgebrochen / Hängengeblieben
```

Ein `Trainingslauf` referenziert **`DatensatzId` + `DatensatzVersion`** (immutabler Input →
reproduzierbar) sowie die **Hyperparameter** (Epochen, LR, Batch, Architektur, Seed).

### 3.2 Der Trigger-Flow: derselbe Python-Client-Weg wie die Inferenz

Der bestehende ML-Worker ist ein out-of-process **Event→Command-Reaktor** (`CqrsClient[State]`,
`@handle.register`, `yield` von Commands, §09). **Training ist dasselbe Muster, nur langlaufend
mit Fortschritt:**

```
GUI ──StarteTraining(datensatzId, version, hyperparameter)──►  Trainingslauf-Aggregat
                                                                 │ TrainingAngefordert(manifestPfad, hyperparameter)
        ┌─────────────────────────────────────────────────────────┘   (Event, per Capabilities abonniert)
        ▼  gRPC Event-Push
  Python TrainingWorker (CqrsClient, eigener Prozess, GPU)
     • liest manifest.json  (Pfad im Event, bzw. HTTP wie der Classifier heute Bilder holt)
     • Training läuft in  asyncio.to_thread   → Event-Loop bleibt frei
     • Trainingsthread → asyncio.Queue → Handler yieldet mehrfach über die Zeit:
          MeldeTrainingBegonnen                         ──► TrainingBegonnen
          MeldeFortschritt(epoche, loss, metriken)  ×N  ──► TrainingFortschritt
          MeldeTrainingAbgeschlossen(modellPfad, metriken) ──► TrainingAbgeschlossen
     • bei Exception:  MeldeTrainingGescheitert(grund)  ──► TrainingGescheitert
```

Maximal architekturtreu: **kein neuer Transport, kein neuer Konsumenten-Typ.** Der Python-Handler
ist ein Async-Generator, der über Minuten/Stunden **mehrfach yieldet** (Fortschritt) — der Router
verpackt jeden Yield als Command zurück. Der `Trainingslauf`-Decider foldet die Fortschritts-Events
in seinen State (`AktuelleEpoche`, `MetrikHistorie`, …).

**Schön dabei:** Weil Fortschritt event-sourced zurückfließt, **streamt die Live-Trainingskurve
von selbst** über den bestehenden gRPC-Event-Push in die Blazor-Training-Stage — genau der
Chart/ApexCharts-Weg, den es schon gibt.

### 3.3 Robustheit — vorhandene Mechanismen

- **Timeout**: bei `TrainingBegonnen` eine **`Frist`** planen (`IDbClock` / `FristScheduler`, in
  `Host.Grpc` bereits über `AddDeadlines` verdrahtet). `FristFaellig` vor Abschluss →
  `MarkiereAlsHängengeblieben`. Kein Polling von Hand. *(Verdrahtungs-Detail: die Frist muss den
  `Trainingslauf` als Ziel adressieren — heute mappt `AddDeadlines` auf
  `Domain.Erinnerung.FristFaellig`; hier analog auf ein Trainings-Ziel-Command.)*
- **Exactly-once / Idempotenz**: greift automatisch (Empfänger-Dedup über `ReaktionId`/
  `CausationId`) — ein doppelt zugestelltes `TrainingAngefordert` startet **kein** zweites Training.
- **Abbruch**: `BricheTrainingAb` (GUI) → `TrainingAbgebrochen`; der Worker prüft ein Abbruch-Flag
  zwischen Epochen (kooperativer Abbruch).

### 3.3 Commands / Events

| Command | Quelle | Event |
|---|---|---|
| `StarteTraining(id, datensatzId, version, hyperparameter)` `: ICreationCommand` | GUI | `TrainingAngefordert(manifestPfad, hyperparameter)` |
| `MeldeTrainingBegonnen(id)` | Python | `TrainingBegonnen` |
| `MeldeFortschritt(id, epoche, metriken)` | Python (×N) | `TrainingFortschritt` |
| `MeldeTrainingAbgeschlossen(id, modellPfad, endmetriken)` | Python | `TrainingAbgeschlossen` |
| `MeldeTrainingGescheitert(id, grund)` | Python | `TrainingGescheitert` |
| `BricheTrainingAb(id)` | GUI | `TrainingAbgebrochen` |
| `MarkiereAlsHängengeblieben(id)` | Frist | `TrainingHängengeblieben` |

### 3.4 Read-Model (`TrainingslaufReadModel`, Schema `rm`)

`DatensatzId`+`Version`, `Status`, `AktuelleEpoche`/`GesamtEpochen`, `MetrikHistorie[]` (für die
Live-Kurve), `Hyperparameter`, `ModellPfad`, `Endmetriken`, `Startzeit`/`Dauer`.

---

## 4. Der C#↔Python-Schnitt: ein Manifest

Genau die Aufteilung „Erzeugung in C#, Training in Python, nur nutzen":

- **C# materialisiert** beim Einfrieren ein **`manifest.json`** — eine reine Liste. C# ist
  alleiniger **Eigentümer der Datensatz-Erzeugung**.
- **Python liest nur** das Manifest, trainiert, **schreibt ein Modell**.
- **Events tragen Pfade, keine Blobs** — Invariante „Signal ist nur ein Weckruf, schwere Daten
  liegen daneben" (wie die Bildpfade heute).

```
datasets/{datensatzId}/{version}/manifest.json
├─ datensatzId, version, erstelltAm
├─ klassen: ["KeineAnomalie","Questionable","Anomalie"]
└─ samples: [
     { imagePairId, dc0Pfad, dc2Pfad, label, split }   // split ∈ {train,val,test}
   ]

models/{trainingslaufId}/model.pt      ← Python-Output
models/{trainingslaufId}/metrics.json  ← Python-Output
```

Auslieferung wie heute per Datei-Endpunkt (`/api/files/…`, `LocalFilePathResolver`) oder
gemeinsames Volume. Der Python-`TrainingWorker` lädt das Manifest über den Pfad im
`TrainingAngefordert`-Event.

---

## 5. `Modell` — der Kreis zur Inferenz *(Phase 2)*

| Command | Event | Wirkung |
|---|---|---|
| `RegistriereModell(id, trainingslaufId, pfad, metriken)` `: ICreationCommand` | `ModellRegistriert` | Artefakt wird erstklassig |
| `SetzeAktiv(id)` | `ModellAktiviert` | genau ein aktives Modell je Zweck |
| `Archiviere(id)` | `ModellArchiviert` | |

**Kreis geschlossen:** der bestehende **Inferenz-Worker** (`classifier.py`) subscribed
`ModellAktiviert`, lädt das neue Modell (TorchScript, wie heute) und klassifiziert damit weiter.
So entsteht die Kette **Datensatz → Training → Modell → Inferenz** — vollständig event-sourced und
auditierbar.

**Optionale Saga (Prozess-Maschine, §04):** die Training→Modell-Kante deklarativ verbinden, statt
sie zu Fuß zu verdrahten:

```csharp
Prozess<TrainingAbgeschlossen>.Definiere(p =>
    p.Auf<TrainingAbgeschlossen>().Sende<RegistriereModell>(e =>
        new RegistriereModell(NeueId(), e.TrainingslaufId, e.ModellPfad, e.Endmetriken)));
```

---

## 6. GUI (Blazor — nur neue Slot-Module, kein neuer Unterbau)

### 6.1 Neue Main-View: „Datensatz-Komposition" (`IStageModule`)

Transfer-/Korb-Layout — Suchergebnis links, Datensatz-Korb rechts:

```
┌── Suchergebnis (Kandidaten) ──────────┐   ┌── Datensatz „Charge-Juni" (Entwurf) ──┐
│ Filter: Juni 2025 · Produktlabel=Anom │   │  4 812 Paare · 3 Ranges · noch offen  │
│ ▣ 12.06 09:11  Anomalie                │   │  Klassenbalance ▐OK 60▐Frag 25▐Anom15 │
│ ▣ 11.06 08:10  Anomalie          [→]   │   │  Split  train 70 / val 15 / test 15   │
│                     gesamt 1 240 Tr.   │   │  Provenienz:                          │
│  [ganze Range → Datensatz]             │   │   • Juni·Anomalie 1 240               │
│                                        │   │   • Juli·OK       3 100  [entfernen]  │
│                                        │   │  [Einfrieren]  [Materialisieren]      │
└────────────────────────────────────────┘   └────────────────────────────────────────┘
```

- Der **Filter links** ist der vorhandene `SuchKriterien`-Baustein inkl. **Produktionstage-Baum**
  (die natürliche Zeit-Range bei kontinuierlicher Sammlung).
- „**ganze Range → Datensatz**" fügt das *komplette* Suchergebnis hinzu (nicht nur die sichtbare
  Seite) — Union, Duplikate verpuffen.
- Rechts live: **Größe, Klassenbalance, Split, Provenienz**.

### 6.2 Weitere Module

| Slot | Modul | Inhalt |
|---|---|---|
| `IStageModule` | **Training-Dashboard** | „Neues Training" (Datensatz + Hyperparameter) + **Live-Kurven** (loss/accuracy) je Lauf über ApexCharts, gespeist vom Event-Stream |
| `IStageModule` *(Ph.2)* | **Modelle** | registrierte Modelle, Metrik-Vergleich, „aktiv setzen" |
| `ISidebarModule` | **Datensatz-Liste** | Entwurf/Eingefroren, Größe, Version |
| `ISidebarModule` | **Trainingslauf-Liste** | Status-Badges live |
| `IHeadlessModule` | **Data/Refresh/Intent** | Stores + RefreshHandler für die neuen Read-Models, IntentHandler für „Starte Training"/„Friere ein" |

Alles über die **generierte Verdrahtung** (`AddClientDomain_…` / `AddModules_…`) — keine
Handverdrahtung, keine `IViewModel`-Klassen (neues Modell: RefreshHandler + IntentHandler +
Client-Events).

---

## 7. Nutzer-Flow (so einfach wie möglich)

```
1. Suchen        (Zeit-Range im Produktionstage-Baum + Filter)
2. „ganze Range → Datensatz"      ← beliebig oft, verschiedene Suchen
3. Balance/Größe anschauen
4. „Einfrieren"                    ← Split + Manifest macht das System automatisch
5. „Training starten"  (Datensatz wählen, Hyperparameter)
6. Live-Kurve zusehen  ← Fortschritt streamt event-sourced ins Dashboard
        └─► fertiges Modell (Phase 2: „aktiv setzen" → Inferenz nutzt es)
```

Drei Klicks bis zum trainierbaren Datensatz; alles Technische (Auflösen, Split, Manifest,
Reproduzierbarkeit) bleibt unsichtbar — Invariante 5 („der Fachcode/Nutzer bleibt rein").

---

## 8. Reproduzierbarkeit & Provenienz (die ES-Stärke)

- **Eingefrorener Datensatz** = unveränderlicher Input → zwei Trainings sind vergleichbar.
- **`TrainingAngefordert`** hält `DatensatzId`+`Version`, Hyperparameter, Seed fest.
- **Provenienz je Range** (welcher Filter, wann, wie viele) → nachvollziehbar, *woraus* ein Modell
  gelernt hat.
- Alles im Log → voller Audit-Trail „Datensatz → Training → Modell → Inferenz".

---

## 9. Was zu bauen wäre (Aufwandsskizze — bewusst noch nicht implementiert)

| Baustein | Ort | Anmerkung |
|---|---|---|
| Aggregate `Datensatz`, `Trainingslauf` (+ `Modell` Ph.2) | `Domain/Datensatz/`, `Domain/Trainingslauf/` | Commands/Events/Decider/Applier/State — wie `Domain/ImagePair` |
| **Proto-DTOs** je neuem Command/Event | `Proto.SourceGeneration` → `ProtoRepo` | Pflicht, sonst bricht `DtoMapperGenerator` (CLAUDE.md) |
| Projektionen + Co-Commit-Stores | `Domain.Projections`, `Domain.Infrastructure` (Schema `rm`) | wie `ImagePairProjection`/`ImagePairStore` |
| **Server-Resolver** (Range-Auflösung, Freeze-Snapshot, Manifest) | `Domain.Pipeline`/`Infrastructure` | injizierter `IImagePairReadStore`, yieldet Commands/schreibt Datei |
| **Python `TrainingWorker`** | neben `Domain.Client.Worker.Python.ML` | `CqrsClient`, Event→Fortschritts-Commands, `to_thread` + Queue |
| Blazor-Module (Komposition-Stage, Dashboard, Sidebars, Headless) | `Domain.Client.Modules.Blazor` | generierte Verdrahtung |
| Frist-Verdrahtung (Timeout) | `Host.Grpc` | analog zum bestehenden `AddDeadlines` |

---

## 10. Offene Punkte / spätere Ausbaustufen

- **Hyperparameter-Sweeps**: mehrere `Trainingslauf` unter einer Klammer (Phase 3). Der Zuschnitt
  (Trainingslauf referenziert Datensatz+Hyperparameter) trägt das bereits — eine „Sweep"-Klammer
  wäre nur eine dünne Fan-out-Saga (`SendeJe`, §04).
- **Datensatz-Diff/Vererbung**: „v2 = v1 + Range X" (Delta-Datensätze) — optional, das
  Range-Union-Modell macht es natürlich.
- **Drift-getriggertes Retraining**: automatisch neuen Datensatz-Zeitschnitt + Training anstoßen,
  wenn genug neue gelabelte Bilder da sind (Timer-Trigger, §04) — bewusst Phase ≥ 2.
