using Abstractions;

namespace Domain.Datensatz;

// ═══════════════════════════════════════════════════
// ERFOLGS-EVENTS (IEvent) — werden persistiert + publiziert
// ═══════════════════════════════════════════════════

public record DatensatzErstellt(
    string Name
) : IEvent;

/// <summary>
/// Marker: eine Range wurde angefordert. Trägt die Kriterien als Provenienz.
/// Der server-seitige Resolver reagiert darauf (SearchAsync → NimmRangeAuf).
/// </summary>
public record RangeAngefordert(
    RangeKriterien Kriterien
) : IEvent;

/// <summary>
/// Die aufgelöste Range wurde in den Entwurf aufgenommen (Union, dedupliziert).
/// </summary>
public record PaareAufgenommen(
    IReadOnlyList<Guid> ImagePairIds,
    RangeHerkunft Herkunft
) : IEvent;

public record PaarAufgenommen(
    Guid ImagePairId
) : IEvent;

public record PaarEntfernt(
    Guid ImagePairId
) : IEvent;

public record SplitGesetzt(
    int TrainProzent,
    int ValProzent,
    int TestProzent,
    int Seed
) : IEvent;

/// <summary>
/// Das Einfrieren wurde angefordert. Trägt die <em>autoritative</em> Mitgliedschaft +
/// Split-Konfig aus dem Aggregat-State (Invariante 1: nicht aus dem abgeleiteten Read-Model
/// lesen). Der Resolver liest daraufhin nur noch den Label-Stand + Pfade je Mitglied aus
/// dem ImagePair-Read-Store, teilt den Split zu und schließt mit SchliesseEinfrierenAb ab.
/// </summary>
public record EinfrierenAngefordert(
    IReadOnlyList<Guid> Mitglieder,
    SplitKonfig Split
) : IEvent;

/// <summary>
/// Der Datensatz ist eingefroren: Mitgliedschaft + Label-Stand + Split festgeschrieben,
/// Version vergeben. Danach immutabel. Der immutable Snapshot liegt hiermit im
/// Postgres-Event-Store — kein File-Dump (Konzept §3.1).
/// </summary>
public record DatensatzEingefroren(
    int Version,
    IReadOnlyList<DatensatzMitglied> Mitglieder
) : IEvent;

// ═══════════════════════════════════════════════════
// ABLEHNUNGS-EVENTS (ITransientEvent) — nicht persistiert
// ═══════════════════════════════════════════════════

public record DatensatzExistiertBereits(Guid DatensatzId) : ITransientEvent;
public record DatensatzBereitsEingefroren(Guid DatensatzId) : ITransientEvent;
public record DatensatzLeer(Guid DatensatzId) : ITransientEvent;
public record RangeLeer(Guid DatensatzId) : ITransientEvent;
public record SplitUngueltig(int TrainProzent, int ValProzent, int TestProzent) : ITransientEvent;
