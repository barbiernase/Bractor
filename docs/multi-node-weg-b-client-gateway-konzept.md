# Multi-Node · Weg B: Client-Targeted-Delivery cross-node robust machen

> **Status: UMGESETZT — via Variante B (periodisches Re-Assert), nicht via Gateway-Kind.**
> Der socket-haltende Node meldet seine Client-Subscriptions periodisch erneut an die Shards
> (`SubscriptionTracker.ReassertAllAsync` + Re-Assert-Loop in `CqrsClientService.Connect`, Intervall 20 s) —
> das direkte Analogon zum Projektions-Poll-Backstop. Test: `ClientSubscriptionReassertTests` (Integration).
> Das ursprünglich skizzierte **Gateway-Kind (Variante A, unten)** wurde als überdimensioniert verworfen —
> es löst keinen Fall, den B nicht auch löst (Begründung im Abschnitt „Zwei Wege"). Es bleibt als
> dokumentierte schwerere Alternative erhalten, falls je eine vom Socket-Node UNABHÄNGIGE
> Subscription-Durabilität gebraucht wird.
>
> Ursprünglich erhoben aus zwei code-verifizierten Subsystem-Analysen am Stand `68de45f`.

## Warum dieses Dokument

Bei der Erkundung für Multi-Node Iteration 2 kam heraus: es gibt **zwei** Subscriber-Wege im
PubSub, und sie sind fundamental verschieden. Weg A ist mit dem Iteration-2-Serializer sauber
gelöst; Weg B **funktioniert** damit zwar cross-node, hat aber eine **Robustheits-Lücke**, die
ein eigenes Bauteil braucht. Dieses Dokument hält fest, worin die Lücke besteht und wie das
node-adressierende Gateway-Kind sie schließt — damit die Entscheidung „später, getrennt"
nachvollziehbar und umsetzbar bleibt.

## Die zwei Wege (zur Erinnerung)

| | **Weg A — durable Projektionen** | **Weg B — gRPC-Client-Sessions** |
|---|---|---|
| Receiver | `SignalReceiverActor` (lokal `Root.Spawn`, zustandslos) | `EventProxyActor` (lokal `Root.Spawn`, hält `IServerStreamWriter`) |
| Subscribt mit | `context.Self` (lokale PID) | `proxyPid` (lokale PID) |
| Empfängt | `SignalEnvelope` → weckt Adapter (lokale Fkt.) | `EventEnvelope` → `_responseStream.WriteAsync` (Socket) |
| Ziel-Wahl | Broadcast an alle Shards | **Targeted** via `EventEnvelope.TargetSubscriberId = OriginSessionId` → genau ein Shard (`ShardFor`) |
| **Sicherheitsnetz bei Verlust** | **Poll-Backstop (30 s)** heilt (Invariante 2) | **KEINES** — es gibt keinen Poll für Client-Targeted-Events |

Belege: `Infrastructure/GrpcClient/EventProxyActor.cs:48-50` („EXISTIERT NUR FÜR DIE PID!", hält
`IServerStreamWriter`), `SubscriptionTracker.cs:33,74` (subscribt mit `proxyPid`),
`CqrsClientService.cs:110` (`Root.Spawn` des Proxys), `PubSub/Actors/BrokerShardActor.cs:103-105`
(Targeted: `_registry.TryGet(TargetSubscriberId, out var pid)` → `context.Send(pid, envelope)`),
`AggregateActorBase.cs:681,720` (setzt `TargetSubscriberId = cmdEnvelope.OriginSessionId`).

## Was Iteration 2 für Weg B bereits leistet — und was NICHT

**Leistet (Funktion):** Mit dem Wire-Serializer (`EventEnvelope` serialisierbar) + PID-Location-
Transparency reist ein Targeted-Event auch cross-node ans Ziel: die im Shard gespeicherte
`proxyPid` trägt die Adresse des Node, an dem der Client hängt; `context.Send(proxyPid, envelope)`
routet über die Leitung dorthin, der `EventProxyActor` schreibt in den Socket. Der Socket muss
**nicht** wandern — er bleibt, wo er ist, und das ist richtig.

**Leistet NICHT (Robustheit):** Die Subscription lebt **ausschließlich in-memory im Shard**
(`SubscriberRegistry` = `Dictionary<string, PID>`, `SubscriberRegistry.cs:10`;
`BrokerShardActor.cs:21`). Kein Dedup, keine Durabilität. Der `BrokerShardActor` ist ein
virtueller Cluster-Actor — **rebalanciert der Shard** (Node-Ausfall, Topologie-Änderung), ist
seine Registry weg und **alle Subscriptions dieses Shards sind verloren**.

- **Weg A** übersteht das folgenlos: der Poll-Backstop weckt den Adapter ohnehin alle 30 s, die
  Wahrheit kommt aus dem Log-Read (Invariante 2). Ein verlorenes Signal ist per Design egal.
- **Weg B** hat **kein** solches Netz: geht die Subscription mitten in einer lebenden Verbindung
  verloren, **fallen die Targeted-Events dieses Clients still aus**, bis der Client neu
  subscribed. Das passiert heute nur bei Re-Connect (`SubscriptionTracker` lebt im Scope der
  `Connect()`-Methode, `SubscriptionTracker.cs:15`, und subscribt beim Verbindungsaufbau) — also
  **erst wenn der Client die Verbindung neu aufbaut**, nicht automatisch.

Das ist der Kern: **Weg B verlässt sich heute auf die lokale, unbewegliche PID-im-Shard und hat
keinen Heilungspfad gegen Shard-Rebalance.** Single-node fällt das nie auf (ein Node, kein
Rebalance). Cross-node wird es zur echten, aber seltenen Lücke (nur bei Rebalance während einer
offenen Client-Verbindung).

## Warum der naive Fix (PID → ClusterIdentity) hier NICHT geht

Der offensichtliche Gedanke „speichere statt der PID eine `ClusterIdentity`, dann re-resolved die
Zustellung nach Rebalance" **zerstört Weg B**: der `EventProxyActor` kapselt einen **lebenden
Socket, der physisch an genau einem Node hängt**. Ein virtueller Cluster-Actor darf vom Cluster
auf einem *anderen* Node aktiviert werden — dort existiert der Socket nicht. Der Client-Receiver
lässt sich also **prinzipiell nicht virtualisieren**. Ein reiner `PID→ClusterIdentity`-Tausch am
`Subscribe`-Record würde den gesamten gRPC-Streaming-Layer mitreißen. (Weg A ließe sich
virtualisieren, Weg B nicht — die Asymmetrie ist der Grund, PID grundsätzlich beizubehalten.)

## ✅ Variante B (UMGESETZT): periodisches Re-Assert = das Poll-Äquivalent

Die eigentliche Lücke ist „Subscription verloren, kein Selbstheilungs-Netz". Genau dafür hat Weg A
den Poll. Also bekommt Weg B **denselben Mechanismus**: der socket-haltende Node **erneuert seine
Subscription periodisch** (Intervall 20 s). Rebalanciert ein Shard und verliert seine In-Memory-Liste,
füllt das nächste Re-Assert sie wieder — die Ziel-PID ist stets die aktuelle Proxy-PID.

```
Node X:  alle 20 s ──Subscribe(proxyPid) erneut──► Shard   (Poll-Äquivalent, Inv. 2/6)
```

**Kein neuer Grain, keine geteilte Karte, kein ClusterIdentity-Umbau.** Umgesetzt in:
- `Infrastructure/GrpcClient/SubscriptionTracker.cs` → `ReassertAllAsync` (re-sendet Subscribe für alle
  getrackten Typen; Snapshot unter Lock, Sends ohne Lock; Fehler nur geloggt).
- `Infrastructure/GrpcClient/CqrsClientService.cs` → `SubscriptionReassertLoopAsync`, gestartet je
  `Connect()`, an den Verbindungs-`ct` gekoppelt, gestoppt vor dem Tracker-Dispose.
- Test: `Infrastructure.Integration.Tests/ClientSubscriptionReassertTests.cs` — simuliert Shard-Verlust
  per direktem `Unsubscribe` am Shard und prüft, dass ein Re-Assert die Subscription wiederherstellt.

**Warum B statt A:** Variante A (Gateway-Kind) würde nur gebraucht, wenn die Subscription **unabhängig
vom Socket-Node** überleben müsste. Muss sie nicht: stirbt Node X, ist die Session sowieso tot (Socket
weg) → der Client verbindet neu und subscribed neu. Es gibt keinen Fall, den A löst und B nicht. B ist
das direkte Analogon zum bestehenden Poll-Backstop und passt zur Architektur (Inv. 2/6). Eine tote PID
(Node X weg) bleibt bis zum nächsten Rebalance als harmlose Leiche im Shard (Send ins Leere → Protos
eigenes Dead-Letter), selbst-aufräumend.

Rest-Kante: bis zu einem Re-Assert-Intervall (~20 s) nach einem Rebalance können Targeted-Events
ausfallen — exakt das Verlust-Fenster, das Weg A beim Poll auch hat. Bewusst so (verlierbarer schneller
Kanal).

---

## Variante A (schwerere Alternative, NICHT gebaut): node-adressierendes Gateway-Kind

Die Idee entkoppelt **zwei Dinge, die heute in einer PID verschmolzen sind**:
1. das **stabile, cluster-adressierbare Subscription-Ziel** (überlebt Rebalance, re-resolved), und
2. den **node-gebundenen Socket** (unbeweglich, bleibt wo der Client hängt).

**Bauteile:**

1. **`ClientSessionGateway` — virtuelles Cluster-Kind, `ClusterIdentity = sessionId`.**
   Kann überall aktivieren, ist von überall per `sessionId` resolvebar, übersteht Rebalance
   (re-aktiviert, liest seinen Zustand neu). Es hält **nicht** den Socket, sondern nur die
   **Weiterleitungs-Information**: „an welchem Node hängt der Socket dieser Session?".

2. **Session→Node-Karte (geteilt, abgeleitet).** Beim `Connect` registriert der Node
   `sessionId → nodeAddress` in einem geteilten, abgeleiteten Index (Redis — passt zum
   bestehenden „abgeleitet, nicht-autoritativ"-Muster von Version-Index/Deps-Index). Beim
   `Disconnect` deregistriert er. Der Gateway-Grain liest daraus.

3. **Node-lokale Session→Proxy-Registry.** Jeder Node hält `sessionId → lokale proxyPid` für die
   an *ihm* hängenden Sockets (das ist genau die heutige PID, nur node-lokal indexiert statt im
   Shard).

4. **Subscription per `ClusterIdentity(sessionId)` statt PID.** Der Shard speichert für Weg-B-
   Subscriber die **stabile Gateway-Identität**, nicht die flüchtige proxyPid.

**Zustell-Pfad neu:**
```
Publisher (targeted)                                             Node X (Socket lebt hier)
   │  ShardFor(type, sessionId)                                    ┌───────────────────────┐
   ▼                                                               │  EventProxyActor      │
BrokerShard ──Send/Request──► ClientSessionGateway(sessionId) ─────► (node-lokale proxyPid)│
 (speichert ClusterIdentity)   (virtuell, resolved sessionId→NodeX) │  → IServerStreamWriter│
                                                                    └───────────────────────┘
```
Der Shard adressiert ein **stabiles virtuelles Ziel**; der Gateway-Grain schlägt den Owner-Node
nach und leitet an dessen lokale Proxy-PID weiter. Rebalanciert der Shard, geht die Subscription
zwar immer noch verloren (in-memory) — aber jetzt kann eine **durable Re-Registrierung** greifen
(der Gateway-Grain kennt seine Abos aus der geteilten Karte und re-subscribt), statt auf den
Client-Re-Connect zu warten. Wandert der Node mit dem Socket weg, ist die Session ohnehin tot
(Client muss neu verbinden) — dann räumt die Karte auf.

**Was das löst:** (a) Subscription-Ziel überlebt Shard-Rebalance (stabile Identität +
Re-Registrierung); (b) der Socket bleibt korrekt node-gebunden; (c) kein Zwang, den
gRPC-Streaming-Layer zu virtualisieren.

## Alternativen (erwogen, verworfen)

- **Nur PID beibehalten, nichts weiter (= Iteration-2-Stand).** Funktioniert cross-node, aber die
  Rebalance-Lücke bleibt. Für viele Deployments akzeptabel (Rebalance ist selten, Clients
  reconnecten). **Das ist bewusst der Auslieferungsstand nach Iteration 2** — das Gateway ist die
  Härtung obendrauf, nicht die Voraussetzung.
- **Targeted-Events durabel machen (Outbox je Session).** Überdimensioniert: Client-Feedback ist
  per Design verlierbar (Invariante 6 — UI-Feedback bleibt auf dem schnellen Kanal). Ein durabler
  Store je Session widerspricht der Philosophie.
- **Poll-Backstop auch für Weg B.** Es gibt keinen Log/Cursor für „was hat Session X noch nicht
  gesehen" ohne genau die Session-Outbox von oben — dieselbe Ablehnung.

## Scope, Priorität, Auslöser

- **Priorität: niedrig / bedarfsgetrieben.** Die Lücke ist real, aber schmal (nur Shard-Rebalance
  *während* offener Client-Verbindungen) und heute durch Client-Re-Connect abgefedert.
- **Auslöser, es zu bauen:** ein Multi-Node-Deployment mit (a) langlebigen Client-Streams UND
  (b) beobachtetem Targeted-Event-Verlust bei Rebalance. Vorher nicht nötig.
- **Abhängigkeit:** setzt Iteration 2 (Wire-Serializer für den PubSub-Plane) voraus — ohne die
  serialisierten Envelopes reist gar nichts cross-node.
- **Nicht betroffen:** Weg A (Projektionen) — der ist mit Iteration 2 + Poll-Backstop fertig.

## Verweise

- Iteration-1-Serializer: `~/.claude/plans/spicy-wandering-kay.md`, `docs/backend-neubau-fahrplan.md`
  Phase 7/8.
- Weg-B-Code: `Infrastructure/GrpcClient/{EventProxyActor,SubscriptionTracker,CqrsClientService}.cs`.
- Broker: `Infrastructure/PubSub/{BrokerPublisher,BrokerSubscription,BrokerIdentity}.cs`,
  `PubSub/Actors/{BrokerManagerActor,BrokerShardActor}.cs`, `PubSub/SubscriberRegistry.cs`.
- Invarianten 2 & 6 (Signal verlierbar; Verlierbares bleibt auf dem schnellen Kanal): `CLAUDE.md`.
