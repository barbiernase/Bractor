using Abstractions;
using Domain.Trainingslauf;

namespace Domain.Projections.Trainingslauf;

/// <summary>
/// Reader der Trainingslauf-Projektion (analog <see cref="ImagePairReader"/>). Beantwortet die
/// Trainings-Queries über Lese-Fähigkeiten des <see cref="ITrainingslaufStore"/> — derselbe Kanal für Blazor
/// (Live-Dashboard) und Python.
/// </summary>
public sealed class TrainingslaufReader : IReader
{
    public async Task<OneOf<TrainingslaufAntwort, TrainingslaufNichtGefundenAntwort>> Frage(
        HoleTrainingslauf query, IFindTrainingslauf findTrainingslauf)
    {
        var model = await findTrainingslauf.FindByIdAsync(query.TrainingslaufId);

        if (model is null)
            return new TrainingslaufNichtGefundenAntwort(query.TrainingslaufId);

        return ToAntwort(model);
    }

    public async Task<TrainingslaufListe> Frage(
        HoleTrainingslaeufe query, IGetAlleTrainingslaeufe getAlleTrainingslaeufe)
    {
        var modelle = await getAlleTrainingslaeufe.GetAlleAsync();

        var items = modelle.Select(m =>
        {
            return ToAntwort(m);
        }).ToList();

        return new TrainingslaufListe(items);
    }

    // Fallbacks: nicht-nullable Wire-Felder (siehe TrainingslaufAntwort-Doku). In der Praxis ist
    // Hyperparameter immer gesetzt (TrainingAngefordert); Endmetriken erst bei Abschluss.
    private static readonly Hyperparameter LeerHp = new(0, 0, 0, "", 0);
    private static readonly Endmetriken LeerEnd = new(0, 0);

    private static TrainingslaufAntwort ToAntwort(TrainingslaufReadModel m) => new(
        Id: m.Id,
        DatensatzId: m.DatensatzId,
        DatensatzVersion: m.DatensatzVersion,
        Status: m.Status,
        AktuelleEpoche: m.AktuelleEpoche,
        GesamtEpochen: m.GesamtEpochen,
        Hyperparameter: m.Hyperparameter ?? LeerHp,
        MetrikHistorie: m.MetrikHistorie,
        ModellPfad: m.ModellPfad,
        Endmetriken: m.Endmetriken ?? LeerEnd,
        Fehlergrund: m.Fehlergrund,
        Startzeit: m.Startzeit);
}
