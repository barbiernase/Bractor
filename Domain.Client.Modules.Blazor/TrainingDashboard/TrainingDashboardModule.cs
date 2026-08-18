using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Blazor.TrainingDashboard;

namespace Domain.Client.Modules.TrainingDashboard;

/// <summary>
/// Zentrale Bühne „Training-Dashboard" (Konzept §8.2). Als IStageModule automatisch ein Tab.
/// „Neues Training"-Formular (eingefrorener Datensatz + Hyperparameter) und je Lauf eine
/// Live-Kurve (loss/accuracy) über ApexCharts, gespeist vom TrainingFortschritt-Event-Stream.
/// </summary>
public class TrainingDashboardModule : IStageModule
{
    public string Id    => "training-dashboard";
    public string Title => "Training";
    public Type   ComponentType => typeof(TrainingDashboardPanel);
    public int    Order => 11;
}
