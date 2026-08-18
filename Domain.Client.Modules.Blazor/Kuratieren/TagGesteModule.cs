using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Blazor.Kuratieren;

namespace Domain.Client.Modules.Kuratieren;

/// <summary>
/// Footer-Modul „Tag-Geste" (Konzept datensatz-kuratierung §4.2): Taste <c>A</c> nimmt das
/// betrachtete Cursor-Paar ins aktive Sammel-Ziel auf bzw. wieder heraus — dieselbe
/// Muskelgedächtnis-Logik wie das Labeln (R/Q/F). Der kleine Footer-View zeigt den
/// Toggle-Zustand („✓ in <Name>") des aktuellen Paars.
///
/// Auto-entdeckt als <see cref="IFooterModule"/>; die Taste geht über die Shell-Keybindings
/// (Muster: <c>LabelingModule</c>).
/// </summary>
public class TagGesteModule : IFooterModule
{
    public string Id    => "tag-geste";
    public string Title => "Datensatz-Tag";
    public Type   ComponentType => typeof(TagGesteFooter);
    public int    Order => 3;   // direkt nach Labeling (2)

    public IReadOnlyList<KeyBinding> KeyBindings =>
    [
        new("a", "Aufnehmen/Entfernen", () => new DatensatzTagGetoggelt()),
    ];
}
