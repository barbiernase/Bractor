using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Blazor.Heatmap;

namespace Domain.Client.Modules.Heatmap;
public class HeatmapModule : IStageModule
{
    public string Id    => "heatmap";
    public string Title => "Heatmap";
    public Type   ComponentType => typeof(HeatmapView);
    public int    Order => 4;
    public bool   DefaultVisible => false;
}
