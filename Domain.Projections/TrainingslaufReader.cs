using Abstractions;
using Domain.Trainingslauf;

namespace Domain.Projections;

/// <summary>
/// Reader der Trainingslauf-Projektion (analog <see cref="ImagePairReader"/>). Beantwortet die
/// Trainings-Queries über den <see cref="ITrainingslaufReadStore"/> — derselbe Kanal für Blazor
/// (Live-Dashboard) und Python.
/// </summary>
[ProjectionReader(TrackDeps = true)]
public partial class TrainingslaufReader : IReader<TrainingslaufProjektion>
{
    private readonly ITrainingslaufReadStore _store;

    public TrainingslaufReader(ITrainingslaufReadStore store)
    {
        _store = store;
    }

    public async Task<OneOf<TrainingslaufAntwort, TrainingslaufNichtGefundenAntwort>> Handle(
        HoleTrainingslauf query, IMessageEnvelope envelope, ReadContext ctx)
    {
        var model = await _store.FindByIdAsync(query.TrainingslaufId);

        if (model is null)
            return new TrainingslaufNichtGefundenAntwort(query.TrainingslaufId);

        ctx.Track(query.TrainingslaufId.ToString());
        return ToAntwort(model);
    }

    public async Task<TrainingslaufListe> Handle(
        HoleTrainingslaeufe query, IMessageEnvelope envelope, ReadContext ctx)
    {
        var modelle = await _store.GetAlleAsync();

        var items = modelle.Select(m =>
        {
            ctx.Track(m.Id.ToString());
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
