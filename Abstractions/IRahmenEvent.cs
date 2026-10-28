namespace Abstractions;

/// <summary>
/// Ein Event des RAHMENS (Frameworks) im Stream eines Aggregats — kein Fakt der Domäne: <see cref="FristGesetzt"/> (die Uhr). Noop und Ablehnung
/// stehen nicht im Log, nur in der Inbox-Tabelle (Plan Pub/Sub-Neustrukturierung Phase 5). Es zählt in der Version des Streams (der
/// Tail-Read bleibt bündig), der Applier sieht es nie (beim Falten des Zustands übersprungen, live wie bei der Rehydration), und es geht nicht über
/// die Leitung (kein Proto, kein Client hört es).
/// </summary>
public interface IRahmenEvent { }
