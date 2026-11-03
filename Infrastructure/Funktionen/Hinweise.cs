using Abstractions;

namespace Infrastructure.Funktionen;

// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════
// HINWEISE AUS GRAPHEN (docs/04 §4.4): ein Hinweis aus einem Knoten (oder eine gescheiterte Kette, LaufGescheitert) geht nur an die Akteure draußen, die
// ihn hören — über die Verteiler an deren Sitzungen. Funktionen hören keine Hinweise; was ein Knoten braucht, ist ein Command. Höchstens einmal: was im
// Flug ist, wenn ein Knoten stirbt, geht verloren (Invariante 6).
// ═══════════════════════════════════════════════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Ein Hinweis aus einem Graphen unterwegs zum Verteiler (verlierbar): die Kennung, die der Absender vergibt (A7), der Hinweis selbst und die Herkunft
/// seiner Kette — Stream, Version, Zeit, Korrelation, Akteur, Ursache.
/// </summary>
public sealed record HinweisZustellen(Guid Kennung, IEvent Hinweis, Guid Stream, int Version, DateTimeOffset Zeit, string? Korrelation, string? Akteur,
    string? Ursache = null);
