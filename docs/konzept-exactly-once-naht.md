# Konzept — Die Exactly-once-Naht (Analyse & Entscheidungsgrundlage)

> **Status: Analyse + Hebel 1 umgesetzt (2026-08-12).** Dieses Dokument denkt die Projektions-Naht
> durch und beantwortet die Leitfrage: *Wie voraussetzungsreich ist exactly-once, wenn das Framework
> nur Methoden bereitstellt und der Store sie umsetzt?* **Hebel 1 (`ICoCommitTracker` + verschärfter
> GA-1-Guard) ist inzwischen implementiert** (§7); Hebel 2 (Unit-of-Work) bleibt Kür.
> Verwandt: [04 §4.3](04-konsum-und-prozess-maschine.md), [13 P0-1](13-reifegrad-schulden-bewertung.md).

## 1. Zusammenfassung (für Eilige)

- Die Naht ist **richtig designt**: store-agnostisch, das Framework schreibt die *WIE*-Frage
  nicht vor (`IProjectionTracker`-Doc: „das ist allein Sache des Stores").
- Co-Commit ist **implementiert und gegen echtes Postgres bewiesen** — nicht „vorgesehen":
  `Domain.Infrastructure/ImagePairStore.cs` + `ImagePairHistorieStore` puffern Effekte und
  committen sie mit dem Checkpoint in *einer* Marten-Session; `CoCommitPostgresTests` beweist
  Absturz-Sicherheit (genau ein Eintrag).
- `MartenProjectionTracker` ist der **bewusste at-least-once-Fallback** (Dual-Write) für
  Projektionen mit idempotentem Effekt.
- Der reale Rest war **kein fehlender Mechanismus**, sondern: **Atomarität war nicht aus den Typen
  überprüfbar** — `IProjectionTracker` wird von *beiden* getragen (co-committend UND dual-writing),
  und der Boot-Guard prüfte nur `!= null` (false-green). **Geschlossen (2026-08-12):** der Marker
  `ICoCommitTracker` + GA-1 prüft ihn statt `!= null` (Hebel 1, §7).

## 2. Was „exactly-once" hier heißt (die Theorie, die alles entscheidet)

**Exactly-once-*Zustellung* ist prinzipiell unmöglich** (Zwei-Generäle-Problem: Effekt und
Quittung lassen sich über eine beliebige Grenze nie atomar koppeln). Erreichbar ist nur
**exactly-once-*Wirksamkeit*** (effectively-once), und dafür gibt es genau **zwei** Mechanismen:

| Mechanismus | Kernvoraussetzung | Kostet |
|---|---|---|
| **(A) Atomarer Co-Commit** — Effekt + Marke in *einer* Transaktion | Effekt und Checkpoint in **derselben** transaktionalen Ressource | den **Store** |
| **(B) Idempotenter Effekt** + at-least-once-Zustellung | jeder Effekt wiederholungsfest (Upsert / Dedup) | den **Entwickler** |

Das Framework kann exactly-once **nicht selbst herstellen**. Es kann nur eine Naht anbieten, die
(A) *erlaubt* und sonst auf (B) zurückfällt. Genau das tut es. Wichtig: **(B) entkommt der
Atomarität nicht für Append/Ledger-Effekte** — ein „schon gesehen?"-Dedup müsste selbst atomar
mit dem Append geschrieben werden, sonst ist es wieder Zwei-Generäle. Idempotenz hilft nur bei
*natürlich* idempotenten Upserts.

## 3. Wie die Naht heute aussieht

Der Vertrag (`Abstractions/IProjectionTracker.cs`) ist bewusst minimal:
`LastProcessedVersionAsync` · `MarkProcessedAsync` · `ResetAsync`/`ResetAllAsync`.

Der `ProjectionAdapter` (`Infrastructure/Projections/ProjectionAdapter.cs`) besitzt die *Policy*,
aber **keine Transaktion**: Marke lesen → ab Marke+1 lesen → pro Event `dispatch` (Effekt) →
am Batch-Ende `MarkProcessed` (Marke). Effekt (`:103`) und Marke (`:115`) sind für den Adapter
**zwei getrennte Kollaborateure**.

Der Co-Commit entsteht erst im **Store**: `ImagePairStore` implementiert `IProjectionTracker`
**und** das Effekt-Write-Interface in *einem* Objekt. Die Writes **puffern** nur
(`_pending`); `MarkProcessedAsync` öffnet *eine* `IdentitySession`, spielt die Puffer
geordnet ab (read-your-writes), staged den `ProjectionCheckpoint`, ein `SaveChanges` → atomar.

## 4. Wie voraussetzungsreich ist (A)? — Die 6 impliziten Bedingungen

Damit ein Store aus der minimalen Naht *atomaren* Co-Commit macht, muss er sechs Dinge
erfüllen — fünf davon stehen **nirgends im Typsystem**:

1. **Store == Tracker (dasselbe Objekt).** Nur dann teilen Puffer und Checkpoint eine Session.
2. **Effekt muss aufschiebbar sein** (puffern, nicht durchschreiben).
3. **Unit-of-Work wird aus der Aufrufreihenfolge erraten**: `LastProcessedVersion` = Batch-Beginn
   (leert den Puffer, `ImagePairStore.cs:142`), `MarkProcessed` = Commit. Kein explizites
   Begin/Commit/Rollback.
4. **Read-your-writes im Batch** — Identity-Map-Session + geordnetes Replay (Marten-spezifisch).
5. **Zustandsbehaftet + transient + Single-Writer** — der Puffer lebt in der Instanz; ein Singleton
   würde über nebenläufige Streams korrumpieren.
6. **Fehlerpfad hängt am Adapter** — wirft der Dispatch, wird nie committet (gut), aber der
   schmutzige Puffer wird erst vom nächsten `LastProcessedVersion` geleert. Kein Rollback.

**Antwort auf die Leitfrage:** Co-Commit ist auf der **Framework-Seite fast gratis** (drei
Methoden), auf der **Store-Seite schwer** (sechs Bedingungen, überwiegend implizit). Liegt der
Effekt in einer *anderen* Ressource als der Checkpoint, ist (A) **unmöglich** → nur (B) oder
Outbox/2PC.

## 5. Warum es heute nicht überprüfbar ist (verifiziert am Code)

Der Generator löst den Tracker **nicht** unabhängig aus DI auf, sondern aus den **eigenen
Ctor-Stores der Projektion** (`PullPathGenerator.cs:129–148`):

```csharp
var s0 = provider.GetRequiredService<IFooWriteStore>();   // Ctor-Arg der Projektion
var projection = new FooProjection(s0, ...);              // dieselbe Instanz
var candidates = new object[] { s0, ... };               // dieselben Instanzen
var tracker = candidates.OfType<IProjectionTracker>().FirstOrDefault();
```

Für das **Ein-Store-Muster ist `store == tracker` damit per Konstruktion erzwungen** — der
Tracker *ist* einer der Stores, durch die die Projektion schreibt. (Ein versehentliches
„zwei getrennte DI-Objekte" ist so nicht möglich.)

**Der Rest-Fehlgriff ist enger, existiert aber:**
- **Zwei-Parameter-Fall:** `FooProjection(IFooWriteStore effekt, MartenProjectionTracker marke)` —
  `effekt` schreibt durch, `marke` ist der Tracker (andere Session) → getrennte Commits, und
  GA-1 besteht.
- **Write-through-Einzelstore:** ein *einzelner* Store, der `IProjectionTracker` trägt, aber
  intern durchschreibt statt zu puffern → dual-write, GA-1 besteht.

Kern: **`IProjectionTracker` trägt keinen Beweis der Atomarität.** Er wird von beiden getragen:

| Store | trägt `IProjectionTracker` | co-committet wirklich? |
|---|:--:|:--:|
| `ImagePairStore` (puffert → 1 SaveChanges) | ✓ | ✓ |
| `MartenProjectionTracker` (eigene Session) | ✓ | ✗ |

Und `GaEinsPruefung` prüfte ursprünglich nur `tracker is null` — **notwendig, aber nicht
hinreichend** (false-green). **Geschlossen (2026-08-12):** der Check prüft jetzt
`tracker is not ICoCommitTracker` → ein bloßer `IProjectionTracker` an einer `IAppendProjektion`
bricht laut am Boot. **Atomarität selbst bleibt eine Laufzeiteigenschaft der Store-Transaktion und
statisch nie *beweisbar*** — überprüfbar nur per Store, per Crash-Test (existiert:
`CoCommitPostgresTests`; der neue Guard ist per `GaEinsPruefungTests` gedeckt).

## 6. Design-Optionen für die Naht

| Option | Framework bietet | Store setzt um | Voraussetzung | Mis-Wiring verhindert? |
|---|---|---|---|---|
| **A — Status quo (implizites Protokoll)** | 3 Methoden, UoW implizit | store==tracker, puffern, Reihenfolge erraten | hoch, **implizit** | nein |
| **B — Explizite Unit-of-Work** | `Begin(stream) → Einheit`; Effekt+Marke hindurch; `Commit(version)`/Dispose=Rollback | Effekt + Marke an *ein* Handle | mittel, **explizit** | ja |
| **C — Verschmolzener Projektions-Store-Vertrag** | „puffern" + „commit(version)" als *ein* Interface | formalisiert, was `ImagePairStore` schon ist | mittel | ja |
| **D — Idempotenz-first (kein Co-Commit)** | Marke + Dedup-Schlüssel | jeder Effekt idempotent | niedrig FW / hoch Entwickler | n/a (dodgt Append nicht) |
| **E — Outbox / 2PC** | Intent-Append + Relay | zwei Ressourcen koordinieren | sehr hoch | für getrennte Stores der einzige Weg |

## 7. Die zwei API-Hebel (falls Garantie statt Disziplin gewollt)

**Hebel 1 — Atomarität als eigener Typ. ✅ UMGESETZT (2026-08-12).** Ein distinkter Marker, den
*nur* co-committende Stores tragen:
```
ICoCommitTracker : IProjectionTracker   // "Effekt + Marke in EINER Transaktion"
```
GA-1 prüft jetzt `tracker is not ICoCommitTracker` statt `is null`. `MartenProjectionTracker` trägt
ihn **nicht** → eine `IAppendProjektion` mit Dual-Write-Tracker **bricht laut am Boot**. Umgesetzt:
`Abstractions/ICoCommitTracker.cs` (Marker), `ImagePairStore`/`ImagePairHistorieStore` tragen ihn,
`GaEinsPruefung` prüft ihn, `GaEinsPruefungTests` beweist den geschlossenen false-green. (Bleibt ein
*Versprechen* — aber ein bewusstes, überprüfbares, das `CoCommitPostgresTests` einlöst.) Winziger,
risikoarmer Schritt; konsistent mit P8/P9 (fail-fast, ein Weg erzwungen).

**Hebel 2 — Effektfläche nur durch die Commit-Einheit.** Das Framework holt *eine* Unit-of-Work
vom Store, routet Effekt-Writes *und* Marke hindurch, ruft einmal `Commit(version)`. Dann gibt es
**keinen getrennten Tracker mehr, den man mis-wiren könnte** — Effektfläche und Commit sind *ein*
Objekt per Typ. **→ größerer Umbau (berührt den Adapter + die Handle-/WriteContext-Naht); Kür, nicht
jetzt.**

## 8. Die irreduzible Grenze

Auch mit klarer API bleibt **eine** Sache per-Store zu beweisen: dass die *eine*
`Commit`-Implementierung wirklich eine Transaktion ist (Marten: eine Session, ein `SaveChanges`).
Das ist Laufzeit, kein Typ. Die klare API verengt die Vertrauensgrenze nur — von „irgendein
`IProjectionTracker` irgendwo" auf „diese *eine* markierte `ICoCommitTracker`-Implementierung" —
und die deckt genau ein Crash-Test ab.

## 9. Empfehlung & Stance

1. **Stance beibehalten:** exactly-once bleibt **Opt-in** (idempotenter Upsert als Normalfall,
   atomarer Co-Commit für Append/Ledger). Die Voraussetzungs-Analyse rechtfertigt das — Co-Commit
   überall zu erzwingen wäre teuer und für getrennte Stores unmöglich.
2. **Doku:** diese Analyse + der Vertrag + die 6 Voraussetzungen + die Crash-Test-Pflicht je
   Co-Commit-Store. ✅ erledigt.
3. **Minimal-Code:** `ICoCommitTracker` + GA-1 darauf prüfen. ✅ **umgesetzt (2026-08-12)** —
   schließt den false-green, fail-fast gemäß P9.
4. **Größer (optional/später):** Unit-of-Work (Hebel 2), falls Mis-Wiring strukturell unbaubar
   werden soll. Offen — bewusst zurückgestellt.

## 10. Entscheidungsstatus

- **Doku-Ebene:** erledigt (dieses Dokument + P0-1-Korrektur in [04](04-konsum-und-prozess-maschine.md)/[13](13-reifegrad-schulden-bewertung.md)).
- **Code-Ebene — Hebel 1:** ✅ umgesetzt & getestet (2026-08-12). Neu/geändert:
  `Abstractions/ICoCommitTracker.cs`, `Domain.Infrastructure/ImagePairStore.cs` +
  `ImagePairHistorieStore.cs` (tragen den Marker), `Infrastructure/Projections/GaEinsPruefung.cs`
  (prüft `ICoCommitTracker`), `GaEinsPruefungTests.cs` (4 Fälle). Prüfstand grün.
- **Code-Ebene — Hebel 2 (Unit-of-Work):** offen, bewusst zurückgestellt.
