using Client.Infrastructure.Abstractions;

namespace Domain.Client.Modules.Trainingslaeufe;

/// <summary>
/// Client-lokal (IClientEvent): der Nutzer hat in der Trainingslauf-Liste einen Lauf
/// gewählt. Das Training-Dashboard scrollt/fokussiert daraufhin diesen Lauf. Geht nie
/// an den Server.
/// </summary>
public record TrainingslaufAusgewaehlt(Guid TrainingslaufId) : IClientEvent;
