using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Blazor.Chart;

namespace Domain.Client.Modules.Chart;
public class ChartModule : IStageModule
{
    public string Id    => "chart";
    public string Title => "Chart";
    public Type   ComponentType => typeof(ChartView);
    public int    Order => 3;
}
