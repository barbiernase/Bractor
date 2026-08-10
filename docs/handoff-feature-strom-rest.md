# Handoff — verbleibender Feature-Strom (Multi-Node bewusst außen vor)

> Stand: P4 + P6 vollständig; Feature-Strom-Posten **Projektions-Rebuild-Runner** und **DLQ-Ops-/Read-Pfad**
> geliefert. Multi-Node (P7/P8) ist explizit NICHT im aktuellen Scope.
>
> **✅ ABGEARBEITET (diese Session, je Scheibe beide Ebenen grün → committet/gepusht auf
> `claude/feature-strom-backend-mpmw6p`):**
> 1. **Timer-Trigger** — `TimerTriggerActor` + entkoppelter Kern `TimerTrigger.TickAsync` (Fabrik + Send-Seam)
>    + `TimerTrigger.Registrierung(...)`; `TriggerStartupService` jetzt als Hosted Service verdrahtet.
> 2. **Prozess-Verkettung** — ein Prozess-Ende startet den nächsten OHNE neue Infra (das terminale PERSISTIERTE
>    Domänen-Event von A ist der Auslöser von B; `ProzessBeendet` taugt NICHT — `IProzessIntern`, Signal inert).
>    Beispiel `Domain/Antrag` + `Domain/Vorgang` (GenehmigungsProzess → AktivierungsProzess).
> 3. **Webhook-Trigger** — generische testbare `MapPipelineWebhook<TRequest>(...)` in Infrastructure; Host-Glue
>    `POST /webhook/datei` → `DateiErkannt`.
> 4. **Deadlines/Timeouts** — DB-Uhr-getriebenes STANDALONE-Frist-Primitiv: `IDbClock`/`MartenDbClock`
>    (`SELECT now()`), `Frist`/`IFristplan`, `FristScheduler` (feuert fällige Fristen über das Emit-Primitiv,
>    deterministische CommandId → Inbox-Dedup). `AddDeadlines(baueCommand)`. **Bewusst KEINE ProzessManager-/
>    Marking-Kopplung** (die Timeout→Kompensation hängt am zurückgestellten P5(b) — dies ist ihr Unterbau).
> 5. **Monitoring Scheibe 1** — `BackendMetrics` (offene Prozesse + DLQ-Zahl), `BackendHealthCheck`
>    (Healthy/Degraded/Unhealthy), `GET /health` + `GET /monitoring/metrics`. **Tracing bleibt offen** (Scheibe 2).
>
> **Zählerstände nach dieser Session:** Prüfstand **91/91**, Integration **33/33** (SnapshotLive-Cold-Boot-Flake
> bimodal wie dokumentiert — auf der Baseline bestätigt, NICHT von diesen Änderungen verursacht).
>
> **Noch offen:** Monitoring-Tracing (Emit-/Wake-Kanten); prozess-gekoppelte Deadlines (nach P5(b));
> KlärungNötig-Integrationsdeckung. Der Rest unten ist der historische Plan (erfüllt, zur Referenz belassen).

## 1. Timer/Webhook-Trigger (klein–mittel)
Der Registrier-Mechanismus **existiert schon** (`Infrastructure/Pipeline/TriggerStartupService.cs` iteriert
`ITriggerRegistration`-DI-Instanzen und spawnt sie; der FileWatcher nutzt ihn). Zu bauen:
- **Timer:** ein wiederverwendbarer `TimerTriggerActor` (Infrastructure/Pipeline), der auf einem Intervall
  (Proto `ReenterAfter`-Schleife) einen konfigurierten `IPipelineTrigger` an die Ziel-Pipeline sendet
  (`PipelineTriggerSender.SendAsync`). Registrierung via `TriggerRegistration(name, (sp,cl)=>Props…)`.
  *Testbarkeit:* die Trigger-Erzeugung als reine Factory herausziehen (deterministisch prüfbar); das Timing
  selbst nicht im Prüfstand testen (flaky).
- **Webhook:** host-spezifisch — ein ASP.NET-Minimal-API-Endpoint in `Host.Grpc`, der auf POST einen Trigger
  in die Pipeline sendet (über den Dispatcher/Cluster). Kein Framework-Kern, sondern Host-Glue.
- **Trigger-Ingress bleibt Push** (Zielbild §6): Timer/Webhook sind keine Log-Events (kein Cursor/Fold).

## 2. Prozess-Verkettung (mittel)
Modell trägt bereits (ein Prozess-Ende-Event kann Auslöser einer weiteren Prozess-Regel sein). Zu liefern:
- Ein **Beispiel/Test**: Prozess A endet mit `ProzessBeendet`/einem Fach-Event → dessen Signal ist Auslöser
  von Prozess B (der `KorrelationsRouter` startet B). Am ehesten als zweite kleine Domänen-Saga + ein
  Integrationstest „A → B verkettet".
- *Prüfen:* ob das terminale Ergebnis-Event von A ein persistiertes Domänen-Event ist (nur solche haben ein
  Signal, an dem B hängen kann) — analog zum P6.2-Persistiert/Transient-Split.

## 3. Deadlines/Timeouts (mittel–groß, nach stabilem Prozessmodell)
Neues Primitiv. Zwei dokumentierte Wege (`docs/prozess-marking-cursor-konzept.md` §… / Zielbild §12):
Timer-Token im Marking (durabler Timer-Wheel) ODER ein Zeit-Event auf einem System-Stream. **Querschnitts-
Regel beachten:** Zeit-Entscheidungen per DB-Uhr, NIE per Node-`DateTime.UtcNow`. Berührt den `ProzessManager`
→ höheres Risiko; erst nach P5(b)-Entscheidung sinnvoll.

## 4. Monitoring (groß, profitiert von der uniformen Maschine)
Metrics/Tracing/HealthChecks + eine **Prozess-Sicht** (Read-Pfad auf die offenen Prozesse / den
`ProzessOffenIndex` + die DLQ-Zählung aus dem neuen `IDeadLetterReadStore.CountAsync`). Breit; am besten in
Scheiben (erst HealthChecks + DLQ-/Prozess-Zähler, dann Tracing der Emit-/Wake-Kanten).

## Regressions-Orakel & Infra
- Ebene 1: `dotnet test Infrastructure.Pruefstand.Tests` (aktuell 79/79).
- Ebene 2: `dotnet test Infrastructure.Integration.Tests` (25/25; SnapshotLive-Cold-Boot-Flake bimodal).
- Infra: `scripts/dev-infra-setup.sh` (native Postgres/Redis/Consul + .NET 10, `DOTNET_ROLL_FORWARD=LatestMajor`).
