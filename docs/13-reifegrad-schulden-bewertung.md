# 13 — Reifegrad, Schulden & Bewertungs-Dossier

Dies ist die konsolidierte Entscheidungsgrundlage: eine nach Schwere priorisierte Liste aller
Befunde über alle Subsysteme, jeweils mit Einschätzung und konkreter Handlungsempfehlung.

## 13.1 Gesamteinschätzung

Das Projekt ist ein **konzeptionell außergewöhnlich kohärentes** CQRS/ES-Framework: wenige
tragende Ideen (Log = Wahrheit, Single-Activation, generiertes typbasiertes Dispatching, ein
Emit-Weg, scharfe Durabilitätsgrenze, vier Konsumenten als eine Maschine) sind mit
bemerkenswerter Disziplin durchgezogen — bis in den Multi-Node-Transport, den Blazor-Client und
das Python-SDK. Die Compile-Zeit-Guards (15 Diagnose-Codes + Boot-Checks) und die ehrliche
Test-/Perf-Kultur („nie faken, was man nicht besitzt"; Perf-Beweise gegen echtes Postgres)
heben es deutlich über typische Eigenbau-Frameworks.

**Der Kern (Schreibseite, Konsum-/Prozess-Maschine, Generatoren, Transport) ist solide, gemessen
grün und produktionsnah.** Die Schwächen liegen fast durchweg **an den Rändern** und in der
**Lücke zwischen Anspruch und Einlösung** an genau einer zentralen Stelle (Co-Commit).

**Reifegrad-Ampel** (Wiederholung aus dem [README](README.md)):

| Subsystem | Reife |
|---|:--:|
| Schreibseite | 🟢 |
| Konsum-/Prozess-Maschine | 🟢 / 🟡 (Co-Commit) |
| Generatoren & Analyzer | 🟢 |
| Multi-Node / Transport | 🟢 / 🟡 |
| Graph-Extractor + SimHost | 🟡 |
| Python-SDK | 🟡 |
| Frontend | 🟡 (Build 2026-08-12 repariert; Legacy-Cleanup offen) |
| Tests & Vermessung | 🟢 |

## 13.2 Stärken (was dieses Projekt auszeichnet)

1. **Invarianten-Disziplin über Sprachgrenzen.** Dieselben sechs Invarianten tragen Backend,
   Blazor und Python. Das ist selten und schwer zu erreichen.
2. **Compile-Zeit statt Laufzeit.** 15 Diagnose-Codes + Boot-Guards verwandeln ganze
   Fehlerklassen (Fehlrouting, Dangling-Command, unbounded Token, Wire-Drift, zyklische Saga)
   in Build-Fehler.
3. **Sagas als typisierte DSL** mit Diamant/Fan-out/Kompensation — der Fachcode ist *nur* die
   Regel; Manager/Korrelation/Marking/Kompensation sind einmal geschrieben.
4. **Ehrliche Nicht-Autoritativität.** Jeder Beschleuniger (Redis, Snapshot, Marking-Cursor) hat
   einen Voll-Read-Fallback. Korrektheit hängt nie an einem Cache.
5. **Selbstbeobachtung.** GraphExtractor + SimHost machen das System als traversierbaren Graph
   sichtbar und *echt* simulierbar — inklusive wertabhängiger Saga-Kaskaden.
6. **Test-/Perf-Kultur.** Store-Semantik nur gegen echtes Marten; generative
   Vollständigkeits-Guards; Perf-Beweise koppeln Durchsatz mit Korrektheit.

## 13.3 Priorisierte Befunde

Schweregrade: **P0** = blockiert / korrektheitsrelevant · **P1** = wichtig, produktionsrelevant ·
**P2** = Wartbarkeit/Hygiene · **P3** = kosmetisch.

### P0 — blockierend / korrektheitsrelevant

**P0-1 · Kein echter Co-Commit im Projektions-Tracker.**
`MartenProjectionTracker` ist als „PHASE-0-STAND — EIGENE SESSION (at-least-once)" markiert und
committet Effekt und Checkpoint in **getrennten** Sessions (`MartenProjectionTracker.cs:11`). Die
im Framework versprochene Exactly-once-*Wirksamkeit* der Projektionen ist damit **vorgesehen,
aber nicht eingelöst** — de facto Dual-Write, abgesichert nur über den Dedup-Schlüssel
`(AggregateId, Version)` + idempotente Upserts.
*Risiko:* Bei Absturz zwischen Effekt-Write und Marken-Write kann ein Effekt erneut angewandt
werden; nur die Idempotenz der Upserts rettet die Konsistenz. Nicht-idempotente Projektions-
Effekte wären unsicher. Der GA-1-Check gibt trotzdem grün, weil ein Tracker *existiert*.
*Beweislage:* `CoCommitPostgresTests` beweist, dass der atomare Co-Commit *technisch geht* —
aber der produktive Tracker nutzt ihn nicht.
*Empfehlung:* Den `MartenProjectionTracker` auf die **eine** Session umstellen (Effekt +
Checkpoint in derselben `SaveChangesAsync`), wie es `IProjectionTracker` verspricht und
`CoCommitPostgresTests` demonstriert. Bis dahin: dokumentieren, dass Projektionen **idempotent**
sein müssen, und den GA-1-Check schärfen (echten Co-Commit statt bloße Tracker-Existenz prüfen).

**P0-2 · Frontend baut nicht. → ✅ BEHOBEN 2026-08-12.**
`Host.Blazor` zog über eine stale Referenz (`Host.Blazor.csproj:16` → `Domain.Client.Ui.Blazor`
→ `Domain.Client` ohne Analyzer) das tote alte UI in den Build → 13 CS0103-Fehler (`_publish`).
*Fix (angewandt):* stale `Domain.Client.Ui.Blazor`-Referenz + verwaistes `using
Domain.Client.ImagePair;` entfernt. Verifiziert: `Host.Blazor` 0 Fehler, voller Solution-Build
grün. Die GUI-Struktur läuft über `Domain.Client.Modules.Blazor`.
*Rest (nach P2-3 verschoben):* die beiden toten Legacy-Projekte `Domain.Client` und
`Domain.Client.Ui.Blazor` liegen noch auf Disk und sollten gelöscht werden.

### P1 — wichtig / produktionsrelevant

**P1-1 · Multi-Node nur container-erprobt, nicht produktiv betrieben.**
Cross-Node ist über alle Planes verdrahtet und bewiesen, aber der produktive Regelweg bleibt
Single-Node (systemd/nativ); Multi-Node existiert als Docker-Compose + Verify-Harness. Zudem
Consul im `-dev`-Modus (kein Quorum). *Empfehlung:* produktives Multi-Node-Setup härten (Consul
mit Quorum/Persistenz, systemd-Cluster-Rezept, oder container-orchestriert), bevor Multi-Node als
„produktionsreif" gilt.

**P1-2 · Rolling Schema-Migration fehlt.**
Der Migrator ist ein Init-Job (Cold-Start), keine laufende Migration. Schema-Evolution im
laufenden Cluster ist nicht abgedeckt. *Empfehlung:* Migrations-Strategie für Zero-Downtime
definieren (additive Schema-Änderungen + Migrator-Lauf vor Rollout).

**P1-3 · Event-Upcasting 1:N (Split) blockiert.**
CQRS046 bricht **jeden** realen 1:N-Upcaster bis die Consumer-Fabric steht. Generator-/leseseitig
ist alles da. *Empfehlung:* Consumer-Fabric fertigstellen und CQRS046 lösen — sonst ist die
Schema-Evolution auf 1:1 beschränkt.

**P1-4 · Client-Command-Pfad ohne Ack-Protokoll.**
Nach 3 Fehlversuchen Dead-Letter, kein Auto-Retry; der gRPC-Service quittiert dem externen Client
nicht. Ein verlorener Client-Command heilt kein Poll. *Empfehlung:* sichtbares Ack/Nack an den
Client (oder client-seitiges Retry mit deterministischer CommandId für Idempotenz).

**P1-5 · Python: keine Query-Antwort, keine Sicherheit, keine Tests.**
`router._handle_query_forward` (`# TODO`) setzt das Response-oneof nie; kein TLS/Auth; keine
Tests. *Empfehlung:* Query-oneof schließen (falls Python-Queries gebraucht werden); TLS + Token
für externe Clients (deckt sich mit dem „Prod-Security"-Ziel); pytest-Grundgerüst.

**P1-6 · `CancellationToken.None` im generierten Pull-Emit-Pfad.**
`PullPathGenerator` reicht `CancellationToken.None` in `router.EmitFor(...)`; bounded nur durch
die interne 5-s-Frist des Emitters (CQRS021 greift dort syntaktisch nicht). *Empfehlung:* einen
gebundenen Token durchreichen oder den Analyzer auch auf den generierten Pfad ausdehnen.

### P2 — Wartbarkeit / Hygiene

**P2-1 · Domänen-Leak: `Reaktionsempfaenger.VerarbeiteteReaktionen`** wächst unbegrenzt in der
Domäne (widerspricht P5). *Empfehlung:* auf die Framework-Inbox (`KommandoVerarbeitet`) umstellen,
wie bei Aggregaten.

**P2-2 · Zwei divergierende Proto-Typ-Maps** (`TypeMappingHelper` vs. `FileGenerator`) stimmen
bereits nicht überein (`bool?`→`bool` vs. `int32`) → latenter Serialisierungs-Drift an der
gRPC-Grenze. *Empfehlung:* auf **eine** geteilte Map konsolidieren.

**P2-3 · Graph/SimHost ungetrackt & außerhalb der `.sln`.** Der ganze Umbau (RoutingTruth,
DomainExtractor, GraphBuilder, HtmlPresenter, SimHost, knowledge-graph.html) ist nicht committet;
SimHost fehlt in der Solution. *Empfehlung:* committen, SimHost in die `.sln` aufnehmen (oder
bewusst als Tool außerhalb markieren).

**P2-4 · `BoundedInbox` nicht airtight** (FIFO ab 10.000). Theoretisches Loch bei extremem
Fan-out/langer Latenz. *Empfehlung:* akzeptierter Tradeoff — dokumentieren; ggf. persistente
Inbox-Marke als Rückfall.

**P2-5 · Marking-/Kompensations-Feinschliff.** `MarkingKompakt` O(N) bei Fan-out;
`NächsteKompensationAsync` liest ab 0. Der eigentliche O(N²)→O(N)-Gewinn ist geliefert; das ist
Rest-Feinschliff. *Empfehlung:* niedrige Priorität, außer bei extremem Fan-out.

**P2-6 · JSON-Round-Trip-`Clone` für Snapshots** — brüchig bei nicht sauber round-trippenden
State-Typen. *Empfehlung:* generierten Deep-Clone (oder Record-`with`) statt Reflection-JSON.

**P2-7 · `generate_registry.py` ist ein Stub** — `domain_registry.py` de facto handgepflegt,
obwohl Automatisierung beabsichtigt ist. *Empfehlung:* Generierung fertigstellen oder den
Stub-Anspruch entfernen.

**P2-8 · Hartkodierte Poll-Intervalle** (30 s / 15 s), hartkodierte Connection-Strings in
Integrationstests, hartkodierte Ausgabe-Namespaces in Generatoren. *Empfehlung:* konfigurierbar
machen / zentralisieren.

**P2-9 · Duplizierte Falt-Logik** (`MartenEventStore.LoadStateAsync` tot vs.
`AggregateRehydrator`) und **Identitäts-Vertrag nur per Kommentar** (`AggregateActorGenerator` vs.
`CommandAggregateMapGenerator`). *Empfehlung:* toten Pfad entfernen; Identitäts-Berechnung in
einen geteilten Helfer ziehen (maschinell verriegeln).

**P2-10 · Monitoring schmal & keine Transport-Sicherheit backendseitig.** Nur `/health` + 2
Zahlen; Default-Credentials `postgres/postgres`; kein OTel. *Empfehlung:* für Produktion OTel +
echte Credentials + TLS (deckt sich mit P1-5).

### P3 — kosmetisch

- **P3-1 · Encoding-Schäden (Mojibake)** in Generator-Kommentaren (`MultiCompilationAnalyzer.cs`,
  `AggregateHandlerGenerator.cs`). *Empfehlung:* Dateien auf UTF-8 normalisieren.
- **P3-2 · Dateiname ≠ Klassenname** (`FactoryGenerator`→`HandlerFactoryGenerator`,
  `DtoMapperGenerator`→`DtoMapperSourceGenerator`), toter Code (`proxy._detect_route`,
  `ProtoEndpointExtensions` nicht gemappt, `CqrsFrameworkOptions` obsolet), viele
  `Console.WriteLine` im `GrpcProxy`. *Empfehlung:* aufräumen bei Gelegenheit.
- **P3-3 · Deadline-Primitiv nicht in einen Prozess integriert** (steht allein). *Empfehlung:*
  Referenz-Integration zeigen.

## 13.4 Korrektheits- vs. Reifegrad-Trennung

Wichtig für die Bewertung: Die meisten Befunde sind **Reifegrad/Hygiene**, nicht
**Korrektheit**. Die einzige *korrektheitsnahe* Lücke im laufenden Kern ist **P0-1 (Co-Commit)** —
und selbst die ist durch idempotente Upserts in der Praxis abgefedert. Alle anderen P0/P1-Punkte
sind entweder mechanisch (P0-2), betrieblich (P1-1/2), bewusst blockiert (P1-3) oder an den
externen Rändern (P1-4/5). Der Aggregat-/Event-/Saga-Kern selbst ist konsistent und getestet.

## 13.5 Empfohlene Reihenfolge (wenn produktiv gehärtet werden soll)

1. ~~**P0-2** Frontend-Referenz entwirren~~ ✅ erledigt 2026-08-12 (Solution-Build grün).
2. **P0-1** echten Co-Commit im Marten-Tracker (die eine substanzielle Korrektheits-Härtung).
3. **P1-4 / P1-6** Command-Kanten wirklich bounded + Client-Ack (Zustellgarantien schließen).
4. **P1-1 / P1-2** Multi-Node produktiv härten + Migrations-Strategie.
5. **P1-5** Python-Sicherheit + Query-oneof (falls extern genutzt).
6. **P2-1 / P2-2 / P2-9** Reinheits-Leak, Proto-Map-Konsolidierung, Duplikate — Wartbarkeit.
7. **P1-3** 1:N-Upcasting-Consumer-Fabric (wenn Split-Evolution gebraucht wird).
8. Hygiene (P2-3 committen, P3-\* aufräumen).

## 13.6 Was diese Doku bewusst offen lässt

- **Integration-Tests wurden hier nicht ausgeführt** (benötigen laufendes Postgres/Consul/Redis).
  Die 41 Tests sind statisch erfasst; ein realer Lauf sollte Teil einer formalen Bewertung sein.
- **Absolute Perf-Zahlen** (+48 %, 9×) stammen aus Projektnotizen und wurden hier nicht
  reproduziert; der LoadHarness ist das Werkzeug dafür.
- **Sicherheits-Audit** (AuthN/AuthZ, Tenant-Isolation, TLS end-to-end) wurde nicht durchgeführt;
  die Befunde zeigen aber, dass Transport-Sicherheit aktuell weitgehend fehlt.
