using Client.Infrastructure.Abstractions;
using Domain.Client.ImagePair;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace Domain.Client.Ui.Blazor.Sections;

public partial class PaarlisteSection : ComponentBase, IDisposable
{
    [Inject] private EffectScope Fx { get; set; } = null!;
    [Inject] private ImagePairWorkspace Workspace { get; set; } = null!;
    [Inject] private IJSRuntime JS { get; set; } = null!;

    [Parameter] public bool IsExpanded { get; set; }
    [Parameter] public EventCallback<bool> IsExpandedChanged { get; set; }

    private void Dispatch(object message) => Fx.Dispatch(message);
    private void Render() => InvokeAsync(StateHasChanged);

    // ── Connect ──
    protected override void OnInitialized()
    {
        Fx.OnChanged(Workspace.List, Render);
        Fx.OnChanged(Workspace.Nav,  Render);
        Fx.On<ImagePairAusgewaehlt>(_ => _scrollPending = true);
    }

    // ── Selectors ──
    private IReadOnlyList<ImagePairRecord> Items => Workspace.List.Values.ToList();
    private int SelectedIndex => Workspace.Nav.SelectedIndex;
    private string FilterWert => Workspace.Nav.FilterWert;
    private bool NurNichtInspizierte => Workspace.Nav.NurNichtInspizierte;
    private int Seite => Workspace.List.Seite;
    private int GesamtSeiten => Workspace.List.GesamtSeiten;

    // ── Actions ──
    private void SelectPair(int index)
        => Dispatch(new ImagePairAusgewaehlt(Workspace.List[index].Id));
    private void ChangeFilter(string value)
        => Dispatch(new FilterWertGeaendert(value));
    private void ToggleInspektionsFilter(bool _)
        => Dispatch(new InspektionsfilterGetoggelt());
    private void VorigeSeite()
        => Dispatch(new SeiteAngefordert(Seite - 1));
    private void NaechsteSeite()
        => Dispatch(new SeiteAngefordert(Seite + 1));

    // ── Effects ──
    private bool _scrollPending;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (!_scrollPending) return;
        _scrollPending = false;
        try { await JS.InvokeVoidAsync("eval",
                $"document.getElementById('pair-{SelectedIndex}')?.scrollIntoView({{block:'nearest'}})"); }
        catch { }
    }

    public void Dispose() => Fx.Dispose();
}
