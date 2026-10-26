using Abstractions;

namespace Domain.Projections.Trainingslauf;

/// <summary>Ein Trainingslauf mit Live-Kurve (MetrikHistorie) — Dashboard-Detail.</summary>
public record HoleTrainingslauf(Id<Domain.Trainingslauf.Trainingslauf> TrainingslaufId) : IQuery;

/// <summary>Alle Trainingsläufe (Sidebar-Liste mit Status-Badges).</summary>
public record HoleTrainingslaeufe() : IQuery;
