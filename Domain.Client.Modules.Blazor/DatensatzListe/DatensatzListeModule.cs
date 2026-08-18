using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Blazor.DatensatzListe;

namespace Domain.Client.Modules.Datensaetze;

/// <summary>
/// Linke Sidebar: alle Datensätze (Entwurf/Eingefroren, Größe, Version). Wie jedes
/// IUiModule automatisch vom ModuleRegistryGenerator registriert — kein Handwiring.
/// Klick wählt den aktiven Datensatz (dispatcht <see cref="DatensatzAusgewaehlt"/>),
/// den die Komposition-Stage übernimmt.
/// </summary>
public class DatensatzListeModule : ISidebarModule
{
    public string Id            => "datensatz-liste";
    public string Title         => "Datensätze";
    public Type   ComponentType => typeof(DatensatzListePanel);
    public SidebarSide Side     => SidebarSide.Left;
    public int ExpandedWidth    => 260;
}
