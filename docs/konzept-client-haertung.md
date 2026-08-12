# Konzept — Client-Härtung (verifizierte Analyse & Plan)

> **Status: Analyse + T2a & T3 umgesetzt (2026-08-12).** Fundiert am echten Code (nicht aus
> Subagent-Berichten übernommen — zwei frühere Befunde waren zu grob und sind hier korrigiert).
> **T2a** (Dispatcher-`CommandFailed` bei Erschöpfung) und **T3** (Python-Query-oneof + pytest-
> Grundgerüst) sind implementiert & getestet. **T1** (Transport-Sicherheit) auf Nutzer-Entscheid
> vorerst zurückgestellt. Verwandt: [09](09-python-sdk.md), [08](08-frontend-blazor-client.md),
> [13 P1-2/P1-4/P1-5](13-reifegrad-schulden-bewertung.md).

## 1. Die Client-Oberfläche heute (verifiziert)

Zwei Clients an *einem* bidirektionalen gRPC-Stream (`CqrsClientService.Connect`): der Blazor-Client
(`Client.Infrastructure`) und das Python-SDK (`cqrs_client`). Vier Nachrichtenklassen: Command
(fire-and-forget), Query (request/response), Event (Server-Push), Trigger (Ack).

## 2. Command-Zustellgarantie (P1-4) — was WIRKLICH gilt

Der Client wird quittiert — nur nicht in jedem Fehlerfall:

| Fehlerfall | Client erfährt es? | Weg |
|---|:--:|---|
| Lokaler Sendefehler (nicht verbunden / Serialisierung) | ✅ | `CommandSendFailed` (`ConnectionModule.cs:139/172`) |
| Server-Mapping schlägt fehl | ✅ | `SendErrorAsync("COMMAND_MAPPING_FAILED")` (`CqrsClientService.cs:398`) |
| Fachliche Ablehnung / technischer Actor-Fehler | ✅ | `CommandFailed` targeted an `OriginSessionId` (`AggregateActorBase.cs:124/443`); Client ist auto-subscribed (`CqrsClientService.cs:326`) |
| **Dispatcher-Platzierung erschöpft (3×, erreicht nie einen Actor)** | ❌ | nur Dead-Letter server-seitig (`AggregateDispatcher.cs:114`) |
| **Erfolg** | ❌ (bewusst) | kein positiver Ack; Erfolg = ankommende Events |

**Die zwei realen Lücken:**
- **(a) Stille Verlust-Lücke:** Erschöpft der Dispatcher die Platzierung, existiert kein
  `CommandFailed` — weil das der *Actor* publiziert, den der Command nie erreicht hat. Tritt nur bei
  nicht-routbarem Cluster auf, ist dann aber ein stiller Verlust aus Client-Sicht.
- **(b) Nicht-idempotenter Client-Pfad:** `ConnectionModule.cs:160` sendet `CommandModus.Client`
  (OCC via `ExpectedVersion`), **nicht** `Emittiert` (Inbox-Dedup). Keine deterministische CommandId.
  Ein blinder Retry nach verlorenem Ack könnte doppelt anwenden — deshalb der bewusste „kein
  Auto-Retry" im Dispatcher. Es gibt zudem kein Client-Timeout / kein Pending-Tracking.

## 3. Python-SDK (P1-5) — was fehlt

- **Query-Antwort kaputt:** `router._handle_query_forward` (`router.py:222-229`) baut ein **leeres**
  `QueryResponsePayloadDto()` und sendet es — der `# TODO: Set the correct oneof field` heißt: das
  Response-oneof wird nie gesetzt. Ein Python-Client kann eine Query empfangen und den Handler
  laufen lassen, aber die Antwort nicht zurückserialisieren. **Fix klein:** der `mapper` besitzt die
  `type → oneof-Feld`-Map bereits (für Command/Event-Envelopes) — dieselbe Logik auf
  `QueryResponsePayloadDto` anwenden.
- **Client→Client-Trigger:** nicht implementiert (nur Warnung, `router.py:298`).
- **Keine Tests:** kein `test_*`, keine pytest-Konfig.
- (Der ML-Worker ist davon unberührt: `QUERY_TYPES = frozenset()` — er beantwortet keine Queries.)

## 4. Transport-Sicherheit (P1-2, system-weit) — der große Punkt

**Nicht** Python-lokal, wie ursprünglich notiert, sondern die **gesamte** gRPC-Oberfläche:
- Host.Grpc bindet **h2c-plain** (kein TLS).
- Blazor-Client verbindet `http://…:5001`.
- Python-Client: `Channel(host, port)` plain (`proxy.py:112`).
- **Kein Auth:** kein Token/mTLS am `Connect`-Handshake, `user_id` immer leer, keine Authz an
  Command/Query. Der Capabilities-Handshake trägt nur Nachrichtentypen, keine Identität.

