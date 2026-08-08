using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Blazor.Data;

namespace Domain.Client.Modules.Data;
public class DataModule : IHeadlessModule
{
    public string Id            => "data";
    public string Title         => "Data";
    public Type   ComponentType => typeof(DataEffects);
}
