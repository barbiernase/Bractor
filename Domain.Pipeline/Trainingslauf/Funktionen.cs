using Abstractions;
using Domain.Trainingslauf;

namespace Domain.Pipeline.Trainingslauf;

/// <summary>
/// Ein angefordertes Training ausführen (GPU-Worker): langlaufend, mit Fortschritt — ein STROM von Meldungen über die Zeit (Beginn, Fortschritt ×N,
/// Ende bzw. Scheitern). Eine Funktion draußen: der Trainings-Worker trägt sie (<c>ITraegt&lt;ITraining&gt;</c>); der Funktions-Actor hält jedes
/// Stück fest und sendet es, bevor das nächste kommt — nach einem Abbruch geht es bei der nächsten Meldung weiter.
/// </summary>
public interface ITraining : IFunktion
{
    IAsyncEnumerable<OneOf<MeldeTrainingBegonnen, MeldeFortschritt, MeldeTrainingAbgeschlossen, MeldeTrainingGescheitert>> Auf(TrainingAngefordert e);
}
