using Abstractions;
using Domain.Trainingslauf;

namespace Domain.Projections.Trainingslauf;

/// <summary>
/// Materialisierter Zustand eines Trainingslaufs. Ein Doc pro Lauf; die
/// <see cref="MetrikHistorie"/> wächst je Epoche (Live-Kurve) — append-artig, deshalb
/// braucht die Projektion einen Store der Klasse 1 oder 2 (genau einmal).
/// </summary>
public partial record TrainingslaufReadModel : IReadModel
{
    public Id<Domain.Trainingslauf.Trainingslauf> Id { get; init; }
    public Id<Domain.Datensatz.Datensatz> DatensatzId { get; init; }
    public int DatensatzVersion { get; init; }

    public TrainingsStatus Status { get; init; }

    public int AktuelleEpoche { get; init; }
    public int GesamtEpochen { get; init; }

    public Hyperparameter? Hyperparameter { get; init; }
    public List<EpochenMetrik> MetrikHistorie { get; init; } = new();

    public string? ModellPfad { get; init; }
    public Endmetriken? Endmetriken { get; init; }
    public string? Fehlergrund { get; init; }

    /// <summary>Zeitpunkt des tatsächlichen Beginns (TrainingBegonnen); default = noch nicht begonnen.</summary>
    public DateTimeOffset Startzeit { get; init; }
    public DateTimeOffset LetzteAktualisierung { get; init; }
}
