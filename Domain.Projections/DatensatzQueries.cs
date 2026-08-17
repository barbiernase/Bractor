using Abstractions;

namespace Domain.Projections;

/// <summary>
/// Die eingefrorenen Samples einer Datensatz-Version — paginiert (Konzept §6).
/// Derselbe typisierte Query-Kanal für Python (TrainingWorker) und Blazor (Vorschau):
/// eine Deklaration hier, per Proto-Regen sehen sie beide Clients.
/// Liest den <em>immutablen</em> Snapshot → reproduzierbar.
/// </summary>
public record HoleDatensatzSamples(
    Guid DatensatzId,
    int Version,
    int Seite = 1,
    int SeitenGroesse = 500
) : IQuery;

/// <summary>Ein einzelner Datensatz (Kopf-Daten) — für GUI-Detail und Integrations-Prüfung.</summary>
public record HoleDatensatz(Guid DatensatzId) : IQuery;
