// ── Zielverzeichnis: Domain.Client/ImagePair/Handlers/NavigationHandler.cs ──

using Client.Infrastructure.Abstractions;
using Domain.ImagePair;
using Domain.Projections;

namespace Domain.Client.ImagePair;

/// <summary>
/// Seiteneffekte bei Paarauswahl: Inspektion markieren + Historie laden.
///
/// Sync-Handler: IEnumerable Handle → läuft NACH allen Stores.
/// NavigationStore hat AktuelleId bereits gesetzt.
///
/// Liest: ListStore (FindById für IstInspiziert-Check)
///
/// Yieldet: MarkiereAlsInspiziert (ICommand), GetImagePairHistorie (IQuery)
/// </summary>
public partial class NavigationHandler
{
    private readonly ListStore _listStore;

    public NavigationHandler(ListStore listStore)
    {
        _listStore = listStore;
    }

    IEnumerable<object> Handle(ImagePairAusgewaehlt evt, MessageContext ctx)
    {
        var entry = _listStore.FindById(evt.PairId);
        if (entry == null) yield break;

        // Inspektion markieren — nur wenn noch nicht inspiziert
        if (!entry.IstInspiziert)
            yield return new MarkiereAlsInspiziert(evt.PairId);

        // Historie laden — immer bei Paarwechsel
        yield return new GetImagePairHistorie(evt.PairId);
    }
}
