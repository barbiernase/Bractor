# Multi-Node — interner Plane-Serializer (Konzept + Stand)

> Schließt den größten strukturell offenen Block (P4c/P4d der Backend-Analyse): Cross-Node.
> Stand dieses Dokuments: Phasen 1–4 geliefert und grün (Prüfstand 123/123). Weg **A**
> (JSON-Poly-Serializer) gewählt. Verbleibend: Phase 5–6 (Docker-Multi-Node + Failover + Doku),
> bewusst zurückgestellt — siehe unten.

## Das Problem (code-verifiziert)

Der Cluster war für Multi-Node bereits **verdrahtet** (Consul-Provider, gRPC-Remote auf `0.0.0.0`,
`AdvertisedHost`, alle Kinds registriert) — ihm fehlte **eine** Sache: bei Proto.Remote war **kein
Serializer für die interne Ebene** registriert (`WithRemote(GrpcNetRemoteConfig…)` ohne
`WithSerializer`). Die internen Nachrichten sind rohe CLR-Records; ohne Serializer überleben sie nur
in-process → **de-facto single-node**. Weil Single-Node Serialisierung nie ausübt (lokale
Aktivierungen serialisieren nicht), war die Lücke **still**.

Der externe Plane (Client↔Server über die gehandschriebene `CqrsClientService`-gRPC) ist davon
unberührt — der nutzt die ProtoRepo-DTOs + `ProtoMessageMapper` und war schon immer verdrahtet.

## Die Node-übergreifende Nachrichtenmenge

Über `cluster.RequestAsync` / Broker-`Send` reisen: `CommandEnvelope`→`CommandResult`,
`Wake`/`ProzessWake`→`WakeAck`, `IPipelineTrigger`→`PipelineAck`, `Publish(EventEnvelope|
SignalEnvelope)`→`Ack`, `Subscribe`(enthält Proto-`PID`)/`Unsubscribe`/`Activate`→`Ack`,
`GetSubscriberCount`→`SubscriberCountResponse`, plus die Broker→Subscriber-`Send`s von
`EventEnvelope`/`SignalEnvelope`. Verschachtelt polymorph: `ICommand`, `IEvent`,
`IStateChangeSignal`, `CommandModus` (Union), `PID`.

## Weg A — generierter JSON-Poly-Serializer

Gewählt gegen Weg B (Protobuf-DTOs intern), weil: baut auf dem bestehenden, bewiesenen
reflection-freien `EventJsonSerializerContext` auf; meidet den als fragil dokumentierten
`DtoMapperGenerator`; die interne Ebene braucht kein Cross-Language (beide Node-Enden sind dasselbe
Binary); Serialisierung ist ohnehin nur beim Node-Übergang, nie im Single-Node-Hot-Path
(Schreibpfad ist DB-gebunden, Serialisierung ≈ 0,04 %).

**Die Typ-Routing-Achse ist generiert** (Invariante 3/4): `InternalPlaneTypen` speist seine
Diskriminator-Registry (FullName↔Type) aus dem generierten `GeneratedTypeRegistry`
(`Commands`/`Events`/`Signals`/`Triggers` — offene Domänen-Menge, wächst ohne Handanlegen) plus einer
festen `FrameworkTypen`-Liste (geschlossene Transport-/Broker-Menge). Das reine Feld-Marshalling
besorgt STJ: Events über den Source-Gen-Kontext (reflection-frei), Framework-Records über den
Reflection-Resolver.

Disjunkt zum Protobuf-Serializer (Id 0): `CanSerialize` lehnt jedes `IMessage` ab → Protos eigene
Cluster-Kontrollnachrichten (`ActivationRequest` & Co.) bleiben bei Serializer 0. Unsere POCOs
laufen über Serializer-Id 2.

### Bausteine
- `Infrastructure/Serialization/InternalPlaneTypen.cs` — Diskriminator-Registry + `FrameworkTypen`.
- `Infrastructure/Serialization/InternalPlaneSerializer.cs` — `ISerializer` (Id 2, Prio 100) + STJ-Options
  + Poly-Converter (`ICommand`/`IEvent`/`IStateChangeSignal`/`IMessageEnvelope`/`IMessagePayload`) +
  `CommandModus`- und `PID`-Converter.
- `Infrastructure/Serialization/InternalPlaneCoverage.cs` — Boot-Guard (`PruefeOderWirf`), Fail-Fast bei
  Abdeckungslücke (Stil `ProzessAzyklizität`/`GaEinsPruefung`).
- Registrierung: `CqrsServiceExtension.AddCqrsActorSystem` — `remoteConfig.Serialization.RegisterSerializer`
  vor `WithRemote`, plus Boot-Check.

## Was bewiesen ist (Prüfstand 123/123)

- **Round-Trip je Nachrichtentyp** über den echten `ISerializer`-Pfad (Serialize→Bytes→typeName→
  Deserialize), inkl. Registry-Coverage über alle generierten Domänentypen — `InternalPlaneSerializerTests`.
- **Naht zum Dispatcher**: Proto.Remotes `Serialization` routet interne POCOs auf Serializer 2 und
  verlustfrei zurück; Protobuf/`PID` bleibt bei Serializer 0 — `InternalPlaneRegistrationTests`.
- **Echter Cross-Node-Hop**: zwei ActorSystems in einem Prozess, je eigener gRPC-Port, über
  `TestProvider` zusammengeführt (kein Consul/Postgres). Ein `CommandEnvelope` reist von Node B über
  die echte gRPC-Grenze nach Node A und das `CommandResult` zurück — verlustfrei, mind. ein
  nachgewiesener Remote-Hop — `ZweiNodeSerialisierungTests`. Stabil über mehrere Läufe (~2 s).

## Bewusst offen (Phase 5–6)

- **Docker-Multi-Node + Failover** (Phase 5): Dockerfile für `Host.Grpc`, Compose mit 2–3 Node-Services
  gegen dasselbe Consul/Postgres/Redis, Last über beide Nodes verteilt (Exactly-once unter Split-Last mit
  echter Persistenz), Node-Kill → Aktivierungen wandern → weiter korrekt. Braucht ziehbare
  Container-Images + laufende Infra.
- **Doku-Abschluss** (Phase 6): `architektur/`-Referenz + CLAUDE.md von „de-facto single-node" auf
  „Multi-Node (Serialisierungs-Tor offen)" umstellen; P4c/P4d schließen.

### Umgebungs-Hinweis (warum P5 hier nicht lief)
Die Entwicklungs-Session hatte **kein .NET-SDK vorinstalliert** (per apt `dotnet-sdk-10.0` nachgezogen;
net9.0 baut/testet darauf via `DOTNET_ROLL_FORWARD=LatestMajor`). Docker-Daemon ließ sich starten, aber
**Docker-Hub-Image-Layer sind durch die Egress-Policy blockiert** (403 vom CDN-Blob-Host) → Postgres/
Consul/Redis nicht ziehbar. Deshalb liefen die 33 Integrationstests und ein persistenz-echter Last-/
Failover-Test hier **nicht**. Der Cross-Node-Serialisierungsbeweis (das eigentliche Tor) ist bewusst
infra-frei geführt und daher voll erbracht. Single-Node bleibt strukturell unberührt (in-process wird
nie serialisiert), daher ist keine Single-Node-Regression zu erwarten — auf Infra gegenzuprüfen, sobald
Images verfügbar sind.
