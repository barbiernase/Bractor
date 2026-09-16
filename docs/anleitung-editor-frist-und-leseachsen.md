# Anleitung — Frist & Leseseiten-Achsen im Domänen-Editor

Praktische Bedienung zweier Editor-Bereiche (`GraphExtractor/HtmlPresenter.cs`, SimHost `/editor`):
die **Frist/Deadline** (über den Trigger-/Pipeline-Knoten) und die **zwei Entwurfs-Achsen der
Leseseite** (am Projektion-Knoten). Verwandt: [anleitung-prozess-schreiben.md](anleitung-prozess-schreiben.md),
[konzept-exactly-once-naht.md](konzept-exactly-once-naht.md).

> **Verbinden allgemein:** von einem **Ausgang** (rechts am Knoten, farbiger Punkt) auf einen
> **Eingang** (links) ziehen. Beim Ziehen ringelt der Editor alle gültigen Ziele **grün** — nur
> typgleiche Ports rasten ein. Ports: `+ Trigger` / `+ Pipeline` / `+ Projektion` legen die Knoten an
> (Leiste oben oder Doppelklick auf die Fläche).

## A · Frist (Deadline)

Eine Frist ist im Editor **kein eigener Knoten**, sondern entsteht auf einem von zwei Wegen. Weg 2
ist der idiomatische für „ein Schritt muss binnen X fertig sein" (so macht es `TrainingFristPipeline`).

### Weg 1 — externe Frist als Trigger-Knoten
Für „ein Wecker von außen läuft ab → reagiere".

1. **`+ Trigger`** anlegen.
2. Modus-Dropdown auf **„⏳ Frist (Deadline)"** stellen.
3. **„Dauer / Fälligkeit (IDbClock)"** ausfüllen — z. B. `24:00:00` oder ein Fälligkeitsfeld.
4. Unter **„Trigger-Nachricht (IPipelineTrigger)"** den Namen tippen (z. B. `TrainingFristAbgelaufen`)
   und mit **`+ Feld`** die Nutzlast (`TrainingId : Guid`).
5. Vom Port **„erzeugt … ▶"** (rechts) auf **„+ Trigger andocken"** einer **Pipeline** ziehen.
6. In der Pipeline reagiert der Handle **„◀ Trigger TrainingFristAbgelaufen"**; von **„+ Command ▶"**
   auf den Ziel-Command ziehen (z. B. `BrecheTrainingAb`).

### Weg 2 — Pipeline-interner Timeout (ScheduleSelf) — idiomatisch
Für „ich starte beim Eintritt eine innere Stoppuhr, die zurück in dieselbe Pipeline fällt".

1. In einer **Pipeline** an einem bestehenden Handle (z. B. „◀ Auf TrainingBegonnen") beim
   **„+ plant Self-Tick ↺"** auf **＋** klicken.
2. Das legt eine Zeile **`Tick · 30s · ↺`** an (Name + Delay editierbar) **und** automatisch einen
   neuen Handle **„◀ Self Tick"** — verbunden per gestricheltem Self-Loop.
3. Delay auf die Frist setzen (z. B. `24:00:00`), Namen sprechend machen (z. B. `FristAbgelaufen`).
4. Am **„◀ Self FristAbgelaufen"**-Handle den Timeout-Command verdrahten
   (**„+ Command ▶"** → z. B. `BrecheTrainingAb`).
5. Der Rumpf (📝/🤖 am „Pipeline-Logik"-Port) prüft „ist noch offen?" und feuert nur dann.

**Merke:** Weg 1 = externer Wecker (`IPipelineTrigger`). Weg 2 = interne Stoppuhr
(`ctx.ScheduleSelf` → `IPipelineSelfMessage`), fällt in dieselbe Pipeline zurück.

## B · Leseseiten-Achsen (am Projektion-Knoten)

Der **`+ Projektion`**-Knoten trägt oben zwei Häkchen — die zwei **orthogonalen**
Entwurfsentscheidungen. Ein Klartext-Hinweis darunter spiegelt die gewählte Kombination.

### Achse 1 — Transport: „Geordneter Pull (IPullSubscriber)"

| Häkchen | Bedeutung | Wähle es, wenn … |
|---|---|---|
| **☑ an** (Default) | **Pull** — jedes Event **genau einmal, in Reihenfolge** | dein Read-Model Vollständigkeit/Ordnung braucht (Normalfall) |
| **☐ aus** | **Signal** (`ISubscriber`) — schnell, best-effort, darf verloren/doppelt/ungeordnet sein | reines UI-Feedback/Ticks, wo ein verlorenes Update egal ist |

### Achse 2 — Garantie: „Append-artig ⇒ Exactly-once (IAppendProjektion)"

Entscheidend ist, **was deine Store-Write-Funktion tut**:

| Häkchen | Effekt-Art | Folge |
|---|---|---|
| **☐ aus** (Default) | **idempotenter Upsert** (last-writer-wins auf eine Id) | Doppelverarbeitung harmlos → **at-least-once genügt** |
| **☑ an** | **Append-artig** (Ledger/Historie, jedes Event = neue Zeile) | Doppelverarbeitung **korrumpiert** → verlangt **Co-Commit-Store → exactly-once** (Boot-Check GA-1) |

**Faustregel:** Häkchen **an**, sobald eine Write-Fn *anhängt* (in eine Liste/Historie schreibt); bei
*Upsert* (auf eine Id setzen) aus lassen. Der Editor-Store (`MartenCoCommitStoreBase`) liefert das
Co-Commit ohnehin — das Häkchen macht die Anforderung explizit und schaltet die GA-1-Prüfung scharf.

> **Warum getrennt:** Transport (Pull/Signal) und Garantie (Append/Upsert) sind unabhängig — jede
> Kombination ist gültig. Der Hinweistext unter den Häkchen zeigt die resultierende Semantik.
