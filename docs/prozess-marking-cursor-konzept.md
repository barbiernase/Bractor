# Prozess-Marking-Cursor — Konzept (Skizze)

Status: **Skizze, noch nicht umgesetzt.** Benachbarte Optimierung zu den Aggregat-Snapshots
(docs/snapshot-konzept.md) — dieselbe Idee (Cursor + Tail statt Voll-Read), aber auf der
Prozess-Schicht. Bewusst getrennt gehalten: löst ein anderes Problem als Snapshots.

## 0. Das Problem

Der `ProzessManager` hält das Marking (welche Transitionen schon gefeuert/quittiert sind)
NICHT in einem Feld, sondern faltet es bei **jeder** Weckung frisch aus den Ziel-Streams
(`FaltMarkingAsync`, [ProzessManager.cs:116](../Infrastructure/Prozess/ProzessManager.cs)).
Die Reads gehen **ab 0**:

```csharp
async Task<...> Lies(Guid s) { ... evs = await _store.ReadStreamAsync(s, 0, ct); ... }
```

`WakeAsync` feuert **eine** Transition pro Weckung (sequenziell). Ein Prozess mit N Transitionen
braucht also ~N Weckungen — und jede re-faltet über die (wachsenden) Ziel-Streams:

> **N Weckungen × O(N) Lesen = O(N²).**

Für typische Sagas (3–15 Schritte, kleine Ziel-Streams) ist das harmlos. Es wird erst relevant
bei **großen** Prozessen — zwei Achsen:

- **Breite (Fan-out):** `SendeJe(...)` + Count-Join `UndAlle<E>(n)` über N Ziele (Massen-Auszahlung,
  Sammelüberweisung, Bestellung mit N Positionen). ~2N Weckungen, jede sammelt bis zu N Join-Tokens.
- **Länge (akkumulierendes Ziel):** ein Prozess, der M Schritte in **ein** Sammel-Aggregat bucht
  (Batch-Import, Inventurlauf). Derselbe wachsende Stream wird M-mal ab 0 gelesen.

Beispiel Massen-Auszahlung, N = 10.000: ~20.000 Weckungen × bis zu ~10.000 Reads ≈ **2·10⁸
Stream-Reads pro Prozess** — Minuten reines Re-Scannen plus DB-Last.

## 1. Abgrenzung zu Snapshots — zwei orthogonale Kosten

Pro Prozess-Schritt fallen zwei Kosten an:

| Kost­e | Was | Fix |
|---|---|---|
| ① Ziel-Aggregat-Weckung (Rehydration) | ein passiviertes Ziel faltet seinen Stream | **Aggregat-Snapshot** ✓ (docs/snapshot-konzept.md) |
| ② Marking-Re-Fold | der Manager re-liest alle Ziel-Streams ab 0 | **dieses Konzept** |

Aggregat-Snapshots helfen ② NICHT: der Marking-Fold sucht per **`CausationId`** nach Ergebnis-Events
über *mehrere* Streams — der gefaltete State (Saldo …) wirft genau diese Info weg. Bei kurzen
Prozessen dominiert ①; bei großen dominiert ② (quadratisch).

## 2. Die Idee: Marking-Snapshot + Read-Cursor je Ziel-Stream

Das prozess-lokale Analogon zum Aggregat-Snapshot. Ein Dokument pro Korrelation:

```csharp
public sealed class ProzessMarking
{
    public Guid Id { get; set; }              // = Korrelation
    public string RegelHash { get; set; }     // Struktur-Hash der Prozess-Regeln (Invalidierung)
    public int LogVersion { get; set; }       // Stand des Manager-Entscheidungs-Logs
    public Dictionary<Guid,int> StreamCursor { get; set; }   // je Ziel-Stream: zuletzt gefaltete Version
    public MarkingKompakt Marking { get; set; }              // die verdichtete Faltung (s.u.)
    public DateTimeOffset UpdatedAt { get; set; }
}
```

**Weckung mit Cursor** (statt Voll-Fold ab 0):

1. `ProzessMarking` laden. Fehlt es oder `RegelHash` passt nicht → leer starten (Voll-Fold ab 0).
2. Manager-Entscheidungs-Log laden (`LadeStatusAsync`, **unverändert** — nur Entscheidungen, klein).
3. Je bekanntem Ziel-Stream nur den **Tail** lesen: `ReadStreamAsync(s, StreamCursor[s] + 1, ct)`;
   neue Ergebnis-Tokens ins Marking einarbeiten, Cursor vorrücken.
4. Fixpunkt **inkrementell** weiterlaufen: neue Tokens können neue Transitionen aktivieren, deren
   Ziele evtl. neue Streams sind → die einmalig ab 0 lesen (neu = kurz) und in `StreamCursor` aufnehmen.
5. Entscheiden/feuern wie heute.
6. `ProzessMarking` fortschreiben (best-effort).

> **O(N²) Reads → O(N):** jedes Ziel-Event wird genau einmal gelesen (über den Tail), nicht bei
> jeder Weckung neu.

## 3. Warum das SICHER ist (der tragende Punkt)

