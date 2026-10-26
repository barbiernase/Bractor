using Client.Infrastructure.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using Domain.Projections;
using Domain.Projections.ImagePair;
namespace Domain.Client.Modules.Historie;

public partial class HistorieStore : StoreBase
{
    private readonly Dictionary<ImagePairId, List<HistorieEintrag>> _data = new();

    [ObservableProperty] private int _version;

    public IReadOnlyList<HistorieEintrag> Get(ImagePairId pairId)
        => _data.TryGetValue(pairId, out var list) ? list : [];
    public bool IstGeladen(ImagePairId pairId) => _data.ContainsKey(pairId);

    // Reine Backend-Historie: der Server ist die Quelle der Wahrheit. Kein lokaler
    // Aufbau, keine Texte, kein Versioning — die View lädt und reagiert nur.
    // Neu geladen wird bei jeder Record-Änderung des aktuellen Paars (DataEffects).
    void Handle(ImagePairHistorieAntwort a, MessageContext ctx)
        { _data[a.PairId] = a.Eintraege.ToList(); Version++; }

    void Handle(ImagePairHistorieNichtGefunden a, MessageContext ctx)
        { _data[a.PairId] = []; Version++; }
}
