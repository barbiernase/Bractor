using Abstractions;

namespace Domain.Trainingslauf;

// FOLGEN des Trainingslaufs (docs/03 §3.2): was eine Entscheidung den Funktionen sagt, die sie hören — schlichte Records, nicht im Log.

/// <summary>Das Training ist angefordert: der Lauf ist auf dem Datensatz in dieser Version mit diesen Hyperparametern zu rechnen.</summary>
public record TrainingZuRechnen(Id<Trainingslauf> Lauf, Id<Datensatz.Datensatz> DatensatzId, int DatensatzVersion, Hyperparameter Hyperparameter);
