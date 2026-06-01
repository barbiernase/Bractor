// ── Domain.Client/ImagePair/ViewModels/NavigationViewModel.cs ──

using Client.Infrastructure.Abstractions;
using Domain.ImagePair;

namespace Domain.Client.ImagePair;

/// <summary>
/// User-Intents für Navigation, Modus, Seitenwechsel.
///
/// Generator-managed (single return):
///   _wechsleZu, _toggleModus, _starteTagLabeling, _beendeTagLabeling
///
/// Manuell (multi-publish):
///   NavigateNext, NavigatePrev, NavigateNextOffen, VorigeSeite, NaechsteSeite
///
/// Kein BaueFilter — Seitenwechsel emittiert SeiteAngefordert,
/// der ListRefreshHandler baut den Filter allein.
/// </summary>
public partial class NavigationViewModel : IViewModel
{
    private readonly ImagePairWorkspace _workspace;

    public NavigationViewModel(ImagePairWorkspace workspace)
    {
        _workspace = workspace;
    }

    // ═══════════════════════════════════════════════════
    // GENERATOR-MANAGED
    // ═══════════════════════════════════════════════════

    private ImagePairAusgewaehlt? _wechsleZu(int index)
    {
        if (index < 0 || index >= _workspace.List.Count) return null;
        return new ImagePairAusgewaehlt(_workspace.List[index].Id);
    }

    private IClientEvent _toggleModus() => _workspace.Nav.Modus switch
    {
        ArbeitsModus.Live => new ModusGeaendert(ArbeitsModus.Inspekt),
        ArbeitsModus.Inspekt => new ModusGeaendert(ArbeitsModus.Live),
        ArbeitsModus.Tag => new TagLabelingBeendet(),
        _ => new ModusGeaendert(ArbeitsModus.Live)
    };

    private TagLabelingGestartet? _starteTagLabeling()
    {
        if (_workspace.Chart.GewaehlterTag is not { } tag) return null;
        return new TagLabelingGestartet(tag);
    }

    private TagLabelingBeendet _beendeTagLabeling() => new();

    // ═══════════════════════════════════════════════════
    // MANUELL — multi-publish
    // ═══════════════════════════════════════════════════

    public void NavigateNext()
    {
        var nav = _workspace.Nav;
        var list = _workspace.List;

        if (nav.SelectedIndex < list.Count - 1)
        {
            _publish(new ImagePairAusgewaehlt(list[nav.SelectedIndex + 1].Id));
        }
        else if (list.Seite < list.GesamtSeiten)
        {
            _publish(new NavigationZielGesetzt(0));
            _publish(new SeiteAngefordert(list.Seite + 1));
        }
    }

    public void NavigatePrev()
    {
        var nav = _workspace.Nav;
        var list = _workspace.List;

        if (nav.SelectedIndex > 0)
        {
            _publish(new ImagePairAusgewaehlt(list[nav.SelectedIndex - 1].Id));
        }
        else if (list.Seite > 1)
        {
            _publish(new NavigationZielGesetzt(-1));
            _publish(new SeiteAngefordert(list.Seite - 1));
        }
    }

    public void NavigateNextOffen()
    {
        var nav = _workspace.Nav;
        var list = _workspace.List;

        for (var i = nav.SelectedIndex + 1; i < list.Count; i++)
        {
            var entry = list[i];
            if (entry.PhysischesProduktLabel == null || entry.MenschBildpaarLabel == null)
            {
                _publish(new ImagePairAusgewaehlt(entry.Id));
                return;
            }
        }
    }

    public void VorigeSeite()
    {
        if (_workspace.List.Seite <= 1) return;
        _publish(new NavigationZielGesetzt(0));
        _publish(new SeiteAngefordert(_workspace.List.Seite - 1));
    }

    public void NaechsteSeite()
    {
        var list = _workspace.List;
        if (list.Seite >= list.GesamtSeiten) return;
        _publish(new NavigationZielGesetzt(0));
        _publish(new SeiteAngefordert(list.Seite + 1));
    }
}
