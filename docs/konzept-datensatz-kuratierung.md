# Konzept — Datensatz als kuratierter Tag (dedizierte Auswahl beim Betrachten)

> **Stand: 2026-08-18. Status: KONZEPT — nicht implementiert. Präsentation, kein Bauauftrag.**
> Neuausrichtung der Datensatz-Erzeugung: weg vom „Korb, den man aus Filter-Ranges füllt",
> hin zu **Datensatz = Tag, den man beim Betrachten der Bilder vergibt** — gekoppelt an die
> Galerie (Phase 1, gebaut) und die Einbild-Anzeige, über eine **dedizierte Auswahl**.
>
> Baut auf und **verschiebt den Schwerpunkt** von
> [konzept-galerie-datensatz-komposition.md](konzept-galerie-datensatz-komposition.md): dort war
> die Transfer-/Korb-Bühne die Mitte; hier wird der Datensatz zu einer **Eigenschaft der Bilder**,
> die man kuratierend beim Durchsehen setzt. Die Lifecycle-Teile (Einfrieren, Split, Training)
> bleiben unverändert.

---

## 1. Warum das jetzige Modell „Mist" ist

Heute komponiert man einen Datensatz über den **Suchfilter**: Kriterien setzen → „ganze Range →
Datensatz". Man **sieht die Bilder dabei nicht**, denkt in Filtern statt in Bildern, und die
manuelle Feinauswahl (`NimmPaarAuf`/`EntfernePaar`) existiert im Aggregat, hat aber **keinen
Bedienweg**. Kuratieren heißt aber: **hinschauen und gezielt entscheiden**. Genau das fehlt.

Zwei Kern-Defizite:
- **Indirekt statt visuell.** Der Korb füllt sich aus einer Filtermenge, nicht aus dem, was du
  gerade ansiehst und für gut befindest.
- **Kein dediziertes Auswählen.** Es gibt keine „ich nehme *dieses* Bild, und dieses, dieses
  nicht"-Geste — obwohl das die eigentliche Kuratier-Handlung ist.

---

## 2. Leitidee

**Ein Datensatz ist ein Tag.** Während du Bilder durchsiehst (Galerie oder Einbild), vergibst du
mit einer Geste den Datensatz-Tag — genau wie du mit `R/Q/F` labelst. Die Mitgliedschaft ist
**überall als Tag sichtbar** (Galerie-Kachel, Einbild-Detail), ein Bild kann in **mehreren**
Datensätzen liegen, und die eigentliche Auswahl ist **dediziert**: per Bild oder als bewusste
Mehrfach-Auswahl.

> In einem Satz: *Du schaust Bilder an und taggst die guten in deinen Datensatz — der Datensatz
> ist keine Filtermenge mehr, sondern eine kuratierte Sammlung, die als Tag an den Bildern hängt.*

Der Datensatz wird damit eine **Dimension neben dem Label**: Label = *„was ist das Bild"* (OK/
Fraglich/Anomalie), Datensatz-Tag = *„wofür sammle ich es"* (Charge-Juni, Q3-Anomalien, …).

---

## 3. Vom Korb zum Tag — das mentale Modell

| | Alt (Korb) | Neu (Tag) |
|---|---|---|
| Was ist ein Datensatz | eine Filtermenge, in einen Korb kopiert | eine **kuratierte Sammlung**, als Tag an Bildern |
| Wie füllt man ihn | Filter setzen → „ganze Range rein" | Bilder ansehen → **taggen** (per Bild / Mehrfach) |
| Sieht man die Bilder | nein (nur Trefferzahl) | **ja** — man kuratiert im Betrachten |
| Mitgliedschaft sichtbar | nur im Korb-Panel | **überall** (Galerie-Badge, Einbild-Tag) |
| Mehrfach-Zugehörigkeit | umständlich | natürlich (Tags sind mehrfach) |
| Feinauswahl | Domain kann es, GUI nicht | **primärer Weg** (dediziert) |

