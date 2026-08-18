# Konzept — Galerie, Einbild-Anzeige & Datensatz-Komposition (verwoben)

> **Stand: 2026-08-18. Status: KONZEPT — nicht implementiert.** Entwurf einer neuen
> Kurations-Oberfläche: eine **Thumbnail-Galerie**, die **Einbild-Detail-Anzeige** und die
> **Datensatz-Erzeugung** zu *einer* zusammenhängenden Fläche verweben — mit expliziten
> **Wechselwirkungen** (Filter ↔ Datensatz ↔ Auswahl ↔ Detail), damit zusammengehörige
> Datenpunkte sichtbar miteinander assoziiert werden. Im Stil des Frameworks (sechs
> Invarianten, vier Konsumenten, generierte Verdrahtung, geteilter Client-Store als eine
> Quelle der Wahrheit).
>
> Vorgelagerter Fix (bereits umgesetzt, Commit-Kandidat): der **Baum-Range-Bug** — „ganze
> Range → Datensatz" nahm eine unbegrenzte Range auf, obwohl im Produktionstage-Baum nur
> bestimmte Tage gewählt waren. Behoben in
> [`DatensatzKompositionIntentHandler`](../Domain.Client.Modules.Blazor/DatensatzKomposition/DatensatzKompositionIntentHandler.cs):
> bei aktiver Baum-Auswahl wird **je Bereich eine `FuegeRangeHinzu`** gesendet — deckungsgleich
> mit der Kandidaten-Anzeige. Dieses Konzept baut auf dem korrigierten Verhalten auf.
>
> Verwandt: [konzept-training-und-datensatz.md](konzept-training-und-datensatz.md) (Datensatz-/
> Trainings-Aggregate), [08-frontend-blazor-client.md](08-frontend-blazor-client.md).

---

## Inhalt

1. Warum überhaupt — die Reibung von heute
2. Leitidee
3. Die drei Flächen und ihr gemeinsamer Zustand
4. Der geteilte Auswahl-/Assoziations-Kontext (das Herzstück)
5. Die Wechselwirkungen (Assoziations-Matrix)
6. Wie das „clever" bleibt — Mechanik ohne Invarianten-Bruch
7. Ergänzungen am Read-/Command-Modell (Skizze)
8. Thumbnails — der eine echte Perf-Punkt
9. Nutzer-Flows
10. Bauplan & Phasen (Abgrenzung)
11. Offene Entscheidungen

---

## 1. Warum überhaupt — die Reibung von heute

Der Ist-Zustand hat drei Flächen, die **nebeneinanderher** laufen statt zusammenzuspielen:

| Fläche | Heute | Datei |
|---|---|---|
| „Galerie" | eine **virtualisierte Text-Liste** (22 px-Zeilen, `PaarZeile`), Klick → Cursor | [`PaarlistenPanel.razor`](../Domain.Client.Modules.Blazor/Paarliste/PaarlistenPanel.razor) |
| Einbild-Anzeige | zeigt DC0+DC2 **des Cursor-Paars**, lazy geladen (nur das aktuelle) | [`BilderStage.razor`](../Domain.Client.Modules.Blazor/Bilder/BilderStage.razor), [`DataEffects.razor`](../Domain.Client.Modules.Blazor/Data/DataEffects.razor) |
| Datensatz-Komposition | Transfer-/Korb-Layout, nimmt **die ganze gefilterte Range** auf | [`DatensatzKompositionPanel.razor`](../Domain.Client.Modules.Blazor/DatensatzKomposition/DatensatzKompositionPanel.razor) |

Die konkreten Reibungspunkte:

1. **Man sieht die Bilder nicht, während man kuratiert.** Die Kandidatenauswahl passiert über
   Filter + Trefferzahl; die Bilder selbst sieht man erst einzeln auf einer *anderen* Bühne.
   Kuratieren heißt aber „hinschauen".
2. **Nur Range-Granularität.** Es gibt genau eine Geste: „ganze (gefilterte) Range rein". Das
   manuelle Delta (`NimmPaarAuf`/`EntfernePaar`) existiert im Aggregat, hat aber **keinen
   UI-Auslöser** — man kann kein einzelnes Paar per Klick aufnehmen oder wieder herauswerfen.
