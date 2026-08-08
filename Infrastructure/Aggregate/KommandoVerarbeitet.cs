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

/// <summary>
/// Die interne ABLEHNUNGS-Marke der Framework-Idempotenz (Treiber-Fold, EM-1): der Aggregat-Actor co-committet
/// sie MIT nichts (kein Domänen-Effekt), wenn er einen EMITTIERTEN Command (Reaktion/Prozess) fachlich ablehnt
/// (<see cref="ITransientEvent"/>). Sie macht eine Ablehnung DURABEL auf dem Ziel-Stream — der Prozess-Manager
/// faltet sie als eigene Achse (<c>AbgelehntDa</c>) zu <c>SchrittGescheitert</c>, ohne die Ziel-Quittung zu
/// brauchen (der EINE Emit-Weg). Der <see cref="Grund"/> reist mit, damit der Fold ihn in den Fehlschlag stempelt.
///
/// Die Kopplung (Backend-Handoff §4): Marke + Zwei-Mengen-Inbox + <c>AbgelehntDa</c>-Fold-Achse gehören ZUSAMMEN.
/// Fehlte die Fold-Achse, läse der Manager den Marker als „aufgelöst" (ErgebnisDa) und schriebe im
/// quittungs-verlorenen Fall fälschlich <c>ProzessBeendet(true)</c> (stiller Falsch-Erfolg).
///
/// <see cref="IProzessIntern"/>: proto-frei; beim Falten des DOMÄNEN-States übersprungen (der Applier sieht sie
/// nie), aber in der Version mitgezählt (Stream-Position bleibt konsistent) — exakt wie <see cref="KommandoVerarbeitet"/>.
/// </summary>
public sealed record KommandoAbgelehnt(Guid CommandId, string Grund) : IEvent, IProzessIntern;

/// <summary>Inertes Signal (Registry-Invariante „ein Signal je persistierbarem Event"; wird nie emittiert).</summary>
public sealed record StateChangeViaKommandoAbgelehnt(Guid StreamId, int Version) : IStateChangeSignal;