Range-Aufnahme verschwindet nicht — sie wird zum **optionalen Bulk-Saat** („nimm alle Juni-
Anomalien als Startmenge auf"), von dem aus man dann **dediziert aussortiert/ergänzt**. Primär ist
die dedizierte Auswahl.

---

## 4. Die drei Säulen

### 4.1 Aktives Sammel-Ziel (ein Datensatz als Auswahl-Ziel)
Du **aktivierst** einen Datensatz (oder legst on-the-fly einen neuen an). Er wird zum
**Sammel-Ziel** — persistent sichtbar in einer schmalen Leiste:

```
┌───────────────────────────────────────────────────────────────┐
│  Sammle in: ▸ Charge-Juni (Entwurf) · 128 Paare  [wechseln ▾]  │
│  Balance ▐OK 60▐Frag 25▐Anom 15   [Einfrieren]                  │
└───────────────────────────────────────────────────────────────┘
```

Solange ein Sammel-Ziel aktiv ist, kann jedes betrachtete Bild mit **einer** Geste hinein/heraus.

### 4.2 Taggen beim Betrachten (Einbild + Galerie)
- **Einbild-Anzeige** (Bilder-Bühne): unter/neben den Label-Buttons ein **Datensatz-Tag-Toggle**:
  `[ + Charge-Juni ]` bzw. wenn schon drin `[ ✓ Charge-Juni ]`. Tastenkürzel (z. B. `A` =
  „aufnehmen") togglet die Mitgliedschaft des aktuellen Paars im Sammel-Ziel. Gleiche Muskel-
  gedächtnis-Logik wie Labeln: **du labelst UND sammelst im selben Durchgang**.
- **Galerie**: jede Kachel trägt ein **Mitgliedschafts-Badge** (● im aktiven Datensatz). Klick/
  Taste auf der Kachel togglet; **Mehrfach-Auswahl** (Rubber-Band / Shift-Klick) taggt viele auf
  einmal.

### 4.3 Mitgliedschaft als Tag überall (auch rückwärts)
Beim Betrachten eines Bildes siehst du **alle** Datensätze, in denen es liegt — als Chips:
```
in Datensätzen:  [Charge-Juni ·Entwurf]  [Q3-Anomalien ·v2]
```
So ist die Datensatz-Zugehörigkeit eine **sichtbare Eigenschaft des Bildes**, nicht in einem
separaten Korb versteckt. In der Galerie gibt es die Klemmen „nur im aktiven Datensatz" / „nur
noch nicht drin" zum fokussierten Kuratieren.

---

## 5. Die dedizierte Auswahl

Das ist der Kern der Anfrage — bewusstes, visuelles Auswählen statt Filter-Ableitung:

- **Per-Bild-Toggle**: Taste/Klick nimmt genau das betrachtete Paar auf/heraus.
- **Mehrfach-Auswahl (Markierung)**: in der Galerie einen Satz Kacheln aufziehen (Rubber-Band)
  oder mit Shift/Strg picken → als **Batch** ins Sammel-Ziel (oder heraus). Die Markierung ist ein
  eigener, sichtbarer Zustand (Häkchen auf den Kacheln), unabhängig von der Mitgliedschaft.
- **Bulk-Saat (optional)**: „ganze gefilterte Menge aufnehmen" bleibt als Startpunkt, danach
  kuratiert man dediziert nach. Nicht mehr der Hauptweg.

Damit deckt die dedizierte Auswahl beide Maßstäbe ab: **ein Bild** (Präzision) und **eine bewusst
gezogene Menge** (Tempo) — ohne dass man je in Filter denken *muss*.

---

## 6. Kopplung mit Galerie + Einbild

Alles reitet auf dem **geteilten Cursor** (schon gebaut) plus zwei neuen, rein clientseitigen
Notionen im geteilten `Store` (wie `AktiveBereiche` heute):

```csharp
/// <summary>Das aktive Sammel-Ziel — der Datensatz, in den getaggt wird.</summary>
public DatensatzKontext? SammelZiel { get; private set; }   // Id, Status, MitgliederIds

/// <summary>Dedizierte Mehrfach-Auswahl in der Galerie (Kachel-Häkchen).</summary>
public IReadOnlySet<Guid> Markierung { get; private set; }
```

- **Galerie → Einbild**: Cursor (vorhanden). Das betrachtete Bild ist immer das Tag-Ziel der
  Per-Bild-Geste.
- **Sammel-Ziel → alle Flächen**: `SammelZiel.MitgliederIds` speist die Badges/Tags (O(1)-
  Enthaltensein je Kachel/Detail).
- **Tag-Geste → Datensatz**: dispatcht `NimmPaarAuf`/`EntfernePaar` (einzeln) bzw. die Batch-
  Variante (Mehrfach-Auswahl).

Kein neuer Transport, kein neuer Konsumenten-Typ — dasselbe View→Intent→Command-Muster wie das
Labeln.

---

## 7. Domänen-Abbildung (was schon da ist, was fehlt)

| Baustein | Status | Rolle im Konzept |
|---|---|---|
| `NimmPaarAuf(id, imagePairId)` / `EntfernePaar(id, imagePairId)` | **existiert** ([Commands.cs](../Domain/Datensatz/Commands.cs)) | endlich der GUI-Weg der Tag-Geste (einzeln) |
| Batch aufnehmen | offen | Mehrfach-Auswahl → `NimmRangeAuf(ids, „manuelle Auswahl")` wiederverwenden (Session-Entscheidung) |
| `DatensatzKontext` (SammelZiel) + `Markierung` | offen | client-seitige Notionen im geteilten Store |
| `HoleDatensatz` + `Mitglieder[]` | Erweiterung | Entwurfs-Mitgliedschaft für die Badges (ohne Sample-Seite) |
| Reverse-Mitgliedschaft: `HoleDatensaetzeFuerPaar(imagePairId)` + Rückwärts-Index | offen | die „in Datensätzen: …"-Tags im Einbild |
| Lifecycle: `FriereEin` → Snapshot + Split + Version | **unverändert** | Entwurf (taggbar) → Eingefroren (immutabel) → Training |

Die Wahrheit bleibt im Aggregat/Log; die Tags sind **Sicht** (Read-Model-Join), kein neuer
Domänenzustand — Invariante 5 bleibt gewahrt.

---

## 8. Wechselwirkungen (aus dem Vorgänger-Konzept, verfeinert)

- **Sammel-Ziel → Galerie**: Mitglieder hervorheben; Modus „nur Mitglieder" / „nur Nicht-Mitglieder".
- **Balance → Auswahl**: „Anomalie fehlt" → ein Klick filtert auf Nicht-Mitglieder der fehlenden
  Klasse, die du dann gezielt taggst → Balance steigt sichtbar.
- **Datensatz → Filter** (optional): einen range-gesäten Datensatz öffnen → Filter springt auf
  seine Provenienz; ein rein getaggter Datensatz hat keine Provenienz-Range, nur Mitglieder.
- **Label-Änderung → Balance**: neu labeln eines Draft-Mitglieds verschiebt live die Balance des
  Sammel-Ziels.

---

## 9. Was das ggü. dem alten Konzept ändert

- **Die Transfer-/Korb-Bühne verliert die Hauptrolle.** Sie bleibt bestenfalls als **Verwalten-
  Sicht** („alle Mitglieder sehen, aussortieren, einfrieren") — nicht mehr der Kompositions-Ort.
- **Dedizierte Auswahl (Tag beim Betrachten) wird der primäre Kompositionsweg.**
- **Das aktive Sammel-Ziel** wird ein persistenter, erststelliger Modus (wie der „Aufnahme"-Knopf
  einer Kamera).
- **Der Datensatz wird ein Tag** — überall an den Bildern sichtbar, mehrfach, rückwärts abfragbar.

Kurz: das alte Konzept hatte die Bausteine (Overlay, Mehrfach-Auswahl, Reverse-Tag), aber die
falsche Mitte. Diese Neuausrichtung dreht die Mitte auf **Kuratieren beim Betrachten**.

---

## 10. Nutzer-Flows

**A — Kuratieren im Durchsehen.**
`Sammel-Ziel „Charge-Juni" aktivieren → Galerie/Einbild durchgehen → gute Bilder mit A/Klick
taggen (Badge ● erscheint sofort) → Balance-Leiste zieht mit → Einfrieren.`

**B — Bewusste Mehrfach-Auswahl.**
`In der Galerie eine Reihe auffälliger Kacheln aufziehen → „Auswahl aufnehmen" → alle bekommen
das Badge.`

**C — Balance gezielt schließen.**
`Sammel-Ziel zeigt „Anomalie 8 %" → Klick auf den Anomalie-Balken → Galerie zeigt nur fehlende
Anomalie-Kandidaten (Nicht-Mitglieder) → taggen → Balance steigt.`

**D — Herkunft eines Bildes.**
`Bild ansehen → Chips „in: Charge-Juni, Q3-Anomalien v2" → per Klick zum jeweiligen Datensatz
springen / als Sammel-Ziel aktivieren.`

---

## 11. Entscheidungen & Restfragen

**Getroffen (2026-08-18):**
- ✅ **Genau ein aktives Sammel-Ziel.** Wechsel per Dropdown; die Aufnahme-Geste ist damit
  eindeutig (kein Ziel-Picker nötig).
- ✅ **Korb-Bühne bleibt als „Verwalten"-Sicht** (Mitglieder-Liste, aussortieren, Provenienz,
  Einfrieren) — NICHT mehr der Kompositions-Ort. Kuratiert wird in Galerie/Einbild.
- ✅ **Reichweite: Kern + Reverse-Tags.** Also Sammel-Ziel-Leiste + Tag beim Betrachten
  (Einbild-Toggle + Galerie-Badges/Toggle) + dedizierte Mehrfach-Auswahl **und** die
  „in Datensätzen: …"-Chips im Einbild → braucht den **Rückwärts-Index serverseitig**
  (`HoleDatensaetzeFuerPaar` + Read-Model).

**Noch offen (klein, beim Handoff/Bau zu klären):**
- **Tastenkürzel fürs Aufnehmen:** `A` (aufnehmen) vorgeschlagen; `+` denkbar. (Leertaste ist
  belegt = „nächstes offenes".)
- **Bulk-Saat (Range-Aufnahme):** als optionaler Startpunkt behalten (Empfehlung ja).
- **Mehrfach-Auswahl-Command:** `NimmRangeAuf(ids, „manuelle Auswahl")` wiederverwenden (frühere
  Session-Entscheidung) statt neues Batch-Command.

> Entscheidungen eingearbeitet. Nächster Schritt auf Zuruf: ein konkreter Umsetzungs-Handoff in
> Phasen (wie M9/Galerie). Dieses Dokument bleibt der Soll-Entwurf — noch nicht gebaut.