3. **Keine Mitgliedschafts-Rückmeldung.** Nichts zeigt, ob ein Paar schon im aktiven Datensatz
   liegt (oder in welchem anderen). Man kuratiert blind gegen Dubletten.
4. **Filter und Datensatz wissen nichts voneinander.** Öffne ich einen bestehenden Datensatz,
   springt der Filter *nicht* auf seine Herkunft. Habe ich einen Datensatz mit Klassen-
   Ungleichgewicht, hilft mir der Filter nicht, gezielt die fehlende Klasse nachzulegen.
5. **Der Hinweistext lügt.** Das Korb-Panel sagt „Filter im **Suche**-Panel (rechts) setzen" —
   auf dieser Bühne gibt es rechts aber den Korb, kein Suchpanel. Der Filter lebt auf einer
   anderen Bühne. Symptom der Entkopplung.

Der C-Fix hat #2 nur *halb* adressiert (Range ist jetzt korrekt), aber die eigentliche
Interaktions-Armut bleibt. Dieses Konzept adressiert #1–#5 gemeinsam.

---

## 2. Leitidee

**Eine Fläche zum Sichten und Kuratieren.** Thumbnails links, das gewählte Paar groß in der
Mitte/rechts, der aktive Datensatz als lebender Korb daneben — und **alles hängt an *einem*
geteilten Auswahl-Zustand**. Assoziationen zwischen Datenpunkten werden nicht als neuer
Domänenzustand modelliert, sondern als **Live-Overlays aus Read-Model-Joins** sichtbar gemacht:
Mitgliedschaft, Herkunft, Balance-Lücken. Der Nutzer denkt in *Bildern und Mengen*, nicht in
IDs und Filtern.

> In einem Satz: *Du browst Bilder, siehst sofort was schon drin ist, klickst rein oder raus,
> und der Filter und der Datensatz ziehen sich gegenseitig nach — Kuratieren als geschlossener
> Regelkreis statt drei getrennter Schritte.*

Konsequent zur bestehenden Architektur: **kein neuer Transport, kein neuer Konsumenten-Typ.**
Der geteilte [`Store`](../Domain.Client.Modules.Blazor/Data/Store.cs) ist schon heute die eine
Filter-/Cursor-Wahrheit; wir erweitern ihn um zwei Notionen (Markierungs-Set, aktiver
Datensatz-Kontext) und koppeln die vorhandenen Signale.

---

## 3. Die drei Flächen und ihr gemeinsamer Zustand

```
┌─ Galerie (Thumbnail-Grid) ─────────┐  ┌─ Detail ───────────┐  ┌─ Korb: aktiver Datensatz ─┐
│ ▣▣▢▣ ▢▣▣▣  (Mehrfach-Auswahl)      │  │  DC0        DC2     │  │ „Charge-Juni" · Entwurf   │
│ ▣ = markiert  ● = im Datensatz     │  │  [ groß ]  [ groß ] │  │ 366 Paare · 3 Ranges      │
│ Hover → Detail, Klick → Auswahl    │  │  Label · Historie   │  │ Balance ▐OK▐Frag▐Anom     │
│ [zur Auswahl hinzufügen] [Range +] │  │  „in: Charge-Juni"  │  │ Split 70/15/15            │
└─────────────────────────────────────┘  └────────────────────┘  │ [Einfrieren]              │
        │  ▲ Cursor koppelt beide          ▲ Reverse-Mitgliedschaft └───────────────────────────┘
        └──┴─ ein VirtualImagePairs-Fenster, ein Cursor, ein Markierungs-Set ─────────────────┘
```

- **Galerie** *ergänzt* die Text-Liste (**Entscheidung**): ein zusätzlicher Ansichtsmodus über
  demselben `VirtualImagePairs`-Fenster, umschaltbar. Die dichte Text-Liste bleibt fürs schnelle
  Label-Durchklicken; die Galerie ist die Sicht- und Kuratier-Fläche. Zwei Zustände je Kachel:
  **markiert** (Teil der aktuellen Mehrfach-Auswahl) und **im aktiven Datensatz**
  (Mitgliedschafts-Overlay).
