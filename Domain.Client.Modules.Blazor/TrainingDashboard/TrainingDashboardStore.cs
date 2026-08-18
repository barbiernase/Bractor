using Client.Infrastructure.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using Domain.Datensatz;
using Domain.Projections;

namespace Domain.Client.Modules.TrainingDashboard;

/// <summary>
/// Dashboard-State: alle Trainingsläufe (für die Live-Kurven) und die eingefrorenen
/// Datensätze (für die „Neues Training"-Auswahl). Reducer:
/// <list type="bullet">
///   <item><see cref="Handle(TrainingslaufListe, MessageContext)"/> — Voll-Refresh der Läufe.</item>
///   <item><see cref="Handle(TrainingslaufAntwort, MessageContext)"/> — Einzel-Upsert (Live-Kurve wächst).</item>
///   <item><see cref="Handle(DatensatzListe, MessageContext)"/> — eingefrorene Datensätze fürs Formular.</item>
/// </list>
/// </summary>
public partial class TrainingDashboardStore : StoreBase
{
    // Interner Index; die öffentliche Liste ist die (neu zugewiesene) Sicht darauf.
    private readonly Dictionary<Guid, TrainingslaufAntwort> _index = new();

    [ObservableProperty] private IReadOnlyList<TrainingslaufAntwort> _laeufe = [];
    [ObservableProperty] private IReadOnlyList<DatensatzAntwort> _eingefroreneDatensaetze = [];

    void Handle(TrainingslaufListe antwort, MessageContext ctx)
    {
        _index.Clear();
        foreach (var l in antwort.Items) _index[l.Id] = l;
        Neuberechnen();
    }

    void Handle(TrainingslaufAntwort antwort, MessageContext ctx)
    {
        _index[antwort.Id] = antwort;
        Neuberechnen();
    }

    void Handle(DatensatzListe antwort, MessageContext ctx)
    {
        EingefroreneDatensaetze = antwort.Items
            .Where(d => d.Status == DatensatzStatus.Eingefroren)
            .OrderBy(d => d.Name)
            .ToList();
    }

    // Neu zuweisen (nicht in-place mutieren) → die [ObservableProperty] feuert Changed.
    private void Neuberechnen() =>
        Laeufe = _index.Values.OrderByDescending(l => l.Startzeit).ToList();
}
