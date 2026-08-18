using Client.Infrastructure.Abstractions;
using Domain.Trainingslauf;

namespace Domain.Client.Modules.TrainingDashboard;

/// <summary>
/// Übersetzt die Dashboard-Intents in Trainingslauf-Commands. <c>StarteTraining</c> ist ein
/// ICreationCommand → neue AggregateId hier erzeugt. Der Python-TrainingWorker (M8) abonniert
/// das resultierende <c>TrainingAngefordert</c>-Event und fährt den Lauf.
/// </summary>
public partial class TrainingDashboardIntentHandler
{
    IEnumerable<object> Handle(StarteTrainingIntent evt, MessageContext ctx)
    {
        var id = Guid.NewGuid();
        var hp = new Hyperparameter(
            Epochen: evt.Epochen,
            LernRate: evt.LernRate,
            BatchGroesse: evt.BatchGroesse,
            Architektur: string.IsNullOrWhiteSpace(evt.Architektur) ? "cnn" : evt.Architektur.Trim(),
            Seed: evt.Seed);
        yield return new StarteTraining(id, evt.DatensatzId, evt.DatensatzVersion, hp);
    }

    IEnumerable<object> Handle(BrichTrainingAbIntent evt, MessageContext ctx)
    {
        yield return new BricheTrainingAb(evt.TrainingslaufId);
    }
}
