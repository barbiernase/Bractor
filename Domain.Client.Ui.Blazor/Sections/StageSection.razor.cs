using Client.Infrastructure.Abstractions;
using Domain.Client.ImagePair;
using Microsoft.AspNetCore.Components;

namespace Domain.Client.Ui.Blazor.Sections;

public partial class StageSection : ComponentBase, IDisposable
{
    [Inject] private EffectScope Fx { get; set; } = null!;
    [Inject] private ImagePairWorkspace Workspace { get; set; } = null!;

    [Parameter] public bool BilderAktiv { get; set; } = true;
    [Parameter] public bool DebugAktiv { get; set; } = true;
    [Parameter] public bool ChartAktiv { get; set; } = true;

    private void Render() => InvokeAsync(StateHasChanged);

    // ── Connect ──
    protected override void OnInitialized()
    {
        Fx.OnChanged(Workspace.Nav,   Render);
        Fx.OnChanged(Workspace.List,  Render);
        Fx.OnChanged(Workspace.Chart, Render);
        Fx.On<TagLabelingGestartet>(_ => { ActiveStageIndex = 0; Render(); });
    }

    // ── Selectors ──
    protected ImagePairRecord? AktuellerRecord => Workspace.AktuellerRecord;

    // ── Stage-Tabs ──
    protected int ActiveStageIndex;
    protected record StageTab(string Title, string Kind);
    protected List<StageTab> StageTabs { get; private set; } = new();
    private (bool b, bool d, bool c) _lastConfig;

    protected override void OnParametersSet()
    {
        var current = (BilderAktiv, DebugAktiv, ChartAktiv);
        if (StageTabs.Count > 0 && current == _lastConfig) return;

        _lastConfig = current;
        StageTabs.Clear();
        if (BilderAktiv) StageTabs.Add(new StageTab("Bilder", "bilder"));
        if (DebugAktiv)  StageTabs.Add(new StageTab("Debug",  "debug"));
        if (ChartAktiv)  StageTabs.Add(new StageTab("Chart",  "chart"));

        if (ActiveStageIndex >= StageTabs.Count)
            ActiveStageIndex = Math.Max(0, StageTabs.Count - 1);
    }

    protected string ActiveKind =>
        ActiveStageIndex >= 0 && ActiveStageIndex < StageTabs.Count
            ? StageTabs[ActiveStageIndex].Kind
            : "";

    public void Dispose() => Fx.Dispose();
}
