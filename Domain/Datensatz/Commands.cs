using Abstractions;

namespace Domain.Datensatz;

// ═══════════════════════════════════════════════════
// LIFECYCLE
// ═══════════════════════════════════════════════════

public record ErstelleDatensatz(
    Id<Datensatz> AggregateId,
    string Name
) : ICommand<Datensatz>, ICreationCommand;

// ═══════════════════════════════════════════════════
// RANGE — Variante A (zweiphasig)
//   GUI ──FuegeRangeHinzu(kriterien)──► RangeAngefordert
//        └─► server-seitiger Resolver löst auf ──NimmRangeAuf(ids, herkunft)──► PaareAufgenommen
// Der Nutzer fasst nie IDs an — er denkt in Suchen.
// ═══════════════════════════════════════════════════

/// <summary>GUI-Command: eine ganze Such-Range in den Datensatz aufnehmen.</summary>
public record FuegeRangeHinzu(
    Id<Datensatz> AggregateId,
    RangeKriterien Kriterien
) : ICommand<Datensatz>;

/// <summary>
/// Resolver-Command: die aufgelöste Range (konkrete IDs) aufnehmen (Union, dedupliziert).
/// Wird vom server-seitigen Resolver nach SearchAsync ausgelöst, nie von der GUI.
/// </summary>
public record NimmRangeAuf(
    Id<Datensatz> AggregateId,
    IReadOnlyList<Id<ImagePair.ImagePair>> ImagePairIds,
    RangeHerkunft Herkunft
) : ICommand<Datensatz>;

// ═══════════════════════════════════════════════════
// MANUELLES DELTA
// ═══════════════════════════════════════════════════

public record NimmPaarAuf(
    Id<Datensatz> AggregateId,
    Id<ImagePair.ImagePair> ImagePairId
) : ICommand<Datensatz>;

public record EntfernePaar(
    Id<Datensatz> AggregateId,
    Id<ImagePair.ImagePair> ImagePairId
) : ICommand<Datensatz>;

// ═══════════════════════════════════════════════════
// SPLIT — optionaler Override (Default 70/15/15)
// ═══════════════════════════════════════════════════

public record SetzeSplit(
    Id<Datensatz> AggregateId,
    int TrainProzent,
    int ValProzent,
    int TestProzent,
    int Seed
) : ICommand<Datensatz>;

// ═══════════════════════════════════════════════════
// EINFRIEREN — zweiphasig
//   GUI ──FriereEin──► EinfrierenAngefordert
//        └─► Resolver snapshottet Label-Stand + Split ──SchliesseEinfrierenAb(mitglieder)──► DatensatzEingefroren
// ═══════════════════════════════════════════════════

/// <summary>GUI-Command: das Einfrieren anstoßen.</summary>
public record FriereEin(
    Id<Datensatz> AggregateId
) : ICommand<Datensatz>;

/// <summary>
/// Resolver-Command: das Einfrieren abschließen mit dem aufgelösten Mitglieder-Snapshot
/// (Label-Stand + Split je Mitglied). Wird vom Resolver ausgelöst, nie von der GUI.
/// </summary>
public record SchliesseEinfrierenAb(
    Id<Datensatz> AggregateId,
    IReadOnlyList<DatensatzMitglied> Mitglieder
) : ICommand<Datensatz>;
