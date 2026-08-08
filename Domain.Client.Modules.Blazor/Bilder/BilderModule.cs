using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Blazor.Bilder;

namespace Domain.Client.Modules.Bilder;
public class BilderModule : IStageModule
{
    public string Id    => "bilder";
    public string Title => "Bilder";
    public Type   ComponentType => typeof(BilderStage);
    public int    Order => 1;
}
