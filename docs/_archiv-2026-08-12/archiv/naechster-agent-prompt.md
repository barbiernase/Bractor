# Nächster Agent — Prompt & Weiter-Plan

> Einstiegspunkt für die Folge-Session. Enthält den fertigen Prompt (unten, im Codeblock) plus den
> Kontext, warum diese Reihenfolge. Stand: P4 + P6 vollständig, 2/6 Feature-Strom-Posten geliefert.

## Was fertig ist (auf `main`)
- **P4 — Konsumenten-Maschine** (P4.1 `IEmittentenCursor`, P4.2 Achse-B-Compile-Zeit-Schnitt, P4.3 GA-1-Check).
- **P6 — Pipeline zerlegt** (P6.1 Ballast/Bounding, P6.2 Event-Pfad-Fold: persistiert→Pull, transient→Push).
- **Feature-Strom:** Projektions-Rebuild-Runner, DLQ-Ops-/Read-Pfad.
- **Zählerstände:** Prüfstand **79/79**, Integration **24–25/25** (der eine wackelige Test ist der
  dokumentierte `SnapshotLive`-Cold-Boot-Flake — **NICHT an Timeouts drehen**).

## Was offen ist (empfohlene Reihenfolge)
1. **Rest-Feature-Strom** (Plan: `docs/handoff-feature-strom-rest.md`), tractabelste zuerst:
   Timer-Trigger → Prozess-Verkettung → (Webhook-Trigger) → Deadlines → Monitoring.
2. **KlärungNötig-Integrationstest** (kleine Lücke; braucht eine Saga mit ablehnbarer Kompensation).
3. **P5(b) Marking-Cursor** — bewusst zurückgestellt (`docs/prozess-marking-cursor-konzept.md` §8): riskanter
   inkrementeller Join-Fixpunkt, aktuell kein großer Prozess als Nutznießer. Erst wenn einer existiert.
4. **Multi-Node (P7/P8)** — außerhalb des aktuellen Scopes; NICHT beginnen ohne explizite Ansage.

## Vor dem ersten Build LESEN
`CLAUDE.md` (Projektgedächtnis, oben der Fortschrittsblock), `docs/backend-neubau-fahrplan.md`
(Tor-Liste), `docs/handoff-feature-strom-rest.md`, `docs/zielbild-vereinheitlichte-konsumenten-maschine.md`.

## Umgebung/Test (kritisch — .NET 9 ist EOL, kein Docker-Hub)
- Einmalig: `bash scripts/dev-infra-setup.sh` (installiert .NET-10-SDK + native Postgres/Redis/Consul, startet sie).
- Die Services stoppen nach Inaktivität → bei „Connection refused" das Skript erneut laufen lassen.
- Immer `export DOTNET_ROLL_FORWARD=LatestMajor` (das 10er-SDK baut/läuft `net9.0`).
- Ebene 1: `dotnet test Infrastructure.Pruefstand.Tests/Infrastructure.Pruefstand.Tests.csproj`
- Ebene 2 (sequentiell!): `dotnet test Infrastructure.Integration.Tests/Infrastructure.Integration.Tests.csproj`
- **Bekannt & vorbestehend:** `Domain.Client` baut nicht (`_publish` fehlt, laufendes Client-Refactoring) —
  UNABHÄNGIG vom Backend; die ganze Backend-Kette (Infrastructure/Host.Grpc/Tests) baut grün. Nicht „mitfixen".
- Konvention: jede Scheibe grün getestet (beide Ebenen) → committen → pushen. Deutsch für Kommentare/Domäne.

---

## Prompt (für den neuen Agenten — kopieren)

```
Du arbeitest am Bractor-Backend (selbstgebautes CQRS/ES-Framework auf Proto.Actor/Marten/Redis). Der
große Backend-Neubau („eine einheitliche Konsumenten-Maschine") ist im Kern fertig: P0–P6 abgeschlossen.
Lies ZUERST, in dieser Reihenfolge: docs/naechster-agent-prompt.md, CLAUDE.md (Fortschrittsblock oben),
docs/backend-neubau-fahrplan.md, docs/handoff-feature-strom-rest.md.

Setup (Umgebung ohne Docker-Hub, .NET 9 ist EOL): führe einmal `bash scripts/dev-infra-setup.sh` aus
(installiert .NET-10-SDK + native Postgres/Redis/Consul und startet sie; bei „Connection refused" erneut
laufen lassen). Setze immer `export DOTNET_ROLL_FORWARD=LatestMajor`. Tests:
Ebene 1 `dotnet test Infrastructure.Pruefstand.Tests/...`, Ebene 2 (sequentiell)
`dotnet test Infrastructure.Integration.Tests/...`. Erwartung: Prüfstand 79/79, Integration 24–25/25
(der eine wackelige ist der bekannte SnapshotLive-Cold-Boot-Flake — NICHT an Timeouts drehen).
Ignoriere den vorbestehenden Domain.Client-Build-Fehler (_publish, laufendes Client-Refactoring; das
Backend baut grün).

Aufgabe: Arbeite den restlichen Feature-Strom ab, tractabelste zuerst, jede Scheibe einzeln grün getestet
(beide Ebenen) → committen → pushen:
  1. Timer-Trigger (wiederverwendbarer TimerTriggerActor + ITriggerRegistration; Ingress bleibt Push).
  2. Prozess-Verkettung (Beispiel + Integrationstest: Prozess-Ende-Event startet den nächsten Prozess).
  3. Webhook-Trigger (ASP.NET-Endpoint in Host.Grpc → Trigger).
  4. Deadlines/Timeouts (DB-Uhr, nie Node-DateTime; berührt ProzessManager → vorsichtig).
  5. Monitoring (HealthChecks + Prozess-/DLQ-Zähler zuerst, dann Tracing).
Jeder Punkt hat einen Plan in docs/handoff-feature-strom-rest.md. P5(b) (Marking-Cursor) NICHT anfangen
(bewusst zurückgestellt, siehe docs/prozess-marking-cursor-konzept.md §8). Multi-Node (P7/P8) NICHT anfangen.

Arbeitsweise: reiner Fachcode, keine Runtime-Reflection, alles über Typen/Generatoren. Verteilte Hänge
in-memory (Fake-Cluster) beweisen, nie im langsamen Integrationstest raten. Deutsch für Kommentare.
Bleibe auf einer Feature-Branch und pushe dorthin; nach Main nur auf ausdrückliche Ansage.
```
