using System;
using Abstractions;

namespace Infrastructure.Aggregate;

/// <summary>
/// Die interne INBOX-Marke der Framework-Idempotenz (Spec „Exactly-once — die ehrliche Aussage"): der
/// Aggregat-Actor co-committet sie MIT den Domänen-Events, wenn er einen Command mit deterministischer
/// <c>CommandId</c> verarbeitet (der <c>AnyVersion</c>-Pfad = Reaktion/Prozess). Beim nächsten Eintreffen
/// desselben Commands (at-least-once) erkennt der Actor die <c>CommandId</c> und verpufft ihn (Noop) —
/// exactly-once-wirksam, OHNE dass der Fachcode eine Dedup-Zeile trägt.
///
/// <see cref="IProzessIntern"/>: proto-frei; beim Falten des DOMÄNEN-States wird sie übersprungen (der
/// Applier sieht sie nie), aber in der Version mitgezählt (Stream-Position bleibt konsistent).
/// </summary>
public sealed record KommandoVerarbeitet(Guid CommandId) : IEvent, IProzessIntern;

/// <summary>Inertes Signal (Registry-Invariante „ein Signal je persistierbarem Event"; wird nie emittiert).</summary>
public sealed record StateChangeViaKommandoVerarbeitet(Guid StreamId, int Version) : IStateChangeSignal;
