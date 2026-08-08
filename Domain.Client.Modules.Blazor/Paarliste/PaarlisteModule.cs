using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Blazor.Paarliste;

namespace Domain.Client.Modules.Paarliste;
public class PaarlisteModule : ISidebarModule
{
    public string Id            => "paarliste";
    public string Title         => "Paarliste";
    public Type   ComponentType => typeof(PaarlistenPanel);
    public SidebarSide Side     => SidebarSide.Left;

    // Breiter, damit Datum+Uhrzeit (inkl. Jahr) plus die drei Punkte vollständig
    // lesbar sind, ohne Abschneiden. Eine Zahl — bei Bedarf feinjustieren.
    public int ExpandedWidth    => 260;
}
