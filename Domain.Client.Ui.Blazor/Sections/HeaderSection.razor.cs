using Client.Infrastructure.Abstractions;
using Domain.Client.ImagePair;
using Microsoft.AspNetCore.Components;

namespace Domain.Client.Ui.Blazor.Sections;

public partial class HeaderSection : ComponentBase, IDisposable
{
    [Inject] private EffectScope Fx { get; set; } = null!;
    [Inject] private ImagePairWorkspace Workspace { get; set; } = null!;

    [Parameter] public EventCallback OnSettingsClick { get; set; }

    private void Dispatch(object message) => Fx.Dispatch(message);
    private void Render() => InvokeAsync(StateHasChanged);

    // ── Connect ──
    protected override void OnInitialized()
    {
        Fx.OnChanged(Workspace.Nav,  Render);
        Fx.OnChanged(Workspace.List, Render);
    }

    // ── Selectors ──
    private ArbeitsModus Modus => Workspace.Nav.Modus;
    private DateTimeOffset? TagDatum => Workspace.Nav.TagLabelingDatum;
    private ImagePairRecord? Record => Workspace.AktuellerRecord;
    private int Position => Workspace.AktuellePosition;
    private int GesamtAnzahl => Workspace.List.GesamtAnzahl;

    // ── Actions ──
    private void ToggleModus() => Dispatch(new ModusToggleAngefordert());

    public void Dispose() => Fx.Dispose();
}
