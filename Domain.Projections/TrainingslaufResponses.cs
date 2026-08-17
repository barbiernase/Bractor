using Abstractions;
using Domain.Trainingslauf;

namespace Domain.Projections;

/// <remarks>
/// <see cref="Hyperparameter"/> und <see cref="Endmetriken"/> sind bewusst NICHT nullable:
/// der Proto-/DtoMapper-Generator bildet ein nullable Komplex-VO (<c>T?</c>) fälschlich als
/// <c>string</c> ab (bekannte Schuld). Nicht-nullable Komplex-VOs funktionieren. Endmetriken
/// ist erst bei <see cref="TrainingsStatus.Abgeschlossen"/> aussagekräftig — davor Default (0/0);
/// der Client wertet sie über den Status.
/// </remarks>
public record TrainingslaufAntwort(
    Guid Id,
    Guid DatensatzId,
    int DatensatzVersion,
    TrainingsStatus Status,
    int AktuelleEpoche,
    int GesamtEpochen,
    Hyperparameter Hyperparameter,
    IReadOnlyList<EpochenMetrik> MetrikHistorie,
    string? ModellPfad,
    Endmetriken Endmetriken,
    string? Fehlergrund,
    DateTimeOffset Startzeit
) : IQueryResponse;

public record TrainingslaufNichtGefundenAntwort(Guid TrainingslaufId) : IQueryResponse;

public record TrainingslaufListe(
    IReadOnlyList<TrainingslaufAntwort> Items
) : IQueryResponse;
