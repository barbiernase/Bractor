using Client.Infrastructure.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using Domain.Client.Modules.Datensaetze;
using Domain.Datensatz;
using Domain.ImagePair;
using Domain.Projections;

namespace Domain.Client.Modules.DatensatzKomposition;

/// <summary>
/// Der Datensatz-Korb (rechte Spalte). Hält Kopf-Daten des aktiven Datensatzes
/// (<see cref="DatensatzAntwort"/>) und — nach dem Einfrieren — die aus den Samples
/// live gerechnete Klassenbalance / Split-Verteilung (pragmatische Alternative zu
/// §5.6: Balance erst nach Freeze aus <see cref="DatensatzSamples"/>).
///
/// Der aktive Datensatz kommt aus der Sidebar-Auswahl (<see cref="DatensatzAusgewaehlt"/>)
/// oder direkt nach dem Anlegen (der IntentHandler dispatcht die Auswahl mit).
/// </summary>
public partial class DatensatzKompositionStore : StoreBase
{
    [ObservableProperty] private Guid? _aktuelleDatensatzId;
    [ObservableProperty] private string? _name;
    [ObservableProperty] private DatensatzStatus _status;
    [ObservableProperty] private int _anzahlMitglieder;
    [ObservableProperty] private int _eingefroreneVersion;
    [ObservableProperty] private SplitKonfig _aktuellerSplit = SplitKonfig.Default;
    [ObservableProperty] private IReadOnlyList<RangeHerkunft> _ranges = [];

    // Balance/Split-Verteilung nach dem Einfrieren (aus den Samples gerechnet).
    [ObservableProperty] private bool _balanceGeladen;
    [ObservableProperty] private int _balOk;
    [ObservableProperty] private int _balFraglich;
    [ObservableProperty] private int _balAnomalie;
    [ObservableProperty] private int _splTrain;
    [ObservableProperty] private int _splVal;
    [ObservableProperty] private int _splTest;

    public bool IstEingefroren => Status == DatensatzStatus.Eingefroren;

    void Handle(DatensatzAusgewaehlt evt, MessageContext ctx)
    {
        if (AktuelleDatensatzId == evt.DatensatzId) return;
        AktuelleDatensatzId = evt.DatensatzId;
        // Kopf-Daten werden vom RefreshHandler (HoleDatensatz) nachgeladen; Balance zurücksetzen.
        BalanceGeladen = false;
    }

    void Handle(DatensatzAntwort a, MessageContext ctx)
    {
        // Nur die Antwort für den aktiven Datensatz übernehmen.
        if (AktuelleDatensatzId is { } id && a.Id != id) return;

        AktuelleDatensatzId = a.Id;
        Name = a.Name;
        Status = a.Status;
        AnzahlMitglieder = a.AnzahlMitglieder;
        EingefroreneVersion = a.EingefroreneVersion;
        AktuellerSplit = a.Split;
        Ranges = a.Ranges;
    }

    void Handle(DatensatzSamples s, MessageContext ctx)
    {
        // Balance/Split über die eingefrorene Stichprobe (Seite 1, bis 500 Samples) rechnen.
        BalOk       = s.Samples.Count(x => x.Label == Klassifikation.KeineAnomalie);
        BalFraglich = s.Samples.Count(x => x.Label == Klassifikation.Questionable);
        BalAnomalie = s.Samples.Count(x => x.Label == Klassifikation.Anomalie);
        SplTrain    = s.Samples.Count(x => x.Split == Domain.Datensatz.Split.Train);
        SplVal      = s.Samples.Count(x => x.Split == Domain.Datensatz.Split.Val);
        SplTest     = s.Samples.Count(x => x.Split == Domain.Datensatz.Split.Test);
        BalanceGeladen = true;
    }
}
