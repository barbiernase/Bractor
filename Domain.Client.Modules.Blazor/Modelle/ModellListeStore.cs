using Client.Infrastructure.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using Domain.Projections;

namespace Domain.Client.Modules.Modelle;

/// <summary>
/// Hält die Modell-Liste für die Modelle-Bühne (Konzept konzept-training-und-datensatz §5).
/// Reducer = <see cref="Handle"/> auf die <see cref="ModellListe"/>-Antwort (auf <c>HoleModelle</c>).
/// </summary>
public partial class ModellListeStore : StoreBase
{
    [ObservableProperty] private IReadOnlyList<ModellAntwort> _modelle = [];

    void Handle(ModellListe antwort, MessageContext ctx)
    {
        Modelle = antwort.Items;
    }
}
