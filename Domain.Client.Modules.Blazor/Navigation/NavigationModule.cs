using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Blazor.Navigation;

namespace Domain.Client.Modules.Navigation;
public class NavigationModule : IFooterModule
{
    public string Id            => "navigation";
    public string Title         => "Navigation";
    public Type   ComponentType => typeof(NavigationFooter);
    public int    Order         => 1;
    public IReadOnlyList<KeyBinding> KeyBindings =>
    [
        new("ArrowDown", "Nächstes",        () => new NavigateNextAngefordert()),
        new("ArrowUp",   "Vorheriges",      () => new NavigatePrevAngefordert()),
        new(" ",         "Nächstes offenes", () => new NavigateNextOffenAngefordert()),
    ];
}
