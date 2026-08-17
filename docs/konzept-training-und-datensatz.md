# Konzept — Datensätze & Training (KI-Modell-Lebenszyklus)

> **Stand: 2026-08-17. Status: KONZEPT — nicht implementiert.** Vollständiger Entwurf der
> Erweiterung, mit der aus kontinuierlich gesammelten, gelabelten Bildpaaren **Datensätze**
> dynamisch zusammengeführt und daraus **KI-Modelle trainiert** werden — reproduzierbar,
> event-sourced, im Stil des bestehenden Frameworks (sechs Invarianten, vier Konsumenten,
> generiert, typgetrieben). **Training in Python**, **Datensatz-Erzeugung/-Verwaltung in C#**,
> **Steuerung in Blazor** (Slot-Module: Sidebar / Main-View).
>
> Visuelles Board: `docs/konzept-training-und-datensatz.board.html`.
> Verwandt: [08-frontend-blazor-client.md](08-frontend-blazor-client.md),
> [09-python-sdk.md](09-python-sdk.md), [04-konsum-und-prozess-maschine.md](04-konsum-und-prozess-maschine.md).

---

## Inhalt

1. Ziel & Leitidee
2. Der Zuschnitt — drei Aggregate
3. Aggregat `Datensatz`
4. Aggregat `Trainingslauf`
5. Aggregat `Modell` (Phase 2)
6. Der C#↔Python-Schnitt — **ein Query-Kanal, kein File**
7. **Query-Parität Python ↔ Blazor** (Analyse + Lücke)
8. GUI (Blazor-Module)
9. Nutzer-Flow
10. Reproduzierbarkeit & Provenienz
11. Was zu bauen wäre
12. Offene Punkte

---

## 1. Ziel & Leitidee

Wir sammeln kontinuierlich Bildpaare (`ImagePair`) und labeln sie (KI + Mensch). Über die
**bestehende Suche** (`ImagePairFilter` → `SearchAsync`) bekommen wir jederzeit eine **Range**
passender Bilder. Diese Ranges führen wir zu **Datensätzen** zusammen, frieren sie ein und
trainieren daraus **Modelle**.

**Leitidee in einem Satz:** *Du suchst, du schiebst die Treffer in einen Datensatz, du frierst ein
— den Rest (Auflösen, Split, Reproduzierbarkeit, Zustellung an Python) macht das System
unsichtbar.*

`ImagePair` und die Suche bleiben **unverändert**.

---

## 2. Der Zuschnitt — drei Aggregate

| Aggregat | Verantwortung | Phase |
|---|---|:---:|
| **`Datensatz`** | kuratierte, **eingefrorene, versionierte** Menge gelabelter Bildpaare — Vereinigung mehrerer Such-Ranges | 1 |
| **`Trainingslauf`** | ein Trainings-Job: angefordert → läuft → Fortschritt → fertig/gescheitert | 1 |
| **`Modell`** | trainiertes Artefakt: registriert, mit Metriken, „aktiv setzen" → schließt den Kreis zur Inferenz | 2 |

Kleine, fokussierte Aggregate — je ein Ordner unter `Domain/`, wie `Domain/ImagePair`.

---

## 3. Aggregat `Datensatz`

### 3.1 Lebenszyklus

```
Entwurf  ──(Ranges +/-, manuell +/-)──►  Entwurf  ──FriereEin──►  Eingefroren (v1, immutable)
    │ dynamisch, Live-Label-Status                                     └─► Projektion macht Samples
    │                                                                       queryierbar (Schema rm)
```

- **Entwurf** = ein **wachsender Korb konkreter Bildpaar-Referenzen**, gefüllt aus **mehreren**
  Such-Ranges (Union, dedupliziert) + manuellen Deltas. **Dynamisch**: solange Entwurf, zeigt der
  Korb den **aktuellen** Label-/Vollständigkeitsstand aus dem Read-Model (live).
- **Eingefroren** = `FriereEin` friert für **jedes Mitglied den Label-Stand *zum Zeitpunkt des
  Einfrierens*** fest, berechnet den **Split** und vergibt eine `DatensatzVersion`. Danach
  **immutable**. Ein späteres Neu-Labeln eines Bildpaars ändert den eingefrorenen Datensatz
  **nicht** → reproduzierbar.