Für den „Prod-Security"-Anspruch ist das die größte Lücke. Sie betrifft **Server + beide Clients
gemeinsam** — eine isolierte Client-Änderung reicht nicht.

## 5. Die drei Arbeits-Themen (priorisiert)

| Thema | Umfang | Berührt | Nutzen |
|---|---|---|---|
| **T1 — Transport-Sicherheit** (TLS + Auth-Token/mTLS + Identitäts-Propagation) | groß | Server + Blazor + Python | Voraussetzung für jeden externen/produktiven Betrieb |
| **T2 — Command-Zustellgarantie** | klein + optional groß | Server (Dispatcher) [+ Client] | schließt den stillen Verlust; optional sicherer Retry |
| **T3 — Python-SDK-Vervollständigung** (Query-oneof, Trigger, pytest) | klein–mittel | nur Python | vollständiges, testbares SDK |

### T2 im Detail (zwei Stufen)
- **T2a (klein, hoher Wert):** Der Dispatcher emittiert bei erschöpfter Platzierung ein
  `CommandFailed` (die `OriginSessionId` liegt im Envelope vor; er braucht Zugriff auf den
  `BrokerPublisher`). Schließt die stille Verlust-Lücke — der Client erfährt „nicht zugestellt".
- **T2b (größer, optional):** deterministische CommandId auf dem Client-Command + Inbox-Dedup auf
  dem Client-Pfad (analog zum `Emittiert`-Pfad, der es schon hat) → macht Client-seitigen Retry
  *sicher*; erlaubt einen Client-Timeout mit Pending-Registry.

### T1 im Detail (Skizze, noch zu entscheiden)
- Server: Kestrel auf h2 + TLS-Zertifikat; `Connect` verlangt ein Token (Header) oder mTLS.
- Identität/Tenant aus dem authentifizierten Kontext in die Envelope-Metadaten (nicht in die
  Domänen-Typen — Reinheit, P5), nutzbar für Authz + Mandanten-Trennung.
- Blazor-Client: `https://` + Token-Beschaffung; Python-Client: `Channel(..., ssl=...)` + Token.
- Offene Design-Fragen: Token-Quelle (OIDC? statischer Service-Token für Worker?), mTLS vs. Bearer,
  Tenant-Modell. **Das ist selbst ein eigenes Konzept wert, bevor Code entsteht.**

## 6. Empfohlene Reihenfolge

1. **T2a** (Dispatcher-`CommandFailed`) — ✅ **umgesetzt 2026-08-12.** Der Dispatcher publiziert bei
   erschöpfter Platzierung ein targeted `CommandFailed` an die `OriginSessionId` (spiegelt das
   Actor-Muster). Neu/geändert: `AggregateDispatcher.cs` (+`BaueCommandFailed`), DI in
   `CqrsServiceExtension.cs`, Prüfstand-Test `DispatcherCommandFailedTests` (2 Fälle). Prüfstand grün.
2. **T3** (Python-Query-oneof + pytest) — ✅ **umgesetzt 2026-08-12.** `PayloadMapper.wrap_query_response`
   setzt das oneof-Feld über die vorhandene Feld-Map; `router._handle_query_forward` nutzt es (TODO weg).
   pytest-Grundgerüst (`requirements-dev.txt`, `pytest.ini`, `tests/test_mapper_query_response.py`,
   2 Tests) — lokal in einem venv verifiziert (2 passed).
3. **T1** (Transport-Sicherheit) — ⏸ zurückgestellt (Nutzer-Entscheid). Der große Brocken; verlangt
   vorab eine eigene Design-Entscheidung (Token-Modell/mTLS/Tenant). Danach Server + beide Clients.
4. **T2b** (idempotenter Client-Pfad + Timeout) — offen/optional, wenn echte Auto-Retry-Garantie gewollt.

> Offen aus T3: Client→Client-Trigger (`router.py:298`, nur Warnung) — separat, geringe Priorität.

## 7. Stance / Grenzen

- **Command bleibt fire-and-forget** (kein synchroner positiver Ack) — das ist bewusstes CQRS: der
  Read-Model-Update *ist* die Bestätigung. T2a/T2b härten nur den *Fehler*-Rückkanal.
- **Sicherheit ist kein reines Client-Thema** — T1 ist Server-first.

## 8. Entscheidungsstatus

- **T2a (Command-Zustellgarantie, klein):** ✅ umgesetzt & getestet (2026-08-12).
- **T3 (Python-SDK: Query-oneof + pytest):** ✅ umgesetzt & getestet (2026-08-12).
- **T1 (Transport-Sicherheit):** ⏸ zurückgestellt (Nutzer-Entscheid) — braucht vorab ein eigenes
  Design-Konzept (Token-Modell/mTLS/Tenant), bevor Code entsteht.
- **T2b (idempotenter Client-Pfad + Timeout):** offen/optional.
