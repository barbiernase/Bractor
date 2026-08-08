using Client.Infrastructure.Abstractions;
using Domain.Client.ImagePair;
using Domain.Projections;
using Microsoft.AspNetCore.Components;

namespace Domain.Client.Ui.Blazor.Sections;

public partial class HistorieSection : ComponentBase, IDisposable
{
    [Inject] private EffectScope Fx { get; set; } = null!;
    [Inject] private ImagePairWorkspace Workspace { get; set; } = null!;

    [Parameter] public bool IsExpanded { get; set; }
    [Parameter] public EventCallback<bool> IsExpandedChanged { get; set; }

    private void Render() => InvokeAsync(StateHasChanged);

    // ── Connect ──
    protected override void OnInitialized()
    {
        Fx.OnChanged(Workspace.Nav,      Render);
        Fx.OnChanged(Workspace.Historie, Render);
    }

    // ── Selectors ──
    private bool HatAuswahl => Workspace.HatAuswahl;
    private IReadOnlyList<HistorieEintrag> Eintraege => Workspace.AktuelleHistorie;
    private bool IstGeladen => Workspace.AktuelleHistorieGeladen;

    public void Dispose() => Fx.Dispose();
}