> **Warum überhaupt einfrieren, wenn der Datensatz dynamisch ist?** Weil *Komposition* und
> *Training* zwei Dinge sind: Die **Komposition ist dynamisch** (Draft), aber **Training läuft
> gegen eine eingefrorene Version** — sonst ist ein Lauf nicht reproduzierbar (Bilder werden
> kontinuierlich weiter gelabelt). Einfrieren ist **kein File-Dump**, sondern ein **Event**:
> `DatensatzEingefroren` trägt die konkrete Mitgliedschaft `{ImagePairId, Label, Split}` — der
> Snapshot liegt **im Postgres-Event-Store**, immutabel, quasi gratis.

### 3.2 Range hinzufügen — der Server löst auf (Variante A)

Der Decider ist rein (kein I/O) — die Suche darf nicht *im* Decider laufen. Deshalb:

```
GUI  ──FügeRangeHinzu(datensatzId, kriterien)──►  Datensatz
                                                    │ RangeAngefordert(kriterien)   (Event)
        ┌───────────────────────────────────────────┘
        ▼  server-seitiger Resolver (Pull-/Pipeline-Stil, injizierter IImagePairReadStore)
   SearchAsync(filter) → PaareAufgenommen(imagePairIds[], herkunft)  ──►  Datensatz (Union, dedup)
```

