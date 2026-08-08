using Client.Infrastructure.Abstractions;
using Domain.Client.ImagePair;
using Domain.ImagePair;
using Microsoft.AspNetCore.Components;

namespace Domain.Client.Ui.Blazor.Components;

public partial class FooterBar : ComponentBase, IDisposable
{
    [Inject] private EffectScope Fx { get; set; } = null!;

    private void Dispatch(object message) => Fx.Dispatch(message);

    // ── Actions: Produkt ──
    private void LabelOk()       => Dispatch(new LabelProduktAngefordert(Klassifikation.KeineAnomalie));
    private void LabelFraglich() => Dispatch(new LabelProduktAngefordert(Klassifikation.Questionable));
    private void LabelAnomalie() => Dispatch(new LabelProduktAngefordert(Klassifikation.Anomalie));

    // ── Actions: Kamera ──
    private void KameraErkennbar() => Dispatch(new LabelKameraAngefordert(Klassifikation.Anomalie));
    private void KameraNicht()     => Dispatch(new LabelKameraAngefordert(Klassifikation.KeineAnomalie));

    // ── Actions: Navigation ──
    private void NavPrev()      => Dispatch(new NavigatePrevAngefordert());
    private void NavNext()      => Dispatch(new NavigateNextAngefordert());
    private void NavNextOffen() => Dispatch(new NavigateNextOffenAngefordert());

    public void Dispose() => Fx.Dispose();
}
