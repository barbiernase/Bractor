using Abstractions;

namespace Domain.Pipeline.Trainingslauf;

/// <summary>Ein angeforderter Trainingslauf wird gerechnet — draußen, beim Trainings-Worker (ein Strom von Fortschritten).</summary>
public sealed record Trainingsrechnung(ITraining Training) : IGraph;