- **Detail** ist die heutige `BilderStage`, unverändert an den Cursor gekoppelt — plus eine
  Zeile „ist in Datensatz X, Y" (Reverse-Mitgliedschaft).
- **Korb** ist das heutige Komposition-Panel; die Balance-/Split-/Provenienz-Anzeige bleibt.

**Ein Cursor, ein Fenster, ein Markierungs-Set** — dieselbe „eine Quelle der Wahrheit"-Linie
wie heute schon `Suche`/`AktiveBereiche`. Die Galerie schreibt den Cursor (Hover/Klick), das
Detail liest ihn; Navigation (Pfeiltasten) läuft über den bestehenden
[`NavigationIntentHandler`](../Domain.Client.Modules.Blazor/Navigation/NavigationIntentHandler.cs).

---

## 4. Der geteilte Auswahl-/Assoziations-Kontext (das Herzstück)

Zwei neue Notionen im geteilten `Store` (rein clientseitig, wie `AktiveBereiche` heute):

```csharp
// Store (Ergänzung, Skizze)

/// <summary>Mehrfach-Auswahl in der Galerie (Kachel-Häkchen). Ziel manueller Delta-Gesten.</summary>
public IReadOnlySet<Guid> Markierung { get; private set; } = new HashSet<Guid>();

/// <summary>Der aktuell in Bearbeitung befindliche Datensatz — der „Ziel-Korb".
/// Treibt die Mitgliedschafts-Overlays und die Datensatz→Filter-Wechselwirkungen.</summary>
public DatensatzKontext? AktiverDatensatz { get; private set; }
```

`DatensatzKontext` hält, was für die Overlays nötig ist — **abgeleitet aus vorhandenen Queries**,
nicht neu erfunden:

```csharp
public sealed record DatensatzKontext(
    Guid Id,
    DatensatzStatus Status,           // Entwurf | Eingefroren
    IReadOnlySet<Guid> MitgliederIds, // aus HoleDatensatz (Entwurf) bzw. HoleDatensatzSamples (eingefroren)
    IReadOnlyList<RangeHerkunft> Ranges);
```

Damit ist die zentrale Assoziation — *„gehört dieses Bild in den aktiven Datensatz?"* — eine
reine **Mengen-Enthaltensein-Prüfung im Client** (`AktiverDatensatz.MitgliederIds.Contains(id)`),
also O(1) je Kachel, ohne Server-Roundtrip je Kachel.

> Warum kein neuer Domänen-Zustand? Weil Assoziation hier **Sicht** ist, nicht **Wahrheit**.
> Die Wahrheit (Mitgliedschaft) liegt schon im Datensatz-Aggregat/Read-Model. Wir spiegeln sie
> nur in die Galerie. Invariante 5 (Fachcode rein) und die „C# besitzt den Store"-Linie bleiben
> unangetastet.

---

## 5. Die Wechselwirkungen (Assoziations-Matrix)

Das ist der Kern der Anfrage: **Datenpunkte miteinander assoziieren, Zustände ziehen sich
gegenseitig nach.** Jede Zelle ist eine gerichtete Kopplung; jede ist mit vorhandenen Signalen
realisierbar.

