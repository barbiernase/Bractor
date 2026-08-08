using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Connection;
using Domain.ImagePair;
using Domain.Projections;
using Domain.Client.Modules.Suche;
namespace Domain.Client.Modules.Paarliste;

/// <summary>
/// Feuert die Query(s) nach jedem Kontextwechsel. Das Fenster hat der Store schon
/// ge-Reset() und den Sammel-Zähler scharf gestellt; hier wird nur dispatcht.
///
/// Kern der Multi-Query-Idee: bei aktiver Baum-Auswahl feuern wir JE BEREICH eine
/// eigene SucheImagePairs (voll geladen). Der Store sammelt die N Antworten und
/// merged sie zu einem Snapshot. Ohne Auswahl bleibt es bei einer unbegrenzten,
/// lazy-paginierten Einzel-Query.
/// </summary>
public partial class PaarlisteRefreshHandler
{
    private readonly Store _store;
    public PaarlisteRefreshHandler(Store store) => _store = store;

    IEnumerable<object> Handle(ConnectionEstablished evt, MessageContext ctx)      => LadeAktuelle();
    IEnumerable<object> Handle(AuswahlGeaendert evt, MessageContext ctx)           => LadeAktuelle();
    IEnumerable<object> Handle(SucheGeaendert evt, MessageContext ctx)             => LadeAktuelle();
    IEnumerable<object> Handle(FilterWertGeaendert evt, MessageContext ctx)        => LadeAktuelle();
    IEnumerable<object> Handle(InspektionsfilterGetoggelt evt, MessageContext ctx) => LadeAktuelle();
    IEnumerable<object> Handle(ModusGeaendert evt, MessageContext ctx)            => LadeAktuelle();
    IEnumerable<object> Handle(TagLabelingGestartet evt, MessageContext ctx)      => LadeAktuelle();
    IEnumerable<object> Handle(TagLabelingBeendet evt, MessageContext ctx)        => LadeAktuelle();

    private IEnumerable<object> LadeAktuelle()
    {
        yield return new NavigationZielGesetzt(0);   // Cursor nach dem Laden oben

        if (_store.AktiveBereiche.Count == 0)
        {
            // Keine Baum-Auswahl → EINE unbegrenzte, lazy-paginierte Query.
            yield return _store.VirtualImagePairs.BaueQuery(1);
        }
        else
        {
            // N Bereiche → N Queries. Der Store sammelt die Antworten und merged.
            foreach (var b in _store.AktiveBereiche)
                yield return new SucheImagePairs(_store.BaueBereichsFilter(b));
        }
    }
}
