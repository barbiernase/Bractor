using Client.Infrastructure.Abstractions;

namespace Host.Blazor.Shell;

public class ShellState
{
    private readonly Dictionary<string, bool> _visibility = new();
    private readonly Dictionary<string, bool> _expanded = new();

    public int ActiveStageIndex { get; set; }

    public bool IsVisible(IUiModule module)
        => _visibility.TryGetValue(module.Id, out var v) ? v : module.DefaultVisible;

    public void SetVisible(string moduleId, bool visible)
        => _visibility[moduleId] = visible;

    public bool IsExpanded(string moduleId)
        => _expanded.TryGetValue(moduleId, out var v) ? v : true;

    public void SetExpanded(string moduleId, bool expanded)
        => _expanded[moduleId] = expanded;
}
