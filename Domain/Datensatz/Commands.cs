using Abstractions;

namespace Domain.Datensatz;

// ═══════════════════════════════════════════════════
// LIFECYCLE
// ═══════════════════════════════════════════════════

public record ErstelleDatensatz(
    Guid AggregateId,
    string Name
) : ICreationCommand;

// ═══════════════════════════════════════════════════
// RANGE — Variante A (zweiphasig, Konzept §3.2)
//   GUI ──FuegeRangeHinzu(kriterien)──► RangeAngefordert
//        └─► server-seitiger Resolver löst auf ──NimmRangeAuf(ids, herkunft)──► PaareAufgenommen
// Der Nutzer fasst nie IDs an — er denkt in Suchen.
// ═══════════════════════════════════════════════════

/// <summary>GUI-Command: eine ganze Such-Range in den Datensatz aufnehmen.</summary>
public record FuegeRangeHinzu(
    Guid AggregateId,
    RangeKriterien Kriterien
) : ICommand;

/// <summary>
/// Resolver-Command: die aufgelöste Range (konkrete IDs) aufnehmen (Union, dedupliziert).
/// Wird vom server-seitigen Resolver nach SearchAsync ausgelöst, nie von der GUI.
/// </summary>
public record NimmRangeAuf(
    Guid AggregateId,
    IReadOnlyList<Guid> ImagePairIds,
    RangeHerkunft Herkunft
) : ICommand;

// ═══════════════════════════════════════════════════
// MANUELLES DELTA
// ═══════════════════════════════════════════════════

public record NimmPaarAuf(
    Guid AggregateId,
    Guid ImagePairId
) : ICommand;

public record EntfernePaar(
    Guid AggregateId,
    Guid ImagePairId
) : ICommand;

// ═══════════════════════════════════════════════════
// SPLIT — optionaler Override (Default 70/15/15)
// ═══════════════════════════════════════════════════

public record SetzeSplit(
    Guid AggregateId,
    int TrainProzent,
    int ValProzent,
    int TestProzent,
    int Seed
) : ICommand;

// ═══════════════════════════════════════════════════
// EINFRIEREN — zweiphasig (Konzept §3.1)
//   GUI ──FriereEin──► EinfrierenAngefordert
//        └─► Resolver snapshottet Label-Stand + Split ──SchliesseEinfrierenAb(mitglieder)──► DatensatzEingefroren
// ═══════════════════════════════════════════════════

/// <summary>GUI-Command: das Einfrieren anstoßen.</summary>
public record FriereEin(
    Guid AggregateId
) : ICommand;

/// <summary>
/// Resolver-Command: das Einfrieren abschließen mit dem aufgelösten Mitglieder-Snapshot
/// (Label-Stand + Split je Mitglied). Wird vom Resolver ausgelöst, nie von der GUI.
/// </summary>
public record SchliesseEinfrierenAb(
    Guid AggregateId,
    IReadOnlyList<DatensatzMitglied> Mitglieder
) : ICommand;