| Von → Nach | Wechselwirkung | Mechanik |
|---|---|---|
| **Datensatz → Galerie** | Mitglieder werden markiert/hervorgehoben; Modus „nur Mitglieder" / „nur Nicht-Mitglieder" | Overlay aus `AktiverDatensatz.MitgliederIds`; Filter-Toggle klemmt das Fenster |
| **Datensatz → Filter** | Datensatz öffnen → Filter **springt auf seine Provenienz-Ranges** (`Ranges[]` → `AktiveBereiche` + Label-Filter) | `DatensatzGeöffnet` setzt `Suche`/`AktiveBereiche` aus `RangeHerkunft`, dann `NeuLaden()` |
| **Balance → Filter/Galerie** | „Anomalie fehlt" → ein Klick filtert auf **Kandidaten der unterrepräsentierten Klasse**, die **noch nicht** im Datensatz sind | Balance (aus Samples/Draft) → `ProduktLabel=Anomalie` + „Nicht-Mitglieder"-Klemme |
| **Galerie/Auswahl → Datensatz** | markierte Kacheln per Klick aufnehmen/entfernen; einzelnes Paar direkt rein/raus | `NimmPaareAuf(ids)` / `EntfernePaare(ids)` (Batch, §7) → `PaareAufgenommen`/`PaarEntfernt` |
| **Filter/Range → Datensatz** | „ganze Range → Datensatz" (nach C-Fix je Bereich exakt) | bestehend, korrigiert |
| **Auswahl → Detail** | Klick/Hover auf Kachel → großes Paar; Detail-Prev/Next läuft in **Galerie-Reihenfolge** | Cursor (bestehend); Navigation-Handler (bestehend) |
| **Detail → Datensätze** | „dieses Paar liegt in: Charge-Juni, Q3-OK" (Reverse-Mitgliedschaft) | neue Query `HoleDatensaetzeFuerPaar(imagePairId)` (§7) |
| **Label-Änderung → Korb (Entwurf)** | Neu-Labeln eines Draft-Mitglieds verschiebt live die Balance | schon dynamisch (Draft liest Live-Label); Refresh bei Label-Events |
| **Eingefroren → alles** | eingefrorener Datensatz ist immutabel → Aufnehmen/Entfernen deaktiviert, Overlay „read-only" | `Status==Eingefroren` blendet Delta-Gesten aus (Aggregat lehnt ohnehin ab) |

