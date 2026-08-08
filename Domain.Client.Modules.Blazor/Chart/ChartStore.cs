using Client.Infrastructure.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using Domain.Projections;
namespace Domain.Client.Modules.Chart;

public partial class ChartStore : StoreBase, IHydrationStore
{
    [ObservableProperty] private IReadOnlyList<ProduktionsTag>? _produktionsTage;
    [ObservableProperty] private ProduktionsStripAntwort? _strip;
    [ObservableProperty] private DateTimeOffset? _gewaehlterTag;

    void Handle(ProduktionsTageAntwort a, MessageContext ctx)
    {
        ProduktionsTage = a.Tage;
        // Wenn keine Tage existieren, dispatcht der RefreshHandler KEIN
        // GetProduktionsStrip — die Kette endet hier. Dann sofort hydriert
        // markieren, sonst hängt der Bootstrap ewig auf die Strip-Antwort.
        if (a.Tage.Count == 0) MarkHydrated();
    }

    void Handle(ProduktionsStripAntwort a, MessageContext ctx)
    {
        Strip = a;
        // Ende der Query-Kette (GetProduktionsTage → GetProduktionsStrip).
        MarkHydrated();
    }

    void Handle(TagGewaehlt evt, MessageContext ctx) => GewaehlterTag = evt.Datum;
}
