using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Connection;
using Domain.Projections;
using Domain.Trainingslauf;

namespace Domain.Client.Modules.Trainingslaeufe;

/// <summary>
/// Feuert die Listen-Query nach dem Verbindungsaufbau und bei jedem Statuswechsel eines
/// Laufs. <c>TrainingFortschritt</c> ist bewusst NICHT dabei: der Status ändert sich dabei
/// nicht (nur die Epoche), und die Live-Kurve treibt das Dashboard, nicht die Sidebar.
/// </summary>
public partial class TrainingslaufListeRefreshHandler
{
    IEnumerable<object> Handle(ConnectionEstablished evt, MessageContext ctx)     => Lade();
    IEnumerable<object> Handle(TrainingAngefordert evt, MessageContext ctx)       => Lade();
    IEnumerable<object> Handle(TrainingBegonnen evt, MessageContext ctx)          => Lade();
    IEnumerable<object> Handle(TrainingAbgeschlossen evt, MessageContext ctx)     => Lade();
    IEnumerable<object> Handle(TrainingGescheitert evt, MessageContext ctx)       => Lade();
    IEnumerable<object> Handle(TrainingAbgebrochen evt, MessageContext ctx)       => Lade();
    IEnumerable<object> Handle(TrainingHaengengeblieben evt, MessageContext ctx)  => Lade();

    private static IEnumerable<object> Lade()
    {
        yield return new HoleTrainingslaeufe();
    }
}