Die drei mit **Fettdruck** links sind die „cleveren" Kopplungen, nach denen der Nutzer explizit
gefragt hat (*„der Filter wird angepasst, sobald wir einen Datensatz haben"*): **Datensatz →
Filter** und **Balance → Filter** schließen den Regelkreis — der Datensatz *formt* die Suche,
statt nur ihr Ergebnis zu sein.

---

## 6. Wie das „clever" bleibt — Mechanik ohne Invarianten-Bruch

Alles läuft über das vorhandene Muster **View → Intent (IClientEvent) → Handler → Command/Query**
und die generierte Verdrahtung. Kein Handwiring, kein neuer Transport.

- **Mitgliedschafts-Overlay** = Mengenprüfung im Client gegen `AktiverDatensatz.MitgliederIds`.
  Für den *Entwurf* kommen die IDs aus einer erweiterten `HoleDatensatz`-Antwort (das
  Read-Model hält `Mitglieder` bereits — siehe
  [`DatensatzReadModel.Mitglieder`](../Domain.Projections/DatensatzReadModel.cs)); für den
  *eingefrorenen* Stand aus `HoleDatensatzSamples`.
- **Datensatz → Filter** = ein Intent `DatensatzGeöffnet(id)`, dessen Handler die `Ranges[]` der
  `DatensatzAntwort` in `SucheGeaendert` + `AuswahlGeaendert` zurückübersetzt. Die Rückübersetzung
  ist die Umkehrung von `MapKriterien`/`Bereiche.AusTagen` — sauber symmetrisch.
- **Manuelles Delta (Batch)** = eine Galerie-Geste „Auswahl aufnehmen" dispatcht einen Intent, der
  Handler emittiert **ein** `NimmRangeAuf(id, ids, Herkunft("manuelle Auswahl"))` — der vorhandene
  Resolver-Command wird wiederverwendet (**Entscheidung**): kein neues Command, kein Proto-Regen.
  Die IDs sind schon bekannt (Auswahl), also läuft *kein* Resolver-Search — die GUI ruft den
  Command direkt, er yieldet das vorhandene `PaareAufgenommen`. Herkunft mit leeren `Kriterien`
  markiert die Provenienz als „manuelle Auswahl". Batch-**Entfernen** bleibt vorerst eine Schleife
  über das vorhandene, idempotente `EntfernePaar` (kein neuer Vertrag nötig).
- **Reverse-Mitgliedschaft** = eine neue read-only Query auf einen Rückwärts-Index (§7); nur fürs
  Detail, nicht je Kachel.
- **Reaktivität** = die bestehenden `Fx.OnChanged(Store, Store.Cursor|VirtualImagePairs, …)`-
  Kopplungen; die Galerie hängt sich zusätzlich an `Store.Markierung` und `Store.AktiverDatensatz`.

**Invarianten-Check:** Signal bleibt Weckruf (2); Routing über Typen (3); keine
Runtime-Reflection (4); der Fachcode (Aggregate) bleibt rein — Assoziation ist Client-Sicht (5);
nichts Persistentes ohne durablen Konsumenten (6). Die **einzige** Server-Ergänzung ist eine
read-only Query (Reverse-Index) + optional ein Batch-Command — beide additiv.

---

## 7. Ergänzungen am Read-/Command-Modell (Skizze, nicht implementiert)

| Baustein | Ort | Zweck |
|---|---|---|
| **Kein** neues Batch-Command — `NimmRangeAuf` mit Herkunft „manuelle Auswahl" wiederverwenden (Entscheidung) | [`Domain/Datensatz/Commands.cs`](../Domain/Datensatz/Commands.cs) | Batch-Add aus Galerie-Mehrfachauswahl ohne Proto-Regen; Batch-Remove = Schleife über `EntfernePaar` |
| `HoleDatensatz`-Antwort um `Mitglieder[]` erweitern | [`DatensatzResponses.cs`](../Domain.Projections/DatensatzResponses.cs) | Entwurfs-Mitgliedschaft im Client ohne Sample-Seite |
| `HoleDatensaetzeFuerPaar(imagePairId)` + Rückwärts-Index-Read-Model | [`DatensatzQueries.cs`](../Domain.Projections/DatensatzQueries.cs), [`DatensatzProjektion.cs`](../Domain.Projections/DatensatzProjektion.cs) | Reverse-Mitgliedschaft fürs Detail („in welchen Datensätzen liegt dieses Paar?") |
| Thumbnail-Endpunkt oder Größen-Param am `/api/files/…` | Host.Grpc | verkleinerte Bilder für das Grid (§8) |
| Galerie-Stage + `Store.Markierung`/`AktiverDatensatz` + Intents | [`Domain.Client.Modules.Blazor`](../Domain.Client.Modules.Blazor) | die Fläche selbst, generator-verdrahtet |

Bewusst **nicht** nötig: kein neuer Konsumenten-Typ, kein direkter DB-Zugriff, keine
Materialisierung der Balance (bleibt dynamisch/abgeleitet, Konzept-Training §3.3).

---

## 8. Thumbnails — der eine echte Perf-Punkt

Heute wird ein Bild **nur fürs Cursor-Paar** geladen (`DataEffects.razor` → `LoadFile`). Ein
Grid zeigt Dutzende gleichzeitig. Drei Bausteine, damit das trägt:

1. **Verkleinerte Auslieferung** — ein `?w=160`-Param bzw. dedizierter Thumbnail-Pfad am
   Datei-Endpunkt; nicht das Vollbild pro Kachel.
2. **Sichtfenster-getriebenes Laden** — nur Thumbnails der **sichtbaren** Kacheln anfordern
   (die `VirtualList`/`VirtualCollection` kennt das Fenster bereits); Cache pro `Dc*Pfad`.
3. **Ein Bild je Paar im Grid** — im Grid reicht DC0 (oder DC2) als Repräsentant; das Paar-
   Nebeneinander bleibt dem Detail vorbehalten.

Das ist der einzige Punkt mit echtem Ressourcen-Gewicht — bewusst früh benannt, damit die
Galerie nicht am Bild-Traffic erstickt.

---

## 9. Nutzer-Flows (die Wechselwirkungen in Aktion)

**A — Kuratieren mit Sicht.**
`Filter/Baum wählen → Galerie zeigt Thumbnails → auffällige Kacheln markieren → „Auswahl
aufnehmen" → Overlay ● erscheint sofort → Balance rechts zieht nach.`

**B — Datensatz weiterbauen (Datensatz → Filter).**
`Bestehenden Entwurf „Charge-Juni" in der Liste wählen → Filter springt auf seine Ranges → Grid
zeigt genau die Herkunft, Mitglieder als ● markiert → weitere Tage im Baum dazunehmen → „Range
+" nimmt exakt die neuen Bereiche auf (C-Fix).`

**C — Balance gezielt schließen (Balance → Filter).**
`Korb zeigt „Anomalie 8 %" → Klick auf den Anomalie-Balken → Filter = ProduktLabel:Anomalie +
„nur Nicht-Mitglieder" → Grid zeigt nur fehlende Anomalie-Kandidaten → markieren → aufnehmen →
Balance steigt.`

**D — Herkunft eines Bildes (Detail → Datensätze).**
`Kachel anklicken → Detail groß → Zeile „liegt in: Charge-Juni (Entwurf), Q3-OK (v2)" → per Klick
zum jeweiligen Datensatz springen.`

---

## 10. Bauplan & Phasen (Abgrenzung)

| Phase | Inhalt | Server-Anteil |
|---|---|---|
| **0 (erledigt)** | C-Fix: Range je Bereich exakt | — |
| **1** | Galerie-Stage als **zusätzlicher Modus** (Thumbnail-Grid auf `VirtualImagePairs`), Cursor-Kopplung zum Detail, Thumbnail-Laden (§8) | Thumbnail-Endpunkt |
| **2** | Mehrfach-Auswahl (`Store.Markierung`) + manuelles Batch-Delta über **`NimmRangeAuf`-Wiederverwendung** (Add) / `EntfernePaar`-Schleife (Remove) | — (kein neues Command) |
| **3** | Mitgliedschafts-Overlay (`AktiverDatensatz`), `HoleDatensatz`+`Mitglieder[]` | Response-Erweiterung |
| **4** | Wechselwirkung **Datensatz → Filter** (Ranges zurück in den Filter) | — (Client) |
| **5** | Wechselwirkung **Balance → Filter** + Reverse-Mitgliedschaft (Detail) | Reverse-Query + Index |

Jede Phase ist für sich lauffähig und liefert Wert. **Reichweite: alle Phasen 1–5 fest
eingeplant** (Entscheidung) — inkl. der beiden Regelkreis-Kopplungen und der Reverse-Sicht.

**Bewusst außerhalb:** kein Rebalancing/Datenwegwerfen (bleibt Trainings-Sache, Klassengewichte);
keine Materialisierung der Balance; kein neuer Transport.

---

## 11. Entscheidungen & Restfragen

**Getroffen (2026-08-18):**
- ✅ **Galerie ergänzt** die Text-Liste als zusätzlicher Modus (nicht ersetzen).
- ✅ **Batch-Add** über `NimmRangeAuf`-Wiederverwendung (Herkunft „manuelle Auswahl") — kein neues
  Command, kein Proto-Regen. Batch-Remove = `EntfernePaar`-Schleife.
- ✅ **Reichweite bis Phase 5** — inkl. Balance→Filter und Reverse-Mitgliedschaft. Der
  Rückwärts-Index (Server-Read-Model) ist damit fest im Umfang (klärt zugleich die frühere
  Mitgliedschafts-Frage: aktiver Datensatz client-abgeleitet, „in welchen Datensätzen?" über den
  Index).

**Noch offen (klein, vor/bei Umsetzung zu klären):**
- **Balance-Genauigkeit:** die heutige Balance zählt nur die erste Sample-Seite (≤500). Für die
  Balance→Filter-Kopplung genügt die *Richtung* — exakte Zähler (Aggregat-Zähler im Read-Model)
  wären Kür. Default: Richtung genügt, exakt später bei Bedarf.
- **Thumbnail-Auslieferung:** dedizierter Thumbnail-Pfad vs. `?w=`-Param am `/api/files/…` — beim
  Bau von Phase 1 zu entscheiden (Infra-Detail).

> Diese Entscheidungen sind eingearbeitet. Nächster Schritt (auf Zuruf): ein konkreter
> Umsetzungs-Handoff je Phase (wie der M9-Handoff) — dieses Dokument bleibt der Soll-Entwurf.
