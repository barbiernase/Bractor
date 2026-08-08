using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Blazor.Statistik;

namespace Domain.Client.Modules.Statistik;
public class StatistikModule : IHeadlessModule
{
    public string Id    => "statistik";
    public string Title => "Statistik";
    public Type   ComponentType => typeof(StatistikEffects);
}