**Der Nutzer fasst nie IDs an** — er denkt in *Suchen*. Die **Kriterien landen als Provenienz** im
Log („diese Range kam aus *dieser* Suche"). Genau deshalb kann ein Datensatz **mehrere** Ranges
elegant vereinen: „Mein Datensatz = *Juni-Anomalien* + *Juli-OK* + ein paar manuelle."

### 3.3 Split & Klassenbalance

- **Split (train/val/test) beim Einfrieren automatisch & deterministisch**: stratifiziert nach
  Klasse, Default **70/15/15**, fester Seed. Im Normalfall **null Knöpfe** — optional
  überschreibbar (`SetzeSplit`). So sind zwei Trainings auf *v1* wirklich vergleichbar.
- **Klassenbalance = nur angezeigte Kennzahl**, **kein Rebalancing**. Wir werfen keine Daten weg —
  Ungleichgewicht behandelt später das **Training** über Klassengewichte.

### 3.4 Commands / Events

| Command | Event(s) | Bemerkung |
|---|---|---|
| `ErstelleDatensatz(id, name)` `: ICreationCommand` | `DatensatzErstellt` | |
| `FügeRangeHinzu(id, kriterien)` | `RangeAngefordert` → *(Resolver)* → `PaareAufgenommen(ids, herkunft)` | Variante A |
| `NimmPaarAuf(id, imagePairId)` / `EntfernePaar(id, imagePairId)` | `PaarAufgenommen` / `PaarEntfernt` | manuelles Delta |
| `SetzeSplit(id, train, val, test, seed)` | `SplitGesetzt` | optionaler Override |
| `FriereEin(id)` | `EinfrierenAngefordert` → *(Resolver)* → `DatensatzEingefroren(version, mitglieder[])` | Snapshot Label+Split |

**Ablehnungs-Events** (`ITransientEvent`): `DatensatzExistiertBereits`,
`DatensatzBereitsEingefroren` (jede Änderung nach dem Einfrieren), `DatensatzLeer`, `RangeLeer`.

> **Kein `MaterialisiereDatensatz` / kein `manifest.json` mehr** (Änderung ggü. erstem Entwurf).
> Das Einfrieren macht die Samples direkt **queryierbar** (§6) — es braucht keine Datei auf Platte.

### 3.5 Read-Models (Schema `rm`, Co-Commit-Projektion wie `ImagePairProjection`)

- `DatensatzReadModel`: `Name`, `Status`, `AnzahlMitglieder`, `Klassenbalance`,
  `AnteilMitMenschLabel`, `Version`, `Ranges[]` (Herkunft).
- `DatensatzSampleReadModel`: je eingefrorenem Mitglied eine Zeile `{DatensatzId, Version,
  ImagePairId, Dc0Pfad, Dc2Pfad, Label, Split}` — die **queryierbare** Trainings-Wahrheit
  (bespielt aus `DatensatzEingefroren`). Grundlage von `HoleDatensatzSamples` (§6).

---

## 4. Aggregat `Trainingslauf`

### 4.1 Lebenszyklus

```
Angefordert ──►  Läuft ──(Fortschritt je Epoche)──►  Abgeschlossen
     │                                                     
     └────────►  Gescheitert / Abgebrochen / Hängengeblieben
```

Referenziert **`DatensatzId` + `DatensatzVersion`** (immutabler Input → reproduzierbar) sowie die
**Hyperparameter** (Epochen, LR, Batch, Architektur, Seed).

### 4.2 Der Trigger-Flow — derselbe Python-Client-Weg wie die Inferenz

Der bestehende ML-Worker ist ein out-of-process **Event→Command-Reaktor** (`CqrsClient[State]`,
`@handle.register`, `yield` von Commands, §09). **Training ist dasselbe Muster, nur langlaufend
mit Fortschritt:**

```
GUI ──StarteTraining(datensatzId, version, hyperparameter)──►  Trainingslauf-Aggregat
                                                                 │ TrainingAngefordert(datensatzId, version, hyperparameter)
        ┌─────────────────────────────────────────────────────────┘   (Event, per Capabilities abonniert)
        ▼  gRPC Event-Push
  Python TrainingWorker (CqrsClient, eigener Prozess, GPU)
     • holt die Sample-Liste per Query (§6):  await self.query(HoleDatensatzSamples(id, version, seite))
     • holt Pixel je Bild über /api/files/…   (HTTP, wie die Inferenz heute)
     • Training läuft in  asyncio.to_thread   → Event-Loop bleibt frei
     • Trainingsthread → asyncio.Queue → Handler yieldet mehrfach über die Zeit:
          MeldeTrainingBegonnen                         ──► TrainingBegonnen
          MeldeFortschritt(epoche, loss, metriken)  ×N  ──► TrainingFortschritt
          MeldeTrainingAbgeschlossen(modellPfad, metriken) ──► TrainingAbgeschlossen
     • bei Exception:  MeldeTrainingGescheitert(grund)  ──► TrainingGescheitert
```

Maximal architekturtreu: **kein neuer Transport, kein neuer Konsumenten-Typ.** Der Python-Handler
ist ein Async-Generator, der über Minuten/Stunden **mehrfach yieldet**; der Router verpackt jeden
Yield als Command zurück. Der `Trainingslauf`-Decider foldet die Fortschritts-Events in seinen
State.

**Schön dabei:** Weil Fortschritt event-sourced zurückfließt, **streamt die Live-Trainingskurve
von selbst** über den gRPC-Event-Push in die Blazor-Training-Stage — genau der Chart/ApexCharts-Weg.

### 4.3 Robustheit — vorhandene Mechanismen

- **Timeout**: bei `TrainingBegonnen` eine **`Frist`** planen (`IDbClock` / `FristScheduler`, in
  `Host.Grpc` über `AddDeadlines` verdrahtet). `FristFaellig` vor Abschluss →
  `MarkiereAlsHängengeblieben`. Kein Polling von Hand.
- **Exactly-once / Idempotenz**: Empfänger-Dedup über `ReaktionId`/`CausationId` → ein doppelt
  zugestelltes `TrainingAngefordert` startet **kein** zweites Training.
- **Abbruch**: `BricheTrainingAb` (GUI) → `TrainingAbgebrochen`; der Worker prüft ein Abbruch-Flag
  zwischen Epochen (kooperativer Abbruch).

### 4.4 Commands / Events

| Command | Quelle | Event |
|---|---|---|
| `StarteTraining(id, datensatzId, version, hyperparameter)` `: ICreationCommand` | GUI | `TrainingAngefordert(datensatzId, version, hyperparameter)` |
| `MeldeTrainingBegonnen(id)` | Python | `TrainingBegonnen` |
| `MeldeFortschritt(id, epoche, metriken)` | Python (×N) | `TrainingFortschritt` |
| `MeldeTrainingAbgeschlossen(id, modellPfad, endmetriken)` | Python | `TrainingAbgeschlossen` |
| `MeldeTrainingGescheitert(id, grund)` | Python | `TrainingGescheitert` |
| `BricheTrainingAb(id)` | GUI | `TrainingAbgebrochen` |
| `MarkiereAlsHängengeblieben(id)` | Frist | `TrainingHängengeblieben` |

### 4.5 Read-Model (`TrainingslaufReadModel`, Schema `rm`)

`DatensatzId`+`Version`, `Status`, `AktuelleEpoche`/`GesamtEpochen`, `MetrikHistorie[]` (Live-Kurve),
`Hyperparameter`, `ModellPfad`, `Endmetriken`, `Startzeit`/`Dauer`.

---

## 5. Aggregat `Modell` (Phase 2)

| Command | Event | Wirkung |
|---|---|---|
| `RegistriereModell(id, trainingslaufId, pfad, metriken)` `: ICreationCommand` | `ModellRegistriert` | Artefakt wird erstklassig |
| `SetzeAktiv(id)` | `ModellAktiviert` | genau ein aktives Modell je Zweck |
| `Archiviere(id)` | `ModellArchiviert` | |

**Kreis geschlossen:** der bestehende **Inferenz-Worker** (`classifier.py`) subscribed
`ModellAktiviert`, lädt das neue Modell (TorchScript, wie heute) und klassifiziert damit weiter.
Kette **Datensatz → Training → Modell → Inferenz** — vollständig event-sourced und auditierbar.

**Optionale Saga (Prozess-Maschine, §04):** die Training→Modell-Kante deklarativ verbinden:

```csharp
Prozess<TrainingAbgeschlossen>.Definiere(p =>
    p.Auf<TrainingAbgeschlossen>().Sende<RegistriereModell>(e =>
        new RegistriereModell(NeueId(), e.TrainingslaufId, e.ModellPfad, e.Endmetriken)));
```

Das **Modell-Artefakt selbst** (`model.pt`, `metrics.json`) ist eine **Datei** — Python schreibt
sie, das Event trägt nur den **Pfad**. (Ein Modell *ist* eine Binärdatei; hier ist eine Datei
richtig — anders als beim Datensatz, siehe §6.)

---

## 6. Der C#↔Python-Schnitt — **ein Query-Kanal, kein File**

> **Kernentscheidung (überarbeitet).** Der Datensatz wird **nicht** als `manifest.json` auf Platte
> materialisiert und Python liest **nicht** direkt Postgres. Stattdessen liefert C# die Samples
> **dynamisch über denselben typisierten Query-Kanal, den auch das Blazor-Frontend nutzt.**

### 6.1 Warum nicht Datei, warum nicht Python→Postgres

| Ansatz | Problem |
|---|---|
| **`manifest.json` (Datei)** | statische Datei für dynamische Daten; ein Artefakt mehr zu verwalten; kann veralten. |
| **Python → Postgres direkt** | koppelt Python an die **physische Marten-Form** (`rm.mt_doc_…`, JSONB) → Schema-Änderung bricht Python **still**; braucht DB-Credentials/-Netz auf der GPU-Box; hebelt „C# besitzt den Store, Python ist Client" aus (dieselbe Linie, aus der `InMemoryEventStore` verboten ist). |
| **✅ served Query (C# löst auf)** | dein „Postgres-Call" — nur in einen **typisierten Query** gewickelt. Keine Datei, immer aktuell, Python bleibt reiner Client, C# behält Schema-Hoheit, reproduzierbar (liest den **eingefrorenen** Snapshot). |

### 6.2 Der Fluss

```
Python  ──await self.query(HoleDatensatzSamples(id, version, seite))──►  Host (gRPC Query-Kanal)
                                                                          │ liest rm.DatensatzSampleReadModel
Python  ◄──DatensatzSamples(samples[], seiteInfo)──────────────────────────┘   (eingefrorener Snapshot)
Python  ──GET /api/files/…──►  Host        (Pixel je Bild, HTTP — wie die Inferenz heute)
```

- **Sample-Liste**: neue `HoleDatensatzSamples(datensatzId, version, seite) : IQuery` → Antwort
  `DatensatzSamples`. **Paginiert** (wie `SucheImagePairs` mit `Seite`/`SeitenGroesse`) — Python
  blättert bei großen Mengen durch. Denselben Kanal nutzt die GUI (z. B. Vorschau).
- **Pixel**: unverändert über den bestehenden Datei-Endpunkt `/api/files/…`
  (`LocalFilePathResolver`) — Events/Queries tragen **Pfade, keine Blobs**.
- **Reproduzierbarkeit**: die Query liest die **eingefrorene** `DatensatzSampleReadModel` (aus
  `DatensatzEingefroren`) — also den immutablen Snapshot, nicht die Live-Daten.

**Ein Mechanismus für beide Clients.** Kein bespoke REST-Endpunkt, keine zweite Datenzugriffsform.
Eine neue Datensatz-Query wird einmal in C# als `IQuery` deklariert, per Proto-Regen sehen sie
**beide** Clients (§7).

> **Voraussetzung** dafür: der Python-Client muss Queries **stellen** können — Analyse in §7.

---

## 7. Query-Parität Python ↔ Blazor (Analyse)

**Frage:** Hat der Python-Client dieselben Query-Möglichkeiten wie das Blazor-Frontend?
**Kurzantwort:** im Prinzip ja (gleiche Query-Menge, gleicher Server, Transport vorhanden) — aber
die **Ask-Seite ist ergonomisch nicht ausgebaut**. Es fehlt eine öffentliche `query()`-Methode +
typisiertes Response-Unpacking. Kleine, gekapselte Lücke — **keine** Server-, Proto- oder
Capabilities-Änderung.

### 7.1 Was schon gleich ist

| Baustein | Zustand | Beleg |
|---|---|---|
| **Query-Menge** | *eine* Quelle → beide Clients | `: IQuery` in `Domain.Projections` → `domain.proto`. `SucheImagePairs(ImagePairFilter)`, `GetImagePair`, `GetImagePairStatistik`, `GetProduktionsTage`, `GetVerlauf`, `GetImagePairHistorie`, … Neue Query + Proto-Regen → Python sieht die DTOs automatisch. |
| **Filtertyp** | identisch | `ImagePairFilter` ist shared DTO → Python baut exakt dieselben Filter. |
| **Server** | client-agnostisch | gleiche `QueryRequest`/`QueryResponse`-Envelope über denselben bidi-Stream — keine Server-Änderung, damit Python fragen darf. |
| **Transport** | vorhanden | `proxy.send_query` (`Client.Infrastructure.Python/cqrs_client/proxy.py:232`) = Spiegel von C# `GrpcProxy.QueryAsync`; Korrelation via `Future`, 30 s Timeout. |
| **Registry** | vorhanden | klassifiziert `QUERY` / `QUERY_RESPONSE` (`registry.py`). |
| **Capabilities** | frei | *Fragen* braucht **keine** Deklaration (nur *Beantworten* würde) — `_build_capabilities_request` deklariert nur Events/Commands/handle-Queries. |

### 7.2 Die Lücke (Python-only, Ergonomie)

1. **Keine öffentliche `query()`** auf `CqrsClient` — `send_query` liegt am privaten `_proxy`;
   `client.py` bietet nur `state`/`session_id`/`is_connected`/`run`.
2. **Kein typisiertes Response-Unpacking** auf der Ask-Seite: `send_query` liefert die **rohe**
   `QueryResponse`. Der Mapper hat `wrap_query_response` (Antworten) und `extract_query_payload`
   (Query beim Antworten), aber **kein** `extract_query_response`. Der generische
   `extract_oneof_payload` existiert bereits — muss nur verdrahtet werden.
3. **Kein Fehler-Mapping**: C# `QueryBridge` emittiert `QueryFailed`; Python müsste ein
   Server-`error` auf einer Query-Korrelation in eine Exception übersetzen.
4. **Kein Deps/Versions-Tracking** aus Responses (C# `QueryBridge.TrackFromDeps` → OCC). Für
   **read-only** Datensatz-/Trainings-Queries **irrelevant** — Python schreibt nichts
   versionsabhängig.

### 7.3 Die Ergänzung (Skizze, nicht implementiert)

Spiegelbildlich zum vorhandenen `send_command`-Pfad — ~1 kleines Modul:

```python
# cqrs_client/client.py — fehlende öffentliche API
async def query(self, query_dto) -> betterproto.Message:
    raw = await self._proxy.send_query(query_dto)          # existiert
    return self._mapper.extract_query_response(raw)        # neu: oneof → typisierte Antwort

# Nutzung im TrainingWorker:
#   samples = await self.query(HoleDatensatzSamplesDto(datensatz_id=…, version=1, seite=1))
```

### 7.4 Struktureller Unterschied — Absicht, kein Defekt

- **Blazor** ist reaktiv/bus-integriert: `bus.Publish(query)` → Antwort als Bus-Event →
  `Store.Handle` → Hydration.
- **Python** wäre imperativ/awaitable: `treffer = await self.query(...)`. Für einen Worker die
  **bessere** Form (linearer Code). → *Fähigkeits*-Parität ja, *Stil* bewusst anders.

> ⚠️ **Nicht verwechseln** mit der bekannten Schuld (Doku §9.7): „Python kann Queries nicht
> *beantworten*" (`router._handle_query_forward` TODO) — das ist die **andere** Richtung (Python
> als Responder). Wir brauchen Python als **Frager**; das ist die kleinere, hier beschriebene
> Lücke, unabhängig davon.

### 7.5 Caveat — große Mengen

Query→Response ist **eine** Antwort. Große Datensätze → **paginieren** (`Seite`/`SeitenGroesse`,
wie `SucheImagePairs`). Für sehr große Datensätze wäre echtes **Server-Streaming** eine Option —
das liegt außerhalb des aktuellen Query-Vertrags und wäre eine separate Entscheidung.

---

## 8. GUI (Blazor — nur neue Slot-Module, kein neuer Unterbau)

### 8.1 Neue Main-View: „Datensatz-Komposition" (`IStageModule`)

Transfer-/Korb-Layout — Suchergebnis links, Datensatz-Korb rechts:

```
┌── Suchergebnis (Kandidaten) ──────────┐   ┌── Datensatz „Charge-Juni" (Entwurf) ──┐
│ Filter: Juni 2025 · Produktlabel=Anom │   │  4 812 Paare · 3 Ranges · noch offen  │
│ ▣ 12.06 09:11  Anomalie                │   │  Klassenbalance ▐OK 60▐Frag 25▐Anom15 │
│ ▣ 11.06 08:10  Anomalie          [→]   │   │  Split  train 70 / val 15 / test 15   │
│                     gesamt 1 240 Tr.   │   │  Provenienz:                          │
│  [ganze Range → Datensatz]             │   │   • Juni·Anomalie 1 240               │
│                                        │   │   • Juli·OK       3 100  [entfernen]  │
│                                        │   │  [Einfrieren]                         │
└────────────────────────────────────────┘   └────────────────────────────────────────┘
```

- Der **Filter links** ist der vorhandene `SuchKriterien`-Baustein inkl. **Produktionstage-Baum**
  (die natürliche Zeit-Range bei kontinuierlicher Sammlung).
- „**ganze Range → Datensatz**" fügt das *komplette* Suchergebnis hinzu (Union, Dedup).
- Rechts live: **Größe, Klassenbalance, Split, Provenienz**.

### 8.2 Weitere Module

| Slot | Modul | Inhalt |
|---|---|---|
| `IStageModule` | **Training-Dashboard** | „Neues Training" (Datensatz + Hyperparameter) + **Live-Kurven** (loss/accuracy) je Lauf über ApexCharts, gespeist vom Event-Stream |
| `IStageModule` *(Ph.2)* | **Modelle** | registrierte Modelle, Metrik-Vergleich, „aktiv setzen" |
| `ISidebarModule` | **Datensatz-Liste** | Entwurf/Eingefroren, Größe, Version |
| `ISidebarModule` | **Trainingslauf-Liste** | Status-Badges live |
| `IHeadlessModule` | **Data/Refresh/Intent** | Stores + RefreshHandler für die neuen Read-Models, IntentHandler für „Starte Training"/„Friere ein" |

Alles über die **generierte Verdrahtung** (`AddClientDomain_…` / `AddModules_…`) — keine
Handverdrahtung.

---

## 9. Nutzer-Flow (so einfach wie möglich)

```
1. Suchen        (Zeit-Range im Produktionstage-Baum + Filter)
2. „ganze Range → Datensatz"      ← beliebig oft, verschiedene Suchen
3. Balance/Größe anschauen
4. „Einfrieren"                    ← Split automatisch, Samples werden queryierbar
5. „Training starten"  (Datensatz wählen, Hyperparameter)
6. Live-Kurve zusehen  ← Fortschritt streamt event-sourced ins Dashboard
        └─► fertiges Modell (Phase 2: „aktiv setzen" → Inferenz nutzt es)
```

Drei Klicks bis zum trainierbaren Datensatz; alles Technische bleibt unsichtbar — Invariante 5.

---

## 10. Reproduzierbarkeit & Provenienz (die ES-Stärke)

- **Eingefrorener Datensatz** = unveränderlicher Input → zwei Trainings sind vergleichbar.
- **`TrainingAngefordert`** hält `DatensatzId`+`Version`, Hyperparameter, Seed fest.
- **Provenienz je Range** (welcher Filter, wann, wie viele) → nachvollziehbar, *woraus* ein Modell
  gelernt hat. Provenienz lebt im **Log** (queryable), nicht in einer Datei neben dem Modell.
- Voller Audit-Trail: **Suche-Ranges → Datensatz v1 → Trainingslauf (+ Seed) → Modell + Metriken →
  aktive Inferenz.**

---

## 11. Was zu bauen wäre (Aufwandsskizze — bewusst noch nicht implementiert)

| Baustein | Ort | Anmerkung |
|---|---|---|
| Aggregate `Datensatz`, `Trainingslauf` (+ `Modell` Ph.2) | `Domain/Datensatz/`, `Domain/Trainingslauf/` | Commands/Events/Decider/Applier/State — wie `Domain/ImagePair` |
| **Proto-DTOs** je neuem Command/Event/**Query** | `Proto.SourceGeneration` → `ProtoRepo` | Pflicht, sonst bricht `DtoMapperGenerator` (CLAUDE.md) |
| Projektionen + Co-Commit-Stores (`DatensatzReadModel`, **`DatensatzSampleReadModel`**, `TrainingslaufReadModel`) | `Domain.Projections`, `Domain.Infrastructure` (Schema `rm`) | wie `ImagePairProjection`/`ImagePairStore` |
| Query `HoleDatensatzSamples` + Reader | `Domain.Projections` | paginiert; liest den eingefrorenen Snapshot |
| **Server-Resolver** (Range-Auflösung, Freeze-Snapshot) | `Domain.Pipeline`/`Infrastructure` | injizierter `IImagePairReadStore`, yieldet Commands |
| **Python: öffentliche `query()` + `extract_query_response`** | `Client.Infrastructure.Python/cqrs_client` | §7 — klein, spiegelt `send_command` |
| **Python `TrainingWorker`** | neben `Domain.Client.Worker.Python.ML` | `CqrsClient`, Event→Fortschritts-Commands, `to_thread` + Queue, Samples per `query()` |
| Blazor-Module (Komposition-Stage, Dashboard, Sidebars, Headless) | `Domain.Client.Modules.Blazor` | generierte Verdrahtung |
| Frist-Verdrahtung (Timeout) | `Host.Grpc` | analog zum bestehenden `AddDeadlines` |

**Kein** `manifest.json`-Materializer, **kein** REST-Endpunkt, **kein** direkter DB-Zugriff aus
Python (siehe §6).

---

## 12. Offene Punkte / spätere Ausbaustufen

- **Hyperparameter-Sweeps**: mehrere `Trainingslauf` unter einer Klammer (Phase 3) — eine dünne
  Fan-out-Saga (`SendeJe`, §04).
- **Datensatz-Diff/Vererbung**: „v2 = v1 + Range X" (Delta-Datensätze) — das Range-Union-Modell
  macht es natürlich.
- **Drift-getriggertes Retraining**: automatisch neuen Zeitschnitt + Training anstoßen, wenn genug
  neue gelabelte Bilder da sind (Timer-Trigger, §04) — Phase ≥ 2.
- **Server-Streaming** für sehr große Datensätze statt Paginierung (§7.5) — bei Bedarf.
