using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Blazor.TrainingslaufListe;

namespace Domain.Client.Modules.Trainingslaeufe;

/// <summary>
/// Linke Sidebar: alle Trainingsläufe mit Live-Status-Badges. Automatisch als IUiModule
/// registriert. Klick fokussiert den Lauf im Training-Dashboard (<see cref="TrainingslaufAusgewaehlt"/>).
/// </summary>
public class TrainingslaufListeModule : ISidebarModule
{
    public string Id            => "trainingslauf-liste";
    public string Title         => "Trainingsläufe";
    public Type   ComponentType => typeof(TrainingslaufListePanel);
    public SidebarSide Side     => SidebarSide.Left;
    public int ExpandedWidth    => 260;
}
