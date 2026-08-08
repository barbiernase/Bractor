using Client.Infrastructure.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using Domain.Projections;
namespace Domain.Client.Modules.Statistik;

public partial class StatistikStore : StoreBase
{
    [ObservableProperty] private int _gesamt;
    [ObservableProperty] private int _komplett;
    [ObservableProperty] private int _mitKi;
    [ObservableProperty] private int _ohneKlassifikation;
    [ObservableProperty] private int _anomalie;

    void Handle(ImagePairStatistikAntwort a, MessageContext ctx)
    {
        Gesamt = a.Gesamt;
        Komplett = a.Komplett;
        MitKi = a.MitKiKlassifikation;
        OhneKlassifikation = a.OhneKlassifikation;
        Anomalie = a.AnzahlAnomalienKi;
    }
}
