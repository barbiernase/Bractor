using Client.Infrastructure.Abstractions;

namespace Domain.Client.Modules.TrainingDashboard;

// Client-lokale Intents: die View dispatcht sie, der TrainingDashboardIntentHandler
// übersetzt sie in Trainingslauf-Commands.

/// <summary>Ein neues Training gegen eine eingefrorene Datensatz-Version starten.</summary>
public record StarteTrainingIntent(
    DatensatzId DatensatzId,
    int DatensatzVersion,
    int Epochen,
    double LernRate,
    int BatchGroesse,
    string Architektur,
    int Seed) : IClientEvent;

/// <summary>Einen laufenden Trainingslauf abbrechen.</summary>
public record BrichTrainingAbIntent(TrainingslaufId TrainingslaufId) : IClientEvent;
