# Konzept — Streaming-taugliche Architektur (OPC UA, MQTT, Börsen-Feeds)

> **Stand:** 2026-08-28 · **Status:** Konzept (nichts implementiert) · **Fokus dieser Fassung:**
> Ingress (externe Ströme → System). Egress ist mitkartiert, aber nachrangig behandelt.
>
> **Leitfrage:** Wie nimmt das Framework kontinuierliche externe Datenströme (OPC-UA-Subscriptions,
> MQTT-Topics, Börsen-WebSockets wie Bitpanda) auf, **ohne** eine einzige der sechs Invarianten zu
> brechen — und wo genau muss es dafür erweitert werden?

---

## 0. Die eine Entscheidung, aus der alles folgt

Jedes Datum, das über einen Stream hereinkommt, gehört in **genau eine** von zwei Klassen. Diese
Einteilung ist keine Stilfrage — sie ist **Invariante 6** angewandt auf Streaming:

| Klasse | Beispiele | Landet als | Kanal | Garantie |
|---|---|---|---|---|
| **Telemetrie** (verlierbar) | OPC-UA-Sensorwert, MQTT-Heartbeat, „letzter Kurs"-Tick, Orderbuch-Snapshot | `IPipelineTrigger` bzw. `ITransientEvent` | schneller Push-Broker | best-effort, kein Cursor |
| **Faktum** (durabel) | ausgeführter Trade, überschrittener Grenzwert, Auftrag angenommen, Alarm quittiert | `ICommand` → `IEvent` (Log) | Aggregat → Marten | exactly-once wirksam, OCC, Saga-fähig |

