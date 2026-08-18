using Client.Infrastructure.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using Domain.Projections;

namespace Domain.Client.Modules.Datensaetze;

/// <summary>
/// Hält die Datensatz-Liste für die Sidebar. Reducer = <see cref="Handle"/>: nimmt die
/// <see cref="DatensatzListe"/>-Antwort (auf <c>HoleDatensaetze</c>) entgegen. Der Generator
/// subscribed die Handle-Methode automatisch auf dem Bus — kein Wiring.
/// </summary>
public partial class DatensatzListeStore : StoreBase
{
    [ObservableProperty] private IReadOnlyList<DatensatzAntwort> _datensaetze = [];

    /// <summary>Die aktuell gewählte Datensatz-Id (Klick-Markierung in der Liste).</summary>
    [ObservableProperty] private Guid? _aktiveId;

    void Handle(DatensatzListe antwort, MessageContext ctx)
    {
        Datensaetze = antwort.Items;
    }

    void Handle(DatensatzAusgewaehlt evt, MessageContext ctx)
    {
        AktiveId = evt.DatensatzId;
    }
}
