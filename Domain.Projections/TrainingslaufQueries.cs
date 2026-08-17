using Abstractions;

namespace Domain.Projections;

/// <summary>Ein Trainingslauf mit Live-Kurve (MetrikHistorie) — Dashboard-Detail.</summary>
public record HoleTrainingslauf(Guid TrainingslaufId) : IQuery;

/// <summary>Alle Trainingsläufe (Sidebar-Liste mit Status-Badges).</summary>
public record HoleTrainingslaeufe() : IQuery;
