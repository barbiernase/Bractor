using System;
using Abstractions;

namespace Infrastructure.Prozess;

/// <summary>
/// Das GENERISCHE Prozess-Manager-Log (Spec §4/§5.3). Der Manager ist EIN Aggregat für ALLE Prozess-Typen —
/// die Struktur (Regeln) ist Daten, nicht Code pro Prozess. Sein Log speichert bewusst nur ENTSCHEIDUNGEN,
/// die sich NICHT aus den Ziel-Streams rekonstruieren lassen:
///   - <see cref="ProzessGestartet"/>: welcher Prozess-Typ + wo der Auslöser liegt (Marking-Wurzel).
///   - <see cref="SchrittGescheitert"/>: eine Ziel-Ablehnung ist transient (nicht im Ziel-Log) — der Manager
///     macht sie durabel, sobald er die negative Quittung sieht (§5.2), sonst kann er nicht kompensieren.
///   - <see cref="ProzessBeendet"/>: Terminal-Marke (aus dem Marking ableitbar, aber als Halt-/Beobachtungs-Fakt).
///
/// Alles Übrige — welche Transitionen gefeuert/erfolgreich sind — FALTET der Manager bei jeder Weckung aus den
/// autoritativen Ziel-Streams (Invariante 1), nie aus einem Feld. Diese Typen sind <see cref="IProzessIntern"/>
/// (proto-frei, nur Event-Store/intern).
/// </summary>
public sealed record ProzessGestartet(string ProzessName, Guid AuslöserStream, int AuslöserVersion)
    : IEvent, IProzessIntern;

/// <summary>Eine gefeuerte Transition wurde vom Ziel abgelehnt (durabel gemacht → löst Kompensation aus).</summary>
public sealed record SchrittGescheitert(Guid Vorgang, string Grund) : IEvent, IProzessIntern;

/// <summary>
/// Der Prozess ist terminal. <paramref name="Erfolg"/> = alle Transitionen erledigt; sonst
/// kompensiert/fehlgeschlagen. <paramref name="KlärungNötig"/> (Audit-Fix #12) markiert den dritten
/// Ausgang: ein Schritt scheiterte UND die Kompensation ließ sich NICHT vollziehen (der Gegenzug wurde
/// selbst abgelehnt) → der Prozess steckt halb-kompensiert fest, es gibt keinen sauberen Rollback. Statt
/// den Gegenzug endlos neu zu feuern (Kompensations-Livelock) hält der Manager hier terminal an; ein
/// Mensch/anderer Prozess muss auflösen (beobachtbar zusätzlich über die DLQ).
/// </summary>
public sealed record ProzessBeendet(bool Erfolg, string Grund, bool KlärungNötig = false) : IEvent, IProzessIntern;

// ── Signale (StateChangeVia{Event}) für die Manager-Log-Events ──
// Das Framework hält die Invariante „ein Signal pro persistierbarem Event" (auch für IProzessIntern-Events,
// die der TypeRegistryGenerator symbol-basiert mitzählt). Der Manager weckt sich zwar über den
// Korrelations-Router (Ziel-Events), nicht über sein EIGENES Log — diese Signale sind deshalb inert
// (der Manager appendet direkt am Store, nicht über AggregateActorBase, das die Signale emittieren würde).
// Sie existieren nur für die Registry-Konsistenz (wie die generierten Signale der alten Prozess-Events).
public sealed record StateChangeViaProzessGestartet(Guid StreamId, int Version) : IStateChangeSignal;
public sealed record StateChangeViaSchrittGescheitert(Guid StreamId, int Version) : IStateChangeSignal;
public sealed record StateChangeViaProzessBeendet(Guid StreamId, int Version) : IStateChangeSignal;