> **Merksatz:** *Ein Kurs-Tick ist kein Ereignis — er ist ein Weckruf. Ein ausgeführter Trade ist ein
> Ereignis.* Wer das verwechselt, flutet entweder den Log mit wegwerfbarer Telemetrie (Inv. 1 verletzt:
> der Log ist nicht mehr „die Wahrheit", sondern ein Messwertpuffer) oder verliert ein Faktum, das
> jemand durabel gebraucht hätte (Inv. 6 verletzt).

Das Schöne: **für beide Antworten existiert die Maschine bereits.** Streaming fügt keine neue
Verarbeitungs-Ebene hinzu — es fügt nur eine neue **Ingress-Kante** vor die bestehende Pipeline-/
Aggregat-Maschine.

---

## 1. Wo Streaming andockt — der bestehende Ingress ist schon die halbe Miete

Das Framework hat **drei fertige Ingress-Muster**, die alle demselben Vertrag folgen. Ein
Streaming-Konnektor ist ein viertes Muster derselben Familie:

| Bestehend | Datei | Charakter |
|---|---|---|
| **FileWatcher** | [Domain.Pipeline/ImageProcessing/FilewatcherActor.cs](../Domain.Pipeline/ImageProcessing/FilewatcherActor.cs) | nativer Proto-Actor, pollt eine Quelle, feuert Trigger |
| **Timer** | [Infrastructure/Pipeline/TimerTrigger.cs](../Infrastructure/Pipeline/TimerTrigger.cs) | Intervall → Trigger |
| **Webhook** | [Infrastructure/Pipeline/WebhookTrigger.cs](../Infrastructure/Pipeline/WebhookTrigger.cs) | `POST /webhook/…` → Trigger |
| **→ Streaming-Konnektor** *(neu)* | *n/a* | dauerhaft verbundener Actor, **abonniert** eine Quelle, feuert Trigger |

Alle drei registrieren sich über **denselben** Vertrag `ITriggerRegistration`
([Core/TriggerRegistration.cs](../Core/TriggerRegistration.cs)); der `TriggerStartupService`
([Infrastructure/Pipeline/TriggerStartupService.cs](../Infrastructure/Pipeline/TriggerStartupService.cs))
spawnt sie beim Boot. Ein Konnektor ist **nichts Neues im Kern** — er ist eine weitere
`ITriggerRegistration`.

### 1.1 Der Dreiklang eines Konnektors

```
┌──────────────────────┐   IPipelineTrigger    ┌───────────────────┐   ICommand   ┌───────────┐
│  Konnektor-Actor      │  ───────────────────► │  Pipeline          │ ──────────► │  Aggregat  │
│  (nativer Proto-Actor)│  PipelineTriggerSender │ (IPipelineHandler) │ CommandEmit │  (Decider) │
│  KEIN Domänenwissen   │                        │  ALLES Fachwissen  │             │            │
└──────────┬───────────┘                        └───────────────────┘             └───────────┘
           │ abonniert
           ▼
   OPC UA / MQTT / Bitpanda-WS
```

1. **Konnektor-Actor** — ein nativer `IActor`, exakt wie `FileWatcherActor`. Er hält die Verbindung
   zur Quelle offen, übersetzt jede eingehende Nachricht in einen **rohen** `IPipelineTrigger` und
   sendet ihn via `PipelineTriggerSender.SendAsync`. Er kennt **kein** Aggregat, keine `Guid`, keine
   Fachbedeutung — nur „Topic X hat Payload Y geliefert". (Vorbild-Kommentar in `FilewatcherActor.cs:14`:
   *„Dieser Actor hat KEIN Domain-Wissen!"*)
2. **Trigger-Record** — z.B. `record MqttNachricht(string Topic, byte[] Payload, DateTimeOffset Empfangen) : IPipelineTrigger`.
   Weil `IPipelineTrigger : IWireMessage` ([Abstractions/Interfaces.cs:435](../Abstractions/Interfaces.cs)),
   reist er cross-node und muss vom Wire-Serializer erfasst sein (Boot-Guard fängt das Vergessen).
3. **Pipeline** — die einzige Stelle mit Fachwissen. `Handle(MqttNachricht t, …)` parst das Topic,
   entscheidet „Telemetrie oder Faktum" und `yield`et entweder nichts (reiner Weckruf), ein
   `ITransientEvent` (Telemetrie weiterreichen) oder ein `ICommand` (Faktum → Aggregat).

**Konsequenz:** Für einen neuen Datenstrom schreibt der Entwickler **einen Actor** (Protokoll-Glue,
domänenfrei) + **einen Trigger-Record** + **Pipeline-Handler-Methoden** (Fachwissen). Kern, Generatoren,
Wire, Prozess-Maschine bleiben unberührt. Das ist die Reinheits-Invariante (Inv. 5), durchgezogen bis
an die Streaming-Kante.

---

## 2. Was Streaming *neu* verlangt — die vier ehrlichen Lücken

Die vorhandenen Trigger (File/Timer/Webhook) sind **niederfrequent und zustandslos**. Ein
Live-Feed ist **hochfrequent und verbindungsbehaftet**. Genau hier braucht das Framework Ergänzungen.
Alle vier lassen sich *in der bestehenden Grammatik* lösen — keine bricht eine Invariante.

### 2.1 Backpressure / Frequenz — die wichtigste Lücke

Der bestehende Trigger-Pfad quittiert **jeden** Trigger synchron:
`PipelineTriggerSender.SendAsync` macht ein `RequestAsync<PipelineAck>` mit 5-s-Frist
([PipelineTriggerSender.cs:35](../Infrastructure/Pipeline/PipelineTriggerSender.cs)) — **ein
Cluster-Roundtrip pro Nachricht**. Für einen Datei-Fund alle paar Sekunden ist das ideal. Für einen
Börsen-Tick-Stream mit tausenden Nachrichten/Sekunde ist es der Flaschenhals.

**Lösung — Konflation & Batching *im Konnektor-Actor*, vor der Trigger-Kante:**

- **Konflation (conflation):** Bei reiner Telemetrie zählt nur der *letzte* Wert je Schlüssel
  (Symbol / OPC-Node / Topic). Der Actor hält eine `Dictionary<Schlüssel, LetzterWert>` und feuert nur
  im Takt eines internen `PollTick` (via `context.ReenterAfter`, wie `FileWatcherActor` es für sein
  1-s-Polling tut — `FilewatcherActor.cs:125`). Zwischen zwei Ticks eingetroffene Werte überschreiben
  sich — **verlustbehaftet gewollt** (Inv. 2/6). Ergebnis: konstante Trigger-Rate unabhängig von der
  Eingangs-Rate.
- **Batching:** Ein `record TickBatch(IReadOnlyList<Tick> Ticks) : IPipelineTrigger` trägt viele Werte
  pro Roundtrip. Ein Ack pro Batch statt pro Tick.
- **Fire-and-forget für reine Weckrufe:** Wo selbst der Batch-Ack unnötig ist (reine
  Anzeige-Telemetrie ohne Fachwirkung), kann der Konnektor am Trigger-Pfad vorbei direkt über den
  `BrokerPublisher.PublishFireAndForget` ein `ITransientEvent` in den Broker legen — kein Roundtrip,
  kein Ack. Das ist der schnellste Kanal und exakt das, wofür Invariante 6 den Push-Broker vorhält.

> **Faustregel:** Faktum → gebatchte Trigger mit Ack (Rückstau ist erwünscht, er schützt das Aggregat).
> Telemetrie → konfliert + fire-and-forget (Rückstau ist sinnlos, der nächste Wert heilt).

### 2.2 Verbindungs-Lebenszyklus — Reconnect, Auth, Zertifikate

File/Timer/Webhook haben keinen Verbindungszustand. Ein Feed hat: TCP-Session, TLS/Zertifikate
(OPC UA), API-Keys (Bitpanda), Re-Subscribe nach Reconnect, Heartbeats, Session-Renewal.

**Das gehört *ganz* in den Konnektor-Actor** — und nirgends sonst. Der Actor ist der einzige Ort mit
Protokoll- und Credential-Wissen; die Domäne sieht davon nichts (Inv. 5). Muster:

- **Verbindung als Actor-Zustand.** `Started` → verbinden + abonnieren. Ein interner `WatchdogTick`
  prüft Liveness; bei Abbruch `ReenterAfter(backoff)` → neu verbinden + **alle** Subscriptions neu
  anmelden. Das spiegelt exakt die `SubscriptionReassertLoop` des gRPC-Service (alle 20 s neu anmelden,
  `CqrsClientService.cs:200`) — dasselbe Selbstheilungs-Muster, nur nach außen gerichtet.
- **Credentials über `IConfiguration`/Secrets**, injiziert in die `ITriggerRegistration`-Factory
  (`Props CreateProps(IServiceProvider, Cluster)` — der `provider` liefert Config/Secrets). Nie in der
  Domäne, nie im Trigger-Record.
- **Ein Konnektor-Actor pro Quelle**, gespawnt als top-level Actor durch den `TriggerStartupService` —
  bewusst **kein** virtueller Cluster-Actor (er hat exklusiven, zustandsbehafteten Besitz einer
  externen Verbindung; Single-Activation-Semantik der Aggregate passt hier nicht).

### 2.3 Ordnung & Duplikate

Streams liefern Nachrichten doppelt oder umsortiert (QoS-1-MQTT „at least once", Reconnect-Replay).

- **Für Telemetrie egal** — Konflation nimmt ohnehin den letzten Wert.
- **Für Fakten erledigt es das Framework schon:** wird der Strom-Punkt zu einem `ICommand`, greift die
  **Framework-Inbox-Dedup** über die `CommandId` ([03-schreibseite.md §3.1](03-schreibseite.md), Punkt 3)
  + OCC. Der Konnektor muss nur eine **deterministische, quellen-stabile ID** in den Trigger legen
  (Exchange-Trade-ID, OPC-Sequenznummer, MQTT-Message-ID). Die Pipeline leitet daraus die `CommandId`
  ab → derselbe Trade zweimal empfangen = ein Event. Das ist dieselbe Mechanik, die `CommandEmitter`
  aus der `EmitKausalität` baut ([04 §4.4](04-konsum-und-prozess-maschine.md)).

### 2.4 Bounded Puffer — Speicher gegen Flut

Ein Konnektor darf bei einem Ziel-Rückstau nicht unbegrenzt puffern. Regel wie beim FileWatcher-Cap
(`_maxSeenEntries`, `FilewatcherActor.cs:188`): bounded Ring/Dictionary, ältestes fällt raus. Für
Telemetrie ist Verwerfen korrekt (Inv. 6); für Fakten muss der Rückstau **bremsen** (Batch-Ack
abwarten), nicht verwerfen.

---

## 3. Der neue Baustein — `IStreamKonnektor` (Vorschlag)

`ITriggerRegistration` reicht mechanisch schon aus (ein Konnektor *ist* eine). Aber die vier
Streaming-Sorgen (Verbindung, Reconnect, Konflation, Health) wiederholen sich pro Quelle. Deshalb ein
**dünner, gemeinsamer Rahmen** — analog dazu, wie `PipelineActorBase` den Pipeline-Boilerplate bündelt:

```csharp
// Abstractions — der Vertrag (protokollfrei, testbar)
public interface IStreamQuelle<TRoh>
{
    // Öffnet die Verbindung, liefert einen kalten Strom roher Nachrichten.
    // Reconnect/Auth lebt in der Implementierung; der Rahmen ruft nur (re)StarteAsync.
    IAsyncEnumerable<TRoh> VerbindeAsync(CancellationToken ct);
}

// Infrastructure — der generische Actor-Rahmen (einmal geschrieben)
//   • hält die Verbindung, Reconnect mit Backoff, Watchdog
//   • wendet eine KonflationsPolicy<TRoh> an (None | LetzterWert | Batch(n, fenster))
//   • ruft pro (konflierter) Nachricht: baueTrigger(roh) → PipelineTriggerSender / FireAndForget
public sealed class StreamKonnektorActor<TRoh> : IActor { … }

// Registrierung — wie TimerTrigger.Registrierung(...)
public static ITriggerRegistration Registrierung<TRoh>(
    string name,
    IStreamQuelle<TRoh> quelle,
    Func<TRoh, IPipelineOutput> baueTrigger,   // IPipelineTrigger (mit Ack) ODER ITransientEvent (fire&forget)
    KonflationsPolicy policy);
```

**Was der Rahmen liefert** (und was sonst jeder Konnektor neu erfände):

- Reconnect-Loop + exponentielles Backoff + Watchdog, mailbox-safe (`ReenterAfter`, kein `Task.Run`).
- Konflations-/Batch-Policy vor der Trigger-Kante (§2.1).
- Wahl des Ausgangskanals aus dem Rückgabetyp von `baueTrigger` (Inv. 6 wird zur *Typ*-Entscheidung:
  `IPipelineTrigger` → gebatcht mit Ack; `ITransientEvent` → fire-and-forget Broker).
- Health-Meldung (§6).

**Was die Domäne liefert:** `IStreamQuelle<TRoh>` (Protokoll-Glue) + `baueTrigger` (rohe Übersetzung) +
die Pipeline (Fachwissen). Kein Framework-Kern wird angefasst; die Registrierung ist eine Zeile im
Host-Wiring, genau wie `TimerTrigger.Registrierung(...)`.

> Das ist bewusst **dieselbe Formensprache** wie der Rest des Systems: ein Vertrag in `Abstractions`,
> ein wiederverwendbarer Actor-Rahmen in `Infrastructure`, eine Registrierungs-Zeile im Host, und die
> Domäne bleibt rein. Kein neuer Generator nötig (der Konnektor ist nicht dispatch-abhängig); nur der
> Trigger-Record braucht — wie jeder neue Domain-Typ — seinen Proto-DTO + Wire-Eintrag (CLAUDE.md-Regel).

---

## 4. Egress — Ströme *hinaus* (kurz, für Vollständigkeit)

Fokus dieser Fassung ist Ingress; hier nur das Gerüst, damit die Richtung stimmt.

Die Kartierung ergab: es gibt genau **einen** Live-Push-Ausgang (den bidirektionalen gRPC-Stream), und
der hängt als Subscriber am internen **PubSub-Broker**. Ein externer Egress-Kanal (MQTT-Publish,
OPC-UA-Server-Node) dockt am selben Broker an — **zwei Wege**, je nach Garantie:

| Bedarf | Baustein | Garantie |
|---|---|---|
| **Live-Feed nach außen** (Kurse spiegeln, Dashboard) | **Broker-Subscriber-Actor** — abonniert Event-/Signal-Typen via `BrokerSubscription`, übersetzt in MQTT/OPC. Vorbild 1:1: `EventProxyActor` / `SignalReceiverActor`. | best-effort, verlierbar (Inv. 2) |
| **Garantierter Outbound** (jeden Trade zuverlässig publizieren) | **Reaktion / Emittent** auf der Pull-Maschine — liest den Log geordnet ab Cursor (`IEmittentenCursor`), Seiteneffekt = der externe Publish. Der framework-native **„Reliable Outbox"**. | at-least-once, cursor-getrackt |

> **Kernaussage:** Ein Egress, der **nichts verlieren darf**, ist kein Broker-Abonnent — er ist ein
> **durabler Konsument** (eine Reaktion mit `IEmittentenCursor`, [Abstractions/IEmittentenCursor.cs](../Abstractions/IEmittentenCursor.cs)),
> dessen Wirkung nach außen zeigt. Damit ist „zuverlässiges Publizieren nach MQTT" genau dasselbe
> Problem wie „zuverlässig ein Command emittieren" — und die Maschine dafür steht schon. Ein
> Broker-Abonnent ist nur für das Verlierbare (die Live-Kurs-Spiegelung) richtig.

---

## 5. Bitpanda / Börsen — der Lehrfall, weil er *beide* Klassen mischt

Ein Krypto-Feed ist der ideale Testfall, weil er §0 in Reinform zeigt:

| Datum vom Feed | Klasse | Weg |
|---|---|---|
| Preis-Tick / Orderbuch-Update | **Telemetrie** | Konnektor → konfliert → `ITransientEvent` fire-and-forget → Live-Kurs-Read-Model / Egress-Spiegel |
| Eigener Order-Fill / Trade-Execution | **Faktum** | Konnektor → Trigger → Pipeline → `ICommand` → `Order`-Aggregat-Event (dedupliziert über Exchange-Trade-ID) |
| Order-Lebenszyklus (offen → teilausgeführt → geschlossen) | **Faktum + Prozess** | ein **Saga** (`IProzessDefinition`) über die Order-Events — genau das Diamant-/Join-Muster, das die Prozess-Maschine kann |

**Wichtige Abgrenzung (bewusst, nicht verhandelbar):** Diese Architektur beschreibt das **Aufnehmen,
Modellieren und Persistieren** von Marktdaten und Order-*Ereignissen*. Das **Auslösen echter Trades /
Geldbewegungen** (eine Order tatsächlich an die Börse senden) ist ein Egress-Command mit realer
Finanzwirkung — der gehört hinter eine **explizite, menschlich kontrollierte** Freigabe, nie in einen
automatischen Reaktions-/Saga-Zweig ohne bewusste Bestätigung. Das Framework kann Orders sauber als
Events abbilden; die *Ausführung* real ist eine Betriebs-/Governance-Entscheidung außerhalb dieses
Architektur-Konzepts. (Und: keine Anlageberatung — das ist Technik, keine Finanzempfehlung.)

---

## 6. Betrieb, Config, Monitoring

- **Config:** Konnektoren über `IConfiguration` mit `__`-Env-Override, wie der Rest
  ([06 §6.5](06-transport-multinode-betrieb.md)). Z.B. `Mqtt__Broker`, `Mqtt__Topics__0`,
  `OpcUa__Endpoint`, `OpcUa__CertPath`, `Bitpanda__WsUrl`, `Bitpanda__ApiKey` (Secret).
- **Wo läuft der Konnektor?** Ein Konnektor besitzt eine exklusive Verbindung → er darf **nicht** auf
  mehreren Cluster-Nodes gleichzeitig laufen (doppelte Subscriptions, doppelte Fakten). Optionen:
  (a) als top-level Actor nur auf einer designierten Node-Rolle spawnen (Env `Konnektor__Aktiv=true`);
  (b) später als Single-Activation-Cluster-Actor mit Leader-Wahl. Für Phase 1 reicht (a).
- **Health:** der Konnektor meldet Verbindungsstatus an das bestehende schmale Monitoring
  ([06 §6.6](06-transport-multinode-betrieb.md)) — `/health` wird `Degraded`, wenn ein Konnektor
  getrennt ist. Metrik: `KonnektorVerbunden`, `TicksProSekunde`, `Konflations-Drop-Rate`.
- **Backstop bei Faktum-Verlust:** Ein Konnektor ist Push (verlierbar). Wo ein Faktum **garantiert**
  ankommen muss und der Feed einen Abfrage-Endpunkt hat (REST-„meine letzten Trades"), ergänzt ein
  periodischer **Reconciliation-Poll** (ein Timer-Trigger!) den Live-Feed — dieselbe Signal-plus-Poll-
  Staffelung wie im Kern (P2): der Stream ist schnell, der Poll ist die Wahrheit.

---

## 7. Phasenplan

| Phase | Inhalt | Ergebnis |
|---|---|---|
| **P0 — Fundament** *(dieses Dokument)* | Taxonomie (§0), Andockpunkt (§1), Lücken (§2), Baustein-Vorschlag (§3) | gemeinsame Sprache; Bauplan steht |
| **P1 — Rahmen** | `IStreamQuelle<TRoh>` + `StreamKonnektorActor` + `KonflationsPolicy` + Registrierung, store-frei prüfbar (Konflation/Batch/Reconnect als reine Nähte, wie `TimerTrigger.TickAsync`) | Konnektor-Rahmen grün im Prüfstand |
| **P2 — Erster Konnektor** | **MQTT** (MQTTnet, einfachster: Pub/Sub, kein Session-Zertifikat) end-to-end: Topic → Trigger → Pipeline → Command/Transient; Live gegen einen lokalen Broker | ein realer Strom fließt |
| **P3 — Egress** | Broker-Subscriber-Actor (verlierbar) **und** Reaktions-Outbox (garantiert) für MQTT-Publish | bidirektional |
| **P4 — OPC UA** | Subscriptions/MonitoredItems, Session/Reconnect, Zertifikats-Handling im Konnektor | Industrie-Feed |
| **P5 — Börse/Bitpanda** | Preis-Telemetrie (konfliert) + Order-Fakten (dedupliziert) + Order-Saga + Reconciliation-Poll; Ausführung real bleibt hinter expliziter Freigabe (§5) | der Misch-Fall |

---

## 8. Was NICHT angefasst wird (die Zusicherung)

Der Kern bleibt unberührt: Aggregat-Actor, Batching, Marten-Store, Signal-Mechanik, Prozess-Maschine,
Wire-Serializer, Generatoren, Analyzer. Streaming ist **additiv** — eine neue Ingress-Kante (Konnektor)
und optional eine neue Egress-Kante (Broker-Abonnent / Reaktions-Outbox), beide in der bestehenden
Formensprache. Kein Datenstrom zwingt zu einer neuen Verarbeitungs-Ebene; jeder Strom teilt sich an §0
auf die zwei Maschinen auf, die es schon gibt.

**Die sechs Invarianten, gegen Streaming geprüft:**

1. *Log = Wahrheit* → nur Fakten in den Log, Telemetrie nie. ✔
2. *Signal = Weckruf* → der Konnektor-Tick/-Trigger ist genau das. ✔
3. *Routing über Typen* → Trigger-/Event-Typ routet, keine Topic-Strings im Kern. ✔
4. *keine Runtime-Reflection* → Konnektor braucht keinen Generator; Trigger-Dispatch ist bereits generiert. ✔
5. *Fachcode rein* → Protokoll/Credentials/Reconnect im Konnektor-Actor, Fachwissen nur in der Pipeline. ✔
6. *persistent nur bei durablem Bedarf* → wird zur **Typ-Wahl** `IPipelineTrigger` vs. `ITransientEvent` vs. `ICommand`. ✔
