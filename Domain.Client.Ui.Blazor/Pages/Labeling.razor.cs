using Client.Infrastructure.Abstractions;
using Domain.Client.ImagePair;
using Domain.ImagePair;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using MudBlazor;

namespace Domain.Client.Ui.Blazor.Pages;

public partial class Labeling : IDisposable
{
    [Inject] private EffectScope Fx { get; set; } = null!;

    // ── UI-State ──
    protected ElementReference Container;
    protected bool PaarlisteAktiv = true, PaarlisteOffen = true;
    protected bool HistoriePanelAktiv = true, HistoriePanelOffen = true;
    protected bool BilderStageAktiv = true, DebugStageAktiv = true, ChartStageAktiv = true;
    protected bool SettingsOffen;
    protected readonly DialogOptions DialogOptions = new() { MaxWidth = MaxWidth.Small };

    // Stabile Methoden-Referenzen
    protected void OpenSettings() => SettingsOffen = true;
    protected void CloseSettings() => SettingsOffen = false;
    protected void SetPaarlisteOffen(bool v) => PaarlisteOffen = v;
    protected void SetHistoriePanelOffen(bool v) => HistoriePanelOffen = v;

    // ── Lifecycle ──
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender) await Container.FocusAsync();
    }

    // ── Keyboard → Dispatch ──
    protected void OnKeyDown(KeyboardEventArgs e)
    {
        switch (e.Key)
        {
            case "ArrowDown":   Fx.Dispatch(new NavigateNextAngefordert()); break;
            case "ArrowUp":     Fx.Dispatch(new NavigatePrevAngefordert()); break;
            case " ":           Fx.Dispatch(new NavigateNextOffenAngefordert()); break;

            case "r": case "R": Fx.Dispatch(new LabelProduktAngefordert(Klassifikation.KeineAnomalie)); break;
            case "q": case "Q": Fx.Dispatch(new LabelProduktAngefordert(Klassifikation.Questionable)); break;
            case "f": case "F": Fx.Dispatch(new LabelProduktAngefordert(Klassifikation.Anomalie)); break;

            case "e": case "E": Fx.Dispatch(new LabelKameraAngefordert(Klassifikation.Anomalie)); break;
            case "n": case "N": Fx.Dispatch(new LabelKameraAngefordert(Klassifikation.KeineAnomalie)); break;

            case "Tab": PaarlisteOffen = !PaarlisteOffen; break;
            case "l":   Fx.Dispatch(new ModusToggleAngefordert()); break;
            case "i":   Fx.Dispatch(new InspektionsfilterGetoggelt()); break;
            case "1":   Fx.Dispatch(new FilterWertGeaendert("alle")); break;
            case "2":   Fx.Dispatch(new FilterWertGeaendert("offen")); break;
            case "3":   Fx.Dispatch(new FilterWertGeaendert("anomalie")); break;

            case "Escape": Fx.Dispatch(new BeendeTagLabelingAngefordert()); break;
        }
    }

    public void Dispose() => Fx.Dispose();
}
