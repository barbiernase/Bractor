// ── Domain.Client/ImagePair/Handlers/ListRefreshHandler.cs ──

using Client.Infrastructure.Abstractions;
using Client.Infrastructure.Connection;
using Domain.ImagePair;
using Domain.Projections;

namespace Domain.Client.ImagePair;

/// <summary>
/// Orchestriert Re-Queries bei Zustandsänderungen.
///
/// Einzige Stelle die BaueFilter aufruft — kein zweiter Ort.
///
/// Sync-Handler: IEnumerable Handle → läuft NACH allen Stores.
/// Liest: NavigationStore (Modus, FilterWert, NurNichtInspizierte, TagLabelingDatum)
///        ListStore (Seite, SeitenGroesse)
/// </summary>
public partial class ListRefreshHandler
{
    private readonly NavigationStore _nav;
    private readonly ListStore _list;

    public ListRefreshHandler(NavigationStore nav, ListStore list)
    {
        _nav = nav;
        _list = list;
    }

    // ═══════════════════════════════════════════════════
    // HANDLE — Connection
    // ═══════════════════════════════════════════════════

    IEnumerable<object> Handle(ConnectionEstablished evt, MessageContext ctx)
    {
        yield return new NavigationZielGesetzt(0);
        yield return new SucheImagePairs(BaueFilter(seite: 1));
    }

    // ═══════════════════════════════════════════════════
    // HANDLE — Server-Events
    // ═══════════════════════════════════════════════════

    IEnumerable<object> Handle(ImagePairErstellt evt, MessageContext ctx)
    {
        if (_nav.Modus == ArbeitsModus.Live)
        {
            yield return new NavigationZielGesetzt(0);
            yield return new SucheImagePairs(BaueFilter(seite: 1));
        }
        else
        {
            yield return new SucheImagePairs(BaueFilter());
        }
    }

    // ═══════════════════════════════════════════════════
    // HANDLE — Client-Events: Filter
    // ═══════════════════════════════════════════════════

    IEnumerable<object> Handle(FilterWertGeaendert evt, MessageContext ctx)
    {
        yield return new NavigationZielGesetzt(0);
        yield return new SucheImagePairs(BaueFilter(seite: 1));
    }

    IEnumerable<object> Handle(InspektionsfilterGetoggelt evt, MessageContext ctx)
    {
        yield return new NavigationZielGesetzt(0);
        yield return new SucheImagePairs(BaueFilter(seite: 1));
    }

    // ═══════════════════════════════════════════════════
    // HANDLE — Client-Events: Modus
    // ═══════════════════════════════════════════════════

    IEnumerable<object> Handle(ModusGeaendert evt, MessageContext ctx)
    {
        yield return new NavigationZielGesetzt(0);
        yield return new SucheImagePairs(BaueFilter(seite: 1));
    }

    IEnumerable<object> Handle(TagLabelingGestartet evt, MessageContext ctx)
    {
        yield return new NavigationZielGesetzt(0);
        yield return new SucheImagePairs(BaueFilter(seite: 1));
    }

    IEnumerable<object> Handle(TagLabelingBeendet evt, MessageContext ctx)
    {
        yield return new NavigationZielGesetzt(0);
        yield return new SucheImagePairs(BaueFilter(seite: 1));
    }

    // ═══════════════════════════════════════════════════
    // HANDLE — Client-Events: Seitenwechsel
    // ═══════════════════════════════════════════════════

    IEnumerable<object> Handle(SeiteAngefordert evt, MessageContext ctx)
    {
        yield return new SucheImagePairs(BaueFilter(seite: evt.Seite));
    }

    // ═══════════════════════════════════════════════════
    // FILTER-BUILDER — einzige Stelle im System
    // ═══════════════════════════════════════════════════

    private ImagePairFilter BaueFilter(int? seite = null)
    {
        DateTimeOffset? von = null, bis = null;

        if (_nav.Modus == ArbeitsModus.Tag && _nav.TagLabelingDatum is { } tag)
        {
            von = new DateTimeOffset(tag.Date, TimeSpan.Zero);
            bis = von.Value.AddDays(1);
        }

        var nurNichtInspizierte = _nav.NurNichtInspizierte ? true : (bool?)null;

        return _nav.FilterWert switch
        {
            "offen" => new ImagePairFilter(
                Von: von, Bis: bis,
                HatMenschLabel: false,
                NurNichtInspizierte: nurNichtInspizierte,
                Seite: seite ?? _list.Seite,
                SeitenGroesse: _list.SeitenGroesse),

            "anomalie" => new ImagePairFilter(
                Von: von, Bis: bis,
                ProduktLabel: Klassifikation.Anomalie,
                NurNichtInspizierte: nurNichtInspizierte,
                Seite: seite ?? _list.Seite,
                SeitenGroesse: _list.SeitenGroesse),

            _ => new ImagePairFilter(
                Von: von, Bis: bis,
                NurNichtInspizierte: nurNichtInspizierte,
                Seite: seite ?? _list.Seite,
                SeitenGroesse: _list.SeitenGroesse)
        };
    }
}
