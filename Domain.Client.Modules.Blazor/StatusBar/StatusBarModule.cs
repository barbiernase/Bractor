using Client.Infrastructure.Abstractions;
using Domain.Client.Modules.Blazor.StatusBar;

namespace Domain.Client.Modules.StatusBar;
public class StatusBarModule : IHeaderModule
{
    public string Id    => "statusbar";
    public string Title => "Status";
    public Type   ComponentType => typeof(StatusBarView);
    public IReadOnlyList<KeyBinding> KeyBindings =>
    [
        new("l", "Modus wechseln", () => new ModusToggleAngefordert()),
        new("i", "Inspektionsfilter", () => new InspektionsfilterGetoggelt()),
        new("1", "Filter: alle",     () => new FilterWertGeaendert("alle")),
        new("2", "Filter: offen",    () => new FilterWertGeaendert("offen")),
        new("3", "Filter: anomalie", () => new FilterWertGeaendert("anomalie")),
        new("Escape", "Tag-Labeling beenden", () => new BeendeTagLabelingAngefordert()),
    ];
}
