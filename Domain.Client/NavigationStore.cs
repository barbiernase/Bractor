// ── Domain.Client/ImagePair/Stores/NavigationStore.cs ──

using Client.Infrastructure.Abstractions;
using CommunityToolkit.Mvvm.ComponentModel;
using Domain.Projections;

namespace Domain.Client.ImagePair;

/// <summary>
/// Navigationszustand: welches Paar ist ausgewählt, welcher Modus, welcher Filter.
///
/// Liest: ListStore (FindById, Count, Indexer) — nur im Handle.
/// Publisht nichts.
/// Subscribed als 2. Kind im Workspace — nach ListStore.
/// </summary>
public partial class NavigationStore : StoreBase
{
    private readonly ListStore _listStore;

    [ObservableProperty] private int _selectedIndex;
    [ObservableProperty] private Guid? _aktuelleId;
    [ObservableProperty] private ArbeitsModus _modus = ArbeitsModus.Live;
    [ObservableProperty] private DateTimeOffset? _tagLabelingDatum;
    [ObservableProperty] private string _filterWert = "alle";
    [ObservableProperty] private bool _nurNichtInspizierte;

    public bool IstLive => Modus == ArbeitsModus.Live;

    private int? _pendingIndex;
    private bool _ersteLadung = true;

    public NavigationStore(ListStore listStore)
    {
        _listStore = listStore;
    }

    // ═══════════════════════════════════════════════════
    // HANDLE — Query-Responses
    // ═══════════════════════════════════════════════════

    void Handle(ImagePairSuchergebnis result, MessageContext ctx)
    {
        if (_pendingIndex != null && _listStore.Count > 0)
        {
            var ziel = _pendingIndex.Value < 0
                ? _listStore.Count - 1
                : Math.Min(_pendingIndex.Value, _listStore.Count - 1);
            _pendingIndex = null;
            SetzeAuswahl(ziel);
        }
        else if (_ersteLadung && _listStore.Count > 0)
        {
            _ersteLadung = false;
            SetzeAuswahl(0);
        }
        else if (AktuelleId != null && _listStore.Count > 0)
        {
            var gefunden = false;
            for (int i = 0; i < _listStore.Count; i++)
            {
                if (_listStore[i].Id == AktuelleId)
                {
                    SelectedIndex = i;
                    gefunden = true;
                    break;
                }
            }
            if (!gefunden)
                SetzeAuswahl(0);
        }
    }

    void Handle(ImagePairArbeitsliste liste, MessageContext ctx)
    {
        if (_ersteLadung && _listStore.Count > 0)
        {
            _ersteLadung = false;
            SetzeAuswahl(0);
        }
    }

    // ═══════════════════════════════════════════════════
    // HANDLE — Client-Events
    // ═══════════════════════════════════════════════════

    void Handle(NavigationZielGesetzt evt, MessageContext ctx)
        => _pendingIndex = evt.Index;

    void Handle(ImagePairAusgewaehlt evt, MessageContext ctx)
    {
        for (int i = 0; i < _listStore.Count; i++)
        {
            if (_listStore[i].Id == evt.PairId)
            {
                SetzeAuswahl(i);
                return;
            }
        }
    }

    void Handle(FilterWertGeaendert evt, MessageContext ctx)
        => FilterWert = evt.Wert;

    void Handle(InspektionsfilterGetoggelt evt, MessageContext ctx)
        => NurNichtInspizierte = !NurNichtInspizierte;

    void Handle(ModusGeaendert evt, MessageContext ctx)
        => Modus = evt.Modus;

    void Handle(TagLabelingGestartet evt, MessageContext ctx)
    {
        TagLabelingDatum = evt.Datum;
        Modus = ArbeitsModus.Tag;
    }

    void Handle(TagLabelingBeendet evt, MessageContext ctx)
    {
        TagLabelingDatum = null;
        Modus = ArbeitsModus.Live;
    }

    // ═══════════════════════════════════════════════════
    // INTERN
    // ═══════════════════════════════════════════════════

    private void SetzeAuswahl(int index)
    {
        SelectedIndex = index;
        AktuelleId = index >= 0 && index < _listStore.Count
            ? _listStore[index].Id
            : null;
    }
}
