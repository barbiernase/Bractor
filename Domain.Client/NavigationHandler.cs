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
/// Liest: NavigationStore (AktuelleId), ListStore (FindById für IstInspiziert-Check)
///
/// Reagiert auf:
///   - ImagePairAusgewaehlt        → explizite User-Navigation
///   - ImagePairSuchergebnis       → implizite Auswahl nach Seitenladung
///   - ImagePairArbeitsliste       → implizite Auswahl nach Arbeitsliste
///
/// Yieldet: MarkiereAlsInspiziert (ICommand), GetImagePairHistorie (IQuery)
/// </summary>
public partial class NavigationHandler
{
    private readonly NavigationStore _nav;
    private readonly ListStore _listStore;

    public NavigationHandler(NavigationStore nav, ListStore listStore)
    {
        _nav = nav;
        _listStore = listStore;
    }

    IEnumerable<object> Handle(ImagePairAusgewaehlt evt, MessageContext ctx)
        => SeiteneffekteFuerAuswahl(evt.PairId);

    IEnumerable<object> Handle(ImagePairSuchergebnis result, MessageContext ctx)
    {
        // NavigationStore.SetzeAuswahl() hat AktuelleId bereits gesetzt,
        // aber kein ImagePairAusgewaehlt published. Handler übernimmt.
        if (_nav.AktuelleId is { } id)
            return SeiteneffekteFuerAuswahl(id);
        return [];
    }

    IEnumerable<object> Handle(ImagePairArbeitsliste liste, MessageContext ctx)
    {
        if (_nav.AktuelleId is { } id)
            return SeiteneffekteFuerAuswahl(id);
        return [];
    }

    private IEnumerable<object> SeiteneffekteFuerAuswahl(Guid pairId)
    {
        var entry = _listStore.FindById(pairId);
        if (entry == null) yield break;

        // Inspektion markieren — nur wenn noch nicht inspiziert
        if (!entry.IstInspiziert)
            yield return new MarkiereAlsInspiziert(pairId);

        // Historie laden — immer bei Paarwechsel
        yield return new GetImagePairHistorie(pairId);
    }
}
