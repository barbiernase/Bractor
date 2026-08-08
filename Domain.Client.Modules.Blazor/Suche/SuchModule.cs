using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Blazor.Suche;

namespace Domain.Client.Modules.Suche;

/// <summary>
/// Rechtes Such-/Query-Panel. Wird — wie jedes IUiModule — vom
/// ModuleRegistryGenerator automatisch als IUiModule registriert; kein
/// manuelles Wiring. Steuert über SucheGeaendert den einen Filter-Zustand
/// des Store und damit, welche Paare in Liste und Bilder-Panel erscheinen.
/// </summary>
public class SuchModule : ISidebarModule
{
    public string Id            => "suche";
    public string Title         => "Suche";
    public Type   ComponentType => typeof(SuchPanel);
    public SidebarSide Side     => SidebarSide.Right;
    public int ExpandedWidth    => 300;
}
