using Client.Infrastructure.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using Domain.Projections;

namespace Domain.Client.Modules.Trainingslaeufe;

/// <summary>
/// Hält die Trainingslauf-Liste für die Sidebar. Reducer = <see cref="Handle"/> auf die
/// <see cref="TrainingslaufListe"/>-Antwort (auf <c>HoleTrainingslaeufe</c>). Auto-subscribed.
/// </summary>
public partial class TrainingslaufListeStore : StoreBase
{
    [ObservableProperty] private IReadOnlyList<TrainingslaufAntwort> _laeufe = [];
    [ObservableProperty] private Guid? _aktiveId;

    void Handle(TrainingslaufListe antwort, MessageContext ctx)
    {
        // Neueste zuerst (Startzeit absteigend) — der Reader liefert die Reihenfolge des
        // Read-Stores; hier defensiv nach Startzeit sortiert, damit frische Läufe oben stehen.
        Laeufe = antwort.Items.OrderByDescending(l => l.Startzeit).ToList();
    }

    void Handle(TrainingslaufAusgewaehlt evt, MessageContext ctx)
    {
        AktiveId = evt.TrainingslaufId;
    }
}