Der Manager feuert ohnehin **at-least-once** (fire-and-forget), und das Ziel **dedupliziert** über
den deterministischen `Vorgang` = CommandId (Framework-Inbox). Ein **falsches/veraltetes** Marking kann
daher im schlimmsten Fall nur eine Transition **erneut feuern** — die beim Empfänger **verpufft**.
Nie ein falscher Effekt.

Das macht den Cursor risikoarm und deckt Invariante 1 sauber ab: **die Wahrheit bleibt der Log**
(der Manager KANN jederzeit ab 0 falten und tut es bei fehlendem/stale Cache). Der Cursor ist ein
abgeleiteter Beschleuniger, kein autoritativer Zustand — der Design-Grundsatz „Marking aus dem Log,
nie in einem Feld gehalten" bleibt gewahrt, weil das Feld nur ein Cache ist.

## 4. Die eine Falle: kompakte Marking-Darstellung

Naiv „alle Tokens persistieren" bringt das O(N²) zurück — ein Fan-out-Marking mit N Tokens wäre O(N)
groß, und es bei jeder Weckung zu laden ergäbe wieder N × O(N).

Deshalb muss `MarkingKompakt` **verdichtet** sein, nicht roh:

- **Count-Join (`UndAlle<E>(n)`):** nicht N Event-Tokens, sondern ein **Zähler + Done-Set** (welche der
  N erwarteten Vorgänge quittiert sind — Bitset/kompakte Menge). O(1) inkrementelles Update je Tail-Event.
- **Fan-out (`SendeJe`):** ein Done-Set der abgeschlossenen Zweige (welche `AggregateId`/Diskriminator
  fertig ist), statt der vollen Event-Payloads.
- **Lineare Schritte:** ohnehin klein — der letzte quittierte Vorgang je Kante genügt.

Damit bleibt die Marking-State pro Weckung klein und das Update inkrementell (O(Tail)).

## 5. Ablage — wie bei allem anderen

Marten-Dokument pro Korrelation (`mt_doc_prozess_marking`, jsonb), Identität = Korrelation, überschreibend.
`RegelHash` invalidiert bei Regeländerung automatisch (analog zum State-Struktur-Hash der Snapshots).
Schreiben best-effort außerhalb der Entscheidungs-Transaktion (die Entscheidungen bleiben OCC im Log).
Ein `ProzessMarkingThreshold` (jede Weckung / alle K) steuert die Write-Frequenz — da jede Weckung nur
~1 Ergebnis nachträgt, ist „jede Weckung" ein kleiner Upsert und in Summe O(N).

## 6. Phasen & Tore (Vorschlag)

| Phase | Inhalt | Tor |
|---|---|---|
| M0 | `ProzessMarking`-Vertrag + `IProzessMarkingStore` (InMemory + Marten) | kompiliert |
| M1 | `MarkingKompakt` + inkrementeller Fixpunkt neben dem bestehenden Voll-Fold | in-memory: Cursor-Fold == Voll-Fold (identische Feuer-Entscheidungen) |
| M2 | Cursor im `ProzessManager` verdrahten (Voll-Fold bleibt als Fallback) | Prüfstand: alle bestehenden Prozess-Proben grün mit Cursor |
| M3 | Marten-Store + `RegelHash`-Generator + Write-Hook | Live: großer Fan-out (z. B. N=1000) läuft mit O(N) Reads statt O(N²) |

**Proben (in-memory, im Stil der Crash-Proben):**

1. **Äquivalenz:** Cursor-Fold trifft bei jeder Weckung dieselbe Transition wie der Voll-Fold ab 0.
2. **Stale/fehlend:** Marking gelöscht / `RegelHash`-Mismatch → sauberer Voll-Fold, gleiches Ergebnis.
3. **„Während unten":** ein Ziel-Event, das ankam, als der Manager unten war, wird über den Tail
   (Cursor < seine Version) nachgeholt.
4. **Duplikat verpufft:** ein bewusst veraltetes Marking feuert eine Transition erneut → Empfänger
   dedupliziert, kein Doppeleffekt (die Sicherheitsgarantie aus §3).
5. **Fan-out-Skalierung:** bei N Zweigen wird jedes Ziel-Event genau einmal gelesen (Read-Zähler).

## 7. Wann man es NICHT braucht

Kurze Sagas (wenige Schritte, kleine Ziel-Streams) und lang **laufende**, aber schmale Prozesse
(N klein, nur oft geweckt) brauchen den Cursor nicht — für die ist der Voll-Fold billig. Der Cursor
ist die gezielte Antwort auf **breite oder akkumulierende** Prozesse (N in die Tausende). Deshalb nach
den Snapshots: die decken den breiten Normalfall (heiße, lange Aggregate); der Marking-Cursor ist die
Spezialantwort, die erst ein echter Batch/Fan-out-Orchestrator nötig macht.

## 8. Aufwand / Ehrlichkeit

Deutlich anspruchsvoller als Snapshots: der **inkrementelle Fixpunkt** über mehrere Streams mit Joins
ist kniffliger als ein linearer Tail-Fold, und die kompakte Join-Darstellung (§4) ist der Knackpunkt.
Der Nutzen ist real, aber eng: nur bei massiven Prozessen. Empfehlung: erst umsetzen, wenn ein
konkreter großer Prozess existiert — bis dahin dokumentiert als nächste abgegrenzte Optimierung.
