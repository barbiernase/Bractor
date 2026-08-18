using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Connection;
using Domain.Datensatz;
using Domain.Projections;
using Domain.Trainingslauf;

namespace Domain.Client.Modules.TrainingDashboard;

/// <summary>
/// Lädt beim Verbindungsaufbau die Läufe + eingefrorenen Datensätze und hält jeden Lauf
/// über gezielte Einzel-Queries frisch: <b>jeder</b> <c>TrainingFortschritt</c> triggert ein
/// <c>HoleTrainingslauf(id)</c> → die MetrikHistorie wächst → die Live-Kurve streamt.
/// </summary>
public partial class TrainingDashboardRefreshHandler
{
    IEnumerable<object> Handle(ConnectionEstablished evt, MessageContext ctx)
    {
        yield return new HoleTrainingslaeufe();
        yield return new HoleDatensaetze();   // eingefrorene Datensätze für das Formular
    }

    // Ein frisch eingefrorener Datensatz wird trainierbar → Formular-Auswahl nachladen.
    IEnumerable<object> Handle(DatensatzEingefroren evt, MessageContext ctx)
    {
        yield return new HoleDatensaetze();
    }

    IEnumerable<object> Handle(TrainingAngefordert evt, MessageContext ctx)      => Einzel(ctx);
    IEnumerable<object> Handle(TrainingBegonnen evt, MessageContext ctx)         => Einzel(ctx);
    IEnumerable<object> Handle(TrainingFortschritt evt, MessageContext ctx)      => Einzel(ctx);
    IEnumerable<object> Handle(TrainingAbgeschlossen evt, MessageContext ctx)    => Einzel(ctx);
    IEnumerable<object> Handle(TrainingGescheitert evt, MessageContext ctx)      => Einzel(ctx);
    IEnumerable<object> Handle(TrainingAbgebrochen evt, MessageContext ctx)      => Einzel(ctx);
    IEnumerable<object> Handle(TrainingHaengengeblieben evt, MessageContext ctx) => Einzel(ctx);

    private static IEnumerable<object> Einzel(MessageContext ctx)
    {
        yield return new HoleTrainingslauf(ctx.AggregateId);
    }
}
