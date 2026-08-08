using Client.Infrastructure;
using Client.Infrastructure.Abstractions;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace Host.Blazor.Shell;

public partial class Shell : ComponentBase, IDisposable
{
    [Inject] private IEnumerable<IUiModule> AllModules { get; set; } = null!;
    [Inject] private ShellState State { get; set; } = null!;
    [Inject] private IBus Bus { get; set; } = null!;
    [Inject] private EffectScope Fx { get; set; } = null!;
    [Inject] private ClientStartupService Startup { get; set; } = null!;

    private ElementReference _container;
    private readonly ShellKeyBindingBuilder _bindingBuilder = new();
    private Dictionary<string, ShellKeyBindingBuilder.ResolvedBinding> _activeBindings = new();
    private bool _settingsOpen;
    private readonly DialogOptions _dialogOptions = new() { MaxWidth = MaxWidth.Small };

    // Ready-Gate: Module werden erst gerendert, wenn die Hydration fertig ist.
    private bool IsReady => Startup.State == BootstrapState.Ready;

    protected override void OnInitialized()
    {
        _bindingBuilder.AddShellKey("Tab", "Sidebar toggle", () => new ShellSidebarToggle());
        Fx.On<OpenSettingsRequested>(_ => { _settingsOpen = true; InvokeAsync(StateHasChanged); });
        RebuildBindings();

        // Auf Bootstrap-Fortschritt reagieren: bei Ready (bzw. jedem Wechsel)
        // neu rendern, damit das Gate öffnet und die Module mounten.
        Startup.StateChanged += OnBootstrapStateChanged;
    }

    private void OnBootstrapStateChanged() => InvokeAsync(StateHasChanged);

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await _container.FocusAsync();
    }

    private void OnKeyDown(KeyboardEventArgs e)
    {
        if (e.Key == "Tab") { ToggleFirstLeftSidebar(); return; }
        if (_activeBindings.TryGetValue(e.Key, out var resolved))
            Bus.Publish(resolved.Binding.CreateMessage());
    }

    private void RebuildBindings()
    {
        var allStages = AllModules.OfType<IStageModule>()
            .Where(m => State.IsVisible(m)).OrderBy(m => m.Order).ToList();
        var activeStage = State.ActiveStageIndex >= 0 && State.ActiveStageIndex < allStages.Count
            ? [allStages[State.ActiveStageIndex]] : Enumerable.Empty<IStageModule>();

        _activeBindings = _bindingBuilder.Build(
            AllModules.OfType<IFooterModule>().Where(m => State.IsVisible(m)),
            AllModules.OfType<IHeaderModule>().Where(m => State.IsVisible(m)),
            AllModules.OfType<ISidebarModule>().Where(m => State.IsVisible(m)),
            activeStage);
    }

    private void OnStageTabChanged(int i) { State.ActiveStageIndex = i; RebuildBindings(); }
    private void OnSidebarToggle(string id, bool v) { State.SetExpanded(id, v); RebuildBindings(); }

    private void ToggleFirstLeftSidebar()
    {
        var sb = AllModules.OfType<ISidebarModule>()
            .FirstOrDefault(m => m.Side == SidebarSide.Left && State.IsVisible(m));
        if (sb != null) { State.SetExpanded(sb.Id, !State.IsExpanded(sb.Id)); RebuildBindings(); }
    }

    public void Dispose()
    {
        Startup.StateChanged -= OnBootstrapStateChanged;
        Fx.Dispose();
    }
}

public record ShellSidebarToggle;
