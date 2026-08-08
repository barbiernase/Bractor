using Abstractions;
using Client.Infrastructure.Abstractions;
using Domain.ImagePair;
using Microsoft.AspNetCore.Components;
using MudBlazor;

namespace Domain.Client.Ui.Blazor.Components;

public partial class GlobalEffects : ComponentBase, IDisposable
{
    [Inject] private EffectScope Fx { get; set; } = null!;
    [Inject] private ISnackbar Snackbar { get; set; } = null!;

    protected override void OnInitialized()
    {
        Fx.On<BildNichtVerfuegbar>(evt        => Show(evt.ToString()));
        Fx.On<RegionIndexUngueltig>(evt       => Show(evt.ToString()));
        Fx.On<PaarNichtKomplett>(evt           => Show(evt.ToString()));
        Fx.On<ImagePairNichtGefunden>(evt      => Show(evt.ToString()));
        Fx.On<ImagePairEingabeUngueltig>(evt   => Show($"{evt.Feld}: {evt.Grund}"));
        Fx.On<CommandFailed>(evt              => ShowError(evt.Reason));
    }

    private void Show(string text)
        => InvokeAsync(() => Snackbar.Add(text, Severity.Warning));

    private void ShowError(string text)
        => InvokeAsync(() => Snackbar.Add(text, Severity.Error));

    public void Dispose() => Fx.Dispose();
}
