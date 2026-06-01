using Abstractions;

namespace Domain.Counter;

// ═══════════════════════════════════════════════
// IEvent — persistiert im Stream + an alle verteilt
// ═══════════════════════════════════════════════

public record CounterErstellt(
    string Name,
    long? MaxGrenze,
    long? MinGrenze,
    long StartWert,
    DateTimeOffset ErstelltAm
) : IEvent;

public record CounterIncrementiert(
    long Delta,
    long NeuerWert,
    DateTimeOffset Zeitpunkt
) : IEvent;

public record CounterDekrementiert(
    long Delta,
    long NeuerWert,
    DateTimeOffset Zeitpunkt
) : IEvent;

// Audit-Signale: passieren beim Erreichen einer Grenze,
// brauchen aber keine State-Mutation (siehe Applier).
public record MaximumErreicht(long Wert, long MaxGrenze) : IEvent;
public record MinimumErreicht(long Wert, long MinGrenze) : IEvent;

public record MaxGrenzeGeaendert(long? MaxGrenze) : IEvent;
public record MinGrenzeGeaendert(long? MinGrenze) : IEvent;

public record CounterZurueckgesetzt(long VorherigerWert) : IEvent;

public record CounterGeloescht() : IEvent;

// ═══════════════════════════════════════════════
// ITransientEvent — Ablehnungen, NICHT persistiert
// Werden nur an den auslösenden Client als
// Targeted Delivery zurückgeschickt.
// ═══════════════════════════════════════════════

public record OberhalbMaximum(long Versuch, long MaxGrenze) : ITransientEvent;
public record UnterhalbMinimum(long Versuch, long MinGrenze) : ITransientEvent;
public record CounterNichtGefunden(Guid Id) : ITransientEvent;
public record CounterBereitsGeloescht(Guid Id) : ITransientEvent;
public record CounterEingabeUngueltig(string Feld, string Grund) : ITransientEvent;
