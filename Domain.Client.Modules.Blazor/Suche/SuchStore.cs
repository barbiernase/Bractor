using Client.Infrastructure.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using Domain.Projections;
namespace Domain.Client.Modules.Suche;

/// <summary>
/// Store des Suche-Moduls für die Baum-Navigation. Hält NUR Anzeige-Zustand:
/// die (vom Bus gelieferte) Produktionstage-Liste und die ausgewählten Tage
/// (Multi-Select über die Häkchen). Der eigentliche Bildfilter (SuchKriterien)
/// bleibt im großen Store; die Baum-Auswahl speist ihn per Bounding-Range.
///
/// Entkopplung: hört per eigenem Handle auf dieselbe ProduktionsTageAntwort wie
/// der ChartStore. Der Load kommt weiterhin vom ChartRefreshHandler.
/// </summary>
public partial class SuchStore : StoreBase
{
    [ObservableProperty] private IReadOnlyList<ProduktionsTag> _produktionsTage = Array.Empty<ProduktionsTag>();

    // Auf Tagesgrenze normierte Menge der ausgewählten Tage (00:00, TimeSpan.Zero).
    [ObservableProperty] private IReadOnlySet<DateTimeOffset> _selektierteTage = new HashSet<DateTimeOffset>();

    void Handle(ProduktionsTageAntwort a, MessageContext ctx) => ProduktionsTage = a.Tage;

    // Rein clientseitig: nur State setzen. Kein Reset, kein Query, kein Handler.
    void Handle(AuswahlGeaendert evt, MessageContext ctx) => SelektierteTage = evt.Tage;
}
