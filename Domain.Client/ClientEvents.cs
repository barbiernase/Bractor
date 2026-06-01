// ── Domain.Client/ImagePair/ClientEvents.cs ──

using Client.Infrastructure.Abstractions;
using Domain.ImagePair;

namespace Domain.Client.ImagePair;

/// <summary>
/// Drei explizite Arbeitsmodi statt kombinierter Booleans.
/// </summary>
public enum ArbeitsModus { Live, Inspekt, Tag }

// ═══════════════════════════════════════════════════
// STATISTIK
// ═══════════════════════════════════════════════════

public record ImagePairStatistikBerechnet(
    int AnzahlGesamt,
    int AnzahlKomplett,
    int AnzahlMitKiKlassifikation,
    int AnzahlOhneKlassifikation,
    int AnzahlAnomalie
) : IClientEvent;

// ═══════════════════════════════════════════════════
// NAVIGATION
// ═══════════════════════════════════════════════════

public record ImagePairAusgewaehlt(Guid PairId) : IClientEvent;

/// <summary>
/// Setzt _pendingIndex im NavigationStore.
///   0 = erstes Item, -1 = letztes Item.
/// </summary>
public record NavigationZielGesetzt(int Index) : IClientEvent;

/// <summary>
/// Seitenwechsel angefordert.
/// ListRefreshHandler baut den Filter und yieldet SucheImagePairs.
/// </summary>
public record SeiteAngefordert(int Seite) : IClientEvent;

public record ModusGeaendert(ArbeitsModus Modus) : IClientEvent;

// ═══════════════════════════════════════════════════
// FILTER
// ═══════════════════════════════════════════════════

public record FilterWertGeaendert(string Wert) : IClientEvent;

public record InspektionsfilterGetoggelt() : IClientEvent;

// ═══════════════════════════════════════════════════
// TAG-LABELING
// ═══════════════════════════════════════════════════

public record TagLabelingGestartet(DateTimeOffset Datum) : IClientEvent;

public record TagLabelingBeendet() : IClientEvent;

// ═══════════════════════════════════════════════════
// CHART
// ═══════════════════════════════════════════════════

public record TagGewaehlt(DateTimeOffset? Datum) : IClientEvent;
