using System;
using Abstractions;

namespace Infrastructure.Aggregate;

/// <summary>
/// Die interne INBOX-Marke der Framework-Idempotenz (docs/04 §4.6): der
/// Aggregat-Actor co-committet sie MIT den Domänen-Events, wenn er einen Command mit deterministischer
/// <c>CommandId</c> verarbeitet (der <c>AnyVersion</c>-Pfad = Funktion, Uhr). Beim nächsten Eintreffen
/// desselben Commands (at-least-once) erkennt der Actor die <c>CommandId</c> und verpufft ihn (Noop) —
/// exactly-once-wirksam, OHNE dass der Fachcode eine Dedup-Zeile trägt.
///
/// <see cref="IRahmenEvent"/>: proto-frei; beim Falten des DOMÄNEN-States wird sie übersprungen (der
/// Applier sieht sie nie), aber in der Version mitgezählt (Stream-Position bleibt konsistent).
/// </summary>
public sealed record KommandoVerarbeitet(Guid CommandId) : IEvent, IRahmenEvent;

/// <summary>Inertes Signal (Registry-Invariante „ein Signal je persistierbarem Event"; wird nie emittiert).</summary>
public sealed record StateChangeViaKommandoVerarbeitet(Guid StreamId, int Version) : IStateChangeSignal;

/// <summary>
/// Die interne ABLEHNUNGS-Marke der Framework-Idempotenz: der Aggregat-Actor co-committet sie MIT nichts (kein Domänen-Effekt), wenn er
/// einen EMITTIERTEN Command (Funktion, Uhr) fachlich ablehnt (<see cref="ITransientEvent"/>). Sie macht die Ablehnung DURABEL auf dem
/// Ziel-Stream: eine Wiederholung derselben CommandId bekommt dieselbe Ablehnung (Zwei-Mengen-Inbox), der Sender sieht „abgelehnt“ als
/// entschieden (<see cref="EmitAusgang.Abgelehnt"/>) und die Spur zeigt den <see cref="Grund"/>. Ob sie bleibt oder einer anderen Form weicht,
/// entscheidet Plan Pub/Sub-Neustrukturierung Phase 5 (Marken).
///
/// <see cref="IRahmenEvent"/>: proto-frei; beim Falten des DOMÄNEN-States übersprungen (der Applier sieht sie
/// nie), aber in der Version mitgezählt (Stream-Position bleibt konsistent) — exakt wie <see cref="KommandoVerarbeitet"/>.
/// </summary>
public sealed record KommandoAbgelehnt(Guid CommandId, string Grund) : IEvent, IRahmenEvent;

/// <summary>Inertes Signal (Registry-Invariante „ein Signal je persistierbarem Event"; wird nie emittiert).</summary>
public sealed record StateChangeViaKommandoAbgelehnt(Guid StreamId, int Version) : IStateChangeSignal;
