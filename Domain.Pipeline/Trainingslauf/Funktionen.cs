using Abstractions;
using Domain.Trainingslauf;

namespace Domain.Pipeline.Trainingslauf;

/// <summary>
/// Ein angefordertes Training ausführen (GPU-Worker): langlaufend, mit Fortschritt — ein STROM von Meldungen über die Zeit (Beginn, Fortschritt ×N,
/// Ende bzw. Scheitern). Eine Funktion draußen: der Trainings-Worker trägt sie (er verkörpert das TrainingsSystem, das sie rechnen darf); das Kind sendet
/// jedes Stück, bevor das nächste kommt — nach einem Abbruch rechnet ein Arbeiter den Lauf von vorn, die Inbox des Laufs verwirft schon Gemeldetes.
/// </summary>
public interface ITraining : IFunktion
{
    IAsyncEnumerable<OneOf<MeldeTrainingBegonnen, MeldeFortschritt, MeldeTrainingAbgeschlossen, MeldeTrainingGescheitert>> Auf(TrainingZuRechnen f);
}
