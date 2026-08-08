using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Blazor.Historie;

namespace Domain.Client.Modules.Historie;
public class HistorieModule : ISidebarModule
{
    public string Id            => "historie";
    public string Title         => "Historie";
    public Type   ComponentType => typeof(HistoriePanel);
    public SidebarSide Side     => SidebarSide.Right;
    public int ExpandedWidth    => 220;
}
