namespace Abstractions;

/// <summary>
/// Marker für generierte Signal-Typen `StateChangeVia{Event}` (Spec 5.1).
///
/// Ein Signal ist NUR ein Weckruf: es trägt ausschließlich `(StreamId, Version)`,
/// keine Nutzlast. Reihenfolge und Exactly-once entstehen aus dem Log-Read des
/// Adapters, nie aus dem Signal — deshalb darf es verloren, doppelt oder ungeordnet
/// ankommen (Invariante 2).
///
/// WARUM ein eigener Marker (statt bare IMessagePayload): Er gibt dem
/// TypeRegistryGenerator eine eigene Kategorie („Signals") und hält das Routing
/// rein typbasiert (Invariante 3) — es wird nie ein Identitäts-String gebaut.
/// IStateChangeSignal erbt von IMessagePayload, damit die Signale durch dasselbe
/// typ-geshardete PubSub laufen wie Events/Commands und cross-node serialisierbar sind.
///
/// Pro persistiertem Event-Typ wird genau ein Signal-Typ generiert (nicht für
/// ITransientEvent — die stehen nicht im Log, also gibt es nichts nachzulesen).
/// </summary>
public interface IStateChangeSignal : IMessagePayload
{
    /// <summary>Der Stream (AggregateId), in dem sich etwas geändert hat.</summary>
    Guid StreamId { get; }

    /// <summary>Die Position des auslösenden Events in seinem Stream.</summary>
    int Version { get; }
}

/// <summary>
/// ★ P1b (TG-1): der typisierte Marker, der die Event→Signal-Kante <b>am Typ</b> trägt — nicht mehr am
/// Namens-Präfix <c>StateChangeVia{Event}</c>. Das generierte <c>StateChangeVia{X}</c> implementiert
/// <c>IStateChangeSignal&lt;X&gt;</c>; der <c>SignalFactoryGenerator</c> liest das Typ-Argument <c>TEvent</c>
/// und paart Event↔Signal signaturgetrieben (kein String-Strip, kein Namespace-Namenslookup). Der Name des
/// Signal-Typs bleibt lesbar, ist aber nicht mehr die Ableitungsquelle.
/// </summary>
public interface IStateChangeSignal<TEvent> : IStateChangeSignal where TEvent : IEvent
{
}
